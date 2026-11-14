using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // The survivor's side of third person: the state, the stand-still rule and the avatar pose.
    // Presentation draws the orbit camera and mirror while Inspecting is true.
    public sealed class CastleThirdPerson : MonoBehaviour, IThirdPersonInspection
    {
        IPlayableCharacter self;
        bool suspended, poseWas, vrAtStart;
        public bool Inspecting { get; private set; }

        void Awake() => self = GetComponent<IPlayableCharacter>();

        public bool CanInspect
        {
            get
            {
                var session = Services.Session;
                if (session == null || self == null || !ReferenceEquals(session.Player, self)) return false;
                if ((session as ILobbyMatchVisitControls)?.VisitingLobby == true) return false;
                if (session.Demo == null || !(session.Demo.DemoMode || session.Demo.CanCommand)) return false;
                if (session.PlayingHand || (session.Demo as ISpectatorControls)?.Spectating == true) return false;
                if (MatchRuntime.Current?.Frame.phase == RoundPhase.Results) return false;
                return self.ViewCamera != null;
            }
        }

        public void Begin()
        {
            if (Inspecting || !CanInspect) return;
            var session = Services.Session;
            session.SetMenu(false);
            suspended = self.InputSuspended; self.InputSuspended = true;
            vrAtStart = session.IsVR;
            var pose = GetComponentInChildren<IAvatarInspectionPose>();
            if (pose != null) { poseWas = pose.NeutralInspection; pose.NeutralInspection = true; }
            Inspecting = true;
        }

        public void End()
        {
            if (!Inspecting) return;
            Inspecting = false;
            var session = Services.Session;
            bool spectating = (session?.Demo as ISpectatorControls)?.Spectating == true;
            if (self != null && NetworkActors.Alive(self))
                self.InputSuspended = suspended || session?.MenuOpen == true || spectating;
            var pose = GetComponentInChildren<IAvatarInspectionPose>();
            if (pose != null) pose.NeutralInspection = poseWas;
        }

        void Update()
        {
            if (!Inspecting) return;
            var session = Services.Session;
            if (!CanInspect || session.MenuOpen || session.IsVR != vrAtStart || !NetworkActors.Alive(self))
            {
                End();
                return;
            }
            var runtime = MatchRuntime.Current;
            if (runtime?.Frame != null)
                foreach (var row in runtime.Frame.players)
                    if (row.id == runtime.LocalActorId && (!row.alive || row.dying || row.role == "spectator")) { End(); return; }
            self.InputSuspended = true;
        }

        void OnDisable() => End();
        void OnDestroy() => End();
        void OnApplicationPause(bool paused) { if (paused) End(); }
    }
}
