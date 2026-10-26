import numpy as np
from PIL import Image
from collections import deque

def run_solvability_sim():
    im = Image.open('Assets/32x32/BalPetegi.png')
    arr = np.array(im)
    h, w = arr.shape[:2]
    
    # Color palette
    colors = [
        (212, 126, 16, 255),  # 0: Süzülen Bal
        (252, 188, 28, 255),  # 1: Altın Bal
        (148, 80, 20, 255),   # 2: Petek Çerçevesi
        (68, 36, 12, 255),    # 3: Koyu Çikolata / Arı
        (255, 255, 255, 255), # 4: Bal Parıltısı & Kanat
    ]
    
    grid = np.full((h, w), -1, dtype=int)
    for y in range(h):
        for x in range(w):
            c = tuple(arr[y, x])
            if c[3] > 0:
                for idx, pal in enumerate(colors):
                    if c == pal:
                        grid[y, x] = idx
                        break
                        
    # Wagon sequence:
    wagons = [
        (0, 30), (1, 20), (2, 20), (3, 20), (4, 20),
        (0, 30), (1, 20), (2, 20), (3, 20), (4, 13),
        (0, 30), (1, 20), (2, 22), (0, 30), (1, 13),
        (0, 30), (0, 30), (0, 30), (0, 30), (0, 30),
        (0, 30), (0, 20)
    ]
    
    print(f"Total cubes in grid: {np.sum(grid >= 0)}")
    for i, col in enumerate(colors):
        cnt = np.sum(grid == i)
        wag_cap = sum(cap for c_idx, cap in wagons if c_idx == i)
        print(f"Color {i}: {cnt} cubes vs {wag_cap} wagon cap (Diff: {wag_cap - cnt})")
        
    # Simulate step-by-step
    # Outside air flood fill (4 directions):
    def get_exposed():
        # Pad grid with air
        pad = np.pad(grid, 1, mode='constant', constant_values=-1)
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
                        
        exposed = np.zeros_like(grid, dtype=bool)
        for y in range(h):
            for x in range(w):
                if grid[y, x] >= 0:
                    # check 4 neighbors in pad (y+1, x+1)
                    py, px = y + 1, x + 1
                    for dy, dx in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
                        if air[py + dy, px + dx]:
                            exposed[y, x] = True
                            break
        return exposed

    # Simulation with 4 slots:
    slots = [] # active wagons in dock (color, remaining_capacity)
    queue = deque(wagons)
    max_slots = 4
    
    steps = 0
    while np.sum(grid >= 0) > 0 and steps < 200:
        steps += 1
        # Fill empty slots from queue
        while len(slots) < max_slots and queue:
            slots.append(list(queue.popleft()))
            
        exposed = get_exposed()
        cleared_any = False
        
        # Collect matching cubes for each slot
        completed_slots = []
        for s_idx, slot in enumerate(slots):
            c_idx, rem_cap = slot
            # Find exposed cubes of this color
            match = np.argwhere(exposed & (grid == c_idx))
            if len(match) > 0:
                take = min(rem_cap, len(match))
                for i in range(take):
                    my, mx = match[i]
                    grid[my, mx] = -1
                slot[1] -= take
                cleared_any = True
                if slot[1] == 0:
                    completed_slots.append(s_idx)
                    
        # Remove completed slots (in reverse order)
        for s_idx in sorted(completed_slots, reverse=True):
            del slots[s_idx]
            
        if not cleared_any:
            # Check if any slot has remaining capacity but no exposed cubes
            print(f"Deadlock detected at step {steps}!")
            print(f"Remaining cubes: {np.sum(grid >= 0)}")
            print(f"Active slots: {slots}")
            return False

    if np.sum(grid >= 0) == 0:
        print(f"Level solved successfully in {steps} steps! Zero deadlock!")
        return True
    else:
        print("Did not finish completely.")
        return False

if __name__ == '__main__':
    run_solvability_sim()
