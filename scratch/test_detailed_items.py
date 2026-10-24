import math, sys
from PIL import Image

def create_pirate_galleon():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    px = im.load()
    
    # Palette:
    c_sky = (0, 0, 0, 0)
    c_ocean = (25, 118, 185, 255)       # Ocean blue
    c_foam = (168, 224, 245, 255)       # Wave foam / crest
    c_wood_dark = (66, 36, 17, 255)     # Dark hull wood / masts
    c_wood_light = (140, 78, 36, 255)   # Warm hull wood / deck
    c_gold = (235, 175, 45, 255)        # Gold stern trim / cannon ports
    c_sail = (248, 246, 235, 255)       # Billowed white sail
    c_sail_sh = (195, 192, 178, 255)    # Sail shadow
    c_black = (24, 25, 28, 255)         # Flag / rigging
    c_red = (215, 38, 42, 255)          # Pennant / hull stripe
    
    # Ocean waves at bottom (y >= 26)
    for x in range(32):
        wave_y = 27 + int(math.sin(x * 0.7) * 1.5)
        for y in range(wave_y, 32):
            px[x, y] = c_foam if y == wave_y else c_ocean
            
    # Ship Hull (y: 19..26, x: 4..27)
    for y in range(19, 27):
        for x in range(4, 28):
            # Curved hull silhouette
            left_bound = 7 - (y - 19) * 0.5
            right_bound = 24 + (y - 19) * 0.4
            if y == 26: left_bound += 2; right_bound -= 2
            if left_bound <= x <= right_bound:
                if y == 19:
                    px[x, y] = c_wood_dark # railing
                elif y == 20:
                    px[x, y] = c_red # red accent stripe
                elif y in (21, 22) and x in (9, 13, 17, 21):
                    px[x, y] = c_gold # gunports / gold trim
                elif y >= 24:
                    px[x, y] = c_wood_dark # lower hull
                else:
                    px[x, y] = c_wood_light # main planks
                    
    # Bowsprit (front spar jutting right, x: 25..30, y: 17..20)
    for i in range(5):
        px[25 + i, 19 - int(i * 0.4)] = c_wood_dark
        
    # Masts (Foremast x=11, Mainmast x=18)
    for y in range(3, 20):
        px[18, y] = c_wood_dark # Mainmast
        if y >= 6:
            px[11, y] = c_wood_dark # Foremast
            
    # Yardarms (horizontal crossbeams)
    for x in range(6, 17): px[x, 7] = c_wood_dark
    for x in range(7, 16): px[x, 12] = c_wood_dark
    for x in range(13, 24): px[x, 5] = c_wood_dark
    for x in range(14, 23): px[x, 11] = c_wood_dark
    
    # Foremast Sails (x: 7..15)
    for y in range(8, 12):
        for x in range(7, 16):
            if x != 11:
                px[x, y] = c_sail_sh if (y == 11 or x == 7) else c_sail
    for y in range(13, 18):
        for x in range(7, 16):
            if x != 11:
                px[x, y] = c_sail_sh if (y == 17 or x == 7) else c_sail

    # Mainmast Sails (x: 14..23)
    for y in range(6, 10):
        for x in range(14, 23):
            if x != 18:
                px[x, y] = c_sail_sh if (y == 9 or x == 14) else c_sail
    for y in range(12, 17):
        for x in range(14, 23):
            if x != 18:
                px[x, y] = c_sail_sh if (y == 16 or x == 14) else c_sail
                
    # Jolly Roger Pirate Flag on Top Mainmast (x: 19..23, y: 2..5)
    for y in range(2, 6):
        for x in range(19, 24):
            px[x, y] = c_black
    # White skull emblem on flag
    px[21, 3] = c_sail
    px[20, 4] = c_sail; px[22, 4] = c_sail
    px[21, 5] = c_sail
    
    return im

