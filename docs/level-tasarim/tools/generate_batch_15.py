#!/usr/bin/env python3
"""Batch level generator for 15 new levels:
3 Kolay, 8 Orta (tightened challenge), 4 Zor.
Includes dual-color (Süper/Güçlü) container support.
"""
import os, sys, json, random, re
from PIL import Image

TOOL_DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, TOOL_DIR)
import design, level_asset

LEVELS_15 = [
    # (file_num, name, title, png_path, tier, cols, dual_spec, mystery_frac, seq_pos)
    (124, "AntikMisir", "Antik Mısır", "Assets/04.11.2026/zor/AntikMisir.png", "S", 4, 2, 0.05, 123),
    (125, "Hamburger", "Hamburger", "Assets/04.11.2026/kolay/Hamburger.png", "K", 3, 0, 0.0, 124),
    (126, "Bukalemun", "Bukalemun", "Assets/04.11.2026/orta/Bukalemun.png", "O", 4, 0, 0.05, 125),
    (127, "Daktilo", "Daktilo", "Assets/04.11.2026/orta/Daktilo.png", "O", 4, (0, 1, 20, 20), 0.05, 126),
    (128, "KanyonGunesi", "Kanyon Güneşi", "Assets/04.11.2026/zor/KanyonGunesi.png", "S", 4, 2, 0.05, 127),
    (129, "DalgicMaskesi", "Dalgıç Maskesi", "Assets/04.11.2026/kolay/DalgicMaskesi.png", "K", 3, 0, 0.0, 128),
    (130, "DalisKaski", "Dalış Kaskı", "Assets/04.11.2026/orta/DalisKaski.png", "O", 4, 0, 0.05, 129),
    (131, "Kaset", "Kaset", "Assets/04.11.2026/orta/Kaset.png", "O", 4, [(0, 1, 20, 20)], 0.05, 130),
    (132, "KorsanAdasi", "Korsan Adası", "Assets/04.11.2026/zor/KorsanAdasi.png", "S", 4, [(0, 1, 40, 40), (2, 3, 30, 30)], 0.05, 131),
    (133, "Keman", "Keman", "Assets/04.11.2026/orta/Keman.png", "O", 4, None, 0.05, 132),
    (134, "Mikroskop", "Mikroskop", "Assets/04.11.2026/orta/Mikroskop.png", "O", 4, None, 0.05, 133),
    (135, "RubikKup", "Rubik Küp", "Assets/04.11.2026/kolay/RubikKup.png", "K", 3, None, 0.0, 134),
    (136, "MercanResifi", "Mercan Resifi", "Assets/04.11.2026/zor/MercanResifi.png", "S", 4, [(0, 1, 40, 40), (2, 3, 40, 40)], 0.05, 135),
    (137, "UfoGemi", "UFO", "Assets/04.11.2026/orta/UfoGemi.png", "O", 4, [(0, 1, 20, 20)], 0.05, 136),
    (138, "YelkenliGemi", "Yelkenli Gemi", "Assets/04.11.2026/orta/YelkenliGemi.png", "O", 4, None, 0.05, 137),
]

def smart_initial_order(ships, rnd, max_tries=15000):
    COLS = design.COLS
    n = len(ships)
    for _ in range(max_tries):
        outer = [s for s in ships if design.is_outer(s)]
        inner = [s for s in ships if not design.is_outer(s)]
        rnd.shuffle(outer)
        rnd.shuffle(inner)
        if len(inner) < 5:
            cand = list(ships)
            rnd.shuffle(cand)
            if design.valid_order(cand): return cand
            continue
        r1 = inner[:3] + outer[:1]
        rnd.shuffle(r1)
        r2 = inner[3:5] + outer[1:3]
        rnd.shuffle(r2)
        rest = inner[5:] + outer[3:]
        rnd.shuffle(rest)
        cand = r1 + r2 + rest
        if design.valid_order(cand): return cand
    return None

