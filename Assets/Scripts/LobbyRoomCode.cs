using Fusion;
using TMPro;
using UnityEngine;

public class LobbyRoomCode : MonoBehaviour
{
    [SerializeField] FusionBootstrap bootstrap;
    [SerializeField] TMP_InputField codeInput;
    [SerializeField] TMP_Text roomCodeLabel;

    const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    string displayedCode;

    void Awake()
    {
        if (bootstrap == null)
        {
            bootstrap = GetComponent<FusionBootstrap>();
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

        if (roomCodeLabel == null) return;
        string label = string.IsNullOrEmpty(sessionCode)
            ? "Room Code: —"
            : $"Room Code: {sessionCode}";
        if (displayedCode != label || roomCodeLabel.text != label)
        {
            roomCodeLabel.text = label;
            displayedCode = label;
        }
    }

    public void Host()
    {
        string code = GenerateCode(4);
        bootstrap.DefaultRoomName = code;

        Debug.Log($"Hosting room {code}");
        bootstrap.StartHost();
    }

    public void Join()
    {
        string code = codeInput.text.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code))
        {
            Debug.LogWarning("Enter a room code before joining.");
            return;
        }

        bootstrap.DefaultRoomName = code;
        Debug.Log($"Joining room {code}");
        bootstrap.StartClient();
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
