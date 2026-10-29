using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class LobbyTerminalGlyph : MaskableGraphic
    {
        public enum Symbol { Play, Plus, People, Radar, Gear, Chevron, Back, Check, Globe, Island }
        public Symbol symbol;
        VertexHelper mesh;
        Vector2 origin;
        float unit;

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();mesh=vertices;
            var rect=GetPixelAdjustedRect();origin=rect.center;unit=Mathf.Min(rect.width,rect.height)*.45f;
            switch(symbol)
            {
                case Symbol.Play: Triangle(new Vector2(-.6f,-.85f),new Vector2(-.6f,.85f),new Vector2(.85f,0));break;
                case Symbol.Plus: Bar(Vector2.zero,new Vector2(1.6f,.35f));Bar(Vector2.zero,new Vector2(.35f,1.6f));break;
                case Symbol.Chevron: Bar(new Vector2(-.26f,0),new Vector2(.85f,.24f),-45);Bar(new Vector2(.26f,0),new Vector2(.85f,.24f),45);break;
                case Symbol.Back: Bar(Vector2.zero,new Vector2(1.5f,.24f));Bar(new Vector2(-.44f,.27f),new Vector2(.85f,.24f),45);Bar(new Vector2(-.44f,-.27f),new Vector2(.85f,.24f),-45);break;
                case Symbol.Check: Bar(new Vector2(-.35f,-.16f),new Vector2(.75f,.28f),-45);Bar(new Vector2(.22f,.04f),new Vector2(1.25f,.28f),45);break;
                case Symbol.People:
                    Disc(new Vector2(0,.45f),.30f);Bar(new Vector2(0,-.43f),new Vector2(.63f,.72f));
                    Disc(new Vector2(-.68f,.28f),.21f);Bar(new Vector2(-.68f,-.42f),new Vector2(.38f,.60f));
                    Disc(new Vector2(.68f,.28f),.21f);Bar(new Vector2(.68f,-.42f),new Vector2(.38f,.60f));break;
                case Symbol.Radar:
                    Ring(Vector2.zero,.90f,.82f);Ring(Vector2.zero,.55f,.47f);Disc(Vector2.zero,.15f);
                    Bar(new Vector2(.37f,.37f),new Vector2(.13f,1.05f),-45);break;
                case Symbol.Gear:
                    Ring(Vector2.zero,.68f,.33f);
                    for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Bar(new Vector2(Mathf.Sin(a),Mathf.Cos(a))*.75f,new Vector2(.30f,.42f),-i*45);}break;
                case Symbol.Globe:
                    Ring(Vector2.zero,.90f,.79f);Bar(Vector2.zero,new Vector2(1.64f,.10f));
                    Bar(Vector2.zero,new Vector2(.10f,1.64f));Ring(Vector2.zero,.52f,.45f);break;
                case Symbol.Island:
                    Bar(new Vector2(0,-.72f),new Vector2(1.7f,.13f));
                    Bar(new Vector2(-.1f,-.12f),new Vector2(.18f,1.10f),-15);
                    for(int i=0;i<4;i++)Bar(new Vector2((i<2?-.3f:.3f),.49f),new Vector2(.78f,.14f),(i%2==0?20:-20)*(i<2?-1:1));break;
            }
        }
        void Triangle(Vector2 a,Vector2 b,Vector2 c)
        {
            int start=mesh.currentVertCount;
            mesh.AddVert(origin+a*unit,color,Vector2.zero);mesh.AddVert(origin+b*unit,color,Vector2.zero);mesh.AddVert(origin+c*unit,color,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);
        }
        void Bar(Vector2 center,Vector2 size,float degrees=0)
        {
            int start=mesh.currentVertCount;float a=degrees*Mathf.Deg2Rad;
            Vector2 u=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*size.x*.5f,v=new Vector2(-Mathf.Sin(a),Mathf.Cos(a))*size.y*.5f;
            foreach(var point in new[]{center-u-v,center-u+v,center+u+v,center+u-v})mesh.AddVert(origin+point*unit,color,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
        }
        void Disc(Vector2 center,float radius)
        {
            for(int i=0;i<20;i++){float a=i*Mathf.PI/10,b=(i+1)*Mathf.PI/10;Triangle(center,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius);}
        }
        void Ring(Vector2 center,float outer,float inner)
        {
            for(int i=0;i<24;i++)
            {
                float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;
                var u=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var v=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                Triangle(center+u*inner,center+u*outer,center+v*outer);Triangle(center+u*inner,center+v*outer,center+v*inner);
            }
        }
    }
}
