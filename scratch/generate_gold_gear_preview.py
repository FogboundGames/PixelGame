import os, math, shutil
from PIL import Image, ImageDraw, ImageFilter

artifact_dir = r"C:\Users\ezgid\.gemini\antigravity-ide\brain\e40f8c0a-1bd9-4ddf-997a-0ae99ed8cfa0"
os.makedirs(artifact_dir, exist_ok=True)

def make_gear_gold_v2(angle_deg=0, size=512):
    '''Variant B: Royal Blue Cog with Gleaming 24K Gold Inlays & Gold Mechanical Crown'''
    ss = 4
    W = size * ss
    H = size * ss
    cx, cy = W / 2.0, H / 2.0
    
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    
    # 1. Soft Shadow
    shadow = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    sdraw = ImageDraw.Draw(shadow)
    r_shadow = 205 * ss
    sdraw.ellipse([cx - r_shadow, cy - r_shadow + 8*ss, cx + r_shadow, cy + r_shadow + 8*ss], fill=(5, 10, 25, 180))
    shadow = shadow.filter(ImageFilter.GaussianBlur(14 * ss))
    img.paste(shadow, (0, 0), shadow)
    
    # 2. Outer Collar: High-contrast Dark Navy + Vibrant Gold Ring
    r_outer_housing = 192 * ss
    draw.ellipse([cx - r_outer_housing, cy - r_outer_housing, cx + r_outer_housing, cy + r_outer_housing], 
                 fill=(22, 38, 80, 255), outline=(250, 204, 21, 255), width=int(5 * ss))
    r_inner_well = 180 * ss
    draw.ellipse([cx - r_inner_well, cy - r_inner_well, cx + r_inner_well, cy + r_inner_well], 
                 fill=(10, 18, 36, 255), outline=(15, 30, 60, 255), width=int(3 * ss))
    
    # 3. Rotating Cog: Royal Blue Teeth with Gold Rim Bevel & Golden Tip Caps
    num_teeth = 8
    r_outer = 174 * ss
    r_inner = 126 * ss
    tooth_w_angle = 360.0 / (num_teeth * 2.2)
    
    points = []
    outer_tips = []
    for i in range(num_teeth):
        base_a = math.radians(angle_deg + i * (360.0 / num_teeth))
        half_w = math.radians(tooth_w_angle / 2.0)
        a0 = base_a - half_w * 1.4
        points.append((cx + r_inner * math.cos(a0), cy + r_inner * math.sin(a0)))
        a1 = base_a - half_w * 0.95
        points.append((cx + (r_inner + 12*ss) * math.cos(a1), cy + (r_inner + 12*ss) * math.sin(a1)))
        a2 = base_a - half_w * 0.65
        p2 = (cx + r_outer * math.cos(a2), cy + r_outer * math.sin(a2))
        points.append(p2)
        a3 = base_a + half_w * 0.65
        p3 = (cx + r_outer * math.cos(a3), cy + r_outer * math.sin(a3))
        points.append(p3)
        outer_tips.append((p2, p3, base_a))
        a4 = base_a + half_w * 0.95
        points.append((cx + (r_inner + 12*ss) * math.cos(a4), cy + (r_inner + 12*ss) * math.sin(a4)))
        a5 = base_a + half_w * 1.4
        points.append((cx + r_inner * math.cos(a5), cy + r_inner * math.sin(a5)))
        
    gear_mask = Image.new('L', (W, H), 0)
    gdraw = ImageDraw.Draw(gear_mask)
    gdraw.polygon(points, fill=255)
    gdraw.ellipse([cx - r_inner, cy - r_inner, cx + r_inner, cy + r_inner], fill=255)
    
    # Tooth Drop Shadow
    tooth_shadow = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    ts_draw = ImageDraw.Draw(tooth_shadow)
    ts_offset_y = 6 * ss
    ts_draw.polygon([(p[0], p[1] + ts_offset_y) for p in points], fill=(4, 8, 18, 190))
    ts_draw.ellipse([cx - r_inner, cy - r_inner + ts_offset_y, cx + r_inner, cy + r_inner + ts_offset_y], fill=(4, 8, 18, 190))
    tooth_shadow = tooth_shadow.filter(ImageFilter.GaussianBlur(6 * ss))
    img.paste(tooth_shadow, (0, 0), tooth_shadow)
    
    # Teeth Surface: Royal Toy Blue (#3B82F6 -> #1E3A8A)
    gear_surf = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    gs_draw = ImageDraw.Draw(gear_surf)
    for y in range(int(cy - r_outer), int(cy + r_outer) + 1):
        t = (y - (cy - r_outer)) / (2.0 * r_outer)
        t = max(0.0, min(1.0, t))
        r = int(59 * (1 - t) + 20 * t)
        g = int(130 * (1 - t) + 50 * t)
        b = int(246 * (1 - t) + 150 * t)
        gs_draw.line([(0, y), (W, y)], fill=(r, g, b, 255))
        
    gear_colored = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    gear_colored.paste(gear_surf, (0, 0), gear_mask)
    
    # Golden Bevel Outline on teeth!
    out_draw = ImageDraw.Draw(gear_colored)
    out_draw.polygon(points, outline=(255, 215, 0, 240), width=int(4 * ss))
    
    # Golden Tooth Tip Caps (Accent Inlays on each tooth)
    for p2, p3, base_a in outer_tips:
        tip_r = 160 * ss
        cap_p1 = (cx + tip_r * math.cos(base_a - 0.12), cy + tip_r * math.sin(base_a - 0.12))
        cap_p4 = (cx + tip_r * math.cos(base_a + 0.12), cy + tip_r * math.sin(base_a + 0.12))
        out_draw.polygon([cap_p1, p2, p3, cap_p4], fill=(255, 195, 20, 255), outline=(255, 245, 160, 255), width=int(1.5*ss))
        
    img.paste(gear_colored, (0, 0), gear_colored)
    
    # 4. Polished Golden Hub Ring (Replaces plain chrome with Golden Bezel)
    r_gold_out = 120 * ss
    r_gold_in = 80 * ss
    gold_mask = Image.new('L', (W, H), 0)
    cdraw = ImageDraw.Draw(gold_mask)
    cdraw.ellipse([cx - r_gold_out, cy - r_gold_out, cx + r_gold_out, cy + r_gold_out], fill=255)
    cdraw.ellipse([cx - r_gold_in, cy - r_gold_in, cx + r_gold_in, cy + r_gold_in], fill=0)
    
    gold_surf = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    cs_draw = ImageDraw.Draw(gold_surf)
    for y in range(int(cy - r_gold_out), int(cy + r_gold_out) + 1):
        t = (y - (cy - r_gold_out)) / (2.0 * r_gold_out)
        t = max(0.0, min(1.0, t))
        v = math.sin(t * math.pi * 1.5)
        gr = int(255 - 40 * v)
        gg = int(215 - 65 * v)
        gb = int(50 - 35 * v)
        cs_draw.line([(0, y), (W, y)], fill=(max(180, min(255, gr)), max(120, min(240, gg)), max(10, min(100, gb)), 255))
    img.paste(gold_surf, (0, 0), gold_mask)
    
    draw.ellipse([cx - r_gold_out, cy - r_gold_out, cx + r_gold_out, cy + r_gold_out], outline=(255, 250, 200, 240), width=int(2.5 * ss))
    draw.ellipse([cx - r_gold_in, cy - r_gold_in, cx + r_gold_in, cy + r_gold_in], outline=(160, 90, 10, 255), width=int(2.5 * ss))
    
    # 4b. Deep Blue Sapphire Rivets on the Golden Hub
    r_rivets = 100 * ss
    num_rivets = 4
    for ri in range(num_rivets):
        ra = math.radians(angle_deg + ri * (360.0 / num_rivets))
        rx = cx + r_rivets * math.cos(ra)
        ry = cy + r_rivets * math.sin(ra)
        r_rad = 7.5 * ss
        draw.ellipse([rx - r_rad, ry - r_rad, rx + r_rad, ry + r_rad], fill=(24, 75, 200, 255), outline=(147, 197, 253, 255), width=int(1.5 * ss))
        draw.ellipse([rx - r_rad*0.5, ry - r_rad*0.5, rx + r_rad*0.1, ry + r_rad*0.1], fill=(200, 235, 255, 240))
        
    # 5. Glowing Cyan Core
    r_hub = 76 * ss
    draw.ellipse([cx - r_hub, cy - r_hub, cx + r_hub, cy + r_hub], fill=(8, 16, 36, 255))
    glow_core = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    gcdraw = ImageDraw.Draw(glow_core)
    r_neon = 52 * ss
    gcdraw.ellipse([cx - r_neon, cy - r_neon, cx + r_neon, cy + r_neon], outline=(0, 235, 255, 255), width=int(8 * ss))
    glow_core = glow_core.filter(ImageFilter.GaussianBlur(10 * ss))
    img.paste(glow_core, (0, 0), glow_core)
    
    r_dome = 45 * ss
    for r in range(int(r_dome), 0, -1):
        t = r / float(r_dome)
        cr = int(0 * t + 220 * (1 - t*t))
        cg = int(185 * t + 248 * (1 - t))
        cb = 255
        draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(cr, cg, cb, 255))
    sp_cx, sp_cy = cx - 12 * ss, cy - 12 * ss
    draw.ellipse([sp_cx - 14*ss, sp_cy - 9*ss, sp_cx + 14*ss, sp_cy + 9*ss], fill=(255, 255, 255, 240))
    
    return img.resize((size, size), Image.Resampling.LANCZOS)

print("Rendering 36 animation frames for Option B...")
frames = []
for i in range(36):
    f = make_gear_gold_v2(i * 10.0)
    frames.append(f)

# Save looping GIF to scratch and artifact dir
gif_path = "scratch/gear_gold_v2_loop.gif"
frames[0].save(gif_path, save_all=True, append_images=frames[1:], duration=50, loop=0, disposal=2)
shutil.copy2(gif_path, os.path.join(artifact_dir, "gear_gold_v2_loop.gif"))
print("Saved gear_gold_v2_loop.gif successfully!")
