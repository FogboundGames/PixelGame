import os
from PIL import Image, ImageDraw, ImageFont

C_WHITE = (245, 240, 228, 255) # Sail
C_BLUE  = (60, 140, 230, 255)  # Hull
C_RED   = (222, 58, 52, 255)   # Deck, Flag & Bow
C_BROWN = (120, 75, 40, 255)   # Mast & Step

# V1: Raised Bow Sheer (Deck sweeps UP and OUT at prow)
# Look at row 8, 9, 10 on the right:
# Row 8: ................R (Tiny raised bowsprit tip!)
# Row 9: ..RRRRRRRRRRRRR.  (Red deck sheer)
# Row 10: ...BBBBBBBBBBBB. (Blue hull)
v1 = [
    '................', # 0
    '.......MRRR.....', # 1 - Flag
    '.......M........', # 2 - Mast
    '.....W.M.W......', # 3 - Sail tops
    '....WW.M.WW.....', # 4 - Sails
    '...WWW.M.WWW....', # 5 - Sails
    '..WWWW.M.WWWW...', # 6 - Sails
    '.WWWWW.M.WWWWW..', # 7 - Sails
    '......MMM......R', # 8 - Mast Step + Raised Bow Tip (R) at X=15!
    '..RRRRRRRRRRRRR.', # 9 - Deck sweeps forward
    '...BBBBBBBBBBBB.', # 10 - Blue hull
    '....BBBBBBBBBB..', # 11 - Mid hull
    '.....BBBBBBBB...', # 12 - Keel
    '................', # 13
]

# V2: Sleek Clipper Cut (Hull sweeps forward into a sharp marine prow)
# Row 9:  ..RRRRRRRRRRRR.
# Row 10: ...BBBBBBBBBBBB (Prow extends to X=14)
# Row 11: ....BBBBBBBBBB.
# Row 12: .....BBBBBBBB..
v2 = [
    '...............', # 0
    '.......MRRR....', # 1 - Flag
    '.......M.......', # 2 - Mast
    '.....W.M.W.....', # 3 - Sails
    '....WW.M.WW....', # 4 - Sails
    '...WWW.M.WWW...', # 5 - Sails
    '..WWWW.M.WWWW..', # 6 - Sails
    '.WWWWW.M.WWWWW.', # 7 - Sails
    '......MMM......', # 8 - Mast Step (3 blocks)
    '..RRRRRRRRRRRR.', # 9 - Deck
    '...BBBBBBBBBBBB', # 10 - Prow extends to X=14
    '....BBBBBBBBBB.', # 11 - Mid hull
    '.....BBBBBBBB..', # 12 - Keel
    '...............', # 13
]

# V3: Double-Sail Billowing Sloop with Deck Cabin Accent
# - Asymmetrical main sail with wind belly
# - Jib sail forward
# - Red deck line with golden/distinct cabin or sheer
v3 = [
    '...............', # 0
    '......MRRR.....', # 1 - Flag
    '......M........', # 2 - Mast
    '....W.M.W......', # 3 - Sails
    '...WW.M.WW.....', # 4 - Sails
    '..WWW.M.WWW....', # 5 - Sails
    '.WWWW.M.WWWW...', # 6 - Sails
    'WWWWW.M.WWWWW..', # 7 - Billowing mainsail on left
    '.....MMM.......', # 8 - Sturdy Mast Step
    '.RRRRRRRRRRRR..', # 9 - Deck
    '..BBBBBBBBBBBB.', # 10 - Hull with forward sheer
    '...BBBBBBBBBBB.', # 11 - Mid hull
    '....BBBBBBBBB..', # 12 - Keel
    '...............', # 13
]

# V4: True Toy Boat (Chunky & Cute - 14x14)
# - Very rounded, chunky, adorable toy boat proportions
# - 28 white, 22 blue, 12 red, 8 brown = 70 cubes
v4 = [
    '..............', # 0
    '......MRR.....', # 1 - Flag (2 red)
    '......M.......', # 2 - Mast
    '.....WM.W.....', # 3 - Sails
    '....WWM.WW....', # 4 - Sails
    '...WWWM.WWW...', # 5 - Sails
    '..WWWWM.WWWW..', # 6 - Sails
    '.WWWW.M.WWWW..', # 7 - Sails (curved cut)
    '.....MMM......', # 8 - Mast Foot (3 brown)
    '.RRRRRRRRRRR..', # 9 - Red Deck
    '..BBBBBBBBBBB.', # 10 - Hull (raised bow at right)
    '...BBBBBBBBB..', # 11 - Mid hull
    '....BBBBBBB...', # 12 - Keel
    '..............', # 13
]

versions = {
    'V1: Bowsprit Raised Prow': v1,
    'V2: Sleek Clipper Cut': v2,
    'V3: Billowing Wind Sloop': v3,
    'V4: Chunky Cute Toy Boat': v4,
}

def render(grid, cell_size=32):
    h = len(grid)
    w = len(grid[0])
    img = Image.new('RGBA', (w * cell_size, h * cell_size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cmap = {'W': C_WHITE, 'B': C_BLUE, 'R': C_RED, 'M': C_BROWN}
    for y in range(h):
        for x in range(w):
            ch = grid[y][x]
            if ch in cmap:
                c = cmap[ch]
                px = x * cell_size
                py = y * cell_size
                draw.rounded_rectangle([px + 3, py + 3, px + cell_size - 3, py + cell_size - 3], radius=7, fill=c)
                hl = tuple(min(255, int(v * 1.30)) for v in c[:3]) + (255,)
                draw.rounded_rectangle([px + 5, py + 5, px + cell_size - 5, py + 10], radius=3, fill=hl)
                sh = tuple(max(0, int(v * 0.70)) for v in c[:3]) + (255,)
                draw.rounded_rectangle([px + 5, py + cell_size - 8, px + cell_size - 5, py + cell_size - 5], radius=2, fill=sh)
    return img

col_w = 480
comp = Image.new('RGB', (len(versions) * col_w + 40, 600), (18, 24, 34))
draw = ImageDraw.Draw(comp)
try:
    font_title = ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf', 20)
    font_sub = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 15)
except:
    font_title = font_sub = ImageFont.load_default()

x_offset = 20
for name, g in versions.items():
    draw.text((x_offset, 20), name, font=font_title, fill=(245, 250, 255))
    c_dict = {'W': 0, 'B': 0, 'R': 0, 'M': 0}
    for row in g:
        for ch in row:
            if ch in c_dict: c_dict[ch] += 1
    total = sum(c_dict.values())
    cnt_str = f"Toplam: {total} | W: {c_dict['W']} | B: {c_dict['B']} | R: {c_dict['R']} | M: {c_dict['M']}"
    draw.text((x_offset, 52), cnt_str, font=font_sub, fill=(140, 180, 220))
    im = render(g)
    comp.paste(im, (x_offset, 90), im)
    x_offset += col_w

comp.save('scratch/prow_variations_comparison.png')
print('Prow variations comparison saved!')
