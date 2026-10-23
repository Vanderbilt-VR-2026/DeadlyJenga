using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.Presentation.Editor
{
    public static class NeonPixelChecks
    {
        public static void Run()
        {
            var report=new StringBuilder();int failed=0;
            Check((ok,label)=>{report.AppendLine((ok?"PASS ":"FAIL ")+label);if(!ok)failed++;});
            File.WriteAllText(ProjectPath.Report("NeonPixels.txt"),report.ToString());Debug.Log(report.ToString());
            if(failed>0)throw new InvalidOperationException("Neon pixel regression failed");
        }
        public static void Check(Action<bool,string> check)
        {
            GameObject cube=null,cameraRoot=null;Material material=null;Texture2D image=null;RenderTexture target=null;var previous=RenderTexture.active;
            try
            {
                var shader=Resources.Load<Shader>("CandyScenery/CandyNeon");if(!shader)throw new InvalidOperationException("Neon shader missing");
                cube=new GameObject("QA isolated neon cube",typeof(MeshFilter),typeof(MeshRenderer));cube.layer=31;cube.transform.position=Vector3.one*4000;
                cube.GetComponent<MeshFilter>().sharedMesh=Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                material=new Material(shader);material.SetColor("_BaseColor",Color.blue);material.SetFloat("_Filled",0);material.SetFloat("_Rim",1000);material.SetVector("_BoundsCenter",Vector4.zero);material.SetVector("_BoundsSize",Vector3.one);cube.GetComponent<MeshRenderer>().sharedMaterial=material;
                cameraRoot=new GameObject("QA isolated neon camera",typeof(Camera));var camera=cameraRoot.GetComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.fieldOfView=55;camera.aspect=1;camera.transform.position=cube.transform.position+Vector3.back*3;camera.transform.LookAt(cube.transform.position);
                target=RenderTexture.GetTemporary(new RenderTextureDescriptor(256,256,RenderTextureFormat.ARGB32,24){sRGB=QualitySettings.activeColorSpace==ColorSpace.Linear});camera.targetTexture=target;
                image=new Texture2D(256,256,TextureFormat.RGB24,false);camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();
                var edge=camera.WorldToViewportPoint(cube.transform.position+new Vector3(.485f,0,-.5f));Color center=image.GetPixel(128,128),rim=image.GetPixel(Mathf.Clamp(Mathf.RoundToInt(edge.x*255),0,255),Mathf.Clamp(Mathf.RoundToInt(edge.y*255),0,255));
                check(center.b<.3f,"Unselected neon cube face center stays dark: "+center.b.ToString("0.000"));check(rim.b>.4f&&rim.b>center.b*2,"Unselected cube edge glows independently of face: "+rim.b.ToString("0.000"));
                material.SetFloat("_Filled",1);camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();check(image.GetPixel(128,128).b>.5f,"Selected neon bulb fills its face with glow");
            }
            finally
            {
                RenderTexture.active=previous;if(cameraRoot){cameraRoot.GetComponent<Camera>().targetTexture=null;UnityEngine.Object.DestroyImmediate(cameraRoot);}if(cube)UnityEngine.Object.DestroyImmediate(cube);if(material)UnityEngine.Object.DestroyImmediate(material);if(image)UnityEngine.Object.DestroyImmediate(image);if(target)RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
