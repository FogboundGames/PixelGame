import re

with open(r'Assets\Scenes\Gemi.unity', 'r', encoding='utf-8', errors='ignore') as f:
    content = f.read()

docs = content.split('--- !u!')
game_objects = {}
transforms = {}
mono_behaviours = {}

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
        children = []
        in_children = False
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
            elif ls.startswith('m_Children:'):
                in_children = True
            elif in_children:
                if ls.startswith('- {fileID:'):
                    m_ch = re.search(r'fileID:\s*(-?\d+)', ls)
                    if m_ch: children.append(m_ch.group(1))
                else:
                    in_children = False
        transforms[f_id] = {'go_id': go_id, 'father_id': father_id, 'pos': pos, 'scale': scale, 'children': children}
    elif t_id == '114': # MonoBehaviour
        go_id = None
        script = None
        props = {}
        for l in lines:
            ls = l.strip()
            if ls.startswith('m_GameObject:'):
                m_go = re.search(r'fileID:\s*(-?\d+)', ls)
                if m_go: go_id = m_go.group(1)
            elif ls.startswith('m_Script:'):
                script = ls
            elif ':' in ls:
                k, v = ls.split(':', 1)
                props[k.strip()] = v.strip()
        mono_behaviours[f_id] = {'go_id': go_id, 'script': script, 'props': props}

# Print hierarchy starting from root objects
def print_tree(tr_id, depth=0):
    if tr_id not in transforms: return
    d = transforms[tr_id]
    go_name = game_objects.get(d['go_id'], 'unknown')
    if go_name.startswith('Pixel_'): return
    print("  " * depth + f"- {go_name} (TR:{tr_id}) pos={d['pos']} scale={d['scale']}")
    # Print monobehaviours on this go
    for mb_id, mb in mono_behaviours.items():
        if mb['go_id'] == d['go_id']:
            print("  " * depth + f"    [Script: {mb['script']}]")
            # print some interesting props
            for pk in ['levelData', 'currentLevel', 'pixelSize', 'boardOffset', 'targetArea', 'dockPosition', 'slotSpacing']:
                if pk in mb['props']:
                    print("  " * depth + f"       {pk} = {mb['props'][pk]}")
    for ch in d['children']:
        print_tree(ch, depth + 1)

for f_id, d in transforms.items():
    if not d['father_id'] or d['father_id'] == '0':
        print_tree(f_id)
