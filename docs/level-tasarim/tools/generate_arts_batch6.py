"""Pixel art generator for PixelGame - Batch 6 (20 new levels: 10 Kolay, 10 Orta).
Both Kolay and Orta are full 32x32 canvas (etrafi kapali / 1024 cubes).
- 10 Kolay: 5 colors, full 32x32 canvas.
- 10 Orta: 7-8 colors, full 32x32 canvas.
Validates all images against game rules (no close colors, min pixels >= 10, exact dimensions, alpha 0 or 255).
"""
import os, sys, uuid, re, collections, itertools, math
from PIL import Image

TEX_META_TEMPLATE = 'Assets/32x32/Saturn.png.meta'

def make_meta(png_path):
    meta_path = png_path + '.meta'
    if not os.path.exists(meta_path):
        tmpl = open(TEX_META_TEMPLATE).read()
        meta = re.sub(r'guid: \w+', 'guid: ' + uuid.uuid4().hex, tmpl, count=1)
        with open(meta_path, 'w') as f:
            f.write(meta)

class Canvas:
    def __init__(self, bg='1'):
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

    def to_image(self, palette):
        im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
        px = im.load()
        for y in range(32):
            for x in range(32):
                ch = self.grid[y][x]
                if ch in palette:
                    r, g, b = palette[ch]
                    px[x, y] = (r, g, b, 255)
        return im

# ==============================================================================
# 10 KOLAY SEVIYE (ETRAFI KAPALI - FULL 32x32 CANVAS, 5 RENK)
# ==============================================================================

def gen_buzulayisi():
    c = Canvas('1')
    pal = {
        '1': (20, 30, 55),      # Arctic night sky
        '2': (70, 195, 140),    # Aurora borealis band
        '3': (210, 235, 250),   # Iceberg snow plateau
        '4': (100, 155, 195),   # Ice cliff shadow
        '5': (255, 250, 235),   # Polar bear fur
    }
    # Aurora arch
    c.fill_polygon([(0, 4), (16, 1), (31, 3), (31, 6), (16, 3), (0, 6)], '2')
    # Massive ice cliff plateau
    c.fill_polygon([(0, 16), (22, 14), (31, 18), (31, 31), (0, 31)], '3')
    # Ice shelf shadows
    c.fill_polygon([(0, 22), (22, 19), (31, 23), (31, 31), (0, 31)], '4')
    # Polar bear standing on ice ridge (5 >= 35 px)
    # Body
    c.fill_ellipse(13, 11, 4, 2, '5')
    # Bear head & muzzle looking up at sky
    c.fill_circle(18, 10, 2, '5')
    c.set(20, 9, '5'); c.set(20, 10, '5')
    # Legs
    c.fill_rect(10, 13, 11, 15, '5')
    c.fill_rect(15, 13, 16, 15, '5')
    return c.to_image(pal)

def gen_denizfeneriada():
    c = Canvas('1')
    pal = {
        '1': (25, 30, 50),      # Night sky
        '2': (30, 80, 130),     # Dark ocean waves
        '3': (255, 235, 90),    # Lighthouse yellow lantern beam
        '4': (240, 245, 250),   # Lighthouse white tower & surf
        '5': (65, 55, 70),      # Dark rocky islet
    }
    # Ocean surface
    c.fill_rect(0, 18, 31, 31, '2')
    # Rocky islet
    c.fill_polygon([(6, 21), (25, 21), (28, 31), (3, 31)], '5')
    # Lighthouse lantern diagonal beam cutting across night sky
    c.fill_polygon([(15, 7), (0, 10), (0, 17)], '3')
    c.fill_polygon([(17, 7), (31, 12), (31, 20)], '3')
    # Lighthouse white tower
    c.fill_polygon([(14, 8), (17, 8), (18, 22), (13, 22)], '4')
    # Crashing surf wave foam at base
    c.draw_line(4, 28, 27, 28, '4', 1)
    return c.to_image(pal)

