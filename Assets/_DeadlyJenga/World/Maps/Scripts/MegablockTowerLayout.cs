using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public static class MegablockTowerLayout
    {
        public const int Floors=30;
        public const float Storey=4.8f, Width=40, Depth=30, FloorThickness=.6f;
        public const float ShaftHalfWidth=5.2f, ShaftHalfDepth=11;
        public static float FloorTop(int level)=>FloorThickness+Storey*level;
        public static readonly Vector2 IslandHalfSize=new(44,39);

        public static List<MayhemModule> Build()
        {
            var parts=new List<MayhemModule>();
            for(int level=0;level<Floors;level++)
            {
                float top=FloorTop(level);
                foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
                {
                    var m=new MayhemModule{district=level,purpose=$"Floor {level+1:00} windowed corner {x},{z}",position=new Vector3(x*10,top,z*7.5f),mass=450};
                    parts.Add(m);
                    void Box(Vector3 p,Vector3 size,int color)=>Add(m,p-m.position,size,color);
                    FloorQuarter(m,x,z,3);
                    // Window bands have genuine openings, not a solid collision proxy.
                    Wall(m,true,x,z,0,20,top);
                    Wall(m,false,x,z,0,13.8f,top);
                    // One partitioned room per floor; the other bays remain open for movement.
                    if(x==(level%2==0?1:-1)&&z==(level%4<2?1:-1))
                    {
                        float door=12.6f,halfDoor=1.5f;
                        Box(new Vector3(x*((5.2f+door-halfDoor)*.5f),top+1.8f,z*7.2f),new Vector3(door-halfDoor-5.2f,3.6f,.6f),1);
                        Box(new Vector3(x*((door+halfDoor+18.8f)*.5f),top+1.8f,z*7.2f),new Vector3(18.8f-door-halfDoor,3.6f,.6f),1);
                        Box(new Vector3(x*12,top+3.9f,z*7.2f),new Vector3(13.6f,.6f,.6f),1);
                    }
                }
                parts.Add(Stair(level));
            }
            // Roof quarters remain separately movable. The stair opening reaches the roof.
            float roof=FloorTop(Floors);
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
            {
                var m=new MayhemModule{district=Floors,purpose="Separate roof quarter",position=new Vector3(x*10,roof,z*7.5f),mass=450};parts.Add(m);
                FloorQuarter(m,x,z,2);
                Add(m,new Vector3(0,.45f,z*6.9f),new Vector3(20,.9f,1.2f),2);
                Add(m,new Vector3(x*9.4f,.45f,-z*.6f),new Vector3(1.2f,.9f,13.8f),2);
                if(x==1&&z==1)Landing(m,new Vector3(-10,0,-7.5f),2);
            }
            foreach(int z in new[]{-1,1})
            {
                var m=new MayhemModule{purpose="Wide entrance ramp",position=new Vector3(0,0,z*20),rotation=Quaternion.Euler(0,z<0?0:180,0),mass=160};
                var v=new[]{new Vector3(-1.9f,0,0),new Vector3(1.9f,0,0),new Vector3(1.9f,.6f,5),new Vector3(-1.9f,.6f,5),new Vector3(-1.9f,0,0),new Vector3(1.9f,0,0),new Vector3(1.9f,0,5),new Vector3(-1.9f,0,5)};
                m.shapes.Add(new MayhemShape(Vector3.zero,new Vector3(3.8f,.6f,5),2,Quaternion.identity,v));parts.Add(m);
            }
            return parts;
        }
        static void FloorQuarter(MayhemModule m,int x,int z,int color)
        {
            if(z<0)Add(m,new Vector3(x*2.6f,-.3f,0),new Vector3(14.8f,.6f,15),color);
            else
            {
                Add(m,new Vector3(x*3.2f,-.3f,0),new Vector3(13.6f,.6f,15),color);
                Add(m,new Vector3(-x*4.2f,-.3f,-3.5f),new Vector3(1.2f,.6f,8),color);
                Add(m,new Vector3(-x*4.2f,-.3f,5.5f),new Vector3(1.2f,.6f,4),color);
                // Recessed seats carry the stair landing while its top stays flush with the floor.
                Add(m,new Vector3(-x*4.2f,-.39f,2),new Vector3(1.2f,.42f,3),color);
            }
            Add(m,new Vector3(-x*7.4f,-.3f,z*5.5f),new Vector3(5.2f,.6f,4),color);
        }
        static void Wall(MayhemModule m,bool front,int sx,int sz,float from,float to,float top)
        {
            void Segment(float a,float b,float y,float h,int color)
            {
                var p=front?new Vector3(sx*(a+b)*.5f,top+y,sz*14.4f):new Vector3(sx*19.4f,top+y,sz*(a+b)*.5f);
                Add(m,p-m.position,front?new Vector3(b-a,h,1.2f):new Vector3(1.2f,h,b-a),color);
            }
            bool entry=m.district==0&&front;
            Segment(entry?2:from,to,.225f,.45f,1);
            Segment(from,to,3.6f,1.2f,1);
            if(front)
            {
                Segment(entry?2:0,3,1.725f,2.55f,entry?0:2);
                Segment(8.5f,11.5f,1.725f,2.55f,2);
                Segment(17,20,1.725f,2.55f,0);
            }
            else
            {Segment(0,3,1.725f,2.55f,0);Segment(10.8f,13.8f,1.725f,2.55f,0);}
        }
        static MayhemModule Stair(int level)
        {
            float top=FloorTop(level);
            var m=new MayhemModule{district=level,purpose=$"Floor {level+1:00} supported switchback",position=new Vector3(0,top,0),mass=160};
            Landing(m,Vector3.zero,2);
            Add(m,new Vector3(0,2.31f,-9.5f),new Vector3(12.8f,.18f,3),2);
            foreach(int x in new[]{-1,1})Add(m,new Vector3(x*5.8f,1.11f,-9.5f),new Vector3(1.2f,2.22f,3),0);
            Flight(m,new Vector3(-2.5f,-.0975f,8.65f),new Vector3(-2.5f,2.4f,-8),4.2f);
            Flight(m,new Vector3(2.5f,2.4f,-8),new Vector3(2.5f,4.8f,8),4.2f);
            return m;
        }
        static void Landing(MayhemModule m,Vector3 offset,int color)
        {
            Add(m,offset+new Vector3(0,-.09f,9.8f),new Vector3(12.8f,.18f,2.4f),color);
            var v=new[]{new Vector3(-6.4f,-.18f,0),new Vector3(6.4f,-.18f,0),new Vector3(6.4f,-.18f,0),new Vector3(-6.4f,-.18f,0),new Vector3(-6.4f,-.18f,.6f),new Vector3(6.4f,-.18f,.6f),new Vector3(6.4f,0,.6f),new Vector3(-6.4f,0,.6f)};
            m.shapes.Add(new MayhemShape(offset+new Vector3(0,0,8),new Vector3(12.8f,.18f,.6f),color,Quaternion.identity,v));
        }
        static void Flight(MayhemModule m,Vector3 a,Vector3 b,float width)
        {
            var delta=b-a;var rotation=Quaternion.LookRotation(delta,Vector3.up);
            // Clearance prevents tilted end caps from wedging the next movable landing upward.
            Add(m,(a+b)*.5f-Vector3.up*.09f,new Vector3(width,.18f,delta.magnitude-.1f),2,rotation);
        }
        static void Add(MayhemModule m,Vector3 center,Vector3 size,int color,Quaternion? rotation=null)
        {
            // Remove subtraction noise so repeated storeys reuse the same cached visual mesh.
            for(int axis=0;axis<3;axis++)center[axis]=Mathf.Round(center[axis]*10000)/10000;
            m.shapes.Add(new MayhemShape(center,size,color,rotation??Quaternion.identity));
        }
    }
}
