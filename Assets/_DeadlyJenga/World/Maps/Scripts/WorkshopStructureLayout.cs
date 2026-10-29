using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public enum WorkshopStructureKind { Crown, Terraces, Viaduct, Megakeeps, CrownGrand, TerracesGrand, ViaductGrand, MegakeepsGrand, Archipelago }

    public readonly struct WorkshopMember
    {
        public readonly Vector3 Center, Size;
        public readonly CastleSection Section;
        public readonly string Purpose;
        public WorkshopMember(Vector3 center, Vector3 size, CastleSection section, string purpose)
        { Center = center; Size = size; Section = section; Purpose = purpose; }
        public Bounds Bounds => new Bounds(Center, Size);
    }

    // Ordinary, unconnected rigid members. All coordinates are measured from the ground top.
    public static class WorkshopStructureLayout
    {
        public static readonly string[] Names = { "Crown Workshop", "Terrace Workshop", "Viaduct Workshop", "Megakeep Workshop", "Crown Constellation", "Terrace Metropolis", "Skywater Viaduct", "Grand Hollow Keeps", "Candy Archipelago" };
        public static List<WorkshopMember> Build(WorkshopStructureKind kind)
        {
            if (!Enum.IsDefined(typeof(WorkshopStructureKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            var b = new Builder();
            switch (kind)
            {
                case WorkshopStructureKind.Crown: b.Crown(Vector2.zero, 3, true); break;
                case WorkshopStructureKind.Terraces: b.Terraces(Vector2.zero, 5, 6); break;
                case WorkshopStructureKind.Viaduct: b.Viaduct(Vector2.zero, 5, 3, false); break;
                case WorkshopStructureKind.Megakeeps: b.KeepChain(Vector2.zero, 2, 2, 3); break;
                case WorkshopStructureKind.CrownGrand:
                    for (int x = 0; x < 3; x++) for (int z = 0; z < 2; z++) b.Crown(new Vector2((x-1)*58, (z-.5f)*58), 4+(x+z)%3, x==0);
                    for (int x = 0; x < 2; x++) for (int z = 0; z < 2; z++) for (int side=-1;side<=1;side+=2)
                        b.Bridge(new Vector2((x-1)*58+14,(z-.5f)*58+side*14),new Vector2(x*58-14,(z-.5f)*58+side*14),18,4,3);
                    for(int x=0;x<3;x++)for(int side=-1;side<=1;side+=2)b.Bridge(new Vector2((x-1)*58+side*14,-15),new Vector2((x-1)*58+side*14,15),12,4,3);
                    break;
                case WorkshopStructureKind.TerracesGrand: b.Terraces(Vector2.zero, 9, 6); break;
                case WorkshopStructureKind.ViaductGrand: b.Viaduct(Vector2.zero, 9, 4, true); break;
                case WorkshopStructureKind.MegakeepsGrand: b.KeepChain(Vector2.zero, 3, 3, 5); break;
                case WorkshopStructureKind.Archipelago:
                    b.Terraces(Vector2.zero,5,6);
                    b.Crown(new Vector2(0,-76),5,true);
                    b.KeepChain(new Vector2(0,76),2,1,4);
                    b.Viaduct(new Vector2(-76,0),3,2,false);
                    b.Viaduct(new Vector2(76,0),3,2,false);
                    b.TransferPier(new Vector2(-14,-40),18); b.TransferPier(new Vector2(14,-40),18);
                    b.Bridge(new Vector2(-14,-62),new Vector2(-14,-40),12,4,3);
                    b.Bridge(new Vector2(14,-62),new Vector2(14,-40),12,4,3);
                    // These low recovery streets are grounded prisms, rather than floating connectors.
                    b.Street(new Vector2(-48,0),new Vector2(-36,0),2,4);
                    b.Street(new Vector2(36,4),new Vector2(48,4),2,4);
                    b.Street(new Vector2(4,36),new Vector2(4,60),2,4);
                    b.Street(new Vector2(0,-48),new Vector2(0,-36),2,4);
                    break;
            }
            return b.Members;
        }
        public static Bounds Bounds(IReadOnlyList<WorkshopMember> members)
        {
            var bounds = new Bounds(Vector3.zero,Vector3.zero);
            foreach(var m in members) bounds.Encapsulate(m.Bounds);
            return bounds;
        }
        sealed class Builder
        {
            public readonly List<WorkshopMember> Members=new List<WorkshopMember>();
            void Box(Vector2 p,float bottom,Vector3 size,CastleSection section,string purpose)
            { Members.Add(new WorkshopMember(new Vector3(p.x,bottom+size.y*.5f,p.y),size,section,purpose)); }
            public void Street(Vector2 a,Vector2 c,float height,float width)
            {var d=c-a;Box((a+c)*.5f,0,new Vector3(Mathf.Abs(d.x)+width,height,Mathf.Abs(d.y)+width),CastleSection.Wall,"Grounded recovery street");}
            void Pylon(Vector2 p,int tiers,float width=8)
            {
                for(int i=0;i<tiers;i++)
                {
                    Box(p,i*6,new Vector3(width,5,width),CastleSection.Corner,"Load-bearing pylon");
                    Box(p,i*6+5,new Vector3(width+2,1,width+2),CastleSection.Wall,"Rest and bearing cap");
                }
                foreach(int side in new[]{-1,1})Box(p+new Vector2(side*(width*.5f+.55f),width*.5f+.55f),tiers*6,new Vector3(.8f,1,.8f),CastleSection.Keep,"Loose summit cover");
            }
            void Stairs(Vector2 p,float outerEdge,int height,bool alongX)
            {
                for(int i=1;i<=height;i++)
                {
                    var offset=new Vector2(outerEdge+1.5f*(height-i)+.75f,0);
                    if(!alongX)offset=new Vector2(0,offset.x);
                    Box(p+offset,0,alongX?new Vector3(1.5f,i,3):new Vector3(3,i,1.5f),CastleSection.Wall,"Grounded approach step");
                }
            }
            public void Bridge(Vector2 a,Vector2 c,float bottom,float inset,float width)
            {
                var delta=c-a;
                if(Mathf.Abs(delta.x)>.001f&&Mathf.Abs(delta.y)>.001f)throw new InvalidOperationException("Bearing bridges must be axis aligned.");
                float length=delta.magnitude-2*inset;
                if(length<=0)throw new InvalidOperationException("Bridge has no clear span.");
                Box((a+c)*.5f,bottom,Mathf.Abs(delta.x)>.001f?new Vector3(length,1,width):new Vector3(width,1,length),CastleSection.Keep,"Two-ended loose bridge deck");
            }
            public void Crown(Vector2 origin,int tiers,bool stairs)
            {
                for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Pylon(origin+new Vector2(x*14,z*14),tiers);
                for(int level=1;level<=tiers;level+=2)for(int side=-1;side<=1;side+=2)
                {
                    Bridge(origin+new Vector2(-14,side*14),origin+new Vector2(14,side*14),level*6,4,3);
                    Bridge(origin+new Vector2(side*14,-14),origin+new Vector2(side*14,14),level*6,4,3);
                }
                if(stairs){Stairs(origin+new Vector2(14,-14),5,6,true);Stairs(origin+new Vector2(-14,14),5,6,false);}
            }
            public void TransferPier(Vector2 p,int height){Pylon(p,height/6);}
            public void Terraces(Vector2 origin,int cells,float pitch)
            {
                // Width 12, spacing 14: a genuine two-metre gap between separate rooftop stacks.
                int half=cells/2;
                for(int x=-half;x<=half;x++)for(int z=-half;z<=half;z++)
                {
                    if(x==0&&z==0)continue;
                    int tiers=half+1-Mathf.Max(Mathf.Abs(x),Mathf.Abs(z));
                    Vector2 p=origin+new Vector2(x*14,z*14);
                    for(int level=0;level<tiers;level++)Box(p,level*pitch,new Vector3(12,pitch,12),level%2==0?CastleSection.Corner:CastleSection.Wall,"Solid terrace mass");
                    if((x+z)%2==0)Box(p+new Vector2(4,4),tiers*pitch,new Vector3(1.5f,1,1.5f),CastleSection.Keep,"Loose rooftop cover");
                }
                Stairs(origin+new Vector2(half*14,0),6,(int)pitch,true);
                Stairs(origin+new Vector2(0,half*14),6,(int)pitch,false);
            }
            public void Viaduct(Vector2 origin,int length,int rows,bool grand)
            {
                var heights=new int[length,rows];
                for(int x=0;x<length;x++)for(int z=0;z<rows;z++)
                {
                    int tiers=grand?3+(x/2+z)%4:2+(x+z)%2;heights[x,z]=tiers;
                    Pylon(origin+new Vector2((x-(length-1)*.5f)*22,(z-(rows-1)*.5f)*18),tiers,6);
                }
                for(int x=0;x<length;x++)for(int z=0;z<rows;z++)
                {
                    Vector2 p=origin+new Vector2((x-(length-1)*.5f)*22,(z-(rows-1)*.5f)*18);
                    if(x+1<length)for(int level=1;level<=Mathf.Min(heights[x,z],heights[x+1,z]);level+=2)Bridge(p,p+Vector2.right*22,level*6,3,3);
                    if(z+1<rows&&(x%2==0||x==length-1))Bridge(p,p+Vector2.up*18,6,3,3);
                }
                Stairs(origin+new Vector2((length-1)*11,-(rows-1)*9),4,6,true);
            }
            void Keep(Vector2 p,int tiers,bool stairs)
            {
                for(int level=0;level<tiers;level++)
                {
                    float y=level*8;
                    for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Box(p+new Vector2(x*6,z*6),y,new Vector3(3,5,3),CastleSection.Corner,"Megakeep corner pier");
                    for(int side=-1;side<=1;side+=2)
                    {
                        Box(p+new Vector2(0,side*6),y+5,new Vector3(15,1,3),CastleSection.Wall,"Lower bearing lintel");
                        Box(p+new Vector2(side*6,0),y+6,new Vector3(3,1,15),CastleSection.Wall,"Cross bearing lintel");
                        Box(p+new Vector2(0,side*6),y+7,new Vector3(15,1,3),CastleSection.Keep,"Open floor sector");
                        Box(p+new Vector2(side*6,0),y+7,new Vector3(3,1,9),CastleSection.Keep,"Open floor sector");
                    }
                }
                if(stairs)Stairs(p,7.5f,8,false);
            }
            public void KeepChain(Vector2 origin,int columns,int rows,int height)
            {
                var tiers=new int[columns,rows];
                for(int x=0;x<columns;x++)for(int z=0;z<rows;z++)
                {tiers[x,z]=height+(x+z)%2;Keep(origin+new Vector2((x-(columns-1)*.5f)*34,(z-(rows-1)*.5f)*34),tiers[x,z],z==rows-1);}
                for(int x=0;x<columns;x++)for(int z=0;z<rows;z++)
                {
                    var p=origin+new Vector2((x-(columns-1)*.5f)*34,(z-(rows-1)*.5f)*34);
                    if(x+1<columns)for(int level=1;level<=Mathf.Min(tiers[x,z],tiers[x+1,z]);level+=2)Bridge(p,p+Vector2.right*34,level*8,6,3);
                    if(z+1<rows)for(int level=1;level<=Mathf.Min(tiers[x,z],tiers[x,z+1]);level+=2)Bridge(p,p+Vector2.up*34,level*8,6,3);
                }
            }
        }
    }
}
