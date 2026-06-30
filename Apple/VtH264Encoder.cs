using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Tractus.Encoders.Apple;

public readonly record struct VtEncodedFrame(nint BufferPointer, int SizeInBytes, bool IsKeyFrame)
{
    public static VtEncodedFrame Empty { get; } = new(nint.Zero, 0, false);
}

public enum VtCodec
{
    H264,
    Hevc
}

public sealed unsafe class VtH264Encoder : VtVideoEncoder
{
    public VtH264Encoder(int bufferCount, bool pipelineFrames = false)
        : base(bufferCount, VtCodec.H264, pipelineFrames)
    {
    }
}

public sealed unsafe class VtHevcEncoder : VtVideoEncoder
{
    public VtHevcEncoder(int bufferCount, bool pipelineFrames = false)
        : base(bufferCount, VtCodec.Hevc, pipelineFrames)
    {
    }
}

public abstract unsafe class VtVideoEncoder : IDisposable
{
    private const int OutputCapacityFloor = 1024 * 1024;
    private const int EncodeTimeoutMs = 5000;

    private readonly VtCodec codec;
    private readonly int bufferCount;
    private readonly bool pipelineFrames;
    private readonly Queue<PendingEncode> pendingEncodes = new();
    private nint compressionSession;
    private nint forceKeyFrameProperties;
    private nint[] pixelBuffers = [];
    private nint[] encodedBuffers = [];
    private int[] encodedBufferCapacities = [];
    private int width;
    private int height;
    private int fpsNumerator = 60;
    private int fpsDenominator = 1;
    private long frameIndex;
    private bool disposed;

