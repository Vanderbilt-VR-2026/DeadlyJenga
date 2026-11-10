using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class RoomGameExperience
    {
        CastlePlaySession match;
        readonly List<(string name, Color color, AvatarAppearance? appearance)> returningBots = new();
        internal void ApplyReturningBot(GameObject bot, int index)
        {
            if(index>=returningBots.Count)return;
            var saved=returningBots[index];
            var actor=bot.GetComponent<IModeActor>();
            if(actor!=null&&(actor.DisplayName!=saved.name||actor.Color!=saved.color))
                actor.SetIdentity(actor.ActorId,saved.name,-1,saved.color);
            if(saved.appearance.HasValue&&bot.GetComponent<IAvatarAppearance>() is { } wardrobe&&
                !wardrobe.Appearance.Equals(saved.appearance.Value))wardrobe.ApplyAppearance(saved.appearance.Value);
        }
        void RememberReturningBots()
        {
            returningBots.Clear();
            var members=match?match.SurvivorBots?.Members:null;
            if(members==null)return;
            foreach(var bot in members)
            {
                if(!bot||returningBots.Count==9||bot.GetComponent<IEnemyActor>()?.IsEnemy==true)continue;
                var actor=bot.GetComponent<IModeActor>();
                if(actor==null||!actor.IsBot)continue;
                returningBots.Add((actor.DisplayName,actor.Color,bot.GetComponent<IAvatarAppearance>()?.Appearance));
            }
        }
        string matchMap="",matchMode="",preparingOperation="";
        int preparingAttempt,preparingRound,preparingTerm;
        bool matchEventsBound,preparingRematch,matchRulesApplied,populationReset,modeStartIssued,startPermit,matchFailed,resultsNotified;
        bool deadlineArmed;
        float preparationDeadline,nextPopulationCheck;
        internal string MatchStatus {get;private set;}="";
        internal bool ParticipationMappingReady
        {
            get
            {
                if(TransportRecovering||WaitingForRoster)return false;
                var s=State;
                foreach(var stable in s.launchIds)
                    if(Array.IndexOf(s.humanIds,stable)>=0&&string.IsNullOrEmpty(room.GameExperienceActorId(stable)))return false;
                return true;
            }
        }

        internal bool MigratedLaunchRosterPresent
        {
            get
            {
                // The original host is gone. Give the rest of the launched cohort
                // time to reattach before reconciling the restored participant rows.
                var ids=State.launchIds;
                if(ids.Length<=1)return true;
                int present=0;
                var roster=room.GameExperienceRosterIds;
                foreach(var stable in ids)if(Array.IndexOf(roster,stable)>=0)present++;
                return present>=ids.Length-1;
            }
        }
        void BindMatchEvents()
        {
            if(matchEventsBound)return;
            matchEventsBound=true;GameEvents.SessionStarted+=OnMatchStarted;
            FindExistingMatch();
        }
        void UnbindMatchEvents()
        {
            if(matchEventsBound)GameEvents.SessionStarted-=OnMatchStarted;
            matchEventsBound=false;
            if(match&&match.HasWorldAuthority&&match.SurvivorBots!=null)match.SurvivorBots.AutoFill=false;
            match=null;MatchStatus="";
        }
        void OnMatchStarted(GameObject root)
        {if(root)AttachMatch(root.GetComponent<CastlePlaySession>());}
        void FindExistingMatch()
        {
            var candidate=Services.Session as CastlePlaySession;
            if(!candidate)candidate=UnityEngine.Object.FindFirstObjectByType<CastlePlaySession>();
            if(candidate&&Manages(candidate))AttachMatch(candidate);
        }
        internal void AttachMatch(CastlePlaySession session)
        {
            if(!session||!Manages(session))return;
            match=session;matchMap=MatchSetup.MapId;matchMode=MatchSetup.ModeId;
            ResetMatchPreparation(false);
        }
        void ResetMatchPreparation(bool rematch)
        {
            var s=flow.State;
            preparingOperation=s.operationId;preparingAttempt=s.attempts;preparingRound=s.round;preparingTerm=s.authorityTerm;
            preparingRematch=rematch;matchRulesApplied=false;populationReset=false;modeStartIssued=false;
            matchFailed=false;resultsNotified=false;deadlineArmed=false;nextPopulationCheck=0;startPermit=false;
            MatchStatus="Preparing actual match participants…";
        }
        internal bool TryPrepareRematch(CuratedGameProfile profile)
        {
            if(!match||match.VisitingLobby||profile==null||matchMap!=profile.mapId||matchMode!=profile.modeId)return false;
            // A same-scene request with the wrong phase must not become an unannounced reset.
            if(!IsAuthority||!match.HasWorldAuthority||!match.Modes||match.Modes.Frame?.phase!=RoundPhase.Results)
            {FailMatch("This match has not reached Results. Wait or return to the lobby.");return true;}
            ResetMatchPreparation(true);return true;
        }
        void FailMatch(string reason)
        {
            matchFailed=true;MatchStatus=reason;
            if(match&&match.HasWorldAuthority&&match.SurvivorBots!=null)match.SurvivorBots.AutoFill=false;
            var s=flow.State;
            if(IsAuthority&&s.pending==GameFlowEffect.LoadGame&&flow.SceneFailed(s.authorityTerm,s.operationId,s.attempts))CommitFlow();
        }

        // The only route into the internal Modes starter is this synchronous checked window.
        internal static bool TryConsumeCuratedStartPermit(CastlePlaySession session)
        {
            var owner=IslandRoomConnection.Instance;
            var provider=owner?owner.GetComponent<RoomGameExperience>():null;
            if(!provider||!ReferenceEquals(provider.match,session)||!provider.startPermit||!provider.IsAuthority||!session.HasWorldAuthority)return false;
            var s=provider.flow.State;
            bool allowed=s.phase==GameFlowPhase.LoadingGame&&s.pending==GameFlowEffect.LoadGame&&
                s.operationId==provider.preparingOperation&&s.round==provider.preparingRound&&
                s.modeStartOperation==s.operationId&&s.modeStartRound==s.round;
            if(allowed)provider.startPermit=false;
            return allowed;
        }
        internal static RoomGameExperience ForManagedMatch(CastlePlaySession session)
        {
            var owner=IslandRoomConnection.Instance;
            return owner&&Manages(session)?owner.GetComponent<RoomGameExperience>():null;
        }
        internal bool HandleLegacyModeRematch(string actorId)
        {
            var s=flow.State;
            if(!IsAuthority||s.phase!=GameFlowPhase.Results||string.IsNullOrEmpty(actorId))return false;
            foreach(var stable in s.humanIds)
                if(room.GameExperienceActorId(stable)==actorId&&room.GameExperienceIsLeader(stable))
                {
                    HandleAuthoritativeCommand(stable,new GameExperienceCommand {verb="rematch",authorityTerm=s.authorityTerm,selectionVersion=s.selectionVersion,rosterVersion=s.rosterVersion,round=s.round});
                    return flow.State.phase==GameFlowPhase.LoadingGame;
                }
            return false;
        }

        bool ObserveCohort(CuratedGameProfile profile,out int humans,out int bots,out string waiting)
        {
            humans=0;bots=0;waiting="";
            var state=flow.State;
            var actorIds=new HashSet<string>(StringComparer.Ordinal);
            foreach(var stableId in state.launchIds)
            {
                if(Array.IndexOf(state.humanIds,stableId)<0)continue;
                string actorId=room.GameExperienceActorId(stableId);
                var actor=string.IsNullOrEmpty(actorId)?null:match.Modes.FindActor(actorId);
                var frame=string.IsNullOrEmpty(actorId)?null:Array.Find(match.Modes.Frame.players,p=>p!=null&&p.id==actorId);
                if(actor==null||actor.IsBot||frame==null||!actorIds.Add(actorId))
                {waiting="Waiting for a real player's match actor and mode snapshot…";continue;}
                humans++;
            }
            int expected=0;foreach(var id in state.launchIds)if(Array.IndexOf(state.humanIds,id)>=0)expected++;
            if(expected==0){waiting="No launch participants remain. Waiting for room recovery.";return false;}
            var roots=new HashSet<int>();
            if(match.SurvivorBots!=null&&match.SurvivorBots.Members!=null)
                foreach(var root in match.SurvivorBots.Members)
                {
                    if(!root||!root.activeInHierarchy||!roots.Add(root.GetInstanceID()))continue;
                    var actor=root.GetComponent<IModeActor>();
                    if(actor==null||!actor.IsBot||!actor.Alive||root.GetComponent<IEnemyActor>()?.IsEnemy==true)continue;
                    // A spawned body is not yet a mode participant until the runtime samples it.
                    bool observed=false;
                    foreach(var row in match.Modes.Frame.players)
                        if(row!=null&&row.bot&&ReferenceEquals(match.Modes.FindActor(row.id)?.gameObject,root)){observed=true;break;}
                    if(observed)bots++;
                }
            if(humans!=expected)return false;
            if(humans>profile.maximumPopulation){waiting="The actual human cohort exceeds this prototype profile. Return to the lobby.";return false;}
            return true;
        }

        internal void TickMatch(float now)
        {
            if(float.IsNaN(now)||float.IsInfinity(now))return;
            var s=flow.State;
            if(s.demo){MatchStatus="";return;}
            if(!match)FindExistingMatch();
            if(!match||!Manages(match)){MatchStatus="Waiting for the selected game session…";return;}
            if(!IsAuthority||!match.HasWorldAuthority){MatchStatus="Waiting for the host's match state…";return;}
            if(match.SurvivorBots!=null)match.SurvivorBots.AutoFill=false;
            if(s.phase==GameFlowPhase.Playing)
            {
                MatchStatus=s.playerPlaylist?"Round in progress.":"PROTOTYPE · round running · no disasters";
                if(!resultsNotified&&match.Modes&&match.Modes.Frame?.phase==RoundPhase.Results)
                {resultsNotified=true;MatchResultsReached();MatchStatus="Round results · Next Round rematches; Return keeps the room.";}
                return;
            }
            if(s.phase!=GameFlowPhase.LoadingGame||s.pending!=GameFlowEffect.LoadGame)return;
            // Dispatcher owns deciding whether an old match can be reused for this new operation.
            if(preparingOperation!=s.operationId||preparingRound!=s.round)return;
            if(preparingAttempt!=s.attempts)
            {preparingAttempt=s.attempts;matchFailed=false;deadlineArmed=false;}
            if(preparingTerm!=s.authorityTerm){preparingTerm=s.authorityTerm;deadlineArmed=false;}
            if(matchFailed)return;
            if(!deadlineArmed){deadlineArmed=true;preparationDeadline=now+20;}
            if(now>preparationDeadline){FailMatch("Match preparation timed out before the actual cohort was ready. Retry or return to the lobby.");return;}
            var profile=CurrentProfile;
            if(profile==null||profile.hands!=0||profile.hazards==null||profile.hazards.Length!=0)
            {FailMatch("This initial prototype supports profiles with no AI hands or disasters.");return;}
            if(matchMap!=profile.mapId||matchMode!=profile.modeId)
            {FailMatch("The loaded match map or mode does not match the selected curated profile.");return;}
            if(!match.Modes||match.Modes.Frame==null||match.Modes.Frame.players==null||match.SurvivorBots==null)
            {MatchStatus="Waiting for match initialization and bot population…";return;}
            if(!matchRulesApplied)
            {
                // ExperienceSelect seeds these rules before deferred Initialize. Public Configure is a debug command.
                var expected=ProfileRules(profile);
                if(expected==null||expected.mode==GameModeKind.Demo||expected.mode!=MatchModeRuntime.ParseMode(profile.modeId)||
                    !s.playerPlaylist&&expected.automaticDisasters||JsonUtility.ToJson(expected)!=JsonUtility.ToJson(match.Modes.Rules))
                {FailMatch("The initialized match rules do not match the selected curated profile.");return;}
                matchRulesApplied=true;
            }
            var phase=match.Modes.Frame.phase;
            bool committed=s.modeStartOperation==s.operationId&&s.modeStartRound==s.round;
            if(phase==RoundPhase.Countdown||phase==RoundPhase.Playing)
            {
                if(!committed){FailMatch("Match started without the curated start receipt. Deferred startup integration is required.");return;}
                // Marker was published after staffing checks; never restart a recovered active round.
                modeStartIssued=true;MatchLoadCompleted();MatchStatus="PROTOTYPE · round starting · no disasters";return;
            }
            if(phase!=RoundPhase.Waiting&&!(preparingRematch&&phase==RoundPhase.Results))
            {FailMatch("The selected match is not in a safe pre-start phase.");return;}
            if(!populationReset)
            {
                // Clear inherited or previous-round bots once, then observe the resulting population.
                match.SurvivorBots.Clear();match.HandBots?.Clear();populationReset=true;
                nextPopulationCheck=now+.25f;return;
            }
            if(now<nextPopulationCheck)return;nextPopulationCheck=now+.25f;
            var state=flow.State;int expectedHumans=0;
            foreach(var id in state.launchIds)if(Array.IndexOf(state.humanIds,id)>=0)expectedHumans++;
            if(expectedHumans<1){MatchStatus="No launch participants remain. Waiting for room recovery.";return;}
            if(expectedHumans>profile.maximumPopulation){FailMatch("The human launch cohort exceeds the prototype maximum.");return;}
            var population=match.SurvivorBots;
            population.SetHumanPlayerCount(expectedHumans);population.TargetPopulation=Math.Max(expectedHumans,profile.targetPopulation);
            ObserveCohort(profile,out int humans,out int bots,out string waiting);
            int target=Math.Max(expectedHumans,profile.targetPopulation);
            if(humans+bots>profile.maximumPopulation){FailMatch("Observed actors exceed the prototype population cap.");return;}
            if(humans<expectedHumans){MatchStatus=waiting;return;}
            if(humans+bots<target)
            {
                // Capacity/SpawnOne return is not proof: success is observed next time through Members+Frame.
                if(population.Count<target-expectedHumans&&population.Count<population.Capacity)population.SpawnOne();
                MatchStatus="Preparing players and bots · "+(humans+bots)+"/"+target+" · waiting for real bot actors";return;
            }
            if(humans+bots<profile.minimumPopulation){MatchStatus="Waiting for the profile's minimum observed population…";return;}
            if(modeStartIssued)return;
            if(!AuthorizeModeStart()){MatchStatus="Cohort ready · waiting for the replicated start receipt…";return;}
            startPermit=true;
            try
            {
                if(!match.Modes.StartCuratedRound()){FailMatch("The trusted curated round start was refused.");return;}
                modeStartIssued=true;MatchStatus="Cohort ready · waiting for the actual round countdown…";
            }
            catch(Exception error)
            {
                Debug.LogException(error,match);
                FailMatch("The round start failed before its ready frame was confirmed. Retry or return to the lobby.");
            }
            finally{startPermit=false;}
        }
    }
}
