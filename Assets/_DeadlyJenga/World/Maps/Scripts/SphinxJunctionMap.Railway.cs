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
            Vector2[] control = {new(-18,5),new(-15,3),new(-11,7.8f),new(-7,9.1f),new(1,12.5f),new(10,10),
                new(14,10),new(20,10),new(26,10),new(30,11),new(35.2f,15.8f),new(35.2f,20.2f),
                new(34.5f,23),new(25,25),new(18,25),new(11,24),new(4,22),new(-3,22),
                new(-10,21),new(-16,20),new(-19,16.5f),new(-19,12),new(-19,8)};
            total=0;
            for(int i=0;i<control.Length;i++)
            {
                var p=control[i];
                var q=i>=5 && i<=9 ? PyramidPoint(p.x-20,p.y-10) : new Vector3(p.x,0,p.y);
                nodes.Add(new(q.x,RailY,q.z));
            }
            for(int i=0;i<nodes.Count;i++)
            {
                var a=nodes[i]; var b=nodes[(i+1)%nodes.Count];
                var mid=(a+b)*.5f;
                float len=Vector3.Distance(a,b);
                float yaw=Quaternion.LookRotation(b-a).eulerAngles.y;
                chainage.Add(total); total+=len;
                float bottom=i==10?4.28f:i==11?-2:2.5f;
                if(i==10)
                {
                    // Each offshore timber shoe carries one post. Either shoe can be withdrawn.
                    for(int side=-1;side<=1;side+=2)
                    {
                        var foot=mid+Quaternion.Euler(0,yaw,0)*new Vector3(side*1.22f,0,0);
                        Begin("Driven trestle pile "+side,foot.x,0,foot.z,yaw);
                        Box(0,-.62f,0,.94f,3.24f,1.42f,Wood);
                        End(true);
                        Begin(side==-1?"Timber trestle bent 10":"Outer trestle shoe",foot.x,2.64f,foot.z,yaw);
                        Box(0,0,0,.92f,3.28f,1.4f,Red);
                        Box(0,-1.46f,0,1.10f,.31f,1.53f,Iron,false);
                        Box(0,1.46f,0,1.10f,.31f,1.53f,Iron,false);
                        End();
                    }
                    Begin("East trestle frame",mid.x,bottom,mid.z,yaw);
                }
                else Begin("Timber trestle bent "+i,mid.x,bottom,mid.z,yaw);
                float h=RailY-bottom-.61f;
                for(int s=-1;s<=1;s+=2)
                {
                    Box(s*1.22f,h*.5f,0,.58f,h,i<2?2.22f:1.25f,Wood);
                    Beam(new(s*1.22f,.65f,0),new(-s*1.22f,h-.5f,0),.31f,.37f,Red);
                }
                Box(0,h+.15f,0,3.85f,.3f,i<2?2.6f:1.6f,Dark);
                End(bottom<0);
                Begin("Railway span "+i,mid.x,RailY-.155f,mid.z,yaw);
                var forward=(b-a).normalized;
                var right=Vector3.Cross(Vector3.up,forward);
                var entry=forward+(a-nodes[(i+nodes.Count-1)%nodes.Count]).normalized;
                var exit=forward+(nodes[(i+2)%nodes.Count]-b).normalized;
                model.SetTrackMiters(len,-Vector3.Dot(entry,right)/Vector3.Dot(entry,forward),
                    -Vector3.Dot(exit,right)/Vector3.Dot(exit,forward));
                Box(0,0,0,4.04f,.31f,len-.06f,Wood);
                for(int j=0;j<8;j++) Box(0,.19f,(j-3.5f)*len/8,3.53f,.11f,len/8-.04f,Shade,false);
                for(int s=-1;s<=1;s+=2) Box(s*.59f,.32f,0,.105f,.18f,len,Iron,false);
                for(int j=-1;j<=1;j+=2) Box(1.94f,.55f,j*len*.38f,.13f,1.1f,.13f,Wood);
                Box(1.94f,1.04f,0,.13f,.14f,len,Wood);
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
                    Round(0,1.67f,-.06f,.52f,.36f,.52f,Gold,.86f);
                    Round(0,1.62f,1.76f,.53f,.46f,.37f,Amber);
                    for(int bar=-3;bar<=3;bar++)
                        Beam(new(bar*.23f,.54f,1.78f),new(bar*.22f,.04f,2.36f-Mathf.Abs(bar)*.08f),.08f,.09f,Gold);
                    for(int side=-1;side<=1;side+=2)
                    {
                        Box(side*.93f,.05f,0,.09f,.10f,2.67f,Gold,false);
                        for(int axle=-1;axle<=1;axle++)
                            model.Shape(new(side*.917f,.05f,axle*1.31f),new(.29f,.08f,.29f),Gold,10,1,Quaternion.Euler(0,0,90),false);
                    }
                }
                float cab=i==0?-1.27f:0, length=i==0?1.35f:3.61f;
                if(i!=0)
                {
                    for(int end=-1;end<=1;end+=2)
                    {
                        Box(0,.64f,end*1.36f,1.35f,.30f,.43f,Wood);
                        Box(0,.95f,end*1.56f,1.35f,.45f,.11f,Red);
                    }
                    Box(-.97f,.24f,0,.30f,.14f,1.28f,Wood);
                    Box(.97f,.24f,0,.30f,.14f,1.28f,Wood);
                }
                foreach(int s in new[]{-1,1})
                {
                    for(int j=-1;j<=1;j++) Box(s*.77f,1.27f,cab+j*length*.45f,.12f,1.6f,.13f,Red);
                    Box(s*.77f,.7f,cab,.14f,.46f,length*.66f,Teal);
                }
                Box(0,2.12f,cab,1.95f,.21f,length+.25f,Red);
                Box(0,2.24f,cab,1.88f,.08f,length+.18f,Gold,false);
                train.Add(End()); train[i].isKinematic=true;
            }
            Begin("Depot platform timber bent",-16.35f,2.5f,12.2f);
            foreach(int end in new[]{-1,1})
            {
                foreach(int side in new[]{-1,1})
                    Box(side*.39f,3.82f,end*2.75f,.30f,7.64f,.74f,Wood);
                Box(0,7.80f,end*2.75f,2.12f,.32f,.9f,Dark);
                Beam(new(0,.75f,end*2.75f),new(0,7.3f,-end*2.75f),.27f,.27f,Dark);
            }
            var platformBent=End();
            Begin("Depot boarding platform",-16.35f,10.68f,12.2f);
            Box(0,0,0,3.2f,.44f,7.15f,Wood);
            for(int j=-4;j<=4;j++) Box(0,.231f,j*.81f,3.2f,.05f,.79f,Honey,false);
            foreach(int end in new[]{-1,1}) Round(0,.255f,end*2.75f,.14f,.035f,.14f,Iron);
            Fasten(End(),platformBent);
            Stairs("Depot boarding stair",-8.74f,2.5f,14.38f,2.3f,8.4f,12,-90);
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
