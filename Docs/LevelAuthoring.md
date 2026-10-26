# Per-level content and live preview

Open **Tools > Sticker Category Game > Level Preview**, select a level in the current gameplay scene, then enable **Enable Generated Content** in **Special Sticker Ratios — this level**. The same controls are available by selecting its LevelDefinition asset. Existing authored boards stay unchanged until generation is enabled.

Adjust **Mystery (?) %** and **Locked %** at the top of the inspector. **Requested (rounded)** shows the expected counts from the percentages; **Current board** shows the successfully generated counts. The preview rebuilds after a 0.5-second pause. The total percentage must not exceed 100%; geometry and key-category constraints may require lower ratios. Expand **Generation Options** for difficulty, category/type counts and the sticker pool. For example, 10% mystery and 5% locked on a 64-sticker level request 6 mystery and 3 locked stickers.

| Setting | Meaning |
|---|---|
| Generated Sticker Count | Total instances, 4–256 in multiples of four. Every album has four targets. |
| Category Count | Distinct categories drawn from the pool, in pool order. Different from Active Category Count (simultaneous cards). |
| Types Per Category | Distinct artwork definitions in each category. Repeated albums cycle through these definitions; repeated instances share artwork. |
| Sticker Pool | Explicit allowed definitions. Enabling generation initially copies the current board's definitions. Reorder/add/remove these to choose categories and art. |
| Mystery / Locked Percent | Percentage of all instances, rounded independently to nearest whole sticker. Special kinds are disjoint. The summary shows actual counts. |
| Difficulty | Easy/Normal/Hard changes packing density (1.55/1.95/2.15). This is a layout preset, not a measured solver difficulty rating. |
| Layout Seed | Reproducible special assignment, tray allocation, placement, rotation and winning witness. Change it for another arrangement. |
| Board Sticker Size Multiplier | Size on the board only; album and buffer icons are unaffected. |
| Buffer Capacity / Active Category Count | Waiting capacity / simultaneous album cards. Full buffer remains the lose rule when enabled. |
| Piles | Tray IDs, positions, sizes and angles. Stickers are allocated proportionally to tray area. |

After a pause of 0.5 seconds, the inspector stages content on a temporary level, packs it and replays a winning witness through the actual overlap/lock rules. It then updates the asset's generated placements and the scene Game View without stealing Inspector focus. The generated data is used unchanged in builds; no runtime generation or Editor dependency is required.

The first category supplies the lock key and cannot contain locked pieces. Every mystery must start covered with its question center visible. Impossible ratios, insufficient pool artwork, missing trays or failed witness/packing produce an Inspector error and retain the last valid generated board. Decrease special ratios or change the seed/geometry if packing cannot satisfy these constraints. Pool choices remain available when reducing type/category counts.

**Rebuild & Preview Now** forces an update. **Play Selected** starts the chosen level. Live rebuilding pauses during Play Mode. Edit preview uses isolated progress and does not save PlayerPrefs or automatically save the scene. Save edited assets normally with Ctrl+S.

Generation authors ordinary, mystery and locked stickers. For manually positioned content, frozen stickers or linked pairs, disable Generation and edit the placement/objective lists. Turning generation on replaces those lists with generated content.
