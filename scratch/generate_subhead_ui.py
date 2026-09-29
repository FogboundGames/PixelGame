import os
import math
from PIL import Image, ImageDraw, ImageFilter, ImageFont

def draw_squircle_button(top_color, bot_color, size=512):
    # Supersampled drawing at 512x512
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    margin = 32
    r = 110
    
    # 1. Soft Drop Shadow
    shadow = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    s_draw = ImageDraw.Draw(shadow)
    s_draw.rounded_rectangle([margin, margin+24, size-margin, size-margin+24], radius=r, fill=(0, 0, 0, 110))
    shadow = shadow.filter(ImageFilter.GaussianBlur(16))
    img = Image.alpha_composite(img, shadow)

    draw = ImageDraw.Draw(img)
    
    # 2. Outer golden rim
    # Bottom darker gold bevel
    draw.rounded_rectangle([margin, margin+12, size-margin, size-margin+12], radius=r, fill=(175, 105, 5, 255))
    # Main golden yellow border
    draw.rounded_rectangle([margin, margin, size-margin, size-margin], radius=r, fill=(255, 205, 25, 255))
    # Top highlight rim
    draw.rounded_rectangle([margin+4, margin+4, size-margin-4, size-margin-14], radius=r-4, outline=(255, 245, 140, 255), width=6)
    
    # 3. Inner border channel (darker orange/amber groove)
    b_margin = margin + 24
    draw.rounded_rectangle([b_margin, b_margin, size-b_margin, size-b_margin], radius=r-20, fill=(155, 60, 5, 255))
    
    # 4. Button Core (Gradient Fill)
    c_margin = b_margin + 12
    inner_w = size - 2 * c_margin
    inner_h = inner_w
    inner_img = Image.new('RGBA', (inner_w, inner_h), (0, 0, 0, 0))
    i_draw = ImageDraw.Draw(inner_img)
    
    for y in range(inner_h):
        t = y / float(inner_h)
        t_curv = t * t * (3 - 2 * t)
        cr = int(top_color[0] * (1-t_curv) + bot_color[0] * t_curv)
        cg = int(top_color[1] * (1-t_curv) + bot_color[1] * t_curv)
        cb = int(top_color[2] * (1-t_curv) + bot_color[2] * t_curv)
        i_draw.line([0, y, inner_w, y], fill=(cr, cg, cb, 255))
        
    # Mask to rounded rect
    core_mask = Image.new('L', (inner_w, inner_h), 0)
    m_draw = ImageDraw.Draw(core_mask)
    m_draw.rounded_rectangle([0, 0, inner_w, inner_h], radius=r-32, fill=255)
    
    # Gloss highlight on top half of button
    gloss = Image.new('RGBA', (inner_w, inner_h), (0, 0, 0, 0))
    g_draw = ImageDraw.Draw(gloss)
    g_draw.ellipse([-inner_w*0.35, -inner_h*0.8, inner_w*1.35, inner_h*0.65], fill=(255, 255, 255, 85))
    inner_img = Image.alpha_composite(inner_img, gloss)
    
    # Bottom inner shadow
    bot_sh = Image.new('RGBA', (inner_w, inner_h), (0, 0, 0, 0))
    bs_draw = ImageDraw.Draw(bot_sh)
    bs_draw.rectangle([0, int(inner_h*0.75), inner_w, inner_h], fill=(0, 0, 0, 70))
    bot_sh = bot_sh.filter(ImageFilter.GaussianBlur(18))
    inner_img = Image.alpha_composite(inner_img, bot_sh)
    
    # Put masked core onto main img
    core_full = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    core_full.paste(inner_img, (c_margin, c_margin), core_mask)
    img = Image.alpha_composite(img, core_full)
    
    return img

def create_restart_button():
    top_col = (255, 55, 45)
    bot_col = (195, 15, 25)
    img = draw_squircle_button(top_col, bot_col, 512)
    
    icon_layer = Image.new('RGBA', (512, 512), (0, 0, 0, 0))
    d = ImageDraw.Draw(icon_layer)
    cx, cy = 256, 252
    
    # Drop shadow for arrow icon
    for oy, alpha in [(8, 140), (12, 70)]:
        arc_box = [cx - 100, cy - 100 + oy, cx + 100, cy + 100 + oy]
        d.arc(arc_box, start=45, end=320, fill=(0, 0, 0, alpha), width=44)
        ax, ay = cx + 64, cy - 68 + oy
        d.polygon([(ax - 10, ay - 44), (ax + 54, ay), (ax - 10, ay + 44)], fill=(0, 0, 0, alpha))

    # White icon
    arc_box = [cx - 100, cy - 100, cx + 100, cy + 100]
    d.arc(arc_box, start=45, end=320, fill=(255, 255, 255, 255), width=44)
    ax, ay = cx + 64, cy - 68
    d.polygon([(ax - 10, ay - 44), (ax + 54, ay), (ax - 10, ay + 44)], fill=(255, 255, 255, 255))
    
    img = Image.alpha_composite(img, icon_layer)
    return img.resize((256, 256), Image.Resampling.LANCZOS)

