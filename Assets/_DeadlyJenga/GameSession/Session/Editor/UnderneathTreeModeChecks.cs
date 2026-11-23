using System;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype.Editor
{
    public static class UnderneathTreeModeChecks
    {
        const string Pending = "TreeModes.Pending", Pass = "TreeModes.Pass", Index = "TreeModes.Index";
        const string MapId = "workshop-under-the-tree";
        static string Report => ProjectPath.Report("UnderneathTreeModes.txt");
        static double deadline;
        static int stage;
        static float settled;
        static float? sankAt;
        static string lastSink = "";
        static string Mode => SessionState.GetInt(Index, 0) == 0 ? "teamtickets" : "survival";
        public static void Run()
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Use a checked batch editor.");
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, "Real Game.unity: Tree Team Kill and Disaster Survival setup\n");
            SessionState.SetInt(Index, 0);
            SessionState.SetBool(Pass, false);
            SessionState.SetBool(Pending, true);
            Load();
        }
        static void Load()
        {
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Select()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            MatchChoices.MapId = MapId;
            MatchChoices.ModeId = Mode;
            MatchChoices.BotTarget = 10;
            MatchChoices.HandCount = 0;
        }
        [InitializeOnLoadMethod]
        static void Register()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Pending, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    stage = 0; settled = 0; sankAt = null; lastSink = "";
                    deadline = EditorApplication.timeSinceStartup + 150;
                    EditorApplication.update += Tick;
                }
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    EditorApplication.update -= Tick;
                    if (SessionState.GetBool(Pass, false) && SessionState.GetInt(Index, 0) == 0)
                    {
                        SessionState.SetInt(Index, 1); SessionState.SetBool(Pass, false);
                        EditorApplication.delayCall += Load;
                    }
                    else
                    {
                        bool success = SessionState.GetBool(Pass, false);
                        SessionState.SetBool(Pending, false);
                        EditorApplication.delayCall += () => EditorApplication.Exit(success ? 0 : 1);
                    }
                }
            };
        }
        static void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException(label);
            File.AppendAllText(Report, "PASS " + Mode + ": " + label + "\n");
        }
        // An open bit of carpet (or of the wood floor), clear overhead, nearest the course middle.
        static bool TryGroundSpot(IWorldQuery world, bool wood, out Vector3 spot)
        {
            int mask = (1 << Layers.Surface) | (1 << Layers.Block) | 1;
            float top = world.PlayableBounds.max.y + 5;
            for (float ring = 0; ring < 300; ring += 3)
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * Mathf.PI / 8;
                    var probe = new Vector3(world.Center.x + Mathf.Cos(angle) * ring, top, world.Center.z + Mathf.Sin(angle) * ring);
                    if (!Physics.Raycast(probe, Vector3.down, out var hit, top + 50, mask, QueryTriggerInteraction.Ignore) ||
                        (wood ? hit.collider.GetComponent<ILethalGround>() == null : !hit.collider.name.Contains("kill floor")))
                        continue;
                    spot = hit.point + Vector3.up * .05f;
                    if (!Physics.CheckCapsule(spot + Vector3.up * .4f, spot + Vector3.up * 1.65f, .4f, mask, QueryTriggerInteraction.Ignore))
                        return true;
                }
            spot = default;
            return false;
        }
        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Stage " + stage);
                var session = Services.Session as CastlePlaySession;
                var mode = session ? session.Modes : null;
                var world = Services.World;
                if (!mode || world == null || Time.frameCount < 10) return;
                if (stage == 0)
                {
                    if (mode.Frame.phase != RoundPhase.Playing || mode.Frame.players.Length < 2) return;
                    Check(world is ISyncedWorld synced && synced.SaveLayout() == "underneath-the-tree-v7-building-shells", "approved map loaded");
                    Check(mode.Rules.mode == MatchModeRuntime.ParseMode(Mode), "requested mode playing");
                    Check(mode.Rules.island == IslandRule.Sink, "the carpet is sinking ground");
                    var catalog = IslandLobbyCatalog.Runtime(Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog"));
                    var entry = Array.Find(catalog.maps, m => m.id == MapId);
                    Check(entry != null && entry.SupportsMode(Mode), "catalog permits mode");
                    var layout = Resources.LoadAll<MapMatchLayout>("MapMatchLayouts").Single(l => l.Matches(world));
                    Physics.SyncTransforms();
                    var root = ((ISyncedWorld)world).Root;
                    int mask = (1 << Layers.Surface) | (1 << Layers.Block) | 1;
                    var problems = new System.Collections.Generic.List<string>();
                    foreach (var local in layout.freeSpawns)
                    {
                        if (!layout.TrySpawnPosition(world, local, out var p)) { problems.Add("No nearby ground for " + local); continue; }
                        if (!Physics.Raycast(p + Vector3.up * .25f, Vector3.down, out var hit, .5f, mask, QueryTriggerInteraction.Ignore) || hit.normal.y < .9f)
                            problems.Add("Missing spawn floor at " + p.ToString("F3"));
                        else if (hit.collider.gameObject.layer != Layers.Block || !hit.collider.attachedRigidbody)
                            problems.Add("Spawn not on a gift top at " + p.ToString("F3") + " (" + hit.collider.name + ")");
                        if (Physics.CheckCapsule(p + Vector3.up * .4f, p + Vector3.up * 1.65f, .32f, mask, QueryTriggerInteraction.Ignore))
                            problems.Add("Blocked spawn at " + p.ToString("F3"));
                    }
                    if (problems.Count > 0) throw new Exception(string.Join("\n", problems));
                    Check(layout.teams.Length == 8 && layout.freeSpawns.Length == 128, "128 supported clear authored spawns across eight groups");
                    for (int team = 0; team < mode.Rules.teamCount; team++)
                    {
                        var p = mode.SpawnPoint(team, 0, "survivor");
                        var candidates = Mode == "teamtickets" ? layout.teams[team].spawns : layout.freeSpawns;
                        Check(candidates.Any(v => layout.TrySpawnPosition(world, v, out var resolved) && Vector3.Distance(resolved, p) < .01f), "spawn resolver uses authored group " + team);
                    }
                    if (Mode == "teamtickets")
                        Check(mode.Frame.players.Where(p => p.alive).Select(p => p.team).Distinct().Count() >= 2, "live participants assigned opposing teams");
                    Check(mode.Frame.players.Length >= 2, "local player and real bots registered");
                    settled = Time.time; stage = 1;
                }
                else if (stage == 1 && Time.time - settled >= 2)
                {
                    Check(mode.Frame.players.Any(p => p.id == mode.LocalActorId && p.alive), "local player survives initial setup");
                    Check(mode.Frame.players.Any(p => p.bot && p.alive), "bots survive initial setup");
                    Check(!mode.Frame.players.Any(p => p.id == mode.LocalActorId && p.sinking), "local player starts on a gift, not sinking");
                    var floor = GameObject.Find("Family room floor");
                    Check(floor && floor.GetComponent<ILethalGround>() != null, "wood room floor is marked deadly");
                    // Team Tickets proves the carpet sinks; Survival proves the wood floor kills at once.
                    bool wood = Mode == "survival";
                    Check(TryGroundSpot(world, wood, out var spot), wood ? "found open wood floor" : "found open carpet");
                    session.Player.Place(spot, Quaternion.identity);
                    settled = Time.time; stage = wood ? 3 : 2;
                }
                else if (stage == 2)
                {
                    var me = mode.Frame.players.FirstOrDefault(p => p.id == mode.LocalActorId);
                    if (me != null && me.sinking && !sankAt.HasValue)
                    {
                        sankAt = Time.time;
                        File.AppendAllText(Report, "PASS " + Mode + ": player on the carpet starts sinking after " + (Time.time - settled).ToString("F1") + " s\n");
                    }
                    if (me != null && (!me.alive || me.dying) && sankAt.HasValue)
                    {
                        Check(Time.time - sankAt.Value >= mode.Rules.sinkGraceSeconds - .6f, "carpet kills only after the sink grace");
                        File.AppendAllText(Report, "PASS " + Mode + ": carpet eliminated the player " + (Time.time - sankAt.Value).ToString("F1") + " s after sinking began\n");
                        File.AppendAllText(Report, "NOT VERIFIED: custom disasters, human navigation, network peers, Quest.\n");
                        SessionState.SetBool(Pass, true);
                        EditorApplication.ExitPlaymode();
                    }
                    else if (me != null && me.sinking)
                        lastSink = "sinking " + me.sinkProgress.ToString("F2") + " at y " + session.Player.transform.position.y.ToString("F2") + " (t " + (Time.time - settled).ToString("F1") + ")";
                    else if (sankAt.HasValue && lastSink.Length > 0 && !lastSink.StartsWith("stopped"))
                        lastSink = "stopped after " + lastSink + "; now y " + session.Player.transform.position.y.ToString("F2");
                    if (stage == 2 && Time.time - settled > mode.Rules.sinkGraceSeconds + 6)
                        throw new Exception((sankAt.HasValue ? "player sank but was not eliminated: " : "player on the carpet never started sinking: ") + lastSink +
                            " alive " + me?.alive + " dying " + me?.dying);
                }
                else if (stage == 3)
                {
                    var me = mode.Frame.players.FirstOrDefault(p => p.id == mode.LocalActorId);
                    if (me != null && (!me.alive || me.dying))
                    {
                        Check(!sankAt.HasValue, "wood floor kills without a sinking phase");
                        File.AppendAllText(Report, "PASS " + Mode + ": wood floor eliminated the player " + (Time.time - settled).ToString("F1") + " s after touching it\n");
                        File.AppendAllText(Report, "NOT VERIFIED: custom disasters, human navigation, network peers, Quest.\n");
                        SessionState.SetBool(Pass, true);
                        EditorApplication.ExitPlaymode();
                    }
                    else
                    {
                        if (me != null && me.sinking) sankAt = Time.time;
                        if (Time.time - settled > 3)
                            throw new Exception("player on the wood floor was not eliminated within 3 s");
                    }
                }
            }
            catch (Exception e)
            {
                File.AppendAllText(Report, "FAIL " + Mode + ": " + e + "\n");
                SessionState.SetBool(Pass, false);
                EditorApplication.ExitPlaymode();
            }
        }
    }
}
