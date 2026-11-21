# Stage 0 results

Date: 2026-11-19. Archive: `C:\Users\evanp\VanderbiltCampusData` (`data_root`). Commands in run order:
`Tools/VanderbiltCampus/README.md`, section "Stage 0: commands in run order".

## 1. What was downloaded (`raw/`, from the manifest)

| Source | Folder in `raw/` | Files | Size | License |
|--------|------------------|------:|-----:|---------|
| OpenStreetMap (Overpass), dated snapshot | `osm/2026-11-19/` | 4 | 5.6 MB | ODbL 1.0, © OpenStreetMap contributors |
| USGS 3DEP TN_DavidsonCounty_D22 (2022 QL1), EPT copy cropped to campus + ring | `lidar/TN_DavidsonCounty_D22/EPT/` | 2 | 362.8 MB | Public domain (USGS) |
| USGS 3DEP D22 original LAZ tiles | `lidar/TN_DavidsonCounty_D22/LAZ/` | 10 of 10 (finished 2026-11-20) | 292 MB | Public domain (USGS) |
| USGS 3DEP D22 metadata, reports, tile index | `lidar/TN_DavidsonCounty_D22/metadata/` | 9 | 11.0 MB | Public domain (USGS) |
| USGS 3DEP TN_NASHVILLE_2011, EPT crop (comparison only) | `lidar/TN_NASHVILLE_2011/EPT/` | 2 | 25.7 MB | Public domain (USGS) |
| USGS 3DEP 2011 metadata | `lidar/TN_NASHVILLE_2011/metadata/` | 7 | 2.4 MB | Public domain (USGS) |
| Metro Nashville building footprints | `metro/2026-11-19/footprints/` | 4 | 1.5 MB | Metro open data, no explicit license (EO FO-018) |
| Metro Nashville tree canopy 2021 | `metro/2026-11-19/canopy/` | 6 | 3.5 MB | Metro open data, no explicit license (EO FO-018) |
| Metro Nashville 2026 orthophoto, 0.15 m | `metro/2026-11-19/ortho_2026/` | 169 | 462.5 MB | Not stated: reference only, never shared |
| USDA NAIP 2021 via USGS, 0.6 m | `metro/2026-11-19/naip/` | 13 | 26.9 MB | Public domain |

The manifest also lists Stage 2 stand-in capture samples under `raw/capture/`, added by the capture
worker (KartaView CC BY-SA 4.0, a Zenodo CC BY 4.0 set). They are outside Stage 0 and are credited in
`ATTRIBUTION.md`.

## 2. Campus frame (`Tools/VanderbiltCampus/config/frame.json`, `pipeline/frame.py`)

- Axes: X = grid east, Y = grid north (EPSG:6576 grid north), Z = up, meters. Blender uses (X, Y, Z).
  Unity = (X, Z, Y).
- Origin: center of **Alumni Lawn**, OSM **way/259811259** (`leisure=park`, `name=Alumni Lawn`). This is
  the area centroid computed in EPSG:6576, rounded to 0.01 ftUS.
  - WGS84: lat 36.147457290, lon -86.803953057
  - EPSG:6576: E0 = 1,731,135.91 ftUS, N0 = 661,330.12 ftUS
  - Ground: 168.660 m NAVD88 (553.345 ftUS). This is the median of the 422 LiDAR class-2 points within 3 m.
  - Campus Z of the origin: 26.42 m
- Horizontal: `X = (E − E0) × 0.3048006096012192 / k`, and the same for Y. No rotation.
  - k = **0.999941553295** (grid scale factor 0.999963314240 × elevation factor 0.999978238256)
  - Elevation factor = R / (R + h). R = 6,371,593.352 m (GRS80 Gaussian mean radius at the origin).
    h = 138.66 m ellipsoid height = 168.66 m NAVD88 + an assumed GEOID18 height of −30.0 m. The GEOID18
    grid is not installed; a 1 m error in h changes k by 1.6e-7, which is 0.016 mm per 100 m.
  - Meridian convergence at the origin: −0.470666° (information only; grid north is kept)
  - The NAD83(2011) ↔ WGS 84 step is PROJ's null transform, the same one the USGS EPT copy used.
