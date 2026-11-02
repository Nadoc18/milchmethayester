"""
Sprites du theme "Milchemet" - le style des maquettes : plaques bleu nuit a coins
chanfreines, bord fin lumineux (acier / cyan / rouge / or), halo exterieur doux,
boutons pleins bleu electrique, hexagones du menu.

Tout est dessine en sur-echantillonnage x4 puis reduit, pour des bords nets.
Les plaques et boutons sont en 9 tranches (bord 40 px sur 192 px) ; les
hexagones sont des sprites entiers (256 px), jamais etires.
"""
import os, math
from PIL import Image, ImageDraw, ImageFilter

OUT = "/mnt/user-data/outputs/mnlth/Assets/Resources/UI"
os.makedirs(OUT, exist_ok=True)
SS = 4  # sur-echantillonnage

# --- palette des maquettes ---
NAVY      = (14, 27, 54)       # fond des plaques
NAVY_DEEP = (9, 18, 38)        # fond des grands panneaux
STEEL     = (201, 214, 242)    # bord neutre (energie, rail des phases, menu)
CYAN      = (91, 200, 245)     # bord / bouton cyan (Base, bouton principal)
CYAN_DEEP = (30, 136, 216)
RED       = (255, 92, 108)     # bord rouge (Shofars, menace)
RED_DEEP  = (178, 44, 66)
GOLD      = (255, 198, 92)
GOLD_DEEP = (196, 132, 40)
WHITE     = (255, 255, 255)


def chamfer_poly(x0, y0, x1, y1, c):
    return [(x0 + c, y0), (x1 - c, y0), (x1, y0 + c), (x1, y1 - c),
            (x1 - c, y1), (x0 + c, y1), (x0, y1 - c), (x0, y0 + c)]


def glow_layer(size, poly, color, width, blur, alpha):
    layer = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.polygon(poly, outline=color + (alpha,), fill=None)
    for i in range(width):
        d.line(poly + [poly[0]], fill=color + (alpha,), width=width)
    return layer.filter(ImageFilter.GaussianBlur(blur))


def plate(name, border_color, fill=NAVY, size=192, border_px=2.5, chamfer=22,
          glow=True, fill_alpha=240, inner_line=True, filled_button=None):
    """Plaque 9 tranches. filled_button=(top,bottom) : un bouton plein en degrade."""
    S = size * SS
    m = 14 * SS                      # marge pour le halo
    c = chamfer * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))

    poly = chamfer_poly(m, m, S - m, S - m, c)

    # halo exterieur
    if glow:
        img = Image.alpha_composite(img, glow_layer((S, S), poly, border_color, int(6 * SS), 7 * SS, 110))

    body = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(body)

    if filled_button:
        top, bottom = filled_button
        # degrade vertical dans un masque chanfreine
        grad = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        gd = ImageDraw.Draw(grad)
        for y in range(m, S - m):
            t = (y - m) / float(S - 2 * m)
            col = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (255,)
            gd.line([(m, y), (S - m, y)], fill=col)
        mask = Image.new("L", (S, S), 0)
        ImageDraw.Draw(mask).polygon(poly, fill=255)
        body.paste(grad, (0, 0), mask)
        # reflet haut
        hi = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        hd = ImageDraw.Draw(hi)
        for y in range(m, m + int(26 * SS)):
            a = int(70 * (1 - (y - m) / (26.0 * SS)))
            hd.line([(m, y), (S - m, y)], fill=(255, 255, 255, a))
        hi.putalpha(Image.composite(hi.split()[3], Image.new("L", (S, S), 0), mask))
        body = Image.alpha_composite(body, hi)
    else:
        d.polygon(poly, fill=fill + (fill_alpha,))
        # leger assombrissement vers le bas (profondeur)
        sh = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        sd = ImageDraw.Draw(sh)
        for y in range(m, S - m):
            t = (y - m) / float(S - 2 * m)
            sd.line([(m, y), (S - m, y)], fill=(0, 0, 0, int(55 * t)))
        mask = Image.new("L", (S, S), 0)
        ImageDraw.Draw(mask).polygon(poly, fill=255)
        sh.putalpha(Image.composite(sh.split()[3], Image.new("L", (S, S), 0), mask))
        body = Image.alpha_composite(body, sh)

    d = ImageDraw.Draw(body)
    # bord
    d.line(poly + [poly[0]], fill=border_color + (255,), width=int(border_px * SS), joint="curve")
    # liseret interieur discret
    if inner_line:
        inner = chamfer_poly(m + int(7 * SS), m + int(7 * SS), S - m - int(7 * SS), S - m - int(7 * SS), c - int(4 * SS))
        d.line(inner + [inner[0]], fill=border_color + (55,), width=int(1.2 * SS), joint="curve")

    img = Image.alpha_composite(img, body)
    img = img.resize((size, size), Image.LANCZOS)
    img.save(os.path.join(OUT, name))
    print("ok", name)


