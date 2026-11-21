# Stage 0: setup

Goal: every later stage has its data, folders, standards files and tools
ready, and nothing needs Evan except the items on his to-do list.

## Build

### Folders and config
- `Tools/VanderbiltCampus/` laid out as STANDARDS.md §13 describes, with
  `README.md` and pinned `requirements.txt`.
- `config/local.example.json` (committed) and `local.json` (git-ignored; add
  the ignore rule) with `data_root`. If no data drive exists, default to a
  folder on this PC and add "move archive to a drive with ≥ 500 GB free" to
  `EVAN-TODO.md`.
- Archive skeleton and `manifest.sqlite` (STANDARDS.md §8) plus
  `pipeline/manifest.py` with `add`, `list` and `verify`.
- `progress/` files: DECISIONS, EVAN-TODO, CROSS-BOX-CHANGES, HANDOFF.

### Campus frame and boundary
- `config/boundary.geojson`: main campus including Peabody, from OSM, with
  the 150 m backdrop ring as a second polygon.
- `config/frame.json`: origin, EPSG codes and transforms (STANDARDS.md §1),
  plus `tests/frame_points.json` (10 known points) and a test that passes.

### Data downloads (scripted, checksummed, into `raw/`)
- USGS 3DEP **TN Davidson County 2022 (QL1)** LAZ tiles covering the
  boundary and ring, from The National Map / the USGS rockyweb staging
  folder. Record the tile index, EPSG and vertical datum from the metadata.
- The 2011 Nashville LiDAR tiles for the same area (comparison only).
- OSM extract for the boundary + ring (Overpass or a Geofabrik Tennessee
  extract clipped), dated.
- Metro Nashville open data: building footprints, tree canopy, the most
  recent orthophoto covering campus, if published (log what exists and the
  license).
- Each source's license and attribution text goes into the manifest and a
  `Tools/VanderbiltCampus/ATTRIBUTION.md` (OSM: © OpenStreetMap
  contributors, ODbL).

### Tools
- Installed and recorded with versions in `Tools/VanderbiltCampus/README.md`:
  Python 3.11+, PDAL (+ python bindings), GDAL/rasterio, laspy, Open3D,
  Shapely, pyproj, COLMAP and GLOMAP (CPU builds), Blender (headless; record
  version), FFmpeg, ExifTool, CGAL-based tools as needed (PolyFit/City3D,
  3dfier) or their Python equivalents.
- Optional, written down but not required: RealityScan (needs Evan's Epic
  login; to-do), Brush, GPU tools for Stage 6.
- If a tool won't install on this Windows PC, try WSL or conda; log the
  working route.

### Standards files
- `config/material_ids.json`: the v1 list with a starting candy color per
  ID and an index number.
- Decide palette encoding (UV1 swatch or vertex-color index; STANDARDS.md
  §7) and log it.
- Placeholder player-facing map name (STANDARDS.md §12).
- Hero list confirmed against OSM names (unknown ones flagged).

### Evan items (to-do, with exact steps)
- Draft email to Vanderbilt GIS / Facilities asking for existing drone
  orthophotos or photogrammetry, building 3D models, CAD or elevations, and
  their terms of use. Short, student-project framing, written out ready to
  send. Evan sends it; the builder does not.
- Ask about ACCRE access (for Stage 6).
- Optional: sign up for RealityScan.
- Trademark question to Vanderbilt licensing: draft only.

## Done when
1. All downloads are in `raw/`, in the manifest, and `manifest.py verify`
   passes.
2. `frame.json` test passes (10 points within 1 cm).
3. A top-down render of LiDAR heights over the boundary and the orthophoto
   is in `progress/STAGE-0-results.md`, with the boundary drawn.
4. Email drafts are in `EVAN-TODO.md`.
5. `Tools/VanderbiltCampus/tests` run green.
