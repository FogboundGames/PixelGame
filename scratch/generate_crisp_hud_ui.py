import os
import math
import numpy as np
import cv2
from PIL import Image, ImageDraw, ImageFilter

def create_smooth_polygon(pts, radius=18, num_interp=16):
    n = len(pts)
    out_pts = []
    for i in range(n):
        p_prev = np.array(pts[(i - 1) % n], dtype=np.float32)
        p_curr = np.array(pts[i], dtype=np.float32)
        p_next = np.array(pts[(i + 1) % n], dtype=np.float32)

        v1 = p_prev - p_curr
        v2 = p_next - p_curr
        d1 = np.linalg.norm(v1)
        d2 = np.linalg.norm(v2)
        if d1 < 1e-4 or d2 < 1e-4:
            out_pts.append(p_curr)
            continue

        u1 = v1 / d1
        u2 = v2 / d2
        cos_theta = np.clip(np.dot(u1, u2), -0.999, 0.999)
        theta = np.arccos(cos_theta)
        
        t_dist = min(radius / np.tan(theta / 2.0), min(d1, d2) * 0.45)
        pt1 = p_curr + u1 * t_dist
        pt2 = p_curr + u2 * t_dist

        for s in np.linspace(0, 1, num_interp):
            p = (1 - s)**2 * pt1 + 2 * (1 - s) * s * p_curr + s**2 * pt2
            out_pts.append(p)
            
    return np.array(out_pts, dtype=np.int32)

def create_crisp_squircle_base(top_color, bot_color, size=1024):
    """
    Renders an ultra-crisp, vibrant casual game 3D button frame:
    - 3D Gold bottom bevel tray
    - Thick solid white outer border
    - Vibrant saturated candy jelly gradient core
    - Sleek curved glass reflection on top
    """
    canvas = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    margin = 60
    radius = 250
    
    # 1. Soft Drop Shadow (tight and punchy)
    shadow = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    s_draw = ImageDraw.Draw(shadow)
    s_draw.rounded_rectangle(
        [margin, margin + 46, size - margin, size - margin + 46],
        radius=radius,
        fill=(0, 0, 0, 130)
    )
    shadow = shadow.filter(ImageFilter.GaussianBlur(24))
    canvas = Image.alpha_composite(canvas, shadow)

    draw = ImageDraw.Draw(canvas)

    # 2. Bottom Golden Bevel Tray
    gold_dark = (175, 105, 8, 255)
    gold_mid  = (255, 195, 30, 255)
    draw.rounded_rectangle([margin - 4, margin + 40, size - margin + 4, size - margin + 40], radius=radius + 4, fill=gold_dark)
    draw.rounded_rectangle([margin - 4, margin + 22, size - margin + 4, size - margin + 22], radius=radius + 4, fill=gold_mid)

    # 3. Thick Crisp White Outer Border
    draw.rounded_rectangle([margin, margin, size - margin, size - margin], radius=radius, fill=(255, 255, 255, 255))

    # Inner subtle rim bevel
    rim_sh = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    rs_draw = ImageDraw.Draw(rim_sh)
    rs_draw.rounded_rectangle([margin + 4, margin + 14, size - margin - 4, size - margin + 4], radius=radius - 4, fill=(210, 205, 195, 160))
    rim_sh = rim_sh.filter(ImageFilter.GaussianBlur(8))
    canvas = Image.alpha_composite(canvas, rim_sh)

    # 4. Candy Jelly Gradient Core
    c_margin = margin + 32
    inner_w = size - 2 * c_margin
    inner_h = inner_w
    inner_img = Image.new('RGBA', (inner_w, inner_h), (0, 0, 0, 0))
    i_draw = ImageDraw.Draw(inner_img)

    for y in range(inner_h):
        t = y / float(inner_h)
        t_curv = t * t * (3.0 - 2.0 * t)
        cr = int(top_color[0] * (1.0 - t_curv) + bot_color[0] * t_curv)
        cg = int(top_color[1] * (1.0 - t_curv) + bot_color[1] * t_curv)
        cb = int(top_color[2] * (1.0 - t_curv) + bot_color[2] * t_curv)
        i_draw.line([0, y, inner_w, y], fill=(cr, cg, cb, 255))

    core_mask = Image.new('L', (inner_w, inner_h), 0)
    m_draw = ImageDraw.Draw(core_mask)
    m_draw.rounded_rectangle([0, 0, inner_w, inner_h], radius=radius - 42, fill=255)

    # Bottom inner shadow
    bot_sh = Image.new('RGBA', (inner_w, inner_h), (0, 0, 0, 0))
    bs_draw = ImageDraw.Draw(bot_sh)
    bs_draw.rectangle([0, int(inner_h * 0.65), inner_w, inner_h], fill=(0, 0, 0, 75))
    bot_sh = bot_sh.filter(ImageFilter.GaussianBlur(24))
    inner_img = Image.alpha_composite(inner_img, bot_sh)

    # Top jelly highlight
    gloss = Image.new('RGBA', (inner_w, inner_h), (0, 0, 0, 0))
    g_draw = ImageDraw.Draw(gloss)
    g_draw.ellipse([-inner_w * 0.30, -inner_h * 0.75, inner_w * 1.30, inner_h * 0.52], fill=(255, 255, 255, 140))
    gloss = gloss.filter(ImageFilter.GaussianBlur(16))
    inner_img = Image.alpha_composite(inner_img, gloss)

    core_full = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    core_full.paste(inner_img, (c_margin, c_margin), core_mask)
    canvas = Image.alpha_composite(canvas, core_full)

    return canvas

