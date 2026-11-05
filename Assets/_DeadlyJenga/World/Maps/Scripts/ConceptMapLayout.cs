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

    // Small experimental maps. The caller supplies a fixed foundation with its top at 0.4 m.
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
                case ConceptMapKind.MovingShelter:return new Vector2(30,22);
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
            public MayhemModule Platform(string purpose,Vector3 center,float top,Vector2 size)
            {
                var m=Module(purpose,new Vector3(center.x,.4f,center.z));
                // A solid grounded platform is intentionally fixed rather than a hidden floating deck.
                Part(m,new Vector3(0,(top-.4f)*.5f,0),new Vector3(size.x,top-.4f,size.y),1);return m;
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
            foreach(float x in new[]{-3.4f,3.4f})b.Part(fixedRoof,new Vector3(x,1.5f,0),new Vector3(.8f,3,5.5f),0);
            var support=b.Module("Fixed roof frame A",new Vector3(4,.4f,0));
            foreach(float x in new[]{-3.4f,3.4f})b.Part(support,new Vector3(x,1.5f,0),new Vector3(.8f,3,5.5f),0);
            b.Box("Moving roof",new Vector3(4,3.575f,0),new Vector3(8,.35f,6),2,48);
        }

        static void Screens(Builder b)
        {
            Screen(b,"Screen west",new Vector3(-1.5f,.4f,-1.5f),0);
            Screen(b,"Screen east",new Vector3(1.5f,.4f,1.5f),180);
        }
        static void Screen(Builder b,string purpose,Vector3 p,float heading)
        {
            var m=b.Module(purpose,p,55,heading);
            b.Part(m,new Vector3(0,.15f,0),new Vector3(4,.3f,2.4f),2);
            b.Part(m,new Vector3(0,1.8f,0),new Vector3(4,3,.35f),1);
            b.Part(m,new Vector3(-1.825f,1.8f,.8f),new Vector3(.35f,3,1.25f),0);
        }

        static void Afterfall(Builder b)
        {
            // The high shelf holds one end. Whether the plate becomes a useful ramp is tested, not assumed.
            var shelf=b.Module("Fixed high landing and plate shelf",new Vector3(6.5f,.4f,0));
            b.Part(shelf,new Vector3(0,1.175f,0),new Vector3(3.4f,2.35f,5),1);
            b.Part(shelf,new Vector3(-1.85f,1,0),new Vector3(.7f,2,3),0);
            b.Box("Removable support",new Vector3(-4.5f,1.525f,0),new Vector3(.8f,1.75f,3),0,70);
            b.Box("Falling route",new Vector3(0,2.575f,0),new Vector3(9.6f,.35f,3),2,65);
            b.Ramp("Fixed independent high-landing ramp",new Vector3(6.5f,.4f,-12),new Vector3(6.5f,2.75f,-2.5f),3);
            b.Platform("Fixed low catch landing",new Vector3(-5.5f,0,0),.65f,new Vector2(4,5));
        }

        static void Causeway(Builder b)
        {
            var west=b.Platform("Fixed west landing",new Vector3(-7,0,0),2.4f,new Vector2(5,5));
            b.Platform("Fixed east landing",new Vector3(7,0,0),2.4f,new Vector2(5,5));
            var pivot=b.Box("Fixed causeway pivot",new Vector3(-4.5f,1.225f,0),new Vector3(1.2f,1.65f,3),0);
            var plate=b.Box("Pivot route plate",new Vector3(0,2.225f,0),new Vector3(9,.35f,3),2,80);
            plate.hingeSupport=pivot;plate.pivot=new Vector3(-4.5f,2.225f,0);
            b.Ramp("Fixed west access ramp",new Vector3(-7,.4f,-11),new Vector3(-7,2.4f,-2.5f));
            b.Ramp("Fixed east access ramp",new Vector3(7,.4f,-11),new Vector3(7,2.4f,-2.5f));
            b.Box("Fixed outer route bridge",new Vector3(0,2.225f,6.5f),new Vector3(14,.35f,3),1);
            b.Box("Fixed west outer landing",new Vector3(-7,1.4f,4.5f),new Vector3(3,2,4),0);
            b.Box("Fixed east outer landing",new Vector3(7,1.4f,4.5f),new Vector3(3,2,4),0);
        }

        static void Refuges(Builder b)
        {
            var low=b.Module("Fixed low-refuge piers",new Vector3(-6,.4f,0));
            foreach(float x in new[]{-3.4f,3.4f})b.Part(low,new Vector3(x,1.5f,0),new Vector3(.8f,3,5.5f),0);
            b.Box("Loose low-refuge roof",new Vector3(-6,3.575f,0),new Vector3(8,.35f,6),1,48);
            b.Platform("Fixed high open refuge",new Vector3(6,0,0),4.4f,new Vector2(7,7));
            b.Ramp("Fixed broad high-refuge ramp south",new Vector3(6,.4f,-14),new Vector3(6,4.4f,-3.5f),3.5f);
            b.Ramp("Fixed broad high-refuge ramp north",new Vector3(6,.4f,14),new Vector3(6,4.4f,3.5f),3.5f);
        }

        static void Borough(Builder b)
        {
            FuelPod(b,"Fuel pod west",new Vector3(-10,.4f,0));
            FuelPod(b,"Fuel pod middle",new Vector3(0,.4f,0));
            FuelPod(b,"Fuel pod east",new Vector3(10,.4f,0));
            b.Box("Connector west",new Vector3(-5,1.16f,0),new Vector3(4.8f,.02f,3),2,20);
            b.Box("Connector east",new Vector3(5,1.16f,0),new Vector3(4.8f,.02f,3),2,20);
            b.Ramp("Fixed west pod access",new Vector3(-10,.4f,-7),new Vector3(-10,1.15f,-3),3);
            b.Ramp("Fixed middle pod access",new Vector3(0,.4f,-7),new Vector3(0,1.15f,-3),3);
            b.Ramp("Fixed east pod access",new Vector3(10,.4f,-7),new Vector3(10,1.15f,-3),3);
        }
        static void FuelPod(Builder b,string purpose,Vector3 p)
        {
            var m=b.Module(purpose,p,110);
            b.Part(m,new Vector3(0,.375f,0),new Vector3(6,.75f,6),0);
            b.Part(m,new Vector3(0,3.575f,0),new Vector3(6,.35f,6),1);
            foreach(float x in new[]{-2.6f,2.6f})foreach(float z in new[]{-2.6f,2.6f})
                b.Part(m,new Vector3(x,2.075f,z),new Vector3(.65f,2.65f,.65f),0);
        }
    }
}
