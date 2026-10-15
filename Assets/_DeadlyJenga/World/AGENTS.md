# World (level 1)

The place you play in. May use: Core only.

## Lanes
- Blocks: CastleBlock (+ prefab), Castle Friction material,
  CastleBlockAppearance (the block's visual slot), pull rules.
- Maps: CastleGenerator, CastleLayout, castle settings, island,
  ocean height / kill plane, sky objects.

## Contracts
- CastleBlock implements IPullable (weights, cooldown, last-in-layer lock).
- Blocks and maps implement ISyncState for networking.
- Raise GameEvents.BlockHit / BlockScrape on collisions.

## Sandbox
World/Sandbox.unity: a small castle, no players, a fake pull button.

## Rules
- Must play as gray boxes. Never require Presentation art to work.
  CastleBlockAppearance holds visuals, Presentation supplies them.
- Characters depend on CastleBlock. Only add to its public members;
  never rename or remove them without the task saying so.
- Ocean "touching kills you" lives here. Ocean visuals live in Presentation.

## Known fixes
- Port pull weights/cooldown/lock from legacy JengaBlock + PullRules into
  CastleBlock via IPullable, then retire TOWER/.
- Remove CastleBlock's use of Art types (appearance moves here).