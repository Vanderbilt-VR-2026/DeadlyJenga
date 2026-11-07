# Feed and leaderboard readability

The activity feed previously used 28px headshots and names that could shrink from 17px to 11px. Results used 32px portraits and 18px names in 36px rows. The held scoreboard was a single multiline text block without portraits.

The feed now uses 44px portraits and fixed 22px names on two lines. Held standings use six rows per page with 48px portraits, 28px names, a separate public-stat line and the existing hold/pin/page controls. Results use 48px portraits and 26px names, six entries per full page or five per Infection column. Each list retains local-player identification. Names truncate with a width-measured ellipsis instead of shrinking into unreadable text; stats retain separate space. Candy panels and current VR canvas distance/scale are preserved.

Portraits are cached at 96px rather than 72px; the existing two-new-portraits-per-frame and 256-entry limits remain. Name-width measurements are cached by source text, font and available width. The feed still suppresses all entries during Murder Mystery, and the live roster still displays only public reported-body information, never secret roles or hidden kill attribution.

## Source and validation

- Source branch: `codex/feed-leaderboard-readability-20261106`.
- Base: `8c03e7be22f89ff5bc4f5cf73317dabe818930b4`.
- Target: `Evan_Sprint_4` at `https://github.com/Vanderbilt-VR-2026/DeadlyJenga.git`, through the designated integration owner.
- Candidate: the commit containing this handoff. Obtain its full SHA with `git log -1 --format=%H -- Docs/Handoffs/FEED_LEADERBOARD_READABILITY_20261106.md`; its four runtime postimages identify the tested slice.
- Scope: Presentation/Visuals only; `CandyEliminationFeed.cs`, `CandyMatchHud.cs`, `CandyResultsHud.cs`, `CandyScorePortraits.cs`. Existing asset GUIDs/metas are unchanged. No Core, gameplay, SDK, settings, camera, equipment-model or icon changes.
- C# reference preflight: all 180 Presentation source files compile with zero errors against the existing Unity 6000.3.23f1 / Fusion 2.1.3 reference assemblies. This is an external compiler check, not a native editor import or runtime pass. Existing inherited warnings remain.
- Source-derived checks: paging covers each participant exactly once for populations 0–257, including empty and partial pages; row/card/header/footer/column geometry stays separate; fixed name fonts, bounded cached portrait generation and Murder Mystery privacy branches are preserved. These checks do not prove rendered glyph widths, runtime art or headset readability.
- Native Unity compile, actual Game solo, busy-feed/current-board/results captures and headset validation remain pending integration. No APK inclusion or final visual acceptance is claimed.

## Required combined captures

Use the existing single QA capture editor after integration. Capture a four-entry mixed kill/environmental feed, the held board with long names and 50 participants, its last partial page, a Murder Mystery roster with hidden roles and no kill-feed disclosure, full results, and Infection split results. Include representative close crops, a 1280×800 desktop frame and VR/simulator angular framing; identify simulated framing honestly. Use wide glyphs, markup-like names, local-player prefixes, missing-portrait initial fallbacks and an empty roster. Confirm every page is reachable and shrinking the roster clamps the current page. Attach the actual combined source SHA, map, mode and capture metadata.
