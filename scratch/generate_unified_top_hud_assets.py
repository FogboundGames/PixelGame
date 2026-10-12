import math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont
import os

# Base paths
ui_dir = 'Assets/UI/CasualUI'
scratch_dir = 'scratch'
os.makedirs(scratch_dir, exist_ok=True)

# 1. Load Master Blue Baseplate
baseplate_orig = Image.open(os.path.join(ui_dir, 'btn_marine_blank.png')).convert('RGBA')
w, h = baseplate_orig.size # 512, 512

def add_glyph_to_baseplate(glyph_alpha_mask, glyph_color=(255, 255, 255), shadow_offset=(0, 10), shadow_blur=8):
    """
    Takes a 512x512 grayscale mask of a glyph (0-255) and creates a 3D toy glyph:
    - Soft bottom ambient drop shadow
    - Crisp white/cream body with subtle vertical gradient
    - Top specular highlight
    - Clean composition onto baseplate_orig
    """
    res = baseplate_orig.copy()
    
    # 1. Shadow layer
    shadow_mask = glyph_alpha_mask.filter(ImageFilter.GaussianBlur(shadow_blur))
    shadow_img = Image.new('RGBA', (w, h), (10, 25, 75, 180))
    # offset shadow
    shadow_shifted = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    shadow_shifted.paste(shadow_img, shadow_offset, mask=shadow_mask)
    res.alpha_composite(shadow_shifted)
    
    # 2. 3D Body (with vertical subtle shading: top slightly warmer white, bottom slightly cooler light blue/gray)
    body_img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    # Create gradient for body
    gradient = np.zeros((h, w, 4), dtype=np.uint8)
    for y in range(h):
        # subtle factor from 1.0 (top) to 0.88 (bottom)
        factor = 1.0 - 0.12 * (y / h)
        r = int(glyph_color[0] * factor)
        g = int(glyph_color[1] * factor)
        b = int(min(255, glyph_color[2] * (factor + 0.05)))
        gradient[y, :, :] = [r, g, b, 255]
    grad_im = Image.fromarray(gradient, 'RGBA')
    
    # Composite body with mask
    res.paste(grad_im, (0, 0), mask=glyph_alpha_mask)
    
    # 3. Top rim highlight (shifted 3px up)
    # Mask diff between mask and mask shifted down gives top rim!
    shifted_down = Image.new('L', (w, h), 0)
    shifted_down.paste(glyph_alpha_mask, (0, 4))
    
    # top_rim = glyph_alpha_mask - shifted_down
    m_arr = np.array(glyph_alpha_mask, dtype=float)
    s_arr = np.array(shifted_down, dtype=float)
    rim_arr = np.clip(m_arr - s_arr, 0, 255).astype(np.uint8)
    rim_mask = Image.fromarray(rim_arr, 'L').filter(ImageFilter.GaussianBlur(1.5))
    
    rim_im = Image.new('RGBA', (w, h), (255, 255, 255, 220))
    res.paste(rim_im, (0, 0), mask=rim_mask)
    
    return res

# -------------------------------------------------------------------------
# A. Circular Restart / Retry Arrow (↺)
# -------------------------------------------------------------------------
retry_mask = Image.new('L', (w, h), 0)
draw_r = ImageDraw.Draw(retry_mask)

# Center of button
cx, cy = 256, 240
radius = 115
thickness = 38

# Draw 270-degree arc from 60 deg to 330 deg (clockwise opening at top-right)
# In PIL, 0 is 3 o'clock. We want arrow starting at top-right and circling around.
# Let's draw high-res arc using supersampling (1024x1024)
ss_w, ss_h = 1024, 1024
ss_mask = Image.new('L', (ss_w, ss_h), 0)
ss_draw = ImageDraw.Draw(ss_mask)
scx, scy = 512, 480
s_r = radius * 2
s_th = thickness * 2

# Arc bbox
bbox_arc = [scx - s_r, scy - s_r, scx + s_r, scy + s_r]
# Arc angles: from -135 to 110 degrees
ss_draw.arc(bbox_arc, start=-150, end=115, fill=255, width=s_th)

