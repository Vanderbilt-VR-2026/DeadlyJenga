using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Each mesh and its collision shapes belong to one removable physical member.
    internal sealed class FossilCarving
    {
        readonly List<Vector3> verts = new();
        readonly List<Vector3> normals=new();
        readonly List<Color32> colors = new();
        readonly List<int> indices = new();
        readonly FossilMember member;
        readonly Vector3 pivot;
        public FossilCarving(FossilMember member, Vector3 pivot) { this.member = member; this.pivot = pivot; }
        public void Finish(Material material)
        {
            var mesh = new Mesh { name = member.name };
            mesh.SetVertices(verts); mesh.SetColors(colors); mesh.SetTriangles(indices, 0);
            mesh.SetNormals(normals); mesh.RecalculateBounds(); member.mesh = mesh;
            member.art = new GameObject("Visual").transform;
            member.art.SetParent(member.transform, false);
            member.art.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            member.art.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            int n = verts.Count;
            verts.Add(a-pivot); verts.Add(b-pivot); verts.Add(c-pivot); verts.Add(d-pivot);
            var normal=Vector3.Cross(b-a,c-a).normalized;
            for(int i=0;i<4;i++){colors.Add(color);normals.Add(normal);}
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
        public void SkullWindow(Vector3 at,Vector3 forward,Vector3 side,float length,float height,Color finish)
        {
            // A beveled rim around an actual open fenestra; both sides of the skull are modeled.
            Vector3 Ring(int k,float scale)
            {
                float t=k*Mathf.PI/5;
                return at+forward*(length*Mathf.Cos(t)*scale)+Vector3.up*(height*Mathf.Sin(t)*scale);
            }
            for(int i=0;i<10;i++)
            {
                var outer=Ring(i,1);var next=Ring(i+1,1);
                var lip=Ring(i,.60f);var lipNext=Ring(i+1,.60f);
                Face(outer,outer+side*.12f,next+side*.12f,next,finish);
                Face(outer+side*.12f,lip+side*.16f,lipNext+side*.16f,next+side*.12f,finish);
                Face(lip+side*.16f,lip,lipNext,lipNext+side*.16f,finish*.64f);
            }
        }
        public void ShieldSector(Vector3 root,Vector3 a,Vector3 tip,Vector3 b,Vector3 bevel,Color color)
        {
            var ridge=Vector3.Lerp(root,tip,.60f);
            foreach(int sign in new[]{-1,1})
            {
                var offset=bevel*sign;var crown=ridge+offset*1.65f;
                Face(root+offset,a+offset,crown,root+offset,color*.92f);
                Face(a+offset,tip+offset,crown,a+offset,color);
                Face(tip+offset,b+offset,crown,tip+offset,color*.98f);
                Face(b+offset,root+offset,crown,b+offset,color*.94f);
            }
            Face(a-bevel,a+bevel,tip+bevel,tip-bevel,color*.92f);
            Face(tip-bevel,tip+bevel,b+bevel,b-bevel,color*.92f);
            var axis=(tip-root).normalized;
            var across=Vector3.ProjectOnPlane(b-a,axis).normalized;
            var rot=Quaternion.LookRotation(Vector3.Cross(across,axis),axis);
            Collision(Vector3.Lerp(root,tip,.62f),rot,new Vector3((b-a).magnitude*.65f,(tip-root).magnitude*.76f,bevel.magnitude*2));
        }
        // Elliptical cross sections with enlarged articular ends, tapered enamel or flattened plates.
        public void Shaft(Vector3 a,Vector3 b,float width,float depth,Color color,bool tapered=false,bool solid=true,bool plate=false)
        {
            var axis=(b-a).normalized;
            var side=Vector3.Cross(Vector3.forward,axis);
            if(side.sqrMagnitude<.01f) side=Vector3.Cross(Vector3.up,axis);
            side.Normalize(); var back=Vector3.Cross(axis,side);
            float[] radii=plate?new[]{.58f,1f,.87f,.44f,.025f}:tapered?new[]{.79f,1f,.74f,.38f,.025f}:new[]{.64f,1.10f,.70f,.99f,.63f};
            float[] along={0,.13f,.48f,.84f,1};
            Vector3 P(int k,int j)
            {
                float t=j*2*Mathf.PI/12;
                return Vector3.Lerp(a,b,along[k])+radii[k]*(side*(width*Mathf.Cos(t))+back*(depth*Mathf.Sin(t)));
            }
            for(int i=0;i<12;i++)
            {
                for(int k=0;k<4;k++)
                {
                    int start=verts.Count;
                    Face(P(k,i),P(k,i+1),P(k+1,i+1),P(k+1,i),color*((k==0?.87f:1f)-.015f*((i+k)%3)));
                    float slope=(radii[k+1]-radii[k])*Mathf.Min(width,depth)/((b-a).magnitude*(along[k+1]-along[k]));
                    for(int v=0;v<4;v++)
                    {
                        float angle=(i+(v==1||v==2?1:0))*Mathf.PI/6;
                        var radial=(side*(Mathf.Cos(angle)/width)+back*(Mathf.Sin(angle)/depth)).normalized;
                        normals[start+v]=Vector3.Slerp(normals[start+v],(radial-axis*slope).normalized,.74f);
                    }
                }
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
