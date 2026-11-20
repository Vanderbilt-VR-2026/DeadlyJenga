# Trailer craft research 01: structure and format

Researcher topic: how professional game trailer editors structure and format trailers.
Compiled 2026-11-19 from about 50 sources, mostly working trailer editors' own writing:
Derek Lieu (about 35 blog posts), M. Joshua Cauller (4 articles), Kert Gartner (VR),
plus Valve/Steamworks, Meta Horizon Store, TikTok and YouTube/Google platform guidance.

Labels used below:
- **[Src: ...]** names the sources a claim comes from. Full entries are in section 5.
- **[Inference]** marks my own synthesis or extrapolation. No source states it directly.
- All wording is summarized. Short quoted phrases are marked with quotation marks.

Abbreviations for the most-cited sources (details in section 5):
- DL = Derek Lieu, blog at derek-lieu.com, cited by post short name, e.g. DL-NoLogo.
- MJC = M. Joshua Cauller (Game Developer articles).
- KG = Kert Gartner. STEAM = Steamworks trailer docs. META = Meta Horizon Store asset docs.

---

## 1. Rules

Each rule is imperative and testable. The test is in brackets. The "Why" line gives the reason and the source.

### A. Before cutting: decide the job

1. **Write the trailer's single goal and one-sentence pitch before choosing any shot.**
   [Test: one written line exists, e.g. "a giant hand pulls blocks while you climb; don't be the one who falls."]
   Why: the goal drives every later choice. A trailer with no clear goal reads as random clips. [Src: DL-Grade, DL-Skin Deep ("pick one takeaway and keep returning to it"), DL-TrailerBeforeGame]

2. **Name the hook: the one thing no other game has. Find it by listing the game's features and crossing out every one you have seen elsewhere.**
   [Test: the hook sentence would be false for any competitor.]
   Why: viewers are looking for what is unique. Generic features ("co-op", "physics") bore them. [Src: DL-FindingHook, DL-Prioritize, Road to VR]

3. **Pick the trailer type and its length before editing, and match the content to the label.**
   [Test: the type is written down. Runtime is within the band in the type table below.]
   Why: a label sets the viewer's expectation of depth and length. A mismatch frustrates them. [Src: DL-Length, DL-PreviewVsTrailer]

### B. The first seconds

4. **Show player-controlled gameplay within the first 2 to 3 seconds of any store-page or social cut, and within at most 5 seconds of a showcase trailer.**
   [Test: the timecode of the first player-input frame is 0:03 or earlier.]
   Why: Steam shoppers scrub the progress bar looking for gameplay. Discovery Queue viewers give "less than 10 seconds." TikTok's top click-through ads state their key message in the first 3 seconds. [Src: STEAM, Zukowski checklist (3 s) and 2023 guide (2 s), GameDiscoverCo/Lieu, TikTok Ads help center]

5. **Do not open on logos. Put studio and publisher logos after the cold open (a couple of seconds total) or on the end slate. The exception is a legally required card.**
   [Test: frame 0 is not a logo. Logo time before 0:15 is 2 s or less, and 1 s or less per logo.]
   Why: unfamiliar logos waste the most-watched seconds and can be mistaken for the title. 4 to 6 s of unknown logos frustrates viewers. [Src: DL-NoLogo, DL-Firewatch, MJC-WorstMistakes, Gameworldobserver/Lieu]

6. **Do not open on slow environment flythroughs. Use at most 2 or 3 establishing shots before visible player interaction, and keep them a few seconds total.**
   [Test: the count of non-interactive shots before the first player action is 3 or fewer.]
   Why: establishing shots "don't establish much." They only say the game is pretty. [Src: DL-EstablishingShots, DL-Firewatch, DL-Steam2023, DL-WhatToShow]

7. **Make the cold open a self-contained 5 to 10 second unit that raises a question or starts mid-action.**
   [Test: a viewer who stops at 0:10 can say what question was raised.]
   Why: the cold open's only job is to give a reason to keep watching. Pros use scene lift (in medias res), enigma, rug pull, flashback or a fast "thumbstopper" burst. [Src: DL-EngagingOpening, MJC-LaunchTrailers ("open in the middle of the action"), DL-SlowIntro]

8. **Answer "what genre is this, what perspective, and what do I control" within the first 10 to 15 seconds.**
   [Test: after 15 s a fresh viewer can name the genre and the controlled character.]
   Why: viewers filter by genre and leave fast if it is not theirs. The hook only lands once the genre is known. [Src: DL-AudienceQuestions, DL-HadesII, DL-EstablishingShots, Zukowski]

9. **Make the opening read with the sound off. The first trailer must communicate through visuals and on-screen text.**
   [Test: watch muted. The core action is still understood.]
   Why: Steam warns that customers may watch with no audio. Store-page trailers and feeds often autoplay muted. [Src: STEAM, DL-HadesII (cards go full screen, then move to the top of frame for muted store viewers), Murauski]

### C. Order of information (the body)

10. **Order the ideas as: genre, then hook, then why the hook matters, then challenge, then scope, then title and call to action.**
    [Test: the beat sheet lists these six in this order.]
    Why: this is the order in which viewers ask their questions. [Src: DL-AudienceQuestions, DL-Prioritize ("genre first, then the hook, then the content")]

11. **Teach the core verb first, then add one layer per shot or beat, in the order a player learns them (teach, test, escalate, test harder).**
    [Test: every new mechanic is preceded by the simpler one it builds on.]
    Why: trailers are partly instructional. Later action is unreadable without the basics. [Src: DL-WhatToShow, DL-Viewfinder (each of 11 shots adds one new idea), MJC-LaunchTrailers, MJC-Salvage (teach basics in about 20 s)]

