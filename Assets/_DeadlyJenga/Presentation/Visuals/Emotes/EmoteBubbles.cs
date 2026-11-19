using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;

namespace DeadlyJenga.CastlePrototype
{
    // A pop-up speech bubble over whoever emotes or taunts, plus the voice line when nothing else plays it
    // (other players' survivors and giant-hand taunts).
    public sealed class EmoteBubbles : MonoBehaviour
    {
        const float Life = 2.4f;
        static EmoteBubbles instance;

        sealed class Bubble { public GameObject root; public Transform who; public Renderer[] renderers; public IHandLook hand; public float born; public CanvasGroup group; }
        readonly List<Bubble> bubbles = new List<Bubble>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance) return;
            var go = new GameObject("Emote bubbles");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<EmoteBubbles>();
        }

        void OnEnable() => GameEvents.Emoted += OnEmoted;
        void OnDisable() => GameEvents.Emoted -= OnEmoted;

        void OnEmoted(GameObject who, string emote)
        {
            if (!who) return;
            string text = EmoteText.Bubble(emote);
            if (text == null) return;
            var hand = who.GetComponent<IHandLook>();
            // Remove an older bubble on the same person.
            for (int i = bubbles.Count - 1; i >= 0; i--) if (bubbles[i].who == who.transform) { Destroy(bubbles[i].root); bubbles.RemoveAt(i); }

            var root = new GameObject("Emote bubble", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)root.transform).sizeDelta = new Vector2(400, 90);
            var bg = root.AddComponent<Image>(); bg.sprite = CandyHudVisuals.Pill; bg.type = Image.Type.Sliced; bg.color = hand != null ? new Color(.35f, .05f, .2f, .92f) : new Color(1, 1, 1, .95f);
            var group = root.AddComponent<CanvasGroup>();
            var label = new GameObject("Text", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(root.transform, false);
            var t = label.GetComponent<Text>(); t.text = text; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 36; t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.color = hand != null ? Color.white : CandyHudVisuals.Ink;
            t.rectTransform.sizeDelta = new Vector2(400, 90);

            bubbles.Add(new Bubble { root = root, who = who.transform, renderers = hand != null ? null : who.GetComponentsInChildren<Renderer>(), hand = hand, born = Time.time, group = group });
            Voice(who, emote, hand != null);
        }

        static void Voice(GameObject who, string emote, bool isHand)
        {
            var engine = CandyAudioEngine.Instance;
            if (!engine) return;
            var at = who.transform.position;
            if (isHand)
            {
                var (sound, pitch) = emote switch
                {
                    "FingerWag" => (CandySound.NopeNopeNope, .6f), "ThumbsDown" => (CandySound.WhatWasThat, .55f),
                    "SlowClap" => (CandySound.GolfClap, .5f), _ => (CandySound.TotallyMeantThat, .6f)
                };
                engine.Play(sound, ((IHandLook)who.GetComponent<IHandLook>()).Position, 1, who.GetInstanceID(), pitch);
                return;
            }
            // The player's own survivor and bots already voice their emotes (CandyPlayerAudio).
            if (who.GetComponentInParent<CandyPlayerAudio>()) return;
            if (System.Enum.TryParse<CandySound>(emote, out var cue)) engine.Play(cue, at + Vector3.up * 1.5f, .8f, who.GetInstanceID());
        }

        void LateUpdate()
        {
            var cam = Services.Session?.ActiveCamera;
            if (!cam) cam = Camera.main;
            for (int i = bubbles.Count - 1; i >= 0; i--)
            {
                var b = bubbles[i];
                float age = Time.time - b.born;
                if (age > Life || !b.who) { Destroy(b.root); bubbles.RemoveAt(i); continue; }
                var t = b.root.transform;
                t.position = Top(b) + Vector3.up * (age * .15f);
                if (cam) t.rotation = Quaternion.LookRotation(t.position - cam.transform.position, Vector3.up);
                float pop = age < .15f ? Mathf.SmoothStep(0, 1.15f, age / .15f) : Mathf.Lerp(1.15f, 1, Mathf.Clamp01((age - .15f) / .1f));
                t.localScale = Vector3.one * (b.hand != null ? .009f : .0028f) * pop;
                b.group.alpha = Mathf.Clamp01((Life - age) / .4f);
            }
        }

        static Vector3 Top(Bubble b)
        {
            if (b.hand != null) return b.hand.Position + Vector3.up * 3.5f;
            bool any = false; var bounds = new Bounds();
            if (b.renderers != null)
                foreach (var r in b.renderers)
                {
                    if (!r || !r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer || r is TrailRenderer) continue;
                    if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
                }
            return any ? new Vector3(bounds.center.x, bounds.max.y + .45f, bounds.center.z) : b.who.position + Vector3.up * 2.3f;
        }
    }
}
