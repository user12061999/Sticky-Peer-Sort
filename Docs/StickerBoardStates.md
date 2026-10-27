# Board sticker states

The board uses the existing GameSession rules and alpha hit areas. Visual decorations do not intercept input.

| Appearance | Meaning |
|---|---|
| Full color, no badge | Uncovered, collectible sticker. The destination highlights when peeling starts. |
| Dark artwork, no destination marker | Covered or otherwise blocked sticker. Frozen pieces retain the ice badge/tint and require ice-breaking taps first. |
| Solid black silhouette + large white question | Unrevealed mystery. Its texture detail remains hidden during click-fail feedback. |
| Dim artwork + metallic X chains + central padlock | Locked piece. Chains remain until the unlock presentation swaps its face. |

Check and clock badges have been removed, including the idle gallery/waiting classification, drawing routines and theme color settings. Only the destination itself is highlighted during an interactive peel. Mystery/lock visibility remains tied to the ordered reveal/unlock presentation rather than disappearing early from the final logical state. Undo and level reload rebuild the corresponding visuals.

`StickerBoardAdornment` draws lock chain links only, and is only created for locked pieces. It uses no extra image assets and has `raycastTarget=false`. The same behavior is used by the runtime and Level Preview.

Theme controls: **Board sticker states** on GameplayUITheme exposes blocked tint and the mystery question size ratio. The default question font size is 78% of the smaller placement dimension. Question marks remain upright and the outline uses the existing TMP heading material.

Regression coverage checks ordinary stickers have no destination badge graphics, waiting/gallery highlights restore correctly on cancellation, and lock chains, mystery concealment, collection and landing continue to work.
