using System;
using System.Collections;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    internal static class NeedlefrostPeers
    {
        internal static IEnumerator Run(CastleGenerator map,CastlePlaySession session,Action<bool,string> check)
        {
            var config=NetworkProjectConfig.Global;
            var originalPeerMode=config.PeerMode;
            var originalPhysics=Physics.simulationMode;
            var guestScene=SceneManager.CreateScene("Needlefrost guest",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var guestRoot=new GameObject("Needlefrost received world");
            guestRoot.SetActive(false);
            SceneManager.MoveGameObjectToScene(guestRoot,guestScene);
            var host=new GameObject("Needlefrost host").AddComponent<NetworkRunner>();
            var guest=new GameObject("Needlefrost guest").AddComponent<NetworkRunner>();
            var capture=host.gameObject.AddComponent<CastleLanSession>();
            var receiver=guest.gameObject.AddComponent<CastleLanSession>();
            capture.enabled=false;receiver.enabled=false;
            try
            {
                config.PeerMode=NetworkProjectConfig.PeerModes.Multiple;
                var app=PhotonAppSettings.Global.AppSettings.GetCopy();
                app.FixedRegion="us";app.AppVersion+="/needlefrost-review";
                string room="ice-"+Guid.NewGuid().ToString("N").Substring(0,12);
                var create=host.StartGame(new StartGameArgs{GameMode=GameMode.Host,SessionName=room,PlayerCount=2,
                    IsVisible=false,CustomPhotonAppSettings=app,Scene=new NetworkSceneInfo(),
                    SceneManager=host.gameObject.AddComponent<NetworkSceneManagerDefault>()});
                yield return WaitFor(()=>create.IsCompleted,45);
                check(create.IsCompletedSuccessfully && create.Result.Ok,"Needlefrost Photon host connected");
                var join=guest.StartGame(new StartGameArgs{GameMode=GameMode.Client,SessionName=room,EnableClientSessionCreation=false,
                    CustomPhotonAppSettings=app,Scene=new NetworkSceneInfo(),SceneManager=guest.gameObject.AddComponent<NetworkSceneManagerDefault>()});
                yield return WaitFor(()=>join.IsCompleted,45);
                check(join.IsCompletedSuccessfully && join.Result.Ok,"Second Photon peer joined the private match");
                Physics.simulationMode=originalPhysics;

                var prefab=Resources.Load<GameObject>("ModeMaps/Mode_needlefrost-keep");
                var copy=Object.Instantiate(prefab,guestRoot.transform);
                var replica=copy.GetComponent<CastleGenerator>();
                var presentation=copy.GetComponent<DeadlyJenga.Polar.NeedlefrostArt>();
                if(presentation)presentation.enabled=false;
                guestRoot.SetActive(true);
                Services.World=replica;
                receiver.InitializeFusion(session,"ice-guest",true);
                Services.World=map;
                capture.InitializeFusion(session,"ice-host",false);
                bool received=false;string failure=null;
                var callbacks=new NetworkDelegates();
                callbacks.OnReliableDataReceived=(runner,source,key,data)=>
                {
                    try
                    {
                        Services.World=replica;
                        receiver.FusionApplyWorld(data.ToArray());
                    }
                    catch(Exception e){failure=e.ToString();}
                    finally{Services.World=map;received=true;}
                };
                guest.AddCallbacks(callbacks);
                var coast=map.Bodies.First(b=>Vector3.Distance(map.transform.InverseTransformPoint(b.position),new Vector3(-26,2.35f,-19))<.4f);
                var start=coast.position;
                coast.GetComponent<IPushable>().Push(Vector3.left*12,null);
                yield return new WaitForSeconds(.9f);
                float coastTravel=Vector3.Distance(coast.position,start);
                MayhemMarinaChecks.LogNeighbours(map,CastleLayout.Build(map.settings),"peer coast",coast);
                check(coastTravel>1,$"Host moved the supported coast member before guest snapshot ({coastTravel:F2} m)");
                for(int pass=0;pass<2;pass++)
                {
                    if(pass==1){map.Rebuild();yield return new WaitForSeconds(1);}
                    var packet=capture.FusionCaptureWorld();
                    var expected=CastleLanProtocol.DecodeWorld(packet);
                    received=false;
                    host.SendReliableDataToPlayer(guest.LocalPlayer,ReliableKey.FromInts(0x494345,pass,0,0),packet);
                    yield return WaitFor(()=>received,20);
                    check(received && failure==null,"Needlefrost production snapshot applied on peer "+pass+" "+failure);
                    var match=replica.Bodies.Count==expected.blocks.Length && replica.Bodies.Select((body,i)=>
                        body.isKinematic && body.gameObject.activeSelf==expected.active[i] &&
                        Vector3.Distance(body.position,expected.blocks[i].position)<.04f &&
                        Quaternion.Angle(body.rotation,expected.blocks[i].rotation)<1).All(equal=>equal);
                    check(match,pass==0?"Late guest restored coast collapse poses and authority":"Guest restored all reset block poses and authority");
                }
                var leave=guest.Shutdown();yield return WaitFor(()=>leave.IsCompleted,10);
                var stop=host.Shutdown();yield return WaitFor(()=>stop.IsCompleted,10);
            }
            finally
            {
                Services.World=map;config.PeerMode=originalPeerMode;Physics.simulationMode=originalPhysics;
                if(guest){if(guest.IsRunning)_=guest.Shutdown();Object.Destroy(guest.gameObject);}
                if(host){if(host.IsRunning)_=host.Shutdown();Object.Destroy(host.gameObject);}
                if(guestRoot)guestRoot.SetActive(false);
                if(guestScene.isLoaded)_=SceneManager.UnloadSceneAsync(guestScene);
            }
        }

        static IEnumerator WaitFor(Func<bool> done,float seconds)
        {
            float deadline=Time.realtimeSinceStartup+seconds;
            while(!done() && Time.realtimeSinceStartup<deadline)yield return null;
        }
    }
}
