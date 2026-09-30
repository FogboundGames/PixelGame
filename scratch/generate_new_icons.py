import os
import math
import numpy as np
import cv2
from PIL import Image, ImageDraw, ImageFilter
from ui_button_factory import create_squircle_base, composite_icon_with_3d_effects

def create_smooth_polygon(pts, radius=12, num_interp=16):
    """
    Rounds the corners of a 2D polygon using circular arc filleting.
    """
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
        
        # distance from vertex to tangent points
        t_dist = min(radius / np.tan(theta / 2.0), min(d1, d2) * 0.45)
        pt1 = p_curr + u1 * t_dist
        pt2 = p_curr + u2 * t_dist

        # interpolate arc between pt1 and pt2
        # quadratic bezier with control point at p_curr
        for s in np.linspace(0, 1, num_interp):
            p = (1 - s)**2 * pt1 + 2 * (1 - s) * s * p_curr + s**2 * pt2
            out_pts.append(p)
            
    return np.array(out_pts, dtype=np.int32)

def draw_retry_icon_mask(size=1024):
    """
    Creates a sleek, modern, perfectly curved circular retry arrow icon.
    """
    mask = np.zeros((size, size), dtype=np.uint8)
    cx, cy = 512, 510
    
    r_mid = 175.0
    w_stroke = 74.0
    r_out = r_mid + w_stroke / 2.0  # 212
    r_in  = r_mid - w_stroke / 2.0  # 138

    # Angles in degrees (standard math: 0 = East, 90 = South in image coordinates)
    # Start at bottom-right: 50 deg
    # Sweep clockwise around bottom, left, top to 290 deg
    a_start = 55.0
    a_end   = 292.0
    
    # Generate points along outer arc
    num_pts = 120
    angles = np.linspace(a_start, a_end, num_pts)
    outer_pts = []
    inner_pts = []
    
    for a in angles:
        rad = np.radians(a)
        outer_pts.append([cx + r_out * np.cos(rad), cy + r_out * np.sin(rad)])
        inner_pts.append([cx + r_in * np.cos(rad),  cy + r_in * np.sin(rad)])
        
    # Tail end cap (smooth semicircle at a_start)
    cap_center = [cx + r_mid * np.cos(np.radians(a_start)), cy + r_mid * np.sin(np.radians(a_start))]
    cap_pts = []
    for ca in np.linspace(a_start - 90, a_start + 90, 20):
        crad = np.radians(ca)
        cap_pts.append([cap_center[0] + (w_stroke / 2.0) * np.cos(crad), cap_center[1] + (w_stroke / 2.0) * np.sin(crad)])

    # Assemble arc body polygon
    arc_poly = outer_pts + [outer_pts[-1]] + inner_pts[::-1] + cap_pts
    cv2.fillPoly(mask, [np.array(arc_poly, dtype=np.int32)], 255, lineType=cv2.LINE_AA)

    # Arrowhead at a_end (292 deg, which is at x = cx + r*cos(292), y = cy + r*sin(292))
    # Tangent vector at 292 deg: (-sin(292), cos(292))
    head_rad = np.radians(a_end)
    hx = cx + r_mid * np.cos(head_rad)
    hy = cy + r_mid * np.sin(head_rad)

    # Tangent direction (clockwise): dx = -sin(head_rad), dy = cos(head_rad)
    tx = -np.sin(head_rad)
    ty =  np.cos(head_rad)
    # Normal direction (outwards): nx = cos(head_rad), ny = sin(head_rad)
    nx =  np.cos(head_rad)
    ny =  np.sin(head_rad)

    # Arrowhead dimensions
    head_len = 145.0
    head_wing = 82.0

    # Tip of arrow
    tip = [hx + tx * head_len, hy + ty * head_len]
    # Base left (outward) wing
    w_out = [hx - tx * 15 + nx * head_wing, hy - ty * 15 + ny * head_wing]
    # Base right (inward) wing
    w_in  = [hx - tx * 15 - nx * head_wing, hy - ty * 15 - ny * head_wing]
    # Notch/base center
    base_mid = [hx - tx * 5, hy - ty * 5]

    arrow_poly = create_smooth_polygon([tip, w_out, base_mid, w_in], radius=16)
    cv2.fillPoly(mask, [arrow_poly], 255, lineType=cv2.LINE_AA)

    # Smooth full mask slightly with anti-aliasing
    mask = cv2.GaussianBlur(mask, (3, 3), 0.75)
    return mask