# Arrow head at the end (around -150 deg, near top-left pointing right/down)
# Or standard restart arrow: circling clockwise, arrow head at top pointing right
# Let's use clean geometry:
# Arc from -45 to 225 deg (circling clockwise) with arrow head at top:
ss_mask = Image.new('L', (ss_w, ss_h), 0)
ss_draw = ImageDraw.Draw(ss_mask)

# Arc from 50 deg to 300 deg (clockwise: angle 0 is right, 90 is bottom, 180 is left, 270 is top)
# In PIL angles are measured clockwise from 3 o'clock!
# Start at 270 (top), sweep clockwise to 180 (left) -> 270 to 180+360=540 (full 270 deg)
ss_draw.arc(bbox_arc, start=280, end=540, fill=255, width=s_th)

# Arrow head at top: at angle 280 deg, pointing clockwise (down-right)
rad_tip = math.radians(280)
tip_x = scx + s_r * math.cos(rad_tip)
tip_y = scy + s_r * math.sin(rad_tip)

# Arrow head triangle
head_size = 90
# Tip direction is tangent to circle: perpendicular to radius vector (cos, sin)
# Tangent pointing clockwise: (-sin, cos)
tang_x = -math.sin(rad_tip)
tang_y = math.cos(rad_tip)
norm_x = math.cos(rad_tip)
norm_y = math.sin(rad_tip)

p_tip = (tip_x + tang_x * 45, tip_y + tang_y * 45)
p_left = (tip_x - tang_x * 45 - norm_x * 55, tip_y - tang_y * 45 - norm_y * 55)
p_right = (tip_x - tang_x * 45 + norm_x * 55, tip_y - tang_y * 45 + norm_y * 55)

ss_draw.polygon([p_tip, p_left, p_right], fill=255)

retry_mask = ss_mask.resize((w, h), Image.Resampling.LANCZOS)
btn_retry = add_glyph_to_baseplate(retry_mask)
btn_retry.save(os.path.join(scratch_dir, 'btn_marine_retry_new.png'))
btn_retry.save(os.path.join(ui_dir, 'btn_marine_retry.png'))

# -------------------------------------------------------------------------
# B. Settings Gear (⚙️) - Recognizable & Crisp
# -------------------------------------------------------------------------
gear_mask = Image.new('L', (ss_w, ss_h), 0)
gear_draw = ImageDraw.Draw(gear_mask)

num_teeth = 6
outer_r = 190
inner_r = 145
hole_r = 62

# Create gear polygon
points = []
for i in range(num_teeth * 2):
    angle_base = (i * math.pi / num_teeth)
    if i % 2 == 0:
        # Tooth outer flat
        a1 = angle_base - 0.16
        a2 = angle_base + 0.16
        points.append((scx + outer_r * math.cos(a1), scy + outer_r * math.sin(a1)))
        points.append((scx + outer_r * math.cos(a2), scy + outer_r * math.sin(a2)))
    else:
        # Tooth valley
        a1 = angle_base - 0.18
        a2 = angle_base + 0.18
        points.append((scx + inner_r * math.cos(a1), scy + inner_r * math.sin(a1)))
        points.append((scx + inner_r * math.cos(a2), scy + inner_r * math.sin(a2)))

gear_draw.polygon(points, fill=255)
# Cut center hole
gear_draw.ellipse([scx - hole_r, scy - hole_r, scx + hole_r, scy + hole_r], fill=0)

gear_mask_res = gear_mask.resize((w, h), Image.Resampling.LANCZOS)
btn_settings = add_glyph_to_baseplate(gear_mask_res)
btn_settings.save(os.path.join(scratch_dir, 'btn_marine_settings_new.png'))
btn_settings.save(os.path.join(ui_dir, 'btn_marine_settings.png'))

# -------------------------------------------------------------------------
# C. Speed State Indicators (Pills)
# -------------------------------------------------------------------------
# Dimensions: 342 x 156 (3x supersample for crisp 114x52 UI pill)
pw, ph = 342, 156

