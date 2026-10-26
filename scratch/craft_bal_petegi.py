import numpy as np
from PIL import Image

def craft_honeycomb():
    # Load quantized draft
    im = Image.open('scratch/quantized_32.png')
    arr = np.array(im)
    
    # Target 5-color ASMR palette:
    # 0: Transparent (0, 0, 0, 0)
    # 1: Dark Amber / Outlines & Bee Stripes (68, 36, 12, 255)  # #44240C
    # 2: Honeycomb Frame / Amber Rim        (148, 80, 20, 255)  # #945014
    # 3: Deep Liquid Honey                  (212, 126, 16, 255) # #D47E10
    # 4: Bright Golden Honey                (252, 188, 28, 255) # #FCBC1C
    # 5: Glossy Specular White & Wings      (255, 255, 255, 255)# #FFFFFF
    
    C_TRANS = (0, 0, 0, 0)
    C_DARK  = (68, 36, 12, 255)
    C_RIM   = (148, 80, 20, 255)
    C_DHONEY= (212, 126, 16, 255)
    C_BHONEY= (252, 188, 28, 255)
    C_WHITE = (255, 255, 255, 255)
    
    # Original draft centers were:
    # Color 0: [182, 105, 16] -> D_HONEY (or RIM)
    # Color 1: [238, 172, 32] -> B_HONEY
    # Color 2: [73, 49, 17]   -> DARK
    # Color 3: [208, 136, 22] -> D_HONEY
    # Color 4: [132, 87, 31]  -> RIM
    # Color 5: [166, 164, 164] -> WHITE
    
    orig_colors = {
        (0, 0, 0, 0): C_TRANS,
        (182, 105, 16, 255): C_DHONEY,
        (208, 136, 22, 255): C_DHONEY,
        (238, 172, 32, 255): C_BHONEY,
        (132, 87, 31, 255): C_RIM,
        (73, 49, 17, 255): C_DARK,
        (166, 164, 164, 255): C_WHITE,
    }
    
    h, w = arr.shape[:2]
    out = np.zeros((h, w, 4), dtype=np.uint8)
    
    for y in range(h):
        for x in range(w):
            c = tuple(arr[y, x])
            out[y, x] = orig_colors.get(c, C_TRANS)
            
    # Now let's add juicy white specular glints (ASMR wet honey gloss)
    # In each cell, place a tiny white shine highlight at the upper curved edge:
    highlights = [
        # Bee wings & eyes
        (0, 18), (0, 19), (1, 18), (1, 19), (1, 20), (2, 19), (2, 20),
        # Honeycomb cell glints
        (7, 8), (7, 9), (8, 9),      # Top left cell
        (6, 15), (7, 15),            # Top middle cell
        (7, 22), (8, 22),            # Top right cell
        (11, 6), (12, 6),            # Mid left cell
        (12, 13), (13, 13),          # Center cell
        (12, 20), (13, 20),          # Mid right cell
        (17, 7), (18, 7),            # Lower left cell
        (17, 14), (18, 14),          # Lower center cell
        (17, 21), (18, 21),          # Lower right cell
        # Dripping honey droplet glints (shiny liquid highlights!)
        (24, 6),                     # Left drip glint
        (27, 11), (28, 11),          # Big middle drip glint
        (24, 17),                    # Small drip glint
        (28, 22), (29, 22), (30, 22) # Long right drip bulb glint
    ]
    
    for (hy, hx) in highlights:
        if 0 <= hy < h and 0 <= hx < w:
            if out[hy, hx, 3] > 0: # only on existing visible pixels
                out[hy, hx] = C_WHITE
                
    # Bee face detail (cute little smile / eye):
    # Bee sits at rows 1-4, cols 17-21
    # Little black eye dot:
    out[3, 20] = C_DARK
    # Little yellow bee head:
    out[3, 18] = C_BHONEY
    out[3, 19] = C_BHONEY
    out[4, 19] = C_DARK # bee stripe
    out[4, 20] = C_BHONEY
    
    # Save results
    result_img = Image.fromarray(out, 'RGBA')
    result_img.save('scratch/crafted_bal_petegi.png')
    result_img.resize((512, 512), Image.Resampling.NEAREST).save('scratch/crafted_bal_petegi_512.png')
    
    print('Color distribution in crafted Bal Peteği:')
    colors, counts = np.unique(out.reshape(-1, 4), axis=0, return_counts=True)
    for c, cnt in sorted(zip(colors, counts), key=lambda x: -x[1]):
        print(f'RGBA: {tuple(c)} -> {cnt} pixels')

if __name__ == '__main__':
    craft_honeycomb()
