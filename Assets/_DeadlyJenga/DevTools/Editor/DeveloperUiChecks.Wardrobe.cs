using System.Collections;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.DevTools.Editor
{
    public static partial class DeveloperUiChecks
    {
        static IEnumerator WardrobeLayoutChecks()
        {
            Check(IslandLobby.Current && Services.Session?.Player != null, "Actual Marina lobby initializes");
            var view = Object.FindFirstObjectByType<IslandLobbyView>();
            var player = Services.Session.Player;
            var motor = player.gameObject.GetComponent<CastleSurvivor>();
            motor.controlMode = FlyingHand.ControlMode.Desktop; motor.RefreshMode();
            Check(!player.IsVR, "Fixture explicitly selects desktop input");
            var camera = player.ViewCamera;
            var paused = player.InputSuspended;
            player.Place(view.WardrobePosition, Quaternion.identity);
            for (int i = 0; i < 120 && !view.WardrobeOpen; i++) { Call(view, "UpdateWardrobe"); yield return null; }
            Check(view.WardrobeOpen, "Production proximity check opens desktop UI at the wardrobe");
            Check(player.InputSuspended && player.ViewCamera == camera, "Preview preserves player camera and suspends movement");
            yield return new WaitForSeconds(.5f);
            for (int i = 0; i < 4; i++) { Call(view, "UpdateWardrobe"); yield return null; }
            CheckWardrobePreview(Field<RenderTexture>(view, "wardrobeTexture"));
            var desktop = Field<RectTransform>(view, "wardrobeDesktop");
            CheckWardrobeTargets(desktop, Field<System.Collections.Generic.List<System.Tuple<RectTransform,System.Action>>>(view, "wardrobeButtons")
                .Select(b => b.Item1).Where(r => r.gameObject.activeInHierarchy).ToArray());
            CaptureWardrobeCanvas(desktop, "wardrobe-desktop.png", true);
            yield return null;
            Call(view, "SetWardrobeCategory", 2);
            yield return null;
            Check(desktop.Find("Options/Accessory 0").gameObject.activeInHierarchy, "Headgear category displays its items");
            Call(view, "ChangeWardrobePage", 1);
            yield return null;
            Check(Field<int>(view, "wardrobePage") == 1, "Next page opens remaining headgear");
            Call(view, "SetWardrobeCategory", 0);
            Check(Field<int>(view, "wardrobePage") == 0, "Category switch resets pagination");
            view.SetWardrobeOpen(false);
            motor.controlMode = FlyingHand.ControlMode.VR; motor.RefreshMode();
            player.Place(view.WardrobePosition, Quaternion.identity);
            view.SetWardrobeOpen(true);
            Check(view.WardrobeOpen && player.IsVR, "Actual VR wardrobe branch opens in desktop fixture");
            var panel = Field<RectTransform>(view, "wardrobePanel");
            Call(view, "RefreshWardrobeLabels");
            view.Point(new Ray(panel.position - panel.forward * 2, -panel.forward), false);
            Canvas.ForceUpdateCanvases();
            yield return null;
            CaptureWardrobeCanvas(panel, "wardrobe-vr-layout.png", false);
            CheckWardrobeTargets(panel, panel.GetComponentsInChildren<Button>().Select(b => (RectTransform)b.transform).ToArray());
            Check(panel.GetComponentsInChildren<BoxCollider>(true).All(c => c.isTrigger), "VR selection targets remain triggers");
            var avatar = player.gameObject.GetComponent<IAvatarAppearance>();
            var look = avatar.Appearance;
            Call(view, "ConfirmWardrobeReset");
            Check(avatar.Appearance.Equals(look), "First reset click only asks for confirmation");
            view.SetWardrobeOpen(false);
            Check(!view.WardrobeOpen && player.InputSuspended == paused && player.ViewCamera == camera, "Close restores movement and original camera");
        }

        static void CheckWardrobePreview(RenderTexture texture)
        {
            var previous = RenderTexture.active;
            var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);pixels.Apply();
                var colors = pixels.GetPixels32();var background = colors[0];int visible = 0;
                foreach (var color in colors)
                    if (Mathf.Abs(color.r-background.r)+Mathf.Abs(color.g-background.g)+Mathf.Abs(color.b-background.b)>45) visible++;
                Check(visible > colors.Length / 100, "Character preview contains rendered avatar geometry");
            }
            finally { RenderTexture.active = previous;Object.Destroy(pixels); }
        }

        static void CheckWardrobeTargets(RectTransform root, RectTransform[] controls)
        {
            var rectangles = controls.Select(control =>
            {
                var corners = new Vector3[4]; control.GetWorldCorners(corners);
                var a = root.InverseTransformPoint(corners[0]); var b = root.InverseTransformPoint(corners[2]);
                return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            }).ToArray();
            for (int i = 0; i < rectangles.Length; i++)
            {
                for (int j = i + 1; j < rectangles.Length; j++)
                    Check(!rectangles[i].Overlaps(rectangles[j]), "Separate targets: " + controls[i].name + " / " + controls[j].name);
            }
        }

        static void CaptureWardrobeCanvas(RectTransform root, string filename, bool desktop)
        {
            var canvas = root.GetComponent<Canvas>();
            var mode = canvas.renderMode;
            var position = root.position; var rotation = root.rotation; var scale = root.localScale;
            var size = root.sizeDelta;
            var scaler = root.GetComponent<CanvasScaler>();
            bool scalerEnabled = scaler && scaler.enabled;
            try
            {
                if (scaler) scaler.enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                root.SetPositionAndRotation(new Vector3(0, 200, 0), Quaternion.identity);
                root.localScale = Vector3.one * .001f;
                if (desktop) root.sizeDelta = new Vector2(1280, 800);
                Canvas.ForceUpdateCanvases();
                CaptureTerminal(root, filename);
            }
            finally
            {
                canvas.renderMode = mode;
                root.SetPositionAndRotation(position, rotation);root.localScale = scale;root.sizeDelta = size;
                if (scaler) scaler.enabled = scalerEnabled;
                Canvas.ForceUpdateCanvases();
            }
        }
    }
}