def create_speed_pill(mode_text, is_turbo=False):
    pill = Image.new('RGBA', (pw, ph), (0, 0, 0, 0))
    pdraw = ImageDraw.Draw(pill)
    
    corner_r = ph // 2 - 4
    bbox_shadow = [6, 12, pw - 6, ph - 2]
    bbox_outer = [6, 6, pw - 6, ph - 8]
    bbox_inner = [10, 10, pw - 10, ph - 12]
    
    # 1. Soft Shadow
    shadow_layer = Image.new('RGBA', (pw, ph), (0, 0, 0, 0))
    s_draw = ImageDraw.Draw(shadow_layer)
    s_draw.rounded_rectangle(bbox_shadow, radius=corner_r, fill=(10, 25, 60, 140))
    shadow_layer = shadow_layer.filter(ImageFilter.GaussianBlur(5))
    pill.alpha_composite(shadow_layer)
    
    # 2. Outer Border & Gradient
    if not is_turbo:
        # 1X Mode: Calm Deep Marine Navy with Cyan Accent
        border_col = (14, 45, 95, 255)
        top_grad = (26, 82, 158)
        bot_grad = (12, 40, 88)
        inner_rim = (80, 160, 240, 180)
        text_col = (255, 255, 255)
        accent_col = (56, 189, 248) # bright cyan
    else:
        # 2X Mode: High-Energy Vibrant Amber / Gold Turbo
        border_col = (140, 60, 0, 255)
        top_grad = (245, 158, 11)
        bot_grad = (180, 83, 9)
        inner_rim = (254, 240, 138, 220)
        text_col = (255, 255, 255)
        accent_col = (254, 243, 199)
        
    pdraw.rounded_rectangle(bbox_outer, radius=corner_r, fill=border_col)
    
    # Gradient body
    body = Image.new('RGBA', (pw, ph), (0, 0, 0, 0))
    bdraw = ImageDraw.Draw(body)
    bdraw.rounded_rectangle(bbox_inner, radius=corner_r - 4, fill=top_grad)
    # Apply vertical gradient
    arr = np.array(body)
    mask = arr[:, :, 3] > 0
    for y in range(ph):
        t = y / ph
        r = int(top_grad[0] * (1 - t) + bot_grad[0] * t)
        g = int(top_grad[1] * (1 - t) + bot_grad[1] * t)
        b = int(top_grad[2] * (1 - t) + bot_grad[2] * t)
        arr[y, mask[y, :], 0] = r
        arr[y, mask[y, :], 1] = g
        arr[y, mask[y, :], 2] = b
    body = Image.fromarray(arr, 'RGBA')
    pill.alpha_composite(body)
    
    # Top rim highlight
    rim = Image.new('RGBA', (pw, ph), (0, 0, 0, 0))
    rdraw = ImageDraw.Draw(rim)
    rdraw.rounded_rectangle([14, 12, pw - 14, 28], radius=8, fill=(255, 255, 255, 90))
    pill.alpha_composite(rim)
    
    # Text and Icon
    # Load bold font
    try:
        font = ImageFont.truetype('arialbd.ttf', 56)
        font_sub = ImageFont.truetype('arialbd.ttf', 38)
    except:
        font = font_sub = ImageFont.load_default()
        
    pdraw = ImageDraw.Draw(pill)
    
    # Icon (⏩ for 1X, ⚡ for 2X)
    text_y = ph // 2 - 3
    if not is_turbo:
        # Draw double chevron (⏩)
        # Left chevron
        cx1 = 70
        pdraw.polygon([(cx1 - 18, text_y - 24), (cx1 + 2, text_y), (cx1 - 18, text_y + 24), (cx1 - 28, text_y + 24), (cx1 - 8, text_y), (cx1 - 28, text_y - 24)], fill=accent_col)
        # Right chevron
        cx2 = cx1 + 24
        pdraw.polygon([(cx2 - 18, text_y - 24), (cx2 + 2, text_y), (cx2 - 18, text_y + 24), (cx2 - 28, text_y + 24), (cx2 - 8, text_y), (cx2 - 28, text_y - 24)], fill=accent_col)
        
        # Text "1X"
        # Shadow
        pdraw.text((205, text_y + 4), '1X', fill=(10, 30, 70, 200), font=font, anchor='mm')
        pdraw.text((205, text_y), '1X', fill=(255, 255, 255), font=font, anchor='mm')
    else:
        # Draw Bolt (⚡)
        bx = 72
        pdraw.polygon([
            (bx + 6, text_y - 32),
            (bx - 16, text_y - 2),
            (bx - 2, text_y - 2),
            (bx - 10, text_y + 32),
            (bx + 18, text_y + 2),
            (bx + 4, text_y + 2)
        ], fill=(255, 255, 255))
        
        # Text "2X"
        pdraw.text((205, text_y + 4), '2X', fill=(100, 30, 0, 200), font=font, anchor='mm')
        pdraw.text((205, text_y), '2X', fill=(255, 255, 255), font=font, anchor='mm')
        
    return pill

