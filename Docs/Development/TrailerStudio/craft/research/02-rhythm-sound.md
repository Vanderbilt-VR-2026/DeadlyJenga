# 02 — Editing rhythm, music and sound in trailers

Research for the Deadly Jenga trailer playbook. Audience: an AI editor that cannot hear, so every rule
here is written to be checked from numbers (beat times, loudness meters, cut times, waveforms).

Why this file exists: the first AI trailer had equal-length shots, music laid under the picture
afterwards, and sound out of sync. Each of those has a rule and a measurement below.

Conventions
- "Beat" = a beat from a beat tracker. "Downbeat" = beat 1 of a bar. "Bar" at 4/4 = 4 beats.
- At tempo T BPM: beat = 60/T s; bar = 240/T s. Example: 120 BPM gives a 0.5 s beat and a 2.0 s bar;
  100 BPM gives 0.6 s and 2.4 s.
- Frame lengths: 24 fps = 41.7 ms, 30 fps = 33.3 ms, 60 fps = 16.7 ms.
- **[S]** = stated by a source. **[I]** = my inference or a working number chosen for testability.
  Treat [I] thresholds as starting values to tune by review, not industry standards.

---

## 1. Rules

### A. Music comes first

1. **Pick and lock the music before cutting any picture.** Build the edit on the track, not the
   other way round. *Why:* music sets mood, pace and structure, and it tells you where cuts should land.
   [S] Lieu, "do audio first" (PC Gamer); Lieu, *Feelings Before Information*; Brandt (ProVideo
   Coalition): the traditional method builds the audio bed and dialogue first and fills in picture last.
2. **Use a track with a clear arc (beginning, build, peak, end), never a looping in-game track
   as is.** Test: the short-term loudness curve (EBU S, 3 s window) must rise by at least 6 LU from the
   first quarter to the loudest quarter, and must contain at least one dip and one peak [I].
   *Why:* looping game music has no arc for the trailer to ride. [S] Lieu (Premortem/Kwalee summaries;
   *Feelings Before Information*).
3. **Map the track before editing: mark sections, rises, fills, stop-downs and the biggest hit.**
   Lieu's colour system: green = steady/repeat, red = melodic or tempo change (new section),
   cyan = fill, yellow = rise, purple = big moment. The AI equivalent is the beat map in Procedure
   step 2. [S] Lieu, *How to Prepare Music for Editing*; No Film School (mark every beat, colour the
   first beat of every 4 bars).
4. **Edit the music in whole phrases (4 or 8 bars), joining just before a downbeat.** Never cut
   music mid-phrase. Use a short crossfade (about 5-30 ms [I]) only to hide a click; prefer joining at
   a transient. To end early, use a reverb-out (the music cuts on a downbeat and a reverb tail carries
   it) rather than a fade. [S] Lieu (reverb-out; music cutting "the hardest part for beginners");
   Frame.io / Videomaker / Resolve guides via search; SOS (fades of "a few milliseconds").
5. **Several cues are normal.** A professional trailer may join about four pieces of music to get the
   arc; join them at stop-downs, rises or matched tempos. [S] Lieu, *Elevating… Intermediate*;
   Lieu, Figment case study (matched tempos let cues flow).

### B. Cutting to the music

6. **Land the important cuts on beats, prefer downbeats for section changes, and land the biggest
   picture event on the biggest music hit.** Test: every section-change cut is within 1 frame of a
   downbeat; the trailer's biggest impact shot starts within 1 frame of the detected biggest hit [I].
   [S] Lieu ("I always try to make picture cuts land on the beat"); Violet Recording (riser peak exactly
   on the downbeat of the hit); Morphic (end of the riser exactly on the cut).
7. **Do not cut on every beat.** Mix cut types: some cuts on beats, some on action, and some
   syncs where a motion *inside* a shot (a block popping out, a survivor landing) hits the beat with
   no cut. Test: 40-75% of cuts within 1 frame of a beat; at least 3 in-shot action syncs per 30 s;
   never more than 4 consecutive cuts on consecutive beats except in one deliberate climax burst [I].
   *Why:* beat-every-beat cutting becomes predictable and "stale" quickly. [S] Lieu, *Thought Process*;
   *Elevating… Intermediate* (prefers cutting to actions); *Variety is the Spice*.
8. **Vary shot length; never let shot lengths go flat.** Test: coefficient of variation of shot
   durations (std / mean) at least 0.35 across the trailer; no run of more than 3 consecutive shots
   within ±2 frames of each other's length [I]. *Why:* equal-length shots make the trailer predictable.
   [S] Lieu, *Variety is the Spice* ("If every shot is the same length…"). Equal-length shots were the
   first AI attempt's main failure.
9. **Shorten shots through the build, then release.** Mean shot length should fall section by
   section into the peak (e.g. 3 s → 1.5 s → 0.7 s) and then lengthen for the breath and end card [I].
   [S] BEAT paper (rapid cuts on high-energy passages, sustained shots over quiet bars); Lieu, pacing
   (slow down after a climax so viewers breathe); Rowney/Gonzales (progression after each beat).
10. **Cut on action.** When cutting during a motion (a swing of the hand, a fall), cut mid-motion and
    overlap the action by 2-5 frames so motion reads continuous. [S] Dmytryk (3-5 frames), Anderson
    (~2), Pepperman (2-3) via Hecht & Kalkofen / Wikipedia.
11. **Nudge by feel, but within tight limits.** Editors sometimes place a cut or hit a frame or two
    off the waveform because it looks more in sync; vary this only within ±2 frames [S/I]. Sound for
    an on-screen impact must never lead the picture by more than 1 frame (people detect audio leading by
    about 45 ms, audio lagging by about 125 ms). [S] ITU-R BT.1359 detectability +45/−125 ms; EBU R37
    end-to-end +40/−60 ms; forum/editor practice via search.

### C. Pacing and contrast

