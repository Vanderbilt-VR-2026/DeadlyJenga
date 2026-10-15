# Core (level 0)

Shared definitions every box can use. Core uses nothing.

## Contents
- Layers: layer numbers for Survivor, Block, Surface.
- GameEvents: static events other boxes raise or listen to.
  BlockHit, BlockScrape, PlayerJumped, PlayerLanded, PlayerClimbed,
  PlayerVaulted, HandGrabbed, HandReleased, PlayerEliminated,
  RoundStarted, RoundEnded, BotEmote.
- IPullable: a block a hand can pull (CanPull, TryStartPull, PullToward, EndPull).
  Implemented by World. Used by Characters/Hand.
- ISessionInfo: read-only game state (is host, is menu open, local player,
  round time, survivors left, hands count). Implemented by GameSession.
  Used by Characters/Bots and Presentation.
- ISyncState: how a box hands its state to networking.
  Implemented by World and Characters. Used by GameSession.

## Rules
- Only interfaces, events, constants and plain data. No gameplay logic.
- Never reference any other box.
- Changes need approval from the whole team. Adding is OK, but renaming
  or removing anything breaks other boxes.
- Keep it small. If something only one box needs, it does not belong here.