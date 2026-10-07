"""Gráficos del tráiler: tipografía, chakana, títulos y paneles de código (todo en capas RGBA)."""
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
from pygments.lexers import CSharpLexer
from pygments.token import Token

W, H = 1920, 1080
GOLD = (246, 196, 83); CREAM = (244, 238, 221); GREEN = (132, 214, 150); RED = (232, 96, 80)
INK = (10, 12, 15); MUTED = (150, 158, 170)
LT_MARGIN = 70

F_LORA = '/usr/share/fonts/truetype/google-fonts/Lora-Variable.ttf'
F_INTER = '/usr/share/fonts/opentype/inter/Inter-%s.otf'
F_DISPLAY = '/usr/share/fonts/opentype/inter/InterDisplay-%s.otf'
F_MONO = '/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf'
F_MONO_B = '/usr/share/fonts/truetype/dejavu/DejaVuSansMono-Bold.ttf'

def lora(size, weight='Bold'):
    f = ImageFont.truetype(F_LORA, size)
    try: f.set_variation_by_name(weight)
    except Exception: pass
    return f
def inter(size, weight='SemiBold'): return ImageFont.truetype(F_INTER % weight, size)
def display(size, weight='Bold'): return ImageFont.truetype(F_DISPLAY % weight, size)
def mono(size, bold=False): return ImageFont.truetype(F_MONO_B if bold else F_MONO, size)

def text_size(text, font, tracking=0.0):
    if not tracking:
        b = font.getbbox(text); return b[2], font.size
    return int(sum(font.getlength(c) + tracking for c in text) - tracking), font.size

def draw_text(draw, xy, text, font, fill, tracking=0.0, anchor='l'):
    """Texto con interletraje; anchor l / m / r respecto a x."""
    w, _ = text_size(text, font, tracking)
    x, y = xy
    if anchor == 'm': x -= w / 2
    elif anchor == 'r': x -= w
    if not tracking:
        draw.text((x, y), text, font=font, fill=fill); return w
    for c in text:
        draw.text((x, y), c, font=font, fill=fill)
        x += font.getlength(c) + tracking
    return w

def with_shadow(layer, radius=10, opacity=0.75, offset=(0, 3)):
    """Sombra suave bajo una capa RGBA (para que el texto se lea sobre el juego)."""
    a = layer.split()[3]
    sh = Image.new('RGBA', layer.size, (0, 0, 0, 0))
    sh.paste((0, 0, 0, 255), offset, a)
    sh = sh.filter(ImageFilter.GaussianBlur(radius))
    r, g, b, sa = sh.split()
    sa = sa.point(lambda v: int(min(255, v * opacity * 1.6)))
    sh = Image.merge('RGBA', (r, g, b, sa))
    return Image.alpha_composite(sh, layer)

