using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Candy Mechanism City: Candy Plaza Canyon with engineered mechanisms transplanted from the workshop maps.
    // Each piece keeps its own tested geometry, masses and hinges; it is only moved, turned and (for the two
    // lab towers) trimmed to fewer tiers.
    // Candy Sky Ring City (skyRing): the same city with the sky ring closed. Two annexes grow taller so their
    // roofs line up with the two lab towers, and those sky bridges land on the towers' moving parts (the Bookfall
    // wing and the Drop-Stop's dropping floor). The island is trimmed close to the buildings.
    public static partial class EpicCityLayout
    {
        sealed partial class Builder
        {
            public void MechanismCity(bool skyRing = false)
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
                order.Add(Transplant("Downtown drop-stop tower", TowerExhibitionLayout.Build(false, 5), TowerExhibitionLayout.Centers[5], p, TangentX(45) + (skyRing ? 180 : 0),
                    m => m.position.y - TowerExhibitionLayout.Centers[5].y < 31));
                if (skyRing)
                {
                    int tier = 0;
                    foreach (var member in plan.sites[order[0]].members)
                        if (member.purpose == "Escape spine") SubstationStairs(member, tier++);
                }
                // Sugar Market: unchanged hall with two annexes.
                p = OnRing(105, ring); order.Add(JengaSite("Sugar Market west annex", p.x, p.z, 3, 24, 24, FacePlaza(105)));
                var schoolFront=Quaternion.Euler(0,FacePlaza(165)+180,0);
                p = skyRing ? OnRing(165,ring)+schoolFront*new Vector3(-25.5f,0,0) : OnRing(135,ring);
                order.Add(JengaSite("Sugar Market hall",p.x,p.z,skyRing?4:2,skyRing?20:28,22,FacePlaza(skyRing?165:135)));
                p = OnRing(165, ring); order.Add(JengaSite("Sugar Market east annex", p.x, p.z, skyRing ? 7 : 2, 24, 24, FacePlaza(165)));
                // Grape Heights: Bookfall Keep (three spine tiers, one pair of swing-down wings).
                p = OnRing(225, 84);
                order.Add(Transplant("Grape Heights bookfall keep", TowerExhibitionLayout.Build(false, 8), TowerExhibitionLayout.Centers[8], p, TangentZ(225), m =>
                {
                    var local = m.position - TowerExhibitionLayout.Centers[8];
                    bool wingPart = m.purpose.Contains("wing") || m.purpose.Contains("pedestal") || m.purpose.Contains("receiving foot");
                    return wingPart ? Mathf.Abs(local.z + 24) < 1 : local.y < 36;
                }));
                if (skyRing)
                {
                    var apartments=plan.sites[order[4]];
                    var upper=apartments.members.FindLast(m=>m.purpose=="Independent bookfall support spine");
                    foreach (int side in new[]{-1,1})
                        Part(upper,new Vector3(side*5.1f,14.2f,7),new Vector3(11.5f,.58f,34),
                            0,Quaternion.Euler(0,0,-side*22));
                    Part(upper,new Vector3(0,14.15f,7),new Vector3(.8f,4.3f,34),3);
                    geometryBounds.Remove(upper);
                }
                // Cloud Harbor: unchanged block with two annexes.
                p = OnRing(285, ring); order.Add(JengaSite("Cloud Harbor west annex", p.x, p.z, 2, 24, 24, FacePlaza(285)));
                p = OnRing(315, ring); order.Add(JengaSite("Cloud Harbor block", p.x, p.z, skyRing ? 2 : 3, 28, 22, FacePlaza(315)));
                p = OnRing(345, ring); order.Add(JengaSite("Cloud Harbor east annex", p.x, p.z, skyRing ? 5 : 3, 24, 24, FacePlaza(345)));
                for (int i = 0; i < order.Count; i++)
                {
                    int next = (i + 1) % order.Count;
                    // Points found offline with the same clearance test the planner uses.
                    if (skyRing && i == 2)
                    {
                        var annex=OnRing(165,ring);
                        DeckLink(order[2],order[3],annex+schoolFront*new Vector3(-17.5f,9.15f,-3),
                            annex+schoolFront*new Vector3(-10,9.15f,-3),false);
                    }
                    else if (skyRing && i == 3) DeckLink(order[3], order[4], new Vector3(-63.96f, 29.15f, 8.96f), new Vector3(-56.46f, 29.15f, -28.39f), false);
                    else if (skyRing && i == 7) DeckLink(order[7], order[0], new Vector3(81.11f, 21.15f, -13.55f), OnRing(45,80) + Quaternion.Euler(0,45,0)*new Vector3(26.8f,21.15f,-3.1f), false);
                    else TryLink(order[i], order[next], true, 9.2f, 13.2f, 5.2f, 17.2f);
                }

                // Canyon: two tower pairs, wider apart so the keystone vault fits between them.
                int nw = JengaSite("Canyon north-west tower", -27, 17, 6, 22, 22, 0, true);
                int ne = JengaSite("Canyon north-east tower", 27, 17, 4, 22, 22, 0);
                int sw = JengaSite("Canyon south-west tower", -27, -17, 3, 22, 22, 180);
                int se = JengaSite("Canyon south-east tower", 27, -17, 7, 22, 22, 180, !skyRing);
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
                plan.routes.Add(new EpicCityRoute { name = "Plaza keystone vault to Canyon south-west tower", from = vault, to = sw, a = new Vector3(-11,13.01f,-10), b = new Vector3(-19.5f,13.15f,-12) });
                Bridge(new Vector3(11, 13.01f, 8), new Vector3(20, 13.15f, 12), BridgeWidth, "Loose city street crossing");
                plan.routes.Add(new EpicCityRoute { name = "Plaza keystone vault to Canyon north-east tower", from = vault, to = ne, a = new Vector3(11,13.01f,8), b = new Vector3(20,13.15f,12) });
                if (skyRing)
                {
                    // Low spokes from the canyon's first floors to the ring (the planner never tries below 9 m).
                    DeckLink(nw, order[2], new Vector3(-34.2f,5.15f,18), plan.sites[order[2]].position + schoolFront*new Vector3(0,4.8f,8), true);
                    DeckLink(ne, order[7], new Vector3(27, 5.15f, 11.65f), new Vector3(59.87f, 5.15f, -24.22f), true);
                }
                foreach (int inner in skyRing ? new[] { sw, se } : new[] { nw, ne, sw, se })
                {
                    int best = order[0];
                    foreach (int s in order) if ((plan.sites[s].position - plan.sites[inner].position).sqrMagnitude < (plan.sites[best].position - plan.sites[inner].position).sqrMagnitude) best = s;
                    if (skyRing && inner==sw && best==order[4])
                        DeckLink(inner,best,new Vector3(-35,13.15f,-25.2f),new Vector3(-51.5658f,13.15f,-58.7429f),true);
                    else if (skyRing && inner==se && best==order[6])
                        DeckLink(inner,best,new Vector3(31,9.15f,-25.2f),new Vector3(46,9.15f,-43),true);
                    else TryLink(inner, best, true, 9.2f, 13.2f, 5.2f);
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
                    Transplant("Harbor bascule " + angle, MayhemStructureLayout.Build(9), harborOrigin + unit, OnRing(angle, skyRing ? 90 : 98), TangentZ(angle), m =>
                    {
                        var local = m.position - harborOrigin - unit;
                        return Mathf.Abs(local.x) < 9 && local.z > -20 && local.z < 25 && m.purpose != "Loose route bridge";
                    }, true);
                }
                // Last, once every bridge exists: Jenga plank walls that leave each crossing open.
                if (skyRing)
                {
                    BuildPumpHall(plan.sites[se]);
                    BuildPumpHall(plan.sites[sw]);
                    district = sw;
                    var a = new Vector3(-18,9.15f,-23);
                    var b = new Vector3(18,9.15f,-23);
                    plan.routes.Add(new EpicCityRoute { name="Rainwater plant service bridge", from=sw, to=se, a=a, b=b });
                    Bridge(a,b,3.4f,"Rainwater gallery crossing");
                    PlankWalls();
                }
            }

            void BuildPumpHall(EpicCitySite site)
            {
                int level = 0;
                foreach (var floor in site.members)
                {
                    if (!floor.purpose.Contains(" floor ")) continue;
                    level++;
                    // At alternate heights the stair and its side landings occupy just the rear
                    // half. From the street the lower pump room and upper gallery are double-height.
                    if (level == 7)
                    {
                        floor.shapes.RemoveRange(4,floor.shapes.Count-4);
                        Flight(floor,new Vector3(-7.95f,0,-6),new Vector3(-7.95f,4,6),2.9f);
                        foreach(int end in new[]{-1,1})
                            Part(floor,new Vector3(-7.7f,3.65f,end*8.2f),new Vector3(5.4f,.7f,4.4f),1);
                        Part(floor,new Vector3(2.7f,3.65f,0),new Vector3(15.4f,.7f,20.8f),1);
                        // The collector is seated on the solid half of the roof, past the stair head.
                        var center = new Vector3(2.7f,4.12f,0);
                        for(int edge=0;edge<4;edge++)
                        {
                            var turn=Quaternion.Euler(0,90*edge,0);
                            float halfWidth=edge%2==0?7.3f:9.6f;
                            float reach=edge%2==0?9.6f:7.3f;
                            var a=new Vector3(-1.65f,0,1.65f);
                            var c=new Vector3(halfWidth,2.85f,reach);
                            var top=new[]{a,new Vector3(-a.x,0,a.z),c,new Vector3(-c.x,c.y,c.z)};
                            var v=new Vector3[8];
                            for(int n=0;n<4;n++){v[n]=top[n]-Vector3.up*.22f;v[n+4]=top[n];}
                            floor.shapes.Add(new MayhemShape(center,new Vector3(halfWidth*2,3.07f,reach-1.65f),
                                2,turn,v));
                        }
                        continue;
                    }
                    floor.shapes.RemoveRange(4,floor.shapes.Count-4);
                    // The stairwell is on the back wall. Only a low mezzanine and the roof
                    // extend across the large public face overlooking the pool.
                    bool upNorth = level%2==1;
                    float lane = upNorth ? -7.95f : -4.7f;
                    Flight(floor,new Vector3(lane,0,upNorth?-6:6),new Vector3(lane,4,upNorth?6:-6),2.9f);
                    foreach(int end in new[]{-1,1})
                        Part(floor,new Vector3(-6.6f,3.65f,end*8.2f),new Vector3(7.6f,.7f,4.4f),1);
                    bool roofOfWing=level==3 && site.members.Count<7;
                    if(level==2 || roofOfWing)
                    {
                        // Front edge of the gallery under both yellow bridge bearings.
                        Part(floor,new Vector3(3.8f,3.65f,-8.2f),new Vector3(13.2f,.7f,4.4f),1);
                        Part(floor,new Vector3(8.35f,3.65f,0),new Vector3(4.1f,.7f,12),1);
                    }
                    if(level==3 || level==5 || roofOfWing)
                        Part(floor,new Vector3(3.8f,3.65f,8.2f),new Vector3(13.2f,.7f,4.4f),1);
                    // This pair of recessed wall panels backs the pumps and frames the open room.
                    Part(floor,new Vector3(-10.6f,2,1.5f),new Vector3(.8f,4,8),0);
                    foreach(int side in new[]{-1,1})
                        Part(floor,new Vector3(side*6.9f,2,10.6f),new Vector3(3.4f,4,.8f),0);
                    geometryBounds.Remove(floor);
                }
            }

            // Keep the four original load-bearing columns. The public stairs run along the long
            // elevation. A full-width landing at either end joins successive flights and tiers.
            void SubstationStairs(MayhemModule tower, int tier)
            {
                tower.shapes.RemoveRange(4, tower.shapes.Count - 4);
                const float run = 9.3f;
                float start = tier%2==0 ? run : -run;
                // Three tall storeys produce one continuous zigzag in the public elevation.
                Flight(tower,new Vector3(4.85f,0,start),new Vector3(4.85f,15,-start),6.5f);
                foreach(int end in new[]{-1,1})
                    Part(tower,new Vector3(0,14.6f,end*10.9f),new Vector3(17,.8f,3.2f),1);
                Part(tower,new Vector3(-3.6f,14.6f,0),new Vector3(9.8f,.8f,18.6f),1);
                // Backstage service floor connects the drop cassette's loose crossing to the public flight.
                float zAtTen=Mathf.Lerp(start,-start,2f/3f);
                Part(tower,new Vector3(-3.6f,9.6f,0),new Vector3(9.8f,.8f,18.6f),1);
                Part(tower,new Vector3(1.47f,9.6f,zAtTen),new Vector3(.34f,.8f,3),1);
                foreach(int end in new[]{-1,1})
                {
                    Part(tower,new Vector3(-8.15f,7.5f,end*10),new Vector3(.68f,15,4.8f),0);
                    Part(tower,new Vector3(-4.5f,11.1f,end*7),new Vector3(3.6f,2.2f,2.4f),3);
                }
                if(tier==1)
                {
                    // The entrance from the purple annex meets the outside stair at exactly +5 m.
                    Part(tower,new Vector3(9.9f,4.6f,-3.1f),new Vector3(3.8f,.8f,4.8f),1);
                    Part(tower,new Vector3(9.4f,2.25f,-3.1f),new Vector3(.75f,4.5f,3.4f),0);
                }
                if (tier == 2)
                    Part(tower,new Vector3(-4,15.55f,0),new Vector3(4.8f,1.1f,4.8f),3);
                geometryBounds.Remove(tower);
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

            // A bridge between two known deck points, with trestles on long spans (like Link).
            void DeckLink(int from, int to, Vector3 a, Vector3 b, bool durable)
            {
                district = from; int before = plan.modules.Count;
                plan.routes.Add(new EpicCityRoute { name = plan.sites[from].name + " to " + plan.sites[to].name, from = from, to = to, a = a, b = b });
                float distance = Vector3.ProjectOnPlane(b - a, Vector3.up).magnitude; int spans = Mathf.Max(1, Mathf.CeilToInt(distance / 32));
                var seats = new List<Vector3> { a };
                for (int n = 1; n < spans; n++) { var point = Vector3.Lerp(a, b, n / (float)spans); if (FootClear(point)) { Trestle(point, b - a); seats.Add(point); } }
                seats.Add(b);
                for (int n = 1; n < seats.Count; n++) Bridge(seats[n - 1], seats[n], BridgeWidth, "Loose city street crossing", n == 1, n == seats.Count - 1);
                if (durable) for (int i = before; i < plan.modules.Count; i++) plan.modules[i].mass = 450;
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
