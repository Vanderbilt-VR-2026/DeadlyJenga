using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.World.Hazards
{
    public sealed class EvilSantaHazard : TreeCreatureHazard
    {
        enum Routine { Alternate, GrabOnly, SlamOnly }
        protected override string Kind => "EvilSanta";
        [SerializeField] Routine routine = Routine.Alternate;
        [SerializeField, Range(2,8)] float secondsPerStrike = 3.8f;
        [SerializeField, Range(35,95)] float reach = 65;
        [SerializeField, Range(6,28)] float liftHeight = 18;
        Vector3 anchor,forward,target;
        Rigidbody selected;
        int lastCycle;
        bool contacted;

        protected override void Setup()
        {
            anchor = area.center + Vector3.forward*(area.extents.z*.85f);
            anchor.y = Ground(anchor);
            forward = Vector3.back;
            var nearest = Pick(anchor,false);
            if(nearest)
            {
                anchor = nearest.worldCenterOfMass-forward*27;
                anchor.y = Ground(anchor);
            }
            lastCycle = -1;
            selected = null;
            points = new DisasterPoint[3];
            points[0] = new DisasterPoint{id=1,position=anchor,velocity=forward,size=1};
            for(int i=0;i<2;i++) points[i+1]=new DisasterPoint{id=i+2,position=Home(i),size=7};
        }

        Vector3 Home(int arm) => anchor + Vector3.up*50 + Vector3.Cross(Vector3.up,forward)*(arm==0?-19:19) + forward*12;

        Rigidbody Pick(Vector3 origin,bool limit)
        {
            Rigidbody best = null;
            float bestScore = float.PositiveInfinity;
            foreach(var b in world.Blocks)
            {
                if(!b || !b.isKinematic || !b.gameObject.activeInHierarchy) continue;
                Vector3 offset = b.worldCenterOfMass-origin;
                if(limit && (offset.sqrMagnitude>reach*reach || Vector3.Dot(b.worldCenterOfMass-anchor,forward)<0)) continue;
                float score = offset.sqrMagnitude*(limit?Random.Range(.65f,1.35f):1);
                if(score<bestScore){ best=b; bestScore=score; }
            }
            return best;
        }

        static Vector3 Between(Vector3 a,Vector3 b,float t) => Vector3.Lerp(a,b,Mathf.SmoothStep(0,1,t));

        protected override void Step(float dt)
        {
            float clock = Elapsed/Mathf.Max(2,secondsPerStrike);
            int cycle = Mathf.FloorToInt(clock);
            float t = clock-cycle;
            int arm = cycle%2;
            bool slam = routine==Routine.SlamOnly || (routine==Routine.Alternate && arm==1);
            Vector3 home=Home(arm);
            if(lastCycle!=cycle)
            {
                selected=Pick(home,true);
                target=selected?selected.worldCenterOfMass:home+forward*16-Vector3.up*28;
                contacted=false;
                touched.Clear();
                lastCycle=cycle;
            }
            Vector3 ready = slam ? target+Vector3.up*(liftHeight+12) : home-forward*5+Vector3.up*5;
            Vector3 release = slam ? target+Vector3.up*7 : target+Vector3.up*liftHeight-forward*6;
            Vector3 hand;
            if(t<.32f) hand=Between(home,ready,t/.32f);
            else if(t<.52f) hand=Between(ready,target,(t-.32f)/.2f);
            else if(t<.76f) hand=Between(target,release,(t-.52f)/.24f);
            else hand=Between(release,home,(t-.76f)/.24f);
            if(!contacted && t>=.52f)
            {
                contacted=true;
                if(slam) Impact(target,Vector3.one*10,Quaternion.identity,(forward+Vector3.up*.35f)*V(0));
                else if(selected && V(0)*strength>0)
                    selected.GetComponent<IPushable>()?.Push(Vector3.up*Mathf.Min(3,V(0)*strength),gameObject);
            }
            if(!slam && contacted && t<.76f && selected && !selected.isKinematic && V(0)*strength>0)
                selected.AddForce(Vector3.ClampMagnitude((hand-selected.worldCenterOfMass)*8-selected.linearVelocity,V(0)*strength)*dt*8,ForceMode.VelocityChange);
            points[arm+1]=new DisasterPoint{id=arm+2,position=hand,size=7,life=slam?-t:t};
            points[2-arm]=new DisasterPoint{id=3-arm,position=Home(1-arm),size=7};
        }

        protected override void EndHazard(){ selected=null; base.EndHazard(); }
    }
}
