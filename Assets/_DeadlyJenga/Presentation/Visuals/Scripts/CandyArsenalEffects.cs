using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;
using UnityEngine;
namespace DeadlyJenga.Presentation
{
    public sealed partial class CandyArsenalEffects:MonoBehaviour
    {
        sealed class Beam
        {
            public LineRenderer outer, core;
            public Transform model;
            public Vector3 origin, endpoint;
            public float lastShot, nextSpark;
        }

        static readonly Color WristCyan = new Color(.08f,.68f,1f);
        static readonly Color WristCore = new Color(.61f,.95f,1f);
        const float BeamGrace = .135f, BeamFade = .055f;
        readonly List<string> expiredWristBeams = new List<string>();
        sealed class WeaponEffect
        {
            public EquipmentKind kind;
            public Transform model;
            public StormStroke stormCharge;
            public LineRenderer heatLine;
            public Transform[] heatBars;
            public Transform coolingPose, coolBody, coolGlass;
            public float searchAfter, heat, lastShot;
            public bool venting, reloading;
            public int seenFrame;
        }
        readonly Dictionary<string,WeaponEffect> weaponEffects=new Dictionary<string,WeaponEffect>();
        readonly List<string> staleEffects=new List<string>();
        IMatchRuntime effectsRuntime;
        ParticleSystem steam;
        readonly Dictionary<int,Transform> shots=new Dictionary<int,Transform>();
        readonly Dictionary<string,Beam> beams=new Dictionary<string,Beam>();
        readonly HashSet<int> seen=new HashSet<int>();readonly List<int> stale=new List<int>();
        readonly CandyEquipmentActionCursor cursor=new CandyEquipmentActionCursor();Material glow;
        EquipmentProjectileFrame[] lastProjectiles;float receivedProjectiles;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=go=>{if(!go.GetComponent<CandyArsenalEffects>())go.AddComponent<CandyArsenalEffects>();};
        void Awake(){glow=Resources.Load<Material>("CandyUtilityGlow");PrepareOrdnance();cursor.Baseline(MatchRuntime.Current?.Frame);}
        void Update()
        {
            var match=MatchRuntime.Current;
            if(!ReferenceEquals(effectsRuntime,match))
            {
                ClearWeaponEffects();
                effectsRuntime=match;
            }
            if(match==null)return;
            UpdateLodgedWheels(match.Frame.phase==RoundPhase.Playing);
            seen.Clear();
            var projectiles=match.Frame.weaponProjectiles;
            if(!ReferenceEquals(projectiles,lastProjectiles)){lastProjectiles=projectiles;receivedProjectiles=Time.time;}
            float elapsed=Mathf.Min(.08f,Time.time-receivedProjectiles);
            foreach(var shot in projectiles)
            {
                seen.Add(shot.id);if(!shots.TryGetValue(shot.id,out var model))
                {
                    model=TakeProjectile(shot.kind,shot.position,shot.rocketCandyVariant);shots[shot.id]=model;
                    if (shot.kind == EquipmentKind.DualUzi)
                        TintSpikerProjectile(model, SpikerShotTint(shot.owner));
                }
                Vector3 forward=shot.velocity.sqrMagnitude>.001f?shot.velocity:Vector3.forward;
                // Bound cosmetic prediction to half a meter; no extra collision queries.
                var prediction=Vector3.ClampMagnitude(shot.velocity*elapsed,.5f);
                model.SetPositionAndRotation(shot.position+prediction,Quaternion.LookRotation(forward));
                if(shot.kind==EquipmentKind.Chainsaw)model.Rotate(90,0,Time.time*1500,Space.Self);
                else if(shot.kind==EquipmentKind.GrenadeLauncher)model.Rotate(0,0,Time.time*370,Space.Self);
                else if(shot.kind==EquipmentKind.RocketLauncher)
                {
                    model.Rotate(0,0,(Time.time*190+shot.id*39)%360,Space.Self);
                    var plume=model.GetChild(3).GetChild(0);
                    plume.localScale=new Vector3(.105f,.105f,.34f*(.91f+.10f*Mathf.Sin(Time.time*39+shot.id)));
                }
            }
            stale.Clear();foreach(var item in shots)if(!seen.Contains(item.Key)){ReturnProjectile(item.Value);stale.Add(item.Key);}foreach(int id in stale)shots.Remove(id);
            cursor.Read(match.Frame,Action);
            UpdateShells();

            UpdateWeaponEffects(match);
            UpdateStormStrokes();
        }
        void Action(EquipmentActionFrame action)
        {
            if (action.kind == EquipmentKind.LaserAxe)
            {
                if (action.action == EquipmentAction.Shot) PresentStormShot(action);
                return;
            }
            if(action.kind!=EquipmentKind.WristLaser&&action.action==EquipmentAction.Shot&&action.remaining>=.55f)
                PresentShot(action);
            if(action.action==EquipmentAction.Shot&&UsesHeat(action.kind)&&!string.IsNullOrEmpty(action.actor))
            {
                var effect=EffectFor(action.actor,action.kind);
                effect.lastShot=Time.time;
                if(UsesShotHeat(action.kind))
                    effect.heat=Mathf.Min(1,effect.heat+.46f);
            }
            if(action.kind==EquipmentKind.WristLaser)
            {
                PresentWristAction(action);
                return;
            }
            if(action.kind<=EquipmentKind.PoweredArmor)return;
            if(action.action==EquipmentAction.Impact&&action.kind==EquipmentKind.LaserClaws)
            {
                ClawImpact(action);
                return;
            }
            if(action.action==EquipmentAction.Impact&&action.kind==EquipmentKind.DualUzi)
            {
                Spike(action.position, SpikerShotTint(action.actor));
                return;
            }
            if(action.action==EquipmentAction.Impact)
            {
                EmitBlast(action);
                if(action.kind==EquipmentKind.Chainsaw)
                {
                    if(action.shotgun)LodgeWheel(action);
                }
                else BlastDetail(action);
                return;
            }
        }

