# Vanderbilt campus pipeline tools

This folder holds the scripts that turn public campus data (LiDAR, orthophotos,
OpenStreetMap, photos) into the Vanderbilt campus map for Deadly Jenga. Each
step is a small script in `pipeline/` that you can run on its own. Everything
here runs on a normal CPU. No NVIDIA GPU is needed. Big data and tool installs
live outside git: the data in `data_root` and the tools in
`C:\Users\<you>\VanderbiltCampusTools\`. The specs are in
`Docs/Development/VanderbiltCampus/`.

## Install

Run these from the repository root in PowerShell. Change `C:\Users\evanp` to
your own user folder. Then copy `config\local.example.json` to
`config\local.json` and set the paths for your PC.

### 1. Python environment (Python 3.12)

All packages have Windows wheels for Python 3.12, so 3.12 is used.

```powershell
uv python install 3.12
uv venv --python 3.12 Tools\VanderbiltCampus\.venv
uv pip install --python Tools\VanderbiltCampus\.venv\Scripts\python.exe -r Tools\VanderbiltCampus\requirements.txt
```

If `uv venv` fails with "Missing expected target directory for Python minor
version link", give it the full path to the Python that uv installed:

```powershell
uv venv --python "$env:APPDATA\uv\python\cpython-3.12.16-windows-x86_64-none\python.exe" Tools\VanderbiltCampus\.venv
```

### 2. PDAL (through micromamba)

PDAL has no pip wheel for Windows, so it comes from conda-forge. micromamba is
one exe and needs no admin rights.

```powershell
$T = "C:\Users\evanp\VanderbiltCampusTools"
mkdir $T\micromamba
curl.exe -L -o $T\micromamba\micromamba.exe https://github.com/mamba-org/micromamba-releases/releases/download/2.10.0-0/micromamba-win-64.exe
$env:MAMBA_ROOT_PREFIX = "$T\micromamba\root"
& $T\micromamba\micromamba.exe create -y -p $T\pdal-env -c conda-forge --override-channels pdal python-pdal
& $T\micromamba\micromamba.exe run -p $T\pdal-env pdal --version
```

### 3. COLMAP (CPU build)

```powershell
mkdir $T\downloads\colmap, $T\colmap
curl.exe -L -o $T\downloads\colmap\colmap-x64-windows-nocuda.zip https://github.com/colmap/colmap/releases/download/4.2.1/colmap-x64-windows-nocuda.zip
Expand-Archive $T\downloads\colmap\colmap-x64-windows-nocuda.zip -DestinationPath $T\colmap
& $T\colmap\bin\colmap.exe version
```

COLMAP 4.2.1 includes the global mapper (`colmap global_mapper`), which
replaced the separate GLOMAP program. You do not need GLOMAP.

### 4. ExifTool

The download link on https://exiftool.org points to SourceForge. Check the
SHA-256 against https://exiftool.org/checksums.txt.

```powershell
mkdir $T\downloads\exiftool, $T\exiftool
curl.exe -L -o $T\downloads\exiftool\exiftool-13.64_64.zip https://sourceforge.net/projects/exiftool/files/exiftool-13.64_64.zip/download
Expand-Archive $T\downloads\exiftool\exiftool-13.64_64.zip -DestinationPath $T\exiftool
Rename-Item "$T\exiftool\exiftool-13.64_64\exiftool(-k).exe" exiftool.exe
& $T\exiftool\exiftool-13.64_64\exiftool.exe -ver
```

Keep `exiftool.exe` next to its `exiftool_files` folder. It needs that folder.

### 5. Blender and FFmpeg

Install Blender 5.3 from blender.org and FFmpeg (it must be on PATH). Check:

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.3\blender.exe" -b --version
ffmpeg -version
```

## Tools

| Tool | Version | How it is called (local.json key) |
|------|---------|-----------------------------------|
| Python (venv) | 3.12.16 | `Tools\VanderbiltCampus\.venv\Scripts\python.exe` (`python`) |
| numpy, scipy | 2.5.5, 1.18.1 | Python import |
| laspy + lazrs (LAZ) | 2.7.0, 0.8.2 | Python import |
| pyproj, shapely | 3.8.0, 2.2.0 | Python import |
| rasterio (GDAL 3.12.2 inside) | 1.5.2 | Python import |
| open3d | 0.20.0 | Python import |
| trimesh, pygltflib | 5.2.1, 1.16.5 | Python import |
| opencv-python-headless | 5.1.0.95 | Python import (`cv2`) |
| pycolmap (CPU, has global mapping) | 4.2.1 | Python import |
| pillow, scikit-image, scikit-learn | 13.0.0, 0.26.0, 1.9.1 | Python import |
| networkx, matplotlib | 3.7, 3.11.2 | Python import |
| requests, pytest | 2.34.2, 9.2.0 | Python import |
| PDAL (+ python-pdal 3.5.5 in the same env) | 2.10.2 | `micromamba.exe run -p <tools>\pdal-env pdal ...` (`pdal`) |
| micromamba | 2.10.0 | `<tools>\micromamba\micromamba.exe` |
| COLMAP (no CUDA) | 4.2.1 | `<tools>\colmap\bin\colmap.exe` (`colmap`) |
| ExifTool | 13.64 | `<tools>\exiftool\exiftool-13.64_64\exiftool.exe` (`exiftool`) |
| Blender | 5.3.0 | `blender.exe -b ...` (`blender`) |
| FFmpeg | 9.0.2 | `ffmpeg` on PATH |

