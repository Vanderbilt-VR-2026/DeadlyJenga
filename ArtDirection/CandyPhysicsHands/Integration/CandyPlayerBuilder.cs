using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Hands.OpenXR;
using UnityEngine.XR.OpenXR;
using DeadlyJenga.CastlePrototype;

namespace DeadlyJenga.CandyPlayers.Editor
{
    public static class CandyPlayerBuilder
    {
        public const string Root="Assets/CandyPlayers/";
        public const string PrefabPath=Root+"Resources/CandyPlayerVisual.prefab";
        public static CandyPlayerStyle Style=>AssetDatabase.LoadAssetAtPath<CandyPlayerStyle>(Root+"Candy Player Style.asset");
        [MenuItem("Tools/Candy Players/Rebuild Smooth Visual Meshes")]
        public static void RebuildVisual()
        {
            if(Application.isPlaying)throw new System.InvalidOperationException("Exit Play Mode before rebuilding visuals.");
            // Visual-only rebuild: keep locomotion, tracking settings and gameplay prefabs intact.
            Directory.CreateDirectory(Root+"Meshes");var go=new GameObject("Candy Player Visual");
            try
            {
                var rig=go.AddComponent<CandyPlayerRig>();rig.style=Style;rig.BuildModel();
                Mesh Persist(Mesh generated,string path)
                {
                    var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(!saved){AssetDatabase.CreateAsset(generated,path);return generated;}
                    saved.Clear();saved.indexFormat=generated.indexFormat;saved.vertices=generated.vertices;saved.normals=generated.normals;saved.uv=generated.uv;
                    saved.boneWeights=generated.boneWeights;saved.bindposes=generated.bindposes;saved.triangles=generated.triangles;saved.bounds=generated.bounds;
                    EditorUtility.SetDirty(saved);Object.DestroyImmediate(generated);return saved;
                }
                rig.bodyRenderer.sharedMesh=Persist(rig.bodyRenderer.sharedMesh,Root+"Meshes/Candy Body.asset");
                rig.headRenderer.sharedMesh=Persist(rig.headRenderer.sharedMesh,Root+"Meshes/Candy Head.asset");
                PrefabUtility.SaveAsPrefabAsset(go,PrefabPath);AssetDatabase.SaveAssets();
            }
            finally{Object.DestroyImmediate(go);}
        }
        [MenuItem("Tools/Candy Players/Build And Install")]
        public static void Install()
        {
            if(Application.isPlaying)throw new System.InvalidOperationException("Exit Play Mode before installing avatar assets.");
            Directory.CreateDirectory(Root+"Resources");Directory.CreateDirectory(Root+"Meshes");AssetDatabase.Refresh();
            var style=Style;
            if(!style){style=ScriptableObject.CreateInstance<CandyPlayerStyle>();AssetDatabase.CreateAsset(style,Root+"Candy Player Style.asset");}
            if(!style.material)
            {
                var shader=Shader.Find("Universal Render Pipeline/Complex Lit");if(!shader)shader=Shader.Find("Universal Render Pipeline/Lit");
                var mat=new Material(shader){name="Glossy candy character",enableInstancing=true};mat.SetColor("_BaseColor",Color.yellow);mat.SetFloat("_Smoothness",style.smoothness);mat.SetFloat("_Metallic",0);
                if(mat.HasProperty("_ClearCoatMask")){mat.SetFloat("_ClearCoatMask",.5f);mat.SetFloat("_ClearCoatSmoothness",.88f);mat.EnableKeyword("_CLEARCOAT");}
                AssetDatabase.CreateAsset(mat,Root+"Candy Character.mat");style.material=mat;EditorUtility.SetDirty(style);
            }
            if(!AssetDatabase.LoadAssetAtPath<CandyPlayerRig>(PrefabPath))
            {
                var go=new GameObject("Candy Player Visual");var rig=go.AddComponent<CandyPlayerRig>();rig.style=style;rig.BuildModel();
                AssetDatabase.CreateAsset(rig.bodyRenderer.sharedMesh,Root+"Meshes/Candy Body.asset");AssetDatabase.CreateAsset(rig.headRenderer.sharedMesh,Root+"Meshes/Candy Head.asset");
                PrefabUtility.SaveAsPrefabAsset(go,PrefabPath);Object.DestroyImmediate(go);
            }
            var visual=AssetDatabase.LoadAssetAtPath<CandyPlayerRig>(PrefabPath);
            string playerPath="Assets/CastlePrototype/Prefabs/CastleSurvivor.prefab";
            var player=PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                var motor=player.GetComponent<CastleSurvivor>();motor.jumpHeight=1.82f;motor.vaultReach=.65f;motor.vaultDuration=.65f;
                var avatar=player.GetComponent<CandyPlayerAvatar>();if(!avatar)avatar=player.AddComponent<CandyPlayerAvatar>();
                avatar.visualPrefab=visual;avatar.survivor=motor;avatar.viewCamera=motor.viewCamera;avatar.trackingOrigin=motor.cameraOffset;
                PrefabUtility.SaveAsPrefabAsset(player,playerPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(player);}
            foreach(var target in new[]{BuildTargetGroup.Standalone,BuildTargetGroup.Android})
            {
                UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(target);
                var settings=OpenXRSettings.GetSettingsForBuildTargetGroup(target);if(!settings)continue;
                var feature=settings.GetFeature<HandTracking>();
                if(!feature)throw new System.InvalidOperationException("OpenXR Hand Tracking Subsystem feature is missing for "+target+". Refresh after XR Hands finishes importing.");
                feature.enabled=true;EditorUtility.SetDirty(feature);EditorUtility.SetDirty(settings);
            }
            AssetDatabase.SaveAssets();Debug.Log("Candy avatar installed. Hands and body contain no physics components; OpenXR hand tracking enabled.");
        }
        [MenuItem("Tools/Candy Players/Export Reusable Player Package")]
        public static void Export()
        {
            Directory.CreateDirectory("ArtDirection/CandyPlayers");
            var paths=new System.Collections.Generic.List<string>();
            foreach(var guid in AssetDatabase.FindAssets("",new[]{Root.TrimEnd('/')}))
            {var path=AssetDatabase.GUIDToAssetPath(guid);if(!AssetDatabase.IsValidFolder(path))paths.Add(path);}
            paths.Add("Assets/CastlePrototype/Prefabs/CastleSurvivor.prefab");
            paths.Add("Assets/Scripts/LocalHandInput.cs");paths.Add("Assets/Scripts/NetworkHands.cs");
            AssetDatabase.ExportPackage(paths.ToArray(),"ArtDirection/CandyPlayers/CandyPlayers.unitypackage",ExportPackageOptions.IncludeDependencies);
        }
    }
}
