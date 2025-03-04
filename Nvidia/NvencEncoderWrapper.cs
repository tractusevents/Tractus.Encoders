using Serilog;
using System;
using System.Runtime.InteropServices;

namespace Tractus.Encoders.Nvidia;
public unsafe class NvencEncoderWrapper
{
    private NvEncDelegateWrapper methods;
    private nint cudaContextPtr;
    private NV_ENCODE_API_FUNCTION_LIST functionList;
    private Guid selectedCodec;

    public bool IsH264 => this.selectedCodec == EncodeGuids.NV_ENC_CODEC_H264_GUID;
    public bool IsHEVC => this.selectedCodec == EncodeGuids.NV_ENC_CODEC_HEVC_GUID;
    public bool IsAV1 => this.selectedCodec == EncodeGuids.NV_ENC_CODEC_AV1_GUID;

    public NvencEncoderWrapper(
        int cudaDeviceNumber = 0)
    {
        var nvencList = new NV_ENCODE_API_FUNCTION_LIST()
        {
            version = NvEncodeApiVersion.GetFunctionListVersion(),
        };

        Log.Logger.Debug("Attempting to get the NVENC function list...");
        var status = NvencNativeApi.NvEncodeAPICreateInstance(ref nvencList);

        if (status != NVENCSTATUS.NV_ENC_SUCCESS)
        {
            throw new Exception("NVENC Init failed - native function list could not be loaded.");
        }

        this.methods = new NvEncDelegateWrapper(ref nvencList);

        var initCudaResult = CudaNative.cuInit(0);
        Log.Logger.Debug($"cuInit(0): {initCudaResult}");
        if(initCudaResult != CUresult.CUDA_SUCCESS)
        {
            throw new Exception("Could not init CUDA. Turn on debug logging and try again.");
        }

        var getDeviceResult = CudaNative.cuDeviceGet(out cudaDeviceNumber, 0);
        Log.Logger.Debug($"cuDeviceGet: {getDeviceResult}, CUDA Device #: {cudaDeviceNumber}");
        if (getDeviceResult != CUresult.CUDA_SUCCESS)
        {
            throw new Exception("Could not get CUDA device. Turn on debug logging and try again.");
        }

        var ctxResult = CudaNative.cuCtxCreate(out var pCtx, 0, cudaDeviceNumber);
        Log.Logger.Debug($"cuCtxCreate: {ctxResult}, pCtx: {pCtx}");
        if (ctxResult != CUresult.CUDA_SUCCESS)
        {
            throw new Exception("Could not open a CUDA context. Turn on debug logging and try again.");
        }

        this.functionList = nvencList;
        this.cudaContextPtr = pCtx;
    }

