"""PixelGame level resimleri için küçük piksel sanat kütüphanesi (32×32).

Bu dosyayı CIZIM-REHBERI.md anlatır. Kısaca:

    from pixelkit import *
    c = Canvas()
    c.fill(band(0, 15), SKY)                       # arka plan bandı
    c.shape(circ(16, 18, 7), BODY, LIGHT, DARK, OUT)  # dolgu + ışık/gölge kenarı + kontur
    c.save('Kedi.png')                             # 32×32 PNG
    print(c.report())                              # küp, renk sayısı, kural uyarıları

Koordinatlar: x sağa, y AŞAĞI (resim koordinatı). Şekil fonksiyonları piksel kümesi (set of (x, y)) döndürür;
kümeler | (birleşim), - (fark), & (kesişim) ile birleştirilir. Merkez/yarıçap gibi değerler ondalık olabilir:
piksel merkezi (x+0.5, y+0.5) şeklin içindeyse piksel dahildir.
"""
import math
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFont

W = H = 32
N4 = ((1, 0), (-1, 0), (0, 1), (0, -1))
ALL = {(x, y) for y in range(H) for x in range(W)}

# Proje genelinde kullanılan kontur rengi (koyu mor-siyah). Figür rengine yakın kontur kullanma.
OUTLINE = (34, 26, 40)


# ------------------------------------------------------------------ şekiller (piksel kümeleri)

def M(fn):
    """fn(x, y) piksel merkezinde True dönen tüm pikseller."""
    return {(x, y) for (x, y) in ALL if fn(x + .5, y + .5)}


def circ(cx, cy, r):
    return M(lambda x, y: math.hypot(x - cx, y - cy) <= r)


def ring(cx, cy, r_out, r_in):
    """Halka: kilit yapmanın en kolay yolu (içindeki renk, halka kalkana kadar kapalı kalır)."""
    return circ(cx, cy, r_out) - circ(cx, cy, r_in)


def ell(cx, cy, rx, ry, rot=0.0):
    """Elips; rot radyan cinsinden döndürme."""
    c, s = math.cos(rot), math.sin(rot)

    def f(x, y):
        dx, dy = x - cx, y - cy
        u, v = dx * c + dy * s, -dx * s + dy * c
        return (u / rx) ** 2 + (v / ry) ** 2 <= 1
    return M(f)


def rect(x0, y0, x1, y1):
    """Dikdörtgen, iki uç dahil (tam sayı piksel koordinatı)."""
    x0, y0, x1, y1 = int(x0), int(y0), int(x1), int(y1)
    return {(x, y) for x in range(x0, x1 + 1) for y in range(y0, y1 + 1) if 0 <= x < W and 0 <= y < H}


def band(y0, y1):
    """Tam genişlik yatay bant (gökyüzü/deniz katmanları). DİKKAT: tahtanın kenarına değer → baştan açıkta."""
    return rect(0, y0, W - 1, y1)


def poly(pts):
    """Çokgen: [(x, y), ...]"""
    def f(px, py):
        ins = False
        for i in range(len(pts)):
            (x1, y1), (x2, y2) = pts[i], pts[i - 1]
            if (y1 > py) != (y2 > py) and px < (x2 - x1) * (py - y1) / (y2 - y1) + x1:
                ins = not ins
        return ins
    return M(f)


def seg(pts, r):
    """Kalın çizgi/kıvrım: noktalardan geçen, r yarıçaplı kalem. Halat, gövde, sap, dal için."""
    out = set()
    for (x1, y1), (x2, y2) in zip(pts, pts[1:]):
        n = int(math.hypot(x2 - x1, y2 - y1) * 4) + 1
        for i in range(n + 1):
            t = i / n
            out |= circ(x1 + (x2 - x1) * t, y1 + (y2 - y1) * t, r)
    return out


def edge(m):
    """m'nin dış kenar pikselleri (m dışına komşu olanlar)."""
    return {(x, y) for (x, y) in m if any((x + dx, y + dy) not in m and 0 <= x + dx < W and 0 <= y + dy < H for dx, dy in N4)}


# ------------------------------------------------------------------ tuval

