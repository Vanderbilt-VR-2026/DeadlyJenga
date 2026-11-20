# Optional GPU scripts: dense reconstruction per building

These scripts are written but **not run**. Evan's laptop has no NVIDIA GPU, and none of
Stage 3 needs them. Run them on a machine with an NVIDIA GPU, such as ACCRE or a rented cloud
GPU. Stage 4 uses the results as an extra fit reference when they exist (`derived/dense/`).

## What you need
- An NVIDIA GPU with 8 GB or more and a recent driver.
- A COLMAP build with CUDA (4.x). The Windows release zip "colmap-x64-windows-cuda", or on
  Linux the `colmap` conda-forge package with CUDA, or a source build with `-DCUDA_ENABLED=ON`.
  Check it with `colmap help`: the header has to say "with CUDA".
- A copy of the archive folders the script reads:
  - `derived/alignment/<set>/<version>/` with `sparse/`, `cameras.json` and `registration.json`
  - `derived/capture/<set>/blurred/` (only the blurred photos; never `raw/`)
  - `buildings/<id>/sources.json` and/or `buildings/<id>/facades/*/layout_v*.json` (they list
    which photos see the building)
- This repository's `Tools/VanderbiltCampus` folder and a Python environment with
  `requirements.txt` installed. Point `config/local.json` (`data_root`, `colmap`) at the copy.

## Run (COLMAP multi-view stereo)
```
python Tools/VanderbiltCampus/gpu/dense_colmap.py --set kartaview-2026-11-19 --unit 1.4 --building B_820346890 --dry-run
python Tools/VanderbiltCampus/gpu/dense_colmap.py --set kartaview-2026-11-19 --unit 1.4 --building B_820346890 --colmap /path/to/colmap
```
`--unit` is a registration unit from `registration.json` (`units`). A unit is one rigid
component, or one 20-photo segment of a component that bent. The script only uses photos from
that one unit, because only they share the unit's transform to the campus frame.

The script runs these commands in order and prints each one first (`--dry-run` only prints
them):
```
colmap model_transformer  --input_path <alignment>/sparse/<component> --output_path <work>/sparse_campus --transform_path <work>/colmap_to_campus.txt
colmap image_undistorter  --image_path <capture>/blurred --input_path <work>/sparse_campus --output_path <work>/dense --output_type COLMAP --image_list_path <work>/images.txt --max_image_size 3200
colmap patch_match_stereo --workspace_path <work>/dense --workspace_format COLMAP --PatchMatchStereo.geom_consistency 1 --PatchMatchStereo.gpu_index 0 --PatchMatchStereo.max_image_size 3200
colmap stereo_fusion      --workspace_path <work>/dense --workspace_format COLMAP --input_type geometric --output_path <work>/dense/fused.ply --StereoFusion.min_num_pixels 3 --StereoFusion.max_reproj_error 2
colmap poisson_mesher     --input_path <work>/dense/fused.ply --output_path <work>/dense/meshed-poisson.ply
colmap delaunay_mesher    --input_path <work>/dense --input_type dense --output_path <work>/dense/meshed-delaunay.ply
```
`<work>` is `derived/dense/<set>/<out-version>/<building>/`. The first step moves the model into
campus meters, so `fused.ply` and the meshes line up with the LiDAR and the LoD2/LoD3 models.
`run.json` records the photos, the commands, the exit codes and the timings. The outputs are
added to the archive manifest (stage 3). They count as derived from the photos, so they carry the
photos' license (KartaView: CC BY-SA 4.0, credit "Imagery (c) KartaView contributors").

Expected time: a few minutes per building for 10–30 photos on one modern GPU. Memory grows
with `--max-size`.

## What to expect from the stand-in photos
The KartaView sequences are forward-facing dashcam frames taken 7–11 m apart. Dense matching on
them gives patchy, noisy walls. The script is really meant for Evan's own facade walks (70–80 %
overlap, facing the wall), where COLMAP MVS works well.

## Alternatives (not scripted)
- **OpenMVS** (AGPL-3.0): `InterfaceCOLMAP`, then `DensifyPointCloud`, `ReconstructMesh`,
  `RefineMesh` and `TextureMesh` on the same undistorted workspace. It often gives cleaner
  meshes than the COLMAP meshers, and it can also run on CPU (slowly).
- **RealityScan** (Epic; needs Evan's login, so don't accept its terms on his behalf): import
  the photos with the COLMAP poses, or let it align on its own. Keep its exports as a second
  version (`derived/dense/<set>/v2`).
- **Gaussian splats plus a mesh extractor** (3DGS/2DGS, then MILo or similar): the most
  realistic look, but the licenses vary. The original 3DGS code is research-only, so check each
  project's license before use. Start from the same COLMAP model and undistorted images.
- **Feed-forward models** (VGGT, MapAnything): the checkpoints that allow commercial use are
  VGGT-1B-Commercial and `facebook/map-anything-apache` (Apache-2.0). They need a GPU for
  reasonable speed.
