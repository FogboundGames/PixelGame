"""Pixel art generator for PixelGame - Batch 7 (30 new levels: 10 Kolay, 10 Orta, 10 Zor).
All 30 levels are full 32x32 canvas (etrafi kapali / 1024 cubes).
- 10 Kolay: 5 colors, full 32x32 canvas.
- 10 Orta: 8 colors, full 32x32 canvas.
- 10 Zor: 10 colors, full 32x32 canvas.
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
# 10 KOLAY SEVIYE (FULL 32x32 CANVAS, 5 RENK)
# ==============================================================================

def gen_tamgunestutulmasi():
    c = Canvas('1')
    pal = {
        '1': (15, 18, 32),      # Dark eclipse twilight sky
        '2': (65, 70, 95),      # Horizon purple haze
        '3': (255, 240, 160),   # Solar corona diamond ring
        '4': (250, 250, 255),   # Corona bright flare
        '5': (6, 6, 12),        # Black lunar disk & dark hills
    }
    # Horizon haze at bottom
    c.fill_rect(0, 20, 31, 31, '2')
    # Dark silhouette hills
    c.fill_polygon([(0, 25), (14, 23), (31, 26), (31, 31), (0, 31)], '5')
    # Solar corona ring (3 >= 45 px)
    c.fill_circle(16, 11, 7, '3')
    # Bright diamond ring glint flare (4 >= 15 px)
    c.fill_circle(21, 6, 2, '4')
    c.set(21, 3, '4'); c.set(21, 9, '4')
    c.set(18, 6, '4'); c.set(24, 6, '4')
    # Black moon silhouette blocking the sun
    c.fill_circle(16, 11, 5, '5')
    return c.to_image(pal)

def gen_lavmagarasi():
    c = Canvas('1')
    pal = {
        '1': (35, 25, 30),      # Dark cavern vault
        '2': (85, 45, 40),      # Basalt stalactites
        '3': (245, 75, 25),     # Glowing lava river
        '4': (255, 215, 45),    # White-hot molten hotspots
        '5': (65, 215, 235),    # Luminous cyan cave crystals
    }
    # Basalt stalactites hanging from cavern roof (2 >= 60 px)
    c.fill_polygon([(2, 0), (7, 0), (4, 11)], '2')
    c.fill_polygon([(10, 0), (16, 0), (13, 14)], '2')
    c.fill_polygon([(23, 0), (29, 0), (26, 10)], '2')
    # Cavern rock bank
    c.fill_rect(0, 19, 31, 31, '1')
    # Glowing river of molten lava (3 >= 55 px)
    c.fill_polygon([(0, 24), (31, 22), (31, 28), (0, 30)], '3')
    # White-hot magma hotspots (4 >= 16 px)
    c.fill_ellipse(12, 26, 4, 1, '4')
    c.fill_ellipse(24, 25, 3, 1, '4')
    # Luminous cyan crystal clusters on wall (5 >= 20 px)
    c.fill_polygon([(1, 14), (5, 14), (3, 9)], '5')
    c.fill_polygon([(28, 15), (31, 15), (30, 11)], '5')
    return c.to_image(pal)

def gen_kutupgeyigi():
    c = Canvas('1')
    pal = {
        '1': (20, 25, 48),      # Midnight polar sky
        '2': (240, 245, 255),   # Giant silver full moon
        '3': (75, 185, 145),    # Emerald aurora curtain
        '4': (140, 185, 215),   # Snowy cliff ridge
        '5': (35, 30, 42),      # Reindeer silhouette
    }
    # Giant full moon
    c.fill_circle(14, 10, 7, '2')
    # Aurora green ribbon streaming across upper sky
    c.fill_polygon([(0, 2), (18, 5), (31, 1), (31, 4), (18, 7), (0, 4)], '3')
    # Snowy mountain ridge plateau
    c.fill_polygon([(0, 22), (19, 18), (31, 21), (31, 31), (0, 31)], '4')
    # Majestic reindeer standing on ridge looking at moon (5 >= 40 px)
    # Body
    c.fill_ellipse(17, 16, 4, 2, '5')
    # Neck & head
    c.fill_polygon([(19, 16), (22, 11), (20, 11), (18, 16)], '5')
    c.fill_circle(21, 11, 2, '5')
    # Sprawling antlers
    c.draw_line(22, 10, 25, 6, '5', 1)
    c.draw_line(24, 7, 26, 8, '5', 1)
    c.draw_line(21, 9, 19, 6, '5', 1)
    # Legs
    c.fill_rect(14, 18, 15, 22, '5')
    c.fill_rect(19, 18, 20, 22, '5')
    return c.to_image(pal)

def gen_sihirlimantarormani():
    c = Canvas('1')
    pal = {
        '1': (25, 20, 45),      # Enchanted night sky
        '2': (45, 95, 55),      # Mossy forest bank
        '3': (235, 45, 105),    # Glowing mushroom cap
        '4': (245, 235, 220),   # Mushroom stalk & spots
        '5': (255, 225, 65),    # Firefly light orbs
    }
    # Mossy ground bank
    c.fill_rect(0, 20, 31, 31, '2')
    # Giant glowing mushroom cap (3 >= 60 px)
    c.fill_ellipse(16, 14, 10, 5, '3')
    # White dots on mushroom cap (4 >= 25 px)
    c.fill_circle(11, 12, 1, '4')
    c.fill_circle(16, 11, 1, '4')
    c.fill_circle(21, 12, 1, '4')
    # Mushroom white stalk
    c.fill_rect(14, 18, 18, 25, '4')
    # Golden fireflies floating in the dark woods (5 >= 16 px)
    for fx, fy in [(5, 8), (25, 7), (8, 16), (24, 17), (16, 5), (10, 24)]:
        c.fill_circle(fx, fy, 1, '5')
    return c.to_image(pal)

def gen_kapadokyavadi():
    c = Canvas('1')
    pal = {
        '1': (245, 175, 110),   # Dawn sky
        '2': (255, 225, 140),   # Rising golden sun
        '3': (185, 130, 85),    # Fairy chimney rock cones
        '4': (110, 70, 50),     # Valley canyon shadows
        '5': (215, 45, 55),     # Hot air balloon
    }
    # Rising sun on horizon
    c.fill_circle(16, 15, 7, '2')
    # Tall fairy chimney rock cones
    c.fill_polygon([(2, 31), (7, 14), (12, 31)], '3')
    c.fill_polygon([(10, 31), (16, 17), (21, 31)], '3')
    c.fill_polygon([(21, 31), (26, 12), (31, 31)], '3')
    # Valley shadows at base
    c.fill_rect(0, 27, 31, 31, '4')
    # Hot air balloon floating high in sky (5 >= 25 px)
    c.fill_circle(8, 7, 3, '5')
    c.fill_polygon([(5, 8), (11, 8), (9, 12), (7, 12)], '5')
    c.set(8, 13, '4') # Basket
    return c.to_image(pal)

def gen_mercanresifigece():
    c = Canvas('1')
    pal = {
        '1': (12, 25, 55),      # Midnight deep ocean
        '2': (45, 145, 175),    # Bioluminescent water glow
        '3': (235, 65, 140),    # Vibrant neon coral
        '4': (250, 210, 50),    # Glowing anemone tips
        '5': (60, 225, 185),    # Luminous jellyfish bell
    }
    # Glowing ambient water layer
    c.fill_ellipse(16, 26, 15, 5, '2')
    # Neon pink coral reef branches at seabed (3 >= 45 px)
    c.fill_rect(2, 20, 7, 31, '3')
    c.fill_circle(4, 18, 3, '3')
    c.fill_rect(24, 21, 29, 31, '3')
    c.fill_circle(27, 19, 3, '3')
    # Golden anemone tentacles (4 >= 25 px)
    for ax in [10, 13, 16, 19, 22]:
        c.draw_line(ax, 31, ax, 25, '4', 1)
        c.set(ax, 24, '4')
    # Luminous green jellyfish swimming in center (5 >= 30 px)
    c.fill_circle(16, 8, 4, '5')
    c.draw_line(14, 12, 13, 18, '5', 1)
    c.draw_line(16, 12, 16, 19, '5', 1)
    c.draw_line(18, 12, 19, 18, '5', 1)
    return c.to_image(pal)

def gen_ucurumbonsaisi():
    c = Canvas('1')
    pal = {
        '1': (220, 195, 175),   # Dawn mist sky
        '2': (235, 60, 55),     # Crimson sunrise sun
        '3': (85, 80, 95),      # Craggy granite cliff
        '4': (135, 85, 50),     # Gnarled bonsai trunk
        '5': (45, 125, 65),     # Pine needle pads
    }
    # Crimson morning sun
    c.fill_circle(8, 9, 6, '2')
    # Granite cliff on right
    c.fill_polygon([(19, 17), (31, 14), (31, 31), (15, 31)], '3')
    # Gnarled bonsai wood trunk arching over abyss (4 >= 25 px)
    c.draw_line(23, 21, 15, 15, '4', 2)
    c.draw_line(15, 15, 7, 14, '4', 2)
    c.draw_line(15, 15, 12, 9, '4', 2)
    # Lush layered pine needle cloud pads (5 >= 50 px)
    c.fill_ellipse(7, 13, 4, 2, '5')
    c.fill_ellipse(12, 8, 4, 2, '5')
    c.fill_ellipse(18, 11, 4, 2, '5')
    return c.to_image(pal)

def gen_samuraykalesi():
    c = Canvas('1')
    pal = {
        '1': (40, 30, 55),      # Twilight purple sky
        '2': (240, 85, 105),    # Cherry blossom moon
        '3': (175, 185, 195),   # White castle walls
        '4': (20, 20, 30),      # Curved roof tiles
        '5': (235, 175, 45),    # Golden tier lanterns
    }
    # Giant pink spring moon
    c.fill_circle(21, 8, 7, '2')
    # Castle white tiered keep (3 >= 60 px)
    c.fill_rect(7, 15, 23, 29, '3')
    # Dark curved traditional Japanese roofs (4 >= 70 px)
    # Upper roof
    c.fill_polygon([(10, 12), (20, 12), (23, 15), (7, 15)], '4')
    c.fill_polygon([(12, 9), (18, 9), (15, 6)], '4')
    # Lower roof
    c.fill_polygon([(4, 21), (26, 21), (28, 24), (2, 24)], '4')
    # Stone castle base
    c.fill_rect(0, 28, 31, 31, '4')
    # Glowing golden lantern windows (5 >= 16 px)
    c.fill_rect(10, 17, 12, 19, '5')
    c.fill_rect(18, 17, 20, 19, '5')
    c.fill_rect(14, 25, 16, 27, '5')
    return c.to_image(pal)

def gen_gokyuzyadasi():
    c = Canvas('1')
    pal = {
        '1': (115, 185, 245),   # Azure celestial sky
        '2': (245, 250, 255),   # Fluffy white clouds
        '3': (165, 125, 85),    # Floating earth rock
        '4': (55, 155, 65),     # Island meadow grass
        '5': (65, 215, 240),    # Cascading waterfall
    }
    # Soft clouds below island
    c.fill_ellipse(7, 28, 8, 3, '2')
    c.fill_ellipse(24, 29, 9, 3, '2')
    # Floating celestial island inverted cone rock (3 >= 60 px)
    c.fill_polygon([(5, 13), (27, 13), (16, 26)], '3')
    # Lush grassy plateau surface
    c.fill_ellipse(16, 13, 11, 3, '4')
    # Waterfall cascading from island edge into the clouds (5 >= 25 px)
    c.draw_line(19, 13, 19, 28, '5', 2)
    return c.to_image(pal)

def gen_antikliman():
    c = Canvas('1')
    pal = {
        '1': (245, 155, 85),    # Sunset amber glow
        '2': (165, 65, 75),     # Upper dusk sky
        '3': (45, 95, 125),     # Mediterranean sea
        '4': (235, 225, 205),   # Greek marble temple
        '5': (45, 35, 45),      # Trireme ship silhouette
    }
    # Upper dusk sky
    c.fill_rect(0, 0, 31, 6, '2')
    # Calm Mediterranean sea
    c.fill_rect(0, 17, 31, 31, '3')
    # Greek marble colonnade temple on left cape (4 >= 55 px)
    c.fill_polygon([(0, 12), (12, 12), (14, 24), (0, 24)], '4')
    c.fill_polygon([(0, 12), (6, 7), (12, 12)], '4') # Pediment
    # Ancient trireme galley silhouette sailing at sunset (5 >= 35 px)
    c.fill_polygon([(17, 22), (29, 22), (27, 25), (19, 25)], '5')
    c.draw_line(23, 14, 23, 22, '5', 1) # Mast
    c.fill_polygon([(23, 15), (28, 17), (23, 19)], '5') # Sail
    return c.to_image(pal)

# ==============================================================================
# 10 ORTA SEVIYE (FULL 32x32 CANVAS, 8 RENK)
# ==============================================================================

def gen_antikkutuphane():
    c = Canvas('1')
    pal = {
        '1': (35, 25, 20),      # Dark mahogany room
        '2': (145, 90, 50),     # Carved bookshelf wood
        '3': (195, 55, 45),     # Red leather book spines
        '4': (65, 125, 85),     # Green leather books
        '5': (245, 215, 140),   # Parchment book pages
        '6': (255, 225, 65),    # Banker lamp warm light
        '7': (85, 55, 35),      # Arched alcove shadow
        '8': (215, 175, 95),    # Rolling brass ladder
    }
    # Arch alcove
    c.fill_ellipse(16, 12, 13, 9, '7')
    # Left and right tall bookshelves (2 >= 80 px)
    c.fill_rect(0, 4, 10, 28, '2')
    c.fill_rect(22, 4, 31, 28, '2')
    # Leather bound books on shelves
    for sy in [8, 14, 20]:
        c.fill_rect(2, sy, 8, sy + 3, '3')
        c.fill_rect(24, sy, 30, sy + 3, '4')
    # Grand mahogany study desk in center
    c.fill_rect(9, 22, 23, 27, '2')
    # Open parchment tome on desk (5 >= 15 px)
    c.fill_rect(14, 21, 18, 23, '5')
    # Glowing banker desk lamp (6 >= 18 px)
    c.fill_circle(12, 19, 2, '6')
    # Rolling brass library ladder on right (8 >= 20 px)
    c.draw_line(20, 6, 23, 28, '8', 1)
    c.draw_line(23, 6, 26, 28, '8', 1)
    for ly in [10, 15, 20, 25]:
        c.draw_line(21, ly, 25, ly, '8', 1)
    return c.to_image(pal)

def gen_korsanhazinemagarasi():
    c = Canvas('1')
    pal = {
        '1': (20, 22, 35),      # Cavern shadow
        '2': (85, 75, 80),      # Cave limestone wall
        '3': (45, 125, 145),    # Ocean view through mouth
        '4': (155, 95, 45),     # Wooden treasure chest
        '5': (250, 215, 50),    # Overflowing gold coins
        '6': (225, 45, 65),     # Ruby gems
        '7': (65, 225, 165),    # Emerald gems
        '8': (240, 245, 250),   # Pirate skull
    }
    # Cave mouth opening revealing turquoise ocean
    c.fill_ellipse(16, 10, 11, 7, '3')
    # Cavern framing rock walls
    c.fill_polygon([(0, 0), (31, 0), (31, 15), (26, 10), (5, 10), (0, 15)], '2')
    c.fill_rect(0, 17, 31, 31, '1')
    # Open treasure chest in center (4 >= 40 px)
    c.fill_rect(10, 19, 22, 26, '4')
    c.fill_polygon([(10, 19), (22, 19), (24, 15), (8, 15)], '4') # Open lid
    # Heap of glittering gold coins (5 >= 45 px)
    c.fill_ellipse(16, 20, 5, 2, '5')
    c.fill_polygon([(5, 27), (27, 27), (24, 31), (8, 31)], '5')
    # Ruby & emerald gems scattered (6 & 7 >= 14 px)
    for rx, ry in [(9, 25), (14, 21), (20, 28)]:
        c.fill_circle(rx, ry, 1, '6')
    for ex, ey in [(18, 21), (12, 28), (23, 25)]:
        c.fill_circle(ex, ey, 1, '7')
    # Pirate skull resting on rocks (8 >= 16 px)
    c.fill_circle(6, 22, 2, '8')
    c.set(6, 24, '8')
    return c.to_image(pal)

def gen_buharligemi():
    c = Canvas('1')
    pal = {
        '1': (245, 165, 95),    # Sunset amber sky
        '2': (175, 75, 65),     # Sunset cloud rim
        '3': (45, 85, 95),      # Mississippi river water
        '4': (240, 245, 250),   # Steamboat white hull & decks
        '5': (195, 45, 40),     # Red paddle wheel box
        '6': (35, 35, 45),      # Twin black funnels
        '7': (175, 180, 195),   # Puffy billowy steam
        '8': (255, 225, 75),    # Warm cabin lights
    }
    # Sunset cloud band
    c.fill_polygon([(0, 6), (31, 4), (31, 9), (0, 11)], '2')
    # River water
    c.fill_rect(0, 19, 31, 31, '3')
    # Multi-deck white paddle steamer (4 >= 65 px)
    c.fill_rect(5, 17, 26, 22, '4') # Lower hull
    c.fill_rect(7, 13, 23, 17, '4') # Middle saloon deck
    c.fill_rect(10, 10, 18, 13, '4') # Pilot house
    # Red stern paddle wheel housing (5 >= 25 px)
    c.fill_circle(4, 19, 3, '5')
    # Twin black smoke funnels (6 >= 20 px)
    c.fill_rect(12, 4, 13, 10, '6')
    c.fill_rect(16, 4, 17, 10, '6')
    # Billowy white steam drifting behind (7 >= 25 px)
    c.fill_circle(9, 3, 2, '7')
    c.fill_circle(6, 2, 3, '7')
    # Lit cabin windows (8 >= 16 px)
    for wx in [7, 10, 13, 16, 19, 22]:
        c.set(wx, 15, '8'); c.set(wx + 1, 15, '8')
    return c.to_image(pal)

def gen_siberramenbari():
    c = Canvas('1')
    pal = {
        '1': (18, 20, 32),      # Dark rain alley
        '2': (65, 55, 75),      # Concrete alley walls
        '3': (245, 45, 120),    # Neon pink ramen kanji
        '4': (45, 215, 235),    # Cyan neon bar trim
        '5': (235, 60, 45),     # Red paper lanterns
        '6': (255, 225, 75),    # Warm lantern glow
        '7': (155, 105, 65),    # Wooden bar counter
        '8': (240, 245, 250),   # Steaming noodle bowl
    }
    # Concrete wall perspective
    c.fill_polygon([(0, 0), (10, 3), (10, 24), (0, 27)], '2')
    c.fill_polygon([(31, 0), (22, 3), (22, 24), (31, 27)], '2')
    # Neon pink sign at top (3 >= 25 px)
    c.fill_rect(11, 4, 21, 9, '3')
    # Cyan neon roof awning trim (4 >= 20 px)
    c.draw_line(4, 11, 28, 11, '4', 1)
    # Hanging red lanterns (5 & 6 >= 25 px)
    c.fill_circle(7, 14, 2, '5')
    c.fill_circle(7, 14, 1, '6')
    c.fill_circle(16, 14, 2, '5')
    c.fill_circle(16, 14, 1, '6')
    c.fill_circle(25, 14, 2, '5')
    c.fill_circle(25, 14, 1, '6')
    # Wooden ramen stall counter (7 >= 45 px)
    c.fill_rect(5, 19, 27, 24, '7')
    # Steaming porcelain bowl of ramen noodles (8 >= 18 px)
    c.fill_ellipse(16, 18, 3, 2, '8')
    c.draw_line(15, 15, 15, 13, '8', 1) # Steam trail
    c.draw_line(17, 15, 17, 13, '8', 1)
    return c.to_image(pal)

def gen_ortacagdemircisi():
    c = Canvas('1')
    pal = {
        '1': (30, 25, 28),      # Stone forge chamber
        '2': (85, 75, 80),      # Cobblestone wall
        '3': (55, 60, 75),      # Steel anvil
        '4': (245, 85, 25),     # Roaring coal fire
        '5': (255, 225, 65),    # White-hot glowing blade
        '6': (215, 155, 85),    # Wood tool rack
        '7': (145, 155, 175),   # Finished iron armor/helm
        '8': (250, 190, 45),    # Flying glowing sparks
    }
    # Cobblestone chimney arch on right
    c.fill_polygon([(19, 2), (31, 2), (31, 24), (19, 24)], '2')
    # Roaring furnace hearth (4 >= 35 px)
    c.fill_ellipse(25, 16, 4, 3, '4')
    # Heavy steel anvil in center (3 >= 40 px)
    c.fill_rect(9, 18, 17, 24, '3')
    c.fill_polygon([(6, 18), (9, 18), (9, 21)], '3') # Anvil horn
    c.fill_rect(10, 24, 16, 28, '1') # Anvil tree stump
    # White-hot forged sword blade across anvil (5 >= 16 px)
    c.draw_line(6, 17, 18, 17, '5', 1)
    # Weapon rack on left (6 >= 25 px)
    c.fill_rect(2, 8, 4, 25, '6')
    # Hanging iron knight helmet (7 >= 20 px)
    c.fill_circle(3, 11, 2, '7')
    # Flying hammer sparks (8 >= 14 px)
    for sx, sy in [(11, 14), (13, 12), (16, 13), (18, 11), (14, 10)]:
        c.set(sx, sy, '8')
        c.set(sx + 1, sy, '8')
    return c.to_image(pal)

def gen_colvahasigece():
    c = Canvas('1')
    pal = {
        '1': (15, 22, 45),      # Starry desert night
        '2': (250, 235, 145),   # Moon & stars
        '3': (185, 135, 80),    # Sand dunes
        '4': (115, 75, 45),     # Sand shadows
        '5': (35, 115, 135),    # Oasis clear spring
        '6': (45, 135, 60),     # Date palm fronds
        '7': (215, 65, 55),     # Bedouin carpet tent
        '8': (250, 185, 45),    # Campfire lantern amber
    }
    # Crescent moon & stars (2 >= 18 px)
    c.fill_circle(25, 6, 3, '2')
    c.fill_circle(26, 5, 3, '1')
    for sx, sy in [(5, 4), (14, 5), (9, 9)]:
        c.set(sx, sy, '2')
        c.set(sx + 1, sy, '2')
    # Sweeping desert sand dunes
    c.fill_polygon([(0, 14), (18, 11), (31, 16), (31, 31), (0, 31)], '3')
    c.fill_polygon([(0, 20), (18, 17), (31, 22), (31, 31), (0, 31)], '4')
    # Oasis crystal spring pool (5 >= 35 px)
    c.fill_ellipse(15, 24, 7, 3, '5')
    # Lush date palms flanking pool (6 >= 35 px)
    c.fill_circle(7, 15, 4, '6')
    c.fill_circle(23, 16, 4, '6')
    # Striped Bedouin desert tent (7 >= 25 px)
    c.fill_polygon([(2, 23), (8, 18), (11, 23)], '7')
    # Glowing campfire by the water (8 >= 14 px)
    c.fill_circle(14, 21, 2, '8')
    return c.to_image(pal)

def gen_uzayistasyonuyorunge():
    c = Canvas('1')
    pal = {
        '1': (10, 12, 25),      # Void of space
        '2': (45, 115, 215),    # Earth blue ocean curve
        '3': (240, 250, 255),   # Earth white clouds
        '4': (75, 175, 105),    # Earth continent green
        '5': (195, 205, 220),   # Station white module
        '6': (75, 85, 105),     # Station truss shadow
        '7': (245, 185, 45),    # Golden solar arrays
        '8': (65, 215, 240),    # Docking cyan beacon
    }
    # Earth planetary horizon curve across lower screen
    c.fill_ellipse(16, 34, 25, 14, '2')
    # Continents and swirl clouds on Earth (3 & 4 >= 45 px)
    c.fill_ellipse(10, 26, 6, 2, '4')
    c.fill_ellipse(20, 28, 5, 2, '3')
    c.fill_ellipse(12, 23, 8, 2, '3')
    # Space station truss spine (6 >= 25 px)
    c.draw_line(4, 12, 28, 12, '6', 2)
    # Cylindrical habitat modules (5 >= 40 px)
    c.fill_rect(13, 8, 19, 16, '5')
    c.fill_circle(16, 12, 4, '5')
    # Golden solar panel wings left and right (7 >= 50 px)
    c.fill_rect(5, 7, 9, 17, '7')
    c.fill_rect(23, 7, 27, 17, '7')
    # Cyan communication & docking port beacon (8 >= 15 px)
    c.fill_circle(16, 7, 2, '8')
    return c.to_image(pal)

def gen_alpdagevi():
    c = Canvas('1')
    pal = {
        '1': (35, 45, 75),      # Alpine winter dusk
        '2': (215, 235, 250),   # Snowdrifts on roof & ground
        '3': (85, 135, 175),    # Snow shadows
        '4': (135, 80, 50),     # Log cabin timber walls
        '5': (65, 40, 25),      # Timber shadows
        '6': (255, 225, 85),    # Cozy lit windows
        '7': (245, 65, 45),     # Chimney smoke red glow
        '8': (35, 85, 55),      # Evergreen pine tree
    }
    # Mountain slope
    c.fill_polygon([(0, 12), (31, 16), (31, 31), (0, 31)], '3')
    c.fill_polygon([(0, 16), (31, 20), (31, 31), (0, 31)], '2')
    # Pine tree on left (8 >= 35 px)
    c.fill_polygon([(2, 23), (6, 9), (10, 23)], '8')
    # Log chalet cabin in center (4 & 5 >= 50 px)
    c.fill_rect(11, 16, 25, 26, '4')
    c.fill_rect(20, 16, 25, 26, '5') # Shadow side
    # Snow-covered pitched gable roof (2 >= 45 px)
    c.fill_polygon([(8, 16), (18, 10), (28, 16)], '2')
    # Stone chimney with glowing sparks (7 >= 16 px)
    c.fill_rect(21, 8, 23, 12, '5')
    c.fill_circle(22, 7, 1, '7')
    c.fill_circle(23, 5, 1, '7')
    # Warm glowing window panes (6 >= 16 px)
    c.fill_rect(13, 19, 16, 22, '6')
    return c.to_image(pal)

def gen_kristalmagaragolu():
    c = Canvas('1')
    pal = {
        '1': (25, 20, 35),      # Dark cavern vault
        '2': (75, 60, 85),      # Limestone stalactites
        '3': (175, 65, 205),    # Purple amethyst crystals
        '4': (235, 145, 255),   # Amethyst glints
        '5': (35, 155, 165),    # Subterranean turquoise lake
        '6': (75, 225, 215),    # Water glowing ripples
        '7': (95, 65, 45),      # Cave floor gravel
        '8': (255, 230, 95),    # Miner lantern on rock
    }
    # Limestone stalactites hanging from ceiling (2 >= 50 px)
    c.fill_polygon([(3, 0), (9, 0), (6, 12)], '2')
    c.fill_polygon([(22, 0), (28, 0), (25, 13)], '2')
    # Subterranean crystal lake
    c.fill_rect(0, 18, 31, 28, '5')
    # Luminous water ripples (6 >= 25 px)
    c.draw_line(5, 21, 26, 21, '6', 1)
    c.draw_line(8, 25, 23, 25, '6', 1)
    # Gravel shore foreground
    c.fill_rect(0, 27, 31, 31, '7')
    # Amethyst crystal clusters on stalactites (3 & 4 >= 40 px)
    c.fill_polygon([(5, 10), (9, 10), (7, 16)], '3')
    c.fill_polygon([(23, 11), (27, 11), (25, 17)], '3')
    c.draw_line(7, 11, 7, 14, '4', 1)
    c.draw_line(25, 12, 25, 15, '4', 1)
    # Explorer lantern on rock bank (8 >= 16 px)
    c.fill_circle(16, 27, 2, '8')
    return c.to_image(pal)

def gen_gelenekselcaybahcesi():
    c = Canvas('1')
    pal = {
        '1': (215, 225, 230),   # Morning garden mist
        '2': (165, 115, 75),    # Tea house cedar timber
        '3': (245, 245, 240),   # Shoji paper screens
        '4': (55, 45, 50),      # Dark tile roof eaves
        '5': (55, 135, 65),     # Moss & bamboo grove
        '6': (135, 140, 145),   # Stone basin & pavers
        '7': (45, 115, 110),    # Water basin pool
        '8': (235, 75, 65),     # Red tea umbrella / bench
    }
    # Bamboo foliage background
    c.fill_polygon([(0, 4), (12, 0), (20, 6), (31, 2), (31, 15), (0, 15)], '5')
    # Garden moss ground
    c.fill_rect(0, 16, 31, 31, '5')
    # Traditional Japanese tea house structure (2, 3, 4 >= 80 px)
    c.fill_rect(8, 12, 26, 24, '2')
    # White translucent shoji screens
    c.fill_rect(10, 14, 15, 21, '3')
    c.fill_rect(18, 14, 23, 21, '3')
    # Curved tile roof overhang
    c.fill_polygon([(5, 12), (29, 12), (27, 9), (7, 9)], '4')
    # Stone tsukubai water basin on left (6 & 7 >= 25 px)
    c.fill_circle(4, 24, 3, '6')
    c.fill_circle(4, 24, 2, '7')
    # Red ceremonial tea umbrella on right (8 >= 20 px)
    c.fill_polygon([(24, 20), (30, 20), (27, 16)], '8')
    c.draw_line(27, 20, 27, 26, '2', 1)
    return c.to_image(pal)

# ==============================================================================
# 10 ZOR SEVIYE (FULL 32x32 CANVAS, 10 RENK)
# ==============================================================================

def gen_atlantisharabeleri():
    c = Canvas('1')
    pal = {
        '1': (10, 35, 75),      # Deep ocean blue
        '2': (35, 105, 145),    # Sunbeams in water
        '3': (215, 225, 230),   # Sunken marble columns
        '4': (105, 125, 135),   # Column shadows & cracks
        '5': (45, 145, 95),     # Sea kelp ribbons
        '6': (225, 95, 65),     # Giant red octopus
        '7': (255, 225, 75),    # Golden glowing trident
        '8': (65, 225, 215),    # Ancient magical portal
        '9': (185, 145, 95),    # Seabed sand
        'A': (245, 250, 255),   # Rising bubbles & pearls
    }
    # Sunbeams filtering down through water (2 >= 50 px)
    c.fill_polygon([(4, 0), (10, 0), (18, 20), (12, 20)], '2')
    c.fill_polygon([(20, 0), (26, 0), (31, 16), (27, 16)], '2')
    # Sandy seabed
    c.fill_rect(0, 24, 31, 31, '9')
    # Sunken Greek temple colonnade (3 & 4 >= 65 px)
    c.fill_rect(5, 8, 8, 24, '3')
    c.fill_rect(7, 8, 8, 24, '4')
    c.fill_rect(22, 11, 25, 24, '3')
    c.fill_rect(24, 11, 25, 24, '4')
    c.fill_rect(3, 6, 27, 8, '3') # Architrave beam
    # Glowing magical portal in archway (8 >= 30 px)
    c.fill_circle(15, 17, 4, '8')
    # Golden Poseidon trident in center (7 >= 16 px)
    c.draw_line(15, 13, 15, 25, '7', 1)
    c.draw_line(13, 13, 17, 13, '7', 1)
    # Giant octopus tentacle curling around column (6 >= 35 px)
    c.draw_line(2, 26, 6, 21, '6', 2)
    c.draw_line(6, 21, 4, 16, '6', 2)
    # Sea kelp waving from sand (5 >= 25 px)
    c.draw_line(28, 28, 27, 18, '5', 2)
    # Water bubbles floating to surface (A >= 15 px)
    for bx, by in [(11, 6), (17, 4), (20, 8), (14, 10)]:
        c.fill_circle(bx, by, 1, 'A')
    return c.to_image(pal)

def gen_sibergokdelenkanyonu():
    c = Canvas('1')
    pal = {
        '1': (15, 15, 28),      # Dark smog night sky
        '2': (55, 50, 70),      # Megatower skyscrapers
        '3': (30, 28, 42),      # Skyscraper deep shadows
        '4': (245, 45, 125),    # Huge pink hologram kanji
        '5': (45, 215, 240),    # Cyan transit track
        '6': (255, 225, 65),    # Matrix window lights
        '7': (235, 45, 45),     # Flying vehicle red
        '8': (95, 105, 125),    # Sky-bridge overpass
        '9': (165, 75, 225),    # Purple ion exhaust
        'A': (245, 245, 210),   # Headlight speed streaks
    }
    # Twin monolithic skyscrapers flanking screen (2 & 3 >= 120 px)
    c.fill_rect(0, 2, 10, 27, '2')
    c.fill_rect(7, 2, 10, 27, '3')
    c.fill_rect(21, 1, 31, 27, '2')
    c.fill_rect(28, 1, 31, 27, '3')
    # Lit matrix office windows (6 >= 35 px)
    for wy in [5, 9, 13, 17, 21]:
        c.fill_rect(2, wy, 4, wy + 1, '6')
        c.fill_rect(23, wy, 25, wy + 1, '6')
    # Holographic giant pink kanji ad in sky (4 >= 25 px)
    c.fill_rect(12, 4, 19, 11, '4')
    # Connecting skybridge highway (8 >= 25 px)
    c.fill_rect(0, 16, 31, 18, '8')
    # Flying spinner car in transit (7, 9, A >= 35 px)
    c.fill_rect(13, 13, 18, 15, '7')
    c.fill_circle(11, 14, 2, '9') # Purple ion plume
    c.draw_line(18, 14, 24, 14, 'A', 1) # Headlight beam
    # Lower cyan transit highway (5 >= 25 px)
    c.fill_rect(0, 26, 31, 27, '5')
    c.fill_rect(0, 28, 31, 31, '3')
    return c.to_image(pal)

def gen_kuzeykalesiaurora():
    c = Canvas('1')
    pal = {
        '1': (15, 22, 48),      # Arctic night sky
        '2': (65, 215, 145),    # Emerald green aurora arch
        '3': (175, 75, 215),    # Violet aurora curtain
        '4': (220, 240, 255),   # Fjord snow & glacier
        '5': (85, 125, 165),    # Glacier blue shadows
        '6': (125, 120, 130),   # Stone fortress towers
        '7': (55, 50, 60),      # Battlements shadow
        '8': (255, 215, 55),    # Torchlight beacons
        '9': (20, 55, 80),      # Icy fjord deep water
        'A': (205, 40, 45),     # Red banners on towers
    }
    # Double aurora curtains: green and violet (2 & 3 >= 70 px)
    c.fill_polygon([(0, 4), (16, 1), (31, 3), (31, 7), (16, 4), (0, 7)], '2')
    c.fill_polygon([(0, 7), (16, 4), (31, 7), (31, 10), (16, 7), (0, 10)], '3')
    # Snowy fjord cliff
    c.fill_polygon([(0, 18), (31, 15), (31, 26), (0, 26)], '4')
    c.fill_polygon([(0, 22), (31, 19), (31, 26), (0, 26)], '5')
    # Icy deep water fjord at bottom
    c.fill_rect(0, 26, 31, 31, '9')
    # Stone fortress citadel on cliff edge (6 & 7 >= 60 px)
    c.fill_rect(8, 12, 23, 21, '6')
    c.fill_rect(17, 12, 23, 21, '7')
    # Watchtowers with crenellations
    c.fill_rect(7, 9, 10, 14, '6')
    c.fill_rect(21, 9, 24, 14, '7')
    # Blazing golden torch beacons (8 >= 16 px)
    c.fill_circle(8, 8, 2, '8')
    c.fill_circle(22, 8, 2, '8')
    # Crimson heraldic banners (A >= 15 px)
    c.draw_line(10, 11, 10, 16, 'A', 1)
    c.draw_line(24, 11, 24, 16, 'A', 1)
    return c.to_image(pal)

def gen_mayapiramidigunesi():
    c = Canvas('1')
    pal = {
        '1': (245, 185, 115),   # Equinox amber morning
        '2': (255, 235, 135),   # Radiant golden sun
        '3': (195, 175, 135),   # Limestone step pyramid
        '4': (115, 95, 65),     # Pyramid staircase shadow
        '5': (45, 125, 55),     # Dense jungle canopy
        '6': (25, 75, 35),      # Jungle deep shade
        '7': (215, 45, 45),     # Exotic red macaw parrot
        '8': (65, 175, 215),    # Sacred cenote water
        '9': (235, 115, 45),    # Feathered serpent stone
        'A': (155, 195, 215),   # Morning river mist
    }
    # Radiant equinox sun
    c.fill_circle(16, 7, 6, '2')
    # Stepped Mayan pyramid tiers (3 & 4 >= 85 px)
    c.fill_polygon([(12, 8), (20, 8), (24, 21), (8, 21)], '3')
    c.fill_polygon([(16, 8), (20, 8), (24, 21), (18, 21)], '4')
    # Temple summit shrine
    c.fill_rect(13, 6, 19, 8, '3')
    # Feathered serpent carved balustrade head (9 >= 15 px)
    c.fill_rect(16, 18, 19, 21, '9')
    # Dense rainforest jungle canopy flanking pyramid (5 & 6 >= 80 px)
    c.fill_polygon([(0, 15), (7, 15), (9, 28), (0, 28)], '5')
    c.fill_polygon([(24, 15), (31, 15), (31, 28), (22, 28)], '5')
    c.fill_rect(0, 20, 8, 28, '6')
    c.fill_rect(23, 20, 31, 28, '6')
    # Sacred turquoise cenote pool at bottom (8 >= 35 px)
    c.fill_ellipse(16, 28, 9, 3, '8')
    # Morning mist rising (A >= 20 px)
    c.draw_line(2, 26, 30, 26, 'A', 1)
    # Red scarlet macaw in flight over jungle (7 >= 14 px)
    c.fill_ellipse(6, 12, 3, 1, '7')
    c.set(6, 11, '7'); c.set(6, 13, '7')
    return c.to_image(pal)

def gen_steampunkfabrikasi():
    c = Canvas('1')
    pal = {
        '1': (35, 28, 30),      # Brick factory hall
        '2': (105, 65, 45),     # Industrial red brickwork
        '3': (215, 165, 65),    # Giant brass gear wheel
        '4': (145, 105, 40),    # Brass gear shadow & teeth
        '5': (155, 165, 180),   # Steel steam pipes
        '6': (245, 75, 25),     # Blast furnace orange glow
        '7': (255, 220, 65),    # Molten brass vat
        '8': (210, 215, 225),   # Escaping steam plume
        '9': (65, 65, 75),      # Iron catwalk railing
        'A': (75, 195, 175),    # Green pressure gauge
    }
    # Red brick arches on sides (2 >= 60 px)
    c.fill_rect(0, 2, 8, 28, '2')
    c.fill_rect(24, 2, 31, 28, '2')
    # Giant rotating brass clockwork gear in center (3 & 4 >= 85 px)
    c.fill_circle(16, 13, 8, '3')
    c.fill_circle(16, 13, 5, '4')
    c.fill_circle(16, 13, 2, '3')
    # Steel high pressure steam pipes (5 >= 35 px)
    c.draw_line(3, 8, 29, 8, '5', 2)
    c.draw_line(16, 8, 16, 26, '5', 2)
    # Blast furnace molten copper vat at bottom (6 & 7 >= 50 px)
    c.fill_rect(8, 23, 24, 30, '6')
    c.fill_ellipse(16, 25, 6, 2, '7')
    # Escaping high-pressure billowy steam (8 >= 25 px)
    c.fill_circle(6, 6, 3, '8')
    # Iron catwalk bridge across mid (9 >= 25 px)
    c.draw_line(0, 19, 31, 19, '9', 1)
    # Green brass pressure gauge dial (A >= 15 px)
    c.fill_circle(10, 8, 2, 'A')
    return c.to_image(pal)

def gen_biyoluminesanmantarvadi():
    c = Canvas('1')
    pal = {
        '1': (12, 18, 38),      # Alien midnight sky
        '2': (240, 235, 255),   # Twin gas giant moons
        '3': (45, 225, 195),    # Cyan giant mushroom
        '4': (20, 125, 115),    # Mushroom shadow
        '5': (225, 45, 155),    # Magenta spores
        '6': (30, 45, 55),      # Alien terrain
        '7': (75, 185, 75),     # Moss ground
        '8': (255, 225, 65),    # Spore dust particles
        '9': (165, 65, 235),    # Violet tendril vine
        'A': (35, 95, 175),     # Glowing river rapids
    }
    # Twin moons in alien sky
    c.fill_circle(8, 6, 3, '2')
    c.fill_circle(23, 5, 2, '2')
    # Terraced alien ridges
    c.fill_polygon([(0, 13), (31, 11), (31, 23), (0, 23)], '6')
    c.fill_rect(0, 20, 31, 31, '7')
    # Bioluminescent river flowing through valley (A >= 35 px)
    c.fill_polygon([(9, 21), (15, 21), (22, 31), (14, 31)], 'A')
    # Colossal glowing cyan mushroom in center (3 & 4 >= 60 px)
    c.fill_ellipse(16, 12, 9, 4, '3')
    c.fill_ellipse(16, 13, 7, 2, '4')
    c.fill_rect(15, 15, 17, 23, '4') # Stalk
    # Hot magenta glowing spores and caps (5 >= 25 px)
    c.fill_circle(6, 17, 3, '5')
    c.fill_circle(26, 18, 3, '5')
    # Violet tendril vines climbing (9 >= 25 px)
    c.draw_line(2, 22, 5, 14, '9', 1)
    c.draw_line(29, 22, 26, 15, '9', 1)
    # Floating golden dust spores (8 >= 16 px)
    for sx, sy in [(12, 7), (20, 8), (14, 17), (18, 16), (22, 24)]:
        c.fill_circle(sx, sy, 1, '8')
    return c.to_image(pal)

def gen_kizilgezegenus():
    c = Canvas('1')
    pal = {
        '1': (145, 55, 40),     # Mars rust sky
        '2': (225, 165, 110),   # Twin moons Phobos/Deimos
        '3': (195, 80, 50),     # Red crater rim
        '4': (115, 45, 30),     # Crater shadow
        '5': (240, 245, 255),   # Geodesic habitat dome white
        '6': (85, 185, 225),    # Greenhouse dome cyan
        '7': (245, 195, 45),    # Solar array farm
        '8': (55, 60, 70),      # Six-wheeled rover
        '9': (225, 45, 45),     # Rover beacon light
        'A': (175, 125, 95),    # Tire tracks in dust
    }
    # Twin moons in Martian rust sky
    c.fill_circle(6, 4, 2, '2')
    c.fill_circle(24, 7, 1, '2')
    # Massive Martian impact crater rim (3 & 4 >= 90 px)
    c.fill_polygon([(0, 11), (14, 8), (31, 12), (31, 23), (0, 23)], '3')
    c.fill_polygon([(0, 17), (14, 14), (31, 18), (31, 23), (0, 23)], '4')
    c.fill_rect(0, 23, 31, 31, '3')
    # White pressurized geodesic habitat dome (5 >= 45 px)
    c.fill_circle(14, 18, 5, '5')
    c.fill_rect(9, 18, 19, 21, '5')
    # Cyan hydroponic greenhouse dome (6 >= 25 px)
    c.fill_circle(21, 19, 3, '6')
    # Solar array farm panels (7 >= 30 px)
    c.fill_rect(2, 17, 7, 21, '7')
    # Exploration rover with red beacon (8 & 9 >= 25 px)
    c.fill_rect(14, 25, 19, 28, '8')
    c.fill_circle(14, 24, 1, '9')
    c.fill_circle(19, 24, 1, '9')
    c.set(16, 23, '9'); c.set(16, 22, '9')
    # Wheel tire tracks in red Martian regolith (A >= 20 px)
    c.draw_line(0, 27, 14, 27, 'A', 1)
    c.draw_line(0, 29, 14, 29, 'A', 1)
    return c.to_image(pal)

def gen_buyucukutuphanesi():
    c = Canvas('1')
    pal = {
        '1': (25, 20, 42),      # Tower vaulted chamber
        '2': (115, 70, 45),     # Spiral bookshelf
        '3': (225, 55, 75),     # Crimson spellbook
        '4': (55, 135, 215),    # Cobalt blue arcane tome
        '5': (255, 225, 85),    # Golden runic lectern
        '6': (65, 225, 195),    # Levitating crystal orb
        '7': (175, 65, 225),    # Swirling magical vortex
        '8': (240, 235, 215),   # Stained glass window
        '9': (75, 45, 85),      # Velvet wizard chair
        'A': (255, 255, 255),   # Arcane sparkling stars
    }
    # Stained glass rose window in background (8 >= 35 px)
    c.fill_circle(16, 8, 5, '8')
    # Bookshelves lining walls (2, 3, 4 >= 90 px)
    c.fill_rect(0, 3, 8, 28, '2')
    c.fill_rect(24, 3, 31, 28, '2')
    for sy in [6, 12, 18, 24]:
        c.fill_rect(2, sy, 7, sy + 2, '3')
        c.fill_rect(25, sy, 30, sy + 2, '4')
    # Velvet reading armchair (9 >= 25 px)
    c.fill_rect(5, 23, 9, 29, '9')
    # Golden runic lectern in center (5 >= 25 px)
    c.fill_polygon([(12, 21), (20, 21), (18, 29), (14, 29)], '5')
    # Swirling magical spell vortex (7 >= 40 px)
    c.fill_ellipse(16, 14, 6, 3, '7')
    # Glowing levitating crystal orb (6 >= 18 px)
    c.fill_circle(16, 14, 2, '6')
    # White magical sparkling stars floating (A >= 15 px)
    for sx, sy in [(11, 11), (21, 12), (13, 17), (19, 16), (16, 20)]:
        c.fill_circle(sx, sy, 1, 'A')
    return c.to_image(pal)

def gen_tropikmercankoyu():
    c = Canvas('1')
    pal = {
        '1': (95, 185, 245),    # Tropical azure sky
        '2': (245, 240, 215),   # White sandbar atoll
        '3': (45, 145, 55),     # Coconut palm trees
        '4': (45, 205, 205),    # Crystal turquoise lagoon
        '5': (20, 115, 165),    # Deep reef drop-off
        '6': (235, 85, 115),    # Pink brain coral garden
        '7': (250, 215, 50),    # Yellow tang fish school
        '8': (65, 145, 105),    # Green sea turtle
        '9': (45, 50, 75),      # Manta ray silhouette
        'A': (240, 250, 255),   # Surf break foam
    }
    # Sandbar atoll with coconut palms
    c.fill_polygon([(0, 6), (14, 6), (18, 12), (0, 12)], '2')
    c.fill_circle(5, 5, 3, '3') # Palm trees
    # Shallow turquoise lagoon (4 >= 75 px)
    c.fill_rect(0, 11, 31, 22, '4')
    # Deep water drop-off zone at bottom (5 >= 75 px)
    c.fill_rect(0, 23, 31, 31, '5')
    # Surf break foam dividing lagoon & deep water (A >= 25 px)
    c.draw_line(0, 22, 31, 22, 'A', 1)
    # Pink coral reef garden (6 >= 35 px)
    c.fill_circle(25, 16, 4, '6')
    # Yellow tang fish school (7 >= 20 px)
    for fx, fy in [(12, 14), (16, 16), (20, 14)]:
        c.fill_ellipse(fx, fy, 2, 1, '7')
    # Gliding green sea turtle in lagoon (8 >= 25 px)
    c.fill_ellipse(8, 18, 3, 2, '8')
    c.set(11, 18, '8') # Head
    # Majestic manta ray silhouette in deep water (9 >= 30 px)
    c.fill_polygon([(12, 28), (22, 28), (17, 25)], '9')
    c.draw_line(17, 28, 17, 31, '9', 1) # Tail
    return c.to_image(pal)

def gen_ejderhaininhazinesi():
    c = Canvas('1')
    pal = {
        '1': (28, 20, 25),      # Volcanic cavern ceiling
        '2': (75, 55, 60),      # Basalt vault pillars
        '3': (245, 70, 30),     # Magma fissures
        '4': (250, 205, 45),    # Mountainous hoard of gold
        '5': (195, 145, 30),    # Gold hoard shadow
        '6': (215, 35, 40),     # Red dragon scales & wings
        '7': (130, 20, 25),     # Dragon shadow & spines
        '8': (255, 235, 135),   # Dragon glowing eye & smoke
        '9': (65, 215, 225),    # Sapphire & diamond gems
        'A': (175, 55, 195),    # Imperial ruby & velvet
    }
    # Cavern pillars on left and right (2 >= 60 px)
    c.fill_rect(0, 0, 7, 24, '2')
    c.fill_rect(25, 0, 31, 24, '2')
    # Magma fissure in floor (3 >= 35 px)
    c.fill_polygon([(0, 28), (31, 26), (31, 31), (0, 31)], '3')
    # Mountainous hoard of sparkling gold coins (4 & 5 >= 95 px)
    c.fill_polygon([(3, 29), (28, 29), (24, 18), (6, 18)], '4')
    c.fill_polygon([(18, 29), (28, 29), (24, 18), (20, 18)], '5')
    # Red dragon coiled sleeping atop the treasure hoard (6 & 7 >= 80 px)
    c.fill_ellipse(15, 16, 7, 4, '6')
    c.fill_polygon([(10, 14), (6, 9), (13, 13)], '6') # Wing
    c.fill_circle(19, 14, 3, '6') # Head
    c.draw_line(12, 13, 17, 13, '7', 1) # Dorsal spines
    # Glowing slit eye & smoke puff (8 >= 14 px)
    c.set(20, 13, '8'); c.set(21, 13, '8')
    c.fill_circle(24, 10, 2, '8')
    c.fill_circle(27, 8, 1, '8')
    # Sapphire gems glittering in gold pile (9 >= 15 px)
    for gx, gy in [(7, 23), (12, 26), (21, 22), (25, 25)]:
        c.fill_circle(gx, gy, 1, '9')
    # Imperial royal velvet cushions & rubies (A >= 15 px)
    for rx, ry in [(9, 21), (17, 26), (23, 20)]:
        c.fill_circle(rx, ry, 1, 'A')
    return c.to_image(pal)

GENERATORS = {
    # 10 Kolay (Full 32x32, 5 colors)
    'kolay/TamGunesTutulmasi': gen_tamgunestutulmasi,
    'kolay/LavMagarasi': gen_lavmagarasi,
    'kolay/KutupGeyigi': gen_kutupgeyigi,
    'kolay/SihirliMantarOrmani': gen_sihirlimantarormani,
    'kolay/KapadokyaVadi': gen_kapadokyavadi,
    'kolay/MercanResifiGece': gen_mercanresifigece,
    'kolay/UcurumBonsaisi': gen_ucurumbonsaisi,
    'kolay/SamurayKalesi': gen_samuraykalesi,
    'kolay/GokyuzuAdasi': gen_gokyuzyadasi,
    'kolay/AntikLiman': gen_antikliman,

    # 10 Orta (Full 32x32, 8 colors)
    'orta/AntikKutuphane': gen_antikkutuphane,
    'orta/KorsanHazineMagarasi': gen_korsanhazinemagarasi,
    'orta/BuharliGemi': gen_buharligemi,
    'orta/SiberRamenBari': gen_siberramenbari,
    'orta/OrtaCagDemircisi': gen_ortacagdemircisi,
    'orta/ColVahasiGece': gen_colvahasigece,
    'orta/UzayIstasyonuYorunge': gen_uzayistasyonuyorunge,
    'orta/AlpDagEvi': gen_alpdagevi,
    'orta/KristalMagaraGolu': gen_kristalmagaragolu,
    'orta/GelenekselCayBahcesi': gen_gelenekselcaybahcesi,

    # 10 Zor (Full 32x32, 10 colors)
    'zor/AtlantisHarabeleri': gen_atlantisharabeleri,
    'zor/SiberGokdelenKanyonu': gen_sibergokdelenkanyonu,
    'zor/KuzeyKalesiAurora': gen_kuzeykalesiaurora,
    'zor/MayaPiramidiGunesi': gen_mayapiramidigunesi,
    'zor/SteampunkFabrikasi': gen_steampunkfabrikasi,
    'zor/BiyoluminesanMantarVadi': gen_biyoluminesanmantarvadi,
    'zor/KizilGezegenUs': gen_kizilgezegenus,
    'zor/BuyucuKutuphanesi': gen_buyucukutuphanesi,
    'zor/TropikMercanKoyu': gen_tropikmercankoyu,
    'zor/EjderhaIninHazinesi': gen_ejderhaininhazinesi,
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
    if cubes != 1024:
        warns.append(f"Cubes is {cubes} != 1024 (not full canvas)")
        
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
    print("Generating 30 batch 7 pixel arts (10 Kolay, 10 Orta, 10 Zor - all full canvas)...")
    all_ok = True
    for rel_path, gen_fn in GENERATORS.items():
        im = gen_fn()
        out_png = os.path.join('Assets', '04.11.2026', rel_path + '.png')
        os.makedirs(os.path.dirname(out_png), exist_ok=True)
        im.save(out_png)
        make_meta(out_png)
        
        cubes, n_colors, warns = validate_image(rel_path, im)
        status = "[OK  ]" if not warns else "[WARN]"
        print(f"{status} {rel_path:28} -> cubes={cubes:4}, colors={n_colors:2}")
        for w in warns:
            print(f"       WARN:  {w}")
            all_ok = False
            
    print(f"\nCompleted 30 generations. Overall OK: {all_ok}")

if __name__ == '__main__':
    main()
