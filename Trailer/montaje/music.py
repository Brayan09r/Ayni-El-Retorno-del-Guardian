"""Música original del tráiler (estilo andino, 100 pulsos por minuto). Los cambios caen sobre los cortes del montaje."""
import numpy as np, wave
from synth import *

TOTAL = 67.8
B = 0.6          # un pulso
E8 = 0.3         # corchea
m = Mix(TOTAL + 2)

N = {'A2':45,'C3':48,'D3':50,'E3':52,'F3':53,'G3':55,'A3':57,'B3':59,'C4':60,'D4':62,'E4':64,'F4':65,'G4':67,'A4':69,'B4':71,
     'C5':72,'D5':74,'E5':76,'F5':77,'G5':79,'A5':81,'B5':83,'C6':84,'D6':86,'E6':88,'E2':40,'F2':41,'G2':43,'C2':36,'A1':33}
CH = {'Am': (['A2','E3','A3','C4'], ['A4','C5','E5','A5'], 'A2'),
      'C':  (['C3','G3','C4','E4'], ['G4','C5','E5','G5'], 'C3'),
      'G':  (['G2','D3','G3','B3'], ['G4','B4','D5','G5'], 'G2'),
      'F':  (['F2','C3','F3','A3'], ['F4','A4','C5','F5'], 'F2'),
      'Em': (['E2','B3','E3','G3'], ['G4','B4','E5','G5'], 'E2')}

def mel(notes, start, inst='quena', vol=1.0, pan=0.0):
    t = start
    for name, dur in notes:
        if name:
            f = quena if inst == 'quena' else zampona
            kw = dict(bend=0.03) if inst == 'quena' else {}
            soft = min(1.0, (hz(76) / hz(N[name])) ** 0.7)      # las notas muy agudas, más suaves
            m.add(f(N[name], dur * 0.96, vol * soft, **kw), t, pan)
        t += dur
    return t

def groove(start, chord, drive=1.0, drums=True, strums=True, bass=True, shaker=True, eighth_kick=False):
    low, high, root = CH[chord]
    hi = [N[x] for x in high]
    for i in range(8):                       # ocho corcheas por compás
        t = start + i * E8
        if shaker: m.add(chajcha(0.55 if i % 2 else 0.3), t, 0.35, drive)
        if strums:
            down = i % 2 == 0
            acc = 1.0 if i in (0, 3, 4, 6) else 0.55
            m.add(strum(hi, 0.55, acc * 1.25, 0.012, 0.85, up=not down), t, -0.3, drive)
        if drums:
            if i in (0, 3, 4) or (eighth_kick and i in (2, 6, 7)): m.add(bombo(1.0 if i == 0 else 0.75), t, 0, drive)
            if i in (2, 6): m.add(aro(0.8), t, 0.15, drive)
        if bass and i in (0, 3, 4, 6):
            m.add(pluck(N[root] + (7 if i == 6 else 0), 0.5, 0.8, 0.35), t, 0, drive)

def arp(start, chord, bars=1, vol=0.7):
    _, high, _ = CH[chord]
    seq = [0, 1, 2, 3, 2, 1, 2, 1]
    for b in range(bars):
        for i, k in enumerate(seq):
            m.add(pluck(N[high[k]], 0.7, vol * 1.3, 0.85), start + b*2.4 + i*E8, -0.25 + 0.5*(i % 2))

# ── 0.0 – 8.4 · La noche de las cenizas ──────────────────────────────
m.add(wind(9.5, 0.2), 0.0)
m.add(pad([N['A2'], N['E3'], N['A3']], 9.2, 0.3, 700), 0.0)
for t, v in [(0.0, 0.6), (3.6, 0.5), (6.0, 0.65), (7.2, 0.45), (7.8, 0.55), (8.1, 0.7)]:
    m.add(bombo(v), t)
mel([('A4', 1.2), ('C5', 0.6), ('D5', 0.6), ('E5', 1.8), ('D5', 0.3), ('C5', 0.3), ('A4', 1.8)], 1.2, vol=0.75, pan=0.1)
m.add(riser(1.6, 0.5), 6.8)

