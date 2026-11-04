#!/usr/bin/env python3
"""Batch level generator for 10 new levels (2 Kolay, 5 Orta, 3 Zor).
Includes dual-color (Süper/Güçlü) container support for Orta and Zor levels.
"""
import os, sys, json, random, re, subprocess, collections
from PIL import Image

TOOL_DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, TOOL_DIR)
import design, level_asset

LEVELS = [
    # (file_num, name, title, png_path, tier, cols, dual_count, mystery_frac, seq_pos)
    (114, "KahveKupasi", "Kahve Kupası", "Assets/04.11.2026/kolay/KahveKupasi.png", "K", 3, 0, 0.0, 114),
    (115, "Avokado", "Avokado", "Assets/04.11.2026/kolay/Avokado.png", "K", 3, 0, 0.0, 120),
    (116, "GameBoy", "Game Boy", "Assets/04.11.2026/orta/GameBoy.png", "O", 4, 1, 0.04, 115),
    (117, "BuyuKitabi", "Büyü Kitabı", "Assets/04.11.2026/orta/BuyuKitabi.png", "O", 4, 1, 0.04, 117),
    (118, "Akvaryum", "Akvaryum", "Assets/04.11.2026/orta/Akvaryum.png", "O", 4, 1, 0.04, 118),
    (119, "Kronometre", "Kronometre", "Assets/04.11.2026/orta/Kronometre.png", "O", 4, 1, 0.04, 121),
    (120, "RamenKasesi", "Ramen Kasesi", "Assets/04.11.2026/orta/RamenKasesi.png", "O", 4, 1, 0.04, 122),
    (121, "Kapadokya", "Kapadokya", "Assets/04.11.2026/zor/Kapadokya.png", "S", 4, 2, 0.05, 113),
    (122, "JaponTapinagi", "Japon Tapınağı", "Assets/04.11.2026/zor/JaponTapinagi.png", "S", 4, 2, 0.05, 116),
    (123, "SiberSehir", "Siber Şehir", "Assets/04.11.2026/zor/SiberSehir.png", "S", 4, 2, 0.05, 119),
]

def optimize_level(png, tier, cols, dual_count, out_json, seed=42):
    W, H, g, pal, cnt = design.load(png)
    design.COLS = cols
    design.OUTER = set()
    design.OUTER.update(design.outer_colors(W, H, g))

    maxc = 50 if tier in 'ZS' else (40 if tier == 'O' or sum(cnt) >= 900 else 30)
    rem_cnt = list(cnt)
    dual_ships = []

    if dual_count > 0:
        cands = [i for i, n in enumerate(cnt) if n >= (50 if tier in 'ZS' else 35)]
        used = set()
        for _ in range(dual_count):
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
                cap1 = 40 if tier in 'ZS' and rem_cnt[c1] >= 80 else (30 if rem_cnt[c1] >= 60 else 20)
                cap2 = 40 if tier in 'ZS' and rem_cnt[c2] >= 80 else (30 if rem_cnt[c2] >= 60 else 20)
                rem_cnt[c1] -= cap1
                rem_cnt[c2] -= cap2
                dual_ships.append({'c': c1, 'cap': cap1, 'link': 0, 'hid': 0, 'c2': c2, 'cap2': cap2})

    caps = {i: design.split(n, maxc) for i, n in enumerate(rem_cnt)}
    initial_kept = dual_ships + [{'c': c, 'cap': cap, 'link': 0, 'hid': 0, 'c2': 0, 'cap2': 0} for c, cl in caps.items() for cap in cl]

    best = None
    iters = 350
    for restart in range(3):
        rnd = random.Random(seed + restart * 1000)
        s = design.initial(caps, tier, rnd, kept=initial_kept)
        r, sm, _ = design.run(W, H, g, s, 60, False, seed)
        p = design.penalty(r, sm, tier) + (0 if design.structure_ok(s, tier) else 1)
        for it in range(iters):
            t = design.mutate(s, tier, rnd)
            if not design.valid_order(t):
                continue
            r2, sm2, _ = design.run(W, H, g, t, 60, False, seed + it)
            p2 = design.penalty(r2, sm2, tier) + (0 if design.structure_ok(t, tier) else 1)
            if p2 <= p:
                s, p, r, sm = t, p2, r2, sm2
            if p == 0 and it > 40:
                break
        r, sm, d = design.run(W, H, g, s, 300, True, 777)
        rm, smm, _ = design.run(W, H, g, s, 300, False, 778, 'm')
        pf = design.penalty(r, sm, tier) + (0 if design.structure_ok(s, tier) else 1) + (0 if d == 1 else 2) + max(0, sm - smm - 0.1)
        if best is None or pf < best[0]:
            best = (pf, s, r, sm, d, rm, smm)
        if pf == 0:
            break

    pf, s, r, sm, d, rm, smm = best
    with open(out_json, 'w') as f:
        json.dump({'png': png, 'tier': tier, 'cols': cols, 'pal': pal, 'cnt': cnt, 'seq': s,
                   'random': r, 'smart': sm, 'dfs': d, 'tap_random': rm, 'tap_smart': smm, 'pen': pf}, f)
    nl, nh = design.counts(s)
    ndual = sum(1 for x in s if x.get('cap2'))
    print(f"[{tier}] {os.path.basename(png)}: cols={cols} ships={len(s)} dual={ndual} links={nl} hidden={nh} | random={r:.2f} smart={sm:.2f} dfs={d} tap={rm:.2f}/{smm:.2f} pen={pf:.3f}")
    return pf, r, sm, d, rm, smm, len(s), ndual, nl, nh

