# 06 Measured trailers: what 22 acclaimed game trailers actually do

Written sources tell the playbook what editors *say* they do. This file measures what 22 official
trailers *did*, frame by frame, with scripts in `../tools/` that run on any mp4 (including our renders).
Every number below comes from those scripts plus my own look at downscaled filmstrips. Videos stayed
outside the repo (`Documents\Codex\Deliverables\TrailerCraft-Reference\videos`, not redistributed);
this folder holds only numbers, my own shot descriptions and the scripts.

Counts are written as "17 of 21" so the reader can see how thin or solid each claim is. "Body" means
the part of a trailer from its first content frame to the start of the closing title/end card.

---

## 1. Method and its error rates

### Pipeline (`../tools/`)
| Step | Script | What it does |
|---|---|---|
| Shots | `analyze.py` | Every frame at 128x72: HSV pixel difference and colour-histogram change. A frame is a cut if pixel change is >= 20 and >= 2.5x its 4 neighbours' mean, OR histogram change is >= 0.3 and >= 2.5x its neighbours (the second test rescues cuts inside very fast footage). Runs of near-black/near-white frames become fade boundaries. Optional hand corrections from `edits.json`. |
| Motion | `analyze.py` | Farneback optical flow per frame (mean pixels/frame at 128x72), averaged per shot. Static shots read 0.0-0.3, fast pans/POV 1.5-4. |
| Audio | `analyze.py` | librosa tempo, beats, downbeat guess (strongest low-frequency phase of 4), onsets, strong onsets (top 20 %), the 8 biggest hits (onset strength x loudness jump), RMS silences; ffmpeg EBU R128 integrated LUFS, LRA, true peak, 100 ms momentary/short-term curves. |
| Sync | `analyze.py`, `sync.py` | For each cut: distance to nearest beat / downbeat / onset / strong onset, share within 1, 2, 3 frames, and the **chance rate** for that tempo (share of all body time that lies within the same tolerance of a beat). Tracker-independent checks: Rayleigh vector strength of cut phases on the beat grid (and on the half-beat grid), and the onset-strength percentile at each cut versus every frame of the body (50 = random). |
| Looking | `sheets.py` | First/middle/last frame of every shot, 30 shots per sheet; `dense` mode (one frame every 0.25-0.4 s) to re-segment doubtful stretches; `validate` mode (frame before/after each cut plus near-miss peaks). |
| Ground truth | `candidates.py` | Lists every local peak of either metric far below threshold, makes before/after sheets, takes hand labels, grid-searches thresholds. |
| Hand data | `annotations/*.txt` -> `annot_convert.py` | Per shot: size, camera move, kind (gameplay / cinematic / text / live action / UI), job, short description; per trailer: first gameplay, first "what is this game", logo/title/end-card times, button. |
| Tables | `aggregate.py`, `profile.py` | Per-trailer rows, grouped tables, pooled tests, `data/reference_profile.json`, and `profile.py check` for a new trailer. |

To run on a new render: `sh run_one.sh <slug> <video.mp4>` (or `python -P analyze.py <mp4> <dir>`, then `sheets.py`), annotate in `annotations/<slug>.txt`, `annot_convert.py`, `sync.py <dir>`, `aggregate.py`, `profile.py check <root> <slug>`. Paths in the two shell scripts point at the Deliverables folder; change `ROOT` for another machine.

### Cut detector error rate
1. **Hand-labelled ground truth (3 trailers: Gang Beasts, Fall Guys E3 2019, Beat Saber release).**
   Every candidate peak (222) was judged by eye from before/after frames. 36 were clear hard cuts;
   graphic wipes, dissolves and Gang Beasts' letter-by-letter title flashes were marked ambiguous and
   left out (Fall Guys alone has 14 colour wipes).

   | Settings | Found | Missed | False | Miss rate | False rate |
   |---|---|---|---|---|---|
   | PySceneDetect-style defaults (ratio 3.0, min 15, pixel test only) | 31 | 5 | 1 | 13.9 % | 3.1 % |
   | **Tuned (ratio 2.5, min 20, + histogram test 2.5 / 0.3)** | 33 | 3 | 0 | **8.3 %** | **0 %** |

   36 cuts is a small tuning set; treat the tuned numbers as optimistic.
2. **Whole-sample review.** Every filmstrip of all 22 trailers was read; where a shot's first, middle
   and last frames showed different shots, or where a shot was a flicker fragment, I checked a dense
   sheet and corrected `edits.json`. Of 794 automatic boundaries, **62 were removed as false (7.8 %)**
   and **39 were added (missed)**. Errors cluster in a few trailers:
   - False cuts: strobe/VHS flicker (Lethal Company trailer, 29), fade-through-black fragments and a
     glowing logo (Moss, 25), head turns in a dark first-person take and an explosion flash
     (Half-Life: Alyx, 8).
   - Missed: graphic colour wipes and fast cuts between similar pink scenes (Fall Guys, 25), dissolves
     and split-screen cuts (It Takes Two, 7 in the first 24 s), a glass-shatter transition and two
     white-on-white cuts (Superhot VR, 3), slide-wipes (Ultimate Chicken Horse, 3), one whip-blur
     (Job Simulator, 1).
   - Added cuts are a lower bound: a miss is only caught when the three stills visibly disagree.
3. **What it cannot do:** wipes, slow dissolves and whip-pans are not detected as transitions;
   strobe, muzzle flashes and camcorder glitches create false cuts. Content Warning's deliberate
   camcorder jump-cut glitches were kept as cuts (the image does jump); fragments under 0.12 s are
   merged into the next shot for all shot-length statistics.

