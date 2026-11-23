using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class TidalCircuitMap : MonoBehaviour, IWorldQuery, ISyncedWorld, IRebuildable, IMapFootprint, ITraversalCollisionWorld
    {
        public Material sand;
        public PhysicsMaterial friction;
        readonly List<Rigidbody> bodies=new();
        readonly List<TidalCircuitPiece> pieces=new();
        readonly List<Vector4> shores=new();
        readonly HashSet<Rigidbody> tilters=new();
        Material[] palette;
        Transform generated;
        Mesh shoreMesh;
        bool replica, collisionIgnored;
        int spawn;
        public int Revision { get; private set; }
        public Transform Root => transform;
        public IReadOnlyList<Rigidbody> Blocks => bodies;
        public Vector3 Center => transform.position;
        public float Radius => 44;
        public float TopHeight => Center.y+8.4f;
        public float StepHeight => .4f;
        public Bounds PlayableBounds => new Bounds(Center+Vector3.up*3,new Vector3(80,12,44));
        public bool UsesOriginalColliders => true;
        public float FootprintHalfSize => 40;
        public int FootprintVersion => 14001;
        public float WaterHeight(Vector3 point) => Center.y-.32f;
        public bool IsSheltered(Vector3 point) => gameObject.scene.GetPhysicsScene().Raycast(point+Vector3.up*.1f,Vector3.up,out _,16,1<<Layers.Block,QueryTriggerInteraction.Ignore);
        internal bool Authoritative => !replica && Services.Rewind?.Rewinding!=true && !(Services.Session is IWorldAuthority a && !a.HasWorldAuthority);
        public void GetGroundFootprints(List<Footprint> result)
        {
            foreach(var s in shores) result.Add(new Footprint{center=new Vector2(s.x,s.y),halfSize=new Vector2(s.z,s.w),rotation=Quaternion.identity});
        }
        void OnEnable()
        {
            Services.World=this;
            collisionIgnored=Physics.GetIgnoreLayerCollision(Layers.Survivor,Layers.Block);
            Physics.IgnoreLayerCollision(Layers.Survivor,Layers.Block,false);
        }
        void OnDisable()
        {
            Physics.IgnoreLayerCollision(Layers.Survivor,Layers.Block,collisionIgnored);
            if(ReferenceEquals(Services.World,this))Services.World=null;
        }
        void Start(){if(!generated)Rebuild();}
        void OnDestroy()
        {
            if(shoreMesh)Destroy(shoreMesh);
            if(palette!=null)foreach(var mat in palette)Destroy(mat);
        }
        public void Rebuild()
        {
            if(generated){generated.gameObject.SetActive(false);Destroy(generated.gameObject);}
            if(shoreMesh)Destroy(shoreMesh);
            generated=new GameObject("Tidal Circuit geometry").transform;
            generated.SetParent(transform,false);
            if(palette==null)
            {
                var colors=new[]{new Color(.94f,.11f,.14f),new Color(.12f,.76f,.21f),new Color(.57f,.16f,.95f),new Color(1,.77f,.035f)};
                palette=new Material[colors.Length];
                for(int i=0;i<colors.Length;i++)
                {
                    var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=colors[i],enableInstancing=true};
                    m.SetFloat("_Smoothness",.54f);
                    palette[i]=m;
                }
            }
            pieces.Clear();bodies.Clear();tilters.Clear();shores.Clear();spawn=0;
            BuildCircuit();
            BuildBeach();
            Physics.SyncTransforms();
            Revision++;
        }
        void BuildBeach()
        {
            const int nx=161,nz=93;
            var vertices=new Vector3[nx*nz];
            var uv=new Vector2[vertices.Length];
            var triangles=new List<int>();
            for(int j=0;j<nz;j++)
            for(int i=0;i<nx;i++)
            {
                float x=(i-(nx-1)*.5f)*.52f,z=(j-(nz-1)*.5f)*.52f;
                float distance=100;
                foreach(var s in shores)
                {
                    float dx=(x-s.x)/s.z,dz=(z-s.y)/s.w;
                    // Rounded rectangular lobes have flat bearing seats and narrow sloping shores.
                    float d=Mathf.Pow(dx*dx*dx*dx+dz*dz*dz*dz,.25f);
                    distance=Mathf.Min(distance,(d-1)*Mathf.Min(s.z,s.w));
                }
                float height=-Mathf.Clamp(distance+.13f*Mathf.Sin(x*1.3f+z*.6f),0,3)*.85f;
                int index=j*nx+i;
                vertices[index]=V(x,height,z);uv[index]=new Vector2(x,z)*.2f;
                if(i==0 || j==0)continue;
                int a=index-nx-1;
                triangles.Add(a);triangles.Add(index-1);triangles.Add(index);
                triangles.Add(a);triangles.Add(index);triangles.Add(a+1);
            }
            shoreMesh=new Mesh{name="Tidal sand lobes"};
            shoreMesh.vertices=vertices;shoreMesh.uv=uv;shoreMesh.SetTriangles(triangles,0);shoreMesh.RecalculateNormals();
            var beach=new GameObject("Sand lobes and submerged channels");
            beach.transform.SetParent(generated,false);
            beach.AddComponent<MeshCollider>().sharedMesh=shoreMesh;
            beach.AddComponent<MeshFilter>().sharedMesh=shoreMesh;
            if(sand)beach.AddComponent<MeshRenderer>().sharedMaterial=sand;
        }
        public Vector3 SafeSpawnPoint()
        {
            // The beach remains after every removable deck has fallen.
            var physics=gameObject.scene.GetPhysicsScene();
            for(int n=0;n<15;n++)
            {
                int i=spawn++%15;
                var p=transform.TransformPoint(V((i%5-2)*13,12,(i/5-1)*16));
                if(physics.Raycast(p,Vector3.down,out var hit,14,1|(1<<Layers.Block),QueryTriggerInteraction.Ignore) && hit.normal.y>.8f && hit.point.y>WaterHeight(p))
                    return hit.point+Vector3.up*.08f;
            }
            return transform.TransformPoint(V(0,.08f,-3));
        }
        void FixedUpdate()
        {
            if(!Authoritative)return;
            for(int i=Tracked.Movers.Count-1;i>=0;i--)
            {
                if(!(Tracked.Movers[i] is IModeActor actor) || !actor.Alive)continue;
                var p=actor.transform.position;
                if(p.y<WaterHeight(p)-.35f)actor.gameObject.GetComponent<IDamageable>()?.Eliminate(gameObject);
                else if(actor.Grounded && actor.StandingBody && tilters.Contains(actor.StandingBody))
                    actor.StandingBody.AddForceAtPosition(Vector3.down*100,p,ForceMode.Force);
            }
        }
        public string SaveLayout() => "tidal-circuit-v1";
        public bool LoadLayout(string layout,int count,Pose origin,Vector3 scale)
        {
            if(layout!=SaveLayout() || scale!=Vector3.one || !float.IsFinite(origin.position.sqrMagnitude) ||
                !float.IsFinite(Quaternion.Dot(origin.rotation,origin.rotation)) ||
                Mathf.Abs(Quaternion.Dot(origin.rotation,origin.rotation)-1)>.001f || Quaternion.Angle(origin.rotation,Quaternion.identity)>.01f)return false;
            if(!generated)Rebuild();
            if(count!=bodies.Count)return false;
            transform.SetPositionAndRotation(origin.position,origin.rotation);
            Rebuild();return true;
        }
        public void SetReplica(bool value)
        {
            if(replica==value)return;
            replica=value;
            foreach(var p in pieces)
            {
                if(!value && !p.Loose && (transform.InverseTransformPoint(p.Body.position)-p.Rest).sqrMagnitude>.16f)p.Unseat();
                p.Body.isKinematic=value || !(p.Loose || p.Rotor);
                if(p.Hinge && !p.Hinge.useLimits)p.Hinge.useMotor=!value;
                if(value)p.Release();
            }
        }
    }
}
