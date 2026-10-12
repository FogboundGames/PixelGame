import os
from PIL import Image, ImageDraw, ImageFont
import numpy as np

# Palette
C_EMPTY = (0, 0, 0, 0)
C_WHITE = (245, 240, 228, 255) # Sail
C_BLUE  = (60, 140, 230, 255)  # Hull
C_RED   = (222, 58, 52, 255)   # Deck & Flag
C_BROWN = (120, 75, 40, 255)   # Mast & Spar

# Old Layout:
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

# Concept 1: Balanced Toy Sloop with Distinct Prow & Flag
# - Mast top has a red pennant flag pointing right
# - Mainsail on left (curved wind pocket), Jib triangle on right pointing forward
# - Sturdy mast step tab
# - Prow sweeps forward and up on right (X=12) with a bold raised bow
# - Tapered hull bottom
cand_1 = [
    '..............',
    '......MRR.....', # Masthead + Red Flag
    '......M.......',
    '....W.M.W.....',
    '...WW.M.WW....',
    '..WWW.M.WWW...',
    '.WWWW.M.WWWW..',
    'WWWWW.M.WWWW..', # Mainsail billow + Jib
    '.....MMM......', # Reinforced Mast Step
    '.RRRRRRRRRRRR.', # Deck line with raised bow tip
    '..BBBBBBBBBBB.', # Upper hull
    '...BBBBBBBBB..', # Mid hull
    '....BBBBBBB...', # Keel bottom
    '..............',
]

# Concept 2: Iconic Clipper Bow Toy Sailboat (Strong prow lift, dual crisp sails)
# - Very strong forward-facing prow (burun)
# - Clear separation between deck, hull and sails
# - Mast cleanly rooted into hull
cand_2 = [
    '..............',
    '......MRR.....', # Flag
    '......M.......',
    '.....WM.W.....',
    '....WWM.WW....',
    '...WWWM.WWW...',
    '..WWWWM.WWWW..',
    '.WWWWWM.WWWWW.',
    '.....MMM......', # Mast Foot
    '..RRRRRRRRRRR.', # Deck
    '...BBBBBBBBBBB', # Swept Prow (Burun) at right
    '....BBBBBBBBB.', # Hull taper
    '.....BBBBBBB..', # Keel
    '..............',
]

# Concept 3: Dynamic Swept Yacht with Billowing Sails
# - Sail shape looks filled with sea breeze
# - Prominent angled prow (burun)
# - Deck line with cabin/stern transom step
cand_3 = [
    '..............',
    '.....MRRR.....', # 3-block Fluttering Pennant
    '.....M........',
    '...W.M.W......',
    '..WW.M.WW.....',
    '.WWW.M.WWW....',
    'WWWW.M.WWWW...',
    '.WWW.M.WWWWW..', # Billow
    '....MMM.......', # Solid mast base
    '.RRRRRRRRRRRR.', # Full deck
    '..BBBBBBBBBBB.', # Sleek hull
    '...BBBBBBBBB..',
    '....BBBBBBB...',
    '..............',
]

candidates = {
    'Mevcut (Old)': old_layout,
    'Konsept 1: Dengeli Oyuncak Sloop': cand_1,
    'Konsept 2: Belirgin Burunlu Clipper': cand_2,
    'Konsept 3: Ruzgarli Dinamik Yacht': cand_3,
}

def render_grid(grid, cell_size=28):
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
                # Cube body
                draw.rounded_rectangle([px + 2, py + 2, px + cell_size - 2, py + cell_size - 2], radius=6, fill=color)
                # Cube top highlight
                hl = tuple(min(255, int(c * 1.25)) for c in color[:3]) + (255,)
                draw.rectangle([px + 4, py + 4, px + cell_size - 4, py + 8], fill=hl)
                # Inner edge shadow
                sh = tuple(max(0, int(c * 0.75)) for c in color[:3]) + (255,)
                draw.rectangle([px + 4, py + cell_size - 6, px + cell_size - 4, py + cell_size - 4], fill=sh)
    return img

imgs = {}
counts = {}
for name, g in candidates.items():
    imgs[name] = render_grid(g)
    c_dict = {'W': 0, 'B': 0, 'R': 0, 'M': 0}
    for row in g:
        for ch in row:
            if ch in c_dict: c_dict[ch] += 1
    counts[name] = c_dict

comp_w = len(candidates) * 440 + 40
comp_h = 560
comp = Image.new('RGB', (comp_w, comp_h), (20, 26, 36))
draw = ImageDraw.Draw(comp)
try:
    font = ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf', 20)
    font_sm = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 15)
except:
    font = font_sm = ImageFont.load_default()

x_offset = 20
for name, g in candidates.items():
    draw.text((x_offset, 18), name, font=font, fill=(245, 250, 255))
    cnt = counts[name]
    total = sum(cnt.values())
    cnt_str = f"Toplam: {total} | Beyaz: {cnt['W']} | Mavi: {cnt['B']} | Kirmizi: {cnt['R']} | Kahve: {cnt['M']}"
    draw.text((x_offset, 48), cnt_str, font=font_sm, fill=(150, 185, 215))
    im = imgs[name]
    comp.paste(im, (x_offset, 85), im)
    x_offset += 440

out_path = 'scratch/sailboat_concepts_comparison.png'
comp.save(out_path)
art_path = r'C:\Users\ezgid\.gemini\antigravity-ide\brain\b0e11281-eb77-4b21-b381-cd5c39c72a1e\sailboat_concepts_comparison.png'
comp.save(art_path)
print('Sailboat concepts comparison saved!')
