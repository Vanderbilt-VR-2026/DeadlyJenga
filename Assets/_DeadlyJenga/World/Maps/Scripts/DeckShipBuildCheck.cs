using System.Collections.Generic;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Safety checks for a Deck Ship build, for whoever is editing the hull:
    // the cargo deck is flat, at its marked height and empty; every cargo layout fits on it; spawns stand on
    // floor; nothing in the hull is Static or has its own Rigidbody; and ship events really tilt every part.
    // Run it on a throwaway copy of the map that has been built: the motion part moves the ship.
    // Lines start with FAIL (must fix), WARN (worth a look) or OK.
    public static class DeckShipBuildCheck
    {
        const float Flat = .03f;          // allowed bump in the cargo deck, metres
        const float MaxTilt = 1.5f;       // allowed slope of the cargo deck at rest, degrees

        public static int Run(DeckShipMap map, StringBuilder report)
        {
            int failures = 0;
            void Fail(string text) { report.AppendLine("  FAIL " + text); failures++; }
            void Warn(string text) => report.AppendLine("  WARN " + text);
            void Ok(string text) => report.AppendLine("  OK   " + text);

            var ship = map.Ship; var hull = map.Hull;
            if (!ship || !hull || map.Blocks.Count == 0) { Fail("map is not built (no hull)"); return failures; }
            report.AppendLine($"Ship build: {(map.hullPrefab ? "hull prefab '" + map.hullPrefab.name + "'" : "built-in box ship")}");
            Physics.SyncTransforms();

            // 1. Things that would stop the hull from moving as one piece.
            var statics = hull.GetComponentsInChildren<Transform>(true).Where(t => t.gameObject.isStatic).Select(t => t.name).ToList();
            if (statics.Count > 0) Fail($"{statics.Count} hull objects are marked Static and will not rock with the ship: {string.Join(", ", statics.Take(6))}");
            var extraBodies = hull.GetComponentsInChildren<Rigidbody>(true).Where(b => b.transform != hull).ToList();
            if (extraBodies.Count > 0) Fail("Rigidbodies inside the hull: " + string.Join(", ", extraBodies.Select(b => b.name)));
            var blockLayer = hull.GetComponentsInChildren<Collider>(true).Where(c => c.gameObject.layer == Layers.Block).ToList();
            if (blockLayer.Count > 0) Fail("hull colliders on the Block layer (players fall through those): " + string.Join(", ", blockLayer.Select(c => c.name).Take(6)));
            var solids = hull.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger && c.gameObject.layer == 0).ToList();
            var climbs = hull.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger && c.gameObject.layer == Layers.Surface).ToList();
            if (solids.Count == 0) Fail("the hull has no solid (Default layer) colliders");
            int registered = climbs.Count(c => Climbables.TryGet(c, out var climb) && climb.Body == map.Blocks[0]);
            if (registered != climbs.Count) Fail($"only {registered}/{climbs.Count} climbable (Surface layer) colliders are registered");
            if (failures == 0) Ok($"{solids.Count} solid colliders, {climbs.Count} climbable, nothing Static, one body");

            // 2. The cargo deck: flat, at the marked height, hatch where it says.
            var center = map.CargoCenter; var rotation = map.CargoRotation;
            var up = rotation * Vector3.up;
            float tilt = Vector3.Angle(up, Vector3.up);
            if (tilt > MaxTilt) Fail($"cargo area marker is tilted {tilt:0.0} deg at rest (its up arrow must point up)");
            var half = ship.cargoHalfSize;
            int mask = ~((1 << Layers.Block) | (1 << Layers.Survivor) | (1 << Layers.Surface));
            int samples = 0, missing = 0, bumpy = 0, sloped = 0, foreign = 0;
            var bad = new List<string>();
            for (float x = -half.x + .25f; x <= half.x - .25f + .001f; x += .5f)
                for (float z = -half.y + .25f; z <= half.y - .25f + .001f; z += .5f)
                {
                    bool inHatch = ship.hatchHeight > 0 && Mathf.Abs(x) < ship.hatchHalf && Mathf.Abs(z) < ship.hatchHalf;
                    bool hatchEdge = ship.hatchHeight > 0 && Mathf.Abs(Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) - ship.hatchHalf) < .2f && Mathf.Min(Mathf.Abs(x), Mathf.Abs(z)) < ship.hatchHalf + .2f;
                    if (hatchEdge) continue;
                    samples++;
                    float expected = inHatch ? ship.hatchHeight : 0;
                    var from = center + rotation * new Vector3(x, ship.headroom + 1, z);
                    if (!Physics.Raycast(from, -up, out var hit, ship.headroom + 3, mask, QueryTriggerInteraction.Ignore)) { missing++; Note(bad, $"no floor at ({x:0.0}, {z:0.0})"); continue; }
                    if (!hit.collider.transform.IsChildOf(hull)) { foreign++; Note(bad, $"'{hit.collider.name}' (not part of the ship) at ({x:0.0}, {z:0.0})"); continue; }
                    float height = Vector3.Dot(hit.point - center, up);
                    if (Mathf.Abs(height - expected) > Flat) { bumpy++; Note(bad, $"deck at ({x:0.0}, {z:0.0}) is {height:0.00} m, expected {expected:0.00} m ('{hit.collider.name}')"); continue; }
                    if (Vector3.Angle(hit.normal, up) > MaxTilt) { sloped++; Note(bad, $"deck at ({x:0.0}, {z:0.0}) slopes {Vector3.Angle(hit.normal, up):0} deg"); }
                }
            int deckProblems = missing + bumpy + sloped + foreign;
            if (deckProblems > 0) { Fail($"cargo deck not flat: {missing} holes, {bumpy} wrong heights or bumps, {sloped} sloped, {foreign} non-ship surfaces (of {samples} samples)"); foreach (var b in bad) report.AppendLine("       " + b); }
            else Ok($"cargo deck {half.x * 2:0.0} x {half.y * 2:0.0} m is flat at its marker ({samples} samples){(ship.hatchHeight > 0 ? $", hatch {ship.hatchHeight:0.0} m" : "")}, {Vector3.Dot(center - map.transform.position, Vector3.up) - map.WaterHeight(center):0.0} m above the water");

            // 3. Empty: nothing of the ship sticks into the space above the cargo deck (except the hatch).
            var hits = Physics.OverlapBox(center + up * (ship.headroom * .5f + .06f), new Vector3(half.x - .02f, ship.headroom * .5f - .05f, half.y - .02f), rotation, mask, QueryTriggerInteraction.Ignore);
            var intruders = hits.Where(c => c.transform.IsChildOf(hull) && !IsHatch(c, center, rotation, ship)).Select(c => c.name).Distinct().ToList();
            if (intruders.Count > 0) Fail($"ship parts inside the cargo space ({half.x * 2:0.0} x {half.y * 2:0.0} x {ship.headroom:0} m high): {string.Join(", ", intruders.Take(8))}");
            else Ok($"cargo space is empty up to {ship.headroom:0} m");
            if (ship.hatchHeight > 0 && Mathf.Abs(ship.hatchHeight - DeckShipCargo.HatchHeight) > .01f) Warn($"hatch is {ship.hatchHeight:0.00} m; Tower and Forts' bridge planks are sized for {DeckShipCargo.HatchHeight} m");

            // 4. Every cargo layout fits inside the cargo deck and its headroom.
            for (int k = 0; k < DeckShipCargo.Count; k++)
            {
                var pieces = DeckShipCargo.Build((DeckShipCargoKind)k, 1, ship.hatchHeight);
                float maxX = 0, maxZ = 0, maxY = 0;
                foreach (var p in pieces)
                {
                    var r = Quaternion.Euler(0, p.yaw, 0);
                    var ex = new Vector3(Mathf.Abs((r * Vector3.right).x) * p.size.x + Mathf.Abs((r * Vector3.forward).x) * p.size.z, p.size.y, Mathf.Abs((r * Vector3.right).z) * p.size.x + Mathf.Abs((r * Vector3.forward).z) * p.size.z) * .5f;
                    maxX = Mathf.Max(maxX, Mathf.Abs(p.position.x) + ex.x); maxZ = Mathf.Max(maxZ, Mathf.Abs(p.position.z) + ex.z); maxY = Mathf.Max(maxY, p.position.y + ex.y);
                }
                string name = DeckShipCargo.Titles[k];
                if (maxX > half.x + .01f || maxZ > half.y + .01f) Fail($"{name} needs {maxX * 2:0.0} x {maxZ * 2:0.0} m but the cargo deck is {half.x * 2:0.0} x {half.y * 2:0.0} m");
                else if (maxY + map.dropHeight > ship.headroom) Warn($"{name} is {maxY:0.0} m tall; dropped in from {map.dropHeight} m it needs more than the {ship.headroom:0} m headroom");
                else Ok($"{name} fits ({maxX * 2:0.0} x {maxZ * 2:0.0} m, {maxY:0.0} m tall)");
            }

            // 5. Spawns stand on flat ship floor with room for a player, and not on the cargo deck.
            var spawns = (ship.spawnPoints ?? new Transform[0]).Where(s => s).ToList();
            if (spawns.Count == 0) Fail("no spawn points");
            int badSpawns = 0;
            var probe = new Collider[8];
            foreach (var s in spawns)
            {
                var p = s.position;
                bool floor = Physics.Raycast(p + Vector3.up * .5f, Vector3.down, out var hit, .8f, mask, QueryTriggerInteraction.Ignore) && hit.normal.y > .95f && hit.collider.transform.IsChildOf(hull);
                bool room = Physics.OverlapCapsuleNonAlloc(p + Vector3.up * .25f, p + Vector3.up * .8f, .2f, probe, mask, QueryTriggerInteraction.Ignore) == 0;
                var local = Quaternion.Inverse(rotation) * (p - center);
                bool onCargo = Mathf.Abs(local.x) < half.x && Mathf.Abs(local.z) < half.y && local.y < ship.headroom;
                if (!floor) { Fail($"spawn '{s.name}' has no flat ship floor under it"); badSpawns++; }
                else if (!room) { Fail($"spawn '{s.name}' is inside something"); badSpawns++; }
                if (onCargo) Warn($"spawn '{s.name}' is on the cargo deck; dropped cargo can land on players there");
            }
            if (spawns.Count > 0 && badSpawns == 0) Ok($"{spawns.Count} spawns on flat floor with room");

            // 6. Ship events really move the whole build (needs scripted physics: Physics.simulationMode = Script).
            if (Physics.simulationMode != SimulationMode.Script) { Warn("motion not checked (run with Physics.simulationMode = Script)"); return failures; }
            var parts = hull.GetComponentsInChildren<Transform>(true).Where(t => t != hull).ToList();
            var partLocal = parts.Select(t => (hull.InverseTransformPoint(t.position), Quaternion.Inverse(hull.rotation) * t.rotation)).ToList();
            bool auto = map.autoEvents; map.autoEvents = false;
            try
            {
                foreach (var (ev, seconds) in new[] { (DeckShipEvent.Roll, DeckShipMap.RollSeconds), (DeckShipEvent.Pitch, DeckShipMap.PitchSeconds), (DeckShipEvent.Iceberg, DeckShipMap.IcebergSeconds) })
                {
                    map.StartEvent(ev);
                    float commanded = 0, reached = 0, lowestCorner = float.MaxValue;
                    for (float t = 0; t < seconds; t += .05f)
                    {
                        map.Step(.05f); Physics.Simulate(.05f);
                        commanded = Mathf.Max(commanded, Mathf.Sqrt(map.RollAngle * map.RollAngle + map.PitchAngle * map.PitchAngle));
                        reached = Mathf.Max(reached, Vector3.Angle(hull.up, map.transform.up));
                        var c = map.CargoCenter; var r = map.CargoRotation;
                        for (int i = 0; i < 4; i++)
                        {
                            var corner = c + r * new Vector3((i & 1) == 0 ? -half.x : half.x, 0, (i & 2) == 0 ? -half.y : half.y);
                            lowestCorner = Mathf.Min(lowestCorner, corner.y - map.WaterHeight(corner));
                        }
                    }
                    if (reached < commanded * .8f) Fail($"{ev}: the hull only tilted {reached:0.0} of {commanded:0.0} deg");
                    else Ok($"{ev}: hull tilts {reached:0.0} deg; lowest cargo-deck corner {lowestCorner:0.0} m above the water");
                    if (lowestCorner < 0) Warn($"{ev}: a cargo-deck corner dips {-lowestCorner:0.0} m under the water");
                }
                int left = 0;
                for (int i = 0; i < parts.Count; i++)
                    if (parts[i] && (Vector3.Distance(hull.InverseTransformPoint(parts[i].position), partLocal[i].Item1) > .01f)) left++;
                if (left > 0) Fail($"{left} hull parts did not move with the hull");
            }
            finally { map.autoEvents = auto; map.StartEvent(DeckShipEvent.Righting); }
            return failures;
        }

        static void Note(List<string> list, string text) { if (list.Count < 8) list.Add(text); }

        // A collider belongs to the hatch if it lies within the hatch square and below its top.
        // Box colliders use their real corners (a world-aligned box around a tilted part is too big).
        static bool IsHatch(Collider c, Vector3 center, Quaternion rotation, DeckShipHull ship)
        {
            if (ship.hatchHeight <= 0) return false;
            var b = c.bounds; var box = c as BoxCollider;
            for (int i = 0; i < 8; i++)
            {
                var sign = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                var corner = box ? box.transform.TransformPoint(box.center + Vector3.Scale(box.size * .5f, sign))
                                 : new Vector3(sign.x < 0 ? b.min.x : b.max.x, sign.y < 0 ? b.min.y : b.max.y, sign.z < 0 ? b.min.z : b.max.z);
                var local = Quaternion.Inverse(rotation) * (corner - center);
                if (local.y > ship.hatchHeight + .05f) return false;
                if (local.y > .05f && (Mathf.Abs(local.x) > ship.hatchHalf + .25f || Mathf.Abs(local.z) > ship.hatchHalf + .25f)) return false;
            }
            return true;
        }
    }
}
