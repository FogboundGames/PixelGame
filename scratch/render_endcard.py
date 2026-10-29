import os, sys, math, shutil, subprocess
from PIL import Image

ASSETS_DIR = "/home/emre/Resimler/PixelGame_Ikon/kapanis_karti"
SRC_VIDEO = "/home/emre/İndirilenler/landflow_ad_fixed.mp4"
CUT_TIME = 31.8  # seconds
END_CARD_FRAMES = 150  # 5.0 seconds at 30 fps
FPS = 30
W, H = 1080, 1920

def fade(img, a):
    if a >= 0.999:
        return img
    r, g, b, al = img.split()
    return Image.merge("RGBA", (r, g, b, al.point(lambda v: int(v * max(0.0, min(1.0, a))))))

def render_end_card(lang="en", output_dir="scratch/frames_endcard"):
    os.makedirs(output_dir, exist_ok=True)
    
    dark_img = Image.open(f"{ASSETS_DIR}/1_karartma_1080x1920.png").convert("RGBA")
    title_file = f"{ASSETS_DIR}/2_baslik_{lang}_1080x1920.png"
    title_img = Image.open(title_file).convert("RGBA")
    icon_img = Image.open(f"{ASSETS_DIR}/3_ikon_cerceveli_458x458.png").convert("RGBA")
    logo_img = Image.open(f"{ASSETS_DIR}/4_landflow_yazi_760x264.png").convert("RGBA")
    btn_file = f"{ASSETS_DIR}/5_buton_{lang}_664x204.png"
    btn_img = Image.open(btn_file).convert("RGBA")
    hand_img = Image.open(f"{ASSETS_DIR}/6_el_210x246.png").convert("RGBA")
    
    last_frame = Image.open("scratch/last_frame.png").convert("RGBA")
    if last_frame.size != (W, H):
        last_frame = last_frame.resize((W, H), Image.LANCZOS)
        
    for k in range(END_CARD_FRAMES):
        fr = last_frame.copy()
        
        # Layers 2-5: Frames 0..7 fade in together from 0 to 100%
        opacity = min(1.0, (k + 1) / 8.0)
        if opacity > 0:
            fr.alpha_composite(fade(dark_img, opacity), (0, 0))
            fr.alpha_composite(fade(title_img, opacity), (0, 0))
            fr.alpha_composite(fade(icon_img, opacity), (311, 560))
            fr.alpha_composite(fade(logo_img, opacity), (160, 1048))
            
        # Layer 6: Button (from frame 6)
        if k >= 6:
            btn_k = k - 6
            if btn_k < 8:
                t = (btn_k + 1) / 8.0
                sc = 1.0 - (1.0 - t) ** 3
            else:
                sc = 1.0 + 0.05 * math.sin(0.35 * btn_k)
                
            bw, bh = max(1, int(btn_img.width * sc)), max(1, int(btn_img.height * sc))
            btn_scaled = btn_img.resize((bw, bh), Image.LANCZOS)
            bx = int(540 - bw / 2.0)
            by = int(1484 - bh / 2.0)
            fr.alpha_composite(btn_scaled, (bx, by))
            
        # Layer 7: Hand (from frame 18)
        if k >= 18:
            if k < 28:
                u = (k - 18 + 1) / 10.0
                fx = 850.0 + (690.0 - 850.0) * u
                fy = 1774.0 + (1514.0 - 1774.0) * u
                hx = int(fx - 86)
                hy = int(fy - 6)
                fr.alpha_composite(fade(hand_img, u), (hx, hy))
            else:
                tap_k = (k - 28) % 22
                hand_sc = 1.0 - 0.12 * math.sin(tap_k / 22.0 * math.pi)
                hw, hh = max(1, int(hand_img.width * hand_sc)), max(1, int(hand_img.height * hand_sc))
                hand_scaled = hand_img.resize((hw, hh), Image.LANCZOS)
                hx = int(690 - 86 * hand_sc)
                hy = int(1514 - 6 * hand_sc)
                fr.alpha_composite(hand_scaled, (hx, hy))
                
        fr.convert("RGB").save(f"{output_dir}/ec_{k:04d}.jpg", quality=95)
        
    print(f"Rendered {END_CARD_FRAMES} frames for {lang}")

if __name__ == "__main__":
    lang = sys.argv[1] if len(sys.argv) > 1 else "en"
    render_end_card(lang)
