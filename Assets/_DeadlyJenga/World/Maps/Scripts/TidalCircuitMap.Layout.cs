using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class TidalCircuitMap
    {
        const int Red = 0, Green = 1, Purple = 2, Yellow = 3;
        const float Slab = .4f;
        static Vector3 V(float x, float y, float z) => new Vector3(x,y,z);
        static MayhemShape Box(Vector3 center, Vector3 size, int color, Quaternion rotation = default)
            => new MayhemShape(center,size,color,rotation == default ? Quaternion.identity : rotation);

        TidalCircuitPiece Unit(string name, Vector3 position, params MayhemShape[] shapes)
        {
            var module = new MayhemModule { purpose = name, position = position };
            module.shapes.AddRange(shapes);
            var obj = new GameObject(name) { layer = DeadlyJenga.Core.Layers.Block };
            obj.transform.SetParent(generated,false);
            obj.transform.localPosition = position;
            obj.transform.localRotation = shapes[0].rotation;
            var body = obj.AddComponent<Rigidbody>();
            body.isKinematic = true;
            var member = obj.AddComponent<MayhemStructuralMember>();
            member.Build(module, palette, friction, true);
            var piece = obj.AddComponent<TidalCircuitPiece>();
            piece.Map = this;
            piece.Body = body;
            piece.Rest = position;
            member.StructuralLoad = force => { if (force.magnitude >= 4) piece.Unseat(); };
            pieces.Add(piece);
            bodies.Add(body);
            return piece;
        }

        void Land(float x,float z,float rx,float rz) => shores.Add(new Vector4(x,z,rx,rz));
        TidalCircuitPiece Pad(string name,float x,float z,float top,float width,float depth,
            TidalCircuitPiece lower = null, float bottom = 0)
        {
            var surface = Unit(name, V(x,top-Slab/2,z),Box(Vector3.zero,V(width,Slab,depth),Red));
            float height = top-Slab-bottom;
            foreach (int side in new[]{-1,1})
            foreach (int end in new[]{-1,1})
            {
                var post = Unit(name+" post "+side+" "+end,
                    V(x+side*(width/2-.42f),bottom+height/2,z+end*(depth/2-.42f)),
                    Box(Vector3.zero,V(.62f,height,.62f),Green));
                post.Supported.Add(surface);
                if (lower) lower.Supported.Add(post);
            }
            if (!lower) Land(x,z,width/2+2,depth/2+2);
            return surface;
        }

        // Route endpoints are the edges of the two seats, at walking-surface height.
        TidalCircuitPiece Span(string name,Vector3 a,Vector3 b,float width,TidalCircuitPiece first,TidalCircuitPiece last,int finish=Purple)
        {
            var delta=b-a;
            var rot=Quaternion.LookRotation(delta);
            var seat=Unit(name,(a+b)/2-rot*Vector3.up*.16f,
                Box(Vector3.zero,V(width,.32f,delta.magnitude+.12f),finish,rot));
            first.Supported.Add(seat);
            last.Supported.Add(seat);
            return seat;
        }

        void BuildCircuit()
        {
            var hub = Pad("Hub lower",0,0,1.6f,14,10);
            var middle = Pad("Hub middle",0,0,5,11,8,hub,1.6f);
            var crown = Pad("Hub crown",0,0,8.4f,8,6,middle,5);
            var pads = new TidalCircuitPiece[3,5];
            for(int row=0;row<3;row++)
            for(int col=0;col<5;col++)
            {
                if(row==1 && col==2){pads[row,col]=hub;continue;}
                float x=(col-2)*13,z=(row-1)*16;
                pads[row,col]=Pad("Red landing "+row+" "+col,x,z,1.6f,5,4);
            }
            for(int row=0;row<3;row++)
            for(int col=0;col<5;col++)
            {
                float x=(col-2)*13,z=(row-1)*16;
                bool center=row==1 && col==2;
                if(col<4) Span("Low east link "+row+" "+col,V(x+(center?7:2.5f),1.6f,z),
                    V(x+13-(row==1 && col==1?7:2.5f),1.6f,z),2,pads[row,col],pads[row,col+1],col%2==0?Purple:Red);
                if(row<2 && (col==0 || col==4))
                {
                    float outer=x+Mathf.Sign(x)*8;
                    var a=Pad("Bypass near "+row+" "+col,outer,z+3,2.2f,3,3);
                    var b=Pad("Bypass far "+row+" "+col,outer,z+13,2.2f,3,3);
                    Span("Bypass entry",V(x+Mathf.Sign(x)*2.5f,1.6f,z),V(outer-Mathf.Sign(x)*1.45f,2.2f,z+2.4f),2,pads[row,col],a);
                    Span("Outer boardwalk",V(outer,2.2f,z+4.5f),V(outer,2.2f,z+11.5f),2,a,b);
                    Span("Bypass exit",V(outer-Mathf.Sign(x)*1.45f,2.2f,z+13.6f),V(x+Mathf.Sign(x)*2.5f,1.6f,z+16),2,b,pads[row+1,col]);
                }
                else if(row<2) Span("Low north link "+row+" "+col,V(x,1.6f,z+(center?5:2)),
                    V(x,1.6f,z+16-(row==0 && col==2?5:2)),2,pads[row,col],pads[row+1,col]);
            }
            foreach(int side in new[]{-1,1})
            {
                var high=Pad("High gallery "+side,side*15,10,5.5f,5,4);
                Span("Diagonal crown crossing "+side,V(side*3.6f,8.4f,2.8f),V(side*13,5.5f,9),2.2f,crown,high);
                Span("Rear gallery ascent "+side,V(side*11,1.6f,14.3f),V(side*17,5.5f,11.9f),2, pads[2,2+side],high);
                var turn=Pad("Front switchback "+side,side*12,-8,3.1f,4,4);
                Span("Lower incline "+side,V(side*13,1.6f,-14),V(side*12,3.1f,-10),2,pads[0,2+side],turn);
                Span("Middle incline "+side,V(side*10.1f,3.1f,-7),V(side*5,5,-3.8f),2,turn,middle);
                Span("Gallery switchback "+side,V(side*24,1.6f,15),V(side*17,5.5f,11),2,pads[2,2+2*side],high);
                if(side==1)
                {
                    Land(15,1.5f,2.1f,2.1f);
                    var post=Unit("Right crossover pier post",V(15,1.8f,1.5f),Box(Vector3.zero,V(.8f,3.6f,.8f),Green));
                    var pier=Unit("Right crossover pier",V(15,3.8f,1.5f),Box(Vector3.zero,V(2.8f,.4f,2.8f),Red));
                    post.Supported.Add(pier);
                    Span("Midlevel right crossover",V(5.4f,5,1.5f),V(13.65f,4,1.5f),2,middle,pier,Red);
                    Span("Right crossover exit",V(16.35f,4,1.5f),V(23.55f,1.6f,1),2,pier,pads[1,4],Red);
                }
            }
            Drum(-26,-8);
            Bank("Teeter",-26,8,2,4.2f);
            Bank("Tipping ramp",26,-8,3,3.1f);
            Sweeper(26,8);
            Span("Teeter gallery entrance",V(-17.45f,5.5f,10),V(-22.2f,3,12.1f),1.8f,
                Named("High gallery -1"),Named("Teeter north landing"));
            Span("Sweeper gallery entrance",V(17.45f,5.5f,10),V(21.2f,3.3f,8),1.8f,
                Named("High gallery 1"),Named("Sweeper deck 0"));
            Span("Drum upper entrance",V(-13.9f,3.1f,-8),V(-24.3f,2.85f,-5.25f),1.6f,
                Named("Front switchback -1"),Named("Drum boarding 1"));
            Span("Ramp upper entrance",V(13.9f,3.1f,-8),V(21.45f,3,-3.9f),1.8f,
                Named("Front switchback 1"),Named("Tipping ramp north landing"));
        }

        void Hinge(TidalCircuitPiece rotor,TidalCircuitPiece mount,Vector3 pivot,Vector3 axis,float limit,float speed)
        {
            rotor.Rotor=true;
            rotor.Body.isKinematic=replica;
            var joint=rotor.gameObject.AddComponent<HingeJoint>();
            joint.connectedBody=mount.Body;
            joint.anchor=rotor.transform.InverseTransformPoint(transform.TransformPoint(pivot));
            joint.axis=rotor.transform.InverseTransformDirection(transform.TransformDirection(axis));
            joint.breakForce=4000; joint.breakTorque=4000;
            if(limit>0)
            {
                joint.useLimits=true;
                joint.limits=new JointLimits{min=-limit,max=limit,contactDistance=.5f};
                joint.spring=new JointSpring{spring=80,damper=28,targetPosition=0};
                joint.useSpring=true;
                tilters.Add(rotor.Body);
            }
            else
            {
                joint.motor=new JointMotor{targetVelocity=speed,force=180};
                joint.useMotor=!replica;
            }
            rotor.Hinge=joint;
            mount.Supported.Add(rotor);
        }

        void Bank(string name,float x,float z,int count,float spacing)
        {
            float width=count*spacing;
            var near=Pad(name+" south landing",x,z-4.1f,3,width,2.4f);
            var far=Pad(name+" north landing",x,z+4.1f,3,width,2.4f);
            int col=x<0?0:4, row=z<0?0:1;
            Span(name+" approach",V(x-1.6f,1.6f,z-6.5f),V(x+1.6f,3,z-5.1f),2,padsFor(row,col),near);
            Span(name+" exit",V(x+1.6f,3,z+5.1f),V(x-1.6f,1.6f,z+6.5f),2,far,padsFor(row+1,col));
            for(int i=0;i<count;i++)
            {
                float px=x+(i-(count-1)*.5f)*spacing;
                float w=spacing-.55f;
                var mount=Unit(name+" cradle "+i,V(px,1.4f,z),
                    Box(Vector3.zero,V(.75f,2.8f,.85f),Purple),
                    Box(V(0,1.4f,0),V(w+.26f,.38f,.38f),Purple),
                    Box(V(0,-.30f,-2.45f),V(.9f,2.2f,.55f),Yellow),
                    Box(V(0,-.30f,2.45f),V(.9f,2.2f,.55f),Yellow));
                Land(px,z,w*.68f,3.1f);
                var rotor=Unit(name+" panel "+i,V(px,2.95f,z),
                    Box(Vector3.zero,V(w,.28f,5.62f),Red),
                    Box(V(0,.15f,-2.45f),V(w*.9f,.08f,.32f),Yellow));
                Hinge(rotor,mount,V(px,2.8f,z),Vector3.right,15,0);
            }
        }
        TidalCircuitPiece Named(string name) => pieces.Find(p=>p.name==name);
        TidalCircuitPiece padsFor(int row,int col) => Named("Red landing "+row+" "+col);

        void Drum(float x,float z)
        {
            var shapes=new List<MayhemShape>();
            for(int i=0;i<8;i++)
            {
                var rot=Quaternion.AngleAxis(45*i,Vector3.right);
                shapes.Add(Box(rot*V(0,1.04f,0),V(3.8f,.78f,1.14f),Red,rot));
                shapes.Add(Box(rot*V(0,1.46f,0),V(3.7f,.10f,.45f),Yellow,rot));
            }
            shapes.Add(Box(Vector3.zero,V(5.4f,.5f,.5f),Purple));
            var rotor=Unit("Tread drum",V(x,1.8f,z),shapes.ToArray());
            foreach(int side in new[]{-1,1})
            {
                var mount=Unit("Drum bearing "+side,V(x+side*2.5f,.9f,z),
                    Box(Vector3.zero,V(.7f,1.8f,1.55f),Green),Box(V(0,.95f,0),V(.85f,.52f,.78f),Green));
                Land(x+side*2.5f,z,1.8f,2.1f);
                if(side<0)Hinge(rotor,mount,V(x,1.8f,z),Vector3.right,0,11);
                else
                {
                    mount.Supported.Add(rotor);
                    // The shaft runs through this bearing bore.
                    var shaft=rotor.GetComponent<MayhemStructuralMember>().Shapes;
                    foreach(var cheek in mount.GetComponent<MayhemStructuralMember>().Shapes)
                        Physics.IgnoreCollision(shaft[shaft.Count-1],cheek);
                }
                var pad=Pad("Drum boarding "+side,x,z+side*2.75f,2.85f,3.6f,2.9f);
                Span("Drum access "+side,V(x,2.85f,z+side*4.2f),V(x,1.6f,z+side*6),2,pad,padsFor(side<0?0:1,0));
            }
        }

        void Sweeper(float x,float z)
        {
            // Parallel chords form a round deck with a distinct seam between each course.
            TidalCircuitPiece center=null;
            for(int i=-3;i<=3;i++)
            {
                float width=2*Mathf.Sqrt(5*5-i*i*1.3f*1.3f);
                var pad=Pad("Sweeper deck "+i,x,z+i*1.3f,3.3f,width,1.3f);
                if(i==0)center=pad;
                if(Mathf.Abs(i)==3)
                    Span("Sweeper access "+i,V(x+1.6f,3.3f,z+Mathf.Sign(i)*4.45f),V(x-1.6f,1.6f,z+Mathf.Sign(i)*6.5f),2,pad,padsFor(i<0?1:2,4));
            }
            var mount=Unit("Sweeper axle",V(x,3.63f,z),Box(Vector3.zero,V(.85f,.66f,.85f),Purple));
            center.Supported.Add(mount);
            var rotor=Unit("Four-arm sweeper",V(x,4.08f,z),
                Box(Vector3.zero,V(8.45f,.26f,.44f),Yellow),Box(Vector3.zero,V(.44f,.26f,8.45f),Yellow),
                Box(Vector3.up*.2f,V(.75f,.42f,.75f),Purple));
            Hinge(rotor,mount,V(x,3.96f,z),Vector3.up,0,10);
        }
    }
}
