import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter

os.makedirs("Assets/Textures/Marina", exist_ok=True)

# 1. Buoy Striped Texture (256x256)
def create_buoy_texture():
    w, h = 256, 256
    img = Image.new("RGBA", (w, h), (255, 255, 255, 255))
    draw = ImageDraw.Draw(img)
    
    red_color = (225, 42, 36, 255)
    white_color = (250, 250, 252, 255)
    dark_trim = (180, 25, 20, 255)
    rubber_trim = (45, 45, 50, 255)
    
    # UV mapping assumes Y is vertical along the buoy
    # 0 - 28%: Red cap
    # 28% - 31%: dark rubber ring
    # 31% - 69%: Crisp white center band
    # 69% - 72%: dark rubber ring
    # 72% - 100%: Red bottom
    
    draw.rectangle([0, 0, w, int(h * 0.28)], fill=red_color)
    draw.rectangle([0, int(h * 0.28), w, int(h * 0.31)], fill=rubber_trim)
    draw.rectangle([0, int(h * 0.31), w, int(h * 0.69)], fill=white_color)
    draw.rectangle([0, int(h * 0.69), w, int(h * 0.72)], fill=rubber_trim)
    draw.rectangle([0, int(h * 0.72), w, h], fill=red_color)
    
    # Soft vertical shading / ambient occlusion gradient across X (subtle cylinder shading)
    for x in range(w):
        # subtle vignette on edges
        factor = 1.0 - 0.18 * ((x - w/2) / (w/2))**2
        # apply subtly
    
    img.save("Assets/Textures/Marina/Buoy_Striped_Tex.png")
    print("Saved Buoy_Striped_Tex.png")

# 2. Wooden Sign Textures for Slots 1-5 (512x512)
def create_slot_signs():
    font_path = "Assets/Fonts/LilitaOne-Regular.ttf"
    font_size = 230
    font = ImageFont.truetype(font_path, font_size)
    
    wood_bg = (165, 102, 65, 255)
    wood_dark = (120, 70, 42, 255)
    wood_light = (195, 125, 85, 255)
    border_dark = (75, 42, 25, 255)
    text_color = (255, 250, 235, 255)
    text_shadow = (48, 25, 15, 255)
    rivet_color = (80, 80, 85, 255)
    rivet_hi = (150, 150, 155, 255)
    
    for slot_num in range(1, 6):
        w, h = 512, 512
        img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        draw = ImageDraw.Draw(img)
        
        # Rounded wooden sign board in center
        margin_x, margin_y = 48, 72
        rw, rh = w - 2 * margin_x, h - 2 * margin_y
        
        # Outer dark border
        draw.rounded_rectangle([margin_x, margin_y, w - margin_x, h - margin_y], radius=32, fill=border_dark)
        # Inner wood fill
        bw = 14
        draw.rounded_rectangle([margin_x + bw, margin_y + bw, w - margin_x - bw, h - margin_y - bw], radius=24, fill=wood_bg)
        
        # Wood horizontal planks lines
        plank_h = (h - 2 * (margin_y + bw)) / 3.0
        for i in range(1, 3):
            py = margin_y + bw + i * plank_h
            draw.line([margin_x + bw, py, w - margin_x - bw, py], fill=wood_dark, width=4)
            draw.line([margin_x + bw, py + 2, w - margin_x - bw, py + 2], fill=wood_light, width=2)
            
        # 4 Corner metal rivets
        corners = [
            (margin_x + 36, margin_y + 36),
            (w - margin_x - 36, margin_y + 36),
            (margin_x + 36, h - margin_y - 36),
            (w - margin_x - 36, h - margin_y - 36)
        ]
        for cx, cy in corners:
            draw.ellipse([cx - 10, cy - 10, cx + 10, cy + 10], fill=rivet_color)
            draw.ellipse([cx - 7, cy - 7, cx + 3, cy + 3], fill=rivet_hi)
            
        # Draw the slot number with heavy stylized outline & shadow
        text = str(slot_num)
        bbox = font.getbbox(text)
        tw = bbox[2] - bbox[0]
        th = bbox[3] - bbox[1]
        tx = (w - tw) / 2.0 - bbox[0]
        ty = (h - th) / 2.0 - bbox[1] - 8
        
        # Drop shadow
        for ox in range(-12, 13):
            for oy in range(-12, 13):
                if ox*ox + oy*oy <= 144:
                    draw.text((tx + ox, ty + oy + 12), text, font=font, fill=text_shadow)
                    
        # Dark outline
        for ox in range(-8, 9):
            for oy in range(-8, 9):
                if ox*ox + oy*oy <= 64:
                    draw.text((tx + ox, ty + oy), text, font=font, fill=border_dark)
                    
        # Main text
        draw.text((tx, ty), text, font=font, fill=text_color)
        
        out_path = f"Assets/Textures/Marina/Sign_Slot_{slot_num}.png"
        img.save(out_path)
        print(f"Saved {out_path}")

create_buoy_texture()
create_slot_signs()
print("All Marina textures generated successfully!")
