using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.World.Hazards
{
    public abstract partial class BasicDisaster
    {
        readonly List<int> burning=new List<int>();
        readonly Dictionary<int,float> heat=new Dictionary<int,float>();
        readonly Dictionary<int,float> lastContact=new Dictionary<int,float>();
        readonly Dictionary<Rigidbody,int> blockIds=new Dictionary<Rigidbody,int>();
        readonly HashSet<int> ignited=new HashSet<int>();
        readonly Dictionary<int,float> fireStarted=new Dictionary<int,float>();
        readonly Dictionary<IDamageable,float> lastFireDamage=new Dictionary<IDamageable,float>();
        int fireCursor;
        FireScars fireScars;
        void SeedFire()
        {
            burning.Clear();ignited.Clear();heat.Clear();lastContact.Clear();blockIds.Clear();fireStarted.Clear();fireCursor=0;
            fireScars=FireScars.For(world);
            var candidates=new List<int>();
            for(int i=0;i<world.Blocks.Count;i++)if(world.Blocks[i]&&world.Blocks[i].gameObject.activeInHierarchy){if(!fireScars||!fireScars.Burnt(i))candidates.Add(i);blockIds[world.Blocks[i]]=i;}
            int count=Mathf.Min((int)V("seeds"),candidates.Count);
            for(int i=0;i<count;i++){int pick=Random.Range(i,candidates.Count);int id=candidates[pick];candidates[pick]=candidates[i];candidates[i]=id;Ignite(id);}
        }
        void Ignite(int id){if((!fireScars||!fireScars.Burnt(id))&&ignited.Add(id)){burning.Add(id);fireStarted[id]=age;}}
        void Scorch(int id){if(fireScars)fireScars.Scorch(id,V("smokeSeconds"));}
        BlockBurnFrame[] CaptureBurns()
        {
            var result=new BlockBurnFrame[burning.Count];
            for(int i=0;i<burning.Count;i++)result[i]=new BlockBurnFrame{block=burning[i],flameRemaining=Mathf.Max(0,V("burnSeconds")-(age-fireStarted[burning[i]]))};
            return result;
        }
        float FireRemaining()
        {
            if(V("neverEnd")>.5f)return -1;
            float remaining=Mathf.Max(0,V("warning")+V("duration")-age);
            foreach(int id in burning)remaining=Mathf.Max(remaining,V("burnSeconds")-age+fireStarted[id]);
            return remaining;
        }
        void TickFire(float dt)
        {
            for(int i=burning.Count-1;i>=0;i--)
                if(age-fireStarted[burning[i]]>=V("burnSeconds")){Scorch(burning[i]);burning.RemoveAt(i);}
            int count=Mathf.Min(32,burning.Count);if(count==0)return;
            bool spreading=V("neverEnd")>.5f||age<V("warning")+V("duration");
            float visitInterval=Mathf.Max(dt,Mathf.Ceil(burning.Count/32f)*dt);
            receivers.Clear();
            for(int step=0;step<count;step++)
            {
                int index=burning[fireCursor++%burning.Count];if(index>=world.Blocks.Count)continue;
                var body=world.Blocks[index];if(!body||!body.gameObject.activeInHierarchy)continue;
                var box=body.GetComponent<Collider>();if(!box)continue;
                int n=Query(box.bounds.center,box.bounds.extents.magnitude+.3f);
                for(int i=0;i<n;i++)
                {
                    var other=hits[i];if(!other||other==box)continue;
                    Vector3 a=box.ClosestPoint(other.bounds.center),b=other.ClosestPoint(a);
                    if((a-b).sqrMagnitude>.09f)continue;
                    var health=other.GetComponentInParent<IDamageable>();
                    if(health!=null&&!receivers.Contains(health))
                    {
                        float elapsed=lastFireDamage.TryGetValue(health,out float when)&&age-when<visitInterval*2.5f?Mathf.Min(visitInterval,age-when):dt;
                        if(elapsed>.001f){Affect(other,Vector3.zero,V("damage")*elapsed);lastFireDamage[health]=age;}
                    }
                    if(!spreading||!other.attachedRigidbody||!blockIds.TryGetValue(other.attachedRigidbody,out int id)||ignited.Contains(id))continue;
                    if(lastContact.TryGetValue(id,out float previous)&&age-previous<dt*.5f)continue;
                    float accumulated=lastContact.TryGetValue(id,out previous)&&age-previous<=visitInterval*2.5f&&heat.TryGetValue(id,out float h)?h:0;
                    heat[id]=accumulated+visitInterval;lastContact[id]=age;
                    if(heat[id]>=V("contact"))Ignite(id);
                }
            }
        }
    }
}