def composite_crisp_icon(base_button, mask_np, dark_shadow_color=(0, 0, 0, 190)):
    """
    Composites the icon with MAXIMUM clarity, punch, and 3D depth:
    - Solid crisp contact drop shadow (y = +12) with minimal blur
    - Pure white body (255, 255, 255)
    - Specular top rim highlight
    """
    size = base_button.size[0]
    out = base_button.copy()

    # 1. Solid Contact Drop Shadow (offset y = +14, very tight blur for sharpness)
    sh_np = np.zeros((size, size), dtype=np.uint8)
    sh_np[14:, :] = mask_np[:-14, :]
    sh_pil = Image.fromarray(sh_np, 'L').filter(ImageFilter.GaussianBlur(6))
    sh_layer = Image.new('RGBA', (size, size), dark_shadow_color)
    sh_layer.putalpha(sh_pil)
    out = Image.alpha_composite(out, sh_layer)

    # 2. Pure White Body with subtle top-to-bottom shading
    body = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    b_draw = ImageDraw.Draw(body)
    for y in range(size):
        t = y / float(size)
        v = int(255 - t * 12)
        b_draw.line([0, y, size, y], fill=(v, v, min(255, v + 2), 255))
    
    icon_pil_mask = Image.fromarray(mask_np, 'L')
    body.putalpha(icon_pil_mask)
    out = Image.alpha_composite(out, body)

    # 3. Specular Top Rim Highlight
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7))
    eroded = cv2.erode(mask_np, kernel)
    hl_np = np.clip(mask_np.astype(np.int16) - eroded.astype(np.int16), 0, 255).astype(np.uint8)
    hl_np[int(size * 0.55):, :] = 0
    hl_pil = Image.fromarray(hl_np, 'L').filter(ImageFilter.GaussianBlur(2))
    hl_layer = Image.new('RGBA', (size, size), (255, 255, 255, 175))
    hl_layer.putalpha(hl_pil)
    out = Image.alpha_composite(out, hl_layer)

    return out

