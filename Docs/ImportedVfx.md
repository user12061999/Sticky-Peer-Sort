# Imported VFX and event audio

Selected assets copied from `D:/Project_Unity/VFX/Assets/Epic Toon FX` into `Assets/_Game/ThirdParty/EpicToonFX`. Only three particle prefabs, their seven textures/materials and selected event sounds are included. Original GUIDs are retained. Imported material copies use the project Canvas shader; missing URP conversion sub-assets were removed. The source project is unchanged.

| Event | Visual | Audio |
|---|---|---|
| Sticker lands in a sheet / waiting slot | TinySparkle + existing landing pop | etfx_target_hit |
| Sheet completes (after filled hold) | StarPoof | etfx_explosion_sparkle2 |
| New sheet enters | Existing card entrance | etfx_spawn |
| Mystery reveal / unlocked sticker flips open | TinySparkle at artwork swap | etfx_explosion_minimagic |
| Win | StarExplosion + existing reward coins | etfx_explosion_sparkle3 |

Peel, rejection, button, ice break, lose and music retain their existing clips. There is no success sparkle on lose. The VFX adapter never changes collection, buffer or input rules.

## Tuning

- `Assets/_Game/Resources/StickerVfx.asset`: landing/collection/reveal/win prefab references and UI scales, maximum concurrent effects (8) and maximum lifetime (4 seconds).
- `Assets/_Game/Data/Audio.asset`: event clips, master volume and per-event volume multipliers. Existing sound toggle mutes all imported event sounds.
- `GameBalanceConfig.celebrations`: existing switch controls event visuals.

`ImportedVfxPlayer` owns simulation instances and UI graphics. `UIParticleGraphic` bakes native particle meshes onto the overlay Canvas with source texture UVs/colors. Renderers are disabled, simulation uses unscaled time, and graphics never receive raycasts. The bake camera is disabled with culling mask zero and performs no scene rendering. Effects expire independently of gameplay, and reset/disable clears both simulation and graphics. No additional package is required.

## Verification

Use **Tools > Sticker Sort > Validate Imported VFX** for asset/material/shader/audio and particle geometry checks in an isolated preview scene. **Preview Imported VFX (Play Mode)** demonstrates the effects without modifying the session and checks live Canvas geometry, expiry, concurrency and cleanup. Reports and a preview image are saved under `Temp/`.

Validated in Unity 6000.3.26f1: all three imported prefabs baked successfully (TinySparkle 12, StarPoof 208, StarExplosion 84 vertices at the sampled time). The live preview produced 208 Canvas vertices and passed automatic expiry, concurrency cap, clear/reset and unchanged-session checks. The captured StarPoof preview was visually inspected.
