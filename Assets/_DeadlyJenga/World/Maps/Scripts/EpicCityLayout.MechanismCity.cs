using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Candy Mechanism City: Candy Plaza Canyon with engineered mechanisms transplanted from the workshop maps.
    // Each piece keeps its own tested geometry, masses and hinges; it is only moved, turned and (for the two
    // lab towers) trimmed to fewer tiers.
    public static partial class EpicCityLayout
    {
        sealed partial class Builder
        {
            public void MechanismCity()
            {
                const float ring = 72;
                Vector3 OnRing(float angle, float radius) => new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0, Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;
                // Yaw that turns a piece's local X (or Z) along the ring's tangent at this angle.
                float TangentX(float angle) => Mathf.Atan2(-Mathf.Cos(angle * Mathf.Deg2Rad), -Mathf.Sin(angle * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
                float TangentZ(float angle) => Mathf.Atan2(-Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
                float FacePlaza(float angle) => 90 - angle;

                var order = new List<int>();
                // Downtown: Drop-Stop Tower (two tiers): pulling its shoes drops floors onto catch frames.
                var p = OnRing(45, 80);
                order.Add(Transplant("Downtown drop-stop tower", TowerExhibitionLayout.Build(false, 5), TowerExhibitionLayout.Centers[5], p, TangentX(45),
                    m => m.position.y - TowerExhibitionLayout.Centers[5].y < 31));
                // Sugar Market: unchanged hall with two annexes.
                p = OnRing(105, ring); order.Add(JengaSite("Sugar Market west annex", p.x, p.z, 3, 24, 24, FacePlaza(105)));
                p = OnRing(135, ring); order.Add(JengaSite("Sugar Market hall", p.x, p.z, 2, 28, 22, FacePlaza(135)));
                p = OnRing(165, ring); order.Add(JengaSite("Sugar Market east annex", p.x, p.z, 2, 24, 24, FacePlaza(165)));
                // Grape Heights: Bookfall Keep (three spine tiers, one pair of swing-down wings).
                p = OnRing(225, 84);
                order.Add(Transplant("Grape Heights bookfall keep", TowerExhibitionLayout.Build(false, 8), TowerExhibitionLayout.Centers[8], p, TangentZ(225), m =>
                {
                    var local = m.position - TowerExhibitionLayout.Centers[8];
                    bool wingPart = m.purpose.Contains("wing") || m.purpose.Contains("pedestal") || m.purpose.Contains("receiving foot");
                    return wingPart ? Mathf.Abs(local.z + 24) < 1 : local.y < 36;
                }));
                // Cloud Harbor: unchanged block with two annexes.
                p = OnRing(285, ring); order.Add(JengaSite("Cloud Harbor west annex", p.x, p.z, 2, 24, 24, FacePlaza(285)));
                p = OnRing(315, ring); order.Add(JengaSite("Cloud Harbor block", p.x, p.z, 3, 28, 22, FacePlaza(315)));
                p = OnRing(345, ring); order.Add(JengaSite("Cloud Harbor east annex", p.x, p.z, 3, 24, 24, FacePlaza(345)));
                for (int i = 0; i < order.Count; i++) TryLink(order[i], order[(i + 1) % order.Count], true, 9.2f, 13.2f, 5.2f, 17.2f);

                // Canyon: two tower pairs, wider apart so the keystone vault fits between them.
                int nw = JengaSite("Canyon north-west tower", -27, 17, 6, 22, 22, 0, true);
                int ne = JengaSite("Canyon north-east tower", 27, 17, 4, 22, 22, 0);
                int sw = JengaSite("Canyon south-west tower", -27, -17, 3, 22, 22, 180);
                int se = JengaSite("Canyon south-east tower", 27, -17, 7, 22, 22, 180, true);
                DurableLink(nw, sw, 9.2f); Link(ne, se, 13.2f, BridgeWidth); Link(nw, se, 21.2f, BridgeWidth);
                // Keystone vault: a raised plaza on two five-stone arches; pull a keystone and the deck fails.
                var vaultOrigin = MayhemStructureLayout.Centers[7];
                int vault = Transplant("Plaza keystone vault", MayhemStructureLayout.Build(7), vaultOrigin, Vector3.zero, 0, m =>
                {
                    var local = m.position - vaultOrigin;
                    return Mathf.Abs(local.x) < 16 && local.y < 12.6f && m.purpose != "Loose route bridge";
                });
                // The planner treats the arch stones under the deck like ramps and refuses every path off it,
                // so the vault gets two explicit short bridges: to the south-west roof and the north-east third deck.
                Bridge(new Vector3(-11, 13.01f, -10), new Vector3(-19.5f, 13.15f, -12), BridgeWidth, "Loose city street crossing");
                plan.routes.Add(new EpicCityRoute { name = "Plaza keystone vault to Canyon south-west tower", from = vault, to = sw });
                Bridge(new Vector3(11, 13.01f, 8), new Vector3(20, 13.15f, 12), BridgeWidth, "Loose city street crossing");
                plan.routes.Add(new EpicCityRoute { name = "Plaza keystone vault to Canyon north-east tower", from = vault, to = ne });
                foreach (int inner in new[] { nw, ne, sw, se })
                {
                    int best = order[0];
                    foreach (int s in order) if ((plan.sites[s].position - plan.sites[inner].position).sqrMagnitude < (plan.sites[best].position - plan.sites[inner].position).sqrMagnitude) best = s;
                    TryLink(inner, best, true, 9.2f, 13.2f, 5.2f);
                }

                // Small set pieces in the four pockets between the canyon and the ring.
                var concept = new Vector3(0, .4f, 0);
                Transplant("North pivoting causeway", ConceptMapLayout.Build(ConceptMapKind.PivotingCauseway), concept, new Vector3(0, 0, 42), 180);
                Transplant("South collapse-created route", ConceptMapLayout.Build(ConceptMapKind.CollapseCreatedRoute), concept, new Vector3(0, 0, -42), 0);
                Transplant("East moving shelter", ConceptMapLayout.Build(ConceptMapKind.MovingShelter), concept, new Vector3(47, 0, 0), 90);
                Transplant("West peel-away screens", ConceptMapLayout.Build(ConceptMapKind.PeelAwayScreen), concept, new Vector3(-47, 0, 0), 90);

                // Two counterweighted bascule drawbridges on the outer harbor edge.
                var harborOrigin = MayhemStructureLayout.Centers[9];
                foreach (var (unitX, angle) in new[] { (-43.5f, 300f), (-14.5f, 330f) })
                {
                    var unit = new Vector3(unitX, 0, -26);
                    Transplant("Harbor bascule " + angle, MayhemStructureLayout.Build(9), harborOrigin + unit, OnRing(angle, 98), TangentZ(angle), m =>
                    {
                        var local = m.position - harborOrigin - unit;
                        return Mathf.Abs(local.x) < 9 && local.z > -20 && local.z < 25 && m.purpose != "Loose route bridge";
                    }, true);
                }
            }

            // Recreates what ConceptMechanismMap adds itself: anchored "Fixed" pieces, hinges, the roof slider.
            static void ConfigureConcept(List<MayhemModule> kept)
            {
                MayhemModule Find(string purpose) => kept.Find(m => m.purpose == purpose);
                foreach (var m in kept)
                {
                    if (m.purpose.StartsWith("Fixed ", StringComparison.Ordinal)) m.anchored = true;
                    if (m.purpose == "Falling route" && m.hingeSupport != null) { m.hingeAxis = Vector3.forward; m.hingeMin = 0; m.hingeMax = 28; m.hingeCollide = false; }
                    if (m.purpose == "Pivot route plate" && m.hingeSupport != null)
                    {
                        m.hingeAxis = Vector3.up; m.hingeMin = -65; m.hingeMax = 65; m.hingeCollide = false;
                        foreach (var landing in new[] { Find("Fixed west landing"), Find("Fixed east landing") }) if (landing != null) m.ignore.Add(landing);
                    }
                    if (m.purpose.StartsWith("Screen ", StringComparison.Ordinal))
                    {
                        bool west = m.purpose == "Screen west";
                        m.hingeToWorld = true; m.pivot = m.position + m.rotation * new Vector3(-2, 0, 0);
                        m.hingeAxis = Vector3.forward; m.hingeMin = west ? 0 : -100; m.hingeMax = west ? 100 : 0;
                    }
                    if (m.purpose == "Moving roof") { m.slideSupport = Find("Fixed roof frame A"); m.slideAxis = Vector3.right; m.slideLimit = 2.5f; }
                }
            }

            // Tries each height until a real bridge lands; keeps a single ground-street note if none do.
            bool TryLink(int from, int to, bool durable, params float[] heights)
            {
                foreach (float h in heights)
                {
                    int routes = plan.routes.Count, modules = plan.modules.Count;
                    if (durable) DurableLink(from, to, h); else Link(from, to, h, BridgeWidth);
                    if (plan.routes.Count > routes && !plan.routes[routes].name.StartsWith("Ground street", StringComparison.Ordinal)) return true;
                    plan.routes.RemoveRange(routes, plan.routes.Count - routes);
                    plan.modules.RemoveRange(modules, plan.modules.Count - modules);
                }
                plan.routes.Add(new EpicCityRoute { name = "Ground street: " + plan.sites[from].name + " to " + plan.sites[to].name, from = from, to = to, a = plan.sites[from].position, b = plan.sites[to].position });
                return false;
            }

            // Moves pieces built by another map's generator into this city as one site.
            // sourceOrigin is the source ground point that lands on "at". Hinges whose support was left out are dropped.
            int Transplant(string name, List<MayhemModule> parts, Vector3 sourceOrigin, Vector3 at, float yaw, Func<MayhemModule, bool> keep = null, bool mayhemHinges = false)
            {
                district = plan.sites.Count;
                var site = new EpicCitySite { kind = "Transplant", name = name, position = new Vector3(at.x, .35f, at.z), heading = yaw, tiers = 1 };
                plan.sites.Add(site);
                var q = Quaternion.Euler(0, yaw, 0);
                var kept = new List<MayhemModule>();
                foreach (var m in parts) if (keep == null || keep(m)) kept.Add(m);
                var set = new HashSet<MayhemModule>(kept);
                foreach (var m in kept)
                {
                    m.position = site.position + q * (m.position - sourceOrigin);
                    m.rotation = q * m.rotation;
                    if (m.hingeSupport != null)
                    {
                        if (!set.Contains(m.hingeSupport)) m.hingeSupport = null;
                        else
                        {
                            m.pivot = site.position + q * (m.pivot - sourceOrigin);
                            // Mayhem Foundry's bascule leaves pitch about the map's X axis, within -3..55 degrees.
                            if (mayhemHinges) { m.hingeAxis = Vector3.right; m.hingeMin = -3; m.hingeMax = 55; }
                        }
                    }
                    m.district = district;
                    plan.modules.Add(m);
                }
                if (sourceOrigin.y > .3f) ConfigureConcept(kept); // concept pieces stand on a .4 m deck
                site.members.AddRange(kept); site.bounds = MayhemStructureLayout.Bounds(kept);
                site.width = site.bounds.size.x; site.depth = site.bounds.size.z; site.tiers = Mathf.Max(1, Mathf.RoundToInt(site.bounds.size.y / 12));
                foreach (float y in new[] { 4.8f, 8.8f, 12.8f, 16.8f, 20.8f, 24.8f, 28.8f }) if (y <= site.bounds.max.y) site.ports.Add(site.position + Vector3.up * y);
                var inward = new Vector3(-at.x, 0, -at.z);
                plan.spawnPoints.Add(inward.sqrMagnitude > 1
                    ? site.position + inward.normalized * (Mathf.Max(site.bounds.extents.x, site.bounds.extents.z) + 6) + Vector3.up
                    : site.position + new Vector3(0, 1, -site.bounds.extents.z - 6));
                return district;
            }
        }
    }
}
