using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public static partial class TowerExhibitionLayout
    {
        static void Tower(Builder b,int d)
        {
            switch(d)
            {
                case 0: SkyTree(b);break;
                case 1:
                    b.Box(new(0,.4f,0),new(32,.8f,28),"Zipper raft",3);
                    for(int t=0;t<5;t++)
                    {
                        var m=b.Cassette(new(0,.8f+t*12,0),24,24,3,0,"Mirrored zipper street");
                        for(int f=1;f<=3;f++)b.Part(m,new((t%2==0?1:-1)*15,4*f-.35f,3),new(8,.7f,12),t%2==0?1:2);
                    }
                    break;
                case 2: Lantern(b,false);break;
                case 3:
                    b.Box(new(0,.4f,0),new(38,.8f,38),"Crossing raft",3);
                    for(int t=0;t<6;t++)
                    {
                        var bar=b.Cassette(new(0,.8f+t*12,0),38,22,3,t%2*90,"Alternating inhabited box girder");
                        foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})b.Part(bar,new(x*9,6,z*8),new(2.5f,12,2.5f));
                    }
                    break;
                case 4: Lantern(b,true);break;
                case 5: DropStop(b);break;
                case 6: Saddle(b);break;
                case 7: Chalice(b);break;
                case 8: Bookfall(b);break;
                case 9: Kissing(b);break;
            }
        }
        static void SkyTree(Builder b)
        {
            b.Box(new(0,.4f,0),new(62,.8f,62),"Tri-wing raft",3);
            for(int t=0;t<6;t++)
            {
                var root=b.Module(new(0,.8f+t*12,0),"Three-wing sky-tree section");
                for(int arm=0;arm<3;arm++)
                {
                    var q=Quaternion.Euler(0,120*arm,0);
                    var wing=b.Cassette(new Vector3(0,.8f+t*12,0)+q*new Vector3(0,0,16),18,24,3,120*arm,"Integral tree wing");
                    foreach(var s in wing.shapes)root.shapes.Add(new MayhemShape(wing.position-root.position+q*s.center,s.size,s.finish,q*s.rotation,s.vertices));
                    b.modules.Remove(wing);
                }
                for(int f=1;f<=3;f++)b.Part(root,new(0,f*4-.35f,0),new(9,.7f,9),2);
            }
        }
        static void Lantern(Builder b,bool cage)
        {
            b.Box(new(0,.4f,0),new(34,.8f,34),"Faceted lantern raft",3);
            for(int t=0;t<(cage?5:6);t++)
            {
                var m=b.Module(new(0,.8f+t*12,0),cage?"Open cathedral cage":"Two-ribbon lantern shell");
                for(int i=0;i<8;i++)
                {
                    var q=Quaternion.Euler(0,i*45,0);b.Part(m,q*new Vector3(0,6,14),new(2.5f,12,2.5f));
                    if(cage&&i%2==0)b.Beam(m,q*new Vector3(-5,1,14.5f),q*new Vector3(5,11,14.5f),1.2f,1.2f,3);
                }
                for(int f=0;f<3;f++)
                {
                    float y=f*4;int side=f%2==0?1:-1;
                    // Split galleries leave the two ramp lanes uncovered at the destination floor.
                    foreach(int s in new[]{-1,1})b.Part(m,new(0,y+3.65f,s*3.5f),new(16,.7f,3),1);
                    foreach(int s in new[]{-1,1})
                    {
                        float lane=s*(8.5f+(f%2==0?2:-2));
                        b.Flight(m,new(side*-5.5f,y,lane),new(side*5.5f,y+4,lane),3);
                        foreach(int end in new[]{-1,1})b.Part(m,new(end*7,y+3.65f,s*8.5f),new(3,.7f,7.3f),2);
                    }
                    // Side courts remain connected by the central landing rather than a closed ring.
                    foreach(int end in new[]{-1,1})b.Part(m,new(end*11,y+3.65f,0),new(6,.7f,28),3);
                }
            }
        }
        static void DropStop(Builder b)
        {
            b.Box(new(0,.4f,0),new(54,.8f,36),"Drop-stop raft",3);
            for(int t=0;t<4;t++)
            {
                float y=.8f+t*15;
                // The independent escape stair is outside the cassette's drop envelope.
                var escape=b.Cassette(new(16,y,0),18,26,3,0,"Escape spine");
                for(int i=0;i<escape.shapes.Count;i++)
                {
                    var shape=escape.shapes[i];var center=shape.center;center.y*=1.25f;var size=shape.size;size.y*=1.25f;
                    Vector3[] vertices=shape.vertices==null?null:(Vector3[])shape.vertices.Clone();if(vertices!=null)for(int v=0;v<vertices.Length;v++)vertices[v].y*=1.25f;
                    escape.shapes[i]=new MayhemShape(center,size,shape.finish,shape.rotation,vertices);
                }
                var frame=b.Module(new(-10,y,0),"Broad catch frame");
                foreach(int s in new[]{-1,1})
                {
                    b.Part(frame,new(s*12,7.5f,-10),new(3,15,3));b.Part(frame,new(s*12,7.5f,10),new(3,15,3));
                    b.Part(frame,new(s*7,1,0),new(3,2,20),3);
                    b.Box(new(-10+s*7,y+2.75f,0),new(3,1.5f,20),"Removable drop shoe",2);
                }
                var cassette=b.Cassette(new(-10,y+4.2f,0),17,18,2,0,"Two-floor dropping cassette");
                b.Part(cassette,new(0,-.35f,0),new(17,.7f,18),1);
                b.Bridge(new(-3,y+8.2f,3),new(7.5f,y+10,3),3,"Loose escape crossing");
            }
        }
        static void Saddle(Builder b)
        {
            b.Box(new(0,.4f,0),new(38,.8f,32),"Trestle raft",3);
            for(int t=0;t<4;t++)
            {
                var m=b.Cassette(new(0,.8f+t*12,0),28-t*2,24,3,0,"Inhabited tapered trestle");
                foreach(int s in new[]{-1,1})
                {
                    b.Beam(m,new(s*(18-t),1,0),new(s*(11-t),11,0),18,1.6f,3);
                    b.Part(m,new(s*(11-t),11.6f,0),new(5,.8f,20),2);
                }
            }
        }
        static void Chalice(Builder b)
        {
            b.Box(new(0,.4f,0),new(36,.8f,36),"Bowl court raft",3);
            for(int t=0;t<6;t++)
            {
                var m=b.Cassette(new(0,.8f+t*8,0),20,22,2,0,"Hollow sleeve and ascent");
                foreach(int s in new[]{-1,1})
                {
                    b.Beam(m,new(s*10,4,3),new(s*18,7,3),16,.7f,1);
                    b.Part(m,new(s*16,7.65f,3),new(5,.7f,16),2);
                }
                // Wide front/back spill gaps stop the bowl becoming a sealed debris bucket.
            }
        }
        static void Bookfall(Builder b)
        {
            b.Stack(new(0,0,0),5,20,74,0,"Independent bookfall support spine");
            for(int t=0;t<3;t++)foreach(int s in new[]{-1,1})
            {
                float y=24.8f+t*12,z=(t-1)*24;var frame=b.modules.FindLast(m=>m.purpose=="Independent bookfall support spine"&&Mathf.Abs(m.position.y-y)<.1f);
                float catchHeight=y-15.6f;
                b.Box(new(s*19,catchHeight*.5f,z),new(17,catchHeight,21),"Bookfall physical landing pedestal",3);
                b.Box(new(s*19,(y+catchHeight)*.5f,z),new(17,15.6f,21),"Wing receiving foot",2);
                var wing=b.Cassette(new(s*19,y,z),18,20,3,0,"Hinged inhabited book wing");
                wing.hingeSupport=frame;wing.pivot=b.origin+new Vector3(s*11,y,z);
                // Broad feet carry standing load; hinge only keeps the wing attached to its moving frame.
            }
        }
        static void Kissing(Builder b)
        {
            b.Box(new(0,.6f,0),new(94,1.2f,34),"Tied compression raft",3);
            for(int s=-1;s<=1;s+=2)
            {
                for(int t=0;t<4;t++)
                {
                    float x=s*(36-t*6.5f);var half=b.Cassette(new(x,1.2f+t*12,0),18,26,3,0,"Sheared compression half with level floors");
                    // An affine shear leans the real bearing ribs while every floor remains horizontal.
                    for(int i=0;i<half.shapes.Count;i++)
                    {
                        var shape=half.shapes[i];var center=shape.center;center.x-=s*center.y*26/48;
                        var vertices=shape.vertices??MayhemStructureLayout.Cube(shape.size);var deformed=new Vector3[vertices.Length];
                        for(int v=0;v<vertices.Length;v++){var p=shape.rotation*vertices[v];p.x-=s*p.y*26/48;deformed[v]=p;}
                        half.shapes[i]=new MayhemShape(center,shape.size,shape.finish,Quaternion.identity,deformed);
                    }
                }
            }
            var shim=b.Module(new(0,41.2f,0),"Loose crown compression keystone");
            var crown=new[]{new Vector3(-5.333333f,0,-13),new Vector3(5.333333f,0,-13),new Vector3(1,8,-13),new Vector3(-1,8,-13),new Vector3(-5.333333f,0,13),new Vector3(5.333333f,0,13),new Vector3(1,8,13),new Vector3(-1,8,13)};
            shim.shapes.Add(new MayhemShape(Vector3.zero,new Vector3(10.666666f,8,26),2,Quaternion.identity,crown));
            foreach(int s in new[]{-1,1})b.Part(shim,new(0,8.4f,s*11.5f),new(14,.8f,3),2);
        }
    }
}