`<tools>` is `C:\Users\evanp\VanderbiltCampusTools` on Evan's PC.
`pdal.exe` in `pdal-env\Library\bin` also runs directly, but the
`micromamba run` form sets up PROJ and GDAL data paths, so use that.

## Check the install and run the tests

```powershell
Tools\VanderbiltCampus\.venv\Scripts\python Tools\VanderbiltCampus\pipeline\check_tools.py
Tools\VanderbiltCampus\.venv\Scripts\python -m pytest Tools\VanderbiltCampus\tests
```

`check_tools.py` prints a table of every package and tool. It exits with an
error if anything is missing.

## Optional (not required)

- RealityScan: photogrammetry app. Needs Evan's Epic Games login. Not set up yet.
- Brush: Gaussian splat trainer. Only if we try splats.
- GPU tools for Stage 6 (CUDA COLMAP, neural reconstruction): need an NVIDIA
  GPU, for example on ACCRE. This PC does not have one.

## Stage 0: commands in run order

Run from the repository root in PowerShell. `$PY` is
`Tools\VanderbiltCampus\.venv\Scripts\python.exe`; `$P` is
`Tools\VanderbiltCampus\pipeline`. Every step writes into `data_root`, records
its files in the manifest, and can be run again (it skips or rewrites the same
files).

```powershell
$PY = "Tools\VanderbiltCampus\.venv\Scripts\python.exe"; $P = "Tools\VanderbiltCampus\pipeline"
& $PY $P\check_tools.py                                    # 1. tools installed?
& $PY $P\fetch_osm.py                                      # 2. OSM snapshot -> raw/osm/<date>/
& $PY $P\make_boundary.py                                  # 3. config/boundary.geojson (campus + 150 m ring)
& $PY $P\fetch_lidar.py --project TN_DavidsonCounty_D22    # 4. D22 tiles (slow: rockyweb rate limit)
& $PY $P\fetch_lidar.py --project TN_DavidsonCounty_D22 --source ept   #    working EPT copy (D007)
& $PY $P\fetch_lidar.py --project TN_NASHVILLE_2011 --source ept       #    2011 comparison set
& $PY $P\fetch_metro.py                                    # 5. Metro footprints, canopy, ortho; NAIP
& $PY $P\frame.py build                                    # 6. config/frame.json (origin, k, Z0)
& $PY Tools\VanderbiltCampus\tests\make_frame_points.py    #    tests/frame_points.json (10 known points)
& $PY $P\prepare_lidar.py --with-2011                      # 7. derived/lidar/v1/tiles/*.laz + index.json,
                                                           #    EPT-vs-original check, 2011 copy
& $PY $P\make_rasters.py                                   # 8. derived/rasters/v1 (DTM, DSM, nDSM, masks,
                                                           #    density) + derived/ortho/v1 mosaics
& $PY $P\render_stage0.py --git-copies                     # 9. reports/stage0/*.jpg (+ 2 copies in git)
& $PY $P\hero_spots.py                                     # 10. config/hero_spots.json
& $PY $P\manifest.py verify                                # 11. every archived file still matches
& $PY -m pytest Tools\VanderbiltCampus\tests -q            # 12. tests
```

Useful afterwards:

```powershell
& $PY $P\frame.py convert --epsg 4326 -86.80297 36.14804 170 --unity   # lon lat NAVD88-m -> campus/Unity
& $PY $P\frame.py convert --epsg 6576 --inverse 0 0 26.4              # campus -> EPSG:6576 ftUS
& $PY $P\frame.py tile 350 -120                                       # -> T_03_-02
& $PY $P\prepare_lidar.py --skip-tiles     # re-run only the check against newly downloaded original tiles
```

In Python, later stages read LiDAR with
`lidar_io.load_points(xmin, ymin, xmax, ymax, classes=[2, 6], buffer=5)`
(campus meters) and convert coordinates with `frame.to_campus(x, y, z, epsg)` /
`frame.from_campus(X, Y, Z, epsg)`.
