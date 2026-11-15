using System.Collections.Generic;
namespace DeadlyJenga.Core
{
    public readonly struct NearbyRoomInfo
    {
        public readonly string Id, HostName;
        public readonly int Players, Capacity;
        public NearbyRoomInfo(string id, string hostName, int players, int capacity)
        { Id=id; HostName=hostName; Players=players; Capacity=capacity; }
    }
    public readonly struct NearbyJoinRequest
    {
        public readonly string Id, PlayerName;
        public readonly float SecondsRemaining;
        public NearbyJoinRequest(string id, string playerName, float secondsRemaining)
        { Id=id; PlayerName=playerName; SecondsRemaining=secondsRemaining; }
    }
    public interface INearbyRoomControls
    {
        string PlayerName { get; set; }
        bool NearbyEnabled { get; set; }
        bool CanAdvertise { get; }
        string NearbyStatus { get; }
        string JoinRequestStatus { get; }
        IReadOnlyList<NearbyRoomInfo> NearbyRooms { get; }
        IReadOnlyList<NearbyJoinRequest> JoinRequests { get; }
        void RefreshNearby();
        void RequestJoin(string roomId);
        void CancelJoinRequest();
        void RespondToJoin(string requestId, bool accept);
    }
}
