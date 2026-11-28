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
        sealed class PickupSource : IEquipmentPickupSource
        { public EquipmentKind Kind { get; set; } public Vector3 Position { get; set; } public float Duration { get; set; } = 1; }
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
                    // Keep a copy beside the pictures so it travels with the test output.
                    try { File.Copy(Report, Path.Combine(Output, "WeaponLook.txt"), true); } catch (IOException) { }
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
            // A wall in front of the player gives every shot and swing something to hit.
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Weapon look impact wall";
            wall.layer = Layers.Surface;
            wall.transform.localScale = new Vector3(3, 2.4f, .4f);
            int index = 0;
            try
            {
                var kinds = Enum.GetValues(typeof(EquipmentKind)).Cast<EquipmentKind>()
                    .Where(kind => kind >= EquipmentKind.Blaster && kind != EquipmentKind.FlyingHand);
                foreach (var kind in kinds)
                {
                    index++;
                    if (!runtime.GiveEquipment(kind))
                    {
                        Check(false, "Developer grant equips " + kind);
                        continue;
                    }
                    // The developer grant can reopen the menu; it would cover the first-person shot.
                    session.SetMenu(false);
                    yield return new WaitForSeconds(1.1f);
                    session.SetMenu(false);
                    yield return null;
                    var model = player.transform.Find("Visual - " + kind);
                    Check(model, "Held model exists: " + kind);
                    if (!model) continue;
                    CheckSurfaces(kind, model);
                    var eye = view ? view.transform : player.transform;
                    File.AppendAllText(Report, $"  {kind} pose: model forward {model.forward:F2} up {model.up:F2}, " +
                        $"view forward {eye.forward:F2}, model offset from view (view space) {eye.InverseTransformPoint(model.position):F2}, " +
                        $"menu open {session.MenuOpen}, VR {session.IsVR}\n");
                    ShootFirstPerson(view, close, rt, pixels, kind + "-1-first-person.jpg");

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
                    if (kind == EquipmentKind.LaserAxe)
                    {
                        // A second sample shows whether the draw was still finishing or the carry itself is wrong.
                        yield return new WaitForSeconds(3);
                        File.AppendAllText(Report, $"  {kind} pose after 3 s: model forward {model.forward:F2} up {model.up:F2}, " +
                            $"model offset from view (view space) {eye.InverseTransformPoint(model.position):F2}\n");
                        Shoot(close, rt, pixels, kind + "-4-third-person-after-3s.jpg");
                    }

                    // World pickup: the weapon as it lies in the world, with its pickup bubble.
                    // Each pickup gets its own spot so earlier ones don't crowd the picture.
                    var lying = player.transform.position + side * (3f + (index / 6) * 2.6f) +
                        front * ((index % 6) * 2.6f - 6.5f) + Vector3.up * .6f;
                    runtime.RegisterPickup(new PickupSource { Kind = kind, Position = lying });
                    yield return new WaitForSeconds(.5f);
                    close.transform.position = lying + (-side * .9f + front * .9f + Vector3.up * .45f).normalized * 2.1f;
                    close.transform.LookAt(lying);
                    Shoot(close, rt, pixels, kind + "-5-world-pickup.jpg");

                    // Fire or swing, then the projectile, the impact and a reload where the weapon has them.
                    var spec = EquipmentSpecifications.Get(kind);
                    var eyePoint = player.ViewPose.position;
                    wall.transform.position = eyePoint + front * (spec.melee ? Mathf.Min(1.2f, spec.range * .7f) : 5f) +
                        Vector3.down * .15f;
                    wall.transform.rotation = Quaternion.LookRotation(front);
                    Physics.SyncTransforms();
                    var aim = new Pose(eyePoint + front * .4f,
                        Quaternion.LookRotation((wall.transform.position - (eyePoint + front * .4f)).normalized));
                    int before = runtime.Frame.equipmentSequence;
                    var held = (IHeldEquipmentRuntime)runtime;
                    bool fired;
                    if (spec.charge > 0 || spec.automatic)
                    {
                        fired = held.RequestEquipmentTrigger(true, aim);
                        float holdUntil = Time.time + spec.charge + .25f;
                        while (Time.time < holdUntil) { held.RequestEquipmentTrigger(true, aim); yield return new WaitForSeconds(.06f); }
                    }
                    else fired = runtime.RequestEquipmentAttack(aim);
                    yield return new WaitForSeconds(.06f);
                    ShootFirstPerson(view, close, rt, pixels, kind + "-6-fire.jpg");
                    if (spec.charge > 0 || spec.automatic) held.RequestEquipmentTrigger(false, aim);
                    File.AppendAllText(Report, $"  {kind} fire request accepted {fired}\n");

                    if (spec.projectileSpeed > 0)
                    {
                        yield return new WaitForSeconds(.18f);
                        var shot = runtime.Frame.weaponProjectiles.FirstOrDefault(p => p.kind == kind);
                        if (shot != null)
                        {
                            close.transform.position = shot.position + (side * 1.2f + Vector3.up * .4f - front * .3f).normalized * 1.8f;
                            close.transform.LookAt(shot.position);
                            Shoot(close, rt, pixels, kind + "-7-projectile.jpg");
                        }
                        else File.AppendAllText(Report, $"  {kind} no projectile in flight to picture\n");
                    }

                    Vector3? impact = null;
                    // Slow projectiles (rocket, grenade capsule, blaster bolt) need longer to land.
                    float impactDeadline = Time.time + (spec.projectileSpeed > 0 ? 4f : 1.6f);
                    while (impact == null && Time.time < impactDeadline)
                    {
                        foreach (var action in runtime.Frame.equipmentActions)
                        {
                            if (action == null || action.id <= before || action.kind != kind) continue;
                            if (action.action == EquipmentAction.Impact) { impact = action.position; break; }
                            if (action.action == EquipmentAction.Shot && action.endpoints != null && action.endpoints.Length > 0 &&
                                spec.projectileSpeed <= 0) { impact = action.endpoints[0]; break; }
                        }
                        if (impact == null) yield return null;
                    }
                    if (impact is Vector3 hit)
                    {
                        yield return new WaitForSeconds(.08f);
                        close.transform.position = hit + (side * 1f + Vector3.up * .8f - front * .6f).normalized * 2.4f;
                        close.transform.LookAt(hit);
                        Shoot(close, rt, pixels, kind + "-8-impact.jpg");
                    }
                    else File.AppendAllText(Report, $"  {kind} no impact to picture\n");

                    if (spec.magazine > 0 && spec.reloadSeconds > 0)
                    {
                        float reloadDeadline = Time.time + 3f;
                        while (runtime.EquipmentFor(runtime.LocalActorId).reloadRemaining <= 0 && Time.time < reloadDeadline)
                        {
                            if (spec.charge > 0 || spec.automatic) held.RequestEquipmentTrigger(true, aim);
                            else runtime.RequestEquipmentAttack(aim);
                            yield return new WaitForSeconds(Mathf.Max(.06f, spec.cooldown + .02f));
                        }
                        held.RequestEquipmentTrigger(false, aim);
                        var state = runtime.EquipmentFor(runtime.LocalActorId);
                        if (state.reloadRemaining > 0)
                        {
                            yield return new WaitForSeconds(spec.reloadSeconds * .45f);
                            close.transform.position = body.center + (front * 1f + side * .45f + Vector3.up * .2f).normalized *
                                (body.extents.magnitude / Mathf.Sin(close.fieldOfView * Mathf.Deg2Rad * .5f));
                            close.transform.LookAt(body.center);
                            Shoot(close, rt, pixels, kind + "-9-reload.jpg");
                        }
                        else File.AppendAllText(Report, $"  {kind} did not reach a reload within 3 s\n");
                    }
                    yield return null;
                }

                // Jump and speed powerups together, to judge the edge tint.
                var receiver = (IAffectable)player;
                var jump = Resources.Load<EffectType>("Powerups/Jump3x");
                var speed = Resources.Load<EffectType>("Powerups/Speed2x");
                runtime.GiveEquipment(EquipmentKind.CompactSidearm);
                // The tint is hidden while the pause menu is open or input is paused, so play
                // unpaused for these pictures (the test editor has no focus, which reopens the menu).
                IEnumerator Unpaused(string file)
                {
                    for (int frame = 0; frame < 30; frame++)
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                        session.SetMenu(false);
                        player.InputSuspended = false;
                        yield return null;
                    }
                    Cursor.lockState = CursorLockMode.Locked;
                    session.SetMenu(false);
                    player.InputSuspended = false;
                    // WaitForEndOfFrame never fires in an unfocused test editor; one more frame is enough.
                    yield return null;
                    var tint = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                        .FirstOrDefault(c => c.name == "Body feedback - no bars");
                    File.AppendAllText(Report, $"  {file}: menu open {session.MenuOpen}, tint canvas " +
                        $"{(tint ? (tint.enabled ? "shown" : "hidden") : "not built")}, mode {(tint ? tint.renderMode.ToString() : "-")}\n");
                    // The tint canvas is a screen overlay, which camera renders skip; draw its material over the picture.
                    var overlay = tint ? tint.GetComponentInChildren<UnityEngine.UI.Image>()?.materialForRendering : null;
                    ShootFirstPerson(view, close, rt, pixels, file, overlay);
                    player.InputSuspended = true;
                }
                receiver.Apply(new Effect { type = jump, strength = 1, duration = 8 });
                yield return new WaitForSeconds(1.2f);
                yield return Unpaused("Powerup-jump.jpg");
                receiver.Remove(jump);
                receiver.Apply(new Effect { type = speed, strength = 1, duration = 8 });
                yield return new WaitForSeconds(1.2f);
                yield return Unpaused("Powerup-speed.jpg");
                receiver.Apply(new Effect { type = jump, strength = 1, duration = 8 });
                yield return new WaitForSeconds(1.2f);
                yield return Unpaused("Powerup-jump-and-speed.jpg");
                Check(receiver.Has(jump) && receiver.Has(speed), "Jump and speed powerups are active for the tint pictures");
            }
            finally
            {
                rt.Release();
                Object.Destroy(rt);
                Object.Destroy(pixels);
                Object.Destroy(close.gameObject);
                Object.Destroy(wall);
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

        // The editor reopens the pause menu when it loses focus (always, in a test editor). The
        // player camera is still used (only it hides the player's own head), with the nearby
        // world-space pause tablet switched off for that frame.
        static void ShootFirstPerson(Camera view, Camera unused, RenderTexture rt, Texture2D pixels, string file,
            Material overlay = null)
        {
            var hidden = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(c => c.isRootCanvas && c.renderMode == RenderMode.WorldSpace && c.gameObject.activeSelf &&
                    (c.transform.position - view.transform.position).sqrMagnitude < 4)
                .Select(c => c.gameObject).ToList();
            foreach (var item in hidden) item.SetActive(false);
            try { Shoot(view, rt, pixels, file, overlay); }
            finally { foreach (var item in hidden) if (item) item.SetActive(true); }
        }

        static void Shoot(Camera camera, RenderTexture rt, Texture2D pixels, string file, Material overlay = null)
        {
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            // The unfocused test editor keeps the pause tablet open; hide any world-space canvas near this camera.
            var tablets = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(c => c.isRootCanvas && c.renderMode == RenderMode.WorldSpace && c.gameObject.activeSelf &&
                    (c.transform.position - camera.transform.position).sqrMagnitude < 36)
                .Select(c => c.gameObject).ToList();
            foreach (var tablet in tablets) tablet.SetActive(false);
            try
            {
                camera.targetTexture = rt;
                camera.Render();
                // A full-screen UI image draws white texture tinted by its material, blended over the picture.
                if (overlay) Graphics.Blit(Texture2D.whiteTexture, rt, overlay);
                RenderTexture.active = rt;
                pixels.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(Output, file), pixels.EncodeToJPG(90));
                // Unity draws a missing or broken shader as pure magenta; count those pixels in every picture.
                var colors = pixels.GetPixels32();
                int magenta = 0;
                foreach (var c in colors) if (c.r > 245 && c.g < 12 && c.b > 245) magenta++;
                Check(magenta < colors.Length / 500, $"No missing-shader magenta in {file} ({magenta} pixels)");
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                foreach (var tablet in tablets) if (tablet) tablet.SetActive(true);
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