### Audio timing calibration
A synthetic clip (`make_calib.py`: clicks at 120 bpm, hard colour cut exactly on every second click)
showed librosa onset/beat times landing ~30 ms (about 1 frame) late. After a fixed 30 ms correction,
**20 of 20 cuts were within 1 frame of a tracked beat** and the phase vector strength was 0.997, so the
pipeline does see beat-locked cutting when it exists. librosa snaps tempo to a grid (117.5 for 120),
which drifts beats by up to 1 frame per 10 s. On the real trailers a second tracker run on the
percussive part alone agreed with the first within 2 frames on a median of ~0.9 of beats (range 0.1
Walkabout to 1.0); both are librosa, so agreement is not proof the beats are right.

### Looking
All 22 trailers were annotated shot by shot from filmstrips (full trailer if <= 95 s, otherwise the
first 60 s and the last 20 s): 686 shot annotations (ranges of near-identical shots were labelled together in Content Warning and Job Simulator). Camera motion comes from three stills plus the flow
number; "push" vs "static" is a judgement, and POV gameplay is its own class.

---

## 2. Trailers measured

22 official trailers: 9 VR, 13 flat; 4 short (<= 32 s), 11 mid (33-75 s), 7 long (> 75 s).
Downloads were official uploads only: the developer's/publisher's/platform's YouTube channel, or the
game's official Steam store page (developer-supplied trailer files). Dates are YouTube upload dates;
Steam store files carry no public date.

| # | Trailer | Uploader / source | Date | Length | Type | VR | Shots (corr.) |
|---|---|---|---|---|---|---|---|
| 1 | Human Fall Flat VR - launch spot | Curve Games, YouTube `zEsSjrmLg4Y` | 2024-10-03 | 18.0 s | launch | yes | 2 |
| 2 | Beat Saber - Teaser (OST "7") | Beat Saber Official, YouTube `TkCD58VxS7w` | 2024-05-24 | 22.0 s | teaser | yes | 6 |
| 3 | Lethal Company - Version 85 Teaser | Zeekerss, YouTube `slaywoqP3XU` | 2026-11-01 | 27.0 s | teaser | no | 13 |
| 4 | Beat Saber - Release Trailer | Beat Saber Official, YouTube `vL39Sg2AqWg` | 2018-05-01 | 31.2 s | launch | yes | 4 |
| 5 | Walkabout Mini Golf - PS VR2 Launch | PlayStation, YouTube `SULUgn3pja8` | 2023-05-05 | 35.2 s | launch | yes | 23 |
| 6 | Among Us - Release Trailer | Innersloth, Steam app 945360 | - | 43.0 s | launch | no | 18 |
| 7 | Teardown trailer | Teardown, YouTube `cVRyoUJyxIE` | 2021-01-10 | 43.1 s | reveal | no | 30 |
| 8 | TABS - Early Access Trailer | Landfall, YouTube `Z2e9vd3Znz4` | 2019-04-01 | 50.1 s | launch | no | 35 |
| 9 | Lethal Company - Trailer | Zeekerss, Steam app 1966720 | - | 52.3 s | launch | no | 32 |
| 10 | Untitled Goose Game - Honk Trailer | House House, Steam app 837470 | - | 54.5 s | gameplay | no | 23 |
| 11 | SUPERHOT VR - Release Trailer | SUPERHOT, YouTube `pzG7Wc6mbwE` | 2016-12-05 | 55.1 s | launch | yes | 26 |
| 12 | Gang Beasts Trailer | Boneloaf, YouTube `tNgCy92QWZY` | 2020-12-11 | 58.3 s | gameplay | no | 32 |
| 13 | Party Animals - Pre-Order Trailer | Party Animals, YouTube `3vicJv-bZa0` | 2023-08-23 | 60.9 s | gameplay | no | 33 |
| 14 | Content Warning - Trailer | Landfall, Steam app 2881650 | - | 69.1 s | reveal | no | 94 |
| 15 | Ultimate Chicken Horse - Launch | Clever Endeavour, Steam app 386940 | - | 72.4 s | launch | no | 40 |
| 16 | Besiege - Alpha Trailer | Spiderling, Steam app 346010 | - | 79.6 s | reveal | no | 21 |
| 17 | Job Simulator - Trailer | Owlchemy Labs, Steam app 448280 | - | 87.4 s | launch | yes | 66 |
| 18 | Gorilla Tag - release trailer | Another Axiom, Steam app 1533390 | - | 93.6 s | launch | yes | 51 |
| 19 | Fall Guys - E3 2019 Trailer | PlayStation, YouTube `GAiaXIxS_4c` | 2019-06-10 | 93.8 s | reveal | no | 35 |
| 20 | Half-Life: Alyx - Announce Trailer | Valve, Steam app 546560 | - | 109.0 s | reveal | yes | 59 |
| 21 | Moss - Launch Trailer | Polyarc, Steam app 846470 | - | 120.0 s | launch | yes | 52 |
| 22 | It Takes Two - Reveal Trailer | Hazelight/EA, Steam app 1426210 | - | 162.5 s | reveal | no | 96 |

Relevance coverage: giant-versus-small (Moss: the player is a giant "Reader" helping a tiny mouse;
It Takes Two: shrunken parents), asymmetric/party/physics (Gang Beasts, Party Animals, TABS, Fall
Guys, UCH, Among Us, Teardown, Besiege, Goose), co-op horror comedy (Lethal Company x2, Content
Warning), VR shown flat (9).

