import os
from PIL import Image, ImageDraw, ImageFont
import numpy as np

C_WHITE = (245, 240, 228, 255) # Sail
C_BLUE  = (60, 140, 230, 255)  # Hull
C_RED   = (222, 58, 52, 255)   # Deck, Bowsprit & Flag
C_BROWN = (120, 75, 40, 255)   # Mast, Spar & Base

# Current (Old) for reference:
old_layout = [
    '..............',
    '..............',
    '......M.......',
    '.....WMW......',
    '....WWMWW.....',
    '...WWWMWWW....',
    '..WWWWMWWWW...',
    '.WWWWWMWWWWW..',
    '......M.......',
    '.RRRRRRRRRRR..',
    '..BBBBBBBBB...',
    '...BBBBBBB....',
    '..............',
    '..............',
]

# Option A: "Classic Toy Cutter" (14x14)
# - Red pennant at masthead (pointing right with the wind)
# - Mainsail (left) has curved belly billow; Jib (right) is a sharp triangle
# - Mast has a reinforced base step (MMM) at the deck level (row 8)
# - Deck (red) sweeps up at right into a jaunty raised prow (burun)
# - Hull (blue) follows the sheer curve: raised at bow, curved keel at bottom
opt_a = [
    '..............', # 0
    '......MRR.....', # 1 - Masthead + Red Pennant
    '......M.......', # 2 - Mast
    '....W.M.W.....', # 3 - Sail tips
    '...WW.M.WW....', # 4 - Sails
    '..WWW.M.WWW...', # 5 - Sails
    '.WWWW.M.WWWW..', # 6 - Mainsail + Jib
    'WWWWW.M.WWWWW.', # 7 - Billowing sail base
    '.....MMM......', # 8 - Sturdy Mast Step Tab!
    '..RRRRRRRRRRR.', # 9 - Deck line
    '...BBBBBBBBBBB', # 10 - Hull with sharp raised prow at X=13!
    '....BBBBBBBBB.', # 11 - Tapered mid-hull
    '.....BBBBBBB..', # 12 - Curved keel
    '..............', # 13
]

# Option B: "Charming Wooden Toy Yacht" (14x14)
# - Pronounced bowsprit (burun ucu) pointing forward
# - Transom stern at left
# - Dual sails with distinct gap along the mast
# - Reinforced mast foot
opt_b = [
    '..............', # 0
    '.....MRRR.....', # 1 - Triple red pennant!
    '.....M........', # 2 - Mast
    '...W.M.W......', # 3 - Top of sails
    '..WW.M.WW.....', # 4 - Sails
    '.WWW.M.WWW....', # 5 - Sails
    'WWWW.M.WWWW...', # 6 - Sails
    '.WWW.M.WWWWW..', # 7 - Billowing mainsail curve
    '....MMM.......', # 8 - Reinforced Mast Base (3 blocks)
    '.RRRRRRRRRRR..', # 9 - Deck line
    '..BBBBBBBBBBBR', # 10 - Blue hull with raised red bowsprit tip at front!
    '...BBBBBBBBBB.', # 11 - Hull curve
    '....BBBBBBBB..', # 12 - Keel
    '..............', # 13
]

# Option C: "Bold Clipper Bow Sailboat" (15x14)
# - 15 columns wide: gives a magnificent, aerodynamic toy boat bow (burun)!
# - Raised prow in both deck (row 9) and hull (row 10)
# - Sail shapes are clearly separated: large main sail, dynamic jib
# - Sturdy mast step firmly rooted between sails and deck
opt_c = [
    '...............', # 0
    '.......MRR.....', # 1 - Pennant
    '.......M.......', # 2 - Mast
    '.....W.M.W.....', # 3 - Sails
    '....WW.M.WW....', # 4 - Sails
    '...WWW.M.WWW...', # 5 - Sails
    '..WWWW.M.WWWW..', # 6 - Sails
    '.WWWWW.M.WWWWW.', # 7 - Sails
    '......MMM......', # 8 - Reinforced Mast Step
    '..RRRRRRRRRRRR.', # 9 - Deck extending into prow!
    '...BBBBBBBBBBBB', # 10 - Hull sweeps all the way to X=14 (sharp bow)!
    '....BBBBBBBBBB.', # 11 - Mid-hull
    '.....BBBBBBBB..', # 12 - Keel
    '...............', # 13
]

