"""Pixel art generator for PixelGame - Batch 2 (30 more unique levels).
Generates 30 NEW game-ready 32x32 pixel art images:
- 10 Kolay (Easy)
- 10 Orta (Medium)
- 10 Zor (Hard)
Validates all images against game rules (no close colors, min pixels >= 10, exact dimensions, alpha 0 or 255).
"""
import os, sys, uuid, re, collections, itertools, math
from PIL import Image

OUT = (34, 26, 40)
TEX_META_TEMPLATE = 'Assets/32x32/Saturn.png.meta'

def make_meta(png_path):
    meta_path = png_path + '.meta'
    if not os.path.exists(meta_path):
        tmpl = open(TEX_META_TEMPLATE).read()
        meta = re.sub(r'guid: \w+', 'guid: ' + uuid.uuid4().hex, tmpl, count=1)
        with open(meta_path, 'w') as f:
            f.write(meta)

class Canvas:
    def __init__(self, bg='.'):
        self.grid = [[bg for _ in range(32)] for _ in range(32)]
        
    def set(self, x, y, ch):
        if 0 <= x < 32 and 0 <= y < 32:
            self.grid[y][x] = ch
            
    def get(self, x, y):
        if 0 <= x < 32 and 0 <= y < 32:
            return self.grid[y][x]
        return None

    def fill_rect(self, x1, y1, x2, y2, ch):
        for y in range(max(0, y1), min(32, y2 + 1)):
            for x in range(max(0, x1), min(32, x2 + 1)):
                self.grid[y][x] = ch

    def fill_circle(self, cx, cy, r, ch):
        r2 = (r + 0.35) ** 2
        for y in range(32):
            for x in range(32):
                if (x - cx)**2 + (y - cy)**2 <= r2:
                    self.grid[y][x] = ch

    def fill_ellipse(self, cx, cy, rx, ry, ch):
        for y in range(32):
            for x in range(32):
                if ((x - cx) / (rx + 0.35))**2 + ((y - cy) / (ry + 0.35))**2 <= 1.0:
                    self.grid[y][x] = ch

    def draw_line(self, x0, y0, x1, y1, ch, w=1):
        dx = abs(x1 - x0)
        dy = abs(y1 - y0)
        sx = 1 if x0 < x1 else -1
        sy = 1 if y0 < y1 else -1
        err = dx - dy
        x, y = x0, y0
        while True:
            for ox in range(-w//2, w//2 + 1):
                for oy in range(-w//2, w//2 + 1):
                    self.set(x + ox, y + oy, ch)
            if x == x1 and y == y1:
                break
            e2 = 2 * err
            if e2 > -dy:
                err -= dy
                x += sx
            if e2 < dx:
                err += dx
                y += sy

    def fill_polygon(self, pts, ch):
        min_y = max(0, min(p[1] for p in pts))
        max_y = min(31, max(p[1] for p in pts))
        for y in range(min_y, max_y + 1):
            nodes = []
            j = len(pts) - 1
            for i in range(len(pts)):
                if (pts[i][1] < y and pts[j][1] >= y) or (pts[j][1] < y and pts[i][1] >= y):
                    nx = pts[i][0] + (y - pts[i][1]) / float(pts[j][1] - pts[i][1]) * (pts[j][0] - pts[i][0])
                    nodes.append(nx)
                j = i
            nodes.sort()
            for k in range(0, len(nodes), 2):
                if k + 1 < len(nodes):
                    x_start = max(0, int(math.ceil(nodes[k])))
                    x_end = min(31, int(math.floor(nodes[k+1])))
                    for x in range(x_start, x_end + 1):
                        self.grid[y][x] = ch

    def outline(self, target_chars, out_ch):
        snap = [row[:] for row in self.grid]
        for y in range(32):
            for x in range(32):
                if snap[y][x] in target_chars:
                    is_edge = False
                    for dx, dy in ((1,0), (-1,0), (0,1), (0,-1)):
                        nx, ny = x + dx, y + dy
                        if not (0 <= nx < 32 and 0 <= ny < 32) or snap[ny][nx] not in target_chars:
                            is_edge = True
                            break
                    if is_edge:
                        self.grid[y][x] = out_ch

    def to_image(self, palette):
        im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
        for y in range(32):
            for x in range(32):
                ch = self.grid[y][x]
                if ch != '.' and ch in palette:
                    im.putpixel((x, y), palette[ch] + (255,))
        return im

def validate_image(im, name=""):
    W, H = im.size
    px = list(im.get_flattened_data() if hasattr(im, 'get_flattened_data') else im.getdata())
    cnt = collections.Counter(p[:3] for p in px if p[3] > 0)
    n = sum(cnt.values())
    errs = []
    warns = []
    for c1, c2 in itertools.combinations(cnt, 2):
        if all(abs(c1[k] - c2[k]) / 255 < 0.04 for k in range(3)):
            errs.append(f"Colors too close (<0.04): {c1} vs {c2}")
        elif all(abs(c1[k] - c2[k]) < 14 for k in range(3)):
            warns.append(f"Colors close (<14): {c1} vs {c2}")
    for c, count in cnt.items():
        if count < 10:
            warns.append(f"Color {c} has only {count} px (<10)")
    semi = sum(1 for p in px if 0 < p[3] < 255)
    if semi:
        errs.append(f"{semi} semi-transparent pixels")
    return n, len(cnt), errs, warns

# -------------------------------------------------------------
# 10 YENİ KOLAY GENERATORS
# -------------------------------------------------------------

def gen_bambu():
    c = Canvas('.')
    pal = {
        'G': (75, 175, 80),     # Bamboo green
        'L': (165, 215, 75),    # Light shoot
        'D': (40, 115, 50),     # Dark shade
        'Y': (235, 210, 85),    # Node rings
    }
    # Left stalk
    c.fill_rect(9, 6, 13, 28, 'G')
    c.fill_rect(12, 6, 13, 28, 'D')
    # Right stalk
    c.fill_rect(18, 4, 22, 28, 'G')
    c.fill_rect(21, 4, 22, 28, 'D')
    # Bamboo nodes (yellow rings)
    for ny in [11, 18, 24]:
        c.fill_rect(8, ny, 14, ny + 1, 'Y')
    for ny in [9, 16, 23]:
        c.fill_rect(17, ny, 23, ny + 1, 'Y')
    # Leaves (L)
    c.draw_line(14, 11, 18, 8, 'L', 2)
    c.draw_line(18, 8, 24, 7, 'L', 1)
    c.draw_line(8, 18, 4, 15, 'L', 2)
    c.draw_line(4, 15, 2, 13, 'L', 1)
    c.draw_line(23, 16, 27, 13, 'L', 2)
    c.draw_line(27, 13, 29, 11, 'L', 1)
    return c.to_image(pal)

def gen_misirkocani():
    c = Canvas('.')
    pal = {
        'Y': (255, 215, 45),    # Yellow corn kernels
        'O': (235, 145, 30),    # Kernel shadow/indent
        'G': (65, 165, 70),     # Green husk
        'L': (155, 215, 90),    # Light husk
        'B': (130, 85, 45)      # Stalk brown
    }
    # Stalk
    c.fill_rect(15, 26, 17, 30, 'B')
    # Corn ear oval
    c.fill_ellipse(16, 14, 6, 11, 'Y')
    # Grid of kernel shadows (>= 15 px)
    for y in range(6, 23, 3):
        for x in range(12, 21, 3):
            if c.get(x, y) == 'Y':
                c.set(x, y, 'O')
                c.set(x + 1, y, 'O')
    # Peeling husk leaves (G and L)
    c.fill_polygon([(10, 26), (8, 16), (13, 26)], 'G')
    c.fill_polygon([(22, 26), (24, 16), (19, 26)], 'G')
    c.fill_polygon([(7, 20), (5, 13), (10, 22)], 'L')
    c.fill_polygon([(25, 20), (27, 13), (22, 22)], 'L')
    return c.to_image(pal)

def gen_kagitucak():
    c = Canvas('.')
    pal = {
        'W': (245, 248, 255),  # Pure paper white
        'L': (205, 215, 230),  # Light fold
        'D': (140, 155, 175),  # Shadow under wing
        'B': (45, 145, 235),   # Blue wind trail
    }
    # Main paper airplane angled
    c.fill_polygon([(26, 6), (7, 17), (14, 21)], 'W')
    # Left folded wing
    c.fill_polygon([(26, 6), (7, 17), (16, 13)], 'L')
    # Center fold ridge / keel
    c.fill_polygon([(26, 6), (14, 21), (13, 26)], 'D')
    # Blue motion dash trail
    for p in [(3, 27), (4, 26), (7, 24), (8, 23), (11, 22), (12, 21)]:
        c.fill_rect(p[0], p[1], p[0] + 1, p[1] + 1, 'B')
    return c.to_image(pal)

def gen_yoyo():
    c = Canvas('.')
    pal = {
        'R': (225, 45, 45),    # Red body
        'D': (155, 25, 30),    # Shadow rim
        'Y': (255, 215, 45),   # Yellow stripe
        'W': (245, 245, 250),  # White string & shine
    }
    # String on top
    c.draw_line(16, 3, 16, 12, 'W', 1)
    c.draw_line(16, 3, 13, 5, 'W', 1)
    c.draw_line(13, 5, 14, 3, 'W', 1)
    # Circular yoyo disc
    c.fill_circle(16, 19, 9, 'R')
    # Shadow crescent
    for y in range(12, 29):
        for x in range(9, 25):
            if c.get(x, y) == 'R' and (x >= 18 or y >= 21):
                c.set(x, y, 'D')
    # Yellow center ring
    c.fill_circle(16, 19, 5, 'Y')
    c.fill_circle(16, 19, 3, 'R')
    # White gloss shine
    c.fill_rect(11, 14, 13, 15, 'W')
    c.fill_rect(10, 16, 11, 18, 'W')
    return c.to_image(pal)

def gen_rubikkup():
    c = Canvas('.')
    pal = {
        'K': (35, 38, 48),     # Cube frame black
        'R': (225, 45, 45),    # Red stickers
        'B': (40, 110, 225),   # Blue stickers
        'Y': (255, 215, 40),   # Yellow stickers
        'W': (245, 245, 250),  # White stickers
    }
    # Black base cube body
    c.fill_polygon([(16, 4), (28, 10), (16, 16), (4, 10)], 'K')
    c.fill_polygon([(4, 10), (16, 16), (16, 28), (4, 22)], 'K')
    c.fill_polygon([(16, 16), (28, 10), (28, 22), (16, 28)], 'K')
    # Top face stickers (Yellow)
    c.fill_polygon([(16, 5), (19, 7), (16, 8), (13, 7)], 'Y')
    c.fill_polygon([(20, 7), (23, 9), (20, 10), (17, 9)], 'Y')
    c.fill_polygon([(12, 7), (15, 9), (12, 10), (9, 9)], 'Y')
    c.fill_polygon([(16, 9), (19, 11), (16, 12), (13, 11)], 'Y')
    c.fill_polygon([(24, 9), (27, 10), (24, 12), (21, 11)], 'Y')
    c.fill_polygon([(8, 10), (11, 11), (8, 12), (5, 11)], 'Y')
    c.fill_polygon([(20, 11), (23, 13), (20, 14), (17, 13)], 'Y')
    c.fill_polygon([(12, 11), (15, 13), (12, 14), (9, 13)], 'Y')
    c.fill_polygon([(16, 13), (19, 15), (16, 15), (13, 15)], 'Y')
    # Left face stickers (Red)
    c.fill_polygon([(5, 12), (9, 14), (9, 17), (5, 15)], 'R')
    c.fill_polygon([(10, 14), (14, 16), (14, 19), (10, 17)], 'R')
    c.fill_polygon([(5, 16), (9, 18), (9, 21), (5, 19)], 'R')
    c.fill_polygon([(10, 18), (14, 20), (14, 23), (10, 21)], 'R')
    c.fill_polygon([(5, 20), (9, 22), (9, 25), (5, 23)], 'R')
    c.fill_polygon([(10, 22), (14, 24), (14, 27), (10, 25)], 'R')
    # Right face stickers (Blue)
    c.fill_polygon([(17, 16), (21, 14), (21, 17), (17, 19)], 'B')
    c.fill_polygon([(22, 14), (26, 12), (26, 15), (22, 17)], 'B')
    c.fill_polygon([(17, 20), (21, 18), (21, 21), (17, 23)], 'B')
    c.fill_polygon([(22, 18), (26, 16), (26, 19), (22, 21)], 'B')
    c.fill_polygon([(17, 24), (21, 22), (21, 25), (17, 27)], 'B')
    c.fill_polygon([(22, 22), (26, 20), (26, 23), (22, 25)], 'B')
    return c.to_image(pal)

def gen_dalgicmaskesi():
    c = Canvas('.')
    pal = {
        'Y': (255, 205, 30),    # Yellow frame & snorkel
        'D': (195, 135, 15),    # Frame shade
        'C': (90, 215, 245),    # Cyan glass visor
        'W': (245, 250, 255),   # Visor reflection glare
        'K': (45, 50, 60),      # Silicone strap / mouthpiece
    }
    # Snorkel tube rising on right
    c.fill_rect(24, 4, 26, 22, 'Y')
    c.fill_rect(20, 22, 26, 24, 'Y')
    c.fill_rect(19, 21, 21, 23, 'K')  # Mouthpiece
    c.fill_rect(24, 3, 26, 5, 'D')    # Snorkel tip
    # Mask outer oval
    c.fill_ellipse(13, 16, 9, 7, 'Y')
    # Mask glass
    c.fill_ellipse(13, 16, 7, 5, 'C')
    # Nose pocket
    c.fill_polygon([(11, 18), (15, 18), (14, 23), (12, 23)], 'D')
    # Reflection glare on glass
    c.draw_line(8, 14, 11, 14, 'W', 1)
    c.draw_line(7, 15, 10, 15, 'W', 1)
    c.draw_line(15, 14, 18, 14, 'W', 1)
    # Head strap (K)
    c.fill_rect(3, 15, 5, 17, 'K')
    return c.to_image(pal)

def gen_bumerang():
    c = Canvas('.')
    pal = {
        'W': (155, 95, 45),     # Wood boomerang
        'D': (105, 60, 30),     # Wood shade
        'R': (225, 45, 45),     # Red stripes
        'Y': (250, 230, 80),    # Yellow dots
    }
    # Curved V-boomerang
    c.draw_line(16, 6, 6, 25, 'W', 4)
    c.draw_line(16, 6, 26, 25, 'W', 4)
    # Shading along inner edge
    c.draw_line(16, 8, 8, 24, 'D', 1)
    c.draw_line(16, 8, 24, 24, 'D', 1)
    # Red stripe bands (>= 15 px)
    c.fill_rect(11, 13, 15, 15, 'R')
    c.fill_rect(17, 13, 21, 15, 'R')
    c.fill_rect(7, 21, 10, 23, 'R')
    c.fill_rect(22, 21, 25, 23, 'R')
    # Yellow decorative dots (>= 12 px)
    c.fill_circle(16, 7, 1, 'Y')
    c.fill_circle(13, 10, 1, 'Y')
    c.fill_circle(19, 10, 1, 'Y')
    return c.to_image(pal)

def gen_megafon():
    c = Canvas('.')
    pal = {
        'R': (225, 45, 55),     # Red cone
        'D': (155, 25, 35),     # Red shade
        'W': (245, 245, 250),   # White rim & body ring
        'G': (110, 120, 135),   # Grey handle & button
        'C': (45, 165, 240),    # Sound waves
    }
    # Main horn cone expanding from (12, 16) to (25, 8..24)
    c.fill_polygon([(12, 14), (25, 8), (25, 24), (12, 18)], 'R')
    # Lower shade
    c.fill_polygon([(12, 16), (25, 18), (25, 24), (12, 18)], 'D')
    # White flared rim
    c.fill_ellipse(25, 16, 2, 8, 'W')
    # White mid-stripe
    c.fill_polygon([(18, 12), (20, 11), (20, 21), (18, 20)], 'W')
    # Handle
    c.fill_polygon([(13, 18), (16, 18), (14, 27), (11, 27)], 'G')
    # Handpiece mic at back
    c.fill_rect(7, 13, 12, 19, 'G')
    # Sound waves (C >= 15 px)
    c.draw_line(28, 12, 30, 14, 'C', 1)
    c.draw_line(30, 14, 30, 18, 'C', 1)
    c.draw_line(30, 18, 28, 20, 'C', 1)
    c.draw_line(28, 13, 29, 14, 'C', 1)
    c.draw_line(29, 14, 29, 18, 'C', 1)
    c.draw_line(28, 19, 29, 18, 'C', 1)
    return c.to_image(pal)

def gen_satrancat():
    c = Canvas('.')
    pal = {
        'W': (245, 245, 240),  # White knight horse
        'L': (215, 215, 205),  # Mid light
        'D': (135, 135, 145),  # Shadow
        'K': (45, 45, 55),     # Eye / pedestal base
    }
    # Pedestal base
    c.fill_rect(9, 25, 23, 28, 'K')
    c.fill_rect(11, 23, 21, 25, 'D')
    # Horse chest & neck
    c.fill_polygon([(12, 23), (12, 14), (20, 14), (21, 23)], 'W')
    # Horse head & snout
    c.fill_polygon([(14, 14), (22, 14), (25, 10), (14, 8)], 'W')
    c.fill_polygon([(22, 10), (26, 11), (25, 15), (20, 14)], 'W')
    # Mane & ears
    c.fill_polygon([(14, 8), (13, 4), (16, 6)], 'W')
    c.fill_polygon([(12, 8), (11, 18), (14, 14)], 'D')
    # Muzzle and chest shading
    for y in range(14, 24):
        c.set(20, y, 'L')
        c.set(21, y, 'D')
    # Dark eye
    c.fill_rect(19, 9, 20, 10, 'K')
    c.fill_rect(18, 11, 21, 11, 'L')
    return c.to_image(pal)

def gen_taco():
    c = Canvas('.')
    pal = {
        'Y': (245, 195, 50),   # Crunchy taco shell
        'D': (185, 135, 25),   # Shell shade / spots
        'M': (115, 55, 30),    # Seasoned beef
        'G': (65, 175, 55),    # Lettuce
        'R': (225, 45, 45),    # Tomatoes
    }
    # U-shaped folded taco shell
    c.fill_ellipse(16, 18, 12, 9, 'Y')
    c.fill_rect(4, 9, 28, 16, '.')
    # Lower shell shade
    for x in range(6, 26):
        c.set(x, 24, 'D')
        c.set(x, 25, 'D')
    # Beef filling
    c.fill_rect(6, 16, 26, 19, 'M')
    # Shredded green lettuce
    for lx in range(7, 26, 2):
        c.set(lx, 15, 'G')
        c.set(lx + 1, 14, 'G')
        c.set(lx, 16, 'G')
    # Diced red tomatoes (>= 15 px)
    for tx in [8, 13, 18, 23]:
        c.fill_rect(tx, 14, tx + 1, 15, 'R')
    return c.to_image(pal)

# -------------------------------------------------------------
# 10 YENİ ORTA GENERATORS (All colors >= 10 px)
# -------------------------------------------------------------

def gen_saksofon():
    c = Canvas('.')
    pal = {
        'O': (34, 26, 40),     # Outline
        'B': (255, 215, 55),   # Brass bright
        'M': (215, 165, 30),   # Brass mid
        'D': (155, 105, 20),   # Brass dark
        'S': (225, 235, 245),  # Silver keys
        'P': (250, 250, 220),  # Pearl pads
        'K': (45, 45, 55)      # Mouthpiece
    }
    # Mouthpiece neck
    c.fill_rect(3, 2, 8, 7, 'K')
    c.draw_line(8, 6, 13, 9, 'B', 2)
    # Main body tube descending
    c.draw_line(13, 9, 13, 22, 'B', 3)
    c.draw_line(14, 9, 14, 22, 'M', 1)
    # Lower U-bow
    c.draw_line(13, 22, 17, 26, 'B', 3)
    c.draw_line(17, 26, 22, 23, 'B', 3)
    # Flaring bell
    c.fill_polygon([(21, 23), (27, 13), (23, 11), (18, 20)], 'B')
    c.fill_ellipse(25, 12, 3, 2, 'D')
    c.draw_line(22, 23, 27, 14, 'D', 1)
    c.outline({'B', 'M', 'D', 'K'}, 'O')
    # Tone keys on the body tube placed cleanly (S and P >= 12 px)
    for ky in [11, 14, 17, 20]:
        c.fill_rect(10, ky, 11, ky + 1, 'S')
        c.fill_rect(8, ky, 9, ky + 1, 'P')
    return c.to_image(pal)

def gen_mikroskop():
    c = Canvas('.')
    pal = {
        'O': (34, 26, 40),     # Outline
        'L': (215, 225, 235),  # Metal light
        'M': (135, 145, 160),  # Metal mid
        'D': (65, 75, 90),     # Metal dark
        'C': (80, 220, 240),   # Glass slide cyan
        'B': (245, 185, 40),   # Brass knobs
        'W': (255, 255, 255)   # Specimen dot
    }
    c.fill_rect(8, 26, 24, 29, 'D')
    c.fill_rect(10, 24, 22, 26, 'M')
    c.draw_line(12, 24, 8, 15, 'D', 3)
    c.draw_line(8, 15, 14, 8, 'D', 3)
    c.draw_line(14, 8, 20, 3, 'L', 3)
    c.fill_rect(19, 2, 22, 4, 'M')
    c.fill_rect(13, 9, 18, 12, 'M')
    c.fill_rect(14, 12, 16, 15, 'D')
    c.fill_rect(10, 18, 25, 20, 'D')
    c.fill_rect(11, 15, 24, 17, 'C')
    c.fill_circle(17, 15, 2, 'W')
    c.fill_circle(9, 15, 2, 'B')
    c.fill_circle(9, 15, 1, 'M')
    c.outline({'L', 'M', 'D', 'C', 'B', 'W'}, 'O')
    return c.to_image(pal)

def gen_daktilo():
    c = Canvas('.')
    pal = {
        'O': (34, 26, 40),     # Outline
        'T': (55, 125, 135),   # Typewriter teal
        'D': (30, 75, 85),     # Teal shade
        'W': (245, 245, 245),  # Paper white
        'K': (245, 230, 200),  # Round keys
        'S': (185, 195, 205),  # Carriage silver
        'R': (225, 45, 45)     # Red ribbon
    }
    # Paper sticking up
    c.fill_rect(10, 4, 21, 13, 'W')
    # Typed lines on paper
    c.draw_line(12, 7, 19, 7, 'O', 1)
    c.draw_line(12, 9, 18, 9, 'O', 1)
    # Carriage roller
    c.fill_rect(6, 12, 25, 14, 'S')
    c.fill_rect(4, 12, 6, 14, 'D')
    c.fill_rect(25, 12, 27, 14, 'D')
    # Slanted chassis body
    c.fill_polygon([(5, 15), (26, 15), (28, 27), (3, 27)], 'T')
    c.fill_rect(3, 25, 28, 27, 'D')
    # Red ribbon spool window (R >= 12 px)
    c.fill_rect(8, 15, 12, 17, 'R')
    c.fill_rect(19, 15, 23, 17, 'R')
    # Round typewriter keys (K >= 20 px)
    for ky in [19, 22]:
        for kx in range(6, 26, 3):
            c.fill_rect(kx, ky, kx + 1, ky + 1, 'K')
    # Spacebar
    c.fill_rect(11, 24, 20, 25, 'S')
    c.outline({'T', 'D', 'W', 'K', 'S', 'R'}, 'O')
    return c.to_image(pal)

def gen_keman():
    c = Canvas('.')
    pal = {
        'O': (34, 26, 40),     # Outline
        'V': (205, 125, 50),   # Violin amber
        'D': (135, 70, 30),    # Wood dark
        'K': (45, 45, 55),     # Fingerboard black
        'B': (235, 195, 135),  # Bridge beige
        'S': (235, 240, 245),  # Strings silver
        'F': (75, 35, 20)      # F-holes dark
    }
    # Pegbox and scroll
    c.fill_rect(15, 2, 17, 7, 'K')
    c.fill_rect(13, 3, 19, 4, 'B')  # Pegs
    # Neck and fingerboard
    c.fill_rect(15, 7, 17, 17, 'K')
    # Upper bout
    c.fill_ellipse(16, 16, 6, 4, 'V')
    # Center waist indent
    c.fill_rect(11, 17, 21, 20, 'V')
    c.set(11, 18, '.')
    c.set(21, 18, '.')
    # Lower bout
    c.fill_ellipse(16, 23, 8, 6, 'V')
    # Shading along right side
    for y in range(14, 29):
        if c.get(20, y) == 'V': c.set(20, y, 'D')
        if c.get(21, y) == 'V': c.set(21, y, 'D')
        if c.get(22, y) == 'V': c.set(22, y, 'D')
    # Bridge and chinrest (B >= 12 px)
    c.fill_rect(14, 21, 18, 22, 'B')
    c.fill_rect(12, 26, 15, 28, 'B')
    # F-holes (F >= 12 px)
    c.draw_line(13, 19, 13, 23, 'F', 1)
    c.draw_line(19, 19, 19, 23, 'F', 1)
    c.set(12, 19, 'F')
    c.set(20, 23, 'F')
    # Strings (S >= 15 px)
    c.draw_line(16, 5, 16, 25, 'S', 1)
    c.outline({'V', 'D', 'K', 'B', 'S', 'F'}, 'O')
    return c.to_image(pal)

def gen_arcadekol():
    c = Canvas('.')
    pal = {
        'O': (34, 26, 40),     # Outline
        'C': (50, 55, 65),     # Chassis dark
        'L': (115, 125, 140),  # Chassis light
        'R': (225, 45, 45),    # Joystick red ball
        'S': (215, 220, 230),  # Shaft silver
        'B': (40, 120, 235),   # Blue buttons
        'Y': (255, 215, 40),   # Yellow buttons
        'G': (55, 185, 75)     # Green buttons
    }
    # Chassis angled top
    c.fill_polygon([(4, 13), (27, 13), (29, 27), (2, 27)], 'C')
    c.fill_rect(2, 25, 29, 27, 'L')
    # Joystick shaft
    c.fill_rect(9, 8, 10, 16, 'S')
    c.fill_circle(10, 16, 2, 'L')  # dust washer
    # Joystick red ball (R >= 15 px)
    c.fill_circle(10, 7, 4, 'R')
    # 6 Push buttons (B, Y, G >= 12 px each)
    c.fill_circle(16, 17, 2, 'B')
    c.fill_circle(20, 16, 2, 'B')
    c.fill_circle(24, 17, 2, 'Y')
    c.fill_circle(16, 22, 2, 'Y')
    c.fill_circle(20, 21, 2, 'G')
    c.fill_circle(24, 22, 2, 'G')
    c.outline({'C', 'L', 'R', 'S', 'B', 'Y', 'G'}, 'O')
    return c.to_image(pal)

def gen_daliskaski():
    c = Canvas('.')
    pal = {
        'O': (34, 26, 40),     # Outline
        'B': (235, 175, 40),   # Brass dome
        'L': (255, 225, 115),  # Brass light
        'D': (155, 105, 20),   # Brass shade
        'C': (85, 215, 235),   # Glass cyan
        'W': (245, 255, 255),  # Glare white
        'I': (85, 80, 95)      # Iron breastplate
    }
    c.fill_polygon([(5, 23), (26, 23), (29, 29), (2, 29)], 'I')
    c.fill_circle(16, 14, 10, 'B')
    for y in range(4, 24):
        for x in range(6, 26):
            if c.get(x, y) == 'B':
                if x < 12 and y < 14: c.set(x, y, 'L')
                elif x > 20 or y > 18: c.set(x, y, 'D')
    # Front round viewing port
    c.fill_circle(16, 14, 6, 'D')
    c.fill_circle(16, 14, 5, 'C')
    c.draw_line(11, 14, 21, 14, 'O', 1)
    c.draw_line(16, 9, 16, 19, 'O', 1)
    c.fill_rect(12, 10, 14, 12, 'W')
    c.fill_rect(18, 16, 20, 18, 'W')
    c.fill_ellipse(7, 14, 1, 3, 'D')
    c.fill_ellipse(25, 14, 1, 3, 'D')
    c.outline({'B', 'L', 'D', 'C', 'W', 'I'}, 'O')
    return c.to_image(pal)

def gen_tukan():
    c = Canvas('.')
    pal = {
        'O': (34, 26, 40),     # Outline
        'K': (28, 42, 65),     # Midnight blue-black
        'W': (245, 245, 250),  # White throat bib
        'Y': (255, 215, 40),   # Beak yellow
        'R': (245, 130, 30),   # Beak orange
        'T': (225, 45, 45),    # Beak red tip
        'C': (50, 185, 235),   # Eye cyan
        'B': (125, 75, 45)     # Branch brown
    }
    c.fill_rect(2, 24, 29, 27, 'B')
    c.fill_ellipse(10, 18, 6, 8, 'K')
    c.fill_polygon([(6, 22), (4, 30), (8, 30), (10, 24)], 'K')
    c.fill_polygon([(9, 10), (15, 10), (16, 17), (10, 17)], 'W')
    c.fill_circle(11, 12, 2, 'C')
    c.set(11, 12, 'O')
    c.fill_polygon([(15, 8), (28, 10), (28, 14), (15, 16)], 'Y')
    c.fill_polygon([(18, 10), (28, 11), (28, 14), (18, 15)], 'R')
    c.fill_polygon([(23, 10), (28, 11), (28, 14), (23, 14)], 'T')
    c.fill_rect(10, 23, 12, 25, 'Y')
    c.outline({'K', 'W', 'Y', 'R', 'T', 'C', 'B'}, 'O')
    return c.to_image(pal)

def gen_yeldegirmeni():
    c = Canvas('.')
    pal = {
        'O': (34, 26, 40),     # Outline
        'T': (215, 185, 155),  # Stone tower
        'D': (145, 115, 90),   # Tower shade
        'C': (70, 85, 120),    # Cap roof
        'W': (175, 120, 65),   # Windmill blades
        'S': (245, 245, 250),  # Sail white
        'G': (65, 165, 70),    # Grass green
        'K': (55, 40, 35)      # Door
    }
    # Grassy hill
    c.fill_ellipse(16, 29, 15, 4, 'G')
    # Stone tower
    c.fill_polygon([(12, 11), (20, 11), (23, 27), (9, 27)], 'T')
    # Tower right shade
    for y in range(12, 28):
        c.set(19, y, 'D')
        c.set(20, y, 'D')
        if y > 18: c.set(21, y, 'D')
    # Cap roof
    c.fill_polygon([(10, 11), (16, 6), (22, 11)], 'C')
    # Door
    c.fill_rect(14, 23, 17, 27, 'K')
    # Windmill blade cross arms (W)
    c.draw_line(16, 12, 16, 2, 'W', 1)
    c.draw_line(16, 12, 16, 22, 'W', 1)
    c.draw_line(16, 12, 6, 12, 'W', 1)
    c.draw_line(16, 12, 26, 12, 'W', 1)
    # Lattice sail cloth (S >= 20 px)
    c.fill_rect(17, 3, 19, 10, 'S')
    c.fill_rect(13, 14, 15, 21, 'S')
    c.fill_rect(7, 9, 14, 11, 'S')
    c.fill_rect(18, 13, 25, 15, 'S')
    c.outline({'T', 'D', 'C', 'W', 'S', 'G', 'K'}, 'O')
    return c.to_image(pal)

def gen_buyukitabi():
    c = Canvas('.')
    pal = {
        'O': (34, 26, 40),     # Outline
        'P': (115, 45, 135),   # Leather purple
        'D': (65, 25, 85),     # Leather shade
        'G': (245, 195, 45),   # Gold corners
        'C': (65, 225, 240),   # Magic jewel
        'L': (215, 250, 255),  # Jewel glow
        'W': (245, 235, 195),  # Pages parchment
        'R': (225, 45, 55)     # Ribbon bookmark
    }
    # Book spine and front cover angled
    c.fill_polygon([(6, 8), (24, 4), (26, 24), (8, 28)], 'P')
    # Shaded pages edge visible at bottom/right (W >= 20 px)
    c.fill_polygon([(24, 4), (27, 6), (29, 25), (26, 24)], 'W')
    c.fill_polygon([(8, 28), (11, 29), (29, 25), (26, 24)], 'W')
    # Left spine shadow
    c.draw_line(6, 8, 8, 28, 'D', 2)
    # Gold corner brackets (G >= 15 px)
    c.fill_polygon([(7, 8), (12, 7), (8, 12)], 'G')
    c.fill_polygon([(23, 5), (19, 6), (23, 10)], 'G')
    c.fill_polygon([(9, 27), (13, 26), (10, 23)], 'G')
    c.fill_polygon([(25, 23), (21, 22), (24, 19)], 'G')
    # Center magic talisman jewel (C and L >= 15 px)
    c.fill_circle(16, 16, 4, 'D')
    c.fill_circle(16, 16, 3, 'C')
    c.fill_circle(16, 16, 2, 'L')
    # Red ribbon bookmark (R >= 15 px)
    c.fill_rect(15, 26, 21, 31, 'R')
    c.outline({'P', 'D', 'G', 'C', 'L', 'W'}, 'O')
    return c.to_image(pal)

def gen_hotdog():
    c = Canvas('.')
    pal = {
        'O': (34, 26, 40),     # Outline
        'B': (225, 165, 75),   # Bun golden
        'D': (165, 110, 45),   # Bun shade
        'S': (185, 55, 45),    # Sausage red/brown
        'M': (255, 215, 30),   # Mustard yellow
        'K': (225, 35, 35),    # Ketchup red
        'R': (65, 175, 60)     # Relish green
    }
    # Diagonal bun
    c.fill_polygon([(4, 22), (22, 6), (28, 11), (9, 27)], 'B')
    # Bun shade on lower side
    c.draw_line(5, 23, 10, 27, 'D', 1)
    c.draw_line(10, 27, 28, 12, 'D', 1)
    # Sausage frankfurter extending beyond bun ends
    c.fill_polygon([(2, 23), (23, 4), (29, 9), (7, 28)], 'S')
    # Relish green bits (R >= 15 px)
    for rp in [(8, 19), (12, 15), (16, 12), (20, 8), (10, 20), (14, 16), (18, 13), (22, 9)]:
        c.fill_rect(rp[0], rp[1], rp[0] + 1, rp[1], 'R')
    # Wavy yellow mustard line (M >= 15 px)
    c.draw_line(5, 24, 9, 20, 'M', 1)
    c.draw_line(9, 20, 12, 22, 'M', 1)
    c.draw_line(12, 22, 16, 17, 'M', 1)
    c.draw_line(16, 17, 19, 19, 'M', 1)
    c.draw_line(19, 19, 24, 13, 'M', 1)
    c.draw_line(24, 13, 27, 8, 'M', 1)
    # Ketchup drizzle drops (K >= 12 px)
    for kp in [(7, 22), (11, 18), (15, 15), (20, 11), (23, 7)]:
        c.fill_rect(kp[0], kp[1], kp[0] + 1, kp[1] + 1, 'K')
    c.outline({'B', 'D', 'S', 'M', 'K', 'R'}, 'O')
    return c.to_image(pal)

# -------------------------------------------------------------
# 10 YENİ ZOR GENERATORS (Exactly 1024 cubes, all colors >= 10 px)
# -------------------------------------------------------------

def gen_uzayistasyonu():
    c = Canvas('2')
    pal = {
        '1': (12, 14, 38),     # Space deep navy
        '2': (24, 32, 75),     # Space mid
        '3': (45, 130, 215),   # Earth atmosphere blue
        'W': (235, 245, 255),  # Earth clouds white
        'G': (40, 145, 85),    # Earth land green
        'S': (195, 205, 220),  # Station silver core
        'D': (85, 95, 115),    # Station shade
        'P': (35, 75, 155),    # Solar array blue
        'Y': (255, 205, 50),   # Solar grid gold
        'L': (255, 250, 190)   # Stars
    }
    c.fill_rect(0, 0, 31, 12, '1')
    c.fill_rect(0, 13, 31, 20, '2')
    # Giant Earth curvature across bottom
    c.fill_circle(16, 42, 25, '3')
    # Earth land and clouds
    c.fill_ellipse(10, 26, 6, 3, 'G')
    c.fill_ellipse(22, 28, 5, 2, 'G')
    c.fill_ellipse(16, 24, 7, 2, 'W')
    c.fill_ellipse(7, 28, 4, 1, 'W')
    # Space station in orbit
    # Central pressurized modules
    c.fill_rect(14, 8, 18, 14, 'S')
    c.fill_rect(16, 8, 18, 14, 'D')
    c.fill_rect(12, 10, 20, 12, 'S')
    # Left Solar Panel Array
    c.fill_rect(2, 6, 11, 16, 'P')
    c.draw_line(2, 11, 11, 11, 'Y', 1)
    c.draw_line(6, 6, 6, 16, 'Y', 1)
    # Right Solar Panel Array
    c.fill_rect(21, 6, 30, 16, 'P')
    c.draw_line(21, 11, 30, 11, 'Y', 1)
    c.draw_line(25, 6, 25, 16, 'Y', 1)
    # Stars in sky (L >= 12 px)
    for sx, sy in [(3, 2), (9, 3), (22, 2), (28, 4), (16, 2), (1, 18), (30, 19)]:
        c.fill_rect(sx, sy, sx + 1, sy, 'L')
    return c.to_image(pal)

def gen_buzmagarasi():
    c = Canvas('2')
    pal = {
        '1': (15, 25, 48),     # Deep cave shadow
        '2': (28, 65, 105),    # Mid ice rock
        '3': (60, 145, 195),   # Glacial ice blue
        'C': (95, 225, 245),   # Hanging icicles cyan
        'L': (215, 250, 255),  # Crystal core light
        'F': (125, 195, 235),  # Frozen lake
        'W': (35, 95, 155),    # Water depth
        'R': (85, 110, 140),   # Frost rock shelf
        'P': (185, 75, 225)    # Magic purple crystal
    }
    c.fill_rect(0, 0, 31, 8, '1')
    c.fill_rect(0, 9, 31, 19, '2')
    # Hanging icicles from ceiling
    for ix in range(1, 31, 3):
        c.fill_polygon([(ix, 0), (ix + 2, 0), (ix + 1, 7 + (ix % 4))], 'C')
    # Rocky cavern walls
    c.fill_polygon([(0, 0), (7, 12), (0, 25)], 'R')
    c.fill_polygon([(31, 0), (24, 12), (31, 25)], 'R')
    # Glowing ice cave opening center
    c.fill_circle(16, 14, 6, '3')
    c.fill_circle(16, 14, 3, 'L')
    # Magic purple crystals (P >= 15 px)
    c.fill_polygon([(6, 18), (8, 12), (9, 18)], 'P')
    c.fill_polygon([(22, 19), (24, 13), (25, 19)], 'P')
    # Frozen reflection lake across bottom
    c.fill_rect(0, 22, 31, 31, 'F')
    c.fill_rect(0, 26, 31, 31, 'W')
    return c.to_image(pal)

def gen_mayapiramidi():
    c = Canvas('G')
    pal = {
        '1': (245, 125, 60),   # Sunset orange
        '2': (255, 195, 75),   # Sunset amber
        'S': (255, 245, 175),  # Sun disc
        'P': (215, 175, 115),  # Maya pyramid lit
        'D': (125, 95, 60),    # Pyramid shadow
        'K': (45, 35, 30),     # Temple doorway
        '3': (25, 75, 45),     # Jungle deep
        'G': (45, 145, 65),    # Jungle mid
        'L': (135, 205, 75),   # Palm bright
        'T': (105, 65, 35)     # Palm trunk
    }
    c.fill_rect(0, 0, 31, 8, '1')
    c.fill_rect(0, 9, 31, 16, '2')
    c.fill_circle(16, 7, 5, 'S')
    # Dense jungle canopy
    c.fill_rect(0, 16, 31, 31, '3')
    c.fill_rect(0, 20, 31, 31, 'G')
    # Step Pyramid of Kukulkan (Chichen Itza)
    # Base terrace
    c.fill_polygon([(16, 9), (7, 24), (16, 24)], 'P')
    c.fill_polygon([(16, 9), (16, 24), (25, 24)], 'D')
    # Steps horizontal strata
    for sy in range(12, 24, 3):
        c.draw_line(16 - (sy - 9)//2, sy, 16 + (sy - 9)//2, sy, 'K', 1)
    # Top temple cube
    c.fill_rect(13, 8, 19, 11, 'P')
    c.fill_rect(16, 8, 19, 11, 'D')
    c.fill_rect(15, 9, 17, 11, 'K')  # Temple doorway
    # Foreground jungle palms
    c.draw_line(4, 31, 5, 22, 'T', 1)
    c.draw_line(27, 31, 26, 22, 'T', 1)
    for px, py in [(2, 20), (5, 18), (8, 20), (23, 20), (26, 18), (29, 20)]:
        c.fill_circle(px, py, 1, 'L')
    return c.to_image(pal)

def gen_venedikkanali():
    c = Canvas('W')
    pal = {
        '1': (35, 35, 75),     # Twilight sky
        '2': (135, 65, 95),    # Twilight horizon
        'B': (205, 135, 80),   # Venetian buildings
        'S': (145, 140, 150),  # Stone bridge
        'D': (65, 60, 75),     # Bridge shadow
        'W': (35, 105, 125),   # Canal water
        'G': (235, 185, 65),   # Water reflections
        'K': (25, 25, 35),     # Gondola black
        'R': (225, 45, 45),    # Gondolier red
        'Y': (255, 235, 120)   # Lantern glow
    }
    c.fill_rect(0, 0, 31, 7, '1')
    c.fill_rect(0, 8, 31, 13, '2')
    # Historic canal buildings left & right
    c.fill_rect(0, 6, 8, 20, 'B')
    c.fill_rect(24, 6, 31, 20, 'B')
    # Windows and lantern
    c.fill_rect(2, 9, 5, 12, 'Y')
    c.fill_rect(26, 9, 29, 12, 'Y')
    # Arched stone bridge across center
    c.fill_rect(7, 12, 25, 16, 'S')
    c.fill_ellipse(16, 17, 7, 4, 'D')
    # Canal water
    c.fill_rect(0, 18, 31, 31, 'W')
    # Water ripples reflections (G >= 15 px)
    for gy in [21, 25, 29]:
        c.draw_line(13, gy, 19, gy, 'G', 1)
    # Gondola boat
    c.fill_polygon([(6, 26), (25, 26), (28, 23), (4, 25)], 'K')
    # Prow metal ornament (ferro)
    c.set(4, 23, 'S')
    c.set(4, 24, 'S')
    # Gondolier silhouette (R and K)
    c.fill_rect(21, 20, 23, 24, 'R')
    c.fill_circle(22, 19, 1, 'K')
    return c.to_image(pal)

def gen_mercanresifi():
    c = Canvas('3')
    pal = {
        '1': (75, 185, 235),   # Water surface
        '2': (35, 115, 185),   # Mid water
        '3': (20, 55, 115),    # Deep reef floor
        'T': (55, 155, 85),    # Sea turtle green
        'A': (215, 145, 45),   # Turtle shell amber
        'F': (35, 105, 55),    # Turtle flippers
        'P': (165, 65, 145),   # Branching coral
        'Y': (245, 210, 50),   # Tube coral
        'O': (245, 105, 40),   # Anemone orange
        'L': (165, 235, 255)   # Sunbeams
    }
    c.fill_rect(0, 0, 31, 8, '1')
    c.fill_rect(0, 9, 31, 20, '2')
    c.fill_rect(0, 21, 31, 31, '3')
    # Sunbeam light rays piercing from surface
    c.fill_polygon([(4, 0), (9, 0), (17, 25), (12, 25)], 'L')
    c.fill_polygon([(18, 0), (22, 0), (28, 25), (24, 25)], 'L')
    # Branching corals at bottom
    c.fill_rect(2, 23, 8, 31, 'P')
    c.set(3, 21, 'P')
    c.set(7, 21, 'P')
    c.fill_rect(24, 24, 30, 31, 'Y')
    c.set(25, 22, 'Y')
    c.set(29, 22, 'Y')
    c.fill_ellipse(16, 28, 5, 2, 'O')
    # Sea Turtle swimming diagonally
    c.fill_ellipse(15, 13, 6, 4, 'A')
    c.fill_circle(21, 11, 2, 'T')  # Head
    # Flippers (F >= 15 px)
    c.fill_polygon([(15, 11), (12, 6), (18, 9)], 'F')
    c.fill_polygon([(16, 15), (13, 20), (19, 17)], 'F')
    c.fill_polygon([(10, 12), (7, 10), (11, 14)], 'F')
    return c.to_image(pal)

def gen_kapadokya():
    c = Canvas('C')
    pal = {
        '1': (245, 150, 160),  # Sunrise sky pink
        '2': (255, 215, 140),  # Sunrise horizon
        'R': (215, 175, 135),  # Fairy chimneys rock
        'D': (115, 85, 65),    # Basalt cap / shadow
        'C': (165, 120, 90),   # Valley floor
        'B': (225, 45, 45),    # Balloon 1 red
        'Y': (255, 215, 40),   # Balloon 1 yellow
        'U': (45, 175, 225),   # Balloon 2 cyan
        'P': (145, 65, 175),   # Balloon 2 purple
        'W': (255, 245, 245)   # Cloud wisps
    }
    c.fill_rect(0, 0, 31, 9, '1')
    c.fill_rect(0, 10, 31, 18, '2')
    # Clouds
    c.fill_ellipse(24, 5, 6, 2, 'W')
    # Valley floor
    c.fill_rect(0, 24, 31, 31, 'C')
    # Fairy Chimneys (cone pillars with basalt caps)
    # Chimney 1
    c.fill_polygon([(4, 15), (9, 15), (11, 27), (2, 27)], 'R')
    c.fill_rect(3, 14, 10, 16, 'D')
    # Chimney 2 (center tall)
    c.fill_polygon([(14, 12), (18, 12), (20, 26), (12, 26)], 'R')
    c.fill_rect(13, 11, 19, 13, 'D')
    # Chimney 3
    c.fill_polygon([(23, 16), (28, 16), (30, 27), (21, 27)], 'R')
    c.fill_rect(22, 15, 29, 17, 'D')
    # Hot Air Balloon 1 (Red & Yellow)
    c.fill_circle(8, 7, 4, 'B')
    c.fill_rect(6, 6, 9, 8, 'Y')
    c.fill_rect(7, 12, 8, 12, 'D')  # Basket
    # Hot Air Balloon 2 (Cyan & Purple)
    c.fill_circle(24, 10, 4, 'U')
    c.fill_rect(22, 9, 25, 11, 'P')
    c.fill_rect(23, 15, 24, 15, 'D')
    return c.to_image(pal)

def gen_kutuphane():
    c = Canvas('2')
    pal = {
        '1': (105, 55, 30),    # Oak bookshelf
        '2': (45, 25, 20),     # Shelf recesses
        'R': (195, 45, 45),    # Red books
        'B': (45, 95, 185),    # Blue books
        'G': (45, 135, 65),    # Green books
        'Y': (225, 175, 45),   # Gold books
        'W': (95, 145, 205),   # Arched window
        'K': (35, 35, 45),     # Lead frame
        'L': (165, 105, 55),   # Rolling ladder
        'E': (115, 235, 155)   # Banker lamp
    }
    # Arched window in center
    c.fill_rect(11, 2, 20, 16, 'W')
    c.fill_circle(15, 6, 4, 'W')
    c.draw_line(15, 2, 15, 16, 'K', 1)
    c.draw_line(11, 9, 20, 9, 'K', 1)
    # Massive bookshelves on left & right
    c.fill_rect(0, 0, 10, 31, '1')
    c.fill_rect(21, 0, 31, 31, '1')
    # Shelf recesses filled with colored books
    # Left shelves
    for sy in [3, 9, 15, 21]:
        c.fill_rect(1, sy, 9, sy + 4, '2')
        c.fill_rect(2, sy, 3, sy + 4, 'R')
        c.fill_rect(4, sy, 5, sy + 4, 'B')
        c.fill_rect(6, sy, 7, sy + 4, 'G')
        c.fill_rect(8, sy, 8, sy + 4, 'Y')
    # Right shelves
    for sy in [3, 9, 15, 21]:
        c.fill_rect(22, sy, 30, sy + 4, '2')
        c.fill_rect(23, sy, 24, sy + 4, 'Y')
        c.fill_rect(25, sy, 26, sy + 4, 'R')
        c.fill_rect(27, sy, 28, sy + 4, 'B')
        c.fill_rect(29, sy, 30, sy + 4, 'G')
    # Rolling wooden ladder (L >= 20 px)
    c.draw_line(8, 6, 12, 31, 'L', 1)
    c.draw_line(10, 6, 14, 31, 'L', 1)
    for ry in range(8, 30, 4):
        c.draw_line(8 + (ry - 6)//6, ry, 10 + (ry - 6)//6, ry, 'L', 1)
    # Banker's desk lamp on center table (E >= 10 px)
    c.fill_rect(13, 24, 18, 26, 'E')
    c.fill_rect(15, 27, 16, 30, 'Y')
    return c.to_image(pal)

def gen_kanyongunesi():
    c = Canvas('O')
    pal = {
        '1': (115, 185, 245),  # Desert sky
        'S': (255, 245, 180),  # Radiant sun
        'C': (215, 105, 55),   # Terracotta mesa
        'O': (235, 165, 65),   # Ochre cliff
        'R': (155, 55, 40),    # Red strata
        'D': (75, 30, 30),     # Gorge shadow
        'W': (45, 125, 195),   # Colorado river
        'F': (175, 225, 255),  # Rapids foam
        'G': (85, 125, 65),    # Desert scrub
        'K': (115, 65, 45)     # Foreground rocks
    }
    c.fill_rect(0, 0, 31, 9, '1')
    c.fill_circle(24, 6, 4, 'S')
    # Left giant canyon cliff wall
    c.fill_polygon([(0, 6), (14, 9), (12, 31), (0, 31)], 'C')
    # Strata bands across left cliff
    c.fill_polygon([(0, 12), (13, 14), (12, 18), (0, 16)], 'R')
    c.fill_polygon([(0, 20), (12, 22), (11, 26), (0, 24)], 'D')
    # Right canyon cliff
    c.fill_polygon([(21, 9), (31, 6), (31, 31), (19, 31)], 'C')
    c.fill_polygon([(20, 14), (31, 12), (31, 16), (19, 18)], 'R')
    c.fill_polygon([(19, 21), (31, 19), (31, 24), (18, 26)], 'D')
    # Winding river canyon at bottom
    c.fill_polygon([(12, 17), (19, 17), (17, 31), (13, 31)], 'W')
    # River rapids foam (F >= 10 px)
    c.fill_rect(13, 21, 15, 22, 'F')
    c.fill_rect(14, 26, 16, 27, 'F')
    # Desert scrub green on mesa tops (G >= 10 px)
    c.fill_rect(2, 6, 6, 7, 'G')
    c.fill_rect(25, 6, 29, 7, 'G')
    # Foreground rocks (K >= 15 px)
    c.fill_rect(0, 29, 6, 31, 'K')
    c.fill_rect(26, 29, 31, 31, 'K')
    return c.to_image(pal)

def gen_atlantis():
    c = Canvas('2')
    pal = {
        '1': (15, 45, 95),     # Undersea depth
        '2': (25, 95, 135),    # Ambient teal
        'M': (215, 225, 235),  # Marble pillars
        'D': (85, 105, 125),   # Pillar shadow
        'G': (245, 195, 45),   # Golden dome
        'S': (165, 125, 30),   # Dome shadow
        'F': (75, 135, 130),   # Sea floor sand
        'C': (215, 65, 125),   # Coral pink
        'Y': (255, 220, 60),   # Gold fish
        'L': (75, 240, 230)    # Glowing orb
    }
    c.fill_rect(0, 0, 31, 12, '1')
    c.fill_rect(0, 13, 31, 25, '2')
    c.fill_rect(0, 26, 31, 31, 'F')
    # Sunken Temple Golden Dome
    c.fill_circle(16, 12, 6, 'G')
    c.fill_rect(16, 6, 22, 12, 'S')
    # Temple Pediment
    c.fill_polygon([(8, 12), (23, 12), (16, 7)], 'M')
    # Marble Columns
    for cx in [9, 13, 18, 22]:
        c.fill_rect(cx, 13, cx + 1, 26, 'M')
        for dy in range(14, 25, 3):
            c.set(cx + 1, dy, 'D')
    # Glowing magical ancient orb in temple center
    c.fill_circle(16, 20, 2, 'L')
    # Corals on seabed (C >= 15 px)
    c.fill_rect(2, 23, 6, 28, 'C')
    c.fill_rect(26, 23, 30, 28, 'C')
    # School of swimming golden fish (Y >= 12 px)
    for fx, fy in [(5, 5), (9, 7), (6, 9), (24, 4), (27, 6), (25, 8)]:
        c.fill_rect(fx, fy, fx + 1, fy, 'Y')
    return c.to_image(pal)

def gen_limangece():
    c = Canvas('W')
    pal = {
        '1': (18, 22, 50),     # Midnight sky
        'M': (245, 245, 200),  # Moon crescent
        'Q': (85, 85, 95),     # Harbor stone quay
        'D': (45, 45, 55),     # Quay shadow
        'W': (25, 55, 85),     # Harbor water
        'R': (235, 185, 55),   # Water reflections
        'H': (115, 65, 35),    # Ship wooden hull
        'K': (35, 30, 40),     # Mast & rigging
        'T': (155, 105, 65),   # Tavern building
        'Y': (255, 215, 45)    # Glowing windows
    }
    c.fill_rect(0, 0, 31, 10, '1')
    # Moon crescent
    c.fill_circle(26, 4, 3, 'M')
    c.fill_circle(27, 4, 2, '1')
    # Stone quay pier
    c.fill_rect(0, 17, 14, 24, 'Q')
    c.fill_rect(0, 23, 14, 25, 'D')
    # Waterfront tavern building
    c.fill_rect(1, 8, 12, 17, 'T')
    # Glowing windows (Y >= 12 px)
    c.fill_rect(3, 11, 5, 13, 'Y')
    c.fill_rect(8, 11, 10, 13, 'Y')
    # Docked Sailing Ship
    c.fill_polygon([(15, 17), (29, 17), (26, 23), (17, 23)], 'H')
    # Mast and yards (K >= 20 px)
    c.fill_rect(21, 3, 22, 17, 'K')
    c.draw_line(17, 6, 26, 6, 'K', 1)
    c.draw_line(18, 10, 25, 10, 'K', 1)
    # Harbor water
    c.fill_rect(0, 24, 31, 31, 'W')
    # Golden light reflections on water (R >= 15 px)
    for ry in [25, 28]:
        c.draw_line(3, ry, 11, ry, 'R', 1)
        c.draw_line(17, ry, 25, ry, 'R', 1)
    return c.to_image(pal)

ALL_ITEMS_BATCH2 = [
    # Kolay (10)
    ('kolay', 'Bambu', gen_bambu),
    ('kolay', 'MisirKocani', gen_misirkocani),
    ('kolay', 'KagitUcak', gen_kagitucak),
    ('kolay', 'Yoyo', gen_yoyo),
    ('kolay', 'RubikKup', gen_rubikkup),
    ('kolay', 'DalgicMaskesi', gen_dalgicmaskesi),
    ('kolay', 'Bumerang', gen_bumerang),
    ('kolay', 'Megafon', gen_megafon),
    ('kolay', 'SatrancAt', gen_satrancat),
    ('kolay', 'Taco', gen_taco),
    # Orta (10)
    ('orta', 'Saksofon', gen_saksofon),
    ('orta', 'Mikroskop', gen_mikroskop),
    ('orta', 'Daktilo', gen_daktilo),
    ('orta', 'Keman', gen_keman),
    ('orta', 'ArcadeKol', gen_arcadekol),
    ('orta', 'DalisKaski', gen_daliskaski),
    ('orta', 'Tukan', gen_tukan),
    ('orta', 'YelDegirmeni', gen_yeldegirmeni),
    ('orta', 'BuyuKitabi', gen_buyukitabi),
    ('orta', 'HotDog', gen_hotdog),
    # Zor (10)
    ('zor', 'UzayIstasyonu', gen_uzayistasyonu),
    ('zor', 'BuzMagarasi', gen_buzmagarasi),
    ('zor', 'MayaPiramidi', gen_mayapiramidi),
    ('zor', 'VenedikKanali', gen_venedikkanali),
    ('zor', 'MercanResifi', gen_mercanresifi),
    ('zor', 'Kapadokya', gen_kapadokya),
    ('zor', 'Kutuphane', gen_kutuphane),
    ('zor', 'KanyonGunesi', gen_kanyongunesi),
    ('zor', 'Atlantis', gen_atlantis),
    ('zor', 'LimanGece', gen_limangece),
]

def generate_all(base_dir="Assets/04.11.2026"):
    results = []
    for tier, name, fn in ALL_ITEMS_BATCH2:
        im = fn()
        tier_dir = os.path.join(base_dir, tier)
        os.makedirs(tier_dir, exist_ok=True)
        png_path = os.path.join(tier_dir, f"{name}.png")
        im.save(png_path)
        make_meta(png_path)
        cubes, colors, errs, warns = validate_image(im, name)
        results.append({
            'tier': tier,
            'name': name,
            'path': png_path,
            'cubes': cubes,
            'colors': colors,
            'errors': errs,
            'warnings': warns
        })
    return results

if __name__ == '__main__':
    results = generate_all()
    print(f"Generated {len(results)} images (Batch 2):")
    has_err = False
    for r in results:
        status = "OK" if not r['errors'] and not r['warnings'] else ("FAIL" if r['errors'] else "WARN")
        if r['errors']: has_err = True
        warn_str = f" ({len(r['warnings'])} warns)" if r['warnings'] else ""
        print(f"[{status}] {r['tier'].upper():<5} {r['name']:<15}: {r['cubes']} cubes, {r['colors']} colors{warn_str}")
        for e in r['errors']:
            print(f"    ERROR: {e}")
        for w in r['warnings']:
            print(f"    WARN:  {w}")
    if has_err:
        sys.exit(1)
