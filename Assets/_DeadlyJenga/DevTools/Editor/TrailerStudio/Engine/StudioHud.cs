using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // What the performer sees in the Game view while playing an actor on desktop:
    // the countdown, a record light with the time left, and the keys.
    // This is DevTools UI. It only exists while the Studio's stage is open and never ships.
    public static class StudioHud
    {
        static GUIStyle big, small;

        public static void Update()
        {
            // In-game keys, so a take can be redone without leaving the Game view.
            var stage = StudioRun.Stage;
            var keys = Keyboard.current;
            if (stage == null || keys == null || stage.performer.Length == 0) return;
            try
            {
                if (keys.f9Key.wasPressedThisFrame)
                {
                    if (StudioRun.State == StudioState.Running) StudioRun.Stop();
                    else if (StudioRun.State == StudioState.Held) StudioPerform.Record(StudioRun.Frame);
                }
                if (keys.f10Key.wasPressedThisFrame) StudioPerform.Retake();
                if (keys.f8Key.wasPressedThisFrame) StudioPerform.PunchBack();
                if (keys.f7Key.wasPressedThisFrame) StudioPerform.CycleSpeed();
            }
            catch (System.Exception error) { Debug.LogWarning("Trailer Studio: " + error.Message); }
        }

        public static void OnGUI()
        {
            var stage = StudioRun.Stage;
            if (stage == null || stage.performer.Length == 0 || stage.session.IsVR) return;
            if (big == null)
            {
                big = new GUIStyle(GUI.skin.label) { fontSize = 120, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                small = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
            }
            var mark = stage.set.Mark(stage.performer);
            string who = mark?.actor ?? "";
            if (StudioRun.Countdown > 0)
            {
                Shadowed(new Rect(0, 0, Screen.width, Screen.height), Mathf.CeilToInt(StudioRun.Countdown).ToString(), big, Color.white);
                Shadowed(new Rect(0, Screen.height * .5f + 80, Screen.width, 40), "Get ready to play " + who,
                    new GUIStyle(small) { alignment = TextAnchor.UpperCenter }, Color.white);
                return;
            }
            float left = Mathf.Max(0, (StudioRun.LastFrame - StudioRun.Frame) / (float)stage.Fps);
            if (StudioRun.Recording)
            {
                bool lead = StudioRun.LeadIn;
                string speed = StudioRun.Pass != null && StudioRun.Pass.speed < .99f ? "   slow motion " + StudioPerform.SpeedName : "";
                Shadowed(new Rect(24, 20, 900, 30), lead ? "Rolling…  you take over in a moment" : $"● REC  {who}   {left:0.0} s left{speed}",
                    small, lead ? Color.white : new Color(1, .35f, .38f));
                Shadowed(new Rect(24, 46, 900, 30), "F9 stop    F10 again    F8 back 3 s and punch in", new GUIStyle(small) { fontSize = 13 }, new Color(1, 1, 1, .7f));
            }
            else if (StudioRun.State == StudioState.Held)
                Shadowed(new Rect(24, 20, 900, 30), $"You are {who}.   F9 record    F10 again    F8 back 3 s and punch in    F7 speed {StudioPerform.SpeedName}",
                    new GUIStyle(small) { fontSize = 14 }, new Color(1, 1, 1, .8f));
        }

        static void Shadowed(Rect rect, string text, GUIStyle style, Color color)
        {
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, .6f);
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), text, style);
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = old;
        }
    }
}
