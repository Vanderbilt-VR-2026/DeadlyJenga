using System;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class ThemedSkyInstall
    {
        static ThemedSkyInstall(){EditorApplication.update+=Tick;}
        static void Tick()
        {
            string request=ProjectPath.Report("ThemedSkyInstall.request");
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(File.ReadAllText(request).Trim()!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString())return;
            File.Delete(request);
            try{Install();}catch(Exception e){File.WriteAllText(ProjectPath.Report("ThemedSkyInstall-error.txt"),e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Tools/Deadly Jenga/Maps/Install Glass and Honeycomb Skies")]
        public static void Install()
        {
            var shader=Resources.Load<Shader>("FantasyMaps/CarnivalCube");
            if(!shader||ShaderUtil.GetShaderMessages(shader).Any(m=>m.severity.ToString()=="Error"))throw new InvalidOperationException("Shared sky shader did not compile.");
            var report=new StringBuilder();
            InstallOne("GlassSkyCube","Mode_epic-candy-glass-cloud.prefab","Glass",shader,report);
            InstallOne("HiveSkyCube","Mode_epic-honeycomb-hive.prefab","Honeycomb",shader,report);
            AssetDatabase.SaveAssets();File.WriteAllText(ProjectPath.Report("ThemedSkyInstall.txt"),report.ToString());
        }
        static void InstallOne(string prefix,string prefab,string gallery,Shader shader,StringBuilder report)
        {
            string first=ProjectPath.Find(prefix+"-front-v1.png");if(string.IsNullOrEmpty(first))throw new InvalidOperationException("Missing sky face: "+prefix);
            string art=Path.GetDirectoryName(Path.GetDirectoryName(first)).Replace('\\','/');
            string path=art+"/Materials/"+prefix+"-v1.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}material.shader=shader;
            foreach(string face in new[]{"front","right","back","left","up","down"})
            {
                string texturePath=ProjectPath.Find(prefix+"-"+face+"-v1.png");
                if(string.IsNullOrEmpty(texturePath))throw new InvalidOperationException("Missing face: "+prefix+" "+face);
                var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.mipmapEnabled=true;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;
                importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if(texture.width!=1254||texture.height!=1254)throw new InvalidOperationException("Native resolution changed: "+texturePath);
                material.SetTexture("_"+char.ToUpperInvariant(face[0])+face.Substring(1),texture);
            }
            material.SetFloat("_Exposure",1);material.SetFloat("_Rotation",0);EditorUtility.SetDirty(material);
            string mapPath=ProjectPath.Find(prefab);var root=PrefabUtility.LoadPrefabContents(mapPath);
            try{var view=root.GetComponent<FantasyMapArt>();view.skyMaterial=material;view.texturedEnvironmentSky=true;PrefabUtility.SaveAsPrefabAsset(root,mapPath);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
            CarnivalSkyInstall.CaptureSky(material,Path.Combine(Path.GetDirectoryName(Application.dataPath),"ArtDirection","FantasySkies-20261026",gallery));
            report.AppendLine("PASS "+gallery+": six native 1254x1254 faces; shared URP stereo shader; prefab sky reference saved; distant placeholders disabled; twelve directional/join renders. Physics unchanged; no headset test.");
        }
    }
}
