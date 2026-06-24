using System.Runtime.InteropServices;
using System.Text;

namespace Tractus.Encoders.AMF;

public enum AmfCodec
{
    H264 = 1,
    Hevc = 2
}

public readonly record struct AmfEncodedFrame(nint BufferPointer, int SizeInBytes, bool IsKeyFrame);

public sealed unsafe class AmfH264Encoder : AmfNativeEncoder
{
    protected override string WorkerName => "AMF H.264";
    protected override AmfCodec Codec => AmfCodec.H264;

    public AmfH264Encoder(int bufferCount)
        : base(bufferCount)
    {
    }
}

public sealed unsafe class AmfHevcEncoder : AmfNativeEncoder
{
    protected override string WorkerName => "AMF HEVC";
    protected override AmfCodec Codec => AmfCodec.Hevc;

    public AmfHevcEncoder(int bufferCount)
        : base(bufferCount)
    {
    }
}

public abstract unsafe class AmfNativeEncoder : IDisposable
{
    private readonly int bufferCount;
    private nint nativeEncoder;
    private nint[] encodedBuffers = [];
    private int[] encodedBufferCapacities = [];
    private bool disposed;

    protected AmfNativeEncoder(int bufferCount)
    {
        if (bufferCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferCount), bufferCount, "AMF encoder buffer count must be positive.");
        }

        this.bufferCount = bufferCount;
    }

    protected abstract string WorkerName { get; }
    protected abstract AmfCodec Codec { get; }

    public void Initialize(int width, int height, int bitrateBps)
        => this.Initialize(width, height, bitrateBps, 60, 1, 1);

    public void Initialize(int width, int height, int bitrateBps, int keyFrameIntervalFrames)
        => this.Initialize(width, height, bitrateBps, 60, 1, keyFrameIntervalFrames);

    public void Initialize(int width, int height, int bitrateBps, int fpsNumerator, int fpsDenominator, int keyFrameIntervalFrames)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        this.ReleaseNativeEncoder();
        this.EnsureBuffers(width, height);

        try
        {
            var status = AmfNative.CreateEncoder(
                this.Codec,
                width,
                height,
                Math.Max(1, bitrateBps),
                Math.Max(1, fpsNumerator),
                Math.Max(1, fpsDenominator),
                Math.Max(1, keyFrameIntervalFrames),
                out this.nativeEncoder);

            if (status != AmfNative.StatusOk || this.nativeEncoder == nint.Zero)
            {
                throw new InvalidOperationException($"{this.WorkerName} initialization failed: {AmfNative.DescribeFailure(status)}");
            }
        }
        catch (DllNotFoundException ex)
        {
            throw new InvalidOperationException(
                $"{this.WorkerName} native shim was not found. Build Tractus.Encoders/native/Tractus.AmfShim and place Tractus.AmfShim.dll or libTractus.AmfShim.so beside the application executable, or on the platform native library path.",
                ex);
        }
        catch (EntryPointNotFoundException ex)
        {
            throw new InvalidOperationException(
                $"{this.WorkerName} native shim is present but does not expose the expected Tractus AMF C ABI.",
                ex);
        }
    }

    public AmfEncodedFrame EncodeFrame(byte* uyvyFrame, int uyvyStride, int frameBuffer, bool forceKeyFrame)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        if (this.nativeEncoder == nint.Zero)
        {
            throw new InvalidOperationException($"{this.WorkerName} has not been initialized.");
        }

        if ((uint)frameBuffer >= (uint)this.encodedBuffers.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(frameBuffer), frameBuffer, $"Invalid {this.WorkerName} framebuffer index.");
        }

        var encodedBuffer = this.encodedBuffers[frameBuffer];
        var encodedCapacity = this.encodedBufferCapacities[frameBuffer];

        var status = AmfNative.EncodeFrame(
            this.nativeEncoder,
            uyvyFrame,
            uyvyStride,
            forceKeyFrame ? 1 : 0,
            (byte*)encodedBuffer.ToPointer(),
            encodedCapacity,
            out var encodedSize,
            out var nativeKeyFrame);

        if (status != AmfNative.StatusOk)
        {
            throw new InvalidOperationException($"{this.WorkerName} encode failed: {AmfNative.DescribeFailure(status)}");
        }

        return new AmfEncodedFrame(encodedBuffer, encodedSize, forceKeyFrame || nativeKeyFrame != 0);
    }

    private void EnsureBuffers(int width, int height)
    {
        var rawFrameBytes = (long)width * height * 2;
        if (rawFrameBytes <= 0 || rawFrameBytes > int.MaxValue)
        {
            throw new InvalidOperationException($"{this.WorkerName} frame size is not supported: {width}x{height}.");
        }

        var defaultCapacity = Math.Max((int)rawFrameBytes, 1024 * 1024);
        if (this.encodedBuffers.Length != this.bufferCount)
        {
            this.ReleaseBuffers();
            this.encodedBuffers = new nint[this.bufferCount];
            this.encodedBufferCapacities = new int[this.bufferCount];
        }

        for (var i = 0; i < this.encodedBuffers.Length; i++)
        {
            if (this.encodedBuffers[i] != nint.Zero && this.encodedBufferCapacities[i] >= defaultCapacity)
            {
                continue;
            }

            if (this.encodedBuffers[i] != nint.Zero)
            {
                Marshal.FreeHGlobal(this.encodedBuffers[i]);
            }

            this.encodedBuffers[i] = Marshal.AllocHGlobal(defaultCapacity);
            this.encodedBufferCapacities[i] = defaultCapacity;
        }
    }

    private void ReleaseNativeEncoder()
    {
        if (this.nativeEncoder == nint.Zero)
        {
            return;
        }

        AmfNative.DestroyEncoder(this.nativeEncoder);
        this.nativeEncoder = nint.Zero;
    }

    private void ReleaseBuffers()
    {
        foreach (var buffer in this.encodedBuffers)
        {
            if (buffer != nint.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        this.encodedBuffers = [];
        this.encodedBufferCapacities = [];
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
        GC.SuppressFinalize(this);
    }
}

internal static unsafe partial class AmfNative
{
    public const int StatusOk = 0;

    private const string LibraryName = "Tractus.AmfShim";

    public static int CreateEncoder(
        AmfCodec codec,
        int width,
        int height,
        int bitrateBps,
        int fpsNumerator,
        int fpsDenominator,
        int keyFrameIntervalFrames,
        out nint encoder)
    {
        return CreateEncoder(
            (int)codec,
            width,
            height,
            bitrateBps,
            fpsNumerator,
            fpsDenominator,
            keyFrameIntervalFrames,
            out encoder);
    }

    public static string DescribeFailure(int status)
    {
        var detail = GetLastError();
        return string.IsNullOrWhiteSpace(detail)
            ? $"AMF shim returned status {status}."
            : $"{detail} (AMF shim status {status}).";
    }

    private static string GetLastError()
    {
        var buffer = new StringBuilder(2048);
        var result = GetLastError(buffer, buffer.Capacity);
        return result > 0
            ? buffer.ToString()
            : string.Empty;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.ApplicationDirectory | DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    [DllImport(LibraryName, EntryPoint = "tractus_amf_create_encoder", CallingConvention = CallingConvention.Cdecl)]
    private static extern int CreateEncoder(
        int codec,
        int width,
        int height,
        int bitrateBps,
        int fpsNumerator,
        int fpsDenominator,
        int keyFrameIntervalFrames,
        out nint encoder);

    [DefaultDllImportSearchPaths(DllImportSearchPath.ApplicationDirectory | DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    [DllImport(LibraryName, EntryPoint = "tractus_amf_encode_frame", CallingConvention = CallingConvention.Cdecl)]
    public static extern int EncodeFrame(
        nint encoder,
        byte* uyvy,
        int uyvyStride,
        int forceKeyFrame,
        byte* output,
        int outputCapacity,
        out int outputSize,
        out int isKeyFrame);

    [DefaultDllImportSearchPaths(DllImportSearchPath.ApplicationDirectory | DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    [DllImport(LibraryName, EntryPoint = "tractus_amf_destroy_encoder", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DestroyEncoder(nint encoder);

    [DefaultDllImportSearchPaths(DllImportSearchPath.ApplicationDirectory | DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
    [DllImport(LibraryName, EntryPoint = "tractus_amf_get_last_error", CallingConvention = CallingConvention.Cdecl)]
    private static extern int GetLastError(StringBuilder buffer, int capacity);
}
