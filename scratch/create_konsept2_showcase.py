import os
from PIL import Image, ImageDraw, ImageFont

def create_konsept2_showcase():
    out_dir = r"C:\Users\ezgid\.gemini\antigravity-ide\brain\6c76da88-bba0-4dd5-8054-826797c4d077"
    out_path = os.path.join(out_dir, "konsept2_marina_berths_showcase.png")
    
    W, H = 1440, 960
    canvas = Image.new("RGBA", (W, H), (14, 20, 32, 255))
    draw = ImageDraw.Draw(canvas)
    
    # Fonts
    try:
        font_title = ImageFont.truetype("Assets/Fonts/LilitaOne-Regular.ttf", 34)
        font_sub = ImageFont.truetype("Assets/Fonts/LilitaOne-Regular.ttf", 22)
        font_card_h = ImageFont.truetype("Assets/Fonts/LilitaOne-Regular.ttf", 18)
        font_body = ImageFont.truetype("arial.ttf", 15)
        font_body_bold = ImageFont.truetype("arialbd.ttf", 15)
    except:
        font_title = font_sub = font_card_h = font_body = font_body_bold = ImageFont.load_default()

    # 1. Header Banner
    draw.rectangle([0, 0, W, 80], fill=(22, 32, 50))
    draw.line([0, 80, W, 80], fill=(45, 68, 105), width=2)
    draw.text((40, 22), "⚓ KONSEPT 2: YÜZEN KIRMIZI ŞAMANDIRALAR & SU ÜSTÜ ZİNCİR IZGARASI", fill=(255, 230, 110), font=font_title)

    # 2. Left side: Concept Image & Live Asset Previews
    concept_img_path = os.path.join(out_dir, "floating_buoy_chains_concept_1790716510508.jpg")
    if os.path.exists(concept_img_path):
        c_img = Image.open(concept_img_path).convert("RGBA")
        c_img = c_img.resize((540, 540), Image.Resampling.LANCZOS)
        canvas.paste(c_img, (40, 110))
        draw.rectangle([40, 110, 580, 650], outline=(235, 75, 75), width=3)
        
        # Badge
        draw.rounded_rectangle([55, 125, 290, 160], radius=8, fill=(20, 28, 44, 220), outline=(235, 75, 75))
        draw.text((68, 133), "KULLANICI ONAYLI KONSEPT", fill=(255, 255, 255), font=font_card_h)

    # Asset Previews strip below concept (Signs 1-5 + Buoy Texture)
    signs_y = 675
    draw.rounded_rectangle([40, signs_y, 580, 915], radius=12, fill=(20, 28, 44), outline=(40, 58, 88), width=2)
    draw.text((60, signs_y + 15), "ÜRETİLEN ÖZEL 3D DOKULAR & NUMARA TABELALARI", fill=(100, 220, 255), font=font_card_h)

    # Paste sign previews
    sign_w, sign_h = 76, 76
    sign_gap = 24
    start_x = 65
    for i in range(1, 6):
        sign_path = f"Assets/Textures/Marina/Sign_Slot_{i}.png"
        if os.path.exists(sign_path):
            s_img = Image.open(sign_path).convert("RGBA").resize((sign_w, sign_h), Image.Resampling.LANCZOS)
            canvas.paste(s_img, (start_x + (i - 1) * (sign_w + sign_gap), signs_y + 55), s_img)
            # Label
            draw.text((start_x + (i - 1) * (sign_w + sign_gap) + 20, signs_y + 140), f"Slot {i}", fill=(200, 215, 235), font=font_body_bold)

    # Buoy striped texture swatch
    buoy_tex_path = "Assets/Textures/Marina/Buoy_Striped_Tex.png"
    if os.path.exists(buoy_tex_path):
        b_img = Image.open(buoy_tex_path).convert("RGBA").resize((110, 55), Image.Resampling.LANCZOS)
        canvas.paste(b_img, (440, signs_y + 160))
        draw.rectangle([440, signs_y + 160, 550, 215 + signs_y], outline=(235, 75, 75), width=1)
        draw.text((310, signs_y + 175), "Şamandıra Deseni:", fill=(200, 215, 235), font=font_body)

    # 3. Right side: Technical Architecture Cards
    rx = 620
    rw = W - rx - 40

    cards = [
        ("🔴 1. Low-Profile Deniz Şamandıraları (Floating Buoys)", 
         "• Her slotun (WaterSlot_1..5) 4 köşesine su seviyesinde (Y = 0.04) yerleşen kırmızı-beyaz çizgili deniz şamandıraları.\n"
         "• Alçak profil (Yükseklik = 0.18): Kameranın, gemi gövdesinin ve kargo küplerinin önünü ASLA kapatmaz.\n"
         "• Tepe kısmında gerçekçi metal bağlama halkası (eyelet ring) ve URP Lit parlak yüzey yansıması.\n"
         "• Toplam 20 adet şamandıra, her biri MarinaBuoyBobbing ile su üstünde faz ofsetli doğal dalga salınımı yapar.",
         (240, 80, 80)),
        
        ("⛓️ 2. Su Üstü Deniz Zincir Izgarası (Marine Chains Grid)",
         "• Şamandıraları birbirine bağlayan interlocking 3D oval halkalı dökme demir zincirler.\n"
         "• Sol, sağ ve arka sahil hattını çevreler; slot girişi (ön taraf) gemilerin yanaşması için tamamen açık ve ferahtır.\n"
         "• Hafif sarkma eğrisi (-0.015) ve metalik döküm malzeme (%85 Metallic, %40 Smoothness) ile gerçekçi su temas hissi.",
         (120, 190, 255)),
        
        ("🪵 3. Ahşap Slot Numara Tabelaları (1-2-3-4-5)",
         "• Slot girişlerinde suyun hemen üzerinde duran rustik ahşap tabela ve direk.\n"
         "• LilitaOne fontu ile hazırlanmış canlı, okunabilir slot numaraları (1, 2, 3, 4, 5).\n"
         "• İzometrik / -68° kamera açısına doğrudan dik bakacak şekilde -35° eğim verilerek maksimum netlik sağlandı.",
         (255, 200, 90)),

        ("✨ 4. Temiz Görsel ve Fiziksel Bütünlük",
         "• Eski beyaz kesikli kutular (IndicatorMesh) sahnede deaktive edildi; artık gözü yoran yapay çizgiler yok!\n"
         "• Gemi yanaşma boyutu: 1.04 birim genişlik (1.20 world), 0.94'lük gemilerle tam 0.13 ideal yanaşma payı.\n"
         "• Önceki Stage 2-5 SmoothDamp drag, pickup lift, dynamic banking, su bobbing ve manyetik snap sistemleri %100 korundu.",
         (100, 230, 160))
    ]

    cy = 110
    card_h = 188
    for title, text, col in cards:
        draw.rounded_rectangle([rx, cy, rx + rw, cy + card_h], radius=10, fill=(20, 28, 44), outline=(40, 58, 88), width=2)
        # Header accent bar
        draw.line([rx + 15, cy + 34, rx + rw - 15, cy + 34], fill=(35, 50, 75), width=1)
        draw.text((rx + 20, cy + 12), title, fill=col, font=font_sub)
        
        ty = cy + 44
        for line in text.split("\n"):
            draw.text((rx + 24, ty), line, fill=(205, 218, 235), font=font_body)
            ty += 28
        
        cy += card_h + 16

    canvas.save(out_path, "PNG")
    print(f"Showcase successfully saved to {out_path}")

if __name__ == "__main__":
    create_konsept2_showcase()
