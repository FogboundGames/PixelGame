import re

scene_path = r"Assets\Scenes\Gemi.unity"

with open(scene_path, "r", encoding="utf-8") as f:
    lines = f.readlines()

print(f"Total lines: {len(lines)}")

# Find GameObject 1699816078
go_start = -1
for i, line in enumerate(lines):
    if line.startswith("--- !u!1 &1699816078"):
        go_start = i
        break

print(f"GameObject 1699816078 found at line {go_start + 1}")

# Count components in GameObject
components = []
for i in range(go_start, min(go_start + 500, len(lines))):
    if lines[i].startswith("--- !u!") and i > go_start:
        break
    m = re.search(r"fileID: (\d+)", lines[i])
    if m and "m_Component:" not in lines[i]:
        components.append(m.group(1))

print(f"Number of component references in GameObject: {len(components)}")
print(f"First 10 component fileIDs: {components[:10]}")

# Count AudioSources for this GameObject
audio_sources = []
current_header = None
current_id = None
for i, line in enumerate(lines):
    if line.startswith("--- !u!82 &"):
        current_id = line.strip().split("&")[1]
    elif current_id and "m_GameObject: {fileID: 1699816078}" in line:
        audio_sources.append(current_id)
        current_id = None
    elif line.startswith("--- !u!") and not line.startswith("--- !u!82 &"):
        current_id = None

print(f"Number of AudioSource blocks matching GameObject 1699816078: {len(audio_sources)}")
