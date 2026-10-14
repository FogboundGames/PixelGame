import os
from PIL import Image, ImageDraw, ImageFont
import numpy as np

# Prepare canvas
W, H = 1920, 780
bg = Image.new('RGB', (W, H), (16, 20, 26))
draw = ImageDraw.Draw(bg)

# Fonts
font_title = ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf', 26)
font_subtitle = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 16)
font_panel_h = ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf', 18)
font_panel_badge = ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf', 13)
font_desc = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 13)
font_small = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 11)

# Header
draw.text((40, 24), "OYUN ALANI (BOARD PODIUM) İÇ ÇERÇEVE RENK DÜZENLEMESİ", fill=(255, 255, 255), font=font_title)
draw.text((40, 62), 'Kullanıcı Talebi: "içini beyaz yapmayalım yine grimsi olsun ama zeminden biraz farklı tonda koyu olsun"', fill=(160, 185, 215), font=font_subtitle)

# Crop authentic boat with sails from user screenshot
im_user = Image.open('scratch/user_last_upload.png').convert('RGB')
arr_u = np.array(im_user)
# Authentic boat area from screenshot
crop_boat_arr = arr_u[138:245, 130:270] # (107, 140, 3)
im_boat = Image.fromarray(crop_boat_arr)

# Create white texture for panel 0
arr_w = np.array(Image.open('scratch/tex_board_v1_cool_gray.png'))
rim_mask = (arr_w[:, :, 0] == 168) & (arr_w[:, :, 1] == 184) & (arr_w[:, :, 2] == 198)
arr_w[rim_mask, :3] = 255
sh_mask = (arr_w[:, :, 0] == 128) & (arr_w[:, :, 1] == 145) & (arr_w[:, :, 2] == 160)
arr_w[sh_mask, :3] = 255
tex_white_obj = Image.fromarray(arr_w)

tex_map = {
    'white': tex_white_obj,
    'v1': Image.open('scratch/tex_board_v1_cool_gray.png'),
    'v2': Image.open('scratch/tex_board_v2_darker_slate.png'),
    'v3': Image.open('scratch/tex_board_v3_full_interior_gray.png')
}

panels = [
    {
        'title': 'MEVCUT DURUM',
        'badge': 'BEYAZ İÇ',
        'badge_col': (110, 120, 135),
        'sub': 'Tamamen Beyaz İç Zemin',
        'desc': '• Çerçeve & Tuval: Beyaz (#FFFFFF)\n• Dış Zemin: #D3DFEA | Oluk: #555F6C\n• Kullanıcının "beyaz yapmayalım" dediği durum.',
        'tex_type': 'white',
        'rim_hex': '#FFFFFF',
        'diff': 'Zeminle tezat yok'
    },
    {
        'title': 'SEÇENEK 1 (AKTİF EDİLDİ)',
        'badge': 'ÖNERİLEN',
        'badge_col': (36, 150, 60),
        'sub': 'Doğal & Şık Koyu Gri Çerçeve',
        'desc': '• İç Çerçeve: #A8B8C6 (Zeminden ~40 ton koyu)\n• İç Tuval (Oyun Alanı): Temiz Beyaz (#FFFFFF)\n• Gemi küpleri pırıl pırıl, çerçeve zarif ve belirgin.',
        'tex_type': 'v1',
        'rim_hex': '#A8B8C6',
        'diff': 'Zeminden ~40 ton koyu'
    },
    {
        'title': 'SEÇENEK 2',
        'badge': 'DERİN ARDUVAZ',
        'badge_col': (40, 110, 210),
        'sub': 'Daha Belirgin Koyu Gri Çerçeve',
        'desc': '• İç Çerçeve: #94A5B4 (Zeminden ~65 ton koyu)\n• İç Tuval (Oyun Alanı): Temiz Beyaz (#FFFFFF)\n• Güçlü kontrast ve derinlikli bordür etkisi.',
        'tex_type': 'v2',
        'rim_hex': '#94A5B4',
        'diff': 'Zeminden ~65 ton koyu'
    },
    {
        'title': 'SEÇENEK 3',
        'badge': 'TAM GRİMSİ',
        'badge_col': (180, 105, 25),
        'sub': 'Tüm İç Alan Koyu Grimsi',
        'desc': '• İç Çerçeve: #94A5B4 | Tuval: #B9C6D2\n• Tuval dahil tüm iç alan zeminden koyu gri.\n• Tüm oyun alanı podiumdan içeri oyulmuş hissettirir.',
        'tex_type': 'v3',
        'rim_hex': '#94A5B4 / #B9C6D2',
        'diff': 'Tüm iç zemin koyu'
    }
]

