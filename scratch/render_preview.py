import os
from PIL import Image, ImageDraw, ImageFont

def render_9slice(sprite_path, target_w, target_h, border):
    """Render a 9-sliced sprite to target dimensions."""
    src = Image.open(sprite_path).convert("RGBA")
    sw, sh = src.size
    bx, by, bz, bw = border # left, bottom, right, top
    left, bottom, right, top = int(bx), int(by), int(bz), int(bw)
    
    dst = Image.new("RGBA", (target_w, target_h), (0, 0, 0, 0))
    
    # 9 regions:
    # Corners
    c_tl = src.crop((0, 0, left, top))
    c_tr = src.crop((sw - right, 0, sw, top))
    c_bl = src.crop((0, sh - bottom, left, sh))
    c_br = src.crop((sw - right, sh - bottom, sw, sh))
    
    # Edges
    e_t = src.crop((left, 0, sw - right, top)).resize((max(1, target_w - left - right), top), Image.Resampling.BILINEAR)
    e_b = src.crop((left, sh - bottom, sw - right, sh)).resize((max(1, target_w - left - right), bottom), Image.Resampling.BILINEAR)
    e_l = src.crop((0, top, left, sh - bottom)).resize((left, max(1, target_h - top - bottom)), Image.Resampling.BILINEAR)
    e_r = src.crop((sw - right, top, sw, sh - bottom)).resize((right, max(1, target_h - top - bottom)), Image.Resampling.BILINEAR)
    
    # Center
    center_w = max(1, target_w - left - right)
    center_h = max(1, target_h - top - bottom)
    if sw - left - right > 0 and sh - top - bottom > 0:
        c_mid = src.crop((left, top, sw - right, sh - bottom)).resize((center_w, center_h), Image.Resampling.BILINEAR)
        dst.paste(c_mid, (left, top))
    
    dst.paste(c_tl, (0, 0))
    dst.paste(c_tr, (target_w - right, 0))
    dst.paste(c_bl, (0, target_h - bottom))
    dst.paste(c_br, (target_w - right, target_h - bottom))
    
    dst.paste(e_t, (left, 0))
    dst.paste(e_b, (left, target_h - bottom))
    dst.paste(e_l, (0, top))
    dst.paste(e_r, (target_w - right, top))
    
    return dst

W, H = 1080, 1920
screen = Image.new("RGBA", (W, H), (20, 28, 52, 255))

# 1. Background
bg_path = r"Assets\UI\CasualUI\bg_dark_navy.png"
if os.path.exists(bg_path):
    bg = Image.open(bg_path).resize((W, H), Image.Resampling.BILINEAR)
    screen.paste(bg, (0, 0))

# Fonts
font_path = r"Assets\Fonts\LilitaOne-Regular.ttf"
font_title = ImageFont.truetype(font_path, 52)
font_widget = ImageFont.truetype(font_path, 32)
font_counter = ImageFont.truetype(font_path, 40)
font_badge = ImageFont.truetype(font_path, 48)

# 2. Top UI Bar
top_y = 60
# Settings Button
btn_set = Image.open(r"Assets\UI\CasualUI\btn_settings.png").resize((96, 96), Image.Resampling.BILINEAR)
screen.paste(btn_set, (50, top_y), btn_set)

# Level Badge (Center)
lvl_text = "LEVEL 1"
bbox = font_title.getbbox(lvl_text)
tw = bbox[2] - bbox[0]
draw = ImageDraw.Draw(screen)
# Draw outline/shadow
tx = (W - tw) // 2
ty = top_y + 16
for dx, dy in [(-2, 0), (2, 0), (0, -2), (0, 3), (-2, 2), (2, 2)]:
    draw.text((tx + dx, ty + dy), lvl_text, font=font_title, fill=(15, 22, 44, 255))
draw.text((tx, ty), lvl_text, font=font_title, fill=(255, 255, 255, 255))

# Pill Widgets (Lives & Coins)
pill_sprite = r"Assets\UI\CasualUI\ui_pill.png"
pill_lives = render_9slice(pill_sprite, 135, 66, (44, 44, 44, 44))
screen.paste(pill_lives, (W - 400, top_y + 15), pill_lives)

heart_icon = Image.open(r"Assets\UI\CasualUI\icon_heart.png").resize((40, 40), Image.Resampling.BILINEAR)
screen.paste(heart_icon, (W - 390, top_y + 28), heart_icon)

draw.text((W - 335, top_y + 28), "3", font=font_widget, fill=(255, 255, 255, 255))

plus_icon = Image.open(r"Assets\UI\CasualUI\btn_plus.png").resize((28, 28), Image.Resampling.BILINEAR)
screen.paste(plus_icon, (W - 295, top_y + 34), plus_icon)

pill_coins = render_9slice(pill_sprite, 165, 66, (44, 44, 44, 44))
screen.paste(pill_coins, (W - 230, top_y + 15), pill_coins)

coin_icon = Image.open(r"Assets\UI\CasualUI\icon_coin.png").resize((40, 40), Image.Resampling.BILINEAR)
screen.paste(coin_icon, (W - 220, top_y + 28), coin_icon)

draw.text((W - 165, top_y + 28), "250", font=font_widget, fill=(255, 255, 255, 255))
screen.paste(plus_icon, (W - 95, top_y + 34), plus_icon)

