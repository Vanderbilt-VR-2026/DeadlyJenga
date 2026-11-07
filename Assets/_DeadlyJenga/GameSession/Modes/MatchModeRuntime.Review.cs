using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        public bool DevelopmentReviewEnabled=>session&&session.developmentReviewTools&&Authority&&DeadlyJenga.CastlePrototype.SessionDevelopmentPolicy.CanExecuteLocal(session);
        public bool CanEndGame=>session&&session.developmentReviewTools&&frame.phase==RoundPhase.Playing&&
            SessionDevelopmentPolicy.CanRequestLocal(session)&&IsRoomLeader;
        bool IsRoomLeader
        {
            get
            {
                var room=IslandRoomConnection.Instance;
                return !room||!room.Connected?Authority:room.LocalIsLobbyHost;
            }
        }
        public string PlayerInRole(MysteryRole role)
        {
            if(!DevelopmentReviewEnabled||!MysteryActive||(role!=MysteryRole.Murderer&&role!=MysteryRole.Sheriff))return "";
            foreach(var p in simulation.Participants)if(p.present&&mystery.Role(p.frame.id)==role)return p.frame.id;
            return "";
        }
        public void WatchPlayer(string id)
        {
            if(!DevelopmentReviewEnabled||string.IsNullOrEmpty(id))return;
            if(id==LocalActorId){SetDemoSpectating(false);session.SetMenu(false);return;}
            GatherDemoCandidates();int at=demoCandidates.FindIndex(p=>p.id==id);if(at<0)return;
            SetDemoFourViews(false);SetDemoSpectating(true);demoOffset=at;
            for(int i=0;i<4;i++)demoTargets[i]=null;
            TickDemoSpectator();session.SetMenu(false);
        }
        public void EndGame()
        {
            if(!CanEndGame)return;
            if(Authority)EndGameForDevelopment();
            else CastleDemoCommands.Run(session,CastleDemoAction.EndGame);
            session.SetMenu(false);
        }
        internal bool EndGameForDevelopment()
        {
            if(!Authority||!session||!session.developmentReviewTools||frame.phase!=RoundPhase.Playing||
                !SessionDevelopmentPolicy.CanExecuteServer(session))return false;
            simulation.EndForDevelopmentReview();frame=simulation.Snapshot();PublishEnemyActions();PublishEquipment();
            if(MysteryActive)frame.mystery=MysteryFrame();
            ApplyFrames();
            return true;
        }
    }
}
