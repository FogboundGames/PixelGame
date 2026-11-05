#!/usr/bin/env python3
"""Fix levels with capacity surplus (+50, +61, +102, +42, +20) and rule errors.
Regenerates wagon sequence and updates level assets so that:
1. Every color has assigned capacity == cube count (diff = 0, '✓ Dengeli').
2. Column same-color adjacency rules are strictly obeyed.
3. Halat and hidden ships follow all guidelines.
4. Solvability simulation is verified.
"""
import os, sys, json, random, re, collections
from PIL import Image

TOOL_DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, TOOL_DIR)
import design, level_asset

LEVELS_TO_FIX = [
    # (asset_path, png_path, tier, cols, dual_count, mystery_frac, seq_pos, tex_guid)
    ('Assets/Levels/Level_147_AlpDagEvi.asset', 'Assets/PixelArt/Optimized/AlpDagEvi_6c.png', 'O', 4, 1, 0.05, 123, None),
    ('Assets/Levels/Level_146_RuzgarGulu.asset', 'Assets/PixelArt/Optimized/RuzgarGulu_6c.png', 'K', 4, 0, 0.0, 133, None),
    ('Assets/Levels/Level_141_Elma.asset', 'Assets/04.11.2026/kolay/Elma.png', 'O', 4, 0, 0.0, 136, 'a7f36fd536ff47a591576f876a0098fc'),
    ('Assets/Levels/Level_144_RetroKamera.asset', 'Assets/PixelArt/Optimized/RetroKamera_6c.png', 'O', 4, 1, 0.04, 137, None),
    ('Assets/Levels/Level_138_MuzikKutusu.asset', 'Assets/04.11.2026/orta/MuzikKutusu.png', 'O', 4, 1, 0.04, 148, None),
    ('Assets/Levels/Level_144_KutupAyisi.asset', 'Assets/PixelArt/Optimized/KutupAyisi_6c.png', 'O', 4, 1, 0.05, 150, None),
    ('Assets/Levels/Level_139_GazLambasi.asset', 'Assets/PixelArt/Optimized/GazLambasi_canvas.png', 'O', 4, 0, 0.04, 155, None),
    ('Assets/Levels/Level_143_MisirKocani.asset', 'Assets/04.11.2026/kolay/MisirKocani.png', 'K', 4, 0, 0.0, 157, '73c710da649b45f09354b59e786c283f'),
    ('Assets/Levels/Level_137_BuyuAsasi.asset', 'Assets/04.11.2026/orta/BuyuAsasi.png', 'O', 3, 0, 0.04, 161, None),
    ('Assets/Levels/Level_145_Sihirlikule.asset', 'Assets/PixelArt/Optimized/Sihirlikule_6c.png', 'Z', 4, 1, 0.05, 162, None),
]

