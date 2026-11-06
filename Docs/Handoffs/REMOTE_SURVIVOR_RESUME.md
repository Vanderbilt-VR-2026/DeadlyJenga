# Remote survivor suspension recovery

Remote survivor views could remain suspended permanently after a temporary stream interruption. `Suspend()` disabled their alive/interactive state, but later valid `ReadState` calls never cleared the flag. This also kept the host's hazard target and weapon presentation suspended. Remote hand views already resumed on valid updates.

This checkpoint adds only `suspended = false` beside the decoded active-state assignment in `Characters/Network/SurvivorNetworkView.cs`. It runs after the full wire decode, host-side NPC filtering and existing disappearance handling, before the optional visual-template early return and hazard refresh. No wire format, Core API, SDK, scene, input or damage policy changes.

Invalid or truncated data cannot clear suspension. A valid inactive state still reports not alive. Session-owned managed suspension is reapplied after state ingestion and during render; the change does not remove spectator/death holds or grant clients host impact authority.

Base: `02c0027ddd75656aa47750222eed13041e85f3d4`.
Canonical LF source before/after: `8ac74595944cf3e72f18ebaeb5538d4e99c7848a9fb6d88b44c8d885aa5beccf` / `98da69b66a96c92c27dcbd7dc147ade278665ed459dbb42cf1620f47c087dc04`.
DJenga 2 authored the one-line fix; MAIN integrates it. Existing Unity metadata is unchanged.

## Evidence and limits

- Current-source cached-reference compilation passed for Core, HandDemo and Characters plus the test fixture. This is distinct from Unity import/weaving.
- A native Unity lifecycle case used the actual avatar writer, character reader, remote view, rig and collider. All 18 checks passed with no errors and observer cleanup verified. Cases include temporary suspension/resume, early and late malformed data, inactive state, host forwarding, client authority limits, and explicitly reapplying suspension.
- The manual re-suspension case proves the view can remain held; it is not a full session-managed spectator test.
- A subsequent two-editor Hill case passed the original remote-target, health and team readiness guards and completed actual keyboard movement. Firing then stopped on an input/menu transition before any accepted shot. That fixture issue is being investigated separately.
- The earlier opaque target rejection did not log its individual factors, so suspension is not retrospectively asserted as its proven cause. No accepted-shot, damage/health replication, complete recovery/migration, Quest or APK acceptance is claimed here.

Producer handoff SHA256: `1baa8625d6b30d7bbb83cda8dddff89ee1459fbe031e6211af81867d207105eb`.
Native lifecycle report SHA256: `ac1792c3b643ce4486b098caff9a412961c4071dba2462d2949d5f4ffeb8efef`.
Paired Run04 handoff SHA256: `1e647f9abcac63985c7633bd811a7c823075cc1ee93c0f14b9e72e288bb30b87`.

The commit containing this note identifies the source checkpoint. Test-only fixtures and diagnostics are excluded from this change; remote publication and APK inclusion are separate facts.
