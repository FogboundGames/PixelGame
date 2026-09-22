import os
from PIL import Image, ImageDraw

def render_comparison_final():
    user_screen_path = r"c:\Users\ezgid\.gemini\antigravity-ide\brain\aa4c8cd2-dae3-4a8a-b626-4bc1fdfdce7c\.user_uploaded\media_1790073736908.png"
    ref_screen_path = r"c:\Users\ezgid\.gemini\antigravity-ide\brain\aa4c8cd2-dae3-4a8a-b626-4bc1fdfdce7c\.user_uploaded\media_1790073762258.png"
    texture_dir = r"c:\Users\ezgid\OneDrive\Masaüstü\PixelGame\Assets\Textures"

    out_path = r"c:\Users\ezgid\.gemini\antigravity-ide\brain\aa4c8cd2-dae3-4a8a-b626-4bc1fdfdce7c\final_rotation_scale_comparison.png"

    face_smile = Image.open(os.path.join(texture_dir, "KawaiiFace_Smile.png")).convert("RGBA")
    user_img = Image.open(user_screen_path)
    ref_img = Image.open(ref_screen_path)

    W, H = 1000, 520
    canvas = Image.new("RGBA", (W, H), (15, 21, 48))
    draw = ImageDraw.Draw(canvas)

    # Main Title
    draw.text((30, 18), "ROTATION, CAMERA ANGLE, SCALE & POSITION FIX", fill=(255, 255, 255))
    draw.text((30, 40), "Matching Image 2 Reference: 38° Isometric Tilt, Chunky Scale (0.64), Track Axis Centering (Offset 0)", fill=(130, 175, 230))

    # Panel 1: Current In-Game Screenshot (Image 1)
    draw.text((30, 75), "[1. CURRENT IN-GAME] Pushed to rail, flat 14° tilt, small", fill=(255, 105, 105))
    p1 = user_img.resize((260, 380), Image.Resampling.LANCZOS)
    canvas.paste(p1, (30, 100))

    # Panel 2: Reference Image (Image 2)
    draw.text((320, 75), "[2. USER REFERENCE] Target: Centered, 38° tilt, chunky", fill=(100, 225, 150))
    p2 = ref_img.resize((320, 380), Image.Resampling.LANCZOS)
    canvas.paste(p2, (320, 100))

    # Panel 3: New Fixed Render
    draw.text((670, 75), "[3. NEW ADJUSTED RESULT] Dead-center, 38°, scale 0.64", fill=(80, 210, 255))
    draw.rounded_rectangle([670, 100, 970, 480], radius=10, fill=(22, 28, 50), outline=(45, 65, 110), width=2)

    # Draw vertical track inside Panel 3
    t_cx = 820
    t_w = 170
    t_left = t_cx - t_w // 2
    t_right = t_cx + t_w // 2

    # Track channel
    draw.rectangle([t_left, 110, t_right, 470], fill=(28, 32, 45))
    # Outer rails
    draw.line([t_left, 110, t_left, 470], fill=(45, 82, 140), width=8)
    draw.line([t_right, 110, t_right, 470], fill=(45, 82, 140), width=8)

    # Chevron arrows pointing UP ^
    for ay in range(145, 460, 68):
        pts = [(t_cx, ay - 14), (t_cx - 12, ay + 6), (t_cx, ay - 2), (t_cx + 12, ay + 6)]
        draw.polygon(pts, fill=(80, 190, 235))

    # Draw the new chunky Kawaii Cube (Yellow) centered on the track:
    cube_size = 142 # 0.64 scale -> fills ~84% of track width
    cube_cy = 285

    # Shadow
    draw.ellipse([t_cx - cube_size//2 + 6, cube_cy + cube_size//2 - 12,
                  t_cx + cube_size//2 - 6, cube_cy + cube_size//2 + 10], fill=(10, 12, 18, 170))

    # Cube body (Puffy 38° squircle)
    body_box = [t_cx - cube_size//2, cube_cy - cube_size//2,
                t_cx + cube_size//2, cube_cy + cube_size//2]
    draw.rounded_rectangle(body_box, radius=32, fill=(250, 210, 45))

    # 38° Glossy Top Reflection (Pill highlight exactly as in Image 2!)
    shine_w = int(cube_size * 0.76)
    shine_h = int(cube_size * 0.28)
    draw.rounded_rectangle([t_cx - shine_w//2, cube_cy - cube_size//2 + 12,
                            t_cx + shine_w//2, cube_cy - cube_size//2 + 12 + shine_h],
                            radius=16, fill=(255, 255, 255, 145))
    # Inner bright shine
    draw.rounded_rectangle([t_cx - shine_w//4, cube_cy - cube_size//2 + 14,
                            t_cx + shine_w//4, cube_cy - cube_size//2 + 14 + shine_h//2],
                            radius=8, fill=(255, 255, 255, 220))

    # Subtle bottom bevel shading
    draw.arc([t_cx - cube_size//2 + 4, cube_cy - cube_size//2 + 4,
              t_cx + cube_size//2 - 4, cube_cy + cube_size//2 - 4], start=20, end=160, fill=(0, 0, 0, 45), width=4)

    # Face Overlay (Cute Smile, crisp and round)
    f_size = int(cube_size * 0.78)
    f_img = face_smile.resize((f_size, f_size), Image.Resampling.LANCZOS)
    fx = t_cx - f_size // 2
    fy = cube_cy - f_size // 2 + 14
    canvas.alpha_composite(f_img, (fx, fy))

    # Caption inside Panel 3
    draw.text((685, 452), "• Offset: 0.0  • Tilt: 38°  • Scale: 0.64", fill=(100, 220, 160))

    canvas.save(out_path, "PNG")
    print(f"Comparison saved to: {out_path}")

if __name__ == "__main__":
    render_comparison_final()
