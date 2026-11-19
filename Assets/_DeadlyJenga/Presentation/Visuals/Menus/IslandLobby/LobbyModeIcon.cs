using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class LobbyModeIcon : MaskableGraphic
    {
        string mode;
        public void SetMode(string value){if(mode==value)return;mode=value;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d){int i=mesh.currentVertCount;mesh.AddVert(a,color,Vector2.zero);mesh.AddVert(b,color,Vector2.zero);mesh.AddVert(c,color,Vector2.zero);mesh.AddVert(d,color,Vector2.zero);mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);}
            void Line(Vector2 a,Vector2 b){var n=new Vector2(-(b-a).y,(b-a).x).normalized*2;Quad(a-n,a+n,b+n,b-n);}
            void Rect(float x,float y,float w,float h)=>Quad(new Vector2(x,y),new Vector2(x,y+h),new Vector2(x+w,y+h),new Vector2(x+w,y));
            if(mode=="ctf"){Rect(-22,-30,5,60);Quad(new Vector2(-17,30),new Vector2(25,18),new Vector2(-17,5),new Vector2(-17,5));}
            else if(mode=="hill"){Line(new Vector2(-30,20),new Vector2(-23,-20));Line(new Vector2(-23,-20),new Vector2(23,-20));Line(new Vector2(23,-20),new Vector2(30,20));Line(new Vector2(-30,20),new Vector2(-10,0));Line(new Vector2(-10,0),new Vector2(0,30));Line(new Vector2(0,30),new Vector2(10,0));Line(new Vector2(10,0),new Vector2(30,20));}
            else if(mode=="blockcontrol"){for(int y=0;y<2;y++)for(int x=0;x<3;x++)Rect(-32+x*23,-20+y*23,18,18);}
            else if(mode=="survival"){Line(new Vector2(-25,28),new Vector2(25,28));Line(new Vector2(-25,-28),new Vector2(25,-28));Line(new Vector2(-20,28),new Vector2(20,-28));Line(new Vector2(20,28),new Vector2(-20,-28));}
            else if(mode=="teamtickets"){Line(new Vector2(-25,25),new Vector2(25,25));Line(new Vector2(-25,25),new Vector2(-20,-5));Line(new Vector2(-20,-5),new Vector2(0,-30));Line(new Vector2(0,-30),new Vector2(20,-5));Line(new Vector2(20,-5),new Vector2(25,25));}
            else if(mode=="infection"){Rect(-6,-29,12,58);Rect(-29,-6,58,12);}
            else if(mode=="demo"){for(int i=0;i<4;i++){float a=i*Mathf.PI/4;Line(new Vector2(Mathf.Cos(a),Mathf.Sin(a))*-30,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*30);}}
            else{Rect(-4,-8,8,37);Rect(-4,-28,8,8);}
        }
    }
}
