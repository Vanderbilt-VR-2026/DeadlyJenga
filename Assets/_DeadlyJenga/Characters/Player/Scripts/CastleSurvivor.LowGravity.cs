using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleSurvivor
    {
        float DisasterGravity(out float fallLimit)
        {
            float scale=1;fallLimit=float.PositiveInfinity;
            foreach(var source in DisasterStates.Active)
            {
                var frame=source.Capture();
                if(frame==null||frame.kind!="AntiGravity"||frame.warning>0||frame.values==null||frame.values.Length<2)continue;
                var bounds=new Bounds(frame.center,frame.size+Vector3.one*10);
                if(!bounds.Contains(transform.position))continue;
                scale=Mathf.Min(scale,Mathf.Clamp(frame.values[0],.05f,1));
                fallLimit=Mathf.Min(fallLimit,Mathf.Clamp(frame.values[1],1,20));
            }
            return scale;
        }
    }
}
