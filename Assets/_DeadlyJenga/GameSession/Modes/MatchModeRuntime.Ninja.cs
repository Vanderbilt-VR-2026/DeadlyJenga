using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        readonly List<Vector3> lanternPerches = new List<Vector3>();
        readonly RaycastHit[] lanternHits = new RaycastHit[8];
        System.Random lanternRandom = new System.Random(4271);
        bool lanternOpening = true;

        bool GongSurge => Authority ? simulation != null && simulation.GongActive : frame.lantern != null && frame.lantern.gongRemaining > 0;

        void TickLanternWorld()
        {
            if (!Authority || simulation == null || Rules.mode != GameModeKind.Ninja) return;
            if (simulation.Phase != RoundPhase.Playing && simulation.Phase != RoundPhase.Countdown) { lanternOpening = true; return; }
            var world = Services.World; if (world == null) return;
            if (simulation.LanternNeedsPerch) { simulation.PerchLantern(LanternPerch(simulation.Lantern.position, lanternOpening)); lanternOpening = false; return; }
            var l = simulation.Lantern; if (!string.IsNullOrEmpty(l.carrier)) return;
            var p = l.position; var bounds = world.PlayableBounds;
            p.x = Mathf.Clamp(p.x, bounds.min.x + .5f, bounds.max.x - .5f); p.z = Mathf.Clamp(p.z, bounds.min.z + .5f, bounds.max.z - .5f);
            int mask = (1 << Layers.Block) | (1 << Layers.Surface) | 1;
            if (Physics.Raycast(p + Vector3.up * .2f, Vector3.down, out var hit, 200, mask, QueryTriggerInteraction.Ignore) && hit.point.y > world.WaterHeight(hit.point) + .1f)
            { float floor = hit.point.y + .75f; p.y = p.y > floor ? Mathf.Max(floor, p.y - 2.2f) : floor; simulation.SettleLantern(p); }
            else simulation.PerchLantern(LanternPerch(p, false));
        }

        // Courtyard spots and wide, exposed rooftops. The opening perch is on the ground; later perches flee far away.
        Vector3 LanternPerch(Vector3 from, bool opening)
        {
            var world = Services.World; lanternPerches.Clear();
            float top = world.TopHeight, water = world.WaterHeight(world.Center); var bounds = world.PlayableBounds;
            int ground = 0;
            for (int i = 0; i < 24; i++)
            {
                float a = (float)lanternRandom.NextDouble() * Mathf.PI * 2, r = opening ? .3f : .25f + (float)lanternRandom.NextDouble() * .6f;
                var probe = new Vector3(world.Center.x + Mathf.Sin(a) * bounds.extents.x * r, bounds.max.y + 5, world.Center.z + Mathf.Cos(a) * bounds.extents.z * r);
                if (!Physics.Raycast(probe, Vector3.down, out var hit, bounds.size.y + 40, (1 << Layers.Block) | (1 << Layers.Surface) | 1, QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider.gameObject.layer != 0 || hit.normal.y < .9f || hit.point.y < water + .2f || hit.point.y > bounds.min.y + 1.5f) continue;
                if (Physics.CheckCapsule(hit.point + Vector3.up * .5f, hit.point + Vector3.up * 1.8f, .6f, (1 << Layers.Block) | (1 << Layers.Surface) | 1, QueryTriggerInteraction.Ignore)) continue;
                lanternPerches.Add(hit.point + Vector3.up * .95f); ground++;
            }
            if (!opening) foreach (var body in world.Blocks)
            {
                if (!body || !body.gameObject.activeInHierarchy || body.linearVelocity.sqrMagnitude > .5f || Vector3.Dot(body.transform.up, Vector3.up) < .97f) continue;
                var collider = body.GetComponent<Collider>(); if (!collider) continue;
                var b = collider.bounds; if (b.extents.x < 1.4f || b.extents.z < 1.4f || b.max.y > bounds.min.y + 13 || b.max.y < water + 2) continue;
                var origin = new Vector3(b.center.x, b.max.y + 4, b.center.z);
                int n = Physics.RaycastNonAlloc(origin, Vector3.down, lanternHits, 6, (1 << Layers.Block) | 1, QueryTriggerInteraction.Ignore);
                float nearest = float.MaxValue; Rigidbody first = null;
                for (int i = 0; i < n; i++) if (lanternHits[i].distance < nearest) { nearest = lanternHits[i].distance; first = lanternHits[i].rigidbody; }
                if (first != body) continue;
                var edge = new Vector3(b.center.x + Mathf.Sign(b.center.x - world.Center.x + .01f) * (b.extents.x - .9f), b.max.y + .75f, b.center.z + Mathf.Sign(b.center.z - world.Center.z + .01f) * (b.extents.z - .9f));
                lanternPerches.Add(Physics.CheckCapsule(edge + Vector3.up * .2f, edge + Vector3.up * 1.2f, .4f, (1 << Layers.Block) | (1 << Layers.Surface), QueryTriggerInteraction.Ignore) ? new Vector3(b.center.x, b.max.y + .75f, b.center.z) : edge);
            }
            if (lanternPerches.Count == 0) return world.SafeSpawnPoint() + Vector3.up * .75f;
            bool high = !opening && lanternPerches.Count > ground && lanternRandom.NextDouble() < .4;
            Vector3 best = lanternPerches[0]; float bestScore = float.MinValue;
            for (int i = 0; i < lanternPerches.Count; i++)
            {
                var perch = lanternPerches[i]; if (!opening && (i >= ground) != high) continue;
                float score = opening ? -Mathf.Abs(new Vector2(perch.x - world.Center.x, perch.z - world.Center.z).magnitude - 10)
                    : -Mathf.Abs(Vector3.Distance(perch, from) - 18) + (float)lanternRandom.NextDouble() * 10;
                if (score > bestScore) { bestScore = score; best = perch; }
            }
            return best;
        }

        sealed partial class MatchBotFacts
        {
            static readonly string[] lanternQueries = { "lantern", "lantern-carrier", "lantern-escape" };
            bool LanternQuery(string name, BotDirectorActor actor, List<BotDirectorTarget> results)
            {
                if (System.Array.IndexOf(lanternQueries, name) < 0) return false;
                if (!Sim.NinjaRound) return true;
                var l = Sim.Lantern;
                if (name == "lantern") { if (string.IsNullOrEmpty(l.carrier) && !Sim.LanternNeedsPerch) Add(results, "lantern", l.position - Vector3.up * .75f, null, 0, 1.2f); return true; }
                if (name == "lantern-carrier")
                {
                    if (string.IsNullOrEmpty(l.carrier) || l.carrier == actor.id) return true;
                    var p = Sim.Find(l.carrier); if (p == null || !p.frame.alive || p.frame.dying) return true;
                    Add(results, p.frame.id, p.frame.position, owner.FindActor(p.frame.id)?.gameObject, 0, 1.1f); return true;
                }
                if (l.carrier != actor.id) return true;
                owner.RefreshSupportGoals(); Known(actor); ranked.Clear();
                foreach (var point in owner.supportCandidates)
                {
                    var delta = point - actor.position; if (delta.y > 8 || delta.y < -8 || delta.sqrMagnitude > 28 * 28 || delta.sqrMagnitude < 9) continue;
                    float safest = 40; foreach (var h in knownHostiles) safest = Mathf.Min(safest, Vector3.Distance(h.position, point));
                    Rank(new BotDirectorTarget { id = PointId(point), position = point, radius = .7f, utility = Mathf.Clamp((safest - delta.magnitude * .35f) * .25f, -9, 9) }, 4);
                }
                results.AddRange(ranked); return true;
            }
        }
    }
}
