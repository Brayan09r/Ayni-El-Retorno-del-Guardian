"""
Generador de efectos de sonido de AYNI: El Retorno del Guardián.

Sintetiza todos los sonidos del juego (golpes, esquivas, pasos, caídas, dardos, ambiente, quena del prólogo...)
y los guarda como .wav en Assets/Resources/AyniAudio, de donde los carga AyniAudio en el juego.
Son sonidos propios, sin licencias de terceros. Si alguien del equipo graba o consigue uno mejor, basta con
reemplazar el .wav con el mismo nombre (las variantes van numeradas: golpe_ligero_1, golpe_ligero_2...).

Uso (desde la raíz del proyecto):
    python Herramientas/Audio/generar_sfx.py            genera todo
    python Herramientas/Audio/generar_sfx.py --espectros  además dibuja Herramientas/Audio/espectros.png para revisar

Requiere numpy y scipy.
"""

import os
import sys

import numpy as np
from scipy import signal
from scipy.io import wavfile

SR = 44100
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "AyniAudio")

rng = np.random.default_rng(7)
generated = {}


# ───────────────────────── Piezas básicas ─────────────────────────

def n_samples(seconds):
    return max(1, int(round(SR * seconds)))


def time_axis(seconds):
    return np.arange(n_samples(seconds)) / SR


def white(seconds):
    return rng.standard_normal(n_samples(seconds))


def pink(seconds):
    n = n_samples(seconds)
    spectrum = np.fft.rfft(rng.standard_normal(n))
    freqs = np.fft.rfftfreq(n, 1 / SR)
    freqs[0] = freqs[1]
    spectrum /= np.sqrt(freqs)
    x = np.fft.irfft(spectrum, n)
    return x / (np.max(np.abs(x)) + 1e-9)


def brown(seconds):
    x = np.cumsum(rng.standard_normal(n_samples(seconds)))
    x = highpass(x, 20)
    return x / (np.max(np.abs(x)) + 1e-9)


def _sos(kind, freq, order=2):
    nyq = SR / 2
    if isinstance(freq, (list, tuple)):
        wn = [min(f / nyq, 0.999) for f in freq]
    else:
        wn = min(freq / nyq, 0.999)
    return signal.butter(order, wn, btype=kind, output="sos")


def lowpass(x, freq, order=2):
    return signal.sosfilt(_sos("lowpass", freq, order), x)


def highpass(x, freq, order=2):
    return signal.sosfilt(_sos("highpass", freq, order), x)


def bandpass(x, lo, hi, order=2):
    return signal.sosfilt(_sos("bandpass", [lo, hi], order), x)


def sweep_bandpass(x, centers, q=1.5):
    """Filtro pasa-banda de estado variable cuyo centro cambia en cada muestra (para los zumbidos de aire)."""
    y = np.zeros_like(x)
    low = band = 0.0
    damp = 1.0 / q
    for i in range(len(x)):
        f = 2.0 * np.sin(np.pi * min(centers[i], SR * 0.22) / SR)
        high = x[i] - low - damp * band
        band += f * high
        low += f * band
        y[i] = band
    return y


def env_exp(seconds, attack, decay, total=None):
    """Ataque lineal y caída exponencial (decay = segundos hasta bajar a ~1/e)."""
    t = time_axis(total if total is not None else seconds)
    e = np.where(t < attack, t / max(attack, 1e-6), np.exp(-(t - attack) / max(decay, 1e-6)))
    return e


def bell_curve(seconds, peak=0.45, sharpness=2.0):
    t = np.linspace(0, 1, n_samples(seconds))
    a = np.where(t < peak, t / peak, (1 - t) / (1 - peak))
    return np.clip(a, 0, 1) ** sharpness


def sine_sweep(f0, f1, seconds, curve=3.0):
    t = np.linspace(0, 1, n_samples(seconds))
    f = f1 + (f0 - f1) * np.exp(-t * curve)
    return np.sin(2 * np.pi * np.cumsum(f) / SR)


