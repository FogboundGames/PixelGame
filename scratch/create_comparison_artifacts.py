from PIL import Image, ImageDraw, ImageFont
import numpy as np

# Load crops
before = Image.open(r'C:\Users\ezgid\.gemini\antigravity-ide\brain\63c02eea-87da-4b2d-ae5e-b8879530b435\final_gameplay_full.png')
after = Image.open('scratch/gameplay_view_9_16.png')

# Slots bounding box: (0, 1110, 1080, 1270) -> height 160
crop_box = (0, 1110, 1080, 1270)
b_crop = before.crop(crop_box)
a_crop = after.crop(crop_box)

# 1. Stacked full row comparison (width 1080, height ~420 with header banners)
w, h = b_crop.size # 1080, 160
banner_h = 44
pad = 12
total_w = w
total_h = (h + banner_h) * 2 + pad

comp = Image.new('RGB', (total_w, total_h), (18, 22, 32))
draw = ImageDraw.Draw(comp)

# Try loading standard font or default
try:
    font_title = ImageFont.truetype("arialbd.ttf", 22)
    font_sub = ImageFont.truetype("arial.ttf", 18)
except:
    font_title = ImageFont.load_default()
    font_sub = ImageFont.load_default()

# --- TOP: BEFORE ---
# Banner
draw.rectangle([(0, 0), (total_w, banner_h)], fill=(40, 44, 55))
draw.rectangle([(0, banner_h - 2), (total_w, banner_h)], fill=(120, 120, 130))
draw.text((20, 10), "ÖNCE (Soluk & Açık Renkli Simitler)", fill=(220, 220, 225), font=font_title)
comp.paste(b_crop, (0, banner_h))

# --- BOTTOM: AFTER ---
y_after_banner = banner_h + h + pad
draw.rectangle([(0, y_after_banner), (total_w, y_after_banner + banner_h)], fill=(28, 48, 65))
draw.rectangle([(0, y_after_banner + banner_h - 2), (total_w, y_after_banner + banner_h)], fill=(255, 58, 155))
draw.text((20, y_after_banner + 10), "SONRA (Daha Canlı & Doygun Renkler - Küpler ve Gemilerle Uyumlu)", fill=(255, 200, 235), font=font_title)
comp.paste(a_crop, (0, y_after_banner + banner_h))

comp_path = r'C:\Users\ezgid\.gemini\antigravity-ide\brain\63c02eea-87da-4b2d-ae5e-b8879530b435\slots_vibrancy_comparison.png'
comp.save(comp_path)
print("Saved slots_vibrancy_comparison.png")

# 2. Close-up Zoom Comparison of Middle Lifebuoy
# Middle slot is roughly x=450 to 630
middle_box = (455, 1115, 625, 1265)
b_mid = before.crop(middle_box).resize((340, 300), Image.Resampling.LANCZOS)
a_mid = after.crop(middle_box).resize((340, 300), Image.Resampling.LANCZOS)

mid_w = 340 * 2 + 30
mid_h = 300 + 70
mid_comp = Image.new('RGB', (mid_w, mid_h), (20, 24, 34))
mid_draw = ImageDraw.Draw(mid_comp)

# Headers
mid_draw.text((20, 15), "ÖNCE (Soluk Pembe)", fill=(180, 180, 190), font=font_title)
mid_draw.text((340 + 30 + 10, 15), "SONRA (Canlı Simit)", fill=(255, 100, 190), font=font_title)

# Frames
mid_comp.paste(b_mid, (10, 55))
mid_comp.paste(a_mid, (340 + 20, 55))
mid_draw.rectangle([(9, 54), (350, 356)], outline=(80, 85, 100), width=2)
mid_draw.rectangle([(340 + 19, 54), (mid_w - 9, 356)], outline=(255, 58, 155), width=2)

zoom_path = r'C:\Users\ezgid\.gemini\antigravity-ide\brain\63c02eea-87da-4b2d-ae5e-b8879530b435\slots_vibrant_zoom.png'
mid_comp.save(zoom_path)
print("Saved slots_vibrant_zoom.png")