        void PresentWristAction(EquipmentActionFrame action)
        {
            if(string.IsNullOrEmpty(action.actor)||MatchRuntime.Current?.Frame.phase!=RoundPhase.Playing||
                action.remaining<.55f)return;
            if(action.action==EquipmentAction.Impact)
            {
                if(beams.TryGetValue(action.actor,out var contactBeam)&&Time.time>=contactBeam.nextSpark)
                {
                    contactBeam.nextSpark=Time.time+.13f;
                    EmitWristContact(action.position,contactBeam.origin,action.id);
                }
                return;
            }
            if(action.action!=EquipmentAction.Shot||action.endpoints==null||action.endpoints.Length==0)return;
            if((action.endpoints[0]-action.position).sqrMagnitude<.001f)return;
            bool fresh=!beams.TryGetValue(action.actor,out var beam);
            if(fresh)
            {
                beam=new Beam
                {
                    outer=MakeWristLine("Oxblood beam fringe",.027f),
                    core=MakeWristLine("Oxblood beam core",.0105f)
                };
                beams.Add(action.actor,beam);
            }
            if(fresh||Time.time-beam.lastShot>BeamGrace)
                EmitMuzzle(action.position,Quaternion.LookRotation(action.endpoints[0]-action.position),WristCyan,.038f,0,action.id);
            beam.origin=action.position;
            beam.endpoint=action.endpoints[0];
            beam.lastShot=Time.time;
            if(!beam.model)
            {
                var owner=effectsRuntime.FindParticipantRoot(action.actor);
                if(owner)beam.model=owner.Find("Visual - WristLaser");
            }
        }

        LineRenderer MakeWristLine(string name,float width)
        {
            var node=new GameObject(name);
            node.transform.SetParent(transform,false);
            var line=node.AddComponent<LineRenderer>();
            line.sharedMaterial=ordnanceRibbon;
            line.startWidth=line.endWidth=width;
            line.positionCount=2;
            line.useWorldSpace=true;
            line.numCapVertices=3;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows=false;
            line.enabled=false;
            return line;
        }