12. **Build highs and lows on purpose; contrast matters more than tempo.** Test: the loudest 3 s
    short-term window is at least 10 LU above the quietest non-silent 3 s window (LRA roughly 6-14 LU) [I].
    *Why:* "lows don't look low unless there's a high"; wall-to-wall loudness has nothing to contrast.
    [S] Lieu (Premortem summary; *Variety is the Spice*); Violet Recording (wall-to-wall sound mistake);
    Krotos (contrast, avoid overcompression).
13. **Put a pause (stop-down) before the biggest hit.** Drop the music to near silence or a single
    sound for about 0.3-1.0 s (one beat to two bars) before the drop; momentary loudness should fall at
    least 15 dB below the preceding section during the gap [I]. [S] Violet Recording (drop-out just
    before the final hit); SOS (a beat of silence lets ears reset; Helm's Deep mix pull-back); Krotos
    (cut almost everything before the hit); Lieu (pausing before a peak makes it hit harder).
14. **Ration stop-downs.** Use them only where tension needs releasing. Lieu counts four in the
    2.5-minute *Steve Jobs* trailer (at about 19%, 35%, 58% and 92% of runtime) and four in *Half-Life:
    Alyx*. Scale down: at most 1 in a 15 s, 1-2 in 30 s, 2-4 in 60-150 s [I from S].
    [S] Lieu, *Tension and Trailer Stopdowns*; *Good Transitions*.
15. **Save the biggest moment for late.** Place the biggest hit at roughly 60-85% of runtime (50-70%
    for a 15 s, where the end card eats the tail), after
    the last build, followed by the logo/end card [I]. Do not peak early. [S] Violet Recording (peaking
    too early mistake; biggest impacts in final act); sizzle-reel advice (build to a big finish);
    Rareform (Act III climax).
16. **Use the groups-of-three rule for montages.** No more than three similar shots in a row; the
    third shot is different or transitions out. [S] Lieu, *How to Think About Pacing*.
17. **Hook inside the first 1-2 seconds, gameplay within 10 s, and make it work muted.** No black or
    silence at frame 0: picture with motion and audio above −35 LUFS momentary within the first 0.25 s [I].
    Steam may autoplay muted and you may have "less than 10 seconds"; its micro-trailer takes six 1-second
    clips from the first trailer, so every second must read. [S] Steamworks trailer docs; Game Developer
    *Salvage Your Game Trailer* (about 10 s to gameplay); Road to VR (Hellsweeper: ~10 s of combat
    before logos).

### D. Sound design and mix

18. **Layer trailer sound design in a small set of roles, each placed by rule:**
    - *Riser / suck-back*: starts 1-4 bars before a hit, peaks exactly on it.
    - *Hit / impact*: on the cut or on the in-shot impact; built from transient + body + sub.
    - *Sub drop*: under major hits only, not every hit.
    - *Whoosh*: on fast camera moves, whip transitions, logo and title reveals.
    - *Drone / tension bed*: under whole quiet sections; swells with the build.
    - *Braam*: one or two per trailer, as a section jolt or the climax.
    [S] Violet Recording; No Film School (hit, suckback, whoosh, riser, drone); Morphic; Lieu
    (*Good Transitions*: SFX rise starts over the last shot, ends on first beat of next); Netflix promo
    (every title gets a suck-back and a hit, Inside Professional Trailer Editing).
19. **Keep and feature the game's own sounds.** Recognisable game sounds sell the game and its
    genre; give one key game sound a clear moment by pulling the music out around it. For Deadly Jenga
    that means the block scrape/pop, tower creak, the hand's whoosh, the tsunami roar, survivor screams.
    [S] Lieu, *Targeting Your Audience With Sound Effects* (Half-Life: Alyx health sound right after a
    music drop-out); Witcher 4 trailer (reuse in-game assets); Battlefront trailer (test game SFX against
    music).
20. **Sound must serve the music; avoid clashes.** Time SFX to the beat, avoid tonal effects in a
    clashing key, carve EQ so subs and hits don't fight the score's low end. [S] Bryan Jerden
    (Designing Sound); Violet Recording; Lieu (*Intermediate*: effects must not step on music or
    dialogue).
21. **Duck music under any voice or key line by 6-10 dB, and briefly under big hits.** Music should
    sit lower whenever dialogue plays; side-chain the bed for a fraction of a second under each hit so
    the hit reads. [S] Lieu, *Elevating… Baseline* (music should "duck down"); Violet Recording; mixing
    guides (music 3-9 dB below dialogue). The 6-10 dB figure is [I].
22. **Restraint: subtle sound that supports the moment beats constant effects.** Reserve the deepest
    sub for key impacts and high-pass the rest. [S] Lieu, *Thought Process* (overused effects early in
    his career); Violet Recording (sub overload).
23. **During slow motion or a speed ramp, the sound changes with it.** Into slow motion: a descending
    whoosh or pitch-down that bottoms out at the slowest frame, music often filtered or dropped to a
    drone; back to speed: a rising reversed build that stops dead on the frame normal speed returns,
    usually landing on a downbeat [S: vendor practice, Morphic/Artlist; I: downbeat landing]. Never play
    slowed gameplay audio at its natural pitch over slowed picture without treatment [I].

### E. Transitions

24. **Default to hard cuts; let sound make the transition.** Sound is the foundation of transitions:
    stop-downs, reverb tails, SFX rises, impacts and whooshes carry section changes. [S] Lieu,
    *Good Transitions*.
25. **Use dips to black sparingly and pair them with a hit, sub or impact; white flashes with a hit or
    whoosh.** Cap at about 1 dip-to-black per 15 s of runtime outside the end card [I]. Overuse makes the
    pattern predictable. [S] Lieu, *Good Transitions* (Spider-Man 2 overuse example).
26. **Flash frames only on purpose, never by accident, and never more than 3 flashes in any 1 s.**
    Step through the export frame by frame for stray frames. [S] Lieu, *Baseline* (stray flash frames);
    ITU-R BT.1702 (more than three flashes in one second is potentially harmful).
27. **Flashy editing (flutter cuts, glitch, constant whips) is not better editing.** Game trailers
    must show readable play; flash can make a game look broken or seem to hide weak footage. [S] Lieu,
    *More Trailer Editing Is Not Better*; Premortem summary of Lieu's priorities.

### F. Mix delivery targets (measurable)

28. **Online master (YouTube, Steam, social, Meta store page): −14 LUFS integrated (±1 LU), true peak
    ≤ −1.0 dBTP, loudest short-term (3 s) ≤ −10 LUFS.** YouTube turns louder uploads down to about −14
    and (per Shepherd) does not turn quieter ones up, so a quiet master just plays quiet. [S] Production
    Advice (YouTube = −14 LUFS since 2019; master no louder than −10 LUFS short-term; TP ≤ −1 dBTP);
    Youlean/Sweetwater summaries; social platforms publish no spec, −14/−1 is consensus.
29. **Broadcast or festival deliveries use their own spec instead**: EBU R128 −23 LUFS, TP −1 dBTP;
    Netflix dialogue-gated −27 LKFS ±2, TP −2 dBTP. [S] EBU R128; Netflix Partner Help.
30. **Steam transcodes to stereo; deliver stereo 48 kHz AAC.** [S] Steamworks trailer docs.
31. **Fades:** audio edit points get 2-10 ms micro-fades against clicks; the opening must not fade
    from silence for more than 0.25 s [I]; the end lets the last hit's tail ring out (at least 1 s tail
    before the file ends, no truncated reverb) [I from S: SOS on letting sound ring out at the peak].

