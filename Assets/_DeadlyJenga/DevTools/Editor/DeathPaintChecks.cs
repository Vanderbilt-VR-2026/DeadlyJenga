using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class DeathPaintChecks
    {
        static DeathPaintChecks()=>EditorApplication.update+=Poll;
        static void Poll()
        {
            string request=ProjectPath.Report("DeathPaint.request");
            if(!File.Exists(request)||File.ReadAllText(request).Trim()!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString()||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(!ValidationRunGate.TryAcquire("DeathPaint"))return;
            File.Delete(request);
            try{Run();}catch(Exception e){File.WriteAllText(ProjectPath.Report("DeathPaint.txt"),"FAIL "+e);}
            finally{ValidationRunGate.Release("DeathPaint");}
        }
        internal static void Run()
        {
            var shader=Shader.Find("Candy Tropical/Death Paint");
            if(!shader||!shader.isSupported||UnityEditor.ShaderUtil.ShaderHasError(shader))throw new Exception("Paint shader failed compilation");
            var material=new Material(shader);var rt=RenderTexture.GetTemporary(1280,800,0,RenderTextureFormat.ARGB32);
            var pixels=new Texture2D(1280,800,TextureFormat.RGBA32,false);var old=RenderTexture.active;
            string report="GPU shader preview; not a gameplay or headset test.\n";
            try
            {
                var samples=new[]{(.06f,0f),(.35f,0f),(4f,0f),(4f,.5f),(4f,1f)};
                for(int sample=0;sample<samples.Length;sample++)
                {
                    var (age,wipe)=samples[sample];
                    RenderTexture.active=rt;GL.Clear(true,true,Color.clear);
                    material.SetFloat("_Age",age);material.SetFloat("_Aspect",1.6f);material.SetFloat("_Wipe",wipe);
                    // Vertex tint matches the gameplay Image tint.
                    GL.PushMatrix();GL.LoadOrtho();material.SetPass(0);GL.Begin(GL.QUADS);GL.Color(new Color(.15f,.75f,1,1));
                    GL.TexCoord2(0,0);GL.Vertex3(0,0,0);GL.TexCoord2(1,0);GL.Vertex3(1,0,0);
                    GL.TexCoord2(1,1);GL.Vertex3(1,1,0);GL.TexCoord2(0,1);GL.Vertex3(0,1,0);GL.End();GL.PopMatrix();
                    pixels.ReadPixels(new Rect(0,0,1280,800),0,0);pixels.Apply();
                    File.WriteAllBytes(ProjectPath.Report("DeathPaint-"+sample+".png"),pixels.EncodeToPNG());
                    int visible=0,opaque=0;foreach(var c in pixels.GetPixels32()){if(c.a>20)visible++;if(c.a>=254)opaque++;}
                    int total=1280*800;
                    if(age>.28f&&wipe==0&&opaque!=total)throw new Exception("Splat left uncovered pixels: "+(total-opaque));
                    if(wipe==.5f&&(visible<total/5||visible>total*4/5))throw new Exception("Wipe did not reveal a partial view: "+visible);
                    if(wipe==1&&visible>0)throw new Exception("Paint did not clear");
                    report+="PASS age="+age+" wipe="+wipe+" visiblePixels="+visible+" opaquePixels="+opaque+"\n";
                }
                File.WriteAllText(ProjectPath.Report("DeathPaint.txt"),report+"COMPLETE\n");
            }
            finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(material);}
        }
    }
}
