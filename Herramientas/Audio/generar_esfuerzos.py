#!/usr/bin/env python3
"""
Genera los gritos de esfuerzo y de dolor del combate (Ayni: El Retorno del Guardián).

No usa grabaciones: cada grito se sintetiza con una fuente glotal (pulsos con un poco de temblor), una aspiración
de aire al principio y tres formantes de vocal. Así los sonidos son originales y libres, y se pueden regenerar
con otros valores si suenan demasiado agudos, largos o fuertes.

    python Herramientas/Audio/generar_esfuerzos.py

Escribe en Assets/Resources/AyniAudio/:
    esfuerzo_yari_1..4.wav   golpe fuerte de Yari ("¡Ha!")
    dolor_yari_1..3.wav      Yari recibe un golpe ("¡Uh!")
    dolor_rival_1..4.wav     un rival recibe un golpe fuerte (más grave y ronco)
Necesita numpy y scipy.
"""
import os
import wave

import numpy as np
from scipy.signal import lfilter

RATE = 22050
RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SALIDA = os.path.join(RAIZ, "Assets", "Resources", "AyniAudio")

# Formantes (Hz, ancho de banda Hz) de algunas vocales de voz adulta
VOCALES = {
    "a": [(730, 90), (1150, 110), (2450, 160)],
    "o": [(520, 80), (900, 100), (2400, 160)],
    "u": [(360, 70), (780, 90), (2300, 150)],
    "e": [(480, 80), (1800, 120), (2550, 170)],
}


def resonador(x, frecuencia, ancho):
    """Filtro resonante de dos polos (un formante)."""
    r = np.exp(-np.pi * ancho / RATE)
    theta = 2 * np.pi * frecuencia / RATE
    a = [1.0, -2 * r * np.cos(theta), r * r]
    b = [1.0 - r]
    return lfilter(b, a, x)


def fuente_glotal(f0_inicio, f0_fin, duracion, rng, ronquera=0.0):
    n = int(duracion * RATE)
    t = np.arange(n) / RATE
    # Curva de tono: cae (esfuerzo que se suelta), con un temblor leve
    f0 = np.interp(t, [0, duracion * 0.25, duracion], [f0_inicio * 1.04, f0_inicio, f0_fin])
    f0 *= 1.0 + 0.012 * np.sin(2 * np.pi * 6.5 * t + rng.uniform(0, 6)) + 0.006 * rng.standard_normal(n).cumsum() / np.sqrt(n)
    fase = np.cumsum(f0 / RATE) % 1.0
    # Pulso glotal tipo Rosenberg: subida suave y cierre brusco
    abierta = 0.6
    pulso = np.where(fase < abierta, 0.5 - 0.5 * np.cos(np.pi * fase / abierta),
                     np.cos(0.5 * np.pi * (fase - abierta) / (1 - abierta)))
    pulso = np.diff(pulso, prepend=pulso[0]) * 40.0
    # Ronquera: ruido modulado por el ciclo glotal
    if ronquera > 0:
        pulso += ronquera * rng.standard_normal(n) * (fase < abierta)
    return pulso


def grito(vocal, f0_inicio, f0_fin, duracion, aire, ronquera, rng, ataque=0.012, caida=2.6, brillo=1.0):
    n = int(duracion * RATE)
    voz = fuente_glotal(f0_inicio, f0_fin, duracion, rng, ronquera)
    # Aspiración ("h") al principio y un soplo que acompaña a toda la vocal
    ruido = rng.standard_normal(n)
    t = np.arange(n) / RATE
    h = np.exp(-t / 0.035) * aire
    fuente = voz * (1 - np.exp(-t / 0.02)) + ruido * (h + 0.06 * aire)

    salida = np.zeros(n)
    formantes = VOCALES[vocal]
    ganancias = [1.0, 0.55 * brillo, 0.22 * brillo]
    for (f, bw), g in zip(formantes, ganancias):
        f *= rng.uniform(0.96, 1.04)
        salida += g * resonador(fuente, f, bw)

    # Envolvente: ataque rápido, cuerpo corto y caída
    env = np.minimum(1.0, t / ataque) * np.exp(-np.maximum(0, t - duracion * 0.3) * caida / duracion * 2.2)
    salida *= env
    # Pasa-altos suave para quitar retumbe
    salida = lfilter([1, -1], [1, -0.995], salida)
    salida /= np.max(np.abs(salida)) + 1e-9
    # Un poco de saturación: suena más a garganta
    salida = np.tanh(salida * 1.6) / np.tanh(1.6)
    # Colas en silencio
    fade = int(0.01 * RATE)
    salida[-fade:] *= np.linspace(1, 0, fade)
    return salida * 0.8


def guardar(nombre, señal):
    ruta = os.path.join(SALIDA, nombre)
    datos = (np.clip(señal, -1, 1) * 32767).astype(np.int16)
    with wave.open(ruta, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(datos.tobytes())
    print(f"  {nombre}  {len(señal) / RATE:.2f} s")


def main():
    os.makedirs(SALIDA, exist_ok=True)
    rng = np.random.default_rng(2611)
    print("Esfuerzo de Yari (golpe fuerte)")
    for i, (vocal, f0) in enumerate([("a", 175), ("a", 160), ("o", 168), ("e", 182)], 1):
        guardar(f"esfuerzo_yari_{i}.wav", grito(vocal, f0, f0 * 0.82, rng.uniform(0.2, 0.26), aire=0.9, ronquera=0.08, rng=rng))
    print("Dolor de Yari (golpe recibido)")
    for i, (vocal, f0) in enumerate([("u", 165), ("o", 150), ("a", 158)], 1):
        guardar(f"dolor_yari_{i}.wav", grito(vocal, f0 * 1.1, f0 * 0.7, rng.uniform(0.28, 0.34), aire=0.7, ronquera=0.18, rng=rng, ataque=0.008, caida=2.0, brillo=0.85))
    print("Dolor de los rivales (golpe fuerte)")
    for i, (vocal, f0) in enumerate([("u", 118), ("o", 110), ("a", 124), ("u", 104)], 1):
        guardar(f"dolor_rival_{i}.wav", grito(vocal, f0 * 1.08, f0 * 0.72, rng.uniform(0.26, 0.34), aire=0.6, ronquera=0.32, rng=rng, ataque=0.008, caida=2.0, brillo=0.7))


if __name__ == "__main__":
    main()
