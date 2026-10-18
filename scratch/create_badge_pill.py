from PIL import Image, ImageDraw, ImageFilter

def create_badge_pill():
    w, h = 256, 144
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    
    pad = 12
    rect = [pad, pad, w - pad, h - pad]
    radius = 36  # Pill/capsule rounded rectangle
    
    # Soft drop shadow
    shadow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    s_draw = ImageDraw.Draw(shadow)
    s_rect = [pad, pad + 4, w - pad, h - pad + 4]
    s_draw.rounded_rectangle(s_rect, radius=radius, fill=(0, 0, 0, 140))
    shadow = shadow.filter(ImageFilter.GaussianBlur(8))
    img.paste(shadow, (0, 0), shadow)
    
    # Dark body
    body = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    b_draw = ImageDraw.Draw(body)
    
    # Fill with deep navy/charcoal as in Image 1: #1E2538
    fill_color = (30, 37, 56, 255)
    border_color = (48, 58, 82, 255)  # Subtle upper edge definition
    
    b_draw.rounded_rectangle(rect, radius=radius, fill=fill_color, outline=border_color, width=2)
    img.paste(body, (0, 0), body)
    
    out_path = "Assets/UI/Badge_Pill.png"
    img.save(out_path, "PNG")
    print(f"Saved badge pill to {out_path}")

if __name__ == "__main__":
    create_badge_pill()
