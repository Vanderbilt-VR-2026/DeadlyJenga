using Fusion;
using TMPro;
using UnityEngine;

public class LobbyRoomCode : MonoBehaviour
{
    [SerializeField] FusionBootstrap bootstrap;
    [SerializeField] TMP_InputField codeInput;
    [SerializeField] TMP_Text roomCodeLabel;

    const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    void Awake()
    {
        if (bootstrap == null)
        {
            bootstrap = GetComponent<FusionBootstrap>();
        }
    }

    public void Host()
    {
        string code = GenerateCode(4);
        bootstrap.DefaultRoomName = code;

        if (roomCodeLabel != null)
        {
            roomCodeLabel.text = $"Room Code: {code}";
        }

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
