import re

with open('Assets/Models/Sailor_Runner/Sailor_Runner.fbx', 'rb') as f:
    text = f.read()

# find printable ascii strings of length >= 4
strings = re.findall(rb'[A-Za-z0-9_]{4,}', text)
names = set()
for s in strings:
    s_str = s.decode('latin1')
    if any(k in s_str for k in ['Body', 'Head', 'Torso', 'Arm', 'Leg', 'Cap', 'Hat', 'Mesh', 'Pompom', 'Band']):
        names.add(s_str)
print('Mesh/Part names:', sorted(list(names)))
