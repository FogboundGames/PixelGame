with open('Assets/Models/Sailor_Runner/Sailor_Runner.fbx', 'rb') as f:
    data = f.read()

conn_idx = data.find(b'Connections')
if conn_idx != -1:
    part = data[conn_idx:conn_idx+4000]
    print(''.join(chr(b) if 32 <= b < 127 else '.' for b in part[:1000]))
else:
    # In binary FBX, 'Connections' is a node name
    pos = 0
    while True:
        pos = data.find(b'Connections', pos + 1)
        if pos == -1: break
        print('Found Connections at', pos)
        part = data[pos:pos+1000]
        print(''.join(chr(b) if 32 <= b < 127 else '.' for b in part[:500]))
