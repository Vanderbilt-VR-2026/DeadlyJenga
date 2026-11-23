using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class SphinxJunctionMap : MonoBehaviour, IWorldQuery, ISyncedWorld, IMapFootprint, IRebuildable, ITraversalCollisionWorld
    {
        public const string Signature = "sphinx-junction-7";
        public Shader finishShader;
        Material finish;
        Transform built;
        JunctionModel model;
        JunctionStructure structure;
        GameObject unit;
        readonly List<Rigidbody> bodies = new();
        readonly List<Rigidbody> plazas = new();
        readonly Collider[] victims = new Collider[128];
        bool replica;
        float cleanup;
        public Transform Root => transform;
        public int Revision { get; private set; }
        public IReadOnlyList<Rigidbody> Blocks => bodies;
        public Vector3 Center => transform.position;
        public float Radius => 43 * transform.lossyScale.x;
        public float TopHeight => transform.TransformPoint(0,24,0).y;
        public float StepHeight => 1;
        public Bounds PlayableBounds => new(transform.TransformPoint(0,10,4), Vector3.one * (Radius*2));
        public int FootprintVersion => Revision;
        public float FootprintHalfSize => Radius;
        public bool UsesOriginalColliders => true;
        public float WaterHeight(Vector3 p) => transform.TransformPoint(0,-1,0).y;
        public bool IsSheltered(Vector3 p) => gameObject.scene.GetPhysicsScene().Raycast(p+Vector3.up*.15f,Vector3.up,out _,50,1<<Layers.Block,QueryTriggerInteraction.Ignore);
        public void GetGroundFootprints(List<Footprint> into) { }
        public Vector3 SafeSpawnPoint()
        {
            foreach (var slab in plazas)
            {
                if (!slab || !slab.gameObject.activeSelf || slab.transform.up.y < .9f) continue;
                var p = slab.position + Vector3.up * .61f;
                if (p.y > WaterHeight(p)+.5f && !gameObject.scene.GetPhysicsScene().Raycast(p,Vector3.up,out _,1.4f,1<<Layers.Block))
                    return p;
            }
            return transform.TransformPoint(0,3,-8);
        }
        void OnEnable() => Services.World = this;
        void OnDisable() { if (ReferenceEquals(Services.World,this)) Services.World = null; }
        void Start() { if (!built) Rebuild(); }
        void OnDestroy() { if (finish) Destroy(finish); }
        public void Rebuild()
        {
            if (built) { built.gameObject.SetActive(false); Destroy(built.gameObject); }
            built = new GameObject("Sphinx Junction").transform;
            built.SetParent(transform,false);
            if (!finish) finish = new Material(finishShader);
            bodies.Clear(); plazas.Clear();
            authoredSeams.Clear();
            bakedMembers.Clear();
            structure = JunctionStructure.Load();
            Island();
            if (structure == null) Sphinx(); else BuildBakedSphinx();
            Saloon(); Pyramid();
            if (structure != null) BuildBakedLandmarkAdditions();
            Railway();
            Physics.SyncTransforms();
            if (structure != null) ExcludeAuthoredOverlaps();
            SetReplica(replica);
            Revision++;
        }
        public string SaveLayout() => Signature;
        public bool LoadLayout(string layout,int expectedBlocks,Pose origin,Vector3 scale)
        {
            if (layout != Signature || !float.IsFinite(origin.position.sqrMagnitude) ||
                !(Mathf.Abs(Quaternion.Dot(origin.rotation,origin.rotation)-1)<.001f) ||
                !(scale.x>0 && float.IsFinite(scale.sqrMagnitude) && Mathf.Abs(scale.x-scale.y)<.001f && Mathf.Abs(scale.z-scale.x)<.001f)) return false;
            if (!built) Rebuild();
            if (expectedBlocks != bodies.Count) return false;
            transform.SetPositionAndRotation(origin.position,origin.rotation);
            transform.localScale = scale;
            Rebuild();
            return true;
        }
        public void SetReplica(bool value)
        {
            if (replica && !value) RecoverTrain();
            replica = value;
            foreach (var rb in bodies)
            {
                rb.GetComponent<JunctionMember>().Replica = value;
                rb.isKinematic = value || OnRails(rb);
            }
        }
        void FixedUpdate()
        {
            if (!built || replica || Services.Session is IWorldAuthority a && !a.HasWorldAuthority) return;
            DriveTrain();
            if (Time.fixedTime < cleanup) return;
            cleanup = Time.fixedTime + .18f;
            RestoreSeparatedContacts();
            var physics = gameObject.scene.GetPhysicsScene();
            var p = new Vector3(Center.x,WaterHeight(Center)-3.95f,Center.z);
            int hits = physics.OverlapBox(p,new Vector3(Radius+12,4,Radius+12),victims,Quaternion.identity,~((1<<Layers.Block)|(1<<Layers.Surface)),QueryTriggerInteraction.Ignore);
            for(int i=0;i<hits;i++) victims[i].GetComponentInParent<IDamageable>()?.Eliminate(gameObject);
            foreach (var rb in bodies)
                if (rb.gameObject.activeSelf && rb.position.y < Center.y-14) rb.gameObject.SetActive(false);
        }
        static readonly Color Sand = Hex(0xe6a558), Honey = Hex(0xf3bd69), Shade = Hex(0xbd7542), Teal = Hex(0x218e91),
            Gold = Hex(0xf5b344), Wood = Hex(0xac573c), Dark = Hex(0x553834), Red = Hex(0xab4532),
            Iron = Hex(0x34404a), Amber = Hex(0xffdf73);
        static Color Hex(int rgb) => new Color32((byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb,255);
        void Begin(string name,float x,float y,float z,float yaw=0)
        {
            unit = new GameObject(name);
            unit.layer = Layers.Block;
            unit.transform.SetParent(built,false);
            unit.transform.SetLocalPositionAndRotation(new Vector3(x,y,z),Quaternion.Euler(0,yaw,0));
            model = new JunctionModel(unit);
        }
        Rigidbody End(bool pile=false)
        {
            if (structure != null) structure.AddTo(unit.name, model);
            var rb = model.Finish(finish);
            bodies.Add(rb);
            if (pile)
            {
                // The visible pile is driven below the sand bed. A pull can shear its embedment.
                var joint = unit.AddComponent<FixedJoint>();
                joint.breakForce = rb.mass * 1500;
                joint.breakTorque = rb.mass * 1500;
            }
            return rb;
        }
        static void Fasten(Rigidbody member,Rigidbody bearing)
        {
            var pin=member.gameObject.AddComponent<FixedJoint>();
            pin.connectedBody=bearing;
            pin.breakForce=member.mass*190;
            pin.breakTorque=member.mass*260;
        }
        void Box(float x,float y,float z,float w,float h,float d,Color c,bool solid=true)
            => model.Shape(new(x,y,z),new(w,h,d),c,solid:solid);
        void Round(float x,float y,float z,float w,float h,float d,Color c,float top=1,int sides=12,bool solid=false)
            => model.Shape(new(x,y,z),new(w,h,d),c,sides,top,solid:solid);
        void Beam(Vector3 a,Vector3 b,float w,float d,Color c,bool solid=false)
            => model.Shape((a+b)*.5f,new(w,(b-a).magnitude,d),c,rotation:Quaternion.FromToRotation(Vector3.up,b-a),solid:solid);
        void Lamp(float x,float y,float z)
        {
            Round(x,y,z,.45f,.13f,.45f,Dark);
            Round(x,y+.25f,z,.27f,.42f,.27f,Amber,.86f);
            Round(x,y+.5f,z,.42f,.12f,.42f,Gold,.4f);
        }
        void Stairs(string name,float x,float bottom,float z,float width,float rise,float run,float yaw=0)
        {
            Begin(name,x,bottom,z,yaw);
            int steps = Mathf.CeilToInt(rise/.22f);
            for(int i=0;i<steps;i++) {
                float h=rise*(i+1)/steps;
                Box(0,h*.5f,run*((i+.5f)/steps-.5f),width,h,run/steps,Honey);
            }
            End();
        }
        void Island()
        {
            for(int z=-2;z<=4;z++) for(int x=-5;x<=5;x++)
            {
                if (Mathf.Abs(x)>=4 && z==-2) continue;
                Begin("Driven sandstone pier "+x+","+z,x*6,0,z*6);
                Box(0,-.84f,0,2.9f,3.24f,2.9f,Shade);
                Box(0,.67f,0,2.94f,.44f,2.94f,Teal,false);
                model.Shape(new(0,1.14f,0),new(3.0f,.72f,3.0f),Sand,top:1.84f);
                var pier=End(true);
                Begin("Plaza paving "+x+","+z,x*6,2,z*6);
                Box(0,0,0,5.99f,1,5.99f,Shade);
                for(int row=0;row<3;row++) for(int col=0;col<3;col++)
                    Box((col-1)*2,.48f,(row-1)*2,1.985f,.09f,1.985f,(row+col+x+z)%3==0?Honey:Sand,false);
                if (x==0 && z==-1)
                {
                    Round(0,.54f,0,3.5f,.035f,3.5f,Teal,1,24);
                    Round(0,.566f,0,2.62f,.028f,2.62f,Gold,1,24);
                    Round(0,.589f,0,1.23f,.025f,1.23f,Teal,1,16);
                }
                // The central keystone is dowelled to its flared capital below the chest opening.
                if(x==0 && z==0)
                    foreach(int side in new[]{-1,1}) Round(side*1.65f,.536f,0,.19f,.035f,.19f,Gold);
                var deck=End();
                if(x==0 && z==0) Fasten(deck,pier);
                if (z==-2) plazas.Add(deck);
            }
            for(int i=-5;i<=5;i++)
            {
                float x=i*5.4f, z=-22.7f+.30f*i*i;
                Begin("Causeway pile "+i,x,0,z);
                Box(0,-.8f,0,2.3f,4,2.4f,Shade);
                Box(0,.38f,0,2.36f,.37f,2.46f,Teal,false);
                Box(0,.78f,0,2.92f,.34f,2.74f,Sand);
                Box(0,1.07f,0,3.62f,.26f,3.25f,Honey);
                End(true);
                Begin("Causeway span "+i,x,1.53f,z+.075f,90-Mathf.Atan(.60f*i/5.4f)*Mathf.Rad2Deg);
                float chord=Mathf.Sqrt(5.4f*5.4f+.36f*i*i);
                model.SetTrackMiters(chord,-.05f,.05f);
                Box(0,0,0,3.8f,.66f,chord-.04f,Honey);
                for(int k=-2;k<=2;k++) Box(1.73f,.52f,k*chord/5,.37f,.38f,chord/5-.06f,Sand);
                if(i%2==0)
                {
                    Box(1.57f,.66f,0,.74f,.67f,.94f,Sand);
                    Box(1.57f,1.035f,0,.90f,.12f,1.08f,Honey);
                    Round(1.57f,1.23f,0,.48f,.28f,.48f,Dark,1.72f);
                    Round(1.57f,1.39f,0,.73f,.08f,.73f,Gold);
                    Round(1.57f,1.52f,0,.48f,.25f,.48f,Amber,.30f,9);
                }
                End();
            }
            foreach(int x in new[]{-27,-18,0,18,27})
            {
                float edge=Mathf.Abs(x)==27?-9:-15;
                float i=x/5.4f;
                float foot=-22.7f+.30f*i*i+1.53f;
                float run=edge-foot;
                float mid=(edge+foot)*.5f;
                Begin("Approach stair driven pier",x,0,edge-.78f);
                Box(0,-.22f,0,1.85f,4.16f,Mathf.Min(run,1.45f),Shade);
                End(true);
                Stairs("Plaza approach",x,1.86f,mid,3,.64f,run);
            }
            for(int side=-1;side<=1;side+=2)
            {
                bool terrace=side==1;
                float x=terrace?15:-13;
                float halfBay=terrace?3.15f:2.1f;
                Begin("Sandstone market arcade "+side,x,2.5f,-6.4f);
                foreach(int a in new[]{-1,1}) foreach(int b in new[]{-1,1})
                {
                    Round(a*halfBay,1.15f,b*1.28f,.62f,2.3f,.62f,Honey,.86f,9,true);
                    Round(a*halfBay,1.94f,b*1.28f,.84f,.46f,.84f,Teal,1.12f,9);
                }
                End();
                Begin("Market arcade roof "+side,x,5.03f,-6.4f);
                float width=terrace?8.4f:5.45f;
                Box(0,0,0,width,.46f,3.45f,Honey);
                Box(0,-.06f,-1.73f,width-.15f,.17f,.14f,Teal,false);
                foreach(int end in new[]{-1,1}) Lamp(end*halfBay,.30f,-1.26f);
                if(terrace)
                {
                    for(int post=-2;post<=2;post++) Box(post*1.83f,.61f,-1.51f,.49f,.76f,.38f,Sand);
                    Box(0,.53f,-1.50f,7.6f,.21f,.22f,Teal);
                    Box(0,.88f,-1.50f,7.6f,.16f,.29f,Honey);
                }
                End();
                if(terrace) Stairs("East market terrace stairs",21.32f,2.5f,-6.4f,2.28f,2.76f,4.2f,-90);
                Begin("Potter's stall "+side,x,2.5f,-6.48f);
                foreach(int leg in new[]{-1,1}) Box(leg*.84f,.38f,0,.21f,.76f,.6f,Wood);
                Box(0,.84f,0,2.10f,.16f,.89f,Wood);
                for(int jar=-1;jar<=1;jar++)
                {
                    Round(jar*.64f,1.11f,0,.45f,.38f,.45f,jar==0?Teal:Red,1.22f);
                    Round(jar*.64f,1.42f,0,.54f,.26f,.54f,jar==0?Teal:Red,.52f);
                }
                End();
            }
        }
    }
}