- Vertical: `Z = H_ftUS × ftUS − Z0`, with **Z0 = 142.24 m**.
  - Lowest ground point inside boundary + ring: 144.24 m NAVD88 at E 1,732,836.52, N 662,521.83 ftUS,
    near 21st Ave S / West End in the ring. Its campus Z is **2.00 m**; the 0.1 percentile is 3.22 m.
  - Ground Z range in boundary + ring: **2.00 – 59.53 m**. Inside the campus polygon only (DTM): 6.54 – 47.42 m.
    All walkable ground is at Unity y ≥ 2.
- **Finding: the EPT copies store Z in meters, not US survey feet.** The D22 EPT header range of 56.78 m
  matches the original tile's 186.27 ftUS, and same-pulse Z agrees to a median of 2.5 mm. So
  `to_campus(..., 3857)` defaults to Z in meters (`z_unit="ftUS"` is available as a flag). `fetch_lidar.py`'s
  docstring ("Z in the project's original vertical units") is wrong for D22, but nothing reads it.
- Tiles: 100 m, col = floor(X/100), row = floor(Y/100). The name is `T_<col>_<row>`, where each number is
  an optional `-` followed by at least two digits of its absolute value: `T_03_-02`, `T_-01_00`,
  `T_123_-01`. This is not C's `%02d`, which would print `-2`.
- Tests: `tests/frame_points.json` has 10 points: the origin, the 4 boundary + ring extremes, corners of
  Kirkland Hall, Cohen Memorial Hall and Furman Hall, one D22 EPT point and one original tile 092150 point.
  `tests/make_frame_points.py` computes the expected values independently: it uses a longhand LCC PROJ
  string in meters, a hand-written web-mercator inverse and the frame formula, and never calls frame.py.
  All 10 agree within 1 cm (actual: < 0.1 mm), and round trips agree within 1 mm.

## 3. Working LiDAR (`derived/lidar/v1/`, `pipeline/prepare_lidar.py`, `pipeline/lidar_io.py`)

- Source: the D22 EPT crop, 113,677,080 points. Every point is in it exactly twice.
- Kept: tiles touching boundary + ring grown by 20 m. That is **310 tiles** with data: **203 campus,
  93 ring, 14 outside** (within the 20 m margin only). 4 more tiles in the margin
  (T_04_05, T_10_-05, T_10_-06, T_10_-07) lie outside the EPT crop box and have no points; they are
  listed in `index.json` under `empty_tiles`.
- Points in the kept tiles: 91,538,218 before de-duplication; **45,769,109 unique** after removing
  45,769,109 exact duplicates (same X, Y, Z, gps_time, return_number). The whole crop has 56,838,540
  unique points; the rest fall in the crop box corners outside the kept tiles.
- Unique points by class (kept tiles): 1 unclassified 10,748,479 · 2 ground 15,598,067 · 3 low veg
  533,981 · 4 medium veg 1,238,727 · 5 high veg 6,648,563 · 6 building 10,951,256 · 7 low noise 40,797 ·
  18 high noise 9,239. Classes 9 and 20 appear only outside the kept tiles.
- Density (points/m², no noise, 0.5 m cells): campus polygon mean **16.2** (median 12.0); ring mean
  13.9 (median 12.0). Ground was measured in 60.6 % of campus cells; the rest is under buildings or
  trees and was filled.
- File format: LAZ 1.4, point format 6, scale 0.001 m, offset = the tile's SW corner, campus-frame
  meters. There is no CRS WKT; a `DeadlyJenga` text VLR describes the frame. 193 MB in total.
- `lidar_io.load_points(xmin, ymin, xmax, ymax, classes=None, buffer=0)` returns numpy arrays (x, y, z,
  classification, intensity, return_number, number_of_returns, gps_time) from the tiles touching the box.
- 2011 set: one file, `derived/lidar/v1_2011/TN_NASHVILLE_2011_campus.laz`, 4,859,595 points (no
  duplicates in that crop; all class 0; Z in meters).

### EPT copy vs the original tile 092150 (`reports/stage0/ept_vs_original.json`)

Both datasets were converted to the campus frame. The original went through the 6576 path (ftUS), the
working tiles through the 3857 path. They were compared inside the overlap of the original tile, the
EPT crop box and the working tiles, shrunk by 1 m (221,308 m²).

