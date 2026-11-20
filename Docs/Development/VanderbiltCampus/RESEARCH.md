# Vanderbilt main campus in Unity: research report

Goal: a faithful, detailed, well-textured 3D Vanderbilt main campus as a
Deadly Jenga map that runs on Meta Quest 3, made with free tools where
possible. Researched 2026-11-18. Tool claims come from vendor pages,
papers and community write-ups; test on real campus data before
committing.

## Chosen route: shape first

Evan chose to build the **exact shape now with simple style materials**, and
add realistic textures later on a strong GPU from the same archive (see
[README.md](README.md) and [STANDARDS.md](STANDARDS.md)). Layer 1 and the
"Shape-first tools" section below are used now. Layer 2 capture is done now,
to the standard the later bake needs. The splat, de-lighting and texture
tools in Layer 3 are used in Stage 6, except alignment, which runs now on
CPU.

## Shape-first tools (used in Stages 1–5)

- **CityGML levels of detail** (LoD1–LoD3) are the mapping world's shared
  vocabulary for building detail; STANDARDS.md uses them.
- **Roofs and massing from LiDAR:** CGAL Shape Detection (finds planes in
  point clouds), PolyFit and City3D (TU Delft / Liangliang Nan: watertight
  building models from point clouds, City3D for aerial LiDAR), 3dfier (TU
  Delft: footprints plus terrain into a solid city model). PDAL for
  classifying and cropping LiDAR.
- **Camera alignment on CPU:** COLMAP and GLOMAP both run without CUDA
  (slower). An aligned photo set is enough to measure facades accurately,
  so no GPU is needed for exact shapes.
- **Facade measurement:** project aligned photos onto fitted wall planes
  (rectified facades), then detect windows, doors and trim with
  facade-parsing or general segmentation models, checked in a review page.
- **Clean modeling:** Blender (headless Python and Geometry Nodes) with
  parametric style kits; hand finishing for hero detail. Studios rebuilding
  real places for games work this way: scans and photos measure, artists
  model.
- **Stylized look on Quest:** one palette atlas by material ID, baked AO
  and lightmaps. Very cheap to draw.

## The answer in one paragraph (original photoreal framing)

No single magic tool does this. The best results come from **stacking
three layers**: free government LiDAR for the exact shape of the ground and
buildings, a huge ground-level photo/360-video capture for detail and
texture, and AI reconstruction (Gaussian splatting plus mesh extraction) to
turn the photos into 3D. The photoreal splat version is the "jaw-drop"
result for PC trailers. The Quest version has to be meshes with baked
textures and levels of detail, because a standalone headset can only draw
about 50k–200k splats smoothly, and a campus is tens of millions.

## Hard limits found

| Limit | What it means |
|-------|---------------|
| **Google Earth / Google 3D Tiles can't be used in the game.** Google's terms forbid caching, offline use, exporting or scraping the tiles, and Cesium staff confirm there is no offline/standalone option for Quest. | Use them only to look at campus while planning, streamed live with credits shown. Don't trace or rip them. |
| **The Vanderbilt virtual tour is too sparse.** It's about 20 stops of 360° photos and videos, owned by the university. | Good for reference and checking colors; not enough views to reconstruct from, and not ours to reuse in the game. |
| **Students may not fly drones on campus** (Student Handbook); exceptions for teaching or research go through the Provost. Vanderbilt GIS offers drone mapping services. | Don't fly. Ask Vanderbilt GIS whether they have campus drone imagery, 3D models or CAD they'd share. That could be the single biggest shortcut. |
| **Vanderbilt's name and logos are protected trademarks**, licensed through CLC. | Using the name or logos in a released game needs permission. Buildings are generally fine to model; logos and the name are the risk. |
| **This laptop has integrated AMD graphics only.** Most splat trainers, MILo, Postshot and RealityScan meshing want an NVIDIA GPU. | Use a GPU elsewhere: Vanderbilt's research computing cluster (ACCRE) if you can get access, a rented cloud GPU (a few dollars per big job), or Brush, which runs on any GPU through WebGPU but is slow on a laptop chip. |
| **Quest 3 performance.** | Full campus as one photoreal mesh won't run. Needs hero areas in high detail, distant areas as simple shapes or images, and streaming. |
| **People and cars in photos.** | Blur faces and plates before sharing anything; mask moving people out of reconstruction. |

## Layer 1: exact shape, free

- **USGS 3DEP LiDAR, Davidson County 2022, Quality Level 1** (about 8
  points per square meter, covering the whole county). Free download from
  the USGS LidarExplorer / National Map Downloader as LAS/LAZ. Gives
  ground, tree canopy and building roofs to within centimeters.
- **Older 2011 Nashville LiDAR** (about 1 point/m²) for comparison.
- **OpenStreetMap** building footprints, paths, trees and names (free,
  ODbL license: credit OSM).
- **Nashville/Metro open data** (building footprints, tree canopy,
  orthophotos) where available.
- Use: terrain mesh, building massing and roof shapes, tree positions, and
  a georeferenced frame that every photo set aligns into.

## Layer 2: detail, captured by you

The capture plan that gives the most detail per hour:

