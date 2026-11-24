using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Pictures of every weapon as the real desktop player holds it, plus a close-up and a
    // shader check for each held model. Used to confirm the Blender art reached the game.
    [InitializeOnLoad]
    public static class WeaponLookCapture
    {
        const string Key = "WeaponLookCapture";
        static int checks, errors;
        static bool failed, completed;
        static string Report => ProjectPath.Report("WeaponLook.txt");
        static string Output => Path.Combine(Path.GetDirectoryName(Application.dataPath), "ArtDirection", "WeaponLookCapture");

        static WeaponLookCapture()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    checks = errors = 0;
                    failed = completed = false;
                    Application.logMessageReceived += Log;
                    EditorApplication.delayCall += () =>
                        Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(Guard());
                }
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    Application.logMessageReceived -= Log;
                    SessionState.SetBool(Key, false);
                    MatchSetup.FromLobby = SessionState.GetBool(Key + "Lobby", false);
                    MatchSetup.StartLocalSession = SessionState.GetBool(Key + "Local", false);
                    failed |= !completed;
                    File.AppendAllText(Report, $"COMPLETE checks={checks} runtimeErrors={errors} failed={failed}. Desktop editor; headset not tested.\n");
                    int result = failed ? 1 : 0;
                    if (!string.IsNullOrEmpty(SessionState.GetString("DJWarm.id", "")))
                    {
                        Directory.CreateDirectory("Logs/Warm");
                        File.WriteAllText("Logs/Warm/exit.txt", result.ToString());
                    }
                    else if (Application.isBatchMode)
                        EditorApplication.Exit(result);
                }
            };
        }

        public static void Run()
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save edited scenes first.");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Report, "Weapon look capture: every weapon held by the real desktop player.\n");
            SessionState.SetBool(Key + "Lobby", MatchSetup.FromLobby);
            SessionState.SetBool(Key + "Local", MatchSetup.StartLocalSession);
            SessionState.SetBool(Key, true);
            MatchSetup.FromLobby = false;
            MatchSetup.StartLocalSession = false;
            MatchSetup.MapId = "castle";
            MatchSetup.ModeId = "demo";
            MatchSetup.BotTarget = 0;
            MatchSetup.HandCount = 0;
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }

        static void Log(string message, string trace, LogType kind)
        {
            if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert)
            {
                errors++;
                File.AppendAllText(Report, "ERROR " + message + "\n");
            }
        }

        static void Check(bool pass, string label)
        {
            File.AppendAllText(Report, (pass ? "PASS " : "FAIL ") + label + "\n");
            checks++;
            if (!pass) failed = true;
        }

        static IEnumerator Guard()
        {
            var run = Capture();
            while (true)
            {
                bool more = false;
                object value = null;
                try
                {
                    more = run.MoveNext();
                    if (more) value = run.Current;
                }
                catch (Exception error)
                {
                    failed = true;
                    File.AppendAllText(Report, "FAIL " + error + "\n");
                    break;
                }
                if (!more) break;
                yield return value;
            }
            completed = !failed || checks > 0;
            EditorApplication.ExitPlaymode();
        }

        static IEnumerator Capture()
        {
            var session = Object.FindFirstObjectByType<CastlePlaySession>();
            float until = Time.realtimeSinceStartup + 40;
            while ((session.Player == null || session.Modes == null || Services.World == null) && Time.realtimeSinceStartup < until)
                yield return null;
            Check(session.Player != null && session.Modes != null, "Actual Game initializes");
            session.SetRole(false);
            session.SetMenu(false);
            session.SurvivorBots.AutoFill = false;
            session.SurvivorBots.Clear();
            session.HandBots?.Clear();
            if (session.Social != null) session.Social.VoiceEnabled = false;
            session.Demo.Run(DemoCommand.StopDisasters);
            var runtime = session.Modes;
            var player = session.Survivor();
            var rules = ModeSimulation.Defaults(GameModeKind.Demo);
            rules.countdownSeconds = 0;
            rules.deathEnabled = false;
            rules.fallDamage = false;
            runtime.Configure(rules);
            runtime.StartRound();

            // Stand on the castle so the first-person pictures have the real game behind the weapon.
            var top = Services.World.Blocks.Where(b => b && b.GetComponent<Collider>())
                .Select(b => b.GetComponent<Collider>().bounds).OrderByDescending(b => b.max.y).First();
            player.Place(new Vector3(top.center.x, top.max.y + .03f, top.center.z), Quaternion.Euler(0, 35, 0));
            player.InputSuspended = true;
            yield return new WaitForSeconds(1);

            var view = session.ActiveCamera;
            var rt = new RenderTexture(1280, 800, 24);
            var pixels = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            var close = new GameObject("Weapon look close-up camera").AddComponent<Camera>();
            close.enabled = false;
            close.fieldOfView = 32;
            close.nearClipPlane = .02f;
            close.clearFlags = CameraClearFlags.SolidColor;
            close.backgroundColor = new Color(.10f, .11f, .13f);
            try
            {
                var kinds = Enum.GetValues(typeof(EquipmentKind)).Cast<EquipmentKind>()
                    .Where(kind => kind >= EquipmentKind.Blaster && kind != EquipmentKind.FlyingHand);
                foreach (var kind in kinds)
                {
                    if (!runtime.GiveEquipment(kind))
                    {
                        Check(false, "Developer grant equips " + kind);
                        continue;
                    }
                    yield return new WaitForSeconds(1.1f);
                    var model = player.transform.Find("Visual - " + kind);
                    Check(model, "Held model exists: " + kind);
                    if (!model) continue;
                    CheckSurfaces(kind, model);
                    Shoot(view, rt, pixels, kind + "-1-first-person.jpg");

                    var bounds = BoundsOf(model.gameObject);
                    float distance = Mathf.Max(.35f, bounds.extents.magnitude / Mathf.Sin(close.fieldOfView * Mathf.Deg2Rad * .5f) * 1.1f);
                    var side = player.transform.right;
                    close.transform.position = bounds.center + (side * 1f + Vector3.up * .25f).normalized * distance;
                    close.transform.LookAt(bounds.center);
                    Shoot(close, rt, pixels, kind + "-2-close.jpg");

                    // Third person, facing the player: shows how both hands hold it.
                    var body = BoundsOf(player.gameObject);
                    body.Encapsulate(bounds);
                    var front = player.transform.forward;
                    close.transform.position = body.center + (front * 1f + side * .45f + Vector3.up * .2f).normalized *
                        (body.extents.magnitude / Mathf.Sin(close.fieldOfView * Mathf.Deg2Rad * .5f));
                    close.transform.LookAt(body.center);
                    close.cullingMask = ~0;
                    Shoot(close, rt, pixels, kind + "-3-third-person.jpg");
                    yield return null;
                }

                // Jump and speed powerups together, to judge the edge tint.
                var receiver = (IAffectable)player;
                var jump = Resources.Load<EffectType>("Powerups/Jump3x");
                var speed = Resources.Load<EffectType>("Powerups/Speed2x");
                runtime.GiveEquipment(EquipmentKind.CompactSidearm);
                receiver.Apply(new Effect { type = jump, strength = 1, duration = 6 });
                yield return new WaitForSeconds(1.2f);
                Shoot(view, rt, pixels, "Powerup-jump.jpg");
                receiver.Remove(jump);
                receiver.Apply(new Effect { type = speed, strength = 1, duration = 6 });
                yield return new WaitForSeconds(1.2f);
                Shoot(view, rt, pixels, "Powerup-speed.jpg");
                receiver.Apply(new Effect { type = jump, strength = 1, duration = 6 });
                yield return new WaitForSeconds(1.2f);
                Shoot(view, rt, pixels, "Powerup-jump-and-speed.jpg");
                Check(receiver.Has(jump) && receiver.Has(speed), "Jump and speed powerups are active for the tint pictures");
            }
            finally
            {
                rt.Release();
                Object.Destroy(rt);
                Object.Destroy(pixels);
                Object.Destroy(close.gameObject);
            }
        }

        static void CheckSurfaces(EquipmentKind kind, Transform model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            var broken = new List<string>();
            var shaders = new HashSet<string>();
            foreach (var renderer in renderers)
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) continue;
                foreach (var material in renderer.sharedMaterials)
                {
                    if (!material || !material.shader)
                    {
                        broken.Add(renderer.name + " has no material");
                        continue;
                    }
                    shaders.Add(material.shader.name);
                    if (!material.shader.isSupported || ShaderUtil.ShaderHasError(material.shader) ||
                        material.shader.name.Contains("InternalError"))
                        broken.Add(renderer.name + " uses broken shader " + material.shader.name);
                }
            }
            File.AppendAllText(Report, $"{kind}: {renderers.Length} renderers, shaders [{string.Join(", ", shaders)}]\n");
            foreach (var line in broken) File.AppendAllText(Report, "  " + line + "\n");
            Check(broken.Count == 0, "Every held surface renders with a working shader: " + kind);
        }

        static void Shoot(Camera camera, RenderTexture rt, Texture2D pixels, string file)
        {
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            try
            {
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                pixels.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(Output, file), pixels.EncodeToJPG(90));
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
            }
        }

        static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>()
                .Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer)).ToArray();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * .3f);
            var total = renderers[0].bounds;
            foreach (var r in renderers) total.Encapsulate(r.bounds);
            return total;
        }
    }
}
