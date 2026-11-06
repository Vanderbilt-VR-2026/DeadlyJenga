# Reliable networking hotpath checkpoint

Non-host players observed delayed remote movement while reliable world snapshots and pose receipts competed for transport progress. This checkpoint combines acknowledged pose receipts, lossless world deltas and per-peer LateUpdate message batching. It preserves the current reliable Fusion 2.1.3 path and every original inner payload. It is a reviewed source checkpoint with focused native evidence; smooth movement and combat acceptance remain pending.

Base: `72aa920683a520df0d32b7f5acdaaff53a70b545` on `Evan_Sprint_4`.
Original native-tested production payload SHA256: `bbf5d78914a2f78c16626a27b70d5318e1d03d8a86be6540dfe3f47c5a6782f9`.
Owner: Game Experience producer; MAIN integration writer. Steward prepared the exact source packet. No Core changes.

## Changed source

Exactly five production files in `Assets/_DeadlyJenga/GameSession/Session/Scripts/`:

- `CastleFusionSession.cs`: pose receipt piggybacking, ACK-anchored lossless world deltas and ordered per-peer batching during LateUpdate. Successful SDK return commits send accounting; context changes and terminal failures cancel reservations.
- `CastleFusionSession.Modes.cs`: preserve ordered private Murder Mystery trail messages and defer persistent progress until successful submission.
- `IslandLobbyCatalog.cs`: include world-delta and hotpath-bundle content markers so incompatible peers cannot silently mix protocols.
- `WorldDeltaCodec.cs` and its new `.meta`: bounded raw-byte lossless delta codec with exact base/target hashes and full-state fallback. Existing asset metadata is unchanged; the new GUID was checked against tracked Assets.

Physics tick, SDK, packages, project settings, rate/window experiments, scenes and Core are unchanged. Editor tracing, cadence/room fixtures, LAN-shadow/precision prototypes and Harbor assets are excluded.

## Validation

The packet was compiled from canonical Git sources plus only these production files: 77 Core and 114 GameSession sources. Direct Roslyn compilation against installed Unity/Photon references passed with Editor symbols enabled and excluded. All four compiler logs were empty. The new codec compiles without LAN-shadow or QA fixture sources. This is source compilation, not a Unity import, weaving pass or player build.

The review patch passed a read-only apply check against the preserved canonical working checkout. Canonical Git-blob, working raw/LF preimages and exact raw/LF postimages are retained in the packet. The five imported payloads matched the independent native candidate's source inventory. Integration then removed trailing spaces on one blank line in `CastleFusionSession.cs`; all C# token content is identical. Producer source handoff SHA256: `d5aa9cacae978dbd2e772ff0f2d4b34800dfdbc4e542168e522d0876113df807`. Native handoff SHA256: `543bce5dc7b34f865e9256725bf0fcce98c6238097770e25c264a57af5eae8e2`.

Producer managed checks include codec 97 assertions/90 cases, outbox 43 assertions/18 cases, and 41 extracted-driver assertions/7 cases with SDK/context stand-ins. Repeated execution on different runtimes is not additional gameplay coverage.

One independent two-editor, approximately ten-second passive native sample completed with null controller errors and verified cleanup. Within LateUpdate CAST traffic, HOST combined 270 logical records into 197 SDK submissions (27.04% fewer submissions); GUEST had 168 single-record submissions with no reduction. This excludes direct callbacks, controls and Social traffic, and is not a wire-packet or byte-savings claim. Bundle envelopes added bytes. Observed host-to-guest exact-serial delay bounds had median 147.7–227.7 ms versus prior restored measurements 252.2–330.1 ms; p95 upper bound 346.5 versus 412.5 ms. Pose submission was approximately 16.3 Hz versus 13.3 Hz. These are one nonrandomized same-host application observations with different load/target timing; they do not establish pure wire latency, rendered-motion quality or a proven delay below 150 ms.

## Pending and risks

The first role-swapped movement/combat attempt stopped before input because the operator focus acknowledgement timed out. Both peers cleaned up; no movement or combat pass resulted. A corrected genuine-focus test remains pending at this checkpoint. Controlled movement, accepted fire/damage and health replication, changed-arrival command rate guards, lifecycle/migration/missing-base recovery, large-message behavior and final combined-source regression still need the actual selected evidence or explicit disposition. No physical Quest, stereo/comfort or APK acceptance is established.

A large bundle can introduce head-of-line delay. The 4 MiB bound has managed/source evidence, not native maximum-message acceptance. SDK void return establishes submission only. Receiver partial-apply failure is terminal and stops remaining bundled records; it does not roll back already-applied records. Social can overtake queued CAST records; cross-channel ordering is not claimed.

MAIN applied the reviewed production files after rechecking the base and every preimage. The commit containing this document identifies the source checkpoint; remote publication and APK inclusion are separate facts. Existing metadata and unrelated local changes were preserved. This is a measured networking checkpoint, not final release acceptance. These public notes carry the essential evidence and limitations without requiring machine-local chats or Workboard access.
