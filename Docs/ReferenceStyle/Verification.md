# Reference style verification — 2026-10-24

## Smaller board / full-buffer loss — 2026-10-25

Four sample boards have their serialized sticker scale reduced from 1 to 0.75. The alpha hitbox and initial flight size use the same scale; target/buffer sizes are unchanged. The authoring generator retains the per-level multiplier. Any Mystery cover affected by shrinking is rechecked for partial coverage and a readable question center.

The loss threshold is now `Buffer.Count >= Capacity`, after Resolve, with input stopped immediately on logical Fail. Presentation still completes the last sticker's peel/flight/stick, shows the occupied last slot and holds 0.24 seconds before TRAY FULL. No overflow slot is required. Undo returns to capacity minus one; Retry resets the board. All 14 authored levels enable the rule. All 14 winning traces were replayed; only main Level03 needed a replacement solver trace, and no main-board transforms changed. Backups are in UserSettings/StickyPeerSortBeforeFullTrayLose.

Model verification: **55 EditMode passed** (`79d520f231fd4aa3b5f0954a0acd83e6`). Main gameplay verification: **20 PlayMode passed** (`27ea499a3da64e38ba488039bf797a5f`), including all ten actual level wins, last-slot landing before the lose modal, no fifth buffered sticker after rapid taps, Undo to 3/4, Retry and no fail reward. The initial combined PlayMode run was interrupted after 20 cases by a Test Framework PlayModeRunTask reload exception, so the fixtures were rerun separately.

Reference gameplay verification: **2 PlayMode passed** (`98bc3103542743d484255fc456f2fd68`), including wins through all four 32/40/48/64-piece levels after the 25% scale reduction and full-buffer rule, plus landing into the original-size album slot. Both fixtures completed with no game Console errors; total current checks are 55 EditMode and 22 PlayMode across the two fixture runs.

## Dense randomized content — 2026-10-25

Supersedes the earlier 16-piece/grid layouts below. Four sample assets now contain 32/40/48/64 pieces and 8/10/12/16 exact-target albums, using the existing 16 artwork definitions. Each tray mixes categories; random angles, size variation and alpha-based coverage scoring replace category rows and the fixed lattice. Seeds and requested counts are serialized per level. Main-scene authored content is retained.

**55 EditMode passed** (`e81c83a181db460880a8ca522ab8dd3a`), including all saved traces, per-tray category diversity, real overlap, sampled visible-alpha containment, minimum well coverage and the visible covered Mystery center. Containment now checks the actual artwork rather than transparent PNG rectangles; padded rectangles may extend into the rim while their visible pixels stay inside.

**3 focused PlayMode tests passed** (`e5e696797a874b609c0b4581adc6a6c6`): replay to Win through the actual controller/animation for all four expanded levels, exact silhouette-to-artwork landing and immediate covered-veil refresh/Undo. The authoring pass also replays a separate flat-model plan through actual packed geometry before any asset is saved. Linked partners and frozen multi-taps are handled in that plan. No screenshot background is used as board content.

Measured initial well coverage (44×44 alpha samples, inset 24 units): Grid4 79–84%, Rows2 87%, Mixed3 86–89%, Pile1 96%. Inspected live Play Mode captures at 1080×1920. Largest pile reports 64 pieces; one diagnostic Editor frame reported 104 batches / 3,696 triangles. No Android profiling claim.

Tray spread follow-up: **55 EditMode passed**, job `ebcf9838ec424466b97e83f2c7d0efc6`. Includes a per-tray spread/overlap check, full rotated-rectangle containment, visible covered Mystery center and replay of every saved solution. All four layouts were staged and solved before commit; originals were backed up in UserSettings. Both themes use 0.68 covered darkness. Main-scene authored layouts were retained.

Focused PlayMode verification after the spread: **3 passed**, job `857da12a226849b4b386ab4ee1e48b29`: actual wins through all four sample traces, silhouette/landing consistency and immediate covered-veil refresh/Undo. No new Console errors. Left Rows2 selected in the Level Preview window with its updated authoring preview saved.

Unity 6000.3.26f1, connected through Unity MCP. The six supplied references were reviewed before the implementation.

- Created and opened `Assets/_Game/Scenes/SampleStickerLevel.unity` in the live Editor. Entered Play Mode and inspected all four level arrangements at 1080×1920.
- Four independent LevelDefinition assets passed LevelValidator and solver replay without boosters. All 16 stickers stay inside their assigned tray including rotated bounds; the Mystery's question center remains visible while the sticker is covered.
- EditMode: **52 passed, 0 failed**, job `a017bbcdced7476b9abea92010067262`. Includes the four new reference-style tests, exact tray counts, independent data, geometry, solved traces and isolated progress storage.
- PlayMode: **21 passed, 0 failed**, job `4b7353178c0941b5bdf87f4b08bc68b2`. Includes full controller/presentation replay for all four sample levels, every original level, silhouette-to-artwork landing, key/lock/Mystery sequencing, overflow/Undo/Retry, rapid input and legacy mouse input.
- Existing flat-layout smoke fixtures were updated to derive spacing from placement size and to explicitly set their two open categories. Their overflow paths now assert that all five chosen stickers have no open target. This corrects obsolete assumptions after the existing project adopted larger artwork and a FOOD repeat in Level 5; production level data was not changed.
- Main gameplay scene, main theme and original level assets were not regenerated or edited. The 16 modified original sticker definition assets present before this task were retained.
- Original Play Store images remain in ignored Temp. Scene/prefab dependencies point to project artwork, Layer Lab assets or generated original wood/silhouettes, never reference screenshots.

No APK, physical-device safe-area check or Android GPU profile was performed. Editor batch/triangle observations are diagnostic, not mobile performance claims.

Portrait Game View checks: **1080×1920, 1080×2400, 1170×2532 and 1080×1440**. After Canvas safe-area fitting, all four corners of LevelHeader, albums, buffer, board, booster dock and restart were inside Screen.safeArea in every resolution (zero out-of-bounds results). Saved tall/compact screenshots alongside the four level shots. Restored the original Game View selection afterward. This tests responsive fitting in the Editor; it does not emulate physical notches.

Scene validation through MCP reported **0 issues / 0 missing scripts / 0 broken prefabs**. No new C# compiler errors. The sample scene is left open in Edit Mode for Level Preview / Play Selected.

2026-10-25 — Per-level generation: 61/61 EditMode tests passed (job 5ece2827a3e74287864eb26bf712d9ea). Checked all four tray layouts at 10% mystery + 10% locked, exact type/category counts, deterministic seeds, difficulty density, witness wins, visible mystery centers and invalid-settings preservation. Inspector update callback rendered a temporary 36-sticker / 6-artwork / 3-category level as 36 StickerViews without changing demo PlayerPrefs; original levels and preview restored.

2026-10-26 — Level analysis: 68/68 EditMode checks passed (ba3b39e65e204d99b707c9611db1ec35). All 14 current authored traces verified. Main Level03 path peak resolved buffer is 3/4. Actual Game View replay of Level62 reached Win with 0 board stickers, emitted unlock and reveal, and seeking to step10 restored 54/54 expected views. Batch scheduler checked all14 plus a deliberate missing entry (14Verified, 1Invalid), continuing after the invalid entry. Demo PlayerPrefs unchanged.
