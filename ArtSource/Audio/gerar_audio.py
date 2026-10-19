# Travessia nas Alturas - sintese das trilhas e efeitos sonoros (audio proprio).
# Uso: blender --background --python gerar_audio.py   (ou qualquer Python 3 com numpy)

import os
import wave
import numpy as np

OUT = r"C:\Users\rodri\TrabalhoKinho\Assets\Audio"
os.makedirs(OUT, exist_ok=True)
SR = 44100


def midi(n):
    return 440.0 * 2 ** ((n - 69) / 12)


NOTE = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6,
        "G": 7, "G#": 8, "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}


def n(name):
    """'C5' -> numero MIDI; 'r' -> pausa."""
    if name == "r":
        return None
    pitch, octave = name[:-1], int(name[-1])
    return 12 * (octave + 1) + NOTE[pitch]


def osc(kind, freq, length, duty=0.5):
    t = np.arange(int(length * SR)) / SR
    ph = (t * freq) % 1.0
    if kind == "square":
        return np.where(ph < duty, 1.0, -1.0)
    if kind == "tri":
        return 4 * np.abs(ph - 0.5) - 1
    if kind == "sine":
        return np.sin(2 * np.pi * freq * t)
    if kind == "saw":
        return 2 * ph - 1
    raise ValueError(kind)


def env(length, a=0.005, d=0.08, s=0.6, r=0.05):
    count = int(length * SR)
    e = np.ones(count) * s
    ai, di, ri = int(a * SR), int(d * SR), int(r * SR)
    ai = min(ai, count)
    e[:ai] = np.linspace(0, 1, ai)
    end_d = min(ai + di, count)
    e[ai:end_d] = np.linspace(1, s, end_d - ai)
    if ri > 0 and count > ri:
        e[-ri:] *= np.linspace(1, 0, ri)
    return e


def sequence(notes, bpm, kind, vol, duty=0.5, s=0.6, legato=0.9):
    beat = 60.0 / bpm
    total = sum(b for _, b in notes) * beat
    buf = np.zeros(int(total * SR) + SR)
    pos = 0.0
    for name, beats in notes:
        length = beats * beat
        m = n(name) if isinstance(name, str) else name
        if m is not None:
            dur = length * legato
            w = osc(kind, midi(m), dur, duty) * env(dur, s=s)
            i = int(pos * SR)
            buf[i:i + len(w)] += w * vol
        pos += length
    return buf[:int(total * SR)]


def drums(pattern_bars, bpm, vol=0.35):
    """pattern: string por colcheia: K=bumbo, S=caixa, h=chimbal, .=nada"""
    beat = 60.0 / bpm
    step = beat / 2
    total = len(pattern_bars) * step
    buf = np.zeros(int(total * SR) + SR)
    rng = np.random.default_rng(3)
    for i, c in enumerate(pattern_bars):
        start = int(i * step * SR)
        if c == "K":
            L = int(0.12 * SR)
            t = np.arange(L) / SR
            f = 120 * np.exp(-t * 25) + 45
            w = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 18)
        elif c == "S":
            L = int(0.1 * SR)
            t = np.arange(L) / SR
            w = rng.uniform(-1, 1, L) * np.exp(-t * 30) * 0.7
        elif c == "h":
            L = int(0.03 * SR)
            t = np.arange(L) / SR
            w = rng.uniform(-1, 1, L) * np.exp(-t * 120) * 0.35
        else:
            continue
        buf[start:start + len(w)] += w * vol
    return buf[:int(total * SR)]


def bass_line(roots, bpm, vol=0.3):
    """roots: uma nota por meio compasso; toca colcheias alternando oitava."""
    notes = []
    for r in roots:
        m = n(r)
        for k in range(4):
            notes.append((m if k % 2 == 0 else m + 12, 0.5))
    return sequence(notes, bpm, "tri", vol, s=0.8, legato=0.85)


def mix(*tracks):
    L = max(len(t) for t in tracks)
    out = np.zeros(L)
    for t in tracks:
        out[:len(t)] += t
    return out


