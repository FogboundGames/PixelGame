import os
from PIL import Image, ImageDraw, ImageFont
import numpy as np

# Load original BoardPodium_Reference
base_im = Image.open('Assets/Textures/BoardPodium_Reference.png')
base_arr = np.array(base_im)

# Masks
canvas_mask = (base_arr[:, :, 0] == 255) & (base_arr[:, :, 1] == 255) & (base_arr[:, :, 2] == 255) & (base_arr[:, :, 3] == 255)
rim_mask = (base_arr[:, :, 0] == 168) & (base_arr[:, :, 1] == 184) & (base_arr[:, :, 2] == 198) & (base_arr[:, :, 3] == 255)
podium_mask = (base_arr[:, :, 0] == 211) & (base_arr[:, :, 1] == 223) & (base_arr[:, :, 2] == 234) & (base_arr[:, :, 3] == 255)
groove_mask = (base_arr[:, :, 0] < 140) & (base_arr[:, :, 0] > 70) & (base_arr[:, :, 3] == 255)

options = [
    {
        'id': 'opt1_pure2tone',
        'title': 'SEÇENEK 1 (ÖNERİLEN)',
        'badge': 'TAM 2 TON',
        'badge_col': (36, 150, 60),
        'sub': 'Dış Gri & İç Açık Gri (Sıfır 3. Renk)',
        'desc': '• Dış Çerçeve: #B4C2CD (Dengeli Nötr Gri)\n• İç Tuval: #E5ECF2 (Daha Açık Zarif Gri)\n• Çerçeve olukla belirginleşir; 3. renk yok, tam 2 ton!',
        'podium': (180, 194, 205),
        'rim': (175, 190, 202),
        'canvas': (229, 236, 242),
        'groove': (105, 118, 130),
    },
    {
        'id': 'opt2_subtle_frame_depth',
        'title': 'SEÇENEK 2',
        'badge': 'ZARİF DERİNLİK',
        'badge_col': (40, 110, 210),
        'sub': 'Belirgin Dış Çerçeve Bordürü & Açık İç',
        'desc': '• Dış Çerçeve Bordürü: #9CB0C0 (Karakterli Gri)\n• Dış Zemin: #CBD6E0 (Doğal Zemin Grisi)\n• İç Tuval: #EAF0F5 (Açık Gri İç Alan)',
        'podium': (203, 214, 224),
        'rim': (156, 176, 192),
        'canvas': (234, 240, 245),
        'groove': (95, 110, 122),
    },
    {
        'id': 'opt3_soft_monochrome',
        'title': 'SEÇENEK 3',
        'badge': 'MİNİMAL',
        'badge_col': (180, 105, 25),
        'sub': 'Yumuşak Geçişli İki Ton Gri',
        'desc': '• Dış Çerçeve: #C4D0DB (Açık Orta Gri)\n• Çerçeve Bordürü: #B0BFCB (Hafifçe Vurgulu)\n• İç Tuval: #EEF3F7 (Çok Açık Pastel Gri)',
        'podium': (196, 208, 219),
        'rim': (176, 191, 203),
        'canvas': (238, 243, 247),
        'groove': (115, 128, 140),
    },
    {
        'id': 'opt4_deep_contrast',
        'title': 'SEÇENEK 4',
        'badge': 'KONTRAST',
        'badge_col': (140, 70, 180),
        'sub': 'Tok Gri Çerçeve & Açık Gri Tuval',
        'desc': '• Dış Çerçeve Bordürü: #8FA2B2 (Tok Kurşuni Gri)\n• Dış Zemin: #B8C6D2 (Orta Gri)\n• İç Tuval: #DFE6EC (Belirgin Açık Gri)',
        'podium': (184, 198, 210),
        'rim': (143, 162, 178),
        'canvas': (223, 230, 236),
        'groove': (85, 100, 112),
    }
]

# Generate each texture
tex_dict = {}
for opt in options:
    arr = base_arr.copy()
    arr[podium_mask, :3] = opt['podium']
    arr[rim_mask, :3] = opt['rim']
    arr[canvas_mask, :3] = opt['canvas']
    arr[groove_mask, :3] = opt['groove']
    out_im = Image.fromarray(arr)
    fname = 'scratch/tex_' + opt['id'] + '.png'
    out_im.save(fname)
    tex_dict[opt['id']] = out_im
    print('Generated texture:', fname)

