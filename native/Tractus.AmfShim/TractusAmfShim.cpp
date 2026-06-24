#include "TractusAmfShim.h"

#include <algorithm>
#include <chrono>
#include <cstring>
#include <memory>
#include <mutex>
#include <sstream>
#include <string>
#include <thread>
#include <vector>

#if defined(__SSE2__) || defined(_M_X64) || (defined(_M_IX86_FP) && _M_IX86_FP >= 2)
#include <emmintrin.h>
#define TRACTUS_AMF_HAS_SSE2 1
#else
#define TRACTUS_AMF_HAS_SSE2 0
#endif

#if defined(_WIN32)
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#else
#include <dlfcn.h>
#endif

#include "components/VideoEncoderHEVC.h"
#include "components/VideoEncoderVCE.h"
#include "core/Buffer.h"
#include "core/Context.h"
#include "core/Factory.h"
#include "core/Surface.h"

namespace
{
thread_local std::string g_last_error;

class AmfRuntime
{
public:
    static AMF_RESULT AddRef()
    {
        std::lock_guard<std::mutex> lock(Mutex());
        LastError().clear();

        if (RefCount() > 0)
        {
            RefCount()++;
            return AMF_OK;
        }

#if defined(_WIN32)
        Library() = static_cast<void*>(LoadLibraryW(AMF_DLL_NAME));
#else
        Library() = dlopen(AMF_DLL_NAMEA, RTLD_LAZY | RTLD_LOCAL);
#endif

        if (Library() == nullptr)
        {
            LastError() = std::string("Could not load AMD AMF runtime library ") + AMF_DLL_NAMEA + ".";
            return AMF_FAIL;
        }

        auto init = reinterpret_cast<AMFInit_Fn>(GetSymbol(AMF_INIT_FUNCTION_NAME));
        auto query_version = reinterpret_cast<AMFQueryVersion_Fn>(GetSymbol(AMF_QUERY_VERSION_FUNCTION_NAME));
        if (init == nullptr || query_version == nullptr)
        {
            LastError() = "AMD AMF runtime library is missing AMFInit or AMFQueryVersion.";
            ReleaseLibrary();
            return AMF_FAIL;
        }

        RuntimeVersion() = 0;
        const AMF_RESULT version_result = query_version(&RuntimeVersion());
        const amf_uint64 requested_version = version_result == AMF_OK && RuntimeVersion() != 0 && RuntimeVersion() < AMF_FULL_VERSION
            ? RuntimeVersion()
            : static_cast<amf_uint64>(AMF_FULL_VERSION);

        const AMF_RESULT init_result = init(requested_version, &Factory());
        if (init_result != AMF_OK)
        {
            std::ostringstream builder;
            builder << "AMFInit failed with AMF_RESULT " << static_cast<int>(init_result)
                << ". Header API " << FormatVersion(AMF_FULL_VERSION)
                << ", runtime query "
                << (version_result == AMF_OK ? FormatVersion(RuntimeVersion()) : std::string("AMFQueryVersion failed with AMF_RESULT ") + std::to_string(static_cast<int>(version_result)))
                << ", requested API " << FormatVersion(requested_version) << ".";
            LastError() = builder.str();
            ReleaseLibrary();
            return init_result;
        }

        RefCount() = 1;
        return AMF_OK;
    }

    static void Release()
    {
        std::lock_guard<std::mutex> lock(Mutex());

        if (RefCount() <= 0)
        {
            return;
        }

        RefCount()--;
        if (RefCount() == 0)
        {
            Factory() = nullptr;
            RuntimeVersion() = 0;
            ReleaseLibrary();
        }
    }

    static amf::AMFFactory* FactoryPtr()
    {
        return Factory();
    }

    static const std::string& ErrorText()
    {
        return LastError();
    }

private:
    static std::mutex& Mutex()
    {
        static std::mutex mutex;
        return mutex;
    }

    static void*& Library()
    {
        static void* library = nullptr;
        return library;
    }

    static amf::AMFFactory*& Factory()
    {
        static amf::AMFFactory* factory = nullptr;
        return factory;
    }

    static amf_uint64& RuntimeVersion()
    {
        static amf_uint64 version = 0;
        return version;
    }

    static int& RefCount()
    {
        static int ref_count = 0;
        return ref_count;
    }

    static std::string& LastError()
    {
        static std::string last_error;
        return last_error;
    }

