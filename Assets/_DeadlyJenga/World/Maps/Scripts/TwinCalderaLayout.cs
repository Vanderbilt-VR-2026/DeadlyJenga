using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // A station is a landing with its own column. Flights sit between stations on
    // two independent shoes. Dimensions are in metres, relative to the lava.
    internal sealed class TwinCalderaLayout
    {
        internal const string Preset = "twin-caldera";
        const float Bed = -3, LandingWidth = 3.6f, Deck = .85f;
        const float CrossingRise = 4f;
        internal readonly List<Piece> Pieces = new();

        internal struct Piece
        {
            internal string name;
            internal CastlePlacement placement;
            internal float bottomRise, topRise, lean, taper;
        }

        internal static TwinCalderaLayout Create()
        {
            var map = new TwinCalderaLayout();
            map.Cone(-1);
            map.Cone(1);
            map.ShieldSpine();
            map.Crossing();
            map.Breakaway();
            map.Aprons();
            return map;
        }

        internal static List<CastlePlacement> Build() => Create().Pieces.ConvertAll(p => p.placement);

        void Rock(string name, Vector3 center, Vector3 size, float yaw = 0,
            float bottomRise = 0, float topRise = 0, float lean = 0, float taper = .96f)
        {
            Pieces.Add(new Piece { name = name, placement = new CastlePlacement(center,
                Quaternion.Euler(0,yaw,0), CastleSection.Wall, Mathf.Max(0,Mathf.FloorToInt(center.y)), size),
                bottomRise = bottomRise, topRise = topRise, lean = lean, taper = taper });
        }

        void Pier(string name, Vector3 point, float top, float width, float depth, float yaw = 0)
        {
            int count = Mathf.Max(1, Mathf.CeilToInt((top-Bed)/5));
            float h = (top-Bed)/count;
            for (int n = 0; n < count; n++)
            {
                float spread = 1 + .20f*(count-1-n)/count;
                Rock(name+" bearing "+n, new Vector3(point.x,Bed+(n+.5f)*h,point.z),
                    new Vector3(width*spread,h,depth*spread),yaw);
            }
        }

        void Landing(string name, Vector3 point)
        {
            Pier(name,point,point.y-Deck,2.55f,2.65f);
            Rock(name,point-Vector3.up*(Deck/2),new Vector3(LandingWidth,Deck,LandingWidth));
        }

        static Vector3 Node(int side,int row,int edge)
        {
            float width = (side<0 ? 14f : 12f) - row*1.28f;
            float center = side*(23 + .28f*row);
            float height = row==3 ? 10.8f : row<3 ? 1.8f+3*row : 10.8f+(row-3)*(side<0?2.6f:3.4f);
            return new Vector3(center+edge*width,height,-21+row*7);
        }

        // The top and underside share a slope. Each shoe has a horizontal sole
        // and a sloped crown bearing directly against the slab underside.
        void Flight(string name,Vector3 from,Vector3 to,float width,int parts,float shoeDrop=0)
        {
            // Square-cut ends meet both landings flush. The landing width
            // accommodates the lateral setback between successive levels.
            if(Mathf.Abs(to.x-from.x)<1.8f && Mathf.Abs(to.z-from.z)>3)
                from.x=to.x=(from.x+to.x)/2;
            var d = to-from;
            var horizontal = new Vector3(d.x,0,d.z);
            var axis = horizontal.normalized;
            float trim = LandingWidth*.5f/Mathf.Max(Mathf.Abs(axis.x),Mathf.Abs(axis.z));
            var start=from+axis*(trim+.025f);
            var end=to-axis*(trim+.025f);
            float yaw=Mathf.Atan2(axis.x,axis.z)*Mathf.Rad2Deg;
            float length=Vector3.Dot(end-start,axis)/parts;
            float rise=d.y/parts;
            for(int i=0;i<parts;i++)
            {
                var a=Vector3.Lerp(start,end,(float)i/parts);
                var b=Vector3.Lerp(start,end,(i+1f)/parts);
                var mid=(a+b)*.5f;
                float pitch=rise;
                string label=name+" "+i;
                Rock(label,mid-Vector3.up*(Deck/2),new Vector3(width,Deck,length-.025f),yaw,pitch,pitch,0,1);
                // A pair of pulled bearings can tip a long slab. Short stair treads
                // each sit on a single broad, independently removable riser.
                int shoes=length>3.5f?2:1;
                for(int j=0;j<shoes;j++)
                {
                    float t=shoes==1?0:(j==0?-.31f:.31f);
                    var at=mid+axis*(t*length);
                    float depth=length*(shoes==1?.74f:.26f);
                    float sole=mid.y+t*pitch-Deck-.55f-Mathf.Abs(pitch)*depth/(2*length)-shoeDrop;
                    Pier(label+" shoe "+j,at,sole,width*.68f,depth*.90f,yaw);
                    float h=mid.y+t*pitch-Deck-sole;
                    Rock(label+" seat "+j,new Vector3(at.x,sole+h/2,at.z),
                        new Vector3(width*.85f,h,depth),yaw,0,pitch*depth/length,0,1);
                }
            }
        }

        void Cone(int side)
        {
            string region=side<0?"Shield":"Spire";
            for(int row=0;row<=5;row++)
            {
                var left=Node(side,row,-1);
                var right=Node(side,row,1);
                Landing(region+" west landing "+row,left);
                Landing(region+" east landing "+row,right);
                // Leave the two upper courses open over the vent.
                if(row<5) Flight(region+" traverse "+row,left,right,3.25f,row%2==0?3:2);
                if(row<5)
                {
                    for(int edge=-1;edge<=1;edge+=2)
                    {
                        var a=Node(side,row,edge);
                        var b=Node(side,row+1,edge);
                        // The inner flank is a segmented direct climb. The opposite
                        // flank has longer stair runs diagonally across the terraces.
                        if (edge == -side && row == 3)
                        {
                            a.y += CrossingRise;
                            // The upper shoulder has its own connection to the rim.
                            Flight(region+" bridge approach",a,b,2.65f,1);
                        }
                        else
                        {
                            Flight(region+(edge==-side?" direct":" outer")+" "+row,a,b,
                                edge==-side?2.9f:2.65f,1);
                        }
                    }
                    // Stair flights run diagonally across the width, clear of the two side climbs.
                    if(row<4)
                    {
                        var a=Node(side,row,row%2==0?-1:1);
                        var b=Node(side,row+1,row%2==0?1:-1);
                        Stair(region+" stair "+row,a,b);
                    }
                }
                for(int edge=-1;edge<=1;edge+=2)
                {
                    if(edge==-side && (row==3 || row==2))continue;
                    var p=Node(side,row,edge);
                    // Outer ribs taper toward an offset tip, with a level rooted foot.
                    for(int j=0;j<2;j++)
                    {
                        float x=p.x+edge*(3.9f+j*3.0f);
                        float z=p.z+(j==0?.3f:2.3f);
                        float h=p.y-Bed+(row>=4?3.4f:1.3f)-j*1.7f;
                        Rock(region+" flank rib",new Vector3(x,Bed+h/2,z),
                            new Vector3(2.4f,h,3.4f),edge*8,0,.65f, -edge*.27f,.61f);
                    }
                }
            }
            // A high horseshoe wraps the back of the open crater.
            var west=Node(side,5,-1);
            var east=Node(side,5,1);
            var backWest=west+new Vector3(1,2,7);
            var backEast=east+new Vector3(-1,2,7);
            Landing(region+" back west",backWest);
            Landing(region+" back east",backEast);
            Flight(region+" rim west",west,backWest,2.9f,1);
            Flight(region+" rim east",east,backEast,2.9f,1);
            Flight(region+" rim crown",backWest,backEast,3,2);
            int fins=side<0?5:7;
            for(int n=0;n<fins;n++)
            {
                float x=Mathf.Lerp(backWest.x-4,backEast.x+4,n/(fins-1f));
                float z=backWest.z+4.8f+(n%2)*4.45f;
                float shoulder=backWest.y-2.6f+(n%3)*.64f;
                float width=side<0?3.55f:2.65f;
                var p=new Vector3(x,0,z);
                Pier(region+" crest pedestal "+n,p,shoulder,width*1.12f,3.5f);
                float[] crestScale = {1.01f, 1.16f, .91f, 1.19f, 1.07f, 1.04f, .91f};
                float height=((side<0?5.9f:8.2f)+(n*5%7)*.65f)*crestScale[n];
                width *= side < 0 ? 1.20f : 1.04f;
                Rock(region+" summit fin "+n,new Vector3(x,shoulder+height/2,z),
                    new Vector3(width,height,3.3f),n%2==0?-9:8,0,1.65f,side<0?.85f:.17f,.42f);
            }
        }

        void ShieldSpine()
        {
            // Radial slabs rise over the front terraces. Their sloping shoes sit
            // on the traverses themselves, leaving a second passage underneath.
            // The first section emerges from a footing below the lowest apron.
            var stations=new List<Vector3>();
            for(int i=-1;i<=4;i++)
            {
                float z=-21+i*7;
                float y=i<0?1.25f:Node(-1,i,1).y+3.05f;
                stations.Add(new Vector3(-29.8f+i*2.55f,y,z));
            }
            for(int i=0;i<stations.Count-1;i++)
            {
                var a=stations[i];var b=stations[i+1];
                var line=b-a;var flat=new Vector3(line.x,0,line.z);
                var axis=flat.normalized;
                float length=flat.magnitude, slope=line.y/length;
                float yaw=Mathf.Atan2(axis.x,axis.z)*Mathf.Rad2Deg;
                // Retain the thinner central slab at the switchback crossing.
                float slab=i==2?.98f:1.45f;
                float width=i==2?3.75f:4.3875f;
                // Consecutive slabs meet with a 5 cm joint so feet cannot drop through.
                float run=length-.05f;
                Rock("Shield radial slab "+i,(a+b)*.5f-Vector3.up*(slab/2),
                    new Vector3(width,slab,run),yaw,slope*run,slope*run,0,1);
                for(int j=0;j<2;j++)
                {
                    var edge=j==0?a:b;
                    float offset=j==0?.74f:-.74f;
                    var shoe=edge+axis*offset;
                    float crown=edge.y+offset*slope-slab;
                    float baseY=i==0 && j==0?Bed:edge.y-3.05f;
                    float h=crown-baseY;
                    Rock("Shield radial seat "+i+"-"+j,new Vector3(shoe.x,baseY+h/2,shoe.z),
                        new Vector3(2.6f,h,1.03f),yaw,0,slope*1.03f,0,.98f);
                }
            }
        }

        void Stair(string name,Vector3 from,Vector3 to)
        {
            // Join the inside-facing edge of each landing. Use long treads
            // at half-scale survivor step height.
            float direction=Mathf.Sign(to.x-from.x);
            Vector3 axis=Vector3.right*direction;
            float laneZ=(from.z+to.z)/2;
            var lower=from+new Vector3(direction*3.65f,0,3.12f);
            var upper=to+new Vector3(-direction*3.65f,0,-3.12f);
            void Turn(Vector3 pt,string suffix)
            {
                Pier(name+suffix,pt,pt.y-Deck,1.6f,2.3f);
                Rock(name+suffix,pt-Vector3.up*(Deck/2),new Vector3(2.15f,Deck,2.94f),0,0,0,0,1);
            }
            Turn(lower," lower turn");Turn(upper," upper turn");
            var a=new Vector3(lower.x+direction*1.09f,from.y,laneZ);
            var b=new Vector3(upper.x-direction*1.09f,to.y,laneZ);
            var line=b-a;
            float length=Vector3.Dot(b-a,axis);
            float rise=line.y, yaw=Mathf.Atan2(axis.x,axis.z)*Mathf.Rad2Deg;
            int count=Mathf.CeilToInt(rise/.24f);
            float step=rise/count, run=length/count;
            // Two horizontal bearing seats carry the inclined stringer. Each
            // wedge tread has a matching inclined sole and level walking face.
            var middle=(a+b)/2-Vector3.up*.53f;
            Rock(name+" stringer",middle,new Vector3(2.2f,.65f,length),yaw,rise,rise,0,1);
            for(int j=0;j<2;j++)
            {
                float t=j==0?-.28f:.28f;
                var p=middle+axis*(length*t);
                float y=middle.y+t*rise-.325f;
                Pier(name+" leg "+j,p,y-.78f,1.75f,1.25f,yaw);
                Rock(name+" shoe "+j,new Vector3(p.x,y-.39f,p.z),new Vector3(2,.78f,1.55f),
                    yaw,0,rise*1.55f/length,0,1);
            }
            for(int i=0;i<count;i++)
            {
                var p=Vector3.Lerp(a,b,(i+.5f)/count);
                // Survivors only step up a few centimetres without jumping, so each
                // tread's walking face continues the incline instead of a flat riser.
                Rock(name+" tread "+i,p-Vector3.up*.1025f,new Vector3(2.1f,.205f,run-.006f),
                    yaw,step,step,0,1);
            }
        }

        void Breakaway()
        {
            var high=Node(1,2,-1);
            var low=high+new Vector3(-10.8f,-3,0);
            Landing("Spire overlook",low);
            // Tall seats on low shoes: once both seats are pulled, the slab drops
            // past the shoes onto the lower catch instead of resting on them.
            Flight("Spire peeling slab",low,high,2.95f,1,1.8f);
            // Only the middle of this independent ledge lies beneath the moving
            // slab. Its two shoes stand outside the falling slab's bearing stacks.
            var mid=(low+high)*.5f + new Vector3(0,-2.7f,-1.5f);
            Rock("Spire lower catch",mid,new Vector3(1.65f,.7f,5.4f));
            for(int j=-1;j<=1;j+=2)
                Pier("Catch leg "+j,mid+Vector3.forward*(j*1.8f),mid.y-.35f,1.32f,1.4f);
        }

        void Aprons()
        {
            // The low silhouettes from the Blender terraces. Each leaning unit
            // sits on a separate broad footing and leaves space at the arrival.
            ApronBlock("Shield",0,-46.5f,-24.4f,3.6f,4f,4.1f,3.6f,18);
            ApronBlock("Shield",1,-40.8f,-26.1f,4.2f,3.6f,4.2f,2.5f,-24);
            ApronBlock("Shield",2,-35.4f,-30.7f,4.2f,3.5f,4.3f,1.9f,14);
            ApronBlock("Shield",3,-22.6f,-26.2f,4.6f,4.2f,4.5f,2.4f,-14);
            ApronBlock("Shield",4,-15.1f,-25.1f,3.7f,3.4f,4.15f,2.3f,20);
            ApronBlock("Spire",0,45.9f,-18.2f,3.8f,3.6f,4.25f,3.7f,-21);
            ApronBlock("Spire",1,39.3f,-25.7f,4.25f,3.8f,4.3f,3.3f,19);
            ApronBlock("Spire",2,30.5f,-26.2f,4.45f,3.8f,4.4f,2.35f,-10);
            ApronBlock("Spire",3,24.9f,-25.7f,4.1f,3.7f,4.2f,2.0f,29);
            ApronBlock("Spire",4,14.8f,-25.9f,4.1f,3.65f,4.55f,2.6f,-12);
        }

        void ApronBlock(string region,int index,float x,float z,float width,float depth,
            float footHeight,float ribHeight,float yaw)
        {
            string name=region+" apron "+index;
            Rock(name+" foundation",new Vector3(x,Bed+footHeight/2,z),
                new Vector3(width,footHeight,depth),yaw,0,0,0,.96f);
            Rock(name+" buttress",new Vector3(x,Bed+footHeight+ribHeight/2,z),
                new Vector3(width*.80f,ribHeight,depth*.82f),yaw,0,.34f,index%2==0?.16f:-.16f,.72f);
        }

        void Crossing()
        {
            var a=Node(-1,3,1);
            var b=Node(1,3,-1);
            BridgeShoulder("Shield",a);
            BridgeShoulder("Spire",b);
            a.y += CrossingRise;
            b.y += CrossingRise;
            float start=a.x+LandingWidth/2, end=b.x-LandingWidth/2;
            float span=(end-start)/3;
            for(int i=0;i<=3;i++)
            {
                float x=start+span*i;
                // End bearings sit underneath the landing edge and the first span.
                // The two central caps each carry half the ends of adjacent spans.
                if(i==0||i==3) x+=(i==0?.46f:-.46f);
                var p=new Vector3(x,a.y,a.z);
                float w=i==0||i==3?.82f:2.7f;
                if (i == 1 || i == 2) BridgePier(i,p);
                else Pier("Bridge pier "+i,p,a.y-Deck-.7f,w*.88f,3.1f);
                Rock("Bridge cap "+i,p-Vector3.up*(Deck+.35f),new Vector3(w,.7f,3.5f));
            }
            for(int i=0;i<3;i++)
                Rock("Bridge span "+i,new Vector3(start+(i+.5f)*span,a.y-Deck/2,a.z),
                    new Vector3(span-.04f,Deck,3.7f),0,0,0,0,1);
        }

        void BridgeShoulder(string region, Vector3 lower)
        {
            float bearingHeight = (CrossingRise - Deck) / 2;
            for (int course = 0; course < 2; course++)
            {
                var center = lower + Vector3.up * ((course + .5f) * bearingHeight);
                Rock(region+" bridge shoulder bearing "+course,center,
                    new Vector3(2.62f - .22f*course,bearingHeight,2.7f - .16f*course));
            }
            Rock(region+" bridge shoulder",lower + Vector3.up * (CrossingRise - Deck/2),
                new Vector3(LandingWidth,Deck,LandingWidth),0,0,0,0,1);
        }

        void BridgePier(int index, Vector3 top)
        {
            float height = top.y - Deck - .7f - Bed;
            float[] widths = {4.7f, 4f, 3.39f, 2.9f, 3.9f};
            float[] depths = {4.55f, 4f, 3.7f, 3.38f, 3.75f};
            for (int course = 0; course < widths.Length; course++)
            {
                float step = height / widths.Length;
                var center = new Vector3(top.x,Bed + (course + .5f)*step,top.z);
                Rock("Bridge pier "+index+" bearing "+course,center,
                    new Vector3(widths[course],step,depths[course]),0,0,0,0,.96f);
            }
        }
    }
}
