using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static class BreakwaterVillageBuilder
    {
        public const string MapId="breakwater-village";
        static readonly List<VillagePiece> pieces=new List<VillagePiece>();
        static Transform root, neighborhood;
        static string artFolder;
        static Mesh rounded;
        static Material timber,cream,teal,purple,coral,yellow,green,dark,glass,metal;
        static readonly Dictionary<string,Material> paints=new Dictionary<string,Material>();
        static System.Random random;
        [MenuItem("Tools/Deadly Jenga/Maps/Build Breakwater Physics Village")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            if(Application.isBatchMode&&string.IsNullOrEmpty(SceneManager.GetActiveScene().path))EditorSceneManager.OpenScene(ProjectPath.Find("Andy.unity"));
            var previousWorld=Services.World;var active=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                random=new System.Random(441);pieces.Clear();paints.Clear();
                var presentation=Path.GetDirectoryName(ProjectPath.FolderOf("VillageFlatScenery.cs").TrimEnd('/')).Replace('\\','/');
                artFolder=Folder(presentation,"BreakwaterVillage");Folder(artFolder,"Materials");Folder(artFolder,"Meshes");
                rounded=AssetDatabase.LoadAssetAtPath<Mesh>(artFolder+"/Meshes/Soft candy box.asset");
                if(!rounded){rounded=RoundBox();AssetDatabase.CreateAsset(rounded,artFolder+"/Meshes/Soft candy box.asset");}
                timber=Paint("Honey timber",new Color(.48f,.24f,.09f));cream=Paint("Vanilla trim",new Color(1,.88f,.61f));
                teal=Paint("Lagoon mint",new Color(.07f,.72f,.67f));purple=Paint("Mulberry inn",new Color(.54f,.25f,.8f));
                coral=Paint("Coral pink",new Color(.95f,.26f,.3f));yellow=Paint("Mango",new Color(1,.67f,.08f));
                green=Paint("Palm leaves",new Color(.13f,.58f,.22f));dark=Paint("Espresso",new Color(.12f,.08f,.07f));
                glass=Paint("Opaque sky glass",new Color(.19f,.55f,.7f));metal=Paint("Copper fixtures",new Color(.45f,.27f,.12f),.65f);
                root=new GameObject("Breakwater Village - connected physical community").transform;
                var map=root.gameObject.AddComponent<BreakwaterVillageMap>();root.gameObject.AddComponent<VillageFlatScenery>();
                var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Flat sand foundation";ground.transform.SetParent(root,false);
                ground.transform.localPosition=new Vector3(0,-.35f,0);ground.transform.localScale=new Vector3(74,.7f,70);
                ground.layer=0;ground.GetComponent<Renderer>().sharedMaterial=Sand();
                var arrival=new GameObject("Arrival - safe street entrance").transform;arrival.SetParent(root,false);arrival.localPosition=new Vector3(-1,.15f,-26);map.arrival=arrival;
                House("THE LANTERN INN",new Vector3(-13,0,11),10,12,3,8,purple,true);
                House("MANGO HOUSE",new Vector3(13,0,12),8,8,2,-13,coral,false);
                House("LAGOON COTTAGE",new Vector3(18,0,-8),8,7,2,-28,teal,true);
                House("THE BUTTON ARCADE",new Vector3(-16,0,-9),9,8,2,19,teal,false);
                House("BOAT & BOARD",new Vector3(3,0,23),8,6,1,174,yellow,true);
                House("THE FIXERY",new Vector3(-27,0,17),6,6,1,83,coral,false);
                CommunityConnections();
                Pavilion(new Vector3(0,0,-3));
                StreetLife();
                foreach(var piece in pieces)if(piece.GetComponents<BoxCollider>().Length>1)piece.gameObject.AddComponent<VillageExtraSurfaces>();
                map.pieces=pieces.ToArray();
                var maps=Path.GetDirectoryName(ProjectPath.FolderOf("BreakwaterVillageMap.cs").TrimEnd('/')).Replace('\\','/');
                string prefabFolder=Folder(Folder(maps,"Resources"),"ModeMaps");
                string path=prefabFolder+"/Mode_breakwater-village.prefab";
                PrefabUtility.SaveAsPrefabAsset(root.gameObject,path);
                var catalog=Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog");
                var list=catalog.maps.ToList();var entry=list.Find(m=>m.id==MapId);
                if(entry==null){entry=new IslandLobbyCatalog.Map{id=MapId};list.Add(entry);}
                entry.title="Breakwater Village";entry.mapPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);entry.scenePath=ProjectPath.Find("Game.unity");
                entry.recommendedMode="demo";entry.recommendedPlayers=12;entry.walkableIsland=true;
                catalog.maps=list.ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
                File.WriteAllText(ProjectPath.Report("BreakwaterBuild.txt"),"Breakwater Village\nBodies="+pieces.Count+" structural="+pieces.Count(p=>p.structural)+" connections="+pieces.Sum(p=>p.connections.Length)+"\n"+
                    string.Join("\n",pieces.GroupBy(p=>p.building).Select(g=>g.Key+": "+g.Count()+" bodies"))+"\nFlat sand; native dynamic bodies and finite breakable FixedJoints. No Core changes.\n");
            }
            finally{EditorSceneManager.CloseScene(scene,true);if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);Services.World=previousWorld;}
        }
        static string Folder(string parent,string name){string p=parent+"/"+name;if(!AssetDatabase.IsValidFolder(p))AssetDatabase.CreateFolder(parent,name);return p;}
        static Material Paint(string name,Color color,float metallic=0)
        {
            string path=artFolder+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};AssetDatabase.CreateAsset(m,path);}
            m.color=color;m.SetFloat("_Smoothness",.55f);m.SetFloat("_Metallic",metallic);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
        }
        static Material Sand()
        {
            string folder=Folder(artFolder,"Textures");string path=folder+"/Fine sand grain.asset";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(!texture)
            {
                texture=new Texture2D(128,128,TextureFormat.RGB24,true){name="Fine sand grain",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear,anisoLevel=4};
                var colors=new Color[128*128];var grain=new System.Random(2718);
                for(int y=0;y<128;y++)for(int x=0;x<128;x++){float n=(Mathf.PerlinNoise(x*.13f,y*.13f)-.5f)*.07f+((float)grain.NextDouble()-.5f)*.055f;colors[y*128+x]=new Color(.82f+n,.69f+n,.44f+n);}
                texture.SetPixels(colors);texture.Apply();AssetDatabase.CreateAsset(texture,path);
            }
            var material=Paint("Flat textured sand",Color.white);material.mainTexture=texture;material.mainTextureScale=new Vector2(26,26);material.SetFloat("_Smoothness",.08f);EditorUtility.SetDirty(material);return material;
        }
        static Transform Group(string name,Vector3 position,float yaw=0)
        {var t=new GameObject(name).transform;t.SetParent(root,false);t.localPosition=position;t.localRotation=Quaternion.Euler(0,yaw,0);return t;}
        static VillagePiece Part(string name,Vector3 position,Vector3 size,Material material,bool structural=true,float mass=45,Quaternion? rotation=null)
        {
            var go=new GameObject(name);go.transform.SetParent(neighborhood,false);go.transform.localPosition=position;go.transform.localRotation=rotation??Quaternion.identity;
            go.layer=Layers.Block;var box=go.AddComponent<BoxCollider>();box.size=size;
            var body=go.AddComponent<Rigidbody>();body.mass=mass;body.solverIterations=16;body.solverVelocityIterations=6;
            body.linearDamping=.07f;body.angularDamping=.12f;body.maxAngularVelocity=10;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            var block=go.AddComponent<CastleBlock>();var visual=new GameObject("Visual").transform;visual.SetParent(go.transform,false);block.visualRoot=visual;
            Art(visual,name,Vector3.zero,size,material);
            var p=go.AddComponent<VillagePiece>();p.building=neighborhood.name;p.purpose=name;p.structural=structural;p.durability=structural?12:5;
            p.homePosition=go.transform.localPosition;p.homeRotation=go.transform.localRotation;pieces.Add(p);return p;
        }
        static Transform Art(Transform parent,string name,Vector3 position,Vector3 size,Material material,Quaternion? rotation=null,PrimitiveType type=PrimitiveType.Cube)
        {
            GameObject go;
            if(type==PrimitiveType.Cube){go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.GetComponent<MeshFilter>().sharedMesh=rounded;}
            else{go=GameObject.CreatePrimitive(type);Object.DestroyImmediate(go.GetComponent<Collider>());}
            go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation??Quaternion.identity;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;
        }
        static Transform Visual(VillagePiece p)=>p.transform.Find("Visual");
        static void Join(VillagePiece piece,VillagePiece target,Vector3 localAnchor,float force=10000,float torque=14000)
        {var list=piece.connections.ToList();list.Add(new VillageConnection{target=target,anchor=localAnchor,force=force,torque=torque});piece.connections=list.ToArray();}
        static void Label(VillagePiece p,string text,Vector3 local,float size=.36f)
        {
            var go=new GameObject("Sign lettering");go.transform.SetParent(Visual(p),false);go.transform.localPosition=local;
            var t=go.AddComponent<TextMesh>();t.text=text;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=64;t.characterSize=size*.25f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=new Color(1,.94f,.75f);
            string path=artFolder+"/Materials/Depth-tested lettering.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("DeadlyJenga/Village Lettering")){name="Depth-tested lettering"};AssetDatabase.CreateAsset(material,path);}
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=t.font.material;
            go.AddComponent<VillageSignLettering>().template=material;
            var bounds=renderer.localBounds;var shape=p.GetComponent<BoxCollider>().size;
            float fit=Mathf.Min(1,Mathf.Min(shape.x*.88f/Mathf.Max(.01f,bounds.size.x),shape.y*.8f/Mathf.Max(.01f,bounds.size.y)));
            go.transform.localScale=Vector3.one*fit;
        }
        static void House(string name,Vector3 position,float w,float d,int stories,float yaw,Material color,bool pitch)
        {
            neighborhood=Group(name,position,yaw);VillagePiece[] previousPosts=null;VillagePiece[] previousFloor=null;VillagePiece signSupport=null;
            for(int level=0;level<stories;level++)
            {
                float y=level*3.2f;var floor=new VillagePiece[4];var posts=new VillagePiece[4];
                for(int i=0;i<4;i++)
                {
                    float x=(i%2==0?-1:1)*w*.25f,z=(i<2?-1:1)*d*.25f;
                    floor[i]=Part("Floor bay "+level+"-"+i,new Vector3(x,y+.18f,z),new Vector3(w*.5f-.02f,.28f,d*.5f-.02f),timber,true,110);
                    Join(floor[i],level==0?null:previousPosts[i],level==0?Vector3.down*.14f:new Vector3((i%2==0?-1:1)*(w*.25f-.19f),-.14f,(i<2?-1:1)*(d*.25f-.19f)),level==0?35000:20000,level==0?100000:60000);
                    if(level>0&&i%2==1)Join(floor[i],floor[i-1],Vector3.left*w*.25f,20000,60000);
                    if(level>0&&i>=2)Join(floor[i],floor[i-2],Vector3.back*d*.25f,20000,60000);
                    for(int plank=0;plank<5;plank++)Art(Visual(floor[i]),"Floorboard seam",new Vector3(0,.146f,(plank-2)*d*.09f),new Vector3(w*.47f,.009f,.016f),dark);
                }
                for(int i=0;i<4;i++)
                {
                    float x=(i%2==0?-1:1)*(w*.5f-.19f),z=(i<2?-1:1)*(d*.5f-.19f);
                    posts[i]=Part("Load-bearing post "+level+"-"+i,new Vector3(x,y+1.78f,z),new Vector3(.38f,2.9f,.38f),cream,true,35);
                    Join(posts[i],floor[i],new Vector3(0,-1.45f,0),9000*(stories-level),25000*(stories-level));
                    Art(Visual(posts[i]),"Copper base shoe",new Vector3(0,-1.29f,0),new Vector3(.43f,.21f,.43f),metal);
                }
                // Window openings remain open between the sill, jambs and header.
                for(int side=0;side<3;side++)
                {
                        bool back=side==0;float span=back?w:d;int bays=Mathf.Max(2,Mathf.RoundToInt(span/3));
                    for(int bay=0;bay<bays;bay++)
                    {
                        float innerSpan=span-.8f;float along=(bay+.5f)*innerSpan/bays-innerSpan*.5f;float width=innerSpan/bays-.04f;
                        Vector3 Base(float height)=>back?new Vector3(along,y+height,d*.5f-.17f):new Vector3((side==1?-1:1)*(w*.5f-.17f),y+height,along);
                        Quaternion rot=Quaternion.Euler(0,back?0:90,0);
                        if(name=="THE LANTERN INN"&&level==1&&side==1&&bay==bays-1)
                        {
                            var doorway=Part("Upper community doorway",Base(2.91f),new Vector3(width,.45f,.3f),color,true,65,rot);
                            Join(doorway,floor[2],new Vector3(-width*.44f,-2.59f,0));
                            for(int sideJamb=-1;sideJamb<=1;sideJamb+=2){var shape=doorway.gameObject.AddComponent<BoxCollider>();shape.center=new Vector3(sideJamb*width*.44f,-1.3f,0);shape.size=new Vector3(.24f,2.6f,.32f);Art(Visual(doorway),"Doorway jamb",shape.center,shape.size,cream);}
                            continue;
                        }
                        var sillPosition=Base(.85f);int bearing=(sillPosition.x>=0?1:0)+(sillPosition.z>=0?2:0);
                        var sill=Part("Window bay "+level+"-"+side+"-"+bay,sillPosition,new Vector3(width,1.02f,.28f),color,true,65,rot);Join(sill,floor[bearing],new Vector3(0,-.5f,0));
                        var header=sill.gameObject.AddComponent<BoxCollider>();header.center=new Vector3(0,2.06f,0);header.size=new Vector3(width,.45f,.3f);
                        var jamb=sill.gameObject.AddComponent<BoxCollider>();jamb.center=new Vector3(width*.44f,1.17f,0);jamb.size=new Vector3(.24f,1.3f,.32f);
                        Art(Visual(sill),"Window header",header.center,header.size,color);
                        Art(Visual(sill),"Window jamb",jamb.center,jamb.size,cream);
                        var shutter=Part("Shutter "+level+"-"+side+"-"+bay,Base(2.06f)-(rot*Vector3.right)*(width*.39f)-(rot*Vector3.forward)*.08f,new Vector3(.35f,1.2f,.11f),yellow,false,3,rot);Join(shutter,sill,Vector3.down*.55f,480,500);
                        for(int slat=0;slat<5;slat++)Art(Visual(shutter),"Shutter louver",new Vector3(0,(slat-2)*.19f,-.07f),new Vector3(.34f,.055f,.06f),cream);
                    }
                }
                var beam=Part("Front lintel "+level,new Vector3(0,y+2.98f,-d*.5f+.1f),new Vector3(w,.42f,.4f),timber,true,70);
                Join(beam,posts[0],Vector3.left*(w*.5f-.2f),10000,18000);Join(beam,posts[1],Vector3.right*(w*.5f-.2f),10000,18000);
                if(level==0)signSupport=beam;
                if(name.Contains("HOUSE")||name.Contains("COTTAGE"))
                {
                    for(int side=-1;side<=1;side+=2)
                    {
                        var facade=Part("Front window wall "+level+"-"+side,new Vector3(side*w*.3f,y+.87f,-d*.5f+.13f),new Vector3(w*.25f,1.05f,.25f),color,true,30);
                        Join(facade,floor[side<0?0:1],Vector3.down*.52f);
                        Art(Visual(facade),"Window flower box",new Vector3(0,.6f,-.25f),new Vector3(w*.25f,.22f,.38f),cream);
                        for(int leaf=0;leaf<5;leaf++)Art(Visual(facade),"Window flowers",new Vector3((leaf-2)*.25f,.8f,-.25f),new Vector3(.17f,.3f,.17f),leaf%2==0?yellow:coral,Quaternion.identity,PrimitiveType.Sphere);
                    }
                }
                if(level>0)
                {
                    var balcony=Part("Balcony slab "+level,new Vector3(0,y+.18f,-d*.5f-1),new Vector3(w,.24f,2),timber,true,100);
                    Join(balcony,floor[0],new Vector3(-w*.25f,0,.9f),5500,35000);Join(balcony,floor[1],new Vector3(w*.25f,0,.9f),5500,35000);
                    var rail=Part("Balcony rail "+level,new Vector3(0,y+1.1f,-d*.5f-1.9f),new Vector3(w,.15f,.15f),cream,false,12);
                    Join(rail,balcony,new Vector3(0,-.9f,0),1500,1800);
                    for(int r=0;r<=8;r++)Art(Visual(rail),"Baluster",new Vector3((r/8f-.5f)*(w-.2f),-.43f,0),new Vector3(.09f,.8f,.09f),color);
                    Stair(w*.5f+1.2f,y,d,floor[3],previousFloor[1],level);
                }
                Furniture(floor[0],new Vector3(-w*.18f,y+.36f,-d*.18f),level,color);
                if(!(level==0&&(name.Contains("ARCADE")||name.Contains("FIXERY"))))Sofa(floor[3],new Vector3(w*.22f,y+.36f,d*.22f),color);
                if(level==1&&(name.Contains("HOUSE")||name.Contains("COTTAGE")))
                {
                    var bed=Part("Loose guest bed",new Vector3(-w*.23f,y+.65f,d*.28f),new Vector3(1.7f,.65f,2),cream,false,35);
                    Art(Visual(bed),"Quilt",new Vector3(0,.35f,-.2f),new Vector3(1.68f,.12f,1.5f),color);
                    Art(Visual(bed),"Pillow",new Vector3(0,.4f,.66f),new Vector3(1.2f,.2f,.38f),cream);
                }
                Plant(new Vector3(-w*.37f,y+.36f,d*.36f));
                Lamp(beam,new Vector3(0,y+2.45f,-d*.5f+.3f));
                previousPosts=posts;previousFloor=floor;
            }
            float roof=stories*3.2f+.12f;
            var roofPieces=new VillagePiece[2];
            for(int side=0;side<2;side++)
            {
                var roofPart=Part("Roof section "+side,new Vector3((side==0?-1:1)*w*.25f,roof+(pitch?.6f:0),0),new Vector3(w*.5f-.04f,.24f,d+.8f),pitch?coral:color,true,100,Quaternion.Euler(0,0,pitch?(side==0?14:-14):0));
                roofPieces[side]=roofPart;
                Join(roofPart,previousPosts[side],roofPart.transform.InverseTransformPoint(previousPosts[side].transform.TransformPoint(Vector3.up*1.45f)),10000,15000);
                Join(roofPart,previousPosts[side+2],roofPart.transform.InverseTransformPoint(previousPosts[side+2].transform.TransformPoint(Vector3.up*1.45f)),10000,15000);
                for(int line=0;line<7;line++)Art(Visual(roofPart),"Roof panel ridge",new Vector3(0,.14f,(line-3)*d/7),new Vector3(w*.52f,.06f,.07f),cream);
            }
            var sign=Part("Community shop sign",new Vector3(0,2.35f,-d*.5f-.18f),new Vector3(w*.75f,.75f,.15f),dark,false,7);
            Join(sign,signSupport,Vector3.up*.35f,900,950);Label(sign,name,new Vector3(0,0,-.09f),name.Length>15?.18f:.24f);
            for(int stripe=0;stripe<8;stripe++)
            {
                var awning=Part("Striped awning "+stripe,new Vector3((stripe-3.5f)*w/8,2.8f,-d*.5f-.8f),new Vector3(w/8,.12f,1.6f),stripe%2==0?cream:color,false,4,Quaternion.Euler(-9,0,0));
                Join(awning,signSupport,Vector3.forward*.7f,1200,3000);
            }
            EntrySteps(w,d);
            if(name=="THE FIXERY")Stair(w*.5f+1.2f,3.2f,d,roofPieces[1],previousFloor[1],1);
            if(name.Contains("ARCADE"))ArcadeCabinets(d);
            if(name.Contains("BOARD"))
                for(int i=0;i<3;i++)
                {
                    var board=Part("Loose surfboard "+i,new Vector3((i-1)*1.6f,1.55f,d*.5f-.6f),new Vector3(.55f,2.4f,.16f),i%2==0?teal:coral,false,7,Quaternion.Euler(-9,0,7));
                    Art(Visual(board),"Board stripe",new Vector3(0,0,-.09f),new Vector3(.08f,2.2f,.02f),cream);
                }
            if(name.Contains("FIXERY"))
            {
                var work=Part("Loose repair workbench",new Vector3(0,1.1f,d*.25f),new Vector3(3,.2f,1),timber,false,35);
                Art(Visual(work),"Tool cabinet",Vector3.down*.4f,new Vector3(2.7f,.75f,.8f),teal);
                var baseShape=work.gameObject.AddComponent<BoxCollider>();baseShape.center=Vector3.down*.4f;baseShape.size=new Vector3(2.7f,.75f,.8f);
                for(int tool=0;tool<6;tool++)Art(Visual(work),"Mounted workshop tool",new Vector3((tool-2.5f)*.35f,.23f,.1f),new Vector3(.11f,.1f,.5f),metal,Quaternion.Euler(0,tool*15,0));
            }
        }
        static void EntrySteps(float w,float d)
        {
            var step=Part("Front door step",new Vector3(0,.09f,-d*.5f-.4f),new Vector3(2.6f,.18f,.7f),cream,true,30);Join(step,null,Vector3.down*.09f,22000,22000);
        }
        static void Stair(float x,float top,float depth,VillagePiece upper,VillagePiece lower,int level)
        {
            float run=5.8f;float angle=Mathf.Atan2(3.2f,run)*Mathf.Rad2Deg;
            var stairs=Part("Outside stair flight "+level,new Vector3(x,top-1.42f,0),new Vector3(1.8f,.24f,Mathf.Sqrt(run*run+3.2f*3.2f)),timber,true,85,Quaternion.Euler(-angle,0,0));
            Join(stairs,upper,Vector3.forward*3.1f,14000,50000);Join(stairs,lower,Vector3.back*3.1f,14000,50000);
            for(int i=0;i<14;i++)Art(Visual(stairs),"Stair tread",new Vector3(0,.18f,(i-6.5f)*.45f),new Vector3(1.85f,.12f,.42f),cream);
            var landing=Part("Side stair landing "+level,new Vector3(x-.2f,top+.18f,run*.5f),new Vector3(2,.24f,1.5f),timber,true,40);Join(landing,upper,Vector3.left,16000,24000);
            Join(landing,stairs,Vector3.zero,16000,24000);
        }
        static void Furniture(VillagePiece floor,Vector3 p,int level,Material color)
        {
            var table=Part("Loose cafe table "+level,p+Vector3.up*.72f,new Vector3(1.45f,.14f,.9f),timber,false,15);
            Art(Visual(table),"Table pedestal",new Vector3(0,-.35f,0),new Vector3(.22f,.65f,.22f),cream);
            var leg=table.gameObject.AddComponent<BoxCollider>();leg.center=new Vector3(0,-.35f,0);leg.size=new Vector3(.3f,.66f,.3f);
            for(int i=0;i<2;i++)
            {
                var chair=Part("Loose cafe chair "+level+"-"+i,p+new Vector3(i==0?-1.13f:1.13f,.45f,0),new Vector3(.6f,.14f,.6f),color,false,5);
                Art(Visual(chair),"Chair back",new Vector3(i==0?-.25f:.25f,.34f,0),new Vector3(.12f,.6f,.6f),cream);
                for(int a=-1;a<=1;a+=2)for(int b=-1;b<=1;b+=2)Art(Visual(chair),"Chair leg",new Vector3(a*.22f,-.23f,b*.22f),new Vector3(.09f,.4f,.09f),timber);
                var feet=chair.gameObject.AddComponent<BoxCollider>();feet.center=Vector3.down*.22f;feet.size=new Vector3(.49f,.43f,.49f);
            }
            var mug=Part("Loose turquoise cup "+level,p+new Vector3(.3f,.9f,.12f),new Vector3(.18f,.22f,.18f),teal,false,.35f);
            Art(Visual(mug),"Cup handle",new Vector3(.12f,0,0),new Vector3(.1f,.14f,.08f),cream);
            Art(Visual(mug),"Coffee surface",new Vector3(0,.115f,0),new Vector3(.13f,.009f,.13f),dark);
            Art(Visual(table),"Board game",new Vector3(-.28f,.081f,-.03f),new Vector3(.5f,.015f,.38f),purple);
            for(int token=0;token<6;token++)Art(Visual(table),"Game token",new Vector3(-.45f+token*.065f,.1f,.02f),new Vector3(.035f,.035f,.035f),token%2==0?coral:yellow);
        }
        static void Sofa(VillagePiece floor,Vector3 p,Material color)
        {
            var sofa=Part("Loose lounge sofa",p+Vector3.up*.4f,new Vector3(2.1f,.65f,.9f),color,false,24);
            Art(Visual(sofa),"Sofa back",new Vector3(0,.4f,.36f),new Vector3(2.1f,.65f,.22f),color);
            Art(Visual(sofa),"Sofa cushion",new Vector3(-.5f,.37f,-.05f),new Vector3(.85f,.14f,.6f),cream);
            Art(Visual(sofa),"Sofa cushion",new Vector3(.5f,.37f,-.05f),new Vector3(.85f,.14f,.6f),cream);
            var back=sofa.gameObject.AddComponent<BoxCollider>();back.center=new Vector3(0,.4f,.36f);back.size=new Vector3(2.1f,.65f,.22f);
        }
        static void Plant(Vector3 p)
        {
            var pot=Part("Loose planted pot",p+Vector3.up*.3f,new Vector3(.55f,.6f,.55f),coral,false,8);
            Art(Visual(pot),"Pot rim",new Vector3(0,.28f,0),new Vector3(.64f,.12f,.64f),cream);
            for(int i=0;i<5;i++)Art(Visual(pot),"Sculpted leaf",new Vector3(Mathf.Sin(i*1.26f)*.15f,.63f,Mathf.Cos(i*1.26f)*.15f),new Vector3(.13f,.85f,.33f),green,Quaternion.Euler(15,i*72,25),PrimitiveType.Capsule);
        }
        static void Lamp(VillagePiece beam,Vector3 p)
        {
            var lamp=Part("Mounted lantern",p,new Vector3(.35f,.48f,.35f),yellow,false,2);Join(lamp,beam,Vector3.up*.25f,350,420);
            Art(Visual(lamp),"Lantern cap",Vector3.up*.25f,new Vector3(.44f,.08f,.44f),metal);
            Art(Visual(lamp),"Lantern base",Vector3.down*.25f,new Vector3(.4f,.06f,.4f),metal);
        }
        static void Pavilion(Vector3 position)
        {
            neighborhood=Group("THE COMMON TABLE",position,5);var floor=Part("Shared bar floor",new Vector3(0,.14f,0),new Vector3(10,.28f,7),timber,true,140);Join(floor,null,Vector3.down*.14f,50000,60000);
            var posts=new List<VillagePiece>();
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
            {var p=Part("Bar roof post",new Vector3(x*4.5f,1.95f,z*3),new Vector3(.4f,3.35f,.4f),cream,true,40);Join(p,floor,Vector3.down*1.67f,8000,9000);posts.Add(p);}
            for(int i=0;i<2;i++)
            {var roof=Part("Pavilion roof half "+i,new Vector3(i==0?-2.65f:2.65f,4.1f,0),new Vector3(5.25f,.28f,8),teal,true,110,Quaternion.Euler(0,0,i==0?10:-10));Join(roof,posts[i*2],Vector3.back*3,10000,16000);Join(roof,posts[i*2+1],Vector3.forward*3,10000,16000);}
            for(int side=-1;side<=1;side+=2)
            {
                var counter=Part("Bar counter "+side,new Vector3(side*2.9f,1.19f,0),new Vector3(.85f,.2f,4.5f),coral,false,45);Join(counter,floor,Vector3.down,4500,6000);
                Art(Visual(counter),"Bar cabinet",Vector3.down*.47f,new Vector3(.65f,.85f,4.2f),timber);
                var cabinet=counter.gameObject.AddComponent<BoxCollider>();cabinet.center=Vector3.down*.47f;cabinet.size=new Vector3(.65f,.85f,4.2f);
                for(int i=0;i<3;i++){var stool=Part("Loose bar stool",new Vector3(side*4.05f,.9f,(i-1)*1.4f),new Vector3(.6f,.16f,.6f),yellow,false,5);Art(Visual(stool),"Stool stem",Vector3.down*.3f,new Vector3(.19f,.58f,.19f),metal);var stem=stool.gameObject.AddComponent<BoxCollider>();stem.center=Vector3.down*.3f;stem.size=new Vector3(.4f,.6f,.4f);}
            }
            var sign=Part("Bar community sign",new Vector3(0,3.35f,-3.9f),new Vector3(6,.65f,.18f),purple,false,10);Join(sign,posts[0],Vector3.left*2.5f,1600,2200);Label(sign,"THE COMMON TABLE",new Vector3(0,0,-.11f),.22f);
            Furniture(floor,new Vector3(0,.3f,0),0,teal);Lamp(posts[0],new Vector3(-4.3f,3.15f,-3));
        }
        static void StreetLife()
        {
            neighborhood=Group("Village lanes and gardens",Vector3.zero);
            // Offset paving follows the open main street rather than an orthogonal housing grid.
            for(int i=0;i<16;i++)
            {
                float z=-25+i*3;float x=Mathf.Sin(i*.35f)*2.5f;
                if(z>-9&&z<2)x=7;
                var paving=Part("Loose stepping stone "+i,new Vector3(x,.045f,z),new Vector3(1.25f,.09f,.8f),cream,false,12);
                paving.Body.linearDamping=.2f;
            }
            for(int i=0;i<5;i++)
            {
                Vector3 p=new Vector3(i%2==0?-8:8,.22f,-17+i*8);
                if(i==3)p=new Vector3(4,.22f,8);
                if(i==4)p=new Vector3(-2,.22f,15);
                var bench=Part("Street bench "+i,p+Vector3.up*.3f,new Vector3(2,.18f,.7f),i%2==0?teal:coral,false,18);
                for(int s=-1;s<=1;s+=2){Art(Visual(bench),"Bench foot",new Vector3(s*.75f,-.2f,0),new Vector3(.2f,.4f,.65f),timber);var leg=bench.gameObject.AddComponent<BoxCollider>();leg.center=new Vector3(s*.75f,-.2f,0);leg.size=new Vector3(.2f,.4f,.65f);}
                Art(Visual(bench),"Bench back",new Vector3(0,.3f,.3f),new Vector3(2,.55f,.12f),cream);Plant(p+new Vector3(1.8f,-.22f,0));
            }
            for(int i=0;i<6;i++)
            {
                var crate=Part("Loose delivery crate "+i,new Vector3(-25+(i%3)*1.2f,.45f,-13+(i/3)*1.2f),new Vector3(.85f,.9f,.85f),timber,false,10);
                Art(Visual(crate),"Crate brace",Vector3.back*.44f,new Vector3(.95f,.09f,.05f),cream,Quaternion.Euler(0,0,43));
            }
            for(int i=0;i<5;i++)
            {
                Vector3 pos=new Vector3(i<3?-30:29,2.7f,-22+i*10);
                var palm=Part("Rooted breakable palm "+i,pos,new Vector3(.55f,5.4f,.55f),timber,true,70);
                Join(palm,null,Vector3.down*2.7f,8000,3500);
                for(int ring=0;ring<8;ring++)Art(Visual(palm),"Palm trunk ring",new Vector3(0,(ring-3.5f)*.6f,0),new Vector3(.6f,.08f,.6f),cream);
                for(int leaf=0;leaf<7;leaf++)
                {
                    float a=leaf*360f/7;var direction=Quaternion.Euler(0,a,0)*Vector3.forward;
                    Art(Visual(palm),"Palm frond",Vector3.up*2.6f+direction*1.25f,new Vector3(.65f,.12f,3.3f),green,Quaternion.Euler(20,a,0));
                }
            }
            var notice=Part("Village noticeboard",new Vector3(-6,1.7f,-21),new Vector3(2.8f,1.55f,.2f),dark,false,22);Join(notice,null,Vector3.down*1.7f,3500,4500);
            Art(Visual(notice),"Noticeboard post",new Vector3(-1,-.9f,0),new Vector3(.18f,1.6f,.18f),timber);
            Art(Visual(notice),"Noticeboard post",new Vector3(1,-.9f,0),new Vector3(.18f,1.6f,.18f),timber);
            for(int s=-1;s<=1;s+=2){var post=notice.gameObject.AddComponent<BoxCollider>();post.center=new Vector3(s,-.9f,0);post.size=new Vector3(.18f,1.6f,.18f);}
            Label(notice,"BREAKWATER\nmeet / play / explore",new Vector3(0,0,-.12f),.16f);
            neighborhood=Group("Ferry landing",new Vector3(0,0,-29),0);
            VillagePiece prior=null;
            for(int i=0;i<3;i++)
            {
                var deck=Part("Landing deck "+i,new Vector3(0,.22f,-i*2),new Vector3(5,.28f,1.98f),timber,true,90);
                Join(deck,null,Vector3.down*.14f,14000,18000);if(prior)Join(deck,prior,Vector3.forward,4500,6000);prior=deck;
                for(int s=-1;s<=1;s+=2){var bollard=Part("Landing bollard",new Vector3(s*2.3f,.7f,-i*2),new Vector3(.3f,.7f,.3f),cream,false,8);Join(bollard,deck,Vector3.down*.35f,1300,1600);}
            }
        }
        static void ArcadeCabinets(float depth)
        {
            for(int i=0;i<3;i++)
            {
                var cabinet=Part("Loose arcade cabinet "+i,new Vector3((i-1)*2.1f,1.23f,depth*.28f),new Vector3(1.15f,1.8f,.85f),i%2==0?purple:coral,false,45);
                Art(Visual(cabinet),"Arcade screen",new Vector3(0,.35f,-.44f),new Vector3(.9f,.65f,.035f),glass);
                Art(Visual(cabinet),"Game display",new Vector3(0,.35f,-.465f),new Vector3(.3f,.3f,.02f),yellow,Quaternion.Euler(0,0,25));
                Art(Visual(cabinet),"Control shelf",new Vector3(0,-.16f,-.58f),new Vector3(1.1f,.12f,.4f),cream);
                Art(Visual(cabinet),"Joystick",new Vector3(-.28f,-.02f,-.58f),new Vector3(.1f,.2f,.1f),dark);
                for(int button=0;button<3;button++)Art(Visual(cabinet),"Arcade button",new Vector3(.1f+button*.17f,-.06f,-.58f),new Vector3(.1f,.06f,.1f),button%2==0?yellow:teal);
            }
        }
        static void CommunityConnections()
        {
            var inn=pieces.First(p=>p.building=="THE LANTERN INN"&&p.purpose=="Floor bay 1-2");
            var repair=pieces.First(p=>p.building=="THE FIXERY"&&p.purpose=="Roof section 1");
            Vector3 a=inn.transform.parent.TransformPoint(new Vector3(-5,3.38f,4.5f));
            Vector3 b=repair.transform.parent.TransformPoint(new Vector3(1,3.38f,3));
            neighborhood=Group("Neighbors' shared upper passage",Vector3.zero);
            var delta=b-a;float length=delta.magnitude;
            var bridge=Part("Shared workshop bridge",(a+b)*.5f,new Vector3(1.85f,.22f,length+.2f),timber,true,85,Quaternion.LookRotation(delta,Vector3.up));
            Join(bridge,inn,Vector3.back*length*.5f,7000,35000);Join(bridge,repair,Vector3.forward*length*.5f,7000,35000);
            for(int plank=0;plank<14;plank++)Art(Visual(bridge),"Bridge board",new Vector3(0,.12f,(plank/13f-.5f)*length),new Vector3(1.9f,.05f,.13f),cream);
            for(int side=-1;side<=1;side+=2)
            {
                Art(Visual(bridge),"Low bridge railing",new Vector3(side*.9f,.9f,0),new Vector3(.09f,.1f,length),coral);
                var rail=bridge.gameObject.AddComponent<BoxCollider>();rail.center=new Vector3(side*.9f,.9f,0);rail.size=new Vector3(.09f,.1f,length);
                for(int post=0;post<5;post++)Art(Visual(bridge),"Bridge rail post",new Vector3(side*.9f,.48f,(post/4f-.5f)*length),new Vector3(.09f,.85f,.09f),cream);
            }
        }
        static Mesh RoundBox()
        {
            var v=new List<Vector3>();var n=new List<Vector3>();var tri=new List<int>();float[] grid={-.5f,-.46f,0,.46f,.5f};
            void Face(Vector3 normal,Vector3 u,Vector3 w){int start=v.Count;for(int y=0;y<5;y++)for(int x=0;x<5;x++){var p=normal*.5f+u*grid[x]+w*grid[y];var inner=new Vector3(Mathf.Clamp(p.x,-.46f,.46f),Mathf.Clamp(p.y,-.46f,.46f),Mathf.Clamp(p.z,-.46f,.46f));var norm=(p-inner).normalized;v.Add(inner+norm*.04f);n.Add(norm);}for(int y=0;y<4;y++)for(int x=0;x<4;x++){int a=start+y*5+x;tri.AddRange(new[]{a,a+1,a+5,a+1,a+6,a+5});}}
            Face(Vector3.forward,Vector3.right,Vector3.up);Face(Vector3.back,Vector3.left,Vector3.up);Face(Vector3.up,Vector3.right,Vector3.back);Face(Vector3.down,Vector3.right,Vector3.forward);Face(Vector3.right,Vector3.back,Vector3.up);Face(Vector3.left,Vector3.forward,Vector3.up);
            var mesh=new Mesh{name="Soft candy building box"};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetTriangles(tri,0);mesh.RecalculateBounds();return mesh;
        }
    }
}
