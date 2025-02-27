using System;
using System.Runtime.InteropServices;
using static Tractus.Encoders.Nvidia.NvencNativeApi;

namespace Tractus.Encoders.Nvidia;
public static class NvEncodeApiVersion
{
    // The NVENCAPI version values.
    private const uint NVENCAPI_MAJOR_VERSION = 13;
    private const uint NVENCAPI_MINOR_VERSION = 0;

    // Computes NVENCAPI_VERSION: (major | (minor << 24))
    private static uint GetNvEncodeApiVersion()
    {
        return NVENCAPI_MAJOR_VERSION | (NVENCAPI_MINOR_VERSION << 24);
    }

    /// <summary>
    /// Mimics the NVENCAPI_STRUCT_VERSION(ver) macro:
    /// ((uint32_t)NVENCAPI_VERSION | ((ver)<<16) | (0x7 << 28))
    /// For NV_ENCODE_API_FUNCTION_LIST_VER, ver should be 2.
    /// </summary>
    /// <param name="ver">The structure version component, typically 2.</param>
    /// <returns>The computed version number.</returns>
    public static uint GetFunctionListVersion(uint ver = 2)
    {
        return GetNvEncodeApiVersion() | (ver << 16) | (0x7u << 28);
    }

    public static uint NVENCAPI_STRUCT_VERSION(uint ver)
    {
        return GetApiVersion() | (ver << 16) | (0x7u << 28);
    }

    public static uint GetApiVersion()
    {
        return NVENCAPI_MAJOR_VERSION | (NVENCAPI_MINOR_VERSION << 24);
    }

    public static uint NV_ENC_CREATE_INPUT_BUFFER_VER => NVENCAPI_STRUCT_VERSION(2);
    public static uint NV_ENC_INITIALIZE_PARAMS_VER => NVENCAPI_STRUCT_VERSION(7) | (1u << 31);
    public static uint NV_ENC_CAPS_PARAM_VER => NVENCAPI_STRUCT_VERSION(1);
    public static uint NV_ENC_PRESET_CONFIG_VER => NVENCAPI_STRUCT_VERSION(5) | (1u << 31);
    public static uint NV_ENC_CONFIG_VER => NVENCAPI_STRUCT_VERSION(9) | (1u << 31);
    public static uint NV_ENC_RC_PARAMS_VER => NVENCAPI_STRUCT_VERSION(1);
    public static uint NV_ENC_CREATE_BITSTREAM_BUFFER_VER => NVENCAPI_STRUCT_VERSION(1);
    public static uint NV_ENC_LOCK_BITSTREAM_VER => NVENCAPI_STRUCT_VERSION(2) | (1u << 31);
    public static uint NV_ENC_LOCK_INPUT_BUFFER_VER => NVENCAPI_STRUCT_VERSION(1);
    public static uint NV_ENC_PIC_PARAMS_VER => NVENCAPI_STRUCT_VERSION(7) | (1u << 31);
    public static uint NV_ENC_PIC_PARAMS_MVC_VER => NVENCAPI_STRUCT_VERSION(1);

    // #define NV_ENC_RECONFIGURE_PARAMS_VER (NVENCAPI_STRUCT_VERSION(2) | ( 1<<31 ))
    public static uint NV_ENC_RECONFIGURE_PARAMS_VER => NVENCAPI_STRUCT_VERSION(2) | (1u << 31);
}

public enum NV_ENC_TUNING_INFO
{
    NV_ENC_TUNING_INFO_UNDEFINED = 0,                                     /**< Undefined tuningInfo. Invalid value for encoding. */
    NV_ENC_TUNING_INFO_HIGH_QUALITY = 1,                                     /**< Tune presets for latency tolerant encoding.*/
    NV_ENC_TUNING_INFO_LOW_LATENCY = 2,                                     /**< Tune presets for low latency streaming.*/
    NV_ENC_TUNING_INFO_ULTRA_LOW_LATENCY = 3,                                     /**< Tune presets for ultra low latency streaming.*/
    NV_ENC_TUNING_INFO_LOSSLESS = 4,                                     /**< Tune presets for lossless encoding.*/
    NV_ENC_TUNING_INFO_ULTRA_HIGH_QUALITY = 5,                                    /**< Tune presets for latency tolerant encoding for higher quality. Only supported for HEVC and AV1 on Turing+ architectures */
    NV_ENC_TUNING_INFO_COUNT
}

public enum NV_ENC_DEVICE_TYPE
{
    NV_ENC_DEVICE_TYPE_DIRECTX = 0x0,   /**< encode device type is a directx9 device */
    NV_ENC_DEVICE_TYPE_CUDA = 0x1,   /**< encode device type is a cuda device */
    NV_ENC_DEVICE_TYPE_OPENGL = 0x2    /**< encode device type is an OpenGL device. */
}

public enum NV_ENC_PARAMS_FRAME_FIELD_MODE
{
    NV_ENC_PARAMS_FRAME_FIELD_MODE_FRAME = 0x01,  /**< Frame mode */
    NV_ENC_PARAMS_FRAME_FIELD_MODE_FIELD = 0x02,  /**< Field mode */
    NV_ENC_PARAMS_FRAME_FIELD_MODE_MBAFF = 0x03   /**< MB adaptive frame/field */
}

public enum NV_ENC_PARAMS_RC_MODE
{
    NV_ENC_PARAMS_RC_CONSTQP = 0x0,       /**< Constant QP mode */
    NV_ENC_PARAMS_RC_VBR = 0x1,       /**< Variable bitrate mode */
    NV_ENC_PARAMS_RC_CBR = 0x2,       /**< Constant bitrate mode */
}

public enum NV_ENC_MULTI_PASS
{
    NV_ENC_MULTI_PASS_DISABLED = 0x0,        /**< Single Pass */
    NV_ENC_TWO_PASS_QUARTER_RESOLUTION = 0x1,        /**< Two Pass encoding is enabled where first Pass is quarter resolution */
    NV_ENC_TWO_PASS_FULL_RESOLUTION = 0x2,        /**< Two Pass encoding is enabled where first Pass is full resolution */
}

public enum NV_ENC_STATE_RESTORE_TYPE
{
    NV_ENC_STATE_RESTORE_FULL = 0x01,      /**< Restore full encoder state */
    NV_ENC_STATE_RESTORE_RATE_CONTROL = 0x02,      /**< Restore only rate control state */
    NV_ENC_STATE_RESTORE_ENCODE = 0x03,      /**< Restore full encoder state except for rate control state */
}

public enum NV_ENC_OUTPUT_STATS_LEVEL
{
    NV_ENC_OUTPUT_STATS_NONE = 0,             /** No output stats */
    NV_ENC_OUTPUT_STATS_BLOCK_LEVEL = 1,             /** Output stats for every block.
                                                           Block represents a CTB for HEVC, macroblock for H.264, super block for AV1 */
    NV_ENC_OUTPUT_STATS_ROW_LEVEL = 2,             /** Output stats for every row.
                                                           Row represents a CTB row for HEVC, macroblock row for H.264, super block row for AV1 */
}

public enum NV_ENC_EMPHASIS_MAP_LEVEL
{
    NV_ENC_EMPHASIS_MAP_LEVEL_0 = 0x0,       /**< Emphasis Map Level 0, for zero Delta QP value */
    NV_ENC_EMPHASIS_MAP_LEVEL_1 = 0x1,       /**< Emphasis Map Level 1, for very low Delta QP value */
    NV_ENC_EMPHASIS_MAP_LEVEL_2 = 0x2,       /**< Emphasis Map Level 2, for low Delta QP value */
    NV_ENC_EMPHASIS_MAP_LEVEL_3 = 0x3,       /**< Emphasis Map Level 3, for medium Delta QP value */
    NV_ENC_EMPHASIS_MAP_LEVEL_4 = 0x4,       /**< Emphasis Map Level 4, for high Delta QP value */
    NV_ENC_EMPHASIS_MAP_LEVEL_5 = 0x5        /**< Emphasis Map Level 5, for very high Delta QP value */
}

public enum NV_ENC_QP_MAP_MODE
{
    NV_ENC_QP_MAP_DISABLED = 0x0,             /**< Value in NV_ENC_PIC_PARAMS::qpDeltaMap have no effect. */
    NV_ENC_QP_MAP_EMPHASIS = 0x1,             /**< Value in NV_ENC_PIC_PARAMS::qpDeltaMap will be treated as Emphasis level. Currently this is only supported for H264 */
    NV_ENC_QP_MAP_DELTA = 0x2,             /**< Value in NV_ENC_PIC_PARAMS::qpDeltaMap will be treated as QP delta map. */
    NV_ENC_QP_MAP = 0x3,             /**< Currently This is not supported. Value in NV_ENC_PIC_PARAMS::qpDeltaMap will be treated as QP value.   */
}

public enum NV_ENC_PIC_STRUCT
{
    NV_ENC_PIC_STRUCT_FRAME = 0x01,                 /**< Progressive frame */
    NV_ENC_PIC_STRUCT_FIELD_TOP_BOTTOM = 0x02,                 /**< Field encoding top field first */
    NV_ENC_PIC_STRUCT_FIELD_BOTTOM_TOP = 0x03                  /**< Field encoding bottom field first */
}

public enum NV_ENC_DISPLAY_PIC_STRUCT
{
    NV_ENC_PIC_STRUCT_DISPLAY_FRAME = 0x00,                 /**< Field encoding top field first */
    NV_ENC_PIC_STRUCT_DISPLAY_FIELD_TOP_BOTTOM = 0x01,                 /**< Field encoding top field first */
    NV_ENC_PIC_STRUCT_DISPLAY_FIELD_BOTTOM_TOP = 0x02,                 /**< Field encoding bottom field first */
    NV_ENC_PIC_STRUCT_DISPLAY_FRAME_DOUBLING = 0x03,                 /**< Frame doubling */
    NV_ENC_PIC_STRUCT_DISPLAY_FRAME_TRIPLING = 0x04                  /**< Field tripling */
}

public enum NV_ENC_PIC_TYPE
{
    NV_ENC_PIC_TYPE_P = 0x0,     /**< Forward predicted */
    NV_ENC_PIC_TYPE_B = 0x01,    /**< Bi-directionally predicted picture */
    NV_ENC_PIC_TYPE_I = 0x02,    /**< Intra predicted picture */
    NV_ENC_PIC_TYPE_IDR = 0x03,    /**< IDR picture */
    NV_ENC_PIC_TYPE_BI = 0x04,    /**< Bi-directionally predicted with only Intra MBs */
    NV_ENC_PIC_TYPE_SKIPPED = 0x05,    /**< Picture is skipped */
    NV_ENC_PIC_TYPE_INTRA_REFRESH = 0x06,    /**< First picture in intra refresh cycle */
    NV_ENC_PIC_TYPE_NONREF_P = 0x07,    /**< Non reference P picture */
    NV_ENC_PIC_TYPE_SWITCH = 0x08,    /**< Switch frame (AV1 only) */
    NV_ENC_PIC_TYPE_UNKNOWN = 0xFF     /**< Picture type unknown */
}

public enum NV_ENC_MV_PRECISION
{
    NV_ENC_MV_PRECISION_DEFAULT = 0x0,     /**< Driver selects Quarter-Pel motion vector precision by default */
    NV_ENC_MV_PRECISION_FULL_PEL = 0x01,    /**< Full-Pel motion vector precision */
    NV_ENC_MV_PRECISION_HALF_PEL = 0x02,    /**< Half-Pel motion vector precision */
    NV_ENC_MV_PRECISION_QUARTER_PEL = 0x03     /**< Quarter-Pel motion vector precision */
}

public enum NV_ENC_BUFFER_FORMAT
{
    NV_ENC_BUFFER_FORMAT_UNDEFINED = 0x00000000,  /**< Undefined buffer format */

    NV_ENC_BUFFER_FORMAT_NV12 = 0x00000001,  /**< Semi-Planar YUV [Y plane followed by interleaved UV plane] */
    NV_ENC_BUFFER_FORMAT_YV12 = 0x00000010,  /**< Planar YUV [Y plane followed by V and U planes] */
    NV_ENC_BUFFER_FORMAT_IYUV = 0x00000100,  /**< Planar YUV [Y plane followed by U and V planes] */
    NV_ENC_BUFFER_FORMAT_YUV444 = 0x00001000,  /**< Planar YUV [Y plane followed by U and V planes] */
    NV_ENC_BUFFER_FORMAT_YUV420_10BIT = 0x00010000,  /**< 10 bit Semi-Planar YUV [Y plane followed by interleaved UV plane]. Each pixel of size 2 bytes. Most Significant 10 bits contain pixel data. */
    NV_ENC_BUFFER_FORMAT_YUV444_10BIT = 0x00100000,  /**< 10 bit Planar YUV444 [Y plane followed by U and V planes]. Each pixel of size 2 bytes. Most Significant 10 bits contain pixel data.  */
    NV_ENC_BUFFER_FORMAT_ARGB = 0x01000000,  /**< 8 bit Packed A8R8G8B8. This is a word-ordered format
                                                                             where a pixel is represented by a 32-bit word with B
                                                                             in the lowest 8 bits, G in the next 8 bits, R in the
                                                                             8 bits after that and A in the highest 8 bits. */
    NV_ENC_BUFFER_FORMAT_ARGB10 = 0x02000000,  /**< 10 bit Packed A2R10G10B10. This is a word-ordered format
                                                                             where a pixel is represented by a 32-bit word with B
                                                                             in the lowest 10 bits, G in the next 10 bits, R in the
                                                                             10 bits after that and A in the highest 2 bits. */
    NV_ENC_BUFFER_FORMAT_AYUV = 0x04000000,  /**< 8 bit Packed A8Y8U8V8. This is a word-ordered format
                                                                             where a pixel is represented by a 32-bit word with V
                                                                             in the lowest 8 bits, U in the next 8 bits, Y in the
                                                                             8 bits after that and A in the highest 8 bits. */
    NV_ENC_BUFFER_FORMAT_ABGR = 0x10000000,  /**< 8 bit Packed A8B8G8R8. This is a word-ordered format
                                                                             where a pixel is represented by a 32-bit word with R
                                                                             in the lowest 8 bits, G in the next 8 bits, B in the
                                                                             8 bits after that and A in the highest 8 bits. */
    NV_ENC_BUFFER_FORMAT_ABGR10 = 0x20000000,  /**< 10 bit Packed A2B10G10R10. This is a word-ordered format
                                                                             where a pixel is represented by a 32-bit word with R
                                                                             in the lowest 10 bits, G in the next 10 bits, B in the
                                                                             10 bits after that and A in the highest 2 bits. */
    NV_ENC_BUFFER_FORMAT_U8 = 0x40000000,  /**< Buffer format representing one-dimensional buffer.
                                                                             This format should be used only when registering the
                                                                             resource as output buffer, which will be used to write
                                                                             the encoded bit stream or H.264 ME only mode output. */
    NV_ENC_BUFFER_FORMAT_NV16 = 0x40000001,  /**< Semi-Planar YUV 422 [Y plane followed by interleaved UV plane] */
    NV_ENC_BUFFER_FORMAT_P210 = 0x40000002,  /**< Semi-Planar 10-bit YUV 422 [Y plane followed by interleaved UV plane] */
}

