with open('Assets/Resources/Sailor_Runner.prefab', 'r', encoding='utf-8') as f:
    lines = f.readlines()

for i, l in enumerate(lines[:100]):
    print(l, end='')
