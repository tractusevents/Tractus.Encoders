#pragma once

#include <stdint.h>

#if defined(_WIN32)
#define TRACTUS_AMF_EXPORT __declspec(dllexport)
#else
#define TRACTUS_AMF_EXPORT __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef void* tractus_amf_encoder_handle;

enum tractus_amf_status
{
    TRACTUS_AMF_OK = 0,
    TRACTUS_AMF_INVALID_ARGUMENT = 1,
    TRACTUS_AMF_RUNTIME_UNAVAILABLE = 2,
    TRACTUS_AMF_ENCODER_UNAVAILABLE = 3,
    TRACTUS_AMF_OUTPUT_TOO_SMALL = 4,
    TRACTUS_AMF_ENCODE_FAILED = 5
};

enum tractus_amf_codec
{
    TRACTUS_AMF_CODEC_H264 = 1,
    TRACTUS_AMF_CODEC_HEVC = 2
};

TRACTUS_AMF_EXPORT int tractus_amf_create_encoder(
    int codec,
    int width,
    int height,
    int bitrate_bps,
    int fps_numerator,
    int fps_denominator,
    int keyframe_interval_frames,
    tractus_amf_encoder_handle* encoder);

TRACTUS_AMF_EXPORT int tractus_amf_encode_frame(
    tractus_amf_encoder_handle encoder,
    const uint8_t* uyvy,
    int uyvy_stride,
    int force_keyframe,
    uint8_t* output,
    int output_capacity,
    int* output_size,
    int* is_keyframe);

TRACTUS_AMF_EXPORT void tractus_amf_destroy_encoder(tractus_amf_encoder_handle encoder);

TRACTUS_AMF_EXPORT int tractus_amf_get_last_error(char* buffer, int capacity);

#ifdef __cplusplus
}
#endif
