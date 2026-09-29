import os
from PIL import Image, ImageDraw, ImageFilter, ImageFont

def build_full_gemi_showcase():
    W, H = 1080, 1920
    img = Image.new("RGBA", (W, H), (248, 224, 188, 255)) # Warm beach sand
    draw = ImageDraw.Draw(img)

    # 1. Subtle sand gradient & lighting
    sand_sh = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    s_draw = ImageDraw.Draw(sand_sh)
    for y in range(H):
        t = y / float(H)
        alpha = int(12 * (1 - t))
        s_draw.line([0, y, W, y], fill=(210, 175, 125, alpha))
    img = Image.alpha_composite(img, sand_sh)

    # 2. Lower Marina Water Zone (from y=1340 to 1920)
    water_y = 1340
    water_img = Image.new("RGBA", (W, H - water_y), (0, 0, 0, 0))
    w_draw = ImageDraw.Draw(water_img)
    for y in range(H - water_y):
        t = y / float(H - water_y)
        cr = int(45 * (1-t) + 20 * t)
        cg = int(155 * (1-t) + 90 * t)
        cb = int(235 * (1-t) + 175 * t)
        w_draw.line([0, y, W, y], fill=(cr, cg, cb, 255))
    
    # Wooden bridge / shoreline deck
    bridge_h = 36
    w_draw.rectangle([0, 0, W, bridge_h], fill=(135, 85, 50, 255))
    w_draw.line([0, 0, W, 0], fill=(190, 135, 90, 255), width=4)
    w_draw.line([0, bridge_h, W, bridge_h], fill=(70, 40, 20, 255), width=5)
    
    # Marina Berths (5 slots with floating buoy circles & chains)
    slot_w = 175
    slot_gap = 22
    total_slots_w = 5 * slot_w + 4 * slot_gap
    start_slot_x = (W - total_slots_w) // 2
    
    for i in range(5):
        sx = start_slot_x + i * (slot_w + slot_gap)
        sy = 70
        # Water slot berth outline (dashed/chain feel)
        w_draw.rounded_rectangle([sx, sy, sx + slot_w, sy + 250], radius=16, outline=(255, 255, 255, 110), width=3)
        # 4 Red-white corner buoys
        for bx, by in [(sx, sy), (sx + slot_w, sy), (sx, sy + 250), (sx + slot_w, sy + 250)]:
            w_draw.ellipse([bx - 12, by - 12, bx + 12, by + 12], fill=(240, 45, 45, 255), outline=(255, 255, 255, 255), width=3)
        # Slot number badge at top of berth
        w_draw.rounded_rectangle([sx + slot_w//2 - 22, sy - 14, sx + slot_w//2 + 22, sy + 14], radius=6, fill=(110, 65, 35, 255), outline=(230, 190, 120, 255), width=2)
        
    img.paste(water_img, (0, water_y), water_img)

    # 3. Paste Calibrated Heart Pixel Art in Center (Transparent backing)
    heart_sim_path = r"C:\Users\ezgid\.gemini\antigravity-ide\brain\6c76da88-bba0-4dd5-8054-826797c4d077\heart_snug_spacing_calibrated.png"
    if os.path.exists(heart_sim_path):
        heart_sim = Image.open(heart_sim_path)
        hw, hh = int(heart_sim.width * 1.32), int(heart_sim.height * 1.32)
        heart_sim_resized = heart_sim.resize((hw, hh), Image.Resampling.LANCZOS)
        hx = (W - hw) // 2
        hy = 480
        img.paste(heart_sim_resized, (hx, hy), heart_sim_resized)

    # 4. Top Banner (Row 1 - Dark Navy Notch Banner)
    banner_path = "Assets/UI/CasualUI/hud_top_banner.png"
    if os.path.exists(banner_path):
        banner_img = Image.open(banner_path).convert("RGBA")
        bw, bh = W, 185
        banner_img = banner_img.resize((bw, bh), Image.Resampling.LANCZOS)
        img.paste(banner_img, (0, 0), banner_img)

    # 4a. Settings button (Top-Left)
    settings_path = "Assets/UI/CasualUI/btn_settings.png"
    if os.path.exists(settings_path):
        set_img = Image.open(settings_path).convert("RGBA").resize((104, 104), Image.Resampling.LANCZOS)
        img.paste(set_img, (40, 40), set_img)

    # 4b. Stats Row (Top-Right): LEVEL 1 + Heart Pill + Coin Pill
    font_path = "Assets/Fonts/LilitaOne-Regular.ttf"
    if not os.path.exists(font_path): font_path = "C:/Windows/Fonts/arialbd.ttf"
    try: font_banner = ImageFont.truetype(font_path, 44)
    except: font_banner = ImageFont.load_default()

    # Draw "LEVEL 1" in banner
    draw.text((435, 68), "LEVEL 1", font=font_banner, fill=(255, 255, 255, 255))

    # Heart Pill & Coin Pill
    pill_path = "Assets/UI/CasualUI/ui_pill.png"
    icon_heart_path = "Assets/UI/CasualUI/icon_heart.png"
    icon_coin_path = "Assets/UI/CasualUI/icon_coin.png"
    plus_path = "Assets/UI/CasualUI/btn_plus.png"

    if os.path.exists(pill_path):
        pill_img = Image.open(pill_path).convert("RGBA")
        # Heart Pill
        hp = pill_img.resize((190, 78), Image.Resampling.LANCZOS)
        img.paste(hp, (635, 52), hp)
        if os.path.exists(icon_heart_path):
            hi = Image.open(icon_heart_path).convert("RGBA").resize((76, 76), Image.Resampling.LANCZOS)
            img.paste(hi, (625, 53), hi)
        draw.text((712, 65), "3", font=font_banner, fill=(255, 255, 255, 255))
        if os.path.exists(plus_path):
            pp = Image.open(plus_path).convert("RGBA").resize((44, 44), Image.Resampling.LANCZOS)
            img.paste(pp, (772, 69), pp)

        # Coin Pill
        cp = pill_img.resize((200, 78), Image.Resampling.LANCZOS)
        img.paste(cp, (845, 52), cp)
        if os.path.exists(icon_coin_path):
            ci = Image.open(icon_coin_path).convert("RGBA").resize((76, 76), Image.Resampling.LANCZOS)
            img.paste(ci, (835, 53), ci)
        draw.text((910, 65), "250", font=font_banner, fill=(255, 255, 255, 255))
        if os.path.exists(plus_path):
            img.paste(pp, (990, 69), pp)

    # 5. SUBHEADER (Row 2 - Level Action Bar from media_1790718328948.png)
    sub_y = 230
    
    # 5a. Restart Button (Left: x=45, y=sub_y)
    restart_path = "Assets/UI/CasualUI/btn_restart.png"
    if os.path.exists(restart_path):
        r_img = Image.open(restart_path).convert("RGBA").resize((106, 106), Image.Resampling.LANCZOS)
        img.paste(r_img, (45, sub_y), r_img)

    # 5b. HARD Badge (Left: x=175, y=sub_y+14)
    hard_path = "Assets/UI/CasualUI/badge_hard.png"
    if os.path.exists(hard_path):
        h_img = Image.open(hard_path).convert("RGBA").resize((220, 88), Image.Resampling.LANCZOS)
        img.paste(h_img, (175, sub_y + 10), h_img)

    # 5c. LevelTitle ("Level 35" in Center)
    try: font_sublevel = ImageFont.truetype(font_path, 66)
    except: font_sublevel = font_banner
    
    lvl_text = "Level 35"
    text_x = 445
    text_y = sub_y + 16
    
    # Deep dark shadow / outline for Level 35 (exact look from screenshot)
    outline_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ol_draw = ImageDraw.Draw(outline_layer)
    # Drop shadow
    ol_draw.text((text_x + 3, text_y + 6), lvl_text, font=font_sublevel, fill=(0, 0, 0, 180))
    # Outline
    for dx in range(-5, 6):
        for dy in range(-5, 6):
            if dx*dx + dy*dy <= 25:
                ol_draw.text((text_x + dx, text_y + dy + 2), lvl_text, font=font_sublevel, fill=(30, 20, 45, 255))
    
    # White fill
    ol_draw.text((text_x, text_y), lvl_text, font=font_sublevel, fill=(255, 255, 255, 255))
    img = Image.alpha_composite(img, outline_layer)

    # 5d. Sound Button (Right: x=805, y=sub_y)
    sound_path = "Assets/UI/CasualUI/btn_sound.png"
    if os.path.exists(sound_path):
        s_img = Image.open(sound_path).convert("RGBA").resize((106, 106), Image.Resampling.LANCZOS)
        img.paste(s_img, (805, sub_y), s_img)

    # 5e. Haptic Button (Right: x=930, y=sub_y)
    haptic_path = "Assets/UI/CasualUI/btn_haptic.png"
    if os.path.exists(haptic_path):
        vib_img = Image.open(haptic_path).convert("RGBA").resize((106, 106), Image.Resampling.LANCZOS)
        img.paste(vib_img, (930, sub_y), vib_img)

    out_showcase = r"C:\Users\ezgid\.gemini\antigravity-ide\brain\6c76da88-bba0-4dd5-8054-826797c4d077\gemi_full_canvas_showcase.png"
    img.save(out_showcase)
    print(f"Full UI canvas showcase successfully saved to {out_showcase}")

if __name__ == "__main__":
    build_full_gemi_showcase()
