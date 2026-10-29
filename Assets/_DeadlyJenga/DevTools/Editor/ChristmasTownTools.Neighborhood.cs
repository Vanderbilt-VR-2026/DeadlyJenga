using System;
using System.Collections.Generic;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    public static partial class ChristmasTownTools
    {
        static void FurnishBuilding(Transform c,Transform v,int index,string accent)
        {
            var use=ChristmasTownLayout.Houses[index].use;
            if(FurnishFamilyPlace(c,v,use,accent))return;
            if(use==ChristmasTownLayout.BuildingUse.Home)
            {
                Part(c,v,"Bed",new Vector3(3.1f,.35f,1.3f),new Vector3(2.2f,.7f,3.3f),"Cocoa");
                Visual(v,"Quilt",new Vector3(3.1f,.76f,1.5f),new Vector3(2.15f,.2f,2.8f),index%2==0?"Rose":"Mint");
                Visual(v,"Pillow",new Vector3(3.1f,.91f,2.5f),new Vector3(1.7f,.25f,.6f),"Frosting",PrimitiveType.Sphere);
                Visual(v,"Headboard",new Vector3(3.1f,.85f,3),new Vector3(2.35f,1.3f,.16f),"Cocoa");
                Part(c,v,"Sofa",new Vector3(-3.4f,.45f,-1.5f),new Vector3(1.4f,.9f,2.4f),accent);
                Visual(v,"Sofa back",new Vector3(-4,1,-1.5f),new Vector3(.3f,1.1f,2.4f),accent);
                for(int cushion=0;cushion<2;cushion++)Visual(v,"Cushion",new Vector3(-3.5f,1,-2.1f+cushion*1.1f),new Vector3(.7f,.4f,.8f),"Frosting",PrimitiveType.Sphere);
                Part(c,v,"Hearth",new Vector3(-3.5f,.7f,2.8f),new Vector3(1.9f,1.4f,1),"Biscuit");
                Visual(v,"Hearth opening",new Vector3(-3.5f,.65f,2.22f),new Vector3(1.3f,.8f,.08f),"Cocoa");
                Visual(v,"Warm hearth",new Vector3(-3.5f,.46f,2.16f),new Vector3(.8f,.35f,.1f),"WarmWindow",PrimitiveType.Sphere);
                Visual(v,"Mantel",new Vector3(-3.5f,1.5f,2.8f),new Vector3(2.2f,.15f,1.2f),"Frosting");
                Prop(v,"sock-red",new Vector3(-3.5f,1.3f,2.1f),.5f);
                Table(c,v,new Vector3(3,.0f,-2.7f),"Small dining table",accent);
                Mug(v,new Vector3(3,.86f,-2.7f));
                Visual(v,"Woven rug",new Vector3(0,.052f,.7f),new Vector3(3,.012f,3),"Rose");
                Visual(v,"Ceiling shade",new Vector3(0,3.45f,.6f),new Vector3(.9f,.25f,.9f),"WarmWindow",PrimitiveType.Sphere);
                for(int side=-1;side<=1;side+=2)for(int curtain=0;curtain<2;curtain++)
                    Visual(v,"Window curtains",new Vector3(side*4.72f,2,curtain==0?1:3.7f),new Vector3(.14f,1.7f,.4f),index%2==0?"Rose":"Lavender");
                return;
            }
            if(use==ChristmasTownLayout.BuildingUse.Cafe)
            {
                Part(c,v,"Cafe serving counter",new Vector3(-3.3f,.55f,1),new Vector3(1.4f,1.1f,4),accent);
                for(int side=-1;side<=1;side+=2)
                {
                    var point=new Vector3(3,0,side*2.1f);Table(c,v,point,"Cafe table",accent);Mug(v,point+new Vector3(.22f,.86f,0));Mug(v,point+new Vector3(-.22f,.86f,0));
                    Chair(c,v,point+new Vector3(1.3f,0,0),90,accent);Chair(c,v,point+new Vector3(-1.3f,0,0),-90,accent);
                }
                Visual(v,"Cafe rug",new Vector3(0,.052f,1),new Vector3(2.4f,.01f,4),"Rose");
                MenuBoard(v,new Vector3(-4.65f,2.05f,0),-90,"HOT COCOA\nGINGER COOKIES\nOPEN DAILY");return;
            }
            ShopFurniture(c,v,index,accent);
            if(use==ChristmasTownLayout.BuildingUse.Books)
            {
                for(int shelf=0;shelf<3;shelf++)for(int book=0;book<9;book++)
                    Visual(v,"Book spine",new Vector3(3.78f,.7f+shelf*.75f,-.4f+book*.4f),new Vector3(.45f,.5f+(book%3)*.06f,.22f),book%3==0?"Cherry":book%3==1?"Mint":"Lavender");
                MenuBoard(v,new Vector3(-4.65f,2.1f,0),-90,"BOOK CLUB\nTHURSDAYS\nALL WELCOME");
            }
            else if(use==ChristmasTownLayout.BuildingUse.Workshop)
            {
                for(int toy=0;toy<4;toy++)Prop(v,toy%2==0?"gingerbread-man":"nutcracker",new Vector3(-3.3f,1.12f,-.2f+toy*.75f),.4f,toy*25);
                Prop(v,"sled",new Vector3(2.8f,0,-2.8f),1.1f,90);
                MenuBoard(v,new Vector3(-4.65f,2.3f,0),-90,"TOY REPAIRS\nWRAPPING DESK");
            }
            else if(use==ChristmasTownLayout.BuildingUse.PostOffice)
            {
                for(int parcel=0;parcel<5;parcel++)Prop(v,"present-a-rectangle",new Vector3(-3.2f,1.1f,-.7f+parcel*.7f),.55f);
                MenuBoard(v,new Vector3(-4.65f,2.3f,0),-90,"LAST POST 5 PM\nLETTERS TO SANTA");
            }
            else if(use==ChristmasTownLayout.BuildingUse.Civic)
            {
                MenuBoard(v,new Vector3(-4.65f,2.1f,0),-90,"TOWN MEETING\nCAROLS AT SIX\nMARKET TODAY");
                Table(c,v,new Vector3(2.5f,0,-2.5f),"Council desk","Cocoa");
            }
            else if(index==0)
            {
                for(int tray=0;tray<4;tray++)
                {
                    Visual(v,"Bakery tray",new Vector3(-3.3f,1.14f,-.4f+tray*.8f),new Vector3(1,.06f,.65f),"Frosting");
                    for(int roll=0;roll<3;roll++)Visual(v,"Fresh bread",new Vector3(-3.6f+roll*.3f,1.27f,-.4f+tray*.8f),new Vector3(.24f,.2f,.43f),"Caramel",PrimitiveType.Sphere);
                }
            }
        }

        static void Table(Transform c,Transform v,Vector3 p,string name,string color)
        {
            Solid(c,name,p+Vector3.up*.4f,new Vector3(1.35f,.8f,1.35f));
            Visual(v,name+" top",p+Vector3.up*.78f,new Vector3(1.5f,.12f,1.5f),color,PrimitiveType.Cylinder);
            Visual(v,name+" pedestal",p+Vector3.up*.38f,new Vector3(.3f,.75f,.3f),"Cocoa",PrimitiveType.Cylinder);
        }
        static void Chair(Transform c,Transform v,Vector3 p,float yaw,string color)
        {
            var solid=Group(c,"Chair",p,yaw);var visual=Group(v,"Chair",p,yaw);
            Solid(solid,"Chair body",new Vector3(0,.45f,0),new Vector3(.65f,.9f,.65f));
            Visual(visual,"Seat",new Vector3(0,.45f,0),new Vector3(.65f,.12f,.65f),color);
            Visual(visual,"Chair back",new Vector3(0,.85f,.28f),new Vector3(.65f,.7f,.12f),color);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Visual(visual,"Chair leg",new Vector3(x*.23f,.23f,z*.23f),new Vector3(.1f,.45f,.1f),"Cocoa");
        }
        static void Mug(Transform v,Vector3 p)
        {
            Visual(v,"Cocoa mug",p+Vector3.up*.1f,new Vector3(.18f,.2f,.18f),"Frosting",PrimitiveType.Cylinder);
            Visual(v,"Hot cocoa",p+Vector3.up*.205f,new Vector3(.145f,.012f,.145f),"Cocoa",PrimitiveType.Cylinder);
            Visual(v,"Mug handle",p+new Vector3(.11f,.11f,0),new Vector3(.1f,.13f,.05f),"Frosting");
        }
        static void MenuBoard(Transform v,Vector3 p,float yaw,string text)
        {
            var board=Group(v,"Local notice",p,yaw);
            Visual(board,"Board frame",Vector3.zero,new Vector3(2.1f,1.3f,.12f),"Caramel");
            Visual(board,"Chalkboard",Vector3.back*.08f,new Vector3(1.9f,1.1f,.06f),"DarkGreen");
            Label(board,text,Vector3.back*.12f,.105f,1.75f,.95f);
        }

        static void HouseholdDetails(Transform c,Transform v,int index,string accent)
        {
            var use=ChristmasTownLayout.Houses[index].use;
            FamilyFacade(c,v,use,accent);
            if(index%3!=1)
            {
                Visual(v,"Cookie chimney",new Vector3(3,6.45f,2.4f),new Vector3(.85f,2.2f,.85f),"Biscuit");
                Visual(v,"Snow chimney cap",new Vector3(3,7.6f,2.4f),new Vector3(1.1f,.2f,1.1f),"Frosting");
                Visual(v,"Chimney opening",new Vector3(3,7.72f,2.4f),new Vector3(.58f,.04f,.58f),"Cocoa");
            }
            for(int side=-1;side<=1;side+=2)
            {
                Part(c,v,"Evergreen window box",new Vector3(side*3.7f,.3f,-5.7f),new Vector3(1.3f,.6f,.7f),accent);
                for(int bush=0;bush<3;bush++)Visual(v,"Planter evergreen",new Vector3(side*3.7f-.4f+bush*.4f,.7f,-5.7f),new Vector3(.5f,.55f,.55f),"DarkGreen",PrimitiveType.Sphere);
                Visual(v,"Snow in planter",new Vector3(side*3.7f,.61f,-5.7f),new Vector3(1.35f,.12f,.75f),"Frosting");
            }
            if(use==ChristmasTownLayout.BuildingUse.Home)
            {
                Visual(v,"Porch canopy",new Vector3(0,2.95f,-5.8f),new Vector3(3.9f,.2f,1.5f),"Frosting");
                for(int side=-1;side<=1;side+=2)
                {
                    Part(c,v,"Porch post",new Vector3(side*1.8f,1.4f,-6.2f),new Vector3(.18f,2.8f,.18f),accent);
                    Part(c,v,"Back garden fence",new Vector3(side*4.55f,.4f,7),new Vector3(.12f,.8f,3),"Caramel");
                    Part(c,v,"Back garden gate opening",new Vector3(side*3,.4f,8.6f),new Vector3(3.4f,.8f,.12f),"Caramel");
                    for(int slat=0;slat<6;slat++)Visual(v,"Fence picket",new Vector3(side*(1.5f+slat*.58f),.75f,8.6f),new Vector3(.14f,.6f,.2f),"Frosting");
                }
                Part(c,v,"Mailbox post",new Vector3(-2.45f,.6f,-6.4f),new Vector3(.18f,1.2f,.18f),"Cocoa");
                Visual(v,"Mailbox",new Vector3(-2.45f,1.3f,-6.4f),new Vector3(.65f,.45f,.55f),accent);
                Visual(v,"Mail slot",new Vector3(-2.45f,1.33f,-6.7f),new Vector3(.36f,.055f,.025f),"Cocoa");
                for(int log=0;log<4;log++)Visual(v,"Stacked firewood",new Vector3(-3.2f+(log%2)*.4f,.2f+(log/2)*.3f,6.2f),new Vector3(.35f,1.5f,.35f),"Biscuit",PrimitiveType.Cylinder,Quaternion.Euler(90,0,0));
                Prop(v,"sled",new Vector3(3.3f,0,6.2f),.8f,20+index*9);
                Visual(v,"Doormat",new Vector3(0,.052f,-5.3f),new Vector3(1.8f,.025f,.7f),"Cocoa");
            }
            else
            {
                var board=Group(v,"Pavement menu",new Vector3(-3.1f,.9f,-6.5f));
                Visual(board,"Menu stand",Vector3.zero,new Vector3(.8f,1.4f,.12f),accent);
                Label(board,use==ChristmasTownLayout.BuildingUse.Cafe?"COCOA\n& COOKIES":"OPEN\nCOME IN",Vector3.back*.08f,.11f,.68f,1.1f);
                Solid(c,"Menu stand",new Vector3(-3.1f,.7f,-6.5f),new Vector3(.8f,1.4f,.2f));
                Prop(v,"present-a-rectangle",new Vector3(3.1f,0,-6.2f),.7f,index*21);
            }
            // A few footprints lead to each door without adding any collision.
            var house=ChristmasTownLayout.Houses[index];var rot=Quaternion.Euler(0,house.yaw,0);
            for(int step=0;step<12;step++)
            {
                var p=house.position+rot*new Vector3(step%2==0?-.2f:.2f,.056f,-5*house.scale.z-.3f-step*.4f);
                Visual(art.transform,"Snow footprint",p,new Vector3(.14f,.008f,.28f),"Footprints",PrimitiveType.Cylinder,rot*Quaternion.Euler(0,step%2==0?-12:12,0));
            }
            if(use==ChristmasTownLayout.BuildingUse.Civic)
            {
                Visual(v,"Town clock tower",new Vector3(0,7.8f,0),new Vector3(2.4f,3,2.4f),accent);
                Visual(v,"Clock cap",new Vector3(0,9.35f,0),new Vector3(2.9f,.25f,2.9f),"Frosting");
                Visual(v,"Clock face",new Vector3(0,8.15f,-1.26f),new Vector3(1.7f,1.7f,.1f),"Frosting",PrimitiveType.Sphere);
                Visual(v,"Clock hour hand",new Vector3(.16f,8.2f,-1.34f),new Vector3(.48f,.09f,.03f),"Cocoa",PrimitiveType.Cube,Quaternion.Euler(0,0,30));
                Visual(v,"Clock minute hand",new Vector3(0,8.45f,-1.34f),new Vector3(.08f,.68f,.03f),"Cocoa");
            }
        }

        static void DressNeighborhood(Transform collision)
        {
            for(int side=-1;side<=1;side+=2)for(int end=-1;end<=1;end+=2)
            {
                var p=new Vector3(side*10,0,end*8.8f);var c=Group(collision,"Christmas market stall",p,side*90);var v=Group(art.transform,"Christmas market stall",p,side*90);
                string accent=side==end?"Cherry":"Mint";
                Part(c,v,"Market counter",new Vector3(0,.5f,.45f),new Vector3(3.5f,1,.9f),"Biscuit");
                for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Part(c,v,"Stall post",new Vector3(x*1.8f,1.2f,z),new Vector3(.13f,2.4f,.13f),"Cocoa");
                for(int stripe=0;stripe<10;stripe++)Visual(v,"Striped market canopy",new Vector3(-1.8f+stripe*.4f,2.5f,0),new Vector3(.4f,.14f,2.6f),stripe%2==0?accent:"Frosting");
                Solid(c,"Market canopy",new Vector3(0,2.5f,0),new Vector3(4,.14f,2.6f));
                Visual(v,"Market sign fascia",new Vector3(0,2.2f,-1.1f),new Vector3(3.6f,.4f,.12f),accent);
                Label(v,end<0?"COCOA & COOKIES":"GIFTS & GREENS",new Vector3(0,2.2f,-1.18f),.095f,3.3f,.3f);
                for(int stock=0;stock<5;stock++)
                    if(end>0)Prop(v,stock%2==0?"present-a-cube":"present-b-cube",new Vector3(-1.3f+stock*.65f,1.02f,.45f),.36f,stock*23);
                    else { Mug(v,new Vector3(-1.2f+stock*.6f,1.02f,.45f)); }
                var benchPoint=new Vector3(side*(end<0?20:21),0,end<0?-5.2f:7.1f);
                Prop(art.transform,"bench",benchPoint,1.4f,end<0?0:180);
                Solid(collision,"Public bench",benchPoint+Vector3.up*.45f,new Vector3(1.6f,.9f,.8f));
            }
            var notice=Group(art.transform,"Village noticeboard",new Vector3(-6.5f,1.8f,6.5f),-25);
            MenuBoard(notice,Vector3.zero,0,"CHRISTMAS MARKET\nCAROLS AT SIX\nSKATING TOMORROW");
            var noticeCollision=Group(collision,"Village noticeboard",new Vector3(-6.5f,0,6.5f),-25);
            Solid(noticeCollision,"Noticeboard panel",Vector3.up*1.8f,new Vector3(2.1f,1.3f,.2f));
            for(int side=-1;side<=1;side+=2)
            {
                Visual(notice,"Noticeboard support",new Vector3(side*.8f,-.9f,0),new Vector3(.14f,1.8f,.14f),"Cocoa");
                Solid(noticeCollision,"Noticeboard support",new Vector3(side*.8f,.9f,0),new Vector3(.14f,1.8f,.14f));
            }
            for(int side=-1;side<=1;side+=2)
            {
                var p=new Vector3(side*6,0,12);Solid(collision,"Village wayfinding post",p+Vector3.up*1.2f,new Vector3(.17f,2.4f,.17f));
                Visual(art.transform,"Village wayfinding post",p+Vector3.up*1.2f,new Vector3(.17f,2.4f,.17f),"Cocoa");
                Visual(art.transform,"Wayfinding arrow",p+new Vector3(0,2,0),new Vector3(2,.45f,.12f),"Cherry");
                Label(art.transform,side<0?"COTTAGE LANE":"MARKET ROW",p+new Vector3(0,2,-.08f),.13f,1.8f,.32f);
            }
        }

        static void ValidateConnections(ChristmasTownMap map)
        {
            const int width=93,depth=73;const int offsetX=46,offsetZ=36;
            int mask=(1<<Layers.Surface)|(1<<Layers.Block);var open=new bool[width*depth];var points=new Vector3[open.Length];
            for(int z=0;z<depth;z++)for(int x=0;x<width;x++)
            {
                int id=z*width+x;var p=new Vector3(x-offsetX,.6f,z-offsetZ);
                if(!Physics.Raycast(p,Vector3.down,out var floor,1.2f,mask,QueryTriggerInteraction.Ignore))continue;
                p.y=floor.point.y+.06f;points[id]=p;
                open[id]=!Physics.CheckCapsule(p+Vector3.up*.35f,p+Vector3.up*1.7f,.3f,mask,QueryTriggerInteraction.Ignore);
            }
            var seen=new bool[open.Length];var queue=new Queue<int>();int start=(12+offsetZ)*width+offsetX;
            if(!open[start])throw new Exception("Town plaza route start is blocked.");queue.Enqueue(start);seen[start]=true;
            int[] dx={1,-1,0,0},dz={0,0,1,-1};
            while(queue.Count>0)
            {
                int id=queue.Dequeue(),x=id%width,z=id/width;
                for(int side=0;side<4;side++)
                {
                    int nx=x+dx[side],nz=z+dz[side];if(nx<0||nz<0||nx>=width||nz>=depth)continue;
                    int next=nz*width+nx;if(seen[next]||!open[next]||Mathf.Abs(points[id].y-points[next].y)>.25f)continue;
                    var from=points[id];from.y=Mathf.Max(from.y,points[next].y);var direction=Vector3.ProjectOnPlane(points[next]-from,Vector3.up);
                    if(Physics.CapsuleCast(from+Vector3.up*.35f,from+Vector3.up*1.7f,.3f,direction.normalized,out _,direction.magnitude,mask,QueryTriggerInteraction.Ignore))continue;
                    seen[next]=true;queue.Enqueue(next);
                }
            }
            foreach(var goal in map.interiorCenters)
            {
                int x=Mathf.RoundToInt(goal.position.x)+offsetX,z=Mathf.RoundToInt(goal.position.z)+offsetZ;
                if(!seen[z*width+x])throw new Exception("Interior disconnected from the plaza: "+goal.name);
            }
            foreach(var goal in FoundationPositions())
            {int x=Mathf.RoundToInt(goal.x)+offsetX,z=Mathf.RoundToInt(goal.z)+offsetZ;if(!seen[z*width+x])throw new Exception("Match anchor disconnected: "+goal);}
            checks.Add("PASS All authored spawn, flag and hill positions connect to the plaza without jumping.");
            foreach(var goal in ChristmasTownLayout.CommunityPoints)
            {int x=Mathf.RoundToInt(goal.x)+offsetX,z=Mathf.RoundToInt(goal.z)+offsetZ;if(!seen[z*width+x])throw new Exception("Community space disconnected: "+goal);}
            checks.Add("PASS Park and schoolyard are reachable from the plaza with a standing capsule.");
            foreach(var goal in ChristmasTownLayout.SpawnPoints)
            {int x=Mathf.RoundToInt(goal.x)+offsetX,z=Mathf.RoundToInt(goal.z)+offsetZ;if(!seen[z*width+x])throw new Exception("Team/spawn route disconnected: "+goal);}
            checks.Add("PASS Capsule connectivity from plaza to all "+map.interiorCenters.Length+" interiors and four spawn points; no jumps required.");
        }
    }
}
