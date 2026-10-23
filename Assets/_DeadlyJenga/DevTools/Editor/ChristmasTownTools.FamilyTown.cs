using DeadlyJenga.CastlePrototype;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    public static partial class ChristmasTownTools
    {
        static bool FurnishFamilyPlace(Transform c,Transform v,ChristmasTownLayout.BuildingUse use,string accent)
        {
            if(use==ChristmasTownLayout.BuildingUse.Diner)
            {
                for(int x=0;x<10;x++)for(int z=0;z<10;z++)
                    Visual(v,"Diner checker tile",new Vector3(-4.32f+x*.96f,.055f,-4.32f+z*.96f),new Vector3(.95f,.012f,.95f),(x+z)%2==0?"Frosting":"DinerTile");
                Part(c,v,"Long soda counter",new Vector3(-3.4f,.58f,0),new Vector3(1.1f,1.16f,6.8f),"Cherry");
                Visual(v,"Cream counter top",new Vector3(-3.4f,1.2f,0),new Vector3(1.35f,.13f,7.1f),"Frosting");
                for(int stool=0;stool<5;stool++)
                {
                    var p=new Vector3(-1.95f,0,-2.7f+stool*1.35f);
                    Solid(c,"Fixed counter stool",p+Vector3.up*.4f,new Vector3(.65f,.8f,.65f));
                    Visual(v,"Red vinyl stool",p+Vector3.up*.78f,new Vector3(.65f,.15f,.65f),"Cherry",PrimitiveType.Cylinder);
                    Visual(v,"Stool pedestal",p+Vector3.up*.38f,new Vector3(.16f,.75f,.16f),"Metal",PrimitiveType.Cylinder);
                    Mug(v,new Vector3(-3.15f,1.28f,p.z));
                }
                for(int end=-1;end<=1;end+=2)
                {
                    var booth=Group(v,"Family booth",new Vector3(3.15f,0,end*2.35f));
                    var body=Group(c,"Family booth",new Vector3(3.15f,0,end*2.35f));
                    for(int side=-1;side<=1;side+=2)
                    {
                        Part(body,booth,"Booth bench",new Vector3(0,.35f,side*.92f),new Vector3(2.4f,.7f,.75f),"Cherry");
                        Part(body,booth,"Quilted booth back",new Vector3(0,.85f,side*1.22f),new Vector3(2.4f,1.1f,.2f),"Rose");
                        for(int button=0;button<4;button++)Visual(booth,"Vinyl button",new Vector3(-.9f+button*.6f,1,side*1.1f),Vector3.one*.08f,"Frosting",PrimitiveType.Sphere);
                    }
                    Part(body,booth,"Booth table",new Vector3(0,.75f,0),new Vector3(2.3f,.15f,1),"Frosting");
                    Visual(booth,"Table pedestal",new Vector3(0,.35f,0),new Vector3(.18f,.7f,.18f),"Metal",PrimitiveType.Cylinder);
                    Mug(booth,new Vector3(-.6f,.84f,0));Mug(booth,new Vector3(.6f,.84f,0));
                    Visual(booth,"Pie plate",new Vector3(0,.86f,0),new Vector3(.5f,.035f,.5f),"Metal",PrimitiveType.Cylinder);
                    Visual(booth,"Cherry pie",new Vector3(0,.93f,0),new Vector3(.42f,.12f,.42f),"Caramel",PrimitiveType.Cylinder);
                    Visual(booth,"Pendant lamp",new Vector3(0,3.25f,0),new Vector3(.85f,.3f,.85f),"WarmWindow",PrimitiveType.Sphere);
                }
                MenuBoard(v,new Vector3(-4.66f,2.35f,0),-90,"SUGARPLUM DINER\nPANCAKES ALL DAY\nPIE & COCOA  $2");
                Visual(v,"Soda fountain",new Vector3(-3.4f,1.55f,2.5f),new Vector3(.55f,.55f,.8f),"Metal");
                Visual(v,"Old tabletop radio",new Vector3(-3.4f,1.5f,-2.8f),new Vector3(.65f,.4f,.3f),"Caramel");
                return true;
            }
            if(use==ChristmasTownLayout.BuildingUse.School)
            {
                for(int side=-1;side<=1;side+=2)for(int row=0;row<3;row++)
                {
                    var p=new Vector3(side*2.85f,0,-2.6f+row*2.15f);
                    Part(c,v,"School desk",p+Vector3.up*.65f,new Vector3(1.75f,.12f,.85f),"Caramel");
                    for(int leg=-1;leg<=1;leg+=2)Visual(v,"Desk leg",p+new Vector3(leg*.65f,.3f,0),new Vector3(.1f,.6f,.1f),"Mint");
                    Chair(c,v,p+Vector3.back*.75f,180,"Mint");
                    Visual(v,"Exercise book",p+new Vector3(-.3f,.74f,0),new Vector3(.45f,.045f,.32f),"Cherry");
                    Visual(v,"Pencil",p+new Vector3(.3f,.75f,0),new Vector3(.035f,.035f,.35f),"Caramel");
                }
                for(int side=-1;side<=1;side+=2)
                {
                    var board=Group(v,"Class chalkboard",new Vector3(side*2.8f,2.1f,4.72f));
                    Visual(board,"Board frame",Vector3.zero,new Vector3(3.4f,1.5f,.1f),"Caramel");
                    Visual(board,"Chalk surface",Vector3.back*.07f,new Vector3(3.2f,1.3f,.05f),"DarkGreen");
                    Label(board,side<0?"WELCOME, CLASS!\n2 + 2 = 4\nBE KIND":"OUR TOWN\nFAMILY ART NIGHT\nFRIDAY AT SIX",Vector3.back*.11f,.12f,3f,1.1f);
                }
                Part(c,v,"Book cubbies",new Vector3(4.3f,.65f,3.2f),new Vector3(.7f,1.3f,1.7f),"Caramel");
                for(int i=0;i<4;i++)Visual(v,"Cubby book",new Vector3(3.9f,.4f+i*.23f,3.2f),new Vector3(.18f,.16f,1.2f),i%2==0?"Cherry":"Mint");
                for(int i=0;i<4;i++)FamilyDrawing(v,new Vector3(-4.68f,1.8f,-3+i*1.6f),-90,i);
                Visual(v,"Class reading rug",new Vector3(0,.06f,2.3f),new Vector3(2.2f,.015f,2),"Lavender");
                Visual(v,"School ceiling light",new Vector3(0,3.65f,0),new Vector3(2,.12f,.7f),"WarmWindow");
                return true;
            }
            if(use==ChristmasTownLayout.BuildingUse.Grocery)
            {
                Part(c,v,"General store shelves",new Vector3(4.25f,1.2f,0),new Vector3(.7f,2.4f,7.7f),"Cocoa");
                for(int shelf=0;shelf<3;shelf++)
                {
                    Visual(v,"Grocery shelf",new Vector3(4.1f,.4f+shelf*.72f,0),new Vector3(.95f,.12f,7.8f),"Caramel");
                    for(int item=0;item<15;item++)
                        Visual(v,"Pantry tin",new Vector3(3.9f,.65f+shelf*.72f,-3.4f+item*.48f),new Vector3(.3f,.38f,.3f),item%3==0?"Cherry":item%3==1?"Mint":"Frosting",PrimitiveType.Cylinder);
                }
                Part(c,v,"Neighborhood checkout",new Vector3(-3.25f,.55f,-2.7f),new Vector3(2,1.1f,1.3f),"Mint");
                Visual(v,"Old cash register",new Vector3(-3.25f,1.3f,-2.7f),new Vector3(.6f,.4f,.6f),"Frosting");
                for(int row=0;row<3;row++)
                {
                    var p=new Vector3(-3.4f,.45f,.1f+row*1.3f);
                    Part(c,v,"Produce crate",p,new Vector3(1.8f,.9f,1),"Caramel");
                    for(int x=0;x<4;x++)for(int z=0;z<2;z++)
                        Visual(v,"Fresh produce",p+new Vector3(-.6f+x*.4f,.54f,-.23f+z*.46f),new Vector3(.35f,.3f,.35f),row==0?"Cherry":row==1?"Mint":"Caramel",PrimitiveType.Sphere);
                }
                MenuBoard(v,new Vector3(-4.65f,2.35f,1),-90,"MAPLE CORNER\nMILK - BREAD - APPLES\nNEIGHBORS SINCE 1958");
                Visual(v,"Grocery runner",new Vector3(0,.056f,0),new Vector3(1.9f,.012f,7),"Rose");
                return true;
            }
            return false;
        }

        static void FamilyDrawing(Transform v,Vector3 p,float yaw,int variant)
        {
            var picture=Group(v,"Family artwork",p,yaw);
            Visual(picture,"Wood picture frame",Vector3.zero,new Vector3(.95f,.8f,.07f),"Caramel");
            Visual(picture,"Drawing paper",Vector3.back*.05f,new Vector3(.82f,.67f,.03f),"Frosting");
            Visual(picture,"Child's house drawing",new Vector3(-.16f,-.12f,-.075f),new Vector3(.26f,.26f,.012f),variant%2==0?"Rose":"Mint");
            Visual(picture,"Drawing roof",new Vector3(-.16f,.08f,-.08f),new Vector3(.24f,.24f,.012f),"Cherry",PrimitiveType.Cube,Quaternion.Euler(0,0,45));
            for(int person=0;person<3;person++)
            {
                Visual(picture,"Family drawing head",new Vector3(.08f+person*.1f,.01f,-.08f),new Vector3(.07f,.08f,.012f),"Caramel",PrimitiveType.Sphere);
                Visual(picture,"Family drawing body",new Vector3(.08f+person*.1f,-.13f,-.08f),new Vector3(.05f,.19f,.012f),person%2==0?"Lavender":"Cherry");
            }
        }

        static void FamilyFacade(Transform c,Transform v,ChristmasTownLayout.BuildingUse use,string accent)
        {
            if(use==ChristmasTownLayout.BuildingUse.Home)
            {
                FamilyDrawing(v,new Vector3(-4.69f,2.05f,-1.8f),-90,0);
                Part(c,v,"Kitchen cupboard",new Vector3(3.6f,.5f,-.7f),new Vector3(1.4f,1,.75f),"Caramel");
                Visual(v,"Cookie jar",new Vector3(3.6f,1.2f,-.7f),new Vector3(.3f,.35f,.3f),"Frosting",PrimitiveType.Cylinder);
                for(int boot=0;boot<2;boot++)Visual(v,"Boots by the door",new Vector3(1.9f+boot*.3f,.14f,-4.35f),new Vector3(.2f,.28f,.4f),"Cocoa");
                return;
            }
            if(use!=ChristmasTownLayout.BuildingUse.Diner&&use!=ChristmasTownLayout.BuildingUse.Grocery&&use!=ChristmasTownLayout.BuildingUse.School)return;
            for(int stripe=0;stripe<16;stripe++)
                Visual(v,"Striped welcome awning",new Vector3(-4.2f+stripe*.56f,2.92f,-5.85f),new Vector3(.56f,.16f,1.7f),stripe%2==0?(use==ChristmasTownLayout.BuildingUse.Grocery?"Mint":"Cherry"):"Frosting",PrimitiveType.Cube,Quaternion.Euler(-9,0,0));
            Visual(v,"Awning cream valance",new Vector3(0,2.7f,-6.65f),new Vector3(8.95f,.35f,.1f),"Frosting");
            if(use==ChristmasTownLayout.BuildingUse.Diner)
            {
                Visual(v,"Diner roof marquee",new Vector3(0,7.45f,-1),new Vector3(7.2f,1.05f,.3f),"Cherry");
                Label(v,"SUGARPLUM DINER",new Vector3(0,7.45f,-1.18f),.31f,6.8f,.8f);
                for(int i=0;i<13;i++)Visual(v,"Marquee bulb",new Vector3(-3.3f+i*.55f,7,-1.2f),Vector3.one*.1f,"WarmWindow",PrimitiveType.Sphere);
                var board=Group(v,"Diner window invitation",new Vector3(3.15f,2,-5.35f));
                Visual(board,"Diner invitation",Vector3.zero,new Vector3(2.4f,1.3f,.08f),"Cherry");
                Label(board,"MEET YOU\nAT OUR USUAL",Vector3.back*.06f,.14f,2.1f,1.05f);
            }
            else if(use==ChristmasTownLayout.BuildingUse.School)
            {
                Visual(v,"School bell frame",new Vector3(0,7.8f,0),new Vector3(1.65f,1.9f,1.65f),"Caramel");
                Visual(v,"Belfry opening",new Vector3(0,7.8f,-.84f),new Vector3(1.25f,1.45f,.035f),"Cocoa");
                Visual(v,"Brass school bell",new Vector3(0,7.8f,-.9f),new Vector3(.75f,.7f,.35f),"Caramel",PrimitiveType.Sphere);
                Visual(v,"Belfry snow cap",new Vector3(0,8.85f,0),new Vector3(2,.22f,2),"Frosting");
                Label(v,"CLASS OF '26",new Vector3(3.2f,1.9f,-5.3f),.16f,2.4f,.4f);
            }
            else
            {
                var sign=Group(v,"Corner store hours",new Vector3(3.15f,2,-5.35f));
                Visual(sign,"Hours board",Vector3.zero,new Vector3(2.6f,1.4f,.08f),"DarkGreen");
                Label(sign,"FAMILY OWNED\nSINCE 1958\nOPEN 7 - 7",Vector3.back*.06f,.13f,2.3f,1.15f);
            }
        }

        static void DressFamilyTown(Transform collision)
        {
            // Crossings connect the family places to the town square without raised curbs.
            foreach(float x in new[]{-26f,26f})for(int stripe=0;stripe<7;stripe++)
                Visual(art.transform,"Neighborhood crossing",new Vector3(x-1.5f+stripe*.5f,.045f,0),new Vector3(.28f,.014f,6.5f),"Frosting");
            CommunityGarden(collision,new Vector3(-26,0,-10),false);
            CommunityGarden(collision,new Vector3(26,0,-10),true);
        }

        static void CommunityGarden(Transform collision,Vector3 position,bool schoolyard)
        {
            var c=Group(collision,schoolyard?"Schoolyard":"Founders family park",position);
            var v=Group(art.transform,schoolyard?"Schoolyard":"Founders family park",position);
            Visual(v,"Community paving",new Vector3(0,.035f,0),new Vector3(10,.035f,7),"Paving");
            Visual(v,"Community center path",new Vector3(0,.057f,0),new Vector3(2.6f,.012f,9),"Caramel");
            for(int side=-1;side<=1;side+=2)
            {
                Part(c,v,"Low park fence",new Vector3(side*4.9f,.42f,0),new Vector3(.12f,.84f,6.5f),"Caramel");
                for(int end=-1;end<=1;end+=2)
                {
                    Part(c,v,"Open park gate",new Vector3(side*3.25f,.42f,end*3.4f),new Vector3(3,.84f,.12f),"Caramel");
                    for(int slat=0;slat<6;slat++)Visual(v,"White fence picket",new Vector3(side*(1.85f+slat*.55f),.65f,end*3.4f),new Vector3(.14f,.9f,.16f),"Frosting");
                }
            }
            if(schoolyard)
            {
                // Fixed play equipment: no swinging joints, loose toys or dynamic bodies.
                for(int end=-1;end<=1;end+=2)
                    Part(c,v,"Play shelter post",new Vector3(3.2f,1.25f,end*1.1f),new Vector3(.18f,2.5f,.18f),"Cherry");
                Part(c,v,"Play shelter roof",new Vector3(3.2f,2.6f,0),new Vector3(2.6f,.2f,3),"Cherry");
                Part(c,v,"Play shelter bench",new Vector3(3.4f,.35f,0),new Vector3(.7f,.7f,2.2f),"Mint");
                for(int cell=0;cell<6;cell++)
                {
                    float x=-2.7f+(cell%2)*.72f,z=-2.2f+(cell/2)*1.15f;
                    Visual(v,"Hopscotch chalk",new Vector3(x,.06f,z),new Vector3(.65f,.015f,.9f),cell%2==0?"Rose":"Lavender");
                    var number=Group(v,"Hopscotch number",new Vector3(x,.075f,z));number.localRotation=Quaternion.Euler(90,0,0);Label(number,(cell+1).ToString(),Vector3.zero,.2f);
                }
                MenuBoard(v,new Vector3(3.15f,1.7f,3.3f),0,"AFTER SCHOOL CLUB\nBRING A FRIEND");
                Solid(c,"Schoolyard notice",new Vector3(3.15f,1.7f,3.3f),new Vector3(2.1f,1.3f,.15f));
            }
            else
            {
                for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                    Part(c,v,"Bandstand post",new Vector3(x*1.8f,1.65f,z*1.8f),new Vector3(.16f,3.3f,.16f),"Mint");
                Part(c,v,"Bandstand roof",new Vector3(0,3.45f,0),new Vector3(4.3f,.2f,4.3f),"Cherry");
                var roof=Group(v,"Bandstand snowy hip roof");var mesh=new Mesh{name="Snowy bandstand cap"};
                mesh.vertices=new[]{new Vector3(-2.3f,3.6f,-2.3f),new Vector3(2.3f,3.6f,-2.3f),new Vector3(2.3f,3.6f,2.3f),new Vector3(-2.3f,3.6f,2.3f),new Vector3(0,4.7f,0)};
                mesh.triangles=new[]{0,4,1,1,4,2,2,4,3,3,4,0};mesh.RecalculateNormals();
                roof.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;roof.gameObject.AddComponent<MeshRenderer>().sharedMaterial=materials["Frosting"];
                for(int side=-1;side<=1;side+=2)
                {
                    Prop(v,"bench",new Vector3(side*3.5f,0,0),1.35f,side*90);
                    Solid(c,"Park bench",new Vector3(side*3.5f,.48f,0),new Vector3(.85f,.96f,1.6f));
                }
                Visual(v,"Park name",new Vector3(0,3.1f,-1.94f),new Vector3(3.5f,.45f,.12f),"Mint");
                Label(v,"FOUNDERS FAMILY PARK",new Vector3(0,3.1f,-2.02f),.14f,3.2f,.3f);
                for(int bulb=0;bulb<7;bulb++)Visual(v,"Bandstand fairy light",new Vector3(-1.5f+bulb*.5f,2.8f,-1.9f),Vector3.one*.13f,"WarmWindow",PrimitiveType.Sphere);
            }
        }
    }
}
