using System.Collections.Generic;
using UnityEngine;
namespace DeadlyJenga.CastlePrototype
{
    public static class EpicCityTerrain
    {
        public static Mesh Create(EpicCityPlan plan,Vector2[] outline=null)
        {
            int sides=outline!=null?outline.Length:256;bool elevated=false;foreach(var site in plan.sites)elevated|=site.position.y>.01f;
            int rings=elevated?96:1;var vertices=new List<Vector3>{new(0,EpicCityLayout.GroundHeight(plan,Vector3.zero)-.02f,0)};var triangles=new List<int>();
            for(int r=1;r<=rings;r++)for(int i=0;i<sides;i++)
            {
                float angle=i*Mathf.PI*2/sides;var shore=outline!=null?outline[i]:new Vector2(Mathf.Cos(angle)*plan.halfSize.x*1.08f,Mathf.Sin(angle)*plan.halfSize.y*1.08f);
                var xy=shore*(r/(float)rings);var p=new Vector3(xy.x,0,xy.y);p.y=EpicCityLayout.GroundHeight(plan,p)-.02f;vertices.Add(p);
            }
            for(int i=0;i<sides;i++){triangles.Add(0);triangles.Add(1+(i+1)%sides);triangles.Add(1+i);}
            for(int r=1;r<rings;r++)for(int i=0;i<sides;i++)
            {int a=1+(r-1)*sides+i,b=1+(r-1)*sides+(i+1)%sides,c=1+r*sides+i,d=1+r*sides+(i+1)%sides;triangles.AddRange(new[]{a,b,d,a,d,c});}
            var mesh=new Mesh{name="Shore matched city terrain"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
