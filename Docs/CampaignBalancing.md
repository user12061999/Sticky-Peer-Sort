# Balanced opening campaign

Both gameplay scenes now reference `Assets/_Game/ReferenceStyle/Data/Campaign/Campaign_01.asset` through `Campaign_15.asset`. Existing prototype and reference samples remain available as layout templates. Saved progress and booster inventory are not reset.

- Levels 1–2 teach sorting by category and changing sheets, with no special stickers.
- Level 3 introduces one covered mystery; level 5 introduces locks separately.
- Levels 6–9 combine the mechanics and introduce future-sheet parking.
- Levels 10, 12, 13 and 15 provide planning challenges. Levels 8, 11 and 14 provide relief with three open sheets.
- Counts progress from 12 to 48 stickers. **Each design appears once per level and each category has one sheet**, so every sticker of a category has a slot, shown as its silhouette, on its sheet. The 64 designs form 16 themed categories of four (`ProductionCategories.cs`, assets under `ProductionStickers/Categories`). Five waiting slots are available throughout; filling all of them still loses. Mystery reaches 6/48 (12.5%) and locks 5/48 (about 10.4%).

The board size multiplier scales with sticker count: 1.1 up to 16 stickers, 0.95 up to 24, 0.85 up to 32, then 0.75. Trays alternate among two wide wells, four compartments, three mixed wells and a single large well. Layouts remain irregular and overlapping.

## Authoring and verification

Open **Tools > Sticker Category Game > Level Preview** to select any campaign level, edit ratios and preview changes. Generation Options includes **Planned Buffer Peak**, which authors a route parking 0–3 future-sheet pieces after the opening moves. This is the witnessed route's pressure, not proof that every possible solution requires that many slots. The same generator drives Inspector rebuilds and saved content.

**Build Balanced Campaign (15 Levels)** recreates the chapter from fixed recipes. Every candidate board is generated on a temporary copy, validated and replayed to Win before campaign assets are updated. A deterministic target-first bot screens large difficulty spikes; random play is an additional diagnostic. These bot results are not player completion-rate predictions. The final results are in `Assets/_Game/Validation/CampaignBalance.md`; replay traces are in `CampaignSolutions.txt`.

Use **Test Campaign Balance** for data/generator/analysis regression tests and **Test Campaign Presentation (Play Mode)** to replay all scene-assigned campaign levels through the real animations. Progress is restored by the presentation test fixture. Playtest the chapter with people next to tune pacing and recognition difficulty.
