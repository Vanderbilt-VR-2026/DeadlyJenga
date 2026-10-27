using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Cached projected exposure for fabricated sections, not a per-frame fluid simulation.
    public sealed class TowerSectionWaterLoad : MonoBehaviour,IWaterResistance
    {
        public float WaterResistance {get;private set;}=1;
        public float FrontalArea {get;private set;}
        public void Initialize(MayhemModule module,float mass,float scale,float referenceArea,float minimum)
        {
            var xy=new List<Rect>();var zy=new List<Rect>();
            foreach(var shape in module.shapes)
            {
                var vertices=shape.vertices??MayhemStructureLayout.Cube(shape.size);
                var bounds=new Bounds();bool first=true;
                foreach(var v in vertices)
                {
                    var point=module.rotation*(shape.center+shape.rotation*v);
                    if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
                }
                xy.Add(Rect.MinMaxRect(bounds.min.x,bounds.min.y,bounds.max.x,bounds.max.y));
                zy.Add(Rect.MinMaxRect(bounds.min.z,bounds.min.y,bounds.max.z,bounds.max.y));
            }
            // Union avoids counting the same projected patch once for every overlapping rib or floor.
            FrontalArea=Mathf.Sqrt(Union(xy)*Union(zy))*scale*scale;
            float exposure=Mathf.Max(1,FrontalArea/Mathf.Max(.01f,referenceArea));
            WaterResistance=Mathf.Clamp(mass/40f/exposure,Mathf.Max(.65f,minimum),12);
        }
        static float Union(List<Rect> rectangles)
        {
            var edges=new List<float>();foreach(var r in rectangles){edges.Add(r.xMin);edges.Add(r.xMax);}edges.Sort();
            var spans=new List<Vector2>();float area=0;
            for(int i=1;i<edges.Count;i++)
            {
                float width=edges[i]-edges[i-1];if(width<.00001f)continue;float x=(edges[i]+edges[i-1])*.5f;spans.Clear();
                foreach(var r in rectangles)if(r.xMin<x&&r.xMax>x)spans.Add(new Vector2(r.yMin,r.yMax));
                if(spans.Count==0)continue;spans.Sort((a,b)=>a.x.CompareTo(b.x));float low=spans[0].x,high=spans[0].y,length=0;
                for(int n=1;n<spans.Count;n++){var s=spans[n];if(s.x<=high)high=Mathf.Max(high,s.y);else{length+=high-low;low=s.x;high=s.y;}}
                area+=width*(length+high-low);
            }
            return area;
        }
    }
}