**Skipped.** After 11 YouTube downloads, YouTube answered HTTP 403 to every further plain download
(no workaround was attempted beyond one later retry): Fall Guys launch trailer, Fall Guys x Incredibles
(23 s official cut), Untitled Goose Game release-date trailer (replaced by its Steam Honk trailer), Job
Simulator Quest trailer (replaced by the Steam trailer), Human Fall Flat gameplay trailer, Astro Bot
announcement, Shadow of the Colossus (2018) launch, Dead by Daylight launch, Evolve Big Alpha. Not on
Steam or not found there: Astro Bot, Shadow of the Colossus, Evolve, Fall Guys. Totally Reliable
Delivery Service was not measured (wrong store ID, dropped for time). **Consequence:** only 4 short
cuts (the target was at least 4 of <= 30 s; one is 31.2 s), and two of those are single takes, so the
short class is weak (section 6).

---

## 3. Aggregate tables

Median and interquartile range (25th to 75th percentile) per group. Shot statistics use picture shots
in the body (no black, no text cards, fragments < 0.12 s merged). Full table: `../tools/data/tables.md`;
per trailer: `../tools/data/per_trailer.csv`.

| metric | all (n=22) | short <=32 s (4) | mid 33-75 s (11) | long >75 s (7) | reveal (6) | launch (11) | VR (9) | flat (13) | party/physics (10) |
|---|---|---|---|---|---|---|---|---|---|
| median shot, s | 1.62 (1.43-2.03) | 12.5 (7.4-14.3) | 1.60 (1.44-1.71) | 1.53 (1.25-1.92) | 1.28 (1.12-1.83) | 1.62 (1.49-2.00) | 1.67 (1.42-4.53) | 1.60 (1.43-2.03) | 1.65 (1.54-2.09) |
| mean shot, s | 1.88 (1.71-2.58) | 12.5 | 1.83 (1.59-2.09) | 1.88 (1.75-2.72) | 1.75 | 1.97 | 2.04 | 1.87 | 1.93 |
| 10th-pct shot, s | 0.78 (0.47-1.32) | - | 0.83 (0.60-1.23) | 0.50 (0.28-0.61) | 0.28 (0.21-0.43) | 0.83 | 0.74 | 0.83 | 0.80 |
| longest shot, s | 5.9 (4.2-9.7) | 14.1 | 4.2 (3.3-5.3) | 9.5 (7.6-10.8) | 7.5 | 5.9 | 10.3 | 5.7 | 5.3 |
| median shot 1st quarter / last quarter | 1.95 / 1.63 | - | 1.70 / 1.57 | 3.17 / 1.45 | 2.78 / 1.25 | 1.75 / 1.48 | 2.01 / 1.57 | 1.70 / 1.70 | 2.07 / 1.70 |
| last/first quarter ratio | 0.85 (0.41-1.26) | - | 1.06 (0.68-1.51) | 0.57 (0.37-0.88) | 0.39 (0.20-0.75) | 0.90 | 0.69 | 0.90 | 0.85 |
| cuts per minute | 27 (19-35) | 4 | 30 (25-38) | 27 (19-34) | 32 (22-40) | 26 (19-34) | 23 | 27 | 29 |
| most cuts in a 5 s window | 4 (3-6) | 0-1 | 4 (4-5.5) | 6 (4-7.5) | 6.5 | 4 | 4 | 4 | 4 |
| first gameplay, s | 1.0 (0.2-5.9) | 1.5 | 0.27 (0.04-1.5) | 11.0 (3.3-28) | 3.3 | 0.5 | 1.75 | 0.27 | 0.30 |
| first "what is this game", s | 7.3 (2.1-12.0) | 2.0 | 4.3 (2.3-8.8) | 12.0 (10.7-17.0) | 10.7 | 4.2 | 3.2 | 10.3 | 3.7 |
| closing title start / runtime | 0.90 (0.85-0.92) | 0.80 (0.62-0.87) | 0.91 (0.88-0.92) | 0.89 (0.86-0.92) | 0.89 | 0.88 | 0.85 | 0.92 | 0.90 |
| title + end card, s | 6.2 (5.5-7.1) | 5.1 (4.0-5.7) | 5.8 (5.2-6.4) | 10.5 (7.6-12.9) | 9.3 | 6.3 | 6.0 | 6.3 | 6.4 |
| integrated loudness, LUFS | -16.0 (-19.6 to -13.0) | -15.9 | -16.9 | -16.0 | -15.0 | -15.9 | -15.9 | -16.9 | -15.2 |
| true peak, dBTP | -0.9 (-3.6 to +1.1) | -1.0 | +0.1 | -2.5 | +0.8 | -1.1 | -1.2 | +0.1 | -0.5 |
| loudness last quarter minus first, LU | +3.7 (1.4-6.0) | +3.6 | +2.9 | +6.1 | +10.4 | +3.8 | +3.4 | +3.7 | +1.9 |
| closing title vs body, LU | -8.9 (-17.8 to -6.0) | -2.9 | -8.9 | -14.3 | -10.6 | -8.9 | -12.1 | -8.5 | -7.0 |
| stop-downs in body (>= 0.3 s, >= 12 LU dip) | 1.5 (0-3.8) | 0 | 2 | 2 | 3 | 2 | 1 | 2 | 1 |
| cuts within 2 frames of a beat | 0.30 (0.19-0.34) | 0.20 | 0.30 | 0.25 | 0.27 | 0.30 | 0.21 | 0.30 | 0.34 |
| ... chance for that tempo | 0.27 (0.20-0.29) | 0.27 | 0.27 | 0.28 | 0.27 | 0.28 | 0.28 | 0.27 | 0.27 |
| cuts within 2 frames of a strong onset | 0.10 (0.06-0.15) | 0.20 | 0.10 | 0.07 | 0.11 | 0.07 | 0.06 | 0.12 | 0.11 |
| ... chance | 0.08 (0.04-0.10) | 0.09 | 0.09 | 0.06 | 0.06 | 0.09 | 0.06 | 0.08 | 0.10 |
| onset percentile at cuts (50 = random) | 56 (48-59) | 56 | 58 | 51 | 50 | 56 | 54 | 56 | 52 |
| share wide shots | 0.50 (0.39-0.64) | 0.00 | 0.62 | 0.44 | 0.51 | 0.45 | 0.24 | 0.59 | 0.60 |
| share medium shots | 0.38 (0.28-0.48) | 1.00 | 0.36 | 0.34 | 0.33 | 0.46 | 0.56 | 0.34 | 0.34 |
| share close shots | 0.05 (0.00-0.10) | 0.00 | 0.04 | 0.10 | 0.06 | 0.06 | 0.08 | 0.04 | 0.04 |
| share static camera | 0.67 (0.11-0.79) | 0.00 | 0.75 | 0.47 | 0.29 | 0.52 | 0.25 | 0.70 | 0.71 |
| share first-person (POV) | 0.00 (0-0.56) | 1.00 | 0.00 | 0.00 | 0.01 | 0.48 | 0.67 | 0.00 | 0.00 |
| share designed moves (push/pan/follow/orbit) | 0.11 (0-0.21) | 0.00 | 0.11 | 0.21 | 0.21 | 0.04 | 0.03 | 0.14 | 0.14 |
| cuts joining two shots of the same size | 0.59 (0.44-0.81) | 0.72 | 0.62 | 0.49 | 0.48 | 0.60 | 0.58 | 0.60 | 0.50 |

