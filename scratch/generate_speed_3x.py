import os
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ui_dir = 'Assets/UI/CasualUI'
scratch_dir = 'scratch'

# 1. Load baseplate
baseplate_orig = Image.open(os.path.join(ui_dir, 'btn_marine_blank.png')).convert('RGBA')
w, h = baseplate_orig.size

# Load font
font_path = os.path.join(scratch_dir, 'TitanOne.ttf')
if not os.path.exists(font_path):
    font_path = 'C:/Windows/Fonts/arialbd.ttf'

font = ImageFont.truetype(font_path, 210)

def create_3x_button(top_color=(255, 110, 80), bot_color=(230, 35, 55), rim_color=(255, 220, 220)):
    # 2x supersampling for ultra crispness
    ss_w, ss_h = w * 2, h * 2
    ss_font = ImageFont.truetype(font_path, 420)
    
    # Measure "3X"
    ss_mask = Image.new('L', (ss_w, ss_h), 0)
    draw_ss = ImageDraw.Draw(ss_mask)
    
    bbox = ss_font.getbbox('3X')
    bw = bbox[2] - bbox[0]
    bh = bbox[3] - bbox[1]
    
    # Center position
    cx = ss_w // 2 - (bbox[0] + bbox[2]) // 2
    cy = int(ss_h * 0.46) - (bbox[1] + bbox[3]) // 2
    
    draw_ss.text((cx, cy), '3X', font=ss_font, fill=255)
    
    # Resize down with Lanczos to 512x512
    glyph_mask = ss_mask.resize((w, h), Image.Resampling.LANCZOS)
    
    res = baseplate_orig.copy()
    
    # 1. Ambient Drop Shadow (subtle deep dark navy-red shadow)
    shadow_mask = glyph_mask.filter(ImageFilter.GaussianBlur(8))
    shadow_img = Image.new('RGBA', (w, h), (30, 10, 40, 210))
    shadow_shifted = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    shadow_shifted.paste(shadow_img, (0, 9), mask=shadow_mask)
    res.alpha_composite(shadow_shifted)
    
    # 2. 3D Body with vertical gradient
    gradient = np.zeros((h, w, 4), dtype=np.uint8)
    for y in range(h):
        factor = y / h
        r = int(top_color[0] * (1.0 - factor) + bot_color[0] * factor)
        g = int(top_color[1] * (1.0 - factor) + bot_color[1] * factor)
        b = int(top_color[2] * (1.0 - factor) + bot_color[2] * factor)
        gradient[y, :, :] = [r, g, b, 255]
    grad_im = Image.fromarray(gradient, 'RGBA')
    
    res.paste(grad_im, (0, 0), mask=glyph_mask)
    
    # 3. Top rim specular highlight (inner bevel)
    shifted_down = Image.new('L', (w, h), 0)
    shifted_down.paste(glyph_mask, (0, 5))
    
    m_arr = np.array(glyph_mask, dtype=float)
    s_arr = np.array(shifted_down, dtype=float)
    rim_arr = np.clip(m_arr - s_arr, 0, 255).astype(np.uint8)
    rim_mask = Image.fromarray(rim_arr, 'L').filter(ImageFilter.GaussianBlur(1.8))
    
    rim_im = Image.new('RGBA', (w, h), (rim_color[0], rim_color[1], rim_color[2], 225))
    res.paste(rim_im, (0, 0), mask=rim_mask)
    
    return res

btn_3x = create_3x_button()

# Save both circular and square paths
out_3x = os.path.join(ui_dir, 'btn_marine_speed_3x.png')
out_3x_sq = os.path.join(ui_dir, 'btn_marine_speed_3x_square.png')

btn_3x.save(out_3x)
btn_3x.save(out_3x_sq)
print(f'Successfully generated {out_3x} and {out_3x_sq}')
