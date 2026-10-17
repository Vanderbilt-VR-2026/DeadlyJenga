using TMPro;
using UnityEngine;
using DeadlyJenga.Core;

// Sits on the Lobby Menu prefab. Every action goes to Services.Lobby (Core), which GameSession provides.
// LobbyMatchSetup is the main menu now: it borrows this prefab's room-code field for its keypad, and the
// prefab's old Home / Host / Join panels stay hidden (kept because the scene's buttons are wired to them).
public class LobbyRoomCode : MonoBehaviour
{
    [SerializeField] TMP_InputField codeInput;
    [SerializeField] TMP_Text roomCodeLabel;

    static string lastJoinCode = "";
    CanvasGroup oldPanels;
    Transform hostMenu, homeMenu, joinMenu;

    public TMP_InputField CodeInput => codeInput;

    void Awake()
    {
        if (FindFirstObjectByType<LobbyMatchSetup>() == null)
            new GameObject("Match Setup").AddComponent<LobbyMatchSetup>();
    }

    void Start()
    {
        if (codeInput && string.IsNullOrEmpty(codeInput.text)) codeInput.SetTextWithoutNotify(lastJoinCode);
        var canvas = codeInput ? codeInput.GetComponentInParent<Canvas>(true) : null;
        if (!canvas) return;
        var root = canvas.transform;
        oldPanels = root.GetComponent<CanvasGroup>() ?? root.gameObject.AddComponent<CanvasGroup>();
        hostMenu = root.Find("Host Menu");
        homeMenu = root.Find("Home Menu");
        joinMenu = root.Find("Join Menu");
    }

    void Update()
    {
        if (homeMenu && homeMenu.gameObject.activeSelf) homeMenu.gameObject.SetActive(false);
        if (hostMenu && hostMenu.gameObject.activeSelf) hostMenu.gameObject.SetActive(false);
        if (joinMenu && joinMenu.gameObject.activeSelf && Services.Lobby != null && Services.Lobby.Connected) joinMenu.gameObject.SetActive(false);
        if (oldPanels) { oldPanels.alpha = 0; oldPanels.interactable = false; oldPanels.blocksRaycasts = false; }

        if (roomCodeLabel == null) return;
        string label = Services.Lobby != null ? Services.Lobby.RoomLabel : "Room Code: —";
        if (roomCodeLabel.text != label) roomCodeLabel.text = label;
    }

    public void Host() => Services.Lobby?.Host();
    public void Join()
    {
        string code = codeInput ? codeInput.text : "";
        lastJoinCode = code.Trim().ToUpperInvariant();
        Services.Lobby?.Join(code);
    }
    public void Leave() => Services.Lobby?.Leave();
    public void StartMatch() => Services.Lobby?.StartMatch();
    public void CancelMatch() => Services.Lobby?.CancelMatch();
}
