using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;

// The Lobby scene's connection logic: host, join, leave, the countdown and solo practice.
// Offered to the lobby menus (Presentation) as Services.Lobby. Draws nothing.
// Added automatically to the FusionBootstrap object in any scene that has a LobbyCountdown.
[DisallowMultipleComponent]
public sealed class LobbyConnection : MonoBehaviour, ILobbyControls
{
    const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    FusionBootstrap bootstrap;
    LobbyCountdown countdown;

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

    void OnEnable() => Services.Lobby = this;
    void OnDisable() { if (ReferenceEquals(Services.Lobby, this)) Services.Lobby = null; }

    public string RoomLabel
    {
        get
        {
            // The session name is already shared by Fusion, including with late joiners.
            // This project runs one session per device; ignore the inactive runner template.
            string code = null;
            foreach (var runner in NetworkRunner.Instances)
                if (runner != null && runner.IsRunning && runner.SessionInfo.IsValid) { code = runner.SessionInfo.Name; break; }
            if (countdown != null && countdown.TryGetLabel(out var label)) return label;
            return string.IsNullOrEmpty(code) ? "Room Code: —" : $"Room Code: {code}";
        }
    }

    public void Host()
    {
        MatchSetup.FromLobby = true; MatchSetup.StartLocalSession = false;
        string code = GenerateCode(4);
        bootstrap.DefaultRoomName = code;
        Debug.Log($"Hosting room {code}");
        bootstrap.StartHost();
    }

    public void Join(string code)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code)) { Debug.LogWarning("Enter a room code before joining."); return; }
        MatchSetup.FromLobby = true; MatchSetup.StartLocalSession = false;
        bootstrap.DefaultRoomName = code;
        Debug.Log($"Joining room {code}");
        bootstrap.StartClient();
    }

    public void Leave() => bootstrap.Shutdown();
    public void StartMatch() { if (countdown) countdown.StartCountdown(); }
    public void CancelMatch() { if (countdown) countdown.CancelCountdown(); }

    public bool TryStartSolo(out string problem)
    {
        if (NetworkRunner.Instances.Any(r => r != null && r.IsRunning))
        { problem = "Leave the current room before starting solo practice."; return false; }
        problem = null;
        MatchSetup.FromLobby = true; MatchSetup.StartLocalSession = false;
        SceneManager.LoadScene("Game");
        return true;
    }

    static string GenerateCode(int length)
    {
        var chars = new char[length];
        for (int i = 0; i < length; i++) chars[i] = Alphabet[Random.Range(0, Alphabet.Length)];
        return new string(chars);
    }
}
