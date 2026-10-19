using System.Collections;
using UnityEngine;

namespace DeadlyJenga.CastleSocial
{
    public sealed class SocialMicrophone : MonoBehaviour
    {
        public bool Capturing=>clip;public string Device="";public string Status {get;private set;}="Microphone off (opt-in)";
        [Range(.001f,.1f)] public float threshold=.012f;
        readonly System.Collections.Generic.Queue<float[]> preRoll=new System.Collections.Generic.Queue<float[]>();
        SocialWorld world;AudioClip clip;int read,sequence;float lastLoud=-100;bool speaking,requesting,captureWanted;
        public void Initialize(SocialWorld owner){world=owner;}
        public void EnableCapture(bool enabled)
        {captureWanted=enabled;if(!enabled){StopCapture();return;}if(!clip&&!requesting)StartCoroutine(Request());}
        IEnumerator Request()
        {
            requesting=true;
#if UNITY_ANDROID && !UNITY_EDITOR
            if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
                float until=Time.realtimeSinceStartup+15;while(Time.realtimeSinceStartup<until&&!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))yield return null;
            }
            bool permission=UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#else
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            bool permission=Application.HasUserAuthorization(UserAuthorization.Microphone);
#endif
            if(!permission||!captureWanted){Status="Microphone permission denied or request cancelled";requesting=false;yield break;}
            if(Microphone.devices.Length==0){Status="No microphone available";requesting=false;yield break;}
            if(string.IsNullOrEmpty(Device))Device=Microphone.devices[0];
            clip=Microphone.Start(Device,true,2,16000);read=0;world.Settings.microphoneEnabled=true;Status="Live microphone · VAD · no recording retained";requesting=false;
        }
        void Update()
        {
            if(!clip)return;int position=Microphone.GetPosition(Device);if(position<0){StopCapture();Status="Microphone disconnected";return;}
            int available=(position-read+clip.samples)%clip.samples;
            for(int frame=0;frame<4&&available>=800;frame++)
            {
                var data=new float[800];clip.GetData(data,read);read=(read+800)%clip.samples;available-=800;
                double energy=0;foreach(float x in data)energy+=x*x;bool loud=System.Math.Sqrt(energy/data.Length)>threshold;
                if(loud)lastLoud=Time.unscaledTime;bool active=Time.unscaledTime-lastLoud<(world.OnDevice&&world.OnDevice.Requested?.45f:.18f);
                if(active){if(!speaking){foreach(var prior in preRoll)world.SendAudio(new SocialAudioPacket{sequence=++sequence,samples=prior},true);preRoll.Clear();}world.SendAudio(new SocialAudioPacket{sequence=++sequence,samples=data},true);speaking=true;}
                else if(speaking){world.SendAudio(new SocialAudioPacket{sequence=++sequence,end=true,samples=new float[0]},true);speaking=false;}
                if(!active){preRoll.Enqueue(data);while(preRoll.Count>3)preRoll.Dequeue();}
            }
        }
        public void StopCapture()
        {
            captureWanted=false;preRoll.Clear();
            if(clip){Microphone.End(Device);Destroy(clip);clip=null;}
            if(speaking&&world)world.SendAudio(new SocialAudioPacket{sequence=++sequence,end=true,samples=new float[0]},true);
            speaking=false;if(world&&world.Settings)world.Settings.microphoneEnabled=false;Status="Microphone off";
        }
        void OnDestroy(){StopCapture();}
    }
}