def gen_sualtimercan():
    c = Canvas('1')
    pal = {
        '1': (15, 60, 120),     # Deep ocean blue
        '2': (65, 160, 220),    # Surface sunlight rays
        '3': (235, 75, 120),    # Pink branch coral
        '4': (215, 185, 110),   # Sandy seabed
        '5': (250, 130, 30),    # Clownfish orange body
    }
    # Diagonal sunlight rays streaming from surface
    c.fill_polygon([(3, 0), (7, 0), (14, 20), (10, 20)], '2')
    c.fill_polygon([(18, 0), (22, 0), (29, 20), (25, 20)], '2')
    # Sandy seabed
    c.fill_rect(0, 24, 31, 31, '4')
    # Pink branching reef corals (3 >= 45 px)
    c.fill_rect(2, 17, 5, 25, '3')
    c.fill_rect(7, 19, 10, 25, '3')
    c.fill_circle(4, 16, 2, '3')
    c.fill_circle(9, 18, 2, '3')
    c.fill_rect(25, 16, 28, 25, '3')
    c.fill_circle(27, 15, 2, '3')
    # Orange clownfish swimming in midwater (5 >= 25 px)
    c.fill_ellipse(18, 12, 3, 2, '5')
    c.fill_polygon([(15, 12), (13, 10), (13, 14)], '5') # Tail fin
    c.fill_ellipse(11, 8, 2, 1, '5')
    return c.to_image(pal)

def gen_kanyonkartali():
    c = Canvas('1')
    pal = {
        '1': (245, 160, 60),    # Sunset orange sky
        '2': (255, 235, 120),   # Radiant golden sun
        '3': (165, 55, 40),     # Red sandstone mesa
        '4': (85, 30, 30),      # Canyon valley shadow
        '5': (30, 25, 35),      # Soaring eagle silhouette
    }
    # Giant setting sun
    c.fill_circle(16, 12, 6, '2')
    # Left and right stepped canyon mesas
    c.fill_polygon([(0, 14), (8, 14), (11, 31), (0, 31)], '3')
    c.fill_polygon([(23, 13), (31, 13), (31, 31), (20, 31)], '3')
    # Deep canyon shadows
    c.fill_polygon([(0, 22), (9, 22), (12, 31), (0, 31)], '4')
    c.fill_polygon([(22, 21), (31, 21), (31, 31), (19, 31)], '4')
    c.fill_rect(9, 27, 22, 31, '4')
    # Soaring eagle in center sky (5 >= 20 px)
    # Wings
    c.fill_polygon([(8, 7), (16, 10), (24, 7), (16, 8)], '5')
    # Body & tail
    c.fill_ellipse(16, 9, 1, 2, '5')
    return c.to_image(pal)

def gen_kampatesigece():
    c = Canvas('1')
    pal = {
        '1': (15, 20, 40),      # Midnight starry sky
        '2': (25, 55, 40),      # Pine tree silhouettes
        '3': (55, 95, 50),      # Forest clearing ground
        '4': (195, 150, 95),    # Canvas triangle tent
        '5': (250, 110, 25),    # Blazing campfire flame
    }
    # Pine trees flanking the sky
    c.fill_polygon([(0, 4), (4, 18), (0, 18)], '2')
    c.fill_polygon([(27, 3), (31, 3), (31, 18), (25, 18)], '2')
    # Ground
    c.fill_rect(0, 18, 31, 31, '3')
    # Canvas tent on left (4 >= 40 px)
    c.fill_polygon([(3, 27), (12, 15), (15, 27)], '4')
    # Campfire logs & bright blazing flame (5 >= 25 px)
    c.fill_polygon([(21, 22), (24, 16), (27, 22)], '5')
    c.fill_polygon([(22, 23), (25, 23), (26, 26), (20, 26)], '5')
    return c.to_image(pal)

def gen_samuraytapinak():
    c = Canvas('1')
    pal = {
        '1': (45, 30, 60),      # Deep twilight purple
        '2': (225, 45, 50),     # Giant blood moon
        '3': (85, 65, 105),     # Distant mountain ridge
        '4': (185, 35, 35),     # Red Torii gate
        '5': (20, 15, 25),      # Pagoda & stone steps silhouette
    }
    # Giant crimson moon
    c.fill_circle(16, 9, 7, '2')
    # Distant mountain ridge
    c.fill_polygon([(0, 15), (14, 9), (31, 16), (31, 22), (0, 22)], '3')
    # Ground / stone terrace
    c.fill_rect(0, 22, 31, 31, '5')
    # Red Torii gate in center (4 >= 45 px)
    # Top curved beam
    c.fill_rect(8, 14, 24, 15, '4')
    c.fill_rect(9, 17, 23, 17, '4')
    # Vertical pillars
    c.fill_rect(10, 15, 11, 26, '4')
    c.fill_rect(21, 15, 22, 26, '4')
    # Stone lantern silhouette at base
    c.fill_rect(5, 23, 7, 27, '5')
    c.fill_rect(25, 23, 27, 27, '5')
    return c.to_image(pal)

