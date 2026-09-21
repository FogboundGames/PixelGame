import os
import math
import shutil
from PIL import Image, ImageDraw, ImageFilter
import numpy as np

os.makedirs("Assets/UI/GearFrames", exist_ok=True)
os.makedirs("Assets/Resources/GearFrames", exist_ok=True)
os.makedirs("scratch", exist_ok=True)

artifact_dir = r"C:\Users\ezgid\.gemini\antigravity-ide\brain\e40f8c0a-1bd9-4ddf-997a-0ae99ed8cfa0"
os.makedirs(artifact_dir, exist_ok=True)

def render_frame_clean(angle_deg, size=512):
    # Supersampling factor for ultra crisp anti-aliasing
    ss = 4
    W = size * ss
    H = size * ss
    cx = W / 2.0
    cy = H / 2.0
    
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    
    # 1. Soft Ambient Outer Drop Shadow
    shadow = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    sdraw = ImageDraw.Draw(shadow)
    r_shadow = 205 * ss
    sdraw.ellipse([cx - r_shadow, cy - r_shadow + 8*ss, cx + r_shadow, cy + r_shadow + 8*ss], fill=(5, 10, 25, 180))
    shadow = shadow.filter(ImageFilter.GaussianBlur(14 * ss))
    img.paste(shadow, (0, 0), shadow)
    
    # 2. Static Outer Housing / Corner Mounting Well (Deep Navy & Toy Blue Rim)
    r_outer_housing = 192 * ss
    draw.ellipse([cx - r_outer_housing, cy - r_outer_housing, cx + r_outer_housing, cy + r_outer_housing], fill=(30, 75, 170, 255), outline=(147, 197, 253, 240), width=int(4 * ss))
    r_inner_well = 180 * ss
    draw.ellipse([cx - r_inner_well, cy - r_inner_well, cx + r_inner_well, cy + r_inner_well], fill=(10, 18, 36, 255), outline=(15, 30, 60, 255), width=int(3 * ss))
    
    # 3. Rotating Cog Wheel
    num_teeth = 8
    r_outer = 174 * ss
    r_inner = 126 * ss
    tooth_w_angle = 360.0 / (num_teeth * 2.2)
    
    points = []
    for i in range(num_teeth):
        base_a = math.radians(angle_deg + i * (360.0 / num_teeth))
        half_w = math.radians(tooth_w_angle / 2.0)
        
        a0 = base_a - half_w * 1.4
        points.append((cx + r_inner * math.cos(a0), cy + r_inner * math.sin(a0)))
        a1 = base_a - half_w * 0.95
        points.append((cx + (r_inner + 12*ss) * math.cos(a1), cy + (r_inner + 12*ss) * math.sin(a1)))
        a2 = base_a - half_w * 0.65
        points.append((cx + r_outer * math.cos(a2), cy + r_outer * math.sin(a2)))
        a3 = base_a + half_w * 0.65
        points.append((cx + r_outer * math.cos(a3), cy + r_outer * math.sin(a3)))
        a4 = base_a + half_w * 0.95
        points.append((cx + (r_inner + 12*ss) * math.cos(a4), cy + (r_inner + 12*ss) * math.sin(a4)))
        a5 = base_a + half_w * 1.4
        points.append((cx + r_inner * math.cos(a5), cy + r_inner * math.sin(a5)))
        
    gear_mask = Image.new('L', (W, H), 0)
    gdraw = ImageDraw.Draw(gear_mask)
    gdraw.polygon(points, fill=255)
    gdraw.ellipse([cx - r_inner, cy - r_inner, cx + r_inner, cy + r_inner], fill=255)
    
    # Tooth Drop Shadow onto recessed well
    tooth_shadow = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    ts_draw = ImageDraw.Draw(tooth_shadow)
    ts_offset_y = 6 * ss
    ts_draw.polygon([(p[0], p[1] + ts_offset_y) for p in points], fill=(4, 8, 18, 190))
    ts_draw.ellipse([cx - r_inner, cy - r_inner + ts_offset_y, cx + r_inner, cy + r_inner + ts_offset_y], fill=(4, 8, 18, 190))
    tooth_shadow = tooth_shadow.filter(ImageFilter.GaussianBlur(6 * ss))
    img.paste(tooth_shadow, (0, 0), tooth_shadow)
    
    # Shading on gear body: Royal Toy Blue with rich 3D gradient (Brighter, vibrant, no greyness)
    gear_surf = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    gs_draw = ImageDraw.Draw(gear_surf)
    for y in range(int(cy - r_outer), int(cy + r_outer) + 1):
        t = (y - (cy - r_outer)) / (2.0 * r_outer)
        t = max(0.0, min(1.0, t))
        # Smooth transition from top highlight blue #408CFA to deep rich blue #123088
        r = int(64 * (1 - t) + 18 * t)
        g = int(142 * (1 - t) + 48 * t)
        b = int(252 * (1 - t) + 138 * t)
        gs_draw.line([(0, y), (W, y)], fill=(r, g, b, 255))
        
    gear_colored = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    gear_colored.paste(gear_surf, (0, 0), gear_mask)
    
    # Precision Bevel Outline on teeth (light sky blue rim)
    out_draw = ImageDraw.Draw(gear_colored)
    out_draw.polygon(points, outline=(190, 225, 255, 230), width=int(3.5 * ss))
    img.paste(gear_colored, (0, 0), gear_colored)
    
    # 4. Polished Metallic Chrome Wheel Hub
    r_chrome_out = 120 * ss
    r_chrome_in = 82 * ss
    chrome_mask = Image.new('L', (W, H), 0)
    cdraw = ImageDraw.Draw(chrome_mask)
    cdraw.ellipse([cx - r_chrome_out, cy - r_chrome_out, cx + r_chrome_out, cy + r_chrome_out], fill=255)
    cdraw.ellipse([cx - r_chrome_in, cy - r_chrome_in, cx + r_chrome_in, cy + r_chrome_in], fill=0)
    
    chrome_surf = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    cs_draw = ImageDraw.Draw(chrome_surf)
    for y in range(int(cy - r_chrome_out), int(cy + r_chrome_out) + 1):
        t = (y - (cy - r_chrome_out)) / (2.0 * r_chrome_out)
        t = max(0.0, min(1.0, t))
        v = int(245 - 85 * math.sin(t * math.pi * 1.5))
        v = max(130, min(255, v))
        cs_draw.line([(0, y), (W, y)], fill=(v - 8, v + 2, v + 12, 255))
    img.paste(chrome_surf, (0, 0), chrome_mask)
    
    draw.ellipse([cx - r_chrome_out, cy - r_chrome_out, cx + r_chrome_out, cy + r_chrome_out], outline=(240, 248, 255, 240), width=int(2.5 * ss))
    draw.ellipse([cx - r_chrome_in, cy - r_chrome_in, cx + r_chrome_in, cy + r_chrome_in], outline=(30, 48, 80, 255), width=int(2.5 * ss))
    
    # 4b. Tiny Golden Mechanical Rivets on the Chrome Ring (Rotating with gear!)
    r_rivets = 101 * ss
    num_rivets = 4
    for ri in range(num_rivets):
        ra = math.radians(angle_deg + ri * (360.0 / num_rivets))
        rx = cx + r_rivets * math.cos(ra)
        ry = cy + r_rivets * math.sin(ra)
        r_rad = 7 * ss
        draw.ellipse([rx - r_rad, ry - r_rad, rx + r_rad, ry + r_rad], fill=(245, 160, 20, 255), outline=(255, 230, 140, 255), width=int(1.5 * ss))
        draw.ellipse([rx - r_rad*0.5, ry - r_rad*0.5, rx + r_rad*0.1, ry + r_rad*0.1], fill=(255, 250, 210, 230))
        
    # 5. Central Power Hub: Deep Recess + Neon Cyan Glow + 3D Jewel Dome
    r_hub = 78 * ss
    draw.ellipse([cx - r_hub, cy - r_hub, cx + r_hub, cy + r_hub], fill=(8, 16, 36, 255))
    
    # Neon Cyan Glow Ring (Bloom effect)
    glow_core = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    gcdraw = ImageDraw.Draw(glow_core)
    r_neon = 54 * ss
    gcdraw.ellipse([cx - r_neon, cy - r_neon, cx + r_neon, cy + r_neon], outline=(0, 225, 255, 255), width=int(8 * ss))
    glow_core = glow_core.filter(ImageFilter.GaussianBlur(10 * ss))
    img.paste(glow_core, (0, 0), glow_core)
    
    # 3D Power Dome / Jewel
    r_dome = 46 * ss
    for r in range(int(r_dome), 0, -1):
        t = r / float(r_dome)
        cr = int(0 * t + 220 * (1 - t*t))
        cg = int(185 * t + 248 * (1 - t))
        cb = 255
        draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(cr, cg, cb, 255))
        
    # Specular Gleam on Dome
    sp_cx = cx - 12 * ss
    sp_cy = cy - 12 * ss
    draw.ellipse([sp_cx - 14*ss, sp_cy - 9*ss, sp_cx + 14*ss, sp_cy + 9*ss], fill=(255, 255, 255, 240))
    
    # ZERO GREY OVERLAY!
    
    return img.resize((size, size), Image.Resampling.LANCZOS)

