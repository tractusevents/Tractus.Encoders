using Serilog;
using System;
using System.Runtime.InteropServices;

namespace Tractus.Encoders.Nvidia;

public sealed record CudaDeviceInfo(int Ordinal, int Device, string Name);

public unsafe class NvencEncoderWrapper
{
    private NvEncDelegateWrapper methods;
    private nint cudaContextPtr;
    private NV_ENCODE_API_FUNCTION_LIST functionList;
    private Guid selectedCodec;
    private int width;
    private int height;
    private int fpsNumerator;
    private int fpsDenominator;

    public bool IsH264 => this.selectedCodec == EncodeGuids.NV_ENC_CODEC_H264_GUID;
    public bool IsHEVC => this.selectedCodec == EncodeGuids.NV_ENC_CODEC_HEVC_GUID;
    public bool IsAV1 => this.selectedCodec == EncodeGuids.NV_ENC_CODEC_AV1_GUID;

    public int CudaDeviceOrdinal { get; }
    public string CudaDeviceName { get; }

    public static NvencEncoderWrapper CreateInitialized(
        Guid codec,
        int bitrateBps,
        int width,
        int height,
        int fpsNumerator = 60,
        int fpsDenominator = 1,
        int keyFrameIntervalFrames = 1,
        int? cudaDeviceOrdinal = null)
    {
        var requestedOrdinal = cudaDeviceOrdinal ?? GetConfiguredCudaDeviceOrdinal();
        var candidates = GetCandidateCudaDevices(requestedOrdinal);
        var errors = new List<string>();

        foreach (var candidate in candidates)
        {
            NvencEncoderWrapper? wrapper = null;
            try
            {
                wrapper = new NvencEncoderWrapper(candidate.Ordinal);
                wrapper.Initialize(codec, bitrateBps, width, height, fpsNumerator, fpsDenominator, keyFrameIntervalFrames);
                Log.Logger.Information("NVENC initialized on CUDA device {Ordinal}: {Name}", candidate.Ordinal, candidate.Name);
                return wrapper;
            }
            catch (Exception ex)
            {
                errors.Add($"CUDA device {candidate.Ordinal} ({candidate.Name}): {ex.Message}");
                Log.Logger.Warning(ex, "NVENC initialization failed on CUDA device {Ordinal}: {Name}", candidate.Ordinal, candidate.Name);
                wrapper?.Destroy();
            }
        }

        throw new Exception("Could not initialize NVENC on any candidate CUDA device. " + string.Join(" ", errors));
    }

    public static int? GetConfiguredCudaDeviceOrdinal()
    {
        var raw = Environment.GetEnvironmentVariable("TRACTUS_NVENC_CUDA_DEVICE");
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = Environment.GetEnvironmentVariable("NVENC_CUDA_DEVICE");
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (int.TryParse(raw, out var ordinal) && ordinal >= 0)
        {
            return ordinal;
        }

        throw new Exception($"Invalid NVENC CUDA device ordinal '{raw}'. Use a zero-based CUDA ordinal.");
    }

    public static IReadOnlyList<CudaDeviceInfo> GetCudaDevices()
    {
        var initCudaResult = CudaNative.cuInit(0);
        Log.Logger.Debug($"cuInit(0): {initCudaResult}");
        if (initCudaResult != CUresult.CUDA_SUCCESS)
        {
            throw new Exception($"Could not initialize CUDA. Result code: {initCudaResult}");
        }

        var countResult = CudaNative.cuDeviceGetCount(out var count);
        Log.Logger.Debug($"cuDeviceGetCount: {countResult}, Count: {count}");
        if (countResult != CUresult.CUDA_SUCCESS)
        {
            throw new Exception($"Could not enumerate CUDA devices. Result code: {countResult}");
        }

        if (count <= 0)
        {
            throw new Exception("CUDA initialized successfully, but no CUDA devices were found.");
        }

        var devices = new List<CudaDeviceInfo>(count);
        for (var ordinal = 0; ordinal < count; ordinal++)
        {
            var getDeviceResult = CudaNative.cuDeviceGet(out var device, ordinal);
            Log.Logger.Debug($"cuDeviceGet({ordinal}): {getDeviceResult}, CUDA Device #: {device}");
            if (getDeviceResult != CUresult.CUDA_SUCCESS)
            {
                continue;
            }

            devices.Add(new CudaDeviceInfo(ordinal, device, CudaNative.GetDeviceName(device)));
        }

        if (devices.Count == 0)
        {
            throw new Exception("CUDA devices were reported, but none could be opened.");
        }

        return devices;
    }

