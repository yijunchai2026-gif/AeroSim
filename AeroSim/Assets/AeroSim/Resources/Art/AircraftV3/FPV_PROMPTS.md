# FPV aircraft material set V3

Created with the built-in imagegen tool. These are newly generated original material surfaces, not extracted DJI product assets. “Avata-style” means a premium compact ducted-FPV material direction; no logos or licensed texture sheets are included.

## Assets and integration

- `avata-shell-v3.png`: clean dark graphite molded polymer, for shell and propeller ducts. Suggested roughness 0.72–0.84; metallic 0.0. Use repeat wrapping and a small UV scale (about 0.15–0.4 tile across a shell face) so the material stays subtly grained rather than coarse.
- `carbon-weave-v3.png`: low-contrast carbon composite weave for structural/detail parts. Suggested roughness 0.48–0.68; metallic 0.0. Use restrained area coverage. The generated weave is approximately 32–40 repeats across the source width, so avoid extreme magnification.

Both actual PNGs are **1254 × 1254 RGB**, though the prompt requested 1024 × 1024. The built-in generation tool does not expose a deterministic size argument. Original generated files were copied unchanged into this workspace. Engine import may cap max size at 1024 for mobile/desktop memory without changing the source PNGs.

Visual inspection completed on both images: full-canvas material, no drone photograph, no logo/text, no UV islands, no major painted lighting gradient. They are intended to tile, but exact pixel-perfect opposite-edge equality is not certified. Test mipmaps and grazing-angle repetition in Unity; do not describe these as calibrated PBR scans. These are albedo-only assets, not measured roughness/metalness/normal maps. Do not derive physical flight behavior from them.

Source tool files:

- Shell: `/Users/bytedance/.codex/generated_images/01a10eb0-2c4e-7533-b4b6-d697d31a7d35/exec-9599af88-012c-45f8-888b-b11a0b7075d2.png`
- Carbon: `/Users/bytedance/.codex/generated_images/01a10eb0-2c4e-7533-b4b6-d697d31a7d35/exec-ca09d2cc-7a7f-45ad-b7e8-0e090cfa079c.png`

## Final shell prompt

```text
Use case: stylized-concept
Asset type: seamless tileable albedo texture for a realtime 3D FPV drone shell, not a product picture.
Primary request: a square photorealistic surface texture of clean dark graphite gray injection-molded engineering polymer, with exceptionally fine uniform molded micrograin. Designed for a premium compact ducted FPV drone. The surface is new and understated, neither scratched nor dirty.
Composition/framing: orthographic straight top-down view of the flat material covering the entire canvas edge to edge; no physical object, no perspective. Seamlessly repeatable horizontally and vertically, perfectly matching opposite edges, no distinctive focal marks.
Lighting/mood: completely delit neutral diffuse base color only, uniform exposure across image. Material reflectance will be added by the 3D renderer, not painted in.
Color palette: subdued charcoal graphite, medium-dark gray rather than pure black; tiny restrained grain variations only.
Constraints: square 1024 by 1024 desired, seamless tileable texture, no text, no logos, no brand marks, no diagrams, no UV islands, no drone silhouette. No highlights, cast shadows, directional lighting, vignette, borders, edges, seams, panel lines, chips, grunge or large-scale mottling.
```

## Final carbon prompt

```text
Use case: stylized-concept
Asset type: seamless tileable albedo texture for realtime 3D drone carbon composite components, not a product picture.
Primary request: photorealistic extremely fine 2x2 twill woven carbon fiber composite surface, small regular interlaced carbon tows with subtle realistic individual fiber strands. Clean premium engineering material, restrained and low contrast, charcoal graphite rather than shiny black checkerboard.
Composition/framing: square, orthographic straight top-down macro scan of a flat sheet covering the entire canvas; regular tiny diagonal twill weave repeat with about 32 weave repeats across the full width. Opposite edges must match seamlessly in both axes. No object silhouette and no perspective.
Lighting/mood: completely delit albedo/base color only, uniform neutral exposure. No resin reflections or specular highlights: renderer adds those later.
Color palette: dark graphite gray, understated slight gray differences between fiber directions, realistic matte microtexture. Fine small-scale weave, no oversized dramatic checker pattern.
Constraints: square 1024 by 1024 desired, seamless tileable, no text, no logos, no brand marks, no UV layout, no physical object, no scratches, no glitter, no bright silver strands, no lights, no directional shadows, no vignette, no border or seams.
```

