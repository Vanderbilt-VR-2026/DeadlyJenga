using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public static class ChristmasTownLayout
    {
        public enum BuildingUse { Shop, Home, Cafe, Workshop, Books, PostOffice, Civic, Diner, School, Grocery }
        public const float Width = 96, Depth = 76, HouseWidth = 10, HouseDepth = 10, WallHeight = 3.8f, DoorWidth = 2.6f;
        public readonly struct House
        {
            public readonly string name;
            public readonly Vector3 position;
            public readonly float yaw;
            public readonly Vector3 scale;
            public readonly BuildingUse use;
            public House(string name, float x, float z, float yaw):this(name,x,z,yaw,BuildingUse.Shop) { }
            public House(string name, float x, float z, float yaw, BuildingUse use, float width=1, float depth=1, float height=1)
            { this.name = name; position = new Vector3(x, 0, z); this.yaw = yaw; this.use=use; scale=new Vector3(width,height,depth); }
        }
        public static readonly House[] Houses =
        {
            new House("Cocoa Bakery", -25, 23, 0), new House("Peppermint Post", 0, 25, 0, BuildingUse.PostOffice), new House("Toymaker Workshop", 25, 23, 0, BuildingUse.Workshop),
            new House("Sugarplum Books", -25, -23, 180, BuildingUse.Books), new House("Sugarplum Family Diner", 0, -25, 180, BuildingUse.Diner), new House("Snowbell Elementary", 25, -23, 180, BuildingUse.School),
            new House("Cherry Town Hall", -35, 0, -90, BuildingUse.Civic), new House("Maple Corner Grocery", 35, 0, 90, BuildingUse.Grocery),
            new House("Holly Cottage", -12.5f, 26, 0, BuildingUse.Home, .7f, .8f, .88f),
            new House("Cinnamon Cottage", 12.5f, 26, 0, BuildingUse.Home, .75f, .8f, .95f),
            new House("Snowbell House", -12.5f, -26, 180, BuildingUse.Home, .7f, .8f, .92f),
            new House("Ribbon Cottage", 12.5f, -26, 180, BuildingUse.Home, .75f, .8f, .86f),
            new House("Juniper Cabin", -39, 25, 0, BuildingUse.Home, .7f, .8f, .86f),
            new House("Maple Cottage", 39, 25, 0, BuildingUse.Home, .7f, .8f, .94f),
            new House("Starfall House", -39, -25, 180, BuildingUse.Home, .7f, .8f, .94f),
            new House("Nutmeg Cottage", 39, -25, 180, BuildingUse.Home, .7f, .8f, .86f)
        };
        public static readonly Vector3[] CommunityPoints =
        { new Vector3(-26,.08f,-10), new Vector3(26,.08f,-10) };
        public static readonly Vector3[] ClaimPoints =
        { new Vector3(0,0,7), new Vector3(-40,0,0),new Vector3(40,0,0),new Vector3(-17,0,12),new Vector3(17,0,12),new Vector3(-17,0,-12),new Vector3(17,0,-12) };
        public static readonly Vector3[] SpawnPoints =
        { new Vector3(-42,.08f,0),new Vector3(42,.08f,0),new Vector3(0,.08f,12),new Vector3(0,.08f,-12) };
    }
}
