using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Wildlife;
using UnityEngine;

namespace DeadlyJenga.CastleBots
{
    // Shared by the authoritative bot and its remote visual copy. No extra physics bodies.
    [DefaultExecutionOrder(700)]
    public sealed class CandyMayhemEnemyVisual : MonoBehaviour
    {
        static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        static readonly Dictionary<Color,Material> materials=new Dictionary<Color,Material>();
        Transform model,leftArm,rightArm,leftLeg,rightLeg;
        Renderer[] originals;
        bool[] originalHidden;
        IEnemyActor enemy;
        IModeActor actor;
        Vector3 previous;
        float phase,speed,attack;
        public Transform Model=>model;
        void LateUpdate()
        {
            enemy??=GetComponent<IEnemyActor>();actor??=GetComponent<IModeActor>();
            bool supported=enemy?.IsEnemy==true&&(enemy.ArchetypeId=="longhand"||enemy.ArchetypeId=="gum-brute");
            if(!supported){if(model)model.gameObject.SetActive(false);Restore();return;}
            var rig=GetComponentInChildren<CandyPlayerRig>();if(!rig)return;
            if(!model)Build(rig.transform,enemy.ArchetypeId=="gum-brute");
            foreach(var part in originals)if(part)part.forceRenderingOff=true;
            model.gameObject.SetActive(true);
            var parentScale=model.parent.lossyScale;model.localScale=new Vector3(1/Mathf.Max(.001f,Mathf.Abs(parentScale.x)),1/Mathf.Max(.001f,Mathf.Abs(parentScale.y)),1/Mathf.Max(.001f,Mathf.Abs(parentScale.z)));
            float dt=Mathf.Max(.001f,Time.deltaTime);
            float target=Vector3.ProjectOnPlane(transform.position-previous,Vector3.up).magnitude/dt;
            speed=Mathf.Lerp(speed,Mathf.Min(target,8),1-Mathf.Exp(-dt*12));previous=transform.position;
            phase+=dt*Mathf.Min(speed,4)*2.4f;
            bool brute=enemy.ArchetypeId=="gum-brute";
            model.SetPositionAndRotation(transform.position,Quaternion.Euler(0,actor?.ViewPose.rotation.eulerAngles.y??transform.eulerAngles.y,0));
            float stride=Mathf.Sin(phase)*Mathf.Clamp01(speed)* (brute?12:22);
            attack=Mathf.MoveTowards(attack,enemy is IEnemyPose pose&&pose.PreparingMelee?1:0,dt*5);
            float strike=attack;
            leftArm.localScale=rightArm.localScale=new Vector3(1,brute?1:1+strike*.6f,1);
            leftArm.localRotation=Quaternion.Euler(strike*-65+stride,0,brute?-12:8);
            rightArm.localRotation=Quaternion.Euler(strike*-85-stride,0,brute?12:-8);
            leftLeg.localRotation=Quaternion.Euler(-stride,0,0);rightLeg.localRotation=Quaternion.Euler(stride,0,0);
        }
        void Build(Transform rig,bool brute)
        {
            originals=rig.GetComponentsInChildren<Renderer>(true);originalHidden=new bool[originals.Length];
            for(int i=0;i<originals.Length;i++)originalHidden[i]=originals[i].forceRenderingOff;
            model=new GameObject(brute?"Visual - Gum Brute":"Visual - Longhand").transform;model.SetParent(rig,false);
            previous=transform.position;
            Color body=brute?new Color(.48f,.86f,.07f):new Color(.25f,.055f,.49f);
            Color accent=brute?new Color(.015f,.24f,.27f):new Color(1,.32f,.035f);
            Part("torso",model,Vector3.zero,body,brute,m=>{
                m.Sphere(new Vector3(0,brute?1.14f:1.38f,0),brute?new Vector3(.61f,.55f,.38f):new Vector3(.23f,.43f,.18f));
                m.Sphere(new Vector3(0,brute?1.58f:1.98f,.08f),brute?new Vector3(.28f,.29f,.27f):new Vector3(.19f,.22f,.18f));
            });
            Part("eyes",model,Vector3.zero,new Color(.012f,.009f,.023f),brute,m=>{
                for(int s=-1;s<=1;s+=2)m.Sphere(new Vector3(s*(brute?.075f:.055f),brute?1.63f:2.01f,brute?.34f:.251f),new Vector3(.027f,.031f,.018f));
            });
            leftArm=Arm(-1);rightArm=Arm(1);leftLeg=Leg(-1);rightLeg=Leg(1);
            Transform Arm(int side)
            {
                var pivot=new GameObject("Arm pivot").transform;pivot.SetParent(model,false);pivot.localPosition=new Vector3(side*(brute?.51f:.24f),brute?1.38f:1.63f,0);
                Part("arm",pivot,Vector3.zero,body,brute,m=>{m.Sphere(new Vector3(0,-.39f,.04f),brute?new Vector3(.28f,.47f,.27f):new Vector3(.09f,.55f,.09f));});
                Part("hand",pivot,Vector3.zero,accent,brute,m=>{
                    float y=brute?-.94f:-1.21f;
                    m.Sphere(new Vector3(0,y,.08f),brute?new Vector3(.31f,.3f,.32f):new Vector3(.19f,.21f,.11f));
                    for(int i=-1;i<=1;i++)m.Sphere(new Vector3(i*(brute?.15f:.115f),y-(brute?.14f:.2f),.16f),brute?new Vector3(.105f,.15f,.21f):new Vector3(.06f,.19f,.07f));
                    m.Sphere(new Vector3(.2f,y,.18f),new Vector3(.09f,.13f,.085f));
                });return pivot;
            }
            Transform Leg(int side)
            {
                var pivot=new GameObject("Leg pivot").transform;pivot.SetParent(model,false);pivot.localPosition=new Vector3(side*(brute?.27f:.13f),brute?.66f:.98f,0);
                Part("leg",pivot,Vector3.zero,body,brute,m=>m.Sphere(new Vector3(0,brute?-.23f:-.43f,0),brute?new Vector3(.22f,.34f,.22f):new Vector3(.1f,.48f,.1f)));
                Part("foot",pivot,Vector3.zero,brute?accent:body,brute,m=>m.Sphere(new Vector3(0,brute?-.53f:-.86f,.12f),brute?new Vector3(.26f,.14f,.35f):new Vector3(.13f,.12f,.22f)));return pivot;
            }
        }
        static Transform Part(string name,Transform parent,Vector3 at,Color color,bool brute,System.Action<AnimalMesh> make)
        {
            string key=(brute?"brute-":"longhand-")+name;
            if(!meshes.TryGetValue(key,out var mesh)||!mesh){var builder=new AnimalMesh();make(builder);meshes[key]=mesh=builder.Mesh(key);}
            if(!materials.TryGetValue(color,out var material)||!material){
                material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Mayhem glossy candy",color=color,enableInstancing=true};
                material.SetFloat("_Smoothness",.82f);material.SetFloat("_Metallic",.07f);materials[color]=material;
            }
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=at;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go.transform;
        }
        void Restore(){if(originals==null)return;for(int i=0;i<originals.Length;i++)if(originals[i])originals[i].forceRenderingOff=originalHidden[i];}
        void OnDisable(){Restore();if(model)model.gameObject.SetActive(false);}
        void OnDestroy(){Restore();if(model)Destroy(model.gameObject);}
    }
}