---

## 2. Procedure: music-first cutting for an editor that cannot hear

Each step lists the measurement and pass/fail check. Numbers marked [I] are working thresholds.

**Step 0 — Inputs.** Target length (15 s, 30 s, 60 s), platform (default: online, −14 LUFS), candidate
tracks (WAV/FLAC 48 kHz preferred), gameplay clips with their own game audio, and an event log of
gameplay moments (block pulled, tower sway, tsunami impact, survivor fall) with timestamps if the capture
tool provides them.

**Step 1 — Screen candidate tracks (reject before editing).** For each track:
- Tempo and beats: `librosa.beat.beat_track(y, sr, units='time')` or `aubio tempo`. Cross-check with a
  second tracker (madmom or All-In-One). PASS if the two tempos agree within 2% or at a 2:1 ratio
  (half/double time is common; choose the one whose beat interval is 0.4-0.75 s) [I].
- Loudness curve: `ffmpeg -i track.wav -af ebur128=peak=true:framelog=verbose -f null -` gives
  momentary (M, 400 ms), short-term (S, 3 s), integrated (I), LRA and true peak. PASS if the curve has
  a clear rise (≥ 6 LU from first quarter mean S to max S), at least one dip of ≥ 8 LU after a high
  section (a usable stop-down or one you can create), and LRA ≥ 5 LU [I].
- Structure: `allin1 track.wav` returns bpm, beats, downbeats, beat positions and labelled segments
  (intro, verse, chorus, break, bridge, inst, outro…). PASS if there is a section boundary within 15%
  of where the trailer's turn should fall, or the track can be cut in whole phrases to put one there.
- Reject tracks that are flat (LRA < 5 LU), loop-like (no section changes), or whose biggest hit is in
  the first third.

**Step 2 — Build the beat map (one JSON per track).** Fields and tools:
- `tempo_bpm`, `beats[]` (s): librosa `beat_track`, aubio `aubio beat`/`aubiotrack`, madmom
  `DBNBeatTrackingProcessor`.
- `downbeats[]`, `beat_in_bar[]`: madmom `RNNDownBeatProcessor` + `DBNDownBeatTrackingProcessor
  (beats_per_bar=[3,4], fps=100)` (output rows: time, position, 1 = downbeat), or All-In-One.
- `phrases[]`: every 4th downbeat (4-bar phrase) starting from the first labelled section start [I].
- `sections[]` {start, end, label}: All-In-One segments; fallback librosa structural segmentation
  (`librosa.segment` agglomerative / Laplacian on chroma+MFCC).
- `onsets[]` with strength: `librosa.onset.onset_detect(..., units='time', backtrack=True)` with
  `librosa.onset.onset_strength`; or `aubio onset -t 0.3` / `aubioonset -M 0.05`.
- `energy[]`: RMS (librosa `feature.rms`) every 10 ms, plus ffmpeg ebur128 M and S per 100 ms.
- `hits[]`: onsets where onset strength is in the top 5% and momentary loudness jumps ≥ 6 dB within
  100 ms [I]. Label each hit by its bar position.
- `biggest_hit`: the hit with the largest momentary-loudness rise from a preceding 0.25-2 s low
  window (this catches drop-after-stop-down rather than merely the loudest sustained part) [I].
  Confirm it lies on or within 1 beat of a downbeat; snap to that downbeat if within 60 ms.
- `stopdowns[]`: windows ≥ 0.25 s where M falls ≥ 15 dB below the previous 2 s mean, found with
  ffmpeg `silencedetect=noise=-40dB:d=0.25` plus the M curve, or `aubioquiet` [I].
- `rises[]`: windows of 1-8 bars where S increases monotonically by ≥ 6 LU and ends at a hit [I].
- Save a waveform image for a human check: `ffmpeg -i track.wav -filter_complex
  "showwavespic=s=3000x400:split_channels=1" wave.png`, and overlay beats/sections on it.
- Quality check: sonify the map (All-In-One `--sonify` / librosa `clicks`) so a human can confirm the
  grid once. The AI should instead check that beat intervals have standard deviation < 3% of the mean
  (a steady grid) [I]; if not, the track has tempo changes and needs per-section tempos.

**Step 3 — Edit the music to length first.** Choose the section order (e.g. intro → build →
stop-down → drop → end). Remove or repeat only whole 4- or 8-bar phrases; place every music join
2-10 ms before a downbeat transient; crossfade ≤ 30 ms. End with the full hit/sting and its tail, or a
reverb-out on a downbeat. PASS if the edited track length is within 1 frame of the target and all joins
fall within 10 ms before a downbeat. Re-run Step 2 on the edited track; the rendered beat map is the
one you cut against.

