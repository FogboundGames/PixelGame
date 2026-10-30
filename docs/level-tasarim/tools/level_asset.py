"""PixelGame level asset aracı (PixelLevelData .asset YAML dosyaları).

Komutlar (proje kökünden çalıştır: python3 docs/level-tasarim/tools/level_asset.py <komut> ...):

  new      --png Assets/32x32/X.png --name X --title "Görünen Ad" --index N
           Şablondan (Level_40_Saturn) yeni Assets/Levels/Level_<N>_<X>.asset + .meta oluşturur, PNG'ye .meta verir
           (PNG zaten .meta'lıysa dokunmaz). LevelSequence'e EKLEMEZ — sıra Unity içinden ayarlanır (REHBER §9).
  apply    --asset Assets/Levels/Level_..asset --design out.json [--cols 4] [--tier K|O|Z]
           design.py çıktısını asset'e yazar: palet, gemi sırası, sütun/satır, zorluk (elle sabit), kapasite alanları.
  mystery  --asset ... [--frac 0.05] [--seed 1] [--clear]
           Gizli küpleri (?) iç bölgelerde renk sınırlarına 2–3 küçük küme olarak yerleştirir.
  check    --asset ...            Bütün kuralları denetler (renk, sayı, halat, açılış, gizli). Hata varsa çıkış kodu 1.
  export   --asset ... --out x.txt   Simülatör girdisi üretir (./sim x.txt 300 1 1).
  art      --png ...              Sadece resim denetimi (renk sayısı, küçük renk, yakın tonlar, küp sayısı).
"""
import argparse, collections, glob, itertools, json, os, random, re, sys, uuid
from PIL import Image

ROOT = os.path.abspath(os.environ.get('PIXELGAME_ROOT', os.path.join(os.path.dirname(__file__), '..', '..', '..')))
TEMPLATE = 'Assets/Levels/Level_40_Saturn.asset'
TEX_META_TEMPLATE = 'Assets/32x32/Saturn.png.meta'
TIER = {'K': 0, 'O': 1, 'Z': 2, 'S': 2}
GAME_TOL = 0.04          # oyunun renk eşleştirme toleransı (kanal başına, 0–1)
NEAR = 14                # 0–255: bu kadar yakın iki renk oyuncu için de ayırt edilemez


def P(rel):
    return rel if os.path.isabs(rel) else os.path.join(ROOT, rel)


