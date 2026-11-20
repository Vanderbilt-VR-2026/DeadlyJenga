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