def gen_volkankrateri():
    c = Canvas('1')
    pal = {
        '1': (40, 35, 45),      # Dark ashen sky
        '2': (95, 70, 60),      # Volcano basalt slope
        '3': (250, 80, 20),     # Bubbling lava lake
        '4': (255, 215, 45),    # Golden molten hotspots
        '5': (155, 145, 155),   # Rising ash smoke plume
    }
    # Ash smoke plume billowing into sky (5 >= 45 px)
    c.fill_ellipse(16, 7, 6, 4, '5')
    c.fill_ellipse(14, 4, 4, 3, '5')
    # Volcano cone slopes
    c.fill_polygon([(0, 31), (9, 16), (23, 16), (31, 31)], '2')
    # Glowing lava caldera crater (3 >= 40 px)
    c.fill_ellipse(16, 17, 6, 2, '3')
    c.fill_polygon([(15, 18), (17, 18), (18, 27), (14, 27)], '3') # Lava river
    # Golden hot spots in crater (4 >= 15 px)
    c.fill_ellipse(16, 17, 3, 1, '4')
    c.set(16, 21, '4'); c.set(15, 24, '4')
    return c.to_image(pal)

def gen_ucanbalonkapadokya():
    c = Canvas('1')
    pal = {
        '1': (245, 190, 145),   # Pastel dawn sky
        '2': (185, 135, 95),    # Fairy chimneys rock
        '3': (115, 75, 55),     # Valley canyon shadows
        '4': (215, 40, 45),     # Hot air balloon red stripes
        '5': (250, 215, 50),    # Hot air balloon yellow stripes
    }
    # Pointed fairy chimney rock cones
    c.fill_polygon([(1, 31), (5, 18), (9, 31)], '2')
    c.fill_polygon([(8, 31), (13, 21), (18, 31)], '2')
    c.fill_polygon([(22, 31), (27, 17), (31, 31)], '2')
    c.fill_rect(0, 27, 31, 31, '3')
    # Ascending hot air balloon in upper sky
    # Yellow base envelope
    c.fill_circle(16, 9, 6, '5')
    c.fill_polygon([(11, 10), (21, 10), (18, 16), (14, 16)], '5')
    # Red vibrant vertical stripes (4 >= 30 px)
    c.fill_rect(15, 3, 17, 15, '4')
    c.fill_polygon([(11, 6), (13, 6), (13, 13), (12, 13)], '4')
    c.fill_polygon([(19, 6), (21, 6), (20, 13), (19, 13)], '4')
    # Basket
    c.fill_rect(15, 17, 17, 18, '3')
    return c.to_image(pal)

def gen_gecedegirmeni():
    c = Canvas('1')
    pal = {
        '1': (20, 25, 55),      # Starry indigo night
        '2': (250, 215, 75),    # Golden crescent moon
        '3': (45, 40, 50),      # Windmill body silhouette
        '4': (235, 240, 245),   # Windmill white sails
        '5': (205, 45, 110),    # Vibrant tulip field
    }
    # Crescent moon
    c.fill_circle(24, 6, 4, '2')
    c.fill_circle(26, 5, 4, '1')
    # Tulip field rows at bottom (5 >= 60 px)
    c.fill_rect(0, 23, 31, 31, '5')
    c.draw_line(0, 26, 31, 26, '3', 1)
    # Dutch windmill tapered octagonal body (3 >= 50 px)
    c.fill_polygon([(10, 14), (18, 14), (20, 24), (8, 24)], '3')
    c.fill_polygon([(9, 14), (14, 10), (19, 14)], '3') # Windmill cap
    # White diagonal turning sails (4 >= 25 px)
    c.draw_line(7, 6, 21, 18, '4', 1)
    c.draw_line(7, 18, 21, 6, '4', 1)
    c.fill_rect(13, 11, 15, 13, '2') # Golden hub
    return c.to_image(pal)

