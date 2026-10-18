import os
from PIL import Image, ImageDraw, ImageFilter

def create_clean_reference_background():
    ref_path = r"C:\Users\ezgid\.gemini\antigravity-ide\brain\7e607747-85f0-4ade-aaed-60466c67547e\.user_uploaded\media_1792335487082.jpg"
    ref = Image.open(ref_path).convert("RGBA")
    
    target_w, target_h = 1080, 1920
    out_img = Image.new("RGBA", (target_w, target_h), (252, 222, 148, 255))
    draw = ImageDraw.Draw(out_img)
    
    # 1. Base Sand Gradient from Y=0 to 1920
    for y in range(target_h):
        t = y / target_h
        r = int(253 - t * 4)
        g = int(226 - t * 14)
        b = int(158 - t * 24)
        draw.line([(0, y), (target_w, y)], fill=(r, g, b, 255))
        
    # 2. Extract top harbor scenery
    # Width 682, height 120
    top_strip = ref.crop((0, 0, ref.width, 105))
    top_strip_clean = top_strip.copy()
    ts_draw = ImageDraw.Draw(top_strip_clean)
    
    # Sample clean water color and clean sand color
    water_color = (0, 185, 245, 255)
    sand_top_color = (252, 226, 158, 255)
    
    # Inpaint/cover the baked UI buttons on the top strip:
    # Button 1 (lifebuoy): X=50..120, Y=30..90 -> water/pier
    # Button 2 (1X): X=120..190, Y=30..90 -> water/pier
    # Center Level banner: X=240..445, Y=20..75 -> pure water & shore
    # Button 4 (sound): X=495..560, Y=30..90 -> water/shore
    # Button 5 (vibe): X=565..630, Y=30..90 -> water/pier
    
    # We want to keep the stacked containers in the top-left (X=0..60) and top-right (X=620..682)
    # and fill the middle with beautiful turquoise water, shoreline, and sand!
    
    # Fill water across center
    for y in range(0, 60):
        # subtle water caustics / gradient
        caustic_g = int(175 + (y / 60.0) * 20)
        caustic_b = int(240 + (y / 60.0) * 15)
        ts_draw.line([(60, y), (ref.width - 60, y)], fill=(0, caustic_g, caustic_b, 255))
        
    # Shoreline curve (Y = 60 to 75)
    for x in range(60, ref.width - 60):
        wave_y = int(58 + 4 * (0.5 + 0.5 * (x / 100.0) % 1.0))
        # White foam line
        ts_draw.line([(x, wave_y), (x, wave_y + 3)], fill=(255, 255, 255, 220))
        # Wet sand
        ts_draw.line([(x, wave_y + 4), (x, wave_y + 12)], fill=(235, 205, 140, 255))
        # Golden sand
        ts_draw.line([(x, wave_y + 13), (x, 105)], fill=sand_top_color)
        
    # Scale top strip to target 1080 width
    scaled_h = int(105 * (target_w / ref.width))
    scaled_top = top_strip_clean.resize((target_w, scaled_h), Image.Resampling.LANCZOS)
    
    # Soft fade at bottom of scaled top strip
    fade_mask = Image.new("L", (target_w, scaled_h), 255)
    fm_draw = ImageDraw.Draw(fade_mask)
    f_start = int(scaled_h * 0.75)
    for y in range(f_start, scaled_h):
        a = int(255 * (1.0 - (y - f_start) / (scaled_h - f_start)))
        fm_draw.line([(0, y), (target_w, y)], fill=a)
        
    out_img.paste(scaled_top, (0, 0), fade_mask)
    
    out_path1 = "Assets/Kenney/BeachBackground_Clean.png"
    out_path2 = "Assets/Kenney/GroundBackground_Clean.png"
    out_img.save(out_path1, "PNG")
    out_img.save(out_path2, "PNG")
    print(f"Generated clean background without baked buttons to {out_path1}")

if __name__ == "__main__":
    create_clean_reference_background()
