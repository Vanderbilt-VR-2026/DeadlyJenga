# Stage 2 results: capture kit

Built 2026-11-20 by Vanderbilt-Main. Written while Stage 1's building accuracy work was still running
(it only needs the footprints, which did not change).

## What exists
| Piece | Where | Numbers |
|-------|-------|---------|
| Capture guide | `capture/README.md`, `iphone.md`, `android.md`, `facade-walk.md` (+ svg), `360-walk.md`, `swatches-and-details.md`, `weather-light-privacy.md`, `checklist.md` | 8 pages |
| Shot list | `capture/SHOT-LIST.md`, `Tools/VanderbiltCampus/config/shot_list.json` | 375 buildings, 2,691 facades (1,912 public, 301 restricted, 478 blocked), 3,233 entries (384 hero, 2,228 normal, 621 background) |
| Capture days | `capture/CAPTURE-DAYS.md`, `capture/days/day-NNN.png` | 74 main days (217 h, 160,339 photos) + 20 optional; each 2.5-3.4 h. Day 1 = Kirkland Hall + Alumni Lawn (2.6 h, 2,008 photos) |
| Coverage map | `capture/coverage/index.html` | one 1.08 MB self-contained page; status per facade from `config/coverage_status.json` |
| Intake | `Tools/VanderbiltCampus/pipeline/intake.py` (+ `intake_*.py`) | copy with hashes, metadata, 360 frame split, face/plate blur, masks, quality flags, coverage update |

The full plan is large; see DECISIONS D013 for the tiers (Day 1, hero at full standard, the rest lighter).

## Done when
1. **Shot list, capture days and guide exist; hero list confirmed.** Yes. Hero names checked against OSM
   (SHOT-LIST.md). Approximate, to check on site: Kirkland tower faces, Peabody Esplanade outline, Commons
   Center Lawn, Branscomb Quad, and Rand Wall (a guess: a strip along Rand Hall's north-east side).
2. **Intake runs end to end on a sample set.** Yes, on stand-ins (Evan has not captured yet; D009):
   - KartaView street photos on 21st Ave S (CC BY-SA 4.0): 353 images, 348 usable, 132 assigned to shot-list
     path entries, 221 farther than 60 m from campus.
   - Zenodo 16576623 building set (CC BY 4.0): 65 images, 58 usable.
   - 360 sample: **synthetic** 30 s equirectangular clip (the real CC BY-SA clip on Wikimedia Commons was
     blocked by HTTP 429): 60 frames, 480 perspective views. The 360 path has not seen a real camera file.
3. **The coverage map shows the sample set.** Yes: 22 path entries near 21st Ave S show as "needs reshoot"
   (they have 1-16 photos against 19-58 needed); everything else is "not started".
4. **Nothing in raw/ changed; blurred copies have no readable faces.** `manifest.py verify --prefix raw/`:
   595 ok before, 597 ok after (the two new files are the synthetic clip and its sidecar), 0 problems.
   50-image spot check (`reports/stage2/blur_spotcheck.jpg` in the archive): no readable face or plate,
   checked by the worker and by the lead. The real face test is one public-domain photo with 14 people
   (all blurred); KartaView arrives pre-blurred.

## Known limits
- Plate detection relies mostly on blurring the lower band of every detected vehicle (the plate model was
  trained on non-US plates). A plate on a vehicle the detector misses can stay sharp: look before sharing.
- Blurred copies keep GPS and time. Safe to share: coverage status, intake reports, masks, spot-check sheets.
- HEIC and DNG reading are installed but untested on real phone files. `.insv` must be exported first.
- The coverage page was tested in desktop browsers at phone size, not on a real phone (pinch, GPS button).
- Wikimedia Commons photo downloads are still rate-limited (1 of 370 files).

## Tests
`pytest tests/test_intake.py tests/test_shotlist.py tests/test_coverage_map.py`: 34 passed.
