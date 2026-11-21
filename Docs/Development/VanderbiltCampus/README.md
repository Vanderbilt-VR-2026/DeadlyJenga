# Vanderbilt campus map

A Deadly Jenga map of Vanderbilt's main campus, built **shape first**:
- the exact 3D shape of every building, path, lawn and tree;
- only details too small to matter are merged into nearby shapes, and each
  merge is recorded;
- simple Deadly Jenga colors and materials for now;
- an archive that keeps every photo, scan and measurement, so a strong GPU
  can later bake real textures onto the **same models** without rebuilding
  anything.

This folder is the complete handoff package. A builder chat needs nothing
else except the repository and this folder.

## Read in this order

| File | What it is |
|------|------------|
| README.md (this file) | Vision, route, glossary, rules, how the work is split |
| [STANDARDS.md](STANDARDS.md) | The contracts every stage must follow: coordinates, material IDs, detail levels, the merge rule, ledgers, archive, budgets, Unity integration |
| [RESEARCH.md](RESEARCH.md) | Tools, hard limits and sources |
| [STAGE-0-Setup.md](STAGE-0-Setup.md) | Data download, folders, standards files |
| [STAGE-1-Massing-Campus.md](STAGE-1-Massing-Campus.md) | Playable campus with exact footprints, heights and roofs (LoD2), from free LiDAR |
| [STAGE-2-Capture-Kit.md](STAGE-2-Capture-Kit.md) | Shot list, phone coverage map, capture guide, intake |
| [STAGE-3-Alignment-and-Measurement.md](STAGE-3-Alignment-and-Measurement.md) | Photos aligned to the LiDAR on CPU; facade measurement |
| [STAGE-4-Style-Kits-and-Kirkland.md](STAGE-4-Style-Kits-and-Kirkland.md) | Architecture kits; first full-detail building |
| [STAGE-5-Full-Campus.md](STAGE-5-Full-Campus.md) | Every building at full detail, gameplay, candy look, realism readiness test |
| [STAGE-6-Later-Realism.md](STAGE-6-Later-Realism.md) | Not built now: how real textures get added on a strong GPU |
| [BUILDER-PROMPT.md](BUILDER-PROMPT.md) | The prompts to paste into each builder chat |

## The route in four sentences

1. Free government LiDAR and OpenStreetMap give the exact ground, building
   outlines, heights and roofs, which is playable before any photo exists.
2. Evan walks campus once, capturing photos and 360 video the way a future
   texture bake needs them, and every file goes into the archive.
3. The photos are aligned to the LiDAR on an ordinary CPU. Then clean,
   modeled buildings (walls, windows, columns, cornices, roofs) are fitted
   to those measurements from per-style kits. The game never uses a lumpy
   scan mesh.
4. Every surface carries a material ID and real UVs. Today the IDs map to
   flat candy colors; later they map to textures baked from the archived
   photos.

## Glossary

- **LoD1 / LoD2 / LoD3 / LoD3+**: detail levels (see STANDARDS.md). LoD2 =
  exact outline, height and roof. LoD3 = plus openings and architectural
  features. LoD3+ = hero spots with fine trim.
- **Merge rule**: features smaller than the tier's threshold are merged into
  a neighbor shape and written to the detail ledger.
- **Detail ledger**: per-building list of everything merged or simplified,
  with position, size, type and the photos that show it.
- **Source map**: per-building list of which photos and scans cover which
  facade.
- **Material ID**: what a surface is made of (`brick_red`, `limestone`, ...).
  Fixed list in STANDARDS.md.
- **Palette atlas**: a small texture with one color swatch per material ID.
  The candy look.
- **Campus frame**: the one local coordinate system (meters) shared by LiDAR,
  photos, models and Unity.
- **Archive**: the data folder outside git that keeps every raw file.
- **Tile**: a 100 m × 100 m square of campus; the unit of building, loading
  and review.
- **Hero spot**: an area that gets LoD3+ (list in STANDARDS.md).

## Rules for every builder chat

- Follow the repository `AGENTS.md` and each box's `AGENTS.md`.
- **Never stop to ask Evan anything until the chat's stages are done.**
  Decide, write it in `progress/DECISIONS.md`, keep going.
- Things only Evan can do (email Vanderbilt GIS, capture photos, set up a
  GPU or ACCRE account, decide trademark use) go in `progress/EVAN-TODO.md`
  with exact steps. Build around them with stand-ins and public sample data,
  and mark what is waiting on him.
- **Cross-box changes are pre-approved.** Make them in their own commits
  tagged `[Vanderbilt cross-box]` and log each in
  `progress/CROSS-BOX-CHANGES.md` (file, why, how to revert). Core changes
  only add. Gameplay boxes never draw UI. Evan reviews all of it at the end.
- **Never lose data.** Nothing captured, downloaded or measured is deleted or
  overwritten; new versions are added beside old ones. No step may produce
  a model that a later texture bake can't build on (no missing UVs, no
  missing material IDs, nothing outside the campus frame).
- No Google Earth / Google 3D Tiles content anywhere in data or game. No
  drone flights by Evan. No Vanderbilt name or logos in a release build until
  licensing is sorted (see STANDARDS.md "Names and marks").
- Faces and license plates blurred in anything that leaves Evan's machine.
- All Unity use goes through the Deadly Jenga Unity coordinator
  (`deadlyjenga-worktree-pool` skill). Never on the central checkout.
- This PC has **no NVIDIA GPU**. Stages 0–5 must run on CPU. GPU jobs are
  optional extras, written as ready-to-run scripts.
- Work on your own task branch in a worktree; commit and push checkpoints as
  `AGENTS.md` describes.

## How the work is split

| Chat | Stages | Starts when | Ends with Evan reviewing |
|------|--------|-------------|--------------------------|
| A | 0 → 1 | now | walking the LoD2 campus in the game |
| B | 2 → 3 | A approved | the capture kit on his phone, and alignment on a sample set |
| — | Evan captures | B's kit is ready | at least Kirkland Hall + Alumni Lawn captured (more is fine) |
| C | 4 → 5 | B approved (works with whatever has been captured) | Kirkland at full detail, then the whole captured campus |

Chat C may start before capture is complete: buildings without photos stay
at LoD2 and are listed in `progress/EVAN-TODO.md` with the shots still
needed. Re-running Chat C's prompt later picks up new captures.

## Progress files (created by Stage 0)

`Docs/Development/VanderbiltCampus/progress/`:
`DECISIONS.md`, `EVAN-TODO.md`, `CROSS-BOX-CHANGES.md`,
`STAGE-<n>-results.md` (actual results, numbers and renders per stage),
`HANDOFF.md` (where the last chat stopped, branch, SHAs, what's next).