    // TODO: We need to complete the close/dispose the encoder properly so we can test it at launch.
    public void Initialize(
        Guid codec,
        int bitrateBps)
    {
        this.selectedCodec = codec;
        Log.Logger.Debug($"NVENC selecting codec {codec} ({(this.IsH264 ? "H264" : this.IsHEVC ? "H265" : this.IsAV1 ? "AV1" : "????")})");

        var openSessionParams = new NV_ENC_OPEN_ENCODE_SESSION_EX_PARAMS
        {
            version = NvEncodeApiVersion.GetFunctionListVersion(1),
            deviceType = NV_ENC_DEVICE_TYPE.NV_ENC_DEVICE_TYPE_CUDA,
            apiVersion = NvEncodeApiVersion.GetApiVersion(),
            device = this.cudaContextPtr,
            reserved = nint.Zero,
            reserved1 = new uint[253],
            reserved2 = new nint[64]
        };

        //var openSessionEx = Marshal.GetDelegateForFunctionPointer<NvEncOpenEncodeSessionEx>(nvencList.nvEncOpenEncodeSessionEx);
        var cudaNvencSessionResult = this.methods.NvEncOpenEncodeSessionEx(ref openSessionParams, out var encoderPtr);
        Log.Logger.Debug($"NvEncOpenEncodeSessionEx: {cudaNvencSessionResult}");

        if(cudaNvencSessionResult != NVENCSTATUS.NV_ENC_SUCCESS)
        {
            var lastError = this.methods.NvEncGetLastErrorString(encoderPtr);
            throw new Exception($"Could not start an NVENC session.\r\n\r\nResult code: {cudaNvencSessionResult}\r\n{lastError}");
        }

        //var getEncodeGuidCount = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodeGuidCount>(nvencList.nvEncGetEncodeGUIDCount);
        var guidCountResult = this.methods.NvEncGetEncodeGUIDCount(encoderPtr, out var guidCount);
        var guids = new Guid[guidCount];

        //var getGuids = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodeGuids>(nvencList.nvEncGetEncodeGUIDs);
        var getGuidResult = this.methods.NvEncGetEncodeGUIDs(encoderPtr, guids, guidCount, ref guidCount);
        var encoderGuid = guids.FirstOrDefault(x => x == codec);

        //var getEncodePresetCount = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodePresetCount>(nvencList.nvEncGetEncodePresetCount);
        var presetCountResult = this.methods.NvEncGetEncodePresetCount(encoderPtr, encoderGuid, out var encodePresetCount);

        //var getEncodePresets = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodePresetGUIDs>(nvencList.nvEncGetEncodePresetGUIDs);
        var presetGuids = new Guid[encodePresetCount];
        var encodePresetsResult = this.methods.NvEncGetEncodePresetGUIDs(encoderPtr, encoderGuid, presetGuids, encodePresetCount, ref encodePresetCount);

        var preset = presetGuids.FirstOrDefault(x => x == EncodePresetGuids.P1);

        //var getPresetConfig = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodePresetConfigEx>(nvencList.nvEncGetEncodePresetConfigEx);
        var presetConfig = new NV_ENC_PRESET_CONFIG
        {
            version = NvEncodeApiVersion.NV_ENC_PRESET_CONFIG_VER,
            presetConfig = new NV_ENC_CONFIG
            {
                version = NvEncodeApiVersion.NV_ENC_CONFIG_VER
            }
        };

        var presetConfigResult = this.methods.NvEncGetEncodePresetConfigEx(
            encoderPtr, 
            encoderGuid, 
            preset, 
            NV_ENC_TUNING_INFO.NV_ENC_TUNING_INFO_LOW_LATENCY, 
            ref presetConfig);


        //var getProfileGuidCount = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodeProfileGUIDCount>(nvencList.nvEncGetEncodeProfileGUIDCount);
        var getProfileGuidCountResult = this.methods.NvEncGetEncodeProfileGUIDCount(encoderPtr, encoderGuid, out var encodeProfileGuidCount);

        //var getProfileGuids = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodeProfileGUIDs>(nvencList.nvEncGetEncodeProfileGUIDs);

        var profileGuids = new Guid[encodeProfileGuidCount];
        var getProfileGuidsResult = this.methods.NvEncGetEncodeProfileGUIDs(encoderPtr, encoderGuid, profileGuids, encodeProfileGuidCount, ref encodeProfileGuidCount);

        var profileGuid = profileGuids.FirstOrDefault(x => x == EncodeProfileGuids.NV_ENC_H264_PROFILE_BASELINE_GUID);

        //var inputFormatCounts = Marshal.GetDelegateForFunctionPointer<NvEncGetInputFormatCount>(nvencList.nvEncGetInputFormatCount);
        var inputFormatCountsResult = this.methods.NvEncGetInputFormatCount(encoderPtr, encoderGuid, out var inputFmtCount);

        var inputFormats = new NV_ENC_BUFFER_FORMAT[inputFmtCount];
        //var getInputFormats = Marshal.GetDelegateForFunctionPointer<NvEncGetInputFormats>(nvencList.nvEncGetInputFormats);
        var getInputFormatResult = this.methods.NvEncGetInputFormats(encoderPtr, encoderGuid, inputFormats, inputFmtCount, ref inputFmtCount);


        //var getEncoderCaps = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodeCaps>(nvencList.nvEncGetEncodeCaps);

        var capsParam = new NV_ENC_CAPS_PARAM
        {
            version = NvEncodeApiVersion.NV_ENC_CAPS_PARAM_VER,
            capsToQuery = NV_ENC_CAPS.NV_ENC_CAPS_WIDTH_MAX
        };

        var getEncoderCapsResult = this.methods.NvEncGetEncodeCaps(encoderPtr, encoderGuid, ref capsParam, out var capsVal);

        //var initializeEncoder = Marshal.GetDelegateForFunctionPointer<NvEncInitializeEncoder>(nvencList.nvEncInitializeEncoder);

        NV_ENC_CODEC_CONFIG codecConfig;

        if (codec == EncodeGuids.NV_ENC_CODEC_H264_GUID)
        {
            codecConfig = new NV_ENC_CODEC_CONFIG
            {
                h264Config = new NV_ENC_CONFIG_H264
                {
                    
                }
            };
        }
        else if (codec == EncodeGuids.NV_ENC_CODEC_HEVC_GUID)
        {
            codecConfig = new NV_ENC_CODEC_CONFIG
            {
                hevcConfig = new NV_ENC_CONFIG_HEVC
                {
                    enableLTR = false,
                    repeatSPSPPS = true,
                    idrPeriod = 1,
                    intraRefreshPeriod = 1,
                    useBFramesAsRef = NV_ENC_BFRAME_REF_MODE.NV_ENC_BFRAME_REF_MODE_DISABLED,
                    
                }
            };
        }
        else
        {
            throw new NotImplementedException();
        }
            
        //var nvEncConfig = new NV_ENC_CONFIG
        //{
        //    version = NvEncodeApiVersion.NV_ENC_CONFIG_VER,
        //    profileGUID = profileGuid,
        //    frameIntervalP = 1,
        //    gopLength = 0xffffffffu,
        //    frameFieldMode = NV_ENC_PARAMS_FRAME_FIELD_MODE.NV_ENC_PARAMS_FRAME_FIELD_MODE_FRAME,
        //    rcParams = new NV_ENC_RC_PARAMS
        //    {
        //        version = NvEncodeApiVersion.NV_ENC_RC_PARAMS_VER,
        //        rateControlMode = NV_ENC_PARAMS_RC_MODE.NV_ENC_PARAMS_RC_CBR,
        //        averageBitRate = (uint)bitrateBps,
        //        enableAQ = true,
        //        zeroReorderDelay = true,
        //    },
        //    encodeCodecConfig = codecConfig, 
        //    reserved2 = new nint[64]
        //};

        var pNvEncConfig = Marshal.AllocHGlobal(Marshal.SizeOf<NV_ENC_CONFIG>());

        //presetConfig.presetConfig.profileGUID = profileGuid;
        presetConfig.presetConfig.frameIntervalP = 1;
        presetConfig.presetConfig.gopLength = 0x1;
        //presetConfig.presetConfig.frameFieldMode = NV_ENC_PARAMS_FRAME_FIELD_MODE.NV_ENC_PARAMS_FRAME_FIELD_MODE_FRAME;

        //presetConfig.presetConfig.encodeCodecConfig = codecConfig;
        presetConfig.presetConfig.rcParams.rateControlMode = NV_ENC_PARAMS_RC_MODE.NV_ENC_PARAMS_RC_CBR;
        presetConfig.presetConfig.rcParams.averageBitRate = (uint)bitrateBps;
        //presetConfig.presetConfig.rcParams.enableAQ = true;
        presetConfig.presetConfig.rcParams.zeroReorderDelay = true;

        //presetConfig.presetConfig.frameIntervalP = 1;
        //presetConfig.presetConfig.frameIntervalP = 1;
        //presetConfig.presetConfig.frameIntervalP = 1;
        //presetConfig.presetConfig.frameIntervalP = 1;
        //presetConfig.presetConfig.frameIntervalP = 1;



        Marshal.StructureToPtr(presetConfig.presetConfig, pNvEncConfig, false);

        var createEncoderParams = new NV_ENC_INITIALIZE_PARAMS()
        {
            version = NvEncodeApiVersion.NV_ENC_INITIALIZE_PARAMS_VER,
            enableEncodeAsync = 0,
            encodeGUID = encoderGuid,
            presetGUID = preset,
            encodeWidth = 1920,
            encodeHeight = 1080,
            frameRateNum = 60,
            frameRateDen = 1,
            enablePTD = 1,
            tuningInfo = NV_ENC_TUNING_INFO.NV_ENC_TUNING_INFO_LOW_LATENCY,
            encodeConfig = pNvEncConfig,
            enableMEOnlyMode = false,

            bufferFormat = NV_ENC_BUFFER_FORMAT.NV_ENC_BUFFER_FORMAT_NV12,
            outputStatsLevel = NV_ENC_OUTPUT_STATS_LEVEL.NV_ENC_OUTPUT_STATS_NONE,
            reserved1 = new uint[284],
            reserved2 = new nint[64]
        };

        var initializeEncoderResult = this.methods.NvEncInitializeEncoder(encoderPtr, ref createEncoderParams);
        Log.Logger.Debug($"NvEncInitializeEncoder: {initializeEncoderResult}");

        if(initializeEncoderResult != NVENCSTATUS.NV_ENC_SUCCESS)
        {
            throw new Exception($"Could not initialize NVENC encoder. {initializeEncoderResult}");
        }

        //var createInputBuffer = Marshal.GetDelegateForFunctionPointer<NvEncCreateInputBuffer>(nvencList.nvEncCreateInputBuffer);

        var createInputBufferParams = new NV_ENC_CREATE_INPUT_BUFFER
        {
            version = NvEncodeApiVersion.NV_ENC_CREATE_INPUT_BUFFER_VER,
            width = 1920,
            height = 1080,
            bufferFmt = NV_ENC_BUFFER_FORMAT.NV_ENC_BUFFER_FORMAT_NV12,
            memoryHeap = NV_ENC_MEMORY_HEAP.NV_ENC_MEMORY_HEAP_AUTOSELECT,
            reserved = 0,
            reserved1 = new uint[58],
            reserved2 = new nint[63]
        };

        var createInputBufferResult = this.methods.NvEncCreateInputBuffer(encoderPtr, ref createInputBufferParams);
        Log.Logger.Debug($"NvEncCreateInputBuffer: {createInputBufferResult}");

        //var createBitstreamBuffer = Marshal.GetDelegateForFunctionPointer<NvEncCreateBitstreamBuffer>(nvencList.nvEncCreateBitstreamBuffer);

        var createBitstreamBufferParams = new NV_ENC_CREATE_BITSTREAM_BUFFER
        {
            version = NvEncodeApiVersion.NV_ENC_CREATE_BITSTREAM_BUFFER_VER,
            size = 0,
            memoryHeap = NV_ENC_MEMORY_HEAP.NV_ENC_MEMORY_HEAP_AUTOSELECT,
            reserved = 0,
            reserved1 = new uint[58],
            reserved2 = new nint[64]
        };

        var createBitstreamBufferResult = this.methods.NvEncCreateBitstreamBuffer(encoderPtr, ref createBitstreamBufferParams);
        Log.Logger.Debug($"NvEncCreateBitstreamBuffer: {createBitstreamBufferResult}");

        this.bufferFmt = NV_ENC_BUFFER_FORMAT.NV_ENC_BUFFER_FORMAT_NV12;
        this.bitstreamBuffer = createBitstreamBufferParams.bitstreamBuffer;
        this.inputBuffer = createInputBufferParams.inputBuffer;
        this.encoderPtr = encoderPtr;

        Log.Logger.Information("NVENC encoder created.");
    }

