using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class FossilBasinMap
    {
        static readonly Color Coral=new Color32(223,100,67,255), Clay=new Color32(173,73,49,255), Teal=new Color32(42,143,145,255),
            Timber=new Color32(98,55,44,255), Brass=new Color32(239,171,77,255), Ivory=new Color32(245,215,166,255), Ochre=new Color32(208,165,113,255);
        static Vector3 P(float x,float y,float z)=>new Vector3(x,y,z);
        static readonly Quaternion Flat=Quaternion.identity;
        sealed class Site
        {
            public Vector3 top;public Vector2 span;public FossilMember slab;
            public Vector3 Edge(Vector3 toward)
            {
                var d=toward-top; d.y=0;
                float k=Mathf.Max(Mathf.Abs(d.x)/span.x,Mathf.Abs(d.z)/span.y);
                return top+d*(.5f/k);
            }
        }
        Site Landing(string name,Vector3 top,float width,float depth)
        {
            const float thick=.52f;
            var center=top-Vector3.up*thick*.5f;
            // The isolated buried bearing pads receive only the piers directly over them.
            for(int ix=-1;ix<=1;ix+=2)for(int iz=-1;iz<=1;iz+=2)
            {
                var basePos=P(top.x+ix*(width*.5f-.58f),-1,top.z+iz*(depth*.5f-.58f));
                var pad=new GameObject(name+" buried footing");pad.transform.SetParent(structure,false);
                pad.transform.localPosition=basePos-Vector3.up*.25f;
                pad.AddComponent<BoxCollider>().size=P(1.4f,.5f,1.4f);
                int count=Mathf.CeilToInt((top.y+1-thick)/3.4f);
                float height=(top.y+1-thick)/count;
                for(int k=0;k<count;k++)
                {
                    var at=basePos+Vector3.up*((k+.5f)*height);
                    Piece(name+" pier "+ix+"/"+iz+"/"+k,at,32,c=>{
                        c.Box(at,P(.86f,height,.86f),Teal,Flat);
                        c.Box(at+Vector3.up*(height*.5f-.18f),P(1.05f,.36f,1.05f),Clay,Flat,false);
                    });
                }
            }
            var slab=Piece(name+" deck",center,46,c=>{
                c.Box(center,P(width,thick,depth),Clay,Flat);
                int nx=Mathf.CeilToInt(width/1.2f),nz=Mathf.CeilToInt(depth/1.3f);
                for(int x=0;x<nx;x++)for(int z=0;z<nz;z++)
                {
                    var p=top+P(((x+.5f)/nx-.5f)*width,-.055f,((z+.5f)/nz-.5f)*depth);
                    c.Box(p,P(width/nx-.018f,.15f,depth/nz-.018f),Color.Lerp(Coral,Clay,((x+z*3)%5)*.07f),Flat,false);
                }
                for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                {
                    var corner=top+P(x*(width*.5f-.4f),0,z*(depth*.5f-.4f));
                    c.Shaft(corner,corner+Vector3.up*.53f,.075f,.075f,Timber,false,false);
                    c.Box(corner+Vector3.up*.58f,P(.24f,.18f,.24f),Brass,Flat,false);
                }
            });
            decks.Add(slab);
            return new Site{top=top,span=new Vector2(width,depth),slab=slab};
        }
        void Flight(Site low,Site high)
        {
            Vector3 a=low.Edge(high.top),b=high.Edge(low.top);
            var dir=b-a;dir.y=0;
            int count=Mathf.Max(2,Mathf.CeilToInt(Mathf.Abs(b.y-a.y)/.22f));
            float tread=dir.magnitude/count;var facing=Quaternion.LookRotation(dir);
            var mid=(a+b)*.5f;
            var flight=Piece("Teal stair "+low.slab.name+" to "+high.slab.name,mid,24,c=>{
                for(int i=0;i<count;i++)
                {
                    var p=Vector3.Lerp(a,b,(i+.5f)/count)-Vector3.up*.14f;
                    c.Box(p,P(1.58f,.28f,tread+.012f),Teal,facing);
                    c.Box(p+facing*P(0,.145f,-tread*.42f),P(1.58f,.034f,.055f),Brass,facing,false);
                }
                for(int sign=-1;sign<=1;sign+=2)
                {
                    var off=facing*P(sign*.7f,-.31f,0);
                    c.Shaft(a+off,b+off,.13f,.13f,Timber,false,false);
                }
            });
            Socket(flight,low.slab,a-Vector3.up*.17f,3500);
            Socket(flight,high.slab,b-Vector3.up*.17f,3500);
        }
        void BuildExcavation()
        {
            var west=Landing("West apron",P(-7,1,-9),7,4);
            var camp=Landing("Dig camp",P(0,1,-9),7,4);
            var east=Landing("East apron",P(7,1,-9),7,4);
            var switchback=Landing("West switchback",P(-13,3,-4),4,3);
            var gallery=Landing("Plate gallery",P(-7,5,0),4,3);
            var saddle=Landing("Stegosaur forefoot",P(1,4.5f,-3),3.8f,3.4f);
            var lowEast=Landing("East switchback",P(9,3,-4),3.8f,3);
            var tail=Landing("Triceratops rear foot",P(14,4.9f,2),4,4);
            var horn=Landing("Triceratops forefoot",P(6,7.3f,2),4.6f,4.5f);
            var ankle=Landing("Rex near ankle",P(-12,7.2f,4),4,3.5f);
            var bridge=Landing("Rex far ankle",P(-4,8.6f,6),4.6f,4);
            var summit=Landing("Rex rib balcony",P(-8,11.5f,10),5.2f,3);
            var overlook=Landing("Frill balcony",P(11,9.4f,8),4.8f,3);
            Flight(west,switchback);Flight(switchback,gallery);Flight(gallery,ankle);
            Flight(ankle,bridge);Flight(bridge,summit);Flight(camp,saddle);
            Flight(east,lowEast);Flight(lowEast,tail);Flight(tail,horn);Flight(horn,overlook);
            Flight(saddle,horn);Flight(gallery,bridge);
            Skeleton(Dinosaur.Stegosaurus,P(-6,5.0f,-7.6f),P(.4f,7.6f,-3.9f),west,saddle);
            Skeleton(Dinosaur.Rex,P(-8.2f,12.5f,5.2f),P(-2,13.6f,4.6f),ankle,bridge);
            Skeleton(Dinosaur.Triceratops,P(13.3f,8.1f,3),P(7.0f,10.1f,2.9f),tail,horn);
        }
    }
}
