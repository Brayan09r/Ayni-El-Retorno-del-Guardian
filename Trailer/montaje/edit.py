"""Montaje del tráiler: lee las tomas, compone títulos y paneles de código fotograma a fotograma y codifica con la música."""
import subprocess, sys, numpy as np, cv2
from PIL import Image, ImageDraw
import gfx
from gfx import W, H, GOLD, CREAM, GREEN, MUTED

FPS = 30
TOMAS = '/mnt/user-data/uploads/AyniElRetornoDelGuardian/Trailer/tomas/'
OUT = sys.argv[1] if len(sys.argv) > 1 else 'trailer.mp4'

# ───────────────────────── Fragmentos de código (tal cual están en el proyecto) ─────────────────────────

P_SALTO = gfx.CodePanel('YariCombatController.cs', 'HandleJump()', '''// Corriendo (sin rival fijado) el salto es largo:
// Yari no se frena, sale lanzado hacia donde corría
bool runningJump = lockTarget == null
    && Time.frameCount == paceFrame
    && paceRatio >= leapMinPace
    && lastMoveDir.sqrMagnitude > 0.5f;

velocity.y = Mathf.Sqrt(
    (runningJump ? leapHeight : jumpHeight) * -2f * gravity);

if (runningJump)
{
    leaping = true;
    leapDir = lastMoveDir.normalized;
    leapSpeed = lastMoveSpeed * leapSpeedBoost;
}''', 'La altura del salto sale de la física: v = √(2·g·h). En carrera, Yari conserva el impulso y cruza el tronco.',
    width=840, highlight=(8, 9))

P_ALDEA = gfx.CodePanel('AyniVillageBuilder.cs', 'LayoutChasquis()', '''/// K1 · Puesto de chasquis: dos casas, una colca
/// y el fogón de los mensajeros.
private static void LayoutChasquis(Compound c)
{
    float hw = c.site.HalfW, hl = c.site.HalfL;
    Gate(c, -hl, false);
    Gate(c, hl, true);

    House(c, -hw + 2.0f, 0.5f, 90f, 7.6f, 4.4f, 2.3f, 1, 0);
    House(c, hw - 2.0f, 4.2f, -90f, 6.6f, 4.4f, 2.3f, 1, 1);
    Enclosure(c, 1.9f);
    Qolqa(c, 8.8f, -6.3f, 1.6f, 2.5f, -90f);

    FirePit(c, -7.4f, -7.6f);
    Bench(c, -10.95f, -7.8f, 90f, 1.9f);
}''', 'No hay modelos importados: cada muro de pirca, techo de paja y cántaro es una malla generada por este código.',
    width=840, highlight=(9, 10))

P_RIVALES = gfx.CodePanel('AyniEncounterSite.cs', 'Update()', '''if (!Contains(player.position)) return;

PlayerEntered = true;
for (int i = 0; i < rivals.Count; i++)
{
    if (rivals[i] != null) rivals[i].SetActive(true);
}
OnPlayerEntered?.Invoke(this);''', 'Cada kancha es una sala: los rivales permanecen ocultos hasta que Yari cruza la portada.',
    width=840, highlight=(6,))

P_POSTURA = gfx.CodePanel('StructureSystem.cs', 'AddStructureDamage()', '''public void AddStructureDamage(float amount)
{
    if (IsBroken) return;

    currentStructure += amount;
    lastDamageTime = Time.time;
    OnStructureChanged?.Invoke(currentStructure, maxStructure);

    if (currentStructure >= maxStructure)
    {
        BreakStructure();
    }
}''', 'Combate de postura, como en Sifu: cada golpe la carga y, al llenarse, el rival queda a merced de Yari.',
    width=840, highlight=(9, 11))

P_JUICIO = gfx.CodePanel('AyniPurificationManager.cs', 'ResolveDilemma()', '''private void ResolveDilemma(EnemyController enemy,
                            bool isAyniMercy)
{
    if (isAyniMercy)
    {
        enemiesSpared++;
        enemy.Defeat(killed: false, reactionDelay: 0.45f);
        // Reducir el contador de muerte del talismán
        // como recompensa por restaurar el Ayni
        talisman.DecreaseDeathCounter();
    }
    else
    {
        enemiesKilled++;
        enemy.Defeat(killed: true, reactionDelay: 0.25f);
    }
    OnCombatResolved?.Invoke(enemy, isAyniMercy);
}''', 'El jugador decide. Perdonar restaura el Ayni y le devuelve a Yari años de vida; la venganza no.',
    width=840, highlight=(7, 10))