    protected VtVideoEncoder(int bufferCount, VtCodec codec, bool pipelineFrames)
    {
        if (bufferCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferCount), bufferCount, "VideoToolbox encoder buffer count must be positive.");
        }

        this.codec = codec;
        this.bufferCount = bufferCount;
        this.pipelineFrames = pipelineFrames && bufferCount > 1;
    }

    private string WorkerName => GetWorkerName(this.codec);

    private static string GetWorkerName(VtCodec codec)
        => codec == VtCodec.H264 ? "VideoToolbox H.264" : "VideoToolbox HEVC";

    private uint CodecType => this.codec == VtCodec.H264
        ? AppleNative.KCMVideoCodecTypeH264
        : AppleNative.KCMVideoCodecTypeHEVC;

    private string ProfileLevelConstantName => this.codec == VtCodec.H264
        ? "kVTProfileLevel_H264_Baseline_AutoLevel"
        : "kVTProfileLevel_HEVC_Main_AutoLevel";

    public void Initialize(int width, int height, int bitrateBps)
        => this.Initialize(width, height, bitrateBps, 60, 1, 1);

    public void Initialize(int width, int height, int bitrateBps, int keyFrameIntervalFrames)
        => this.Initialize(width, height, bitrateBps, 60, 1, keyFrameIntervalFrames);

    public void Initialize(int width, int height, int bitrateBps, int fpsNumerator, int fpsDenominator, int keyFrameIntervalFrames)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("VideoToolbox encoding is only available on macOS.");
        }

        if (width <= 0 || height <= 0 || (width & 1) != 0 || (height & 1) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), $"{WorkerName} requires positive even dimensions.");
        }

        this.ReleaseNativeEncoder();
        this.width = width;
        this.height = height;
        this.fpsNumerator = Math.Max(1, fpsNumerator);
        this.fpsDenominator = Math.Max(1, fpsDenominator);
        this.frameIndex = 0;

        this.EnsureBuffers(width, height);
        this.forceKeyFrameProperties = AppleNative.CreateSingleEntryDictionary(
            AppleNative.ResolveVideoToolboxConstant("kVTEncodeFrameOptionKey_ForceKeyFrame"),
            AppleNative.ResolveCoreFoundationConstant("kCFBooleanTrue"));

        var encoderSpecification = AppleNative.CreateDictionary(
            [
                AppleNative.ResolveVideoToolboxConstant("kVTVideoEncoderSpecification_EnableHardwareAcceleratedVideoEncoder"),
                AppleNative.ResolveVideoToolboxConstant("kVTVideoEncoderSpecification_RequireHardwareAcceleratedVideoEncoder")
            ],
            [
                AppleNative.ResolveCoreFoundationConstant("kCFBooleanTrue"),
                AppleNative.ResolveCoreFoundationConstant("kCFBooleanTrue")
            ]);

        try
        {
            var status = AppleNative.VTCompressionSessionCreate(
                nint.Zero,
                width,
                height,
                this.CodecType,
                encoderSpecification,
                nint.Zero,
                nint.Zero,
                AppleNative.CompressionOutputCallback,
                nint.Zero,
                out this.compressionSession);

            AppleNative.ThrowIfFailed(status, "VTCompressionSessionCreate");
        }
        finally
        {
            AppleNative.CFReleaseIfNotZero(encoderSpecification);
        }

        this.TrySetBooleanProperty("kVTCompressionPropertyKey_RealTime", true);
        this.TrySetBooleanProperty("kVTCompressionPropertyKey_AllowFrameReordering", false);
        this.TrySetNumberProperty("kVTCompressionPropertyKey_MaxFrameDelayCount", 0);
        this.TrySetNumberProperty("kVTCompressionPropertyKey_ExpectedFrameRate", Math.Max(1, (int)Math.Round(this.fpsNumerator / (double)this.fpsDenominator)));
        this.TrySetNumberProperty("kVTCompressionPropertyKey_MaxKeyFrameInterval", Math.Max(1, keyFrameIntervalFrames));
        this.TrySetNumberProperty("kVTCompressionPropertyKey_AverageBitRate", Math.Max(1, bitrateBps));
        this.TrySetProperty(
            "kVTCompressionPropertyKey_ProfileLevel",
            AppleNative.ResolveVideoToolboxConstant(this.ProfileLevelConstantName));

        AppleNative.ThrowIfFailed(
            AppleNative.VTCompressionSessionPrepareToEncodeFrames(this.compressionSession),
            "VTCompressionSessionPrepareToEncodeFrames");
    }

    public VtEncodedFrame EncodeFrame(byte* uyvyFrame, int uyvyStride, int frameBuffer, bool forceKeyFrame)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        if (this.compressionSession == nint.Zero)
        {
            throw new InvalidOperationException($"{WorkerName} has not been initialized.");
        }

        if ((uint)frameBuffer >= (uint)this.pixelBuffers.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(frameBuffer), frameBuffer, $"Invalid {WorkerName} framebuffer index.");
        }

        this.EnsureFrameBufferIsNotPending(frameBuffer);
        this.WriteUyvyAs2Vuy(uyvyFrame, uyvyStride, this.pixelBuffers[frameBuffer]);

        var pending = new PendingEncode(forceKeyFrame, this.codec, frameBuffer);
        var pendingHandle = GCHandle.Alloc(pending);
        pending.AttachHandle(pendingHandle);
        var submitted = false;

        try
        {
            var pts = new CMTime
            {
                Value = checked(this.frameIndex * this.fpsDenominator),
                Timescale = this.fpsNumerator,
                Flags = AppleNative.KCMTimeFlagsValid,
                Epoch = 0
            };

            var duration = new CMTime
            {
                Value = this.fpsDenominator,
                Timescale = this.fpsNumerator,
                Flags = AppleNative.KCMTimeFlagsValid,
                Epoch = 0
            };

            this.frameIndex++;

            var status = AppleNative.VTCompressionSessionEncodeFrame(
                this.compressionSession,
                this.pixelBuffers[frameBuffer],
                pts,
                duration,
                forceKeyFrame ? this.forceKeyFrameProperties : nint.Zero,
                GCHandle.ToIntPtr(pendingHandle),
                out _);

            AppleNative.ThrowIfFailed(status, "VTCompressionSessionEncodeFrame");
            submitted = true;

            this.pendingEncodes.Enqueue(pending);
            var pendingToReturn = this.SelectPendingEncodeToReturn(pending, forceKeyFrame);
            if (pendingToReturn is null)
            {
                return VtEncodedFrame.Empty;
            }

            return this.CopyCompletedPendingEncode(pendingToReturn, frameBuffer);
        }
        catch
        {
            if (!submitted)
            {
                pending.ReleaseHandleAndDispose();
            }

            throw;
        }
    }

    private PendingEncode? SelectPendingEncodeToReturn(PendingEncode current, bool forceKeyFrame)
    {
        if (!this.pipelineFrames || forceKeyFrame)
        {
            return this.DequeueThrough(current);
        }

        if (this.pendingEncodes.Count <= 1)
        {
            return null;
        }

        return this.pendingEncodes.Dequeue();
    }

    private PendingEncode DequeueThrough(PendingEncode target)
    {
        while (this.pendingEncodes.Count > 0)
        {
            var candidate = this.pendingEncodes.Dequeue();
            if (ReferenceEquals(candidate, target))
            {
                return candidate;
            }

            this.DiscardPendingEncode(candidate);
        }

        return target;
    }

    private void EnsureFrameBufferIsNotPending(int frameBuffer)
    {
        if (!this.pipelineFrames)
        {
            return;
        }

        while (this.pendingEncodes.Any(x => x.FrameBuffer == frameBuffer))
        {
            this.DiscardPendingEncode(this.pendingEncodes.Dequeue());
        }
    }

    private void DiscardPendingEncode(PendingEncode pending)
    {
        var completed = false;
        try
        {
            completed = pending.Completed.Wait(EncodeTimeoutMs);
            if (!completed)
            {
                pending.Abandon();
                throw new TimeoutException($"{WorkerName} did not retire a pending encoded frame within {EncodeTimeoutMs} ms.");
            }

            if (!string.IsNullOrWhiteSpace(pending.ErrorMessage))
            {
                throw new InvalidOperationException(pending.ErrorMessage);
            }
        }
        finally
        {
            if (completed)
            {
                pending.ReleaseHandleAndDispose();
            }
        }
    }

    private VtEncodedFrame CopyCompletedPendingEncode(PendingEncode pending, int outputFrameBuffer)
    {
        var completed = false;
        try
        {
            completed = pending.Completed.Wait(EncodeTimeoutMs);
            if (!completed)
            {
                pending.Abandon();
                throw new TimeoutException($"{WorkerName} did not emit an encoded frame within {EncodeTimeoutMs} ms.");
            }

            if (!string.IsNullOrWhiteSpace(pending.ErrorMessage))
            {
                throw new InvalidOperationException(pending.ErrorMessage);
            }

            var encodedBytes = pending.EncodedBytes;
            if (encodedBytes is null || encodedBytes.Length == 0)
            {
                throw new InvalidOperationException($"{WorkerName} returned an empty encoded frame.");
            }

            this.EnsureEncodedBufferCapacity(outputFrameBuffer, encodedBytes.Length);
            fixed (byte* encodedPtr = encodedBytes)
            {
                Buffer.MemoryCopy(
                    encodedPtr,
                    this.encodedBuffers[outputFrameBuffer].ToPointer(),
                    this.encodedBufferCapacities[outputFrameBuffer],
                    encodedBytes.Length);
            }

            return new VtEncodedFrame(this.encodedBuffers[outputFrameBuffer], encodedBytes.Length, pending.IsKeyFrame);
        }
        finally
        {
            if (completed)
            {
                pending.ReleaseHandleAndDispose();
            }
        }
    }

    private bool TrySetBooleanProperty(string keyName, bool value)
    {
        return this.TrySetProperty(
            keyName,
            AppleNative.ResolveCoreFoundationConstant(value ? "kCFBooleanTrue" : "kCFBooleanFalse"));
    }

    private bool TrySetNumberProperty(string keyName, int value)
    {
        var cfNumber = AppleNative.CFNumberCreateInt32(value);
        try
        {
            return this.TrySetProperty(keyName, cfNumber);
        }
        finally
        {
            AppleNative.CFReleaseIfNotZero(cfNumber);
        }
    }

    private bool TrySetProperty(string keyName, nint value)
    {
        var key = AppleNative.ResolveVideoToolboxConstant(keyName);
        var status = AppleNative.VTSessionSetProperty(this.compressionSession, key, value);
        if (status == AppleNative.KVTPropertyNotSupportedErr || status == AppleNative.KVTPropertyReadOnlyErr)
        {
            return false;
        }

        AppleNative.ThrowIfFailed(status, $"VTSessionSetProperty({keyName})");
        return true;
    }

    private void EnsureBuffers(int width, int height)
    {
        this.ReleaseBuffers();

        var rawFrameBytes = (long)width * height * 2;
        if (rawFrameBytes <= 0 || rawFrameBytes > int.MaxValue)
        {
            throw new InvalidOperationException($"{WorkerName} frame size is not supported: {width}x{height}.");
        }

        this.pixelBuffers = new nint[this.bufferCount];
        this.encodedBuffers = new nint[this.bufferCount];
        this.encodedBufferCapacities = new int[this.bufferCount];

        for (var i = 0; i < this.bufferCount; i++)
        {
            AppleNative.ThrowIfFailed(
                AppleNative.CVPixelBufferCreate(
                    nint.Zero,
                    (nuint)width,
                    (nuint)height,
                    AppleNative.KCVPixelFormatType422YpCbCr8,
                    nint.Zero,
                    out this.pixelBuffers[i]),
                "CVPixelBufferCreate");

            this.encodedBufferCapacities[i] = Math.Max((int)rawFrameBytes, OutputCapacityFloor);
            this.encodedBuffers[i] = Marshal.AllocHGlobal(this.encodedBufferCapacities[i]);
        }
    }

    private void EnsureEncodedBufferCapacity(int frameBuffer, int requiredBytes)
    {
        if (this.encodedBufferCapacities[frameBuffer] >= requiredBytes)
        {
            return;
        }

        if (this.encodedBuffers[frameBuffer] != nint.Zero)
        {
            Marshal.FreeHGlobal(this.encodedBuffers[frameBuffer]);
        }

        var nextCapacity = Math.Max(requiredBytes, this.encodedBufferCapacities[frameBuffer] * 2);
        this.encodedBuffers[frameBuffer] = Marshal.AllocHGlobal(nextCapacity);
        this.encodedBufferCapacities[frameBuffer] = nextCapacity;
    }

    private void WriteUyvyAs2Vuy(byte* uyvyFrame, int uyvyStride, nint pixelBuffer)
    {
        AppleNative.ThrowIfFailed(
            AppleNative.CVPixelBufferLockBaseAddress(pixelBuffer, 0),
            "CVPixelBufferLockBaseAddress");

        try
        {
            var destination = (byte*)AppleNative.CVPixelBufferGetBaseAddress(pixelBuffer).ToPointer();
            var destinationStride = checked((int)AppleNative.CVPixelBufferGetBytesPerRow(pixelBuffer));

            if (destination is null)
            {
                throw new InvalidOperationException($"{WorkerName} pixel buffer did not expose UYVY memory.");
            }

            CopyUyvyFrame(uyvyFrame, uyvyStride, destination, destinationStride, this.width, this.height);
        }
        finally
        {
            AppleNative.CVPixelBufferUnlockBaseAddress(pixelBuffer, 0);
        }
    }

    private static void CopyUyvyFrame(byte* source, int sourceStride, byte* destination, int destinationStride, int width, int height)
    {
        var rowBytes = checked(width * 2);
        for (var row = 0; row < height; row++)
        {
            Buffer.MemoryCopy(
                source + (row * sourceStride),
                destination + (row * destinationStride),
                destinationStride,
                rowBytes);
        }
    }

    private void ReleaseNativeEncoder()
    {
        this.AbandonPendingEncodes();

        if (this.compressionSession != nint.Zero)
        {
            AppleNative.VTCompressionSessionInvalidate(this.compressionSession);
            AppleNative.CFReleaseIfNotZero(this.compressionSession);
            this.compressionSession = nint.Zero;
        }

        AppleNative.CFReleaseIfNotZero(this.forceKeyFrameProperties);
        this.forceKeyFrameProperties = nint.Zero;
    }

    private void AbandonPendingEncodes()
    {
        while (this.pendingEncodes.Count > 0)
        {
            var pending = this.pendingEncodes.Dequeue();
            if (pending.Completed.IsSet)
            {
                pending.ReleaseHandleAndDispose();
                continue;
            }

            pending.Abandon();
        }
    }

    private void ReleaseBuffers()
    {
        foreach (var pixelBuffer in this.pixelBuffers)
        {
            AppleNative.CFReleaseIfNotZero(pixelBuffer);
        }

        foreach (var encodedBuffer in this.encodedBuffers)
        {
            if (encodedBuffer != nint.Zero)
            {
                Marshal.FreeHGlobal(encodedBuffer);
            }
        }

        this.pixelBuffers = [];
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

    private sealed class PendingEncode(bool forceKeyFrame, VtCodec codec, int frameBuffer) : IDisposable
    {
        private GCHandle handle;
        private int hasHandle;
        private int abandoned;
        private int released;

        public ManualResetEventSlim Completed { get; } = new(false);
        public bool ForceKeyFrame { get; } = forceKeyFrame;
        public VtCodec Codec { get; } = codec;
        public int FrameBuffer { get; } = frameBuffer;
        public byte[]? EncodedBytes { get; private set; }
        public bool IsKeyFrame { get; private set; }
        public string? ErrorMessage { get; private set; }

        public void AttachHandle(GCHandle handle)
        {
            this.handle = handle;
            Volatile.Write(ref this.hasHandle, 1);
        }

        public void Abandon()
        {
            Volatile.Write(ref this.abandoned, 1);
        }

        public void ReleaseIfAbandoned()
        {
            if (Volatile.Read(ref this.abandoned) != 0)
            {
                this.ReleaseHandleAndDispose();
            }
        }

        public void ReleaseHandleAndDispose()
        {
            if (Interlocked.Exchange(ref this.released, 1) != 0)
            {
                return;
            }

            if (Volatile.Read(ref this.hasHandle) != 0)
            {
                this.handle.Free();
            }

            this.Completed.Dispose();
        }

        public void Complete(byte[] encodedBytes, bool isKeyFrame)
        {
            this.EncodedBytes = encodedBytes;
            this.IsKeyFrame = isKeyFrame;
            this.Completed.Set();
        }

        public void Fail(string message)
        {
            this.ErrorMessage = message;
            this.Completed.Set();
        }

        public void Dispose()
        {
            this.ReleaseHandleAndDispose();
        }
    }

    private static class EncodedSampleConverter
    {
        private static readonly byte[] StartCode = [0x00, 0x00, 0x00, 0x01];

        public static byte[] ConvertSampleBufferToAnnexB(nint sampleBuffer, VtCodec codec, bool forceKeyFrame, out bool isKeyFrame)
        {
            isKeyFrame = false;

            var blockBuffer = AppleNative.CMSampleBufferGetDataBuffer(sampleBuffer);
            if (blockBuffer == nint.Zero)
            {
                throw new InvalidOperationException($"{GetWorkerName(codec)} sample buffer did not contain encoded data.");
            }

            var formatDescription = AppleNative.CMSampleBufferGetFormatDescription(sampleBuffer);
            if (formatDescription == nint.Zero)
            {
                throw new InvalidOperationException($"{GetWorkerName(codec)} sample buffer did not contain a format description.");
            }

            AppleNative.ThrowIfFailed(
                AppleNative.CMBlockBufferGetDataPointer(blockBuffer, 0, out var lengthAtOffset, out var totalLength, out var dataPointer),
                "CMBlockBufferGetDataPointer");

            if (totalLength > int.MaxValue)
            {
                throw new InvalidOperationException($"{GetWorkerName(codec)} encoded sample is too large: {totalLength} bytes.");
            }

            var avcc = new byte[(int)totalLength];
            if (totalLength > 0)
            {
                fixed (byte* avccPtr = avcc)
                {
                    if (dataPointer != nint.Zero && lengthAtOffset == totalLength)
                    {
                        Buffer.MemoryCopy(dataPointer.ToPointer(), avccPtr, avcc.Length, avcc.Length);
                    }
                    else
                    {
                        AppleNative.ThrowIfFailed(
                            AppleNative.CMBlockBufferCopyDataBytes(blockBuffer, 0, totalLength, avccPtr),
                            "CMBlockBufferCopyDataBytes");
                    }
                }
            }

            var parameterSets = CopyParameterSets(formatDescription, codec, out var nalLengthHeaderSize);
            nalLengthHeaderSize = nalLengthHeaderSize is >= 1 and <= 4 ? nalLengthHeaderSize : 4;

            var nals = ParseLengthPrefixedNals(avcc, codec, nalLengthHeaderSize);
            isKeyFrame = nals.Any(x => IsKeyFrameNal(codec, x.NalType));

            var includeParameterSets = parameterSets.Count > 0 && (isKeyFrame || forceKeyFrame);
            var outputSize = nals.Sum(x => StartCode.Length + x.Length)
                + (includeParameterSets ? parameterSets.Sum(x => StartCode.Length + x.Length) : 0);

            if (outputSize <= 0)
            {
                return [];
            }

            var output = new byte[outputSize];
            var offset = 0;

            if (includeParameterSets)
            {
                foreach (var parameterSet in parameterSets)
                {
                    WriteStartCode(output, ref offset);
                    parameterSet.CopyTo(output.AsSpan(offset));
                    offset += parameterSet.Length;
                }
            }

            foreach (var nal in nals)
            {
                WriteStartCode(output, ref offset);
                avcc.AsSpan(nal.Offset, nal.Length).CopyTo(output.AsSpan(offset));
                offset += nal.Length;
            }

            return output;
        }

        private static List<byte[]> CopyParameterSets(nint formatDescription, VtCodec codec, out int nalLengthHeaderSize)
        {
            nalLengthHeaderSize = 4;
            var parameterSets = new List<byte[]>();

            var status = GetParameterSetAtIndex(
                codec,
                formatDescription,
                0,
                out var parameterSetPointer,
                out var parameterSetSize,
                out var parameterSetCount,
                out nalLengthHeaderSize);

            if (status != AppleNative.NoErr || parameterSetPointer == nint.Zero || parameterSetSize == 0)
            {
                return parameterSets;
            }

            parameterSets.Add(CopyParameterSet(codec, parameterSetPointer, parameterSetSize));

            for (nuint i = 1; i < parameterSetCount; i++)
            {
                status = GetParameterSetAtIndex(
                    codec,
                    formatDescription,
                    i,
                    out parameterSetPointer,
                    out parameterSetSize,
                    out _,
                    out _);

                if (status != AppleNative.NoErr || parameterSetPointer == nint.Zero || parameterSetSize == 0)
                {
                    continue;
                }

                parameterSets.Add(CopyParameterSet(codec, parameterSetPointer, parameterSetSize));
            }

            return parameterSets;
        }

        private static int GetParameterSetAtIndex(
            VtCodec codec,
            nint formatDescription,
            nuint parameterSetIndex,
            out nint parameterSetPointer,
            out nuint parameterSetSize,
            out nuint parameterSetCount,
            out int nalLengthHeaderSize)
        {
            return codec == VtCodec.H264
                ? AppleNative.CMVideoFormatDescriptionGetH264ParameterSetAtIndex(
                    formatDescription,
                    parameterSetIndex,
                    out parameterSetPointer,
                    out parameterSetSize,
                    out parameterSetCount,
                    out nalLengthHeaderSize)
                : AppleNative.CMVideoFormatDescriptionGetHEVCParameterSetAtIndex(
                    formatDescription,
                    parameterSetIndex,
                    out parameterSetPointer,
                    out parameterSetSize,
                    out parameterSetCount,
                    out nalLengthHeaderSize);
        }

        private static byte[] CopyParameterSet(VtCodec codec, nint parameterSetPointer, nuint parameterSetSize)
        {
            if (parameterSetSize > int.MaxValue)
            {
                throw new InvalidOperationException($"{GetWorkerName(codec)} parameter set is too large: {parameterSetSize} bytes.");
            }

            var parameterSet = new byte[(int)parameterSetSize];
            Marshal.Copy(parameterSetPointer, parameterSet, 0, parameterSet.Length);
            return parameterSet;
        }

        private static List<LengthPrefixedNal> ParseLengthPrefixedNals(byte[] avcc, VtCodec codec, int nalLengthHeaderSize)
        {
            var nals = new List<LengthPrefixedNal>();
            var offset = 0;

            while (offset + nalLengthHeaderSize <= avcc.Length)
            {
                var nalLength = ReadBigEndianNalLength(avcc.AsSpan(offset, nalLengthHeaderSize));
                offset += nalLengthHeaderSize;

                if (nalLength <= 0 || offset + nalLength > avcc.Length)
                {
                    break;
                }

                nals.Add(new LengthPrefixedNal(offset, nalLength, GetNalType(codec, avcc[offset])));
                offset += nalLength;
            }

            return nals;
        }

        private static byte GetNalType(VtCodec codec, byte firstHeaderByte)
            => codec == VtCodec.H264
                ? (byte)(firstHeaderByte & 0x1F)
                : (byte)((firstHeaderByte >> 1) & 0x3F);

        private static bool IsKeyFrameNal(VtCodec codec, byte nalType)
            => codec == VtCodec.H264
                ? nalType == 5
                : nalType is 19 or 20 or 21;

        private static int ReadBigEndianNalLength(ReadOnlySpan<byte> bytes)
        {
            return bytes.Length switch
            {
                1 => bytes[0],
                2 => BinaryPrimitives.ReadUInt16BigEndian(bytes),
                3 => (bytes[0] << 16) | (bytes[1] << 8) | bytes[2],
                4 => checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes)),
                _ => throw new InvalidOperationException($"Unsupported H.264 NAL length header size: {bytes.Length}.")
            };
        }

        private static void WriteStartCode(byte[] output, ref int offset)
        {
            StartCode.CopyTo(output.AsSpan(offset));
            offset += StartCode.Length;
        }

        private readonly record struct LengthPrefixedNal(int Offset, int Length, byte NalType);
    }

    private static class AppleNative
    {
        public const int NoErr = 0;
        public const uint KCMTimeFlagsValid = 1;
        public const uint KCMVideoCodecTypeH264 = 0x61766331;
        public const uint KCMVideoCodecTypeHEVC = 0x68766331;
        public const uint KCVPixelFormatType422YpCbCr8 = 0x32767579;
        public const int KVTPropertyNotSupportedErr = -12900;
        public const int KVTPropertyReadOnlyErr = -12901;

        private const string CoreFoundationLibrary = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        private const string CoreVideoLibrary = "/System/Library/Frameworks/CoreVideo.framework/CoreVideo";
        private const string CoreMediaLibrary = "/System/Library/Frameworks/CoreMedia.framework/CoreMedia";
        private const string VideoToolboxLibrary = "/System/Library/Frameworks/VideoToolbox.framework/VideoToolbox";
        private const int KCFNumberSInt32Type = 3;

        private static readonly nint CoreFoundationHandle = LoadFramework(CoreFoundationLibrary, "CoreFoundation");
        private static readonly nint VideoToolboxHandle = LoadFramework(VideoToolboxLibrary, "VideoToolbox");
        private static readonly Dictionary<string, nint> Constants = new(StringComparer.Ordinal);

        public static readonly VTCompressionOutputCallback CompressionOutputCallback = OnCompressionOutput;

        public static nint ResolveCoreFoundationConstant(string name)
            => ResolveExportedPointer(CoreFoundationHandle, name);

        public static nint ResolveVideoToolboxConstant(string name)
            => ResolveExportedPointer(VideoToolboxHandle, name);

        public static nint CreateSingleEntryDictionary(nint key, nint value)
        {
            nint* keys = stackalloc nint[1];
            nint* values = stackalloc nint[1];
            keys[0] = key;
            values[0] = value;
            return CFDictionaryCreate(
                nint.Zero,
                keys,
                values,
                1,
                nint.Zero,
                nint.Zero);
        }

        public static nint CreateDictionary(ReadOnlySpan<nint> keys, ReadOnlySpan<nint> values)
        {
            if (keys.Length != values.Length)
            {
                throw new ArgumentException("CoreFoundation dictionary key and value counts must match.", nameof(values));
            }

            nint* keyPointers = stackalloc nint[keys.Length];
            nint* valuePointers = stackalloc nint[values.Length];
            for (var i = 0; i < keys.Length; i++)
            {
                keyPointers[i] = keys[i];
                valuePointers[i] = values[i];
            }

            return CFDictionaryCreate(
                nint.Zero,
                keyPointers,
                valuePointers,
                keys.Length,
                nint.Zero,
                nint.Zero);
        }

        public static nint CFNumberCreateInt32(int value)
        {
            return CFNumberCreate(nint.Zero, KCFNumberSInt32Type, &value);
        }

        public static void ThrowIfFailed(int status, string operation)
        {
            if (status == NoErr)
            {
                return;
            }

            throw new InvalidOperationException($"{operation} failed with OSStatus {status}.");
        }

        public static void CFReleaseIfNotZero(nint value)
        {
            if (value != nint.Zero)
            {
                CFRelease(value);
            }
        }

        private static nint LoadFramework(string frameworkPath, string fallbackName)
        {
            if (NativeLibrary.TryLoad(frameworkPath, out var handle)
                || NativeLibrary.TryLoad(fallbackName, out handle))
            {
                return handle;
            }

            throw new DllNotFoundException($"Could not load macOS framework '{frameworkPath}'.");
        }

        private static nint ResolveExportedPointer(nint frameworkHandle, string name)
        {
            lock (Constants)
            {
                if (Constants.TryGetValue(name, out var cached))
                {
                    return cached;
                }

                var symbol = NativeLibrary.GetExport(frameworkHandle, name);
                var value = Marshal.ReadIntPtr(symbol);
                if (value == nint.Zero)
                {
                    throw new EntryPointNotFoundException($"macOS framework constant '{name}' resolved to null.");
                }

                Constants[name] = value;
                return value;
            }
        }

        private static void OnCompressionOutput(
            nint outputCallbackRefCon,
            nint sourceFrameRefCon,
            int status,
            uint infoFlags,
            nint sampleBuffer)
        {
            if (sourceFrameRefCon == nint.Zero)
            {
                return;
            }

            var pending = (PendingEncode?)GCHandle.FromIntPtr(sourceFrameRefCon).Target;
            if (pending is null)
            {
                return;
            }

            try
            {
                if (status != NoErr)
                {
                    pending.Fail($"{GetWorkerName(pending.Codec)} output callback failed with OSStatus {status}.");
                    return;
                }

                if (sampleBuffer == nint.Zero)
                {
                    pending.Fail($"{GetWorkerName(pending.Codec)} output callback did not include a sample buffer.");
                    return;
                }

                var bytes = EncodedSampleConverter.ConvertSampleBufferToAnnexB(
                    sampleBuffer,
                    pending.Codec,
                    pending.ForceKeyFrame,
                    out var isKeyFrame);
                pending.Complete(bytes, isKeyFrame);
            }
            catch (Exception ex)
            {
                try
                {
                    pending.Fail(ex.Message);
                }
                catch
                {
                }
            }
            finally
            {
                pending.ReleaseIfAbandoned();
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void VTCompressionOutputCallback(
            nint outputCallbackRefCon,
            nint sourceFrameRefCon,
            int status,
            uint infoFlags,
            nint sampleBuffer);

        [DllImport(CoreFoundationLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void CFRelease(nint cf);

        [DllImport(CoreFoundationLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern nint CFNumberCreate(nint allocator, int theType, int* valuePtr);

        [DllImport(CoreFoundationLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern nint CFDictionaryCreate(
            nint allocator,
            nint* keys,
            nint* values,
            nint numValues,
            nint keyCallBacks,
            nint valueCallBacks);

        [DllImport(CoreVideoLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern int CVPixelBufferCreate(
            nint allocator,
            nuint width,
            nuint height,
            uint pixelFormatType,
            nint pixelBufferAttributes,
            out nint pixelBufferOut);

        [DllImport(CoreVideoLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern int CVPixelBufferLockBaseAddress(nint pixelBuffer, ulong lockFlags);

        [DllImport(CoreVideoLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern int CVPixelBufferUnlockBaseAddress(nint pixelBuffer, ulong unlockFlags);

        [DllImport(CoreVideoLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern nint CVPixelBufferGetBaseAddress(nint pixelBuffer);

        [DllImport(CoreVideoLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern nuint CVPixelBufferGetBytesPerRow(nint pixelBuffer);

        [DllImport(CoreMediaLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern nint CMSampleBufferGetDataBuffer(nint sampleBuffer);

        [DllImport(CoreMediaLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern nint CMSampleBufferGetFormatDescription(nint sampleBuffer);

        [DllImport(CoreMediaLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern int CMBlockBufferGetDataPointer(
            nint blockBuffer,
            nuint offset,
            out nuint lengthAtOffset,
            out nuint totalLength,
            out nint dataPointer);

        [DllImport(CoreMediaLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern int CMBlockBufferCopyDataBytes(
            nint blockBuffer,
            nuint offsetToData,
            nuint dataLength,
            byte* destination);

        [DllImport(CoreMediaLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern int CMVideoFormatDescriptionGetH264ParameterSetAtIndex(
            nint videoDescription,
            nuint parameterSetIndex,
            out nint parameterSetPointer,
            out nuint parameterSetSize,
            out nuint parameterSetCount,
            out int nalUnitHeaderLength);

        [DllImport(CoreMediaLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern int CMVideoFormatDescriptionGetHEVCParameterSetAtIndex(
            nint videoDescription,
            nuint parameterSetIndex,
            out nint parameterSetPointer,
            out nuint parameterSetSize,
            out nuint parameterSetCount,
            out int nalUnitHeaderLength);

        [DllImport(VideoToolboxLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern int VTCompressionSessionCreate(
            nint allocator,
            int width,
            int height,
            uint codecType,
            nint encoderSpecification,
            nint sourceImageBufferAttributes,
            nint compressedDataAllocator,
            VTCompressionOutputCallback outputCallback,
            nint outputCallbackRefCon,
            out nint compressionSessionOut);

        [DllImport(VideoToolboxLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern void VTCompressionSessionInvalidate(nint session);

        [DllImport(VideoToolboxLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern int VTCompressionSessionPrepareToEncodeFrames(nint session);

        [DllImport(VideoToolboxLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern int VTCompressionSessionEncodeFrame(
            nint session,
            nint imageBuffer,
            CMTime presentationTimeStamp,
            CMTime duration,
            nint frameProperties,
            nint sourceFrameRefcon,
            out uint infoFlagsOut);

        [DllImport(VideoToolboxLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern int VTSessionSetProperty(nint session, nint key, nint value);
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct CMTime
{
    public long Value;
    public int Timescale;
    public uint Flags;
    public long Epoch;
}
