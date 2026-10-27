using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobby : ILobbyDetails
    {
        public IReadOnlyList<LobbyParticipant> Participants=>Room.Participants;
        public IReadOnlyList<string> HostRequests=>Room.HostRequests;
        public IReadOnlyList<LobbyOption> Maps=>catalog.maps.Where(m=>m.SupportsMode(SelectedModeId)).Select(m=>new LobbyOption{id=m.id,title=m.title}).ToArray();
        public IReadOnlyList<LobbyOption> Modes=>catalog.modes.Select(m=>new LobbyOption{id=m.id,title=m.title,description=m.description,available=m.available}).ToArray();
        public string LocalParticipantId=>Room.LocalLobbyId;
        public string SelectedMapId=>catalog.maps[Room.MapIndex].id;
        public string SelectedModeId=>catalog.modes[Room.ModeIndex].id;
        public string Notification=>Room.LobbyNotice;
        public int ConnectedCount=>Room.Connected?Room.HumanCount:0;
        public Transform ParticipantRoot(string id)
        {if(id==Room.LocalLobbyId)return Player?.transform;return peers.TryGetValue(id,out var peer)&&peer is Component c&&c?c.transform:null;}
        public void RequestHost(){if(!VisitingLobby)Room.RequestHost();}
        public void ReviewHostRequest(string id,bool accept){if(!VisitingLobby)Room.ReviewHostRequest(id,accept);}
        public LobbySetup Setup=>Room.Setup;
        public void ConfigureSetup(LobbySetup value){if(CanConfigure)Room.ConfigureSetup(value);}
        void ApplyLobbyIdentities()
        {
            foreach(var p in Participants)
            {
                var root=ParticipantRoot(p.id);if(!root)continue;
                var actor=root.GetComponent<IModeActor>();
                if(actor!=null&&(actor.DisplayName!=p.displayName||actor.Color!=p.color))actor.SetIdentity(p.id,p.displayName,-1,p.color);
            }
        }
    }
}