# 3. Game Board (Center: 540, 780)
bw, bh = 880, 920
bx = (W - bw) // 2
by = 280

# Board Shadow
shadow_w, shadow_h = bw + 80, bh + 80
board_shadow = render_9slice(r"Assets\UI\CasualUI\board_shadow.png", shadow_w, shadow_h, (52, 52, 52, 52))
screen.paste(board_shadow, (bx - 40, by - 20), board_shadow)

# Board Frame
board_frame = render_9slice(r"Assets\UI\CasualUI\board_frame_25d.png", bw, bh, (48, 48, 48, 48))
screen.paste(board_frame, (bx, by), board_frame)

# Board Inner Well
inner_w, inner_h = bw - 80, bh - 80
board_inner = render_9slice(r"Assets\UI\CasualUI\board_inner_well.png", inner_w, inner_h, (36, 36, 36, 36))
screen.paste(board_inner, (bx + 40, by + 40), board_inner)

# Draw simulated pixel cubes inside the board inner well
grid_w, grid_h = 16, 16
cell_size = (inner_w - 60) // grid_w
grid_start_x = bx + 40 + (inner_w - cell_size * grid_w) // 2
grid_start_y = by + 40 + (inner_h - cell_size * grid_h) // 2

# Draw sample raccoon / pixel pattern
cube_colors = [
    (240, 180, 70), (140, 110, 80), (60, 50, 45), (230, 225, 220), (80, 140, 220), (220, 80, 80)
]
import math
for gy in range(grid_h):
    for gx in range(grid_w):
        # make a circular/cute silhouette
        cx, cy = gx - grid_w / 2, gy - grid_h / 2
        dist = math.sqrt(cx * cx + cy * cy)
        if dist < 6.5:
            col_idx = int((gx + gy) % len(cube_colors))
            c = cube_colors[col_idx]
            px = grid_start_x + gx * cell_size
            py = grid_start_y + gy * cell_size
            # cube shadow
            draw.rounded_rectangle([px + 2, py + 3, px + cell_size - 1, py + cell_size], radius=4, fill=(15, 20, 38, 120))
            # cube body
            draw.rounded_rectangle([px, py, px + cell_size - 2, py + cell_size - 2], radius=4, fill=c)
            # top bevel highlight
            draw.line([(px + 1, py + 1), (px + cell_size - 3, py + 1)], fill=(255, 255, 255, 110), width=2)

# 4. Perimeter Track & Moving Wagons
# Draw simulated corner progress pod (2/5)
pod_w, pod_h = 100, 100
pod_x = bx - 25
pod_y = by + bh - 90
pod = render_9slice(r"Assets\UI\CasualUI\progress_station_pod.png", pod_w, pod_h, (36, 36, 36, 36))
screen.paste(pod, (pod_x, pod_y), pod)

p_bbox = font_counter.getbbox("2/5")
ptw = p_bbox[2] - p_bbox[0]
draw.text((pod_x + (pod_w - ptw) // 2, pod_y + 24), "2/5", font=font_counter, fill=(255, 255, 255, 255))

# 5. Bottom 5 Slots
slot_y = 1320
slot_w, slot_h = 165, 175
gap = (W - 100 - 5 * slot_w) // 4
start_slot_x = 50

slot_pod_sprite = r"Assets\UI\CasualUI\slot_pod_25d.png"
slot_shadow_sprite = r"Assets\UI\CasualUI\slot_shadow.png"

for i in range(5):
    sx = start_slot_x + i * (slot_w + gap)
    # slot shadow
    s_shadow = render_9slice(slot_shadow_sprite, slot_w + 30, slot_h + 30, (36, 36, 36, 36))
    screen.paste(s_shadow, (sx - 15, slot_y - 5), s_shadow)
    
    # slot pod
    s_pod = render_9slice(slot_pod_sprite, slot_w, slot_h, (42, 42, 42, 42))
    screen.paste(s_pod, (sx, slot_y), s_pod)
    
    # Add a wagon badge on some slots
    if i in (1, 3):
        # simulated bottle/cannon wagon
        badge_text = "20" if i == 1 else "30"
        b_bbox = font_badge.getbbox(badge_text)
        btw = b_bbox[2] - b_bbox[0]
        bx_pos = sx + (slot_w - btw) // 2
        by_pos = slot_y + 55
        for dx, dy in [(-2, 0), (2, 0), (0, -2), (0, 2)]:
            draw.text((bx_pos + dx, by_pos + dy), badge_text, font=font_badge, fill=(10, 16, 32, 255))
        draw.text((bx_pos, by_pos), badge_text, font=font_badge, fill=(255, 255, 255, 255))

# 6. Character / Visual Support Area (Between slots and bottom)
char_y = 1580
# Soft platform shadow
draw.ellipse([W // 2 - 250, char_y + 120, W // 2 + 250, char_y + 180], fill=(12, 16, 30, 140))
# Label for waiting queue / pool
pool_text = "WAITING WAGONS"
p_bbox = font_widget.getbbox(pool_text)
ptw = p_bbox[2] - p_bbox[0]
draw.text(((W - ptw) // 2, char_y + 160), pool_text, font=font_widget, fill=(130, 155, 205, 180))

out_path = r"scratch\gameplay_view_9_16.png"
screen.save(out_path)
print(f"Full 9:16 mobile render saved to: {out_path}")
