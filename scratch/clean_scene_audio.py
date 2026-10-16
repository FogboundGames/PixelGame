import re
import shutil

scene_path = r"Assets\Scenes\Gemi.unity"
backup_path = r"Assets\Scenes\Gemi.unity.bak_audio_cleanup"

shutil.copyfile(scene_path, backup_path)
print(f"Created backup at {backup_path}")

with open(scene_path, "r", encoding="utf-8") as f:
    content = f.read()

# We keep these 4 AudioSources:
kept_audio_ids = {"1699816081", "1699816080", "1699816079", "1699816086"}
kept_other_ids = {"1699816083", "1699816082"} # Transform and HypercasualFeedbackManager

# Find GameObject 1699816078 block
go_pattern = re.compile(r"(--- !u!1 &1699816078\nGameObject:.*?\n  m_Component:\n)(.*?)(\n  m_Layer:)", re.DOTALL)
match = go_pattern.search(content)
if not match:
    print("ERROR: Could not find GameObject 1699816078")
    exit(1)

component_lines = match.group(2).split("\n")
removed_ids = set()
kept_component_lines = []

for line in component_lines:
    m = re.search(r"fileID: (\d+)", line)
    if m:
        fid = m.group(1)
        if fid in kept_other_ids or fid in kept_audio_ids:
            kept_component_lines.append(line)
        else:
            removed_ids.add(fid)
    else:
        if line.strip():
            kept_component_lines.append(line)

print(f"Components to keep: {len(kept_component_lines)}")
print(f"Component AudioSources to remove: {len(removed_ids)}")

new_go_block = match.group(1) + "\n".join(kept_component_lines) + match.group(3)
content = content[:match.start()] + new_go_block + content[match.end():]

# Now remove the YAML blocks for the removed AudioSources
# Each block starts with '--- !u!82 &<id>\n' and ends before the next '--- !u!'
print(f"Removing {len(removed_ids)} AudioSource YAML blocks...")
blocks = content.split("--- !u!")
print(f"Total YAML blocks: {len(blocks)}")

filtered_blocks = [blocks[0]] # header before first block
removed_block_count = 0

for block in blocks[1:]:
    # First line has type and id, e.g. "82 &1699816085\n"
    first_line = block.split("\n", 1)[0]
    if "&" in first_line:
        block_id = first_line.split("&")[1].strip()
        if block_id in removed_ids:
            removed_block_count += 1
            continue
    filtered_blocks.append(block)

print(f"Actually removed {removed_block_count} blocks from YAML.")

new_content = "--- !u!".join(filtered_blocks)

with open(scene_path, "w", encoding="utf-8") as f:
    f.write(new_content)

print(f"Successfully updated {scene_path}!")
print(f"Original size: {len(content)} chars, New size: {len(new_content)} chars")