P_RENACER = gfx.CodePanel('AyniReforestationRevival.cs', 'ReviveBurntTrees()', '''foreach (AyniBurntTree tree in trees)
{
    // La ola de vida sale del lugar del perdón
    float arrives = WaveArrival(tree.transform.position);

    for (int i = 0; i < tree.ShootCount; i++)
    {
        tree.GetShoot(i, out Vector3 point,
            out Vector3 direction, out float size,
            out bool crown);
        AddRegrowth(rng,
            crown ? Kind.YoungTree : Kind.Sapling,
            model, point, direction, size, duration, delay);
    }
    if (!tree.Fallen)
        GroundLife(rng, tree.transform.position, arrives);
}''', 'Los 61 árboles quemados del camino rebrotan: copa nueva en los que siguen en pie y varas en los troncos caídos.',
    width=840, highlight=(11, 12, 13))

for p in (P_SALTO, P_ALDEA, P_RIVALES, P_POSTURA, P_JUICIO, P_RENACER):
    assert p.max_cols <= 63, p.max_cols

# ───────────────────────── Guion ─────────────────────────

SHOTS = [
    dict(src='prologo', ss=4.4, dur=3.6, hint=True, fade_in=1.2),
    dict(src='prologo', ss=12.5, dur=2.4, hint=True, trans=('dissolve', 9)),
    dict(src='prologo', ss=16.25, dur=2.4, hint=True, flash_out=7),
    dict(card='title', dur=3.6),
    dict(src='prologo', ss=35.3, dur=1.2, hint=True, trans=('dip', 9)),
    dict(src='prologo', ss=38.5, dur=1.2, hint=True),
    dict(src='salto_lado2', ss=3.25, dur=3.0, bars=True,
         lt=('NIVEL 1 · ANTISUYO', 'EL CAMINO', '630 metros de Qhapaq Ñan y ocho obstáculos')),
    dict(src='salto', ss=3.35, dur=4.2, panel=P_SALTO),
    dict(src='valle', ss=1.4, dur=2.4, bars=True, lt=('TODO GENERADO POR CÓDIGO', 'LA ALDEA INCA', 'Cuatro kanchas a caballo sobre el camino'), lt_dur=4.4),
    dict(src='aldea_k3', ss=3.1, dur=2.4, bars=True, trans=('dissolve', 9)),
    dict(src='casa_dentro', ss=1.5, dur=4.8, panel=P_ALDEA),
    dict(src='combate_k1', ss=0.5, dur=4.2, panel=P_RIVALES),
    dict(src='combate_k3', ss=4.65, dur=2.4, lt=('CUATRO TIPOS DE RIVAL', 'LA BANDA DE AMARU', 'Rastreadores, saqueadores, guardias y cazadores de élite')),
    dict(src='combate_k4', ss=15.6, dur=4.8, panel=P_POSTURA),
    dict(src='prologo', ss=40.5, dur=1.8, hint=True),
    dict(src='jefe', ss=1.6, dur=1.8),
    dict(src='jefe', ss=16.25, dur=2.4),
    dict(src='jefe', ss=18.65, dur=4.8, panel=P_JUICIO),
    dict(src='jefe', ss=25.6, dur=2.4, speed=2.0, lt=('SI YARI PERDONA', 'EL BOSQUE RENACE', None), accent=GREEN, lt_top=True),
    dict(src='renacer_o4', ss=7.9, dur=4.8, speed=1.55, panel=P_RENACER),
    dict(src='renacer_pie_O5', ss=8.8, dur=2.4, speed=1.7, bars=True, accent=GREEN,
         lt=('OBJETIVO DE DESARROLLO SOSTENIBLE 15', 'VIDA DE ECOSISTEMAS TERRESTRES', None)),
    dict(card='end', dur=4.8),
]
t = 0
for s in SHOTS:
    s['f0'] = round(t * FPS); s['n'] = round(s['dur'] * FPS); t += s['dur']
TOTAL = round(t * FPS)
print('duración', t, 's ·', TOTAL, 'fotogramas')

# ───────────────────────── Lectura de tomas ─────────────────────────

