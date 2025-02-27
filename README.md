# Tractus.Encoders

Eventual purpose: to contain managed wrappers for OpenH264, NVENC, Intel QuickSync, and Apple VT.

For now, it's just NVENC with CUDA cores.

Developed for primary use by [Multiview for NDI by Tractus Events](https://multiviewforndi.com/).


### NVENC Notes

Read this first - it does a great job (at a high level) explaining how to build an NVENC session: https://docs.nvidia.com/video-technologies/video-codec-sdk/12.0/nvenc-video-encoder-api-prog-guide/index.html

For reference, you can grab the header files here: https://developer.nvidia.com/nvidia-video-codec-sdk/download

Inside this library, we initialize NVENC using CUDA, that way we're 100% cross-platform in theory. Though right now CUDA/NVENC are Windows-only since I haven't put in the full "look here for the dynlib when you load it" code.