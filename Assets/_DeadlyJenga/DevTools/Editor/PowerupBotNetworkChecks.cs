using System;
using System.Collections;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using DeadlyJenga.CastlePrototype;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static class PowerupBotNetworkChecks
    {
        public static IEnumerator Run(MatchFrame[] frames,int blockCount,Action<bool,string> check)
        {
            foreach(var frame in frames)check(ModeFrameValidation.Frame(frame,blockCount),"Actual random-pickup snapshot passes production validation");
            var config=NetworkProjectConfig.Global;var mode=config.PeerMode;var physics=Physics.simulationMode;NetworkRunner host=null,guest=null;int received=-1;string error=null;
            PowerupBotChecks.TransportActive=true;
            try
            {
                config.PeerMode=NetworkProjectConfig.PeerModes.Multiple;
                host=new GameObject("Powerup validation host").AddComponent<NetworkRunner>();guest=new GameObject("Powerup validation guest").AddComponent<NetworkRunner>();
                var app=PhotonAppSettings.Global.AppSettings.GetCopy();app.FixedRegion="us";app.AppVersion=(app.AppVersion??"")+"/powerup-validation";string room="powerup-"+Guid.NewGuid().ToString("N").Substring(0,12);
                var start=host.StartGame(new StartGameArgs{GameMode=Fusion.GameMode.Host,SessionName=room,PlayerCount=2,CustomPhotonAppSettings=app,Scene=new NetworkSceneInfo(),SceneManager=host.gameObject.AddComponent<NetworkSceneManagerDefault>()});
                yield return Until(()=>start.IsCompleted,45);check(!start.IsFaulted&&!start.IsCanceled&&start.Result.Ok,"Actual Photon powerup host connects");
                var callbacks=new NetworkDelegates();callbacks.OnReliableDataReceived=(runner,source,key,data)=>
                {
                    key.GetInts(out int channel,out int kind,out int sequence,out _);if(channel!=0x43415354||kind!=13)return;
                    try
                    {
                        var frame=JsonUtility.FromJson<MatchFrame>(Encoding.UTF8.GetString(CastleFusionWire.Decompress(data.ToArray())));
                        if(!ModeFrameValidation.Frame(frame,blockCount))throw new Exception("Transported powerup state failed production validation");
                        var expected=frames[sequence];
                        if(frame.randomPickupCount!=expected.randomPickupCount||JsonUtility.ToJson(frame.powerupSpawns)!=JsonUtility.ToJson(expected.powerupSpawns))throw new Exception("Random pickup settings/count changed in transport");
                        foreach(var pickup in expected.pickups.Where(p=>p.randomSpawn))
                        {var remote=frame.pickups.Single(p=>p.id==pickup.id);if(!remote.randomSpawn||remote.supportBlock!=pickup.supportBlock||remote.supportLocal!=pickup.supportLocal||remote.position!=pickup.position||remote.expiresIn!=pickup.expiresIn)throw new Exception("Anchor/lifetime changed in transport");}
                    }
                    catch(Exception e){error=e.ToString();}
                    received=sequence;
                };guest.AddCallbacks(callbacks);
                var join=guest.StartGame(new StartGameArgs{GameMode=Fusion.GameMode.Client,SessionName=room,EnableClientSessionCreation=false,CustomPhotonAppSettings=app,Scene=new NetworkSceneInfo(),SceneManager=guest.gameObject.AddComponent<NetworkSceneManagerDefault>()});
                yield return Until(()=>join.IsCompleted,45);check(!join.IsFaulted&&!join.IsCanceled&&join.Result.Ok,"A second actual Photon peer joins");Physics.simulationMode=physics;
                for(int i=0;i<frames.Length;i++)
                {
                    var packet=CastleFusionWire.Compress(Encoding.UTF8.GetBytes(JsonUtility.ToJson(frames[i])));host.SendReliableDataToPlayer(guest.LocalPlayer,ReliableKey.FromInts(0x43415354,13,i,0),packet);int sequence=i;
                    yield return Until(()=>received==sequence,20);check(error==null,"Real Photon channel preserves random state packet "+i+(error==null?"":" "+error));
                }
                var guestRoot=guest.gameObject;var stop=guest.Shutdown();yield return Until(()=>stop.IsCompleted,15);if(guestRoot)Object.Destroy(guestRoot);guest=null;
                var hostRoot=host.gameObject;stop=host.Shutdown();yield return Until(()=>stop.IsCompleted,15);if(hostRoot)Object.Destroy(hostRoot);host=null;
            }
            finally
            {
                if(guest){var root=guest.gameObject;_=guest.Shutdown();if(root)Object.Destroy(root);}if(host){var root=host.gameObject;_=host.Shutdown();if(root)Object.Destroy(root);}
                config.PeerMode=mode;Physics.simulationMode=physics;PowerupBotChecks.TransportActive=false;
            }
        }
        static IEnumerator Until(Func<bool> condition,float seconds){float end=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<end)yield return null;if(!condition())throw new Exception("Photon fixture timed out");}
    }
}