    static std::string FormatVersion(amf_uint64 version)
    {
        std::ostringstream builder;
        builder
            << ((version >> 48) & 0xFFFF)
            << "."
            << ((version >> 32) & 0xFFFF)
            << "."
            << ((version >> 16) & 0xFFFF)
            << "."
            << (version & 0xFFFF);
        return builder.str();
    }

    static void* GetSymbol(const char* name)
    {
#if defined(_WIN32)
        return reinterpret_cast<void*>(GetProcAddress(static_cast<HMODULE>(Library()), name));
#else
        return dlsym(Library(), name);
#endif
    }

    static void ReleaseLibrary()
    {
        if (Library() == nullptr)
        {
            return;
        }

#if defined(_WIN32)
        FreeLibrary(static_cast<HMODULE>(Library()));
#else
        dlclose(Library());
#endif

        Library() = nullptr;
    }
};

std::string ResultText(const char* action, AMF_RESULT result)
{
    std::ostringstream builder;
    builder << action << " failed with AMF_RESULT " << static_cast<int>(result);
    return builder.str();
}

int Fail(int status, const std::string& message)
{
    g_last_error = message;
    return status;
}

const wchar_t* ComponentName(int codec)
{
    switch (codec)
    {
    case TRACTUS_AMF_CODEC_H264:
        return AMFVideoEncoderVCE_AVC;
    case TRACTUS_AMF_CODEC_HEVC:
        return AMFVideoEncoder_HEVC;
    default:
        return nullptr;
    }
}

void CopyError(char* buffer, int capacity)
{
    if (buffer == nullptr || capacity <= 0)
    {
        return;
    }

    const auto bytes_to_copy = std::min(static_cast<int>(g_last_error.size()), capacity - 1);
    if (bytes_to_copy > 0)
    {
        std::memcpy(buffer, g_last_error.data(), static_cast<size_t>(bytes_to_copy));
    }

    buffer[bytes_to_copy] = '\0';
}

void ConvertUyvyToNv12(
    const uint8_t* uyvy,
    int uyvy_stride,
    uint8_t* y_plane,
    int y_pitch,
    uint8_t* uv_plane,
    int uv_pitch,
    int width,
    int height)
{
    for (int row = 0; row < height; row++)
    {
        const uint8_t* src = uyvy + (static_cast<size_t>(row) * uyvy_stride);
        uint8_t* dst_y = y_plane + (static_cast<size_t>(row) * y_pitch);

#if TRACTUS_AMF_HAS_SSE2
        int col = 0;
        for (; col <= width - 8; col += 8)
        {
            const auto packed = _mm_loadu_si128(reinterpret_cast<const __m128i*>(src + (col * 2)));
            const auto y_words = _mm_srli_epi16(packed, 8);
            const auto y_bytes = _mm_packus_epi16(y_words, y_words);
            _mm_storel_epi64(reinterpret_cast<__m128i*>(dst_y + col), y_bytes);
        }
#else
        int col = 0;
#endif

        for (; col < width; col += 2)
        {
            const int source_offset = col * 2;
            dst_y[col] = src[source_offset + 1];
            dst_y[col + 1] = src[source_offset + 3];
        }
    }

    for (int row = 0; row < height; row += 2)
    {
        const uint8_t* top = uyvy + (static_cast<size_t>(row) * uyvy_stride);
        const uint8_t* bottom = row + 1 < height
            ? uyvy + (static_cast<size_t>(row + 1) * uyvy_stride)
            : top;
        uint8_t* dst_uv = uv_plane + (static_cast<size_t>(row / 2) * uv_pitch);

#if TRACTUS_AMF_HAS_SSE2
        int col = 0;
        const auto low_byte_mask = _mm_set1_epi16(0x00FF);
        const auto one = _mm_set1_epi16(1);

        for (; col <= width - 8; col += 8)
        {
            const auto top_words = _mm_and_si128(
                _mm_loadu_si128(reinterpret_cast<const __m128i*>(top + (col * 2))),
                low_byte_mask);
            const auto bottom_words = _mm_and_si128(
                _mm_loadu_si128(reinterpret_cast<const __m128i*>(bottom + (col * 2))),
                low_byte_mask);
            const auto averaged = _mm_srli_epi16(_mm_add_epi16(_mm_add_epi16(top_words, bottom_words), one), 1);
            const auto uv_bytes = _mm_packus_epi16(averaged, averaged);
            _mm_storel_epi64(reinterpret_cast<__m128i*>(dst_uv + col), uv_bytes);
        }
#else
        int col = 0;
#endif

        for (; col < width; col += 2)
        {
            const int source_offset = col * 2;
            const int u = static_cast<int>(top[source_offset])
                + static_cast<int>(bottom[source_offset]);
            const int v = static_cast<int>(top[source_offset + 2])
                + static_cast<int>(bottom[source_offset + 2]);

            dst_uv[col] = static_cast<uint8_t>((u + 1) / 2);
            dst_uv[col + 1] = static_cast<uint8_t>((v + 1) / 2);
        }
    }
}

class Encoder
{
public:
    Encoder(int codec, int width, int height, int bitrate_bps, int fps_numerator, int fps_denominator, int keyframe_interval_frames)
        : codec_(codec),
          width_(width),
          height_(height),
          bitrate_bps_(std::max(1, bitrate_bps)),
          fps_numerator_(std::max(1, fps_numerator)),
          fps_denominator_(std::max(1, fps_denominator)),
          keyframe_interval_frames_(std::max(1, keyframe_interval_frames))
    {
    }

