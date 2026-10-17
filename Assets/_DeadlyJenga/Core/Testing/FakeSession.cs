using UnityEngine;

namespace DeadlyJenga.Core.Testing
{
    // Stand-in session for sandbox scenes. Registers itself as Services.Session.
    public class FakeSession : MonoBehaviour, ISessionInfo
    {
        public bool MenuOpen { get; private set; }
        public void SetMenu(bool open) => MenuOpen = open;

        public bool PlayingHand { get; private set; }
        public void SetRole(bool hand) { PlayingHand = hand; Debug.Log("FakeSession: role " + (hand ? "hand" : "survivor"), this); }
        public IPlayableCharacter Player => null;
        public IPlayableCharacter Hand => null;
        public bool IsVR => false;
        public Camera ActiveCamera => Camera.main;
        public bool MenuSelectPressed => false;
        public bool CanEditWorld => true;
        public void ResetMap() => Debug.Log("FakeSession: reset map", this);
        public ISurvivorBotPopulation SurvivorBots => null;
        public IBotPopulation HandBots => null;
        public IMultiplayerControls Multiplayer => null;

        void OnEnable() => Services.Session = this;
        void OnDisable() { if (ReferenceEquals(Services.Session, this)) Services.Session = null; }
    }
}
