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
        father_id = '0'
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

def get_full_path(tr_id):
    if tr_id not in transforms: return "Unknown"
    data = transforms[tr_id]
    name = game_objects.get(data['go_id'], f"GO_{data['go_id']}")
    if data['father_id'] and data['father_id'] != '0':
        return get_full_path(data['father_id']) + " -> " + name
    return name

for f_id, data in transforms.items():
    go_id = data['go_id']
    name = game_objects.get(go_id, '')
    if any(k in name.lower() for k in ['pixel', 'slot', 'water', 'truck', 'manager', 'level']):
        path = get_full_path(f_id)
        print(f"Path: {path} | Pos: {data['pos']} | Scale: {data['scale']}")
