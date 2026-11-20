# Decisions

Each entry: choice, alternatives, why. Newest at the bottom. Owner: Vanderbilt-Main.

## D001 Archive location (Stage 0)
- Choice: `C:\Users\evanp\VanderbiltCampusData` (set in `config/local.json`).
- Alternatives: `D:\VanderbiltCampusData` (no D: drive on this PC); inside the repo (forbidden).
- Why: only C: exists, with 314 GB free. Moving to a drive with >= 500 GB free is on EVAN-TODO.

## D002 One chat runs all stages
- Choice: this chat (Main) runs Stages 0-5 in order without stopping between them; Evan reviews at the end.
- Why: Evan's instruction. Stage "Done when" checks still gate each next stage.

## D003 Palette encoding: vertex color index (Stage 0)
- Choice: every exported vertex carries its material index in vertex color R (`index/255`, G/B/A = 1);
  submeshes are also named `mat_<id>`. Index list in `Tools/VanderbiltCampus/config/material_ids.json`.
- Alternatives: UV1 pointing at a palette swatch (STANDARDS §7 allows either).
- Why: Unity uses UV1 (mesh `uv2`) for lightmaps, and Stage 4/5 bake lightmaps per tile. Vertex color keeps
  UV0 for the bake and UV1 for lightmaps, with nothing repurposed. Submesh names still let Presentation pick
  materials directly if it prefers.

## D004 Starting palette (Stage 0)
- Choice: two colors per ID in `material_ids.json`: `candy` (Deadly Jenga glossy candy/plastic, the default
  look) and `plain` (true-to-life flat colors for the Stage 5 comparison look). Gray fallback `#9A9A9A` when
  Presentation is removed. Presentation owns the final values.

## D005 Player-facing map name (Stage 0)
- Choice: "Campus" (lobby title "Campus (LoD2)" while massing only). Data and editor tools may use
  "Vanderbilt" and OSM building names; nothing player-facing does until Evan records a trademark decision
  (STANDARDS §12).

## D006 Campus boundary (Stage 0)
- Choice: OSM way/131042264 "Vanderbilt University" (the only amenity=university polygon; Peabody and the
  Medical Center lie inside it), closed by 20 m, plus whole street blocks that are >= 75% campus.
  Campus 1,603,003 m2; 150 m backdrop ring 964,158 m2.
- Alternatives: cut out the Medical Center (not a separate polygon in OSM; keeping it loses nothing).
- Why: keeps the most area; the shot list (Stage 2) marks hospital buildings as background priority.

## D007 Working LiDAR copy while USGS throttles the original tiles (Stage 0)
- Choice: Stage 1 works from the USGS public EPT copy of TN_DavidsonCounty_D22 cropped to campus + ring
  (`raw/lidar/TN_DavidsonCounty_D22/EPT/`), de-duplicated (the crop has every point twice) and converted
  to the campus frame under `derived/lidar/`. The 10 original LAZ tiles keep downloading in the background
  (rockyweb answers HTTP 429; about one tile per 30-40 minutes) and are compared with the EPT copy when
  complete.
- Alternatives: wait hours for the tiles before starting Stage 1.
- Why: same USGS data, same points; nothing is lost, and the originals still land in `raw/`.

## D008 2011 LiDAR and orthophoto sources (Stage 0)
- 2011 Nashville LiDAR: EPT crop only (6.18 M points, all class 0, 1.6 pts/m2); the 3 GB staging strips
  were skipped because of the rate limit. Comparison only, so nothing depends on it.
- Orthophoto: Metro Nashville 2026 imagery exported at 0.15 m/px (license not stated: reference only,
  never shipped, never leaves this machine) and USGS NAIP 2021 at 0.6 m (public domain) as the
  shareable fallback. Overlays in results files that might be shared use NAIP.
- Metro footprints (with Height and RoofType) and 2021 canopy: Metro open data, no explicit license text
  (Executive Order FO-018); used for cross-checks, credited in ATTRIBUTION.md.

## D009 Stand-in capture sets (Stages 2-4, until Evan's capture exists)
- Found: public photos cannot cover Kirkland Hall. Wikimedia Commons has 13 modern Kirkland photos from
  different years and cameras (not alignable as a set); KartaView (CC BY-SA 4.0) has 353 street images along
  21st Ave S / the east edge of campus (closest 313 m from Kirkland); Mapillary and Flickr need accounts.
- Choice: (a) intake and alignment are proven on the KartaView 21st Ave S sequences, which lie on the campus
  LiDAR, so registration to LiDAR is tested for real; (b) the Zenodo 16576623 building set (CC BY 4.0, 65
  photos) is the well-overlapped control set for the alignment code; (c) Stage 4's measured LoD3 building is
  one the KartaView images see; Kirkland Hall gets its LoD3+ model from LiDAR plus the Commons photos as
  visual reference only, flagged "not photo-measured" with its needed shots at the top of EVAN-TODO.
- Alternatives: stop Stage 4 until Evan captures Kirkland (the brief says not to stop).
- Why: keeps every stage's code proven on real, licensed, on-campus data, and reports honestly what waits
  on the capture.

## D010 Campus frame v1 (Stage 0)
- Origin: centroid of OSM way/259811259 "Alumni Lawn" (lat 36.147457290, lon -86.803953057; E0 1,731,135.91,
  N0 661,330.12 ftUS in EPSG:6576). X = grid east, Y = grid north (no rotation; convergence -0.4707 deg
  recorded), Z up, meters. Unity (x, y, z) = (X, Z, Y).
- Scale: grid feet to ground meters with the combined factor k = 0.999941553295 (grid 0.999963314 x
  elevation 0.999978238), so campus distances are true ground meters (5.8 cm per km differs from grid meters).
- Vertical: Z = NAVD88 meters - 142.24, which puts the lowest ground in campus + ring at Z = 2.00 m, so all
  walkable ground is above Unity y = 0 (bots die below y = -6). The STANDARDS note "map root at the world
  origin" holds; no extra Unity offset is needed.
- The EPT copy stores Z in meters (the original tiles use ftUS); `frame.py` handles both.
- EPT positions are rounded to 1 cm (median 4.3 mm from the original tile 092150, same points, same
  classes). Far inside Stage 1 tolerances; a `derived/lidar/v2` from the original tiles can replace it later
  without changing the frame.

## D011 Building IDs
- OSM ways: `B_<way id>`; OSM multipolygon relations: `B_r<relation id>`; not in OSM: `B_new_<n>`.
- Why: way and relation ids overlap in OSM, and STANDARDS §6 only says `B_<osm_id>`.

## D012 Hero list against OSM (Stage 0)
- Found under other OSM names: Central Library = "Jean & Alexander Heard Library"; Benson Hall = "Benson
  Science Hall"; Engineering and Science Building = "Science and Engineering Building".
- No OSM outline: Kirkland tower (inside Kirkland Hall, way/544397715), Rand Wall, Peabody Esplanade
  (node only), Branscomb Quad (treated as its four houses + market). "Commons" = Commons Center
  (way/370608816). Stage 2's shot list carries these as named areas; see `config/hero_spots.json`.
