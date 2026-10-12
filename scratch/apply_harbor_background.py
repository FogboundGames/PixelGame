import numpy as np
from PIL import Image
import os

MEDIA_PATH = r'C:\Users\ezgid\.gemini\antigravity-ide\brain\b0e11281-eb77-4b21-b381-cd5c39c72a1e\.user_uploaded\media_1791826791914.png'

# 1. Load original uploaded screenshot
src_img = Image.open(MEDIA_PATH)
# Exact inner screen bounds without phone bezel:
# x: 33 to 515 (w = 482)
# y: 50 to 1000 (h = 950)
crop = src_img.crop((33, 50, 515, 1000))
arr = np.array(crop).astype(np.float32)
h_crop, w_crop, _ = arr.shape

# 2. Clean sky (remove old UI buttons, keep crane trusses)
top_sky = np.array([90.0, 203.0, 253.0], dtype=np.float32)
mid_sky = np.array([148.0, 225.0, 253.0], dtype=np.float32)

for y in range(0, 75):
    t = y / 75.0
    sky_col = top_sky * (1.0 - t) + mid_sky * t
    for x in range(0, w_crop):
        # Preserve yellow crane trusses:
        is_crane = (x < 35 and y >= 60 and arr[y, x, 0] > 180 and arr[y, x, 1] > 130 and arr[y, x, 2] < 90)
        if not is_crane:
            if y < 65:
                arr[y, x, :3] = sky_col
            else:
                fade = (75.0 - y) / 10.0
                arr[y, x, :3] = arr[y, x, :3] * (1.0 - fade) + sky_col * fade

# 3. Clean platform (remove old 2D boat cubes & shadow)
x_l = 110
x_r = 375
boat_mask = np.zeros((h_crop, w_crop), dtype=np.float32)
for y in range(220, 460):
    for x in range(x_l, x_r):
        dist_x = min(x - x_l, x_r - 1 - x) / 25.0
        dist_y = min(y - 220, 459 - y) / 20.0
        boat_mask[y, x] = np.clip(min(dist_x, dist_y), 0.0, 1.0)

for y in range(220, 460):
    col_l = arr[y, x_l, :3]
    col_r = arr[y, x_r, :3]
    for x in range(x_l, x_r):
        t = (x - x_l) / float(x_r - x_l)
        interp_col = col_l * (1.0 - t) + col_r * t
        m = boat_mask[y, x]
        arr[y, x, :3] = arr[y, x, :3] * (1.0 - m) + interp_col * m

cleaned_crop = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGBA')

# 4. Extract upper harbor scene (y = 0 to 235: sky, sea, cargo ship, tugboat, crane, warehouse, quays)
harbor_crop = cleaned_crop.crop((0, 0, 482, 235))
# Scale proportionally to 1080 width:
h_harbor = round(235 * 1080 / 482)  # 527 px
harbor_scaled = harbor_crop.resize((1080, h_harbor), Image.Resampling.LANCZOS)
harbor_arr = np.array(harbor_scaled).astype(np.float32)

# Platform top color and bottom color for smooth vertical gradient
top_col = harbor_arr[-1, 540, :3]  # [215, 226, 237]
bot_col = np.array([199.0, 207.0, 215.0], dtype=np.float32)

np.random.seed(42)
platform_noise = (np.random.rand(1218 - h_harbor, 1080, 1) - 0.5) * 1.5

def build_background(existing_path):
    orig_img = Image.open(existing_path)
    has_alpha = (orig_img.mode == 'RGBA')
    orig = np.array(orig_img).astype(np.float32)
    
    new_bg = np.zeros((1920, 1080, 4), dtype=np.float32)
    new_bg[:, :, 3] = 255.0
    
    # Upper harbor (0 to 527)
    new_bg[:h_harbor, :, :3] = harbor_arr[:, :, :3]
    
    # Platform (527 to 1218)
    for y in range(h_harbor, 1218):
        t = (y - float(h_harbor)) / (1218.0 - float(h_harbor))
        st = t * t * (3.0 - 2.0 * t)  # Smoothstep
        col = top_col * (1.0 - st) + bot_col * st
        new_bg[y, :, :3] = col
    new_bg[h_harbor:1218, :, :3] = np.clip(new_bg[h_harbor:1218, :, :3] + platform_noise, 0, 255)
    
    # Pier, berths & water (1218 to 1920) - preserved from current improved layout!
    new_bg[1218:, :, :3] = orig[1218:, :, :3]
    if orig.shape[-1] == 4:
        new_bg[1218:, :, 3] = orig[1218:, :, 3]
        
    mode = 'RGBA' if has_alpha else 'RGB'
    if mode == 'RGB':
        res = Image.fromarray(np.clip(new_bg[:, :, :3], 0, 255).astype(np.uint8), 'RGB')
    else:
        res = Image.fromarray(np.clip(new_bg, 0, 255).astype(np.uint8), 'RGBA')
    return res

# 5. Apply to all three background files
targets = [
    'Assets/Kenney/BeachBackground_Clean.png',
    'Assets/Kenney/BeachBackground_3Slots.png',
    'Assets/Kenney/BeachBackground_4Slots.png'
]

for t in targets:
    img = build_background(t)
    img.save(t)
    print('Updated:', t)

# Also save preview for inspection
preview = build_background('Assets/Kenney/BeachBackground_3Slots.png')
preview.save('scratch/harbor_background_final_preview.png')
print('Preview saved to scratch/harbor_background_final_preview.png')
