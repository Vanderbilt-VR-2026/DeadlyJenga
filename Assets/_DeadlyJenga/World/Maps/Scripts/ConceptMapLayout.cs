using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public enum ConceptMapKind
    {
        MovingShelter, PeelAwayScreen, CollapseCreatedRoute,
        PivotingCauseway, RefugeExchange, BreakContactBorough, SkylineIsland
    }

    // Layouts use the island deck at 0.4 m as the common ground level.
    public static partial class ConceptMapLayout
    {
        public static readonly string[] Names={"Moving Shelter","Peel-Away Screen","Collapse-Created Route","Pivoting Causeway","Refuge Exchange","Break-Contact Borough","Skyline Exchange"};
        public static List<MayhemModule> Build(ConceptMapKind kind)=>Build(kind,null);

        internal readonly struct SkylineSupport
        {
            public readonly MayhemModule piece,parent;
            public SkylineSupport(MayhemModule piece,MayhemModule parent)
            {this.piece=piece;this.parent=parent;}
        }

        internal static List<MayhemModule> Build(ConceptMapKind kind,List<SkylineSupport> supports)
        {
            if(!Enum.IsDefined(typeof(ConceptMapKind),kind))throw new ArgumentOutOfRangeException(nameof(kind));
            if(kind==ConceptMapKind.SkylineIsland)return Skyline(supports??new List<SkylineSupport>());
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
                case ConceptMapKind.SkylineIsland:return new Vector2(200,144);
                default:return new Vector2(36,24);
            }
        }
        public static Vector3[] SpawnCandidates(ConceptMapKind kind)
        {
            if(kind==ConceptMapKind.SkylineIsland)
                return new[]{new Vector3(0,.45f,-5),new Vector3(0,.45f,5),new Vector3(-55,.45f,0),new Vector3(55,.45f,0),new Vector3(-94,.45f,-55),new Vector3(94,.45f,55)};
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

        public const float TowerLane=2.6f,TowerHalfDepth=4.25f,TowerRun=12f,TowerHalfWidth=5f;
        public static float TowerDeck(int floor)=>.4f+floor*3.6f;
        public static float TowerEntrySide(float z)=>z>0?1:-1;
        public const float SkylineDistrictScale=1.5f;
        public const float HallBay=9.4f,HallHalfWidth=4.2f,HallNear=11.5f,HallFar=19.5f,HallTurn=28f;
        public const float HallLane=2.6f;
        public static readonly int[] SkylineBridgeFloors={1,4,7,10};
        public static readonly Vector3[] SkylineDistricts=
        {
            new(-61,0,-40),new(-61,0,40),new(0,0,40),
            new(0,0,-40),new(61,0,40),new(58.5f,0,-40)
        };
        public static readonly (float x,float z,int floors,string name)[] SkylineTowers=
        {
            (-31,0,11,"West Crown"),(31,0,10,"East Crown"),
            (-84,-35,5,"Shelter Spire"),(-84,35,4,"Screen Spire"),
            (84,-35,4,"Borough Spire"),(84,35,5,"Refuge Spire"),
            (-31,-43,8,"Caramel Pinnacle"),(31,-43,9,"Cherry Pinnacle"),
            (-31,43,9,"Mint Pinnacle"),(31,43,8,"Lemon Pinnacle")
        };
        public static Vector3 HallPoint(Vector3 district,float x,float y,float distance)
            =>new Vector3(district.x+x,y,Mathf.Sign(district.z)*distance);

        public static IEnumerable<(string name,Vector3 a,Vector3 b,float width,int finish)> SkylineCrossings()
        {
            foreach(int floor in SkylineBridgeFloors)
                yield return ("Crown crossing "+floor,new Vector3(-26,TowerDeck(floor),0),
                    new Vector3(26,TowerDeck(floor),0),4.1f,floor>=7?5:2);
            float middle=TowerDeck(3),high=TowerDeck(7);
            foreach(float x in new[]{-84f,84f})
                yield return ("Outer skyway",new Vector3(x,middle,-35+TowerHalfDepth),
                    new Vector3(x,middle,35-TowerHalfDepth),4.1f,1);
            foreach(float side in new[]{-1f,1f})
            {
                yield return ("Pinnacle crossing",new Vector3(-26,high,side*43),new Vector3(26,high,side*43),4.1f,5);
                foreach(float xSide in new[]{-1f,1f})
                {
                    // Enter the spires along X; the long flights run along Z.
                    yield return ("District skyway",new Vector3(xSide*36,middle,side*43),
                        new Vector3(xSide*79,middle,side*35),3.2f,1);
                    float y=TowerDeck(side>0?5:6),x=xSide*31;
                    yield return ("Crown spur",new Vector3(x,y,side*TowerHalfDepth),
                        new Vector3(x,y,side*(43-TowerHalfDepth)),1.45f,2);
                }
            }
        }
        static List<MayhemModule> Skyline(List<SkylineSupport> supports)
        {
            var b=new Builder((int)ConceptMapKind.SkylineIsland);
            for(int district=0;district<SkylineDistricts.Length;district++)
            {
                var offset=SkylineDistricts[district];
                foreach(var module in Build((ConceptMapKind)district))
                {
                    EnlargeDistrictModule(module,offset);
                    if(district==(int)ConceptMapKind.BreakContactBorough&&module.purpose.EndsWith(" pod access",StringComparison.Ordinal))
                        module.position.z-=.08f;
                    b.modules.Add(module);
                }
                DistrictHalls(b,offset,district,supports);
            }
            var towers=new List<(Vector3 center,MayhemModule[] decks)>();
            foreach(var tower in SkylineTowers)
                towers.Add((new Vector3(tower.x,0,tower.z),Tower(b,tower.x,tower.z,tower.floors,tower.name,supports)));
            foreach(var link in SkylineCrossings())
            {
                var bridge=SkylineBridge(b,link.name,link.a,link.b,link.width,link.finish);
                foreach(var end in new[]{link.a,link.b})
                {
                    var closest=towers[0];
                    foreach(var tower in towers)
                        if((tower.center-Vector3.ProjectOnPlane(end,Vector3.up)).sqrMagnitude<
                           (closest.center-Vector3.ProjectOnPlane(end,Vector3.up)).sqrMagnitude)closest=tower;
                    int level=Mathf.RoundToInt((end.y-.4f)/3.6f);
                    supports.Add(new SkylineSupport(bridge,closest.decks[level]));
                }
            }
            LandmarkRoofAccess(b);
            foreach(float x in new[]{-52f,52f})
            {
                var gate=b.Module("Boulevard arch",new Vector3(x,.4f,0));
                supports.Add(new SkylineSupport(gate,null));
                foreach(float side in new[]{-1f,1f})
                {
                    b.Part(gate,new Vector3(0,3.4f,side*5.4f),new Vector3(1.7f,6.8f,1.7f),3);
                    b.Part(gate,new Vector3(0,6.6f,side*5.4f),new Vector3(2.3f,.6f,2.3f),5);
                }
                b.Part(gate,new Vector3(0,7,0),new Vector3(1.8f,.85f,12.6f),2);
            }
            return b.modules;
        }
        static void EnlargeDistrictModule(MayhemModule module,Vector3 offset)
        {
            const float scale=SkylineDistrictScale;
            var floor=Vector3.up*.4f;
            module.position=offset+floor+(module.position-floor)*scale;
            if(module.hingeSupport!=null)module.pivot=offset+floor+(module.pivot-floor)*scale;
            module.mass*=scale*scale*scale;
            for(int i=0;i<module.shapes.Count;i++)
            {
                var shape=module.shapes[i];
                var vertices=shape.vertices==null?null:Array.ConvertAll(shape.vertices,v=>v*scale);
                module.shapes[i]=new MayhemShape(shape.center*scale,shape.size*scale,shape.finish,shape.rotation,vertices);
            }
        }

        static void DistrictHalls(Builder b,Vector3 district,int index,List<SkylineSupport> supports)
        {
            float lower=TowerDeck(1),upper=TowerDeck(2),side=Mathf.Sign(district.z);
            int accent=index%3;
            string name=Names[index]+" hall";
            Vector3 P(float x,float y,float distance)=>HallPoint(district,x,y,distance);
            var bays=new MayhemModule[3];
            for(int bay=-1;bay<=1;bay++)
            {
                var hall=b.Module(name+" bay "+bay,P(bay*HallBay,.4f,15.5f));
                bays[bay+1]=hall;
                supports.Add(new SkylineSupport(hall,null));
                foreach(float x in new[]{-3.55f,3.55f})foreach(float z in new[]{-3.5f,3.5f})
                {
                    b.Part(hall,new Vector3(x,3.4f,z),new Vector3(.86f,6.8f,.86f),accent);
                    b.Part(hall,new Vector3(x,3.34f,z),new Vector3(1.3f,.54f,1.3f),5);
                    b.Part(hall,new Vector3(x,6.9f,z),new Vector3(1.26f,.6f,1.26f),5);
                }
                b.Part(hall,new Vector3(0,lower-.6f,0),new Vector3(HallHalfWidth*2,.4f,8),accent);
                b.Part(hall,new Vector3(0,upper-.6f,0),new Vector3(HallHalfWidth*2,.4f,8),bay==0?5:accent);
                b.Rail(hall,0,-side*3.82f,upper-.4f,7.9f);
                if(bay!=0)b.Rail(hall,0,side*3.82f,upper-.4f,7.9f);
            }
            foreach(int bay in new[]{-1,1})
            {
                float xGap=(bay*.5f)*HallBay;
                var bypass=b.Box(name+" bypass "+bay,P(xGap,lower-.2f,14.3f),new Vector3(HallBay-HallHalfWidth*2,.4f,3.8f),5);
                supports.Add(new SkylineSupport(bypass,bays[1]));
                supports.Add(new SkylineSupport(bypass,bays[bay+1]));
            }
            var entry=b.Ramp(name+" entry",P(HallLane,.46f,4.5f),P(HallLane,lower,HallNear),3.2f);
            supports.Add(new SkylineSupport(entry,bays[1]));
            var flight=b.Ramp(name+" roof flight",P(HallLane,lower,HallFar),P(HallLane,upper,HallTurn),3.2f,true);
            supports.Add(new SkylineSupport(flight,bays[1]));
            var turn=b.Platform(name+" outlook",P(0,0,HallTurn+1.2f),upper,new Vector2(9.2f,2.4f),accent,5);
            supports.Add(new SkylineSupport(turn,null));
            b.Rail(turn,0,side*1.06f,upper-.4f,8.8f);
            var roofReturn=b.Box(name+" roof return",P(-HallLane,upper-.2f,(HallFar+HallTurn)*.5f),
                new Vector3(3.2f,.4f,HallTurn-HallFar),accent);
            supports.Add(new SkylineSupport(roofReturn,bays[1]));
            supports.Add(new SkylineSupport(roofReturn,turn));
        }

        static void LandmarkRoofAccess(Builder b)
        {
            float Roof(float standalone)=>.4f+(standalone-.4f)*SkylineDistrictScale;
            b.Ramp("Fixed sliding canopy roof access",new Vector3(-55,.4f,-58),new Vector3(-55,Roof(3.75f),-44.5f),3.1f);
            b.Ramp("Fixed loose refuge roof access",new Vector3(52,.4f,58),new Vector3(52,Roof(3.75f),44.5f),3.1f);
            float boroughRoof=Roof(4.15f);
            var borough=SkylineDistricts[(int)ConceptMapKind.BreakContactBorough];
            float roofAccessX=borough.x+15;
            b.Ramp("Fixed borough roof access",new Vector3(roofAccessX,.4f,-24.5f),new Vector3(roofAccessX,boroughRoof,borough.z+4.58f),2.4f);
            foreach(float x in new[]{-7.5f,7.5f})
                b.Platform("Fixed borough roof stepping block",borough+Vector3.right*x,boroughRoof,new Vector2(4,8.2f),3,5);
        }

        static MayhemModule SkylineBridge(Builder b,string name,Vector3 a,Vector3 end,float width,int finish)
        {
            Vector3 delta=end-a;
            float length=delta.magnitude+.5f,edge=width*.5f-.12f;
            var bridge=b.Module(name,(a+end)*.5f-Vector3.up*.2f,
                heading:Mathf.Atan2(-delta.z,delta.x)*Mathf.Rad2Deg);
            b.Part(bridge,Vector3.zero,new Vector3(length,.4f,width),finish);
            b.Part(bridge,new Vector3(0,-.38f,0),new Vector3(length,.38f,width*.65f),3);
            if(width<2)return bridge;
            b.Rail(bridge,0,edge,.2f,length-1.4f);
            b.Rail(bridge,0,-edge,.2f,length-1.4f);
            for(float x=-length*.5f+4;x<length*.5f-1;x+=6)
                foreach(float side in new[]{-1f,1f})
                    b.Part(bridge,new Vector3(x,.44f,side*edge),new Vector3(.24f,.5f,.24f),5);
            return bridge;
        }
        static MayhemModule[] Tower(Builder b,float x,float z,int floors,string name,List<SkylineSupport> supports)
        {
            var frame=new MayhemModule{position=new Vector3(x,.4f,z)};
            foreach(float side in new[]{-1f,1f})foreach(float end in new[]{-1f,1f})
            {
                float height=TowerDeck(floors)-.8f;
                b.Part(frame,new Vector3(side*4.35f,height*.5f,end*3.58f),new Vector3(1.25f,height,1.25f),3);
                b.Part(frame,new Vector3(side*4.35f,.3f,end*3.58f),new Vector3(1.8f,.6f,1.8f),4);
                for(int level=1;level<=floors;level++)
                    b.Part(frame,new Vector3(side*4.35f,TowerDeck(level)-.65f,end*3.58f),new Vector3(1.6f,.48f,1.6f),5);
            }
            for(int level=1;level<=floors;level++)
            {
                float deck=TowerDeck(level)-.4f;
                b.Part(frame,new Vector3(0,deck-.2f,0),new Vector3(TowerHalfWidth*2,.4f,TowerHalfDepth*2+.06f),level==floors?5:level%2==0?1:2);
                if(level==floors)
                    foreach(float side in new[]{-1f,1f})foreach(float end in new[]{-1f,1f})
                    {
                        b.Part(frame,new Vector3(side*4.34f,deck+.95f,end*3.58f),new Vector3(1.25f,1.9f,1.25f),2);
                        b.Part(frame,new Vector3(side*4.34f,deck+1.94f,end*3.58f),new Vector3(1.5f,.24f,1.5f),5);
                    }
            }
            float outer=TowerHalfDepth+TowerRun;
            float entry=TowerEntrySide(z);
            var groundTop=new Vector3(x-TowerLane,TowerDeck(1),z+entry*TowerHalfDepth);
            var groundFlight=b.Ramp(name+" ground flight",new Vector3(x-TowerLane,.46f,z+entry*outer),groundTop,3.5f);
            foreach(float side in new[]{entry,-entry})
            {
                int last=(floors%2!=0)==(side==entry)?floors:floors-1;
                float postHeight=TowerDeck(last)-.8f;
                foreach(float end in new[]{-1f,1f})
                {
                    float postX=end*4.08f,postZ=side*(outer+2.03f);
                    b.Part(frame,new Vector3(postX,postHeight*.5f,postZ),new Vector3(.82f,postHeight,.72f),3);
                    b.Part(frame,new Vector3(postX,postHeight-.12f,postZ),new Vector3(1,.24f,.9f),5);
                }
            }
            var decks=SectionTower(b,frame,floors,name,supports);
            supports.Add(new SkylineSupport(groundFlight,decks[1]));
            for(int level=2;level<=floors;level++)
            {
                float side=level%2==0?-entry:entry;
                float height=TowerDeck(level);
                var low=new Vector3(x-TowerLane,TowerDeck(level-1),z+side*TowerHalfDepth);
                var high=new Vector3(x-TowerLane,height,z+side*outer);
                var flight=b.Ramp(name+" flight "+level,low,high,3.5f,true);
                var turn=b.Box(name+" landing "+level,new Vector3(x,height-.2f,z+side*(outer+1.2f)),new Vector3(9.1f,.4f,2.4f),5);
                if(Mathf.Abs(x)==31&&(level==5||level==6))
                {
                    b.Rail(turn,-2.73f,side*1.06f,.2f,3.3f);
                    b.Rail(turn,2.73f,side*1.06f,.2f,3.3f);
                }
                else b.Rail(turn,0,side*1.06f,.2f,8.8f);
                var gallery=b.Box(name+" gallery "+level,new Vector3(x+TowerLane,height-.2f,z+side*(TowerHalfDepth+TowerRun*.5f)),
                    new Vector3(3.5f,.4f,TowerRun),2);
                supports.Add(new SkylineSupport(flight,decks[level-1]));
                supports.Add(new SkylineSupport(turn,decks[level]));
                supports.Add(new SkylineSupport(gallery,decks[level]));
            }
            return decks;
        }

        static MayhemModule[] SectionTower(Builder b,MayhemModule frame,int floors,string name,List<SkylineSupport> supports)
        {
            const int floorsPerSection=2;
            var decks=new MayhemModule[floors+1];
            MayhemModule lower=null;
            for(int first=1;first<=floors;first+=floorsPerSection)
            {
                int last=Mathf.Min(first+floorsPerSection-1,floors);
                float bottom=TowerDeck(first-1)-.4f;
                float top=last==floors?TowerDeck(last)+3:TowerDeck(last)-.4f;
                var section=b.Module(name+" tower section "+first,frame.position+Vector3.up*bottom);
                foreach(var shape in frame.shapes)
                {
                    float low=Mathf.Max(bottom,shape.center.y-shape.size.y*.5f);
                    float high=Mathf.Min(top,shape.center.y+shape.size.y*.5f);
                    if(high-low<.001f)continue;
                    var center=new Vector3(shape.center.x,(low+high)*.5f-bottom,shape.center.z);
                    b.Part(section,center,new Vector3(shape.size.x,high-low,shape.size.z),shape.finish);
                }
                supports.Add(new SkylineSupport(section,lower));
                for(int level=first;level<=last;level++)decks[level]=section;
                lower=section;
            }
            return decks;
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
            public MayhemModule Ramp(string purpose,Vector3 low,Vector3 high,float width=3,bool raised=false)
            {
                if(high.y<=low.y)throw new ArgumentException("Ramp must rise.");
                var horizontal=Vector3.ProjectOnPlane(high-low,Vector3.up);
                var heading=Quaternion.LookRotation(horizontal,Vector3.up);
                float run=horizontal.magnitude,rise=high.y-low.y;
                var center=(low+high)*.5f;var m=Module(purpose,center);
                float half=width*.5f,z=run*.5f,bottom=-rise*.5f-.05f;
                float lowerEdge=raised?-rise*.5f-.28f:bottom,upperEdge=raised?rise*.5f-.28f:bottom;
                var vertices=new[]{new Vector3(-half,lowerEdge,-z),new Vector3(half,lowerEdge,-z),new Vector3(half,-rise*.5f,-z),new Vector3(-half,-rise*.5f,-z),new Vector3(-half,upperEdge,z),new Vector3(half,upperEdge,z),new Vector3(half,rise*.5f,z),new Vector3(-half,rise*.5f,z)};
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