# ── 8.4 – 12.0 · Título ──────────────────────────────────────────────
m.add(bombo(1.25), 8.4); m.add(bombo(0.8, False), 8.4)
m.add(strum([N[x] for x in ['A2','E3','A3','C4','E4','A4','C5','E5']], 3.2, 1.5, 0.02, 0.8), 8.4)
m.add(pad([N['A2'], N['E3'], N['C4'], N['B4']], 4.4, 0.6, 1100), 8.3)
mel([('A5', 2.4), ('G5', 0.6), ('E5', 0.6)], 8.4, vol=0.8, pan=0.1)
m.add(strum([N[x] for x in ['A4','C5','E5','A5']], 1.6, 0.6, 0.03, 0.8), 10.2, -0.3)

# ── 12.0 – 14.4 · Despertar ──────────────────────────────────────────
arp(12.0, 'Am', 1, 0.6)
m.add(pad([N['A2'], N['E3'], N['A3']], 2.8, 0.4, 800), 11.8)
for i in range(8): m.add(chajcha(0.25 + 0.05*i), 12.0 + i*E8, 0.35)
mel([('E5', 0.3), ('G5', 0.3), ('A5', 0.6)], 13.2, vol=0.7, pan=0.1)

# ── 14.4 – 21.6 · El camino ──────────────────────────────────────────
for i, ch in enumerate(['Am', 'C', 'G']):
    groove(14.4 + i*2.4, ch, 0.9)
mel([('A5', 0.6), ('G5', 0.3), ('E5', 0.3), ('G5', 0.6), ('E5', 0.3), ('D5', 0.3),
     ('E5', 0.6), ('G5', 0.3), ('E5', 0.3), ('D5', 0.6), ('C5', 0.6),
     ('D5', 0.3), ('E5', 0.3), ('D5', 0.3), ('C5', 0.3), ('A4', 1.2)], 14.4, vol=0.85, pan=0.1)

# ── 21.6 – 31.2 · La aldea ───────────────────────────────────────────
for i, ch in enumerate(['C', 'Am', 'F', 'G']):
    t = 21.6 + i*2.4
    arp(t, ch, 1, 0.65)
    groove(t, ch, 0.55 if i < 2 else 0.75, drums=i >= 2, strums=False, bass=True, shaker=True)
    m.add(pad([N[x] for x in CH[ch][0][1:]], 2.6, 0.35, 1000), t - 0.1)
mel([('E5', 0.6), ('G5', 0.6), ('A5', 0.6), ('G5', 0.6),
     ('E5', 0.9), ('D5', 0.3), ('C5', 0.6), ('A4', 0.6),
     ('A4', 0.6), ('C5', 0.6), ('D5', 0.6), ('C5', 0.6),
     ('D5', 0.6), ('E5', 0.6), ('G5', 1.2)], 21.6, inst='zampona', vol=0.9, pan=-0.1)
m.add(riser(1.2, 0.35), 30.0)

# ── 31.2 – 46.2 · La banda de Amaru ──────────────────────────────────
combat = ['Am', 'Am', 'F', 'G', 'Am', 'Em']
riffs = [['A5','A5','G5','A5',None,'E5','G5',None], ['A5','A5','G5','A5',None,'C6','A5',None],
         ['C6','C6','A5','C6',None,'A5','G5',None], ['D6','D6','B5','D6',None,'B5','G5',None],
         ['E6',None,'D6','C6','A5',None,'G5','A5'], ['B5','B5','G5','B5',None,'E5','G5','B5']]
for i, ch in enumerate(combat):
    t = 31.2 + i*2.4
    groove(t, ch, 1.0, eighth_kick=True)
    m.add(pad([N[x] for x in CH[ch][0][:3]], 2.5, 0.45, 600), t - 0.05)
    mel([(n, E8) for n in riffs[i]], t, inst='zampona', vol=0.8, pan=-0.15)
    mel([(n and n.replace('6', '5').replace('5', '4') if False else n, E8) for n in riffs[i]], t, vol=0.4, pan=0.2)
m.add(bombo(1.2), 31.2)
for k, t in enumerate([45.6, 45.75, 45.9, 46.05]): m.add(bombo(0.7 + 0.12*k, False), t)   # redoble de cierre

