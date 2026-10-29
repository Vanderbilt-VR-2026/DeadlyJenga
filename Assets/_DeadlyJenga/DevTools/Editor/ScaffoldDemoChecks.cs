using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CandyArt;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class ScaffoldDemoChecks
    {
        const string Key="ScaffoldDemoChecks";
        static int errors;
        static string Report=>ProjectPath.Report("ScaffoldBuildUp.txt");
        static ScaffoldDemoChecks()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {
                    errors=0;Application.logMessageReceived+=Log;
                    EditorApplication.delayCall+=()=>{var go=new GameObject("Scaffold regression driver");Object.DontDestroyOnLoad(go);go.AddComponent<ScaffoldCheckDriver>().StartCoroutine(Guard(Check()));};
                }
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);Time.timeScale=1;
                    MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.FromLobby=false;
                    string original=SessionState.GetString(Key+"Original","");if(File.Exists(original))EditorSceneManager.OpenScene(original);
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Scaffold Build Up %&j")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            if(!EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"))throw new InvalidOperationException("Disaster setup validator is missing.");
            File.WriteAllText(Report,"Actual Game scene scaffold regression\n");
            SessionState.SetString(Key+"Original",SceneManager.GetActiveScene().path);SessionState.SetBool(Key,true);
            MatchSetup.FromLobby=true;MatchSetup.StartLocalSession=false;MatchSetup.MapId="mode-buildup";MatchSetup.ModeId="demo";MatchSetup.HandCount=0;
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType type)
        {
            if(Fusion.NetworkProjectConfig.Global.PeerMode==Fusion.NetworkProjectConfig.PeerModes.Multiple&&message.EndsWith("PeerModes.Multiple requires a scene to be set in StartGameArgs.Scene. Set a scene or disable Multiple Peer mode in the NetworkProjectConfig."))return;
            if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");}
        }
        static void Require(bool ok,string message)
        {File.AppendAllText(Report,(ok?"PASS ":"FAIL ")+message+"\n");if(!ok)throw new Exception(message);}
        static IEnumerator Guard(IEnumerator check)
        {
            var stack=new Stack<IEnumerator>();stack.Push(check);
            while(stack.Count>0)
            {
                bool next;object value=null;
                try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}
                catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");break;}
                if(!next){(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(value is IEnumerator child){stack.Push(child);continue;}yield return value;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();Time.timeScale=1;EditorApplication.ExitPlaymode();
        }
        static IEnumerator Check()
        {
            MatchSetup.FromLobby=true;MatchSetup.StartLocalSession=false;MatchSetup.MapId="mode-buildup";MatchSetup.ModeId="demo";MatchSetup.HandCount=0;
            SceneManager.LoadScene("Game");
            yield return new WaitForSeconds(1);
            var map=Services.World as BuildUpMap;var session=Services.Session as CastlePlaySession;
            Require(map&&session&&session.Player!=null,"Game starts solo with actual scaffold map and survivor");
            session.SetMenu(false);
            Require(map.Stage==0&&map.CanAdvance&&map.Blocks.Count>=300&&map.Blocks.Count<=650,"Initial state has two arrived modules and bounded preallocated block budget: "+map.Blocks.Count);
            Require(map.GetComponentsInChildren<ScaffoldModule>().Length==2&&map.Scaffolds.All(s=>!s.PlayerSolid&&s.Body.isKinematic),"Only base modules active; all fixed scaffolds start as pass-through beacons");
            Require(map.Scaffolds.All(s=>s.transform.localPosition==(Vector3)s.gridOrigin*map.cellSize&&s.GetComponent<BoxCollider>().size==(Vector3)s.Cells*map.cellSize),"Every scaffold dimension and origin snaps to the shared 2m grid");
            Require(map.GetComponentsInChildren<ScaffoldModule>().All(m=>m.Structure.Blocks.All(b=>b.Supported)),"Authored towers, gallery and stair modules form supported magnetic structures");
            foreach(string name in new[]{"CandyScaffoldGrate","CandyScaffoldBeacon"}){var shader=Resources.Load<Shader>("CandyScenery/"+name);Require(shader&&shader.isSupported&&!ShaderUtil.ShaderHasError(shader),name+" compiles and is supported");}
            Capture("Scaffold-Beacons.png",new Vector3(39,27,-44),new Vector3(0,22,0));
            var stand=Object.Instantiate(map.scaffoldPrefab);stand.gridOrigin=new Vector3Int(30,0,0);stand.dimensions=new Vector3Int(2,1,2);stand.Configure();
            var drop=GameObject.CreatePrimitive(PrimitiveType.Cube);drop.name="Block-support probe";drop.layer=Layers.Block;drop.transform.position=new Vector3(61,4,1);var rb=drop.AddComponent<Rigidbody>();
            var hand=GameObject.CreatePrimitive(PrimitiveType.Cube);hand.name="Default-layer hand-body probe";hand.transform.position=new Vector3(62.8f,4,1);var handBody=hand.AddComponent<Rigidbody>();
            var walker=new GameObject("Survivor collision probe");walker.layer=Layers.Survivor;walker.transform.position=new Vector3(63,3,3);var cc=walker.AddComponent<CharacterController>();cc.height=1;cc.radius=.2f;cc.center=Vector3.up*.5f;
            for(int i=0;i<50;i++){cc.Move(Vector3.down*.08f);yield return new WaitForFixedUpdate();}
            Require(Mathf.Abs(rb.position.y-2.5f)<.08f&&walker.transform.position.y<1,"Beacon supports real falling blocks but survivor capsule passes through");
            Require(handBody.position.y<1,"Beacon also lets the physical hand collision layer pass through");
            handBody.position=new Vector3(62.8f,4,1);handBody.linearVelocity=Vector3.zero;
            cc.enabled=false;walker.transform.position=new Vector3(63,3,3);cc.enabled=true;stand.SetPlayerSolid(true);
            for(int i=0;i<30;i++){cc.Move(Vector3.down*.08f);yield return new WaitForFixedUpdate();}
            Require(walker.transform.position.y>1.95f&&stand.CanClimb,"Grate supports survivor capsule and exposes climb permission");
            Require(Mathf.Abs(handBody.position.y-2.5f)<.1f,"Solid grate also stops physical hand bodies");
            stand.SetPlayerSolid(false);for(int i=0;i<30;i++){cc.Move(Vector3.down*.08f);yield return new WaitForFixedUpdate();}
            Require(walker.transform.position.y<1&&!stand.CanClimb&&Mathf.Abs(rb.position.y-2.5f)<.08f,"Returning to beacon removes player collision and climb grip without losing block support");
            Object.Destroy(drop);Object.Destroy(hand);Object.Destroy(walker);Object.Destroy(stand.gameObject);
            int count=map.Blocks.Count;var controls=session.Demo as IBuildUpControls;Require(controls!=null&&controls.CanAdvance,"Host demo controls expose an authorized delivery action");controls.AdvanceStage();
            Require(map.Delivering&&map.Stage==1&&!map.CanAdvance&&map.Scaffolds.All(s=>s.PlayerSolid),"Demo command starts next tier, solidifies grates and blocks duplicate launches");
            var arriving=map.GetComponentsInChildren<ScaffoldModule>().Where(m=>m.socket.y==map.tierHeightCells+1).ToArray();
            Require(arriving.Length==2&&arriving.All(m=>m.Blocks.All(b=>!b.CanClimb&&b.Body.isKinematic)),"Whole buildings arrive guided with survivor collision disabled");
            string flight=map.SaveLayout();var replica=Object.Instantiate(Resources.Load<GameObject>("ModeMaps/Mode_buildup")).GetComponent<BuildUpMap>();replica.Rebuild();
            Require(replica.LoadLayout(flight,count,new Pose(Vector3.right*120,Quaternion.identity),Vector3.one)&&replica.Stage==map.Stage&&replica.Delivering&&!replica.CanAdvance,"Late-join layout restores in-flight stage and replica authority");
            replica.gameObject.SetActive(false);Services.World=map;
            float timeout=Time.time+7;while(map.Delivering&&Time.time<timeout)yield return null;
            Require(!map.Delivering&&map.ClimbSecondsRemaining>18&&map.Blocks.Count==count,"Delivery finishes and starts the full 20-second climb window without changing block order/count");
            Require(arriving.All(m=>Vector3.Distance(m.transform.localPosition,(Vector3)m.socket*map.cellSize)<.001f&&m.Blocks.All(b=>b.CanClimb&&b.Body.isKinematic)),"Modules land precisely on sockets and become traversable magnetic buildings");
            Capture("Scaffold-Grates.png",new Vector3(32,27,-37),new Vector3(0,18,0));
            var player=session.GetComponent<CastlePlaySession>().Player as CastleSurvivor;
            var driver=new ClimbInput();player.externallyDriven=true;player.InputDriver=driver;player.Teleport(new Vector3(-10-player.Capsule.radius-.07f,2.08f,-9),Quaternion.LookRotation(Vector3.right));
            float initial=player.FootPosition.y;bool climbed=false;float end=Time.time+1.2f;
            while(Time.time<end){climbed|=player.Climbing;yield return null;}
            Require(climbed&&player.FootPosition.y>initial+.4f,"Actual survivor controller latches and climbs the fixed pillar through normal movement input");
            player.InputDriver=null;player.externallyDriven=false;player.Teleport(map.SafeSpawnPoint(),Quaternion.identity);
            string window=map.SaveLayout();Require(replica.LoadLayout(window,count,new Pose(Vector3.right*120,Quaternion.identity),Vector3.one)&&replica.ClimbSecondsRemaining>0,"Late join restores remaining climb time");replica.gameObject.SetActive(false);Services.World=map;
            int oldStage=map.Stage;Require(!map.LoadLayout("bad",count,new Pose(),Vector3.one)&&map.Stage==oldStage,"Malformed snapshot rejected without mutating map");
            Time.timeScale=4;timeout=Time.time+24;while(map.ClimbSecondsRemaining>0&&Time.time<timeout)yield return null;Time.timeScale=1;
            Require(map.Scaffolds.All(s=>!s.PlayerSolid)&&map.CanAdvance&&arriving.All(m=>m.Blocks.All(b=>b.CanClimb)),"After the climb window, grates phase out while delivered buildings stay solid");
            var breakable=arriving[0].Structure.Blocks.First(b=>!b.foundationAnchor);breakable.Damage(100,null);yield return new WaitForFixedUpdate();
            Require(breakable.Broken&&!breakable.Body.isKinematic,"Delivered magnetic blocks remain breakable by gameplay forces");
            string broken=map.SaveLayout();Require(replica.LoadLayout(broken,count,new Pose(Vector3.right*120,Quaternion.identity),Vector3.one)&&replica.Blocks.Any(b=>b.GetComponent<MagneticBlock>().Broken),"Broken magnetic state survives layout restore");replica.gameObject.SetActive(false);Services.World=map;
            replica.SetReplica(false);replica.gameObject.SetActive(true);Require(replica.CanAdvance,"Restored replica can resume map authority after host migration");replica.gameObject.SetActive(false);Services.World=map;Object.Destroy(replica.gameObject);
            yield return ScaffoldNetworkCheck.Run(map,session,Require);
            Time.timeScale=4;
            while(map.Delivering||map.ClimbSecondsRemaining>0)yield return null;
            while(map.CanAdvance)
            {
                map.AdvanceStage();timeout=Time.time+30;
                while((map.Delivering||map.ClimbSecondsRemaining>0)&&Time.time<timeout)yield return null;
                Require(!map.Delivering&&map.ClimbSecondsRemaining==0,"Later tier completes its delivery and climb cycle: "+map.Stage);
            }
            Time.timeScale=1;
            Require(map.Stage==map.StageCount-1&&!map.CanAdvance&&map.Blocks.Count==count,"Final tier stops further advances and keeps snapshot block count stable");
            Capture("Scaffold-Complete.png",new Vector3(63,43,-69),new Vector3(0,28,0));
            map.Rebuild();yield return null;yield return new WaitForSeconds(1.1f);
            Require(map.Stage==0&&map.Blocks.Count==count&&map.CanAdvance&&map.Scaffolds.All(s=>!s.PlayerSolid),"Reset restores the original stage, support, controls and scaffold art");
            MatchSetup.FromLobby=false;MatchSetup.MapId="castle";SceneManager.LoadScene("Game");yield return new WaitForSeconds(1);
            Require(Services.World is CastleGenerator&&Services.Session.Player!=null,"Original castle Game scene still starts normally");
            Require(errors==0,"No runtime errors or exceptions");File.AppendAllText(Report,"COMPLETE. Actual collisions, delivery, controller climb, state round-trips and two Photon-runner snapshot transport verified. Separate machines and Quest hardware not tested.\n");
        }
        sealed class ClimbInput:ICastleSurvivorDriver
        {
            public CastleBotCommand ReadCommand(CastleSurvivor motor,float dt)=>new CastleBotCommand{move=Vector3.right,look=Vector3.right,climbHeld=true};
        }
        static void Capture(string name,Vector3 position,Vector3 target)
        {
            var batches=Object.FindObjectsByType<CandyCrateBatchRenderer>(FindObjectsSortMode.None).Where(b=>b.enabled).ToArray();foreach(var batch in batches)batch.enabled=false;
            var artwork=Object.FindObjectsByType<CandyCrateVisual>(FindObjectsSortMode.None).Select(v=>v.GetComponent<Renderer>()).ToArray();var hidden=artwork.Select(r=>r.forceRenderingOff).ToArray();foreach(var r in artwork)r.forceRenderingOff=false;
            var go=new GameObject("Scaffold screenshot");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.useOcclusionCulling=false;camera.fieldOfView=58;camera.transform.position=position;camera.transform.LookAt(target);
            var rt=RenderTexture.GetTemporary(1280,800,24);var old=RenderTexture.active;var image=new Texture2D(1280,800,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes(ProjectPath.Report(name),image.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image);Object.Destroy(go);for(int i=0;i<artwork.Length;i++)if(artwork[i])artwork[i].forceRenderingOff=hidden[i];foreach(var batch in batches)if(batch)batch.enabled=true;}
        }
    }
    public sealed class ScaffoldCheckDriver:MonoBehaviour{}
}

