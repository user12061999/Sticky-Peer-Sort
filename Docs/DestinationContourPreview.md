# Green sticker contour preview

The destination preview follows the incoming sticker's alpha silhouette instead of drawing a rounded rectangular frame. It uses a thin green edge with a softer outer glow and a gentle pulse. The original destination image, tint and artwork stay unchanged.

`DestinationPreviewGlow` samples the existing baked alpha mask, computes an outside-only contour, and fits it to the same artwork bounds used by the destination. Transparent padding preserves outlines at the texture edges. For a waiting slot it previews the incoming sticker at its eventual landing size.

The temporary texture and sprite belong to the active preview and are released when it is hidden/destroyed. Preview images do not intercept raycasts. Cancel and collection remove the glow as before. No check/clock badge or rectangular frame is added.

The 2026-10-28 review MP4 was recorded before this green silhouette refinement; it remains a record of the earlier animation review.

Validation: 3/3 targeted PlayMode checks passed (883f59c5b1c54ef8b28fb39bbc293844): destination preview/cancellation, gallery collection/undo and waiting-area landing. Verified green tint, transparent corners and zero overlay alpha inside the original opaque artwork.

The green outline and soft outer glow widths are now 3x the initial version. Texture padding increased proportionally, so the artwork remains aligned and the wider glow is not cropped.