        void UpdateWristBeams()
        {
            if(effectsRuntime is not IEquipmentRuntime equipment)return;
            expiredWristBeams.Clear();
            foreach(var pair in beams)
            {
                var beam=pair.Value;
                var state=equipment.EquipmentFor(pair.Key);
                float age=Time.time-beam.lastShot;
                bool canShow=state?.kind==EquipmentKind.WristLaser&&state.drawn&&effectsRuntime.Frame.phase==RoundPhase.Playing;
                float grace=state!=null&&state.triggerHeld&&state.reloadRemaining<=0?BeamGrace:.045f;
                float intensity=canShow?1-Mathf.Clamp01((age-grace)/BeamFade):0;
                beam.outer.enabled=beam.core.enabled=intensity>0;
                if(intensity<=0)
                {
                    if(age>1)expiredWristBeams.Add(pair.Key);
                    continue;
                }

                Vector3 start=beam.origin;
                if(beam.model&&beam.model.gameObject.activeInHierarchy)
                {
                    var socket=beam.model.TransformPoint(ShotAnchor(EquipmentKind.WristLaser,false));
                    if((socket-start).sqrMagnitude<.09f)start=socket;
                }
                SetWristLine(beam.outer,start,beam.endpoint,WristCyan,intensity*.54f);
                SetWristLine(beam.core,start,beam.endpoint,WristCore,intensity);
            }
            foreach(string id in expiredWristBeams)
            {
                Destroy(beams[id].outer.gameObject);
                Destroy(beams[id].core.gameObject);
                beams.Remove(id);
            }
        }

        static void SetWristLine(LineRenderer line,Vector3 start,Vector3 end,Color color,float alpha)
        {
            color.a=alpha;
            line.SetPosition(0,start);
            line.SetPosition(1,end);
            line.startColor=line.endColor=color;
        }

        void EmitWristContact(Vector3 point,Vector3 origin,int seed)
        {
            if(!CandyWeaponImpacts.VisibleToAnyCamera(point,90))return;
            var incoming=point-origin;
            if(incoming.sqrMagnitude<.0001f)return;
            var normal=-incoming.normalized;
            var orientation=Quaternion.LookRotation(normal);
            if(burstParticles.isStopped)burstParticles.Play();
            burstParticles.Emit(new ParticleSystem.EmitParams
            {
                position=point+normal*.012f,startColor=WristCore,startLifetime=.065f,startSize=.059f
            },1);
            for(int i=0;i<6;i++)
            {
                float angle=i*2.39996f+seed*.11f;
                var sideways=orientation*new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
                burstParticles.Emit(new ParticleSystem.EmitParams
                {
                    position=point+normal*.018f,
                    velocity=normal*(.42f+i*.15f)+sideways*(.65f+i*.19f)+Vector3.up*.38f,
                    startColor=i%3==0?new Color(.68f,.29f,.10f):new Color(1,.54f,.17f),
                    startLifetime=.12f+i*.013f,startSize=i%3==0?.013f:.022f
                },1);
            }
        }

        static bool UsesShotHeat(EquipmentKind kind) =>
            kind==EquipmentKind.Blaster||kind==EquipmentKind.PoweredArmor;

        static bool UsesHeat(EquipmentKind kind) => UsesShotHeat(kind)||
            kind==EquipmentKind.WristLaser||kind==EquipmentKind.ChainGun||kind==EquipmentKind.LaserAxe||
            kind==EquipmentKind.NeedleDrill||kind==EquipmentKind.Chainsaw;

        WeaponEffect EffectFor(string actor, EquipmentKind kind)
        {
            if(weaponEffects.TryGetValue(actor,out var effect))
            {
                if(effect.kind==kind)return effect;
                ReleaseEffect(effect);
            }
            effect=new WeaponEffect{kind=kind};
            weaponEffects[actor]=effect;
            return effect;
        }

