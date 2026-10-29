using UnityEngine;

namespace DeadlyJenga.CastleSocial
{
    public enum SocialOperatingMode { Offline,LocalDevelopment,DedicatedService,ReducedResource,Full }
    [CreateAssetMenu(menuName="Deadly Jenga/Social Bot Population")]
    public sealed class SocialSettings : ScriptableObject
    {
        public SocialConfiguration configuration=SocialConfiguration.Defaults();
        public SocialOperatingMode mode=SocialOperatingMode.Offline;
        [Tooltip("Optional self-hosted speech bridge. Never a paid API by default. On Quest use the server's LAN address.")]
        public string serviceUrl="http://127.0.0.1:8765";
        [Tooltip("Explicit opt-in to microphone capture; no voice recording is retained.")]
        public bool microphoneEnabled;
        public bool showRuntimePanel=true;
        [Range(0,1)] public float botVolume=.8f,humanVolume=1;
        [Range(0,1)] public float spatialBlend;
        public SocialMicrophoneProfile microphoneEffects=new SocialMicrophoneProfile();
        public bool usePerIdentityMic=true;
        public OnDeviceConversationSettings onDevice=new OnDeviceConversationSettings();
        public void ResetDefaults(){configuration=SocialConfiguration.Defaults();}
    }
}
