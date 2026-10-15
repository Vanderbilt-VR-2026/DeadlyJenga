using UnityEngine;
namespace DeadlyJenga.CandyAudio
{
    // Contact observer on existing bodies: no colliders, drives, joints or forces.
    public sealed class CandyHandAudioContact : MonoBehaviour
    {
        public CandyHandAudio owner;
        void OnCollisionEnter(Collision collision){if(owner)owner.Contact(collision);}
        void OnCollisionStay(Collision collision){if(owner)owner.Contact(collision);}
    }
}