speed_1x = create_speed_pill('1X', is_turbo=False)
speed_2x = create_speed_pill('2X', is_turbo=True)

speed_1x.save(os.path.join(scratch_dir, 'indicator_speed_1x.png'))
speed_1x.save(os.path.join(ui_dir, 'btn_marine_speed_1x.png'))
speed_2x.save(os.path.join(scratch_dir, 'indicator_speed_2x.png'))
speed_2x.save(os.path.join(ui_dir, 'btn_marine_speed_2x.png'))

# -------------------------------------------------------------------------
# D. Sleek Level Capsule (Horizontal Stadium Pill)
# -------------------------------------------------------------------------
# Native dimensions: 660 x 140 (rendered at 330 x 70 in UI)
lw, lh = 660, 140
level_capsule = Image.new('RGBA', (lw, lh), (0, 0, 0, 0))

# 1. Soft shadow
l_shadow = Image.new('RGBA', (lw, lh), (0, 0, 0, 0))
ls_draw = ImageDraw.Draw(l_shadow)
ls_draw.rounded_rectangle([10, 14, lw - 10, lh - 2], radius=(lh - 16) // 2, fill=(10, 25, 60, 140))
l_shadow = l_shadow.filter(ImageFilter.GaussianBlur(6))
level_capsule.alpha_composite(l_shadow)

# 2. Outer Border (Deep navy toy plastic)
ldraw = ImageDraw.Draw(level_capsule)
ldraw.rounded_rectangle([8, 8, lw - 8, lh - 10], radius=(lh - 18) // 2, fill=(14, 45, 100, 255))

# 3. Inner gradient body
lbody = Image.new('RGBA', (lw, lh), (0, 0, 0, 0))
lb_draw = ImageDraw.Draw(lbody)
lb_draw.rounded_rectangle([14, 14, lw - 14, lh - 16], radius=(lh - 30) // 2, fill=(30, 90, 185))

# Apply gradient (rich ocean blue)
l_arr = np.array(lbody)
l_mask = l_arr[:, :, 3] > 0
top_c = (32, 98, 204)
bot_c = (14, 48, 120)
for y in range(lh):
    t = y / lh
    r = int(top_c[0] * (1 - t) + bot_c[0] * t)
    g = int(top_c[1] * (1 - t) + bot_c[1] * t)
    b = int(top_c[2] * (1 - t) + bot_c[2] * t)
    l_arr[y, l_mask[y, :], 0] = r
    l_arr[y, l_mask[y, :], 1] = g
    l_arr[y, l_mask[y, :], 2] = b
level_capsule.alpha_composite(Image.fromarray(l_arr, 'RGBA'))

# 4. Top specular gloss rim
l_gloss = Image.new('RGBA', (lw, lh), (0, 0, 0, 0))
lg_draw = ImageDraw.Draw(l_gloss)
lg_draw.rounded_rectangle([24, 18, lw - 24, 38], radius=10, fill=(255, 255, 255, 80))
level_capsule.alpha_composite(l_gloss)

# 5. Flanking nautical diamonds (decorative subtle toy accents)
ldraw = ImageDraw.Draw(level_capsule)
mid_y = lh // 2 - 2
def draw_diamond(cx, cy, sz, col):
    ldraw.polygon([(cx, cy - sz), (cx + sz, cy), (cx, cy + sz), (cx - sz, cy)], fill=col)

draw_diamond(60, mid_y, 10, (147, 197, 253, 220))
draw_diamond(lw - 60, mid_y, 10, (147, 197, 253, 220))

level_capsule.save(os.path.join(scratch_dir, 'bg_level_capsule_sleek.png'))
level_capsule.save(os.path.join(ui_dir, 'bg_level_capsule.png'))
level_capsule.save(os.path.join(ui_dir, 'bg_level_marine.png'))

print('All unified UI assets successfully generated!')