    ~Encoder()
    {
        if (encoder_ != nullptr)
        {
            encoder_->Terminate();
            encoder_ = nullptr;
        }

        surfaces_.clear();
        context_ = nullptr;
        AmfRuntime::Release();
    }

    int Initialize()
    {
        const wchar_t* component_name = ComponentName(codec_);
        if (component_name == nullptr)
        {
            return Fail(TRACTUS_AMF_INVALID_ARGUMENT, "Unsupported AMF codec.");
        }

        const AMF_RESULT init_result = AmfRuntime::AddRef();
        if (init_result != AMF_OK)
        {
            return Fail(
                TRACTUS_AMF_RUNTIME_UNAVAILABLE,
                AmfRuntime::ErrorText().empty()
                    ? ResultText("AMF runtime initialization", init_result)
                    : AmfRuntime::ErrorText());
        }

        amf::AMFFactory* factory = AmfRuntime::FactoryPtr();
        if (factory == nullptr)
        {
            AmfRuntime::Release();
            return Fail(TRACTUS_AMF_RUNTIME_UNAVAILABLE, "AMF factory is not available.");
        }

        AMF_RESULT result = factory->CreateContext(&context_);
        if (result != AMF_OK)
        {
            AmfRuntime::Release();
            return Fail(TRACTUS_AMF_RUNTIME_UNAVAILABLE, ResultText("AMF context creation", result));
        }

        result = factory->CreateComponent(context_, component_name, &encoder_);
        if (result != AMF_OK)
        {
            AmfRuntime::Release();
            return Fail(TRACTUS_AMF_ENCODER_UNAVAILABLE, ResultText("AMF encoder creation", result));
        }

        if (codec_ == TRACTUS_AMF_CODEC_H264)
        {
            ConfigureH264();
        }
        else
        {
            ConfigureHevc();
        }

        result = encoder_->Init(amf::AMF_SURFACE_NV12, width_, height_);
        if (result != AMF_OK)
        {
            AmfRuntime::Release();
            return Fail(TRACTUS_AMF_ENCODER_UNAVAILABLE, ResultText("AMF encoder initialization", result));
        }

        const int pool_status = CreateSurfacePool();
        if (pool_status != TRACTUS_AMF_OK)
        {
            return pool_status;
        }

        return TRACTUS_AMF_OK;
    }

