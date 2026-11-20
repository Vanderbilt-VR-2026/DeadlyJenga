using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Compact candy-city layouts built from existing city parts.
    // New here: Jenga towers (one physical body per floor, so the Hand can pull single floors)
    // and climb-only crowns (walls taller than jump + vault, no stairs).
    public static partial class EpicCityLayout
    {
        public const int PlazaWheelDesign = 10, CandyCanyonDesign = 11, PlazaCanyonDesign = 12, MechanismCityDesign = 13, SkyRingCityDesign = 14;
        public static bool IsCompactCandyCity(int design) => design >= PlazaWheelDesign;

        sealed partial class Builder
        {
            // 3 m bridges fit between a deck's corner pillars and its stair opening (4.5 m ones clip them).
            const float BridgeWidth = 3;
            // A: four districts around an open central plaza, a durable sky ring and a fragile cross-bridge.
            public void PlazaWheel()
            {
                var order = Ring();
                Link(order[1], order[7], 13.2f, BridgeWidth);
            }

            // A+B: the Plaza Wheel ring with a short canyon (two tower pairs) through its middle.
            // The gap between the two columns is a small central plaza; both canyon ends open onto the ring street.
            public void PlazaCanyon()
            {
                var order = Ring();
                int nw = JengaSite("Canyon north-west tower", -23, 17, 6, 22, 22, 0, true);
                int ne = JengaSite("Canyon north-east tower", 23, 17, 4, 22, 22, 0);
                int sw = JengaSite("Canyon south-west tower", -23, -17, 3, 22, 22, 180);
                int se = JengaSite("Canyon south-east tower", 23, -17, 7, 22, 22, 180, true);
                // Crossings at three heights: durable low, fragile middle, fragile high diagonal over the plaza.
                DurableLink(nw, sw, 9.2f); Link(ne, se, 13.2f, BridgeWidth); Link(nw, se, 21.2f, BridgeWidth);
                DurableLink(nw, ne, 9.2f);
                // Spokes tie each inner tower to its nearest ring building, so the canyon joins the sky ring.
                foreach (int inner in new[] { nw, ne, sw, se })
                {
                    int best = order[0];
                    foreach (int s in order) if ((plan.sites[s].position - plan.sites[inner].position).sqrMagnitude < (plan.sites[best].position - plan.sites[inner].position).sqrMagnitude) best = s;
                    DurableLink(inner, best, 9.2f);
                }
            }

            System.Collections.Generic.List<int> Ring()
            {
                const float ring = 66;
                var order = new System.Collections.Generic.List<int>();
                for (int i = 0; i < 12; i++)
                {
                    float angle = 15 + i * 30;
                    var p = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0, Mathf.Sin(angle * Mathf.Deg2Rad)) * ring;
                    float yaw = 90 - angle; // front (local -z) faces the plaza
                    int zone = i / 3, slot = i % 3;
                    if (slot == 1)
                    {
                        switch (zone)
                        {
                            case 0: order.Add(JengaSite("Downtown spire", p.x, p.z, 6, 24, 24, yaw, true)); break;
                            case 1: order.Add(JengaSite("Sugar Market hall", p.x, p.z, 2, 28, 22, yaw)); break;
                            case 2: order.Add(JengaSite("Grape Heights tower", p.x, p.z, 4, 24, 24, yaw, true)); break;
                            default: order.Add(JengaSite("Cloud Harbor block", p.x, p.z, 3, 28, 22, yaw)); break;
                        }
                    }
                    else
                    {
                        string[] names = { "Downtown", "Sugar Market", "Grape Heights", "Cloud Harbor" };
                        int floors = zone == 2 ? (slot == 0 ? 2 : 3) : (slot == 0 ? 3 : 2);
                        order.Add(JengaSite(names[zone] + (slot == 0 ? " west" : " east") + " annex", p.x, p.z, floors, 24, 24, yaw));
                    }
                }
                // Durable sky ring at the second-floor decks.
                for (int i = 0; i < order.Count; i++) DurableLink(order[i], order[(i + 1) % order.Count], 9.2f);
                return order;
            }

            // B: one boulevard between two rows of Jenga towers, crossed at three heights.
            public void CandyCanyon()
            {
                float[] xs = { -75, -45, -15, 15, 45, 75 };
                int[] northFloors = { 3, 7, 4, 6, 3, 5 }, southFloors = { 5, 3, 7, 4, 6, 3 };
                var north = new int[6]; var south = new int[6];
                for (int i = 0; i < 6; i++)
                {
                    north[i] = JengaSite("North tower " + (i + 1), xs[i], 26, northFloors[i], 22, 22, 0, northFloors[i] >= 6);
                    south[i] = JengaSite("South tower " + (i + 1), xs[i], -26, southFloors[i], 22, 22, 180, southFloors[i] >= 6);
                }
                JengaSite("Cloud Harbor pier", 101, 0, 1, 40, 18, 90);
                // Row links make upper loops along each wall (durable).
                DurableLink(north[0], north[1], 9.2f); DurableLink(north[1], north[2], 12.8f); DurableLink(north[3], north[4], 9.2f);
                DurableLink(south[0], south[1], 9.2f); DurableLink(south[2], south[3], 12.8f); DurableLink(south[4], south[5], 9.2f);
                // Canyon crossings: two durable at 8 m, the higher ones fragile (light bridges the Hand can pull).
                DurableLink(north[0], south[0], 9.2f); DurableLink(north[3], south[3], 9.2f);
                Link(north[1], south[1], 12.8f, BridgeWidth); Link(north[2], south[2], 16.8f, BridgeWidth); Link(north[4], south[4], 12.8f, BridgeWidth); Link(north[5], south[5], 12.8f, BridgeWidth);
                Link(north[1], south[2], 24.8f, BridgeWidth);
            }

            int JengaSite(string name, float x, float z, int floors, float w, float depth, float yaw = 0, bool crown = false)
            {
                district = plan.sites.Count;
                var site = new EpicCitySite { kind = "Jenga tower", name = name, position = new Vector3(x, .35f, z), heading = yaw, tiers = Mathf.Max(1, floors / 3), width = w, depth = depth };
                plan.sites.Add(site);
                int first = plan.modules.Count; var q = Quaternion.Euler(0, yaw, 0);
                Base(site, w, depth);
                for (int f = 0; f < floors; f++)
                {
                    // Turned around so the stair opening is at the back; front and side decks stay clear for bridges.
                    var floor = Cassette(site.position + Vector3.up * (.8f + f * 4), w, depth, 1, yaw + 180, name + " floor " + (f + 1), false, f);
                    floor.mass = Mathf.Clamp(w * depth * .45f, 60, 260);
                }
                float roof = .8f + floors * 4;
                if (crown)
                {
                    // On the front half of the roof, clear of the stair hole at the back; 5.5 m of plain wall is climb-only.
                    var top = Module(site.position + Vector3.up * roof, "Climb-only candy crown", yaw);
                    Part(top, new Vector3(0, 2.75f, -depth * .25f), new Vector3(w * .45f, 5.5f, depth * .4f), 0);
                    top.mass = 160;
                }
                var own = plan.modules.GetRange(first, plan.modules.Count - first);
                site.members.AddRange(own); site.bounds = MayhemStructureLayout.Bounds(own);
                foreach (float y in new[] { 4.8f, 8.8f, 12.8f, 16.8f, 20.8f, 24.8f, 28.8f }) if (y <= roof + .5f) site.ports.Add(site.position + Vector3.up * y);
                plan.spawnPoints.Add(site.position + q * new Vector3(0, 1, -depth * .5f - 8));
                return district;
            }

            void DurableLink(int from, int to, float target)
            {
                int before = plan.modules.Count;
                Link(from, to, target, BridgeWidth);
                for (int i = before; i < plan.modules.Count; i++) plan.modules[i].mass = 450;
            }
        }
    }
}
