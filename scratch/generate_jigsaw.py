import numpy as np
from PIL import Image

def generate_jigsaw_puzzle():
    # 32x32 grid, 4x4 pieces, each base cell is 8x8
    grid = np.zeros((32, 32), dtype=int)
    
    # 16 piece IDs: 0 to 15 (row * 4 + col)
    # Colors matching user reference image:
    colors = {
        0:  (250, 250, 250, 255),  # (0,0) White
        1:  (245, 120, 25,  255),  # (0,1) Orange
        2:  (225, 45,  30,  255),  # (0,2) Red
        3:  (125, 35,  155, 255),  # (0,3) Deep Purple
        
        4:  (25,  165, 75,  255),  # (1,0) Green
        5:  (135, 45,  170, 255),  # (1,1) Medium Purple
        6:  (250, 250, 250, 255),  # (1,2) White
        7:  (20,  110, 225, 255),  # (1,3) Royal Blue
        
        8:  (245, 105, 180, 255),  # (2,0) Pink
        9:  (165, 115, 60,  255),  # (2,1) Ochre Brown
        10: (255, 195, 10,  255),  # (2,2) Bright Yellow
        11: (20,  210, 235, 255),  # (2,3) Bright Cyan
        
        12: (20,  210, 235, 255),  # (3,0) Bright Cyan
        13: (15,  85,  205, 255),  # (3,1) Dark Blue
        14: (245, 115, 25,  255),  # (3,2) Orange
        15: (240, 90,  170, 255),  # (3,3) Magenta Pink
    }
    
    # First, fill each base 8x8 cell with its piece ID
    for r in range(4):
        for c in range(4):
            pid = r * 4 + c
            grid[r*8 : (r+1)*8, c*8 : (c+1)*8] = pid
            
    # Connector helper:
    # A tab connects two pieces across an edge.
    # owner_id: which piece owns the tab (the tab sticks out from owner into target).
    # Horizontal edge: between (r, c) and (r, c+1). Border is at x = (c+1)*8.
    # Vertical range for horizontal edge: y_center = r*8 + 4.
    # y ranges from r*8+2 to r*8+5.
    
    def apply_h_tab(r, c, owner_is_left):
        border_x = (c + 1) * 8
        cy = r * 8 + 4
        owner = r * 4 + c if owner_is_left else r * 4 + (c + 1)
        
        if owner_is_left:
            # Tab sticks RIGHT into (c+1)
            # Neck: x = border_x, border_x + 1; y = cy-1, cy
            grid[cy-1 : cy+1, border_x : border_x+1] = owner
            # Bulb: x = border_x+1, border_x+2; y = cy-2, cy-1, cy, cy+1
            grid[cy-2 : cy+2, border_x+1 : border_x+3] = owner
        else:
            # Tab sticks LEFT into c
            # Neck: x = border_x-1; y = cy-1, cy
            grid[cy-1 : cy+1, border_x-1 : border_x] = owner
            # Bulb: x = border_x-3, border_x-2; y = cy-2, cy-1, cy, cy+1
            grid[cy-2 : cy+2, border_x-3 : border_x-1] = owner

    def apply_v_tab(r, c, owner_is_top):
        border_y = (r + 1) * 8
        cx = c * 8 + 4
        owner = r * 4 + c if owner_is_top else (r + 1) * 4 + c
        
        if owner_is_top:
            # Tab sticks DOWN into r+1
            # Neck: y = border_y; x = cx-1, cx
            grid[border_y : border_y+1, cx-1 : cx+1] = owner
            # Bulb: y = border_y+1, border_y+2; x = cx-2, cx-1, cx, cx+1
            grid[border_y+1 : border_y+3, cx-2 : cx+2] = owner
        else:
            # Tab sticks UP into r
            # Neck: y = border_y-1; x = cx-1, cx
            grid[border_y-1 : border_y, cx-1 : cx+1] = owner
            # Bulb: y = border_y-3, border_y-2; x = cx-2, cx-1, cx, cx+1
            grid[border_y-3 : border_y-1, cx-2 : cx+2] = owner

    # Horizontal connectors:
    # (0,0)-(0,1): (0,1) Orange has tab pointing LEFT into White
    apply_h_tab(0, 0, owner_is_left=False)
    # (0,1)-(0,2): (0,1) Orange has tab pointing RIGHT into Red
    apply_h_tab(0, 1, owner_is_left=True)
    # (0,2)-(0,3): (0,2) Red has tab pointing RIGHT into Purple
    apply_h_tab(0, 2, owner_is_left=True)
    
    # (1,0)-(1,1): (1,0) Green has tab pointing RIGHT into Purple
    apply_h_tab(1, 0, owner_is_left=True)
    # (1,1)-(1,2): (1,2) White has tab pointing LEFT into Purple
    apply_h_tab(1, 1, owner_is_left=False)
    # (1,2)-(1,3): (1,2) White has tab pointing RIGHT into Blue
    apply_h_tab(1, 2, owner_is_left=True)
    
    # (2,0)-(2,1): (2,1) Brown has tab pointing LEFT into Pink
    apply_h_tab(2, 0, owner_is_left=False)
    # (2,1)-(2,2): (2,1) Brown has tab pointing RIGHT into Yellow
    apply_h_tab(2, 1, owner_is_left=True)
    # (2,2)-(2,3): (2,3) Cyan has tab pointing LEFT into Yellow
    apply_h_tab(2, 2, owner_is_left=False)
    
    # (3,0)-(3,1): (3,0) Cyan has tab pointing RIGHT into Blue
    apply_h_tab(3, 0, owner_is_left=True)
    # (3,1)-(3,2): (3,2) Orange has tab pointing LEFT into Blue
    apply_h_tab(3, 1, owner_is_left=False)
    # (3,2)-(3,3): (3,2) Orange has tab pointing RIGHT into Pink
    apply_h_tab(3, 2, owner_is_left=True)

    # Vertical connectors:
    # (0,0)-(1,0): (0,0) White has tab pointing DOWN into Green
    apply_v_tab(0, 0, owner_is_top=True)
    # (0,1)-(1,1): (1,1) Purple has tab pointing UP into Orange
    apply_v_tab(0, 1, owner_is_top=False)
    # (0,2)-(1,2): (0,2) Red has tab pointing DOWN into White
    apply_v_tab(0, 2, owner_is_top=True)
    # (0,3)-(1,3): (1,3) Blue has tab pointing UP into Purple
    apply_v_tab(0, 3, owner_is_top=False)
    
    # (1,0)-(2,0): (2,0) Pink has tab pointing UP into Green
    apply_v_tab(1, 0, owner_is_top=False)
    # (1,1)-(2,1): (1,1) Purple has tab pointing DOWN into Brown
    apply_v_tab(1, 1, owner_is_top=True)
    # (1,2)-(2,2): (2,2) Yellow has tab pointing UP into White
    apply_v_tab(1, 2, owner_is_top=False)
    # (1,3)-(2,3): (1,3) Blue has tab pointing DOWN into Cyan
    apply_v_tab(1, 3, owner_is_top=True)
    
    # (2,0)-(3,0): (2,0) Pink has tab pointing DOWN into Cyan
    apply_v_tab(2, 0, owner_is_top=True)
    # (2,1)-(3,1): (3,1) Blue has tab pointing UP into Brown
    apply_v_tab(2, 1, owner_is_top=False)
    # (2,2)-(3,2): (2,2) Yellow has tab pointing DOWN into Orange
    apply_v_tab(2, 2, owner_is_top=True)
    # (2,3)-(3,3): (3,3) Pink has tab pointing UP into Cyan
    apply_v_tab(2, 3, owner_is_top=False)

    # Render image
    out_img = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    for y in range(32):
        for x in range(32):
            pid = grid[y, x]
            out_img.putpixel((x, y), colors[pid])
            
    return out_img, grid

if __name__ == '__main__':
    img, grid = generate_jigsaw_puzzle()
    img.save('scratch/jigsaw_puzzle_32.png')
    img.resize((512, 512), Image.NEAREST).save('scratch/jigsaw_puzzle_512.png')
    print('Generated scratch/jigsaw_puzzle_32.png and scratch/jigsaw_puzzle_512.png')
