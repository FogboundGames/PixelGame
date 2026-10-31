"""Kuyruk tasarlayıcı: renkleri gemilere böler, sonra sıra/halat/gizli'yi hedef zorluk bandına göre arar.

kullanım:  COLS=4 ROWS=3 ITERS=400 python3 design.py <resim.png> <K|O|S|Z> <çıktı.json> [tohum]
  K = Kolay, O = Orta, S = Zor (adil, önerilen), Z = Zor (eski, sert — kullanma)
çıktı.json: palette (piksel sayısına göre sıralı), gemi sırası, sim sonuçları. level_asset.py apply ile asset'e yazılır."""
import sys, json, random, subprocess, os, collections, tempfile
from PIL import Image

D = os.path.dirname(os.path.abspath(__file__))
SIM = D + '/sim'
COLS, ROWS = int(os.environ.get('COLS', 4)), int(os.environ.get('ROWS', 3))

BANDS = {  # (random lo, hi), (smart lo, hi)
    'K': ((0.55, 0.80), (0.98, 1.00)),  # 2026-10-31: 0.90+ "ekrana bakmadan geçiliyor" — çok kolaydı
    'O': ((0.30, 0.55), (0.90, 1.00)),
    'Z': ((0.05, 0.20), (0.30, 0.80)),
    'S': ((0.12, 0.30), (0.80, 0.95)),  # fair Zor: a careful player should win
}
LINKS = {'K': (1, 2), 'O': (4, 6), 'Z': (5, 7), 'S': (5, 6)}
HIDDEN = {'K': (1, 2), 'O': (4, 6), 'Z': (5, 7), 'S': (5, 6)}


def load(png):
    im = Image.open(png).convert('RGBA')
    W, H = im.size
    px = list(im.get_flattened_data())
    cnt = collections.Counter(p[:3] for p in px if p[3] > 0)
    pal = [c for c, _ in sorted(cnt.items(), key=lambda t: (-t[1], t[0]))]
    idx = {c: i for i, c in enumerate(pal)}
    g = [[idx[px[y * W + x][:3]] if px[y * W + x][3] > 0 else -1 for x in range(W)] for y in range(H)]
    return W, H, g, pal, [cnt[c] for c in pal]


# başta açıkta olan (dış katman) renkler: en öndeki gemiler bunlardan olursa level kendini oynar
OUTER = set()
FRONT_OUTER = int(os.environ.get('FRONT_OUTER', 1))   # ön sırada en fazla bu kadar dış-katman gemisi
OPEN_OUTER = int(os.environ.get('OPEN_OUTER', 3))     # ilk iki sırada en fazla bu kadar


def outer_colors(W, H, g, min_exposed=6):
    air = set(); st = [(-1, -1)]
    while st:
        x, y = st.pop()
        if (x, y) in air or not (-1 <= x <= W and -1 <= y <= H):
            continue
        if 0 <= x < W and 0 <= y < H and g[y][x] >= 0:
            continue
        air.add((x, y))
        st += [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
    exp = collections.Counter(g[y][x] for y in range(H) for x in range(W)
                              if g[y][x] >= 0 and any(q in air for q in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1))))
    return {c for c, n in exp.items() if n >= min_exposed}


def split(n, maxc):
    if n <= maxc:
        return [n]
    out = []
    r = n % 10
    if r:
        out.append(10 + r); n -= 10 + r
    while n > 0:
        c = min(maxc, n); out.append(c); n -= c
    return sorted(out, reverse=True)


def write_level(path, W, H, g, seq):
    with open(path, 'w') as f:
        f.write(f'{W} {H}\n')
        for r in g:
            f.write(' '.join(map(str, r)) + '\n')
        f.write(f'{COLS} {ROWS} 5\n{len(seq)}\n')
        for s in seq:
            f.write(f"{s['c']} {s['cap']} {s['link']} {s['hid']}\n")


