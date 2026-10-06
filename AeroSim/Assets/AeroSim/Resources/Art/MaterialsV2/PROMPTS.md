# Materials V2 — original generated albedo assets

Generated with the built-in `image_gen__imagegen` tool, one independent generation per asset. No external stock images, logos, copied DJI texture maps, CLI, or API fallback were used. The textures are intended for the Unity flight simulator's original scenery and aircraft geometry.

## Assets and visual QA

The first three outputs are RGB square PNG files; the foliage addition is RGBA. All four are 1254 × 1254 pixels. The generated images were visually inspected before acceptance. They use diffuse, mostly even illumination and contain no text, brands or scene borders.

- `meadow-grass-v2.png`: muted dry coastal meadow grass and soil; intended 2 m approximate tile scale. Fine grasses are visible at close range; use mipmaps/anisotropic filtering for stable oblique flight views.
- `coastal-rock-v2.png`: gray weathered mineral surface with medium-scale irregular detail. Suitable for rocky ground and triplanar mapping on boulders.
- `aircraft-shell-v2.png`: light warm-gray fine polymer micrograin. The finish intentionally stays subtle; shape, shading and separate lens/metal materials should carry the drone's main visual definition.
- `evergreen-needles-v2.png`: additional non-tileable RGBA pine branch cluster, 1254 × 1254 pixels, for alpha-tested foliage cards. Genuine alpha channel was verified with macOS `sips -g hasAlpha`; the visible result has irregular needle edges and internal gaps. Unlike the three surface albedos above, this asset contains transparency. Use clamp wrapping and an alpha cutoff (approximately 0.35–0.5), double-sided foliage shading, mipmap coverage preservation and varied card rotation/scale. Fine edge softness means this is intended as foliage-card dressing, not a botanical close-up reference.

## Limitations

These are AI-generated color/albedo textures, not measured photogrammetry PBR scans. Seamlessness was requested but edge matching is not mathematically guaranteed. Avoid conspicuous repetition using secondary scale blending, UV rotation or triplanar mapping, and confirm in the actual renderer. There are no authored normal, roughness, displacement or occlusion maps in this deliverable. Texture-only changes do not achieve commercial DJI simulator fidelity without corresponding geometry, lighting, environment dressing and platform performance work.

## Full generation prompt set

### evergreen-needles-v2.png — foliage pass addition

```text
Use case: photorealistic-natural
Asset type: original RGBA pine foliage cutout texture for alpha-tested Unity foliage cards.
Primary request: a single dense natural coastal evergreen pine branch cluster, a flat branching fan of realistic medium dark green pine needles and a few fine brown woody twigs.
Style/medium: photorealistic detailed botanical material cutout, not an illustration, tree render, landscape or full tree.
Composition/framing: front-on orthographic view of a broad roughly square fan-shaped branch cluster, spread naturally in the plane of the image. It fills most of the square image with a thin transparent safety margin on all edges. Irregular organic silhouette with many individual needle tips, small transparent gaps within the foliage, subtle asymmetry. No artificial rectangular foliage boundary.
Lighting/mood: soft flat neutral diffuse daylight from all sides, no prominent directional shadows, no specular glare, no lighting gradient.
Color palette: subdued varied natural coastal pine greens with fine warmer brown twigs, no oversaturated neon green.
Constraints: genuinely transparent alpha background, retain individual fine needles and interior gaps. No checkerboard painted into the image, no white matte or colored backdrop, no halos, no ground, sky, trunk, roots, pot, complete tree, text, labels, logos or watermark. One single isolated pine branch cluster only, not a sprite sheet or multi-panel preview. This is not tileable.
```

### meadow-grass-v2.png

```text
Use case: photorealistic-natural
Asset type: original seamless tileable albedo texture for a Unity flight simulator terrain.
Primary request: an evenly distributed close top-down surface of short California coastal meadow grass, faded olive green blades mixed with fine dry straw and small irregular patches of muted sandy brown earth.
Style/medium: realistic material capture, high fidelity natural surface detail, not concept art or a landscape photo.
Composition/framing: a square full-bleed orthographic texture swatch covering approximately 2 by 2 meters, perfectly straight down, no perspective or horizon. The grass and dirt distribution should be uniform stochastic detail with medium-low contrast and no dominant patch.
Lighting/mood: completely flat neutral diffuse illumination, de-lit albedo, no directional shadows, no highlights, no ambient occlusion, no vignette.
Color palette: subdued olive and sage greens, tan dry stems, dusty earth. Avoid saturated vivid green.
Constraints: seamless tileable left/right and top/bottom edges; all edges must wrap convincingly. Output one single square texture only, no tiled preview, no border, no grid, no labels, no text, no branding or watermark. No flowers, trees, large rocks, objects, paths, or repeated focal patterns. Sharp consistent focus over entire texture.
```

### coastal-rock-v2.png

```text
Use case: photorealistic-natural
Asset type: original seamless tileable albedo texture for rocky coastal terrain and stones in a Unity flight simulator.
Primary request: a weathered gray coastal granite and slate stone surface with fine mineral grains, restrained small fractures and salt-weathered mottling.
Style/medium: realistic scanned-material appearance, physically plausible base color only, not a photograph of a rock in a scene.
Composition/framing: square full-bleed orthographic surface swatch, straight-on to a flat stone surface, all detail distributed without a central focal crack. Fine to medium stone detail, low-to-medium contrast, broad calm color variation.
Lighting/mood: completely flat neutral diffuse illumination, de-lit albedo, no directional shadows, baked lighting, specular highlights or vignette.
Color palette: natural neutral medium-light gray with subtle slate blue and beige mineral variation. Neither nearly black nor white.
Constraints: seamlessly tileable on all four edges, no edge discontinuities, no border, grid, preview panels, text, logos or watermark. No protruding isolated rocks, plants, moss, ocean, horizon or perspective. One single square texture, uniformly sharp.
```

### aircraft-shell-v2.png

```text
Use case: photorealistic-natural
Asset type: original seamless tileable albedo material texture for the shell of a compact civilian camera drone in a Unity flight simulator.
Primary request: premium injection-molded light warm-gray matte polymer surface with extremely subtle dense fine micrograin, nearly uniform overall color, clean new material with physically plausible barely visible microspeckle.
Style/medium: photorealistic material swatch, base color only. This is a surface texture, not a picture of a drone or product.
Composition/framing: one square full-bleed orthographic material swatch, straight-on, distributed ultra-fine material detail over entire image.
Lighting/mood: fully uniform neutral diffuse illumination, de-lit albedo, no shadows, highlights, lighting gradients or vignette.
Color palette: light warm gray, around RGB 190/191/187, very low contrast. Avoid pure white, beige cast or dark gray.
Constraints: seamless all four edges, no visible seams, panel lines, vents, rivets, screws, scratches, text, labels, branding, logos, borders, product silhouette, tile preview or watermark. No objects or scene. Only clean fine polymer grain.
```
