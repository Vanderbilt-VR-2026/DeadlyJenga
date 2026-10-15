using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Shared presentation assets, generated once. No scene assets or physics required.
    public enum CandyHudIcon { Play, Survivor, Hand, Friends, Settings, Help, Home, Move, Look, Jump, Climb, Pinch, Sound, Menu, Turn, Flight, Stick, ButtonA, ButtonY, Key, Trigger }
    public static class CandyHudVisuals
    {
        public static readonly Color Ink=new Color(.095f,.055f,.18f),Panel=new Color(.18f,.115f,.29f),Card=new Color(.25f,.17f,.38f),
            Mint=new Color(.35f,1,.77f),Lavender=new Color(.78f,.64f,1),Peach=new Color(1,.71f,.53f),Muted=new Color(.80f,.75f,.90f);
        static Texture2D rounded;static Sprite pill;
        static readonly Dictionary<CandyHudIcon,Texture2D> icons=new Dictionary<CandyHudIcon,Texture2D>();
        public static Texture2D Rounded
        {
            get
            {
                if(rounded)return rounded;
                rounded=new Texture2D(32,32,TextureFormat.RGBA32,false){name="Candy HUD round corners",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp};
                for(int y=0;y<32;y++)for(int x=0;x<32;x++)
                {var point=new Vector2(Mathf.Clamp(x,9,22),Mathf.Clamp(y,9,22));float a=Mathf.Clamp01(9.5f-Vector2.Distance(new Vector2(x,y),point));rounded.SetPixel(x,y,new Color(1,1,1,a));}
                rounded.Apply();return rounded;
            }
        }
        public static Sprite Pill
        {
            get{if(!pill){pill=Sprite.Create(Rounded,new Rect(0,0,32,32),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(10,10,10,10));pill.hideFlags=HideFlags.HideAndDontSave;}return pill;}
        }
        public static Texture2D Icon(CandyHudIcon kind)
        {
            if(icons.TryGetValue(kind,out var result)&&result)return result;
            var pixels=new Color[4096];
            void Dot(float x,float y,float radius=2)
            {for(int py=Mathf.Max(0,(int)(y-radius-1));py<Mathf.Min(64,y+radius+2);py++)for(int px=Mathf.Max(0,(int)(x-radius-1));px<Mathf.Min(64,x+radius+2);px++){float a=Mathf.Clamp01(radius+.5f-Vector2.Distance(new Vector2(px,py),new Vector2(x,y)));if(a>pixels[py*64+px].a)pixels[py*64+px]=new Color(1,1,1,a);}}
            void Line(float x,float y,float xx,float yy,float width=2)
            {int n=Mathf.CeilToInt(Vector2.Distance(new Vector2(x,y),new Vector2(xx,yy))*2);for(int i=0;i<=n;i++){float t=(float)i/Mathf.Max(1,n);Dot(Mathf.Lerp(x,xx,t),Mathf.Lerp(y,yy,t),width);}}
            void Ring(float x,float y,float radius)
            {for(int i=0;i<100;i++){float a=i*Mathf.PI/50;Dot(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius,1.8f);}}
            void Person(float x,float y,float size=1){Ring(x,y+14*size,6*size);Line(x,y+5*size,x,y-10*size,3*size);Line(x-10*size,y-2*size,x+10*size,y-2*size,2*size);Line(x,y-10*size,x-8*size,y-21*size,2*size);Line(x,y-10*size,x+8*size,y-21*size,2*size);}
            void Arrow(float x,float y,float xx,float yy){Line(x,y,xx,yy);Vector2 d=(new Vector2(x,y)-new Vector2(xx,yy)).normalized*8;Line(xx,yy,xx+d.x-d.y*.7f,yy+d.y+d.x*.7f);Line(xx,yy,xx+d.x+d.y*.7f,yy+d.y-d.x*.7f);}
            switch(kind)
            {
                case CandyHudIcon.Play: Line(23,13,23,51,3);Line(23,51,50,32,3);Line(50,32,23,13,3);break;
                case CandyHudIcon.Survivor:Person(32,33);break;
                case CandyHudIcon.Friends:Person(20,32,.8f);Person(44,32,.8f);break;
                case CandyHudIcon.Hand:case CandyHudIcon.Pinch:
                    Ring(32,25,13);for(int i=0;i<4;i++)Line(21+i*7,30,21+i*7,49-i%2*4,2.7f);Line(20,25,10,34,3);
                    if(kind==CandyHudIcon.Pinch){Ring(19,40,8);Dot(19,40,2);}break;
                case CandyHudIcon.Settings:Ring(32,32,16);Ring(32,32,6);for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Line(32+Mathf.Cos(a)*18,32+Mathf.Sin(a)*18,32+Mathf.Cos(a)*24,32+Mathf.Sin(a)*24,3);}break;
                case CandyHudIcon.Help:Ring(32,32,24);Line(32,18,32,20,3);Line(32,27,32,32);Line(32,32,40,37);Ring(32,41,8);break;
                case CandyHudIcon.Home:Line(9,30,32,52);Line(32,52,55,30);Line(16,32,16,12);Line(16,12,48,12);Line(48,12,48,32);Line(27,12,27,28);Line(27,28,37,28);Line(37,28,37,12);break;
                case CandyHudIcon.Move:Arrow(32,28,32,55);Arrow(32,36,32,9);Arrow(28,32,55,32);Arrow(36,32,9,32);break;
                case CandyHudIcon.Look:Line(18,37,18,23,3);Line(46,37,46,23,3);Ring(32,25,14);Ring(32,38,14);Line(32,52,32,34);break;
                case CandyHudIcon.Jump:Arrow(32,15,32,52);Line(10,10,54,10,3);break;
                case CandyHudIcon.Climb:Line(20,10,20,55);Line(44,10,44,55);for(int i=0;i<4;i++)Line(20,16+i*11,44,16+i*11);break;
                case CandyHudIcon.Sound:Line(24,19,24,47);Line(24,47,47,53);Line(47,53,47,25);Dot(17,17,7);Dot(40,23,7);break;
                case CandyHudIcon.Menu:for(int i=0;i<3;i++)Line(13,17+i*15,51,17+i*15,3);break;
                case CandyHudIcon.Turn:Ring(32,32,21);Arrow(32,53,50,46);break;
                case CandyHudIcon.Stick:Ring(32,19,20);Line(32,21,32,42,4);Ring(32,45,10);Arrow(7,38,7,54);Arrow(50,54,58,54);break;
                case CandyHudIcon.ButtonA:Ring(32,32,25);Line(21,19,32,46,2.5f);Line(32,46,43,19,2.5f);Line(25,29,39,29,2.5f);break;
                case CandyHudIcon.ButtonY:Ring(32,32,25);Line(21,45,32,32,2.5f);Line(43,45,32,32,2.5f);Line(32,32,32,18,2.5f);break;
                case CandyHudIcon.Key:Line(8,18,8,45);Line(8,45,56,45);Line(56,45,56,18);Line(56,18,8,18);Line(20,34,20,27);Line(20,27,44,27);Line(44,27,44,34);break;
                case CandyHudIcon.Trigger:Line(18,9,18,49,3);Line(18,49,40,53,3);Line(40,53,50,35,3);Line(50,35,35,23,3);Line(35,23,35,9,3);Line(18,9,35,9,3);Line(24,40,38,43,3);break;
                default:Arrow(13,21,51,43);Line(13,40,30,40);Line(11,30,23,30);break;
            }
            result=new Texture2D(64,64,TextureFormat.RGBA32,false){name="Candy HUD "+kind,hideFlags=HideFlags.HideAndDontSave};result.SetPixels(pixels);result.Apply();icons[kind]=result;return result;
        }
    }
}
