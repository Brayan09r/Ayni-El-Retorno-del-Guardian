#!/usr/bin/env python3
"""
Genera las voces de los subtítulos de AYNI con Piper (síntesis de voz sin internet) a partir de voces_guion.txt.

Cada frase se guarda en Assets/Resources/AyniVoces/v_XXXXXXXX.ogg, donde XXXXXXXX es el hash FNV-1a (32 bits) del
texto normalizado: el mismo que calcula AyniVoice.Key en el juego, así que no hace falta registrar nada en Unity.

Requisitos:
    pip install piper-tts        (Python 3.9+)
    ffmpeg en el PATH            (pitch, eco y codificación a .ogg)
Las voces de Piper se descargan solas la primera vez (versión v0.0.2 de rhasspy/piper en GitHub) a
Herramientas/Audio/modelos_voz/ (fuera de git):
    es-carlfm-x-low  voz masculina (dominio público)      → la narración, Amaru, el Capitán y Yari
    es-mls_10246-low voz femenina (MLS, CC BY 4.0)          → la tejedora

Uso:
    python Herramientas/Audio/generar_voces.py            genera las que faltan
    python Herramientas/Audio/generar_voces.py --todas    las rehace todas
    python Herramientas/Audio/generar_voces.py --limpiar  además borra las voces que ya no están en el guion
"""
import argparse
import os
import subprocess
import sys
import tarfile
import tempfile
import urllib.request
import wave

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.abspath(os.path.join(AQUI, "..", ".."))
GUION = os.path.join(AQUI, "voces_guion.txt")
MODELOS = os.path.join(AQUI, "modelos_voz")
SALIDA = os.path.join(RAIZ, "Assets", "Resources", "AyniVoces")
URL_VOCES = "https://github.com/rhasspy/piper/releases/download/v0.0.2/voice-{}.tar.gz"

# Cómo suena cada personaje: modelo, ritmo (length_scale: más bajo = más rápido), ruido_w (variación de la duración de
# cada sonido: la voz femenina, entrenada con audiolibros, alarga mucho las pausas si es alta), tono (1 = el del modelo)
# y filtros de ffmpeg
PERSONAJES = {
    # Calmada y con sala, como un relato junto al fuego. (La voz femenina MLS no sirve aquí: en unas frases estira las
    # palabras y en otras se las salta.)
    "NARRADORA": dict(modelo="es-carlfm-x-low", ritmo=1.02, ruido_w=0.5, tono=1.0, volumen=1.0,
                      filtro="lowpass=f=6500,aecho=0.85:0.5:50|100:0.16|0.09"),
    # Grave, lento y con eco de cañón
    "AMARU": dict(modelo="es-carlfm-x-low", ritmo=1.08, ruido_w=0.6, tono=0.86, volumen=1.15,
                  filtro="bass=g=4:f=140,aecho=0.8:0.55:70|140:0.2|0.12"),
    # Adulto, firme
    "CAPITAN": dict(modelo="es-carlfm-x-low", ritmo=1.0, ruido_w=0.6, tono=0.94, volumen=1.05, filtro="bass=g=2:f=160"),
    # Joven y decidido
    "YARI": dict(modelo="es-carlfm-x-low", ritmo=0.95, ruido_w=0.6, tono=1.07, volumen=1.05, filtro=""),
    "TEJEDORA": dict(modelo="es-mls_10246-low", ritmo=0.6, ruido_w=0.25, tono=1.06, volumen=1.0, filtro=""),
}
ACELERAR_MAX = 1.3
# Las pausas de más de este tiempo (s) se acortan a PAUSA_MAX: sin silencios largos entre frases
PAUSA_MAX = 0.28


def clave(texto: str) -> int:
    """FNV-1a de 32 bits del texto normalizado (igual que AyniVoice.Key en C#)."""
    h = 2166136261
    for b in normalizar(texto).encode("utf-8"):
        h ^= b
        h = (h * 16777619) & 0xFFFFFFFF
    return h


def normalizar(texto: str) -> str:
    """Igual que AyniVoice.Normalize: sin etiquetas ni teclas [...], sin "NOMBRE:" inicial, solo letras/números, en minúsculas."""
    sin_etiquetas, dentro = [], False
    for c in texto:
        if c in "<[":
            dentro = True
            continue
        if c in ">]" and dentro:
            dentro = False
            continue
        if not dentro:
            sin_etiquetas.append(c)
    t = "".join(sin_etiquetas).strip()
    dos_puntos = t.find(":")
    if 1 < dos_puntos < 32 and all(c.isupper() or c == " " for c in t[:dos_puntos]):
        t = t[dos_puntos + 1:]
    salida, espacio = [], False
    for c in t:
        if c.isalnum():
            if espacio and salida:
                salida.append(" ")
            espacio = False
            salida.append(c.lower())
        elif c.isspace():
            espacio = True
    return "".join(salida)


