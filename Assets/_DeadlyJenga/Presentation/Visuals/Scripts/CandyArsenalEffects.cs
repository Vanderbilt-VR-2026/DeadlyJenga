using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;
using UnityEngine;
namespace DeadlyJenga.Presentation
{
    public sealed partial class CandyArsenalEffects:MonoBehaviour
    {
        sealed class Beam {public LineRenderer line;public float until;}
        readonly Dictionary<int,Transform> shots=new Dictionary<int,Transform>();
        readonly Dictionary<string,Beam> beams=new Dictionary<string,Beam>();
        readonly HashSet<int> seen=new HashSet<int>();readonly List<int> stale=new List<int>();
        readonly CandyEquipmentActionCursor cursor=new CandyEquipmentActionCursor();Material glow;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=go=>{if(!go.GetComponent<CandyArsenalEffects>())go.AddComponent<CandyArsenalEffects>();};
        void Awake(){glow=Resources.Load<Material>("CandyUtilityGlow");cursor.Baseline(MatchRuntime.Current?.Frame);}
        void Update()
        {
            var match=MatchRuntime.Current;if(match==null)return;seen.Clear();
            foreach(var shot in match.Frame.weaponProjectiles)
            {
                seen.Add(shot.id);if(!shots.TryGetValue(shot.id,out var model))
                {
                    var go=new GameObject("Visual - "+shot.kind+" projectile",typeof(MeshFilter),typeof(MeshRenderer));model=go.transform;model.SetParent(transform,false);shots[shot.id]=model;
                    go.GetComponent<MeshFilter>().sharedMesh=Resources.GetBuiltinResource<Mesh>(shot.kind==EquipmentKind.Chainsaw?"Cylinder.fbx":shot.kind==EquipmentKind.RocketLauncher||shot.kind==EquipmentKind.GrenadeLauncher?"Sphere.fbx":"Capsule.fbx");if(shot.kind==EquipmentKind.Chainsaw&&EquipmentModelData.Parts.TryGetValue(shot.kind,out var pieces))foreach(var part in pieces)if(part.mesh.name.Contains("Serrated saw wheel")){go.GetComponent<MeshFilter>().sharedMesh=part.mesh;break;}
                    go.GetComponent<MeshRenderer>().sharedMaterial=glow;
                    var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",shot.kind==EquipmentKind.DualUzi?new Color(.3f,1,.02f):shot.kind==EquipmentKind.GrenadeLauncher?new Color(.35f,.15f,1):shot.kind==EquipmentKind.Chainsaw?new Color(.65f,.8f,.95f):new Color(1,.2f,.05f));go.GetComponent<MeshRenderer>().SetPropertyBlock(block);
                    var trail=go.AddComponent<TrailRenderer>();trail.sharedMaterial=glow;trail.time=.18f;trail.startWidth=.07f;trail.endWidth=0;trail.minVertexDistance=.12f;
                }
                model.position=shot.position;model.rotation=Quaternion.LookRotation(shot.velocity.sqrMagnitude>.001f?shot.velocity:Vector3.forward)*Quaternion.Euler(90,0,shot.kind==EquipmentKind.Chainsaw?Time.time*1500:0);model.localScale=shot.kind==EquipmentKind.Chainsaw?Vector3.one*.8f:shot.kind==EquipmentKind.RocketLauncher?Vector3.one*.65f:shot.kind==EquipmentKind.GrenadeLauncher?Vector3.one*.28f:new Vector3(.13f,.32f,.13f);
            }
            stale.Clear();foreach(var item in shots)if(!seen.Contains(item.Key)){Destroy(item.Value.gameObject);stale.Add(item.Key);}foreach(int id in stale)shots.Remove(id);
            cursor.Read(match.Frame,Action);
            foreach(var beam in beams.Values)beam.line.enabled=Time.time<beam.until;
        }
        void Action(EquipmentActionFrame action)
        {
            if(action.kind<=EquipmentKind.PoweredArmor)return;
            if(action.action==EquipmentAction.Impact&&action.kind==EquipmentKind.DualUzi){Spike(new EquipmentActionFrame{position=action.position,endpoints=new[]{action.position}});return;}
            if(action.action==EquipmentAction.Impact)
            {
                var burst=new GameObject("Visual - candy blast");burst.transform.position=action.position;var particles=burst.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=particles.main;main.loop=false;main.duration=.4f;main.startLifetime=action.kind==EquipmentKind.RocketLauncher?1.1f:.65f;main.startSpeed=action.kind==EquipmentKind.RocketLauncher?11:6;main.startSize=action.kind==EquipmentKind.RocketLauncher?.7f:.25f;main.maxParticles=72;main.startColor=action.kind==EquipmentKind.GrenadeLauncher?new Color(.35f,.12f,1,.8f):new Color(1,.4f,.06f,.8f);var emission=particles.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,action.kind==EquipmentKind.RocketLauncher?72:40)});var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.3f;particles.GetComponent<ParticleSystemRenderer>().sharedMaterial=glow;particles.Play();Destroy(burst,2);BlastDetail(action);return;
            }
            if(action.action==EquipmentAction.Shot&&action.kind==EquipmentKind.DualUzi&&action.endpoints.Length>0)Spike(action);
            if(action.action!=EquipmentAction.Shot||action.endpoints.Length==0||action.kind!=EquipmentKind.WristLaser&&!(action.kind==EquipmentKind.LaserAxe&&action.shotgun))return;
            if(!beams.TryGetValue(action.actor,out var beam))
            {
                var go=new GameObject("Visual - continuous weapon beam");go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=glow;line.positionCount=2;line.useWorldSpace=true;line.numCapVertices=3;beam=new Beam{line=line};beams[action.actor]=beam;
            }
            beam.line.SetPosition(0,action.position);beam.line.SetPosition(1,action.endpoints[0]);beam.line.startWidth=action.kind==EquipmentKind.LaserAxe?.8f:.12f;beam.line.endWidth=beam.line.startWidth;beam.line.startColor=beam.line.endColor=new Color(1,.22f,.015f);beam.until=Time.time+(action.kind==EquipmentKind.LaserAxe?.25f:.18f);
        }
        void OnDestroy(){foreach(var s in shots.Values)if(s)Destroy(s.gameObject);foreach(var b in beams.Values)if(b.line)Destroy(b.line.gameObject);}
    }
}
