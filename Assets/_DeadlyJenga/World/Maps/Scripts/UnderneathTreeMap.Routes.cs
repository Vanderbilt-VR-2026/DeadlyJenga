using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class UnderneathTreeMap
    {
        readonly List<GiftBearing> giftBearings = new List<GiftBearing>();

        readonly struct GiftBearing
        {
            readonly UnderneathTreeGiftPanel load;
            readonly UnderneathTreeGiftPanel first;
            readonly UnderneathTreeGiftPanel second;

            public GiftBearing(UnderneathTreeGiftPanel load, UnderneathTreeGiftPanel first,
                UnderneathTreeGiftPanel second)
            {
                this.load = load;
                this.first = first;
                this.second = second;
            }

            public void Check()
            {
                if (load.IsLoose || !Lost(first) && (!second || !Lost(second)))
                    return;
                load.Push(Vector3.down * 1.5f, null);
            }

            static bool Lost(UnderneathTreeGiftPanel panel) =>
                !panel || !panel.gameObject.activeInHierarchy || panel.IsLoose;
        }

        static readonly Vector3 CourseRight = new Vector3(.66f, 0f, .751266f);
        static readonly Vector3 CourseBack = new Vector3(-.751266f, 0f, .66f);
        static Vector3 CoursePoint(float u, float v, float height = 0f) =>
            new Vector3(-62f, height, -80f) + CourseRight * u + CourseBack * v;

        void BuildGiftRoutes()
        {
            var cream = ColoredPaper("Ivory snowflake gift wrap", new Color(.96f, .84f, .64f), .38f);
            var navy = ColoredPaper("Sapphire snowflake gift wrap", new Color(.035f, .16f, .65f), .42f);
            var green = ColoredPaper("Emerald snowflake gift wrap", new Color(.035f, .40f, .20f), .39f);
            var red = ColoredPaper("Ruby snowflake gift wrap", new Color(.80f, .025f, .065f), .4f);
            var gold = ColoredPaper("Gold ribbon", new Color(.95f, .63f, .18f), .67f);
            var ivory = ColoredPaper("Ivory ribbon", new Color(.98f, .89f, .72f), .62f);
            var berry = ColoredPaper("Crimson ribbon", new Color(.8f, .025f, .06f), .63f);
            float yaw = Mathf.Atan2(CourseBack.x, CourseBack.z) * Mathf.Rad2Deg;

            UnderneathTreeGiftPanel Parcel(string name, float u, float v, float w, float h, float d,
                Material wrap, Material ribbon, float turn = 0f, UnderneathTreeGiftPanel on = null)
            {
                var pos = CoursePoint(u, v);
                float floor = on ? transform.InverseTransformPoint(on.transform.position).y +
                    on.GetComponent<BoxCollider>().size.y * .5f : GiftFloor(pos);
                var lid = BoxGift(name, pos + Vector3.up * (floor + .012f + h * .5f),
                    new Vector3(w, h, d), Quaternion.Euler(0f, yaw + turn, 0f), wrap, ribbon);
                if (on) giftBearings.Add(new GiftBearing(GiftBase(lid), on, null));
                return lid;
            }

            // Two asymmetric shoulders share their lower terraces with the railway.
            var left = Parcel("Gift Bridge / Pine Bearing", -28f, -9f, 15f, 16f, 18f, green, gold);
            var right = Parcel("Gift Bridge / Cranberry Bearing", 27f, -9f, 15f, 16f, 18f, red, ivory);
            float seat = Mathf.Max(transform.InverseTransformPoint(left.transform.position).y + left.GetComponent<BoxCollider>().size.y * .5f,
                transform.InverseTransformPoint(right.transform.position).y + right.GetComponent<BoxCollider>().size.y * .5f);
            var span = BoxGift("Gift Bridge / Navy Span", CoursePoint(-.5f, -9f, seat + 1.6f),
                new Vector3(69f, 3.2f, 10f), Quaternion.Euler(0f, yaw, 0f), navy, ivory);
            giftBearings.Add(new GiftBearing(GiftBase(span), left, right));

            var l1 = Parcel("Left Summit / Broad Cream", -29f, 4f, 20f, 9f, 16f, cream, berry, -3f);
            var l2 = Parcel("Left Summit / Ruby Shoulder", -30f, 4f, 16f, 8f, 13f, red, gold, 4f, l1);
            var l3 = Parcel("Left Summit / Emerald Shelf", -32f, 5f, 12f, 6f, 10f, green, ivory, -5f, l2);
            var crown = RoundParcel("Left Summit / Ivory Crown", CoursePoint(-33f, 6f,
                transform.InverseTransformPoint(l3.transform.position).y + l3.GetComponent<BoxCollider>().size.y * .5f + .012f),
                4.7f, 8f, cream, gold);
            giftBearings.Add(new GiftBearing(GiftBase(crown), l3, null));
            Parcel("Left Summit / Side Foothold", -20f, 4f, 7f, 4f, 10f, navy, ivory, 5f, l1);
            Parcel("Left Summit / Rear Entry", -25f, 16f, 13f, 3f, 8f, green, gold, -6f);
            Parcel("Left Summit / Rear Step", -28f, 12f, 10f, 6f, 8f, red, ivory, 4f);
            Parcel("Left Summit / Front Entry", -23f, -22f, 15f, 3f, 8f, cream, berry, 3f);
            Parcel("Left Summit / Front Step", -27f, -18f, 11f, 7f, 8f, navy, ivory, -4f);
            Parcel("Left Summit / Front Shelf", -31f, -15f, 8f, 11f, 7f, red, gold);

            var r1 = Parcel("Right Summit / Sapphire Base", 29f, 6f, 19f, 8f, 17f, navy, ivory, 2f);
            var r2 = Parcel("Right Summit / Cream Shelf", 31f, 7f, 14f, 6f, 12f, cream, berry, -5f, r1);
            var r3 = Parcel("Right Summit / Ruby Perch", 32f, 8f, 10f, 7f, 9f, red, gold, 6f, r2);
            Parcel("Right Summit / Sapphire Crown", 34f, 9f, 8f, 7f, 7f, navy, berry, -5f, r3);
            Parcel("Right Summit / Emerald Foothold", 21f, 5f, 7f, 4f, 9f, green, gold, 4f, r1);
            Parcel("Right Summit / Rear Entry", 25f, 19f, 12f, 3f, 8f, cream, berry, 8f);
            Parcel("Right Summit / Rear Step", 29f, 15f, 10f, 5f, 8f, green, ivory, -4f);
            Parcel("Right Summit / Front Entry", 21f, -23f, 14f, 2.5f, 8f, red, gold, -5f);
            Parcel("Right Summit / Front Step", 26f, -19f, 12f, 6f, 8f, green, ivory, 3f);
            Parcel("Right Summit / Front Shelf", 30f, -15f, 8f, 10f, 7f, cream, berry);

            // Broad side terraces give each summit a second, less direct climbing route.
            var ls = Parcel("Left Terrace / Emerald Foundation", -47f, 14f, 9f, 8f, 10f, green, ivory, -4f);
            var ls2 = Parcel("Left Terrace / Ruby Shelf", -47f, 14f, 8f, 5f, 9f, red, gold, 6f, ls);
            Parcel("Left Terrace / Cream Perch", -47f, 14f, 7f, 5f, 7f, cream, berry, -3f, ls2);
            Parcel("Left Terrace / Navy Toe", -52f, 25f, 8f, 3f, 8f, navy, ivory, 7f);
            Parcel("Left Terrace / Cream Stair", -43f, 25f, 8f, 5.5f, 8f, cream, gold, -3f);
            Parcel("Left Terrace / Ruby Rear", -34f, 25f, 8f, 8f, 8f, red, gold, 4f);
            var rs = Parcel("Right Terrace / Ruby Foundation", 52f, 15f, 9f, 7f, 10f, red, gold, 4f);
            var rs2 = Parcel("Right Terrace / Emerald Shelf", 52f, 15f, 8f, 5f, 9f, green, ivory, -5f, rs);
            Parcel("Right Terrace / Ivory Perch", 52f, 15f, 7f, 5f, 7f, cream, berry, 5f, rs2);
            Parcel("Right Terrace / Navy Toe", 51f, 27f, 8f, 2.5f, 8f, navy, gold, -4f);
            Parcel("Right Terrace / Cream Stair", 42f, 27f, 8f, 4.5f, 8f, cream, berry, 6f);
            Parcel("Right Terrace / Green Rear", 33f, 27f, 8f, 7f, 8f, green, ivory, -3f);
            Parcel("Bridge Approach / Left Low", -15f, -15f, 9f, 3f, 9f, red, gold, 5f);
            Parcel("Bridge Approach / Left Mid", -19f, -10f, 7f, 7f, 8f, cream, berry, -4f);
            Parcel("Bridge Approach / Right Low", 14f, -16f, 9f, 3f, 8f, green, ivory, -6f);
            Parcel("Bridge Approach / Right Mid", 18f, -10f, 7f, 7f, 8f, navy, gold, 4f);

            // The train passes between these two piers, below a real breakaway gift lintel.
            var outer = Parcel("Train Tunnel / Outer Pier", -50f, -7f, 6f, 13f, 12f, cream, gold);
            var inner = Parcel("Train Tunnel / Inner Pier", -35f, -7f, 5f, 13f, 12f, red, gold);
            var tunnel = BoxGift("Train Tunnel / Gold Lintel", CoursePoint(-42.5f, -7f, 15f),
                new Vector3(21f, 2.8f, 13f), Quaternion.Euler(0f, yaw, 0f), cream, gold);
            giftBearings.Add(new GiftBearing(GiftBase(tunnel), outer, inner));
            Parcel("Tunnel / Green Shoulder", -50f, 4f, 8f, 6f, 9f, green, ivory);
            Parcel("Tunnel / Navy Approach", -52f, -19f, 10f, 3f, 9f, navy, ivory, 6f);
            Parcel("Tunnel / Cream Step", -51f, -13f, 8f, 6f, 8f, cream, berry);
            Parcel("Tunnel / Ruby Cap", -49f, -6f, 6f, 4f, 7f, red, gold, 0f, tunnel);
            Parcel("Outer Right / Ruby Landing", 49f, -7f, 9f, 7f, 12f, red, gold);
            Parcel("Outer Right / Cream Entry", 50f, -20f, 11f, 3f, 8f, cream, berry, -7f);
            Parcel("Outer Right / Green Step", 50f, -14f, 9f, 5f, 7f, green, ivory, 4f);
            Parcel("Courtyard / Navy Hop", -5f, -20f, 8f, 2f, 6f, navy, ivory, 8f);
            Parcel("Courtyard / Green Hop", 7f, 2f, 7f, 3f, 7f, green, gold, -9f);
            Parcel("Courtyard / Ruby Hop", -9f, 7f, 8f, 4f, 6f, red, gold, 4f);
            Parcel("Entrance / Cream Catch", 0f, -39f, 13f, 2f, 8f, cream, berry, -5f);
            Parcel("Entrance / Green Step", 7f, -35f, 10f, 3.5f, 7f, green, ivory, 3f);
            var trunk = Child(built, "Fir trunk collision", new Vector3(-37f, 17f, -35.7f), Quaternion.identity);
            trunk.gameObject.layer = Layers.Surface;
            var shape = trunk.gameObject.AddComponent<CapsuleCollider>();
            shape.radius = 4.65f;
            shape.height = 34f;
        }

        UnderneathTreeGiftPanel GiftBase(UnderneathTreeGiftPanel lid) =>
            lid.transform.parent.Find("Bottom").GetComponent<UnderneathTreeGiftPanel>();
    }
}
