import os
from PIL import Image

ref_path = r'C:/Users/ezgid/.gemini/antigravity-ide/brain/cc396c31-feb2-429d-9102-5ba578c6e840/.user_uploaded/media_1790801172026.jpg'
clean_hd = r'scratch/beach_new_reference_hd.png'
dest_bg = r'Assets/Kenney/BeachBackground_Clean.png'

ref = Image.open(ref_path).convert('RGBA')
bg = Image.open(clean_hd).convert('RGBA')

# Crop the full banner with generous border
banner_crop = ref.crop((190, 680, 492, 765))

scale_x = 1080.0 / 682.0
scale_y = 1920.0 / 1024.0

w = int(round(banner_crop.width * scale_x))
h = int(round(banner_crop.height * scale_y))

banner_scaled = banner_crop.resize((w, h), Image.Resampling.LANCZOS)

target_x = int(round(190 * scale_x))
target_y = int(round(680 * scale_y))

print(f"Pasting banner at ({target_x}, {target_y}) size ({w}, {h})")
bg.paste(banner_scaled, (target_x, target_y))
bg.save(dest_bg)

# Also save a crop of the area to verify
check = bg.crop((260, 1240, 820, 1460))
check.save('scratch/banner_verified.png')
print("Successfully updated background and saved verification crop!")
