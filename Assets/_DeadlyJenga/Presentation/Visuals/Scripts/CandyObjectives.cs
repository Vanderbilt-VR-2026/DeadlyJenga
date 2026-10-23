using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    public sealed class CandyObjectives : MonoBehaviour
    {
        readonly Dictionary<int,Transform> flags=new Dictionary<int,Transform>();readonly Dictionary<string,Transform> blades=new Dictionary<string,Transform>();
        LineRenderer hillRing,hillBeam;
        readonly List<Material> materials=new List<Material>();float scanAt;Camera camera;
        void Update()
        {
            var runtime=MatchRuntime.Current;if(runtime?.Frame==null)return;var f=runtime.Frame;camera=Services.Session?.ActiveCamera;if(Time.unscaledTime<scanAt)return;scanAt=Time.unscaledTime+.05f;
            Hill(f);
            foreach(var flag in flags.Values)if(flag)flag.gameObject.SetActive(false);
            foreach(var fstate in f.flags)
            {
                if(!flags.TryGetValue(fstate.team,out var root)||!root){Color c=Color.white;foreach(var t in f.teams)if(t.id==fstate.team)c=t.color;root=MakeFlag(c);flags[fstate.team]=root;}
                root.gameObject.SetActive(f.rules.mode==GameModeKind.CaptureFlag);root.position=fstate.position;
                if(!string.IsNullOrEmpty(fstate.carrier))
                {var actor=runtime.FindActor(fstate.carrier);if(actor!=null){var pose=actor.HandPose(false);root.position=pose.position+pose.rotation*new Vector3(0,0,.06f);root.rotation=pose.rotation*Quaternion.Euler(0,0,75);}}
                else root.rotation=Quaternion.identity;
            }
            foreach(var blade in blades.Values)if(blade)blade.gameObject.SetActive(false);
            foreach(var player in f.players)if(player.alive&&player.role=="infected")
            {
                if(!blades.TryGetValue(player.id,out var blade)||!blade){blade=Shape("Visual - infected candy needle",PrimitiveType.Capsule,new Color(.2f,1,.035f));blade.localScale=new Vector3(.035f,.5f,.035f);blades[player.id]=blade;}
                var actor=runtime.FindActor(player.id);var hand=actor!=null?actor.HandPose(false):new Pose(player.viewPosition+player.viewRotation*new Vector3(.22f,-.2f,.15f),player.viewRotation);
                blade.gameObject.SetActive(true);blade.position=hand.position+hand.rotation*Vector3.forward*.3f;blade.rotation=hand.rotation*Quaternion.Euler(90,0,0);
            }
        }
        void Hill(MatchFrame frame)
        {
            bool show=frame.rules.mode==GameModeKind.KingOfHill&&frame.phase!=RoundPhase.Results;
            if(!show){if(hillRing)hillRing.gameObject.SetActive(false);if(hillBeam)hillBeam.gameObject.SetActive(false);return;}
            if(!hillRing)
            {
                var shader=Resources.Load<Shader>("CandyScenery/CandyObjectiveBeacon");if(!shader)return;var material=new Material(shader);materials.Add(material);
                hillRing=HillLine("Visual - hill capture ring",material,48,.09f);hillRing.loop=true;
                hillRing.startColor=hillRing.endColor=new Color(1,.73f,.08f,.8f);
                hillBeam=HillLine("Visual - hill beacon",material,2,.2f);hillBeam.startColor=new Color(.12f,.7f,1,.55f);hillBeam.endColor=new Color(.12f,.7f,1,0);
            }
            hillRing.gameObject.SetActive(true);hillBeam.gameObject.SetActive(true);float radius=Mathf.Max(.1f,frame.rules.hillRadius);
            for(int i=0;i<48;i++){float angle=i*Mathf.PI*2/48;hillRing.SetPosition(i,frame.hill+new Vector3(Mathf.Cos(angle)*radius,.05f,Mathf.Sin(angle)*radius));}
            hillBeam.SetPosition(0,frame.hill+Vector3.up*.08f);hillBeam.SetPosition(1,frame.hill+Vector3.up*12);
        }
        LineRenderer HillLine(string name,Material material,int count,float width)
        {var go=new GameObject(name);go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.positionCount=count;line.widthMultiplier=width;line.useWorldSpace=true;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;return line;}
        Transform MakeFlag(Color color)
        {
            var root=new GameObject("Visual - lollipop flag").transform;root.SetParent(transform,false);
            var stick=Shape("Guidon handle",PrimitiveType.Cylinder,Color.white);stick.SetParent(root,false);stick.localPosition=new Vector3(0,1,0);stick.localScale=new Vector3(.055f,1,.055f);
            var candy=Shape("Team lollipop",PrimitiveType.Sphere,color);candy.SetParent(root,false);candy.localPosition=new Vector3(0,2.1f,0);candy.localScale=new Vector3(.85f,.85f,.2f);
            var swirl=Shape("Candy pearl center",PrimitiveType.Sphere,Color.Lerp(color,Color.white,.7f));swirl.SetParent(root,false);swirl.localPosition=new Vector3(0,2.1f,.11f);swirl.localScale=new Vector3(.38f,.38f,.06f);return root;
        }
        Transform Shape(string name,PrimitiveType type,Color color)
        {
            var mesh=Resources.GetBuiltinResource<Mesh>(type+".fbx");
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.85f);materials.Add(material);var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;return go.transform;
        }
        void OnDestroy(){foreach(var m in materials)if(m)Destroy(m);}
    }
}