    int Encode(
        const uint8_t* uyvy,
        int uyvy_stride,
        int force_keyframe,
        uint8_t* output,
        int output_capacity,
        int* output_size,
        int* is_keyframe)
    {
        if (encoder_ == nullptr || context_ == nullptr)
        {
            return Fail(TRACTUS_AMF_INVALID_ARGUMENT, "AMF encoder is not initialized.");
        }

        if (uyvy == nullptr || output == nullptr || output_size == nullptr || is_keyframe == nullptr)
        {
            return Fail(TRACTUS_AMF_INVALID_ARGUMENT, "AMF encode was given a null pointer.");
        }

        *output_size = 0;
        *is_keyframe = 0;

        if (uyvy_stride < width_ * 2 || output_capacity <= 0)
        {
            return Fail(TRACTUS_AMF_INVALID_ARGUMENT, "AMF encode was given an invalid stride or output capacity.");
        }

        amf::AMFSurfacePtr surface;
        int surface_status = AcquireSurface(surface);
        if (surface_status != TRACTUS_AMF_OK)
        {
            return surface_status;
        }

        amf::AMFPlane* y_plane = surface->GetPlane(amf::AMF_PLANE_Y);
        amf::AMFPlane* uv_plane = surface->GetPlane(amf::AMF_PLANE_UV);
        if (y_plane == nullptr || uv_plane == nullptr)
        {
            return Fail(TRACTUS_AMF_ENCODE_FAILED, "AMF NV12 surface did not expose Y and UV planes.");
        }

        ConvertUyvyToNv12(
            uyvy,
            uyvy_stride,
            static_cast<uint8_t*>(y_plane->GetNative()),
            y_plane->GetHPitch(),
            static_cast<uint8_t*>(uv_plane->GetNative()),
            uv_plane->GetHPitch(),
            width_,
            height_);

        surface->SetPts(frame_index_++);

        ApplyPerFrameProperties(surface, force_keyframe, is_keyframe);

        AMF_RESULT submit_result = AMF_INPUT_FULL;
        for (int attempt = 0; attempt < 200; attempt++)
        {
            submit_result = encoder_->SubmitInput(surface);
            if (submit_result == AMF_OK)
            {
                break;
            }

            if (submit_result != AMF_INPUT_FULL)
            {
                return Fail(TRACTUS_AMF_ENCODE_FAILED, ResultText("AMF input submission", submit_result));
            }

            std::this_thread::sleep_for(std::chrono::milliseconds(1));
        }

        if (submit_result != AMF_OK)
        {
            return Fail(TRACTUS_AMF_ENCODE_FAILED, "AMF input queue stayed full.");
        }

        return QueryOutput(output, output_capacity, output_size);
    }

private:
    int CreateSurfacePool()
    {
        surfaces_.clear();
        surfaces_.reserve(SurfacePoolSize);
        next_surface_index_ = 0;

        for (int i = 0; i < SurfacePoolSize; i++)
        {
            amf::AMFSurfacePtr surface;
            const AMF_RESULT result = context_->AllocSurface(amf::AMF_MEMORY_HOST, amf::AMF_SURFACE_NV12, width_, height_, &surface);
            if (result != AMF_OK || surface == nullptr)
            {
                surfaces_.clear();
                return Fail(TRACTUS_AMF_ENCODE_FAILED, ResultText("AMF NV12 surface allocation", result));
            }

            if (surface->GetPlane(amf::AMF_PLANE_Y) == nullptr || surface->GetPlane(amf::AMF_PLANE_UV) == nullptr)
            {
                surfaces_.clear();
                return Fail(TRACTUS_AMF_ENCODE_FAILED, "AMF NV12 surface did not expose Y and UV planes.");
            }

            surfaces_.push_back(surface);
        }

        return TRACTUS_AMF_OK;
    }

    int AcquireSurface(amf::AMFSurfacePtr& surface)
    {
        if (surfaces_.empty())
        {
            return Fail(TRACTUS_AMF_ENCODE_FAILED, "AMF surface pool is empty.");
        }

        surface = surfaces_[next_surface_index_];
        next_surface_index_ = (next_surface_index_ + 1) % surfaces_.size();
        return TRACTUS_AMF_OK;
    }

    void ApplyPerFrameProperties(const amf::AMFSurfacePtr& surface, int force_keyframe, int* is_keyframe)
    {
        if (codec_ == TRACTUS_AMF_CODEC_H264)
        {
            surface->SetProperty(AMF_VIDEO_ENCODER_FORCE_PICTURE_TYPE, AMF_VIDEO_ENCODER_PICTURE_TYPE_NONE);
            surface->SetProperty(AMF_VIDEO_ENCODER_INSERT_SPS, false);
            surface->SetProperty(AMF_VIDEO_ENCODER_INSERT_PPS, false);

            if (force_keyframe != 0)
            {
                surface->SetProperty(AMF_VIDEO_ENCODER_FORCE_PICTURE_TYPE, AMF_VIDEO_ENCODER_PICTURE_TYPE_IDR);
                surface->SetProperty(AMF_VIDEO_ENCODER_INSERT_SPS, true);
                surface->SetProperty(AMF_VIDEO_ENCODER_INSERT_PPS, true);
                *is_keyframe = 1;
            }
        }
        else
        {
            surface->SetProperty(AMF_VIDEO_ENCODER_HEVC_FORCE_PICTURE_TYPE, AMF_VIDEO_ENCODER_HEVC_PICTURE_TYPE_NONE);
            surface->SetProperty(AMF_VIDEO_ENCODER_HEVC_INSERT_HEADER, false);

            if (force_keyframe != 0)
            {
                surface->SetProperty(AMF_VIDEO_ENCODER_HEVC_FORCE_PICTURE_TYPE, AMF_VIDEO_ENCODER_HEVC_PICTURE_TYPE_IDR);
                surface->SetProperty(AMF_VIDEO_ENCODER_HEVC_INSERT_HEADER, true);
                *is_keyframe = 1;
            }
        }
    }

