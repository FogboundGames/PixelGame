import glob, os, re
from PIL import Image
import numpy as np

shoreline = [
    (6.49, -4.50, 4.49),
    (5.87, -4.50, 4.49),
    (5.24, -4.50, 4.49),
    (4.62, -4.50, 4.49),
    (3.99, -4.50, 4.49),
    (3.37, -4.42, 4.36),
    (2.74, -3.38, 3.46),
    (2.12, -2.91, 2.93),
    (1.49, -2.84, 2.73),
    (0.87, -2.71, 2.72),
    (0.24, -2.51, 2.59),
    (-0.38, -2.42, 2.51)
]

def get_sand_x_bounds(y):
    for i in range(len(shoreline)-1):
        y1, l1, r1 = shoreline[i]
        y2, l2, r2 = shoreline[i+1]
        if y2 <= y <= y1:
            t = (y - y2) / (y1 - y2)
            left = l2 + t * (l1 - l2)
            right = r2 + t * (r1 - r2)
            return left, right
    if y > shoreline[0][0]:
        return -4.5, 4.5
    return shoreline[-1][1], shoreline[-1][2]

base_center_y = 3.75
world_width = 4.80
world_height = 7.00

metas = glob.glob('Assets/**/*.meta', recursive=True)
meta_cache = {}
for m in metas:
    with open(m, 'r', encoding='utf-8') as mf:
        c = mf.read()
        m_g = re.search(r'guid: ([a-f0-9]{32})', c)
        if m_g: meta_cache[m_g.group(1)] = m[:-5]

for lpath in sorted(glob.glob('Assets/Levels/*.asset')):
    with open(lpath, 'r', encoding='utf-8') as f: text = f.read()
    tex_guid_m = re.search(r'm_LevelTexture: \{fileID: 2800000, guid: ([a-f0-9]{32})', text)
    name_m = re.search(r'm_LevelName: ["\']?([^"\n\r]+)', text)
    lname = name_m.group(1) if name_m else os.path.basename(lpath)
    if not tex_guid_m: continue
    tex_path = meta_cache.get(tex_guid_m.group(1))
    if not tex_path or not os.path.exists(tex_path): continue
    img = Image.open(tex_path).convert('RGBA')
    arr = np.array(img)
    alpha = arr[:, :, 3]
    ys, xs = np.where(alpha > 25)
    if len(xs) == 0: continue
    visW = int(max(xs) - min(xs) + 1)
    visH = int(max(ys) - min(ys) + 1)
    
    cell = min(world_width / visW, world_height / visH)
    fig_w = visW * cell * 1.06
    fig_h = visH * cell * 1.06
    
    slack = world_height - fig_h
    cy = base_center_y + max(0.0, slack * 0.40)
    
    top_y = cy + fig_h / 2
    bot_y = cy - fig_h / 2
    left_x = -fig_w / 2
    right_x = fig_w / 2
    
    sand_l, sand_r = get_sand_x_bounds(bot_y)
    margin_l = left_x - sand_l
    margin_r = sand_r - right_x
    margin_top = 8.8 - top_y
    margin_bot = bot_y - (-0.38)
    
    has_overflow = (margin_l < 0.1) or (margin_r < 0.1) or (margin_top < 0.4) or (margin_bot < 0.2)
    status = 'FAIL' if has_overflow else 'OK'
    print(f'[{status}] {lname:16s} ({visW:2d}x{visH:2d}): cell={cell:.3f} W={fig_w:.2f} H={fig_h:.2f} | Y:[{bot_y:.2f}, {top_y:.2f}] X:[{left_x:.2f}, {right_x:.2f}] | Margins: L={margin_l:.2f} R={margin_r:.2f} Top={margin_top:.2f} Bot={margin_bot:.2f}')
