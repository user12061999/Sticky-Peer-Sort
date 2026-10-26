# Level analysis and solution preview

Open **Tools > Sticker Category Game > Level Preview** in Edit Mode. Select a scene level. **Level Analysis & Solution** appears above its authoring controls.

The panel initially validates the actual generated/manual board and replays its saved trace. **Analyze & Find Winning Path** also runs the existing solver if that trace is missing or stale. This is read-only: it does not rebuild layouts, write solutionTrace, award rewards or save progress. Authoring changes invalidate the report and stop playback.

## Metrics

- Initially clickable: legal actions using the same coverage and special rules as gameplay. A frozen sticker can be clicked to damage ice even when it cannot yet be collected.
- Initially covered: pieces with any higher opaque overlap. Locked but uncovered pieces are not included in this count.
- Maximum overlap chain: longest directed cover chain, with the same sorting-order/index tie-break as input. This is not a count at one shared pixel.
- Locked and mystery totals: authored instance counts.
- Peak resolved buffer: maximum waiting occupancy **after** all automatic transfers in the verified winning trace. The first step attaining that peak is shown. Full-buffer failure is evaluated at this resolved state.
- Transient buffer peak: also includes a Buffered event's temporary slot before automatic transfers.

Buffer peaks describe **one winning path**, not a minimum or a universal difficulty score. No automatic Easy/Normal/Hard reassignment is made from these measurements.

## Verification results

| Result | Meaning |
|---|---|
| Invalid | Missing asset, malformed board, invalid definition/dependency or construction failure. |
| Unverified | No saved trace successfully completed a replay; no solver search requested. |
| Verified | Every recorded action was legal and the final state was Win. |
| NoSolution | Solver exhausted reachable states without finding a win. |
| BudgetExceeded | Search hit its node budget; solvability is still unknown. |

## Replay

Click **Load Solution Preview**. The Game View displays an isolated level snapshot; a gold tint marks the next sticker. Use **Play Solution / Pause**, **Next**, **Previous**, **Reset**, or the **Action** slider. Playback advances logical actions at the configured interval; it does not run the runtime peel/flight animation in Edit Mode.

The event details show each pick, buffer placement, exact album slot, completed/activated album, mystery reveal, ice break and unlock. Previous and seek restore full snapshots, so repeated ice taps and automatic buffer transfers remain correct. Switching level, refreshing the preview, changing authored data, closing the window or entering Play Mode stops automatic playback. Runtime **Play Selected** remains separate.

## Batch check

**Check All Scene Levels** checks the current GameController's ordered level list, including missing entries. Enable **Search if trace is invalid** to permit bounded search; otherwise it validates saved traces only. The same node budget applies per level. One level is processed per Editor update, allowing cancellation between levels.

Results show verification and resolved buffer peak. Click a row to select/ping the asset. **CHANGED — recheck** indicates its serialized level data changed after that result; rerun the batch. Missing/invalid levels do not prevent other entries from being checked. Cancel preserves completed results and leaves remaining levels unchecked.
