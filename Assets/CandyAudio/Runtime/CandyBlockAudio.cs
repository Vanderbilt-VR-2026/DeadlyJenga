using UnityEngine;
using DeadlyJenga.CastlePrototype;
namespace DeadlyJenga.CandyAudio
{
    // Observes existing rigidbody contacts without changing their response.
    [DisallowMultipleComponent]
    public sealed class CandyBlockAudio : MonoBehaviour
    {
        Rigidbody body;BoxCollider shape;float nextSample,nextScrape,previousBottom,born,nextImpact;bool submerged;Vector3 bornUp;
        void Awake(){body=GetComponent<Rigidbody>();shape=GetComponent<BoxCollider>();born=Time.time;bornUp=transform.up;previousBottom=Bottom();}
        float Bottom()=>shape?shape.bounds.min.y:transform.position.y;
        void Update()
        {
            if(Time.time<nextSample)return;nextSample=Time.time+.15f;
            var world=CandyAudioWorld.Instance;if(!world||!body)return;
            float bottom=Bottom();
            if(previousBottom>world.WaterHeight+.08f&&bottom<=world.WaterHeight&&body.linearVelocity.y<-.8f&&Time.time-born>1)
            {world.Splash(transform.position,Mathf.Clamp01(.35f-body.linearVelocity.y*.07f),GetInstanceID());submerged=true;}
            if(bottom>world.WaterHeight+.3f)submerged=false;previousBottom=bottom;
            if(submerged||Time.time-born<2||!CandyAudioWorld.Near(transform.position))return;
            float tilt=Vector3.Angle(bornUp,transform.up);
            if(tilt>12&&tilt<78&&body.angularVelocity.magnitude>.22f)world.Lean(transform.position,Mathf.Clamp01(tilt/65));
        }
        void OnCollisionEnter(Collision collision)
        {
            var engine=CandyAudioEngine.Instance;if(!engine||!body||Time.time-born<1.5f||Time.time<nextImpact||!CandyAudioWorld.Near(transform.position)||submerged||collision.contactCount==0)return;
            // Both blocks receive the callback; only one produces a sound.
            if(collision.rigidbody&&collision.rigidbody.TryGetComponent<CandyBlockAudio>(out var other)&&other.GetInstanceID()<GetInstanceID())return;
            var contact=collision.GetContact(0);float normal=Mathf.Abs(Vector3.Dot(collision.relativeVelocity,contact.normal));
            float impulseSpeed=collision.impulse.magnitude/Mathf.Max(1,body.mass);
            float strength=Mathf.Clamp01(Mathf.Max(normal/9,impulseSpeed/15));if(strength<.045f)return;
            var cue=strength>.72f?CandySound.BlockSmash:strength>.24f?CandySound.BlockHit:CandySound.BlockTap;
            float patch=Mathf.Clamp01((collision.contactCount-1)/3f);float resonance=1;
            if(collision.collider.TryGetComponent<CandyAudioSurface>(out var surface))resonance=surface.resonance;
            float size=shape?Mathf.Pow(Mathf.Max(1,shape.bounds.size.magnitude)/6,-.18f):1;
            engine.Play(cue,contact.point,Mathf.Lerp(.15f,1,strength)*resonance,GetInstanceID(),size*Mathf.Lerp(1.08f,.9f,patch));nextImpact=Time.time+.07f;
        }
        void OnCollisionStay(Collision collision)
        {
            if(Time.time<nextScrape||!body||body.IsSleeping()||submerged||Time.time-born<1.5f||!CandyAudioWorld.Near(transform.position)||collision.contactCount==0)return;
            nextScrape=Time.time+.15f;
            if(collision.rigidbody&&collision.rigidbody.TryGetComponent<CandyBlockAudio>(out var other)&&other.GetInstanceID()<GetInstanceID())return;
            var contact=collision.GetContact(0);float speed=Vector3.ProjectOnPlane(collision.relativeVelocity,contact.normal).magnitude;
            if(speed>.25f&&collision.impulse.magnitude>1)CandyAudioEngine.Instance.Loop(CandySound.BlockScrape,transform,Mathf.Clamp01(speed/4),false,Mathf.Lerp(.8f,1.25f,Mathf.Clamp01(speed/6)));
        }
    }
}
