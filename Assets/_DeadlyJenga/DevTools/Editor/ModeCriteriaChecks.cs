using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class ModeCriteriaChecks
    {
        static int assertions;
        static ModeCriteriaChecks()=>EditorApplication.update+=()=>
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            var request=ProjectPath.Report("ModeCriteria.request");if(!File.Exists(request))return;File.Delete(request);Run();
        };
        [MenuItem("Tools/Deadly Jenga/Validation/Check Mode Criteria")]
        public static void Run()
        {
            assertions=0;string path=ProjectPath.Report("ModeCriteria.txt");
            try
            {
                foreach(GameModeKind mode in Enum.GetValues(typeof(GameModeKind)))
                {
                    var defaults=ModeSimulation.Defaults(mode);
                    Check(ModeFrameValidation.Rules(defaults),mode+" defaults are valid");
                    Check(defaults.automaticDisasters==(mode==GameModeKind.Demo||mode==GameModeKind.Survival),mode+" deliberate disaster default");
                    Check(CandyHudContent.LiveStandings(mode)==(mode==GameModeKind.KingOfHill),mode+" focused live standings");
                    Check(!string.IsNullOrEmpty(CandyHudContent.ResultStats(new MatchPlayerFrame(),mode)),mode+" result detail");
                }
                Check(ModeSimulation.Defaults(GameModeKind.CaptureFlag).metric==ScoreMetric.FlagCaptures,"CTF personal capture criterion");
                Check(ModeSimulation.Defaults(GameModeKind.TeamTickets).metric==ScoreMetric.Eliminations,"Tickets individual eliminations criterion");
                Check(ModeSimulation.Defaults(GameModeKind.Infection).starterInfected,"Infection seeds an initial opponent");
                Check(ModeSimulation.Defaults(GameModeKind.Infection).roundSeconds>0,"Infection offers a survivor time-limit victory");
                var ctf=Create(GameModeKind.CaptureFlag);var a=ctf.Find("a");var b=ctf.Find("b");
                ctf.SetFlagHomes(new[]{Vector3.zero,Vector3.right*20});
                Observe(ctf,"a",Vector3.right*20);Observe(ctf,"b",Vector3.forward*30);ctf.Tick(.1f);
                Observe(ctf,"a",Vector3.zero);ctf.Tick(.1f);
                Check(a.frame.captures==1&&ctf.CapturesBy("a")==1,"Actual flag delivery increments actor once");
                Check(ctf.ScoreOf("a")==1&&ctf.ScoreOf("b")==0,"Team capture does not give every teammate a personal capture");
                ctf.Kill("b","a");Check(a.frame.kills==1&&b.frame.deaths==1,"Authoritative elimination/death counters");
                ctf.Kill("a","a");Check(a.frame.kills==1&&a.frame.deaths==1,"Self-death does not grant a kill");
                var restored=new ModeSimulation(ctf.Rules);restored.Restore(ctf.Snapshot());
                Check(restored.CapturesBy("a")==1&&restored.Find("b").frame.deaths==1,"Migration preserves individual capture and death totals");
                restored.StartRound();Check(restored.CapturesBy("a")==0&&restored.Find("a").frame.kills==0,"New round resets contributions");
                var returns=Create(GameModeKind.CaptureFlag);returns.SetFlagHomes(new[]{Vector3.zero,Vector3.right*20});
                Observe(returns,"a",Vector3.forward*30);Observe(returns,"b",Vector3.zero);returns.Tick(.1f);
                returns.Kill("b",null);Observe(returns,"b",Vector3.forward*30);Observe(returns,"a",Vector3.zero);returns.Tick(.1f);returns.Tick(.1f);
                Check(returns.Find("a").frame.flagReturns==1,"Bringing own dropped flag home records a return");
                var claims=Create(GameModeKind.BlockControl);var custom=claims.Rules.Copy();custom.claimSeconds=.1f;claims.Tune(custom);
                claims.Observe("a",Vector3.zero,Quaternion.identity,2,1,true,0);Observe(claims,"b",Vector3.forward*30);claims.Tick(.2f);claims.Tick(.2f);
                Check(claims.Find("a").frame.blocksClaimed==1,"Actual ownership transition counts one completed claim");
                Check(claims.Find("b").frame.blocksClaimed==0,"Non-contributing actor receives no personal claim");
                custom=claims.Rules.Copy();custom.claimDecaySeconds=1;claims.Tune(custom);
                Observe(claims,"a",Vector3.forward*30);claims.Tick(.5f);
                var migratingClaims=new ModeSimulation(claims.Rules);migratingClaims.Restore(claims.Snapshot());
                Check(migratingClaims.EmptySecondsOf(0)==.5f,"Authority migration preserves an abandoned claim's existing decay clock");
                migratingClaims.Tick(.5f);Check(migratingClaims.OwnerOf(0)==-1,"Migrated abandoned claim expires at its original deadline");
                var hill=Create(GameModeKind.KingOfHill);hill.Hill=Vector3.zero;
                Observe(hill,"a",Vector3.zero);Observe(hill,"b",Vector3.right*20);hill.Tick(.5f);
                Check(hill.Find("a").frame.hillSeconds==.5f,"Grounded hill time collected independently");
                custom=hill.Rules.Copy();custom.metric=ScoreMetric.Eliminations;hill.Tune(custom);hill.Tick(.5f);
                Check(hill.Find("a").frame.hillSeconds==1&&hill.ScoreOf("a")==0,"Changing ranking preserves actual hill collection");
                var tickets=Create(GameModeKind.TeamTickets);custom=tickets.Rules.Copy();custom.tickets=0;custom.roundSeconds=10;tickets.Tune(custom);
                foreach(string id in new[]{"c","d"}){tickets.Register(id,Color.red,true,false);tickets.Find(id).frame.team=0;}
                for(int i=0;i<21;i++)tickets.Tick(.5f);
                Check(tickets.WinnerTeam==0,"Tickets timeout favors three active lives over one when both reserve pools are empty");
                tickets=Create(GameModeKind.TeamTickets);custom=tickets.Rules.Copy();custom.tickets=1;custom.roundSeconds=10;tickets.Tune(custom);
                foreach(string id in new[]{"c","d","e","f"}){tickets.Register(id,Color.blue,true,false);tickets.Find(id).frame.team=1;}
                tickets.Kill("b",null);
                for(int i=0;i<21;i++)tickets.Tick(.5f);
                Check(tickets.WinnerTeam==1,"Tickets timeout compares active plus reserve lives, not reserve count alone");
                Check(tickets.Snapshot().teams[1].tickets==0,"Remaining-life scoring preserves the separate reserve-respawn HUD value");
                var infection=Create(GameModeKind.Infection);custom=infection.Rules.Copy();custom.starterInfected=false;custom.roundSeconds=10;infection.Tune(custom);
                for(int i=0;i<21;i++)infection.Tick(.5f);
                Check(infection.Winner=="Survivors","Infection deadline awards survivor faction");
                var mystery=Create(GameModeKind.MurderMystery);mystery.Find("a").frame.kills=7;mystery.Find("a").frame.deaths=2;
                var secret=mystery.Snapshot();Check(secret.topTen.Length==0&&secret.players.All(p=>p.kills==0&&p.deaths==0&&p.metric==0),"Live Mystery snapshot does not expose identifying kill totals");
                mystery.FinishMystery("Innocents");Check(mystery.Snapshot().players.First(p=>p.id=="a").kills==7,"Mystery totals become available after round conclusion");
                File.WriteAllText(path,"PASS "+assertions+" mode criteria model assertions. Closed-menu XR input requires CheckClosedMenuInputPlay in Play Mode. No headset claim.\n");
            }
            catch(Exception e){File.WriteAllText(path,"FAIL after "+assertions+" assertions\n"+e);throw;}
        }
        public static IEnumerator CheckClosedMenuInputPlay()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Closed-menu XR input regression requires Play Mode; Editor input updates intentionally do not process actions.");
            const string layout="ModeCriteriaXRController";
            InputDevice device=null;InputAction action=null;GameObject root=null;var previous=Services.Session;
            try
            {
                InputSystem.RegisterLayout("{\"name\":\""+layout+"\",\"extend\":\"XRController\",\"canRunInBackground\":true,\"controls\":[{\"name\":\"trigger\",\"layout\":\"Axis\"}]}");
                device=InputSystem.AddDevice(layout);InputSystem.SetDeviceUsage(device,CommonUsages.RightHand);
                root=new GameObject("Closed-menu HUD input fixture");var session=root.AddComponent<DeadlyJenga.Core.Testing.FakeSession>();session.SetMenu(false);
                action=CandyHudContent.CreateSelectInput();
                InputSystem.QueueDeltaStateEvent((AxisControl)device["trigger"],1f);yield return null;
                Check(!session.MenuOpen&&!session.MenuSelectPressed&&!session.MenuSelectHeld,"Fixture has no menu-gated selection available");
                Check(CandyHudContent.SelectPressed(action)&&CandyHudContent.SelectHeld(action),"Actual XR controller trigger still selects and holds closed-menu HUD context actions");
            }
            finally
            {
                action?.Dispose();if(device!=null)InputSystem.RemoveDevice(device);InputSystem.RemoveLayout(layout);
                if(root)UnityEngine.Object.DestroyImmediate(root);Services.Session=previous;
            }
        }
        static ModeSimulation Create(GameModeKind mode)
        {
            var rules=ModeSimulation.Defaults(mode);rules.countdownSeconds=0;rules.starterInfected=false;rules.automaticDisasters=false;
            var sim=new ModeSimulation(rules);sim.Register("a",Color.red,false,true);sim.Register("b",Color.blue,true,false);if(ModeSimulation.TeamMode(mode)){sim.Find("a").frame.team=0;sim.Find("b").frame.team=1;}sim.StartRound();sim.Tick(.01f);return sim;
        }
        static void Observe(ModeSimulation sim,string id,Vector3 point)=>sim.Observe(id,point,Quaternion.identity,2,1,true,-1);
        static void Check(bool value,string message){assertions++;if(!value)throw new InvalidOperationException(message);}
    }
}