def gen_tropiklagun():
    c = Canvas('1')
    pal = {
        '1': (110, 195, 245),   # Azure sky
        '2': (40, 175, 185),    # Turquoise shallow ocean
        '3': (245, 235, 205),   # Sand beach
        '4': (130, 85, 45),     # Palm tree curved trunk
        '5': (35, 145, 55),     # Tropical palm fronds
    }
    # Turquoise sea
    c.fill_rect(0, 14, 31, 21, '2')
    # Sand beach curve
    c.fill_polygon([(0, 22), (31, 20), (31, 31), (0, 31)], '3')
    # Arching coconut palm trunk (4 >= 20 px)
    c.draw_line(26, 26, 21, 12, '4', 2)
    # Palm fronds fan arching over the lagoon (5 >= 40 px)
    c.draw_line(21, 12, 10, 11, '5', 2)
    c.draw_line(21, 12, 13, 17, '5', 2)
    c.draw_line(21, 12, 15, 6, '5', 2)
    c.draw_line(21, 12, 28, 8, '5', 2)
    return c.to_image(pal)

# ==============================================================================
# 10 ORTA SEVIYE (ETRAFI KAPALI - FULL 32x32 CANVAS, 7-8 RENK)
# ==============================================================================

def gen_demiryolukoprusu():
    c = Canvas('1')
    pal = {
        '1': (105, 165, 235),   # Alpine blue sky
        '2': (240, 245, 255),   # Mountain snowcaps
        '3': (35, 95, 60),      # Pine forest ridge
        '4': (160, 150, 140),   # Stone viaduct bridge
        '5': (75, 65, 60),      # Viaduct arch shadow
        '6': (25, 25, 35),      # Steam train locomotive
        '7': (215, 45, 40),     # Passenger coaches red
        '8': (195, 205, 215),   # Steam smoke cloud
    }
    # Alpine snowy mountain peaks
    c.fill_polygon([(0, 11), (9, 3), (18, 11)], '2')
    c.fill_polygon([(15, 12), (24, 4), (31, 12)], '2')
    # Pine forest mountain slopes
    c.fill_rect(0, 11, 31, 16, '3')
    # Deep valley below
    c.fill_rect(0, 24, 31, 31, '3')
    # Massive arched stone viaduct bridge (4 & 5 >= 70 px)
    c.fill_rect(0, 17, 31, 24, '4')
    # Bridge arches
    c.fill_circle(7, 24, 4, '5')
    c.fill_circle(16, 24, 4, '5')
    c.fill_circle(25, 24, 4, '5')
    # Steam train crossing viaduct
    c.fill_rect(16, 13, 23, 17, '6') # Locomotive
    c.fill_rect(8, 13, 14, 17, '7')  # Red coach
    c.fill_rect(2, 13, 6, 17, '7')   # Red coach 2
    # Puffy steam cloud (8 >= 20 px)
    c.fill_circle(24, 10, 3, '8')
    c.fill_circle(27, 8, 2, '8')
    return c.to_image(pal)

def gen_venedikgondol():
    c = Canvas('1')
    pal = {
        '1': (225, 135, 110),   # Venetian dusk sky
        '2': (195, 155, 105),   # Historic palazzo facade
        '3': (65, 45, 40),      # Arched bridge & window shadows
        '4': (30, 95, 90),      # Canal emerald water
        '5': (20, 20, 28),      # Black gondola
        '6': (235, 50, 55),     # Gondolier red shirt & ribbons
        '7': (75, 160, 140),    # Water reflections
        '8': (255, 215, 65),    # Glowing canal lantern
    }
    # Historic palazzo buildings on left and right
    c.fill_rect(0, 4, 10, 20, '2')
    c.fill_rect(22, 5, 31, 20, '2')
    # Palazzo windows
    for wy in [8, 13]:
        c.fill_rect(3, wy, 6, wy + 2, '3')
        c.fill_rect(25, wy, 28, wy + 2, '3')
    # Distant arched stone bridge
    c.fill_rect(10, 12, 22, 16, '2')
    c.fill_circle(16, 17, 4, '3')
    # Emerald canal water
    c.fill_rect(0, 18, 31, 31, '4')
    # Water ripples & reflections
    c.draw_line(0, 24, 31, 24, '7', 1)
    c.draw_line(4, 28, 27, 28, '7', 1)
    # Black gondola in foreground (5 >= 35 px)
    c.fill_polygon([(4, 25), (27, 25), (23, 28), (8, 28)], '5')
    c.fill_polygon([(26, 23), (28, 23), (27, 25)], '5') # Ferro metal prow
    # Gondolier standing with oar (6 >= 15 px)
    c.fill_rect(11, 19, 13, 23, '6')
    # Glowing bridge lantern (8 >= 14 px)
    c.fill_circle(16, 12, 2, '8')
    return c.to_image(pal)

