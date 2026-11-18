"""
Analiza las pistas de música de AYNI (las de Suno en Assets/Resources/AyniAudio): duración, primer sonido, primer
golpe fuerte, tempo aproximado y volumen del principio y del final.

AyniLevelMusic usa esos segundos para que el combate entre en su primer golpe fuerte y para que el golpe fuerte de la
pista de Amaru caiga sobre su título. Si se cambia una pista, se vuelve a medir y se ajustan sus campos de "Entradas".

Necesita ffmpeg en el PATH y numpy.
uso: python analizar_musica.py Assets/Resources/AyniAudio/musica_nivel1_combate.mp3 [...]
"""
import subprocess
import sys

import numpy as np

SR = 22050


def decode(path):
    raw = subprocess.run(["ffmpeg", "-v", "quiet", "-i", path, "-ac", "1", "-ar", str(SR), "-f", "f32le", "-"],
                         capture_output=True).stdout
    return np.frombuffer(raw, dtype=np.float32)


def analyze(path):
    x = decode(path)
    dur = len(x) / SR
    hop = 512
    frames = len(x) // hop
    rms = np.array([np.sqrt(np.mean(x[i * hop:(i + 1) * hop] ** 2)) for i in range(frames)])
    db = 20 * np.log10(rms + 1e-9)
    peak = db.max()
    t = np.arange(frames) * hop / SR
    first_sound = t[np.argmax(db > peak - 40)]
    # flujo de energía (inicio de golpes)
    flux = np.maximum(0, np.diff(rms, prepend=rms[0]))
    thr = flux.mean() + 4 * flux.std()
    strong = t[np.argmax(flux > thr)] if (flux > thr).any() else first_sound
    # tempo por autocorrelación del flujo (60-180 bpm)
    f = flux - flux.mean()
    ac = np.correlate(f, f, mode="full")[len(f) - 1:]
    lags = np.arange(len(ac)) * hop / SR
    mask = (lags > 60 / 180) & (lags < 60 / 60)
    beat = lags[mask][np.argmax(ac[mask])]
    bpm = 60 / beat
    # volumen al principio y al final (para ver si el bucle se nota)
    def level(a, b):
        s = x[int(a * SR):int(b * SR)]
        return 20 * np.log10(np.sqrt(np.mean(s ** 2)) + 1e-9)
    print(f"{path.split('/')[-1].split(chr(92))[-1]}: {dur:.1f} s, pico {peak:.1f} dB, primer sonido {first_sound:.2f} s, "
          f"primer golpe fuerte {strong:.2f} s, ~{bpm:.0f} bpm, inicio(0-3s) {level(0, 3):.1f} dB, "
          f"final(-3s) {level(dur - 3, dur):.1f} dB, medio {level(dur / 2 - 5, dur / 2 + 5):.1f} dB")
    # los 8 golpes más fuertes de los primeros 20 s
    first = flux[t < 20]
    idx = np.argsort(first)[-8:]
    print("   golpes fuertes en los primeros 20 s:", ", ".join(f"{t[i]:.2f}" for i in sorted(idx)))


for p in sys.argv[1:]:
    analyze(p)
