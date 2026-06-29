using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Tractus.Encoders.Nvidia;

internal static class NvidiaNativeLibraryResolver
{
    public const string CudaDriverLibrary = "TractusCudaDriver";
    public const string NvEncodeApiLibrary = "TractusNvEncodeApi";
    public const string OneVplLibrary = "TractusOneVpl";

    private static int registered;

    public static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref registered, 1) == 1)
        {
            return;
        }

        NativeLibrary.SetDllImportResolver(typeof(NvidiaNativeLibraryResolver).Assembly, Resolve);
    }

    private static nint Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName == CudaDriverLibrary)
        {
            return LoadFirstAvailable(assembly, searchPath, GetCudaDriverCandidates());
        }

        if (libraryName == NvEncodeApiLibrary)
        {
            return LoadFirstAvailable(assembly, searchPath, GetNvEncodeApiCandidates());
        }

        if (libraryName == OneVplLibrary)
        {
            return LoadFirstAvailable(assembly, searchPath, GetOneVplCandidates());
        }

        return nint.Zero;
    }

    private static string[] GetCudaDriverCandidates()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ["nvcuda.dll"];
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return ["libcuda.so.1", "libcuda.so"];
        }

        return ["libcuda.so.1", "libcuda.so", "nvcuda.dll"];
    }

    private static string[] GetNvEncodeApiCandidates()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ["nvEncodeAPI64.dll"];
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return ["libnvidia-encode.so.1", "libnvidia-encode.so"];
        }

        return ["libnvidia-encode.so.1", "libnvidia-encode.so", "nvEncodeAPI64.dll"];
    }

    private static string[] GetOneVplCandidates()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ["libvpl.dll", "vpl.dll", "libmfx.dll"];
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return ["libvpl.so.2", "libvpl.so", "libmfx.so.1", "libmfx.so"];
        }

        return ["libvpl.so.2", "libvpl.so", "libmfx.so.1", "libmfx.so", "libvpl.dll", "vpl.dll", "libmfx.dll"];
    }

    private static nint LoadFirstAvailable(Assembly assembly, DllImportSearchPath? searchPath, IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out var handle)
                || NativeLibrary.TryLoad(candidate, out handle))
            {
                return handle;
            }
        }

        return nint.Zero;
    }
}