| Check | Result |
|-------|--------|
| Point count | original 2,840,513 · working 2,840,540 (+27, 0.001 %; points on the overlap edge) |
| Per class | same within 12 points per class (for example ground 1,071,739 vs 1,071,747) |
| Nearest neighbour, working → original (3-D) | median **4.30 mm**, p95 6.14 mm, p99 6.73 mm, 100.0 % ≤ 10 mm, 5.1 % ≤ 2 mm |
| Nearest neighbour, original → working | median 4.30 mm, p95 6.14 mm, p99 6.73 mm, 100.0 % ≤ 10 mm |
| Same pulse (gps_time + return), 2,644,100 matched | horizontal median 3.24 mm, p95 5.00 mm; vertical median 2.52 mm, p95 4.80 mm |

**The ≤ 2 mm target is not met, and cannot be met with this source.** The EPT copy stores X/Y at
0.01 m in web mercator, which is about 8 mm on the ground at this latitude, and Z at 0.01 m. The original
stores 0.01 ftUS. The offsets are pure rounding: there is no bias (mean horizontal offset 0.2 / 0.1 mm),
and no point is more than 1 cm off except a few hundred edge or gps-time-collision cases. This is far
below every Stage 1 tolerance (≥ 15 cm). The original tiles replace the EPT copy when they land (D007).

## 4. Rasters for everyone (`derived/rasters/v1/`, `derived/ortho/v1/`, `pipeline/make_rasters.py`)

Grid: boundary + ring + 20 m, snapped to whole meters. Every raster and mosaic shares the top-left corner X = −1123, Y = 520; bounds
X −1123 … 1016, Y −1345 … 520 (campus m). Each `.tif` has a JSON sidecar with the origin, cell size,
size and units. The `.tif` files carry the same transform and no CRS.

| File | Size | Notes |
|------|------|-------|
| `dtm.tif` | 4278 × 3730, 0.5 m, float32 | lowest class-2 point per cell; holes filled smoothly (push-pull pyramid fill) |
| `dtm_measured.tif` | same, uint8 | 1 = measured, 0 = filled |
| `dsm.tif` | same, float32 | highest first return per cell, no class 7/18; holes filled |
| `ndsm.tif` | same, float32 | DSM − DTM, ≥ 0 |
| `building_mask.tif` / `highveg_mask.tif` | same, uint8 | class 6 / class 5 point in the cell |
| `density.tif` | same, float32 | points/m² (no class 7/18) |
| `metro2026_campus_0.15m.tif` | 14260 × 12433, 0.15 m RGB | Metro 2026, reference only, archive only |
| `naip2021_campus_0.6m.tif` | 3565 × 3108, 0.6 m RGB | NAIP, public domain |

The masks are stored as uint8 rather than float32 (smaller, exact 0/1). The orthophotos were resampled
bilinearly from EPSG:32616 through `frame.from_campus`.

## 5. Stage 0 result images

LiDAR heights: nDSM colors over a DSM hillshade. Shown with the campus boundary (yellow), the 150 m ring
(cyan), 100 m tiles with their names, and the origin. Cells more than 3 m from any LiDAR point are dark.

![LiDAR heights, campus frame](images/stage0_lidar_height.jpg)

The same lines over NAIP 2021 (0.6 m). Buildings, stadium and streets line up with the LiDAR picture.

![NAIP 2021 with boundary and tiles](images/stage0_naip2021.jpg)

The Metro 2026 version (`reports/stage0/stage0_metro2026.jpg`, 8000 px) stays in the archive only,
because its license is not stated. The full-size LiDAR and NAIP versions are in `reports/stage0/`.

## 6. Hero list check (`Tools/VanderbiltCampus/config/hero_spots.json`, `pipeline/hero_spots.py`)

