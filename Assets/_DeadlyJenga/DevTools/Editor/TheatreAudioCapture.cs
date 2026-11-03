using System;
using System.IO;
using System.Text;
using Unity.Collections;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    // Unity's offline mixer advances on the same fixed frame clock as the picture.
    sealed class TheatreAudioCapture : IDisposable
    {
        readonly BinaryWriter writer;
        readonly int channels, rate;
        int bytes;
        bool disposed;

        public TheatreAudioCapture(string path)
        {
            var config = AudioSettings.GetConfiguration();
            rate = config.sampleRate;
            channels = config.speakerMode switch
            {
                AudioSpeakerMode.Mono => 1, AudioSpeakerMode.Quad => 4,
                AudioSpeakerMode.Surround => 5, AudioSpeakerMode.Mode5point1 => 6,
                AudioSpeakerMode.Mode7point1 => 8, _ => 2
            };
            if (!AudioRenderer.Start()) throw new InvalidOperationException("Unity audio capture is already active.");
            try
            {
                writer = new BinaryWriter(File.Open(path, FileMode.CreateNew), Encoding.ASCII);
                Header();
            }
            catch
            {
                writer?.Dispose();
                AudioRenderer.Stop();
                throw;
            }
        }

        void Header()
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + bytes);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)channels); writer.Write(rate);
            writer.Write(rate * channels * 2); writer.Write((short)(channels * 2)); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
        }

        public void CaptureFrame()
        {
            int count = AudioRenderer.GetSampleCountForCaptureFrame() * channels;
            using var samples = new NativeArray<float>(count, Allocator.Temp);
            if (!AudioRenderer.Render(samples)) throw new InvalidOperationException("Unity audio capture stopped.");
            for (int i = 0; i < count; i++)
                writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue));
            bytes += count * 2;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            AudioRenderer.Stop();
            try { writer.BaseStream.Position = 0; Header(); }
            finally { writer.Dispose(); }
        }
    }
}
