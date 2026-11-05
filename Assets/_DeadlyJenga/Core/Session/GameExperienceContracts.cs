namespace DeadlyJenga.Core
{
    public enum GameExperience { Game, Demo }
    public enum RoomGamePhase { Idle, Choosing, Selected, WaitingReady, LoadingGame, Playing, Results, LoadingLobby, Failed }

    // Optional capability on the lobby/session; existing interfaces remain unchanged.
    public interface IGameExperienceControls
    {
        GameExperience Experience { get; }
        bool CanConfigureExperience { get; }
        void SetExperience(GameExperience value);
        bool AutomaticRounds { get; }
        void SetAutomaticRounds(bool value);
        RoomGamePhase GamePhase { get; }
        string GameStatus { get; }
        string ProfileId { get; }
        float CountdownSeconds { get; }
        int MinimumPopulation { get; }
        int MaximumPopulation { get; }
        bool LocalReady { get; }
        int ReadyCount { get; }
        int RequiredReadyCount { get; }
        void SetReady(bool value);
        bool CanCancelGameStart { get; }
        void CancelGameStart();
        bool CanRetryGameTransition { get; }
        void RetryGameTransition();
        string GameActionHint { get; }
    }
}
