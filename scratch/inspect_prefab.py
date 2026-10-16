import re

with open(r"Assets\Resources\Sailor_Runner.prefab", "r", encoding="utf-8") as f:
    text = f.read()

names = re.findall(r"m_Name:\s*(.+)", text)
print("Names in prefab:", set(names))

# Find all GameObjects
gos = re.findall(r"--- !u!1 &(\d+)\nGameObject:.*?\n  m_Name:\s*([^\n\r]+)", text, re.DOTALL)
go_dict = {gid: gname.strip() for gid, gname in gos}

# Find all Renderers (23 is MeshRenderer, 137 is SkinnedMeshRenderer)
renderers = re.findall(r"--- !u!(?:23|137) &(\d+)\n(.*?)(?=\n--- !u!|\Z)", text, re.DOTALL)
print(f"Total renderers: {len(renderers)}")
for rid, rbody in renderers:
    goid_m = re.search(r"m_GameObject: {fileID: (\d+)}", rbody)
    goid = goid_m.group(1) if goid_m else "unknown"
    goname = go_dict.get(goid, goid)
    mats = re.findall(r"fileID: \d+, guid: ([0-9a-f]+)", rbody)
    print(f"Renderer on '{goname}': mats count={len(mats)}")
