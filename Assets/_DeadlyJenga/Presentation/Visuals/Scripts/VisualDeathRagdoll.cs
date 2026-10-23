using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    // Cosmetic Verlet skeleton. It queries scenery but adds no colliders, bodies or forces.
    sealed class VisualDeathRagdoll
    {
        sealed class Joint
        {
            public Vector3 origin,position,previous,direction;
            public int parent=-1,child=-1;
            public float length;
            public Quaternion rotation=Quaternion.identity;
        }
        sealed class Skin
        {
            public Mesh mesh;
            public Transform transform;
            public Renderer renderer;
            public Vector3[] origin,normal,vertices,normals;
            public BoneWeight[] weights;
            public int[] bones;
            public int nearest;
        }
        readonly List<Joint> joints=new List<Joint>();
        readonly List<Skin> skins=new List<Skin>();
        readonly Dictionary<Transform,int> indices=new Dictionary<Transform,int>();
        readonly RaycastHit[] hits=new RaycastHit[32];
        readonly float radius;
        Transform support;
        Vector3 supportLocal,supportWorld;
        float quiet;
        public Bounds Bounds {get;private set;}
        public int ConstraintCount {get;private set;}
        public bool Settled=>support&&quiet>.25f;
        public float GroundHeight {get;private set;}

        public VisualDeathRagdoll(Transform visual,Bounds bounds)
        {
            Bounds=bounds;GroundHeight=bounds.min.y;radius=Mathf.Clamp(bounds.size.y*.055f,.025f,.12f);
            foreach(var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(!CandyMelt.SnapshotRendererVisible(skin,visual))continue;
                foreach(var bone in skin.bones)
                {
                    if(!bone||indices.ContainsKey(bone))continue;
                    indices[bone]=joints.Count;
                    var p=bone.position;
                    // A small sideways impulse starts a limp fall instead of a perfectly balanced statue.
                    var velocity=new Vector3(.35f,.03f,.22f)*Mathf.Max(.3f,bounds.size.y);
                    joints.Add(new Joint{origin=p,position=p,previous=p-velocity/30});
                }
            }
            var names=new Dictionary<string,int>(System.StringComparer.OrdinalIgnoreCase);
            foreach(var pair in indices)names[pair.Key.name]=pair.Value;
            // Procedural rigs may keep every bone directly under Visual for pose updates.
            // Use their art names to recover joint links, retaining real hierarchy for other rigs.
            if(names.TryGetValue("Torso",out int torsoIndex)&&indices.Count==44)
            {
                var torso=joints[torsoIndex];var offset=visual.up*bounds.size.y*.44f;
                torso.origin+=offset;torso.position+=offset;torso.previous+=offset;
            }
            foreach(var pair in indices)
            {
                var parent=pair.Key.parent;while(parent&&!indices.ContainsKey(parent))parent=parent.parent;
                var joint=joints[pair.Value];
                if(parent)joint.parent=indices[parent];
                else{var name=ParentName(pair.Key.name);if(name==null||!names.TryGetValue(name,out int parentIndex))continue;joint.parent=parentIndex;}
                if(joint.parent==pair.Value){joint.parent=-1;continue;}
                joint.length=Vector3.Distance(joint.origin,joints[joint.parent].origin);
                if(joint.length>.0001f)ConstraintCount++;
                var ancestor=joints[joint.parent];
                if(ancestor.child<0||joint.length>joints[ancestor.child].length)ancestor.child=pair.Value;
            }
            if(joints.Count==0)
                joints.Add(new Joint{origin=bounds.center,position=bounds.center,previous=bounds.center-new Vector3(.004f,0,.003f)});
            foreach(var joint in joints)
                joint.direction=joint.child>=0?joints[joint.child].origin-joint.origin:joint.parent>=0?joint.origin-joints[joint.parent].origin:Vector3.up;
        }
        static string ParentName(string name)
        {
            if(name=="Head")return "Torso";
            foreach(string side in new[]{"Left","Right"})
            {
                if(name==side+" upper arm"||name==side+" thigh")return "Torso";
                if(name==side+" forearm")return side+" upper arm";
                if(name==side+" palm")return side+" forearm";
                if(name==side+" shin")return side+" thigh";
                if(name==side+" foot")return side+" shin";
                if(name.StartsWith(side+" finger ",System.StringComparison.Ordinal))
                {
                    int split=name.LastIndexOf(" segment ",System.StringComparison.Ordinal);
                    if(split>=0&&int.TryParse(name.Substring(split+9),out int segment))
                        return segment==0?side+" palm":name.Substring(0,split+9)+(segment-1);
                }
            }
            return null;
        }
        public void Attach(Renderer source,Transform target,Mesh mesh)
        {
            var vertices=mesh.vertices;var normals=mesh.normals;
            var skin=new Skin{mesh=mesh,transform=target,renderer=target.GetComponent<Renderer>(),origin=new Vector3[vertices.Length],normal=new Vector3[vertices.Length],vertices=vertices,normals=new Vector3[vertices.Length]};
            for(int i=0;i<vertices.Length;i++)
            {
                skin.origin[i]=target.TransformPoint(vertices[i]);
                skin.normal[i]=i<normals.Length?target.TransformDirection(normals[i]):Vector3.up;
            }
            float best=float.PositiveInfinity;
            for(int i=0;i<joints.Count;i++){float d=(joints[i].origin-source.bounds.center).sqrMagnitude;if(d<best){best=d;skin.nearest=i;}}
            if(source is SkinnedMeshRenderer rig&&rig.sharedMesh)
            {
                skin.weights=rig.sharedMesh.boneWeights;skin.bones=new int[rig.bones.Length];
                for(int i=0;i<rig.bones.Length;i++)skin.bones[i]=rig.bones[i]&&indices.TryGetValue(rig.bones[i],out int index)?index:skin.nearest;
                if(skin.weights.Length!=vertices.Length)skin.weights=null;
            }
            skins.Add(skin);
        }
        public Vector3 FollowSurface()
        {
            if(!support)return Vector3.zero;
            Vector3 next=support.TransformPoint(supportLocal),delta=next-supportWorld;supportWorld=next;
            if(delta.sqrMagnitude<.00000001f)return Vector3.zero;
            foreach(var joint in joints){joint.position+=delta;joint.previous+=delta;}
            GroundHeight+=delta.y;Deform();return delta;
        }
        public void Step(float dt)
        {
            if(support)
            {
                Vector3 next=support.TransformPoint(supportLocal),delta=next-supportWorld;supportWorld=next;
                foreach(var joint in joints){joint.position+=delta;joint.previous+=delta;}
                GroundHeight+=delta.y;
            }
            foreach(var joint in joints)
            {
                Vector3 p=joint.position;var velocity=(p-joint.previous)*.965f;
                joint.previous=p;joint.position=p+velocity+Physics.gravity*dt*dt;
            }
            for(int iteration=0;iteration<7;iteration++)
            {
                foreach(var joint in joints)
                {
                    if(joint.parent<0||joint.length<.0001f)continue;
                    var parent=joints[joint.parent];Vector3 delta=joint.position-parent.position;float length=delta.magnitude;
                    if(length<.00001f)continue;
                    Vector3 correction=delta*((length-joint.length)/length)*.5f;
                    joint.position-=correction;parent.position+=correction;
                }
            }
            bool grounded=false;float movement=0;
            foreach(var joint in joints)
            {
                Vector3 delta=joint.position-joint.previous;float distance=delta.magnitude;
                if(distance>.00001f)
                {
                    int count=Physics.SphereCastNonAlloc(joint.previous,radius,delta/distance,hits,distance,~0,QueryTriggerInteraction.Ignore);
                    float closest=float.PositiveInfinity;int index=-1;
                    // Initial overlaps have no usable contact point (Unity may return world zero).
                    for(int i=0;i<count;i++)if(Scenery(hits[i])&&hits[i].distance>.00001f&&hits[i].distance<closest){closest=hits[i].distance;index=i;}
                    if(index>=0)
                    {
                        var hit=hits[index];
                        // Sweep distance describes the sphere centre; do not reconstruct it from hit.point.
                        joint.position=joint.previous+delta/distance*Mathf.Max(0,hit.distance-.002f);
                        var slide=Vector3.ProjectOnPlane(joint.position-joint.previous,hit.normal)*.48f;
                        joint.previous=joint.position-slide;
                        if(hit.normal.y>.45f){grounded=true;RememberSupport(hit);}
                    }
                }
                // Constraints can press a joint into the surface after the swept query starts touching it.
                int nearby=Physics.RaycastNonAlloc(joint.position+Vector3.up*radius*2,Vector3.down,hits,radius*3,~0,QueryTriggerInteraction.Ignore);
                float ground=float.NegativeInfinity;int selected=-1;
                for(int i=0;i<nearby;i++)if(Scenery(hits[i])&&hits[i].normal.y>.45f&&hits[i].point.y>ground){ground=hits[i].point.y;selected=i;}
                if(selected>=0&&joint.position.y<ground+radius)
                {joint.position=new Vector3(joint.position.x,ground+radius,joint.position.z);joint.previous=Vector3.Lerp(joint.previous,joint.position,.45f);grounded=true;RememberSupport(hits[selected]);}
                movement+=(joint.position-joint.previous).sqrMagnitude;
            }
            quiet=grounded&&movement/joints.Count<.000025f?quiet+dt:0;
            foreach(var joint in joints)
            {
                Vector3 direction=joint.child>=0?joints[joint.child].position-joint.position:joint.parent>=0?joint.position-joints[joint.parent].position:Vector3.up;
                joint.rotation=joint.direction.sqrMagnitude>.000001f&&direction.sqrMagnitude>.000001f?Quaternion.FromToRotation(joint.direction,direction):Quaternion.identity;
            }
            Deform();
        }
        bool Scenery(RaycastHit hit)
        {
            if(!hit.collider||hit.collider.isTrigger)return false;
            return hit.collider.GetComponentInParent<IModeActor>()==null&&hit.collider.GetComponentInParent<IMovementState>()==null;
        }
        void RememberSupport(RaycastHit hit)
        {support=hit.collider.transform;supportLocal=support.InverseTransformPoint(hit.point);supportWorld=hit.point;GroundHeight=hit.point.y;}
        Vector3 Point(int index,Vector3 origin)=>joints[index].position+joints[index].rotation*(origin-joints[index].origin);
        Vector3 Normal(int index,Vector3 normal)=>joints[index].rotation*normal;
        void Deform()
        {
            bool first=true;Bounds bounds=default;
            foreach(var skin in skins)
            {
                for(int i=0;i<skin.vertices.Length;i++)
                {
                    Vector3 p,n;
                    if(skin.weights!=null)
                    {
                        var w=skin.weights[i];float total=w.weight0+w.weight1+w.weight2+w.weight3;
                        if(total>.001f)
                        {
                            int a=skin.bones[w.boneIndex0],b=skin.bones[w.boneIndex1],c=skin.bones[w.boneIndex2],d=skin.bones[w.boneIndex3];
                            p=(Point(a,skin.origin[i])*w.weight0+Point(b,skin.origin[i])*w.weight1+Point(c,skin.origin[i])*w.weight2+Point(d,skin.origin[i])*w.weight3)/total;
                            n=Normal(a,skin.normal[i])*w.weight0+Normal(b,skin.normal[i])*w.weight1+Normal(c,skin.normal[i])*w.weight2+Normal(d,skin.normal[i])*w.weight3;
                        }
                        else{p=Point(skin.nearest,skin.origin[i]);n=Normal(skin.nearest,skin.normal[i]);}
                    }
                    else{p=Point(skin.nearest,skin.origin[i]);n=Normal(skin.nearest,skin.normal[i]);}
                    skin.vertices[i]=skin.transform.InverseTransformPoint(p);skin.normals[i]=skin.transform.InverseTransformDirection(n.normalized);
                    if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                }
                skin.mesh.vertices=skin.vertices;skin.mesh.normals=skin.normals;skin.mesh.RecalculateBounds();
                skin.renderer.localBounds=new Bounds(skin.mesh.bounds.center,skin.mesh.bounds.size+Vector3.one*radius*24);
            }
            Bounds=bounds;
        }
    }
}
