# Tractus.AmfShim

Native C ABI shim for AMD AMF H.264/HEVC encoding.

This is the AMD path used by Tractus encoder consumers such as Multiview's `amfh264` and `amfhevc` NDI|HX encoders. It calls AMD AMF directly and does not use FFmpeg or GStreamer.

## Requirements

- Windows x64 or Linux x64.
- AMD GPU/APU driver with AMF runtime installed.
- AMD AMF SDK headers. `AMF_SDK_ROOT` can point either at the full GPUOpen AMF checkout or at the header folder that directly contains `core` and `components`.
- CMake 3.21+ and a C++17 compiler.

macOS is intentionally unsupported for this shim.

## Build

Set `AMF_SDK_ROOT` to the AMF header location.

Windows:

```powershell
cmake -S native\Tractus.AmfShim -B native\Tractus.AmfShim\build -DAMF_SDK_ROOT=C:\AMF
cmake --build native\Tractus.AmfShim\build --config Release
```

Linux:

```bash
cmake -S native/Tractus.AmfShim -B native/Tractus.AmfShim/build -DAMF_SDK_ROOT=$HOME/src/AMF -DCMAKE_BUILD_TYPE=Release
cmake --build native/Tractus.AmfShim/build
```

Copy `Tractus.AmfShim.dll` on Windows or `libTractus.AmfShim.so` on Linux beside the consuming application executable, or place it on the platform native library path. On Linux the AMD runtime must also be discoverable, typically as `libamfrt64.so.1`.

The CMake build also writes the shim to `native/Tractus.AmfShim/artifacts/win-x64` or `native/Tractus.AmfShim/artifacts/linux-x64`. When that artifact exists, the Multiview `.csproj` copies it to the managed build/publish output automatically.
