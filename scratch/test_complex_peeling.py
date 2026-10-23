from test_peeling import compute_peeling_layers

# Create an anchor / T-shape or sailboat silhouette
# T-shape:
# 6 across top, 2 wide stem in middle down 4 rows
cubes = set()
for x in range(8):
    cubes.add((x, 5))
    cubes.add((x, 4))
for y in range(4):
    cubes.add((3, y))
    cubes.add((4, y))

layers = compute_peeling_layers(cubes)
print("Peeling Layers for T-shape:")
for y in range(5, -1, -1):
    row = "".join(f"{layers.get((x,y), -1):2d}" for x in range(8))
    print(f"y={y}: {row.replace('-1', ' .')}")
