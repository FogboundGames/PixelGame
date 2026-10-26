import numpy as np
from PIL import Image

def build_exact_puzzle():
    grid = np.zeros((32, 32), dtype=int)
    
    # 16 piece IDs:
    # (r, c) = r * 4 + c
    for r in range(4):
        for c in range(4):
            grid[r*8 : (r+1)*8, c*8 : (c+1)*8] = r * 4 + c
            
    # Connector function with rounded bulb
    def apply_h_tab(r, c, owner_is_left):
        border_x = (c + 1) * 8
        cy = r * 8 + 4
        owner = r * 4 + c if owner_is_left else r * 4 + (c + 1)
        
        if owner_is_left:
            # Tab sticks RIGHT into (c+1)
            # Neck
            grid[cy-1 : cy+1, border_x : border_x+1] = owner
            # Bulb
            grid[cy-2 : cy+2, border_x+1 : border_x+3] = owner
            # Rounded tip
            grid[cy-1 : cy+1, border_x+3 : border_x+4] = owner
        else:
            # Tab sticks LEFT into c
            # Neck
            grid[cy-1 : cy+1, border_x-1 : border_x] = owner
            # Bulb
            grid[cy-2 : cy+2, border_x-3 : border_x-1] = owner
            # Rounded tip
            grid[cy-1 : cy+1, border_x-4 : border_x-3] = owner

    def apply_v_tab(r, c, owner_is_top):
        border_y = (r + 1) * 8
        cx = c * 8 + 4
        owner = r * 4 + c if owner_is_top else (r + 1) * 4 + c
        
        if owner_is_top:
            # Tab sticks DOWN into r+1
            # Neck
            grid[border_y : border_y+1, cx-1 : cx+1] = owner
            # Bulb
            grid[border_y+1 : border_y+3, cx-2 : cx+2] = owner
            # Rounded tip
            grid[border_y+3 : border_y+4, cx-1 : cx+1] = owner
        else:
            # Tab sticks UP into r
            # Neck
            grid[border_y-1 : border_y, cx-1 : cx+1] = owner
            # Bulb
            grid[border_y-3 : border_y-1, cx-2 : cx+2] = owner
            # Rounded tip
            grid[border_y-4 : border_y-3, cx-1 : cx+1] = owner

    # Exact connectors from user reference:
    # Row 0 Horizontal:
    apply_h_tab(0, 0, owner_is_left=False) # Orange into White
    apply_h_tab(0, 1, owner_is_left=True)  # Orange into Red
    apply_h_tab(0, 2, owner_is_left=True)  # Red into Purple
    
    # Row 1 Horizontal:
    apply_h_tab(1, 0, owner_is_left=True)  # Green into Purple
    apply_h_tab(1, 1, owner_is_left=False) # White into Purple
    apply_h_tab(1, 2, owner_is_left=True)  # White into Blue
    
    # Row 2 Horizontal:
    apply_h_tab(2, 0, owner_is_left=False) # Brown into Pink
    apply_h_tab(2, 1, owner_is_left=True)  # Brown into Yellow
    apply_h_tab(2, 2, owner_is_left=False) # Cyan into Yellow
    
    # Row 3 Horizontal:
    apply_h_tab(3, 0, owner_is_left=True)  # Cyan into Blue
    apply_h_tab(3, 1, owner_is_left=False) # Orange into Blue
    apply_h_tab(3, 2, owner_is_left=True)  # Orange into Pink

    # Row 0-1 Vertical:
    apply_v_tab(0, 0, owner_is_top=True)   # White into Green
    apply_v_tab(0, 1, owner_is_top=False)  # Purple into Orange
    apply_v_tab(0, 2, owner_is_top=True)   # Red into White
    apply_v_tab(0, 3, owner_is_top=False)  # Blue into Purple
    
    # Row 1-2 Vertical:
    apply_v_tab(1, 0, owner_is_top=False)  # Pink into Green
    apply_v_tab(1, 1, owner_is_top=True)   # Purple into Brown
    apply_v_tab(1, 2, owner_is_top=False)  # Yellow into White
    apply_v_tab(1, 3, owner_is_top=True)   # Blue into Cyan
    
    # Row 2-3 Vertical:
    apply_v_tab(2, 0, owner_is_top=True)   # Pink into Cyan
    apply_v_tab(2, 1, owner_is_top=False)  # Blue into Brown
    apply_v_tab(2, 2, owner_is_top=True)   # Yellow into Orange
    apply_v_tab(2, 3, owner_is_top=False)  # Pink into Cyan

    # Palette mapping matching the image:
    # 10 colors
    C_WHITE  = (252, 252, 252, 255)
    C_ORANGE = (248, 116, 24,  255)
    C_RED    = (224, 46,  32,  255)
    C_PURPLE = (126, 38,  162, 255)
    C_GREEN  = (28,  168, 76,  255)
    C_BLUE   = (22,  108, 224, 255)
    C_PINK   = (244, 104, 180, 255)
    C_BROWN  = (166, 114, 58,  255)
    C_YELLOW = (254, 194, 12,  255)
    C_CYAN   = (22,  208, 236, 255)
    
    piece_to_color = {
        0:  C_WHITE,
        1:  C_ORANGE,
        2:  C_RED,
        3:  C_PURPLE,
        
        4:  C_GREEN,
        5:  C_PURPLE,
        6:  C_WHITE,
        7:  C_BLUE,
        
        8:  C_PINK,
        9:  C_BROWN,
        10: C_YELLOW,
        11: C_CYAN,
        
        12: C_CYAN,
        13: C_BLUE,
        14: C_ORANGE,
        15: C_PINK,
    }

    out_img = Image.new('RGBA', (32, 32))
    for y in range(32):
        for x in range(32):
            pid = grid[y, x]
            out_img.putpixel((x, y), piece_to_color[pid])
            
    return out_img, grid, piece_to_color

if __name__ == '__main__':
    img, grid, _ = build_exact_puzzle()
    img.save('scratch/puzzle_32.png')
    img.resize((512, 512), Image.NEAREST).save('scratch/puzzle_512.png')
    print('Saved puzzle_32.png and puzzle_512.png')