def optimize_level(png, tier, cols, dual_spec, out_json, seed=42):
    W, H, g, pal, cnt = design.load(png)
    design.COLS = cols
    design.OUTER = set()
    design.OUTER.update(design.outer_colors(W, H, g))

    maxc = 50 if tier in 'ZS' else 30
    rem_cnt = list(cnt)
    dual_ships = []

    if isinstance(dual_spec, list):
        for c1, c2, cap1, cap2 in dual_spec:
            dual_ships.append({'c': c1, 'cap': cap1, 'link': 0, 'hid': 0, 'c2': c2, 'cap2': cap2})
            rem_cnt[c1] -= cap1
            rem_cnt[c2] -= cap2
    elif isinstance(dual_spec, tuple):
        c1, c2, cap1, cap2 = dual_spec
        dual_ships.append({'c': c1, 'cap': cap1, 'link': 0, 'hid': 0, 'c2': c2, 'cap2': cap2})
        rem_cnt[c1] -= cap1
        rem_cnt[c2] -= cap2
    elif isinstance(dual_spec, int) and dual_spec > 0:
        cands = [i for i, n in enumerate(cnt) if n >= 50]
        used = set()
        for _ in range(dual_spec):
            pair = None
            for i in range(len(cands)):
                for j in range(i + 1, len(cands)):
                    c1, c2 = cands[i], cands[j]
                    if (c1, c2) not in used and rem_cnt[c1] >= 30 and rem_cnt[c2] >= 30:
                        pair = (c1, c2)
                        break
                if pair: break
            if pair:
                c1, c2 = pair
                used.add((c1, c2))
                cap1 = 40 if rem_cnt[c1] >= 80 else 30
                cap2 = 40 if rem_cnt[c2] >= 80 else 30
                rem_cnt[c1] -= cap1
                rem_cnt[c2] -= cap2
                dual_ships.append({'c': c1, 'cap': cap1, 'link': 0, 'hid': 0, 'c2': c2, 'cap2': cap2})

    caps = {i: design.split(n, maxc) for i, n in enumerate(rem_cnt)}
    initial_kept = dual_ships + [{'c': c, 'cap': cap, 'link': 0, 'hid': 0, 'c2': 0, 'cap2': 0} for c, cl in caps.items() for cap in cl]

    def penalty_fn(r, sm, t):
        if t == 'O':
            p = 0.0
            if r < 0.18: p += (0.18 - r) * 2.5
            elif r > 0.35: p += (r - 0.35) * 3.5
            if sm < 0.88: p += (0.88 - sm) * 4.0
            elif sm > 0.95: p += (sm - 0.95) * 2.0
            return p
        return design.penalty(r, sm, t)

    if tier == 'O':
        design.LINKS['O'] = (3, 5)
        design.HIDDEN['O'] = (4, 6)
    elif tier == 'K':
        design.LINKS['K'] = (1, 2)
        design.HIDDEN['K'] = (1, 2)
    elif tier == 'S':
        design.LINKS['S'] = (5, 6)
        design.HIDDEN['S'] = (5, 6)

    best = None
    iters = 350
    for restart in range(4):
        rnd = random.Random(seed + restart * 150)
        ordered = smart_initial_order(initial_kept, rnd)
        if not ordered:
            continue
        s = [dict(x) for x in ordered]
        for _ in range(60):
            t = design.mutate(s, tier, rnd)
            if design.valid_order(t): s = t
            if design.structure_ok(s, tier): break
        r, sm, _ = design.run(W, H, g, s, 60, False, seed)
        p = penalty_fn(r, sm, tier) + (0 if design.structure_ok(s, tier) else 1)
        for it in range(iters):
            t = design.mutate(s, tier, rnd)
            if not design.valid_order(t): continue
            r2, sm2, _ = design.run(W, H, g, t, 60, False, seed + it)
            p2 = penalty_fn(r2, sm2, tier) + (0 if design.structure_ok(t, tier) else 1)
            if p2 <= p:
                s, p, r, sm = t, p2, r2, sm2
            if p == 0 and it > 40: break
        r, sm, d = design.run(W, H, g, s, 300, True, 777)
        rm, smm, _ = design.run(W, H, g, s, 300, False, 778, 'm')
        pf = penalty_fn(r, sm, tier) + (0 if design.structure_ok(s, tier) else 1) + (0 if d == 1 else 2) + max(0, sm - smm - 0.1)
        if best is None or pf < best[0]:
            best = (pf, s, r, sm, d, rm, smm)
        if pf == 0: break

    pf, s, r, sm, d, rm, smm = best
    with open(out_json, 'w') as f:
        json.dump({'png': png, 'tier': tier, 'cols': cols, 'pal': pal, 'cnt': cnt, 'seq': s,
                   'random': r, 'smart': sm, 'dfs': d, 'tap_random': rm, 'tap_smart': smm, 'pen': pf}, f)
    nl, nh = design.counts(s)
    ndual = sum(1 for x in s if x.get('cap2'))
    print(f"[{tier}] {os.path.basename(png):18}: cols={cols} ships={len(s):2} dual={ndual} links={nl} hids={nh} | random={r:.2f} smart={sm:.2f} dfs={d} tap={rm:.2f}/{smm:.2f} pen={pf:.3f}")
    return pf, r, sm, d, rm, smm, len(s), ndual, nl, nh