def gen_antikpiramitgece():
    c = Canvas('1')
    pal = {
        '1': (15, 20, 45),      # Starry desert night
        '2': (255, 235, 140),   # Stars & moon glow
        '3': (215, 160, 95),    # Pyramid moonlit face
        '4': (115, 80, 45),     # Pyramid shadow face
        '5': (175, 125, 70),    # Sand dunes
        '6': (75, 50, 35),      # Sphinx silhouette
        '7': (45, 115, 65),     # Oasis palms green
    }
    # Crescent moon & stars (2 >= 18 px)
    c.fill_circle(6, 6, 3, '2')
    c.fill_circle(7, 5, 3, '1')
    for sx, sy in [(16, 3), (25, 4), (20, 8), (29, 9)]:
        c.set(sx, sy, '2')
        c.set(sx + 1, sy, '2')
    # Great Pyramid in center (3 & 4 >= 60 px)
    c.fill_polygon([(16, 8), (6, 22), (16, 22)], '3')
    c.fill_polygon([(16, 8), (16, 22), (27, 22)], '4')
    # Sand dunes foreground
    c.fill_polygon([(0, 22), (31, 20), (31, 31), (0, 31)], '5')
    # Great Sphinx monument on right (6 >= 25 px)
    c.fill_rect(21, 23, 28, 27, '6')
    c.fill_circle(23, 22, 2, '6')
    # Tiny oasis palm trees on left (7 >= 20 px)
    c.fill_polygon([(2, 21), (4, 18), (6, 21)], '7')
    c.fill_polygon([(6, 21), (8, 17), (10, 21)], '7')
    return c.to_image(pal)

def gen_siberyagmuru():
    c = Canvas('1')
    pal = {
        '1': (15, 18, 30),      # Dark rain sky
        '2': (55, 50, 65),      # Concrete alley walls
        '3': (45, 220, 240),    # Neon cyan sign
        '4': (245, 40, 130),    # Neon magenta billboard
        '5': (255, 225, 55),    # Yellow street lamp
        '6': (225, 45, 50),     # Vending machine red
        '7': (85, 90, 115),     # Wet asphalt pavement
        '8': (160, 195, 225),   # Rain streaks
    }
    # Alley building perspective
    c.fill_polygon([(0, 0), (9, 4), (9, 23), (0, 27)], '2')
    c.fill_polygon([(31, 0), (22, 4), (22, 23), (31, 27)], '2')
    # Wet asphalt pavement
    c.fill_polygon([(0, 27), (31, 27), (31, 31), (0, 31)], '7')
    # Cyan neon sign on left building (3 >= 20 px)
    c.fill_rect(2, 6, 6, 15, '3')
    # Magenta neon billboard on right building (4 >= 25 px)
    c.fill_rect(24, 7, 29, 14, '4')
    # Drink vending machine glowing on pavement (6 & 5 >= 25 px)
    c.fill_rect(12, 18, 17, 26, '6')
    c.fill_rect(13, 20, 16, 23, '5') # Glowing display
    # Diagonal rain streaks falling (8 >= 25 px)
    for rx, ry in [(4, 2), (12, 1), (19, 3), (27, 2), (7, 10), (16, 9), (22, 11), (5, 19), (18, 18), (14, 28), (24, 28)]:
        c.draw_line(rx, ry, rx + 1, ry + 2, '8', 1)
    return c.to_image(pal)

def gen_mantarkoyu():
    c = Canvas('1')
    pal = {
        '1': (30, 35, 60),      # Twilight forest sky
        '2': (220, 45, 45),     # Giant mushroom red cap
        '3': (245, 240, 235),   # Mushroom white spots & stalk
        '4': (255, 225, 70),    # Lit cottage window
        '5': (65, 135, 55),     # Mossy meadow
        '6': (30, 65, 30),      # Deep forest foliage
        '7': (65, 165, 215),    # Winding stream brook
        '8': (185, 95, 235),    # Magical fairy orbs
    }
    # Forest canopy background
    c.fill_polygon([(0, 6), (12, 2), (22, 7), (31, 3), (31, 15), (0, 15)], '6')
    # Mossy ground
    c.fill_rect(0, 16, 31, 31, '5')
    # Crystal brook winding through village
    c.fill_polygon([(9, 21), (15, 21), (22, 31), (15, 31)], '7')
    # Giant toadstool mushroom house (center)
    c.fill_ellipse(15, 12, 9, 5, '2')
    # White mushroom spots
    c.fill_circle(11, 10, 1, '3')
    c.fill_circle(15, 8, 1, '3')
    c.fill_circle(19, 10, 1, '3')
    # Stalk house base
    c.fill_rect(13, 16, 17, 23, '3')
    # Lit circular window & door (4 >= 14 px)
    c.fill_circle(15, 18, 2, '4')
    c.fill_rect(14, 21, 16, 23, '2')
    # Magical glowing fairy orbs in air (8 >= 15 px)
    for fx, fy in [(5, 7), (25, 8), (4, 18), (26, 17), (9, 14)]:
        c.fill_circle(fx, fy, 1, '8')
    return c.to_image(pal)

