# Stage 3 results: alignment and measurement

Built 2026-11-20, CPU only (Ryzen 7 7730U, no NVIDIA GPU). Evan has not captured anything yet, so
the work was proven on stand-in photos (DECISIONS D009):
- **KartaView** street photos along 21st Ave S and Midtown: 353 photos in 4 sequences (2019–2021),
  forward-facing dashcam frames. CC BY-SA 4.0, credit: *Imagery © KartaView contributors*.
- **Zenodo 16576623** control set: 65 photos of one building in Italy. CC BY 4.0, credit:
  Legut-Pintal, Maria. Not in Nashville and has no GPS, so it only tests the aligner.

The LiDAR is USGS 3DEP (credit: *Lidar data: U.S. Geological Survey, 3D Elevation Program*).
Footprints come from OpenStreetMap (© OpenStreetMap contributors, ODbL) and Metro Nashville Open
Data. The background orthophoto is USDA NAIP.

## Against the "Done when" list

| # | Target | Result |
|---|--------|--------|
| 1 | ≥ 90 % of images registered | **Not met.** Control set: 53 of 65 photos (81.5 %) are in one model. The other 12 form a second model with no verified match to the rest. KartaView: 352 of 353 photos register (99.7 %), but split into 3 models of 172 / 162 / 18 photos. The largest is 48.7 %. |
| 1 | Mean reprojection error ≤ 1 px | **Met.** Control set: 0.42 px. KartaView: 0.59 / 0.64 / 0.50 px. This is after a clean-up step: re-triangulate, then drop observations above 2 px. Straight from the mapper it was 1.28–2.03 px. |
| 2 | Registration to the LiDAR ≤ 0.1 m RMS on facade points | **Not met.** Best result: about 0.18 m RMS, and only on the 6–31 % of facade points that come within 0.5 m of a target. Within 1 m the RMS is 0.35–0.42 m. Details below. |
| 3 | One facade has a reviewed `facade_layout.json` that lines up within 0.1 m in three photos | **Partly met.** One facade was reviewed: B_820346890 F01, the "Losers" bar on Division St, Midtown. It is **off campus** (backdrop ring). In three photos the median edge offset is **0.08 m**, but only 30 of 54 edge measurements are within 0.1 m (p90 0.39 m). Those are the same photos the rectified image was made from. In photos from a neighbouring registration segment, the layout lands on the next building, several meters off. |
| 4 | `sources.json` for every aligned building; the coverage map shows "aligned" | **Met for what aligned.** 17 campus buildings have `sources.json` (33 facades); 19 shot-list facades show "aligned" (≥ 2 photos, coverage above 0). |

## Alignment (`pipeline/align.py`)

**Control set** (`fallback-2026-11-19/zenodo-16576623/v1`)
- 65 photos, exhaustive matching (2,080 pairs). Both COLMAP mappers gave the same two models,
  53 + 12 photos.
- Mean error 0.42 px (median 0.33, p95 1.01). Mean track length 4.6.
- The 12 photos (IMG_4987–4998) show a stretch of wall with photos IMG_4984–4986 missing from the
  published set. They have zero verified matches to the rest.
- The "more features" fallback was not tried on this set.

**KartaView** (`kartaview-2026-11-19/v1`)
- 353 photos, images shrunk to 2,400 px, people and cars masked out.
- Matching: next 10 photos in the sequence plus GPS neighbours within 60 m; 4,153 pairs took 37
  minutes.
- The wider-overlap fallback (7,204 pairs, 27 minutes) changed nothing.
- Global mapper with a fixed focal length, then the 2 px clean-up. Total run 36 minutes plus about
  10 minutes of feature extraction.
- The three models follow the match graph exactly:
  - 3632429 plus the first 4 photos of 3632465 (162 photos)
  - the rest of 3632465 after a U-turn onto another street (172)
  - 4073705, filmed in 2021 in another area (18)
  - The single photo of 1342319 matches nothing.
  - No setting can join them: the photos show different streets.
- The focal length is assumed (29.4 mm equivalent, about 3,290 px). The photos have no EXIF, and
  this is what the best-calibrated 43-photo model gave. Letting each model calibrate itself sent
  the focal length to 12,000–17,000 px on forward-moving frames.
- **Chunks** (`v1/chunk_test`, sequence 3632465 forced into 2 chunks of 103 photos with 30
  shared): the chunks joined on 29 of the 30 shared photos. Result: 172 of 176 photos at 0.56 px,
  the same as without chunks.
- A CPU-capable, permissively licensed VGGT-family model exists (VGGT-1B-Commercial; MapAnything
  Apache-2.0 checkpoint), but it was not tried. No CPU timing is published.

## Registration to the campus frame (`pipeline/register_lidar.py`)

- **Starting point:** the camera positions are fitted to GPS with a robust 7-number similarity
  (Umeyama with RANSAC). The cameras' upright direction stands in for gravity.
- **Refinement:** fitted point-to-plane against three targets, with scale free (Huber loss, 27
  starts):
  - LiDAR facade points (vertical patches of class 1/6)
  - footprint walls (footprints extruded to the eave)
  - LiDAR ground
