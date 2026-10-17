using System;
using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleFusionSession
    {
        const int DisasterImpact = 10;
        const int DisasterVisuals = 11;
        [Serializable] sealed class DisasterPacket { public DisasterFrame[] frames; }
        sealed class RemoteDisaster : IDisasterState
        {public DisasterFrame frame;public float received;public DisasterFrame Capture()=>Time.unscaledTime-received<3?frame:null;}
        readonly List<RemoteDisaster> remoteDisasters=new List<RemoteDisaster>();
        float nextDisasterVisuals;
        byte[] disasterVisualPacket;
        void CaptureDisasters()
        {
            if(Time.unscaledTime<nextDisasterVisuals)return;
            nextDisasterVisuals=Time.unscaledTime+.2f;
            var frames=new List<DisasterFrame>();foreach(var source in DisasterStates.Active){var frame=source.Capture();if(frame!=null)frames.Add(frame);}
            disasterVisualPacket=CastleFusionWire.Compress(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new DisasterPacket{frames=frames.ToArray()})));
        }
        void ReceiveDisasters(byte[] data)
        {
            var packet=JsonUtility.FromJson<DisasterPacket>(System.Text.Encoding.UTF8.GetString(CastleFusionWire.Decompress(data)));
            if(packet?.frames==null||packet.frames.Length>32)throw new System.IO.InvalidDataException("Invalid disaster frame list");
            foreach(var source in remoteDisasters)DisasterStates.Active.Remove(source);remoteDisasters.Clear();
            foreach(var frame in packet.frames)
            {if(frame==null||frame.points?.Length>128||frame.blocks?.Length>20000)throw new System.IO.InvalidDataException("Invalid disaster detail");frame.sampledAt=Time.time;var source=new RemoteDisaster{frame=frame,received=Time.unscaledTime};remoteDisasters.Add(source);DisasterStates.Active.Add(source);}
        }
        [Serializable] sealed class Impact { public Vector3 push; public float damage; }
        readonly Dictionary<string, Impact> disasterImpacts = new Dictionary<string, Impact>();
        readonly RemoteWave remoteWave = new RemoteWave();

        sealed class RemoteWave : IWaveState
        {
            public WaveFrame frame;
            public float received;
            public WaveFrame Wave
            {
                get
                {
                    var result = frame;
                    float lag = Time.time - received;
                    if (lag > 3) result.active = false;
                    float dt = Mathf.Min(.25f, lag);
                    result.center += result.direction * result.speed * dt;
                    result.height = Mathf.Max(0, result.height + result.heightRate * dt);
                    result.warningRemaining = Mathf.Max(0, result.warningRemaining - dt);
                    result.remaining = Mathf.Max(0, result.remaining - dt);
                    return result;
                }
            }
        }

        WaveFrame CaptureWave()
        {
            foreach (var wave in Tracked.Waves) if (wave.Wave.active) return wave.Wave;
            return default;
        }
        void ReceiveWave(WaveFrame frame)
        {
            remoteWave.frame = frame; remoteWave.received = Time.time;
            if (frame.active && !Tracked.Waves.Contains(remoteWave)) Tracked.Waves.Add(remoteWave);
            if (!frame.active) Tracked.Waves.Remove(remoteWave);
        }
        public void QueueDisasterImpact(string actorId, Vector3 push, float damage)
        {
            if (!IsHost || closing) return;
            if (!disasterImpacts.TryGetValue(actorId, out var impact)) disasterImpacts[actorId] = impact = new Impact();
            impact.push = Vector3.ClampMagnitude(impact.push + push, 20);
            impact.damage = Mathf.Clamp(impact.damage + damage, 0, 10000);
        }
        void FlushDisasterImpacts()
        {
            foreach (var pair in peers)
                if (pair.Value.ready && disasterImpacts.TryGetValue(pair.Key.ToString(), out var impact))
                    Send(pair.Key, DisasterImpact, System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(impact)), ++sequence);
            disasterImpacts.Clear();
        }
        void ReceiveDisasterImpact(byte[] bytes)
        {
            var impact = JsonUtility.FromJson<Impact>(System.Text.Encoding.UTF8.GetString(bytes));
            if (impact == null || !session || !NetworkActors.Alive(session.Player) || session.PlayingHand) return;
            if (float.IsNaN(impact.push.sqrMagnitude) || float.IsInfinity(impact.push.sqrMagnitude) ||
                float.IsNaN(impact.damage) || float.IsInfinity(impact.damage)) return;
            var push = Vector3.ClampMagnitude(impact.push,30);
            if (push.sqrMagnitude > .000001f) session.Player.gameObject.GetComponent<IPushable>()?.Push(Vector3.ClampMagnitude(push, 20), gameObject);
            if (impact.damage > 0) session.Player.gameObject.GetComponent<IDamageable>()?.Damage(Mathf.Min(impact.damage, 10000), gameObject);
        }
        void ResetDisasters()
        {
            Tracked.Waves.Remove(remoteWave); remoteWave.frame = default; disasterImpacts.Clear();
            foreach(var source in remoteDisasters)DisasterStates.Active.Remove(source);remoteDisasters.Clear();disasterVisualPacket=null;nextDisasterVisuals=0;
            if (session) DemoDisasters.StopAll(session);
        }
    }
}
