import numpy as np
from PIL import Image, ImageDraw, ImageFilter

def create_slot_texture():
    size = 512
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    
    margin = 36
    rect = [margin, margin, size - margin, size - margin]
    radius = 92  # Exactly matches the smooth rounded corners of Image 2
    
    # 1. Soft ambient drop shadow underneath
    shadow_img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    shadow_draw = ImageDraw.Draw(shadow_img)
    shadow_rect = [margin, margin + 6, size - margin, size - margin + 6]
    shadow_draw.rounded_rectangle(shadow_rect, radius=radius, fill=(0, 0, 0, 60))
    shadow_img = shadow_img.filter(ImageFilter.GaussianBlur(16))
    img.paste(shadow_img, (0, 0), shadow_img)
    
    # 2. Main Slot Pad (clean white / off-white)
    pad_img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    pad_draw = ImageDraw.Draw(pad_img)
    
    # Base fill - clean crisp white
    pad_draw.rounded_rectangle(rect, radius=radius, fill=(255, 255, 255, 255))
    
    # 3. Soft outer border rim (subtle light gray)
    border_color = (205, 212, 222, 255)
    pad_draw.rounded_rectangle(rect, radius=radius, outline=border_color, width=4)
    
    # 4. Subtle inner bevel highlight
    inner_rect = [margin + 2, margin + 2, size - margin - 2, size - margin - 2]
    inner_radius = radius - 2
    pad_draw.rounded_rectangle(inner_rect, radius=inner_radius, outline=(248, 250, 252, 255), width=2)
    
    img.paste(pad_img, (0, 0), pad_img)
    
    out_path = "Assets/Textures/Marina/slot_rounded_square.png"
    img.save(out_path, "PNG")
    print(f"Updated slot texture to {out_path}")

if __name__ == "__main__":
    create_slot_texture()
