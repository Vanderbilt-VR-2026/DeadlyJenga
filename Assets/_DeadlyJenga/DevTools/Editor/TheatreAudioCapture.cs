using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DeadlyJenga.CandyAudio;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace DeadlyJenga.DevTools.Editor
{
    sealed class TheatreAudioCapture : IDisposable
    {
        sealed class WaveFile : IDisposable
        {
            readonly BinaryWriter writer;
            readonly int channels, rate;
            int bytes;
            public WaveFile(string path, int channels, int rate)
            {
                this.channels = channels;
                this.rate = rate;
                writer = new BinaryWriter(File.Open(path, FileMode.CreateNew), Encoding.ASCII);
                Header();
            }

            void Header()
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + bytes);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)channels); writer.Write(rate);
                writer.Write(rate * channels * 2); writer.Write((short)(channels * 2)); writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
            }

            public void Write(NativeArray<float> samples)
            {
                for (int i = 0; i < samples.Length; i++)
                    writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue));
                bytes += samples.Length * 2;
            }

            public void Complete(int frames, int fps)
            {
                int expected = (int)Math.Round(frames * (double)rate / fps) * channels * 2;
                int padding = expected - bytes;
                if (padding < 0) writer.BaseStream.SetLength(44 + expected);
                else if (padding > 0) writer.Write(new byte[padding]);
                bytes = expected;
            }

            public void Dispose()
            {
                try { writer.BaseStream.Position = 0; Header(); }
                finally { writer.Dispose(); }
            }
        }

        delegate bool MixerSink(AudioMixerGroup group, NativeArray<float> samples, bool excludeFromMix);
        const string MixerName = "TheatreCapture";
        static readonly string[] StemNames = { "World", "Character", "Ambience", "Music", "UI", "Voice", "Other" };
        static readonly FieldInfo Voices = typeof(CandyAudioEngine).GetField("voices", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly Type VoiceType = Voices?.FieldType.GetGenericArguments()[0];
        static readonly FieldInfo VoiceSource = VoiceType?.GetField("source");
        static readonly FieldInfo VoiceCue = VoiceType?.GetField("cue");
        readonly Dictionary<AudioSource, AudioMixerGroup> original = new();
        readonly HashSet<AudioSource> pooled = new();
        readonly AudioMixerGroup[] groups;
        readonly WaveFile[] stems;
        readonly MixerSink sink;
        readonly WaveFile master;
        readonly int channels;
        bool disposed;

        // The mixer asset is created only by an explicit editor command.
        [MenuItem("Tools/Deadly Jenga/Install Theatre Stem Mixer")]
        public static void InstallMixer()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Install the Theatre mixer in Edit Mode.");
            if (LoadMixer()) return;
            var type = typeof(AudioImporter).Assembly.GetType("UnityEditor.Audio.AudioMixerController", true);
            string path = ProjectPath.FolderOf("GameplayTheatre.cs") + MixerName + ".mixer";
            var mixer = (AudioMixer)type.GetMethod("CreateMixerControllerAtPath").Invoke(null, new object[] { path });
            var parent = type.GetProperty("masterGroup").GetValue(mixer);
            foreach (string name in StemNames)
            {
                var group = type.GetMethod("CreateNewGroup").Invoke(mixer, new object[] { name, false });
                type.GetMethod("AddChildToParent").Invoke(mixer, new[] { group, parent });
            }
            EditorUtility.SetDirty(mixer);
            AssetDatabase.SaveAssets();
        }

        static AudioMixer LoadMixer()
        {
            try { return AssetDatabase.LoadAssetAtPath<AudioMixer>(ProjectPath.Find(MixerName + ".mixer")); }
            catch (FileNotFoundException) { return null; }
        }

        public TheatreAudioCapture(string path)
        {
            var config = AudioSettings.GetConfiguration();
            channels = config.speakerMode switch
            {
                AudioSpeakerMode.Mono => 1, AudioSpeakerMode.Quad => 4,
                AudioSpeakerMode.Surround => 5, AudioSpeakerMode.Mode5point1 => 6,
                AudioSpeakerMode.Mode7point1 => 8, _ => 2
            };
            var mixer = LoadMixer();
            if (mixer)
            {
                var method = typeof(AudioRenderer).GetMethod("AddMixerGroupSink", BindingFlags.Static | BindingFlags.NonPublic);
                if (method == null || VoiceSource == null || VoiceCue == null)
                    throw new NotSupportedException("This Unity or game version changed the Theatre audio routing adapter.");
                sink = (MixerSink)method.CreateDelegate(typeof(MixerSink));
                groups = StemNames.Select(name => mixer.FindMatchingGroups("Master/" + name).Single()).ToArray();
                stems = new WaveFile[groups.Length];
            }
            if (!AudioRenderer.Start()) throw new InvalidOperationException("Unity audio capture is already active.");
            try
            {
                master = new WaveFile(path, channels, config.sampleRate);
                if (stems == null) return;
                string folder = Path.Combine(Path.GetDirectoryName(path), "stems");
                Directory.CreateDirectory(folder);
                for (int i = 0; i < stems.Length; i++)
                    stems[i] = new WaveFile(Path.Combine(folder, StemNames[i] + ".wav"), channels, config.sampleRate);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        void Route(AudioSource source, int group)
        {
            var current = source.outputAudioMixerGroup;
            if (current == groups[group]) return;
            if (!original.ContainsKey(source)) original.Add(source, current);
            else if (!groups.Contains(current)) original[source] = current;
            source.outputAudioMixerGroup = groups[group];
        }

        void RouteSources()
        {
            pooled.Clear();
            var engine = CandyAudioEngine.Instance;
            if (engine && Voices.GetValue(engine) is IEnumerable voices)
                foreach (var voice in voices)
                {
                    var source = (AudioSource)VoiceSource.GetValue(voice);
                    if (!source) continue;
                    pooled.Add(source);
                    if (VoiceCue.GetValue(voice) is CandyAudioCue cue) Route(source, (int)cue.bus);
                }
            foreach (var source in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (!source.isPlaying || pooled.Contains(source)) continue;
                int group = source.GetComponentInParent<CandyHomeMusic>() || source.GetComponentInParent<CandyLobbyMusic>()
                    ? 3 : source.GetComponentInParent<VoicePlayer>() ? 5 : 6;
                Route(source, group);
            }
        }

        public void CaptureFrame()
        {
            int count = AudioRenderer.GetSampleCountForCaptureFrame() * channels;
            using var samples = new NativeArray<float>(count, Allocator.Temp);
            NativeArray<float>[] split = null;
            try
            {
                if (stems != null)
                {
                    RouteSources();
                    split = new NativeArray<float>[stems.Length];
                    if (count > 0)
                        for (int i = 0; i < split.Length; i++)
                        {
                            split[i] = new NativeArray<float>(count, Allocator.Temp);
                            if (!sink(groups[i], split[i], false))
                                throw new InvalidOperationException("Unity rejected the " + StemNames[i] + " audio sink.");
                        }
                }
                if (!AudioRenderer.Render(samples)) throw new InvalidOperationException("Unity audio capture stopped.");
                master.Write(samples);
                if (split != null && count > 0)
                    for (int i = 0; i < split.Length; i++) stems[i].Write(split[i]);
            }
            finally
            {
                if (split != null)
                    foreach (var buffer in split) if (buffer.IsCreated) buffer.Dispose();
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            AudioRenderer.Stop();
            foreach (var pair in original)
                if (pair.Key) pair.Key.outputAudioMixerGroup = pair.Value;
            master?.Dispose();
            if (stems != null) foreach (var stem in stems) stem?.Dispose();
        }

        public void Complete(int frames, int fps)
        {
            master.Complete(frames, fps);
            if (stems != null) foreach (var stem in stems) stem.Complete(frames, fps);
        }
    }
}
