using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyPlayers;
using Object=UnityEngine.Object;

namespace DeadlyJenga.CastleBots.Editor
{
    [InitializeOnLoad]
    public static class CastleBotInstaller
    {
        const string Root="Assets/CastleBots/",Output="ArtDirection/CastleBots/",Key="CastleBotChecks";
        static readonly List<string> report=new List<string>();
        static CastleBotInstaller()
        {
            EditorApplication.update+=()=>
            {
                if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Output+"install.request"))return;
                File.Move(Output+"install.request",Output+"install.request.consumed-"+DateTime.UtcNow.Ticks);
                try{InstallAndTest();}catch(Exception e){File.WriteAllText(Output+"ERROR.txt",e.ToString());Debug.LogException(e);}
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))
                {SessionState.SetBool(Key,false);EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(Checks());}
            };
        }
        [MenuItem("Tools/Castle Bots/Install Profiles And Verify")]
        public static void InstallAndTest()
        {
            if(Application.isPlaying||EditorSceneManager.GetActiveScene().isDirty||EditorSceneManager.GetActiveScene().path!="Assets/Scenes/Game.unity")throw new InvalidOperationException("Open the saved Game scene in Edit Mode before running NPC checks.");
            Directory.CreateDirectory(Root+"Resources/CastleBots");Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            for(int i=0;i<4;i++)if(!AssetDatabase.LoadAssetAtPath<CastleBotPersonality>(Root+"Resources/CastleBots/"+i+".asset"))AssetDatabase.CreateAsset(CastleBotPersonality.Create(i),Root+"Resources/CastleBots/"+i+".asset");
            Directory.CreateDirectory(Root+"Resources/CastleHandBots");AssetDatabase.Refresh();
            for(int i=0;i<4;i++)if(!AssetDatabase.LoadAssetAtPath<CastleHandBotPersonality>(Root+"Resources/CastleHandBots/"+i+".asset"))AssetDatabase.CreateAsset(CastleHandBotPersonality.Create(i),Root+"Resources/CastleHandBots/"+i+".asset");
            AssetDatabase.SaveAssets();SessionState.SetString(Key+"Hash",HashScene());SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static string HashScene()=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes("Assets/Scenes/Game.unity")));
        static void Check(bool ok,string label){report.Add((ok?"PASS ":"FAIL ")+label);File.WriteAllLines(Output+"Validation.txt",report);Debug.Log(report.Last());}
        sealed class ManualDriver:ICastleSurvivorDriver
        {
            public CastleBotCommand command;
            public CastleBotCommand ReadCommand(CastleSurvivor motor,float dt){var next=command;command.jump=false;return next;}
        }
        static IEnumerator Checks()
        {
            Application.runInBackground=true;report.Clear();yield return new WaitForSeconds(1);
            var session=Object.FindFirstObjectByType<CastlePlaySession>();session.SetMenu(true);var population=session.Bots;
            Check(population&&population.Count==0&&!population.autoFill,"Demo starts without unsolicited bots or auto-fill.");
            var first=population.SpawnBot();yield return new WaitForSeconds(.5f);
            Check(first&&population.Count==1,"Public spawn function creates one NPC.");
            Check(first&&first.Motor.externallyDriven&&ReferenceEquals(first.Motor.InputDriver,first)&&!session.Player.externallyDriven,"NPC input owns only its motor; local human retains normal controls.");
            Check(first.GetComponentsInChildren<Camera>(true).All(c=>!c.enabled)&&first.GetComponentsInChildren<AudioListener>(true).All(l=>!l.enabled)&&!first.Motor.IsVR,"NPC has no rendering camera, audio listener or headset pose driver.");
            var avatar=first.GetComponent<CandyPlayerAvatar>();
            Check(avatar&&avatar.Rig&&avatar.visualPrefab==session.Player.GetComponent<CandyPlayerAvatar>().visualPrefab,"NPC uses the same current avatar prefab as human survivors.");
            Check(first.Motor.Capsule.gameObject.layer==CastleBlock.SurvivorLayer&&avatar.Rig.GetComponentsInChildren<Collider>().Length==0,"NPC retains player collision isolation and visual-only avatar hands.");
            Vector3 start=first.transform.position;float explored=0;
            for(int i=0;i<20;i++){yield return new WaitForSeconds(.25f);explored=Mathf.Max(explored,Vector3.Distance(start,first.transform.position));}
            Check(explored>.4f,"Unscripted NPC explores actual castle surfaces while the menu is open.");
            population.FillToTarget();
            Check(population.Count==9&&population.autoFill,"Fill-to-ten creates nine NPCs alongside one human.");
            Check(!population.SpawnBot()&&population.Count==9,"Manual spawn respects population capacity.");
            bool spaced=true;for(int i=0;i<population.Count;i++)for(int j=i+1;j<population.Count;j++)if(Vector3.Distance(population.Bots[i].transform.position,population.Bots[j].transform.position)<.35f)spaced=false;
            Check(spaced,"Top spawns distribute NPCs rather than stacking inside one another.");
            yield return new WaitForSeconds(.5f);
            population.SetHumanPlayerCount(4);Check(population.Count==6,"Human join hook removes excess filler NPCs.");
            population.SetHumanPlayerCount(1);yield return null;Check(population.Count==9,"Human leave hook replenishes filler NPCs.");
            population.ClearBots();yield return null;Check(population.Count==0&&!population.autoFill,"Clear removes NPCs and stops automatic refill.");
            population.CyclePersonality();var bot=population.SpawnBot();yield return null;
            Check(bot.Personality.displayName=="Careful"&&bot.Personality.bravery<.3f,"Personality selector applies editable preset to the next NPC.");
            // A controlled two-crate obstacle verifies actual shared jump/vault behaviour.
            Rigidbody Block(string name,Vector3 center,Vector3 size)
            {
                var go=new GameObject(name);go.transform.position=center;go.transform.localScale=size;go.AddComponent<BoxCollider>();var rb=go.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;go.AddComponent<CastleBlock>().SyncSurface();session.generator.Bodies.Add(rb);return rb;
            }
            var floor=Block("NPC validation base",new Vector3(60,79,0),new Vector3(12,2,12));
            var crate=Block("NPC validation 2m crate",new Vector3(60,81,2.4f),new Vector3(6,2,2));
            bot.Motor.Teleport(new Vector3(60,80.06f,.6f),Quaternion.identity);bot.Spawned(false);
            bot.diagnostics=true;File.WriteAllText(Output+"Decisions.txt","");
            int initialVaults=bot.Motor.VaultCount;
            for(int i=0;i<32;i++){yield return new WaitForSeconds(.25f);File.AppendAllText(Output+"Decisions.txt",bot.transform.position+" "+bot.DebugState+" intent="+bot.Intent+" goal="+bot.Goal+"\n");}
            File.WriteAllText(Output+"Traversal.txt","Position "+bot.transform.position+"; jumps "+bot.Jumps+"; vaults "+bot.Motor.VaultCount+"; intent "+bot.Intent+"; goal "+bot.Goal);
            Check(bot.Jumps>0,"Autonomous brain presses normal jump for a higher crate.");
            Check(bot.Motor.VaultCount>initialVaults,"Autonomous second press uses a real reachable ledge vault.");
            Check(bot.transform.position.y>81.8f,"NPC reaches the top of a two-metre crate using shared movement.");
            // Verify motor input isolation without depending on planner randomness.
            var driver=new ManualDriver{command=new CastleBotCommand{move=Vector3.right,look=Vector3.right}};bot.enabled=false;bot.Motor.InputDriver=driver;
            bot.Motor.Teleport(new Vector3(60,80.05f,-3),Quaternion.identity);yield return new WaitForSeconds(.4f);start=bot.transform.position;
            yield return new WaitForSeconds(.5f);Check(bot.transform.position.x>start.x+1,"Injected NPC command moves through the human CharacterController motor.");
            driver.command=default;yield return new WaitForSeconds(.3f);start=bot.transform.position;yield return new WaitForSeconds(.3f);
            Check(Vector3.ProjectOnPlane(bot.transform.position-start,Vector3.up).magnitude<.02f,"No-command NPC does not inherit desktop/controller movement.");
            bot.Motor.InputDriver=bot;bot.enabled=true;
            crate.rotation=Quaternion.Euler(40,0,0);crate.transform.rotation=crate.rotation;crate.GetComponent<CastleBlock>().SyncSurface();Physics.SyncTransforms();
            Check(!CastleBotNavigation.Landing(bot.Motor,new Vector3(60,82,2.4f),out _),"Tilted former landing is rejected by live physics queries.");
            var falling=new GameObject("Predicted falling hazard").AddComponent<Rigidbody>();falling.transform.position=new Vector3(100,100,0);falling.useGravity=false;falling.linearVelocity=Vector3.down*2;session.generator.Bodies.Add(falling);
            yield return new WaitForSeconds(.3f);Check(population.ThreatCost(new Vector3(100,96,0))>population.ThreatCost(new Vector3(130,96,0)),"Falling-block prediction penalizes its projected landing area.");session.generator.Bodies.Remove(falling);Object.Destroy(falling.gameObject);
            session.generator.Bodies.Remove(crate);session.generator.Bodies.Remove(floor);Object.Destroy(crate.gameObject);Object.Destroy(floor.gameObject);yield return null;
            bot.Motor.Teleport(new Vector3(0,-8,0),Quaternion.identity);yield return new WaitForSeconds(.2f);
            Check(bot.Respawning&&!bot.Motor.Capsule.enabled,"Fallen NPC enters knockout state instead of moving underwater.");
            yield return new WaitForSeconds(3);
            Check(!bot.Respawning&&bot.Respawns>0&&bot.Motor.Capsule.enabled&&bot.transform.position.y>0,"Knocked-out NPC respawns on a clear surviving castle surface.");
            population.FillToTarget();yield return new WaitForSeconds(6);
            Check(population.Count==9&&population.Bots.All(b=>b&&b.GetComponent<CandyPlayerAvatar>().Rig),"Nine-bot local play smoke test retains live shared avatars.");
            session.SetRole(true);yield return new WaitForSeconds(.7f);
            var threat=session.Hand.motor.transform.position;
            Check(population.ThreatCost(threat)>population.ThreatCost(threat+Vector3.right*30),"Survival sensor assigns greater danger near the giant hand.");
            session.SetRole(false);session.SetMenu(true);
            session.ResetCastle();yield return new WaitForSeconds(.5f);
            Check(population.Count==9&&population.Bots.All(b=>b.transform.position.y>0),"Castle rebuild places existing NPCs onto fresh surfaces.");
            Capture(session);
            yield return HandChecks(session);
            Check(HashScene()==SessionState.GetString(Key+"Hash",""),"Saved castle scene is byte-for-byte unchanged.");
            population.ClearBots();Export();
            File.WriteAllText(Output+"COMPLETE.txt",report.Count(x=>x.StartsWith("PASS"))+" passed, "+report.Count(x=>x.StartsWith("FAIL"))+" failed. Local editor simulation; real Quest performance and multiplayer integration require separate validation.");
            EditorApplication.isPlaying=false;
        }
        static IEnumerator HandChecks(CastlePlaySession session)
        {
            var population=session.HandBots;var survivors=session.Bots;
            Check(population&&population.Count==0,"AI hands are opt-in and start unspawned.");
            var hand=population.SpawnHand();yield return new WaitForSeconds(.8f);
            Check(hand&&population.Count==1&&survivors.Count==8,"Spawning an AI hand reserves one of the ten participant slots.");
            Check(hand.Actor.motor.externallyDriven&&ReferenceEquals(hand.Actor.motor.InputDriver,hand)&&ReferenceEquals(hand.Bridge.PoseSource,hand),"AI flight and articulated pose use shared external-input interfaces.");
            Check(hand.Actor.GetComponentsInChildren<Camera>(true).All(c=>!c.enabled)&&hand.Actor.GetComponentsInChildren<AudioListener>(true).All(c=>!c.enabled)&&!hand.Actor.follow.enabled,"AI hand does not render a camera, play a listener or move the player's view.");
            Check(hand.GetComponent<CandyTrackingSource>()==null&&!session.Hand.motor.externallyDriven,"AI hand never reads the human's hand tracking or takes over their controller.");
            Check(hand.Bridge.PhysicalHand&&hand.Bridge.PhysicalHand.IsInteractive&&hand.Bridge.PhysicalHand.skin.sharedMesh==hand.Bridge.prefab.skin.sharedMesh,"Spawned AI uses the current continuous hand mesh and contact rig.");
            var open=CastleHandBotBrain.Articulate(false,0);var closed=CastleHandBotBrain.Articulate(false,.85f);float lengthError=0;
            for(int f=0;f<5;f++)for(int j=0;j<3;j++)lengthError=Mathf.Max(lengthError,Mathf.Abs(Vector3.Distance(open.Joint(f*4+j),open.Joint(f*4+j+1))-Vector3.Distance(closed.Joint(f*4+j),closed.Joint(f*4+j+1))));
            Check(lengthError<.0025f,"AI finger animation preserves tracked phalanx lengths rather than shrinking fingers.");
            // Real shared crate, isolated from the castle so contact displacement is measurable.
            var block=Object.Instantiate(session.generator.blockPrefab);block.name="AI hand physical contact fixture";block.transform.SetPositionAndRotation(new Vector3(60,70,0),Quaternion.identity);block.transform.localScale=session.generator.settings.blockSize;
            var body=block.GetComponent<Rigidbody>();body.position=block.transform.position;body.rotation=Quaternion.identity;body.mass=session.generator.mass;body.useGravity=false;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;block.GetComponent<CastleBlock>().SyncSurface();
            hand.Bridge.ResetExternalRig();hand.Actor.Place(new Vector3(67,70.15f,0),Quaternion.LookRotation(Vector3.left));hand.SetTarget(body);
            var actorStart=hand.Actor.motor.Body.position;var blockStart=body.position;float displacement=0;
            for(int i=0;i<48;i++){yield return new WaitForSeconds(.25f);displacement=Mathf.Max(displacement,Vector3.Distance(body.position,blockStart));}
            File.WriteAllText(Output+"HandContact.txt","AI attacks: "+hand.Attacks+"; crate displacement: "+displacement+" m; intent: "+hand.Intent+"; wrist: "+hand.ContactPosition);
            Check(hand.Attacks>0&&Vector3.Distance(actorStart,hand.Actor.motor.Body.position)>.5f,"Autonomous hand flies, lines up and performs a physical attack.");
            Check(displacement>.08f,"AI hand contact displaces a real castle-mass crate.");
            Check(body.GetComponents<Joint>().Length==0&&body.transform.parent==null,"AI attacks do not attach, parent or teleport the target crate.");
            var physical=hand.Bridge.PhysicalHand;
            Check(physical&&physical.bodies.All(b=>!float.IsNaN(b.position.x)&&b.linearVelocity.magnitude<=physical.settings.maxSpeed+.1f)&&physical.MaximumAppliedForce<=Mathf.Max(physical.settings.palmForce,physical.settings.fingerForce)+.01f,"AI contact retains the human hand's velocity and force limits.");
            Check(survivors.ThreatCost(hand.ContactPosition)>survivors.ThreatCost(hand.ContactPosition+Vector3.right*40),"Survivor NPCs perceive AI hands as moving threats.");
            hand.Paused=true;yield return new WaitForSeconds(.7f);Check(!physical.IsInteractive,"Pausing an AI pose source safely releases physical contacts.");
            block.SetActive(false);Object.Destroy(block);hand.Paused=false;hand.Restart();yield return new WaitForSeconds(.8f);
            Check(hand.Bridge.PhysicalHand&&hand.Bridge.PhysicalHand.IsInteractive,"AI hand resumes through the same safe acquisition path.");
            string selected=population.SelectedName;population.CyclePersonality();var second=population.SpawnHand();yield return new WaitForSeconds(.7f);
            Check(second&&second.Personality.displayName!=selected,"Hand personality selector applies to the next spawned AI hand.");
            int configuredLimit=population.maximumHands;population.maximumHands=3;var third=population.SpawnHand();yield return new WaitForSeconds(.7f);
            Check(third&&population.Count==3&&!population.SpawnHand()&&survivors.Count==6,"More than two AI hands coexist; configurable cap keeps total fill at ten.");population.maximumHands=configuredLimit;
            session.ResetCastle();yield return new WaitForSeconds(1);
            Check(population.Count==3&&population.Hands.All(h=>h.Bridge.PhysicalHand&&h.Bridge.PhysicalHand.IsInteractive),"Castle rebuild resets multiple AI hand contact rigs without stale targets.");
            CaptureHands(session);
            survivors.SetHumanPlayerCount(9);yield return null;
            Check(population.Count==1&&survivors.Count==0,"Human join reconciliation also removes excess AI hand participants.");
            survivors.SetHumanPlayerCount(1);population.ClearHands();yield return null;
            Check(population.Count==0&&survivors.Count==9,"Clearing AI hands releases their rigs and refills survivor slots.");
        }
        static void CaptureHands(CastlePlaySession session)
        {
            var camera=new GameObject("AI hand proof camera").AddComponent<Camera>();camera.fieldOfView=55;
            var top=session.generator.transform.position+Vector3.up*(session.generator.settings.keepCourses*session.generator.settings.blockSize.y-3);
            camera.transform.position=top+new Vector3(30,10,-32);camera.transform.LookAt(top);
            var rt=new RenderTexture(1400,1000,24);var previous=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var texture=new Texture2D(1400,1000,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1400,1000),0,0);texture.Apply();File.WriteAllBytes(Output+"ai-hands-castle.png",texture.EncodeToPNG());RenderTexture.active=previous;Object.Destroy(texture);Object.Destroy(rt);Object.Destroy(camera.gameObject);
        }
        static void Capture(CastlePlaySession session)
        {
            var camera=new GameObject("NPC proof camera").AddComponent<Camera>();camera.fieldOfView=55;camera.nearClipPlane=.05f;
            var highest=session.generator.Bodies.Where(b=>b).OrderByDescending(b=>b.position.y).First().position;
            camera.transform.position=highest+new Vector3(9,6,-10);camera.transform.LookAt(highest+Vector3.up);
            var rt=new RenderTexture(1400,1000,24);var previous=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var texture=new Texture2D(1400,1000,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1400,1000),0,0);texture.Apply();File.WriteAllBytes(Output+"npc-castle.png",texture.EncodeToPNG());RenderTexture.active=previous;
            Object.Destroy(texture);Object.Destroy(rt);Object.Destroy(camera.gameObject);
        }
        [MenuItem("Tools/Castle Bots/Export Module")]
        public static void Export()
        {
            Directory.CreateDirectory(Output);
            AssetDatabase.ExportPackage(new[]{Root.TrimEnd('/'),"Assets/Scripts/CastleSurvivor.cs","Assets/Scripts/CastlePlaySession.cs","Assets/Scripts/CastleWorldMenu.cs","Assets/CandyPlayers/CandyPlayerAvatar.cs","Assets/Prefabs/CastleSurvivor.prefab","Assets/Prefabs/CastleFlyingHand.prefab","Assets/HandDemo/Scripts/FlyingHandController.cs","Assets/HandDemo/Scripts/FlyingHandInputSource.cs","Assets/CandyPhysicsHands"},Output+"CastleBots.unitypackage",ExportPackageOptions.Recurse|ExportPackageOptions.IncludeDependencies);
        }
    }
}