class Reader:
    def __init__(self, src, ss, frames, speed=1.0):
        vf = f'setpts=PTS/{speed},fps={FPS}' if speed != 1.0 else f'fps={FPS}'
        cmd = ['ffmpeg', '-v', 'error', '-ss', str(ss), '-i', TOMAS + src + '.mp4', '-t', str(frames / FPS * speed + 1.0),
               '-vf', vf, '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-']
        self.p = subprocess.Popen(cmd, stdout=subprocess.PIPE, bufsize=W*H*3)
        self.last = np.zeros((H, W, 3), np.uint8)
    def read(self):
        buf = self.p.stdout.read(W*H*3)
        if len(buf) == W*H*3: self.last = np.frombuffer(buf, np.uint8).reshape(H, W, 3)
        return self.last
    def close(self):
        try: self.p.kill()
        except Exception: pass

# ───────────────────────── Capas preparadas ─────────────────────────

yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
r2 = ((xx - W/2) / (W/2))**2 + ((yy - H/2) / (H/2))**2
VIGNETTE = (1 - 0.20 * np.clip(r2 - 0.25, 0, None)**1.1)[..., None].astype(np.float32)

def ease(x): x = min(1.0, max(0.0, x)); return x * x * (3 - 2 * x)
def ease_out(x): x = min(1.0, max(0.0, x)); return 1 - (1 - x)**3

def blit(frame, layer, x, y, alpha=1.0):
    """Compone una capa (rgb premultiplicado, alfa) sobre el fotograma float32, recortando a los bordes."""
    rgb, al = layer
    h, w = al.shape[:2]
    x, y = int(round(x)), int(round(y))
    x0, y0, x1, y1 = max(0, x), max(0, y), min(W, x + w), min(H, y + h)
    if x1 <= x0 or y1 <= y0 or alpha <= 0: return
    sx, sy = x0 - x, y0 - y
    a = al[sy:sy + y1 - y0, sx:sx + x1 - x0] * alpha
    frame[y0:y1, x0:x1] = frame[y0:y1, x0:x1] * (1 - a) + rgb[sy:sy + y1 - y0, sx:sx + x1 - x0] * alpha

for s in SHOTS:
    if 'lt' in s:
        s['lt_layer'] = gfx.to_np(gfx.lower_third(*s['lt'], accent=s.get('accent', GOLD)))

