using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;

namespace DeadlyJenga.Presentation
{
    // Shows the Kraken whenever a "Kraken" disaster frame is active (host or remote) and gives it sound:
    // a deep rumble as it rises, a splash when an arm surfaces or drags a block under, a crash on a slam.
    public sealed class KrakenPresentation : MonoBehaviour
    {
        static KrakenPresentation instance;
        KrakenVisual visual;
        readonly Dictionary<int, int> lastState = new Dictionary<int, int>();
        float lastRise;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance) return;
            var go = new GameObject("Kraken presentation");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<KrakenPresentation>();
        }

        void LateUpdate()
        {
            DisasterFrame frame = null;
            foreach (var source in DisasterStates.Active)
            {
                var f = source?.Capture();
                if (f != null && f.kind == "Kraken") { frame = f; break; }
            }
            if (frame == null)
            {
                if (visual) { Destroy(visual.gameObject); visual = null; }
                lastState.Clear(); lastRise = 0;
                return;
            }
            if (!visual)
            {
                var go = new GameObject("Kraken");
                go.transform.SetParent(transform, false);
                visual = go.AddComponent<KrakenVisual>();
            }
            visual.Show(frame, Time.deltaTime);
            Sounds(frame);
        }

        void Sounds(DisasterFrame f)
        {
            var e = CandyAudioEngine.Instance;
            if (!e || f.values == null || f.values.Length < 2) return;
            float rise = f.values[1];
            if (lastRise < .05f && rise >= .05f)
            {
                e.Play(CandySound.StructureLean, f.center, 1, GetInstanceID(), .35f);
                e.Play(CandySound.FallWind, f.center, 1, GetInstanceID() + 1, .4f);
            }
            lastRise = rise;
            if (f.points == null) return;
            foreach (var p in f.points)
            {
                int state = (int)p.size;
                lastState.TryGetValue(p.id, out int was);
                if (state == was) continue;
                lastState[p.id] = state;
                var arm = (KrakenVisual.KrakenArmState)state;
                if (arm == KrakenVisual.KrakenArmState.Resting) e.Play(CandySound.BlockSplash, p.velocity, .8f, GetInstanceID() + 10 + p.id, .6f);
                else if (arm == KrakenVisual.KrakenArmState.Dragging) e.Play(CandySound.PlayerSplash, p.velocity, 1, GetInstanceID() + 20 + p.id, .5f);
                else if (arm == KrakenVisual.KrakenArmState.Slamming) e.Play(CandySound.BlockSmash, p.position, 1, GetInstanceID() + 30 + p.id, .55f);
                else if (arm == KrakenVisual.KrakenArmState.Holding) e.Play(CandySound.RubberTouch, p.position, 1, GetInstanceID() + 40 + p.id, .5f);
            }
        }
    }
}
