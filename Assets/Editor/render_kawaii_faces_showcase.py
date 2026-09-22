import math
import os
from PIL import Image, ImageDraw, ImageFont

def render_kawaii_showcase(output_path):
    # Load face textures
    texture_dir = r"c:\Users\ezgid\OneDrive\Masaüstü\PixelGame\Assets\Textures"
    face_names = [
        ("KawaiiFace_Smile.png", "Smile (• ‿ •)"),
        ("KawaiiFace_Wink.png", "Wink (• ‿ <)"),
        ("KawaiiFace_Happy.png", "Happy (^ ‿ ^)"),
        ("KawaiiFace_Excited.png", "Excited (• ᗜ •)"),
        ("KawaiiFace_Cat.png", "Cat (• ω •)")
    ]

    face_imgs = {}
    for fname, label in face_names:
        p = os.path.join(texture_dir, fname)
        if os.path.exists(p):
            face_imgs[fname] = Image.open(p).convert("RGBA")

    # Colors
    c_bg = (15, 21, 48)            # Deep navy game background
    c_belt_outer = (45, 82, 140)    # Blue rail borders
    c_belt_inner = (28, 32, 45)     # Dark charcoal conveyor belt
    c_arrow = (80, 190, 235)        # Cyan chevron arrows

    # Wagon colors (matching game palette)
    wagon_colors = [
        ((250, 210, 45), "Yellow"),
        ((40, 165, 255), "Blue"),
        ((245, 85, 120), "Pink"),
        ((50, 215, 150), "Mint"),
        ((175, 95, 245), "Purple")
    ]

    width = 1100
    height = 680
    img = Image.new("RGBA", (width, height), c_bg)
    draw = ImageDraw.Draw(img)

    # Title
    draw.text((30, 25), "KAWAII CUBE WAGONS - CONVEYOR BELT & FACES SHOWCASE", fill=(255, 255, 255))
    draw.text((30, 50), "Position: Dead-center over conveyor arrows | Multiple expressive faces (yüzler)", fill=(140, 175, 225))

    # -------------------------------------------------------------
    # SECTION 1: THE CONVEYOR BELT WITH CENTERED CUBES & FACES
    # -------------------------------------------------------------
    track_y = 120
    track_h = 150
    rail_thick = 10

    # Draw conveyor belt
    # Top rail
    draw.rounded_rectangle([30, track_y, width - 30, track_y + rail_thick], radius=4, fill=c_belt_outer)
    # Belt channel
    draw.rectangle([30, track_y + rail_thick, width - 30, track_y + track_h - rail_thick], fill=c_belt_inner)
    # Bottom rail
    draw.rounded_rectangle([30, track_y + track_h - rail_thick, width - 30, track_y + track_h], radius=4, fill=c_belt_outer)

    # Conveyor chevron arrows > > > (rightwards, centered)
    arrow_cy = track_y + track_h // 2
    for ax in range(70, width - 50, 85):
        # Draw chevron arrow >
        pts = [(ax - 10, arrow_cy - 14), (ax + 6, arrow_cy), (ax - 10, arrow_cy + 14),
               (ax - 4, arrow_cy + 14), (ax + 12, arrow_cy), (ax - 4, arrow_cy - 14)]
        draw.polygon(pts, fill=c_arrow)

    # Draw 5 centered Kawaii Cubes with different colors and faces
    cube_size = 105 # chunky candy cube size (~0.52 scale)
    cube_cy = arrow_cy # EXACTLY centered on arrows!

    step_x = (width - 160) // 5
    for i in range(5):
        cx = 95 + i * step_x
        color, color_name = wagon_colors[i]
        fname, face_label = face_names[i]

        # Draw soft shadow under cube
        shadow_rect = [cx - cube_size//2 + 4, cube_cy + cube_size//2 - 8,
                       cx + cube_size//2 - 4, cube_cy + cube_size//2 + 8]
        draw.ellipse(shadow_rect, fill=(10, 12, 18, 160))

        # Draw rounded candy cube body (Puffy Squircle)
        body_rect = [cx - cube_size//2, cube_cy - cube_size//2,
                     cx + cube_size//2, cube_cy + cube_size//2]
        draw.rounded_rectangle(body_rect, radius=24, fill=color)

        # Draw glossy 3D shine on top face (specular candy gleam as in reference)
        shine_rect = [cx - cube_size//2 + 10, cube_cy - cube_size//2 + 8,
                      cx + cube_size//2 - 10, cube_cy - cube_size//2 + 34]
        draw.rounded_rectangle(shine_rect, radius=14, fill=(255, 255, 255, 110))
        # Top inner bright core
        draw.rounded_rectangle([cx - cube_size//4, cube_cy - cube_size//2 + 10,
                                cx + cube_size//4, cube_cy - cube_size//2 + 22], radius=6, fill=(255, 255, 255, 180))

        # Bottom rim bevel
        draw.arc([cx - cube_size//2 + 3, cube_cy - cube_size//2 + 3,
                  cx + cube_size//2 - 3, cube_cy + cube_size//2 - 3], start=20, end=160, fill=(0, 0, 0, 45), width=3)

        # Composite face texture on the cube
        if fname in face_imgs:
            f_img = face_imgs[fname].resize((int(cube_size * 0.88), int(cube_size * 0.88)), Image.Resampling.LANCZOS)
            # Center face slightly lower (towards bottom-front, as in reference)
            fx = cx - f_img.width // 2
            fy = cube_cy - f_img.height // 2 + 10
            img.alpha_composite(f_img, (fx, fy))

        # Label below track
        draw.text((cx - 38, track_y + track_h + 12), face_label, fill=(230, 240, 255))
        draw.text((cx - 22, track_y + track_h + 30), color_name, fill=(160, 180, 210))

    # -------------------------------------------------------------
    # SECTION 2: CLOSE-UP FACE VARIETIES ("yüzler")
    # -------------------------------------------------------------
    box_top = 340
    draw.text((30, box_top), "5 EXPRESSIVE FACES (YÜZLER) - HIGH RESOLUTION CLOSE-UP", fill=(255, 255, 255))

    card_w = 190
    card_h = 240
    card_spacing = 22

    for i in range(5):
        card_x = 30 + i * (card_w + card_spacing)
        card_y = box_top + 30
        fname, face_label = face_names[i]
        color, _ = wagon_colors[i]

        # Card container
        draw.rounded_rectangle([card_x, card_y, card_x + card_w, card_y + card_h], radius=16, fill=(22, 28, 55))
        draw.rounded_rectangle([card_x, card_y, card_x + card_w, card_y + card_h], radius=16, outline=(45, 60, 105), width=2)

        # Mini jelly cube inside card
        c_size = 110
        c_x = card_x + card_w // 2
        c_y = card_y + 85

        draw.rounded_rectangle([c_x - c_size//2, c_y - c_size//2,
                                c_x + c_size//2, c_y + c_size//2], radius=26, fill=color)

        # Glossy highlight
        draw.rounded_rectangle([c_x - c_size//2 + 12, c_y - c_size//2 + 10,
                                c_x + c_size//2 - 12, c_y - c_size//2 + 36], radius=12, fill=(255, 255, 255, 110))

        # Face overlay
        if fname in face_imgs:
            f_img = face_imgs[fname].resize((int(c_size * 0.88), int(c_size * 0.88)), Image.Resampling.LANCZOS)
            fx = c_x - f_img.width // 2
            fy = c_y - f_img.height // 2 + 10
            img.alpha_composite(f_img, (fx, fy))

        # Card text
        draw.text((card_x + 18, card_y + 160), face_label, fill=(255, 255, 255))
        draw.text((card_x + 18, card_y + 185), fname, fill=(130, 150, 195))
        draw.text((card_x + 18, card_y + 208), "• SpriteRenderer 15", fill=(100, 220, 160))

    # Save showcase image
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    img.save(output_path, "PNG")
    print(f"Showcase image saved to: {output_path}")

if __name__ == "__main__":
    out_file = r"c:\Users\ezgid\OneDrive\Masaüstü\PixelGame\Assets\Editor\kawaii_faces_showcase.png"
    render_kawaii_showcase(out_file)