public enum NV_ENC_LEVEL
{
    NV_ENC_LEVEL_AUTOSELECT = 0,

    NV_ENC_LEVEL_H264_1 = 10,
    NV_ENC_LEVEL_H264_1b = 9,
    NV_ENC_LEVEL_H264_11 = 11,
    NV_ENC_LEVEL_H264_12 = 12,
    NV_ENC_LEVEL_H264_13 = 13,
    NV_ENC_LEVEL_H264_2 = 20,
    NV_ENC_LEVEL_H264_21 = 21,
    NV_ENC_LEVEL_H264_22 = 22,
    NV_ENC_LEVEL_H264_3 = 30,
    NV_ENC_LEVEL_H264_31 = 31,
    NV_ENC_LEVEL_H264_32 = 32,
    NV_ENC_LEVEL_H264_4 = 40,
    NV_ENC_LEVEL_H264_41 = 41,
    NV_ENC_LEVEL_H264_42 = 42,
    NV_ENC_LEVEL_H264_5 = 50,
    NV_ENC_LEVEL_H264_51 = 51,
    NV_ENC_LEVEL_H264_52 = 52,
    NV_ENC_LEVEL_H264_60 = 60,
    NV_ENC_LEVEL_H264_61 = 61,
    NV_ENC_LEVEL_H264_62 = 62,

    NV_ENC_LEVEL_HEVC_1 = 30,
    NV_ENC_LEVEL_HEVC_2 = 60,
    NV_ENC_LEVEL_HEVC_21 = 63,
    NV_ENC_LEVEL_HEVC_3 = 90,
    NV_ENC_LEVEL_HEVC_31 = 93,
    NV_ENC_LEVEL_HEVC_4 = 120,
    NV_ENC_LEVEL_HEVC_41 = 123,
    NV_ENC_LEVEL_HEVC_5 = 150,
    NV_ENC_LEVEL_HEVC_51 = 153,
    NV_ENC_LEVEL_HEVC_52 = 156,
    NV_ENC_LEVEL_HEVC_6 = 180,
    NV_ENC_LEVEL_HEVC_61 = 183,
    NV_ENC_LEVEL_HEVC_62 = 186,

    NV_ENC_TIER_HEVC_MAIN = 0,
    NV_ENC_TIER_HEVC_HIGH = 1,

    NV_ENC_LEVEL_AV1_2 = 0,
    NV_ENC_LEVEL_AV1_21 = 1,
    NV_ENC_LEVEL_AV1_22 = 2,
    NV_ENC_LEVEL_AV1_23 = 3,
    NV_ENC_LEVEL_AV1_3 = 4,
    NV_ENC_LEVEL_AV1_31 = 5,
    NV_ENC_LEVEL_AV1_32 = 6,
    NV_ENC_LEVEL_AV1_33 = 7,
    NV_ENC_LEVEL_AV1_4 = 8,
    NV_ENC_LEVEL_AV1_41 = 9,
    NV_ENC_LEVEL_AV1_42 = 10,
    NV_ENC_LEVEL_AV1_43 = 11,
    NV_ENC_LEVEL_AV1_5 = 12,
    NV_ENC_LEVEL_AV1_51 = 13,
    NV_ENC_LEVEL_AV1_52 = 14,
    NV_ENC_LEVEL_AV1_53 = 15,
    NV_ENC_LEVEL_AV1_6 = 16,
    NV_ENC_LEVEL_AV1_61 = 17,
    NV_ENC_LEVEL_AV1_62 = 18,
    NV_ENC_LEVEL_AV1_63 = 19,
    NV_ENC_LEVEL_AV1_7 = 20,
    NV_ENC_LEVEL_AV1_71 = 21,
    NV_ENC_LEVEL_AV1_72 = 22,
    NV_ENC_LEVEL_AV1_73 = 23,
    NV_ENC_LEVEL_AV1_AUTOSELECT,

    NV_ENC_TIER_AV1_0 = 0,
    NV_ENC_TIER_AV1_1 = 1
}

[Flags]
public enum NV_ENC_PIC_FLAGS
{
    NV_ENC_PIC_FLAG_FORCEINTRA = 0x1,   /**< Encode the current picture as an Intra picture */
    NV_ENC_PIC_FLAG_FORCEIDR = 0x2,   /**< Encode the current picture as an IDR picture.
                                                            This flag is only valid when Picture type decision is taken by the Encoder
                                                            [_NV_ENC_INITIALIZE_PARAMS::enablePTD == 1]. */
    NV_ENC_PIC_FLAG_OUTPUT_SPSPPS = 0x4,   /**< Write the sequence and picture header in encoded bitstream of the current picture */
    NV_ENC_PIC_FLAG_EOS = 0x8,   /**< Indicates end of the input stream */
    NV_ENC_PIC_FLAG_DISABLE_ENC_STATE_ADVANCE = 0x10,  /**< Do not advance encoder state during encode */
    NV_ENC_PIC_FLAG_OUTPUT_RECON_FRAME = 0x20,  /**< Write reconstructed frame */
}

public enum NV_ENC_MEMORY_HEAP
{
    NV_ENC_MEMORY_HEAP_AUTOSELECT = 0, /**< Memory heap to be decided by the encoder driver based on the usage */
    NV_ENC_MEMORY_HEAP_VID = 1, /**< Memory heap is in local video memory */
    NV_ENC_MEMORY_HEAP_SYSMEM_CACHED = 2, /**< Memory heap is in cached system memory */
    NV_ENC_MEMORY_HEAP_SYSMEM_UNCACHED = 3  /**< Memory heap is in uncached system memory */
}

public enum NV_ENC_BFRAME_REF_MODE
{
    NV_ENC_BFRAME_REF_MODE_DISABLED = 0x0,          /**< B frame is not used for reference */
    NV_ENC_BFRAME_REF_MODE_EACH = 0x1,          /**< Each B-frame will be used for reference */
    NV_ENC_BFRAME_REF_MODE_MIDDLE = 0x2,          /**< Only(Number of B-frame)/2 th B-frame will be used for reference */
}

public enum NV_ENC_H264_ENTROPY_CODING_MODE
{
    NV_ENC_H264_ENTROPY_CODING_MODE_AUTOSELECT = 0x0,   /**< Entropy coding mode is auto selected by the encoder driver */
    NV_ENC_H264_ENTROPY_CODING_MODE_CABAC = 0x1,   /**< Entropy coding mode is CABAC */
    NV_ENC_H264_ENTROPY_CODING_MODE_CAVLC = 0x2    /**< Entropy coding mode is CAVLC */
}

public enum NV_ENC_H264_BDIRECT_MODE
{
    NV_ENC_H264_BDIRECT_MODE_AUTOSELECT = 0x0,          /**< BDirect mode is auto selected by the encoder driver */
    NV_ENC_H264_BDIRECT_MODE_DISABLE = 0x1,          /**< Disable BDirect mode */
    NV_ENC_H264_BDIRECT_MODE_TEMPORAL = 0x2,          /**< Temporal BDirect mode */
    NV_ENC_H264_BDIRECT_MODE_SPATIAL = 0x3           /**< Spatial BDirect mode */
}

public enum NV_ENC_H264_FMO_MODE
{
    NV_ENC_H264_FMO_AUTOSELECT = 0x0,          /**< FMO usage is auto selected by the encoder driver */
    NV_ENC_H264_FMO_ENABLE = 0x1,          /**< Enable FMO */
    NV_ENC_H264_FMO_DISABLE = 0x2,          /**< Disable FMO */
}

public enum NV_ENC_H264_ADAPTIVE_TRANSFORM_MODE
{
    NV_ENC_H264_ADAPTIVE_TRANSFORM_AUTOSELECT = 0x0,   /**< Adaptive Transform 8x8 mode is auto selected by the encoder driver*/
    NV_ENC_H264_ADAPTIVE_TRANSFORM_DISABLE = 0x1,   /**< Adaptive Transform 8x8 mode disabled */
    NV_ENC_H264_ADAPTIVE_TRANSFORM_ENABLE = 0x2,   /**< Adaptive Transform 8x8 mode should be used */
}

public enum NV_ENC_STEREO_PACKING_MODE
{
    NV_ENC_STEREO_PACKING_MODE_NONE = 0x0,  /**< No Stereo packing required */
    NV_ENC_STEREO_PACKING_MODE_CHECKERBOARD = 0x1,  /**< Checkerboard mode for packing stereo frames */
    NV_ENC_STEREO_PACKING_MODE_COLINTERLEAVE = 0x2,  /**< Column Interleave mode for packing stereo frames */
    NV_ENC_STEREO_PACKING_MODE_ROWINTERLEAVE = 0x3,  /**< Row Interleave mode for packing stereo frames */
    NV_ENC_STEREO_PACKING_MODE_SIDEBYSIDE = 0x4,  /**< Side-by-side mode for packing stereo frames */
    NV_ENC_STEREO_PACKING_MODE_TOPBOTTOM = 0x5,  /**< Top-Bottom mode for packing stereo frames */
    NV_ENC_STEREO_PACKING_MODE_FRAMESEQ = 0x6   /**< Frame Sequential mode for packing stereo frames */
}

public enum NV_ENC_INPUT_RESOURCE_TYPE
{
    NV_ENC_INPUT_RESOURCE_TYPE_DIRECTX = 0x0,   /**< input resource type is a directx9 surface*/
    NV_ENC_INPUT_RESOURCE_TYPE_CUDADEVICEPTR = 0x1,   /**< input resource type is a cuda device pointer surface*/
    NV_ENC_INPUT_RESOURCE_TYPE_CUDAARRAY = 0x2,   /**< input resource type is a cuda array surface.
                                                              This array must be a 2D array and the CUDA_ARRAY3D_SURFACE_LDST
                                                              flag must have been specified when creating it. */
    NV_ENC_INPUT_RESOURCE_TYPE_OPENGL_TEX = 0x3    /**< input resource type is an OpenGL texture */
}

public enum NV_ENC_BUFFER_USAGE
{
    NV_ENC_INPUT_IMAGE = 0x0,          /**< Registered surface will be used for input image */
    NV_ENC_OUTPUT_MOTION_VECTOR = 0x1,          /**< Registered surface will be used for output of H.264 ME only mode.
                                                         This buffer usage type is not supported for HEVC ME only mode. */
    NV_ENC_OUTPUT_BITSTREAM = 0x2,          /**< Registered surface will be used for output bitstream in encoding */
    NV_ENC_OUTPUT_RECON = 0x4,          /**< Registered surface will be used for output reconstructed frame in encoding */
}

public enum NV_ENC_NUM_REF_FRAMES
{
    NV_ENC_NUM_REF_FRAMES_AUTOSELECT = 0x0,          /**< Number of reference frames is auto selected by the encoder driver */
    NV_ENC_NUM_REF_FRAMES_1 = 0x1,          /**< Number of reference frames equal to 1 */
    NV_ENC_NUM_REF_FRAMES_2 = 0x2,          /**< Number of reference frames equal to 2 */
    NV_ENC_NUM_REF_FRAMES_3 = 0x3,          /**< Number of reference frames equal to 3 */
    NV_ENC_NUM_REF_FRAMES_4 = 0x4,          /**< Number of reference frames equal to 4 */
    NV_ENC_NUM_REF_FRAMES_5 = 0x5,          /**< Number of reference frames equal to 5 */
    NV_ENC_NUM_REF_FRAMES_6 = 0x6,          /**< Number of reference frames equal to 6 */
    NV_ENC_NUM_REF_FRAMES_7 = 0x7           /**< Number of reference frames equal to 7 */
}

public enum NV_ENC_TEMPORAL_FILTER_LEVEL
{
    NV_ENC_TEMPORAL_FILTER_LEVEL_0 = 0,
    NV_ENC_TEMPORAL_FILTER_LEVEL_4 = 4,
}

public enum NV_ENC_CAPS
{
    /**
     * Maximum number of B-Frames supported.
     */
    NV_ENC_CAPS_NUM_MAX_BFRAMES,

    /**
     * Rate control modes supported.
     * \n The API return value is a bitmask of the values in NV_ENC_PARAMS_RC_MODE.
     */
    NV_ENC_CAPS_SUPPORTED_RATECONTROL_MODES,

    /**
     * Indicates HW support for field mode encoding.
     * \n 0 : Interlaced mode encoding is not supported.
     * \n 1 : Interlaced field mode encoding is supported.
     * \n 2 : Interlaced frame encoding and field mode encoding are both supported.
     */
    NV_ENC_CAPS_SUPPORT_FIELD_ENCODING,

    /**
     * Indicates HW support for monochrome mode encoding.
     * \n 0 : Monochrome mode not supported.
     * \n 1 : Monochrome mode supported.
     */
    NV_ENC_CAPS_SUPPORT_MONOCHROME,