- **The KartaView models bend.** Forward-facing dashcam frames make the scale drift along the
  street, by 10–100× over a 1.7 km model. A single similarity fits only 51 of 172 and 69 of 162
  cameras within 10 m. Those components were split into 20-photo segments, each registered on its
  own.
- **Result:**
  - 16 units registered (two segments lie outside the LiDAR); 11 got the LiDAR refinement.
  - 7 kept their scale within ±3 % of GPS; the others moved 4–22 % and are flagged.
  - The rigid 18-photo model came in at −0.23 % scale.
- Facade and ground residuals after refinement, pooled over the 7 accepted units (fit areas):

| Data | Points within 0.5 m | RMS | Points within 1 m | RMS |
|------|---------------------|-----|-------------------|-----|
| LiDAR facade points | 6.4 % | 0.18 m | 11.6 % | 0.42 m |
| Footprint walls | 30.8 % | 0.18 m | 38.4 % | 0.35 m |
| LiDAR ground | 48 % | 0.19 m | 61 % | 0.31 m |

- **Before refinement**, almost no facade points were within 0.5 m of either target.
- **Held-out check:** 30 % of the area, in 10 m blocks, was kept out of the fit. Only 95 facade
  points fall in those areas and almost none land near a target, so it says little. Ground there
  got slightly worse after refinement (within 0.5 m: 10 of 12 points before, 3 of 12 after),
  which points to overfitting.
- **Camera heights** come out at 0.9–2.1 m for most units. That is not an independent check: the
  fit pulls the height toward ground + 1.6 m.
- **Cameras inside footprints:** 1–6 per unit.
- **Unreliable poses:** 106 of the 312 registered photos sit more than 25 m from their GPS
  position and are marked unreliable. Even "reliable" photos can be badly rotated. On segment 0.6
  the facades project tilted about 45° into the photos, and those photos still pass the GPS check.
- **Why 0.1 m is out of reach on this data:**
  - Aerial LiDAR has few wall returns.
  - The sparse points are thin, about 50 per photo, with little ground.
  - The street-photo models bend within a segment.
  - Footprint walls are only accurate to about 0.3 m.

Images (credit line on each):
- ![top-down](images/stage3-kartaview-2026-11-19-topdown.jpg)
- ![overlay 1](images/stage3-kartaview-2026-11-19-overlay_1.jpg)
- ![overlay 2](images/stage3-kartaview-2026-11-19-overlay_2.jpg)
- ![overlay 3](images/stage3-kartaview-2026-11-19-overlay_3.jpg)

## Measurement

**Choosing the facade**
- Campus facades seen by the photos were tried first. Along 21st Ave S they are mostly behind
  trees (for example B_new_213, next to Owen).
- The ones that are visible sit in segments whose poses are wrong:
  - Heard Library and Tibbot Art Center (segments 0.3/0.4): GPS start only.
  - Kay Bob's (0.6): poses rotated about 45°.
- The best registered views of a building are segment 1.4 in Midtown (scale −0.39 %, LiDAR
  refined). It sees building **B_820346890 facade F01**, the front of the "Losers" bar on Division
  St. That building is in the backdrop ring, not on campus.
- It has no v2 LoD2 model, so its facade frame comes from `facade_layout.facades_of()` with the
  v1 footprint extruded from ground to eave. The layout schema is unchanged.

**Steps** (`pipeline/facade_measure.py`)
- **Plane:** the LoD2/footprint wall, moved −0.055 m along its normal to the median of 399 sparse
  points on it.
- **Rectified image:** 9 photos (18–66 m away, 12–20° from straight-on) blended at 40 px/m, 717 ×
  198 px. It stays in the archive at `buildings/B_820346890/facades/F01/rectified_v1.jpg`.
- **Detection:** classical OpenCV only; no learned model is used. It found 3 features:
  - the beam line under the sign band, correct to within about 0.06 m
  - two "windows" that are really signs: wrong
  - It missed the posts and the screened openings of this dark wooden front. Detection on this
    facade is weak.
  - On a synthetic window grid (unit test) it finds every window and the door within 0.1 m.
- **Depth:** taken from the sparse points where 3 or more fall inside and around a feature;
  otherwise null.
- **Output:** `layout_v1.json` (detected).

**Review** (`pipeline/facade_review.py`)
- A local page (served on 127.0.0.1, or a static file that downloads an edit file) with the
  rectified image and editable boxes. I checked in the built-in browser that boxes can be
  selected and moved.
- I reviewed F01 through the page's save endpoint, which writes `layout_v2.json` with reviewed =
  true. It holds 15 features, all marked manual:
  - 6 posts (column)
  - 6 screened openings (window)
  - the enclosed lower front (balustrade)
  - the beam (cornice, moved from the detected one)
  - the outside stair (steps)
- The two wrong detections were deleted.

**Done-when 3 check** (`facade_measure.py check`)
- Each window and post of the reviewed layout was drawn into the three nearest photos (0.6–0.8 cm
  per pixel at the wall).
