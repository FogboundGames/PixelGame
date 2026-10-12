import math
import os
from PIL import Image, ImageDraw, ImageFilter
import numpy as np

def generate_textures():
    w, h = 340, 448
    scale = 4
    sw, sh = w * scale, h * scale

    top_y = int(20 * scale)
    bot_y = int(428 * scale)
    top_inset = int(14 * scale)
    margin_x = int(22 * scale)
    r = int(26 * scale)

    corners = [
        (margin_x + top_inset, top_y),
        (sw - margin_x - top_inset, top_y),
        (sw - margin_x, bot_y),
        (margin_x, bot_y)
    ]

    def get_rounded_poly(pts, radius):
        poly = []
        n = len(pts)
        for i in range(n):
            p_prev = pts[(i - 1) % n]
            p_curr = pts[i]
            p_next = pts[(i + 1) % n]
            v1 = (p_prev[0] - p_curr[0], p_prev[1] - p_curr[1])
            v2 = (p_next[0] - p_curr[0], p_next[1] - p_curr[1])
            len1 = math.hypot(*v1)
            len2 = math.hypot(*v2)
            u1 = (v1[0] / len1, v1[1] / len1)
            u2 = (v2[0] / len2, v2[1] / len2)
            t1 = (p_curr[0] + u1[0] * radius, p_curr[1] + u1[1] * radius)
            t2 = (p_curr[0] + u2[0] * radius, p_curr[1] + u2[1] * radius)
            steps = 16
            for s in range(steps + 1):
                st = s / float(steps)
                bx = (1 - st)**2 * t1[0] + 2 * (1 - st) * st * p_curr[0] + st**2 * t2[0]
                by = (1 - st)**2 * t1[1] + 2 * (1 - st) * st * p_curr[1] + st**2 * t2[1]
                poly.append((bx, by))
        return poly

    poly_pts = get_rounded_poly(corners, r)

    # =========================================================================
    # 1. EMPTY / TARGET BERTH (slot_berth_inner.png)
    #    - Thinner, lower contrast, subtle perspective dashes
    #    - Soft cyan target glow
    #    - Embedded dock mooring cleats at top edge
    #    - Subtle translucent water basin tint
    # =========================================================================
    empty_hi = Image.new('RGBA', (sw, sh), (0, 0, 0, 0))

    # A. Water Basin Tint
    f_layer = Image.new('RGBA', (sw, sh), (0, 0, 0, 0))
    f_draw = ImageDraw.Draw(f_layer)
    f_draw.polygon(poly_pts, fill=(150, 230, 255, 26))

    # B. Target Glow (Luminescence / Aura)
    g_layer = Image.new('RGBA', (sw, sh), (0, 0, 0, 0))
    g_draw = ImageDraw.Draw(g_layer)
    g_draw.line(poly_pts + [poly_pts[0]], fill=(110, 220, 255, 120), width=int(15 * scale), joint='curve')
    g_layer = g_layer.filter(ImageFilter.GaussianBlur(radius=6.5 * scale))

    g_core = Image.new('RGBA', (sw, sh), (0, 0, 0, 0))
    gc_draw = ImageDraw.Draw(g_core)
    gc_draw.line(poly_pts + [poly_pts[0]], fill=(185, 245, 255, 155), width=int(7 * scale), joint='curve')
    g_core = g_core.filter(ImageFilter.GaussianBlur(radius=3.0 * scale))

    # C. Refined Dashes
    d_layer = Image.new('RGBA', (sw, sh), (0, 0, 0, 0))
    d_draw = ImageDraw.Draw(d_layer)

    total_pts = poly_pts + [poly_pts[0]]
    cum_dists = [0.0]
    for i in range(len(total_pts) - 1):
        d = math.hypot(total_pts[i+1][0] - total_pts[i][0], total_pts[i+1][1] - total_pts[i][1])
        cum_dists.append(cum_dists[-1] + d)

    perimeter = cum_dists[-1]
    dash_len = 24.0 * scale
    gap_len = 17.0 * scale
    cycle_len = dash_len + gap_len
    num_cycles = round(perimeter / cycle_len)
    actual_cycle = perimeter / num_cycles
    actual_dash = actual_cycle * (dash_len / cycle_len)

    def get_pt_at_dist(d_query):
        d_query = d_query % perimeter
        for i in range(len(cum_dists) - 1):
            if cum_dists[i] <= d_query <= cum_dists[i+1]:
                t = (d_query - cum_dists[i]) / (cum_dists[i+1] - cum_dists[i] + 1e-6)
                x = total_pts[i][0] + (total_pts[i+1][0] - total_pts[i][0]) * t
                y = total_pts[i][1] + (total_pts[i+1][1] - total_pts[i][1]) * t
                return (x, y)
        return total_pts[0]

    line_w = int(4.2 * scale)
    dash_color = (230, 246, 255, 175) # Refined, semi-transparent nautical cyan-white

    for k in range(num_cycles):
        start_d = k * actual_cycle
        end_d = start_d + actual_dash
        sub_steps = 10
        dash_pts = []
        for s in range(sub_steps + 1):
            cur_d = start_d + (end_d - start_d) * (s / float(sub_steps))
            dash_pts.append(get_pt_at_dist(cur_d))
        d_draw.line(dash_pts, fill=dash_color, width=line_w, joint='curve')

    # D. Dock Cleats / Mooring Attachment Tabs at top edge
    cleat_layer = Image.new('RGBA', (sw, sh), (0, 0, 0, 0))
    cl_draw = ImageDraw.Draw(cleat_layer)
    cw, ch = int(14 * scale), int(7 * scale)
    cl_draw.rounded_rectangle([margin_x + top_inset - 2*scale, top_y - 2*scale, margin_x + top_inset + cw, top_y + ch], radius=int(3*scale), fill=(210, 235, 255, 160))
    cl_draw.rounded_rectangle([sw - margin_x - top_inset - cw, top_y - 2*scale, sw - margin_x - top_inset + 2*scale, top_y + ch], radius=int(3*scale), fill=(210, 235, 255, 160))
    cleat_layer = cleat_layer.filter(ImageFilter.GaussianBlur(radius=1.2 * scale))

    empty_hi = Image.alpha_composite(empty_hi, f_layer)
    empty_hi = Image.alpha_composite(empty_hi, g_layer)
    empty_hi = Image.alpha_composite(empty_hi, g_core)
    empty_hi = Image.alpha_composite(empty_hi, d_layer)
    empty_hi = Image.alpha_composite(empty_hi, cleat_layer)

    empty_final = empty_hi.resize((w, h), Image.Resampling.LANCZOS)
    empty_path = 'Assets/Textures/Marina/slot_berth_inner.png'
    empty_final.save(empty_path)
    print(f'Saved {empty_path}')

    # =========================================================================
    # 2. DOCKED SHIP CONTACT SHADOW & WATER WAKE (slot_berth_docked.png)
    #    - Deep ambient occlusion contact shadow under the hull
    #    - Water contact meniscus / calm wake ring
    # =========================================================================
    docked_hi = Image.new('RGBA', (sw, sh), (0, 0, 0, 0))
    ship_w = int(222 * scale)
    ship_h = int(320 * scale)
    cx = sw // 2
    cy = int((top_y + bot_y) * 0.5 + 8 * scale)
    s_r = int(30 * scale)

    # Ambient soft hull occlusion shadow
    s_layer = Image.new('RGBA', (sw, sh), (0, 0, 0, 0))
    s_draw = ImageDraw.Draw(s_layer)
    s_draw.rounded_rectangle([cx - ship_w//2, cy - ship_h//2, cx + ship_w//2, cy + ship_h//2], radius=s_r, fill=(5, 20, 46, 200))
    s_layer = s_layer.filter(ImageFilter.GaussianBlur(radius=8.5 * scale))

    # Core keel contact shadow
    core_layer = Image.new('RGBA', (sw, sh), (0, 0, 0, 0))
    c_draw = ImageDraw.Draw(core_layer)
    c_draw.rounded_rectangle([cx - (ship_w - 24*scale)//2, cy - (ship_h - 24*scale)//2, cx + (ship_w - 24*scale)//2, cy + (ship_h - 24*scale)//2], radius=s_r, fill=(3, 14, 32, 185))
    core_layer = core_layer.filter(ImageFilter.GaussianBlur(radius=3.8 * scale))

    # Water contact ring (calm hull mooring meniscus)
    wake_layer = Image.new('RGBA', (sw, sh), (0, 0, 0, 0))
    w_draw = ImageDraw.Draw(wake_layer)
    w_draw.rounded_rectangle([cx - ship_w//2 - int(4*scale), cy - ship_h//2 - int(3*scale), cx + ship_w//2 + int(4*scale), cy + ship_h//2 + int(5*scale)], radius=s_r + int(4*scale), outline=(180, 240, 255, 115), width=int(3.2*scale))
    wake_layer = wake_layer.filter(ImageFilter.GaussianBlur(radius=2.2 * scale))

    docked_hi = Image.alpha_composite(docked_hi, s_layer)
    docked_hi = Image.alpha_composite(docked_hi, core_layer)
    docked_hi = Image.alpha_composite(docked_hi, wake_layer)

    docked_final = docked_hi.resize((w, h), Image.Resampling.LANCZOS)
    docked_path = 'Assets/Textures/Marina/slot_berth_docked.png'
    docked_final.save(docked_path)
    print(f'Saved {docked_path}')

    # =========================================================================
    # 3. CLOSED / LOCKED BERTH (slot_berth_closed.png)
    #    - Subtle diagonal stripes, perspective aligned
    # =========================================================================
    closed_hi = Image.new('RGBA', (sw, sh), (0, 0, 0, 0))

    mask_layer = Image.new('L', (sw, sh), 0)
    m_draw = ImageDraw.Draw(mask_layer)
    m_draw.polygon(poly_pts, fill=255)

    pat_layer = Image.new('RGBA', (sw, sh), (16, 32, 54, 50))
    p_draw = ImageDraw.Draw(pat_layer)
    diag_w = int(7 * scale)
    diag_gap = int(14 * scale)
    for d in range(-sh, sw + sh, diag_w + diag_gap):
        p_draw.line([(d, 0), (d + sh, sh)], fill=(160, 185, 210, 42), width=diag_w)

    closed_hi.paste(pat_layer, (0, 0), mask_layer)

    b_layer = Image.new('RGBA', (sw, sh), (0, 0, 0, 0))
    b_draw = ImageDraw.Draw(b_layer)
    b_draw.line(poly_pts + [poly_pts[0]], fill=(170, 195, 215, 90), width=int(2.5 * scale), joint='curve')
    b_layer = b_layer.filter(ImageFilter.GaussianBlur(radius=1.0 * scale))
    closed_hi = Image.alpha_composite(closed_hi, b_layer)

    closed_final = closed_hi.resize((w, h), Image.Resampling.LANCZOS)
    closed_path = 'Assets/Textures/Marina/slot_berth_closed.png'
    closed_final.save(closed_path)
    print(f'Saved {closed_path}')

if __name__ == '__main__':
    generate_textures()
