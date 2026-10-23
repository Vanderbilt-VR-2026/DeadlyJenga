using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class LobbyBackspaceIcon:MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            void Line(Vector2 a,Vector2 b)
            {
                var normal=new Vector2(-(b-a).y,(b-a).x).normalized*2.4f;int start=mesh.currentVertCount;
                mesh.AddVert(a-normal,color,Vector2.zero);mesh.AddVert(a+normal,color,Vector2.zero);mesh.AddVert(b+normal,color,Vector2.zero);mesh.AddVert(b-normal,color,Vector2.zero);
                mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
            }
            var points=new[]{new Vector2(-44,0),new Vector2(-24,24),new Vector2(42,24),new Vector2(42,-24),new Vector2(-24,-24),new Vector2(-44,0)};
            for(int i=1;i<points.Length;i++)Line(points[i-1],points[i]);
            Line(new Vector2(-2,-11),new Vector2(20,11));Line(new Vector2(-2,11),new Vector2(20,-11));
        }
    }
}