- How it was measured: each feature's vertical sides were slid sideways together, and its
  horizontal sides up and down together, in 2 cm steps up to ±0.5 m. At each step the image
  gradient across the drawn edges was summed. The shift with the strongest edges is that edge's
  offset. It counts only if the peak stands out (≥ 1.15 × the median) and is not at the ±0.5 m
  limit.
- Results:

| Photo | Edges measured | Median offset | p90 | Within 0.1 m |
|-------|----------------|---------------|-----|--------------|
| 3632429/00298 | 18 | 0.09 m | 0.33 m | 10 |
| 3632429/00297 | 18 | 0.07 m | 0.30 m | 13 |
| 3632429/00296 | 18 | 0.12 m | 0.43 m | 7 |
| **All** | 54 | **0.08 m** | 0.39 m | 30 of 54 |

- Visually, the posts line up to within a few centimetres in these photos. Some measured offsets
  are the slide locking onto a tarp seam or sign edge instead of the post.
- These three photos also built the rectified image, so the check is not independent of the
  layout.
- A tougher check used three photos from the next segment (1.7, its own transform). There the
  layout lands on the neighbouring building, several meters off. The edge slide only searches
  ±0.5 m, so its "0.26 m" for those photos is meaningless. Absolute accuracy is limited by
  registration, not by the layout.

Overlays (yellow = reviewed layout, orange = trim, cyan = best edge fit):
- ![facade check 1](images/stage3-facade-1.jpg)
- ![facade check 2](images/stage3-facade-2.jpg)
- ![facade check 3](images/stage3-facade-3.jpg)

**Stage 4 buildings**
- Tibbot Art Center (B_544413714) is seen by 5 photos, but all of them are in segments 0.3/0.4.
  Those segments only have a GPS start, and the drawn facade misses the building by meters.
- No detected layout was made for Tibbot. Saving it would also put a wrong newest version in
  front of Stage 4's estimated layouts.
- Kirkland Hall is not seen by any photo.

## Optional GPU scripts
`Tools/VanderbiltCampus/gpu/README.md` and `gpu/dense_colmap.py` (written, only dry-run here):
- COLMAP MVS per building on an NVIDIA machine: `model_transformer` to the campus frame, then
  `image_undistorter`, `patch_match_stereo`, `stereo_fusion` and the Poisson/Delaunay meshers.
- Output goes to `derived/dense/<set>/v<n>/<building>/`.
- The README lists OpenMVS, RealityScan, splats with a mesher, and VGGT/MapAnything as
  alternatives.

## Re-run commands
```
P=Tools/VanderbiltCampus/pipeline; PY=Tools/VanderbiltCampus/.venv/Scripts/python.exe
$PY $P/align.py --set fallback-2026-11-19/zenodo-16576623 --mapper both
$PY $P/align.py --set kartaview-2026-11-19 --fix-intrinsics --default-focal-35mm 29.4 --mapper global --refine-px 2.0 --max-fallbacks 1
$PY $P/align.py --set kartaview-2026-11-19 --sequences 3632465 --out v1/chunk_test --chunk 103 --overlap 30 --mapper global --fix-intrinsics --default-focal-35mm 29.4 --refine-px 2.0 --no-fallbacks
$PY $P/register_lidar.py --set kartaview-2026-11-19
$PY $P/align_sources.py  --set kartaview-2026-11-19          # sources.json, coverage "aligned", coverage page
$PY $P/align_review.py   --set kartaview-2026-11-19 --copy-to-docs
$PY $P/facade_measure.py measure --building B_820346890 --facade F01 --units 1.4
$PY $P/facade_review.py  serve --building B_820346890        # review, Save -> next layout version
$PY $P/facade_measure.py check --building B_820346890 --facade F01 --units 1.4 --photos 3 --copy-to-docs
```
Tests: `pytest Tools/VanderbiltCampus/tests`.

## What Evan's own capture would change
- **Facade walks with 70–80 % overlap, facing the wall** (not forward dashcam frames 7–11 m
  apart): rigid models with no segment splitting, many more points on walls and ground, and
  straight-on photos for rectification.
- **A known phone camera with EXIF focal length:** no assumed focal length.
- **One continuous set per building:** ≥ 90 % in one model becomes realistic.
- **Campus buildings seen in full** (the stand-in photos mostly show trees): measurement could
  run on the real Stage 4 buildings (Kirkland, Tibbot).
- **The 0.1 m target on facades** would still need a better wall reference than aerial LiDAR. A
  dense reconstruction (the GPU scripts) or a few surveyed points would do.

## Not done
- No campus facade was measured. Every visible one sits in a badly registered segment.
- No detected layout for Tibbot (Stage 4) or Kirkland (no photos).
- No learned facade-segmentation model. The classical detector misses most features on dark or
  cluttered fronts.
- The LiDAR registration target (0.1 m) is not reached, and the held-out check has too few points
  to judge.
- The GPU scripts were not run.
- The control set's "more features" fallback was not tried.
- Headset or Unity: not touched. This stage has no game-side output.
