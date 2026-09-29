import math
from PIL import Image, ImageDraw

def render_cube_grid(spacing, size=24, cube_px=18):
    # spacing is gap as fraction of cube_px
    gap_px = int(round(spacing * cube_px))
    step_px = cube_px + gap_px
    
    # Load heart texture
    img = Image.open('Assets/Textures/kalp.png').convert('RGBA')
    
    vis_w = 20 * step_px
    vis_h = 18 * step_px
    
    pad = 40
    out_img = Image.new('RGBA', (vis_w + pad * 2, vis_h + pad * 2), (238, 222, 192, 255)) # Sand color
    draw = ImageDraw.Draw(out_img)
    
    # Colors
    color_inner = (255, 49, 155, 255)
    color_border = (200, 10, 80, 255)
    color_highlight = (255, 195, 230, 255)
    bevel_light = (255, 90, 175, 255)
    bevel_dark = (170, 8, 70, 255)
    
    # Grid range: gx 2..21, gy 3..20
    for gy in range(3, 21):
        for gx in range(2, 22):
            p = img.getpixel((gx, 23 - gy))
            if p[3] < 25:
                continue
            
            # Determine color
            r, g, b = p[0], p[1], p[2]
            if r > 240 and g > 150:
                c = color_highlight
            elif r < 210:
                c = color_border
            else:
                c = color_inner
                
            x_pos = pad + (gx - 2) * step_px
            # In PIL, y=0 is top, gy=20 is top of heart
            y_pos = pad + (20 - gy) * step_px
            
            # Draw rounded/beveled cube
            # Base shadow/bevel
            draw.rectangle([x_pos, y_pos, x_pos + cube_px, y_pos + cube_px], fill=c)
            # Subtle top/left highlight bevel
            draw.line([x_pos, y_pos, x_pos + cube_px - 1, y_pos], fill=(min(255, c[0]+35), min(255, c[1]+35), min(255, c[2]+35)), width=1)
            draw.line([x_pos, y_pos, x_pos, y_pos + cube_px - 1], fill=(min(255, c[0]+35), min(255, c[1]+35), min(255, c[2]+35)), width=1)
            # Subtle bottom/right shadow bevel
            draw.line([x_pos, y_pos + cube_px - 1, x_pos + cube_px - 1, y_pos + cube_px - 1], fill=(max(0, c[0]-40), max(0, c[1]-40), max(0, c[2]-40)), width=1)
            draw.line([x_pos + cube_px - 1, y_pos, x_pos + cube_px - 1, y_pos + cube_px - 1], fill=(max(0, c[0]-40), max(0, c[1]-40), max(0, c[2]-40)), width=1)
            
    return out_img

def main():
    spacings = [0.00, 0.02, 0.04, 0.07]
    images = [render_cube_grid(s) for s in spacings]
    
    W = sum(im.width for im in images) + 60
    H = max(im.height for im in images) + 80
    
    comp = Image.new('RGB', (W, H), (24, 30, 44))
    draw = ImageDraw.Draw(comp)
    
    cur_x = 20
    for s, im in zip(spacings, images):
        comp.paste(im, (cur_x, 60))
        draw.text((cur_x + 10, 25), f'Spacing: {s*100:.1f}% (gap = {s*18:.1f}px)', fill=(255, 255, 255))
        cur_x += im.width + 20
        
    comp.save('scratch/compare_cube_spacings.png')
    print('Saved scratch/compare_cube_spacings.png')

if __name__ == '__main__':
    main()
