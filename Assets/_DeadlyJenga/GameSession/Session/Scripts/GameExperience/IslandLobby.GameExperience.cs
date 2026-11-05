using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobby : IGameExperienceControls
    {
        RoomGameExperience ExperienceProvider=>RoomGameExperience.Ensure(IslandRoomConnection.Instance);
        public GameExperience Experience=>ExperienceProvider?.Experience??GameExperience.Demo;
        public bool CanConfigureExperience=>ExperienceProvider?.CanConfigureExperience??false;
        public bool AutomaticRounds=>ExperienceProvider?.AutomaticRounds??false;
        public RoomGamePhase GamePhase=>ExperienceProvider?.GamePhase??RoomGamePhase.Idle;
        public string GameStatus=>ExperienceProvider?.GameStatus??"Demo";
        public string ProfileId=>ExperienceProvider?.ProfileId??"";
        public float CountdownSeconds=>ExperienceProvider?.CountdownSeconds??0;
        public int MinimumPopulation=>ExperienceProvider?.MinimumPopulation??0;
        public int MaximumPopulation=>ExperienceProvider?.MaximumPopulation??0;
        public bool LocalReady=>ExperienceProvider?.LocalReady??false;
        public int ReadyCount=>ExperienceProvider?.ReadyCount??0;
        public int RequiredReadyCount=>ExperienceProvider?.RequiredReadyCount??0;
        public bool CanCancelGameStart=>ExperienceProvider?.CanCancelGameStart??false;
        public bool CanRetryGameTransition=>ExperienceProvider?.CanRetryGameTransition??false;
        public string GameActionHint=>ExperienceProvider?.GameActionHint??"";
        public void SetExperience(GameExperience value)=>ExperienceProvider?.SetExperience(value);
        public void SetAutomaticRounds(bool value)=>ExperienceProvider?.SetAutomaticRounds(value);
        public void SetReady(bool value)=>ExperienceProvider?.SetReady(value);
        public void CancelGameStart()=>ExperienceProvider?.CancelGameStart();
        public void RetryGameTransition()=>ExperienceProvider?.RetryGameTransition();
    }
}
