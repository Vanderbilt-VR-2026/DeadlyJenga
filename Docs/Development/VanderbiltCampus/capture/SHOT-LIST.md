# Shot list (Stage 2)

Generated 2026-11-20T09:35:51 by `Tools/VanderbiltCampus/pipeline/shotlist.py` from `derived/buildings/v1/footprints.json`. Season for best times: 2026-11-20 (re-run with `--date` for another season). The full list is `Tools/VanderbiltCampus/config/shot_list.json`; the phone page is [coverage/index.html](coverage/index.html); the walking days are [CAPTURE-DAYS.md](CAPTURE-DAYS.md).

## Counts

- Campus buildings: 375; facades: 2691 (blocked 478, public 1912, restricted 301).
- Entries by kind: courtyard 15, detail 28, facade 2691, path 370, plaza 10, stairs 41, swatch 78.
- By priority: background 621, hero 384, normal 2228.
- By method: 360_walk 395, detail_set 69, photo_set 2691, swatch 78.
- Estimated photos: 200,684 (minimum to count as captured: 81,366).

## Rules used

- Facades: consecutive wall segments whose direction differs by less than 20 degrees merge; a facade is at least 4 m (a short jog between two walls running the same way joins them; other short pieces are corners, covered by the corner turns). Names are the compass side the wall faces (true north), with -1, -2 (longest first) when a side has several. Interior rings over 20 m2 are courtyards (`courtyard-...`, then `courtyard-b-...` for a second one).
- Access: rays straight out from the wall every 2 m up to 25 m, stopped by buildings, fences and hedges. `blocked` when half the wall has a building under 3 m in front; `public` when at least 30% of the rays reach a path, sidewalk or lawn (terrain surface map) that is not private; else `restricted` (fenced/private/construction, only asphalt, or nothing walkable). Hospital and clinical buildings are always `restricted` (stay on public sidewalks) and background.
- Stand-off: 0.8 x wall height, 3-15 m, no farther than the median open space in front. Camera path: parallel to the wall at that distance, trimmed to walkable ground (asphalt only when nothing else is there).
- est_photos (photo sets) = 3 passes x 2 heights x ceil(length / step) + 6 corner shots x 2 heights, step = 0.25 x the 70 degree view width at the stand-off (75% overlap: 0.35 x distance), plus one far pass from 10-15 m when the wall is taller than 1.2 x the stand-off. min_photos = 40% of est (at least 3). 360 walks: phone fallback of one photo every 1.5 m, two passes. Paths: one photo every 2 m.
- best_time: the sun (Nashville 36.15N) is computed every 5 minutes on the season date; a wall is in shade when the sun is down or behind the wall plane. North walls: any time; east: afternoon; west: morning; south: sunlit all day in late autumn, so overcast or low sun. Overcast is always fine.
- Priority: `hero` = hero buildings, hero areas, and other facades that face a hero lawn; `background` = hospital/clinical, parking structures, utility buildings, and walls facing out of campus (backdrop ring); else `normal`.

## Hero spots

| Spot (STANDARDS section 3) | Used in the shot list | OSM | Status |
|---|---|---|---|
| Kirkland Hall | Kirkland Hall | way/544397715 | confirmed |
| Kirkland Hall tower | Kirkland Hall tower: four `detail/B_544397715/tower-*` entries at the tallest LiDAR roof part | inside way/544397715 | approximate (LiDAR roof part, no OSM outline) |
| Alumni Lawn | Alumni Lawn | way/259811259 | confirmed |
| Central Library | Jean & Alexander Heard Library | way/544413710 | confirmed under the OSM name |
| Library Lawn | Library Lawn | way/1050096525 | confirmed |
| Peabody Esplanade | `plaza/approx-peabody-esplanade` | node/356898107 | approximate outline (OSM has a node only) |
| Cohen Memorial Hall | Cohen Memorial Hall | way/486160405 | confirmed |
| Furman Hall | Furman Hall | way/261715434 | confirmed |
| Benson Hall | Benson Science Hall | way/544413708 | confirmed under the OSM name |
| Wyatt Center | Wyatt Center | way/234871501 | confirmed |
| Commons | Commons Center building + `plaza/approx-commons-center-lawn` (the wider Ingram Commons area is a node only) | way/370608816 | ambiguous name resolved to Commons Center; lawn approximate |
| Rand Hall | Rand Hall | way/1465205768 | confirmed |
| Rand Wall | `detail/rand-wall`: approximate: 1-6 m strip along Rand Hall's north-east-1 facade, the side with the most OSM footway in front; confirm the seating wall on site | none | approximate, confirm on site |
| Engineering and Science Building | Science and Engineering Building | way/461630342 | confirmed under the OSM name |
| Featheringill/Jacobs Hall | Featheringill / Jacobs Hall | way/178414029 | confirmed |
| Student Life Center | Student Life Center | way/544597207 | confirmed |
| Branscomb Quad | the four houses + Branscomb Quad Market (hero buildings) and `plaza/approx-branscomb-quad` | way/544597204, relation/14043732, way/544597202, way/1052134886, way/544597206 | group; quad outline approximate |
| Memorial Gymnasium exterior | Memorial Gymnasium | way/544597209 | confirmed |

Approximate shapes (drawn by the script from nearby OSM features; treat as guesses until checked on site):

- **Peabody Esplanade**: approximate: lawn from the terrain surface map within 150 m of OSM node node/356898107, hull minus buildings (13711 m2).
- **Commons Center Lawn**: approximate: lawn within 80 m of OSM node node/9823385284 (4907 m2).
- **Branscomb Quad**: approximate: convex hull of Vaughn, Lupton, Scales, Stapleton and Branscomb Quad Market minus their footprints (5744 m2).
- **Rand Wall**: approximate: 1-6 m strip along Rand Hall's north-east-1 facade, the side with the most OSM footway in front; confirm the seating wall on site (232 m2).

## Capture day 1: Kirkland Hall and Alumni Lawn

Kirkland is the first full-detail building (Stage 4), so its entries carry `first_capture: true`:

- `B_544397715/south-east-1` (photo_set, 40 photos, public): a short window: in shade 15:55 to sunset; overcast: any time
- `B_544397715/south-west-3` (photo_set, 40 photos, public): mornings: in shade sunrise to 09:35; overcast: any time
- `B_544397715/south-east-2` (photo_set, 40 photos, public): a short window: in shade 15:55 to sunset; overcast: any time
- `B_544397715/north-east-1` (photo_set, 68 photos, public): afternoons: in shade 10:15 to sunset; overcast: any time
- `B_544397715/north-west-3` (photo_set, 26 photos, public): most of the day: in shade sunrise to 15:50; overcast: any time
- `B_544397715/north-east-2` (photo_set, 40 photos, public): afternoons: in shade 09:20 to sunset; overcast: any time
- `B_544397715/north-west-2` (photo_set, 40 photos, public): most of the day: in shade sunrise to 15:50; overcast: any time
- `B_544397715/south-west-2` (photo_set, 40 photos, public): mornings: in shade sunrise to 09:35; overcast: any time
- `B_544397715/north-west-1` (photo_set, 47 photos, public): most of the day: in shade sunrise to 15:30; overcast: any time
- `B_544397715/south-west-1` (photo_set, 40 photos, public): a short window: in shade sunrise to 08:30; overcast: any time
- `plaza/way-259811259` (360_walk, 430 photos, public): any time; even light (overcast or full shade) is best for ground and swatches
- `path/P0039` (360_walk, 77 photos, public): any time; even light (overcast or full shade) is best for ground and swatches
- `path/P0042` (360_walk, 54 photos, public): any time; even light (overcast or full shade) is best for ground and swatches
- `path/P0043` (360_walk, 39 photos, public): any time; even light (overcast or full shade) is best for ground and swatches
- `path/P0046` (360_walk, 36 photos, public): any time; even light (overcast or full shade) is best for ground and swatches
- `path/P0047` (360_walk, 24 photos, public): any time; even light (overcast or full shade) is best for ground and swatches
- `path/P0076` (360_walk, 28 photos, public): any time; even light (overcast or full shade) is best for ground and swatches
- `detail/B_544397715/tower-north` (detail_set, 25 photos, public): any time (in shade all day this season); overcast: any time
- `detail/B_544397715/tower-east` (detail_set, 25 photos, public): afternoons: in shade 11:35 to sunset; overcast: any time
- `detail/B_544397715/tower-south` (detail_set, 25 photos, public): sunlit all day this season: best on an overcast day; on a sunny day keep the sun behind you or go when it is low (before 08:00 / after 15:05); overcast: any time
- `detail/B_544397715/tower-west` (detail_set, 25 photos, public): mornings: in shade sunrise to 11:30; overcast: any time
- `detail/B_544397715/entrance` (detail_set, 30 photos, public): overcast is best; on a sunny day choose the side in shade
- `detail/B_544397715/window-types` (detail_set, 60 photos, public): overcast is best; on a sunny day choose the side in shade
- `detail/B_544397715/cornice` (detail_set, 40 photos, public): overcast is best; on a sunny day choose the side in shade
- `detail/B_544397715/buttresses` (detail_set, 30 photos, public): overcast is best; on a sunny day choose the side in shade
- `swatch/C12/brick` (swatch, 8 photos, public): overcast, or a fully shaded patch at any time (no sun spots)
- `swatch/C12/stone_trim` (swatch, 8 photos, public): overcast, or a fully shaded patch at any time (no sun spots)
- `swatch/C12/concrete` (swatch, 8 photos, public): overcast, or a fully shaded patch at any time (no sun spots)
- `swatch/C12/paving_concrete` (swatch, 8 photos, public): overcast, or a fully shaded patch at any time (no sun spots)
- `swatch/C12/asphalt` (swatch, 8 photos, public): overcast, or a fully shaded patch at any time (no sun spots)
- `swatch/C12/grass` (swatch, 8 photos, public): overcast, or a fully shaded patch at any time (no sun spots)
- `swatch/C12/bark` (swatch, 8 photos, public): overcast, or a fully shaded patch at any time (no sun spots)

