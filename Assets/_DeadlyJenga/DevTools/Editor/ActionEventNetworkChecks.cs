using System;
using System.Collections;
using System.Linq;
using System.Text;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;
using DeadlyJenga.Modes;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static class ActionEventNetworkChecks
    {
        // Real Photon transport peers; migration cursor fixtures below do not simulate a host election.
        public static IEnumerator Run(IMatchRuntime owner,MatchFrame baseline,MatchFrame accepted,Action<bool,string> check)
        {
            baseline=Copy(baseline);accepted=Copy(accepted);
            int blockCount=Services.World?.Blocks?.Count??0;
            Require(owner!=null&&ModeFrameValidation.Frame(baseline,blockCount)&&ModeFrameValidation.Frame(accepted,blockCount),"Production action baseline and accepted snapshots validate",check);
            Require(accepted.revision>baseline.revision,"Accepted capture advances the production match revision",check);
            int expectedWeapons=accepted.equipmentActions.Count(a=>a.id>baseline.equipmentSequence&&a.remaining>0);
            int expectedEnemies=accepted.enemyActions.Count(a=>a.id>baseline.enemyActionSequence&&a.remaining>0);
            Require(expectedWeapons>0&&expectedEnemies>0,"Accepted authority snapshots contain new weapon and NPC actions",check);
            Require(baseline.equipmentEpoch==accepted.equipmentEpoch&&baseline.enemyActionEpoch==accepted.enemyActionEpoch,"Authority event epochs are stable between accepted captures",check);
            Require(accepted.enemyActions.Any(a=>!accepted.players.Any(p=>p.id==a.actor)),"NPC events are represented independently of the player scoreboard",check);

            var config=NetworkProjectConfig.Global;var originalMode=config.PeerMode;var originalPhysics=Physics.simulationMode;
            NetworkRunner host=null,client=null;
            var weaponCursor=new CandyEquipmentActionCursor();var enemyCursor=new EnemyActionPlayback();
            int weapons=0,enemies=0,received=-1;string failure=null;
            try
            {
                config.PeerMode=NetworkProjectConfig.PeerModes.Multiple;
                host=new GameObject("Action event Photon host").AddComponent<NetworkRunner>();
                client=new GameObject("Action event Photon guest").AddComponent<NetworkRunner>();
                string room="actions-"+Guid.NewGuid().ToString("N").Substring(0,12);
                var app=PhotonAppSettings.Global.AppSettings.GetCopy();app.FixedRegion="us";app.AppVersion=(app.AppVersion??"")+"/action-validation";
                var start=host.StartGame(new StartGameArgs{GameMode=Fusion.GameMode.Host,SessionName=room,PlayerCount=2,CustomPhotonAppSettings=app,Scene=new NetworkSceneInfo(),SceneManager=host.gameObject.AddComponent<NetworkSceneManagerDefault>()});
                yield return Until(()=>start.IsCompleted,"Independent Photon action host startup completes",45,check);
                Require(!start.IsFaulted&&!start.IsCanceled&&start.Result.Ok,"Real Photon action host connects",check);
                var callbacks=new NetworkDelegates();
                callbacks.OnReliableDataReceived=(runner,source,key,data)=>
                {
                    key.GetInts(out int channel,out int kind,out int sequence,out _);
                    if(channel!=0x43415354||kind!=12)return;
                    try
                    {
                        var bytes=CastleFusionWire.Decompress(data.ToArray());
                        var frame=JsonUtility.FromJson<MatchFrame>(Encoding.UTF8.GetString(bytes));
                        if(!ModeFrameValidation.Frame(frame,blockCount))throw new Exception("Production validator rejected transported match frame");
                        weaponCursor.Read(frame,a=>weapons++);enemyCursor.Consume(owner,frame,a=>enemies++);
                    }
                    catch(Exception e){failure=e.ToString();}
                    received=sequence;
                };
                client.AddCallbacks(callbacks);
                var join=client.StartGame(new StartGameArgs{GameMode=Fusion.GameMode.Client,SessionName=room,EnableClientSessionCreation=false,CustomPhotonAppSettings=app,Scene=new NetworkSceneInfo(),SceneManager=client.gameObject.AddComponent<NetworkSceneManagerDefault>()});
                yield return Until(()=>join.IsCompleted,"Independent Photon action guest startup completes",45,check);
                Require(!join.IsFaulted&&!join.IsCanceled&&join.Result.Ok,"Second real Photon peer joins the action room",check);
                Physics.simulationMode=originalPhysics;

                var transferred=new[]{baseline,accepted,accepted};
                for(int i=0;i<transferred.Length;i++)
                {
                    var packet=CastleFusionWire.Compress(Encoding.UTF8.GetBytes(JsonUtility.ToJson(transferred[i])));
                    host.SendReliableDataToPlayer(client.LocalPlayer,ReliableKey.FromInts(0x43415354,12,i,0),packet);
                    int sequence=i;
                    yield return Until(()=>received==sequence,"Match action packet "+i+" crosses real Photon channel 12",20,check);
                    Require(failure==null,"Transported action packet passes production decompression and validation: "+i+(failure==null?"":" "+failure),check);
                    Require(weapons==(i==0?0:expectedWeapons)&&enemies==(i==0?0:expectedEnemies),i==0?"First received snapshot silently establishes both action baselines":i==1?"New accepted weapon and NPC actions are consumed exactly once":"Duplicate reliable snapshot replays neither sounds nor bullet trails",check);
                }
                CheckCursors(owner,accepted,expectedWeapons,expectedEnemies,blockCount,check);
                var shutdown=client.Shutdown();yield return Until(()=>shutdown.IsCompleted,"Photon action guest shuts down",15,check);if(client)Object.Destroy(client.gameObject);client=null;
                shutdown=host.Shutdown();yield return Until(()=>shutdown.IsCompleted,"Photon action host shuts down",15,check);if(host)Object.Destroy(host.gameObject);host=null;
            }
            finally
            {
                if(client){var root=client.gameObject;_=client.Shutdown();if(root)Object.Destroy(root);}
                if(host){var root=host.gameObject;_=host.Shutdown();if(root)Object.Destroy(root);}
                config.PeerMode=originalMode;Physics.simulationMode=originalPhysics;
            }
            Require(config.PeerMode==originalMode&&Physics.simulationMode==originalPhysics,"Temporary multiple-peer and physics settings are restored",check);
        }

        static void CheckCursors(IMatchRuntime owner,MatchFrame accepted,int weaponCount,int enemyCount,int blockCount,Action<bool,string> check)
        {
            var weapon=new CandyEquipmentActionCursor();var enemy=new EnemyActionPlayback();int weapons=0,enemies=0;
            weapon.Read(accepted,a=>weapons++);enemy.Consume(owner,accepted,a=>enemies++);
            Require(weapons==0&&enemies==0,"Late join silently baselines recent shots and already dead NPC events",check);

            var restored=Copy(accepted);restored.equipmentActions=Array.Empty<EquipmentActionFrame>();restored.enemyActions=Array.Empty<EnemyActionFrame>();
            weapon.Read(restored,a=>weapons++);enemy.Consume(owner,restored,a=>enemies++);
            Require(weapons==0&&enemies==0,"Migration fixture preserves epochs and high-water marks without replaying cleared history",check);
            var continued=Copy(accepted);
            foreach(var item in continued.equipmentActions)item.id+=accepted.equipmentSequence;
            foreach(var item in continued.enemyActions)item.id+=accepted.enemyActionSequence;
            continued.equipmentSequence+=accepted.equipmentSequence;continued.enemyActionSequence+=accepted.enemyActionSequence;
            Require(ModeFrameValidation.Frame(continued,blockCount),"Post-migration sequence fixture remains a valid production match frame",check);
            weapon.Read(continued,a=>weapons++);enemy.Consume(owner,continued,a=>enemies++);
            Require(weapons==accepted.equipmentActions.Length&&enemies==accepted.enemyActions.Length,"First new sequence after authority restoration is consumed once",check);

            var newSession=Copy(continued);newSession.equipmentEpoch=Guid.NewGuid().ToString("N");newSession.enemyActionEpoch=Guid.NewGuid().ToString("N");
            int beforeWeapons=weapons,beforeEnemies=enemies;
            weapon.Read(newSession,a=>weapons++);enemy.Consume(owner,newSession,a=>enemies++);
            Require(weapons==beforeWeapons&&enemies==beforeEnemies,"Changed session epochs establish silent baselines instead of replaying old action history",check);

            var invalid=Copy(accepted);invalid.equipmentSequence=-1;
            Require(!ModeFrameValidation.Frame(invalid,blockCount),"Production decoder rejects a negative weapon action sequence",check);
            invalid=Copy(accepted);invalid.enemyActionSequence=-1;
            Require(!ModeFrameValidation.Frame(invalid,blockCount),"Production decoder rejects a negative NPC action sequence",check);
            invalid=Copy(accepted);invalid.enemyActions=new EnemyActionFrame[65];
            Require(!ModeFrameValidation.Frame(invalid,blockCount),"Production decoder rejects NPC event histories above their bounded capacity",check);
            invalid=Copy(accepted);invalid.enemyActions[0].position=new Vector3(float.NaN,0,0);
            Require(!ModeFrameValidation.Frame(invalid,blockCount),"Production decoder rejects non-finite NPC sound positions",check);
        }
        static MatchFrame Copy(MatchFrame source)=>JsonUtility.FromJson<MatchFrame>(JsonUtility.ToJson(source));
        static void Require(bool condition,string message,Action<bool,string> check)
        {check(condition,message);if(!condition)throw new Exception(message);}
        static IEnumerator Until(Func<bool> ready,string message,float timeout,Action<bool,string> check)
        {float end=Time.realtimeSinceStartup+timeout;while(!ready()&&Time.realtimeSinceStartup<end)yield return null;Require(ready(),message,check);}
    }
}
