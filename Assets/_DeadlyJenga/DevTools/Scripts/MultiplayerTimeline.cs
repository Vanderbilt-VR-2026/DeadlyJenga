#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // Opt-in test recorder. Launch a development player with --dj-timeline=<folder> --dj-smoke=<role>.
    // Once a second it writes one JSON line: connection, frame times, and a pose list of every block,
    // so separate processes can be diffed afterwards (Logs/MPGate/diff.py).
    public sealed class MultiplayerTimeline : MonoBehaviour
    {
        string file;
        float nextWrite;
        readonly float[] frames = new float[1024];
        int frameCount;
        readonly StringBuilder line = new StringBuilder(64 * 1024);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            string folder = null, role = "solo";
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("--dj-timeline=")) folder = arg.Substring(14);
                if (arg.StartsWith("--dj-smoke=")) role = arg.Substring(11);
            }
            if (folder == null) return;
            Directory.CreateDirectory(folder);
            var go = new GameObject("Multiplayer timeline recorder");
            DontDestroyOnLoad(go);
            go.AddComponent<MultiplayerTimeline>().file = Path.Combine(folder, role + "-timeline.jsonl");
        }

        void Update()
        {
            if (frameCount < frames.Length) frames[frameCount++] = Time.unscaledDeltaTime * 1000f;
            if (Time.realtimeSinceStartup < nextWrite) return;
            nextWrite = Time.realtimeSinceStartup + 1f;
            Write();
            frameCount = 0;
        }

        void Write()
        {
            var inv = CultureInfo.InvariantCulture;
            Array.Sort(frames, 0, frameCount);
            float p50 = frameCount > 0 ? frames[frameCount / 2] : 0;
            float p95 = frameCount > 0 ? frames[Mathf.Min(frameCount - 1, (int)(frameCount * .95f))] : 0;
            float max = frameCount > 0 ? frames[frameCount - 1] : 0;
            var room = IslandRoomConnection.Instance;
            var session = FindFirstObjectByType<CastlePlaySession>();
            line.Clear();
            line.Append("{\"t\":").Append(Time.realtimeSinceStartup.ToString("F1", inv));
            line.Append(",\"utc\":").Append(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            line.Append(",\"scene\":\"").Append(SceneManager.GetActiveScene().name).Append('"');
            line.Append(",\"connected\":").Append(room && room.Connected ? "true" : "false");
            line.Append(",\"host\":").Append(room && room.IsHost ? "true" : "false");
            line.Append(",\"humans\":").Append(room ? room.HumanCount : 0);
            line.Append(",\"code\":\"").Append(room ? room.Code : "").Append('"');
            line.Append(",\"waiting\":").Append(session && session.Network && session.Network.WaitingForWorld ? "true" : "false");
            line.Append(",\"frames\":").Append(frameCount);
            line.Append(",\"p50\":").Append(p50.ToString("F1", inv));
            line.Append(",\"p95\":").Append(p95.ToString("F1", inv));
            line.Append(",\"max\":").Append(max.ToString("F1", inv));
            line.Append(",\"bots\":").Append(session && session.SurvivorBots != null ? session.SurvivorBots.Count : 0);
            line.Append(",\"hazards\":").Append(Tracked.Waves.Count);
            var blocks = Services.World?.Blocks;
            line.Append(",\"blocks\":[");
            if (blocks != null)
                for (int i = 0; i < blocks.Count; i++)
                {
                    var b = blocks[i];
                    if (i > 0) line.Append(',');
                    if (!b || !b.gameObject.activeInHierarchy) { line.Append("null"); continue; }
                    var p = b.transform.position;
                    var e = b.transform.rotation;
                    line.Append('[').Append(p.x.ToString("F2", inv)).Append(',').Append(p.y.ToString("F2", inv)).Append(',').Append(p.z.ToString("F2", inv))
                        .Append(',').Append(e.x.ToString("F3", inv)).Append(',').Append(e.y.ToString("F3", inv)).Append(',').Append(e.z.ToString("F3", inv)).Append(',').Append(e.w.ToString("F3", inv))
                        .Append(',').Append(b.isKinematic ? 1 : 0).Append(',').Append(b.linearVelocity.sqrMagnitude > .05f ? 1 : 0).Append(']');
                }
            line.Append("]}\n");
            try { File.AppendAllText(file, line.ToString()); } catch (IOException) { }
        }
    }
}
#endif