    private static IReadOnlyList<CudaDeviceInfo> GetCandidateCudaDevices(int? requestedOrdinal)
    {
        var devices = GetCudaDevices();
        if (!requestedOrdinal.HasValue)
        {
            Log.Logger.Information(
                "NVENC will try {DeviceCount} CUDA device(s). Set TRACTUS_NVENC_CUDA_DEVICE or --nvenccudadevice to force a specific ordinal.",
                devices.Count);
            return devices;
        }

        var device = devices.FirstOrDefault(x => x.Ordinal == requestedOrdinal.Value);
        if (device is null)
        {
            var available = string.Join(", ", devices.Select(x => $"{x.Ordinal} ({x.Name})"));
            throw new Exception($"Requested CUDA device ordinal {requestedOrdinal.Value} was not found. Available devices: {available}");
        }

        return [device];
    }

    public NvencEncoderWrapper(int? cudaDeviceOrdinal = null)
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

        var requestedOrdinal = cudaDeviceOrdinal ?? GetConfiguredCudaDeviceOrdinal();
        var deviceInfo = GetCandidateCudaDevices(requestedOrdinal)[0];

        var ctxResult = CudaNative.cuCtxCreate(out var pCtx, 0, deviceInfo.Device);
        Log.Logger.Debug($"cuCtxCreate CUDA ordinal {deviceInfo.Ordinal} ({deviceInfo.Name}): {ctxResult}, pCtx: {pCtx}");
        if (ctxResult != CUresult.CUDA_SUCCESS)
        {
            throw new Exception($"Could not open a CUDA context on device {deviceInfo.Ordinal} ({deviceInfo.Name}). Result code: {ctxResult}");
        }

