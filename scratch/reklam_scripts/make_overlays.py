"""Reklam yazı katmanları (1080x1920 şeffaf PNG): kontürlü başlıklar. Font: Titan One (Türkçe harfler; İ elle noktalanır)."""
import os
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = "/home/emre/Belgeler/GitHub/PixelGame/Assets/Fonts/"
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "katmanlar")
os.makedirs(OUT, exist_ok=True)
W, H = 1080, 1920


def font(size):
    return ImageFont.truetype(ROOT + "TitanOne-Regular.ttf", size)


def top_band(h=420, alpha=150):
    """Yazının okunması için üstte koyu, aşağı doğru kaybolan bant."""
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for y in range(h):
        a = int(alpha * (1 - (y / h) ** 1.6))
        d.line([(0, y), (W, y)], fill=(8, 18, 48, a))
    return img


def outlined_lines(lines, size, y0, fill=(255, 255, 255), stroke=(18, 32, 80), sw=14, gap=10, max_w=980, band=False):
    img = top_band() if band else Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    y = y0
    for ln in lines:
        plain = ln.replace("İ", "I")
        s = size
        f = font(s)
        while d.textlength(plain, font=f) > max_w and s > 40:
            s -= 4; f = font(s)
        w = d.textlength(plain, font=f)
        x = (W - w) / 2
        sh = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        ImageDraw.Draw(sh).text((x, y + 10), plain, font=f, fill=(0, 0, 0, 120), stroke_width=sw, stroke_fill=(0, 0, 0, 120))
        img = Image.alpha_composite(img, sh.filter(ImageFilter.GaussianBlur(6)))
        d = ImageDraw.Draw(img)
        d.text((x, y), plain, font=f, fill=fill, stroke_width=sw, stroke_fill=stroke)
        # Fontta İ bileşik çiziliyor (arada boşluk): I yazılır, noktası elle eklenir
        top = f.getbbox("I")[1]
        for k, ch in enumerate(ln):
            if ch != "İ":
                continue
            cx = x + d.textlength(plain[:k], font=f) + d.textlength("I", font=f) / 2
            r = max(6, s // 11)
            cy = y + top - r - s * 0.06
            d.ellipse([cx - r - sw * 0.6, cy - r - sw * 0.6, cx + r + sw * 0.6, cy + r + sw * 0.6], fill=stroke)
            d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=fill)
        y += s + gap
    return img
