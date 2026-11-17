// Generated from the layout lab (ArtDirection/UnderneathTree-PictureMatch-20261116/layout-lab).
// Change the layout there and export again; do not edit the numbers by hand.
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class UnderneathTreeMap
    {
        // Rug size and how far its centre sits behind the train loop, in course units.
        const float ApprovedRugRadius = 65f, ApprovedRugBack = 17f;
        const float ApprovedRailHeight = 6.5f;

        readonly struct GiftSpec
        {
            public readonly string name, wrap, ribbon, on, on2;
            public readonly float x, z, y, w, h, d, turn;
            public readonly bool round, rail;

            public GiftSpec(string name, float x, float z, float y, float w, float h, float d, float turn,
                string wrap, string ribbon, string on = null, string on2 = null, bool round = false, bool rail = false)
            {
                this.name = name; this.x = x; this.z = z; this.y = y; this.w = w; this.h = h; this.d = d;
                this.turn = turn; this.wrap = wrap; this.ribbon = ribbon; this.on = on; this.on2 = on2;
                this.round = round; this.rail = rail;
            }
        }

        // x = right, z = back, y = height of the gift's underside above the rug.
        static readonly GiftSpec[] ApprovedGifts =
        {
            new GiftSpec("Rail Gift 1", 41.101f, 6.41f, 0f, 7.6f, 6.15f, 10.357f, -7.099f, "cream", "berry", rail: true),
            new GiftSpec("Rail Gift 2", 35.66f, 18.72f, 0f, 7.6f, 6.15f, 12.478f, -37.894f, "green", "gold", rail: true),
            new GiftSpec("Rail Gift 3", 23.091f, 28.419f, 0f, 7.6f, 6.15f, 13.661f, -69.517f, "red", "ivory", rail: true),
            new GiftSpec("Rail Gift 4", 6.668f, 30.465f, 0f, 7.6f, 6.15f, 14.983f, -90.313f, "cream", "gold", rail: true),
            new GiftSpec("Rail Gift 5", -9.329f, 28.897f, 0f, 7.6f, 6.15f, 13.562f, -102.854f, "navy", "gold", rail: true),
            new GiftSpec("Rail Gift 6", -22.466f, 23.651f, 0f, 7.6f, 6.15f, 11.947f, -121.106f, "cream", "gold", rail: true),
            new GiftSpec("Rail Gift 7", -32.521f, 15.374f, 0f, 7.6f, 6.15f, 11.112f, -137.541f, "red", "ivory", rail: true),
            new GiftSpec("Rail Gift 8", -39.323f, 5.317f, 0f, 7.6f, 6.15f, 8.934f, -155.952f, "green", "gold", rail: true),
            new GiftSpec("Rail Gift 9", -41.101f, -5.317f, 0f, 7.6f, 6.15f, 7.828f, 171.481f, "cream", "gold", rail: true),
            new GiftSpec("Rail Gift 10", -35.66f, -15.374f, 0f, 7.6f, 6.15f, 10.332f, 135.195f, "green", "gold", rail: true),
            new GiftSpec("Rail Gift 11", -23.091f, -23.651f, 0f, 7.6f, 6.15f, 14.875f, 114.577f, "red", "gold", rail: true),
            new GiftSpec("Rail Gift 12", -6.668f, -28.888f, 0f, 7.6f, 6.15f, 15.621f, 101.133f, "cream", "berry", rail: true),
            new GiftSpec("Rail Gift 13", 9.329f, -29.959f, 0f, 7.6f, 6.15f, 12.915f, 85.025f, "navy", "ivory", rail: true),
            new GiftSpec("Rail Gift 14", 22.466f, -26.238f, 0f, 7.6f, 6.15f, 11.221f, 62.052f, "cream", "gold", rail: true),
            new GiftSpec("Rail Gift 15", 32.521f, -17.961f, 0f, 7.6f, 6.15f, 11.48f, 40.021f, "red", "gold", rail: true),
            new GiftSpec("Rail Gift 16", 39.323f, -6.388f, 0f, 7.6f, 6.15f, 11.125f, 20.516f, "green", "gold", rail: true),
            new GiftSpec("Gift Bridge / Pine Bearing", -21.4f, -3f, 0f, 6.5f, 8.6f, 8f, 0f, "green", "gold"),
            new GiftSpec("Gift Bridge / Cranberry Bearing", 21.5f, -3f, 0f, 6.5f, 8.6f, 8f, 0f, "red", "ivory"),
            new GiftSpec("Gift Bridge / Navy Span", 1.4f, -3f, 8.6f, 52f, 3.2f, 9.6f, 0f, "navy", "ivory", on: "Gift Bridge / Pine Bearing", on2: "Gift Bridge / Cranberry Bearing"),
            new GiftSpec("Left Tower / Outer Pier 1 / Base", -49.101f, -9f, 0f, 6.4f, 7.2f, 8f, 0.306f, "cream", "gold"),
            new GiftSpec("Left Tower / Outer Pier 1 / Cap", -49.101f, -9f, 7.2f, 6f, 6.2f, 7.6f, 1.961f, "red", "gold", on: "Left Tower / Outer Pier 1 / Base"),
            new GiftSpec("Left Tower / Inner Pier 1 / Base", -29.051f, -9f, 0f, 5.4f, 6.4f, 8f, 2.685f, "green", "gold"),
            new GiftSpec("Left Tower / Inner Pier 1 / Cap", -29.051f, -9f, 6.4f, 5.2f, 7f, 7.6f, 0.463f, "cream", "berry", on: "Left Tower / Inner Pier 1 / Base"),
            new GiftSpec("Train Tunnel / Gold Lintel", -39.076f, -9f, 13.4f, 25.65f, 3f, 8.2f, 0f, "cream", "gold", on: "Left Tower / Outer Pier 1 / Cap", on2: "Left Tower / Inner Pier 1 / Cap"),
            new GiftSpec("Left Tower / Outer Pier 2 / Base", -49.267f, -0.5f, 0f, 6.4f, 7.2f, 8f, 2.858f, "green", "gold"),
            new GiftSpec("Left Tower / Outer Pier 2 / Cap", -49.267f, -0.5f, 7.2f, 6f, 6.2f, 7.6f, 0.34f, "cream", "gold", on: "Left Tower / Outer Pier 2 / Base"),
            new GiftSpec("Left Tower / Inner Pier 2 / Base", -31.172f, -0.5f, 0f, 5.4f, 6.4f, 8f, -1.262f, "cream", "gold"),
            new GiftSpec("Left Tower / Inner Pier 2 / Cap", -31.172f, -0.5f, 6.4f, 5.2f, 7f, 7.6f, 0.244f, "red", "ivory", on: "Left Tower / Inner Pier 2 / Base"),
            new GiftSpec("Left Tower / Lintel 2", -40.22f, -0.5f, 13.4f, 23.695f, 3f, 8.2f, 0f, "navy", "berry", on: "Left Tower / Outer Pier 2 / Cap", on2: "Left Tower / Inner Pier 2 / Cap"),
            new GiftSpec("Left Tower / Outer Pier 3 / Base", -47.872f, 8f, 0f, 6.4f, 7.2f, 8f, 1.897f, "red", "gold"),
            new GiftSpec("Left Tower / Outer Pier 3 / Cap", -47.872f, 8f, 7.2f, 6f, 6.2f, 7.6f, -2.382f, "green", "gold", on: "Left Tower / Outer Pier 3 / Base"),
            new GiftSpec("Left Tower / Inner Pier 3 / Base", -26.812f, 8f, 0f, 5.4f, 6.4f, 8f, -0.766f, "navy", "berry"),
            new GiftSpec("Left Tower / Inner Pier 3 / Cap", -26.812f, 8f, 6.4f, 5.2f, 7f, 7.6f, 1.273f, "cream", "gold", on: "Left Tower / Inner Pier 3 / Base"),
            new GiftSpec("Left Tower / Lintel 3", -37.342f, 8f, 13.4f, 26.661f, 3f, 8.2f, 0f, "red", "gold", on: "Left Tower / Outer Pier 3 / Cap", on2: "Left Tower / Inner Pier 3 / Cap"),
            new GiftSpec("Left Summit / Broad Cream", -42.5f, -2.5f, 16.4f, 13f, 4.4f, 8f, -0.022f, "cream", "berry", on: "Left Tower / Lintel 2"),
            new GiftSpec("Left Summit / Ruby Shoulder", -41.9f, -1.7f, 20.8f, 9.6f, 3.6f, 9f, 1.663f, "red", "ivory", on: "Left Summit / Broad Cream"),
            new GiftSpec("Left Summit / Wizard Cap", -41.9f, -1.7f, 24.4f, 15.8f, 2.1f, 15.8f, 0f, "red", "gold", on: "Left Summit / Ruby Shoulder"),
            new GiftSpec("Left Summit / Emerald Front", -44.6f, -10.3f, 16.4f, 8.4f, 4.2f, 5f, 4f, "green", "gold", on: "Train Tunnel / Gold Lintel"),
            new GiftSpec("Left Summit / Ruby Front", -35.4f, -10.2f, 16.4f, 5f, 2.6f, 5f, -6f, "red", "gold", on: "Train Tunnel / Gold Lintel"),
            new GiftSpec("Left Summit / Sapphire Rear", -43f, 8.3f, 16.4f, 10f, 3.8f, 6f, -3f, "navy", "ivory", on: "Left Tower / Lintel 3"),
            new GiftSpec("Left Summit / Cream Rear", -43.6f, 8.3f, 20.2f, 6f, 3f, 5f, 5f, "cream", "berry", on: "Left Summit / Sapphire Rear"),
            new GiftSpec("Left Summit / Green Ledge", -33.2f, 0.4f, 16.4f, 4.2f, 3f, 5.6f, 3f, "green", "ivory", on: "Left Tower / Lintel 2"),
            new GiftSpec("Left Stair / Low Navy", -19f, -14.5f, 0f, 7f, 3.2f, 6f, 8f, "navy", "ivory"),
            new GiftSpec("Left Stair / Mid Cream", -19.6f, 7.6f, 0f, 7f, 6f, 7f, -5f, "cream", "berry"),
            new GiftSpec("Left Stair / High Ruby", -19.2f, 15.6f, 0f, 6.5f, 10f, 6f, 4f, "red", "gold"),
            new GiftSpec("Bridge Terrace / Left Ruby", -20f, -3f, 11.8f, 7f, 3.2f, 7.6f, -2f, "red", "gold", on: "Gift Bridge / Navy Span"),
            new GiftSpec("Left Foot / Navy Toe", -33f, -31f, 0f, 6.4f, 3f, 5.4f, 30f, "navy", "gold"),
            new GiftSpec("Left Foot / Ruby Step", -39.5f, -27.5f, 0f, 6f, 6.4f, 5.6f, 24f, "red", "gold"),
            new GiftSpec("Left Foot / Cream Step", -45.2f, -23.2f, 0f, 5.6f, 9.6f, 4.6f, 16f, "cream", "berry"),
            new GiftSpec("Left Foot / Green Side", -56.4f, -5f, 0f, 4f, 5f, 7f, 2f, "green", "ivory"),
            new GiftSpec("Left Foot / Cream Side", -57f, 4.6f, 0f, 4.6f, 8.4f, 7f, -3f, "cream", "gold"),
            new GiftSpec("Left Foot / Ruby Rear", -53f, 19.4f, 0f, 7f, 4.6f, 6f, -22f, "red", "ivory"),
            new GiftSpec("Right Summit / Sapphire Base", 25.6f, 9.8f, 0f, 12f, 8f, 12f, 4.513f, "navy", "ivory"),
            new GiftSpec("Right Summit / Cream Shelf", 26.4f, 10.6f, 8f, 10.6f, 5.4f, 10f, 3.169f, "cream", "gold", on: "Right Summit / Sapphire Base"),
            new GiftSpec("Right Summit / Ruby Perch", 26.6f, 11f, 13.4f, 8.4f, 4f, 8f, 5.766f, "red", "gold", on: "Right Summit / Cream Shelf"),
            new GiftSpec("Right Summit / Sapphire Crown", 26.8f, 11.2f, 17.4f, 6.4f, 4.6f, 6.2f, 1.491f, "navy", "ivory", on: "Right Summit / Ruby Perch"),
            new GiftSpec("Right Stair / Low Green", 21.4f, -13.4f, 0f, 7f, 3f, 6f, -8f, "green", "ivory"),
            new GiftSpec("Right Stair / Cream One", 29.6f, -5f, 0f, 6.4f, 5.6f, 6.4f, 3f, "cream", "gold"),
            new GiftSpec("Right Stair / Cream Two", 15.8f, 9f, 0f, 4.4f, 11.6f, 7f, -3f, "cream", "berry"),
            new GiftSpec("Bridge Terrace / Right Cream", 22.6f, -3f, 11.8f, 8f, 3f, 7.6f, 2f, "cream", "gold", on: "Gift Bridge / Navy Span"),
            new GiftSpec("Right Terrace / Ruby Foundation", 53f, 2f, 0f, 8f, 7.6f, 10f, -1.849f, "red", "ivory"),
            new GiftSpec("Right Terrace / Emerald Shelf", 53f, 2.6f, 7.6f, 7f, 4.6f, 8f, -0.623f, "green", "ivory", on: "Right Terrace / Ruby Foundation"),
            new GiftSpec("Right Terrace / Ivory Perch", 53f, 3f, 12.2f, 5.6f, 3.6f, 6f, -1.346f, "cream", "gold", on: "Right Terrace / Emerald Shelf"),
            new GiftSpec("Right Foot / Navy Toe", 41.5f, -23.5f, 0f, 6f, 2.8f, 5f, -28f, "navy", "gold"),
            new GiftSpec("Right Foot / Cream Step", 51.4f, -11.6f, 0f, 6.6f, 5.2f, 7f, -10f, "cream", "berry"),
            new GiftSpec("Right Foot / Green Step", 54f, 13.6f, 0f, 6f, 9.6f, 7f, 8f, "green", "ivory"),
            new GiftSpec("Right Foot / Ruby Rear", 49.6f, 22.6f, 0f, 7f, 5f, 6f, 24f, "red", "gold"),
            new GiftSpec("Right Foot / Cream Rear", 43f, 27.4f, 0f, 6.4f, 3f, 5.6f, 34f, "cream", "gold"),
            new GiftSpec("Courtyard / Ruby Hop", -12f, -14.6f, 0f, 6.4f, 3.4f, 5.4f, 10f, "red", "gold"),
            new GiftSpec("Courtyard / Green Hop", 6f, -16f, 0f, 7.6f, 2.4f, 5f, -7f, "green", "gold"),
            new GiftSpec("Courtyard / Cream Hop", 13.6f, -9.6f, 0f, 5f, 4.6f, 4.6f, 14f, "cream", "berry"),
            new GiftSpec("Courtyard / Navy Hop", -8f, 9f, 0f, 7f, 3f, 6f, -9f, "navy", "ivory"),
            new GiftSpec("Courtyard / Ruby Rear", 6.6f, 12f, 0f, 6f, 5f, 5.6f, 6f, "red", "ivory"),
            new GiftSpec("Courtyard / Green Rear", -1f, 17.6f, 0f, 6.6f, 2.6f, 5f, -4f, "green", "gold"),
            new GiftSpec("Back Row / Gift 1", -21f, 39f, 0f, 7f, 5.6f, 6f, 6f, "red", "gold"),
            new GiftSpec("Back Row / Gift 2", -13f, 40.6f, 0f, 7.4f, 4f, 6f, -4f, "green", "ivory"),
            new GiftSpec("Back Row / Gift 3", -5f, 41f, 0f, 6.4f, 6.6f, 6f, 3f, "red", "gold"),
            new GiftSpec("Back Row / Gift 4", 2.6f, 41f, 0f, 7f, 4.4f, 6f, -5f, "cream", "berry"),
            new GiftSpec("Back Row / Gift 5", 10.6f, 39.6f, 0f, 6.6f, 5.4f, 6f, 4f, "navy", "gold"),
            new GiftSpec("Back Row / Gift 6", -31.5f, 33.4f, 0f, 6f, 3.6f, 5.6f, 30f, "cream", "berry"),
            new GiftSpec("Back Row / Top Green", -12.6f, 40.6f, 4f, 5f, 3.4f, 4.6f, 8f, "green", "gold", on: "Back Row / Gift 2"),
            new GiftSpec("Back Row / Top Cream", 2.8f, 41f, 4.4f, 4.6f, 3f, 4.4f, -10f, "cream", "berry", on: "Back Row / Gift 4"),
            new GiftSpec("Entrance / Cream Catch", 9f, -40.6f, 0f, 9f, 2.2f, 6f, -6f, "cream", "berry"),
            new GiftSpec("Entrance / Ruby Step", 13.6f, -41f, 2.2f, 6f, 2.2f, 4.6f, 4f, "red", "gold", on: "Entrance / Cream Catch"),
            new GiftSpec("Entrance / Green Step", -0.6f, -43f, 0f, 5.6f, 3f, 4.4f, 12f, "green", "ivory"),
            new GiftSpec("Entrance / Navy Step", -20f, -39f, 0f, 6.6f, 3.4f, 5f, 22f, "navy", "ivory"),
            new GiftSpec("Entrance / Cream Side", 29f, -34.6f, 0f, 6.4f, 4f, 5f, -30f, "cream", "gold"),
        };

        // Centre line of the rails, one point per rung.
        static readonly Vector2[] ApprovedRail =
        {
            new Vector2(41f, 0.001f), new Vector2(41.185f, 1.612f), new Vector2(41.267f, 3.22f), new Vector2(41.24f, 4.82f),
            new Vector2(41.101f, 6.41f), new Vector2(40.846f, 7.988f), new Vector2(40.471f, 9.552f), new Vector2(39.976f, 11.104f),
            new Vector2(39.357f, 12.645f), new Vector2(38.616f, 14.176f), new Vector2(37.751f, 15.7f), new Vector2(36.765f, 17.215f),
            new Vector2(35.66f, 18.72f), new Vector2(34.438f, 20.205f), new Vector2(33.105f, 21.657f), new Vector2(31.664f, 23.055f),
            new Vector2(30.123f, 24.377f), new Vector2(28.487f, 25.595f), new Vector2(26.764f, 26.686f), new Vector2(24.963f, 27.631f),
            new Vector2(23.091f, 28.419f), new Vector2(21.157f, 29.052f), new Vector2(19.17f, 29.539f), new Vector2(17.141f, 29.896f),
            new Vector2(15.078f, 30.145f), new Vector2(12.99f, 30.309f), new Vector2(10.887f, 30.409f), new Vector2(8.777f, 30.457f),
            new Vector2(6.668f, 30.465f), new Vector2(4.57f, 30.434f), new Vector2(2.488f, 30.365f), new Vector2(0.429f, 30.254f),
            new Vector2(-1.6f, 30.095f), new Vector2(-3.594f, 29.883f), new Vector2(-5.55f, 29.615f), new Vector2(-7.462f, 29.287f),
            new Vector2(-9.329f, 28.897f), new Vector2(-11.148f, 28.446f), new Vector2(-12.917f, 27.933f), new Vector2(-14.635f, 27.359f),
            new Vector2(-16.302f, 26.727f), new Vector2(-17.919f, 26.038f), new Vector2(-19.484f, 25.294f), new Vector2(-21f, 24.497f),
            new Vector2(-22.466f, 23.651f), new Vector2(-23.885f, 22.757f), new Vector2(-25.256f, 21.817f), new Vector2(-26.581f, 20.835f),
            new Vector2(-27.86f, 19.813f), new Vector2(-29.094f, 18.754f), new Vector2(-30.282f, 17.659f), new Vector2(-31.425f, 16.531f),
            new Vector2(-32.521f, 15.374f), new Vector2(-33.569f, 14.188f), new Vector2(-34.566f, 12.978f), new Vector2(-35.512f, 11.744f),
            new Vector2(-36.401f, 10.491f), new Vector2(-37.231f, 9.219f), new Vector2(-37.998f, 7.931f), new Vector2(-38.697f, 6.629f),
            new Vector2(-39.323f, 5.317f), new Vector2(-39.872f, 3.996f), new Vector2(-40.338f, 2.667f), new Vector2(-40.716f, 1.335f),
            new Vector2(-41f, 0f), new Vector2(-41.185f, -1.335f), new Vector2(-41.267f, -2.667f), new Vector2(-41.24f, -3.996f),
            new Vector2(-41.101f, -5.317f), new Vector2(-40.846f, -6.629f), new Vector2(-40.471f, -7.931f), new Vector2(-39.976f, -9.219f),
            new Vector2(-39.357f, -10.491f), new Vector2(-38.616f, -11.744f), new Vector2(-37.751f, -12.978f), new Vector2(-36.765f, -14.188f),
            new Vector2(-35.66f, -15.374f), new Vector2(-34.438f, -16.531f), new Vector2(-33.105f, -17.659f), new Vector2(-31.664f, -18.754f),
            new Vector2(-30.123f, -19.813f), new Vector2(-28.487f, -20.835f), new Vector2(-26.764f, -21.817f), new Vector2(-24.963f, -22.757f),
            new Vector2(-23.091f, -23.651f), new Vector2(-21.157f, -24.497f), new Vector2(-19.17f, -25.294f), new Vector2(-17.141f, -26.037f),
            new Vector2(-15.078f, -26.726f), new Vector2(-12.99f, -27.358f), new Vector2(-10.887f, -27.93f), new Vector2(-8.777f, -28.441f),
            new Vector2(-6.668f, -28.888f), new Vector2(-4.57f, -29.269f), new Vector2(-2.488f, -29.582f), new Vector2(-0.429f, -29.827f),
            new Vector2(1.6f, -30f), new Vector2(3.594f, -30.101f), new Vector2(5.55f, -30.129f), new Vector2(7.462f, -30.082f),
            new Vector2(9.329f, -29.959f), new Vector2(11.148f, -29.761f), new Vector2(12.917f, -29.486f), new Vector2(14.635f, -29.134f),
            new Vector2(16.302f, -28.706f), new Vector2(17.919f, -28.202f), new Vector2(19.484f, -27.622f), new Vector2(21f, -26.967f),
            new Vector2(22.466f, -26.238f), new Vector2(23.885f, -25.436f), new Vector2(25.256f, -24.563f), new Vector2(26.581f, -23.622f),
            new Vector2(27.86f, -22.613f), new Vector2(29.094f, -21.54f), new Vector2(30.282f, -20.405f), new Vector2(31.425f, -19.211f),
            new Vector2(32.521f, -17.961f), new Vector2(33.569f, -16.658f), new Vector2(34.566f, -15.306f), new Vector2(35.512f, -13.909f),
            new Vector2(36.401f, -12.47f), new Vector2(37.231f, -10.995f), new Vector2(37.998f, -9.486f), new Vector2(38.697f, -7.949f),
            new Vector2(39.323f, -6.388f), new Vector2(39.872f, -4.808f), new Vector2(40.338f, -3.214f), new Vector2(40.716f, -1.609f),
        };
    }
}
