# Measured reference: 22 official trailers

Open at stage 10 (profile check of a render) and whenever a number needs its evidence. Source:
report 06 (`craft/research/06-measured-trailers.md`), tables in `craft/tools/data/tables.md`, profile in
`craft/tools/data/reference_profile.json`. One rater, shot sizes and moves judged from small stills,
beats from librosa on the full mix. Videos are not in the repository.

## 1. The sample

22 official uploads (developer, publisher or platform channel, or the Steam store): 9 VR, 13 flat; 4
short (≤ 32 s), 11 mid (33-75 s), 7 long (> 75 s). Includes Human Fall Flat VR, Beat Saber (2),
Lethal Company (2), Walkabout Mini Golf, Among Us, Teardown, TABS, Untitled Goose Game, Superhot VR,
Gang Beasts, Party Animals, Content Warning, Ultimate Chicken Horse, Besiege, Job Simulator, Gorilla
Tag, Fall Guys (E3 2019), Half-Life: Alyx, Moss, It Takes Two. The short class has no montage: two
single takes, one graphic teaser, one slow mood teaser. Short-form rules are the weakest here.

Cut detector: tuned miss rate 8.3 % and false rate 0 % on 36 hand-labelled cuts; on the full sample
62 of 794 automatic cuts were removed as false and 39 added. It misses wipes, dissolves and whip
pans and is fooled by strobes and flashes. Audio onsets land about 30 ms late (corrected in the
scripts).

## 2. Key numbers

Median and interquartile range. "Body" = first content frame to the closing title.

| Metric | All (22) | Short ≤ 32 s (4) | Mid 33-75 s (11) | Long > 75 s (7) | VR (9) | Party / physics (10) |
|--------|----------|------------------|------------------|-----------------|--------|----------------------|
| Median shot, s | 1.62 (1.43-2.03) | 12.5 (single takes) | 1.60 (1.44-1.71) | 1.53 (1.25-1.92) | 1.67 | 1.65 |
| 10th-percentile shot, s | 0.78 | - | 0.83 (0.60-1.23) | 0.50 | 0.74 | 0.80 |
| Longest shot, s | 5.9 | 14.1 | 4.2 (3.3-5.3) | 9.5 | 10.3 | 5.3 |
| Last / first quarter median shot | 0.85 (0.41-1.26) | - | 1.06 (0.68-1.51) | 0.57 | 0.69 | 0.85 |
| Cuts per minute | 27 (19-35) | 4 | 30 (25-38) | 27 | 23 | 29 |
| Most cuts in 5 s | 4 (3-6) | 0-1 | 4 (4-5.5) | 6 | 4 | 4 |
| First gameplay, s | 1.0 (0.2-5.9) | 1.5 | 0.27 (0.04-1.5) | 11.0 | 1.75 | 0.30 |
| First "what is this game", s | 7.3 (2.1-12.0) | 2.0 | 4.3 (2.3-8.8) | 12.0 | 3.2 | 3.7 |
| Closing title start / runtime | 0.90 (0.85-0.92) | 0.80 (0.62-0.87) | 0.91 (0.88-0.92) | 0.89 | 0.85 | 0.90 |
| Title + end card, s | 6.2 (5.5-7.1) | 5.1 (4.0-5.7) | 5.8 (5.2-6.4) | 10.5 | 6.0 | 6.4 |
| Integrated loudness, LUFS | -16.0 (-19.6 to -13.0) | -15.9 | -16.9 | -16.0 | -15.9 | -15.2 |
| True peak, dBTP | -0.9 (-3.6 to +1.1) | -1.0 | +0.1 | -2.5 | -1.2 | -0.5 |
| Loudness last quarter minus first, LU | +3.7 (1.4-6.0) | +3.6 | +2.9 | +6.1 | +3.4 | +1.9 |
| Closing title vs body, LU | -8.9 (-17.8 to -6.0) | -2.9 | -8.9 | -14.3 | -12.1 | -7.0 |
| Stop-downs in body | 1.5 | 0 | 2 | 2 | 1 | 1 |
| Cuts within 2 frames of a beat (chance) | 0.30 (0.27) | 0.20 (0.27) | 0.30 (0.27) | 0.25 (0.28) | 0.21 (0.28) | 0.34 (0.27) |
| Onset percentile at cuts (50 = random) | 56 | 56 | 58 | 51 | 54 | 52 |
| Wide share | 0.50 | 0.00 | 0.62 | 0.44 | 0.24 | 0.60 |
| Close share | 0.05 | 0.00 | 0.04 | 0.10 | 0.08 | 0.04 |
| Static camera share | 0.67 | 0.00 | 0.75 | 0.47 | 0.25 | 0.71 |
| First-person share | 0.00 | 1.00 | 0.00 | 0.00 | 0.67 | 0.00 |
| Designed-move share | 0.11 | 0.00 | 0.11 | 0.21 | 0.03 | 0.14 |
| Cuts joining same-size shots | 0.59 | 0.72 | 0.62 | 0.49 | 0.58 | 0.50 |

