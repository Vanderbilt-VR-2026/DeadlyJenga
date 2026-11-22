using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Each mesh and its collision shapes belong to one removable physical member.
    internal sealed class FossilCarving
    {
        readonly List<Vector3> verts = new();
        readonly List<Color32> colors = new();
        readonly List<int> indices = new();
        readonly FossilMember member;
        readonly Vector3 pivot;
        public FossilCarving(FossilMember member, Vector3 pivot) { this.member = member; this.pivot = pivot; }
        public void Finish(Material material)
        {
            var mesh = new Mesh { name = member.name };
            mesh.SetVertices(verts); mesh.SetColors(colors); mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); member.mesh = mesh;
            member.art = new GameObject("Visual").transform;
            member.art.SetParent(member.transform, false);
            member.art.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            member.art.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            int n = verts.Count;
            verts.Add(a-pivot); verts.Add(b-pivot); verts.Add(c-pivot); verts.Add(d-pivot);
            for(int i=0;i<4;i++) colors.Add(color);
            indices.Add(n); indices.Add(n+1); indices.Add(n+2);
            indices.Add(n); indices.Add(n+2); indices.Add(n+3);
        }
        void Collision(Vector3 center, Quaternion rotation, Vector3 size)
        {
            var owner=member.gameObject;
            // Session spawn and bot goal queries inspect colliders on the body itself.
            if(Quaternion.Angle(rotation,Quaternion.identity)>.01f)
            {
                owner=new GameObject("Bearing"){layer=Layers.Block};
                owner.transform.SetParent(member.transform,false);
                owner.transform.localPosition=center-pivot;
                owner.transform.localRotation=rotation;
            }
            var col=owner.AddComponent<BoxCollider>();
            if(owner==member.gameObject)col.center=center-pivot;
            col.size=size;
            col.includeLayers=1<<Layers.Survivor; col.layerOverridePriority=12;
            member.shapes.Add(col); Climbables.Register(col,member);
        }
        public void Box(Vector3 center, Vector3 size, Color color, Quaternion rotation, bool solid=true)
        {
            var h=size*.5f; float e=Mathf.Min(.12f, Mathf.Min(h.x,Mathf.Min(h.y,h.z))*.25f);
            var rim=new[]{new Vector2(-h.x+e,-h.z),new Vector2(h.x-e,-h.z),new Vector2(h.x,-h.z+e),new Vector2(h.x,h.z-e),
                new Vector2(h.x-e,h.z),new Vector2(-h.x+e,h.z),new Vector2(-h.x,h.z-e),new Vector2(-h.x,-h.z+e)};
            Vector3 P(int i,float y,float inset)
            {
                var p=rim[i%8]; return center+rotation*new Vector3(p.x*(1-inset/h.x),y,p.y*(1-inset/h.z));
            }
            for(int i=0;i<8;i++)
            {
                int j=i+1;
                Face(P(i,-h.y,e),P(i,-h.y+e,0),P(j,-h.y+e,0),P(j,-h.y,e),color*.92f);
                Face(P(i,-h.y+e,0),P(i,h.y-e,0),P(j,h.y-e,0),P(j,-h.y+e,0),color);
                Face(P(i,h.y-e,0),P(i,h.y,e),P(j,h.y,e),P(j,h.y-e,0),color);
                var up=rotation*Vector3.up*h.y;
                Face(center+up,P(j,h.y,e),P(i,h.y,e),center+up,color);
                Face(center-up,P(i,-h.y,e),P(j,-h.y,e),center-up,color);
            }
            if(solid) Collision(center,rotation,size);
        }
        // Elliptical cross sections with enlarged articular ends, tapered enamel or flattened plates.
        public void Shaft(Vector3 a,Vector3 b,float width,float depth,Color color,bool tapered=false,bool solid=true,bool plate=false)
        {
            var axis=(b-a).normalized;
            var side=Vector3.Cross(Vector3.forward,axis);
            if(side.sqrMagnitude<.01f) side=Vector3.Cross(Vector3.up,axis);
            side.Normalize(); var back=Vector3.Cross(axis,side);
            float[] radii=plate?new[]{.58f,1f,.87f,.44f,.025f}:tapered?new[]{.79f,1f,.74f,.38f,.025f}:new[]{.68f,1f,.72f,.86f,.64f};
            float[] along={0,.13f,.48f,.84f,1};
            Vector3 P(int k,int j)
            {
                float t=j*2*Mathf.PI/8;
                return Vector3.Lerp(a,b,along[k])+radii[k]*(side*(width*Mathf.Cos(t))+back*(depth*Mathf.Sin(t)));
            }
            for(int i=0;i<8;i++)
            {
                for(int k=0;k<4;k++) Face(P(k,i),P(k,i+1),P(k+1,i+1),P(k+1,i),color*(1-.028f*((i+k)%3)));
                Face(a,P(0,i+1),P(0,i),a,color); Face(b,P(4,i),P(4,i+1),b,color);
            }
            if(solid)
            {
                var rot=Quaternion.LookRotation(back,axis);
                if(tapered || plate)
                {
                    Collision(Vector3.Lerp(a,b,.25f),rot,new Vector3(width*1.55f,(b-a).magnitude*.50f,depth*1.55f));
                    Collision(Vector3.Lerp(a,b,.70f),rot,new Vector3(width*.86f,(b-a).magnitude*.40f,depth*.86f));
                }
                else Collision((a+b)*.5f,rot,new Vector3(width*1.48f,(b-a).magnitude,depth*1.48f));
            }
        }
    }
}
