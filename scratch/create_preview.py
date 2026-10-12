from PIL import Image
import numpy as np

# Load gameplay view and new background
gameplay = Image.open('scratch/gameplay_view_9_16.png').convert('RGBA')
bg_new = Image.open('Assets/Kenney/BeachBackground_3Slots.png').convert('RGBA')

# In gameplay_view_9_16, let's identify the sand pixels:
# The sand pixels in gameplay view are in the background where original sand was.
# Specifically, in the upper area (Y < 1240) not covered by UI or OtCerceve or cubes:
gp_arr = np.array(gameplay).astype(np.float32)
bg_arr = np.array(bg_new).astype(np.float32)

# Sand in gameplay is warm yellow: R > 200, G > 160, B > 100, R - B > 25
# (Notice: ship white cubes are neutral (R~255, G~255, B~255), blue cubes are B > R, red cubes are R >> G, leaves are G > R)
# Sand is uniquely warm yellow/tan!
is_sand = (
    (gp_arr[:, :, 0] > 195) & 
    (gp_arr[:, :, 1] > 165) & 
    (gp_arr[:, :, 2] > 95) & 
    (gp_arr[:, :, 0] > gp_arr[:, :, 2] + 25) &
    (abs(gp_arr[:, :, 0] - gp_arr[:, :, 1]) < 55)
)

# And Y < 1250 (above pier deck)
y_indices = np.arange(gp_arr.shape[0])[:, None]
is_sand &= (y_indices < 1245)

# Also there's shadows under cubes on the sand: where sand is slightly darker (R: 160..210, G: 130..180, B: 80..130)
# We can blend cleanly
out_arr = gp_arr.copy()

# Replace sand with new background
out_arr[is_sand, :3] = bg_arr[is_sand, :3]

preview = Image.fromarray(np.clip(out_arr, 0, 255).astype(np.uint8))
preview.save('scratch/new_gameplay_preview.png')
print('Saved scratch/new_gameplay_preview.png')
