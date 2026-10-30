using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.Core.Testing;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class DemoGrantCosmeticChecks
    {
        const string Key="DemoGrantCosmetics";
        static int checks,errors;static bool failed;
        static string Report=>ProjectPath.Report("DemoGrantCosmetics.txt");
        [Serializable] sealed class Scenes{public SceneSetup[] values;}
        static DemoGrantCosmeticChecks()
        {
            EditorApplication.update+=()=>
            {
                string path=ProjectPath.Report("DemoGrantCosmetics.request");
                if(!File.Exists(path)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
                File.Delete(path);Run();
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {checks=errors=0;failed=false;Application.logMessageReceived+=Log;EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(Guard());}
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);
                    SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));
                    MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);
                    EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Scenes>(SessionState.GetString(Key+"Scenes","")).values);
                    File.AppendAllText(Report,$"COMPLETE checks={checks} runtimeErrors={errors} failed={failed}. Desktop runtime; headset not tested.\n");
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Demo Grants and Bot Cosmetics")]
        public static void Run()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            if(setup.Any(s=>s.isLoaded&&UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty))throw new InvalidOperationException("Save edited scenes first.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{values=setup}));
            SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);SessionState.SetBool(Key,true);
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.BotTarget=0;MatchSetup.HandCount=0;
            File.WriteAllText(Report,"Real Game player grants, inventory replacement, VR menu entries and ordinary bot spawning.\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType kind)
        {if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n");}}
        static void Check(bool pass,string label)
        {File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+label+"\n");checks++;if(!pass)throw new InvalidOperationException(label);}
        static IEnumerator Guard()
        {
            var run=Test();
            try
            {
                while(true)
                {
                    bool more=false;object value=null;Exception error=null;
                    try{more=run.MoveNext();if(more)value=run.Current;}catch(Exception e){error=e;}
                    if(error!=null){failed=true;File.AppendAllText(Report,"FAIL "+error+"\n");break;}
                    if(!more)break;yield return value;
                }
            }
            finally{(run as IDisposable)?.Dispose();EditorApplication.ExitPlaymode();}
        }
        static int Bits(uint bits){int count=0;while(bits!=0){count++;bits&=bits-1;}return count;}
        static int Pieces(AvatarAppearance a)=>Bits((uint)a.accessories)+(a.skin!=0?1:0)+(a.hair!=0?1:0)+(a.eyewear!=0?1:0)+Bits((uint)a.clothing)+(a.poweredArmor?1:0);
        static IEnumerator Test()
        {
            var session=Object.FindFirstObjectByType<CastlePlaySession>();float until=Time.realtimeSinceStartup+30;
            while((session.Player==null||session.Modes==null)&&Time.realtimeSinceStartup<until)yield return null;
            Check(session.Player!=null&&session.Modes!=null,"solo Game initializes");
            session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots.Clear();if(session.Social!=null)session.Social.VoiceEnabled=false;
            var oldWorld=Services.World;var fixture=new GameObject("Grant fixture world");fixture.transform.position=new Vector3(1000,0,1000);
            var world=fixture.AddComponent<FakeWorldQuery>();world.waterHeight=-100;world.spawnPoint=new Vector3(1000,.05f,1000);world.playableBounds=new Bounds(Vector3.up*10,new Vector3(100,30,100));
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.layer=Layers.Surface;floor.transform.SetParent(fixture.transform,false);floor.transform.localPosition=Vector3.down*.5f;floor.transform.localScale=new Vector3(100,1,100);
            var body=floor.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;world.blocks.Add(body);
            try
            {
                var runtime=session.Modes;var rules=ModeSimulation.Defaults(GameModeKind.Demo);rules.island=IslandRule.Walkable;rules.countdownSeconds=0;runtime.Configure(rules);runtime.StartRound();
                var player=session.Survivor();player.Place(world.spawnPoint,Quaternion.identity);session.SetMenu(true);yield return new WaitForSeconds(.25f);
                var controls=(IDemoEquipmentControls)runtime;
                foreach(EquipmentKind kind in Enum.GetValues(typeof(EquipmentKind)))
                {
                    if(kind==EquipmentKind.None)continue;
                    var previous=runtime.EquipmentFor(runtime.LocalActorId);int drops=runtime.Frame.pickups.Length;
                    Check(controls.GiveEquipment(kind),"demo menu command grants "+kind);yield return null;
                    var state=runtime.EquipmentFor(runtime.LocalActorId);
                    Check(state.kind==kind&&runtime.Frame.equipment.Count(e=>e.actor==runtime.LocalActorId)==1,"one slot contains "+kind);
                    Check(state.leftHanded==(player.GetComponent<IBlasterControls>()?.LeftHanded??false),"grant respects dominant hand");
                    Check(kind<EquipmentKind.Blaster||state.drawn,"weapon arrives drawn: "+kind);
                    if(previous.kind!=EquipmentKind.None&&previous.kind!=kind)Check(runtime.Frame.pickups.Length==drops+1&&runtime.Frame.pickups.Any(p=>p.kind==previous.kind),"replacement drops previous item once");
                    int before=runtime.Frame.pickups.Length;Check(controls.GiveEquipment(kind)&&runtime.Frame.pickups.Length==before,"repeating "+kind+" refreshes without duplicate drops");
                }
                Check(!controls.GiveEquipment(EquipmentKind.None)&&!controls.GiveEquipment((EquipmentKind)999),"invalid equipment is rejected");
                string savedStyle=PlayerPrefs.GetString("CandyAvatar.Style.v2","");int savedAccessories=PlayerPrefs.GetInt("CandyAvatar.Accessories.v1",0);
                for(int i=0;i<6;i++)Check(session.SurvivorBots.SpawnOne(),"ordinary bot spawns");
                yield return new WaitForSeconds(.3f);
                var bots=session.SurvivorBots.Members.Select(b=>b.GetComponent<CandyPlayerAvatar>()).ToArray();
                var appearances=bots.Select(b=>b.Appearance).ToArray();
                for(int i=0;i<bots.Length;i++)
                {
                    var a=appearances[i];Check(Pieces(a)==1,"spawned bot has exactly one cosmetic choice");
                    Check(Pieces(CandyPlayerAvatar.WithStyle(a,CandyPlayerAvatar.StyleBits(a)))==1,"cosmetic survives existing style serialization");
                }
                session.SurvivorBots.MapRebuilt();yield return null;
                Check(bots.Select((b,i)=>JsonUtility.ToJson(b.Appearance)==JsonUtility.ToJson(appearances[i])).All(v=>v),"repositioning bots retains their cosmetic");
                Check(PlayerPrefs.GetString("CandyAvatar.Style.v2","")==savedStyle&&PlayerPrefs.GetInt("CandyAvatar.Accessories.v1",0)==savedAccessories,"bot cosmetics do not overwrite the player's wardrobe");
                var menuObject=new GameObject("Grant menu fixture",typeof(RectTransform));
                try
                {
                    var menu=menuObject.AddComponent<CastleWorldMenu>();menu.Build(session);
                    const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                    typeof(CastleWorldMenu).GetMethod("PointGiveEquipmentPage",flags).Invoke(menu,new object[]{session.ActiveCamera,false});
                    var labels=(UnityEngine.UI.Text[])typeof(CastleWorldMenu).GetField("labels",flags).GetValue(menu);
                    var targets=(BoxCollider[])typeof(CastleWorldMenu).GetField("targets",flags).GetValue(menu);
                    for(int i=1;i<=8;i++)Check(labels[i].text.Contains(DeadlyJenga.Presentation.CandyEquipmentView.Name((EquipmentKind)i))&&targets[i].enabled,"VR menu exposes grant control for "+(EquipmentKind)i);
                }
                finally{Object.Destroy(menuObject);}
                rules=ModeSimulation.Defaults(GameModeKind.Survival);rules.countdownSeconds=0;runtime.Configure(rules);runtime.StartRound();yield return new WaitForSeconds(.3f);Check(controls.GiveEquipment(EquipmentKind.Blaster),"instant grant also works during Survival");
                Check(errors==0,"no runtime errors");
            }
            finally{session.SurvivorBots.Clear();Object.Destroy(fixture);Services.World=oldWorld;}
        }
    }
}
