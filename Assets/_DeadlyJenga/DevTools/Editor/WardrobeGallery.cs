using System;
using System.IO;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;
namespace DeadlyJenga.DevTools.Editor
{
    public sealed class WardrobeGallery:IDisposable
    {
        readonly GameObject root;readonly Camera camera;readonly RenderTexture target;readonly Texture2D sheet,pixels;readonly TextMesh label;readonly Vector3 feet;int index;
        public WardrobeGallery(Vector3 feet)
        {
            this.feet=feet;root=new GameObject("Wardrobe capture studio");var view=new GameObject("Wardrobe capture camera",typeof(Camera));view.transform.SetParent(root.transform,false);camera=view.GetComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;camera.orthographic=true;camera.orthographicSize=1.35f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.065f,.10f);camera.nearClipPlane=.05f;camera.farClipPlane=15;camera.transform.position=feet+new Vector3(2.6f,1.3f,3.8f);camera.transform.LookAt(feet+Vector3.up);camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
            target=new RenderTexture(200,240,24);camera.targetTexture=target;sheet=new Texture2D(2000,1680,TextureFormat.RGB24,false);pixels=new Texture2D(200,240,TextureFormat.RGB24,false);
            foreach(var offset in new[]{new Vector3(-3,3,2),new Vector3(3,1,1)}){var light=new GameObject("Gallery softbox",typeof(Light));light.transform.SetParent(root.transform,false);light.transform.position=feet+offset;var source=light.GetComponent<Light>();source.type=LightType.Point;source.range=8;source.intensity=24;source.shadows=LightShadows.None;}
            var name=new GameObject("Skin name",typeof(TextMesh));name.transform.SetParent(root.transform,false);label=name.GetComponent<TextMesh>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;name.layer=31;label.fontSize=48;label.characterSize=.055f;label.anchor=TextAnchor.MiddleCenter;label.color=Color.white;label.transform.position=feet-Vector3.up*.18f;label.transform.rotation=Quaternion.LookRotation(label.transform.position-camera.transform.position);
        }
        public void Capture(AvatarSkin skin)
        {
            label.text=System.Text.RegularExpressions.Regex.Replace(skin.ToString(),"([a-z])([A-Z])","$1 $2");camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,200,240),0,0);pixels.Apply();RenderTexture.active=previous;sheet.SetPixels((index%10)*200,(6-index/10)*240,200,240,pixels.GetPixels());index++;
        }
        public void Dispose()
        {
            sheet.Apply();File.WriteAllBytes(ProjectPath.Report("Expanded-Wardrobe-Gallery.png"),sheet.EncodeToPNG());camera.targetTexture=null;target.Release();Object.Destroy(target);Object.Destroy(sheet);Object.Destroy(pixels);Object.Destroy(root);
        }
    }
}
