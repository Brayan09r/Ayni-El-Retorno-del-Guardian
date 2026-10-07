"""Sintetizador pequeño para la música del tráiler: quena, zampoña, charango, bombo, chajchas y colchón."""
import numpy as np
SR = 44100
rng = np.random.default_rng(7)

def hz(midi): return 440.0 * 2 ** ((midi - 69) / 12)

def env_adsr(n, a, d, s, r):
    a_n, d_n, r_n = int(a*SR), int(d*SR), int(r*SR)
    s_n = max(0, n - a_n - d_n - r_n)
    e = np.concatenate([np.linspace(0, 1, max(a_n,1), endpoint=False), np.linspace(1, s, max(d_n,1), endpoint=False),
                        np.full(s_n, s), np.linspace(s, 0, max(r_n,1))])
    if len(e) < n: e = np.pad(e, (0, n-len(e)))
    return e[:n]

def lowpass(x, cutoff):
    # un polo, pasado dos veces
    a = np.exp(-2*np.pi*cutoff/SR)
    from scipy.signal import lfilter
    y = lfilter([1-a], [1, -a], x)
    return lfilter([1-a], [1, -a], y)

def highpass(x, cutoff):
    return x - lowpass(x, cutoff)

def quena(midi, dur, vol=1.0, vib=5.2, bend=0.0):
    """Flauta de caña: fundamental fuerte, armónicos impares suaves, soplo y vibrato que entra tarde."""
    n = int((dur + 0.25) * SR); t = np.arange(n) / SR
    f = hz(midi)
    vib_depth = 0.006 * np.clip((t - 0.18) / 0.35, 0, 1)
    glide = 1 - bend * np.exp(-t / 0.05)          # entra un poco por debajo de la nota
    phase = 2*np.pi*np.cumsum(f * glide * (1 + vib_depth*np.sin(2*np.pi*vib*t))) / SR
    tone = np.sin(phase) + 0.32*np.sin(2*phase) + 0.2*np.sin(3*phase) + 0.07*np.sin(4*phase) + 0.05*np.sin(5*phase)
    breath = highpass(lowpass(rng.standard_normal(n), 5200), 1400) * 0.9
    e = env_adsr(n, 0.055, 0.12, 0.8, 0.22)
    chiff = np.exp(-t/0.03) * 1.3
    return vol * (tone * e + breath * e * (0.055 + 0.25*chiff*0.2)) * 0.5

def zampona(midi, dur, vol=1.0):
    """Siku: ataque con mucho aire y nota hueca, corta."""
    n = int((dur + 0.2) * SR); t = np.arange(n) / SR
    f = hz(midi)
    phase = 2*np.pi*f*t
    tone = np.sin(phase) + 0.12*np.sin(3*phase) + 0.05*np.sin(5*phase)
    breath = highpass(lowpass(rng.standard_normal(n), 4200), 900)
    e = env_adsr(n, 0.03, 0.1, 0.65, 0.16)
    return vol * (tone*e + breath*(np.exp(-t/0.05)*0.55 + 0.06)*e) * 0.45

def pluck(midi, dur, vol=1.0, bright=0.5):
    """Cuerda pulsada (Karplus-Strong): charango si es aguda, guitarra si es grave."""
    f = hz(midi); n = int(dur * SR); period = int(round(SR / f))
    buf = rng.uniform(-1, 1, period)
    buf = lowpass(buf, 2500 + 7000*bright)
    out = np.empty(n)
    damp = 0.996 - 0.0015*(1-bright)
    # bucle por bloques de un periodo
    cur = buf.copy(); pos = 0
    while pos < n:
        m = min(period, n - pos)
        out[pos:pos+m] = cur[:m]
        cur = damp * 0.5 * (cur + np.roll(cur, 1))
        pos += m
    out *= np.minimum(1, np.arange(n)/ (0.002*SR))
    return vol * out * 0.6

def strum(midis, dur, vol=1.0, spread=0.018, bright=0.6, up=False):
    n = int((dur + spread*len(midis)) * SR) + 1
    out = np.zeros(n)
    order = list(reversed(midis)) if up else midis
    for i, m in enumerate(order):
        s = pluck(m, dur, vol, bright); o = int(i*spread*SR)
        out[o:o+len(s)] += s[:n-o]
    return out / max(1, len(midis))**0.5

