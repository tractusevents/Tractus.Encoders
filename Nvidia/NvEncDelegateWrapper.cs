using System;
using System.Runtime.InteropServices;
using static Tractus.Encoders.Nvidia.NvencNativeApi;

namespace Tractus.Encoders.Nvidia;

/// <summary>
/// Used to wrap unmanaged delegates for the NVENC API into
/// managed methods.
/// </summary>
public class NvEncDelegateWrapper
{
    // Public  fields/properties for the NVENC functions.
    public NvEncOpenEncodeSessionEx NvEncOpenEncodeSessionEx { get; }
    public NvEncGetEncodeGuidCount NvEncGetEncodeGUIDCount { get; }
    public NvEncGetEncodeProfileGUIDCount NvEncGetEncodeProfileGUIDCount { get; }
    public NvEncGetEncodeProfileGUIDs NvEncGetEncodeProfileGUIDs { get; }
    public NvEncGetEncodeGuids NvEncGetEncodeGUIDs { get; }
    public NvEncGetInputFormatCount NvEncGetInputFormatCount { get; }
    public NvEncGetInputFormats NvEncGetInputFormats { get; }
    public NvEncGetEncodeCaps NvEncGetEncodeCaps { get; }
    public NvEncGetEncodePresetCount NvEncGetEncodePresetCount { get; }
    public NvEncGetEncodePresetGUIDs NvEncGetEncodePresetGUIDs { get; }
    //public NvEncGetEncodePresetConfig NvEncGetEncodePresetConfig { get; }
    public NvEncInitializeEncoder NvEncInitializeEncoder { get; }
    public NvEncCreateInputBuffer NvEncCreateInputBuffer { get; }
    public NvEncDestroyInputBuffer NvEncDestroyInputBuffer { get; }
    public NvEncCreateBitstreamBuffer NvEncCreateBitstreamBuffer { get; }
    public NvEncDestroyBitstreamBuffer NvEncDestroyBitstreamBuffer { get; }
    public NvEncEncodePicture NvEncEncodePicture { get; }
    public NvEncLockBitstream NvEncLockBitstream { get; }
    public NvEncUnlockBitstream NvEncUnlockBitstream { get; }
    public NvEncLockInputBuffer NvEncLockInputBuffer { get; }
    public NvEncUnlockInputBuffer NvEncUnlockInputBuffer { get; }
    //public NvEncGetEncodeStats NvEncGetEncodeStats { get; }
    //public NvEncGetSequenceParams NvEncGetSequenceParams { get; }
    //public NvEncRegisterAsyncEvent NvEncRegisterAsyncEvent { get; }
    //public NvEncUnregisterAsyncEvent NvEncUnregisterAsyncEvent { get; }
    //public NvEncMapInputResource NvEncMapInputResource { get; }
    //public NvEncUnmapInputResource NvEncUnmapInputResource { get; }
    public NvEncDestroyEncoder NvEncDestroyEncoder { get; }
    public NvEncInvalidateRefFrames NvEncInvalidateRefFrames { get; }
    //public NvEncRegisterResource NvEncRegisterResource { get; }
    //public NvEncUnregisterResource NvEncUnregisterResource { get; }
    public NvEncReconfigureEncoder NvEncReconfigureEncoder { get; }
    //public NvEncCreateMVBuffer NvEncCreateMVBuffer { get; }
    //public NvEncDestroyMVBuffer NvEncDestroyMVBuffer { get; }
    //public NvEncRunMotionEstimationOnly NvEncRunMotionEstimationOnly { get; }
    public NvEncGetLastErrorString NvEncGetLastErrorString { get; }
    //public NvEncSetIOCudaStreams NvEncSetIOCudaStreams { get; }
    public NvEncGetEncodePresetConfigEx NvEncGetEncodePresetConfigEx { get; }
    //public NvEncGetSequenceParamEx NvEncGetSequenceParamEx { get; }
    //public NvEncRestoreEncoderState NvEncRestoreEncoderState { get; }
    //public NvEncLookaheadPicture NvEncLookaheadPicture { get; }

    private NV_ENCODE_API_FUNCTION_LIST functionList;

