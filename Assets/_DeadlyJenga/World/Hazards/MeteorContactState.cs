using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.World.Hazards
{
    // Recent contacts use the existing replicated disaster snapshot. They survive the source projectile.
    public sealed class MeteorContactState : MonoBehaviour
    {
        sealed class ContactStream : IDisasterState
        {
            readonly string kind;
            readonly Queue<DisasterPoint> points=new Queue<DisasterPoint>();
            int sequence;
            public ContactStream(string kind){this.kind=kind;}
            public void Add(DisasterPoint meteor,Vector3 position)
            {
                if(points.Count>=12)points.Dequeue();
                meteor.id=++sequence;
                meteor.position=position;
                meteor.life=Time.time+.75f;
                points.Enqueue(meteor);
            }

            public DisasterFrame Capture()
            {
                while(points.Count>0&&points.Peek().life<=Time.time)points.Dequeue();
                if(points.Count==0)return null;
                var visible=points.ToArray();
                for(int i=0;i<visible.Length;i++)visible[i].life-=Time.time;
                return new DisasterFrame
                {
                    kind=kind,center=visible[0].position,sampledAt=Time.time,residualOnly=true,points=visible
                };
            }
        }

        readonly ContactStream[] streams={
            new ContactStream("MeteorLandContact"),new ContactStream("MeteorWaterContact"),
            new ContactStream("PlanetLandContact"),new ContactStream("PlanetWaterContact")
        };

        public static void Publish(IWorldQuery world,DisasterPoint meteor,Vector3 point,bool planet,bool water)
        {
            if(!(world is Component owner)||!owner)return;
            var state=owner.GetComponent<MeteorContactState>();
            if(!state)state=owner.gameObject.AddComponent<MeteorContactState>();
            state.streams[(planet?2:0)+(water?1:0)].Add(meteor,point);
        }

        void OnEnable(){foreach(var stream in streams)DisasterStates.Active.Add(stream);}
        void OnDisable(){foreach(var stream in streams)DisasterStates.Active.Remove(stream);}
    }
}
