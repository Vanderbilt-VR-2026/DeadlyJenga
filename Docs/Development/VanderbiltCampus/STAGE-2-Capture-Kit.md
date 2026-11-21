# Stage 2: capture kit

Goal: Evan can walk campus with his phone (and a 360 camera if he gets one)
and know exactly what to shoot, how, and what is left. Every capture lands
in the archive, blurred copies are made, and the coverage map updates.

## Build

### Shot list
- Generated from the LoD2 model: every facade (by building and side),
  courtyard, path, plaza, staircase and hero detail, with:
  - priority: hero, normal or background;
  - method: photo set, 360 walk, close-up detail set, or material swatch;
  - estimated photo count, best time of day (shade side), and access notes
    (public sidewalks; no entering buildings or restricted areas).
- Ordered into **capture days** of about 2–3 hours, each a walking route.

### Coverage map (phone page)
- A single static web page Evan opens on his phone: campus top-down with
  every facade colored by status (not started, captured, aligned, needs
  reshoot). Tapping one shows its shot list entry and an example framing.
- Updated by the intake script; run locally or published privately (it shows
  no faces or raw photos, only status and the model).

### Capture guide
- Written for a first-timer, one page per device: iPhone/Android camera
  settings (RAW or highest-quality, lock exposure and focus, no HDR or
  night mode), how to walk a facade (two heights, 70–80% overlap, corners
  turned slowly), how to do a 360 walk, gray-card first frame, swatch
  shots, weather (overcast is best), people and privacy etiquette.
- A 360 camera recommendation with price (for example Insta360 X5) as an
  optional purchase in `EVAN-TODO.md`; the plan works with only a phone.

### Intake script (`pipeline/intake.py`)
For a day's capture:
1. Copy originals into `raw/capture/<date>/<device>/` with hashes; never
   modify them.
2. Read metadata (time, GPS, exposure, lens); sort by shot-list area using
   GPS and time.
3. 360 video: extract frames at a spacing set by speed; split into flat
   perspective views; drop top and bottom faces.
4. Blur faces and license plates in `derived/` copies (a local detector; no
   cloud upload). Mask moving people and cars for alignment.
5. Flag blurry, overexposed and duplicate shots.
6. Update the manifest and the coverage map; print a short report
   (captured, rejected, what to reshoot).

## Done when
1. The shot list, capture days and guide exist, and the hero list is
   confirmed.
2. Intake runs end to end on a sample set: Evan's test capture if he has
   made one, otherwise a public, permissively licensed building photo set
   (license logged), plus 30 seconds of any 360 sample.
3. The coverage map shows the sample set.
4. Nothing in `raw/` changed (verify passes), and blurred copies contain no
   readable faces in a 50-image spot check.
