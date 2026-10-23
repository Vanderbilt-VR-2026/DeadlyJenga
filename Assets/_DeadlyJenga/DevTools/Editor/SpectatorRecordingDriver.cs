using System;
using System.Collections;
using System.IO;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public sealed class SpectatorRecordingDriver
    {
        int oldCaptureRate, oldTargetRate, oldVsync;
        float oldScale;
        bool oldBackground, changed;

        public void StartCapture(MonoBehaviour host) => host.StartCoroutine(Guard());

        IEnumerator Guard()
        {
            var sequence = Capture();
            string failure = "";
            while (true)
            {
                bool next = false;
                object value = null;
                try { next = sequence.MoveNext(); if (next) value = sequence.Current; }
                catch (Exception e) { failure = e.ToString(); }
                if (!string.IsNullOrEmpty(failure) || !next) break;
                yield return value;
            }
            Restore();
            SpectatorVideoCapture.EndCapture(failure);
        }

        IEnumerator Capture()
        {
            var job = SpectatorVideoCapture.Job;
            var plan = job.plan;
            oldBackground = Application.runInBackground; oldCaptureRate = Time.captureFramerate;
            oldTargetRate = Application.targetFrameRate; oldVsync = QualitySettings.vSyncCount; oldScale = Time.timeScale;
            changed = true;
            Application.runInBackground = true;
            Time.timeScale = 1;
            yield return null;
            var session = Object.FindFirstObjectByType<CastlePlaySession>();
            if (!session || !session.Modes || session.IsVR) throw new InvalidOperationException("A ready desktop Game session is required.");
            // Core resets MatchChoices on entering Play Mode. Apply the saved job afterward.
            SpectatorVideoCapture.ApplyChoices(plan);
            MatchSetup.FromLobby = true;
            MatchSetup.StartLocalSession = false;
            session.SurvivorBots.AutoFill = false;
            session.SurvivorBots.Clear(); session.HandBots.Clear();
            var previousWorld = Services.World;
            var selectedMap = MatchSetup.SelectedMap;
            if (selectedMap == null) throw new InvalidOperationException("The requested map is missing from the catalog.");
            MatchSetup.ApplyMap();
            yield return new WaitForSeconds(1);
            if (Services.World == null || Services.World.Blocks.Count == 0 || (selectedMap.mapPrefab && ReferenceEquals(previousWorld, Services.World)))
                throw new InvalidOperationException("The selected map did not provide the active world.");
            if (!selectedMap.mapPrefab && UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != selectedMap.scenePath)
                throw new InvalidOperationException("The active scene does not match the selected map scene.");
            var rules = SpectatorRecordingPlan.Clone(plan.match.rules);
            rules.sameHumanTeam = plan.match.sameHumanTeam;
            session.Modes.Configure(rules);
            if (session.Modes.Rules.mode != plan.match.rules.mode) throw new InvalidOperationException("The selected mode settings were rejected.");
            session.SurvivorBots.AutoFill = false;
            session.SurvivorBots.Clear(); session.HandBots.Clear();
            yield return null;
            if (plan.match.survivorPreset < 0 || plan.match.survivorPreset >= MatchOptions.SurvivorPersonalities().Length || plan.match.handPreset < 0 || plan.match.handPreset >= MatchOptions.HandPersonalities().Length)
                throw new InvalidOperationException("The selected bot personality preset is unavailable.");
            session.SurvivorBots.TargetPopulation = plan.match.population;
            session.SurvivorBots.SetHumanPlayerCount(1);
            session.SurvivorBots.SelectPreset(plan.match.survivorPreset);
            session.HandBots.SelectPreset(plan.match.handPreset);
            (session.Player as ISurvivorSettings)?.ApplyMatchRules(SpectatorRecordingPlan.Clone(plan.match.movement));
            for (int i = 0; i < plan.match.population - 1; i++)
            {
                if (!session.SurvivorBots.SpawnOne()) throw new InvalidOperationException("Could not spawn survivor bot " + (i + 1) + ": " + session.SurvivorBots.Status);
                if (i % 5 == 4) yield return null;
            }
            for (int i = 0; i < plan.match.hands; i++)
                if (!session.HandBots.SpawnOne()) throw new InvalidOperationException("Could not spawn flying hand " + (i + 1));
            foreach (var hazard in plan.match.disasters ?? Array.Empty<LobbyHazardSetup>())
            {
                var prefab = Array.Find(DemoDisasters.Prefabs, p => p.name == hazard.id);
                if (prefab && hazard.values != null)
                    for (int i = 0; i < hazard.values.Length; i++) DemoDisasters.SetValue(session, prefab, i, hazard.values[i]);
            }
            session.Modes.StartRound();
            yield return new WaitForSeconds(.5f);
            if (session.SurvivorBots.Count != plan.match.population - 1 || session.HandBots.Count != plan.match.hands || MatchSetup.MapId != plan.mapId || session.Modes.Rules.mode != plan.match.rules.mode)
                throw new InvalidOperationException("Actual match population, mode, or map does not match the recording request.");
            var controls = session.Demo as ISpectatorControls;
            if (controls == null || !controls.CanUseFourViews) throw new InvalidOperationException("Four-view spectator mode is unavailable.");
            controls.SetFourViews(true); controls.SetSpectating(true); session.SetMenu(false);
            if (!controls.Spectating || !controls.FourViews) throw new InvalidOperationException("Four-view spectating did not activate.");
            File.WriteAllText(Path.Combine(job.runFolder, "actual-match.txt"), "Map: " + MatchSetup.SelectedMap.id + " (" + MatchSetup.SelectedMap.title + ")\nMode: " + session.Modes.Rules.mode + "\nSurvivor bots: " + session.SurvivorBots.Count + "\nAI hands: " + session.HandBots.Count + "\nWorld root: " + ((Services.World as Component)?.name ?? "unknown") + "\n");
            File.WriteAllText(Path.Combine(job.runFolder, "actual-rules.json"), JsonUtility.ToJson(session.Modes.Rules, true));
            int simulationFps = Mathf.CeilToInt(60f / plan.fps) * plan.fps;
            int sampleEvery = simulationFps / plan.fps;
            Time.captureFramerate = simulationFps;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = simulationFps;
            File.WriteAllText(Path.Combine(job.runFolder, "simulation.txt"), "Simulation FPS: " + simulationFps + "\nVideo FPS: " + plan.fps + "\nCapture samples every " + sampleEvery + " simulation updates.\n");
            File.WriteAllText(Path.Combine(job.runFolder,"bots.csv"),"seconds,id,action,state,reason,x,y,z,speed,desired,plannerAge,recoveries,objective\n");
            for (int tick = 0; tick < plan.seconds * simulationFps; tick++)
            {
                if(tick>0&&plan.viewSeconds>0&&tick%(simulationFps*plan.viewSeconds)==0)controls.CycleWatched(4);
                yield return new WaitForEndOfFrame();
                if (!SpectatorVideoCapture.Busy || job.cancel) yield break;
                if ((tick + 1) % sampleEvery != 0) continue;
                int frame = (tick + 1) / sampleEvery - 1;
                var image = ScreenCapture.CaptureScreenshotAsTexture();
                if (!image) throw new InvalidOperationException("Game View returned no image.");
                try
                {
                    if (frame > 0 && (image.width != job.width || image.height != job.height)) throw new InvalidOperationException("Game View size changed during recording. Keep its size fixed until this run finishes.");
                    File.WriteAllBytes(Path.Combine(job.runFolder, "frames", frame.ToString("D6") + ".jpg"), image.EncodeToJPG(85));
                    SpectatorVideoCapture.Progress(frame + 1, image.width, image.height);
                }
                finally { Object.Destroy(image); }
                if (frame % plan.fps == 0)
                {
                    var rows=new System.Text.StringBuilder();
                    var population=session.Bots();
                    if(population!=null)foreach(var bot in population.Bots)
                    {
                        var p=bot.transform.position;
                        rows.AppendLine(string.Join(",",((float)frame/plan.fps).ToString("F3",System.Globalization.CultureInfo.InvariantCulture),bot.GetInstanceID(),bot.CurrentAction,bot.MovementState,bot.StationaryReason.Replace(',',';'),p.x.ToString("F3",System.Globalization.CultureInfo.InvariantCulture),p.y.ToString("F3",System.Globalization.CultureInfo.InvariantCulture),p.z.ToString("F3",System.Globalization.CultureInfo.InvariantCulture),bot.ActualSpeed.ToString("F3",System.Globalization.CultureInfo.InvariantCulture),bot.DesiredSpeed.ToString("F3",System.Globalization.CultureInfo.InvariantCulture),bot.PlannerAge.ToString("F3",System.Globalization.CultureInfo.InvariantCulture),bot.Recoveries,bot.ObjectiveId));
                    }
                    File.AppendAllText(Path.Combine(job.runFolder,"bots.csv"),rows.ToString());
                }
                if (frame % plan.fps == 0)
                    for (int slot = 0; slot < 4; slot++)
                        File.AppendAllText(Path.Combine(job.runFolder, "targets.csv"), frame + "," + ((float)frame / plan.fps).ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "," + slot + ",\"" + controls.WatchedName(slot).Replace("\"", "\"\"") + "\"\n");
            }
        }

        public void Restore()
        {
            if (!changed) return;
            changed = false;
            Time.captureFramerate = oldCaptureRate; Time.timeScale = oldScale;
            Application.targetFrameRate = oldTargetRate; QualitySettings.vSyncCount = oldVsync;
            Application.runInBackground = oldBackground;
        }
    }
}