    void ConfigureH264()
    {
        encoder_->SetProperty(AMF_VIDEO_ENCODER_USAGE, AMF_VIDEO_ENCODER_USAGE_ULTRA_LOW_LATENCY);
        encoder_->SetProperty(AMF_VIDEO_ENCODER_FRAMESIZE, ::AMFConstructSize(width_, height_));
        encoder_->SetProperty(AMF_VIDEO_ENCODER_FRAMERATE, ::AMFConstructRate(fps_numerator_, fps_denominator_));
        encoder_->SetProperty(AMF_VIDEO_ENCODER_RATE_CONTROL_METHOD, AMF_VIDEO_ENCODER_RATE_CONTROL_METHOD_CBR);
        encoder_->SetProperty(AMF_VIDEO_ENCODER_TARGET_BITRATE, bitrate_bps_);
        encoder_->SetProperty(AMF_VIDEO_ENCODER_PEAK_BITRATE, bitrate_bps_);
        encoder_->SetProperty(AMF_VIDEO_ENCODER_QUALITY_PRESET, AMF_VIDEO_ENCODER_QUALITY_PRESET_SPEED);
        encoder_->SetProperty(AMF_VIDEO_ENCODER_LOWLATENCY_MODE, true);
        encoder_->SetProperty(AMF_VIDEO_ENCODER_B_PIC_PATTERN, 0);
        encoder_->SetProperty(AMF_VIDEO_ENCODER_HEADER_INSERTION_SPACING, keyframe_interval_frames_);
        encoder_->SetProperty(AMF_VIDEO_ENCODER_IDR_PERIOD, keyframe_interval_frames_);
        encoder_->SetProperty(AMF_VIDEO_ENCODER_INTRA_PERIOD, keyframe_interval_frames_);
        encoder_->SetProperty(AMF_VIDEO_ENCODER_QUERY_TIMEOUT, 1);
    }

    void ConfigureHevc()
    {
        encoder_->SetProperty(AMF_VIDEO_ENCODER_HEVC_USAGE, AMF_VIDEO_ENCODER_HEVC_USAGE_ULTRA_LOW_LATENCY);
        encoder_->SetProperty(AMF_VIDEO_ENCODER_HEVC_FRAMESIZE, ::AMFConstructSize(width_, height_));
        encoder_->SetProperty(AMF_VIDEO_ENCODER_HEVC_FRAMERATE, ::AMFConstructRate(fps_numerator_, fps_denominator_));
        encoder_->SetProperty(AMF_VIDEO_ENCODER_HEVC_RATE_CONTROL_METHOD, AMF_VIDEO_ENCODER_HEVC_RATE_CONTROL_METHOD_CBR);
        encoder_->SetProperty(AMF_VIDEO_ENCODER_HEVC_TARGET_BITRATE, bitrate_bps_);
        encoder_->SetProperty(AMF_VIDEO_ENCODER_HEVC_PEAK_BITRATE, bitrate_bps_);
        encoder_->SetProperty(AMF_VIDEO_ENCODER_HEVC_QUALITY_PRESET, AMF_VIDEO_ENCODER_HEVC_QUALITY_PRESET_SPEED);
        encoder_->SetProperty(AMF_VIDEO_ENCODER_HEVC_LOWLATENCY_MODE, true);
        encoder_->SetProperty(AMF_VIDEO_ENCODER_HEVC_HEADER_INSERTION_MODE, AMF_VIDEO_ENCODER_HEVC_HEADER_INSERTION_MODE_GOP_ALIGNED);
        encoder_->SetProperty(AMF_VIDEO_ENCODER_HEVC_NUM_GOPS_PER_IDR, 1);
        encoder_->SetProperty(AMF_VIDEO_ENCODER_HEVC_GOP_SIZE, keyframe_interval_frames_);
        encoder_->SetProperty(AMF_VIDEO_ENCODER_HEVC_QUERY_TIMEOUT, 1);
    }

