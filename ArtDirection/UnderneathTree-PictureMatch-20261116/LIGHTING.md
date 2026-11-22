# Underneath the Tree — bulb pool tuning

To edit one patch of colored light, find its name in `BuildCourseBulbPools()`. Each row creates a real Unity spotlight. The approved version has 27 rows. The lights stay fixed in the room as the train moves or gifts fall through them. [Open the pool list and light helpers](../../Assets/_DeadlyJenga/Presentation/Visuals/UnderneathTree/UnderneathTreeFinish.cs)

![Approved desktop Play Mode overview showing small pools crossing track, gift edges, and open carpet](lighting-reference/room-overview.jpg)

The front-left blue crosses both rails and the box seam. The red to its left grazes the outside of the curve and continues onto the skirt. Behind the fir, some patches land entirely on yarn. Leave space between patches and vary which part of a prop catches light. The room beyond the skirt stays dark blue, with the fire and windows lighting their immediate surroundings. [Approved rear view](lighting-reference/rear-rug.jpg)

## Edit a pool

This is the approved front-left blue. The two vectors have different jobs.

```csharp
("Near-left rail", new Vector3(-29f, 11f, -151f), new Vector3(-5f, 21f, 14f), blue, 4600f, 63f),
// Name            Aim point in map-local space    Lamp offset from aim point  Tint  Power  Outer angle
```

The aim point is where the cone axis passes through the scene. The lamp itself sits at `target + offset`, here `(-34, 32, -137)`, and faces the target. X and Z run along the floor, Y runs upward. Use the local coordinates of **Christmas Eve Art and Lighting** under the active map root. The layout lab has its own coordinate frame.

| Desired edit | What to change |
| --- | --- |
| Move a patch | Move `target`. Keeping `offset` fixed moves the entire beam without changing its tilt. For this blue, changing target X from −29 to −25 shifts it four units along local +X. |
| Graze an edge or stretch a patch | Aim beside a seam or slightly below a lid. Increase the sideways part of `offset` relative to its height to hit a horizontal surface at a shallower angle. Reverse the sideways offset to light the opposite face. |
| Make it wider or narrower | Change the last value, the **full outer cone angle in degrees**. Try 72° in place of 63° for a wider footprint at the same throw. Moving the source farther away also widens the footprint and changes its brightness. |
| Soften or sharpen the edge | The pool loop passes `.28f` as `innerFraction` to `MakeSpot`. The inner angle is `angle * innerFraction`. Lower it for a smaller full-strength core and a longer fade. Raise it for a broader core and a tighter rim. Outer angle controls where the fade ends. |
| Dim a hotspot | Lower that row’s `power`. For example, 4600 → 3450 is a 25% reduction. Check the print on the lid and the side facing the lamp. |
| Change the color | Swap the row’s `blue` for `green`, or supply a new `Color(r, g, b)` in that row. Editing a palette variable changes every pool that uses it. The fir string has a separate palette. |
| Add or remove one | Duplicate a nearby row, give it a distinct searchable name, then change its target, throw, and tint. Delete a row to remove its spotlight. The loop derives the count automatically. |
| Limit how far it reaches | Range currently equals `offset.magnitude * 1.8f`. This multiplier is shared by all course spots. Moving the lamp farther from its target also increases range. To change range for one spot, give that row its own multiplier using the same pattern as softness below. |

**Softening just one spot.** The current list exposes angle and power per row. To expose softness too, append `float innerFraction` to the tuple type and its `foreach` deconstruction, append `.28f` to every existing row, then pass `innerFraction` as the final argument to `MakeSpot`. Try `.15f` on the chosen row. For a 63° cone the full-strength core then spans 9.45° instead of 17.64°, with the same outer footprint. The `.67f` default on `MakeSpot` is used by other callers, including the windows.

**A useful starting row.** For a patch on bare carpet, copy “Inside the near loop” (blue, Y = 1, power 1600, angle 54°). For a high gift, copy “Top stack ribbon” (red, Y = 44, power 3500, angle 64°). Place the aim point at the receiving height first. A floor-level target can leave the bow above it dark even with a brighter lamp.

## Preview the change in Unity

