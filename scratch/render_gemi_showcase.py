import os
from PIL import Image, ImageDraw, ImageFont

def render_gemi_showcase():
    bg_path = "Assets/Kenney/ChatGPT Image 22 Eyl 2026 19_23_43.png"
    out_path = r"C:\Users\ezgid\.gemini\antigravity-ide\brain\01f162d7-efe4-4233-aea8-513a2538bb80\gemi_scene_architecture.png"

    bg_img = Image.open(bg_path).convert("RGBA")
    
    # Create high-res infographic canvas
    W, H = 1400, 900
    canvas = Image.new("RGBA", (W, H), (18, 24, 38, 255))
    draw = ImageDraw.Draw(canvas)

    # 1. Header
    draw.rectangle([0, 0, W, 70], fill=(26, 36, 56))
    draw.text((40, 22), "GEMI SCENE - 2D FIXED BACKGROUND & 3D GAMEPLAY LAYER SETUP", fill=(255, 255, 255))

    # 2. Left panel: Preview of the game background with zone overlays
    # Target height 760, aspect ratio of bg_img is 905/1738 ~ 0.5207
    p_h = 760
    p_w = int(p_h * (bg_img.width / bg_img.height))
    p_img = bg_img.resize((p_w, p_h), Image.Resampling.LANCZOS)
    
    px = 60
    py = 100
    canvas.paste(p_img, (px, py))

    # Draw Zone annotations over the image
    overlay = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ov_draw = ImageDraw.Draw(overlay)

    # Zone 1: Sand Play Area (unity_y = 0.60 to 0.91 -> from top: 0.09 to 0.40)
    z1_top = py + int(p_h * 0.09)
    z1_bottom = py + int(p_h * 0.40)
    z1_left = px + int(p_w * 0.22)
    z1_right = px + int(p_w * 0.77)
    ov_draw.rectangle([z1_left, z1_top, z1_right, z1_bottom], outline=(255, 215, 0, 230), width=3)
    ov_draw.rectangle([z1_left, z1_top, z1_right, z1_bottom], fill=(255, 215, 0, 35))

    # Zone 2: Wooden Bridge (unity_y = 0.42 to 0.54 -> from top: 0.46 to 0.58)
    z2_top = py + int(p_h * 0.46)
    z2_bottom = py + int(p_h * 0.58)
    z2_left = px + int(p_w * 0.16)
    z2_right = px + int(p_w * 0.84)
    ov_draw.rectangle([z2_left, z2_top, z2_right, z2_bottom], outline=(245, 130, 40, 230), width=3)
    ov_draw.rectangle([z2_left, z2_top, z2_right, z2_bottom], fill=(245, 130, 40, 35))

    # Zone 3: Water Area (unity_y = 0.00 to 0.40 -> from top: 0.60 to 1.00)
    z3_top = py + int(p_h * 0.60)
    z3_bottom = py + p_h - 10
    z3_left = px + 10
    z3_right = px + p_w - 10
    ov_draw.rectangle([z3_left, z3_top, z3_right, z3_bottom], outline=(0, 210, 255, 230), width=3)
    ov_draw.rectangle([z3_left, z3_top, z3_right, z3_bottom], fill=(0, 210, 255, 30))

    canvas.alpha_composite(overlay)

    # 3. Right Panel: Architecture & Instructions
    rx = px + p_w + 50
    rw = W - rx - 50

    # Section 1: Layer Hierarchy
    draw.rounded_rectangle([rx, 100, rx + rw, 340], radius=12, fill=(28, 38, 60), outline=(50, 70, 110), width=2)
    draw.text((rx + 25, 118), "LAYER SIRALAMASI & MIMARI (ARCHITECTURE)", fill=(255, 220, 100))
    
    layers = [
        ("Layer 1 & 2: Sabit 2D Arka Plan & Çevre", "Background_Canvas (Screen Space - Camera, planeDistance: 35, sortingOrder: -100). Asla bozulmaz, tüm ekranlarda tam oturur.", (100, 220, 150)),
        ("Layer 3: 3D Gameplay Modelleri (Ön Plan)", "[GAMEPLAY_MODELS] root objesi altinda 3D modeller serbestce yerlestirilir. Z=0 düzleminde arka planin önünde görünür.", (80, 200, 255)),
        ("Layer 4: URP Şeffaf Gölge Yakalayıcı Zemin", "Ground_ShadowCatcher (URP Shadow Catcher Shader). Yalnizca Directional Light golgelerini yakalar, arkaplani karartarak fiziksel temas hissi verir.", (255, 160, 80)),
        ("Aydınlatma (Directional Light)", "Görseldeki sol-üst günes açısıyla kalibre edildi: Euler (48, -32, 0), yumusak soft shadows.", (255, 230, 140))
    ]

    ly = 150
    for title, desc, col in layers:
        draw.ellipse([rx + 25, ly + 4, rx + 37, ly + 16], fill=col)
        draw.text((rx + 45, ly + 2), title, fill=col)
        draw.text((rx + 45, ly + 20), desc, fill=(190, 205, 225))
        ly += 44

    # Section 2: Zone Coordinates
    draw.rounded_rectangle([rx, 360, rx + rw, 590], radius=12, fill=(28, 38, 60), outline=(50, 70, 110), width=2)
    draw.text((rx + 25, 378), "3D MODEL YERLEŞİM BÖLGELERİ (GAMEPLAY ZONES)", fill=(255, 220, 100))

    zones = [
        ("1. [Zone_Sand_PlayArea] -> Vector3(0, 3.85, 0)", "Dikdörtgen çimen/taş sınırının tam merkezine odaklanmıştır. Puzzle/küp modelleri buraya çocuk olarak eklenir.", (255, 215, 0)),
        ("2. [Zone_Wooden_Bridge] -> Vector3(0, -0.32, 0)", "Ahşap köprü / iskelenin yüzeyine hizalanmıştır. Geçiş yapan arabalar, karakterler veya vagonlar için uygundur.", (245, 130, 40)),
        ("3. [Zone_Water_LowerArea] -> Vector3(0, -4.80, 0)", "Alt kısımdaki turkuaz su alanına hizalanmıştır. Kenney tekneleri, gemiler veya su efektleri için idealdir.", (0, 210, 255)),
        ("Önemli Not", "Modellerin Y veya Z rotasyonunu dilediğiniz gibi çevirebilirsiniz; gölgeleri otomatik olarak kuma/suya düşer.", (200, 215, 235))
    ]

    zy = 410
    for ztitle, zdesc, zcol in zones:
        draw.text((rx + 25, zy), ztitle, fill=zcol)
        draw.text((rx + 25, zy + 18), zdesc, fill=(185, 200, 220))
        zy += 42

    # Section 3: Hierarchy Structure
    draw.rounded_rectangle([rx, 610, rx + rw, 860], radius=12, fill=(24, 32, 50), outline=(45, 60, 95), width=2)
    draw.text((rx + 25, 628), "GEMİ SAHNESİ HİYERARŞİ DÜZENİ (SCENE HIERARCHY)", fill=(100, 220, 255))

    hierarchy_lines = [
        "Main Camera                    -> Ortografik (Size 8), SolidColor, URP Camera Data",
        "Directional Light              -> Açılı sıcak gün ışığı (48, -32, 0), Soft Shadows",
        "Ground_ShadowCatcher           -> Şeffaf URP Gölge Düzlemi (modellerin altına gölge düşürür)",
        "[GAMEPLAY_MODELS]              -> 3D MODELLERİNİZ BURAYA EKLENECEK",
        "   ├── [Zone_Sand_PlayArea]    -> Kum alanına yerleştirilecek objeler",
        "   ├── [Zone_Wooden_Bridge]    -> Köprüye yerleştirilecek objeler",
        "   └── [Zone_Water_LowerArea]  -> Suya yerleştirilecek tekneler/gemiler",
        "Background_Canvas              -> Screen Space - Camera (planeDistance: 35, sorting: -100)",
        "   └── BackgroundImage         -> ChatGPT Image 22 Eyl 2026 19_23_43.png (Tam Ekran)",
        "[Legacy_Prototype_Backup]      -> Eski geçici prototip objeleri (Pasif/Yedek)"
    ]

    hy = 658
    for line in hierarchy_lines:
        col = (255, 210, 100) if "[GAMEPLAY_MODELS]" in line else ((100, 225, 160) if "Zone" in line else (200, 215, 235))
        draw.text((rx + 25, hy), line, fill=col)
        hy += 19

    canvas.save(out_path, "PNG")
    print(f"Showcase image saved to: {out_path}")

if __name__ == "__main__":
    render_gemi_showcase()
