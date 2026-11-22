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
            map.Crossing();
            map.Breakaway();
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
            int count = Mathf.Max(1, Mathf.CeilToInt((top-Bed)/4));
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
        void Flight(string name,Vector3 from,Vector3 to,float width,int parts,bool stairs=false)
        {
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
                float pitch=stairs?0:rise;
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
                    float sole=mid.y+t*pitch-Deck-.55f-Mathf.Abs(pitch)*depth/(2*length);
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
                        Flight(region+(edge==-side?" direct":" outer")+" "+row,a,b,
                            edge==-side?2.9f:2.65f,1);
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
                        float x=p.x+edge*(3.6f+j*2.2f);
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
            for(int n=0;n<7;n++)
            {
                float x=Mathf.Lerp(backWest.x-2,backEast.x+2,n/6f);
                float h=backWest.y-Bed+2.8f+(n*5%7)*.87f;
                Rock(region+" summit fin",new Vector3(x,Bed+h/2,backWest.z+4.6f+(n%2)*1.4f),
                    new Vector3(2.5f,h,3.1f),n%2==0?-12:9,0,1.3f,side<0?.68f:.16f,.52f);
            }
        }

        void Stair(string name,Vector3 from,Vector3 to)
        {
            // Join the inside-facing edge of each landing. Use long treads
            // at half-scale survivor step height.
            Vector3 line=to-from;
            Vector3 flat=new Vector3(line.x,0,line.z);
            Vector3 axis=flat.normalized;
            float fraction=1.65f/Mathf.Abs(line.z);
            Vector3 a=from+flat*fraction, b=to-flat*fraction;
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
                float t=j==0?-.35f:.35f;
                var p=middle+axis*(length*t);
                float y=middle.y+t*rise-.325f;
                Pier(name+" leg "+j,p,y-.78f,1.75f,1.25f,yaw);
                Rock(name+" shoe "+j,new Vector3(p.x,y-.39f,p.z),new Vector3(2,.78f,1.55f),
                    yaw,0,rise*1.55f/length,0,1);
            }
            for(int i=0;i<count;i++)
            {
                var p=Vector3.Lerp(a,b,(i+.5f)/count);
                Rock(name+" tread "+i,p-Vector3.up*.1025f,new Vector3(2.1f,.205f,run-.006f),
                    yaw,step,0,0,1);
            }
        }

        void Breakaway()
        {
            var high=Node(1,2,-1);
            var low=high+new Vector3(-10.8f,-3,0);
            Landing("Spire overlook",low);
            Flight("Spire peeling slab",low,high,2.95f,1);
            // Only the middle of this independent ledge lies beneath the moving
            // slab. Its two shoes stand outside the falling slab's bearing stacks.
            var mid=(low+high)*.5f + new Vector3(0,-3.1f,-1.5f);
            Rock("Spire lower catch",mid,new Vector3(1.65f,.7f,5.4f));
            for(int j=-1;j<=1;j+=2)
                Pier("Catch leg "+j,mid+Vector3.forward*(j*1.8f),mid.y-.35f,1.32f,1.4f);
        }

        void Crossing()
        {
            var a=Node(-1,3,1);
            var b=Node(1,3,-1);
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
                Pier("Bridge pier "+i,p,a.y-Deck-.7f,w*.88f,3.1f);
                Rock("Bridge cap "+i,p-Vector3.up*(Deck+.35f),new Vector3(w,.7f,3.5f));
            }
            for(int i=0;i<3;i++)
                Rock("Bridge span "+i,new Vector3(start+(i+.5f)*span,a.y-Deck/2,a.z),
                    new Vector3(span-.04f,Deck,3.7f),0,0,0,0,1);
        }
    }
}