def write(name, data, peak=0.85):
    data = data / (np.max(np.abs(data)) + 1e-9) * peak
    pcm = (data * 32767).astype(np.int16)
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    print("Gerado:", path)


def P(s):
    """'E5:.5 G5:.5 C6:1' -> lista de (nota, tempos)"""
    out = []
    for tok in s.split():
        name, beats = tok.split(":")
        out.append((name, float(beats)))
    return out


# ------------------------------------------------------------------ trilhas

def music_fase1():
    bpm = 132
    mel = P("E5:.5 G5:.5 C6:1 B5:.5 G5:.5 E5:1 "
            "F5:.5 A5:.5 G5:1 E5:.5 C5:.5 D5:1 "
            "E5:.5 G5:.5 C6:.5 D6:.5 E6:1 D6:.5 C6:.5 "
            "B5:1 G5:1 C6:2 "
            "A5:.5 C6:.5 F6:1 E6:.5 C6:.5 A5:1 "
            "G5:.5 B5:.5 D6:1 C6:.5 B5:.5 G5:1 "
            "E5:.5 G5:.5 A5:.5 B5:.5 C6:.5 E6:.5 D6:.5 B5:.5 "
            "C6:2 G5:1 r:1")
    harmony = P("C5:2 G4:2 A4:2 B4:2 C5:2 E5:2 D5:2 E5:2 "
                "F5:2 C5:2 D5:2 B4:2 C5:2 F5:2 E5:2 r:2")
    roots = ["C3", "G2", "F2", "G2", "C3", "C3", "G2", "C3",
             "F2", "F2", "G2", "G2", "A2", "F2", "C3", "G2"]
    pat = "KhShKhSh" * 16
    return mix(sequence(mel, bpm, "square", 0.32, duty=0.25),
               sequence(harmony, bpm, "square", 0.12, duty=0.5, s=0.5),
               bass_line(roots, bpm),
               drums(pat, bpm))


def music_fase2():
    bpm = 144
    mel = P("A4:.5 C5:.5 E5:.5 A5:.5 G5:1 E5:1 "
            "F5:.5 E5:.5 D5:.5 C5:.5 D5:1 E5:1 "
            "A4:.5 C5:.5 E5:.5 A5:.5 B5:1 C6:1 "
            "B5:.5 A5:.5 G5:.5 E5:.5 A5:2 "
            "F5:1 A5:1 C6:1 A5:1 "
            "G5:1 B5:1 D6:1 B5:1 "
            "E5:.5 F5:.5 G5:.5 A5:.5 B5:.5 C6:.5 D6:.5 E6:.5 "
            "A5:3 r:1")
    counter = P("E4:2 C4:2 A4:2 G4:2 E4:2 E5:2 E4:2 C5:2 "
                "C5:2 A4:2 D5:2 B4:2 G#4:2 B4:2 C5:2 r:2")
    roots = ["A2", "A2", "F2", "G2", "A2", "A2", "E2", "E2",
             "F2", "F2", "G2", "G2", "E2", "E2", "A2", "A2"]
    pat = "KhhSKKhS" * 16
    return mix(sequence(mel, bpm, "square", 0.3, duty=0.125),
               sequence(counter, bpm, "saw", 0.08, s=0.5),
               bass_line(roots, bpm, 0.33),
               drums(pat, bpm, 0.4))


def music_gameover():
    bpm = 76
    mel = P("C5:1 B4:1 Bb4:1 A4:1 Ab4:1 G4:1 F#4:1 F4:1 E4:2 Eb4:2 D4:3 r:1")
    bass = P("C3:4 F2:4 C3:4 G2:3 r:1")
    return mix(sequence(mel, bpm, "tri", 0.45, s=0.7, legato=0.95),
               sequence(bass, bpm, "sine", 0.3, s=0.8, legato=0.95),
               sequence(P("E4:4 Ab4:4 G4:4 B3:3 r:1"), bpm, "square", 0.06, duty=0.5, s=0.5, legato=0.95))