# Rótulos del modo "juego + código"
def label(text, color):
    im = Image.new('RGBA', (600, 44), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.rectangle([0, 14, 22, 17], fill=color + (255,))
    gfx.draw_text(d, (34, 2), text, gfx.inter(24, 'SemiBold'), color + (255,), tracking=5)
    return gfx.to_np(im)
L_GAME, L_CODE = label('EN EL JUEGO', CREAM), label('EN EL CÓDIGO', GOLD)

# Título
def sub_layers():
    full = gfx.title_layer()
    a = np.asarray(full).copy()
    parts = []
    for y0, y1 in [(0, 372), (372, 668), (668, H)]:      # chakana · AYNI · línea y subtítulo
        b = np.zeros_like(a); b[y0:y1] = a[y0:y1]
        parts.append(gfx.to_np(Image.fromarray(b)))
    return parts
T_CHAK, T_AYNI, T_SUB = sub_layers()

def end_card():
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    ch = gfx.chakana(64); im.alpha_composite(ch, (W//2 - 32, 118))
    gfx.draw_text(d, (W/2 + 14, 200), 'AYNI', gfx.lora(118), GOLD + (255,), tracking=28, anchor='m')
    gfx.draw_text(d, (W/2 + 5, 356), 'EL RETORNO DEL GUARDIÁN', gfx.display(30, 'Medium'), CREAM + (255,), tracking=11, anchor='m')
    cols = [('NIVEL 1 JUGABLE', ['Prólogo, recorrido, cuatro kanchas,', 'jefe final y dos desenlaces']),
            ('HECHO POR CÓDIGO', ['Aldea, obstáculos, bosque y escenas', 'generados en C#, sin modelos de escenario']),
            ('UNITY 6 · URP · C#', ['Combate de postura, Illa Sagrada', 'y Juicio Ayni: venganza o perdón'])]
    cw = 540; x0 = W/2 - cw * 1.5
    for i, (head, body) in enumerate(cols):
        cx = x0 + cw * i + cw / 2
        d.rectangle([cx - 24, 486, cx + 24, 489], fill=GOLD + (255,))
        gfx.draw_text(d, (cx + 3, 512), head, gfx.inter(29, 'Bold'), CREAM + (255,), tracking=5, anchor='m')
        for k, line in enumerate(body):
            gfx.draw_text(d, (cx, 568 + k * 38), line, gfx.inter(25, 'Regular'), (196, 200, 208, 255), anchor='m')
    d.line([(W/2 - 380, 760), (W/2 + 380, 760)], fill=(255, 255, 255, 40))
    gfx.draw_text(d, (W/2, 800), 'CÓDIGO FUENTE', gfx.inter(22, 'SemiBold'), GOLD + (255,), tracking=6, anchor='m')
    gfx.draw_text(d, (W/2, 842), 'github.com/Brayan09r/Ayni-El-Retorno-del-Guardian', gfx.inter(33, 'Medium'), CREAM + (255,), anchor='m')
    return gfx.to_np(im)
END = end_card()

GLOW = np.clip(1 - np.sqrt(((xx - W/2) / 900)**2 + ((yy - 470) / 560)**2), 0, 1)[..., None] ** 2 * np.array([46, 34, 12], np.float32)

# Disposición "juego + código"
WIN = (44, 268, 980, 551)          # ventana del juego: x, y, ancho, alto
PANEL_X = 1048

def split_frame(base, s, k):
    """k = 0 pantalla completa ... 1 ventana a la izquierda con fondo desenfocado."""
    small = cv2.resize(base, (160, 90), interpolation=cv2.INTER_AREA)
    small = cv2.GaussianBlur(small, (0, 0), 7)
    bg = cv2.resize(small, (W, H), interpolation=cv2.INTER_LINEAR).astype(np.float32)
    bg = bg * 0.26 + np.array([6, 8, 12], np.float32)
    x = WIN[0] * k; y = WIN[1] * k; w = W + (WIN[2] - W) * k; h = H + (WIN[3] - H) * k
    xi, yi, wi, hi = int(round(x)), int(round(y)), int(round(w)), int(round(h))
    win = cv2.resize(base, (wi, hi), interpolation=cv2.INTER_AREA)
    out = base.astype(np.float32) * (1 - k) + bg * k if k < 1 else bg
    # sombra y marco
    sm = np.zeros((H // 8, W // 8), np.float32)
    sm[max(0, (yi + 10) // 8):(yi + hi + 26) // 8, max(0, (xi - 8) // 8):(xi + wi + 8) // 8] = 1.0
    sm = cv2.resize(cv2.GaussianBlur(sm, (0, 0), 4), (W, H), interpolation=cv2.INTER_LINEAR)
    out *= (1 - 0.6 * k * sm)[..., None]
    out[max(0, yi - 2):yi + hi + 2, max(0, xi - 2):xi + wi + 2] = out[max(0, yi - 2):yi + hi + 2, max(0, xi - 2):xi + wi + 2] * (1 - 0.8 * k) + np.array(GOLD, np.float32) * 0.8 * k * 0.9
    out[yi:yi + hi, xi:xi + wi] = win
    return out

# ───────────────────────── Render ─────────────────────────

enc = subprocess.Popen(['ffmpeg', '-v', 'error', '-y', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', f'{W}x{H}', '-r', str(FPS), '-i', '-',
                        '-i', 'musica.wav', '-c:v', 'libx264', '-preset', 'slow', '-crf', '22', '-pix_fmt', 'yuv420p',
                        '-c:a', 'aac', '-b:a', '192k', '-movflags', '+faststart', '-t', str(TOTAL / FPS), OUT], stdin=subprocess.PIPE)

readers = {}
def reader(i):
    s = SHOTS[i]
    if i not in readers:
        readers[i] = Reader(s['src'], s['ss'], s['n'] + 12, s.get('speed', 1.0))
    return readers[i]

def footage(i, s):
    fr = reader(i).read()
    if s.get('hint') or s.get('bars'):
        fr = fr.copy()
        if s.get('hint'): fr[985:, 1560:] = 0          # aviso "[Tab / Esc] Saltar escena" del prólogo
        if s.get('bars'): fr[:119] = 0; fr[960:] = 0   # mismas franjas que las escenas del juego
    return fr

prev_tail = None
for f in range(TOTAL):
    i = max(k for k, s in enumerate(SHOTS) if s['f0'] <= f)
    s = SHOTS[i]; lf = f - s['f0']; n = s['n']; tsec = lf / FPS
    prev = SHOTS[i - 1] if i > 0 else None
    nxt = SHOTS[i + 1] if i + 1 < len(SHOTS) else None

    if 'card' in s:
        frame = np.zeros((H, W, 3), np.float32)
        if s['card'] == 'title':
            frame += GLOW * ease(tsec / 1.2)
            z = 1.0
            blit(frame, T_CHAK, 0, 10 * (1 - ease_out(tsec / 0.6)), ease(tsec / 0.45))
            blit(frame, T_AYNI, 0, 0, ease((tsec - 0.12) / 0.55))
            blit(frame, T_SUB, 0, 0, ease((tsec - 0.75) / 0.6))
            zoom = 1.0 + 0.035 * tsec / s['dur']
            M = np.float32([[zoom, 0, W/2 * (1 - zoom)], [0, zoom, H/2 * (1 - zoom)]])
            frame = cv2.warpAffine(frame, M, (W, H), flags=cv2.INTER_LINEAR)
            if lf < 12: frame = frame + np.array([255, 250, 236], np.float32) * (1 - lf / 12) ** 1.8    # destello de la Illa
            frame *= ease((n - lf) / 10)
        else:
            a = ease(tsec / 0.7)
            frame += GLOW * 0.7 * a
            blit(frame, END, 0, 0, a)
    else:
        base = footage(i, s)
        if lf == 0 and i - 1 in readers: pass
        tr = s.get('trans')
        if tr and tr[0] == 'dissolve' and lf < tr[1] and i - 1 in readers:
            a = (lf + 1) / (tr[1] + 1)
            base = (footage(i - 1, prev).astype(np.float32) * (1 - a) + base.astype(np.float32) * a).astype(np.uint8)

        panel = s.get('panel')
        if panel is not None:
            k_in = 1.0 if (prev is not None and prev.get('panel') is not None) else ease_out(tsec / 0.5)
            k_out = 1.0 if (nxt is not None and nxt.get('panel') is not None) else ease((n - lf) / 11)
            k = min(k_in, k_out)
            frame = split_frame(base, s, k)
            pin = ease_out((tsec - 0.12) / 0.5); pout = ease((n - lf) / 9)
            pa = min(pin, pout)
            py = WIN[1] + WIN[3] / 2 - panel.height / 2
            reveal = min(1.0, max(0.0, (tsec - 0.35) / 1.5))
            blit(frame, panel.render(reveal), PANEL_X + 70 * (1 - pin), py, pa)
            blit(frame, L_GAME, WIN[0], WIN[1] - 62, k)
            blit(frame, L_CODE, PANEL_X, py - 62, pa)
        else:
            frame = base.astype(np.float32) * VIGNETTE

        if tr and tr[0] == 'dip': frame *= ease((lf + 1) / tr[1])
        if nxt is not None and nxt.get('trans', ('',))[0] == 'dip': frame *= ease((n - lf) / nxt['trans'][1])
        if 'fade_in' in s: frame *= ease(tsec / s['fade_in'])
        if 'flash_out' in s and n - lf <= s['flash_out']:
            a = (1 - (n - lf) / s['flash_out']) ** 1.5
            frame = frame * (1 - a) + np.array([255, 250, 236], np.float32) * a

    # Rótulos de sección (pueden seguir en el plano siguiente)
    for k2 in (i, i - 1):
        if k2 < 0: continue
        q = SHOTS[k2]
        if 'lt_layer' not in q: continue
        tl = (f - q['f0']) / FPS; dur = q.get('lt_dur', q['dur'] - 0.25)
        if tl < 0.2 or tl > dur: continue
        a = min(ease((tl - 0.2) / 0.35), ease((dur - tl) / 0.3))
        hh = q['lt_layer'][1].shape[0]
        blit(frame, q['lt_layer'], 96 - gfx.LT_MARGIN - 36 * (1 - ease_out((tl - 0.2) / 0.5)), (150 - gfx.LT_MARGIN) if q.get('lt_top') else (925 - hh + gfx.LT_MARGIN), a)

    if f >= TOTAL - 30: frame *= ease((TOTAL - 1 - f) / 30)          # fundido final
    enc.stdin.write(np.clip(frame, 0, 255).astype(np.uint8).tobytes())

    # cerrar lectores que ya no hacen falta
    for k2 in list(readers):
        if k2 < i - 1: readers.pop(k2).close()
    if f % 150 == 0: print('fotograma', f, '/', TOTAL, flush=True)

enc.stdin.close(); enc.wait()
for r in readers.values(): r.close()
print('listo', OUT)
