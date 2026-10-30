using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyArt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Only an explicit menu action or request builds assets. Loading Unity does nothing.
    [InitializeOnLoad] public static partial class CantinaMapTools
    {
        [Serializable] public sealed class Data { public int version; public MaterialData[] materials; public Part[] parts; }
        [Serializable] public sealed class MaterialData { public string key,family; public float r,g,b; }
        [Serializable] public sealed class Part { public string name,material; public float[] positions; public int[] triangles; }
        sealed class Bucket { public string name,material; public List<Vector3> vertices=new List<Vector3>(); }
        static string output,source;
        static readonly List<string> checks=new List<string>();
        static CantinaMapTools()=>EditorApplication.update+=()=>
        {
            string request=ProjectPath.Report("Cantina.build.request");
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            string command=File.ReadAllText(request).Trim();File.Delete(request);
            try{if(command=="polish")PolishAndCapture();else Build();}catch(Exception e){File.WriteAllText(ProjectPath.Report("Cantina-authoring.txt"),"FAIL "+e);Debug.LogException(e);}
        };
        [MenuItem("Tools/Deadly Jenga/Maps/Build Cantina V2 Map")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before building a map.");
            if(Resources.Load<GameObject>("ModeMaps/Mode_cantina"))throw new InvalidOperationException("Cantina already exists. Use Polish and Capture instead of rebuilding it.");
            source=Path.GetDirectoryName(ProjectPath.FolderOf("cantina-v2.meshdata.json").TrimEnd('/')).Replace('\\','/');
            output=AssetDatabase.GenerateUniqueAssetPath(source+"/Authored");
            Directory.CreateDirectory(output+"/Meshes");Directory.CreateDirectory(output+"/Materials");AssetDatabase.Refresh();
            checks.Clear();var previous=SceneManager.GetActiveScene();var previousWorld=Services.World;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                var data=JsonUtility.FromJson<Data>(AssetDatabase.LoadAssetAtPath<TextAsset>(ProjectPath.Find("cantina-v2.meshdata.json")).text);
                Require(data.version==2&&data.parts.Length>1000,"Native version-2 geometry is present");
                var materials=Materials(data.materials);
                var map=Group("Cantina V2").AddComponent<CantinaMap>();var collision=Group("Collision",map.transform);
                var visual=Group("Visual",map.transform);
                var buckets=new Dictionary<string,Bucket>();
                foreach(var part in data.parts)
                {
                    if(part.name=="Continuous floor"||part.name.StartsWith("Twin tanks")&&part.material.StartsWith("cyanpaint"))continue;
                    string key=part.name+"|"+part.material;
                    if(!buckets.TryGetValue(key,out var bucket)){bucket=new Bucket{name=part.name,material=part.material};buckets.Add(key,bucket);}
                    foreach(int index in part.triangles)
                    {int j=index*3;bucket.vertices.Add(new Vector3(part.positions[j],part.positions[j+1],part.positions[j+2]));}
                }
                var groups=new Dictionary<string,Transform>();int number=0;
                foreach(var b in buckets.Values)
                {
                    if(!groups.TryGetValue(b.name,out var parent)){parent=Group(b.name,visual.transform).transform;groups.Add(b.name,parent);}
                    var go=Group(b.material,parent);var mesh=Mesh(b,number++);
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=materials[b.material];
                    r.receiveShadows=true;GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic|StaticEditorFlags.ContributeGI);
                }
                // Close the roof-off reconstruction into a walkable room. These parts are inferred.
                Shape(visual.transform,"Continuous stone floor",new Vector3(0,-.14f,0),new Vector3(28,.26f,21),materials["floor"]);
                var closure=Group("Inferred room enclosure",visual.transform);
                Shape(closure.transform,"Ceiling",new Vector3(0,5.12f,0),new Vector3(28,.28f,21),materials[data.materials[0].key]);
                // The band and nearby passage sit forward of this closure; neither is blocked by a false wall.
                Shape(closure.transform,"South wall",new Vector3(0,2.4f,-10.25f),new Vector3(28,4.8f,.5f),materials[data.materials[0].key]);
                Shape(visual.transform,"Band back wall",new Vector3(-7,2,-9.9f),new Vector3(5,4,.25f),materials[data.materials[0].key]);
                Shape(visual.transform,"Passage shutter",new Vector3(0,1.5f,-9.8f),new Vector3(2.5f,3,.15f),materials["darkmetal_41473f"]);
                FinishTanks(visual.transform,materials);
                CreateCollision(collision.transform,data.parts);
                var lights=Lighting(visual.transform);
                var atmosphere=visual.AddComponent<CantinaAtmosphere>();atmosphere.practicalLights=lights;
                // The visual prefab owns all materials/lights; World owns every solid collider.
                var visualAsset=PrefabUtility.SaveAsPrefabAsset(visual,output+"/Cantina V2 Visual.prefab");
                Object.DestroyImmediate(visual);visual=(GameObject)PrefabUtility.InstantiatePrefab(visualAsset,map.transform);visual.name="Visual";
                var maps=Path.GetDirectoryName(ProjectPath.FolderOf("CantinaMap.cs").TrimEnd('/')).Replace('\\','/');
                string folder=Folder(Folder(maps,"Resources"),"ModeMaps"),path=folder+"/Mode_cantina.prefab";
                if(AssetDatabase.LoadAssetAtPath<GameObject>(path))throw new InvalidOperationException("Cantina map already exists; edit the existing assets instead of rebuilding over them.");
                PrefabUtility.SaveAsPrefabAsset(map.gameObject,path);AssetDatabase.SaveAssets();Physics.SyncTransforms();
                Validate(map,visual);
                RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.32f,.28f,.22f);
                RenderSettings.ambientEquatorColor=new Color(.23f,.20f,.16f);RenderSettings.ambientGroundColor=new Color(.12f,.115f,.10f);
                RenderSettings.skybox=null;RenderSettings.fog=false;
                var camera=Group("Cantina review camera").AddComponent<Camera>();camera.nearClipPlane=.05f;camera.farClipPlane=80;camera.fieldOfView=66;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.025f,.03f);camera.allowHDR=true;
                var scenes=Folder(Path.GetDirectoryName(ProjectPath.FolderOf("CantinaMapTools.cs").TrimEnd('/')).Replace('\\','/'),"Scenes");
                camera.transform.position=new Vector3(2.8f,1.65f,-6.5f);camera.transform.LookAt(new Vector3(0,2,9));
                EditorSceneManager.SaveScene(scene,scenes+"/Cantina V2 Preview.unity");
                Require(EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"),"Existing disaster setup validator ran");
                checks.Add("OUTPUT "+path);checks.Add("VISUAL "+output+"/Cantina V2 Visual.prefab");
                checks.Add("PREVIEW "+scenes+"/Cantina V2 Preview.unity");
                checks.Add("Editor camera captures, not headset captures. Lighting is realtime/ambient; no GI bake or headset performance claim.");
                File.WriteAllLines(ProjectPath.Report("Cantina-authoring.txt"),checks);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene,true);
                if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
                Services.World=previousWorld;
            }
            PolishAndCapture();
        }
        static Dictionary<string,Material> Materials(MaterialData[] specs)
        {
            foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{source+"/Textures"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=path.Contains("_Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.sRGBTexture=path.Contains("_BaseColor");importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;
                importer.maxTextureSize=512;importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();
            }
            var result=new Dictionary<string,Material>();
            foreach(var spec in specs)
            {
                var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=spec.key};
                material.SetColor("_BaseColor",new Color(spec.r,spec.g,spec.b));
                material.SetTexture("_BaseMap",Texture(spec.family,"BaseColor"));material.SetTexture("_BumpMap",Texture(spec.family,"Normal"));
                material.SetTexture("_MetallicGlossMap",Texture(spec.family,"MetallicSmoothness"));
                material.SetFloat("_BumpScale",spec.family=="plaster"?.28f:.13f);material.SetFloat("_Smoothness",.65f);
                material.EnableKeyword("_NORMALMAP");material.EnableKeyword("_METALLICSPECGLOSSMAP");material.enableInstancing=true;
                Save(material,spec.key);result.Add(spec.key,material);
            }
            var floor=new Material(result[specs[0].key]){name="Dusty stone floor"};floor.SetColor("_BaseColor",new Color(.32f,.30f,.23f));
            floor.SetTexture("_BaseMap",Texture("floor","BaseColor"));floor.SetTexture("_BumpMap",Texture("floor","Normal"));
            floor.SetTexture("_MetallicGlossMap",Texture("floor","MetallicSmoothness"));Save(floor,"Floor");result.Add("floor",floor);
            result.Add("liquid",Glow("Cyan liquid",new Color(.07f,.48f,.56f),new Color(.04f,.38f,.46f),.7f));
            result.Add("bubble",Glow("Tank bubbles",new Color(.45f,.95f,1),new Color(.1f,.5f,.6f),.8f));
            result.Add("lamp",Glow("Warm practical",new Color(1,.8f,.48f),new Color(1,.55f,.2f)*2,.3f));
            return result;
        }
        static Texture Texture(string family,string suffix)=>AssetDatabase.LoadAssetAtPath<Texture2D>(source+"/Textures/"+family+"_"+suffix+".png");
        static Material Glow(string name,Color tint,Color emission,float smooth)
        {var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};m.SetColor("_BaseColor",tint);m.SetFloat("_Smoothness",smooth);m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission);Save(m,name);return m;}
        static void Save(Material m,string name)=>AssetDatabase.CreateAsset(m,output+"/Materials/"+name+".mat");
        static Mesh Mesh(Bucket bucket,int number)
        {
            var v=bucket.vertices;var mesh=new Mesh{name=bucket.name+" "+bucket.material,indexFormat=IndexFormat.UInt32};
            var uv=new Vector2[v.Count];var normals=new Vector3[v.Count];var indices=new int[v.Count];
            var sharing=new Dictionary<Vector3,List<int>>();
            for(int i=0;i<v.Count;i+=3)
            {
                var n=Vector3.Cross(v[i+1]-v[i],v[i+2]-v[i]).normalized;var a=new Vector3(Mathf.Abs(n.x),Mathf.Abs(n.y),Mathf.Abs(n.z));
                for(int k=0;k<3;k++)
                {
                    int j=i+k;var p=v[j];normals[j]=n;indices[j]=j;
                    uv[j]=(a.y>=a.x&&a.y>=a.z?new Vector2(p.x,p.z):a.x>=a.z?new Vector2(p.z,p.y):new Vector2(p.x,p.y))*.5f;
                    var key=new Vector3(Mathf.Round(p.x*10000),Mathf.Round(p.y*10000),Mathf.Round(p.z*10000));
                    if(!sharing.TryGetValue(key,out var at))sharing.Add(key,at=new List<int>());at.Add(j);
                }
            }
            var smooth=(Vector3[])normals.Clone();
            foreach(var at in sharing.Values)foreach(int i in at)
            {var n=Vector3.zero;foreach(int j in at)if(Vector3.Dot(normals[i],normals[j])>.65f)n+=normals[j];smooth[i]=n.normalized;}
            mesh.SetVertices(v);mesh.triangles=indices;mesh.uv=uv;mesh.normals=smooth;mesh.RecalculateTangents();mesh.RecalculateBounds();
            Unwrapping.GenerateSecondaryUVSet(mesh);
            AssetDatabase.CreateAsset(mesh,output+"/Meshes/Part_"+number.ToString("D3")+".asset");return mesh;
        }
        static void FinishTanks(Transform parent,Dictionary<string,Material> materials)
        {
            var tanks=Group("Finished tank interiors",parent);
            foreach(float z in new[]{3.1f,5.45f})
            {
                var liquid=Shape(tanks.transform,"Contained cyan liquid",new Vector3(11.3f,1.88f,z),new Vector3(1.52f,1.16f,1.52f),materials["liquid"],PrimitiveType.Cylinder);
                // A narrow metallic shell cage avoids multiple overlapping transparent cylinders on Quest.
                for(int i=0;i<8;i++)
                {float a=i*Mathf.PI/4;Shape(tanks.transform,"Glass edge mullion",new Vector3(11.3f+Mathf.Cos(a)*.82f,1.94f,z+Mathf.Sin(a)*.82f),new Vector3(.025f,2.35f,.025f),materials["ivory_bbc4ba"]);}
                for(int i=0;i<9;i++)
                {float a=i*2.4f;Shape(tanks.transform,"Suspended bubble",new Vector3(11.3f+Mathf.Cos(a)*.43f,.9f+i*.23f,z+Mathf.Sin(a)*.43f),Vector3.one*(.035f+(i%3)*.015f),materials["bubble"],PrimitiveType.Sphere);}
                Shape(tanks.transform,"Liquid meniscus",new Vector3(11.3f,3.04f,z),new Vector3(1.54f,.015f,1.54f),materials["bubble"],PrimitiveType.Cylinder);
            }
        }
        static Light[] Lighting(Transform parent)
        {
            var group=Group("Warm practical lighting",parent);var lights=new List<Light>();
            void Add(string name,Vector3 p,Color color,float intensity,float range)
            {var l=Group(name,group.transform).AddComponent<Light>();l.transform.localPosition=p;l.type=LightType.Point;l.color=color;l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;lights.Add(l);}
            foreach(float x in new[]{-8f,-4.8f,-1.6f,1.6f,4.8f,8f})Add("Amber door wash",new Vector3(x,3.95f,7.9f),new Color(1,.73f,.41f),2.8f,6);
            foreach(float z in new[]{-3f,2.8f})Add("Bar warm pool",new Vector3(-10.6f,3.25f,z),new Color(1,.67f,.33f),3.2f,7);
            Add("Cool tank spill",new Vector3(9.7f,2.5f,4.3f),new Color(.2f,.85f,1),2.2f,6);
            Add("Band alcove magenta",new Vector3(-7,2.5f,-8.4f),new Color(1,.26f,.52f),2.4f,5);
            Add("Central warm bounce",new Vector3(0,4,0),new Color(1,.86f,.66f),2.5f,13);
            return lights.ToArray();
        }
        static void CreateCollision(Transform parent,Part[] parts)
        {
            Box(parent,"Floor",new Vector3(0,-.14f,0),new Vector3(28,.26f,21));
            Box(parent,"Ceiling",new Vector3(0,5.12f,0),new Vector3(28,.28f,21));
            Box(parent,"West wall",new Vector3(-13.67f,2.4f,0),new Vector3(.66f,4.8f,21));
            Box(parent,"East wall",new Vector3(13.67f,2.4f,0),new Vector3(.66f,4.8f,21));
            Box(parent,"North wall and closed doors",new Vector3(0,2.4f,9.88f),new Vector3(28,4.8f,.3f));
            Box(parent,"South wall",new Vector3(0,2.4f,-10.25f),new Vector3(28,4.8f,.5f));
            Box(parent,"Counter",new Vector3(-11.1f,.72f,.2f),new Vector3(3.6f,1.44f,10.45f));
            Box(parent,"Band raised stage",new Vector3(-7,.17f,-8.9f),new Vector3(6.35f,.34f,2.5f));
            Box(parent,"Passage shutter",new Vector3(0,1.5f,-9.8f),new Vector3(2.5f,3,.2f));
            Box(parent,"Tank plinth",new Vector3(11.3f,.25f,4.27f),new Vector3(3.75f,.5f,5.12f));
            foreach(float z in new[]{3.1f,5.45f})
            {var go=Group("Tank",parent);go.transform.localPosition=new Vector3(11.3f,1.9f,z);go.layer=Layers.Surface;var c=go.AddComponent<CapsuleCollider>();c.radius=.9f;c.height=3.5f;}
            foreach(var part in parts.Where(p=>p.name.StartsWith("Table and three chairs")))
            {
                var bounds=new Bounds(new Vector3(part.positions[0],part.positions[1],part.positions[2]),Vector3.zero);
                for(int i=3;i<part.positions.Length;i+=3)bounds.Encapsulate(new Vector3(part.positions[i],part.positions[i+1],part.positions[i+2]));
                // Only the large furniture pieces collide, never cup handles or tiny chair studs.
                if(bounds.size.x<.3f||bounds.size.z<.2f||bounds.size.y<.1f)continue;
                Box(parent,part.name+" furniture",bounds.center,bounds.size);
            }
            foreach(var part in parts.Where(p=>p.name.StartsWith("Recessed band")||p.name.StartsWith("Side passage")))
            {
                var b=new Bounds(new Vector3(part.positions[0],part.positions[1],part.positions[2]),Vector3.zero);
                for(int i=3;i<part.positions.Length;i+=3)b.Encapsulate(new Vector3(part.positions[i],part.positions[i+1],part.positions[i+2]));
                // Narrow arch piers need collision; a coarse box across the arch would close the entrance.
                if(b.size.y>1.8f&&b.size.x<.8f)Box(parent,"Arch pier",b.center,b.size);
            }
        }
        static void Validate(CantinaMap map,GameObject visual)
        {
            Require(visual.GetComponentsInChildren<Collider>().Length==0,"Presentation visual contains no solid collision");
            Require(map.Blocks.Count==0&&map.PlayableBounds.size.y>2,"Static interior exposes world bounds without fake dynamic blocks");
            int mask=1<<Layers.Surface;var spawn=map.SafeSpawnPoint();
            Require(Physics.Raycast(spawn+Vector3.up*.1f,Vector3.down,.4f,mask),"Spawn has a floor beneath it");
            float scale=Mathf.Abs(map.transform.lossyScale.y);
            Require(!Physics.CheckCapsule(spawn+Vector3.up*.4f*scale,spawn+Vector3.up*1.6f*scale,.32f*scale,mask),"Spawn has full standing body clearance");
            Require(map.IsSheltered(spawn),"Completed roof provides shelter");
            Require(!map.LoadLayout(map.SaveLayout(),1,new Pose(Vector3.zero,Quaternion.identity),Vector3.one),"Layout rejects incompatible block count");
            Require(!map.LoadLayout(map.SaveLayout(),0,new Pose(Vector3.zero,Quaternion.identity),new Vector3(float.NaN,1,1)),"Layout rejects non-finite scale");
            Require(map.GetComponentsInChildren<Collider>().All(c=>c.gameObject.layer==Layers.Surface),"Every solid collider uses the gameplay surface layer");
            Require(visual.GetComponentsInChildren<MeshFilter>().All(f=>f.sharedMesh&&f.sharedMesh.uv.Length==f.sharedMesh.vertexCount),"All rendered meshes have material UVs");
            Require(visual.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m&&m.shader&&m.shader.isSupported)),"All renderers have supported materials");
            Require(visual.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Table and three chairs"))==7,"Seven modular seating groups are preserved");
        }
        static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);checks.Add("PASS "+message);}
        static GameObject Group(string name,Transform parent=null){var go=new GameObject(name);if(parent)go.transform.SetParent(parent,false);return go;}
        static GameObject Shape(Transform parent,string name,Vector3 p,Vector3 size,Material material,PrimitiveType type=PrimitiveType.Cube)
        {var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;return go;}
        static void Box(Transform parent,string name,Vector3 center,Vector3 size)
        {var go=Group(name,parent);go.transform.localPosition=center;go.layer=Layers.Surface;go.AddComponent<BoxCollider>().size=size;}
        static string Folder(string parent,string name){string path=parent+"/"+name;if(!AssetDatabase.IsValidFolder(path))AssetDatabase.CreateFolder(parent,name);return path;}
    }
}
