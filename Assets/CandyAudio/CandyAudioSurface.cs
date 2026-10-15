using UnityEngine;
namespace DeadlyJenga.CandyAudio
{
    // Optional material sound response, independent of physics material/friction.
    public sealed class CandyAudioSurface : MonoBehaviour
    {
        public CandySurface surface;
        [Range(.2f,2)] public float resonance=1;
    }
}