def fnum(v):
    return str(v // 255) if v in (0, 255) else repr(round(v / 255.0, 8)).rstrip('0').rstrip('.')


def col(c):
    return '{r: %s, g: %s, b: %s, a: 1}' % (fnum(c[0]), fnum(c[1]), fnum(c[2]))


def yaml_title(t):
    # Unity YAML: çift tırnak içinde \xNN / \uNNNN kaçışlı
    out = ''
    for ch in t:
        o = ord(ch)
        out += ch if o < 128 else ('\\x%02X' % o if o < 256 else '\\u%04X' % o)
    return '"' + out + '"'


def png_pixels(png):
    im = Image.open(P(png)).convert('RGBA')
    return im.size, list(im.get_flattened_data() if hasattr(im, 'get_flattened_data') else im.getdata())


def guid_of(path):
    return re.search(r'guid: (\w+)', open(path + '.meta').read()).group(1)


def png_for_guid(g):
    for m in glob.glob(P('Assets/**/*.png.meta'), recursive=True):
        if 'guid: ' + g in open(m, errors='ignore').read(400):
            return m[:-5]
    raise SystemExit('texture guid bulunamadı: ' + g)


def parse_asset(path):
    a = open(P(path)).read()
    pal = [tuple(round(float(v) * 255) for v in m.groups()) for m in
           re.finditer(r'- originalColor: \{r: ([\d.e-]+), g: ([\d.e-]+), b: ([\d.e-]+)', a)]
    ws = [dict(cap=int(m.group(1)), c=int(m.group(2)), link=int(m.group(3)), hid=int(m.group(4))) for m in
          re.finditer(r'capacity: (\d+)\n    paletteIndex: (\d+)\n    label: [^\n]*\n    linkId: (\d+)\n    isHidden: (\d)', a)]
    tex = re.search(r'm_LevelTexture: \{fileID: \d+, guid: (\w+)', a).group(1)
    cols = int(re.search(r'm_PoolColumns: (\d+)', a).group(1))
    rows = int(re.search(r'm_PoolRows: (\d+)', a).group(1))
    myst = [tuple(map(int, m.groups())) for m in re.finditer(r'^  - \{x: (-?\d+), y: (-?\d+)\}$', a, re.M)]
    return a, pal, ws, tex, cols, rows, myst


# ------------------------------------------------------------------ new
def cmd_new(args):
    png = args.png
    if not os.path.exists(P(png) + '.meta'):
        meta = re.sub(r'guid: \w+', 'guid: ' + uuid.uuid4().hex, open(P(TEX_META_TEMPLATE)).read(), count=1)
        open(P(png) + '.meta', 'w').write(meta)
        print('PNG .meta yazıldı (Saturn ayarları: Read/Write açık, Point filtre, sıkıştırma yok)')
    tex_guid = guid_of(P(png))
    out = P(f'Assets/Levels/Level_{args.index}_{args.name}.asset')
    if os.path.exists(out):
        raise SystemExit('zaten var: ' + out)
    a = open(P(TEMPLATE)).read()
    a = re.sub(r'm_Name: .*', f'm_Name: Level_{args.index}_{args.name}', a, count=1)
    a = re.sub(r'm_LevelName: .*', lambda m: 'm_LevelName: ' + yaml_title(args.title), a, count=1)
    a = re.sub(r'm_LevelIndex: \d+', f'm_LevelIndex: {args.index}', a, count=1)
    a = re.sub(r'(m_LevelTexture: \{fileID: 2800000, guid: )\w+', r'\g<1>' + tex_guid, a)
    a = re.sub(r'(m_OriginalSourceTexture: \{fileID: 2800000, guid: )\w+', r'\g<1>' + tex_guid, a)
    a = re.sub(r'm_MysteryCubeCoordinates:(?: \[\]\n|\n(?:  - \{x: -?\d+, y: -?\d+\}\n)*)', 'm_MysteryCubeCoordinates: []\n', a, count=1)
    a = re.sub(r'  m_WagonSequence:\n.*?(?=  m_FigureShadowTexture:)', '  m_WagonSequence: []\n', a, flags=re.S)
    open(out, 'w').write(a)
    meta = re.sub(r'guid: \w+', 'guid: ' + uuid.uuid4().hex, open(P(TEMPLATE) + '.meta').read(), count=1)
    open(out + '.meta', 'w').write(meta)
    print('oluşturuldu:', out, '→ şimdi design.py + apply')


# ------------------------------------------------------------------ apply
def cmd_apply(args):
    d = json.load(open(args.design))
    path = P(args.asset)
    a = open(path).read()
    pal = ''.join(f'  - originalColor: {col(c)}\n    targetColor: {col(c)}\n    pixelCount: {n}\n    label: \n'
                  for c, n in zip(d['pal'], d['cnt']))
    wag = ''.join(f'  - wagonColor: {col(d["pal"][s["c"]])}\n    capacity: {s["cap"]}\n    paletteIndex: {s["c"]}\n'
                  f'    label: Vagon\n    linkId: {s["link"]}\n    isHidden: {s["hid"]}\n' for s in d['seq'])
    a = re.sub(r'  m_ColorPalette:\n.*?(?=  m_TintColor:)', lambda m: '  m_ColorPalette:\n' + pal, a, flags=re.S)
    a = re.sub(r'  m_WagonSequence:(?: \[\])?\n.*?(?=  m_FigureShadowTexture:)', lambda m: '  m_WagonSequence:\n' + wag, a, flags=re.S)
    cols = args.cols or d.get('cols', 4)
    tier = args.tier or {'S': 'Z'}.get(d['tier'], d['tier'])
    big = sum(d['cnt']) >= 900
    for k, v in [('m_PoolColumns', cols), ('m_PoolRows', 3), ('m_DifficultyTier', TIER[tier]), ('m_DifficultyManual', 1),
                 ('m_UseCustomWagonSequence', 1), ('m_TruckCapacity', 40 if big else 30), ('m_UseNativeResolution', 1)]:
        a = re.sub(rf'{k}: \d+', f'{k}: {v}', a, count=1)
    open(path, 'w').write(a)
    print(f'yazıldı: {args.asset}  gemi {len(d["seq"])}  sütun {cols}  zorluk {tier}')


# ------------------------------------------------------------------ mystery
def layers(occ, W, H):
    rem, lay, L = set(occ), {}, 0
    while rem:
        air, st = {(-1, -1)}, [(-1, -1)]
        while st:
            x, y = st.pop()
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                n = (x + dx, y + dy)
                if -1 <= n[0] <= W and -1 <= n[1] <= H and n not in air and n not in rem:
                    air.add(n); st.append(n)
        shell = [c for c in rem if any((c[0] + dx, c[1] + dy) in air for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))]
        if not shell:
            for c in rem: lay[c] = L
            break
        for c in shell:
            lay[c] = L; rem.discard(c)
        L += 1
    return lay


