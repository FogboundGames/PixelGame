"""Reklam efektleri: el göstergesi, dokunma dalgası, kızarma, sarsıntı, yıldız, kapanış kartı (logo + isim + Play Now)."""
import math, os
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops

FONT = "/home/emre/Belgeler/GitHub/PixelGame/Assets/Fonts/TitanOne-Regular.ttf"
ICON = os.path.expanduser("~/Resimler/PixelGame_Ikon/logo_ikon.png")
WORDMARK = "/home/emre/Belgeler/GitHub/PixelGame/Assets/UI/LandFlow_Logo.png"
W, H = 1080, 1920
NAVY = (18, 32, 80)


def font(s):
    return ImageFont.truetype(FONT, s)


def fade(img, a):
    if a >= 0.999:
        return img
    r, g, b, al = img.split()
    return Image.merge("RGBA", (r, g, b, al.point(lambda v: int(v * max(0.0, a)))))


# ---------------------------------------------------------------- el göstergesi
def make_hand(width=210):
    """Emoji 👆 silüetinden beyaz eldiven + koyu kontur + yumuşak gölge. Dönüş: (görsel, parmak ucu offseti)."""
    emo = ImageFont.truetype("/usr/share/fonts/noto/NotoColorEmoji.ttf", 109)
    im = Image.new("RGBA", (160, 160), (0, 0, 0, 0))
    ImageDraw.Draw(im).text((10, 10), "\U0001F446", font=emo, embedded_color=True)
    im = im.crop(im.getbbox())
    S = 6
    big = im.split()[3].resize((im.width * S, im.height * S), Image.LANCZOS).filter(ImageFilter.GaussianBlur(S * 0.8))
    mask = big.point(lambda v: 255 if v > 120 else 0)
    outline = mask.filter(ImageFilter.MaxFilter(S * 4 + 1))
    pad = S * 10
    canvas = Image.new("RGBA", (mask.width + pad * 2, mask.height + pad * 2), (0, 0, 0, 0))
    sh = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    sh.paste((0, 0, 0, 110), (pad + S * 4, pad + S * 6), outline)
    canvas = Image.alpha_composite(canvas, sh.filter(ImageFilter.GaussianBlur(S * 4)))
    canvas.paste(NAVY + (255,), (pad, pad), outline)
    canvas.paste((255, 255, 255, 255), (pad, pad), mask)
    lum = im.convert("L").resize(mask.size, Image.LANCZOS)
    lines = lum.point(lambda v: 255 if v < 140 else 0)
    lines = ImageChops.multiply(lines, mask.filter(ImageFilter.MinFilter(S * 4 + 1)))
    canvas.paste((200, 210, 230, 255), (pad, pad), lines.filter(ImageFilter.GaussianBlur(S * 0.6)))
    bbox = canvas.getbbox()
    canvas = canvas.crop(bbox)
    scale = width / canvas.width
    hand = canvas.resize((width, int(canvas.height * scale)), Image.LANCZOS)
    m = mask.getbbox()
    tb = mask.crop((0, m[1], mask.width, m[1] + S * 3)).getbbox()
    tip_x = ((tb[0] + tb[2]) / 2 + pad - bbox[0]) * scale
    tip_y = (m[1] + pad - bbox[1]) * scale
    return hand, (int(tip_x), int(tip_y))