1. **360 camera walk** (Insta360 X5 recommended by Scaniverse's docs; raw
   .insv works with newer tools): walk every path, slowly, at two heights.
   Covers the whole campus quickly. Tools: FreeGaussian (trains from raw
   .insv), 360 Gaussian Pro (rebuilt COLMAP, outputs to Brush, Postshot,
   LichtFeld), 360 Splat Pro (RealityScan meshing). Split panoramas into
   flat views and drop the top and bottom faces.
2. **Hero facades and details** with a phone or a camera: tens of
   thousands of overlapping photos, about 70–80% overlap, fixed exposure,
   overcast days (soft light = easier to relight later). Kirkland Hall,
   the library, Alumni Lawn, Peabody Esplanade, etc.
3. **Small details** (benches, lamps, signs, statues, doors) with phone
   LiDAR scanners (Scaniverse, Polycam, KIRI Engine) or as photo sets for
   AI image-to-3D.
4. **Material swatches**: flat, shadow-free photos of brick, stone, paving,
   bark and grass for tiling materials.

Capture tip from Scaniverse: walk the outside edge first, then fill the
inside with overlapping passes.

## Layer 3: reconstruction

### Aligning thousands of photos
- **RealityScan 2.0** (Epic, formerly RealityCapture): free for students
  and anyone under $1M revenue. Combines ground photos, aerial LiDAR
  (LAS/LAZ/E57) and drone images into one aligned, georeferenced model;
  GPU-accelerated alignment, AI masking, quality heat maps. Epic's Alcatraz
  project merged 2,805 drone images, ground control and aerial LiDAR this
  way. Best single free tool for this job.
- **COLMAP / GLOMAP**: free, open source, scriptable. Split very large sets
  into chunks.
- **New AI aligners** for hard sets with few shared features: VGGT-Long
  (kilometer-scale sequences), FastVGGT (1000+ images), SwiftVGGT, MUSt3R.
  Research code: impressive, but expect rough edges.

### Photoreal (the jaw-drop version)
- **Gaussian splatting** trainers: Brush (free, Apache 2.0, any GPU),
  Postshot (Jawset; scripting needs the paid tier), LichtFeld Studio, the
  Inria original and nerfstudio's gsplat (CUDA).
- **City-scale splatting** research: Hierarchical 3DGS (Inria), HUG
  (ICCV 2025, block-based with level of detail), CityGaussian v2,
  VastGaussian, BlitzGS (2026), and NVIDIA's open-source fVDB with NuRec
  splatting for city-wide scenes.
- Use for PC trailers, flythroughs and as the reference the game version
  is checked against.

### Game meshes (the Quest version)
- **RealityScan meshes** with textures, decimated per area.
- **MILo** (mesh-in-the-loop splatting, ACM TOG 2025): extracts meshes with
  about 10× fewer vertices than older methods, clean enough for physics.
  Needs its own UV and texture baking step. PGSR and 2DGS are alternatives.
- **Delighting** (remove the sun and shadows baked into photos so the game
  can light it): Agisoft Texture De-Lighter (free), Unity's old delighting
  tool, KIRI Engine 4.0's PBR mode.
- **Props from a few photos**: TRELLIS.2 (Microsoft, MIT license, PBR
  output, about 16 GB VRAM), Hunyuan3D 2.1 (good PBR; license excludes the
  EU, UK and South Korea), Pixal3D (MIT).

### Into Unity on Quest
- Split campus into tiles. Each tile has 3–4 levels of detail, with the
  furthest as simple boxes or images on cards (impostors).
- Bake lighting into textures or lightmaps; keep texture sizes in budget
  (atlases, ASTC compression).