Shot sizes are coarse (extreme wide, wide, medium, close) and judged from 176x99 stills.

**Pooled sync tests (599 body cuts, 21 trailers):**

| Reference event | Cuts within +-2 frames | Chance | p (one-sided binomial) |
|---|---|---|---|
| tracked beat | 25.0 % | 24.9 % | 0.47 |
| guessed downbeat | 5.5 % | 6.2 % | 0.78 |
| any detected onset | 40.9 % | 36.5 % | 0.014 |
| strong onset (top 20 %) | 9.8 % | 7.4 % | 0.016 |
| strong onset, cuts ending a shot < 0.8 s (n=156) | 12.8 % | 6.7 % | 0.004 |
| strong onset, cuts ending a shot >= 0.8 s (n=516) | 9.1 % | 7.4 % | 0.08 |

Beat-phase test over 659 cuts: vector strength 0.036 (0 = cuts spread evenly over the beat, 1 = all at
one point), p = 0.44; half-beat grid 0.036, p = 0.42. Phase histogram in eighths of a beat:
73, 91, 72, 86, 75, 75, 89, 98 (flat).

---

## 4. Patterns

Patterns that hold in most of the sample:

1. **Montage shots sit around 1.6 s.** In mid and long trailers the median picture shot is 1.0-2.2 s in
   16 of 18 (median 1.6 s, mean 1.9 s). Exceptions: Content Warning 0.77 s (camcorder jump cuts),
   Besiege 3.7 s (slow alpha trailer). Cuts per minute 20-48 in 16 of 18.
