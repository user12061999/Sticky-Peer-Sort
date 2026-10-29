# First-time tutorial guides

The game creates temporary coaching at runtime; the removed Instruction/Buffer Status HUD stays absent. All text uses TextMeshPro and the existing theme fonts, outlines and underlays. The animated hand comes from the existing Layer Lab tutorial-hand sprites.

- **Level 1:** an available sticker and its exact sheet destination get silhouette outlines. The hand demonstrates dragging to peel. The card explains exact-picture matching and releasing early to reattach. The first completed collection finishes the lesson; partial peeling/cancellation does not.
- **Mystery:** shown the first time an unrevealed mystery exists on the board (campaign level 3). The ? silhouette is outlined and the hand points to an actually selectable cover, walking upward through overlap layers as needed.
- **Lock:** shown the first time an active lock exists (campaign level 5). The chained sticker is outlined while the hand points to the matching key sheet. The card explains completing that sheet to unlock. Sticker-removal dependencies get an appropriate alternate explanation.

The card has Skip/Got it. Dismissal records only tutorial completion; it never reveals, unlocks, collects or spends a booster. Regular gameplay remains available. Coaching hides during drag/reattachment, animations, pause and result modals; loading/restarting/shuffling rebinds it to the live board. It disappears when the relevant collection/reveal/unlock completes.

`GameBalanceConfig.tutorialsEnabled` enables/disables coaching. `GameplayUITheme.tutorialHand` chooses the pointer artwork. Completion flags are stored in `SaveData.completedTutorials` in the same save slot as the game (Peel=1, Mystery=2, Lock=4). Existing version-1 saves decode with uncompleted flags and retain coins, boosters and level progress. Completed lessons do not repeat across restarts.

`TutorialGuidePolicy` selects lessons and valid cover targets without changing the puzzle. `GameTutorialGuide` owns the temporary Canvas UI and consumes presentation-finished events. `SaveService` persists only completed/dismissed lessons.

Use **Tools > Sticker Category Game > Test Tutorial Guides (Play Mode)** for partial-peel cancellation, persistence, first appearances, pause/resume, successful completion and disable checks. Tests restore the previous sample save. The three tutorial screenshots are captured under `Temp/TutorialPeel.png`, `Temp/TutorialMystery.png`, and `Temp/TutorialLock.png`.

Validation: the combined presentation/tutorial fixture passed 5/5 tests, including all 15 campaign traces. After refining caption placement, the three dedicated tutorial tests passed again (3/3). All three captured tutorial screens were visually reviewed; the coached artwork, cover and key-sheet targets remain visible.
