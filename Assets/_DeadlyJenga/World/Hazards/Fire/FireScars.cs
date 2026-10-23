using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.World.Hazards
{
    // Burned surfaces belong to the map, not to a running disaster.
    public sealed class FireScars:MonoBehaviour,IDisasterState
    {
        IWorldQuery world;int count;Rigidbody first;
        readonly Dictionary<int,float> smokeUntil=new Dictionary<int,float>();
        DisasterFrame cached;int capturedAt=-1;
        public static FireScars For(IWorldQuery map)
        {
            var owner=map as Component;if(!owner)return null;
            var scars=owner.GetComponent<FireScars>();if(!scars)scars=owner.gameObject.AddComponent<FireScars>();
            if(!ReferenceEquals(scars.world,map)){scars.world=map;scars.ResetRevision();}
            return scars;
        }
        void OnEnable()=>DisasterStates.Active.Add(this);
        void OnDisable()=>DisasterStates.Active.Remove(this);
        void ResetRevision()
        {
            smokeUntil.Clear();cached=null;capturedAt=-1;
            count=world?.Blocks.Count??0;first=count>0?world.Blocks[0]:null;
        }
        void Refresh()
        {
            if(world==null)return;
            if(count>world.Blocks.Count||(count>0&&first!=world.Blocks[0]))ResetRevision();
            else{count=world.Blocks.Count;first=count>0?world.Blocks[0]:null;}
        }
        public bool Burnt(int block){Refresh();return smokeUntil.ContainsKey(block);}
        public void Scorch(int block,float seconds)
        {
            Refresh();if(world==null||block<0||block>=world.Blocks.Count)return;
            smokeUntil[block]=Time.time+Mathf.Max(0,seconds);capturedAt=-1;
        }
        public DisasterFrame Capture()
        {
            Refresh();if(world==null||!ReferenceEquals(Services.World,world)||smokeUntil.Count==0)return null;
            if(capturedAt==Time.frameCount&&cached!=null)return cached;capturedAt=Time.frameCount;
            var burns=new BlockBurnFrame[smokeUntil.Count];int at=0;
            foreach(var pair in smokeUntil)burns[at++]=new BlockBurnFrame{block=pair.Key,smokeRemaining=Mathf.Max(0,pair.Value-Time.time)};
            return cached=new DisasterFrame{kind="Fire",label="Burned surfaces",residualOnly=true,center=world.Center,size=world.PlayableBounds.size,
                sampledAt=Time.time,burns=burns,blocks=Array.Empty<int>(),points=Array.Empty<DisasterPoint>(),values=Array.Empty<float>()};
        }
    }
}
