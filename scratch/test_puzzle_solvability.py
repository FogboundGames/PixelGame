import numpy as np
from PIL import Image
from collections import deque

def test_puzzle_solvability():
    im = Image.open('scratch/puzzle_32.png')
    arr = np.array(im)
    h, w = arr.shape[:2]
    
    # 10 colors
    colors = [
        (248, 116, 24,  255), # 0: Orange (152)
        (244, 104, 180, 255), # 1: Pink (140)
        (22,  108, 224, 255), # 2: Blue (128)
        (252, 252, 252, 255), # 3: White (128)
        (22,  208, 236, 255), # 4: Cyan (116)
        (126, 38,  162, 255), # 5: Purple (106)
        (224, 46,  32,  255), # 6: Red (74)
        (166, 114, 58,  255), # 7: Brown (64)
        (254, 194, 12,  255), # 8: Yellow (64)
        (28,  168, 76,  255), # 9: Green (52)
    ]
    
    grid = np.full((h, w), -1, dtype=int)
    for y in range(h):
        for x in range(w):
            c = tuple(arr[y, x])
            for idx, pal in enumerate(colors):
                if c == pal:
                    grid[y, x] = idx
                    break

    # Let's verify cube counts:
    color_counts = {}
    for i in range(len(colors)):
        cnt = int(np.sum(grid == i))
        color_counts[i] = cnt
        print(f"Color {i}: {cnt} cubes")

    # Split capacities for each color:
    # TruckCapacity = 30, Min = 10
    # 0 Orange (152): 30, 30, 30, 30, 32 = 5 wagons
    # 1 Pink (140): 30, 30, 30, 30, 20 = 5 wagons
    # 2 Blue (128): 30, 30, 30, 20, 18 = 5 wagons
    # 3 White (128): 30, 30, 30, 20, 18 = 5 wagons
    # 4 Cyan (116): 30, 30, 30, 26 = 4 wagons
    # 5 Purple (106): 30, 30, 26, 20 = 4 wagons
    # 6 Red (74): 30, 24, 20 = 3 wagons
    # 7 Brown (64): 30, 20, 14 = 3 wagons
    # 8 Yellow (64): 30, 20, 14 = 3 wagons
    # 9 Green (52): 30, 22 = 2 wagons
    
    # Let's arrange wagons in layers:
    # Outer pieces first: Orange, White, Red, Purple, Green, Pink, Cyan, Blue
    # Inner pieces later: Brown, Yellow, White, Purple
    wagons = [
        # Wave 1: Peel outer border pieces
        (0, 30), (3, 30), (6, 30), (5, 30),
        (9, 30), (1, 30), (4, 30), (2, 30),
        
        # Wave 2: Continuing outer pieces and exposing inner pieces
        (0, 30), (3, 30), (1, 30), (4, 30),
        (2, 30), (6, 24), (5, 30), (9, 22),
        
        # Wave 3: Inner pieces Brown (7) & Yellow (8) join!
        (7, 30), (8, 30), (0, 30), (3, 30),
        (1, 30), (4, 30), (2, 30), (5, 26),
        
        # Wave 4: Harvesting remaining inner and outer cubes
        (7, 20), (8, 20), (0, 30), (1, 30),
        (3, 20), (2, 20), (4, 26), (6, 20),
        
        # Wave 5: Finishing all remaining cubes
        (7, 14), (8, 14), (0, 32), (1, 20),
        (2, 18), (3, 18), (5, 20)
    ]
    
    # Check total capacity per color
    for i in range(len(colors)):
        w_sum = sum(cap for c_idx, cap in wagons if c_idx == i)
        print(f"Color {i}: {color_counts[i]} cubes vs {w_sum} wagon cap (Diff: {w_sum - color_counts[i]})")
        assert w_sum == color_counts[i], f"Mismatch for color {i}!"

    def simulate(max_slots=4):
        test_grid = grid.copy()
        queue = deque(wagons)
        slots = []
        steps = 0
        while np.sum(test_grid >= 0) > 0 and steps < 300:
            steps += 1
            while len(slots) < max_slots and queue:
                slots.append(list(queue.popleft()))
                
            pad = np.pad(test_grid, 1, mode='constant', constant_values=-1)
            air = np.zeros_like(pad, dtype=bool)
            q = deque([(0, 0)])
            air[0, 0] = True
            ph, pw = pad.shape
            while q:
                cy, cx = q.popleft()
                for dy, dx in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
                    ny, nx = cy + dy, cx + dx
                    if 0 <= ny < ph and 0 <= nx < pw:
                        if not air[ny, nx] and pad[ny, nx] == -1:
                            air[ny, nx] = True
                            q.append((ny, nx))
            exposed = np.zeros_like(test_grid, dtype=bool)
            for y in range(h):
                for x in range(w):
                    if test_grid[y, x] >= 0:
                        py, px = y + 1, x + 1
                        for dy, dx in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
                            if air[py + dy, px + dx]:
                                exposed[y, x] = True
                                break
                                
            cleared_any = False
            completed = []
            for s_idx, slot in enumerate(slots):
                c_idx, rem_cap = slot
                match = np.argwhere(exposed & (test_grid == c_idx))
                if len(match) > 0:
                    take = min(rem_cap, len(match))
                    for i in range(take):
                        my, mx = match[i]
                        test_grid[my, mx] = -1
                    slot[1] -= take
                    cleared_any = True
                    if slot[1] == 0:
                        completed.append(s_idx)
            for s_idx in sorted(completed, reverse=True):
                del slots[s_idx]
                
            if not cleared_any:
                print(f"Deadlock at step {steps} with slots={max_slots}!")
                print(f"Remaining cubes: {np.sum(test_grid >= 0)}")
                print(f"Active slots: {slots}")
                return False
                
        print(f"Slots={max_slots}: Solved successfully in {steps} steps! Zero deadlock!")
        return True

    simulate(max_slots=4)
    simulate(max_slots=5)

if __name__ == '__main__':
    test_puzzle_solvability()
