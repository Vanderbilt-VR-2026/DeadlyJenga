using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class GingerbreadBearing : MonoBehaviour, IGrabbable
    {
        Rigidbody body;
        GameObject holder;
        Vector3 offset,target;
        public float Weight=>body.mass;
        void Awake()=>body=GetComponent<Rigidbody>();
        public bool CanGrab(GameObject hand)=>hand && !holder && !body.isKinematic;
        public bool TryStartGrab(GameObject hand,Vector3 handPosition)
        {
            if(!CanGrab(hand))return false;
            holder=hand;offset=body.position-handPosition;target=body.position;body.WakeUp();return true;
        }
        public void Hold(Vector3 handPosition){if(holder)target=handPosition+offset;}
        void FixedUpdate()
        {
            if(holder && !body.isKinematic)
                body.AddForce(Vector3.ClampMagnitude((target-body.position)*100-body.linearVelocity*15,75),ForceMode.Acceleration);
        }
        public void Release()=>holder=null;
        void OnDisable()=>Release();
    }
}
