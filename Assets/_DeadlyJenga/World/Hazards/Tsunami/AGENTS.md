# Tsunami

The prefab in Resources/DemoDisasters is the authored configuration. All distances are metres,
times are seconds, push/lift are m/s², and carried-speed limit is m/s.

- Height is above IWorldQuery.WaterHeight. Depth is front-to-back thickness.
- Width Override = 0 fits the projected map width, margin, and tapered edges automatically.
- Approach Heading is where the wave comes FROM: north/+Z = 0, east/+X = 90.
  Random Direction selects this once on the host; clients never choose their own direction.
- Offshore Distance is from the near map edge to the initial leading edge. Warning Time
  happens first, then the wave rises while approaching. To form fully offshore, allow at least
  Rise Time × Approach Speed metres before reaching the island.
- Recession Distance is measured from the far map edge to the trailing edge. Fall Time begins
  only after that point (and after formation has finished). The wave keeps moving as it falls.
- Route and phase timing determine lifetime. Base Duration is used only when Use Duration
  Timeout is checked; enabling that option can deliberately cut a wave short.
- Intensity changes height and force through the two explicit multipliers. It does not change
  map size, width, travel speed or duration. Target resistance defaults to body mass/reference
  mass for blocks and 1 for characters; targets may implement IWaterResistance. Higher resists more.
- Affected Players/Blocks, Respect Shelter and Damage Per Second are optional. Damage is off by
  default. Existing ocean elimination handles falling below the map's kill height.

## Shape and performance
The wave shape is data: Resources/TsunamiProfile.json, exported from the Blender source
(Workboard Blender-Disaster-Art-Pass/Tsunami/scripts: tsunami_profile.py -> export_tsunami_profile.py).
It is a breaking "surfer" wave: gentle back, concave rippled face, a lip thrown forward into a barrel
that peels along the width, bowed in and out along its length, tapering to nothing at the ends.
- `path` is the full cross-section (with overhang) per lateral sample; Presentation draws it.
- `envelope` is the highest water at each along/lateral point (no overhang); SurfaceHeight uses it.
- All values are normalised to the WaveFrame's depth, width and height (envelope max is exactly 1).
To change the shape, edit tsunami_profile.py and re-export; gameplay and every tsunami shader
follow automatically (CandySurgeSurface.hlsl reads the same data through _SurgeProfile/_SurgeEnvelope).
If the file is missing, SurfaceHeight and the shaders fall back to the original analytic hump
(crest at .72, steep front, outer 15% tapered). Small shader-only ocean ripples are decorative;
do not introduce gameplay dependence on the presentation mesh.

One nonallocating overlap-box query gathers candidates per physics step; a height-profile test
determines actual submersion. Abilities are cached per collider and receivers are deduplicated.
There are no water mesh colliders, water rigidbodies or particle collisions. The query buffer
grows only on saturation, up to 32768 colliders. Profile dense towers: waking many blocks still
costs physics time even with a cheap wave shape.

Presentation owns the GPU surface, foam, 72-particle spray cap and ocean blend. It reads Core
WaveFrame only. The map is the source of water level. Ocean materials supply the colors, ripple
settings and reflections. The crest does not perform a full fluid simulation or overturn.

## Networking and cleanup
Only the host runs impacts. Its remote-survivor trigger proxies forward pushes/damage to the
player owner; impacts are combined into the existing 10 Hz session update. Host block movement
continues through the existing world snapshots. Clients receive WaveFrame for visuals and late
joins, with short bounded extrapolation. Only one active instance of a disaster type is allowed.
Stop, scene shutdown and host migration cancel the wave. Migration intentionally does not resume
an in-progress disaster. Never run a second client-side impact simulation.

## Verification
Run Tools > Deadly Jenga > Test Disaster Map Contract, then the Game Play Mode smoke check.
Verify random/fixed approach, nonzero map origins, large/small maps, stop/retrigger and optional
damage. Check two players, late join and host migration. Profile CPU physics and GPU transparency
on Quest 3; desktop compilation does not certify headset performance or visual quality.
