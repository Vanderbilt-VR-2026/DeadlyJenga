# Christmas material sources

The following Poly Haven assets are CC0. The desktop material maps retain the source 4K resolution. Unity uses reduced import limits on Android.

- Velour Scan and normal: [Velour Velvet](https://polyhaven.com/a/velour_velvet), photography by colormass and processing by Rico Cilliers.
- Fleece Scan and normal: [Polar Fleece](https://polyhaven.com/a/polar_fleece), photography by colormass and processing by Rico Cilliers.
- Pine Scan and normal: [Pine Bark](https://polyhaven.com/a/pine_bark).
- Fir Twig Atlas: diffuse and opacity maps from [Fir Tree 01](https://polyhaven.com/a/fir_tree_01), modeled by Rico Cilliers with photography by Rob Tuytel. Opacity is packed into the diffuse alpha channel.

## Gold Snowflake Foil (2026-11-15)

Generated with the built-in image generation tool for this project. Native output: 1254 x 1254 pixels; not a native 4K texture. Prompt: seamless flat neutral-grey paper albedo with delicate, warm-gold six-armed snowflakes, approximately four motifs across, scattered small stars and dots, no objects, text, folds or lighting gradients. The red-minus-blue separation drives the foil mask in Christmas Foil Paper.shader. Kept as a new sibling asset; the prior Winter Print is retained.

## Cranberry Rug Weave and matching normal (2026-11-15)

Built-in image generation, native 1254 x 1254 albedo and matching tangent-space normal assets. The normal is an authored approximation, not a measured scan. The prior fleece texture remains in use on the ivory edging.

Albedo prompt: Production PBR albedo texture for a luxurious cranberry-red Christmas tree rug in a realistic Unity game. Square seamless repeatable tile, orthographic top-down macro textile scan, fill whole image edge to edge. Dense plush chunky loop-pile wool, visibly intertwined irregular curled yarn loops, warm crimson and deep ruby fibers, fine stray soft fibers, realistic dark self-occlusion between loops but even neutral diffuse illumination overall. About 20 to 25 distinct thick loops across the tile, giving clearly readable physical yarn depth. Faithful cozy expensive real living-room rug rather than plastic or flat felt. No borders, no trim, no objects, no perspective, no words, no folds, no broad light gradients, no highlights or cast shadows from outside the textile. Seamless in both directions. Highest available resolution and crisp fine fiber detail. This is a reusable texture asset, not a room illustration.

Normal prompt: Convert this exact textile texture into a matching tangent-space NORMAL MAP for a PBR game material. Preserve the same wool-loop positions, shapes, scale and arrangement exactly. Output only the normal map, square full bleed, seamless repeat. Standard blue/purple tangent-space encoding: a flat upward-facing surface is RGB 128,128,255; raised yarn loops have smooth directional red/green gradients describing their curved surfaces; fine fibers have subtle normal variation. OpenGL +Y green convention. No red albedo remains, no photographic lighting, no baked shadows, no black cavities, no text, no borders. Smooth correct blue normal vectors throughout, richer directional gradients on the thick curled loops, subtle detail on fibers. This normal map will pair with the supplied image as albedo.
