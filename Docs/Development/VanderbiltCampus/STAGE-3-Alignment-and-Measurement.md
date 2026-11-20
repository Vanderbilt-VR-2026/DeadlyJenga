# Stage 3: alignment and measurement (CPU)

Goal: every captured photo knows exactly where it was taken, in the campus
frame, and each facade has measured positions for its windows, doors,
columns and trim. This is the step that makes the models exact and keeps
realism possible later.

## Build

### Alignment
- `pipeline/align.py`: per capture set (a building or area), run COLMAP or
  GLOMAP on the blurred and masked derived images, on CPU. Large sets are
  split into overlapping chunks and merged.
- Fallback for sets that won't align: try sequential matching for 360
  frames, then a VGGT-family aligner if it runs on CPU in reasonable time
  (log it). If RealityScan is installed (Evan's login), it may be used and
  its exports kept as a second version.
- Register each alignment to the campus frame: match the sparse points to
  the LiDAR (ICP on facade and ground points, initialized from GPS), then
  check the scale. Write the transform and residuals.
- Output in `derived/alignment/<set>/v<n>/`: cameras (intrinsics and
  poses), sparse points, the registration, and a report (images aligned,
  mean reprojection error, registration error against the LiDAR).

### Measurement
- Facade planes: fit wall planes per facade from the sparse points + the
  LoD2 model.
- Rectified facade images: project aligned photos onto each plane to make a
  straight-on facade image (CPU; for measuring, not for the game).
- Feature detection on the rectified facades: windows, doors, columns,
  cornice lines, string courses, arches. Use a facade-parsing or general
  segmentation model that runs on CPU; hand-check results in a review page.
  Output `facade_layout.json` per facade (each feature: type, rectangle or
  outline in facade coordinates, depth if the points show it, confidence).
- Depth: from the sparse/semi-dense points (CPU patch-match on small crops
  is allowed if fast enough), to place recesses and projections.
- `sources.json` per building (STANDARDS.md §6).

### Review page
- A local page per building: facade images with detected features drawn on
  top, clickable to accept or fix boxes; saves corrections to
  `facade_layout.json` as a new version. The builder can run the review
  itself, and Evan can open it later.

### Optional GPU scripts (written, not required)
- Dense reconstruction per hero building (COLMAP MVS, RealityScan mesh, or
  a splat + MILo mesh) for ACCRE or a cloud GPU, with a README. Results go
  into `derived/dense/` and Stage 4 uses them as an extra fit reference
  when present.

## Done when
1. The sample set (or Evan's first real capture) aligns with ≥ 90% of
   images registered and mean reprojection error ≤ 1 px.
2. Registration to the LiDAR is within 0.1 m RMS on facade points.
3. One facade has a reviewed `facade_layout.json`, and drawing it back into
   three of the original photos lines up within 0.1 m (overlay images in the
   results file).
4. `sources.json` exists for every aligned building and the coverage map
   shows "aligned".
