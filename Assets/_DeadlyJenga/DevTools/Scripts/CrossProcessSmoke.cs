#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CastleSocial;
using DeadlyJenga.World.Hazards;

namespace DeadlyJenga.CastlePrototype
{
    // Opt-in development-player test. Inert unless launched with both explicit smoke arguments.
    public sealed class CrossProcessSmoke : MonoBehaviour
    {
        string folder,role;bool done;float deadline;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            var args=Environment.GetCommandLineArgs();string role=args.FirstOrDefault(a=>a.StartsWith("--dj-smoke="));
            string folder=args.FirstOrDefault(a=>a.StartsWith("--dj-smoke-dir="));
            if(role==null||folder==null)return;
            var go=new GameObject("Explicit multiplayer smoke test");DontDestroyOnLoad(go);
            var test=go.AddComponent<CrossProcessSmoke>();test.role=role.Substring(11);test.folder=Path.GetFullPath(folder.Substring(15));
            Directory.CreateDirectory(test.folder);test.deadline=Time.realtimeSinceStartup+180;
            Application.runInBackground=true;Application.logMessageReceived+=test.Log;test.StartCoroutine(test.Guard(test.Check()));
        }
        string Report=>Path.Combine(folder,role+".txt");
        string Mark(string name)=>Path.Combine(folder,name);
        void Log(string message,string trace,LogType type)
        {
            if(done||type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            // Explicit deliberate disconnect can race Fusion's current cloud request.
            File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");
        }
        void Update(){if(!done&&Time.realtimeSinceStartup>deadline)Finish(false,"Timed out");}
        void Finish(bool ok,string message)
        {if(done)return;done=true;File.AppendAllText(Report,(ok?"COMPLETE ":"FAILED ")+message+"\n");Application.logMessageReceived-=Log;Application.Quit(ok?0:1);}
        void Require(bool ok,string message){File.AppendAllText(Report,(ok?"PASS ":"FAIL ")+message+"\n");if(!ok)throw new Exception(message);}
        IEnumerator Until(Func<bool> condition,string message,float seconds=45)
        {float until=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<until)yield return null;Require(condition(),message);}
        IEnumerator Guard(IEnumerator root)
        {
            var work=new Stack<IEnumerator>();work.Push(root);
            while(work.Count>0&&!done)
            {
                bool next=false;object value=null;Exception error=null;
                try{next=work.Peek().MoveNext();if(next)value=work.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){Finish(false,error.ToString());yield break;}
                if(!next){(work.Pop() as IDisposable)?.Dispose();continue;}if(value is IEnumerator child){work.Push(child);continue;}yield return value;
            }
            if(!done)Finish(!File.ReadAllText(Report).Contains("ERROR "),"Separate-process multiplayer run");
        }
        IEnumerator Check()
        {
            File.WriteAllText(Report,"Independent player "+role+"\n");
            yield return Until(()=>IslandLobby.Current&&NetworkActors.Alive(IslandLobby.Current.Player),"Independent process opens playable lobby");
            var room=IslandRoomConnection.Instance;
            if(role=="host")
            {
                var task=room.Host();yield return Until(()=>task.IsCompleted&&room.IsHost,"Host connects to real Fusion");
                File.WriteAllText(Mark("code"),room.Code);
                yield return Until(()=>room.HumanCount==2&&IslandLobby.Current.PeerCount==1,"Independent guest avatar joins host island");
                room.SetSharedControls(true);room.Launch();
                yield return Until(()=>FindFirstObjectByType<CastlePlaySession>(),"Host transitions lobby to Castle Demo");
                var session=FindFirstObjectByType<CastlePlaySession>();session.SetMenu(true);
                yield return Until(()=>session.Network.HumanCount==2,"Guest completes castle snapshot handshake");
                yield return Until(()=>session.SurvivorBots.Count==1,"Guest shared-control command creates exactly one authoritative AI");
                Require(session.GetComponent<SocialWorld>().IsAuthority,"Host retains AI voice authority with shared UI");
                Require(DemoDisasters.Execute(session,"Tsunami"),"Host triggers network tsunami");
                yield return Until(()=>File.Exists(Mark("wave-seen")),"Independent guest receives active wave state");
                DemoDisasters.StopAll(session);
                yield return Until(()=>File.Exists(Mark("wave-cleared")),"Independent guest clears wave visuals after stop");
                yield return new WaitForSeconds(4);
                File.WriteAllText(Mark("host-leaving"),"ready");
                task=room.Leave();yield return Until(()=>task.IsCompleted,"Original host leaves Game cleanly");
            }
            else
            {
                yield return Until(()=>File.Exists(Mark("code")),"Host publishes room address");string code=File.ReadAllText(Mark("code"));
                yield return Until(()=>room.Discovery!=null&&room.Discovery.Nearby.Any(e=>e.message.code==code),"Separate process discovers host over UDP",12);
                var task=room.Join(code);yield return Until(()=>task.IsCompleted&&room.Connected,"Guest joins the discovered Fusion room");
                yield return Until(()=>FindFirstObjectByType<CastlePlaySession>()&&FindFirstObjectByType<CastlePlaySession>().Network&&!FindFirstObjectByType<CastlePlaySession>().Network.WaitingForWorld,"Guest transitions and receives castle snapshot",65);
                var session=FindFirstObjectByType<CastlePlaySession>();session.SetMenu(true);var sync=session.GetComponent<CastleFusionSession>();
                yield return Until(()=>sync.CanUseDemoControls,"Shared Demo powers arrive on independent guest");
                Require(!session.GetComponent<SocialWorld>().IsAuthority,"Shared UI does not create a second AI voice authority");
                Require(Services.World.Blocks.All(b=>b&&b.isKinematic),"Independent guest has replica-only block physics");
                CastleDemoCommands.Run(session,CastleDemoAction.AddSurvivor);
                yield return Until(()=>sync.Demo.survivors==1,"Guest receives authoritative AI population update");
                yield return Until(()=>Tracked.Waves.Any(w=>w.Wave.active),"Active tsunami is replicated to guest");File.WriteAllText(Mark("wave-seen"),"yes");
                yield return Until(()=>!Tracked.Waves.Any(w=>w.Wave.active),"Stopped tsunami is removed on guest");File.WriteAllText(Mark("wave-cleared"),"yes");
                yield return Until(()=>File.Exists(Mark("host-leaving")),"Host begins intentional departure");
                yield return Until(()=>room.IsHost&&!room.Busy,"Guest becomes host inside actual Game process",70);
                session=FindFirstObjectByType<CastlePlaySession>();
                Require(session&&session.Network&&!session.Network.IsClient,"Promoted guest keeps actual Castle Game active");
                Require(Services.World.Blocks.Count>0&&Services.World.Blocks.All(b=>b&&!b.isKinematic),"Promoted guest resumes authoritative block physics");
                Require(session.GetComponent<SocialWorld>().IsAuthority,"AI voice authority follows real process host migration");
                yield return new WaitForSeconds(3);
                task=room.Leave();yield return Until(()=>task.IsCompleted,"Migrated host leaves cleanly");
                yield return Until(()=>IslandLobby.Current&&NetworkActors.Alive(IslandLobby.Current.Player),"Migrated host returns to usable island");
            }
        }
    }
}
#endif
