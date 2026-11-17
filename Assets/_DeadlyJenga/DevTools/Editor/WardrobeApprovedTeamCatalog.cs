using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class WardrobeApprovedTeamCatalog
    {
        const string Fixture = "DeadlyJenga.WardrobeApprovedTeamCatalog";
        const string BridgeFolder = "DeadlyJenga.WardrobePreviewBridge";
        const BindingFlags Private = BindingFlags.Static | BindingFlags.NonPublic;
        static readonly Type Bridge = typeof(WardrobePreviewBridge);

        static WardrobeApprovedTeamCatalog()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetString(Fixture, "") != "")
                    EditorApplication.update += CaptureWhenReady;
                if (state == PlayModeStateChange.ExitingPlayMode)
                    EditorApplication.update -= CaptureWhenReady;
            };
        }

        public static void Run()
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Use the reserved batch editor.");
            RuntimeHelpers.RunClassConstructor(Bridge.TypeHandle);
            string output = Path.GetFullPath(ProjectPath.Report(
                "WardrobeApprovedTeamCatalog-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
            Directory.CreateDirectory(output);
            SessionState.SetString(Fixture, output);
            SessionState.SetString(BridgeFolder, output);
            SessionState.SetFloat(Fixture + ".Deadline", (float)EditorApplication.timeSinceStartup + 105f);
            Debug.Log("WARDROBE_APPROVED_TEAM_CATALOG_OUTPUT " + output);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }

        static void CaptureWhenReady()
        {
            string output = SessionState.GetString(Fixture, "");
            if (string.IsNullOrEmpty(output)) return;
            try
            {
                bool ready = (bool)Bridge.GetField("castleReady", Private).GetValue(null);
                if (!ready && !(bool)Call("TryPrepareCastle"))
                {
                    if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Fixture + ".Deadline", 0))
                        throw new TimeoutException("Castle preview did not become ready.");
                    return;
                }
                if (batch == null) batch=OpenBatch(output);
                if (batch.Index < batch.Recipes.Count)
                {
                    var recipe=batch.Recipes[batch.Index];
                    try { CaptureSkin(batch,recipe); }
                    catch (Exception error)
                    {
                        File.AppendAllText(Path.Combine(output,"skin-errors.txt"),recipe.Skin+": "+error+"\n");
                        Debug.LogWarning($"Catalog capture skipped {recipe.Skin}: {error.Message}");
                    }
                    batch.Index++;
                    return;
                }
                EditorApplication.update -= CaptureWhenReady;
                if (!File.Exists(Path.Combine(output,"skin-errors.txt")))
                {
                    AssetDatabase.SaveAssets();
                    var folder = batch.AssetFolder;
                    var destination = Path.Combine(output, "WardrobeSkins");
                    Directory.CreateDirectory(destination);
                    foreach (var file in Directory.GetFiles(folder)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
                    File.Copy(folder + ".meta", destination + ".meta");
                }
                batch=null;
                if (File.Exists(Path.Combine(output,"skin-errors.txt")))
                    throw new InvalidOperationException("One or more approved skins failed capture.");
                File.WriteAllText(Path.Combine(output,"catalog-complete.txt"),"57 approved originals plus accepted team variants exported.");
                Debug.Log("WARDROBE_APPROVED_TEAM_CATALOG_COMPLETE asset export.");
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(output, "unity-error.txt"), error.ToString());
                Debug.LogException(error);
            }

            SessionState.EraseString(Fixture);
            SessionState.EraseFloat(Fixture + ".Deadline");
            EditorApplication.ExitPlaymode();
        }

        static object Call(string method, params object[] arguments) =>
            Bridge.GetMethod(method, Private).Invoke(null, arguments);


        sealed class Recipe
        {
            public AvatarSkin Skin;
            public string Dll;
            public string Trial;
            public string Sha;
        }

        sealed class Batch
        {
            public string Output;
            public GameObject Stage;
            public Texture2D Pixels;
            public List<Recipe> Recipes;
            public int Index;
            public string AssetFolder;
            public readonly Dictionary<string, Assembly> Assemblies = new Dictionary<string, Assembly>();
        }

        static Batch batch;

        static Batch OpenBatch(string output)
        {
            var stage = (GameObject)Bridge.GetField("root", Private).GetValue(null);
            var main = (CandyPlayerRig)Bridge.GetField("rig", Private).GetValue(null);
            if (!stage || !main) throw new InvalidOperationException("Production Castle preview rig is missing.");
            main.gameObject.SetActive(false);
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents",
                "Codex", "Deliverables", "DeadlyJenga-Workboard", "requests",
                "sprint4-wardrobe-all-approved-team-colors-recipes.tsv");
            var recipes = new List<Recipe>();
            var seen = new HashSet<AvatarSkin>();
            foreach (var line in File.ReadAllLines(path))
            {
                string[] fields = line.Split('\t');
                if (fields.Length != 4) throw new InvalidOperationException("Malformed approved recipe record.");
                var skin = (AvatarSkin)Enum.Parse(typeof(AvatarSkin), fields[0]);
                if (skin == AvatarSkin.Default || !seen.Add(skin))
                    throw new InvalidOperationException("Invalid approved recipe identity " + skin);
                recipes.Add(new Recipe { Skin=skin, Dll=fields[1], Trial=fields[2], Sha=fields[3] });
            }
            if (recipes.Count != 57 || ModeNames.Colors.Length != 12)
                throw new InvalidOperationException("Unexpected catalog or team color count.");
            var catalog = Resources.Load<TextAsset>("WardrobeCatalog");
            string resources = Path.GetDirectoryName(AssetDatabase.GetAssetPath(catalog)).Replace('\\','/');
            AssetDatabase.CreateFolder(resources, "WardrobeSkins");
            string assetFolder = resources + "/WardrobeSkins";
            return new Batch { Output=output, Stage=stage, AssetFolder=assetFolder, Recipes=recipes };
        }

        static void CaptureSkin(Batch batch, Recipe recipe)
        {
            if (recipe.Dll != "-")
            {
                var avatar = NewRig(recipe.Skin, batch.Stage.transform);
                try
                {
                    using var look = ApplyRecordedRecipe(batch, recipe, avatar);
                    Export(batch, avatar, recipe.Skin.ToString());
                }
                finally { Remove(avatar); }
                return;
            }
            for (int team = -1; team < ModeNames.Colors.Length; team++)
            {
                Color? color = team < 0 ? null : ModeNames.Colors[team];
                string name = recipe.Skin.ToString();
                if (color.HasValue) name += "-" + ColorUtility.ToHtmlStringRGB(color.Value);
                var avatar = NewRig(recipe.Skin, batch.Stage.transform);
                try
                {
                    using var rabbit = recipe.Skin == AvatarSkin.Rabbit ? new CaramelRabbitPreview() : null;
                    using var store = recipe.Skin == AvatarSkin.Rabbit ? null : new StoreSkinPreview(color);
                    if (rabbit != null) rabbit.Apply(avatar, true, color);
                    else store.Apply(avatar, recipe.Skin);
                    if (color.HasValue && avatar.bodyRenderer.sharedMaterial.GetColor("_BaseColor") != color.Value)
                        throw new InvalidOperationException("Approved team palette did not apply to " + name);
                    Export(batch, avatar, name);
                }
                finally { Remove(avatar); }
            }
        }

        static void Export(Batch batch, CandyPlayerRig rig, string id)
        {
            var asset = ScriptableObject.CreateInstance<WardrobeSkinAsset>();
            string path = batch.AssetFolder + "/" + id + ".asset";
            AssetDatabase.CreateAsset(asset, path);
            var saved = new Dictionary<Object, Object>();
            var bones = new Dictionary<Transform, int>();
            for (int index = 0; index < rig.BoneCount; index++) bones[rig.AppearanceBone(index)] = index;
            bones[rig.SpineBone] = rig.BoneCount;
            bones[rig.ChestBone] = rig.BoneCount + 1;

            T Persist<T>(T source) where T : Object
            {
                if (!source || AssetDatabase.Contains(source)) return source;
                if (saved.TryGetValue(source, out var existing)) return (T)existing;
                var copy = Object.Instantiate(source);
                copy.name = source.name + " " + saved.Count;
                saved.Add(source, copy);
                if (copy is Material material)
                    foreach (var property in material.GetTexturePropertyNames())
                        material.SetTexture(property, Persist(material.GetTexture(property)));
                AssetDatabase.AddObjectToAsset(copy, asset);
                return copy;
            }

            Material[] Materials(Renderer renderer)
            {
                var materials = renderer.sharedMaterials;
                var block = new MaterialPropertyBlock();
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    var source = materials[slot];
                    if (!source) continue;
                    renderer.GetPropertyBlock(block, slot);
                    if (block.isEmpty) renderer.GetPropertyBlock(block);
                    if (block.isEmpty)
                    {
                        materials[slot] = Persist(source);
                        continue;
                    }
                    var copy = new Material(source) { name = source.name };
                    foreach (var property in new[] { "_BaseColor", "_EmissionColor" })
                        if (block.HasColor(property)) copy.SetColor(property, block.GetColor(property));
                    foreach (var property in new[] { "_Smoothness", "_Metallic" })
                        if (block.HasFloat(property)) copy.SetFloat(property, block.GetFloat(property));
                    materials[slot] = Persist(copy);
                    Object.Destroy(copy);
                }
                return materials;
            }

            int BoneIndex(Transform bone)
            {
                if (!bone || bone == rig.transform) return -1;
                if (bones.TryGetValue(bone, out var index)) return index;
                throw new InvalidOperationException("Unbound wardrobe bone " + bone.name);
            }

            asset.headScale = rig.HeadAnchor.localScale;
            asset.bodyMesh = Persist(rig.bodyRenderer.sharedMesh);
            asset.headMesh = Persist(rig.headRenderer.sharedMesh);
            asset.bodyMaterials = Materials(rig.bodyRenderer);
            asset.headMaterials = Materials(rig.headRenderer);
            asset.bodyVisible = rig.bodyRenderer.enabled && !rig.bodyRenderer.forceRenderingOff;
            asset.headVisible = rig.headRenderer.enabled && !rig.headRenderer.forceRenderingOff;
            var parts = new List<WardrobeSkinAsset.Part>();
            foreach (var renderer in rig.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer.forceRenderingOff || renderer == rig.bodyRenderer || renderer == rig.headRenderer) continue;
                var skinned = renderer as SkinnedMeshRenderer;
                Mesh mesh = skinned ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (!mesh) continue;
                var anchor = renderer.transform.parent;
                while (anchor && anchor != rig.transform && !bones.ContainsKey(anchor)) anchor = anchor.parent;
                if (!anchor) throw new InvalidOperationException("Unbound wardrobe part " + renderer.name);
                Matrix4x4 relative = anchor.worldToLocalMatrix * renderer.localToWorldMatrix;
                parts.Add(new WardrobeSkinAsset.Part
                {
                    label = renderer.name,
                    anchor = BoneIndex(anchor),
                    position = relative.GetColumn(3),
                    rotation = relative.rotation,
                    scale = relative.lossyScale,
                    mesh = Persist(mesh),
                    materials = Materials(renderer),
                    shadows = renderer.shadowCastingMode,
                    bones = skinned ? Array.ConvertAll(skinned.bones, BoneIndex) : null,
                    rootBone = skinned ? BoneIndex(skinned.rootBone) : -1,
                    bounds = skinned ? skinned.localBounds : mesh.bounds
                });
            }
            asset.parts = parts.ToArray();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            File.AppendAllText(Path.Combine(batch.Output, "skin-assets.txt"), id + ": " + parts.Count + " parts\n");
        }

        static CandyPlayerRig NewRig(AvatarSkin skin, Transform stage)
        {
            var avatar = Object.Instantiate(Resources.Load<CandyPlayerRig>("CandyPlayerVisual"),stage);
            avatar.SetColor(0);
            if (avatar.TryGetComponent<CandyAvatarDetail>(out var detail)) detail.enabled=false;
            avatar.SetAppearance(new AvatarAppearance { skin=skin });
            Call("Pose",avatar);
            return avatar;
        }

        static void Remove(CandyPlayerRig avatar)
        {
            avatar.gameObject.SetActive(false);
            Object.Destroy(avatar.gameObject);
        }

        static IDisposable ApplyRecordedRecipe(Batch batch, Recipe recipe, CandyPlayerRig avatar)
        {
            if (!batch.Assemblies.TryGetValue(recipe.Dll,out var assembly))
            {
                byte[] bytes=File.ReadAllBytes(recipe.Dll);
                using var sha=SHA256.Create();
                string actual=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");
                if (!StringComparer.OrdinalIgnoreCase.Equals(actual,recipe.Sha))
                    throw new InvalidOperationException($"Review assembly hash changed for {recipe.Skin}");
                batch.Assemblies[recipe.Dll]=assembly=Assembly.Load(bytes);
            }

            var type=assembly.GetType("DeadlyJenga.DevTools.Editor.StoreSkinPreview",true);
            var look=(IDisposable)Activator.CreateInstance(type,true);
            try
            {
                var apply=Array.Find(type.GetMethods(BindingFlags.Instance|BindingFlags.Public),m=>m.Name=="Apply");
                if (apply == null) throw new InvalidOperationException("Approved recipe Apply method missing.");
                var arguments=new List<object>();
                bool actualTrial=false;
                foreach (var param in apply.GetParameters())
                {
                    if (param.ParameterType == typeof(CandyPlayerRig)) arguments.Add(avatar);
                    else if (param.ParameterType == typeof(AvatarSkin)) arguments.Add(recipe.Skin);
                    else if (param.ParameterType == typeof(bool)) arguments.Add(false);
                    else if (param.ParameterType.IsEnum)
                    {
                        arguments.Add(Enum.Parse(param.ParameterType,recipe.Trial));
                        actualTrial=true;
                    }
                    else throw new InvalidOperationException("Unknown StoreSkinPreview.Apply signature.");
                }
                if (!actualTrial && recipe.Trial!="Original")
                    throw new InvalidOperationException($"Approved variant {recipe.Trial} is missing for {recipe.Skin}");
                apply.Invoke(look,arguments.ToArray());
                return look;
            }
            catch { look.Dispose(); throw; }
        }

        readonly struct Surface
        {
            public readonly Renderer Renderer;
            public readonly Material Material;
            public readonly int Slot;
            public readonly float White;
            public Surface(Renderer renderer, Material material, int slot, float white)
            { Renderer=renderer; Material=material; Slot=slot; White=white; }
        }

        static List<Surface> TeamSurfaces(CandyPlayerRig avatar, AvatarSkin skin)
        {
            var output=new List<Surface>();
            var body=avatar.bodyRenderer.sharedMaterial;
            foreach(var renderer in avatar.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer.forceRenderingOff) continue;
                string name=renderer.name.ToLowerInvariant();
                var materials=renderer.sharedMaterials;
                for(int index=0;index<materials.Length;index++)
                {
                    var material=materials[index];
                    if (!material || !material.HasProperty("_BaseColor")) continue;
                    string finish=material.name.ToLowerInvariant();
                    bool accent=Has(name,"eye","tooth","teeth","fang","pupil","mouth","nostril","eyebrow","lens",
                        "pocket","button","buckle","muzzle","badge","gasket","seam","stitch","ear inset","hoof tip","horn");
                    bool main=material==body && !accent;
                    float white=0f;
                    switch(skin)
                    {
                        case AvatarSkin.Snowman:
                            main=Has(name,"scarf","hat band");
                            break;
                        case AvatarSkin.Ghost:
                            main=Has(name,"shroud");
                            white=.25f;
                            break;
                        case AvatarSkin.Astronaut:
                            main|=Has(name,"pressure vest","life support pack");
                            break;
                        case AvatarSkin.DeepSeaDiver:
                            main|=!accent&&Has(name,"suit torso","canvas suit","diving jacket","diver chest","canvas body");
                            break;
                        case AvatarSkin.Pineapple:
                            main|=Has(name,"pineapple shell","pineapple rind","carved rind");
                            break;
                        case AvatarSkin.Zebra:
                            main|=!accent&&Has(finish,"zebra stripe","striped zebra","stripe coat");
                            break;
                        case AvatarSkin.MushroomSprite:
                            main|=!accent&&Has(name,"mushroom cap","toadstool cap");
                            break;
                        default:
                            main|=!accent&&(Has(name,"breastplate","vest body","upper tunic","coat torso","chest shell",
                                "coat body","jacket body","jersey body","armor chest","plate body","shell top") ||
                                Has(finish,"main coat","main suit","body shell"));
                            break;
                    }
                    if (main) output.Add(new Surface(renderer,material,materials.Length>1?index:-1,white));
                }
            }
            return output;
        }

        static bool Has(string name,params string[] words)
        {
            foreach(string word in words) if(name.Contains(word)) return true;
            return false;
        }

        static void ApplyTint(Surface item, Color team, MaterialPropertyBlock properties)
        {
            if (item.Slot < 0) item.Renderer.GetPropertyBlock(properties);
            else item.Renderer.GetPropertyBlock(properties,item.Slot);
            Color shade=Color.Lerp(team,Color.white,item.White);
            properties.SetColor("_BaseColor",shade);
            if (item.Material.HasProperty("_EmissionColor"))
            {
                float authored=item.Material.GetColor("_EmissionColor").maxColorComponent;
                float original=Mathf.Max(.06f,item.Material.GetColor("_BaseColor").linear.maxColorComponent);
                properties.SetColor("_EmissionColor",shade.linear*Mathf.Min(.35f,authored/original));
            }
            if (item.Slot < 0) item.Renderer.SetPropertyBlock(properties);
            else item.Renderer.SetPropertyBlock(properties,item.Slot);
        }

        static void Shot(Batch batch, AvatarSkin skin, int team, Bounds frame, List<string> rows)
        {
            string label=team<0 ? "original" : ModeNames.ColorNames[team].ToLowerInvariant();
            string rgb=team<0 ? "-" : ColorUtility.ToHtmlStringRGB(ModeNames.Colors[team]);
            string name=$"{skin}-{label}.jpg";
            Call("PlaceCamera",frame,true,153f,8f,1f);
            Call("RenderView",batch.Pixels,true,0);
            batch.Pixels.Apply();
            File.WriteAllBytes(Path.Combine(batch.Output,name),batch.Pixels.EncodeToJPG(90));
            rows.Add($"{skin}\t{label}\t{rgb}\t{name}");
        }
    }
}
