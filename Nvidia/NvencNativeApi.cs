using System;
using System.Runtime.InteropServices;

namespace Tractus.Encoders.Nvidia;

public static class NvencNativeApi
{
    // NvEncodeAPICreateInstance
    /**
     * \ingroup ENCODE_FUNC
     * Entry Point to the NvEncodeAPI interface.
     *
     * Creates an instance of the NvEncodeAPI interface, and populates the
     * pFunctionList with function pointers to the API routines implemented by the
     * NvEncodeAPI interface.
     *
     * \param [out] functionList
     *
     * \return
     * ::NV_ENC_SUCCESS
     * ::NV_ENC_ERR_INVALID_PTR
     */

    // TODO: Configure the DLL import resolver so we look for the NVENC dynlib
    // based on the correct name & OS. Right now this is windows only.
    //NVENCSTATUS NVENCAPI NvEncodeAPICreateInstance(NV_ENCODE_API_FUNCTION_LIST* functionList);
    [DllImport("nvEncodeAPI64.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "NvEncodeAPICreateInstance")]
    public static extern NVENCSTATUS NvEncodeAPICreateInstance(ref NV_ENCODE_API_FUNCTION_LIST functionList);

    // NvEncOpenEncodeSessionEx
    /**
     * \brief Opens an encoding session.
     *
     * Opens an encoding session and returns a pointer to the encoder interface in
     * the \p **encoder parameter. The client should start encoding process by calling
     * this API first.
     * The client must pass a pointer to IDirect3DDevice9 device or CUDA context in the \p *device parameter.
     * For the OpenGL interface, \p device must be NULL. An OpenGL context must be current when
     * calling all NvEncodeAPI functions.
     * If the creation of encoder session fails, the client must call ::NvEncDestroyEncoder API
     * before exiting.
     *
     * \param [in] openSessionExParams
     *    Pointer to a ::NV_ENC_OPEN_ENCODE_SESSION_EX_PARAMS structure.
     * \param [out] encoder
     *    Encode Session pointer to the NvEncodeAPI interface.
     * \return
     * ::NV_ENC_SUCCESS \n
     * ::NV_ENC_ERR_INVALID_PTR \n
     * ::NV_ENC_ERR_NO_ENCODE_DEVICE \n
     * ::NV_ENC_ERR_UNSUPPORTED_DEVICE \n
     * ::NV_ENC_ERR_INVALID_DEVICE \n
     * ::NV_ENC_ERR_DEVICE_NOT_EXIST \n
     * ::NV_ENC_ERR_UNSUPPORTED_PARAM \n
     * ::NV_ENC_ERR_GENERIC \n
     *
     */
    //NVENCSTATUS NVENCAPI NvEncOpenEncodeSessionEx(NV_ENC_OPEN_ENCODE_SESSION_EX_PARAMS* openSessionExParams, void** encoder);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncOpenEncodeSessionEx(
        ref NV_ENC_OPEN_ENCODE_SESSION_EX_PARAMS openSessionExParams,
        out IntPtr encoder);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncGetEncodeGuidCount(
        nint encoder,
        out uint encodeGuidCount);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncGetEncodeGuids(
        nint encoder,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]
        Guid[] guids,
        uint guidArraySize,
        ref uint guidCount);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncGetEncodePresetCount(
        nint encoder,
        Guid encodeGuid,
        out uint encodePresetGuidCount);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncGetEncodePresetGUIDs(
        nint encoder,
        Guid encodeGuid,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
        Guid[] guids,
        uint guidArraySize,
        ref uint encoderPresetGuidCount);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncGetEncodePresetConfigEx(
        nint encoder,
        Guid encodeGuid,
        Guid presetGuid,
        NV_ENC_TUNING_INFO tuningInfo,
        ref NV_ENC_PRESET_CONFIG presetConfig);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncGetEncodeProfileGUIDCount(
        nint encoder,
        Guid encodeGuid,
        out uint encodeProfileGuidCount);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncGetEncodeProfileGUIDs(
        nint encoder,
        Guid encodeGuid,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
        Guid[] profileGuids,
        uint guidArraySize,
        ref uint encoderProfileGuidCount);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncGetInputFormatCount(
        nint encoder,
        Guid encodeGuid,
        out uint inputFmtCount);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncGetInputFormats(
        nint encoder,
        Guid encodeGuid,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
        NV_ENC_BUFFER_FORMAT[] inputFmts,
        uint inputFmtArraySize,
        ref uint inputFmtCount);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncGetEncodeCaps(
        nint encoder,
        Guid encodeGuid,
        ref NV_ENC_CAPS_PARAM capsParam,
        out int capsVal);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncInitializeEncoder(
        nint encoder,
        ref NV_ENC_INITIALIZE_PARAMS createEncoderParams);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncCreateInputBuffer(
        nint encoder,
        ref NV_ENC_CREATE_INPUT_BUFFER createInputBufferParams);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncCreateBitstreamBuffer(
        nint encoder,
        ref NV_ENC_CREATE_BITSTREAM_BUFFER createBitstreamBufferParams);


    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncLockInputBuffer(
        nint encoder,
        ref NV_ENC_LOCK_INPUT_BUFFER lockInputBufferParams);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncUnlockInputBuffer(
        nint encoder,
        nint inputBuffer);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncLockBitstream(
        nint encoder,
        ref NV_ENC_LOCK_BITSTREAM lockBitstreamBufferParams);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncUnlockBitstream(
        nint encoder,
        nint bitstreamBuffer);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncEncodePicture(
        nint encoder,
        ref NV_ENC_PIC_PARAMS encodePicParams);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate nint NvEncGetLastErrorString(nint encoder);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncDestroyInputBuffer(
        nint encoder,
        nint inputBuffer);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncDestroyBitstreamBuffer(
        nint encoder,
        nint bitstreamBuffer);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncDestroyEncoder(
        nint encoder);


    // This stuff is un-tested.
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncReconfigureEncoder(
        nint encoder,
        ref NV_ENC_RECONFIGURE_PARAMS reinitEncodeParams);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate NVENCSTATUS NvEncInvalidateRefFrames(
        nint encoder,
        ulong invalidRefFrameTimeStamp);


}