print("Rendering 36 clean frames...")
frames = []
for i in range(36):
    angle = i * (360.0 / 36) # 10 deg step
    f = render_frame_clean(angle)
    frames.append(f)
    
    # Save individual frame to both folders
    f_path1 = f"Assets/UI/GearFrames/gear_{i:02d}.png"
    f_path2 = f"Assets/Resources/GearFrames/gear_{i:02d}.png"
    f.save(f_path1, "PNG")
    f.save(f_path2, "PNG")

print(f"Rendered and saved 36 frames to Assets/UI/GearFrames and Assets/Resources/GearFrames.")

# Save animated GIFs
gif_path1 = "Assets/UI/ChatGPT Image_unity_gear.gif"
gif_path2 = "Assets/UI/unity_gear_hypercasual_clean.gif"
gif_scratch = "scratch/corner_gear_loop.gif"
gif_artifact = os.path.join(artifact_dir, "corner_gear_clean_loop.gif")

# Duration: 50ms per frame = 20 fps = 1.8s per full 360 rotation
frames[0].save(
    gif_path1,
    save_all=True,
    append_images=frames[1:],
    duration=50,
    loop=0,
    disposal=2
)
shutil.copy2(gif_path1, gif_path2)
shutil.copy2(gif_path1, gif_scratch)
shutil.copy2(gif_path1, gif_artifact)

