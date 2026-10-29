using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    public sealed partial class FantasyMapArt
    {
        readonly List<GameObject> bodyObjects=new();
        readonly List<Mesh> bodyMeshes=new();
        void Decorate(Rigidbody body,Transform visual,int index)
        {
            var mesh=new ArtMesh(3);int count=0,stars=0;
            foreach(var shape in body.GetComponentsInChildren<BoxCollider>())
            {
                var size=shape.size;
                if(theme==FantasyMapTheme.Carnival&&size.y>3&&size.x<4&&size.z<4&&stars<2)
                {
                    stars++;
                    for(int side=0;side<4;side++)for(int star=0;star<3;star++)
                    {
                        var rotation=Quaternion.Euler(0,side*90,0);
                        var local=rotation*new Vector3(0,(star-1)*size.y*.27f,(side%2==0?size.z:size.x)*.5f+.035f);
                        var p=visual.InverseTransformPoint(shape.transform.TransformPoint(shape.center+local));
                        mesh.Star(p,Quaternion.Inverse(visual.rotation)*shape.transform.rotation*rotation,.35f);
                    }
                }
                if(size.y>1.2f||size.x<6||size.z<3||count>=4)continue;
                count++;
                Vector3 P(Vector3 p)=>visual.InverseTransformPoint(shape.transform.TransformPoint(shape.center+p));
                float z=size.z*.5f+.10f;float y=-size.y*.5f;
                var shapeRotation=Quaternion.Inverse(visual.rotation)*shape.transform.rotation;
                for(int edge=0;edge<4;edge++)
                {
                    bool alongX=edge<2;float sign=edge%2==0?-1:1;
                    var center=alongX?new Vector3(0,size.y*.10f,sign*(size.z*.5f+.015f)):new Vector3(sign*(size.x*.5f+.015f),size.y*.10f,0);
                    var band=alongX?new Vector3(size.x-.25f,.075f,.07f):new Vector3(.07f,.075f,size.z-.25f);
                    mesh.Box(P(center),band,shapeRotation,0);
                    if(theme==FantasyMapTheme.Carnival)
                    {
                        float length=alongX?size.x:size.z;int bulbs=Mathf.Clamp(Mathf.RoundToInt(length/3),2,9);
                        for(int bulb=0;bulb<bulbs;bulb++)
                        {
                            float t=(bulb+.5f)/bulbs-.5f;
                            var at=alongX?new Vector3(t*size.x,size.y*.19f,sign*(size.z*.5f+.10f)):new Vector3(sign*(size.x*.5f+.10f),size.y*.19f,t*size.z);
                            mesh.Sphere(P(at),Vector3.one*.13f,0,Color.white,5,8);
                        }
                    }
                }
                for(int face=0;face<2;face++)
                {
                z=(face==0?1:-1)*(size.z*.5f+.10f);
                if(theme==FantasyMapTheme.Honeycomb)
                {
                    for(int i=0;i<3;i++)
                    {
                        var p=P(new Vector3((i-1)*Mathf.Min(2,size.x*.2f),y-.32f,z));
                        mesh.Sphere(p,new Vector3(.20f,.5f+i*.22f,.20f),0);mesh.Sphere(p+Vector3.down*(.5f+i*.22f),Vector3.one*.24f,0);
                    }
                    var center=P(new Vector3(-size.x*.3f,0,z));var q=Quaternion.Inverse(visual.rotation)*shape.transform.rotation*Quaternion.Euler(0,face==0?180:0,0);
                    for(int n=0;n<3;n++)mesh.Hex(center+q*new Vector3((n-1)*.72f,n%2*.38f,0),q,.46f,.2f,1);
                    int cells=Mathf.Clamp(Mathf.RoundToInt(size.x/1.5f),3,20);
                    for(int n=0;n<cells;n++)
                    {
                        float x=-size.x*.46f+(n+.5f)*size.x*.92f/cells;
                        mesh.Hex(P(new Vector3(x,-.05f,z)),q,.42f,.17f,0);
                    }
                    if(count==1)for(int n=0;n<9;n++)
                        mesh.Sphere(P(new Vector3(-size.x*.43f+n%3*.3f,size.y*.5f+.13f+n/3*.12f,size.z*.38f)),Vector3.one*(.12f+n%3*.035f),0);
                }
                else
                {
                    int flags=Mathf.Clamp(Mathf.RoundToInt(size.x/3.5f),2,7);
                    for(int n=0;n<flags;n++)
                    {
                        float x=-size.x*.45f+(n+.5f)*size.x*.9f/flags;float width=size.x*.72f/flags;
                        mesh.Triangle(P(new Vector3(x-width*.5f,y-.12f,z)),P(new Vector3(x+width*.5f,y-.12f,z)),P(new Vector3(x,y-1.5f,z)),n%3);
                    }
                    mesh.Sphere(P(new Vector3(-size.x*.42f,size.y*.5f+.25f,z)),Vector3.one*.27f,0);
                    mesh.Sphere(P(new Vector3(size.x*.42f,size.y*.5f+.25f,z)),Vector3.one*.27f,0);
                    for(int n=0;n<5;n++)
                    {
                        float x=-size.x*.4f+n*size.x*.2f;
                        mesh.Sphere(P(new Vector3(x,y-.14f,z)),new Vector3(size.x*.10f,.26f,.13f),1);
                    }
                }
            }
                }
            if(count>0||stars>0)Model("Fantasy decorations",mesh,visual,new[]{goldMaterial,pinkMaterial,blueMaterial},true);
        }
        void BuildCarnival()
        {
            var ride=new ArtMesh(3);var center=new Vector3(80,104,halfDepth+290);float radius=65;
            for(int i=0;i<96;i++)
            {
                float a=i*Mathf.PI*2/96,b=(i+1)*Mathf.PI*2/96;
                var p=center+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius;
                var q=center+new Vector3(Mathf.Cos(b),Mathf.Sin(b),0)*radius;
                ride.Beam(p,q,1.4f,0);
                if(i%8==0)
                {
                    ride.Beam(center,p,.65f,0);ride.Sphere(p,Vector3.one*1.5f,0);
                    ride.Box(p+Vector3.down*5,new Vector3(8,7,6),Quaternion.identity,(i/8)%2+1);
                }
            }
            ride.Beam(center,new Vector3(center.x-40,8,center.z),3,1);ride.Beam(center,new Vector3(center.x+40,8,center.z),3,1);
            Model("Visual · distant carnival wheel",ride,transform,new[]{goldMaterial,pinkMaterial,blueMaterial});
            var balloons=new ArtMesh(3);
            for(int i=0;i<7;i++)
            {
                float a=.35f+i*.9f;var p=new Vector3(Mathf.Cos(a)*(halfWidth+260),75+i%3*43,Mathf.Sin(a)*(halfDepth+290));
                balloons.Sphere(p,new Vector3(14,20,14),i%3,Color.white,14,20);
                balloons.Box(p+Vector3.down*29,new Vector3(6,4,6),Quaternion.identity,0);
                for(int n=-1;n<=1;n+=2)balloons.Beam(p+new Vector3(n*7,-14,0),p+new Vector3(n*2,-27,0),.2f,0);
            }
            Model("Visual · distant candy balloons",balloons,transform,new[]{goldMaterial,pinkMaterial,blueMaterial});
        }
    }
}
