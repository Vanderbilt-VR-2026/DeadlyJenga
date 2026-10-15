using UnityEngine;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyPhysicsHands;
namespace DeadlyJenga.CandyAudio
{
    [DisallowMultipleComponent]
    public sealed class CandyHandAudio : MonoBehaviour
    {
        CastleHandActor actor;CandyFlyingHandBridge bridge;CandyPhysicalHand bound;Transform soundAnchor;
        float bindAt,contactAt=-100,slide,nextTouch;Vector3 contactPosition;
        void Awake(){actor=GetComponent<CastleHandActor>();bridge=GetComponent<CandyFlyingHandBridge>();soundAnchor=new GameObject("Rubber audio position").transform;soundAnchor.SetParent(transform,false);}
        void Update()
        {
            var engine=CandyAudioEngine.Instance;if(!engine||!actor||!actor.motor||!actor.motor.Body)return;
            if(Time.time>=bindAt)
            {
                bindAt=Time.time+.5f;var hand=bridge?bridge.PhysicalHand:null;
                if(hand&&hand!=bound&&hand.bodies!=null)
                {
                    bound=hand;
                    foreach(var body in hand.bodies)if(body){var probe=body.GetComponent<CandyHandAudioContact>();if(!probe)probe=body.gameObject.AddComponent<CandyHandAudioContact>();probe.owner=this;}
                }
            }
            float speed=actor.motor.Body.linearVelocity.magnitude;
            if(speed>.5f)engine.Loop(CandySound.HandFlight,actor.motor.transform,Mathf.Clamp01((speed-.5f)/11),false,Mathf.Lerp(.8f,1.2f,Mathf.Clamp01(speed/12)));
            if(bound&&bound.IsInteractive&&Time.time-contactAt<.16f)
            {soundAnchor.position=contactPosition;engine.Loop(CandySound.RubberSlide,soundAnchor,Mathf.Clamp01(slide/2),false,Mathf.Lerp(.75f,1.25f,Mathf.Clamp01(slide/3)));slide=0;}
        }
        public void Contact(Collision collision)
        {
            if(!isActiveAndEnabled||collision.gameObject.layer!=CastleBlock.BlockLayer||collision.contactCount==0||!CandyAudioWorld.Near(transform.position,65))return;
            var cp=collision.GetContact(0);float tangent=Vector3.ProjectOnPlane(collision.relativeVelocity,cp.normal).magnitude;
            contactAt=Time.time;contactPosition=cp.point;slide=Mathf.Max(slide,tangent);
            if(tangent>.12f&&Time.time>=nextTouch)
            {CandyAudioEngine.Instance.Play(CandySound.RubberTouch,cp.point,Mathf.Clamp(tangent*.2f,.1f,.55f),GetInstanceID());nextTouch=Time.time+.45f;}
        }
    }
}
