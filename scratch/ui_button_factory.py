import os
import math
import numpy as np
import cv2
from PIL import Image, ImageDraw, ImageFilter

def create_squircle_base(top_color, bot_color, size=1024):
    """
    Renders the premium 3D casual candy squircle button frame at 1024x1024:
    - Soft deep ambient occlusion & drop shadow
    - 3D Metallic golden bottom tray / bevel
    - Crisp white outer border with subtle rim shading
    - Juicy gradient candy core with top glass/jelly highlight
    """
    canvas = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    margin = 76
    radius = 240
    
    # 1. Soft Drop Shadow
    shadow = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    s_draw = ImageDraw.Draw(shadow)
    s_draw.rounded_rectangle(
        [margin, margin + 56, size - margin, size - margin + 56],
        radius=radius,
        fill=(0, 0, 0, 120)
    )
    shadow = shadow.filter(ImageFilter.GaussianBlur(32))
    canvas = Image.alpha_composite(canvas, shadow)

    draw = ImageDraw.Draw(canvas)

    # 2. Bottom Golden Bevel / Tray (3D base)
    gold_dark = (175, 105, 8, 255)
    gold_mid  = (255, 195, 35, 255)
    draw.rounded_rectangle([margin - 4, margin + 44, size - margin + 4, size - margin + 44], radius=radius + 4, fill=gold_dark)
    draw.rounded_rectangle([margin - 4, margin + 24, size - margin + 4, size - margin + 24], radius=radius + 4, fill=gold_mid)

    # 3. Thick Crisp White Outer Frame
    draw.rounded_rectangle([margin, margin, size - margin, size - margin], radius=radius, fill=(255, 255, 255, 255))

    # Inner bevel shadow on white rim
    rim_sh = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    rs_draw = ImageDraw.Draw(rim_sh)
    rs_draw.rounded_rectangle([margin + 4, margin + 16, size - margin - 4, size - margin + 4], radius=radius - 4, fill=(210, 205, 200, 180))
    rim_sh = rim_sh.filter(ImageFilter.GaussianBlur(10))
    canvas = Image.alpha_composite(canvas, rim_sh)

    # 4. Button Core (Candy Gradient)
    c_margin = margin + 36
    inner_w = size - 2 * c_margin
    inner_h = inner_w
    inner_img = Image.new('RGBA', (inner_w, inner_h), (0, 0, 0, 0))
    i_draw = ImageDraw.Draw(inner_img)

    for y in range(inner_h):
        t = y / float(inner_h)
        t_curv = t * t * (3.0 - 2.0 * t) # smoothstep
        cr = int(top_color[0] * (1.0 - t_curv) + bot_color[0] * t_curv)
        cg = int(top_color[1] * (1.0 - t_curv) + bot_color[1] * t_curv)
        cb = int(top_color[2] * (1.0 - t_curv) + bot_color[2] * t_curv)
        i_draw.line([0, y, inner_w, y], fill=(cr, cg, cb, 255))

    core_mask = Image.new('L', (inner_w, inner_h), 0)
    m_draw = ImageDraw.Draw(core_mask)
    m_draw.rounded_rectangle([0, 0, inner_w, inner_h], radius=radius - 48, fill=255)

    # Bottom inner shadow in core (depth)
    bot_sh = Image.new('RGBA', (inner_w, inner_h), (0, 0, 0, 0))
    bs_draw = ImageDraw.Draw(bot_sh)
    bs_draw.rectangle([0, int(inner_h * 0.65), inner_w, inner_h], fill=(0, 0, 0, 75))
    bot_sh = bot_sh.filter(ImageFilter.GaussianBlur(28))
    inner_img = Image.alpha_composite(inner_img, bot_sh)

    # Glossy jelly highlight arc on top
    gloss = Image.new('RGBA', (inner_w, inner_h), (0, 0, 0, 0))
    g_draw = ImageDraw.Draw(gloss)
    g_draw.ellipse([-inner_w * 0.30, -inner_h * 0.75, inner_w * 1.30, inner_h * 0.52], fill=(255, 255, 255, 135))
    gloss = gloss.filter(ImageFilter.GaussianBlur(18))
    inner_img = Image.alpha_composite(inner_img, gloss)

    # Paste core
    core_full = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    core_full.paste(inner_img, (c_margin, c_margin), core_mask)
    canvas = Image.alpha_composite(canvas, core_full)

    return canvas

def composite_icon_with_3d_effects(base_button, icon_mask_1024):
    """
    Takes a 1024x1024 binary/alpha mask of the icon shape and applies:
    1. Multi-tier drop shadows (crisp contact shadow + soft ambient shadow)
    2. Subtle dark edge bevel / outline for physical embossing
    3. Pure white body with subtle top-to-bottom shading
    4. Top specular highlight
    """
    size = base_button.size[0]
    out = base_button.copy()

    # Convert icon_mask to numpy
    mask_np = np.array(icon_mask_1024)
    if len(mask_np.shape) == 3:
        mask_np = mask_np[:, :, 3]

    # 1. Ambient drop shadow (offset y=+20, blur=24)
    sh1 = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    sh1_np = np.zeros((size, size), dtype=np.uint8)
    sh1_np[20:, :] = mask_np[:-20, :]
    sh1_pil = Image.fromarray(sh1_np, 'L')
    sh1_blur = sh1_pil.filter(ImageFilter.GaussianBlur(18))
    sh1_draw = Image.new('RGBA', (size, size), (0, 0, 0, 110))
    sh1_draw.putalpha(sh1_blur)
    out = Image.alpha_composite(out, sh1_draw)

    # 2. Contact drop shadow (offset y=+10, blur=8)
    sh2_np = np.zeros((size, size), dtype=np.uint8)
    sh2_np[10:, :] = mask_np[:-10, :]
    sh2_pil = Image.fromarray(sh2_np, 'L')
    sh2_blur = sh2_pil.filter(ImageFilter.GaussianBlur(8))
    sh2_draw = Image.new('RGBA', (size, size), (0, 0, 0, 140))
    sh2_draw.putalpha(sh2_blur)
    out = Image.alpha_composite(out, sh2_draw)

    # 3. Icon Body with subtle gradient (top pure white, bottom slightly cool silver)
    body = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    b_draw = ImageDraw.Draw(body)
    for y in range(size):
        t = y / float(size)
        v = int(255 - t * 14) # 255 down to 241
        b_draw.line([0, y, size, y], fill=(v, v, min(255, v + 2), 255))
    
    icon_pil_mask = Image.fromarray(mask_np, 'L')
    body.putalpha(icon_pil_mask)
    out = Image.alpha_composite(out, body)

    # 4. Top inner highlight (eroded mask offset down subtracted from body)
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7))
    eroded = cv2.erode(mask_np, kernel)
    highlight_np = np.clip(mask_np.astype(np.int16) - eroded.astype(np.int16), 0, 255).astype(np.uint8)
    cutoff = int(size * 0.55)
    highlight_np[cutoff:, :] = 0
    hl_pil = Image.fromarray(highlight_np, 'L').filter(ImageFilter.GaussianBlur(3))
    hl_draw = Image.new('RGBA', (size, size), (255, 255, 255, 160))
    hl_draw.putalpha(hl_pil)
    out = Image.alpha_composite(out, hl_draw)

    return out
