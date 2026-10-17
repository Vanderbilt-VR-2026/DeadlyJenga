using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.World.Hazards
{
    public abstract partial class BasicDisaster
    {
        readonly List<DisasterPoint> meteors=new List<DisasterPoint>();
        readonly RaycastHit[] rayHits=new RaycastHit[64];
        float meteorClock;int meteorId;bool launched;
        void TickMeteors(float dt)
        {
            meteorClock-=dt;
            if(Kind=="PlanetKillerMeteor")
            {if(!launched){launched=true;SpawnMeteor(true);}}
            else while(meteorClock<=0){meteorClock+=1/Mathf.Max(.5f,V("frequency"));if(meteors.Count<128)SpawnMeteor(false);}
            for(int i=meteors.Count-1;i>=0;i--)
            {
                var p=meteors[i];float distance=p.velocity.magnitude*dt;
                int n=Physics.SphereCastNonAlloc(p.position,p.size*.5f,p.velocity.normalized,rayHits,distance,(1<<Layers.Block)|(1<<Layers.Surface)|(1<<Layers.Survivor),QueryTriggerInteraction.Collide);
                float nearest=float.PositiveInfinity;Vector3 point=p.position;
                for(int j=0;j<n;j++)if(rayHits[j].distance<nearest){nearest=rayHits[j].distance;point=rayHits[j].point;}
                if(n>0){MeteorImpact(point,p);meteors.RemoveAt(i);continue;}
                p.position+=p.velocity*dt;p.life-=dt;
                if(p.life<=0||p.position.y<world.WaterHeight(p.position)-p.size){meteors.RemoveAt(i);continue;}
                meteors[i]=p;
            }
            if(Kind=="PlanetKillerMeteor"&&launched&&meteors.Count==0)End();
        }
        void SpawnMeteor(bool planet)
        {
            float u=Mathf.Max(.0001f,Random.value),normal=Mathf.Sqrt(-2*Mathf.Log(u))*Mathf.Cos(2*Mathf.PI*Random.value);
            float diameter=planet?V("size"):Mathf.Clamp(V("size")+normal*V("deviation"),.3f,25);
            Vector3 target=area.center+new Vector3(Random.Range(-area.extents.x,area.extents.x),Random.Range(-area.extents.y,area.extents.y),Random.Range(-area.extents.z,area.extents.z));
            if(planet)target=area.center;
            var from=Quaternion.Euler(0,Random.Range(0f,360f),0)*new Vector3(1,Random.Range(.8f,2.5f),0).normalized;
            float distance=area.size.magnitude+diameter*2+45;
            var start=target+from*distance;
            meteors.Add(new DisasterPoint{id=++meteorId,position=start,velocity=(target-start).normalized*(planet?28:45),size=diameter,life=distance/(planet?28:45)+4});
        }
        void MeteorImpact(Vector3 point,DisasterPoint meteor)
        {
            float radius=Mathf.Max(2,meteor.size*.8f);int n=Query(point,radius);receivers.Clear();
            float force=Kind=="PlanetKillerMeteor"?28:V("force");
            for(int i=0;i<n;i++)
            {
                var delta=hits[i].bounds.center-point;float falloff=1-Mathf.Clamp01(delta.magnitude/radius);
                Affect(hits[i],(meteor.velocity.normalized*.7f+delta.normalized*.6f+Vector3.up*.25f)*force*falloff,force*falloff*.7f);
            }
            GameEvents.RaiseBlockHit(point,force);
        }
    }
}
