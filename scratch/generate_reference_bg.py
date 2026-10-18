import os
from PIL import Image, ImageDraw, ImageFilter

def generate_reference_background():
    ref_path = r"C:\Users\ezgid\.gemini\antigravity-ide\brain\7e607747-85f0-4ade-aaed-60466c67547e\.user_uploaded\media_1792335487082.jpg"
    ref = Image.open(ref_path)
    
    target_w, target_h = 1080, 1920
    out_img = Image.new("RGBA", (target_w, target_h), (252, 222, 148, 255))
    draw = ImageDraw.Draw(out_img)
    
    # 1. Base Sand Gradient from Y=0 to 1920
    # Matches the exact colors sampled from reference: (253, 226, 156) at top to (251, 214, 136) at bottom
    for y in range(target_h):
        t = y / target_h
        r = int(253 - t * 4)
        g = int(226 - t * 14)
        b = int(158 - t * 24)
        draw.line([(0, y), (target_w, y)], fill=(r, g, b, 255))
        
    # 2. Extract the top harbor / port portion from the reference image
    # Top portion height in ref is ~115px (above HUD buttons)
    # The reference is 682x1024. Let's crop the top 120 pixels:
    ref_top = ref.crop((0, 0, ref.width, 120))
    # Resize to target width (1080) and height proportional (~190)
    scaled_top_h = int(120 * (target_w / ref.width))
    ref_top_scaled = ref_top.resize((target_w, scaled_top_h), Image.Resampling.LANCZOS).convert("RGBA")
    
    # Create soft alpha fade at the bottom of the harbor strip into the sand
    mask = Image.new("L", (target_w, scaled_top_h), 255)
    mask_draw = ImageDraw.Draw(mask)
    fade_start = int(scaled_top_h * 0.70)
    for y in range(fade_start, scaled_top_h):
        alpha = int(255 * (1.0 - (y - fade_start) / (scaled_top_h - fade_start)))
        mask_draw.line([(0, y), (target_w, y)], fill=alpha)
        
    out_img.paste(ref_top_scaled, (0, 0), mask)
    
    # Save as BeachBackground_Clean.png and GroundBackground_Clean.png
    out_path1 = "Assets/Kenney/BeachBackground_Clean.png"
    out_path2 = "Assets/Kenney/GroundBackground_Clean.png"
    
    out_img.save(out_path1, "PNG")
    out_img.save(out_path2, "PNG")
    print(f"Generated seamless reference background to {out_path1}")

if __name__ == "__main__":
    generate_reference_background()
