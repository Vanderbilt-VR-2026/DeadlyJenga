using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public static class ModeSearchGeometry
    {
        public static void Midfield(Vector3 feet,Vector3 center,IList<Vector3> supports,bool visibleAttack,List<Vector3> result)
        {
            result.Clear();if(visibleAttack)return;
            var best=new Vector3[8];var scores=new float[8];var found=new bool[8];
            float current=Vector3.ProjectOnPlane(feet-center,Vector3.up).magnitude;
            foreach(var point in supports)
            {
                var offset=point-feet;float rise=offset.y;offset.y=0;float distance=offset.magnitude;
                float central=Vector3.ProjectOnPlane(point-center,Vector3.up).magnitude;
                if(rise>10.5f||rise< -20||distance>40||current>12&&central>=current-1||current<=12&&central>12)continue;
                int sector=Mathf.Clamp(Mathf.FloorToInt((Mathf.Atan2(point.z-center.z,point.x-center.x)+Mathf.PI)/(Mathf.PI*.25f)),0,7);
                float score=-central*.04f-distance*.01f;
                if(!found[sector]||score>scores[sector]){found[sector]=true;scores[sector]=score;best[sector]=point;}
            }
            for(int i=0;i<8;i++)if(found[i])result.Add(best[i]);
        }
        public static void OuterCrossings(Vector3 feet,Vector3 center,IList<Vector3> supports,float safeDrop,List<Vector3> result)
        {
            result.Clear();
            var best=new Vector3[8];var scores=new float[8];var found=new bool[8];
            float current=Vector3.ProjectOnPlane(feet-center,Vector3.up).magnitude;
            foreach(var point in supports)
            {
                var delta=point-feet;float rise=delta.y;delta.y=0;
                float distance=delta.magnitude;
                var radial=Vector3.ProjectOnPlane(point-center,Vector3.up);
                if(distance<7||distance>38||rise>10||rise< -safeDrop||
                    current<13&&radial.magnitude<current+5)continue;
                int sector=Mathf.Clamp(Mathf.FloorToInt((Mathf.Atan2(delta.z,delta.x)+Mathf.PI)/(Mathf.PI*.25f)),0,7);
                float score=Mathf.Min(radial.magnitude,30)*.1f-distance*.045f-Mathf.Max(0,-rise)*.015f+rise*.035f;
                if(found[sector]&&score<=scores[sector])continue;
                found[sector]=true;scores[sector]=score;best[sector]=point;
            }
            for(int i=0;i<8;i++)if(found[i])result.Add(best[i]);
        }
        public static void Select(Vector3 feet,IList<Vector3> supports,bool visibleAttack,List<Vector3> result)
        {
            result.Clear();if(visibleAttack)return;
            var best=new Vector3[8];var scores=new float[8];var found=new bool[8];
            foreach(var point in supports)
            {
                var offset=point-feet;float rise=offset.y;offset.y=0;float distance=offset.magnitude;
                if(rise<1.5f||rise>10.5f||distance>24)continue;
                int sector=Mathf.Clamp(Mathf.FloorToInt((Mathf.Atan2(offset.z,offset.x)+Mathf.PI)/(Mathf.PI*.25f)),0,7);
                float score=rise*.12f-distance*.04f;
                if(!found[sector]||score>scores[sector]){found[sector]=true;scores[sector]=score;best[sector]=point;}
            }
            for(int i=0;i<8;i++)if(found[i])result.Add(best[i]);
        }
    }
}