    /**
     * Indicates HW support for FMO.
     * \n 0 : FMO not supported.
     * \n 1 : FMO supported.
     */
    NV_ENC_CAPS_SUPPORT_FMO,

    /**
     * Indicates HW capability for Quarter pel motion estimation.
     * \n 0 : Quarter-Pel Motion Estimation not supported.
     * \n 1 : Quarter-Pel Motion Estimation supported.
     */
    NV_ENC_CAPS_SUPPORT_QPELMV,

    /**
     * H.264 specific. Indicates HW support for BDirect modes.
     * \n 0 : BDirect mode encoding not supported.
     * \n 1 : BDirect mode encoding supported.
     */
    NV_ENC_CAPS_SUPPORT_BDIRECT_MODE,

    /**
     * H264 specific. Indicates HW support for CABAC entropy coding mode.
     * \n 0 : CABAC entropy coding not supported.
     * \n 1 : CABAC entropy coding supported.
     */
    NV_ENC_CAPS_SUPPORT_CABAC,

    /**
     * Indicates HW support for Adaptive Transform.
     * \n 0 : Adaptive Transform not supported.
     * \n 1 : Adaptive Transform supported.
     */
    NV_ENC_CAPS_SUPPORT_ADAPTIVE_TRANSFORM,

    /**
     * Indicates HW support for Multi View Coding.
     * \n 0 : Multi View Coding not supported.
     * \n 1 : Multi View Coding supported.
     */
    NV_ENC_CAPS_SUPPORT_STEREO_MVC,

    /**
     * Indicates HW support for encoding Temporal layers.
     * \n 0 : Encoding Temporal layers not supported.
     * \n 1 : Encoding Temporal layers supported.
     */
    NV_ENC_CAPS_NUM_MAX_TEMPORAL_LAYERS,

    /**
     * Indicates HW support for Hierarchical P frames.
     * \n 0 : Hierarchical P frames not supported.
     * \n 1 : Hierarchical P frames supported.
     */
    NV_ENC_CAPS_SUPPORT_HIERARCHICAL_PFRAMES,

    /**
     * Indicates HW support for Hierarchical B frames.
     * \n 0 : Hierarchical B frames not supported.
     * \n 1 : Hierarchical B frames supported.
     */
    NV_ENC_CAPS_SUPPORT_HIERARCHICAL_BFRAMES,

    /**
     * Maximum Encoding level supported (See ::NV_ENC_LEVEL for details).
     */
    NV_ENC_CAPS_LEVEL_MAX,

    /**
     * Minimum Encoding level supported (See ::NV_ENC_LEVEL for details).
     */
    NV_ENC_CAPS_LEVEL_MIN,

    /**
     * Indicates HW support for separate colour plane encoding.
     * \n 0 : Separate colour plane encoding not supported.
     * \n 1 : Separate colour plane encoding supported.
     */
    NV_ENC_CAPS_SEPARATE_COLOUR_PLANE,

    /**
     * Maximum output width supported.
     */
    NV_ENC_CAPS_WIDTH_MAX,

    /**
     * Maximum output height supported.
     */
    NV_ENC_CAPS_HEIGHT_MAX,

    /**
     * Indicates Temporal Scalability Support.
     * \n 0 : Temporal SVC encoding not supported.
     * \n 1 : Temporal SVC encoding supported.
     */
    NV_ENC_CAPS_SUPPORT_TEMPORAL_SVC,

    /**
     * Indicates Dynamic Encode Resolution Change Support.
     * Support added from NvEncodeAPI version 2.0.
     * \n 0 : Dynamic Encode Resolution Change not supported.
     * \n 1 : Dynamic Encode Resolution Change supported.
     */
    NV_ENC_CAPS_SUPPORT_DYN_RES_CHANGE,

    /**
     * Indicates Dynamic Encode Bitrate Change Support.
     * Support added from NvEncodeAPI version 2.0.
     * \n 0 : Dynamic Encode bitrate change not supported.
     * \n 1 : Dynamic Encode bitrate change supported.
     */
    NV_ENC_CAPS_SUPPORT_DYN_BITRATE_CHANGE,

    /**
     * Indicates Forcing Constant QP On The Fly Support.
     * Support added from NvEncodeAPI version 2.0.
     * \n 0 : Forcing constant QP on the fly not supported.
     * \n 1 : Forcing constant QP on the fly supported.
     */
    NV_ENC_CAPS_SUPPORT_DYN_FORCE_CONSTQP,

    /**
     * Indicates Dynamic rate control mode Change Support.
     * \n 0 : Dynamic rate control mode change not supported.
     * \n 1 : Dynamic rate control mode change supported.
     */
    NV_ENC_CAPS_SUPPORT_DYN_RCMODE_CHANGE,

    /**
     * Indicates Subframe readback support for slice-based encoding. If this feature is supported, it can be enabled by setting enableSubFrameWrite = 1.
     * \n 0 : Subframe readback not supported.
     * \n 1 : Subframe readback supported.
     */
    NV_ENC_CAPS_SUPPORT_SUBFRAME_READBACK,

    /**
     * Indicates Constrained Encoding mode support.
     * Support added from NvEncodeAPI version 2.0.
     * \n 0 : Constrained encoding mode not supported.
     * \n 1 : Constrained encoding mode supported.
     * If this mode is supported client can enable this during initialization.
     * Client can then force a picture to be coded as constrained picture where
     * in-loop filtering is disabled across slice boundaries and prediction vectors for inter
     * macroblocks in each slice will be restricted to the slice region.
     */
    NV_ENC_CAPS_SUPPORT_CONSTRAINED_ENCODING,

    /**
     * Indicates Intra Refresh Mode Support.
     * Support added from NvEncodeAPI version 2.0.
     * \n 0 : Intra Refresh Mode not supported.
     * \n 1 : Intra Refresh Mode supported.
     */
    NV_ENC_CAPS_SUPPORT_INTRA_REFRESH,

    /**
     * Indicates Custom VBV Buffer Size support. It can be used for capping frame size.
     * Support added from NvEncodeAPI version 2.0.
     * \n 0 : Custom VBV buffer size specification from client, not supported.
     * \n 1 : Custom VBV buffer size specification from client, supported.
     */
    NV_ENC_CAPS_SUPPORT_CUSTOM_VBV_BUF_SIZE,

    /**
     * Indicates Dynamic Slice Mode Support.
     * Support added from NvEncodeAPI version 2.0.
     * \n 0 : Dynamic Slice Mode not supported.
     * \n 1 : Dynamic Slice Mode supported.
     */
    NV_ENC_CAPS_SUPPORT_DYNAMIC_SLICE_MODE,

    /**
     * Indicates Reference Picture Invalidation Support.
     * Support added from NvEncodeAPI version 2.0.
     * \n 0 : Reference Picture Invalidation not supported.
     * \n 1 : Reference Picture Invalidation supported.
     */
    NV_ENC_CAPS_SUPPORT_REF_PIC_INVALIDATION,

    /**
     * Indicates support for Pre-Processing.
     * The API return value is a bitmask of the values defined in ::NV_ENC_PREPROC_FLAGS
     */
    NV_ENC_CAPS_PREPROC_SUPPORT,

    /**
    * Indicates support Async mode.
    * \n 0 : Async Encode mode not supported.
    * \n 1 : Async Encode mode supported.
    */
    NV_ENC_CAPS_ASYNC_ENCODE_SUPPORT,

    /**
     * Maximum MBs per frame supported.
     */
    NV_ENC_CAPS_MB_NUM_MAX,

    /**
     * Maximum aggregate throughput in MBs per sec.
     */
    NV_ENC_CAPS_MB_PER_SEC_MAX,

    /**
     * Indicates HW support for YUV444 mode encoding.
     * \n 0 : YUV444 mode encoding not supported.
     * \n 1 : YUV444 mode encoding supported.
     */
    NV_ENC_CAPS_SUPPORT_YUV444_ENCODE,

    /**
     * Indicates HW support for lossless encoding.
     * \n 0 : lossless encoding not supported.
     * \n 1 : lossless encoding supported.
     */
    NV_ENC_CAPS_SUPPORT_LOSSLESS_ENCODE,

    /**
    * Indicates HW support for Sample Adaptive Offset.
    * \n 0 : SAO not supported.
    * \n 1 : SAO encoding supported.
    */
    NV_ENC_CAPS_SUPPORT_SAO,

    /**
     * Indicates HW support for Motion Estimation Only Mode.
     * \n 0 : MEOnly Mode not supported.
     * \n 1 : MEOnly Mode supported for I and P frames.
     * \n 2 : MEOnly Mode supported for I, P and B frames.
     */
    NV_ENC_CAPS_SUPPORT_MEONLY_MODE,

    /**
     * Indicates HW support for lookahead encoding (enableLookahead=1).
     * \n 0 : Lookahead not supported.
     * \n 1 : Lookahead supported.
     */
    NV_ENC_CAPS_SUPPORT_LOOKAHEAD,

    /**
     * Indicates HW support for temporal AQ encoding (enableTemporalAQ=1).
     * \n 0 : Temporal AQ not supported.
     * \n 1 : Temporal AQ supported.
     */
    NV_ENC_CAPS_SUPPORT_TEMPORAL_AQ,
    /**
     * Indicates HW support for 10 bit encoding.
     * \n 0 : 10 bit encoding not supported.
     * \n 1 : 10 bit encoding supported.
     */
    NV_ENC_CAPS_SUPPORT_10BIT_ENCODE,
    /**
     * Maximum number of Long Term Reference frames supported
     */
    NV_ENC_CAPS_NUM_MAX_LTR_FRAMES,

    /**
     * Indicates HW support for Weighted Prediction.
     * \n 0 : Weighted Prediction not supported.
     * \n 1 : Weighted Prediction supported.
     */
    NV_ENC_CAPS_SUPPORT_WEIGHTED_PREDICTION,


    /**
     * On managed (vGPU) platforms (Windows only), this API, in conjunction with other GRID Management APIs, can be used
     * to estimate the residual capacity of the hardware encoder on the GPU as a percentage of the total available encoder capacity.
     * This API can be called at any time; i.e. during the encode session or before opening the encode session.
     * If the available encoder capacity is returned as zero, applications may choose to switch to software encoding
     * and continue to call this API (e.g. polling once per second) until capacity becomes available.
     *
     * On bare metal (non-virtualized GPU) and linux platforms, this API always returns 100.
     */
    NV_ENC_CAPS_DYNAMIC_QUERY_ENCODER_CAPACITY,

    /**
    * Indicates B as reference support.
    * \n 0 : B as reference is not supported.
    * \n 1 : each B-Frame as reference is supported.
    * \n 2 : only Middle B-frame as reference is supported.
    */
    NV_ENC_CAPS_SUPPORT_BFRAME_REF_MODE,

    /**
     * Indicates HW support for Emphasis Level Map based delta QP computation.
     * \n 0 : Emphasis Level Map based delta QP not supported.
     * \n 1 : Emphasis Level Map based delta QP is supported.
     */
    NV_ENC_CAPS_SUPPORT_EMPHASIS_LEVEL_MAP,

    /**
     * Minimum input width supported.
     */
    NV_ENC_CAPS_WIDTH_MIN,

    /**
     * Minimum input height supported.
     */
    NV_ENC_CAPS_HEIGHT_MIN,

    /**
     * Indicates HW support for multiple reference frames.
     */
    NV_ENC_CAPS_SUPPORT_MULTIPLE_REF_FRAMES,

    /**
     * Indicates HW support for HEVC with alpha encoding.
     * \n 0 : HEVC with alpha encoding not supported.
     * \n 1 : HEVC with alpha encoding is supported.
     */
    NV_ENC_CAPS_SUPPORT_ALPHA_LAYER_ENCODING,

    /**
     * Indicates number of Encoding engines present on GPU.
     */
    NV_ENC_CAPS_NUM_ENCODER_ENGINES,

    /**
     * Indicates single slice intra refresh support.
     */
    NV_ENC_CAPS_SINGLE_SLICE_INTRA_REFRESH,

    /**
     * Indicates encoding without advancing the state support.
     */
    NV_ENC_CAPS_DISABLE_ENC_STATE_ADVANCE,

    /**
     * Indicates reconstructed output support.
     */
    NV_ENC_CAPS_OUTPUT_RECON_SURFACE,

    /**
     * Indicates encoded frame output stats support for every block. Block represents a CTB for HEVC, macroblock for H.264 and super block for AV1.
     */
    NV_ENC_CAPS_OUTPUT_BLOCK_STATS,

    /**
     * Indicates encoded frame output stats support for every row. Row represents a CTB row for HEVC, macroblock row for H.264 and super block row for AV1.
     */
    NV_ENC_CAPS_OUTPUT_ROW_STATS,


    /**
     * Indicates temporal filtering support.
     */
    NV_ENC_CAPS_SUPPORT_TEMPORAL_FILTER,

    /**
     * Maximum Lookahead level supported (See ::NV_ENC_LOOKAHEAD_LEVEL for details).
     */
    NV_ENC_CAPS_SUPPORT_LOOKAHEAD_LEVEL,

    /**
     * Indicates UnidirectionalB support.
     */
    NV_ENC_CAPS_SUPPORT_UNIDIRECTIONAL_B,

    /**
     * Indicates HW support for MVHEVC encoding.
     * \n 0 : MVHEVC encoding not supported.
     * \n 1 : MVHEVC encoding supported.
     */
    NV_ENC_CAPS_SUPPORT_MVHEVC_ENCODE,

    /**
     * Indicates HW support for YUV422 mode encoding.
     * \n 0 : YUV422 mode encoding not supported.
     * \n 1 : YUV422 mode encoding supported.
     */
    NV_ENC_CAPS_SUPPORT_YUV422_ENCODE,

    /**
     * Reserved - Not to be used by clients.
     */
    NV_ENC_CAPS_EXPOSED_COUNT

}

public enum NV_ENC_HEVC_CUSIZE
{
    NV_ENC_HEVC_CUSIZE_AUTOSELECT = 0,
    NV_ENC_HEVC_CUSIZE_8x8 = 1,
    NV_ENC_HEVC_CUSIZE_16x16 = 2,
    NV_ENC_HEVC_CUSIZE_32x32 = 3,
    NV_ENC_HEVC_CUSIZE_64x64 = 4,
}

