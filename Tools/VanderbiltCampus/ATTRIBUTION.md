# Data sources and attribution

Every downloaded file is also listed in the archive manifest (`manifest.sqlite`)
with its source URL, license and date. Fetched 2026-11-19 by the Stage 0 scripts
unless a row says otherwise. No Google Earth, Google Maps or Google 3D Tiles
content is used anywhere.

## OpenStreetMap
- What: buildings and building parts (heights, levels, roof tags), roads and
  paths, landuse/leisure/natural, parking, fountains, trees, barriers, names.
- Where: Overpass API (https://overpass-api.de/api/interpreter; fallback public
  instance https://overpass.private.coffee/api/interpreter, same OSM data).
- Script: `pipeline/fetch_osm.py` -> `raw/osm/<date>/`.
- License: Open Database License (ODbL) 1.0, https://opendatacommons.org/licenses/odbl/
- Required attribution: **© OpenStreetMap contributors**
  (https://www.openstreetmap.org/copyright). Derived databases must stay ODbL;
  produced works (the game map) must show the credit.
- `config/boundary.geojson` is derived from this data (ODbL).

## USGS 3DEP LiDAR: TN_DavidsonCounty_D22 (Davidson County 2022, QL1)
- Where: The National Map API (https://tnmaccess.nationalmap.gov/api/v1/products),
  tiles from https://rockyweb.usgs.gov/vdelivery/Datasets/Staged/Elevation/LPC/Projects/TN_DavidsonCounty_D22/TN_DavidsonCo_1_2022/,
  metadata and reports from https://prd-tnm.s3.amazonaws.com/StagedProducts/Elevation/metadata/TN_DavidsonCounty_D22/TN_DavidsonCo_1_2022/
- Script: `pipeline/fetch_lidar.py --project TN_DavidsonCounty_D22` -> `raw/lidar/TN_DavidsonCounty_D22/`.
- License: public domain (U.S. Government work).
- Suggested credit: **Lidar data: U.S. Geological Survey, 3D Elevation Program (3DEP).**

## USGS 3DEP LiDAR: TN_NASHVILLE_2011 (comparison only)
- Where: points from the USGS 3DEP Entwine Point Tiles copy on AWS Open Data
  (https://s3-us-west-2.amazonaws.com/usgs-lidar-public/TN_Nashville_2011/ept.json),
  cropped to campus + ring with PDAL. The original staging tiles
  (https://rockyweb.usgs.gov/vdelivery/Datasets/Staged/Elevation/LPC/Projects/legacy/TN_NASHVILLE_2011/,
  four strips totalling about 3 GB) were not downloaded because the staging
  server was rate-limiting (HTTP 429). Metadata from
  https://prd-tnm.s3.amazonaws.com/StagedProducts/Elevation/metadata/legacy/TN_NASHVILLE_2011/
- Script: `pipeline/fetch_lidar.py --project TN_NASHVILLE_2011 --source ept` -> `raw/lidar/TN_NASHVILLE_2011/`.
- License: public domain. Credit: **U.S. Geological Survey.**

## Metro Nashville: Building Footprints
- Where: Nashville Open Data, ArcGIS item 193c44177c814ac7ab3a605c5a407b05,
  https://services2.arcgis.com/HdTo6HJqh92wn4D8/arcgis/rest/services/Building_Footprints_view/FeatureServer/0
- Script: `pipeline/fetch_metro.py --only footprints` -> `raw/metro/<date>/footprints/`.
- License: no explicit license; the item's license field points to Metro's Open
  Data Executive Order FO-018
  (https://www.nashville.gov/departments/metro-clerk/legal-resources/executive-orders/mayor-freddie-oconnell/fo018).
  Contact: opendata@nashville.gov.
- Credit: **Metropolitan Government of Nashville and Davidson County (Nashville Open Data).**

## Metro Nashville: Urban Tree Canopy 2021
- Where: Nashville Open Data, ArcGIS item 702e9efe5b4f4ccf90c267c5c3a4ba87,
  https://services2.arcgis.com/HdTo6HJqh92wn4D8/arcgis/rest/services/Urban_Tree_Canopy_2021_view/FeatureServer/0
- Script: `pipeline/fetch_metro.py --only canopy` -> `raw/metro/<date>/canopy/`.
- License and credit: as Building Footprints (Executive Order FO-018).

## Metro Nashville: orthoimagery (2026)
- Where: https://maps.nashville.gov/arcgis/rest/services/Imagery/2026Imagery_WGS84/MapServer
  (export at 0.15 m/px, EPSG:32616).
- Script: `pipeline/fetch_metro.py --only ortho` -> `raw/metro/<date>/ortho_2026/`.
- License: **not stated** in the service. Not listed on the Nashville Open Data
  portal. Earlier imagery years in the same folder credit "Metro GIS; Pictometry
  International". Treat as reference only (alignment checks), not as game
  texture, until Metro confirms terms.
- Credit (when shown): **Imagery: Metro Nashville GIS.**

## USDA NAIP via USGS The National Map (fallback orthophoto)
- Where: https://imagery.nationalmap.gov/arcgis/rest/services/USGSNAIPImagery/ImageServer
  (export at 0.6 m/px, EPSG:32616).
- Script: `pipeline/fetch_metro.py --only naip` -> `raw/metro/<date>/naip/`.
- License: public domain. Credit: **USDA Farm Production and Conservation
  Business Center (NAIP); served by USGS The National Map.**

## Stand-in capture photos (Stage 2/3 samples)

Public photos used only to test intake, alignment and measurement until Evan's
own capture exists. Script: `pipeline/fetch_samples.py` (stage 2 in the
manifest). Per-file author, license and source URL are in the `licenses.json`
/ `sequence.json` / `record.json` files named below and in the manifest's
license column. **CC BY-SA items require author credit and share-alike:** any
texture, image or other adaptation made from them must be released under the
same CC BY-SA license with credit, so prefer them for alignment/measurement
(facts, not copied pixels) and do not bake them into game textures without
deciding that first. CC BY items need credit only. Faces/plates: blur in
`derived/` before any sharing (STANDARDS.md §9).

### Wikimedia Commons: Vanderbilt campus photos
- Where: MediaWiki API https://commons.wikimedia.org/w/api.php (categories
  "Kirkland Hall", "Vanderbilt University structures" and siblings, plus every
  geotagged file inside the campus box); originals from upload.wikimedia.org.
- Script: `pipeline/fetch_samples.py commons` -> `raw/capture/commons-<date>/wikimedia/<category>/`.
- Licenses accepted: public domain, CC0, CC BY, CC BY-SA (no NC/ND). Per-file
  author, license, license URL, page URL, date taken and GPS:
  `raw/capture/commons-<date>/licenses.json` (also one per category folder).
- Credit: **"<file title>" by <author>, <license>, via Wikimedia Commons**, per file.

### KartaView street-level imagery
- Where: public API https://api.kartaview.org/2.0 (no account); images are
  KartaView's processed copies with faces and plates blurred by KartaView.
- Script: `pipeline/fetch_samples.py kartaview` -> `raw/capture/kartaview-<date>/<sequence>/`
  with `sequence.json` (lat, lon, heading, time, size per photo); summary in
  `derived/capture/kartaview-<date>/summary.json`.
- License: CC BY-SA 4.0 (https://creativecommons.org/licenses/by-sa/4.0/).
  Credit: **Imagery © KartaView contributors, CC BY-SA 4.0** (user ids in sequence.json).

### Wikimedia Commons: 360 video sample
- File: "3-mins-in-360 on The Old Ghost Road.webm" (outdoor trail walk,
  4096x2048 equirectangular), https://commons.wikimedia.org/wiki/File:3-mins-in-360_on_The_Old_Ghost_Road.webm
- Script: `pipeline/fetch_samples.py video360` -> `raw/capture/commons-<date>/video360/`
  (original) and a 45 s stream copy in `derived/capture/commons-<date>/video360/`.
- License: CC BY-SA 3.0; author and credit in `video360/licenses.json`.
- Status 2026-11-19: **not downloaded yet** (upload.wikimedia.org answered
  HTTP 429 to this client; see the Commons note below).

Note (2026-11-19): Wikimedia's User-Agent policy asks automated clients for
contact details in the User-Agent. Our generic agent got the metadata but
upload.wikimedia.org refused almost every original file (HTTP 429). Only 1
Commons photo is on disk; the full candidate list with licenses is in
`derived/capture/commons-<date>/api_cache.json` and `licenses-progress.json`.
`raw/.../licenses.json` is written once a run finishes without failures.

### Zenodo 16576623: Paganico street-facade photogrammetry set (fallback)
- "Photogrammetry Photo Dataset of Historic Architecture in Paganico, Italy",
  Maria Legut-Pintal, 2025, 65 JPGs (Canon EOS RP, 50 mm), https://zenodo.org/records/16576623
- Script: `pipeline/fetch_samples.py fallback` -> zip in `raw/capture/fallback-<date>/zenodo-16576623/`
  with `record.json`; images unpacked to `derived/capture/fallback-<date>/zenodo-16576623/images/`.
- License: CC BY 4.0 (record license field "cc-by-4.0"). Credit: **Maria
  Legut-Pintal, Photogrammetry Photo Dataset of Historic Architecture in Paganico, Italy, Zenodo, CC BY 4.0.**

### Checked and not used
- Mapillary: the Graph API needs an access token (account). Optional Evan to-do.
- Flickr: the search API needs a key; the keyless public feed has no license
  field, so licenses cannot be checked. Not used.
- COLMAP sample datasets (UNC buildings): no license stated. Not used.
- CRBeDaSet (Mendeley Data 9nvn27yw62): CC BY-NC 3.0. Not used (NC).
