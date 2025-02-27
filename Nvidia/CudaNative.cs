using System;
using System.Runtime.InteropServices;

namespace Tractus.Encoders.Nvidia;

public static class CudaNative
{
    private const string CudaDll = "nvcuda.dll";

    [DllImport(CudaDll)]
    public static extern CUresult cuInit(uint Flags);

    [DllImport(CudaDll)]
    public static extern CUresult cuDeviceGet(out int device, int ordinal);

    [DllImport(CudaDll)]
    public static extern CUresult cuCtxCreate(out nint pCtx, uint flags, int device);

    [DllImport(CudaDll)]
    public static extern CUresult cuCtxDestroy(nint pCtx);
}
