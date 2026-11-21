# Evan's to-do list

Ordered by importance. Builder chats never send, buy, sign up or accept terms.

1. **Photograph the campus (this unlocks everything past LoD2).** Nothing on site has been captured yet, so
   every building is still LoD2 from LiDAR, and Kirkland Hall's detail is an estimate from one public photo.
   Start with Day 1 in `capture/CAPTURE-DAYS.md` (Kirkland Hall first: about 2.5 hours, about 2,000 photos);
   the guide is `capture/README.md`, the phone map is `capture/coverage/index.html`. Copy each day's card to
   `raw/capture/<date>/` and run `Tools/VanderbiltCampus/pipeline/intake.py` (commands in `STAGE-2-results.md`).
   Optional: a 360 camera makes paths and interiors of quads much faster (the 360 path is only tested on a
   synthetic clip so far).

2. **Move the archive to a drive with >= 500 GB free, and keep a second copy.**
   Today it is at `C:\Users\evanp\VanderbiltCampusData` (C: has about 314 GB free).
   Steps: copy the whole folder to the new drive, edit `Tools/VanderbiltCampus/config/local.json`
   (`data_root`), then run `python Tools/VanderbiltCampus/pipeline/manifest.py verify`.
   Keep a backup copy (another drive or cloud). `raw/` must never leave your machine or your backup.

3. **Send the Vanderbilt GIS / Facilities data request** (biggest possible shortcut). Draft, ready to send
   from your Vanderbilt email to the Facilities GIS team (address on
   https://www.vanderbilt.edu/facilities/maps/ ; the drone page is https://www.vanderbilt.edu/facilities/maps/drone/):

   > Subject: Student project request: campus 3D / drone / CAD data
   >
   > Hello,
   >
   > I'm a Vanderbilt student building a non-commercial 3D model of main campus (including Peabody) for a
   > student VR game project. I'm currently using public USGS LiDAR and OpenStreetMap.
   >
   > Would Facilities GIS be able to share any of the following, and under what terms of use?
   > - drone orthophotos or photogrammetry of campus;
   > - 3D building models (any format), or building footprints with heights;
   > - CAD floor plans or exterior elevations of main campus buildings (exterior only is fine);
   > - any campus tree inventory.
   >
   > I'd credit Vanderbilt Facilities GIS in the project and follow any restrictions you set (for example,
   > no redistribution of the raw files). I'm happy to share the finished model back with your team.
   >
   > Thank you,
   > Evan Prestipino

   When anything arrives: put it untouched in `raw/gis/<date>/` with their terms in a `TERMS.txt` beside it,
   then run `manifest.py add raw/gis/<date> --source "Vanderbilt Facilities GIS" --license "<their terms>" --stage 0`.

4. **Ask about ACCRE access** (for Stage 6 GPU texture baking; optional). Draft for your faculty sponsor or
   ACCRE (https://www.vanderbilt.edu/accre/):

   > Subject: ACCRE GPU access for a student project
   >
   > Hello, I'm working on a student VR project that reconstructs Vanderbilt's campus from photos. A later
   > step needs an NVIDIA GPU (photo alignment, dense reconstruction and texture baking: tens of GPU-hours).
   > Is there a way for a student to get GPU time on ACCRE, for example through a course or a faculty
   > sponsor's allocation? What would I need to set up?
   > Thank you, Evan Prestipino

5. **Trademark question to Vanderbilt licensing** (draft only; send when you want to use the name in a
   release). https://www.vanderbilt.edu/communications/trademark-licensing/

   > Subject: Using the Vanderbilt name in a student game
   >
   > Hello, I'm a student making a free multiplayer VR game with friends. One map is a model of the
   > Vanderbilt campus buildings and lawns. We don't use any logos. Would we need permission to call the map
   > "Vanderbilt" in a public release, or to show building names on signs? If so, what is the process?
   > Thank you, Evan Prestipino

   Until you record an answer in DECISIONS.md, the map is called "Campus" and signs are blank.

6. **Optional: sign up for RealityScan** (free for students; needs your Epic account; you accept the terms,
   not the builder). It can be used in Stage 3 as a second alignment. https://www.realityscan.com

7. **Optional: more public reference photos.** Wikimedia Commons blocked bulk downloads (HTTP 429) after 1
   of 370 files. If you want them, either let `fetch_samples.py commons --file-pause 45` run overnight, or
   make a free Mapillary developer token yourself (you accept their terms) and put it in
   `Tools/VanderbiltCampus/config/local.json` as `mapillary_token`.
