"""Düz (3–4 renkli) pixel art'ı Orta/Zor level için zenginleştirir: kilit oluşturan katmanlar ekler.

  python3 enrich.py <girdi.png> <çıktı.png> [seçenekler]
    --outline            figürün kenarına koyu outline (figür rengiyle aynı tonda OLMAYAN koyu mor-siyah)
    --shade              her rengin sağ-alt kenarına daha koyu ama AYRI SEÇİLEN bir ton (iç kilit)
    --highlight          sol-üst kenarlara açık parlama tonu
    --ring               en büyük renk bölgesinin içine 1 piksellik halka (iç çekirdeği kilitler)
    --bg sky|night|sea|checker|sunset   boşlukları çok tonlu arka planla doldurur (1024 küp = "büyük" level)
    --frame              boşlukları doldurmadan resmin çevresine 2 piksellik çerçeve
    --preview önizleme.png   önce/sonra yan yana görsel (8x büyütülmüş)

Kurallar (otomatik uygulanır): iki renk kanal başına 14'ten (0–255) yakın olmaz, 10 pikselden az renk kalmaz
(en yakın renge katılır), toplam renk 11'i geçmez. Sonucu MUTLAKA kullanıcıya önce/sonra göstererek onaylat.
"""
import argparse, colorsys, itertools, collections
from PIL import Image, ImageDraw

OUT = (34, 26, 40)
BGS = {
    'sky': [(92, 164, 232), (168, 214, 246)],
    'night': [(28, 36, 92), (58, 82, 160)],
    'sea': [(36, 104, 170), (70, 160, 200)],
    'sunset': [(250, 170, 110), (240, 110, 120)],
    'checker': [(152, 216, 200), (252, 214, 222)],
}


def far(c, others, d=14):
    return all(any(abs(c[k] - o[k]) >= d for k in range(3)) for o in others)


def tone(c, dl):
    h, l, s = colorsys.rgb_to_hls(*[v / 255 for v in c])
    l = min(0.95, max(0.08, l + dl))
    return tuple(round(v * 255) for v in colorsys.hls_to_rgb(h, l, s))


