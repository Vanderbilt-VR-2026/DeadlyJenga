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
    [InitializeOnLoad] public static class TikiResortTools
    {
        static string output;
        static Transform art,collision;
        static Material teak,bamboo,thatch,sand,coral,turquoise,yellow,ivory,leaf,water;
        static readonly List<string> report=new List<string>();
        static TikiResortTools()=>EditorApplication.update+=()=>
        {
            string request=ProjectPath.Report("Tiki.build.request");
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            string action=File.ReadAllText(request).Trim();File.Delete(request);try{if(action=="polish")PolishTraversal();else Build();}catch(Exception e){File.WriteAllText(ProjectPath.Report("Tiki-authoring.txt"),"FAIL "+e);Debug.LogException(e);}
        };
        [MenuItem("Tools/Deadly Jenga/Maps/Build Separate Waterfront Tiki Resort")]
        public static void Build()
        {
            if(Resources.Load<GameObject>("ModeMaps/Mode_tiki-resort"))throw new InvalidOperationException("The resort already exists; edit its own assets rather than replacing them.");
            string visuals=Path.GetDirectoryName(ProjectPath.FolderOf("TikiResortAtmosphere.cs").TrimEnd('/')).Replace('\\','/');
            output=Folder(visuals,"TikiResort");Folder(output,"Materials");Folder(output,"Textures");Folder(output,"Meshes");
            var active=SceneManager.GetActiveScene();var previousWorld=Services.World;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            report.Clear();
            try
            {
                Materials();
                // Duplicate through Unity, then unpack so no object overrides can reach the original.
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("Mode_cantina.prefab"));
                var root=(GameObject)PrefabUtility.InstantiatePrefab(source,scene);PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                root.name="Waterfront Tiki Resort";Object.DestroyImmediate(root.GetComponent<CantinaMap>());var map=root.AddComponent<TikiResortMap>();
                art=root.transform.Find("Visual");collision=root.transform.Find("Collision");
                Object.DestroyImmediate(art.GetComponent<CantinaAtmosphere>());
                foreach(var child in art.Cast<Transform>().ToArray())
                {
                    string n=child.name;
                    if(!(n.StartsWith("Rounded bar")||n=="Bar display props"||n.StartsWith("Twin tanks")||n.StartsWith("Table and three chairs")||n=="Finished tank interiors"))Object.DestroyImmediate(child.gameObject);
                }
                foreach(var child in collision.Cast<Transform>().ToArray())
                    if(child.name.Contains("wall")||child.name=="Ceiling"||child.name=="Passage shutter"||child.name=="Arch pier")Object.DestroyImmediate(child.gameObject);
                var originals=new Dictionary<Material,Material>();
                foreach(var renderer in art.GetComponentsInChildren<Renderer>())
                {
                    var replacements=renderer.sharedMaterials;
                    for(int i=0;i<replacements.Length;i++)
                    {
                        var original=replacements[i];if(!original)continue;
                        if(!originals.TryGetValue(original,out var copy))
                        {
                            copy=new Material(original){name="Resort "+original.name};AssetDatabase.CreateAsset(copy,output+"/Materials/"+copy.name+".mat");originals.Add(original,copy);
                        }
                        replacements[i]=copy;
                    }
                    renderer.sharedMaterials=replacements;
                }
                // Preserve glass and liquid while replacing the architectural surfaces with wood.
                foreach(Transform group in art)
                {
                    if(group.name=="Finished tank interiors")continue;
                    foreach(var renderer in group.GetComponentsInChildren<Renderer>())
                    {
                        bool painted=renderer.sharedMaterial.name.Contains("ivory")||renderer.sharedMaterial.name.Contains("counter");
                        Material mat=group.name.StartsWith("Table")?((group.GetSiblingIndex()%3)==0?coral:(group.GetSiblingIndex()%3)==1?turquoise:ivory):painted?bamboo:teak;
                        renderer.sharedMaterials=Enumerable.Repeat(mat,renderer.sharedMaterials.Length).ToArray();
                    }
                }
                Deck();CabanaRow();Bar();Stage();LagoonDrinks();Pier();Island();Beach();Palms();Lanterns();
                var sky=new Material(Shader.Find("Candy Tropical/Sky")){name="Resort afternoon sky"};sky.SetColor("_Zenith",new Color(.06f,.48f,.95f));sky.SetColor("_Horizon",new Color(.55f,.9f,1));sky.SetFloat("_CloudCover",.3f);Save(sky);
                var lighting=Group(art,"Resort sunlight");var sun=lighting.gameObject.AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.91f,.76f);sun.intensity=1.15f;sun.shadows=LightShadows.Soft;lighting.localRotation=Quaternion.Euler(48,-32,0);
                var mood=art.gameObject.AddComponent<TikiResortAtmosphere>();mood.resortSky=sky;mood.sunlight=sun;mood.practicalLights=art.GetComponentsInChildren<Light>().Where(l=>l!=sun).ToArray();
                CombineOpaqueArt();
                var visualAsset=PrefabUtility.SaveAsPrefabAsset(art.gameObject,output+"/Waterfront Tiki Resort Visual.prefab");Object.DestroyImmediate(art.gameObject);
                art=((GameObject)PrefabUtility.InstantiatePrefab(visualAsset,root.transform)).transform;art.name="Visual";
                string maps=Path.GetDirectoryName(ProjectPath.Find("Mode_cantina.prefab")).Replace('\\','/');
                PrefabUtility.SaveAsPrefabAsset(root,maps+"/Mode_tiki-resort.prefab");AssetDatabase.SaveAssets();Physics.SyncTransforms();
                Check(!art.GetComponentsInChildren<Collider>().Any(),"Resort art contains no solid collision");
                Check(!art.GetComponent<CantinaAtmosphere>()&&art.GetComponent<TikiResortAtmosphere>(),"Resort owns independent outdoor lighting");
                Check(!art.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Any(m=>m&&AssetDatabase.GetAssetPath(m).Contains("CantinaV2/")),"No resort renderer shares a mutable cantina material");
                Check(!Physics.CheckCapsule(map.SafeSpawnPoint()+Vector3.up*.2f,map.SafeSpawnPoint()+Vector3.up*.8f,.15f,1<<Layers.Surface),"Original seating-area spawn remains clear");
                Check(!map.IsSheltered(map.SafeSpawnPoint()),"Central seating is open to the sky");
                Check(EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"),"Disaster setup validator ran");
                var camera=Group(root.transform,"Resort review camera").gameObject.AddComponent<Camera>();camera.transform.position=root.transform.TransformPoint(new Vector3(3,1.7f,-7));camera.transform.LookAt(root.transform.TransformPoint(new Vector3(-4,2,5)));camera.nearClipPlane=.025f;
                RenderSettings.skybox=sky;RenderSettings.ambientLight=new Color(.55f,.65f,.7f);
                string scenes=Path.GetDirectoryName(ProjectPath.Find("Cantina V2 Preview.unity")).Replace('\\','/');EditorSceneManager.SaveScene(scene,scenes+"/Waterfront Tiki Resort Preview.unity");
                report.Add("OUTPUT "+maps+"/Mode_tiki-resort.prefab");report.Add("Separate duplicate. Source geometry reused read-only; original map and materials remain intact.");
                File.WriteAllLines(ProjectPath.Report("Tiki-authoring.txt"),report);
            }
            finally{EditorSceneManager.CloseScene(scene,true);if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);Services.World=previousWorld;}
        }
        static void Materials()
        {
            var grain=new Texture2D(512,512,TextureFormat.RGB24,false);var colors=new Color[512*512];
            for(int y=0;y<512;y++)for(int x=0;x<512;x++)
            {float u=x/512f,v=y/512f;float bend=Mathf.Sin(v*6.283f)*.023f+Mathf.Sin(v*18.85f)*.009f;float stripe=Mathf.Sin((u+bend)*6.283f*54);float fine=Mathf.PerlinNoise(u*110,v*9);float knots=Mathf.Sin(Mathf.Sqrt(Mathf.Pow((u-.47f)*4,2)+Mathf.Pow((v-.36f)*1.3f,2))*95);float value=.78f+stripe*.08f+fine*.2f+knots*.022f;colors[y*512+x]=Color.white*value;}
            grain.SetPixels(colors);grain.Apply();string path=output+"/Textures/Tiki wood grain.png";File.WriteAllBytes(path,grain.EncodeToPNG());Object.DestroyImmediate(grain);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;importer.maxTextureSize=512;importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Material Wood(string name,Color color,float shine=.25f){var m=Mat(name,color,shine);m.SetTexture("_BaseMap",texture);m.SetTextureScale("_BaseMap",new Vector2(2,.5f));EditorUtility.SetDirty(m);return m;}
            teak=Wood("Honey teak",new Color(.51f,.25f,.105f));bamboo=Wood("Golden bamboo",new Color(.84f,.61f,.28f));thatch=Wood("Woven palm thatch",new Color(.75f,.49f,.15f),.12f);
            sand=Mat("Warm fine sand",new Color(.92f,.79f,.54f),.08f);coral=Wood("Coral painted wood",new Color(1,.26f,.22f));turquoise=Wood("Lagoon painted wood",new Color(.04f,.7f,.67f));yellow=Wood("Sunshine painted wood",new Color(1,.73f,.12f));ivory=Wood("Weathered cream wood",new Color(.93f,.89f,.7f));leaf=Mat("Lush palm fronds",new Color(.14f,.46f,.16f),.3f);
            water=new Material(Shader.Find("Candy Tropical/Ocean")){name="Clear resort lagoon"};water.SetColor("_Shallow",new Color(.035f,.82f,.74f));water.SetColor("_Deep",new Color(.015f,.38f,.72f));water.SetFloat("_WaterHeight",-.7f);water.SetFloat("_WaveHeight",.06f);water.SetFloat("_SandTop",-.225f);water.SetFloat("_SandDepth",1.4f);water.SetVector("_Island",new Vector4(0,0,15,12.5f));Save(water);
        }
        static Material Mat(string name,Color tint,float shine)
        {var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};m.SetColor("_BaseColor",tint);m.SetFloat("_Smoothness",shine);Save(m);return m;}
        static void Save(Material m)=>AssetDatabase.CreateAsset(m,output+"/Materials/"+m.name+".mat");
        static void Deck()
        {
            var deck=Group(art,"Hand-laid teak boardwalk");
            for(int x=0;x<28;x++)Shape(deck,"Deck plank",new Vector3(-13.5f+x,-.06f,0),new Vector3(.975f,.12f,21),x%6==0?bamboo:teak);
            for(int side=-1;side<=1;side+=2)
            {Shape(deck,"Colored deck fascia",new Vector3(side*14,-.3f,0),new Vector3(.16f,.6f,21),side<0?coral:turquoise);Rail(new Vector3(side*14,0,0),21,true);}
            Rail(new Vector3(-8,0,-10.5f),12,false);Rail(new Vector3(4,0,-10.5f),4,false);Rail(new Vector3(12,0,-10.5f),4,false);
            for(int s=0;s<4;s++){float z=-11-s*.62f;float top=-s*.12f;Shape(art,"Broad beach steps",new Vector3(8,top-.1f,z),new Vector3(4,.2f,.7f),teak);Box("Beach step",new Vector3(8,top-.1f,z),new Vector3(4,.2f,.7f));}
        }
        static void CabanaRow()
        {
            var row=Group(art,"Six colorful resort cabanas");var palette=new[]{turquoise,coral,yellow,ivory,coral,turquoise};string[] titles={"ALOHA","TIKI","SURF","ISLAND","SUNSET","LAGOON"};
            for(int i=0;i<6;i++)
            {
                float x=-7.985f+i*3.194f;var bay=Group(row,"Resort station "+(i+1));
                foreach(float dx in new[]{-1.38f,1.38f})foreach(float z in new[]{8.1f,10f}){Pole(bay,"Bamboo corner",new Vector3(x+dx,1.9f,z),new Vector3(.16f,3.8f,.16f),bamboo);Box("Cabana post",new Vector3(x+dx,1.9f,z),new Vector3(.16f,3.8f,.16f));}
                for(int y=0;y<9;y++)Shape(bay,"Painted shutter board",new Vector3(x,.18f+y*.25f,9.97f),new Vector3(2.65f,.23f,.12f),y%4==0?ivory:palette[i]);
                Box("Cabana back panel",new Vector3(x,1.15f,9.97f),new Vector3(2.65f,2.3f,.12f));
                Shape(bay,"Colored header",new Vector3(x,3.15f,8.03f),new Vector3(2.9f,.55f,.16f),palette[i]);Text(bay,titles[i],new Vector3(x,3.15f,7.92f),.16f);
                Roof(bay,new Vector3(x,3.55f,9),3.2f,3.3f);
            }
        }
        static void Bar()
        {
            var bar=Group(art,"Lagoon tiki bar canopy");Roof(bar,new Vector3(-11,4.1f,.2f),6.4f,12.5f);
            foreach(float x in new[]{-13.8f,-8.2f})foreach(float z in new[]{-5.4f,5.8f}){Pole(bar,"Bound bamboo post",new Vector3(x,2.05f,z),new Vector3(.22f,4.1f,.22f),bamboo);Box("Bar canopy post",new Vector3(x,2.05f,z),new Vector3(.22f,4.1f,.22f));}
            for(int i=0;i<4;i++)Shape(bar,"Open display shelf",new Vector3(-13,1.5f+i*.55f,.2f),new Vector3(.7f,.1f,8),bamboo);
            var palette=new[]{turquoise,coral,yellow};for(int i=0;i<15;i++){var bottle=Shape(bar,"Island fruit bottle",new Vector3(-12.9f,1.9f,-3.5f+i*.5f),new Vector3(.18f,.3f,.18f),palette[i%3],PrimitiveType.Cylinder);Shape(bar,"Bottle neck",new Vector3(-12.9f,2.25f,-3.5f+i*.5f),new Vector3(.08f,.1f,.08f),palette[i%3],PrimitiveType.Cylinder);}
            Shape(bar,"TIKI BAR sign",new Vector3(-8.14f,3.25f,.1f),new Vector3(.15f,.7f,4.2f),turquoise);var label=Text(bar,"TIKI BAR",new Vector3(-8.03f,3.25f,.1f),.25f);label.localRotation=Quaternion.Euler(0,-90,0);
        }
        static void Stage()
        {
            var stage=Group(art,"Open-air sunset music stage");Shape(stage,"Stage deck",new Vector3(-7,.22f,-8.9f),new Vector3(6.35f,.4f,2.5f),teak);Roof(stage,new Vector3(-7,4.1f,-8.9f),7,3.8f);
            foreach(float x in new[]{-10f,-4f}){Pole(stage,"Stage bamboo",new Vector3(x,2.05f,-9.5f),new Vector3(.2f,4.1f,.2f),bamboo);Box("Stage post",new Vector3(x,2.05f,-9.5f),new Vector3(.2f,4.1f,.2f));}
            Shape(stage,"Music backdrop stripe",new Vector3(-7,2.4f,-9.85f),new Vector3(5,.3f,.12f),coral);Text(stage,"SUNSET SESSIONS",new Vector3(-7,2.85f,-9.9f),.18f);
            for(int i=0;i<3;i++){Shape(stage,"Colorful conga",new Vector3(-8.2f+i*1.1f,.75f,-9),new Vector3(.5f,.55f,.5f),i==1?coral:turquoise,PrimitiveType.Cylinder);Shape(stage,"Drum skin",new Vector3(-8.2f+i*1.1f,1.31f,-9),new Vector3(.53f,.04f,.53f),ivory,PrimitiveType.Cylinder);}
        }
        static void LagoonDrinks()
        {
            var station=Group(art,"Lagoon refreshments");
            foreach(float z in new[]{3.172f,5.359f}){Shape(station,"Drink dispenser bamboo tap",new Vector3(10.68f,1.2f,z),new Vector3(.4f,.12f,.12f),bamboo);Shape(station,"Tap handle",new Vector3(10.55f,1.35f,z),new Vector3(.08f,.2f,.08f),coral);}
            Text(station,"LAGOON COOLERS",new Vector3(10.5f,3.9f,4.3f),.18f).localRotation=Quaternion.Euler(0,90,0);
        }
        static void Pier()
        {
            var pier=Group(art,"Waterfront timber pier");
            for(int i=0;i<39;i++)Shape(pier,"Pier plank",new Vector3(0,-.06f,-10.7f-i*.6f),new Vector3(3.8f,.12f,.57f),i%6==0?turquoise:teak);
            Box("Pier continuous walking deck",new Vector3(0,-.12f,-22.1f),new Vector3(3.8f,.24f,23.6f));
            foreach(float x in new[]{-1.9f,1.9f}){Rail(new Vector3(x,0,-22.1f),23.6f,true);for(int i=0;i<8;i++)Pole(pier,"Pier piling",new Vector3(x,-1.1f,-11-i*3),new Vector3(.3f,3.2f,.3f),teak);}
            Shape(pier,"Waterfront landing",new Vector3(0,-.06f,-34.2f),new Vector3(7,.12f,3),teak);Box("Pier landing",new Vector3(0,-.12f,-34.2f),new Vector3(7,.24f,3));Rail(new Vector3(0,0,-35.65f),7,false);
        }
        static void Island()
        {
            var vertices=new List<Vector3>{new Vector3(0,-.45f,0)};var indices=new List<int>();const int segments=96;
            for(int ring=0;ring<2;ring++)for(int i=0;i<segments;i++)
            {float a=i*Mathf.PI*2/segments;float r=ring==0?.86f:1.06f;float shape=1+Mathf.Sin(a*5)*.012f;vertices.Add(new Vector3(Mathf.Cos(a)*30*r*shape,ring==0?-.45f:-1.8f,Mathf.Sin(a)*25*r*shape));}
            for(int i=0;i<segments;i++){int n=(i+1)%segments;indices.AddRange(new[]{0,1+n,1+i});int a=1+i,b=1+n,c=1+segments+i,d=1+segments+n;indices.AddRange(new[]{a,b,c,b,d,c});}
            var mesh=new Mesh{name="Resort beach contour"};mesh.SetVertices(vertices);mesh.triangles=indices.ToArray();mesh.uv=vertices.Select(v=>new Vector2(v.x,v.z)*.2f).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,output+"/Meshes/Resort beach visual.asset");var beach=Group(art,"Pale sand island");beach.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;beach.gameObject.AddComponent<MeshRenderer>().sharedMaterial=sand;
            var physical=Object.Instantiate(mesh);physical.name="Resort beach collision";string maps=Path.GetDirectoryName(ProjectPath.FolderOf("TikiResortMap.cs").TrimEnd('/')).Replace('\\','/');var folder=Folder(maps,"TikiResort");AssetDatabase.CreateAsset(physical,folder+"/Resort beach collision.asset");var ground=Group(collision,"Contoured walkable beach");ground.gameObject.layer=Layers.Surface;ground.gameObject.AddComponent<MeshCollider>().sharedMesh=physical;
            var ocean=Shape(art,"Turquoise waterfront",new Vector3(0,-1.4f,0),new Vector3(1000,1,1000),water,PrimitiveType.Plane);
        }
        static void Beach()
        {
            var beach=Group(art,"Island resort beach lounges");
            foreach(int side in new[]{-1,1})for(int i=0;i<3;i++)
            {
                float x=side*(19+i%2*3),z=-5+i*6;
                var lounge=Group(beach,"Painted beach lounger");lounge.localPosition=new Vector3(x,-.43f,z);lounge.localRotation=Quaternion.Euler(0,side*-25,0);
                Shape(lounge,"Timber seat",new Vector3(0,.35f,0),new Vector3(1.3f,.15f,2.6f),bamboo);var back=Shape(lounge,"Colored recliner back",new Vector3(0,.8f,.8f),new Vector3(1.3f,1.2f,.14f),i%2==0?coral:turquoise);back.transform.localRotation=Quaternion.Euler(-25,0,0);
                foreach(float dx in new[]{-.5f,.5f})Shape(lounge,"Lounge leg",new Vector3(dx,.08f,0),new Vector3(.12f,.55f,2),teak);
                Box("Beach lounger",new Vector3(x,.2f,z),new Vector3(1.6f,1.3f,2.7f));
                var parasol=Group(beach,"Beach parasol");parasol.localPosition=new Vector3(x+side*2,-.45f,z+1);Pole(parasol,"Parasol pole",new Vector3(0,1.5f,0),new Vector3(.09f,3,.09f),bamboo);
                for(int p=0;p<8;p++){var panel=Shape(parasol,"Colored parasol panel",new Vector3(Mathf.Sin(p*Mathf.PI/4)*.65f,3.05f,Mathf.Cos(p*Mathf.PI/4)*.65f),new Vector3(1.1f,.12f,1.5f),p%2==0?turquoise:ivory);panel.transform.localRotation=Quaternion.Euler(-18,p*45,0);}
            }
        }
        static void Palms()
        {
            var palms=Group(art,"Resort palms");
            for(int i=0;i<14;i++)
            {
                float a=i*Mathf.PI*2/14;float x=Mathf.Cos(a)*25,z=Mathf.Sin(a)*20;float h=6.8f+(i%3)*.65f;var palm=Group(palms,"Sculpted island palm");palm.localPosition=new Vector3(x,-.45f,z);
                for(int s=0;s<6;s++){float y=(s+.5f)*h/6;var trunk=Pole(palm,"Ringed teak trunk",new Vector3(Mathf.Sin(s*.25f)*.4f,y,0),new Vector3(.45f-s*.035f,h/6*1.06f,.45f-s*.035f),teak);trunk.localRotation=Quaternion.Euler(0,0,-s*2);}
                for(int f=0;f<8;f++)
                {
                    var frond=Group(palm,"Curved palm frond");frond.localPosition=new Vector3(.4f,h,0);frond.localRotation=Quaternion.Euler(0,f*45+i*13,0);
                    for(int s=0;s<5;s++){float t=s/4f;var blade=Shape(frond,"Tapered leaf",new Vector3(0,-t*t*1.2f,t*3.1f),new Vector3(.7f*(1-t)+.07f,.08f,.9f),leaf);blade.transform.localRotation=Quaternion.Euler(12+t*25,0,0);}
                }
                var body=Group(collision,"Palm trunk");body.localPosition=new Vector3(x,h*.5f-.45f,z);body.gameObject.layer=Layers.Surface;var capsule=body.gameObject.AddComponent<CapsuleCollider>();capsule.height=h;capsule.radius=.32f;
            }
        }
        static void Lanterns()
        {
            var strings=Group(art,"Festoon lanterns and bunting");var palette=new[]{coral,turquoise,yellow};
            for(int row=0;row<2;row++)for(int i=0;i<18;i++)
            {float x=-13+i*1.53f,y=4.6f-Mathf.Sin(i/17f*Mathf.PI)*.8f,z=row==0?-5.8f:5.5f;Shape(strings,"Festoon cord",new Vector3(x,y,z),new Vector3(1.58f,.025f,.025f),teak);Shape(strings,"Colored lantern",new Vector3(x,y-.2f,z),new Vector3(.23f,.3f,.23f),palette[i%3],PrimitiveType.Sphere);if(i%5==0){var l=Group(strings,"Soft lantern accent").gameObject.AddComponent<Light>();l.transform.localPosition=new Vector3(x,y-.3f,z);l.type=LightType.Point;l.range=2;l.intensity=.3f;l.color=i%2==0?new Color(1,.57f,.25f):new Color(.15f,1,.85f);l.shadows=LightShadows.None;}}
        }
        static void Roof(Transform parent,Vector3 p,float width,float depth)
        {
            var roof=Group(parent,"Layered palm-thatch roof");roof.localPosition=p;
            foreach(int side in new[]{-1,1})
            {
                var panel=Shape(roof,"Woven thatch slope",new Vector3(side*width*.25f,width*.14f,0),new Vector3(width*.58f,.18f,depth),thatch);panel.transform.localRotation=Quaternion.Euler(0,0,-side*28);
                for(int i=0;i<Mathf.CeilToInt(depth/.22f);i++){float z=-depth*.5f+i*.22f;var fringe=Shape(roof,"Thatch fringe bundle",new Vector3(side*width*.48f,.02f,z),new Vector3(.38f,.12f,.17f),i%3==0?bamboo:thatch);fringe.transform.localRotation=Quaternion.Euler(0,0,-side*28);}
            }
            Shape(roof,"Bound ridge cap",new Vector3(0,width*.28f,0),new Vector3(.22f,.22f,depth+.1f),bamboo);
            Box("Thatched shelter roof",p+Vector3.up*(width*.14f),new Vector3(width,.18f,depth));
        }
        static void Rail(Vector3 p,float length,bool alongZ)
        {
            var rail=Group(art,"Painted waterfront railing");rail.localPosition=p;
            Shape(rail,"Coral top rail",new Vector3(0,1.05f,0),alongZ?new Vector3(.12f,.12f,length):new Vector3(length,.12f,.12f),turquoise);
            int count=Mathf.CeilToInt(length/2);for(int i=0;i<=count;i++){float d=-length/2+length*i/count;Shape(rail,"Bamboo baluster",new Vector3(alongZ?0:d,.52f,alongZ?d:0),new Vector3(.12f,1.1f,.12f),bamboo);}
            Box("Rail guard",p+Vector3.up*.55f,alongZ?new Vector3(.15f,1.1f,length):new Vector3(length,1.1f,.15f));
        }
        static Transform Text(Transform parent,string text,Vector3 position,float size)
        {var go=Group(parent,text);go.localPosition=position;go.localScale=Vector3.one*.6f;var label=go.gameObject.AddComponent<TextMesh>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");go.GetComponent<Renderer>().sharedMaterial=label.font.material;label.text=text;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.characterSize=size;label.fontSize=48;label.color=new Color(1,.96f,.8f);return go;}
        static Transform Group(Transform parent,string name){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        static GameObject Shape(Transform parent,string name,Vector3 p,Vector3 size,Material material,PrimitiveType type=PrimitiveType.Cube)
        {var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;return go;}
        static Transform Pole(Transform parent,string name,Vector3 p,Vector3 size,Material mat)=>Shape(parent,name,p,new Vector3(size.x,size.y*.5f,size.z),mat,PrimitiveType.Cylinder).transform;
        static void CombineOpaqueArt()
        {
            foreach(var old in art.Cast<Transform>().Where(t=>t.name.StartsWith("Batched ")).ToArray())Object.DestroyImmediate(old.gameObject);
            var batches=new Dictionary<Material,List<CombineInstance>>();var renderers=new List<Renderer>();
            foreach(var filter in art.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<MeshRenderer>();if(!renderer||!filter.sharedMesh||renderer.sharedMaterials.Length!=1)continue;
                var material=renderer.sharedMaterial;if(!material||material.renderQueue>=3000)continue;
                if(!batches.TryGetValue(material,out var batch))batches.Add(material,batch=new List<CombineInstance>());
                batch.Add(new CombineInstance{mesh=filter.sharedMesh,subMeshIndex=0,transform=art.worldToLocalMatrix*filter.transform.localToWorldMatrix});renderers.Add(renderer);
            }
            int index=0;
            foreach(var pair in batches)
            {
                var mesh=new Mesh{name="Resort batch "+pair.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();
                string path=output+"/Meshes/Batch_"+(index++).ToString("D2")+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
                var go=Group(art,"Batched "+pair.Key.name);go.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;go.gameObject.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;
            }
            foreach(var renderer in renderers)renderer.enabled=false;
        }
        static void Box(string name,Vector3 p,Vector3 size){var go=Group(collision,name);go.localPosition=p;go.gameObject.layer=Layers.Surface;go.gameObject.AddComponent<BoxCollider>().size=size;}
        [MenuItem("Tools/Deadly Jenga/Maps/Polish Tiki Resort Traversal")]
        public static void PolishTraversal()
        {
            string visualPath=ProjectPath.Find("Waterfront Tiki Resort Visual.prefab"),mapPath=ProjectPath.Find("Mode_tiki-resort.prefab");output=Path.GetDirectoryName(visualPath).Replace('\\','/');
            var previous=Services.World;var world=PrefabUtility.LoadPrefabContents(mapPath);var visual=PrefabUtility.LoadPrefabContents(visualPath);
            try
            {
                art=visual.transform;collision=world.transform.Find("Collision");
                teak=AssetDatabase.LoadAssetAtPath<Material>(output+"/Materials/Honey teak.mat");bamboo=AssetDatabase.LoadAssetAtPath<Material>(output+"/Materials/Golden bamboo.mat");turquoise=AssetDatabase.LoadAssetAtPath<Material>(output+"/Materials/Lagoon painted wood.mat");
                foreach(var rail in art.Cast<Transform>().Where(t=>t.name=="Painted waterfront railing"&&Vector3.Distance(t.localPosition,new Vector3(8,0,-10.5f))<.01f).ToArray())Object.DestroyImmediate(rail.gameObject);
                foreach(var guard in collision.Cast<Transform>().Where(t=>t.name=="Rail guard"&&Vector3.Distance(t.localPosition,new Vector3(8,.55f,-10.5f))<.01f).ToArray())Object.DestroyImmediate(guard.gameObject);
                if(!art.Cast<Transform>().Any(t=>t.name=="Painted waterfront railing"&&Vector3.Distance(t.localPosition,new Vector3(4,0,-10.5f))<.01f))
                {Rail(new Vector3(4,0,-10.5f),4,false);Rail(new Vector3(12,0,-10.5f),4,false);}
                if(!collision.Cast<Transform>().Any(t=>t.name=="Cabana post"))foreach(var post in art.GetComponentsInChildren<Transform>().Where(t=>t.name=="Bamboo corner"))
                    Box("Cabana post",art.InverseTransformPoint(post.position),new Vector3(.16f,3.8f,.16f));
                if(!collision.Cast<Transform>().Any(t=>t.name=="Cabana back panel"))for(int i=0;i<6;i++)Box("Cabana back panel",new Vector3(-7.985f+i*3.194f,1.15f,9.97f),new Vector3(2.65f,2.3f,.12f));
                var stage=collision.Find("Band raised stage");if(stage){stage.localPosition=new Vector3(-7,.22f,-8.9f);stage.GetComponent<BoxCollider>().size=new Vector3(6.35f,.4f,2.5f);}
                foreach(var label in art.GetComponentsInChildren<TextMesh>())label.transform.localScale=Vector3.one*.6f;
                CombineOpaqueArt();PrefabUtility.SaveAsPrefabAsset(visual,visualPath);PrefabUtility.SaveAsPrefabAsset(world,mapPath);AssetDatabase.SaveAssets();
                File.AppendAllText(ProjectPath.Report("Tiki-authoring.txt"),"PASS Beach stair opening, cabana post collision and stage height refined; material batches refreshed without changing original assets.\n");
            }
            finally{PrefabUtility.UnloadPrefabContents(visual);PrefabUtility.UnloadPrefabContents(world);Services.World=previous;}
            var active=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(mapPath),scene);
                var camera=Group(root.transform,"Resort review camera").gameObject.AddComponent<Camera>();camera.transform.position=root.transform.TransformPoint(new Vector3(3,1.7f,-7));camera.transform.LookAt(root.transform.TransformPoint(new Vector3(-4,2,5)));camera.nearClipPlane=.025f;
                var mood=root.GetComponentInChildren<TikiResortAtmosphere>();RenderSettings.skybox=mood.resortSky;
                EditorSceneManager.SaveScene(scene,ProjectPath.Find("Waterfront Tiki Resort Preview.unity"));
            }
            finally{EditorSceneManager.CloseScene(scene,true);if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);Services.World=previous;}
        }
        static string Folder(string parent,string name){var path=parent+"/"+name;if(!AssetDatabase.IsValidFolder(path))AssetDatabase.CreateFolder(parent,name);return path;}
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);report.Add("PASS "+message);}
    }
}
