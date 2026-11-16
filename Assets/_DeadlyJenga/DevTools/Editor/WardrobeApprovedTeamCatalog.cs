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
                Object.Destroy(batch.Pixels);
                batch=null;
                if (File.Exists(Path.Combine(output,"skin-errors.txt")))
                    throw new InvalidOperationException("One or more approved skins failed capture.");
                File.WriteAllText(Path.Combine(output,"catalog-complete.txt"),"57 approved skins, 741 images.");
                Debug.Log("WARDROBE_APPROVED_TEAM_CATALOG_COMPLETE 741 images.");
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
            return new Batch { Output=output, Stage=stage, Pixels=new Texture2D(720,720,TextureFormat.RGB24,false),
                Recipes=recipes };
        }

        static void CaptureSkin(Batch batch, Recipe recipe)
        {
            var rows = new List<string>();
            if (recipe.Dll == "-")
            {
                Bounds frame = default;
                for (int team=-1; team<ModeNames.Colors.Length; team++)
                {
                    Color? color = team < 0 ? null : ModeNames.Colors[team];
                    var avatar = NewRig(recipe.Skin, batch.Stage.transform);
                    try
                    {
                        using var rabbit = recipe.Skin == AvatarSkin.Rabbit ? new CaramelRabbitPreview() : null;
                        using var store = recipe.Skin == AvatarSkin.Rabbit ? null : new StoreSkinPreview(color);
                        if (rabbit != null) rabbit.Apply(avatar,true,color);
                        else store.Apply(avatar,recipe.Skin);
                        var measured = (Bounds)Call("Measure",avatar);
                        if (team < 0) frame=measured;
                        Color actual = avatar.bodyRenderer.sharedMaterial.GetColor("_BaseColor");
                        if (color.HasValue && color.Value != actual)
                            throw new InvalidOperationException($"Accepted palette did not apply to {recipe.Skin}");
                        Shot(batch,recipe.Skin,team,frame,rows);
                    }
                    finally { Remove(avatar); }
                }
            }
            else
            {
                var avatar = NewRig(recipe.Skin,batch.Stage.transform);
                try
                {
                    using var design = ApplyRecordedRecipe(batch, recipe, avatar);
                    var frame = (Bounds)Call("Measure",avatar);
                    var surfaces = TeamSurfaces(avatar,recipe.Skin);
                    if (surfaces.Count == 0) throw new InvalidOperationException($"No team surfaces on {recipe.Skin}");
                    Shot(batch,recipe.Skin,-1,frame,rows);
                    var properties = new MaterialPropertyBlock();
                    for (int team=0; team<ModeNames.Colors.Length; team++)
                    {
                        foreach (var item in surfaces) ApplyTint(item,ModeNames.Colors[team],properties);
                        Shot(batch,recipe.Skin,team,frame,rows);
                    }
                }
                finally { Remove(avatar); }
            }

            File.AppendAllLines(Path.Combine(batch.Output,"catalog.tsv"),rows);
            Debug.Log($"WARDROBE_CATALOG_SKIN {recipe.Skin} {batch.Index+1}/{batch.Recipes.Count}");
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