- Simple invisible collision shapes separate from the visual mesh.
- Optional splats on Quest only for small hero moments (SplatXR measured
  about 72 fps at 100k splats on Quest 3; a 2M-splat file crashed Quest in
  World Labs' tests).
- Deadly Jenga's look is candy/plastic. Decide early whether the campus is
  photoreal, or real shapes with candy materials. Both use the same capture.

## What's surprising and worth trying

- **RealityScan merging your photos with the free 2022 county LiDAR**, so
  every photo snaps into real-world position and the buildings come out
  true to scale.
- **Training straight from raw 360 video** (FreeGaussian): a walk around
  campus becomes a photoreal splat with almost no setup.
- **AI aligners (VGGT family)** that work on photos ordinary tools can't
  match up.
- **Mesh-in-the-loop splatting (MILo)**: photoreal capture to light,
  game-usable mesh.
- **Image-to-3D (TRELLIS.2)** for hundreds of props from a few photos each.
- **Vanderbilt GIS** may already have drone imagery or building models.

## Sources

Shape-first tools:
- [CityGML (OGC)](https://www.ogc.org/standards/citygml/)
- [CGAL Shape Detection](https://doc.cgal.org/latest/Shape_detection/index.html)
- [PolyFit](https://github.com/LiangliangNan/PolyFit)
- [City3D](https://github.com/tudelft3d/City3D)
- [3dfier](https://github.com/tudelft3d/3dfier)
- [PDAL](https://pdal.io/)
- [COLMAP](https://colmap.github.io/)
- [GLOMAP](https://github.com/colmap/glomap)

Original research:

- [Cesium: Google Photorealistic 3D Tiles in Unity](https://cesium.com/learn/unity/unity-photorealistic-3d-tiles)
- [Cesium forum: offline VR not allowed](https://community.cesium.com/t/seeking-cesium-solutions-for-3d-gis-mapping-in-vr-with-offline-access/36282)
- [Cesium forum: Google tiles limitations](https://community.cesium.com/t/google-tiles-limitations/31034)
- [Cesium forum: offline data for Cesium Unity](https://community.cesium.com/t/offline-data-for-cesium-unity/25505)
- [Vanderbilt virtual tour](https://admissions.vanderbilt.edu/visit/virtual-tour/)
- [Vanderbilt Student Handbook: aerial devices](https://studenthandbook.vanderbilt.edu/node/880)
- [Vanderbilt GIS drone services](https://www.vanderbilt.edu/facilities/maps/drone/)
- [Vanderbilt trademark licensing](https://www.vanderbilt.edu/communications/trademark-licensing/)
- [USGS Davidson County 2022 LiDAR report](https://rockyweb.usgs.gov/vdelivery/Datasets/Staged/Elevation/metadata/TN_DavidsonCounty_D22/TN_DavidsonCo_1_2022/reports/TN_Davidson_County_Lidar2022_D22_Report_WU300056.pdf)
- [USGS 3DEP in Tennessee](https://pubs.usgs.gov/publication/fs20253037/full)
- [Tennessee LiDAR access](https://www.tn.gov/finance/news/2019/9/25/tennessee-makes-lidar-mapping-available--with-instructions.html)
- [RealityScan 2.0 released](https://radiancefields.com/realityscan-2-0-released)
- [RealityScan 2.0 aerial LiDAR support](https://www.geoweeknews.com/?p=41859)
- [RealityScan Alcatraz project](https://www.realityscan.com/spotlights/digitizing-the-rock-how-pete-kelsey-and-realityscan-2-0-brought-alcatraz-into-the-epic-ecosystem)
- [RealityScan is free (All3DP)](https://all3dp.com/4/realitycapture-is-now-realityscan-its-free-and-has-powerful-new-ai-features/)
- [FreeGaussian from raw Insta360](https://radiancefields.com/freegaussian-trains-gaussian-splats-from-raw-insta360-insv-files-and-ships-sim-ready-meshes)
- [360 Gaussian Pro](https://radiancefields.com/laskos-releases-360-gaussian-pro-with-a-rebuilt-colmap)
- [360 Splat Pro with RealityScan meshing](https://radiancefields.com/360-splat-pro-v1.2-adds-realityscan-meshing-gpu-controls-and-more)
- [Scaniverse 360 camera guide](https://ardk-dev-documentation.eng.nianticspatial.com/docs/scaniverse/360camera/)
- [Brush](https://radiancefields.com/platforms/brush)
- [PlayCanvas recommended splat tools](https://developer.playcanvas.com/user-manual/gaussian-splatting/creating/recommended-tools/)
- [HUG (ICCV 2025)](https://openaccess.thecvf.com/content/ICCV2025/papers/Su_HUG_Hierarchical_Urban_Gaussian_Splatting_with_Block-Based_Reconstruction_for_Large-Scale_ICCV_2025_paper.pdf)
- [Robust urban 3DGS (ICCV 2025)](https://openaccess.thecvf.com/content/ICCV2025/papers/Yuan_Robust_and_Efficient_3D_Gaussian_Splatting_for_Urban_Scene_Reconstruction_ICCV_2025_paper.pdf)
- [BlitzGS](https://arxiv.org/pdf/2605.13794)
- [City-scale 3DGS and fVDB](https://dl.acm.org/doi/10.1145/3799820.3812508)
- [MILo](https://arxiv.org/abs/2506.24096v2)
- [VGGT-Long](https://arxiv.org/html/2507.16443v2)
- [MUSt3R](https://huggingface.co/papers/2503.01661.md)
- [SwiftVGGT](https://arxiv.org/pdf/2511.18290v1)
- [SplatXR on Quest 3](https://radiancefields.com/splatxr-brings-interactive-gaussian-splats-to-quest-3)
- [World Labs: splats in Unity](https://docs.worldlabs.ai/marble/export/gaussian-splat/unity)
- [Unity VR Gaussian Splatting](https://github.com/ninjamode/Unity-VR-Gaussian-Splatting)
- [TRELLIS.2 vs Hunyuan3D vs Pixal3D](https://www.3daistudio.com/blog/pixal3d-vs-trellis-2-vs-hunyuan-3d-comparison)
- [AI 3D generation timeline 2026](https://app.cinevva.com/guides/ai-3d-model-generation-timeline-2026)
- [Agisoft Texture De-Lighter](https://cgpress.org/archives/agisoft-de-lighter-released.html)
- [KIRI Engine 4.0 PBR](https://beforesandafters.com/2025/09/08/kiri-engine-4-0-major-updates-in-photogrammetry-and-ai-powered-pbr/)
