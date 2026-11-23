using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    internal sealed class StudioWardrobeSheet : EditorWindow
    {
        const int PageSize = 8;
        readonly List<Texture2D> portraits = new List<Texture2D>();
        StudioContext context;
        string group, setId;
        int page;

        internal static void Open(StudioContext context, string group = null)
        {
            var window = GetWindow<StudioWardrobeSheet>();
            window.titleContent = new GUIContent("Cast wardrobe");
            window.minSize = new Vector2(365, 415);
            window.context = context;
            window.group = group;
            window.setId = context.Set.id;
            window.page = 0;
            window.Draw();
            window.Show();
        }

        void OnDisable() => ReleasePortraits();

        void ReleasePortraits()
        {
            foreach (var image in portraits) if (image) DestroyImmediate(image);
            portraits.Clear();
        }

        void Draw()
        {
            ReleasePortraits();
            var panel = rootVisualElement;
            panel.Clear();
            if (context?.Set?.id != setId) { panel.Add(new Label("Open a contact sheet from the current set.")); return; }
            var marks = context.Set.marks.Where(m => m.role != StudioRole.Hand && (string.IsNullOrEmpty(group) || m.crowdGroup == group))
                .OrderByDescending(m => m.heroLocked).ThenBy(m => string.IsNullOrEmpty(m.crowdGroup) ? 0 : 1).ToArray();
            if (marks.Length == 0) { panel.Add(new Label("Place a survivor before opening the wardrobe sheet.")); return; }
            var rigPrefab = Resources.Load<CandyPlayerRig>("CandyPlayerVisual");
            if (!rigPrefab) { panel.Add(new Label("The project's survivor visual prefab could not be loaded.")); return; }
            int pages = Mathf.CeilToInt(marks.Length / (float)PageSize);
            page = Mathf.Clamp(page, 0, pages - 1);
            var header = StudioUi.Row();
            var prev = new Button(() => { page--; Draw(); }) { text = "Previous" };
            var next = new Button(() => { page++; Draw(); }) { text = "Next" };
            prev.SetEnabled(page > 0);
            next.SetEnabled(page + 1 < pages);
            header.Add(prev); header.Add(new Label($"Page {page + 1} of {pages}")); header.Add(next);
            panel.Add(header);
            panel.Add(new Label("Neutral portraits show saved outfits or the first recorded look. Equipment is listed below each portrait."));
            var scroll = new ScrollView();
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            scroll.Add(grid);
            panel.Add(scroll);
            foreach (var mark in marks.Skip(page * PageSize).Take(PageSize))
            {
                var card = new VisualElement();
                card.style.width = 156;
                card.style.marginLeft = card.style.marginRight = 5;
                card.style.marginTop = card.style.marginBottom = 5;
                card.Add(new Label(mark.actor) { tooltip = mark.actor });
                card.Add(new Label(mark.heroLocked ? "Protected hero" : string.IsNullOrEmpty(mark.crowdGroup) ? "Cast" : mark.crowdGroup));
                var wardrobe = Appearance(mark, context.Tape);
                try
                {
                    var texture = Render(rigPrefab, wardrobe.look, wardrobe.color);
                    if (texture)
                    {
                        portraits.Add(texture);
                        var portrait = new Image { image = texture, scaleMode = ScaleMode.ScaleToFit };
                        portrait.style.width = 150;
                        portrait.style.height = 160;
                        card.Add(portrait);
                    }
                }
                catch (Exception error) { card.Add(new Label("Portrait unavailable") { tooltip = error.Message }); }
                card.Add(new Label(wardrobe.gear == EquipmentKind.None ? "No equipment" : wardrobe.gear.ToString()));
                card.Add(new Button(() =>
                {
                    if (context.Set?.id != setId) return;
                    var current = context.Set.Mark(mark.id);
                    if (current == null) return;
                    context.window.ShowMode("Set");
                    context.Select(current);
                }) { text = "Edit wardrobe" });
                grid.Add(card);
            }
        }

        internal static (AvatarAppearance look, int color, EquipmentKind gear) Appearance(StudioMark mark, StudioTape tape)
        {
            var look = mark.appearance;
            int color = mark.color;
            EquipmentKind gear = StudioCast.StartingEquipment(mark);
            var track = tape?.Find(mark.id);
            if (track?.ActiveTake != null)
            {
                foreach (var sample in track.ActiveTake.frames)
                {
                    if (!ActorTrack.TryReadPosition(track.type, sample, out _)) continue;
                    var frame = ActorFrame.Read(track.type, sample);
                    if (!mark.customLook) { look = frame.look; color = frame.color & 0xff; }
                    if (gear == EquipmentKind.None && !string.IsNullOrEmpty(frame.equipment))
                    {
                        try { gear = JsonUtility.FromJson<EquipmentState>(frame.equipment)?.kind ?? EquipmentKind.None; }
                        catch (ArgumentException) { }
                    }
                    break;
                }
            }
            look.poweredArmor = gear == EquipmentKind.PoweredArmor;
            return (look, color, gear);
        }

        static Texture2D Render(CandyPlayerRig prefab, AvatarAppearance look, int color)
        {
            var preview = new PreviewRenderUtility();
            try
            {
                var rig = Instantiate(prefab);
                preview.AddSingleGO(rig.gameObject);
                rig.ownerCamera = null;
                rig.ApplyFrame(new CandyAvatarFrame
                {
                    head = Vector3.up * 1.6f, headRotation = Quaternion.identity, scale = 1,
                    left = RestHand(true), right = RestHand(false)
                });
                rig.SetColor(color);
                rig.SetAppearance(look);
                var camera = preview.camera;
                camera.orthographic = true;
                camera.orthographicSize = 1.05f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.11f, .14f, .19f, 1);
                camera.transform.position = new Vector3(1.1f, 1.25f, 3.5f);
                camera.transform.LookAt(Vector3.up * .87f);
                preview.lights[0].intensity = 1.25f;
                preview.lights[0].transform.rotation = Quaternion.Euler(33, 145, 0);
                preview.lights[1].intensity = .8f;
                preview.lights[1].transform.rotation = Quaternion.Euler(22, -115, 0);
                preview.BeginStaticPreview(new Rect(0, 0, 150, 160));
                preview.Render(true, false);
                return preview.EndStaticPreview();
            }
            finally { preview.Cleanup(); }
        }

        static CandyHandPose RestHand(bool left)
        {
            float sign = left ? -1 : 1;
            return new CandyHandPose
            {
                tracked = true, skeletonTracked = true, skeleton = CandyHandSkeleton.Rest(left, .04f),
                position = new Vector3(sign * .29f, .77f, .025f), rotation = Quaternion.Euler(0, sign * 12, sign * 85)
            };
        }
    }
}
