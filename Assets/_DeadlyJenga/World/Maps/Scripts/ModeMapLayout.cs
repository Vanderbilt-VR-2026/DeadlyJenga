using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Every bridge has positive bearing on two grounded, cross-stacked towers.
    public static class ModeMapLayout
    {
        public static readonly string[] Presets={"ctf","infection","survival","territory","koth","tickets"};
        public static bool Known(string preset)=>Array.IndexOf(Presets,preset)>=0;
        public static int Height(string preset)=>preset=="infection"?48:preset=="koth"?32:preset=="survival"?20:preset=="territory"?16:preset=="ctf"?18:12;
        public static Vector2 Size(CastleSettings s)
        {
            float pitch=Pitch(s),length=s.blockSize.x;
            return s.layoutPreset=="ctf"?new Vector2(pitch*8+length,pitch*2+3*length):s.layoutPreset=="tickets"?new Vector2(pitch*8+length,pitch*2+length):s.layoutPreset=="infection"?Vector2.one*(pitch*2+length):s.layoutPreset=="survival"?Vector2.one*(pitch*3+length):Vector2.one*(pitch*4+length);
        }
        static float Pitch(CastleSettings s)=>2*s.blockSize.x-s.blockSize.z;
        readonly struct Tower
        {public readonly int x,z,height;public Tower(int x,int z,int height){this.x=x;this.z=z;this.height=height;}}
        public static List<CastlePlacement> Build(CastleSettings s)
        {
            if(!Known(s.layoutPreset))throw new ArgumentException("Unknown mode layout.");
            var result=new List<CastlePlacement>(720);var towers=new List<Tower>();string preset=s.layoutPreset;
            int width=preset=="ctf"||preset=="tickets"?9:preset=="infection"?3:preset=="survival"?4:5;
            int depth=preset=="ctf"||preset=="tickets"?3:width;
            for(int x=0;x<width;x++)for(int z=0;z<depth;z++)
            {
                int h;
                if(preset=="ctf"||preset=="tickets")
                {int end=Mathf.Abs(x-4);h=preset=="ctf"?end==4?17:end==3?8:end==2?6:4:end>=2?end==4?12:end==3?10:8:4;}
                else if(preset=="infection")h=x==1&&z==1?48:x==1||z==1?40:28;
                else if(preset=="survival")h=12+4*((x+z)%3);
                else if(preset=="territory")h=x==2&&z==2?16:x>0&&x<4&&z>0&&z<4?12:8;
                else h=x==2&&z==2?32:x>0&&x<4&&z>0&&z<4?18:6;
                towers.Add(new Tower(x,z,h));
                AddTower(result,s,Center(s,x,z,width,depth),h,x==width/2&&z==depth/2?CastleSection.Keep:(x==0||x==width-1)&&(z==0||z==depth-1)?CastleSection.Corner:CastleSection.Wall);
            }
            int[] levels=preset=="infection"?new[]{2,10,20,26}:preset=="territory"?new[]{2,6}:preset=="koth"?new[]{2,6,12,18}:new[]{2,6};
            foreach(var a in towers)foreach(int level in levels)
            {
                if(a.x+1<width){var b=towers[(a.x+1)*depth+a.z];Bridge(result,s,Center(s,a.x,a.z,width,depth),Center(s,b.x,b.z,width,depth),a.height,b.height,level,false);}
                if(a.z+1<depth){var b=towers[a.x*depth+a.z+1];Bridge(result,s,Center(s,a.x,a.z,width,depth),Center(s,b.x,b.z,width,depth),a.height,b.height,level+1,true);}
            }
            if(preset=="ctf")
            {
                AddCarrierStairs(result,s);
                AddCarrierLandings(result,s);
                foreach(int end in new[]{0,8})for(int z=0;z<2;z++)
                    Bridge(result,s,Center(s,end,z,width,depth),Center(s,end,z+1,width,depth),17,17,17,true);
            }
            if(result.Count>800)throw new InvalidOperationException("Mode map exceeds the authored block budget.");return result;
        }
        static void AddCarrierStairs(List<CastlePlacement> output,CastleSettings s)
        {
            // Solid grounded columns leave every tread open above for jumping with a flag.
            float pitch=Pitch(s),width=s.blockSize.z;
            for(int side=-1;side<=1;side+=2)for(int step=0;step<16;step++)
            {
                var center=new Vector3(side*(4*pitch-15*width+step*width),0,side*(pitch+s.blockSize.x));
                for(int course=0;course<=step;course++)
                    output.Add(new CastlePlacement(center+Vector3.up*((course+.5f)*s.blockSize.y),Quaternion.Euler(0,90,0),CastleSection.Wall,course));
            }
        }
        static void AddCarrierLandings(List<CastlePlacement> output,CastleSettings s)
        {
            float pitch=Pitch(s),width=s.blockSize.z;
            for(int side=-1;side<=1;side+=2)
            {
                float roof=3*pitch,stair=4*pitch-8*width,z=pitch+width;
                output.Add(new CastlePlacement(new Vector3(side*roof,8.5f*s.blockSize.y,side*z),Quaternion.identity,CastleSection.Keep,8));
                output.Add(new CastlePlacement(new Vector3(side*stair,8.5f*s.blockSize.y,side*(pitch+s.blockSize.x-width)),Quaternion.Euler(0,90,0),CastleSection.Keep,8));
                output.Add(new CastlePlacement(new Vector3(side*(roof+stair)*.5f,9.5f*s.blockSize.y,side*z),Quaternion.identity,CastleSection.Keep,9));
            }
        }
        static Vector3 Center(CastleSettings s,int x,int z,int width,int depth)=>new Vector3((x-(width-1)*.5f)*Pitch(s),0,(z-(depth-1)*.5f)*Pitch(s));
        static void AddTower(List<CastlePlacement> output,CastleSettings s,Vector3 center,int courses,CastleSection section)
        {
            float offset=(s.blockSize.x-s.blockSize.z)*.5f;
            for(int c=0;c<courses;c++){var rotation=Quaternion.Euler(0,(c%2)*90,0);for(int side=-1;side<=1;side+=2)output.Add(new CastlePlacement(center+Vector3.up*((c+.5f)*s.blockSize.y)+rotation*new Vector3(0,0,side*offset),rotation,section,c));}
        }
        static void Bridge(List<CastlePlacement> output,CastleSettings s,Vector3 a,Vector3 b,int firstHeight,int secondHeight,int course,bool alongZ)
        {
            if(firstHeight<course||secondHeight<course)return;
            output.Add(new CastlePlacement((a+b)*.5f+Vector3.up*((course+.5f)*s.blockSize.y),Quaternion.Euler(0,alongZ?90:0,0),CastleSection.Keep,course));
        }
    }
}