class HandTrack:
    """Dokunuşlar arasında elin süzülerek gitmesi ve basma animasyonu. taps: [(çıktı karesi, x, y)]."""
    GLIDE, PRESS = 12, 8

    def __init__(self, taps, hand, tip, rest=(860, 1760)):
        self.taps = sorted(taps)
        self.hand, self.tip = hand, tip
        self.rest = rest

    def state(self, i):
        prev = self.rest
        for k, (t, x, y) in enumerate(self.taps):
            start = t - self.GLIDE
            if i < start:
                if k == 0 and i < start - 8:
                    return None
                return prev, 1.0, max(0.0, min(1.0, (i - (start - 8)) / 8)) if k == 0 else 1.0
            if i < t:
                u = (i - start) / self.GLIDE
                u = u * u * (3 - 2 * u)
                px = prev[0] + (x - prev[0]) * u
                py = prev[1] + (y - prev[1]) * u - math.sin(u * math.pi) * 60
                return (px, py), 1.0, 1.0
            if i < t + self.PRESS:
                p = (i - t) / self.PRESS
                return (x, y), 1.0 - 0.14 * math.sin(p * math.pi), 1.0
            prev = (x, y)
        t_last = self.taps[-1][0] + self.PRESS if self.taps else 0
        a = max(0.0, 1.0 - (i - t_last - 20) / 12)
        return (prev, 1.0, a) if a > 0 else None

    def draw(self, fr, i):
        st = self.state(i)
        if st is None:
            return fr
        (x, y), sc, a = st
        h = self.hand if abs(sc - 1) < 1e-3 else self.hand.resize((int(self.hand.width * sc), int(self.hand.height * sc)), Image.LANCZOS)
        tx, ty = int(self.tip[0] * sc), int(self.tip[1] * sc)
        layer = Image.new("RGBA", fr.size, (0, 0, 0, 0))
        layer.alpha_composite(h, (int(x - tx + 8), int(y - ty + 18)))
        return Image.alpha_composite(fr, fade(layer, a))


def tap_ripple(fr, i, taps):
    """Parmak ucunda iki halkalı dalga."""
    d = ImageDraw.Draw(fr, "RGBA")
    for (t, x, y) in taps:
        k = i - t
        if 0 <= k < 14:
            for j, delay in enumerate((0, 4)):
                kk = k - delay
                if kk < 0:
                    continue
                r = 30 + kk * 11
                a = int(220 * max(0, 1 - kk / 10))
                d.ellipse([x - r, y - r, x + r, y + r], outline=(255, 255, 255, a), width=9 - j * 3)
    return fr


# ---------------------------------------------------------------- yanlış / doğru
def red_flash_layer():
    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).rounded_rectangle([90, 90, W - 90, H - 90], radius=160, fill=255)
    edge = Image.eval(mask.filter(ImageFilter.GaussianBlur(110)), lambda v: 255 - v)
    layer = Image.new("RGBA", (W, H), (235, 30, 40, 0))
    layer.putalpha(Image.eval(edge, lambda v: int(min(255, v * 0.95 + 45))))
    return layer


def sparkles(fr, k, x, y, color=(255, 230, 90), n=10, dur=18):
    if not (0 <= k < dur):
        return fr
    d = ImageDraw.Draw(fr, "RGBA")
    a = int(255 * (1 - k / dur))
    for j in range(n):
        ang = j / n * math.tau + 0.3
        dist = 40 + k * 9
        px, py = x + math.cos(ang) * dist, y + math.sin(ang) * dist * 0.8
        s = 14 * (1 - k / dur) + 4
        d.polygon([(px, py - s), (px + s * 0.35, py - s * 0.35), (px + s, py), (px + s * 0.35, py + s * 0.35),
                   (px, py + s), (px - s * 0.35, py + s * 0.35), (px - s, py), (px - s * 0.35, py - s * 0.35)], fill=color + (a,))
    return fr


def shake_offset(k, dur=10, amp=16):
    if not (0 <= k < dur):
        return 0, 0
    decay = 1 - k / dur
    return int(math.sin(k * 2.9) * amp * decay), int(math.cos(k * 3.7) * amp * 0.5 * decay)


def apply_shake(fr, dx, dy):
    if dx == 0 and dy == 0:
        return fr
    out = Image.new("RGBA", fr.size, (0, 0, 0, 255))
    big = fr.resize((int(W * 1.03), int(H * 1.03)), Image.BILINEAR)
    out.paste(big, (int(-W * 0.015) + dx, int(-H * 0.015) + dy))
    return out


