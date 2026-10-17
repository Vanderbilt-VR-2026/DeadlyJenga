using System;
using System.IO;
using UnityEngine;

namespace DeadlyJenga.CastleSocial
{
    // Explicit consent in the profile is required. No microphone, transcript,
    // account, IP, or human identity data enters a movement demonstration.
    public sealed class SocialDemonstrationRecorder : MonoBehaviour
    {
        [Serializable] sealed class Sample {public float time;public Vector3 feet,forward;public bool grounded,vaulting,climbing;}
        SocialWorld world;StreamWriter writer;float next;int samples;
        public void Initialize(SocialWorld owner){world=owner;}
        void Update()
        {
            if(!world||!world.Config.recordDemonstrations){Close();return;}
            if(samples>=18000){Close();return;} // 30 minutes at 10Hz; explicit new session needed.
            if(Time.unscaledTime<next)return;next=Time.unscaledTime+.1f;
            if(writer==null){var folder=Path.Combine(Application.persistentDataPath,"ConsentedMovement");Directory.CreateDirectory(folder);writer=new StreamWriter(Path.Combine(folder,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")+".jsonl"));}
            var p=world.Session.Player;writer.WriteLine(JsonUtility.ToJson(new Sample{time=Time.unscaledTime,feet=p.transform.position,forward=p.transform.forward,grounded=p.Grounded,vaulting=p.Vaulting,climbing=p.Climbing}));samples++;if(samples%50==0)writer.Flush();
        }
        void Close(){writer?.Dispose();writer=null;}
        void OnDestroy(){Close();}
    }
}
