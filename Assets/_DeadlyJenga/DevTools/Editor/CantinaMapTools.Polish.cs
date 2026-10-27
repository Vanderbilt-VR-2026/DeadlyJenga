using System;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static partial class CantinaMapTools
    {
        [MenuItem("Tools/Deadly Jenga/Maps/Polish and Capture Authored Cantina")]
        public static void PolishAndCapture()
        {
            source=Path.GetDirectoryName(ProjectPath.FolderOf("cantina-v2.meshdata.json").TrimEnd('/')).Replace('\\','/');
            string assetPath=source+"/Authored/Cantina V2 Visual.prefab";output=Path.GetDirectoryName(assetPath).Replace('\\','/');
            var root=PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                var enclosure=root.transform.Find("Inferred room enclosure");
                var plaster=root.GetComponentsInChildren<Renderer>().Select(r=>r.sharedMaterial).First(m=>m&&m.name.StartsWith("plaster"));
                if(!enclosure.Find("North opaque backdrop"))
                {
                    Shape(enclosure,"North opaque backdrop",new Vector3(0,2.55f,10.34f),new Vector3(28,5.1f,.3f),plaster);
                    foreach(float x in new[]{-13.67f,13.67f})Shape(enclosure,"Upper side closure",new Vector3(x,4.95f,0),new Vector3(.66f,.4f,21),plaster);
                    Shape(enclosure,"Upper south closure",new Vector3(0,4.95f,-10.25f),new Vector3(28,.4f,.5f),plaster);
                }
                var inserts=root.transform.Find("Finished tank interiors");
                foreach(Transform item in inserts)
                {
                    var p=item.localPosition;bool far=p.z>4.25f;
                    // Apply once, using the exact source cylinder centers rather than inferred plinth centers.
                    if(p.x<11.4f){p.x+=.294f;p.z+=far?-.091f:.072f;item.localPosition=p;}
                    if(item.name=="Contained cyan liquid")item.localPosition=new Vector3(11.594f,1.98f,far?5.359f:3.172f);
                    if(item.name=="Liquid meniscus")item.localPosition=new Vector3(11.594f,3.14f,far?5.359f:3.172f);
                }
                string glassPath=output+"/Materials/Clear tank shell.mat";var glass=AssetDatabase.LoadAssetAtPath<Material>(glassPath);
                if(!glass)
                {
                    glass=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Clear tank shell",renderQueue=3000,enableInstancing=true};
                    glass.SetColor("_BaseColor",new Color(.6f,.92f,1,.12f));glass.SetFloat("_Surface",1);glass.SetFloat("_Smoothness",.88f);
                    glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);glass.SetFloat("_ZWrite",0);
                    glass.SetOverrideTag("RenderType","Transparent");glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    AssetDatabase.CreateAsset(glass,glassPath);
                }
                foreach(float z in new[]{3.172f,5.359f})if(!inserts.Cast<Transform>().Any(t=>t.name=="Clear tank shell"&&Mathf.Abs(t.localPosition.z-z)<.01f))
                    Shape(inserts,"Clear tank shell",new Vector3(11.594f,2.056f,z),new Vector3(1.72f,1.258f,1.72f),glass,PrimitiveType.Cylinder);
                foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{output+"/Materials"}))
                {
                    var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    if(m.name.StartsWith("plaster")||m.name.Contains("floor"))
                    {m.SetFloat("_SpecularHighlights",0);m.SetFloat("_Smoothness",.08f);m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");EditorUtility.SetDirty(m);}
                    // A small material fill keeps the unbaked mobile interior readable, without a global post effect.
                    if(m.HasProperty("_BaseColor")&&m.GetFloat("_Surface")==0&&!m.name.Contains("Cyan")&&!m.name.Contains("bubble")&&!m.name.Contains("practical"))
                    {m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",m.GetColor("_BaseColor")*.12f);EditorUtility.SetDirty(m);}
                    if(m.name.Contains("floor")){m.SetTextureScale("_BaseMap",new Vector2(14,10.5f));m.SetTextureScale("_BumpMap",new Vector2(14,10.5f));}
                }
                if(!root.transform.Find("Interior fill pools"))
                {
                    var fill=Group("Interior fill pools",root.transform);
                    foreach(float x in new[]{-4f,4f})foreach(float z in new[]{-3f,3f})
                    {var l=Group("Broad warm ceiling bounce",fill.transform).AddComponent<Light>();l.transform.localPosition=new Vector3(x,3.8f,z);l.type=LightType.Point;l.range=10;l.intensity=9;l.color=new Color(1,.84f,.62f);l.shadows=LightShadows.None;}
                    root.GetComponent<DeadlyJenga.CandyArt.CantinaAtmosphere>().practicalLights=root.GetComponentsInChildren<Light>();
                }
                if(!root.transform.Find("Half-scale light calibration"))
                {
                    foreach(var light in root.GetComponentsInChildren<Light>()){light.intensity*=.25f;light.range*=.5f;}
                    Group("Half-scale light calibration",root.transform);
                }
                PrefabUtility.SaveAsPrefabAsset(root,assetPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            string mapPath=ProjectPath.Find("Mode_cantina.prefab");root=PrefabUtility.LoadPrefabContents(mapPath);
            try
            {
                root.transform.localScale=Vector3.one*.5f;
                foreach(var capsule in root.GetComponentsInChildren<CapsuleCollider>())
                {var p=capsule.transform.localPosition;p.x=11.594f;p.z=p.z>4.25f?5.359f:3.172f;capsule.transform.localPosition=p;}
                PrefabUtility.SaveAsPrefabAsset(root,mapPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
            var active=SceneManager.GetActiveScene();var world=DeadlyJenga.Core.Services.World;
            var scene=EditorSceneManager.OpenScene(ProjectPath.Find("Cantina V2 Preview.unity"),OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);
            var suns=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.type==LightType.Directional).Select(l=>(light:l,enabled:l.enabled)).ToArray();
            try
            {
                foreach(var sun in suns)sun.light.enabled=false;
                var map=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CantinaMap>()).Single();
                map.transform.localScale=Vector3.one*.5f;
                var art=map.transform.Find("Visual");var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).Single();
                camera.transform.position=map.transform.TransformPoint(new Vector3(2.8f,1.65f,-6.5f));camera.transform.LookAt(map.transform.TransformPoint(new Vector3(0,1.9f,9)));
                EditorSceneManager.SaveScene(scene);
                checks.Clear();Physics.SyncTransforms();Validate(map,art.gameObject);
                var probe=new SphericalHarmonicsL2();probe.AddAmbientLight(new Color(.48f,.42f,.33f));RenderSettings.ambientProbe=probe;
                foreach(var t in art.GetComponentsInChildren<Transform>())t.gameObject.layer=31;camera.cullingMask=1<<31;
                var reflect=art.GetComponentInChildren<ReflectionProbe>();
                if(!reflect){reflect=new GameObject("Baked room reflection").AddComponent<ReflectionProbe>();reflect.transform.SetParent(art,false);}
                reflect.transform.localPosition=new Vector3(0,2,0);reflect.size=new Vector3(27,5,20);reflect.boxProjection=true;reflect.resolution=128;reflect.cullingMask=1<<31;
                string reflectionPath=output+"/Cantina Reflection.exr";
                if(!AssetDatabase.LoadAssetAtPath<Cubemap>(reflectionPath)&&Lightmapping.BakeReflectionProbe(reflect,reflectionPath))
                {
                    AssetDatabase.ImportAsset(reflectionPath);var texture=AssetDatabase.LoadAssetAtPath<Cubemap>(reflectionPath);
                    reflect.mode=ReflectionProbeMode.Custom;reflect.customBakedTexture=texture;
                    // Save the probe independently of the review-only layer assignment.
                    var authored=PrefabUtility.LoadPrefabContents(assetPath);
                    try
                    {
                        var old=authored.transform.Find("Baked room reflection");if(old)Object.DestroyImmediate(old.gameObject);
                        var go=Object.Instantiate(reflect.gameObject,authored.transform);go.name="Baked room reflection";go.GetComponent<ReflectionProbe>().cullingMask=-1;
                        PrefabUtility.SaveAsPrefabAsset(authored,assetPath);
                    }
                    finally{PrefabUtility.UnloadPrefabContents(authored);}
                }
                Require(AssetDatabase.LoadAssetAtPath<Cubemap>(reflectionPath),"Room reflection capture is present");
                reflect.mode=ReflectionProbeMode.Custom;reflect.customBakedTexture=AssetDatabase.LoadAssetAtPath<Cubemap>(reflectionPath);
                checks.Add("PASS Aligned tank inserts and simple clear shells, matte masonry, tiled floor, warm fill and baked room reflection. Runtime camera capture is provided by the separate Cantina Runtime check.");
                checks.Add("OUTPUT "+mapPath);checks.Add("PREVIEW "+ProjectPath.Find("Cantina V2 Preview.unity"));
                checks.Add("Editor captures. No indirect GI bake, headset or two-device test.");
                File.WriteAllLines(ProjectPath.Report("Cantina-authoring.txt"),checks);
            }
            finally
            {
                foreach(var sun in suns)if(sun.light)sun.light.enabled=sun.enabled;
                EditorSceneManager.CloseScene(scene,true);if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);DeadlyJenga.Core.Services.World=world;
            }
        }
    }
}
