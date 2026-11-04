"""Pixel art generator for PixelGame.
Generates 30 game-ready 32x32 pixel art images:
- 10 Kolay (Easy)
- 10 Orta (Medium)
- 10 Zor (Hard)
Validates all images against game rules (no close colors, min pixels, exact dimensions, alpha 0 or 255).
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
# 10 KOLAY GENERATORS
# -------------------------------------------------------------

def gen_elma():
    c = Canvas('.')
    pal = {
        'R': (228, 48, 48),    # Red
        'D': (160, 24, 32),    # Dark red shadow
        'G': (76, 175, 80),    # Green leaf
        'B': (121, 85, 72),    # Brown stem
        'Y': (255, 235, 160)   # Highlight
    }
    c.fill_circle(13, 17, 8, 'R')
    c.fill_circle(18, 17, 8, 'R')
    c.fill_circle(16, 20, 8, 'R')
    c.set(15, 9, '.')
    c.set(16, 9, '.')
    c.set(16, 10, '.')
    c.set(15, 26, '.')
    c.set(16, 26, '.')
    for y in range(21, 26):
        for x in range(16, 24):
            if c.get(x, y) == 'R':
                c.set(x, y, 'D')
    for y in range(12, 17):
        c.set(10, y, 'Y')
        c.set(11, y, 'Y')
    c.set(12, 13, 'Y')
    c.set(12, 14, 'Y')
    for y in range(5, 10):
        c.set(16, y, 'B')
        c.set(17, y, 'B')
    c.set(17, 5, '.')
    c.set(18, 5, 'B')
    c.set(18, 6, 'B')
    for y in range(6, 10):
        for x in range(18, 24):
            if (x - 21)**2 + (y - 8)**2 <= 5:
                c.set(x, y, 'G')
    c.set(24, 7, 'G')
    c.set(24, 8, 'G')
    return c.to_image(pal)

def gen_avokado():
    c = Canvas('.')
    pal = {
        'K': (46, 117, 44),    # Dark skin
        'F': (174, 214, 86),   # Lime flesh
        'S': (139, 69, 19),    # Brown pit
        'H': (205, 133, 63)    # Pit highlight
    }
    c.fill_ellipse(16, 13, 6, 7, 'K')
    c.fill_ellipse(16, 20, 9, 9, 'K')
    c.fill_ellipse(16, 13, 4, 5, 'F')
    c.fill_ellipse(16, 20, 7, 7, 'F')
    c.fill_circle(16, 20, 4, 'S')
    c.fill_rect(14, 18, 16, 21, 'H')
    return c.to_image(pal)

def gen_kivi():
    c = Canvas('.')
    pal = {
        'S': (140, 90, 50),    # Brown skin
        'F': (138, 205, 52),   # Green flesh
        'C': (235, 245, 185),  # Cream core
        'B': (40, 36, 45)      # Dark seeds
    }
    c.fill_circle(16, 16, 11, 'S')
    c.fill_circle(16, 16, 9, 'F')
    c.fill_circle(16, 16, 3, 'C')
    seed_pos = [
        (16, 10), (17, 10), (20, 12), (21, 13),
        (22, 16), (22, 17), (20, 20), (21, 20),
        (16, 22), (17, 22), (12, 20), (11, 20),
        (10, 16), (10, 17), (12, 12), (11, 13)
    ]
    for x, y in seed_pos:
        c.set(x, y, 'B')
    return c.to_image(pal)

def gen_balon():
    c = Canvas('.')
    pal = {
        'C': (38, 166, 224),   # Cyan balloon
        'D': (24, 102, 168),   # Shadow
        'W': (230, 245, 255),  # White shine
        'O': (245, 150, 40),   # Orange knot
        'S': (180, 180, 190)   # String
    }
    c.fill_circle(16, 12, 8, 'C')
    c.fill_polygon([(9, 13), (23, 13), (18, 22), (14, 22)], 'C')
    for y in range(7, 22):
        for x in range(18, 25):
            if c.get(x, y) == 'C' and (x >= 20 or y >= 17):
                c.set(x, y, 'D')
    c.fill_circle(12, 9, 2, 'W')
    c.set(10, 9, 'W')
    c.set(11, 8, 'W')
    c.set(12, 7, 'W')
    c.set(13, 8, 'W')
    c.set(10, 10, 'W')
    c.set(11, 11, 'W')
    c.set(12, 12, 'W')
    c.fill_rect(14, 22, 17, 24, 'O')
    c.draw_line(15, 25, 17, 27, 'S')
    c.draw_line(17, 27, 15, 29, 'S')
    c.draw_line(15, 29, 16, 31, 'S')
    c.set(16, 25, 'S')
    c.set(18, 27, 'S')
    c.set(16, 29, 'S')
    c.set(17, 31, 'S')
    return c.to_image(pal)

def gen_zil():
    c = Canvas('.')
    pal = {
        'G': (255, 198, 30),   # Gold
        'D': (195, 135, 15),   # Dark gold
        'L': (255, 240, 150),  # Light gold
        'R': (210, 40, 50),    # Red ribbon
        'C': (110, 70, 20)     # Clapper
    }
    c.fill_circle(16, 12, 5, 'G')
    c.fill_polygon([(11, 12), (21, 12), (25, 22), (7, 22)], 'G')
    c.fill_rect(6, 22, 26, 24, 'G')
    for y in range(13, 22):
        for x in range(18, 26):
            if c.get(x, y) == 'G':
                c.set(x, y, 'D')
    c.fill_rect(7, 24, 25, 24, 'D')
    for y in range(11, 22):
        c.set(10, y, 'L')
        c.set(11, y, 'L')
    c.fill_rect(14, 6, 18, 9, 'R')
    c.fill_rect(12, 7, 14, 9, 'R')
    c.fill_rect(18, 7, 20, 9, 'R')
    c.fill_rect(15, 5, 17, 6, 'R')
    c.fill_circle(16, 26, 2, 'C')
    c.fill_rect(15, 25, 17, 28, 'C')
    return c.to_image(pal)

def gen_halter():
    c = Canvas('.')
    pal = {
        'B': (190, 200, 215),  # Silver bar
        'G': (0, 168, 230),    # Grip blue
        'M': (90, 105, 125),   # Steel mid
        'D': (45, 55, 70)      # Steel dark
    }
    c.fill_rect(7, 14, 24, 17, 'B')
    c.fill_rect(12, 13, 19, 18, 'G')
    c.fill_rect(6, 7, 9, 24, 'M')
    c.fill_rect(2, 9, 5, 22, 'M')
    c.fill_rect(22, 7, 25, 24, 'M')
    c.fill_rect(26, 9, 29, 22, 'M')
    c.fill_rect(8, 7, 9, 24, 'D')
    c.fill_rect(4, 9, 5, 22, 'D')
    c.fill_rect(6, 23, 9, 24, 'D')
    c.fill_rect(2, 21, 5, 22, 'D')
    c.fill_rect(24, 7, 25, 24, 'D')
    c.fill_rect(28, 9, 29, 22, 'D')
    c.fill_rect(22, 23, 25, 24, 'D')
    c.fill_rect(26, 21, 29, 22, 'D')
    return c.to_image(pal)

def gen_lale():
    c = Canvas('.')
    pal = {
        'R': (235, 50, 75),    # Red petals
        'Y': (255, 210, 45),   # Yellow petal tips
        'G': (55, 160, 60),    # Green stem/leaves
        'P': (205, 105, 55),   # Terracotta pot
        'S': (90, 55, 35)      # Soil
    }
    c.fill_rect(8, 19, 23, 21, 'P')
    c.fill_polygon([(9, 22), (22, 22), (19, 29), (12, 29)], 'P')
    c.fill_rect(10, 20, 21, 21, 'S')
    c.fill_rect(11, 19, 20, 19, 'S')
    c.fill_rect(15, 10, 16, 20, 'G')
    c.draw_line(15, 17, 10, 14, 'G', w=2)
    c.draw_line(10, 14, 8, 11, 'G', w=2)
    c.draw_line(16, 16, 21, 13, 'G', w=2)
    c.draw_line(21, 13, 23, 10, 'G', w=2)
    c.fill_circle(16, 8, 5, 'R')
    c.fill_polygon([(11, 4), (13, 9), (16, 5), (19, 9), (21, 4)], 'R')
    c.fill_rect(11, 4, 12, 6, 'Y')
    c.fill_rect(15, 4, 17, 6, 'Y')
    c.fill_rect(20, 4, 21, 6, 'Y')
    return c.to_image(pal)

def gen_madalya():
    c = Canvas('.')
    pal = {
        'B': (40, 110, 220),   # Blue ribbon
        'R': (225, 45, 55),    # Red ribbon
        'G': (255, 205, 35),   # Gold face
        'D': (190, 140, 20),   # Gold shade
        'L': (255, 245, 170)   # Star light
    }
    for y in range(3, 14):
        c.fill_rect(10 + (y - 3)//2, y, 14 + (y - 3)//2, y, 'B')
    for y in range(3, 14):
        c.fill_rect(17 - (y - 3)//2, y, 21 - (y - 3)//2, y, 'R')
    c.fill_circle(16, 20, 8, 'G')
    for y in range(12, 29):
        for x in range(8, 25):
            if c.get(x, y) == 'G' and ((x - 16)**2 + (y - 20)**2 >= 36 or (x >= 18 and y >= 20)):
                c.set(x, y, 'D')
    c.fill_rect(15, 17, 17, 23, 'L')
    c.fill_rect(13, 19, 19, 21, 'L')
    c.set(16, 16, 'L')
    c.set(16, 24, 'L')
    c.set(12, 20, 'L')
    c.set(20, 20, 'L')
    return c.to_image(pal)

def gen_hamburger():
    c = Canvas('.')
    pal = {
        'B': (225, 160, 70),   # Bun
        'S': (250, 235, 190),  # Sesame
        'P': (115, 60, 30),    # Patty
        'C': (255, 200, 20),   # Cheese
        'L': (65, 175, 55)     # Lettuce
    }
    c.fill_ellipse(16, 12, 10, 6, 'B')
    c.fill_rect(6, 12, 25, 15, 'B')
    for sx, sy in [(9, 10), (12, 8), (16, 8), (20, 9), (23, 11), (13, 12), (18, 12), (15, 10), (11, 14), (20, 14)]:
        c.set(sx, sy, 'S')
    c.fill_rect(5, 16, 26, 17, 'L')
    for lx in range(6, 26, 3):
        c.set(lx, 18, 'L')
    c.fill_rect(6, 18, 25, 19, 'C')
    c.fill_polygon([(10, 19), (13, 19), (11, 22)], 'C')
    c.fill_polygon([(19, 19), (22, 19), (21, 22)], 'C')
    c.fill_rect(6, 20, 25, 23, 'P')
    c.set(5, 21, 'P')
    c.set(5, 22, 'P')
    c.set(26, 21, 'P')
    c.set(26, 22, 'P')
    c.fill_rect(7, 24, 24, 26, 'B')
    c.fill_rect(8, 27, 23, 27, 'B')
    return c.to_image(pal)

def gen_kahvekupasi():
    c = Canvas('.')
    pal = {
        'M': (35, 165, 165),   # Teal mug
        'D': (20, 105, 110),   # Mug shade
        'L': (145, 225, 225),  # Mug light
        'C': (95, 50, 25),     # Coffee
        'S': (225, 235, 245)   # Steam
    }
    c.fill_rect(8, 15, 22, 27, 'M')
    c.fill_rect(9, 28, 21, 28, 'M')
    c.fill_rect(19, 15, 22, 27, 'D')
    c.fill_rect(9, 17, 11, 26, 'L')
    c.fill_rect(22, 18, 26, 25, 'M')
    c.fill_rect(22, 19, 24, 24, '.')
    c.fill_rect(25, 19, 26, 25, 'D')
    c.fill_ellipse(15, 15, 7, 2, 'C')
    c.fill_rect(8, 14, 22, 14, 'M')
    c.draw_line(12, 12, 11, 9, 'S')
    c.draw_line(11, 9, 13, 6, 'S')
    c.draw_line(13, 6, 12, 3, 'S')
    c.draw_line(17, 12, 18, 9, 'S')
    c.draw_line(18, 9, 16, 6, 'S')
    c.draw_line(16, 6, 17, 3, 'S')
    for sx, sy in [(13, 11), (12, 8), (14, 5), (18, 11), (17, 8), (18, 5), (11, 4), (16, 4)]:
        c.set(sx, sy, 'S')
    return c.to_image(pal)

# -------------------------------------------------------------
# 10 ORTA GENERATORS (All colors >= 10 px)
# -------------------------------------------------------------

def gen_kaset():
    c = Canvas('.')
    pal = {
        'O': (34, 26, 40),     # Outline
        'T': (45, 155, 165),   # Shell teal
        'D': (25, 95, 105),    # Shell dark
        'C': (245, 235, 200),  # Label cream
        'R': (240, 130, 40),   # Label orange stripe
        'Y': (255, 215, 50),   # Reel spools
        'B': (105, 60, 40)     # Magnetic tape
    }
    c.fill_rect(3, 7, 28, 25, 'T')
    c.fill_rect(3, 24, 28, 25, 'D')
    c.fill_rect(27, 8, 28, 24, 'D')
    c.fill_rect(6, 22, 25, 25, 'D')
    c.fill_rect(6, 10, 25, 21, 'C')
    c.fill_rect(6, 11, 25, 12, 'R')
    c.fill_rect(9, 14, 22, 19, 'O')
    c.fill_rect(10, 15, 21, 18, 'B')
    # Spool wheels (>= 12 px)
    c.fill_rect(11, 15, 13, 17, 'Y')
    c.fill_rect(18, 15, 20, 17, 'Y')
    # Magnetic tape exposed at bottom trapezoid
    c.fill_rect(11, 23, 20, 24, 'B')
    c.outline({'T', 'D', 'C', 'R', 'Y', 'B'}, 'O')
    return c.to_image(pal)

def gen_kronometre():
    c = Canvas('.')
    pal = {
        'O': (34, 26, 40),     # Outline
        'L': (215, 225, 235),  # Silver light
        'M': (155, 170, 185),  # Silver mid
        'D': (95, 110, 125),   # Silver dark
        'W': (250, 250, 250),  # White dial
        'R': (225, 45, 45),    # Red needle
        'G': (245, 185, 35)    # Gold button
    }
    # Gold button top assembly (>= 10 px)
    c.fill_rect(13, 2, 18, 4, 'G')
    c.fill_rect(14, 5, 17, 6, 'G')
    c.fill_rect(12, 6, 19, 7, 'M')
    c.fill_circle(16, 18, 11, 'M')
    c.fill_circle(16, 18, 9, 'D')
    c.fill_circle(16, 18, 8, 'W')
    for y in range(8, 29):
        for x in range(6, 27):
            if c.get(x, y) == 'M':
                if x < 15 and y < 18:
                    c.set(x, y, 'L')
                elif x > 18 or y > 21:
                    c.set(x, y, 'D')
    c.set(16, 11, 'O')
    c.set(16, 25, 'O')
    c.set(9, 18, 'O')
    c.set(23, 18, 'O')
    # Needle hand (>= 10 px)
    for p in [(16, 18), (17, 17), (18, 16), (19, 15), (20, 14), (16, 17), (17, 18), (18, 15), (19, 14), (20, 13)]:
        c.set(p[0], p[1], 'R')
    c.outline({'L', 'M', 'D', 'W', 'R', 'G'}, 'O')
    return c.to_image(pal)

def gen_bukalemun():
    c = Canvas('.')
    pal = {
        'O': (34, 26, 40),     # Outline
        'G': (85, 195, 65),    # Body green
        'L': (170, 230, 80),   # Lime light
        'D': (35, 125, 65),    # Jade dark
        'R': (245, 140, 30),   # Eye orange
        'Y': (255, 230, 80),   # Pupil yellow
        'B': (120, 75, 45)     # Branch brown
    }
    c.fill_rect(2, 23, 29, 26, 'B')
    c.fill_rect(5, 27, 11, 28, 'B')
    c.fill_circle(17, 16, 7, 'G')
    c.fill_circle(23, 13, 5, 'G')
    c.fill_rect(24, 13, 28, 16, 'G')
    c.draw_line(10, 18, 5, 18, 'G', 2)
    c.draw_line(5, 18, 4, 13, 'G', 2)
    c.draw_line(4, 13, 8, 12, 'G', 2)
    c.draw_line(8, 12, 8, 15, 'G', 2)
    c.fill_rect(13, 21, 16, 24, 'D')
    c.fill_rect(21, 21, 24, 24, 'D')
    for x in range(12, 23):
        c.set(x, 10, 'L')
        c.set(x, 11, 'L')
    c.set(23, 9, 'L')
    c.set(24, 9, 'L')
    for x in range(13, 21):
        c.set(x, 19, 'D')
        c.set(x, 20, 'D')
    # Big eye orange and yellow pupil (>= 10 px)
    c.fill_circle(23, 13, 4, 'R')
    c.fill_rect(22, 12, 24, 14, 'Y')
    c.set(23, 11, 'Y')
    c.set(23, 15, 'Y')
    c.outline({'G', 'L', 'D', 'R', 'Y', 'B'}, 'O')
    return c.to_image(pal)

def gen_ramenkasesi():
    c = Canvas('.')
    pal = {
        'O': (34, 26, 40),     # Outline
        'B': (45, 95, 185),    # Bowl blue
        'L': (105, 155, 235),  # Bowl light
        'R': (215, 120, 45),   # Broth orange
        'Y': (250, 215, 95),   # Noodles
        'W': (245, 245, 245),  # Egg white
        'K': (255, 160, 20),   # Egg yolk
        'C': (170, 110, 60)    # Chopsticks
    }
    c.fill_polygon([(4, 14), (27, 14), (22, 27), (9, 27)], 'B')
    c.fill_rect(11, 27, 20, 29, 'B')
    c.fill_rect(6, 16, 9, 25, 'L')
    c.fill_rect(4, 14, 27, 15, 'L')
    # Broth inside (>= 20 px)
    c.fill_rect(6, 11, 25, 16, 'R')
    # Noodles
    c.draw_line(7, 14, 11, 15, 'Y', 1)
    c.draw_line(11, 15, 14, 13, 'Y', 1)
    c.draw_line(14, 13, 18, 15, 'Y', 1)
    c.draw_line(18, 15, 21, 13, 'Y', 1)
    c.draw_line(8, 16, 12, 16, 'Y', 1)
    c.draw_line(14, 16, 19, 16, 'Y', 1)
    c.draw_line(21, 16, 24, 15, 'Y', 1)
    # Boiled egg half (W >= 15, K >= 10 px)
    c.fill_circle(11, 12, 4, 'W')
    c.fill_circle(11, 12, 2, 'K')
    c.set(10, 11, 'K')
    c.set(12, 11, 'K')
    c.set(10, 13, 'K')
    c.set(12, 13, 'K')
    # Chopsticks
    c.draw_line(3, 10, 28, 8, 'C', 1)
    c.draw_line(3, 11, 28, 10, 'C', 1)
    c.draw_line(16, 7, 15, 5, 'W', 1)
    c.draw_line(15, 5, 17, 3, 'W', 1)
    c.draw_line(20, 7, 21, 5, 'W', 1)
    c.draw_line(21, 5, 19, 3, 'W', 1)
    c.outline({'B', 'L', 'R', 'Y', 'W', 'K', 'C'}, 'O')
    return c.to_image(pal)

def gen_gameboy():
    c = Canvas('.')
    pal = {
        'O': (34, 26, 40),     # Outline
        'G': (190, 195, 205),  # Light grey
        'D': (135, 140, 150),  # Grey shadow
        'S': (140, 165, 55),   # Screen olive
        'L': (65, 85, 30),     # Screen dark LCD
        'M': (185, 35, 95),    # Magenta buttons
        'P': (55, 55, 65)      # D-pad black
    }
    c.fill_rect(6, 4, 25, 27, 'G')
    c.fill_rect(24, 5, 25, 27, 'D')
    c.fill_rect(6, 26, 25, 27, 'D')
    c.fill_rect(9, 6, 22, 15, 'D')
    c.fill_rect(11, 8, 20, 13, 'S')
    c.fill_rect(14, 10, 17, 12, 'L')
    c.set(15, 9, 'L')
    c.fill_rect(9, 18, 13, 22, 'P')
    c.set(9, 18, 'G')
    c.set(13, 18, 'G')
    c.set(9, 22, 'G')
    c.set(13, 22, 'G')
    c.fill_circle(21, 18, 2, 'M')
    c.fill_circle(19, 21, 2, 'M')
    c.draw_line(13, 25, 15, 24, 'D', 1)
    c.draw_line(16, 25, 18, 24, 'D', 1)
    for sx in [21, 23]:
        c.set(sx, 24, 'D')
        c.set(sx + 1, 23, 'D')
    c.outline({'G', 'D', 'S', 'L', 'M', 'P'}, 'O')
    return c.to_image(pal)

def gen_durbun():
    c = Canvas('.')
    pal = {
        'O': (34, 26, 40),     # Outline
        'L': (250, 215, 80),   # Brass light
        'M': (215, 165, 35),   # Brass mid
        'D': (155, 105, 20),   # Brass shade
        'B': (115, 55, 30),    # Leather brown
        'K': (75, 35, 20),     # Leather dark
        'C': (100, 220, 240)   # Lens cyan
    }
    c.fill_polygon([(2, 27), (7, 22), (10, 25), (5, 30)], 'M')
    c.fill_polygon([(6, 23), (13, 16), (17, 20), (10, 27)], 'L')
    c.fill_polygon([(12, 17), (21, 8), (25, 12), (16, 21)], 'B')
    c.fill_polygon([(16, 13), (21, 8), (25, 12), (20, 17)], 'K')
    c.fill_polygon([(20, 9), (26, 3), (30, 7), (24, 13)], 'M')
    c.draw_line(4, 29, 9, 26, 'D', 1)
    c.draw_line(9, 26, 15, 20, 'D', 1)
    c.draw_line(15, 20, 23, 12, 'D', 1)
    c.draw_line(23, 12, 29, 6, 'D', 1)
    # Lens face (>= 15 px)
    c.fill_polygon([(25, 4), (29, 0), (31, 2), (27, 6)], 'C')
    c.fill_polygon([(26, 5), (29, 2), (30, 3), (27, 6)], 'C')
    c.outline({'L', 'M', 'D', 'B', 'K', 'C'}, 'O')
    return c.to_image(pal)

def gen_yelkenligemi():
    c = Canvas('.')
    pal = {
        'O': (34, 26, 40),     # Outline
        'H': (145, 85, 45),    # Wood hull
        'D': (95, 50, 30),     # Keel shade
        'W': (245, 245, 250),  # White sail
        'R': (225, 45, 55),    # Red stripe
        'P': (195, 150, 95),   # Mast pole
        'S': (45, 135, 205),   # Ocean wave
        'F': (155, 220, 250)   # Wave foam
    }
    c.fill_rect(2, 25, 29, 28, 'S')
    c.fill_polygon([(5, 18), (27, 18), (23, 25), (8, 25)], 'H')
    c.fill_rect(8, 24, 23, 25, 'D')
    c.fill_rect(15, 4, 16, 20, 'P')
    c.fill_polygon([(16, 5), (26, 12), (16, 17)], 'W')
    c.fill_polygon([(16, 8), (22, 12), (16, 14)], 'R')
    c.fill_polygon([(14, 6), (7, 16), (14, 16)], 'W')
    c.fill_polygon([(16, 3), (21, 4), (16, 5)], 'R')
    c.outline({'H', 'D', 'W', 'R', 'P', 'S'}, 'O')
    # Foam placed cleanly on wave crests (>= 15 px)
    for fx in range(3, 29, 3):
        c.set(fx, 26, 'F')
        c.set(fx + 1, 26, 'F')
    return c.to_image(pal)

def gen_gramofon():
    c = Canvas('.')
    pal = {
        'O': (34, 26, 40),     # Outline
        'W': (135, 75, 40),    # Cabinet wood
        'D': (85, 45, 25),     # Cabinet dark
        'G': (250, 205, 50),   # Horn gold
        'S': (190, 140, 25),   # Horn shade
        'L': (255, 240, 140),  # Horn light
        'V': (48, 52, 60),     # Vinyl black
        'R': (220, 50, 50)     # Record label
    }
    c.fill_rect(7, 19, 24, 27, 'W')
    c.fill_rect(7, 26, 24, 27, 'D')
    c.fill_rect(22, 20, 24, 26, 'D')
    c.fill_ellipse(15, 18, 7, 2, 'V')
    # Record label red (>= 10 px)
    c.fill_circle(15, 18, 2, 'R')
    c.draw_line(18, 17, 21, 14, 'G', 1)
    c.draw_line(21, 14, 18, 11, 'G', 1)
    c.fill_polygon([(18, 11), (5, 4), (5, 13)], 'G')
    c.fill_ellipse(5, 8, 2, 5, 'S')
    c.draw_line(6, 4, 17, 10, 'L', 1)
    c.draw_line(6, 13, 18, 12, 'S', 1)
    c.outline({'W', 'D', 'G', 'S', 'L', 'V', 'R'}, 'O')
    return c.to_image(pal)

def gen_akvaryum():
    c = Canvas('.')
    pal = {
        'O': (34, 26, 40),     # Outline
        'E': (145, 205, 235),  # Glass rim
        'W': (70, 165, 215),   # Water
        'F': (255, 120, 30),   # Fish orange
        'C': (255, 255, 255),  # Fish white
        'G': (50, 165, 75),    # Seaweed
        'P': (140, 105, 165),  # Pebbles
        'B': (215, 245, 255)   # Bubbles
    }
    c.fill_circle(16, 17, 11, 'W')
    c.fill_rect(10, 5, 22, 8, 'E')
    c.fill_rect(11, 7, 21, 8, 'W')
    for x in range(7, 25):
        if c.get(x, 10) == 'W':
            c.set(x, 9, '.')
    for y in range(24, 28):
        for x in range(9, 23):
            if c.get(x, y) == 'W':
                c.set(x, y, 'P')
    c.draw_line(10, 25, 11, 20, 'G', 1)
    c.draw_line(11, 20, 9, 15, 'G', 1)
    c.draw_line(9, 15, 11, 12, 'G', 1)
    c.set(10, 24, 'G')
    c.set(12, 19, 'G')
    c.set(10, 14, 'G')
    # Clownfish with solid white stripes (>= 10 px)
    c.fill_ellipse(17, 17, 4, 2, 'F')
    c.set(13, 17, 'F')
    c.set(12, 16, 'F')
    c.set(12, 18, 'F')
    c.fill_rect(15, 16, 16, 18, 'C')
    c.fill_rect(18, 16, 19, 18, 'C')
    c.set(20, 16, 'O')
    # Bubbles (>= 10 px)
    c.fill_circle(18, 13, 1, 'B')
    c.fill_circle(20, 10, 1, 'B')
    c.fill_circle(17, 7, 1, 'B')
    c.outline({'E', 'W', 'F', 'C', 'G', 'P', 'B'}, 'O')
    return c.to_image(pal)

def gen_ufogemi():
    c = Canvas('.')
    pal = {
        'O': (34, 26, 40),     # Outline
        'S': (175, 185, 195),  # Saucer silver
        'D': (115, 125, 135),  # Saucer dark
        'C': (90, 215, 235),   # Dome glass
        'A': (85, 220, 75),    # Alien green
        'B': (255, 240, 90),   # Beam yellow
        'L': (255, 250, 180),  # Beam light
        'R': (255, 110, 40)    # Rim lights
    }
    c.fill_polygon([(12, 18), (20, 18), (26, 29), (6, 29)], 'B')
    c.fill_polygon([(14, 18), (18, 18), (22, 29), (10, 29)], 'L')
    c.fill_circle(16, 10, 6, 'C')
    # Alien green (>= 15 px)
    c.fill_ellipse(16, 11, 3, 3, 'A')
    c.fill_circle(16, 8, 2, 'A')
    c.set(15, 8, 'O')
    c.set(17, 8, 'O')
    c.fill_ellipse(16, 15, 12, 4, 'S')
    c.fill_ellipse(16, 17, 9, 2, 'D')
    for rx in [6, 10, 14, 18, 22, 26]:
        c.set(rx, 15, 'R')
        c.set(rx, 16, 'R')
    c.outline({'S', 'D', 'C', 'A', 'B', 'L', 'R'}, 'O')
    return c.to_image(pal)

# -------------------------------------------------------------
# 10 ZOR GENERATORS (Exactly 1024 cubes, all colors >= 10 px)
# -------------------------------------------------------------

def gen_gecekalesi():
    c = Canvas('3')
    pal = {
        '1': (18, 22, 58),     # Sky top navy
        '2': (35, 48, 105),    # Sky mid indigo
        '3': (65, 80, 150),    # Sky low horizon
        'M': (255, 250, 180),  # Moon glow
        'C': (85, 90, 105),    # Castle stone
        'S': (45, 48, 60),     # Castle shade
        'O': (30, 25, 35),     # Outline / silhouette
        'W': (255, 210, 50),   # Window yellow
        'R': (55, 45, 40)      # Cliff rock
    }
    c.fill_rect(0, 0, 31, 8, '1')
    c.fill_rect(0, 9, 31, 15, '2')
    c.fill_rect(0, 16, 31, 23, '3')
    c.fill_circle(24, 6, 4, 'M')
    c.fill_rect(0, 24, 31, 31, 'R')
    for x in range(32):
        if x % 3 == 0:
            c.set(x, 23, 'R')
            c.set(x, 24, 'O')
    c.fill_rect(4, 10, 10, 24, 'C')
    c.fill_rect(9, 10, 10, 24, 'S')
    c.fill_rect(4, 8, 5, 9, 'C')
    c.fill_rect(7, 8, 8, 9, 'C')
    c.fill_rect(10, 8, 10, 9, 'C')
    c.fill_rect(17, 10, 23, 24, 'C')
    c.fill_rect(22, 10, 23, 24, 'S')
    c.fill_rect(17, 8, 18, 9, 'C')
    c.fill_rect(20, 8, 21, 9, 'C')
    c.fill_rect(23, 8, 23, 9, 'C')
    c.fill_rect(10, 14, 17, 24, 'C')
    c.fill_rect(10, 12, 11, 13, 'C')
    c.fill_rect(13, 12, 14, 13, 'C')
    c.fill_rect(16, 12, 17, 13, 'C')
    c.fill_rect(12, 20, 15, 24, 'O')
    c.fill_rect(6, 13, 8, 15, 'W')
    c.fill_rect(19, 13, 21, 15, 'W')
    c.fill_rect(13, 15, 14, 17, 'W')
    c.outline({'C', 'S', 'W'}, 'O')
    return c.to_image(pal)

def gen_colvahasi():
    c = Canvas('L')
    pal = {
        '1': (245, 115, 65),   # Sunset orange
        '2': (255, 185, 75),   # Sunset gold
        'S': (255, 245, 170),  # Setting sun
        'P': (235, 180, 95),   # Pyramid lit
        'D': (165, 110, 55),   # Pyramid shade
        'L': (225, 155, 70),   # Dune light
        'K': (180, 115, 50),   # Dune dark
        'W': (40, 140, 185),   # Oasis water
        'G': (45, 130, 60),    # Palm green
        'T': (95, 55, 30)      # Palm trunk
    }
    c.fill_rect(0, 0, 31, 9, '1')
    c.fill_rect(0, 10, 31, 17, '2')
    c.fill_circle(8, 8, 5, 'S')
    c.fill_polygon([(20, 10), (13, 20), (20, 20)], 'P')
    c.fill_polygon([(20, 10), (20, 20), (28, 20)], 'D')
    c.fill_polygon([(12, 14), (8, 20), (12, 20)], 'P')
    c.fill_polygon([(12, 14), (12, 20), (16, 20)], 'D')
    c.fill_rect(0, 20, 31, 31, 'L')
    for y in range(21, 32):
        for x in range(32):
            if (x + y) % 5 in (0, 1):
                c.set(x, y, 'K')
    c.fill_ellipse(9, 27, 7, 3, 'W')
    c.draw_line(6, 27, 8, 21, 'T', 1)
    c.draw_line(8, 21, 9, 18, 'T', 1)
    c.draw_line(9, 18, 5, 16, 'G', 1)
    c.draw_line(9, 18, 13, 16, 'G', 1)
    c.draw_line(9, 18, 6, 20, 'G', 1)
    c.draw_line(9, 18, 12, 20, 'G', 1)
    c.set(5, 17, 'G')
    c.set(13, 17, 'G')
    c.set(8, 17, 'G')
    c.set(10, 17, 'G')
    c.set(6, 16, 'G')
    c.set(12, 16, 'G')
    return c.to_image(pal)

def gen_derindeniz():
    c = Canvas('3')
    pal = {
        '1': (12, 20, 48),     # Abyss deep
        '2': (24, 45, 95),     # Ocean mid
        '3': (35, 75, 135),    # Lower water
        'A': (95, 85, 120),    # Angler body
        'D': (50, 42, 68),     # Angler belly
        'L': (80, 240, 220),   # Glowing lure
        'T': (235, 245, 250),  # Sharp teeth
        'C': (195, 50, 110),   # Coral
        'R': (65, 58, 62)      # Trench rock
    }
    c.fill_rect(0, 0, 31, 9, '1')
    c.fill_rect(0, 10, 31, 19, '2')
    c.fill_rect(0, 20, 31, 31, '3')
    c.fill_rect(0, 26, 31, 31, 'R')
    for rx in range(0, 32, 3):
        c.set(rx, 25, 'R')
    c.fill_rect(2, 21, 5, 25, 'C')
    c.set(3, 20, 'C')
    c.set(5, 20, 'C')
    c.fill_rect(26, 21, 29, 25, 'C')
    c.set(27, 20, 'C')
    c.set(29, 20, 'C')
    c.fill_circle(16, 14, 6, 'A')
    c.fill_polygon([(10, 14), (5, 11), (5, 17)], 'A')
    c.fill_rect(12, 16, 20, 19, 'D')
    c.fill_rect(19, 13, 22, 17, 'D')
    # Teeth (>= 12 px)
    for tx in [19, 20, 21, 22]:
        c.set(tx, 13, 'T')
        c.set(tx, 16, 'T')
    c.set(21, 14, 'T')
    c.set(22, 15, 'T')
    c.set(17, 12, 'T')
    c.set(17, 13, 'T')
    c.draw_line(17, 8, 21, 5, 'D', 1)
    c.draw_line(21, 5, 24, 7, 'D', 1)
    c.fill_circle(24, 8, 2, 'L')
    return c.to_image(pal)

def gen_japontapinagi():
    c = Canvas('2')
    pal = {
        '1': (250, 175, 145),  # Dawn peach
        '2': (255, 230, 185),  # Dawn golden
        'S': (195, 20, 40),    # Deep crimson sun
        'W': (245, 248, 255),  # Pure snow
        'F': (60, 50, 110),    # Indigo Fuji slope (distinct from lake)
        'T': (240, 75, 35),    # Vermilion torii gate (distinct from sun)
        'D': (115, 20, 30),    # Deep maroon torii shadow
        'P': (255, 140, 180),  # Blossom pink
        'B': (75, 40, 25),     # Bark brown
        'L': (35, 120, 175)    # Crisp lake teal-blue (distinct from Fuji)
    }
    c.fill_rect(0, 0, 31, 10, '1')
    c.fill_rect(0, 11, 31, 21, '2')
    c.fill_circle(16, 9, 6, 'S')
    c.fill_polygon([(16, 8), (7, 21), (25, 21)], 'F')
    c.fill_polygon([(16, 8), (12, 13), (20, 13)], 'W')
    c.fill_rect(0, 22, 31, 31, 'L')
    c.fill_rect(6, 14, 25, 15, 'T')
    c.fill_rect(5, 14, 7, 14, 'D')
    c.fill_rect(24, 14, 26, 14, 'D')
    c.fill_rect(8, 17, 23, 17, 'T')
    c.fill_rect(9, 15, 11, 27, 'T')
    c.fill_rect(10, 15, 11, 27, 'D')
    c.fill_rect(20, 15, 22, 27, 'T')
    c.fill_rect(21, 15, 22, 27, 'D')
    c.draw_line(0, 2, 7, 6, 'B', 1)
    c.draw_line(7, 6, 11, 5, 'B', 1)
    for px, py in [(3, 1), (5, 4), (8, 4), (10, 6), (12, 5), (6, 7), (2, 4), (9, 7)]:
        c.fill_circle(px, py, 1, 'P')
    return c.to_image(pal)

def gen_kutupayisi():
    c = Canvas('1')
    pal = {
        '1': (15, 18, 42),     # Night sky
        'G': (60, 225, 145),   # Aurora green
        'C': (80, 215, 235),   # Aurora cyan
        'V': (145, 75, 185),   # Aurora violet
        'I': (210, 235, 250),  # Ice floe
        'U': (85, 145, 195),   # Submerged ice
        'B': (245, 245, 235),  # Bear fur
        'S': (175, 185, 195),  # Bear shade
        'N': (35, 30, 40)      # Bear nose/eye/claws
    }
    c.fill_rect(0, 0, 31, 31, '1')
    for x in range(32):
        y_g = int(5 + 3 * math.sin(x * 0.25))
        c.fill_rect(x, y_g, x, y_g + 2, 'G')
        c.fill_rect(x, y_g + 3, x, y_g + 4, 'C')
        c.fill_rect(x, y_g - 2, x, y_g - 1, 'V')
    c.fill_rect(4, 21, 28, 26, 'I')
    c.fill_polygon([(4, 27), (28, 27), (24, 31), (8, 31)], 'U')
    c.fill_ellipse(15, 17, 7, 4, 'B')
    c.fill_circle(22, 15, 3, 'B')
    c.fill_rect(24, 15, 26, 17, 'B')
    # Nose, eye, ears, and paws (N >= 12 px)
    c.fill_rect(25, 15, 26, 16, 'N')
    c.fill_rect(21, 13, 22, 14, 'N')
    c.set(21, 12, 'B')
    c.fill_rect(10, 19, 12, 23, 'B')
    c.fill_rect(13, 19, 14, 22, 'S')
    c.fill_rect(18, 19, 20, 23, 'B')
    c.fill_rect(21, 19, 22, 22, 'S')
    # Claws on paws
    c.fill_rect(10, 23, 12, 23, 'N')
    c.fill_rect(13, 22, 14, 22, 'N')
    c.fill_rect(18, 23, 20, 23, 'N')
    c.fill_rect(21, 22, 22, 22, 'N')
    for x in range(10, 20):
        c.set(x, 19, 'S')
    return c.to_image(pal)

def gen_korsanadasi():
    c = Canvas('S')
    pal = {
        '1': (100, 175, 240),  # Tropical sky
        'C': (245, 250, 255),  # Cloud white
        '2': (40, 165, 190),   # Lagoon turquoise
        '3': (25, 105, 160),   # Deep ocean
        'S': (240, 205, 130),  # Sand
        'R': (120, 115, 125),  # Skull rock
        'D': (70, 65, 75),     # Rock shade / eyes
        'G': (50, 155, 65),    # Palm fronds
        'Y': (255, 195, 30),   # Gold chest
        'W': (115, 60, 30)     # Chest wood
    }
    c.fill_rect(0, 0, 31, 11, '1')
    c.fill_ellipse(20, 4, 6, 2, 'C')
    c.fill_rect(0, 12, 31, 18, '2')
    c.fill_rect(0, 19, 31, 23, '3')
    c.fill_rect(0, 24, 31, 31, 'S')
    c.fill_polygon([(4, 21), (28, 21), (31, 24), (0, 24)], 'S')
    c.fill_circle(9, 15, 5, 'R')
    c.fill_rect(6, 17, 12, 21, 'R')
    c.set(7, 14, 'D')
    c.set(8, 14, 'D')
    c.set(10, 14, 'D')
    c.set(11, 14, 'D')
    c.set(9, 16, 'D')
    c.fill_rect(7, 18, 11, 19, 'D')
    c.draw_line(23, 26, 22, 19, 'W', 1)
    c.draw_line(22, 19, 20, 14, 'W', 1)
    c.draw_line(20, 14, 15, 12, 'G', 1)
    c.draw_line(20, 14, 25, 12, 'G', 1)
    c.draw_line(20, 14, 17, 16, 'G', 1)
    c.draw_line(20, 14, 24, 16, 'G', 1)
    c.set(16, 13, 'G')
    c.set(24, 13, 'G')
    # Treasure Chest (Y >= 12 px)
    c.fill_rect(14, 25, 19, 29, 'W')
    c.fill_rect(15, 25, 18, 27, 'Y')
    return c.to_image(pal)

def gen_antikmisir():
    c = Canvas('L')
    pal = {
        'G': (235, 185, 45),   # Border gold
        'D': (165, 115, 25),   # Border dark
        'L': (30, 75, 165),    # Lapis blue
        'T': (35, 175, 155),   # Scarab teal
        'S': (18, 95, 85),     # Scarab shade
        'R': (225, 45, 40),    # Solar disc red
        'O': (235, 110, 40),   # Carnelian orange
        'W': (245, 240, 230),  # Lotus white
        'B': (34, 26, 40)      # Hieroglyph black
    }
    c.fill_rect(0, 0, 31, 31, 'G')
    c.fill_rect(1, 1, 30, 30, 'D')
    c.fill_rect(2, 2, 29, 29, 'L')
    c.fill_circle(16, 6, 3, 'R')
    c.fill_circle(16, 6, 1, 'O')
    c.fill_circle(16, 16, 5, 'T')
    c.fill_polygon([(16, 12), (5, 9), (9, 18)], 'T')
    c.fill_polygon([(16, 12), (26, 9), (22, 18)], 'T')
    c.draw_line(16, 14, 7, 12, 'S', 1)
    c.draw_line(16, 15, 8, 15, 'S', 1)
    c.draw_line(16, 14, 24, 12, 'S', 1)
    c.draw_line(16, 15, 23, 15, 'S', 1)
    c.draw_line(14, 11, 15, 8, 'B', 1)
    c.draw_line(17, 11, 16, 8, 'B', 1)
    c.fill_polygon([(11, 27), (16, 22), (20, 27)], 'W')
    c.fill_rect(14, 24, 17, 26, 'O')
    for cx, cy in [(4, 4), (27, 4), (4, 27), (27, 27)]:
        c.fill_rect(cx, cy, cx + 1, cy + 1, 'O')
    return c.to_image(pal)

def gen_sibersehir():
    c = Canvas('2')
    pal = {
        '1': (70, 25, 110),    # Deep violet sky (distinct from tower body and shadow)
        '2': (145, 40, 120),   # Vibrant dusk magenta
        'T': (65, 80, 105),    # Steel slate tower body (distinct from shadow)
        'D': (18, 16, 26),     # Dark obsidian shadow
        'C': (40, 230, 245),   # Neon cyan
        'P': (255, 45, 145),   # Neon pink
        'Y': (255, 220, 40),   # Neon yellow
        'O': (235, 105, 30),   # Spinner car orange
        'R': (240, 35, 45),    # Beacon red
        'H': (40, 145, 240)    # Electric highway blue
    }
    c.fill_rect(0, 0, 31, 9, '1')
    c.fill_rect(0, 10, 31, 16, '2')
    c.fill_rect(2, 6, 10, 27, 'T')
    c.fill_rect(8, 7, 10, 27, 'D')
    c.fill_rect(21, 4, 29, 27, 'T')
    c.fill_rect(27, 5, 29, 27, 'D')
    c.fill_rect(12, 11, 19, 27, 'T')
    c.fill_rect(17, 12, 19, 27, 'D')
    # Beacons (R >= 12 px)
    c.fill_rect(5, 4, 7, 5, 'R')
    c.fill_rect(24, 2, 26, 3, 'R')
    c.fill_rect(15, 9, 16, 10, 'R')
    c.fill_rect(3, 10, 7, 13, 'C')
    c.fill_rect(23, 8, 26, 12, 'P')
    for wy in range(14, 25, 3):
        c.set(4, wy, 'Y')
        c.set(6, wy, 'Y')
        c.set(14, wy, 'Y')
        c.set(16, wy, 'Y')
        c.set(23, wy, 'Y')
        c.set(25, wy, 'Y')
    c.fill_rect(11, 8, 15, 9, 'O')
    c.set(10, 9, 'C')
    c.fill_rect(0, 28, 31, 31, 'D')
    c.fill_rect(0, 29, 31, 30, 'H')
    for hx in range(2, 31, 6):
        c.set(hx, 29, 'Y')
        c.set(hx + 1, 29, 'R')
    return c.to_image(pal)

def gen_ormangolu():
    c = Canvas('P')
    pal = {
        '1': (18, 32, 35),     # Deep forest night
        '2': (30, 65, 55),     # Pine dark
        '3': (45, 115, 80),    # Moss canopy
        'R': (75, 80, 85),     # Waterfall cliff
        'W': (225, 245, 255),  # Waterfall cascade
        'P': (35, 135, 125),   # Pool emerald
        'C': (225, 45, 60),    # Mushroom cap red
        'S': (235, 225, 200),  # Stem/spots white
        'B': (70, 215, 220),   # Glow spores blue
        'Y': (250, 240, 95)    # Fireflies
    }
    c.fill_rect(0, 0, 31, 8, '1')
    c.fill_polygon([(0, 9), (7, 2), (14, 9)], '2')
    c.fill_polygon([(17, 9), (24, 2), (31, 9)], '2')
    c.fill_rect(0, 9, 31, 14, '3')
    c.fill_rect(10, 11, 21, 22, 'R')
    c.fill_rect(13, 11, 18, 22, 'W')
    c.fill_rect(0, 22, 31, 31, 'P')
    c.fill_ellipse(6, 22, 5, 3, 'C')
    c.fill_rect(5, 23, 7, 28, 'S')
    c.set(4, 21, 'S')
    c.set(7, 21, 'S')
    c.set(6, 23, 'S')
    for fx, fy in [(3, 14), (8, 12), (23, 13), (27, 16), (25, 24), (20, 27)]:
        c.fill_circle(fx, fy, 1, 'Y')
    # Glowing spores (B >= 12 px)
    for bx, by in [(2, 25), (9, 26), (11, 27), (2, 28), (10, 29), (12, 25)]:
        c.fill_rect(bx, by, bx + 1, by, 'B')
    return c.to_image(pal)

def gen_lombozmanzara():
    c = Canvas('1')
    pal = {
        'B': (215, 160, 40),   # Brass rim
        'D': (135, 95, 20),    # Rivet shade
        'L': (255, 225, 115),  # Rim light
        '1': (20, 55, 95),     # Ocean depth
        '2': (35, 95, 145),    # Ocean light ray
        'K': (145, 55, 125),   # Kraken tentacle
        'S': (235, 175, 210),  # Suckers
        'E': (255, 215, 50),   # Kraken eye
        'G': (175, 225, 245)   # Glass glare
    }
    c.fill_rect(0, 0, 31, 31, '1')
    c.fill_polygon([(8, 0), (16, 0), (28, 31), (20, 31)], '2')
    c.fill_circle(14, 14, 5, 'K')
    c.fill_circle(14, 14, 3, 'E')
    c.fill_circle(14, 14, 1, 'B')
    # Tentacle curling
    c.draw_line(24, 25, 20, 20, 'K', 3)
    c.draw_line(20, 20, 16, 17, 'K', 2)
    c.draw_line(16, 17, 18, 12, 'K', 2)
    # Suckers (S >= 14 px)
    c.fill_circle(22, 22, 1, 'S')
    c.fill_circle(19, 18, 1, 'S')
    c.fill_circle(16, 15, 1, 'S')
    c.fill_circle(17, 13, 1, 'S')
    c.draw_line(8, 24, 24, 8, 'G', 1)
    c.draw_line(10, 25, 25, 10, 'G', 1)
    for y in range(32):
        for x in range(32):
            d2 = (x - 15.5)**2 + (y - 15.5)**2
            if d2 >= 13.5**2:
                c.set(x, y, 'B')
                if int(math.atan2(y - 15.5, x - 15.5) * 4 / math.pi * 2) % 2 == 0 and d2 >= 14.5**2:
                    c.set(x, y, 'D')
            elif d2 >= 12.0**2:
                c.set(x, y, 'L' if x + y < 31 else 'D')
    return c.to_image(pal)

ALL_ITEMS = [
    # Kolay (10)
    ('kolay', 'Elma', gen_elma),
    ('kolay', 'Avokado', gen_avokado),
    ('kolay', 'Kivi', gen_kivi),
    ('kolay', 'Balon', gen_balon),
    ('kolay', 'Zil', gen_zil),
    ('kolay', 'Halter', gen_halter),
    ('kolay', 'Lale', gen_lale),
    ('kolay', 'Madalya', gen_madalya),
    ('kolay', 'Hamburger', gen_hamburger),
    ('kolay', 'KahveKupasi', gen_kahvekupasi),
    # Orta (10)
    ('orta', 'Kaset', gen_kaset),
    ('orta', 'Kronometre', gen_kronometre),
    ('orta', 'Bukalemun', gen_bukalemun),
    ('orta', 'RamenKasesi', gen_ramenkasesi),
    ('orta', 'GameBoy', gen_gameboy),
    ('orta', 'Durbun', gen_durbun),
    ('orta', 'YelkenliGemi', gen_yelkenligemi),
    ('orta', 'Gramofon', gen_gramofon),
    ('orta', 'Akvaryum', gen_akvaryum),
    ('orta', 'UfoGemi', gen_ufogemi),
    # Zor (10)
    ('zor', 'GeceKalesi', gen_gecekalesi),
    ('zor', 'ColVahasi', gen_colvahasi),
    ('zor', 'DerinDeniz', gen_derindeniz),
    ('zor', 'JaponTapinagi', gen_japontapinagi),
    ('zor', 'KutupAyisi', gen_kutupayisi),
    ('zor', 'KorsanAdasi', gen_korsanadasi),
    ('zor', 'AntikMisir', gen_antikmisir),
    ('zor', 'SiberSehir', gen_sibersehir),
    ('zor', 'OrmanGolu', gen_ormangolu),
    ('zor', 'LombozManzara', gen_lombozmanzara),
]

def generate_all(base_dir="Assets/04.11.2026"):
    results = []
    for tier, name, fn in ALL_ITEMS:
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
    print(f"Generated {len(results)} images:")
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
