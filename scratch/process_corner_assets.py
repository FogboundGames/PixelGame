import os
import math
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance

os.makedirs("Assets/UI", exist_ok=True)

# 1. Process Launch Button
btn_path = r"C:\Users\ezgid\.gemini\antigravity-ide\brain\ff61f5bd-286c-4f54-b132-3cc977bbe0ef\arcade_launch_button_1790004791276.jpg"
if os.path.exists(btn_path):
    img = Image.open(btn_path).convert("RGBA")
    w, h = img.size
    cx, cy = w / 2, h / 2
    r = min(w, h) * 0.46  # radius of the outer silver bezel
    
    # Create high-res circular mask with anti-aliasing
    scale = 4
    mask = Image.new("L", (w * scale, h * scale), 0)
    draw = ImageDraw.Draw(mask)
    draw.ellipse((cx * scale - r * scale, cy * scale - r * scale, cx * scale + r * scale, cy * scale + r * scale), fill=255)
    mask = mask.resize((w, h), Image.Resampling.LANCZOS)
    
    btn_out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    btn_out.paste(img, (0, 0), mask=mask)
    
    # Crop to bounding box of circle
    bbox = mask.getbbox()
    if bbox:
        btn_out = btn_out.crop(bbox)
        # Resize to crisp 512x512
        btn_out = btn_out.resize((512, 512), Image.Resampling.LANCZOS)
        btn_out.save("Assets/UI/Corner_LaunchButton.png", "PNG")
        print("Saved Assets/UI/Corner_LaunchButton.png (512x512)")

# 2. Create Neon Cyan Ring Asset for pulsing / breathing
ring_size = 512
ring_img = Image.new("RGBA", (ring_size, ring_size), (0, 0, 0, 0))
ring_draw = ImageDraw.Draw(ring_img)

# Outer glow
for i in range(20):
    alpha = int(120 * math.exp(-i / 5.0))
    rad = 190 + i * 2
    ring_draw.ellipse((256 - rad, 256 - rad, 256 + rad, 256 + rad), outline=(0, 240, 255, alpha), width=3)

# Inner bright core
ring_draw.ellipse((256 - 190, 256 - 190, 256 + 190, 256 + 190), outline=(180, 255, 255, 255), width=6)
ring_draw.ellipse((256 - 192, 256 - 192, 256 + 192, 256 + 192), outline=(0, 225, 255, 230), width=4)
ring_img = ring_img.filter(ImageFilter.GaussianBlur(1))
ring_img.save("Assets/UI/Corner_NeonRing.png", "PNG")
print("Saved Assets/UI/Corner_NeonRing.png")

# 3. Create Shockwave Ripple Ring Asset for button press punch
shock_size = 512
shock_img = Image.new("RGBA", (shock_size, shock_size), (0, 0, 0, 0))
shock_draw = ImageDraw.Draw(shock_img)
for i in range(12):
    a = int(220 * (1.0 - i / 12.0))
    rad = 160 + i
    shock_draw.ellipse((256 - rad, 256 - rad, 256 + rad, 256 + rad), outline=(255, 255, 255, a), width=2)
shock_img.save("Assets/UI/Corner_Shockwave.png", "PNG")
print("Saved Assets/UI/Corner_Shockwave.png")

# 4. Create Curved Corner Hub Baseplate (Toy Blue + Chrome Bevel)
base_size = 512
base_img = Image.new("RGBA", (base_size, base_size), (0, 0, 0, 0))
base_draw = ImageDraw.Draw(base_img)

# Rounded rectangular corner platform with gradient & bevel
# Top-left to bottom-right curved corner plate
for r_off in range(40, 0, -1):
    # Shadow/glow layer
    alpha = int(80 * (40 - r_off) / 40.0)
    base_draw.rounded_rectangle([30 - r_off//2, 30 - r_off//2, 482 + r_off//2, 482 + r_off//2], radius=110, fill=(10, 18, 45, alpha))

# Main body plate - Royal Toy Blue (#1C4ED8 to #2563EB)
base_draw.rounded_rectangle([36, 36, 476, 476], radius=95, fill=(28, 78, 216, 255), outline=(78, 140, 246, 255), width=8)
# Inner bevel rim
base_draw.rounded_rectangle([48, 48, 464, 464], radius=85, fill=(18, 54, 168, 255), outline=(147, 197, 253, 200), width=4)
# Subtle top highlight
base_draw.rounded_rectangle([54, 54, 458, 140], radius=40, fill=(59, 130, 246, 120))

base_img.save("Assets/UI/Corner_BasePlate.png", "PNG")
print("Saved Assets/UI/Corner_BasePlate.png")
