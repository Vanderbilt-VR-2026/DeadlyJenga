using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;
namespace DeadlyJenga.CandyAudio
{
    // Flight and rubber-contact sounds for anything with an IFlyingState.
    [DisallowMultipleComponent]
    public sealed class CandyHandAudio : MonoBehaviour
    {
        IFlyingState hand;IReadOnlyList<Rigidbody> bound;Transform soundAnchor;
        float bindAt,contactAt=-100,slide,nextTouch;Vector3 contactPosition;
        void Awake(){hand=GetComponent<IFlyingState>();soundAnchor=new GameObject("Rubber audio position").transform;soundAnchor.SetParent(transform,false);}
        void Update()
        {
            var engine=CandyAudioEngine.Instance;if(!engine||hand==null||!hand.Anchor)return;
            if(Time.time>=bindAt)
            {
                bindAt=Time.time+.5f;var bodies=hand.ContactBodies;
                if(bodies!=null&&!ReferenceEquals(bodies,bound))
                {
                    bound=bodies;
                    foreach(var body in bodies)if(body){var probe=body.GetComponent<CandyHandAudioContact>();if(!probe)probe=body.gameObject.AddComponent<CandyHandAudioContact>();probe.owner=this;}
                }
            }
            float speed=hand.Velocity.magnitude;
            if(speed>.5f)engine.Loop(CandySound.HandFlight,hand.Anchor,Mathf.Clamp01((speed-.5f)/11),false,Mathf.Lerp(.8f,1.2f,Mathf.Clamp01(speed/12)));
            if(bound!=null&&hand.Interactive&&Time.time-contactAt<.16f)
            {soundAnchor.position=contactPosition;engine.Loop(CandySound.RubberSlide,soundAnchor,Mathf.Clamp01(slide/2),false,Mathf.Lerp(.75f,1.25f,Mathf.Clamp01(slide/3)));slide=0;}
        }
        public void Contact(Collision collision)
        {
            if(!isActiveAndEnabled||collision.gameObject.layer!=Layers.Block||collision.contactCount==0||!CandyAudioWorld.Near(transform.position,65))return;
            var cp=collision.GetContact(0);float tangent=Vector3.ProjectOnPlane(collision.relativeVelocity,cp.normal).magnitude;
            contactAt=Time.time;contactPosition=cp.point;slide=Mathf.Max(slide,tangent);
            if(tangent>.12f&&Time.time>=nextTouch)
            {CandyAudioEngine.Instance.Play(CandySound.RubberTouch,cp.point,Mathf.Clamp(tangent*.2f,.1f,.55f),GetInstanceID());nextTouch=Time.time+.45f;}
        }
    }
}