def gen_kacakcilimani():
    c = Canvas('1')
    pal = {
        '1': (18, 25, 45),      # Night sky
        '2': (95, 85, 80),      # Skull rock cliff
        '3': (40, 35, 40),      # Cavern shadow
        '4': (25, 80, 100),     # Hidden cove water
        '5': (140, 85, 45),     # Wooden pirate sloop hull
        '6': (220, 225, 235),   # Furled white sails
        '7': (250, 195, 45),    # Golden dock lantern
        '8': (205, 40, 45),     # Red skull flag & tavern roof
    }
    # Rocky cavern entrance arching overhead
    c.fill_polygon([(0, 0), (14, 0), (7, 14), (0, 14)], '2')
    c.fill_polygon([(19, 0), (31, 0), (31, 14), (25, 14)], '2')
    c.fill_rect(0, 0, 31, 3, '3')
    # Cove water
    c.fill_rect(0, 17, 31, 31, '4')
    # Pirate sloop moored at wooden dock (5 & 6 >= 45 px)
    c.fill_polygon([(6, 21), (21, 21), (18, 26), (9, 26)], '5')
    # Mast & furled sails
    c.fill_rect(13, 8, 14, 21, '5')
    c.fill_rect(9, 11, 18, 13, '6')
    # Jolly Roger red flag fluttering on mast top (8 >= 14 px)
    c.fill_polygon([(14, 8), (19, 9), (14, 11)], '8')
    # Wooden pier with glowing lantern (7 >= 15 px)
    c.fill_rect(21, 22, 31, 24, '5')
    c.fill_circle(25, 20, 2, '7')
    return c.to_image(pal)

def gen_gokdelencatisi():
    c = Canvas('1')
    pal = {
        '1': (235, 130, 90),    # Sunset skyline orange
        '2': (95, 55, 105),     # Upper sky purple dusk
        '3': (55, 50, 75),      # Distant skyscrapers
        '4': (255, 230, 95),    # Office window lights
        '5': (135, 140, 150),   # Concrete helipad rooftop
        '6': (245, 195, 40),    # Helipad yellow 'H' circle
        '7': (235, 45, 50),     # Red perimeter beacon lights
        '8': (30, 28, 40),      # AC chiller machinery
    }
    # Dusk gradient sky
    c.fill_rect(0, 0, 31, 4, '2')
    c.fill_rect(0, 5, 31, 10, '1')
    # Distant towering skyscraper silhouettes
    c.fill_rect(0, 8, 6, 20, '3')
    c.fill_rect(8, 6, 15, 20, '3')
    c.fill_rect(23, 7, 31, 20, '3')
    # Windows lit in skyscrapers
    for wx, wy in [(2, 10), (2, 14), (10, 8), (10, 12), (10, 16), (25, 10), (25, 14), (28, 12)]:
        c.fill_rect(wx, wy, wx + 1, wy + 1, '4')
    # Rooftop foreground
    c.fill_rect(0, 19, 31, 31, '5')
    # Helipad circle and 'H' marking (6 >= 30 px)
    c.fill_ellipse(16, 25, 7, 4, '6')
    c.fill_ellipse(16, 25, 5, 2, '5')
    # Letter 'H'
    c.fill_rect(14, 24, 14, 26, '6')
    c.fill_rect(18, 24, 18, 26, '6')
    c.fill_rect(14, 25, 18, 25, '6')
    # Red perimeter beacon lights (7 >= 14 px)
    for bx in [2, 10, 22, 30]:
        c.fill_rect(bx, 19, bx + 1, 20, '7')
    # Machinery housing on roof
    c.fill_rect(2, 22, 6, 26, '8')
    return c.to_image(pal)

