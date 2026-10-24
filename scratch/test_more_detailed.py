import math
from PIL import Image

def create_steam_locomotive():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    px = im.load()
    
    c_iron = (32, 34, 40, 255)         # Black boiler iron
    c_iron_dk = (20, 21, 24, 255)      # Deep shadow
    c_brass = (235, 175, 40, 255)      # Golden brass bands / headlight
    c_lamp = (255, 245, 140, 255)      # Glowing yellow beam
    c_red = (195, 30, 42, 255)         # Cowcatcher / cab trim
    c_steam = (245, 248, 252, 255)     # Puffy steam clouds
    c_steam_sh = (205, 215, 228, 255)  # Steam cloud shading
    c_rail = (85, 90, 100, 255)        # Steel track
    
    # Track at bottom (y: 29..30)
    for x in range(2, 30):
        px[x, 30] = c_rail
        if x % 3 == 0: px[x, 31] = c_iron_dk # ties
        
    # Steam Puff expanding upwards from smokestack (Stack at x=9..11, y=10..13)
    # Steam clouds at x: 4..18, y: 1..9
    steam_clouds = [(10, 8, 2.5), (12, 5, 3.5), (16, 3, 4.0)]
    for cx, cy, r in steam_clouds:
        for y in range(int(cy - r - 1), int(cy + r + 2)):
            for x in range(int(cx - r - 1), int(cx + r + 2)):
                if 0 <= x < 32 and 0 <= y < 32:
                    dx, dy = (x - cx) / r, (y - cy) / r
                    dist = math.sqrt(dx*dx + dy*dy)
                    if dist <= 1.0:
                        px[x, y] = c_steam_sh if (dy > 0.2 or dist > 0.8) else c_steam

    # Cowcatcher / Wedge Pilot at front (x: 3..7, y: 24..28)
    for y in range(24, 29):
        dy = y - 24
        for x in range(7 - dy, 8):
            px[x, y] = c_red
            
    # Smokestack (x: 9..11, y: 10..15)
    for y in range(10, 16):
        for x in range(9, 12):
            px[x, y] = c_brass if y in (10, 15) else c_iron
            
    # Headlight at front (x: 6..8, y: 16..19)
    for y in range(16, 20):
        for x in range(6, 9):
            px[x, y] = c_lamp if (x == 6 and y in (17, 18)) else c_brass
    # Light beam
    px[4, 17] = c_lamp; px[5, 17] = c_lamp
    px[3, 18] = c_lamp; px[4, 18] = c_lamp; px[5, 18] = c_lamp

    # Cylindrical Boiler (x: 8..20, y: 15..24)
    for y in range(15, 25):
        for x in range(8, 21):
            if x in (8, 12, 16, 20) and y in (15, 16, 23, 24):
                px[x, y] = c_brass # Brass rivet bands
            else:
                px[x, y] = c_iron_dk if y >= 23 else c_iron

    # Driver Cab (x: 21..28, y: 11..25)
    for y in range(11, 26):
        for x in range(21, 29):
            if y == 11:
                px[x, y] = c_red # Cab curved roof
            elif 13 <= y <= 17 and 23 <= x <= 26:
                px[x, y] = c_lamp # Lit cab window
            else:
                px[x, y] = c_iron

    # Wheels (Front pilot wheels at x: 8, 11; Giant driving wheels at x: 16, 23, y: 24..29)
    # Wheel 1 (x: 14..18, y: 24..29, r=2.5)
    # Wheel 2 (x: 21..25, y: 24..29, r=2.5)
    for w_cx in (16, 23):
        for y in range(24, 30):
            for x in range(w_cx - 2, w_cx + 3):
                dx = x - w_cx
                dy = y - 27
                if dx*dx + dy*dy <= 2.8**2:
                    px[x, y] = c_brass if (dx == 0 or dy == 0) else c_iron_dk
                    
    # Connecting Rod (iron piston bar connecting wheel centers)
    for x in range(14, 25):
        px[x, 27] = c_iron
    return im

def create_ship_compass():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    px = im.load()
    
    cx, cy = 15.5, 15.5
    c_brass_dk = (160, 110, 20, 255)   # Antique dark brass
    c_brass = (225, 175, 45, 255)      # Polished brass rim
    c_brass_lt = (255, 225, 110, 255)  # Bezel highlight
    c_dial = (24, 36, 56, 255)         # Deep nautical navy dial
    c_dial_ring = (48, 70, 105, 255)   # Compass degree circle
    c_needle_n = (225, 38, 48, 255)    # North needle ruby red
    c_needle_s = (245, 245, 245, 255)  # South needle pure white
    c_needle_sh = (180, 20, 30, 255)   # Needle shade
    c_pivot = (255, 215, 60, 255)      # Center pivot brass screw
    
    # Outer Brass Gimbal & Casing (radius 14.5)
    for y in range(32):
        for x in range(32):
            dx = x - cx
            dy = y - cy
            dist = math.sqrt(dx*dx + dy*dy)
            
            # Top mounting ring / loop at (15..16, 1..3)
            if 1 <= y <= 3 and 14 <= x <= 17:
                px[x, y] = c_brass if (x in (14, 17) or y == 1) else (0, 0, 0, 0)
                
            if dist <= 14.2:
                # Outer Brass Rim
                if dist > 12.2:
                    px[x, y] = c_brass_lt if (dx < 0 and dy < 0) else (c_brass_dk if (dx > 0 and dy > 0) else c_brass)
                # Inner Bezel Ring
                elif dist > 10.8:
                    px[x, y] = c_brass_dk
                # Degree Scale & Navy Dial Face
                elif dist > 8.5:
                    # Degree tick marks every 45 and 90 degrees
                    angle = math.atan2(dy, dx)
                    is_tick = (abs(dx) <= 1 or abs(dy) <= 1 or abs(abs(dx) - abs(dy)) <= 1)
                    px[x, y] = c_brass if is_tick else c_dial_ring
                else:
                    px[x, y] = c_dial
                    
    # 8-Point Compass Rose (x: 10..21, y: 10..21)
    # North Needle (pointing up to y=6)
    for y in range(7, 16):
        w = max(0, int((y - 7) * 0.4))
        for x in range(15 - w, 17 + w):
            px[x, y] = c_needle_n if x <= 15 else c_needle_sh
            
    # South Needle (pointing down to y=24)
    for y in range(16, 25):
        w = max(0, int((24 - y) * 0.4))
        for x in range(15 - w, 17 + w):
            px[x, y] = c_needle_s if x <= 15 else c_dial_ring

    # East & West Needles (horizontal points)
    for x in range(7, 15):
        w = max(0, int((x - 7) * 0.4))
        for y in range(15 - w, 17 + w):
            px[x, y] = c_needle_s if y <= 15 else c_dial_ring
    for x in range(17, 25):
        w = max(0, int((24 - x) * 0.4))
        for y in range(15 - w, 17 + w):
            px[x, y] = c_needle_n if y <= 15 else c_needle_sh

    # Center Brass Pivot Screw (x: 14..17, y: 14..17)
    for y in range(14, 18):
        for x in range(14, 18):
            dx, dy = x - cx, y - cy
            if dx*dx + dy*dy <= 1.8**2:
                px[x, y] = c_pivot
    return im

print('Steam locomotive and compass defined successfully!')
