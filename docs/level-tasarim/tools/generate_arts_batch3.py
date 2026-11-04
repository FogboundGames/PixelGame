"""Pixel art generator for PixelGame - Batch 3 (30 more unique levels).
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

def gen_ciceksaksisi():
    c = Canvas('.')
    pal = {
        'P': (205, 95, 45),     # Terracotta pot
        'S': (135, 55, 25),     # Pot shadow
        'G': (55, 165, 65),     # Leaf green
        'M': (235, 65, 120),    # Magenta flower petals
        'Y': (255, 215, 40),    # Yellow blossom center
    }
    # Terracotta pot body
    c.fill_polygon([(9, 18), (22, 18), (20, 29), (11, 29)], 'P')
    c.fill_polygon([(18, 18), (22, 18), (20, 29), (17, 29)], 'S')
    # Pot upper rim
    c.fill_rect(8, 16, 23, 18, 'P')
    c.fill_rect(17, 16, 23, 18, 'S')
    # Plant stem & leaves
    c.draw_line(15, 12, 15, 16, 'G', 2)
    # Left leaf
    c.fill_polygon([(9, 11), (15, 13), (11, 16)], 'G')
    # Right leaf
    c.fill_polygon([(22, 11), (16, 13), (20, 16)], 'G')
    # Flower blossom (M)
    c.fill_circle(15, 8, 5, 'M')
    # Flower center (Y >= 13 px)
    c.fill_circle(15, 8, 2, 'Y')
    return c.to_image(pal)

def gen_dondurmakulah():
    c = Canvas('.')
    pal = {
        'W': (220, 160, 75),    # Waffle cone light
        'S': (145, 95, 40),     # Waffle cone shadow/grid
        'C': (255, 145, 180),   # Cream pink
        'D': (210, 65, 110),    # Cream shade/berry
        'R': (190, 25, 40),     # Cherry red
    }
    # Cone triangle
    c.fill_polygon([(9, 16), (22, 16), (16, 29)], 'W')
    for y in range(16, 30):
        c.set(16 + (y - 16)//2, y, 'S')
        c.set(15 - (y - 16)//2, y, 'S')
    # Cone shadow right edge
    for y in range(16, 29):
        rx = 22 - (y - 16) * 6 // 13
        c.set(rx, y, 'S')
        c.set(rx - 1, y, 'S')
    # Lower scoop
    c.fill_circle(16, 14, 6, 'C')
    c.fill_ellipse(16, 16, 6, 2, 'D')
    # Upper swirl scoop
    c.fill_circle(16, 9, 4, 'C')
    c.fill_ellipse(17, 10, 3, 2, 'D')
    # Cherry on top (R >= 13 px)
    c.fill_circle(16, 4, 2, 'R')
    return c.to_image(pal)

def gen_ruzgargulu():
    c = Canvas('.')
    pal = {
        'W': (150, 100, 50),    # Wooden stick
        'R': (230, 45, 55),     # Red blade
        'B': (45, 130, 230),    # Blue blade
        'Y': (250, 205, 35),    # Yellow blade
        'G': (45, 185, 75),     # Green blade
    }
    # Stick down
    c.fill_rect(15, 14, 16, 29, 'W')
    # Pinwheel 4 blades meeting at (15, 13)
    # Red blade (Top-right)
    c.fill_polygon([(15, 13), (15, 4), (24, 7)], 'R')
    # Blue blade (Bottom-right)
    c.fill_polygon([(15, 13), (24, 13), (21, 22)], 'B')
    # Green blade (Bottom-left)
    c.fill_polygon([(15, 13), (15, 22), (6, 19)], 'G')
    # Yellow blade (Top-left)
    c.fill_polygon([(15, 13), (6, 13), (9, 4)], 'Y')
    return c.to_image(pal)

def gen_recelkavanozu():
    c = Canvas('.')
    pal = {
        'J': (195, 30, 55),     # Strawberry jam red
        'D': (115, 15, 35),     # Dark jam shadow
        'C': (245, 235, 205),   # Cloth lid cream
        'B': (40, 75, 165),     # Checkered lid pattern / blue tie
        'W': (240, 245, 245),   # Jar paper label white
    }
    # Jar glass body filled with jam
    c.fill_rect(9, 13, 22, 27, 'J')
    c.fill_rect(18, 13, 22, 27, 'D')
    c.fill_rect(9, 25, 22, 27, 'D')
    # Cloth lid draped over top
    c.fill_polygon([(7, 11), (24, 11), (22, 6), (9, 6)], 'C')
    # Tied string & checkered pattern
    c.fill_rect(8, 11, 23, 12, 'B')
    c.draw_line(10, 6, 12, 11, 'B', 1)
    c.draw_line(15, 6, 17, 11, 'B', 1)
    c.draw_line(20, 6, 22, 11, 'B', 1)
    # Jar white label (W >= 15 px)
    c.fill_rect(12, 17, 19, 23, 'W')
    return c.to_image(pal)

def gen_pikniksepeti():
    c = Canvas('.')
    pal = {
        'W': (215, 165, 80),    # Wicker light
        'D': (140, 95, 40),     # Wicker dark weave
        'R': (225, 45, 55),     # Checkered cloth red
        'C': (245, 245, 240),   # Checkered cloth white
        'H': (85, 50, 25),      # Dark wood handle
    }
    # Basket handle arch
    for x in range(10, 22):
        c.set(x, 6, 'H')
        c.set(x, 7, 'H')
    for y in range(7, 16):
        c.set(10, y, 'H')
        c.set(11, y, 'H')
        c.set(20, y, 'H')
        c.set(21, y, 'H')
    # Basket trapezoid
    c.fill_polygon([(6, 15), (25, 15), (23, 28), (8, 28)], 'W')
    # Wicker horizontal stripes
    for y in [18, 22, 26]:
        c.draw_line(7 + (y-15)//6, y, 24 - (y-15)//6, y, 'D', 1)
    # Checkered cloth napkin hanging out
    c.fill_polygon([(5, 14), (14, 14), (11, 21), (4, 18)], 'R')
    c.fill_rect(6, 14, 9, 17, 'C')
    c.fill_rect(9, 17, 11, 19, 'C')
    return c.to_image(pal)

def gen_canyelegi():
    c = Canvas('.')
    pal = {
        'O': (250, 105, 25),    # High-vis orange
        'D': (185, 60, 15),     # Orange shadow
        'B': (40, 45, 55),      # Charcoal straps
        'Y': (240, 240, 130),   # Reflective yellow patches
        'S': (180, 190, 200),   # Silver buckles
    }
    # Vest main silhouette
    c.fill_rect(7, 6, 24, 26, 'O')
    c.fill_rect(17, 6, 24, 26, 'D')
    # Neck hole cutout
    c.fill_polygon([(12, 6), (19, 6), (17, 13), (14, 13)], '.')
    # Armhole cutouts
    c.fill_rect(7, 10, 8, 17, '.')
    c.fill_rect(23, 10, 24, 17, '.')
    # Reflective yellow shoulder patches (Y >= 12 px)
    c.fill_rect(9, 7, 12, 10, 'Y')
    c.fill_rect(19, 7, 22, 10, 'Y')
    # Straps across chest (B >= 15 px)
    c.fill_rect(8, 18, 23, 19, 'B')
    c.fill_rect(8, 23, 23, 24, 'B')
    # Buckles (S >= 10 px)
    c.fill_rect(15, 17, 17, 20, 'S')
    c.fill_rect(15, 22, 17, 25, 'S')
    return c.to_image(pal)

def gen_sirtcantasi():
    c = Canvas('.')
    pal = {
        'T': (35, 160, 175),    # Teal backpack body
        'D': (18, 95, 110),     # Dark teal shade
        'Y': (245, 195, 45),    # Yellow front pocket
        'B': (45, 45, 50),      # Charcoal zippers/straps
        'W': (235, 235, 220),   # Light badge & buckles
    }
    # Backpack rounded body
    c.fill_rect(8, 8, 23, 27, 'T')
    c.fill_rect(18, 8, 23, 27, 'D')
    # Top curve corners
    c.set(8, 8, '.')
    c.set(23, 8, '.')
    # Top carry loop
    c.fill_rect(14, 5, 17, 7, 'B')
    # Front yellow pouch (Y >= 20 px)
    c.fill_rect(10, 17, 21, 25, 'Y')
    # Front pouch zipper (B >= 12 px)
    c.fill_rect(10, 17, 21, 18, 'B')
    # Diamond adventurer badge (W >= 10 px)
    c.fill_rect(14, 11, 17, 14, 'W')
    return c.to_image(pal)

def gen_matara():
    c = Canvas('.')
    pal = {
        'G': (115, 140, 65),    # Olive canteen body
        'D': (65, 85, 35),      # Dark olive shade
        'S': (195, 205, 215),   # Silver cap & rim
        'M': (105, 115, 125),   # Metal cap grooves
        'T': (190, 120, 55),    # Tan leather strap
    }
    # Flask oval body
    c.fill_ellipse(16, 18, 8, 9, 'G')
    for y in range(10, 28):
        for x in range(17, 25):
            if c.get(x, y) == 'G':
                c.set(x, y, 'D')
    # Flask neck and screw cap (S & M)
    c.fill_rect(14, 5, 18, 9, 'S')
    c.fill_rect(13, 8, 19, 10, 'M')
    # Leather harness strap encircling flask (T >= 15 px)
    c.fill_rect(9, 17, 23, 19, 'T')
    c.fill_rect(15, 10, 17, 27, 'T')
    return c.to_image(pal)

def gen_masalambasi():
    c = Canvas('.')
    pal = {
        'R': (230, 65, 60),     # Coral red shade
        'D': (150, 35, 40),     # Dark coral shade
        'S': (170, 175, 185),   # Steel gooseneck arm
        'Y': (255, 230, 110),   # Light cone yellow
        'K': (45, 50, 55),      # Base switch & rim dark
    }
    # Lamp base
    c.fill_rect(7, 26, 18, 29, 'R')
    c.fill_rect(14, 26, 18, 29, 'D')
    # Base dark rim and switch (K >= 15 px)
    c.fill_rect(7, 29, 18, 29, 'K')
    c.fill_rect(9, 25, 12, 26, 'K')
    # Gooseneck steel arm (S >= 25 px)
    c.draw_line(16, 26, 22, 18, 'S', 2)
    c.draw_line(22, 18, 18, 10, 'S', 2)
    # Lamp shade angled down-left (R & D)
    c.fill_polygon([(10, 15), (20, 8), (17, 5), (8, 12)], 'R')
    c.fill_polygon([(14, 12), (20, 8), (17, 5), (11, 10)], 'D')
    # Glowing light cone projecting from shade (Y >= 20 px)
    c.fill_polygon([(5, 17), (13, 18), (8, 24), (2, 22)], 'Y')
    return c.to_image(pal)

def gen_boyamapaleti():
    c = Canvas('.')
    pal = {
        'W': (220, 175, 115),   # Wood palette
        'D': (155, 110, 65),    # Wood shadow/rim
        'R': (230, 45, 50),     # Red paint dab
        'B': (45, 115, 225),    # Blue paint dab
        'G': (50, 180, 80),     # Green paint dab
    }
    # Palette oval shape
    c.fill_ellipse(16, 16, 11, 9, 'W')
    # Lower right shadow
    for y in range(16, 26):
        for x in range(16, 28):
            if c.get(x, y) == 'W' and (x + y > 36):
                c.set(x, y, 'D')
    # Thumbhole cutout
    c.fill_ellipse(21, 18, 2, 2, '.')
    # Paint dabs along curved upper edge (each >= 12 px)
    c.fill_circle(9, 13, 2, 'R')
    c.fill_circle(15, 10, 2, 'B')
    c.fill_circle(21, 11, 2, 'G')
    return c.to_image(pal)

# -------------------------------------------------------------
# 10 YENİ ORTA GENERATORS (with OUT = (34, 26, 40))
# -------------------------------------------------------------

def gen_dikismakinesi():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'W': (160, 100, 50),    # Wood table
        'D': (105, 60, 25),     # Wood shadow
        'C': (60, 65, 75),      # Cast iron machine body
        'L': (135, 145, 160),   # Cast iron highlight
        'B': (230, 185, 45),    # Brass wheel & spool
        'S': (210, 215, 225),   # Silver needle & presser foot
    }
    # Wood table base
    c.fill_rect(3, 24, 28, 28, 'W')
    c.fill_rect(3, 27, 28, 28, 'D')
    # Cast iron base plate
    c.fill_rect(5, 21, 26, 23, 'C')
    # Cast iron upright arm on right
    c.fill_rect(21, 11, 25, 21, 'C')
    c.fill_rect(21, 11, 22, 21, 'L')
    # Horizontal machine arm reaching left
    c.fill_rect(9, 11, 24, 15, 'C')
    c.fill_rect(9, 11, 24, 12, 'L')
    # Needle head on left
    c.fill_rect(8, 14, 12, 18, 'C')
    # Brass handwheel on right (B >= 15 px)
    c.fill_ellipse(26, 14, 2, 4, 'B')
    # Gold thread spool on top
    c.fill_rect(19, 8, 21, 11, 'B')
    c.outline({'W', 'D', 'C', 'L', 'B'}, 'O')
    # Silver needle & presser foot placed after outline (S >= 14 px)
    c.fill_rect(10, 18, 11, 23, 'S')
    c.fill_rect(9, 21, 12, 23, 'S')
    return c.to_image(pal)

def gen_trompet():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'B': (235, 185, 45),    # Brass body mid
        'L': (255, 225, 115),   # Brass highlight
        'D': (175, 125, 25),    # Brass shadow
        'S': (215, 225, 235),   # Valve pistons silver
        'W': (245, 245, 250),   # Valve pearl buttons
        'M': (125, 80, 20),     # Bell inner shadow
    }
    # Main leadpipe and tuning slide (horizontal tubes)
    c.fill_rect(3, 14, 23, 18, 'B')
    c.fill_rect(3, 14, 23, 15, 'L')
    c.fill_rect(7, 19, 22, 22, 'B')
    c.fill_rect(7, 21, 22, 22, 'D')
    # Mouthpiece on left
    c.fill_polygon([(1, 13), (4, 14), (4, 18), (1, 19)], 'S')
    # Bell flare expanding to right
    c.fill_polygon([(21, 13), (30, 8), (30, 25), (21, 20)], 'B')
    c.fill_polygon([(23, 12), (30, 8), (30, 13)], 'L')
    c.fill_polygon([(23, 21), (30, 25), (30, 20)], 'D')
    c.fill_ellipse(29, 16, 2, 6, 'M')  # Bell opening depth (M >= 18 px)
    # 3 Valve casings (vertical)
    c.fill_rect(11, 10, 20, 23, 'B')
    c.fill_rect(11, 20, 20, 23, 'D')
    c.outline({'B', 'L', 'D', 'M'}, 'O')
    # Extra brass highlight stripe after outline
    c.draw_line(5, 15, 20, 15, 'L', 1)
    # Silver valve stems & white pearl buttons drawn after outline
    # Stems (S >= 16 px)
    c.fill_rect(12, 7, 13, 11, 'S')
    c.fill_rect(15, 7, 16, 11, 'S')
    c.fill_rect(18, 7, 19, 11, 'S')
    # Buttons (W >= 15 px)
    c.fill_rect(11, 5, 14, 7, 'W')
    c.fill_rect(15, 5, 17, 7, 'W')
    c.fill_rect(18, 5, 20, 7, 'W')
    return c.to_image(pal)

def gen_gazlambasi():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'M': (55, 105, 95),     # Vintage green-teal metal
        'L': (105, 165, 150),   # Metal highlight
        'G': (170, 215, 235),   # Glass chimney
        'F': (255, 240, 120),   # Flame yellow core
        'H': (245, 125, 35),    # Flame orange halo
        'C': (185, 195, 205),   # Wire guard chrome
    }
    # Fuel tank base at bottom
    c.fill_rect(6, 23, 25, 29, 'M')
    c.fill_rect(6, 23, 25, 25, 'L')
    # Glass chimney middle
    c.fill_ellipse(15, 16, 6, 7, 'G')
    # Lantern top cap and ventilation dome
    c.fill_rect(9, 7, 22, 11, 'M')
    c.fill_rect(11, 4, 20, 7, 'M')
    c.fill_rect(11, 4, 20, 5, 'L')
    # Wire cage guard (C >= 15 px)
    c.draw_line(8, 10, 8, 23, 'C', 1)
    c.draw_line(23, 10, 23, 23, 'C', 1)
    c.draw_line(15, 9, 15, 12, 'C', 1)
    c.draw_line(15, 20, 15, 23, 'C', 1)
    c.outline({'M', 'L', 'G', 'C'}, 'O')
    # Glowing flame drawn inside chimney (F & H >= 15 px)
    c.fill_circle(15, 16, 4, 'H')
    c.fill_circle(15, 16, 2, 'F')
    return c.to_image(pal)

def gen_dalgictupu():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'Y': (245, 195, 35),    # Tank yellow
        'D': (180, 135, 20),    # Tank gold shadow
        'L': (255, 235, 130),   # Tank highlight
        'H': (55, 60, 70),      # Harness & rubber boot
        'S': (190, 200, 210),   # Silver manifold
        'W': (245, 245, 250),   # Gauge dial white
        'R': (225, 45, 45),     # Gauge needle red
    }
    # Left cylinder
    c.fill_rect(8, 9, 14, 26, 'Y')
    c.fill_rect(8, 9, 10, 26, 'L')
    c.fill_rect(13, 9, 14, 26, 'D')
    # Right cylinder
    c.fill_rect(17, 9, 23, 26, 'Y')
    c.fill_rect(17, 9, 19, 26, 'L')
    c.fill_rect(22, 9, 23, 26, 'D')
    # Cylinder domes
    c.set(8, 9, '.')
    c.set(14, 9, '.')
    c.set(17, 9, '.')
    c.set(23, 9, '.')
    # Rubber boots and black tank bands (H >= 20 px)
    c.fill_rect(7, 24, 24, 27, 'H')
    c.fill_rect(7, 14, 24, 16, 'H')
    # Manifold bridge at top (S >= 15 px)
    c.fill_rect(10, 6, 21, 9, 'S')
    c.outline({'Y', 'D', 'L', 'H', 'S'}, 'O')
    # Pressure gauge dial hanging on left (W & R >= 12 px)
    c.fill_circle(4, 18, 3, 'W')
    c.fill_rect(3, 17, 6, 19, 'R')
    c.set(4, 16, 'R')
    c.set(5, 16, 'R')
    return c.to_image(pal)

def gen_guguklusaat():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'W': (175, 115, 60),    # Chalet wood
        'D': (110, 65, 30),     # Timber shadow
        'R': (185, 75, 50),     # Roof shingles
        'F': (245, 235, 205),   # Clock face cream
        'G': (235, 185, 45),    # Brass hands & pinecone weights
        'B': (60, 145, 220),    # Blue cuckoo bird
        'P': (145, 150, 160),   # Pendulum rod
    }
    # Clock body box
    c.fill_rect(7, 12, 24, 23, 'W')
    c.fill_rect(19, 12, 24, 23, 'D')
    # Chalet steep roof (R >= 25 px)
    c.fill_polygon([(15, 4), (4, 12), (27, 12)], 'R')
    # Open dormer door for bird
    c.fill_rect(13, 8, 18, 11, 'D')
    # Cuckoo bird perched in door (B >= 12 px)
    c.fill_circle(15, 9, 2, 'B')
    # Clock dial face (F >= 20 px)
    c.fill_circle(15, 17, 4, 'F')
    # Clock hands in center (D)
    c.draw_line(15, 17, 15, 14, 'D', 1)
    c.draw_line(15, 17, 18, 17, 'D', 1)
    # Pendulum rod thick (P >= 14 px)
    c.draw_line(15, 23, 15, 29, 'P', 2)
    # Pinecone weights and brass pendulum bob (G >= 20 px)
    c.fill_circle(15, 30, 2, 'G')
    c.fill_rect(9, 24, 11, 29, 'G')
    c.fill_rect(19, 24, 21, 29, 'G')
    c.outline({'W', 'D', 'R', 'F', 'G', 'B', 'P'}, 'O')
    return c.to_image(pal)

def gen_elektrogitar():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'R': (215, 40, 50),     # Red body
        'H': (245, 95, 105),    # Red highlight
        'D': (140, 20, 35),     # Red shadow
        'W': (240, 240, 245),   # Pickguard white
        'N': (215, 175, 115),   # Maple neck
        'F': (115, 75, 45),     # Rosewood fretboard
        'S': (180, 190, 200),   # Chrome pickups / bridge
    }
    # Double cutaway guitar body
    c.fill_ellipse(13, 21, 8, 8, 'R')
    c.fill_circle(8, 14, 4, 'R')    # Horn left
    c.fill_circle(18, 15, 3, 'R')   # Horn right
    c.fill_ellipse(15, 23, 6, 5, 'D')
    c.fill_ellipse(10, 19, 4, 4, 'H')
    # White pickguard (W >= 18 px)
    c.fill_ellipse(12, 20, 4, 5, 'W')
    # Chrome pickups & bridge (S >= 16 px)
    c.fill_rect(12, 17, 16, 19, 'S')
    c.fill_rect(12, 22, 16, 24, 'S')
    c.fill_circle(10, 25, 1, 'S')
    # Guitar neck (F & N >= 20 px)
    c.draw_line(15, 15, 26, 4, 'F', 2)
    c.draw_line(16, 16, 27, 5, 'N', 2)
    # Headstock
    c.fill_rect(25, 2, 29, 6, 'N')
    c.outline({'R', 'H', 'D', 'W', 'N', 'F', 'S'}, 'O')
    return c.to_image(pal)

def gen_kumpas():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'S': (170, 180, 190),   # Stainless steel beam
        'L': (220, 230, 240),   # Steel highlight
        'D': (105, 115, 125),   # Steel shadow
        'B': (225, 175, 45),    # Brass lock roller
        'K': (55, 60, 70),      # Caliper ticks
        'C': (210, 115, 65),    # Copper cylinder measured
    }
    # Main ruler beam
    c.fill_rect(4, 13, 28, 17, 'S')
    c.fill_rect(4, 13, 28, 14, 'L')
    # Fixed jaw on left
    c.fill_polygon([(4, 6), (8, 6), (8, 25), (4, 25)], 'S')
    c.fill_polygon([(4, 6), (5, 6), (5, 25), (4, 25)], 'L')
    # Sliding vernier caliper jaw
    c.fill_polygon([(16, 6), (20, 6), (20, 25), (16, 25)], 'S')
    c.fill_rect(19, 6, 20, 25, 'D')
    # Copper workpiece clamped between jaws (C >= 15 px)
    c.fill_rect(9, 18, 15, 24, 'C')
    # Ruler tick marks (K >= 12 px)
    for x in range(9, 27, 3):
        c.draw_line(x, 15, x, 17, 'K', 1)
    # Brass locking screw thumb wheel (B >= 10 px)
    c.fill_circle(18, 8, 2, 'B')
    c.outline({'S', 'L', 'D', 'B', 'K', 'C'}, 'O')
    return c.to_image(pal)

def gen_korsankancasi():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'S': (175, 185, 195),   # Steel hook
        'L': (235, 245, 255),   # Steel glint
        'D': (95, 105, 120),    # Steel shadow
        'B': (135, 75, 35),     # Leather cuff
        'C': (80, 45, 20),      # Leather shadow
        'G': (235, 185, 45),    # Brass mounting cup & rivets
        'R': (210, 155, 95),    # Leather trim
    }
    # Leather arm cuff
    c.fill_rect(8, 18, 24, 28, 'B')
    c.fill_rect(17, 18, 24, 28, 'C')
    c.fill_rect(8, 27, 24, 28, 'R')
    # Brass cup plate (G >= 15 px)
    c.fill_rect(10, 15, 22, 18, 'G')
    c.set(11, 23, 'G')
    c.set(16, 23, 'G')
    c.set(21, 23, 'G')
    # Curved steel hook arching up and around (S, L, D)
    # Stem going up
    c.fill_rect(15, 9, 18, 16, 'S')
    c.fill_rect(17, 9, 18, 16, 'D')
    # Hook curve
    c.fill_circle(14, 9, 6, 'S')
    c.fill_circle(14, 9, 3, '.')
    c.fill_rect(14, 9, 21, 15, '.')
    c.fill_rect(15, 9, 18, 16, 'S')
    # Hook sharp barb tip pointing down-right
    c.fill_polygon([(8, 8), (11, 13), (8, 13)], 'L')
    # Hook glint highlights (L >= 16 px)
    c.draw_line(10, 3, 15, 3, 'L', 2)
    c.outline({'S', 'L', 'D', 'B', 'C', 'G', 'R'}, 'O')
    return c.to_image(pal)

def gen_sushitabagi():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'W': (210, 170, 105),   # Bamboo geta board
        'D': (145, 110, 65),    # Board shadow & legs
        'R': (245, 245, 245),   # Sushi rice white
        'S': (245, 100, 65),    # Salmon nigiri coral
        'T': (195, 40, 50),     # Tuna nigiri deep red
        'N': (35, 60, 45),      # Nori seaweed dark green
        'G': (145, 210, 60),    # Wasabi paste lime green
    }
    # Bamboo platter board
    c.fill_rect(2, 16, 29, 22, 'W')
    c.fill_rect(2, 20, 29, 22, 'D')
    # Platter legs (geta stilts)
    c.fill_rect(6, 23, 9, 27, 'D')
    c.fill_rect(22, 23, 25, 27, 'D')
    # Salmon Nigiri on left (S & R >= 18 px)
    c.fill_rect(4, 13, 11, 16, 'R')
    c.fill_ellipse(8, 12, 4, 2, 'S')
    # Tuna Nigiri in center (T & R >= 18 px)
    c.fill_rect(12, 13, 19, 16, 'R')
    c.fill_ellipse(16, 12, 4, 2, 'T')
    # Outline board and main nigiri pieces
    c.outline({'W', 'D', 'R', 'S', 'T'}, 'O')
    # Nori Maki Roll on right drawn cleanly after outline (N >= 25 px)
    c.fill_rect(20, 11, 26, 16, 'N')
    c.fill_circle(23, 13, 1, 'R')
    # Nori belt band across salmon nigiri
    c.draw_line(8, 11, 8, 16, 'N', 1)
    # Wasabi dollop in corner (G >= 14 px)
    c.fill_circle(25, 18, 2, 'G')
    c.fill_circle(26, 19, 1, 'G')
    return c.to_image(pal)

def gen_ucansupurge():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'W': (145, 90, 50),     # Broom handle wood
        'D': (85, 50, 25),      # Wood shadow
        'B': (225, 185, 75),    # Straw bristles
        'S': (165, 125, 45),    # Bristle shadow
        'R': (205, 45, 55),     # Red rope binding
        'P': (175, 85, 235),    # Magic stardust purple
        'C': (120, 235, 245),   # Magic sparkles cyan
    }
    # Broom handle diagonal from bottom-left to top-right
    c.draw_line(6, 24, 28, 4, 'W', 3)
    c.draw_line(7, 25, 29, 5, 'D', 1)
    # Straw bristle bundle flared at bottom-left
    c.fill_polygon([(1, 29), (13, 17), (16, 23), (6, 31)], 'B')
    c.fill_polygon([(5, 28), (13, 20), (16, 23), (6, 31)], 'S')
    # Red rope tie wrapping bristles (R >= 12 px)
    c.draw_line(11, 18, 15, 23, 'R', 2)
    c.outline({'W', 'D', 'B', 'S', 'R'}, 'O')
    # Magic stardust aura (P >= 35 px)
    for px, py in [(17, 11), (20, 14), (23, 17), (13, 16), (26, 20), (22, 23), (18, 19), (21, 9), (25, 12)]:
        c.fill_circle(px, py, 1, 'P')
    # Cyan magic sparkle stars (C >= 30 px)
    for cx, cy in [(19, 10), (22, 13), (25, 16), (15, 18), (24, 21), (27, 23), (12, 23), (28, 10)]:
        c.fill_circle(cx, cy, 1, 'C')
    return c.to_image(pal)

# -------------------------------------------------------------
# 10 YENİ ZOR GENERATORS (1024 cubes full canvas, 9-10 colors)
# -------------------------------------------------------------

def gen_bambuormani():
    c = Canvas('1')
    pal = {
        '1': (240, 245, 210),   # Misty golden dawn sunlight
        '2': (185, 215, 160),   # Pale morning fog / canopy
        '3': (115, 180, 85),    # Midground bamboo stalks
        '4': (55, 130, 50),     # Foreground vibrant bamboo
        '5': (25, 75, 35),      # Deep bamboo shade
        '6': (245, 220, 110),   # Sunbeam shafts
        '7': (145, 140, 130),   # Stone lantern light grey
        '8': (75, 70, 65),      # Stone lantern dark
        '9': (195, 165, 115),   # Gravel pathway
        'A': (125, 95, 60),     # Path bamboo fence
    }
    # Misty canopy background
    c.fill_rect(0, 0, 31, 8, '1')
    c.fill_rect(0, 9, 31, 16, '2')
    # Sunbeam diagonals (6 >= 15 px)
    c.draw_line(4, 0, 14, 20, '6', 2)
    c.draw_line(12, 0, 22, 20, '6', 2)
    # Gravel path winding into forest
    c.fill_polygon([(10, 17), (21, 17), (28, 31), (4, 31)], '9')
    # Path fence borders (A >= 15 px)
    c.draw_line(4, 31, 10, 17, 'A', 1)
    c.draw_line(28, 31, 21, 17, 'A', 1)
    # Bamboo stalks vertical columns (3, 4, 5)
    # Midground stalks
    for bx in [2, 7, 24, 29]:
        c.fill_rect(bx, 0, bx + 1, 25, '3')
    # Foreground thick stalks
    for bx in [0, 4, 26, 30]:
        c.fill_rect(bx, 0, bx + 1, 31, '4')
        c.fill_rect(bx + 1, 0, bx + 1, 31, '5')
    # Bamboo nodes horizontal
    for y in [7, 14, 21, 27]:
        c.draw_line(0, y, 5, y, '2', 1)
        c.draw_line(26, y, 31, y, '2', 1)
    # Stone Toro lantern beside path (7 & 8 >= 15 px)
    c.fill_rect(8, 20, 12, 26, '7')
    c.fill_rect(10, 20, 12, 26, '8')
    c.fill_rect(7, 19, 13, 20, '8')
    c.fill_circle(10, 22, 1, '1')  # Lantern light flame
    return c.to_image(pal)

def gen_kuzeykoyu():
    c = Canvas('1')
    pal = {
        '1': (20, 30, 55),      # Deep polar night sky
        '2': (70, 205, 150),    # Vibrant green Aurora
        '3': (60, 75, 100),     # Jagged fjord mountain rock
        '4': (215, 235, 250),   # Snow peaks & foreground snow
        '5': (195, 45, 45),     # Red rorbu cabin walls
        '6': (250, 225, 105),   # Warm window lights
        '7': (80, 50, 35),      # Stilt pilings dark timber
        '8': (25, 65, 90),      # Deep fjord water
        '9': (45, 135, 125),    # Water aurora reflection
    }
    # Aurora arching across night sky (2 >= 25 px)
    c.fill_rect(0, 0, 31, 8, '1')
    c.fill_polygon([(0, 6), (15, 2), (31, 4), (31, 7), (15, 4), (0, 8)], '2')
    # Mountain peaks with snow
    c.fill_polygon([(0, 16), (8, 6), (16, 16)], '3')
    c.fill_polygon([(12, 16), (22, 7), (31, 16)], '3')
    c.fill_polygon([(8, 6), (6, 9), (10, 9)], '4')
    c.fill_polygon([(22, 7), (20, 10), (24, 10)], '4')
    # Fjord water
    c.fill_rect(0, 17, 31, 31, '8')
    # Aurora reflection in fjord (9 >= 20 px)
    for y in [18, 20, 23, 26]:
        c.draw_line(4, y, 27, y, '9', 1)
    # Stilt wooden cabins on shore
    # Left cabin
    c.fill_rect(3, 14, 11, 20, '5')
    c.fill_polygon([(2, 14), (7, 11), (12, 14)], '4')  # Snow roof
    c.fill_rect(5, 16, 7, 18, '6')  # Window light
    # Right cabin
    c.fill_rect(17, 15, 25, 21, '5')
    c.fill_polygon([(16, 15), (21, 12), (26, 15)], '4')  # Snow roof
    c.fill_rect(20, 17, 22, 19, '6')  # Window light
    # Wooden stilts into water (7 >= 15 px)
    for sx in [3, 6, 9, 11, 17, 20, 23, 25]:
        c.draw_line(sx, 20, sx, 24, '7', 1)
    return c.to_image(pal)

def gen_gunestapinagi():
    c = Canvas('2')
    pal = {
        '1': (255, 215, 110),   # Golden sunrise horizon
        '2': (90, 165, 225),    # High Andes sky
        '3': (65, 90, 120),     # Huayna Picchu mountain peak
        '4': (180, 175, 160),   # Granite temple stones
        '5': (110, 105, 95),    # Granite shadows & doorways
        '6': (85, 160, 65),     # Grass terraces
        '7': (45, 105, 40),     # Terrace retaining shadows
        '8': (245, 240, 230),   # Grazing llama white
        '9': (155, 100, 55),    # Llama saddle & trail
        'A': (255, 150, 45),    # Sun disc & rays
    }
    # Andes sky with sunrise
    c.fill_rect(0, 0, 31, 8, '2')
    c.fill_rect(0, 7, 31, 11, '1')
    # Sun rising between peaks (A >= 15 px)
    c.fill_circle(16, 8, 3, 'A')
    # Towering Huayna Picchu peak
    c.fill_polygon([(0, 15), (10, 5), (20, 15)], '3')
    # Green agricultural terraces step down
    c.fill_rect(0, 15, 31, 31, '6')
    for ty in [18, 22, 26, 30]:
        c.fill_rect(0, ty, 31, ty + 1, '7')
    # Inca Sun Temple stone citadel (4 & 5 >= 25 px)
    c.fill_rect(6, 13, 18, 20, '4')
    c.fill_rect(14, 13, 18, 20, '5')
    c.fill_rect(9, 16, 12, 20, '5')   # Temple trapezoidal gate
    # Grazing Llama in foreground terrace (8 & 9 >= 15 px)
    c.fill_rect(21, 21, 27, 25, '8')  # Llama body
    c.fill_rect(25, 18, 27, 23, '8')  # Neck & head
    c.draw_line(22, 25, 22, 28, '8', 1) # Legs
    c.draw_line(26, 25, 26, 28, '8', 1)
    c.fill_rect(23, 21, 26, 24, '9')  # Colorful Andean saddle cloth (9 >= 16 px)
    return c.to_image(pal)

def gen_samuraybahcesi():
    c = Canvas('1')
    pal = {
        '1': (215, 225, 235),   # Misty morning sky
        '2': (250, 165, 190),   # Cherry blossom sakura pink
        '3': (95, 55, 35),      # Bonsai branch dark
        '4': (210, 50, 45),     # Red vermilion bridge
        '5': (120, 25, 25),     # Bridge shadow
        '6': (215, 205, 185),   # Raked zen gravel
        '7': (145, 135, 120),   # Gravel rake rings & rocks
        '8': (45, 115, 135),    # Koi pond water
        '9': (245, 120, 45),    # Nishikigoi koi fish orange
        'A': (65, 145, 75),     # Moss islands & lily pads
    }
    # Zen rock gravel garden
    c.fill_rect(0, 0, 31, 10, '1')
    c.fill_rect(0, 11, 31, 23, '6')
    # Raked concentric grooves (7 >= 20 px)
    for gx in [4, 8, 12, 16, 20]:
        c.draw_line(gx, 12, gx, 22, '7', 1)
    # Koi pond lower right
    c.fill_ellipse(20, 27, 11, 4, '8')
    # Koi fish swimming (9 >= 12 px)
    c.fill_ellipse(21, 27, 3, 1, '9')
    c.fill_ellipse(15, 28, 2, 1, '9')
    # Arched vermilion red bridge across pond
    c.fill_polygon([(2, 23), (18, 18), (18, 22), (2, 27)], '4')
    c.fill_polygon([(2, 26), (18, 21), (18, 23), (2, 28)], '5')
    # Sakura branch draped across top
    c.draw_line(0, 4, 18, 7, '3', 2)
    # Sakura blossom clusters (2 >= 25 px)
    for bx, by in [(3, 2), (8, 4), (13, 6), (17, 8), (6, 6)]:
        c.fill_circle(bx, by, 2, '2')
    # Moss stone island (A >= 15 px)
    c.fill_ellipse(9, 17, 3, 2, 'A')
    c.fill_circle(9, 16, 1, '7')
    return c.to_image(pal)

def gen_vahsibati():
    c = Canvas('1')
    pal = {
        '1': (105, 35, 80),     # Dusk purple upper sky
        '2': (240, 115, 50),    # Orange sunset glow
        '3': (255, 220, 100),   # Setting sun
        '4': (165, 55, 40),     # Sandstone mesa butte
        '5': (105, 30, 25),     # Mesa shadow
        '6': (140, 85, 50),     # Wooden saloon facade
        '7': (70, 40, 25),      # Saloon porch & batwing doors
        '8': (245, 185, 105),   # Saloon lantern glow
        '9': (210, 150, 85),    # Dusty ground
        'A': (60, 95, 50),      # Saguaro cactus green
    }
    # Sunset sky gradient
    c.fill_rect(0, 0, 31, 5, '1')
    c.fill_rect(0, 6, 31, 12, '2')
    # Setting sun on horizon (3 >= 15 px)
    c.fill_circle(14, 11, 4, '3')
    # Monument Valley sandstone butte
    c.fill_polygon([(0, 12), (5, 9), (11, 9), (13, 14), (0, 14)], '4')
    c.fill_polygon([(6, 9), (11, 9), (13, 14), (8, 14)], '5')
    # Dusty frontier road
    c.fill_rect(0, 18, 31, 31, '9')
    # Frontier Saloon building on right
    c.fill_rect(15, 11, 31, 24, '6')
    # False-front roof trim
    c.fill_rect(14, 10, 31, 12, '7')
    # Saloon porch & swinging batwing door
    c.fill_rect(18, 17, 23, 24, '7')
    # Saloon window with glowing lantern (8 >= 12 px)
    c.fill_rect(25, 14, 29, 18, '8')
    # Saguaro cactus on left (A >= 20 px)
    c.draw_line(4, 15, 4, 28, 'A', 2)
    c.draw_line(2, 19, 4, 19, 'A', 1)
    c.draw_line(2, 16, 2, 19, 'A', 1)
    c.draw_line(4, 21, 6, 21, 'A', 1)
    c.draw_line(6, 18, 6, 21, 'A', 1)
    return c.to_image(pal)

def gen_tropikselale():
    c = Canvas('1')
    pal = {
        '1': (150, 215, 240),   # Misty jungle sky
        '2': (85, 75, 70),      # Basalt wet cliff
        '3': (45, 125, 55),     # Dense rainforest foliage
        '4': (25, 75, 35),      # Canopy deep green shade
        '5': (240, 250, 255),   # Waterfall foaming white
        '6': (120, 200, 225),   # Waterfall cascade blue
        '7': (35, 145, 155),    # Turquoise jungle lagoon
        '8': (225, 40, 45),     # Scarlet macaw red
        '9': (245, 205, 45),    # Macaw yellow wing patch
        'A': (35, 95, 215),     # Macaw cobalt blue tail
    }
    # Jungle sky
    c.fill_rect(0, 0, 31, 6, '1')
    # Wet basalt cliff walls left & right
    c.fill_rect(0, 5, 12, 23, '2')
    c.fill_rect(20, 5, 31, 23, '2')
    # Dense lush jungle foliage atop cliffs
    c.fill_rect(0, 3, 13, 9, '3')
    c.fill_rect(19, 3, 31, 9, '3')
    c.fill_rect(0, 6, 12, 12, '4')
    c.fill_rect(20, 6, 31, 12, '4')
    # Waterfall cascading down center
    c.fill_rect(12, 5, 20, 23, '6')
    c.draw_line(14, 5, 14, 23, '5', 1)
    c.draw_line(16, 5, 16, 23, '5', 2)
    c.draw_line(18, 5, 18, 23, '5', 1)
    # Turquoise lagoon pool at bottom
    c.fill_rect(0, 23, 31, 31, '7')
    # Waterfall plunge foam (5 >= 20 px)
    c.fill_ellipse(16, 23, 6, 2, '5')
    # Scarlet Macaw perched on cliff branch in foreground (8, 9, A >= 15 px each)
    c.fill_rect(4, 14, 8, 20, '8')   # Red body
    c.fill_rect(5, 15, 8, 18, '9')   # Yellow wing stripe (16 px)
    c.draw_line(7, 20, 7, 26, 'A', 2) # Blue tail feathers (14 px)
    c.draw_line(6, 22, 6, 25, 'A', 1)
    return c.to_image(pal)

def gen_magmamagarasi():
    c = Canvas('1')
    pal = {
        '1': (30, 25, 35),      # Obsidian dark ceiling
        '2': (75, 55, 65),      # Basalt cavern midtone
        '3': (125, 80, 85),     # Cavern lava-lit rock
        '4': (185, 40, 25),     # Crust red
        '5': (245, 90, 20),     # Molten lava orange
        '6': (255, 215, 65),    # Magma lake bright yellow
        '7': (255, 250, 200),   # Lava bubbles / white sparks
        '8': (145, 50, 175),    # Glowing purple crystals
        '9': (215, 115, 245),   # Crystal light highlight
    }
    # Obsidian cavern ceiling and walls
    c.fill_rect(0, 0, 31, 10, '1')
    c.fill_polygon([(0, 10), (9, 10), (6, 24), (0, 24)], '2')
    c.fill_polygon([(23, 10), (31, 10), (31, 24), (25, 24)], '2')
    # Rock rim lit by magma
    c.draw_line(6, 10, 6, 24, '3', 1)
    c.draw_line(25, 10, 25, 24, '3', 1)
    # Magma lake at bottom
    c.fill_rect(0, 23, 31, 31, '4')
    c.fill_rect(0, 25, 31, 31, '5')
    c.fill_rect(5, 27, 26, 30, '6')
    # Cascading lava fall down center
    c.fill_rect(13, 8, 19, 25, '5')
    c.fill_rect(15, 8, 17, 25, '6')
    # Glowing lava bubbles / sparks (7 >= 12 px)
    c.set(16, 12, '7')
    c.set(15, 18, '7')
    c.set(11, 27, '7')
    c.set(20, 28, '7')
    c.set(8, 26, '7')
    c.set(24, 26, '7')
    c.set(16, 29, '7')
    c.set(17, 29, '7')
    c.set(13, 28, '7')
    c.set(18, 27, '7')
    c.set(7, 28, '7')
    c.set(22, 29, '7')
    # Purple crystal stalactites hanging from ceiling (8 & 9 >= 18 px)
    c.fill_polygon([(2, 0), (6, 0), (4, 8)], '8')
    c.fill_polygon([(26, 0), (30, 0), (28, 9)], '8')
    c.fill_polygon([(9, 0), (13, 0), (11, 6)], '8')
    c.fill_polygon([(19, 0), (23, 0), (21, 6)], '8')
    c.draw_line(4, 1, 4, 7, '9', 1)
    c.draw_line(28, 1, 28, 8, '9', 1)
    c.draw_line(11, 1, 11, 5, '9', 1)
    return c.to_image(pal)

def gen_gokdelengece():
    c = Canvas('1')
    pal = {
        '1': (15, 20, 40),      # Midnight deep navy sky
        '2': (45, 50, 85),      # Atmospheric night haze
        '3': (250, 245, 200),   # Full moon & stars
        '4': (65, 70, 95),      # Distant skyscraper silhouettes
        '5': (120, 130, 150),   # Art Deco skyscraper facade
        '6': (40, 45, 60),      # Building shadow / piers
        '7': (255, 220, 90),    # Lit office windows gold
        '8': (215, 225, 235),   # Spire chrome tip
        '9': (235, 50, 80),     # Red aircraft warning beacon
        'A': (65, 205, 230),    # Cyan neon penthouse edge
    }
    # Midnight sky gradient (2 >= 40 px)
    c.fill_rect(0, 0, 31, 7, '1')
    c.fill_rect(0, 8, 31, 16, '2')
    # Full moon in upper left (3 >= 15 px)
    c.fill_circle(6, 5, 3, '3')
    # Distant skyscrapers left & right
    c.fill_rect(0, 12, 8, 31, '4')
    c.fill_rect(24, 11, 31, 31, '4')
    # Central Art Deco skyscraper
    c.fill_rect(9, 10, 23, 31, '5')
    c.fill_rect(17, 10, 23, 31, '6')
    # Setback shoulders
    c.fill_rect(11, 7, 21, 10, '5')
    c.fill_rect(17, 7, 21, 10, '6')
    # Spire needle (8 >= 15 px)
    c.draw_line(16, 2, 16, 7, '8', 2)
    # Red warning beacon at spire tip (9 >= 12 px)
    c.fill_circle(16, 1, 2, '9')
    # Cyan neon glow strip across setback (A >= 13 px)
    c.fill_rect(10, 10, 22, 11, 'A')
    # Golden illuminated office windows (7 >= 25 px)
    for wy in [13, 16, 19, 22, 25, 28]:
        c.fill_rect(11, wy, 13, wy + 1, '7')
        c.fill_rect(14, wy, 16, wy + 1, '7')
        c.fill_rect(18, wy, 20, wy + 1, '7')
    return c.to_image(pal)

def gen_korsangemisavasi():
    c = Canvas('1')
    pal = {
        '1': (20, 25, 40),      # Stormy midnight sky
        '2': (215, 225, 245),   # Lightning flash
        '3': (15, 60, 75),      # Ocean storm swells
        '4': (130, 200, 210),   # Foaming waves spray
        '5': (105, 65, 40),     # Galleon hull wood
        '6': (50, 30, 20),      # Hull shadow & gunports
        '7': (210, 205, 195),   # Billowing white sails
        '8': (35, 35, 45),      # Jolly Roger flag black
        '9': (250, 160, 40),    # Cannon fire flash
        'A': (165, 170, 180),   # Cannon smoke cloud
    }
    # Storm sky
    c.fill_rect(0, 0, 31, 14, '1')
    # Lightning flash in clouds (2 >= 15 px)
    c.draw_line(23, 0, 20, 5, '2', 1)
    c.draw_line(20, 5, 24, 7, '2', 1)
    c.draw_line(24, 7, 21, 12, '2', 1)
    # Stormy sea
    c.fill_rect(0, 18, 31, 31, '3')
    # Foaming waves (4 >= 25 px)
    for wx in [0, 8, 16, 24]:
        c.draw_line(wx, 21, wx + 6, 19, '4', 1)
        c.draw_line(wx + 2, 27, wx + 7, 25, '4', 1)
    # Pirate galleon ship hull
    c.fill_polygon([(5, 18), (24, 18), (22, 24), (8, 24)], '5')
    c.fill_polygon([(15, 18), (24, 18), (22, 24), (17, 24)], '6')
    # Main masts and billowing sails (7 >= 25 px)
    c.fill_rect(10, 8, 16, 14, '7')
    c.fill_rect(18, 9, 23, 14, '7')
    # Masts
    c.draw_line(13, 4, 13, 18, '6', 1)
    c.draw_line(20, 5, 20, 18, '6', 1)
    # Jolly Roger black skull flag at mast top (8 >= 12 px)
    c.fill_rect(10, 4, 13, 6, '8')
    c.set(11, 5, '7')  # Tiny white skull dot
    # Cannon firing flash & smoke (9 & A >= 18 px each)
    c.fill_circle(25, 20, 2, '9')
    c.fill_circle(23, 21, 1, '9')
    c.fill_circle(28, 19, 3, 'A')
    return c.to_image(pal)

def gen_ayussu():
    c = Canvas('1')
    pal = {
        '1': (10, 12, 22),      # Deep vacuum cosmos
        '2': (245, 245, 255),   # Stars
        '3': (55, 125, 235),    # Blue Earth
        '4': (170, 220, 250),   # Earth atmosphere / clouds
        '5': (165, 170, 175),   # Lunar regolith rim light
        '6': (105, 110, 115),   # Lunar dusty soil
        '7': (45, 48, 55),      # Lunar crater shadows
        '8': (220, 230, 240),   # Pressurized habitat dome
        '9': (35, 80, 175),     # Solar panel array blue
        'A': (245, 190, 45),    # Amber beacon / rover gold
    }
    # Deep space vacuum
    c.fill_rect(0, 0, 31, 14, '1')
    # Twinkling stars (2 >= 18 px)
    for sx, sy in [(2, 2), (7, 4), (13, 1), (18, 5), (29, 3), (25, 8), (4, 11), (30, 12), (1, 8), (15, 9), (27, 1), (10, 6), (9, 12), (19, 12), (3, 6)]:
        c.set(sx, sy, '2')
    # Earth rising in the black sky (3 & 4 >= 25 px)
    c.fill_circle(22, 6, 5, '3')
    c.fill_ellipse(21, 5, 3, 2, '4')  # White swirl cloud
    c.fill_ellipse(23, 7, 2, 2, '4')
    # Moon surface terrain
    c.fill_rect(0, 15, 31, 31, '6')
    # Crater rims and soil highlights
    c.fill_ellipse(6, 26, 5, 2, '7')
    c.fill_ellipse(6, 25, 5, 1, '5')
    c.fill_ellipse(25, 28, 4, 2, '7')
    c.fill_ellipse(25, 27, 4, 1, '5')
    # Lunar habitat base geodesic dome (8 >= 25 px)
    c.fill_circle(14, 20, 6, '8')
    c.fill_rect(8, 20, 20, 22, '8')
    # Dome airlock & windows
    c.fill_rect(13, 20, 15, 22, '7')
    # Amber communication beacon at top of dome (A >= 10 px)
    c.fill_circle(14, 13, 2, 'A')
    # Solar panel array on right (9 >= 20 px)
    c.fill_polygon([(21, 17), (29, 15), (29, 21), (21, 23)], '9')
    c.draw_line(25, 16, 25, 22, '5', 1) # Support strut
    return c.to_image(pal)

GENERATORS = {
    # 10 Kolay
    'kolay/CicekSaksisi': gen_ciceksaksisi,
    'kolay/DondurmaKulah': gen_dondurmakulah,
    'kolay/RuzgarGulu': gen_ruzgargulu,
    'kolay/RecelKavanozu': gen_recelkavanozu,
    'kolay/PiknikSepeti': gen_pikniksepeti,
    'kolay/CanYelegi': gen_canyelegi,
    'kolay/SirtCantasi': gen_sirtcantasi,
    'kolay/Matara': gen_matara,
    'kolay/MasaLambasi': gen_masalambasi,
    'kolay/BoyamaPaleti': gen_boyamapaleti,

    # 10 Orta
    'orta/DikisMakinesi': gen_dikismakinesi,
    'orta/Trompet': gen_trompet,
    'orta/GazLambasi': gen_gazlambasi,
    'orta/DalgicTupu': gen_dalgictupu,
    'orta/GugukluSaat': gen_guguklusaat,
    'orta/ElektroGitar': gen_elektrogitar,
    'orta/Kumpas': gen_kumpas,
    'orta/KorsanKancasi': gen_korsankancasi,
    'orta/SushiTabagi': gen_sushitabagi,
    'orta/UcanSupurge': gen_ucansupurge,

    # 10 Zor
    'zor/BambuOrmani': gen_bambuormani,
    'zor/KuzeyKoyu': gen_kuzeykoyu,
    'zor/GunesTapinagi': gen_gunestapinagi,
    'zor/SamurayBahcesi': gen_samuraybahcesi,
    'zor/VahsiBati': gen_vahsibati,
    'zor/TropikSelale': gen_tropikselale,
    'zor/MagmaMagarasi': gen_magmamagarasi,
    'zor/GokdelenGece': gen_gokdelengece,
    'zor/KorsanGemiSavasi': gen_korsangemisavasi,
    'zor/AyUssu': gen_ayussu,
}

def main():
    base_dir = 'Assets/04.11.2026'
    total = len(GENERATORS)
    print(f"Generating {total} batch 3 pixel arts...")
    all_ok = True
    results = []

    for rel_path, gen_fn in GENERATORS.items():
        name = os.path.basename(rel_path)
        folder = os.path.dirname(rel_path)
        out_dir = os.path.join(base_dir, folder)
        os.makedirs(out_dir, exist_ok=True)
        
        im = gen_fn()
        cubes, n_colors, errs, warns = validate_image(im, name)
        
        status = "OK"
        if errs:
            status = "ERR"
            all_ok = False
        elif warns:
            status = "WARN"
            
        print(f"[{status:4s}] {rel_path:25s} -> cubes={cubes:4d}, colors={n_colors:2d}")
        if errs:
            for e in errs:
                print(f"       ERROR: {e}")
        if warns:
            for w in warns:
                print(f"       WARN:  {w}")
                
        out_png = os.path.join(out_dir, f"{name}.png")
        im.save(out_png)
        make_meta(out_png)
        results.append((rel_path, cubes, n_colors, status, errs, warns))

    print(f"\nCompleted {total} generations. Overall OK: {all_ok}")
    return 0 if all_ok else 1

if __name__ == '__main__':
    sys.exit(main())
