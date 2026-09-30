import os
from PIL import Image

ref_path = r'C:/Users/ezgid/.gemini/antigravity-ide/brain/cc396c31-feb2-429d-9102-5ba578c6e840/.user_uploaded/media_1790801172026.jpg'
bg_path = r'Assets/Kenney/BeachBackground_Clean.png'

ref = Image.open(ref_path).convert('RGBA')
bg = Image.open(bg_path).convert('RGBA')

# In ref (682 x 1024):
# Wooden sign box:
# x: ~205 to ~475, y: ~685 to ~755
# Let's crop with a bit of margin
sign_crop = ref.crop((205, 688, 477, 755))

# Scale to match 1080x1920 (scale factor ~ 1080/682 = 1.58357)
scale = 1080.0 / 682.0
new_w = int(round(sign_crop.width * scale))
new_h = int(round(sign_crop.height * scale))

sign_scaled = sign_crop.resize((new_w, new_h), Image.Resampling.LANCZOS)

# Target center on 1080x1920
# y_center in ref was ~ 721.5 -> in 1920: 721.5 * (1920/1024) = 1352.8
target_x = int(round((1080 - new_w) / 2.0))
target_y = int(round(1353 - new_h / 2.0))

print(f"Pasting sign at ({target_x}, {target_y}) size ({new_w}, {new_h})")

# Blend with soft edge mask to seamlessly merge with sand
mask = Image.new('L', (new_w, new_h), 255)
from PIL import ImageDraw
draw = ImageDraw.Draw(mask)
# Soft feather on outermost 3 pixels
for i in range(3):
    alpha = int(255 * (i + 1) / 4.0)
    draw.rectangle([i, i, new_w - 1 - i, new_h - 1 - i], outline=alpha)

bg.paste(sign_scaled, (target_x, target_y), sign_scaled)
bg.save(bg_path)
print("Updated BeachBackground_Clean.png with original crisp pixel-art CARD DECK banner!")
