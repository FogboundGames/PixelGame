import os
import shutil
from PIL import Image, ImageFilter
import numpy as np

def update_background(img_path):
    orig_img = Image.open(img_path)
    orig_mode = orig_img.mode
    arr = np.array(orig_img.convert('RGBA')).astype(np.float32)
    h, w, _ = arr.shape

    # Coordinate grids
    y_grid, x_grid = np.mgrid[0:h, 0:w]
    nx = (x_grid - w * 0.5) / (w * 0.5)
    ny = y_grid / float(h)

    # Soft radial & vertical vignette for studio floor depth
    dist = np.sqrt(nx**2 * 0.45 + ((ny - 0.32) / 0.52)**2 * 0.40)
    dist = np.clip(dist, 0.0, 1.0)
    # Smooth Hermite curve
    t = dist * dist * (3.0 - 2.0 * dist)

    # Balanced modern cool studio gray palette:
    # Center/Top: #D6DAE1 (214, 218, 225) - crisp, elegant, high-contrast against white cubes
    # Edge/Bottom: #C2C8D0 (194, 200, 208) - gentle vignette for depth
    col_center = np.array([214.0, 218.0, 225.0])
    col_edge = np.array([194.0, 200.0, 208.0])
    gray_ground = col_center * (1.0 - t[:, :, None]) + col_edge * t[:, :, None]

    # Ground detection:
    # 1. Everything above the posts (Y < 1180) is 100% ground
    is_sand = (y_grid < 1180)
    # 2. In Y between 1180 and 1255, detect ground pixels (avoiding dark wood posts)
    # Ground pixels in current images have min(R,G,B) > 205, wood posts have min(R,G,B) < 185
    is_sand |= ((y_grid < 1255) & (arr[:, :, 0] > 205) & (arr[:, :, 1] > 205) & (arr[:, :, 2] > 205))
    is_sand &= (y_grid < 1255)

    # Anti-aliasing with 0.75px Gaussian blur
    mask_img = Image.fromarray((is_sand.astype(np.uint8) * 255))
    mask_blur = np.array(mask_img.filter(ImageFilter.GaussianBlur(radius=0.75))).astype(np.float32) / 255.0

    out_arr = arr.copy()
    for c in range(3):
        out_arr[:, :, c] = arr[:, :, c] * (1.0 - mask_blur) + gray_ground[:, :, c] * mask_blur

    out_arr_uint8 = np.clip(out_arr, 0, 255).astype(np.uint8)
    if orig_mode == 'RGB':
        out_img = Image.fromarray(out_arr_uint8[:, :, :3], mode='RGB')
    else:
        out_img = Image.fromarray(out_arr_uint8, mode='RGBA')

    out_img.save(img_path)
    print(f"Successfully updated {img_path} with balanced darker studio gray ground (mode: {orig_mode})")

if __name__ == '__main__':
    targets = [
        'Assets/Kenney/BeachBackground_3Slots.png',
        'Assets/Kenney/BeachBackground_4Slots.png',
        'Assets/Kenney/BeachBackground_Clean.png'
    ]
    for target in targets:
        if os.path.exists(target):
            update_background(target)