# ---------------------------------------------------------------- kapanış kartı
def end_card_parts(title_lines, cta):
    """Statik kapanış katmanı (başlık + ikon + isim) ve ayrı Play Now butonu (nabız için)."""
    from make_overlays import outlined_lines
    base = Image.new("RGBA", (W, H), (10, 20, 50, 175))
    base = Image.alpha_composite(base, outlined_lines(title_lines, 108, 250))
    icon = Image.open(ICON).convert("RGBA").resize((430, 430), Image.LANCZOS)
    m = Image.new("L", icon.size, 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, icon.width - 1, icon.height - 1], radius=96, fill=255)
    icon.putalpha(m)
    frame = Image.new("RGBA", (icon.width + 28, icon.height + 28), (0, 0, 0, 0))
    ImageDraw.Draw(frame).rounded_rectangle([0, 0, frame.width - 1, frame.height - 1], radius=110, fill=(255, 255, 255, 255))
    frame.alpha_composite(icon, (14, 14))
    sh = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ix, iy = (W - frame.width) // 2, 560
    ImageDraw.Draw(sh).rounded_rectangle([ix + 6, iy + 20, ix + frame.width + 6, iy + frame.height + 20], radius=110, fill=(0, 0, 0, 140))
    base = Image.alpha_composite(base, sh.filter(ImageFilter.GaussianBlur(18)))
    base.alpha_composite(frame, (ix, iy))
    wm = Image.open(WORDMARK).convert("RGBA")
    wm = wm.crop(wm.getbbox())
    wm = wm.resize((760, int(wm.height * 760 / wm.width)), Image.LANCZOS)
    base.alpha_composite(wm, ((W - wm.width) // 2, iy + frame.height + 30))
    bw, bh = 640, 180
    btn = Image.new("RGBA", (bw + 24, bh + 24), (0, 0, 0, 0))
    d = ImageDraw.Draw(btn)
    d.rounded_rectangle([0, 0, bw + 23, bh + 23], radius=66, fill=(250, 252, 255))
    body = Image.new("RGBA", (bw, bh))
    bd = ImageDraw.Draw(body)
    for yy in range(bh):
        t = yy / bh
        bd.line([(0, yy), (bw, yy)], fill=tuple(int(a + (b - a) * t) for a, b in zip((0x3a, 0xc4, 0x4f), (0x1f, 0x9a, 0x35))) + (255,))
    bd.rectangle([0, bh - 24, bw, bh], fill=(0x13, 0x6e, 0x24, 255))
    bd.rectangle([0, 0, bw, 10], fill=(0xb6, 0xf2, 0xbf, 255))
    mk = Image.new("L", (bw, bh), 0)
    ImageDraw.Draw(mk).rounded_rectangle([0, 0, bw - 1, bh - 1], radius=56, fill=255)
    btn.paste(body, (12, 12), mk)
    d = ImageDraw.Draw(btn)
    fs = 104
    f = font(fs)
    while d.textlength(cta, font=f) > bw - 70 and fs > 50:
        fs -= 4; f = font(fs)
    cw = d.textlength(cta, font=f)
    d.text(((btn.width - cw) / 2, (btn.height - fs) / 2 - 8), cta, font=f, fill=(255, 255, 255), stroke_width=8, stroke_fill=(16, 80, 30))
    btn_center = (W // 2, iy + frame.height + 30 + wm.height + 70 + btn.height // 2)
    return base, btn, btn_center


def draw_end(fr, k, base, btn, center, hand=None):
    """k: kapanış kartının kaçıncı karesi. Kart belirir, buton nabız atar, el butona basar."""
    a = min(1.0, (k + 1) / 8)
    fr = Image.alpha_composite(fr, base if a >= 1 else fade(base, a))
    if k >= 6:
        kk = k - 6
        sc = max(0.05, (1 - (1 - min(1, kk / 8)) ** 3) + 0.05 * math.sin(kk * 0.35) * min(1, kk / 8))
        b = btn.resize((int(btn.width * sc), int(btn.height * sc)), Image.LANCZOS)
        fr.alpha_composite(b, (int(center[0] - b.width / 2), int(center[1] - b.height / 2)))
    if hand is not None and k >= 18:
        hk = k - 18
        img, tip = hand
        press = 1 - 0.12 * max(0, math.sin((hk % 22) / 22 * math.pi)) if hk > 10 else 1
        h = img.resize((int(img.width * press), int(img.height * press)), Image.LANCZOS)
        tx, ty = center[0] + 150, center[1] + 30
        u = min(1.0, hk / 10)
        sx, sy = tx + 160 * (1 - u), ty + 260 * (1 - u)
        lay = Image.new("RGBA", fr.size, (0, 0, 0, 0))
        lay.alpha_composite(h, (int(sx - tip[0] * press), int(sy - tip[1] * press)))
        fr = Image.alpha_composite(fr, fade(lay, u))
    return fr
