namespace DeadlyJenga.Core
{
    // What other boxes may ask of the running game. Game Session provides it.
    // Round info (time, survivors left, hands) gets added here when rounds exist.
    public interface ISessionInfo
    {
        bool MenuOpen { get; }
        void SetMenu(bool open);
    }
}