def tone(freq, seconds, harmonics=((1, 1.0),), vibrato=0.0, vib_rate=5.5, vib_delay=0.25):
    t = time_axis(seconds)
    depth = vibrato * np.clip((t - vib_delay) / 0.3, 0, 1)
    f = freq * (1 + depth * np.sin(2 * np.pi * vib_rate * t))
    phase = 2 * np.pi * np.cumsum(f) / SR
    out = np.zeros_like(t)
    for mult, amp in harmonics:
        out += amp * np.sin(mult * phase)
    return out


def softclip(x, drive=2.0):
    return np.tanh(x * drive) / np.tanh(drive)


def tail_fade(x, seconds=0.006):
    """Apaga el final de una capa en unos milisegundos: si se corta de golpe se oye un clic."""
    x = np.asarray(x, dtype=float).copy()
    k = min(len(x), n_samples(seconds))
    if k > 1:
        x[-k:] *= np.linspace(1, 0, k)
    return x


def pad(x, seconds):
    n = n_samples(seconds)
    if len(x) >= n:
        return tail_fade(x[:n])
    return np.concatenate([tail_fade(x), np.zeros(n - len(x))])


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[: len(p)] += p if len(p) == n else tail_fade(p)
    return out


def at(x, seconds, total):
    """Coloca x a partir de 'seconds' dentro de una pista de 'total' segundos."""
    out = np.zeros(n_samples(total))
    start = n_samples(seconds)
    end = min(len(out), start + len(x))
    out[start:end] += tail_fade(x[: end - start])
    return out


def reverb(x, seconds=1.4, mix_amount=0.25, damping=4500):
    """Reverberación de Schroeder: cuatro peines y dos pasa-todo."""
    x = tail_fade(x, 0.02)
    tail = np.concatenate([x, np.zeros(n_samples(seconds))])
    wet = np.zeros_like(tail)
    for delay_ms, gain in ((29.7, 0.0), (37.1, 0.0), (41.1, 0.0), (43.7, 0.0)):
        d = int(SR * delay_ms / 1000)
        g = 10 ** (-3 * d / (SR * seconds))
        a = np.zeros(d + 1)
        a[0] = 1
        a[-1] = -g
        wet += signal.lfilter([1], a, tail)
    for delay_ms in (5.0, 1.7):
        d = int(SR * delay_ms / 1000)
        b = np.zeros(d + 1)
        a = np.zeros(d + 1)
        b[0], b[-1] = -0.7, 1
        a[0], a[-1] = 1, -0.7
        wet = signal.lfilter(b, a, wet)
    wet = lowpass(wet, damping)
    wet /= np.max(np.abs(wet)) + 1e-9
    dry = np.concatenate([x, np.zeros(n_samples(seconds))])
    return (1 - mix_amount) * dry + mix_amount * wet * np.max(np.abs(x))


def fade(x, fade_in=0.002, fade_out=0.01):
    x = x.copy()
    i = min(len(x), n_samples(fade_in))
    o = min(len(x), n_samples(fade_out))
    if i > 0:
        x[:i] *= np.linspace(0, 1, i)
    if o > 0:
        x[-o:] *= np.linspace(1, 0, o)
    return x


def loopify(x, crossfade=1.0):
    """Une el final con el principio para que el bucle no tenga corte."""
    c = n_samples(crossfade)
    head = x[:c]
    body = x[c:].copy()
    ramp = np.linspace(0, 1, c)
    body[-c:] = body[-c:] * (1 - ramp) + head * ramp
    return body


def normalize(x, peak=0.89):
    m = np.max(np.abs(x))
    return x if m < 1e-9 else x / m * peak


def save(name, x, peak=0.89, sample_rate=SR):
    x = normalize(fade(x), peak)
    os.makedirs(OUT, exist_ok=True)
    data = np.int16(np.clip(x, -1, 1) * 32767)
    wavfile.write(os.path.join(OUT, name + ".wav"), sample_rate, data)
    generated[name] = x


def var(value, amount, r):
    return value * (1 + amount * (r.random() * 2 - 1))


# ───────────────────────── Combate ─────────────────────────

