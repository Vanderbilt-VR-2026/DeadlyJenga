using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;
using UnityEngine;
namespace DeadlyJenga.Presentation
{
    public sealed partial class CandyArsenalEffects:MonoBehaviour
    {
        sealed class Beam {public LineRenderer line;public float until;}
        sealed class WeaponEffect
        {
            public EquipmentKind kind;
            public Transform model;
            public LineRenderer[] arcs;
            public LineRenderer heatLine;
            public float searchAfter, heat, lastShot;
            public bool venting, reloading;
            public int seenFrame;
        }
        sealed class Thunderbolt
        {
            public LineRenderer[] lines;
            public Vector3 start, end;
            public float born;
            public int seed;
        }
        static readonly Color LightningBlue = new Color(.25f,.70f,1);
        readonly Dictionary<string,WeaponEffect> weaponEffects=new Dictionary<string,WeaponEffect>();
        readonly Dictionary<string,Thunderbolt> thunderbolts=new Dictionary<string,Thunderbolt>();
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
            seen.Clear();
            var projectiles=match.Frame.weaponProjectiles;
            if(!ReferenceEquals(projectiles,lastProjectiles)){lastProjectiles=projectiles;receivedProjectiles=Time.time;}
            float elapsed=Mathf.Min(.08f,Time.time-receivedProjectiles);
            foreach(var shot in projectiles)
            {
                seen.Add(shot.id);if(!shots.TryGetValue(shot.id,out var model))
                {
                    model=TakeProjectile(shot.kind,shot.position);shots[shot.id]=model;
                }
                Vector3 forward=shot.velocity.sqrMagnitude>.001f?shot.velocity:Vector3.forward;
                // Bound cosmetic prediction to half a meter; no extra collision queries.
                var prediction=Vector3.ClampMagnitude(shot.velocity*elapsed,.5f);
                model.SetPositionAndRotation(shot.position+prediction,Quaternion.LookRotation(forward));
                if(shot.kind==EquipmentKind.Chainsaw)model.Rotate(90,0,Time.time*1500,Space.Self);
                else if(shot.kind==EquipmentKind.GrenadeLauncher)model.Rotate(0,0,Time.time*370,Space.Self);
            }
            stale.Clear();foreach(var item in shots)if(!seen.Contains(item.Key)){ReturnProjectile(item.Value);stale.Add(item.Key);}foreach(int id in stale)shots.Remove(id);
            cursor.Read(match.Frame,Action);
            UpdateShells();
            foreach(var beam in beams.Values)beam.line.enabled=Time.time<beam.until;
            UpdateWeaponEffects(match);
            UpdateThunderbolts();
        }
        void Action(EquipmentActionFrame action)
        {
            if(action.action==EquipmentAction.Shot&&action.remaining>=.55f)
                PresentShot(action);
            if(action.action==EquipmentAction.Shot&&UsesHeat(action.kind)&&!string.IsNullOrEmpty(action.actor))
            {
                var effect=EffectFor(action.actor,action.kind);
                effect.lastShot=Time.time;
                if(UsesShotHeat(action.kind))
                    effect.heat=Mathf.Min(1,effect.heat+.46f);
            }
            if(action.kind<=EquipmentKind.PoweredArmor)return;
            if(action.action==EquipmentAction.Impact&&action.kind==EquipmentKind.DualUzi){EmitBlast(action);Spike(new EquipmentActionFrame{position=action.position,endpoints=new[]{action.position}});return;}
            if(action.action==EquipmentAction.Impact)
            {
                EmitBlast(action);if(action.kind!=EquipmentKind.Chainsaw)BlastDetail(action);return;
            }
            if(action.action==EquipmentAction.Shot&&action.kind==EquipmentKind.DualUzi&&action.endpoints.Length>0)Spike(action);
            if(action.action!=EquipmentAction.Shot||action.endpoints==null||action.endpoints.Length==0)return;
            if(action.kind==EquipmentKind.LaserAxe&&action.shotgun)
            {
                FireThunderbolt(action);
                return;
            }
            if(action.kind!=EquipmentKind.WristLaser)return;
            if(!beams.TryGetValue(action.actor,out var beam))
            {
                var go=new GameObject("Visual - continuous weapon beam");go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=ordnanceRibbon;line.positionCount=2;line.useWorldSpace=true;line.numCapVertices=3;beam=new Beam{line=line};beams[action.actor]=beam;
            }
            beam.line.SetPosition(0,action.position);beam.line.SetPosition(1,action.endpoints[0]);beam.line.startWidth=.12f;beam.line.endWidth=beam.line.startWidth;beam.line.startColor=beam.line.endColor=new Color(1,.22f,.015f);beam.until=Time.time+.18f;
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
                            if(child.name==modelName){effect.model=child;break;}
                    }
                }

                if(item.kind==EquipmentKind.LaserAxe)UpdateAxeArcs(effect);
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

        void UpdateAxeArcs(WeaponEffect effect)
        {
            bool active=effect.model&&effect.model.gameObject.activeInHierarchy;
            if(!active&&effect.arcs==null)return;
            if(effect.arcs==null)
            {
                effect.arcs=new LineRenderer[4];
                for(int i=0;i<effect.arcs.Length;i++)effect.arcs[i]=MakeLine("Stormbreaker surface arc",7);
            }
            var model=effect.model;
            int tick=Mathf.FloorToInt(Time.time*19);
            for(int i=0;i<effect.arcs.Length;i++)
            {
                var line=effect.arcs[i];
                line.enabled=active;
                if(!active)continue;
                float side=i%2==0?1:-1;
                var from=i<2?new Vector3(side*.072f,-.22f,.82f):new Vector3(side*.044f,0,.08f);
                var to=i<2?new Vector3(side*.062f,.39f,.96f):new Vector3(side*.065f,.06f,.76f);
                line.startWidth=.027f*Mathf.Abs(model.lossyScale.z);line.endWidth=line.startWidth*.33f;
                line.startColor=LightningBlue;line.endColor=new Color(.53f,.87f,1,.55f);
                for(int j=0;j<line.positionCount;j++)
                {
                    float t=j/(line.positionCount-1f);
                    float kick=j==0||j==line.positionCount-1?0:.047f*Mathf.Sin(t*Mathf.PI);
                    var point=Vector3.Lerp(from,to,t)+new Vector3(
                        Mathf.Sin(j*16.7f+i*3+tick*4),Mathf.Cos(j*8.1f+tick),Mathf.Sin(j*9.6f-i+tick*2))*kick;
                    line.SetPosition(j,model.TransformPoint(point));
                }
            }
        }

        void FireThunderbolt(EquipmentActionFrame action)
        {
            if(string.IsNullOrEmpty(action.actor))return;
            if(!thunderbolts.TryGetValue(action.actor,out var bolt))
            {
                bolt=new Thunderbolt{lines=new LineRenderer[5]};
                for(int i=0;i<bolt.lines.Length;i++)bolt.lines[i]=MakeLine(i<2?"Stormbreaker bolt":"Stormbreaker fork",i<2?17:7);
                thunderbolts[action.actor]=bolt;
            }
            bolt.start=action.position;bolt.end=action.endpoints[0];bolt.born=Time.time;bolt.seed=action.id;
        }

        void UpdateThunderbolts()
        {
            staleEffects.Clear();
            foreach(var pair in thunderbolts)
            {
                var bolt=pair.Value;
                float age=Time.time-bolt.born;
                if(age>=.27f)
                {
                    foreach(var line in bolt.lines)Destroy(line.gameObject);
                    staleEffects.Add(pair.Key);
                    continue;
                }
                var travel=bolt.end-bolt.start;
                var side=Vector3.Cross(travel.normalized,Vector3.up);
                if(side.sqrMagnitude<.01f)side=Vector3.right;
                side.Normalize();
                var up=travel.sqrMagnitude<.001f?Vector3.up:Vector3.Cross(side,travel.normalized);
                float alpha=1-age/.27f;
                float size=Mathf.Clamp(travel.magnitude*.11f,.09f,1.1f);
                float seed=bolt.seed%7919+Mathf.Floor(age*22)*7;
                for(int i=0;i<bolt.lines.Length;i++)
                {
                    var line=bolt.lines[i];
                    var color=i==1?new Color(.8f,.94f,1):LightningBlue;
                    color.a=alpha*(i>=2?.68f:1);
                    line.startColor=line.endColor=color;
                    line.startWidth=i==0?.23f:i==1?.065f:.07f;
                    line.endWidth=i<2?line.startWidth:.005f;
                    if(i<2)
                    {
                        for(int j=0;j<line.positionCount;j++)
                            line.SetPosition(j,BoltPoint(bolt,side,up,size,seed,j));
                        continue;
                    }
                    int joint=i==4?15:4+(i-2)*5;
                    var start=BoltPoint(bolt,side,up,size,seed,joint);
                    var end=start+travel*(i==4?.055f:.16f)+
                        side*(i%2==0?1:-1)*size*(i==4?2.4f:1.55f)+up*size*.6f;
                    for(int j=0;j<line.positionCount;j++)
                    {
                        float t=j/(line.positionCount-1f);
                        line.SetPosition(j,Vector3.Lerp(start,end,t)+up*(Mathf.Sin(t*Mathf.PI)*Mathf.Sin(j*11+seed)*size*.37f));
                    }
                }
            }
            foreach(var actor in staleEffects)thunderbolts.Remove(actor);
        }

        static Vector3 BoltPoint(Thunderbolt bolt,Vector3 side,Vector3 up,float size,float seed,int joint)
        {
            float t=joint/16f;
            var center=Vector3.Lerp(bolt.start,bolt.end,t);
            if(joint==0||joint==16)return center;
            float envelope=Mathf.Sin(t*Mathf.PI);
            return center+(side*Mathf.Sin(joint*14.7f+seed)+up*Mathf.Cos(joint*8.4f-seed))*size*envelope;
        }

        void ReleaseEffect(WeaponEffect effect)
        {
            if(effect.heatLine)Destroy(effect.heatLine.gameObject);
            if(effect.arcs!=null)foreach(var line in effect.arcs)if(line)Destroy(line.gameObject);
        }

        void ClearWeaponEffects()
        {
            foreach(var effect in weaponEffects.Values)ReleaseEffect(effect);
            weaponEffects.Clear();
            foreach(var bolt in thunderbolts.Values)foreach(var line in bolt.lines)if(line)Destroy(line.gameObject);
            thunderbolts.Clear();
            if(steam)steam.Clear();
            foreach(var shell in shells)if(shell.model)shell.model.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            ClearWeaponEffects();
            ReleaseOrdnance();
            if(shellMaterial)Destroy(shellMaterial);
            if(shellMesh)Destroy(shellMesh);
            foreach(var s in shots.Values)if(s)Destroy(s.gameObject);
            foreach(var b in beams.Values)if(b.line)Destroy(b.line.gameObject);
        }
    }
}
