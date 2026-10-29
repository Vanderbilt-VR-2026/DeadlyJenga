using System;
using System.IO;
using System.Linq;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class CarnivalSkyInstall
    {
        static CarnivalSkyInstall(){EditorApplication.update+=Tick;}
        static void Tick()
        {
            string request=ProjectPath.Report("CarnivalSkyInstall.request");
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(File.ReadAllText(request).Trim()!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString())return;
            File.Delete(request);
            try{Install();}catch(Exception e){File.WriteAllText(ProjectPath.Report("CarnivalSkyInstall-error.txt"),e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Tools/Deadly Jenga/Maps/Install Carnival Panorama")]
        public static void Install()
        {
            string[] faces={"front","right","back","left","up","down"};
            bool cube=faces.All(f=>!string.IsNullOrEmpty(ProjectPath.Find("CarnivalCube-"+f+"-v1.png")));
            string texturePath=ProjectPath.Find("CarnivalPanorama-v1.png");
            if(string.IsNullOrEmpty(texturePath))throw new InvalidOperationException("Carnival panorama texture is missing.");
            var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.mipmapEnabled=true;
            importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=8192;
            importer.wrapModeU=TextureWrapMode.Repeat;importer.wrapModeV=TextureWrapMode.Clamp;
            importer.filterMode=FilterMode.Trilinear;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            var shader=Resources.Load<Shader>(cube?"FantasyMaps/CarnivalCube":"FantasyMaps/CarnivalPanorama");
            if(!shader||ShaderUtil.GetShaderMessages(shader).Any(m=>m.severity.ToString()=="Error"))throw new InvalidOperationException("Panorama shader did not compile.");
            string art=Path.GetDirectoryName(Path.GetDirectoryName(texturePath)).Replace('\\','/');
            string materialPath=art+"/Materials/"+(cube?"CarnivalCube-v1.mat":"CarnivalPanorama-v1.mat");
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}
            material.shader=shader;
            if(cube)foreach(string face in faces)
            {
                string path=ProjectPath.Find("CarnivalCube-"+face+"-v1.png");
                var faceImporter=(TextureImporter)AssetImporter.GetAtPath(path);
                faceImporter.textureType=TextureImporterType.Default;faceImporter.sRGBTexture=true;faceImporter.mipmapEnabled=true;faceImporter.npotScale=TextureImporterNPOTScale.None;faceImporter.maxTextureSize=2048;
                faceImporter.wrapMode=TextureWrapMode.Clamp;faceImporter.filterMode=FilterMode.Trilinear;faceImporter.textureCompression=TextureImporterCompression.CompressedHQ;faceImporter.SaveAndReimport();
                var faceTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if(faceTexture.width!=1254||faceTexture.height!=1254)throw new InvalidOperationException("Native cube face resolution changed: "+face);
                material.SetTexture("_"+char.ToUpperInvariant(face[0])+face.Substring(1),faceTexture);
            }
            else material.SetTexture("_MainTex",texture);
            material.SetFloat("_Exposure",1);material.SetFloat("_Rotation",0);EditorUtility.SetDirty(material);
            string mapPath=ProjectPath.Find("Mode_epic-cloud-carnival.prefab");
            var root=PrefabUtility.LoadPrefabContents(mapPath);
            try{var view=root.GetComponent<FantasyMapArt>();view.skyMaterial=material;view.texturedCarnivalSky=cube;PrefabUtility.SaveAsPrefabAsset(root,mapPath);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
            CaptureSky(material);
            File.WriteAllText(ProjectPath.Report("CarnivalSkyInstall.txt"),cube?"Installed six native 1254x1254 faces (9,435,096 generated pixels). sRGB, mipmaps, trilinear, clamp. URP stereo shader compiled; soft cube edge blending. Carnival prefab saved with old distant placeholder scenery disabled; other maps untouched. Editor renders captured in six directions and across joins. Not a native 4K panorama; no Quest test.":"Installed "+texture.width+"x"+texture.height+" native panorama.");
        }
        static void CaptureSky(Material material)
        {
            CaptureSky(material,Path.Combine(Path.GetDirectoryName(Application.dataPath),"ArtDirection","CarnivalSky-20261026"));
        }
        public static void CaptureSky(Material material,string output)
        {
            Directory.CreateDirectory(output);
            Material previous=RenderSettings.skybox;var previousTarget=RenderTexture.active;
            var go=new GameObject("Temporary carnival sky verification");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=0;camera.clearFlags=CameraClearFlags.Skybox;camera.fieldOfView=90;
            string[] names={"front","right","back","left","up","down","join-front-right","join-right-back","join-back-left","join-left-front","join-front-up","join-front-down"};
            Vector3[] directions={Vector3.forward,Vector3.right,Vector3.back,Vector3.left,Vector3.up,Vector3.down,new Vector3(1,0,1),new Vector3(1,0,-1),new Vector3(-1,0,-1),new Vector3(-1,0,1),new Vector3(0,1,1),new Vector3(0,-1,1)};
            var rt=RenderTexture.GetTemporary(1600,1200,24);var image=new Texture2D(1600,1200,TextureFormat.RGB24,false);
            try
            {
                RenderSettings.skybox=material;camera.targetTexture=rt;
                for(int i=0;i<names.Length;i++)
                {
                    camera.transform.rotation=Quaternion.LookRotation(directions[i],i==4?Vector3.back:i==5?Vector3.forward:Vector3.up);
                    camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,1200),0,0);image.Apply();
                    File.WriteAllBytes(Path.Combine(output,"unity-sky-"+names[i]+".png"),image.EncodeToPNG());
                }
            }
            finally{RenderSettings.skybox=previous;RenderTexture.active=previousTarget;camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