def impact(r, body_f0, body_f1, body_len, body_drive, crunch_band, crunch_decay, slap_decay, sub_amp, total, crunch_amp=0.45):
    body = sine_sweep(var(body_f0, 0.12, r), var(body_f1, 0.1, r), body_len, curve=5) * env_exp(body_len, 0.001, body_len * 0.35)
    body = softclip(body * 1.3, body_drive)
    crunch = bandpass(white(total), *crunch_band) * env_exp(total, 0.0005, var(crunch_decay, 0.2, r))
    slap = highpass(white(0.06), 2200) * env_exp(0.06, 0.0003, var(slap_decay, 0.2, r))
    click = highpass(white(0.004), 6000) * np.linspace(1, 0, n_samples(0.004))
    sub = np.sin(2 * np.pi * var(52, 0.1, r) * time_axis(total)) * env_exp(total, 0.002, total * 0.35) * sub_amp
    return mix(pad(body, total), crunch * crunch_amp, pad(slap * 0.5, total), pad(click * 0.35, total), sub)


def make_combat():
    for i in range(3):
        r = np.random.default_rng(100 + i)
        save(f"golpe_ligero_{i + 1}", impact(r, 190, 62, 0.09, 2.5, (450, 1600), 0.03, 0.016, 0.15, 0.22), 0.85)
    for i in range(3):
        r = np.random.default_rng(200 + i)
        save(f"golpe_fuerte_{i + 1}", impact(r, 150, 42, 0.17, 3.5, (300, 1300), 0.065, 0.028, 0.45, 0.42, 0.6), 0.92)

    r = np.random.default_rng(300)
    base = impact(r, 140, 38, 0.2, 4.0, (250, 1200), 0.09, 0.03, 0.6, 1.0, 0.7)
    boom = np.sin(2 * np.pi * 46 * time_axis(1.0)) * env_exp(1.0, 0.004, 0.32) * 0.7
    crack = highpass(white(1.0), 3000) * env_exp(1.0, 0.0002, 0.02) * 0.6
    save("remate", reverb(mix(base, boom, crack), 0.9, 0.18), 0.95)

    for i in range(2):
        r = np.random.default_rng(400 + i)
        thud = lowpass(white(0.2), var(750, 0.15, r)) * env_exp(0.2, 0.001, var(0.045, 0.2, r))
        body = sine_sweep(130, 80, 0.12, 4) * env_exp(0.12, 0.001, 0.04) * 0.5
        slap = highpass(white(0.03), 2500) * env_exp(0.03, 0.0002, 0.01) * 0.3
        save(f"bloqueo_{i + 1}", mix(thud, pad(body, 0.2), pad(slap, 0.2)), 0.7)

    # Desvío (parry): golpe seco y un anillo brillante corto, como un "clac" con brillo
    total = 0.7
    crack = highpass(white(total), 2500) * env_exp(total, 0.0002, 0.012)
    t = time_axis(total)
    index = 3.0 * np.exp(-t / 0.08)
    ring = np.sin(2 * np.pi * 1180 * t + index * np.sin(2 * np.pi * 1180 * 1.41 * t)) * np.exp(-t / 0.22)
    ring += 0.4 * np.sin(2 * np.pi * 2950 * t) * np.exp(-t / 0.12)
    body = sine_sweep(220, 120, 0.1, 4) * env_exp(0.1, 0.001, 0.03)
    save("parry", reverb(mix(crack * 0.9, ring * 0.45, pad(body * 0.5, total)), 0.6, 0.15), 0.85)

    # Zumbidos del aire al golpear
    for i in range(3):
        r = np.random.default_rng(500 + i)
        dur = var(0.17, 0.12, r)
        u = np.linspace(0, 1, n_samples(dur))
        centers = var(700, 0.15, r) + (var(2600, 0.15, r) - 700) * np.sin(np.pi * np.clip(u / 0.9, 0, 1)) ** 1.5
        whoosh = sweep_bandpass(white(dur), centers, 1.4) * bell_curve(dur, 0.5, 1.6)
        flap = lowpass(white(dur), 400) * bell_curve(dur, 0.4, 2) * 0.25
        save(f"swing_ligero_{i + 1}", mix(whoosh, flap), 0.5)
    for i in range(2):
        r = np.random.default_rng(600 + i)
        dur = var(0.28, 0.1, r)
        u = np.linspace(0, 1, n_samples(dur))
        centers = 350 + (var(1500, 0.15, r) - 350) * np.sin(np.pi * u) ** 1.2
        whoosh = sweep_bandpass(white(dur), centers, 1.2) * bell_curve(dur, 0.55, 1.4)
        flap = lowpass(white(dur), 300) * bell_curve(dur, 0.5, 2) * 0.35
        save(f"swing_fuerte_{i + 1}", mix(whoosh, flap), 0.62)

    # Esquiva lograda: el golpe pasa rozando (zumbido rápido y brillante)
    dur = 0.38
    u = np.linspace(0, 1, n_samples(dur))
    centers = 1300 + 3200 * np.sin(np.pi * u) ** 2
    swish = sweep_bandpass(white(dur), centers, 1.8) * bell_curve(dur, 0.42, 1.5)
    air = highpass(white(dur), 5000) * bell_curve(dur, 0.45, 2) * 0.25
    save("esquiva", mix(swish, air), 0.6)

    # Movimiento de la esquiva (roce de la ropa)
    for i in range(2):
        r = np.random.default_rng(700 + i)
        dur = 0.2
        gate = np.repeat(r.random(int(dur * 200)) * 0.7 + 0.3, n_samples(dur) // int(dur * 200) + 1)[: n_samples(dur)]
        rustle = bandpass(white(dur), 800, 3500) * gate * bell_curve(dur, 0.3, 1.5)
        save(f"esquiva_mov_{i + 1}", rustle, 0.35)

    # Postura rota: crujido, astillas y un golpe sordo que se apaga
    total = 0.9
    crack = highpass(white(total), 1500) * env_exp(total, 0.0003, 0.035)
    shards = np.zeros(n_samples(total))
    r = np.random.default_rng(800)
    for _ in range(14):
        start = r.random() * 0.15
        click = highpass(white(0.008), 3500) * np.linspace(1, 0, n_samples(0.008)) * (0.3 + 0.7 * r.random())
        shards += at(click, start, total)
    boom = np.sin(2 * np.pi * 68 * time_axis(total)) * env_exp(total, 0.003, 0.25) * 0.8
    fall = sine_sweep(820, 180, total, 2.5) * env_exp(total, 0.01, 0.25) * 0.15
    save("postura_rota", reverb(mix(crack, shards * 0.6, boom, fall), 0.8, 0.2), 0.9)


# ───────────────────────── Historia y decisiones ─────────────────────────

def bombo(total=2.2, f0=78, f1=46, amp_skin=0.35):
    t = time_axis(total)
    body = sine_sweep(f0, f1, total, 6) * np.exp(-t / 0.55)
    skin = bandpass(white(total), 140, 650) * env_exp(total, 0.001, 0.07) * amp_skin
    attack = highpass(white(total), 1500) * env_exp(total, 0.0003, 0.01) * 0.25
    return mix(body, skin, attack)


def chime(freq, total, decay=1.4, bright=0.35):
    t = time_axis(total)
    x = np.sin(2 * np.pi * freq * t) + bright * np.sin(2 * np.pi * freq * 2.0 * t) * np.exp(-t / (decay * 0.5))
    x += 0.12 * np.sin(2 * np.pi * freq * 3.01 * t) * np.exp(-t / (decay * 0.3))
    return x * np.exp(-t / decay) * np.minimum(1, t / 0.003)


def note(name):
    names = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}
    octave = int(name[-1])
    semitone = names[name[0]] + (1 if "#" in name else 0)
    midi = 12 * (octave + 1) + semitone
    return 440.0 * 2 ** ((midi - 69) / 12)


