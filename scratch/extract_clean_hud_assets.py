import os
from PIL import Image, ImageDraw

ref_path = r'C:/Users/ezgid/.gemini/antigravity-ide/brain/cc396c31-feb2-429d-9102-5ba578c6e840/.user_uploaded/media_1790801172026.jpg'
im = Image.open(ref_path).convert('RGBA')

# Precise bounding boxes for the 4 HUD items
# Let's inspect coordinates precisely
# Sound button:
# x: ~508 to ~576, y: ~22 to ~90 (width ~68, height ~68)
sound_crop = im.crop((508, 22, 576, 90))
settings_crop = im.crop((590, 22, 658, 90))

# Level capsule:
# x: ~220 to ~460, y: ~23 to ~88 (width ~240, height ~65)
level_crop = im.crop((220, 23, 460, 88))

# Coin pill:
# x: ~20 to ~195, y: ~26 to ~84 (width ~175, height ~58)
coin_crop = im.crop((20, 26, 195, 84))

def make_clean_rounded_mask(img, radius=16):
    # Create an alpha mask with rounded corners
    w, h = img.size
    mask = Image.new('L', (w, h), 0)
    draw = ImageDraw.Draw(mask)
    draw.rounded_rectangle((1, 1, w - 2, h - 2), radius=radius, fill=255)
    
    # Also do color-based keying on the 4 extreme corners if background is sand/cyan
    result = img.copy()
    result.putalpha(mask)
    return result

sound_clean = make_clean_rounded_mask(sound_crop, radius=18)
settings_clean = make_clean_rounded_mask(settings_crop, radius=18)
level_clean = make_clean_rounded_mask(level_crop, radius=28)
coin_clean = make_clean_rounded_mask(coin_crop, radius=26)

out_dir = r'Assets/UI/CasualUI'
sound_clean.save(os.path.join(out_dir, 'btn_sound_ref.png'))
settings_clean.save(os.path.join(out_dir, 'btn_settings_ref.png'))
level_clean.save(os.path.join(out_dir, 'bg_level_ref.png'))
coin_clean.save(os.path.join(out_dir, 'bg_coin_ref.png'))

print("All reference HUD assets extracted and saved to Assets/UI/CasualUI!")
