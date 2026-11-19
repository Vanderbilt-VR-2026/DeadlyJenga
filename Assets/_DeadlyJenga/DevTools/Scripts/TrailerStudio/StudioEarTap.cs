#if UNITY_EDITOR
using System;
using System.Threading;
using UnityEngine;

namespace DeadlyJenga.DevTools.TrailerStudio
{
    // Copies what an AudioListener hears (everything, after it is mixed), so the Studio can save
    // the sound of a performed take. Put it on the same GameObject as the active AudioListener.
    // Unity calls OnAudioFilterRead on the audio thread, so this only copies numbers; the editor
    // code takes them out with Drain once per frame and writes the file.
    public sealed class StudioEarTap : MonoBehaviour
    {
        readonly object gate = new object();
        float[] buffer = new float[96000];
        int count;
        long callbacks;
        volatile bool recording;
        volatile int channels;

        public bool Recording { get => recording; set => recording = value; }
        public int Channels => channels;
        public long Callbacks => Interlocked.Read(ref callbacks);

        void OnAudioFilterRead(float[] data, int channelCount)
        {
            Interlocked.Increment(ref callbacks);
            channels = channelCount;
            if (!recording) return;
            lock (gate)
            {
                if (count + data.Length > buffer.Length) Array.Resize(ref buffer, Math.Max(buffer.Length * 2, count + data.Length));
                Array.Copy(data, 0, buffer, count, data.Length);
                count += data.Length;
            }
        }

        // Main thread: move everything heard since the last call into "into". Returns how many samples.
        public int Drain(ref float[] into)
        {
            lock (gate)
            {
                if (into == null || into.Length < count) into = new float[Math.Max(count, 4096)];
                Array.Copy(buffer, into, count);
                int taken = count;
                count = 0;
                return taken;
            }
        }
    }
}
#endif
