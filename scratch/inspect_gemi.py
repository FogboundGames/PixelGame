import re

with open('Assets/Scenes/Gemi.unity', 'r', encoding='utf-8', errors='ignore') as f:
    text = f.read()

blocks = text.split('--- !u!')
for b in blocks:
    if 'GameObject:' in b:
        name_m = re.search(r'm_Name: (.*)', b)
        name = name_m.group(1) if name_m else 'Unknown'
        id_m = re.search(r'&(\d+)', b)
        gid = id_m.group(1) if id_m else '?'
        print(f'GameObject: {name} (ID: {gid})')
        comp_ids = re.findall(r'- component: \{fileID: (\d+)\}', b)
        for cid in comp_ids:
            cb_m = re.search(r'--- !u!(\d+) &' + cid + r'\n(.*?)(?=\n--- !u|\Z)', text, re.DOTALL)
            if cb_m:
                ctype = cb_m.group(1)
                cbody = cb_m.group(2)
                if ctype in ['4', '224']:
                    pos = re.search(r'm_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^\}]+)\}', cbody)
                    euler = re.search(r'm_LocalEulerAnglesHint: \{x: ([^,]+), y: ([^,]+), z: ([^\}]+)\}', cbody)
                    scale = re.search(r'm_LocalScale: \{x: ([^,]+), y: ([^,]+), z: ([^\}]+)\}', cbody)
                    father = re.search(r'm_Father: \{fileID: (\d+)\}', cbody)
                    p_str = f"({pos.group(1)}, {pos.group(2)}, {pos.group(3)})" if pos else "?"
                    e_str = f"({euler.group(1)}, {euler.group(2)}, {euler.group(3)})" if euler else "?"
                    s_str = f"({scale.group(1)}, {scale.group(2)}, {scale.group(3)})" if scale else "?"
                    f_str = father.group(1) if father else '0'
                    print(f'  [Transform] father={f_str}, pos={p_str}, euler={e_str}, scale={s_str}')
                elif ctype == '20':
                    fov = re.search(r'field of view: (.*)', cbody)
                    ortho = re.search(r'orthographic: (.*)', cbody)
                    fov_str = fov.group(1) if fov else "?"
                    ortho_str = ortho.group(1) if ortho else "?"
                    print(f'  [Camera] fov={fov_str}, ortho={ortho_str}')
                elif ctype == '114':
                    script_m = re.search(r'm_Script: \{fileID: [^,]+, guid: ([^,]+)', cbody)
                    tex_m = re.search(r'm_Texture: \{fileID: [^,]+, guid: ([^,]+)', cbody)
                    spr_m = re.search(r'm_Sprite: \{fileID: [^,]+, guid: ([^,]+)', cbody)
                    s_str = script_m.group(1) if script_m else "?"
                    t_str = tex_m.group(1) if tex_m else "None"
                    sp_str = spr_m.group(1) if spr_m else "None"
                    print(f'  [MonoBehaviour] script={s_str}, tex={t_str}, spr={sp_str}')
                elif ctype == '223':
                    mode = re.search(r'm_RenderMode: (.*)', cbody)
                    m_str = mode.group(1) if mode else "?"
                    print(f'  [Canvas] renderMode={m_str}')
                elif ctype == '33':
                    mesh = re.search(r'm_Mesh: \{fileID: [^,]+, guid: ([^,]+)', cbody)
                    print(f'  [MeshFilter] meshGuid={mesh.group(1) if mesh else "builtin"}')
