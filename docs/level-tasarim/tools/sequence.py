"""LevelSequence sıralama aracı.

  python3 sequence.py show [--from 1]          sırayı zorluk/büyüklük/sütun ile listeler, kural ihlallerini işaretler
  python3 sequence.py suggest --from 39        o noktadan sona kadar kurallara uyan bir sıra önerir (dosyaya YAZMAZ)

Sıra dosyaya elle yazılmaz: önerilen listeyi REHBER §9'daki Unity kodu ile uygula (yoksa Level Designer ezer).
Kurallar: iki Zor yan yana olmaz · 3 büyük (≥900 küp) art arda olmaz · iki küçük yan yana olmasın (yumuşak) ·
          Zor'dan sonra Kolay iyidir · m_LevelIndex = sıradaki konum, tekrar yok.
"""
import argparse, glob, math, os, random, re, sys
from PIL import Image

ROOT = os.path.abspath(os.environ.get('PIXELGAME_ROOT', os.path.join(os.path.dirname(__file__), '..', '..', '..')))


def load():
    g = {}
    for m in glob.glob(os.path.join(ROOT, 'Assets/**/*.meta'), recursive=True):
        mm = re.search(r'guid: (\w+)', open(m, errors='ignore').read(300))
        if mm: g[mm.group(1)] = m[:-5]
    seq = re.findall(r'guid: (\w+), type: 2', open(os.path.join(ROOT, 'Assets/Levels/LevelSequence.asset')).read())
    out = []
    for i, gu in enumerate(seq):
        p = g[gu]; a = open(p).read()
        tex = g[re.search(r'm_LevelTexture: \{fileID: \d+, guid: (\w+)', a).group(1)]
        im = Image.open(tex).convert('RGBA')
        px = im.get_flattened_data() if hasattr(im, 'get_flattened_data') else im.getdata()
        n = sum(1 for q in px if q[3] > 0)
        out.append(dict(pos=i + 1, file=os.path.basename(p)[:-6], idx=int(re.search(r'm_LevelIndex: (\d+)', a).group(1)),
                        tier='KOZ'[int(re.search(r'm_DifficultyTier: (\d)', a).group(1))], cubes=n, big=n >= 900,
                        cols=int(re.search(r'm_PoolColumns: (\d+)', a).group(1))))
    return out


def violations(lv, start=1):
    v = []
    for i in range(1, len(lv)):
        a, b = lv[i - 1], lv[i]
        if b['pos'] < start: continue
        if a['tier'] == 'Z' and b['tier'] == 'Z': v.append(f"{a['pos']}-{b['pos']}: iki Zor yan yana")
        if not a['big'] and not b['big']: v.append(f"{a['pos']}-{b['pos']}: iki küçük resim yan yana (yumuşak)")
        if i >= 2 and lv[i - 2]['big'] and a['big'] and b['big']: v.append(f"{lv[i-2]['pos']}-{b['pos']}: 3 büyük resim art arda")
    seen = {}
    for l in lv:
        if l['idx'] != l['pos']: v.append(f"{l['pos']} {l['file']}: m_LevelIndex {l['idx']} ≠ konum")
        if l['idx'] in seen: v.append(f"m_LevelIndex {l['idx']} tekrar: {seen[l['idx']]} ve {l['file']}")
        seen[l['idx']] = l['file']
    return v


def cmd_show(args):
    lv = load()
    for l in lv:
        if l['pos'] >= args.start:
            print(f"{l['pos']:>3} {l['file']:<32} {l['tier']} {l['cubes']:>5} {'BÜYÜK' if l['big'] else 'küçük'} {l['cols']} sütun")
    for x in violations(lv, args.start): print('  !', x)


def cost(order, prev):
    s = prev + order; c = 0
    for i in range(1, len(s)):
        if s[i]['tier'] == 'Z' and s[i - 1]['tier'] == 'Z': c += 1000
        if i >= 2 and s[i]['big'] and s[i - 1]['big'] and s[i - 2]['big']: c += 1000
        if not s[i]['big'] and not s[i - 1]['big']: c += 50
        if s[i]['big'] and s[i - 1]['big']: c += 10
        if s[i]['tier'] == 'K' and s[i - 1]['tier'] == 'Z': c -= 5
    # mevcut sıradan çok uzaklaşma (eski levelleri çok kaydırma)
    c += sum(abs(i - o['_orig']) for i, o in enumerate(order)) * 0.5
    return c


def cmd_suggest(args):
    lv = load()
    k = args.start - 1
    prev, rest = lv[max(0, k - 2):k], lv[k:]
    for i, o in enumerate(rest): o['_orig'] = i
    rnd, best = random.Random(1), None
    for _ in range(40):
        o = rest[:]; rnd.shuffle(o); cur = cost(o, prev); T = 50.0
        for _ in range(40000):
            i, j = rnd.randrange(len(o)), rnd.randrange(len(o))
            o[i], o[j] = o[j], o[i]; nc = cost(o, prev)
            if nc <= cur or rnd.random() < math.exp((cur - nc) / T): cur = nc
            else: o[i], o[j] = o[j], o[i]
            T = max(0.05, T * 0.9997)
        if best is None or cur < best[0]: best = (cur, o[:])
    print('önerilen sıra (Unity koduna verilecek dosya adları):')
    for i, l in enumerate(best[1]):
        print(f"{args.start + i:>3} {l['file']:<32} {l['tier']} {'BÜYÜK' if l['big'] else 'küçük'}")
    print('C# dizi:', '{' + ','.join('"%s"' % l['file'] for l in best[1]) + '}')


ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
sub = ap.add_subparsers(dest='cmd', required=True)
s = sub.add_parser('show'); s.add_argument('--from', dest='start', type=int, default=1)
s = sub.add_parser('suggest'); s.add_argument('--from', dest='start', type=int, required=True)
args = ap.parse_args()
globals()['cmd_' + args.cmd](args)
