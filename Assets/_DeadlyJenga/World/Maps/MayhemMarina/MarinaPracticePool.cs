using System.Collections;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.World.Maps
{
    public sealed class MarinaPracticePool : MonoBehaviour,IRebuildable
    {
        readonly List<MarinaPracticeBlock> blocks=new();
        readonly List<Material> materials=new();
        Mesh mesh;
        ILobbyControls lobby;
        bool shared,started;
        float nextCheck,resetAt;
        float nextHands,lastHands;
        bool handsReady;
        Vector3 leftHand,rightHand;
        public IReadOnlyList<MarinaPracticeBlock> Blocks=>blocks;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.LobbyStarted+=Attach;
        static void Attach(GameObject root)
        {
            var world=root.GetComponent<IWorldQuery>();
            if(world!=null&&Mathf.Approximately(world.Radius,37))BuildFor(root);
        }
        public static MarinaPracticePool BuildFor(GameObject root)
        {
            var found=root.GetComponentInChildren<MarinaPracticePool>(true);if(found)return found;
            foreach(var old in root.GetComponentsInChildren<LobbyMilkshake>(true))old.gameObject.SetActive(false);
            var go=new GameObject("Marina practice block pool");go.transform.SetParent(root.transform,false);
            var pool=go.AddComponent<MarinaPracticePool>();pool.lobby=root.GetComponent<ILobbyControls>();pool.Build();return pool;
        }
        void Build()
        {
            mesh=RoundedBox();
            foreach(var color in new[]{new Color(.95f,.025f,.07f),new Color(.025f,.9f,.18f),new Color(.59f,.035f,.95f)})
            {
                var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.82f);materials.Add(material);
            }
            for(int layer=0;layer<6;layer++)for(int plank=0;plank<3;plank++)
            {
                bool turn=layer%2==1;var offset=turn?new Vector3(0,0,(plank-1)*.57f):new Vector3((plank-1)*.57f,0,0);
                var go=new GameObject("Practice block "+blocks.Count,typeof(BoxCollider),typeof(Rigidbody));go.transform.SetParent(transform,false);go.transform.localPosition=new Vector3(17,.55f+layer*.39f,-8)+offset;go.transform.localRotation=Quaternion.Euler(0,turn?90:0,0);
                go.layer=Layers.Surface;var box=go.GetComponent<BoxCollider>();box.size=new Vector3(.54f,.38f,1.7f);box.includeLayers=(1<<Layers.Surface)|(1<<Layers.Block)|(1<<Layers.Survivor)|1;box.layerOverridePriority=100;
                var body=go.GetComponent<Rigidbody>();body.mass=1.7f;body.linearDamping=.18f;body.angularDamping=.28f;body.solverIterations=6;body.solverVelocityIterations=2;body.interpolation=RigidbodyInterpolation.Interpolate;
                var visual=new GameObject("Visual",typeof(MeshFilter),typeof(MeshRenderer));visual.transform.SetParent(go.transform,false);visual.transform.localScale=box.size;visual.GetComponent<MeshFilter>().sharedMesh=mesh;visual.GetComponent<MeshRenderer>().sharedMaterial=materials[layer%3];
                var block=go.AddComponent<MarinaPracticeBlock>();block.Initialize(this,body,visual.transform);blocks.Add(block);
            }
            shared=lobby?.Connected==true;Rebuild();started=true;
        }
        public bool CanPush=>started&&!shared;
        public void Rebuild()
        {foreach(var block in blocks)if(block)block.ResetPose(shared);resetAt=0;}
        void Update()
        {
            TouchHands();
            if(Time.unscaledTime<nextCheck)return;nextCheck=Time.unscaledTime+.25f;
            var player=(lobby as ISessionInfo)?.Player;bool connected=lobby?.Connected==true||player!=null&&!player.gameObject.activeInHierarchy;if(connected!=shared){shared=connected;Rebuild();}
            if(shared)return;
            bool moved=false;foreach(var block in blocks)
            {
                if(!block)continue;var local=transform.InverseTransformPoint(block.transform.position);
                if(local.y<-.7f||Vector2.Distance(new Vector2(local.x,local.z),new Vector2(17,-8))>6.5f){Rebuild();return;}
                moved|=block.Moved;
            }
            if(moved&&resetAt==0)resetAt=Time.unscaledTime+25;
            if(resetAt>0&&Time.unscaledTime>=resetAt)Rebuild();
        }
        void TouchHands()
        {
            if(Time.unscaledTime<nextHands)return;nextHands=Time.unscaledTime+.05f;
            var player=(lobby as ISessionInfo)?.Player;
            if(!CanPush||player==null||!player.gameObject.activeInHierarchy||player.InputSuspended||Vector3.Distance(player.transform.position,transform.TransformPoint(new Vector3(17,1,-8)))>6)
            {handsReady=false;return;}
            var actor=player.gameObject.GetComponent<IModeActor>();if(actor==null)return;
            var left=actor.HandPose(true).position;var right=actor.HandPose(false).position;
            if(handsReady)
            {
                float dt=Mathf.Max(.02f,Time.unscaledTime-lastHands);
                Sweep(leftHand,left,dt,player.gameObject);Sweep(rightHand,right,dt,player.gameObject);
            }
            leftHand=left;rightHand=right;lastHands=Time.unscaledTime;handsReady=true;
        }
        void Sweep(Vector3 from,Vector3 to,float dt,GameObject source)
        {
            var delta=to-from;float speed=delta.magnitude/dt;if(speed<.45f||speed>10)return;
            foreach(var block in blocks)
            {
                if((block.transform.position-to).sqrMagnitude>2)continue;
                float along=delta.sqrMagnitude>.00001f?Mathf.Clamp01(Vector3.Dot(block.transform.position-from,delta)/delta.sqrMagnitude):0;
                var point=from+delta*along;
                if((block.Shape.ClosestPoint(point)-point).sqrMagnitude<.035f)block.Touch(delta.normalized*Mathf.Clamp(speed*1.6f,1.4f,6),source);
            }
        }
        void OnDestroy(){if(mesh)Destroy(mesh);foreach(var material in materials)if(material)Destroy(material);}
        static Mesh RoundedBox()
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var indices=new List<int>();
            Vector3[] axes={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            foreach(var normal in axes)
            {
                var u=Vector3.Cross(normal,Mathf.Abs(normal.y)>.5f?Vector3.forward:Vector3.up).normalized;var v=Vector3.Cross(normal,u);int start=vertices.Count;
                for(int y=0;y<5;y++)for(int x=0;x<5;x++)
                {
                    var p=normal*.5f+u*(x*.25f-.5f)+v*(y*.25f-.5f);var inner=new Vector3(Mathf.Clamp(p.x,-.42f,.42f),Mathf.Clamp(p.y,-.42f,.42f),Mathf.Clamp(p.z,-.42f,.42f));var n=(p-inner).normalized;vertices.Add(inner+n*.08f);normals.Add(n);
                }
                for(int y=0;y<4;y++)for(int x=0;x<4;x++){int a=start+y*5+x;indices.AddRange(new[]{a,a+1,a+5,a+1,a+6,a+5});}
            }
            var mesh=new Mesh{name="Rounded practice block"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();return mesh;
        }
    }
    public sealed class MarinaPracticeBlock : MonoBehaviour,IPushable,IClimbable
    {
        MarinaPracticePool pool;Rigidbody body;Transform visual;Vector3 home;Quaternion rotation;
        float nextTouch;
        public Collider Shape {get;private set;}
        public bool CanClimb=>isActiveAndEnabled;
        public float Grip=>1;
        public Transform Anchor=>transform;
        public Rigidbody Body=>body;
        public Transform Visual=>visual;
        public bool Moved=>(transform.localPosition-home).sqrMagnitude>.04f||Quaternion.Angle(transform.localRotation,rotation)>15;
        public void Initialize(MarinaPracticePool owner,Rigidbody rigidbody,Transform model)
        {pool=owner;body=rigidbody;visual=model;home=transform.localPosition;rotation=transform.localRotation;Shape=GetComponent<Collider>();Climbables.Register(Shape,this);}
        public void Touch(Vector3 velocity,GameObject source)
        {if(Time.unscaledTime<nextTouch)return;nextTouch=Time.unscaledTime+.25f;Push(velocity,source);}
        public void Push(Vector3 force,GameObject source)
        {if(!pool.CanPush)return;body.AddForce(Vector3.ClampMagnitude(force,10),ForceMode.VelocityChange);GameEvents.RaisePushed(gameObject,force);}
        public void ResetPose(bool frozen)
        {body.isKinematic=true;transform.localPosition=home;transform.localRotation=rotation;body.position=transform.position;body.rotation=transform.rotation;body.isKinematic=frozen;if(!frozen){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.Sleep();}}
        void OnDestroy(){Climbables.Unregister(GetComponent<Collider>());}
    }
}