def cmd_mystery(args):
    path = P(args.asset)
    a, pal, ws, tex, cols, rows, _ = parse_asset(path)
    if args.clear:
        coords, mcol = [], (32, 30, 78)
    else:
        (W, H), px = png_pixels(png_for_guid(tex))
        occ = {(x, H - 1 - y): px[y * W + x][:3] for y in range(H) for x in range(W) if px[y * W + x][3] > 0}
        lay = layers(occ, W, H)
        target = round(len(occ) * args.frac)
        rnd = random.Random(args.seed)

        def mixed(c):
            s = {occ.get((c[0] + dx, c[1] + dy)) for dx in (-1, 0, 1) for dy in (-1, 0, 1)}
            s.discard(None)
            return len(s) >= 2
        cands = [c for c in occ if lay[c] >= 3 and mixed(c)]
        chosen, centers = set(), []
        while len(chosen) < target and cands:
            c = rnd.choice(cands)
            if any(abs(c[0] - o[0]) + abs(c[1] - o[1]) < 8 for o in centers):
                cands.remove(c); continue
            centers.append(c)
            for d in sorted((d for d in occ if lay[d] >= 2 and abs(d[0] - c[0]) + abs(d[1] - c[1]) <= 3),
                            key=lambda d: abs(d[0] - c[0]) + abs(d[1] - c[1])):
                if len(chosen) >= target: break
                chosen.add(d)
        coords = sorted(chosen)
        cands_col = [(106, 90, 122), (128, 128, 140), (96, 96, 104), (150, 140, 160), (120, 110, 100)]
        palset = set(occ.values())
        mcol = max(cands_col, key=lambda m: min(max(abs(m[i] - p[i]) for i in range(3)) for p in palset))
    lst = 'm_MysteryCubeCoordinates:' + (' []\n' if not coords else '\n' + ''.join(f'  - {{x: {x}, y: {y}}}\n' for x, y in coords))
    a = re.sub(r'm_MysteryCubeCoordinates:(?: \[\]\n|\n(?:  - \{x: -?\d+, y: -?\d+\}\n)*)', lambda m: lst, a, count=1)
    a = re.sub(r'm_MysteryRevealCondition: \d', 'm_MysteryRevealCondition: 0', a)
    a = re.sub(r'm_MysteryCubeColor: \{[^}]*\}', lambda m: 'm_MysteryCubeColor: {r: %s, g: %s, b: %s, a: 1}' % tuple(
        round(v / 255, 6) for v in mcol), a)
    open(path, 'w').write(a)
    print(f'gizli küp: {len(coords)}  renk {mcol}')


