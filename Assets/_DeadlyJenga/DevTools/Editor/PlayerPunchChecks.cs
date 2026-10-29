using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CharacterNetwork;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class PlayerPunchChecks
    {
        const string Key="PlayerPunchChecks";
        static int errors;
        static string Report=>ProjectPath.Report("PlayerPunch.txt");
        static PlayerPunchChecks()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {
                    errors=0;Application.logMessageReceived+=Log;
                    EditorApplication.delayCall+=()=>{var go=new GameObject("Punch animation check");Object.DontDestroyOnLoad(go);go.AddComponent<PunchCheckDriver>().StartCoroutine(Guard(Check()));};
                }
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);
                    var original=SessionState.GetString(Key+"Original","");if(File.Exists(original))EditorSceneManager.OpenScene(original);
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Player Punch Animation %&u")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            File.WriteAllText(Report,"Actual Game scene desktop/bot punch animation checks\n");SessionState.SetString(Key+"Original",SceneManager.GetActiveScene().path);SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");}}
        static void Require(bool ok,string message)
        {File.AppendAllText(Report,(ok?"PASS ":"FAIL ")+message+"\n");if(!ok)throw new Exception(message);}
        static IEnumerator Guard(IEnumerator check)
        {
            while(true)
            {
                object value=null;bool next;
                try{next=check.MoveNext();if(next)value=check.Current;}catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");break;}
                if(!next)break;yield return value;
            }
            (check as IDisposable)?.Dispose();EditorApplication.ExitPlaymode();
        }
        static IEnumerator Check()
        {
            yield return new WaitForSeconds(1);
            var session=Services.Session as CastlePlaySession;var player=session.Player as CastleSurvivor;var avatar=player.GetComponent<CandyPlayerAvatar>();
            session.SetMenu(false);var rules=session.Modes.Rules;rules.punchEnabled=true;rules.punchCooldown=.8f;session.Modes.Configure(rules);yield return null;
            var rest=avatar.ReadFrame();Require(player.RequestPunch(player.ViewPose.position,player.ViewPose.rotation*Vector3.forward,2),"Desktop punch is accepted through the existing cooldown/mode action");
            Require(!player.RequestPunch(player.ViewPose.position,Vector3.forward,2),"Animation does not bypass the punch cooldown");
            yield return new WaitForSeconds(.13f);avatar.RefreshNow();var strike=avatar.ReadFrame();
            Require(Vector3.Distance(strike.right.position,rest.right.position)>.06f,"Desktop wrist visibly extends during the strike");
            Require(strike.right.skeletonTracked&&Vector3.Distance(strike.right.skeleton.Joint(7),strike.right.skeleton.Joint(4))<Vector3.Distance(rest.right.skeleton.Joint(7),rest.right.skeleton.Joint(4)),"Desktop fingers close into a fist");
            Require(Vector3.Distance(avatar.Rig.RightPalm.position,strike.right.position)<.01f,"Shared rendered avatar actually receives the punching hand pose");
            var again=avatar.ReadFrame();Require(Vector3.Distance(again.right.position,strike.right.position)<.00001f,"Before-render/network sampling does not accumulate or restart the animation");
            using(var stream=new MemoryStream())
            {
                using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))avatar.WriteState(writer);stream.Position=0;
                using var reader=new BinaryReader(stream);CharacterWire.ReadSurvivor(reader,out var remote,out _);
                Require(Vector3.Distance(remote.right.position,strike.right.position)<.005f&&remote.right.skeletonTracked,"Existing network packet carries the animated fist without changing wire format");
            }
            yield return new WaitForSeconds(.3f);var recovered=avatar.ReadFrame();
            Vector3 RestOffset(CandyAvatarFrame f)=>Quaternion.Inverse(f.headRotation)*(f.right.position-f.head);
            Require(Vector3.Distance(RestOffset(recovered),RestOffset(rest))<.005f,"Hand returns to its ordinary rest pose even as the character moves");
            session.SurvivorBots.SpawnOne();yield return new WaitForSeconds(.2f);
            var member=session.SurvivorBots.Members.First();var bot=member.GetComponent<CastleSurvivor>();var botAvatar=member.GetComponent<CandyPlayerAvatar>();
            var brain=member.GetComponent<DeadlyJenga.CastleBots.CastleBotBrain>();brain.enabled=false;
            bot.ApplyModeRules(rules);var botRest=botAvatar.ReadFrame();botAvatar.OverridePose(botRest);
            Require(bot.RequestPunch(bot.ViewPose.position,bot.ViewPose.rotation*Vector3.forward,2),"Bot punch uses the same action event");
            yield return new WaitForSeconds(.13f);botAvatar.RefreshExternal(.016f);var botStrike=botAvatar.LastFrame;
            Require(Vector3.Distance(botStrike.right.position,botRest.right.position)>.06f&&botStrike.right.skeletonTracked,"Bot-supplied pose gets the same visible fist animation");botAvatar.ClearPoseOverride();
            Require(Vector3.Distance(botAvatar.Rig.RightPalm.position,botStrike.right.position)<.01f,"Bot rendered palm follows the animated pose");
            using(var stream=new MemoryStream())
            {
                using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))botAvatar.WriteState(writer);stream.Position=0;
                using var reader=new BinaryReader(stream);CharacterWire.ReadSurvivor(reader,out var remote,out _);
                Require(Vector3.Distance(remote.right.position,botStrike.right.position)<.005f,"Bot network poses match its rendered punch rather than its default resting hands");
            }
            yield return new WaitForSeconds(.9f);
            bot.InputSuspended=true;Vector3 forward=player.ViewPose.rotation*Vector3.forward;
            bot.Teleport(player.transform.position+forward*.45f,Quaternion.identity);Physics.SyncTransforms();
            Require(player.RequestPunch(player.ViewPose.position,forward,2),"Close-range punch is accepted with a real survivor target");
            var targetField=typeof(CandyPlayerAvatar).GetField("punchTarget",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Require((Transform)targetField.GetValue(avatar)==bot.transform,"Visual strike selects the nearby survivor body as its contact target");
            yield return new WaitForSeconds(.9f);
            var vrFlag=typeof(CastleSurvivor).GetField("<IsVR>k__BackingField",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);vrFlag.SetValue(player,true);
            var trackedRest=avatar.ReadFrame();Require(player.RequestPunch(player.ViewPose.position,Vector3.forward,2),"VR punch action remains available");var trackedStrike=avatar.ReadFrame();
            Require(Vector3.Distance(trackedRest.right.position,trackedStrike.right.position)<.0001f,"Procedural desktop animation never overrides a VR hand");vrFlag.SetValue(player,false);
            yield return new WaitForSeconds(.9f);player.ConfigureActions(false,false);
            Require(!player.RequestPunch(player.ViewPose.position,Vector3.forward,2),"Modes that disable punching still reject the action");
            Require(errors==0,"Game scene completes without runtime errors");File.AppendAllText(Report,"COMPLETE. Desktop/bot render poses, cooldown, packet serialization and VR animation bypass checked. Real headset/network peer rendering not tested.\n");
        }
    }
    public sealed class PunchCheckDriver:MonoBehaviour{}
}
