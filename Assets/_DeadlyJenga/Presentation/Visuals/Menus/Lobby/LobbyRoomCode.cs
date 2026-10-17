using TMPro;
using UnityEngine;
using DeadlyJenga.Core;

// The lobby menu's buttons and text. Every action goes to Services.Lobby (Core), which GameSession provides.
// The Lobby scene's buttons call Host, Join, Leave, StartMatch and CancelMatch on this component.
public class LobbyRoomCode : MonoBehaviour
{
    [SerializeField] TMP_InputField codeInput;
    [SerializeField] TMP_Text roomCodeLabel;

    void Awake()
    {
        if (FindFirstObjectByType<LobbyMatchSetup>() == null)
            new GameObject("Match Setup").AddComponent<LobbyMatchSetup>();
    }

    void Update()
    {
        if (roomCodeLabel == null) return;
        string label = Services.Lobby != null ? Services.Lobby.RoomLabel : "Room Code: —";
        if (roomCodeLabel.text != label) roomCodeLabel.text = label;
    }

    public void Host() => Services.Lobby?.Host();
    public void Join() => Services.Lobby?.Join(codeInput ? codeInput.text : "");
    public void Leave() => Services.Lobby?.Leave();
    public void StartMatch() => Services.Lobby?.StartMatch();
    public void CancelMatch() => Services.Lobby?.CancelMatch();
}
