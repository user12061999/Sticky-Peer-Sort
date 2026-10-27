# Interactive sticker peeling and sheet priority

## Picking a destination

The sticker must match a free exact design target on an active sheet. Among all eligible sheets, `GameSession` prefers the sheet with the **largest filled target count**. Equal counts keep objective list order. The first matching free slot inside the chosen sheet wins. A fuller sheet cannot accept an unmatched design or an already occupied target.

The same selector drives direct collection, transfers from the waiting area, destination preview and the solver. Filled counts include logically committed moves whose flight may still be landing, so rapid moves reserve destinations consistently. Legacy objectives with empty exactTargets retain their category-only fallback.

## Old Input drag interaction

With **Drag To Peel** enabled on GameBalanceConfig (default):

1. Press an exposed, legal sticker. Its destination highlights, and an isolated visual ghost appears. The logical sticker remains on the board.
2. Drag in any direction. The first movement outside the dead zone establishes the peel direction. Progress is the projected distance along that direction, scaled to the sticker's visible size.
3. Drag back towards the starting point to reverse the peel. Perpendicular movement does not silently change direction or increase progress.
4. Release before the complete peel to animate back to the board. No target, waiting slot, undo step, reward, mystery reveal or cover is committed by this cancellation.
5. Reach the full distance to commit exactly once. The already folded ghost hands off to the existing flight and reverse-peel landing. It goes to the chosen sheet, or to the waiting area if there is no matching active target. The flight does not replay the initial peel.

Quick press/release on an ordinary sticker no longer collects it. Frozen pieces still use taps to crack ice; once the ice is gone, they can be peeled. Covered/locked pieces retain their click-fail feedback. Drag controls use the existing StandaloneInputModule and pointer IDs for both mouse and touch; no New Input System dependency is introduced.

Only one uncommitted peel owns input at a time. Other pointer taps and boosters cannot alter the board during it. Level reload, cancel, loss of application focus or pause restores/cancels the uncommitted view. Ordinary previously committed flights may continue while another legal sticker is being peeled. Programmatic `GameController.Tap` remains available for solver/test playback when no gesture holds input.

## Configuration

**Interactive drag peel (Old Input EventSystem)** on the main/reference GameBalanceConfig:

- **Drag To Peel**: default true; false restores the previous tap-to-collect behavior.
- **Drag Peel Distance Ratio**: default 0.85 of the sticker's smaller fitted dimension, including board scale.
- **Drag Peel Dead Zone**: default 0.05 of that distance.
- **Drag Reattach Duration**: default 0.18 seconds for an early release to flatten again.

`PeelDragProgress` contains direction/progress math; the gameplay coordinator owns reservation and commit; `StickerDragPeel` owns pointer handling and the reversible ghost; `StickerCurlMesh` handles the directional geometry. The live source hit geometry is unchanged until commitment. No review video or touch-indicator recording is part of this update.

Validation: 72/72 EditMode tests passed (fe44de83857346859f6646b0e7ff3362), 26/26 GameplaySmokeTests passed (9ee03e8bdf504ab7b8e7d944987d9dc6), and 2/2 ReferenceStylePlayTests passed (de7890195c32412c86ddaee1f8f46500). Actual Level19 visual inspection at 50% peel confirmed the target highlight and unchanged session key; releasing restored alpha=1, no effects and identical logical state.
