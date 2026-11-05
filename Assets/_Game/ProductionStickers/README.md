# Individual production stickers

64 new designs: 16 FOOD, 16 ANIMALS, 16 PLANTS, 16 VEHICLES.
Each sticker was generated with a separate built-in image_gen request from scratch. The supplied sample sheet is a style/subject reference only; it was not sliced. Source hashes and per-subject prompts are recorded in manifest.json together with the shared prompt template.

Final artwork: Art/sps_<category>_<design>.png, 512 x 512 RGBA, transparent background, centered silhouette with at least 32px padding, white die-cut border, no outer cast shadow. Fully transparent pixels have zero RGB. All 64 PNGs have unique content hashes. The approximately 12px white border was requested during generation; its thickness varies locally with the generated contour.

Data contains the matching StickerDefinition assets. Silhouettes contains matching gallery targets. All new definitions have baked 256px alpha masks, no text labels, white tint, and aspect-preserving artwork. Unity imports use Single Sprite, Full Rect, clamp, bilinear, no mipmaps, uncompressed 512, non-readable runtime textures.

The 15 active campaign levels rotate four artwork banks so all 64 new designs appear across the chapter. Counts, difficulty settings, special ratios, buffer capacity and the full-buffer loss rule are retained. Layouts and winning routes were rebuilt against the new artwork alpha. Integration.md records all 15 verified routes. Existing art remains available in each level's pool.

Existing ReferenceStyle definition copies now have ref_ IDs and filenames, while their GUIDs are retained. The original definitions retain their IDs. Validation/StickerCatalog.csv lists all 96 existing and new definitions with unique IDs and definition filenames.

Menu: Tools > Sticker Category Game > Production Stickers > Import and Apply 64 Individual Stickers. The campaign builder also retains this artwork pool on future builds. Validate Unique Catalog regenerates the CSV and checks for collisions.

Validation/GameplaySize_96.png and GameplaySize_160.png are composite review proofs made from the final independent PNGs, not source sheets. All 64 were visually reviewed on the warm gameplay background for recognition, white edges, clipping and shadows. ProductionStickerImport.txt in Temp records successful import/route verification. Unity compilation has no errors. The follow-up Play Mode QA run on 2026-11-05 passed all 9 checks, including all 64 production designs and all 15 campaign winning traces. See Docs/ProductionStickerQA.md and Assets/_Game/Validation/ProductionGameplayTests.xml. Android device testing remains outstanding.
