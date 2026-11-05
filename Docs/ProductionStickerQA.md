# Production sticker gameplay QA

Verified 2026-11-05 in Unity 6000.3.26f1 Editor, with the project targeting Android and Old Input / StandaloneInputModule. Final Play Mode run: **9 passed, 0 failed, 0 skipped** (68.36 seconds).

| Coverage | Result |
|---|---|
| All 64 production designs: baked alpha, transparent padding hit rejection, visible edge hit through EventSystem, partial peel, drag back, early release, restored source, full peel and exact artwork landing | PASS |
| Same category with a different required design goes to waiting; five occupied slots fail; Undo recovers | PASS |
| Covered mystery rejects selection with click-fail feedback, remains black, reveals only after its cover leaves | PASS |
| Key sheet completion unlocks X chains and restores artwork; partial peel, reattachment and collection render above a masked/sorted board | PASS |
| Saved winning traces played through the real presentation to Win for all 15 campaign levels, including filled slots, tray transitions and waiting flights | PASS |
| Landing replaces the correct silhouette with the matching texture at the configured board scale | PASS |
| Three first-time tutorial checks: cancellation, persistence/dismissal/pause, collection/reveal/unlock completion | PASS |

## Test fixes

The active Android Build Profile does not ship SampleStickerLevel. Editor Play Mode fixtures now load its asset path through EditorSceneManager.LoadSceneAsyncInPlayMode, without changing the shipping scene list. Reference tests explicitly load level 1 instead of assuming the saved current level is 1.

Tutorial assertions now mask the three lesson bits they test; additional gameplay tip completion flags remain valid. The synthetic nested-canvas fixture includes its own GraphicRaycaster and waits for its first rendered frame before testing input.

No runtime gameplay defect was reproduced by this suite. The new production interaction fixture is included in Tools > Sticker Category Game > Test Campaign Presentation (Play Mode). Every setup/cleanup backs up and restores the review save, and fixtures clone balance/content rather than edit shipping assets. The reference test also verifies that the main save is unchanged.

Results: [Unity XML](../Assets/_Game/Validation/ProductionGameplayTests.xml). Screenshot inspection covered the mystery/lock tutorials with the new artwork and a partial peel with a matching silhouette glow. Screenshots are temporary QA captures, not a partner review video.

## Limits

This validates Editor gameplay and programmatically dispatched legacy pointer events through the UI raycaster. Physical touch latency, mobile GPU rendering, memory/performance and Android lifecycle need a device build; they were not measured here. The legacy GameplaySmokeTests fixture still contains assumptions about the older ten-level content and was not part of this nine-test run.
