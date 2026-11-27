#!/usr/bin/env python3
"""Render the original Veilward sanctuary score and cue designs as PCM16 WAV."""
from array import array
from math import exp, pi, sin, tanh
from pathlib import Path
from random import Random
import sys
import wave

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/_Game/Audio/Clips"
RATE = 48000
TAU = 2 * pi
SEED = 490607


def hz(note):
    return 440 * 2 ** ((note - 69) / 12)


def layer(length, pitch, fall, shape, seed=SEED):
    """Each voice has a separate excitation, envelope and resonator bank."""
    noise = Random(seed)
    brown = soft = phase = 0.0
    partials = {
        "bell": ((1, 0.70, 1), (2.01, 0.26, 1.8), (3.02, 0.13, 2.5), (4.19, 0.07, 4)),
        "wood": ((1, 0.72, 1), (1.59, 0.36, 2.8), (2.64, 0.16, 3.9)),
        "string": ((1, 0.7, 1), (2.002, 0.23, 1.65), (2.999, 0.1, 2.7), (4.008, 0.05, 5)),
        "drum": ((1, .85, 1), (1.46, .23, 2.3), (2.14, .09, 4)),
        "water": ((1, .8, 1), (1.995, .18, 2), (2.97, .09, 3)),
        "storm": ((1, .69, 1), (1.504, .32, 1.8), (2.541, .14, 3)),
    }.get(shape, ())
    noise_gain, lowpass = {
        "wind": (2.7, .035), "steam": (1.9, .12), "ember": (1.65, .18),
        "water": (.23, .42), "wood": (.64, .10), "drum": (.43, .065),
        "storm": (.96, .16), "string": (.1, .13), "bell": (.04, .12),
    }[shape]
    count = round(length * RATE)
    for i in range(count):
        t = i / RATE
        sample = noise.uniform(-1, 1)
        brown += lowpass * (sample - brown)
        soft += .013 * (brown - soft)
        phase += TAU / RATE * (pitch + fall * exp(-t * (17 if shape != "wind" else 6)))
        attack = .045 if shape in ("wind", "steam") else .007
        edge = min(1, t / attack, (count - 1 - i) / (RATE * .035))
        if shape == "wind":
            edge *= sin(pi * i / count) ** 1.3
        if shape == "ember":
            edge *= .58 + .42 * abs(sin(t * 117) * sin(t * 227))
        ring = sum(gain * exp(-t * decay * 4 / length) * sin(ratio * phase)
                   for ratio, gain, decay in partials)
        air = noise_gain * (brown - soft) * exp(-t * 3.25 / length)
        yield edge * (ring + air)


def write(path, channels, peak):
    highest = max(abs(value) for channel in channels for value in channel)
    pcm = array("h", (round(tanh(value / highest * 0.76) / tanh(.76) * 32767 * peak)
                      for frame in zip(*channels) for value in frame))
    if sys.byteorder == "big":
        pcm.byteswap()
    with wave.open(str(path), "wb") as stream:
        stream.setparams((len(channels), 2, RATE, 0, "NONE", "not compressed"))
        stream.writeframes(pcm.tobytes())


def cue(name, notes, seed=SEED):
    duration = max(at + length for at, length, *_ in notes)
    signal = array("f", [0]) * (round(duration * RATE) + 1)
    for index, (at, length, pitch, fall, shape, gain) in enumerate(notes):
        offset = round(at * RATE)
        for i, sample in enumerate(layer(length, pitch, fall, shape, seed + index * 739)):
            signal[offset + i] += sample * gain
    write(OUT / (name + ".wav"), (signal,), .60)


def score(name, combat):
    beat = .64 if combat else .82
    cycle = round(beat * 32 * RATE)
    left = array("f", [0]) * cycle
    right = array("f", [0]) * cycle

    def add(at, note, length, shape, spread, gain):
        offset = round(at * beat * RATE)
        for i, value in enumerate(layer(length, hz(note), -5 if shape == "string" else 0, shape, SEED + note)):
            index = (offset + i) % cycle
            left[index] += value * gain * (1 - spread)
            right[index] += value * gain * (1 + spread)
            echo = (index + 5757) % cycle
            left[echo] += value * gain * (.12 + spread / 3)
            right[echo] += value * gain * (.12 - spread / 3)

    # D minor pentatonic motif over open fifths and an unresolved turnaround.
    roots = (50, 50, 53, 48, 55, 53, 48, 50)
    motif = ((.5, 62), (2, 65), (3.5, 69), (5.5, 67), (7, 65), (9, 62), (10.5, 60),
             (12, 65), (14.5, 62), (16.5, 69), (18, 72), (19, 74), (21, 69), (22.5, 67),
             (25, 65), (27, 60), (29, 62), (31, 69))
    for bar, root in enumerate(roots):
        add(bar * 4, root - 12, beat * 5, "string", -.14, .14 if combat else .22)
        for index, interval in enumerate((0, 7, 12, 7)):
            at = bar * 4 + (0, 1.5, 2.5, 3.25)[index]
            add(at, root + interval, beat * 2.4, "string", (.22 if index % 2 else -.22), .12)
        if combat:
            for index, when in enumerate((0, 1.5, 2.75)):
                add(bar * 4 + when, 32 + index * 3, .38, "drum", -.06, .36 if index == 0 else .14)
            for when in (.75, 2, 3.5):
                add(bar * 4 + when, 71 + (bar % 3) * 2, .15, "wood", .28, .095)
    for index, (at, note) in enumerate(motif):
        add(at, note, beat * (2.15 if combat else 3.8), "bell", (.24 if index % 2 else -.24), .23)
        if index % 4 == 2:
            add(at + .5, note - 12, beat * 3, "string", .18, .13)
    write(OUT / (name + ".wav"), (left, right), .60)