def gen_buzulmagarasi():
    c = Canvas('1')
    pal = {
        '1': (195, 235, 255),   # Outside polar day
        '2': (115, 200, 235),   # Translucent ice arch
        '3': (40, 110, 175),    # Deep blue glacial ice
        '4': (245, 250, 255),   # Sharp ice stalactites
        '5': (40, 45, 60),      # Dark cavern rock base
        '6': (35, 170, 160),    # Turquoise frozen pool
        '7': (245, 95, 35),     # Explorer orange flare
    }
    # Arched glacial ice walls surrounding the view
    c.fill_polygon([(0, 0), (31, 0), (31, 10), (22, 7), (9, 7), (0, 10)], '3')
    c.fill_polygon([(0, 10), (6, 9), (4, 23), (0, 25)], '3')
    c.fill_polygon([(31, 10), (25, 9), (27, 23), (31, 25)], '3')
    # Middle ice vault
    c.fill_ellipse(16, 12, 10, 4, '2')
    # Sharp hanging white stalactites (4 >= 25 px)
    for sx, sh in [(8, 4), (13, 6), (18, 5), (23, 4)]:
        c.fill_polygon([(sx, 7), (sx + 2, 7), (sx + 1, 7 + sh)], '4')
    # Rocky cavern base
    c.fill_rect(0, 24, 31, 31, '5')
    # Frozen turquoise meltwater lake (6 >= 35 px)
    c.fill_ellipse(16, 27, 9, 3, '6')
    # Explorer orange flare beacon glowing on ice bank (7 >= 15 px)
    c.fill_circle(7, 26, 2, '7')
    return c.to_image(pal)

def gen_zenbahcesigolu():
    c = Canvas('1')
    pal = {
        '1': (215, 225, 230),   # Misty morning sky
        '2': (205, 45, 45),     # Red arched bridge
        '3': (115, 30, 30),     # Bridge shadow
        '4': (45, 120, 115),    # Koi pond water
        '5': (135, 140, 145),   # Stone lantern & pavers
        '6': (40, 105, 45),     # Bonsai pine foliage
        '7': (245, 140, 35),    # Golden orange koi fish
        '8': (245, 160, 185),   # Pink cherry blossoms
    }
    # Zen garden foliage framing sky
    c.fill_circle(4, 5, 4, '6')
    c.fill_circle(28, 4, 3, '6')
    # Tranquil koi pond
    c.fill_rect(0, 17, 31, 31, '4')
    # Graceful curved wooden vermilion bridge spanning the pond (2 & 3 >= 55 px)
    c.fill_ellipse(16, 18, 12, 5, '2')
    c.fill_ellipse(16, 19, 10, 4, '4')
    c.fill_ellipse(16, 20, 9, 3, '3') # Bridge under-arch shadow
    # Bridge vertical railings
    c.draw_line(8, 14, 8, 16, '2', 1)
    c.draw_line(16, 13, 16, 15, '2', 1)
    c.draw_line(24, 14, 24, 16, '2', 1)
    # Traditional stone toro lantern on bank (5 >= 20 px)
    c.fill_rect(26, 16, 29, 23, '5')
    c.fill_polygon([(25, 17), (27, 14), (30, 17)], '5')
    # Golden orange koi fish swimming in pond (7 >= 18 px)
    c.fill_ellipse(12, 25, 3, 1, '7')
    c.fill_ellipse(20, 28, 3, 1, '7')
    # Drifting pink cherry blossom petals (8 >= 16 px)
    for px, py in [(6, 21), (18, 23), (25, 27), (8, 28), (14, 29), (22, 21)]:
        c.set(px, py, '8')
        c.set(px + 1, py, '8')
    return c.to_image(pal)

def gen_denizaltidunyasi():
    c = Canvas('1')
    pal = {
        '1': (10, 18, 35),      # Abyssal midnight water
        '2': (50, 50, 60),      # Hydrothermal basalt chimney
        '3': (160, 80, 40),     # Mineral copper crust
        '4': (245, 205, 45),    # Yellow submarine hull
        '5': (65, 155, 195),    # Cockpit glass bubble
        '6': (255, 245, 165),   # Searchlight cone beam
        '7': (225, 75, 185),    # Bioluminescent pink jellyfish
        '8': (120, 125, 135),   # Mineral geothermal smoke
    }
    # Hydrothermal black smoker chimney on right
    c.fill_polygon([(22, 12), (28, 12), (30, 31), (20, 31)], '2')
    c.fill_polygon([(24, 12), (28, 12), (29, 24), (25, 24)], '3') # Mineral crust
    # Billowing mineral smoke plume rising from chimney (8 >= 25 px)
    c.fill_ellipse(25, 6, 4, 3, '8')
    c.fill_ellipse(23, 2, 3, 2, '8')
    # Bright searchlight cone illuminating abyssal trench (6 >= 35 px)
    c.fill_polygon([(12, 18), (24, 15), (26, 26)], '6')
    # Yellow exploration submarine (4 & 5 >= 45 px)
    c.fill_ellipse(8, 18, 5, 3, '4')
    c.fill_circle(11, 18, 2, '5') # Cockpit dome
    c.fill_polygon([(3, 18), (1, 16), (1, 20)], '4') # Propeller tail
    # Deep sea glowing pink jellyfish drifting in dark water (7 >= 20 px)
    c.fill_circle(16, 7, 2, '7')
    c.draw_line(15, 9, 15, 13, '7', 1)
    c.draw_line(17, 9, 17, 13, '7', 1)
    return c.to_image(pal)

