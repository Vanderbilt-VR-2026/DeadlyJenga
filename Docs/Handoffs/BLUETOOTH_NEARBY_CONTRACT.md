# Bluetooth nearby rooms: proposed additive Core contract

Approved by Evan in this chat on 2026-11-15. No existing Core member changes. GameSession owns the implementation; Presentation only reads values and invokes commands. The provider is attached to the persistent room object; views locate the interface through scene objects, including Home, lobby and match. No Bluetooth or gameplay implementation belongs in Core.

Proposed file: Assets/_DeadlyJenga/Core/Session/NearbyRoomContracts.cs

```csharp
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
```

Host rooms advertise only an ephemeral ID and name. The room code is available only through an approved request bound to its live BLE connection. Pending requests expire; denial, disconnect, room change and disabling discovery invalidate them. This controls Bluetooth code sharing, not admission by somebody who already knows the normal room code. Names are player-chosen labels, not authenticated identities.

Names persist in application preferences across runs and in-place APK updates with the same package and signing key. Uninstall/data-clear, a different package, and cross-device synchronization are outside that persistence guarantee.

Headset gate: two physical Quests, different networks, no Meta discovery API/developer-account dependency at runtime; install/sideload setup remains subject to the headset OS. No headset was connected at task start.
