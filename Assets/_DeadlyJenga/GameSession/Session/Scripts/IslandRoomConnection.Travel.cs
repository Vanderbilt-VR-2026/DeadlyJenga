using System;
using System.Collections;
using DeadlyJenga.Core;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandRoomConnection
    {
        const int DepartureChannel=0x44455054;
        const float DepartureSeconds=1.05f;
        int departureSequence;
        public bool Departing {get;private set;}
        void BeginDeparture(int scene,bool networked)
        {
            if(Departing||!IslandLobby.Current)return;
            ReceiveDeparture(DepartureSeconds);
            if(networked&&IsHost)foreach(var player in Runner.ActivePlayers)
                if(player!=Runner.LocalPlayer)Runner.SendReliableDataToPlayer(player,ReliableKey.FromInts(DepartureChannel,1,0,0),BitConverter.GetBytes(DepartureSeconds));
            StartCoroutine(LoadAfterDeparture(scene,networked,generation,departureSequence));
        }
        void ReceiveDeparture(float seconds)
        {
            if(Departing)return;
            departureSequence++;Departing=true;LaunchPending=true;
            IslandLobby.Current?.BeginDeparture(seconds);
            GameEvents.RaiseDepartureStarted(seconds);
        }
        IEnumerator LoadAfterDeparture(int scene,bool networked,int connectionGeneration,int sequence)
        {
            yield return new WaitForSecondsRealtime(DepartureSeconds);
            if(connectionGeneration!=generation||sequence!=departureSequence)yield break;
            if(!Departing||!IslandLobby.Current||(networked&&!IsHost)){CancelDeparture();yield break;}
            try
            {
                transitioning=true;
                if(networked)Runner.LoadScene(SceneRef.FromIndex(scene),LoadSceneMode.Single);
                else SceneManager.LoadScene(scene);
            }
            catch(Exception error){Status="Could not load map: "+error.Message;CancelDeparture();}
        }
        public void CompleteDeparture(){departureSequence++;Departing=false;LaunchPending=false;transitioning=false;}
        void CancelDeparture()
        {
            if(!Departing)return;
            departureSequence++;Departing=false;LaunchPending=false;transitioning=false;
            IslandLobby.Current?.CancelDeparture();
            GameEvents.RaiseEmoted(gameObject,"DepartureCancelled");
        }
    }
}