def make_story():
    save("juicio", reverb(bombo(2.4), 1.8, 0.3, 3500), 0.92)
    save("titulo_golpe", reverb(bombo(1.8, 70, 44, 0.45), 1.5, 0.25, 3000), 0.85)

    total = 3.4
    notes = [("A4", 0.0), ("E5", 0.14), ("A5", 0.28), ("C6", 0.46)]
    x = sum(at(chime(note(n), total - s, 1.6), s, total) for n, s in notes)
    save("ayni_perdon", reverb(x, 2.2, 0.35), 0.6)

    total = 2.8
    hits = mix(at(bombo(1.6, 60, 38, 0.5), 0.0, total), at(bombo(1.4, 58, 36, 0.5) * 0.8, 0.42, total))
    t = time_axis(total)
    saw = signal.sawtooth(2 * np.pi * 55 * t) + signal.sawtooth(2 * np.pi * 58.3 * t)
    growl = lowpass(saw, 320) * np.clip(t / 1.2, 0, 1) * np.exp(-np.clip(t - 1.6, 0, None) / 0.5) * 0.25
    save("venganza", reverb(mix(hits, growl), 1.6, 0.25, 2500), 0.9)

    # La Illa: arpegio pentatónico brillante con un destello de aire
    total = 3.2
    arp = ["A5", "C6", "D6", "E6", "G6", "A6"]
    x = sum(at(chime(note(n), total - i * 0.07, 0.9, 0.5), i * 0.07, total) for i, n in enumerate(arp))
    shimmer = highpass(white(total), 6000) * (0.5 + 0.5 * np.sin(2 * np.pi * 11 * time_axis(total))) * env_exp(total, 0.2, 0.6) * 0.08
    save("illa_brillo", reverb(mix(x, shimmer), 2.4, 0.4), 0.7)