    int QueryOutput(uint8_t* output, int output_capacity, int* output_size)
    {
        for (int attempt = 0; attempt < 500; attempt++)
        {
            amf::AMFDataPtr data;
            const AMF_RESULT result = encoder_->QueryOutput(&data);
            if (result == AMF_OK && data != nullptr)
            {
                amf::AMFBufferPtr buffer(data);
                if (buffer == nullptr || buffer->GetNative() == nullptr)
                {
                    return Fail(TRACTUS_AMF_ENCODE_FAILED, "AMF encoder output was not an AMFBuffer.");
                }

                const int buffer_size = static_cast<int>(buffer->GetSize());
                *output_size = buffer_size;

                if (buffer_size > output_capacity)
                {
                    return Fail(TRACTUS_AMF_OUTPUT_TOO_SMALL, "AMF encoder output exceeded the provided output buffer.");
                }

                std::memcpy(output, buffer->GetNative(), static_cast<size_t>(buffer_size));
                return TRACTUS_AMF_OK;
            }

            if (result != AMF_OK && result != AMF_REPEAT)
            {
                return Fail(TRACTUS_AMF_ENCODE_FAILED, ResultText("AMF output query", result));
            }

            std::this_thread::sleep_for(std::chrono::milliseconds(1));
        }

        return Fail(TRACTUS_AMF_ENCODE_FAILED, "AMF encoder did not return output within 500 ms.");
    }

    int codec_;
    int width_;
    int height_;
    int bitrate_bps_;
    int fps_numerator_;
    int fps_denominator_;
    int keyframe_interval_frames_;
    int64_t frame_index_ = 0;

    static constexpr int SurfacePoolSize = 4;
    std::vector<amf::AMFSurfacePtr> surfaces_;
    size_t next_surface_index_ = 0;

    amf::AMFContextPtr context_;
    amf::AMFComponentPtr encoder_;
};
}

int tractus_amf_create_encoder(
    int codec,
    int width,
    int height,
    int bitrate_bps,
    int fps_numerator,
    int fps_denominator,
    int keyframe_interval_frames,
    tractus_amf_encoder_handle* encoder)
{
    if (encoder == nullptr)
    {
        return Fail(TRACTUS_AMF_INVALID_ARGUMENT, "AMF encoder output handle was null.");
    }

    *encoder = nullptr;

    if (width <= 0 || height <= 0 || (width % 2) != 0 || (height % 2) != 0)
    {
        return Fail(TRACTUS_AMF_INVALID_ARGUMENT, "AMF encoder requires positive even dimensions.");
    }

    auto instance = std::make_unique<Encoder>(
        codec,
        width,
        height,
        bitrate_bps,
        fps_numerator,
        fps_denominator,
        keyframe_interval_frames);

    const int status = instance->Initialize();
    if (status != TRACTUS_AMF_OK)
    {
        return status;
    }

    *encoder = instance.release();
    return TRACTUS_AMF_OK;
}

int tractus_amf_encode_frame(
    tractus_amf_encoder_handle encoder,
    const uint8_t* uyvy,
    int uyvy_stride,
    int force_keyframe,
    uint8_t* output,
    int output_capacity,
    int* output_size,
    int* is_keyframe)
{
    if (encoder == nullptr)
    {
        return Fail(TRACTUS_AMF_INVALID_ARGUMENT, "AMF encoder handle was null.");
    }

    return static_cast<Encoder*>(encoder)->Encode(
        uyvy,
        uyvy_stride,
        force_keyframe,
        output,
        output_capacity,
        output_size,
        is_keyframe);
}

void tractus_amf_destroy_encoder(tractus_amf_encoder_handle encoder)
{
    delete static_cast<Encoder*>(encoder);
}

int tractus_amf_get_last_error(char* buffer, int capacity)
{
    CopyError(buffer, capacity);
    return static_cast<int>(g_last_error.size());
}
