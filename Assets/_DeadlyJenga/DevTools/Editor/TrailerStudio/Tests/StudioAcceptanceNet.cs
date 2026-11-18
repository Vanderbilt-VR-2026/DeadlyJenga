using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.CandyPhysicsHands;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CharacterNetwork;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    public static partial class StudioAcceptance
    {
        // Remote copies of characters, the way multiplayer shows them, with nothing from the Trailer Studio.
        //   -executeMethod DeadlyJenga.DevTools.Editor.TrailerStudio.StudioAcceptance.RunNet
        // This file only uses game code that also exists before the Studio's changes, so the same file can run
        // on the old commit and the numbers can be compared.
        public static void RunNet() => StudioNetCheck.Run();
    }

    [InitializeOnLoad]
    public static class StudioNetCheck
    {
        const string Key = "DeadlyJenga.TrailerStudio.NetCheck";
        const float Dt = 1f / 72f;   // one Quest frame; fixed so two runs give the same numbers
        static readonly Vector3 Origin = new Vector3(0, 300, 0);   // far above every map, so no ground is involved

        static string Folder => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Validation", "TrailerStudio-Net");
        static int Step { get => SessionState.GetInt(Key, 0); set => SessionState.SetInt(Key, value); }
        static string Lines { get => SessionState.GetString(Key + ".Lines", ""); set => SessionState.SetString(Key + ".Lines", value); }
        static int Failures { get => SessionState.GetInt(Key + ".Failures", 0); set => SessionState.SetInt(Key + ".Failures", value); }
        static IEnumerator<int> routine;
        static int errors;
        static readonly List<string> errorLines = new List<string>();

        static StudioNetCheck()
        {
            EditorApplication.playModeStateChanged += Changed;
            if (Step != 0) { EditorApplication.update -= Tick; EditorApplication.update += Tick; }
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start in Edit Mode.");
            if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
            Directory.CreateDirectory(Folder);
            Lines = ""; Failures = 0; Step = 1;
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }

        static void Changed(PlayModeStateChange change)
        {
            if (Step == 0) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                Step = 2; routine = null; errors = 0; errorLines.Clear();
                Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
                EditorApplication.update -= Tick; EditorApplication.update += Tick;
            }
            if (change == PlayModeStateChange.EnteredEditMode && Step == 3) Finish();
        }

        static void Log(string message, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            errors++;
            if (errorLines.Count < 8) errorLines.Add(type + ": " + message.Split('\n')[0]);
        }

        static void Result(string test, bool passed, string detail)
        {
            Lines += $"| {test} | {(passed ? "PASS" : "FAIL")} | {detail.Replace("|", "/").Replace("\n", " ")} |\n";
            if (!passed) Failures++;
            Debug.Log($"[TrailerStudio acceptance] {(passed ? "PASS" : "FAIL")} {test}: {detail}");
        }

        static void Tick()
        {
            if (Step != 2 || !EditorApplication.isPlaying) return;
            try
            {
                routine ??= Tests();
                if (routine.MoveNext()) return;
            }
            catch (Exception error) { Debug.LogException(error); Result("Run stopped", false, error.Message); }
            routine = null; Step = 3;
            Application.logMessageReceived -= Log;
            EditorApplication.ExitPlaymode();
        }

        static void Finish()
        {
            Step = 0;
            EditorApplication.update -= Tick;
            var text = new StringBuilder("# Trailer Studio Net check (remote character copies)\n\n");
            text.Append($"Date: {DateTime.Now:yyyy-MM-dd HH:mm}  \nUnity: {Application.unityVersion}\n\n| Test | Result | Detail |\n|------|--------|--------|\n").Append(Lines);
            text.Append($"\nFailures: {Failures}\n");
            File.WriteAllText(Path.Combine(Folder, "report.md"), text.ToString());
            Debug.Log("[TrailerStudio acceptance] finished with " + Failures + " failures. Report: " + Path.Combine(Folder, "report.md"));
            if (Application.isBatchMode || Environment.GetCommandLineArgs().Any(a => a.Contains("StudioAcceptance.Run")))
                EditorApplication.Exit(Failures == 0 ? 0 : 1);
        }

        static string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "({0:F5}, {1:F5}, {2:F5})", v.x, v.y, v.z);

        static byte[] Bytes(Action<BinaryWriter> write)
        {
            using (var memory = new MemoryStream())
            using (var w = new BinaryWriter(memory))
            {
                write(w);
                w.Flush();
                return memory.ToArray();
            }
        }

        static void Feed(INetworkActorView view, byte[] state)
        {
            using (var r = new BinaryReader(new MemoryStream(state))) view.ReadState(r, true);
        }

        // A made-up but fixed walk: the same states on every run and every commit.
        static CandyAvatarFrame SurvivorState(int k)
        {
            var turn = Quaternion.Euler(0, 15 * k, 0);
            var feet = Origin + new Vector3(.4f * k, 0, .05f * k * k);
            var head = feet + new Vector3(0, 1.6f, 0) + turn * new Vector3(0, .02f * Mathf.Sin(k), .05f);
            CandyHandPose Hand(bool left, Vector3 offset) => new CandyHandPose
            {
                tracked = true, skeletonTracked = true, position = head + turn * offset,
                rotation = turn * Quaternion.Euler(10 * k, left ? -20 : 20, 0), skeleton = CandyHandSkeleton.Rest(left)
            };
            return new CandyAvatarFrame
            {
                feet = feet, head = head, headRotation = turn * Quaternion.Euler(-5 * k, 0, 0), scale = 1,
                left = Hand(true, new Vector3(-.25f, -.45f + .05f * k, .3f)), right = Hand(false, new Vector3(.25f, -.4f, .35f + .04f * k))
            };
        }

        static CandyHandPose HandTarget(int k) => new CandyHandPose
        {
            tracked = true, skeletonTracked = true, position = Origin + new Vector3(2 + .3f * k, 4 - .2f * k, .1f * k),
            rotation = Quaternion.Euler(0, 20 * k, 5 * k), skeleton = CandyHandSkeleton.Rest(false)
        };

        static Pose[] HandBones(int k)
        {
            var target = HandTarget(k);
            var bones = new Pose[16];
            for (int i = 0; i < 16; i++)
                bones[i] = new Pose(target.position + target.rotation * new Vector3(.012f * i, .004f * i, .01f * (i % 4)), target.rotation * Quaternion.Euler(0, 0, 6 * i));
            return bones;
        }

        static IEnumerator<int> Tests()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            CastlePlaySession session;
            while ((!(session = Object.FindFirstObjectByType<CastlePlaySession>()) || !NetworkActors.Alive(session.Player)) && watch.Elapsed.TotalSeconds < 90) yield return 0;
            if (!session || !NetworkActors.Alive(session.Player)) { Result("Setup", false, "Game.unity did not start a local session with a player within 90 s."); yield break; }
            session.SurvivorBots?.Clear(); session.HandBots?.Clear();
            for (int n = 0; n < 60; n++) yield return 0;

            var localSurvivor = session.Player.gameObject;
            var localHand = NetworkActors.Alive(session.Hand) ? session.Hand.gameObject : null;
            bool snapExists = typeof(INetworkActorView).Assembly.GetType("DeadlyJenga.Core.INetworkActorSnap") != null;
            Result("Setup", true, $"Game.unity running, local survivor '{localSurvivor.name}', local hand '{(localHand ? localHand.name : "none")}'. " +
                $"This build {(snapExists ? "has" : "does not have")} the Studio's INetworkActorSnap; this check never calls SnapToState, Gait, StepGait or Heading. " +
                $"Every copy is made with NetworkActors.CreateView and rendered with a fixed dt of {Dt * 1000:F2} ms (one 72 Hz frame), so runs on different commits give comparable numbers.");

            // ---- 1. A real state from the local player, as a guest would receive it ----
            var actor = localSurvivor.GetComponentsInChildren<INetworkActor>(true).FirstOrDefault(a => a.NetworkKind == NetworkKinds.Survivor);
            var realRoot = new GameObject("Net check: copy of the local player");
            var realView = NetworkActors.CreateView(NetworkKinds.Survivor, realRoot, new NetworkContext { host = false, localSurvivor = localSurvivor, localHand = localHand, actorId = "net-check-real" });
            if (actor == null || realView == null) Result("1. The local player's own WriteState is shown by a remote survivor copy", false, $"local actor found: {actor != null}, view made: {realView != null}");
            else
            {
                var state = Bytes(actor.WriteState);
                Feed(realView, state);
                for (int n = 0; n < 60; n++) realView.Render(Dt);
                CandyAvatarFrame sent;
                using (var r = new BinaryReader(new MemoryStream(state))) CharacterWire.ReadSurvivor(r, out sent, out _, out _, out _, out _);
                var survivorView = realView as SurvivorNetworkView;
                float headError = survivorView ? Vector3.Distance(survivorView.ViewPose.position, sent.head) : 99;
                float feetError = Vector3.Distance(realRoot.transform.position, sent.feet);
                Result("1. The local player's own WriteState is shown by a remote survivor copy", headError < .001f && feetError < .001f && survivorView && survivorView.Visual,
                    $"{state.Length} bytes. After 60 frames the copy's head is {headError * 1000:F3} mm and its root {feetError * 1000:F3} mm from the sent pose; it has a body: {survivorView && survivorView.Visual}. " +
                    "(Not compared between commits: the live player's idle hands move with time.)");
            }
            Object.Destroy(realRoot);
            yield return 0;

            // ---- 2. A fixed sequence of survivor states, eased frame by frame ----
            var root = new GameObject("Net check: survivor copy");
            var view = NetworkActors.CreateView(NetworkKinds.Survivor, root, new NetworkContext { host = false, localSurvivor = localSurvivor, localHand = localHand, actorId = "net-check" }) as SurvivorNetworkView;
            var numbers = new StringBuilder();
            float worstStep = 0, lastGap = 0;
            if (!view) Result("2. A survivor copy eases toward each new state", false, "NetworkActors.CreateView(survivor) did not make a SurvivorNetworkView.");
            else
            {
                Vector3 lastHead = Vector3.zero; bool hasLast = false;
                for (int k = 0; k < 6; k++)
                {
                    var frame = SurvivorState(k);
                    Feed(view, Bytes(w => CharacterWire.WriteSurvivor(w, frame, 3, default(AvatarAppearance))));
                    for (int n = 1; n <= 12; n++)
                    {
                        view.Render(Dt);
                        var rig = view.Visual ? view.Visual.GetComponent<CandyPlayerRig>() : null;
                        if (hasLast) worstStep = Mathf.Max(worstStep, Vector3.Distance(view.ViewPose.position, lastHead));
                        lastHead = view.ViewPose.position; hasLast = true;
                        if (n == 1 || n == 4 || n == 12)
                            numbers.Append($"state {k} frame {n}: view head {V(view.ViewPose.position)}, rig head {V(rig ? rig.HeadAnchor.position : Vector3.zero)}, " +
                                $"body {V(rig ? rig.BodyAnchor.position : Vector3.zero)}, right palm {V(rig ? rig.RightPalm.position : Vector3.zero)}; ");
                    }
                    lastGap = Vector3.Distance(view.ViewPose.position, frame.head);
                }
                // The easing used by the game: 1 - exp(-20 dt) of the gap per frame. After 12 frames of 1/72 s about 3.6 % of a jump is left.
                float expectedLeft = Mathf.Pow(Mathf.Exp(-20 * Dt), 12);
                var final = SurvivorState(5); var previous = SurvivorState(4);
                float predicted = Vector3.Distance(previous.head, final.head) * expectedLeft;
                Result("2. A survivor copy eases toward each new state", Mathf.Abs(lastGap - predicted) < .003f && lastGap > .00001f && worstStep < .2f,
                    $"6 states x 12 frames. Gap left to the last state after 12 frames {lastGap * 1000:F3} mm; the easing formula predicts {predicted * 1000:F3} mm (it would be 0 if the copy snapped). " +
                    $"Largest head move in one frame {worstStep * 100:F2} cm. Numbers to compare between commits: {numbers}");
            }
            Object.Destroy(root);
            yield return 0;

            // ---- 3. A fixed sequence of hand states (a guest's copy of the flying hand) ----
            var handRoot = new GameObject("Net check: hand copy");
            var hand = localHand ? NetworkActors.CreateView(NetworkKinds.Hand, handRoot, new NetworkContext { host = false, localSurvivor = localSurvivor, localHand = localHand, actorId = "net-check-hand" }) as HandNetworkView : null;
            if (!hand) Result("3. A hand copy eases its bones toward each new state", false, localHand ? "NetworkActors.CreateView(hand) did not make a HandNetworkView." : "This Game.unity session has no local hand to copy from.");
            else
            {
                var handNumbers = new StringBuilder();
                float handGap = 0;
                for (int k = 0; k < 4; k++)
                {
                    var target = HandTarget(k); var bones = HandBones(k);
                    Feed(hand, Bytes(w => CharacterWire.WriteHand(w, false, 1, target, bones, 0)));
                    for (int n = 1; n <= 12; n++)
                    {
                        hand.Render(Dt);
                        if (!hand.Hand) continue;
                        if (n == 1 || n == 12)
                            handNumbers.Append($"state {k} frame {n}: bone 0 {V(hand.Hand.bodies[0].position)}, bone 15 {V(hand.Hand.bodies[15].position)}; ");
                    }
                    if (hand.Hand) handGap = Vector3.Distance(hand.Hand.bodies[15].position, bones[15].position);
                }
                float jump = Vector3.Distance(HandBones(2)[15].position, HandBones(3)[15].position);
                float predicted = jump * Mathf.Pow(Mathf.Exp(-20 * Dt), 12);
                Result("3. A hand copy eases its bones toward each new state", hand.Hand && handGap < jump * .2f && handGap > .00001f,
                    $"4 states x 12 frames. Bone 15 is {handGap * 1000:F3} mm from its last state; the easing formula predicts {predicted * 1000:F3} mm. Numbers to compare between commits: {handNumbers}");
            }
            Object.Destroy(handRoot);
            yield return 0;

            Result("Runtime errors during the check", errors == 0, errors == 0 ? "None." : $"{errors} errors. First: {string.Join(" // ", errorLines)}");
        }
    }
}