# ───────────────────────── Movimiento ─────────────────────────

def make_movement():
    for i in range(4):
        r = np.random.default_rng(900 + i)
        total = 0.11
        thud = lowpass(white(total), var(900, 0.2, r)) * env_exp(total, 0.0008, var(0.022, 0.25, r))
        grit = bandpass(white(total), 2000, 6500) * env_exp(total, 0.0005, var(0.009, 0.3, r)) * (0.25 + 0.2 * r.random())
        body = np.sin(2 * np.pi * var(95, 0.15, r) * time_axis(total)) * env_exp(total, 0.001, 0.02) * 0.4
        save(f"paso_piedra_{i + 1}", mix(thud, grit, body), 0.55)

    dur = 0.26
    u = np.linspace(0, 1, n_samples(dur))
    save("salto", sweep_bandpass(white(dur), 500 + 1100 * u, 1.3) * bell_curve(dur, 0.35, 1.6), 0.4)

    total = 0.28
    thud = lowpass(white(total), 520) * env_exp(total, 0.001, 0.055)
    body = sine_sweep(100, 55, total, 4) * env_exp(total, 0.001, 0.05) * 0.6
    grit = bandpass(white(total), 1800, 5000) * env_exp(total, 0.001, 0.02) * 0.2
    save("aterrizaje", mix(thud, body, grit), 0.65)

    total = 0.75
    thud = lowpass(white(total), 420) * env_exp(total, 0.001, 0.09)
    body = sine_sweep(90, 40, total, 4) * env_exp(total, 0.001, 0.12)
    dust = lowpass(white(total), 2200) * env_exp(total, 0.02, 0.25) * 0.3
    save("aterrizaje_fuerte", mix(thud, body * 0.8, dust), 0.9)

    # Viento de la caída: rugido que crece y aletea
    total = 3.2
    t = time_axis(total)
    grow = np.clip(t / 2.6, 0, 1) ** 1.3
    centers = 300 + 1700 * grow
    roar = sweep_bandpass(pink(total), centers, 0.9)
    flutter = 1 - 0.35 * grow * (0.5 + 0.5 * np.sin(2 * np.pi * 7 * t))
    save("caida_viento", roar * (0.15 + 0.85 * grow) * flutter, 0.75)

    # Chapuzón en el río de la garganta
    total = 1.4
    impact_noise = sweep_bandpass(white(total), np.linspace(7000, 900, n_samples(total)), 0.8) * env_exp(total, 0.001, 0.22)
    thump = np.sin(2 * np.pi * 58 * t[: n_samples(total)]) * env_exp(total, 0.002, 0.12) * 0.7
    spray = highpass(white(total), 2500) * env_exp(total, 0.01, 0.45) * 0.3
    bubbles = np.zeros(n_samples(total))
    r = np.random.default_rng(1000)
    for _ in range(26):
        start = 0.15 + r.random() * 0.9
        d = 0.02 + r.random() * 0.03
        f = 350 + r.random() * 800
        bub = np.sin(2 * np.pi * np.cumsum(np.linspace(f, f * 1.8, n_samples(d))) / SR) * np.hanning(n_samples(d))
        bubbles += at(bub * (0.15 + 0.2 * r.random()), start, total)
    save("chapuzon", mix(impact_noise, thump, spray, bubbles), 0.9)