For a mayhem cut, read the party / physics column (10 references: Gang Beasts, Fall Guys, TABS,
Party Animals, Human Fall Flat VR, Ultimate Chicken Horse, Among Us, Gorilla Tag, Teardown, Besiege) beside the mid
column: gameplay by 0.3 s, "what is this game" by 3.7 s, 60 % wide shots, 71 % static cameras, 14 %
designed moves, a mild loudness ramp (+1.9 LU) and no shortening into the end (mid-length party
trailers end on a held gag or hero shot). These trailers sell readable chaos mostly with one fixed
wide game camera (Gang Beasts 91 % same-size cuts; the comedy Goose 100 %). Nothing here measured how
readable a fight is; the mayhem lints' numbers are estimates.

Pooled sync tests (599 body cuts, 21 trailers): beat 25.0 % vs 24.9 % chance (p 0.47); guessed
downbeat 5.5 % vs 6.2 %; any onset 40.9 % vs 36.5 % (p 0.014); strong onset 9.8 % vs 7.4 % (p 0.016);
strong onset after shots under 0.8 s 12.8 % vs 6.7 % (p 0.004). Beat-phase vector strength 0.036
(flat).

## 3. Patterns (counts)

1. Montage shots sit around 1.6 s: 16 of 18 mid and long trailers have a median of 1.0-2.2 s.
2. Quick shots and one long breath: 10th percentile ≤ 1.0 s in 13 of 18; a shot ≥ 4 s in 13 of 18.
3. Gameplay first: within 2 s in 13 of 21, within 8 s in 17 of 21.
4. "What is this game" by 12 s in 15 of 21, usually one readable verb.
5. The name waits: closing title at 80-95 % of runtime in 18 of 22.
6. Title and card hold 4-8 s in 15 of 22.
7. The music builds (16 of 20) and drops for the title (16 of 21 at ≥ 6 LU).
8. Cuts are not beat-locked; they lean to onsets, most in fast passages. The biggest audio hit is
   within 3 frames of a cut in only 7 of 22.
9. Wide dominates flat gameplay; VR trailers are mostly first person (6 of 8).
10. Montages do not alternate shot sizes (59 % same-size cuts).
11. Gameplay ≥ 70 % of runtime in 16 of 22; 11 of 22 use captions.
12. Live action of a headset player: 2 of 9 VR trailers.
13. Buttons after the title: 2 of 22.
14. Logos first: 4 of 22.
15. Shortening into the climax: a tendency (12 of 19), strong in reveals and long trailers, absent in
    mid-length party trailers.

## 4. Reference profile (mid class, used for 15 s and 30 s montage metrics)

| Metric | p10 | p25 | median | p75 | p90 |
|--------|-----|-----|--------|-----|-----|
| median shot `msl`, s | 1.17 | 1.44 | 1.60 | 1.71 | 2.03 |
| 10th-pct shot `sl_p10`, s | - | 0.60 | 0.83 | 1.23 | - |
| longest shot `sl_max`, s | - | 3.28 | 4.17 | 5.33 | - |
| cuts per minute | - | 24.7 | 30.0 | 38.1 | - |
| most cuts in 5 s | - | 4 | 4 | 5.5 | - |
| first gameplay, s | - | 0.04 | 0.27 | 1.5 | - |
| first "what", s | - | 2.3 | 4.3 | 8.8 | - |
| closing title fraction | - | 0.88 | 0.91 | 0.93 | - |
| title + card, s | - | 5.2 | 5.8 | 6.4 | - |
| integrated LUFS | - | -19.5 | -16.9 | -13.3 | - |
| loudness ramp, LU | - | 0.2 | 2.9 | 5.6 | - |
| title vs body, LU | - | -21.3 | -8.9 | -7.0 | - |
| onset percentile at cuts | - | 49.8 | 57.5 | 65.1 | - |
| share of cuts on top-25 % onsets | - | 0.21 | 0.35 | 0.42 | - |
| wide share | - | 0.47 | 0.62 | 0.80 | - |
| close share | - | 0.00 | 0.04 | 0.07 | - |

Short class (n=4), the closest for timing: closing title fraction IQR 0.62-0.87 (median 0.80); title +
card IQR 4.0-5.7 s (median 5.1); first gameplay 0.9-1.8 s. `profile.py check` compares a short trailer's
montage metrics against the mid class automatically and its timing metrics against the short class.
Profile rules it prints: first gameplay ≤ 2 s; first "what" ≤ 12 s (mid, long); closing title 0.80-0.95
(mid, long); card 4-8 s (short, mid); median shot 1.0-2.2 s; loudness ramp ≥ 0; title ≤ -3 LU; -20 to -12
LUFS; true peak ≤ -1; close share ≥ 0.05 (mid, long).

`profile.py check` prints every rule whatever its `classes` field says. Where the skill's 15 s rules
differ from the profile, the skill wins (they are extrapolated on purpose): a 15 s closing title
starting at 11.0-12.0 s (fraction 0.73-0.80) prints `final_title_late` as FAIL below 0.80, and its
3.0-4.0 s card prints `endcard` as FAIL (under 4.0 s). Note both under R06 as expected; any other
FAIL or OUT needs a real explanation.

