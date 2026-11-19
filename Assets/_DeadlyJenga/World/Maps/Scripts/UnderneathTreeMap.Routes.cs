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

        // The course runs along the room: right is east, back is north (towards the tree wall).
        static readonly Vector3 CourseRight = Vector3.right;
        static readonly Vector3 CourseBack = Vector3.forward;
        static Vector3 CoursePoint(float u, float v, float height = 0f) =>
            new Vector3(-62f, height, -80f) + CourseRight * u + CourseBack * v;

        readonly Dictionary<string, UnderneathTreeGiftPanel> giftLids =
            new Dictionary<string, UnderneathTreeGiftPanel>();

        // Builds every present from the exported layout table.
        void BuildGiftRoutes()
        {
            giftLids.Clear();
            var wraps = new Dictionary<string, Material>
            {
                { "cream", ColoredPaper("Ivory snowflake gift wrap", new Color(.96f, .84f, .64f), .38f) },
                { "navy", ColoredPaper("Sapphire snowflake gift wrap", new Color(.035f, .16f, .65f), .42f) },
                { "green", ColoredPaper("Emerald snowflake gift wrap", new Color(.035f, .40f, .20f), .39f) },
                { "red", ColoredPaper("Ruby snowflake gift wrap", new Color(.80f, .025f, .065f), .4f) }
            };
            var ribbons = new Dictionary<string, Material>
            {
                { "gold", ColoredPaper("Gold ribbon", new Color(.95f, .63f, .18f), .67f) },
                { "ivory", ColoredPaper("Ivory ribbon", new Color(.98f, .89f, .72f), .62f) },
                { "berry", ColoredPaper("Crimson ribbon", new Color(.8f, .025f, .06f), .63f) }
            };

            foreach (var gift in ApprovedGifts)
            {
                // A hair of air under each gift keeps stacked shells from touching before anything breaks.
                const float air = .02f;
                float rug = GiftFloor(CoursePoint(gift.x, gift.z));
                float bottom = rug + gift.y + air;
                float height = gift.h - air;
                UnderneathTreeGiftPanel lid;
                if (gift.round)
                    lid = RoundParcel(gift.name, CoursePoint(gift.x, gift.z, bottom), gift.w * .5f, height,
                        wraps[gift.wrap], ribbons[gift.ribbon]);
                else
                    lid = BoxGift(gift.name, CoursePoint(gift.x, gift.z, bottom + height * .5f),
                        new Vector3(gift.w, height, gift.d), Quaternion.Euler(0f, gift.turn, 0f),
                        wraps[gift.wrap], ribbons[gift.ribbon], !gift.rail);
                giftLids.Add(gift.name, lid);
                if (gift.on != null)
                    giftBearings.Add(new GiftBearing(GiftBase(lid), giftLids[gift.on],
                        gift.on2 != null ? giftLids[gift.on2] : null));
            }

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
