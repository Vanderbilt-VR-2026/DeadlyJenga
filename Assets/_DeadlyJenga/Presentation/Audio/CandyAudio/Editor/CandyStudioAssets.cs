using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.CandyAudio.Editor
{
    public static class CandyStudioAssets
    {
        public static string RecordingFolder(CandyAudioBank bank)
        {
            string path = AssetDatabase.GetAssetPath(bank);
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("Choose a saved project audio bank first.");
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            int resources = parent.IndexOf("/Resources/", StringComparison.Ordinal);
            if (resources >= 0) parent = parent.Substring(0, resources);
            string folder = parent + "/StudioRecordings";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(parent, "StudioRecordings");
            return folder;
        }
        public static void PersistGeneratedClips(CandyAudioBank bank, CandyAudioCue cue)
        {
            for (int i = 0; i < cue.variants.Length; i++)
            {
                var clip = cue.variants[i]; if (!clip || AssetDatabase.Contains(clip)) continue;
                string path = AssetDatabase.GenerateUniqueAssetPath(RecordingFolder(bank) + "/Sound-" + (int)cue.sound + "-take-" + (i + 1) + ".wav");
                WriteWave(path, clip); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var settings = importer.defaultSampleSettings; settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM; importer.defaultSampleSettings = settings; importer.SaveAndReimport();
                cue.variants[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (!cue.variants[i]) throw new IOException("The baked clip failed to import: " + path);
            }
        }
        static void WriteWave(string path, AudioClip clip)
        {
            var samples = new float[checked(clip.samples * clip.channels)];
            if (!clip.GetData(samples, 0)) throw new InvalidOperationException("Cannot bake unreadable clip " + clip.name);
            int dataSize = checked(samples.Length * 2);
            using var stream = new FileStream(path, FileMode.CreateNew);
            using var writer = new BinaryWriter(stream);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + dataSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1);
            writer.Write((short)clip.channels); writer.Write(clip.frequency); writer.Write(clip.frequency * clip.channels * 2);
            writer.Write((short)(clip.channels * 2)); writer.Write((short)16); writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(dataSize);
            foreach (float sample in samples) writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -1, 1) * 32767));
        }
        public static List<string> Validate(CandyAudioBank bank)
        {
            var issues = new List<string>();
            if (!bank) { issues.Add("No bank selected."); return issues; }
            var ids = new HashSet<CandySound>();
            foreach (var cue in bank.cues ?? Array.Empty<CandyAudioCue>())
            {
                if (cue == null) { issues.Add("Null cue entry."); continue; }
                string label = string.IsNullOrWhiteSpace(cue.displayName) ? cue.sound.ToString() : cue.displayName;
                if (!ids.Add(cue.sound)) issues.Add(label + ": duplicate sound ID.");
                if (!cue.enabled) continue;
                if (cue.variants == null || cue.variants.Length == 0 || cue.variants.Any(c => !c)) issues.Add(label + ": missing recording.");
                else if (cue.variants.Any(c => !AssetDatabase.Contains(c))) issues.Add(label + ": temporary generated clip must be baked before saving.");
                if (cue.minDistance <= 0 || cue.maxDistance <= cue.minDistance) issues.Add(label + ": invalid distance range.");
                if (cue.pitch.x <= 0 || cue.pitch.y < cue.pitch.x) issues.Add(label + ": invalid pitch range.");
                if (cue.maxVoices < 1 || cue.maxVoices > 12 || cue.cooldown < 0) issues.Add(label + ": invalid voice/cooldown limit.");
            }
            foreach (var binding in bank.bindings ?? Array.Empty<CandyAudioBinding>())
            {
                if (binding == null || !binding.enabled) continue;
                var cue = CandySoundCatalog.Authored(bank, binding.sound);
                if (cue == null) issues.Add("Binding references a cue without a project override: " + binding.sound);
                if (cue != null && cue.loop && binding.trigger != CandyAudioTrigger.Manual) issues.Add("Event binding requires a one-shot, not a continuous loop: " + binding.sound);
            }
            return issues;
        }
    }
}