# ------------------------------------------------------------------ checks
def art_report(png):
    (W, H), px = png_pixels(png)
    cnt = collections.Counter(p[:3] for p in px if p[3] > 0)
    n = sum(cnt.values())
    errs, warns = [], []
    for a_, b_ in itertools.combinations(cnt, 2):
        if all(abs(a_[k] - b_[k]) / 255 < GAME_TOL for k in range(3)):
            errs.append(f'oyun bu iki rengi AYNI sayar (tolerans 0.04): {a_} {b_}')
        elif all(abs(a_[k] - b_[k]) < NEAR for k in range(3)):
            warns.append(f'çok yakın tonlar, oyuncu ayırt edemez: {a_} {b_}')
    small = [(c, v) for c, v in cnt.items() if v < 10]
    if small:
        warns.append(f'10 pikselden az renk (birleştir ya da büyüt): {small}')
    semi = sum(1 for p in px if 0 < p[3] < 255)
    if semi:
        errs.append(f'{semi} yarı saydam piksel var (alfa 0 ya da 255 olmalı)')
    if not 6 <= len(cnt) <= 11:
        warns.append(f'renk sayısı {len(cnt)} (önerilen 6–11)')
    return dict(size=(W, H), cubes=n, colors=len(cnt), fill=round(n / (W * H), 2), big=n >= 900), errs, warns


def cmd_art(args):
    info, errs, warns = art_report(args.png)
    print(info)
    for e in errs: print('HATA  ', e)
    for w in warns: print('UYARI ', w)
    sys.exit(1 if errs else 0)