        void UpdateWeaponEffects(IMatchRuntime match)
        {
            foreach(var item in match.Frame.equipment)
            {
                if(item==null||string.IsNullOrEmpty(item.actor)||!UsesHeat(item.kind))continue;
                var effect=EffectFor(item.actor,item.kind);
                effect.seenFrame=Time.frameCount;
                if(!effect.model&&Time.time>=effect.searchAfter)
                {
                    effect.searchAfter=Time.time+.25f;
                    var owner=match.FindParticipantRoot(item.actor);
                    if(owner)
                    {
                        string modelName="Visual - "+item.kind;
                        foreach(var child in owner.GetComponentsInChildren<Transform>(true))
                            if(child.name==modelName)
                            {
                                effect.model=child;
                                effect.heatBars=null;
                                if (item.kind == EquipmentKind.Blaster)
                                {
                                    effect.coolingPose = child.Find("Open cooling hatch pose");
                                    effect.coolBody = child.Find("Crimson Alien Blaster body opaque");
                                    effect.coolGlass = child.Find("Crimson Alien Blaster body glass");
                                }
                                if (item.kind == EquipmentKind.ChainGun && child.Find("Heat bar 1"))
                                {
                                    effect.heatBars = new Transform[10];
                                    for (int i = 0; i < effect.heatBars.Length; i++)
                                        effect.heatBars[i] = child.Find("Heat bar " + (i + 1));
                                }
                                break;
                            }
                    }
                }

                if (item.kind == EquipmentKind.LaserAxe) UpdateStormCharge(effect, item);
                bool reload=item.drawn&&item.reloadRemaining>0;
                bool shotHeat=UsesShotHeat(item.kind);
                if(!shotHeat)
                {
                    int capacity=EquipmentSpecifications.Get(item.kind).magazine;
                    float consumed=item.magazineInitialized&&capacity>0?1-item.rounds/(float)capacity:0;
                    float recent=Mathf.Clamp01(1-(Time.time-effect.lastShot)/2.5f);
                    float target=0;
                    if(item.drawn&&(item.kind!=EquipmentKind.LaserAxe||item.shotgun))
                    {
                        float reloadSeconds=EquipmentSpecifications.Get(item.kind).reloadSeconds;
                        target=reload&&reloadSeconds>0 ? item.reloadRemaining/reloadSeconds :
                            Mathf.Max(item.charge*.42f,consumed*recent);
                    }
                    effect.heat=Mathf.MoveTowards(effect.heat,target,Time.deltaTime*2.5f);
                }
                else effect.heat=Mathf.Max(0,effect.heat-Time.deltaTime*.43f);

                bool active=effect.model&&effect.model.gameObject.activeInHierarchy&&item.drawn;
                if(active&&((reload&&!effect.reloading&&item.rounds==0)||
                    (shotHeat&&effect.heat>=.93f&&!effect.venting)))
                {
                    EmitSteam(effect);
                    effect.venting=true;
                }
                if(effect.heat<.45f&&!reload)effect.venting=false;
                effect.reloading=reload&&(effect.reloading||active);
                if (effect.coolingPose)
                {
                    bool open = active && effect.venting;
                    if (effect.coolingPose.gameObject.activeSelf != open)
                    {
                        effect.coolingPose.gameObject.SetActive(open);
                        if (effect.coolBody) effect.coolBody.gameObject.SetActive(!open);
                        if (effect.coolGlass) effect.coolGlass.gameObject.SetActive(!open);
                    }
                }
                if (effect.heatBars != null)
                {
                    int lit = active ? Mathf.CeilToInt(effect.heat * effect.heatBars.Length) : 0;
                    for (int i = 0; i < effect.heatBars.Length; i++)
                        if (effect.heatBars[i] && effect.heatBars[i].gameObject.activeSelf != (i < lit))
                            effect.heatBars[i].gameObject.SetActive(i < lit);
                }
                UpdateHeatLine(effect,active);
            }

            staleEffects.Clear();
            foreach(var pair in weaponEffects)
                if(pair.Value.seenFrame!=Time.frameCount)
                {
                    ReleaseEffect(pair.Value);
                    staleEffects.Add(pair.Key);
                }
            foreach(var actor in staleEffects)weaponEffects.Remove(actor);
        }

        static Vector3 VentPoint(WeaponEffect effect)
        {
            return effect.kind switch
            {
                EquipmentKind.ChainGun=>new Vector3(0,-.235f,.72f),
                EquipmentKind.WristLaser=>new Vector3(0,.12f,.12f),
                EquipmentKind.PoweredArmor=>new Vector3(0,0,.035f),
                EquipmentKind.NeedleDrill=>new Vector3(0,.19f,.20f),
                EquipmentKind.Chainsaw=>new Vector3(0,.15f,.28f),
                EquipmentKind.LaserAxe=>new Vector3(0,.25f,.94f),
                _=>new Vector3(0,.18f,.48f)
            };
        }