public enum NV_ENC_AV1_PART_SIZE
{
    NV_ENC_AV1_PART_SIZE_AUTOSELECT = 0,
    NV_ENC_AV1_PART_SIZE_4x4 = 1,
    NV_ENC_AV1_PART_SIZE_8x8 = 2,
    NV_ENC_AV1_PART_SIZE_16x16 = 3,
    NV_ENC_AV1_PART_SIZE_32x32 = 4,
    NV_ENC_AV1_PART_SIZE_64x64 = 5,
}


public enum NV_ENC_VUI_VIDEO_FORMAT
{
    NV_ENC_VUI_VIDEO_FORMAT_COMPONENT = 0,
    NV_ENC_VUI_VIDEO_FORMAT_PAL = 1,
    NV_ENC_VUI_VIDEO_FORMAT_NTSC = 2,
    NV_ENC_VUI_VIDEO_FORMAT_SECAM = 3,
    NV_ENC_VUI_VIDEO_FORMAT_MAC = 4,
    NV_ENC_VUI_VIDEO_FORMAT_UNSPECIFIED = 5,
}

public enum NV_ENC_VUI_COLOR_PRIMARIES
{
    NV_ENC_VUI_COLOR_PRIMARIES_UNDEFINED = 0,
    NV_ENC_VUI_COLOR_PRIMARIES_BT709 = 1,
    NV_ENC_VUI_COLOR_PRIMARIES_UNSPECIFIED = 2,
    NV_ENC_VUI_COLOR_PRIMARIES_RESERVED = 3,
    NV_ENC_VUI_COLOR_PRIMARIES_BT470M = 4,
    NV_ENC_VUI_COLOR_PRIMARIES_BT470BG = 5,
    NV_ENC_VUI_COLOR_PRIMARIES_SMPTE170M = 6,
    NV_ENC_VUI_COLOR_PRIMARIES_SMPTE240M = 7,
    NV_ENC_VUI_COLOR_PRIMARIES_FILM = 8,
    NV_ENC_VUI_COLOR_PRIMARIES_BT2020 = 9,
    NV_ENC_VUI_COLOR_PRIMARIES_SMPTE428 = 10,
    NV_ENC_VUI_COLOR_PRIMARIES_SMPTE431 = 11,
    NV_ENC_VUI_COLOR_PRIMARIES_SMPTE432 = 12,
    NV_ENC_VUI_COLOR_PRIMARIES_JEDEC_P22 = 22,
}

public enum NV_ENC_VUI_TRANSFER_CHARACTERISTIC
{
    NV_ENC_VUI_TRANSFER_CHARACTERISTIC_UNDEFINED = 0,
    NV_ENC_VUI_TRANSFER_CHARACTERISTIC_BT709 = 1,
    NV_ENC_VUI_TRANSFER_CHARACTERISTIC_UNSPECIFIED = 2,
    NV_ENC_VUI_TRANSFER_CHARACTERISTIC_RESERVED = 3,
    NV_ENC_VUI_TRANSFER_CHARACTERISTIC_BT470M = 4,
    NV_ENC_VUI_TRANSFER_CHARACTERISTIC_BT470BG = 5,
    NV_ENC_VUI_TRANSFER_CHARACTERISTIC_SMPTE170M = 6,
    NV_ENC_VUI_TRANSFER_CHARACTERISTIC_SMPTE240M = 7,
    NV_ENC_VUI_TRANSFER_CHARACTERISTIC_LINEAR = 8,
    NV_ENC_VUI_TRANSFER_CHARACTERISTIC_LOG = 9,
    NV_ENC_VUI_TRANSFER_CHARACTERISTIC_LOG_SQRT = 10,
    NV_ENC_VUI_TRANSFER_CHARACTERISTIC_IEC61966_2_4 = 11,
    NV_ENC_VUI_TRANSFER_CHARACTERISTIC_BT1361_ECG = 12,
    NV_ENC_VUI_TRANSFER_CHARACTERISTIC_SRGB = 13,
    NV_ENC_VUI_TRANSFER_CHARACTERISTIC_BT2020_10 = 14,
    NV_ENC_VUI_TRANSFER_CHARACTERISTIC_BT2020_12 = 15,
    NV_ENC_VUI_TRANSFER_CHARACTERISTIC_SMPTE2084 = 16,
    NV_ENC_VUI_TRANSFER_CHARACTERISTIC_SMPTE428 = 17,
    NV_ENC_VUI_TRANSFER_CHARACTERISTIC_ARIB_STD_B67 = 18,
}

public enum NV_ENC_VUI_MATRIX_COEFFS
{
    NV_ENC_VUI_MATRIX_COEFFS_RGB = 0,
    NV_ENC_VUI_MATRIX_COEFFS_BT709 = 1,
    NV_ENC_VUI_MATRIX_COEFFS_UNSPECIFIED = 2,
    NV_ENC_VUI_MATRIX_COEFFS_RESERVED = 3,
    NV_ENC_VUI_MATRIX_COEFFS_FCC = 4,
    NV_ENC_VUI_MATRIX_COEFFS_BT470BG = 5,
    NV_ENC_VUI_MATRIX_COEFFS_SMPTE170M = 6,
    NV_ENC_VUI_MATRIX_COEFFS_SMPTE240M = 7,
    NV_ENC_VUI_MATRIX_COEFFS_YCGCO = 8,
    NV_ENC_VUI_MATRIX_COEFFS_BT2020_NCL = 9,
    NV_ENC_VUI_MATRIX_COEFFS_BT2020_CL = 10,
    NV_ENC_VUI_MATRIX_COEFFS_SMPTE2085 = 11,
}

public enum NV_ENC_LOOKAHEAD_LEVEL
{
    NV_ENC_LOOKAHEAD_LEVEL_0 = 0,
    NV_ENC_LOOKAHEAD_LEVEL_1 = 1,
    NV_ENC_LOOKAHEAD_LEVEL_2 = 2,
    NV_ENC_LOOKAHEAD_LEVEL_3 = 3,
    NV_ENC_LOOKAHEAD_LEVEL_AUTOSELECT = 15,
}

public enum NV_ENC_BIT_DEPTH
{
    NV_ENC_BIT_DEPTH_INVALID = 0,         /**< Invalid Bit Depth */
    NV_ENC_BIT_DEPTH_8 = 8,         /**< Bit Depth 8 */
    NV_ENC_BIT_DEPTH_10 = 10,        /**< Bit Depth 10 */
}

[StructLayout(LayoutKind.Explicit)]
public struct NV_ENC_CODEC_PIC_PARAMS
{
    [FieldOffset(0)]
    public NV_ENC_PIC_PARAMS_H264 h264PicParams;

    //[FieldOffset(0)]
    //public NV_ENC_PIC_PARAMS_HEVC hevcPicParams;

    //[FieldOffset(0)]
    //public NV_ENC_PIC_PARAMS_AV1 av1PicParams;

    [FieldOffset(0)]
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
    public uint[] reserved;
}

[StructLayout(LayoutKind.Explicit)]
public struct NV_ENC_PIC_PARAMS_H264_EXT
{
    // MVC picture parameters.
    [FieldOffset(0)]
    public NV_ENC_PIC_PARAMS_MVC mvcPicParams;

    // Reserved array of 32 uints.
    [FieldOffset(0)]
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
    public uint[] reserved1;
}

[StructLayout(LayoutKind.Sequential)]
public struct NV_ENC_SEI_PAYLOAD
{
    public uint payloadSize;   // SEI payload size in bytes.
    public uint payloadType;   // SEI payload type.
    public IntPtr payload;     // Pointer to the user data.
}

[StructLayout(LayoutKind.Sequential)]
public struct NV_ENC_PIC_PARAMS_MVC
{
    public uint version;   // Must be set to NV_ENC_PIC_PARAMS_MVC_VER.
    public uint viewID;    // Specifies the view ID for the current input.
    public uint temporalID;// Specifies the temporal ID for the current input.
    public uint priorityID;// Specifies the priority ID (reserved, ignored).

    // Reserved array of 12 uints, must be set to 0.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
    public uint[] reserved1;

    // Reserved array of 8 pointers, must be set to NULL.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public IntPtr[] reserved2;
}


[StructLayout(LayoutKind.Sequential)]
public struct NV_ENC_PIC_PARAMS_H264
{
    public uint displayPOCSyntax;              // Specifies the display POC syntax.
    public uint reserved3;                     // Reserved; must be 0.
    public uint refPicFlag;                    // Set to 1 for a reference picture.
    public uint colourPlaneId;                 // Colour plane ID.
    public uint forceIntraRefreshWithFrameCnt; // Force intra refresh frame count.

    // The next 32 bits are a packed bitfield:
    //   Bit 0: constrainedFrame (1 bit)
    //   Bit 1: sliceModeDataUpdate (1 bit)
    //   Bit 2: ltrMarkFrame (1 bit)
    //   Bit 3: ltrUseFrames (1 bit)
    //   Bits 4-31: reserved (28 bits, must be 0)
    private uint bitFields;

    public bool constrainedFrame
    {
        get => (this.bitFields & (1u << 0)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 0)) : (this.bitFields & ~(1u << 0));
    }
    public bool sliceModeDataUpdate
    {
        get => (this.bitFields & (1u << 1)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 1)) : (this.bitFields & ~(1u << 1));
    }
    public bool ltrMarkFrame
    {
        get => (this.bitFields & (1u << 2)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 2)) : (this.bitFields & ~(1u << 2));
    }
    public bool ltrUseFrames
    {
        get => (this.bitFields & (1u << 3)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 3)) : (this.bitFields & ~(1u << 3));
    }
    // Bits 4..31 are reserved and should be 0.

    public IntPtr sliceTypeData;          // Deprecated pointer to slice type data.
    public uint sliceTypeArrayCnt;        // Deprecated count.
    public uint seiPayloadArrayCnt;       // Number of elements allocated in the SEI payload array.
    public IntPtr seiPayloadArray;        // Pointer to an array of NV_ENC_SEI_PAYLOAD.
    public uint sliceMode;                // Slice mode.
    public uint sliceModeData;            // Slice mode data.
    public uint ltrMarkFrameIdx;          // LTR frame index to mark.
    public uint ltrUseFrameBitmap;        // Bitmap of LTR frame indices used.
    public uint ltrUsageMode;             // Reserved; must be 0.
    public uint forceIntraSliceCount;     // Number of slices forced to intra.
    public IntPtr forceIntraSliceIdx;     // Pointer to an array of uint (slice indices); length equals forceIntraSliceCount.

    public NV_ENC_PIC_PARAMS_H264_EXT h264ExtPicParams; // H.264 extension picture parameters.
    public NV_ENC_TIME_CODE timeCode;                   // Time-code for picture timing SEI.

    // Reserved array of 202 uints.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 202)]
    public uint[] reserved;

    // Reserved array of 61 pointers.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 61)]
    public IntPtr[] reserved2;
}

[StructLayout(LayoutKind.Sequential)]
public struct NV_ENC_PIC_PARAMS
{
    public uint version;                // Must be set to NV_ENC_PIC_PARAMS_VER.
    public uint inputWidth;             // Input frame width.
    public uint inputHeight;            // Input frame height.
    public uint inputPitch;             // Input buffer pitch (if unknown, set to inputWidth).
    public NV_ENC_PIC_FLAGS encodePicFlags;         // Bitwise OR of encode picture flags.
    public uint frameIdx;               // Monotonically increasing frame index.
    public ulong inputTimeStamp;        // Opaque data associated with this frame.
    public ulong inputDuration;         // Duration of the input picture.

    // Pointers to buffers:
    public IntPtr inputBuffer;          // NV_ENC_INPUT_PTR: Obtained from NvEncCreateInputBuffer() or NvEncMapInputResource().
    public IntPtr outputBitstream;      // NV_ENC_OUTPUT_PTR: See comments in the API.

    public IntPtr completionEvent;      // Event to be signaled on frame encode completion.

    public NV_ENC_BUFFER_FORMAT bufferFmt;  // Input buffer format.
    public NV_ENC_PIC_STRUCT pictureStruct;   // Structure of the input picture.
    public NV_ENC_PIC_TYPE pictureType;       // Picture type.

    public NV_ENC_CODEC_PIC_PARAMS codecPicParams; // Codec-specific per-picture parameters.

    // Two-element array for ME hint candidate counts:
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public NVENC_EXTERNAL_ME_HINT_COUNTS_PER_BLOCKTYPE[] meHintCountsPerBlock;

    // Pointer to ME external hints (for H264/HEVC).
    public IntPtr meExternalHints;

    // Reserved array of 7 uints.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
    public uint[] reserved2;

    // Reserved array of 2 pointers.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public IntPtr[] reserved5;

    // Pointer to a signed byte array for qpDeltaMap.
    public IntPtr qpDeltaMap;
    public uint qpDeltaMapSize;         // Size in bytes of qpDeltaMap.

    public uint reservedBitFields;      // Reserved bitfields; must be 0.

    // Two-element array for ME hint reference picture distances.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public ushort[] meHintRefPicDist;

    public uint reserved4;              // Reserved.

    public IntPtr alphaBuffer;          // NV_ENC_INPUT_PTR for alpha layer (if applicable).

    // Pointer to ME external SB hints (for AV1).
    public IntPtr meExternalSbHints;

    public uint meSbHintsCount;         // Total number of external ME SB hint candidates.
    public uint stateBufferIdx;         // Index of the state buffer for saving encoder state.
    public IntPtr outputReconBuffer;    // NV_ENC_OUTPUT_PTR for reconstructed frame output.

    // Reserved array of 284 uints.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 284)]
    public uint[] reserved3;

    // Reserved array of 57 pointers.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 57)]
    public IntPtr[] reserved6;
}