def create_lighthouse_night():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    px = im.load()
    
    c_sky = (16, 20, 38, 255)       # Midnight sky
    c_star = (248, 244, 180, 255)   # Twinkling star
    c_beam = (255, 235, 120, 255)   # Glowing light beam
    c_beam_soft = (255, 245, 180, 180) # Translucent light halo
    c_red = (218, 38, 42, 255)      # Lighthouse red
    c_white = (245, 246, 248, 255)  # Lighthouse white
    c_black = (28, 29, 36, 255)     # Lantern dome / railing
    c_rock = (62, 68, 76, 255)      # Dark sea cliff
    c_rock_lt = (98, 106, 118, 255) # Rock highlight
    c_water = (22, 78, 126, 255)    # Ocean water
    c_foam = (175, 225, 240, 255)   # Wave foam
    
    # 1. Background Sky (Circular / framed sky cut)
    cx, cy = 15.5, 15.5
    for y in range(32):
        for x in range(32):
            dx, dy = x - cx, y - cy
            if dx*dx + dy*dy <= 15.0**2:
                px[x, y] = c_sky
                
    # Stars in sky
    star_coords = [(6, 5), (10, 8), (24, 4), (27, 9), (7, 13), (25, 14)]
    for sx, sy in star_coords:
        px[sx, sy] = c_star
        
    # Light beam sweeping diagonally right: from lantern (15, 9) outward
    for x in range(16, 31):
        rel_x = x - 15
        top_y = 9 - int(rel_x * 0.25)
        bot_y = 10 + int(rel_x * 0.45)
        for y in range(top_y, bot_y + 1):
            if 0 <= y < 32 and px[x, y] == c_sky:
                px[x, y] = c_beam if abs(y - (9 + int(rel_x*0.1))) <= 1 else c_beam_soft
                
    # Cliffs & Water at bottom (y >= 23)
    for y in range(23, 32):
        for x in range(32):
            if px[x, y] != (0, 0, 0, 0):
                if y >= 28:
                    px[x, y] = c_foam if y == 28 and x % 3 == 0 else c_water
                else:
                    px[x, y] = c_rock_lt if (x + y) % 3 == 0 else c_rock

    # Lighthouse Tower (x: 12..18, y: 7..24)
    # Lantern dome on top (y: 6..8, x: 13..17)
    px[15, 6] = c_black
    for x in range(14, 17): px[x, 7] = c_black
    for x in range(13, 18): px[x, 8] = c_beam # Lantern glass glowing yellow!
    for x in range(12, 19): px[x, 9] = c_black # Balcony catwalk railing
    
    # Tapered tower body (y: 10..24)
    for y in range(10, 25):
        width = 2.4 + (y - 10) * 0.18
        lx = int(round(15.0 - width))
        rx = int(round(15.0 + width))
        # Stripes: alternating red and white
        # y 10..12: Red
        # y 13..15: White
        # y 16..18: Red
        # y 19..21: White
        # y 22..24: Red
        stripe_col = c_red if (y // 3) % 2 == 1 else c_white
        for x in range(lx, rx + 1):
            # Window at (15, 14) and (15, 20)
            if x == 15 and y in (14, 20):
                px[x, y] = c_black
            elif x in (lx, rx):
                px[x, y] = c_black # outline
            else:
                px[x, y] = stripe_col
    return im

def create_samurai_kabuto():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    px = im.load()
    
    c_black = (24, 25, 30, 255)        # Lacquered black helmet
    c_gold = (238, 180, 36, 255)       # Golden crest & brass ornaments
    c_gold_dk = (180, 130, 24, 255)    # Gold shade
    c_red = (195, 30, 42, 255)         # Crimson lacing / silk cord
    c_navy = (36, 48, 85, 255)         # Navy tiered armor plates
    c_white = (245, 245, 245, 255)     # Mask teeth / highlights
    
    cx = 15.5
    # 1. Golden Kuwagata Crest (Swept-back horns at top, y: 3..11)
    # Center sun disk (x: 14..17, y: 9..11)
    for y in range(9, 12):
        for x in range(14, 18):
            px[x, y] = c_gold
    # Curved horns sweeping outward and up
    for i in range(8):
        # Left horn
        hx_l = 14 - i
        hy = 10 - int(i * 0.9)
        if 0 <= hx_l < 32 and 0 <= hy < 32:
            px[hx_l, hy] = c_gold
            px[hx_l, hy+1] = c_gold_dk
        # Right horn
        hx_r = 17 + i
        if 0 <= hx_r < 32 and 0 <= hy < 32:
            px[hx_r, hy] = c_gold
            px[hx_r, hy+1] = c_gold_dk

    # 2. Helmet Bowl (Kabuto Hachi, y: 11..18, x: 9..22)
    for y in range(11, 19):
        dy = y - 11
        half_w = 4.5 + dy * 0.5
        for x in range(int(15.5 - half_w), int(16.5 + half_w)):
            if (x == int(15.5 - half_w) or x == int(15.5 + half_w) or y == 11):
                px[x, y] = c_gold # Gold rim / ridges
            else:
                px[x, y] = c_black

    # 3. Tiered Neck Guard (Shikoro plates, y: 19..28, curving around sides)
    for y in range(19, 29):
        # 3 layered tiers: y 19..21 (tier 1), y 22..24 (tier 2), y 25..27 (tier 3)
        tier = (y - 19) // 3
        tier_w = 8.5 + tier * 1.5
        col = c_navy if (y % 2 == 0) else c_red # alternating lacing threads
        for x in range(int(15.5 - tier_w), int(16.5 + tier_w)):
            # Open face opening in middle (x: 12..19, y: 18..25)
            if not (12 <= x <= 19 and 18 <= y <= 24):
                px[x, y] = c_gold if x in (int(15.5 - tier_w), int(15.5 + tier_w)) else col

    # 4. Menpo Face Mask in center (y: 19..25, x: 12..19)
    for y in range(19, 26):
        for x in range(12, 20):
            # Nose bridge & cheeks
            if y == 19:
                px[x, y] = c_gold if x in (15, 16) else c_black
            elif y in (20, 21):
                px[x, y] = c_black
            elif y == 22:
                # Fierce white fangs/teeth
                px[x, y] = c_white if x in (14, 17) else c_black
            elif y in (23, 24):
                # Chin plate
                px[x, y] = c_red if x in (14, 15, 16, 17) else c_black
                
    # Decorative red silk cord tie hanging below (y: 26..30, x: 14..17)
    for y in range(26, 31):
        px[15, y] = c_red
        px[16, y] = c_red
    px[14, 27] = c_red; px[17, 27] = c_red # Knot loop
    return im

def create_magic_potion():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    px = im.load()
    
    c_cork = (145, 95, 45, 255)       # Wooden cork
    c_gold = (238, 185, 42, 255)      # Ornate gold cage / filigree
    c_gold_dk = (175, 125, 25, 255)   # Gold shade
    c_glass = (185, 220, 240, 255)    # Glass shine / rim
    c_potion_dk = (92, 28, 142, 255)  # Deep purple mana potion
    c_potion_mid = (172, 54, 215, 255)# Bright magenta/purple
    c_potion_lt = (225, 130, 255, 255)# Glowing potion highlight
    c_bubble = (100, 245, 255, 255)   # Cyan magical bubbles / sparkles
    c_white = (255, 255, 255, 255)    # Specular glass glint
    
    # 1. Cork & Neck (y: 4..10, x: 13..18)
    for x in range(14, 18):
        px[x, 4] = c_cork; px[x, 5] = c_cork # Cork top
    for x in range(12, 20):
        px[x, 6] = c_gold # Golden bottle rim
    for y in range(7, 11):
        px[13, y] = c_gold; px[18, y] = c_gold # Neck walls
        for x in range(14, 18):
            px[x, y] = c_glass
            
    # 2. Bulbous Flask Body (Spherical flask, cx=15.5, cy=20.5, radius=10.5)
    cx, cy = 15.5, 20.5
    for y in range(10, 31):
        for x in range(5, 27):
            dx = x - cx
            dy = y - cy
            dist = math.sqrt(dx*dx + dy*dy)
            if dist <= 10.2:
                # Outer glass boundary & ornate golden filigree ribs
                is_filigree = (dist >= 8.8 and (abs(dx) == abs(dy) or abs(dx) == 0 or abs(dy) == 0))
                if dist >= 9.2:
                    px[x, y] = c_gold if is_filigree else c_glass
                else:
                    # Inside liquid (potion fill level: y >= 13)
                    if y >= 13:
                        # Liquid vortex & gradient
                        if dist <= 4.0:
                            px[x, y] = c_potion_lt # Glowing center core
                        elif dy > 0:
                            px[x, y] = c_potion_dk # Deep shaded bottom
                        else:
                            px[x, y] = c_potion_mid
                    else:
                        # Empty upper glass
                        px[x, y] = c_glass
                        
    # 3. Add magical cyan bubbles & specular highlights
    bubbles = [(13, 23), (17, 21), (15, 17), (19, 24), (12, 18)]
    for bx, by in bubbles:
        px[bx, by] = c_bubble
    # White glass glint curve (top-left)
    glints = [(9, 15), (8, 16), (8, 17), (8, 18)]
    for gx, gy in glints:
        px[gx, gy] = c_white
        
    return im

def create_retro_console():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    px = im.load()
    
    c_case = (210, 208, 198, 255)     # Retro grey/beige console body
    c_case_sh = (165, 162, 152, 255)  # Case shadow/bezel
    c_screen_border = (85, 88, 105, 255)# Dark screen bezel
    c_screen_bg = (142, 168, 62, 255) # Classic pea-soup greenish LCD screen
    c_screen_px = (45, 68, 22, 255)   # Dark LCD pixel graphic
    c_dpad = (35, 36, 40, 255)        # Black D-Pad
    c_btn_a = (195, 30, 60, 255)      # Red/Magenta A button
    c_btn_b = (165, 24, 50, 255)      # B button
    c_pill = (100, 102, 110, 255)     # Select/Start rubber pills
    c_led = (245, 30, 30, 255)        # Red power LED
    
    # Console Body: x: 5..26, y: 2..29 (rounded corners)
    for y in range(2, 30):
        for x in range(5, 27):
            # Rounded corners
            if (x in (5, 26) and y in (2, 29)) or (x in (5, 6) and y == 29):
                continue # Bottom-left slant / curve
            # Shaded bottom/right edge
            if x == 26 or y == 29:
                px[x, y] = c_case_sh
            else:
                px[x, y] = c_case

    # Screen Bezel (x: 7..24, y: 5..17)
    for y in range(5, 18):
        for x in range(7, 25):
            px[x, y] = c_screen_border
            
    # Power LED at (8, 10)
    px[8, 10] = c_led
    
    # LCD Screen Window (x: 10..22, y: 7..15)
    for y in range(7, 16):
        for x in range(10, 23):
            px[x, y] = c_screen_bg
            
    # Mini Pixel Landscape on Screen!
    # Mountains / character
    mountain_pts = [(11, 14), (12, 13), (13, 12), (14, 13), (15, 14), (17, 14), (18, 13), (19, 14)]
    for mx, my in mountain_pts: px[mx, my] = c_screen_px
    # Little pixel hero at (14, 10)
    px[14, 10] = c_screen_px; px[14, 11] = c_screen_px
    # Sun dot at (20, 8)
    px[20, 8] = c_screen_px
    
    # Controls (y: 19..28)
    # D-Pad Cross at (x: 8..12, y: 20..24)
    for y in range(20, 25):
        for x in range(8, 13):
            if x == 10 or y == 22:
                px[x, y] = c_dpad
                
    # A & B Action Buttons (slanted, B at (19, 23), A at (22, 21))
    for dy in (0, 1):
        for dx in (0, 1):
            px[18 + dx, 22 + dy] = c_btn_b
            px[22 + dx, 20 + dy] = c_btn_a

    # Select & Start angled pills (x: 12..18, y: 26..27)
    px[13, 27] = c_pill; px[14, 26] = c_pill
    px[16, 27] = c_pill; px[17, 26] = c_pill
    
    # Speaker Slots at bottom-right (x: 22..25, y: 26..28)
    px[22, 28] = c_case_sh; px[24, 27] = c_case_sh; px[25, 26] = c_case_sh
    
    return im

def create_fire_dragon():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    px = im.load()
    
    c_horn = (45, 40, 52, 255)        # Obsidian horns / spikes
    c_horn_tip = (235, 180, 45, 255)  # Horn gold tips
    c_scale_dk = (138, 22, 32, 255)   # Deep crimson shadow
    c_scale = (212, 38, 48, 255)      # Bright dragon scale red
    c_belly = (242, 132, 36, 255)     # Warm flame orange underbelly
    c_eye = (255, 235, 30, 255)       # Glowing reptile eye
    c_fang = (248, 248, 240, 255)     # Sharp fangs
    c_fire = (255, 190, 40, 255)      # Fire breath spark
    
    # Dragon Head Facing Left
    # 1. Horns (Swept up and right, x: 18..29, y: 3..12)
    horn_pts = [
        (18, 10), (19, 9), (20, 8), (21, 7), (22, 6), (23, 5), (24, 4), (25, 3), # Upper Horn
        (19, 10), (20, 9), (21, 8), (22, 7), (23, 6),
        (20, 14), (21, 13), (22, 12), (23, 11), (24, 10), (25, 9), (26, 8)       # Lower Horn
    ]
    for hx, hy in horn_pts:
        px[hx, hy] = c_horn
    px[25, 3] = c_horn_tip; px[26, 8] = c_horn_tip
    
    # 2. Main Cranium & Snout (y: 8..23, x: 5..23)
    for y in range(8, 25):
        for x in range(5, 24):
            # Snout narrows forward to x=5
            is_head = False
            # Upper snout / brow
            if 8 <= y <= 15 and 9 <= x <= 20: is_head = True
            # Long jaw extending to x=6 (y: 13..17)
            if 13 <= y <= 17 and 5 <= x <= 16: is_head = True
            # Lower jaw (y: 19..22, x: 8..17)
            if 19 <= y <= 22 and 8 <= x <= 17: is_head = True
            # Neck & crest (y: 16..24, x: 14..23)
            if 16 <= y <= 24 and 14 <= x <= 22: is_head = True
            
            if is_head:
                if y >= 20:
                    px[x, y] = c_belly # Orange throat / underbelly
                elif y == 8 or x in (5, 23):
                    px[x, y] = c_scale_dk # Outline / dark scales
                else:
                    px[x, y] = c_scale
                    
    # Eye at (12, 12)
    px[12, 12] = c_eye
    px[13, 12] = c_eye
    px[13, 11] = c_scale_dk # Brow ridge
    
    # Nostril at (7, 13)
    px[7, 13] = c_scale_dk
    
    # Fangs in open jaw (x: 8..14, y: 17..18)
    px[8, 17] = c_fang; px[11, 17] = c_fang
    px[9, 19] = c_fang; px[12, 19] = c_fang
    
    # Fire puff escaping mouth (x: 2..5, y: 16..18)
    px[3, 17] = c_fire; px[4, 17] = c_belly; px[2, 18] = c_fire
    
    return im

def create_zen_bonsai():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    px = im.load()
    
    c_pot = (38, 92, 154, 255)         # Cobalt blue glazed ceramic pot
    c_pot_rim = (85, 145, 215, 255)    # Glaze highlight
    c_soil = (72, 45, 28, 255)         # Rich dark potting soil
    c_moss = (95, 155, 45, 255)        # Green surface moss
    c_trunk = (112, 68, 38, 255)       # Gnarled bark brown
    c_foliage_dk = (32, 85, 42, 255)   # Deep pine / juniper needle shadow
    c_foliage = (56, 142, 60, 255)     # Vibrant bonsai green
    c_foliage_lt = (129, 199, 132, 255)# Tender spring leaf tips
    c_blossom = (255, 182, 193, 255)   # Pink sakura petal accent
    
    # 1. Ceramic Pot with feet (y: 24..29, x: 7..24)
    # Pot body
    for y in range(25, 29):
        for x in range(8, 24):
            px[x, y] = c_pot
    # Pot top rim
    for x in range(7, 25): px[x, 24] = c_pot_rim
    # Pot pedestal feet (x: 9..10, 21..22, y: 29)
    px[9, 29] = c_pot; px[10, 29] = c_pot
    px[21, 29] = c_pot; px[22, 29] = c_pot
    
    # 2. Soil & Moss Mound (y: 22..24, x: 9..22)
    for x in range(9, 23):
        px[x, 23] = c_moss if x % 2 == 0 else c_soil
        
    # 3. Gnarled Twisted Bonsai Trunk (y: 11..23)
    # Base trunk (x: 14..17, y: 19..23)
    for y in range(19, 24):
        for x in range(14, 18): px[x, y] = c_trunk
    # Diagonal lean to left (y: 15..18, x: 11..15)
    for y in range(15, 19):
        offset = 18 - y
        for x in range(14 - offset, 17 - offset): px[x, y] = c_trunk
    # Branch split: Left branch (x: 7..11, y: 12..14), Right branch (x: 14..22, y: 10..15)
    branch_pts = [
        (9, 14), (8, 13), (7, 13), # Left branch
        (13, 14), (15, 13), (17, 12), (19, 12), (21, 11) # Right ascending branch
    ]
    for bx, by in branch_pts: px[bx, by] = c_trunk
    
    # 4. Foliage Cloud Pads (Clusters of needles)
    # Cloud 1: Left lower pad (cx=8, cy=12, r=4)
    # Cloud 2: Top crown pad (cx=14, cy=7, r=5)
    # Cloud 3: Right upper pad (cx=22, cy=10, r=4.5)
    clouds = [(8, 12, 3.8), (14, 7, 5.0), (22, 10, 4.2)]
    for cx, cy, r in clouds:
        for y in range(int(cy - r - 1), int(cy + r + 2)):
            for x in range(int(cx - r - 1), int(cx + r + 2)):
                dx, dy = (x - cx) / (r * 1.1), (y - cy) / (r * 0.75)
                dist = math.sqrt(dx*dx + dy*dy)
                if dist <= 1.0:
                    if dist <= 0.45:
                        px[x, y] = c_foliage_lt
                    elif dy > 0.2:
                        px[x, y] = c_foliage_dk
                    else:
                        px[x, y] = c_foliage
                        
    # Delicate sakura petals on foliage
    petals = [(13, 5), (16, 7), (7, 11), (23, 9), (18, 25)]
    for px_x, px_y in petals:
        px[px_x, px_y] = c_blossom
        
    return im

DETAILED_ITEMS = [
    ("KorsanGemisi", create_pirate_galleon, "Korsan Kalyonu (Ahşap Gövde, Şişkin Beyaz Yelkenler, Jolly Roger Korsan Bayrağı & Dalgalar)"),
    ("DenizFeneriGece", create_lighthouse_night, "Gece Deniz Feneri (Kırmızı-Beyaz Kule, Gece Göğü, Yıldızlar, Projektör Işığı & Dalgalar)"),
    ("SamurayMigferi", create_samurai_kabuto, "Samuray Kabuto Miğferi (Altın Hilal Arması, Katmanlı Zırh Plakaları, Korkutucu Yüz Maskesi)"),
    ("BuyuluIksir", create_magic_potion, "Büyülü Simya İksiri (Altın Kafesli Şişe, Mor Mana Girdabı, Işıldayan Baloncuklar & Kristal Tıpa)"),
    ("RetroKonsol", create_retro_console, "Retro El Konsolu (Piksel Ekran, D-Pad Yön Tuşları, Kırmızı Aksiyon Tuşları, Hoparlör Kanalları)"),
    ("EjderhaBasi", create_fire_dragon, "Kızıl Ejderha Başı (Obsidyen Boynuzlar, Yakut Pullar, Parlayan Sarı Sürüngen Gözü & Alev Dili)"),
    ("BonsaiAgaci", create_zen_bonsai, "Zen Bonsai Ağacı (Kobalt Mavi Seramik Saksı, Yaşlı Kıvrımlı Gövde, Zümrüt İğne Bulutları & Sakura)")
]

if __name__ == '__main__':
    for name, func, desc in DETAILED_ITEMS:
        im = func()
        # Count non-transparent pixels and colors
        colors = set()
        cubes = 0
        for p in im.getdata():
            if p[3] > 20:
                cubes += 1
                colors.add(p[:3])
        print(f"{name:18} | Cubes: {cubes:4} | Unique Colors: {len(colors):2} | {desc[:40]}...")