        void UpdateHeatLine(WeaponEffect effect,bool active)
        {
            if(!active||effect.heat<.15f)
            {
                if(effect.heatLine)effect.heatLine.enabled=false;
                return;
            }
            if(!effect.heatLine)effect.heatLine=MakeLine("Weapon heat shimmer",2);
            var vent=VentPoint(effect);
            var line=effect.heatLine;
            line.enabled=true;
            line.SetPosition(0,effect.model.TransformPoint(vent-Vector3.forward*.055f));
            line.SetPosition(1,effect.model.TransformPoint(vent+Vector3.forward*.055f));
            line.startWidth=line.endWidth=(.025f+.055f*effect.heat)*Mathf.Abs(effect.model.lossyScale.z);
            line.startColor=line.endColor=new Color(1,.12f+.46f*effect.heat,.025f,effect.heat*.84f);
        }

        void EmitSteam(WeaponEffect effect)
        {
            if(!steam)
            {
                var emitter=new GameObject("Weapon vent steam");
                emitter.transform.SetParent(transform,false);
                steam=emitter.AddComponent<ParticleSystem>();
                steam.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=steam.main;
                main.playOnAwake=false;main.maxParticles=128;
                main.simulationSpace=ParticleSystemSimulationSpace.World;
                var emission=steam.emission;emission.enabled=false;
                var shape=steam.shape;shape.enabled=false;
                var size=steam.sizeOverLifetime;size.enabled=true;
                size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.35f,1,1.6f));
                var fade=steam.colorOverLifetime;fade.enabled=true;
                var gradient=new Gradient();
                gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                    new[]{new GradientAlphaKey(.7f,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(0,1)});
                fade.color=gradient;
                emitter.GetComponent<ParticleSystemRenderer>().sharedMaterial=ordnanceBurst;
                steam.Play();
            }
            var model=effect.model;
            var port=VentPoint(effect);
            for(int i=0;i<18;i++)
            {
                float side=i%2==0?1:-1;
                float a=i*2.39996f;
                var direction=model.right*(side*(.42f+.21f*Mathf.Cos(a)))+
                    Vector3.up*(.26f+.17f*Mathf.Sin(a))+model.forward*.13f;
                steam.Emit(new ParticleSystem.EmitParams{
                    position=model.TransformPoint(port+Vector3.right*(side*.055f)+Vector3.forward*(i%4*.018f)),
                    velocity=direction,startLifetime=.38f+i%5*.075f,startSize=.075f+i%4*.026f,
                    startColor=new Color(.69f,.77f,.82f,.48f)},1);
            }
        }

        LineRenderer MakeLine(string name,int points)
        {
            var go=new GameObject(name);
            go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();
            line.sharedMaterial=ordnanceRibbon;
            line.useWorldSpace=true;
            line.positionCount=points;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows=false;
            return line;
        }

        void ReleaseEffect(WeaponEffect effect)
        {
            if(effect.heatLine)Destroy(effect.heatLine.gameObject);
            if (effect.stormCharge?.filter) Destroy(effect.stormCharge.filter.gameObject);
        }

        void ClearWeaponEffects()
        {
            foreach(var effect in weaponEffects.Values)ReleaseEffect(effect);
            weaponEffects.Clear();
            ClearLodgedWheels();
            foreach(var beam in beams.Values)
            {
                if(beam.outer)Destroy(beam.outer.gameObject);
                if(beam.core)Destroy(beam.core.gameObject);
            }
            beams.Clear();
            while (stormStrokes.Count > 0) RetireStormStroke(stormStrokes.Count - 1);
            if(steam)steam.Clear();
            foreach(var shell in shells)if(shell.model)shell.model.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            ClearWeaponEffects();
            ReleaseOrdnance();
            if (stormMeshes != null) foreach (var mesh in stormMeshes) Destroy(mesh);
            if(shellMaterial)Destroy(shellMaterial);
            if(shellMesh)Destroy(shellMesh);
            foreach(var s in shots.Values)if(s)Destroy(s.gameObject);

        }
    }
}
