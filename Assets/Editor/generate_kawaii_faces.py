import math
import os
from PIL import Image, ImageDraw, ImageFilter

def create_face_textures(output_dir):
    os.makedirs(output_dir, exist_ok=True)
    size = 512
    # Supersampling factor for smooth, anti-aliased edges
    scale = 4
    canvas_size = size * scale

    color_black = (17, 19, 26, 255) # Deep rich cartoon black
    color_blush = (255, 105, 140, 150) # Soft rosy cheeks
    color_white = (255, 255, 255, 220) # Catchlight shine

    faces = {}

    # Helper to create supersampled canvas
    def new_canvas():
        return Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))

    def downsample(img):
        return img.resize((size, size), Image.Resampling.LANCZOS)

    # -------------------------------------------------------------
    # 1. CLASSIC SMILE (• ‿ •) - Matches Reference Image 2
    # -------------------------------------------------------------
    img = new_canvas()
    draw = ImageDraw.Draw(img)
    # Eyes
    eye_r = 38 * scale
    eye_y = 235 * scale
    lx, rx = 165 * scale, 347 * scale
    draw.ellipse([lx - eye_r, eye_y - eye_r, lx + eye_r, eye_y + eye_r], fill=color_black)
    draw.ellipse([rx - eye_r, eye_y - eye_r, rx + eye_r, eye_y + eye_r], fill=color_black)
    # Catchlight dots (tiny cute white shine)
    shine_r = 9 * scale
    draw.ellipse([lx - eye_r*0.25 - shine_r, eye_y - eye_r*0.25 - shine_r, lx - eye_r*0.25 + shine_r, eye_y - eye_r*0.25 + shine_r], fill=color_white)
    draw.ellipse([rx - eye_r*0.25 - shine_r, eye_y - eye_r*0.25 - shine_r, rx - eye_r*0.25 + shine_r, eye_y - eye_r*0.25 + shine_r], fill=color_white)
    # Curved Smile
    box = [205 * scale, 240 * scale, 307 * scale, 315 * scale]
    draw.arc(box, start=25, end=155, fill=color_black, width=int(14 * scale))
    # Soft end dots for smile tips
    t1x, t1y = 212 * scale, 264 * scale
    t2x, t2y = 300 * scale, 264 * scale
    draw.ellipse([t1x - 7*scale, t1y - 7*scale, t1x + 7*scale, t1y + 7*scale], fill=color_black)
    draw.ellipse([t2x - 7*scale, t2y - 7*scale, t2x + 7*scale, t2y + 7*scale], fill=color_black)
    faces["KawaiiFace_Smile.png"] = downsample(img)

    # -------------------------------------------------------------
    # 2. WINKING SMILE (• ‿ <)
    # -------------------------------------------------------------
    img = new_canvas()
    draw = ImageDraw.Draw(img)
    # Left round eye
    draw.ellipse([lx - eye_r, eye_y - eye_r, lx + eye_r, eye_y + eye_r], fill=color_black)
    draw.ellipse([lx - eye_r*0.25 - shine_r, eye_y - eye_r*0.25 - shine_r, lx - eye_r*0.25 + shine_r, eye_y - eye_r*0.25 + shine_r], fill=color_white)
    # Right winking eye (cute closed arc or chevron)
    wink_box = [rx - eye_r, eye_y - eye_r*0.5, rx + eye_r, eye_y + eye_r*0.8]
    draw.arc(wink_box, start=195, end=345, fill=color_black, width=int(15 * scale))
    # Smile
    draw.arc(box, start=25, end=155, fill=color_black, width=int(14 * scale))
    draw.ellipse([t1x - 7*scale, t1y - 7*scale, t1x + 7*scale, t1y + 7*scale], fill=color_black)
    draw.ellipse([t2x - 7*scale, t2y - 7*scale, t2x + 7*scale, t2y + 7*scale], fill=color_black)
    faces["KawaiiFace_Wink.png"] = downsample(img)

    # -------------------------------------------------------------
    # 3. HAPPY / JOYFUL (^ ‿ ^)
    # -------------------------------------------------------------
    img = new_canvas()
    draw = ImageDraw.Draw(img)
    # Both happy arched eyes
    l_box = [lx - eye_r, eye_y - eye_r*0.6, lx + eye_r, eye_y + eye_r*0.7]
    r_box = [rx - eye_r, eye_y - eye_r*0.6, rx + eye_r, eye_y + eye_r*0.7]
    draw.arc(l_box, start=195, end=345, fill=color_black, width=int(15 * scale))
    draw.arc(r_box, start=195, end=345, fill=color_black, width=int(15 * scale))
    # Sweet Smile
    draw.arc(box, start=20, end=160, fill=color_black, width=int(14 * scale))
    draw.ellipse([t1x - 7*scale, t1y - 7*scale, t1x + 7*scale, t1y + 7*scale], fill=color_black)
    draw.ellipse([t2x - 7*scale, t2y - 7*scale, t2x + 7*scale, t2y + 7*scale], fill=color_black)
    # Rosy blush
    blush_r = 26 * scale
    draw.ellipse([lx - blush_r, eye_y + 35*scale - blush_r, lx + blush_r, eye_y + 35*scale + blush_r], fill=color_blush)
    draw.ellipse([rx - blush_r, eye_y + 35*scale - blush_r, rx + blush_r, eye_y + 35*scale + blush_r], fill=color_blush)
    faces["KawaiiFace_Happy.png"] = downsample(img)

    # -------------------------------------------------------------
    # 4. EXCITED / OPEN MOUTH (• ᗜ •)
    # -------------------------------------------------------------
    img = new_canvas()
    draw = ImageDraw.Draw(img)
    # Big sparkling round eyes
    draw.ellipse([lx - eye_r*1.05, eye_y - eye_r*1.05, lx + eye_r*1.05, eye_y + eye_r*1.05], fill=color_black)
    draw.ellipse([rx - eye_r*1.05, eye_y - eye_r*1.05, rx + eye_r*1.05, eye_y + eye_r*1.05], fill=color_black)
    draw.ellipse([lx - eye_r*0.3 - shine_r*1.2, eye_y - eye_r*0.3 - shine_r*1.2, lx - eye_r*0.3 + shine_r*1.2, eye_y - eye_r*0.3 + shine_r*1.2], fill=color_white)
    draw.ellipse([rx - eye_r*0.3 - shine_r*1.2, eye_y - eye_r*0.3 - shine_r*1.2, rx - eye_r*0.3 + shine_r*1.2, eye_y - eye_r*0.3 + shine_r*1.2], fill=color_white)
    # Open happy D-mouth
    open_box = [218 * scale, 252 * scale, 294 * scale, 320 * scale]
    draw.pieslice(open_box, start=0, end=180, fill=color_black)
    # Cute pink tongue inside mouth
    tongue_box = [234 * scale, 280 * scale, 278 * scale, 318 * scale]
    draw.pieslice(tongue_box, start=0, end=180, fill=(255, 120, 150, 255))
    faces["KawaiiFace_Excited.png"] = downsample(img)

    # -------------------------------------------------------------
    # 5. CUTE CAT / ANIME SMILE (• ω •)
    # -------------------------------------------------------------
    img = new_canvas()
    draw = ImageDraw.Draw(img)
    # Round eyes
    draw.ellipse([lx - eye_r, eye_y - eye_r, lx + eye_r, eye_y + eye_r], fill=color_black)
    draw.ellipse([rx - eye_r, eye_y - eye_r, rx + eye_r, eye_y + eye_r], fill=color_black)
    draw.ellipse([lx - eye_r*0.25 - shine_r, eye_y - eye_r*0.25 - shine_r, lx - eye_r*0.25 + shine_r, eye_y - eye_r*0.25 + shine_r], fill=color_white)
    draw.ellipse([rx - eye_r*0.25 - shine_r, eye_y - eye_r*0.25 - shine_r, rx - eye_r*0.25 + shine_r, eye_y - eye_r*0.25 + shine_r], fill=color_white)
    # Double curved cat mouth (w)
    m_w = 26 * scale
    m_y = 262 * scale
    mid_x = 256 * scale
    box_l = [mid_x - 2*m_w, m_y - 12*scale, mid_x, m_y + 30*scale]
    box_r = [mid_x, m_y - 12*scale, mid_x + 2*m_w, m_y + 30*scale]
    draw.arc(box_l, start=20, end=160, fill=color_black, width=int(13 * scale))
    draw.arc(box_r, start=20, end=160, fill=color_black, width=int(13 * scale))
    faces["KawaiiFace_Cat.png"] = downsample(img)

    # Save all files
    for filename, img_res in faces.items():
        p = os.path.join(output_dir, filename)
        img_res.save(p, "PNG")
        print(f"Generated face texture: {p}")

    # Also update KawaiiCube_Face.png to match KawaiiFace_Smile.png for default
    default_p = os.path.join(output_dir, "KawaiiCube_Face.png")
    faces["KawaiiFace_Smile.png"].save(default_p, "PNG")
    print(f"Updated default: {default_p}")

if __name__ == "__main__":
    out_dir = r"c:\Users\ezgid\OneDrive\Masaüstü\PixelGame\Assets\Textures"
    create_face_textures(out_dir)
