"""Original JinxCasino mono audio. Standard library only; no downloaded samples."""
import math
import random
import wave
import struct
import pathlib
import json
import hashlib

RATE = 24000
OUTPUT = pathlib.Path('Library/JinxCasino/Staging/Audio')
OUTPUT.mkdir(parents=True, exist_ok=True)


def write(name, samples):
    peak = max(.1, max(abs(value) for value in samples))
    pcm = b''.join(struct.pack('<h', int(max(-1, min(1, value / peak * .68)) * 32767)) for value in samples)
    path = OUTPUT / (name + '.wav')
    with wave.open(str(path), 'wb') as writer:
        writer.setnchannels(1)
        writer.setsampwidth(2)
        writer.setframerate(RATE)
        writer.writeframes(pcm)
    return {'name': name, 'seconds': len(samples) / RATE, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}


def notes(sequence, beat=.18, decay=5):
    length = int((len(sequence) * beat + .25) * RATE)
    output = [0.0] * length
    for index, frequency in enumerate(sequence):
        if not frequency:
            continue
        for sample in range(int((beat + .2) * RATE)):
            position = int(index * beat * RATE) + sample
            if position >= length:
                break
            time = sample / RATE
            attack = min(1, time / .007)
            tone = math.sin(2 * math.pi * frequency * time + .35 * math.sin(2 * math.pi * 2 * frequency * time))
            bell = .18 * math.sin(2 * math.pi * frequency * 2.76 * time)
            output[position] += (tone + bell) * attack * math.exp(-decay * time)
    return output


def glide(start, end, duration, noise=0):
    generator = random.Random(2718)
    samples = []
    phase = 0
    for index in range(int(duration * RATE)):
        time = index / RATE
        progress = time / duration
        frequency = start + (end - start) * (1 - (1 - progress) ** 2)
        phase += 2 * math.pi * frequency / RATE
        envelope = min(1, time / .005) * math.sin(math.pi * progress) ** .6
        samples.append(envelope * (math.sin(phase + .7 * math.sin(phase / 2)) + noise * generator.uniform(-1, 1)))
    return samples


clips = []
for name, sequence, beat in [
    ('UiClick', [880], .045), ('MachineBegin', [196, 247, 294, 370], .085),
    ('Win', [659, 784, 740, 988, 1318], .15), ('Lose', [294, 277, 220, 147], .16),
    ('Coin', [1568, 2349], .075), ('TaskComplete', [440, 554, 659, 880], .11),
    ('Event', [370, 0, 370, 554], .14),
    ('EndingDignity', [330, 440, 494, 659, 554, 494, 440, 0, 370, 494, 659, 740, 659, 554, 440, 330], .36),
    ('EndingTakeover', [247, 370, 494, 554, 659, 554, 494, 370, 294, 440, 587, 659, 740, 659, 587, 494], .28),
    ('EndingWithdraw', [330, 311, 294, 277, 0, 220, 196, 185, 165, 147, 0, 196, 147, 110], .32)
]:
    clips.append(write(name, notes(sequence, beat)))
clips.append(write('Horn', glide(185, 247, .65)))
clips.append(write('Boing', glide(110, 770, .5)))
clips.append(write('Charge', glide(90, 1600, .45, .1)))
melody = [247, 0, 370, 440, 494, 440, 370, 0, 294, 0, 370, 494, 554, 494, 370, 0,
          330, 0, 440, 554, 659, 554, 440, 0, 294, 247, 220, 185, 247, 0, 0, 0]
clips.append(write('ClubLoop', notes(melody * 2, .30, 7)))
(OUTPUT / 'audio-manifest.json').write_text(json.dumps(clips, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'clips': len(clips), 'rate': RATE, 'output': str(OUTPUT)}, ensure_ascii=False))