def hexagon(name, border_color, fill=NAVY, size=256, border_px=3, glow=False,
            fill_alpha=235, filled=None, dim=False):
    """Hexagone a pointe en haut, sprite entier."""
    S = size * SS
    m = 18 * SS
    cx = cy = S / 2.0
    r = (S / 2.0) - m
    pts = [(cx + r * math.cos(math.radians(60 * i - 90)), cy + r * math.sin(math.radians(60 * i - 90))) for i in range(6)]
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))

    if glow:
        img = Image.alpha_composite(img, glow_layer((S, S), pts, border_color, int(8 * SS), 9 * SS, 150))

    body = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(body)
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).polygon(pts, fill=255)

    if filled:
        top, bottom = filled
        grad = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        gd = ImageDraw.Draw(grad)
        for y in range(int(cy - r), int(cy + r) + 1):
            t = (y - (cy - r)) / (2.0 * r)
            col = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (255,)
            gd.line([(0, y), (S, y)], fill=col)
        body.paste(grad, (0, 0), mask)
    else:
        a = 90 if dim else fill_alpha
        d.polygon(pts, fill=fill + (a,))

    d = ImageDraw.Draw(body)
    bc = border_color + (120 if dim else 255,)
    d.line(pts + [pts[0]], fill=bc, width=int(border_px * SS), joint="curve")

    img = Image.alpha_composite(img, body)
    img = img.resize((size, size), Image.LANCZOS)
    img.save(os.path.join(OUT, name))
    print("ok", name)


def title_plate(name, size_w=1024, size_h=256):
    """La plaque du titre : large, bord acier, trois noeuds hexagonaux sur le bord."""
    W, H = size_w * SS, size_h * SS
    m = 16 * SS
    c = 22 * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    poly = chamfer_poly(m, m, W - m, H - m, c)
    img = Image.alpha_composite(img, glow_layer((W, H), poly, STEEL, int(6 * SS), 8 * SS, 120))
    body = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(body)
    d.polygon(poly, fill=NAVY + (235,))
    d.line(poly + [poly[0]], fill=STEEL + (255,), width=int(3 * SS), joint="curve")

    def node(x, y, r):
        pts = [(x + r * math.cos(math.radians(60 * i - 90)), y + r * math.sin(math.radians(60 * i - 90))) for i in range(6)]
        d.polygon(pts, fill=NAVY + (255,))
        d.line(pts + [pts[0]], fill=STEEL + (255,), width=int(3 * SS), joint="curve")

    r = 24 * SS
    node(W / 2, m, r)
    node(m + 40 * SS, H - m, r)
    node(W - m - 40 * SS, H - m, r)
    img = Image.alpha_composite(img, body)
    img = img.resize((size_w, size_h), Image.LANCZOS)
    img.save(os.path.join(OUT, name))
    print("ok", name)


# ---------------- plaques (9 tranches, bord 40) ----------------
plate("ui_plate_cyan.png", CYAN)
plate("ui_plate_red.png", RED)
plate("ui_plate_gold.png", GOLD)
plate("ui_plate_steel.png", STEEL)
plate("ui_panel_dark.png", STEEL, fill=NAVY_DEEP, fill_alpha=248, border_px=3)

# ---------------- boutons ----------------
plate("ui_button_cyan.png", WHITE, filled_button=((63, 178, 245), (26, 118, 204)), border_px=3)
plate("ui_button_gold.png", WHITE, filled_button=(GOLD, GOLD_DEEP), border_px=3)
plate("ui_button_red.png", WHITE, filled_button=((255, 110, 120), RED_DEEP), border_px=3)
plate("ui_button_dark.png", STEEL, fill=NAVY, fill_alpha=245, border_px=2.5, glow=False)
plate("ui_button_dark_red.png", RED, fill=NAVY, fill_alpha=245, border_px=2.5, glow=True)

# ---------------- hexagones ----------------
hexagon("ui_hex.png", STEEL)
hexagon("ui_hex_cyan.png", (170, 230, 255), filled=((95, 200, 250), (30, 130, 215)), glow=True)
hexagon("ui_hex_red.png", (255, 160, 170), filled=((255, 110, 125), (170, 40, 60)), glow=True)
hexagon("ui_hex_dim_red.png", RED, dim=True)
hexagon("ui_hex_dim_blue.png", CYAN, dim=True)
hexagon("ui_hex_badge.png", STEEL, size=128, border_px=3)

# ---------------- titre ----------------
title_plate("ui_title_plate.png")

# ---------------- fond ----------------
def backdrop(name, w=512, h=288):
    """Le fond des ecrans : bleu nuit avec une lueur rouge a gauche, cyan a droite."""
    img = Image.new("RGB", (w, h), (10, 20, 40))
    px = img.load()
    for y in range(h):
        for x in range(w):
            fx, fy = x / float(w), y / float(h)
            dr = math.hypot(fx - 0.08, fy - 0.55)
            db = math.hypot(fx - 0.92, fy - 0.45)
            r = 10 + 70 * max(0.0, 1 - dr / 0.55) ** 2
            g = 20 + 10 * max(0.0, 1 - dr / 0.55) ** 2 + 45 * max(0.0, 1 - db / 0.55) ** 2
            b = 40 + 10 * max(0.0, 1 - dr / 0.55) ** 2 + 70 * max(0.0, 1 - db / 0.55) ** 2
            v = 1 - 0.35 * math.hypot(fx - 0.5, fy - 0.5)
            px[x, y] = (int(r * v), int(g * v), int(b * v))
    img.save(os.path.join(OUT, name))
    print("ok", name)

backdrop("ui_backdrop.png")
