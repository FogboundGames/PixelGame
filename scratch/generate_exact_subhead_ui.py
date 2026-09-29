import os
import math
from PIL import Image, ImageDraw, ImageFilter, ImageFont

def draw_squircle_button(top_color, bot_color, size=512):
    """
    Creates the 3D casual cartoon squircle button from the reference:
    - Soft drop shadow
    - Bottom golden bevel / tray
    - Thick crisp white outer frame
    - Juicy gradient core with top jelly gloss reflection
    """
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    margin = 38
    r = 120
    
    # 1. Soft Drop Shadow
    shadow = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    s_draw = ImageDraw.Draw(shadow)
    s_draw.rounded_rectangle([margin, margin + 28, size - margin, size - margin + 28], radius=r, fill=(0, 0, 0, 110))
    shadow = shadow.filter(ImageFilter.GaussianBlur(16))
    img = Image.alpha_composite(img, shadow)

    draw = ImageDraw.Draw(img)
    
    # 2. Bottom Golden Bevel / Tray (extends below the button)
    gold_bot = (185, 115, 10, 255)
    gold_mid = (252, 192, 38, 255)
    draw.rounded_rectangle([margin - 2, margin + 22, size - margin + 2, size - margin + 22], radius=r + 2, fill=gold_bot)
    draw.rounded_rectangle([margin - 2, margin + 12, size - margin + 2, size - margin + 12], radius=r + 2, fill=gold_mid)
    
    # 3. Thick Crisp White Outer Frame
    draw.rounded_rectangle([margin, margin, size - margin, size - margin], radius=r, fill=(255, 255, 255, 255))
    
    # Subtle inner bevel shadow on the white rim bottom
    rim_sh = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    rs_draw = ImageDraw.Draw(rim_sh)
    rs_draw.rounded_rectangle([margin + 2, margin + 8, size - margin - 2, size - margin + 2], radius=r - 2, fill=(220, 215, 210, 180))
    rim_sh = rim_sh.filter(ImageFilter.GaussianBlur(5))
    img = Image.alpha_composite(img, rim_sh)
    
    # 4. Button Core (Gradient Fill)
    c_margin = margin + 18
    inner_w = size - 2 * c_margin
    inner_h = inner_w
    inner_img = Image.new('RGBA', (inner_w, inner_h), (0, 0, 0, 0))
    i_draw = ImageDraw.Draw(inner_img)
    
    # Smooth vertical color gradient
    for y in range(inner_h):
        t = y / float(inner_h)
        t_curv = t * t * (3 - 2 * t)
        cr = int(top_color[0] * (1 - t_curv) + bot_color[0] * t_curv)
        cg = int(top_color[1] * (1 - t_curv) + bot_color[1] * t_curv)
        cb = int(top_color[2] * (1 - t_curv) + bot_color[2] * t_curv)
        i_draw.line([0, y, inner_w, y], fill=(cr, cg, cb, 255))
        
    core_mask = Image.new('L', (inner_w, inner_h), 0)
    m_draw = ImageDraw.Draw(core_mask)
    m_draw.rounded_rectangle([0, 0, inner_w, inner_h], radius=r - 24, fill=255)
    
    # Bottom inner shadow in core
    bot_sh = Image.new('RGBA', (inner_w, inner_h), (0, 0, 0, 0))
    bs_draw = ImageDraw.Draw(bot_sh)
    bs_draw.rectangle([0, int(inner_h * 0.70), inner_w, inner_h], fill=(0, 0, 0, 60))
    bot_sh = bot_sh.filter(ImageFilter.GaussianBlur(14))
    inner_img = Image.alpha_composite(inner_img, bot_sh)
    
    # Glossy jelly highlight on top
    gloss = Image.new('RGBA', (inner_w, inner_h), (0, 0, 0, 0))
    g_draw = ImageDraw.Draw(gloss)
    g_draw.ellipse([-inner_w * 0.25, -inner_h * 0.70, inner_w * 1.25, inner_h * 0.55], fill=(255, 255, 255, 125))
    gloss = gloss.filter(ImageFilter.GaussianBlur(10))
    inner_img = Image.alpha_composite(inner_img, gloss)
    
    # Composite core onto main img
    core_full = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    core_full.paste(inner_img, (c_margin, c_margin), core_mask)
    img = Image.alpha_composite(img, core_full)
    
    return img

