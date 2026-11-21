# Rhythm and sound for an editor that cannot hear

Open at stage 2 (music map), when writing the `sound` lists in the plan, and at stage 10 (mix).
Sources: report 02 (Derek Lieu's blog, Violet Recording, Sound On Sound, Krotos, No Film School,
Ian Shepherd, ITU and EBU specs, Steamworks) checked against report 06's measurements. Every check
here is a number, because you cannot listen. A person still listens before the trailer is finished.

## 1. What the measurements changed

- **Cuts are not gridded to beats.** Across 599 reference cuts, 25.0 % land within 2 frames of a
  tracked beat, the same as chance (24.9 %). [measured]
- **Cuts lean toward hits.** 40.9 % land within 2 frames of an audio onset (36.5 % chance); in fast
  passages (shots under 0.8 s) 12.8 % land on strong onsets (6.7 % chance). TABS' final duel barrage
  puts six cuts within 0-3 frames of a beat or strong onset. [measured]
- **So plan sync points, not a grid.** Put the payoff, the title and the barrage on hits; let the
  rest of the cuts follow the action. Lieu: he tries to land cuts on beats but prefers cutting to
  actions; beat-every-beat cutting goes stale. [sourced]
- **The music builds, then drops for the title.** The last quarter of the body is louder than the
  first in 16 of 20 (median +3.7 LU); the title is ≥ 6 LU quieter than the body in 16 of 21 (median
  -8.9 LU). Short spots dropped less (median -2.9 LU, n=4). [measured]

## 2. Procedure

**Step 0. Inputs.** Target length and class, platform, candidate tracks (WAV 48 kHz), the take's
own sound stems, and the take report's event times. To make a new cue or stems for the game, use the
`deadlyjenga-audio-production` skill.

**Step 0b. No music file yet (planning-only mode).** Do not fake stage 2. Write **target hits**:
each hit's `frame` with `measured_onset_frame: unmeasured`, chosen from the picture plan (open at 0,
the verb or reveal, the main hit on the payoff in the last 40 % of the body, the title hit at the
closing title). Planning on a target tempo grid is fine if it is labelled a target. Add a music
brief: tempo range, character, where the main hit, the stop-down and the title hit fall, and the
end (a sting with a tail). When a track exists, run steps 1-3, then move the *picture* to the
measured hits (never the hits to the picture), re-run L11, and re-run the stage-4 review if any key
moved more than 6 frames. Every frame-level sound rule (L12, R03) is checked against the targets
now and against the measured mix later.

**Step 1. Screen tracks** (reject before editing).
- Tempo and beats with two trackers (librosa and aubio, or madmom); they agree within 2 % or at a 2:1
  ratio; choose the reading whose beat lasts 0.4-0.75 s. [inferred thresholds]
- Loudness curve with ffmpeg ebur128 (commands in section 6). Reject a track that is flat (LRA under
  5 LU), loop-like (no section change), or whose biggest hit falls in its first third. Never use a
  looping in-game track as is. [sourced: Lieu]
- Structure (All-In-One, or librosa segmentation): there is a section change near where the trailer
  turns, or the track can be cut in whole phrases to put one there.

**Step 2. Build the beat map** (`music.yaml`; `tools/music_map.py` writes most of it, though not
downbeats, sections or rises): `bpm`, `beats[]`, `downbeats[]`, `sections[]`,
`onsets[]` with strength, `energy[]` (RMS every 10 ms plus EBU momentary and short-term every
100 ms), `hits[]` (onsets in the top 5 % of strength with a momentary jump of 6 dB or more within
100 ms), `biggest_hit` (largest momentary rise from a 0.25-2 s low before it), `stopdowns[]` (0.25 s
or more where momentary loudness falls 15 dB below the previous 2 s), `rises[]`. Check the beat grid
is steady (interval std under 3 % of the mean); if not, keep per-section tempos. Save a waveform
image with beats and sections drawn on it for the human pass.

