using System;
using System.Collections;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    internal static class FossilBasinPeer
    {
        // The same production world packet used by the session, delivered between separate Fusion runners.
        public static IEnumerator Receive(FossilBasinMap map, CastlePlaySession session, Action<bool,string> record)
        {
            var config=NetworkProjectConfig.Global;
            var oldMode=config.PeerMode;
            var simulation=Physics.simulationMode;
            NetworkRunner host=null,peer=null;
            GameObject sender=null,copy=null;
            try
            {
                config.PeerMode=NetworkProjectConfig.PeerModes.Multiple;
                host=new GameObject("Fossil check host").AddComponent<NetworkRunner>();
                peer=new GameObject("Fossil check peer").AddComponent<NetworkRunner>();
                string room="fossil-"+Guid.NewGuid().ToString("N");
                var app=PhotonAppSettings.Global.AppSettings.GetCopy();
                app.FixedRegion="us";app.AppVersion+="/fossil-validation";
                var start=host.StartGame(new StartGameArgs{GameMode=GameMode.Host,SessionName=room,PlayerCount=2,
                    IsVisible=false,CustomPhotonAppSettings=app,Scene=new NetworkSceneInfo(),
                    SceneManager=host.gameObject.AddComponent<NetworkSceneManagerDefault>()});
                float until=Time.realtimeSinceStartup+40;
                while(!start.IsCompleted && Time.realtimeSinceStartup<until)yield return null;
                bool online=start.IsCompletedSuccessfully&&start.Result.Ok;
                record(online,"Fusion host started");if(!online)yield break;
                var join=peer.StartGame(new StartGameArgs{GameMode=GameMode.Client,SessionName=room,EnableClientSessionCreation=false,
                    CustomPhotonAppSettings=app,Scene=new NetworkSceneInfo(),SceneManager=peer.gameObject.AddComponent<NetworkSceneManagerDefault>()});
                until=Time.realtimeSinceStartup+40;
                while(!join.IsCompleted && Time.realtimeSinceStartup<until)yield return null;
                online=join.IsCompletedSuccessfully&&join.Result.Ok;record(online,"second Fusion runner joined");if(!online)yield break;
                Physics.simulationMode=simulation;
                sender=new GameObject("Fossil world packet capture");
                var transport=sender.AddComponent<CastleLanSession>();
                transport.InitializeFusion(session,"fossil-check",false);
                var packet=transport.FusionCaptureWorld();
                var expected=CastleLanProtocol.DecodeWorld(packet);
                bool received=false;
                var callbacks=new NetworkDelegates();
                callbacks.OnReliableDataReceived=(runner,source,key,data)=>{
                    try
                    {
                        var frame=CastleLanProtocol.DecodeWorld(data.ToArray());
                        copy=Object.Instantiate(Resources.Load<GameObject>("ModeMaps/Mode_fossil-basin"),Vector3.right*500,Quaternion.identity);
                        var replica=copy.GetComponent<FossilBasinMap>();
                        replica.SetReplica(true);
                        var offset=Vector3.right*500;
                        bool loaded=replica.LoadLayout(frame.settings,frame.blocks.Length,new Pose(frame.origin.position+offset,frame.origin.rotation),frame.originScale);
                        record(loaded && replica.SaveLayout()==expected.settings,"late peer rebuilt transmitted joint state");
                        if(loaded)
                        {
                            for(int i=0;i<replica.Blocks.Count;i++)
                            {
                                var b=replica.Blocks[i];b.position=frame.blocks[i].position+offset;b.rotation=frame.blocks[i].rotation;
                                b.gameObject.SetActive(frame.active[i]);
                            }
                            Physics.SyncTransforms();
                            var bad=replica.Blocks.Select((b,i)=>(b,i)).Where(entry=>{var (b,i)=entry;return !(
                                b.isKinematic && b.gameObject.activeSelf==expected.active[i] &&
                                (!b.gameObject.activeSelf || (Vector3.Distance(b.position-offset,expected.blocks[i].position)<.025f &&
                                Quaternion.Angle(b.rotation,expected.blocks[i].rotation)<.5f)));}).ToArray();
                            string details=string.Join(", ",bad.Take(3).Select(x=>x.b.name+" p="+x.b.position+" expected="+(expected.blocks[x.i].position+offset)+" active="+x.b.gameObject.activeSelf));
                            record(replica.Blocks.Count==expected.blocks.Length && bad.Length==0,"peer reconstructed body activity, poses and ordering "+details);
                        }
                    }
                    catch(Exception e){record(false,e.Message);}
                    finally{Services.World=map;received=true;}
                };
                peer.AddCallbacks(callbacks);
                host.SendReliableDataToPlayer(peer.LocalPlayer,ReliableKey.FromInts(0x464F5353,1,0,0),packet);
                until=Time.realtimeSinceStartup+20;
                while(!received && Time.realtimeSinceStartup<until)yield return null;
                record(received,"peer received post-collapse production packet");
            }
            finally
            {
                if(peer){_=peer.Shutdown();Object.Destroy(peer.gameObject);}
                if(host){_=host.Shutdown();Object.Destroy(host.gameObject);}
                if(copy)Object.Destroy(copy);
                if(sender)Object.Destroy(sender);
                Services.World=map;Physics.simulationMode=simulation;config.PeerMode=oldMode;
            }
        }
    }
}