def create_restart_button():
    top_col = (255, 60, 75)
    bot_col = (215, 20, 38)
    img = draw_squircle_button(top_col, bot_col, size=512)
    
    icon_layer = Image.new('RGBA', (512, 512), (0, 0, 0, 0))
    d = ImageDraw.Draw(icon_layer)
    cx, cy = 256, 254
    
    r_arc = 88
    stroke_w = 56
    arc_box = [cx - r_arc, cy - r_arc, cx + r_arc, cy + r_arc]
    
    # Shadows for circular arrow icon
    for oy, alpha in [(8, 140), (14, 75)]:
        s_box = [cx - r_arc, cy - r_arc + oy, cx + r_arc, cy + r_arc + oy]
        # Arc from 70 deg clockwise to 330 deg
        d.arc(s_box, start=70, end=330, fill=(0, 0, 0, alpha), width=stroke_w)
        # Arrowhead at top: points right/up
        ax, ay = cx + 32, cy - r_arc + oy - 4
        d.polygon([(ax - 10, ay - 46), (ax + 54, ay + 6), (ax - 10, ay + 50)], fill=(0, 0, 0, alpha))
        # End cap shadow at 70 deg
        rad = math.radians(70)
        ex = cx + r_arc * math.cos(rad)
        ey = cy + r_arc * math.sin(rad) + oy
        d.ellipse([ex - stroke_w/2, ey - stroke_w/2, ex + stroke_w/2, ey + stroke_w/2], fill=(0, 0, 0, alpha))

    # White circular arrow
    d.arc(arc_box, start=70, end=330, fill=(255, 255, 255, 255), width=stroke_w)
    ax, ay = cx + 32, cy - r_arc - 4
    d.polygon([(ax - 10, ay - 46), (ax + 54, ay + 6), (ax - 10, ay + 50)], fill=(255, 255, 255, 255))
    
    rad = math.radians(70)
    ex = cx + r_arc * math.cos(rad)
    ey = cy + r_arc * math.sin(rad)
    d.ellipse([ex - stroke_w/2, ey - stroke_w/2, ex + stroke_w/2, ey + stroke_w/2], fill=(255, 255, 255, 255))
    
    img = Image.alpha_composite(img, icon_layer)
    return img.resize((256, 256), Image.Resampling.LANCZOS)

def create_sound_button():
    top_col = (95, 230, 24)
    bot_col = (35, 178, 12)
    img = draw_squircle_button(top_col, bot_col, size=512)
    
    icon_layer = Image.new('RGBA', (512, 512), (0, 0, 0, 0))
    d = ImageDraw.Draw(icon_layer)
    cx, cy = 246, 252
    
    # Shadows
    for oy, alpha in [(8, 140), (14, 75)]:
        # Speaker horn
        d.rounded_rectangle([cx - 96, cy - 36 + oy, cx - 44, cy + 36 + oy], radius=10, fill=(0, 0, 0, alpha))
        d.polygon([(cx - 44, cy - 36 + oy), (cx + 20, cy - 86 + oy), (cx + 20, cy + 86 + oy), (cx - 44, cy + 36 + oy)], fill=(0, 0, 0, alpha))
        # Waves
        d.arc([cx - 28, cy - 54 + oy, cx + 64, cy + 54 + oy], start=-48, end=48, fill=(0, 0, 0, alpha), width=30)
        d.arc([cx - 28, cy - 98 + oy, cx + 128, cy + 98 + oy], start=-48, end=48, fill=(0, 0, 0, alpha), width=30)

    # White speaker icon
    d.rounded_rectangle([cx - 96, cy - 36, cx - 44, cy + 36], radius=10, fill=(255, 255, 255, 255))
    d.polygon([(cx - 44, cy - 36), (cx + 20, cy - 86), (cx + 20, cy + 86), (cx - 44, cy + 36)], fill=(255, 255, 255, 255))
    d.arc([cx - 28, cy - 54, cx + 64, cy + 54], start=-48, end=48, fill=(255, 255, 255, 255), width=30)
    d.arc([cx - 28, cy - 98, cx + 128, cy + 98], start=-48, end=48, fill=(255, 255, 255, 255), width=30)

    img = Image.alpha_composite(img, icon_layer)
    return img.resize((256, 256), Image.Resampling.LANCZOS)

