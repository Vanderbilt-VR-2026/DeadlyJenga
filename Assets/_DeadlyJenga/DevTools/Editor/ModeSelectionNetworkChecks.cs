using System;
using System.Collections;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;
using P=DeadlyJenga.CastlePrototype.CastleLanProtocol;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static class ModeSelectionNetworkChecks
    {
        public static IEnumerator Run(Action<bool,string> check)
        {
            var lobby=IslandLobby.Current;var primary=lobby.Room;var catalog=primary.Catalog;
            var config=NetworkProjectConfig.Global;var oldMode=config.PeerMode;var oldPhysics=Physics.simulationMode;
            int oldMap=primary.MapIndex,oldSelection=primary.ModeIndex;var oldSetup=lobby.Setup;NetworkRunner guest=null;
            try
            {
                config.PeerMode=NetworkProjectConfig.PeerModes.Multiple;
                var host=primary.Host();yield return Until(()=>host.IsCompleted,"Production lobby Photon host finishes connecting",45,check);
                check(primary.IsHost,"Production lobby owns a real host room");
                guest=new GameObject("Mode selector Photon guest").AddComponent<NetworkRunner>();var probe=guest;
                int snapshots=0,lastMap=-1,lastMode=-1;string failure=null;
                var callbacks=new NetworkDelegates();callbacks.OnReliableDataReceived=(runner,source,key,data)=>
                {
                    key.GetInts(out int channel,out int kind,out int id,out _);if(channel!=0x49534C44||kind!=3)return;
                    try
                    {
                        using var reader=P.Reader(data.ToArray(),P.World);lastMap=reader.ReadInt32();lastMode=reader.ReadInt32();
                        if(lastMap<0||lastMap>=catalog.maps.Length||lastMode<0||lastMode>=catalog.modes.Length||!catalog.maps[lastMap].SupportsMode(catalog.modes[lastMode].id))throw new Exception("Invalid transported map/mode pair");
                        int count=P.Count(reader,10);for(int i=0;i<count;i++)P.ReadActor(reader);reader.ReadBoolean();
                        JsonUtility.FromJson<IslandRoomConnection.LobbyState>(P.Text(reader,16000));if(reader.BaseStream.Position!=reader.BaseStream.Length)throw new Exception("Trailing lobby selection data");snapshots++;
                        probe.SendReliableDataToServer(ReliableKey.FromInts(channel,4,id,0),Array.Empty<byte>());
                    }
                    catch(Exception e){failure=e.ToString();}
                };
                guest.AddCallbacks(callbacks);var app=PhotonAppSettings.Global.AppSettings.GetCopy();app.FixedRegion=primary.Region;app.AppVersion=(app.AppVersion??"")+"/"+catalog.NetworkVersion;
                var join=guest.StartGame(new StartGameArgs{GameMode=GameMode.Client,SessionName=primary.Code,EnableClientSessionCreation=false,CustomPhotonAppSettings=app,Scene=new NetworkSceneInfo(),SceneManager=guest.gameObject.AddComponent<NetworkSceneManagerDefault>()});
                yield return Until(()=>join.IsCompleted,"Independent selector client finishes connecting",45,check);check(join.Result.Ok,"Second real Photon peer joins the production lobby");Physics.simulationMode=oldPhysics;
                guest.SendReliableDataToServer(ReliableKey.FromInts(0x49534C44,1,0,0),Array.Empty<byte>());
                yield return Until(()=>snapshots>0||failure!=null,"Guest receives the production lobby selection packet",15,check);check(failure==null,"Production map/mode packet decodes and validates on the second peer");
                int capture=Array.FindIndex(catalog.modes,m=>m.id=="ctf"),hill=Array.FindIndex(catalog.modes,m=>m.id=="hill");
                check(lobby.SelectMode("ctf"),"Host selects Capture the Flag through the real lobby service");
                yield return Until(()=>lastMode==capture,"Host mode-first selection replicates to the second peer",15,check);
                var other=catalog.maps.FirstOrDefault(m=>m.recommendedMode=="survival")??catalog.maps[0];check(lobby.SelectMap(other.id),"Host selects a map whose recommendation differs from the chosen mode");int otherIndex=Array.IndexOf(catalog.maps,other);
                yield return Until(()=>lastMap==otherIndex,"Host map selection replicates after mode selection",15,check);check(lastMode==capture&&primary.ModeIndex==capture,"Map selection never overwrites the selected mode on either peer");
                guest.SendReliableDataToServer(ReliableKey.FromInts(0x49534C44,6,1,0),P.Encode(P.Player,w=>w.Write(JsonUtility.ToJson(new IslandRoomConnection.LobbyAction{verb="mode",index=hill}))));
                yield return new WaitForSeconds(.35f);check(primary.ModeIndex==capture,"Authenticated non-host peer cannot forge a mode selection");
                check(lobby.SelectMode("hill"),"Host can change mode after selecting a map");yield return Until(()=>lastMode==hill,"Updated host mode reaches the guest",15,check);check(failure==null,"All selection packets use the existing production lobby codec");
                var leave=guest.Shutdown();yield return Until(()=>leave.IsCompleted,"Selector guest shuts down",15,check);if(guest)Object.Destroy(guest.gameObject);guest=null;
                leave=primary.Leave();yield return Until(()=>leave.IsCompleted,"Production host leaves cleanly",15,check);
            }
            finally
            {
                if(guest){_=guest.Shutdown();Object.Destroy(guest.gameObject);}if(primary.Connected)_=primary.Leave();
                config.PeerMode=oldMode;Physics.simulationMode=oldPhysics;primary.MapIndex=oldMap;primary.ModeIndex=oldSelection;lobby.ConfigureSetup(oldSetup);
            }
            check(config.PeerMode==oldMode&&Physics.simulationMode==oldPhysics,"Temporary two-peer and physics settings restore after selector validation");
        }
        static IEnumerator Until(Func<bool> ready,string text,float seconds,Action<bool,string> check)
        {float until=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;check(ready(),text);}
    }
}