def draw_sound_icon_mask(size=1024):
    """
    Creates a sleek casual game speaker icon with 2 elegant concentric sound waves.
    """
    mask = np.zeros((size, size), dtype=np.uint8)
    cx, cy = 490, 508

    # 1. Speaker body
    # Back rectangular housing
    back_box = np.array([
        [cx - 165, cy - 65],
        [cx - 85, cy - 65],
        [cx - 85, cy + 65],
        [cx - 165, cy + 65]
    ], dtype=np.int32)
    back_box_rounded = create_smooth_polygon(back_box, radius=18)
    cv2.fillPoly(mask, [back_box_rounded], 255, lineType=cv2.LINE_AA)

    # Expanding front cone / horn
    horn = np.array([
        [cx - 95, cy - 62],
        [cx + 25, cy - 150],
        [cx + 25, cy + 150],
        [cx - 95, cy + 62]
    ], dtype=np.int32)
    horn_rounded = create_smooth_polygon(horn, radius=22)
    cv2.fillPoly(mask, [horn_rounded], 255, lineType=cv2.LINE_AA)

    # Small rounded dome cap at front mouth of horn
    cv2.ellipse(mask, (cx + 25, cy), (16, 142), 0, -90, 90, 255, -1, lineType=cv2.LINE_AA)

    # 2. Sound waves (concentric arcs with rounded caps)
    wave1_r = 135
    wave1_w = 46
    cv2.ellipse(mask, (cx + 10, cy), (wave1_r, wave1_r), 0, -42, 42, 255, wave1_w, lineType=cv2.LINE_AA)
    # caps for wave 1
    for a in [-42, 42]:
        rad = np.radians(a)
        wx = int(cx + 10 + wave1_r * np.cos(rad))
        wy = int(cy + wave1_r * np.sin(rad))
        cv2.circle(mask, (wx, wy), wave1_w // 2, 255, -1, lineType=cv2.LINE_AA)

    wave2_r = 230
    wave2_w = 46
    cv2.ellipse(mask, (cx + 10, cy), (wave2_r, wave2_r), 0, -42, 42, 255, wave2_w, lineType=cv2.LINE_AA)
    # caps for wave 2
    for a in [-42, 42]:
        rad = np.radians(a)
        wx = int(cx + 10 + wave2_r * np.cos(rad))
        wy = int(cy + wave2_r * np.sin(rad))
        cv2.circle(mask, (wx, wy), wave2_w // 2, 255, -1, lineType=cv2.LINE_AA)

    mask = cv2.GaussianBlur(mask, (3, 3), 0.75)
    return mask

def draw_music_icon_mask(size=1024):
    """
    Creates a dynamic, joyful casual game double eighth-note (beamed notes ♫).
    """
    mask = np.zeros((size, size), dtype=np.uint8)

    # Left note head (tilted oval)
    cv2.ellipse(mask, (375, 625), (78, 54), -26, 0, 360, 255, -1, lineType=cv2.LINE_AA)
    # Right note head (higher pitch, positioned higher)
    cv2.ellipse(mask, (640, 555), (78, 54), -26, 0, 360, 255, -1, lineType=cv2.LINE_AA)

    # Stems
    stem_w = 42
    # Left stem: from right side of left head up to beam
    s1_x = 425
    s1_top = 370
    s1_bot = 620
    # Right stem: from right side of right head up to beam
    s2_x = 690
    s2_top = 300
    s2_bot = 550

    # Draw stems
    cv2.rectangle(mask, (s1_x - stem_w//2, s1_top), (s1_x + stem_w//2, s1_bot), 255, -1, lineType=cv2.LINE_AA)
    cv2.rectangle(mask, (s2_x - stem_w//2, s2_top), (s2_x + stem_w//2, s2_bot), 255, -1, lineType=cv2.LINE_AA)

    # Top slanted beam
    beam_thick = 76
    beam_pts = np.array([
        [s1_x - stem_w//2 - 12, s1_top - 15],
        [s2_x + stem_w//2 + 18, s2_top - 15],
        [s2_x + stem_w//2 + 18, s2_top + beam_thick],
        [s1_x - stem_w//2 - 12, s1_top + beam_thick]
    ], dtype=np.int32)
    beam_rounded = create_smooth_polygon(beam_pts, radius=20)
    cv2.fillPoly(mask, [beam_rounded], 255, lineType=cv2.LINE_AA)

    mask = cv2.GaussianBlur(mask, (3, 3), 0.75)
    return mask

def draw_settings_icon_mask(size=1024):
    """
    Creates a friendly, rounded 6-tooth toy gear/cogwheel.
    """
    mask = np.zeros((size, size), dtype=np.uint8)
    cx, cy = 512, 510

    num_teeth = 6
    r_inner_hub = 150.0
    r_outer_tip = 245.0
    tooth_half_w = 48.0

    hub_pts = []
    for i in range(num_teeth):
        angle_deg = i * (360.0 / num_teeth)
        rad = np.radians(angle_deg)
        # Tooth direction
        tx = np.cos(rad)
        ty = np.sin(rad)
        # Perpendicular
        px = -ty
        py = tx

        # Tooth 4 points
        p_base_left  = [cx + r_inner_hub * tx - tooth_half_w * 1.35 * px, cy + r_inner_hub * ty - tooth_half_w * 1.35 * py]
        p_tip_left   = [cx + r_outer_tip * tx - tooth_half_w * 0.90 * px, cy + r_outer_tip * ty - tooth_half_w * 0.90 * py]
        p_tip_right  = [cx + r_outer_tip * tx + tooth_half_w * 0.90 * px, cy + r_outer_tip * ty + tooth_half_w * 0.90 * py]
        p_base_right = [cx + r_inner_hub * tx + tooth_half_w * 1.35 * px, cy + r_inner_hub * ty + tooth_half_w * 1.35 * py]

        hub_pts.extend([p_base_left, p_tip_left, p_tip_right, p_base_right])

    hub_rounded = create_smooth_polygon(hub_pts, radius=24)
    cv2.fillPoly(mask, [hub_rounded], 255, lineType=cv2.LINE_AA)
    # Fill central disc
    cv2.circle(mask, (cx, cy), int(r_inner_hub + 5), 255, -1, lineType=cv2.LINE_AA)
    # Cut out center hole
    cv2.circle(mask, (cx, cy), 82, 0, -1, lineType=cv2.LINE_AA)

    mask = cv2.GaussianBlur(mask, (3, 3), 0.75)
    return mask

def main():
    out_dir = 'Assets/UI/CasualUI'
    os.makedirs(out_dir, exist_ok=True)
    os.makedirs('scratch', exist_ok=True)

    print('1. Generating Restart Button...')
    red_top = (255, 60, 75)
    red_bot = (215, 20, 38)
    base_restart = create_squircle_base(red_top, red_bot, size=1024)
    restart_mask = draw_retry_icon_mask(size=1024)
    final_restart = composite_icon_with_3d_effects(base_restart, restart_mask)
    final_restart_256 = final_restart.resize((256, 256), Image.Resampling.LANCZOS)
    final_restart_256.save(os.path.join(out_dir, 'btn_restart.png'))
    final_restart_256.save('scratch/new_btn_restart.png')

    print('2. Generating Sound Button...')
    green_top = (95, 230, 24)
    green_bot = (35, 178, 12)
    base_sound = create_squircle_base(green_top, green_bot, size=1024)
    sound_mask = draw_sound_icon_mask(size=1024)
    final_sound = composite_icon_with_3d_effects(base_sound, sound_mask)
    final_sound_256 = final_sound.resize((256, 256), Image.Resampling.LANCZOS)
    final_sound_256.save(os.path.join(out_dir, 'btn_sound.png'))
    final_sound_256.save('scratch/new_btn_sound.png')

    print('3. Generating Music Button...')
    base_music = create_squircle_base(green_top, green_bot, size=1024)
    music_mask = draw_music_icon_mask(size=1024)
    final_music = composite_icon_with_3d_effects(base_music, music_mask)
    final_music_256 = final_music.resize((256, 256), Image.Resampling.LANCZOS)
    final_music_256.save(os.path.join(out_dir, 'btn_music.png'))
    final_music_256.save('scratch/new_btn_music.png')

    print('4. Generating Settings Button...')
    purple_top = (180, 85, 245)
    purple_bot = (120, 35, 195)
    base_settings = create_squircle_base(purple_top, purple_bot, size=1024)
    settings_mask = draw_settings_icon_mask(size=1024)
    final_settings = composite_icon_with_3d_effects(base_settings, settings_mask)
    final_settings_140 = final_settings.resize((140, 140), Image.Resampling.LANCZOS)
    final_settings_140.save(os.path.join(out_dir, 'btn_settings.png'))
    final_settings_140.save('scratch/new_btn_settings.png')

    # Also create side-by-side comparison with the old buttons
    print('5. Creating visual comparison...')
    compare_img = Image.new('RGBA', (1080, 300), (242, 235, 225, 255))
    # Paste old buttons vs new buttons
    # Old
    if os.path.exists('scratch/current_restart_icon.png'):
        pass
    
    # Save a showcase banner of all new buttons
    banner = Image.new('RGBA', (1080, 280), (245, 240, 230, 255))
    banner.paste(final_restart.resize((180, 180), Image.Resampling.LANCZOS), (80, 50), final_restart.resize((180, 180), Image.Resampling.LANCZOS))
    banner.paste(final_sound.resize((180, 180), Image.Resampling.LANCZOS), (330, 50), final_sound.resize((180, 180), Image.Resampling.LANCZOS))
    banner.paste(final_music.resize((180, 180), Image.Resampling.LANCZOS), (580, 50), final_music.resize((180, 180), Image.Resampling.LANCZOS))
    banner.paste(final_settings.resize((180, 180), Image.Resampling.LANCZOS), (830, 50), final_settings.resize((180, 180), Image.Resampling.LANCZOS))
    banner.save('scratch/new_buttons_showcase.png')
    print('Done! Saved all new buttons to Assets/UI/CasualUI/ and showcase to scratch/new_buttons_showcase.png')

if __name__ == '__main__':
    main()
