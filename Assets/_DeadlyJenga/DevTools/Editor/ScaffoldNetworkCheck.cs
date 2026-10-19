using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;
using Object=UnityEngine.Object;
using P=DeadlyJenga.CastlePrototype.CastleLanProtocol;

namespace DeadlyJenga.DevTools.Editor
{
    public static class ScaffoldNetworkCheck
    {
        public static IEnumerator Run(BuildUpMap map,CastlePlaySession session,Action<bool,string> check)
        {
            var config=NetworkProjectConfig.Global;var original=config.PeerMode;var physics=Physics.simulationMode;
            NetworkRunner host=null,client=null;GameObject copy=null,captureObject=null;
            bool received=false;string failure=null;int packets=0;
            try
            {
                config.PeerMode=NetworkProjectConfig.PeerModes.Multiple;
                host=new GameObject("Scaffold Photon host probe").AddComponent<NetworkRunner>();
                client=new GameObject("Scaffold Photon client probe").AddComponent<NetworkRunner>();
                string room="scaffold-check-"+Guid.NewGuid().ToString("N").Substring(0,12);
                var app=PhotonAppSettings.Global.AppSettings.GetCopy();app.FixedRegion="us";app.AppVersion=(app.AppVersion??"")+"/scaffold-validation";
                var start=host.StartGame(new StartGameArgs{GameMode=GameMode.Host,SessionName=room,PlayerCount=2,CustomPhotonAppSettings=app,Scene=new NetworkSceneInfo(),SceneManager=host.gameObject.AddComponent<NetworkSceneManagerDefault>()});
                float until=Time.realtimeSinceStartup+45;while(!start.IsCompleted&&Time.realtimeSinceStartup<until)yield return null;
                check(start.IsCompleted&&!start.IsFaulted&&start.Result.Ok,"Independent Photon scaffold test host connects");
                var join=client.StartGame(new StartGameArgs{GameMode=GameMode.Client,SessionName=room,EnableClientSessionCreation=false,CustomPhotonAppSettings=app,Scene=new NetworkSceneInfo(),SceneManager=client.gameObject.AddComponent<NetworkSceneManagerDefault>()});
                until=Time.realtimeSinceStartup+45;while(!join.IsCompleted&&Time.realtimeSinceStartup<until)yield return null;
                check(join.IsCompleted&&!join.IsFaulted&&join.Result.Ok,"Second Photon runner joins the same scaffold test session");
                Physics.simulationMode=physics;
                copy=Object.Instantiate(Resources.Load<GameObject>("ModeMaps/Mode_buildup"));var replica=copy.GetComponent<BuildUpMap>();replica.Rebuild();replica.SetReplica(true);Services.World=map;
                var callbacks=new NetworkDelegates();callbacks.OnReliableDataReceived=(runner,source,key,data)=>
                {
                    try
                    {
                        var snapshot=P.DecodeWorld(data.ToArray());
                        if(!replica.LoadLayout(snapshot.settings,snapshot.blocks.Length,new Pose(snapshot.origin.position+Vector3.right*120,snapshot.origin.rotation),snapshot.originScale))throw new Exception("Scaffold rejected transmitted layout");
                        replica.SetReplica(true);
                        for(int i=0;i<replica.Blocks.Count;i++){var b=replica.Blocks[i];b.position=snapshot.blocks[i].position+Vector3.right*120;b.rotation=snapshot.blocks[i].rotation;b.gameObject.SetActive(snapshot.active[i]);}
                        Physics.SyncTransforms();packets++;received=true;
                    }
                    catch(Exception e){failure=e.ToString();received=true;}
                    finally{Services.World=map;}
                };
                client.AddCallbacks(callbacks);
                captureObject=new GameObject("Actual world snapshot capture");var capture=captureObject.AddComponent<CastleLanSession>();capture.InitializeFusion(session,"scaffold-test-host",false);
                // Send actual production world packets, not a simplified fixture serializer.
                foreach(int pass in new[]{0,1,2})
                {
                    if(pass==1){map.AdvanceStage();yield return new WaitForSeconds(.5f);}
                    if(pass==2){until=Time.realtimeSinceStartup+8;while(map.Delivering&&Time.realtimeSinceStartup<until)yield return null;}
                    var packet=capture.FusionCaptureWorld();var expected=P.DecodeWorld(packet);int expectedStage=map.Stage;bool expectedFlight=map.Delivering,expectedSolid=map.Scaffolds[0].PlayerSolid;
                    received=false;host.SendReliableDataToPlayer(client.LocalPlayer,ReliableKey.FromInts(0x53434146,pass,0,0),packet);
                    until=Time.realtimeSinceStartup+20;while(!received&&Time.realtimeSinceStartup<until)yield return null;
                    check(received&&failure==null,"Production scaffold world packet reaches independent Fusion client, phase "+pass+(failure==null?"":": "+failure));
                    check(replica.Stage==expectedStage&&replica.Delivering==expectedFlight&&replica.Scaffolds.All(s=>s.PlayerSolid==expectedSolid),"Received scaffold phase, collisions and stage match host, phase "+pass);
                    var different=Enumerable.Range(0,replica.Blocks.Count).Where(i=>replica.Blocks[i].gameObject.activeInHierarchy&&Vector3.Distance(replica.Blocks[i].position-Vector3.right*120,expected.blocks[i].position)>.03f).ToArray();
                    string detail=string.Join("; ",different.Take(3).Select(i=>i+" got="+(replica.Blocks[i].position-Vector3.right*120)+" expected="+expected.blocks[i].position));
                    check(replica.Blocks.Count==expected.blocks.Length&&different.Length==0,"Received active module poses preserve production block ordering, phase "+pass+" "+detail);
                    copy.SetActive(false);Services.World=map;
                }
                check(packets==3,"Two connected Photon runners exchange waiting, delivering and climb-window map snapshots");
                var stopClient=client.Shutdown();until=Time.realtimeSinceStartup+10;while(!stopClient.IsCompleted&&Time.realtimeSinceStartup<until)yield return null;
                var stopHost=host.Shutdown();until=Time.realtimeSinceStartup+10;while(!stopHost.IsCompleted&&Time.realtimeSinceStartup<until)yield return null;
            }
            finally
            {
                config.PeerMode=original;Physics.simulationMode=physics;Services.World=map;
                if(client){_ = client.Shutdown();Object.Destroy(client.gameObject);}
                if(host){_ = host.Shutdown();Object.Destroy(host.gameObject);}
                if(copy)Object.Destroy(copy);if(captureObject)Object.Destroy(captureObject);
            }
        }
    }
}
