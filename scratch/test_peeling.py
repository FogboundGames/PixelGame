def compute_peeling_layers(all_cubes):
    """
    Computes topological peeling layers (onion shells) for any arbitrary shape:
    Layer 0: Outer perimeter touching outside air
    Layer 1: Next perimeter after removing Layer 0
    Layer 2: Next perimeter after removing Layer 1...
    """
    remaining = set(all_cubes)
    layers = {}
    current_layer = 0
    dirs = [(0, -1), (-1, 0), (1, 0), (0, 1)]

    while remaining:
        # Compute outside air for remaining
        min_x = min(c[0] for c in remaining) - 1
        max_x = max(c[0] for c in remaining) + 1
        min_y = min(c[1] for c in remaining) - 1
        max_y = max(c[1] for c in remaining) + 1

        air = set()
        start = (min_x, min_y)
        air.add(start)
        queue = [start]

        while queue:
            cur = queue.pop(0)
            for dx, dy in dirs:
                n = (cur[0] + dx, cur[1] + dy)
                if n[0] < min_x or n[0] > max_x or n[1] < min_y or n[1] > max_y:
                    continue
                if n in air or n in remaining:
                    continue
                air.add(n)
                queue.append(n)

        # Cubes touching outside air belong to current_layer
        shell = set()
        for c in remaining:
            touches_air = any((c[0] + dx, c[1] + dy) in air for dx, dy in dirs)
            if touches_air:
                shell.add(c)

        if not shell:
            # Enclosed island without air contact
            for c in remaining:
                layers[c] = current_layer
            break

        for c in shell:
            layers[c] = current_layer
            remaining.remove(c)

        current_layer += 1

    return layers

# Let's test on 6x6
cubes = set((x, y) for x in range(6) for y in range(6))
layers = compute_peeling_layers(cubes)

print("Topological Peeling Layers for 6x6 Square:")
for y in range(5, -1, -1):
    row = "".join(f"{layers[(x,y)]:2d}" for x in range(6))
    print(f"y={y}: {row}")
