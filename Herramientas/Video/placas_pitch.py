"""Placas (1920x1080) y subtítulos transparentes del video pitch de AYNI, con el estilo del HUD inca.

uso: python placas_pitch.py <carpeta_salida> <qr_play.png>
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

OUT, QR = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)
W, H = 1920, 1080
STONE = (21, 17, 14)
STONE2 = (34, 27, 22)
GOLD = (219, 168, 77)
BONE = (236, 226, 205)
JADE = (110, 190, 150)
GRANA = (190, 52, 40)
F = "C:/Windows/Fonts/"


def font(name, size):
    return ImageFont.truetype(F + name, size)


def spaced(text):
    return " ".join(text)


def tocapu(d, x, y, w, h, color):
    """Franja de tocapu: escalones y rombos repetidos, como en el HUD."""
    step = h * 2
    for i in range(int(w // step) + 1):
        x0 = x + i * step
        d.rectangle([x0, y, x0 + h * 0.6, y + h], fill=color)
        d.polygon([(x0 + h, y + h / 2), (x0 + h * 1.5, y), (x0 + h * 2, y + h / 2), (x0 + h * 1.5, y + h)], fill=color)


def chakana(d, cx, cy, size, color):
    u = size / 7
    for (a, b, c, e) in [(-0.5, -3.5, 0.5, 3.5), (-3.5, -0.5, 3.5, 0.5), (-1.5, -2.5, 1.5, 2.5), (-2.5, -1.5, 2.5, 1.5)]:
        d.rectangle([cx + a * u * 2, cy + b * u, cx + c * u * 2, cy + e * u], fill=color)
    d.ellipse([cx - u * 0.8, cy - u * 0.8, cx + u * 0.8, cy + u * 0.8], fill=STONE)


def background():
    im = Image.new("RGB", (W, H), STONE)
    d = ImageDraw.Draw(im)
    # Viñeta suave y chakana gigante y tenue a la derecha
    glow = Image.new("RGB", (W, H), STONE)
    gd = ImageDraw.Draw(glow)
    gd.ellipse([W * 0.15, -H * 0.4, W * 1.2, H * 1.3], fill=STONE2)
    im = Image.blend(im, glow.filter(ImageFilter.GaussianBlur(160)), 1.0)
    d = ImageDraw.Draw(im)
    chakana(d, W - 330, H / 2, 620, (40, 32, 26))
    tocapu(d, 0, 40, W, 16, (120, 92, 44))
    tocapu(d, 0, H - 56, W, 16, (120, 92, 44))
    return im, d


def wrap(d, text, fnt, width):
    lines, line = [], ""
    for word in text.split():
        test = (line + " " + word).strip()
        if d.textlength(test, font=fnt) <= width:
            line = test
        else:
            lines.append(line)
            line = word
    if line:
        lines.append(line)
    return lines


def card(name, kicker, title, bullets=None, body=None, extra=None):
    im, d = background()
    x = 150
    d.text((x, 190), spaced(kicker.upper()), font=font("corbelb.ttf", 30), fill=GOLD)
    d.line([x, 240, x + 120, 240], fill=GOLD, width=4)
    y = 270
    for line in wrap(d, title, font("cambriab.ttf", 74), 1250):
        d.text((x, y), line, font=font("cambriab.ttf", 74), fill=BONE)
        y += 88
    y += 30
    if body:
        for line in wrap(d, body, font("corbel.ttf", 40), 1180):
            d.text((x, y), line, font=font("corbel.ttf", 40), fill=BONE)
            y += 54
        y += 20
    for b in bullets or []:
        d.rectangle([x, y + 18, x + 12, y + 30], fill=GOLD)
        lines = wrap(d, b, font("corbel.ttf", 38), 1150)
        for i, line in enumerate(lines):
            d.text((x + 34, y), line, font=font("corbel.ttf", 38), fill=BONE)
            y += 50
        y += 14
    if extra:
        extra(im, d)
    im.save(os.path.join(OUT, name + ".png"))


def title_card():
    im, d = background()
    chakana(d, W / 2, 300, 230, GOLD)
    t = "AYNI"
    f = font("cambriab.ttf", 150)
    d.text(((W - d.textlength(spaced(t), font=f)) / 2, 430), spaced(t), font=f, fill=BONE)
    s = spaced("EL RETORNO DEL GUARDIÁN")
    f2 = font("corbelb.ttf", 44)
    d.text(((W - d.textlength(s, font=f2)) / 2, 610), s, font=f2, fill=GOLD)
    sub = "Videojuego 3D de acción y combate andino · Prototipo funcional v0.10.0"
    f3 = font("corbel.ttf", 36)
    d.text(((W - d.textlength(sub, font=f3)) / 2, 690), sub, font=f3, fill=BONE)
    team = "Brayan Capani Paitan · Benn Julius De la Cruz Ramos · Gilmer Antonny Penadillo Gonzales · Junior Saul Ramírez Espíritu"
    f4 = font("corbel.ttf", 28)
    d.text(((W - d.textlength(team, font=f4)) / 2, 820), team, font=f4, fill=(200, 188, 165))
    uni = "Ingeniería de Sistemas e Informática · Universidad Continental · Feria de Proyectos 2026"
    d.text(((W - d.textlength(uni, font=f4)) / 2, 866), uni, font=f4, fill=(160, 148, 128))
    im.save(os.path.join(OUT, "00_titulo.png"))


def closing_card():
    im, d = background()
    d.text((150, 190), spaced("ÚNETE AL CAMINO"), font=font("corbelb.ttf", 30), fill=GOLD)
    d.line([150, 240, 270, 240], fill=GOLD, width=4)
    d.text((150, 270), "Juega la demo del Nivel 1", font=font("cambriab.ttf", 74), fill=BONE)
    lines = ["Siguiente: niveles 2 a 5 (Canteras, Acueductos, Palacio Alquímico y el General Sayri),",
             "Yari envejece también en su aspecto y pruebas con jugadores.",
             "",
             "Hoy por ti, mañana por mí: eso es el Ayni."]
    y = 400
    for line in lines:
        d.text((150, y), line, font=font("corbel.ttf", 38) if not line.startswith("Hoy") else font("cambriai.ttf", 44),
               fill=BONE if not line.startswith("Hoy") else GOLD)
        y += 56
    qr = Image.open(QR).convert("RGB").resize((330, 330))
    frame = Image.new("RGB", (360, 360), BONE)
    frame.paste(qr, (15, 15))
    im.paste(frame, (1400, 560))
    d.text((1400, 935), "Escanea y juega en el navegador", font=font("corbel.ttf", 28), fill=BONE)
    im.save(os.path.join(OUT, "90_cierre.png"))


def lower_third(name, text, sub=None):
    """Subtítulo abajo a la izquierda (banda de piedra con filete de oro), transparente para superponer."""
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    f = font("corbelb.ttf", 40)
    f2 = font("corbel.ttf", 30)
    w = max(d.textlength(text, font=f), d.textlength(sub or "", font=f2)) + 80
    h = 110 if sub else 76
    x, y = 70, H - 150 - h
    d.rectangle([x, y, x + w, y + h], fill=(21, 17, 14, 215))
    d.rectangle([x, y, x + 8, y + h], fill=GOLD + (255,))
    d.rectangle([x, y + h - 4, x + w, y + h], fill=(120, 92, 44, 255))
    d.text((x + 34, y + 14), text, font=f, fill=BONE + (255,))
    if sub:
        d.text((x + 34, y + 62), sub, font=f2, fill=GOLD + (255,))
    im.save(os.path.join(OUT, name + ".png"))


title_card()
card("10_problema", "El problema", "Los jóvenes peruanos casi nunca juegan su propia cultura",
     bullets=["En los videojuegos que más se juegan, lo andino casi no aparece o aparece mal representado.",
              "Pocos juegos hechos en el Perú se sienten al nivel de un juego comercial.",
              "El cuidado de los ecosistemas suele llegar como un sermón, no como una experiencia."])
card("20_solucion", "Nuestra solución", "AYNI: acción al estilo Sifu en el mundo andino",
     bullets=["Yari, el último Guardián del Sol, recorre el Qhapaq Ñan peleando con un estilo inspirado en el Rumi Maki.",
              "Cada caída le cuesta años de vida: la Illa cobra su precio.",
              "Al final, el Juicio Ayni: vengarse apaga el valle; perdonar hace brotar el bosque."])
card("70_tecnologia", "Cómo está hecho", "Un prototipo funcional, hecho por estudiantes",
     bullets=["Unity 6 (URP) y C#: 91 scripts y unas 30 mil líneas de código.",
              "IA de rivales con rutinas, turnos de ataque, oleadas y un jefe en dos fases con diálogos.",
              "Modelos con Tripo3D y Blender, animaciones de Mixamo y propias; 42 sonidos creados en Python y música con Suno.",
              "Trabajo en equipo con Git y GitHub: más de 170 commits y 8 versiones publicadas."])
card("80_ods", "Impacto · ODS 15", "Vida de ecosistemas terrestres, dentro del juego",
     bullets=["Meta 15.7, contra la caza furtiva: el jefe es un cazador y los pumas no mueren; si los atacas, la manada los defiende.",
              "Meta 15.2, reforestación: si perdonas, el bosque de queñuales vuelve a crecer en tiempo real.",
              "También ODS 4 y 11: patrimonio cultural andino (kanchas, Qhapaq Ñan, fauna nativa y términos en quechua)."])
card("85_negocio", "Modelo de negocio", "Accesible para jugadores y colegios",
     bullets=["Venta en Steam e itch.io a US$ 7.99 (unos S/ 30), con demo gratuita en la web.",
              "Licencia educativa para colegios e institutos, con guía docente sobre el Ayni y el ODS 15.",
              "Costos bajos: herramientas gratuitas o educativas; publicar en Steam cuesta un pago único de US$ 100."])
closing_card()

lower_third("lt_parkour", "Parkour fluido", "Corriendo, Yari salta solo troncos y muros bajos")
lower_third("lt_fauna", "Fauna andina viva", "Llamas, alpacas, vicuñas, vizcachas y cóndores junto al camino")
lower_third("lt_rivales", "Rivales vivos, como en Sifu", "Esperan en su kancha haciendo lo suyo hasta que te descubren")
lower_third("lt_turnos", "Peleas tácticas", "Postura, bloqueo y turnos: solo dos rivales atacan a la vez")
lower_third("lt_oleada", "Oleadas de refuerzo", "Cuando cae la mitad llegan más guardias; el más duro se levanta FURIOSO")
lower_third("lt_patrulla", "El camino no está vacío", "Parejas de guardias patrullan entre kancha y kancha")
lower_third("lt_pumas", "Los pumas no mueren", "Si atacas a uno, la manada lo defiende · ODS 15")
lower_third("lt_illa", "La Illa cobra años", "Cada caída envejece a Yari")
lower_third("lt_amaru", "Amaru el Cazador", "Espera en el puente y te habla antes del duelo")
lower_third("lt_fase2", "Segunda fase", "Amaru llama a sus cazadores")
lower_third("lt_juicio", "Juicio Ayni", "Perdonar hace brotar el bosque · ODS 15")
print("placas listas")
