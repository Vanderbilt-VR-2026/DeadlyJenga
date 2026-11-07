using System.Text.RegularExpressions;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // Desktop menu section for the survivor's VR and stamina settings.
    // Reads and changes them only through ISurvivorSettings (Core); the survivor draws nothing itself.
    public static class SurvivorSettingsPanel
    {
        static bool staminaOpen, handPhysicsOpen;

        // staminaEditable: false for joined players (the host owns stamina rules).
        // shareStamina: called with the new stamina rules after a change, so the host can share them.
        public static void Draw(ISurvivorSettings survivor, bool staminaEditable = true, System.Action<string> shareStamina = null, bool showComfort = true)
        {
            if (survivor == null) return;
            var rules = survivor.CurrentRules;
            bool changed = false;
            string staminaBefore = survivor.SaveStaminaRules();

            if(showComfort)
            {
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

            }

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
            DrawHandPhysics(survivor,Services.Session?.Demo?.CanCommand==true,shareStamina);
        }

        static void DrawHandPhysics(ISurvivorSettings survivor,bool editable,System.Action<string> share)
        {
            handPhysicsOpen=GUILayout.Toggle(handPhysicsOpen,"Flying hand drive / contact"+(editable?"":" (host only)"));
            if(!handPhysicsOpen)return;
            var physics=survivor.CurrentRules.handPhysics;
            float force=physics?.forceNewtons??3000,friction=physics?.friction??1.8f;
            bool previous=GUI.enabled;GUI.enabled=previous&&editable;
            GUILayout.Label("Hand drive force (N): "+force.ToString("0"));
            float position=Mathf.Sqrt(Mathf.Clamp(force,0,10000)/10000);
            float next=GUILayout.HorizontalSlider(position,0,1);
            if(!Mathf.Approximately(next,position))force=Mathf.Round(next*next*10000/10)*10;
            GUILayout.BeginHorizontal();
            foreach(float preset in new[]{0f,100f,300f,1000f,3000f})
                if(GUILayout.Button(preset.ToString("0")+" N"))force=preset;
            GUILayout.EndHorizontal();
            Slider("Contact friction: "+friction.ToString("0.00"),ref friction,0,2);
            if(GUILayout.Button("Reset hand drive / friction")){force=3000;friction=1.8f;}
            GUI.enabled=previous;
            GUILayout.Label("Drive budget only. Momentum can produce larger contact impulses. Zero disables drive; it does not freeze the hand.");
            if(editable&&(!Mathf.Approximately(force,physics?.forceNewtons??3000)||!Mathf.Approximately(friction,physics?.friction??1.8f)))
                SetHandPhysics(survivor,force,friction,true,share);
        }
        public static void SetHandPhysics(ISurvivorSettings survivor,float force,float friction,bool editable,System.Action<string> share)
        {
            if(!editable||survivor==null||float.IsNaN(force)||float.IsInfinity(force)||float.IsNaN(friction)||float.IsInfinity(friction))return;
            var rules=JsonUtility.FromJson<MatchRules>(JsonUtility.ToJson(survivor.CurrentRules));
            if(rules==null)return;
            rules.handPhysics=new HandPhysicsRules{forceNewtons=Mathf.Clamp(force,0,10000),friction=Mathf.Clamp(friction,0,2)};
            string before=survivor.SaveStaminaRules();survivor.ApplyMatchRules(rules);
            string after=survivor.SaveStaminaRules();if(after!=before)share?.Invoke(after);
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
