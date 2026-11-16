using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.World.Hazards
{
    public sealed class EvilSantaHazard : TreeCreatureHazard
    {
        protected override string Kind=>"EvilSanta";
        Vector3 anchor,forward;
        readonly Rigidbody[] held=new Rigidbody[2];
        readonly Vector3[] targets=new Vector3[2];
        readonly int[] cycles={-1,-1};
        protected override void Setup()
        {
            anchor=area.center+Vector3.back*(area.extents.z*.85f);anchor.y=Ground(anchor);
            forward=Vector3.ProjectOnPlane(area.center-anchor,Vector3.up).normalized;
            points=new DisasterPoint[3];points[0]=new DisasterPoint{id=1,position=anchor,velocity=forward,size=1};
            for(int i=0;i<2;i++){cycles[i]=-1;held[i]=null;targets[i]=Home(i);points[i+1]=new DisasterPoint{id=i+2,position=Home(i),size=7};}
        }
        Vector3 Home(int arm)=>anchor+Vector3.up*54+Vector3.Cross(Vector3.up,forward)*(arm==0?-20:20)+forward*10;
        Rigidbody Pick()
        {
            var bodies=world.Blocks;if(bodies.Count==0)return null;
            int start=Random.Range(0,bodies.Count);
            for(int n=0;n<bodies.Count;n++)
            {var b=bodies[(start+n)%bodies.Count];if(b&&b.gameObject.activeInHierarchy&&b.isKinematic&&Vector3.Distance(b.position,anchor)<area.size.magnitude)return b;}
            return null;
        }
        protected override void Step(float dt)
        {
            for(int i=0;i<2;i++)
            {
                float clock=Elapsed+i*3.5f;int cycle=Mathf.FloorToInt(clock/7);float t=clock-cycle*7;
                if(cycles[i]!=cycle)
                {cycles[i]=cycle;held[i]=Pick();targets[i]=held[i]?held[i].worldCenterOfMass:area.center;touched.Clear();}
                var home=Home(i);var target=targets[i];Vector3 p;
                if(t<2)p=Vector3.Lerp(home,target,Mathf.SmoothStep(0,1,t/2));
                else if(i==0)
                {
                    p=Vector3.Lerp(target,home,Mathf.SmoothStep(0,1,(t-2)/3));
                    if(held[i]&&t<5)
                    {
                        var push=held[i].GetComponent<IPushable>();
                        if(push!=null)push.Push(held[i].isKinematic?Vector3.up*2:
                            Vector3.ClampMagnitude((p-held[i].worldCenterOfMass)*2-held[i].linearVelocity,12)*dt*4,gameObject);
                    }
                }
                else
                {
                    p=t<3?target+Vector3.up*(Mathf.Sin((t-2)*Mathf.PI)*16):Vector3.Lerp(target,home,Mathf.Clamp01((t-3)/3));
                    if(t>=2.9f&&t<3.2f)Impact(target,Vector3.one*10,Quaternion.identity,(forward+Vector3.up*.3f)*V(0));
                }
                points[i+1]=new DisasterPoint{id=i+2,position=p,velocity=home,size=7,life=t};
            }
        }
    }
}