    private nint inputBuffer;
    private nint bitstreamBuffer;
    private nint encoderPtr;
    private NV_ENC_BUFFER_FORMAT bufferFmt;

    public void FinishEncodeFrame()
    {
        //var unlockBitstream = Marshal.GetDelegateForFunctionPointer<NvEncUnlockBitstream>(this.nvencList.nvEncUnlockBitstream);
        var unlockBitstreamResult = this.methods.NvEncUnlockBitstream(this.encoderPtr, this.bitstreamBuffer);
    }

    public void EndEncode()
    {
        var picParams = new NV_ENC_PIC_PARAMS
        {
            version = NvEncodeApiVersion.NV_ENC_PIC_PARAMS_VER,
            encodePicFlags = NV_ENC_PIC_FLAGS.NV_ENC_PIC_FLAG_EOS,
            completionEvent = nint.Zero,
            inputTimeStamp = 0,
        };

        //var encodePicture = Marshal.GetDelegateForFunctionPointer<NvEncEncodePicture>(this.nvencList.nvEncEncodePicture);
        var encodePictureResult = this.methods.NvEncEncodePicture(this.encoderPtr, ref picParams);
    }

    public void Destroy()
    {
        this.methods.NvEncDestroyInputBuffer(this.encoderPtr, this.inputBuffer);
        this.methods.NvEncDestroyBitstreamBuffer(this.encoderPtr, this.bitstreamBuffer);
        this.methods.NvEncDestroyEncoder(this.encoderPtr);
        CudaNative.cuCtxDestroy(this.cudaContextPtr);
    }