## Schema of `shot_list.json` (intake reads this)

Top level: `{version: int, generated: str (ISO time), season_date, sources, rules, clusters, hero_areas, entries: [...]}`.

Each entry (required keys; extra keys may be added later, never renamed):

| Key | Type | Meaning |
|---|---|---|
| id | str | unique; facades `<building id>/<facade>`, else `courtyard/..`, `plaza/..`, `path/P0001`, `stairs/way-..`, `detail/..`, `swatch/<cluster>/<family>` |
| kind | str | one of facade, courtyard, plaza, path, stairs, detail, swatch |
| building | str or null | building id (`B_...`) |
| facade | str or null | facade name (`north`, `east-2`, `courtyard-south`) |
| priority | str | hero, normal, background |
| method | str | photo_set, 360_walk, detail_set, swatch |
| est_photos | int | planned photos (0 for blocked facades) |
| min_photos | int | photos at which intake marks it captured |
| centerline | [[x, y], ...] | campus meters (facade polyline; area outline; path line; point) |
| normal | [x, y] or null | unit outward normal (facades, tower faces) |
| bearing_deg | number or null | compass bearing the facade faces, true north |
| standoff_m | number or null | suggested camera distance from the wall |
| camera_path | [[x, y], ...] | where to walk (may be empty when no walkable ground) |
| tile | str | `T_<col>_<row>` of the entry's anchor point |
| best_time | object | `summary` text, `shade` [[from, to], ...] local time, `morning_ok`, `afternoon_ok`, `date` |
| access | str | public, restricted, blocked |
| access_notes | str | why, and what to watch for |
| example_framing | str | where to stand and what to include |
| day | int or null | capture day (filled by capture_days.py; null = not scheduled) |

Common extra keys: `name`, `building_name`, `length_m`, `height_m`, `area_m2`, `trees_in_view`, `first_capture`, `ring` (outer/courtyard), `far_pass`, `cluster`, `family`, `detail_type`, `approx`, `day_order`.