def run(W, H, g, seq, runs, dfs, seed=1, noise=None):
    os.makedirs(D + '/tmp', exist_ok=True)
    fd, path = tempfile.mkstemp(suffix='.txt', dir=D + '/tmp')
    os.close(fd)
    write_level(path, W, H, g, seq)
    args = [SIM, path, str(runs), str(seed), '1' if dfs else '0'] + ([noise] if noise else [])
    out = subprocess.run(args, capture_output=True, text=True).stdout.split()
    os.remove(path)
    return float(out[0]), float(out[1]), int(out[2])


def valid_order(seq):
    n = len(seq)
    for i in range(n - COLS):
        if seq[i]['c'] == seq[i + COLS]['c']:
            return False
    lk = collections.defaultdict(set); cn = collections.Counter()
    for x in seq:
        if x['link']:
            lk[x['link']].add(x['c']); cn[x['link']] += 1
    if not all(len(lk[l]) == cn[l] for l in lk):
        return False
    # readable opening: at most 2 halat among the first 3 rows, at most half of the front row linked
    pool = COLS * ROWS
    first = {}
    for i, x in enumerate(seq):
        if x['link'] and x['link'] not in first:
            first[x['link']] = i
    if sum(1 for i in first.values() if i < pool) > 2:
        return False
    if sum(1 for i in range(min(COLS, n)) if seq[i]['link']) > COLS // 2:
        return False
    if sum(1 for i in range(min(COLS, n)) if seq[i]['c'] in OUTER) > FRONT_OUTER:
        return False
    if sum(1 for i in range(min(2 * COLS, n)) if seq[i]['c'] in OUTER) > OPEN_OUTER:
        return False
    return True


def pair_ok(i, j):
    return (j == i + 1 and i % COLS != COLS - 1) or (j == i + COLS and j < COLS * ROWS)


def norm_links(seq):
    ids = collections.defaultdict(list)
    for i, s in enumerate(seq):
        if s['link']:
            ids[s['link']].append(i)
    for s in seq:
        s['link'] = 0
    k = 0
    for lid, m in sorted(ids.items(), key=lambda t: t[1][0]):
        if len(m) == 2 and pair_ok(m[0], m[1]):
            k += 1
            for i in m:
                seq[i]['link'] = k
    for i, s in enumerate(seq):
        if s['link'] or i < COLS:
            s['hid'] = 0


def counts(seq):
    return len({s['link'] for s in seq if s['link']}), sum(s['hid'] for s in seq)


def penalty(r, sm, tier):
    (rl, rh), (sl, sh) = BANDS[tier]
    return max(0, rl - r) + max(0, r - rh) + max(0, sl - sm) * 1.5 + max(0, sm - sh)


def structure_ok(seq, tier):
    nl, nh = counts(seq)
    return LINKS[tier][0] <= nl <= LINKS[tier][1] and HIDDEN[tier][0] <= nh <= HIDDEN[tier][1] and valid_order(seq)


def mutate(seq, tier, rnd):
    s = [dict(x) for x in seq]
    n = len(s)
    op = rnd.random()
    if op < 0.6:
        for _ in range(50):
            i, j = rnd.randrange(n), rnd.randrange(n)
            if s[i]['c'] == s[j]['c']:
                continue
            s[i]['c'], s[j]['c'] = s[j]['c'], s[i]['c']
            s[i]['cap'], s[j]['cap'] = s[j]['cap'], s[i]['cap']
            if valid_order(s):
                break
            s[i]['c'], s[j]['c'] = s[j]['c'], s[i]['c']
            s[i]['cap'], s[j]['cap'] = s[j]['cap'], s[i]['cap']
    elif op < 0.8:
        nl, _ = counts(s)
        lo, hi = LINKS[tier]
        if nl > lo and (nl >= hi or rnd.random() < 0.5):
            lid = rnd.choice([x['link'] for x in s if x['link']])
            for x in s:
                if x['link'] == lid:
                    x['link'] = 0
        if counts(s)[0] < hi:
            for _ in range(50):
                i = rnd.randrange(n - 1)
                j = i + 1 if rnd.random() < 0.7 else i + COLS
                if j < n and pair_ok(i, j) and not s[i]['link'] and not s[j]['link'] and s[i]['c'] != s[j]['c']:
                    s[i]['link'] = s[j]['link'] = 99
                    s[i]['hid'] = s[j]['hid'] = 0
                    break
    else:
        _, nh = counts(s)
        lo, hi = HIDDEN[tier]
        if nh > lo and (nh >= hi or rnd.random() < 0.5):
            rnd.choice([x for x in s if x['hid']])['hid'] = 0
        if counts(s)[1] < hi:
            cands = [i for i in range(COLS, n) if not s[i]['link'] and not s[i]['hid']]
            if cands:
                s[rnd.choice(cands)]['hid'] = 1
    norm_links(s)
    return s