def generate_valid_sequence(ships, cols=4, front_outer=1, open_outer=3, seed=42):
    rnd = random.Random(seed)
    n = len(ships)
    for attempt in range(2500):
        pool = list(ships)
        rnd.shuffle(pool)
        color_counts = collections.Counter(s['c'] for s in pool)
        placed = []
        possible = True
        for i in range(n):
            forbidden = placed[i - cols]['c'] if i >= cols else -1
            forbidden_dual = placed[i - cols].get('c2', -1) if i >= cols and placed[i - cols].get('cap2') else -1
            cands = [s for s in pool if s['c'] != forbidden and (s.get('c2') != forbidden if s.get('cap2') else True) and (forbidden_dual < 0 or (s['c'] != forbidden_dual and s.get('c2') != forbidden_dual))]
            if i < cols:
                cur_outer = sum(1 for s in placed if design.is_outer(s))
                cands_non_outer = [s for s in cands if not design.is_outer(s)]
                if cur_outer >= front_outer and cands_non_outer:
                    cands = cands_non_outer
            elif i < 2 * cols:
                cur_outer = sum(1 for s in placed if design.is_outer(s))
                cands_non_outer = [s for s in cands if not design.is_outer(s)]
                if cur_outer >= open_outer and cands_non_outer:
                    cands = cands_non_outer
            if not cands:
                possible = False
                break
            cands.sort(key=lambda s: color_counts[s['c']], reverse=True)
            pick = rnd.choice(cands[:max(1, len(cands)//2)]) if len(cands) > 1 else cands[0]
            placed.append(pick)
            pool.remove(pick)
            color_counts[pick['c']] -= 1
        if possible and design.valid_order(placed):
            return placed
    return None

def smart_initial_order(ships, cols, rnd):
    for fo, oo in [(1, 3), (2, 4), (3, 5), (4, 8), (4, 12)]:
        design.FRONT_OUTER = fo
        design.OPEN_OUTER = oo
        res = generate_valid_sequence(ships, cols=cols, front_outer=fo, open_outer=oo, seed=rnd.randint(1, 999999))
        if res:
            return res
    return None

def optimize_level(png, tier, cols, dual_count, out_json, seed=42):
    W, H, g, pal, cnt = design.load(png)
    design.COLS = cols
    design.OUTER = set()
    design.OUTER.update(design.outer_colors(W, H, g))

    dominant_color = max(range(len(cnt)), key=lambda i: cnt[i])
    dominant_ratio = cnt[dominant_color] / sum(cnt)
    maxc = 50 if tier in 'ZS' else (40 if (tier == 'O' or sum(cnt) >= 900) else 30)

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
                    if (c1, c2) not in used and rem_cnt[c1] >= 25 and rem_cnt[c2] >= 25:
                        pair = (c1, c2)
                        break
                if pair:
                    break
            if pair:
                c1, c2 = pair
                used.add((c1, c2))
                cap1 = 30 if rem_cnt[c1] >= 60 else 20
                cap2 = 30 if rem_cnt[c2] >= 60 else 20
                rem_cnt[c1] -= cap1
                rem_cnt[c2] -= cap2
                dual_ships.append({'c': c1, 'cap': cap1, 'link': 0, 'hid': 0, 'c2': c2, 'cap2': cap2})

    caps = {}
    for i, n in enumerate(rem_cnt):
        if i == dominant_color and dominant_ratio > 0.45:
            caps[i] = design.split(n, maxc)
        elif dominant_ratio > 0.45:
            caps[i] = design.split(n, min(30, maxc))
        else:
            caps[i] = design.split(n, maxc)

    initial_kept = dual_ships + [{'c': c, 'cap': cap, 'link': 0, 'hid': 0, 'c2': 0, 'cap2': 0} for c, cl in caps.items() for cap in cl]

    if tier == 'O':
        design.LINKS['O'] = (2, 4)
        design.HIDDEN['O'] = (2, 4)
    elif tier == 'K':
        design.LINKS['K'] = (1, 2)
        design.HIDDEN['K'] = (1, 2)
    elif tier in ('Z', 'S'):
        design.LINKS['Z'] = (3, 5)
        design.HIDDEN['Z'] = (3, 5)
        design.LINKS['S'] = (3, 5)
        design.HIDDEN['S'] = (3, 5)

    is_big = sum(cnt) >= 900
    iters = 100 if is_big else 180
    restarts = 2 if is_big else 3

    best = None
    for restart in range(restarts):
        rnd = random.Random(seed + restart * 150)
        ordered = smart_initial_order(initial_kept, cols, rnd)
        if not ordered:
            continue
        s = [dict(x) for x in ordered]
        for _ in range(60):
            t = design.mutate(s, tier, rnd)
            if design.valid_order(t):
                s = t
            if design.structure_ok(s, tier):
                break
        r, sm, _ = design.run(W, H, g, s, 50, False, seed)
        p = design.penalty(r, sm, tier) + (0 if design.structure_ok(s, tier) else 1)
        for it in range(iters):
            t = design.mutate(s, tier, rnd)
            if not design.valid_order(t):
                continue
            r2, sm2, _ = design.run(W, H, g, t, 50, False, seed + it)
            p2 = design.penalty(r2, sm2, tier) + (0 if design.structure_ok(t, tier) else 1)
            if p2 <= p:
                s, p, r, sm = t, p2, r2, sm2
            if p == 0 and it > 25:
                break
        r, sm, d = design.run(W, H, g, s, 200, True, 777)
        rm, smm, _ = design.run(W, H, g, s, 200, False, 778, 'm')
        pf = design.penalty(r, sm, tier) + (0 if design.structure_ok(s, tier) else 1) + (0 if d == 1 else 2) + max(0, sm - smm - 0.1)
        print(f"  restart {restart}: r={r:.2f} sm={sm:.2f} dfs={d} pen={pf:.3f}", flush=True)
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
    print(f"[{tier}] {os.path.basename(png):22}: cols={cols} ships={len(s):2} dual={ndual} links={nl} hids={nh} | random={r:.2f} smart={sm:.2f} dfs={d} tap={rm:.2f}/{smm:.2f} pen={pf:.3f}", flush=True)
    return pf, r, sm, d, rm, smm, len(s), ndual, nl, nh

def main():
    print("=== DENGESİZ / HATALI LEVELLERİN DÜZELTİLMESİ BAŞLIYOR ===", flush=True)
    for asset_path, png, tier, cols, dual_count, mystery_frac, seq_pos, tex_guid in LEVELS_TO_FIX:
        name = os.path.basename(asset_path).replace('.asset', '')
        print(f"\n--- {name} [Hedef: {tier}, Sütun: {cols}, Sıra: {seq_pos}] ---", flush=True)
        
        info, errs, warns = level_asset.art_report(png)
        if errs:
            print("HATA art_report:", errs, flush=True)
            sys.exit(1)

        out_json = f"/tmp/fix_{name}.json"
        optimize_level(png, tier, cols, dual_count, out_json)

        # Update texture reference if changed
        if tex_guid:
            content = open(asset_path).read()
            content = re.sub(r'(m_LevelTexture: \{fileID: 2800000, guid: )\w+', r'\g<1>' + tex_guid, content)
            content = re.sub(r'(m_OriginalSourceTexture: \{fileID: 2800000, guid: )\w+', r'\g<1>' + tex_guid, content)
            open(asset_path, 'w').write(content)

        # Apply design to asset
        class ApplyArgs: pass
        aa = ApplyArgs()
        aa.asset = asset_path
        aa.design = out_json
        aa.cols = cols
        aa.tier = tier
        level_asset.cmd_apply(aa)

        # Apply mystery cubes if requested
        if mystery_frac > 0:
            class MystArgs: pass
            ma = MystArgs()
            ma.asset = asset_path
            ma.frac = mystery_frac
            ma.seed = 42
            ma.clear = False
            level_asset.cmd_mystery(ma)

        # Ensure LevelIndex and DifficultyManual are preserved properly
        content = open(asset_path).read()
        content = re.sub(r'm_LevelIndex: \d+', f'm_LevelIndex: {seq_pos}', content, count=1)
        content = re.sub(r'm_DifficultyManual: \d+', 'm_DifficultyManual: 1', content, count=1)
        open(asset_path, 'w').write(content)

        # Verify with cmd_check
        class CheckArgs: pass
        ca = CheckArgs()
        ca.asset = asset_path
        print("Kontrol ediliyor...", flush=True)
        try:
            level_asset.cmd_check(ca)
        except SystemExit as se:
            if se.code != 0:
                print(f"HATA: {name} kontrolü başarısız!")
                sys.exit(1)

    # Also update Level_142_LavLambasi to have DifficultyManual: 1
    lav = 'Assets/Levels/Level_142_LavLambasi.asset'
    if os.path.exists(lav):
        t = open(lav).read()
        t = re.sub(r'm_DifficultyManual: \d+', 'm_DifficultyManual: 1', t, count=1)
        open(lav, 'w').write(t)
        print("\nLevel_142_LavLambasi m_DifficultyManual 1 yapıldı.", flush=True)

    print("\n=== TÜM LEVELLER BAŞARIYLA DÜZELTİLDİ VE DOĞRULANDI ===", flush=True)

if __name__ == '__main__':
    main()
