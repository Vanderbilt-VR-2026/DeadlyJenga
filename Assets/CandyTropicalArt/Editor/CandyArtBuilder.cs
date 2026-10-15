using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using DeadlyJenga.CastlePrototype;
using Object = UnityEngine.Object;

namespace DeadlyJenga.CandyArt.Editor
{
    public static class CandyArtBuilder
    {
        public const string Root="Assets/CandyTropicalArt/";
        const string ProfilePath=Root+"Candy Tropical.asset";
        const string AppearancePath=Root+"Candy Appearance.asset";
        const string BlockPath=Root+"Prefabs/Candy Physics Block.prefab";
        public static CandyArtProfile Profile=>AssetDatabase.LoadAssetAtPath<CandyArtProfile>(ProfilePath);

        static T Keep<T>(string path,Func<T> create) where T:Object
        {var item=AssetDatabase.LoadAssetAtPath<T>(path);if(!item){item=create();AssetDatabase.CreateAsset(item,path);}return item;}
        static Material Lit(string name,Color color,float smoothness)
        {
            return Keep(Root+"Materials/"+name+".mat",()=>
            {
                var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};
                mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",smoothness);mat.SetFloat("_Metallic",0);
                return mat;
            });
        }
        [MenuItem("Tools/Candy Tropical/1 Build Reusable Assets")]
        public static void Build()
        {
            Directory.CreateDirectory(Root+"Materials");Directory.CreateDirectory(Root+"Prefabs");
            AssetDatabase.Refresh();
            var p=Keep(ProfilePath,()=>ScriptableObject.CreateInstance<CandyArtProfile>());
            if(!p.red)p.red=Lit("Cherry Red",new Color(.94f,.115f,.17f),.68f);
            if(!p.green)p.green=Lit("Apple Green",new Color(.12f,.78f,.35f),.65f);
            if(!p.purple)p.purple=Lit("Lavender",new Color(.64f,.37f,.91f),.7f);
            if(!p.sand)p.sand=Lit("Warm Sand",new Color(.94f,.85f,.63f),.12f);
            if(!p.water)p.water=Keep(Root+"Materials/Turquoise Ocean.mat",()=>new Material(Shader.Find("Candy Tropical/Ocean")));
            if(!p.sky)p.sky=Keep(Root+"Materials/Tropical Sky.mat",()=>new Material(Shader.Find("Candy Tropical/Sky")));
            EditorUtility.SetDirty(p);
            string visualPath=Root+"Prefabs/Candy Crate Visual.prefab";
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(visualPath))
            {
                var visual=new GameObject("Candy crate artwork",typeof(MeshFilter),typeof(MeshRenderer));
                visual.GetComponent<MeshRenderer>().sharedMaterial=p.red;
                visual.AddComponent<CandyCrateVisual>().profile=p;
                PrefabUtility.SaveAsPrefabAsset(visual,visualPath);Object.DestroyImmediate(visual);
            }
            var appearance=Keep(AppearancePath,()=>ScriptableObject.CreateInstance<CastleBlockAppearance>());
            appearance.visualPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);appearance.materialOverride=null;EditorUtility.SetDirty(appearance);
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(BlockPath))
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CastleBlock.prefab");
                if(!source)throw new InvalidOperationException("The local CastleBlock physics prefab is required. Build assets in the prototype project first.");
                var copy=Object.Instantiate(source);copy.name="Candy Physics Block";
                var block=copy.GetComponent<CastleBlock>();block.appearance=appearance;
                foreach(Transform child in block.visualRoot)Object.DestroyImmediate(child.gameObject);
                PrefabUtility.SaveAsPrefabAsset(copy,BlockPath);Object.DestroyImmediate(copy);
            }
            Keep(Root+"Sandbar Mesh.asset",CreateSandbar);
            Keep(Root+"Ocean Mesh.asset",CreateOcean);
            string environmentPath=Root+"Prefabs/Candy Environment.prefab";
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(environmentPath))
            {
                var environment=new GameObject("Candy Tropical Environment");
                var component=environment.AddComponent<CandyEnvironment>();component.profile=p;
                component.waterRenderer=Surface(environment.transform,"Ocean",AssetDatabase.LoadAssetAtPath<Mesh>(Root+"Ocean Mesh.asset"),p.water);
                component.waterRenderer.gameObject.AddComponent<CandyOceanSurface>().profile=p;
                component.sandRenderer=Surface(environment.transform,"Submerged Sandbar",AssetDatabase.LoadAssetAtPath<Mesh>(Root+"Sandbar Mesh.asset"),p.sand);
                component.sandRenderer.gameObject.AddComponent<CandySandbar>().profile=p;
                PrefabUtility.SaveAsPrefabAsset(environment,environmentPath);Object.DestroyImmediate(environment);
            }
            AssetDatabase.SaveAssets();
        }
        static Renderer Surface(Transform parent,string name,Mesh mesh,Material material)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;return renderer;
        }
        static Mesh CreateOcean()
        {
            var mesh=new Mesh{name="Unit horizontal ocean"};
            mesh.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(.5f,0,-.5f)};
            mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh CreateSandbar()
        {
            const int count=96;var points=new Vector3[count+1];var uv=new Vector2[count+1];var triangles=new int[count*3];
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count;float r=.5f*(1+.035f*Mathf.Sin(a*5)+.025f*Mathf.Sin(a*9));
                points[i+1]=new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);uv[i+1]=new Vector2(points[i+1].x+.5f,points[i+1].z+.5f);
                triangles[i*3]=0;triangles[i*3+1]=(i+1)%count+1;triangles[i*3+2]=i+1;
            }
            var mesh=new Mesh{name="Soft sandbar outline",vertices=points,uv=uv,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        [MenuItem("Tools/Candy Tropical/2 Apply To Selected Castle %#F12")]
        public static void Apply()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Apply in Edit Mode; tune the profile live afterward.");
            var generator=Selection.activeGameObject?Selection.activeGameObject.GetComponentInParent<CastleGenerator>():null;
            if(!generator)
            {
                var candidates=Object.FindObjectsByType<CastleGenerator>(FindObjectsSortMode.None);
                if(candidates.Length!=1)throw new InvalidOperationException("Select the CastleGenerator to style.");generator=candidates[0];
            }
            Build();
            var scene=generator.gameObject.scene;
            var baseline=scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<CandyArtBaseline>(true)).FirstOrDefault(b=>b.generator==generator);
            if(!baseline)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/Candy Environment.prefab"),scene);
                go.transform.position=generator.transform.position;
                baseline=go.AddComponent<CandyArtBaseline>();baseline.generator=generator;
                baseline.blockPrefab=generator.blockPrefab;baseline.red=generator.wallMaterial;baseline.green=generator.cornerMaterial;baseline.purple=generator.keepMaterial;
                baseline.sky=RenderSettings.skybox;baseline.ambientMode=RenderSettings.ambientMode;
                baseline.ambientSky=RenderSettings.ambientSkyColor;baseline.ambientEquator=RenderSettings.ambientEquatorColor;baseline.ambientGround=RenderSettings.ambientGroundColor;baseline.ambientIntensity=RenderSettings.ambientIntensity;
                baseline.sun=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Light>(true)).FirstOrDefault(l=>l.type==LightType.Directional);
                if(baseline.sun){baseline.sunColor=baseline.sun.color;baseline.sunIntensity=baseline.sun.intensity;baseline.sunRotation=baseline.sun.transform.rotation;}
                foreach(var block in generator.GetComponentsInChildren<CastleBlock>(true))
                    baseline.blocks.Add(new CandyArtBaseline.BlockState{block=block,appearance=block.appearance,material=block.sectionMaterial});
                var ocean=scene.GetRootGameObjects().FirstOrDefault(root=>root.name=="Ocean (visual only)");
                foreach(var renderer in (ocean?ocean.GetComponentsInChildren<Renderer>(true):Array.Empty<Renderer>()).Concat(generator.foundation?generator.foundation.GetComponentsInChildren<Renderer>(true):Array.Empty<Renderer>()))
                    baseline.renderers.Add(new CandyArtBaseline.RendererState{renderer=renderer,enabled=renderer.enabled});
            }
            var environment=baseline.GetComponent<CandyEnvironment>();environment.profile=Profile;environment.foundation=generator.foundation;environment.sun=baseline.sun;environment.applyLighting=true;
            var p=Profile;var appearance=AssetDatabase.LoadAssetAtPath<CastleBlockAppearance>(AppearancePath);
            generator.blockPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(BlockPath);generator.wallMaterial=p.red;generator.cornerMaterial=p.green;generator.keepMaterial=p.purple;
            foreach(var block in generator.GetComponentsInChildren<CastleBlock>(true))
            {
                block.appearance=appearance;
                block.sectionMaterial=block.name.StartsWith("Corner")?p.green:block.name.StartsWith("Keep")?p.purple:
                    block.sectionMaterial==baseline.green?p.green:block.sectionMaterial==baseline.purple?p.purple:p.red;
                block.RefreshAppearance();EditorUtility.SetDirty(block);
            }
            foreach(var state in baseline.renderers)if(state.renderer)state.renderer.enabled=false;
            environment.Refresh();EditorUtility.SetDirty(baseline);EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("Candy art applied. Geometry and physics were not rebuilt. Restore is available under Tools/Candy Tropical.");
        }
        [MenuItem("Tools/Candy Tropical/3 Restore Previous Appearance")]
        public static void Restore()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Restore in Edit Mode.");
            var baselines=Object.FindObjectsByType<CandyArtBaseline>(FindObjectsSortMode.None);
            foreach(var b in baselines)
            {
                var scene=b.gameObject.scene;var g=b.generator;
                if(g)
                {
                    g.blockPrefab=b.blockPrefab;g.wallMaterial=b.red;g.cornerMaterial=b.green;g.keepMaterial=b.purple;
                    foreach(var block in g.GetComponentsInChildren<CastleBlock>(true))
                    {
                        var saved=b.blocks.FirstOrDefault(s=>s.block==block);
                        block.appearance=saved!=null?saved.appearance:b.blockPrefab.GetComponent<CastleBlock>().appearance;
                        block.sectionMaterial=saved!=null?saved.material:block.name.StartsWith("Corner")?b.green:block.name.StartsWith("Keep")?b.purple:b.red;
                        block.RefreshAppearance();EditorUtility.SetDirty(block);
                    }
                    EditorUtility.SetDirty(g);
                }
                foreach(var state in b.renderers)if(state.renderer)state.renderer.enabled=state.enabled;
                b.GetComponent<CandyEnvironment>().enabled=false;
                RenderSettings.skybox=b.sky;RenderSettings.ambientMode=b.ambientMode;RenderSettings.ambientSkyColor=b.ambientSky;RenderSettings.ambientEquatorColor=b.ambientEquator;RenderSettings.ambientGroundColor=b.ambientGround;RenderSettings.ambientIntensity=b.ambientIntensity;
                if(b.sun){b.sun.color=b.sunColor;b.sun.intensity=b.sunIntensity;b.sun.transform.rotation=b.sunRotation;}
                Object.DestroyImmediate(b.gameObject);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
        }
        [MenuItem("Tools/Candy Tropical/4 Export Unity Package")]
        public static void Export()
        {
            Build();Directory.CreateDirectory("ArtDirection/CandyTropical/Exports");
            var paths=AssetDatabase.FindAssets("",new[]{Root.TrimEnd('/')}).Select(AssetDatabase.GUIDToAssetPath).Where(path=>!AssetDatabase.IsValidFolder(path)&&path!=Root+"Editor/CandyPolishUpgrade.cs").ToArray();
            AssetDatabase.ExportPackage(paths,"ArtDirection/CandyTropical/Exports/CandyTropicalArt.unitypackage",ExportPackageOptions.IncludeDependencies);
            Debug.Log("Candy art Unity package exported with dependencies.");
        }
    }
}
