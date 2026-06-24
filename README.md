# Tractus.Encoders

Eventual purpose: to contain managed wrappers for OpenH264, NVENC, AMD AMF, Intel QuickSync, and Apple VT.

For now, it includes NVENC, OpenH264, and AMD AMF.

Developed for primary use by [Multiview for NDI by Tractus Events](https://multiviewforndi.com/).


### NVENC Notes

Read this first - it does a great job (at a high level) explaining how to build an NVENC session: https://docs.nvidia.com/video-technologies/video-codec-sdk/12.0/nvenc-video-encoder-api-prog-guide/index.html

For reference, you can grab the header files here: https://developer.nvidia.com/nvidia-video-codec-sdk/download

Inside this library, we initialize NVENC using CUDA. The managed wrapper is intended to be cross-platform, but Linux support still needs native library loading and probe validation.

### AMD AMF Notes

The AMD AMF path lives under `AMF/` with a native C ABI shim in `native/Tractus.AmfShim`. The managed AMF wrapper returns encoded Annex B frames; application-specific packetization, such as NDI|HX compressed packet headers, belongs in the consuming application.

Build the shim from this repository root:

```powershell
cmake -S native\Tractus.AmfShim -B native\Tractus.AmfShim\build -DAMF_SDK_ROOT=C:\AMF
cmake --build native\Tractus.AmfShim\build --config Release
```
