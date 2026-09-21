import math
from PIL import Image, ImageDraw, ImageFilter

size = 512
# Padding around the shadow shape so blur has room to fade to zero at edges
pad_x = 64
pad_y = 96
rad = 90

# Create grayscale mask for alpha
mask = Image.new("L", (size, size), 0)
draw = ImageDraw.Draw(mask)

# Rounded rectangle with pebble/tile aspect ratio (slightly wider than tall)
draw.rounded_rectangle([pad_x, pad_y + 20, size - pad_x, size - pad_y + 20], radius=rad, fill=255)

# Layered Gaussian blur for realistic soft contact shadow
b1 = mask.filter(ImageFilter.GaussianBlur(16))
b2 = mask.filter(ImageFilter.GaussianBlur(32))
b3 = mask.filter(ImageFilter.GaussianBlur(56))

final_mask = Image.new("L", (size, size), 0)
final_mask = Image.blend(b3, b2, 0.55)
final_mask = Image.blend(final_mask, b1, 0.45)

# Soft contrast curve - not harsh black, but a natural, velvety ambient occlusion gradient
pixels = final_mask.load()
for y in range(size):
    for x in range(size):
        v = pixels[x, y] / 255.0
        # Smooth gentle falloff
        nv = math.pow(v, 0.9) * 0.95
        pixels[x, y] = min(255, int(nv * 255))

# Create pure white RGB texture with the soft alpha mask
out = Image.new("RGBA", (size, size), (255, 255, 255, 0))
out.putalpha(final_mask)
out.save("Assets/UI/PoolSlot_Shadow.png", "PNG")
print("Saved harmonious soft Assets/UI/PoolSlot_Shadow.png")
