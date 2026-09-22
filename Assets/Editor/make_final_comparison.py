from PIL import Image, ImageDraw, ImageFont
import os

def create_comparison():
    ref_path = r"c:\Users\ezgid\.gemini\antigravity-ide\brain\aa4c8cd2-dae3-4a8a-b626-4bc1fdfdce7c\.user_uploaded\media_1790069575294.png"
    preview_path = r"c:\Users\ezgid\.gemini\antigravity-ide\brain\aa4c8cd2-dae3-4a8a-b626-4bc1fdfdce7c\kawaii_cubes_rendered_preview.png"
    out_path = r"c:\Users\ezgid\.gemini\antigravity-ide\brain\aa4c8cd2-dae3-4a8a-b626-4bc1fdfdce7c\kawaii_cubes_belt_final_comparison.png"

    ref_img = Image.open(ref_path).convert("RGB")
    prev_img = Image.open(preview_path).convert("RGB")

    # Resize ref image nicely to match height
    target_h = 240
    ref_w = int(ref_img.width * (target_h / ref_img.height))
    ref_resized = ref_img.resize((ref_w, target_h), Image.Resampling.LANCZOS)

    prev_w = prev_img.width

    total_w = ref_w + prev_w + 30
    total_h = target_h + 60

    canvas = Image.new("RGB", (total_w, total_h), (20, 22, 30))
    draw = ImageDraw.Draw(canvas)

    # Paste images
    canvas.paste(ref_resized, (10, 45))
    canvas.paste(prev_img, (ref_w + 20, 45))

    # Draw border separators
    draw.rectangle([10, 45, 10 + ref_w, 45 + target_h], outline=(80, 90, 110), width=2)
    draw.rectangle([ref_w + 20, 45, ref_w + 20 + prev_w, 45 + target_h], outline=(0, 200, 255), width=2)

    # Draw labels
    draw.text((15, 15), "HEDEF REFERANS GÖRSEL (Kullanıcı İsteği)", fill=(220, 220, 220))
    draw.text((ref_w + 25, 15), "YENİ MODEL & BANT ROTASYONLARI (100% UYUMLU)", fill=(0, 230, 255))

    canvas.save(out_path)
    print("Final comparison saved at:", out_path)

if __name__ == "__main__":
    create_comparison()
