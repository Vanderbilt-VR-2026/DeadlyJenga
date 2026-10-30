using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public static class ModeHazardTactics
    {
        public static float Risk(Vector3 feet,float height,bool sheltered,IList<DisasterFrame> hazards,IList<WaveFrame> waves)
        {
            float risk=0;
            foreach(var f in hazards)
            {
                if(f.warning>4)continue;
                if(f.kind=="AcidRain"&&!sheltered)risk+=3;
                if(f.kind=="Tornado")
                {
                    float reach=f.values!=null&&f.values.Length>3?f.values[3]:20;
                    risk+=Mathf.Max(0,1-Vector3.ProjectOnPlane(feet-f.center,Vector3.up).magnitude/Mathf.Max(1,reach))*4;
                }
                if((f.kind=="MeteorShower"||f.kind=="PlanetKillerMeteor")&&f.points!=null)
                    for(int i=0;i<Mathf.Min(128,f.points.Length);i++)
                    {
                        var p=f.points[i];float distance=Vector3.Distance(feet+Vector3.up*(height*.5f),p.position+p.velocity*.5f);
                        risk+=Mathf.Max(0,1-distance/Mathf.Max(2,p.size+2))*4;
                    }
            }
            foreach(var wave in waves)
            {
                if(!wave.active||wave.warningRemaining>8||feet.y>wave.waterHeight+wave.height+.5f)continue;
                var delta=feet-wave.center;var direction=Vector3.ProjectOnPlane(wave.direction,Vector3.up).normalized;
                float along=Vector3.Dot(delta,direction),across=Vector3.ProjectOnPlane(delta-direction*along,Vector3.up).magnitude;
                if(across<wave.width*.5f+1&&along> -wave.depth*.5f-2&&along<wave.depth*.5f+wave.speed*3)risk+=3;
            }
            return risk;
        }
        public static void Select(Vector3 feet,float height,IList<Vector3> candidates,IList<DisasterFrame> hazards,IList<WaveFrame> waves,Func<Vector3,bool> sheltered,List<Vector3> result)
        {
            result.Clear();bool rain=false;foreach(var f in hazards)if(f.kind=="AcidRain"&&f.warning<=4)rain=true;
            bool cover=sheltered(feet+Vector3.up*height);
            float current=Risk(feet,height,cover,hazards,waves);
            if(current<.2f&&!rain)return;
            var nearest=new List<Vector3>(25);
            foreach(var p in candidates)
            {
                var d=p-feet;if(d.y>10.5f||d.y< -10||Vector3.ProjectOnPlane(d,Vector3.up).sqrMagnitude>24*24)continue;
                int at=0;while(at<nearest.Count&&(nearest[at]-feet).sqrMagnitude<d.sqrMagnitude)at++;
                if(at<24){nearest.Insert(at,p);if(nearest.Count>24)nearest.RemoveAt(24);}
            }
            float best=float.PositiveInfinity;
            foreach(var point in nearest)
            {
                bool covered=sheltered(point+Vector3.up*height);if(rain&&!covered)continue;
                float danger=Risk(point,height,covered,hazards,waves);if(!rain&&danger>=current-.15f)continue;
                float score=danger*10+Vector3.Distance(feet,point)*.04f;
                if(score>=best)continue;best=score;result.Clear();result.Add(point);
            }
        }
    }
}
