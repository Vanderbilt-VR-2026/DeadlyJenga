using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    public sealed class CandyMysteryBody:MonoBehaviour,IMysteryBody
    {
        readonly List<Rigidbody> parts=new List<Rigidbody>();
        Rigidbody support;
        Vector3 supportNormal;
        float settle;
        bool frozen;
        public Vector3 Position=>parts.Count>0&&parts[0]?parts[0].position:transform.position;
        public void Initialize(Transform[] bones,float[] lengths)
        {
            settle=Time.time+1.1f;
            for(int i=14;i<bones.Length;i++)bones[i].SetParent(bones[i<29?4:7],true);
            for(int i=0;i<14;i++)
            {
                var rb=bones[i].gameObject.AddComponent<Rigidbody>();rb.mass=i==0?1:.1f;rb.linearDamping=2;rb.angularDamping=3;rb.maxDepenetrationVelocity=1;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;parts.Add(rb);
                var capsule=bones[i].gameObject.AddComponent<CapsuleCollider>();capsule.radius=i==0?.15f:i==1?.18f:.05f;capsule.height=i==0?.8f:i==1?.36f:Mathf.Max(.12f,lengths[i]);capsule.center=i==0?Vector3.up*.8f:Vector3.up*lengths[i]*.5f;
                if(i==0)rb.constraints=RigidbodyConstraints.FreezePositionX|RigidbodyConstraints.FreezePositionZ;
                else
                {
                    int parent=i==3?2:i==4?3:i==6?5:i==7?6:i==9?8:i==10?9:i==12?11:i==13?12:0;
                    var joint=bones[i].gameObject.AddComponent<CharacterJoint>();joint.connectedBody=parts[parent];joint.enablePreprocessing=false;
                    joint.lowTwistLimit=new SoftJointLimit{limit=-35};joint.highTwistLimit=new SoftJointLimit{limit=35};joint.swing1Limit=new SoftJointLimit{limit=65};joint.swing2Limit=new SoftJointLimit{limit=35};
                }
                foreach(var other in parts)if(other!=rb)Physics.IgnoreCollision(capsule,other.GetComponent<Collider>());
                foreach(var mover in Tracked.Movers)if(mover is Component c)foreach(var collider in c.GetComponentsInChildren<Collider>())Physics.IgnoreCollision(capsule,collider);
                foreach(var flyer in Tracked.Flyers)if(flyer is Component c)foreach(var collider in c.GetComponentsInChildren<Collider>())Physics.IgnoreCollision(capsule,collider);
            }
        }
        public void Follow(Rigidbody body,Vector3 position)
        {
            var positions=new Vector3[parts.Count];
            for(int i=0;i<parts.Count;i++)if(parts[i])positions[i]=parts[i].transform.position;
            Vector3 anchor=parts.Count>0&&parts[0]?positions[0]:transform.position;
            Vector3 delta=position-anchor;
            transform.position+=delta;
            for(int i=0;i<parts.Count;i++)if(parts[i])
            {
                parts[i].position=positions[i]+delta;
                if(!parts[i].isKinematic){parts[i].linearVelocity=Vector3.zero;parts[i].angularVelocity=Vector3.zero;}
            }
            Physics.SyncTransforms();
            support=body;supportNormal=body?body.transform.InverseTransformDirection(Vector3.up):Vector3.up;
        }
        void LateUpdate()
        {
            if(Position.y<(Services.World?.WaterHeight(Position)??-100)-2){gameObject.SetActive(false);return;}
            if(!frozen&&Time.time>=settle)
            {
                bool supported=support;
                if(!supported&&parts.Count>0&&parts[0].linearVelocity.sqrMagnitude<.5f&&Physics.Raycast(parts[0].worldCenterOfMass+Vector3.up*.2f,Vector3.down,out var hit,Mathf.Max(.4f,transform.lossyScale.y*1.2f),(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore))
                {supported=true;support=hit.rigidbody;if(support)supportNormal=support.transform.InverseTransformDirection(hit.normal);}
                if(supported){foreach(var p in parts)if(p){p.isKinematic=true;p.GetComponent<Collider>().enabled=false;}frozen=true;if(support)transform.SetParent(support.transform,true);}
            }
            if(!frozen)return;
            if(support&&Vector3.Dot(support.transform.TransformDirection(supportNormal),Vector3.up)<-.8f)
            {transform.SetParent(null,true);support=null;foreach(var p in parts)if(p){p.GetComponent<Collider>().enabled=true;p.isKinematic=false;p.linearVelocity=Vector3.zero;}settle=Time.time+1;frozen=false;}
        }
    }
}