**Step 4 — Write the sync plan before choosing shots.** From the edited map, list the
**sync points**: (a) every section start (downbeat), (b) the stop-down start and end, (c) the biggest
hit, (d) the 2-6 next-strongest hits, (e) the logo/title hit. Assign one gameplay *event* to each
(block pops out, tower lurches, tsunami wave strikes, survivor falls, hand grabs). Count: 15 s ≈ 4-6
sync points; 30 s ≈ 7-12; 60 s ≈ 12-20 (about one every 2-4 s) [I]. The biggest hit gets the single most
dramatic gameplay event (e.g. the tower collapse or tsunami impact), never a generic shot.

**Step 5 — Assign shot slots.** Between sync points, fill with shots whose lengths follow the section
targets in Table 3.1. Mark each cut as one of: ON-BEAT (on a beat), ON-DOWNBEAT, ON-ACTION (cut point
chosen at a motion in the clip, overlap 2-5 frames), or HOLD (shot crosses beats with an in-shot event
synced to a beat). Run the variety checks: CV ≥ 0.35, 40-75% beat-locked cuts, no >3 similar-length
runs, no >3 similar-content shots in a row.

**Step 6 — Sync events inside shots.** For each shot with a gameplay event, offset the clip so that
the event frame (impact frame, landing frame, the frame the block leaves the tower) lands on its beat
or hit: event_frame_time = beat_time, tolerance ±1 frame. If the event is visible as motion, the cut
into the shot should come at least 0.3 s before the event so the viewer can read the setup [I].

**Step 7 — Lay sound design against the plan.**
- Game audio: keep the clip's own audio for featured events; trim game audio at each cut with 5-10 ms
  fades; level game SFX so key events read (see mix balance below).