    // Constructor that takes an NV_ENCODE_API_FUNCTION_LIST and converts the pointers to s.
    public NvEncDelegateWrapper(ref NV_ENCODE_API_FUNCTION_LIST functionList)
    {
        this.functionList = functionList;
        // Use Marshal.GetDelegateForFunctionPointer to convert each function pointer.
        this.NvEncOpenEncodeSessionEx = Marshal.GetDelegateForFunctionPointer<NvEncOpenEncodeSessionEx>(functionList.nvEncOpenEncodeSessionEx);
        this.NvEncGetEncodeGUIDCount = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodeGuidCount>(functionList.nvEncGetEncodeGUIDCount);
        this.NvEncGetEncodeProfileGUIDCount = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodeProfileGUIDCount>(functionList.nvEncGetEncodeProfileGUIDCount);
        this.NvEncGetEncodeProfileGUIDs = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodeProfileGUIDs>(functionList.nvEncGetEncodeProfileGUIDs);
        this.NvEncGetEncodeGUIDs = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodeGuids>(functionList.nvEncGetEncodeGUIDs);
        this.NvEncGetInputFormatCount = Marshal.GetDelegateForFunctionPointer<NvEncGetInputFormatCount>(functionList.nvEncGetInputFormatCount);
        this.NvEncGetInputFormats = Marshal.GetDelegateForFunctionPointer<NvEncGetInputFormats>(functionList.nvEncGetInputFormats);
        this.NvEncGetEncodeCaps = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodeCaps>(functionList.nvEncGetEncodeCaps);
        this.NvEncGetEncodePresetCount = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodePresetCount>(functionList.nvEncGetEncodePresetCount);
        this.NvEncGetEncodePresetGUIDs = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodePresetGUIDs>(functionList.nvEncGetEncodePresetGUIDs);
        //this.NvEncGetEncodePresetConfig = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodePresetConfig>(functionList.nvEncGetEncodePresetConfig);
        this.NvEncInitializeEncoder = Marshal.GetDelegateForFunctionPointer<NvEncInitializeEncoder>(functionList.nvEncInitializeEncoder);
        this.NvEncCreateInputBuffer = Marshal.GetDelegateForFunctionPointer<NvEncCreateInputBuffer>(functionList.nvEncCreateInputBuffer);
        this.NvEncDestroyInputBuffer = Marshal.GetDelegateForFunctionPointer<NvEncDestroyInputBuffer>(functionList.nvEncDestroyInputBuffer);
        this.NvEncCreateBitstreamBuffer = Marshal.GetDelegateForFunctionPointer<NvEncCreateBitstreamBuffer>(functionList.nvEncCreateBitstreamBuffer);
        this.NvEncDestroyBitstreamBuffer = Marshal.GetDelegateForFunctionPointer<NvEncDestroyBitstreamBuffer>(functionList.nvEncDestroyBitstreamBuffer);
        this.NvEncEncodePicture = Marshal.GetDelegateForFunctionPointer<NvEncEncodePicture>(functionList.nvEncEncodePicture);
        this.NvEncLockBitstream = Marshal.GetDelegateForFunctionPointer<NvEncLockBitstream>(functionList.nvEncLockBitstream);
        this.NvEncUnlockBitstream = Marshal.GetDelegateForFunctionPointer<NvEncUnlockBitstream>(functionList.nvEncUnlockBitstream);
        this.NvEncLockInputBuffer = Marshal.GetDelegateForFunctionPointer<NvEncLockInputBuffer>(functionList.nvEncLockInputBuffer);
        this.NvEncUnlockInputBuffer = Marshal.GetDelegateForFunctionPointer<NvEncUnlockInputBuffer>(functionList.nvEncUnlockInputBuffer);
        //this.NvEncGetEncodeStats = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodeStats>(functionList.nvEncGetEncodeStats);
        //this.NvEncGetSequenceParams = Marshal.GetDelegateForFunctionPointer<NvEncGetSequenceParams>(functionList.nvEncGetSequenceParams);
        //this.NvEncRegisterAsyncEvent = Marshal.GetDelegateForFunctionPointer<NvEncRegisterAsyncEvent>(functionList.nvEncRegisterAsyncEvent);
        //this.NvEncUnregisterAsyncEvent = Marshal.GetDelegateForFunctionPointer<NvEncUnregisterAsyncEvent>(functionList.nvEncUnregisterAsyncEvent);
        //this.NvEncMapInputResource = Marshal.GetDelegateForFunctionPointer<NvEncMapInputResource>(functionList.nvEncMapInputResource);
        //this.NvEncUnmapInputResource = Marshal.GetDelegateForFunctionPointer<NvEncUnmapInputResource>(functionList.nvEncUnmapInputResource);
        this.NvEncDestroyEncoder = Marshal.GetDelegateForFunctionPointer<NvEncDestroyEncoder>(functionList.nvEncDestroyEncoder);
        this.NvEncInvalidateRefFrames = Marshal.GetDelegateForFunctionPointer<NvEncInvalidateRefFrames>(functionList.nvEncInvalidateRefFrames);
        //this.NvEncRegisterResource = Marshal.GetDelegateForFunctionPointer<NvEncRegisterResource>(functionList.nvEncRegisterResource);
        //this.NvEncUnregisterResource = Marshal.GetDelegateForFunctionPointer<NvEncUnregisterResource>(functionList.nvEncUnregisterResource);
        this.NvEncReconfigureEncoder = Marshal.GetDelegateForFunctionPointer<NvEncReconfigureEncoder>(functionList.nvEncReconfigureEncoder);
        //this.NvEncCreateMVBuffer = Marshal.GetDelegateForFunctionPointer<NvEncCreateMVBuffer>(functionList.nvEncCreateMVBuffer);
        //this.NvEncDestroyMVBuffer = Marshal.GetDelegateForFunctionPointer<NvEncDestroyMVBuffer>(functionList.nvEncDestroyMVBuffer);
        //this.NvEncRunMotionEstimationOnly = Marshal.GetDelegateForFunctionPointer<NvEncRunMotionEstimationOnly>(functionList.nvEncRunMotionEstimationOnly);
        this.NvEncGetLastErrorString = Marshal.GetDelegateForFunctionPointer<NvEncGetLastErrorString>(functionList.nvEncGetLastErrorString);
        //this.NvEncSetIOCudaStreams = Marshal.GetDelegateForFunctionPointer<NvEncSetIOCudaStreams>(functionList.nvEncSetIOCudaStreams);
        this.NvEncGetEncodePresetConfigEx = Marshal.GetDelegateForFunctionPointer<NvEncGetEncodePresetConfigEx>(functionList.nvEncGetEncodePresetConfigEx);
        //this.NvEncGetSequenceParamEx = Marshal.GetDelegateForFunctionPointer<NvEncGetSequenceParamEx>(functionList.nvEncGetSequenceParamEx);
        //this.NvEncRestoreEncoderState = Marshal.GetDelegateForFunctionPointer<NvEncRestoreEncoderState>(functionList.nvEncRestoreEncoderState);
        //this.NvEncLookaheadPicture = Marshal.GetDelegateForFunctionPointer<NvEncLookaheadPicture>(functionList.nvEncLookaheadPicture);
    }
}
