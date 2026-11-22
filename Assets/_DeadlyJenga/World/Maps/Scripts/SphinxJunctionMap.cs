using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class SphinxJunctionMap : MonoBehaviour, IWorldQuery, ISyncedWorld, IMapFootprint, IRebuildable, ITraversalCollisionWorld
    {
        public const string Signature = "sphinx-junction-1";
        public Shader finishShader;
        Material finish;
        Transform built;
        JunctionModel model;
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
        public float WaterHeight(Vector3 p) => transform.position.y;
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
            Island(); Sphinx(); Saloon(); Pyramid(); Railway();
            Physics.SyncTransforms();
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
            var physics = gameObject.scene.GetPhysicsScene();
            var p = Center - Vector3.up*3.95f;
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
                if (Mathf.Abs(x)==5 && (z==-2 || z==4)) continue;
                Begin("Driven sandstone pier "+x+","+z,x*6,0,z*6);
                Box(0,-.48f,0,2.9f,3.96f,2.9f,Shade);
                Box(0,.67f,0,2.94f,.44f,2.94f,Teal,false);
                Box(0,1.26f,0,3.35f,.48f,3.35f,Sand,false);
                End(true);
                Begin("Plaza paving "+x+","+z,x*6,2,z*6);
                Box(0,0,0,5.99f,1,5.99f,Shade);
                for(int row=0;row<3;row++) for(int col=0;col<3;col++)
                    Box((col-1)*2,.48f,(row-1)*2,1.985f,.09f,1.985f,(row+col+x+z)%3==0?Honey:Sand,false);
                var deck=End();
                if (z==-2) plazas.Add(deck);
            }
            for(int i=-5;i<=5;i++)
            {
                float x=i*5.4f, z=-18.5f+.045f*i*i;
                Begin("Causeway pile "+i,x,0,z);
                Box(0,-.8f,0,2.3f,4,2.4f,Shade);
                Box(0,.38f,0,2.36f,.37f,2.46f,Teal,false);
                End(true);
                Begin("Causeway span "+i,x,1.53f,z,-Mathf.Atan(.09f*i/5.4f)*Mathf.Rad2Deg);
                Box(0,0,0,5.5f,.66f,3.8f,Honey);
                for(int k=-2;k<=2;k++) Box(k*1.05f,.52f,-1.73f,1.02f,.38f,.37f,Sand);
                if(i%2==0) Lamp(0,.74f,-1.73f);
                End();
            }
            for(int x=-18;x<=18;x+=18) Stairs("Plaza approach",x,1.86f,-15.87f,3,.64f,2);
        }
    }
}
