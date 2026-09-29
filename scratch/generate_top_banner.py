from PIL import Image, ImageDraw, ImageFilter

def generate_hud_top_banner():
    W, H = 1080, 180
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Notch parameters:
    # Notch width: 440, notch depth: 46
    notch_w = 440
    notch_d = 48
    notch_r = 28
    
    # Outer container with rounded bottom corners
    corner_r = 36
    
    # Draw dark navy base polygon with notch
    # Points from top-left:
    # (0, 0) -> (0, H - corner_r) -> arc to (corner_r, H) -> (W - corner_r, H) -> arc to (W, H - corner_r) -> (W, 0)
    # Notch at top center:
    # (W/2 + notch_w/2, 0) -> curve to (W/2 + notch_w/2 - 20, notch_d) -> line to (W/2 - notch_w/2 + 20, notch_d) -> curve to (W/2 - notch_w/2, 0)
    
    # We can create a mask:
    mask = Image.new("L", (W, H), 0)
    m_draw = ImageDraw.Draw(mask)
    
    # Full rounded rectangle for the bottom
    m_draw.rounded_rectangle([0, 0, W, H], radius=corner_r, fill=255)
    # Re-fill the top corners to be sharp rectangular
    m_draw.rectangle([0, 0, W, corner_r], fill=255)
    
    # Cut out the notch at top center
    notch_x0 = (W - notch_w) // 2
    notch_x1 = (W + notch_w) // 2
    # Notch cutout with rounded bottom corners
    m_draw.rounded_rectangle([notch_x0, -10, notch_x1, notch_d], radius=notch_r, fill=0)

    # Base color gradient
    base_color = (18, 26, 44, 245) # Deep navy matching Image 1
    gradient = Image.new("RGBA", (W, H), base_color)
    g_draw = ImageDraw.Draw(gradient)
    
    # Subtle top-to-bottom shading
    for y in range(H):
        alpha = int(240 + (y / H) * 15)
        r = int(17 + (y / H) * 8)
        g = int(24 + (y / H) * 12)
        b = int(40 + (y / H) * 18)
        g_draw.line([0, y, W, y], fill=(r, g, b, alpha))

    # Apply mask
    img.paste(gradient, (0, 0), mask)
    
    # Draw bottom rim highlight
    rim_img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    r_draw = ImageDraw.Draw(rim_img)
    # Bottom line and bottom corner arcs
    r_draw.line([corner_r, H - 2, W - corner_r, H - 2], fill=(65, 95, 150, 160), width=2)
    r_draw.arc([0, H - corner_r*2, corner_r*2, H], start=90, end=180, fill=(65, 95, 150, 160), width=2)
    r_draw.arc([W - corner_r*2, H - corner_r*2, W, H], start=0, end=90, fill=(65, 95, 150, 160), width=2)
    
    # Notch rim highlight
    r_draw.arc([notch_x0, notch_d - notch_r*2, notch_x0 + notch_r*2, notch_d], start=90, end=180, fill=(45, 65, 105, 140), width=2)
    r_draw.line([notch_x0 + notch_r, notch_d - 1, notch_x1 - notch_r, notch_d - 1], fill=(45, 65, 105, 140), width=2)
    r_draw.arc([notch_x1 - notch_r*2, notch_d - notch_r*2, notch_x1, notch_d], start=0, end=90, fill=(45, 65, 105, 140), width=2)
    
    img = Image.alpha_composite(img, rim_img)
    
    out_path = "Assets/UI/CasualUI/hud_top_banner.png"
    img.save(out_path, "PNG")
    print(f"Saved {out_path} ({W}x{H})")

if __name__ == "__main__":
    generate_hud_top_banner()
