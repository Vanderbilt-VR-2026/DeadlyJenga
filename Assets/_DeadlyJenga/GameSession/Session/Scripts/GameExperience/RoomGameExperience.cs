using System;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    [Serializable]
    public sealed class GameExperienceCommand
    {
        public string verb="",value="";
        public bool ready;
        public int selectionVersion,rosterVersion,authorityTerm,round;
    }

    // Lives on the persistent room, never on its replaceable Fusion runner.
    [DefaultExecutionOrder(300)]
    public sealed partial class RoomGameExperience : MonoBehaviour, IGameExperienceControls
    {
        IslandRoomConnection room;
        RoomGameFlow flow;
        RoomGameFlow cachedFlow;
        GameFlowState cachedState;
        int cachedRevision=-1;
        double lastTick;
        string dispatched="";
        string sceneSelectionError="";
        bool attached;
        public static RoomGameExperience Ensure(IslandRoomConnection owner)
        {
            if(!owner)return null;
            var value=owner.GetComponent<RoomGameExperience>()??owner.gameObject.AddComponent<RoomGameExperience>();
            if(!value.attached)value.Initialize(owner);
            return value;
        }
        public static RoomGameExperience Current=>IslandRoomConnection.Instance?IslandRoomConnection.Instance.GetComponent<RoomGameExperience>():null;
        public bool IsLaunchedActor(string actorId)
        {
            var state=State;
            return state!=null&&state.launchIds.Any(id=>room.GameExperienceActorId(id)==actorId);
        }
        void Initialize(IslandRoomConnection owner)
        {
            room=owner;attached=true;
            flow=new RoomGameFlow(new GameFlowState{roomId=Guid.NewGuid().ToString("N"),authorityTerm=1,demo=true});
            lastTick=Time.realtimeSinceStartupAsDouble;
            TransportBind(owner);
            BindMatchEvents();
        }
        void OnDestroy(){UnbindMatchEvents();TransportDisconnected();}
        public static bool Manages(CastlePlaySession session)
        {
            var connection=IslandRoomConnection.Instance;
            var provider=connection?connection.GetComponent<RoomGameExperience>():null;
            return session&&provider&&provider.Experience==GameExperience.Game;
        }
        internal IslandRoomConnection Room=>room;
        internal bool IsAuthority=>room&&(!room.Connected||room.IsHost);
        bool WaitingForRoster=>room&&room.Connected&&room.Runner&&room.Runner.ActivePlayers.Any()&&room.GameExperienceRosterIds.Length==0;
        internal bool DevelopmentAllowed=>Experience==GameExperience.Demo&&!TransportRecovering;
        GameFlowState State
        {
            get
            {
                if(flow==null)return null;
                if(!ReferenceEquals(cachedFlow,flow)||cachedState==null||cachedRevision!=flow.Revision)
                {cachedFlow=flow;cachedRevision=flow.Revision;cachedState=flow.State;}
                return cachedState;
            }
        }
        internal CuratedGameProfile CurrentProfile=>FindProfile(State?.profileId);
        public GameExperience Experience=>State==null||State.demo?GameExperience.Demo:GameExperience.Game;
        public bool CanConfigureExperience=>room&&room.LocalIsLobbyHost&&!room.Busy&&!TransportRecovering&&!WaitingForRoster&&
            State!=null&&(State.phase==GameFlowPhase.Playing||State.phase==GameFlowPhase.Results||LobbyVisible)&&(State.pending==GameFlowEffect.None||State.pending==GameFlowEffect.ChooseProfile);
        public bool AutomaticRounds=>State?.automatic??false;
        public RoomGamePhase GamePhase=>(RoomGamePhase)(int)(State?.phase??GameFlowPhase.Idle);
        public string ProfileId=>State?.profileId??"";
        public float CountdownSeconds=>(float)(State?.remaining??0);
        public int MinimumPopulation=>CurrentProfile?.minimumPopulation??0;
        public int MaximumPopulation=>CurrentProfile?.maximumPopulation??0;
        public bool LocalReady=>State!=null&&Array.IndexOf(State.automatic&&(State.phase==GameFlowPhase.Playing||State.phase==GameFlowPhase.Results)?State.playlistIds:State.readyIds,room.GameExperienceLocalId)>=0;
        public int ReadyCount=>State?.readyIds.Length??0;
        public int RequiredReadyCount=>State?.humanIds.Length??0;
        public bool CanCancelGameStart=>CanConfigureExperience&&Experience==GameExperience.Game&&State.phase!=GameFlowPhase.Playing&&State.phase!=GameFlowPhase.Results;
        public bool CanRetryGameTransition=>room&&room.LocalIsLobbyHost&&!room.Busy&&!TransportRecovering&&State?.phase==GameFlowPhase.Failed&&State.pending!=GameFlowEffect.None&&State.attempts<3;
        public string GameStatus
        {
            get
            {
                var state=State;if(state==null)return "Preparing room";
                if(!string.IsNullOrEmpty(TransportStatus))return TransportStatus;
                if(TransportRecovering)return "Restoring room authority; waiting for verified checkpoints.";
                if(WaitingForRoster)return "Waiting for connected player identities before changing the room.";
                if(state.demo)return "Demo · development controls available to permitted players.";
                if(state.phase==GameFlowPhase.Failed)return sceneSelectionError.Length>0?sceneSelectionError:state.error;
                if((state.phase==GameFlowPhase.LoadingGame||state.phase==GameFlowPhase.Playing||state.phase==GameFlowPhase.Results)&&!string.IsNullOrEmpty(MatchStatus))return MatchStatus;
                return "PROTOTYPE · unvalidated · no disasters · "+state.phase+" · "+ReadyCount+"/"+RequiredReadyCount+" ready";
            }
        }
        public string GameActionHint=>Experience==GameExperience.Demo?"Host: switch to Game for the prototype playlist.":
            State?.phase==GameFlowPhase.Results?"Next Round keeps admitted players. New guests ready in the lobby; Return keeps the room.":
            AutomaticRounds?"Ready joins continuous rounds until you unready or leave. New guests must ready.":"Ready for this selection, then the host starts. Results: Next Round rematches; Return keeps the room.";
        void Submit(string verb,string value="",bool ready=false)
        {
            var s=State;if(s==null)return;
            TransportSubmit(new GameExperienceCommand{verb=verb,value=value,ready=ready,authorityTerm=s.authorityTerm,selectionVersion=s.selectionVersion,rosterVersion=s.rosterVersion,round=s.round});
        }
        public void SetExperience(GameExperience value){if(Enum.IsDefined(typeof(GameExperience),value))Submit("experience",value==GameExperience.Demo?"demo":"game");}
        public void SetAutomaticRounds(bool value)=>Submit("automatic",ready:value);
        public void SetReady(bool value)=>Submit("ready",ready:value);
        public void CancelGameStart()=>Submit("cancel");
        public void RetryGameTransition()=>Submit("retry");
        public void RequestStart()=>Submit("start");
        public void RequestRematch()=>Submit("rematch");
        public void RequestReturn()=>Submit("return");

        internal GameFlowState CaptureSnapshot()=>flow.State;
        internal bool ValidateAndApplySnapshot(GameFlowState snapshot)
        {
            try
            {
                RoomGameFlow.Validate(snapshot);
                // An over-capacity late roster must still receive state and be able to return/recover.
                if(snapshot.profileId.Length>0&&!ProfileAvailable(snapshot.profileId,1))return false;
                if(!snapshot.demo&&snapshot.profileId.Length>0)
                {
                    var profile=FindProfile(snapshot.profileId);
                    if(!room.ExperienceApplyVerifiedProfile(profile,ProfileRules(profile)))return false;
                }
                flow=new RoomGameFlow(snapshot);return true;
            }
            catch(ArgumentException){return false;}
        }
        internal void OnTransportRecoveryComplete()
        {
            if(IsAuthority){flow.AdoptAuthority(checked(State.authorityTerm+1));UpdateRoster();CommitFlow();}
            dispatched="";lastTick=Time.realtimeSinceStartupAsDouble;
        }
        internal void CommitFlow()=>TransportPublish();
        internal void ResetRoomFlow()
        {
            bool game=flow!=null&&!State.demo;
            bool loadedMatch=UnityEngine.Object.FindFirstObjectByType<CastlePlaySession>();
            flow=new RoomGameFlow(new GameFlowState{roomId=Guid.NewGuid().ToString("N"),authorityTerm=1,demo=!game});
            if(game&&loadedMatch)flow.ReturnToLobby(1);
            dispatched="";sceneSelectionError="";lastTick=Time.realtimeSinceStartupAsDouble;
        }
        void UpdateRoster()
        {
            if(IsAuthority&&flow.SetRoster(State.authorityTerm,room.GameExperienceRosterIds))CommitFlow();
        }
        internal void HandleAuthoritativeCommand(string sender,GameExperienceCommand command)
        {
            var s=State;
            if(!IsAuthority||TransportRecovering||room.Busy||WaitingForRoster||command==null||s==null||command.authorityTerm!=s.authorityTerm||
                command.selectionVersion!=s.selectionVersion||command.rosterVersion!=s.rosterVersion||command.round!=s.round||Array.IndexOf(s.humanIds,sender)<0)return;
            bool leader=room.GameExperienceIsLeader(sender),changed=false;
            if(command.verb=="ready"&&!s.demo)
            {
                if(s.automatic&&!command.ready&&(s.phase==GameFlowPhase.Playing||s.phase==GameFlowPhase.Results))
                {if(flow.WithdrawAutomaticConsent(s.authorityTerm,sender))CommitFlow();return;}
                if(s.phase!=GameFlowPhase.Selected&&s.phase!=GameFlowPhase.WaitingReady)return;
                if(s.automatic)changed=flow.EnrollPlaylist(s.authorityTerm,sender,command.ready);
                changed=flow.SetReady(s.authorityTerm,sender,s.selectionVersion,s.rosterVersion,command.ready)||changed;
            }
            else if(leader&&command.verb=="experience"&&(LobbyVisible||command.value=="demo"&&ActiveMatchVisible))
            {
                changed=command.value=="demo"&&!LobbyVisible?flow.EnableDemo(s.authorityTerm):flow.Configure(s.authorityTerm,command.value=="demo",false);
                if(changed&&!State.demo)SelectNext();
            }
            else if(leader&&command.verb=="automatic"&&(LobbyVisible||s.phase==GameFlowPhase.Playing||s.phase==GameFlowPhase.Results))
            {
                changed=flow.SetAutomatic(s.authorityTerm,command.ready);
                if(changed&&command.ready&&State.phase!=GameFlowPhase.Playing&&State.phase!=GameFlowPhase.Results)flow.Configure(s.authorityTerm,false,true);
            }
            else if(leader&&command.verb=="start")changed=flow.RequestStart(s.authorityTerm,s.selectionVersion,s.rosterVersion);
            else if(leader&&command.verb=="cancel")changed=flow.Cancel(s.authorityTerm);
            else if(leader&&command.verb=="retry")changed=flow.Retry(s.authorityTerm,s.operationId,s.attempts);
            else if(leader&&command.verb=="return"&&!room.RoomSceneBusy)changed=flow.ReturnToLobby(s.authorityTerm);
            else if(leader&&command.verb=="rematch"&&ProfileAvailable(s.profileId,s.humanIds.Length))changed=flow.RequestRematch(s.authorityTerm);
            if(changed)CommitFlow();
        }
        void Update()
        {
            if(!attached||!room)return;
            TransportTick();
            TickDeferredParticipants();
            if(!IsAuthority||TransportRecovering||room.Busy||WaitingForRoster){lastTick=Time.realtimeSinceStartupAsDouble;return;}
            EnsureProfiles();UpdateRoster();
            if(State.phase==GameFlowPhase.LoadingGame||State.phase==GameFlowPhase.Playing||State.phase==GameFlowPhase.Results||State.phase==GameFlowPhase.Failed&&State.pending==GameFlowEffect.LoadGame)TickMatch(Time.unscaledTime);
            var s=State;
            if(!s.demo&&s.automatic&&(s.phase==GameFlowPhase.Selected||s.phase==GameFlowPhase.WaitingReady))
                foreach(var id in s.playlistIds)if(Array.IndexOf(s.readyIds,id)<0&&flow.SetReady(s.authorityTerm,id,s.selectionVersion,s.rosterVersion,true))CommitFlow();
            double now=Time.realtimeSinceStartupAsDouble,elapsed=Math.Max(0,now-lastTick);lastTick=now;
            while(elapsed>0)
            {
                double step=Math.Min(5,elapsed);elapsed-=step;
                if(flow.Tick(State.authorityTerm,step,ProfileAvailable(State.profileId,State.humanIds.Length),LobbyVisible))CommitFlow();
            }
            ReconcileLobbyCompletion();DispatchOutbox();
        }
        bool ActiveMatchVisible=>Services.Session is CastlePlaySession active&&!active.VisitingLobby&&
            NetworkActors.Alive(active.Player)&&NetworkActors.Alive(active.Hand)&&active.ActiveCamera&&
            !room.RoomSceneBusy&&!room.Departing&&!room.SwitchingMap&&!room.Migrating;
        bool LobbyVisible=>IslandLobby.Current&&!IslandLobby.Current.VisitingLobby&&ReferenceEquals(Services.Session,IslandLobby.Current)&&NetworkActors.Alive(IslandLobby.Current.Player)&&IslandLobby.Current.ActiveCamera&&!room.RoomSceneBusy&&!room.Departing;
        internal bool ApplySelectedProfile()
        {
            var profile=CurrentProfile;
            bool applied=Experience==GameExperience.Game&&profile!=null&&room.ExperienceApplyVerifiedProfile(profile,ProfileRules(profile));
            if(applied)sceneSelectionError="";
            return applied;
        }
        internal void ReportSceneSelectionFailure()
        {
            sceneSelectionError="The curated scene selection could not be applied. Retry or leave the room.";
            var s=State;
            if(IsAuthority&&s.pending==GameFlowEffect.LoadGame&&flow.SceneFailed(s.authorityTerm,s.operationId,s.attempts))CommitFlow();
        }
        void ReconcileLobbyCompletion()
        {
            var s=State;
            if(s.pending==GameFlowEffect.ReturnLobby&&LobbyVisible&&flow.SceneCompleted(s.authorityTerm,s.operationId,GameFlowEffect.ReturnLobby))
            {
                CommitFlow();
                if(!State.automatic)SelectNext();
            }
        }
        void DispatchOutbox()
        {
            var s=State;
            if(s.demo||s.pending==GameFlowEffect.None||s.phase==GameFlowPhase.Failed||TransportRecovering)return;
            if(!TransportCheckpointReady(s))return;
            string key=s.roomId+"/"+s.authorityTerm+"/"+s.operationId+"/"+s.attempts;
            if(dispatched==key)return;
            if(s.pending==GameFlowEffect.ChooseProfile){dispatched=key;SelectNext();return;}
            if(room.Busy||room.RoomSceneBusy)return;
            dispatched=key;
            void Failed(){if(flow.SceneFailed(State.authorityTerm,s.operationId,s.attempts))CommitFlow();}
            if(s.pending==GameFlowEffect.LoadGame)
            {
                var profile=CurrentProfile;
                if(profile==null||!ProfileAvailable(profile.id,s.humanIds.Length)){Failed();return;}
                if(TryPrepareRematch(profile)){return;}
                if(!room.ExperienceSelect(profile,ProfileRules(profile))||!room.ExperienceLoadGame(profile,s.operationId,s.attempts,Failed))Failed();
            }
            else if(s.pending==GameFlowEffect.ReturnLobby&&!room.ExperienceReturnLobby(s.operationId,s.attempts,Failed))Failed();
        }
        internal void MatchLoadCompleted()
        {
            var s=State;if(s.pending==GameFlowEffect.LoadGame&&flow.SceneCompleted(s.authorityTerm,s.operationId,GameFlowEffect.LoadGame))CommitFlow();
        }
        internal bool AuthorizeModeStart()
        {
            var s=State;
            if(flow.PrepareModeStart(s.authorityTerm,s.operationId))CommitFlow();
            s=State;return s.modeStartOperation==s.operationId&&s.modeStartRound==s.round&&TransportCheckpointReady(s);
        }
        internal void MatchResultsReached()
        {
            var s=State;if(flow.RoundEnded(s.authorityTerm,s.round))CommitFlow();
        }
    }
}
