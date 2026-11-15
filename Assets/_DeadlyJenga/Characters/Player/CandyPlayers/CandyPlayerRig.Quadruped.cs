using UnityEngine;
namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyPlayerRig
    {
        public void PoseQuadruped(float weight,float phase,bool grounded)
        {
            if(weight<=0)return;
            CapturePoseSpace();
            fingerPoseReady[0] = fingerPoseReady[1] = false;
            Vector3 World(Vector3 local)=>poseToWorld.MultiplyPoint3x4(local);
            void Blend(int bone,Vector3 local,Quaternion rotation){bones[bone].position=Vector3.Lerp(bones[bone].position,World(local),weight);bones[bone].rotation=Quaternion.Slerp(bones[bone].rotation,poseRotation*rotation,weight);}
            bones[0].localPosition=Vector3.Lerp(bones[0].localPosition,new Vector3(0,.30f,-.75f),weight);bones[0].localRotation=Quaternion.Slerp(bones[0].localRotation,Quaternion.Euler(75,0,0),weight);bones[0].localScale=Vector3.Lerp(bones[0].localScale,Vector3.one,weight);
            Blend(1,new Vector3(0,.84f,.58f),Quaternion.identity);
            for(int side=0;side<2;side++)
            {
                int upper=side==0?2:5,thigh=side==0?8:11;float sign=side==0?-1:1;
                float wave=Mathf.Sin(phase+(side==0?0:Mathf.PI));float lift=grounded?Mathf.Max(0,wave)*.10f:.19f;
                Vector3 fore=World(new Vector3(sign*.19f,.08f+lift,.48f+wave*.11f)),hind=World(new Vector3(sign*.15f,.075f+lift,-.22f-wave*.11f));
                var shoulder=World(new Vector3(sign*.20f,.64f,.39f));var hip=World(new Vector3(sign*.14f,.56f,-.13f));
                var elbow=SolveJoint(shoulder,fore,.29f*poseScale,.27f*poseScale,-poseForward);var knee=SolveJoint(hip,hind,.30f*poseScale,.29f*poseScale,poseForward);
                shoulder=Vector3.Lerp(bones[upper].position,shoulder,weight);elbow=Vector3.Lerp(bones[upper+1].position,elbow,weight);fore=Vector3.Lerp(bones[upper+2].position,fore,weight);
                hip=Vector3.Lerp(bones[thigh].position,hip,weight);knee=Vector3.Lerp(bones[thigh+1].position,knee,weight);hind=Vector3.Lerp(bones[thigh+2].position,hind,weight);
                Segment(upper,shoulder,elbow);Segment(upper+1,elbow,fore);bones[upper+2].SetPositionAndRotation(fore,poseRotation);
                Segment(thigh,hip,knee);Segment(thigh+1,knee,hind);bones[thigh+2].SetPositionAndRotation(hind,poseRotation);
                var fingers=CandyHandSkeleton.Rest(side==0,.12f);
                for(int f=0;f<5;f++)for(int j=0;j<3;j++)Segment(14+side*15+f*3+j,fore+poseRotation*(fingers.Joint(f*4+j)*poseScale),fore+poseRotation*(fingers.Joint(f*4+j+1)*poseScale),(poseRotation*Vector3.up));
            }
        }
    }
}
