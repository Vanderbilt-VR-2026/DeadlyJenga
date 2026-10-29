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

namespace DeadlyJenga.DevTools.Editor
{
    public static class PowerupLabTools
    {
        static string artFolder;
        static string Folder(string parent,string name){string path=parent+"/"+name;if(!AssetDatabase.IsValidFolder(path))AssetDatabase.CreateFolder(parent,name);return path;}
        static GameObject Group(string name,Transform parent=null){var go=new GameObject(name);go.transform.SetParent(parent,false);return go;}
        static Material Material(string name,Color color)
        {
            string path=artFolder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.shader=Shader.Find("Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.48f);EditorUtility.SetDirty(m);return m;
        }
        static GameObject Shape(string name,Transform parent,Vector3 p,Vector3 size,Material material,PrimitiveType type=PrimitiveType.Cube)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;
            go.transform.localScale=type==PrimitiveType.Cylinder?new Vector3(size.x,size.y*.5f,size.z):size;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        static GameObject Text(Transform parent,string title,Vector3 p,float size,Material material)
        {
            var go=Group(title,parent);go.transform.localPosition=p;var text=go.AddComponent<TextMesh>();text.text=title;text.anchor=TextAnchor.MiddleCenter;
            text.alignment=TextAlignment.Center;text.fontSize=40;text.characterSize=size;text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.GetComponent<Renderer>().sharedMaterial=material;go.AddComponent<ChristmasTownText>();return go;
        }
        [MenuItem("Tools/Deadly Jenga/Maps/Build Powerup Test Island")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play Mode before building.");
            if(!Application.isBatchMode&&Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))throw new Exception("Save edited scenes first.");
            var saved=EditorSceneManager.GetSceneManagerSetup();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            try
            {
                string visuals=ProjectPath.FolderOf("PowerupOrbVisual.cs").TrimEnd('/');artFolder=visuals;
                var grass=Material("Lab mint grass",new Color(.2f,.65f,.48f));var sand=Material("Lab warm sand",new Color(.95f,.7f,.39f));
                var stone=Material("Lab vanilla tower",new Color(.85f,.77f,.67f));var trim=Material("Lab candy trim",new Color(.62f,.3f,.7f));
                var paint=Material("Lab track paint",Color.white);var ink=Material("Lab dark",new Color(.15f,.09f,.23f));
                var lettering=Material("Powerup labels",Color.white);lettering.shader=Shader.Find("DeadlyJenga/Powerups/WorldLabel");
                var world=Group("Powerup Test Island");var map=world.AddComponent<PowerupTestMap>();var collision=Group("Collision",world.transform);
                var art=Group("Visual");
                var ground=GameObject.CreatePrimitive(PrimitiveType.Cylinder);ground.name="Fixed island ground";ground.transform.SetParent(collision.transform,false);
                ground.transform.localPosition=Vector3.down*.5f;ground.transform.localScale=new Vector3(68,.5f,68);ground.layer=Layers.Surface;
                var mesh=ground.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
                ground.AddComponent<MeshCollider>().sharedMesh=mesh;UnityEngine.Object.DestroyImmediate(ground.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(ground.GetComponent<MeshFilter>());
                Shape("Mint island",art.transform,new Vector3(0,-.52f,0),new Vector3(68,1,68),grass,PrimitiveType.Cylinder);
                Shape("Sand shore",art.transform,new Vector3(0,-1.1f,0),new Vector3(72,.6f,72),sand,PrimitiveType.Cylinder);
                var bodies=new List<Rigidbody>();
                void TowerPart(string name,Vector3 p,Vector3 size,Material material)
                {
                    var go=Group(name,collision.transform);go.transform.localPosition=p;go.layer=Layers.Surface;go.AddComponent<BoxCollider>().size=size;
                    var body=go.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;go.AddComponent<PowerupLabSurface>();bodies.Add(body);
                    Shape(name,art.transform,p,size,material);
                }
                TowerPart("Single climbable tower",new Vector3(0,7.5f,0),new Vector3(4,15,4),stone);
                for(int tier=1;tier<=5;tier++)
                {
                    float y=tier*3;TowerPart("Tower ledge "+tier,new Vector3(0,y,0),new Vector3(7,.35f,7),trim);
                    Text(art.transform,y+" m",new Vector3(0,y-.5f,-2.03f),.12f,lettering);
                }
                map.towerSurfaces=bodies.ToArray();
                for(int mark=0;mark<=30;mark+=5)
                {
                    Shape("Running distance stripe",art.transform,new Vector3(-15+mark,.025f,-18),new Vector3(.12f,.025f,4),paint);
                    Text(art.transform,mark+" m",new Vector3(-15+mark,.04f,-21),.11f,lettering).transform.localRotation=Quaternion.Euler(90,0,0);
                }
                Shape("Testing start circle",art.transform,new Vector3(0,.025f,-12),new Vector3(3,.02f,3),paint,PrimitiveType.Cylinder);
                string visualPath=artFolder+"/Powerup Test Island Visual.prefab";var visual=PrefabUtility.SaveAsPrefabAsset(art,visualPath);
                UnityEngine.Object.DestroyImmediate(art);PrefabUtility.InstantiatePrefab(visual,world.transform);
                string maps=Path.GetDirectoryName(ProjectPath.FolderOf("PowerupTestMap.cs").TrimEnd('/')).Replace('\\','/');
                string prefabs=Folder(Folder(maps,"Resources"),"ModeMaps");PrefabUtility.SaveAsPrefabAsset(world,prefabs+"/Mode_powerup-test.prefab");
                BuildOrb(true,lettering);BuildOrb(false,lettering);
                AssetDatabase.SaveAssets();Physics.SyncTransforms();
                if(Physics.CheckCapsule(map.SafeSpawnPoint()+Vector3.up*.3f,map.SafeSpawnPoint()+Vector3.up*1.7f,.25f,(1<<Layers.Surface),QueryTriggerInteraction.Ignore))throw new Exception("Lab spawn blocked.");
                if(!EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"))throw new Exception("Disaster validator unavailable.");
                var sun=Group("Preview sun").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(45,-35,0);sun.intensity=1.2f;
                RenderSettings.ambientLight=new Color(.55f,.65f,.8f);
                var camera=Group("Preview camera").AddComponent<Camera>();camera.transform.position=new Vector3(44,32,-46);camera.transform.LookAt(new Vector3(0,4,0));camera.backgroundColor=new Color(.35f,.65f,.85f);camera.clearFlags=CameraClearFlags.SolidColor;
                string scenes=Folder(Path.GetDirectoryName(ProjectPath.FolderOf("PowerupLabTools.cs").TrimEnd('/')).Replace('\\','/'),"Scenes");
                EditorSceneManager.SaveScene(scene,scenes+"/Powerup Test Island Preview.unity");
                Capture(camera,"PowerupTestIsland.png");
                foreach(bool speed in new[]{true,false})
                {
                    string prefabPath=Folder(Folder(maps,"Resources"),"Powerups")+"/"+(speed?"Speed":"Jump")+"Orb.prefab";
                    var orb=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));orb.transform.position=new Vector3(speed?0:3,1.1f,-12);
                }
                camera.transform.position=new Vector3(5,2,-18);camera.transform.LookAt(new Vector3(1.5f,1.1f,-12));
                foreach(var orb in UnityEngine.Object.FindObjectsByType<PowerupOrbVisual>(FindObjectsSortMode.None))if(orb.label)orb.label.rotation=Quaternion.LookRotation(orb.label.position-camera.transform.position);
                Capture(camera,"PowerupOrbs.png");
                File.WriteAllText(ProjectPath.Report("PowerupLab-authoring.txt"),"PASS 68m island, one fixed climbable tower, five ledges, ground-level spawn, two 20-second orb prefabs. Presentation has no solid colliders. Disaster validation ran.\n");
            }
            finally
            {
                if(saved.Length>0&&saved.All(s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(saved);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }
        static void BuildOrb(bool speed,Material lettering)
        {
            string id=speed?"Speed":"Jump";var tint=speed?new Color(.1f,.85f,1):new Color(.9f,.35f,1);
            var shell=Material(id+" glass",tint);shell.shader=Shader.Find("DeadlyJenga/Powerups/Glass");shell.SetColor("_Tint",tint);EditorUtility.SetDirty(shell);
            var ring=Material(id+" glow",tint);ring.EnableKeyword("_EMISSION");ring.SetColor("_EmissionColor",tint*.7f);EditorUtility.SetDirty(ring);
            var art=Group(id+" Powerup Visual");var component=art.AddComponent<PowerupOrbVisual>();
            Shape("Glass sphere",art.transform,Vector3.zero,Vector3.one*1.45f,shell,PrimitiveType.Sphere);
            var spin=Group("Orbital rings",art.transform);component.rings=spin.transform;
            for(int plane=0;plane<2;plane++)
            {
                var hoop=Group("Energy hoop",spin.transform);hoop.transform.localRotation=Quaternion.Euler(plane*65,0,20);
                for(int i=0;i<40;i++)
                {
                    float angle=i*Mathf.PI*2/40;var p=new Vector3(Mathf.Cos(angle)*.67f,0,Mathf.Sin(angle)*.67f);
                    var bead=Shape("Hoop segment",hoop.transform,p,new Vector3(.11f,.025f,.035f),ring);bead.transform.localRotation=Quaternion.Euler(0,-angle*Mathf.Rad2Deg+90,0);
                }
            }
            var filters=spin.GetComponentsInChildren<MeshFilter>();
            var combined=new Mesh{name=id+" powerup hoops"};combined.CombineMeshes(filters.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=spin.transform.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray());
            string hoopsPath=artFolder+"/"+id+" hoops.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(hoopsPath);
            if(existing){existing.Clear();existing.vertices=combined.vertices;existing.normals=combined.normals;existing.uv=combined.uv;existing.triangles=combined.triangles;existing.RecalculateBounds();EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(combined);combined=existing;}
            else AssetDatabase.CreateAsset(combined,hoopsPath);
            foreach(var filter in filters)UnityEngine.Object.DestroyImmediate(filter.gameObject);
            var batched=Group("Combined hoops",spin.transform);batched.AddComponent<MeshFilter>().sharedMesh=combined;batched.AddComponent<MeshRenderer>().sharedMaterial=ring;
            var label=Group("Label",art.transform);component.label=label.transform;
            var text=label.AddComponent<TextMesh>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=40;text.characterSize=.075f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
            text.text=speed?"2x\nSPEED\n20 SEC":"3x\nJUMP\n20 SEC";text.color=tint;label.GetComponent<Renderer>().sharedMaterial=lettering;
            var visual=PrefabUtility.SaveAsPrefabAsset(art,artFolder+"/"+id+" Orb Visual.prefab");UnityEngine.Object.DestroyImmediate(art);
            string maps=Path.GetDirectoryName(ProjectPath.FolderOf("PowerupTestMap.cs").TrimEnd('/')).Replace('\\','/');string resources=Folder(Folder(maps,"Resources"),"Powerups");
            string effectPath=resources+"/"+(speed?"Speed2x":"Jump3x")+".asset";var effect=AssetDatabase.LoadAssetAtPath<EffectType>(effectPath);
            if(!effect){effect=ScriptableObject.CreateInstance<EffectType>();effect.name=speed?"2x Speed":"3x Jump";AssetDatabase.CreateAsset(effect,effectPath);}
            var pickup=Group(id+" Powerup");var logic=pickup.AddComponent<MovementPowerupPickup>();logic.effect=effect;logic.duration=20;
            var collider=pickup.AddComponent<SphereCollider>();collider.isTrigger=true;collider.radius=.74f;
            var body=pickup.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
            PrefabUtility.InstantiatePrefab(visual,pickup.transform);PrefabUtility.SaveAsPrefabAsset(pickup,resources+"/"+id+"Orb.prefab");UnityEngine.Object.DestroyImmediate(pickup);
        }
        static void Capture(Camera camera,string file)
        {
            var rt=RenderTexture.GetTemporary(1400,900,24);var previous=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1400,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1400,900),0,0);image.Apply();File.WriteAllBytes(ProjectPath.Report(file),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);}
            finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
        }
    }
}