**Step 3. Edit the music to length first.** Cut and repeat only whole 4- or 8-bar phrases; place every
join 2-10 ms before a downbeat transient, crossfade at most 30 ms; end on a sting with its tail or a
reverb-out on a downbeat, never a fade-out over a chopped phrase. The edited track must match the
target length within 1 frame. Re-run step 2 on the edited file: that is the map you cut against.
[sourced: Lieu on phrase edits and reverb-outs; ms values from Sound On Sound]

**Step 4. Write the sync plan before choosing shots.** Name the hits the plan uses:

| Role | 15 s | 30 s | 60 s | Gets |
|------|------|------|------|------|
| `open` | at frame 0 | at frame 0 | at frame 0 or a cold-open hit | the first gameplay frame |
| `reveal` | 0-1 | 0-1 | 1-2 | the first full view of the threat, or the core verb (in a cold open, `open` already is the reveal) |
| `accent` | 0-1 | 2-4 | 4-9 | a fall, a pull; barrage cuts may use designed SFX onsets instead |
| `main` | 1 | 1 | 1 | the payoff's key moment (hard rule 7), in the last 40 % of the body |
| `title` | 1 | 1 | 1 | the closing title's first frame (hard rule 7) |
| Total | 3-5 | 5-8 | 8-14 | |

The main hit gets the single most dramatic event (the crown toppling, the wave striking), never a
generic shot. If the track's biggest hit is in the wrong place, re-edit the music (step 3), do not
move the payoff off it. [sourced; counts inferred]

**Step 5. Assign shot slots** between sync points, using the shot-length guidance in section 4 and
the shape rules (hard rule 3). Mark each cut `on_hit`, `on_action` (cut during a motion, with 2-5
frames of action overlap), or `free`. Keep beat-snapped cuts under 80 % (soft lint L24).

**Step 6. Sync events inside shots.** Offset the clip (`tape_in`) so that the event frame (block
leaves, crown breaks, survivor lands) falls on its hit within 1 frame. Cut into an event shot at least
0.3 s before the event so the setup reads; the exception is an impact on the cut. [inferred]

**Step 7. Lay sound design against the plan** (section 3). Then measure: every planned sync point
must have an onset within 2 frames of its picture event, never more than 1 frame before it (audio
leading picture is noticed at about 45 ms; lagging at about 125 ms). [sourced: ITU-R BT.1359]
**Measure each sound lane alone, not only the full mix** (`tools/assemble.py` prints both columns):
an onset detector on the full mix picks the strongest change near a frame, and a voice line or
any sound with a soft first syllable can read many frames late under music (hand-reveal-2: "What
was that?" measured +17.3 frames though its first syllable was placed at 0.03 s; the dry run of the
tool: a soft attack read +9.4 frames in the mix and -0.5 in its own lane). Trust the lane column,
and list every voice line as **needs ears** for the human pass whatever the numbers say. [sourced:
hand-reveal-2 REPORT]

**Step 8. Mix and measure** (targets in section 5).

**Step 9. Verify the render against the plan.** Detect cuts in the rendered file; each within 1 frame
of plan (R03). Rebuild the sync table from the final mix. Check flashes: no more than 3 large
luminance flashes in any 1 s (ITU-R BT.1702).

**Step 10. Muted and phone checks.** Read the stills with no sound: the hook in the first 2 s, the
verb by its deadline. For phone speakers, high-pass a copy at 150 Hz: every hit must still show as an
onset (subs vanish on phones). [sourced: Violet Recording; 150 Hz inferred]

## 3. Sound design

| Role | Placement |
|------|-----------|
| Riser / suck-back | starts 1-4 bars before a hit, peaks exactly on it (±1 frame) |
| Hit / impact | on the cut or the in-shot impact; transient + body + sub layers |
| Sub drop | under the main hit and at most 1-2 others |
| Whoosh | starts 3-10 frames before a whip or fast move, peaks on it; also on title reveals |
| Drone / tension bed | under quiet sections; swells with the build |
| Braam | one or two per trailer at most |
| Stop-down | music out (or filtered), one sound left: a creak, a breath, a single block tap. It is a music event; a `breath` section is a picture event. One drop-out 0.3-0.8 s before the main hit satisfies L31 at 30 s |
| Lead-in | a roar or creak 0.5-1.5 s before a threat first appears (sound before sight). Tag it `lead_in`; the "never more than 1 frame early" rule binds only the sound that marks a key moment |