# ------------------------------------------------------------------ efeitos

def sweep(f0, f1, length, kind="square", vol=0.5, duty=0.5, curve=1.0):
    t = np.arange(int(length * SR)) / SR
    k = (t / length) ** curve
    f = f0 + (f1 - f0) * k
    ph = np.cumsum(f) / SR % 1.0
    if kind == "square":
        w = np.where(ph < duty, 1.0, -1.0)
    elif kind == "tri":
        w = 4 * np.abs(ph - 0.5) - 1
    else:
        w = np.sin(2 * np.pi * ph)
    return w * env(length, d=length * 0.5, s=0.3, r=length * 0.3) * vol


def notes_sfx(names, step, kind="square", duty=0.5, vol=0.5, last=0.2):
    parts = []
    for i, name in enumerate(names):
        L = last if i == len(names) - 1 else step
        parts.append(osc(kind, midi(n(name)), L, duty) * env(L, d=L * 0.6, s=0.35) * vol)
    return np.concatenate(parts)


def sfx_all():
    write("Sfx_Moeda", notes_sfx(["B5", "E6"], 0.07, duty=0.25, last=0.28))
    write("Sfx_Coracao", notes_sfx(["C5", "E5", "G5", "C6", "E6"], 0.06, kind="tri", last=0.3))
    write("Sfx_Bota", mix(sweep(300, 1400, 0.35, duty=0.25), sweep(600, 2000, 0.35, kind="tri", vol=0.2)))
    vib = sweep(700, 140, 0.5, kind="tri") * (1 + 0.4 * np.sin(np.arange(int(0.5 * SR)) / SR * 2 * np.pi * 18))
    write("Sfx_Gosma", vib)
    rng = np.random.default_rng(1)
    L = int(0.35 * SR)
    hit = rng.uniform(-1, 1, L) * np.exp(-np.arange(L) / SR * 14) * 0.6
    write("Sfx_Espinhos", mix(hit, sweep(220, 70, 0.35, duty=0.5, vol=0.5)))
    tick = notes_sfx(["E7", "B6"], 0.09, duty=0.5, vol=0.3, last=0.09)
    write("Sfx_Relogio", np.concatenate([tick, notes_sfx(["A5", "E6", "A6"], 0.08, kind="tri", last=0.35)]))
    write("Sfx_Queda", sweep(1000, 90, 0.9, kind="square", duty=0.5, curve=0.6))
    write("Sfx_Checkpoint", notes_sfx(["G5", "C6", "E6", "G6"], 0.07, kind="tri", last=0.3))
    write("Sfx_Bandeira", notes_sfx(["C5", "E5", "G5", "C6", "G5", "C6", "E6"], 0.1, duty=0.25, last=0.6))
    write("Sfx_Pulo", sweep(260, 700, 0.14, duty=0.25, vol=0.4, curve=0.5))
    write("Sfx_Negado", notes_sfx(["D4", "D4"], 0.12, duty=0.5, vol=0.4, last=0.2))
    # Piado de aviso: tres trinados agudos subindo
    chirp = np.concatenate([sweep(2200, 3400, 0.07, kind="sine", vol=0.5, curve=0.7), np.zeros(int(0.04 * SR))])
    write("Sfx_Passaro", np.concatenate([chirp, chirp, sweep(2400, 3800, 0.12, kind="sine", vol=0.55, curve=0.6)]))
    # Bicada: estalo curto + nota grave descendo
    L = int(0.06 * SR)
    peck = rng.uniform(-1, 1, L) * np.exp(-np.arange(L) / SR * 60) * 0.7
    write("Sfx_Bicada", mix(np.concatenate([peck, peck * 0.6]), sweep(500, 120, 0.3, duty=0.5, vol=0.45)))


write("Musica_Fase1", music_fase1(), 0.7)
write("Musica_Fase2", music_fase2(), 0.7)
write("Musica_GameOver", music_gameover(), 0.7)
sfx_all()
print("AUDIO CONCLUIDO")