print("Saved animated GIFs successfully.")

# Create side-by-side comparison image
# User uploaded image crop vs clean gear
user_crop = Image.open(r"C:\Users\ezgid\.gemini\antigravity-ide\brain\e40f8c0a-1bd9-4ddf-997a-0ae99ed8cfa0\.user_uploaded\media_1790022030622.png")
clean_frame0 = frames[0]

comp_w = 900
comp_h = 500
comp = Image.new("RGBA", (comp_w, comp_h), (16, 24, 46, 255))
cdraw = ImageDraw.Draw(comp)

# Title & cards
cdraw.rectangle([0, 0, comp_w, 60], fill=(24, 36, 68, 255))
cdraw.text((comp_w // 2 - 190, 18), "DIŞLİ GRİLİK DÜZELTMESİ (ÖNCE & SONRA)", fill=(255, 255, 255, 255))

# Left: Previous with grey mask
cdraw.rectangle([40, 80, 420, 460], fill=(20, 30, 58, 255), outline=(220, 60, 60, 200), width=3)
uc_res = user_crop.resize((320, 320), Image.Resampling.NEAREST)
comp.paste(uc_res, (70, 95), uc_res if uc_res.mode == 'RGBA' else None)
cdraw.text((120, 425), "ÖNCE: Üstte Grilik Vardı", fill=(248, 113, 113, 255))

# Right: Clean frame 0
cdraw.rectangle([480, 80, 860, 460], fill=(20, 30, 58, 255), outline=(52, 211, 153, 200), width=3)
cf_res = clean_frame0.resize((320, 320), Image.Resampling.LANCZOS)
comp.paste(cf_res, (510, 95), cf_res)
cdraw.text((540, 425), "ŞİMDİ: Pürüzsüz & Temiz Mavi", fill=(52, 211, 153, 255))

comp_out_scratch = "scratch/gear_no_grey_comparison.png"
comp_out_artifact = os.path.join(artifact_dir, "gear_no_grey_comparison.png")
comp.save(comp_out_scratch)
comp.save(comp_out_artifact)
print(f"Saved comparison to {comp_out_scratch} and {comp_out_artifact}")
