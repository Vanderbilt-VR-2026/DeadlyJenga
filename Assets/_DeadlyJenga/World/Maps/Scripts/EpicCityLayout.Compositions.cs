using UnityEngine;
namespace DeadlyJenga.CastlePrototype
{
    public static partial class EpicCityLayout
    {
        static void Compose(Builder b,int design)
        {
            switch(design)
            {
                case 0:
                    b.Site("Foundry",-115,-95,1,name:"Harbor sheds");b.Site("Parking",-35,-95,2,name:"Quay terraces");b.Site("Court",65,-95,2,180,name:"Lantern Court");
                    b.Site("Sky Tree",-105,5,2,name:"Branching plaza");b.Site("Lantern",0,0,3,name:"Civic lantern");b.Site("Office",105,0,4,name:"Crownline offices");
                    b.Site("Parking",-75,100,2,name:"High street decks");b.Site("Gateway",65,100,4,name:"Gateway headland");
                    Loop(b,new[]{0,1,2,5,7,6,3});b.Link(3,4);b.Link(1,4);b.Link(4,5);b.Link(4,7,24.8f);break;
                case 1:
                    b.HarborExchange();break;
                case 2:
                    b.Site("Cathedral",-65,-110,2,name:"Bowl entry arcade");b.Site("Parking",55,-105,1,name:"Low bowl terraces");
                    b.Site("Parking",120,-15,2,0,4,"East switchback");b.Site("Office",105,90,3,0,12,"High east crown");b.Site("Lantern",0,115,3,0,8,"Bowl lantern");
                    b.Site("Saddle",-105,80,4,0,12,"High west crown");b.Site("Parking",-125,-10,2,0,4,"West switchback");
                    Loop(b,new[]{0,1,2,3,4,5,6});b.Link(2,6,8.8f,6);b.Link(4,6,16.8f);b.Link(1,6,4.8f);break;
                case 3:
                    b.Site("Foundry",-70,-115,1,name:"South bazaar");b.Site("Parking",65,-110,2,name:"Court entry terraces");
                    b.Site("Hotel",125,-10,3,90,name:"Eastern civic gateway");b.Site("Office",85,100,4,name:"Crown north-east");
                    b.Site("Lantern",-25,90,4,name:"Off-center civic beacon");b.Site("Chalice",-120,45,4,name:"West crown segment");b.Site("Parking",-120,-55,2,name:"West arcade");
                    Loop(b,new[]{0,1,2,3,4,5,6});b.Link(0,4,4.8f,6);b.Link(1,5,8.8f);b.Link(2,4,24.8f);break;
                case 4:
                    b.Site("Foundry",-120,-85,2,name:"Dry-dock foundry");b.Site("Parking",-30,-95,1,name:"Lower quay");b.Site("Waterworks",65,-100,3,name:"Harbor lantern");
                    b.Site("Relay",-110,15,3,name:"West shipping offices");b.Site("Parking",-5,10,2,name:"Cross-dock interchange");b.Site("Office",105,5,3,name:"East shipping offices");
                    b.Site("Foundry",-100,110,1,name:"Upper shipyard");b.Site("Office",5,110,4,name:"High loom anchor");b.Site("Parking",110,110,2,name:"North quay");
                    Loop(b,new[]{0,1,2,5,8,7,6,3});b.Link(3,4,12.8f);b.Link(4,5,12.8f);b.Link(1,4);b.Link(4,7,24.8f);break;
                case 5:
                    b.Site("Waterworks",-100,-105,2,name:"Ravine mouth lantern");b.Site("Parking",-115,-15,2,0,4,"Low west terraces");b.Site("Skygarden",-100,100,3,0,6,"West stepping homes");
                    b.Site("Office",100,-100,4,0,4,"Eastern office ridge");b.Site("Kissing Towers",110,5,4,90,8,"Ravine compression gateway");b.Site("Lantern",100,115,4,0,8,"High ravine beacon");
                    b.Site("Foundry",0,-110,1,name:"Low mouth arcade");b.Site("Parking",0,110,1,0,4,"Inland saddle");
                    Loop(b,new[]{0,1,2,7,5,4,3,6});b.Link(1,4,16.8f,6);b.Link(2,5,36.8f);b.Link(0,6,4.8f);break;
                case 6:
                    b.Site("Foundry",-110,-105,1,name:"Canopy market");b.Site("Court",0,-105,2,180,name:"Daylight court");b.Site("Parking",110,-105,1,name:"Sunlit entry terraces");
                    b.Site("Sky Tree",-110,0,2,name:"West branching crown");b.Site("Sky Tree",0,0,3,60,name:"Central branching crown");b.Site("Sky Tree",110,0,2,120,name:"East branching crown");
                    b.Site("Parking",-110,110,2,name:"Shaded upper arcade");b.Site("Office",0,110,4,name:"Canopy skyline marker");b.Site("Foundry",110,110,2,name:"Maker hall");
                    Loop(b,new[]{0,1,2,5,8,7,6,3});b.Link(3,4,16.8f);b.Link(4,5,16.8f);b.Link(1,4);b.Link(4,7,24.8f);break;
                case 7:
                    b.Site("Lantern",-130,-65,4,name:"Boulevard civic tower");b.Site("Book Wing",-45,-65,2,name:"Western book court");b.Site("Book Wing",50,-65,2,180,name:"Eastern book court");
                    b.Site("Zipper",140,35,4,name:"Zipper skyline");b.Site("Foundry",55,45,1,name:"Maker hall");b.Site("Book Wing",-40,45,2,name:"Northern book court");b.Site("Parking",-130,50,2,name:"Parallel escape arcade");
                    Loop(b,new[]{0,1,2,3,4,5,6});b.Link(1,5);b.Link(2,4);b.Link(0,6,24.8f);break;
                case 8:
                    b.Site("Gateway",-105,65,4,name:"Business heart");b.Site("Spiral",0,115,3,name:"Civic heart");b.Site("Hotel",110,60,2,90,name:"Lantern Court heart");
                    b.Site("Parking",115,-70,2,name:"Harbor terraces");b.Site("Foundry",20,-110,1,name:"Waterfront heart");b.Site("Parking",-95,-70,1,name:"Market entry");
                    b.Site("Drop Stop",0,0,4,name:"Exchange catch tower");
                    Loop(b,new[]{0,1,2,3,4,5});b.Link(0,6,24.8f);b.Link(2,6,16.8f);b.Link(4,6,4.8f);b.Link(5,6);b.Link(1,6,24.8f);break;
                case 9:
                    b.Site("Foundry",-140,-85,1,name:"Seaside low market");b.Site("Parking",-55,-85,2,name:"First spine terrace");
                    b.Site("Relay",35,-65,3,0,4,"Middle crown");b.Site("Lantern",125,-35,4,0,10,"Inland civic summit");
                    b.Site("Parking",125,70,2,0,8,"Headland shoulder");b.Site("Office",35,75,3,0,6,"Secondary skyline");b.Site("Parking",-55,70,2,0,2,"Lower cross-grain arcade");b.Site("Waterworks",-140,20,2,name:"Waterfront landmark");
                    Loop(b,new[]{0,1,2,3,4,5,6,7});b.Link(1,6,8.8f);b.Link(2,5,16.8f);b.Link(3,5,32.8f);break;
            }
        }
        static void Loop(Builder b,int[] order)
        {for(int i=0;i<order.Length;i++)b.Link(order[i],order[(i+1)%order.Length]);}