1. Enter the **Underneath the Tree** map (`workshop-under-the-tree`) in solo Demo. Use the intact layout for a repeatable comparison.
2. In the Play Mode Hierarchy, expand **Christmas Eve Art and Lighting** or search for the exact row label. Select the Light and use the spotlight gizmo and Light Inspector to audition position, aim, color, intensity, and inner and outer angles.
3. Write the chosen values back into the row. Play Mode Inspector tweaks are temporary. For a manually moved lamp, subtract the desired local target from its local position to recover the row’s offset. Re-enter the map to verify the saved edit.
4. Compare these survivor-camera views. For a repeatable screenshot, use a 16:9 frame and the listed vertical FOV. The desktop references below are reduced copies of the approved 3840 × 2160 Play Mode captures.

| View | Camera position | Unity Euler rotation | FOV | What to inspect |
| --- | --- | --- | --- | --- |
| [Overview](lighting-reference/room-overview.jpg) | (44.175, 81, −158.15) | (27.191, 311.422, 0) | 64° | Separate RGBY patches, darker intervals, dark blue room beyond the skirt. |
| [Rear rug](lighting-reference/rear-rug.jpg) | (−66, 30, 39) | (28.533, 186.459, 0) | 73° | Spill behind the trunk and across open yarn. Cranberry yarn makes several hues read redder here. |
| [Rail height](lighting-reference/rail-height.jpg) | (−83, 17, −168) | (21.998, 313.831, 0) | 59° | Print and rail silhouettes stay legible as a pool crosses from one gift to its neighbor. |
| [Gift-top spawn](lighting-reference/gift-top-spawn.jpg) | (−185.656, 17.308, −105.315) | (12.225, 88.328, 0) | 75° | Check the blue on the near lid and upright face at player scale. |

## How the look was built

- **Course lighting.** Each named spotlight uses a different position and throw. The list includes separate low lights for the carpet, rear aisle, and space behind the fir. The map creates them through `MakeLight`, which registers local lights with the existing blackout lifecycle. The room rendering profile uses a dedicated Forward+ renderer so a large mesh such as the skirt can receive nearby lights across its surface without the old Forward per-object limit. [Room rendering profile](../../Assets/_DeadlyJenga/Presentation/Visuals/Resources/UnderneathTreeFinish/Room%20Rendering.asset) · [Christmas room renderer](../../Assets/_DeadlyJenga/Presentation/Visuals/Resources/UnderneathTreeFinish/Christmas%20Room%20Renderer.asset)
- **Visible tree bulbs.** `LightFir` reads authored bulb positions and tints from *Fir Lights*. It adds a small mesh and halo at each bulb, and a point light near selected bulbs to color branches and ornaments. Among the first 44 bulbs it lights every third one, then every eleventh. Those lights use power 970 and range 32. Adjust these if the *needles* need more or less color. Adjust the course rows if the *gifts or rug* need it. [Fir lighting](../../Assets/_DeadlyJenga/Presentation/Visuals/UnderneathTree/UnderneathTreeFinish.Fir.cs)
- **Paper, foil and bows.** The earlier gift shader added colored shine based on viewing direction, even in shadow. The approved shader lets actual lighting and reflections shape the highlight. Coated paper is dielectric, the snowflake mask picks out metallic foil, and bow materials have their own smoothness and metallic values. For a sharp highlight confined to bows, adjust the bow material. For a whole overexposed pool, start with its spotlight power. [Gift finishes](../../Assets/_DeadlyJenga/Presentation/Visuals/UnderneathTree/UnderneathTreeFinish.Room.cs) · [Foil shader](../../Assets/_DeadlyJenga/Presentation/Visuals/Resources/UnderneathTreeFinish/Christmas%20Foil%20Paper.shader) · [Bow material](../../Assets/_DeadlyJenga/Presentation/Visuals/UnderneathTree/UnderneathTreeFinish.Detail.cs)
- **Room and glow.** `BuildLighting` gives the room two weak directional fills (0.06 and 0.055 intensity) and places the fireplace and moonlight separately. `RestoreIndoorSettings` supplies the dark ambient sky, sides, and ground. The grade uses ACES, exposure +0.1, bloom threshold 0.9, intensity 0.22 and scatter 0.72. Bloom changes the glow around bright pixels across the image. The spot’s inner angle controls the softness of light falling on a surface. [Room lights and grade](../../Assets/_DeadlyJenga/Presentation/Visuals/UnderneathTree/UnderneathTreeFinish.cs)

The approved evidence is desktop solo Play Mode with the normal lights on. For a larger increase in light count or coverage, check frame time on Quest in the densest overlapping view. Headset performance for this lighting pass is unmeasured.