| Spot (STANDARDS §3) | Status | OSM name | OSM id | Building id | Centroid X, Y (m) | Tile |
|---|---|---|---|---|---|---|
| Kirkland Hall | ok | Kirkland Hall | way/544397715 | B_544397715 | 104.6, 83.1 | T_01_00 |
| Kirkland Hall tower | **unknown** | - | - | - | - | - |
| Alumni Lawn | ok | Alumni Lawn | way/259811259 | - | 0.0, 0.0 | T_00_00 |
| Central Library | alias | Jean & Alexander Heard Library | way/544413710 | B_544413710 | 331.7, -196.0 | T_03_-02 |
| Library Lawn | ok | Library Lawn | way/1050096525 | - | 236.5, -185.9 | T_02_-02 |
| Peabody Esplanade | **point** | Peabody Esplanade | node/356898107 | - | 450.4, -670.7 | T_04_-07 |
| Cohen Memorial Hall | ok | Cohen Memorial Hall | way/486160405 | B_486160405 | 404.4, -659.4 | T_04_-07 |
| Furman Hall | ok | Furman Hall | way/261715434 | B_261715434 | 235.9, 53.7 | T_02_00 |
| Benson Hall | alias | Benson Science Hall | way/544413708 | B_544413708 | 170.4, -40.5 | T_01_-01 |
| Wyatt Center | ok | Wyatt Center | way/234871501 | B_234871501 | 438.4, -832.6 | T_04_-09 |
| Commons | **ambiguous** | Commons Center (used) | way/370608816 | B_370608816 | 610.0, -633.8 | T_06_-07 |
| Rand Hall | ok | Rand Hall | way/1465205768 | B_1465205768 | 80.6, -114.7 | T_00_-02 |
| Rand Wall | **unknown** | - | - | - | - | - |
| Engineering and Science Building | alias | Science and Engineering Building | way/461630342 | B_461630342 | -176.2, -496.0 | T_-02_-05 |
| Featheringill/Jacobs Hall | ok | Featheringill / Jacobs Hall (one outline) | way/178414029 | B_178414029 | 72.7, -297.3 | T_00_-03 |
| Student Life Center | ok | Student Life Center | way/544597207 | B_544597207 | -176.7, -339.1 | T_-02_-04 |
| Branscomb Quad | **group** | Vaughn, Lupton, Scales, Stapleton Houses + Branscomb Quad Market | way/544597204, relation/14043732, way/544597202, way/1052134886, way/544597206 | (members) | -105.3, -284.6 | T_-02_-03 |
| Memorial Gymnasium exterior | ok | Memorial Gymnasium | way/544597209 | B_544597209 | -304.5, -325.4 | T_-04_-04 |

Flags for Stage 2:
- **Kirkland Hall tower**: OSM has no separate tower or `building:part`; the tower sits inside
  way/544397715. Stage 1 finds it in the nDSM.
- **Rand Wall**: not in OSM (no named or tagged wall within 80 m of Rand Hall). Locate it on site or from
  Vanderbilt's map.
- **Commons**: candidates are Commons Center (building, used), The Martha Rivers Ingram Commons
  (node/9823385283) and Commons Center Lawn (node/9823385284).
- **Peabody Esplanade**: OSM has only a point (node/356898107, `leisure=park`), not an outline.
- **Branscomb Quad**: OSM has no outline for the quad; it is a group of the four houses plus the market.

## 7. Done when (STAGE-0-Setup.md)

1. **Downloads in `raw/`, in the manifest, verify passes.** Partly done. `manifest.py verify`:
   `verify: 1291 ok, 0 problem(s)` (the count includes files other workers added). Only 1 of the 10 original D22 LAZ tiles (092150) is in `raw/`.
   The other 9 are still downloading in the background (rockyweb HTTP 429, about one tile per
   30–40 minutes; 092160 is in progress as a `.part` file). Per DECISIONS D007, the working copy is the
   USGS EPT crop of the same D22 project. It is de-duplicated and tiled in `derived/lidar/v1/`, and agrees
   with tile 092150 to point-count parity and a median of 4.3 mm (section 3). Run
   `prepare_lidar.py --skip-tiles` after each new tile lands to extend the comparison.
2. **frame.json test passes (10 points within 1 cm).** Done: all 10 points within 1 cm (actual < 0.1 mm);
   round trips within 1 mm; tile names tested, including negatives.
3. **Top-down render of LiDAR heights over the boundary, and the orthophoto, in this file.** Done (section 5).
4. **Email drafts in `EVAN-TODO.md`.** Done in the earlier Stage 0 work (GIS/Facilities request,
   ACCRE, RealityScan, trademark).
5. **`Tools/VanderbiltCampus/tests` run green.** Done: `python -m pytest Tools/VanderbiltCampus/tests -q`
   gives `50 passed, 1 warning`. The warning is a deprecation notice from the `dash` pytest plugin in the
   venv, not from our code.


## Update 2026-11-20: all original tiles
All 10 original D22 LAZ tiles are now in `raw/` and in the manifest. The working copy (EPT crop) was compared with all of them (`reports/stage0/ept_vs_original_all.log`): nearest-neighbor distance median 4.3 mm, p95 6.1 mm, 100 % within 10 mm; same-pulse height difference median 2.5 mm. The working copy stays as it is (D007).
