using System.Collections.Generic;
using UnityEngine;
namespace DeadlyJenga.Presentation
{
    // Render geometry only. The tracked character still owns all collisions.
    public static class CandyKaijuSculpt
    {
        public static Mesh Body(bool head)
        {
            const int rows=20,sides=40;var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int y=0;y<=rows;y++)
            {
                float t=y/(float)rows;
                float radius=head?Mathf.Lerp(.20f,.44f,Mathf.Sin(t*Mathf.PI))*(t<.24f?.83f:1):
                    Mathf.Lerp(.20f,.48f,Mathf.Sin(Mathf.Pow(t,.74f)*Mathf.PI))+.06f*Mathf.Exp(-Mathf.Pow((t-.72f)*8,2));
                for(int i=0;i<=sides;i++)
                {
                    float a=i*Mathf.PI*2/sides;float rib=1+.035f*Mathf.Cos(a*8)*Mathf.Sin(t*Mathf.PI);
                    float front=Mathf.Max(0,Mathf.Sin(a));
                    float z=Mathf.Sin(a)*radius*(head?.91f:.78f);
                    float armor=Mathf.Pow(Mathf.Max(0,Mathf.Cos(t*43-Mathf.Abs(Mathf.Cos(a))*5)),4)*Mathf.Sin(t*Mathf.PI)*.027f;
                    if(head)z+=Mathf.Pow(front,8)*(.19f*Mathf.Exp(-Mathf.Pow((t-.32f)*8,2))+.048f*Mathf.Exp(-Mathf.Pow((t-.64f)*13,2)));
                    else z+=front*(Mathf.Sin(t*Mathf.PI)*.09f+armor);
                    vertices.Add(new Vector3(Mathf.Cos(a)*(radius+armor)*rib,t-.5f,z));uv.Add(new Vector2(i/(float)sides,t));
                    if(y<rows&&i<sides){int k=y*(sides+1)+i;triangles.AddRange(new[]{k,k+sides+1,k+1,k+1,k+sides+1,k+sides+2});}
                }
            }
            // Close the neck / base so tabletop and spectator views cannot see inside.
            int bottom=vertices.Count;vertices.Add(new Vector3(0,-.5f,0));uv.Add(Vector2.zero);int top=vertices.Count;vertices.Add(new Vector3(0,.5f,0));uv.Add(Vector2.one);
            for(int i=0;i<sides;i++){triangles.AddRange(new[]{bottom,i,i+1,top,rows*(sides+1)+i+1,rows*(sides+1)+i});}
            var mesh=new Mesh{name=head?"Sculpted tidal kaiju skull":"Sculpted tidal kaiju thorax"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public static Mesh Arm()
        {
            const int rows=12,sides=16;
            var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
            for(int j=0;j<=rows;j++)for(int i=0;i<=sides;i++)
            {
                float t=j/(float)rows,a=i*Mathf.PI*2/sides;
                float r=(.37f+.23f*Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*Mathf.PI)),.7f))*(1-.24f*t)+Mathf.Cos(a*4+t*5)*.037f;
                if(j==0||j==rows)r*=.65f;
                v.Add(new Vector3(Mathf.Cos(a)*r,2*t-1,Mathf.Sin(a)*r+Mathf.Sin(t*Mathf.PI)*.095f));
                uv.Add(new Vector2(i/(float)sides,t));
                if(j==rows||i==sides)continue;
                int k=j*(sides+1)+i;tri.AddRange(new[]{k,k+sides+1,k+1,k+1,k+sides+1,k+sides+2});
            }
            var mesh=new Mesh{name="Sculpted tidal arm with water channels"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();return mesh;
        }
        public static Mesh Fin()
        {
            var outline=new[]{new Vector3(-.5f,-.4f,.02f),new Vector3(.43f,-.39f,0),new Vector3(.37f,-.08f,-.09f),
                new Vector3(.22f,.19f,-.22f),new Vector3(.06f,.68f,-.46f),new Vector3(-.09f,.30f,-.30f),new Vector3(-.33f,.08f,-.04f)};
            var v=new List<Vector3>(outline){new Vector3(-.06f,-.055f,.20f),new Vector3(-.07f,-.065f,-.19f)};
            var uv=new List<Vector2>();foreach(var p in v)uv.Add(new Vector2(p.x+.5f,p.y+.4f));
            var t=new List<int>();for(int i=0;i<outline.Length;i++){int next=(i+1)%outline.Length;t.AddRange(new[]{i,next,7,next,i,8});}
            var mesh=new Mesh{name="Kaiju swept armored fin"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(t,0);mesh.RecalculateNormals();return mesh;
        }
    }
}