def create_sound_button():
    top_col = (145, 230, 20)
    bot_col = (40, 175, 10)
    img = draw_squircle_button(top_col, bot_col, 512)
    
    icon_layer = Image.new('RGBA', (512, 512), (0, 0, 0, 0))
    d = ImageDraw.Draw(icon_layer)
    cx, cy = 246, 252
    
    # Shadow for speaker icon
    for oy, alpha in [(8, 140), (12, 70)]:
        d.rounded_rectangle([cx - 96, cy - 40 + oy, cx - 44, cy + 40 + oy], radius=8, fill=(0, 0, 0, alpha))
        d.polygon([(cx - 44, cy - 40 + oy), (cx + 18, cy - 88 + oy), (cx + 18, cy + 88 + oy), (cx - 44, cy + 40 + oy)], fill=(0, 0, 0, alpha))
        d.arc([cx - 32, cy - 56 + oy, cx + 68, cy + 56 + oy], start=-50, end=50, fill=(0, 0, 0, alpha), width=32)
        d.arc([cx - 32, cy - 96 + oy, cx + 128, cy + 96 + oy], start=-50, end=50, fill=(0, 0, 0, alpha), width=32)

    # White speaker icon
    d.rounded_rectangle([cx - 96, cy - 40, cx - 44, cy + 40], radius=8, fill=(255, 255, 255, 255))
    d.polygon([(cx - 44, cy - 40), (cx + 18, cy - 88), (cx + 18, cy + 88), (cx - 44, cy + 40)], fill=(255, 255, 255, 255))
    d.arc([cx - 32, cy - 56, cx + 68, cy + 56], start=-50, end=50, fill=(255, 255, 255, 255), width=32)
    d.arc([cx - 32, cy - 96, cx + 128, cy + 96], start=-50, end=50, fill=(255, 255, 255, 255), width=32)

    img = Image.alpha_composite(img, icon_layer)
    return img.resize((256, 256), Image.Resampling.LANCZOS)

def create_haptic_button():
    top_col = (145, 230, 20)
    bot_col = (40, 175, 10)
    img = draw_squircle_button(top_col, bot_col, 512)
    
    icon_layer = Image.new('RGBA', (512, 512), (0, 0, 0, 0))
    d = ImageDraw.Draw(icon_layer)
    cx, cy = 256, 252
    
    pw, ph = 92, 152
    pr = 20
    
    for oy, alpha in [(8, 140), (12, 70)]:
        d.rounded_rectangle([cx - pw/2, cy - ph/2 + oy, cx + pw/2, cy + ph/2 + oy], radius=pr, outline=(0, 0, 0, alpha), width=28)
        # Left waves
        d.arc([cx - 136, cy - 48 + oy, cx - 64, cy + 48 + oy], start=130, end=230, fill=(0, 0, 0, alpha), width=20)
        d.arc([cx - 172, cy - 72 + oy, cx - 72, cy + 72 + oy], start=135, end=225, fill=(0, 0, 0, alpha), width=20)
        # Right waves
        d.arc([cx + 64, cy - 48 + oy, cx + 136, cy + 48 + oy], start=-50, end=50, fill=(0, 0, 0, alpha), width=20)
        d.arc([cx + 72, cy - 72 + oy, cx + 172, cy + 72 + oy], start=-45, end=45, fill=(0, 0, 0, alpha), width=20)

    # White icon
    d.rounded_rectangle([cx - pw/2, cy - ph/2, cx + pw/2, cy + ph/2], radius=pr, outline=(255, 255, 255, 255), width=28)
    d.arc([cx - 136, cy - 48, cx - 64, cy + 48], start=130, end=230, fill=(255, 255, 255, 255), width=20)
    d.arc([cx - 172, cy - 72, cx - 72, cy + 72], start=135, end=225, fill=(255, 255, 255, 255), width=20)
    d.arc([cx + 64, cy - 48, cx + 136, cy + 48], start=-50, end=50, fill=(255, 255, 255, 255), width=20)
    d.arc([cx + 72, cy - 72, cx + 172, cy + 72], start=-45, end=45, fill=(255, 255, 255, 255), width=20)

    img = Image.alpha_composite(img, icon_layer)
    return img.resize((256, 256), Image.Resampling.LANCZOS)

