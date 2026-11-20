using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Drives a separate, externally driven test survivor (pattern: CastleClimbChecks) along the route
    // and up a building wall. The real player stays at the spawn.
    public static partial class VanderbiltCampusPlayChecks
    {
        const float Dt = 1f / 60, WalkSpeed = 4.6f;
        const int TicksPerFrame = 8;   // simulate 8 physics-sized steps per rendered frame to keep the run short
        static GameObject staging;
        static CastleSurvivor motor;
        static int reached;
        static float walked;
        static string failedAt = "";

        static void MakeTestSurvivor(CastlePlaySession session)
        {
            staging = new GameObject("Campus check staging"); staging.SetActive(false);
            motor = Object.Instantiate(session.survivorPrefab, staging.transform).GetComponent<CastleSurvivor>();
            motor.externallyDriven = true; motor.enabled = false;
            var avatar = motor.GetComponent<CandyPlayerAvatar>(); if (avatar) avatar.remoteDriven = true;
            foreach (var camera in motor.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (var listener in motor.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            staging.SetActive(true); motor.RefreshMode();
        }

        // Route points in world space: unity_map.json "route" first, else Route_<nn> markers ordered by number.
        static List<Vector3> Route(VanderbiltCampusMap map, VanderbiltCampusData data)
        {
            if (data.route.Count > 0) return data.route.Select(p => map.transform.TransformPoint(p)).ToList();
            var number = new Regex(@"^Route_(\d+)");
            return map.GetComponentsInChildren<Transform>(true)
                .Select(t => (t, m: number.Match(t.name))).Where(x => x.m.Success)
                .OrderBy(x => int.Parse(x.m.Groups[1].Value)).Select(x => x.t.position).ToList();
        }

        static void Face(Vector3 direction)
        {
            var flat = Flat(direction);
            if (flat.sqrMagnitude > .0001f) motor.transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
        }

        // Walks waypoint to waypoint. Each leg may take 3x the time a straight walk needs (+5 s).
        // When the survivor stops making progress it jumps. Stops early once maxDistance has been covered.
        static IEnumerator Walk(VanderbiltCampusMap map, List<Vector3> route, float maxDistance)
        {
            reached = 0; walked = 0; failedAt = "";
            motor.Teleport(route[0] + Vector3.up * .08f, Quaternion.identity);
            Face(route[1] - route[0]);
            for (int i = 1; i < route.Count; i++)
            {
                var target = route[i];
                float limit = Flat(target - motor.transform.position).magnitude / WalkSpeed * 3 + 5, time = 0, stuckTime = 0;
                var last = motor.transform.position;
                while (!Reached(target))
                {
                    if (time > limit || !motor.Alive || motor.transform.position.y < map.WaterHeight(motor.transform.position))
                    {
                        failedAt = $"stopped {Flat(target - motor.transform.position).magnitude:F1} m short of waypoint {i + 1} at {motor.transform.position:F1}" +
                            (motor.Alive ? "" : " (eliminated)");
                        Shot("route-stuck", motor.transform.position, motor.transform.forward);
                        yield break;
                    }
                    for (int n = 0; n < TicksPerFrame; n++)
                    {
                        Face(target - motor.transform.position);
                        bool jump = stuckTime > .6f;
                        if (jump) stuckTime = 0;
                        motor.Simulate(Vector2.up, jump, Dt);
                        float step = Flat(motor.transform.position - last).magnitude;
                        walked += step; last = motor.transform.position;
                        stuckTime = step < WalkSpeed * Dt * .25f ? stuckTime + Dt : 0;
                        time += Dt;
                        if (walked >= maxDistance) yield break;
                    }
                    yield return null;
                }
                reached = i;
                if (i == route.Count / 2) Shot("mid-route", motor.transform.position, motor.transform.forward);
            }
        }

        // Within 2 m on the ground plane and not on a different floor.
        static bool Reached(Vector3 target) =>
            Flat(target - motor.transform.position).magnitude < 2 && Mathf.Abs(target.y - motor.transform.position.y) < 3;

        // Tries each climb anchor (unity_map.json "climb", else Climb_<id> markers) until one works:
        // walk into the wall holding climb, latch, then climb at least 0.6 m.
        static IEnumerator Climb(VanderbiltCampusMap map, VanderbiltCampusData data)
        {
            var anchors = data.climb.Select(c => (name: c.building, pos: map.transform.TransformPoint(c.pos), forward: map.transform.TransformDirection(c.forward))).ToList();
            if (anchors.Count == 0)
                anchors = map.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Climb_"))
                    .Select(t => (name: t.name, pos: t.position, forward: t.forward)).ToList();
            Require(anchors.Count > 0, anchors.Count + " climb anchors");
            foreach (var anchor in anchors)
            {
                motor.Teleport(anchor.pos + Vector3.up * .08f, Quaternion.LookRotation(Flat(anchor.forward).sqrMagnitude > .0001f ? Flat(anchor.forward).normalized : Vector3.forward));
                for (int i = 0; i < 20; i++) { motor.Simulate(Vector2.zero, false, Dt); }   // settle on the ground
                yield return null;
                bool latched = false; float startY = 0, bestY = 0;
                for (int i = 0; i < 60 && !latched; i++)
                {
                    motor.Simulate(Vector2.up, false, Dt, true);
                    if (motor.Climbing) { latched = true; startY = bestY = motor.transform.position.y; }
                }
                for (int i = 0; i < 90 && latched; i++)
                {
                    motor.Simulate(Vector2.up, false, Dt, true);
                    if (motor.Climbing) bestY = Mathf.Max(bestY, motor.transform.position.y);
                    if (i == 45) yield return null;
                }
                File.AppendAllText(Report, $"NOTE climb at {anchor.name}: latched={latched} gain={bestY - startY:F2} m\n");
                if (latched && bestY - startY > .6f)
                {
                    Shot("climb", motor.transform.position, anchor.forward);   // camera stands back from the wall
                    Require(true, "Test survivor climbed " + anchor.name + " by " + (bestY - startY).ToString("F2") + " m");
                    yield break;
                }
            }
            Require(false, "No climb anchor let the survivor latch and climb 0.6 m");
        }
    }
}
