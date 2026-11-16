# Underneath the Tree: custom disaster set

Owner: underneath-the-tree. Branch: `codex/underneath-tree-sprint4`.
Target: `origin/Evan_Sprint_4`, through its designated integration owner.
Base dependency: `5c1ac9976c7ca1ddbf7cd8140ad178e68e8d0160` (Tree map and mode setup).
Gameplay candidate: `94f84b5f79caabd6cc87619b88f253c824a52837`. All-six-event checked baseline: `9850ab7140be7122d54b56c79510cbcc4e2ca684`, pushed and remote identity verified.
Status: all six events passed desktop runtime at the checked baseline; final focused Santa/cat/earthquake confirmation also passed at the gameplay candidate. Not integrated or included in an APK.

## Behavior

The catalog-backed profile matches `underneath-the-tree-v6-underpass`. Both developer disaster selection and scheduled mode selection use exactly PowerOutage, EvilSanta, CatAttack, OrnamentShower, PlanetKillerOrnament, and Earthquake. Four new prefab/type pairs are registered in the existing DisasterCatalog. The profile excludes those four from other maps; legacy map lists retain their existing disasters.

- Power outage: electrical room lights, lamp emission and tree-bulb emission switch off and restore; faint blue moon illumination remains for navigation.
- Evil Santa: one giant Santa reaches into the arena with two hands. One pulls real present bodies, the other slams a bounded region. Forces and damage run on the world authority.
- Cat attack: one giant tabby follows a single continuous path through the map bounds, pushing breakaway presents and damaging contacted survivors once per run. Its collision region covers body and head; it ends after leaving the course. It does not instantiate a tsunami or repeat a wave.
- Ornament shower: repeated polished baubles fall vertically toward current present positions, using the existing sphere-swept meteor impact path.
- Planet killer ornament: one oversized bauble falls and applies the existing large-meteor impact. It ends on impact.
- Earthquake: existing earthquake forces now build gradual fatigue in intact Tree gift shells. Ordinary small player bumps still do not release wrapping. Released pieces use the existing rigidbody/support-loss behavior.

GameSession/Session owns the map profile and selection filter. Modes keeps its existing scheduling, including team-mode force scaling and early-round exclusions. World/Hazards owns authoritative attacks and meteor trajectories; World/Maps owns gift fatigue. Presentation/Visuals renders sculpted creatures and baubles from existing DisasterFrame data, and controls room lighting. No Core contracts, network transport, packages, scenes or project settings changed. Creature models are a procedural art pass, not scanned/photoreal assets.

## Validation

Entry point: `DeadlyJenga.CastlePrototype.Editor.TreeDisasterChecks.Run`.
It runs the existing disaster catalog/map contract checks, then actual Game.unity with the approved map. Each event starts through `DemoDisasters.ExecuteForMode`; the world resets between events. Assertions cover active state, physical gift release, outage restoration, cat contact damage to a clearly staged TestDummy and single-run completion, JSON frame serialization, and clean End. Cameras are staged actual runtime captures, not input-driven gameplay or concept renders.

First candidate `4a3a4f44042fbb86d463f80d68b31f45913c5048` passed outage, Santa, cat and shower checks. The planet-impact assertion sampled only active frames and missed the terminal physics step; the revised fixture also reads actual body state after the hazard ends. Visual review prompted readable outage moonlight, front-facing Santa placement and a cat path/head-volume revision.

All six event checks and existing map contracts passed in checked job `9225a303ce7e4eb394528ac7955987c0`. See [retained assertions](UNDERNEATH_TREE_DISASTER_CHECKS.md).

Final focused job cfaba3e80d754f3e85bf0cffc32790ad passed Santa, cat and earthquake assertions at the gameplay candidate. Close-view review found heavy occlusion and rough procedural creature art; visual acceptance remains open.

Pending: human playthrough/visual acceptance, real Fusion peers, Quest performance/comfort, and integration/APK identity. Existing disaster snapshot plumbing is reused; a local JSON roundtrip is not peer proof.