def create_music_button():
    top_col = (95, 230, 24)
    bot_col = (35, 178, 12)
    img = draw_squircle_button(top_col, bot_col, size=512)
    
    icon_layer = Image.new('RGBA', (512, 512), (0, 0, 0, 0))
    d = ImageDraw.Draw(icon_layer)
    
    def draw_notes(oy=0, fill_color=(255, 255, 255, 255)):
        # Horizontal beam connecting at top
        b_left = 186
        b_right = 328
        b_y = 168 + oy
        beam_h = 36
        d.rounded_rectangle([b_left, b_y, b_right, b_y + beam_h], radius=8, fill=fill_color)
        
        # Left stem
        s_w = 26
        s1_x = b_left + 14
        d.rounded_rectangle([s1_x - s_w/2, b_y, s1_x + s_w/2, 305 + oy], radius=6, fill=fill_color)
        
        # Right stem
        s2_x = b_right - 14
        d.rounded_rectangle([s2_x - s_w/2, b_y, s2_x + s_w/2, 305 + oy], radius=6, fill=fill_color)
        
        # Left note head (tilted ellipse)
        d.ellipse([s1_x - 48, 290 + oy, s1_x + 16, 338 + oy], fill=fill_color)
        # Right note head (tilted ellipse)
        d.ellipse([s2_x - 48, 290 + oy, s2_x + 16, 338 + oy], fill=fill_color)

    for oy, alpha in [(8, 140), (14, 75)]:
        draw_notes(oy, (0, 0, 0, alpha))
        
    draw_notes(0, (255, 255, 255, 255))
    
    img = Image.alpha_composite(img, icon_layer)
    return img.resize((256, 256), Image.Resampling.LANCZOS)

