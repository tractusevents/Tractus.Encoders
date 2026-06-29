
using System.Runtime.InteropServices;
using Tractus.Encoders.Nvidia;

namespace Tractus.Encoders.Intel;

public enum QsvCodec
{
    H264 = 1,
    Hevc = 2
}

public readonly record struct QsvEncodedFrame(nint BufferPointer, int SizeInBytes, bool IsKeyFrame);

public sealed unsafe class QsvH264Encoder : QsvNativeEncoder
{
    protected override string WorkerName => "Intel QuickSync H.264";
    protected override QsvCodec Codec => QsvCodec.H264;

    public QsvH264Encoder(int bufferCount)
        : base(bufferCount)
    {
    }
}

public sealed unsafe class QsvHevcEncoder : QsvNativeEncoder
{
    protected override string WorkerName => "Intel QuickSync HEVC";
    protected override QsvCodec Codec => QsvCodec.Hevc;

    public QsvHevcEncoder(int bufferCount)
        : base(bufferCount)
    {
    }
}

public abstract unsafe class QsvNativeEncoder : IDisposable
{
    private const int OutputCapacityFloor = 1024 * 1024;
    private const int MaximumOutputResizeAttempts = 4;
    private const int SyncTimeoutMs = 10000;

    private readonly int bufferCount;
    private nint session;
    private nint loader;
    private SurfaceBuffer[] surfaces = [];
    private nint[] encodedBuffers = [];
    private int[] encodedBufferCapacities = [];
    private AlignedBuffer? insertHeadersBuffer;
    private AlignedBuffer? extParamArrayBuffer;
    private int width;
    private int height;
    private int alignedWidth;
    private int alignedHeight;
    private int pitch;
    private uint frameOrder;
    private bool disposed;

