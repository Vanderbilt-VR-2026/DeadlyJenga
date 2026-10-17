using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;

// The Lobby scene's connection logic: host, join, leave, the countdown, solo practice, connection
// messages and the shared lobby screen. Offered to the lobby menus (Presentation) as Services.Lobby.
// Draws nothing. Added automatically to the FusionBootstrap object in any scene that has a LobbyCountdown.
[DisallowMultipleComponent]
public sealed class LobbyConnection : MonoBehaviour, ILobbyControls
{
    const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public static LobbyConnection Current { get; private set; }
    FusionBootstrap bootstrap;
    LobbyCountdown countdown;

    // What the host's menu last published; LobbyCountdown copies it to everyone.
    public bool HasPublishedScreen { get; private set; }
    public int PublishedPage { get; private set; }
    public Pose PublishedPose { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook() { SceneManager.sceneLoaded -= Install; SceneManager.sceneLoaded += Install; }

    static void Install(Scene scene, LoadSceneMode mode)
    {
        var countdown = FindFirstObjectByType<LobbyCountdown>(FindObjectsInactive.Include);
        var bootstrap = FindFirstObjectByType<FusionBootstrap>(FindObjectsInactive.Include);
        if (!countdown || !bootstrap || bootstrap.GetComponent<LobbyConnection>()) return;
        var connection = bootstrap.gameObject.AddComponent<LobbyConnection>();
        connection.bootstrap = bootstrap; connection.countdown = countdown;
    }

    void OnEnable() { Current = this; Services.Lobby = this; }
    void OnDisable()
    {
        if (ReferenceEquals(Services.Lobby, this)) Services.Lobby = null;
        if (Current == this) Current = null;
    }

    // Give the local XR rig its body before Fusion ever asks it for input.
    void Start() => LocalPlayerInput.FindOrCreate();

    NetworkRunner ActiveRunner => NetworkRunner.Instances.FirstOrDefault(r => r && r.IsRunning);
    bool Ready => countdown && countdown.IsReady && countdown.Object;

    public bool Connected => ActiveRunner != null;
    public bool Connecting => bootstrap && bootstrap.ConnectionBusy;
    public bool CanEdit => !Connecting && (!Connected || (Ready && countdown.Object.HasStateAuthority));
    public string Status => FusionBootstrap.ConnectionMessage;
    public bool StatusIsError => FusionBootstrap.ConnectionError;
    public void ShowMessage(string text, bool error) => FusionBootstrap.SetConnectionMessage(text, error);

    public bool HasSharedScreen => Connected && Ready && !countdown.Object.HasStateAuthority;
    public int SharedPage => Ready ? countdown.SelectedPage : 0;
    public Pose SharedScreenPose => Ready ? new Pose(countdown.ScreenPosition, countdown.ScreenRotation) : default;
    public void PublishScreen(int page, Pose pose)
    {
        if (!CanEdit) return;
        PublishedPage = page; PublishedPose = pose; HasPublishedScreen = true;
    }

    public string RoomLabel
    {
        get
        {
            // The session name is already shared by Fusion, including with late joiners.
            var runner = ActiveRunner;
            string code = runner != null && runner.SessionInfo.IsValid ? runner.SessionInfo.Name : null;
            if (countdown != null && countdown.TryGetLabel(out var label)) return label;
            return string.IsNullOrEmpty(code) ? "Room Code: —" : $"Room Code: {code}";
        }
    }

    public void Host()
    {
        if (!CanConnect()) return;
        MatchSetup.FromLobby = true; MatchSetup.StartLocalSession = false;
        string code = GenerateCode(4);
        bootstrap.DefaultRoomName = code;
        Debug.Log($"Hosting room {code}");
        bootstrap.StartHost();
    }

    public void Join(string code)
    {
        if (!CanConnect()) return;
        code = (code ?? "").Trim().ToUpperInvariant();
        if (code.Length != 4 || code.Any(c => Alphabet.IndexOf(c) < 0))
        {
            ShowMessage("Enter a valid 4-character room code using the keypad.", true);
            return;
        }
        MatchSetup.FromLobby = true; MatchSetup.StartLocalSession = false;
        bootstrap.DefaultRoomName = code;
        Debug.Log($"Joining room {code}");
        bootstrap.StartClient();
    }

    public void Leave()
    {
        FusionBootstrap.MarkIntentionalLeave();
        if (bootstrap) bootstrap.Shutdown();
        else if (ActiveRunner) _ = ActiveRunner.Shutdown();
    }

    public void StartMatch() { if (Ready) countdown.StartCountdown(); }
    public void CancelMatch() { if (Ready) countdown.CancelCountdown(); }

    public bool TryStartSolo(out string problem)
    {
        if (Connecting || Connected)
        { problem = "Wait for the connection to finish or leave the room before starting solo practice."; ShowMessage(problem, true); return false; }
        problem = null;
        MatchSetup.FromLobby = true; MatchSetup.StartLocalSession = false;
        SceneManager.LoadScene("Game");
        return true;
    }

    bool CanConnect()
    {
        if (!bootstrap)
        {
            ShowMessage("Multiplayer is not configured. The lobby network component is missing.", true);
            return false;
        }
        if (bootstrap.CurrentStage != FusionBootstrap.Stage.Disconnected) return false;
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            ShowMessage("No network connection. Connect to Wi-Fi or Ethernet, then try again.", true);
            return false;
        }
        return true;
    }

    static string GenerateCode(int length)
    {
        var chars = new char[length];
        for (int i = 0; i < length; i++) chars[i] = Alphabet[Random.Range(0, Alphabet.Length)];
        return new string(chars);
    }
}
