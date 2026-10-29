using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Optional loose-block experiments. Every beam is supported by real blocks below it.
    public static class CastleVariants
    {
        public static float KeepRadius(Vector3 size) => (size.x-size.z*.4f)*.5f/Mathf.Tan(Mathf.PI/8);
        public static void Apply(List<CastlePlacement> blocks,CastleSettings settings)
        {
            if(settings.structureVariant==CastleStructure.Catwalks) Catwalks(blocks,settings);
            if(settings.structureVariant==CastleStructure.FourKeeps) FourKeeps(blocks,settings);
        }
        static void Block(List<CastlePlacement> output,Vector3 position,Vector3 size,int course,float yaw=0,CastleSection section=CastleSection.Keep)
            =>output.Add(new CastlePlacement(position,Quaternion.Euler(0,yaw,0),section,course,size));
        static void Catwalks(List<CastlePlacement> blocks,CastleSettings s)
        {
            float radius=KeepRadius(s.blockSize),height=s.blockSize.y,width=1,thickness=.4f;
            float span=2*(radius+s.blockSize.z*.5f);
            for(int course=0;course<s.keepCourses;course+=4)
            {
                float roof=(course+1)*height;
                Block(blocks,new Vector3(0,roof+thickness*.5f,0),new Vector3(span,thickness,width),course);
                // The perpendicular deck rests on the first beam and two matching end pads.
                foreach(int side in new[]{-1,1})
                    Block(blocks,new Vector3(0,roof+thickness*.5f,side*radius),new Vector3(width,thickness,s.blockSize.z),course);
                Block(blocks,new Vector3(0,roof+thickness*1.5f,0),new Vector3(width,thickness,span),course);
            }
            float tower=2*(s.blockSize.x+s.moduleGap),roofHeight=(s.wallCourses+2)*height;
            float offset=(s.blockSize.x-s.blockSize.z)*.5f;
            // Clear the next diagonal keep course while retaining positive bearing on its cardinal course.
            float inner=radius+s.blockSize.z*.5f-.45f,outer=tower-s.blockSize.x*.5f+1.9f;
            for(int side=0;side<4;side++)
            {
                var rotation=Quaternion.Euler(0,side*90,0);
                void Rotated(Vector3 p,Vector3 size,float yaw=0) => Block(blocks,rotation*p,size,s.wallCourses+2,side*90+yaw,CastleSection.Wall);
                // Two one-metre steps from the red rooftop to the bridge's bearing cap.
                if(side%2==0)Rotated(new Vector3(offset,roofHeight+height*.25f,outer+1.0f),new Vector3(width,height*.5f,1.8f));
                else Rotated(new Vector3(offset-1.75f,roofHeight+height*.25f,outer-.9f),new Vector3(2,height*.5f,1.8f));
                Rotated(new Vector3(offset,roofHeight+height*.5f,outer-.9f),new Vector3(width,height,2));
                Rotated(new Vector3(offset,roofHeight+height+thickness*.5f,(inner+outer)*.5f),new Vector3(width,thickness,outer-inner));
            }
        }
        static void FourKeeps(List<CastlePlacement> blocks,CastleSettings s)
        {
            blocks.RemoveAll(p=>p.Section==CastleSection.Keep);
            var small=new Vector3(s.blockSize.x*2/3,s.blockSize.y,s.blockSize.z);
            float radius=KeepRadius(small),center=radius+small.z*.5f+.025f;
            int transition=s.wallCourses+2;
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                Keep(blocks,small,new Vector3(x*center,0,z*center),0,transition);
            float outer=KeepRadius(s.blockSize),bearingWidth=s.blockSize.z*1.5f,length=2*(outer+bearingWidth*.5f),inside=length-2*bearingWidth;
            float y=(transition+.5f)*s.blockSize.y;
            // Four butt-jointed bearing beams form a ring; no intersecting corner boxes or fixed joints.
            foreach(int side in new[]{-1,1})
            {
                Block(blocks,new Vector3(0,y,side*outer),new Vector3(length,s.blockSize.y,bearingWidth),transition);
                Block(blocks,new Vector3(side*outer,y,0),new Vector3(bearingWidth,s.blockSize.y,inside),transition);
            }
            Keep(blocks,s.blockSize,Vector3.zero,transition+1,transition+1+s.keepCourses);
        }
        static void Keep(List<CastlePlacement> blocks,Vector3 size,Vector3 center,int first,int end)
        {
            float radius=KeepRadius(size);
            for(int course=first;course<end;course++)for(int side=0;side<4;side++)
            {
                var r=Quaternion.Euler(0,side*90+(course%2)*45,0);
                blocks.Add(new CastlePlacement(center+Vector3.up*((course+.5f)*size.y)+r*new Vector3(0,0,radius),r,CastleSection.Keep,course,size));
            }
        }
    }
}
