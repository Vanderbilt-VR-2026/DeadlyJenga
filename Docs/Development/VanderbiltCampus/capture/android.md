# Android setup

Every brand names menus a bit differently. Pixel and Samsung are shown.
For other phones, use **Open Camera** (section 5).

## 1. File format

RAW means the photo keeps all the sensor data, so color and brightness
can be fixed later. Android RAW files are `.dng`.

**Pixel:**
- Camera > Settings > More settings > Advanced > turn on **RAW/JPEG
  control** (not needed on Pixel 8 Pro).
- In photo mode, open the settings at the bottom left and pick
  **RAW + JPEG**.

**Samsung Galaxy:**
- Use **Pro** mode (swipe to More > Pro). In camera settings, turn on
  saving RAW copies (the option name varies by model).
- Or install **Expert RAW**: free, from the Galaxy Store, for Galaxy S20
  and later. It saves a RAW (DNG) and a JPEG every shot.

**No RAW available:** highest resolution, highest JPEG quality.

## 2. Turn these off

- **HDR / HDR+ / Auto HDR**: off where the app allows it.
- **Night Sight / Night mode**: off.
- **Motion photos / Top Shot**: off (they are like iPhone Live Photos).
- **Filters, beauty, "scene optimizer", AI enhance**: off.
- **Macro / auto lens switch**: off, so the phone stays on the main lens.
- **Zoom**: stay on **1x**. Never pinch to zoom (it only crops).
- **Flash**: off.

## 3. Keep location on

- Camera settings > **Save location** (or Location tags) on.
- Phone Settings > Location on, with **Google Location Accuracy** on.
- Keep **Airplane mode off**.

## 4. Lock exposure, focus and white balance

- **Pixel:** touch and hold the wall until the circle turns yellow (lock).
  Tap anywhere to unlock. Pixel cannot lock white balance by touch; RAW
  plus the gray card fixes it later.
- **Samsung Pro mode:** set **WB** (white balance) to a fixed value such as
  Cloudy or a number like 5500K, set **ISO** to a fixed low value, and
  lock focus (touch and hold, or use manual focus).
- Do not tap the screen again during a pass.

## 5. Recommended free app: Open Camera

**Open Camera** (Google Play, also F-Droid) is **free and open source**,
with no ads. On most recent phones it can:
- save **RAW (DNG)** files (Settings > Photo settings > RAW);
- lock exposure (padlock icon, red = locked);
- set fixed white balance, ISO and manual focus;
- tag photos with GPS (Settings > Location settings).

RAW and manual controls need the phone's Camera2 support. If the RAW
option is missing, your phone does not offer it.

## 6. Storage math

| Format | Rough size per photo | Photos per GB |
|--------|----------------------|---------------|
| RAW DNG, 12 MP | about 15 to 25 MB | about 40 to 65 |
| RAW DNG, 50 MP (Samsung full-res) | can be 60 to 100+ MB | about 10 to 15 |
| JPEG, 12 MP | about 3 to 5 MB | about 200 to 300 |

These are estimates; take ten test shots and check the real sizes. A
hero building can take 300 to 600 photos.

## 7. Getting photos off the phone

- Plug into the PC by cable. On the phone, pick **File transfer**.
- Copy from `DCIM/Camera` (and Open Camera's `DCIM/OpenCamera`).
- Turn off Google Photos "Storage saver" for backup; it keeps a smaller
  copy. Never copy from Google Photos; copy from the phone itself.
