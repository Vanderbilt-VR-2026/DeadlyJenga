using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    public sealed class CandyTravelAudio:MonoBehaviour
    {
        static CandyTravelAudio current;
        bool arriving;float arrivalAt;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen(){current=null;GameEvents.DepartureStarted+=Begin;}
        static void Begin(float seconds)
        {
            if(current)return;
            var go=new GameObject("Audio - lobby flight");DontDestroyOnLoad(go);current=go.AddComponent<CandyTravelAudio>();
            CandyAudioEngine.Instance?.Play(CandySound.LobbyLaunch,Vector3.zero,1,local:true);
        }
        void OnEnable(){GameEvents.Emoted+=Emoted;GameEvents.LobbyStarted+=Lobby;}
        void Emoted(GameObject who,string emote)
        {
            if(emote=="DepartureCancelled"){Destroy(gameObject);return;}
            if(emote=="TravelArrived"){arriving=true;arrivalAt=Time.unscaledTime;}
        }
        void Lobby(GameObject who)=>Destroy(gameObject);
        void Update()
        {
            if(!arriving)return;
            if(CandyAudioEngine.Instance&&CandyAudioEngine.Instance.Play(CandySound.SpawnSlam,Vector3.zero,1.15f,local:true)||Time.unscaledTime-arrivalAt>1)Destroy(gameObject);
        }
        void OnDestroy(){GameEvents.Emoted-=Emoted;GameEvents.LobbyStarted-=Lobby;if(current==this)current=null;}
    }
}