def chakana(size, color=GOLD, hole=True):
    """Cruz andina escalonada."""
    s = size * 4; u = s / 6.6; c = s / 2
    pts = [(-1,-3),(1,-3),(1,-2),(2,-2),(2,-1),(3,-1),(3,1),(2,1),(2,2),(1,2),(1,3),(-1,3),(-1,2),(-2,2),(-2,1),(-3,1),(-3,-1),(-2,-1),(-2,-2),(-1,-2)]
    im = Image.new('RGBA', (s, s), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.polygon([(c + x*u, c + y*u) for x, y in pts], fill=color + (255,))
    if hole:
        r = u * 0.95
        d.ellipse([c - r, c - r, c + r, c + r], fill=(0, 0, 0, 0))
    return im.resize((size, size), Image.LANCZOS)

def to_np(layer):
    """Capa RGBA de PIL -> (rgb premultiplicado float32, alfa float32)."""
    a = np.asarray(layer, dtype=np.float32)
    al = a[..., 3:4] / 255.0
    return a[..., :3] * al, al

# ───────────────────────── Títulos ─────────────────────────

def title_layer(small=False):
    """Logotipo: chakana, AYNI y el subtítulo. Devuelve capa RGBA a pantalla completa."""
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    cy = 300 if not small else 250
    ch = chakana(120 if not small else 92)
    im.alpha_composite(ch, (W//2 - ch.width//2, cy - ch.height//2))
    f = lora(230 if not small else 170)
    draw_text(d, (W/2 + 22, cy + 70), 'AYNI', f, GOLD + (255,), tracking=44, anchor='m')
    y2 = cy + 70 + (300 if not small else 225)
    lw = 250
    d.line([(W/2 - lw, y2), (W/2 + lw, y2)], fill=GOLD + (170,), width=2)
    draw_text(d, (W/2 + 7, y2 + 26), 'EL RETORNO DEL GUARDIÁN', display(46 if not small else 38, 'Medium'), CREAM + (255,), tracking=15, anchor='m')
    return im

def lower_third(kicker, title, sub=None, accent=GOLD):
    """Rótulo de sección abajo a la izquierda."""
    fk, ft, fs = inter(25, 'SemiBold'), display(74, 'ExtraBold'), inter(31, 'Medium')
    wk, _ = text_size(kicker, fk, 6); wt, _ = text_size(title, ft, 2)
    ws = text_size(sub, fs)[0] if sub else 0
    w = max(wk, wt, ws) + 90; h = 210 if sub else 160
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.rectangle([0, 8, 7, h - 14], fill=accent + (255,))
    draw_text(d, (34, 4), kicker, fk, accent + (255,), tracking=6)
    draw_text(d, (32, 38), title, ft, CREAM + (255,), tracking=2)
    if sub: draw_text(d, (34, 138), sub, fs, CREAM + (245,))
    im = with_shadow(im, 12, 0.95)
    # Velo oscuro difuminado detrás, para que se lea sobre piedra clara o cielo
    M = LT_MARGIN
    veil = Image.new('RGBA', (w + 2*M, h + 2*M), (0, 0, 0, 0))
    ImageDraw.Draw(veil).rounded_rectangle([M - 30, M - 16, M + w - 20, M + h - 6], 30, fill=(0, 0, 0, 150))
    veil = veil.filter(ImageFilter.GaussianBlur(34))
    veil.alpha_composite(im, (M, M))
    return veil

def center_text(lines, sizes=None, colors=None, weights=None, tracking=None, gap=18):
    """Bloque de texto centrado (para la pregunta del Juicio, el ODS, la tarjeta final...)."""
    n = len(lines)
    sizes = sizes or [64]*n; colors = colors or [CREAM]*n; weights = weights or ['Bold']*n; tracking = tracking or [2]*n
    fonts = [display(s, w) for s, w in zip(sizes, weights)]
    ws = [text_size(t, f, tr)[0] for t, f, tr in zip(lines, fonts, tracking)]
    w = max(ws) + 80; h = sum(int(s*1.22) for s in sizes) + gap*(n-1) + 40
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    y = 16
    for t, f, c, tr, s in zip(lines, fonts, colors, tracking, sizes):
        draw_text(d, (w/2, y), t, f, c + (255,), tracking=tr, anchor='m'); y += int(s*1.22) + gap
    return with_shadow(im, 16, 0.9)

# ───────────────────────── Panel de código ─────────────────────────

SYNTAX = {
    Token.Keyword: (198, 146, 233), Token.Keyword.Type: (130, 200, 255), Token.Name.Class: (255, 203, 107),
    Token.Name.Function: (130, 170, 255), Token.Literal.Number: (247, 140, 108), Token.Literal.String: (195, 232, 141),
    Token.Comment: (112, 128, 150), Token.Operator: (137, 221, 255), Token.Punctuation: (190, 200, 215),
    Token.Name: (224, 228, 236),
}
KNOWN_TYPES = {'Mathf', 'Vector3', 'Vector2', 'Time', 'Kind', 'EnemyController', 'AyniBurntTree', 'Compound', 'Debug', 'GameObject'}

def color_of(tok, value):
    if value in KNOWN_TYPES: return SYNTAX[Token.Name.Class]
    t = tok
    while t is not None:
        if t in SYNTAX: return SYNTAX[t]
        t = t.parent
    return SYNTAX[Token.Name]

class CodePanel:
    """Panel con el fragmento de código: fondo, cabecera con el archivo, líneas que van apareciendo y una explicación."""
    def __init__(self, filename, where, code, caption, width=800, font_size=19, highlight=()):
        self.width = width
        fm = mono(font_size); lh = int(font_size * 1.52)
        lines = code.strip('\n').split('\n')
        pad = 30; head = 62; num_w = 44
        cap_f = inter(26, 'Medium'); cap_lines = self._wrap(caption, cap_f, width - 2*pad - 16)
        cap_h = len(cap_lines) * 36 + 34
        self.height = head + 18 + len(lines) * lh + 20 + cap_h
        # Fondo y cabecera
        bg = Image.new('RGBA', (width, self.height), (0, 0, 0, 0)); d = ImageDraw.Draw(bg)
        d.rounded_rectangle([0, 0, width - 1, self.height - 1], 18, fill=(16, 19, 26, 240), outline=(255, 255, 255, 38), width=1)
        d.rounded_rectangle([0, 0, width - 1, head], 18, fill=(26, 30, 40, 250))
        d.rectangle([0, head - 18, width - 1, head], fill=(26, 30, 40, 250))
        d.line([(0, head), (width, head)], fill=(255, 255, 255, 30))
        for i, c in enumerate([(255, 95, 86), (255, 189, 46), (39, 201, 63)]):
            d.ellipse([pad - 8 + i*24, 24, pad + 6 + i*24, 38], fill=c + (255,))
        draw_text(d, (pad + 84, 17), filename, inter(24, 'SemiBold'), CREAM + (255,))
        fw = text_size(filename, inter(24, 'SemiBold'))[0]
        draw_text(d, (pad + 84 + fw + 16, 21), '·  ' + where, inter(21, 'Medium'), MUTED + (255,))
        draw_text(d, (width - pad, 20), 'C#', inter(21, 'Bold'), GOLD + (255,), anchor='r')
        # Explicación
        y0 = head + 18 + len(lines) * lh + 20
        d.line([(pad, y0), (width - pad, y0)], fill=(255, 255, 255, 28))
        d.rectangle([pad, y0 + 20, pad + 4, y0 + 14 + len(cap_lines)*36], fill=GOLD + (255,))
        for i, t in enumerate(cap_lines):
            draw_text(d, (pad + 20, y0 + 16 + i*36), t, cap_f, CREAM + (240,))
        self.bg = to_np(bg)
        # Líneas
        self.lines = []
        for i, line in enumerate(lines):
            strip = Image.new('RGBA', (width, lh), (0, 0, 0, 0)); ds = ImageDraw.Draw(strip)
            if (i + 1) in highlight:
                ds.rectangle([8, 0, width - 8, lh], fill=(246, 196, 83, 34)); ds.rectangle([8, 0, 11, lh], fill=GOLD + (255,))
            draw_text(ds, (pad + num_w - 14, 4), str(i + 1), fm, (92, 102, 120, 255), anchor='r')
            x = pad + num_w
            for tok, val in CSharpLexer().get_tokens(line):
                val = val.rstrip('\n')
                if not val: continue
                ds.text((x, 4), val, font=fm, fill=color_of(tok, val) + (255,))
                x += fm.getlength(val)
            self.lines.append((head + 18 + i*lh, to_np(strip)))
        self.max_cols = max(len(l) for l in lines)

    @staticmethod
    def _wrap(text, font, maxw):
        out, cur = [], ''
        for word in text.split():
            t = (cur + ' ' + word).strip()
            if font.getlength(t) <= maxw: cur = t
            else: out.append(cur); cur = word
        if cur: out.append(cur)
        return out

    def render(self, reveal):
        """reveal: 0..1 -> las líneas aparecen una tras otra. Devuelve (rgb premultiplicado, alfa)."""
        rgb, al = self.bg[0].copy(), self.bg[1].copy()
        n = len(self.lines)
        for i, (y, (lrgb, lal)) in enumerate(self.lines):
            a = np.clip(reveal * (n + 2) - i, 0, 1)
            if a <= 0: continue
            h = lrgb.shape[0]
            la = lal * a
            rgb[y:y+h] = rgb[y:y+h] * (1 - la) + lrgb * a
            al[y:y+h] = al[y:y+h] * (1 - la) + la
        return rgb, al
