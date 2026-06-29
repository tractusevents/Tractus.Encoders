using System;
using System.Runtime.InteropServices;

namespace Tractus.Encoders.Nvidia;

public static class CudaNative
{
    static CudaNative()
    {
        NvidiaNativeLibraryResolver.EnsureRegistered();
    }

    [DllImport(NvidiaNativeLibraryResolver.CudaDriverLibrary)]
    public static extern CUresult cuInit(uint Flags);

    [DllImport(NvidiaNativeLibraryResolver.CudaDriverLibrary)]
    public static extern CUresult cuDeviceGetCount(out int count);

    [DllImport(NvidiaNativeLibraryResolver.CudaDriverLibrary)]
    public static extern CUresult cuDeviceGet(out int device, int ordinal);

    [DllImport(NvidiaNativeLibraryResolver.CudaDriverLibrary)]
    public static extern CUresult cuDeviceGetName(
        [Out] byte[] name,
        int len,
        int dev);

    [DllImport(NvidiaNativeLibraryResolver.CudaDriverLibrary)]
    public static extern CUresult cuCtxCreate(out nint pCtx, uint flags, int device);

    [DllImport(NvidiaNativeLibraryResolver.CudaDriverLibrary)]
    public static extern CUresult cuCtxDestroy(nint pCtx);

    public static string GetDeviceName(int device)
    {
        var bytes = new byte[256];
        var result = cuDeviceGetName(bytes, bytes.Length, device);
        if (result != CUresult.CUDA_SUCCESS)
        {
            return $"CUDA device {device}";
        }

        var terminator = Array.IndexOf(bytes, (byte)0);
        var length = terminator >= 0 ? terminator : bytes.Length;
        return System.Text.Encoding.UTF8.GetString(bytes, 0, length);
    }
}
