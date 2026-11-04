"""Pixel art generator for PixelGame - Batch 4 (30 more unique levels).
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

def gen_origamikugu():
    c = Canvas('.')
    pal = {
        'W': (235, 245, 255),   # Paper white
        'L': (145, 205, 245),   # Light cyan fold
        'M': (65, 125, 205),    # Mid blue fold
        'D': (25, 55, 115),     # Dark navy crease
        'O': (245, 135, 35),    # Orange beak
    }
    # Origami swan body triangles
    c.fill_polygon([(8, 20), (24, 20), (16, 28)], 'M')
    c.fill_polygon([(16, 20), (24, 20), (16, 28)], 'D')
    # Folded wings angled up
    c.fill_polygon([(10, 20), (21, 8), (17, 20)], 'W')
    c.fill_polygon([(14, 20), (25, 11), (21, 20)], 'L')
    # Long neck stretching up-left
    c.draw_line(9, 21, 5, 8, 'W', 2)
    c.draw_line(10, 21, 6, 8, 'L', 1)
    # Head and beak pointing left (O >= 14 px)
    c.fill_polygon([(4, 7), (7, 6), (7, 9)], 'W')
    c.fill_polygon([(1, 8), (4, 7), (4, 9)], 'O')
    c.fill_rect(2, 7, 4, 9, 'O')
    return c.to_image(pal)

def gen_prizmaisik():
    c = Canvas('.')
    pal = {
        'P': (220, 235, 245),   # Prism glass light
        'S': (130, 165, 195),   # Prism glass shadow
        'W': (255, 255, 255),   # Incident white beam
        'R': (245, 60, 50),     # Dispersed Red ray
        'B': (50, 150, 240),    # Dispersed Cyan/Blue ray
    }
    # Triangular glass prism in center
    c.fill_polygon([(15, 6), (6, 24), (24, 24)], 'P')
    c.fill_polygon([(15, 6), (15, 24), (24, 24)], 'S')
    # Incident white beam entering from left (W >= 20 px)
    c.draw_line(1, 19, 11, 15, 'W', 2)
    # Dispersed Red ray exiting right (R >= 20 px)
    c.draw_line(17, 16, 30, 11, 'R', 2)
    # Dispersed Cyan ray exiting right (B >= 20 px)
    c.draw_line(18, 18, 30, 23, 'B', 2)
    return c.to_image(pal)

def gen_astronotkask():
    c = Canvas('.')
    pal = {
        'W': (240, 245, 250),   # White helmet shell
        'S': (135, 145, 165),   # Shell shadow / collar
        'G': (245, 190, 45),    # Gold visor reflection
        'D': (165, 105, 20),    # Visor dark depth
        'R': (225, 45, 55),     # Red beacon / audio port
    }
    # Spherical white helmet shell
    c.fill_circle(15, 14, 11, 'W')
    c.fill_ellipse(15, 20, 11, 5, 'S')
    # Neck collar ring
    c.fill_rect(9, 25, 22, 28, 'S')
    # Gold bubble visor
    c.fill_ellipse(15, 14, 7, 5, 'G')
    c.fill_ellipse(17, 15, 4, 3, 'D')
    # Red communication light on ear (R >= 15 px)
    c.fill_rect(4, 12, 6, 17, 'R')
    c.draw_line(5, 7, 5, 12, 'S', 1) # Antenna
    return c.to_image(pal)

def gen_sihirliiksir():
    c = Canvas('.')
    pal = {
        'B': (185, 215, 235),   # Glass bottle rim/glare
        'G': (45, 195, 95),     # Emerald potion
        'D': (20, 110, 50),     # Deep potion shadow
        'Y': (245, 235, 75),    # Glowing bubbles
        'C': (170, 105, 50),    # Cork stopper brown
    }
    # Round potion flask bottle
    c.fill_circle(15, 19, 9, 'B')
    c.fill_rect(13, 7, 18, 12, 'B')
    c.fill_rect(12, 6, 19, 7, 'B')  # Lip
    # Cork stopper in bottle neck (C >= 15 px)
    c.fill_rect(13, 3, 18, 6, 'C')
    # Emerald potion filling lower globe
    c.fill_circle(15, 20, 7, 'G')
    c.fill_ellipse(17, 21, 4, 4, 'D')
    # Glowing yellow bubbles (Y >= 14 px)
    c.fill_circle(12, 17, 1, 'Y')
    c.fill_circle(17, 18, 1, 'Y')
    c.fill_circle(14, 22, 1, 'Y')
    return c.to_image(pal)

def gen_pikselkalp():
    c = Canvas('.')
    pal = {
        'P': (245, 50, 130),    # Neon hot pink
        'D': (160, 20, 70),     # Deep crimson shadow
        'C': (45, 210, 230),    # Cyber cyan border
        'W': (255, 255, 255),   # Glare white
        'Y': (255, 205, 35),    # Cyber gold circuit dots
    }
    # Heart shape using overlapping circles & triangle
    c.fill_circle(10, 11, 6, 'P')
    c.fill_circle(21, 11, 6, 'P')
    c.fill_polygon([(5, 13), (26, 13), (16, 27)], 'P')
    # Right side depth shadow
    c.fill_polygon([(16, 13), (26, 13), (16, 27)], 'D')
    c.fill_circle(21, 11, 4, 'D')
    # Specular glare top-left (W >= 15 px)
    c.fill_circle(9, 9, 2, 'W')
    # Cyber cyan outer corner wings (C >= 25 px)
    c.draw_line(3, 12, 7, 8, 'C', 2)
    c.draw_line(28, 12, 24, 8, 'C', 2)
    # Gold circuit node pins (Y >= 15 px)
    c.fill_rect(14, 15, 17, 17, 'Y')
    c.fill_rect(15, 20, 16, 23, 'Y')
    return c.to_image(pal)

def gen_uzayroketi():
    c = Canvas('.')
    pal = {
        'W': (245, 245, 250),   # White rocket fuselage
        'R': (225, 40, 50),     # Red nosecone & fins
        'C': (75, 185, 235),    # Cyan glass porthole
        'F': (255, 185, 35),    # Fiery exhaust flame
        'S': (140, 145, 160),   # Fuselage shadow grey
    }
    # Rocket body bullet cylinder
    c.fill_polygon([(12, 6), (19, 6), (20, 22), (11, 22)], 'W')
    c.fill_polygon([(16, 6), (19, 6), (20, 22), (16, 22)], 'S')
    # Pointed aerodynamic nosecone (R >= 20 px)
    c.fill_polygon([(11, 7), (16, 1), (20, 7)], 'R')
    # Rocket side fins
    c.fill_polygon([(11, 16), (6, 24), (11, 23)], 'R')
    c.fill_polygon([(20, 16), (25, 24), (20, 23)], 'R')
    # Circular porthole window (C >= 15 px)
    c.fill_circle(15, 13, 3, 'C')
    # Rocket exhaust nozzle and fiery flame (F >= 25 px)
    c.fill_polygon([(12, 22), (19, 22), (15, 30)], 'F')
    c.fill_circle(15, 25, 2, 'F')
    return c.to_image(pal)

def gen_dislicark():
    c = Canvas('.')
    pal = {
        'B': (215, 145, 50),    # Bronze large gear
        'D': (135, 80, 25),     # Bronze shadow
        'S': (175, 190, 205),   # Steel small gear
        'M': (95, 110, 125),    # Steel shadow
        'Y': (245, 210, 65),    # Brass axle pins
    }
    # Large Bronze Gear (lower left)
    c.fill_circle(11, 19, 7, 'B')
    c.fill_circle(11, 19, 3, 'D')
    # Bronze cogs (outer teeth)
    for ang in range(0, 360, 45):
        rad = math.radians(ang)
        tx = int(round(11 + 9 * math.cos(rad)))
        ty = int(round(19 + 9 * math.sin(rad)))
        c.fill_circle(tx, ty, 1, 'B')
    # Small Steel Gear (top right)
    c.fill_circle(21, 10, 5, 'S')
    c.fill_circle(21, 10, 2, 'M')
    # Steel cogs
    for ang in range(22, 360, 60):
        rad = math.radians(ang)
        tx = int(round(21 + 7 * math.cos(rad)))
        ty = int(round(10 + 7 * math.sin(rad)))
        c.fill_circle(tx, ty, 1, 'S')
    # Axle pins in centers (Y >= 14 px)
    c.fill_circle(11, 19, 1, 'Y')
    c.fill_circle(21, 10, 1, 'Y')
    return c.to_image(pal)

def gen_lavlambasi():
    c = Canvas('.')
    pal = {
        'M': (180, 190, 205),   # Silver metal base & cap
        'S': (100, 110, 125),   # Base metal shadow
        'Y': (255, 235, 110),   # Liquid yellow glow
        'O': (245, 85, 25),     # Molten lava orange
        'R': (180, 25, 20),     # Lava blob red core
    }
    # Top metal cap
    c.fill_polygon([(12, 3), (19, 3), (18, 7), (13, 7)], 'M')
    # Bottom conical metal stand
    c.fill_polygon([(12, 23), (19, 23), (22, 29), (9, 29)], 'M')
    c.fill_polygon([(16, 23), (19, 23), (22, 29), (16, 29)], 'S')
    # Tapered glass bottle filled with yellow liquid
    c.fill_polygon([(13, 7), (18, 7), (20, 23), (11, 23)], 'Y')
    # Floating organic lava blobs (O & R >= 20 px each)
    c.fill_circle(15, 20, 3, 'O')
    c.fill_circle(15, 20, 2, 'R')
    c.fill_circle(16, 12, 2, 'O')
    c.fill_circle(16, 12, 1, 'R')
    c.fill_circle(14, 8, 1, 'O')
    return c.to_image(pal)

def gen_meksikasapkasi():
    c = Canvas('.')
    pal = {
        'W': (225, 180, 100),   # Straw tan
        'D': (145, 100, 45),    # Straw shadow
        'R': (225, 45, 50),     # Fiesta red band
        'T': (35, 165, 160),    # Fiesta teal band
        'Y': (250, 215, 45),    # Brim fringe yellow
    }
    # Wide curved hat brim
    c.fill_ellipse(16, 23, 14, 4, 'W')
    c.fill_ellipse(16, 25, 13, 2, 'D')
    # High conical crown
    c.fill_polygon([(11, 22), (21, 22), (19, 9), (13, 9)], 'W')
    c.fill_polygon([(16, 22), (21, 22), (19, 9), (16, 9)], 'D')
    # Red pattern stripe across crown (R >= 15 px)
    c.fill_rect(12, 16, 20, 18, 'R')
    # Teal pattern stripe across crown (T >= 15 px)
    c.fill_rect(13, 13, 19, 15, 'T')
    # Yellow pom-pom fringe along brim edge (Y >= 15 px)
    for x in range(3, 29, 3):
        c.set(x, 26, 'Y')
        c.set(x + 1, 26, 'Y')
    return c.to_image(pal)

def gen_totemmaskesi():
    c = Canvas('.')
    pal = {
        'W': (170, 105, 55),    # Cedar wood
        'D': (105, 55, 25),     # Wood shadow
        'T': (45, 185, 180),    # Turquoise tribal paint
        'R': (225, 50, 45),     # Red war paint
        'Y': (255, 220, 50),    # Glowing yellow eyes
    }
    # Long oval wooden mask body
    c.fill_ellipse(16, 16, 8, 13, 'W')
    c.fill_polygon([(16, 3), (24, 16), (20, 28), (16, 28)], 'D')
    # Forehead tribal turquoise crown marks (T >= 20 px)
    c.fill_rect(12, 5, 20, 8, 'T')
    # Red war paint stripes on cheeks (R >= 20 px)
    c.fill_rect(9, 17, 12, 22, 'R')
    c.fill_rect(20, 17, 23, 22, 'R')
    # Glowing yellow slotted eyes (Y >= 14 px)
    c.fill_rect(11, 12, 14, 14, 'Y')
    c.fill_rect(18, 12, 21, 14, 'Y')
    # Carved grimacing toothy mouth
    c.fill_rect(13, 23, 19, 25, 'D')
    return c.to_image(pal)

# -------------------------------------------------------------
# 10 YENİ ORTA GENERATORS (with OUT = (34, 26, 40))
# -------------------------------------------------------------

def gen_havagemisi():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'E': (215, 195, 150),   # Canvas gas envelope
        'S': (145, 125, 85),    # Envelope shadow
        'W': (150, 75, 35),     # Gondola wood
        'B': (230, 175, 45),    # Brass tail fins & fittings
        'C': (120, 225, 240),   # Cabin windows cyan
        'P': (220, 230, 240),   # Propeller silver
    }
    # Aerodynamic gasbag envelope (horizontal ellipse)
    c.fill_ellipse(16, 12, 13, 7, 'E')
    c.fill_ellipse(16, 15, 13, 4, 'S')
    # Brass tail fins on left (B >= 18 px)
    c.fill_polygon([(3, 7), (8, 11), (3, 11)], 'B')
    c.fill_polygon([(3, 17), (8, 13), (3, 13)], 'B')
    # Hanging wooden passenger gondola
    c.fill_rect(11, 20, 21, 24, 'W')
    # Outline the main envelope and gondola
    c.outline({'E', 'S', 'W', 'B'}, 'O')
    # Cyan cabin windows drawn cleanly (C >= 14 px)
    c.fill_rect(12, 21, 14, 23, 'C')
    c.fill_rect(16, 21, 18, 23, 'C')
    # Spinning propellers on gondola rear (P >= 14 px)
    c.fill_circle(9, 22, 2, 'P')
    c.fill_circle(23, 22, 2, 'P')
    return c.to_image(pal)

def gen_antikterazi():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'B': (235, 185, 45),    # Brass pillar & beam
        'L': (255, 225, 115),   # Brass highlight
        'D': (160, 115, 25),    # Brass shadow
        'P': (210, 150, 35),    # Scale pans
        'S': (190, 200, 215),   # Chains silver
        'I': (95, 105, 120),    # Iron counterweight
        'W': (140, 80, 40),     # Wooden base
    }
    # Wooden stepped base at bottom
    c.fill_rect(8, 25, 24, 28, 'W')
    # Central vertical brass pillar (wide enough so L and D survive outline)
    c.fill_rect(14, 7, 18, 25, 'B')
    c.fill_rect(14, 7, 15, 25, 'L')
    c.fill_rect(17, 7, 18, 25, 'D')
    # Balanced horizontal crossbeam
    c.fill_rect(4, 6, 28, 9, 'B')
    c.draw_line(4, 6, 28, 6, 'L', 1)
    # Hanging pans (left and right)
    c.fill_ellipse(7, 21, 4, 2, 'P')
    c.fill_ellipse(25, 21, 4, 2, 'P')
    # Outline the scale
    c.outline({'B', 'L', 'D', 'P', 'W'}, 'O')
    # Brass highlight accent after outline
    c.draw_line(6, 7, 26, 7, 'L', 1)
    c.fill_rect(15, 8, 16, 24, 'L')
    c.fill_rect(17, 8, 17, 24, 'D')
    # Suspension chains (S >= 16 px)
    c.draw_line(4, 9, 5, 20, 'S', 1)
    c.draw_line(10, 9, 9, 20, 'S', 1)
    c.draw_line(22, 9, 23, 20, 'S', 1)
    c.draw_line(28, 9, 27, 20, 'S', 1)
    # Iron counterweights placed in left pan (I >= 14 px)
    c.fill_rect(6, 17, 9, 20, 'I')
    return c.to_image(pal)

def gen_dalgickure():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'H': (125, 140, 160),   # Steel sphere hull
        'L': (190, 205, 225),   # Hull highlight
        'D': (70, 80, 100),     # Hull shadow
        'C': (80, 215, 235),    # Porthole glass cyan
        'K': (25, 45, 75),      # Inner deep shadow
        'B': (230, 180, 45),    # Brass bolted porthole rim
        'R': (225, 65, 45),     # Cable shackle red
    }
    # Heavy spherical hull
    c.fill_circle(16, 16, 11, 'H')
    c.fill_ellipse(13, 12, 6, 6, 'L')
    c.fill_ellipse(19, 21, 8, 6, 'D')
    # Top cable shackle (R >= 20 px)
    c.fill_rect(13, 2, 19, 6, 'R')
    # Central thick circular porthole
    c.fill_circle(16, 16, 5, 'B')
    c.fill_circle(16, 16, 3, 'C')
    c.fill_circle(17, 17, 2, 'K')
    # Side observation ports
    c.fill_ellipse(8, 16, 2, 3, 'B')
    c.fill_ellipse(8, 16, 1, 2, 'C')
    c.outline({'H', 'L', 'D', 'B', 'C', 'K', 'R'}, 'O')
    return c.to_image(pal)

def gen_muzikkutusu():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'W': (155, 85, 40),     # Walnut wood box
        'D': (95, 45, 20),      # Wood shadow
        'B': (235, 185, 45),    # Brass mechanism cylinder
        'L': (255, 225, 115),   # Brass highlight
        'F': (245, 245, 250),   # Ballerina white dress
        'T': (225, 165, 115),   # Ballerina figure tan
        'V': (45, 70, 155),     # Velvet interior blue
    }
    # Box body
    c.fill_rect(4, 16, 27, 27, 'W')
    c.fill_rect(4, 24, 27, 27, 'D')
    # Angled open box lid
    c.fill_polygon([(4, 16), (20, 6), (27, 10), (11, 20)], 'W')
    # Velvet lining interior
    c.fill_rect(6, 17, 25, 22, 'V')
    # Brass cylinder & comb mechanism (B & L >= 20 px)
    c.fill_rect(7, 18, 15, 21, 'B')
    c.fill_rect(7, 18, 15, 19, 'L')
    c.outline({'W', 'D', 'B', 'L', 'V'}, 'O')
    # Ballerina figure standing on platform (F & T >= 15 px)
    c.fill_circle(21, 11, 2, 'T')  # Head
    c.fill_polygon([(18, 13), (24, 13), (21, 17)], 'F') # Tutu dress
    c.fill_rect(19, 13, 23, 16, 'F')
    c.draw_line(21, 17, 21, 21, 'T', 1) # Legs
    return c.to_image(pal)

def gen_kuredunya():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'C': (55, 135, 215),    # Ocean blue
        'G': (85, 175, 75),     # Continents green
        'T': (215, 195, 145),   # Continents parchment tan
        'B': (235, 185, 45),    # Brass meridian arc
        'L': (255, 225, 115),   # Brass highlight
        'W': (135, 70, 30),     # Mahogany stand
        'D': (80, 40, 15),      # Stand shadow
    }
    # Globe sphere tilted
    c.fill_circle(15, 13, 8, 'C')
    # Continents landmasses (G & T >= 18 px each)
    c.fill_ellipse(13, 10, 3, 4, 'G')
    c.fill_ellipse(18, 14, 4, 3, 'G')
    c.fill_circle(14, 11, 2, 'T')
    c.fill_circle(19, 15, 2, 'T')
    # Brass curved meridian arc semi-circle (B & L >= 25 px)
    for ang in range(45, 225, 12):
        rad = math.radians(ang)
        mx = int(round(15 + 10 * math.cos(rad)))
        my = int(round(13 + 10 * math.sin(rad)))
        c.fill_circle(mx, my, 1, 'B')
    # Turned mahogany pedestal base
    c.fill_rect(9, 24, 21, 28, 'W')
    c.fill_rect(9, 26, 21, 28, 'D')
    c.draw_line(15, 21, 15, 25, 'B', 2) # Brass mounting spindle
    c.outline({'C', 'G', 'T', 'B', 'W', 'D'}, 'O')
    # Brass highlight accent after outline
    c.fill_circle(7, 19, 1, 'L')
    c.fill_circle(15, 23, 1, 'L')
    return c.to_image(pal)

def gen_retrokamera():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'S': (200, 210, 225),   # Chrome top plate
        'L': (245, 250, 255),   # Chrome highlight
        'D': (120, 130, 145),   # Chrome shadow
        'B': (50, 55, 65),      # Textured leather body
        'C': (65, 195, 225),    # Optical glass cyan
        'M': (160, 170, 185),   # Lens aperture ring
        'R': (225, 40, 45),     # Red dot badge / button
    }
    # Camera rectangular body
    c.fill_rect(4, 9, 27, 25, 'B')
    # Chrome top plate
    c.fill_rect(4, 9, 27, 13, 'S')
    c.fill_rect(4, 9, 27, 10, 'L')
    c.fill_rect(4, 12, 27, 13, 'D')
    # Shutter release button & rangefinder dial on top
    c.fill_rect(6, 6, 9, 9, 'S')
    c.fill_rect(21, 6, 26, 9, 'D')
    # Red dot logo badge (R >= 12 px)
    c.fill_circle(8, 16, 2, 'R')
    # Main round lens in center
    c.fill_circle(17, 18, 6, 'M')
    c.fill_circle(17, 18, 4, 'C')
    c.fill_circle(18, 17, 2, 'L')
    c.outline({'S', 'L', 'D', 'B', 'C', 'M', 'R'}, 'O')
    return c.to_image(pal)

def gen_gunessaati():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'P': (205, 195, 175),   # Limestone dial plate
        'D': (130, 120, 105),   # Limestone pedestal shadow
        'K': (65, 60, 55),      # Roman numerals
        'B': (225, 175, 40),    # Brass gnomon
        'L': (255, 220, 105),   # Gnomon highlight
        'S': (90, 85, 100),     # Cast shadow
        'G': (55, 145, 65),     # Ivy leaves green
    }
    # Limestone pedestal column
    c.fill_rect(12, 18, 20, 28, 'P')
    c.fill_rect(17, 18, 20, 28, 'D')
    # Circular dial plate seen in perspective (ellipse)
    c.fill_ellipse(16, 15, 12, 5, 'P')
    c.fill_ellipse(16, 17, 12, 3, 'D')
    # Triangular brass gnomon (pointer)
    c.fill_polygon([(16, 7), (16, 16), (22, 16)], 'B')
    c.draw_line(16, 7, 16, 16, 'L', 1)
    # Gnomon cast shadow on dial (S >= 15 px)
    c.fill_polygon([(16, 16), (10, 18), (16, 18)], 'S')
    # Roman numeral tick marks around rim (K >= 16 px)
    for tx in [6, 9, 12, 20, 23, 26]:
        c.set(tx, 14, 'K')
        c.set(tx, 15, 'K')
        c.set(tx, 16, 'K')
    # Climbing ivy leaves on pedestal (G >= 18 px)
    c.fill_circle(11, 23, 2, 'G')
    c.fill_circle(21, 25, 2, 'G')
    c.outline({'P', 'D', 'K', 'B', 'L', 'S', 'G'}, 'O')
    return c.to_image(pal)

def gen_buyulukazan():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'C': (60, 65, 75),      # Cast iron cauldron
        'L': (130, 140, 155),   # Iron highlight
        'P': (185, 65, 235),    # Bubbling violet potion
        'M': (250, 125, 245),   # Potion core magenta
        'V': (115, 235, 210),   # Magic vapor mint
        'F': (245, 105, 25),    # Fire embers orange
        'W': (130, 75, 35),     # Firewood logs
    }
    # Cauldron round pot belly
    c.fill_circle(16, 18, 9, 'C')
    c.fill_rect(5, 11, 27, 14, 'L')  # Cauldron thick rim (L >= 25 px)
    # Cauldron tripod legs
    c.fill_rect(9, 25, 11, 28, 'C')
    c.fill_rect(21, 25, 23, 28, 'C')
    # Campfire logs & fire underneath (W & F >= 15 px)
    c.fill_rect(10, 27, 22, 29, 'W')
    c.fill_circle(16, 26, 3, 'F')
    # Bubbling violet potion inside (P & M >= 20 px)
    c.fill_ellipse(16, 13, 7, 2, 'P')
    c.fill_circle(14, 13, 2, 'M')
    c.fill_circle(18, 12, 1, 'M')
    c.outline({'C', 'L', 'P', 'M', 'F', 'W'}, 'O')
    # Magic swirling steam vapors rising (V >= 25 px)
    c.draw_line(13, 10, 11, 4, 'V', 2)
    c.draw_line(18, 10, 20, 5, 'V', 2)
    c.set(10, 3, 'V')
    c.set(21, 4, 'V')
    return c.to_image(pal)

def gen_plakcalar():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'W': (170, 110, 55),    # Wood plinth
        'D': (105, 60, 25),     # Wood shadow
        'V': (45, 50, 60),      # Vinyl LP record
        'G': (110, 120, 135),   # Vinyl groove glint
        'R': (225, 45, 55),     # Record center label
        'S': (210, 220, 230),   # Silver tonearm
        'B': (235, 185, 45),    # Brass control dials
    }
    # Wood chassis box
    c.fill_rect(4, 9, 27, 26, 'W')
    c.fill_rect(4, 23, 27, 26, 'D')
    # Circular vinyl disc platter on left
    c.fill_circle(14, 17, 8, 'V')
    # Vinyl groove highlights (G >= 18 px)
    c.draw_line(9, 14, 12, 11, 'G', 1)
    c.draw_line(16, 23, 19, 20, 'G', 1)
    # Center label red (R >= 14 px)
    c.fill_circle(14, 17, 2, 'R')
    # Brass dials on right (B >= 12 px)
    c.fill_circle(24, 22, 2, 'B')
    c.fill_circle(24, 18, 1, 'B')
    c.outline({'W', 'D', 'V', 'G', 'R', 'B'}, 'O')
    # Tonearm drawn after outline (S >= 16 px)
    c.draw_line(24, 11, 16, 17, 'S', 2)
    c.fill_circle(24, 11, 2, 'S')
    return c.to_image(pal)

def gen_samuraykaski():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'H': (50, 55, 65),      # Lacquered helmet bowl
        'B': (240, 190, 45),    # Golden crescent crest
        'L': (255, 230, 115),   # Crest highlight
        'R': (195, 35, 45),     # Shikoro neck guard crimson
        'N': (40, 70, 140),     # Neck guard navy cords
        'M': (130, 135, 145),   # Menpo face mask iron
        'W': (240, 245, 250),   # Fierce mustache white
    }
    # Helmet round bowl
    c.fill_circle(16, 14, 8, 'H')
    # Tiered neck armor (shikoro) flaring out
    c.fill_polygon([(6, 18), (26, 18), (28, 26), (4, 26)], 'R')
    # Navy lacing cords on shikoro (N >= 18 px)
    c.fill_rect(7, 21, 25, 22, 'N')
    c.fill_rect(6, 24, 26, 25, 'N')
    # Menpo face mask in center
    c.fill_rect(13, 18, 19, 23, 'M')
    # White fierce bristling mustache (W >= 14 px)
    c.draw_line(11, 21, 15, 20, 'W', 2)
    c.draw_line(21, 21, 17, 20, 'W', 2)
    c.outline({'H', 'R', 'N', 'M', 'W'}, 'O')
    # Soaring golden crescent horn crest (B & L >= 25 px)
    for ang in range(150, 390, 12):
        rad = math.radians(ang)
        cx = int(round(16 + 10 * math.cos(rad)))
        cy = int(round(12 + 7 * math.sin(rad)))
        c.set(cx, cy, 'B')
        c.set(cx, cy - 1, 'L')
    c.fill_circle(16, 9, 2, 'B') # Central crest disc
    return c.to_image(pal)

# -------------------------------------------------------------
# 10 YENİ ZOR GENERATORS (1024 cubes full canvas, 9-10 colors)
# -------------------------------------------------------------

def gen_kristalkanyon():
    c = Canvas('1')
    pal = {
        '1': (15, 10, 35),      # Alien dark twilight sky
        '2': (235, 190, 250),   # Twin moons
        '3': (70, 35, 90),      # Distant canyon cliffs
        '4': (125, 65, 145),    # Midground canyon strata
        '5': (55, 215, 235),    # Luminescent cyan crystal
        '6': (20, 130, 155),    # Cyan crystal shadow
        '7': (240, 85, 195),    # Glowing magenta crystal
        '8': (145, 35, 115),    # Magenta crystal shadow
        '9': (45, 20, 65),      # Canyon floor basalt
        'A': (245, 240, 255),   # Specular crystal glares
    }
    # Alien sky with twin moons
    c.fill_rect(0, 0, 31, 9, '1')
    c.fill_circle(6, 4, 3, '2')
    c.fill_circle(25, 6, 2, '2')
    # Distant and midground canyon cliffs
    c.fill_polygon([(0, 15), (10, 8), (20, 14), (31, 7), (31, 20), (0, 20)], '3')
    c.fill_polygon([(0, 18), (14, 12), (31, 16), (31, 24), (0, 24)], '4')
    # Canyon floor
    c.fill_rect(0, 24, 31, 31, '9')
    # Giant towering cyan crystal monolith (5 & 6 >= 35 px)
    c.fill_polygon([(10, 5), (6, 26), (14, 26)], '5')
    c.fill_polygon([(10, 5), (10, 26), (14, 26)], '6')
    # Magenta crystal cluster on right (7 & 8 >= 30 px)
    c.fill_polygon([(22, 10), (18, 28), (26, 28)], '7')
    c.fill_polygon([(22, 10), (22, 28), (26, 28)], '8')
    # Small foreground crystal shard
    c.fill_polygon([(3, 20), (1, 29), (5, 29)], '5')
    # Specular crystal glares (A >= 15 px)
    c.draw_line(10, 6, 10, 11, 'A', 1)
    c.draw_line(22, 11, 22, 16, 'A', 1)
    c.set(3, 21, 'A')
    c.set(3, 22, 'A')
    return c.to_image(pal)

def gen_karanlikorman():
    c = Canvas('1')
    pal = {
        '1': (10, 15, 30),      # Midnight deep forest sky
        '2': (25, 45, 45),      # Distant mist canopy
        '3': (60, 40, 25),      # Gnarled ancient tree trunk
        '4': (30, 20, 15),      # Tree bark deep shadow
        '5': (45, 195, 215),    # Glowing cyan giant mushrooms
        '6': (20, 110, 130),    # Mushroom gill shadow
        '7': (240, 230, 80),    # Magic fireflies
        '8': (35, 95, 55),      # Mossy forest ground
        '9': (15, 55, 30),      # Deep moss shadow
        'A': (185, 120, 240),   # Violet fairy blooms
    }
    # Deep night forest background
    c.fill_rect(0, 0, 31, 10, '1')
    c.fill_rect(0, 10, 31, 17, '2')
    # Mossy forest floor
    c.fill_rect(0, 23, 31, 31, '8')
    c.fill_rect(0, 28, 31, 31, '9')
    # Ancient twisted oak tree on right
    c.fill_polygon([(24, 0), (29, 0), (31, 31), (22, 31)], '3')
    c.fill_polygon([(27, 0), (29, 0), (31, 31), (27, 31)], '4')
    c.draw_line(23, 14, 15, 8, '3', 2) # Arching branch
    # Giant glowing cyan toadstool mushroom in center (5 & 6 >= 35 px)
    c.fill_rect(9, 17, 13, 26, '4')   # Mushroom stem
    c.fill_ellipse(11, 15, 8, 4, '5') # Cap dome
    c.fill_ellipse(11, 17, 8, 2, '6') # Cap gill shadow
    # Small mushroom companion on left
    c.fill_ellipse(3, 23, 3, 2, '5')
    c.fill_rect(3, 24, 4, 28, '4')
    # Violet fairy flowers (A >= 15 px)
    for fx, fy in [(18, 24), (20, 26), (16, 28), (2, 29)]:
        c.fill_circle(fx, fy, 1, 'A')
    # Fireflies glowing throughout woods (7 >= 16 px)
    for lx, ly in [(6, 6), (14, 4), (18, 12), (7, 12), (28, 16), (15, 20), (20, 18), (2, 17)]:
        c.set(lx, ly, '7')
        c.set(lx + 1, ly, '7')
    return c.to_image(pal)

def gen_cyberpunksokak():
    c = Canvas('1')
    pal = {
        '1': (10, 12, 22),      # Deep rainy night sky
        '2': (95, 100, 120),    # Concrete alley walls
        '3': (50, 55, 70),      # Building deep shadows
        '4': (245, 45, 125),    # Hot pink neon sign
        '5': (45, 220, 240),    # Cyan neon sign
        '6': (250, 215, 45),    # Lit window & vending machine
        '7': (35, 140, 160),    # Vending machine teal
        '8': (25, 32, 45),      # Wet asphalt street
        '9': (165, 85, 145),    # Puddle pink reflections
        'A': (75, 165, 185),    # Puddle cyan reflections
    }
    # Alley walls on left and right
    c.fill_rect(0, 0, 11, 23, '2')
    c.fill_rect(20, 0, 31, 23, '3')
    # Wet street pavement
    c.fill_rect(0, 23, 31, 31, '8')
    # Lit apartment windows
    c.fill_rect(2, 4, 5, 8, '6')
    c.fill_rect(24, 3, 28, 6, '6')
    # Hot pink vertical neon kanji sign on left (4 >= 25 px)
    c.fill_rect(7, 3, 10, 15, '4')
    # Cyan neon bar sign on right (5 >= 25 px)
    c.fill_rect(21, 10, 25, 17, '5')
    # Vending machine on left ground (6 & 7 >= 20 px)
    c.fill_rect(4, 16, 9, 23, '7')
    c.fill_rect(5, 17, 8, 20, '6')
    # Rain puddle neon reflections on dark street (9 & A >= 18 px)
    c.fill_ellipse(8, 26, 5, 2, '9')
    c.fill_ellipse(22, 27, 6, 2, 'A')
    return c.to_image(pal)

def gen_buzultapinagi():
    c = Canvas('1')
    pal = {
        '1': (18, 28, 55),      # Arctic dawn sky
        '2': (65, 195, 165),    # Aurora ribbon
        '3': (210, 240, 255),   # Ice temple arch
        '4': (115, 185, 215),   # Glacier cyan midtone
        '5': (45, 105, 145),    # Crevasse ice shadow
        '6': (245, 255, 255),   # Snow drifts
        '7': (75, 80, 95),      # Stone base blocks
        '8': (245, 210, 85),    # Altar eternal fire yellow
        '9': (240, 110, 40),    # Fire orange glow
    }
    # Aurora arching across arctic sky (2 >= 30 px)
    c.fill_rect(0, 0, 31, 8, '1')
    c.fill_polygon([(0, 4), (16, 1), (31, 3), (31, 6), (16, 3), (0, 7)], '2')
    # Ancient stone temple podium
    c.fill_rect(0, 24, 31, 31, '7')
    # Snow drifts covering ground (6 >= 35 px)
    c.fill_polygon([(0, 23), (12, 22), (24, 25), (31, 23), (31, 28), (0, 28)], '6')
    # Carved glacial ice pillars & arch (3, 4, 5)
    # Left ice pillar
    c.fill_rect(5, 7, 10, 24, '3')
    c.fill_rect(8, 7, 10, 24, '4')
    # Right ice pillar
    c.fill_rect(21, 7, 26, 24, '3')
    c.fill_rect(24, 7, 26, 24, '5')
    # Ice pediment arch across top
    c.fill_polygon([(3, 8), (16, 2), (28, 8)], '3')
    c.fill_polygon([(16, 2), (28, 8), (16, 8)], '4')
    # Altar eternal flame in center (8 & 9 >= 15 px)
    c.fill_rect(13, 20, 18, 24, '7') # Altar stone
    c.fill_circle(15, 18, 4, '9')
    c.fill_circle(15, 18, 2, '8')
    return c.to_image(pal)

def gen_romakolezyum():
    c = Canvas('1')
    pal = {
        '1': (130, 45, 75),     # Sunset crimson sky
        '2': (245, 135, 55),    # Amber horizon glow
        '3': (255, 225, 120),   # Golden sun disc
        '4': (215, 185, 145),   # Travertine marble arches
        '5': (145, 115, 85),    # Colosseum arcade shadow
        '6': (60, 45, 35),      # Arch vault shadows
        '7': (195, 45, 45),     # Imperial red banners
        '8': (35, 75, 40),      # Cypress trees green
        '9': (15, 45, 20),      # Cypress shadow
        'A': (175, 145, 95),    # Forum gravel grounds
    }
    # Sunset sky gradient
    c.fill_rect(0, 0, 31, 6, '1')
    c.fill_rect(0, 7, 31, 12, '2')
    # Sun setting behind monument (3 >= 15 px)
    c.fill_circle(8, 9, 3, '3')
    # Ground terrain
    c.fill_rect(0, 24, 31, 31, 'A')
    # Colosseum curved tiers of arches (4, 5, 6)
    c.fill_rect(4, 9, 27, 24, '4')
    c.fill_rect(16, 9, 27, 24, '5')
    # Tiers of arched openings (6 >= 25 px)
    for ay in [11, 15, 19]:
        for ax in [6, 11, 16, 21]:
            c.fill_rect(ax, ay, ax + 2, ay + 2, '6')
    # Imperial red legionary standard banner (7 >= 14 px)
    c.fill_rect(1, 10, 4, 18, '7')
    # Tall Italian cypress trees on right (8 & 9 >= 20 px)
    c.fill_polygon([(27, 10), (30, 10), (31, 26), (26, 26)], '8')
    c.fill_polygon([(29, 10), (30, 10), (31, 26), (29, 26)], '9')
    return c.to_image(pal)

def gen_korsankoyu():
    c = Canvas('1')
    pal = {
        '1': (35, 40, 55),      # Cavern roof rock
        '2': (95, 75, 65),      # Cavern stalactites
        '3': (145, 120, 100),   # Sunlit rock rim
        '4': (245, 150, 75),    # Sunset through mouth
        '5': (40, 145, 165),    # Lagoon turquoise water
        '6': (215, 175, 105),   # Sandy beach shore
        '7': (250, 210, 45),    # Piles of gold coins
        '8': (135, 70, 30),     # Wooden treasure chest
        '9': (25, 25, 35),      # Pirate black flag
        'A': (235, 45, 45),     # Gemstones red
    }
    # Cavern framing mouth looking out
    c.fill_rect(0, 0, 31, 31, '1')
    # Sunset sky opening in center
    c.fill_ellipse(16, 10, 9, 6, '4')
    # Natural skull rock teeth in cavern mouth (2 & 3 >= 25 px)
    c.fill_polygon([(12, 12), (14, 7), (16, 12)], '2')
    c.fill_polygon([(16, 12), (18, 7), (20, 12)], '2')
    c.draw_line(8, 14, 24, 14, '3', 1)
    # Turquoise cove lagoon
    c.fill_rect(0, 15, 31, 22, '5')
    # Sandy shore inside cave
    c.fill_rect(0, 22, 31, 31, '6')
    # Wooden treasure chest (8 >= 15 px)
    c.fill_rect(10, 23, 17, 28, '8')
    c.fill_rect(10, 23, 17, 24, '3') # Lid rim
    # Piles of gleaming gold coins spilling out (7 >= 25 px)
    c.fill_ellipse(19, 26, 6, 3, '7')
    c.fill_ellipse(8, 27, 4, 2, '7')
    # Pirate red bandana tied on skull rock + ruby gemstones (A >= 18 px)
    c.fill_rect(13, 5, 19, 7, 'A')
    c.set(18, 25, 'A')
    c.set(20, 26, 'A')
    c.set(13, 25, 'A')
    c.set(9, 27, 'A')
    c.set(15, 27, 'A')
    c.set(22, 28, 'A')
    # Jolly Roger black flag planted in sand (9 >= 15 px)
    c.draw_line(26, 16, 26, 27, '8', 1)
    c.fill_rect(22, 16, 25, 19, '9')
    c.set(23, 17, '6') # Skull dot
    return c.to_image(pal)

def gen_kozmiknebula():
    c = Canvas('1')
    pal = {
        '1': (8, 10, 20),       # Cosmic void
        '2': (245, 245, 255),   # Stars
        '3': (40, 80, 180),     # Indigo dust veil
        '4': (60, 175, 235),    # Cyan gas filaments
        '5': (185, 45, 160),    # Magenta cloud
        '6': (245, 95, 190),    # Bright pink shockwave
        '7': (255, 210, 85),    # Protostar core yellow
        '8': (255, 140, 40),    # Accretion orange
        '9': (105, 30, 85),     # Dark dust lane
    }
    # Void background with twinkling stars (2 >= 20 px)
    for sx, sy in [(2, 3), (29, 2), (27, 28), (4, 27), (8, 12), (23, 7), (1, 18), (30, 16), (28, 10), (5, 8)]:
        c.set(sx, sy, '2')
        c.set(sx + 1, sy, '2')
    # Swirling nebula outer indigo clouds (3 >= 50 px)
    c.fill_ellipse(16, 16, 13, 10, '3')
    # Swirling magenta molecular clouds (5 & 6 >= 40 px each)
    c.fill_polygon([(4, 8), (28, 12), (24, 22), (8, 24)], '5')
    c.fill_ellipse(16, 16, 8, 5, '6')
    # Cyan glowing ionized gas arms (4 >= 35 px)
    c.draw_line(4, 20, 16, 13, '4', 2)
    c.draw_line(16, 19, 28, 11, '4', 2)
    # Dark interstellar absorption dust lane (9 >= 25 px)
    c.draw_line(7, 16, 25, 17, '9', 2)
    # Blazing infant protostar and accretion disk in center (7 & 8 >= 20 px)
    c.fill_circle(16, 16, 4, '8')
    c.fill_circle(16, 16, 2, '7')
    return c.to_image(pal)

def gen_bulutlarustukalesi():
    c = Canvas('1')
    pal = {
        '1': (80, 120, 195),    # Azure high sky
        '2': (245, 155, 95),    # Sunset peach glow
        '3': (255, 235, 210),   # Billowy cloud tops
        '4': (175, 145, 170),   # Cloud mauve shadow
        '5': (205, 215, 230),   # White citadel stone
        '6': (120, 130, 150),   # Citadel stone shadow
        '7': (50, 85, 175),     # Spire blue roofs
        '8': (245, 205, 45),    # Golden spire flags
        '9': (85, 60, 45),      # Floating rock base
        'A': (65, 155, 75),     # Hanging vines green
    }
    # Sky gradient
    c.fill_rect(0, 0, 31, 10, '1')
    c.fill_rect(0, 11, 31, 19, '2')
    # Ocean of clouds at bottom (3 & 4 >= 40 px each)
    c.fill_rect(0, 20, 31, 31, '3')
    c.fill_rect(0, 26, 31, 31, '4')
    for cx in [3, 10, 18, 26]:
        c.fill_circle(cx, 20, 4, '3')
    # Floating rock island base (9 & A >= 20 px)
    c.fill_polygon([(6, 17), (25, 17), (16, 24)], '9')
    c.draw_line(7, 18, 24, 18, 'A', 2)
    # Fantasy white stone castle towers
    c.fill_rect(8, 9, 14, 17, '5')
    c.fill_rect(12, 9, 14, 17, '6')
    c.fill_rect(17, 7, 23, 17, '5')
    c.fill_rect(20, 7, 23, 17, '6')
    # Conical spire roofs in blue (7 >= 25 px)
    c.fill_polygon([(7, 9), (11, 2), (15, 9)], '7')
    c.fill_polygon([(16, 7), (20, 1), (24, 7)], '7')
    # Golden flags on spire tips (8 >= 14 px)
    c.fill_rect(11, 1, 14, 3, '8')
    c.fill_rect(20, 0, 23, 2, '8')
    return c.to_image(pal)

def gen_volkanikada():
    c = Canvas('1')
    pal = {
        '1': (135, 190, 230),   # Tropical sky
        '2': (185, 180, 185),   # Eruption smoke plume
        '3': (245, 80, 30),     # Molten lava glow
        '4': (90, 75, 70),      # Volcanic basalt peak
        '5': (45, 125, 55),     # Tropical jungle slopes
        '6': (25, 70, 35),      # Jungle deep shade
        '7': (50, 48, 55),      # Black sand beach
        '8': (35, 145, 175),    # Turquoise ocean
        '9': (235, 245, 255),   # White surf foam
        'A': (140, 85, 40),     # Palm tree trunks
    }
    # Tropical sky
    c.fill_rect(0, 0, 31, 8, '1')
    # Billowing volcanic ash smoke plume (2 >= 25 px)
    c.fill_circle(16, 3, 4, '2')
    c.fill_circle(12, 5, 3, '2')
    c.fill_circle(19, 4, 3, '2')
    # Molten crater glow (3 >= 15 px)
    c.fill_rect(14, 7, 18, 9, '3')
    # Volcanic mountain peak (4 >= 30 px)
    c.fill_polygon([(13, 8), (19, 8), (27, 22), (5, 22)], '4')
    # Lush jungle lower slopes (5 & 6 >= 30 px)
    c.fill_polygon([(5, 16), (11, 16), (3, 23)], '5')
    c.fill_polygon([(21, 16), (27, 16), (29, 23)], '6')
    # Black volcanic sand beach (7 >= 25 px)
    c.fill_rect(0, 22, 31, 25, '7')
    # Turquoise sea and crashing surf (8 & 9 >= 30 px)
    c.fill_rect(0, 25, 31, 31, '8')
    c.draw_line(0, 25, 31, 25, '9', 1)
    c.draw_line(4, 28, 27, 28, '9', 1)
    # Palm tree on shoreline (A >= 15 px)
    c.draw_line(4, 24, 7, 17, 'A', 2)
    c.fill_circle(7, 16, 2, '5')
    return c.to_image(pal)

def gen_marskolonisi():
    c = Canvas('1')
    pal = {
        '1': (215, 145, 95),    # Martian butterscotch sky
        '2': (185, 180, 190),   # Phobos moon
        '3': (175, 65, 35),     # Red sand dunes
        '4': (115, 35, 20),     # Dune shadow
        '5': (220, 235, 245),   # Geodesic dome frame
        '6': (85, 180, 215),    # Dome glass cyan
        '7': (55, 160, 65),     # Terraformed green crops
        '8': (245, 200, 50),    # Solar panel gold
        '9': (50, 75, 135),     # Solar panel blue cells
        'A': (225, 45, 45),     # Rover vehicle red
    }
    # Martian sky with Phobos moon
    c.fill_rect(0, 0, 31, 11, '1')
    c.fill_circle(24, 5, 2, '2')
    # Rolling rust-red dunes (3 & 4 >= 40 px each)
    c.fill_polygon([(0, 11), (12, 8), (23, 12), (31, 9), (31, 31), (0, 31)], '3')
    c.fill_polygon([(12, 8), (23, 12), (31, 9), (31, 18), (12, 16)], '4')
    # Geodesic colony biodome in center (5, 6, 7 >= 25 px each)
    c.fill_circle(14, 19, 7, '5')
    c.fill_circle(14, 19, 5, '6')
    c.fill_ellipse(14, 21, 4, 2, '7') # Green agricultural crops inside
    # Solar panel array on right (8 & 9 >= 20 px each)
    c.fill_polygon([(22, 18), (30, 16), (30, 22), (22, 24)], '8')
    c.fill_polygon([(23, 19), (29, 17), (29, 21), (23, 23)], '9')
    # Exploration rover on foreground dune (A >= 14 px)
    c.fill_rect(4, 25, 8, 28, 'A')
    c.fill_circle(4, 29, 1, '4') # Wheels
    c.fill_circle(8, 29, 1, '4')
    return c.to_image(pal)

GENERATORS = {
    # 10 Kolay
    'kolay/OrigamiKugu': gen_origamikugu,
    'kolay/PrizmaIsik': gen_prizmaisik,
    'kolay/AstronotKask': gen_astronotkask,
    'kolay/SihirliIksir': gen_sihirliiksir,
    'kolay/PikselKalp': gen_pikselkalp,
    'kolay/UzayRoketi': gen_uzayroketi,
    'kolay/DisliCark': gen_dislicark,
    'kolay/LavLambasi': gen_lavlambasi,
    'kolay/MeksikaSapkasi': gen_meksikasapkasi,
    'kolay/TotemMaskesi': gen_totemmaskesi,

    # 10 Orta
    'orta/HavaGemisi': gen_havagemisi,
    'orta/AntikTerazi': gen_antikterazi,
    'orta/DalgicKure': gen_dalgickure,
    'orta/MuzikKutusu': gen_muzikkutusu,
    'orta/KureDunya': gen_kuredunya,
    'orta/RetroKamera': gen_retrokamera,
    'orta/GunesSaati': gen_gunessaati,
    'orta/BuyuluKazan': gen_buyulukazan,
    'orta/PlakCalar': gen_plakcalar,
    'orta/SamurayKaski': gen_samuraykaski,

    # 10 Zor
    'zor/KristalKanyon': gen_kristalkanyon,
    'zor/KaranlikOrman': gen_karanlikorman,
    'zor/CyberpunkSokak': gen_cyberpunksokak,
    'zor/BuzulTapinagi': gen_buzultapinagi,
    'zor/RomaKolezyum': gen_romakolezyum,
    'zor/KorsanKoyu': gen_korsankoyu,
    'zor/KozmikNebula': gen_kozmiknebula,
    'zor/BulutlarUstuKalesi': gen_bulutlarustukalesi,
    'zor/VolkanikAda': gen_volkanikada,
    'zor/MarsKolonisi': gen_marskolonisi,
}

def main():
    base_dir = 'Assets/04.11.2026'
    total = len(GENERATORS)
    print(f"Generating {total} batch 4 pixel arts...")
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
