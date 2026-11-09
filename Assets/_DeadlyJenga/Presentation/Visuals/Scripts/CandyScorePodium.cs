using System;
using System.Collections.Generic;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DeadlyJenga.Presentation
{
    // Render copies of the leaders' visuals. The copies contain no gameplay components.
    internal sealed class CandyScorePodium : IDisposable
    {
        const int PreviewLayer = 31;
        const float RenderInterval = 1f / 20f;
        static readonly int[] Columns = { 0, -1, 1 };
        static readonly Color Ink = new Color(.10f, .056f, .19f);
        static readonly Color[] Medals = {
            new Color(1f, .79f, .36f), new Color(.78f, .91f, 1f), new Color(1f, .67f, .51f)
        };

        sealed class Leader
        {
            public string id;
            public Transform source, model;
            public Vector3 position;
            public Text label, rank;
            public bool disappointed;
            public Image platform;
            public Joint[] joints;
            public Vector3 leftShoulder, rightShoulder, leftHip, rightHip;
        }

        readonly struct Joint
        {
            public readonly Transform bone;
            public readonly Vector3 position;
            public readonly Quaternion rotation;
            public readonly int group;

            public Joint(Transform bone, int group)
            {
                this.bone = bone;
                position = bone.localPosition;
                rotation = bone.localRotation;
                this.group = group;
            }
        }

        readonly Transform owner;
        readonly RectTransform panel;
        readonly RawImage image;
        readonly Leader[] leaders = { new Leader(), new Leader(), new Leader() };
        IMatchRuntime match;
        GameObject stage;
        Camera camera;
        RenderTexture texture;
        float nextRender;

        public CandyScorePodium(Transform owner, Transform parent, Vector2 position)
        {
            this.owner = owner;
            var card = CandyHudCards.Create((RectTransform)parent, "Scoreboard podium",
                position, new Vector2(380, 540), 24, TextAnchor.UpperCenter);
            card.text = "TOP THREE";
            card.color = Color.white;
            card.rectTransform.offsetMax = new Vector2(-12, -16);
            panel = (RectTransform)card.transform.parent;
            panel.GetComponent<Image>().color = Ink;
            var go = new GameObject("Dancing leaders", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(panel, false);
            image = go.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.enabled = false;
            image.rectTransform.sizeDelta = new Vector2(354, 354);
            image.rectTransform.anchoredPosition = new Vector2(0, 28);
            for (int i = 0; i < leaders.Length; i++)
            {
                float x = Columns[i] * 118;
                var baseObject = new GameObject("Podium step", typeof(RectTransform), typeof(Image));
                baseObject.transform.SetParent(panel, false);
                var platform = leaders[i].platform = baseObject.GetComponent<Image>();
                platform.sprite = CandyHudVisuals.Pill;
                platform.type = Image.Type.Sliced;
                platform.color = Color.Lerp(Ink, Medals[i], .65f);
                platform.raycastTarget = false;
                platform.rectTransform.sizeDelta = new Vector2(100, 16);
                platform.rectTransform.anchoredPosition = new Vector2(x, i == 0 ? -69 : -94);
                var rank = leaders[i].rank = Label("Podium rank", new Vector2(x, -161), 27);
                rank.text = "#" + (i + 1);
                rank.color = Medals[i];
                leaders[i].label = Label("Podium player", new Vector2(x, -202), 19);
            }
            panel.gameObject.SetActive(false);
        }

        Text Label(string name, Vector2 position, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(panel, false);
            var label = go.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontStyle = FontStyle.Bold;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.supportRichText = false;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = size;
            label.rectTransform.sizeDelta = new Vector2(112, 48);
            label.rectTransform.anchoredPosition = position;
            return label;
        }

        public void SetPlayers(IMatchRuntime runtime, MatchPlayerFrame[] players, bool visible)
        {
            bool show = visible && players.Length > 0;
            panel.gameObject.SetActive(show);
            if (!show)
            {
                Hide();
                return;
            }
            if (match != runtime)
            {
                Reset();
                match = runtime;
            }
            var frame = runtime.Frame;
            bool results = frame.phase == RoundPhase.Results || frame.phase == RoundPhase.Victory;
            int winningTeam = frame.winnerTeam;
            bool infection = frame.rules.mode == GameModeKind.Infection;
            bool Won(MatchPlayerFrame player) => winningTeam >= 0 ? player.team == winningTeam
                : infection ? (frame.winner == "Infected" ? player.role == "infected" : player.alive && player.role != "infected")
                : frame.winner != "Draw" && player.name == frame.winner;
            int winnerIndex = results ? Array.FindIndex(players, player => Won(player)) : -1;
            int rival = -1;
            if (winnerIndex >= 0 && (winningTeam >= 0 || infection) && players.Length >= leaders.Length)
            {
                bool rivalVisible = false;
                for (int i = 1; i < leaders.Length; i++)
                    rivalVisible |= !Won(players[i <= winnerIndex ? i - 1 : i]);
                if (!rivalVisible) rival = Array.FindIndex(players, player => !Won(player));
            }
            for (int i = 0; i < leaders.Length; i++)
            {
                var leader = leaders[i];
                int rank = i == 0 && winnerIndex >= 0 ? winnerIndex : i <= winnerIndex ? i - 1 : i;
                if (i == leaders.Length - 1 && rival >= 0) rank = rival;
                var player = rank < players.Length ? players[rank] : null;
                leader.disappointed = results && player != null && !Won(player);
                leader.rank.text = "#" + (rank + 1);
                leader.rank.color = leader.disappointed ? CandyHudVisuals.Muted : Medals[i];
                leader.rank.gameObject.SetActive(player != null);
                leader.platform.gameObject.SetActive(player != null);
                var visual = player == null ? null : runtime.FindActor(player.id)?.Visual;
                if (leader.id != player?.id || leader.source != visual)
                {
                    Clear(leader);
                    leader.id = player?.id;
                    leader.source = visual;
                    if (visual)
                    {
                        EnsureStage();
                        CopyAvatar(leader, i);
                    }
                    nextRender = 0;
                }
                leader.label.text = player == null ? "" : CandyHudContent.Safe(player.name, 24);
            }
        }

        public void Hide()
        {
            if (panel) panel.gameObject.SetActive(false);
            if (match != null) Reset();
        }

        void EnsureStage()
        {
            if (stage) return;
            stage = new GameObject("Visual - scoreboard podium stage");
            stage.transform.SetParent(owner, false);
            stage.transform.SetPositionAndRotation(new Vector3(0, -8192, 0), Quaternion.identity);
            stage.SetActive(false);
            var lamp = new GameObject("Podium key light", typeof(Light));
            lamp.transform.SetParent(stage.transform, false);
            lamp.transform.localRotation = Quaternion.Euler(32, 155, 0);
            var light = lamp.GetComponent<Light>();
            light.type = LightType.Directional;
            light.cullingMask = 1 << PreviewLayer;
            light.intensity = 1.5f;
            light.shadows = LightShadows.None;

            var go = new GameObject("Visual - scoreboard podium camera", typeof(Camera));
            go.transform.SetParent(stage.transform, false);
            go.transform.localPosition = new Vector3(0, 0, 5);
            go.transform.localRotation = Quaternion.Euler(0, 180, 0);
            camera = go.GetComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 1.65f;
            camera.aspect = 1;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 10;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.GetUniversalAdditionalCameraData().allowXRRendering = false;
            camera.allowHDR = camera.allowMSAA = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Ink;
            camera.cullingMask = 1 << PreviewLayer;
            texture = new RenderTexture(384, 384, 24) { name = "Scoreboard podium" };
            camera.targetTexture = texture;
            image.texture = texture;
            image.enabled = true;
        }

        void CopyAvatar(Leader leader, int rank)
        {
            var source = leader.source;
            var model = new GameObject("Visual - ranked avatar").transform;
            model.SetParent(stage.transform, false);
            leader.model = model;
            var copies = new Dictionary<Transform, Transform> { { source, model } };
            Transform CopyTransform(Transform original)
            {
                if (!original) return null;
                if (copies.TryGetValue(original, out var existing)) return existing;
                if (!original.IsChildOf(source)) return null;
                var copy = new GameObject(original.name).transform;
                copy.gameObject.layer = PreviewLayer;
                copy.SetParent(CopyTransform(original.parent), false);
                copy.localPosition = original.localPosition;
                copy.localRotation = original.localRotation;
                copy.localScale = original.localScale;
                copies.Add(original, copy);
                return copy;
            }

            var properties = new MaterialPropertyBlock();
            var renderers = new List<Renderer>();
            foreach (var original in source.GetComponentsInChildren<Renderer>(true))
            {
                bool avatarBody = original is SkinnedMeshRenderer && original.name == "Candy body and shared hands";
                if ((!avatarBody && (!original.enabled || original.forceRenderingOff)) ||
                    !CandyScorePortraits.VisibleInAvatar(original.transform, source)) continue;
                Renderer copy;
                if (original is SkinnedMeshRenderer skin && skin.sharedMesh)
                {
                    var bones = Array.ConvertAll(skin.bones, CopyTransform);
                    if (Array.Exists(bones, bone => !bone)) continue;
                    var clone = CopyTransform(original.transform).gameObject.AddComponent<SkinnedMeshRenderer>();
                    clone.sharedMesh = skin.sharedMesh;
                    clone.bones = bones;
                    clone.rootBone = CopyTransform(skin.rootBone);
                    clone.localBounds = skin.localBounds;
                    clone.updateWhenOffscreen = true;
                    for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++)
                        clone.SetBlendShapeWeight(i, skin.GetBlendShapeWeight(i));
                    copy = clone;
                }
                else if (original is MeshRenderer && original.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh)
                {
                    var go = CopyTransform(original.transform).gameObject;
                    go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    copy = go.AddComponent<MeshRenderer>();
                }
                else continue;
                copy.gameObject.layer = PreviewLayer;
                copy.sharedMaterials = original.sharedMaterials;
                original.GetPropertyBlock(properties);
                if (original.sharedMaterial && original.sharedMaterial.HasProperty("_Health"))
                {
                    properties.SetFloat("_Health", 1);
                    properties.SetFloat("_Sink", 0);
                    properties.SetFloat("_MeltStart", -10000);
                    properties.SetFloat("_Recovering", 0);
                }
                copy.SetPropertyBlock(properties);
                copy.shadowCastingMode = ShadowCastingMode.Off;
                copy.receiveShadows = false;
                renderers.Add(copy);
            }
            RestoreStandingPose(source, copies);
            var joints = new List<Joint>();
            foreach (Transform bone in model)
            {
                int group = DanceGroup(bone.name);
                if (group >= 0) joints.Add(new Joint(bone, group));
                switch (bone.name)
                {
                    case "Left upper arm": leader.leftShoulder = bone.localPosition; break;
                    case "Right upper arm": leader.rightShoulder = bone.localPosition; break;
                    case "Left thigh": leader.leftHip = bone.localPosition; break;
                    case "Right thigh": leader.rightHip = bone.localPosition; break;
                }
            }
            leader.joints = joints.ToArray();
            if (renderers.Count == 0)
            {
                Clear(leader);
                leader.source = source;
                return;
            }

            var bounds = PreviewBounds(model, renderers);
            float scale = Mathf.Min((rank == 0 ? 1.95f : 1.70f) / Mathf.Max(.1f, bounds.size.y),
                1.02f / Mathf.Max(.1f, bounds.size.x));
            model.localScale = Vector3.one * scale;
            leader.position = new Vector3(-Columns[rank] * 1.08f - bounds.center.x * scale,
                (rank == 0 ? -.82f : -1.05f) - bounds.min.y * scale, -bounds.center.z * scale);
        }

        static void RestoreStandingPose(Transform source, Dictionary<Transform, Transform> copies)
        {
            // The body bind pose gives the podium an upright starting pose after deaths and ragdolls.
            var body = source.Find("Candy body and shared hands");
            if (!body || !body.TryGetComponent<SkinnedMeshRenderer>(out var skin) || !skin.sharedMesh) return;
            var bind = skin.sharedMesh.bindposes;
            var bones = skin.bones;
            if (bind.Length != bones.Length) return;
            var rigToMesh = source.worldToLocalMatrix * skin.transform.localToWorldMatrix;
            for (int i = 0; i < bones.Length; i++)
            {
                if (!bones[i] || bones[i].parent != source || !copies.TryGetValue(bones[i], out var clone)) continue;
                var pose = rigToMesh * bind[i].inverse;
                clone.localPosition = pose.GetColumn(3);
                clone.localRotation = pose.rotation;
            }
        }

        static Bounds PreviewBounds(Transform model, List<Renderer> renderers)
        {
            var result = new Bounds();
            bool first = true;
            var scratch = new Mesh();
            foreach (var renderer in renderers)
            {
                Bounds bounds;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    skin.BakeMesh(scratch);
                    bounds = scratch.bounds;
                }
                else bounds = renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
                var toModel = model.worldToLocalMatrix * renderer.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (i & 2) == 0 ? bounds.min.y : bounds.max.y, (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                    var point = toModel.MultiplyPoint3x4(corner);
                    if (first) { result = new Bounds(point, Vector3.zero); first = false; }
                    else result.Encapsulate(point);
                }
            }
            Object.Destroy(scratch);
            return result;
        }

        static int DanceGroup(string name)
        {
            if (name == "Head") return 4;
            int side = name.StartsWith("Left ", StringComparison.Ordinal) ? 0
                : name.StartsWith("Right ", StringComparison.Ordinal) ? 1 : -1;
            if (side < 0) return -1;
            if (name.EndsWith("thigh") || name.EndsWith("shin") || name.EndsWith("foot")) return side + 2;
            return name.EndsWith("upper arm") || name.EndsWith("forearm") || name.EndsWith("palm")
                || name.Contains(" finger ") ? side : -1;
        }

        public void Draw()
        {
            float now = Time.unscaledTime;
            if (!stage || !panel.gameObject.activeInHierarchy || now < nextRender) return;
            nextRender = now + RenderInterval;
            for (int i = 0; i < leaders.Length; i++)
            {
                var leader = leaders[i];
                if (!leader.model) continue;
                float beat = now * 6.5f + i * 1.4f;
                float sway = Mathf.Sin(beat);
                leader.model.localPosition = leader.position + (leader.disappointed
                    ? new Vector3(0f, -.035f - .018f * Mathf.Abs(sway), 0f)
                    : new Vector3(.05f * sway, .10f * Mathf.Abs(sway), 0f));
                leader.model.localRotation = leader.disappointed
                    ? Quaternion.Euler(13f + 3f * sway, 0f, 3f * sway)
                    : Quaternion.Euler(0, 18 * sway, 8 * sway);
                foreach (var joint in leader.joints)
                {
                    Vector3 pivot;
                    Quaternion move;
                    switch (joint.group)
                    {
                        case 0: pivot = leader.leftShoulder; move = Quaternion.Euler(-18 * sway, 0, -48 - 26 * sway); break;
                        case 1: pivot = leader.rightShoulder; move = Quaternion.Euler(18 * sway, 0, 48 - 26 * sway); break;
                        case 2: pivot = leader.leftHip; move = Quaternion.Euler(28 * Mathf.Max(0, sway), 0, -6); break;
                        case 3: pivot = leader.rightHip; move = Quaternion.Euler(28 * Mathf.Max(0, -sway), 0, 6); break;
                        default: pivot = joint.position; move = Quaternion.Euler(8 * Mathf.Cos(beat), 0, -6 * sway); break;
                    }
                    if (leader.disappointed)
                    {
                        // The shoulders sag and one hand rises toward the face in short sobs.
                        switch (joint.group)
                        {
                            case 0: move = Quaternion.Euler(-16f + 3f * sway, 0f, 14f); break;
                            case 1: move = Quaternion.Euler(-111f + 8f * sway, -18f, -35f); break;
                            case 2: case 3: move = Quaternion.identity; break;
                            default: move = Quaternion.Euler(20f + 5f * sway, -8f, 8f); break;
                        }
                    }
                    joint.bone.localPosition = pivot + move * (joint.position - pivot);
                    joint.bone.localRotation = move * joint.rotation;
                }
            }
            stage.SetActive(true);
            try { camera.Render(); }
            finally { stage.SetActive(false); }
        }

        static void Clear(Leader leader)
        {
            if (leader.model)
            {
                leader.model.gameObject.SetActive(false);
                Object.Destroy(leader.model.gameObject);
            }
            leader.model = null;
            leader.source = null;
            leader.id = null;
        }

        void Reset()
        {
            match = null;
            foreach (var leader in leaders) Clear(leader);
        }

        public void Dispose()
        {
            Reset();
            if (stage) Object.Destroy(stage);
            if (texture) Object.Destroy(texture);
        }
    }
}
