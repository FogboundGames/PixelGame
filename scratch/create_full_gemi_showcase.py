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
    
    # Marina Berths (5 slots with the new Lifebuoy UI asset)
    lifebuoy_path = "Assets/Textures/Marina/slot_lifebuoy.png"
    lifebuoy_img = None
    if os.path.exists(lifebuoy_path):
        lifebuoy_img = Image.open(lifebuoy_path).convert("RGBA")

    slot_size = 195
    slot_gap = 14
    total_slots_w = 5 * slot_size + 4 * slot_gap
    start_slot_x = (W - total_slots_w) // 2
    
    for i in range(5):
        sx = start_slot_x + i * (slot_size + slot_gap)
        sy = 90
        if lifebuoy_img:
            buoy_resized = lifebuoy_img.resize((slot_size, slot_size), Image.Resampling.LANCZOS)
            water_img.paste(buoy_resized, (sx, sy), buoy_resized)
        else:
            w_draw.rounded_rectangle([sx, sy, sx + slot_size, sy + slot_size], radius=24, outline=(255, 60, 60, 255), width=6)
        
    img.paste(water_img, (0, water_y), water_img)

    # 3. Center Pixel Art or Beach Elements if present
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

    draw.text((435, 68), "LEVEL 1", font=font_banner, fill=(255, 255, 255, 255))

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

    # 5. SUBHEADER (Row 2 - Level Action Bar matching user image)
    sub_y = 230
    
    # 5a. Restart Button (Left: x=40, y=sub_y)
    restart_path = "Assets/UI/CasualUI/btn_restart.png"
    if os.path.exists(restart_path):
        r_img = Image.open(restart_path).convert("RGBA").resize((104, 104), Image.Resampling.LANCZOS)
        img.paste(r_img, (40, sub_y), r_img)

    # 5b. HARD Badge (Bunny mascot + pink HARD capsule: x=165, y=sub_y+12)
    hard_path = "Assets/UI/CasualUI/badge_hard.png"
    if os.path.exists(hard_path):
        h_img = Image.open(hard_path).convert("RGBA").resize((214, 80), Image.Resampling.LANCZOS)
        img.paste(h_img, (165, sub_y + 12), h_img)

    # 5c. Level Capsule Background & "Level 35" in Center
    cap_path = "Assets/UI/CasualUI/bg_level_capsule.png"
    cap_w, cap_h = 280, 76
    cap_x = (W - cap_w) // 2
    cap_y = sub_y + 14
    if os.path.exists(cap_path):
        c_img = Image.open(cap_path).convert("RGBA").resize((cap_w, cap_h), Image.Resampling.LANCZOS)
        img.paste(c_img, (cap_x, cap_y), c_img)

    try: font_sublevel = ImageFont.truetype(font_path, 60)
    except: font_sublevel = font_banner
    
    lvl_text = "Level 35"
    bbox = font_sublevel.getbbox(lvl_text)
    tw = bbox[2] - bbox[0]
    th = bbox[3] - bbox[1]
    text_x = cap_x + (cap_w - tw) // 2 - bbox[0]
    text_y = cap_y + (cap_h - th) // 2 - bbox[1]
    
    # Blue outline + drop shadow for Level 35
    outline_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ol_draw = ImageDraw.Draw(outline_layer)
    # Dark blue drop shadow
    ol_draw.text((text_x + 2, text_y + 6), lvl_text, font=font_sublevel, fill=(30, 80, 145, 200))
    # Sky blue outline
    blue_outline = (48, 120, 200, 255)
    for dx in range(-4, 5):
        for dy in range(-4, 5):
            if dx*dx + dy*dy <= 16:
                ol_draw.text((text_x + dx, text_y + dy + 1), lvl_text, font=font_sublevel, fill=blue_outline)
    
    # White fill
    ol_draw.text((text_x, text_y), lvl_text, font=font_sublevel, fill=(255, 255, 255, 255))
    img = Image.alpha_composite(img, outline_layer)

    # 5d. Sound Button (Right: x=815, y=sub_y)
    sound_path = "Assets/UI/CasualUI/btn_sound.png"
    if os.path.exists(sound_path):
        s_img = Image.open(sound_path).convert("RGBA").resize((104, 104), Image.Resampling.LANCZOS)
        img.paste(s_img, (815, sub_y), s_img)

    # 5e. Music Button (Right: x=935, y=sub_y)
    music_path = "Assets/UI/CasualUI/btn_music.png"
    if os.path.exists(music_path):
        m_img = Image.open(music_path).convert("RGBA").resize((104, 104), Image.Resampling.LANCZOS)
        img.paste(m_img, (935, sub_y), m_img)

    os.makedirs("scratch", exist_ok=True)
    out_showcase = "scratch/gemi_full_canvas_showcase.png"
    img.save(out_showcase)
    
    # Also save top banner crop
    sub_crop = img.crop((0, sub_y - 20, W, sub_y + 130))
    sub_crop.save("scratch/subhead_ingame_crop.png")
    print(f"Full UI canvas showcase successfully saved to {out_showcase}")
    print("Subheader crop saved to scratch/subhead_ingame_crop.png")

if __name__ == "__main__":
    build_full_gemi_showcase()