# Option D: "Playful Pocket Sloop" (14x14 with clean round counts)
# - Tuned for round numbers: White=28, Blue=24, Red=12, Brown=8 (Total=72)
# - Very readable silhouette from far away on mobile screens!
opt_d = [
    '..............', # 0
    '......MRR.....', # 1 - Flag (2 red)
    '......M.......', # 2 - Mast
    '.....WM.W.....', # 3 - Sails
    '....WWM.WW....', # 4 - Sails
    '...WWWM.WWW...', # 5 - Sails
    '..WWWWM.WWWW..', # 6 - Sails
    '.WWWW.M.WWWW..', # 7 - Sails
    '.....MMM......', # 8 - Mast Foot (3 brown)
    '.RRRRRRRRRR...', # 9 - Deck (10 red)
    '..BBBBBBBBBBB.', # 10 - Upper Hull with forward prow (11 blue)
    '...BBBBBBBBB..', # 11 - Mid Hull (9 blue)
    '....BBBBBB....', # 12 - Keel (6 blue)
    '..............', # 13
]

options = {
    'Mevcut (Old)': old_layout,
    'Secenek A: Klasik Oyuncak Sloop (Belirgin Burun & Flama)': opt_a,
    'Secenek B: Ahsap Oyuncak Yacht (Burun Ucu & Kavisli Yelken)': opt_b,
    'Secenek C: Bold Clipper (15x Genis Burun Silueti)': opt_c,
    'Secenek D: Pocket Sloop (Net Hatlar & Dengeli Sayilar)': opt_d,
}

def render_grid_styled(grid, cell_size=32):
    h = len(grid)
    w = len(grid[0])
    img = Image.new('RGBA', (w * cell_size, h * cell_size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cmap = {
        'W': C_WHITE,
        'B': C_BLUE,
        'R': C_RED,
        'M': C_BROWN
    }
    for y in range(h):
        for x in range(w):
            ch = grid[y][x]
            if ch in cmap:
                color = cmap[ch]
                px = x * cell_size
                py = y * cell_size
                # Rounded voxel cube
                draw.rounded_rectangle([px + 3, py + 3, px + cell_size - 3, py + cell_size - 3], radius=7, fill=color)
                # Glossy top highlight
                hl = tuple(min(255, int(c * 1.30)) for c in color[:3]) + (255,)
                draw.rounded_rectangle([px + 5, py + 5, px + cell_size - 5, py + 10], radius=3, fill=hl)
                # Bottom occlusion shadow
                sh = tuple(max(0, int(c * 0.70)) for c in color[:3]) + (255,)
                draw.rounded_rectangle([px + 5, py + cell_size - 8, px + cell_size - 5, py + cell_size - 5], radius=2, fill=sh)
    return img

imgs = {}
counts = {}
for name, g in options.items():
    imgs[name] = render_grid_styled(g)
    c_dict = {'W': 0, 'B': 0, 'R': 0, 'M': 0}
    for row in g:
        for ch in row:
            if ch in c_dict: c_dict[ch] += 1
    counts[name] = c_dict

col_w = 480
comp_w = len(options) * col_w + 40
comp_h = 600
comp = Image.new('RGB', (comp_w, comp_h), (18, 24, 34))
draw = ImageDraw.Draw(comp)

try:
    font_title = ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf', 20)
    font_sub = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 15)
except:
    font_title = font_sub = ImageFont.load_default()

x_offset = 20
for name, g in options.items():
    draw.text((x_offset, 20), name, font=font_title, fill=(245, 250, 255))
    cnt = counts[name]
    total = sum(cnt.values())
    cnt_str = f"Toplam: {total} | Beyaz: {cnt['W']} | Mavi: {cnt['B']} | Kirmizi: {cnt['R']} | Kahve: {cnt['M']}"
    draw.text((x_offset, 52), cnt_str, font=font_sub, fill=(140, 180, 220))
    im = imgs[name]
    comp.paste(im, (x_offset, 90), im)
    x_offset += col_w

out_path = 'scratch/refined_boat_comparison.png'
comp.save(out_path)
art_path = r'C:\Users\ezgid\.gemini\antigravity-ide\brain\b0e11281-eb77-4b21-b381-cd5c39c72a1e\refined_boat_comparison.png'
comp.save(art_path)
print('Refined boat comparison generated successfully!')