# ── 46.2 – 48.6 · El Juicio ──────────────────────────────────────────
m.add(bombo(1.3), 46.2)
m.add(pad([N['A1'], N['A2']], 3.0, 0.3, 350), 46.1)
for t, v in [(46.8, 0.5), (47.1, 0.38), (47.7, 0.55), (48.0, 0.42)]: m.add(bombo(v), t)
m.add(quena(N['E6'], 2.0, 0.28, vib=4.5), 46.4, 0.2)

# ── 48.6 – 53.4 · El perdón ──────────────────────────────────────────
for i, ch in enumerate(['C', 'G']):
    t = 48.6 + i*2.4
    arp(t, ch, 1, 0.6)
    m.add(pad([N[x] for x in CH[ch][0]], 2.7, 0.5, 1100), t - 0.1)
mel([('G5', 1.2), ('E5', 0.6), ('G5', 0.6), ('A5', 1.2), ('G5', 0.6), ('D5', 0.6)], 48.6, vol=0.8, pan=0.1)
m.add(riser(1.0, 0.3), 52.4)

# ── 53.4 – 63.0 · El bosque renace ───────────────────────────────────
for i, ch in enumerate(['C', 'G', 'Am', 'F']):
    t = 53.4 + i*2.4
    groove(t, ch if not (ch == 'F') else 'F', 0.95)
    arp(t, ch, 1, 0.45)
    m.add(pad([N[x] for x in CH[ch][0]], 2.6, 0.45, 1200), t - 0.1)
m.add(bombo(1.2), 53.4)
lead = [('C6', 0.9), ('A5', 0.3), ('G5', 0.6), ('E5', 0.6),
        ('D6', 0.9), ('B5', 0.3), ('G5', 0.6), ('B5', 0.6),
        ('E6', 0.9), ('D6', 0.3), ('C6', 0.6), ('A5', 0.6),
        ('C6', 0.6), ('A5', 0.6), ('B5', 0.6), ('D6', 0.6)]
mel(lead, 53.4, vol=0.85, pan=0.1)
third = {'C6':'A5','A5':'E5','G5':'E5','E5':'C5','D6':'B5','B5':'G5','E6':'C6'}
mel([(third.get(n, n), d) for n, d in lead], 53.4, inst='zampona', vol=0.5, pan=-0.25)

# ── 63.0 – fin · Acorde final ────────────────────────────────────────
m.add(bombo(1.3), 63.0); m.add(bombo(0.8, False), 63.0)
m.add(strum([N[x] for x in ['C3','G3','C4','E4','G4','C5','E5','G5']], 4.5, 1.6, 0.022, 0.8), 63.0)
m.add(pad([N['C3'], N['G3'], N['E4'], N['C5']], 5.0, 0.6, 1300), 62.9)
m.add(quena(N['C6'], 3.0, 0.75), 63.0, 0.1)
m.add(strum([N[x] for x in ['G4','C5','E5','G5']], 2.5, 0.5, 0.035, 0.8), 64.8, -0.3)
m.add(bombo(0.6), 65.4)

# ── Mezcla ───────────────────────────────────────────────────────────
x = m.buf[:int((TOTAL + 0.6) * SR)]
from scipy.signal import butter, sosfilt
x = sosfilt(butter(4, 38, 'hp', fs=SR, output='sos'), x, axis=0)
x = reverb(x, 2.4, 0.22)
x = x / np.abs(x).max()
x = np.tanh(x * 1.15) / np.tanh(1.15)          # compresión suave
t = np.arange(len(x)) / SR
fade = np.clip(t / 0.4, 0, 1) * np.clip((TOTAL + 0.3 - t) / 2.2, 0, 1)
x = x * fade[:, None] * 0.66
with wave.open('musica.wav', 'wb') as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes((x * 32767).astype('<i2').tobytes())
rms = np.sqrt((x**2).mean()); print('duración', len(x)/SR, 'pico', np.abs(x).max().round(3), 'rms dB', (20*np.log10(rms)).round(1))
for a, b in [(0,8.4),(8.4,12),(14.4,21.6),(21.6,31.2),(31.2,46.2),(46.2,48.6),(48.6,53.4),(53.4,63),(63,67.8)]:
    s = x[int(a*SR):int(b*SR)]; print(f'{a:5.1f}-{b:5.1f}: rms {20*np.log10(np.sqrt((s**2).mean())+1e-9):6.1f} dB')
