import os
from PIL import Image, ImageDraw, ImageFont
import numpy as np

# Load assets
splash = Image.open('Assets/Resources/LandFlow_Splash_Clean.png').convert('RGBA')
logo = Image.open('Assets/Resources/LandFlow_Logo.png').convert('RGBA')
bar_bg = Image.open('Assets/Resources/ProgressBar_Capsule_BG.png').convert('RGBA')
bar_fill = Image.open('Assets/Resources/ProgressBar_Capsule_Fill.png').convert('RGBA')
banner_loading = Image.open('Assets/Resources/Text_Banner_Loading.png').convert('RGBA')
banner_completed = Image.open('Assets/Resources/Text_Banner_LevelCompleted.png').convert('RGBA')
banner_restarting = Image.open('Assets/Resources/Text_Banner_Restarting.png').convert('RGBA')
banner_failed = Image.open('Assets/Resources/Text_Banner_LevelFailed.png').convert('RGBA')

# Function to build a progress bar at fraction
def build_capsule_bar(fraction, w=580, h=86):
    bg_img = bar_bg.resize((w, h), Image.Resampling.LANCZOS)
    fill_img = bar_fill.resize((w, h), Image.Resampling.LANCZOS)
    
    # Clip fill to fraction
    arr_fill = np.array(fill_img)
    clip_x = int(w * np.clip(fraction, 0.0, 1.0))
    arr_fill[:, clip_x:, 3] = 0
    clipped = Image.fromarray(arr_fill)
    
    res = bg_img.copy()
    res.paste(clipped, (0, 0), clipped)
    return res

# 1. Loading Screen (Boot)
screen1 = splash.copy()
bw = 420
bh = int(bw * (banner_loading.height / banner_loading.width))
b1 = banner_loading.resize((bw, bh), Image.Resampling.LANCZOS)
screen1.paste(b1, (540 - bw // 2, 1610 - bh), b1)
bar1 = build_capsule_bar(0.70, 600, 90)
screen1.paste(bar1, (540 - 300, 1640), bar1)

# Helper for classic transition screen
def make_transition_screen(banner_img, progress, banner_w=520, bar_w=560, bar_h=84):
    screen = Image.new('RGBA', (1080, 1920), (23, 115, 209, 255))
    lw = 580
    lh = int(lw * (logo.height / logo.width))
    l_img = logo.resize((lw, lh), Image.Resampling.LANCZOS)
    screen.paste(l_img, (540 - lw // 2, 620), l_img)
    
    bh = int(banner_w * (banner_img.height / banner_img.width))
    b_img = banner_img.resize((banner_w, bh), Image.Resampling.LANCZOS)
    screen.paste(b_img, (540 - banner_w // 2, 620 + lh + 70), b_img)
    
    bar = build_capsule_bar(progress, bar_w, bar_h)
    screen.paste(bar, (540 - bar_w // 2, 620 + lh + 70 + bh + 50), bar)
    return screen

screen2 = make_transition_screen(banner_completed, 0.50, banner_w=580)
screen3 = make_transition_screen(banner_restarting, 0.35, banner_w=500)
screen4 = make_transition_screen(banner_failed, 0.20, banner_w=480)

# Build Master Comparison Canvas
W, H = 1920, 960
bg = Image.new('RGB', (W, H), (16, 20, 26))
draw = ImageDraw.Draw(bg)

# Fonts
font_title = ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf', 26)
font_subtitle = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 16)
font_card_h = ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf', 17)
font_badge = ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf', 12)
font_desc = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 13)

# Header
draw.text((40, 22), "YENİ ENTEGRE EDİLEN YÜKLEME VE GEÇİŞ EKRANLARI", fill=(255, 255, 255), font=font_title)
draw.text((40, 58), "Kapsül Progress Bar + 4 Adet 3D Başlık Rozeti + Temiz LandFlow Arka Planı Entegrasyonu", fill=(160, 185, 215), font=font_subtitle)

cards = [
    {
        'title': '1. AÇILIŞ / YÜKLEME',
        'badge': 'BOOT SPLASH',
        'badge_col': (40, 160, 60),
        'sub': 'Ana Oyun Yükleme Ekranı',
        'desc': '• LandFlow Dikey Görsel (1080x1920)\n• 3D "Loading..." Bubbly Başlık\n• Kapsül Bar (%0-%100 Dinamik Dolum)',
        'img': screen1
    },
    {
        'title': '2. BÖLÜM GEÇİŞİ',
        'badge': 'LEVEL COMPLETE',
        'badge_col': (36, 120, 215),
        'sub': 'Sonraki Seviyeye Geçiş',
        'desc': '• 3D "Level Completed..." Rozeti\n• 3D LandFlow Logosu + Okyanus Zemin\n• Organik Kapsül Bar Dolumu',
        'img': screen2
    },
    {
        'title': '3. YENİDEN BAŞLATMA',
        'badge': 'RESTARTING',
        'badge_col': (175, 105, 25),
        'sub': 'Hızlı Seviye Reset Ekranı',
        'desc': '• 3D "Restarting..." Rozeti\n• Nefes alan 3D Logo ve Başlık\n• Takılmasız Hızlı Yükleme',
        'img': screen3
    },
    {
        'title': '4. SEVİYE BAŞARISIZ',
        'badge': 'LEVEL FAILED',
        'badge_col': (205, 50, 50),
        'sub': 'Seviye Kaybedildiğinde',
        'desc': '• 3D "Level Failed..." Rozeti\n• Tatlı ve Neşeli Hata Akışı\n• Otomatik Yumuşak Yeniden Başlatma',
        'img': screen4
    }
]

card_w = 440
card_h = 840
start_y = 95
gap = 24
margin_x = 40

for i, c in enumerate(cards):
    px = margin_x + i * (card_w + gap)
    py = start_y
    
    # card frame
    draw.rounded_rectangle([px, py, px + card_w, py + card_h], radius=16, fill=(24, 29, 37), outline=(50, 60, 75), width=2)
    
    # Title
    draw.text((px + 18, py + 16), c['title'], fill=(255, 255, 255), font=font_card_h)
    
    # Badge
    bw = 130
    draw.rounded_rectangle([px + card_w - bw - 18, py + 14, px + card_w - 18, py + 38], radius=8, fill=c['badge_col'])
    draw.text((px + card_w - bw - 8, py + 18), c['badge'], fill=(255, 255, 255), font=font_badge)
    
    # Sub
    draw.text((px + 18, py + 42), c['sub'], fill=(180, 195, 215), font=font_desc)
    
    # Screen thumbnail (aspect 9:16)
    thumb_w = 340
    thumb_h = int(thumb_w * 16 / 9) # 604
    thumb = c['img'].resize((thumb_w, thumb_h), Image.Resampling.LANCZOS)
    bg.paste(thumb, (px + (card_w - thumb_w)//2, py + 72))
    
    draw.rounded_rectangle([px + (card_w - thumb_w)//2 - 1, py + 71, px + (card_w + thumb_w)//2 + 1, py + 72 + thumb_h + 1], radius=8, outline=(65, 78, 98), width=2)
    
    # Description lines
    lines = c['desc'].split('\n')
    ly = py + 72 + thumb_h + 18
    for line in lines:
        draw.text((px + 20, ly), line, fill=(215, 225, 235), font=font_desc)
        ly += 22

out_path = r'C:\Users\ezgid\.gemini\antigravity-ide\brain\a9d16b58-e6b6-4029-9285-eb2e36a68900\integrated_loading_and_transitions_preview.png'
bg.save(out_path)
print('Master integration preview saved to:', out_path)