[StructLayout(LayoutKind.Sequential)]
public struct NV_ENC_CLOCK_TIMESTAMP_SET
{
    // The first 32 bits are a packed field:
    // Bit 0: countingType (1 bit)
    // Bit 1: discontinuityFlag (1 bit)
    // Bit 2: cntDroppedFrames (1 bit)
    // Bits 3-10: nFrames (8 bits)
    // Bits 11-16: secondsValue (6 bits)
    // Bits 17-22: minutesValue (6 bits)
    // Bits 23-27: hoursValue (5 bits)
    // Bits 28-31: reserved2 (4 bits)
    private uint bitField;

    public bool CountingType
    {
        get => (this.bitField & (1u << 0)) != 0;
        set => this.bitField = value ? (this.bitField | (1u << 0)) : (this.bitField & ~(1u << 0));
    }

    public bool DiscontinuityFlag
    {
        get => (this.bitField & (1u << 1)) != 0;
        set => this.bitField = value ? (this.bitField | (1u << 1)) : (this.bitField & ~(1u << 1));
    }

    public bool CntDroppedFrames
    {
        get => (this.bitField & (1u << 2)) != 0;
        set => this.bitField = value ? (this.bitField | (1u << 2)) : (this.bitField & ~(1u << 2));
    }

    // 8-bit field: bits 3..10
    public byte NFrames
    {
        get => (byte)((this.bitField >> 3) & 0xFF);
        set => this.bitField = (this.bitField & ~(0xFFu << 3)) | (((uint)value & 0xFF) << 3);
    }

    // 6-bit field: bits 11..16
    public byte SecondsValue
    {
        get => (byte)((this.bitField >> 11) & 0x3F);
        set => this.bitField = (this.bitField & ~(0x3Fu << 11)) | (((uint)value & 0x3F) << 11);
    }

    // 6-bit field: bits 17..22
    public byte MinutesValue
    {
        get => (byte)((this.bitField >> 17) & 0x3F);
        set => this.bitField = (this.bitField & ~(0x3Fu << 17)) | (((uint)value & 0x3F) << 17);
    }

    // 5-bit field: bits 23..27
    public byte HoursValue
    {
        get => (byte)((this.bitField >> 23) & 0x1F);
        set => this.bitField = (this.bitField & ~(0x1Fu << 23)) | (((uint)value & 0x1F) << 23);
    }

    // 4-bit reserved field: bits 28..31 (typically must be zero)
    public byte Reserved2
    {
        get => (byte)((this.bitField >> 28) & 0xF);
        set => this.bitField = (this.bitField & ~(0xFu << 28)) | (((uint)value & 0xF) << 28);
    }

    // The second field is a full 32-bit unsigned integer.
    public uint TimeOffset;
}

[StructLayout(LayoutKind.Sequential)]
public struct NV_ENC_TIME_CODE
{
    public NV_ENC_DISPLAY_PIC_STRUCT displayPicStruct;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public NV_ENC_CLOCK_TIMESTAMP_SET[] clockTimestamp;

    public uint skipClockTimestampInsertion;
}



[StructLayout(LayoutKind.Sequential)]
public struct NV_ENC_LOCK_BITSTREAM
{
    public uint version; // Must be set to NV_ENC_LOCK_BITSTREAM_VER

    // The following 32-bit field packs several bitfields:
    // Bit  0: doNotWait (1 bit)
    // Bit  1: ltrFrame (1 bit)
    // Bit  2: getRCStats (1 bit)
    // Bits 3-31: reserved (29 bits)
    private uint bitFields;

    public bool doNotWait
    {
        get => (this.bitFields & (1u << 0)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 0)) : (this.bitFields & ~(1u << 0));
    }

    public bool ltrFrame
    {
        get => (this.bitFields & (1u << 1)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 1)) : (this.bitFields & ~(1u << 1));
    }

    public bool getRCStats
    {
        get => (this.bitFields & (1u << 2)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 2)) : (this.bitFields & ~(1u << 2));
    }

    // No property for reserved bits (must be 0).

    public IntPtr outputBitstream;      // Pointer to the bitstream buffer being locked.
    public IntPtr sliceOffsets;         // Pointer to an array of uint (slice offsets).
    public uint frameIdx;               // Frame number for which the bitstream is retrieved.
    public uint hwEncodeStatus;         // NVENC status for the locked picture.
    public uint numSlices;              // Number of slices/tiles.
    public uint bitstreamSizeInBytes;   // Size in bytes of the generated bitstream.
    public ulong outputTimeStamp;       // Presentation timestamp.
    public ulong outputDuration;        // Presentation duration.
    public IntPtr bitstreamBufferPtr;   // Pointer to the generated output bitstream.
    public NV_ENC_PIC_TYPE pictureType; // Picture type (enum).
    public NV_ENC_PIC_STRUCT pictureStruct; // Structure describing the picture.
    public uint frameAvgQP;             // Average QP of the frame.
    public uint frameSatd;              // Total SATD cost for the frame.
    public uint ltrFrameIdx;            // LTR frame index.
    public uint ltrFrameBitmap;         // Bitmap of LTR frame indices.
    public uint temporalId;             // Temporal ID when using temporal SVC.
    public uint intraMBCount;           // Number of intra MBs/CTBs/SBs.
    public uint interMBCount;           // Number of inter MBs/CTBs/SBs.
    public int averageMVX;              // Average motion vector X.
    public int averageMVY;              // Average motion vector Y.
    public uint alphaLayerSizeInBytes;  // Size in bytes of alpha layer (if used).
    public uint outputStatsPtrSize;     // Size of the output stats buffer.
    public uint reserved;               // Reserved; must be 0.
    public IntPtr outputStatsPtr;       // Pointer to the output stats buffer.
    public uint frameIdxDisplay;        // Display order frame index.

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 219)]
    public uint[] reserved1;            // Reserved, must be 0.

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 63)]
    public IntPtr[] reserved2;          // Reserved, must be NULL.

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public uint[] reservedInternal;     // Reserved, must be 0.
}

[StructLayout(LayoutKind.Sequential)]
public struct NV_ENC_LOCK_INPUT_BUFFER
{
    public uint version; // Must be set to NV_ENC_LOCK_INPUT_BUFFER_VER

    // 32-bit bitfield: Bit 0: doNotWait (1 bit); Bits 1-31: reserved (31 bits)
    private uint bitFields;

    public bool doNotWait
    {
        get => (this.bitFields & 1u) != 0;
        set => this.bitFields = value ? (this.bitFields | 1u) : (this.bitFields & ~1u);
    }
    // Reserved bits not exposed.

    public IntPtr inputBuffer;   // Pointer to the input buffer (NV_ENC_INPUT_PTR).
    public IntPtr bufferDataPtr; // Pointer to the locked input buffer data.
    public uint pitch;           // Pitch of the locked input buffer.

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 251)]
    public uint[] reserved1;     // Reserved, must be 0.

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
    public IntPtr[] reserved2;   // Reserved, must be NULL.
}

[StructLayout(LayoutKind.Sequential)]
public struct NV_ENC_PRESET_CONFIG
{
    public uint version;
    public uint reserved;
    public NV_ENC_CONFIG presetConfig;
    public uint[] reserved1;
    public nint[] reserved2;
}

[StructLayout(LayoutKind.Sequential)]
public struct NV_ENC_QP
{
    public uint qpInterP;
    public uint qpInterB;
    public uint qpIntra;
}

[StructLayout(LayoutKind.Sequential)]
public struct NV_ENC_RC_PARAMS
{
    public uint version;
    public NV_ENC_PARAMS_RC_MODE rateControlMode;
    public NV_ENC_QP constQP;
    public uint averageBitRate;
    public uint maxBitRate;
    public uint vbvBufferSize;
    public uint vbvInitialDelay;

    // Combined 32-bit bit field:
    // Bit  0: enableMinQP
    // Bit  1: enableMaxQP
    // Bit  2: enableInitialRCQP
    // Bit  3: enableAQ
    // Bit  4: reservedBitField1 (must be 0)
    // Bit  5: enableLookahead
    // Bit  6: disableIadapt
    // Bit  7: disableBadapt
    // Bit  8: enableTemporalAQ
    // Bit  9: zeroReorderDelay
    // Bit 10: enableNonRefP
    // Bit 11: strictGOPTarget
    // Bits 12-15: aqStrength (4 bits)
    // Bit 16: enableExtLookahead
    // Bits 17-31: reservedBitFields (15 bits, must be 0)
    private uint bitFields;

    // Helper properties to access individual bit fields:
    public bool enableMinQP
    {
        get => (this.bitFields & (1u << 0)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 0)) : (this.bitFields & ~(1u << 0));
    }
    public bool enableMaxQP
    {
        get => (this.bitFields & (1u << 1)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 1)) : (this.bitFields & ~(1u << 1));
    }
    public bool enableInitialRCQP
    {
        get => (this.bitFields & (1u << 2)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 2)) : (this.bitFields & ~(1u << 2));
    }
    public bool enableAQ
    {
        get => (this.bitFields & (1u << 3)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 3)) : (this.bitFields & ~(1u << 3));
    }
    // Bit 4 is reservedBitField1 (no property needed)
    public bool enableLookahead
    {
        get => (this.bitFields & (1u << 5)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 5)) : (this.bitFields & ~(1u << 5));
    }
    public bool disableIadapt
    {
        get => (this.bitFields & (1u << 6)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 6)) : (this.bitFields & ~(1u << 6));
    }
    public bool disableBadapt
    {
        get => (this.bitFields & (1u << 7)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 7)) : (this.bitFields & ~(1u << 7));
    }
    public bool enableTemporalAQ
    {
        get => (this.bitFields & (1u << 8)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 8)) : (this.bitFields & ~(1u << 8));
    }
    public bool zeroReorderDelay
    {
        get => (this.bitFields & (1u << 9)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 9)) : (this.bitFields & ~(1u << 9));
    }
    public bool enableNonRefP
    {
        get => (this.bitFields & (1u << 10)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 10)) : (this.bitFields & ~(1u << 10));
    }
    public bool strictGOPTarget
    {
        get => (this.bitFields & (1u << 11)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 11)) : (this.bitFields & ~(1u << 11));
    }
    public byte aqStrength
    {
        get => (byte)((this.bitFields >> 12) & 0xF);
        set => this.bitFields = (this.bitFields & ~(0xFu << 12)) | (((uint)value & 0xF) << 12);
    }
    public bool enableExtLookahead
    {
        get => (this.bitFields & (1u << 16)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 16)) : (this.bitFields & ~(1u << 16));
    }
    // The remaining 15 bits (bits 17-31) are reserved.

    public NV_ENC_QP minQP;
    public NV_ENC_QP maxQP;
    public NV_ENC_QP initialRCQP;
    public uint temporallayerIdxMask;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] temporalLayerQP;

    public byte targetQuality;
    public byte targetQualityLSB;
    public ushort lookaheadDepth;
    public byte lowDelayKeyFrameScale;
    public sbyte yDcQPIndexOffset;
    public sbyte uDcQPIndexOffset;
    public sbyte vDcQPIndexOffset;
    public NV_ENC_QP_MAP_MODE qpMapMode;
    public NV_ENC_MULTI_PASS multiPass;
    public uint alphaLayerBitrateRatio;
    public sbyte cbQPIndexOffset;
    public sbyte crQPIndexOffset;
    public ushort reserved2;
    public NV_ENC_LOOKAHEAD_LEVEL lookaheadLevel;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = Constants.MAX_NUM_VIEWS_MINUS_1)]
    public byte[] viewBitrateRatios;

    public byte reserved3;
    public uint reserved1;
}

public static class Constants
{
    public const int MAX_NUM_VIEWS_MINUS_1 = 7;
}

[StructLayout(LayoutKind.Sequential)]
public struct NV_ENC_CONFIG
{
    //#define NV_ENC_CONFIG_VER (NVENCAPI_STRUCT_VERSION(9) | ( 1<<31 ))
    public uint version;
    public Guid profileGUID;
    public uint gopLength;
    public int frameIntervalP;
    public uint monoChromeEncoding;
    public NV_ENC_PARAMS_FRAME_FIELD_MODE frameFieldMode;
    public NV_ENC_MV_PRECISION mvPrecision;
    public NV_ENC_RC_PARAMS rcParams;
    public NV_ENC_CODEC_CONFIG encodeCodecConfig;
    public uint[] reserved; // 278
    public nint[] reserved2; // 64
}

[StructLayout(LayoutKind.Explicit)]
public struct NV_ENC_CODEC_CONFIG
{
    [FieldOffset(0)]
    public NV_ENC_CONFIG_H264 h264Config;

    // TODO: This shit.
    //[FieldOffset(0)]
    //public NV_ENC_CONFIG_HEVC hevcConfig;

    //[FieldOffset(0)]
    //public NV_ENC_CONFIG_AV1 av1Config;

    //[FieldOffset(0)]
    //public NV_ENC_CONFIG_H264_MEONLY h264MeOnlyConfig;

    //[FieldOffset(0)]
    //public NV_ENC_CONFIG_HEVC_MEONLY hevcMeOnlyConfig;

    // Reserved array occupies the same space.
    [FieldOffset(0)]
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 320)]
    public uint[] reserved;
}

[StructLayout(LayoutKind.Sequential)]
public struct NV_ENC_CONFIG_H264
{
    // Bitfields combined into a single 32-bit integer.
    private uint bitField0;