# ───────────────────────── Amaru el Cazador ─────────────────────────

def make_hunter():
    total = 0.4
    t = time_axis(total)
    puff = bandpass(white(total), 900, 3200) * env_exp(total, 0.004, 0.05)
    whistle = np.sin(2 * np.pi * np.cumsum(np.linspace(3300, 2100, len(t))) / SR) * env_exp(total, 0.01, 0.12) * 0.18
    save("dardo_lanzado", mix(puff, whistle), 0.55)

    total = 0.16
    thunk = bandpass(white(total), 600, 2200) * env_exp(total, 0.0003, 0.018)
    tick = np.sin(2 * np.pi * 1800 * time_axis(total)) * env_exp(total, 0.0003, 0.012) * 0.5
    save("dardo_impacto", mix(thunk, tick), 0.6)

    total = 1.3
    hiss = highpass(white(total), 3200) * env_exp(total, 0.05, 0.6)
    pops = np.zeros(n_samples(total))
    r = np.random.default_rng(1100)
    for _ in range(30):
        start = r.random() * 1.0
        pop = bandpass(white(0.01), 1500, 4500) * np.hanning(n_samples(0.01))
        pops += at(pop * r.random(), start, total)
    save("veneno", mix(hiss * 0.6, pops * 0.5), 0.45)


# ───────────────────────── Ambiente ─────────────────────────

def make_ambience():
    total = 13.0
    t = time_axis(total)
    gust = 0.55 + 0.25 * np.sin(2 * np.pi * 0.11 * t) + 0.2 * np.sin(2 * np.pi * 0.047 * t + 1.3)
    rumble = lowpass(brown(total), 480) * 0.8
    air = bandpass(pink(total), 380, 1300) * gust * 0.6
    whistle = bandpass(white(total), 850, 980, 2) * np.clip(gust - 0.6, 0, None) * 2.5
    save("amb_viento", loopify(mix(rumble * gust, air, whistle * 0.25), 1.5), 0.42)

    total = 11.0
    r = np.random.default_rng(1200)
    rumble = lowpass(brown(total), 170) * 0.6
    crackle = np.zeros(n_samples(total))
    events = int(total * 22)
    for _ in range(events):
        start = r.random() * (total - 0.05)
        if r.random() < 0.85:
            d = 0.002 + r.random() * 0.006
            c = highpass(white(d), 2500) * np.linspace(1, 0, n_samples(d)) * r.random() ** 2
        else:
            d = 0.015 + r.random() * 0.02
            c = bandpass(white(d), 900, 3200) * np.hanning(n_samples(d)) * (0.5 + 0.5 * r.random())
        crackle += at(c, start, total)
    hiss = highpass(white(total), 4500) * 0.04
    save("amb_fuego", loopify(mix(rumble, crackle * 0.9, hiss), 1.0), 0.55)

    total = 11.0
    r = np.random.default_rng(1300)
    rain = lowpass(highpass(pink(total), 650), 9000) * 0.7
    drops = np.zeros(n_samples(total))
    for _ in range(int(total * 40)):
        start = r.random() * (total - 0.02)
        d = 0.004 + r.random() * 0.01
        drop = lowpass(white(d), 4000) * np.hanning(n_samples(d)) * r.random()
        drops += at(drop, start, total)
    save("amb_lluvia", loopify(mix(rain, drops * 0.6), 1.0), 0.5)


# ───────────────────────── Música: quena del prólogo ─────────────────────────

