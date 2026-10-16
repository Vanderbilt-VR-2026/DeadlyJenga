# Core (level 0)

The shared vocabulary every box uses to talk to every other box.
Core uses nothing. Namespace: DeadlyJenga.Core.

## Folders
- Abilities/  IPushable, IDamageable, IAffectable, ITeleportable, IClimbable,
              IGrabbable, Climbables (find the IClimbable behind a collider).
- Data/       EffectType and HazardType (ScriptableObject assets), Effect,
              IHazard, Layers.
- Services/   Services (Session, World), ISessionInfo, IWorldQuery.
- Events/     GameEvents.
- Testing/    TestDummy, FakeWorldQuery, FakeSession for Sandbox scenes.
- Editor/     ProjectPath (editor tools find assets by name).

## Rules
- Only interfaces, events, constants, plain data and test stand-ins. No gameplay.
- Never reference any other box.
- Only ADD. Never rename or change an existing member; other boxes depend on it.
- Remove by marking [System.Obsolete] first, delete once nothing uses it.
- Every change here is its own small PR (branch core/<name>) the whole team sees.
- Keep it small. If only one box needs something, it belongs in that box.

## Not built yet (add when the feature that needs it starts)
- Round info on ISessionInfo (round time, survivors left, hand count).
- A way for boxes to hand their state to networking (design with whoever owns Photon).
- IClaimable / points events for team modes.
