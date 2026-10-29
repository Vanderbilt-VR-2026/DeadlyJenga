using DeadlyJenga.Core;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        public bool DevelopmentReviewEnabled=>session&&session.developmentReviewTools&&Authority;
        public bool CanEndGame=>DevelopmentReviewEnabled&&frame.phase==RoundPhase.Playing;
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
            simulation.EndForDevelopmentReview();frame=simulation.Snapshot();PublishEnemyActions();PublishEquipment();
            if(MysteryActive)frame.mystery=MysteryFrame();
            ApplyFrames();session.SetMenu(false);
        }
    }
}
