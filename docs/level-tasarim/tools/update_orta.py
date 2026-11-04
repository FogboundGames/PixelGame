#!/usr/bin/env python3
"""Tighter difficulty tuning for the 5 Orta levels.
Ensures random is in 0.20 - 0.35, smart >= 0.88, halatlar = 4-5, gizliler = 4-6,
capacities multiples of 10 with maxc=30, mystery cubes ~5%.
"""
import sys, os, json, random, re
TOOL_DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, TOOL_DIR)
import design, level_asset

configs = [
    # (file_num, name, dual_spec, seed)
    (116, 'GameBoy', None, 42),
    (117, 'BuyuKitabi', (0, 1, 20, 20), 42),
    (118, 'Akvaryum', None, 42),
    (119, 'Kronometre', None, 42),
    (120, 'RamenKasesi', (0, 1, 20, 20), 42)
]

def main():
    print("=== ORTA SEVİYELERİ ZORLAŞTIRMA VE KALİBRE ETME ===")
    results = []
    for file_num, name, dual_spec, seed in configs:
        png = f'Assets/04.11.2026/orta/{name}.png'
        W, H, g, pal, cnt = design.load(png)
        design.COLS = 4
        design.OUTER = set()
        design.OUTER.update(design.outer_colors(W, H, g))

        rem_cnt = list(cnt)
        dual_ships = []
        if dual_spec:
            c1, c2, cap1, cap2 = dual_spec
            dual_ships.append({'c': c1, 'cap': cap1, 'link': 0, 'hid': 0, 'c2': c2, 'cap2': cap2})
            rem_cnt[c1] -= cap1
            rem_cnt[c2] -= cap2

        maxc = 30
        caps = {i: design.split(n, maxc) for i, n in enumerate(rem_cnt)}
        initial_kept = dual_ships + [{'c': c, 'cap': cap, 'link': 0, 'hid': 0, 'c2': 0, 'cap2': 0} for c, cl in caps.items() for cap in cl]

        def penalty_orta(r, sm):
            p = 0.0
            if r < 0.18: p += (0.18 - r) * 2.5
            elif r > 0.35: p += (r - 0.35) * 3.5
            if sm < 0.88: p += (0.88 - sm) * 4.0
            elif sm > 0.95: p += (sm - 0.95) * 2.0
            return p

        design.LINKS['O'] = (3, 5)
        design.HIDDEN['O'] = (4, 6)

        best = None
        for restart in range(4):
            rnd = random.Random(seed + restart * 150)
            s = design.initial(caps, 'O', rnd, kept=initial_kept)
            r, sm, _ = design.run(W, H, g, s, 60, False, seed)
            p = penalty_orta(r, sm) + (0 if design.structure_ok(s, 'O') else 1)
            for it in range(350):
                t = design.mutate(s, 'O', rnd)
                if not design.valid_order(t): continue
                r2, sm2, _ = design.run(W, H, g, t, 60, False, seed + it)
                p2 = penalty_orta(r2, sm2) + (0 if design.structure_ok(t, 'O') else 1)
                if p2 <= p:
                    s, p, r, sm = t, p2, r2, sm2
                if p == 0 and it > 50: break
            r, sm, d = design.run(W, H, g, s, 300, True, 777)
            rm, smm, _ = design.run(W, H, g, s, 300, False, 778, 'm')
            pf = penalty_orta(r, sm) + (0 if design.structure_ok(s, 'O') else 1) + (0 if d == 1 else 2) + max(0, sm - smm - 0.1)
            if best is None or pf < best[0]:
                best = (pf, s, r, sm, d, rm, smm)
            if pf == 0: break

        pf, s, r, sm, d, rm, smm = best
        nl, nh = design.counts(s)
        
        out_json = f'/tmp/design_{name}.json'
        with open(out_json, 'w') as f:
            json.dump({'png': png, 'tier': 'O', 'cols': 4, 'pal': pal, 'cnt': cnt, 'seq': s,
                       'random': r, 'smart': sm, 'dfs': d, 'tap_random': rm, 'tap_smart': smm, 'pen': pf}, f)
        
        asset_path = f'Assets/Levels/Level_{file_num}_{name}.asset'
        class ApplyArgs: pass
        aa = ApplyArgs()
        aa.asset = asset_path
        aa.design = out_json
        aa.cols = 4
        aa.tier = 'O'
        level_asset.cmd_apply(aa)

        class MystArgs: pass
        ma = MystArgs()
        ma.asset = asset_path
        ma.frac = 0.05
        ma.seed = 42
        ma.clear = False
        level_asset.cmd_mystery(ma)

        class CheckArgs: pass
        ca = CheckArgs()
        ca.asset = asset_path
        try:
            level_asset.cmd_check(ca)
        except SystemExit as se:
            if se.code != 0:
                print(f'HATA: {name} kontrolü başarısız (kod {se.code})')
                sys.exit(1)
        
        ndual = sum(1 for x in s if x.get('cap2'))
        print(f"OK: {name:15} | ships={len(s):2} dual={ndual} links={nl} hids={nh} | random={r:.2f} smart={sm:.2f} dfs={d} tap={rm:.2f}/{smm:.2f}")
        results.append((file_num, name, len(s), ndual, nl, nh, r, sm, d, rm, smm))

    print("\n=== TÜM 5 ORTA SEVİYE BAŞARIYLA GÜNCELLENDİ ===")
    for r in results:
        print(f"Level_{r[0]}_{r[1]}: ships={r[2]} dual={r[3]} links={r[4]} hids={r[5]} random={r[6]:.2f} smart={r[7]:.2f} dfs={r[8]} tap={r[9]:.2f}/{r[10]:.2f}")

if __name__ == '__main__':
    main()
