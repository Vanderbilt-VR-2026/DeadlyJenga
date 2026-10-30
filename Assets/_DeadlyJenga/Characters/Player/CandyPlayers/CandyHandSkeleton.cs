using Fusion;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    // Twenty finger joints, 1mm precision in wrist-relative, unscaled metres. Unmanaged input fits Fusion's input payload.
    public struct CandyHandSkeleton : INetworkStruct
    {
        public ulong J0,J1,J2,J3,J4,J5,J6,J7,J8,J9,J10,J11,J12,J13,J14,J15,J16,J17,J18,J19;
        ulong Get(int i)=>i switch{0=>J0,1=>J1,2=>J2,3=>J3,4=>J4,5=>J5,6=>J6,7=>J7,8=>J8,9=>J9,10=>J10,11=>J11,12=>J12,13=>J13,14=>J14,15=>J15,16=>J16,17=>J17,18=>J18,_=>J19};
        public Vector3 Joint(int i){ulong p=Get(i);return new Vector3((short)p,(short)(p>>16),(short)(p>>32))*.001f;}
        public void SetJoint(int i,Vector3 v)
        {
            ushort Q(float value)=>(ushort)(short)Mathf.Clamp(Mathf.RoundToInt(value*1000),short.MinValue,short.MaxValue);
            ulong p=(ulong)Q(v.x)|((ulong)Q(v.y)<<16)|((ulong)Q(v.z)<<32);
            switch(i){case 0:J0=p;break;case 1:J1=p;break;case 2:J2=p;break;case 3:J3=p;break;case 4:J4=p;break;case 5:J5=p;break;case 6:J6=p;break;case 7:J7=p;break;case 8:J8=p;break;case 9:J9=p;break;case 10:J10=p;break;case 11:J11=p;break;case 12:J12=p;break;case 13:J13=p;break;case 14:J14=p;break;case 15:J15=p;break;case 16:J16=p;break;case 17:J17=p;break;case 18:J18=p;break;default:J19=p;break;}
        }
        public static CandyHandSkeleton Rest(bool left,float curl=0)
        {
            var data=new CandyHandSkeleton();float sign=left?1:-1;curl=Mathf.Clamp01(curl);
            for(int finger=0;finger<5;finger++)
            {
                Vector3 p=finger==0?new Vector3(sign*.033f,-.008f,.012f):new Vector3(sign*(.028f-(finger-1)*.019f),-.004f,.05f);
                data.SetJoint(finger*4,p);float length=finger==4?.051f:finger==2?.076f:.067f;
                for(int joint=0;joint<3;joint++)
                {
                    var segment=finger==0?new Vector3(sign*.009f,-.003f,.018f):Vector3.forward*(length/3);
                    var rotation=finger==0?Quaternion.Euler(curl*20,-sign*curl*(joint+1)*20,0):Quaternion.Euler(curl*(joint+1)*30,0,0);
                    p+=rotation*segment;data.SetJoint(finger*4+joint+1,p);
                }
            }
            return data;
        }
    }
    public struct CandyHandPose
    {
        public bool tracked,skeletonTracked;
        public Vector3 position;
        public Quaternion rotation;
        public CandyHandSkeleton skeleton;
    }
    public struct CandyAvatarFrame
    {
        public Vector3 feet,head;
        public Quaternion headRotation;
        public float scale;
        public CandyHandPose left,right;
    }
}