    protected QsvNativeEncoder(int bufferCount)
    {
        if (bufferCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferCount), bufferCount, "QuickSync encoder buffer count must be positive.");
        }

        this.bufferCount = bufferCount;
    }

    protected abstract string WorkerName { get; }
    protected abstract QsvCodec Codec { get; }

    public int Implementation { get; private set; }

    public void Initialize(int width, int height, int bitrateBps)
        => this.Initialize(width, height, bitrateBps, 60, 1, 1);

    public void Initialize(int width, int height, int bitrateBps, int keyFrameIntervalFrames)
        => this.Initialize(width, height, bitrateBps, 60, 1, keyFrameIntervalFrames);

    public void Initialize(int width, int height, int bitrateBps, int fpsNumerator, int fpsDenominator, int keyFrameIntervalFrames)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        QsvNative.ValidateLayout();

        this.ReleaseNativeEncoder();
        this.width = width;
        this.height = height;
        this.alignedWidth = Align16(width);
        this.alignedHeight = Align16(height);
        this.pitch = this.alignedWidth;
        this.frameOrder = 0;

        this.EnsureBuffers(width, height);
        this.InitializeInsertHeadersBuffer();

        var candidates = GetCandidateImplementations();
        var errors = new List<string>();
        foreach (var implementation in candidates)
        {
            if (this.TryInitializeImplementation(
                implementation,
                width,
                height,
                bitrateBps,
                fpsNumerator,
                fpsDenominator,
                keyFrameIntervalFrames,
                out var error))
            {
                this.Implementation = implementation;
                return;
            }

            errors.Add($"implementation {implementation}: {error}");
        }

        throw new InvalidOperationException($"{this.WorkerName} initialization failed on all candidate oneVPL implementations. {string.Join(" ", errors)}");
    }

    public QsvEncodedFrame EncodeFrame(byte* uyvyFrame, int uyvyStride, int frameBuffer, bool forceKeyFrame)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        if (this.session == nint.Zero)
        {
            throw new InvalidOperationException($"{this.WorkerName} has not been initialized.");
        }

        if ((uint)frameBuffer >= (uint)this.surfaces.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(frameBuffer), frameBuffer, $"Invalid {this.WorkerName} framebuffer index.");
        }

        var surfaceBuffer = this.surfaces[frameBuffer];
        ConvertUyvyToNv12(
            uyvyFrame,
            uyvyStride,
            (byte*)surfaceBuffer.Buffer.AlignedPointer.ToPointer(),
            this.pitch,
            ((byte*)surfaceBuffer.Buffer.AlignedPointer.ToPointer()) + (this.pitch * this.alignedHeight),
            this.pitch,
            this.width,
            this.height);

        var surface = CreateSurface(surfaceBuffer.Buffer.AlignedPointer);

        MfxEncodeCtrl ctrl = default;
        MfxEncodeCtrl* ctrlPtr = null;
        if (forceKeyFrame)
        {
            ctrl.FrameType = QsvNative.MfxFrameTypeIdr | QsvNative.MfxFrameTypeI;
            if (this.Codec == QsvCodec.H264)
            {
                this.WriteInsertHeadersBuffer();
                ctrl.NumExtParam = 1;
                ctrl.ExtParam = this.extParamArrayBuffer?.AlignedPointer ?? nint.Zero;
            }

            ctrlPtr = &ctrl;
        }

        for (var resizeAttempt = 0; resizeAttempt <= MaximumOutputResizeAttempts; resizeAttempt++)
        {
            var encodedBuffer = this.encodedBuffers[frameBuffer];
            var encodedCapacity = this.encodedBufferCapacities[frameBuffer];
            var bitstream = new MfxBitstream
            {
                Data = encodedBuffer,
                DataOffset = 0,
                DataLength = 0,
                MaxLength = (uint)encodedCapacity,
                TimeStamp = surface.Data.TimeStamp
            };

            var surfacePtr = &surface;
            var bitstreamPtr = &bitstream;
            var status = QsvNative.EncodeFrameAsync(this.session, ctrlPtr, surfacePtr, bitstreamPtr, out var syncPoint);
            var retryCount = 0;
            while (status == QsvNative.MfxWrnDeviceBusy && retryCount++ < 50)
            {
                Thread.Sleep(1);
                status = QsvNative.EncodeFrameAsync(this.session, ctrlPtr, surfacePtr, bitstreamPtr, out syncPoint);
            }

            if (status is QsvNative.MfxErrNotEnoughBuffer or QsvNative.MfxErrMoreBitstream)
            {
                this.GrowEncodedBuffer(frameBuffer);
                continue;
            }

            if (status == QsvNative.MfxErrMoreData)
            {
                return new QsvEncodedFrame(encodedBuffer, 0, forceKeyFrame);
            }

            if (status < QsvNative.MfxErrNone)
            {
                throw new InvalidOperationException($"{this.WorkerName} encode failed: {QsvNative.DescribeStatus(status)}");
            }

            if (syncPoint != nint.Zero)
            {
                status = QsvNative.SyncOperation(this.session, syncPoint, SyncTimeoutMs);
                if (status < QsvNative.MfxErrNone)
                {
                    throw new InvalidOperationException($"{this.WorkerName} sync failed: {QsvNative.DescribeStatus(status)}");
                }
            }

            var size = (int)bitstream.DataLength;
            if (size <= 0)
            {
                return new QsvEncodedFrame(encodedBuffer, 0, forceKeyFrame);
            }

            var source = ((byte*)encodedBuffer.ToPointer()) + bitstream.DataOffset;
            if (bitstream.DataOffset != 0)
            {
                Buffer.MemoryCopy(source, encodedBuffer.ToPointer(), encodedCapacity, size);
            }

            return new QsvEncodedFrame(encodedBuffer, size, forceKeyFrame || (bitstream.FrameType & QsvNative.MfxFrameTypeIdr) != 0);
        }

        throw new InvalidOperationException($"{this.WorkerName} encode failed: output bitstream buffer is too small after resizing to {this.encodedBufferCapacities[frameBuffer]} bytes.");
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        this.ReleaseNativeEncoder();
        this.ReleaseBuffers();
        this.insertHeadersBuffer?.Dispose();
        this.extParamArrayBuffer?.Dispose();
        GC.SuppressFinalize(this);
    }

    private bool TryInitializeImplementation(
        int implementation,
        int width,
        int height,
        int bitrateBps,
        int fpsNumerator,
        int fpsDenominator,
        int keyFrameIntervalFrames,
        out string error)
    {
        error = string.Empty;
        var initializedSession = nint.Zero;
        var initializedLoader = nint.Zero;
        var status = QsvNative.MfxErrNone;

        if (!QsvNative.TryCreateLoaderSession(implementation, out initializedLoader, out initializedSession, out error))
        {
            var loaderError = error;
            var version = new MfxVersion { Minor = 35, Major = 1 };
            status = QsvNative.Init(implementation, ref version, out initializedSession);
            if (status < QsvNative.MfxErrNone || initializedSession == nint.Zero)
            {
                error = $"loader: {loaderError}; legacy MFXInit: {QsvNative.DescribeStatus(status)}";
                return false;
            }
        }

        try
        {
            var param = this.CreateVideoParam(width, height, bitrateBps, fpsNumerator, fpsDenominator, keyFrameIntervalFrames);
            status = QsvNative.EncodeInit(initializedSession, ref param);
            if (status < QsvNative.MfxErrNone)
            {
                error = QsvNative.DescribeStatus(status);
                return false;
            }

            this.session = initializedSession;
            this.loader = initializedLoader;
            initializedSession = nint.Zero;
            initializedLoader = nint.Zero;
            return true;
        }
        finally
        {
            if (initializedSession != nint.Zero)
            {
                QsvNative.Close(initializedSession);
            }

            if (initializedLoader != nint.Zero)
            {
                QsvNative.Unload(initializedLoader);
            }
        }
    }

    private MfxVideoParam CreateVideoParam(int width, int height, int bitrateBps, int fpsNumerator, int fpsDenominator, int keyFrameIntervalFrames)
    {
        var targetKbps = (ushort)Math.Clamp((int)Math.Ceiling(Math.Max(1, bitrateBps) / 1000.0), 1, ushort.MaxValue);
        return new MfxVideoParam
        {
            AsyncDepth = 1,
            IOPattern = QsvNative.MfxIoPatternInSystemMemory,
            Mfx = new MfxInfoMfx
            {
                LowPower = QsvNative.MfxCodingOptionOn,
                CodecId = this.Codec == QsvCodec.H264 ? QsvNative.MfxCodecAvc : QsvNative.MfxCodecHevc,
                CodecProfile = this.Codec == QsvCodec.H264 ? QsvNative.MfxProfileAvcHigh : QsvNative.MfxProfileHevcMain,
                TargetUsage = QsvNative.MfxTargetUsageBestSpeed,
                TargetKbps = targetKbps,
                MaxKbps = targetKbps,
                RateControlMethod = QsvNative.MfxRateControlCbr,
                GopPicSize = (ushort)Math.Clamp(Math.Max(1, keyFrameIntervalFrames), 1, ushort.MaxValue),
                GopRefDist = 1,
                IdrInterval = 1,
                NumSlice = 1,
                FrameInfo = new MfxFrameInfo
                {
                    FourCC = QsvNative.MfxFourCcNv12,
                    Width = (ushort)this.alignedWidth,
                    Height = (ushort)this.alignedHeight,
                    CropW = (ushort)width,
                    CropH = (ushort)height,
                    FrameRateExtN = (uint)Math.Max(1, fpsNumerator),
                    FrameRateExtD = (uint)Math.Max(1, fpsDenominator),
                    PicStruct = QsvNative.MfxPicStructProgressive,
                    ChromaFormat = QsvNative.MfxChromaFormatYuv420
                }
            }
        };
    }

    private MfxFrameSurface1 CreateSurface(nint buffer)
    {
        var y = buffer;
        var uv = buffer + (this.pitch * this.alignedHeight);
        return new MfxFrameSurface1
        {
            Info = new MfxFrameInfo
            {
                FourCC = QsvNative.MfxFourCcNv12,
                Width = (ushort)this.alignedWidth,
                Height = (ushort)this.alignedHeight,
                CropW = (ushort)this.width,
                CropH = (ushort)this.height,
                PicStruct = QsvNative.MfxPicStructProgressive,
                ChromaFormat = QsvNative.MfxChromaFormatYuv420
            },
            Data = new MfxFrameData
            {
                TimeStamp = this.frameOrder * 3000UL,
                FrameOrder = this.frameOrder++,
                Pitch = (ushort)this.pitch,
                Y = y,
                UV = uv
            }
        };
    }

    private void GrowEncodedBuffer(int frameBuffer)
    {
        var currentCapacity = this.encodedBufferCapacities[frameBuffer];
        var nextCapacity = checked(Math.Max(currentCapacity + OutputCapacityFloor, currentCapacity * 2));

        if (this.encodedBuffers[frameBuffer] != nint.Zero)
        {
            Marshal.FreeHGlobal(this.encodedBuffers[frameBuffer]);
        }

        this.encodedBuffers[frameBuffer] = Marshal.AllocHGlobal(nextCapacity);
        this.encodedBufferCapacities[frameBuffer] = nextCapacity;
    }

    private void EnsureBuffers(int width, int height)
    {
        var surfaceBytes = (long)this.pitch * this.alignedHeight * 3 / 2;
        var rawFrameBytes = (long)width * height * 2;
        if (surfaceBytes <= 0 || rawFrameBytes <= 0 || surfaceBytes > int.MaxValue || rawFrameBytes > int.MaxValue)
        {
            throw new InvalidOperationException($"{this.WorkerName} frame size is not supported: {width}x{height}.");
        }

        var defaultOutputCapacity = Math.Max((int)rawFrameBytes, OutputCapacityFloor);
        this.ReleaseBuffers();
        this.surfaces = new SurfaceBuffer[this.bufferCount];
        this.encodedBuffers = new nint[this.bufferCount];
        this.encodedBufferCapacities = new int[this.bufferCount];

        for (var i = 0; i < this.bufferCount; i++)
        {
            this.surfaces[i] = new SurfaceBuffer(new AlignedBuffer((int)surfaceBytes, 32));
            this.encodedBuffers[i] = Marshal.AllocHGlobal(defaultOutputCapacity);
            this.encodedBufferCapacities[i] = defaultOutputCapacity;
        }
    }

    private void InitializeInsertHeadersBuffer()
    {
        this.insertHeadersBuffer?.Dispose();
        this.extParamArrayBuffer?.Dispose();
        this.insertHeadersBuffer = new AlignedBuffer(Marshal.SizeOf<MfxExtInsertHeaders>(), 8);
        this.extParamArrayBuffer = new AlignedBuffer(nint.Size, 8);
        *(nint*)this.extParamArrayBuffer!.AlignedPointer.ToPointer() = this.insertHeadersBuffer!.AlignedPointer;
        this.WriteInsertHeadersBuffer();
    }

    private void WriteInsertHeadersBuffer()
    {
        var insertHeaders = new MfxExtInsertHeaders
        {
            Header = new MfxExtBuffer
            {
                BufferId = QsvNative.MfxExtBuffInsertHeaders,
                BufferSz = (uint)Marshal.SizeOf<MfxExtInsertHeaders>()
            },
            SPS = QsvNative.MfxCodingOptionOn,
            PPS = QsvNative.MfxCodingOptionOn
        };

        if (this.insertHeadersBuffer is null)
        {
            throw new InvalidOperationException($"{this.WorkerName} header buffer has not been initialized.");
        }

        Marshal.StructureToPtr(insertHeaders, this.insertHeadersBuffer.AlignedPointer, false);
    }

    private void ReleaseNativeEncoder()
    {
        if (this.session == nint.Zero)
        {
            if (this.loader != nint.Zero)
            {
                QsvNative.Unload(this.loader);
                this.loader = nint.Zero;
            }

            return;
        }

        QsvNative.EncodeClose(this.session);
        QsvNative.Close(this.session);
        this.session = nint.Zero;

        if (this.loader != nint.Zero)
        {
            QsvNative.Unload(this.loader);
            this.loader = nint.Zero;
        }
    }

    private void ReleaseBuffers()
    {
        foreach (var surface in this.surfaces)
        {
            surface.Buffer.Dispose();
        }

        foreach (var buffer in this.encodedBuffers)
        {
            if (buffer != nint.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        this.surfaces = [];
        this.encodedBuffers = [];
        this.encodedBufferCapacities = [];
    }

    private static IReadOnlyList<int> GetCandidateImplementations()
    {
        var configuredAdapter = GetConfiguredAdapterOrdinal();
        if (configuredAdapter.HasValue)
        {
            return [AdapterOrdinalToImplementation(configuredAdapter.Value)];
        }

        return [QsvNative.MfxImplHardwareAny, QsvNative.MfxImplHardware, QsvNative.MfxImplHardware2, QsvNative.MfxImplHardware3, QsvNative.MfxImplHardware4];
    }

    private static int? GetConfiguredAdapterOrdinal()
    {
        var raw = Environment.GetEnvironmentVariable("TRACTUS_QSV_ADAPTER");
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = Environment.GetEnvironmentVariable("QSV_ADAPTER");
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (int.TryParse(raw, out var adapter) && adapter is >= 0 and <= 3)
        {
            return adapter;
        }

        throw new InvalidOperationException($"Invalid QuickSync adapter ordinal '{raw}'. Use 0, 1, 2, or 3.");
    }

    private static int AdapterOrdinalToImplementation(int adapter)
        => adapter switch
        {
            0 => QsvNative.MfxImplHardware,
            1 => QsvNative.MfxImplHardware2,
            2 => QsvNative.MfxImplHardware3,
            3 => QsvNative.MfxImplHardware4,
            _ => throw new ArgumentOutOfRangeException(nameof(adapter), adapter, "QuickSync adapter ordinal must be between 0 and 3.")
        };

    private static void ConvertUyvyToNv12(byte* uyvy, int uyvyStride, byte* yPlane, int yPitch, byte* uvPlane, int uvPitch, int width, int height)
    {
        for (var row = 0; row < height; row++)
        {
            var src = uyvy + (row * uyvyStride);
            var dstY = yPlane + (row * yPitch);
            for (var col = 0; col < width; col += 2)
            {
                var offset = col * 2;
                dstY[col] = src[offset + 1];
                dstY[col + 1] = src[offset + 3];
            }
        }

        for (var row = 0; row < height; row += 2)
        {
            var top = uyvy + (row * uyvyStride);
            var bottom = row + 1 < height ? uyvy + ((row + 1) * uyvyStride) : top;
            var dstUv = uvPlane + ((row / 2) * uvPitch);
            for (var col = 0; col < width; col += 2)
            {
                var offset = col * 2;
                dstUv[col] = (byte)((top[offset] + bottom[offset] + 1) / 2);
                dstUv[col + 1] = (byte)((top[offset + 2] + bottom[offset + 2] + 1) / 2);
            }
        }
    }

    private static int Align16(int value) => (value + 15) & ~15;

    private readonly record struct SurfaceBuffer(AlignedBuffer Buffer);

    private sealed class AlignedBuffer : IDisposable
    {
        private nint basePointer;

        public AlignedBuffer(int size, int alignment)
        {
            if (size <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            this.Size = size;
            this.basePointer = Marshal.AllocHGlobal(size + alignment - 1);
            var aligned = ((long)this.basePointer + alignment - 1) & ~(long)(alignment - 1);
            this.AlignedPointer = (nint)aligned;
        }

        public int Size { get; }
        public nint AlignedPointer { get; private set; }

        public void Dispose()
        {
            if (this.basePointer == nint.Zero)
            {
                return;
            }

            Marshal.FreeHGlobal(this.basePointer);
            this.basePointer = nint.Zero;
            this.AlignedPointer = nint.Zero;
        }
    }
}

internal static unsafe class QsvNative
{
    public const int MfxErrNone = 0;
    public const int MfxErrNotEnoughBuffer = -5;
    public const int MfxErrMoreData = -10;
    public const int MfxErrMoreBitstream = -18;
    public const int MfxWrnDeviceBusy = 2;

    public const int MfxImplHardware = 2;
    public const int MfxImplHardwareAny = 4;
    public const int MfxImplHardware2 = 5;
    public const int MfxImplHardware3 = 6;
    public const int MfxImplHardware4 = 7;
    public const uint MfxImplTypeHardware = 2;
    public const uint MfxAccelModeD3D11 = 768;
    public const uint MfxAccelModeVaapi = 1024;
    public const ushort MfxVariantVersion = 256;
    public const uint MfxVariantTypeU32 = 5;

    public const uint MfxCodecAvc = 541283905;
    public const uint MfxCodecHevc = 1129727304;
    public const uint MfxFourCcNv12 = 842094158;
    public const ushort MfxChromaFormatYuv420 = 1;
    public const ushort MfxPicStructProgressive = 1;
    public const ushort MfxIoPatternInSystemMemory = 2;
    public const ushort MfxRateControlCbr = 1;
    public const ushort MfxTargetUsageBestSpeed = 7;
    public const ushort MfxProfileAvcHigh = 100;
    public const ushort MfxProfileHevcMain = 1;
    public const ushort MfxFrameTypeI = 1;
    public const ushort MfxFrameTypeIdr = 128;
    public const ushort MfxCodingOptionOn = 16;
    public const uint MfxExtBuffInsertHeaders = 1163022419;

    static QsvNative()
    {
        NvidiaNativeLibraryResolver.EnsureRegistered();
    }

    public static string DescribeStatus(int status)
        => status switch
        {
            0 => "MFX_ERR_NONE",
            -1 => "MFX_ERR_UNKNOWN",
            -2 => "MFX_ERR_NULL_PTR",
            -3 => "MFX_ERR_UNSUPPORTED",
            -4 => "MFX_ERR_MEMORY_ALLOC",
            -5 => "MFX_ERR_NOT_ENOUGH_BUFFER",
            -6 => "MFX_ERR_INVALID_HANDLE",
            -8 => "MFX_ERR_NOT_INITIALIZED",
            -9 => "MFX_ERR_NOT_FOUND",
            -10 => "MFX_ERR_MORE_DATA",
            -11 => "MFX_ERR_MORE_SURFACE",
            -13 => "MFX_ERR_DEVICE_LOST",
            -14 => "MFX_ERR_INCOMPATIBLE_VIDEO_PARAM",
            -15 => "MFX_ERR_INVALID_VIDEO_PARAM",
            -17 => "MFX_ERR_DEVICE_FAILED",
            -18 => "MFX_ERR_MORE_BITSTREAM",
            1 => "MFX_WRN_IN_EXECUTION",
            2 => "MFX_WRN_DEVICE_BUSY",
            3 => "MFX_WRN_VIDEO_PARAM_CHANGED",
            4 => "MFX_WRN_PARTIAL_ACCELERATION",
            5 => "MFX_WRN_INCOMPATIBLE_VIDEO_PARAM",
            _ => $"oneVPL status {status}"
        };

    public static void ValidateLayout()
    {
        CheckSize<MfxVersion>(4);
        CheckSize<MfxFrameInfo>(68);
        CheckSize<MfxFrameData>(96);
        CheckSize<MfxFrameSurface1>(184);
        CheckSize<MfxInfoMfx>(136);
        CheckSize<MfxVideoParam>(208);
        CheckSize<MfxBitstream>(72);
        CheckSize<MfxEncodeCtrl>(56);
        CheckSize<MfxExtInsertHeaders>(28);
        CheckSize<MfxVariant>(16);
    }


    public static bool TryCreateLoaderSession(int implementation, out nint loader, out nint session, out string error)
    {
        loader = nint.Zero;
        session = nint.Zero;
        error = string.Empty;

        try
        {
            loader = Load();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            error = ex.Message;
            return false;
        }

        if (loader == nint.Zero)
        {
            error = "MFXLoad returned null.";
            return false;
        }

        var statuses = new List<string>();
        if (!AddLoaderConfigU32(loader, "mfxImplDescription.Impl", MfxImplTypeHardware, statuses))
        {
            error = string.Join(" ", statuses);
            Unload(loader);
            loader = nint.Zero;
            return false;
        }

        var accelerationMode = GetDefaultAccelerationMode();
        if (accelerationMode.HasValue)
        {
            AddLoaderConfigU32(loader, "mfxImplDescription.AccelerationMode", accelerationMode.Value, statuses);
        }

        var adapterOrdinal = ImplementationToAdapterOrdinal(implementation);
        if (adapterOrdinal.HasValue)
        {
            AddLoaderConfigU32(loader, "mfxImplDescription.VendorImplID", (uint)adapterOrdinal.Value, statuses);
        }

        AddLoaderConfigU32(loader, "mfxImplDescription.ApiVersion.Version", 2u << 16, statuses);

        var status = CreateSession(loader, 0, out session);
        if (status < MfxErrNone || session == nint.Zero)
        {
            error = statuses.Count == 0
                ? DescribeStatus(status)
                : $"{DescribeStatus(status)} ({string.Join(" ", statuses)})";
            Unload(loader);
            loader = nint.Zero;
            session = nint.Zero;
            return false;
        }

        return true;
    }

    private static bool AddLoaderConfigU32(nint loader, string propertyName, uint value, List<string> statuses)
    {
        var config = CreateConfig(loader);
        if (config == nint.Zero)
        {
            statuses.Add($"{propertyName}: MFXCreateConfig returned null");
            return false;
        }

        var variant = MfxVariant.CreateU32(value);
        var status = SetConfigFilterProperty(config, propertyName, variant);
        if (status < MfxErrNone)
        {
            statuses.Add($"{propertyName}: {DescribeStatus(status)}");
            return false;
        }

        return true;
    }

    private static uint? GetDefaultAccelerationMode()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return MfxAccelModeD3D11;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return MfxAccelModeVaapi;
        }

        return null;
    }

    private static int? ImplementationToAdapterOrdinal(int implementation)
        => implementation switch
        {
            MfxImplHardware => 0,
            MfxImplHardware2 => 1,
            MfxImplHardware3 => 2,
            MfxImplHardware4 => 3,
            _ => null
        };

    private static void CheckSize<T>(int expected) where T : struct
    {
        var actual = Marshal.SizeOf<T>();
        if (actual != expected)
        {
            throw new InvalidOperationException($"oneVPL binding layout mismatch for {typeof(T).Name}: expected {expected}, got {actual}.");
        }
    }


    [DllImport(NvidiaNativeLibraryResolver.OneVplLibrary, EntryPoint = "MFXLoad", CallingConvention = CallingConvention.Cdecl)]
    public static extern nint Load();

    [DllImport(NvidiaNativeLibraryResolver.OneVplLibrary, EntryPoint = "MFXUnload", CallingConvention = CallingConvention.Cdecl)]
    public static extern void Unload(nint loader);

    [DllImport(NvidiaNativeLibraryResolver.OneVplLibrary, EntryPoint = "MFXCreateConfig", CallingConvention = CallingConvention.Cdecl)]
    public static extern nint CreateConfig(nint loader);

    [DllImport(NvidiaNativeLibraryResolver.OneVplLibrary, EntryPoint = "MFXSetConfigFilterProperty", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int SetConfigFilterProperty(nint config, [MarshalAs(UnmanagedType.LPStr)] string name, MfxVariant value);

    [DllImport(NvidiaNativeLibraryResolver.OneVplLibrary, EntryPoint = "MFXCreateSession", CallingConvention = CallingConvention.Cdecl)]
    public static extern int CreateSession(nint loader, uint index, out nint session);

    [DllImport(NvidiaNativeLibraryResolver.OneVplLibrary, EntryPoint = "MFXInit", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Init(int implementation, ref MfxVersion version, out nint session);

    [DllImport(NvidiaNativeLibraryResolver.OneVplLibrary, EntryPoint = "MFXClose", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Close(nint session);

    [DllImport(NvidiaNativeLibraryResolver.OneVplLibrary, EntryPoint = "MFXVideoENCODE_Init", CallingConvention = CallingConvention.Cdecl)]
    public static extern int EncodeInit(nint session, ref MfxVideoParam param);

    [DllImport(NvidiaNativeLibraryResolver.OneVplLibrary, EntryPoint = "MFXVideoENCODE_Close", CallingConvention = CallingConvention.Cdecl)]
    public static extern int EncodeClose(nint session);

    [DllImport(NvidiaNativeLibraryResolver.OneVplLibrary, EntryPoint = "MFXVideoENCODE_EncodeFrameAsync", CallingConvention = CallingConvention.Cdecl)]
    public static extern int EncodeFrameAsync(nint session, MfxEncodeCtrl* ctrl, MfxFrameSurface1* surface, MfxBitstream* bitstream, out nint syncPoint);

    [DllImport(NvidiaNativeLibraryResolver.OneVplLibrary, EntryPoint = "MFXVideoCORE_SyncOperation", CallingConvention = CallingConvention.Cdecl)]
    public static extern int SyncOperation(nint session, nint syncPoint, uint wait);
}


[StructLayout(LayoutKind.Explicit, Size = 16)]
internal struct MfxVariant
{
    [FieldOffset(0)] public ushort Version;
    [FieldOffset(4)] public uint Type;
    [FieldOffset(8)] public uint U32;
    [FieldOffset(8)] public nint Ptr;

    public static MfxVariant CreateU32(uint value)
        => new()
        {
            Version = QsvNative.MfxVariantVersion,
            Type = QsvNative.MfxVariantTypeU32,
            U32 = value
        };
}

[StructLayout(LayoutKind.Explicit, Size = 4)]
internal struct MfxVersion
{
    [FieldOffset(0)] public ushort Minor;
    [FieldOffset(2)] public ushort Major;
    [FieldOffset(0)] public uint Version;
}

[StructLayout(LayoutKind.Explicit, Size = 68)]
internal struct MfxFrameInfo
{
    [FieldOffset(32)] public uint FourCC;
    [FieldOffset(36)] public ushort Width;
    [FieldOffset(38)] public ushort Height;
    [FieldOffset(40)] public ushort CropX;
    [FieldOffset(42)] public ushort CropY;
    [FieldOffset(44)] public ushort CropW;
    [FieldOffset(46)] public ushort CropH;
    [FieldOffset(48)] public uint FrameRateExtN;
    [FieldOffset(52)] public uint FrameRateExtD;
    [FieldOffset(62)] public ushort PicStruct;
    [FieldOffset(64)] public ushort ChromaFormat;
}

[StructLayout(LayoutKind.Explicit, Size = 96)]
internal struct MfxFrameData
{
    [FieldOffset(32)] public ulong TimeStamp;
    [FieldOffset(40)] public uint FrameOrder;
    [FieldOffset(44)] public ushort Locked;
    [FieldOffset(46)] public ushort Pitch;
    [FieldOffset(48)] public nint Y;
    [FieldOffset(56)] public nint UV;
    [FieldOffset(64)] public nint V;
}

[StructLayout(LayoutKind.Explicit, Size = 184)]
internal struct MfxFrameSurface1
{
    [FieldOffset(16)] public MfxFrameInfo Info;
    [FieldOffset(88)] public MfxFrameData Data;
}

[StructLayout(LayoutKind.Explicit, Size = 136)]
internal struct MfxInfoMfx
{
    [FieldOffset(28)] public ushort LowPower;
    [FieldOffset(30)] public ushort BrcParamMultiplier;
    [FieldOffset(32)] public MfxFrameInfo FrameInfo;
    [FieldOffset(100)] public uint CodecId;
    [FieldOffset(104)] public ushort CodecProfile;
    [FieldOffset(106)] public ushort CodecLevel;
    [FieldOffset(110)] public ushort TargetUsage;
    [FieldOffset(112)] public ushort GopPicSize;
    [FieldOffset(114)] public ushort GopRefDist;
    [FieldOffset(116)] public ushort GopOptFlag;
    [FieldOffset(118)] public ushort IdrInterval;
    [FieldOffset(120)] public ushort RateControlMethod;
    [FieldOffset(122)] public ushort InitialDelayInKB;
    [FieldOffset(124)] public ushort BufferSizeInKB;
    [FieldOffset(126)] public ushort TargetKbps;
    [FieldOffset(128)] public ushort MaxKbps;
    [FieldOffset(130)] public ushort NumSlice;
    [FieldOffset(132)] public ushort NumRefFrame;
    [FieldOffset(134)] public ushort EncodedOrder;
}

[StructLayout(LayoutKind.Explicit, Size = 208)]
internal struct MfxVideoParam
{
    [FieldOffset(0)] public uint AllocId;
    [FieldOffset(14)] public ushort AsyncDepth;
    [FieldOffset(16)] public MfxInfoMfx Mfx;
    [FieldOffset(184)] public ushort Protected;
    [FieldOffset(186)] public ushort IOPattern;
    [FieldOffset(192)] public nint ExtParam;
    [FieldOffset(200)] public ushort NumExtParam;
}

[StructLayout(LayoutKind.Explicit, Size = 72)]
internal struct MfxBitstream
{
    [FieldOffset(32)] public long DecodeTimeStamp;
    [FieldOffset(32)] public ulong TimeStamp;
    [FieldOffset(40)] public nint Data;
    [FieldOffset(48)] public uint DataOffset;
    [FieldOffset(52)] public uint DataLength;
    [FieldOffset(56)] public uint MaxLength;
    [FieldOffset(60)] public ushort PicStruct;
    [FieldOffset(62)] public ushort FrameType;
}

[StructLayout(LayoutKind.Explicit, Size = 56)]
internal struct MfxEncodeCtrl
{
    [FieldOffset(32)] public ushort FrameType;
    [FieldOffset(34)] public ushort NumExtParam;
    [FieldOffset(40)] public nint ExtParam;
    [FieldOffset(48)] public nint Payload;
}

[StructLayout(LayoutKind.Sequential, Size = 8)]
internal struct MfxExtBuffer
{
    public uint BufferId;
    public uint BufferSz;
}

[StructLayout(LayoutKind.Explicit, Size = 28)]
internal struct MfxExtInsertHeaders
{
    [FieldOffset(0)] public MfxExtBuffer Header;
    [FieldOffset(8)] public ushort SPS;
    [FieldOffset(10)] public ushort PPS;
}
