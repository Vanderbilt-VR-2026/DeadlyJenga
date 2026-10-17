using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // Desktop menu section for multiplayer. Shows state and sends choices through IMultiplayerControls (Core);
    // the networking code in GameSession draws nothing itself.
    public static class MultiplayerPanel
    {
        public static void Draw(IMultiplayerControls net)
        {
            if (net == null) return;
            if (net.IsFusion)
            {
                GUILayout.Label(net.Status);
                if (GUILayout.Button(net.IsClient ? "Leave game" : "Close game for everyone", GUILayout.Height(32))) net.LeaveSession();
                return;
            }
            GUILayout.Space(8); GUILayout.Label("LOCAL MULTIPLAYER \u00b7 same Wi-Fi"); GUILayout.Label(net.Status);
            if (!net.IsClient)
            {
                if (GUILayout.Button("Nearby: " + net.SelectedName + " (next)", GUILayout.Height(30))) net.NextNearby();
                if (GUILayout.Button("Request to join selected demo", GUILayout.Height(30))) net.RequestSelected();
                if (GUILayout.Button("Invite selected demo here", GUILayout.Height(30))) net.InviteSelected();
                GUILayout.Label(net.PendingLabel);
                if (net.HasPending)
                {
                    if (GUILayout.Button("Accept", GUILayout.Height(30))) net.AcceptPending();
                    if (GUILayout.Button("Decline", GUILayout.Height(30))) net.DeclinePending();
                }
            }
            if (net.CanLeave && GUILayout.Button(net.IsClient ? "Leave local session" : "End / cancel local session", GUILayout.Height(30))) net.LeaveSession();
        }
    }
}