def create_bunny_hard_badge():
    """
    Creates the cute bunny mascot face + pink HARD capsule pill.
    Rendered at 800x300 downsampled to 400x150.
    """
    W, H = 800, 300
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    
    pill_x1 = 230
    pill_y1 = 66
    pill_x2 = 750
    pill_y2 = 234
    pill_r = (pill_y2 - pill_y1) // 2  # 84
    
    # 1. Drop shadow for pill + bunny
    shadow = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow)
    sd.rounded_rectangle([pill_x1, pill_y1 + 14, pill_x2, pill_y2 + 14], radius=pill_r, fill=(0, 0, 0, 85))
    sd.ellipse([45, 60 + 14, 275, 265 + 14], fill=(0, 0, 0, 80))
    shadow = shadow.filter(ImageFilter.GaussianBlur(14))
    img = Image.alpha_composite(img, shadow)
    
    # 2. Draw HARD Pink Pill
    pill_img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    pd = ImageDraw.Draw(pill_img)
    
    # Bottom bevel (darker raspberry)
    pd.rounded_rectangle([pill_x1, pill_y1 + 12, pill_x2, pill_y2 + 12], radius=pill_r, fill=(180, 8, 70, 255))
    
    pw = pill_x2 - pill_x1
    ph = pill_y2 - pill_y1
    p_body = Image.new('RGBA', (pw, ph), (0, 0, 0, 0))
    pbd = ImageDraw.Draw(p_body)
    
    top_p = (255, 38, 110)
    bot_p = (235, 12, 80)
    for y in range(ph):
        t = y / float(ph)
        cr = int(top_p[0] * (1 - t) + bot_p[0] * t)
        cg = int(top_p[1] * (1 - t) + bot_p[1] * t)
        cb = int(top_p[2] * (1 - t) + bot_p[2] * t)
        pbd.line([0, y, pw, y], fill=(cr, cg, cb, 255))
        
    p_mask = Image.new('L', (pw, ph), 0)
    pmd = ImageDraw.Draw(p_mask)
    pmd.rounded_rectangle([0, 0, pw, ph], radius=pill_r, fill=255)
    
    # Gloss highlight on pill top
    p_gloss = Image.new('RGBA', (pw, ph), (0, 0, 0, 0))
    pgd = ImageDraw.Draw(p_gloss)
    pgd.ellipse([-pw * 0.1, -ph * 0.72, pw * 1.1, ph * 0.58], fill=(255, 255, 255, 105))
    p_body = Image.alpha_composite(p_body, p_gloss)
    
    pill_img.paste(p_body, (pill_x1, pill_y1), p_mask)
    
    # Text "HARD" on pill
    font_path = "Assets/Fonts/LilitaOne-Regular.ttf"
    if not os.path.exists(font_path):
        font_path = "C:/Windows/Fonts/arialbd.ttf"
    font = ImageFont.truetype(font_path, 118)
    
    text = "HARD"
    t_cx = (pill_x1 + pill_x2) // 2 + 25
    t_cy = (pill_y1 + pill_y2) // 2 - 4
    
    bbox = font.getbbox(text)
    tw = bbox[2] - bbox[0]
    th = bbox[3] - bbox[1]
    tx = t_cx - tw // 2
    ty = t_cy - th // 2 - bbox[1]
    
    pd.text((tx, ty + 8), text, font=font, fill=(140, 5, 48, 220))
    pd.text((tx, ty), text, font=font, fill=(255, 255, 255, 255))
    
    img = Image.alpha_composite(img, pill_img)
    
    # 3. Draw Bunny Mascot on the Left
    bunny_img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    bd = ImageDraw.Draw(bunny_img)
    
    bx, by = 155, 168
    
    # Ears:
    ear_w, ear_h = 44, 115
    # Outer white ears
    bd.ellipse([bx - 52 - ear_w/2, by - 142, bx - 52 + ear_w/2, by - 18], fill=(255, 255, 255, 255))
    bd.ellipse([bx + 38 - ear_w/2, by - 142, bx + 38 + ear_w/2, by - 18], fill=(255, 255, 255, 255))
    
    # Inner pink ears
    in_w, in_h = 24, 90
    bd.ellipse([bx - 52 - in_w/2, by - 130, bx - 52 + in_w/2, by - 34], fill=(255, 28, 110, 255))
    bd.ellipse([bx + 38 - in_w/2, by - 130, bx + 38 + in_w/2, by - 34], fill=(255, 28, 110, 255))
    
    # Head (white rounded face)
    head_rx, head_ry = 92, 82
    bd.ellipse([bx - head_rx, by - head_ry + 6, bx + head_rx, by + head_ry + 6], fill=(225, 220, 225, 255))
    bd.ellipse([bx - head_rx, by - head_ry, bx + head_rx, by + head_ry], fill=(255, 255, 255, 255))
    
    # Cheeks (bright cute blush marks)
    ch_col = (255, 135, 180, 245)
    bd.ellipse([bx - 72, by + 12, bx - 34, by + 44], fill=ch_col)
    bd.ellipse([bx + 34, by + 12, bx + 72, by + 44], fill=ch_col)
    
    # Eyes (dark shiny brown/black circles)
    eye_col = (68, 20, 30, 255)
    bd.ellipse([bx - 44, by - 14, bx - 20, by + 12], fill=eye_col)
    bd.ellipse([bx + 20, by - 14, bx + 44, by + 12], fill=eye_col)
    
    # White catchlights
    bd.ellipse([bx - 40, by - 12, bx - 30, by - 2], fill=(255, 255, 255, 255))
    bd.ellipse([bx + 24, by - 12, bx + 34, by - 2], fill=(255, 255, 255, 255))
    
    # Nose & mouth
    nose_col = (195, 28, 75, 255)
    bd.polygon([(bx - 8, by + 12), (bx + 8, by + 12), (bx, by + 22)], fill=nose_col)
    bd.arc([bx - 18, by + 18, bx, by + 34], start=20, end=160, fill=nose_col, width=5)
    bd.arc([bx, by + 18, bx + 18, by + 34], start=20, end=160, fill=nose_col, width=5)
    
    img = Image.alpha_composite(img, bunny_img)
    return img.resize((400, 150), Image.Resampling.LANCZOS)

def create_level_capsule_bg():
    W, H = 640, 180
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    r = H // 2
    bg_col = (245, 222, 192, 140)
    d.rounded_rectangle([4, 4, W - 4, H - 4], radius=r - 4, fill=bg_col)
    d.rounded_rectangle([10, 8, W - 10, H - 24], radius=r - 10, outline=(255, 245, 230, 75), width=4)
    return img.resize((320, 90), Image.Resampling.LANCZOS)

def main():
    target_dir = "Assets/UI/CasualUI"
    create_restart_button().save(os.path.join(target_dir, "btn_restart.png"))
    print("Saved btn_restart.png to Assets/UI/CasualUI")
    create_sound_button().save(os.path.join(target_dir, "btn_sound.png"))
    print("Saved btn_sound.png to Assets/UI/CasualUI")
    create_music_button().save(os.path.join(target_dir, "btn_music.png"))
    print("Saved btn_music.png to Assets/UI/CasualUI")
    create_bunny_hard_badge().save(os.path.join(target_dir, "badge_hard.png"))
    print("Saved badge_hard.png to Assets/UI/CasualUI")
    create_level_capsule_bg().save(os.path.join(target_dir, "bg_level_capsule.png"))
    print("Saved bg_level_capsule.png to Assets/UI/CasualUI")

if __name__ == "__main__":
    main()
