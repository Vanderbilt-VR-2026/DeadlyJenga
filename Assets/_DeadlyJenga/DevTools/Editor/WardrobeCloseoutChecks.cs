using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CharacterNetwork;
using DeadlyJenga.CandyArt.Marina;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class WardrobeCloseoutChecks
    {
        const string Key = "WardrobeCloseoutChecks";
        const string Legacy = "CandyAvatar.Accessories.v1";
        const string Style = "CandyAvatar.Style.v2";
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        static string Report => ProjectPath.Report("WardrobeCloseoutChecks.txt");
        static bool failed;

        static WardrobeCloseoutChecks()
        {
            EditorApplication.playModeStateChanged += State;
        }

        public static void Run()
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Use the reserved batch editor.");
            File.WriteAllText(Report, "Desktop Marina wardrobe and solo match. Buttons invoked through their bound actions. Unity " +
                Application.unityVersion + "\n");
            SessionState.SetBool(Key + Legacy, PlayerPrefs.HasKey(Legacy));
            SessionState.SetInt(Key + Legacy, PlayerPrefs.GetInt(Legacy));
            SessionState.SetBool(Key + Style, PlayerPrefs.HasKey(Style));
            SessionState.SetString(Key + Style, PlayerPrefs.GetString(Style));
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));
            EditorApplication.EnterPlaymode();
        }

        static void State(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                failed = false;
                Application.logMessageReceived += Log;
                var driver = new GameObject("Wardrobe validation driver");
                UnityEngine.Object.DontDestroyOnLoad(driver);
                driver.AddComponent<MayhemMarinaChecksDriver>().StartCoroutine(Guard());
            }
            if (state != PlayModeStateChange.EnteredEditMode) return;
            Application.logMessageReceived -= Log;
            SessionState.SetBool(Key, false);
            if (SessionState.GetBool(Key + Legacy, false)) PlayerPrefs.SetInt(Legacy, SessionState.GetInt(Key + Legacy, 0));
            else PlayerPrefs.DeleteKey(Legacy);
            if (SessionState.GetBool(Key + Style, false)) PlayerPrefs.SetString(Style, SessionState.GetString(Key + Style, ""));
            else PlayerPrefs.DeleteKey(Style);
            PlayerPrefs.Save();
            File.AppendAllText(Report, $"COMPLETE failed={failed}\n");
            EditorApplication.Exit(failed ? 2 : 0);
        }

        static IEnumerator Guard()
        {
            using (var test = CheckWardrobe())
            {
                while (true)
                {
                    object next;
                    try
                    {
                        if (!test.MoveNext()) break;
                        next = test.Current;
                    }
                    catch (Exception exception)
                    {
                        failed = true;
                        File.AppendAllText(Report, exception + "\n");
                        break;
                    }
                    yield return next;
                }
            }
            EditorApplication.ExitPlaymode();
        }

        static System.Collections.Generic.IEnumerator<object> CheckWardrobe()
        {
            float deadline = Time.realtimeSinceStartup + 35;
            while ((!IslandLobby.Current || IslandLobby.Current.Player == null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            var lobby = IslandLobby.Current;
            Check(lobby && lobby.Player != null && !lobby.Player.IsVR, "Desktop lobby player is ready");
            var view = lobby.View();
            Check(view && lobby.GetComponent<MayhemMarina>(), "Actual Marina and wardrobe view are loaded");
            var avatar = lobby.Player.gameObject.GetComponent<CandyPlayerAvatar>();
            Check(avatar && avatar.Rig, "Actual local Candy avatar is available");
            var requested = new AvatarAppearance
            {
                accessories = AvatarAccessory.TopHat | AvatarAccessory.Beanie | AvatarAccessory.DevilHorns,
                hair = AvatarHair.Afro
            };
            avatar.ApplyAppearance(requested);
            string saved = PlayerPrefs.GetString(Style);
            lobby.SetMenu(false);
            lobby.Player.Place(view.WardrobePosition, Quaternion.identity);
            view.SetWardrobeOpen(true);
            for (int frame = 0; frame < 4; frame++) yield return null;
            Check(view.WardrobeOpen, "Wardrobe opens at the Marina boutique");
            Check(avatar.Appearance.Equals(requested) && PlayerPrefs.GetString(Style) == saved,
                "Opening does not normalize or rewrite the desired outfit");
            var status = (Text)typeof(IslandLobbyView).GetField("desktopWardrobeStatus", Instance).GetValue(view);
            Check(status.text.Contains("stay saved"), "Desktop viewer explains hidden items");
            var fitField = typeof(IslandLobbyView).GetField("wardrobeFitDistance", Instance);
            // Batch Unity has no focused Game view. Draw the real mirror after the pose settles.
            typeof(IslandLobbyView).GetMethod("PresentWardrobe", Instance).Invoke(view, new object[] { false });
            var fit = (float)fitField.GetValue(view);
            Check(fit > 0 && fit < 6, $"Mirror fits measured avatar geometry (distance {fit:F2} m)");
            Click(view, "Category 2");
            Click(view, "Accessory 0");
            Check((avatar.Appearance.accessories & AvatarAccessory.Beanie) != 0 &&
                (avatar.EvaluateAppearance(avatar.Appearance).effective.accessories & AvatarAccessory.Beanie) != 0,
                "Removing Top Hat reveals the previously hidden Beanie");
            Click(view, "Accessory 2");
            Check(avatar.EvaluateAppearance(avatar.Appearance).effective.hair == AvatarHair.Afro,
                "Removing Beanie restores the saved Afro");
            Click(view, "Accessory 3");
            Check(avatar.Appearance.accessories == AvatarAccessory.CowboyHat,
                "Selecting a new hat replaces other items in its category");
            Click(view, "Cosmetic shade 3");
            Check(avatar.Appearance.hairShade == AvatarHairShade.Light, "Shade selection applies through the live wardrobe");

            requested = new AvatarAppearance
            {
                accessories = AvatarAccessory.DevilHorns,
                clothing = AvatarClothing.PufferVest,
                hair = AvatarHair.Ponytail,
                eyewear = AvatarEyewear.Round
            };
            avatar.ApplyAppearance(requested);
            Check(avatar.EvaluateAppearance(requested).effective.Equals(requested),
                "Different wardrobe categories coexist in the resolver");
            var covered = requested;
            covered.skin = AvatarSkin.DeepSeaDiver;
            avatar.ApplyAppearance(covered);
            var nativeSkin = Resources.Load<WardrobeSkinAsset>("WardrobeSkins/" + covered.skin);
            Check(nativeSkin && avatar.Rig.bodyRenderer.sharedMesh == nativeSkin.bodyMesh,
                "The live survivor equips the approved native Diver geometry");
            avatar.Rig.SetColor(avatar.Rig.ColorIndex + 1);
            Check(avatar.Rig.bodyRenderer.sharedMaterials.SequenceEqual(nativeSkin.bodyMaterials),
                "Changing player color preserves every approved Diver material");
            var resolution = avatar.EvaluateAppearance(covered);
            Check(resolution.hiddenHair && resolution.hiddenClothing == AvatarClothing.PufferVest &&
                resolution.effective.eyewear == AvatarEyewear.None && avatar.Appearance.Equals(covered),
                "Diver covers outfit, hair and eyewear without rewriting choices");
            Click(view, "Category 0");
            Click(view, "Accessory 0");
            Check(avatar.Appearance.Equals(requested), "Selecting Default restores the saved outfit");
            requested.hairShade = AvatarHairShade.Light;
            using (var packet = new MemoryStream())
            using (var writer = new BinaryWriter(packet))
            using (var reader = new BinaryReader(packet))
            {
                CharacterWire.WriteSurvivor(writer, avatar.LastFrame, avatar.Rig.ColorIndex, requested);
                packet.Position = 0;
                CharacterWire.ReadSurvivor(reader, out _, out _, out var received, out _);
                Check(received.Equals(requested), "Current survivor packets retain the selected shade and outfit");
            }
            requested.hairShade = AvatarHairShade.Original;
            requested.poweredArmor = true;
            Check(CandyPlayerAvatar.WithStyle(requested, CandyPlayerAvatar.StyleBits(requested)).poweredArmor,
                "Existing powered armor bit round-trips");
            requested.poweredArmor = false;
            Click(view, "Done");
            Check(!view.WardrobeOpen, "Done closes the wardrobe");

            var details = (ILobbyDetails)lobby;
            var setup = details.Setup;
            setup.population = 1;
            setup.hands = 0;
            setup.rules.automaticDisasters = false;
            details.ConfigureSetup(setup);
            Check(lobby.SelectMode("demo") && lobby.SelectMap("castle"), "Classic Castle solo demo selected");
            var screen = lobby.GetComponent<MarinaScreen>();
            // The terminal re-arms on the next neutral frame after the wardrobe closes.
            deadline = Time.realtimeSinceStartup + 4;
            bool started = false;
            while (!(started = screen.Invoke("marina-play")) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Check(started, "Marina Play starts travel after the wardrobe closes");
            deadline = Time.realtimeSinceStartup + 45;
            while ((!(Services.Session is CastlePlaySession) || !NetworkActors.Alive(Services.Session.Player))
                && Time.realtimeSinceStartup < deadline) yield return null;
            var match = Services.Session as CastlePlaySession;
            Check(match && NetworkActors.Alive(match.Player), "Actual Game player is ready");
            var gameAvatar = match.Player.gameObject.GetComponent<CandyPlayerAvatar>();
            Check(gameAvatar && gameAvatar.Appearance.Equals(requested), "Selected wardrobe persists into Game");
            match.Modes.ReturnToLobby();
            deadline = Time.realtimeSinceStartup + 35;
            while (!(Services.Session is IslandLobby current && NetworkActors.Alive(current.Player))
                && Time.realtimeSinceStartup < deadline) yield return null;
            var returned = Services.Session as IslandLobby;
            Check(returned && NetworkActors.Alive(returned.Player), "Solo match returns to Marina");
            var returnedView = returned.View();
            returned.Player.Place(returnedView.WardrobePosition, Quaternion.identity);
            returnedView.SetWardrobeOpen(true);
            Check(returnedView.WardrobeOpen &&
                returned.Player.gameObject.GetComponent<CandyPlayerAvatar>().Appearance.Equals(requested),
                "Reopening after Game preserves the selected wardrobe");
            returnedView.SetWardrobeOpen(false);
        }

        static void Click(IslandLobbyView view, string name)
        {
            var entries = (System.Collections.Generic.List<Tuple<RectTransform, Action>>)
                typeof(IslandLobbyView).GetField("wardrobeButtons", Instance).GetValue(view);
            var entry = entries.Single(item => item.Item1.name == name);
            Check(entry.Item1.gameObject.activeInHierarchy, "Desktop item is reachable: " + name);
            entry.Item2();
        }

        static void Check(bool passed, string message)
        {
            File.AppendAllText(Report, (passed ? "PASS " : "FAIL ") + message + "\n");
            if (!passed) throw new InvalidOperationException(message);
        }

        static void Log(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            failed = true;
            File.AppendAllText(Report, message + "\n" + stack + "\n");
        }
    }
}
