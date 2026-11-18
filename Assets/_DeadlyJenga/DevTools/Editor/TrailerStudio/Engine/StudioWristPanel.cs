using System;
using System.Collections.Generic;
using DeadlyJenga.CandyPlayers;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The in-headset panel for performing in VR. It sits on the off-hand (left) wrist.
    // Touch a button with the other hand to press it: record/stop, again, back 3 s and punch in,
    // ghosts on/off, slow motion. A big countdown floats in front of you before the tape rolls.
    // This is DevTools UI. It only exists while the Studio's stage is open and never ships.
    public sealed class StudioWristPanel
    {
        sealed class Key
        {
            public Transform body;
            public TextMesh label;
            public Action press;
            public bool down;
        }

        // Tests set this to show the panel without a headset.
        public static bool ForceVisible;

        GameObject panel;
        TextMesh status, countdown;
        readonly List<Key> keys = new List<Key>();
        Key recordKey, ghostKey, speedKey;
        Font font;

        public bool Built => panel;
        public int KeyCount => keys.Count;
        public Vector3 KeyPosition(int index) => keys[index].body.position;

        void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            panel = new GameObject("Trailer Studio wrist panel");
            var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.Destroy(back.GetComponent<Collider>());
            back.transform.SetParent(panel.transform, false);
            back.transform.localScale = new Vector3(.19f, .12f, .006f);
            Paint(back, new Color(.09f, .09f, .12f));
            status = Text(panel.transform, new Vector3(0, .043f, -.005f), .011f, Color.white);

            recordKey = Add("REC", new Vector3(-.06f, .008f, 0), new Color(.85f, .2f, .25f), () =>
            {
                if (StudioRun.State == StudioState.Running) StudioRun.Stop();
                else StudioPerform.Record(StudioRun.Frame);
            });
            Add("AGAIN", new Vector3(0, .008f, 0), new Color(.25f, .3f, .42f), StudioPerform.Retake);
            Add("BACK 3s", new Vector3(.06f, .008f, 0), new Color(.25f, .3f, .42f), () => StudioPerform.PunchBack());
            ghostKey = Add("GHOSTS", new Vector3(-.03f, -.036f, 0), new Color(.2f, .32f, .3f), StudioPerform.ToggleGhosts);
            speedKey = Add("1x", new Vector3(.03f, -.036f, 0), new Color(.2f, .32f, .3f), StudioPerform.CycleSpeed);

            var floating = new GameObject("Trailer Studio countdown");
            countdown = Text(floating.transform, Vector3.zero, .12f, Color.white);
            countdown.transform.SetParent(null);
            UnityEngine.Object.Destroy(floating);
        }

        Key Add(string text, Vector3 at, Color color, Action press)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(panel.transform, false);
            body.transform.localPosition = at + new Vector3(0, 0, -.006f);
            body.transform.localScale = new Vector3(.052f, .036f, .008f);
            Paint(body, color);
            var key = new Key { body = body.transform, press = press, label = Text(panel.transform, at + new Vector3(0, 0, -.012f), .008f, Color.white) };
            key.label.text = text;
            keys.Add(key);
            return key;
        }

        TextMesh Text(Transform parent, Vector3 at, float size, Color color)
        {
            var host = new GameObject("text");
            host.transform.SetParent(parent, false);
            host.transform.localPosition = at;
            var mesh = host.AddComponent<TextMesh>();
            mesh.font = font; mesh.fontSize = 64; mesh.characterSize = size * .16f;
            mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center; mesh.color = color;
            host.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return mesh;
        }

        static void Paint(GameObject thing, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color);
            thing.GetComponent<Renderer>().sharedMaterial = material;
        }

        public void LateUpdate()
        {
            var stage = StudioRun.Stage;
            bool show = stage != null && stage.performer.Length > 0 && (ForceVisible || stage.session.IsVR);
            if (!show) { if (panel) panel.SetActive(false); if (countdown) countdown.gameObject.SetActive(false); return; }
            if (!panel) Build();
            panel.SetActive(true);

            var body = stage.Live(stage.performer);
            var avatar = body ? body.GetComponent<CandyPlayerAvatar>() : null;
            var camera = stage.session.ActiveCamera;
            Vector3 pointer;
            if (avatar && avatar.LastFrame.scale > 0)
            {
                // On the back of the left wrist, facing the performer's eyes.
                var frame = avatar.LastFrame;
                Vector3 wrist = frame.left.position;
                Vector3 toEyes = (frame.head - wrist).normalized;
                panel.transform.SetPositionAndRotation(wrist + toEyes * .06f + Vector3.up * .05f, Quaternion.LookRotation(-toEyes, Vector3.up));
                pointer = frame.right.position;
            }
            else if (camera)
            {
                // Flying the hand: there is no wrist, so the panel floats low and to the left of the view.
                var view = camera.transform;
                panel.transform.SetPositionAndRotation(view.position + view.forward * .45f - view.right * .22f - view.up * .18f,
                    Quaternion.LookRotation(view.forward, view.up));
                pointer = view.position + view.forward * 10;   // nothing to press with; use the keys or the Studio window
            }
            else return;

            float left = Mathf.Max(0, (StudioRun.LastFrame - StudioRun.Frame) / (float)stage.Fps);
            status.text = StudioRun.Recording ? (StudioRun.LeadIn ? "ROLLING" : $"REC  {left:0.0}s left") : "READY";
            status.color = StudioRun.Recording && !StudioRun.LeadIn ? new Color(1, .4f, .42f) : Color.white;
            recordKey.label.text = StudioRun.State == StudioState.Running ? "STOP" : "REC";
            ghostKey.label.text = StudioPerform.GhostsHidden ? "GHOSTS off" : "GHOSTS on";
            speedKey.label.text = StudioPerform.SpeedName;

            foreach (var key in keys) Touch(key, pointer);

            if (!countdown) return;
            bool counting = StudioRun.Countdown > 0 && camera;
            countdown.gameObject.SetActive(counting);
            if (!counting) return;
            countdown.text = Mathf.CeilToInt(StudioRun.Countdown).ToString();
            countdown.transform.SetPositionAndRotation(camera.transform.position + camera.transform.forward * 1.4f, camera.transform.rotation);
        }

        // A key is pressed when the other hand comes within 3 cm, and is ready again once the hand has moved away.
        void Touch(Key key, Vector3 pointer)
        {
            float distance = Vector3.Distance(pointer, key.body.position);
            if (!key.down && distance < .03f)
            {
                key.down = true;
                try { key.press(); } catch (Exception error) { Debug.LogWarning("Trailer Studio: " + error.Message); }
            }
            else if (key.down && distance > .06f) key.down = false;
        }

        // Tests press a key the same way a fingertip would.
        public void PressForTest(int index) => Touch(keys[index], keys[index].body.position);

        public void Dispose()
        {
            if (panel) UnityEngine.Object.Destroy(panel);
            if (countdown) UnityEngine.Object.Destroy(countdown.gameObject);
        }
    }
}
