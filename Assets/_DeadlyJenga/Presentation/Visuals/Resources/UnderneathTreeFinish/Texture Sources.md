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

## Crimson Damask upholstery
Generated with the built-in image-generation tool on 2026-11-15 for this map. Original source is 1254 x 1254, not native 4K. The matching normal is an authored approximation, not a measured material scan.
Albedo prompt: seamless orthographic crimson velvet damask, tonal acanthus and floral motifs on oxblood ground, realistic short pile, evenly diffuse illumination, no gold, shadows, borders, folds or scene objects. Normal prompt: convert that exact image to shallow OpenGL tangent-space floral textile relief with matching layout, neutral blue-purple flat areas, no albedo or lighting. Used on the sofa cushion and arm material with moderate repeating scale and fiber sheen.

## Noble Fir Sprig
Generated with the built-in image-generation tool on 2026-11-15, transparent-background mode; source 1254 x 1254. Prompt: one isolated photorealistic noble fir sprig, vertical brown stem bottom-to-top, asymmetric lateral twigs, crisp individually distinguishable emerald needles and lighter tips; neutral diffuse light, transparent gaps, no snow or ornaments. Replaces the old atlas sampling with a full-sprig region. Alpha coverage is preserved in importer mipmaps. Runtime appearance requires actual capture review.
