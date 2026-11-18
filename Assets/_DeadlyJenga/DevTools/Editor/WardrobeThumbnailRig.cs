using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Core;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Renders a picture of every wardrobe catalog item on an otherwise plain player,
    // once per body colour (skins: once, default body), into Resources/WardrobeThumbs.
    // The wardrobe picks them up automatically through WardrobeThumbnailLibrary.
    //
    // Editor: Tools > Deadly Jenga > Wardrobe Thumbnails > Render All / Render Missing.
    // CLI (needs graphics, so no -nographics):
    //   Unity -batchmode -projectPath <project> -executeMethod DeadlyJenga.DevTools.Editor.WardrobeThumbnailRig.Batch
    //     [-thumbItem Headgear/CowboyHat]   only this item (Slot/Item, names as in the wardrobe catalog)
    //     [-thumbColor 3]                   only this body colour index
    //     [-thumbOut C:/temp/hat.png]       write a single picture here instead of into the project
    //     [-thumbMissing]                   skip pictures that already exist
    // Everything runs locally in Unity; nothing is uploaded or sent anywhere.
    [InitializeOnLoad]
    public static class WardrobeThumbnailRig
    {
        const string RequestKey = "DeadlyJenga.WardrobeThumbnailRig";
        const int Layer = 31;
        const int Size = 256;
        // Transparent by default so the wardrobe's own candy card shows behind the item.
        static readonly Color Backdrop = new Color(0, 0, 0, 0);

        [Serializable] sealed class Request { public string item = "", output = ""; public int color = -1; public bool missingOnly, batch; }
        [Serializable] sealed class Catalog { public AvatarAppearance[] skins, hair, headgear, eyewear, outfits, back, extras; }
        sealed class Job { public int slot; public AvatarAppearance entry; public int color; public string path; }

        static Request request;
        static readonly List<Job> jobs = new();
        static int jobIndex, waitFrames, written;
        static GameObject stage;
        static CandyPlayerRig rig;
        static Camera camera;
        static RenderTexture target;
        static Texture2D pixels;
        static Color[] bodyColors;

        static WardrobeThumbnailRig() { EditorApplication.playModeStateChanged += OnPlayMode; }

        [MenuItem("Tools/Deadly Jenga/Wardrobe Thumbnails/Render All")]
        static void RenderAll() => Start(new Request());

        [MenuItem("Tools/Deadly Jenga/Wardrobe Thumbnails/Render Missing")]
        static void RenderMissing() => Start(new Request { missingOnly = true });

        public static void Batch()
        {
            var args = Environment.GetCommandLineArgs();
            string Arg(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
            var r = new Request { batch = true, missingOnly = args.Contains("-thumbMissing"), item = Arg("-thumbItem") ?? "", output = Arg("-thumbOut") ?? "" };
            if (int.TryParse(Arg("-thumbColor"), out int c)) r.color = c;
            Start(r);
        }

        static void Start(Request r)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Stop Play Mode before rendering wardrobe thumbnails."); return; }
            if (!r.batch && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.SetString(RequestKey, JsonUtility.ToJson(r));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            string json = SessionState.GetString(RequestKey, "");
            if (string.IsNullOrEmpty(json)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                request = JsonUtility.FromJson<Request>(json);
                try { Build(); EditorApplication.update += Tick; }
                catch (Exception e) { Fail(e); }
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.EraseString(RequestKey);
                var r = JsonUtility.FromJson<Request>(json);
                if (string.IsNullOrEmpty(r.output)) { AssetDatabase.Refresh(); ConfigureImports(); }
                Debug.Log($"Wardrobe thumbnails: wrote {written} picture(s).");
                if (r.batch) EditorApplication.Exit(written > 0 || jobs.Count == 0 ? 0 : 2);
            }
        }

        static string ProjectFolder()
        {
            // Lives next to the wardrobe code, in Presentation's Resources.
            string anchor = ProjectPath.Find("WardrobeThumbnailLibrary.cs");
            string presentation = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(anchor), "..", "..", ".."));
            return Path.Combine(presentation, "Resources", WardrobeThumbnailLibrary.Folder);
        }

        static void Build()
        {
            var prefab = Resources.Load<CandyPlayerRig>("CandyPlayerVisual");
            if (!prefab) throw new InvalidOperationException("CandyPlayerVisual prefab is missing.");
            var asset = Resources.Load<TextAsset>("WardrobeCatalog");
            var catalog = asset ? JsonUtility.FromJson<Catalog>(asset.text) : null;
            if (catalog == null) throw new InvalidOperationException("WardrobeCatalog is missing.");

            stage = new GameObject("Wardrobe thumbnail stage");
            rig = Object.Instantiate(prefab, stage.transform);
            if (rig.TryGetComponent<CandyAvatarDetail>(out var detail)) detail.enabled = false;
            bodyColors = rig.style ? rig.style.colors : new[] { Color.white };

            camera = new GameObject("Thumbnail camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(stage.transform);
            camera.enabled = false;
            camera.cullingMask = 1 << Layer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Backdrop;
            camera.fieldOfView = 28;
            camera.nearClipPlane = .03f;
            camera.farClipPlane = 20;
            var data = camera.GetUniversalAdditionalCameraData();
            data.allowXRRendering = false;
            data.renderPostProcessing = false;
            target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = target;
            pixels = new Texture2D(Size, Size, TextureFormat.RGBA32, false);

            Light(new Vector3(-2.2f, 2.8f, 2.4f), new Color(1f, .9f, .8f), 18);
            Light(new Vector3(2.4f, 1.7f, 2.0f), new Color(.7f, .85f, 1f), 10);
            Light(new Vector3(.3f, 2.6f, -2.0f), Color.white, 14);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.45f, .45f, .5f);

            jobs.Clear(); jobIndex = 0; written = 0; waitFrames = 0;
            var groups = new[] { catalog.skins, catalog.hair, catalog.headgear, catalog.eyewear, catalog.outfits, catalog.back, catalog.extras };
            string folder = ProjectFolder();
            for (int slot = 0; slot < groups.Length; slot++)
                foreach (var entry in groups[slot] ?? Array.Empty<AvatarAppearance>())
                {
                    string key = WardrobeThumbnailLibrary.Slots[slot] + "/" + WardrobeThumbnailLibrary.ItemKey(slot, entry);
                    if (request.item.Length > 0 && !string.Equals(request.item, key, StringComparison.OrdinalIgnoreCase)) continue;
                    int colors = slot == 0 ? 1 : bodyColors.Length;
                    for (int c = 0; c < colors; c++)
                    {
                        if (slot != 0 && request.color >= 0 && c != request.color) continue;
                        string path = request.output.Length > 0 ? request.output
                            : Path.Combine(folder, WardrobeThumbnailLibrary.ResourcePath(slot, entry, c).Substring(WardrobeThumbnailLibrary.Folder.Length + 1) + ".png");
                        if (request.missingOnly && File.Exists(path)) continue;
                        jobs.Add(new Job { slot = slot, entry = entry, color = c, path = path });
                        if (request.output.Length > 0) break;
                    }
                    if (request.output.Length > 0 && jobs.Count > 0) break;
                }
            if (request.item.Length > 0 && jobs.Count == 0) Debug.LogError("No wardrobe item named " + request.item + " (use Slot/Item, e.g. Headgear/CowboyHat).");
            if (request.output.Length == 0)
            {
                Directory.CreateDirectory(folder);
                var manifest = new WardrobeThumbnailLibrary.Manifest { colors = bodyColors, size = Size, backdrop = "transparent" };
                File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonUtility.ToJson(manifest, true));
            }
        }

        static void Light(Vector3 position, Color color, float intensity)
        {
            var light = new GameObject("Thumbnail light", typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(stage.transform);
            light.transform.localPosition = position;
            light.type = LightType.Point; light.color = color; light.intensity = intensity; light.range = 9;
            light.cullingMask = 1 << Layer;
        }

        static void Tick()
        {
            try
            {
                if (jobIndex >= jobs.Count) { Finish(); return; }
                var job = jobs[jobIndex];
                if (waitFrames == 0)
                {
                    rig.SetColor(job.slot == 0 ? 0 : job.color);
                    var look = job.entry;
                    if (job.slot != 0) look.skin = AvatarSkin.Default;
                    rig.SetAppearance(look);
                    Pose();
                }
                // Give attachments two frames to build before taking the picture.
                if (++waitFrames < 3) return;
                waitFrames = 0;
                Pose();
                Frame(job.slot);
                Directory.CreateDirectory(Path.GetDirectoryName(job.path));
                File.WriteAllBytes(job.path, Capture().EncodeToPNG());
                written++;
                jobIndex++;
                if (jobIndex % 25 == 0) Debug.Log($"Wardrobe thumbnails: {jobIndex}/{jobs.Count}");
            }
            catch (Exception e) { Fail(e); }
        }

        static void Pose()
        {
            var feet = Vector3.zero; var facing = Quaternion.identity;
            CandyHandPose Hand(bool left)
            {
                float side = left ? -1 : 1;
                return new CandyHandPose
                {
                    position = feet + facing * new Vector3(side * .31f, .76f, .09f),
                    rotation = facing * Quaternion.Euler(0, side * 12, side * 85),
                    tracked = true, skeletonTracked = true, skeleton = CandyHandSkeleton.Rest(left, .04f)
                };
            }
            rig.ApplyFrame(new CandyAvatarFrame { feet = feet, head = feet + Vector3.up * 1.6f, headRotation = facing, scale = 1, left = Hand(true), right = Hand(false) });
        }

        // Head items get a head-and-shoulders close-up; back items are seen from behind; the rest full body.
        static void Frame(int slot)
        {
            var renderers = rig.GetComponentsInChildren<Renderer>(false);
            foreach (var r in renderers) r.gameObject.layer = Layer;
            var body = new Bounds(new Vector3(0, .95f, 0), new Vector3(.8f, 1.9f, .6f));
            foreach (var r in renderers) if (r.enabled) body.Encapsulate(r.bounds);
            var focus = body;
            bool head = slot == 1 || slot == 2 || slot == 3;
            if (head && rig.headRenderer)
            {
                var h = rig.headRenderer.bounds;
                focus = h;
                foreach (var r in renderers) if (r.enabled && r.bounds.center.y > h.min.y - .05f) focus.Encapsulate(r.bounds);
                focus.Expand(.12f);
            }
            else if (slot == 4 || slot == 6)
            {
                // Outfits and extras (noses, jaws, bow ties, hoodies): waist up, so small items read.
                focus.SetMinMax(new Vector3(body.min.x, Mathf.Lerp(body.min.y, body.max.y, .42f), body.min.z), body.max);
            }
            float yaw = slot == 5 ? 155f : 28f;
            float pitch = head ? 6f : 4f;
            var rotation = Quaternion.Euler(pitch, 180f + yaw, 0);
            float radius = focus.extents.magnitude;
            float distance = radius / Mathf.Sin(camera.fieldOfView * .5f * Mathf.Deg2Rad) * (head ? 1.08f : .95f);
            camera.transform.position = focus.center - rotation * Vector3.forward * distance;
            camera.transform.rotation = rotation;
        }

        static Texture2D Capture()
        {
            var previous = RenderTexture.active;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            pixels.Apply();
            RenderTexture.active = previous;
            return pixels;
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            if (camera) camera.targetTexture = null;
            if (target) target.Release();
            EditorApplication.ExitPlaymode();
        }

        static void Fail(Exception e)
        {
            Debug.LogException(e);
            EditorApplication.update -= Tick;
            EditorApplication.ExitPlaymode();
        }

        // Small, uncompressed-looking UI textures: no mipmaps, clamp, alpha kept.
        static void ConfigureImports()
        {
            string root = ProjectFolder().Replace('\\', '/');
            int assets = root.IndexOf("/Assets/", StringComparison.Ordinal);
            if (assets < 0) return;
            string assetRoot = root.Substring(assets + 1);
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { assetRoot }))
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                if (!importer || (!importer.mipmapEnabled && importer.maxTextureSize == Size && importer.alphaIsTransparency)) continue;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = Size;
                importer.SaveAndReimport();
            }
        }
    }
}
