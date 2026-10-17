using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Identical loose blocks throughout. All coordinates are in six-metre block units.
    public static class CathedralLayout
    {
        public static List<CastlePlacement> Build(CastleSettings s)
        {
            var output=new List<CastlePlacement>();float unit=s.blockSize.x/6;
            void Add(float x,float z,int course,float yaw,CastleSection section)
            {output.Add(new CastlePlacement(new Vector3(x*unit,(course+.5f)*s.blockSize.y,z*unit),Quaternion.Euler(0,yaw,0),section,course));}
            void Pair(float x,float z,int courses,CastleSection section,float yaw=0)
            {
                for(int c=0;c<courses;c++)for(int side=-1;side<=1;side+=2)
                {var q=Quaternion.Euler(0,yaw+c%2*90,0);var p=q*new Vector3(0,0,side*2);Add(x+p.x,z+p.z,c,yaw+c%2*90,section);}
            }
            void Lantern(float x,float z,float radius,int sides,int courses,int crown,CastleSection section)
            {
                // Alternate half the sides of a polygon. Broad end bearings leave open windows.
                float step=360f/sides;
                for(int c=0;c<courses;c++)for(int i=0;i<sides/2;i++)
                {float yaw=i*step*2+c%2*step;var q=Quaternion.Euler(0,yaw,0);var p=q*new Vector3(0,0,radius);Add(x+p.x,z+p.z,c,yaw,section);}
                for(int i=0;i<sides/2;i++)
                {
                    float yaw=i*step*2+(courses-1)%2*step;var p=Quaternion.Euler(0,yaw,0)*new Vector3(0,0,radius);
                    int peak=crown-(i%2==0?0:2);
                    // Cross-stacked finials remain centred on the supporting crate below.
                    for(int c=0;c<peak;c++)Add(x+p.x,z+p.z,courses+c,yaw+c%2*90,section);
                }
            }
            void Arcade(float lateral,float first,float last,int courses,bool across,CastleSection section)
            {
                for(int c=0;c<courses;c++)
                {
                    if(c%10==9)
                    {
                        // A continuous gallery replaces this pier course. Six-metre caps bridge
                        // the two-metre bays without overlapping another crate in the same layer.
                        for(float p=first<0?first-2:first;p<last+3;p+=6)for(int side=-1;side<=1;side+=2)
                        {float x=lateral+side*2,z=p;if(across)Add(z,x,c,0,section);else Add(x,z,c,90,section);}
                    }
                    else for(float p=first;p<=last+.01f;p+=8)for(int side=-1;side<=1;side+=2)
                    {
                        var offset=Quaternion.Euler(0,c%2*90,0)*new Vector3(0,0,side*2);
                        float x=lateral+offset.x,z=p+offset.z;
                        if(across)Add(z,x,c,90-c%2*90,section);else Add(x,z,c,c%2*90,section);
                    }
                }
            }
            Lantern(0,0,10.8f,16,80,10,CastleSection.Keep);
            Lantern(0,55,10.8f,16,56,10,CastleSection.Keep);
            Lantern(-17,-55,8.1f,12,56,10,CastleSection.Corner);
            Lantern(17,-55,8.1f,12,56,10,CastleSection.Corner);
            Lantern(-55,0,8.1f,12,44,10,CastleSection.Corner);
            Lantern(55,0,8.1f,12,44,10,CastleSection.Corner);
            foreach(int side in new[]{-1,1})foreach(int end in new[]{-1,1})
            {
                float first=end<0?-40:24,last=end<0?-24:40;
                Arcade(side*17,first,last,40,false,CastleSection.Wall);
                Arcade(side*27,first,last,24,false,CastleSection.Corner);
                Arcade(side*17,first,last,32,true,CastleSection.Wall);
                Pair(side*8,end*24,32,CastleSection.Wall);
                Pair(side*42,end*42,18,CastleSection.Corner,45);
                Pair(side*55,end*17,18,CastleSection.Wall);
                Pair(side*55,end*25,10,CastleSection.Corner);
            }
            foreach(int side in new[]{-1,1})
            {
                Pair(side*16,0,34,CastleSection.Wall);
                Pair(0,side*16,34,CastleSection.Wall);
                Pair(side*69,0,6,CastleSection.Corner);Pair(side*75,0,3,CastleSection.Corner);
                Pair(side*17,-69,6,CastleSection.Corner);Pair(side*17,-75,3,CastleSection.Corner);
            }
            Pair(0,72,6,CastleSection.Corner);Pair(0,78,3,CastleSection.Corner);
            // Corbelled halves narrow a real entrance into a pointed opening, carrying the belfry.
            Pair(-5,-69,10,CastleSection.Wall);Pair(5,-69,10,CastleSection.Wall);
            for(int c=10;c<14;c++)for(int side=-1;side<=1;side+=2)for(int rail=-1;rail<=1;rail+=2)
                Add(side*(4.5f-(c-10)*.5f),-69+rail*2,c,0,CastleSection.Wall);
            for(int c=14;c<30;c++)for(int side=-1;side<=1;side+=2)
            {var p=Quaternion.Euler(0,c%2*90,0)*new Vector3(0,0,side*2);Add(p.x,-69+p.z,c,c%2*90,CastleSection.Wall);}
            return output;
        }
    }
}