    public NvencBitstreamLockResult Encode(nint nv12Data)
    {
        var lockInputBufferParams = new NV_ENC_LOCK_INPUT_BUFFER
        {
            version = NvEncodeApiVersion.NV_ENC_LOCK_INPUT_BUFFER_VER,
            inputBuffer = this.inputBuffer,
            reserved1 = new uint[251],
            reserved2 = new nint[64]
        };

        //var lockInputBuffer = Marshal.GetDelegateForFunctionPointer<NvEncLockInputBuffer>(this.nvencList.nvEncLockInputBuffer);
        var lockInputBufferResult = this.methods.NvEncLockInputBuffer(this.encoderPtr, ref lockInputBufferParams);

        var inputBufferPtr = (byte*)lockInputBufferParams.bufferDataPtr.ToPointer();

        var bufferSize = (1920 * 1080 * 3) / 2;

        Buffer.MemoryCopy(
            nv12Data.ToPointer(),
            inputBufferPtr,
            bufferSize,
            bufferSize);

        //unsafe
        //{
        //    // Known good file.
        //    var file = File.ReadAllBytes("19201080.nv12");

        //    var toWrite = (byte*)lockInputBufferParams.bufferDataPtr.ToPointer();

        //    for (var i = 0; i < file.Length; i++)
        //    {
        //        toWrite[i] = file[i];
        //    }
        //}

        //var unlockInputBuffer = Marshal.GetDelegateForFunctionPointer<NvEncUnlockInputBuffer>(this.nvencList.nvEncUnlockInputBuffer);
        var unlockInputBufferResult = this.methods.NvEncUnlockInputBuffer(this.encoderPtr, this.inputBuffer);

        NV_ENC_CODEC_PIC_PARAMS codecPicParams;

        if (this.IsH264)
        {
            codecPicParams = new NV_ENC_CODEC_PIC_PARAMS
            {
                h264PicParams = new NV_ENC_PIC_PARAMS_H264
                {

                    h264ExtPicParams = new NV_ENC_PIC_PARAMS_H264_EXT
                    {
                        mvcPicParams = new NV_ENC_PIC_PARAMS_MVC
                        {
                            version = NvEncodeApiVersion.NV_ENC_PIC_PARAMS_MVC_VER,
                        }
                    }
                }
            };
        }
        else if (this.IsHEVC)
        {
            codecPicParams = new NV_ENC_CODEC_PIC_PARAMS
            {
                hevcPicParams = new NV_ENC_PIC_PARAMS_HEVC
                {
                    
                }
            };
        }
        else
        {
            throw new NotImplementedException("AV1 not implemented");
        }

        var picParams = new NV_ENC_PIC_PARAMS
        {
            version = NvEncodeApiVersion.NV_ENC_PIC_PARAMS_VER,
            encodePicFlags = NV_ENC_PIC_FLAGS.NV_ENC_PIC_FLAG_FORCEINTRA | NV_ENC_PIC_FLAGS.NV_ENC_PIC_FLAG_OUTPUT_SPSPPS 
                | NV_ENC_PIC_FLAGS.NV_ENC_PIC_FLAG_FORCEIDR,
            bufferFmt = this.bufferFmt,
            inputWidth = 1920,
            inputHeight = 1080,
            inputBuffer = this.inputBuffer,
            outputBitstream = this.bitstreamBuffer,
            completionEvent = nint.Zero,
            inputTimeStamp = 0,
            pictureStruct = NV_ENC_PIC_STRUCT.NV_ENC_PIC_STRUCT_FRAME,
            inputPitch = 1920,
            codecPicParams = codecPicParams,
        };

        //var encodePicture = Marshal.GetDelegateForFunctionPointer<NvEncEncodePicture>(this.nvencList.nvEncEncodePicture);
        var encodePictureResult = this.methods.NvEncEncodePicture(this.encoderPtr, ref picParams);

        //var getNvencError = Marshal.GetDelegateForFunctionPointer<NvEncGetLastErrorString>(this.nvencList.nvEncGetLastErrorString);

        //var errorRaw = getNvencError(this.encoderPtr);
        //var error = Marshal.PtrToStringAnsi(errorRaw);
        //Console.WriteLine($"Error: {error}");


        //var lockBitstream = Marshal.GetDelegateForFunctionPointer<NvEncLockBitstream>(this.nvencList.nvEncLockBitstream);

        var lockBitstreamParams = new NV_ENC_LOCK_BITSTREAM
        {
            version = NvEncodeApiVersion.NV_ENC_LOCK_BITSTREAM_VER,
            outputBitstream = this.bitstreamBuffer,
            reserved1 = new uint[219],
            reserved2 = new nint[63],
            reservedInternal = new uint[8]
        };

        var lockBitstreamResult = this.methods.NvEncLockBitstream(this.encoderPtr, ref lockBitstreamParams);

        //unsafe
        //{
        //    var bitstreamSpan = new Span<byte>(
        //        (byte*)lockBitstreamParams.bitstreamBufferPtr.ToPointer(),
        //        (int)lockBitstreamParams.bitstreamSizeInBytes);

        //    File.WriteAllBytes("nvenc.h264", bitstreamSpan.ToArray());
        //}

        return new NvencBitstreamLockResult(
            lockBitstreamParams.bitstreamBufferPtr,
            (int)lockBitstreamParams.bitstreamSizeInBytes);
    }
    

}

public class NvencBitstreamLockResult
{
    public nint BufferPointer { get; }
    public int SizeInBytes { get; }

    public unsafe void AsSpan(out ReadOnlySpan<byte> span)
    {
        span = new ReadOnlySpan<byte>(this.BufferPointer.ToPointer(), this.SizeInBytes);
    }

    public NvencBitstreamLockResult(nint bufferPointer, int sizeInBytes)
    {
        this.BufferPointer = bufferPointer;
        this.SizeInBytes = sizeInBytes;
    }
}