def create_hard_badge():
    W, H = 400, 160
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    
    # Draw flaming skull on left: cx=70, cy=80
    sk_cx, sk_cy = 70, 80
    
    flame_col = (255, 30, 110, 240)
    # Flames
    d.polygon([(sk_cx-36, sk_cy+10), (sk_cx-28, sk_cy-50), (sk_cx-10, sk_cy-24), (sk_cx, sk_cy-64), (sk_cx+14, sk_cy-28), (sk_cx+32, sk_cy-52), (sk_cx+36, sk_cy+10), (sk_cx, sk_cy+45)], fill=flame_col)
    
    # Soft skull face (white/pale pink)
    d.ellipse([sk_cx - 30, sk_cy - 25, sk_cx + 30, sk_cy + 28], fill=(255, 240, 248, 255))
    # Jaw
    d.rounded_rectangle([sk_cx - 18, sk_cy + 12, sk_cx + 18, sk_cy + 36], radius=5, fill=(255, 240, 248, 255))
    # Eye sockets (dark pink/magenta)
    eye_col = (185, 15, 80, 255)
    d.ellipse([sk_cx - 20, sk_cy - 6, sk_cx - 5, sk_cy + 12], fill=eye_col)
    d.ellipse([sk_cx + 5, sk_cy - 6, sk_cx + 20, sk_cy + 12], fill=eye_col)
    # Nose
    d.polygon([(sk_cx - 4, sk_cy + 15), (sk_cx + 4, sk_cy + 15), (sk_cx, sk_cy + 10)], fill=eye_col)
    # Teeth
    d.line([sk_cx - 10, sk_cy + 26, sk_cx - 10, sk_cy + 34], fill=eye_col, width=3)
    d.line([sk_cx, sk_cy + 26, sk_cx, sk_cy + 34], fill=eye_col, width=3)
    d.line([sk_cx + 10, sk_cy + 26, sk_cx + 10, sk_cy + 34], fill=eye_col, width=3)

    # Text "HARD"
    font_path = "Assets/Fonts/LilitaOne-Regular.ttf"
    if not os.path.exists(font_path):
        font_path = "C:/Windows/Fonts/impact.ttf"
        if not os.path.exists(font_path):
            font_path = "C:/Windows/Fonts/arialbd.ttf"
            
    try:
        font = ImageFont.truetype(font_path, 80)
    except:
        font = ImageFont.load_default()
        
    text = "HARD"
    tx, ty = 145, 40
    
    # 1. Dark crimson text outline
    outline_col = (110, 5, 45, 255)
    for dx in range(-6, 7):
        for dy in range(-6, 7):
            if dx*dx + dy*dy <= 36:
                d.text((tx+dx, ty+dy+3), text, font=font, fill=outline_col)
                
    # 2. Text Fill gradient (juicy pink to crimson)
    text_mask = Image.new('L', (W, H), 0)
    tm_draw = ImageDraw.Draw(text_mask)
    tm_draw.text((tx, ty), text, font=font, fill=255)
    
    text_grad = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    tg_draw = ImageDraw.Draw(text_grad)
    for y in range(H):
        t = y / float(H)
        cr = int(255 * (1-t) + 215 * t)
        cg = int(60 * (1-t) + 15 * t)
        cb = int(140 * (1-t) + 75 * t)
        tg_draw.line([0, y, W, y], fill=(cr, cg, cb, 255))
        
    # Top highlight on text
    t_high = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    th_draw = ImageDraw.Draw(t_high)
    th_draw.text((tx, ty - 3), text, font=font, fill=(255, 185, 225, 175))
    text_grad = Image.alpha_composite(text_grad, t_high)
    
    img.paste(text_grad, (0, 0), text_mask)
    return img

def main():
    target_dir = "Assets/UI/CasualUI"
    os.makedirs(target_dir, exist_ok=True)
    
    create_restart_button().save(os.path.join(target_dir, "btn_restart.png"))
    print("Saved btn_restart.png")
    
    create_sound_button().save(os.path.join(target_dir, "btn_sound.png"))
    print("Saved btn_sound.png")
    
    create_haptic_button().save(os.path.join(target_dir, "btn_haptic.png"))
    print("Saved btn_haptic.png")
    
    create_hard_badge().save(os.path.join(target_dir, "badge_hard.png"))
    print("Saved badge_hard.png")

if __name__ == "__main__":
    main()