2. **Every montage has quick shots and one long breath.** 10th-percentile shot <= 1.0 s in 13 of 18;
   at least one shot >= 4 s in 13 of 18 (Alyx's 12.9 s first-person opener, Moss's 9.5 s logo shot; Gorilla Tag's 11 s
   live-action finale; It Takes Two's 10.5 s slow-motion fire shot).
3. **Gameplay comes first and fast.** First gameplay frame within 2 s in 13 of 21 trailers that have
   gameplay, within 8 s in 17 of 21. The four late ones are a CG reveal (Fall Guys E3: first real
   gameplay at 64.6 s; 12 % gameplay by runtime), a story opener (Moss 41 s), a cinematic co-op reveal
   (It Takes Two 11 s) and a live-action skit (Job Simulator 15.5 s). Party and physics trailers start
   on gameplay: median 0.3 s.
4. **"What is this game" lands by 12 s** in 15 of 21 (median 7.3 s; by 5 s in 9 of 21). It is usually
   a single readable verb: brawlers knocked into a grinder (Gang Beasts 7.3 s), a catapult crew loading
   (TABS 4.3 s), "Play online with 4-15 players" and "...is an impostor" captions (Among Us 5.4 s),
   time freezing around red men (Superhot 1.5 s).
5. **The name waits for the end.** The closing title starts at 80-95 % of runtime in 18 of 22
   (median 90 %). Only 2 of 22 show the game name early (Gang Beasts' letter-flash title at 19 %, Human
   Fall Flat VR's title laid over its single take at 22 %), and both still close on it.
6. **The title/end card holds 4-8 s** in 15 of 22 (median 6.2 s); long trailers hold 8-13 s (median
   10.5 s); the four short spots 4-6 s.
7. **The music builds, then the title drops out.** Short-term loudness in the last quarter of the body
   is higher than in the first quarter in 16 of 20 (median +3.7 LU; reveals +10.4 LU). The closing title
   is at least 6 LU quieter than the body in 16 of 21 (median -8.9 LU). 14 of 22 have at least one
   stop-down (a >= 0.3 s dip >= 12 LU below the body) and 7 of 22 put one in the 4 s before the title.
8. **Cuts are not locked to the beat grid, but they lean towards hits.** Pooled, cuts land within
   2 frames of a tracked beat exactly as often as chance (25.0 % vs 24.9 %); no trailer passes the phase
   test (0 of 21) and 3 of 21 pass the +-2-frame test, about what chance gives. Cuts do land on audio
   onsets more than chance (40.9 % vs 36.5 %, p = 0.014), and in rapid passages (shots under 0.8 s) they
   land on strong hits at twice chance (12.8 % vs 6.7 %, p = 0.004). Clearest case: TABS' final duel
   barrage, six shots of 0.37-0.87 s, each cut within 0-3 frames of a beat or strong onset. Read: editors
   cut music and sound effects *to the picture* and land the barrages on hits; they do not frame-lock
   every cut to a metronome. The biggest audio hit in the body sits within 3 frames of a cut in only
   7 of 22.
9. **Wide shots dominate flat gameplay; VR lives in first person.** Wide is the most common size in
   14 of 21; close shots are rare (<= 10 % in 15 of 21, median 5 %). Flat trailers are mostly
   locked-off game cameras (static >= 50 % in 10 of 13); VR trailers are mostly POV (>= 50 % of shots in
   6 of 8 with picture). Designed camera moves (push, pan, follow, orbit) are a small share (median 11 %),
   larger in reveals (21 %).
10. **Gameplay montages do not alternate shot sizes.** 59 % of cuts join two shots of the same size
    (12 of 20 above half; Gang Beasts 91 %, Goose 100 %, UCH 94 %, all one fixed wide game camera).
    Film "wide-medium-close" alternation is a cinematic-trailer habit (reveals 48 %), not a gameplay one.
11. **Mostly gameplay, with words doing the teaching.** Gameplay is >= 70 % of runtime (by eye) in
    16 of 22. 11 of 22 use on-screen text inside the body (feature captions, rule captions, jokes
    such as Goose's "It's finally time" ... "to honk.", press quotes).
12. **VR trailers show the body sometimes, the world always.** 2 of 9 VR trailers cut to live action of
    a player in a headset (Gorilla Tag: 13 live-action shots interleaved with POV and finishing on an
    11 s shot where the living room turns into jungle; Job Simulator: a live-action morning skit and
    mixed-reality inserts). The other 7 stay in-engine.
13. **Buttons after the title are rare:** 2 of 22 (Alyx: G-Man steps out of the dark after the logo;
    Job Simulator: the title sits over the player asleep in an office chair). Content Warning puts its
    joke just before the logo (friends watching their footage at home).
14. **Logos up front are uncommon:** 4 of 22 open on a developer/platform logo (Gang Beasts, Goose,
    Alyx, Walkabout); the rest open on content (rating cards aside).

Notable exceptions and splits:
- **Shots shortening into the climax is a tendency, not a rule.** The last quarter's median shot is
  shorter than the first quarter's in 12 of 19 (median ratio 0.85); Spearman rank correlation between
  shot length and time is negative in 12 of 18. Reveals and long trailers shorten strongly (ratio 0.39
  and 0.57: slow openers, fast endings); mid-length party trailers do not (1.06). Superhot VR,
  Party Animals, Goose, UCH and It Takes Two all get *longer* in their last quarter (they end on a
  held gag or hero shot).
- **Motion does not reliably rise:** mean flow in the last quarter exceeds the first in only 10 of 19.
- **Loudness:** 13 of 22 are between -20 and -12 LUFS integrated; 11 of 22 have true peaks above
  -1 dBTP (these are platform files; loudness normalisation happens at playback, not in the file).
- **Short spots (n=4) follow two different strategies:** two are a single continuous gameplay take
  with the title over it (Human Fall Flat VR 16 s; Beat Saber 14 s + 11 s), one is graphic-only (Beat
  Saber "7"), one is a slow mood teaser whose shots shrink from 9.7 s to 0.3 s before a text card
  (Lethal Company V85). No short montage was measured.

---

## 5. Worked examples

Times in seconds. Size: EW/W/M/C, T = text/graphic card. "On" = distance from the cut to the nearest
strong audio onset, in frames (30 fps); "beat" = to the nearest tracked beat.

### A. Totally Accurate Battle Simulator - Early Access trailer (50.1 s, 123 bpm, -13.0 LUFS)
Mid-length physics comedy; the acceleration-into-logo pattern in its purest form.

| Shots | Time | Size / camera | Job | What happens | Sound |
|---|---|---|---|---|---|
| 1 | 0.07-2.10 | EW push | hook | the floating island map, push in | music enters |
| 2-3 | 2.10-4.30 | W static | hook, teach | red army lines up; shield wall | cut 3 on a strong onset (0.2 f) |
| 4-6 | 4.30-10.51 | M follow, W static, W follow | teach, joke, escalate | catapult crew loads; boulders fly; boulder hits the blue army | cut 5 on an onset (0.9 f) |
| 7-10 | 10.51-18.55 | M / M / M / W static | escalate, joke | knights under arrows; mammoths charge; archers flatten a soldier; druid casts | loudness climbing (-18 to -14 LU momentary) |
| 11 | 18.55-19.85 | EW static | wow | a massive army on a plain | |
| 12-24 | 19.85-39.37 | W/M mix, 1.0-2.2 s each, two blurred "handheld" crash shots | variety, wow, escalate | unit tour, mammoth stampede, cart crash, axe men flying over shields | loudest stretch (-9 to -11 LU) |
| 25-31 | 39.37-44.24 | M static (duels) | joke (barrage) | six one-on-one duels, 0.87, 0.70, 0.40, 0.37, 0.47, 0.43 s, one per beat | cuts 25-31 within 0-3 f of a beat or strong onset |
| 32-34 | 44.24-50.13 | T | title, CTA | black flash, studio logo then game logo, "Available now in Steam Early Access" (5.8 s) | music resolves, quieter |

Numbers: median shot 1.60 s, last-quarter / first-quarter ratio 0.55, onset percentile at cuts 64,
wide 45 %, medium 45 %, static camera 81 %. Takeaway: a 1.6 s house rhythm, a single extreme wide as the
"wow" at about 40 %, and a barrage of one-beat shots straight into the logo.

### B. Gang Beasts (58.3 s, 144 bpm, -20.4 LUFS)
Party physics; early title, then a level tour on a fixed wide game camera.

| Shots | Time | Size / camera | Job | What happens |
|---|---|---|---|---|
| 0-1 | 0-7.34 | T | brand | developer logo, partner logo; dissolve into play |
| 2-4 | 7.34-10.88 | W static | hook | brawlers knocked into a grinder pit; brawl on an iceberg; punch in the ring |
| 5-9 | 10.88-12.75 | T (0.13-1.07 s) | title | title letters flash one by one over the ring |
| 10-28 | 12.75-49.82 | W (one M) static, a few follow | variety | 19 levels, 1.3-2.8 s each: train roof, truck roofs, helicopter, gondolas, vat, ferris wheel, incinerator, lighthouse ... |
| 29-30 | 49.82-53.22 | W follow / static | joke | a fighter dangling from a truck; a pile of fighters on a wharf |
| 31 | 53.22-58.26 | T | CTA | yellow end card with logo and platforms (5.0 s) |

Numbers: median 1.70 s with almost no spread (10th pct 1.40 s, max 2.8 s), 96 % wide, 91 % of cuts join
two wides, no stop-downs, beat-phase vector strength 0.25 (not significant), onset percentile 48. It is
the "even metronome" style: no barrage, no breath, variety does the work.

### C. Gorilla Tag release trailer (93.6 s, VR)
How a VR game sells itself to a flat screen: POV play cut against live action of players.

| Shots | Time | Size / camera | Kind | Job | What happens |
|---|---|---|---|---|---|
| 0-2 | 0-2.10 | C push | gameplay | hook | dark canyon wall, near silence (-55 LU momentary) |
| 3 | 2.10-4.17 | M static | gameplay | hook | a gorilla in a banana hat faces camera and waves; first big hit (2.85 s) |
| 4-7 | 4.17-12.40 | W / EW POV | gameplay | teach, variety | arm-swing locomotion over ropes, a chase, a night slide, a mountain vista |
| 8-9 | 12.40-18.47 | M static | gameplay + text | joke | gorillas in party hats; "monke /mon-keh/" definition card |
| 10-17 | 18.47-39.20 | W POV / M-W live action | mixed | teach | live action of players flapping arms in living rooms, alternating with the POV it produces |
| 18-47 | 39.20-75.87 | M/W POV 0.3-2.6 s, live action 0.4-2.2 s | mixed | escalate, wow | faster chase montage, lava-skin tagger, crystal-cave falls; live action every 2-4 shots |
| 48 | 75.87-86.87 | W push | live action | wow | a player walks while the living room becomes jungle (11 s) |
| 49 | 86.87-93.10 | T | text | title | logo |

Numbers: median shot 1.53 s, longest 11.0 s (the finale), POV 56 % of picture shots, live action in
13 shots, length-time correlation -0.58 (shots shorten until the finale breath), onset percentile 42.
Takeaway for Deadly Jenga: show the person in the headset, then what they see; end on a long, single
"you are there" image before the logo.

### D. Lethal Company - Version 85 teaser (27.0 s, -27.9 LUFS)
Short spot built on withholding.

| Shots | Time | Size / camera | Job | What happens |
|---|---|---|---|---|
| 0-1 | 0-0.23 | - | build | black, fade up |
| 2 | 0.23-9.94 | W tilt down | build | slow tilt from a ladder tower to a foggy valley (9.7 s) |
| 3 | 9.94-15.35 | EW push | build | slow push over a cliff landscape at dusk (5.4 s) |
| 4-9 | 15.35-20.52 | W/M static, a black beat | build | dark facility, a hanging lamp, black (0.7 s), a lit room |
| 10 | 20.52-23.69 | W push | escalate | cave tunnel into a corridor |
| 11 | 23.69-25.09 | M static | wow | a figure in the corridor, burst of debris: the biggest hit (24.86 s) |
| 12 | 25.09-27.03 | T | title | "V85 is coming soon" (1.9 s) |

Numbers: shot lengths fall 9.7 -> 5.4 -> 1.0 -> 0.7 -> 0.3 s, last/first ratio 0.19, loudness +3.6 LU
from first to last quarter, peak hit at 92 % of runtime, one shot before the card.

---

## 6. Numeric rules for the playbook

Each rule gives the pass range, the supporting count, and the sample it comes from. "Mid" = 33-75 s
(n=11, the best-supported class). Where the short class is too thin, the rule says so.

### Length class 15-30 s (social cuts; weak evidence, n=4, none of them a montage)
- **S1. Pick one of two shapes.** (a) A single continuous gameplay take with the title laid over it
  (2 of 4: HFF VR, Beat Saber), or (b) a montage at the mid-class rhythm (rule M1). Do not mix: a 15 s
  piece with 5-6 shots of 2-3.5 s is neither.
- **S2.** First gameplay frame within 2 s (3 of 3 short spots with gameplay: 0.2, 1.5, 2.0 s).
- **S3.** Title/end card 4-6 s (short spots 4.6, 5.5, 6.0 s; Lethal V85's text card 1.9 s is the outlier).
- **S4.** If montage, borrow M1-M3 and expect about 8-12 shots in 15 s (1.6 s median shot).
- **S5.** Put the single biggest hit in the last 10-20 % before the card (Lethal V85 at 92 %).

### Length class 31-75 s (the main trailer)
- **M1. Median shot 1.0-2.2 s** (16 of 18 mid/long; mid median 1.60 s, IQR 1.44-1.71). Mean 1.6-2.1 s.
- **M2. Spread:** at least 10 % of shots <= 1.0 s (13 of 18) and at least one shot >= 3.3 s (mid
  25th percentile of the longest shot; 13 of 18 have one >= 4 s). Cuts per minute 25-38 (mid IQR).
- **M3. One barrage:** at least one 5 s window with 4 or more cuts (mid median 4, IQR 4-5.5), usually
  in the last third (TABS right before its logo; Gorilla Tag in its chase section; It Takes Two
  before its 30 s stop-down).
- **M4. First gameplay <= 2 s** (13 of 21) and **never later than 8 s** unless the opener is a
  deliberate cinematic/story hook (17 of 21 by 8 s).
- **M5. "What is this game" by 12 s** (15 of 21; mid median 4.3 s, IQR 2.3-8.8 s): one readable verb
  (pull a block, a tower falls) on screen by then.
- **M6. Closing title starts at 80-95 % of runtime** (18 of 22; mid IQR 88-93 %). Do not show the
  name early unless it is laid over gameplay.
- **M7. Title + end card 4-8 s** (15 of 22; mid IQR 5.2-6.4 s).
- **M8. Loudness:** integrated -20 to -12 LUFS (13 of 22; median -16); deliver true peak <= -1 dBTP
  (a delivery rule, not a sample norm: 11 of 22 references exceed it).
- **M9. Build:** last quarter of the body at least as loud as the first (16 of 20; aim +2 to +6 LU,
  mid IQR +0.2 to +5.6).
- **M10. Drop out for the title:** closing title 6-20 LU quieter than the body (16 of 21 at -6 LU or
  lower; mid IQR -21 to -7 LU). Optional stop-down (>= 0.3 s near-silence) in the 4 s before the title
  (7 of 22).
- **M11. Cut to hits, not to a grid.** Do not require beat-locked cuts: the references do not do it
  (pooled 25.0 % within 2 frames vs 24.9 % chance). Do require that cuts in fast passages land on audio
  hits: for cuts after shots shorter than 0.8 s, at least 12 % within 2 frames of a strong onset
  (references 12.8 % vs 6.7 % chance), and overall onset percentile at cuts >= 50 (mid IQR 50-65).
- **M12. Shot grammar:** wide is the house size (mid IQR 47-80 % wide), close shots 0-7 % (mid IQR)
  but at least one for the human/peril beat (most trailers have one; 15 of 21 keep them <= 10 %).
  Repeating the same size across a cut is normal for gameplay (mid IQR 54-92 %), so do not force
  alternation; vary the *place or action* instead.
- **M13. Camera:** in gameplay sections most shots static or tracking game cameras (flat: static >= 50 %
  in 10 of 13); designed moves (push/orbit/crane) about 10-20 % of shots, reserved for the opener, the
  "wow" and the title.
- **M14. Words teach:** if a rule is not readable from picture in 2 s, add a short caption (11 of 22
  do); keep gameplay >= 70 % of runtime (16 of 22).
- **M15. VR:** POV for play (6 of 8 VR trailers mostly POV) and, if possible, live action of the
  headset player intercut every 2-4 shots (2 of 9: Gorilla Tag, Job Simulator).

### Longer than 75 s
- Slow opener allowed (first-quarter median shot 3.2 s, IQR 1.5-5.0), but shorten into the end
  (last/first ratio 0.57, IQR 0.37-0.88), median shot 1.25-1.9 s overall, title + card 7.6-12.9 s,
  loudness ramp +6 LU.

### Reveal vs launch vs gameplay
- Reveals: faster body (median 1.28 s, 10th pct 0.28 s), more designed camera moves (21 %), bigger
  loudness ramp (+10.4 LU), later first gameplay (3.3 s) and "what" (10.7 s).
- Launch/gameplay cuts: gameplay by 0.5 s, "what" by 4.2 s, steadier rhythm (ratio 0.90).

---

## 7. Reference profile

`../tools/data/reference_profile.json` holds, per class (`short`, `mid`, `long`, `all`), the n, the
trailers, and for 23 metrics the median, p10, p25, p75, p90, min and max, plus the rule list above with
thresholds and support counts. `profile.py check <root> <slug>` prints PASS (inside p25-p75), OK
(inside p10-p90) or OUT for each metric, then each rule. For a short trailer, montage metrics
(shot lengths, cut density, onsets, sizes) are compared with the mid class, because the short
references are single takes. Excerpt (mid class, n=11):

```json
{
  "msl":              {"p25": 1.44, "median": 1.60, "p75": 1.71, "p10": 1.17, "p90": 2.03},
  "asl":              {"p25": 1.59, "median": 1.83, "p75": 2.09},
  "sl_p10":           {"p25": 0.60, "median": 0.83, "p75": 1.23},
  "sl_max":           {"p25": 3.28, "median": 4.17, "p75": 5.33},
  "cuts_per_min":     {"p25": 24.7, "median": 30.0, "p75": 38.1},
  "max_cuts_5s":      {"p25": 4,    "median": 4,    "p75": 5.5},
  "first_gameplay":   {"p25": 0.04, "median": 0.27, "p75": 1.5},
  "first_what":       {"p25": 2.3,  "median": 4.3,  "p75": 8.8},
  "final_title_frac": {"p25": 0.88, "median": 0.91, "p75": 0.93},
  "endcard_len":      {"p25": 5.2,  "median": 5.8,  "p75": 6.4},
  "lufs":             {"p25": -19.5,"median": -16.9,"p75": -13.3},
  "loud_ramp":        {"p25": 0.2,  "median": 2.9,  "p75": 5.6},
  "title_vs_body_LU": {"p25": -21.3,"median": -8.9, "p75": -7.0},
  "onset_pct_mean":   {"p25": 49.8, "median": 57.5, "p75": 65.1},
  "onset_top25_share":{"p25": 0.21, "median": 0.35, "p75": 0.42},
  "size_W":           {"p25": 0.47, "median": 0.62, "p75": 0.80},
  "size_C":           {"p25": 0.00, "median": 0.04, "p75": 0.07}
}
```

---

## 8. Our attempt against the profile

`attempt-1/trailer-attempt-1.mp4`: 15.0 s, 30 fps, 6 shots, -15.1 LUFS, true peak -0.6 dBTP, tempo
112, music biggest hit at 2.66 s. Detector found 5 cuts at 2.80, 5.20, 8.60, 10.20, 12.60 s (all
correct on the filmstrip; the title fades in over the last shot at 12.7 s). Class: short; montage
metrics compared with mid.

| Metric | Attempt 1 | Reference | Verdict |
|---|---|---|---|
| median shot | **2.40 s** | 1.0-2.2 (montage); short refs are single takes of 9-16 s | OUT: too slow for a montage, too cut for a single take (rule S1) |
| shortest / 10th-pct shot | **2.0 s** | mid 10th pct 0.6-1.2 s | OUT: no quick shot anywhere |
| most cuts in 5 s | **2** | mid 4-5.5 | OUT: no barrage before the title (M3) |
| longest shot | 3.4 s | mid 3.3-5.3 | PASS |
| cuts per minute | 23.6 | mid 25-38 | OK (low) |
| first gameplay | 0.0 s | <= 2 s | PASS |
| first "what" | 5.2 s (block slides) | <= 12 s | PASS on time, but FINDINGS F17: the pull reads poorly; no hand on screen |
| closing title start | 12.7 s = 85 % | 80-95 % | PASS |
| title + end card | **2.3 s** | 4-8 s (short refs 4.6-6.0) | OUT (S3/M7) |
| integrated loudness | -15.1 LUFS | -20 to -12 | PASS |
| true peak | **-0.6 dBTP** | deliver <= -1 | FAIL (delivery rule) |
| loudness ramp | +3.2 LU | >= 0 (aim +2 to +6) | PASS |
| title vs body | **+1.3 LU** (title louder) | -6 to -20 LU in 16 of 21 | FAIL (M10): the music does not drop for the title |
| cuts on strong audio hits | **0 of 5** in the top 25 % of onsets | mid 21-42 % of cuts | OUT (M11): the music was laid under, not cut |
| onset percentile at cuts | 56 | >= 50 | PASS (by chance: no hit-aligned cut) |
| biggest audio hit | 2.66 s, music entry over the establishing shot | in the last 10-20 % for a short spot; at the payoff | OUT: the tsunami (10.2 s) and collapse get no hit |
| stop-down before title | none | 7 of 22 use one | optional |
| close shots | **0 %** | mid 0-7 %, but most trailers have one | FAIL (M12): no close shot of a face, the hand or the first falling block |
| sizes | 67 % wide, 33 % medium | mid 47-80 % wide | PASS |
| same size across cuts | 20 % | mid 54-92 % | info: fine; alternation is not required |
| captions | none | 11 of 22 | optional; the "hand pulls a block" rule is not readable without the hand |

What to change, in order: (1) choose a shape for 15 s: either one continuous take of the hand pulling
and the tower falling with the title over it, or 8-12 shots at about 1.6 s with a 4-cut barrage into
the title; (2) cut the collapse and tsunami on hits and put the biggest hit there, not on the opening;
(3) drop the music 6+ LU (or a short stop-down) for a 4-6 s title card; (4) add one close shot of the
first block sliding or a survivor reacting; (5) master to <= -1 dBTP.

---

## 9. Limits

- **Sample:** 22 trailers chosen for relevance and availability, not at random; YouTube blocked half
  the planned list, so Steam store trailers fill in and some are older or less celebrated cuts. Only
  4 short spots, none a montage: short-form rules are the weakest here. 3 "gameplay" trailers make that
  type's column anecdotal. Counts are descriptive, not proof of what makes a trailer succeed; no
  performance data (views, wishlists) was used.
- **Stills are not motion.** Camera moves were judged from three frames and a flow number; push vs
  slow zoom, orbit vs pan, and head motion vs a cut in POV footage are uncertain. Timing of action
  inside a shot (when the punch lands relative to the cut) was not measured.
- **Shot sizes and jobs are one person's labels** from 176x99 stills, with no second rater. Sizes are
  coarse; "job" is interpretive.
- **Cut detection** misses wipes, dissolves and whip-pans and is fooled by strobe and flicker; hand
  corrections were made where filmstrips showed problems, so misses elsewhere are a lower bound
  (labelled-set miss rate 8.3 %, review found 62 false and 39 missed of 794).
- **Music analysis cannot hear.** Beat tracking ran on the full mix (music + effects + voice) with one
  library; tempo snaps to a grid; two trackers agree but share method. "Not beat-locked" means "not
  locked to what librosa calls the beat"; it could hide cutting on phrases, on sound-design hits, or on
  musical accents the tracker misses. Downbeats are a crude guess. The onset results are more robust.
- **Loudness is of the platform files** (YouTube/Steam re-encodes), not the masters; true peak above
  0 dBTP in some files is codec overshoot.
- **What numbers cannot tell:** whether a joke lands, whether a hook is clear, whether the hand reads
  as a hand, whether the peril feels funny or cruel, or whether the music suits the game. Those still
  need a person watching and listening.
