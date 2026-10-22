# Prototype sound effects

These 10 short mono PCM WAV clips are original synthesized effects generated for this project by `Editor/GeneratePrototypeSfx.py` (Python standard library, deterministic seed). They contain no recordings, external samples or third-party music. Regenerate from the project root with `python Assets/_Game/Editor/GeneratePrototypeSfx.py`.

`Data/Audio.asset` maps peel, land, waiting-tray land, invalid tap, collection complete, card entrance, special action, win, failure and UI button sounds. Replace any clip and adjust master SFX volume in that asset. No background music is included.

The popup **SOUND: ON/OFF** button persists `StickyPeerSort.SoundMuted` separately from puzzle progress. AudioSource voices are bounded, non-spatial, and landing feedback is emitted after the target image is committed. Result sounds have a dedicated voice and stop overlapping short effects. Muting stops current voices immediately.
