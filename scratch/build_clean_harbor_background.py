import numpy as np
from PIL import Image, ImageFilter, ImageDraw
import os

screen = Image.open('scratch/ref_screen_only.png')
# High-quality resize to 1080 x 1920
bg = screen.resize((1080, 1920), Image.Resampling.LANCZOS)
arr = np.array(bg).astype(np.float32)

h, w, c = arr.shape

# =========================================================================
# 1. CLEAN THE SKY (Y = 0 to 140, X = 150 to 930)
# =========================================================================
# Preserving the crane on the left (X < 150) and tugboat/warehouse on the right (X > 930)
# The sky has a smooth vertical gradient:
# At Y=140, sky color is around [148, 225, 253]
# At Y=0, deep sky color is around [60, 185, 248]
top_sky = np.array([65.0, 185.0, 248.0], dtype=np.float32)
mid_sky = np.array([148.0, 225.0, 253.0], dtype=np.float32)

sky_mask = np.zeros((h, w), dtype=np.float32)
for y in range(0, 145):
    for x in range(0, w):
        # We want to replace the old buttons:
        # Lifebuoy & 1X: X between 40 and 260, Y < 135
        # Level 1: X between 340 and 740, Y < 135
        # Sound & Phone: X between 800 and 1040, Y < 135
        # Avoid overwriting the crane yellow trusses at X < 180, Y > 130
        is_button_zone = False
        if y < 135:
            # Check if this pixel is an old button (not the sky or crane)
            # The crane trusses are yellow: R > 200, G > 140, B < 80
            r, g, b = arr[y, x, :3]
            is_crane = (r > 190 and g > 130 and b < 90)
            if not is_crane:
                is_button_zone = True
        if is_button_zone:
            # Smooth blend near y=135
            fade = np.clip((140.0 - y) / 15.0, 0.0, 1.0)
            sky_mask[y, x] = fade

# Apply sky gradient
for y in range(0, 145):
    t = y / 145.0
    col = top_sky * (1.0 - t) + mid_sky * t
    m = sky_mask[y, :, None]
    arr[y, :, :3] = arr[y, :, :3] * (1.0 - m) + col * m

# =========================================================================
# 2. CLEAN THE PLAY PLATFORM (Remove Old Boat Cubes & Shadow)
# =========================================================================
# The old boat is located approximately at:
# X: 290 to 790
# Y: 520 to 1050
# Outside this box (X: 130 to 290 and X: 790 to 950), the platform is completely clean!
# For each Y row from 460 to 1100:
# Left sample at x_left = 260
# Right sample at x_right = 820
x_l = 250
x_r = 830

# Create mask for the boat area
boat_mask = np.zeros((h, w), dtype=np.float32)
for y in range(460, 1080):
    for x in range(x_l, x_r):
        # Calculate distance to edges for smooth feathering
        dist_x = min(x - x_l, x_r - 1 - x) / 45.0
        dist_y = min(y - 460, 1079 - y) / 35.0
        alpha = np.clip(min(dist_x, dist_y), 0.0, 1.0)
        boat_mask[y, x] = alpha

# Horizontal interpolation across boat area
for y in range(460, 1080):
    col_l = arr[y, x_l, :3]
    col_r = arr[y, x_r, :3]
    # Linear blend across X
    for x in range(x_l, x_r):
        t = (x - x_l) / float(x_r - x_l)
        interp_col = col_l * (1.0 - t) + col_r * t
        m = boat_mask[y, x]
        arr[y, x, :3] = arr[y, x, :3] * (1.0 - m) + interp_col * m

# =========================================================================
# 3. CLEAN WATER AREA (Y = 1250 to 1920)
# =========================================================================
# In the reference image, the dashed slots are at Y=1250 to 1500, and 3 ships at Y=1500 to 1800.
# The user explicitly said: "ama slot ayarlarını görseldeki gibi yapma lütfen"
# We should take the pristine turquoise water from BeachBackground_Clean.png!
clean_bg = Image.open('Assets/Kenney/BeachBackground_Clean.png')
clean_arr = np.array(clean_bg).astype(np.float32)

# Blend from harbor platform into clean water at Y ~ 1240 to 1270
water_blend = np.clip((np.arange(h) - 1240.0) / 30.0, 0.0, 1.0)[:, None, None]

# For the water area, use the clean water from BeachBackground_Clean.png
# This preserves the pristine caustics and removes any dashed slot lines or flat ships!
arr[:, :, :3] = arr[:, :, :3] * (1.0 - water_blend) + clean_arr[:, :, :3] * water_blend

# Save result
result = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGB')
out_path = 'scratch/harbor_background_unified_1080.png'
result.save(out_path)
print('Saved', out_path)
