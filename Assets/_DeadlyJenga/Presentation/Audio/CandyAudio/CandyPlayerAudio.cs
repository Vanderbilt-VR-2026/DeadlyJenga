using System;
using UnityEngine;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
namespace DeadlyJenga.CandyAudio
{
    [DefaultExecutionOrder(300),DisallowMultipleComponent]
    public sealed class CandyPlayerAudio : MonoBehaviour
    {
        IMovementState motor;Behaviour motorBehaviour;
        Vector3 previous;bool grounded,climbing,underwater;int vaults;
        float stepTravel,nextStep,fallSpeed,airtime,nextGrunt,previousGrip,nextGripWarning;
        readonly Vector3[] gripLast=new Vector3[2];readonly bool[] gripMoving=new bool[2];readonly float[] gripCooldown=new float[2];
        float voicePitch;
        [Range(0,1)] public float staminaBreathingVolume=.65f;
        void Awake()
        {
            motor=GetComponent<IMovementState>();motorBehaviour=motor as Behaviour;previous=transform.position;grounded=motor.Grounded;climbing=motor.Climbing;vaults=motor.VaultCount;
            voicePitch=Mathf.Lerp(.86f,1.16f,Mathf.Abs(GetInstanceID()%101)/100f);
            var engine=CandyAudioEngine.Instance;if(engine)engine.Play(CandySound.Spawn,previous,.4f,GetInstanceID(),voicePitch);
        }
        void Sound(CandySound sound,Vector3 position,float gain=1,float pitch=1)
        {CandyAudioEngine.Instance.Play(sound,position,gain,GetInstanceID(),pitch*voicePitch);}
        void LateUpdate()
        {
            var engine=CandyAudioEngine.Instance;if(!engine||!motorBehaviour)return;
            var p=transform.position;var d=p-previous;previous=p;float dt=Mathf.Max(.001f,Time.deltaTime);
            if(d.magnitude>4){stepTravel=0;fallSpeed=0;airtime=0;grounded=motor.Grounded;Sound(CandySound.Respawn,p,.45f);return;}
            bool nearby=CandyAudioWorld.Near(p,70);
            // Local breathing uses the reserved voice pool and the Character/master mixer.
            // Stop requesting on menu, role change, disable or full stamina: the pool fades out.
            if(motor.IsLocal&&!motor.InputPaused&&motorBehaviour.isActiveAndEnabled)
            {
                var breath=CandyBreathing.State(motor.Climbing,motor.Stamina);
                if(breath.HasValue)engine.Loop(breath.Value,transform,staminaBreathingVolume*Mathf.Lerp(.55f,1,1-motor.Stamina),true,voicePitch);
            }
            if(!grounded&&motor.Grounded&&airtime>.1f&&nearby)
            {Sound(fallSpeed>5?CandySound.LandHard:CandySound.LandSoft,p,Mathf.Clamp(.2f+fallSpeed/12,.2f,1));stepTravel=0;}
            if(grounded&&!motor.Grounded&&motor.VerticalSpeed>2&&!motor.Vaulting&&nearby)Sound(CandySound.Jump,p,.45f);
            if(!motor.Grounded&&!motor.Vaulting&&!motor.Climbing){fallSpeed=Mathf.Max(fallSpeed,-motor.VerticalSpeed);airtime+=dt;}
            else{fallSpeed=0;airtime=0;}
            if(motor.VerticalSpeed<-3&&!motor.Vaulting&&!motor.Climbing&&nearby)
                engine.Loop(CandySound.FallWind,transform,Mathf.Clamp01((-motor.VerticalSpeed-3)/16),motor.IsLocal,Mathf.Lerp(.8f,1.2f,Mathf.Clamp01(-motor.VerticalSpeed/25)));
            if(motor.Grounded&&!motor.Vaulting&&!motor.Climbing&&nearby)
            {
                stepTravel+=Vector3.ProjectOnPlane(d,Vector3.up).magnitude;
                if(stepTravel>Mathf.Max(.35f,motor.Scale*1.3f)&&Time.time>=nextStep)
                {
                    stepTravel=0;nextStep=Time.time+.19f;
                    var cue=CandySound.FootstepCandy;
                    if(Physics.Raycast(p+Vector3.up*.2f,Vector3.down,out var hit,.65f,1<<Layers.Surface,QueryTriggerInteraction.Ignore))
                    {if(hit.collider.TryGetComponent<CandyAudioSurface>(out var surface)&&surface.surface==CandySurface.Sand)cue=CandySound.FootstepSand;}
                    else cue=CandySound.FootstepSand;
                    Sound(cue,p,Mathf.Clamp(Vector3.ProjectOnPlane(d,Vector3.up).magnitude/dt/7,.15f,.65f),Mathf.Clamp(1/motor.Scale*.5f,.85f,1.2f));
                }
            }
            if(motor.VaultCount!=vaults)
            {vaults=motor.VaultCount;if(nearby){Sound(CandySound.VaultGrunt,p+Vector3.up*motor.Height*.8f,.65f);Sound(CandySound.ClimbPlant,p+transform.forward*.4f+Vector3.up*motor.Height,.4f);}}
            if(motor.Climbing&&nearby)
            {
                if(!climbing){Sound(CandySound.ClimbPlant,p+Vector3.up*motor.Height*.8f,.5f);nextGrunt=Time.time+.5f;}
                for(int side=0;side<2;side++)
                {
                    if(!motor.TryGetGrip(side==0,out var grip))continue;
                    float movement=Vector3.Distance(grip.position,gripLast[side])/dt;
                    if(movement>.1f)gripMoving[side]=true;
                    if(gripMoving[side]&&movement<.035f&&Time.time>=gripCooldown[side])
                    {Sound(CandySound.ClimbPlant,grip.position,.45f,side==0?1.07f:.96f);gripMoving[side]=false;gripCooldown[side]=Time.time+.25f;}
                    gripLast[side]=grip.position;
                }
                if(Time.time>=nextGrunt&&Mathf.Abs(d.y)>.001f){Sound(CandySound.ClimbGrunt,p+Vector3.up*motor.Height*.8f,.5f);nextGrunt=Time.time+1.5f+Mathf.Abs(GetInstanceID()%5)*.15f;}
                if(motor.Stamina<.22f&&previousGrip>=.22f&&Time.time>=nextGripWarning){Sound(CandySound.GripWarning,p,.5f);nextGripWarning=Time.time+3;}
            }
            if(climbing&&!motor.Climbing&&!motor.Vaulting&&nearby)Sound(CandySound.ClimbRelease,p+Vector3.up*motor.Height*.7f,.4f);
            var world=CandyAudioWorld.Instance;
            if(world)
            {
                if(!underwater&&p.y<=world.WaterHeight&&motor.VerticalSpeed<-.5f){world.Splash(p,Mathf.Clamp01(.4f-motor.VerticalSpeed*.05f),GetInstanceID(),true);underwater=true;}
                if(p.y>world.WaterHeight+.5f)underwater=false;
            }
            grounded=motor.Grounded;climbing=motor.Climbing;previousGrip=motor.Stamina;
        }
        void OnEnable()=>GameEvents.Emoted+=OnEmoted;
        void OnDisable()=>GameEvents.Emoted-=OnEmoted;
        void OnEmoted(GameObject who,string emote)
        {
            if(who!=gameObject||motor==null||!CandyAudioEngine.Instance||!CandyAudioWorld.Near(transform.position,70))return;
            if(Enum.TryParse<CandySound>(emote,out var cue))Sound(cue,transform.position+Vector3.up*motor.Height*.85f,.7f);
        }
    }
}
