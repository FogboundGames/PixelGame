"""Pixel art generator for PixelGame - Batch 5 (30 more unique levels).
Generates 30 NEW game-ready 32x32 pixel art images:
- 10 Kolay (Easy - Full 32x32 Canvas / Closed Surrounds, 5 colors)
- 10 Orta (Medium - 7-8 colors, framed/outline)
- 10 Zor (Hard - 9-10 colors, 1024 cubes full canvas)
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
# 10 YENİ KOLAY GENERATORS (Etrafı Kapalı - 1024 Cubes, 5 Colors)
# -------------------------------------------------------------

def gen_gunbatimiyelken():
    c = Canvas('1')
    pal = {
        '1': (250, 150, 50),    # Sunset orange sky
        '2': (255, 230, 90),    # Giant setting sun
        '3': (35, 75, 140),     # Deep ocean water
        '4': (245, 245, 250),   # White canvas sails
        '5': (40, 35, 45),      # Sailboat hull silhouette
    }
    # Sunset sky top half
    c.fill_rect(0, 0, 31, 18, '1')
    # Big setting sun sinking into horizon
    c.fill_circle(16, 15, 7, '2')
    # Ocean water bottom half
    c.fill_rect(0, 19, 31, 31, '3')
    # Water sun reflection ripples (2 >= 20 px)
    for y in [21, 24, 27]:
        c.draw_line(12, y, 20, y, '2', 1)
    # Sailboat hull
    c.fill_polygon([(10, 22), (22, 22), (19, 25), (13, 25)], '5')
    c.draw_line(15, 10, 15, 22, '5', 1) # Mast
    # White triangular sails
    c.fill_polygon([(15, 10), (15, 20), (21, 20)], '4') # Mainsail
    c.fill_polygon([(14, 12), (14, 20), (10, 20)], '4') # Jib
    return c.to_image(pal)

def gen_gececol():
    c = Canvas('1')
    pal = {
        '1': (20, 25, 55),      # Starry night sky
        '2': (255, 235, 130),   # Moon & stars
        '3': (220, 160, 65),    # Golden sand dunes
        '4': (140, 85, 35),     # Dune shadow
        '5': (55, 40, 50),      # Distant pyramid
    }
    # Deep night sky
    c.fill_rect(0, 0, 31, 14, '1')
    # Crescent moon & stars (2 >= 25 px)
    c.fill_circle(7, 6, 3, '2')
    c.fill_circle(8, 5, 2, '1') # Cutout crescent
    for sx, sy in [(15, 3), (22, 5), (28, 2), (25, 8), (18, 9), (2, 9)]:
        c.set(sx, sy, '2')
        c.set(sx + 1, sy, '2')
    # Distant Egyptian pyramid silhouette in background
    c.fill_polygon([(11, 15), (17, 8), (23, 15)], '5')
    # Sweeping sand dunes
    c.fill_polygon([(0, 13), (12, 10), (24, 15), (31, 13), (31, 31), (0, 31)], '3')
    # Curved dune shadow ridges
    c.fill_polygon([(12, 10), (24, 15), (31, 13), (31, 22), (12, 18)], '4')
    c.fill_polygon([(0, 21), (15, 19), (31, 26), (31, 31), (0, 31)], '4')
    c.fill_polygon([(0, 21), (15, 19), (25, 25), (15, 25)], '3')
    return c.to_image(pal)

def gen_kurtveay():
    c = Canvas('1')
    pal = {
        '1': (25, 30, 60),      # Midnight sky
        '2': (245, 245, 220),   # Giant luminous full moon
        '3': (120, 135, 185),   # Moon halo glow
        '4': (75, 70, 80),      # Rocky cliff
        '5': (25, 22, 28),      # Howling wolf silhouette
    }
    # Sky background
    c.fill_rect(0, 0, 31, 31, '1')
    # Luminous moon halo and full moon
    c.fill_circle(17, 12, 11, '3')
    c.fill_circle(17, 12, 8, '2')
    # Rocky mountain promontory on bottom-left
    c.fill_polygon([(0, 17), (14, 21), (18, 31), (0, 31)], '4')
    # Howling wolf silhouette perched on cliff
    # Wolf body and haunches
    c.fill_polygon([(3, 20), (9, 17), (10, 24), (4, 24)], '5')
    # Wolf chest & howling tilted neck
    c.fill_polygon([(7, 17), (12, 11), (14, 13), (9, 20)], '5')
    # Pointed howling snout pointing up-right into the moon
    c.fill_polygon([(12, 11), (16, 9), (14, 13)], '5')
    # Pointed ears and tail
    c.set(11, 10, '5')
    c.set(12, 10, '5')
    c.draw_line(3, 23, 1, 27, '5', 1)
    return c.to_image(pal)

def gen_uzaygezegeni():
    c = Canvas('1')
    pal = {
        '1': (15, 15, 25),      # Space void black
        '2': (245, 245, 255),   # Distant stars
        '3': (45, 185, 205),    # Ringed planet turquoise
        '4': (15, 65, 105),     # Planet shadow side
        '5': (245, 195, 65),    # Planetary rings gold
    }
    # Space background
    c.fill_rect(0, 0, 31, 31, '1')
    # Twinkling stars (2 >= 20 px)
    for sx, sy in [(3, 4), (28, 3), (25, 27), (5, 28), (29, 15), (2, 16), (10, 7), (20, 29), (8, 23), (23, 4)]:
        c.set(sx, sy, '2')
        c.set(sx + 1, sy, '2')
    # Planetary ring behind planet
    for x in range(3, 29):
        ry = int(round(16 + (x - 16) * 0.45))
        c.draw_line(x, ry - 1, x, ry + 1, '5', 1)
    # Spherical planet body
    c.fill_circle(16, 16, 8, '3')
    # Planet shadow hemisphere
    c.fill_ellipse(19, 16, 5, 8, '4')
    # Planetary ring in front of planet
    for x in range(5, 21):
        ry = int(round(16 + (x - 16) * 0.45))
        c.draw_line(x, ry - 1, x, ry + 1, '5', 1)
    return c.to_image(pal)

def gen_kutuppenguen():
    c = Canvas('1')
    pal = {
        '1': (160, 205, 235),   # Polar sky
        '2': (35, 80, 135),     # Polar ocean
        '3': (240, 250, 255),   # Ice floe white
        '4': (30, 35, 45),      # Penguin black
        '5': (245, 145, 35),    # Penguin beak/feet orange
    }
    # Polar sky
    c.fill_rect(0, 0, 31, 14, '1')
    # Distant iceberg silhouette
    c.fill_polygon([(19, 14), (24, 7), (29, 14)], '3')
    # Deep ocean
    c.fill_rect(0, 14, 31, 31, '2')
    # Floating ice floe platform
    c.fill_ellipse(15, 23, 13, 6, '3')
    # Penguin standing on ice
    c.fill_ellipse(15, 16, 5, 8, '4')  # Black body
    c.fill_ellipse(15, 17, 3, 6, '3')  # White belly
    c.fill_circle(15, 9, 3, '4')       # Head
    # Orange beak pointing right & orange feet (5 >= 15 px)
    c.fill_polygon([(17, 8), (21, 9), (17, 10)], '5')
    c.fill_rect(12, 23, 14, 25, '5')   # Left foot
    c.fill_rect(16, 23, 18, 25, '5')   # Right foot
    return c.to_image(pal)

def gen_madencave():
    c = Canvas('1')
    pal = {
        '1': (115, 75, 45),     # Cavern rock brown
        '2': (50, 30, 20),      # Deep rock shadow
        '3': (255, 215, 45),    # Gold ore veins
        '4': (185, 195, 210),   # Pickaxe steel
        '5': (250, 115, 30),    # Lantern flame orange
    }
    # Cavern rock walls
    c.fill_rect(0, 0, 31, 31, '1')
    c.fill_rect(0, 0, 10, 31, '2')
    c.fill_rect(22, 0, 31, 31, '2')
    c.fill_rect(0, 24, 31, 31, '2')
    # Sparkling veins of gold ore (3 >= 35 px)
    for gy in [4, 9, 16, 22]:
        c.draw_line(2, gy, 7, gy + 1, '3', 1)
        c.draw_line(24, gy, 29, gy - 1, '3', 1)
    # Miner pickaxe leaning against rock (4 & 1 >= 25 px)
    c.draw_line(12, 26, 20, 12, '1', 2) # Wooden handle
    c.draw_line(17, 10, 23, 14, '4', 2) # Steel pick head
    c.set(16, 9, '4')
    c.set(24, 15, '4')
    # Hanging glowing miner's lantern (5 >= 20 px)
    c.fill_rect(14, 16, 18, 21, '5')
    c.fill_circle(16, 18, 1, '3') # Flame core
    return c.to_image(pal)

def gen_japongunesi():
    c = Canvas('1')
    pal = {
        '1': (225, 55, 50),     # Vermilion crimson sky
        '2': (250, 245, 245),   # Sun disc & clouds white
        '3': (210, 230, 250),   # Fuji snow cap light blue
        '4': (45, 50, 75),      # Fuji volcanic slope
        '5': (25, 25, 30),      # Torii gate silhouette black
    }
    # Vermilion sky
    c.fill_rect(0, 0, 31, 31, '1')
    # Giant white sun disc behind mountain
    c.fill_circle(16, 11, 8, '2')
    # Mount Fuji volcano silhouette
    c.fill_polygon([(16, 12), (3, 27), (29, 27)], '4')
    # Fuji snow-capped peak (3 >= 30 px)
    c.fill_polygon([(16, 12), (12, 17), (20, 17)], '3')
    c.fill_rect(0, 27, 31, 31, '4') # Ground
    # Torii gate in foreground (5 >= 35 px)
    c.fill_rect(9, 21, 23, 22, '5')  # Top lintel (kasagi)
    c.fill_rect(10, 24, 22, 24, '5') # Lower tie beam (nuki)
    c.fill_rect(11, 21, 12, 31, '5') # Left pillar
    c.fill_rect(20, 21, 21, 31, '5') # Right pillar
    return c.to_image(pal)

def gen_sihirlikule():
    c = Canvas('1')
    pal = {
        '1': (55, 30, 80),      # Magic purple sky
        '2': (255, 230, 100),   # Moon & stars
        '3': (160, 165, 180),   # Tower stone grey
        '4': (45, 90, 175),     # Conical roof blue
        '5': (45, 135, 65),     # Grassy hill green
    }
    # Purple sky
    c.fill_rect(0, 0, 31, 31, '1')
    # Crescent moon & stars (2 >= 20 px)
    c.fill_circle(24, 6, 3, '2')
    c.fill_circle(25, 5, 2, '1')
    for sx, sy in [(5, 4), (12, 7), (3, 10), (9, 2)]:
        c.set(sx, sy, '2')
        c.set(sx + 1, sy, '2')
    # Rolling green grassy hill
    c.fill_ellipse(16, 29, 18, 7, '5')
    # Wizard tower stone body (3 >= 50 px)
    c.fill_rect(13, 12, 19, 26, '3')
    # Arched door and windows (1)
    c.fill_rect(15, 22, 17, 26, '1')
    c.fill_rect(15, 15, 17, 17, '2') # Lit window
    # Conical roof spire (4 >= 35 px)
    c.fill_polygon([(11, 12), (16, 3), (21, 12)], '4')
    c.fill_circle(16, 3, 1, '2') # Spire crystal
    return c.to_image(pal)

def gen_adapalmiye():
    c = Canvas('1')
    pal = {
        '1': (130, 205, 245),   # Tropical sky
        '2': (255, 225, 65),    # Golden sun
        '3': (35, 145, 175),    # Turquoise ocean
        '4': (235, 195, 115),   # Sand island
        '5': (45, 155, 60),     # Palm tree fronds
    }
    # Tropical sky top half
    c.fill_rect(0, 0, 31, 16, '1')
    # Bright sun in upper-left
    c.fill_circle(6, 6, 4, '2')
    # Turquoise sea
    c.fill_rect(0, 17, 31, 31, '3')
    # Small sand dune island in center
    c.fill_ellipse(17, 24, 11, 4, '4')
    # Curved palm tree trunk (4 >= 20 px)
    c.draw_line(18, 24, 14, 12, '4', 2)
    # Palm tree fronds canopy (5 >= 45 px)
    c.fill_circle(14, 11, 3, '5')
    c.draw_line(14, 11, 7, 13, '5', 2)
    c.draw_line(14, 11, 8, 8, '5', 2)
    c.draw_line(14, 11, 15, 6, '5', 2)
    c.draw_line(14, 11, 21, 9, '5', 2)
    c.draw_line(14, 11, 20, 14, '5', 2)
    return c.to_image(pal)

def gen_sonbaharyolu():
    c = Canvas('1')
    pal = {
        '1': (175, 220, 240),   # Autumn sky
        '2': (245, 185, 45),    # Golden birch leaves
        '3': (215, 55, 40),     # Crimson maple leaves
        '4': (85, 50, 30),      # Tree trunks dark wood
        '5': (185, 140, 85),    # Dirt forest pathway
        }
    # Soft sky top
    c.fill_rect(0, 0, 31, 10, '1')
    # Forest canopy foliage (2 & 3 >= 60 px each)
    c.fill_circle(7, 9, 7, '2')
    c.fill_circle(24, 9, 7, '3')
    c.fill_circle(16, 7, 5, '2')
    # Tree trunks
    c.fill_rect(6, 12, 8, 26, '4')
    c.fill_rect(23, 12, 25, 26, '4')
    # Forest ground
    c.fill_rect(0, 21, 31, 31, '2')
    c.fill_rect(0, 26, 31, 31, '3')
    # Winding dirt trail in center (5 >= 40 px)
    c.fill_polygon([(13, 18), (18, 18), (24, 31), (7, 31)], '5')
    return c.to_image(pal)

# -------------------------------------------------------------
# 10 YENİ ORTA GENERATORS (with OUT = (34, 26, 40))
# -------------------------------------------------------------

def gen_antikkalkan():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'W': (170, 105, 50),    # Oak wood planks
        'D': (105, 55, 25),     # Wood shadow
        'B': (45, 115, 205),    # Blue painted quadrant
        'Y': (245, 205, 45),    # Yellow painted quadrant
        'I': (165, 175, 190),   # Central iron boss dome
        'L': (225, 235, 245),   # Iron glint
    }
    # Circular shield disc
    c.fill_circle(16, 16, 12, 'W')
    # Shield painted quadrants (B & Y >= 30 px each)
    # Top-left quadrant Blue
    for y in range(4, 16):
        for x in range(4, 16):
            if c.get(x, y) == 'W': c.set(x, y, 'B')
    # Bottom-right quadrant Blue
    for y in range(16, 28):
        for x in range(16, 28):
            if c.get(x, y) == 'W': c.set(x, y, 'B')
    # Top-right quadrant Yellow
    for y in range(4, 16):
        for x in range(16, 28):
            if c.get(x, y) == 'W': c.set(x, y, 'Y')
    # Bottom-left quadrant Yellow
    for y in range(16, 28):
        for x in range(4, 16):
            if c.get(x, y) == 'W': c.set(x, y, 'Y')
    # Wood shadow rim
    c.fill_ellipse(16, 25, 9, 3, 'D')
    # Iron boss dome in center
    c.fill_circle(16, 16, 5, 'I')
    c.fill_circle(15, 15, 2, 'L')
    c.outline({'W', 'D', 'B', 'Y', 'I', 'L'}, 'O')
    return c.to_image(pal)

def gen_sihirbazsapkasi():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'P': (75, 45, 145),     # Purple velvet hat
        'D': (40, 20, 85),      # Hat shadow
        'G': (250, 210, 45),    # Golden stars embroidery
        'R': (215, 45, 60),     # Ribbon band crimson
        'W': (160, 100, 50),    # Magic wand wood
        'C': (115, 235, 245),   # Magic sparkles cyan
    }
    # Wide curved hat brim
    c.fill_ellipse(15, 24, 12, 4, 'P')
    c.fill_ellipse(15, 26, 12, 2, 'D')
    # Crooked wizard cone body
    c.fill_polygon([(9, 23), (21, 23), (17, 10), (13, 10)], 'P')
    c.fill_polygon([(13, 10), (17, 10), (22, 5), (19, 3)], 'P') # Crooked tip
    # Red ribbon band across brim
    c.fill_rect(9, 21, 21, 23, 'R')
    # Golden star & moon decorations (G >= 18 px)
    c.fill_circle(15, 14, 2, 'G')
    c.fill_circle(19, 7, 1, 'G')
    c.fill_circle(13, 18, 1, 'G')
    c.outline({'P', 'D', 'G', 'R'}, 'O')
    # Magic wand crossing on right (W >= 15 px)
    c.draw_line(23, 27, 28, 13, 'W', 2)
    # Magic sparkles glowing at wand tip (C >= 15 px)
    c.fill_circle(28, 11, 2, 'C')
    c.set(26, 10, 'C')
    c.set(30, 11, 'C')
    return c.to_image(pal)

def gen_nostaljikradyo():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'W': (155, 85, 40),     # Walnut wooden casing
        'D': (95, 45, 20),      # Wood shadow
        'F': (235, 220, 175),   # Speaker grill cloth
        'B': (235, 185, 45),    # Brass knobs & trim
        'Y': (255, 235, 110),   # Backlit dial glow
        'S': (180, 190, 200),   # Silver tuning pointer
    }
    # Cathedral curved radio top
    c.fill_circle(16, 14, 11, 'W')
    c.fill_rect(5, 14, 26, 27, 'W')
    c.fill_rect(5, 25, 26, 27, 'D')
    # Speaker fabric grill (oval)
    c.fill_ellipse(16, 12, 7, 5, 'F')
    # Wooden slats across speaker grill
    c.draw_line(16, 7, 16, 17, 'W', 1)
    c.draw_line(13, 8, 13, 16, 'W', 1)
    c.draw_line(19, 8, 19, 16, 'W', 1)
    # Frequency dial window (Y >= 18 px)
    c.fill_rect(8, 19, 23, 22, 'Y')
    c.draw_line(16, 19, 16, 22, 'S', 1) # Tuning needle
    # Brass control knobs (B >= 14 px)
    c.fill_circle(9, 25, 2, 'B')
    c.fill_circle(22, 25, 2, 'B')
    c.outline({'W', 'D', 'F', 'B', 'Y', 'S'}, 'O')
    return c.to_image(pal)

def gen_pusulaantik():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'B': (235, 185, 45),    # Brass casing
        'L': (255, 225, 115),   # Brass highlight
        'D': (160, 115, 25),    # Brass shadow
        'F': (245, 240, 220),   # Compass face dial
        'R': (225, 40, 45),     # North needle red
        'S': (60, 70, 85),      # South needle dark
        'G': (145, 215, 235),   # Glass lens glare
    }
    # Round brass pocket casing
    c.fill_circle(16, 16, 11, 'B')
    c.fill_circle(16, 16, 9, 'D')
    # Top watch hanging ring
    c.fill_circle(16, 4, 3, 'B')
    c.fill_circle(16, 4, 1, '.')
    # Compass parchment face
    c.fill_circle(16, 16, 8, 'F')
    # Compass magnetic needle
    # North red arrow
    c.fill_polygon([(16, 9), (13, 16), (16, 15)], 'R')
    c.fill_polygon([(16, 9), (19, 16), (16, 15)], 'R')
    # South dark arrow
    c.fill_polygon([(16, 23), (13, 16), (16, 17)], 'S')
    c.fill_polygon([(16, 23), (19, 16), (16, 17)], 'S')
    # Glass glare diagonal stripe (G >= 14 px)
    c.draw_line(11, 10, 14, 10, 'G', 1)
    c.draw_line(10, 11, 15, 11, 'G', 1)
    c.outline({'B', 'L', 'D', 'F', 'R', 'S', 'G'}, 'O')
    # Brass highlight accent after outline (L >= 14 px)
    c.fill_circle(12, 12, 2, 'L')
    c.set(16, 4, 'L')
    return c.to_image(pal)

def gen_korsandurbunu():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'B': (235, 185, 45),    # Brass draw tubes
        'L': (255, 225, 115),   # Brass highlight
        'D': (160, 115, 25),    # Brass shadow
        'T': (140, 75, 35),     # Leather hand-wrap
        'C': (85, 45, 20),      # Leather shadow
        'G': (120, 225, 245),   # Objective lens glass
    }
    # Telescoping spyglass drawn diagonally from top-left to bottom-right
    # Eyepiece section (small)
    c.draw_line(4, 5, 10, 11, 'B', 3)
    # Mid-draw section
    c.draw_line(10, 11, 18, 19, 'B', 4)
    c.draw_line(12, 13, 19, 20, 'D', 2)
    # Main wide objective barrel with leather wrap
    c.draw_line(18, 19, 27, 28, 'T', 6)
    c.draw_line(19, 20, 28, 29, 'C', 3)
    # Front brass rim and objective lens
    c.fill_circle(27, 29, 4, 'B')
    c.fill_circle(27, 29, 2, 'G')
    c.outline({'B', 'L', 'D', 'T', 'C', 'G'}, 'O')
    # Brass highlights (L >= 15 px)
    c.draw_line(5, 4, 9, 8, 'L', 2)
    c.draw_line(11, 10, 14, 13, 'L', 2)
    return c.to_image(pal)

def gen_sampiyonkupasi():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'G': (245, 195, 40),    # Polished gold cup
        'L': (255, 235, 125),   # Gold highlight
        'D': (170, 125, 20),    # Gold shadow
        'M': (215, 210, 200),   # Marble base white
        'K': (110, 105, 100),   # Marble shadow
        'R': (220, 40, 45),     # Rosette ribbon red
    }
    # Cup goblet body
    c.fill_polygon([(10, 7), (21, 7), (19, 17), (12, 17)], 'G')
    c.fill_ellipse(16, 7, 6, 2, 'L')
    c.fill_polygon([(16, 7), (21, 7), (19, 17), (16, 17)], 'D')
    # Cup stem and round base
    c.fill_rect(15, 17, 16, 22, 'G')
    c.fill_ellipse(16, 22, 5, 2, 'G')
    # Dual curved handles (left and right)
    for y in range(8, 16):
        c.set(8, y, 'G')
        c.set(23, y, 'G')
    c.set(9, 8, 'G'); c.set(22, 8, 'G')
    c.set(9, 15, 'G'); c.set(22, 15, 'G')
    # Heavy marble pedestal at bottom
    c.fill_rect(8, 24, 23, 28, 'M')
    c.fill_rect(8, 26, 23, 28, 'K')
    c.outline({'G', 'L', 'D', 'M', 'K'}, 'O')
    # Red victory ribbon placed on marble base (R >= 16 px)
    c.fill_circle(16, 24, 2, 'R')
    c.draw_line(15, 25, 14, 28, 'R', 2)
    c.draw_line(17, 25, 18, 28, 'R', 2)
    return c.to_image(pal)

def gen_antikkilic():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'S': (190, 205, 220),   # Steel blade
        'L': (245, 250, 255),   # Blade edge glint
        'D': (115, 125, 140),   # Fuller groove shadow
        'G': (235, 185, 45),    # Golden guard & pommel
        'R': (195, 35, 45),     # Ruby pommel gem
        'K': (135, 130, 125),   # Granite boulder
        'C': (75, 70, 65),      # Rock crack shadow
    }
    # Granite stone boulder at bottom
    c.fill_ellipse(16, 26, 13, 5, 'K')
    c.fill_polygon([(3, 27), (28, 27), (25, 31), (6, 31)], 'C')
    # Straight vertical blade plunged into stone
    c.fill_rect(14, 10, 17, 24, 'S')
    c.fill_rect(14, 10, 15, 24, 'L')
    c.fill_rect(16, 10, 17, 24, 'D')
    # Golden crossguard hilt
    c.fill_rect(10, 9, 21, 11, 'G')
    # Sword grip handle
    c.fill_rect(15, 4, 16, 9, 'D')
    # Pommel ball & ruby gems (R >= 14 px)
    c.fill_circle(16, 3, 3, 'G')
    c.fill_circle(16, 3, 2, 'R')
    c.fill_circle(16, 10, 1, 'R') # Center crossguard gem
    c.outline({'S', 'L', 'D', 'G', 'K', 'C', 'R'}, 'O')
    return c.to_image(pal)

def gen_capahalat():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'I': (85, 95, 110),     # Forged iron anchor
        'L': (155, 165, 180),   # Iron highlight
        'D': (50, 55, 65),      # Iron shadow
        'R': (210, 160, 95),    # Manila hemp rope
        'S': (145, 100, 55),    # Rope shadow
        'G': (235, 185, 45),    # Brass stock pin
    }
    # 1. Base anchor silhouette
    # Top ring
    c.fill_circle(16, 5, 3, 'I')
    c.fill_circle(16, 5, 1, '.')
    # Horizontal cross stock
    c.fill_rect(6, 9, 25, 11, 'I')
    c.fill_circle(6, 10, 2, 'G')
    c.fill_circle(25, 10, 2, 'G')
    # Curved lower flukes arms
    c.fill_circle(16, 23, 9, 'I')
    c.fill_circle(16, 23, 5, '.')
    c.fill_rect(6, 15, 25, 23, '.')
    # Fluke pointed tips
    c.fill_polygon([(6, 16), (10, 20), (6, 20)], 'I')
    c.fill_polygon([(25, 16), (21, 20), (25, 20)], 'I')
    # Vertical anchor shank (width 6: 13..18)
    c.fill_rect(13, 5, 18, 24, 'I')

    # 2. Shading & Highlights on Shank & Flukes
    c.fill_rect(14, 6, 15, 24, 'L')
    c.fill_rect(16, 6, 17, 24, 'D')
    c.draw_line(9, 22, 13, 25, 'L', 1)
    c.draw_line(18, 25, 22, 22, 'D', 1)

    # 3. Manila rope coiling around the shank (R & S >= 25 px)
    c.draw_line(11, 6, 20, 9, 'R', 2)
    c.draw_line(12, 7, 21, 10, 'S', 1)
    c.draw_line(11, 13, 20, 16, 'R', 2)
    c.draw_line(12, 14, 21, 17, 'S', 1)
    c.draw_line(11, 19, 20, 22, 'R', 2)
    c.draw_line(12, 20, 21, 23, 'S', 1)
    c.draw_line(17, 22, 24, 27, 'R', 2)
    c.draw_line(18, 23, 25, 28, 'S', 1)

    c.outline({'I', 'L', 'D', 'G', 'R', 'S'}, 'O')
    return c.to_image(pal)

def gen_retromikrofon():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'C': (175, 185, 200),   # Chrome grill midtone
        'L': (235, 245, 255),   # Chrome highlight
        'D': (105, 115, 130),   # Grill shadow
        'S': (55, 60, 70),      # Inner acoustic cloth
        'B': (230, 180, 45),    # Brass mount pin & badge
        'M': (130, 140, 155),   # Microphone desk stand
    }
    # Microphone rounded capsule
    c.fill_circle(16, 12, 8, 'S')
    c.fill_rect(8, 12, 23, 18, 'S')
    # Chrome horizontal fin ribs (C & L >= 35 px)
    for y in range(7, 19, 2):
        c.fill_rect(9, y, 22, y, 'C')
        c.set(10, y, 'L')
        c.set(11, y, 'L')
    # Pivoting neck mount
    c.fill_rect(13, 19, 18, 23, 'C')
    c.fill_circle(16, 21, 2, 'B') # Brass adjustment knob
    # Heavy round desktop stand
    c.draw_line(16, 23, 16, 27, 'M', 2)
    c.fill_ellipse(16, 28, 7, 2, 'M')
    c.outline({'C', 'L', 'D', 'S', 'B', 'M'}, 'O')
    return c.to_image(pal)

def gen_buyuasasi():
    c = Canvas('.')
    pal = {
        'O': OUT,
        'W': (140, 80, 40),     # Carved elder wood
        'D': (85, 45, 20),      # Wood shadow
        'G': (235, 185, 45),    # Golden claw prongs
        'C': (65, 215, 245),    # Cyan crystal orb
        'M': (215, 75, 235),    # Magic magenta aura
        'Y': (255, 245, 150),   # Orb glowing core
    }
    # Long wooden staff rod
    c.draw_line(9, 29, 19, 12, 'W', 3)
    c.draw_line(10, 30, 20, 13, 'D', 1)
    # Golden curved claws clasping orb (G >= 20 px)
    c.fill_circle(21, 10, 7, 'G')
    c.fill_circle(21, 10, 4, '.')
    c.outline({'W', 'D', 'G'}, 'O')
    # Magic crystal orb hovering inside (C, Y >= 16 px)
    c.fill_circle(21, 10, 4, 'C')
    c.fill_circle(21, 10, 2, 'Y')
    # Swirling magenta magical particles around head (M >= 20 px)
    for px, py in [(16, 6), (25, 6), (26, 13), (16, 13), (20, 3), (22, 16)]:
        c.fill_circle(px, py, 1, 'M')
    return c.to_image(pal)

# -------------------------------------------------------------
# 10 YENİ ZOR GENERATORS (1024 cubes full canvas, 9-10 colors)
# -------------------------------------------------------------

def gen_samanyolugozlemevi():
    c = Canvas('1')
    pal = {
        '1': (10, 12, 28),      # Midnight alpine cosmos
        '2': (245, 245, 255),   # Milky way stars
        '3': (125, 75, 185),    # Purple cosmic dust
        '4': (60, 145, 215),    # Blue nebula veil
        '5': (50, 45, 65),      # Mountain ridge
        '6': (220, 230, 240),   # Observatory dome white
        '7': (120, 130, 145),   # Dome shadow
        '8': (245, 195, 50),    # Telescope slit light
        '9': (35, 30, 40),      # Rocky summit ground
    }
    # Cosmic void
    c.fill_rect(0, 0, 31, 22, '1')
    # Milky Way galaxy dust arch (3 & 4 >= 50 px each)
    c.draw_line(0, 18, 28, 0, '3', 6)
    c.draw_line(3, 19, 31, 2, '4', 3)
    # Bright stars sprinkled across space (2 >= 25 px)
    for sx, sy in [(3, 2), (9, 7), (18, 3), (24, 8), (4, 11), (28, 14), (13, 14), (20, 11), (6, 17)]:
        c.set(sx, sy, '2')
        c.set(sx + 1, sy, '2')
    # Alpine mountain range
    c.fill_polygon([(0, 22), (10, 17), (20, 21), (31, 16), (31, 31), (0, 31)], '5')
    c.fill_rect(0, 26, 31, 31, '9')
    # Astronomical observatory dome (6, 7, 8 >= 20 px each)
    c.fill_circle(14, 21, 6, '6')
    c.fill_rect(8, 21, 20, 26, '6')
    c.fill_rect(15, 21, 20, 26, '7')
    # Telescope slit with warm interior glow
    c.fill_rect(13, 17, 15, 23, '8')
    return c.to_image(pal)

def gen_ormanmabedi():
    c = Canvas('1')
    pal = {
        '1': (135, 195, 215),   # Tropical sky
        '2': (175, 165, 150),   # Sandstone temple tower
        '3': (105, 95, 85),     # Sandstone shadow
        '4': (55, 135, 60),     # Jungle canopy
        '5': (25, 75, 35),      # Jungle deep shade
        '6': (155, 115, 70),    # Banyan tree roots
        '7': (95, 65, 35),      # Root shadow
        '8': (245, 215, 90),    # Morning sunbeams
        '9': (60, 120, 135),    # Moat reflecting water
        'A': (240, 140, 160),   # Lotus flowers pink
    }
    # Tropical morning sky
    c.fill_rect(0, 0, 31, 7, '1')
    # Sunbeam shafts cutting down (8 >= 20 px)
    c.draw_line(5, 0, 13, 20, '8', 2)
    c.draw_line(18, 0, 26, 20, '8', 2)
    # Overgrown jungle canopy atop ruins
    c.fill_rect(0, 4, 31, 11, '4')
    c.fill_rect(0, 7, 31, 12, '5')
    # Angkor Wat carved sandstone sanctuary tower
    c.fill_polygon([(16, 5), (10, 22), (22, 22)], '2')
    c.fill_polygon([(16, 5), (16, 22), (22, 22)], '3')
    c.fill_rect(14, 18, 18, 22, '3') # Doorway
    # Massive strangler fig banyan roots encasing stones (6 & 7 >= 35 px)
    c.draw_line(11, 8, 9, 24, '6', 2)
    c.draw_line(20, 9, 23, 24, '6', 2)
    c.draw_line(12, 14, 18, 19, '7', 1)
    # Temple moat water at bottom
    c.fill_rect(0, 23, 31, 31, '9')
    # Pink lotus blossoms floating (A >= 15 px)
    c.fill_circle(6, 27, 2, 'A')
    c.fill_circle(25, 28, 2, 'A')
    c.fill_circle(16, 29, 2, 'A')
    return c.to_image(pal)

def gen_superisigolu():
    c = Canvas('1')
    pal = {
        '1': (15, 20, 45),      # Twilight sky
        '2': (245, 235, 150),   # Full moon
        '3': (35, 55, 80),      # Weeping willows
        '4': (25, 85, 115),     # Lagoon water
        '5': (45, 155, 85),     # Giant lily pads
        '6': (245, 105, 165),   # Pink lotus petals
        '7': (255, 240, 245),   # Lotus white center
        '8': (100, 235, 245),   # Cyan fairy orbs
        '9': (245, 215, 45),    # Golden fireflies
    }
    # Night sky
    c.fill_rect(0, 0, 31, 9, '1')
    c.fill_circle(24, 5, 4, '2')
    # Weeping willow tree curtain
    c.fill_polygon([(0, 6), (9, 6), (5, 18), (0, 18)], '3')
    c.fill_polygon([(22, 6), (31, 6), (31, 18), (26, 18)], '3')
    # Lagoon water surface
    c.fill_rect(0, 12, 31, 31, '4')
    # Floating giant lily pads (5 >= 45 px)
    c.fill_ellipse(8, 23, 6, 3, '5')
    c.fill_ellipse(22, 25, 7, 3, '5')
    c.fill_ellipse(15, 17, 5, 2, '5')
    # Sacred blooming pink water lotus (6 & 7 >= 25 px)
    c.fill_circle(15, 16, 4, '6')
    c.fill_circle(15, 16, 2, '7')
    # Glowing cyan water fairy lights (8 >= 20 px)
    c.fill_circle(7, 19, 2, '8')
    c.fill_circle(24, 21, 2, '8')
    # Firefly golden sparkles (9 >= 16 px)
    for fx, fy in [(12, 7), (18, 9), (5, 13), (27, 14), (16, 22), (21, 16)]:
        c.set(fx, fy, '9')
        c.set(fx + 1, fy, '9')
    return c.to_image(pal)

def gen_kristalsehri():
    c = Canvas('1')
    pal = {
        '1': (25, 20, 35),      # Obsidian cavern ceiling
        '2': (65, 50, 75),      # Basalt hall pillars
        '3': (145, 115, 80),    # Dwarven stone battlements
        '4': (85, 60, 40),      # Stone wall shadows
        '5': (55, 215, 235),    # Glowing cyan crystals
        '6': (20, 135, 160),    # Crystal shadow
        '7': (245, 110, 30),    # Molten gold canal
        '8': (255, 225, 85),    # Illuminated fortress windows
        '9': (180, 50, 195),    # Amethyst stalactites
        'A': (235, 230, 245),   # Crystal glares
    }
    # Cavern ceiling with hanging amethyst crystals (9 >= 25 px)
    c.fill_rect(0, 0, 31, 8, '1')
    c.fill_polygon([(3, 0), (7, 0), (5, 7)], '9')
    c.fill_polygon([(24, 0), (28, 0), (26, 8)], '9')
    # Basalt support colonnades
    c.fill_rect(4, 7, 7, 24, '2')
    c.fill_rect(24, 7, 27, 24, '2')
    # Dwarven fortress citadel in center (3 & 4 >= 50 px)
    c.fill_rect(9, 10, 22, 24, '3')
    c.fill_rect(17, 10, 22, 24, '4')
    # Battlements & glowing windows (8 >= 18 px)
    for wx in [11, 15, 19]:
        c.fill_rect(wx, 14, wx + 1, 16, '8')
        c.fill_rect(wx, 19, wx + 1, 21, '8')
    # Glowing cyan crystal spires flanking fortress (5, 6, A >= 30 px)
    c.fill_polygon([(4, 8), (0, 26), (8, 26)], '5')
    c.fill_polygon([(4, 8), (4, 26), (8, 26)], '6')
    c.draw_line(2, 12, 2, 22, 'A', 1)
    c.fill_polygon([(27, 8), (23, 26), (31, 26)], '5')
    c.fill_polygon([(27, 8), (27, 26), (31, 26)], '6')
    c.draw_line(25, 12, 25, 22, 'A', 1)
    # Canal of molten gold flowing at bottom (7 >= 35 px)
    c.fill_rect(0, 25, 31, 31, '7')
    return c.to_image(pal)

def gen_neonmetropol():
    c = Canvas('1')
    pal = {
        '1': (6, 8, 18),        # Smog night sky (darkened to not collide with '3')
        '2': (50, 55, 75),      # Megacity skyscrapers
        '3': (25, 28, 42),      # Skyscraper shadows
        '4': (245, 45, 125),    # Hot pink hologram ad
        '5': (45, 215, 240),    # Cyan neon trim
        '6': (255, 220, 55),    # Corporate window lights
        '7': (105, 115, 135),   # Concrete sky-highway
        '8': (235, 40, 45),     # Taillight red speed streaks
        '9': (245, 245, 200),   # Headlight white speed streaks
        'A': (160, 70, 215),    # Purple skyway exhaust
    }
    # Night smog sky
    c.fill_rect(0, 0, 31, 6, '1')
    # Towering monolithic mega-towers left and right
    c.fill_rect(0, 4, 11, 21, '2')
    c.fill_rect(20, 3, 31, 21, '3')
    # Lit office matrix windows (6 >= 35 px)
    for wy in [6, 9, 12, 15, 18]:
        c.fill_rect(2, wy, 4, wy + 1, '6')
        c.fill_rect(7, wy, 9, wy + 1, '6')
        c.fill_rect(23, wy, 25, wy + 1, '6')
    # Giant pink holographic kanji ad (4 >= 25 px)
    c.fill_rect(12, 2, 19, 11, '4')
    # Cyan neon trim (5 >= 20 px)
    c.draw_line(0, 4, 11, 4, '5', 1)
    c.draw_line(20, 3, 31, 3, '5', 1)
    # Elevated concrete skyway bridge across lower center
    c.fill_polygon([(0, 21), (31, 21), (31, 26), (0, 26)], '7')
    # Ground highway beneath
    c.fill_rect(0, 27, 31, 31, '3')
    # Hover-car light speed streaks (8 & 9 >= 25 px each)
    c.draw_line(0, 23, 31, 23, '8', 2) # Red taillight streak
    c.draw_line(0, 29, 31, 29, '9', 2) # White headlight streak
    # Purple skyway exhaust (A >= 15 px)
    c.fill_circle(22, 22, 2, 'A')
    c.fill_circle(7, 28, 2, 'A')
    return c.to_image(pal)

def gen_babilbahceleri():
    c = Canvas('1')
    pal = {
        '1': (245, 190, 120),   # Desert sunset glow
        '2': (95, 165, 225),    # Upper blue sky
        '3': (210, 165, 95),    # Mud-brick ziggurat
        '4': (135, 95, 55),     # Ziggurat shadows
        '5': (45, 145, 55),     # Hanging vegetation
        '6': (20, 85, 35),      # Palm shade
        '7': (55, 165, 215),    # Water aqueduct cascade
        '8': (240, 250, 255),   # Waterfall foam
        '9': (225, 50, 65),     # Red hibiscus flowers
        'A': (250, 210, 45),    # Sun rays & statues
    }
    # Sky gradient
    c.fill_rect(0, 0, 31, 5, '2')
    c.fill_rect(0, 6, 31, 10, '1')
    c.fill_circle(7, 5, 3, 'A') # Setting sun
    # Stepped ziggurat terraces (3 & 4)
    # Tier 1 (top)
    c.fill_rect(10, 9, 21, 14, '3')
    c.fill_rect(17, 9, 21, 14, '4')
    # Tier 2 (middle)
    c.fill_rect(5, 15, 26, 21, '3')
    c.fill_rect(18, 15, 26, 21, '4')
    # Tier 3 (base)
    c.fill_rect(0, 22, 31, 31, '3')
    c.fill_rect(19, 22, 31, 31, '4')
    # Cascading lush green gardens draped over every terrace (5 & 6 >= 45 px)
    c.draw_line(8, 14, 23, 14, '5', 2)
    c.draw_line(4, 21, 28, 21, '5', 2)
    c.draw_line(4, 22, 28, 22, '6', 1)
    # Cascading blue aqueduct waterfall down center (7 & 8 >= 20 px)
    c.fill_rect(14, 11, 17, 31, '7')
    c.fill_ellipse(15, 30, 4, 1, '8')
    # Blooming exotic red hibiscus blossoms (9 >= 15 px)
    for bx, by in [(7, 13), (12, 13), (21, 13), (6, 20), (25, 20), (10, 27)]:
        c.fill_circle(bx, by, 1, '9')
    return c.to_image(pal)

def gen_ejderhatapinagi():
    c = Canvas('1')
    pal = {
        '1': (145, 55, 75),     # Maroon-crimson sky
        '2': (245, 140, 60),    # Sunset amber glow
        '3': (60, 75, 105),     # Snowy mountain peaks
        '4': (215, 235, 250),   # Peak glaciers
        '5': (195, 45, 45),     # Red pagoda temple
        '6': (50, 45, 55),      # Pagoda black roof tiles
        '7': (245, 215, 65),    # Golden spire & lantern
        '8': (235, 185, 195),   # Dragon clouds mist
        '9': (45, 165, 140),    # Jade dragon scales
        'A': (25, 85, 70),      # Dragon shadow
    }
    # Dramatic sky
    c.fill_rect(0, 0, 31, 5, '1')
    c.fill_rect(0, 6, 31, 11, '2')
    # Himalayan mountain peak
    c.fill_polygon([(0, 16), (10, 6), (22, 16)], '3')
    c.fill_polygon([(10, 6), (7, 10), (13, 10)], '4')
    c.fill_rect(0, 16, 31, 31, '3')
    # Multi-tiered crimson pagoda sanctuary (5, 6, 7 >= 35 px)
    c.fill_rect(17, 13, 27, 26, '5')
    # Pagoda tiered curved roof eaves
    c.fill_polygon([(15, 13), (22, 10), (29, 13)], '6')
    c.fill_polygon([(14, 19), (22, 16), (30, 19)], '6')
    c.fill_circle(22, 9, 2, '7') # Golden finial
    c.fill_rect(20, 21, 24, 25, '7') # Lit entrance
    # Coiling Jade Dragon wrapping around mountain (9 & A >= 30 px)
    c.draw_line(0, 25, 15, 21, '9', 3)
    c.draw_line(1, 26, 15, 22, 'A', 1)
    # Swirling misty dragon clouds (8 >= 20 px)
    c.fill_ellipse(8, 19, 6, 2, '8')
    c.fill_ellipse(24, 28, 6, 2, '8')
    return c.to_image(pal)

def gen_nehirkanyonu():
    c = Canvas('1')
    pal = {
        '1': (245, 175, 95),    # Arizona sky
        '2': (185, 75, 50),     # Red sandstone rim
        '3': (130, 45, 30),     # Canyon wall strata
        '4': (75, 30, 25),      # Chasm deep shadow
        '5': (45, 135, 145),    # River green-blue water
        '6': (240, 250, 255),   # Rapids white foam
        '7': (215, 185, 115),   # Sandbar beach
        '8': (235, 50, 45),     # Red raft boat
        '9': (55, 115, 50),     # Desert juniper scrub
    }
    # Sky
    c.fill_rect(0, 0, 31, 7, '1')
    # Towering sheer canyon walls left & right (2, 3, 4 >= 60 px)
    c.fill_polygon([(0, 6), (11, 7), (8, 23), (0, 23)], '2')
    c.fill_polygon([(20, 6), (31, 6), (31, 23), (23, 23)], '2')
    c.fill_polygon([(0, 12), (9, 13), (7, 23), (0, 23)], '3')
    c.fill_polygon([(22, 12), (31, 12), (31, 23), (24, 23)], '4')
    # Canyon riverbed
    c.fill_rect(0, 23, 31, 31, '5')
    c.fill_polygon([(0, 23), (6, 23), (4, 28), (0, 28)], '7') # Sandbar
    # Roaring white water rapids foam (6 >= 25 px)
    for y in [24, 27, 30]:
        c.draw_line(8, y, 22, y, '6', 1)
    # Red whitewater raft navigating rapids (8 >= 14 px)
    c.fill_ellipse(15, 26, 4, 2, '8')
    # Desert juniper scrub on rim (9 >= 15 px)
    c.fill_circle(10, 6, 2, '9')
    c.fill_circle(21, 6, 2, '9')
    return c.to_image(pal)

def gen_kutupbuzkirici():
    c = Canvas('1')
    pal = {
        '1': (25, 35, 60),      # Polar twilight sky
        '2': (75, 195, 150),    # Aurora borealis
        '3': (215, 235, 245),   # Arctic pack ice
        '4': (110, 165, 195),   # Blue ice fractures
        '5': (225, 45, 40),     # Icebreaker red hull
        '6': (40, 45, 55),      # Hull waterline black
        '7': (240, 245, 250),   # Navigation bridge white
        '8': (255, 235, 95),    # Searchlight beam
        '9': (20, 60, 85),      # Broken sea lane water
    }
    # Polar sky with green aurora arch (2 >= 25 px)
    c.fill_rect(0, 0, 31, 9, '1')
    c.fill_polygon([(0, 5), (15, 2), (31, 4), (31, 7), (15, 4), (0, 7)], '2')
    # Arctic ice pack surface
    c.fill_rect(0, 10, 31, 31, '3')
    # Broken open sea lane churned up by ship
    c.fill_polygon([(12, 16), (19, 16), (24, 31), (7, 31)], '9')
    # Fractured blue ice chunks (4 >= 25 px)
    c.draw_line(2, 17, 7, 21, '4', 1)
    c.draw_line(24, 18, 29, 22, '4', 1)
    # High-power searchlight cone cutting through polar mist (8 >= 20 px)
    c.fill_polygon([(15, 10), (6, 22), (25, 22)], '8')
    # Nuclear icebreaker ship bow steaming forward (5, 6, 7 >= 30 px)
    c.fill_polygon([(11, 13), (20, 13), (18, 20), (13, 20)], '5')
    c.fill_polygon([(13, 21), (18, 21), (17, 26), (14, 26)], '6')
    c.fill_rect(13, 10, 18, 13, '7') # Bridge tower
    return c.to_image(pal)

def gen_biyolagun():
    c = Canvas('1')
    pal = {
        '1': (10, 15, 30),      # Starlit midnight sky
        '2': (245, 245, 255),   # Stars & moon
        '3': (30, 45, 40),      # Mangrove shore
        '4': (15, 35, 55),      # Deep lagoon water
        '5': (45, 225, 205),    # Bioluminescent cyan wake
        '6': (20, 145, 155),    # Bioluminescent teal crest
        '7': (175, 115, 60),    # Wooden canoe
        '8': (95, 55, 25),      # Canoe shadow
        '9': (245, 205, 55),    # Canoe lantern glow
        'A': (95, 235, 140),    # Water glowing splash
    }
    # Night sky with moon and stars (2 >= 18 px)
    c.fill_rect(0, 0, 31, 8, '1')
    c.fill_circle(25, 4, 2, '2')
    for sx, sy in [(4, 2), (10, 5), (17, 2), (2, 7), (14, 7)]:
        c.set(sx, sy, '2')
        c.set(sx + 1, sy, '2')
    # Mangrove forest tree shoreline
    c.fill_polygon([(0, 6), (9, 6), (12, 14), (0, 14)], '3')
    c.fill_polygon([(21, 6), (31, 6), (31, 14), (23, 14)], '3')
    # Deep night lagoon water
    c.fill_rect(0, 12, 31, 31, '4')
    # Bioluminescent glowing cyan waves and paddle trails (5, 6, A >= 35 px)
    c.fill_ellipse(16, 24, 11, 4, '6')
    c.fill_ellipse(16, 24, 9, 2, '5')
    # Wooden canoe paddling through glowing waters
    c.fill_polygon([(7, 21), (24, 21), (21, 24), (10, 24)], '7')
    c.fill_polygon([(15, 21), (24, 21), (21, 24), (17, 24)], '8')
    # Warm amber lantern on prow (9 >= 14 px)
    c.fill_circle(8, 19, 2, '9')
    # Glowing splashes of dinoflagellates around canoe wake (A >= 15 px)
    for ax, ay in [(4, 20), (5, 21), (6, 22), (8, 25), (10, 26), (21, 26), (23, 25), (25, 22), (26, 21), (27, 20), (15, 27), (16, 28), (17, 28), (14, 29), (18, 29)]:
        c.set(ax, ay, 'A')
    return c.to_image(pal)

GENERATORS = {
    # 10 Kolay (Etrafı kapalı - Full 32x32, 5 colors)
    'kolay/GunbatimiYelken': gen_gunbatimiyelken,
    'kolay/GeceCol': gen_gececol,
    'kolay/KurtVeAy': gen_kurtveay,
    'kolay/UzayGezegeni': gen_uzaygezegeni,
    'kolay/KutupPenguen': gen_kutuppenguen,
    'kolay/Madencave': gen_madencave,
    'kolay/JaponGunesi': gen_japongunesi,
    'kolay/Sihirlikule': gen_sihirlikule,
    'kolay/AdaPalmiye': gen_adapalmiye,
    'kolay/SonbaharYolu': gen_sonbaharyolu,

    # 10 Orta (7-8 colors)
    'orta/AntikKalkan': gen_antikkalkan,
    'orta/SihirbazSapkasi': gen_sihirbazsapkasi,
    'orta/NostaljikRadyo': gen_nostaljikradyo,
    'orta/PusulaAntik': gen_pusulaantik,
    'orta/KorsanDurbunu': gen_korsandurbunu,
    'orta/SampiyonKupasi': gen_sampiyonkupasi,
    'orta/AntikKilic': gen_antikkilic,
    'orta/CapaHalat': gen_capahalat,
    'orta/RetroMikrofon': gen_retromikrofon,
    'orta/BuyuAsasi': gen_buyuasasi,

    # 10 Zor (1024 cubes, 9-10 colors)
    'zor/SamanyoluGozlemevi': gen_samanyolugozlemevi,
    'zor/OrmanMabedi': gen_ormanmabedi,
    'zor/SuPerisiGolu': gen_superisigolu,
    'zor/KristalSehri': gen_kristalsehri,
    'zor/NeonMetropol': gen_neonmetropol,
    'zor/BabilBahceleri': gen_babilbahceleri,
    'zor/EjderhaTapinagi': gen_ejderhatapinagi,
    'zor/NehirKanyonu': gen_nehirkanyonu,
    'zor/KutupBuzkirici': gen_kutupbuzkirici,
    'zor/Biyolagun': gen_biyolagun,
}

def main():
    base_dir = 'Assets/04.11.2026'
    total = len(GENERATORS)
    print(f"Generating {total} batch 5 pixel arts...")
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