    // Bitfield properties.
    public bool enableTemporalSVC
    {
        get => (this.bitField0 & (1u << 0)) != 0;
        set => this.bitField0 = value ? (this.bitField0 | (1u << 0)) : (this.bitField0 & ~(1u << 0));
    }
    public bool enableStereoMVC
    {
        get => (this.bitField0 & (1u << 1)) != 0;
        set => this.bitField0 = value ? (this.bitField0 | (1u << 1)) : (this.bitField0 & ~(1u << 1));
    }
    public bool hierarchicalPFrames
    {
        get => (this.bitField0 & (1u << 2)) != 0;
        set => this.bitField0 = value ? (this.bitField0 | (1u << 2)) : (this.bitField0 & ~(1u << 2));
    }
    public bool hierarchicalBFrames
    {
        get => (this.bitField0 & (1u << 3)) != 0;
        set => this.bitField0 = value ? (this.bitField0 | (1u << 3)) : (this.bitField0 & ~(1u << 3));
    }
    public bool outputBufferingPeriodSEI
    {
        get => (this.bitField0 & (1u << 4)) != 0;
        set => this.bitField0 = value ? (this.bitField0 | (1u << 4)) : (this.bitField0 & ~(1u << 4));
    }
    public bool outputPictureTimingSEI
    {
        get => (this.bitField0 & (1u << 5)) != 0;
        set => this.bitField0 = value ? (this.bitField0 | (1u << 5)) : (this.bitField0 & ~(1u << 5));
    }
    public bool outputAUD
    {
        get => (this.bitField0 & (1u << 6)) != 0;
        set => this.bitField0 = value ? (this.bitField0 | (1u << 6)) : (this.bitField0 & ~(1u << 6));
    }
    public bool disableSPSPPS
    {
        get => (this.bitField0 & (1u << 7)) != 0;
        set => this.bitField0 = value ? (this.bitField0 | (1u << 7)) : (this.bitField0 & ~(1u << 7));
    }
    public bool outputFramePackingSEI
    {
        get => (this.bitField0 & (1u << 8)) != 0;
        set => this.bitField0 = value ? (this.bitField0 | (1u << 8)) : (this.bitField0 & ~(1u << 8));
    }
    public bool outputRecoveryPointSEI
    {
        get => (this.bitField0 & (1u << 9)) != 0;
        set => this.bitField0 = value ? (this.bitField0 | (1u << 9)) : (this.bitField0 & ~(1u << 9));
    }
    public bool enableIntraRefresh
    {
        get => (this.bitField0 & (1u << 10)) != 0;
        set => this.bitField0 = value ? (this.bitField0 | (1u << 10)) : (this.bitField0 & ~(1u << 10));
    }
    public bool enableConstrainedEncoding
    {
        get => (this.bitField0 & (1u << 11)) != 0;
        set => this.bitField0 = value ? (this.bitField0 | (1u << 11)) : (this.bitField0 & ~(1u << 11));
    }
    public bool repeatSPSPPS
    {
        get => (this.bitField0 & (1u << 12)) != 0;
        set => this.bitField0 = value ? (this.bitField0 | (1u << 12)) : (this.bitField0 & ~(1u << 12));
    }
    public bool enableVFR
    {
        get => (this.bitField0 & (1u << 13)) != 0;
        set => this.bitField0 = value ? (this.bitField0 | (1u << 13)) : (this.bitField0 & ~(1u << 13));
    }
    public bool enableLTR
    {
        get => (this.bitField0 & (1u << 14)) != 0;
        set => this.bitField0 = value ? (this.bitField0 | (1u << 14)) : (this.bitField0 & ~(1u << 14));
    }
    public bool qpPrimeYZeroTransformBypassFlag
    {
        get => (this.bitField0 & (1u << 15)) != 0;
        set => this.bitField0 = value ? (this.bitField0 | (1u << 15)) : (this.bitField0 & ~(1u << 15));
    }
    public bool useConstrainedIntraPred
    {
        get => (this.bitField0 & (1u << 16)) != 0;
        set => this.bitField0 = value ? (this.bitField0 | (1u << 16)) : (this.bitField0 & ~(1u << 16));
    }
    public bool enableFillerDataInsertion
    {
        get => (this.bitField0 & (1u << 17)) != 0;
        set => this.bitField0 = value ? (this.bitField0 | (1u << 17)) : (this.bitField0 & ~(1u << 17));
    }
    public bool disableSVCPrefixNalu
    {
        get => (this.bitField0 & (1u << 18)) != 0;
        set => this.bitField0 = value ? (this.bitField0 | (1u << 18)) : (this.bitField0 & ~(1u << 18));
    }
    public bool enableScalabilityInfoSEI
    {
        get => (this.bitField0 & (1u << 19)) != 0;
        set => this.bitField0 = value ? (this.bitField0 | (1u << 19)) : (this.bitField0 & ~(1u << 19));
    }
    public bool singleSliceIntraRefresh
    {
        get => (this.bitField0 & (1u << 20)) != 0;
        set => this.bitField0 = value ? (this.bitField0 | (1u << 20)) : (this.bitField0 & ~(1u << 20));
    }
    public bool enableTimeCode
    {
        get => (this.bitField0 & (1u << 21)) != 0;
        set => this.bitField0 = value ? (this.bitField0 | (1u << 21)) : (this.bitField0 & ~(1u << 21));
    }
    // Bits 22-31 are reserved.

    // The remaining fields follow.
    public uint level;
    public uint idrPeriod;
    public uint separateColourPlaneFlag;
    public uint disableDeblockingFilterIDC;
    public uint numTemporalLayers;
    public uint spsId;
    public uint ppsId;
    public NV_ENC_H264_ADAPTIVE_TRANSFORM_MODE adaptiveTransformMode;
    public NV_ENC_H264_FMO_MODE fmoMode;
    public NV_ENC_H264_BDIRECT_MODE bdirectMode;
    public NV_ENC_H264_ENTROPY_CODING_MODE entropyCodingMode;
    public NV_ENC_STEREO_PACKING_MODE stereoMode;
    public uint intraRefreshPeriod;
    public uint intraRefreshCnt;
    public uint maxNumRefFrames;
    public uint sliceMode;
    public uint sliceModeData;
    public NV_ENC_CONFIG_H264_VUI_PARAMETERS h264VUIParameters;
    public uint ltrNumFrames;
    public uint ltrTrustMode;
    public uint chromaFormatIDC;
    public uint maxTemporalLayers;
    public NV_ENC_BFRAME_REF_MODE useBFramesAsRef;
    public NV_ENC_NUM_REF_FRAMES numRefL0;
    public NV_ENC_NUM_REF_FRAMES numRefL1;
    public NV_ENC_BIT_DEPTH outputBitDepth;
    public NV_ENC_BIT_DEPTH inputBitDepth;
    public NV_ENC_TEMPORAL_FILTER_LEVEL tfLevel;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 264)]
    public uint[] reserved1;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
    public IntPtr[] reserved2;
}

[StructLayout(LayoutKind.Sequential)]
public struct NV_ENC_CONFIG_H264_VUI_PARAMETERS
{
    public uint overscanInfoPresentFlag;       // Specifies if overscanInfo is present.
    public uint overscanInfo;                  // Overscan information.
    public uint videoSignalTypePresentFlag;    // Indicates if videoFormat and related fields are present.
    public NV_ENC_VUI_VIDEO_FORMAT videoFormat;// Source video format.
    public uint videoFullRangeFlag;            // Output range flag for luma/chroma.
    public uint colourDescriptionPresentFlag;  // Indicates if colour-related fields are present.
    public NV_ENC_VUI_COLOR_PRIMARIES colourPrimaries;  // Color primaries.
    public NV_ENC_VUI_TRANSFER_CHARACTERISTIC transferCharacteristics;  // Transfer characteristics.
    public NV_ENC_VUI_MATRIX_COEFFS colourMatrix; // Matrix coefficients.
    public uint chromaSampleLocationFlag;      // Indicates if chroma sample locations are present.
    public uint chromaSampleLocationTop;       // Chroma sample location for top field.
    public uint chromaSampleLocationBot;       // Chroma sample location for bottom field.
    public uint bitstreamRestrictionFlag;      // Indicates if bitstream restrictions are present.
    public uint timingInfoPresentFlag;         // Indicates if timing info is present.
    public uint numUnitInTicks;                // Number of time units of the clock.
    public uint timeScale;                     // Frequency of the clock.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
    public uint[] reserved;                    // Reserved array (must be set to 0).
}

[StructLayout(LayoutKind.Sequential)]
public struct NV_ENC_OPEN_ENCODE_SESSION_EX_PARAMS
{
    public uint version;                  // Must be set to NV_ENC_OPEN_ENCODE_SESSION_EX_PARAMS_VER.
    public NV_ENC_DEVICE_TYPE deviceType; // E.g., NV_ENC_DEVICE_TYPE_CUDA.
    public IntPtr device;                 // Pointer to the client device (e.g., a CUDA context handle).
    public IntPtr reserved;               // Reserved; must be set to IntPtr.Zero.
    public uint apiVersion;               // Should be set to NVENCAPI_VERSION.

    // Reserved array of 253 uints, must be set to 0.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 253)]
    public uint[] reserved1;

    // Reserved array of 64 pointers, must be set to null.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
    public IntPtr[] reserved2;
}

public static class EncodeGuids
{
    public static Guid NV_ENC_CODEC_H264_GUID { get; } = new Guid("6BC82762-4E63-4ca4-AA85-1E50F321F6BF");
    public static Guid NV_ENC_CODEC_HEVC_GUID { get; } = new Guid("790CDC88-4522-4d7b-9425-BDA9975F7603");
    public static Guid NV_ENC_CODEC_AV1_GUID { get; } = new Guid("0A352289-0AA7-4759-862D-5D15CD16D254");

}

public static class EncodeProfileGuids
{
    public static readonly Guid NV_ENC_CODEC_PROFILE_AUTOSELECT_GUID = new Guid("bfd6f8e7-233c-4341-8b3e-4818523803f4");
    public static readonly Guid NV_ENC_H264_PROFILE_BASELINE_GUID = new Guid("0727bcaa-78c4-4c83-8c2f-ef3dff267c6a");
    public static readonly Guid NV_ENC_H264_PROFILE_MAIN_GUID = new Guid("60b5c1d4-67fe-4790-94d5-c4726d7b6e6d");
    public static readonly Guid NV_ENC_H264_PROFILE_HIGH_GUID = new Guid("e7cbc309-4f7a-4b89-af2a-d537c92be310");
    public static readonly Guid NV_ENC_H264_PROFILE_HIGH_10_GUID = new Guid("8f0c337e-186c-48e9-a69d-7a8334089758");
    public static readonly Guid NV_ENC_H264_PROFILE_HIGH_422_GUID = new Guid("ff3242e9-613c-4295-a1e8-2a7fe94d8133");
    public static readonly Guid NV_ENC_H264_PROFILE_HIGH_444_GUID = new Guid("7ac663cb-a598-4960-b844-339b261a7d52");
    public static readonly Guid NV_ENC_H264_PROFILE_STEREO_GUID = new Guid("40847bf5-33f7-4601-9084-e8fe3c1db8b7");
    public static readonly Guid NV_ENC_H264_PROFILE_PROGRESSIVE_HIGH_GUID = new Guid("b405afac-f32b-417b-89c4-9abeed3e5978");
    public static readonly Guid NV_ENC_H264_PROFILE_CONSTRAINED_HIGH_GUID = new Guid("aec1bd87-e85b-48f2-84c3-98bca6285072");
    public static readonly Guid NV_ENC_HEVC_PROFILE_MAIN_GUID = new Guid("b514c39a-b55b-40fa-878f-f1253b4dfdec");
    public static readonly Guid NV_ENC_HEVC_PROFILE_MAIN10_GUID = new Guid("fa4d2b6c-3a5b-411a-8018-0a3f5e3c9be5");
    public static readonly Guid NV_ENC_HEVC_PROFILE_FREXT_GUID = new Guid("51ec32b5-1b4c-453c-9cbd-b616bd621341");
    public static readonly Guid NV_ENC_AV1_PROFILE_MAIN_GUID = new Guid("5f2a39f5-f14e-4f95-9a9e-b76d568fcf97");
}

public static class EncodePresetGuids
{
    public static Guid P1 { get; } = new Guid("fc0a8d3e-45f8-4cf8-80c7-298871590ebf");
    public static Guid P2 { get; } = new Guid("f581cfb8-88d6-4381-93f0-df13f9c27dab");
    public static Guid P3 { get; } = new Guid("36850110-3a07-441f-94d5-3670631f91f6");
    public static Guid P4 { get; } = new Guid("90a7b826-df06-4862-b9d2-cd6d73a08681");
    public static Guid P5 { get; } = new Guid("21c6e6b4-297a-4cba-998f-b6cbde72ade3");
    public static Guid P6 { get; } = new Guid("8e75c279-6299-4ab6-8302-0b215a335cf5");
    public static Guid P7 { get; } = new Guid("84848c12-6f71-4c13-931b-53e283f57974");

}

public enum NVENCSTATUS
{
    /**
 * This indicates that API call returned with no errors.
 */
    NV_ENC_SUCCESS,

    /**
     * This indicates that no encode capable devices were detected.
     */
    NV_ENC_ERR_NO_ENCODE_DEVICE,

    /**
     * This indicates that devices pass by the client is not supported.
     */
    NV_ENC_ERR_UNSUPPORTED_DEVICE,

    /**
     * This indicates that the encoder device supplied by the client is not
     * valid.
     */
    NV_ENC_ERR_INVALID_ENCODERDEVICE,

    /**
     * This indicates that device passed to the API call is invalid.
     */
    NV_ENC_ERR_INVALID_DEVICE,

    /**
     * This indicates that device passed to the API call is no longer available and
     * needs to be reinitialized. The clients need to destroy the current encoder
     * session by freeing the allocated input output buffers and destroying the device
     * and create a new encoding session.
     */
    NV_ENC_ERR_DEVICE_NOT_EXIST,

    /**
     * This indicates that one or more of the pointers passed to the API call
     * is invalid.
     */
    NV_ENC_ERR_INVALID_PTR,

    /**
     * This indicates that completion event passed in ::NvEncEncodePicture() call
     * is invalid.
     */
    NV_ENC_ERR_INVALID_EVENT,

    /**
     * This indicates that one or more of the parameter passed to the API call
     * is invalid.
     */
    NV_ENC_ERR_INVALID_PARAM,

    /**
     * This indicates that an API call was made in wrong sequence/order.
     */
    NV_ENC_ERR_INVALID_CALL,

    /**
     * This indicates that the API call failed because it was unable to allocate
     * enough memory to perform the requested operation.
     */
    NV_ENC_ERR_OUT_OF_MEMORY,

