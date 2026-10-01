from PIL import Image
import numpy as np

img = Image.open('scratch/gameplay_view_9_16.png').convert('RGB')
arr = np.array(img)

# Look for pink pixels: high R, moderate/low G, moderate B
# In lifebuoy pink: R > 180, B > 120, R > G + 40
r = arr[:, :, 0].astype(int)
g = arr[:, :, 1].astype(int)
b = arr[:, :, 2].astype(int)

pink_mask = (r > 180) & (b > 100) & (r - g > 40)

# Sum across x for each y
y_counts = np.sum(pink_mask, axis=1)
for y, count in enumerate(y_counts):
    if count > 100 and y > 400: # Below top UI
        print(f"y={y}: count={count}")
