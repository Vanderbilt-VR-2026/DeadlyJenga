using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;

namespace DeadlyJenga.DevTools
{
    // Explicit UI test stand-in; never installed by gameplay.
    public sealed class NearbyUiFixture : MonoBehaviour, INearbyRoomControls, IHomeMenuControls
    {
        public readonly List<NearbyRoomInfo> Rooms=new();
        public readonly List<NearbyJoinRequest> Requests=new();
        public string LastRequest,LastDecision;
        public bool Accepted;
        public int Scans,Cancels;
        public string PlayerName {get=>NearbyPlayerProfile.Name;set=>NearbyPlayerProfile.Name=value;}
        public bool NearbyEnabled {get;set;}
        public bool CanAdvertise=>true;
        public string NearbyStatus=>"Simulated nearby rooms · UI test";
        public string JoinRequestStatus {get;private set;}="";
        public IReadOnlyList<NearbyRoomInfo> NearbyRooms=>Rooms;
        public IReadOnlyList<NearbyJoinRequest> JoinRequests=>Requests;
        public void RefreshNearby(){Scans++;NearbyEnabled=true;}
        public void RequestJoin(string id){LastRequest=id;JoinRequestStatus="Waiting for host approval";}
        public void CancelJoinRequest(){Cancels++;JoinRequestStatus="Request cancelled";}
        public void RespondToJoin(string id,bool accept){LastDecision=id;Accepted=accept;Requests.RemoveAll(r=>r.Id==id);}
        public bool Busy=>false;
        public string Status=>"UI fixture: no Bluetooth or Photon connection";
        public string Region=>"US";
        public string Code {get;set;}
        public bool CodeValid=>false;
        public int NearbyCount=>0;
        public string NearbyTitle(int index)=>"";
        public void DetectNearby(){}
        public void NextRegion(){}
        public void JoinCode(){}
        public void JoinNearby(int index){}
        public void Host(){}
        public void RandomLobby(){}
        public void Demo(){}
    }
}
