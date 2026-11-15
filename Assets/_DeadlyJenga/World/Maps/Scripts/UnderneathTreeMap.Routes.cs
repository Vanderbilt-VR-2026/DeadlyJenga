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

        void BuildGiftRoutes()
        {
            var cream = ColoredPaper("Ivory botanical gift wrap", new Color(.88f, .72f, .53f), .33f);
            var navy = ColoredPaper("Midnight snowflake gift wrap", new Color(.16f, .24f, .44f), .36f);
            var green = ColoredPaper("Pine sprig gift wrap", new Color(.15f, .30f, .20f), .34f);
            var red = ColoredPaper("Cranberry berry gift wrap", new Color(.65f, .105f, .155f), .35f);
            var orange = ColoredPaper("Ochre gift wrap", new Color(.78f, .42f, .14f), .35f);
            var ivory = ColoredPaper("Ivory ribbon", new Color(.89f, .79f, .63f), .61f);
            var gold = ColoredPaper("Amber ribbon", new Color(.80f, .48f, .19f), .61f);
            var berry = ColoredPaper("Crimson ribbon", new Color(.64f, .06f, .12f), .61f);

            UnderneathTreeGiftPanel Parcel(string label, float x, float z, float width, float height,
                float depth, float yaw, Material wrap, Material ribbon, UnderneathTreeGiftPanel on = null)
            {
                var floor = new Vector3(x, 0f, z);
                floor.y = on
                    ? transform.InverseTransformPoint(on.transform.position).y +
                      on.GetComponent<BoxCollider>().size.y * .5f + .012f
                    : GiftFloor(floor) + .012f;
                var lid = BoxGift(label, floor + Vector3.up * (height * .5f), new Vector3(width, height, depth),
                    Quaternion.Euler(0f, yaw, 0f), wrap, ribbon, false);
                if (on)
                    giftBearings.Add(new GiftBearing(GiftBase(lid), on, null));
                return lid;
            }

            Parcel("Sofa Approach / Navy Step", -105f, -149f, 5.8f, 1.25f, 4f, 21f, navy, ivory);
            Parcel("Sofa Approach / Cream Step", -108.3f, -144.7f, 5.4f, 2.65f, 5.2f, -9f, cream, berry);
            Parcel("Sofa Approach / Green Climb", -111.6f, -139.7f, 5.1f, 4.75f, 5.1f, 12f, green, gold);

            var back = Parcel("Tree-side Stack / Ochre Base", -101f, -24f, 9.8f, 2.05f, 6.5f, -5f, orange, ivory);
            Parcel("Tree-side Stack / Green Perch", -102f, -24.8f, 4.4f, 2.0f, 4.2f, 9f, green, gold, back);
            Parcel("Tree-side Stack / Red Shelf", -97.8f, -20.7f, 5.5f, 1.35f, 3.9f, 22f, red, ivory);

            Parcel("Bridge Junction / Low Step", -18.8f, -48.4f, 4.8f, 1.75f, 4.0f, 18f, green, gold);

            var front = Parcel("Skirt Landing / Green Base", -6.6f, -117.0f, 9.2f, 1.9f, 6.5f, 13f, green, gold);
            var tread = Parcel("Skirt Landing / Navy Tread", -7.3f, -115.9f, 7.6f, 1.4f, 5.5f, 5f, navy, ivory, front);
            Parcel("Skirt Landing / Red Perch", -8f, -114.8f, 4.4f, 1.8f, 3.8f, -9f, red, gold, tread);
            Parcel("Skirt Landing / Cream Catch", -.1f, -121.7f, 8.8f, 1.3f, 5.6f, 19f, cream, berry);

            // Across the rug, the navy parcel has a real bearing gift at each end.
            var left = new Vector3(7.2f, 0f, -76.6f);
            var right = new Vector3(17.7f, 0f, -55.5f);
            var axis = (right - left).normalized;
            var across = Vector3.Cross(Vector3.up, axis);
            float yaw = Mathf.Atan2(axis.x, axis.z) * Mathf.Rad2Deg;

            UnderneathTreeGiftPanel BridgeGift(string label, Vector3 pos, float width, float topHeight,
                float depth, float turn, Material wrap, Material ribbon)
            {
                float height = topHeight - GiftFloor(pos) - .012f;
                return Parcel("Gift Bridge / " + label, pos.x, pos.z, width, height, depth, yaw + turn, wrap, ribbon);
            }

            const float seat = 5.96f;
            var leftLid = BridgeGift("Pine Bearing", left, 6.4f, seat, 6.9f, 0f, green, gold);
            var rightLid = BridgeGift("Cranberry Bearing", right, 7.0f, seat, 7.4f, 0f, red, ivory);
            var midpoint = (left + right) * .5f;
            const float spanHeight = 1.70f;
            var span = BoxGift("Gift Bridge / Navy Span",
                midpoint + Vector3.up * (seat + .014f + spanHeight * .5f),
                new Vector3(6.0f, spanHeight, Vector3.Distance(left, right) + 4.8f),
                Quaternion.Euler(0f, yaw, 0f), navy, ivory, false);
            giftBearings.Add(new GiftBearing(GiftBase(span), leftLid, rightLid));

            var leftShelf = left + across * 6.3f - axis * 2.2f;
            var leftStep = leftShelf - axis * 4.1f + across * 1.4f;
            var leftEntry = leftStep - axis * 4.9f + across * .9f;
            BridgeGift("Cream Landing", leftEntry, 6.5f, 1.75f, 5.8f, -13f, cream, berry);
            BridgeGift("Red Tread", leftStep, 5.7f, 3.15f, 5.6f, 7f, red, gold);
            BridgeGift("Pine Step", leftShelf, 5.1f, 4.55f, 5.2f, -4f, green, ivory);

            var rightShelf = right + across * 7.05f + axis * 1.2f;
            var rightStep = rightShelf + axis * 4.6f + across * .9f;
            var rightEntry = rightStep + axis * 4.7f + across * 1.5f;
            BridgeGift("Ochre Entry", rightEntry, 6.4f, 1.7f, 5.2f, 11f, orange, ivory);
            BridgeGift("Green Tread", rightStep, 6.1f, 3.05f, 5.7f, -9f, green, gold);
            var shelf = BridgeGift("Red Shelf", rightShelf, 6.2f, 4.45f, 5.2f, 1f, red, ivory);
            var perch = rightShelf + across * .75f;
            Parcel("Gift Bridge / Gold Lookout", perch.x, perch.z, 4.4f, 1.55f, 3.6f, yaw - 17f,
                orange, berry, shelf);

            var trunk = Child(built, "Fir trunk collision", new Vector3(-37f, 17f, -35.7f), Quaternion.identity);
            trunk.gameObject.layer = Layers.Surface;
            var trunkShape = trunk.gameObject.AddComponent<CapsuleCollider>();
            trunkShape.radius = 4.65f;
            trunkShape.height = 34f;
        }

        UnderneathTreeGiftPanel GiftBase(UnderneathTreeGiftPanel lid) =>
            lid.transform.parent.Find("Bottom").GetComponent<UnderneathTreeGiftPanel>();
    }
}
