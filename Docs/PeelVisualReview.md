# Peel visual review — 2026-10-28

The update addresses the review in its requested order.

1. **Rendering/masking:** interactive and flying stickers receive an independent Canvas sort boundary above all board canvases. Their images do not participate in inherited board masks. World size/rotation are preserved when transferring from a scaled board to the overlay. Reattachment remains on the overlay until a flat frame has rendered, then returns to the source. Cancel events use the same reattachment animation.
2. **Flexible front/back:** the mesh now uses 64 × 16 cells, a gently curved crease, a continuously bending tangent instead of a rigid tail, and depth-ordered triangles for self-overlap. Front and back use the same sprite UV/alpha surface. The crease starts from sampled opaque artwork bounds, not transparent PNG padding. Continuous front/back blending, warm off-white shading and a narrow crease highlight replace the flat white cutout appearance.
3. **Destination preview:** the source target's color and artwork remain unchanged. A thin, translucent rounded outline pulses in a softened UI accent color. It is removed on commit/cancel and does not intercept input. No check/clock badge returns.

The editor-only Recorder helper replays pointer-down/drag/up events through the production StickerView/StickerDragPeel handlers on the actual Level19 cat. A cyan ring marks the input location. Captions identify partial peel/drag back, release/reattach and full peel/collection. It is an input replay, not a recording of physical finger input. Production animation durations are used.

Video: `Docs/Review/StickerPeelReview_2026-10-28.mp4` (1080 × 1920, 30 fps).

Repeat the capture in Play Mode in SampleStickerLevel with **Tools > Sticker Category Game > Record Peel Visual Review (Play Mode)**. The helper uses the already installed Unity Recorder package and is excluded from builds.

Final validation: 76/76 EditMode tests passed (50cde8da6fbc47f4bdba14ccf343ab07), 27/27 GameplaySmokeTests passed (085d98796fbd4c7a9a5e8840f47c9223), and 2/2 ReferenceStylePlayTests passed (e46704e4ed5440a3bfae6aec5d3aed0c). Coverage includes sorting above a masked/scaled board, overlay size preservation, flat-frame handback, target artwork tint preservation, flexible fold continuity and all sample landing traces.

The final MP4 was decoded with Unity VideoPlayer and checked at intermediate and final collection frames: 1080x1920, 30fps, 396 frames, 13.2 seconds. File size: 7,908,550 bytes. The final frame shows the cat placed into its exact ANIMALS target (1/4).
