// What other boxes may ask of the running game. Game Session provides it.
public interface ISessionInfo
{
    bool MenuOpen { get; }
    void SetMenu(bool open);
}

public static class GameSessionInfo
{
    public static ISessionInfo Current;
}