def leer_guion():
    frases = []
    with open(GUION, encoding="utf-8") as f:
        for n, linea in enumerate(f, 1):
            linea = linea.strip()
            if not linea or linea.startswith("#"):
                continue
            partes = [p.strip() for p in linea.split("|")]
            if len(partes) < 3 or partes[0] not in PERSONAJES:
                sys.exit(f"voces_guion.txt:{n}: formato «PERSONAJE | segundos | texto [| hablado]» o personaje desconocido")
            frases.append(dict(personaje=partes[0], maximo=float(partes[1]), texto=partes[2],
                               hablado=partes[3] if len(partes) > 3 else partes[2]))
    return frases


def modelo(nombre: str) -> str:
    ruta = os.path.join(MODELOS, nombre + ".onnx")
    if os.path.exists(ruta):
        return ruta
    os.makedirs(MODELOS, exist_ok=True)
    print(f"Descargando la voz {nombre}...")
    with tempfile.TemporaryDirectory() as tmp:
        tgz = os.path.join(tmp, "voz.tar.gz")
        urllib.request.urlretrieve(URL_VOCES.format(nombre), tgz)
        with tarfile.open(tgz) as t:
            t.extractall(MODELOS)
    return ruta


def duracion(wav: str) -> float:
    with wave.open(wav) as w:
        return w.getnframes() / w.getframerate()


def generar(frases, todas: bool):
    from piper import PiperVoice, SynthesisConfig

    os.makedirs(SALIDA, exist_ok=True)
    voces, hechas, saltadas = {}, 0, 0
    for f in frases:
        destino = os.path.join(SALIDA, f"v_{clave(f['texto']):08x}.ogg")
        if os.path.exists(destino) and not todas:
            saltadas += 1
            continue
        p = PERSONAJES[f["personaje"]]
        if p["modelo"] not in voces:
            voces[p["modelo"]] = PiperVoice.load(modelo(p["modelo"]))
        with tempfile.TemporaryDirectory() as tmp:
            crudo = os.path.join(tmp, "crudo.wav")
            with wave.open(crudo, "wb") as w:
                voces[p["modelo"]].synthesize_wav(f["hablado"], w, syn_config=SynthesisConfig(
                    length_scale=p["ritmo"], noise_w_scale=p["ruido_w"]))
            sr = wave.open(crudo).getframerate()

            # Silencios largos del principio, del final y entre frases, acortados
            limpio = os.path.join(tmp, "limpio.wav")
            subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-i", crudo, "-af",
                            "silenceremove=start_periods=1:start_threshold=-42dB:"
                            f"stop_periods=-1:stop_duration={PAUSA_MAX}:stop_silence={PAUSA_MAX}:stop_threshold=-42dB",
                            limpio], check=True)
            crudo = limpio

            # Cambio de tono sin cambiar la duración, y lo que haga falta acelerar para que quepa en la escena
            dur = duracion(crudo)
            tempo = 1.0
            if f["maximo"] > 0 and dur > f["maximo"]:
                tempo = min(ACELERAR_MAX, dur / f["maximo"])
            filtros = [f"asetrate={sr}*{p['tono']}", f"aresample=44100", f"atempo={tempo / p['tono']:.4f}"]
            if p["filtro"]:
                filtros.append(p["filtro"])
            filtros += [f"volume={p['volumen']}", "silenceremove=start_periods=1:start_threshold=-45dB",
                        "areverse", "silenceremove=start_periods=1:start_threshold=-45dB", "areverse",
                        "apad=pad_dur=0.12", "aresample=22050"]
            subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-i", crudo, "-af", ",".join(filtros),
                            "-ac", "1", "-c:a", "libvorbis", "-q:a", "4", destino], check=True)
        hechas += 1
        nota = f" (acelerada x{tempo:.2f})" if tempo > 1.0 else ""
        print(f"{f['personaje']:10s} v_{clave(f['texto']):08x}.ogg  {dur / max(tempo, 1e-6):.1f} s{nota}  {f['texto'][:60]}")
    print(f"\n{hechas} voces generadas, {saltadas} ya estaban (usa --todas para rehacerlas).")


def limpiar(frases):
    validas = {f"v_{clave(f['texto']):08x}.ogg" for f in frases}
    quitadas = 0
    for nombre in os.listdir(SALIDA):
        if nombre.startswith("v_") and nombre.endswith(".ogg") and nombre not in validas:
            os.remove(os.path.join(SALIDA, nombre))
            meta = os.path.join(SALIDA, nombre + ".meta")
            if os.path.exists(meta):
                os.remove(meta)
            quitadas += 1
    print(f"{quitadas} voces que ya no están en el guion, borradas.")


if __name__ == "__main__":
    args = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    args.add_argument("--todas", action="store_true", help="rehace todas las voces")
    args.add_argument("--limpiar", action="store_true", help="borra las voces que ya no están en el guion")
    a = args.parse_args()
    frases = leer_guion()
    claves = {}
    for f in frases:
        k = clave(f["texto"])
        if k in claves and claves[k] != f["texto"]:
            sys.exit(f"Dos frases con la misma clave {k:08x}: «{claves[k]}» y «{f['texto']}»")
        claves[k] = f["texto"]
    generar(frases, a.todas)
    if a.limpiar:
        limpiar(frases)