    /**
     * This indicates that the encoder has not been initialized with
     * ::NvEncInitializeEncoder() or that initialization has failed.
     * The client cannot allocate input or output buffers or do any encoding
     * related operation before successfully initializing the encoder.
     */
    NV_ENC_ERR_ENCODER_NOT_INITIALIZED,

    /**
     * This indicates that an unsupported parameter was passed by the client.
     */
    NV_ENC_ERR_UNSUPPORTED_PARAM,

    /**
     * This indicates that the ::NvEncLockBitstream() failed to lock the output
     * buffer. This happens when the client makes a non blocking lock call to
     * access the output bitstream by passing NV_ENC_LOCK_BITSTREAM::doNotWait flag.
     * This is not a fatal error and client should retry the same operation after
     * few milliseconds.
     */
    NV_ENC_ERR_LOCK_BUSY,

    /**
     * This indicates that the size of the user buffer passed by the client is
     * insufficient for the requested operation.
     */
    NV_ENC_ERR_NOT_ENOUGH_BUFFER,

    /**
     * This indicates that an invalid struct version was used by the client.
     */
    NV_ENC_ERR_INVALID_VERSION,

    /**
     * This indicates that ::NvEncMapInputResource() API failed to map the client
     * provided input resource.
     */
    NV_ENC_ERR_MAP_FAILED,

    /**
     * This indicates encode driver requires more input buffers to produce an output
     * bitstream. If this error is returned from ::NvEncEncodePicture() API, this
     * is not a fatal error. If the client is encoding with B frames then,
     * ::NvEncEncodePicture() API might be buffering the input frame for re-ordering.
     *
     * A client operating in synchronous mode cannot call ::NvEncLockBitstream()
     * API on the output bitstream buffer if ::NvEncEncodePicture() returned the
     * ::NV_ENC_ERR_NEED_MORE_INPUT error code.
     * The client must continue providing input frames until encode driver returns
     * ::NV_ENC_SUCCESS. After receiving ::NV_ENC_SUCCESS status the client can call
     * ::NvEncLockBitstream() API on the output buffers in the same order in which
     * it has called ::NvEncEncodePicture().
     */
    NV_ENC_ERR_NEED_MORE_INPUT,

    /**
     * This indicates that the HW encoder is busy encoding and is unable to encode
     * the input. The client should call ::NvEncEncodePicture() again after few
     * milliseconds.
     */
    NV_ENC_ERR_ENCODER_BUSY,

    /**
     * This indicates that the completion event passed in ::NvEncEncodePicture()
     * API has not been registered with encoder driver using ::NvEncRegisterAsyncEvent().
     */
    NV_ENC_ERR_EVENT_NOT_REGISTERD,

    /**
     * This indicates that an unknown internal error has occurred.
     */
    NV_ENC_ERR_GENERIC,

    /**
     * This indicates that the client is attempting to use a feature
     * that is not available for the license type for the current system.
     */
    NV_ENC_ERR_INCOMPATIBLE_CLIENT_KEY,

    /**
     * This indicates that the client is attempting to use a feature
     * that is not implemented for the current version.
     */
    NV_ENC_ERR_UNIMPLEMENTED,

    /**
     * This indicates that the ::NvEncRegisterResource API failed to register the resource.
     */
    NV_ENC_ERR_RESOURCE_REGISTER_FAILED,

    /**
     * This indicates that the client is attempting to unregister a resource
     * that has not been successfully registered.
     */
    NV_ENC_ERR_RESOURCE_NOT_REGISTERED,

    /**
     * This indicates that the client is attempting to unmap a resource
     * that has not been successfully mapped.
     */
    NV_ENC_ERR_RESOURCE_NOT_MAPPED,

    /**
     * This indicates encode driver requires more output buffers to write an output
     * bitstream. If this error is returned from ::NvEncRestoreEncoderState() API, this
     * is not a fatal error. If the client is encoding with B frames then,
     * ::NvEncRestoreEncoderState() API might be requiring the extra output buffer for accomodating overlay frame output in a separate buffer, for AV1 codec.
     * In this case, client must call NvEncRestoreEncoderState() API again with NV_ENC_RESTORE_ENCODER_STATE_PARAMS::outputBitstream as input along with
     * the parameters in the previous call. When operating in asynchronous mode of encoding, client must also specify NV_ENC_RESTORE_ENCODER_STATE_PARAMS::completionEvent.
     */
    NV_ENC_ERR_NEED_MORE_OUTPUT,
}

[StructLayout(LayoutKind.Sequential)]
public struct NV_ENCODE_API_FUNCTION_LIST
{
    public uint version;    // [in]: set to NV_ENCODE_API_FUNCTION_LIST_VER
    public uint reserved;   // [in]: must be 0

    // All the following are function pointers; for simplicity we declare them as IntPtr.
    public IntPtr nvEncOpenEncodeSession;
    public IntPtr nvEncGetEncodeGUIDCount;
    public IntPtr nvEncGetEncodeProfileGUIDCount;
    public IntPtr nvEncGetEncodeProfileGUIDs;
    public IntPtr nvEncGetEncodeGUIDs;
    public IntPtr nvEncGetInputFormatCount;
    public IntPtr nvEncGetInputFormats;
    public IntPtr nvEncGetEncodeCaps;
    public IntPtr nvEncGetEncodePresetCount;
    public IntPtr nvEncGetEncodePresetGUIDs;
    public IntPtr nvEncGetEncodePresetConfig;
    public IntPtr nvEncInitializeEncoder;
    public IntPtr nvEncCreateInputBuffer;
    public IntPtr nvEncDestroyInputBuffer;
    public IntPtr nvEncCreateBitstreamBuffer;
    public IntPtr nvEncDestroyBitstreamBuffer;
    public IntPtr nvEncEncodePicture;
    public IntPtr nvEncLockBitstream;
    public IntPtr nvEncUnlockBitstream;
    public IntPtr nvEncLockInputBuffer;
    public IntPtr nvEncUnlockInputBuffer;
    public IntPtr nvEncGetEncodeStats;
    public IntPtr nvEncGetSequenceParams;
    public IntPtr nvEncRegisterAsyncEvent;
    public IntPtr nvEncUnregisterAsyncEvent;
    public IntPtr nvEncMapInputResource;
    public IntPtr nvEncUnmapInputResource;
    public IntPtr nvEncDestroyEncoder;
    public IntPtr nvEncInvalidateRefFrames;
    public IntPtr nvEncOpenEncodeSessionEx;
    public IntPtr nvEncRegisterResource;
    public IntPtr nvEncUnregisterResource;
    public IntPtr nvEncReconfigureEncoder;
    public IntPtr reserved1;
    public IntPtr nvEncCreateMVBuffer;
    public IntPtr nvEncDestroyMVBuffer;
    public IntPtr nvEncRunMotionEstimationOnly;
    public IntPtr nvEncGetLastErrorString;
    public IntPtr nvEncSetIOCudaStreams;
    public IntPtr nvEncGetEncodePresetConfigEx;
    public IntPtr nvEncGetSequenceParamEx;
    public IntPtr nvEncRestoreEncoderState;
    public IntPtr nvEncLookaheadPicture;

    // Reserved array (275 pointers) which must be set to null.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 275)]
    public IntPtr[] reserved2;
}

/// <summary>
/// Used to wrap unmanaged delegates for the NVENC API into
/// managed methods.
/// </summary>
public class NvEncDelegateWrapper
{
    // Public  fields/properties for the NVENC functions.
    public NvEncOpenEncodeSessionEx NvEncOpenEncodeSessionEx { get; }
    public NvEncGetEncodeGuidCount NvEncGetEncodeGUIDCount { get; }
    public NvEncGetEncodeProfileGUIDCount NvEncGetEncodeProfileGUIDCount { get; }
    public NvEncGetEncodeProfileGUIDs NvEncGetEncodeProfileGUIDs { get; }
    public NvEncGetEncodeGuids NvEncGetEncodeGUIDs { get; }
    public NvEncGetInputFormatCount NvEncGetInputFormatCount { get; }
    public NvEncGetInputFormats NvEncGetInputFormats { get; }
    public NvEncGetEncodeCaps NvEncGetEncodeCaps { get; }
    public NvEncGetEncodePresetCount NvEncGetEncodePresetCount { get; }
    public NvEncGetEncodePresetGUIDs NvEncGetEncodePresetGUIDs { get; }
    //public NvEncGetEncodePresetConfig NvEncGetEncodePresetConfig { get; }
    public NvEncInitializeEncoder NvEncInitializeEncoder { get; }
    public NvEncCreateInputBuffer NvEncCreateInputBuffer { get; }
    public NvEncDestroyInputBuffer NvEncDestroyInputBuffer { get; }
    public NvEncCreateBitstreamBuffer NvEncCreateBitstreamBuffer { get; }
    public NvEncDestroyBitstreamBuffer NvEncDestroyBitstreamBuffer { get; }
    public NvEncEncodePicture NvEncEncodePicture { get; }
    public NvEncLockBitstream NvEncLockBitstream { get; }
    public NvEncUnlockBitstream NvEncUnlockBitstream { get; }
    public NvEncLockInputBuffer NvEncLockInputBuffer { get; }
    public NvEncUnlockInputBuffer NvEncUnlockInputBuffer { get; }
    //public NvEncGetEncodeStats NvEncGetEncodeStats { get; }
    //public NvEncGetSequenceParams NvEncGetSequenceParams { get; }
    //public NvEncRegisterAsyncEvent NvEncRegisterAsyncEvent { get; }
    //public NvEncUnregisterAsyncEvent NvEncUnregisterAsyncEvent { get; }
    //public NvEncMapInputResource NvEncMapInputResource { get; }
    //public NvEncUnmapInputResource NvEncUnmapInputResource { get; }
    public NvEncDestroyEncoder NvEncDestroyEncoder { get; }
    public NvEncInvalidateRefFrames NvEncInvalidateRefFrames { get; }
    //public NvEncRegisterResource NvEncRegisterResource { get; }
    //public NvEncUnregisterResource NvEncUnregisterResource { get; }
    public NvEncReconfigureEncoder NvEncReconfigureEncoder { get; }
    //public NvEncCreateMVBuffer NvEncCreateMVBuffer { get; }
    //public NvEncDestroyMVBuffer NvEncDestroyMVBuffer { get; }
    //public NvEncRunMotionEstimationOnly NvEncRunMotionEstimationOnly { get; }
    public NvEncGetLastErrorString NvEncGetLastErrorString { get; }
    //public NvEncSetIOCudaStreams NvEncSetIOCudaStreams { get; }
    public NvEncGetEncodePresetConfigEx NvEncGetEncodePresetConfigEx { get; }
    //public NvEncGetSequenceParamEx NvEncGetSequenceParamEx { get; }
    //public NvEncRestoreEncoderState NvEncRestoreEncoderState { get; }
    //public NvEncLookaheadPicture NvEncLookaheadPicture { get; }

    private NV_ENCODE_API_FUNCTION_LIST functionList;

