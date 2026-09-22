import os
from PIL import Image, ImageDraw

def make_comparison():
    user_img_path = r"c:\Users\ezgid\.gemini\antigravity-ide\brain\aa4c8cd2-dae3-4a8a-b626-4bc1fdfdce7c\.user_uploaded\media_1790072622372.png"
    ref_img_path = r"c:\Users\ezgid\.gemini\antigravity-ide\brain\aa4c8cd2-dae3-4a8a-b626-4bc1fdfdce7c\.user_uploaded\media_1790071002340.png"
    showcase_path = r"c:\Users\ezgid\OneDrive\Masaüstü\PixelGame\Assets\Editor\kawaii_faces_showcase.png"
    out_path = r"c:\Users\ezgid\.gemini\antigravity-ide\brain\aa4c8cd2-dae3-4a8a-b626-4bc1fdfdce7c\kawaii_faces_final_comparison.png"

    # Load images
    user_img = Image.open(user_img_path)
    ref_img = Image.open(ref_img_path)
    showcase = Image.open(showcase_path)

    # Crop conveyor portion from showcase
    # Belt is around y: 95 to 295
    conveyor_crop = showcase.crop((30, 95, showcase.width - 30, 295))

    cw, ch = 960, 480
    res = Image.new("RGB", (cw, ch), (15, 21, 48))
    draw = ImageDraw.Draw(res)

    # Title
    draw.text((25, 18), "BEFORE vs AFTER: POSITION CORRECTION & 5 KAWAII FACES", fill=(255, 255, 255))

    # Panel 1: Before (User Screenshot crop)
    draw.text((25, 52), "[BEFORE] Cube pushed onto bottom rail, no face", fill=(240, 100, 100))
    u_crop = user_img.resize((440, 170), Image.Resampling.LANCZOS)
    res.paste(u_crop, (25, 78))

    # Panel 2: Reference Image (User wanted this)
    draw.text((495, 52), "[REFERENCE] Target look: centered + cute faces", fill=(100, 220, 150))
    r_crop = ref_img.resize((440, 170), Image.Resampling.LANCZOS)
    res.paste(r_crop, (495, 78))

    # Panel 3: After (Centered with 5 Expressive Faces)
    draw.text((25, 265), "[AFTER] Adjusted offset (0.14f) centered on arrows + 5 Expressive Faces (Yüzler)", fill=(80, 210, 255))
    c_res = conveyor_crop.resize((910, 165), Image.Resampling.LANCZOS)
    res.paste(c_res, (25, 290))

    res.save(out_path, "PNG")
    print(f"Comparison saved: {out_path}")

if __name__ == "__main__":
    make_comparison()
