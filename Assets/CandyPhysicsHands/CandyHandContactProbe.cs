using UnityEngine;
namespace DeadlyJenga.CandyPhysicsHands
{
    // Diagnostic only; never applies a force or changes the object.
    public sealed class CandyHandContactProbe : MonoBehaviour
    {
        public string LastOther;
        public Vector3 Normal,Impulse;
        public float At;
        void OnCollisionStay(Collision c){LastOther=c.gameObject.name;Impulse=c.impulse;At=Time.fixedTime;if(c.contactCount>0)Normal=c.GetContact(0).normal;}
        void OnCollisionEnter(Collision c){OnCollisionStay(c);}
    }
}
