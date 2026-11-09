using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public enum ConceptMapKind
    {
        MovingShelter, PeelAwayScreen, CollapseCreatedRoute,
        PivotingCauseway, RefugeExchange, BreakContactBorough
    }

    // Layouts use the island deck at 0.4 m as the common ground level.
    public static partial class ConceptMapLayout
    {
        public static readonly string[] Names={"Moving Shelter","Peel-Away Screen","Collapse-Created Route","Pivoting Causeway","Refuge Exchange","Break-Contact Borough"};
        public static List<MayhemModule> Build(ConceptMapKind kind)
        {
            if(!Enum.IsDefined(typeof(ConceptMapKind),kind))throw new ArgumentOutOfRangeException(nameof(kind));
            var b=new Builder((int)kind);
            switch(kind)
            {
                case ConceptMapKind.MovingShelter:Shelter(b);break;
                case ConceptMapKind.PeelAwayScreen:Screens(b);break;
                case ConceptMapKind.CollapseCreatedRoute:Afterfall(b);break;
                case ConceptMapKind.PivotingCauseway:Causeway(b);break;
                case ConceptMapKind.RefugeExchange:Refuges(b);break;
                case ConceptMapKind.BreakContactBorough:Borough(b);break;
            }
            return b.modules;
        }
        public static Bounds Bounds(IReadOnlyList<MayhemModule> modules)=>MayhemStructureLayout.Bounds(modules);
        public static Vector2 FoundationSize(ConceptMapKind kind)
        {
            switch(kind)
            {
                case ConceptMapKind.MovingShelter:return new Vector2(27,20);
                case ConceptMapKind.PeelAwayScreen:return new Vector2(28,22);
                case ConceptMapKind.CollapseCreatedRoute:return new Vector2(30,30);
                case ConceptMapKind.PivotingCauseway:return new Vector2(32,30);
                case ConceptMapKind.RefugeExchange:return new Vector2(34,30);
                default:return new Vector2(36,24);
            }
        }
        public static Vector3[] SpawnCandidates(ConceptMapKind kind)
        {
            var size=FoundationSize(kind);float x=size.x*.5f-3;
            return new[]{new Vector3(-x,.45f,-7),new Vector3(x,.45f,-7),new Vector3(-x,.45f,7),new Vector3(x,.45f,7),new Vector3(0,.45f,-9),new Vector3(0,.45f,9)};
        }
        public static string[] TransformationTargets(ConceptMapKind kind)
        {
            switch(kind)
            {
                case ConceptMapKind.MovingShelter:return new[]{"Moving roof"};
                case ConceptMapKind.PeelAwayScreen:return new[]{"Screen west","Screen east"};
                case ConceptMapKind.CollapseCreatedRoute:return new[]{"Removable support","Falling route"};
                case ConceptMapKind.PivotingCauseway:return new[]{"Pivot route plate"};
                case ConceptMapKind.RefugeExchange:return new[]{"Loose low-refuge roof"};
                default:return new[]{"Connector west","Connector east","Fuel pod west","Fuel pod middle","Fuel pod east"};
            }
        }

        sealed class Builder
        {
            readonly int district;
            public readonly List<MayhemModule> modules=new();
            public Builder(int district){this.district=district;}
            public MayhemModule Module(string purpose,Vector3 position,float mass=0,float heading=0)
            {
                var m=new MayhemModule{district=district,purpose=purpose,position=position,mass=mass,rotation=Quaternion.Euler(0,heading,0)};
                modules.Add(m);return m;
            }
            public MayhemModule Box(string purpose,Vector3 p,Vector3 size,int finish=0,float mass=0)
            {var m=Module(purpose,p,mass);Part(m,Vector3.zero,size,finish);return m;}
            public void Part(MayhemModule m,Vector3 p,Vector3 size,int finish=0)
            {m.shapes.Add(new MayhemShape(p,size,finish,Quaternion.identity));}
            public MayhemModule Platform(string purpose,Vector3 center,float top,Vector2 size,int postFinish=0,int deckFinish=1)
            {
                var m=Module(purpose,new Vector3(center.x,.4f,center.z));
                float height=top-.4f;
                if(height<.9f)
                    Part(m,new Vector3(0,height*.5f,0),new Vector3(size.x,height,size.y),deckFinish);
                else
                {
                    const float deck=.38f;
                    Part(m,new Vector3(0,height-deck*.5f,0),new Vector3(size.x,deck,size.y),deckFinish);
                    Posts(m,size.x*.5f-.48f,size.y*.5f-.48f,height-deck,postFinish);
                }
                return m;
            }
            public void Posts(MayhemModule m,float x,float z,float height,int finish=0)
            {
                foreach(float side in new[]{-1f,1f})foreach(float end in new[]{-1f,1f})
                {
                    Part(m,new Vector3(side*x,height*.5f,end*z),new Vector3(.64f,height,.64f),finish);
                    Part(m,new Vector3(side*x,.15f,end*z),new Vector3(.92f,.3f,.92f),4);
                    Part(m,new Vector3(side*x,height-.14f,end*z),new Vector3(.88f,.28f,.88f),5);
                }
            }
            public void Rail(MayhemModule m,float x,float z,float top,float length)
            {
                Part(m,new Vector3(x,top+.38f,z),new Vector3(length,.22f,.22f),5);
                foreach(float end in new[]{-.5f,.5f})
                    Part(m,new Vector3(x+length*end,top+.25f,z),new Vector3(.32f,.5f,.34f),5);
            }
            public MayhemModule Ramp(string purpose,Vector3 low,Vector3 high,float width=3)
            {
                if(high.y<=low.y)throw new ArgumentException("Ramp must rise.");
                var horizontal=Vector3.ProjectOnPlane(high-low,Vector3.up);
                var heading=Quaternion.LookRotation(horizontal,Vector3.up);
                float run=horizontal.magnitude,rise=high.y-low.y;
                var center=(low+high)*.5f;var m=Module(purpose,center);
                float half=width*.5f,z=run*.5f,bottom=-rise*.5f-.05f;
                var vertices=new[]{new Vector3(-half,bottom,-z),new Vector3(half,bottom,-z),new Vector3(half,-rise*.5f,-z),new Vector3(-half,-rise*.5f,-z),new Vector3(-half,bottom,z),new Vector3(half,bottom,z),new Vector3(half,rise*.5f,z),new Vector3(-half,rise*.5f,z)};
                for(int i=0;i<vertices.Length;i++)vertices[i]=heading*vertices[i];
                // Geometry is pre-rotated so the first shape retains identity rotation.
                m.shapes.Add(new MayhemShape(Vector3.zero,new Vector3(width,rise+.05f,run),2,Quaternion.identity,vertices));return m;
            }
        }

        static void Shelter(Builder b)
        {
            var fixedRoof=b.Module("Fixed sheltered refuge",new Vector3(-6,.4f,0));
            b.Part(fixedRoof,new Vector3(0,3.175f,0),new Vector3(8,.35f,6),1);
            b.Posts(fixedRoof,3.4f,2.45f,3,3);
            var support=b.Module("Fixed roof frame A",new Vector3(4,.4f,0));
            b.Posts(support,3.4f,2.45f,3,3);
            b.Box("Moving roof",new Vector3(4,3.575f,0),new Vector3(8,.35f,6),2,48);
            b.Box("Fixed seaward bench",new Vector3(-6,.7f,-4.4f),new Vector3(4,.6f,1),1);
            b.Box("Fixed outer shelter bench",new Vector3(7,.7f,-4.4f),new Vector3(3,.6f,1),0);
            b.Box("Fixed courtyard divider",new Vector3(-.6f,.7f,-5.8f),new Vector3(2,.6f,.7f),5);
        }

        static void Screens(Builder b)
        {
            Screen(b,"Screen west",new Vector3(-1.5f,.4f,-1.5f),0);
            Screen(b,"Screen east",new Vector3(1.5f,.4f,1.5f),180);
            var exit=b.Module("Fixed courtyard gateway",new Vector3(0,.4f,8.45f));
            foreach(float side in new[]{-1f,1f})
            {
                b.Part(exit,new Vector3(side*2.3f,1.55f,0),new Vector3(.8f,3.1f,.8f),1);
                b.Part(exit,new Vector3(side*2.3f,2.98f,0),new Vector3(1,.28f,1),5);
            }
            b.Part(exit,new Vector3(0,3.2f,0),new Vector3(5.55f,.55f,.92f),1);
            foreach(float x in new[]{-6.5f,6.5f})
            {
                var gate=b.Module("Fixed bypass arch",new Vector3(x,.4f,-6.4f));
                foreach(float side in new[]{-1f,1f})
                    b.Part(gate,new Vector3(side*1.55f,1.5f,0),new Vector3(.7f,3,.85f),1);
                b.Part(gate,new Vector3(0,3,0),new Vector3(4,.55f,1.1f),1);
                b.Box("Fixed courtyard cover",new Vector3(x,.96f,3.4f),new Vector3(2,1.12f,.8f),0);
            }
        }
        static void Screen(Builder b,string purpose,Vector3 p,float heading)
        {
            var m=b.Module(purpose,p,55,heading);
            b.Part(m,new Vector3(0,.15f,0),new Vector3(4,.3f,2.4f),2);
            b.Part(m,new Vector3(0,1.8f,0),new Vector3(4,3,.35f),heading==0?0:1);
            b.Part(m,new Vector3(-1.825f,1.8f,.8f),new Vector3(.35f,3,1.25f),0);
        }

        static void Afterfall(Builder b)
        {
            var shelf=b.Platform("Fixed high landing and plate shelf",new Vector3(7,0,0),2.75f,new Vector2(4.4f,6));
            b.Rail(shelf,0,2.7f,2.35f,3.5f);
            b.Box("Removable support",new Vector3(-4.5f,1.525f,0),new Vector3(.8f,1.75f,3),0,70);
            var falling=b.Box("Falling route",new Vector3(0,2.575f,0),new Vector3(9.6f,.35f,3),2,65);
            falling.hingeSupport=shelf;
            falling.pivot=new Vector3(4.7f,2.575f,0);
            b.Ramp("Fixed independent high-landing ramp",new Vector3(7,.4f,-12),new Vector3(7,2.75f,-3),3.6f);
            b.Platform("Fixed low catch landing",new Vector3(-6.5f,0,0),.65f,new Vector2(5,5));
            b.Ramp("Fixed catch approach",new Vector3(-6.5f,.4f,-5),new Vector3(-6.5f,.65f,-2.5f),3);
            var deck=b.Platform("Fixed outer upper gallery",new Vector3(7,0,6.2f),2.75f,new Vector2(4.4f,6.4f));
            b.Rail(deck,0,2.9f,2.35f,3.5f);
        }

        static void Causeway(Builder b)
        {
            var west=b.Platform("Fixed west landing",new Vector3(-7,0,0),2.4f,new Vector2(5,5));
            var east=b.Platform("Fixed east landing",new Vector3(7,0,0),2.4f,new Vector2(5,5));
            b.Rail(west,-1.9f,-2.25f,2,.65f);
            b.Rail(east,1.9f,-2.25f,2,.65f);
            var pivot=b.Box("Fixed causeway pivot",new Vector3(-4.5f,1.225f,0),new Vector3(1.2f,1.65f,3),5);
            var plate=b.Box("Pivot route plate",new Vector3(0,2.225f,0),new Vector3(9,.35f,3),0,80);
            plate.hingeSupport=pivot;plate.pivot=new Vector3(-4.5f,2.225f,0);
            b.Ramp("Fixed west access ramp",new Vector3(-7,.4f,-11),new Vector3(-7,2.4f,-2.5f));
            b.Ramp("Fixed east access ramp",new Vector3(7,.4f,-11),new Vector3(7,2.4f,-2.5f));
            var bypass=b.Platform("Fixed outer route bridge",new Vector3(0,0,6.5f),2.4f,new Vector2(14,3.4f));
            b.Rail(bypass,0,1.5f,2,13.2f);
            b.Platform("Fixed west outer landing",new Vector3(-7,0,4.5f),2.4f,new Vector2(4.8f,4));
            b.Platform("Fixed east outer landing",new Vector3(7,0,4.5f),2.4f,new Vector2(4.8f,4));
        }

        static void Refuges(Builder b)
        {
            var low=b.Module("Fixed low-refuge piers",new Vector3(-6,.4f,0));
            b.Posts(low,3.4f,2.45f,3,3);
            b.Box("Loose low-refuge roof",new Vector3(-6,3.575f,0),new Vector3(8,.35f,6),1,48);
            var high=b.Platform("Fixed high open refuge",new Vector3(6,0,0),4.4f,new Vector2(7,7),3,2);
            foreach(float side in new[]{-1f,1f})
            {
                b.Rail(high,side*2.62f,3.15f,4,1.15f);
                b.Rail(high,side*2.62f,-3.15f,4,1.15f);
            }
            // The two ramp mouths remain open down the center of their respective sides.
            b.Ramp("Fixed broad high-refuge ramp south",new Vector3(6,.4f,-14),new Vector3(6,4.4f,-3.5f),3.5f);
            b.Ramp("Fixed broad high-refuge ramp north",new Vector3(6,.4f,14),new Vector3(6,4.4f,3.5f),3.5f);
            b.Box("Fixed low refuge bench",new Vector3(-6,.66f,2.2f),new Vector3(3,.52f,.75f),5);
            b.Box("Fixed approach marker west",new Vector3(-1.6f,.68f,-3.5f),new Vector3(.7f,.56f,.7f),5);
            b.Box("Fixed approach marker east",new Vector3(1.4f,.68f,-3.5f),new Vector3(.7f,.56f,.7f),5);
        }

        static void Borough(Builder b)
        {
            FuelPod(b,"Fuel pod west",new Vector3(-10,.4f,0));
            FuelPod(b,"Fuel pod middle",new Vector3(0,.4f,0));
            FuelPod(b,"Fuel pod east",new Vector3(10,.4f,0));
            b.Box("Connector west",new Vector3(-5,1.04f,0),new Vector3(3.7f,.22f,3),2,20);
            b.Box("Connector east",new Vector3(5,1.04f,0),new Vector3(3.7f,.22f,3),2,20);
            b.Ramp("Fixed west pod access",new Vector3(-10,.4f,-7),new Vector3(-10,1.15f,-3),3);
            b.Ramp("Fixed middle pod access",new Vector3(0,.4f,-7),new Vector3(0,1.15f,-3),3);
            b.Ramp("Fixed east pod access",new Vector3(10,.4f,-7),new Vector3(10,1.15f,-3),3);
        }
        static void FuelPod(Builder b,string purpose,Vector3 p)
        {
            var m=b.Module(purpose,p,110);
            b.Part(m,new Vector3(0,.375f,0),new Vector3(6,.75f,6),0);
            b.Part(m,new Vector3(0,3.575f,0),new Vector3(6,.35f,6),p.x<0?1:p.x>0?5:2);
            foreach(float x in new[]{-2.6f,2.6f})foreach(float z in new[]{-2.6f,2.6f})
                b.Part(m,new Vector3(x,2.075f,z),new Vector3(.65f,2.65f,.65f),p.x==0?0:3);
        }
    }
}
