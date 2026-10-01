import re

with open(r'Assets\Scenes\Gemi.unity', 'r', encoding='utf-8', errors='ignore') as f:
    content = f.read()

docs = content.split('--- !u!')
game_objects = {}
transforms = {}

for doc in docs:
    lines = doc.splitlines()
    if not lines: continue
    m = re.match(r'^(\d+)\s+&(-?\d+)', lines[0])
    if not m: continue
    t_id, f_id = m.group(1), m.group(2)
    if t_id == '1':
        name = ''
        for l in lines:
            if l.startswith('  m_Name:'):
                name = l.split(':', 1)[1].strip()
        game_objects[f_id] = name
    elif t_id in ('4', '224'):
        pos, rot, scale = None, None, None
        go_id = None
        father_id = None
        for i, l in enumerate(lines):
            ls = l.strip()
            if ls.startswith('m_GameObject:'):
                m_go = re.search(r'fileID:\s*(-?\d+)', ls)
                if m_go: go_id = m_go.group(1)
            elif ls.startswith('m_Father:'):
                m_fa = re.search(r'fileID:\s*(-?\d+)', ls)
                if m_fa: father_id = m_fa.group(1)
            elif ls.startswith('m_LocalPosition:'):
                pos = ls
            elif ls.startswith('m_LocalScale:'):
                scale = ls
        transforms[f_id] = {'go_id': go_id, 'father_id': father_id, 'pos': pos, 'scale': scale}

for f_id, data in transforms.items():
    go_id = data['go_id']
    name = game_objects.get(go_id, '')
    if any(k in name.lower() for k in ['pixel', 'slot', 'truck', 'art', 'dock', 'manager', 'level', 'spawn', 'background', 'camera', 'canvas', 'board', 'heart']):
        print(f'{name} (GO:{go_id}, TR:{f_id}, father:{data["father_id"]}): pos={data["pos"]} scale={data["scale"]}')
