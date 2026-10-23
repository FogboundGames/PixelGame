def compute_outside_air(occupied):
    if not occupied:
        return set()
    min_x = min(c[0] for c in occupied) - 1
    max_x = max(c[0] for c in occupied) + 1
    min_y = min(c[1] for c in occupied) - 1
    max_y = max(c[1] for c in occupied) + 1

    air = set()
    start = (min_x, min_y)
    air.add(start)
    queue = [start]
    dirs = [(0, -1), (-1, 0), (1, 0), (0, 1)]

    while queue:
        cur = queue.pop(0)
        for dx, dy in dirs:
            n = (cur[0] + dx, cur[1] + dy)
            if n[0] < min_x or n[0] > max_x or n[1] < min_y or n[1] > max_y:
                continue
            if n in air or n in occupied:
                continue
            air.add(n)
            queue.append(n)
    return air

def compute_rim_and_openness(occupied, air):
    row_min = {}
    row_max = {}
    col_min = {}
    col_max = {}
    for x, y in occupied:
        if y not in row_min or x < row_min[y]: row_min[y] = x
        if y not in row_max or x > row_max[y]: row_max[y] = x
        if x not in col_min or y < col_min[x]: col_min[x] = y
        if x not in col_max or y > col_max[x]: col_max[x] = y

    dirs_card = [(0, -1), (-1, 0), (1, 0), (0, 1)]
    dirs_diag = [(-1, -1), (-1, 1), (1, -1), (1, 1)]

    metrics = {}
    for x, y in occupied:
        dist_row = min(x - row_min[y], row_max[y] - x)
        dist_col = min(y - col_min[x], col_max[x] - y)
        rim_dist = min(dist_row, dist_col)

        card = sum(1 for dx, dy in dirs_card if (x + dx, y + dy) in air)
        diag = sum(1 for dx, dy in dirs_diag if (x + dx, y + dy) in air)
        metrics[(x, y)] = {
            'rim_dist': rim_dist,
            'card': card,
            'diag': diag,
            'openness': card * 3 + diag
        }
    return metrics

# Test with a 6x6 square
occupied = set((x, y) for x in range(6) for y in range(6))
air = compute_outside_air(occupied)
metrics = compute_rim_and_openness(occupied, air)

print("6x6 Square Rim Distances:")
for y in range(5, -1, -1):
    row = "".join(f"{metrics[(x,y)]['rim_dist']:2d}" for x in range(6))
    print(f"y={y}: {row}")

print("\n6x6 Square Openness:")
for y in range(5, -1, -1):
    row = "".join(f"{metrics[(x,y)]['openness']:2d}" for x in range(6))
    print(f"y={y}: {row}")

# Now suppose one bottom cube at (2, 0) was removed:
occupied.remove((2, 0))
air = compute_outside_air(occupied)
metrics = compute_rim_and_openness(occupied, air)

print("\nAfter removing (2,0), check cube (2,1):")
print(f"Cube (2,1) rim_dist: {metrics[(2,1)]['rim_dist']}, card air: {metrics[(2,1)]['card']}, openness: {metrics[(2,1)]['openness']}")
print(f"Cube (0,0) rim_dist: {metrics[(0,0)]['rim_dist']}, card air: {metrics[(0,0)]['card']}, openness: {metrics[(0,0)]['openness']}")
print(f"Cube (1,0) rim_dist: {metrics[(1,0)]['rim_dist']}, card air: {metrics[(1,0)]['card']}, openness: {metrics[(1,0)]['openness']}")
print(f"Cube (3,0) rim_dist: {metrics[(3,0)]['rim_dist']}, card air: {metrics[(3,0)]['card']}, openness: {metrics[(3,0)]['openness']}")
