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
            var camera = player.ViewCamera;
            var paused = player.InputSuspended;
            player.Place(view.WardrobePosition, Quaternion.identity);
            for (int i = 0; i < 120 && !view.WardrobeOpen; i++) yield return null;
            Check(view.WardrobeOpen, "Walking into wardrobe opens desktop UI");
            Check(player.InputSuspended && player.ViewCamera == camera, "Preview preserves player camera and suspends movement");
            yield return new WaitForSeconds(.5f);
            var desktop = Field<RectTransform>(view, "wardrobeDesktop");
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
            var panel = Field<RectTransform>(view, "wardrobePanel");
            desktop.gameObject.SetActive(false);
            panel.gameObject.SetActive(true);
            Call(view, "RefreshWardrobeLabels");
            Canvas.ForceUpdateCanvases();
            yield return null;
            CaptureWardrobeCanvas(panel, "wardrobe-vr-layout.png", false);
            Check(panel.GetComponentsInChildren<BoxCollider>(true).All(c => c.isTrigger), "VR selection targets remain triggers");
            view.SetWardrobeOpen(false);
            Check(!view.WardrobeOpen && player.InputSuspended == paused && player.ViewCamera == camera, "Close restores movement and original camera");
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
