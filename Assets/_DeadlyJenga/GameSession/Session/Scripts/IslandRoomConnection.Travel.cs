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
        int remoteDepartureConnection=int.MinValue,remoteDepartureGeneration,remoteDepartureSequence;
        bool remoteDepartureKnown,remoteDepartureActive;
        public bool Departing {get;private set;}
        void BeginDeparture(int scene,bool networked)
        {
            if(!IslandLobby.Current)return;
            BeginSceneDeparture(scene,networked,"map",()=>
            {
                var lobby=IslandLobby.Persistent;
                if(lobby&&lobby.MatchResident&&!FindFirstObjectByType<CastlePlaySession>())lobby.ResumeAfterMatch();
            });
        }
        bool BeginSceneDeparture(int scene,bool networked,string destination,Action failed=null,Func<bool> stillCurrent=null)
        {
            if(Departing||networked&&!IsHost)return false;
            try
            {
                ReceiveDeparture(DepartureSeconds);
                if(networked)foreach(var player in Runner.ActivePlayers)
                    if(player!=Runner.LocalPlayer)Runner.SendReliableDataToPlayer(player,ReliableKey.FromInts(DepartureChannel,1,departureSequence,generation),BitConverter.GetBytes(DepartureSeconds));
                StartCoroutine(LoadAfterDeparture(scene,networked,destination,generation,departureSequence,failed,stillCurrent));
                return true;
            }
            catch(Exception error)
            {
                Status="Could not leave for "+destination+": "+error.Message;
                CancelDeparture();failed?.Invoke();return false;
            }
        }
        void ReceiveDeparture(float seconds)
        {
            if(Departing)return;
            departureSequence++;Departing=true;LaunchPending=true;
            IslandLobby.Current?.PrepareMarinaArrivalCue();
            IslandLobby.Current?.BeginDeparture(seconds);
            GameEvents.RaiseDepartureStarted(seconds);
        }
        void ReceiveDeparturePacket(int kind,int sequence,int hostGeneration,ReadOnlySpan<byte> data)
        {
            if(remoteDepartureConnection!=generation)
            {
                remoteDepartureConnection=generation;remoteDepartureKnown=remoteDepartureActive=false;
            }
            if(kind==2)
            {
                if(data.Length!=0||sequence<=0||!remoteDepartureActive||sequence!=remoteDepartureSequence||hostGeneration!=remoteDepartureGeneration)return;
                CancelDeparture();return;
            }
            if(kind!=1||data.Length!=4||sequence<0||hostGeneration<0||Departing)return;
            float seconds=BitConverter.ToSingle(data.ToArray(),0);
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<.3f||seconds>3)return;
            if(sequence==0)
            {
                if(hostGeneration!=0||remoteDepartureKnown&&remoteDepartureSequence!=0)return;
            }
            else if(remoteDepartureKnown&&(hostGeneration<remoteDepartureGeneration||
                hostGeneration==remoteDepartureGeneration&&sequence<=remoteDepartureSequence))return;
            ReceiveDeparture(seconds);
            remoteDepartureKnown=remoteDepartureActive=true;
            remoteDepartureSequence=sequence;remoteDepartureGeneration=hostGeneration;
        }
        IEnumerator LoadAfterDeparture(int scene,bool networked,string destination,int connectionGeneration,int sequence,Action failed,Func<bool> stillCurrent)
        {
            yield return new WaitForSecondsRealtime(DepartureSeconds);
            if(sequence!=departureSequence)yield break;
            if(connectionGeneration!=generation||!Departing||networked&&!IsHost||stillCurrent!=null&&!stillCurrent())
            {CancelDeparture();yield break;}
            bool failureReported=false;
            void Fail()
            {
                if(failureReported)return;failureReported=true;
                CancelDeparture();
                var lobby=IslandLobby.Persistent;
                if(lobby&&lobby.MatchResident&&!FindFirstObjectByType<CastlePlaySession>())lobby.ResumeAfterMatch();
                failed?.Invoke();
            }
            try
            {
                if(networked)
                {
                    if(!LoadRoomScene(scene,destination,Fail))Fail();
                }
                else
                {
                    transitioning=true;
                    if(destination=="map")IslandLobby.Persistent?.ParkForMatch();
                    SceneManager.LoadScene(scene);
                }
            }
            catch(Exception error){Status="Could not load "+destination+": "+error.Message;Fail();}
        }
        public void CompleteDeparture(){departureSequence++;Departing=false;LaunchPending=false;transitioning=false;remoteDepartureActive=false;}
        void CancelDeparture()
        {
            if(!Departing)return;
            int sequence=departureSequence;
            departureSequence++;Departing=false;LaunchPending=false;transitioning=false;remoteDepartureActive=false;
            if(Connected&&IsHost)foreach(var player in Runner.ActivePlayers)
            {
                if(player==Runner.LocalPlayer)continue;
                try{Runner.SendReliableDataToPlayer(player,ReliableKey.FromInts(DepartureChannel,2,sequence,generation),Array.Empty<byte>());}
                catch(Exception error){Status="Could not cancel departure for a player: "+error.Message;}
            }
            IslandLobby.Current?.CancelMarinaArrivalCue();
            IslandLobby.Current?.CancelDeparture();
            GameEvents.RaiseEmoted(gameObject,"DepartureCancelled");
        }
    }
}
