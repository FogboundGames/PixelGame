import os, sys, math, shutil, subprocess
from PIL import Image

ASSETS_DIR = "/home/emre/Resimler/PixelGame_Ikon/kapanis_karti"
SRC_VIDEO = "/home/emre/İndirilenler/landflow_ad_fixed.mp4"
CUT_TIME = 31.8  # gameplay cuts at 31.8 seconds (frame 954)
END_CARD_FRAMES = 150  # 5.0 seconds at 30 fps
FPS = 30
W, H = 1080, 1920
OUT_DIR = "/home/emre/İndirilenler"

def run_cmd(cmd):
    print("Running:", cmd)
    subprocess.run(cmd, shell=True, check=True)

def fade(img, a):
    if a >= 0.999:
        return img
    r, g, b, al = img.split()
    return Image.merge("RGBA", (r, g, b, al.point(lambda v: int(v * max(0.0, min(1.0, a))))))

def render_end_card_frames(lang, frames_dir, last_frame_path):
    os.makedirs(frames_dir, exist_ok=True)
    dark_img = Image.open(f"{ASSETS_DIR}/1_karartma_1080x1920.png").convert("RGBA")
    title_img = Image.open(f"{ASSETS_DIR}/2_baslik_{lang}_1080x1920.png").convert("RGBA")
    icon_img = Image.open(f"{ASSETS_DIR}/3_ikon_cerceveli_458x458.png").convert("RGBA")
    logo_img = Image.open(f"{ASSETS_DIR}/4_landflow_yazi_760x264.png").convert("RGBA")
    btn_img = Image.open(f"{ASSETS_DIR}/5_buton_{lang}_664x204.png").convert("RGBA")
    hand_img = Image.open(f"{ASSETS_DIR}/6_el_210x246.png").convert("RGBA")
    
    last_frame = Image.open(last_frame_path).convert("RGBA")
    if last_frame.size != (W, H):
        last_frame = last_frame.resize((W, H), Image.LANCZOS)
        
    for k in range(END_CARD_FRAMES):
        fr = last_frame.copy()
        
        # 1) Layers 2-5: Frames 0..7 fade in together from 0 to 100%
        opacity = min(1.0, (k + 1) / 8.0)
        if opacity > 0:
            fr.alpha_composite(fade(dark_img, opacity), (0, 0))
            fr.alpha_composite(fade(title_img, opacity), (0, 0))
            fr.alpha_composite(fade(icon_img, opacity), (311, 560))
            fr.alpha_composite(fade(logo_img, opacity), (160, 1048))
            
        # 2) Layer 6: Button (from frame 6)
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
            
        # 3) Layer 7: Hand (from frame 18)
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
                
        fr.convert("RGB").save(f"{frames_dir}/ec_{k:04d}.jpg", quality=95)

def build_language_video(lang):
    print(f"\n=== BUILDING FOR LANGUAGE: {lang} ===")
    frames_dir = f"scratch/frames_{lang}"
    last_frame_path = "scratch/last_frame_1080.png"
    render_end_card_frames(lang, frames_dir, last_frame_path)
    
    # Encode end card video
    ec_video = f"scratch/endcard_{lang}.mp4"
    run_cmd(f"ffmpeg -y -framerate {FPS} -i {frames_dir}/ec_%04d.jpg -c:v libx264 -pix_fmt yuv420p -crf 17 -preset slow {ec_video}")
    
    # Final concat + audio fade
    # Gameplay is in scratch/gameplay_1080.mp4 (duration CUT_TIME)
    # Total duration = CUT_TIME + END_CARD_FRAMES / FPS
    total_duration = CUT_TIME + (END_CARD_FRAMES / FPS)
    out_video = f"{OUT_DIR}/landflow_ad_final_{lang}.mp4"
    
    filter_complex = (
        f"[0:v][1:v]concat=n=2:v=1:a=0[vcat]; "
        f"[0:a]afade=t=out:st={CUT_TIME}:d=0.6,apad=whole_dur={total_duration}[acat]"
    )
    
    cmd = (
        f"ffmpeg -y -i scratch/gameplay_1080.mp4 -i {ec_video} "
        f"-filter_complex \"{filter_complex}\" "
        f"-map \"[vcat]\" -map \"[acat]\" "
        f"-c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p "
        f"-c:a aac -b:a 160k -movflags +faststart "
        f"{out_video}"
    )
    run_cmd(cmd)
    print(f"SUCCESS: Generated {out_video}")

def main():
    os.makedirs("scratch", exist_ok=True)
    
    # Step 1: Prepare gameplay video at 1080x1920 30fps up to CUT_TIME
    print("Preparing 1080x1920 gameplay video...")
    run_cmd(
        f"ffmpeg -y -ss 0 -to {CUT_TIME} -i {SRC_VIDEO} "
        f"-vf \"scale={W}:{H}:flags=lanczos,fps={FPS}\" "
        f"-c:v libx264 -preset slow -crf 17 -pix_fmt yuv420p "
        f"-c:a aac -b:a 160k scratch/gameplay_1080.mp4"
    )
    
    # Step 2: Extract last frame
    print("Extracting last gameplay frame...")
    run_cmd(
        f"ffmpeg -y -sseof -0.05 -i scratch/gameplay_1080.mp4 -frames:v 1 -update 1 scratch/last_frame_1080.png"
    )
    
    # Step 3: Build English and Turkish versions
    build_language_video("en")
    build_language_video("tr")

if __name__ == "__main__":
    main()
