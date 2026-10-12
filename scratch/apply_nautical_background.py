import os
from PIL import Image, ImageDraw, ImageFilter
import numpy as np

def generate_nautical_ground(concept_idx=2):
    """
    Generates a 1080x1920 nautical sky & ground texture overlay.
    concept_idx:
      1 -> Soft Coastal Sky & Gentle Clouds
      2 -> Distant Harbor, Rolling Hills & Faint Lighthouse Silhouette (Recommended)
      3 -> Warm Lagoon Turquoise Sea Tone & Clouds
    """
    w, h = 1080, 1920
    y_grid, x_grid = np.mgrid[0:h, 0:w]
    
    # Ground coordinate grid & vignette
    nx = (x_grid - w * 0.5) / (w * 0.5)
    dist = np.sqrt(nx**2 * 0.45 + ((y_grid - 650.0) / 450.0)**2 * 0.35)
    dist = np.clip(dist, 0.0, 1.0)
    t_vig = dist * dist * (3.0 - 2.0 * dist)
    
    # Clouds function
    def make_clouds():
        c_img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
        clouds_list = [
            (200, 240, 1.25, 0.28),
            (880, 220, 1.10, 0.25),
            (390, 370, 0.70, 0.16),
            (720, 390, 0.75, 0.18),
            (110, 380, 0.60, 0.14)
        ]
        for cx, cy, sc, alpha in clouds_list:
            layer = Image.new('RGBA', (w, h), (0, 0, 0, 0))
            draw = ImageDraw.Draw(layer)
            draw.rounded_rectangle([cx - 90*sc, cy - 14*sc, cx + 90*sc, cy + 20*sc], radius=18*sc, fill=(255, 255, 255, int(alpha * 255)))
            draw.ellipse([cx - 38*sc, cy - 44*sc, cx + 38*sc, cy + 18*sc], fill=(255, 255, 255, int(alpha * 255)))
            draw.ellipse([cx - 70*sc, cy - 32*sc, cx - 12*sc, cy + 18*sc], fill=(255, 255, 255, int(alpha * 255)))
            draw.ellipse([cx + 16*sc, cy - 28*sc, cx + 70*sc, cy + 18*sc], fill=(255, 255, 255, int(alpha * 255)))
            layer = layer.filter(ImageFilter.GaussianBlur(radius=5.0 * sc))
            c_img = Image.alpha_composite(c_img, layer)
        return np.array(c_img).astype(np.float32)
        
    def make_horizon():
        h_img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
        draw = ImageDraw.Draw(h_img)
        # Distant hills at y ~ 450
        draw.chord([-150, 360, 500, 560], start=180, end=0, fill=(185, 204, 222, 60))
        draw.chord([620, 370, 1220, 550], start=180, end=0, fill=(185, 204, 222, 55))
        # Faint lighthouse silhouette on the right headland (x=880, y=415)
        lx, ly = 880, 415
        draw.polygon([(lx - 5, ly + 25), (lx - 3, ly + 5), (lx + 3, ly + 5), (lx + 5, ly + 25)], fill=(175, 195, 215, 75))
        draw.rectangle([lx - 4, ly + 2, lx + 4, ly + 5], fill=(165, 185, 208, 90))
        draw.polygon([(lx - 5, ly + 2), (lx, ly - 3), (lx + 5, ly + 2)], fill=(160, 180, 205, 95))
        # Distant gulls
        for gx, gy, sz in [(240, 190, 8), (265, 205, 6), (720, 230, 7)]:
            draw.arc([gx - sz, gy - sz//2, gx, gy + sz//2], start=190, end=350, fill=(160, 180, 205, 80), width=1)
            draw.arc([gx, gy - sz//2, gx + sz, gy + sz//2], start=190, end=350, fill=(160, 180, 205, 80), width=1)
        h_img = h_img.filter(ImageFilter.GaussianBlur(radius=1.8))
        return np.array(h_img).astype(np.float32)

    sky_t = np.clip(y_grid / 460.0, 0.0, 1.0)
    blend_sg = np.clip((y_grid - 420.0) / 100.0, 0.0, 1.0)

    if concept_idx == 3: # Warm Lagoon
        col_sky_top = np.array([212.0, 232.0, 240.0])
        col_horizon = np.array([236.0, 242.0, 242.0])
        col_ground_center = np.array([220.0, 226.0, 230.0])
        col_ground_edge = np.array([198.0, 206.0, 210.0])
    else: # Coastal Blue / Harbor
        col_sky_top = np.array([210.0, 228.0, 242.0])
        col_horizon = np.array([230.0, 239.0, 246.0])
        col_ground_center = np.array([218.0, 224.0, 232.0])
        col_ground_edge = np.array([196.0, 204.0, 212.0])

    sky_grad = col_sky_top * (1.0 - sky_t[:, :, None]) + col_horizon * sky_t[:, :, None]
    ground_grad = col_ground_center * (1.0 - t_vig[:, :, None]) + col_ground_edge * t_vig[:, :, None]
    base_field = sky_grad * (1.0 - blend_sg[:, :, None]) + ground_grad * blend_sg[:, :, None]

    if concept_idx == 2:
        h_arr = make_horizon()
        h_alpha = h_arr[:, :, 3:4] / 255.0
        base_field = base_field * (1.0 - h_alpha) + h_arr[:, :, :3] * h_alpha

    c_arr = make_clouds()
    c_alpha = c_arr[:, :, 3:4] / 255.0
    final_field = base_field * (1.0 - c_alpha) + c_arr[:, :, :3] * c_alpha

    return final_field

def apply_to_file(file_path, concept_idx=2):
    if not os.path.exists(file_path):
        print(f"Skipping {file_path} (does not exist)")
        return
    orig_img = Image.open(file_path)
    orig_mode = orig_img.mode
    arr = np.array(orig_img.convert('RGBA')).astype(np.float32)
    h, w, _ = arr.shape
    y_grid, x_grid = np.mgrid[0:h, 0:w]

    # Ground mask:
    # Y < 1180 is 100% ground/sky
    # Y between 1180 and 1255: ground pixels have min(R,G,B) > 195 (not dark wood posts)
    is_ground = (y_grid < 1180)
    is_ground |= ((y_grid < 1255) & (arr[:, :, 0] > 195) & (arr[:, :, 1] > 195) & (arr[:, :, 2] > 195))
    is_ground &= (y_grid < 1255)

    mask_img = Image.fromarray((is_ground.astype(np.uint8) * 255))
    mask_blur = np.array(mask_img.filter(ImageFilter.GaussianBlur(radius=0.75))).astype(np.float32) / 255.0

    nautical_field = generate_nautical_ground(concept_idx)

    out_arr = arr.copy()
    for c in range(3):
        out_arr[:, :, c] = arr[:, :, c] * (1.0 - mask_blur) + nautical_field[:, :, c] * mask_blur

    # Add subtle embedded dock berth pockets between the pier posts
    is_4slot = '4Slots' in file_path
    bays = [185, 327, 469, 611, 753, 895] if is_4slot else [193, 367, 541, 715, 889]
    slip_w = 118 if is_4slot else 124

    pocket_layer = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    p_draw = ImageDraw.Draw(pocket_layer)
    for bx in bays:
        # Subtle recessed mooring shadow under dock beam
        p_draw.rounded_rectangle([bx - slip_w//2, 1255, bx + slip_w//2, 1276], radius=4, fill=(3, 16, 42, 65))
        p_draw.rectangle([bx - slip_w//2 + 2, 1255, bx + slip_w//2 - 2, 1264], fill=(2, 10, 26, 80))
    pocket_layer = pocket_layer.filter(ImageFilter.GaussianBlur(radius=1.5))
    p_arr = np.array(pocket_layer).astype(np.float32)
    p_alpha = p_arr[:, :, 3:4] / 255.0

    for c in range(3):
        out_arr[:, :, c] = out_arr[:, :, c] * (1.0 - p_alpha[:, :, 0]) + p_arr[:, :, c] * p_alpha[:, :, 0]

    out_arr_uint8 = np.clip(out_arr, 0, 255).astype(np.uint8)
    if orig_mode == 'RGB':
        out_img = Image.fromarray(out_arr_uint8[:, :, :3], mode='RGB')
    else:
        out_img = Image.fromarray(out_arr_uint8, mode='RGBA')

    out_img.save(file_path)
    print(f"Successfully applied Concept {concept_idx} with embedded berth pockets to {file_path}")

if __name__ == '__main__':
    import sys
    c_idx = 2
    if len(sys.argv) > 1:
        c_idx = int(sys.argv[1])
        
    targets = [
        'Assets/Kenney/BeachBackground_3Slots.png',
        'Assets/Kenney/BeachBackground_4Slots.png',
        'Assets/Kenney/BeachBackground_Clean.png'
    ]
    for t in targets:
        apply_to_file(t, c_idx)