        this.functionList = nvencList;
        this.cudaContextPtr = pCtx;
        this.CudaDeviceOrdinal = deviceInfo.Ordinal;
        this.CudaDeviceName = deviceInfo.Name;
    }

    // TODO: We need to complete the close/dispose the encoder properly so we can test it at launch.
    public void Initialize(
        Guid codec,
        int bitrateBps,
        int width,
        int height,
        int fpsNumerator = 60,
        int fpsDenominator = 1,
        int keyFrameIntervalFrames = 1)
    {
        this.selectedCodec = codec;
        this.width = width;
        this.height = height;
        this.fpsNumerator = fpsNumerator;
        this.fpsDenominator = fpsDenominator;
        var normalizedKeyFrameIntervalFrames = Math.Max(1, keyFrameIntervalFrames);
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
            throw new Exception($"Could not start an NVENC session on CUDA device {this.CudaDeviceOrdinal} ({this.CudaDeviceName}).\r\n\r\nResult code: {cudaNvencSessionResult}\r\n{lastError}");
        }

        this.encoderPtr = encoderPtr;

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
                version = NvEncodeApiVersion.NV_ENC_CONFIG_VER,
                rcParams = new NV_ENC_RC_PARAMS
                {
                    temporalLayerQP = new byte[8],
                    reserved = new uint[4]
                },
                reserved = new uint[278],
                reserved2 = new nint[64]
            },
            reserved1 = new uint[255],
            reserved2 = new nint[64]
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

        var codecConfig = presetConfig.presetConfig.encodeCodecConfig;

        if (codec == EncodeGuids.NV_ENC_CODEC_H264_GUID)
        {
            var h264Config = codecConfig.h264Config;
            h264Config.repeatSPSPPS = true;
            h264Config.idrPeriod = (uint)normalizedKeyFrameIntervalFrames;
            h264Config.intraRefreshPeriod = 0;
            h264Config.intraRefreshCnt = 0;
            h264Config.useBFramesAsRef = NV_ENC_BFRAME_REF_MODE.NV_ENC_BFRAME_REF_MODE_DISABLED;
            codecConfig.h264Config = h264Config;
        }
        else if (codec == EncodeGuids.NV_ENC_CODEC_HEVC_GUID)
        {
            var hevcConfig = codecConfig.hevcConfig;
            hevcConfig.enableLTR = false;
            hevcConfig.repeatSPSPPS = true;
            hevcConfig.idrPeriod = (uint)normalizedKeyFrameIntervalFrames;
            hevcConfig.intraRefreshPeriod = 0;
            hevcConfig.intraRefreshCnt = 0;
            hevcConfig.useBFramesAsRef = NV_ENC_BFRAME_REF_MODE.NV_ENC_BFRAME_REF_MODE_DISABLED;
            codecConfig.hevcConfig = hevcConfig;
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
        presetConfig.presetConfig.gopLength = (uint)normalizedKeyFrameIntervalFrames;
        //presetConfig.presetConfig.frameFieldMode = NV_ENC_PARAMS_FRAME_FIELD_MODE.NV_ENC_PARAMS_FRAME_FIELD_MODE_FRAME;

        presetConfig.presetConfig.encodeCodecConfig = codecConfig;
        presetConfig.presetConfig.rcParams.rateControlMode = NV_ENC_PARAMS_RC_MODE.NV_ENC_PARAMS_RC_CBR;
        presetConfig.presetConfig.rcParams.averageBitRate = (uint)bitrateBps;
        //presetConfig.presetConfig.rcParams.enableAQ = true;
        presetConfig.presetConfig.rcParams.zeroReorderDelay = true;

        //presetConfig.presetConfig.frameIntervalP = 1;
        //presetConfig.presetConfig.frameIntervalP = 1;
        //presetConfig.presetConfig.frameIntervalP = 1;
        //presetConfig.presetConfig.frameIntervalP = 1;
        //presetConfig.presetConfig.frameIntervalP = 1;

        presetConfig.presetConfig.reserved ??= new uint[278];
        presetConfig.presetConfig.reserved2 ??= new nint[64];
        presetConfig.presetConfig.rcParams.temporalLayerQP ??= new byte[8];
        presetConfig.presetConfig.rcParams.reserved ??= new uint[4];

        Marshal.StructureToPtr(presetConfig.presetConfig, pNvEncConfig, false);

        var createEncoderParams = new NV_ENC_INITIALIZE_PARAMS()
        {
            version = NvEncodeApiVersion.NV_ENC_INITIALIZE_PARAMS_VER,
            enableEncodeAsync = 0,
            encodeGUID = encoderGuid,
            presetGUID = preset,
            encodeWidth = (uint)this.width,
            encodeHeight = (uint)this.height,
            frameRateNum = (uint)this.fpsNumerator,
            frameRateDen = (uint)this.fpsDenominator,
            enablePTD = 1,
            tuningInfo = NV_ENC_TUNING_INFO.NV_ENC_TUNING_INFO_LOW_LATENCY,
            encodeConfig = pNvEncConfig,
            enableMEOnlyMode = false,

            bufferFormat = NV_ENC_BUFFER_FORMAT.NV_ENC_BUFFER_FORMAT_NV12,
            //outputStatsLevel = NV_ENC_OUTPUT_STATS_LEVEL.NV_ENC_OUTPUT_STATS_NONE,
            reserved1 = new uint[287],
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
            width = (uint)this.width,
            height = (uint)this.height,
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

        Log.Logger.Debug($"NVENC encoder created on CUDA device {this.CudaDeviceOrdinal} ({this.CudaDeviceName}) - {this.bufferFmt.ToString()} -- {(encoderGuid == EncodeGuids.NV_ENC_CODEC_HEVC_GUID ? "HEVC" : encoderGuid == EncodeGuids.NV_ENC_CODEC_H264_GUID ? "H264" : encoderGuid.ToString())}");
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
        if (this.encoderPtr != nint.Zero)
        {
            if (this.inputBuffer != nint.Zero)
            {
                this.methods.NvEncDestroyInputBuffer(this.encoderPtr, this.inputBuffer);
                this.inputBuffer = nint.Zero;
            }

            if (this.bitstreamBuffer != nint.Zero)
            {
                this.methods.NvEncDestroyBitstreamBuffer(this.encoderPtr, this.bitstreamBuffer);
                this.bitstreamBuffer = nint.Zero;
            }

            this.methods.NvEncDestroyEncoder(this.encoderPtr);
            this.encoderPtr = nint.Zero;
        }

        if (this.cudaContextPtr != nint.Zero)
        {
            CudaNative.cuCtxDestroy(this.cudaContextPtr);
            this.cudaContextPtr = nint.Zero;
        }
    }

    public NvencBitstreamLockResult Encode(nint nv12Data, bool forceKeyFrame = true)
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

        var bufferSize = (this.width * this.height * 3) / 2;

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
            encodePicFlags = forceKeyFrame
                ? NV_ENC_PIC_FLAGS.NV_ENC_PIC_FLAG_FORCEINTRA | NV_ENC_PIC_FLAGS.NV_ENC_PIC_FLAG_OUTPUT_SPSPPS | NV_ENC_PIC_FLAGS.NV_ENC_PIC_FLAG_FORCEIDR
                : 0,
            bufferFmt = this.bufferFmt,
            inputWidth = (uint)this.width,
            inputHeight = (uint)this.height,
            inputBuffer = this.inputBuffer,
            outputBitstream = this.bitstreamBuffer,
            completionEvent = nint.Zero,
            inputTimeStamp = 0,
            pictureStruct = NV_ENC_PIC_STRUCT.NV_ENC_PIC_STRUCT_FRAME,
            inputPitch = (uint)this.width,
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
            reserved2 = new nint[64],
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