- Risers end exactly on their hit (the riser file's peak sample within ±1 frame of the hit).
- Hits/impacts on the hit frame; sub drop only under the biggest hit and 1-2 others.
- Whooshes start 3-10 frames before a whip/camera move cut and peak on it [I].
- Stop-down: mute or filter music, leave one sound (a creak, a breath, a single block tap).
- Verify by measuring: run onset detection on the *final mix* and on the *video cut list*; for every
  planned sync point, a mix onset must exist within ±1 frame (audio not more than 1 frame early) of the
  picture event.

**Step 8 — Mix and measure (pass/fail).**
- Integrated: −14 LUFS ±1 (online). Measure with `ffmpeg -af ebur128=peak=true` or `loudnorm` in
  analysis mode (`loudnorm=I=-14:TP=-1:LRA=11:print_format=json`), then apply a two-pass loudnorm with the
  measured_* values and `linear=true` so dynamics are kept. FAIL if loudnorm falls back to dynamic mode
  (it does so when the requested gain would breach TP); reduce peaks by limiting the loudest hits first.
- True peak ≤ −1.0 dBTP. Max short-term ≤ −10 LUFS. LRA 6-14 LU [I].
- Contrast: loudest 3 s S minus quietest non-silent 3 s S ≥ 10 LU [I].
- Stop-down depth: momentary dips ≥ 15 dB below the previous bar for 0.25-1.0 s [I].
- Music vs voice: in windows with dialogue/VO, music stem short-term ≥ 6 dB below the voice stem [I].
- Music vs featured game SFX: at a featured game sound, music stem momentary ≥ 3 dB below the SFX
  stem for the event's 0.2-0.5 s, or the music pulled out entirely [I].
- Start: no `silencedetect` silence beginning at 0; ffmpeg `blackdetect=d=0.1:pix_th=0.1` finds no black
  at 0 (unless the cold open is a deliberate black frame with sound, max 0.5 s) [I].
- End: final hit's tail decays naturally (last 0.5 s momentary below −40 LUFS, no truncation click)
  [I]. Clipping: `astats` reports no samples at 0 dBFS.

**Step 9 — Verify the cut against the beat map after render.** Detect actual cuts in the rendered
video (`ffmpeg -vf "select='gt(scene,0.3)',showinfo"` or PySceneDetect). Compute each cut's offset to the
nearest beat and to its planned slot. FAIL if any planned sync cut is off by more than 1 frame, if the
shot-length CV < 0.35, or if the biggest hit and the biggest picture event differ by more than 1 frame.
Also check flashes: no more than 3 large luminance flashes in any 1 s window.

**Step 10 — Muted and phone checks.** Watch the frames with audio off: hook readable in the first 2 s,
the premise clear without sound (Steam/feeds autoplay muted). Phone check for sound: hits must still
show as onsets in a version high-passed at 150 Hz (subs vanish on phone speakers) [S: Violet Recording;
I: 150 Hz].

---

## 3. Timing tables

### 3.1 Shot-length guidance

| Section | Typical mean shot length | Range | Notes |
|---|---|---|---|
| Cold open / hook (0-3 s) | 1.5-3 s | 1-4 s | One strong, readable gameplay moment; can be a single held shot [I] |
| Setup / intro | 2.5-4 s | 2-6 s | Long enough to read the rules (who climbs, what pulls). Lieu measured ASL 4.25 s for *Baba Is You*, 2.6 s for *Papers, Please* [S] |
| Build / escalation | 1.2-2 s | 0.8-3 s | Shortens bar by bar into the riser [I] |
| Climax burst | 0.5-1 s | 0.33-1.5 s | Accent shots can be 8-12 frames if they read as a single image; Lieu measured 1.3 s for *Modern Warfare II* MP and 0.82 s for a *Fast X* trailer [S] |
| Stop-down | 0.3-1 s of near-silence, often over one held shot or black | 1 beat-2 bars | One shot, no cut during the silence [I] |
| Biggest hit shot | 1.5-3 s | 1-4 s | Hold the payoff long enough to see it land (collapse, wave impact) [I] |
| Breath / end card | 2-4 s | 2-5 s | Logo on a hit; tail rings out [I] |

Other limits [I]: minimum readable *gameplay* shot (a player action plus its result) is about 1.0 s at
normal speed; below 0.5 s a shot is an accent, not information. A shot with on-screen text needs at least
about 0.3 s per word plus 0.5 s. Cross-check: Lieu found no "right" ASL; game trailers usually cut slower
than movie trailers because they must show interaction [S].

### 3.2 Example cut list — 15 s at 120 BPM (beat 0.5 s, bar 2.0 s; 7.5 bars)

Track plan: 1 bar hook, 2 bars build with riser, ½ bar stop-down, 2 bars drop, 2 bars end card with sting.

| # | In (s) | Out (s) | Len | Cut type | Music | Picture (Deadly Jenga) |
|---|---|---|---|---|---|---|
| 1 | 0.00 | 2.00 | 2.00 | start, hook | Bar 1: hit on frame 0 | Survivor clinging as a block slides out beside them; block leaves on beat 3 (1.0 s) |
| 2 | 2.00 | 3.50 | 1.50 | downbeat | Build begins | Giant hand swoops in (whoosh peaks at 2.0) |
| 3 | 3.50 | 4.50 | 1.00 | on-action | Build | Hand pulls a block; pop synced to beat at 4.0 |
| 4 | 4.50 | 5.25 | 0.75 | on-beat | Riser 4.0 → 7.5 | Tower sways, survivors stagger |
| 5 | 5.25 | 6.00 | 0.75 | on-beat | Riser | Tsunami wave rising on horizon |
| 6 | 6.00 | 8.00 | 2.00 | downbeat | Riser peaks 7.5; stop-down 7.5-8.0 (one beat): music out, only a creak | Close shot: survivor looks up; held, no cut |
| 7 | 8.00 | 10.00 | 2.00 | **biggest hit** on downbeat 8.0 | Drop + sub | Wave strikes the tower; survivors fly off (impact frame on 8.0) |
| 8 | 10.00 | 11.00 | 1.00 | downbeat / on-action | Back-end | Survivor falls past camera into ocean (splash on beat 10.5) |
| 9 | 11.00 | 11.50 | 0.50 | on-beat | Back-end | Accent: hand flicks a survivor off |
| 10 | 11.50 | 12.00 | 0.50 | on-beat | Back-end | Accent: a block tumbles into the water |
| 11 | 12.00 | 15.00 | 3.00 | downbeat | Logo hit at 12.0, sting tail ~3 s | Title over the wrecked tower; sting rings out |

Checks: lengths 2.0, 1.5, 1.0, 0.75, 0.75, 2.0, 2.0, 1.0, 0.5, 0.5, 3.0 → mean 1.36 s, CV ≈ 0.56 (pass);
no run of more than 2 equal lengths. 1 stop-down; biggest hit at 53% of runtime (acceptable for a 15 s,
where the end card takes the final 20%; for 30 s+ aim later). Sync points: 0.0, 2.0, 4.0, 7.5, 8.0, 10.5,
12.0 = 7 (top of the range). With a real track, the stop-down and drop must fall on detected downbeats:
shift the whole plan to the actual map rather than forcing these numbers.

### 3.3 Example cut list — 30 s at 100 BPM (beat 0.6 s, bar 2.4 s; 12.5 bars)

Track plan: cold open 1 bar → intro 3 bars → build 3½ bars (riser in the last 1½) → stop-down ½ bar
(music drops out on beat 3, so the hit still lands on a downbeat) → drop/back-end 3 bars → end card 1½ bars.

| Section | Time (s) | Bars | Shots (lengths, s) | Mean len | Sync points |
|---|---|---|---|---|---|
| Cold open | 0.0-2.4 | 1 | 1 (2.4) | 2.4 | hit on 0.0; block slips on beat 3 (1.2) |
| Intro (rules) | 2.4-9.6 | 3 | 3 (3.0, 2.4, 1.8) | 2.4 | 2.4 downbeat; survivor climb landing on 7.2 |
| Build | 9.6-18.0 | 3½ | 7 (1.8, 1.2, 1.8, 1.2, 1.2, 0.6, 0.6) | 1.2 | 9.6 downbeat; hand-pull pop on 12.0; tower lurch 14.4; riser 14.4→18.0 |
| Stop-down | 18.0-19.2 | ½ | 1 held (1.2) | 1.2 (near-silence ≈1.0) | music out at 18.0; one sound (survivor gasp, tower creak) |
| Drop / back-end | 19.2-26.4 | 3 | 6 (2.4, 0.6, 0.6, 1.2, 1.2, 1.2) | 1.2 | **biggest hit on downbeat 19.2** = tsunami impact; falls synced to 21.6 and 22.8 |
| End card | 26.4-30.0 | 1½ | 1 (3.6) | 3.6 | logo hit on downbeat 26.4; tail to 30.0 |

Totals: 19 shots, mean ≈1.6 s, CV ≈ 0.55. Biggest hit at 64% of runtime (edge of the 60-85% window).
Sync points ≈ 10. Dips to black: at most 1 (into the end card). Stop-downs: 1. A second cue is optional:
the intro can use a calmer cue that hard-stops at 9.6 into the trailer cue, if both tempos match within 3%.

Generic templates by length [I, derived from Lieu's section timings, Steve Jobs stop-down positions and
library-cue act lengths of roughly 40/40/30 s in 2-3 min cues]:
- 15 s: hook 0-15% · build 15-45% · stop 45-50% · peak 50-75% · end card 75-100%.
- 30 s: hook 0-8% · setup 8-32% · build 32-60% · stop 60-64% · peak/back-end 64-88% · end card 88-100%.
- 60 s: cold open 0-10% · logo/intro 10-30% · build A 30-50% (stop-down at ~50%) · build B 50-72% ·
  stop 72-75% · climax 75-90% · end card 90-100%.

---

## 4. Mistakes (symptom → cause → fix)

| Symptom (how to detect from numbers) | Cause | Fix |
|---|---|---|
| Shot lengths nearly identical (CV < 0.2) | Shots placed on a fixed grid, not the music | Plan from sync points; shorten through the build; hold payoffs (Rules 8-9) |
| Cuts uncorrelated with beats (< 25% within 1 frame of a beat) | Music laid under picture afterwards | Lock and map music first; re-cut against the beat map (Rules 1, 6) |
| Every cut on a beat (> 90%) | Mechanical beat snapping | Mix in on-action cuts and in-shot syncs; keep beat cuts for structure (Rule 7) |
| Impact sound 3+ frames off its picture event | SFX placed by eye or on clip start, or audio offset from encoding | Align event frame to sound onset, ±1 frame; re-measure after render (Rule 11, Step 9) |
| Flat loudness curve (LRA < 5 LU) | Wall-to-wall music and SFX, heavy limiting | Create lows: stop-down, thinner intro, drone sections; limit less (Rules 12-13) |
| Biggest hit early (< 50% of a 30 s+) | Best shot used first | Move biggest event to the late hit; open with a different strong hook (Rule 15) |
| Riser ends before or after the hit | Riser placed by start time | Align riser peak to hit, ±1 frame (Rule 18) |
| Music ends with a fade or chopped mid-phrase | Music trimmed to picture length | Edit music in phrases first; end on sting or reverb-out (Rules 4, Step 3) |
| Clicks at audio edits | No micro-fades | 2-10 ms fades on every edit (Rule 31) |
| First 0.5 s black or silent | Fade-in from black, slate, logo first | Start on a gameplay moment and a sound at frame 0 (Rule 17) |
| Integrated −8 LUFS, TP > 0 dBTP | Mastered "loud" | Target −14 LUFS, TP ≤ −1, max S ≤ −10; platforms turn loud masters down (Rule 28) |
| Integrated −20 LUFS online | Quiet mix assumes the platform will raise it | YouTube does not raise quiet audio (per Shepherd); normalise to −14 (Rule 28) |
| Voice/key line masked | Music not ducked | Duck music 6-10 dB under voice (Rule 21) |
| Game sounds lost under music | Trailer treated as music video only | Pull music out at key game sounds; feature 1-3 signature sounds (Rule 19) |
| Too many dips to black / flashes | Transitions used as rhythm instead of sound | Hard cuts + sound transitions; ≤ 1 dip per 15 s; ≤ 3 flashes/s (Rules 24-26) |
| Stray 1-3 frame clips between shots | Overwrite leftovers | Frame-step the export; scene detect any shot < 4 frames and confirm it is intentional (Rule 26) |
| Montage of 5 similar shots | Too much of one idea | Groups of three; third shot different (Rule 16) |
| Fast cutting hides what the game is | Copying movie trailer pace | Hold gameplay ≥ 1 s per readable action; flash ≠ quality (Rule 27) |
| Slow-mo with normal-pitch audio | Speed change applied to picture only | Pitch-down/whoosh into slow-mo, rising build out, land on downbeat (Rule 23) |
| Sub everywhere, mud on phone | Sub layer on every hit | Sub only on 1-3 hits; high-pass the rest; check a 150 Hz high-passed copy (Rule 22) |

---

## 5. Sources

| # | Title | Author / outlet | URL | What I took |
|---|---|---|---|---|
| 1 | How to Prepare Music For Editing | Derek Lieu | https://www.derek-lieu.com/blog/2017/6/20/how-to-prepare-music-for-editing | Colour marker system for music structure; music editing as the mark of experience |
| 2 | Tension and Trailer Stopdowns | Derek Lieu | https://www.derek-lieu.com/blog/2020/1/10/tension-and-trailer-stopdowns | Stop-down definition, 4 stop-downs in *Steve Jobs* at 0:29/0:53/1:27/2:18, 4 in *Half-Life: Alyx* |
| 3 | Good Transitions Make Great Trailers | Derek Lieu | https://www.derek-lieu.com/blog/2018/2/24/good-transitions-make-great-trailers | Stop-down on a downbeat with SFX; SFX rise ends on next shot's first beat; black/white cuts with hits; overuse warning; sound as foundation |
| 4 | How to Think About Pacing in a Trailer | Derek Lieu | https://www.derek-lieu.com/blog/2020/11/16/how-to-think-about-pacing-in-a-trailer | Section-length breakdown (Spiritfarer), groups of three, breath after climax, keep under 90 s |
| 5 | My Trailer Editing Thought Process | Derek Lieu | https://www.derek-lieu.com/blog/2018/1/6/what-im-thinking-when-i-edit-a-trailer | Cuts on beats, Mickey-Mousing motion to music, don't cut every beat, subtle SFX |
| 6 | Elevating Your Trailer Editing – Intermediate | Derek Lieu | https://www.derek-lieu.com/blog/2021/1/10/elevating-your-trailer-editing-intermediate | Prefers cutting to actions over literal beats; ~4 cues per trailer; SFX not stepping on music |
| 7 | Elevating Your Trailer Editing – Baseline | Derek Lieu | https://derek-lieu-yrd2.squarespace.com/blog/2020/7/14/elevating-your-editing-baseline | Duck music under dialogue; remove stray flash frames |
| 8 | Variety is the Spice of Trailers | Derek Lieu | https://www.derek-lieu.com/blog/2019/3/27/variety-is-the-spice-of-trailers | Vary content/composition/energy/pace/shot length; same-length shots are predictable; contrast > tempo |
| 9 | Targeting Your Audience With the Trailer's Sound Effects | Derek Lieu | https://www.derek-lieu.com/blog/2024/1/7/targeting-your-audience-with-the-trailers-sound-effects-ba73f | Use recognisable game sounds; pull music out so one SFX lands (Half-Life: Alyx) |
| 10 | Why Trailers Should Convey Feelings Before Information | Derek Lieu | https://www.derek-lieu.com/blog/2022/2/14/why-trailers-should-convey-feelings-before-information | Music arc guides cut timing; game BGM often unfit; sound sets genre and mood |
| 11 | More Trailer Editing Is Not Better Trailer Editing | Derek Lieu | https://derek-lieu-yrd2.squarespace.com/blog/2023/4/23/more-trailer-editing-is-not-better-trailer-editing | Measured ASLs (Baba Is You 4.25 s, Papers Please 2.6 s, MWII 1.3 s, Fast X 0.82 s); no ideal ASL |
| 12 | Figment Launch Trailer case study | Derek Lieu | https://derek-lieu-yrd2.squarespace.com/blog/2017/8/30/figment-launch-trailer | Sorting cues by pace/feel; joining cues of matching tempo |
| 13 | The art of making effective game trailers | Alex Wiltshire, PC Gamer | https://www.pcgamer.com/the-art-of-making-effective-game-trailers | "do audio first" (via search excerpts; page body was not readable) |
| 14 | Making great game trailers with Derek Lieu / Premortem summary | Medium; Premortem Games | https://premortem.games/?p=2201 | Highs and lows; stop-downs and flourishes come after fundamentals (search excerpts) |
| 15 | Inside Professional Trailer Editing | Jonny Elwyn (Rowney, Gonzales, Malatesta, Netflix promo, Macdonald) | https://jonnyelwyn.co.uk/film-and-video-editing/inside-professional-trailer-editing/ | Progression after each info beat; suck-back + hit on every title; telegraphing a sound a measure early |
| 16 | Art of the Cut with trailer editor (Brandt) | Steve Hullfish, ProVideo Coalition | https://www.provideocoalition.com/art-cut-trailer-editor/ | Audio bed and dialogue first, picture last; silence as "theater of the mind"; line over black |
| 17 | Meet the People Who Grind Out the Best Movie Trailers | The Ringer | https://www.theringer.com/2018/07/23/movies/movie-trailer-editors-marvel-pixar-how-made | Music/sound as "the heart"; notes like music "hits too late"; 2.5 min to 15 s versions |
| 18 | How to Edit Trailer Music Like a Hollywood Pro | No Film School | https://nofilmschool.com/how-to-edit-trailer-music | Mark every beat, colour every 4th bar; hit/suckback/whoosh/riser/drone roles; stems |
| 19 | Sound Design for Trailers | Violet Recording | https://violetrecording.com/sound-design-for-trailers/ | Layer roles, riser peak on downbeat, drop-out before final hit, side-chain under hits, mistakes list |
| 20 | Exclusive interview: Bryan Jerden on trailer sound design | Designing Sound | https://designingsound.org/2012/07/19/exclusive-interview-bryan-jerden-on-trailer-sound-design/ | Everything serves the music; SFX on the beat; avoid key clashes; planned silence; TASA/LFE note |
| 21 | Sound design for visual media | Sound On Sound | https://www.soundonsound.com/techniques/sound-design-visual-media-radio | Beat of silence before impact; 2-3 frames drift looks sloppy; few-ms fades; 5-layer explosion |
| 22 | 5 Tips for Designing Trailer Sound Effects | Krotos | https://krotos.studio/blog/trailer-sound-design-tips-tricks | Cut almost everything before the hit; contrast over loudness; drop level after impact |
| 23 | All About Library Music, Part 7 | Sound On Sound | https://www.soundonsound.com/music-business/all-about-library-music-part-7 | Three-act trailer cue; gaps between sections as edit points; endings that don't resolve |
| 24 | Making a living from music for picture, Part 7 | Sound On Sound | https://www.soundonsound.com/techniques/making-living-music-picture-part-7 | 60 s trailer hit points (~24 s, ~38 s, ~56 s peak); landing changes on downbeats; let sound ring out |
| 25 | Trailer Music Structure | Rareform Audio | https://www.rareformaudio.com/blog/how-production-music-reveals-trailer-structure | Act I minimal, Act II build, Act III climax with big hits |
| 26 | Learn How to Make High-Impact Sounds for Movies and Trailers | Ableton | https://www.ableton.com/en/blog/learn-how-to-make-high-impact-sounds-for-movies-and-trailers/ | Braam/hit construction; pitch-down layering (via search excerpt) |
| 27 | Movie trailer / slow-motion sound pages | Morphic | https://morphic.com/resources/sounds/movie-trailer-sound-effects | Riser before cut, braam on cut; descending whoosh into slow-mo (vendor practice) |
| 28 | Rule of Six / blink | PremiumBeat; No Film School (on Murch, *In the Blink of an Eye*) | https://www.premiumbeat.com/blog/cutting-on-the-blink-editing-tips-from-walter-murch | Emotion 51, story 23, rhythm 10, eye-trace 7, 2D 5, 3D 4; cuts near natural blinks |
| 29 | When and Where to Make the Cut | PremiumBeat | https://www.premiumbeat.com/blog/when-and-where-to-make-the-cut-inspired-by-walter-murchs-in-the-blink-of-an-eye/ | Blinks as punctuation; "one cut per line" is rhythmically flat |
| 30 | Cutting on action (Dmytryk, Anderson, Pepperman; Hecht & Kalkofen) | Wikipedia; Univ. Mainz paper | https://en.wikipedia.org/wiki/Cutting_on_action | 2-5 frame action overlap |
| 31 | Rec. ITU-R BT.1359 / Audio-to-video synchronization | ITU; Wikipedia | https://en.wikipedia.org/wiki/Audio-to-video_synchronization | Detectability +45/−125 ms; EBU R37 +40/−60 ms |
| 32 | Rec. ITU-R BT.1702 | ITU | https://www.itu.int/rec/R-REC-BT.1702-3-202311-I | More than 3 flashes in 1 s potentially harmful |
| 33 | YouTube loudness / How loud? | Ian Shepherd, Production Advice | https://productionadvice.co.uk/youtube-loudness/ ; https://productionadvice.co.uk/how-loud/ | YouTube −14 LUFS since 2019; max −10 LUFS short-term; TP ≤ −1; YouTube doesn't raise quiet masters |
| 34 | Netflix Sound Mix Specifications / Loudness and True Peaks | Netflix Partner Help | https://backlothelp.netflix.com/hc/en-us/articles/360001794307 | −27 LKFS dialogue-gated ±2, −2 dBTP |
| 35 | EBU R 128 | EBU / Wikipedia | https://en.wikipedia.org/wiki/EBU_R_128 | −23 LUFS, −1 dBTP; LRA descriptive not prescriptive |
| 36 | Steamworks: Trailers | Valve | https://partner.steamgames.com/doc/store/trailer | Muted viewing; < 10 s to impress; micro-trailer = six 1 s clips; stereo transcode; 44/48 kHz AAC |
| 37 | Meta Quest store trailer requirements (VRC) | Meta | https://developers.meta.com/horizon/resources/vrc-quest-asset-7/ | 16:9, 30 s-2 min; no audio spec found |
| 38 | Salvage Your Game Trailer | Game Developer | https://gamedeveloper.com/business/salvage-your-game-trailer | ~10 s to gameplay; sound design makes edits feel intentional |
| 39 | VR Design Unpacked: The Formula for Great VR Game Trailers | Road to VR | https://roadtovr.com/inside-xr-design-make-great-vr-trailer-without-mixed-reality-capture/ | Open on the hook (~10 s combat before logos); choreograph moments |
| 40 | BEAT (automated trailer editing) | arXiv 2605.27067 | https://arxiv.org/abs/2605.27067 | Elastic alignment: fast cuts in high-energy bars, long shots over quiet bars |
| 41 | All-In-One music structure analyzer | mir-aidj (GitHub) | https://github.com/mir-aidj/all-in-one | Outputs bpm, beats, downbeats, beat positions, labelled segments; MIT |
| 42 | aubio command-line tools | aubio | https://aubio.org/manual/latest/cli.html | tempo, beat, onset (method, threshold, min IOI), aubioquiet, aubiocut |
| 43 | madmom downbeats module | madmom | https://madmom.readthedocs.io/en/latest/modules/features/downbeats.html | RNN + DBN downbeat tracking (page body thin; API from general knowledge, verify) |
| 44 | librosa docs | librosa | https://librosa.org/doc/latest/ | beat_track, onset_detect/backtrack, feature.rms, segment (docs pages returned errors; from knowledge, verify) |
| 45 | FFmpeg filters | FFmpeg | https://ffmpeg.org/ffmpeg-filters.html | ebur128, loudnorm, silencedetect, astats, showwavespic, blackdetect, scene select (from knowledge; page too long to read fully) |
| 46 | Stephen Follows trailer length data; Temple thesis on trailer ASL | Follows; Temple ScholarShare | https://stephenfollows.com/p/have-movie-trailers-always-been-this-long | Average trailer ≈123 s; trailer ASL declining over 60 years (no figures retrieved) |

---

## 6. Uncertain or disputed

- **Cut exactly on the beat vs slightly early/late.** No controlled study found. Editors report nudging
  1-2 frames by feel; one vendor puts the cut on the frame after a beat; others cut "slightly early" to
  anticipate. This file uses ±1 frame for sync points and allows ±2 for feel. Perception research (BT.1359)
  says audio leading picture is noticed sooner than audio lagging, which supports "never let the sound
  arrive early".
- **On-beat vs on-action.** Lieu says he tries to land cuts on beats but also prefers cutting to actions
  over literal beats. These are compatible (sync the action to the beat), but the 40-75% beat-locked
  window is my inference, not his number.
- **Number of sync points per trailer.** No source gives a count. The 4-6 / 7-12 / 12-20 numbers are
  inferences from one sync every 2-4 s.
- **Shot length tables.** Only Lieu's handful of measured ASLs are hard data. Section-by-section ranges,
  the 1.0 s minimum readable gameplay shot and the CV ≥ 0.35 test are inferences.
- **Placement of the biggest hit (60-85%).** Inferred from Steve Jobs stop-down positions, library cue act
  lengths (~40/40/30 s, a forum answer) and the "don't peak early" advice; not a stated standard.
- **YouTube normalisation.** Production Advice says −14 LUFS and no upward gain for quiet audio; some
  secondary sources claim YouTube raises quiet audio or targets −12 to −15. YouTube publishes no spec.
  TikTok/Instagram/Meta publish none either; −14 LUFS / −1 dBTP is community consensus, with outliers
  claiming −10 to −12 for TikTok.
- **True-peak ceiling.** −1 dBTP is common; Netflix uses −2; some engineers use −0.3 for lossless use.
  For lossy online delivery keep −1 or lower.
- **Short-term maximum.** Shepherd's −10 LUFS short-term is one engineer's guidance, not a platform rule;
  EBU R128 s1 (short-form) parameters could not be read and were not verified.
- **"Back end", "button", "stop-down" terminology.** "Stop-down" is well documented (Lieu). "Back end"
  (the post-drop final section) and "button" (the final punctuating hit/line) are used informally; no
  authoritative definition found.
- **Murch's Rule of Six.** Percentages are Murch's own half-joking weights; a critic argues eye-trace
  matters more in fast-cut content. Use it as a priority order, not numbers.
- **Tool APIs.** librosa, madmom and FFmpeg doc pages could not be fetched in full during this research;
  the function names and parameters given are from general knowledge and must be checked against the
  installed versions before scripting. aubio and All-In-One outputs were verified from their docs.
- **Mix balance numbers** (music 6-10 dB under voice, ≥ 3 dB under featured SFX, ≥ 10 LU contrast,
  ≥ 15 dB stop-down depth, 150 Hz phone check) are inferences from qualitative advice; sources give ranges
  for dialogue/music levels that disagree by several dB.
- **Not found:** a trailer-specific loudness measurement of published game trailers; a Meta Quest store
  trailer audio spec; Derek Lieu's GDC talks (GDC Vault pages are gated); Kert Gartner's own write-up of
  his music process; the Temple thesis's numeric trailer ASLs; a Tunefind "trailerization" series and a
  Filmmaker Magazine trailer article (both returned 403).
