using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    // Every pose is sampled from the disaster clock, including when a peer first sees a strike.
    sealed class ChristmasCreaturePose
    {
        readonly Transform root;
        readonly Dictionary<string,Transform> bones=new Dictionary<string,Transform>();
        readonly Dictionary<string,Quaternion> rest=new Dictionary<string,Quaternion>();
        readonly Vector3 bodyOrigin;
        readonly bool swapArms;
        static readonly string[] Legs={"Leg0","Leg1","Leg2","Leg3"}, Shins={"Shin0","Shin1","Shin2","Shin3"};
        static readonly string[] Tails={"Tail0","Tail1","Tail2"}, Shoulders={"Shoulder0","Shoulder1"}, Wrists={"Elbow0","Elbow1"};

        public ChristmasCreaturePose(Transform root,bool cat,Material palette)
        {
            this.root=root;
            var prefab=Resources.Load<GameObject>("UnderneathTreeFinish/"+(cat?"ChristmasTabby":"ChristmasSanta"));
            var model=Object.Instantiate(prefab,root,false).transform;
            foreach(var t in model.GetComponentsInChildren<Transform>(true)){bones[t.name]=t;rest[t.name]=t.localRotation;}
            var body=bones["Body"];
            Vector3 up=bones["Up"].position-body.position, forward=bones["Forward"].position-body.position;
            model.rotation=Quaternion.Inverse(Quaternion.LookRotation(forward,up))*model.rotation;
            model.localScale*=(cat?30:34)/up.magnitude;
            model.position+=root.position-body.position;
            bodyOrigin=body.localPosition;
            if(!cat) swapArms=root.InverseTransformPoint(bones[Wrists[0]].position).x>0;
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.sharedMaterial=palette;
                var bounds=skin.localBounds;
                bounds.Expand(bounds.size.magnitude*(cat?.5f:1.5f));
                skin.localBounds=bounds;
            }
        }

        void Turn(string name,float pitch,float yaw=0,float roll=0)
        {
            var bone=bones[name];
            var axes=Quaternion.Inverse(bone.parent.rotation)*root.rotation;
            bone.localRotation=axes*Quaternion.Euler(pitch,yaw,roll)*Quaternion.Inverse(axes)*rest[name];
        }

        public void Cat(DisasterFrame f)
        {
            var p=f.points[0];
            bool running=f.warning<=0 && p.life>=0;
            root.SetPositionAndRotation(p.position+(running?p.velocity*Mathf.Clamp(Time.time-f.sampledAt,0,.07f):Vector3.zero),Quaternion.LookRotation(p.velocity));
            float stride=f.age*10, air=p.size;
            float crouch=running?0:Mathf.SmoothStep(0,1,p.life+1);
            var body=bones["Body"];
            body.localPosition=bodyOrigin+body.parent.InverseTransformVector(root.up*(running?Mathf.Sin(stride*2)*.85f*(1-air):-4*crouch));
            Turn("Body",running?Mathf.Sin(stride)*3*(1-air)-air*9:6*crouch);
            Turn("Head",-5*crouch+4*air,Mathf.Sin(f.age*2)*2);
            for(int i=0;i<4;i++)
            {
                float gait=Mathf.Sin(stride+(i>=2?2.8f:0)+(i%2)*.85f);
                Turn(Legs[i],running?Mathf.Lerp(gait*29,i<2?-40:37,air):crouch*(i<2?12:-20));
                Turn(Shins[i],running?Mathf.Max(0,-gait)*20*(1-air):crouch*17);
            }
            for(int i=0;i<3;i++)Turn(Tails[i],-air*6,Mathf.Sin(f.age*(running?4:2)-i*.6f)*(8+i*2));
        }

        public void Santa(DisasterFrame f)
        {
            root.SetPositionAndRotation(f.points[0].position,Quaternion.LookRotation(f.points[0].velocity));
            int active=Mathf.Abs(f.points[1].life)>0?0:1;
            var action=f.points[active+1];
            float beat=Mathf.Sin(Mathf.PI*Mathf.Abs(action.life));
            float side=active==0?-1:1;
            Turn("Body",beat*7,side*beat*8);
            Turn("Head",beat*9+Mathf.Sin(f.age*1.5f)*1.4f,side*beat*11);
            Turn("Cap",-beat*6,0,-side*beat*9);
            for(int i=0;i<2;i++)
            {
                int index=swapArms?1-i:i;
                var hand=f.points[i+1];
                float weight=Mathf.Sin(Mathf.PI*Mathf.Abs(hand.life));
                Turn(Shoulders[index],-18*weight,0,(i==0?1:-1)*9*weight);
                Turn(Wrists[index],hand.life<0?-22*weight:12*weight);
                var wrist=bones[Wrists[index]];
                var dir=(hand.position-bones[Shoulders[index]].position).normalized;
                wrist.position=hand.position-dir*3;
            }
        }
    }
}
