using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using DeadlyJenga.CandyAudio;

namespace DeadlyJenga.Presentation
{
    public sealed class CandyPoweredSuitFeedback : MonoBehaviour
    {
        readonly Dictionary<string,Transform[]> jets=new Dictionary<string,Transform[]>();
        readonly HashSet<string> visible=new HashSet<string>();
        Material glow;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=host=>{if(!host.GetComponent<CandyPoweredSuitFeedback>())host.AddComponent<CandyPoweredSuitFeedback>();};
        public int ActiveSuits {get;private set;}
        void Update()
        {
            visible.Clear();ActiveSuits=0;var match=MatchRuntime.Current;
            if(match!=null&&match.Frame.phase==RoundPhase.Playing)
            foreach(var state in match.Frame.equipment)
            {
                if(state.kind!=EquipmentKind.PoweredArmor||!state.drawn||!state.thrust)continue;
                var actor=match.FindActor(state.actor);if(actor==null||!actor.Alive)continue;
                visible.Add(state.actor);ActiveSuits++;
                if(!jets.TryGetValue(state.actor,out var flames)){flames=Create();jets.Add(state.actor,flames);}
                float scale=Mathf.Clamp((actor.ViewPose.position.y-actor.transform.position.y)/1.6f,.2f,3);
                var yaw=Quaternion.Euler(0,actor.ViewPose.rotation.eulerAngles.y,0);
                for(int i=0;i<3;i++)
                {
                    var flame=flames[i];flame.gameObject.SetActive(true);
                    var pose=i==2?actor.HandPose(state.leftHanded):new Pose(actor.transform.position+yaw*new Vector3(i==0?-.11f:.11f,.03f,.04f)*scale,Quaternion.identity);
                    var direction=i==2?pose.rotation*Vector3.down:Vector3.down;
                    float pulse=1+Mathf.Sin(Time.time*43+i*2)*.12f;
                    flame.SetPositionAndRotation(pose.position+direction*.23f*scale,Quaternion.FromToRotation(Vector3.up,direction));
                    flame.localScale=new Vector3(.065f,.48f*pulse,.065f)*scale;
                }
                CandyAudioEngine.Instance?.Loop(CandySound.SuitThruster,actor.transform,.8f,actor.IsLocal);
            }
            foreach(var pair in jets)if(!visible.Contains(pair.Key))foreach(var flame in pair.Value)if(flame)flame.gameObject.SetActive(false);
        }
        Transform[] Create()
        {
            glow??=Resources.Load<Material>("CandyUtilityGlow");var result=new Transform[3];
            for(int i=0;i<3;i++)
            {
                var go=new GameObject("Visual - powered suit jet",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
                go.GetComponent<MeshFilter>().sharedMesh=Resources.GetBuiltinResource<Mesh>("Sphere.fbx");var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=glow;
                var properties=new MaterialPropertyBlock();properties.SetColor("_BaseColor",new Color(.35f,.95f,1));properties.SetColor("_EmissionColor",new Color(.3f,1.6f,2));renderer.SetPropertyBlock(properties);result[i]=go.transform;
            }
            return result;
        }
        void OnDisable(){foreach(var pair in jets)foreach(var flame in pair.Value)if(flame)flame.gameObject.SetActive(false);ActiveSuits=0;}
        void OnDestroy(){foreach(var pair in jets)foreach(var flame in pair.Value)if(flame)Destroy(flame.gameObject);}
    }
}
