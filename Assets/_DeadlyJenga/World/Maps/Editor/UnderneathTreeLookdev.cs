using System.Collections.Generic;
using System.IO;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype.Editor
{
    // Art tool: opens Underneath the Tree in a solo demo and saves pictures from fixed cameras,
    // so each change can be compared with the approved picture straight away.
    public static class UnderneathTreeLookdev
    {
        const string Pending = "UnderneathTreeLookdev.Pending";
        const string MapId = "workshop-under-the-tree";

        // The approved picture looks north across the rug from outside the south wall.
        public static readonly (string name, Vector3 eye, Vector3 target, float fov)[] Views =
        {
            ("hero", new Vector3(-77.2f, 105.8f, -367.3f), new Vector3(-77.2f, .5f, -43.1f), 26.2f),
            ("overhead", new Vector3(-74f, 170f, -100f), new Vector3(-74f, 0f, -99f), 80f),
            ("turned-around", new Vector3(-60f, 70f, 60f), new Vector3(-80f, 15f, -150f), 62f),
            ("from-window", new Vector3(70f, 60f, -120f), new Vector3(-90f, 15f, -105f), 62f),
            ("from-sofa", new Vector3(-235f, 55f, -110f), new Vector3(-74f, 15f, -110f), 62f),
            ("player-courtyard", new Vector3(-81.6f, 3f, -153.7f), new Vector3(-112f, 19.5f, -110f), 75f),
            ("player-front", new Vector3(-93f, 3f, -194.5f), new Vector3(-74f, 15.7f, -129f), 70f),
            ("player-bridge", new Vector3(-89.2f, 25.6f, -115.7f), new Vector3(-150f, 36f, -115.7f), 75f),
            ("player-tunnel", new Vector3(-131f, 15.4f, -149.9f), new Vector3(-151.9f, 15.7f, -119.5f), 70f),
            ("player-back", new Vector3(-89.2f, 3f, -15f), new Vector3(-74f, 15.7f, -91f), 72f),
            ("player-left", new Vector3(-188f, 3f, -110f), new Vector3(-153.8f, 27f, -110f), 72f),
            ("player-right", new Vector3(40f, 3f, -117.6f), new Vector3(-24.6f, 23.3f, -92.9f), 72f)
        };

        [MenuItem("Tools/Deadly Jenga/Underneath the Tree/Open Lookdev")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            SessionState.SetBool(Pending, true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SelectMap()
        {
            if (!SessionState.GetBool(Pending, false))
                return;
            SessionState.SetBool(Pending, false);
            MatchChoices.MapId = MapId;
            MatchChoices.ModeId = "demo";
            MatchChoices.BotTarget = 0;
            MatchChoices.HandCount = 0;
        }

        // Family room views, for the unattended capture job below.
        public static readonly (string name, Vector3 eye, Vector3 target, float fov)[] FamilyRoomViews =
        {
            ("room-tree-corner", new Vector3(-250f, 90f, -300f), new Vector3(-40f, 120f, -30f), 70f),
            ("room-sitting", new Vector3(-40f, 120f, -350f), new Vector3(-300f, 30f, -350f), 72f),
            ("room-stockings", new Vector3(-210f, 85f, -350f), new Vector3(-310f, 82f, -350f), 45f),
            ("room-kitchen", new Vector3(-200f, 80f, -110f), new Vector3(-540f, 60f, -110f), 55f),
            ("room-front-entry", new Vector3(-200f, 90f, -540f), new Vector3(-540f, 70f, -545f), 60f),
            ("room-piano-clock", new Vector3(-210f, 80f, -200f), new Vector3(-320f, 75f, 20f), 60f),
            ("room-photos", new Vector3(-200f, 140f, -340f), new Vector3(58f, 140f, -340f), 60f),
            ("room-dog-bed", new Vector3(-180f, 75f, -250f), new Vector3(-25f, 25f, -348f), 55f),
            ("room-dog-face", new Vector3(-115f, 45f, -175f), new Vector3(-44f, 32f, -258f), 45f),
            ("room-south-window", new Vector3(-145f, 80f, -330f), new Vector3(-145f, 95f, -600f), 72f),
            ("room-overview", new Vector3(30f, 225f, -585f), new Vector3(-250f, 40f, -100f), 85f)
        };

        const string BatchKey = "UnderneathTreeLookdev.Batch";
        const string BatchStarted = "UnderneathTreeLookdev.BatchStarted";
        const string BatchPlaying = "UnderneathTreeLookdev.BatchPlaying";
        public const string BatchFolder = "Logs/TreeLookdev/batch";

        // Unattended capture for a coordinator job (-executeMethod): opens the map in Play Mode,
        // waits for it to build, saves every family room view to Logs/TreeLookdev/batch, then quits.
        // Exit code 0 = pictures written; 2 = timed out.
        public static void BatchCaptureFamilyRoom() => StartBatch("room");

        // Same, but from where a player's eyes really are: at spawn, around the playable area
        // looking in and out, and on top of the structure. Pictures go to Logs/TreeLookdev/pov.
        public static void BatchCapturePlayerViews() => StartBatch("pov");
        public static void BatchCaptureTrain() => StartBatch("train");
        public static void BatchCaptureCarpet() => StartBatch("carpet");

        const string BatchMode = "UnderneathTreeLookdev.BatchMode";
        public const string PovFolder = "Logs/TreeLookdev/pov";
        public const string TrainFolder = "Logs/TreeLookdev/train";
        public const string CarpetFolder = "Logs/TreeLookdev/carpet";

        static void StartBatch(string mode)
        {
            SessionState.SetString(BatchMode, mode);
            SessionState.SetBool(BatchKey, true);
            SessionState.SetFloat(BatchStarted, (float)EditorApplication.timeSinceStartup);
            SessionState.SetFloat(BatchPlaying, 0f);
            EditorApplication.update += BatchStep;
            Open();
        }

        // Entering Play Mode reloads scripts, so the wait picks up again afterwards.
        [InitializeOnLoadMethod]
        static void ResumeBatch()
        {
            if (SessionState.GetBool(BatchKey, false))
                EditorApplication.update += BatchStep;
        }

        static void BatchStep()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - SessionState.GetFloat(BatchStarted, (float)now) > 900f)
            {
                FinishBatch(2);
                return;
            }
            if (!EditorApplication.isPlaying || !(Services.Session?.Player?.ViewCamera))
                return;
            float playing = SessionState.GetFloat(BatchPlaying, 0f);
            if (playing <= 0f)
            {
                SessionState.SetFloat(BatchPlaying, (float)now);
                return;
            }
            // Give the map, its art and the fire a few seconds to build and settle.
            if (now - playing < 15f)
                return;
            if (SessionState.GetString(BatchMode, "room") == "train")
            {
                ShootTrainViews(TrainFolder);
                FinishBatch(0);
                return;
            }
            if (SessionState.GetString(BatchMode, "room") == "carpet")
            {
                ShootCarpetViews(CarpetFolder);
                FinishBatch(0);
                return;
            }
            if (SessionState.GetString(BatchMode, "room") == "pov")
            {
                ShootPlayerViews(PovFolder);
                FinishBatch(0);
                return;
            }
            Directory.CreateDirectory(BatchFolder);
            foreach (var view in FamilyRoomViews)
                Shoot(Path.Combine(BatchFolder, view.name + ".png"), view.eye, view.target, view.fov);
            File.WriteAllText(Path.Combine(BatchFolder, "done.txt"), System.DateTime.Now.ToString("s"));
            FinishBatch(0);
        }

        static readonly string[] Compass = { "n", "ne", "e", "se", "s", "sw", "w", "nw" };

        // Places a player can stand on, found by name in the built map. Missing ones are skipped
        // (views.txt says which).
        static readonly (string find, string label, bool allWays)[] Lookouts =
        {
            ("Wizard Cap", "wizard-tower", true),
            ("Sapphire Crown", "right-summit", true),
            ("Ruby Perch", "ruby-perch", false),
            ("Navy Span", "gift-bridge", false),
            ("Mansion Deck", "mansion-deck", false),
            ("Hero Deck", "hero-deck", false),
            ("Ivory Perch", "ivory-perch", false),
            ("High Ruby", "left-stair-top", false),
            ("Rail Gift 8", "train-track", false),
            ("Back Row / Gift 3", "back-row", false),
            ("Green Hop", "courtyard-hop", false)
        };

        // Player eye-level pictures. Positions come from the running map (playable area, spawn,
        // real eye height, named lookouts, the tallest presents), so they follow layout changes.
        // views.txt lists every camera.
        static void ShootPlayerViews(string folder)
        {
            Directory.CreateDirectory(folder);
            var player = Services.Session.Player;
            var cam = player.ViewCamera;
            var world = Services.World;
            float eye = Mathf.Clamp(cam.transform.position.y - player.transform.position.y, .3f, 30f);
            const float fov = 62f, wide = 80f;
            var log = new System.Text.StringBuilder();
            var bounds = world.PlayableBounds;
            var center = world.Center;
            float radius = world.Radius > 0f ? world.Radius : Mathf.Max(bounds.extents.x, bounds.extents.z);
            float topY = Mathf.Max(world.TopHeight, center.y + 1f);
            float skyY = bounds.max.y + 1f;
            log.AppendLine($"eye {eye:F2} center {center} radius {radius:F1} top {world.TopHeight:F1} bounds {bounds}");

            // Menus float in front of the spawn point; hide them while shooting.
            var hidden = new List<Behaviour>();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.enabled)
                {
                    canvas.enabled = false;
                    hidden.Add(canvas);
                }
            var mapCenter = new Vector3(center.x, Mathf.Lerp(center.y, topY, .2f), center.z);
            var self = player.transform;

            void Save(string name, Vector3 at, Vector3 target, float angle = fov)
            {
                Shoot(Path.Combine(folder, name + ".png"), at, target, angle);
                log.AppendLine($"{name}: eye {at} looking at {target}");
            }

            // Stand on top of whatever is here; nudge up if the eye would be inside something.
            Vector3 StandOn(Vector3 spot, float from)
            {
                var at = Ground(spot, from, self) + Vector3.up * eye;
                for (int i = 0; i < 6 && Physics.CheckSphere(at, .25f, ~0, QueryTriggerInteraction.Ignore); i++)
                    at += Vector3.up * .5f;
                return at;
            }

            // Looking at the whole map from a high spot, and away from it at the room.
            void Lookout(string label, Vector3 at, bool allWays)
            {
                var toMap = mapCenter - at;
                var level = Flat(toMap);
                if (new Vector2(toMap.x, toMap.z).magnitude < 12f)
                    level = Flat(at - new Vector3(bounds.min.x, 0f, bounds.min.z));
                // Look across the map, tilted down just enough to see the ground near the middle.
                float drop = Mathf.Clamp(at.y - mapCenter.y, 0f, 60f);
                Save($"pov-{label}-overlook", at, at + level * 40f - Vector3.up * drop * .35f, wide);
                Save($"pov-{label}-room", at, at - level * 40f + Vector3.up * 6f, wide);
                if (!allWays)
                    return;
                var side = Vector3.Cross(Vector3.up, level);
                Save($"pov-{label}-left", at, at + side * 40f - Vector3.up * 4f, wide);
                Save($"pov-{label}-right", at, at - side * 40f - Vector3.up * 4f, wide);
                Save($"pov-{label}-up", at, at + level * 20f + Vector3.up * 30f, wide);
            }

            try
            {
                // 1. Where the player starts, turning all the way around.
                var spawn = self.position + Vector3.up * eye;
                var ahead = Flat(cam.transform.forward);
                string[] turns = { "ahead", "right", "behind", "left" };
                for (int i = 0; i < 4; i++)
                    Save("pov-spawn-" + turns[i], spawn, spawn + Quaternion.Euler(0f, 90f * i, 0f) * ahead * 10f, wide);

                // 2. Around the edge of the playable area on the floor, looking in at the gifts.
                //    A spot on top of or against a present moves outwards until it is clear.
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * 45f * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                    Vector3 at = default;
                    for (float r = radius * .95f; r < radius * 1.6f; r += 6f)
                    {
                        at = StandOn(center + dir * r, skyY);
                        bool onFloor = at.y - center.y < eye + 1.5f;
                        bool clear = !Physics.Raycast(at, -dir, 5f, ~0, QueryTriggerInteraction.Ignore);
                        if (onFloor && clear)
                            break;
                    }
                    Save("pov-floor-" + Compass[i], at, new Vector3(center.x, Mathf.Lerp(at.y, topY, .3f), center.z), wide);
                    if (i % 4 == 0)
                        Save("pov-floor-" + Compass[i] + "-up", at, at - dir * 10f + Vector3.up * 8f, wide);
                }

                // 3. Named lookouts: the wizard tower, summits, decks, the bridge, the track.
                var all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
                var used = new List<Vector3>();
                foreach (var (find, label, allWays) in Lookouts)
                {
                    Transform found = null;
                    foreach (var t in all)
                        if (t.name.Contains(find) && t.GetComponentInChildren<Collider>())
                        {
                            found = t;
                            break;
                        }
                    if (!found)
                    {
                        log.AppendLine($"{label}: no object named like '{find}'");
                        continue;
                    }
                    var b = ColliderBounds(found);
                    var at = StandOn(b.center, b.max.y + .5f);
                    used.Add(at);
                    log.AppendLine($"{label}: standing on {found.name}");
                    Lookout(label, at, allWays);
                }

                // 4. The tallest presents that are not near a lookout already used.
                var tops = new List<UnityEngine.Bounds>();
                foreach (var block in world.Blocks)
                    if (block)
                        tops.Add(ColliderBounds(block.transform));
                tops.Sort((x, y) => y.max.y.CompareTo(x.max.y));
                int count = 0;
                foreach (var b in tops)
                {
                    if (count >= 6)
                        break;
                    var at = StandOn(b.center, b.max.y + .5f);
                    bool near = false;
                    foreach (var u in used)
                        near |= (u - at).sqrMagnitude < 25f * 25f;
                    if (near)
                        continue;
                    used.Add(at);
                    Lookout("present-" + (++count), at, false);
                }
            }
            finally
            {
                foreach (var canvas in hidden)
                    if (canvas)
                        canvas.enabled = true;
            }

            File.WriteAllText(Path.Combine(folder, "views.txt"), log.ToString());
            File.WriteAllText(Path.Combine(folder, "done.txt"), System.DateTime.Now.ToString("s"));
        }

        // The toy train and its coaches from the side, front, back and above.
        static void ShootTrainViews(string folder)
        {
            Directory.CreateDirectory(folder);
            var log = new System.Text.StringBuilder();
            var engine = Object.FindAnyObjectByType<UnderneathTreeTrain>();
            if (!engine)
            {
                File.WriteAllText(Path.Combine(folder, "views.txt"), "no train found");
                File.WriteAllText(Path.Combine(folder, "done.txt"), System.DateTime.Now.ToString("s"));
                return;
            }
            var hidden = new List<Behaviour>();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.enabled)
                {
                    canvas.enabled = false;
                    hidden.Add(canvas);
                }
            try
            {
                // Whole train: the engine plus every "Train Car" next to it.
                var whole = new UnityEngine.Bounds(engine.transform.position, Vector3.zero);
                foreach (var r in engine.transform.parent.GetComponentsInChildren<Renderer>())
                    if (r.transform.IsChildOf(engine.transform) || (r.transform.parent && r.transform.parent.parent && r.transform.parent.parent.name.StartsWith("Train Car")))
                        whole.Encapsulate(r.bounds);
                var ahead = engine.transform.forward;
                var side = engine.transform.right;
                float reach = Mathf.Max(whole.extents.x, whole.extents.z);
                var middle = whole.center;
                var nose = engine.transform.position;
                void Save(string name, Vector3 at, Vector3 target, float angle)
                {
                    Shoot(Path.Combine(folder, name + ".png"), at, target, angle);
                    log.AppendLine($"{name}: eye {at} looking at {target}");
                }
                log.AppendLine($"train bounds {whole}");
                // What each train part is drawn with, to compare the engine and the cars.
                foreach (var r in engine.transform.parent.GetComponentsInChildren<MeshRenderer>())
                {
                    bool car = r.transform.parent && r.transform.parent.parent && r.transform.parent.parent.name.StartsWith("Train Car");
                    if (!car && !r.transform.IsChildOf(engine.transform))
                        continue;
                    var m = r.GetComponent<MeshFilter>() ? r.GetComponent<MeshFilter>().sharedMesh : null;
                    var mat = r.sharedMaterial;
                    log.AppendLine($"part {r.transform.parent.name}/{r.name} mat={(mat ? mat.name : "-")} shader={(mat ? mat.shader.name : "-")} " +
                        $"color={(mat && mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor").ToString() : "-")} " +
                        $"verts={(m ? m.vertexCount : 0)} normal={(m && m.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Normal))} " +
                        $"color0={(m && m.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color))} uv1={(m && m.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord1))} " +
                        $"layer={r.gameObject.layer} rlm={r.renderingLayerMask} probes={r.lightProbeUsage} shadows={r.shadowCastingMode}/{r.receiveShadows} enabled={r.enabled}");
                }
                Save("train-side-right", middle + side * reach * 1.3f + Vector3.up * reach * .25f, middle, 55f);
                Save("train-side-left", middle - side * reach * 1.3f + Vector3.up * reach * .25f, middle, 55f);
                Save("train-front", nose + ahead * reach * .7f + side * reach * .35f + Vector3.up * reach * .15f, middle, 60f);
                Save("train-back", middle - ahead * reach * 1.4f - side * reach * .3f + Vector3.up * reach * .2f, middle, 55f);
                Save("train-above", middle + Vector3.up * reach * 1.6f + side * reach * .5f, middle, 60f);
                Save("train-player-eye", middle + side * reach * .6f + Vector3.up * 1.2f, middle, 75f);
            }
            finally
            {
                foreach (var canvas in hidden)
                    if (canvas)
                        canvas.enabled = true;
            }
            File.WriteAllText(Path.Combine(folder, "views.txt"), log.ToString());
            File.WriteAllText(Path.Combine(folder, "done.txt"), System.DateTime.Now.ToString("s"));
        }

        // Carpet close-ups: the sinking swirls (found by their "Carpet swirl" markers), the yarn
        // loops next to the gifts, and the knitted border, at real eye height and from above.
        static void ShootCarpetViews(string folder)
        {
            Directory.CreateDirectory(folder);
            var log = new System.Text.StringBuilder();
            var player = Services.Session.Player;
            float eye = Mathf.Clamp(player.ViewCamera.transform.position.y - player.transform.position.y, .3f, 30f);
            var hidden = new List<Behaviour>();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.enabled)
                {
                    canvas.enabled = false;
                    hidden.Add(canvas);
                }
            try
            {
                void Save(string name, Vector3 at, Vector3 target, float angle)
                {
                    Shoot(Path.Combine(folder, name + ".png"), at, target, angle);
                    log.AppendLine($"{name}: eye {at} looking at {target}");
                }
                var center = Services.World.Center;
                var swirls = new List<Transform>();
                var loops = new List<Renderer>();
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (t.name.StartsWith("Carpet swirl"))
                        swirls.Add(t);
                    else if (t.name == "Carpet yarn loops" && t.TryGetComponent<Renderer>(out var r))
                        loops.Add(r);
                }
                swirls.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                log.AppendLine($"eye {eye:F2} swirls {swirls.Count} loop patches {loops.Count}");
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                    if (r.name.Contains("kill floor"))
                        log.AppendLine($"rug {r.name} mat={(r.sharedMaterial ? r.sharedMaterial.name : "-")} shader={(r.sharedMaterial ? r.sharedMaterial.shader.name : "-")} supported={(r.sharedMaterial && r.sharedMaterial.shader.isSupported)} enabled={r.enabled}");
                for (int i = 0; i < Mathf.Min(2, swirls.Count); i++)
                {
                    var s = swirls[i].position;
                    var toward = Flat(center - s);
                    Save($"carpet-swirl{i + 1}-eye", s - toward * 5f + Vector3.up * eye, s, 70f);
                    Save($"carpet-swirl{i + 1}-close", s - toward * 2.2f + Vector3.up * 1.6f, s, 65f);
                    Save($"carpet-swirl{i + 1}-above", s - toward * 3f + Vector3.up * 7f, s, 60f);
                }
                // Yarn loops: the patch nearest the course centre, looking from the open side.
                loops.Sort((a, b) => (a.bounds.center - center).sqrMagnitude.CompareTo((b.bounds.center - center).sqrMagnitude));
                for (int i = 0; i < Mathf.Min(2, loops.Count); i++)
                {
                    var c = loops[i].bounds.center;
                    var dir = Flat(c - center);
                    var at = new Vector3(c.x, loops[i].bounds.min.y, c.z) + dir * 3f;
                    Save($"carpet-loops{i + 1}-eye", at + Vector3.up * eye, at - dir * 6f + Vector3.up * eye * .5f, 75f);
                    Save($"carpet-loops{i + 1}-down", at + Vector3.up * 2f, at - dir * 2.5f, 70f);
                }
                // The knitted border near the rug edge, and an overlook from a tall present.
                var rugCentre = new Vector3(-74f, 0f, -77.7f);
                var edge = rugCentre + new Vector3(0f, 0f, -1f) * 125.4f * .9f;
                Save("carpet-border-eye", edge + new Vector3(-6f, eye * 2f, 4f), edge + new Vector3(8f, 0f, -3f), 75f);
                Save("carpet-border-above", edge + new Vector3(0f, 14f, 12f), edge, 60f);
                Save("carpet-overlook", center + new Vector3(30f, 45f, -95f), center + new Vector3(0f, 0f, -30f), 70f);
                var floor = new Vector3(center.x, 0f, center.z + 110f);
                Save("carpet-floor-low", floor + Vector3.up * eye, floor + new Vector3(0f, eye * .4f, -20f), 80f);
            }
            finally
            {
                foreach (var canvas in hidden)
                    if (canvas)
                        canvas.enabled = true;
            }
            File.WriteAllText(Path.Combine(folder, "views.txt"), log.ToString());
            File.WriteAllText(Path.Combine(folder, "done.txt"), System.DateTime.Now.ToString("s"));
        }

        static UnityEngine.Bounds ColliderBounds(Transform root)
        {
            var colliders = root.GetComponentsInChildren<Collider>();
            if (colliders.Length == 0)
                return new UnityEngine.Bounds(root.position, Vector3.zero);
            var b = colliders[0].bounds;
            foreach (var c in colliders)
                b.Encapsulate(c.bounds);
            return b;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        // The highest solid surface under this spot, skipping the player's own body.
        static Vector3 Ground(Vector3 at, float fromHeight, Transform skip)
        {
            var start = new Vector3(at.x, fromHeight, at.z);
            float best = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(start, Vector3.down, fromHeight + 500f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(skip) && hit.point.y > best)
                    best = hit.point.y;
            return new Vector3(at.x, float.IsNegativeInfinity(best) ? at.y : best, at.z);
        }

        static void FinishBatch(int code)
        {
            EditorApplication.update -= BatchStep;
            SessionState.EraseBool(BatchKey);
            EditorApplication.Exit(code);
        }

        // Saves every fixed view into the folder. Returns how many pictures were written.
        public static int ShootAll(string folder, int width = 1600, int height = 900)
        {
            foreach (var view in Views)
                Shoot(Path.Combine(folder, view.name + ".png"), view.eye, view.target, view.fov, width, height);
            return Views.Length;
        }

        public static string Shoot(string path, Vector3 eye, Vector3 target, float fov, int width = 1600, int height = 900)
        {
            var camera = Services.Session?.Player?.ViewCamera;
            if (!camera)
                return "No player camera yet.";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var priorTexture = camera.targetTexture;
            var priorActive = RenderTexture.active;
            var priorPosition = camera.transform.position;
            var priorRotation = camera.transform.rotation;
            float priorFov = camera.fieldOfView, priorAspect = camera.aspect, priorNear = camera.nearClipPlane, priorFar = camera.farClipPlane;
            bool priorAsync = ShaderUtil.allowAsyncCompilation;
            var render = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.Default, RenderTextureReadWrite.Default, 4);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
                camera.targetTexture = render;
                camera.aspect = (float)width / height;
                camera.fieldOfView = fov;
                // A camera outside the room starts drawing past the south wall. The family room's
                // south wall is at z -600 (it was -262 when the room was small).
                camera.nearClipPlane = eye.z < -602f ? (-590f - eye.z) * 1.06f : .05f;
                camera.farClipPlane = 1500f;
                ShaderUtil.allowAsyncCompilation = false;
                camera.Render();
                camera.Render();
                RenderTexture.active = render;
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = priorAsync;
                camera.targetTexture = priorTexture;
                RenderTexture.active = priorActive;
                camera.transform.SetPositionAndRotation(priorPosition, priorRotation);
                camera.fieldOfView = priorFov;
                camera.aspect = priorAspect;
                camera.nearClipPlane = priorNear;
                camera.farClipPlane = priorFar;
                RenderTexture.ReleaseTemporary(render);
                Object.DestroyImmediate(image);
            }
            return path;
        }
    }
}
