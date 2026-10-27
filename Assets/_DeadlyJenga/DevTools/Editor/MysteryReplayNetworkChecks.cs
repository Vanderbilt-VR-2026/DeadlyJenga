using System;
using System.Collections;
using System.Linq;
using System.Text;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static class MysteryReplayNetworkChecks
    {
        public static IEnumerator Run(MatchFrame live,MatchFrame results,string victim,string observer,Action<bool,string> check)
        {
            int blocks=Services.World?.Blocks?.Count??0;
            check(ModeFrameValidation.Frame(live,blocks)&&ModeFrameValidation.Frame(results,blocks),"Replay transport uses validated actual live and results snapshots");
            var config=NetworkProjectConfig.Global;var original=config.PeerMode;var physics=Physics.simulationMode;NetworkRunner host=null,guest=null;
            int received=-1;MysteryReplayCue receivedCue=null;string error=null;
            try
            {
                config.PeerMode=NetworkProjectConfig.PeerModes.Multiple;
                host=new GameObject("Replay Photon host").AddComponent<NetworkRunner>();guest=new GameObject("Replay Photon guest").AddComponent<NetworkRunner>();
                var app=PhotonAppSettings.Global.AppSettings.GetCopy();app.FixedRegion="us";app.AppVersion=(app.AppVersion??"")+"/replay-validation";string room="replay-"+Guid.NewGuid().ToString("N").Substring(0,12);
                var start=host.StartGame(new StartGameArgs{GameMode=Fusion.GameMode.Host,SessionName=room,PlayerCount=2,CustomPhotonAppSettings=app,Scene=new NetworkSceneInfo(),SceneManager=host.gameObject.AddComponent<NetworkSceneManagerDefault>()});
                yield return Wait(()=>start.IsCompleted,45);check(!start.IsFaulted&&!start.IsCanceled&&start.Result.Ok,"Replay host connects through real Photon transport");
                var callbacks=new NetworkDelegates();callbacks.OnReliableDataReceived=(runner,player,key,data)=>
                {
                    key.GetInts(out int channel,out int kind,out int sequence,out _);if(channel!=0x43415354||(kind!=12&&kind!=15))return;
                    try
                    {
                        var json=Encoding.UTF8.GetString(kind==12?CastleFusionWire.Decompress(data.ToArray()):data.ToArray());
                        if(kind==12){var f=JsonUtility.FromJson<MatchFrame>(json);if(!ModeFrameValidation.Frame(f,blocks))throw new Exception("Invalid public replay packet");receivedCue=f.mysteryReplay;}
                        else{var f=JsonUtility.FromJson<MysteryPrivateFrame>(json);if(!ModeFrameValidation.Replay(f.replay))throw new Exception("Invalid private replay packet");receivedCue=f.replay;}
                    }
                    catch(Exception e){error=e.ToString();}
                    received=sequence;
                };guest.AddCallbacks(callbacks);
                var join=guest.StartGame(new StartGameArgs{GameMode=Fusion.GameMode.Client,SessionName=room,EnableClientSessionCreation=false,CustomPhotonAppSettings=app,Scene=new NetworkSceneInfo(),SceneManager=guest.gameObject.AddComponent<NetworkSceneManagerDefault>()});
                yield return Wait(()=>join.IsCompleted,45);check(!join.IsFaulted&&!join.IsCanceled&&join.Result.Ok,"Independent replay guest joins the real Photon room");Physics.simulationMode=physics;
                string[] jsons={JsonUtility.ToJson(live),victim,observer,JsonUtility.ToJson(results),JsonUtility.ToJson(results)};
                for(int i=0;i<jsons.Length;i++)
                {
                    int kind=i==1||i==2?15:12;var bytes=Encoding.UTF8.GetBytes(jsons[i]);if(kind==12)bytes=CastleFusionWire.Compress(bytes);
                    host.SendReliableDataToPlayer(guest.LocalPlayer,ReliableKey.FromInts(0x43415354,kind,i,0),bytes);int index=i;
                    yield return Wait(()=>received==index,20);check(error==null&&received==i,"Replay packet "+i+" crosses actual "+(kind==15?"private 15":"public 12")+" channel and validates");
                    check(i==0||i==2?(receivedCue?.sequence??0)==0:receivedCue?.murderer==results.mysteryReplay.murderer&&receivedCue.roundEnd==(i>=3),"Correct replay visibility at transport step "+i);
                }
                var stop=guest.Shutdown();yield return Wait(()=>stop.IsCompleted,15);if(guest)Object.Destroy(guest.gameObject);guest=null;stop=host.Shutdown();yield return Wait(()=>stop.IsCompleted,15);if(host)Object.Destroy(host.gameObject);host=null;
            }
            finally
            {
                if(guest){var go=guest.gameObject;_=guest.Shutdown();if(go)Object.Destroy(go);}if(host){var go=host.gameObject;_=host.Shutdown();if(go)Object.Destroy(go);}
                config.PeerMode=original;Physics.simulationMode=physics;
            }
            check(config.PeerMode==original&&Physics.simulationMode==physics,"Replay transport restores peer and physics settings");
        }
        static IEnumerator Wait(Func<bool> ready,float seconds)
        {float end=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<end)yield return null;if(!ready())throw new TimeoutException("Replay transport timed out");}
    }
}