def quena_note(freq, seconds):
    """Quena: fundamental con armónicos suaves, vibrato tardío, soplo y el chasquido del ataque."""
    body = tone(freq, seconds, ((1, 1.0), (2, 0.32), (3, 0.11), (4, 0.04)), vibrato=0.006, vib_rate=5.3, vib_delay=0.35)
    t = time_axis(seconds)
    envelope = np.minimum(1, t / 0.09) * np.minimum(1, np.maximum(0, (seconds - t) / 0.16))
    breath = bandpass(white(seconds), freq * 1.6, min(freq * 3.2, 15000)) * 0.07
    chiff = bandpass(white(seconds), 1500, 5000) * env_exp(seconds, 0.002, 0.03) * 0.15
    return (body + breath) * envelope + chiff


def make_music():
    beat = 1.0
    # Yaraví en pentatónica de La menor: melancólico, a tempo libre y lento
    melody = [
        ("E5", 1), ("D5", 0.5), ("C5", 0.5), ("A4", 2),
        ("C5", 1), ("D5", 0.5), ("E5", 0.5), ("G5", 1.5), ("E5", 0.5),
        ("D5", 1), ("C5", 0.5), ("A4", 0.5), ("G4", 1), ("A4", 2),
        ("E5", 1), ("G5", 1), ("A5", 1.5), ("G5", 0.5), ("E5", 1), ("D5", 1),
        ("C5", 0.5), ("D5", 0.5), ("A4", 3.5),
    ]
    total = sum(d for _, d in melody) * beat + 3.5
    track = np.zeros(n_samples(total))
    time = 0.6
    for name, d in melody:
        seconds = d * beat
        track += at(quena_note(note(name), seconds + 0.06) * 0.55, time, total)
        time += seconds

    t = time_axis(total)
    swell = np.clip(t / 4, 0, 1) * np.clip((total - t) / 3, 0, 1)
    drone = (np.sin(2 * np.pi * note("A2") * t) * 0.5 + np.sin(2 * np.pi * note("E3") * t) * 0.3 +
             np.sin(2 * np.pi * note("A3") * t) * 0.12) * swell * 0.22
    drone = lowpass(drone, 900)
    save("prologo_quena", reverb(mix(track, drone), 2.8, 0.32, 5000), 0.62)


# ───────────────────────── Interfaz ─────────────────────────

def make_ui():
    total = 0.06
    t = time_axis(total)
    save("ui_mover", np.sin(2 * np.pi * 1250 * t) * np.exp(-t / 0.012), 0.35)
    total = 0.22
    x = at(chime(note("E5"), 0.12, 0.06, 0.2), 0, total) + at(chime(note("A5"), 0.15, 0.08, 0.2), 0.07, total)
    save("ui_confirmar", x, 0.4)


def draw_spectra(path):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    names = list(generated.keys())
    cols = 6
    rows = (len(names) + cols - 1) // cols
    fig, axes = plt.subplots(rows, cols, figsize=(cols * 3.2, rows * 2.2))
    for ax, name in zip(axes.flat, names):
        x = generated[name]
        ax.specgram(x, NFFT=512, Fs=SR, noverlap=384, cmap="magma", vmin=-120)
        ax.set_ylim(0, 12000)
        ax.set_title(f"{name} ({len(x) / SR:.2f}s)", fontsize=8)
        ax.tick_params(labelsize=6)
    for ax in list(axes.flat)[len(names):]:
        ax.axis("off")
    fig.tight_layout()
    fig.savefig(path, dpi=70)


if __name__ == "__main__":
    make_combat()
    make_story()
    make_movement()
    make_hunter()
    make_ambience()
    make_music()
    make_ui()
    print(f"{len(generated)} sonidos generados en {OUT}")
    for name, x in generated.items():
        rms = np.sqrt(np.mean(x ** 2))
        print(f"  {name:22s} {len(x) / SR:5.2f} s  pico {np.max(np.abs(x)):.2f}  rms {rms:.3f}")
    if "--espectros" in sys.argv:
        out = os.path.join(os.path.dirname(__file__), "espectros.png")
        draw_spectra(out)
        print("Espectrogramas:", out)
