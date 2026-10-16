using UnityEngine;

namespace DeadlyJenga.Core.Testing
{
    // Stand-in session for sandbox scenes. Registers itself as Services.Session.
    public class FakeSession : MonoBehaviour, ISessionInfo
    {
        public bool MenuOpen { get; private set; }
        public void SetMenu(bool open) => MenuOpen = open;

        void OnEnable() => Services.Session = this;
        void OnDisable() { if (ReferenceEquals(Services.Session, this)) Services.Session = null; }
    }
}
