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
        EquipmentProjectileFrame[] lastProjectiles;float receivedProjectiles;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=go=>{if(!go.GetComponent<CandyArsenalEffects>())go.AddComponent<CandyArsenalEffects>();};
        void Awake(){glow=Resources.Load<Material>("CandyUtilityGlow");PrepareOrdnance();cursor.Baseline(MatchRuntime.Current?.Frame);}
        void Update()
        {
            var match=MatchRuntime.Current;if(match==null)return;seen.Clear();
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
            foreach(var beam in beams.Values)beam.line.enabled=Time.time<beam.until;
        }
        void Action(EquipmentActionFrame action)
        {
            if(action.kind<=EquipmentKind.PoweredArmor)return;
            if(action.action==EquipmentAction.Impact&&action.kind==EquipmentKind.DualUzi){EmitBlast(action);Spike(new EquipmentActionFrame{position=action.position,endpoints=new[]{action.position}});return;}
            if(action.action==EquipmentAction.Impact)
            {
                EmitBlast(action);if(action.kind!=EquipmentKind.Chainsaw)BlastDetail(action);return;
            }
            if(action.action==EquipmentAction.Shot&&action.kind==EquipmentKind.DualUzi&&action.endpoints.Length>0)Spike(action);
            if(action.action!=EquipmentAction.Shot||action.endpoints.Length==0||action.kind!=EquipmentKind.WristLaser&&!(action.kind==EquipmentKind.LaserAxe&&action.shotgun))return;
            if(!beams.TryGetValue(action.actor,out var beam))
            {
                var go=new GameObject("Visual - continuous weapon beam");go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=ordnanceRibbon;line.positionCount=2;line.useWorldSpace=true;line.numCapVertices=3;beam=new Beam{line=line};beams[action.actor]=beam;
            }
            beam.line.SetPosition(0,action.position);beam.line.SetPosition(1,action.endpoints[0]);beam.line.startWidth=action.kind==EquipmentKind.LaserAxe?.8f:.12f;beam.line.endWidth=beam.line.startWidth;beam.line.startColor=beam.line.endColor=new Color(1,.22f,.015f);beam.until=Time.time+(action.kind==EquipmentKind.LaserAxe?.25f:.18f);
        }
        void OnDestroy(){ReleaseOrdnance();foreach(var s in shots.Values)if(s)Destroy(s.gameObject);foreach(var b in beams.Values)if(b.line)Destroy(b.line.gameObject);}
    }
}
