using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.World.Hazards
{
    public sealed class CatAttackHazard : TreeCreatureHazard
    {
        protected override string Kind=>"CatAttack";
        Vector3 from,to,direction;
        float travel,speed;
        protected override void Setup()
        {
            direction=new Vector3(.66f,0,-.751266f);
            float reach=Mathf.Abs(direction.x)*area.extents.x+Mathf.Abs(direction.z)*area.extents.z+45;
            var middle=area.center;middle.y=Ground(middle);
            from=middle-direction*reach;to=middle+direction*reach;
            travel=Mathf.Min(Vector3.Distance(from,to)/38f,V(3)-.1f);
            speed=Vector3.Distance(from,to)/travel;
            points=new[]{new DisasterPoint{id=1,position=from,velocity=direction*speed,size=1}};
        }
        protected override void Step(float dt)
        {
            float t=Elapsed/travel;
            if(t>=1){End();return;}
            var p=Vector3.Lerp(from,to,t);p.y=Ground(p);
            points[0]=new DisasterPoint{id=1,position=p,velocity=direction*speed,size=1,life=travel-Elapsed};
            Impact(p+Vector3.up*22,new Vector3(11,22,29),Quaternion.LookRotation(direction),(direction+Vector3.up*.25f)*V(0));
        }
    }
}
