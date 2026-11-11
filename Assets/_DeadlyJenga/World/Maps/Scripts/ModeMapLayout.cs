using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Mode-specific loose-block layouts. CTF uses grounded courts and an optional upper lane.
    public static class ModeMapLayout
    {
        public const int CuratedCtfRevision=2;
        public const float CuratedCtfGroundY=.4f,CuratedCtfTop=4.4f;
        public static readonly string[] Presets={"ctf","infection","survival","territory","koth","tickets"};
        public static bool Known(string preset)=>Array.IndexOf(Presets,preset)>=0||preset==NinjaTempleLayout.Preset;
        public static int Height(string preset)=>preset==NinjaTempleLayout.Preset?NinjaTempleLayout.HeightCourses:preset=="infection"?48:preset=="koth"?32:preset=="survival"?20:preset=="territory"?16:preset=="ctf"?3:12;
        public static Vector2 Size(CastleSettings s)
        {
            if(s.layoutPreset==NinjaTempleLayout.Preset)return NinjaTempleLayout.Size;
            float pitch=Pitch(s),length=s.blockSize.x;
            return s.layoutPreset=="ctf"?new Vector2(88,64):s.layoutPreset=="tickets"?new Vector2(pitch*8+length,pitch*2+length):s.layoutPreset=="infection"?Vector2.one*(pitch*2+length):s.layoutPreset=="survival"?Vector2.one*(pitch*3+length):Vector2.one*(pitch*4+length);
        }
        static float Pitch(CastleSettings s)=>2*s.blockSize.x-s.blockSize.z;
        readonly struct Tower
        {public readonly int x,z,height;public Tower(int x,int z,int height){this.x=x;this.z=z;this.height=height;}}
        public static List<CastlePlacement> Build(CastleSettings s)
        {
            if(!Known(s.layoutPreset))throw new ArgumentException("Unknown mode layout.");
            if(s.layoutPreset==NinjaTempleLayout.Preset)return NinjaTempleLayout.Build(s);
            if(s.layoutPreset=="ctf")return CrownCourtyards();
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
            if(result.Count>800)throw new InvalidOperationException("Mode map exceeds the authored block budget.");return result;
        }
        public static Vector3 CtfHome(int team)
        {
            if(team<0||team>1)throw new ArgumentOutOfRangeException(nameof(team));
            return new Vector3(team==0?-30:30,CuratedCtfGroundY,0);
        }
        public static Vector3 CtfSpawn(int team)
        {
            if(team<0||team>1)throw new ArgumentOutOfRangeException(nameof(team));
            return team==0?new Vector3(-36,CuratedCtfGroundY,-12):new Vector3(36,CuratedCtfGroundY,12);
        }
        static List<CastlePlacement> CrownCourtyards()
        {
            var output=new List<CastlePlacement>(55);
            // Independent grounded plates keep the required carrier routes off the sand.
            for(int x=0;x<4;x++)for(int z=0;z<4;z++)
            {
                if((x==1&&z==1)||(x==2&&z==2))continue;
                CtfBox(output,new Vector3(-33+x*22,.2f,-24+z*16),new Vector3(22,.4f,16),CastleSection.Wall);
            }
            for(int side=-1;side<=1;side+=2)
            {
                RampFootFloor(output,side);
                HomeCourt(output,side);
                CoveredFlank(output,side);
            }
            // Three separately supported plates make the upper route optional and locally removable.
            foreach(float x in new[]{-10.4f,0,10.4f})
                CtfBox(output,new Vector3(x,4.2f,0),new Vector3(10.4f,.4f,4.8f),CastleSection.Keep,2);
            foreach(float x in new[]{-14,-5.2f,5.2f,14})foreach(float z in new[]{-2f,2f})
                CtfBox(output,new Vector3(x,2.2f,z),new Vector3(1.2f,3.6f,.8f),CastleSection.Corner);
            CtfRamp(output,1);
            CtfRamp(output,-1);
            foreach(float x in new[]{-8,8})foreach(float z in new[]{-8,8})
                CtfBox(output,new Vector3(x,1,z),new Vector3(4,1.2f,1.2f),CastleSection.Corner);
            if(output.Count>80)throw new InvalidOperationException("Crown Courtyards exceeds its first authoring ceiling.");
            return output;
        }
        static void RampFootFloor(List<CastlePlacement> output,int side)
        {
            // Only the two low ramp noses need floor cut-outs; a tilted box must not begin inside a floor plate.
            const float nose=-14.533333f;
            CtfMirrorBox(output,side,new Vector3(-18.8f,.2f,-8),new Vector3(6.4f,.4f,16));
            CtfMirrorBox(output,side,new Vector3(-6.2f,.2f,-8),new Vector3(12.4f,.4f,16));
            CtfMirrorBox(output,side,new Vector3(-14,.2f,(-16+nose)*.5f),new Vector3(3.2f,.4f,nose+16));
            CtfMirrorBox(output,side,new Vector3(-14,.2f,-6.6f),new Vector3(3.2f,.4f,13.2f));
        }
        static void HomeCourt(List<CastlePlacement> output,int side)
        {
            // Both X ends remain open. The split rear wall protects the spawn without making a dead end.
            foreach(float z in new[]{-7,7})
                CtfMirrorBox(output,side,new Vector3(-30,1.9f,z),new Vector3(14,3,.8f));
            foreach(float z in new[]{-5,5})
                CtfMirrorBox(output,side,new Vector3(-37,1.9f,z),new Vector3(.8f,3,3.2f));
            CtfMirrorBox(output,side,new Vector3(-30,3.6f,0),new Vector3(14,.4f,14.8f),CastleSection.Keep,1);
        }
        static void CoveredFlank(List<CastlePlacement> output,int side)
        {
            // A broad roofed passage offers a different exposure choice from the open outer ring.
            foreach(float z in new[]{-22.4f,-17.6f})
                CtfMirrorBox(output,side,new Vector3(0,1.9f,z),new Vector3(20,3,.8f));
            CtfMirrorBox(output,side,new Vector3(0,3.6f,-20),new Vector3(20,.4f,5.6f),CastleSection.Keep,1);
        }
        static void CtfRamp(List<CastlePlacement> output,int side)
        {
            // Two full-width end contacts span the centre of mass. Native settling and motor traversal still need testing.
            var low=new Vector3(-14,.4f,-14.533333f);
            var high=new Vector3(-14,4.4f,-2.533333f);
            var rotation=Quaternion.LookRotation(high-low,Vector3.up);
            float thickness=.4f/Mathf.Cos(Mathf.Atan2(4,12));
            var position=(low+high)*.5f-rotation*Vector3.up*(thickness*.5f);
            var mirror=Quaternion.Euler(0,side<0?180:0,0);
            output.Add(new CastlePlacement(mirror*position,mirror*rotation,CastleSection.Keep,1,new Vector3(3.2f,thickness,(high-low).magnitude)));
        }
        static void CtfMirrorBox(List<CastlePlacement> output,int side,Vector3 position,Vector3 size,CastleSection section=CastleSection.Wall,int course=0)
        {
            position.x*=side;position.z*=side;
            CtfBox(output,position,size,section,course);
        }
        static void CtfBox(List<CastlePlacement> output,Vector3 position,Vector3 size,CastleSection section,int course=0)
        {output.Add(new CastlePlacement(position,Quaternion.identity,section,course,size));}
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
