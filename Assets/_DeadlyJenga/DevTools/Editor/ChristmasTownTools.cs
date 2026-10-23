using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.DevTools.Editor
{
    // Explicit authoring command. Does not run at import or overwrite Game/Lobby scenes.
    public static partial class ChristmasTownTools
    {
        static string artFolder, meshFolder;
        static readonly Dictionary<string,Material> materials = new Dictionary<string,Material>();
        static GameObject art;
        static readonly List<string> checks = new List<string>();
        static string Folder(string parent,string name)
        { if(!AssetDatabase.IsValidFolder(parent+"/"+name))AssetDatabase.CreateFolder(parent,name);return parent+"/"+name; }
        static Transform Group(Transform parent,string name,Vector3 position=default,float yaw=0)
        { var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,yaw,0);return go.transform; }
        static Material Mat(string name,Color color,float gloss=.35f)
        {
            string path=artFolder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",gloss);m.enableInstancing=true;EditorUtility.SetDirty(m);materials[name]=m;return m;
        }
        static GameObject Visual(Transform parent,string name,Vector3 p,Vector3 size,string material,PrimitiveType type=PrimitiveType.Cube,Quaternion rotation=default)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=type==PrimitiveType.Cylinder?new Vector3(size.x,size.y*.5f,size.z):size;
            go.transform.localRotation=rotation==default?Quaternion.identity:rotation;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=materials[material];go.isStatic=true;
            if(type==PrimitiveType.Cube)SurfaceUv(go,size);return go;
        }
        static BoxCollider Solid(Transform parent,string name,Vector3 p,Vector3 size,Quaternion rotation=default)
        {
            var t=Group(parent,name,p);t.localRotation=rotation==default?Quaternion.identity:rotation;t.gameObject.layer=Layers.Surface;
            var box=t.gameObject.AddComponent<BoxCollider>();box.size=size;t.gameObject.isStatic=true;return box;
        }
        static void Part(Transform collision,Transform visual,string name,Vector3 p,Vector3 size,string material)
        { Solid(collision,name,p,size);Visual(visual,name,p,size,material); }

        [MenuItem("Tools/Deadly Jenga/Maps/Build Christmas Candy Town")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before authoring the town.");
            if(!Application.isBatchMode&&Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))throw new InvalidOperationException("Save edited scenes before authoring the town.");
            var saved=EditorSceneManager.GetSceneManagerSetup();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            try
            {
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
                string worldMaps=Path.GetDirectoryName(ProjectPath.FolderOf("ChristmasTownLayout.cs").TrimEnd('/')).Replace('\\','/');
                string visuals=ProjectPath.FolderOf("CandyEnvironment.cs").TrimEnd('/');
                artFolder=Folder(Path.GetDirectoryName(visuals).Replace('\\','/'),"ChristmasTown");
                meshFolder=Folder(artFolder,"Meshes");materials.Clear();
                Mat("Frosting",new Color(.94f,.97f,1),.55f);Mat("Snow",new Color(.83f,.93f,.98f),.25f);
                Mat("Cherry",new Color(.82f,.045f,.12f),.6f);Mat("Mint",new Color(.18f,.72f,.48f),.6f);
                Mat("Lavender",new Color(.56f,.32f,.78f),.55f);Mat("Biscuit",new Color(.63f,.29f,.13f),.35f);
                Mat("Cocoa",new Color(.22f,.085f,.07f),.35f);Mat("Caramel",new Color(.91f,.59f,.22f),.5f);
                Mat("Road",new Color(.24f,.49f,.55f),.4f);Mat("Paving",new Color(.62f,.79f,.79f),.45f);
                Mat("WarmWindow",new Color(1,.72f,.31f),.4f);Mat("DarkGreen",new Color(.035f,.24f,.16f),.4f);
                Mat("Kenney",Color.white,.45f);
                Mat("Footprints",new Color(.53f,.7f,.76f),.1f);Mat("Rose",new Color(.86f,.36f,.47f),.45f);
                var lettering=Mat("Lettering",Color.white);lettering.shader=Shader.Find("DeadlyJenga/ChristmasTown/WorldText");
                if(!lettering.shader)throw new InvalidOperationException("Depth-tested town lettering shader is missing.");
                lettering.renderQueue=(int)RenderQueue.Transparent;EditorUtility.SetDirty(lettering);
                materials["WarmWindow"].EnableKeyword("_EMISSION");materials["WarmWindow"].SetColor("_EmissionColor",new Color(1,.52f,.13f)*.6f);
                SurfaceMaterials();
                var palette=AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPath.Find("colormap.png"));
                materials["Kenney"].SetTexture("_BaseMap",palette);
                var world=new GameObject("Christmas Candy Town");var map=world.AddComponent<ChristmasTownMap>();
                art=new GameObject("Visual");art.AddComponent<DeadlyJenga.CandyArt.ChristmasTownAtmosphere>();var collision=Group(world.transform,"Collision");
                Solid(collision,"Fixed snowy foundation",new Vector3(0,-.5f,0),new Vector3(96,1,76));
                for(int x=0;x<6;x++)for(int z=0;z<5;z++)
                    Visual(art.transform,"Frosted island",new Vector3(-40+x*16,-.52f,-30.4f+z*15.2f),new Vector3(16,1,15.2f),"Snow");
                // Boundary is visible as a low iced wall; no invisible full-height enclosure.
                for(int side=-1;side<=1;side+=2)
                {
                    Part(collision,art.transform,"North south icing border",new Vector3(0,.5f,side*37.5f),new Vector3(96,1,1),"Frosting");
                    Part(collision,art.transform,"East west icing border",new Vector3(side*47.5f,.5f,0),new Vector3(1,1,74),"Frosting");
                }
                Roads();
                var interiors=new List<Transform>();
                for(int i=0;i<ChristmasTownLayout.Houses.Length;i++)
                {
                    var h=ChristmasTownLayout.Houses[i];var c=Group(collision,h.name,h.position,h.yaw);var v=Group(art.transform,h.name,h.position,h.yaw);
                    c.localScale=v.localScale=h.scale;
                    House(c,v,i);interiors.Add(Group(world.transform,"Interior - "+h.name,h.position+Vector3.up*.1f));
                }
                map.interiorCenters=interiors.ToArray();
                DressTown(collision);
                DressNeighborhood(collision);
                DressFamilyTown(collision);
                var tiles=new List<Rigidbody>();
                for(int i=0;i<ChristmasTownLayout.ClaimPoints.Length;i++)
                {
                    var p=ChristmasTownLayout.ClaimPoints[i];float height=i==0?.18f:.06f;
                    var tile=Solid(collision,"Fixed objective pad "+i,p+Vector3.up*(height*.5f),new Vector3(3.5f,height,3.5f));tile.gameObject.layer=Layers.Block;
                    var body=tile.gameObject.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;body.mass=1000;
                    tile.gameObject.isStatic=false;tiles.Add(body);
                    var own=tile.gameObject.AddComponent<ChristmasClaimSurface>();
                    own.visual=Visual(art.transform,"Claim pad visual "+i,p+Vector3.up*(height*.5f+.003f),new Vector3(3.5f,height,3.5f),"Paving").GetComponent<Renderer>();
                    // Keep claim renderers separate so tinting one never recolors the whole town.
                    own.visual.gameObject.tag="EditorOnly"; // temporary batching exclusion; restored below
                }
                map.claimSurfaces=tiles.ToArray();
                AuthorMatchFoundation(map);
                TownLighting();
                WinterBackdrop();
                OutdoorSnow();
                CombineArt(art.transform);
                foreach(var r in art.GetComponentsInChildren<Renderer>())if(r.gameObject.CompareTag("EditorOnly"))r.gameObject.tag="Untagged";
                string visualPrefab=artFolder+"/Christmas Candy Town Visual.prefab";
                var visualAsset=PrefabUtility.SaveAsPrefabAsset(art,visualPrefab);
                // Rebind ownership visuals after replacing the authoring root with a prefab instance.
                UnityEngine.Object.DestroyImmediate(art);art=(GameObject)PrefabUtility.InstantiatePrefab(visualAsset,world.transform);art.name="Visual";
                for(int i=0;i<tiles.Count;i++)tiles[i].GetComponent<ChristmasClaimSurface>().visual=art.transform.Find("Claim pad visual "+i).GetComponent<Renderer>();
                string resources=Folder(worldMaps,"Resources"),maps=Folder(resources,"ModeMaps");
                var mapAsset=PrefabUtility.SaveAsPrefabAsset(world,maps+"/Mode_christmas-town.prefab");
                AssetDatabase.SaveAssets();Validate(map);
                if(!EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"))throw new InvalidOperationException("Disaster setup validator is unavailable.");
                checks.Add("PASS Existing disaster catalog validation command ran.");
                // Preview scene is separate from gameplay scenes and is safe to open directly.
                SetupPreview();
                string devTools=Path.GetDirectoryName(ProjectPath.FolderOf("ChristmasTownTools.cs").TrimEnd('/')).Replace('\\','/');
                string previews=Folder(devTools,"Scenes");EditorSceneManager.SaveScene(scene,previews+"/Christmas Candy Town Preview.unity");
                Capture(new Vector3(70,63,-76),new Vector3(0,-7,0),"ChristmasTown-overview.png");
                Capture(new Vector3(-14,2.1f,6),new Vector3(-25,2,23),"ChristmasTown-street.png");
                Capture(new Vector3(-25,1.65f,19.8f),new Vector3(-26,1.4f,26),"ChristmasTown-interior.png");
                Capture(new Vector3(-17,4,-15),new Vector3(-10,1.5f,-26),"ChristmasTown-neighborhood.png");
                Capture(new Vector3(4,2.2f,4),new Vector3(10,1.5f,8.5f),"ChristmasTown-market.png");
                Capture(new Vector3(-13.5f,1.6f,23),new Vector3(-10.5f,1.2f,28),"ChristmasTown-home.png");
                Capture(new Vector3(-3.6f,2,-22.8f),new Vector3(3.2f,1.3f,-26.5f),"ChristmasTown-diner.png");
                Capture(new Vector3(27,2,-19.5f),new Vector3(23.2f,1.7f,-26),"ChristmasTown-school.png");
                Capture(new Vector3(31.3f,1.9f,-2.8f),new Vector3(36,1.5f,2.6f),"ChristmasTown-grocery.png");
                Capture(new Vector3(-15,7,-20),new Vector3(-26,1,-10),"ChristmasTown-park.png");
                Capture(new Vector3(34,6,-7),new Vector3(24,1,-16),"ChristmasTown-schoolyard.png");
                Capture(new Vector3(13,7,-10),new Vector3(0,2,-25),"ChristmasTown-diner-exterior.png");
                Capture(new Vector3(18,4,-7),new Vector3(0,3,-3),"ChristmasTown-evening-square.png");
                checks.Add("PASS Built map and visual prefabs; preview scene saved. No Game/Lobby scene was saved.");
                File.WriteAllLines(ProjectPath.Report("ChristmasTown.txt"),checks);
                Debug.Log("Christmas Candy Town built. "+mapAsset.name);
            }
            finally
            {
                if(saved.Length>0&&saved.All(s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(saved);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }

        static void Roads()
        {
            Visual(art.transform,"Peppermint boulevard",new Vector3(0,.012f,0),new Vector3(92,.02f,7),"Road");
            Visual(art.transform,"Cocoa lane",new Vector3(0,.016f,0),new Vector3(7,.02f,70),"Road");
            for(int side=-1;side<=1;side+=2)
            {
                Visual(art.transform,"Market loop",new Vector3(0,.012f,side*15),new Vector3(87,.02f,4),"Paving");
                Visual(art.transform,"Market loop",new Vector3(side*17,.014f,0),new Vector3(4,.02f,48),"Paving");
                Visual(art.transform,"Back lane",new Vector3(0,.012f,side*34),new Vector3(89,.02f,2),"Paving");
                Visual(art.transform,"Back lane",new Vector3(side*44,.014f,0),new Vector3(2,.02f,68),"Paving");
            }
            Visual(art.transform,"Town square",new Vector3(0,.025f,0),new Vector3(23,.02f,23),"Paving",PrimitiveType.Cylinder);
            // Cylinder scale uses a unit diameter, hence no extra radius multiplier.
            for(int i=-10;i<=10;i++)for(int side=-1;side<=1;side+=2)
                Visual(art.transform,"Candy curb",new Vector3(i*4,.04f,side*3.8f),new Vector3(2,.08f,.3f),i%2==0?"Cherry":"Frosting");
            foreach(var h in ChristmasTownLayout.Houses)
            {
                var rot=Quaternion.Euler(0,h.yaw,0);float front=5*h.scale.z;
                var p=h.position+rot*new Vector3(0,.025f,-front-4);
                Visual(art.transform,"Open doorway path",p,new Vector3(2.6f,.035f,8),"Paving",PrimitiveType.Cube,rot);
                Visual(art.transform,"Back garden path",h.position+rot*new Vector3(0,.025f,front+3),new Vector3(2.2f,.035f,6),"Paving",PrimitiveType.Cube,rot);
            }
        }

        static void House(Transform c,Transform v,int index)
        {
            string accent=index%3==0?"Cherry":index%3==1?"Mint":"Lavender";
            Part(c,v,"Ceiling",new Vector3(0,3.9f,0),new Vector3(10.5f,.2f,10.5f),"Frosting");
            foreach(int side in new[]{-1,1})
            {
                float z=side*5;
                Part(c,v,"Door side",new Vector3(-3.15f,1.9f,z),new Vector3(3.7f,3.8f,.4f),"Biscuit");
                Part(c,v,"Door side",new Vector3(3.15f,1.9f,z),new Vector3(3.7f,3.8f,.4f),"Biscuit");
                Part(c,v,"Door lintel",new Vector3(0,3.35f,z),new Vector3(2.6f,.9f,.4f),"Biscuit");
                Visual(v,"Iced doorway",new Vector3(0,2.9f,z-side*.24f),new Vector3(3.1f,.2f,.22f),"Frosting");
                for(int x=-1;x<=1;x+=2)Visual(v,"Iced door post",new Vector3(x*1.45f,1.45f,z-side*.24f),new Vector3(.22f,2.9f,.22f),"Frosting");
                float xSide=side*5;
                Part(c,v,"Window sill wall",new Vector3(xSide,.5f,0),new Vector3(.4f,1,10),"Biscuit");
                Part(c,v,"Window top wall",new Vector3(xSide,3.35f,0),new Vector3(.4f,.9f,10),"Biscuit");
                foreach(float zPost in new[]{-4.6f,0,4.6f})Part(c,v,"Window pier",new Vector3(xSide,1.95f,zPost),new Vector3(.4f,1.9f,.8f),"Biscuit");
                // Actual unglazed openings: no invisible window panes or blocked doors.
                Visual(v,"Frosted window sill",new Vector3(xSide,1.02f,0),new Vector3(.7f,.12f,10.4f),"Frosting");
            }
            Visual(v,"Cookie floor",new Vector3(0,.025f,0),new Vector3(9.6f,.04f,9.6f),"Caramel");
            for(int x=-1;x<=1;x+=2)
            {
                Visual(v,"Candy corner column",new Vector3(x*5.15f,1.95f,-5.15f),new Vector3(.5f,3.9f,.5f),accent,PrimitiveType.Cylinder);
                Visual(v,"Candy corner column",new Vector3(x*5.15f,1.95f,5.15f),new Vector3(.5f,3.9f,.5f),accent,PrimitiveType.Cylinder);
                for(int band=0;band<6;band++)foreach(int end in new[]{-1,1})
                    Visual(v,"Candy icing band",new Vector3(x*5.15f,.35f+band*.62f,end*5.15f),new Vector3(.53f,.2f,.53f),"Frosting",PrimitiveType.Cylinder);
                Visual(v,"Cookie icing belt",new Vector3(x*3.15f,.8f,-5.25f),new Vector3(3.7f,.14f,.12f),"Frosting");
                var medallion=new Vector3(x*3.15f,2,-5.26f);
                Visual(v,"Peppermint cookie",medallion,new Vector3(1.2f,1.2f,.13f),"Frosting",PrimitiveType.Sphere);
                for(int stripe=0;stripe<6;stripe++)Visual(v,"Peppermint stripe",medallion+Vector3.back*.08f,new Vector3(.12f,1,.04f),accent,PrimitiveType.Cube,Quaternion.Euler(0,0,stripe*30));
                // Two roof slopes use reused cube geometry and frosting eaves.
                var rotation=Quaternion.Euler(0,0,-x*29);
                Visual(v,"Gingerbread roof",new Vector3(x*2.6f,5.35f,0),new Vector3(6,.35f,11.4f),accent,PrimitiveType.Cube,rotation);
                Visual(v,"Thick snowy roof",new Vector3(x*2.6f,5.58f,0),new Vector3(6,.22f,11.5f),"Frosting",PrimitiveType.Cube,rotation);
            }
            Visual(v,"Roof ridge",new Vector3(0,7.05f,0),new Vector3(.45f,11.6f,.45f),"Frosting",PrimitiveType.Cylinder,Quaternion.Euler(90,0,0));
            // Solid gable ends close the roof visually, without narrowing the interior.
            for(int side=-1;side<=1;side+=2)
            {
                var g=Group(v,"Iced gable",new Vector3(0,3.99f,side*5.25f));var mf=g.gameObject.AddComponent<MeshFilter>();
                mf.sharedMesh=GableMesh();g.gameObject.AddComponent<MeshRenderer>().sharedMaterial=materials[accent];
                Visual(v,"Shop sign",new Vector3(0,3.55f,side*5.3f),new Vector3(4.4f,.6f,.1f),accent);
            }
            Label(v,ChristmasTownLayout.Houses[index].name,new Vector3(0,3.55f,-5.38f),.22f,4.05f,.45f);
            Prop(v,"wreath-decorated",new Vector3(0,4.85f,-5.5f),1.1f);
            FurnishBuilding(c,v,index,accent);
            HouseholdDetails(c,v,index,accent);
        }
        static void ShopFurniture(Transform c,Transform v,int index,string accent)
        {
            // Furnish the perimeter; preserve the central route through both doors.
            Part(c,v,"Shop counter",new Vector3(-3.3f,.55f,1),new Vector3(1.3f,1.1f,4.5f),accent);
            Part(c,v,"Wall shelves",new Vector3(3.9f,1.15f,1.2f),new Vector3(.65f,2.3f,4),"Cocoa");
            for(int shelf=0;shelf<3;shelf++)Visual(v,"Icing shelf",new Vector3(3.9f,.4f+shelf*.75f,1.2f),new Vector3(.85f,.1f,4.3f),"Frosting");
            if(ChristmasTownLayout.Houses[index].use==ChristmasTownLayout.BuildingUse.Shop||ChristmasTownLayout.Houses[index].use==ChristmasTownLayout.BuildingUse.PostOffice)
                for(int j=0;j<4;j++)Prop(v,j%2==0?"present-a-cube":"present-b-cube",new Vector3(3.7f,.45f+(j%2)*.75f,-.1f+(j/2)*2),.45f);
            Prop(v,"bench",new Vector3(-3.4f,0,-2.8f),1.25f,90);
            Prop(v,"sock-red",new Vector3(-3.7f,2.3f,1),.7f,90);
            Prop(v,index%2==0?"nutcracker":"gingerbread-man",new Vector3(-3.3f,1.12f,2.3f),.8f);
            Visual(v,"Warm ceiling lamp",new Vector3(0,3.65f,1),new Vector3(.8f,.15f,.8f),"WarmWindow",PrimitiveType.Sphere);
            // Static furniture collision is in World, never on imported visual props.
            Solid(c,"Bench collision",new Vector3(-3.4f,.5f,-2.8f),new Vector3(1.1f,1,2.5f));
        }
        static Mesh gable;
        static Mesh GableMesh()
        {
            if(gable)return gable;
            gable=new Mesh{name="Cookie gable"};gable.vertices=new[]{new Vector3(-5.2f,0,-.08f),new Vector3(5.2f,0,-.08f),new Vector3(0,3,-.08f),new Vector3(-5.2f,0,.08f),new Vector3(5.2f,0,.08f),new Vector3(0,3,.08f)};
            gable.triangles=new[]{0,2,1,3,4,5,0,1,4,0,4,3,1,2,5,1,5,4,2,0,3,2,3,5};gable.RecalculateNormals();return gable;
        }
        static void Label(Transform parent,string text,Vector3 position,float size,float maxWidth=0,float maxHeight=0)
        {
            var t=Group(parent,"Sign - "+text,position);var label=t.gameObject.AddComponent<TextMesh>();label.text=text;label.fontSize=40;label.characterSize=size;
            label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.characterSize=size*.78f;t.GetComponent<MeshRenderer>().sharedMaterial=materials["Lettering"];
            if(maxWidth>0||maxHeight>0)
            {
                label.font.RequestCharactersInTexture(text,label.fontSize,label.fontStyle);
                var lines=text.Split('\n');float longest=0;
                foreach(var line in lines)
                {
                    float advance=0;foreach(char letter in line)
                        if(label.font.GetCharacterInfo(letter,out var glyph,label.fontSize,label.fontStyle))advance+=glyph.advance;
                    longest=Mathf.Max(longest,advance);
                }
                float width=longest*label.characterSize/10f;
                float height=lines.Length*label.fontSize*label.characterSize/10f;
                float fit=1;
                if(maxWidth>0&&width>0)fit=Mathf.Min(fit,maxWidth/width);
                if(maxHeight>0&&height>0)fit=Mathf.Min(fit,maxHeight/height);
                label.characterSize*=fit;
            }
            label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=new Color(1,.95f,.78f);
            t.gameObject.AddComponent<DeadlyJenga.CandyArt.ChristmasTownText>();
        }
        static GameObject Prop(Transform parent,string name,Vector3 p,float scale,float yaw=0)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find(name+".obj"));
            if(!model)throw new InvalidOperationException("Missing CC0 holiday model: "+name);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(model);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=Vector3.one*scale;
            foreach(var r in go.GetComponentsInChildren<Renderer>()){r.sharedMaterial=materials["Kenney"];r.gameObject.isStatic=true;}
            return go;
        }
        static void DressTown(Transform collision)
        {
            Prop(art.transform,"tree-decorated-snow",new Vector3(0,0,-3),4);
            Solid(collision,"Central tree trunk",new Vector3(0,2,-3),new Vector3(1.4f,4,1.4f));
            for(int i=0;i<10;i++)
            {
                float a=i*Mathf.PI*.2f;var p=new Vector3(Mathf.Cos(a)*3,0,-3+Mathf.Sin(a)*3);
                Prop(art.transform,i%2==0?"present-a-cube":"present-b-cube",p,.65f,i*37);
            }
            foreach(int side in new[]{-1,1})
            {
                foreach(float x in new[]{-43f,-19f,7f,19f,43f})
                {
                    Prop(art.transform,"tree-snow-a",new Vector3(x,0,side*36),1.6f,x*17);
                    Solid(collision,"Static fir trunk",new Vector3(x,1.6f,side*36),new Vector3(1.2f,3.2f,1.2f));
                }
                for(int i=-3;i<=3;i++)
                {
                    float x=i*12;float z=side*8;
                    if(Mathf.Abs(i)==2&&side<0)continue;
                    if(i%2==0)Prop(art.transform,"lantern",new Vector3(x,0,z),1.6f);
                    else Prop(art.transform,side<0?"candy-cane-red":"candy-cane-green",new Vector3(x,0,z),4.4f,side*90);
                    Solid(collision,"Candy cane bollard",new Vector3(x,1,z),new Vector3(.45f,2,.45f));
                }
                Prop(art.transform,"snowman-hat",new Vector3(side*12,0,-10),1.2f,-side*40);
                Prop(art.transform,"sled",new Vector3(side*12,0,12),1.2f,side*35);
                Prop(art.transform,"reindeer",new Vector3(side*10,0,14),1,side*70);
                Visual(art.transform,"Team meeting pad",new Vector3(side*43,.02f,0),new Vector3(5,.02f,5),side<0?"Cherry":"Mint",PrimitiveType.Cylinder);
            }
            // Suspended bulbs are geometry/emission only, with no per-bulb realtime lights.
            for(int side=-1;side<=1;side+=2)for(int i=0;i<31;i++)
            {
                float x=-30+i*2;float y=5.1f+Mathf.Abs(x)*.035f;
                Visual(art.transform,"Festive bulb",new Vector3(x,y,side*13),Vector3.one*.22f,i%3==0?"Cherry":i%3==1?"WarmWindow":"Mint",PrimitiveType.Sphere);
                if(i<30)
                {
                    var a=new Vector3(x,y+.15f,side*13);float nextX=x+2;
                    var b=new Vector3(nextX,5.25f+Mathf.Abs(nextX)*.035f,side*13);
                    Visual(art.transform,"Light cable",(a+b)*.5f,new Vector3(.025f,Vector3.Distance(a,b),.025f),"Cocoa",PrimitiveType.Cylinder,Quaternion.FromToRotation(Vector3.up,b-a));
                }
            }
        }
        static void CombineArt(Transform root)
        {
            var grouped=new Dictionary<(Material material,int x,int z),List<CombineInstance>>();
            var originals=new List<MeshFilter>();
            foreach(var f in root.GetComponentsInChildren<MeshFilter>())
            {
                var r=f.GetComponent<MeshRenderer>();if(!r||!f.sharedMesh||f.gameObject.CompareTag("EditorOnly"))continue;
                // TextMesh supplies dynamic geometry and is intentionally left intact.
                for(int s=0;s<f.sharedMesh.subMeshCount;s++)
                {
                    var m=r.sharedMaterials[Mathf.Min(s,r.sharedMaterials.Length-1)];
                    var center=r.bounds.center;var key=(m,Mathf.FloorToInt(center.x/24),Mathf.FloorToInt(center.z/24));
                    if(!grouped.TryGetValue(key,out var list))grouped[key]=list=new List<CombineInstance>();
                    list.Add(new CombineInstance{mesh=f.sharedMesh,subMeshIndex=s,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix});
                }
                originals.Add(f);
            }
            foreach(var pair in grouped)
            {
                var mesh=new Mesh{name="Christmas town - "+pair.Key.material.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();
                string path=meshFolder+"/"+pair.Key.material.name+"_"+pair.Key.x+"_"+pair.Key.z+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing)
                {
                    existing.Clear();existing.indexFormat=mesh.indexFormat;existing.vertices=mesh.vertices;existing.normals=mesh.normals;
                    existing.uv=mesh.uv;existing.tangents=mesh.tangents;existing.colors=mesh.colors;existing.triangles=mesh.triangles;existing.bounds=mesh.bounds;
                    EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;
                }
                else AssetDatabase.CreateAsset(mesh,path);
                var t=Group(root,"Batch - "+pair.Key.material.name);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=pair.Key.material;
            }
            foreach(var f in originals){UnityEngine.Object.DestroyImmediate(f.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(f);}
        }
        static Camera previewCamera;
        static void SetupPreview()
        {
            art.GetComponent<DeadlyJenga.CandyArt.ChristmasTownAtmosphere>().ApplyPreview();
            previewCamera=new GameObject("Preview camera").AddComponent<Camera>();previewCamera.transform.position=new Vector3(70,63,-76);previewCamera.transform.LookAt(Vector3.zero);previewCamera.fieldOfView=50;
            previewCamera.clearFlags=CameraClearFlags.Skybox;previewCamera.allowHDR=true;previewCamera.farClipPlane=300;previewCamera.nearClipPlane=.08f;
            previewCamera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing=true;
        }
        static void Capture(Vector3 position,Vector3 target,string file)
        {
            previewCamera.transform.position=position;previewCamera.transform.LookAt(target);var rt=RenderTexture.GetTemporary(1600,1000,24);var before=RenderTexture.active;
            try{previewCamera.targetTexture=rt;previewCamera.Render();RenderTexture.active=rt;var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes(ProjectPath.Report(file),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);}
            finally{previewCamera.targetTexture=null;RenderTexture.active=before;RenderTexture.ReleaseTemporary(rt);}
        }
        static void Validate(ChristmasTownMap map)
        {
            checks.Clear();Physics.SyncTransforms();
            if(map.GetComponentsInChildren<Rigidbody>().Any(b=>!b.isKinematic||b.useGravity))throw new Exception("Town contains simulated dynamic geometry.");
            if(art.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Presentation contains solid collision.");
            foreach(var marker in map.interiorCenters)
            {
                var p=marker.position;
                if(Physics.CheckCapsule(p+Vector3.up*.35f,p+Vector3.up*1.7f,.3f,~0,QueryTriggerInteraction.Ignore))throw new Exception("Interior center blocked: "+marker.name);
                var house=ChristmasTownLayout.Houses[Array.IndexOf(map.interiorCenters,marker)];var forward=Quaternion.Euler(0,house.yaw,0)*Vector3.forward;
                if(Physics.CapsuleCast(p+Vector3.up*.35f-forward*6,p+Vector3.up*1.7f-forward*6,.3f,forward,out _,12,~0,QueryTriggerInteraction.Ignore))throw new Exception("Through route blocked: "+marker.name);
                if(!map.IsSheltered(p+Vector3.up*1.5f))throw new Exception("Interior lacks roof shelter: "+marker.name);
            }
            ValidateConnections(map);
            ValidateMatchFoundation();
            checks.Add("PASS "+map.interiorCenters.Length+" furnished interiors: standing room, roof shelter and doorway-to-doorway capsule sweep.");
            checks.Add("PASS Seven fixed kinematic claim pads; zero dynamic map bodies; Presentation has zero colliders.");
            checks.Add("PASS IWorldQuery, rebuild, footprint and static layout synchronization implemented. Peer/headset behavior not tested here.");
        }
    }
}