def draw_slash_on_button(img, p1=(710, 314), p2=(314, 710), width_outer=72, width_inner=48):
    """
    Draws a bold, 3D red diagonal strike-through slash with white border.
    """
    slash_overlay = Image.new('RGBA', img.size, (0, 0, 0, 0))
    sh = Image.new('RGBA', img.size, (0, 0, 0, 0))
    sh_draw = ImageDraw.Draw(sh)
    
    # Drop shadow for slash
    sh_draw.line([(p1[0], p1[1]+12), (p2[0], p2[1]+12)], fill=(0, 0, 0, 160), width=width_outer)
    sh_draw.ellipse([p1[0]-width_outer//2, p1[1]+12-width_outer//2, p1[0]+width_outer//2, p1[1]+12+width_outer//2], fill=(0, 0, 0, 160))
    sh_draw.ellipse([p2[0]-width_outer//2, p2[1]+12-width_outer//2, p2[0]+width_outer//2, p2[1]+12+width_outer//2], fill=(0, 0, 0, 160))
    sh = sh.filter(ImageFilter.GaussianBlur(8))
    
    # White border
    s_draw = ImageDraw.Draw(slash_overlay)
    s_draw.line([p1, p2], fill=(255, 255, 255, 255), width=width_outer)
    s_draw.ellipse([p1[0]-width_outer//2, p1[1]-width_outer//2, p1[0]+width_outer//2, p1[1]+width_outer//2], fill=(255, 255, 255, 255))
    s_draw.ellipse([p2[0]-width_outer//2, p2[1]-width_outer//2, p2[0]+width_outer//2, p2[1]+width_outer//2], fill=(255, 255, 255, 255))
    
    # Red core
    red_col = (235, 30, 45, 255)
    s_draw.line([p1, p2], fill=red_col, width=width_inner)
    s_draw.ellipse([p1[0]-width_inner//2, p1[1]-width_inner//2, p1[0]+width_inner//2, p1[1]+width_inner//2], fill=red_col)
    s_draw.ellipse([p2[0]-width_inner//2, p2[1]-width_inner//2, p2[0]+width_inner//2, p2[1]+width_inner//2], fill=red_col)
    
    res = Image.alpha_composite(img, sh)
    res = Image.alpha_composite(res, slash_overlay)
    return res

# --- ICON MASKS ---

def get_bold_restart_mask(size=1024):
    mask = np.zeros((size, size), dtype=np.uint8)
    cx, cy = 512, 502
    r_mid = 250.0
    w_stroke = 110.0
    
    start_angle = 50.0
    end_angle = 312.0
    
    cv2.ellipse(mask, (int(cx), int(cy)), (int(r_mid), int(r_mid)), 0, start_angle, end_angle, 255, int(w_stroke), lineType=cv2.LINE_AA)
    
    s_rad = np.radians(start_angle)
    sx = int(cx + r_mid * np.cos(s_rad))
    sy = int(cy + r_mid * np.sin(s_rad))
    cv2.circle(mask, (sx, sy), int(w_stroke // 2), 255, -1, lineType=cv2.LINE_AA)
    
    e_rad = np.radians(end_angle)
    hx = cx + r_mid * np.cos(e_rad)
    hy = cy + r_mid * np.sin(e_rad)
    
    tx = -np.sin(e_rad)
    ty =  np.cos(e_rad)
    nx =  np.cos(e_rad)
    ny =  np.sin(e_rad)
    
    tip_len = 165.0
    wing_w  = 120.0
    back_len = 35.0
    
    tip   = [hx + tx * tip_len, hy + ty * tip_len]
    w_out = [hx - tx * back_len + nx * wing_w, hy - ty * back_len + ny * wing_w]
    notch = [hx - tx * 12.0, hy - ty * 12.0]
    w_in  = [hx - tx * back_len - nx * wing_w, hy - ty * back_len - ny * wing_w]
    
    arrow_poly = create_smooth_polygon([tip, w_out, notch, w_in], radius=20)
    cv2.fillPoly(mask, [arrow_poly], 255, lineType=cv2.LINE_AA)
    return mask

def get_bold_sound_mask(size=1024):
    mask = np.zeros((size, size), dtype=np.uint8)
    cx, cy = 465, 502
    
    # 1. Back box
    bx1, bx2 = cx - 225, cx - 125
    by1, by2 = cy - 82, cy + 82
    cv2.rectangle(mask, (bx1, by1), (bx2, by2), 255, -1, lineType=cv2.LINE_AA)
    cv2.circle(mask, (bx1 + 18, by1 + 18), 18, 255, -1, lineType=cv2.LINE_AA)
    cv2.circle(mask, (bx1 + 18, by2 - 18), 18, 255, -1, lineType=cv2.LINE_AA)
    
    # 2. Smooth Horn with rounded mouth dome
    pts = [
        [cx - 130, cy - 80],
        [cx + 35,  cy - 195],
        [cx + 58,  cy - 120],
        [cx + 68,  cy],
        [cx + 58,  cy + 120],
        [cx + 35,  cy + 195],
        [cx - 130, cy + 80]
    ]
    cv2.fillPoly(mask, [np.array(pts, dtype=np.int32)], 255, lineType=cv2.LINE_AA)
    
    # 3. Waves
    w_stroke = 54
    wave1_r = 180
    cv2.ellipse(mask, (cx + 15, cy), (wave1_r, wave1_r), 0, -42, 42, 255, w_stroke, lineType=cv2.LINE_AA)
    for a in [-42, 42]:
        rad = np.radians(a)
        wx = int(cx + 15 + wave1_r * np.cos(rad))
        wy = int(cy + wave1_r * np.sin(rad))
        cv2.circle(mask, (wx, wy), w_stroke // 2, 255, -1, lineType=cv2.LINE_AA)

    wave2_r = 300
    cv2.ellipse(mask, (cx + 15, cy), (wave2_r, wave2_r), 0, -42, 42, 255, w_stroke, lineType=cv2.LINE_AA)
    for a in [-42, 42]:
        rad = np.radians(a)
        wx = int(cx + 15 + wave2_r * np.cos(rad))
        wy = int(cy + wave2_r * np.sin(rad))
        cv2.circle(mask, (wx, wy), w_stroke // 2, 255, -1, lineType=cv2.LINE_AA)

    return mask

def get_bold_haptic_mask(size=1024):
    mask = np.zeros((size, size), dtype=np.uint8)
    cx, cy = 512, 502
    
    pw, ph = 240, 390
    phone_box = np.array([
        [cx - pw//2, cy - ph//2],
        [cx + pw//2, cy - ph//2],
        [cx + pw//2, cy + ph//2],
        [cx - pw//2, cy + ph//2]
    ], dtype=np.int32)
    phone_rounded = create_smooth_polygon(phone_box, radius=52)
    cv2.fillPoly(mask, [phone_rounded], 255, lineType=cv2.LINE_AA)
    
    sw, sh = 170, 270
    screen_box = np.array([
        [cx - sw//2, cy - sh//2 - 6],
        [cx + sw//2, cy - sh//2 - 6],
        [cx + sw//2, cy + sh//2 - 6],
        [cx - sw//2, cy + sh//2 - 6]
    ], dtype=np.int32)
    screen_rounded = create_smooth_polygon(screen_box, radius=24)
    cv2.fillPoly(mask, [screen_rounded], 0, lineType=cv2.LINE_AA)
    
    cv2.circle(mask, (cx, cy + ph//2 - 28), 14, 0, -1, lineType=cv2.LINE_AA)
    cv2.line(mask, (cx - 24, cy - ph//2 + 25), (cx + 24, cy - ph//2 + 25), 0, 10, lineType=cv2.LINE_AA)
    
    w_stroke = 42
    # Left vibration waves ((
    cv2.ellipse(mask, (cx - pw//2 + 40, cy), (160, 210), 0, 130, 230, 255, w_stroke, lineType=cv2.LINE_AA)
    for a in [130, 230]:
        rad = np.radians(a)
        wx = int(cx - pw//2 + 40 + 160 * np.cos(rad))
        wy = int(cy + 210 * np.sin(rad))
        cv2.circle(mask, (wx, wy), w_stroke // 2, 255, -1, lineType=cv2.LINE_AA)
        
    cv2.ellipse(mask, (cx - pw//2 + 40, cy), (235, 290), 0, 140, 220, 255, w_stroke, lineType=cv2.LINE_AA)
    for a in [140, 220]:
        rad = np.radians(a)
        wx = int(cx - pw//2 + 40 + 235 * np.cos(rad))
        wy = int(cy + 290 * np.sin(rad))
        cv2.circle(mask, (wx, wy), w_stroke // 2, 255, -1, lineType=cv2.LINE_AA)

    # Right vibration waves ))
    cv2.ellipse(mask, (cx + pw//2 - 40, cy), (160, 210), 0, -50, 50, 255, w_stroke, lineType=cv2.LINE_AA)
    for a in [-50, 50]:
        rad = np.radians(a)
        wx = int(cx + pw//2 - 40 + 160 * np.cos(rad))
        wy = int(cy + 210 * np.sin(rad))
        cv2.circle(mask, (wx, wy), w_stroke // 2, 255, -1, lineType=cv2.LINE_AA)
        
    cv2.ellipse(mask, (cx + pw//2 - 40, cy), (235, 290), 0, -40, 40, 255, w_stroke, lineType=cv2.LINE_AA)
    for a in [-40, 40]:
        rad = np.radians(a)
        wx = int(cx + pw//2 - 40 + 235 * np.cos(rad))
        wy = int(cy + 290 * np.sin(rad))
        cv2.circle(mask, (wx, wy), w_stroke // 2, 255, -1, lineType=cv2.LINE_AA)

    return mask

def get_bold_settings_mask(size=1024):
    mask = np.zeros((size, size), dtype=np.uint8)
    cx, cy = 512, 502

    num_teeth = 6
    r_inner_hub = 160.0
    r_outer_tip = 265.0
    tooth_half_w = 54.0

    hub_pts = []
    for i in range(num_teeth):
        angle_deg = i * (360.0 / num_teeth)
        rad = np.radians(angle_deg)
        tx = np.cos(rad)
        ty = np.sin(rad)
        px = -ty
        py = tx

        p_base_left  = [cx + r_inner_hub * tx - tooth_half_w * 1.35 * px, cy + r_inner_hub * ty - tooth_half_w * 1.35 * py]
        p_tip_left   = [cx + r_outer_tip * tx - tooth_half_w * 0.90 * px, cy + r_outer_tip * ty - tooth_half_w * 0.90 * py]
        p_tip_right  = [cx + r_outer_tip * tx + tooth_half_w * 0.90 * px, cy + r_outer_tip * ty + tooth_half_w * 0.90 * py]
        p_base_right = [cx + r_inner_hub * tx + tooth_half_w * 1.35 * px, cy + r_inner_hub * ty + tooth_half_w * 1.35 * py]

        hub_pts.extend([p_base_left, p_tip_left, p_tip_right, p_base_right])

    hub_rounded = create_smooth_polygon(hub_pts, radius=26)
    cv2.fillPoly(mask, [hub_rounded], 255, lineType=cv2.LINE_AA)
    cv2.circle(mask, (cx, cy), int(r_inner_hub + 5), 255, -1, lineType=cv2.LINE_AA)
    cv2.circle(mask, (cx, cy), 90, 0, -1, lineType=cv2.LINE_AA)

    return mask

def main():
    target_dir = 'Assets/UI/CasualUI'
    os.makedirs(target_dir, exist_ok=True)
    os.makedirs('scratch', exist_ok=True)

    # Color palettes
    red_top   = (255, 60, 75)
    red_bot   = (215, 20, 38)
    green_top = (95, 230, 24)
    green_bot = (35, 178, 12)
    purple_top= (180, 85, 245)
    purple_bot= (120, 35, 195)

    base_red    = create_crisp_squircle_base(red_top, red_bot, size=1024)
    base_green  = create_crisp_squircle_base(green_top, green_bot, size=1024)
    base_purple = create_crisp_squircle_base(purple_top, purple_bot, size=1024)

    export_size = (512, 512)

    # 1. Restart Button
    print('Generating btn_restart.png...')
    m_rst = get_bold_restart_mask(1024)
    btn_rst = composite_crisp_icon(base_red, m_rst, dark_shadow_color=(120, 10, 20, 220))
    btn_rst_exp = btn_rst.resize(export_size, Image.Resampling.LANCZOS)
    btn_rst_exp.save(os.path.join(target_dir, 'btn_restart.png'))

    # 2. Sound Button (ON)
    print('Generating btn_sound.png...')
    m_snd = get_bold_sound_mask(1024)
    btn_snd = composite_crisp_icon(base_green, m_snd, dark_shadow_color=(15, 90, 8, 220))
    btn_snd_exp = btn_snd.resize(export_size, Image.Resampling.LANCZOS)
    btn_snd_exp.save(os.path.join(target_dir, 'btn_sound.png'))

    # 3. Sound Button (OFF)
    print('Generating btn_sound_off.png...')
    btn_snd_off = draw_slash_on_button(btn_snd, p1=(710, 314), p2=(314, 710))
    btn_snd_off_exp = btn_snd_off.resize(export_size, Image.Resampling.LANCZOS)
    btn_snd_off_exp.save(os.path.join(target_dir, 'btn_sound_off.png'))

    # 4. Haptic Button (ON) - Titreşim
    print('Generating btn_haptic.png...')
    m_hap = get_bold_haptic_mask(1024)
    btn_hap = composite_crisp_icon(base_green, m_hap, dark_shadow_color=(15, 90, 8, 220))
    btn_hap_exp = btn_hap.resize(export_size, Image.Resampling.LANCZOS)
    btn_hap_exp.save(os.path.join(target_dir, 'btn_haptic.png'))

    # 5. Haptic Button (OFF) - Titreşim Engel
    print('Generating btn_haptic_off.png...')
    btn_hap_off = draw_slash_on_button(btn_hap, p1=(710, 314), p2=(314, 710))
    btn_hap_off_exp = btn_hap_off.resize(export_size, Image.Resampling.LANCZOS)
    btn_hap_off_exp.save(os.path.join(target_dir, 'btn_haptic_off.png'))

    # 6. Settings Button
    print('Generating btn_settings.png...')
    m_set = get_bold_settings_mask(1024)
    btn_set = composite_crisp_icon(base_purple, m_set, dark_shadow_color=(60, 15, 100, 220))
    btn_set_exp = btn_set.resize(export_size, Image.Resampling.LANCZOS)
    btn_set_exp.save(os.path.join(target_dir, 'btn_settings.png'))

    # 7. Showcase Preview of all buttons
    print('Generating scratch showcase banner...')
    banner = Image.new('RGBA', (1200, 480), (245, 240, 230, 255))
    d = ImageDraw.Draw(banner)
    d.text((40, 20), 'YENI CRİSP CASUAL BUTONLAR (512x512, Ultra Net & 3D):', fill=(40, 40, 40, 255))

    b_size = (180, 180)
    # Row 1: Active buttons
    banner.paste(btn_rst.resize(b_size, Image.Resampling.LANCZOS), (60, 60), btn_rst.resize(b_size, Image.Resampling.LANCZOS))
    banner.paste(btn_snd.resize(b_size, Image.Resampling.LANCZOS), (280, 60), btn_snd.resize(b_size, Image.Resampling.LANCZOS))
    banner.paste(btn_hap.resize(b_size, Image.Resampling.LANCZOS), (500, 60), btn_hap.resize(b_size, Image.Resampling.LANCZOS))
    banner.paste(btn_set.resize(b_size, Image.Resampling.LANCZOS), (720, 60), btn_set.resize(b_size, Image.Resampling.LANCZOS))
    
    # Row 2: Toggled Off buttons
    d.text((40, 255), 'KAPALI DURUMLAR (Ses Kapali & Titresim Engel):', fill=(180, 40, 40, 255))
    banner.paste(btn_snd_off.resize(b_size, Image.Resampling.LANCZOS), (280, 285), btn_snd_off.resize(b_size, Image.Resampling.LANCZOS))
    banner.paste(btn_hap_off.resize(b_size, Image.Resampling.LANCZOS), (500, 285), btn_hap_off.resize(b_size, Image.Resampling.LANCZOS))

    banner.save('scratch/new_crisp_buttons_showcase.png')
    print('All crisp icons generated and saved successfully!')

if __name__ == '__main__':
    main()