GENERATORS = {
    # 10 Kolay (Full 32x32, 5 colors)
    'kolay/BuzulAyisi': gen_buzulayisi,
    'kolay/DenizFeneriAda': gen_denizfeneriada,
    'kolay/SualtiMercan': gen_sualtimercan,
    'kolay/KanyonKartali': gen_kanyonkartali,
    'kolay/KampAtesiGece': gen_kampatesigece,
    'kolay/SamurayTapinak': gen_samuraytapinak,
    'kolay/VolkanKrateri': gen_volkankrateri,
    'kolay/UcanBalonKapadokya': gen_ucanbalonkapadokya,
    'kolay/GeceDegirmeni': gen_gecedegirmeni,
    'kolay/TropikLagun': gen_tropiklagun,

    # 10 Orta (Full 32x32, 7-8 colors)
    'orta/DemiryoluKoprusu': gen_demiryolukoprusu,
    'orta/VenedikGondol': gen_venedikgondol,
    'orta/AntikPiramitGece': gen_antikpiramitgece,
    'orta/SiberYagmuru': gen_siberyagmuru,
    'orta/MantarKoyu': gen_mantarkoyu,
    'orta/KacakciLimani': gen_kacakcilimani,
    'orta/GokdelenCatisi': gen_gokdelencatisi,
    'orta/BuzulMagarasi': gen_buzulmagarasi,
    'orta/ZenBahcesiGolu': gen_zenbahcesigolu,
    'orta/DenizaltiDunyasi': gen_denizaltidunyasi,
}

def validate_image(name, im):
    assert im.size == (32, 32), f'{name}: invalid size {im.size}'
    px = im.load()
    color_counts = collections.defaultdict(int)
    for y in range(32):
        for x in range(32):
            r, g, b, a = px[x, y]
            assert a in (0, 255), f'{name}: invalid alpha {a} at ({x}, {y})'
            if a == 255:
                color_counts[(r, g, b)] += 1
                
    cubes = sum(color_counts.values())
    n_colors = len(color_counts)
    
    warns = []
    # Both Kolay and Orta must be 1024 cubes (etrafi kapali)
    if cubes != 1024:
        warns.append(f"Cubes is {cubes} != 1024 (not closed/full canvas)")
        
    for col, cnt in color_counts.items():
        if cnt < 10:
            warns.append(f"Color {col} has only {cnt} px (<10)")
            
    colors = list(color_counts.keys())
    for c1, c2 in itertools.combinations(colors, 2):
        diff = max(abs(c1[i] - c2[i]) for i in range(3))
        if diff < 14:
            warns.append(f"Colors close (<14): {c1} vs {c2}")
            
    return cubes, n_colors, warns

def main():
    print("Generating 20 batch 6 pixel arts (10 Kolay + 10 Orta, both etrafi kapali)...")
    all_ok = True
    for rel_path, gen_fn in GENERATORS.items():
        im = gen_fn()
        out_png = os.path.join('Assets', '04.11.2026', rel_path + '.png')
        os.makedirs(os.path.dirname(out_png), exist_ok=True)
        im.save(out_png)
        make_meta(out_png)
        
        cubes, n_colors, warns = validate_image(rel_path, im)
        status = "[OK  ]" if not warns else "[WARN]"
        print(f"{status} {rel_path:26} -> cubes={cubes:4}, colors={n_colors:2}")
        for w in warns:
            print(f"       WARN:  {w}")
            all_ok = False
            
    print(f"\nCompleted 20 generations. Overall OK: {all_ok}")

if __name__ == '__main__':
    main()

