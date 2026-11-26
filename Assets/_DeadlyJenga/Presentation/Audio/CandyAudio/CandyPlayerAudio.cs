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
        bool staminaObserved,staminaEmpty;
        bool triggerWasHeld;EquipmentKind triggerKind;float nextGripBuzz;IDamageable damageable;
        // Approved climbing grab (25 Nov 2026), with the older plant sound as a fallback.
        void Grab(Vector3 position,float gain,float pitch=1)
        {if(!CandyWeaponSuite.PlayExtra("climb","grab",position,GetInstanceID()))Sound(CandySound.ClimbPlant,position,gain,pitch);}
        // Approved powerup sounds (25 Nov 2026). The effect assets are the same ones the vision tint reads.
        IAffectable effects;EffectType speedBoost,jumpBoost;
        bool Boosted(EffectType type)=>type&&effects!=null&&effects.Has(type);
        IPlayerView playerView;
        readonly Vector3[] gripLast=new Vector3[2];readonly bool[] gripMoving=new bool[2];readonly float[] gripCooldown=new float[2];
        float voicePitch;
        [Range(0,1)] public float staminaBreathingVolume=.65f;
        void Awake()
        {
            motor=GetComponent<IMovementState>();motorBehaviour=motor as Behaviour;previous=transform.position;grounded=motor.Grounded;climbing=motor.Climbing;vaults=motor.VaultCount;
            playerView=GetComponent<IPlayerView>();
            effects=GetComponent<IAffectable>();
            speedBoost=Resources.Load<EffectType>("Powerups/Speed2x");jumpBoost=Resources.Load<EffectType>("Powerups/Jump3x");
            voicePitch=Mathf.Lerp(.86f,1.16f,Mathf.Abs(GetInstanceID()%101)/100f);
            var engine=CandyAudioEngine.Instance;if(engine&&!CandyJaguarAudio.IsJaguar(this))engine.Play(CandySound.Spawn,previous,.4f,GetInstanceID(),voicePitch);
        }
        void Sound(CandySound sound,Vector3 position,float gain=1,float pitch=1)
        {CandyAudioEngine.Instance.Play(sound,position,gain,GetInstanceID(),pitch*voicePitch);}
        void LateUpdate()
        {
            var engine=CandyAudioEngine.Instance;if(!engine||!motorBehaviour||CandyJaguarAudio.IsJaguar(this))return;
            var p=transform.position;var d=p-previous;previous=p;float dt=Mathf.Max(.001f,Time.deltaTime);
            if(d.magnitude>4){stepTravel=0;fallSpeed=0;airtime=0;grounded=motor.Grounded;Sound(CandySound.Respawn,p,.45f);return;}
            bool nearby=CandyAudioWorld.Near(p,70);
            // VR controller buzz for the local player only (desktop has no haptic device, so this is a no-op there).
            bool feel=motor.IsLocal&&playerView!=null&&playerView.ControlledHere&&playerView.IsVR;
            var actor=GetComponent<IModeActor>();var weapon=actor!=null?(MatchRuntime.Current as IEquipmentRuntime)?.EquipmentFor(actor.ActorId):null;
            bool held=nearby&&actor?.Alive==true&&weapon?.triggerHeld==true&&weapon.drawn;
            var heldKind=weapon?.kind??EquipmentKind.None;
            bool suite=CandyWeaponSuite.Held(transform,heldKind,held,triggerWasHeld&&heldKind==triggerKind,motor.IsLocal);
            if(triggerWasHeld&&heldKind!=triggerKind)CandyWeaponSuite.Held(transform,triggerKind,false,true,motor.IsLocal);
            triggerWasHeld=held;triggerKind=heldKind;
            if(held&&!suite&&(heldKind==EquipmentKind.NeedleDrill||heldKind==EquipmentKind.Chainsaw))
                engine.Loop(CandySound.WeaponTransform,transform,.35f,motor.IsLocal,Mathf.Lerp(.65f,1.4f,weapon.charge));
            // Local breathing uses the reserved voice pool and the Character/master mixer.
            // Stop requesting on menu, role change, disable or full stamina: the pool fades out.
            if(motor.IsLocal&&!motor.InputPaused&&motorBehaviour.isActiveAndEnabled&&CandyAudioEngine.WaveImmersion<.3f)
            {
                var breath=CandyBreathing.State(motor.Climbing,motor.Stamina);
                if(breath.HasValue)engine.Loop(breath.Value,transform,staminaBreathingVolume*Mathf.Lerp(.55f,1,1-motor.Stamina),true,voicePitch);
            }
            ObserveStamina();
            if(!grounded&&motor.Grounded&&airtime>.1f&&nearby)
            {
                // Approved landing weights (25 Nov 2026): soft pat, hard thump, heavy drop with candy debris.
                string landing=fallSpeed>12?"land-heavy":fallSpeed>5?"land-hard":"land-soft";
                if(!CandyWeaponSuite.PlayExtra("move",landing,p,GetInstanceID()))
                    Sound(fallSpeed>5?CandySound.LandHard:CandySound.LandSoft,p,Mathf.Clamp(.2f+fallSpeed/12,.2f,1));
                stepTravel=0;
                if(feel)CandyFeedback.Pulse(Mathf.Clamp(.12f+fallSpeed/18,.12f,.8f),Mathf.Lerp(.05f,.16f,Mathf.Clamp01(fallSpeed/14)));
                if(Boosted(jumpBoost)&&airtime>.35f)CandyWeaponSuite.PlayExtra("jump-powerup","land",p,GetInstanceID());
            }
            if(grounded&&!motor.Grounded&&motor.VerticalSpeed>2&&!motor.Vaulting&&nearby)
            {
                Sound(CandySound.Jump,p,.45f);
                if(feel)CandyFeedback.Pulse(.12f,.04f);
                if(Vector3.ProjectOnPlane(d,Vector3.up).magnitude/dt>6f)CandyWeaponSuite.PlayExtra("move","whoosh",p,GetInstanceID());
                if(Boosted(jumpBoost))CandyWeaponSuite.PlayExtra("jump-powerup","jump",p,GetInstanceID());
            }
            // Speed boost: rushing wind under movement, louder and higher as the player runs faster.
            float ground=Vector3.ProjectOnPlane(d,Vector3.up).magnitude/dt;
            if(nearby&&Boosted(speedBoost)&&ground>.8f&&!motor.Climbing)
                CandyWeaponSuite.Loop("speed-powerup","run-loop",transform,Mathf.Clamp01(ground/9),motor.IsLocal,Mathf.Lerp(.85f,1.2f,Mathf.Clamp01(ground/12)));
            if(!motor.Grounded&&!motor.Vaulting&&!motor.Climbing){fallSpeed=Mathf.Max(fallSpeed,-motor.VerticalSpeed);airtime+=dt;}
            else{fallSpeed=0;airtime=0;}
            if(motor.VerticalSpeed<-3&&!motor.Vaulting&&!motor.Climbing&&nearby)
                CandyWeaponSuite.Loop("move","fall-wind-loop",transform,Mathf.Clamp01((-motor.VerticalSpeed-3)/16),motor.IsLocal,Mathf.Lerp(.8f,1.2f,Mathf.Clamp01(-motor.VerticalSpeed/25)));
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
                    if(Boosted(speedBoost))CandyWeaponSuite.PlayExtra("speed-powerup","footstep",p,GetInstanceID());
                    if(ground>4.5f)
                    {
                        CandyWeaponSuite.PlayExtra("move","sprint-step",p,GetInstanceID());
                        if(feel)CandyFeedback.Pulse(.07f,.03f);
                    }
                }
            }
            if(motor.VaultCount!=vaults)
            {
                vaults=motor.VaultCount;
                if(nearby)
                {
                    Sound(CandySound.VaultGrunt,p+Vector3.up*motor.Height*.8f,.65f);
                    CandyWeaponSuite.PlayExtra("move","whoosh",p,GetInstanceID());
                    Grab(p+transform.forward*.4f+Vector3.up*motor.Height,.4f);
                }
            }
            if(motor.Climbing&&nearby)
            {
                if(!climbing){Grab(p+Vector3.up*motor.Height*.8f,.5f);nextGrunt=Time.time+.5f;}
                for(int side=0;side<2;side++)
                {
                    if(!motor.TryGetGrip(side==0,out var grip))continue;
                    float movement=Vector3.Distance(grip.position,gripLast[side])/dt;
                    if(movement>.1f)gripMoving[side]=true;
                    if(gripMoving[side]&&movement<.035f&&Time.time>=gripCooldown[side])
                    {Grab(grip.position,.45f,side==0?1.07f:.96f);gripMoving[side]=false;gripCooldown[side]=Time.time+.25f;if(feel)CandyFeedback.Hand(side==0,.28f,.05f);}
                    gripLast[side]=grip.position;
                }
                if(Time.time>=nextGrunt&&Mathf.Abs(d.y)>.001f){Sound(CandySound.ClimbGrunt,p+Vector3.up*motor.Height*.8f,.5f);nextGrunt=Time.time+1.5f+Mathf.Abs(GetInstanceID()%5)*.15f;}
            }
            // Low grip: a quickening rumble while hanging on with little stamina left; a jolt if the grip gives out.
            if(feel&&motor.Climbing&&motor.Stamina<.25f&&Time.time>=nextGripBuzz)
            {
                float remaining=motor.Stamina/.25f;nextGripBuzz=Time.time+Mathf.Lerp(.18f,.45f,remaining);
                CandyFeedback.Pulse(Mathf.Lerp(.35f,.1f,remaining),.04f);
            }
            if(climbing&&!motor.Climbing&&!motor.Vaulting&&nearby)
            {
                // Grip gave out: the approved slip; an ordinary let-go keeps the release sound.
                bool slipped=motor.Stamina<.02f;
                if(!slipped||!CandyWeaponSuite.PlayExtra("climb","slip",p+Vector3.up*motor.Height*.8f,GetInstanceID()))
                    Sound(CandySound.ClimbRelease,p+Vector3.up*motor.Height*.7f,.4f);
                if(feel&&slipped)CandyFeedback.Pulse(.45f,.12f);
            }
            // Critical health: the approved candy heartbeat, louder as health drops (local player only).
            if(motor.IsLocal&&playerView!=null&&playerView.ControlledHere)
            {
                damageable??=GetComponent<IDamageable>();
                float health=damageable!=null&&damageable.MaxHealth>0?damageable.Health/damageable.MaxHealth:1;
                if(health>0&&health<.3f)
                    CandyWeaponSuite.Loop("body","low-health-loop",transform,Mathf.Lerp(1,.45f,health/.3f),true);
            }
            var world=CandyAudioWorld.Instance;
            if(world)
            {
                if(!underwater&&p.y<=world.WaterHeight&&motor.VerticalSpeed<-.5f){world.Splash(p,Mathf.Clamp01(.4f-motor.VerticalSpeed*.05f),GetInstanceID(),true);underwater=true;}
                if(p.y>world.WaterHeight+.5f)underwater=false;
            }
            grounded=motor.Grounded;climbing=motor.Climbing;previousGrip=motor.Stamina;
        }
        void ObserveStamina()
        {
            if(!motor.IsLocal||motor.InputPaused||playerView==null||!playerView.ControlledHere)
            {
                staminaObserved=false;
                return;
            }
            bool empty=playerView.StaminaExhausted;
            if(staminaObserved)
            {
                if(empty&&!staminaEmpty)CandySoundSuite.Local(CandySound.StaminaEmpty);
                else if(!empty&&staminaEmpty)CandySoundSuite.Local(CandySound.StaminaUsable);
                else if(!empty&&motor.Stamina<.22f&&previousGrip>=.22f&&Time.unscaledTime>=nextGripWarning)
                {
                    CandySoundSuite.Local(CandySound.StaminaLow);
                    nextGripWarning=Time.unscaledTime+3;
                }
            }
            staminaEmpty=empty;
            staminaObserved=true;
        }
        void OnEnable(){GameEvents.Emoted+=OnEmoted;GameEvents.EffectStarted+=OnEffectStarted;GameEvents.EffectEnded+=OnEffectEnded;}
        void OnDisable(){GameEvents.Emoted-=OnEmoted;GameEvents.EffectStarted-=OnEffectStarted;GameEvents.EffectEnded-=OnEffectEnded;}
        void OnEffectStarted(GameObject who,EffectType type)
        {
            if(who!=gameObject||!type||!CandyAudioWorld.Near(transform.position,70))return;
            if(type==speedBoost)CandyWeaponSuite.PlayExtra("speed-powerup","collect",transform.position,GetInstanceID());
            else if(type==jumpBoost)CandyWeaponSuite.PlayExtra("jump-powerup","collect",transform.position,GetInstanceID());
        }
        void OnEffectEnded(GameObject who,EffectType type)
        {
            // Collecting the same orb again refreshes it; only a real run-out gets the power-down.
            if(who!=gameObject||!type||(type!=speedBoost&&type!=jumpBoost)||Boosted(type)||!CandyAudioWorld.Near(transform.position,70))return;
            CandyWeaponSuite.PlayExtra("powerup","expire",transform.position,GetInstanceID());
        }
        void OnEmoted(GameObject who,string emote)
        {
            if(who!=gameObject||motor==null||CandyJaguarAudio.IsJaguar(this)||!CandyAudioEngine.Instance||!CandyAudioWorld.Near(transform.position,70))return;
            if(Enum.TryParse<CandySound>(emote,out var cue))Sound(cue,transform.position+Vector3.up*motor.Height*.85f,.7f);else CandySignatureSounds.PlayEmote(who,emote);
        }
    }
}
