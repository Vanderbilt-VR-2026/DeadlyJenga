using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    // Render one still per visible avatar and reuse it for the feed and results.
    internal sealed class CandyScorePortraits : MonoBehaviour
    {
        const int PortraitLayer = 31;
        const int Size = 96;
        readonly Dictionary<string, Texture2D> portraits = new Dictionary<string, Texture2D>();
        readonly List<Mesh> baked = new List<Mesh>();
        sealed class NameFit
        {
            public string source, fitted;
            public float width;
            public Font font;
            public int size;
        }
        readonly Dictionary<Text, NameFit> nameFits = new Dictionary<Text, NameFit>();

        public void SetName(Text label, string value)
        {
            string source = CandyHudContent.Safe(value, 64);
            float width = label.rectTransform.rect.width;
            if (!nameFits.TryGetValue(label, out var fit) || fit.source != source || fit.width != width
                || fit.font != label.font || fit.size != label.fontSize)
            {
                var settings = label.GetGenerationSettings(new Vector2(width, label.rectTransform.rect.height));
                settings.scaleFactor = 1;
                settings.horizontalOverflow = HorizontalWrapMode.Overflow;
                string fitted = source;
                if (label.cachedTextGeneratorForLayout.GetPreferredWidth(source, settings) > width)
                {
                    int low = 0, high = source.Length;
                    while (low < high)
                    {
                        int count = (low + high + 1) / 2;
                        string candidate = source.Substring(0, count) + "…";
                        if (label.cachedTextGeneratorForLayout.GetPreferredWidth(candidate, settings) <= width) low = count;
                        else high = count - 1;
                    }
                    if (low > 0 && char.IsHighSurrogate(source[low - 1])) low--;
                    fitted = source.Substring(0, low) + "…";
                }
                fit = new NameFit { source = source, fitted = fitted, width = width, font = label.font, size = label.fontSize };
                nameFits[label] = fit;
            }
            label.text = fit.fitted;
        }
        IMatchRuntime owner;
        Camera portraitCamera;
        GameObject stage;
        RenderTexture target;
        int renderedFrame = -1;
        int budget;

        public static CandyScorePortraits For(GameObject session)
        {
            var cache = session.GetComponent<CandyScorePortraits>();
            return cache ? cache : session.AddComponent<CandyScorePortraits>();
        }

        public Texture Get(IMatchRuntime match, MatchPlayerFrame player)
        {
            if (owner != match)
            {
                Clear();
                owner = match;
            }
            if (portraits.TryGetValue(player.id, out var texture)) return texture;
            if (renderedFrame != Time.frameCount)
            {
                renderedFrame = Time.frameCount;
                budget = 2;
            }
            if (budget == 0 || portraits.Count >= 256) return null;

            var actor = match.FindActor(player.id);
            var visual = actor?.Visual;
            if (!visual) return null;
            var head = visual.Find("Head");
            Vector3 center = head ? head.position : actor.ViewPose.position;
            Quaternion rotation = head ? head.rotation : actor.ViewPose.rotation;
            float scale = Mathf.Max(.15f, Mathf.Abs(visual.lossyScale.y));
            Vector3 focus = center + rotation * Vector3.up * (.055f * scale);

            EnsureCamera();
            var properties = new MaterialPropertyBlock();
            foreach (var source in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (!source.enabled || !VisibleInAvatar(source.transform, visual)) continue;
                if (source.bounds.SqrDistance(center) > .16f * scale * scale) continue;
                Mesh mesh;
                if (source is SkinnedMeshRenderer skin && skin.sharedMesh)
                {
                    mesh = new Mesh();
                    skin.BakeMesh(mesh, true);
                    baked.Add(mesh);
                }
                else if (source is MeshRenderer && source.TryGetComponent<MeshFilter>(out var filter))
                    mesh = filter.sharedMesh;
                else continue;
                if (!mesh) continue;

                var copy = new GameObject("Portrait mesh", typeof(MeshFilter), typeof(MeshRenderer));
                copy.layer = PortraitLayer;
                copy.transform.SetParent(stage.transform, false);
                copy.transform.SetPositionAndRotation(stage.transform.position + source.transform.position - focus,
                    source.transform.rotation);
                copy.transform.localScale = source.transform.lossyScale;
                copy.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = copy.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials;
                properties.Clear();
                source.GetPropertyBlock(properties);
                if (source.sharedMaterial && source.sharedMaterial.HasProperty("_Health"))
                {
                    properties.SetFloat("_Health", 1);
                    properties.SetFloat("_Sink", 0);
                    properties.SetFloat("_MeltStart", -10000);
                    properties.SetFloat("_Recovering", 0);
                }
                renderer.SetPropertyBlock(properties);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            if (stage.transform.childCount == 0) return null;

            budget--;
            portraitCamera.orthographicSize = .31f * scale;
            portraitCamera.transform.SetPositionAndRotation(stage.transform.position + rotation * Vector3.forward * (1.5f * scale),
                rotation * Quaternion.Euler(0, 180, 0));
            portraitCamera.farClipPlane = 2.1f * scale;
            portraitCamera.backgroundColor = Color.Lerp(new Color(.10f, .055f, .17f), player.color, .26f);
            var previous = RenderTexture.active;
            stage.SetActive(true);
            try
            {
                portraitCamera.Render();
                RenderTexture.active = target;
                texture = new Texture2D(Size, Size, TextureFormat.RGB24, false) { name = "Head portrait" };
                texture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                texture.Apply(false, true);
                portraits.Add(player.id, texture);
                return texture;
            }
            finally
            {
                RenderTexture.active = previous;
                stage.SetActive(false);
                while (stage.transform.childCount > 0)
                {
                    var copy = stage.transform.GetChild(0);
                    copy.gameObject.SetActive(false);
                    copy.SetParent(null);
                    Destroy(copy.gameObject);
                }
                foreach (var mesh in baked) Destroy(mesh);
                baked.Clear();
            }
        }

        public static void Show(RawImage image, Text fallback, Texture texture, MatchPlayerFrame player)
        {
            image.texture = texture ? texture : Texture2D.whiteTexture;
            image.color = texture ? Color.white : Color.Lerp(player.color, new Color(.13f, .07f, .21f), .64f);
            string name = CandyHudContent.Safe(player.name);
            fallback.text = texture || name.Length == 0 ? "" : name.Substring(0, 1).ToUpperInvariant();
        }

        static bool VisibleInAvatar(Transform part, Transform visual)
        {
            while (part != visual)
            {
                if (!part.gameObject.activeSelf) return false;
                part = part.parent;
            }
            return true;
        }

        void EnsureCamera()
        {
            if (portraitCamera) return;
            stage = new GameObject("Visual - portrait stage");
            stage.transform.position = new Vector3(0, -4096, 0);
            stage.SetActive(false);
            var go = new GameObject("Visual - portrait camera", typeof(Camera));
            go.transform.SetParent(transform, false);
            portraitCamera = go.GetComponent<Camera>();
            portraitCamera.enabled = false;
            portraitCamera.orthographic = true;
            portraitCamera.stereoTargetEye = StereoTargetEyeMask.None;
            portraitCamera.cullingMask = 1 << PortraitLayer;
            portraitCamera.allowHDR = portraitCamera.allowMSAA = false;
            portraitCamera.nearClipPlane = .01f;
            portraitCamera.clearFlags = CameraClearFlags.SolidColor;
            target = new RenderTexture(Size, Size, 16);
            portraitCamera.targetTexture = target;
        }

        void Clear()
        {
            foreach (var texture in portraits.Values) Destroy(texture);
            portraits.Clear();
            nameFits.Clear();
        }

        void OnEnable() => GameEvents.RoundStarted += Clear;
        void OnDisable() => GameEvents.RoundStarted -= Clear;

        void OnDestroy()
        {
            Clear();
            if (stage) Destroy(stage);
            if (portraitCamera) Destroy(portraitCamera.gameObject);
            if (target) Destroy(target);
        }
    }
}
