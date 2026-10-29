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
                    b.Site("Parking",-120,-65,2,name:"Seaward arcade");b.Site("Hotel",-110,35,2,name:"Low court hotel");b.Site("Lantern",-35,90,3,name:"West lantern");
                    b.Site("Office",15,-45,3,name:"Exchange south");b.Site("Office",15,50,4,name:"Exchange north");b.Site("Court",115,35,3,90,name:"Inland court");
                    b.Site("Foundry",115,-70,2,name:"Market hall");b.Site("Parking",-35,-120,1,name:"Beach walk");
                    Loop(b,new[]{0,1,2,4,3,7});Loop(b,new[]{3,4,5,6});b.Link(0,3);b.Link(2,5,24.8f);break;
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
    }
}