def main():
    report_rows = []
    print("=== 10 LEVEL ÜRETİMİ BAŞLIYOR ===")
    for file_num, name, title, png, tier, cols, dual_count, mystery_frac, seq_pos in LEVELS:
        print(f"\n--- Seviye {file_num}: {name} ({title}) [Hedef: {tier}] ---")
        # 1. Art Check
        info, errs, warns = level_asset.art_report(png)
        if errs:
            print("HATA art_report:", errs)
            sys.exit(1)

        # 2. Design JSON
        out_json = f"/tmp/design_{name}.json"
        pf, r, sm, d, rm, smm, num_ships, ndual, nl, nh = optimize_level(png, tier, cols, dual_count, out_json)

        # 3. Create Asset if not exists
        asset_path = f"Assets/Levels/Level_{file_num}_{name}.asset"
        if not os.path.exists(asset_path):
            class Args: pass
            a = Args()
            a.png = png
            a.name = name
            a.title = title
            a.index = file_num
            try:
                level_asset.cmd_new(a)
            except SystemExit as se:
                if se.code != 0: raise

        # 4. Apply Design
        class ApplyArgs: pass
        aa = ApplyArgs()
        aa.asset = asset_path
        aa.design = out_json
        aa.cols = cols
        aa.tier = tier
        try:
            level_asset.cmd_apply(aa)
        except SystemExit as se:
            if se.code != 0: raise

        # 5. Mystery Cubes
        if mystery_frac > 0:
            class MystArgs: pass
            ma = MystArgs()
            ma.asset = asset_path
            ma.frac = mystery_frac
            ma.seed = 42
            ma.clear = False
            try:
                level_asset.cmd_mystery(ma)
            except SystemExit as se:
                if se.code != 0: raise

        # 6. Set m_LevelIndex to seq_pos
        content = open(asset_path).read()
        content = re.sub(r'm_LevelIndex: \d+', f'm_LevelIndex: {seq_pos}', content, count=1)
        open(asset_path, 'w').write(content)

        # 7. Check Asset
        class CheckArgs: pass
        ca = CheckArgs()
        ca.asset = asset_path
        try:
            level_asset.cmd_check(ca)
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
    # Sort levels by seq_pos:
    sorted_by_pos = sorted(LEVELS, key=lambda x: x[8])
    seq_file = "Assets/Levels/LevelSequence.asset"
    seq_content = open(seq_file).read()
    
    new_entries = []
    for file_num, name, title, png, tier, cols, dual_count, mystery_frac, seq_pos in sorted_by_pos:
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

    # Save summary report
    with open('/tmp/batch_report.json', 'w') as f:
        json.dump(report_rows, f, indent=2)
    print("\n=== TÜM 10 LEVEL BAŞARIYLA ÜRETİLDİ VE DOĞRULANDI ===")

if __name__ == '__main__':
    main()