def pick_tone(c, dl, used):
    for k in range(6):
        t = tone(c, dl * (1 + 0.35 * k))
        if far(t, used):
            return t
    return None


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('src'); ap.add_argument('dst')
    for f in ('outline', 'shade', 'highlight', 'ring', 'frame'):
        ap.add_argument('--' + f, action='store_true')
    ap.add_argument('--bg', choices=list(BGS))
    ap.add_argument('--preview')
    a = ap.parse_args()
    im = Image.open(a.src).convert('RGBA')
    W, H = im.size
    g = [[im.getpixel((x, y))[:3] if im.getpixel((x, y))[3] > 0 else None for x in range(W)] for y in range(H)]
    get = lambda x, y: g[y][x] if 0 <= x < W and 0 <= y < H else None
    fig = {(x, y) for y in range(H) for x in range(W) if g[y][x] is not None}
    used = {c for row in g for c in row if c}
    base = collections.Counter(c for row in g for c in row if c)

    if a.ring:
        main_c = base.most_common(1)[0][0]
        region = {(x, y) for (x, y) in fig if g[y][x] == main_c}
        inner = {p for p in region if all((p[0] + dx, p[1] + dy) in region for dx in (-2, -1, 0, 1, 2) for dy in (-2, -1, 0, 1, 2))}
        rc = pick_tone(main_c, -0.22, used)
        if rc and len(inner) > 12:
            for (x, y) in inner:
                if any((x + dx, y + dy) not in inner for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                    g[y][x] = rc
            used.add(rc)
    if a.shade or a.highlight:
        shades, lights = {}, {}
        for c in list(base):
            if a.shade:
                t = pick_tone(c, -0.16, used | set(shades.values()))
                if t: shades[c] = t
            if a.highlight:
                t = pick_tone(c, +0.16, used | set(lights.values()) | set(shades.values()))
                if t: lights[c] = t
        snap = [row[:] for row in g]
        # kenar = boşluğa değen figür pikseli; outline açıksa kenar outline olacağı için tonlar bir içeriye konur
        edge = {(x, y) for (x, y) in fig if any((x + dx, y + dy) not in fig for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))}
        rim = edge if not a.outline else {p for p in fig - edge if any((p[0] + dx, p[1] + dy) in edge
                                                                        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))}
        border = (lambda q: q not in fig) if not a.outline else (lambda q: q in edge)
        for (x, y) in rim:
            c = snap[y][x]
            if c in shades and (border((x + 1, y)) or border((x, y + 1))):
                g[y][x] = shades[c]
            elif c in lights and (border((x - 1, y)) or border((x, y - 1))):
                g[y][x] = lights[c]
        # gölge 2 piksel kalın olsun (10 pikselin altında kalıp silinmesin)
        for (x, y) in list(rim):
            c = snap[y][x]
            if c in shades and g[y][x] == shades[c]:
                for nx, ny in ((x - 1, y), (x, y - 1)):
                    if (nx, ny) in fig and (nx, ny) not in edge and snap[ny][nx] == c and g[ny][nx] == c:
                        g[ny][nx] = shades[c]
        used |= set(shades.values()) | set(lights.values())
    if a.outline:
        snap = [row[:] for row in g]
        for (x, y) in fig:
            if any(snap[y + dy][x + dx] is None if 0 <= x + dx < W and 0 <= y + dy < H else True
                   for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                g[y][x] = OUT
        used.add(OUT)
    if a.frame:
        for y in range(H):
            for x in range(W):
                if (x < 2 or y < 2 or x >= W - 2 or y >= H - 2) and g[y][x] is None:
                    g[y][x] = OUT
    if a.bg:
        b1, b2 = BGS[a.bg]
        for y in range(H):
            for x in range(W):
                if g[y][x] is None:
                    if a.bg == 'checker':
                        g[y][x] = b1 if ((x // 4) + (y // 4)) % 2 == 0 else b2
                    else:
                        g[y][x] = b1 if y < H * 0.45 + (2 if x % 6 < 3 else 0) else b2

    # temizlik: <10 px renkleri en yakına kat, en fazla 11 renk, yakın tonları birleştir
    def merge_small():
        cnt = collections.Counter(c for row in g for c in row if c)
        order = sorted(cnt, key=lambda c: cnt[c])
        for c in order:
            if cnt[c] < 10 or len(cnt) > 11:
                tgt = min((o for o in cnt if o != c), key=lambda o: sum((o[k] - c[k]) ** 2 for k in range(3)))
                for row in g:
                    for i, v in enumerate(row):
                        if v == c: row[i] = tgt
                return True
        for c1, c2 in itertools.combinations(cnt, 2):
            if not far(c1, [c2]):
                lo, hi = sorted((c1, c2), key=lambda c: cnt[c])
                for row in g:
                    for i, v in enumerate(row):
                        if v == lo: row[i] = hi
                return True
        return False
    while merge_small():
        pass

    out = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    for y in range(H):
        for x in range(W):
            if g[y][x]: out.putpixel((x, y), g[y][x] + (255,))
    out.save(a.dst)
    cnt = collections.Counter(c for row in g for c in row if c)
    print(f'{a.dst}: {sum(cnt.values())} küp, {len(cnt)} renk (önce {sum(base.values())} küp, {len(base)} renk)')
    if a.preview:
        s = Image.new('RGBA', (2 * W * 8 + 30, H * 8 + 20), (60, 60, 60, 255))
        for i, img in enumerate((im, out)):
            big = img.resize((W * 8, H * 8), Image.NEAREST)
            s.paste(big, (10 + i * (W * 8 + 10), 10), big)
        s.save(a.preview)


if __name__ == '__main__':
    main()
