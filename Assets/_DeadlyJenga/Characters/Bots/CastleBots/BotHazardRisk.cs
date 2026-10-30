using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastleBots
{
    public static class BotHazardRisk
    {
        const float CellSize=4;
        static readonly Dictionary<Vector3Int,float> cells=new Dictionary<Vector3Int,float>();
        static readonly List<DisasterFrame> tornadoes=new List<DisasterFrame>();
        static readonly List<WaveFrame> waves=new List<WaveFrame>();
        static readonly List<Bounds> largeRegions=new List<Bounds>();
        static float refreshAt=-1;
        static Vector3Int Cell(Vector3 p)=>new Vector3Int(Mathf.FloorToInt(p.x/CellSize),Mathf.FloorToInt(p.y/CellSize),Mathf.FloorToInt(p.z/CellSize));
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset(){refreshAt=-1;cells.Clear();tornadoes.Clear();waves.Clear();largeRegions.Clear();}
        static void Add(Bounds bounds,float cost)
        {
            var min=Cell(bounds.min);var max=Cell(bounds.max);
            // Very large meteors cover the arena, so bound raster work to its playable volume.
            var world=Services.World;
            if(world!=null){var area=world.PlayableBounds;area.Expand(8);min=Vector3Int.Max(min,Cell(area.min));max=Vector3Int.Min(max,Cell(area.max));}
            long volume=(long)(max.x-min.x+1)*(max.y-min.y+1)*(max.z-min.z+1);if(volume>256){largeRegions.Add(bounds);return;}
            for(int x=min.x;x<=max.x;x++)for(int y=min.y;y<=max.y;y++)for(int z=min.z;z<=max.z;z++)
            {var key=new Vector3Int(x,y,z);if(!cells.TryGetValue(key,out float old)||cost>old)cells[key]=cost;}
        }
        static void Refresh()
        {
            if(Time.time<refreshAt)return;refreshAt=Time.time+.5f;cells.Clear();tornadoes.Clear();waves.Clear();largeRegions.Clear();
            var world=Services.World;
            foreach(var source in DisasterStates.Active)
            {
                var frame=source.Capture();if(frame==null)continue;
                if(frame.kind=="Fire"&&frame.blocks!=null&&world!=null)
                    foreach(int index in frame.blocks)
                    {
                        if(index<0||index>=world.Blocks.Count)continue;var body=world.Blocks[index];if(!body)continue;
                        var collider=body.GetComponent<Collider>();if(!collider)continue;var b=collider.bounds;b.Expand(1.2f);Add(b,8);
                    }
                else if(frame.kind=="Tornado")tornadoes.Add(frame);
                else if((frame.kind=="MeteorShower"||frame.kind=="PlanetKillerMeteor")&&frame.points!=null)
                    foreach(var point in frame.points)
                    {
                        var predicted=point.position+point.velocity*.5f;float diameter=Mathf.Max(2,point.size+3);
                        Add(new Bounds(predicted,Vector3.one*diameter),7);
                    }
            }
            foreach(var source in Tracked.Waves){var wave=source.Wave;if(wave.active)waves.Add(wave);}
        }
        public static float Cost(Vector3 point)
        {
            Refresh();float risk=cells.TryGetValue(Cell(point),out float cell)?cell:0;
            foreach(var region in largeRegions)if(region.Contains(point))risk=Mathf.Max(risk,7);
            foreach(var tornado in tornadoes)
            {
                float reach=tornado.values!=null&&tornado.values.Length>3?tornado.values[3]:10;
                float distance=Vector3.ProjectOnPlane(point-tornado.center,Vector3.up).magnitude;
                if(distance<reach+2)risk+=Mathf.Max(0,1-distance/(reach+2))*7;
            }
            foreach(var wave in waves)
            {
                if(point.y>wave.waterHeight+wave.height+1)continue;
                var forward=Vector3.ProjectOnPlane(wave.direction,Vector3.up).normalized;if(forward.sqrMagnitude<.5f)continue;
                var delta=point-wave.center;float along=Vector3.Dot(delta,forward),across=Mathf.Abs(Vector3.Dot(delta,Vector3.Cross(Vector3.up,forward)));
                if(across<wave.width*.5f+1&&along>-wave.depth*.5f-1&&along<wave.depth*.5f+wave.speed*.6f+1)risk+=6;
            }
            return risk;
        }
    }
}