Why 11.0-12.0 s (revision 2): at 15 s the two measured bands conflict. The final-title fraction is
≥ 0.80 in 19 of 22 and ≥ 0.75 in 20 of 22 (exceptions: Human Fall Flat VR's title over its take at
0.22, Beat Saber's teaser at 0.749, Alyx at 0.797). Title plus card is ≥ 3.5 s in 21 of 22 (only
Lethal Company V85's 1.9 s card is shorter; Teardown's is 3.52 s). A 15 s cut at 0.80 leaves a
3.0 s card; at 0.73 a 4.0 s card. The skill takes the middle (11.5 s, 3.5 s) and accepts both
trades. The earlier 10.0-11.5 s window (0.67-0.77) put both 15 s test plans at 73 %, where graders
did not reward it.

## 5. Attempt 1 against the profile (15.0 s, 6 shots)

| Metric | Attempt 1 | Reference | Verdict |
|--------|-----------|-----------|---------|
| median shot | 2.40 s | montage 1.0-2.2 | OUT: neither montage nor take |
| shortest shot | 2.0 s | mid 10th pct 0.6-1.2 | OUT |
| most cuts in 5 s | 2 | mid 4-5.5 | OUT: no barrage |
| title + card | 2.3 s | 4-8 (short 4.6-6.0) | OUT |
| title vs body | +1.3 LU | ≤ -6 in 16 of 21 | FAIL |
| cuts on top-25 % onsets | 0 of 5 | mid 21-42 % | OUT: music laid under |
| biggest hit | 2.66 s, on the establishing shot | at the payoff | OUT |
| close shots | 0 % | most trailers have one | FAIL |
| true peak | -0.6 dBTP | ≤ -1 | FAIL |
| first gameplay, closing title fraction, LUFS, ramp | 0.0 s, 85 %, -15.1, +3.2 LU | in range | PASS |

## 6. Running the tools on a new render

The scripts are in `craft/tools/`. They expect a working root laid out as `<root>/scripts/` (the
`.py` files and `meta.csv`), `<root>/analysis/<slug>/`, `<root>/annot_src/` and `<root>/aggregate/`.
On Evan's machine the root with all 22 analyses is
`C:\Users\evanp\Documents\Codex\Deliverables\TrailerCraft-Reference` (videos stay there, not in the repo).
On another machine: make a root, copy `tools/*.py` and `tools/meta.csv` into `scripts/`, and copy
`tools/data/reference_profile.json` into `aggregate/` (re-running `aggregate.py` needs the 22 analyses;
without them use `profile.py check` only after adding your row by hand, or compare numbers against the
tables above).

```sh
ROOT=/c/Users/evanp/Documents/Codex/Deliverables/TrailerCraft-Reference
SLUG=dj-hand-reveal-v03
VID=/path/to/trailer.mp4

# 1. Cuts, motion, audio, alignment (writes analysis/$SLUG/summary.json, shots.csv, audio.json)
python -I "$ROOT/scripts/analyze.py" "$VID" "$ROOT/analysis/$SLUG"
# 2. Filmstrips (first, middle, last frame of every shot); 'validate' shows frames around each cut
python -I "$ROOT/scripts/sheets.py" "$VID" "$ROOT/analysis/$SLUG" filmstrip
python -I "$ROOT/scripts/sheets.py" "$VID" "$ROOT/analysis/$SLUG" validate
# 3. Annotate: write $ROOT/annot_src/$SLUG.txt (format below), then convert
python -P "$ROOT/scripts/annot_convert.py" "$ROOT"
# 4. Tracker-independent sync measures (phase strength, onset percentile at cuts)
python -P "$ROOT/scripts/sync.py" "$ROOT/analysis/$SLUG"
# 5. Add a row for $SLUG to $ROOT/scripts/meta.csv, then aggregate and check
python -P "$ROOT/scripts/aggregate.py" "$ROOT"
python -P "$ROOT/scripts/profile.py" check "$ROOT" "$SLUG" short
```

Annotation file (`annot_src/<slug>.txt`), as `tools/annotations/attempt-1.txt` shows:

```text
# content_start=0; first_logo=11.0; first_gameplay=0; first_what=4.5; first_title=11.0; final_title=11.0; endcard_start=11.0; endcard_end=15.0; button=; gameplay_share=1.0
0 C st g hook block slides out, fingertips cropped
1 M st g teach hero looks up
2 W cr g teach hand pulls the block beside her (tilt-up)
...
```
Per line: shot index (or a-b range), size (EW W M C T), move (st pu pl pa fo or cr ha pov un), kind
(g gameplay, c cinematic, t text, l live action, u UI), job (hook teach esc wow joke var build title cta
brand), then a short description. Your own plan gives every field; copy them rather than re-judging.

If the detector's cuts disagree with the plan by more than 1 frame, check the `validate` sheet before
blaming the render: dissolves, flashes and fast camera moves fool it.
