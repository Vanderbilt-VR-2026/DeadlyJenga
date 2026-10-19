using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using UnityEngine;
using Fusion;
using P=DeadlyJenga.CastlePrototype.CastleLanProtocol;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandRoomConnection
    {
        const int LobbyCommand=6;
        [Serializable] public sealed class LobbyAction {public string verb,value;public int index;public LobbySetup setup;}
        [Serializable] public sealed class LobbyState
        {
            public string leader,notice;
            public LobbyParticipant[] roster;
            public string[] requests;
            public LobbySetup setup;
        }
        readonly Dictionary<string,LobbyParticipant> identities=new Dictionary<string,LobbyParticipant>();
        readonly List<string> hostRequests=new List<string>();
        readonly Dictionary<string,float> requestAfter=new Dictionary<string,float>();
        LobbyParticipant localIdentity;
        LobbySetup setup;
        string leaderId="",lobbyNotice="";
        public string LocalLobbyId=>Connected?Runner.LocalPlayer.ToString():"local";
        public bool LocalIsLobbyHost=>!Connected||leaderId==LocalLobbyId;
        public string LeaderId=>leaderId;
        public IReadOnlyList<LobbyParticipant> Participants=>identities.Values.ToArray();
        public IReadOnlyList<string> HostRequests=>hostRequests;
        public string LobbyNotice=>LocalIsLobbyHost&&hostRequests.Count>0?hostRequests.Count+" host request"+(hostRequests.Count==1?"":"s")+" · review at HOST terminal":lobbyNotice;
        public LobbyParticipant LocalIdentity
        {
            get
            {
                if(localIdentity==null){var color=ModeNames.Colors[UnityEngine.Random.Range(0,ModeNames.Colors.Length)];localIdentity=new LobbyParticipant{id="local",noun=ModeNames.Nouns[UnityEngine.Random.Range(0,ModeNames.Nouns.Length)],color=color};localIdentity.displayName=ModeNames.NameFor(color,localIdentity.noun);}
                return localIdentity;
            }
        }
        public LobbySetup Setup
        {
            get
            {
                if(setup==null)ResetModeSetup();
                if(LocalIsLobbyHost){setup.population=MatchSetup.BotTarget;setup.hands=MatchSetup.HandCount;setup.sameHumanTeam=MatchChoices.SameHumanTeam;setup.survivorPreset=MatchSetup.SurvivorPreset;setup.handPreset=MatchSetup.HandPreset;setup.movement=MatchChoices.Rules;}
                return JsonUtility.FromJson<LobbySetup>(JsonUtility.ToJson(setup));
            }
        }
        void ResetModeSetup()
        {
            var kind=MatchModeRuntime.ParseMode(Catalog.modes[ModeIndex].id);
            setup=new LobbySetup{rules=ModeSimulation.Defaults(kind),movement=MatchChoices.Rules,population=MatchSetup.BotTarget,hands=MatchSetup.HandCount,sameHumanTeam=MatchChoices.SameHumanTeam,survivorPreset=MatchSetup.SurvivorPreset,handPreset=MatchSetup.HandPreset,disasters=DemoDisasters.Prefabs.Select(p=>new LobbyHazardSetup{id=p.name,values=DemoDisasters.Parameters(p).Select(v=>v.initial).ToArray()}).ToArray()};
        }
        public void ConfigureSetup(LobbySetup value)
        {if(CanConfigure&&ValidSetup(value))Act(new LobbyAction{verb="setup",setup=value});}
        static bool ValidSetup(LobbySetup value)
        {
            if(value==null||!ModeFrameValidation.Rules(value.rules)||value.movement==null||value.population<1||value.population>100||value.hands<0||value.hands>100||value.survivorPreset<0||value.survivorPreset>100||value.handPreset<0||value.handPreset>100)return false;
            if(value.disasters==null||value.disasters.Length>32)return false;
            foreach(var hazard in value.disasters){var prefab=DemoDisasters.Prefabs.FirstOrDefault(p=>p.name==hazard.id);if(!prefab||hazard.values==null)return false;var parameters=DemoDisasters.Parameters(prefab);if(parameters.Count!=hazard.values.Length)return false;for(int i=0;i<parameters.Count;i++){float v=hazard.values[i];if(float.IsNaN(v)||float.IsInfinity(v)||v<parameters[i].min||v>parameters[i].max)return false;}}
            foreach(var f in typeof(MatchRules).GetFields())if(f.FieldType==typeof(float)){float v=(float)f.GetValue(value.movement);if(float.IsNaN(v)||float.IsInfinity(v)||v<0||v>10000)return false;}
            return value.movement.drainCurve?.Length<=32&&value.movement.recoveryCurve?.Length<=32;
        }
        void ApplySetup(LobbySetup value)
        {
            setup=JsonUtility.FromJson<LobbySetup>(JsonUtility.ToJson(value));setup.rules.mode=MatchModeRuntime.ParseMode(Catalog.modes[ModeIndex].id);
            MatchSetup.BotTarget=setup.population;MatchSetup.HandCount=setup.hands;MatchSetup.SurvivorPreset=setup.survivorPreset;MatchSetup.HandPreset=setup.handPreset;MatchChoices.SameHumanTeam=setup.sameHumanTeam;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(setup.movement),MatchChoices.Rules);
        }
        public void RequestHost(){if(Connected&&!LocalIsLobbyHost&&!Busy)Act(new LobbyAction{verb="request"});}
        public void ReviewHostRequest(string id,bool accept){if(LocalIsLobbyHost&&!Busy)Act(new LobbyAction{verb=accept?"accept":"deny",value=id});}
        void Act(LobbyAction action)
        {
            if(Busy||transitioning)return;
            if(!Connected||IsHost)ApplyLobbyAction(LocalLobbyId,action);
            else Send(default,LobbyCommand,P.Encode(P.Player,w=>w.Write(JsonUtility.ToJson(action))));
        }
        void ApplyLobbyAction(string source,LobbyAction action)
        {
            if(action==null)return;
            bool member=!Connected||source==Runner.LocalPlayer.ToString()||peers.Any(p=>p.Key.ToString()==source&&p.Value.ready);
            if(!member)return;
            if(action.verb=="request")
            {
                if(source==leaderId||hostRequests.Contains(source)||hostRequests.Count>=10)return;
                if(requestAfter.TryGetValue(source,out float after)&&Time.unscaledTime<after)return;
                requestAfter[source]=Time.unscaledTime+20;hostRequests.Add(source);return;
            }
            if(Connected&&source!=leaderId)return;
            if(action.verb=="accept"||action.verb=="deny")
            {
                if(!hostRequests.Remove(action.value))return;
                if(action.verb=="accept"&&identities.ContainsKey(action.value)){leaderId=action.value;hostRequests.Clear();lobbyNotice=identities[action.value].displayName+" is now host.";LaunchPending=false;RefreshHostFlags();}
                else lobbyNotice="Host request declined.";
            }
            else if(!LaunchPending&&action.verb=="map"&&action.index>=0&&action.index<Catalog.maps.Length)MapIndex=action.index;
            else if(!LaunchPending&&action.verb=="mode"&&action.index>=0&&action.index<Catalog.modes.Length){ModeIndex=action.index;ResetModeSetup();}
            else if(!LaunchPending&&action.verb=="setup"&&ValidSetup(action.setup))ApplySetup(action.setup);
            else if(!LaunchPending&&action.verb=="shared"&&Catalog.modes[ModeIndex].id=="demo")SharedDemoControls=action.index!=0;
            else if(action.verb=="launch"){LaunchAuthoritative();return;}
            else if(action.verb=="cancel"){LaunchPending=false;lobbyNotice="Start cancelled.";}
            PublishSelection();
        }
        void RefreshHostFlags(){foreach(var item in identities.Values)item.host=item.id==leaderId;}
        static bool ValidNoun(string value)
        {
            if(string.IsNullOrEmpty(value)||value.Length>32)return false;
            foreach(var noun in ModeNames.Nouns)if(value.StartsWith(noun,StringComparison.Ordinal)&&value.Substring(noun.Length).All(char.IsDigit))return true;
            return false;
        }
        void AssignIdentity(string id,LobbyParticipant preferred)
        {
            if(identities.ContainsKey(id))return;
            string noun=preferred?.noun;
            if(!ValidNoun(noun)||identities.Values.Any(p=>p.noun==noun))
            {
                int first=UnityEngine.Random.Range(0,ModeNames.Nouns.Length);noun=null;
                for(int i=0;i<ModeNames.Nouns.Length;i++){var candidate=ModeNames.Nouns[(i+first)%ModeNames.Nouns.Length];if(!identities.Values.Any(p=>p.noun==candidate)){noun=candidate;break;}}
                if(noun==null){int suffix=2;string stem=ModeNames.Nouns[first];while(identities.Values.Any(p=>p.noun==stem+suffix))suffix++;noun=stem+suffix;}
            }
            Color color=preferred?.color??ModeNames.Colors[UnityEngine.Random.Range(0,ModeNames.Colors.Length)];
            if(float.IsNaN(color.r)||float.IsNaN(color.g)||float.IsNaN(color.b)||float.IsInfinity(color.r)||float.IsInfinity(color.g)||float.IsInfinity(color.b))color=ModeNames.Colors[0];
            color=new Color(Mathf.Clamp01(color.r),Mathf.Clamp01(color.g),Mathf.Clamp01(color.b),1);
            identities[id]=new LobbyParticipant{id=id,noun=noun,color=color,displayName=ModeNames.NameFor(color,noun),host=id==leaderId};
            if(id==LocalLobbyId)localIdentity=identities[id];
        }
        public LobbyParticipant IdentityFor(string id)=>identities.TryGetValue(id,out var value)?value:id==LocalLobbyId?LocalIdentity:null;
        public void PrepareSoloIdentity()
        {
            if(Connected)return;
            var own=LocalIdentity;identities.Clear();own.id="local";own.host=true;identities[own.id]=own;leaderId=own.id;hostRequests.Clear();
        }
        void ConnectedIdentity(bool hosting)
        {
            if(hosting){var own=LocalIdentity;identities.Clear();leaderId=LocalLobbyId;AssignIdentity(LocalLobbyId,own);}
            else{identities.Clear();leaderId="";hostRequests.Clear();}
        }
        void DepartIdentity(string id)
        {
            identities.Remove(id);hostRequests.Remove(id);requestAfter.Remove(id);
            if(IsHost&&leaderId==id){var candidates=Runner.ActivePlayers.Where(p=>p.ToString()!=id).ToArray();leaderId=candidates.Length>0?candidates[UnityEngine.Random.Range(0,candidates.Length)].ToString():LocalLobbyId;LaunchPending=false;RefreshHostFlags();}
        }
        float nextRosterCheck;
        void ReconcileLobbyRoster()
        {
            if(Time.unscaledTime<nextRosterCheck)return;nextRosterCheck=Time.unscaledTime+.5f;
            var active=new HashSet<string>(Runner.ActivePlayers.Select(p=>p.ToString()));
            foreach(var id in identities.Keys.ToArray())if(!active.Contains(id))DepartIdentity(id);
            if(!active.Contains(leaderId))
            {
                var candidates=active.ToArray();
                leaderId=candidates.Length>0?candidates[UnityEngine.Random.Range(0,candidates.Length)]:LocalLobbyId;
                LaunchPending=false;RefreshHostFlags();
            }
        }
        string LobbyStateJson()=>JsonUtility.ToJson(new LobbyState{leader=leaderId,roster=identities.Values.ToArray(),requests=hostRequests.ToArray(),notice=lobbyNotice,setup=Setup});
        void ReadLobbyState(string json)
        {
            var state=JsonUtility.FromJson<LobbyState>(json);
            if(state?.roster==null||state.roster.Length>10||state.requests==null||state.requests.Length>10||!ValidSetup(state.setup)||state.leader?.Length>128||state.notice?.Length>200)throw new System.IO.InvalidDataException("Invalid lobby metadata");
            var used=new HashSet<string>();var nouns=new HashSet<string>();
            foreach(var p in state.roster)if(p==null||string.IsNullOrEmpty(p.id)||p.id.Length>128||!used.Add(p.id)||!ValidNoun(p.noun)||!nouns.Add(p.noun)||p.displayName?.Length>64)throw new System.IO.InvalidDataException("Invalid lobby identity");
            identities.Clear();foreach(var p in state.roster)identities.Add(p.id,p);
            leaderId=state.leader;lobbyNotice=state.notice??"";hostRequests.Clear();hostRequests.AddRange(state.requests);ApplySetup(state.setup);RefreshHostFlags();
            if(identities.TryGetValue(LocalLobbyId,out var own))localIdentity=own;
        }
        public bool HasDemoPermission(string id)=>SharedDemoControls||id==leaderId;
    }
}