[sourced: Violet Recording, No Film School, Morphic, Lieu; frame counts inferred]

Today the Studio has no sound cues: every designed sound is laid in at assembly from the game's
sound files, at times taken from the plan or take report (`tools/assemble.py`). Recorded game sound
events (block hits, match sounds such as hit and kill confirms, block breaks) do replay in a
playback render (`Tracks/AudioEventTrack.cs`); a directed hand is silent (`studio-tooling.md`).

Game sounds to feature (pull the music out around one of them, as Lieu did with Half-Life: Alyx's
health sound): a punch and the hit confirm, the glove's boom, a rocket blast, a meteor impact, the
tsunami roar, the tower creak and groan, a survivor's yelp or "nope nope nope", the splash, the
elimination burst and the kill confirm (`MatchSoundCue`). In a brawl, give each hit its own short
sound on its frame and keep the music under them: a viewer counts hits by ear. [sourced; mayhem
application inferred]

Slow motion: into the ramp, a descending whoosh or pitch-down that bottoms out at the slowest frame,
music filtered or dropped to a drone; out of it, a rising build that stops on the frame normal speed
returns. Never play real-speed take sound under slowed picture (hard rule 8; the Studio does this
today, attempt 1 F4). [sourced vendor practice; attempt 1]

Duck music 6-10 dB under any voice and for a fraction of a second under each big hit; at a featured
game sound the music sits at least 3 dB under it for 0.2-0.5 s, or drops out. [sourced practice;
numbers inferred]

Transitions: hard cuts by default; let sound make the transition (a rise into the next shot's first
hit, a reverb tail, a stop-down). Dips to black at most one per 15 s outside the end card, always with
a hit. [sourced: Lieu]

## 4. Shot-length guidance (montage)

| Section | Typical length | Notes |
|---------|----------------|-------|
| Hook | 1.0-2.5 s | one readable gameplay moment in motion |
| Teach | 1.5-3.0 s | first core-verb shot (pull or hazard hit) ≥ 1.5 s uncut (hard rule 6) |
| Escalate | 1.0-1.8 s, then 0.4-0.9 s in the barrage | barrage: ≥ 4 cuts in 5 s (hard rule 3) |
| Breath / stop-down | 0.3-1.0 s of near-silence over one held shot | no cut inside the silence |
| Payoff | 1.5-3.0 s | hold until the result lands |
| Aftermath | 0.8-1.5 s | |
| Title | per the class window | |

Measured anchors: median body shot 1.6 s in mid-length trailers (IQR 1.44-1.71), 1.28 s in reveals;
10th-percentile shot 0.83 s (mid). Lieu's measured averages: Baba Is You 4.25 s, Papers Please 2.6 s,
Modern Warfare II 1.3 s, a Fast X trailer 0.82 s: game trailers cut slower than film trailers because
play must read. Below 0.5 s a shot is an accent, not information. [inferred]

## 5. Mix targets

| Measure | Hard (R04) | Default | Basis |
|---------|------------|---------|-------|
| Integrated loudness | -20 to -12 LUFS | -15 ±1 LUFS | measured median -16 (IQR -19.6 to -13.0); YouTube turns louder files down to about -14 [sourced] |
| True peak | ≤ -1.0 dBTP on the final mp4 | mix to -1.5 dBTP so the AAC encode stays under -1.0 | delivery spec [sourced]; 11 of 22 references exceed it |
| Short-term max (3 s) | - | ≤ -10 LUFS | Shepherd [sourced] |
| Loudness ramp, body last vs first quarter | ≥ 0 LU | +2 to +6 LU | measured 16 of 20 |
| Title section vs body | ≤ -3 LU | -6 to -12 LU (-4 to -10 in 15 s) | measured 16 of 21 at ≤ -6 LU; short spots weaker |
| Stop-down | - | 1 in 30 s, 1-2 in 60 s, optional in 15 s; ≥ 0.3 s and ≥ 12 LU below the body | measured 14 of 22 |
| Start | no black or silence over 0.25 s at frame 0 | sound at frame 0 | [sourced] |
| End | - | the last hit's tail decays naturally (≥ 1 s), no click | [sourced] |
| Format | stereo, 48 kHz, AAC | | Steamworks [sourced] |