12. **Hold a new mechanic long enough to be understood. Shorten shots of a mechanic only after it has been shown once.**
    [Test: the first appearance of each new mechanic is at least 2 to 3 s and lets the action complete. Later appearances may be under 1 s.]
    Why: cutting away before a mechanic reads "teases, not in a good way." Hold longer for new ideas, speed up once familiar. [Src: DL-Sheepherds, DL-HalfLifeAlyx, DL-MoreEditing; second numbers are [Inference] from DL-SimplestSteam's 3 to 5 s shots]

13. **Show the player's goal, not a list of tasks ("sell the dream, not the job").**
    [Test: a viewer can say what the players are trying to achieve, not only what buttons they press.]
    Why: goal-driven trailers are more compelling. Task lists make the game look like work. [Src: DL-ShowTheDream, DL-Skin Deep, DL-DearPassengers, MJC-WorstMistakes (#4)]

14. **Show failure and challenge, not only perfect play.**
    [Test: at least one shot shows something going wrong for the player.]
    Why: perfect play makes the game look trivial. Lows make highs land. Show "how does the game challenge me." [Src: DL-Sheepherds, DL-AudienceQuestions (#4), MJC-EmotionalJourney ("winning is intense, failing is easy to recover from")]

15. **Save cosmetics, extra modes and content counts for the second half (no earlier than the midpoint).**
    [Test: no cosmetic, customization or mode-list shot appears before 50% of runtime.]
    Why: leading with extras suggests the core is thin. "Don't sell the quantity when the thing itself is still unknown." [Src: DL-Sheepherds, DL-Prioritize, DL-SimplestSteam]

16. **Put the flashiest, most chaotic material last. Simpler, more readable moments go in the early acts.**
    [Test: the sorted list of shots by intensity rises overall from start to climax.]
    Why: escalation is the spine of the trailer. [Src: DL-CaptureBreakdown ("less complicated moments in the first acts"), DL-WhatToShow, DL-Skin Deep]

### D. Readability of each shot

17. **One main idea per shot. Remove whatever does not support it.**
    [Test: each shot in the edit decision list has a one-line "idea" note, and the note is true when watched.]
    Why: a shot "communicates better when it says less." Too much at once leaves only a vague sense of chaos. [Src: DL-SelectCapture, MJC-WorstMistakes (#3), DL-ChunkingConfusion]

18. **Make clear in every shot what the player is controlling and what they affect.**
    [Test: in each gameplay shot the player character or hand can be pointed to within 0.5 s.]
    Why: this is the core rule of the minimum viable Steam trailer. [Src: DL-SimplestSteam, DL-Prioritize (#4)]

19. **Capture "for the back seats": exaggerate actions, move across the whole frame, choose actions that visibly change the screen.**
    [Test: the action reads on a phone held at arm's length.]
    Why: subtle choices that matter to a player look like nothing to a viewer. "Treat it like theater." [Src: DL-Backseats, DL-HalfLifeAlyx ("playing to the camera is essential")]

20. **Direct the eye: one focal point per shot, and match the eye position across cuts.**
    [Test: on each cut, the new focal point is near where the old one was, or the cut is a deliberate jump.]
    Why: "it's very disorienting to not have your eye directed." [Src: DL-ChunkingConfusion, MJC-Salvage, DL-Firewatch (eyetrace and match cuts)]

21. **Choose a HUD policy per trailer type: HUD on for store-page and gameplay trailers, HUD reduced or off for cinematic or announce cuts. Never show unreadable small text as if it mattered.**
    [Test: the HUD state is noted per shot and matches the trailer type.]
    Why: the HUD tells shoppers the genre and shows what the game really looks like, but clutter hurts readability. (Sources disagree. See section 6.) [Src: STEAM, DL-SimplestSteam, Zukowski, DL-HadesII, vs DL-Templates, DL-SelectCapture, MJC-Salvage, KG]

22. **Capture without game music and keep clean sound effects. Build or re-cut a music track with an intro, build, climax and end.**
    [Test: the master has separate SFX and music tracks. The music has at least one audible build and one stop.]
    Why: looping in-game music gives no arc. Sound effects carry game feel that nothing else on the store page conveys. Viewers forgive weak video sooner than weak audio. [Src: DL-Grade, DL-Steam2023, DL-Prioritize, MJC-Salvage, Salters/frame.io]

### E. Escalation, variety and rhythm

23. **Make each beat raise at least one of: stakes, scale, number of players or threats, speed, or novelty.**
    [Test: for each beat, write what it raises over the previous beat. No beat is "same as before."]
    Why: repetition signals that there is nothing more to show. Repeated ideas need a twist (jump, then double jump, then triple jump with a fireball). [Src: DL-Variety, DL-Viewfinder, MJC-LaunchTrailers; the five-axis list is [Inference]]

24. **Show any single idea at most three times, then subvert or drop it.**
    [Test: count shots per idea. 3 or fewer.]
    Why: audiences catch patterns quickly. [Src: DL-PictureBooks, DL-SimplestSteam]

25. **Vary composition, energy, pace and shot length. Do not cut every shot to the same length, even on the beat.**
    [Test: the shot-length list has a spread (no more than 3 consecutive shots of equal length).]
    Why: predictable timing becomes monotonous. [Src: DL-Variety]

26. **Put at least one deliberate drop in energy (a stopdown, a breath, a dip to black or a silence) before the final climax.**
    [Test: the audio waveform shows a clear valley before the last peak.]
    Why: "lows don't look low unless there's a high," and the reverse. A music stopdown often comes before the final climax. [Src: DL-Variety, PC Gamer/Lieu, DL-HadesII, DL-DipToBlack]

27. **Cut at the pace the game needs, not at "real trailer" speed. Default to straight cuts and avoid crossfades as a habit.**
    [Test: no crossfade without a stated reason. Average shot length suits the game (slapstick physics needs time for the gag to finish).]
    Why: "MORE editing does not mean better editing." Flashy editing can make a game look broken or compensating. [Src: DL-MoreEditing, DL-Firewatch, DL-DipToBlack]

### F. Title, end card and button

28. **Land the title after the climax, not at the start. In 60 to 90 s cuts it usually sits in the last 10 to 15%.**
    [Test: the title card's timecode is after the highest-intensity beat.]
    Why: the title pays off the build. Pros place it after the end montage or the climax's closing question. [Src: DL-Mandrake, DL-Skin Deep, DL-Viewfinder (stinger over the title), DL-AudienceQuestions (#6); the percentage is [Inference]]

29. **Put exactly one call to action on the end slate (e.g. "Wishlist on Steam" or "Available now on Meta Quest"), with the logo largest, then the CTA, then legal text.**
    [Test: one verb-led CTA on screen. Logo is the largest element.]
    Why: one simple CTA works. Several dilute it. [Src: DL-Firewatch, DL-AudienceQuestions, MJC-Salvage, TikTok (an end card with download info had 47.3% more impressions in gaming)]

30. **Add a button: 1 to 3 seconds of one last joke, action beat or tease after the title.**
    [Test: the last 3 s before the final slate (or after it) contain a gameplay gag or sting.]
    Why: it leaves a final playful or intriguing impression. It is the classic last beat after the climax. [Src: DL-Skin Deep (gag after the title as a tag), GMTK via search summary; length is [Inference]]

31. **Use title cards only when they say something only this game could say. Never use generic feature bullets ("physics-based!", "online multiplayer!").**
    [Test: swap the game name. If the card still fits another game, rewrite or cut it.]
    Why: generic cards make the trailer generic. Cards should state the fantasy or benefit, then the footage proves it ("See 'N Say"). [Src: DL-TitleCards, DL-RememberTitles, DL-Firewatch, DL-Sheepherds, MJC-Salvage]

32. **Keep text inside title-safe margins, high contrast, never over busy action, and never where a player progress bar sits. Test every card on someone outside the team.**
    [Test: the card survives a muted phone viewing by a non-team viewer, who can repeat what it meant.]
    Why: unreadable or confusing cards cost more than they add. [Src: DL-Firewatch, DL-Grade, DL-Sheepherds]

### G. Platform and format

33. **Make the first trailer on the store page primarily gameplay. Its first 6 seconds feed Steam's auto microtrailer.**
    [Test: frames 0 to 6 s of the first store trailer are all gameplay.]
    Why: Steam builds 6-second looping microtrailers from six 1-second clips of the first trailer, and they cannot be customized. Only 2 trailers show before screenshots. [Src: STEAM, DL-Steam2023, Zukowski]

34. **For Meta Quest store trailers: 16:9 filling the frame (no letterbox or pillarbox), 2 minutes or less (Meta suggests 1 to 2 min, roughly 30 s minimum), captured on Quest hardware, with a "Captured on Meta Quest 3" style label when captured on Quest 3, and no other platforms' headsets or controllers.**
    [Test: technical checklist passes.]
    Why: these are store requirements and rejection reasons. [Src: META VRC.Quest.Asset.7, META asset best practices]

35. **Cut each social concept natively at 60, 30 and 15 s, each in 16:9, 9:16 and 1:1. Re-structure each length. Do not just trim the long version.**
    [Test: the 15 s version has its own hook in its first second and its own end card.]
    Why: this is the standard ad deliverable set (one concept gives 9 deliverables). Short feeds reward a native vertical hook. [Src: Salters/frame.io, TikTok Ads, YouTube ABCD; the "re-structure" part is supported by marketing sources and is partly [Inference]]

### H. VR-specific

36. **Make the VR format obvious in the first shot: tracked hands doing a physical action.**
    [Test: a non-VR viewer can tell it is VR from the first shot.]
    Why: the I Expect You to Die 3 cold open was built to be "easily identifiable as VR." [Src: DL-IEYTD3, DL-HalfLifeAlyx]

37. **Never use raw head-locked footage. Capture first-person with a smoothed spectator camera at 60 fps, and mix in third-person or avatar shots.**
    [Test: no shot has visible head jitter. Capture is 60 fps.]
    Why: raw first-person VR footage is shaky and nauseating on a flat screen. Third person shows scale and how the player moves. [Src: KG-VirtualCinematography, DL-CosmicTrip, Road to VR, META MR trailer guide, Unity spectator blog]

38. **Perform VR actions slowly and deliberately: hold key hand actions an extra 1 to 2 s, keep hands near frame center (higher than feels natural), and exaggerate head turns.**
    [Test: each hand action is readable at normal speed. Nothing important happens at the frame edge.]
    Why: hands move faster than viewers can read, and people move their eyes more than their heads. [Src: DL-HalfLifeAlyx, DL-CosmicTrip, DL-ArizonaSunshine2 (capture at half speed, hands too)]

39. **Brighten dark scenes for flat screens and phones, even past the headset look.**
    [Test: the darkest shot is readable on a phone at 50% brightness.]
    Why: flat viewers are not in a headset. [Src: Road to VR]

### I. Physics, chaos and party games

40. **Make chaos readable: show a clear goal, then the chain reaction, then the consequence. Keep busy, unfocused compositions brief.**
    [Test: in each chaos shot a viewer can say what caused what.]
    Why: unclear chaos is usually a weakness. It becomes an asset only when the goal is clear and the chain of systems reads. [Src: DL-DearPassengers, DL-Skin Deep (banana peel to window to space chain), DL-ChunkingConfusion, Salters ("soupiness")]

41. **Use humor that is captured from real play, not staged skits. Let physics gags finish before you cut.**
    [Test: no scripted fake dialogue. Each gag shot includes its payoff frame.]
    Why: manufactured humor feels forced. Player-driven physics humor (Octodad, Goose Game) sells itself. [Src: DL-DearPassengers, DL-FunnyTrailer, MJC-EmotionalJourney]

---

## Trailer types and their jobs

| Type | Job | Typical length | Must achieve | Differs by |
|---|---|---|---|---|
| Teaser | Create curiosity and mood | 10 to 60 s (sources vary) | One striking image or question, the name, maybe a date | Can withhold gameplay. Often stops at "the problem" in the story template. Goes to the back of a Steam page unless it has gameplay. [DL-Templates, DL-Steam2023, Murauski] |
| Announce / reveal | First impression: hook plus genre | About 60 to 90 s, often fixed by the showcase slot | Strong hook (art, unique mechanic or humor), "get in and get out" | Cold open, then intro, escalation, climax, then title last. [DL-Mandrake, DL-Livestream, DL-Viewfinder] |
| Gameplay | Show how it plays and add depth | 60 to 120 s | The core loop, mechanics in learning order, variety | More holds, HUD on, layers added on top of the reveal. [DL-Viewfinder, DL-WhatToShow] |
| Launch / release | Remove hesitation and drive the purchase | 60 to 120 s | A faster recap of the whole game, scope, "available now," quotes | Audience already knows the game: faster pace, accolades mid or late, FOMO. [MJC-LaunchTrailers, DL-Viewfinder, DL-ReviewQuotes] |
| Store page (first Steam/Quest slot) | Let a scrubbing shopper judge genre, quality and fun | 30 s to 2 min (Quest max 2 min). Lieu's minimum viable trailer is 90 to 180 s | Gameplay from second 0, HUD on, shots of 3 to 5 s so scrubbers do not miss them, readable muted | No drama needed. "A glorified animated GIF." [DL-SimplestSteam, STEAM, META] |
| Gameplay preview / deep dive | Teach the systems to committed viewers and press | 5 to 15 min | Narrated walkthrough | Not a trailer. Only for sandbox or systemic games. [DL-PreviewVsTrailer, DL-Length] |
| Social 60 / 30 / 15 s | Stop the scroll and send people to the page | 60, 30, 15 s (TikTok says any length can work; 9 to 15 s is common for in-feed) | Hook in 1 to 3 s, key message by 3 s, native 9:16, end card with CTA | Re-cut per length. Brand early raises recall but also skips (YouTube ABCD trade-off). [TikTok, YouTube ABCD, Salters] |

Lieu's default length for a game trailer is about 90 s, because audiences are used to it. Length matters less than clarity unless the cut runs past about 90 s. [DL-Length, DL-Prioritize]

Structural models pros name (pick one per trailer):
- **Hero's journey / rising action**: big cold open, slow intro of premise or mechanic, rising complications, climax, title, button. This is the default for announce and launch cuts. [DL-Pacing, DL-Variety, GMTK via search]
- **Slow burn**: starts low and builds steadily. Suits teasers. [DL-Pacing]
- **Assault**: high intensity throughout. Suits relentless action. Tiring if long. [DL-Pacing]
- **Bullet point (tell, show, repeat)**: card states a point, gameplay proves it. Risks predictability, so break the pattern. [DL-Templates, DL-HadesII]
- **Music video montage**: cut to beats, simplest to most complex. Suits games readable at a glance (physics, action). [DL-Templates]
- **Chronological**: follows the game's own teach, twist, test order. [DL-Templates]
- **Four-part arc**: cold open, intro, escalation, climax (then title). [DL-Mandrake]

---

## 2. Templates

These second-by-second sheets are **[Inference]**. I built them from the sourced rules above: gameplay by 0:02 to 0:03 (Rule 4), a 5 to 10 s cold open (7), genre by 10 to 15 s (8), learning order (11), a stopdown before the climax (26), title after the climax (28), a button (30) and one CTA (29). No source publishes exact second-by-second sheets. Lieu says only about 90 s for games and 5 to 10 s cold opens.

### 15 s (social, vertical-first)

| Time | Beat | Fill in | Rule refs |
|---|---|---|---|
| 0.0 to 1.5 | HOOK: the single most surprising gameplay image, in motion from frame 0. No logo, no fade-in. | `[hook shot]` | 4, 5, 7 |
| 1.5 to 4.0 | CONTEXT: one shot that shows what the player controls and their goal. Optional 3 to 5 word card at the top of frame. | `[core verb shot]` + `[card: goal in 5 words]` | 8, 9, 13, 18 |
| 4.0 to 9.0 | ESCALATE: 2 or 3 shots, each raising one thing (scale, threat or number). | `[shot +1]` `[shot +2]` `[shot +3]` | 11, 23 |
| 9.0 to 11.5 | PEAK: the payoff or failure moment, held until it completes. | `[payoff/fail]` | 14, 16 |
| 11.5 to 13.5 | TITLE + CTA: logo, one CTA, platform. Music hit on the title. | `[title]` `[CTA]` | 28, 29 |
| 13.5 to 15.0 | BUTTON: a 1 to 1.5 s gag or sting, under or after the slate. | `[button]` | 30 |

### 30 s

| Time | Beat | Fill in | Rule refs |
|---|---|---|---|
| 0 to 3 | COLD OPEN: mid-action hook, player-controlled. | `[hook]` | 4, 7 |
| 3 to 7 | GENRE/CONTROL: what you are and what you do. | `[core verb]` | 8, 11 |
| 7 to 12 | HOOK EXPLAINED: why it is different, with one card if needed. | `[hook detail]` `[card]` | 10, 31 |
| 12 to 20 | ESCALATION: 3 or 4 shots, each with a raise. One failure shot. | `[+1]` `[+2]` `[fail]` `[+3]` | 14, 23 |
| 20 to 21 | STOPDOWN: a half-second to 1 s breath (silence or a near-miss). | `[breath]` | 26 |
| 21 to 25 | CLIMAX: the biggest chaos, readable. | `[climax]` | 16, 40 |
| 25 to 28.5 | TITLE + CTA | `[title]` `[CTA]` | 28, 29 |
| 28.5 to 30 | BUTTON | `[button]` | 30 |

### 60 s (announce or store)

| Time | Beat | Fill in | Rule refs |
|---|---|---|---|
| 0 to 6 | COLD OPEN: self-contained scene that raises a question. Gameplay by 0:02. | `[cold open]` | 4, 7 |
| 6 to 8 | (Optional) studio logo, 1 to 2 s. Omit for unknown studios. | `[logo]` | 5 |
| 8 to 16 | INTRO / TEACH: core verb in its simplest form, held long enough to read. Genre and perspective clear. | `[verb 1]` `[verb 1 in use]` | 8, 11, 12 |
| 16 to 26 | HOOK + WHY IT MATTERS: the unique twist and its consequence. | `[hook]` `[consequence]` | 10, 13 |
| 26 to 38 | ESCALATION / CHALLENGE: new threats or layers, one per beat. Failures shown. Contrasting color burst mid-trailer. | `[layer 2]` `[layer 3]` `[fail]` `[layer 4]` | 14, 23, 25 |
| 38 to 42 | SCOPE: variety montage (maps, modes, extras), after the midpoint. | `[variety x3]` | 15 |
| 42 to 44 | STOPDOWN / BREATH: music drops, a tense hold. | `[breath]` | 26 |
| 44 to 52 | CLIMAX: biggest set piece, most chaos, still readable. | `[climax]` | 16, 40 |
| 52 to 57 | TITLE + CTA + platform (+ "Captured on Meta Quest 3" if needed). | `[title]` `[CTA]` | 28, 29, 34 |
| 57 to 60 | BUTTON | `[button]` | 30 |

For 90 s, stretch the intro, hook and escalation (the 8 to 38 s range) by about 50%. Keep the cold open at about 10 s or less and the title and button at about 8 s. [Inference]

---

## 3. Mistakes

| Symptom | Cause | Fix | Source |
|---|---|---|---|
| Flat "list of events" with no shape (the first AI attempt) | No goal or structure. Clips ordered as captured. No highs and lows. | Write the goal and hook. Pick a structural model. Sort shots by intensity and learning order. Add a stopdown and a climax. | DL-Grade (C-grade direction: "no discernible structure"), MJC-WorstMistakes #5, DL-Pacing |
| Viewers skip ahead or leave in the first seconds | Logos, studio cards, slow flythroughs or story setup before gameplay | Cold open on gameplay by 0:02 to 0:03. Logos to the end. 2 or 3 establishing shots at most. | DL-NoLogo, DL-Firewatch (36 s to first control is "egregious"), DL-EstablishingShots, Gameworldobserver |
| "I don't get what I'd do" | Mechanics not taught. Shots cut before the action completes. Too many ideas per shot. | Teach the core verb first and hold it. One idea per shot. Show the goal. | MJC-WorstMistakes #1, DL-Sheepherds, DL-SelectCapture |
| Looks like every other game | Generic features shown, hook missing or buried | List features, cross out the familiar ones, lead with what is left | DL-FindingHook, MJC-WorstMistakes #6 |
| Feels repetitive or small | Same action or biome repeated. One color palette. | 3 repeats at most per idea, each with a twist. Vary biomes and palettes. | DL-Variety, DL-WhatToShow, DL-PictureBooks |
| Game looks trivial | Only perfect play shown | Include failures and near-misses | DL-Sheepherds, DL-AudienceQuestions |
| Chaos reads as noise ("soupiness") | Overlapping simultaneous actions, no focal point | One focal point. Show cause, then effect. Keep busy shots brief. Zoom in. Reduce the number of objects. | Salters, DL-ChunkingConfusion, DL-DearPassengers |
| Trailer feels lifeless | Looping in-game music. No SFX, or SFX buried or cut off. | Arc-shaped music with stopdowns. Capture without music. SFX as a percussion layer. | DL-Grade, MJC-Salvage, DL-Variety |
| Over-edited, "trying to look AAA" | Flutter cuts, crossfades, fast cutting for its own sake | Cut at the game's pace. Straight cuts by default. | DL-MoreEditing, DL-DipToBlack |
| Text nobody reads, or text that hurts | Generic feature cards. Small text over busy gameplay. Vague copy ("play at your own pace"). | Cards only for game-specific claims. Big, title-safe, simple background. Test on an outsider. | DL-TitleCards, DL-Firewatch, DL-Sheepherds, Salters |
| Extras undermine the core | Cosmetics or modes before the midpoint | Move them after 50% | DL-Sheepherds |
| Ends limp or confusing | No climax, several CTAs, title buried | Climax, then title, then one CTA, then button | DL-Firewatch, DL-AudienceQuestions, DL-Skin Deep |
| VR footage is nauseating or unreadable | Raw head-locked capture, fast hands, dark grade | Smoothed spectator camera at 60 fps, third person or avatar shots, slow deliberate hands, brighten | KG, DL-CosmicTrip, DL-HalfLifeAlyx, Road to VR |
| Capture looks broken | Stutter, motion blur, low frame rate, keyboard-jerky camera | Recapture on a stable build. Gamepad or smoothed camera. Debug tools for retakes. | DL-DearPassengers, DL-DebugTools (post title), DL-Grade |
| Humor falls flat | Staged or manufactured jokes, too-clean voice chat | Use real play moments. Let gags finish. Keep the genre's rough authenticity. | DL-DearPassengers, DL-FunnyTrailer |

---

## 4. Applied to Deadly Jenga

All three are **[Inference]**: my application of the rules above to the game's elements (giant flying hand, candy block tower over the ocean, survivors climbing, disasters, falls). Shot names are suggestions for the capture list. Each assumes music with a hit at the title and SFX captured separately (Rule 22). Each should be cut 9:16 first and also exported 16:9 (Rule 35). Each needs a "Captured on Meta Quest 3" label if the footage is from Quest 3 and the cut goes to the Quest store (Rule 34).

### 15 s A: "The Pull" (core loop, hand vs survivors)

Goal: "A giant hand pulls blocks while you climb. Don't be the one who falls." Hook = the hand pulling a block from under a living player.

| Time | Shot | Notes |
|---|---|---|
| 0.0 to 1.5 | Close third-person: the giant candy hand's fingers grip a block mid-tower. Behind it a survivor is clinging. The block starts sliding at frame 0. | Hook in motion from frame 0. No logo (Rules 4, 5, 7). |
| 1.5 to 4.0 | Survivor first-person (smoothed): tracked VR hands grab the next block up and climb. Card at top: "CLIMB." | Shows VR and control (Rules 8, 18, 36, 38). |
| 4.0 to 6.0 | Wide: the whole tower over the ocean. The block comes out. The tower sways. Card: "THEY PULL." | Scale raise. Teaches both sides (Rule 11). |
| 6.0 to 9.0 | Two quick shots: (a) blocks above the gap shift and a survivor slips; (b) another survivor leaps to a neighbor block. | Escalation plus failure (Rules 14, 23). |
| 9.0 to 11.5 | The top section topples. Two survivors fall into the ocean with a splash. Hold until the splash completes. | Payoff held (Rules 12, 16). |
| 11.5 to 13.5 | Title DEADLY JENGA over the tower stump. One CTA, e.g. "Wishlist now" or "On Meta Quest." | Rules 28, 29. |
| 13.5 to 15.0 | Button: one survivor still hanging by one hand from the last block. The hand's finger flicks the block. | Comic sting (Rule 30). |

### 15 s B: "Then the Weather Turns" (disaster escalation)

Hook = a tsunami wave rising toward a candy tower full of climbers.

| Time | Shot | Notes |
|---|---|---|
| 0.0 to 1.5 | Low ocean-level camera: a huge wave rises behind the tower with survivors on it. | Enigma or threat open (Rule 7). |
| 1.5 to 4.0 | Survivor POV: hands climbing fast, looking back over the shoulder at the wave (exaggerated head turn). | VR read plus goal "get high" (Rules 13, 36, 38). |
| 4.0 to 6.5 | The wave hits the base. Lower blocks scatter. A climber is knocked off. | First raise: consequence (Rule 23). |
| 6.5 to 8.5 | Hard cut to a tornado spinning loose blocks around the tower. Card: "EVERY ROUND, WORSE." | Second raise: a new disaster. Color contrast (Rules 23, 25). |
| 8.5 to 9.0 | Stopdown: half a second of near-silence, one survivor alone at the top. | Breath before the peak (Rule 26). |
| 9.0 to 12.0 | The giant hand pulls a block from the weakened tower while the tornado hits. Total collapse. Survivors fall. | Climax: everything at once, but one focal point (the hand's block) (Rule 40). |
| 12.0 to 14.0 | Title plus CTA over a calm ocean with floating candy blocks. | Rules 28, 29. |
| 14.0 to 15.0 | Button: one survivor surfaces, spits water, looks up. A single block drops on them. | Rule 30. |

### 15 s C: "Be the Hand" (two-role hook, VR-forward)

Hook = in VR you ARE the giant hand. Lead with the player's real tracked hand becoming the giant hand.

| Time | Shot | Notes |
|---|---|---|
| 0.0 to 2.0 | Hand-player POV (smoothed): the player's tracked hand flexes, and the giant candy hand mirrors it over the tower. Card: "YOU ARE THE HAND." | Makes VR obvious and states the hook (Rules 2, 36). |
| 2.0 to 4.5 | Over-the-hand third person: the hand flies in, picks a block, and pulls slowly. The block held for readability. Survivors visible as small figures. | Teach the verb. Hold it (Rules 11, 12, 38). |
| 4.5 to 6.5 | Reverse angle, survivor POV: the giant hand fills the sky. The survivor scrambles. Card: "OR RUN FROM IT." | Second role. Contrast in scale (Rule 23). |
| 6.5 to 10.0 | Three escalating pulls (1 s each): one survivor wobbles; two fall; a key block brings the top floor down. | Same idea 3 times, each bigger (Rule 24). |
| 10.0 to 11.5 | Hold: the hand turns its "palm" toward the camera as the survivors splash below. | Climax and character beat (Rule 41). |
| 11.5 to 13.5 | Title plus CTA. | Rules 28, 29. |
| 13.5 to 15.0 | Button: a survivor on the hand's knuckle, riding it, waving. | Captured from real play if possible (Rule 41). |

Capture notes for all three [Inference from Rules 19, 37 to 40]:
- Spectator cameras: smoothed survivor POV, smoothed hand POV, a free third-person orbit, and a low ocean-level camera.
- Speed: perform VR hand actions slowly. Capture at half speed where the engine allows and speed up in the edit, moving hands at that slower pace too (DL-ArizonaSunshine2).
- Look: brighten for phones. Keep the HUD off for the social cuts. Use a HUD-on variant for the first store trailer (Rule 21).
- Coverage: capture about ten times more footage than you use (MJC-Salvage).

---

## 5. Sources

Derek Lieu (game trailer editor: Half-Life: Alyx, Firewatch, Subnautica, Spelunky 2, Among Us). Blog at https://www.derek-lieu.com/blog

| Short name | Title | URL | Taken from it |
|---|---|---|---|
| DL-Sheepherds | Game Trailer Makeover - Sheepherds (2026) | https://www.derek-lieu.com/blog/2026/8/4/game-trailer-makeover-sheepherds | Core action in the first 10 s; hold mechanics; cosmetics no sooner than halfway; show failure; test cards with outsiders |
| DL-DearPassengers | Dear Passengers vs Banana Airways (2026) | https://www.derek-lieu.com/blog/2026/8/23/dear-passengers-vs-banana-airways | Party ("friendslop") trailers: goal-oriented, captured vs staged humor, chaos as an asset, capture quality |
| DL-Mandrake | Mandrake - Announce Trailer (2026) | https://www.derek-lieu.com/blog/2026/4/3/mandrake-announce-trailer | Four-part arc; most unexpected element first; title last; 90 s slot budget |
| DL-Skin Deep | Skin Deep Release Trailer (2025) | https://www.derek-lieu.com/blog/2025/7/10/skin-deep-release-trailer | HUD on in the opener for Steam; show the goal; systemic chain reactions; title after the end montage, gag after it |
| DL-HadesII | Hades II Early Access Trailer Review (2024) | https://www.derek-lieu.com/blog/2024/5/19/hades-ii-early-access-trailer-review | Genre before features; cards full screen then top of frame for muted viewers; energy valleys |
| DL-ArizonaSunshine2 | Arizona Sunshine 2 Launch Trailer (2024) | https://www.derek-lieu.com/blog/2024/4/21/arizona-sunshine-2-launch-trailer | VR capture at half speed; hands and head pacing; framing; a 14 s opening oner from about 60 takes; ESRB first-4-seconds rule |
| DL-Frostpunk2 | Frostpunk 2 Gameplay Trailer Review (2024) | https://www.derek-lieu.com/blog/2024/1/20/frostpunk-2-gameplay-trailer-review | Voiceover-led teaching; dense UI as a second read; an example of a slow, gravitas opening |
| DL-IEYTD3 | I Expect You to Die 3 Campaign (2023) | https://www.derek-lieu.com/blog/2023/10/9/i-expect-you-to-die-3-campaign | VR cold open readable as VR; hands as character acting; campaign layering |
| DL-CaptureBreakdown | How to Break Down Gameplay Capture Footage (2023) | https://www.derek-lieu.com/blog/2023/11/6/how-to-break-down-gameplay-capture-footage | Sort selects by act; simpler early, flashier later |
| DL-PictureBooks | What Picture Books Can Teach You About Trailer Editing (2023) | https://www.derek-lieu.com/blog/2023/12/3/what-picture-books-can-teach-you-about-trailer-editing | Repeat an idea about three times, then subvert |
| DL-TrailerBeforeGame | Making a Trailer Before the Game (2023) | https://www.derek-lieu.com/blog/2023/9/23/making-a-trailer-before-the-game | Something must change the formula; player active in every shot |
| DL-Viewfinder | Viewfinder Trailer Campaign (2023) | https://www.derek-lieu.com/blog/2023/7/29/viewfinder-trailer-campaign | Reveal vs gameplay vs launch jobs; one new idea per shot; stinger over the title |
| DL-WhatToShow | How to Decide What to Show in a Game Trailer (and in what order) (2023) | https://www.derek-lieu.com/blog/2023/7/16/how-to-decide-what-to-show-in-a-game-trailer-and-in-what-order-to-put-it | Verbs in learning order; flashy items last; no environment-montage opens; color variety |
| DL-Grade | How to Grade the Quality of a Game Trailer (2023) | https://www.derek-lieu.com/blog/2023/7/2/how-to-grade-the-quality-of-a-game-trailer | C/B/A/S rubric for 8 components; music arc; capture without music; sound priority |
| DL-Steam2023 | How to Optimize Your Steam Trailers in 2023 | https://www.derek-lieu.com/blog/2023/5/8/how-to-optimize-your-steam-trailers-in-2023 | Gameplay trailer first; 2 trailers before screenshots; teaser placement; SFX convey game feel |
| DL-MoreEditing | More Trailer Editing is not Better Trailer Editing (2023) | https://www.derek-lieu.com/blog/2023/4/23/more-trailer-editing-is-not-better-trailer-editing | Average shot length varies 0.82 to 4.25 s; cut at the game's pace |
| DL-Firewatch | I Made the Worst Firewatch Trailer Ever (2023) | https://derek-lieu-yrd2.squarespace.com/blog/2023/3/11/i-made-the-worst-firewatch-trailer-ever | Numeric bad-practice thresholds (logos 1 s or less each, ESRB 2 s or less, 36 s to control is egregious, quotes 2 lines or less); single CTA; title-safe |
| DL-Prioritize | What to Prioritize to Make a Stand Out Game Trailer (2021) | https://www.derek-lieu.com/blog/2021/12/6/what-to-prioritize-to-make-a-stand-out-game-trailer | Priority order; genre, then hook, then content; length matters only past about 90 s |
| DL-AudienceQuestions | Trailer Structure Based on Audience Questions (2021) | https://www.derek-lieu.com/blog/2021/8/23/trailer-structure-based-on-audience-questions | Six-question structure through to the CTA |
| DL-OwnHook | Trailers With Their Own Hook (2021) | https://www.derek-lieu.com/blog/2021/8/30/trailers-with-their-own-hook | The trailer concept must not overshadow the game |
| DL-Livestream | Making Trailers for Livestream Events (2020) | https://www.derek-lieu.com/blog/2020/5/18/making-trailers-for-livestream-events | Announcements: "get in and get out" |
| DL-FunnyTrailer | The Risks and Rewards of Making a Funny Game Trailer (2020) | https://www.derek-lieu.com/blog/2020/5/4/the-risks-and-rewards-of-making-a-funny-game-trailer | Player-driven vs manufactured humor; risks |
| DL-TitleCards | How to Write Trailer Title Cards With Strong Messaging (2020) | https://www.derek-lieu.com/blog/2020/4/19/how-to-write-trailer-title-cards-with-strong-messaging | Cards that can only describe your game; three-word test |
| DL-SelectCapture | How to Select Good Game Capture (2020) | https://www.derek-lieu.com/blog/2020/4/13/how-to-select-good-game-capture | Clarity test; fewer ideas per shot; remove HUD that does not support the idea |
| DL-HalfLifeAlyx | Trailer Review - Half-Life: Alyx Gameplay Video 1 (2020) | https://www.derek-lieu.com/blog/2020/3/8/trailer-review-half-life-alyx-gameplay-video-1 | VR hands, head, performance; hold 1 to 2 s; teleport for first impressions |
| DL-Templates | Simple Game Trailer Templates (2020) | https://www.derek-lieu.com/blog/2020/3/1/simple-game-trailer-templates | Five templates (bullet point, music video, chronological, explain, "in a world") |
| DL-ShowTheDream | Show the Dream, Not the Job (2020) | https://www.derek-lieu.com/blog/2020/2/25/show-the-dream-not-the-job | Fantasy over task lists; multiplayer readability |
| DL-Pacing | How Trailers Tell a Story With Pacing (2020) | https://www.derek-lieu.com/blog/2020/1/20/how-trailers-tell-a-story-with-pacing | Four pacing shapes: slow burn, hero's journey, assault, dream |
| DL-EstablishingShots | Game Trailer Establishing Shots Don't Establish Much (2020) | https://www.derek-lieu.com/blog/2020/1/12/game-trailer-establishing-shots-dont-establish-much | At most 2 or 3 before interaction; answer genre, perspective and control early |
| DL-Backseats | Capture For the Backseats (2019) | https://www.derek-lieu.com/blog/2019/11/25/capture-for-the-backseats | Exaggerate; use the full frame; visible changes |
| DL-ChunkingConfusion | Chunking Out Confusion (2019) | https://www.derek-lieu.com/blog/2019/11/25/chunking-out-confusion | Group similar elements; direct the eye; keep busy shots brief |
| DL-EngagingOpening | Editing an Engaging Trailer Opening (2019) | https://www.derek-lieu.com/blog/2019/10/2/editing-an-engaging-trailer-opening | First 6 s matter most; 5 to 10 s cold open; six opening techniques |
| DL-ReviewQuotes | How to Use Review Quotes (2019) | https://www.derek-lieu.com/blog/2019/8/21/how-to-use-review-quotes | Quotes mid or late; 1 line best |
| DL-PreviewVsTrailer | Gameplay Preview vs Trailer (2019) | https://www.derek-lieu.com/blog/2019/5/15/gameplay-preview-vs-trailer | Preview 6 to 15 min vs trailer 60 to 90 s |
| DL-Variety | Variety is the Spice of Trailers (2019) | https://www.derek-lieu.com/blog/2019/3/27/variety-is-the-spice-of-trailers | Five variables; section order cold open, logos, intro, escalation, stopdown, climax |
| DL-DipToBlack | The Dip to Black (2019) | https://www.derek-lieu.com/blog/2019/3/27/the-dip-to-black | When fades help or hurt |
| DL-RememberTitles | Remember the Titles? (2019) | https://www.derek-lieu.com/blog/2019/1/21/remember-the-titles | Use cards sparingly; four valid uses |
| DL-NoLogo | No Logo (2019) | https://www.derek-lieu.com/blog/2019/1/19/no-logo | Logo placement and duration |
| DL-SlowIntro | The Slow Trailer Intro (2019) | https://www.derek-lieu.com/blog/2019/1/5/the-slow-trailer-intro | Seven ways a slow intro can still hook |
| DL-Length | Ideal Game Trailer Length and Labeling (2018) | https://www.derek-lieu.com/blog/2018/12/20/ideal-game-trailer-length-and-labeling | About 90 s default; duration bands; match the label |
| DL-CosmicTrip | Making a Trailer for Cosmic Trip - Tips for 1st Person VR Capture (2016) | https://www.derek-lieu.com/blog/2016/06/07/making-a-trailer-for-cosmic-trip-tips-for-1st-person-vr-capture | Smoothing; deliberate head and hands; hands higher in frame |
| DL-SimplestSteam | The Simplest Trailer to Make for Your Steam Page (Game Developer, 2021) | https://www.gamedeveloper.com/business/the-simplest-trailer-to-make-for-your-steam-page | Minimum viable store trailer: 3 to 5 s shots, 3 or fewer repeats, HUD on, 90 to 180 s |
| DL-FindingHook | Finding the Hook For a Game Trailer (Game Developer, 2019) | https://www.gamedeveloper.com/business/finding-the-hook-for-a-game-trailer | Cross-out method; four hook qualities; 60 to 90 s to pitch |

Other editors, platforms and coverage:

| Short name | Title / author | URL | Taken from it |
|---|---|---|---|
| MJC-Salvage | Salvage Your Game Trailer, M. Joshua Cauller | https://www.gamedeveloper.com/business/salvage-your-game-trailer | Intro of about 10 s is OK and 30 s loses viewers; teach basics in about 20 s; capture about 10x; logo, then CTA, then legal hierarchy |
| MJC-WorstMistakes | Worst trailer mistakes, M. Joshua Cauller (2016) | https://www.gamedeveloper.com/business/worst-trailer-mistakes | Seven mistakes with fixes; 90 s cap |
| MJC-LaunchTrailers | Launch trailers, M. Joshua Cauller (2019) | https://www.gamedeveloper.com/business/launch-trailers | Launch job (reinforce confidence, FOMO); "teach, test, escalate, test harder"; open mid-action |
| MJC-EmotionalJourney | Make trailers that capture the player's emotional journey, M. Joshua Cauller (2015) | https://www.gamedeveloper.com/business/make-trailers-that-capture-the-player-s-emotional-journey | Emotional highs and lows; failure as setup; party games and real player reactions |
| KG-VirtualCinematography | Virtual Cinematography for VR Trailers, Kert Gartner (2016) | https://www.gamedeveloper.com/business/virtual-cinematography-for-vr-trailers | Third person beats raw first person; avatars; smoothing; waist-up framing; hide HUD |
| KG-UploadVR | New Space Pirate / Fantastic Contraption trailers go beyond mixed reality (UploadVR) | https://www.uploadvr.com/new-space-pirate-fantastic-contraption-trailers-go-beyond-mixed-reality/ | Gartner's avatar-plus-virtual-camera method |
| STEAM | Steamworks Documentation: Trailers (Valve) | https://partner.steamgames.com/doc/store/trailer | First trailer primarily gameplay; HUD "often beneficial"; muted viewing; under 10 s in the Discovery Queue; 6 s microtrailers; specs |
| META | Meta Horizon Store VRC / publishing requirements and asset design best practices | https://developers.meta.com/horizon/resources/publish-quest-req and https://developers.meta.com/horizon/resources/publish-asset-design-bp/ | 2 min max; 1 to 2 min suggested; 16:9 with no letterbox; "Captured on" label; no other-platform hardware |
| META-MR | Meta mixed reality technical and trailer video guide | https://developers.meta.com/vr/resources/mixed-reality-technical-and-trailer-video/ | Mix first-person and third-person; storyboard; performer expressiveness |
| Road to VR | VR Design Unpacked: The Formula for Great VR Game Trailers, Ben Lang (pages 1 and 2) | https://www.roadtovr.com/inside-xr-design-make-great-vr-trailer-without-mixed-reality-capture/ | Hook in the first about 10 s before logos (Hellsweeper); smoothing; 60 fps; brighten for screens |
| GameDiscoverCo | Game trailers: breaking down a key discovery mistake, Simon Carless with Derek Lieu | https://newsletter.gamediscover.co/p/game-trailers-breaking-down-a-key | Gameplay at 15 to 30 s is too late; progress-bar scrubbers |
| Carless | The perfect Steam trailer, overlooked tools and more, Simon Carless | https://www.gamedeveloper.com/business/the-perfect-steam-trailer-overlooked-tools-more-2 | HUD debate; scrubbing behavior |
| Gameworldobserver | Common game trailer mistakes indie developers make on Steam, Evgeny Obedkov (quoting Lieu) | https://gameworldobserver.com/2022/09/22/common-game-trailer-mistkaes-indie-developers-steam | Slow intros; don't copy AAA |
| Zukowski | How To Market A Game (Chris Zukowski): Steam Page Checklist, 60 Mistakes ebook, coming-soon page post | https://howtomarketagame.com/wp-content/uploads/2020/03/SteamPageChecklistv1.pdf ; https://howtomarketagame.com/2025/03/10/when-should-i-post-my-steam-coming-soon-page/ | Gameplay in the first 3 s (checklist), 2 s (2023 guide, via search summary) and 5 s (other posts); genre clarity; HUD as genre shorthand. Read via search summaries, not full text. |
| Salters/frame.io | Things to Know Before You Create a Game Trailer or Promo, Chris Salters | https://blog.frame.io/2023/03/27/editing-game-promo-trailer/ | 60/30/15 s times 16:9/9:16/1:1 deliverables; "soupiness"; SFX; Harding photosensitivity test |
| Murauski | 7 questions you should ask before creating a trailer, Alexander Murauski | https://www.gamedeveloper.com/audio/7-questions-you-should-ask-and-answer-before-creating-a-trailer-for-your-game | Trailer vs teaser; App Store 15 to 30 s; voiceover about 140 wpm; sound-off readability |
| TikTok | TikTok Ads creative best practices; Auction Ads Creative Tips; simulation game tips | https://ads.tiktok.com/resources/help/article/creative-best-practices?lang=en | Hook within 6 s, proposition within 3 s; 63% of top-CTR ads state the message in the first 3 s; 9:16; end card +47.3% impressions (gaming). Read via search summary. |
| YouTube ABCD | The ABCDs of effective video ads (Google) | https://www.youtube.com/intl/en_ae/ads/abcds-of-effective-video-ads | Attract early; brand in the first 5 s raises recall but also skips; explicit CTA. Read via search summary. |
| PC Gamer | The art of making effective game trailers, Alex Wiltshire (2018) | https://www.pcgamer.com/the-art-of-making-effective-game-trailers | Via search snippets only: "lows don't look low unless there's a high"; audio first; progressive structure is not the only way |
| GDC | Trailer Made (Lieu, GDC 2019); Make the Trailer Before the Game (Lieu, GDC 2024); Creating Mixed Reality Trailers (Gartner) | https://gdcvault.com/play/1026358/Trailer-Made-What-Makes-a ; https://gdcvault.com/play/1034405/Independent-Games-Summit-Make-the ; https://gdcvault.com/play/1024520/Creating-Mixed-Reality-Trailers-and | Session descriptions only. Talk contents not accessed (see section 6). |
| Unity | Spectating VR (Unity blog) | https://unity.com/en/blog/games/spectating-vr | Smoothed spectator camera build approach (via search summary) |

---

## 6. Uncertain or disputed

1. **HUD on or off.** Sources disagree.
   - On: Steam ("often beneficial"), Lieu's minimum viable Steam trailer and Skin Deep opener, Zukowski (HUD is genre shorthand), Carless (partly).
   - Off: Lieu's templates post and capture-selection post, Cauller, Gartner (for VR).
   - Resolution I adopted [Inference]: HUD on for the first store trailer and gameplay trailers, off or reduced for announce and social cuts. Lieu himself does both ("use HUD selectively").
2. **How fast gameplay must appear.**
   - Zukowski says 2 s (2023 guide), 3 s (checklist) and 5 s (other posts).
   - Cauller says an intro of about 10 s is acceptable.
   - Lieu says the hook comes within the first 10 to 15 s, but player-controlled gameplay should come "right away" on Steam.
   - Steam says under 10 s in the Discovery Queue.
   - I used 2 to 3 s for store and social cuts and up to about 5 s for showcase reveals. None of these numbers comes from controlled studies. A summary notes Zukowski's figure is "a creative recommendation, not a scientifically validated threshold."
3. **Store trailer length.**
   - Lieu's minimum viable trailer: 90 to 180 s. Lieu's default trailer: about 90 s.
   - Cauller: 90 s max.
   - Meta: 1 to 2 min (2 min hard cap). One Meta VRC page also mentions about 30 s as a minimum.
   - Zukowski-adjacent summaries: "about a minute."
4. **Hook > genre > content vs genre-first.**
   - Secondary summaries attribute "Hook > Genre > Content" to Lieu.
   - Lieu's own 2021 posts say genre first, then hook, then content (genre can take 1 or 2 shots).
   - I could not find the original "Hook > Genre > Content" post. Practical reconciliation [Inference]: open on a hook shot that also shows the genre, then explain the hook.
5. **Five-part "cold open, act 1, act 2, act 3, button" structure.** This comes from a Lieu YouTube video ("How to Make a Trailer - Editing 3-Act Trailer Structure") and the GMTK video with Lieu. I saw only chapter titles and search summaries. The transcripts were blocked (HTTP 403). The "button" definition (a final joke, action beat or tease after the title) is from secondary summaries.
6. **Thumbstopper retention research.** Lieu says research shows that a rapid flash of images up front "significantly" improves retention. I did not locate the study.
7. **Short-form (15/30/60 s) structure.** No working game trailer editor I found published second-by-second beat sheets for social cuts. The social timing numbers come from TikTok and Google ad guidance and from marketing blogs (some low quality, not relied on). The templates in section 2 are my inference.
8. **Showing the brand early in short ads.** YouTube ABCD says a brand in the first 5 s raises recall but increases skips. Game editors say no logos up front. These can both hold [Inference]: show the game's world or characters early (that is the brand for a game) and put the logo late.
9. **Mixed reality vs avatar capture for VR.** Gartner moved from green-screen MR (2016) to in-game avatar or third-person cameras. Meta's own guide still promotes MRC. For a stylized candy game, Gartner's advice that MR suits realistic art and clashes with stylized art favors avatar or third-person capture [Inference for Deadly Jenga].
10. **Not accessed:** PC Gamer feature bodies (truncated), GDC talk videos (paywall or video), the Medium write-up of a Lieu talk (403), and the GMTK transcript (403). Claims from those are marked as coming via search summary.
11. **Meta "Captured on" label.** The requirement wording came from search-result excerpts of the VRC pages. Meta notes that its VRC index lists are "slightly out of date." Re-check the current VRC.Quest.Asset pages before submitting to the store.