    // Constructor that takes an NV_ENCODE_API_FUNCTION_LIST and converts the pointers to s.
    public NvEncDelegateWrapper(ref NV_ENCODE_API_FUNCTION_LIST functionList)
    {
        this.functionList = functionList;
        // Use Marshal.GetDelegateForFunctionPointer to convert each function pointer.
        this.NvEncOpenEncodeSessionEx = Marshal.GetDelegateForFunctionPointer<NvEncOpenEncodeSessionEx>(functionList.nvEncOpenEncodeSessionEx);
        this.NvEncGetEncodeGUIDCount = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodeGuidCount>(functionList.nvEncGetEncodeGUIDCount);
        this.NvEncGetEncodeProfileGUIDCount = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodeProfileGUIDCount>(functionList.nvEncGetEncodeProfileGUIDCount);
        this.NvEncGetEncodeProfileGUIDs = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodeProfileGUIDs>(functionList.nvEncGetEncodeProfileGUIDs);
        this.NvEncGetEncodeGUIDs = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodeGuids>(functionList.nvEncGetEncodeGUIDs);
        this.NvEncGetInputFormatCount = Marshal.GetDelegateForFunctionPointer<NvEncGetInputFormatCount>(functionList.nvEncGetInputFormatCount);
        this.NvEncGetInputFormats = Marshal.GetDelegateForFunctionPointer<NvEncGetInputFormats>(functionList.nvEncGetInputFormats);
        this.NvEncGetEncodeCaps = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodeCaps>(functionList.nvEncGetEncodeCaps);
        this.NvEncGetEncodePresetCount = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodePresetCount>(functionList.nvEncGetEncodePresetCount);
        this.NvEncGetEncodePresetGUIDs = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodePresetGUIDs>(functionList.nvEncGetEncodePresetGUIDs);
        //this.NvEncGetEncodePresetConfig = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodePresetConfig>(functionList.nvEncGetEncodePresetConfig);
        this.NvEncInitializeEncoder = Marshal.GetDelegateForFunctionPointer<NvEncInitializeEncoder>(functionList.nvEncInitializeEncoder);
        this.NvEncCreateInputBuffer = Marshal.GetDelegateForFunctionPointer<NvEncCreateInputBuffer>(functionList.nvEncCreateInputBuffer);
        this.NvEncDestroyInputBuffer = Marshal.GetDelegateForFunctionPointer<NvEncDestroyInputBuffer>(functionList.nvEncDestroyInputBuffer);
        this.NvEncCreateBitstreamBuffer = Marshal.GetDelegateForFunctionPointer<NvEncCreateBitstreamBuffer>(functionList.nvEncCreateBitstreamBuffer);
        this.NvEncDestroyBitstreamBuffer = Marshal.GetDelegateForFunctionPointer<NvEncDestroyBitstreamBuffer>(functionList.nvEncDestroyBitstreamBuffer);
        this.NvEncEncodePicture = Marshal.GetDelegateForFunctionPointer<NvEncEncodePicture>(functionList.nvEncEncodePicture);
        this.NvEncLockBitstream = Marshal.GetDelegateForFunctionPointer<NvEncLockBitstream>(functionList.nvEncLockBitstream);
        this.NvEncUnlockBitstream = Marshal.GetDelegateForFunctionPointer<NvEncUnlockBitstream>(functionList.nvEncUnlockBitstream);
        this.NvEncLockInputBuffer = Marshal.GetDelegateForFunctionPointer<NvEncLockInputBuffer>(functionList.nvEncLockInputBuffer);
        this.NvEncUnlockInputBuffer = Marshal.GetDelegateForFunctionPointer<NvEncUnlockInputBuffer>(functionList.nvEncUnlockInputBuffer);
        //this.NvEncGetEncodeStats = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodeStats>(functionList.nvEncGetEncodeStats);
        //this.NvEncGetSequenceParams = Marshal.GetDelegateForFunctionPointer<NvEncGetSequenceParams>(functionList.nvEncGetSequenceParams);
        //this.NvEncRegisterAsyncEvent = Marshal.GetDelegateForFunctionPointer<NvEncRegisterAsyncEvent>(functionList.nvEncRegisterAsyncEvent);
        //this.NvEncUnregisterAsyncEvent = Marshal.GetDelegateForFunctionPointer<NvEncUnregisterAsyncEvent>(functionList.nvEncUnregisterAsyncEvent);
        //this.NvEncMapInputResource = Marshal.GetDelegateForFunctionPointer<NvEncMapInputResource>(functionList.nvEncMapInputResource);
        //this.NvEncUnmapInputResource = Marshal.GetDelegateForFunctionPointer<NvEncUnmapInputResource>(functionList.nvEncUnmapInputResource);
        this.NvEncDestroyEncoder = Marshal.GetDelegateForFunctionPointer<NvEncDestroyEncoder>(functionList.nvEncDestroyEncoder);
        this.NvEncInvalidateRefFrames = Marshal.GetDelegateForFunctionPointer<NvEncInvalidateRefFrames>(functionList.nvEncInvalidateRefFrames);
        //this.NvEncRegisterResource = Marshal.GetDelegateForFunctionPointer<NvEncRegisterResource>(functionList.nvEncRegisterResource);
        //this.NvEncUnregisterResource = Marshal.GetDelegateForFunctionPointer<NvEncUnregisterResource>(functionList.nvEncUnregisterResource);
        this.NvEncReconfigureEncoder = Marshal.GetDelegateForFunctionPointer<NvEncReconfigureEncoder>(functionList.nvEncReconfigureEncoder);
        //this.NvEncCreateMVBuffer = Marshal.GetDelegateForFunctionPointer<NvEncCreateMVBuffer>(functionList.nvEncCreateMVBuffer);
        //this.NvEncDestroyMVBuffer = Marshal.GetDelegateForFunctionPointer<NvEncDestroyMVBuffer>(functionList.nvEncDestroyMVBuffer);
        //this.NvEncRunMotionEstimationOnly = Marshal.GetDelegateForFunctionPointer<NvEncRunMotionEstimationOnly>(functionList.nvEncRunMotionEstimationOnly);
        this.NvEncGetLastErrorString = Marshal.GetDelegateForFunctionPointer<NvEncGetLastErrorString>(functionList.nvEncGetLastErrorString);
        //this.NvEncSetIOCudaStreams = Marshal.GetDelegateForFunctionPointer<NvEncSetIOCudaStreams>(functionList.nvEncSetIOCudaStreams);
        this.NvEncGetEncodePresetConfigEx = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodePresetConfigEx>(functionList.nvEncGetEncodePresetConfigEx);
        //this.NvEncGetSequenceParamEx = Marshal.GetDelegateForFunctionPointer<NvEncGetSequenceParamEx>(functionList.nvEncGetSequenceParamEx);
        //this.NvEncRestoreEncoderState = Marshal.GetDelegateForFunctionPointer<NvEncRestoreEncoderState>(functionList.nvEncRestoreEncoderState);
        //this.NvEncLookaheadPicture = Marshal.GetDelegateForFunctionPointer<NvEncLookaheadPicture>(functionList.nvEncLookaheadPicture);
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct NV_ENC_CAPS_PARAM
{
    public uint version;
    public NV_ENC_CAPS capsToQuery;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 62)]
    public nint[] reserved;
}

[StructLayout(LayoutKind.Sequential)]
public struct NV_ENC_RECONFIGURE_PARAMS
{
    public uint version;
    public uint reserved;
    public NV_ENC_INITIALIZE_PARAMS reinitEncodeParams;
    // Packed bit-field:
    // Bit 0: resetEncoder (1 bit)
    // Bit 1: forceIDR (1 bit)
    // Bits 2-31: reserved1 (30 bits, must be 0)
    private uint bitField;

    // Property for resetEncoder flag.
    public bool resetEncoder
    {
        get => (this.bitField & (1u << 0)) != 0;
        set => this.bitField = value ? (this.bitField | (1u << 0)) : (this.bitField & ~(1u << 0));
    }

    // Property for forceIDR flag.
    public bool forceIDR
    {
        get => (this.bitField & (1u << 1)) != 0;
        set => this.bitField = value ? (this.bitField | (1u << 1)) : (this.bitField & ~(1u << 1));
    }

    // The remaining 30 bits (reserved1) are not exposed.

    public uint reserved2;      // Reserved; must be 0.
}

// The NV_ENC_INITIALIZE_PARAMS structure
[StructLayout(LayoutKind.Sequential)]
public struct NV_ENC_INITIALIZE_PARAMS
{
    public uint version;                      // Must be set to NV_ENC_INITIALIZE_PARAMS_VER.
    public Guid encodeGUID;                   // Encode GUID for the encoder to be created.
    public Guid presetGUID;                   // Preset GUID; if set, preset config is applied.
    public uint encodeWidth;                  // Encode width.
    public uint encodeHeight;                 // Encode height.
    public uint darWidth;                     // Display aspect ratio width (or render width for AV1).
    public uint darHeight;                    // Display aspect ratio height (or render height for AV1).
    public uint frameRateNum;                 // Numerator for frame rate.
    public uint frameRateDen;                 // Denominator for frame rate.
    public uint enableEncodeAsync;            // Set to 1 to enable asynchronous encoding.
    public uint enablePTD;                    // Set to 1 to enable Picture Type Decision by NVENC.

    // The following 32 bits are composed of multiple 1-bit fields, a 4-bit field, and reserved bits.
    // Layout (from LSB to MSB):
    // Bit 0: reportSliceOffsets (1 bit)
    // Bit 1: enableSubFrameWrite (1 bit)
    // Bit 2: enableExternalMEHints (1 bit)
    // Bit 3: enableMEOnlyMode (1 bit)
    // Bit 4: enableWeightedPrediction (1 bit)
    // Bits 5-8: splitEncodeMode (4 bits)
    // Bit 9: enableOutputInVidmem (1 bit)
    // Bit 10: enableReconFrameOutput (1 bit)
    // Bit 11: enableOutputStats (1 bit)
    // Bit 12: enableUniDirectionalB (1 bit)
    // Bits 13-31: reserved (19 bits, must be 0)
    private uint bitFields;

    // Helper properties for the bitfields:

    public bool reportSliceOffsets
    {
        get => (this.bitFields & (1u << 0)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 0)) : (this.bitFields & ~(1u << 0));
    }
    public bool enableSubFrameWrite
    {
        get => (this.bitFields & (1u << 1)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 1)) : (this.bitFields & ~(1u << 1));
    }
    public bool enableExternalMEHints
    {
        get => (this.bitFields & (1u << 2)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 2)) : (this.bitFields & ~(1u << 2));
    }
    public bool enableMEOnlyMode
    {
        get => (this.bitFields & (1u << 3)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 3)) : (this.bitFields & ~(1u << 3));
    }
    public bool enableWeightedPrediction
    {
        get => (this.bitFields & (1u << 4)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 4)) : (this.bitFields & ~(1u << 4));
    }
    public uint splitEncodeMode
    {
        // Bits 5-8 (4 bits)
        get => (this.bitFields >> 5) & 0xF;
        set => this.bitFields = (this.bitFields & ~(0xFu << 5)) | ((value & 0xF) << 5);
    }
    public bool enableOutputInVidmem
    {
        get => (this.bitFields & (1u << 9)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 9)) : (this.bitFields & ~(1u << 9));
    }
    public bool enableReconFrameOutput
    {
        get => (this.bitFields & (1u << 10)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 10)) : (this.bitFields & ~(1u << 10));
    }
    public bool enableOutputStats
    {
        get => (this.bitFields & (1u << 11)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 11)) : (this.bitFields & ~(1u << 11));
    }
    public bool enableUniDirectionalB
    {
        get => (this.bitFields & (1u << 12)) != 0;
        set => this.bitFields = value ? (this.bitFields | (1u << 12)) : (this.bitFields & ~(1u << 12));
    }
    // The remaining 19 bits are reserved and not exposed.

    public uint privDataSize;                 // Reserved private data buffer size; must be 0.
    public uint reserved;                     // Reserved; must be 0.
    public IntPtr privData;                   // Reserved private data buffer; must be NULL.
    public IntPtr encodeConfig;               // Pointer to an NV_ENC_CONFIG structure.
    public uint maxEncodeWidth;               // Maximum encode width for dynamic resolution changes.
    public uint maxEncodeHeight;              // Maximum encode height for dynamic resolution changes.

    // An array of 2 elements for external ME hint counts per block type.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public NVENC_EXTERNAL_ME_HINT_COUNTS_PER_BLOCKTYPE[] maxMEHintCountsPerBlock;

    public NV_ENC_TUNING_INFO tuningInfo;     // NVENC tuning information.
    public NV_ENC_BUFFER_FORMAT bufferFormat; // Input buffer format (used only for DX12 interface).
    public uint numStateBuffers;              // Number of state buffers to allocate for saving encoder state.
    public NV_ENC_OUTPUT_STATS_LEVEL outputStatsLevel; // Level for encoded frame output stats.

    // Reserved array of 284 uints.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 284)]
    public uint[] reserved1;

    // Reserved array of 64 pointers.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
    public IntPtr[] reserved2;
}

[StructLayout(LayoutKind.Sequential)]
public struct NVENC_EXTERNAL_ME_HINT_COUNTS_PER_BLOCKTYPE
{
    // Packed bitfields (32 bits total):
    // Bits [0-3]:   numCandsPerBlk16x16 (4 bits)
    // Bits [4-7]:   numCandsPerBlk16x8  (4 bits)
    // Bits [8-11]:  numCandsPerBlk8x16  (4 bits)
    // Bits [12-15]: numCandsPerBlk8x8   (4 bits)
    // Bits [16-23]: numCandsPerSb       (8 bits)
    // Bits [24-31]: reserved            (8 bits)
    private uint packed;

    /// <summary>
    /// Specifies the number of candidates per 16x16 block (4 bits).
    /// </summary>
    public byte NumCandsPerBlk16x16
    {
        get => (byte)(this.packed & 0xF);
        set => this.packed = (this.packed & ~0xFu) | ((uint)(value & 0xF));
    }

    /// <summary>
    /// Specifies the number of candidates per 16x8 block (4 bits).
    /// </summary>
    public byte NumCandsPerBlk16x8
    {
        get => (byte)((this.packed >> 4) & 0xF);
        set => this.packed = (this.packed & ~(0xFu << 4)) | (((uint)(value & 0xF)) << 4);
    }

    /// <summary>
    /// Specifies the number of candidates per 8x16 block (4 bits).
    /// </summary>
    public byte NumCandsPerBlk8x16
    {
        get => (byte)((this.packed >> 8) & 0xF);
        set => this.packed = (this.packed & ~(0xFu << 8)) | (((uint)(value & 0xF)) << 8);
    }

    /// <summary>
    /// Specifies the number of candidates per 8x8 block (4 bits).
    /// </summary>
    public byte NumCandsPerBlk8x8
    {
        get => (byte)((this.packed >> 12) & 0xF);
        set => this.packed = (this.packed & ~(0xFu << 12)) | (((uint)(value & 0xF)) << 12);
    }

    /// <summary>
    /// Specifies the number of candidates per SB (8 bits).
    /// </summary>
    public byte NumCandsPerSb
    {
        get => (byte)((this.packed >> 16) & 0xFF);
        set => this.packed = (this.packed & ~(0xFFu << 16)) | (((uint)(value & 0xFF)) << 16);
    }

    /// <summary>
    /// Reserved for padding (8 bits).
    /// </summary>
    public byte Reserved
    {
        get => (byte)((this.packed >> 24) & 0xFF);
        set => this.packed = (this.packed & ~(0xFFu << 24)) | (((uint)(value & 0xFF)) << 24);
    }

    // Reserved for future use: an array of 3 uints.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public uint[] reserved1;
}

[StructLayout(LayoutKind.Sequential)]
public struct NV_ENC_CREATE_INPUT_BUFFER
{
    public uint version;       // Must be set to NV_ENC_CREATE_INPUT_BUFFER_VER.
    public uint width;         // Input frame width.
    public uint height;        // Input frame height.
    public NV_ENC_MEMORY_HEAP memoryHeap;  // Deprecated; do not use.
    public NV_ENC_BUFFER_FORMAT bufferFmt;  // Input buffer format.
    public uint reserved;      // Reserved, must be 0.

    // Output pointer for the input buffer.
    public IntPtr inputBuffer; // NV_ENC_INPUT_PTR

    // Pointer to an existing system memory buffer.
    public IntPtr pSysMemBuffer;

    // Reserved array of 58 uints.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 58)]
    public uint[] reserved1;

    // Reserved array of 63 void* pointers.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 63)]
    public IntPtr[] reserved2;
}

[StructLayout(LayoutKind.Sequential)]
public struct NV_ENC_CREATE_BITSTREAM_BUFFER
{
    public uint version;
    public uint size;
    public NV_ENC_MEMORY_HEAP memoryHeap;
    public uint reserved;
    public nint bitstreamBuffer;
    public nint bitStreamBufferPtr;
    // Reserved array of 58 uints.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 58)]
    public uint[] reserved1;

    // Reserved array of 63 void* pointers.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
    public IntPtr[] reserved2;
}
