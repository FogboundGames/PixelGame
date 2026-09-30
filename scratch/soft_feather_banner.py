import os
from PIL import Image, ImageFilter

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

# Create an alpha mask with 5px soft edge feather
mask = Image.new('L', (w, h), 0)
from PIL import ImageDraw
draw = ImageDraw.Draw(mask)
draw.rectangle((5, 5, w - 6, h - 6), fill=255)
mask = mask.filter(ImageFilter.GaussianBlur(3))

target_x = int(round(190 * scale_x))
target_y = int(round(680 * scale_y))

bg.paste(banner_scaled, (target_x, target_y), mask)
bg.save(dest_bg)

# Verification crop
check = bg.crop((260, 1240, 820, 1460))
check.save('scratch/banner_feathered.png')
print("Soft feathered banner saved successfully!")