def bombo(vol=1.0, deep=True):
    """Bombo legüero: golpe grave con caída de tono y parche."""
    n = int(0.9*SR); t = np.arange(n)/SR
    f0, f1 = (95, 44) if deep else (150, 70)
    f = f1 + (f0-f1)*np.exp(-t/0.06)
    body = np.sin(2*np.pi*np.cumsum(f)/SR) * np.exp(-t/(0.32 if deep else 0.16))
    skin = lowpass(rng.standard_normal(n), 900) * np.exp(-t/0.03) * 0.8
    return vol * (body + skin) * 0.55

def aro(vol=1.0):
    """Golpe en el aro del bombo (madera)."""
    n = int(0.12*SR); t = np.arange(n)/SR
    x = (np.sin(2*np.pi*820*t) + 0.6*np.sin(2*np.pi*1730*t)) * np.exp(-t/0.018)
    return vol * x * 0.5

def chajcha(vol=1.0, dur=0.11):
    """Sonajero de pezuñas: ráfaga de ruido agudo."""
    n = int(dur*SR); t = np.arange(n)/SR
    x = highpass(rng.standard_normal(n), 3500) * (np.exp(-t/0.035) * np.minimum(1, t/0.004))
    return vol * x * 0.5

def pad(midis, dur, vol=1.0, cutoff=900):
    """Colchón: sierras desafinadas muy filtradas, con entrada y salida lentas."""
    n = int(dur*SR); t = np.arange(n)/SR
    out = np.zeros(n)
    for m in midis:
        for det in (-0.07, 0.0, 0.08):
            f = hz(m + det)
            ph = (f*t) % 1.0
            out += 2*ph - 1
    out = lowpass(out, cutoff) / (len(midis)*3)
    e = np.minimum(1, t/min(1.5, dur*0.3)) * np.minimum(1, (dur - t)/min(1.8, dur*0.3))
    return vol * out * e * 1.6

def wind(dur, vol=1.0):
    n = int(dur*SR); t = np.arange(n)/SR
    x = lowpass(rng.standard_normal(n), 700)
    x = highpass(x, 180)
    lfo = 0.55 + 0.45*np.sin(2*np.pi*0.13*t + 1.0) * np.sin(2*np.pi*0.07*t)
    e = np.minimum(1, t/2.0) * np.minimum(1, (dur-t)/2.0)
    return vol * x * lfo * e * 2.2

def riser(dur, vol=1.0):
    n = int(dur*SR); t = np.arange(n)/SR
    x = rng.standard_normal(n)
    from scipy.signal import lfilter
    # barrido de filtro por bloques
    out = np.zeros(n); blk = 2048
    for i in range(0, n, blk):
        c = 300 + 6000*(i/n)**2
        out[i:i+blk] = lowpass(x[i:i+blk], c)
    return vol * out * (t/dur)**2 * 0.8

def reverb(x, seconds=2.2, mix=0.25):
    from scipy.signal import fftconvolve
    n = int(seconds*SR); t = np.arange(n)/SR
    irL = rng.standard_normal(n) * np.exp(-t/(seconds/4.5)); irR = rng.standard_normal(n) * np.exp(-t/(seconds/4.5))
    irL = lowpass(irL, 4500); irR = lowpass(irR, 4500)
    irL /= np.sqrt((irL**2).sum()); irR /= np.sqrt((irR**2).sum())
    wetL = fftconvolve(x[:,0], irL)[:len(x)]; wetR = fftconvolve(x[:,1], irR)[:len(x)]
    return x*(1-mix*0.4) + np.stack([wetL, wetR], 1)*mix

class Mix:
    def __init__(self, seconds):
        self.buf = np.zeros((int(seconds*SR)+SR, 2))
    def add(self, sound, at, pan=0.0, vol=1.0):
        o = int(at*SR)
        if o >= len(self.buf) or o < 0: return
        s = sound[:len(self.buf)-o] * vol
        l, r = np.cos((pan+1)*np.pi/4), np.sin((pan+1)*np.pi/4)
        self.buf[o:o+len(s), 0] += s*l; self.buf[o:o+len(s), 1] += s*r
