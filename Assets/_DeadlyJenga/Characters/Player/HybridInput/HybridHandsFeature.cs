using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif

namespace DeadlyJenga.CandyPlayers
{
#if UNITY_EDITOR
    [OpenXRFeature(UiName="Deadly Jenga simultaneous hands and controllers",Company="Deadly Jenga",Version="1.0.0",
        Desc="Keeps optical hands available while a Quest controller is held.",
        FeatureId="deadlyjenga.hybrid-hands",OpenxrExtensionStrings="XR_META_simultaneous_hands_and_controllers",
        BuildTargetGroups=new[]{BuildTargetGroup.Android,BuildTargetGroup.Standalone})]
#endif
    public sealed class HybridHandsFeature:OpenXRFeature
    {
        public static bool TrackingActive {get;private set;}
        public static string Status {get;private set;}="Simultaneous tracking not started";
        [StructLayout(LayoutKind.Sequential)] public struct ResumeInfo {public int type;public IntPtr next;}
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int GetProc(ulong instance,[MarshalAs(UnmanagedType.LPStr)]string name,out IntPtr function);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Resume(ulong session,ref ResumeInfo info);
        Resume resume;
        protected override bool OnInstanceCreate(ulong instance)
        {
            TrackingActive=false;
            if(!OpenXRRuntime.IsExtensionEnabled("XR_META_simultaneous_hands_and_controllers"))
            {Status="Runtime does not expose simultaneous hands and controllers";Debug.LogWarning(Status);return false;}
            var get=Marshal.GetDelegateForFunctionPointer<GetProc>(xrGetInstanceProcAddr);
            if(get(instance,"xrResumeSimultaneousHandsAndControllersTrackingMETA",out var address)<0||address==IntPtr.Zero)
            {Status="Simultaneous tracking entry point unavailable";return false;}
            resume=Marshal.GetDelegateForFunctionPointer<Resume>(address);return true;
        }
        protected override void OnSessionBegin(ulong session)=>StartTracking(session);
        protected override void OnSessionStateChange(int oldState,int newState)
        {if(newState==6&&!TrackingActive&&activeSession!=0)StartTracking(activeSession);}
        ulong activeSession;
        void StartTracking(ulong session)
        {
            activeSession=session;if(resume==null)return;
            var info=new ResumeInfo{type=1000532002};int result=resume(session,ref info);
            TrackingActive=result>=0;Status=TrackingActive?"Simultaneous hands and controllers active":"Simultaneous tracking failed: "+result;
            if(TrackingActive)Debug.Log(Status);else Debug.LogWarning(Status);
        }
        protected override void OnSessionEnd(ulong session){TrackingActive=false;activeSession=0;}
        protected override void OnInstanceDestroy(ulong instance){resume=null;TrackingActive=false;activeSession=0;}
    }
}