# Load boat sprite
boat_im = Image.open('scratch/boat_transparent.png')

# Create comparison image
W, H = 1920, 800
sheet = Image.new('RGB', (W, H), (16, 20, 26))
draw = ImageDraw.Draw(sheet)

font_title = ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf', 26)
font_subtitle = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 16)
font_panel_h = ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf', 18)
font_panel_badge = ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf', 13)
font_desc = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 13)
font_small = ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf', 11)

# Header
draw.text((40, 24), "OYUN ALANI (BOARD PODIUM) İKİ TON GRİ DÜZENLEMESİ", fill=(255, 255, 255), font=font_title)
draw.text((40, 62), 'Kullanıcı Talebi: "çerçevenin dış kısmını gri iç kısmını da ona kıyasen daha açık gri yapalım 3 renk olmasın"', fill=(160, 185, 215), font=font_subtitle)

panel_w = 440
panel_h = 650
start_y = 105
gap = 24
margin_x = 40

for i, opt in enumerate(options):
    px = margin_x + i * (panel_w + gap)
    py = start_y
    
    border_col = (46, 170, 75) if i == 0 else (50, 60, 75)
    border_w = 3 if i == 0 else 1
    
    # Card bg
    draw.rounded_rectangle([px, py, px + panel_w, py + panel_h], radius=16, fill=(24, 29, 37), outline=border_col, width=border_w)
    
    # Title
    draw.text((px + 20, py + 18), opt['title'], fill=(255, 255, 255), font=font_panel_h)
    
    # Badge
    badge_w = 120
    draw.rounded_rectangle([px + panel_w - badge_w - 20, py + 16, px + panel_w - 20, py + 40], radius=8, fill=opt['badge_col'])
    draw.text((px + panel_w - badge_w - 12, py + 20), opt['badge'], fill=(255, 255, 255), font=font_panel_badge)
    
    # Subtitle
    draw.text((px + 20, py + 46), opt['sub'], fill=(180, 195, 215), font=font_desc)
    
    # Board preview
    tex = tex_dict[opt['id']]
    # Crop to square area and resize to 360x360
    tex_crop = tex.crop((35, 35, 989, 989)).resize((360, 360), Image.Resampling.LANCZOS)
    
    # Paste boat in center
    bw, bh = boat_im.size
    b_scaled = boat_im.resize((int(bw * 1.35), int(bh * 1.35)), Image.Resampling.LANCZOS)
    bsw, bsh = b_scaled.size
    tex_crop.paste(b_scaled, (180 - bsw // 2, 180 - bsh // 2), b_scaled)
    
    # Paste preview on card
    sheet.paste(tex_crop, (px + 40, py + 80))
    draw.rounded_rectangle([px + 38, py + 78, px + 40 + 362, py + 80 + 362], radius=12, outline=(65, 78, 98), width=2)
    
    # Description lines
    lines = opt['desc'].split('\n')
    ly = py + 465
    for line in lines:
        draw.text((px + 20, ly), line, fill=(225, 235, 245), font=font_desc)
        ly += 22
        
    # Color swatches at bottom
    c_rim = opt['rim']
    c_canvas = opt['canvas']
    hex_rim = f"#{c_rim[0]:02X}{c_rim[1]:02X}{c_rim[2]:02X}"
    hex_canvas = f"#{c_canvas[0]:02X}{c_canvas[1]:02X}{c_canvas[2]:02X}"
    
    draw.rectangle([px + 20, py + 575, px + panel_w - 20, py + 615], fill=(35, 42, 54), outline=(50, 60, 75))
    # Swatch 1 (Outer Gray)
    draw.rectangle([px + 28, py + 583, px + 58, py + 607], fill=c_rim, outline=(255, 255, 255))
    draw.text((px + 66, py + 587), f"Dış: {hex_rim}", fill=(200, 220, 240), font=font_small)
    # Swatch 2 (Inner Light Gray)
    draw.rectangle([px + 200, py + 583, px + 230, py + 607], fill=c_canvas, outline=(255, 255, 255))
    draw.text((px + 238, py + 587), f"İç: {hex_canvas}", fill=(200, 220, 240), font=font_small)

out_preview = r'C:\Users\ezgid\.gemini\antigravity-ide\brain\a9d16b58-e6b6-4029-9285-eb2e36a68900\board_2tone_gray_comparison.png'
sheet.save(out_preview)
print('Comparison sheet saved to:', out_preview)
