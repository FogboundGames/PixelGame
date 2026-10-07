from PIL import Image, ImageDraw, ImageFont

before_crop = Image.open('scratch/rocket_tip_crop.png')
after_crop = Image.open('scratch/simulated_tip_refined.png')
tex = Image.open('Assets/Resources/mystery_cube_question.png')

card_w = 840
card_h = 360
comp = Image.new('RGBA', (card_w, card_h), (24, 25, 35, 255))
draw = ImageDraw.Draw(comp)

font_path = r'Assets\Fonts\LilitaOne-Regular.ttf'
sub_font = ImageFont.truetype(font_path, 15)
title_font = ImageFont.truetype(font_path, 15)

# Labels
draw.text((20, 16), 'Onceki (Soru isareti yok)', font=title_font, fill=(240, 110, 110, 255))
draw.text((290, 16), 'Yeni (Kup Dokusuna \"?\" Eklendi)', font=title_font, fill=(90, 230, 150, 255))
draw.text((570, 16), 'Yeni Doku (512x512)', font=title_font, fill=(160, 190, 255, 255))

# Paste crops resized to 230x230
c1 = before_crop.resize((230, 230), Image.Resampling.LANCZOS)
c2 = after_crop.resize((230, 230), Image.Resampling.LANCZOS)
c3 = tex.resize((210, 210), Image.Resampling.LANCZOS)

comp.paste(c1, (20, 45))
comp.paste(c2, (290, 45))
comp.paste(c3, (570, 55), mask=c3)

# Border lines
draw.rectangle([(19, 44), (251, 276)], outline=(70, 70, 90, 255), width=2)
draw.rectangle([(289, 44), (521, 276)], outline=(80, 180, 120, 255), width=2)

draw.text((20, 290), '- Zemin: #201E4E (Gizli Gemi deseniyle tam uyumlu)', font=sub_font, fill=(200, 205, 220, 255))
draw.text((20, 312), '- Merkez: Kalin beyaz Lilita One \"?\" + koyu kontur (#121216)', font=sub_font, fill=(200, 205, 220, 255))
draw.text((20, 334), '- Cevre: 2. gorseldeki (gizli gemi) gibi serpilmis mini soru isaretleri', font=sub_font, fill=(200, 205, 220, 255))

comp.save('scratch/mystery_cube_texture_comparison.png')
print('Comparison saved successfully!')
