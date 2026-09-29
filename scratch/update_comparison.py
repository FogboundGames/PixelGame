from PIL import Image, ImageDraw, ImageFont

ref = Image.open(r'C:\Users\ezgid\.gemini\antigravity-ide\brain\6ac0b548-cac2-41f9-855a-5751dc5d95b1\.user_uploaded\media_1790720486836.png')
bar_h = 60
bar_w = ref.size[0]

gen_dir = 'scratch/test_subhead_gen'
btn_restart = Image.open(f'{gen_dir}/btn_restart.png')
badge_hard = Image.open(f'{gen_dir}/badge_hard.png')
btn_sound = Image.open(f'{gen_dir}/btn_sound.png')
btn_music = Image.open(f'{gen_dir}/btn_music.png')
capsule_bg = Image.open(f'{gen_dir}/bg_level_capsule.png')

sand_bg = Image.new('RGBA', (bar_w, bar_h), (245, 222, 178, 255))

btn_size = 52
r_btn = btn_restart.resize((btn_size, btn_size), Image.Resampling.LANCZOS)
s_btn = btn_sound.resize((btn_size, btn_size), Image.Resampling.LANCZOS)
m_btn = btn_music.resize((btn_size, btn_size), Image.Resampling.LANCZOS)

badge_h = 44
badge_w = int(badge_hard.width * (badge_h / badge_hard.height))
b_badge = badge_hard.resize((badge_w, badge_h), Image.Resampling.LANCZOS)

cap_h = 38
cap_w = 135
c_bg = capsule_bg.resize((cap_w, cap_h), Image.Resampling.LANCZOS)

sand_bg.paste(r_btn, (15, 4), r_btn)
sand_bg.paste(b_badge, (78, 8), b_badge)
sand_bg.paste(c_bg, (195, 11), c_bg)

font = ImageFont.truetype('Assets/Fonts/LilitaOne-Regular.ttf', 32)
d = ImageDraw.Draw(sand_bg)
text = 'Level 35'
bbox = font.getbbox(text)
tw = bbox[2] - bbox[0]
th = bbox[3] - bbox[1]
tx = 195 + (cap_w - tw)//2 - bbox[0]
ty = 11 + (cap_h - th)//2 - bbox[1]

outline_col = (61, 118, 178, 255)
for dx in range(-3, 4):
    for dy in range(-3, 4):
        if dx*dx + dy*dy <= 9:
            d.text((tx + dx, ty + dy + 1), text, font=font, fill=outline_col)
d.text((tx, ty), text, font=font, fill=(255, 255, 255, 255))

sand_bg.paste(s_btn, (340, 4), s_btn)
sand_bg.paste(m_btn, (400, 4), m_btn)

comparison = Image.new('RGBA', (bar_w, bar_h * 2 + 10), (255, 255, 255, 255))
comparison.paste(ref, (0, 0))
comparison.paste(sand_bg, (0, bar_h + 10))
comparison.save('scratch/subhead_comparison.png')
print('Comparison updated successfully!')