def initial(caps, tier, rnd):
    ships = [{'c': c, 'cap': cap, 'link': 0, 'hid': 0} for c, cl in caps.items() for cap in cl]
    for _ in range(3000):
        rnd.shuffle(ships)
        if valid_order(ships):
            break
    s = [dict(x) for x in ships]
    for _ in range(60):
        t = mutate(s, tier, rnd)
        if valid_order(t):
            s = t
        if structure_ok(s, tier):
            break
    return s


def main():
    png, tier, out = sys.argv[1], sys.argv[2], sys.argv[3]
    seed = int(sys.argv[4]) if len(sys.argv) > 4 else 1
    iters = int(os.environ.get('ITERS', 400))
    rnd = random.Random(seed)
    W, H, g, pal, cnt = load(png)
    OUTER.update(outer_colors(W, H, g))
    maxc = 40 if sum(cnt) >= 900 else 30
    caps = {i: split(n, maxc) for i, n in enumerate(cnt)}
    best = None
    for restart in range(3):
        s = initial(caps, tier, rnd)
        r, sm, _ = run(W, H, g, s, 60, False, seed)
        p = penalty(r, sm, tier) + (0 if structure_ok(s, tier) else 1)
        for it in range(iters):
            t = mutate(s, tier, rnd)
            if not valid_order(t):
                continue
            r2, sm2, _ = run(W, H, g, t, 60, False, seed + it)
            p2 = penalty(r2, sm2, tier) + (0 if structure_ok(t, tier) else 1)
            if p2 <= p:
                s, p, r, sm = t, p2, r2, sm2
            if p == 0 and it > 30:
                break
        r, sm, d = run(W, H, g, s, 300, True, 777)
        rm, smm, _ = run(W, H, g, s, 300, False, 778, 'm')
        pf = penalty(r, sm, tier) + (0 if structure_ok(s, tier) else 1) + (0 if d == 1 else 2) + max(0, sm - smm - 0.1)
        print(f'restart {restart}: random {r:.2f} smart {sm:.2f} dfs {d} tap {rm:.2f}/{smm:.2f} pen {pf:.3f}', flush=True)
        if best is None or pf < best[0]:
            best = (pf, s, r, sm, d, rm, smm)
        if pf == 0:
            break
    pf, s, r, sm, d, rm, smm = best
    json.dump({'png': png, 'tier': tier, 'cols': COLS, 'pal': pal, 'cnt': cnt, 'seq': s, 'random': r, 'smart': sm,
               'dfs': d, 'tap_random': rm, 'tap_smart': smm, 'pen': pf}, open(out, 'w'))
    nl, nh = counts(s)
    print(f'DONE {os.path.basename(png)} {tier} cols {COLS} ships {len(s)} links {nl} hidden {nh} '
          f'random {r:.2f} smart {sm:.2f} dfs {d} tap {rm:.2f}/{smm:.2f} pen {pf:.3f}')


if __name__ == '__main__':
    main()
