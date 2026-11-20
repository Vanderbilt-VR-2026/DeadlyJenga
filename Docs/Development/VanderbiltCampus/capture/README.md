# Capture guide

You are taking photos so that, later, real textures can be put on the
campus models. The models already exist from LiDAR (laser scans from a
plane). Your photos add the detail and color.

A phone is enough. A 360 camera is optional.

Prices and app details were checked on 2026-11-19. They change, so check
before you buy.

## Pages

| Page | Read it when |
|------|--------------|
| [iphone.md](iphone.md) / [android.md](android.md) | Once, to set up your phone |
| [facade-walk.md](facade-walk.md) | Before your first building |
| [360-walk.md](360-walk.md) | Only if you have a 360 camera |
| [swatches-and-details.md](swatches-and-details.md) | For materials and small objects |
| [weather-light-privacy.md](weather-light-privacy.md) | When picking a day, and before you go |
| [checklist.md](checklist.md) | Print it. Use it every time |

## What to bring

- **Your phone**, set up with your device page. At least 20 GB free.
- **A charged battery pack and cable.** Cameras drain batteries fast.
- **A gray card.** A flat card that is exactly neutral gray, so the
  computer can fix colors later. A cheap one is fine, for example the
  Neewer 18% Gray/White Card Set (8x10 in, about $15). Optional upgrade:
  Calibrite ColorChecker Classic Mini (a small card with 24 color
  patches, about $55 to $75). Check current price.
- **A microfiber cloth** for the lens. A smudge ruins a whole set.
- **A small ruler or tape measure** for material swatches.
- Optional: a 360 camera and selfie stick ([360-walk.md](360-walk.md)).

## The routine for every session (8 steps)

1. **Check the light.** Overcast is best. See
   [weather-light-privacy.md](weather-light-privacy.md).
2. **Clean the lens.** Wipe it with the cloth.
3. **Check settings.** RAW or highest quality, 1x lens, HDR and night
   mode off, location on. See your device page.
4. **Shoot the gray card first.** Hold it flat in the same light as the
   wall and take one photo that fills about a third of the frame.
5. **Lock exposure and focus**, aimed at the wall (not the sky).
6. **Walk the facade** as in [facade-walk.md](facade-walk.md): straight-on
   pass, two angled passes, two heights, slow corners.
7. **Check a few shots.** Zoom in on the phone. Are they sharp? Not too
   dark or too bright? Reshoot now if not.
8. **Note what you did.** Building, sides, start time. A quick voice memo
   or note is enough. New building or big light change: start again at
   step 4.

## Getting files to the PC

- **Copy the originals straight off the device by cable.** Do not send
  photos through a messaging app, email, AirDrop to another phone, or a
  cloud sync that "optimizes" them. These shrink or change the files.
- Put each day's files in its own folder:

  ```
  raw/capture/<YYYY-MM-DD>/<device>/
  ```

  `raw` is inside the archive folder (`data_root` in
  `Tools/VanderbiltCampus/config/local.json`). Example:
  `raw/capture/2026-12-03/iphone/`. Use one short name per device, for
  example `iphone`, `pixel`, `x5`.
- Never edit, rename or delete anything in `raw/`. Files there are
  written once.
- Keep the photos on the phone until intake has run and your backup
  drive has a copy.

## Then run intake

From the repository folder:

```
python Tools/VanderbiltCampus/pipeline/intake.py <folder>
```

This is the planned command; the script is still being written. It will
hash and record every file, sort photos by building, blur faces and
license plates in separate copies, flag blurry or dark shots, update the
coverage map, and print what to reshoot.
