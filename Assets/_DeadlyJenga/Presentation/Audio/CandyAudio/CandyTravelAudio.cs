using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    public sealed class CandyTravelAudio : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()
        {
            GameEvents.DepartureStarted+=Begin;
            GameEvents.Emoted+=Arrival;
        }

        static void Begin(float seconds) => CandySoundSuite.Local(CandySound.BeamFlyout);

        static void Arrival(GameObject who,string cue)
        {
            if(!who||(cue!="SpawnArrival"&&cue!="PlayerArrival"))return;
            bool local=who==Services.Session?.Player?.gameObject;
            CandyAudioEngine.Instance?.Play(CandySound.BeamArrival,who.transform.position,local?1:.5f,local:local);
        }
    }
}
