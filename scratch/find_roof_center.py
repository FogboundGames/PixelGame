import struct, zlib

with open(r'Assets/Kenney/kenney_watercraft-pack/Models/FBX format/boat-house-a.fbx', 'rb') as f:
    buf = f.read()

# In binary FBX, find 'Vertices' property name
pos = 0
while True:
    idx = buf.find(b'Vertices', pos)
    if idx == -1: break
    pos = idx + 8
    # check property header: length (uint32 or uint64), encoding (uint32), comp_len (uint32 or uint64)
    # usually after property name there might be null or tag
    # Let's inspect 30 bytes
    chunk = buf[idx:idx+40]
    print("Found at", idx, chunk[:25])
    # FBX 7.4+: null byte then tag 'd' (double)
    tag_idx = buf.find(b'd', idx)
    if tag_idx != -1 and tag_idx - idx < 20:
        arr_len, encoding, comp_len = struct.unpack('<IIQ', buf[tag_idx+1:tag_idx+17])
        print("Header 64-bit:", arr_len, encoding, comp_len)
        data = buf[tag_idx+17:tag_idx+17+comp_len]
        if encoding == 1:
            data = zlib.decompress(data)
        doubles = struct.unpack(f'<{arr_len}d', data)
        verts = [(doubles[i], doubles[i+1], doubles[i+2]) for i in range(0, len(doubles), 3)]
        print(f"Total vertices: {len(verts)}")
        print(f"X range: {min(v[0] for v in verts):.3f} to {max(v[0] for v in verts):.3f}")
        print(f"Y range: {min(v[1] for v in verts):.3f} to {max(v[1] for v in verts):.3f}")
        print(f"Z range: {min(v[2] for v in verts):.3f} to {max(v[2] for v in verts):.3f}")
        
        # highest vertices
        max_y = max(v[1] for v in verts)
        roof_verts = [v for v in verts if v[1] >= max_y - 0.15]
        print(f"Roof height: {max_y:.3f}")
        print(f"Roof X: {min(v[0] for v in roof_verts):.3f} to {max(v[0] for v in roof_verts):.3f} (Center X: {(min(v[0] for v in roof_verts)+max(v[0] for v in roof_verts))/2:.3f})")
        print(f"Roof Z: {min(v[2] for v in roof_verts):.3f} to {max(v[2] for v in roof_verts):.3f} (Center Z: {(min(v[2] for v in roof_verts)+max(v[2] for v in roof_verts))/2:.3f})")
        break