panel_w = 440
panel_h = 635
start_y = 110
gap = 24
margin_x = 40

for i, p in enumerate(panels):
    px = margin_x + i * (panel_w + gap)
    py = start_y
    
    # Highlight border for Option 1
    border_col = (46, 170, 75) if i == 1 else (50, 60, 75)
    border_w = 3 if i == 1 else 1
    
    # Card bg
    draw.rounded_rectangle([px, py, px + panel_w, py + panel_h], radius=16, fill=(24, 29, 37), outline=border_col, width=border_w)
    
    # Header
    draw.text((px + 20, py + 18), p['title'], fill=(255, 255, 255), font=font_panel_h)
    
    # Badge
    badge_w = 120
    draw.rounded_rectangle([px + panel_w - badge_w - 20, py + 16, px + panel_w - 20, py + 40], radius=8, fill=p['badge_col'])
    draw.text((px + panel_w - badge_w - 12, py + 20), p['badge'], fill=(255, 255, 255), font=font_panel_badge)
    
    # Subtitle
    draw.text((px + 20, py + 46), p['sub'], fill=(180, 195, 215), font=font_desc)
    
    # Texture crop
    tex = tex_map[p['tex_type']]
    tex_crop = tex.crop((40, 40, 984, 984)).resize((360, 360), Image.Resampling.LANCZOS)
    
    # Paste authentic boat in the center
    # On white canvas (Option 0, 1, 2)
    if p['tex_type'] != 'v3':
        bw, bh = im_boat.size
        # scale boat slightly to look great on 360x360 board
        b_scaled = im_boat.resize((int(bw * 1.25), int(bh * 1.25)), Image.Resampling.LANCZOS)
        bsw, bsh = b_scaled.size
        tex_crop.paste(b_scaled, (180 - bsw // 2, 180 - bsh // 2))
    else:
        # For v3 where canvas is #B9C6D2, blend boat
        bw, bh = im_boat.size
        b_scaled = im_boat.resize((int(bw * 1.25), int(bh * 1.25)), Image.Resampling.LANCZOS)
        arr_bs = np.array(b_scaled).astype(float)
        # multiply blend with grayish background (185, 198, 210)
        canvas_bg = np.array([185, 198, 210], dtype=float)
        blended = (arr_bs / 255.0) * canvas_bg
        blended = np.clip(blended, 0, 255).astype(np.uint8)
        bsw, bsh = b_scaled.size
        tex_crop.paste(Image.fromarray(blended), (180 - bsw // 2, 180 - bsh // 2))
    
    # Paste board preview onto card
    bg.paste(tex_crop, (px + 40, py + 80))
    draw.rounded_rectangle([px + 38, py + 78, px + 40 + 362, py + 80 + 362], radius=12, outline=(65, 78, 98), width=2)
    
    # Clean board view without obstruction
    
    # Description lines
    lines = p['desc'].split('\n')
    ly = py + 465
    for line in lines:
        draw.text((px + 20, ly), line, fill=(225, 235, 245), font=font_desc)
        ly += 22
        
    # Color bar preview at bottom of card
    draw.rectangle([px + 20, py + 575, px + panel_w - 20, py + 605], fill=(35, 42, 54), outline=(50, 60, 75))
    draw.text((px + 28, py + 583), f"Renk: {p['rim_hex']}  |  {p['diff']}", fill=(180, 205, 230), font=font_small)

out_path = r'C:\Users\ezgid\.gemini\antigravity-ide\brain\a9d16b58-e6b6-4029-9285-eb2e36a68900\board_inner_gray_options.png'
bg.save(out_path)
print('Updated comparison sheet saved to:', out_path)
