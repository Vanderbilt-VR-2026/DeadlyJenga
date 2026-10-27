using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class LobbyTerminalPicture : RawImage
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var rect=GetPixelAdjustedRect();
            if(rect.width<=0||rect.height<=0)return;
            float radius=Mathf.Min(14,Mathf.Min(rect.width,rect.height)*.5f);
            void Vertex(Vector2 p)
            {
                var uv=new Vector2(uvRect.x+(p.x-rect.xMin)/rect.width*uvRect.width,uvRect.y+(p.y-rect.yMin)/rect.height*uvRect.height);
                mesh.AddVert(p,color,uv);
            }
            Vertex(rect.center);
            for(int corner=0;corner<4;corner++)
            {
                var center=new Vector2(corner==0||corner==3?rect.xMax-radius:rect.xMin+radius,corner<2?rect.yMax-radius:rect.yMin+radius);
                for(int step=0;step<=8;step++)
                {
                    float angle=(corner*90+step*90f/8)*Mathf.Deg2Rad;
                    Vertex(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius);
                }
            }
            int count=mesh.currentVertCount-1;
            for(int i=1;i<=count;i++)mesh.AddTriangle(0,i,i==count?1:i+1);
        }
    }
}
