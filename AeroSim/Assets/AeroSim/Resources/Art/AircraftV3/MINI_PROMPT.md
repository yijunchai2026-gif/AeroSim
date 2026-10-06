# Mini-style shell material V3

Asset: `mini-shell-v3.png`
Size: 1254 × 1254 pixels, PNG.
Generation mode: built-in `image_gen.imagegen`, new image generation, opaque background, no image references. No API/CLI fallback used.
Created: 2026-10-05.

## Complete prompt

```text
Use case: stylized-concept
Asset type: square seamless tileable game material albedo texture for a compact folding consumer quadcopter shell, inspired by the light cool-gray matte polymer material category of a Mini 4 Pro, not its branding or shape.
Primary request: produce a production-use flat 2D base-color texture of clean light cool-gray injection-molded engineering polymer. Entire image is one continuous uniform material field; very fine realistic isotropic molded micrograin and subtle tiny tonal variations, low contrast, predominantly pale silver-gray with a slight cool cast. Clean new product condition.
Style/medium: photorealistic material scan, delit unlit albedo only. Orthographic top-down flat material field, square 1:1.
Composition/framing: material fills every pixel edge to edge, even scale, no objects, no focal point. Seamless repeating edges across both axes; no directional grain, no broad color gradient.
Lighting: strictly neutral uniform delit color, no baked shadows, no specular highlights, no vignette, no ambient occlusion.
Constraints: original texture, no text, no logos, no DJI marks, no labels, no watermarks, no scratches, no dirt, no cracks, no panel lines, no vents, no screws, no seams, no rivets, no recognizable drone, no product photo, no UV islands or atlas. Only one uniform polymer material. Fine micrograin should remain subtle and non-repetitive.
```

## Validation and limits

Inspected the saved project asset directly: continuous pale cool-gray fine molded polymer field; no text, logos, drone objects, panel seams or atlas islands. It is a new original material inspired by the category of lightweight consumer-drone plastics, not an official DJI asset. It is albedo only, not a certified PBR scan or a normal/roughness set. Seamless repeat was requested but image-generation outputs do not guarantee pixel-exact border continuity; test tiling and choose sensible repeat scale in Unity. Runtime normal/roughness and mesh geometry should provide shading rather than baking large highlights into this texture. A 1254-pixel image is non-power-of-two; Unity import can resize/compress it for iOS.