Coverage status (written by intake to `config/coverage_status.json`, read by coverage_map.py): `{version: 1, updated: str, entries: {<entry id>: {status, photos, last, note}}}` (intake's `usable_count`/`photo_count`, `last_capture` and `reason` are read as photos, last and note) with status one of `not_started`, `captured`, `aligned`, `needs_reshoot` (a bare status string per id also works).

`python pipeline/shotlist.py validate` checks a file against this schema.

## Buildings

| Building | Tile | Priority | Facades (public / restricted / blocked) | Wall height m | Est photos |
|---|---|---|---|---|---|
| Benson Science Hall (`B_544413708`) | T_01_-01 | hero | 8 (8 / 0 / 0) | 17.8 | 332 |
| Branscomb Quad Market (`B_544597206`) | T_-02_-03 | hero | 9 (4 / 0 / 5) | 6.9 | 557 |
| Cohen Memorial Hall (`B_486160405`) | T_04_-07 | hero | 8 (8 / 0 / 0) | 12.4 | 410 |
| Commons Center (`B_370608816`) | T_06_-07 | hero | 21 (19 / 0 / 2) | 15.5 | 764 |
| Featheringill / Jacobs Hall (`B_178414029`) | T_00_-03 | hero | 12 (12 / 0 / 0) | 13.5 | 1,139 |
| Furman Hall (`B_261715434`) | T_02_00 | hero | 10 (10 / 0 / 0) | 6.9 | 469 |
| Jean & Alexander Heard Library (`B_544413710`) | T_03_-02 | hero | 12 (11 / 0 / 1) | 19.4 | 481 |
| Kirkland Hall (`B_544397715`) | T_01_00 | hero | 10 (10 / 0 / 0) | 18.9 | 421 |
| Memorial Gymnasium (`B_544597209`) | T_-04_-04 | hero | 29 (28 / 1 / 0) | 18.2 | 1,843 |
| Rand Hall (`B_1465205768`) | T_00_-02 | hero | 14 (10 / 0 / 4) | 8.7 | 784 |
| Scales House (`B_544597202`) | T_-01_-03 | hero | 4 (3 / 0 / 1) | 8.6 | 458 |
| Science and Engineering Building (`B_461630342`) | T_-02_-05 | hero | 15 (14 / 0 / 1) | 40.5 | 608 |
| Stapleton House (`B_1052134886`) | T_-01_-04 | hero | 6 (5 / 0 / 1) | 9.0 | 532 |
| Student Life Center (`B_544597207`) | T_-02_-04 | hero | 8 (7 / 0 / 1) | 10.5 | 644 |
| Vaughn House (`B_544597204`) | T_-02_-03 | hero | 4 (2 / 0 / 2) | 9.3 | 342 |
| Wyatt Center (`B_234871501`) | T_04_-09 | hero | 12 (12 / 0 / 0) | 15.4 | 612 |
| 1101 Condominium (`B_837307988`) | T_07_-04 | normal | 9 (8 / 0 / 1) | 24.2 | 306 |
| 18th Ave. Childcare (`B_792244259`) | T_07_-04 | normal | 4 (3 / 0 / 1) | 4.2 | 665 |
| 2304G Vanderbilt Place (`B_544662565`) | T_-01_-01 | normal | 4 (4 / 0 / 0) | 6.7 | 212 |
| 2304I Vanderbilt Place (`B_544662563`) | T_-02_00 | normal | 4 (4 / 0 / 0) | 6.6 | 212 |
| 2400E Vanderbilt Place (`B_544597229`) | T_-02_-01 | normal | 6 (6 / 0 / 0) | 4.0 | 574 |
| 2402 Vanderbilt Place (`B_1052134888`) | T_-02_-02 | normal | 4 (3 / 0 / 1) | 3.4 | 270 |
| 24th Annex (`B_778186583`) | T_-01_-04 | normal | 4 (4 / 0 / 0) | 2.5 | 312 |
| 2525 West End (`B_1053160441`) | T_-05_-02 | normal | 4 (2 / 0 / 2) | 55.7 | 212 |
| Alpha Chi Omega House (`B_544597243`) | T_-03_-03 | normal | 6 (6 / 0 / 0) | 5.8 | 412 |
| Alpha Delta Pi House (`B_544597241`) | T_-02_-03 | normal | 6 (6 / 0 / 0) | 6.1 | 411 |
| Alpha Epsilon Pi House (`B_544597235`) | T_-02_-02 | normal | 7 (7 / 0 / 0) | 4.0 | 454 |
| Alpha Tau Omega House (`B_544597218`) | T_-04_-02 | normal | 4 (4 / 0 / 0) | 3.7 | 604 |
| Alumni Hall (`B_544397714`) | T_00_00 | normal | 8 (8 / 0 / 0) | 9.0 | 497 |
| American Economic Association (`B_544658975`) | T_04_01 | normal | 4 (4 / 0 / 0) | 12.0 | 514 |
| Baker Building (`B_544413699`) | T_03_02 | normal | 4 (4 / 0 / 0) | 42.6 | 448 |
| Baptist Collegiate Ministries (`B_544597238`) | T_-02_-02 | normal | 11 (11 / 0 / 0) | 3.3 | 744 |
| Barbizon Apartments (`B_544658974`) | T_04_02 | normal | 4 (4 / 0 / 0) | 15.3 | 290 |
| Ben Schulman Center for Jewish Life (`B_544597208`) | T_-03_-04 | normal | 8 (8 / 0 / 0) | 7.9 | 580 |
| Beta Theta Pi House (`B_544597201`) | T_-01_-02 | normal | 4 (4 / 0 / 0) | 3.1 | 708 |
| Biological Sciences Building/Medical Research Building III (`B_544597154`) | T_03_-04 | normal | 6 (2 / 0 / 4) | 48.2 | 199 |
| Bishop Joseph Johnson Center (`B_544597161`) | T_00_-02 | normal | 12 (12 / 0 / 0) | 5.0 | 914 |
| Blair School of Music (`B_256716167`) | T_-03_-10 | normal | 20 (19 / 0 / 1) | 8.3 | 1,124 |
| Blakemore House (`B_780388689`) | T_-08_-05 | normal | 14 (14 / 0 / 0) | 10.0 | 1,007 |
| Brownlee O Currey Junior Tennis Center (`B_544413697`) | T_-04_-07 | normal | 8 (8 / 0 / 0) | 6.7 | 1,048 |
| Bryan Building (`B_261715431`) | T_00_-03 | normal | 8 (8 / 0 / 0) | 4.1 | 1,157 |
| Buttrick Hall (`B_180245096`) | T_01_-02 | normal | 8 (8 / 0 / 0) | 15.5 | 546 |
| Calhoun Hall (`B_261715433`) | T_02_-01 | normal | 10 (8 / 0 / 2) | 17.2 | 347 |
| Chaffin A (`B_544413654`) | T_-03_-07 | normal | 8 (8 / 0 / 0) | 7.1 | 431 |
| Chaffin B (`B_544413652`) | T_-02_-07 | normal | 4 (4 / 0 / 0) | 6.5 | 340 |
| Chaffin C (`B_544413651`) | T_-02_-08 | normal | 4 (4 / 0 / 0) | 9.3 | 335 |
| Chaffin D (`B_544413653`) | T_-02_-08 | normal | 8 (8 / 0 / 0) | 7.2 | 400 |
| Chaffin E (`B_544413650`) | T_-02_-08 | normal | 6 (6 / 0 / 0) | 7.1 | 356 |
| Chaffin F (`B_544413649`) | T_-02_-08 | normal | 4 (3 / 0 / 1) | 10.9 | 290 |
| Chi Omega House (`B_544597244`) | T_-03_-03 | normal | 4 (4 / 0 / 0) | 4.9 | 358 |
| Childcare (`B_837307990`) | T_06_-03 | normal | 6 (6 / 0 / 0) | 3.6 | 803 |
| Cole Hall (`B_207111375`) | T_-01_-02 | normal | 12 (12 / 0 / 0) | 9.8 | 599 |
| Community Event Space (`B_1052259461`) | T_-01_-02 | normal | 4 (4 / 0 / 0) | 5.2 | 393 |
| Community Partnership House (`B_544597260`) | T_-01_-04 | normal | 7 (7 / 0 / 0) | 7.3 | 356 |
| Crawford House (`B_544597175`) | T_05_-09 | normal | 4 (4 / 0 / 0) | 17.7 | 236 |
| Cumberland House (`B_544662560`) | T_-02_00 | normal | 4 (4 / 0 / 0) | 6.4 | 238 |
| Curb Center/John Seigenthaler Center (`B_544597169`) | T_06_-05 | normal | 15 (15 / 0 / 0) | 5.4 | 1,387 |
| Dean's Residence (`B_544597170`) | T_06_-07 | normal | 9 (8 / 0 / 1) | 7.6 | 524 |
| Delta Delta Delta House (`B_544597197`) | T_-02_-01 | normal | 8 (8 / 0 / 0) | 6.5 | 443 |
| Divinity School (`B_r7779785`) | T_02_-02 | normal | 12 (12 / 0 / 0) | 16.0 | 1,282 |
| E. Bronson Ingram College (`B_r10625821`) | T_00_01 | normal | 26 (22 / 0 / 4) | 15.2 | 1,763 |
| E. Bronson Ingram Studio Art Center (`B_r10033309`) | T_-02_-04 | normal | 11 (11 / 0 / 0) | 13.7 | 977 |
| East House (`B_370613542`) | T_05_-08 | normal | 6 (5 / 0 / 1) | 9.7 | 427 |
| English Language Center (`B_544597193`) | T_07_-05 | normal | 7 (7 / 0 / 0) | 8.2 | 312 |
| Eskind Biomedical Library (`B_510204324`) | T_01_-05 | normal | 8 (8 / 0 / 0) | 17.5 | 560 |
| Fifth Third Bank (`B_544658933`) | T_04_-11 | normal | 4 (4 / 0 / 0) | 3.5 | 620 |
| Frist Hall (`B_544413712`) | T_02_-03 | normal | 6 (5 / 0 / 1) | 8.4 | 282 |
| Garland Hall (`B_261715432`) | T_02_-02 | normal | 12 (12 / 0 / 0) | 14.4 | 564 |
| Gillette House (`B_422234455`) | T_05_-08 | normal | 4 (4 / 0 / 0) | 15.6 | 335 |
| Godchaux Nursing Annex (`B_544413711`) | T_03_-03 | normal | 6 (4 / 0 / 2) | 21.2 | 243 |
| Hank Ingram House (`B_544597172`) | T_06_-08 | normal | 8 (8 / 0 / 0) | 20.9 | 409 |
| Hill Center (`B_370608815`) | T_06_-07 | normal | 4 (3 / 0 / 1) | 9.7 | 341 |
| Historic Old Gym (`B_544597264`) | T_-01_01 | normal | 7 (7 / 0 / 0) | 7.1 | 325 |
| Hobbs Laboratory (`B_544597182`) | T_05_-07 | normal | 4 (4 / 0 / 0) | 15.5 | 312 |
| Home Economics Building (`B_544597166`) | T_05_-05 | normal | 14 (12 / 0 / 2) | 10.7 | 558 |
| Jesup Psychology Building (`B_544597181`) | T_05_-07 | normal | 9 (7 / 0 / 2) | 11.1 | 364 |
| K.C. Potter Center (`B_544662568`) | T_-01_-01 | normal | 4 (4 / 0 / 0) | 6.3 | 225 |
| Kappa Alpha House (`B_544597231`) | T_-02_-02 | normal | 4 (4 / 0 / 0) | 6.7 | 409 |
| Kappa Alpha Theta House (`B_544597199`) | T_-02_-01 | normal | 5 (5 / 0 / 0) | 6.6 | 402 |
| Kappa Delta House (`B_856050859`) | T_-02_-02 | normal | 6 (5 / 0 / 1) | 5.5 | 345 |
| Kappa Kappa Gamma House (`B_544597217`) | T_-03_-02 | normal | 5 (5 / 0 / 0) | 4.7 | 428 |
| Kappa Sigma House (`B_1052134889`) | T_-02_-02 | normal | 7 (7 / 0 / 0) | 6.9 | 424 |
| Kay Bob's Grill & Ale (`B_544658928`) | T_03_-10 | normal | 6 (4 / 0 / 2) | 7.7 | 293 |
| Keck Free Electron Laser Center (`B_544597158`) | T_00_-04 | normal | 9 (9 / 0 / 0) | 10.2 | 470 |
| Kennedy Center (`B_544597179`) | T_03_-05 | normal | 8 (7 / 1 / 0) | 15.7 | 900 |
| Kissam Center (`B_1050096510`) | T_01_02 | normal | 10 (5 / 0 / 5) | 9.0 | 324 |
| Lambda Chi Alpha House (`B_1052259460`) | T_-01_-02 | normal | 4 (3 / 0 / 1) | 6.9 | 246 |
| Langford Auditorium (`B_370611111`) | T_01_-06 | normal | 4 (4 / 0 / 0) | 53.8 | 517 |
| Lewis House (`B_544413646`) | T_-02_-08 | normal | 6 (6 / 0 / 0) | 31.2 | 345 |
| Light Hall (`B_544597164`) | T_01_-06 | normal | 8 (7 / 0 / 1) | 47.0 | 444 |
| Lupton House (`B_r14043732`) | T_-02_-03 | normal | 11 (7 / 0 / 4) | 21.2 | 702 |
| Mapco (`B_370611116`) | T_02_-10 | normal | 4 (4 / 0 / 0) | 3.9 | 595 |
| Margaret Cuninggim Women's Center (`B_544662570`) | T_-01_-01 | normal | 4 (4 / 0 / 0) | 6.4 | 272 |
| Marriott (`B_544397720`) | T_-05_-03 | normal | 18 (17 / 0 / 1) | 34.9 | 715 |
| Martha River Ingram Center for the Performing Arts (`B_256716252`) | T_-02_-10 | normal | 6 (5 / 0 / 1) | 24.7 | 306 |
| Mayborn Building (`B_370608830`) | T_05_-06 | normal | 8 (7 / 0 / 1) | 10.0 | 437 |
| Mayfield A (`B_544413658`) | T_-03_-08 | normal | 11 (9 / 0 / 2) | 5.8 | 476 |
| Mayfield B (`B_544413659`) | T_-03_-09 | normal | 10 (10 / 0 / 0) | 6.1 | 753 |
| Mayfield C (`B_544413655`) | T_-03_-09 | normal | 8 (8 / 0 / 0) | 5.8 | 583 |
| Mayfield D (`B_544413656`) | T_-02_-09 | normal | 10 (10 / 0 / 0) | 5.8 | 599 |
| Mayfield E (`B_544413657`) | T_-02_-09 | normal | 16 (16 / 0 / 0) | 6.3 | 1,242 |
| McGill Hall (`B_207111363`) | T_-01_00 | normal | 11 (11 / 0 / 0) | 9.6 | 594 |
| McGugin Center (`B_544597210`) | T_-05_-06 | normal | 41 (36 / 0 / 5) | 7.7 | 2,705 |
| McTyeire Hall (`B_261715430`) | T_00_-05 | normal | 12 (12 / 0 / 0) | 6.2 | 1,197 |
| Medical Center North (`B_r10973063`) | T_02_-05 | normal | 48 (36 / 0 / 12) | 9.8 | 2,625 |
| Medical Child Care (`B_830622037`) | T_00_-12 | normal | 4 (4 / 0 / 0) | 3.9 | 827 |
| Memorial House (`B_422239142`) | T_05_-08 | normal | 4 (4 / 0 / 0) | 15.0 | 272 |
| Molecular Biology (`B_544597263`) | T_02_-03 | normal | 6 (3 / 0 / 3) | 25.9 | 197 |
| Moore College (`B_1050096509`) | T_01_02 | normal | 12 (11 / 0 / 1) | 14.8 | 915 |
| Morgan House (`B_544413645`) | T_-03_-08 | normal | 4 (4 / 0 / 0) | 31.9 | 346 |
| Multicultural Community Space (`B_544597224`) | T_-03_-02 | normal | 8 (8 / 0 / 0) | 3.2 | 850 |
| Murray House (`B_544597174`) | T_06_-09 | normal | 4 (4 / 0 / 0) | 16.8 | 242 |
| NPHC House (`B_856050858`) | T_-02_-02 | normal | 6 (6 / 0 / 0) | 10.2 | 342 |
| Neely Auditorium (`B_261715435`) | T_00_-01 | normal | 4 (4 / 0 / 0) | 8.8 | 391 |
| Nicolas S. Zeppos College (`B_r14026186`) | T_-04_-01 | normal | 23 (19 / 0 / 4) | 16.2 | 960 |
| North House (`B_307017789`) | T_03_-08 | normal | 4 (4 / 0 / 0) | 17.8 | 323 |
| Nuclear Magnetic Resonance Facility (`B_180245281`) | T_01_-03 | normal | 11 (11 / 0 / 0) | 5.0 | 493 |
| Office of Undergraduate Admissions & Financial Aid (`B_544413718`) | T_-01_00 | normal | 16 (16 / 0 / 0) | 7.7 | 732 |
| Office of the University Chaplain and Religious Life (`B_544597159`) | T_00_-04 | normal | 8 (8 / 0 / 0) | 4.2 | 567 |
| Old Central (`B_544413709`) | T_01_-01 | normal | 8 (8 / 0 / 0) | 9.5 | 349 |
| Olin Hall (`B_461630341`) | T_-02_-05 | normal | 11 (10 / 0 / 1) | 30.0 | 369 |
| Oliver C. Carmichael College (`B_r19167610`) | T_-02_00 | normal | 4 (4 / 0 / 0) | 69.4 | 452 |
| Owen Graduate School of Management (`B_285022929`) | T_03_-01 | normal | 25 (14 / 0 / 11) | 13.5 | 586 |
| Parmer Field House (`B_544597255`) | T_-04_-04 | normal | 10 (2 / 2 / 6) | 8.0 | 202 |
| Payne Building (`B_370608831`) | T_05_-06 | normal | 4 (4 / 0 / 0) | 12.2 | 307 |
| Peabody Administation Building (`B_544597177`) | T_04_-07 | normal | 4 (4 / 0 / 0) | 12.2 | 307 |
| Peabody Library (`B_544597178`) | T_04_-06 | normal | 4 (4 / 0 / 0) | 11.1 | 314 |
| Pi Beta Phi House (`B_544597215`) | T_-03_-01 | normal | 9 (7 / 0 / 2) | 8.8 | 306 |
| Pi Kappa Alpha House (`B_544597220`) | T_-03_-02 | normal | 6 (6 / 0 / 0) | 6.5 | 385 |
| Police Building (`B_773568511`) | T_-07_-05 | normal | 5 (5 / 0 / 0) | 3.5 | 780 |
| RCA Label Group (`B_544662596`) | T_07_-08 | normal | 18 (18 / 0 / 0) | 15.2 | 792 |
| Rothschild College (`B_r14057334`) | T_-03_-01 | normal | 7 (7 / 0 / 0) | 9.6 | 762 |
| Sarratt Student Center (`B_1465205624`) | T_00_-02 | normal | 20 (10 / 0 / 10) | 12.7 | 627 |
| Sigma Alpha Epsilon House (`B_544597251`) | T_-04_-02 | normal | 8 (8 / 0 / 0) | 8.5 | 582 |
| Sigma Chi House (`B_544597246`) | T_-03_-03 | normal | 4 (4 / 0 / 0) | 4.3 | 594 |
| Sigma Nu House (`B_544597216`) | T_-03_-02 | normal | 4 (4 / 0 / 0) | 6.3 | 381 |
| Sportsman's Grille (`B_544658929`) | T_02_-10 | normal | 4 (4 / 0 / 0) | 8.7 | 326 |
| St. Augustine's Episcopal Chapel (`B_544597198`) | T_-02_-01 | normal | 6 (6 / 0 / 0) | 3.6 | 822 |
| Stallworth Child Care (`B_r8765588`) | T_06_-05 | normal | 8 (8 / 0 / 0) | 3.6 | 1,153 |
| Stambaugh House (`B_544597173`) | T_05_-09 | normal | 12 (12 / 0 / 0) | 16.9 | 444 |
| Stevenson Center Complex (`B_544597163`) | T_01_-04 | normal | 19 (19 / 0 / 0) | 28.8 | 1,081 |
| Stevenson Center for Mathematics (`B_544413713`) | T_02_-03 | normal | 4 (4 / 0 / 0) | 10.7 | 380 |
| SunTrust (`B_544658930`) | T_02_-11 | normal | 7 (7 / 0 / 0) | 6.8 | 579 |
| Sutherland House (`B_544597176`) | T_05_-09 | normal | 4 (4 / 0 / 0) | 16.8 | 275 |
| Tibbot Art Center (`B_544413714`) | T_04_-04 | normal | 8 (7 / 0 / 1) | 14.3 | 388 |
| Tolman Hall (`B_207111369`) | T_-01_-01 | normal | 8 (8 / 0 / 0) | 9.1 | 583 |
| University Club of Nashville (`B_r7782343`) | T_-01_-05 | normal | 10 (10 / 0 / 0) | 4.3 | 1,141 |
| University School of Nashville (`B_544413715`) | T_04_-04 | normal | 32 (27 / 0 / 5) | 16.2 | 1,646 |
| VU ROTC (`B_792242791`) | T_06_-04 | normal | 6 (6 / 0 / 0) | 4.7 | 708 |
| VU Real Estate (`B_792243682`) | T_07_-04 | normal | 4 (3 / 0 / 1) | 7.0 | 188 |
| Vanderbilt - Village Commercial (`B_234871502`) | T_03_-09 | normal | 7 (6 / 0 / 1) | 16.9 | 884 |
| Vanderbilt Law School (`B_r7779786`) | T_03_00 | normal | 12 (12 / 0 / 0) | 8.0 | 1,003 |
| Vanderbilt Recreation and Wellness Center (`B_544597213`) | T_-05_-09 | normal | 16 (16 / 0 / 0) | 10.2 | 1,861 |
| Vanderbilt Soccer/Lacross Complex (`B_544597212`) | T_-05_-07 | normal | 4 (4 / 0 / 0) | 2.5 | 1,206 |
| Vanderbilt University Chancellor Emeritus (`B_544662581`) | T_08_-06 | normal | 4 (4 / 0 / 0) | 11.2 | 387 |
| Vanderbilt University Psychological & Counseling Center (`B_544658967`) | T_03_02 | normal | 4 (4 / 0 / 0) | 5.7 | 540 |
| Vanderbilt University Special Collections and Archives (`B_463653434`) | T_05_-03 | normal | 4 (4 / 0 / 0) | 8.6 | 338 |
| Vaughn Home (`B_544597162`) | T_00_-03 | normal | 6 (6 / 0 / 0) | 8.0 | 361 |
| Village Townhomes (`B_544597184`) | T_05_-10 | normal | 8 (8 / 0 / 0) | 5.8 | 624 |
| Village Townhomes (`B_544597185`) | T_05_-10 | normal | 4 (4 / 0 / 0) | 6.2 | 322 |
| Village Townhomes (`B_544597186`) | T_05_-10 | normal | 4 (4 / 0 / 0) | 5.3 | 424 |
| Village Townhomes (`B_544597188`) | T_05_-10 | normal | 4 (4 / 0 / 0) | 5.7 | 546 |
| Village Townhomes (`B_544597189`) | T_05_-09 | normal | 4 (4 / 0 / 0) | 5.9 | 316 |
| Village Townhomes (`B_544597190`) | T_05_-09 | normal | 4 (4 / 0 / 0) | 4.9 | 321 |
| Village Townhomes (`B_544597191`) | T_05_-10 | normal | 5 (5 / 0 / 0) | 4.8 | 388 |
| Village Townhomes (`B_544597192`) | T_05_-10 | normal | 4 (4 / 0 / 0) | 5.9 | 216 |
| Village at Vanderbilt - North Tower (`B_234871500`) | T_04_-09 | normal | 4 (4 / 0 / 0) | 11.9 | 518 |
| Village at Vanderbilt - South Tower (`B_234871499`) | T_04_-10 | normal | 10 (9 / 0 / 1) | 12.0 | 781 |
| Warren College (`B_782304968`) | T_02_02 | normal | 16 (10 / 0 / 6) | 23.0 | 539 |
| Werthan Building (`B_544597155`) | T_03_-05 | normal | 6 (2 / 0 / 4) | 43.2 | 136 |
| Wesley Place Retail (`B_544597194`) | T_04_-02 | normal | 4 (4 / 0 / 0) | 21.8 | 542 |
| West House (`B_307017788`) | T_03_-08 | normal | 8 (8 / 0 / 0) | 13.4 | 486 |
| Wilson Hall (`B_544413702`) | T_02_01 | normal | 12 (12 / 0 / 0) | 15.0 | 571 |
| Zerfoss Health Center (`B_544597157`) | T_01_-04 | normal | 6 (4 / 0 / 2) | 9.5 | 271 |
| Zeta Beta Tau House (`B_544597222`) | T_-03_-02 | normal | 8 (8 / 0 / 0) | 4.4 | 628 |
| Zeta Tau Alpha House (`B_856050857`) | T_-03_-02 | normal | 7 (7 / 0 / 0) | 7.1 | 462 |
| unnamed building B_1052259459 (`B_1052259459`) | T_-01_-02 | normal | 4 (2 / 0 / 2) | 3.8 | 114 |
| unnamed building B_1053160442 (`B_1053160442`) | T_-05_-02 | normal | 4 (3 / 0 / 1) | 11.0 | 280 |
| unnamed building B_1073611437 (`B_1073611437`) | T_07_-06 | normal | 6 (6 / 0 / 0) | 3.1 | 852 |
| unnamed building B_1073870764 (`B_1073870764`) | T_06_-06 | normal | 4 (4 / 0 / 0) | 3.5 | 312 |
| unnamed building B_1073870765 (`B_1073870765`) | T_06_-06 | normal | 4 (3 / 0 / 1) | 6.6 | 199 |
| unnamed building B_1073870766 (`B_1073870766`) | T_04_-05 | normal | 4 (4 / 0 / 0) | 8.0 | 593 |
| unnamed building B_1132353498 (`B_1132353498`) | T_05_-11 | normal | 4 (4 / 0 / 0) | 6.4 | 180 |
| unnamed building B_1132353499 (`B_1132353499`) | T_04_-11 | normal | 4 (4 / 0 / 0) | 4.6 | 228 |
| unnamed building B_187083441 (`B_187083441`) | T_-05_-04 | normal | 27 (7 / 15 / 5) | 16.1 | 2,516 |
| unnamed building B_446534675 (`B_446534675`) | T_-07_-04 | normal | 4 (3 / 0 / 1) | 7.5 | 275 |
| unnamed building B_544597171 (`B_544597171`) | T_06_-07 | normal | 4 (4 / 0 / 0) | 3.9 | 249 |
| unnamed building B_544597183 (`B_544597183`) | T_05_-07 | normal | 4 (3 / 0 / 1) | 4.0 | 144 |
| unnamed building B_544658927 (`B_544658927`) | T_03_-10 | normal | 4 (3 / 0 / 1) | 9.0 | 207 |
| unnamed building B_544658931 (`B_544658931`) | T_03_-11 | normal | 12 (12 / 0 / 0) | 13.3 | 903 |
| unnamed building B_544658934 (`B_544658934`) | T_03_-11 | normal | 4 (4 / 0 / 0) | 6.4 | 276 |
| unnamed building B_544658935 (`B_544658935`) | T_03_-11 | normal | 6 (6 / 0 / 0) | 6.5 | 414 |
| unnamed building B_544658936 (`B_544658936`) | T_03_-10 | normal | 4 (4 / 0 / 0) | 6.6 | 350 |
| unnamed building B_544658937 (`B_544658937`) | T_04_-11 | normal | 4 (3 / 0 / 1) | 6.5 | 193 |
| unnamed building B_544658938 (`B_544658938`) | T_04_-11 | normal | 6 (5 / 0 / 1) | 6.6 | 381 |
| unnamed building B_544658939 (`B_544658939`) | T_04_-11 | normal | 4 (4 / 0 / 0) | 7.1 | 344 |
| unnamed building B_544658940 (`B_544658940`) | T_05_-11 | normal | 4 (4 / 0 / 0) | 10.9 | 296 |
| unnamed building B_544658968 (`B_544658968`) | T_04_02 | normal | 4 (4 / 0 / 0) | 10.0 | 225 |
| unnamed building B_544662576 (`B_544662576`) | T_-01_-03 | normal | 12 (11 / 0 / 1) | 11.5 | 845 |
| unnamed building B_544662589 (`B_544662589`) | T_07_-05 | normal | 4 (4 / 0 / 0) | 3.8 | 318 |
| unnamed building B_544662601 (`B_544662601`) | T_07_-09 | normal | 4 (3 / 0 / 1) | 11.1 | 130 |
| unnamed building B_649567378 (`B_649567378`) | T_-03_-05 | normal | 10 (6 / 3 / 1) | 2.5 | 1,773 |
| unnamed building B_649567381 (`B_649567381`) | T_-04_-05 | normal | 11 (5 / 5 / 1) | 2.5 | 1,526 |
| unnamed building B_674123568 (`B_674123568`) | T_04_-11 | normal | 4 (3 / 0 / 1) | 6.8 | 176 |
| unnamed building B_674123569 (`B_674123569`) | T_04_-11 | normal | 6 (5 / 0 / 1) | 5.4 | 357 |
| unnamed building B_674123570 (`B_674123570`) | T_04_-11 | normal | 6 (5 / 0 / 1) | 6.1 | 347 |
| unnamed building B_724135893 (`B_724135893`) | T_-04_-09 | normal | 4 (4 / 0 / 0) | 3.9 | 468 |
| unnamed building B_724135896 (`B_724135896`) | T_-08_-09 | normal | 4 (4 / 0 / 0) | 2.7 | 228 |
| unnamed building B_724135897 (`B_724135897`) | T_-05_-10 | normal | 4 (4 / 0 / 0) | 2.5 | 252 |
| unnamed building B_724135899 (`B_724135899`) | T_-05_-09 | normal | 4 (4 / 0 / 0) | 4.4 | 278 |
| unnamed building B_724135901 (`B_724135901`) | T_-04_-07 | normal | 1 (1 / 0 / 0) | 8.0 | 26 |
| unnamed building B_724135902 (`B_724135902`) | T_-09_-09 | normal | 4 (4 / 0 / 0) | 8.0 | 116 |
| unnamed building B_724135905 (`B_724135905`) | T_-06_-09 | normal | 4 (4 / 0 / 0) | 3.6 | 288 |
| unnamed building B_724187557 (`B_724187557`) | T_03_-09 | normal | 4 (4 / 0 / 0) | 2.8 | 192 |
| unnamed building B_771280089 (`B_771280089`) | T_-07_-04 | normal | 4 (4 / 0 / 0) | 5.5 | 512 |
| unnamed building B_780388686 (`B_780388686`) | T_-07_-04 | normal | 2 (2 / 0 / 0) | 8.0 | 64 |
| unnamed building B_780388690 (`B_780388690`) | T_-07_-04 | normal | 2 (2 / 0 / 0) | 8.0 | 64 |
| unnamed building B_830622035 (`B_830622035`) | T_-01_-12 | normal | 4 (4 / 0 / 0) | 8.5 | 326 |
| unnamed building B_830622038 (`B_830622038`) | T_-01_-11 | normal | 4 (4 / 0 / 0) | 4.3 | 748 |
| unnamed building B_837311161 (`B_837311161`) | T_06_-04 | normal | 4 (4 / 0 / 0) | 3.5 | 336 |
| unnamed building B_837311163 (`B_837311163`) | T_06_-04 | normal | 4 (4 / 0 / 0) | 5.9 | 316 |
| unnamed building B_838040112 (`B_838040112`) | T_-08_-06 | normal | 4 (4 / 0 / 0) | 8.2 | 322 |
| unnamed building B_840679510 (`B_840679510`) | T_-04_-04 | normal | 4 (3 / 0 / 1) | 7.8 | 237 |
| unnamed building B_new_100 (`B_new_100`) | T_-01_-10 | normal | 3 (3 / 0 / 0) | 10.0 | 110 |
| unnamed building B_new_101 (`B_new_101`) | T_-01_01 | normal | 6 (6 / 0 / 0) | 11.3 | 288 |
| unnamed building B_new_103 (`B_new_103`) | T_-01_-10 | normal | 6 (6 / 0 / 0) | 3.4 | 361 |
| unnamed building B_new_104 (`B_new_104`) | T_-01_-11 | normal | 6 (3 / 0 / 3) | 4.6 | 141 |
| unnamed building B_new_109 (`B_new_109`) | T_-01_-03 | normal | 4 (3 / 0 / 1) | 7.3 | 136 |
| unnamed building B_new_113 (`B_new_113`) | T_-01_01 | normal | 4 (0 / 0 / 4) | 3.0 | 0 |
| unnamed building B_new_114 (`B_new_114`) | T_-01_-01 | normal | 4 (2 / 0 / 2) | 7.8 | 85 |
| unnamed building B_new_116 (`B_new_116`) | T_00_-02 | normal | 5 (3 / 0 / 2) | 2.5 | 294 |
| unnamed building B_new_118 (`B_new_118`) | T_00_-11 | normal | 3 (2 / 0 / 1) | 4.3 | 114 |
| unnamed building B_new_119 (`B_new_119`) | T_00_-02 | normal | 2 (0 / 0 / 2) | 10.3 | 0 |
| unnamed building B_new_120 (`B_new_120`) | T_00_-01 | normal | 5 (0 / 0 / 5) | 12.1 | 0 |
| unnamed building B_new_122 (`B_new_122`) | T_00_-03 | normal | 2 (1 / 0 / 1) | 17.6 | 38 |
| unnamed building B_new_126 (`B_new_126`) | T_00_-02 | normal | 6 (1 / 0 / 5) | 2.6 | 96 |
| unnamed building B_new_127 (`B_new_127`) | T_00_-02 | normal | 4 (2 / 0 / 2) | 14.3 | 58 |
| unnamed building B_new_133 (`B_new_133`) | T_00_-02 | normal | 2 (1 / 0 / 1) | 4.5 | 76 |
| unnamed building B_new_135 (`B_new_135`) | T_00_-02 | normal | 4 (2 / 0 / 2) | 6.6 | 77 |
| unnamed building B_new_140 (`B_new_140`) | T_00_02 | normal | 6 (6 / 0 / 0) | 14.1 | 216 |
| unnamed building B_new_163 (`B_new_163`) | T_01_-11 | normal | 3 (2 / 0 / 1) | 5.7 | 103 |
| unnamed building B_new_166 (`B_new_166`) | T_01_-05 | normal | 4 (2 / 0 / 2) | 3.8 | 193 |
| unnamed building B_new_172 (`B_new_172`) | T_02_00 | normal | 4 (4 / 0 / 0) | 2.6 | 276 |
| unnamed building B_new_174 (`B_new_174`) | T_02_02 | normal | 20 (0 / 0 / 20) | 3.4 | 0 |
| unnamed building B_new_182 (`B_new_182`) | T_02_-04 | normal | 4 (2 / 0 / 2) | 2.5 | 80 |
| unnamed building B_new_183 (`B_new_183`) | T_02_-03 | normal | 4 (3 / 0 / 1) | 3.6 | 198 |
| unnamed building B_new_186 (`B_new_186`) | T_02_-10 | normal | 4 (3 / 0 / 1) | 3.0 | 138 |
| unnamed building B_new_189 (`B_new_189`) | T_02_-03 | normal | 8 (6 / 0 / 2) | 4.7 | 512 |
| unnamed building B_new_196 (`B_new_196`) | T_03_-01 | normal | 5 (3 / 0 / 2) | 8.6 | 157 |
| unnamed building B_new_200 (`B_new_200`) | T_03_-09 | normal | 4 (2 / 0 / 2) | 2.7 | 198 |
| unnamed building B_new_201 (`B_new_201`) | T_03_-04 | normal | 7 (4 / 0 / 3) | 5.1 | 299 |
| unnamed building B_new_202 (`B_new_202`) | T_03_-01 | normal | 4 (2 / 0 / 2) | 7.2 | 97 |
| unnamed building B_new_204 (`B_new_204`) | T_03_-01 | normal | 5 (4 / 0 / 1) | 2.5 | 215 |
| unnamed building B_new_206 (`B_new_206`) | T_03_-01 | normal | 5 (2 / 0 / 3) | 7.1 | 130 |
| unnamed building B_new_208 (`B_new_208`) | T_03_-03 | normal | 4 (3 / 0 / 1) | 3.8 | 182 |
| unnamed building B_new_209 (`B_new_209`) | T_03_-02 | normal | 4 (3 / 0 / 1) | 4.3 | 260 |
| unnamed building B_new_213 (`B_new_213`) | T_03_-01 | normal | 4 (3 / 0 / 1) | 23.8 | 169 |
| unnamed building B_new_214 (`B_new_214`) | T_03_-04 | normal | 4 (4 / 0 / 0) | 3.8 | 314 |
| unnamed building B_new_217 (`B_new_217`) | T_04_-08 | normal | 6 (4 / 0 / 2) | 9.7 | 158 |
| unnamed building B_new_218 (`B_new_218`) | T_04_-07 | normal | 2 (1 / 0 / 1) | 10.0 | 53 |
| unnamed building B_new_219 (`B_new_219`) | T_04_-11 | normal | 4 (3 / 0 / 1) | 2.6 | 150 |
| unnamed building B_new_222 (`B_new_222`) | T_04_-10 | normal | 4 (3 / 0 / 1) | 2.5 | 174 |
| unnamed building B_new_223 (`B_new_223`) | T_04_-03 | normal | 4 (3 / 0 / 1) | 13.8 | 84 |
| unnamed building B_new_225 (`B_new_225`) | T_04_-11 | normal | 4 (3 / 0 / 1) | 2.5 | 252 |
| unnamed building B_new_226 (`B_new_226`) | T_04_-04 | normal | 4 (3 / 0 / 1) | 2.5 | 228 |
| unnamed building B_new_227 (`B_new_227`) | T_04_-09 | normal | 6 (5 / 0 / 1) | 9.4 | 189 |
| unnamed building B_new_228 (`B_new_228`) | T_04_-11 | normal | 4 (4 / 0 / 0) | 3.2 | 216 |
| unnamed building B_new_229 (`B_new_229`) | T_05_-08 | normal | 4 (3 / 0 / 1) | 3.5 | 234 |
| unnamed building B_new_231 (`B_new_231`) | T_05_-04 | normal | 8 (6 / 0 / 2) | 11.4 | 262 |
| unnamed building B_new_236 (`B_new_236`) | T_05_-10 | normal | 4 (4 / 0 / 0) | 3.3 | 264 |
| unnamed building B_new_239 (`B_new_239`) | T_05_-05 | normal | 4 (3 / 0 / 1) | 11.8 | 103 |
| unnamed building B_new_241 (`B_new_241`) | T_05_-02 | normal | 4 (3 / 0 / 1) | 3.8 | 148 |
| unnamed building B_new_242 (`B_new_242`) | T_05_-03 | normal | 8 (7 / 0 / 1) | 13.8 | 225 |
| unnamed building B_new_243 (`B_new_243`) | T_05_-06 | normal | 1 (1 / 0 / 0) | 23.2 | 26 |
| unnamed building B_new_244 (`B_new_244`) | T_05_-06 | normal | 4 (2 / 0 / 2) | 10.7 | 100 |
| unnamed building B_new_247 (`B_new_247`) | T_05_-05 | normal | 10 (7 / 0 / 3) | 2.7 | 347 |
| unnamed building B_new_250 (`B_new_250`) | T_06_-06 | normal | 4 (1 / 0 / 3) | 7.1 | 46 |
| unnamed building B_new_251 (`B_new_251`) | T_06_-05 | normal | 4 (3 / 0 / 1) | 3.0 | 294 |
| unnamed building B_new_254 (`B_new_254`) | T_06_-06 | normal | 4 (3 / 0 / 1) | 4.2 | 321 |
| unnamed building B_new_255 (`B_new_255`) | T_06_-05 | normal | 4 (2 / 0 / 2) | 2.5 | 138 |
| unnamed building B_new_265 (`B_new_265`) | T_06_-05 | normal | 4 (2 / 0 / 2) | 6.8 | 90 |
| unnamed building B_new_266 (`B_new_266`) | T_07_-08 | normal | 3 (3 / 0 / 0) | 4.7 | 148 |
| unnamed building B_new_271 (`B_new_271`) | T_07_-09 | normal | 5 (5 / 0 / 0) | 6.4 | 218 |
| unnamed building B_new_272 (`B_new_272`) | T_07_-05 | normal | 4 (4 / 0 / 0) | 2.5 | 246 |
| unnamed building B_new_33 (`B_new_33`) | T_-09_-08 | normal | 4 (4 / 0 / 0) | 3.0 | 228 |
| unnamed building B_new_45 (`B_new_45`) | T_-07_-04 | normal | 6 (3 / 0 / 3) | 2.5 | 172 |
| unnamed building B_new_50 (`B_new_50`) | T_-06_-10 | normal | 4 (4 / 0 / 0) | 3.4 | 264 |
| unnamed building B_new_55 (`B_new_55`) | T_-05_-05 | normal | 3 (1 / 2 / 0) | 4.9 | 324 |
| unnamed building B_new_57 (`B_new_57`) | T_-05_-02 | normal | 6 (5 / 0 / 1) | 10.8 | 204 |
| unnamed building B_new_60 (`B_new_60`) | T_-05_-02 | normal | 2 (2 / 0 / 0) | 4.6 | 152 |
| unnamed building B_new_61 (`B_new_61`) | T_-05_-02 | normal | 3 (2 / 0 / 1) | 9.4 | 92 |
| unnamed building B_new_62 (`B_new_62`) | T_-05_-04 | normal | 14 (1 / 7 / 6) | 2.6 | 705 |
| unnamed building B_new_63 (`B_new_63`) | T_-05_-03 | normal | 7 (2 / 1 / 4) | 5.3 | 681 |
| unnamed building B_new_70 (`B_new_70`) | T_-04_-10 | normal | 4 (4 / 0 / 0) | 3.0 | 252 |
| unnamed building B_new_71 (`B_new_71`) | T_-04_-06 | normal | 4 (2 / 0 / 2) | 3.2 | 96 |
| unnamed building B_new_74 (`B_new_74`) | T_-04_-01 | normal | 8 (4 / 0 / 4) | 4.3 | 214 |
| unnamed building B_new_75 (`B_new_75`) | T_-04_-01 | normal | 4 (2 / 0 / 2) | 4.4 | 209 |
| unnamed building B_new_76 (`B_new_76`) | T_-04_-10 | normal | 4 (4 / 0 / 0) | 3.9 | 228 |
| unnamed building B_new_77 (`B_new_77`) | T_-03_-08 | normal | 4 (1 / 0 / 3) | 17.7 | 75 |
| unnamed building B_new_78 (`B_new_78`) | T_-03_-08 | normal | 12 (8 / 0 / 4) | 11.0 | 621 |
| unnamed building B_new_79 (`B_new_79`) | T_-03_-02 | normal | 4 (3 / 0 / 1) | 3.6 | 192 |
| unnamed building B_new_80 (`B_new_80`) | T_-03_-01 | normal | 8 (4 / 0 / 4) | 4.0 | 183 |
| unnamed building B_new_81 (`B_new_81`) | T_-03_-04 | normal | 7 (3 / 0 / 4) | 16.6 | 136 |
| unnamed building B_new_82 (`B_new_82`) | T_-03_-03 | normal | 4 (3 / 0 / 1) | 8.7 | 122 |
| unnamed building B_new_84 (`B_new_84`) | T_-03_-07 | normal | 4 (3 / 0 / 1) | 3.5 | 144 |
| unnamed building B_new_85 (`B_new_85`) | T_-03_00 | normal | 3 (2 / 0 / 1) | 7.0 | 116 |
| unnamed building B_new_87 (`B_new_87`) | T_-03_-08 | normal | 3 (2 / 0 / 1) | 11.1 | 71 |
| unnamed building B_new_88 (`B_new_88`) | T_-03_-08 | normal | 4 (2 / 0 / 2) | 14.1 | 85 |
| unnamed building B_new_91 (`B_new_91`) | T_-02_-09 | normal | 4 (3 / 0 / 1) | 5.9 | 114 |
| unnamed building B_new_92 (`B_new_92`) | T_-02_-01 | normal | 5 (5 / 0 / 0) | 4.5 | 316 |
| unnamed building B_new_93 (`B_new_93`) | T_-02_-04 | normal | 5 (3 / 0 / 2) | 4.1 | 202 |
| unnamed building B_new_94 (`B_new_94`) | T_-02_-02 | normal | 4 (3 / 0 / 1) | 3.3 | 144 |
| unnamed building B_new_96 (`B_new_96`) | T_-02_-02 | normal | 4 (3 / 0 / 1) | 2.8 | 148 |
| unnamed building B_new_97 (`B_new_97`) | T_-02_-03 | normal | 14 (6 / 0 / 8) | 3.8 | 529 |
| unnamed building B_new_99 (`B_new_99`) | T_-01_-04 | normal | 2 (2 / 0 / 0) | 3.2 | 228 |
| unnamed building B_r11353006 (`B_r11353006`) | T_-01_-11 | normal | 12 (12 / 0 / 0) | 7.8 | 715 |
| Central Parking Garage (`B_370608818`) | T_02_-09 | background (clinical) | 11 (0 / 11 / 0) | 11.9 | 945 |
| Children's Hospital Outpatient Clinics (`B_370044425`) | T_00_-10 | background (clinical) | 4 (0 / 2 / 2) | 54.6 | 168 |
| Children's Hospital at Vanderbilt (`B_370044426`) | T_01_-09 | background (clinical) | 11 (0 / 8 / 3) | 59.6 | 598 |
| Dayani Center (`B_370608825`) | T_02_-10 | background (clinical) | 4 (0 / 4 / 0) | 6.9 | 704 |
| East Garage (`B_370611106`) | T_02_-07 | background (clinical) | 30 (0 / 5 / 25) | 4.5 | 1,742 |
| Kensington Garage (`B_544397719`) | T_-04_-03 | background (parking) | 6 (6 / 0 / 0) | 16.7 | 529 |
| Medical Arts (`B_370611108`) | T_03_-06 | background (clinical) | 4 (0 / 2 / 2) | 23.0 | 136 |
| Medical Center East North Tower (`B_370608822`) | T_02_-06 | background (clinical) | 7 (0 / 1 / 6) | 35.0 | 47 |
| Medical Center East South Tower (`B_370608823`) | T_02_-07 | background (clinical) | 17 (0 / 0 / 17) | 48.3 | 0 |
| Peabody Maintenance (`B_544597167`) | T_06_-06 | background (utility) | 10 (8 / 0 / 2) | 4.5 | 1,159 |
| Power House (`B_544662573`) | T_00_-02 | background (utility) | 16 (13 / 0 / 3) | 15.2 | 696 |
| Preston Cancer Research Building (`B_370611114`) | T_01_-08 | background (clinical) | 16 (0 / 9 / 7) | 34.5 | 353 |
| Robinson Research Building (`B_370611113`) | T_01_-07 | background (clinical) | 10 (0 / 7 / 3) | 31.0 | 389 |
| South Garage (`B_370044424`) | T_-01_-09 | background (clinical) | 10 (0 / 8 / 2) | 19.8 | 746 |
| Terrace Place Garage (`B_402074282`) | T_03_03 | background (parking) | 4 (3 / 0 / 1) | 19.9 | 578 |
| The Vanderbilt Clinic (`B_r14747568`) | T_01_-08 | background (clinical) | 30 (0 / 22 / 8) | 20.7 | 1,048 |
| Twenty Fifth Avenue Staff Parking Garage (`B_511889986`) | T_-02_-07 | background (parking) | 9 (9 / 0 / 0) | 30.4 | 759 |
| VA Medical Center (`B_544597165`) | T_00_-07 | background (clinical) | 46 (0 / 35 / 11) | 16.1 | 2,679 |
| VA Medical Center Garage (`B_499161824`) | T_-01_-08 | background (clinical) | 4 (0 / 4 / 0) | 17.1 | 569 |
| Vanderbilt Hospital (`B_r14747569`) | T_01_-07 | background (clinical) | 18 (0 / 16 / 2) | 49.5 | 994 |
| Vanderbilt Psychiatric Hospital (`B_544658951`) | T_-01_-10 | background (clinical) | 25 (0 / 22 / 3) | 12.7 | 1,116 |
| Vanderbilt Stallworth Rehabilitation Hospital (`B_544658950`) | T_01_-11 | background (clinical) | 19 (0 / 19 / 0) | 13.1 | 1,412 |
| Veterans Administration (`B_r10268615`) | T_00_-06 | background (clinical) | 4 (0 / 4 / 0) | 3.7 | 844 |
| West Garage (`B_544413647`) | T_-03_-09 | background (parking) | 4 (4 / 0 / 0) | 9.8 | 816 |
| unnamed building B_370608817 (`B_370608817`) | T_02_-08 | background (clinical) | 4 (0 / 2 / 2) | 30.8 | 152 |
| unnamed building B_544597247 (`B_544597247`) | T_-05_-02 | background (parking) | 4 (3 / 0 / 1) | 12.1 | 475 |
| unnamed building B_704646226 (`B_704646226`) | T_07_-06 | background (parking) | 4 (4 / 0 / 0) | 3.1 | 924 |
| unnamed building B_723536872 (`B_723536872`) | T_04_01 | background (parking) | 4 (4 / 0 / 0) | 2.5 | 1,020 |
| unnamed building B_724187556 (`B_724187556`) | T_-01_-08 | background (clinical) | 4 (0 / 3 / 1) | 4.2 | 259 |
| unnamed building B_730806661 (`B_730806661`) | T_03_-10 | background (parking) | 4 (3 / 0 / 1) | 2.5 | 360 |
| unnamed building B_731192006 (`B_731192006`) | T_-09_-08 | background (utility) | 1 (1 / 0 / 0) | 8.0 | 26 |
| unnamed building B_731192011 (`B_731192011`) | T_-09_-08 | background (utility) | 1 (1 / 0 / 0) | 8.0 | 26 |
| unnamed building B_771275339 (`B_771275339`) | T_04_01 | background (utility) | 4 (2 / 2 / 0) | 5.1 | 278 |
| unnamed building B_823263996 (`B_823263996`) | T_07_-09 | background (parking) | 4 (4 / 0 / 0) | 2.9 | 1,116 |
| unnamed building B_new_105 (`B_new_105`) | T_-01_-06 | background (clinical) | 4 (0 / 4 / 0) | 6.4 | 192 |
| unnamed building B_new_108 (`B_new_108`) | T_-01_-09 | background (clinical) | 4 (0 / 4 / 0) | 2.8 | 260 |
| unnamed building B_new_111 (`B_new_111`) | T_-01_-06 | background (clinical) | 5 (0 / 2 / 3) | 3.1 | 96 |
| unnamed building B_new_123 (`B_new_123`) | T_00_-09 | background (clinical) | 4 (0 / 4 / 0) | 3.2 | 252 |
| unnamed building B_new_125 (`B_new_125`) | T_00_-06 | background (clinical) | 14 (0 / 12 / 2) | 5.7 | 581 |
| unnamed building B_new_129 (`B_new_129`) | T_00_-07 | background (clinical) | 4 (0 / 3 / 1) | 3.6 | 168 |
| unnamed building B_new_131 (`B_new_131`) | T_00_-09 | background (clinical) | 14 (0 / 7 / 7) | 8.6 | 497 |
| unnamed building B_new_138 (`B_new_138`) | T_00_-07 | background (clinical) | 3 (0 / 2 / 1) | 8.8 | 238 |
| unnamed building B_new_144 (`B_new_144`) | T_01_-07 | background (clinical) | 3 (0 / 1 / 2) | 4.0 | 82 |
| unnamed building B_new_146 (`B_new_146`) | T_01_-08 | background (clinical) | 4 (0 / 2 / 2) | 2.7 | 84 |
| unnamed building B_new_149 (`B_new_149`) | T_01_-07 | background (clinical) | 4 (0 / 1 / 3) | 8.4 | 26 |
| unnamed building B_new_151 (`B_new_151`) | T_01_-08 | background (clinical) | 13 (0 / 2 / 11) | 3.8 | 204 |
| unnamed building B_new_161 (`B_new_161`) | T_01_-10 | background (clinical) | 6 (0 / 5 / 1) | 2.8 | 234 |
| unnamed building B_new_165 (`B_new_165`) | T_01_-09 | background (clinical) | 3 (0 / 3 / 0) | 11.8 | 90 |
| unnamed building B_new_167 (`B_new_167`) | T_02_-06 | background (clinical) | 4 (0 / 0 / 4) | 16.7 | 0 |
| unnamed building B_new_169 (`B_new_169`) | T_02_-07 | background (clinical) | 2 (0 / 2 / 0) | 5.0 | 320 |
| unnamed building B_new_171 (`B_new_171`) | T_02_-07 | background (clinical) | 5 (0 / 4 / 1) | 21.6 | 296 |
| unnamed building B_new_175 (`B_new_175`) | T_02_-06 | background (clinical) | 4 (0 / 2 / 2) | 8.2 | 162 |
| unnamed building B_new_176 (`B_new_176`) | T_02_-06 | background (clinical) | 4 (0 / 3 / 1) | 34.8 | 220 |
| unnamed building B_new_177 (`B_new_177`) | T_02_-06 | background (clinical) | 4 (0 / 3 / 1) | 18.2 | 172 |
| unnamed building B_new_179 (`B_new_179`) | T_02_-06 | background (clinical) | 4 (0 / 2 / 2) | 22.4 | 98 |
| unnamed building B_new_181 (`B_new_181`) | T_02_-07 | background (clinical) | 2 (0 / 1 / 1) | 9.5 | 86 |
| unnamed building B_new_185 (`B_new_185`) | T_02_-06 | background (clinical) | 7 (0 / 3 / 4) | 14.1 | 464 |
| unnamed building B_new_190 (`B_new_190`) | T_02_-08 | background (clinical) | 4 (0 / 3 / 1) | 3.0 | 744 |
| unnamed building B_new_195 (`B_new_195`) | T_03_-07 | background (clinical) | 4 (0 / 3 / 1) | 3.0 | 174 |
