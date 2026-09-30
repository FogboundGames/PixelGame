import os
import math
import numpy as np
import cv2
from PIL import Image, ImageDraw, ImageFilter
from ui_button_factory import create_squircle_base, composite_icon_with_3d_effects

def create_smooth_polygon(pts, radius=14, num_interp=16):
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

def draw_restart_icon_mask(size=1024):
    mask = np.zeros((size, size), dtype=np.uint8)
    cx, cy = 512, 506
    r_mid = 180.0
    w_stroke = 76.0
    
    start_angle = 50.0
    end_angle = 315.0
    
    # 1. Main circular sweep arc
    cv2.ellipse(mask, (int(cx), int(cy)), (int(r_mid), int(r_mid)), 0, start_angle, end_angle, 255, int(w_stroke), lineType=cv2.LINE_AA)
    
    # 2. Perfect rounded cap at tail (angle = 50 deg)
    s_rad = np.radians(start_angle)
    sx = int(cx + r_mid * np.cos(s_rad))
    sy = int(cy + r_mid * np.sin(s_rad))
    cv2.circle(mask, (sx, sy), int(w_stroke // 2), 255, -1, lineType=cv2.LINE_AA)
    
    # 3. Dynamic arrowhead at end_angle (315 deg, sweeping down-right)
    e_rad = np.radians(end_angle)
    hx = cx + r_mid * np.cos(e_rad)
    hy = cy + r_mid * np.sin(e_rad)
    
    tx = -np.sin(e_rad)
    ty =  np.cos(e_rad)
    nx =  np.cos(e_rad)
    ny =  np.sin(e_rad)
    
    tip_len = 120.0
    wing_w  = 84.0
    back_len = 24.0
    
    tip   = [hx + tx * tip_len, hy + ty * tip_len]
    w_out = [hx - tx * back_len + nx * wing_w, hy - ty * back_len + ny * wing_w]
    notch = [hx - tx * 8.0, hy - ty * 8.0]
    w_in  = [hx - tx * back_len - nx * wing_w, hy - ty * back_len - ny * wing_w]
    
    arrow_poly = create_smooth_polygon([tip, w_out, notch, w_in], radius=14)
    cv2.fillPoly(mask, [arrow_poly], 255, lineType=cv2.LINE_AA)
    
    mask = cv2.GaussianBlur(mask, (3, 3), 0.75)
    return mask

def draw_sound_icon_mask(size=1024):
    mask = np.zeros((size, size), dtype=np.uint8)
    cx, cy = 485, 506

    # 1. Back rectangular body
    back_box = np.array([
        [cx - 165, cy - 65],
        [cx - 85, cy - 65],
        [cx - 85, cy + 65],
        [cx - 165, cy + 65]
    ], dtype=np.int32)
    cv2.fillPoly(mask, [create_smooth_polygon(back_box, radius=18)], 255, lineType=cv2.LINE_AA)

    # 2. Expanding Horn Cone
    horn = np.array([
        [cx - 90, cy - 64],
        [cx + 25, cy - 148],
        [cx + 25, cy + 148],
        [cx - 90, cy + 64]
    ], dtype=np.int32)
    cv2.fillPoly(mask, [create_smooth_polygon(horn, radius=20)], 255, lineType=cv2.LINE_AA)

    # 3. Rounded mouth dome cap (seamlessly matches horn edge)
    cv2.ellipse(mask, (cx + 25, cy), (22, 148), 0, -90, 90, 255, -1, lineType=cv2.LINE_AA)

    # 4. Concentric Sound Waves
    wave1_r = 135
    wave1_w = 44
    cv2.ellipse(mask, (cx + 10, cy), (wave1_r, wave1_r), 0, -42, 42, 255, wave1_w, lineType=cv2.LINE_AA)
    for a in [-42, 42]:
        rad = np.radians(a)
        wx = int(cx + 10 + wave1_r * np.cos(rad))
        wy = int(cy + wave1_r * np.sin(rad))
        cv2.circle(mask, (wx, wy), wave1_w // 2, 255, -1, lineType=cv2.LINE_AA)

    wave2_r = 230
    wave2_w = 44
    cv2.ellipse(mask, (cx + 10, cy), (wave2_r, wave2_r), 0, -42, 42, 255, wave2_w, lineType=cv2.LINE_AA)
    for a in [-42, 42]:
        rad = np.radians(a)
        wx = int(cx + 10 + wave2_r * np.cos(rad))
        wy = int(cy + wave2_r * np.sin(rad))
        cv2.circle(mask, (wx, wy), wave2_w // 2, 255, -1, lineType=cv2.LINE_AA)

    mask = cv2.GaussianBlur(mask, (3, 3), 0.75)
    return mask

def draw_music_icon_mask(size=1024):
    mask = np.zeros((size, size), dtype=np.uint8)

    # Note heads (tilted candy ovals)
    h1_cx, h1_cy = 380, 630
    h2_cx, h2_cy = 645, 560
    cv2.ellipse(mask, (h1_cx, h1_cy), (78, 54), -26, 0, 360, 255, -1, lineType=cv2.LINE_AA)
    cv2.ellipse(mask, (h2_cx, h2_cy), (78, 54), -26, 0, 360, 255, -1, lineType=cv2.LINE_AA)

    stem_w = 42
    # Left stem
    s1_x = 430
    s1_top = 370
    s1_bot = 625
    # Right stem
    s2_x = 695
    s2_top = 300
    s2_bot = 555

    cv2.rectangle(mask, (s1_x - stem_w//2, s1_top), (s1_x + stem_w//2, s1_bot), 255, -1, lineType=cv2.LINE_AA)
    cv2.rectangle(mask, (s2_x - stem_w//2, s2_top), (s2_x + stem_w//2, s2_bot), 255, -1, lineType=cv2.LINE_AA)

    # Top slanted beam
    beam_thick = 74
    beam_pts = np.array([
        [s1_x - stem_w//2, s1_top - 10],
        [s2_x + stem_w//2 + 4, s2_top - 10],
        [s2_x + stem_w//2 + 4, s2_top + beam_thick],
        [s1_x - stem_w//2, s1_top + beam_thick]
    ], dtype=np.int32)
    cv2.fillPoly(mask, [create_smooth_polygon(beam_pts, radius=18)], 255, lineType=cv2.LINE_AA)

    mask = cv2.GaussianBlur(mask, (3, 3), 0.75)
    return mask

def draw_settings_icon_mask(size=1024):
    mask = np.zeros((size, size), dtype=np.uint8)
    cx, cy = 512, 506

    num_teeth = 6
    r_inner_hub = 152.0
    r_outer_tip = 248.0
    tooth_half_w = 48.0

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

    hub_rounded = create_smooth_polygon(hub_pts, radius=24)
    cv2.fillPoly(mask, [hub_rounded], 255, lineType=cv2.LINE_AA)
    cv2.circle(mask, (cx, cy), int(r_inner_hub + 5), 255, -1, lineType=cv2.LINE_AA)
    cv2.circle(mask, (cx, cy), 82, 0, -1, lineType=cv2.LINE_AA)

    mask = cv2.GaussianBlur(mask, (3, 3), 0.75)
    return mask

def main():
    out_dir = 'Assets/UI/CasualUI'
    os.makedirs(out_dir, exist_ok=True)
    os.makedirs('scratch', exist_ok=True)

    print('Rendering 1: btn_restart.png...')
    red_top = (255, 60, 75)
    red_bot = (215, 20, 38)
    base_restart = create_squircle_base(red_top, red_bot, size=1024)
    restart_mask = draw_restart_icon_mask(size=1024)
    final_restart = composite_icon_with_3d_effects(base_restart, restart_mask)
    final_restart_256 = final_restart.resize((256, 256), Image.Resampling.LANCZOS)
    final_restart_256.save(os.path.join(out_dir, 'btn_restart.png'))
    final_restart_256.save('scratch/new_btn_restart.png')

    print('Rendering 2: btn_sound.png...')
    green_top = (95, 230, 24)
    green_bot = (35, 178, 12)
    base_sound = create_squircle_base(green_top, green_bot, size=1024)
    sound_mask = draw_sound_icon_mask(size=1024)
    final_sound = composite_icon_with_3d_effects(base_sound, sound_mask)
    final_sound_256 = final_sound.resize((256, 256), Image.Resampling.LANCZOS)
    final_sound_256.save(os.path.join(out_dir, 'btn_sound.png'))
    final_sound_256.save('scratch/new_btn_sound.png')

    print('Rendering 3: btn_music.png...')
    base_music = create_squircle_base(green_top, green_bot, size=1024)
    music_mask = draw_music_icon_mask(size=1024)
    final_music = composite_icon_with_3d_effects(base_music, music_mask)
    final_music_256 = final_music.resize((256, 256), Image.Resampling.LANCZOS)
    final_music_256.save(os.path.join(out_dir, 'btn_music.png'))
    final_music_256.save('scratch/new_btn_music.png')

    print('Rendering 4: btn_settings.png...')
    purple_top = (180, 85, 245)
    purple_bot = (120, 35, 195)
    base_settings = create_squircle_base(purple_top, purple_bot, size=1024)
    settings_mask = draw_settings_icon_mask(size=1024)
    final_settings = composite_icon_with_3d_effects(base_settings, settings_mask)
    final_settings_140 = final_settings.resize((140, 140), Image.Resampling.LANCZOS)
    final_settings_140.save(os.path.join(out_dir, 'btn_settings.png'))
    final_settings_140.save('scratch/new_btn_settings.png')

    # Showcase Banner
    banner = Image.new('RGBA', (1080, 280), (245, 240, 230, 255))
    banner.paste(final_restart.resize((180, 180), Image.Resampling.LANCZOS), (80, 50), final_restart.resize((180, 180), Image.Resampling.LANCZOS))
    banner.paste(final_sound.resize((180, 180), Image.Resampling.LANCZOS), (330, 50), final_sound.resize((180, 180), Image.Resampling.LANCZOS))
    banner.paste(final_music.resize((180, 180), Image.Resampling.LANCZOS), (580, 50), final_music.resize((180, 180), Image.Resampling.LANCZOS))
    banner.paste(final_settings.resize((180, 180), Image.Resampling.LANCZOS), (830, 50), final_settings.resize((180, 180), Image.Resampling.LANCZOS))
    banner.save('scratch/new_buttons_showcase.png')
    print('All flawless icons generated and saved successfully!')

if __name__ == '__main__':
    main()