def chord(shape, pitches, gap=.08, length=.40):
    return [(i * gap, length, pitch, 0, shape, .65 ** (i * .5)) for i, pitch in enumerate(pitches)]


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    score("MenuMusic", False)
    score("BattleMusic", True)
    attacks = {
        "Flame": ([(0,.24,0,0,"ember",.86),(.016,.15,168,40,"wood",.43)],
                  [(0,.19,0,0,"ember",.68),(0,.16,113,92,"drum",.8)]),
        "Tide": ([(0,.26,367,316,"water",.9),(.065,.14,488,168,"water",.42)],
                 [(0,.21,228,285,"water",.8),(.04,.15,319,185,"water",.53)]),
        "Gale": ([(0,.29,0,0,"wind",1),(.07,.13,390,-115,"string",.32)],
                 [(0,.26,0,0,"wind",1),(.018,.18,306,56,"wood",.31)]),
        "Stone": ([(0,.18,128,91,"wood",.86),(.053,.145,152,21,"wood",.38)],
                  [(0,.27,76,76,"drum",.98),(.029,.115,173,14,"wood",.42)]),
        "Steam": ([(0,.28,0,0,"steam",.92),(.035,.18,211,153,"water",.35)],
                  [(0,.25,0,0,"steam",.81),(.013,.23,117,30,"wood",.65)]),
        "Storm": ([(0,.12,442,82,"storm",.82),(.057,.10,343,61,"storm",.63)],
                  [(0,.20,145,229,"storm",.8),(.044,.11,414,117,"storm",.28)]),
    }
    for name, pair in attacks.items():
        for suffix, notes in zip(("Shot", "Impact"), pair):
            for variation in (1, 2):
                adjusted = [(at, length, pitch * (1.025 if variation == 2 else 1), fall, shape, gain)
                            for at, length, pitch, fall, shape, gain in notes]
                cue(f"{name}{suffix}{variation}", adjusted, SEED + variation * 17)
    cues = {
        "SteamBurst": [(0,.41,0,0,"steam",1),(.007,.21,133,70,"drum",.67),(.10,.14,302,163,"water",.33)],
        "StormChain": [(0,.18,331,79,"storm",1),(.074,.19,441,-30,"storm",.57)],
        "StormHop": [(0,.16,626,-62,"storm",.85),(.026,.1,442,64,"wood",.36)],
        "SealTarget": chord("bell", (329, 493), .044, .29),
        "SealCast": [(0,.49,97,70,"drum",.6), *chord("bell", (293, 440, 587), .06, .68)],
        "SealHit": [(0,.31,144,53,"drum",.79),(.011,.25,586,3,"bell",.37)],
        "BossSpawn": [(0,.84,63,23,"drum",.72),(.22,.92,97,64,"storm",.53),(.56,.67,146,6,"bell",.35)],
        "BossThreat": chord("wood", (118, 124, 152), .15, .41),
        "BossDeath": [(0,.71,66,70,"drum",.79), *chord("bell", (369, 293, 196), .16, .60)],
        "BossLeak": chord("storm", (196, 183, 146), .18, .88),
        "WaveStart": chord("drum", (117, 174), .14, .39),
        "WaveEnd": chord("bell", (293, 440), .15, .70),
        "Victory": chord("bell", (293, 369, 440, 587, 740), .21, 1.36),
        "Defeat": chord("string", (349, 293, 220, 146), .29, 1.32),
        "Select": chord("wood", (445,), .05, .15),
        "Confirm": chord("bell", (392, 587), .06, .26),
        "Cancel": chord("wood", (382, 254), .06, .20),
        "Reject": chord("wood", (241, 210), .045, .135),
        "Build": [(0,.21,113,53,"wood",.92),(.052,.31,440,0,"bell",.41)],
        "Upgrade": chord("bell", (330, 440, 660), .07, .53),
        "Sell": chord("wood", (468, 323, 220), .06, .31),
        "Unlock": chord("bell", (294, 440, 587, 659), .11, .72),
        "SaveError": chord("wood", (261, 207), .13, .38),
        "SaveRecovered": chord("bell", (330, 440), .12, .41),
    }
    for name, notes in cues.items():
        cue(name, notes)


if __name__ == "__main__":
    main()
