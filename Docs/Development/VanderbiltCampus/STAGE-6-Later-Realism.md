# Stage 6 (later): realism

**Not built in this round.** This page says what the realism step will do,
so Stages 0–5 keep everything it needs. Run it once there's a strong NVIDIA
GPU (ACCRE, a rented cloud GPU, or a new PC).

## What it does, using only the archive
1. **Dense reconstruction** per building from the archived photos (COLMAP
   MVS or RealityScan meshing, or a Gaussian splat trained with Brush,
   LichtFeld or a city-scale method such as Hierarchical 3DGS or HUG).
2. **Bake onto the existing models**: project the aligned photos onto each
   building's UV0 at the texel density in STANDARDS.md §7. The models don't
   change shape.
3. **De-light**: remove baked sun and shadow using the gray cards and
   overcast captures (Agisoft Texture De-Lighter or a learned inverse
   rendering method), producing albedo, plus normal and roughness where
   possible.
4. **Restore merged detail** from the ledger: `normal_map` entries become
   baked normals from the dense mesh; `geometry` entries become real
   geometry where the budget allows; `decal` entries become decals.
5. **Tiling materials** from swatch shots for ground, brick and stone,
   blended with the bakes to hide seams.
6. **Swap materials per ID**: a third look ("realistic") next to candy and
   plain colors in Presentation. World stays unchanged.
7. **PC trailer extras**: a photoreal splat flythrough of hero spots for the
   Trailer Studio, PC only.

## Quest notes
- Baked textures in ASTC, atlased per tile; streaming textures with the
  tiles; budget per STANDARDS.md §10.
- Splats on Quest only for tiny hero moments (roughly ≤ 100k splats).

## Before starting, check
- The Stage 5 readiness checker shows zero missing items.
- The archive verifies cleanly and has a backup.
- Vanderbilt GIS data, if received, is included with its terms recorded.
