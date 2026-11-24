"""Monta el video pitch de AYNI: placas + tomas grabadas en Unity + subtítulos + música, en un mp4 1080p de ≤ 5 min.

uso: python montar_pitch.py <tomas> <placas> <musica.mp3> <salida.mp4>
"""
import json
import os
import subprocess
import sys

TOMAS, PLACAS, MUSICA, SALIDA = sys.argv[1:5]
WORK = os.path.join(os.path.dirname(SALIDA), "_segmentos")
os.makedirs(WORK, exist_ok=True)
VENC = ["-c:v", "libx264", "-preset", "medium", "-crf", "18", "-pix_fmt", "yuv420p", "-r", "30"]
AENC = ["-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-ac", "2"]
music_cursor = [0.0]


def run(args):
    r = subprocess.run(["ffmpeg", "-y", "-v", "error"] + args, capture_output=True, text=True)
    if r.returncode != 0:
        raise SystemExit("ffmpeg falló:\n" + r.stderr[-3000:])


def info(path):
    r = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration:stream=codec_type",
                        "-of", "json", path], capture_output=True, text=True)
    j = json.loads(r.stdout)
    return float(j["format"]["duration"]), any(s["codec_type"] == "audio" for s in j["streams"])


def cards(name, images, seconds, music_volume=0.55):
    """
    Placas seguidas (cada una `seconds`, con su fundido a negro) sobre un tramo continuo de la música: cada placa es
    su propio segmento (encadenar varios fundidos en un solo filtro deja en negro todo lo que va tras el primero) y la
    música solo sube al principio del grupo y baja al final.
    """
    out = []
    for i, img in enumerate(images):
        start = music_cursor[0]
        music_cursor[0] += seconds
        path = os.path.join(WORK, f"{name}_{i}.mp4")
        audio = f"[1:a]volume={music_volume}"
        if i == 0:
            audio += ",afade=in:st=0:d=0.8"
        if i == len(images) - 1:
            audio += f",afade=out:st={seconds - 1.0}:d=1.0"
        run(["-loop", "1", "-t", str(seconds), "-i", os.path.join(PLACAS, img), "-ss", str(start), "-t", str(seconds), "-i", MUSICA,
             "-filter_complex", f"[0:v]scale=1920:1080,fps=30,format=yuv420p,fade=in:st=0:d=0.4,fade=out:st={seconds - 0.4}:d=0.4[v];"
                                f"{audio}[a]",
             "-map", "[v]", "-map", "[a]", "-t", str(seconds)] + VENC + AENC + [path])
        out.append(path)
    return out


def clip(name, src, start=0.0, length=None, captions=(), fade=0.3):
    """Toma recortada con subtítulos (png transparente, de t1 a t2 dentro del recorte) y fundidos."""
    path = os.path.join(TOMAS, src + ".mp4")
    dur, has_audio = info(path)
    length = min(length or dur - start, dur - start)
    inputs = ["-ss", str(start), "-t", str(length), "-i", path]
    if not has_audio:
        inputs += ["-f", "lavfi", "-t", str(length), "-i", "anullsrc=r=48000:cl=stereo"]
    graph = [f"[0:v]scale=1920:1080,fps=30,format=yuv420p[v0]"]
    last = "v0"
    base = 2 if not has_audio else 1
    for i, (png, t1, t2) in enumerate(captions):
        t2 = min(t2, length - 0.2)
        if t1 >= t2:
            continue
        inputs += ["-loop", "1", "-t", str(length), "-i", os.path.join(PLACAS, png + ".png")]
        k = base + i
        graph.append(f"[{k}:v]format=rgba,fade=in:st={t1}:d=0.35:alpha=1,fade=out:st={t2 - 0.35}:d=0.35:alpha=1[l{i}]")
        graph.append(f"[{last}][l{i}]overlay=0:0:shortest=1[o{i}]")
        last = f"o{i}"
    graph.append(f"[{last}]fade=in:st=0:d={fade},fade=out:st={length - fade}:d={fade}[v]")
    a_in = "1:a" if not has_audio else "0:a"
    graph.append(f"[{a_in}]aresample=48000,aformat=channel_layouts=stereo,afade=in:st=0:d={fade},afade=out:st={length - fade}:d={fade}[a]")
    out = os.path.join(WORK, name + ".mp4")
    run(inputs + ["-filter_complex", ";".join(graph), "-map", "[v]", "-map", "[a]", "-t", str(length)] + VENC + AENC + [out])
    return out


def length_of(src):
    return info(os.path.join(TOMAS, src + ".mp4"))[0]


segments = []
segments.extend(cards("s00_titulo", ["00_titulo.png"], 6))
pro = length_of("prologo")
segments.append(clip("s01_prologo", "prologo", 1.0, min(40.0, pro - 1.0)))
segments.extend(cards("s02_problema", ["10_problema.png", "20_solucion.png"], 8))
segments.append(clip("s03_camino", "pitch_camino", 0.3, 5.7, [("lt_parkour", 0.4, 5.4)]))
segments.append(clip("s04_llamas", "pitch_llamas", 0.0, None, [("lt_fauna", 0.5, 6.4)]))
k = length_of("pitch_kancha")
segments.append(clip("s05_kancha", "pitch_kancha", 3.0, min(50.0, k - 3.0),
                     [("lt_rivales", 0.4, 5.6), ("lt_turnos", 6.2, 10.4), ("lt_oleada", 11.4, 16.4)]))
segments.append(clip("s06_patrulla", "pitch_patrulla", 0.0, 7.2, [("lt_patrulla", 0.6, 6.6)]))
segments.append(clip("s07_pumas", "pitch_pumas", 0.0, 10.5, [("lt_pumas", 2.5, 9.0)]))
segments.append(clip("s08_illa", "pitch_illa", 0.3, 4.8, [("lt_illa", 0.4, 4.5)]))
segments.append(clip("s09_espera", "pitch_jefe_espera", 0.0, None, [("lt_amaru", 0.6, 5.6)]))
j = length_of("pitch_jefe")
# Toma de Amaru: llega Yari (0-9 s), presentación (9-18), duelo, "SEGUNDA FASE" (~27), cazadores, Juicio (~43),
# perdón y bosque que renace (~48), ODS 15 (~57) y "Nivel 1 completado" (~60)
segments.append(clip("s10_jefe", "pitch_jefe", 2.0, min(61.0, j - 2.0),
                     [("lt_fase2", 31.5, 37.0), ("lt_juicio", 46.0, 53.5)]))
segments.extend(cards("s11_cierre", ["70_tecnologia.png", "80_ods.png", "85_negocio.png", "90_cierre.png"], 8))

listing = os.path.join(WORK, "final.txt")
with open(listing, "w", encoding="utf-8") as f:
    for s in segments:
        f.write(f"file '{s.replace(os.sep, '/')}'\n")
run(["-f", "concat", "-safe", "0", "-i", listing, "-c", "copy", "-movflags", "+faststart", SALIDA])
total, _ = info(SALIDA)
print(f"video listo: {SALIDA} ({total / 60:.0f}:{total % 60:04.1f})")
