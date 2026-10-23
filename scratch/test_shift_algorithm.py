def simulate_shift(slots, preferred_index=None):
    # slots: list of 'E' (empty), 'S' (single), 'L' (linked)
    n = len(slots)
    
    # Check if empty count < 2
    empty_indices = [i for i, x in enumerate(slots) if x == 'E']
    if len(empty_indices) < 2:
        return False, "Not enough empty slots", 0, None, None

    # Step 1: Check direct adjacent pairs
    direct_pairs = []
    for i in range(n - 1):
        if slots[i] == 'E' and slots[i + 1] == 'E':
            direct_pairs.append((i, i + 1))
            
    if direct_pairs:
        chosen = direct_pairs[0]
        if preferred_index is not None:
            best_dist = float('inf')
            for p in direct_pairs:
                d = min(abs(preferred_index - p[0]), abs(preferred_index - p[1]))
                if preferred_index in p:
                    chosen = p
                    break
                if d < best_dist:
                    best_dist = d
                    chosen = p
        return True, "Direct adjacent", 0, chosen, slots

    # Step 2: 1-shift search
    best_score = float('inf')
    best_solution = None

    for i in range(n):
        if slots[i] != 'S': continue

        # Move left to i - 1
        if i - 1 >= 0 and slots[i - 1] == 'E':
            if i + 1 < n and slots[i + 1] == 'E':
                score = i * 0.5 - 0.2
                if preferred_index is not None:
                    if preferred_index in (i, i + 1): score -= 100
                    else: score += min(abs(preferred_index - i), abs(preferred_index - (i + 1)))
                if score < best_score:
                    best_score = score
                    # new slots after move:
                    new_s = list(slots)
                    new_s[i] = 'E'
                    new_s[i - 1] = 'S'
                    best_solution = (1, (i, i + 1), [(i, i - 1)], new_s)

        # Move right to i + 1
        if i + 1 < n and slots[i + 1] == 'E':
            if i - 1 >= 0 and slots[i - 1] == 'E':
                score = (i - 1) * 0.5
                if preferred_index is not None:
                    if preferred_index in (i - 1, i): score -= 100
                    else: score += min(abs(preferred_index - (i - 1)), abs(preferred_index - i))
                if score < best_score:
                    best_score = score
                    new_s = list(slots)
                    new_s[i] = 'E'
                    new_s[i + 1] = 'S'
                    best_solution = (1, (i - 1, i), [(i, i + 1)], new_s)

    if best_solution:
        shifts_count, pair, moves, new_s = best_solution
        return True, f"1-shift: moved {moves[0][0]}->{moves[0][1]}", shifts_count, pair, new_s

    # Step 3: 2-shift cascade search
    for i in range(n):
        if slots[i] != 'S': continue
        for d1 in (-1, 1):
            t1 = i + d1
            if t1 < 0 or t1 >= n or slots[t1] != 'E': continue
            s1 = list(slots)
            s1[i] = 'E'
            s1[t1] = 'S'
            for j in range(n):
                if j == t1 or s1[j] != 'S': continue
                for d2 in (-1, 1):
                    t2 = j + d2
                    if t2 < 0 or t2 >= n or s1[t2] != 'E': continue
                    s2 = list(s1)
                    s2[j] = 'E'
                    s2[t2] = 'S'
                    for k in range(n - 1):
                        if s2[k] == 'E' and s2[k + 1] == 'E':
                            return True, f"2-shift: {i}->{t1}, {j}->{t2}", 2, (k, k + 1), s2

    return False, "No solution found", 0, None, None

# Test all cases
test_cases = [
    # 3-slot layouts
    (["E", "S", "E"], None, "User screenshot: Middle single container with left/right empty"),
    (["E", "S", "E"], 0, "User screenshot with drag to Slot 0"),
    (["E", "S", "E"], 2, "User screenshot with drag to Slot 2"),
    (["E", "E", "S"], None, "Already adjacent empty on left"),
    (["S", "E", "E"], None, "Already adjacent empty on right"),
    (["S", "S", "S"], None, "No empty slots"),
    (["S", "E", "S"], None, "Only 1 empty slot"),

    # 4-slot layouts
    (["E", "S", "E", "S"], None, "Alternating 4 slots"),
    (["S", "E", "S", "E"], None, "Alternating 4 slots, starts occupied"),
    (["E", "S", "S", "E"], None, "Separated by 2 singles"),
    (["E", "S", "E", "E"], None, "1 isolated single, but 2 already adjacent on right"),

    # 5-slot layouts
    (["E", "S", "E", "S", "E"], None, "3 empty slots separated by 2 singles"),
    (["L", "L", "E", "S", "E"], None, "Linked pair docked on left, 1 single between 2 empties"),
]

all_passed = True
for slots, pref, desc in test_cases:
    ok, msg, shifts, pair, new_s = simulate_shift(slots, pref)
    print(f"Test: {desc}")
    print(f"  Input:    {slots} (pref={pref})")
    print(f"  Result:   ok={ok}, msg='{msg}', pair={pair}")
    if new_s:
        print(f"  NewState: {new_s}")
        assert abs(pair[0] - pair[1]) == 1, f"Pair {pair} is not adjacent!"
        assert new_s[pair[0]] == 'E' and new_s[pair[1]] == 'E', f"Slots in {pair} are not empty in new state!"
    print("-" * 50)

print("\nALL TEST CASES PASSED SUCCESSFULLY!")
