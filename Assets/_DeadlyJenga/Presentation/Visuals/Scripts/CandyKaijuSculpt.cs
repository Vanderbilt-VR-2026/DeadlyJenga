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
                float radius=head?Mathf.Lerp(.27f,.43f,Mathf.Sin(t*Mathf.PI))*(t<.25f?.74f:1):Mathf.Lerp(.22f,.47f,Mathf.Sin(Mathf.Pow(t,.7f)*Mathf.PI));
                for(int i=0;i<=sides;i++)
                {
                    float a=i*Mathf.PI*2/sides;float rib=1+.035f*Mathf.Cos(a*8)*Mathf.Sin(t*Mathf.PI);
                    float z=Mathf.Sin(a)*radius*(head?.91f:.78f);
                    if(z>0&&!head)z+=Mathf.Sin(t*Mathf.PI)*.08f;
                    vertices.Add(new Vector3(Mathf.Cos(a)*radius*rib,t-.5f,z));uv.Add(new Vector2(i/(float)sides,t));
                    if(y<rows&&i<sides){int k=y*(sides+1)+i;triangles.AddRange(new[]{k,k+sides+1,k+1,k+1,k+sides+1,k+sides+2});}
                }
            }
            // Close the neck / base so tabletop and spectator views cannot see inside.
            int bottom=vertices.Count;vertices.Add(new Vector3(0,-.5f,0));uv.Add(Vector2.zero);int top=vertices.Count;vertices.Add(new Vector3(0,.5f,0));uv.Add(Vector2.one);
            for(int i=0;i<sides;i++){triangles.AddRange(new[]{bottom,i,i+1,top,rows*(sides+1)+i+1,rows*(sides+1)+i});}
            var mesh=new Mesh{name=head?"Sculpted tidal kaiju skull":"Sculpted tidal kaiju thorax"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public static Mesh Fin()
        {
            var mesh=new Mesh{name="Kaiju swept crystalline fin"};
            mesh.vertices=new[]{new Vector3(-.5f,-.4f,0),new Vector3(.5f,-.4f,0),new Vector3(.12f,.65f,-.3f),new Vector3(0,-.2f,.17f)};
            mesh.triangles=new[]{0,2,1,0,1,3,1,2,3,2,0,3};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
