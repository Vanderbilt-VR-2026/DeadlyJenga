# Match modes

Start from Lobby. The host selects a mode and map at the setup terminal. In a match, the desktop Esc menu and the VR demo panel expose Modes, round settings, scenery, and the local tabletop preview. Competitive rule changes remain host-owned. Shared demo permissions do not grant authority over competitive modes.

## Round rules

`ModeSimulation` owns teams, scores, objectives and round phases. `MatchModeRuntime` supplies real actor positions, standing contacts and health, and applies outcomes through Core abilities. Presentation reads the published frame. No mode depends on a particular character, hand mesh or HUD.

| Mode | Main objective | Default island / spawn |
| --- | --- | --- |
| Survival | Remain alive through destruction | Sinking / high points |
| King of the Hill | Accumulate time in the current highest region | Walkable / rim |
| Block Control | Stand on blocks to claim them; ownership generates team points | Walkable / team poles |
| Capture the Flag | Steal the opposing lollipop and return it while your own flag is home | Walkable / team poles |
| Infection | Survive; eliminated players become infected hunters | Survivors high, infected low |
| Team Tickets | Exhaust opposing teams' respawn supply, then eliminate their remaining players | Walkable / team poles |
| Murder Mystery | Discover and stop the murderer, or eliminate the remaining innocents | Walkable / rim |
| Demo | Host-controlled sandbox, including optional health/death rules | Configurable |

Normal timed rounds default to five minutes, with a three-second countdown and five-second winner reveal. CTF, Infection and Team Tickets default to objective-driven endings; a time limit is configurable. CTF defaults to three captures with a two-capture winning margin. Teams support two through eight colors. The lobby can group human friends on one team or distribute them normally.

Block Control uses standing support, not arbitrary touching. The majority team's standing count drives capture speed; equal opposing counts cancel. Empty blocks decay after the configured interval. Enemy-owned climbing restrictions are optional; vaulting stays available. CTF carriers move and vault more slowly, cannot climb or punch, and have a frontal shield rather than immunity from every direction. Dropped flags require manual recovery.

The selected metric labels both the live top ten and final results. Only the leading ten receive live rank numbers; a local player outside that group gets a highlighted additional row. Final reports rank the full roster. Infection has separate survival and infected-kill standings.

## Destruction protocol

### Murder Mystery

Select Murder Mystery in the lobby. The personal role preference stays on the local device until it is sent privately to the game server. The server assigns one murderer and one sheriff; role overrides remain available in the demo mode panel. At least three total participants are filled for this mode. Ordinary punches deal eight damage; special punches are toggled with Q on desktop or through the mode panel. Only the special swing displays its red blade or blue hammer.

Innocents win if the murderer dies or the eight-minute timer expires. The murderer wins when no capable innocent remains. A sheriff is eliminated after two innocent kills. An innocent's lethal attack against a non-murderer eliminates the attacker and leaves the victim downed. Hold E, trigger or pinch near a downed player for the configured revive time. Downed players cannot jump, climb or vault and cannot survive landing on the island. Natural deaths use the shared death transition; murderer kills leave copies of the current avatar that settle, attach to their support and release if that support turns nearly upside down.

E or trigger/pinch reports a nearby body. A murderer must wait five seconds before reporting their own kill. Reporting a fallen sheriff also allows an innocent to recover the vacant sheriff role. During the first thirty seconds after reporting, look at a nearby colored footprint and interact to register one suspicion vote. At most three distinct suspected colors are displayed. Old reports fade but remain available. Sheriff names remain restricted to close inspection.

The sheriff receives three disaster candidates ten seconds before the first forty-five-second offer, vetoes one, and the murderer chooses one of the remaining two. Timeouts choose automatically. Later murderer offers are scheduled sixty to one hundred twenty seconds after the previous selection, with the sheriff's veto window included in that interval. Number keys 1–3 select candidates on desktop; headset users look at the candidate row and select.

Private role and choice records use targeted Fusion messages, separate from public match snapshots. Footprints are sent as bounded incremental batches, attached to moving block coordinates, merged into meshes and hidden beyond twenty-five meters. They are retained for the round with repeated cells coalesced and a one-hundred-thousand-record safety cap. Bot decisions use their own role and observed attacks, rather than reading other players' private roles. A network-server migration restarts this mode's round because secret role assignments were intentionally never distributed to potential replacement hosts.

