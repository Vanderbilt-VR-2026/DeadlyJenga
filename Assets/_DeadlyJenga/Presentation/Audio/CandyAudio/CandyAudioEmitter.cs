using UnityEngine;
namespace DeadlyJenga.CandyAudio
{
    // Reusable UnityEvent hook for future pickups, UI, gadgets or game rules.
    public sealed class CandyAudioEmitter : MonoBehaviour
    {
        public CandySound cue=CandySound.UiClick;
        [Range(0,1)] public float intensity=1;
        public bool local;
        public void Play(){Play(intensity);}
        public void Play(float strength)
        {var engine=CandyAudioEngine.Instance;if(engine)engine.Play(cue,transform.position,strength,GetInstanceID(),1,local);}
    }
}