class Canvas:
    def __init__(self):
        self.p = [[None] * W for _ in range(H)]

    def fill(self, m, c):
        """m kümesini c rengiyle boya (sonraki çizim öncekinin üstüne gelir)."""
        for x, y in m:
            if 0 <= x < W and 0 <= y < H:
                self.p[y][x] = c

    def outline(self, m, c=OUTLINE):
        """m'nin dış kenarını c yap. Tuval kenarı 'içeride' sayılır (kenara kontur çekilmez)."""
        self.fill(edge(m), c)

    def shape(self, m, base, light=None, dark=None, out=OUTLINE):
        """Dolgu + sol/üst kenar açık ton, sağ/alt kenar koyu ton (ışık sol üstten) + kontur.
        light/dark None ise o kenar boyanmaz; out None ise kontur çekilmez."""
        self.fill(m, base)
        inner = m - edge(m) if out else m
        for x, y in inner:
            gap = lambda dx, dy: (x + dx * 2, y + dy * 2) not in m and 0 <= x + dx * 2 < W and 0 <= y + dy * 2 < H
            if light and (gap(-1, 0) or gap(0, -1)):
                self.p[y][x] = light
            elif dark and (gap(1, 0) or gap(0, 1)):
                self.p[y][x] = dark
        if out:
            self.outline(m, out)

    def dots(self, m, c, every=6, seed=1):
        """m içine seyrek nokta serp (kum, yosun, yıldız dokusu)."""
        rnd = random.Random(seed)
        for p in sorted(m):
            if rnd.random() < 1 / every:
                self.fill({p}, c)

    def where(self, color):
        """Şu an bu renkte olan pikseller (yalnızca bir rengin üstüne doku eklemek için)."""
        return {(x, y) for (x, y) in ALL if self.p[y][x] == color}

    def image(self):
        im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
        for y in range(H):
            for x in range(W):
                if self.p[y][x] is not None:
                    im.putpixel((x, y), tuple(self.p[y][x]) + (255,))
        return im

    def save(self, path):
        self.image().save(path)
        return path

    def counts(self):
        cnt = {}
        for row in self.p:
            for c in row:
                if c is not None:
                    cnt[c] = cnt.get(c, 0) + 1
        return cnt

    def report(self):
        """Küp/renk sayısı ve rehberdeki teknik kuralların ihlalleri (kesin kontrol: level_asset.py art)."""
        cnt = self.counts()
        cubes = sum(cnt.values())
        msgs = [f'{cubes} küp, {len(cnt)} renk, doluluk {cubes / (W * H):.2f}']
        cols = list(cnt)
        for i in range(len(cols)):
            for j in range(i + 1, len(cols)):
                d = max(abs(a - b) for a, b in zip(cols[i], cols[j]))
                if d < 14:
                    msgs.append(f'HATA yakın renk {cols[i]} / {cols[j]} (fark {d} < 14)')
        for col, n in cnt.items():
            if n < 10:
                msgs.append(f'UYARI {col} yalnızca {n} piksel (10+ olmalı ya da başka renge kat)')
        if not 6 <= len(cnt) <= 11:
            msgs.append(f'UYARI renk sayısı {len(cnt)} (önerilen 6–11)')
        return '\n'.join(msgs)


# ------------------------------------------------------------------ önizleme

def _font(n):
    for p in ('/usr/share/fonts/TTF/DejaVuSans-Bold.ttf', '/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf'):
        if os.path.exists(p):
            return ImageFont.truetype(p, n)
    return ImageFont.load_default()


def _tile(path, cell, scale):
    bg = Image.new('RGB', (cell, cell), (44, 46, 56))
    d = ImageDraw.Draw(bg)
    for y in range(0, cell, scale * 2):
        for x in range(0, cell, scale * 2):
            if (x // (scale * 2) + y // (scale * 2)) % 2:
                d.rectangle([x, y, x + scale * 2 - 1, y + scale * 2 - 1], fill=(52, 54, 66))
    im = Image.open(path).convert('RGBA').resize((cell, cell), Image.NEAREST)
    bg.paste(im, (0, 0), im)
    return bg


def sheet(items, out_path, scale=8, cols=5):
    """Toplu önizleme: items = [(başlık, png yolu), ...]. Kullanıcıya göstermek için (projeye koyma)."""
    f = _font(15)
    cell, pad, th = 32 * scale, 18, 40
    rows = (len(items) + cols - 1) // cols
    out = Image.new('RGB', (cols * (cell + pad) + pad, rows * (cell + th + pad) + pad), (28, 30, 38))
    d = ImageDraw.Draw(out)
    for i, (title, p) in enumerate(items):
        r, c = divmod(i, cols)
        x, y = pad + c * (cell + pad), pad + r * (cell + th + pad)
        d.text((x, y), title, fill=(235, 235, 240), font=f)
        out.paste(_tile(p, cell, scale), (x, y + th - 18))
    out.save(out_path)
    return out_path


def before_after(pairs, out_path, scale=7, cols=3):
    """Önce/sonra sayfası: pairs = [(başlık, eski png, yeni png), ...]."""
    f, fs = _font(18), _font(14)
    cell, gap, pad = 32 * scale, 10, 26
    pw = cell * 2 + gap
    rows = (len(pairs) + cols - 1) // cols
    out = Image.new('RGB', (pad + cols * (pw + pad), 30 + rows * (cell + 90) + pad), (24, 26, 33))
    d = ImageDraw.Draw(out)
    for i, (title, old, new) in enumerate(pairs):
        r, c = divmod(i, cols)
        x, y = pad + c * (pw + pad), 20 + r * (cell + 90)
        d.text((x, y), title, fill=(235, 235, 240), font=f)
        for j, (lab, p, col) in enumerate((('ÖNCE', old, (232, 96, 96)), ('SONRA', new, (96, 214, 128)))):
            xx = x + j * (cell + gap)
            d.text((xx, y + 26), lab, fill=col, font=fs)
            out.paste(_tile(p, cell, scale), (xx, y + 46))
            d.rectangle([xx - 1, y + 45, xx + cell, y + 46 + cell], outline=col, width=2)
    out.save(out_path)
    return out_path


if __name__ == '__main__':
    # python3 pixelkit.py resim.png [resim2.png ...] → her resim için rapor
    for p in sys.argv[1:]:
        im = Image.open(p).convert('RGBA')
        c = Canvas()
        for y in range(min(H, im.height)):
            for x in range(min(W, im.width)):
                r, g, b, a = im.getpixel((x, y))
                if a:
                    c.p[y][x] = (r, g, b)
        print(os.path.basename(p) + ':', c.report().replace('\n', '\n  '))
