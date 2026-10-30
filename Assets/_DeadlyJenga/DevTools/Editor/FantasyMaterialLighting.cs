using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static class FantasyMaterialLighting
    {
        // Bake once in the explicit map builder, never during play or editor startup.
        public static Cubemap Bake(Material sky,string folder,int theme)
        {
            string path=folder+"/Fantasy sky reflection "+theme+".asset";
            var cube=AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if(!cube){cube=new Cubemap(256,TextureFormat.RGBAHalf,true){name="Fantasy sky reflection "+theme};AssetDatabase.CreateAsset(cube,path);}
            var go=new GameObject("Fantasy reflection bake");var camera=go.AddComponent<Camera>();camera.enabled=false;
            camera.cullingMask=0;camera.clearFlags=CameraClearFlags.Skybox;camera.allowHDR=true;
            var rt=new RenderTexture(256,256,24,RenderTextureFormat.ARGBHalf){dimension=TextureDimension.Cube,useMipMap=false};
            rt.Create();var oldSky=RenderSettings.skybox;var oldActive=RenderTexture.active;
            var pixels=new Texture2D(256,256,TextureFormat.RGBAHalf,false,true);
            try
            {
                RenderSettings.skybox=sky;
                if(!camera.RenderToCubemap(rt))throw new InvalidOperationException("Sky reflection bake failed.");
                for(int face=0;face<6;face++)
                {
                    Graphics.SetRenderTarget(rt,0,(CubemapFace)face);
                    pixels.ReadPixels(new Rect(0,0,256,256),0,0);pixels.Apply();cube.SetPixels(pixels.GetPixels(),(CubemapFace)face);
                }
                cube.Apply(true,false);cube.wrapMode=TextureWrapMode.Clamp;cube.filterMode=FilterMode.Trilinear;
                EditorUtility.SetDirty(cube);return cube;
            }
            finally{RenderSettings.skybox=oldSky;RenderTexture.active=oldActive;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);Object.DestroyImmediate(go);}
        }
    }
}
