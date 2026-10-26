import os
from PIL import Image

def build_honeycomb_pixel_art():
    # 32x32 grid
    # Colors:
    # . : Transparent
    # B : Deep Amber / Border (148, 68, 16)
    # D : Dark Amber Honey (217, 119, 6)
    # G : Golden Honey (250, 180, 20)
    # L : Light Honey / Cream (254, 240, 138)
    # W : Pure White / Gloss & Wings (255, 255, 255)
    # K : Warm Dark Charcoal / Bee stripes & eyes (45, 30, 22)
    # P : Honeycomb Drip Droplet Accent (255, 204, 0)
    
    palette = {
        '.': (0, 0, 0, 0),
        'B': (148, 68, 16, 255),    # Frame / Comb Rim
        'D': (217, 119, 6, 255),    # Deep Honey / Shadow
        'G': (250, 180, 20, 255),   # Golden Honey Body
        'L': (254, 235, 120, 255),  # Soft Cream Highlight
        'W': (255, 255, 255, 255),  # Specular Gloss / Wings
        'K': (45, 30, 22, 255),     # Bee Charcoal
    }

    # 32 rows of 32 characters
    # Designing a glorious honeycomb cluster with dripping honey and a cute little bee
    art = [
        # 0
        "................................",
        # 1
        "................................",
        # 2
        ".....................WW.........",
        # 3
        "........BBBB........WKKW........",
        # 4
        ".......BDDDDB......WKGGKW.......",
        # 5
        "......BDDGGGGDB...BBDKGKDB......",
        # 6
        ".....BDGGLLGGGB..BDDDGGDDB......",
        # 7
        "....BDGGWWLLGGGB.BDGGLLGGDB.....",
        # 8
        "....BDGGLGGGGGDDBDGGWWLLGGDB....",
        # 9
        "....BDGGGGGGGDDBDGGGLGGGGGDB....",
        # 10
        "....BBDDDGDDDB..BDDGGGGGGGDDB...",
        # 11
        "....BDGGGGGGDB...BBDDDGDDDBB....",
        # 12
        "...BDGGLLGGGGDDB.BDGGGGGGDB.....",
        # 13
        "...BDGWWLLGGGGDBBDGGLLGGGGDDB...",
        # 14
        "...BDGGLLGGGGGDDBDGWLLGGGGGDDB..",
        # 15
        "...BDGGGGGGGGGDDBDGGLLGGGGGDDB..",
        # 16
        "...BBDDDGGDDDDB.BDDGGGGGGGGGDB..",
        # 17
        "....BDGGGGGGDB...BBDDGGDDDDDDB..",
        # 18
        "....BDGGLLGGDB..BDGGGGGGGGGDB...",
        # 19
        "....BDGWWLGGDB.BDGGLLGGGGGGDB...",
        # 20
        "....BDGGLLGGDBBDGWWLLGGGGGGDDB..",
        # 21
        "....BDGGGGGGDDDGGLGGDGGGGGGGDDB.",
        # 22
        "....BDDDGGGGGGGGGGGDDBDGGDDDDDDB",
        # 23
        ".....BBDGGGGGGGGGGDB.BDDGGGGDDDB",
        # 24
        "......BDGGGGGGGGGDB...BDGGGGGDDB",
        # 25
        "......BDGGWLLGGGDDB....BDGGGGDB.",
        # 26
        ".......BDGWWLGGDB.......BDDDDDB.",
        # 27
        ".......BDGGLLGGDB........BBDDB..",
        # 28
        "........BDGGGGDB..........BB....",
        # 29
        ".........BDDDDB.................",
        # 30
        "..........BDB...................",
        # 31
        "................................",
    ]

    img = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    for y, row in enumerate(art):
        for x, ch in enumerate(row):
            img.putpixel((x, y), palette[ch])

    return img, art

if __name__ == "__main__":
    img, art = build_honeycomb_pixel_art()
    os.makedirs("scratch", exist_ok=True)
    img.save("scratch/test_honeycomb_32.png")
    # Upscale 16x for preview
    preview = img.resize((512, 512), Image.NEAREST)
    preview.save("scratch/preview_honeycomb_512.png")
    print("Saved preview to scratch/preview_honeycomb_512.png")
