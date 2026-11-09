using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;

namespace DeadlyJenga.Presentation
{
    // Uses the same Kraken frame for host and remote presentation.
    public sealed class KrakenPresentation : MonoBehaviour
    {
        static KrakenPresentation instance;
        KrakenVisual visual;
        readonly Dictionary<int, int> lastState = new Dictionary<int, int>();
        readonly HashSet<int> soundedSlam = new HashSet<int>();
        float lastRise;
        bool retreatPlayed;

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
                lastState.Clear(); soundedSlam.Clear(); lastRise = 0; retreatPlayed = false;
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
            int owner = GetInstanceID();
            if(lastRise<.05f && rise>=.05f)
                e.Play(CandySound.KrakenRise,f.center,1,owner);
            if(!retreatPlayed && f.remaining<=0 && lastRise>.05f)
            {
                retreatPlayed=true;
                e.Play(CandySound.KrakenRetreat,f.center,1,owner);
            }
            lastRise=rise;
            if(rise>.05f) e.Loop(CandySound.KrakenIdle,visual.SoundAnchor,rise*.7f);
            if(f.points==null)return;

            foreach(var p in f.points)
            {
                int state=(int)p.size;
                bool seen=lastState.TryGetValue(p.id,out int old);
                var arm=(KrakenVisual.KrakenArmState)state;
                int armOwner=unchecked(owner*31+p.id);

                if(!seen || state!=old)
                {
                    soundedSlam.Remove(p.id);
                    lastState[p.id]=state;
                    switch(arm)
                    {
                        case KrakenVisual.KrakenArmState.Resting:
                            e.Play(CandySound.KrakenSurface,p.velocity,.85f,armOwner); break;
                        case KrakenVisual.KrakenArmState.Reaching:
                            e.Play(CandySound.KrakenReach,p.position,.75f,armOwner); break;
                        case KrakenVisual.KrakenArmState.Holding:
                            e.Play(CandySound.KrakenGrip,p.position,1,armOwner); break;
                        case KrakenVisual.KrakenArmState.Dragging:
                            e.Play(CandySound.KrakenDrag,p.position,1,armOwner); break;
                        case KrakenVisual.KrakenArmState.Rearing:
                            e.Play(CandySound.KrakenWindup,p.position,.9f,armOwner); break;
                        case KrakenVisual.KrakenArmState.Sinking:
                            if(seen)e.Play(CandySound.KrakenSubmerge,p.velocity,.7f,armOwner); break;
                    }
                }
                if(arm==KrakenVisual.KrakenArmState.Slamming && !soundedSlam.Contains(p.id) &&
                    (p.life>=.68f || p.life>=.15f && Physics.CheckSphere(p.position,4.5f,
                        (1<<Layers.Block)|(1<<Layers.Survivor),QueryTriggerInteraction.Collide)))
                {
                    soundedSlam.Add(p.id);
                    e.Play(CandySound.KrakenSlam,p.position,1,armOwner);
                }
            }
        }
    }
}
