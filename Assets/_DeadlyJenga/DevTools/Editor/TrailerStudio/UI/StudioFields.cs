using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Form fields that change the Set with undo. Every inspector uses these, so they all behave the same.
    public static class StudioFields
    {
        static T Styled<T>(T field) where T : VisualElement { field.AddToClassList("ts-field"); return field; }

        public static TextField Text(StudioContext context, string label, string value, Action<string> apply, bool structural = false, bool multiline = false)
        {
            var field = Styled(new TextField(label) { value = value ?? "", isDelayed = true, multiline = multiline });
            if (multiline) field.style.minHeight = 54;
            field.RegisterValueChangedCallback(e => context.Edit("Change " + label.ToLowerInvariant(), () => apply(e.newValue), structural));
            return field;
        }

        public static FloatField Number(StudioContext context, string label, float value, Action<float> apply, bool structural = false)
        {
            var field = Styled(new FloatField(label) { value = value, isDelayed = true });
            field.RegisterValueChangedCallback(e => context.Edit("Change " + label.ToLowerInvariant(), () => apply(e.newValue), structural));
            return field;
        }

        public static IntegerField Whole(StudioContext context, string label, int value, Action<int> apply)
        {
            var field = Styled(new IntegerField(label) { value = value, isDelayed = true });
            field.RegisterValueChangedCallback(e => context.Edit("Change " + label.ToLowerInvariant(), () => apply(e.newValue)));
            return field;
        }

        public static Vector3Field Point(StudioContext context, string label, Vector3 value, Action<Vector3> apply)
        {
            var field = Styled(new Vector3Field(label) { value = value });
            field.Query<FloatField>().ForEach(part => part.isDelayed = true);
            field.RegisterValueChangedCallback(e => context.Edit("Change " + label.ToLowerInvariant(), () => apply(e.newValue)));
            return field;
        }

        public static Toggle Switch(StudioContext context, string label, bool value, Action<bool> apply, bool structural = false)
        {
            var field = Styled(new Toggle(label) { value = value });
            field.RegisterValueChangedCallback(e =>
            {
                context.window.RecordPressed(label + (e.newValue ? " on" : " off"));
                context.Edit("Change " + label.ToLowerInvariant(), () => apply(e.newValue), structural);
            });
            return field;
        }

        public static PopupField<string> Choice(StudioContext context, string label, List<string> names, int index, Action<int> apply, bool structural = false)
        {
            if (names.Count == 0) names = new List<string> { "(none)" };
            names = Distinct(names);
            var field = Styled(new PopupField<string>(label, names, Mathf.Clamp(index, 0, names.Count - 1)));
            field.RegisterValueChangedCallback(e =>
            {
                context.window.RecordPressed(label + " · " + e.newValue);
                context.Edit("Change " + label.ToLowerInvariant(), () => apply(names.IndexOf(e.newValue)), structural);
            });
            return field;
        }

        // A popup reports the chosen text, so two entries with the same text (two actors both called "Mia",
        // two Sets both "Untitled set") would both pick the first. Number the repeats: "Mia", "Mia (2)".
        public static List<string> Distinct(List<string> names)
        {
            var seen = new HashSet<string>();
            var result = new List<string>(names.Count);
            foreach (string name in names)
            {
                string shown = name ?? "";
                for (int n = 2; !seen.Add(shown); n++) shown = (name ?? "") + " (" + n + ")";
                result.Add(shown);
            }
            return result;
        }

        public static EnumField Enum<T>(StudioContext context, string label, T value, Action<T> apply, bool structural = false) where T : Enum
        {
            var field = Styled(new EnumField(label, value));
            field.RegisterValueChangedCallback(e => context.Edit("Change " + label.ToLowerInvariant(), () => apply((T)e.newValue), structural));
            return field;
        }

        public static EnumFlagsField Flags<T>(StudioContext context, string label, T value, Action<T> apply) where T : Enum
        {
            var field = Styled(new EnumFlagsField(label, value));
            field.RegisterValueChangedCallback(e => context.Edit("Change " + label.ToLowerInvariant(), () => apply((T)e.newValue)));
            return field;
        }

        // A slider changes the Set while it is dragged and becomes one undo step when it is released.
        public static Slider Slide(StudioContext context, string label, float value, float min, float max, Action<float> apply)
        {
            var field = Styled(new Slider(label, min, max) { value = Mathf.Clamp(value, min, max), showInputField = true });
            bool editing = false;
            void Finish() { if (!editing) return; editing = false; context.window.EndEdit(false); }
            field.RegisterValueChangedCallback(e =>
            {
                if (!editing) { context.window.BeginEdit("Change " + label.ToLowerInvariant()); editing = true; }
                apply(e.newValue);
                context.window.LightRefresh();
            });
            field.RegisterCallback<PointerCaptureOutEvent>(_ => Finish(), TrickleDown.TrickleDown);
            field.RegisterCallback<FocusOutEvent>(_ => Finish());
            return field;
        }

        // The eight actor colors as round swatches.
        static readonly string[] ColorNames = { "Coral", "Blue", "Green", "Gold", "Violet", "Pink", "Teal", "Orange" };

        public static VisualElement Swatches(StudioContext context, int current, Action<int> apply)
        {
            var row = StudioUi.Row();
            row.style.marginTop = 4; row.style.marginBottom = 4;
            for (int i = 0; i < ColorNames.Length; i++)
            {
                int index = i;
                var swatch = new Button(() => context.Edit("Change color", () => apply(index)))
                {
                    text = i == current ? "✓" : "",
                    tooltip = ColorNames[i] + (i == current ? " (selected)" : ""),
                    userData = "Color · " + ColorNames[i]
                };
                swatch.style.color = Color.black;
                swatch.style.paddingLeft = swatch.style.paddingRight = 0;
                swatch.style.paddingTop = swatch.style.paddingBottom = 0;
                swatch.AddToClassList("ts-dot");
                swatch.style.width = 18; swatch.style.height = 18;
                swatch.style.borderTopLeftRadius = swatch.style.borderTopRightRadius = swatch.style.borderBottomLeftRadius = swatch.style.borderBottomRightRadius = 9;
                swatch.style.backgroundColor = StudioColors.For(i);
                swatch.style.borderTopWidth = swatch.style.borderBottomWidth = swatch.style.borderLeftWidth = swatch.style.borderRightWidth = i == current ? 2 : 0;
                swatch.style.borderTopColor = swatch.style.borderBottomColor = swatch.style.borderLeftColor = swatch.style.borderRightColor = Color.white;
                row.Add(swatch);
            }
            return row;
        }
    }
}
