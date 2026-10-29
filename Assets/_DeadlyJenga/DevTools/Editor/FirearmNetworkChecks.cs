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
    public static class FirearmNetworkChecks
    {
        // Two real transport peers exercise the production compressed snapshot channel.
        public static IEnumerator Run(MatchFrame baseline,MatchFrame accepted,Action<bool,string> check)
        {
            baseline=Copy(baseline);accepted=Copy(accepted);
            int blockCount=Services.World?.Blocks?.Count??0;
            Require(ModeFrameValidation.Frame(baseline,blockCount)&&ModeFrameValidation.Frame(accepted,blockCount),"Actual pistol and shotgun revolver frames pass production validation",check);
            Require(baseline.equipment.Any(e=>e.kind==EquipmentKind.SheriffHammer&&!e.shotgun)&&accepted.equipment.Any(e=>e.kind==EquipmentKind.SheriffHammer&&e.shotgun),"Transport fixture captures actual pistol and shotgun revolver state",check);
            var config=NetworkProjectConfig.Global;var originalMode=config.PeerMode;var originalPhysics=Physics.simulationMode;
            NetworkRunner host=null,client=null;
            bool receivedShotgun=false;int received=-1;string failure=null;
            try
            {
                config.PeerMode=NetworkProjectConfig.PeerModes.Multiple;
                host=new GameObject("Firearm state Photon host").AddComponent<NetworkRunner>();
                client=new GameObject("Firearm state Photon guest").AddComponent<NetworkRunner>();
                string room="firearm-"+Guid.NewGuid().ToString("N").Substring(0,12);
                var app=PhotonAppSettings.Global.AppSettings.GetCopy();app.FixedRegion="us";app.AppVersion=(app.AppVersion??"")+"/action-validation";
                var start=host.StartGame(new StartGameArgs{GameMode=Fusion.GameMode.Host,SessionName=room,PlayerCount=2,CustomPhotonAppSettings=app,Scene=new NetworkSceneInfo(),SceneManager=host.gameObject.AddComponent<NetworkSceneManagerDefault>()});
                yield return Until(()=>start.IsCompleted,"Independent Photon firearm host startup completes",45,check);
                Require(!start.IsFaulted&&!start.IsCanceled&&start.Result.Ok,"Real Photon firearm host connects",check);
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
                        receivedShotgun=frame.equipment.Any(e=>e.kind==EquipmentKind.SheriffHammer&&e.shotgun);
                    }
                    catch(Exception e){failure=e.ToString();}
                    received=sequence;
                };
                client.AddCallbacks(callbacks);
                var join=client.StartGame(new StartGameArgs{GameMode=Fusion.GameMode.Client,SessionName=room,EnableClientSessionCreation=false,CustomPhotonAppSettings=app,Scene=new NetworkSceneInfo(),SceneManager=client.gameObject.AddComponent<NetworkSceneManagerDefault>()});
                yield return Until(()=>join.IsCompleted,"Independent Photon firearm guest startup completes",45,check);
                Require(!join.IsFaulted&&!join.IsCanceled&&join.Result.Ok,"Second real Photon peer joins the firearm room",check);
                Physics.simulationMode=originalPhysics;

                var transferred=new[]{baseline,accepted,accepted};
                for(int i=0;i<transferred.Length;i++)
                {
                    var packet=CastleFusionWire.Compress(Encoding.UTF8.GetBytes(JsonUtility.ToJson(transferred[i])));
                    host.SendReliableDataToPlayer(client.LocalPlayer,ReliableKey.FromInts(0x43415354,12,i,0),packet);
                    int sequence=i;
                    yield return Until(()=>received==sequence,"Firearm state packet "+i+" crosses real Photon channel 12",20,check);
                    Require(failure==null,"Transported action packet passes production decompression and validation: "+i+(failure==null?"":" "+failure),check);
                    Require(receivedShotgun==(i!=0),i==0?"Remote peer receives precision revolver mode":i==1?"Remote peer receives rapid shotgun revolver mode":"Duplicate snapshot preserves shotgun mode",check);
                }
                var shutdown=client.Shutdown();yield return Until(()=>shutdown.IsCompleted,"Photon firearm guest shuts down",15,check);if(client)Object.Destroy(client.gameObject);client=null;
                shutdown=host.Shutdown();yield return Until(()=>shutdown.IsCompleted,"Photon firearm host shuts down",15,check);if(host)Object.Destroy(host.gameObject);host=null;
            }
            finally
            {
                if(client){var root=client.gameObject;_=client.Shutdown();if(root)Object.Destroy(root);}
                if(host){var root=host.gameObject;_=host.Shutdown();if(root)Object.Destroy(root);}
                config.PeerMode=originalMode;Physics.simulationMode=originalPhysics;
            }
            Require(config.PeerMode==originalMode&&Physics.simulationMode==originalPhysics,"Temporary multiple-peer and physics settings are restored",check);
        }

        static MatchFrame Copy(MatchFrame source)=>JsonUtility.FromJson<MatchFrame>(JsonUtility.ToJson(source));
        static void Require(bool condition,string message,Action<bool,string> check)
        {check(condition,message);if(!condition)throw new Exception(message);}
        static IEnumerator Until(Func<bool> ready,string message,float timeout,Action<bool,string> check)
        {float end=Time.realtimeSinceStartup+timeout;while(!ready()&&Time.realtimeSinceStartup<end)yield return null;Require(ready(),message,check);}
    }
}
