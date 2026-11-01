"""rule_tests/scenarios.json senaryolarını simülatörden geçirir. Kurallar için Unity: Tools > PixelGame > Kural Katmanı Testleri.
kullanım: python3 run_rule_tests.py"""
import json, os, subprocess, sys, tempfile

D = os.path.dirname(os.path.abspath(__file__))
SIM = os.path.join(D, 'sim.exe' if os.name == 'nt' else 'sim')


def write_level(path, sc):
    grid = [r.split() for r in sc['grid']]
    with open(path, 'w') as f:
        f.write(f'{len(grid[0])} {len(grid)}\n')
        for r in grid:
            f.write(' '.join(r) + '\n')
        f.write(f"{sc['cols']} {sc['rows']} 5\n{len(sc['ships'])}\n")
        for s in sc['ships']:
            f.write(f"{s['c']} {s['cap']} {s.get('link', 0)} {s.get('hid', 0)} {s.get('c2', 0)} {s.get('cap2', 0)}\n")


def main():
    scenarios = json.load(open(os.path.join(D, 'rule_tests', 'scenarios.json')))['scenarios']
    fails = checked = 0
    for sc in scenarios:
        if sc.get('solvable', -1) < 0:
            continue
        checked += 1
        fd, path = tempfile.mkstemp(suffix='.txt')
        os.close(fd)
        write_level(path, sc)
        out = subprocess.run([SIM, path, '20', '1', '1'], capture_output=True, text=True).stdout.split()
        os.remove(path)
        if not out or int(out[2]) != sc['solvable']:
            fails += 1
            print(f"KALDI {sc['name']}: beklenen {sc['solvable']}, simülatör {out}")
    print(f'Simülatör testleri: {checked - fails}/{checked} geçti')
    sys.exit(1 if fails else 0)


if __name__ == '__main__':
    main()
