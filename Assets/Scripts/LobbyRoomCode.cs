using Fusion;
using TMPro;
using UnityEngine;
using DeadlyJenga.CastlePrototype;

public class LobbyRoomCode : MonoBehaviour
{
    [SerializeField] FusionBootstrap bootstrap;
    [SerializeField] TMP_InputField codeInput;
    [SerializeField] TMP_Text roomCodeLabel;
    [SerializeField] LobbyCountdown countdown;

    const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    string displayedCode;
    static string lastJoinCode = "";
    CanvasGroup connectionMenu;
    Transform oldHostMenu, homeMenu, joinMenu;
    bool wasConnected;
    public bool ShowingJoin => false;
    public TMP_InputField CodeInput => codeInput;
    public void ShowJoin()
    {
        if (codeInput) codeInput.ActivateInputField();
    }

    void Awake()
    {
        if (bootstrap == null)
        {
            bootstrap = GetComponent<FusionBootstrap>();
        }
        if (FindFirstObjectByType<LobbyMatchSetup>() == null)
            new GameObject("Match Setup").AddComponent<LobbyMatchSetup>();
    }

    void Start()
    {
        // Give the local hardware rig its body before Fusion ever requests input.
        LocalHandInput.FindOrCreate();
        if (codeInput && string.IsNullOrEmpty(codeInput.text)) codeInput.SetTextWithoutNotify(lastJoinCode);
        // Keep the scene NetworkObject active, but replace its old connected-room UI.
        var legacyCanvas = codeInput ? codeInput.GetComponentInParent<Canvas>(true) : null;
        if (legacyCanvas)
        {
            var root = legacyCanvas.transform;
            connectionMenu = root.GetComponent<CanvasGroup>() ?? root.gameObject.AddComponent<CanvasGroup>();
            oldHostMenu = root.Find("Host Menu");
            homeMenu = root.Find("Home Menu");
            joinMenu = root.Find("Join Menu");
        }
    }

    void Update()
    {
        // The session name is already shared by Fusion, including with late joiners.
        // This project runs one session per device; ignore the inactive runner template.
        string sessionCode = null;
        foreach (var runner in NetworkRunner.Instances)
        {
            if (runner != null && runner.IsRunning && runner.SessionInfo.IsValid)
            {
                sessionCode = runner.SessionInfo.Name;
                break;
            }
        }

        bool connected = sessionCode != null;
        // These are plain UI panels; the countdown NetworkObject stays enabled.
        if (homeMenu && homeMenu.gameObject.activeSelf) homeMenu.gameObject.SetActive(false);
        if (oldHostMenu && oldHostMenu.gameObject.activeSelf) oldHostMenu.gameObject.SetActive(false);
        if (connectionMenu)
        {
            connectionMenu.alpha = !connected && ShowingJoin ? 1 : 0;
            connectionMenu.interactable = !connected && ShowingJoin;
            connectionMenu.blocksRaycasts = !connected && ShowingJoin;
            if (wasConnected && !connected)
            {
                if (oldHostMenu) oldHostMenu.gameObject.SetActive(false);
                if (joinMenu) joinMenu.gameObject.SetActive(false);
                if (homeMenu) homeMenu.gameObject.SetActive(true);
            }
        }
        wasConnected = connected;
        if (roomCodeLabel == null) return;
        string label = string.IsNullOrEmpty(sessionCode)
            ? "Room Code: —"
            : $"Room Code: {sessionCode}";
        if (countdown != null && countdown.TryGetLabel(out var countdownLabel))
            label = countdownLabel;
        if (displayedCode != label || roomCodeLabel.text != label)
        {
            roomCodeLabel.text = label;
            displayedCode = label;
        }
    }

    public void Host()
    {
        if (!CanConnect()) return;
        MatchSetup.FromLobby = true;
        MatchSetup.StartLocalSession = false;
        string code = GenerateCode(4);
        bootstrap.DefaultRoomName = code;

        Debug.Log($"Hosting room {code}");
        bootstrap.StartHost();
    }

    public void Join()
    {
        if (!CanConnect()) return;
        string code = codeInput ? codeInput.text.Trim().ToUpperInvariant() : "";
        if (code.Length != 4 || System.Array.Exists(code.ToCharArray(), c => Alphabet.IndexOf(c) < 0))
        {
            FusionBootstrap.SetConnectionMessage("Enter a valid 4-character room code using the keypad.", true);
            return;
        }

        lastJoinCode = code;
        MatchSetup.FromLobby = true;
        MatchSetup.StartLocalSession = false;
        bootstrap.DefaultRoomName = code;
        Debug.Log($"Joining room {code}");
        bootstrap.StartClient();
    }

    bool CanConnect()
    {
        if (!bootstrap)
        {
            FusionBootstrap.SetConnectionMessage("Multiplayer is not configured. The lobby network component is missing.", true);
            return false;
        }
        if (bootstrap.CurrentStage != FusionBootstrap.Stage.Disconnected) return false;
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            FusionBootstrap.SetConnectionMessage("No network connection. Connect to Wi-Fi or Ethernet, then try again.", true);
            return false;
        }
        return true;
    }

    static string GenerateCode(int length)
    {
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = Alphabet[Random.Range(0, Alphabet.Length)];
        }

        return new string(chars);
    }
}
