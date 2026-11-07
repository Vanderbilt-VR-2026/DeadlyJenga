using System.Linq;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    internal interface IDevelopmentExperienceSource
    {
        bool DevelopmentExperience { get; }
    }

    // Request permission and server execution authority are separate checks.
    public static class SessionDevelopmentPolicy
    {
        static bool StableRoom
        {
            get
            {
                var room=IslandRoomConnection.Instance;
                return !room||!room.Busy&&!room.Migrating&&!room.RoomSceneBusy&&!room.Departing&&!room.SwitchingMap;
            }
        }
        public static bool DemoEnabled(CastlePlaySession session)
        {
            if (!session) return false;
            object room = IslandRoomConnection.Instance;
            if (room is IDevelopmentExperienceSource experience)
                return experience.DevelopmentExperience;
            // Missing experience ownership must not enable development controls.
            return false;
        }
        public static bool CanRequestLocal(CastlePlaySession session)
            => StableRoom && DemoEnabled(session) && (!session.Network || session.Network.CanEditWorld);
        public static bool CanExecuteLocal(CastlePlaySession session)
            => CanRequestLocal(session) && session.HasWorldAuthority;
        internal static bool CanExecuteServer(CastlePlaySession session)
            => StableRoom && DemoEnabled(session) && session.HasWorldAuthority;
        internal static bool CanExecutePeer(CastlePlaySession session, string sender)
        {
            var room = IslandRoomConnection.Instance;
            return CanExecuteServer(session) && room && room.Connected && room.IsHost &&
                !room.Busy && !room.Migrating && !room.RoomSceneBusy && !room.Departing &&
                !string.IsNullOrEmpty(sender) && room.Runner &&
                room.Runner.ActivePlayers.Any(player => player.ToString() == sender) && room.HasDemoPermission(sender);
        }
        internal static bool CanRematch(CastlePlaySession session, RoundPhase phase)
        {
            return StableRoom && session && session.HasWorldAuthority && phase == RoundPhase.Results;
        }
    }
}
