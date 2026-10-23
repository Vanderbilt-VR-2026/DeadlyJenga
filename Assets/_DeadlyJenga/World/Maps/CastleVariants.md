# Experimental castle structures

The lobby lists **Candy Castle · Catwalks** and **Candy Castle · Four Keeps** separately from **Candy Castle · Long Blocks and Gaps**. All retain the widened outer ring and its 3 m gaps. The original map prefabs are unchanged.

## Catwalks

Four one-metre-wide catwalks connect the four higher red wall towers to the purple keep. Each rests on a two-step rooftop bearing cap and an actual keep block. Offsetting the decks to a red tower rail gives them real support rather than spanning from the hollow centre of a tower. Two one-metre rises lead onto each deck; the first step is wider on the two side-facing approaches to bear on the tower rail.

Every four keep courses, starting above the first course, a cross-shaped deck connects all four cardinal purple blocks. The second perpendicular beam rests 0.4 m above the first on its middle and two end pads. This avoids intersecting boxes and gives both beams actual support.

All decks, pads and steps are ordinary loose CastleBlock rigidbodies. No fixed joints, invisible columns, kinematic braces or forced sleep hold them up. Pulling supports or hitting beams can collapse them. Catwalks have their own physical dimensions and volume-based mass; normal blocks keep their original mass.

## Four Keeps

Four octagonal keeps made from the original 6 × 2 × 2 m blocks rise to the high red rooftop level (32 m), with a small separation between them. Four butt-jointed bearing beams form a square ring immediately above them. The full 30-course, 9 m-block octagonal keep is built above the ring, reaching 94 m overall. The ring beams are larger structural members, supported by the four small keeps.

## Integration

The layout owns optional per-placement dimensions. The existing layout JSON includes the variant selector; both sides derive identical sizes and body ordering before normal world snapshots run. Footprints use each bottom block's dimensions. Both variants remain inside the 57 m castle footprint and 61 m foundation; disaster bounds also include each variant's full height.

Run **Tools > Deadly Jenga > Validation > Check Castle Variants** for dynamic gravity, impulse, actual map loading and layout/replica checks. Reports and overview images go to `Logs/Validation`. Gravity checks do not certify every destructive arrangement, headset performance or two-device networking.


Editor verification on 2026-10-23: both variants loaded from the actual match map selector, restored mixed-size replica layouts and passed the disaster validator. After 30 seconds of actual PhysX gravity the catwalk layout (660 bodies) had maximum drift 0.166 m / tilt 0.792 degrees; the four-keep layout (876 bodies) had maximum drift 0.073 m / tilt 0.164 degrees. Exposed top blocks responded to an impulse. The tall central keep uses 60 position / 16 velocity solver iterations; Quest performance for this larger experimental structure has not been measured.