def cmd_check(args):
    a, pal, ws, tex, cols, rows, myst = parse_asset(args.asset)
    png = png_for_guid(tex)
    info, errs, warns = art_report(png)
    (W, H), px = png_pixels(png)
    # sayı eşleşmesi oyunun toleransıyla
    caps = collections.Counter()
    for w in ws: caps[w['c']] += w['cap']
    for k, cap in caps.items():
        c = pal[k]
        cubes = sum(1 for p in px if p[3] > 0 and all(abs(p[i] - c[i]) / 255 < GAME_TOL for i in range(3)))
        if cubes != cap:
            errs.append(f'renk {c}: küp {cubes} ≠ gemi kapasitesi {cap}')
    if sum(caps.values()) != info['cubes']:
        errs.append(f'toplam kapasite {sum(caps.values())} ≠ küp {info["cubes"]}')
    if not ws:
        errs.append('gemi sırası boş')
    if 'm_UseCustomWagonSequence: 1' not in a:
        errs.append('m_UseCustomWagonSequence 1 olmalı')
    if 'm_DifficultyManual: 1' not in a:
        warns.append('m_DifficultyManual 0: Unity zorluğu kendi analiziyle değiştirir')
    maxc = 40 if info['big'] else 30
    for i, w in enumerate(ws):
        if w['cap'] > maxc: errs.append(f'gemi {i}: kapasite {w["cap"]} > {maxc}')
    odd = collections.Counter(w['c'] for w in ws if w['cap'] % 10)
    for k, v in odd.items():
        if v > 1: warns.append(f'renk {pal[k]}: {v} küsuratlı gemi (en fazla 1 önerilir)')
    for i in range(len(ws) - cols):
        if ws[i]['c'] == ws[i + cols]['c']:
            errs.append(f'aynı sütunda aynı renk arka arkaya: sıra {i} ve {i + cols}')
    groups = collections.defaultdict(list)
    for i, w in enumerate(ws):
        if w['link']: groups[w['link']].append(i)
    for lid, m in groups.items():
        if len(m) != 2:
            errs.append(f'halat {lid}: {len(m)} gemi (sadece ikili)'); continue
        i, j = m
        side = j == i + 1 and i % cols != cols - 1
        stack = j == i + cols and j < cols * rows
        if not (side or stack):
            errs.append(f'halat {lid}: sıra {i}-{j} geçersiz (yan yana komşu sütun ya da başlangıç havuzunda alt alta olmalı)')
        if ws[i]['c'] == ws[j]['c']:
            errs.append(f'halat {lid}: iki gemi aynı renk')
    for i, w in enumerate(ws):
        if w['hid'] and i < cols: errs.append(f'gizli gemi ön sırada (sıra {i}) — gemiye yanaşana kadar açılmaz')
        if w['hid'] and w['link']: errs.append(f'gizli gemi halatlı (sıra {i})')
    first = {lid: m[0] for lid, m in groups.items()}
    pool_links = sum(1 for v in first.values() if v < cols * 3)
    front = sum(1 for i in range(min(cols, len(ws))) if ws[i]['link'])
    if pool_links > 2: warns.append(f'açılışta (ilk 3 sıra) {pool_links} halat — en fazla 2')
    if front > cols // 2: warns.append(f'ön sıranın {front}/{cols} gemisi halatlı — en fazla yarısı')
    occ = {(x, H - 1 - y) for y in range(H) for x in range(W) if px[y * W + x][3] > 0}
    for c in myst:
        if c not in occ: errs.append(f'gizli küp boş hücrede: {c}')
    print(f'{os.path.basename(args.asset)}: {info["cubes"]} küp ({"büyük" if info["big"] else "küçük"}), {info["colors"]} renk, '
          f'{len(ws)} gemi, {cols} sütun, halat {len(groups)}, gizli gemi {sum(w["hid"] for w in ws)}, gizli küp {len(myst)}')
    for e in errs: print('HATA  ', e)
    for w in warns: print('UYARI ', w)
    sys.exit(1 if errs else 0)


def cmd_export(args):
    a, pal, ws, tex, cols, rows, _ = parse_asset(args.asset)
    (W, H), px = png_pixels(png_for_guid(tex))
    near = lambda p: min(range(len(pal)), key=lambda i: sum((pal[i][k] - p[k]) ** 2 for k in range(3)))
    with open(args.out, 'w') as f:
        f.write(f'{W} {H}\n')
        for y in range(H):
            f.write(' '.join(str(near(p)) if p[3] else '-1' for p in px[y * W:(y + 1) * W]) + '\n')
        f.write(f'{cols} {min(rows, 3)} 5\n{len(ws)}\n')
        for w in ws: f.write(f'{w["c"]} {w["cap"]} {w["link"]} {w["hid"]}\n')
    print('yazıldı', args.out)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest='cmd', required=True)
    s = sub.add_parser('new'); s.add_argument('--png', required=True); s.add_argument('--name', required=True)
    s.add_argument('--title', required=True); s.add_argument('--index', type=int, required=True)
    s = sub.add_parser('apply'); s.add_argument('--asset', required=True); s.add_argument('--design', required=True)
    s.add_argument('--cols', type=int); s.add_argument('--tier', choices=['K', 'O', 'Z'])
    s = sub.add_parser('mystery'); s.add_argument('--asset', required=True); s.add_argument('--frac', type=float, default=0.05)
    s.add_argument('--seed', type=int, default=1); s.add_argument('--clear', action='store_true')
    s = sub.add_parser('check'); s.add_argument('--asset', required=True)
    s = sub.add_parser('export'); s.add_argument('--asset', required=True); s.add_argument('--out', required=True)
    s = sub.add_parser('art'); s.add_argument('--png', required=True)
    args = ap.parse_args()
    globals()['cmd_' + args.cmd](args)


if __name__ == '__main__':
    main()
