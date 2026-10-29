# Campaign verification — 2026-10-30

Unity 6000.3.26f1.

- Edit Mode: **29 passed, 0 failed, 0 skipped** across CampaignBalanceTests, LevelGenerationTests and LevelAnalysisTests. Includes legal winning witnesses for all 15 campaign assets, safe buffer pressure, visible mystery symbols, deterministic generation and retention of the last valid layout for invalid settings.
- Play Mode: **2 passed, 0 failed, 0 skipped** in ReferenceStylePlayTests. AllCampaignTracesWinThroughRealPresentation plays all **15** scene-assigned levels through real collection, sheet transitions, unlocks, reveals and win UI, without boosters. The second test verifies the matching artwork replaces its silhouette and board scale remains 0.75. The fixture restores saved sample progress and checks the main save remains unchanged.
- Both gameplay scenes reference the same 15 campaign assets in order. Full-buffer loss is enabled, with capacity 5 throughout.

Generation metrics and deterministic 64-run bot diagnostics are in [CampaignBalance.md](CampaignBalance.md). Bot outcomes do not estimate human completion rates; human playtesting is still needed to refine pacing.
