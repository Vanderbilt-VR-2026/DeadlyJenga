using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Local-only review tool (never pushed): Marina camera captures + OBJ export for the web review page.
namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class ClaudeMarinaReviewExport
    {
        const string Key="ClaudeMarinaReviewExport";
        static ClaudeMarinaReviewExport(){EditorApplication.playModeStateChanged+=State;}
        public static void RunBatch()
        {
            File.WriteAllText(ProjectPath.Report("MayhemMarina.txt"),"Capture-only run.\n");
            SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(ProjectPath.Find("Home.unity"));EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){var go=new GameObject("Scratch capture driver");Object.DontDestroyOnLoad(go);go.AddComponent<MayhemMarinaChecksDriver>().StartCoroutine(Run());}
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.Exit(failed?2:0);}
        }
        static bool failed;
        static IEnumerator Wait(float s){float end=Time.realtimeSinceStartup+s;while(Time.realtimeSinceStartup<end)yield return null;}
        static IEnumerator Run()
        {
            Application.runInBackground=true;AudioListener.volume=0;
            var t=typeof(MayhemMarinaChecks);const BindingFlags f=BindingFlags.Static|BindingFlags.NonPublic;
            float limit=Time.realtimeSinceStartup+45;HomeSession home;
            while(!(home=Object.FindFirstObjectByType<HomeSession>())&&Time.realtimeSinceStartup<limit)yield return null;
            home.RandomLobby();limit=Time.realtimeSinceStartup+45;IslandLobby lobby;
            while(!(lobby=Object.FindFirstObjectByType<IslandLobby>())&&Time.realtimeSinceStartup<limit)yield return null;
            yield return Wait(3);
            try
            {
                t.GetField("captureOrigin",f).SetValue(null,lobby.transform.position);
                if(Services.Session?.SurvivorBots!=null){Services.Session.SurvivorBots.AutoFill=false;Services.Session.SurvivorBots.Running=false;Services.Session.SurvivorBots.Clear();}
                DeadlyJenga.World.Maps.MarinaPracticePool.BuildFor(lobby.gameObject);
                foreach(var r in Services.Session.Player.gameObject.GetComponentsInChildren<Renderer>(true))r.enabled=false;
                lobby.enabled=false;
                t.GetMethod("StagePeople",f).Invoke(null,null);
                // No match has been played here, so stand demo figures on the Hall of Fame pistons (statue size).
                int fame=0;
                foreach(var piston in Object.FindObjectsByType<DeadlyJenga.CandyArt.Marina.MarinaFamePiston>(FindObjectsSortMode.None).OrderBy(p=>p.name))
                {
                    var look=new AvatarAppearance{skin=fame==0?AvatarSkin.CrystalKnight:fame==1?AvatarSkin.NeonRobot:AvatarSkin.Astronaut};
                    var figure=Mannequins.Create(piston.transform,piston.transform.TransformPoint(0,.16f,0),piston.transform.rotation,look,fame,MannequinPose.Display);
                    if(figure)figure.transform.localScale*=1.75f;
                    fame++;
                }
            }
            catch(Exception e){failed=true;Debug.LogException(e);}
            yield return Wait(3);
            var capture=t.GetMethod("Capture",f);
            void Shot(Vector3 eye,Vector3 target,float fov,string name){try{capture.Invoke(null,new object[]{eye,target,fov,name});}catch(Exception e){failed=true;Debug.LogException(e);}}
            Shot(new Vector3(-20,8.8f,-22),new Vector3(0,2.7f,5),64,"Marina-Hero.png");yield return null;
            Shot(new Vector3(-24,13.5f,-27),new Vector3(0,2.8f,5),63,"Marina-Overview.png");yield return null;
            Shot(new Vector3(-8,3.25f,-12),new Vector3(-1.5f,3.4f,9.8f),74,"Marina-Arrival.png");yield return null;
            Shot(new Vector3(6,3.1f,1),new Vector3(13.2f,2.8f,7),65,"Marina-Lounge.png");yield return null;
            Shot(new Vector3(-17,3.2f,1),new Vector3(-13,3.4f,8),60,"Marina-Wardrobe.png");yield return null;
            Shot(new Vector3(-18,5.9f,12.5f),new Vector3(-10,4.9f,18),65,"Marina-Terrace.png");yield return null;
            Shot(new Vector3(-2.7f,3.7f,0.1f),new Vector3(0,2.1f,2.75f),52,"Marina-Plinth.png");yield return null;
            Shot(new Vector3(3.1f,2.55f,0.9f),new Vector3(0,2.1f,2.75f),50,"Marina-PlinthSide.png");yield return null;
            Shot(new Vector3(0,2.6f,-10.5f),new Vector3(0,2.6f,-2.8f),58,"Marina-FamePistons.png");yield return null;
            Shot(new Vector3(5.5f,3.2f,-7.5f),new Vector3(0,2.2f,-2.8f),62,"Marina-FamePistonsSide.png");yield return null;
            Shot(new Vector3(0,2.7f,-10.5f),new Vector3(0,5.1f,-2.45f),62,"Marina-LastMatchFront.png");yield return null;
            Shot(new Vector3(1.2f,2.9f,-2.15f),new Vector3(5.6f,2.4f,1.35f),64,"Marina-StyleTablet.png");yield return null;
            Shot(new Vector3(0,3.0f,4.2f),new Vector3(0,5.4f,-2.45f),62,"Marina-LastMatchBack.png");yield return null;
            Shot(new Vector3(-10.4f,4.3f,0.4f),new Vector3(-15f,2.1f,4.6f),56,"Marina-Crates.png");yield return null;
            Shot(new Vector3(-19.6f,2.9f,8.4f),new Vector3(-14.5f,2.0f,4.2f),56,"Marina-CratesBack.png");yield return null;
            Shot(new Vector3(-1.5f,3.6f,9.5f),new Vector3(10f,3.0f,4.1f),66,"Marina-ShopFromLobby.png");yield return null;
            Shot(new Vector3(-4f,10f,-18f),new Vector3(9f,3.5f,1f),55,"Marina-EastGlass.png");yield return null;
            Shot(new Vector3(1.4f,9.98f,-7.4f),new Vector3(14.2f,3.68f,4.1f),52,"Marina-GiftShop.png");yield return null;
            Shot(new Vector3(5.1f,3.48f,4.1f),new Vector3(14.2f,4.38f,4.1f),66,"Marina-GiftShopFront.png");yield return null;
            Shot(new Vector3(4.7f,4.08f,12.2f),new Vector3(14.2f,3.88f,3.1f),58,"Marina-GiftShopSide.png");yield return null;
            Shot(new Vector3(10.2f,3.58f,4.1f),new Vector3(18.7f,3.08f,4.1f),78,"Marina-GiftShopInside.png");yield return null;
            Shot(new Vector3(10.9f,3.68f,3.2f),new Vector3(15.5f,2.88f,9.9f),72,"Marina-GiftShopPlush.png");yield return null;
            Shot(new Vector3(10.9f,3.68f,5f),new Vector3(17.5f,2.78f,0.6f),72,"Marina-GiftShopCounter.png");yield return null;
            Shot(new Vector3(16.5f,3.48f,4.1f),new Vector3(9.9f,3.08f,4.1f),76,"Marina-GiftShopBack.png");yield return null;
            Shot(new Vector3(15.5f,3.38f,2f),new Vector3(12.9f,2.78f,-1.2f),64,"Marina-GiftShopRack.png");yield return null;
            Shot(new Vector3(13.1f,3.28f,4.1f),new Vector3(17.1f,2.88f,4.1f),70,"Marina-GiftShopMannequins.png");yield return null;
            Shot(new Vector3(15.2f,3.68f,5f),new Vector3(15.2f,2.98f,10f),70,"Marina-GiftShopSuits.png");yield return null;
            Shot(new Vector3(12.3f,4.28f,3f),new Vector3(15.2f,3.08f,10f),68,"Marina-GiftShopSuitsWide.png");
            Shot(new Vector3(23.4f,7.98f,19.1f),new Vector3(15.2f,2.88f,11.2f),58,"Marina-GiftShopSuitsOutside.png");yield return null;
            try{foreach(var r in Services.Session.Player.gameObject.GetComponentsInChildren<Renderer>(true))r.enabled=true;Export(lobby.transform.position,Services.Session.Player.gameObject);}catch(Exception e){failed=true;Debug.LogException(e);}
            EditorApplication.ExitPlaymode();
        }
        static string F(float v)=>v.ToString("0.#####",CultureInfo.InvariantCulture);
        static void Export(Vector3 origin,GameObject player)
        {
            vbaseUV=1;string dir=System.IO.Path.GetFullPath("Logs/Validation/MarinaExport");Directory.CreateDirectory(dir);
            var obj=new StringBuilder();var mtl=new StringBuilder();var log=new StringBuilder();
            obj.Append("mtllib map.mtl\n");
            var mats=new Dictionary<string,string>();var texFiles=new Dictionary<Texture,string>();int vbase=1,tris=0,count=0;
            foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if(!r.enabled||!r.gameObject.activeInHierarchy)continue;
                if(player&&r.transform.IsChildOf(player.transform))continue;
                if(r.gameObject.layer==Layers.Survivor)continue;
                var mf=r.GetComponent<MeshFilter>();if(!mf||!mf.sharedMesh)continue;var mesh=mf.sharedMesh;
                if(Vector3.Distance(r.bounds.center,origin)>220){log.Append("SKIP far "+PathOf(r.transform)+"\n");continue;}
                var shared=r.sharedMaterials;if(shared.Any(m=>m&&m.shader&&m.shader.name.Contains("TextMeshPro")))continue;
                var verts=mesh.vertices;var normals=mesh.normals;var uvs=mesh.uv;var m4=r.localToWorldMatrix;
                obj.Append("o ").Append(r.name.Replace(' ','_')).Append('_').Append(count++).Append('\n');
                foreach(var v in verts){var w=m4.MultiplyPoint3x4(v)-origin;obj.Append("v ").Append(F(-w.x)).Append(' ').Append(F(w.y)).Append(' ').Append(F(w.z)).Append('\n');}
                bool hasN=normals!=null&&normals.Length==verts.Length;bool hasUV=uvs!=null&&uvs.Length==verts.Length;
                if(hasN)foreach(var n in normals){var w=m4.MultiplyVector(n).normalized;obj.Append("vn ").Append(F(-w.x)).Append(' ').Append(F(w.y)).Append(' ').Append(F(w.z)).Append('\n');}
                for(int s=0;s<mesh.subMeshCount;s++)
                {
                    var mat=shared.Length>0?shared[Mathf.Min(s,shared.Length-1)]:null;
                    Vector4 st=mat&&mat.HasProperty("_BaseMap")?mat.GetVector("_BaseMap_ST"):new Vector4(1,1,0,0);
                    if(hasUV)foreach(var uv in uvs)obj.Append("vt ").Append(F(uv.x*st.x+st.z)).Append(' ').Append(F(uv.y*st.y+st.w)).Append('\n');
                    string name=MatName(mat,mats,mtl,texFiles,dir);
                    obj.Append("usemtl ").Append(name).Append('\n');
                    var idx=mesh.GetTriangles(s);tris+=idx.Length/3;
                    int tbase=hasUV?vbaseUV:0;
                    for(int i=0;i<idx.Length;i+=3)
                    {
                        obj.Append('f');
                        foreach(int k in new[]{idx[i],idx[i+2],idx[i+1]})
                        {
                            int vi=vbase+k;obj.Append(' ').Append(vi).Append('/');
                            if(hasUV)obj.Append(tbase+k);
                            if(hasN)obj.Append('/').Append(vi);
                        }
                        obj.Append('\n');
                    }
                    if(hasUV)vbaseUV+=uvs.Length;
                }
                vbase+=verts.Length;
                log.Append($"MESH {PathOf(r.transform)} tris={mesh.triangles.Length/3} shader={(shared.Length>0&&shared[0]?shared[0].shader.name:"none")}\n");
            }
            File.WriteAllText(System.IO.Path.Combine(dir,"map.obj"),obj.ToString());
            File.WriteAllText(System.IO.Path.Combine(dir,"map.mtl"),mtl.ToString());
            log.Insert(0,$"EXPORT objects={count} tris={tris} materials={mats.Count} textures={texFiles.Count}\n");
            File.WriteAllText(System.IO.Path.Combine(dir,"export-log.txt"),log.ToString());
            vbaseUV=1;
        }
        static int vbaseUV=1;
        static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
        static string MatName(Material mat,Dictionary<string,string> mats,StringBuilder mtl,Dictionary<Texture,string> texFiles,string dir)
        {
            Color c=Color.gray;Texture tex=null;string shader=mat&&mat.shader?mat.shader.name:"none";
            if(mat)
            {
                if(mat.HasProperty("_BaseColor"))c=mat.GetColor("_BaseColor");else if(mat.HasProperty("_Color"))c=mat.GetColor("_Color");
                if(mat.HasProperty("_BaseMap"))tex=mat.GetTexture("_BaseMap");else if(mat.HasProperty("_MainTex"))tex=mat.GetTexture("_MainTex");
                if(shader.IndexOf("Water",StringComparison.OrdinalIgnoreCase)>=0){c=new Color(.18f,.78f,.8f,.85f);tex=null;}
            }
            string texFile=null;
            if(tex is Texture2D t2&&t2.width>4){if(!texFiles.TryGetValue(tex,out texFile)){texFile="tex_"+texFiles.Count+".png";SaveTexture(t2,System.IO.Path.Combine(dir,texFile));texFiles[tex]=texFile;}}
            string key=(mat?mat.GetInstanceID().ToString():"none")+"|"+c+"|"+texFile;
            if(mats.TryGetValue(key,out var name))return name;
            name="m"+mats.Count+"_"+(mat?mat.name:"none").Replace(' ','_').Replace("(Instance)","");mats[key]=name;
            var lin=c.linear;lin.a=c.a;mtl.Append("newmtl ").Append(name).Append('\n');
            mtl.Append("Kd ").Append(F(lin.r)).Append(' ').Append(F(lin.g)).Append(' ').Append(F(lin.b)).Append('\n');
            if(lin.a<.99f)mtl.Append("d ").Append(F(lin.a)).Append('\n');
            if(texFile!=null)mtl.Append("map_Kd ").Append(texFile).Append('\n');
            return name;
        }
        static void SaveTexture(Texture2D t,string path)
        {
            int w=Mathf.Min(t.width,1024),h=Mathf.Max(1,Mathf.RoundToInt((float)t.height*w/t.width));
            var rt=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var prev=RenderTexture.active;
            Graphics.Blit(t,rt);RenderTexture.active=rt;var img=new Texture2D(w,h,TextureFormat.RGBA32,false);img.ReadPixels(new Rect(0,0,w,h),0,0);img.Apply();
            File.WriteAllBytes(path,img.EncodeToPNG());RenderTexture.active=prev;RenderTexture.ReleaseTemporary(rt);Object.Destroy(img);
        }
    }
}

