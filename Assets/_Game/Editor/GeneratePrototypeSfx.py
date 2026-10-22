"""Original deterministic synth SFX; Python standard library only. Run from project root."""
import math
import random
import struct
import wave
from pathlib import Path

RATE = 44100
OUT = Path('Assets/_Game/Audio')
OUT.mkdir(parents=True, exist_ok=True)

def sound(name, duration, notes, noise=0):
    samples = [0.0] * int((duration + .03) * RATE)
    rng = random.Random(71)
    for offset, frequency, length, gain, sweep in notes:
        for j in range(int(length * RATE)):
            t = j / RATE
            at = int(offset * RATE) + j
            if at >= len(samples): break
            envelope = min(1, t / .006) * math.exp(-4.5 * t / length) * min(1, (length - t) / .015)
            phase = 2 * math.pi * (frequency * t + sweep * t * t / (2 * length))
            tone = math.sin(phase) + .18 * math.sin(2 * phase) + noise * rng.uniform(-1, 1)
            samples[at] += tone * envelope * gain
    peak = max(abs(x) for x in samples) or 1
    gain = min(1, .65 / peak)
    with wave.open(str(OUT / (name + '.wav')), 'wb') as wav:
        wav.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
        wav.writeframes(b''.join(struct.pack('<h', round(max(-1, min(1, x * gain)) * 32767)) for x in samples))

sound('peel', .075, [(0, 760, .07, .26, 540)], .35)
sound('land', .10, [(0, 880, .095, .40, -130)])
sound('buffer', .11, [(0, 510, .105, .35, -100)])
sound('reject', .16, [(0, 240, .15, .24, -90)])
sound('button', .055, [(0, 600, .05, .23, 160)])
sound('entrance', .17, [(0, 580, .16, .30, 350)])
sound('special', .24, [(0, 1047, .13, .31, 90), (.07, 1397, .16, .25, 0)])
sound('collection', .42, [(i * .085, f, .20, .38, 0) for i, f in enumerate([659.25, 830.61, 987.77, 1318.51])])
sound('win', .95, [(i * .105, f, .32, .36, 0) for i, f in enumerate([523.25, 659.25, 783.99, 1046.50, 1318.51, 1567.98])])
sound('fail', .38, [(i * .11, f, .17, .26, -40) for i, f in enumerate([392, 330, 261.63])])
print('Generated 10 original mono PCM WAV effects in', OUT)
