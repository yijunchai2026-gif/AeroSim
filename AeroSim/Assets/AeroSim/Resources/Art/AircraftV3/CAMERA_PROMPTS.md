# Camera aircraft shell textures V3

Generated with the built-in imagegen tool, not fallback CLI/API. These are original material swatches inspired by the finish categories of camera drones. They are not official DJI artwork, licensed product textures, model UV atlases, or photogrammetric measurements. No logos or trademarks appear in either bitmap.

## Delivered assets

- `air-shell-v3.png`: 1254 × 1254 PNG; medium cool-gray finely grained polymer; imported as reusable base-color/albedo texture.
- `mavic-shell-v3.png`: 1254 × 1254 PNG; darker warm-neutral charcoal finely grained polymer; imported as reusable base-color/albedo texture.

Both final files were copied into this workspace from the generator output; originals remain untouched.

## Visual acceptance

Both files were opened at full resolution and inspected. They contain only uniform low-contrast material micrograin: no aircraft photographs, UV layouts, text, labels, screws, scratches, seams, panel lines, obvious focal shapes, highlights, or directional shadow gradients. Medium gray and charcoal swatches are visibly distinct.

Limitations: generative albedo approximation only. Seamless repetition was requested and there are no visible edge borders, but exact edge-pixel continuity has not been certified. No independent normal, metallic, height, or roughness bitmap is included. Keep Unity tiling subtle and shader smoothness modest to avoid magnifying the grain. These materials do not certify an exact physical match to real DJI product finishes.

## Air shell final prompt

```text
Use case: stylized-concept
Asset type: seamless tileable game texture, physically based drone shell albedo/base-color map
Primary request: create an original square full-frame flat material swatch for a premium medium-size camera quadcopter shell, suitable for an Air 3S-inspired cool-gray polymer finish.
Subject: uniform medium cool-gray injection-molded aerospace polymer with extremely fine subtle isotropic micrograin, clean factory-new satin material; slightly blue-neutral gray, very low contrast color variation.
Composition/framing: orthographic top-down scan of a flat surface that fills every pixel; square texture; no object or background; no focal elements; seamless repeating edges in both axes.
Lighting/mood: completely delit albedo only, uniform neutral illumination; no highlights, no reflections, no shading gradients, no ambient occlusion, no baked lighting.
Materials/textures: realistic fine molded-polymer grain at microscopic scale, calm smooth uniform color; not concrete, stone, cloth, carbon weave or leather.
Constraints: generate only an original reusable material texture, not a photograph or render of a drone, not a UV atlas; no logos, no trademarks, no words, no symbols, no seams, no panel lines, no screws, no scratches, no wear, no borders, no watermark. Square high-resolution production bitmap.
```

## Mavic shell final prompt

```text
Use case: stylized-concept
Asset type: seamless tileable game texture, physically based drone shell albedo/base-color map
Primary request: create an original square full-frame flat material swatch for a premium multi-camera quadcopter shell, suitable for a Mavic 3 Pro-inspired darker charcoal warm-neutral aerospace polymer finish.
Subject: uniform refined charcoal dark-gray injection-molded polymer; warm-neutral rather than blue, clean factory-new low-satin finish, extremely subtle fine isotropic micrograin with very low contrast and understated depth.
Composition/framing: orthographic top-down scan of a flat surface that fills every pixel; square texture; no object or background; no focal elements; seamless repeating edges in both axes.
Lighting/mood: completely delit albedo only, uniform neutral illumination; no highlights, no reflections, no shading gradients, no ambient occlusion, no baked lighting.
Materials/textures: realistic clean premium polymer, microscopic fine molded grain only; not concrete, stone, cloth, carbon weave, leather or metallic brushed material.
Constraints: generate only an original reusable material texture, not a photograph or render of a drone, not a UV atlas; no logos, no trademarks, no words, no symbols, no seams, no panel lines, no screws, no scratches, no wear, no borders, no watermark. Square high-resolution production bitmap.
```

