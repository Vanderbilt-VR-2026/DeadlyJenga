using System.Text.RegularExpressions;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // Desktop menu section for the survivor's VR and stamina settings.
    // Reads and changes them only through ISurvivorSettings (Core); the survivor draws nothing itself.
    public static class SurvivorSettingsPanel
    {
        static bool staminaOpen;

        // staminaEditable: false for joined players (the host owns stamina rules).
        // shareStamina: called with the new stamina rules after a change, so the host can share them.
        public static void Draw(ISurvivorSettings survivor, bool staminaEditable = true, System.Action<string> shareStamina = null)
        {
            if (survivor == null) return;
            var rules = survivor.CurrentRules;
            bool changed = false;
            string staminaBefore = survivor.SaveStaminaRules();

            GUILayout.Label(survivor.ControlDescription);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Seated / smooth turn")) survivor.UseStationaryPreset();
            if (GUILayout.Button("Standing / physical turn")) survivor.UseStandingPreset();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Standing jump gesture: " + survivor.JumpGestureLabel)) survivor.CycleJumpGesture();
            if (!survivor.StandingPlay && GUILayout.Button("Recalibrate seated eye height")) survivor.RecalibrateSeatedHeight();
            if (GUILayout.Button("Movement: " + survivor.MovementLabel)) survivor.CycleMovement();
            if (GUILayout.Button("Right-stick turn: " + survivor.TurnLabel)) survivor.CycleTurn();
            changed |= Slider("Snap: " + rules.snapAngle.ToString("0") + "\u00b0", ref rules.snapAngle, 15, 90);
            changed |= Slider("Smooth: " + rules.smoothDegrees.ToString("0") + "\u00b0 / sec", ref rules.smoothDegrees, 15, 180);

            staminaOpen = GUILayout.Toggle(staminaOpen, "Live climbing stamina settings" + (staminaEditable ? "" : " (host only)"));
            if (staminaOpen)
            {
                bool wasEnabled = GUI.enabled; GUI.enabled = staminaEditable;
                changed |= Slider("Capacity: " + rules.gripSeconds.ToString("0.0") + " effort units", ref rules.gripSeconds, .5f, 20);
                changed |= Slider("Drain: " + rules.drainRate.ToString("0.00") + " units / second", ref rules.drainRate, 0, 4);
                changed |= Slider("Recovery: " + (rules.recoveryRate * 100).ToString("0") + "% / second (secure footing)", ref rules.recoveryRate, 0, 2);
                changed |= Curve("Drain curve", ref rules.drainCurve);
                changed |= Slider("Drain degree: " + rules.drainDegree.ToString("0.0"), ref rules.drainDegree, 1, 6);
                changed |= Curve("Recovery curve", ref rules.recoveryCurve);
                changed |= Slider("Recovery degree: " + rules.recoveryDegree.ToString("0.0"), ref rules.recoveryDegree, 1, 6);
                GUILayout.Label("Power in starts gently, power out starts strongly. Exponential accelerates. Degree 1 makes power curves linear. Changes keep the current stamina fraction.");
                GUI.enabled = wasEnabled;
            }
            if (changed) survivor.ApplyMatchRules(rules);
            if (changed && survivor.SaveStaminaRules() != staminaBefore) shareStamina?.Invoke(survivor.SaveStaminaRules());
        }

        static bool Slider(string label, ref float value, float min, float max)
        {
            GUILayout.Label(label);
            float next = GUILayout.HorizontalSlider(value, min, max);
            if (Mathf.Approximately(next, value)) return false;
            value = next; return true;
        }

        static bool Curve(string label, ref string value)
        {
            var names = MatchOptions.Curves;
            GUILayout.Label(label);
            var shown = new string[names.Length];
            for (int i = 0; i < names.Length; i++) shown[i] = Pretty(names[i]);
            int current = System.Array.IndexOf(names, value);
            int next = GUILayout.SelectionGrid(current, shown, 2);
            if (next == current || next < 0) return false;
            value = names[next]; return true;
        }

        // "PowerIn" -> "Power in"
        static string Pretty(string name)
        {
            var spaced = Regex.Replace(name, "(?<=[a-z])([A-Z])", " $1");
            return spaced.Length > 1 ? spaced[0] + spaced.Substring(1).ToLowerInvariant() : spaced;
        }
    }
}