        sealed partial class Builder
        {
            public void HarborExchange()
            {
                ExchangePavilion("Seaward arcade",-36,-20,false);
                ExchangePavilion("Low court hotel",-36,20,true);
                ExchangePavilion("Exchange south",0,-20,true);
                ExchangePavilion("Exchange north",0,20,false);
                ExchangePavilion("Inland court",36,20,true);
                ExchangePavilion("Market hall",36,-20,false);
                ExchangeLoop(new[]{0,1,3,2});ExchangeLoop(new[]{2,3,4,5});
                ExchangeCrossing(0,1);ExchangeCrossing(2,3);
            }
            void ExchangePavilion(string name,float x,float z,bool enclosed)
            {
                district=plan.sites.Count;
                var site=new EpicCitySite{kind="Exchange pavilion",name=name,position=new Vector3(x,.35f,z),width=18,depth=16,tiers=1};
                plan.sites.Add(site);int first=plan.modules.Count;
                Base(site,18,16);
                var bearing=plan.modules[plan.modules.Count-1];
                foreach(int side in new[]{-1,1})
                    Part(bearing,new Vector3(0,0,side*8.75f),new Vector3(5,.8f,.5f),3);
                var hall=Cassette(site.position+Vector3.up*.8f,18,16,1,0,name+" inhabited section",enclosed);
                if(enclosed)
                    foreach(int side in new[]{-1,1})
                        Part(hall,new Vector3(side*5.5f,2,6.8f),new Vector3(6,4,1.2f),3);
                site.members.AddRange(plan.modules.GetRange(first,plan.modules.Count-first));
                site.bounds=MayhemStructureLayout.Bounds(site.members);
                site.ports.Add(site.position+Vector3.up*4.8f);
                // Ground arrivals stay outside the bearing raft and its sloped entries.
                plan.spawnPoints.Add(site.position+new Vector3(0,.1f,-16));
            }
            void ExchangeLoop(int[] order)
            {
                for(int n=0;n<order.Length;n++)
                {
                    int a=order[n],b=order[(n+1)%order.Length];
                    plan.routes.Add(new EpicCityRoute{name="Ground street: "+plan.sites[a].name+" to "+plan.sites[b].name,from=a,to=b,a=plan.sites[a].position,b=plan.sites[b].position});
                }
            }
            void ExchangeCrossing(int from,int to)
            {
                var a=plan.sites[from];var b=plan.sites[to];district=from;
                var first=a.position+new Vector3(0,4.8f,6.8f);
                var last=b.position+new Vector3(0,4.8f,-6.8f);
                Bridge(first,last,4,"Optional exchange upper crossing");
                plan.routes.Add(new EpicCityRoute{name="Upper crossing: "+a.name+" to "+b.name,from=from,to=to,a=first,b=last});
            }
        }
    }
}