def main():
    report_rows = []
    print("=== 15 YENİ LEVEL ÜRETİMİ BAŞLIYOR ===")
    for file_num, name, title, png, tier, cols, dual_spec, mystery_frac, seq_pos in LEVELS_15:
        print(f"\n--- Seviye {file_num}: {name} ({title}) [Hedef: {tier}] ---")
        info, errs, warns = level_asset.art_report(png)
        if errs:
            print("HATA art_report:", errs)
            sys.exit(1)

        out_json = f"/tmp/design_{name}.json"
        asset_path = f"Assets/Levels/Level_{file_num}_{name}.asset"
        if os.path.exists(asset_path) and os.path.exists(out_json) and file_num < 132:
            print(f"{name} zaten üretildi, atlanıyor...")
            d_data = json.load(open(out_json))
            report_rows.append({
                'num': file_num, 'name': name, 'title': title, 'tier': tier,
                'cubes': info['cubes'], 'cols': cols, 'ships': len(d_data['seq']),
                'dual': sum(1 for x in d_data['seq'] if x.get('cap2')),
                'links': design.counts(d_data['seq'])[0],
                'hidden': design.counts(d_data['seq'])[1],
                'random': d_data['random'], 'smart': d_data['smart'], 'dfs': d_data['dfs'],
                'tap': f"{d_data.get('tap_random', 0):.2f}/{d_data.get('tap_smart', 0):.2f}",
                'seq_pos': seq_pos
            })
            continue

        pf, r, sm, d, rm, smm, num_ships, ndual, nl, nh = optimize_level(png, tier, cols, dual_spec, out_json)

        if not os.path.exists(asset_path):
            class Args: pass
            a = Args()
            a.png = png
            a.name = name
            a.title = title
            a.index = file_num
            try: level_asset.cmd_new(a)
            except SystemExit as se:
                if se.code != 0: raise

        class ApplyArgs: pass
        aa = ApplyArgs()
        aa.asset = asset_path
        aa.design = out_json
        aa.cols = cols
        aa.tier = tier
        try: level_asset.cmd_apply(aa)
        except SystemExit as se:
            if se.code != 0: raise

        if mystery_frac > 0:
            class MystArgs: pass
            ma = MystArgs()
            ma.asset = asset_path
            ma.frac = mystery_frac
            ma.seed = 42
            ma.clear = False
            try: level_asset.cmd_mystery(ma)
            except SystemExit as se:
                if se.code != 0: raise

        content = open(asset_path).read()
        content = re.sub(r'm_LevelIndex: \d+', f'm_LevelIndex: {seq_pos}', content, count=1)
        open(asset_path, 'w').write(content)

        class CheckArgs: pass
        ca = CheckArgs()
        ca.asset = asset_path
        try: level_asset.cmd_check(ca)
        except SystemExit as se:
            if se.code != 0:
                print(f"HATA cmd_check on {asset_path} exited with {se.code}")
                sys.exit(1)

        report_rows.append({
            'num': file_num,
            'name': name,
            'title': title,
            'tier': tier,
            'cubes': info['cubes'],
            'cols': cols,
            'ships': num_ships,
            'dual': ndual,
            'links': nl,
            'hidden': nh,
            'random': r,
            'smart': sm,
            'dfs': d,
            'tap': f"{rm:.2f}/{smm:.2f}",
            'seq_pos': seq_pos
        })

    # Update LevelSequence.asset
    sorted_by_pos = sorted(LEVELS_15, key=lambda x: x[8])
    seq_file = "Assets/Levels/LevelSequence.asset"
    seq_content = open(seq_file).read()
    
    new_entries = []
    for file_num, name, title, png, tier, cols, dual_spec, mystery_frac, seq_pos in sorted_by_pos:
        meta_file = f"Assets/Levels/Level_{file_num}_{name}.asset.meta"
        meta_text = open(meta_file).read()
        guid = re.search(r'guid: (\w+)', meta_text).group(1)
        entry = f"  - {{fileID: 11400000, guid: {guid}, type: 2}}"
        if entry not in seq_content:
            new_entries.append(entry)

    if new_entries:
        seq_content = seq_content.rstrip() + "\n" + "\n".join(new_entries) + "\n"
        open(seq_file, 'w').write(seq_content)
        print(f"\nLevelSequence.asset güncellendi: {len(new_entries)} yeni seviye eklendi.")

    with open('/tmp/batch_report_15.json', 'w') as f:
        json.dump(report_rows, f, indent=2)
    print("\n=== TÜM 15 YENİ LEVEL BAŞARIYLA ÜRETİLDİ VE DOĞRULANDI ===")

if __name__ == '__main__':
    main()