## 6. Commands

Verify flags against the installed versions; librosa, madmom and some ffmpeg options were written from
general knowledge in report 02.

```sh
# Loudness: integrated, LRA, true peak, plus momentary (M) and short-term (S) every 100 ms
ffmpeg -hide_banner -i mix.wav -af ebur128=peak=true:framelog=verbose -f null - 2> ebur.txt

# Two-pass loudness normalisation that keeps dynamics (fails over to dynamic mode if TP would clip:
# then limit the loudest hits first and retry)
ffmpeg -i mix.wav -af loudnorm=I=-15:TP=-1.5:LRA=11:print_format=json -f null -
ffmpeg -i mix.wav -af loudnorm=I=-15:TP=-1.5:LRA=11:measured_I=<I>:measured_TP=<TP>:measured_LRA=<LRA>:measured_thresh=<T>:offset=<O>:linear=true -ar 48000 mix_norm.wav

# Silence and black at the start; clipping
ffmpeg -i trailer.mp4 -af silencedetect=noise=-40dB:d=0.25 -f null -
ffmpeg -i trailer.mp4 -vf blackdetect=d=0.1:pix_th=0.1 -an -f null -
ffmpeg -i mix.wav -af astats -f null -

# Waveform picture for the human pass
ffmpeg -i mix.wav -filter_complex "showwavespic=s=3000x400:split_channels=1" wave.png

# Phone check copy
ffmpeg -i mix.wav -af highpass=f=150 mix_phone.wav
```

```python
import librosa
y, sr = librosa.load("cue.wav", sr=None, mono=True)
tempo, beats = librosa.beat.beat_track(y=y, sr=sr, units="time")
env = librosa.onset.onset_strength(y=y, sr=sr)
onsets = librosa.onset.onset_detect(onset_envelope=env, sr=sr, units="time", backtrack=True)
```

Report 06 found librosa onset and beat times about 30 ms (one frame) late on a calibration clip;
subtract 0.030 s before comparing with cut frames. The repository's `tools/analyze.py` and
`tools/sync.py` already do this and produce the cut-to-onset tables (see `measured-reference.md`).

## 7. Mistakes

| Symptom (from numbers) | Cause | Fix |
|------------------------|-------|-----|
| Shot lengths nearly equal (CV under 0.2) | shots placed on a fixed grid | plan from sync points; add a barrage and a breath |
| The biggest hit falls on a generic shot | music laid under afterwards (attempt 1: hit at 2.66 s on the establishing shot) | music first; re-edit so the main hit meets the payoff |
| Over 90 % of cuts on beats | mechanical snapping | only sync points and barrage cuts on hits |
| An impact sound 3+ frames off its picture | sound placed by eye or by clip start | align the event frame to the onset; re-measure the render |
| Flat loudness (LRA under 5 LU) | wall-to-wall music, heavy limiting | a thinner intro, a stop-down, less limiting |
| The title louder than the body (attempt 1: +1.3 LU) | the music never drops | a sting on the title hit, then the music falls away |
| True peak above -1 dBTP (attempt 1: -0.6) | no limiter margin | mix to -1.5, check the encoded file |
| Music ends mid-phrase or on a fade | music trimmed to picture | edit the music to length first, in phrases |
| Slow motion with real-speed sound | the Studio's take sound under a ramp | designed sound only under ramps |
| Game sounds lost | the trailer treated as a music video | pull the music out at one featured game sound |
| A voice line "17 frames late" that sounds right | full-mix onsets miss soft first syllables | check the line's own lane; mark voices "needs ears" |
| Hits in a brawl blur into one | one long sound under several hits | one short sound per hit, on its frame |
