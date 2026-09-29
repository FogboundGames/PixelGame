import os
from PIL import Image, ImageDraw, ImageFont

def render_showcase():
    out_dir = r"C:\Users\ezgid\.gemini\antigravity-ide\brain\6c76da88-bba0-4dd5-8054-826797c4d077"
    out_path = os.path.join(out_dir, "hud_and_cube_spacing_showcase.png")
    
    W, H = 1440, 980
    canvas = Image.new("RGBA", (W, H), (14, 20, 32, 255))
    draw = ImageDraw.Draw(canvas)
    
    try:
        font_title = ImageFont.truetype("Assets/Fonts/LilitaOne-Regular.ttf", 32)
        font_sub = ImageFont.truetype("Assets/Fonts/LilitaOne-Regular.ttf", 22)
        font_card_h = ImageFont.truetype("Assets/Fonts/LilitaOne-Regular.ttf", 18)
        font_body = ImageFont.truetype("arial.ttf", 15)
        font_body_bold = ImageFont.truetype("arialbd.ttf", 15)
    except:
        font_title = font_sub = font_card_h = font_body = font_body_bold = ImageFont.load_default()

    # 1. Header Banner
    draw.rectangle([0, 0, W, 76], fill=(22, 32, 50))
    draw.line([0, 76, W, 76], fill=(45, 68, 105), width=2)
    draw.text((40, 20), "📱 GEMİ SAHNESİ: CASUAL TOP HUD & KALİBRE EDİLMİŞ KÜP IZGARASI", fill=(255, 230, 110), font=font_title)

    # 2. TOP SECTION: HUD Comparison (Image 1 reference vs In-Game Built HUD)
    draw.rounded_rectangle([40, 96, W - 40, 370], radius=14, fill=(20, 28, 44), outline=(40, 60, 92), width=2)
    draw.text((60, 112), "1. BÖLÜM: CASUAL TOP HUD (GÖRSEL 1 REFERANSI İLE SAHNE KURULUMU)", fill=(100, 220, 255), font=font_sub)

    # Reference Image 1
    ref1_path = os.path.join(out_dir, ".user_uploaded", "media_1790717345843.png")
    if os.path.exists(ref1_path):
        ref1_img = Image.open(ref1_path).convert("RGBA")
        r_w, r_h = 580, int(580 * (ref1_img.height / ref1_img.width))
        ref1_scaled = ref1_img.resize((r_w, r_h), Image.Resampling.LANCZOS)
        canvas.paste(ref1_scaled, (70, 160))
        draw.rectangle([70, 160, 70 + r_w, 160 + r_h], outline=(235, 75, 75), width=2)
        draw.text((70, 160 + r_h + 8), "Görsel 1: Kullanıcı Referans Arayüzü", fill=(240, 100, 100), font=font_body_bold)

    # Built HUD Components Preview
    hx = 700
    draw.text((hx, 155), "Sahneye Eklenen Bileşenler (Gemi.unity -> HUD_Canvas):", fill=(255, 255, 255), font=font_card_h)
    
    hud_items = [
        ("🟣 SettingsButton (Mor Kare & Beyaz Dişli)", "btn_settings.png (104x104) + Button + CasualUIButtonJuice dokunsal tepkisi.", (180, 140, 255)),
        ("⚪ LEVEL 1 Başlığı", "LilitaOne fontu, 50pt kalın beyaz yazı, koyu kontur. LevelManager'a otomatik bağlı.", (255, 255, 255)),
        ("❤️ Can Rozeti (HeartPill)", "ui_pill tabanı + kırmızı kalp ikonu + '3' can sayısı + yeşil (+) düğmesi.", (255, 110, 130)),
        ("⭐ Altın Rozeti (CoinPill)", "ui_pill tabanı + sarı yıldızlı altın + '250' bakiye + yeşil (+) düğmesi.", (255, 215, 80)),
        ("🌙 Notch Çentik Bannerı (TopBanner)", "hud_top_banner.png (1080x180) koyu lacivert kavisli çentik başlığı.", (90, 180, 255))
    ]
    
    hy = 190
    for title, desc, col in hud_items:
        draw.ellipse([hx, hy + 4, hx + 10, hy + 14], fill=col)
        draw.text((hx + 18, hy), title, fill=col, font=font_body_bold)
        draw.text((hx + 18, hy + 20), desc, fill=(195, 210, 230), font=font_body)
        hy += 42

    # 3. BOTTOM SECTION: Cube Spacing Calibration (Image 2 issue vs Image 3 reference vs Fixed Grid)
    draw.rounded_rectangle([40, 390, W - 40, 940], radius=14, fill=(20, 28, 44), outline=(40, 60, 92), width=2)
    draw.text((60, 406), "2. BÖLÜM: KÜP IZGARA MESAFELERİNİN EŞİTLENMESİ VE ORANTILANMASI", fill=(255, 205, 90), font=font_sub)

    # Left: User marked issue (Image 2)
    ref2_path = os.path.join(out_dir, ".user_uploaded", "media_1790717372035.png")
    if os.path.exists(ref2_path):
        ref2_img = Image.open(ref2_path).convert("RGBA").resize((260, 244), Image.Resampling.LANCZOS)
        canvas.paste(ref2_img, (70, 450))
        draw.rectangle([70, 450, 330, 694], outline=(235, 75, 75), width=2)
        draw.text((70, 704), "Görsel 2: İşaretlenen Sağ Sütun Açıklığı", fill=(240, 90, 90), font=font_body_bold)
        draw.text((70, 724), "(Önceki durum: X ve Y adımları uyumsuz,\nsütunlar arası dikey yarık görünüyordu)", fill=(190, 200, 215), font=font_body)

    # Middle: Reference Game (Image 3)
    ref3_path = os.path.join(out_dir, ".user_uploaded", "media_1790717443381.png")
    if os.path.exists(ref3_path):
        ref3_img = Image.open(ref3_path).convert("RGBA").resize((260, 244), Image.Resampling.LANCZOS)
        canvas.paste(ref3_img, (360, 450))
        draw.rectangle([360, 450, 620, 694], outline=(100, 220, 150), width=2)
        draw.text((360, 704), "Görsel 3: İstenen Referans Izgara", fill=(100, 220, 150), font=font_body_bold)
        draw.text((360, 724), "(Kare, simetrik, eşit mesafeli,\nne dip dibe ne çok uzak kılcal oluk)", fill=(190, 200, 215), font=font_body)

    # Right: Calibrated Result Explanation & Diagram
    dx = 660
    draw.rounded_rectangle([dx, 450, W - 60, 700], radius=10, fill=(16, 22, 36), outline=(50, 75, 115))
    draw.text((dx + 20, 465), "Kalibre Edilen Yeni Matematiksel Izgara (Sahnede Uygulandı):", fill=(255, 230, 120), font=font_card_h)
    
    calib_lines = [
        "• stepX = stepY = 0.26456 br (Tam Kare & 100% Simetrik)",
        "• cellSize = 0.25988 br (Küp boyutuyla kusursuz orantı)",
        "• Küpler Arası Boşluk Payı: 0.00468 br (%1.8 kılcal oluk)",
        "• dx(20->21) = 0.26456 == dx(2->3) == dy(11->12)",
        "• Sağdaki 21. sütunun ayrık/açık durma problemi tamamen çözüldü.",
        "• Dikeyde küplerin üst üste binmesi/kaynaşması önlendi;",
        "  her küp 4 yönündeki komşusuyla TAM EŞİT mesafededir."
    ]
    
    ly = 500
    for l in calib_lines:
        draw.text((dx + 25, ly), l, fill=(210, 225, 245), font=font_body)
        ly += 26

    # Bottom summary box
    draw.rounded_rectangle([70, 770, W - 60, 920], radius=10, fill=(16, 22, 36), outline=(45, 65, 100))
    draw.text((90, 785), "✨ SONUÇ & SAHNE DURUMU", fill=(100, 240, 180), font=font_card_h)
    draw.text((90, 815), "1. Top UI: HUD_Canvas sahnede aktif, 1080x1920 ScreenSpaceOverlay ölçekli, LevelManager ile canlı senkronize.", fill=(220, 230, 245), font=font_body)
    draw.text((90, 845), "2. Küp Izgarası: Sahnede bulunan 258 adet PixelCube'ün tamamı 0.26456 br adımlı muntazam kare ızgaraya oturtuldu.", fill=(220, 230, 245), font=font_body)
    draw.text((90, 875), "3. Level_02_Heart.asset & PixelArtGenerator: CubeSpacing ve CubeSpacingX 0.018 olarak kalıcı kaydedildi.", fill=(220, 230, 245), font=font_body)

    canvas.save(out_path, "PNG")
    print(f"Showcase saved to {out_path}")

if __name__ == "__main__":
    render_showcase()