Validation reports are in Logs/Validation/MysteryRules.txt and MysteryPlay.txt. The rule model and desktop gameplay checks cover assignment, attacks, penalties, revives and private/public separation. Separate-device Mystery play, Quest input ergonomics, corpse agreement under violent networked motion and large-population performance still need device testing.

Competitive rounds schedule catalog hazards after 90–120 seconds, announce them eight seconds ahead, and gradually approach a 60-second interval. At most two run concurrently by default. Early team play uses gentler choices; destructive fire, meteors and acid rain enter later. Persistent toggles, Planet Killer and Vampire are excluded from automatic selection. These choices and intervals are editable, not requirements for new modes.

## Bots and new modes

All eight shipped modes have validated JSON definitions in Resources/BotModes. The shared director allocates travel, guard, escort, contest, attack and evade assignments through Core, using bounded typed facts, capacity, utility and commitment. Characters executes ordinary movement, climb, vault and punch controls and reports progress. Actual mode state owns completion. Runtime decisions require no language model. See [BotObjectives/README.md](BotObjectives/README.md) for the full mode catalog, extension contract and acceptance tools. New movement mechanics still need a reachability/control adapter in Characters.

Maps declare recommended populations. Filling subtracts the connected human count and supplies ordinary survivor bots. A recommendation is a design starting point, not a measured Quest capacity. See Characters/Bots/CastleBots/TRAVERSAL_AND_OBJECTIVES.md for navigation and physical-hand details.

## Networking and validation

Fusion transports authoritative match frames, inputs and outcomes. Stable bot identities and restored human PlayerRefs keep the roster continuous through host migration. Reconciliation removes departed humans and releases their flags. Clients do not run the host's world disaster physics.

Editor validation reports are written under Logs/Validation, which is intentionally not committed. Pure rule tests, actual Game tests, physical map settling, natural bot outcomes, rendered shader checks, and independent Fusion runners cover different risks; none substitutes for Quest testing. Bare-hand gestures, stereo comfort, real passthrough placement, controller haptics, thermal performance and multiplayer headset play still require device acceptance. Do not infer arbitrary-map success or a 100-player headset budget from small fixtures or desktop CPU measurements.


## Equipment authority
MatchModeRuntime.Equipment owns the equipment ledger and world pickup records.
Core EquipmentContracts contains only enums, snapshot data and commands.
MatchFrame carries bounded inventory/pickup arrays through the existing compressed
match-state stream. Validate arrays before accepting a remote snapshot. Character
pose packets already carry the weapon hand poses; no parallel weapon pose stream
is needed.

An empty slot collects a nearby pickup. Occupied slots require an explicit swap.
The host validates the authenticated sender, living/playing state, distance, line
of sight and pickup availability before changing anything. A successful swap
places the previous item at the new pickup's original position, with its remaining
power duration. Dropped items have a one-second collection grace. Death removes
the slot and drops the item before the respawn/outcome transition. Stations on
the Powerup Test Island offer all seven items and recharge in fifteen seconds.
Menu spawning remains a host-only Demo command. Existing timed effects invoked
directly by other systems remain independent of pickup inventory.

Murder Mystery assigns role weapons into the same slot. Undrawn role-issued
equipment is omitted from public snapshots and hidden by character visuals.
Drawing a role weapon uses the existing Mystery special-attack rules; collecting
a sword or sheriff hammer from the test map grants a generic damage weapon, not
a secret role. Role changes update or remove the issued equipment. Migration
restores public inventory/pickups; a restarted Mystery clears the old ledger.

The rifle has seven shotgun pellets (14 m range, 13 damage each) or one long-rifle
shot (65 m, 55 damage). The glove does 80 damage with a 22 m/s horizontal impulse
and 6 m/s lift before the normal motor/geometry response. All attacks validate
ownership, draw state, cooldown, finite/reachable muzzle, cover and team rules.
These are initial tuning values, not headset balance certification.

Validation: AvatarEnemyChecks also runs EquipmentChecks (actual Game scene,
physical damage/cover fixtures, inventory/drop lifecycle and remote render
reconstruction). Reports and captures go to Logs/Validation. The Photon check
uses two runners inside one Editor; it does not certify two physical devices.
