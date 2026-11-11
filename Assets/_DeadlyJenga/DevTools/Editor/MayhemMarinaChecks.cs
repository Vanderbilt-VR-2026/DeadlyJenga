using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.CandyArt;
using DeadlyJenga.CandyArt.Marina;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class MayhemMarinaChecks
    {
        const string Key="MayhemMarinaChecks";
        [MenuItem("Tools/Deadly Jenga/Lobby/Use Mayhem Marina (Default)")]
        public static void UseMarina(){SelectLayout(false);}
        [MenuItem("Tools/Deadly Jenga/Lobby/Use Previous Lobby (Backup)")]
        public static void UseLegacy(){SelectLayout(true);}
        [MenuItem("Tools/Deadly Jenga/Lobby/Use Mayhem Marina (Default)",true)]
        static bool CanUseMarina()=>!EditorApplication.isPlayingOrWillChangePlaymode;
        [MenuItem("Tools/Deadly Jenga/Lobby/Use Previous Lobby (Backup)",true)]
        static bool CanUseLegacy()=>!EditorApplication.isPlayingOrWillChangePlaymode;
        static void SelectLayout(bool legacy)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before changing lobby layout.");
            if(legacy)PlayerPrefs.SetInt(IslandLobby.LegacyLobbyPreference,1);else PlayerPrefs.DeleteKey(IslandLobby.LegacyLobbyPreference);
            PlayerPrefs.Save();Debug.Log((legacy?"Previous lobby backup":"Mayhem Marina default")+" selected. Enter Play Mode to load it.");
        }
        public static void RunRecovery()
        {
            File.WriteAllText(Report,"Marina ocean recovery and solo match return in actual Unity Play Mode.\n");
            SessionState.SetInt(Key+"Preference",PlayerPrefs.GetInt(IslandLobby.LegacyLobbyPreference,-1));
            SessionState.SetBool(Key+"Layout",true);
            SessionState.SetBool(Key+"Recovery",true);
            SelectLayout(false);
            SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));
            EditorApplication.EnterPlaymode();
        }
        public static void RunBackup(){RunLayout(true);}
        public static void RunDefault(){RunLayout(false);}
        public static void RunWildlifeDistribution()
        {
            SessionState.SetBool(Key + "WildlifeDistribution", true);
            RunLayout(false);
        }
        public static void RunDockActivities()
        {
            SessionState.SetBool(Key + "DockActivities", true);
            RunLayout(false);
        }
        static void RunLayout(bool legacy)
        {
            SessionState.SetInt(Key+"Preference",PlayerPrefs.GetInt(IslandLobby.LegacyLobbyPreference,-1));
            SessionState.SetBool(Key+"Layout",true);SessionState.SetBool(Key+"Legacy",legacy);SelectLayout(legacy);
            File.WriteAllText(Report,"Actual Unity lobby default/backup validation.\n");
            SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(ProjectPath.Find("Home.unity"));EditorApplication.EnterPlaymode();
        }
        static string Report=>ProjectPath.Report("MayhemMarina.txt");
        static int errors;static bool failed;static Vector3 captureOrigin;
        static MayhemMarinaChecks(){EditorApplication.playModeStateChanged+=State;}
        public static void RunBatch()
        {
            try
            {
                if(!Application.isBatchMode)throw new InvalidOperationException("Use the reserved batch editor.");
                InstallArt();File.WriteAllText(Report,"Actual Unity lobby geometry and integration checks. Staged avatar rigs are used for cinematic views; they are not measured bot behavior.\n");
                SessionState.SetBool(Key+"Network",false);SessionState.SetBool(Key+"Solo",false);SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(ProjectPath.Find("Home.unity"));EditorApplication.EnterPlaymode();
            }
            catch(Exception e){File.AppendAllText(Report,"PREFLIGHT FAIL "+e+"\n");Debug.LogException(e);EditorApplication.Exit(2);}
        }
        public static void RunArcade()
        {
            File.WriteAllText(Report,"Mayhem Marina candy arcade · actual Unity Play Mode.\n");
            SessionState.SetInt(Key+"Preference",PlayerPrefs.GetInt(IslandLobby.LegacyLobbyPreference,-1));
            SessionState.SetBool(Key+"Layout",true);
            SessionState.SetBool(Key+"Arcade",true);
            SelectLayout(false);
            SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));
            EditorApplication.EnterPlaymode();
        }
        public static void RunPlayRotation()
        {
            File.WriteAllText(Report,"Play startup, actual match, spectator visit and return.");
            SessionState.SetBool(Key+"PlayRotation",true);
            SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Home.unity"));
            EditorApplication.EnterPlaymode();
        }
        public static void RunSolo()
        {
            File.WriteAllText(Report,"Actual Game.unity solo smoke test with Marina sources installed; shared lobby hooks remain pending.\n");
            SessionState.SetBool(Key+"Network",false);SessionState.SetBool(Key+"Solo",true);SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        public static void RunNetwork()
        {
            File.WriteAllText(Report,"Actual two-peer Photon lobby selection test with integrated Marina; no Quest hardware or full two-client match-travel claim.\n");
            SessionState.SetBool(Key+"Network",true);SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(ProjectPath.Find("Home.unity"));EditorApplication.EnterPlaymode();
        }
        static void InstallArt()
        {
            foreach(string name in new[]{"MarinaWood.png","MarinaStone.png","MarinaSky.png"})
            {
                string path=ProjectPath.Find(name);var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(importer){importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.wrapMode=TextureWrapMode.Repeat;importer.mipmapEnabled=true;importer.maxTextureSize=2048;importer.SaveAndReimport();}
            }
            string folder=ProjectPath.FolderOf("MarinaWood.png");string destination=folder+"/MarinaSky.mat";
            if(!AssetDatabase.LoadAssetAtPath<Material>(destination))
            {
                var source=AssetDatabase.LoadAssetAtPath<Material>(ProjectPath.Find("CarnivalPanorama-v1.mat"));
                if(source)AssetDatabase.CreateAsset(new Material(source){name="Marina clear coastal sky"},destination);
            }
            var marinaSky=AssetDatabase.LoadAssetAtPath<Material>(destination);
            if(marinaSky){marinaSky.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPath.Find("MarinaSky.png")));EditorUtility.SetDirty(marinaSky);}
            AssetDatabase.SaveAssets();
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                errors=0;failed=false;Application.logMessageReceived+=Log;EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();
                var go=new GameObject("Marina validation driver");Object.DontDestroyOnLoad(go);go.AddComponent<MayhemMarinaChecksDriver>().StartCoroutine(Guard());
            }
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                if(SessionState.GetBool(Key+"Layout",false))
                {
                    int preference=SessionState.GetInt(Key+"Preference",-1);
                    if(preference<0)PlayerPrefs.DeleteKey(IslandLobby.LegacyLobbyPreference);else PlayerPrefs.SetInt(IslandLobby.LegacyLobbyPreference,preference);
                    PlayerPrefs.Save();SessionState.SetBool(Key+"Layout",false);
                }
                SessionState.SetBool(Key,false);SessionState.SetBool(Key+"Arcade",false);SessionState.SetBool(Key+"Recovery",false);SessionState.SetBool(Key+"PlayRotation",false);Application.logMessageReceived-=Log;AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();EditorApplication.Exit(errors==0&&!failed?0:2);
            }
        }
        static void Log(string value,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+value+"\n"+stack+"\n");}}
        static IEnumerator Guard()
        {
            var stack=new Stack<IEnumerator>();stack.Push(SessionState.GetBool(Key+"PlayRotation",false)?RunPlayRotationScene():SessionState.GetBool(Key+"Recovery",false)?RunRecoveryScene():SessionState.GetBool(Key+"Arcade",false)?RunArcadeScene():SessionState.GetBool(Key+"Layout",false)?RunLayoutScene():SessionState.GetBool(Key+"Network",false)?RunNetworkScene():SessionState.GetBool(Key+"Solo",false)?RunSoloScene():Run());
            while(stack.Count>0)
            {
                bool next=false;object value=null;
                try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}
                catch(Exception e){failed=true;File.AppendAllText(Report,"FAIL "+e+"\n");break;}
                if(!next){(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(value is IEnumerator child){stack.Push(child);continue;}yield return value;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
            File.AppendAllText(Report,$"COMPLETE failed={failed} runtimeErrors={errors}\n");EditorApplication.ExitPlaymode();
        }
        static void Check(bool value,string message)
        {if(!value)throw new InvalidOperationException(message);File.AppendAllText(Report,"PASS "+message+"\n");}
        static IEnumerator Wait(float duration)
        {float end=Time.realtimeSinceStartup+duration;while(Time.realtimeSinceStartup<end)yield return null;}
        static string ArcadeBack(string id) => id switch
        {
            "marina-join" or "marina-nearby" => "marina-close-join",
            "marina-host" or "marina-invite" => "marina-close-host",
            _ => "marina-close"
        };

        static IEnumerator RunPlayRotationScene()
        {
            var home=Object.FindFirstObjectByType<HomeSession>();
            Check(home,"Home scene provides Play");
            yield return null;
            home.RandomLobby();
            float deadline=Time.realtimeSinceStartup+15;
            while(!IslandLobby.Current&&Time.realtimeSinceStartup<deadline)yield return null;
            var lobby=IslandLobby.Current;
            Check(lobby&&lobby.Experience==GameExperience.Game&&!lobby.CanConfigure,"Play enters the locked Marina lobby");
            while(!lobby.CanStart&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(lobby.CanStart&&lobby.GamePhase==RoomGamePhase.Idle,"Play waits for GO");
            var provider=RoomGameExperience.Current;
            var flowField=typeof(RoomGameExperience).GetField("flow",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            bool survival=System.Environment.GetCommandLineArgs().Contains("-playRotationSurvival");
            if(survival)
            {
                // Change only the test room seed so the normal random selector exercises Survival.
                var profileIndex=typeof(RoomGameExperience).GetMethod("ProfileIndex",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                var snapshot=((RoomGameFlow)flowField.GetValue(provider)).State;
                for(int i=0;i<64;i++)
                {
                    snapshot.roomId="survival-fixture-"+i;
                    if((int)profileIndex.Invoke(null,new object[]{snapshot.roomId,0,2})==0)break;
                }
                flowField.SetValue(provider,new RoomGameFlow(snapshot));
            }
            lobby.StartOrCancel();
            Check(lobby.GamePhase==RoomGamePhase.Selected&&lobby.CountdownSeconds>29&&lobby.CountdownSeconds<=30,
                "GO starts a thirty-second countdown before loading");
            Check(lobby.ProfileId is "play:survival/castle" or "play:teamtickets/castle","The first selection uses the two Candy Castle profiles");
            if(survival)
            {
                Check(lobby.ProfileId=="play:survival/castle","The controlled room seed exercises Disaster Survival");
                var flow=(RoomGameFlow)flowField.GetValue(provider);
                for(int i=0;i<7;i++)flow.Tick(flow.State.authorityTerm,5,true,true);
            }
            int size=lobby.MaximumPopulation;
            Check(size==(lobby.ProfileId=="play:survival/castle"?25:30),"The selected profile fills the intended population");
            deadline=Time.realtimeSinceStartup+110;
            CastlePlaySession game=null;
            while(Time.realtimeSinceStartup<deadline)
            {
                game=Object.FindFirstObjectByType<CastlePlaySession>();
                if(game&&game.Modes&&game.Modes.Frame.phase==RoundPhase.Playing)break;
                yield return null;
            }
            Check(game&&game.Modes&&game.Modes.Frame.phase==RoundPhase.Playing,"The Play lobby launches an actual match");
            Check(game.Modes.Frame.players.Count(player=>player.alive)==size,"The match waits for its full real actor cohort");
            if(size==30) Check(game.Modes.Frame.teams.Length==3,"Team PvP uses three teams");
            if(size==25)
            {
                Check(!game.Modes.Frame.pickups.Any(p=>p.kind>=EquipmentKind.Blaster&&p.kind<=EquipmentKind.Dagger),
                    "Disaster Survival has no opening weapon pickups");
                if(!game.IsVR)
                {
                    game.SetMenu(true);
                    yield return null;
                    yield return null;
                    var tablet=Object.FindObjectsByType<CastleWorldMenu>(FindObjectsSortMode.None).Single(menu=>menu.isActiveAndEnabled);
                    var controls=tablet.GetComponentsInChildren<Text>(true).Single(label=>label.text=="Controls");
                    var point=game.ActiveCamera.WorldToScreenPoint(controls.transform.position);
                    var settings=InputSystem.settings;
                    var previousBackground=settings.backgroundBehavior;
                    var previousRouting=settings.editorInputBehaviorInPlayMode;
                    settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                    settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    var mouse=InputSystem.AddDevice<Mouse>();
                    try
                    {
                        InputSystem.QueueStateEvent(mouse,new MouseState{position=point});
                        yield return null;
                        InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1});
                        yield return null;
                        yield return null;
                        Check(mouse.leftButton.isPressed,"The focused input fixture received the desktop mouse press");
                        InputSystem.QueueStateEvent(mouse,new MouseState{position=point});
                        yield return null;
                        Check(tablet.transform.Find("Controls page").gameObject.activeInHierarchy,
                            "Desktop mouse press opens the Controls page");
                    }
                    finally
                    {
                        InputSystem.RemoveDevice(mouse);
                        settings.backgroundBehavior=previousBackground;
                        settings.editorInputBehaviorInPlayMode=previousRouting;
                        game.SetMenu(false);
                    }
                }
                var matchArt=Object.FindObjectsByType<CandyEnvironment>(FindObjectsSortMode.None)
                    .Single(art=>art.gameObject.scene==game.gameObject.scene);
                float waterY=matchArt.waterRenderer.transform.position.y;
                game.Player.gameObject.GetComponent<IDamageable>().Eliminate(null);
                deadline=Time.realtimeSinceStartup+18;
                while(!game.VisitingLobby&&Time.realtimeSinceStartup<deadline)yield return null;
                Check(game.VisitingLobby&&lobby.ActiveCamera.enabled&&!lobby.CanStart,"An eliminated player watches from the retained lobby");
                Check(lobby.GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&r.name=="Marina ocean"),
                    "Retained Marina ocean is visible during the elimination visit");
                Check(matchArt.waterRenderer.enabled&&Mathf.Abs(matchArt.waterRenderer.transform.position.y-waterY)<.01f,
                    "Visiting the lobby leaves the match ocean at its own waterline");
                var wall=lobby.GetComponentInChildren<LobbySpectatorTVWall>();
                deadline=Time.realtimeSinceStartup+6;
                while(wall.AssignedChannels==0&&Time.realtimeSinceStartup<deadline)yield return null;
                var first=wall.GetComponentsInChildren<RawImage>().First(image=>image.name=="Channel 1");
                var screen=first.rectTransform;
                Check(wall.TryPoint(new Ray(screen.position-screen.forward,screen.forward),2,out int channel,out _)&&channel==0,
                    "The television ray selects the first occupied screen");
                Check(wall.OpenFullScreen(channel)&&wall.FullScreen,"The selected channel opens full screen");
                wall.CloseFullScreen();
            }
        }
        static IEnumerator RunArcadeScene()
        {
            Application.runInBackground=true;
            float until=Time.realtimeSinceStartup+45;
            while((!IslandLobby.Current||!IslandLobby.Current.GetComponent<MarinaScreen>())&&
                Time.realtimeSinceStartup<until)yield return null;
            var lobby=IslandLobby.Current;
            Check(lobby&&lobby.View()&&lobby.GetComponent<MayhemMarina>(),"Actual Mayhem Marina loaded");
            var screen=lobby.GetComponent<MarinaScreen>();
            var view=lobby.View();
            Check(screen&&screen.Visible,"Candy arcade screens are visible");
            var faces=lobby.GetComponentsInChildren<RectTransform>();
            var join=faces.Single(face=>face.name=="Lavender Join display");
            var host=faces.Single(face=>face.name=="Mint Host display");
            Check(join.parent==host.parent&&join.localPosition.x<0&&host.localPosition.x>0,
                "Join and Host physically flank the central screen");
            var detailFaces=lobby.GetComponentsInChildren<RectTransform>(true);
            var joinDetail=detailFaces.Single(face=>face.name=="JOIN");
            var hostDetail=detailFaces.Single(face=>face.name=="HOST");
            Check(Vector3.Distance(join.position,joinDetail.position)<.01f&&
                Vector3.Distance(host.position,hostDetail.position)<.01f,
                "Full room flows are mounted on their own cabinet glass");
            Check(faces.Count(face=>face.name=="Live destination image")==2,"The center has exactly two illustrated selections");
            var start = faces.Single(face => face.name == "marina-play");
            var round = faces.Single(face => face.name == "marina-experience");
            Check(start.anchoredPosition.y + start.rect.yMin > round.anchoredPosition.y + round.rect.yMax,
                "Start Match and Round Info hit areas do not overlap");
            var pickTarget = typeof(MarinaScreen).GetMethod("PickTarget",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var roundUpperEdge = round.TransformPoint(new Vector3(0, round.rect.yMax - 1, 0));
            var target = pickTarget.Invoke(screen, new object[] { new Ray(roundUpperEdge - round.forward, round.forward), 2f });
            Check((string)target?.GetType().GetField("id").GetValue(target) == "marina-experience" && !lobby.StartPending,
                "A ray at the top of Round Info selects its own control and cannot start a match");
            var focus=faces.Single(face=>face.name=="marina-maps");
            var caption=focus.GetComponentsInChildren<UnityEngine.UI.Text>(true).Single(label=>label.text=="MAP");
            Color captionColor=caption.color;
            var hitCenter=focus.TransformPoint(Vector3.zero);
            var cardTarget=pickTarget.Invoke(screen,new object[]{new Ray(hitCenter-focus.forward,focus.forward),2f});
            typeof(MarinaScreen).GetMethod("PaintTarget",System.Reflection.BindingFlags.Static|
                System.Reflection.BindingFlags.NonPublic).Invoke(null,new[]{cardTarget,(object)true,(object)true});
            Check(caption.color==captionColor,"Hover preserves the caption ink on its own dark panel");
            var preview=focus.GetComponentsInChildren<UnityEngine.UI.RawImage>(true).Single();
            var missing=focus.GetComponentsInChildren<UnityEngine.UI.Text>(true).Single(label=>label.text=="Preview unavailable");
            var fit=typeof(MarinaScreen).GetMethod("FitPreview",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            var art=preview.texture;
            preview.texture=null;
            fit.Invoke(null,new object[]{preview,missing});
            Check(!preview.enabled&&missing.gameObject.activeSelf,"Missing card art shows a readable fallback, not a white image");
            preview.texture=art;
            fit.Invoke(null,new object[]{preview,missing});
            var panel=(RectTransform)typeof(MarinaScreen).GetField("focusPanel",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(screen);
            var note=panel.GetComponentInChildren<UnityEngine.UI.Image>(true).rectTransform;
            var noteBottom=note.parent.localPosition.y+note.localPosition.y+note.rect.yMin;
            Check(noteBottom>start.localPosition.y+start.rect.yMax,"Hover text has its own space above Start Match");
            var beam=lobby.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name=="Visual - beachside projector beam");
            Check(beam.enabled&&beam.sharedMaterial.shader.isSupported&&!beam.GetComponent<Collider>(),
                "Beachside projector uses supported art and does not block players or pointers");
            var cinema=lobby.GetComponentsInChildren<UnityEngine.UI.RawImage>(true)
                .Single(image=>image.texture&&image.texture.name=="Marina projection vignette");
            Check(cinema.gameObject.activeInHierarchy,"Projected backdrop is visible on the default match screen");
            var floor=lobby.GetComponentsInChildren<MeshCollider>().Single(c=>c.sharedMesh==lobby.GetComponent<MayhemMarina>().WalkableMesh);
            foreach(float x in new[]{-5.15f,5.15f})
                Check(!floor.Raycast(new Ray(lobby.transform.TransformPoint(new Vector3(x,2.20f,9.05f)),lobby.transform.forward),out _,1.35f),
                    "Side kiosk approach is clear at x="+x);
            for(int x=-10;x<=-5;x++)
                Check(floor.Raycast(new Ray(lobby.transform.TransformPoint(new Vector3(x,4.5f,8f)),Vector3.down),out _,5),
                    "Wardrobe route has floor support at x="+x);
            var television=lobby.GetComponentInChildren<LobbySpectatorTVWall>();
            var televisionBlocker=lobby.GetComponentsInChildren<BoxCollider>()
                .Single(blocker=>blocker.name=="Island blocker · "+television.name);
            Check(Vector3.Distance(televisionBlocker.transform.position,television.transform.position)<.001f,
                "The television has one solid blocker at its visible location");
            Physics.SyncTransforms();
            var approach=lobby.transform.TransformPoint(new Vector3(0,3.51f,3.40f));
            var toHost=host.position-approach;
            Check(!televisionBlocker.Raycast(new Ray(approach,toHost.normalized),out _,toHost.magnitude),
                "The spectator televisions leave the Host cabinet visible from the pavilion approach");
            captureOrigin=lobby.transform.position;
            lobby.Player.Place(lobby.transform.TransformPoint(new Vector3(-3.8f,1.87f,5.4f)),Quaternion.identity);
            yield return Wait(3);
            lobby.SurvivorBots?.Clear();
            if(!Environment.GetCommandLineArgs().Contains("-skipMarinaCapture"))
            {
                Capture(new Vector3(0,3.51f,3.40f),new Vector3(0,3.95f,10.7f),52,"Marina-Candy-Arcade.png");
                Capture(new Vector3(0,3.76f,6.12f),new Vector3(0,3.94f,10.97f),43,"Marina-Candy-Arcade-Screen.png");
            }
            // The hidden batch has no OS focus. Exercise the command routes with simulated focus.
            lobby.SendMessage("OnApplicationFocus",true);
            lobby.SetMenu(false);
            screen.SendMessage("OnApplicationFocus",true);
            view.SendMessage("OnApplicationFocus",true);
            foreach(var id in new[]{"marina-modes","marina-maps","marina-join","marina-host","marina-nearby","marina-settings"})
            {
                yield return null;
                yield return null;
                Check(screen.Invoke(id),id+" opens the existing control flow");
                Check(!screen.Invoke("marina-play")&&!lobby.StartPending,
                    id+" cannot pass the same input through to Start Match");
                yield return null;
                if(id=="marina-join")
                {
                    Check(lobby.CodeFocused&&view.GiantKeyboard.gameObject.activeSelf,"Room code opens the private keyboard");
                    var keyboard = view.GiantKeyboard;
                    var grabBar = keyboard.Find("Keyboard grab bar");
                    Check(grabBar && Vector3.Distance(keyboard.position, lobby.Player.ViewCamera.transform.position) < 1.25f,
                        "The private keyboard floats in front of the player with a grab bar");
                    Vector3 originalPosition = keyboard.position;
                    var grabRay = new Ray(grabBar.position - grabBar.forward * .3f, grabBar.forward);
                    view.Point(grabRay, true, true);
                    view.Point(new Ray(grabRay.origin + grabBar.right * .18f, grabRay.direction), false, true);
                    view.Point(grabRay, false);
                    Check(Vector3.Distance(keyboard.position, originalPosition) > .08f, "Dragging the grab bar moves the keyboard");
                    var originalCode=lobby.EnteredCode;
                    try
                    {
                        lobby.EnteredCode="ABC234";
                        var close=view.GiantKeyboard.Find("keyboard-close");
                        Check(close&&close.gameObject.activeInHierarchy,"The keyboard shows a close control");
                        view.Point(new Ray(close.position-close.forward*.25f,close.forward),true);
                        Check(!lobby.CodeFocused&&!view.GiantKeyboard.gameObject.activeSelf&&
                            lobby.EnteredCode=="ABC234"&&joinDetail.gameObject.activeSelf&&screen.Visible,
                            "Closing the keyboard preserves the code, Join and the map/mode screen");
                        Check(!lobby.MenuOpen && !lobby.Player.InputSuspended,
                            "Closing the room-code keyboard releases the movement lock it opened");
                    }
                    finally { lobby.EnteredCode=originalCode; }
                    yield return null;
                    Check(screen.Invoke("marina-maps"),"Map selection remains usable while Join is open");
                    yield return null;
                    Check(joinDetail.gameObject.activeSelf&&!screen.Visible,
                        "The map picker replaces only the center face");
                    Check(view.InvokeChoice("marina-close")&&joinDetail.gameObject.activeSelf&&screen.Visible,
                        "Closing the map picker preserves the open Join screen");
                    yield return null;
                }
                if(id=="marina-host")
                    Check(hostDetail.gameObject.activeSelf&&!joinDetail.gameObject.activeSelf&&screen.Visible,
                        "Host opens on its cabinet while the center face remains active");
                if(id=="marina-nearby")
                    Check(joinDetail.gameObject.activeSelf&&screen.Visible,"Nearby results stay on Join");
                if(id=="marina-maps")
                {
                    yield return null;
                    Check(view.GetComponentsInChildren<UnityEngine.UI.RawImage>().Any(image=>
                        image.name=="Map card thumbnail"&&image.enabled&&image.texture),
                        "The categorized map list shows thumbnails on the map cards");
                    var previewChoice=typeof(IslandLobbyView).GetMethod("PreviewMapChoice",
                        System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                    var pickerOption=typeof(IslandLobbyView).GetMethod("PickerOption",
                        System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                    var different=Enumerable.Range(0,10).Select(slot=>(slot,option:(LobbyOption)pickerOption.Invoke(view,new object[]{slot})))
                        .FirstOrDefault(row=>row.option!=null&&row.option.id!=lobby.SelectedMapId);
                    if(different.option!=null)
                    {
                        string selected=lobby.SelectedMapId;
                        var shown=view.GetComponentsInChildren<UnityEngine.UI.Text>(true).Single(label=>label.name=="Previewed map name");
                        previewChoice.Invoke(view,new object[]{"picker-"+different.slot});
                        Check(shown.text==different.option.title&&lobby.SelectedMapId==selected,"Map hover previews without selecting");
                        previewChoice.Invoke(view,new object[]{null});
                        Check(shown.text!=different.option.title&&lobby.SelectedMapId==selected,"Moving off the map restores the selected preview");
                    }
                }
                Check(view.InvokeChoice(ArcadeBack(id)),id+" returns to its arcade face");
            }
            yield return null;
            Check(screen.Invoke("marina-modes"),"Mode picker opens from the starting layout");
            yield return null;
            Check(Enumerable.Range(0,8).Any(slot=>view.InvokeChoice("mode-card-"+slot)),
                "A selectable mode can be committed");
            Check(screen.Visible,"Selecting a mode restores the original map and mode cards");
            yield return null;
            Check(screen.Invoke("marina-maps"),"Map picker opens only when requested");
            yield return null;
            Check(view.InvokeChoice("setup-back")&&screen.Visible,"Picker Back restores the same starting layout");
            yield return null;
            Check(screen.Invoke("marina-style")&&view.WardrobeOpen,"Style opens the wardrobe");
            var appearance=lobby.Player.gameObject.GetComponent<IAvatarAppearance>();
            var original=appearance.Appearance;
            try
            {
                appearance.ApplyAppearance(new AvatarAppearance
                {
                    accessories=AvatarAccessory.TopHat|AvatarAccessory.DevilHorns,
                    hair=AvatarHair.Bob
                });
                var toggle=typeof(IslandLobbyView).GetMethod("ToggleWardrobeAccessory",
                    System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
                var changed=(AvatarAppearance)toggle.Invoke(null,new object[]{appearance.Appearance,AvatarAccessory.CowboyHat});
                Check((changed.accessories&AvatarAccessory.TopHat)==0&&(changed.accessories&AvatarAccessory.CowboyHat)!=0&&
                    (changed.accessories&AvatarAccessory.DevilHorns)!=0&&changed.hair==AvatarHair.Bob,
                    "A new hat replaces the old hat and keeps compatible horns and hair");
            }
            finally { appearance.ApplyAppearance(original);view.SetWardrobeOpen(false); }
            if (Environment.GetCommandLineArgs().Contains("-marinaGameSmoke"))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("Game");
                yield return RunSoloScene();
            }
        }
        static IEnumerator RunRecoveryScene()
        {
            Application.runInBackground=true;
            float until=Time.realtimeSinceStartup+45;
            while (!IslandLobby.Current&&Time.realtimeSinceStartup<until) yield return null;
            var lobby=IslandLobby.Current;
            Check(lobby&&lobby.GetComponent<MayhemMarina>()&&NetworkActors.Alive(lobby.Player),"Actual Marina lobby and survivor loaded");
            yield return Wait(.5f);
            lobby.SetMenu(false);
            var animals=lobby.GetComponentsInChildren<DeadlyJenga.Wildlife.IslandAnimal>().ToArray();
            Check(animals.Length==4&&animals.Select(a=>a.Species).OrderBy(id=>id).SequenceEqual(new[]{"crab","fox","frog","tortoise"}),
                "Default wildlife is present exactly once after retaining the lobby");
            Check(animals.All(animal=>animal.DistanceTravelled<10),"Wildlife movement does not count the lobby relocation");
            File.AppendAllText(Report,$"INPUT initial focus={Application.isFocused} suspended={lobby.Player.InputSuspended} setup={lobby.SetupHint}\n");
            if(Application.isFocused)Check(!lobby.Player.InputSuspended,"Focused Marina input available");
            CheckRiderStrands();
            var oldPlayer=lobby.Player;
            var ocean=lobby.GetComponentsInChildren<Renderer>(true).Single(r=>r.name=="Marina ocean");
            var water=lobby.transform.TransformPoint(new Vector3(-14,0,-37));
            water.y=lobby.WaterHeight(water)-.06f;
            lobby.Player.Place(water,Quaternion.identity);
            yield return Wait(.45f);
            var recovered=lobby.transform.InverseTransformPoint(lobby.Player.transform.position);
            Check(recovered.y>1.15f&&Vector2.Distance(new Vector2(recovered.x,recovered.z),new Vector2(-10,-8))<5,
                "Ocean crossing near water level returns the survivor to the dry arrival pier");
            if(Application.isFocused)Check(!lobby.Player.InputSuspended,"Ocean recovery keeps focused movement available");
            lobby.Player.Place(lobby.transform.TransformPoint(new Vector3(17,.47f,-8)),Quaternion.identity);
            yield return Wait(.4f);
            var pool=lobby.transform.InverseTransformPoint(lobby.Player.transform.position);
            Check(Vector2.Distance(new Vector2(pool.x,pool.z),new Vector2(17,-8))<1.2f,"Practice basin stays playable");

            MatchSetup.FromLobby=true;MatchSetup.StartLocalSession=false;
            MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.BotTarget=0;MatchSetup.HandCount=0;
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(ProjectPath.Find("Game.unity"));
            yield return Wait(.5f);
            Check(lobby.MatchResident&&!oldPlayer.gameObject.activeInHierarchy&&Services.Session is CastlePlaySession,
                "Real match scene parks the persistent lobby body");
            Check(!ocean.gameObject.activeInHierarchy,"Parked Marina ocean does not render in the match");
            Check(animals.All(animal=>animal&&!animal.gameObject.activeInHierarchy),"Retained wildlife sleeps while the match is active");
            Check(lobby.Room.ReturnMatchToLobby(),"Solo room accepts return to the lobby");
            until=Time.realtimeSinceStartup+45;
            while (IslandLobby.Current!=lobby&&Time.realtimeSinceStartup<until) yield return null;
            Check(IslandLobby.Current==lobby,"Return reloads the retained Marina through the room");
            yield return Wait(.5f);
            Check(ReferenceEquals(oldPlayer,lobby.Player)&&oldPlayer.gameObject.activeInHierarchy&&oldPlayer.ViewCamera.enabled,
                "Retained survivor and camera are active after return");
            Check(ocean.gameObject.activeInHierarchy&&RenderSettings.skybox,"Marina ocean and sky are restored");
            Check(animals.All(animal=>animal&&animal.gameObject.activeInHierarchy)&&lobby.GetComponent<IWildlifeControls>().Count==animals.Length,
                "The same wildlife resumes without duplicate animals");
            File.AppendAllText(Report,$"INPUT returned focus={Application.isFocused} suspended={lobby.Player.InputSuspended} setup={lobby.SetupHint}\n");
            if(!Application.isFocused&&lobby.Player.InputSuspended)yield break;
            Check(!lobby.MenuOpen&&!lobby.Player.InputSuspended,"Returned survivor is ready for movement");
            var motor=lobby.Survivor();
            var start=motor.transform.position;
            motor.externallyDriven=true;motor.InputDriver=new MarinaWalkDriver{target=start+lobby.transform.right*1.1f};
            yield return Wait(1);
            motor.InputDriver=null;motor.externallyDriven=false;
            Check(Vector3.ProjectOnPlane(motor.transform.position-start,Vector3.up).magnitude>.45f,
                "The real survivor motor accepts movement after the match return");
        }
        static void CheckRiderStrands()
        {
            var shader=Resources.Load<Shader>("CandyScenery/CandySummoningColumn");
            Check(shader&&shader.isSupported,"Travel beacon shader is supported");
            var material=new Material(shader);
            var mesh=CandySummoningColumn.CreateMesh();
            var target=new GameObject("Beacon rider fixture");
            target.transform.position=new Vector3(2000,300,2000);
            var effect=new GameObject("Beacon strand render fixture");
            effect.layer=31;
            var column=effect.AddComponent<CandySummoningColumn>();
            column.Initialize(target.transform,mesh,material,0);
            var eye=new GameObject("Beacon eye fixture").AddComponent<Camera>();
            eye.enabled=false;
            eye.cullingMask=1<<31;
            eye.clearFlags=CameraClearFlags.SolidColor;
            eye.backgroundColor=Color.black;
            eye.allowHDR=false;
            eye.GetUniversalAdditionalCameraData().allowXRRendering=false;
            var output=new RenderTexture(96,96,16);
            var pixels=new Texture2D(96,96,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            eye.targetTexture=output;
            int LitPixels()
            {
                eye.Render();
                RenderTexture.active=output;
                pixels.ReadPixels(new Rect(0,0,96,96),0,0);
                return pixels.GetPixels32().Count(pixel=>pixel.r>18||pixel.g>18||pixel.b>18);
            }
            try
            {
                eye.transform.position=target.transform.position+new Vector3(.35f,1.6f,0);
                column.Render(.1f,.05f,1);
                int distantReveal=LitPixels();
                typeof(CandySummoningColumn).GetMethod("SetRiderCamera",
                    System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(column,new object[]{eye});
                column.Render(.1f,.05f,1);
                int groundReveal=LitPixels();
                target.transform.position+=Vector3.up*32;
                eye.transform.position+=Vector3.up*32;
                column.Render(.2f,.08f,1);
                int flyingReveal=LitPixels();
                File.AppendAllText(Report,$"BEACON lit pixels exterior={distantReveal} ground={groundReveal} flying={flyingReveal}\n");
                Check(groundReveal>distantReveal+64&&flyingReveal>distantReveal+64,
                    "Early strands render near the rider before lift and at altitude");
            }
            finally
            {
                RenderTexture.active=previous;
                eye.targetTexture=null;
                output.Release();
                Object.Destroy(output);Object.Destroy(pixels);Object.Destroy(eye.gameObject);
                Object.Destroy(effect);Object.Destroy(target);Object.Destroy(mesh);Object.Destroy(material);
            }
        }
        static IEnumerator RunLayoutScene()
        {
            Application.runInBackground=true;AudioListener.volume=0;
            float until=Time.realtimeSinceStartup+45;HomeSession home;
            while(!(home=Object.FindFirstObjectByType<HomeSession>())&&Time.realtimeSinceStartup<until)yield return null;
            Check(home,"Home scene loaded");home.RandomLobby();until=Time.realtimeSinceStartup+45;IslandLobby lobby;
            while(!(lobby=Object.FindFirstObjectByType<IslandLobby>())&&Time.realtimeSinceStartup<until)yield return null;
            Check(lobby&&lobby.View(),"Lobby and existing control view loaded");yield return Wait(2);
            bool legacy=SessionState.GetBool(Key+"Legacy",false);var view=lobby.View();
            Check(legacy?Mathf.Approximately(lobby.Radius,9):Mathf.Approximately(lobby.Radius,37),"Correct layout bounds selected");
            Check(legacy?!lobby.GetComponent<MayhemMarina>():lobby.GetComponent<MayhemMarina>().Built,"Correct layout art selected");
            Check(NetworkActors.Alive(lobby.Player),"Local survivor alive");Physics.SyncTransforms();
            Check(Physics.Raycast(lobby.SafeSpawnPoint()+Vector3.up*2,Vector3.down,5,~(1<<Layers.Survivor),QueryTriggerInteraction.Ignore),"Selected lobby safe spawn has solid support");
            if (SessionState.GetBool(Key + "WildlifeDistribution", false))
            {
                SessionState.SetBool(Key + "WildlifeDistribution", false);
                CheckWildlifeDistribution(lobby);
                yield break;
            }
            if (SessionState.GetBool(Key + "DockActivities", false))
            {
                SessionState.SetBool(Key + "DockActivities", false);
                CheckWildlifeDistribution(lobby);
                yield return CheckDockActivities(lobby);
                yield break;
            }
            if(legacy)
            {
                Check(lobby.GetComponents<CandyLobbyResort>().Any(x=>x.enabled),"Previous candy resort preserved and active");
                foreach(string name in new[]{"JOIN","HOST","MAP + MODE"})
                    Check(view.transform.Find(name)&&view.transform.Find(name).gameObject.activeInHierarchy,name+" previous terminal present");
                lobby.Player.Place(view.WardrobePosition+Vector3.up*.04f,Quaternion.identity);view.SetWardrobeOpen(true);yield return Wait(.2f);
                Check(view.WardrobeOpen,"Backup wardrobe opens");view.SetWardrobeOpen(false);
                Check(!lobby.GetComponentInChildren<DeadlyJenga.World.Maps.MarinaPracticePool>(),"Backup has no Marina practice overlay");
            }
            else
            {
                var screen=lobby.GetComponent<MarinaScreen>();Check(screen&&screen.Visible,"Marina facade active by default");
                if(Application.isFocused)
                    foreach(string id in new[]{"marina-modes","marina-maps","marina-host","marina-join","marina-settings"})
                    {
                        yield return null;
                        Check(screen.Invoke(id),id+" opens");
                        yield return null;
                        Check(view.InvokeChoice(ArcadeBack(id)),id+" closes");
                        yield return null;
                    }
                else File.AppendAllText(Report,"SKIP Terminal input requires a focused Game View. Facade presence verified.\n");
                var marina=lobby.GetComponent<MayhemMarina>();
                var ocean = marina.GetComponentsInChildren<MeshRenderer>().Single(renderer => renderer.name == "Marina ocean");
                Check(ocean.sharedMaterial == view.Environment.profile.water && ocean.sharedMaterial.shader.name == "Candy Tropical/Ocean",
                    "Marina uses the gameplay ocean material and shader");
                Check(!view.Environment.waterRenderer.gameObject.activeInHierarchy, "Only the Marina ocean renders in the lobby");
                var properties = new MaterialPropertyBlock();
                ocean.GetPropertyBlock(properties);
                Check(Mathf.Approximately(properties.GetFloat("_WaterHeight"), ocean.transform.position.y) &&
                    Mathf.Approximately(properties.GetFloat("_WaveHeight"), view.Environment.profile.waveHeight),
                    "Ocean waves use gameplay tuning at the Marina waterline");
                var zoneLights = marina.GetComponentsInChildren<Light>().Where(light => light.type == LightType.Point).ToArray();
                Check(zoneLights.Length == 7 && zoneLights.All(light => light.shadows == LightShadows.None),
                    "Seven local lighting accents use no additional shadow maps");
                var signage = marina.GetComponentsInChildren<TextMesh>().Where(label => label.name.StartsWith("Marina lettering", StringComparison.Ordinal)).ToArray();
                Check(signage.Length >= 8 && signage.All(label => label.GetComponent<MeshRenderer>().sharedMaterial.shader.name == "DeadlyJenga/Village Lettering"),
                    "Every wayfinding sign uses depth-tested text");
                yield return CheckTerminalLayout(lobby, view);
                captureOrigin=lobby.transform.position;
                Check(marina.RenderTriangles>0&&marina.CollisionTriangles>1000,"Actual rendered Marina and walkable geometry present");
                Check(!Physics.Raycast(lobby.transform.TransformPoint(new Vector3(2,2,-8)),Vector3.down,3,
                    (1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore),"The central inlet has no hidden walking surface");
                yield return Wait(3);
                Check(!lobby.GetComponentsInChildren<Canvas>().Any(canvas=>canvas.enabled&&canvas.name.EndsWith(" / readable back",StringComparison.Ordinal)),
                    "Hidden legacy terminals leave no visible rear canvases over the marina");
                Capture(new Vector3(-26,19,-30),new Vector3(0,2,5),66,"Marina-Ocean-Lighting-Overview.png");
                Capture(new Vector3(-10.0f,2.56f,-8.35f),new Vector3(0,3.85f,11.1f),65,"Marina-Ocean-Lighting-Arrival.png");
                Capture(new Vector3(15.6f,2.35f,-.4f),new Vector3(17,1.1f,-8),68,"Marina-Ocean-Lighting-Practice.png");
                lobby.enabled=false; // Stop population refill during the isolated motor routes.
                if(Services.Session?.SurvivorBots!=null)
                {
                    Services.Session.SurvivorBots.AutoFill=false;
                    Services.Session.SurvivorBots.Running=false;
                    Services.Session.SurvivorBots.Clear();
                }
                yield return Walk(lobby,new[]{new Vector3(-10,1.35f,-8),new Vector3(-6.9f,1.34f,-5.1f),
                    new Vector3(-3,1.31f,3.15f),new Vector3(0,1.31f,3.5f),new Vector3(0,1.9f,6.3f)},"arrival to Play");
                yield return Walk(lobby,new[]{new Vector3(-6.9f,1.31f,3.15f),new Vector3(-13,1.31f,3.15f),
                    new Vector3(-13,1.31f,6.6f)},"promenade to Style");
                yield return Walk(lobby, new[] { new Vector3(-10, 1.35f, -8), new Vector3(-5.65f, 1.35f, -8.45f),
                    marina.Anchor("Beacon Walk").localPosition + Vector3.up * .025f,
                    new Vector3(11.5f, 1.32f, 1.95f), new Vector3(12.9f, 1.31f, 3.15f) }, "Beacon Walk from arrival to the waterfront lounge");
                yield return Walk(lobby,new[]{new Vector3(6.9f,1.31f,3.15f),new Vector3(15,1.31f,3.15f),
                    new Vector3(15,1.31f,2.6f),new Vector3(17,1.12f,-2.86f),new Vector3(17,.4f,-6.55f)},"promenade to practice basin",.29f);
                yield return Walk(lobby,new[]{new Vector3(8.2f,1.31f,9.25f),new Vector3(8.2f,4.07f,17.95f),
                    new Vector3(3.3f,4.18f,17.95f),new Vector3(-8.7f,4.18f,17.95f)},"east ramp and upper terrace link");
                yield return Walk(lobby,new[]{new Vector3(-21,1.31f,2.6f),new Vector3(-21,4.18f,17.9f),
                    new Vector3(-17.8f,4.18f,17.9f)},"west ramp and lookout");
                lobby.enabled=true;
            }
        }
        static IEnumerator CheckTerminalLayout(IslandLobby lobby, IslandLobbyView view)
        {
            // Call the page actions directly in a hidden editor. Pointer input is checked separately when focused.
            const System.Reflection.BindingFlags methods = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var open = typeof(IslandLobbyView).GetMethod("OpenMarinaDetail", methods);
            var close = typeof(IslandLobbyView).GetMethod("CloseMarinaDetail", methods);
            var join = view.transform.Find("JOIN");
            var host = view.transform.Find("HOST");
            var setup = view.transform.Find("MAP + MODE");
            var back = view.transform.Find("Marina detail back control");
            var wardrobe = view.transform.Find("WARDROBE");
            var styleEntry = view.transform.Find("Marina wardrobe entry");
            var keyboard = view.GiantKeyboard;
            Check(join && host && setup && back && wardrobe && styleEntry && keyboard, "All Marina terminals have mounts");
            var mounts = new[] { join, host, setup, back, wardrobe, styleEntry };
            var positions = mounts.Select(mount => mount.localPosition).ToArray();
            var rotations = mounts.Select(mount => mount.localRotation).ToArray();
            var scales = mounts.Select(mount => mount.localScale).ToArray();
            bool Anchored() => mounts.Select((mount, i) => mount.localPosition == positions[i] &&
                mount.localRotation == rotations[i] && mount.localScale == scales[i]).All(stable => stable);
            var player = lobby.Player;
            var start = player.transform.position;
            var facing = player.transform.rotation;
            bool originalMenu = lobby.MenuOpen;
            try
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    player.Place(lobby.transform.TransformPoint(new Vector3(side * 2, 1.9f, 6.5f)),
                        lobby.transform.rotation * Quaternion.Euler(0, side * 35, 0));
                    foreach (var page in new[] { "join", "host", "modes", "maps", "settings" })
                    {
                        open.Invoke(view, new object[] { page });
                        yield return null;
                        Check(Anchored(), page + " retains its mount when opened from side " + side);
                        close.Invoke(view, null);
                        yield return null;
                    }
                }
                open.Invoke(view, new object[] { "join" });
                yield return null;
                Check(join.gameObject.activeInHierarchy && !keyboard.gameObject.activeSelf, "Join opens at the pavilion mount with its keyboard hidden");
                lobby.FocusCode();
                yield return Wait(.15f);
                Check(lobby.CodeFocused && keyboard.gameObject.activeInHierarchy &&
                    Vector3.Distance(keyboard.position, player.ViewCamera.transform.position) < 1.25f,
                    "Code focus floats the keyboard in front of the local player");
                var slots = join.GetComponentsInChildren<RectTransform>(true).Where(rect => rect.name.StartsWith("Code character ", StringComparison.Ordinal)).ToArray();
                var codeLayout = slots.Select(slot => (slot.anchoredPosition, slot.sizeDelta)).ToArray();
                string originalCode = lobby.EnteredCode;
                try
                {
                    lobby.EnteredCode = "ABCDEFGHIJKL";
                    yield return Wait(.15f);
                    Check(Anchored() && slots.Length == 12 && slots.Select((slot, i) =>
                        (slot.anchoredPosition, slot.sizeDelta) == codeLayout[i]).All(stable => stable),
                        "Typing a full room code leaves every code slot and terminal fixed");
                }
                finally { lobby.EnteredCode = originalCode; }
                lobby.UnfocusCode();
                yield return Wait(.15f);
                Check(!keyboard.gameObject.activeSelf && Anchored(), "Closing the keyboard keeps the terminal mounts still");
                close.Invoke(view, null);
                player.Place(view.WardrobePosition + Vector3.up * .04f, lobby.transform.rotation * MayhemMarina.StyleScreenFacing);
                yield return Wait(.15f);
                Check(!view.WardrobeOpen && styleEntry.gameObject.activeInHierarchy, "Entering the boutique leaves the wardrobe closed until selected");
                var eye = player.ViewCamera.transform.position;
                view.Point(new Ray(eye, (styleEntry.position - eye).normalized), true);
                Check(view.WardrobeOpen && Anchored(), "The fixed boutique entry opens the wardrobe without moving any mount");
            }
            finally { close.Invoke(view, null); player.Place(start, facing); lobby.SetMenu(originalMenu); }
            Check(Anchored() && !join.gameObject.activeSelf, "Closing the detail changes visibility with every mount intact");
        }
        static void CheckWildlifeDistribution(IslandLobby lobby)
        {
            var controls = lobby.GetComponent<IWildlifeControls>();
            var wild = lobby.GetComponentsInChildren<IWildlifeActor>().Cast<Component>().ToArray();
            Check(controls?.Available == true && wild.Length == 4, "The four default wild animals spawn in the actual Marina");
            Vector2 Flat(Vector3 p) => new Vector2(p.x, p.z);
            var player = lobby.Player.transform;
            var positions = wild.Select(animal => animal.transform.position).ToArray();
            foreach (var animal in wild)
                File.AppendAllText(Report, $"{((IWildlifeActor)animal).Species} local={lobby.transform.InverseTransformPoint(animal.transform.position)}\n");
            Check(positions.All(p => lobby.PlayableBounds.Contains(p) && p.y > lobby.WaterHeight(p)),
                "All default wildlife stays in bounds and above the waterline");
            Check(positions.Count(p => Vector2.Distance(Flat(p), Flat(player.position)) > 6) >= 3,
                "Default wildlife spreads beyond the arrival area");
            Check(positions.SelectMany((a, i) => positions.Skip(i + 1).Select(b => Vector2.Distance(Flat(a), Flat(b)))).Min() > 2,
                "Default animals occupy separate parts of the Marina");
            var companion = controls.Spawn("fox", true);
            Check(companion && companion.GetComponent<IWildlifeActor>().FollowTarget == player &&
                Vector2.Distance(Flat(companion.transform.position), Flat(player.position)) < 5.5f,
                "Companion still spawns nearby and follows the player");
        }
        static IEnumerator CheckDockActivities(IslandLobby lobby)
        {
            var activities = lobby.GetComponent<MarinaDockActivities>();
            var ski = lobby.transform.Find("Marina jet ski 1");
            Check(activities && ski && ski.Find("Jet ski hull") && lobby.transform.Find("Marina jet ski 2"),
                "Two playable jet ski bodies have visible hulls at the real dock");
            var tv = lobby.GetComponentInChildren<LobbySpectatorTVWall>();
            Check(tv && tv.transform.localPosition.x > 5 && tv.transform.localPosition.z > 6 && tv.transform.localPosition.z < 9 && tv.transform.localPosition.y < 2,
                "The live spectator wall is installed at the pavilion");
            var gull = lobby.transform.Find("Marina gull calls")?.GetComponent<AudioSource>();
            Check(gull && gull.clip && gull.clip.length > 3 && gull.spatialBlend > .95f,
                "The Marina loads its gull recording as positional ambience");
            foreach (var point in new[] { new Vector3(-25.6f, 2, 9.2f), new Vector3(26.2f, 2, 9.1f), new Vector3(5, 2, 28) })
                Check(Physics.Raycast(lobby.transform.TransformPoint(point), Vector3.down, 2, 1 << Layers.Surface, QueryTriggerInteraction.Ignore),
                    $"Sandy habitat has actual surface at {point.x:F1}, {point.z:F1}");

            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(MarinaDockActivities);
            void Call(string method, params object[] args) => type.GetMethod(method, flags).Invoke(activities, args);
            var player = lobby.Player;
            lobby.SetMenu(false);
            player.Place(lobby.transform.TransformPoint(new Vector3(-23, 1.28f, -25.8f)), lobby.transform.rotation);
            yield return null;
            Call("TryBoard");
            Check(player.transform.parent == ski && player.InputSuspended, "Boarding attaches the actual survivor to the ski seat");
            var moored = ski.position;
            for (int i = 0; i < 30; i++) Call("Drive", Vector2.up, .05f);
            Check(Vector3.Distance(moored, ski.position) > 2 && player.transform.parent == ski,
                "The ski accelerates offshore with the rider attached");
            Call("ReturnToDock", true);
            Check(player.transform.parent != ski && player.gameObject.GetComponent<CharacterController>().enabled && !player.InputSuspended &&
                Vector3.Distance(moored, ski.position) < .02f, "Returning re-docks the ski and restores movement");
            var start = lobby.transform.TransformPoint(new Vector3(10.5f, 5.16f, 15.9f));
            Call("LaunchKnife", new Pose(start, lobby.transform.rotation));
            yield return Wait(.52f);
            var knife = lobby.transform.Find("Thrown practice knife");
            Check(knife && knife.Find("Knife blade") && knife.localPosition.z > 17.55f && knife.localPosition.z < 18.25f,
                "A ballistic practice knife travels into an actual target and sticks");
        }
        static IEnumerator RunNetworkScene()
        {
            Application.runInBackground=true;AudioListener.volume=0;float until=Time.realtimeSinceStartup+45;HomeSession home;
            while(!(home=Object.FindFirstObjectByType<HomeSession>())&&Time.realtimeSinceStartup<until)yield return null;
            Check(home,"Actual Home scene loaded for peer validation");home.Demo();until=Time.realtimeSinceStartup+45;IslandLobby lobby;
            while(!(lobby=Object.FindFirstObjectByType<IslandLobby>())&&Time.realtimeSinceStartup<until)yield return null;
            Check(lobby&&lobby.GetComponent<MayhemMarina>(),"Actual integrated Marina loaded for two-peer test");yield return Wait(.3f);
            yield return ModeSelectionNetworkChecks.Run((passed,label)=>
            {
                Check(passed,label);
                if(label=="Second real Photon peer joins the production lobby")
                {
                    var pool=lobby.GetComponentInChildren<DeadlyJenga.World.Maps.MarinaPracticePool>();
                    Check(pool&&!pool.CanPush&&pool.Blocks.All(b=>b.Body.isKinematic),"Connected Marina practice toys remain static on shared lobby");
                }
            });
            Check(lobby&&lobby.Player.gameObject.activeInHierarchy,"Original Marina remains usable after both peers leave");
        }
        static IEnumerator RunSoloScene()
        {
            Application.runInBackground=true;AudioListener.volume=0;
            float until=Time.realtimeSinceStartup+45;CastlePlaySession session;
            while(!(session=Object.FindFirstObjectByType<CastlePlaySession>())&&Time.realtimeSinceStartup<until)yield return null;
            Check(session,"Actual Game.unity session exists");yield return Wait(6);
            Check(NetworkActors.Alive(session.Player),"Game.unity actual solo player is alive");
            Check(Services.World!=null&&Services.World.Blocks.Count>0,"Game.unity actual map blocks are loaded");
        }
        static IEnumerator Run()
        {
            Application.runInBackground=true;Application.targetFrameRate=60;AudioListener.volume=0;
            float limit=Time.realtimeSinceStartup+45;HomeSession home;
            while(!(home=Object.FindFirstObjectByType<HomeSession>())&&Time.realtimeSinceStartup<limit)yield return null;
            Check(home,"Actual Home scene loaded");home.Demo();
            limit=Time.realtimeSinceStartup+45;IslandLobby lobby;
            while(!(lobby=Object.FindFirstObjectByType<IslandLobby>())&&Time.realtimeSinceStartup<limit)yield return null;
            Check(lobby&&lobby.View(),"Actual Lobby scene and Presentation loaded");var view=lobby.View();
            if(Services.Session?.SurvivorBots!=null){Services.Session.SurvivorBots.AutoFill=false;Services.Session.SurvivorBots.Running=false;Services.Session.SurvivorBots.Clear();}
            captureOrigin=lobby.transform.position;
            var marina=lobby.GetComponent<MayhemMarina>();
            bool integrated=marina&&marina.Built;
            if(!integrated)
            {
                File.AppendAllText(Report,"GEOMETRY PREVIEW ONLY: shared lobby integration hooks are not installed in this snapshot. No functional-lobby claim.\n");
                foreach(var canvas in lobby.GetComponentsInChildren<Canvas>(true))canvas.gameObject.SetActive(false);
                foreach(var renderer in lobby.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
                foreach(var resort in lobby.GetComponents<CandyLobbyResort>())resort.enabled=false;
                foreach(var collider in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                    if(collider.gameObject.scene==lobby.gameObject.scene&&collider.GetComponentInParent<IPlayableCharacter>()==null)collider.enabled=false;
                File.AppendAllText(Report,"Preview fixture disables legacy collisions before testing the authored Marina mesh. This does not validate shared floor-replacement hooks.\n");
                marina=lobby.gameObject.AddComponent<MayhemMarina>();marina.Build(lobby,view.Environment);
                var screen=lobby.gameObject.AddComponent<MarinaScreen>();screen.Build(lobby,_=>{});
            }
            var practice=DeadlyJenga.World.Maps.MarinaPracticePool.BuildFor(lobby.gameObject);
            Check(practice.Blocks.Count==18,"Eighteen actual physics practice blocks built");
            Check(marina&&marina.Built,"Marina geometry completed without errors");
            Check(marina.CollisionTriangles>1000,"Authored solid collision mesh present");
            File.AppendAllText(Report,$"Architecture renderTriangles={marina.RenderTriangles}; collisionTriangles={marina.CollisionTriangles}; integration={integrated}\n");
            if(integrated)
            {
                Physics.SyncTransforms();
                foreach(string name in new[]{"Arrival","Wardrobe","Play interaction","Lounge","Roof terrace"})
                {
                    var p=marina.Anchor(name).position;Check(Physics.Raycast(p+Vector3.up*2,Vector3.down,out var hit,5,~(1<<Layers.Survivor),QueryTriggerInteraction.Ignore),name+" has real solid support");
                }
                Check(!Physics.Raycast(lobby.transform.TransformPoint(new Vector3(2,2,-8)),Vector3.down,3,~(1<<Layers.Survivor),QueryTriggerInteraction.Ignore),"Central inlet has no invisible legacy sand floor");
                Check(Physics.Raycast(lobby.transform.TransformPoint(new Vector3(2.8f,4,8.3f)),Vector3.up,out var ceiling,4,~(1<<Layers.Survivor),QueryTriggerInteraction.Ignore)&&ceiling.normal.y<-.3f,"Curved pavilion roof blocks upward passage through its underside");
                Check(lobby.Radius>30&&lobby.SafeSpawnPoint().y>1,"Larger marina lobby bounds and safe spawn active");
                var binding=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                typeof(IslandLobby).GetMethod("BuildFlatFloor",binding).Invoke(lobby,null);
                var gray=(GameObject)typeof(IslandLobby).GetField("flatFloor",binding).GetValue(lobby);Physics.SyncTransforms();
                try{Check(gray.GetComponent<Collider>().Raycast(new Ray(lobby.SafeSpawnPoint()+Vector3.up*2,Vector3.down),out _,4),"GameSession gray-box floor supports configured Marina spawn without art");}
                finally{gray.SetActive(false);Object.Destroy(gray);}
                var screen=lobby.GetComponent<MarinaScreen>();Check(screen&&screen.Visible,"Live Marina facade is active");
                Check(screen.Invoke("marina-modes"),"Mode card opens existing full mode picker");
                Check(!screen.Visible,"Facade yields to detail picker");yield return null;
                Check(view.InvokeChoice("marina-close"),"Detail picker can return to facade");yield return null;
                Check(screen.Invoke("marina-maps"),"Map card opens existing category browser");yield return null;
                Check(view.InvokeChoice("marina-close"),"Map browser can return");yield return null;
                foreach(var id in new[]{"marina-host","marina-join","marina-settings"})
                {
                    Check(screen.Invoke(id),id+" opens existing controls");yield return null;
                    Check(view.InvokeChoice(ArcadeBack(id)),id+" can return");yield return null;
                }
                Check(screen.Invoke("marina-host"),"Host cabinet detail opens for ray check");yield return null;
                var back=view.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b=>b.name=="marina-close-host").transform;
                view.Point(new Ray(back.position-back.forward*8,back.forward),true);
                Check(!back.gameObject.activeInHierarchy&&screen.Visible,"Actual UI ray closes only the Host cabinet from eight meters");
                yield return null;
                Check(screen.Invoke("marina-style"),"Style quick travel opens wardrobe");yield return Wait(.25f);
                Check(view.WardrobeOpen,"Wardrobe remains open at new boutique position");
                var appearance=lobby.Player.gameObject.GetComponent<IAvatarAppearance>();var before=appearance.Appearance;
                try
                {
                    Check(view.InvokeChoice("wardrobe-0"),"Existing wardrobe entry is interactive");
                    Check(appearance.Appearance.accessories!=before.accessories,"Wardrobe applies cosmetic through existing appearance contract");
                    var mirror=view.GetComponentsInChildren<UnityEngine.UI.RawImage>(true).First(x=>x.name=="Mirror image");
                    Check(mirror.texture is RenderTexture image&&image.IsCreated(),"Boutique mirror has a live rendered character texture");
                }
                finally{appearance.ApplyAppearance(before);view.SetWardrobeOpen(false);}
                yield return Wait(.25f);
                Check(screen.Visible,"Closing wardrobe restores facade");
                foreach(string name in new[]{"JOIN","HOST","MODE + MAP","WARDROBE"})
                {var backFace=view.transform.Find(name+" / readable back");Check(!backFace||!backFace.gameObject.activeInHierarchy,"Hidden "+name+" terminal has no orphan rear canvas");}
            }
            foreach(var renderer in Services.Session.Player.gameObject.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            if(Services.Session?.SurvivorBots!=null){Services.Session.SurvivorBots.AutoFill=false;Services.Session.SurvivorBots.Running=false;Services.Session.SurvivorBots.Clear();}
            lobby.enabled=false; // Freeze population refill only for these explicitly staged cinematic captures.
            var sky=RenderSettings.skybox;GameEvents.RaiseEmoted(lobby.gameObject,"LobbyParked");
            Check(RenderSettings.skybox!=sky,"Marina releases its global sky when parked");GameEvents.RaiseEmoted(lobby.gameObject,"LobbyResumed");
            Check(RenderSettings.skybox==sky,"Marina restores its coastal lighting when resumed");
            yield return Walk(lobby,new[]{new Vector3(-17,1.3f,-23),new Vector3(-11,1.4f,-14),new Vector3(-10,1.4f,-9),new Vector3(-9.5f,1.4f,-3),new Vector3(-7.7f,1.4f,3.5f)},"arrival pier and bridge");
            yield return Walk(lobby,new[]{new Vector3(-21,1.4f,3),new Vector3(-21,4.2f,17)},"upper terrace ramp");
            StagePeople();yield return Wait(3);
            var block=practice.Blocks[17];var start=block.transform.position;File.AppendAllText(Report,$"POOL BEFORE canPush={practice.CanPush} frozen={block.Body.isKinematic} position={start} simulation={Physics.simulationMode} timescale={Time.timeScale}\n");block.Push(Vector3.right*5,lobby.gameObject);yield return Wait(.65f);
            File.AppendAllText(Report,$"POOL AFTER position={block.transform.position} velocity={block.Body.linearVelocity} frozen={block.Body.isKinematic} displacement={Vector3.Distance(start,block.transform.position)}\n");
            Check(Vector3.Distance(start,block.transform.position)>.15f,"Practice tower responds to real physics pushes");
            practice.Rebuild();yield return Wait(.5f);
            Check(Vector3.Distance(start,block.transform.position)<.15f,"Practice tower reset restores layout");
            Capture(new Vector3(-20,8.8f,-22),new Vector3(0,2.7f,5),64,"Marina-Hero.png");yield return null;
            Capture(new Vector3(-24,13.5f,-27),new Vector3(0,2.8f,5),63,"Marina-Overview.png");yield return null;
            Capture(new Vector3(-8,3.25f,-12),new Vector3(-1.5f,3.4f,9.8f),74,"Marina-Arrival.png");yield return null;
            Capture(new Vector3(6,3.1f,1),new Vector3(13.2f,2.8f,7),65,"Marina-Lounge.png");yield return null;
            Capture(new Vector3(-17,3.2f,1),new Vector3(-13,3.4f,8),60,"Marina-Wardrobe.png");yield return null;
            Capture(new Vector3(-18,5.9f,12.5f),new Vector3(-10,4.9f,18),65,"Marina-Terrace.png");
            if(integrated)yield return Travel(lobby,marina);
        }
        static IEnumerator Travel(IslandLobby lobby,MayhemMarina marina)
        {
            lobby.enabled=true;lobby.SetMenu(false);
            var details=(ILobbyDetails)lobby;var setup=details.Setup;setup.population=1;details.ConfigureSetup(setup);
            Check(lobby.SelectMode("demo")&&lobby.SelectMap("castle"),"Actual lobby selects a short solo demo session");
            Check(lobby.GetComponent<MarinaScreen>().Invoke("marina-play"),"Marina PLAY requests the normal departure flow");
            float until=Time.realtimeSinceStartup+50;CastlePlaySession game;
            while(!(game=Object.FindFirstObjectByType<CastlePlaySession>())&&Time.realtimeSinceStartup<until)yield return null;
            Check(game,"PLAY reaches actual Game.unity");yield return Wait(3);
            Check(NetworkActors.Alive(game.Player)&&Services.World.Blocks.Count>0,"Actual match player and physical map remain functional");
            Check(!lobby.Player.gameObject.activeInHierarchy,"Persistent Marina player is parked during the match");
            Check(RenderSettings.skybox!=Resources.Load<Material>("MayhemMarina/MarinaSky"),"Parked Marina does not replace match sky");
            var matchWorld=Services.World;game.VisitLobby();yield return Wait(.3f);
            Check(game.VisitingLobby&&lobby.Player.gameObject.activeInHierarchy,"Match menu can visit the same Marina");
            Check(ReferenceEquals(Services.World,matchWorld),"Lobby visit preserves the running match world");
            game.ReturnToMatch();yield return null;Check(!game.VisitingLobby,"Return to match restores match control");
            MatchRuntime.Current.ReturnToLobby();until=Time.realtimeSinceStartup+35;
            while(!ReferenceEquals(Services.Session,lobby)&&Time.realtimeSinceStartup<until)yield return null;
            Check(ReferenceEquals(Services.Session,lobby)&&lobby.GetComponent<MayhemMarina>()==marina,"Round return reuses the original Marina");
            Check(lobby.Player.gameObject.activeInHierarchy&&marina.Built,"Returned Marina remains usable");
            Check(Vector3.Distance(lobby.Player.transform.position,lobby.SafeSpawnPoint())<2,"Returned player is at the supported Marina spawn");
        }
        static IEnumerator Walk(IslandLobby lobby,Vector3[] points,string label,float minHeight=.8f)
        {
            var motor=lobby.Survivor();var driver=new MarinaWalkDriver();motor.externallyDriven=true;motor.InputDriver=driver;motor.InputSuspended=false;
            motor.Place(lobby.transform.TransformPoint(points[0]),Quaternion.identity);
            for(int i=1;i<points.Length;i++)
            {
                driver.target = lobby.transform.TransformPoint(points[i]);
                float simulatedStart = Time.time, wallStart = Time.realtimeSinceStartup;
                while (Vector3.ProjectOnPlane(driver.target - motor.transform.position, Vector3.up).magnitude > .45f &&
                    Time.time < simulatedStart + 12 && Time.realtimeSinceStartup < wallStart + 35)
                    yield return null;
                File.AppendAllText(Report, $"WALK {label} at={motor.transform.position} target={driver.target} status={motor.ActionStatus} simulatedSeconds={Time.time - simulatedStart:F2}\n");
                Check(Vector3.ProjectOnPlane(driver.target-motor.transform.position,Vector3.up).magnitude<.6f,label+" waypoint "+i+" reached by actual survivor motor");
                Check(motor.transform.position.y>lobby.transform.position.y+minHeight,label+" remains above the waterline or basin floor");
            }
            motor.InputDriver=null;motor.externallyDriven=false;motor.InputSuspended=true;
        }
        static void StagePeople()
        {
            var prefab=Resources.Load<CandyPlayerRig>("CandyPlayerVisual");Check(prefab,"Existing real character rig available");
            Vector3[] feet={new(-11,1.35f,-8),new(-8.2f,1.35f,-6.3f),new(-13,1.54f,8.2f),new(-3.5f,1.88f,6.5f),new(3.5f,1.88f,6.4f),new(13.3f,1.32f,4),new(-13,4.16f,16),new(17,.39f,-8)};
            for(int i=0;i<feet.Length;i++)
            {
                var rig=Object.Instantiate(prefab);rig.name="Staged real avatar rig "+i;rig.SetColor(i%rig.style.colors.Length);rig.ownerCamera=null;
                var q=Quaternion.Euler(0,i*53,0);feet[i]+=captureOrigin;var frame=new CandyAvatarFrame{feet=feet[i],head=feet[i]+Vector3.up*1.6f,headRotation=q,scale=1};
                frame.left=new CandyHandPose{position=feet[i]+q*new Vector3(-.36f,1.06f,.14f),rotation=q,skeletonTracked=true,skeleton=CandyHandSkeleton.Rest(true,.25f)};
                frame.right=new CandyHandPose{position=feet[i]+q*new Vector3(.37f,i%3==0?1.65f:1.12f,.23f),rotation=q*Quaternion.Euler(-35,0,-15),skeletonTracked=true,skeleton=CandyHandSkeleton.Rest(false,.15f)};rig.ApplyFrame(frame,true,0);
            }
        }
        static void Capture(Vector3 eye,Vector3 target,float fieldOfView,string filename)
        {
            eye+=captureOrigin;target+=captureOrigin;
            var go=new GameObject("Marina actual Unity capture camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.Skybox;camera.fieldOfView=fieldOfView;camera.nearClipPlane=.08f;camera.farClipPlane=600;camera.allowHDR=true;camera.stereoTargetEye=StereoTargetEyeMask.None;camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
            var rt=new RenderTexture(2560,1440,24);camera.targetTexture=rt;var previous=RenderTexture.active;
            try{camera.Render();RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(ProjectPath.Report(filename),image.EncodeToPNG());Object.Destroy(image);File.AppendAllText(Report,"CAPTURE "+filename+" 2560x1440 actual Unity\n");}
            finally{RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Object.Destroy(rt);Object.Destroy(go);}
        }
    }
    public sealed class MarinaWalkDriver : DeadlyJenga.CastleBots.ICastleSurvivorDriver
    {
        public Vector3 target;
        public DeadlyJenga.CastleBots.CastleBotCommand ReadCommand(CastleSurvivor motor,float dt)
        {var delta=Vector3.ProjectOnPlane(target-motor.transform.position,Vector3.up);return new DeadlyJenga.CastleBots.CastleBotCommand{move=delta.magnitude>.3f?delta.normalized:Vector3.zero,look=delta.normalized};}
    }
    public sealed class MayhemMarinaChecksDriver : MonoBehaviour { }
}
