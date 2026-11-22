using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class SphinxJunctionMap
    {
        const float RailY = 10.1f, CarSpacing = 4.5f;
        readonly List<Vector3> nodes = new();
        readonly List<float> chainage = new();
        readonly List<Rigidbody> track = new();
        readonly List<Vector3> seats = new();
        readonly List<Rigidbody> train = new();
        readonly bool[] derailed = new bool[3];
        float mileage, dwell, stationDistance, total;
        void Railway()
        {
            nodes.Clear(); chainage.Clear(); track.Clear(); seats.Clear(); train.Clear();
            System.Array.Clear(derailed,0,3);
            // A long straight bore across the pyramid. The western turn faces the depot.
            Vector2[] control = {new(-18,5),new(-15,3),new(-10,4),new(-6,8),new(1,10),new(8,10),
                new(14,10),new(20,10),new(26,10),new(30,11),new(32,14),new(32,18),
                new(30,21),new(25,22),new(18,22),new(11,22),new(4,22),new(-3,22),
                new(-10,21),new(-15,19),new(-18,16),new(-19,12),new(-19,8)};
            total=0;
            foreach(var p in control) nodes.Add(new(p.x,RailY,p.y));
            for(int i=0;i<nodes.Count;i++)
            {
                var a=nodes[i]; var b=nodes[(i+1)%nodes.Count];
                var mid=(a+b)*.5f;
                float len=Vector3.Distance(a,b);
                float yaw=Quaternion.LookRotation(b-a).eulerAngles.y;
                chainage.Add(total); total+=len;
                Begin("Timber trestle bent "+i,mid.x,2.5f,mid.z,yaw);
                float h=RailY-2.5f-.61f;
                for(int s=-1;s<=1;s+=2)
                {
                    Box(s*1.27f,h*.5f,0,.58f,h,1.25f,Wood);
                    Beam(new(s*1.27f,.65f,0),new(-s*1.27f,h-.5f,0),.31f,.37f,Red);
                }
                Box(0,h+.15f,0,3.85f,.3f,1.6f,Dark);
                End();
                Begin("Railway span "+i,mid.x,RailY-.155f,mid.z,yaw);
                Box(0,0,0,3.65f,.31f,len-.06f,Wood);
                for(int j=0;j<8;j++) Box(0,.19f,(j-3.5f)*len/8,3.53f,.11f,len/8-.04f,Shade,false);
                for(int s=-1;s<=1;s+=2) Box(s*.59f,.32f,0,.105f,.18f,len,Iron,false);
                for(int j=-1;j<=1;j+=2) Box(1.72f,.55f,j*len*.38f,.13f,1.1f,.13f,Wood);
                Box(1.72f,1.04f,0,.13f,.14f,len,Wood);
                seats.Add(unit.transform.localPosition); track.Add(End());
            }
            stationDistance=chainage[21]; mileage=stationDistance; dwell=3;
            for(int i=0;i<3;i++)
            {
                var p=Sample(mileage-i*CarSpacing,out var yaw);
                Begin(i==0?"Copper Canyon locomotive":"Sunset passenger coach "+i,p.x,p.y+.42f,p.z,yaw);
                Box(0,.32f,0,1.73f,.29f,4.08f,Red);
                for(int s=-1;s<=1;s+=2) for(int ax=-1;ax<=1;ax++)
                    model.Shape(new(s*.79f,.05f,ax*1.31f),new(.68f,.22f,.68f),Iron,12,1,Quaternion.Euler(0,0,90),false);
                if(i==0)
                {
                    model.Shape(new(0,1,.54f),new(1.23f,2.24f,1.23f),Iron,16,1,Quaternion.Euler(90,0,0),true);
                    for(int j=0;j<3;j++) model.Shape(new(0,1,.05f+j*.65f),new(1.26f,.12f,1.26f),Gold,16,1,Quaternion.Euler(90,0,0),false);
                    Round(0,1.91f,1.08f,.35f,.85f,.35f,Dark,1.55f);
                    Round(0,2.33f,1.08f,.69f,.18f,.69f,Iron);
                    Round(0,1.62f,1.76f,.53f,.46f,.37f,Amber);
                }
                float cab=i==0?-1.27f:0, length=i==0?1.35f:3.61f;
                foreach(int s in new[]{-1,1})
                {
                    for(int j=-1;j<=1;j++) Box(s*.77f,1.27f,cab+j*length*.45f,.12f,1.6f,.13f,Red);
                    Box(s*.77f,.7f,cab,.14f,.46f,length*.66f,Teal);
                }
                Box(0,2.12f,cab,1.95f,.21f,length+.25f,Red);
                Box(0,2.24f,cab,1.88f,.08f,length+.18f,Gold,false);
                train.Add(End()); train[i].isKinematic=true;
            }
            Stairs("Depot boarding stair",-13,2.5f,11,2.5f,7.6f,12, -90);
        }
        int Section(float d)
        {
            int i=chainage.BinarySearch(Mathf.Repeat(d,total));
            return i>=0?i:Mathf.Clamp(~i-1,0,nodes.Count-1);
        }
        Vector3 Sample(float d,out float yaw)
        {
            d=Mathf.Repeat(d,total);
            int i=Section(d);
            var delta=nodes[(i+1)%nodes.Count]-nodes[i];
            yaw=Quaternion.LookRotation(delta).eulerAngles.y;
            return nodes[i]+delta.normalized*(d-chainage[i]);
        }
        bool Intact(float d)
        {
            int i=Section(d); var span=track[i];
            return span.gameObject.activeSelf && Vector3.Distance(span.transform.localPosition,seats[i])<.28f && span.transform.up.y>.98f;
        }
        bool OnRails(Rigidbody rb)
        {
            int i=train.IndexOf(rb);
            return i>=0 && !derailed[i];
        }
        void RecoverTrain()
        {
            if(train.Count!=3) return;
            float best=float.MaxValue;
            var p=train[0].transform.localPosition-Vector3.up*.42f;
            for(int i=0;i<nodes.Count;i++)
            {
                var delta=nodes[(i+1)%nodes.Count]-nodes[i];
                float d=Mathf.Clamp(Vector3.Dot(p-nodes[i],delta.normalized),0,delta.magnitude);
                float error=(nodes[i]+delta.normalized*d-p).sqrMagnitude;
                if(error<best) { best=error; mileage=chainage[i]+d; }
            }
            for(int i=0;i<3;i++)
                derailed[i]=!train[i].gameObject.activeSelf || Vector3.Distance(train[i].transform.localPosition,Sample(mileage-i*CarSpacing,out _)+Vector3.up*.42f)>.55f;
        }
        void DriveTrain()
        {
            bool stop=false;
            for(int i=0;i<3;i++)
            {
                float d=mileage-i*CarSpacing;
                if(!Intact(d-1.45f) || !Intact(d+1.45f))
                {
                    derailed[i]=true;
                    train[i].isKinematic=false;
                }
                stop |= derailed[i];
            }
            if(stop || !Intact(mileage+2.2f)) return;
            if(dwell>0) { dwell-=Time.fixedDeltaTime; return; }
            float travel=2.5f*Time.fixedDeltaTime;
            if(Mathf.Repeat(stationDistance-mileage,total)>0 && Mathf.Repeat(stationDistance-mileage,total)<=travel) dwell=4;
            mileage=Mathf.Repeat(mileage+travel,total);
            for(int i=0;i<3;i++)
            {
                var p=Sample(mileage-i*CarSpacing,out var yaw);
                train[i].MovePosition(transform.TransformPoint(p+Vector3.up*.42f));
                train[i].MoveRotation(transform.rotation*Quaternion.Euler(0,yaw,0));
            }
        }
    }
}
