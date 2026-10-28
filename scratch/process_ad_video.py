#!/usr/bin/env python3
import json
import math
import os
import shutil
import subprocess
import sys
import tempfile
import numpy as np
from PIL import Image

def fade_image(img, alpha_float):
    if alpha_float >= 1.0:
        return img
    if alpha_float <= 0.0:
        return Image.new("RGBA", img.size, (0, 0, 0, 0))
    res = img.copy()
    r, g, b, a = res.split()
    a = a.point(lambda p: int(round(p * alpha_float)))
    res.putalpha(a)
    return res

def build_end_card_frame(frozen_frame, k, layers, lang="tr", show_headline=True):
    """
    k: end card frame index (0-indexed, 60 fps)
    frozen_frame: PIL.Image of last gameplay frame (1080x1920)
    layers: dict of preloaded PNGs
    show_headline: boolean to toggle headline text
    """
    dark = layers["dark"]
    head = layers.get(f"head_{lang}")
    icon = layers["icon"]
    name = layers["name"]
    btn = layers[f"btn_{lang}"]
    hand = layers["hand"]

    # Start with frozen gameplay frame
    fr = frozen_frame.copy().convert("RGBA")

    # At 60 fps:
    # 0.27s fade in = ~16 frames
    fade_alpha = min(1.0, (k + 1) / 16.0)

    # 2) Dark overlay (solid navy #0a1432 at 69% opacity)
    fr.alpha_composite(fade_image(dark, fade_alpha), (0, 0))

    # 3) Headline (if enabled)
    if show_headline and head is not None:
        fr.alpha_composite(fade_image(head, fade_alpha), (0, 0))

    # 4) App icon at (311, 560)
    fr.alpha_composite(fade_image(icon, fade_alpha), (311, 560))

    # 5) Game name at (160, 1048)
    fr.alpha_composite(fade_image(name, fade_alpha), (160, 1048))

    # 6) Button centered at (540, 1484)
    btn_center = (540, 1484)
    # Starts at 0.2s = frame 12
    if k >= 12:
        kk = k - 12
        pop_p = min(1.0, kk / 16.0) # 16 frames pop-in
        pop_scale = 1.0 - math.pow(1.0 - pop_p, 3)
        scale = max(0.01, pop_scale + 0.05 * math.sin(kk * 0.175) * pop_p)

        bw = max(1, int(round(btn.width * scale)))
        bh = max(1, int(round(btn.height * scale)))
        b_scaled = btn.resize((bw, bh), Image.LANCZOS)
        bx = int(round(btn_center[0] - bw / 2.0))
        by = int(round(btn_center[1] - bh / 2.0))
        fr.alpha_composite(b_scaled, (bx, by))

    # 7) Hand pointer (fingertip at 86, 6)
    # Starts at 0.6s = frame 36
    if k >= 36:
        hk = k - 36
        u = min(1.0, hk / 20.0) # 20 frames slide in

        # Fingertip targets: Start at (850, 1774), target (690, 1514)
        tip_x = 690 + 160.0 * (1.0 - u)
        tip_y = 1514 + 260.0 * (1.0 - u)

        # Press animation from frame 56 (hk >= 20)
        # Period: 44 frames at 60 fps (half period peak press at +22 frames)
        press = 1.0
        if hk > 20:
            press = 1.0 - 0.12 * max(0.0, math.sin(((hk - 20) % 44) / 44.0 * math.pi))

        hw = max(1, int(round(hand.width * press)))
        hh = max(1, int(round(hand.height * press)))
        h_scaled = hand.resize((hw, hh), Image.LANCZOS)

        hx = int(round(tip_x - 86 * press))
        hy = int(round(tip_y - 6 * press))

        hand_layer = Image.new("RGBA", fr.size, (0, 0, 0, 0))
        hand_layer.alpha_composite(h_scaled, (hx, hy))
        fr = Image.alpha_composite(fr, fade_image(hand_layer, u))

    return fr.convert("RGB")

def generate_ambient_ocean_breeze(duration_sec, sample_rate, out_wav_path):
    """
    Synthesize gentle summer ocean wave / coastal breeze audio using ffmpeg pink noise & tremolo.
    """
    cmd = [
        "ffmpeg", "-y", "-loglevel", "warning",
        "-f", "lavfi",
        "-i", f"anoisesrc=d={duration_sec:.3f}:c=pink:r={sample_rate},lowpass=f=520,highpass=f=80,volume=0.075,tremolo=f=0.18:d=0.75,aformat=channel_layouts=stereo",
        out_wav_path
    ]
    subprocess.run(cmd, check=True)

def build_master_audio(raw_gameplay_path, sample_rate, channels, gp_dur, total_sec, click_path, out_wav_path):
    """
    Precision audio mastering:
    1. Synchronize gameplay audio with measured 850ms offset.
    2. Fade out gameplay audio smoothly as end card appears.
    3. Mix soothing ambient coastal breeze continuously.
    4. Inject crisp button click sound on exact hand tap frames in end card.
    """
    total_samples = int(math.ceil(total_sec * sample_rate))
    master = np.zeros((total_samples, 2), dtype=np.float32)

    # 1. Gameplay Audio (offset by +850ms)
    sync_offset_sec = 0.850
    sync_offset_samples = int(round(sync_offset_sec * sample_rate))

    raw_data = np.fromfile(raw_gameplay_path, dtype=np.float32).reshape(-1, channels)
    gp_samples = len(raw_data)

    # Fade out gameplay audio around gameplay end
    fade_start_sample = int(round(gp_dur * sample_rate))
    fade_len_samples = int(round(0.40 * sample_rate))

    # Apply fade out curve
    gp_processed = raw_data.copy()
    if fade_start_sample - sync_offset_samples < gp_samples:
        rel_fade_start = max(0, fade_start_sample - sync_offset_samples)
        fade_end = min(gp_samples, rel_fade_start + fade_len_samples)
        fade_curve = np.linspace(1.0, 0.0, fade_end - rel_fade_start, dtype=np.float32)[:, None]
        gp_processed[rel_fade_start:fade_end] *= fade_curve
        if fade_end < gp_samples:
            gp_processed[fade_end:] = 0.0

    # Place in master
    gp_dest_end = min(total_samples, sync_offset_samples + gp_samples)
    master[sync_offset_samples:gp_dest_end] += gp_processed[:gp_dest_end - sync_offset_samples]

    # 2. Coastal Ambient Breeze (Ocean whisper)
    amb_tmp = out_wav_path + ".amb.wav"
    generate_ambient_ocean_breeze(total_sec, sample_rate, amb_tmp)
    # Read ambient wav via ffmpeg into raw float32
    amb_raw_tmp = out_wav_path + ".amb.raw"
    subprocess.run(["ffmpeg", "-y", "-loglevel", "warning", "-i", amb_tmp, "-f", "f32le", "-ar", str(sample_rate), "-ac", "2", amb_raw_tmp], check=True)
    amb_data = np.fromfile(amb_raw_tmp, dtype=np.float32).reshape(-1, 2)
    amb_samples = min(total_samples, len(amb_data))
    master[:amb_samples] += amb_data[:amb_samples]
    try: os.remove(amb_tmp); os.remove(amb_raw_tmp)
    except: pass

    # 3. Button Clicks in End Card
    # Click audio
    click_raw_tmp = out_wav_path + ".click.raw"
    subprocess.run(["ffmpeg", "-y", "-loglevel", "warning", "-i", click_path, "-f", "f32le", "-ar", str(sample_rate), "-ac", "2", click_raw_tmp], check=True)
    click_data = np.fromfile(click_raw_tmp, dtype=np.float32).reshape(-1, 2)
    click_len = len(click_data)
    try: os.remove(click_raw_tmp)
    except: pass

    # Hand tap frames at 60 fps: 78, 122, 166, 210, 254
    tap_frames = [78, 122, 166, 210, 254]
    for tf in tap_frames:
        t_click = gp_dur + (tf / 60.0)
        c_idx = int(round(t_click * sample_rate))
        c_end = min(total_samples, c_idx + click_len)
        chunk_len = c_end - c_idx
        if chunk_len > 0:
            master[c_idx:c_end] += click_data[:chunk_len] * 0.85

    # Peak normalization / limiter to prevent clipping
    max_val = np.max(np.abs(master))
    if max_val > 0.95:
        master = master * (0.95 / max_val)

    # Write as 16-bit PCM WAV
    master_16 = np.clip(master * 32767.0, -32768.0, 32767.0).astype(np.int16)
    import wave
    with wave.open(out_wav_path, "wb") as wf:
        wf.setnchannels(2)
        wf.setsampwidth(2)
        wf.setframerate(sample_rate)
        wf.writeframes(master_16.tobytes())

def main():
    rec_dir = os.path.expanduser("/home/emre/Belgeler/GitHub/PixelGame/scratch/gameplay_recording")
    meta_path = os.path.join(rec_dir, "metadata.json")

    if not os.path.exists(meta_path):
        print(f"Error: {meta_path} not found!")
        sys.exit(1)

    with open(meta_path) as f:
        meta = json.load(f)

    frame_count = meta["frameCount"]
    target_fps = 60
    sample_rate = meta.get("sampleRate", 48000)
    channels = meta.get("channels", 2)
    audio_file = os.path.join(rec_dir, meta.get("audioFile", "audio_f32le.raw"))

    # Compute exact audio duration (ground truth real-time)
    raw_size = os.path.getsize(audio_file)
    audio_dur = raw_size / float(sample_rate * channels * 4)
    real_rec_fps = frame_count / audio_dur

    print(f"Recorded frames: {frame_count}, Ground truth audio duration: {audio_dur:.3f}s")
    print(f"Capture real-time frame rate: {real_rec_fps:.3f} fps -> Output: {target_fps} fps")

    # Load endcard PNG layers
    layers_dir = os.path.expanduser("~/Resimler/PixelGame_Ikon/kapanis_karti")
    layers = {
        "dark": Image.open(os.path.join(layers_dir, "1_karartma_1080x1920.png")).convert("RGBA"),
        "head_tr": Image.open(os.path.join(layers_dir, "2_baslik_tr_1080x1920.png")).convert("RGBA"),
        "head_en": Image.open(os.path.join(layers_dir, "2_baslik_en_1080x1920.png")).convert("RGBA"),
        "icon": Image.open(os.path.join(layers_dir, "3_ikon_cerceveli_458x458.png")).convert("RGBA"),
        "name": Image.open(os.path.join(layers_dir, "4_landflow_yazi_760x264.png")).convert("RGBA"),
        "btn_tr": Image.open(os.path.join(layers_dir, "5_buton_tr_664x204.png")).convert("RGBA"),
        "btn_en": Image.open(os.path.join(layers_dir, "5_buton_en_664x204.png")).convert("RGBA"),
        "hand": Image.open(os.path.join(layers_dir, "6_el_210x246.png")).convert("RGBA"),
    }

    last_frame_path = os.path.join(rec_dir, meta.get("lastFrame", f"frames/f_{frame_count-1:05d}.jpg"))
    if not os.path.exists(last_frame_path):
        last_frame_path = os.path.join(rec_dir, "frames", f"f_{frame_count-1:05d}.jpg")
    print(f"Using last gameplay frame: {last_frame_path}")
    last_frame_img = Image.open(last_frame_path).convert("RGBA")

    # 1. Render gameplay at 60 fps using ffmpeg framerate motion-blend filter
    gameplay_mp4 = os.path.join(rec_dir, "gameplay_60fps.mp4")
    print(f"Rendering silky smooth 60fps gameplay to {gameplay_mp4}...")
    cmd_gp = [
        "ffmpeg", "-y", "-loglevel", "warning",
        "-framerate", str(real_rec_fps),
        "-i", os.path.join(rec_dir, "frames", "f_%05d.jpg"),
        "-vf", "framerate=fps=60",
        "-c:v", "libx264", "-crf", "16", "-preset", "fast", "-pix_fmt", "yuv420p",
        gameplay_mp4
    ]
    subprocess.run(cmd_gp, check=True)

    # Get exact duration of gameplay video
    probe_cmd = ["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", gameplay_mp4]
    gp_dur_str = subprocess.check_output(probe_cmd).decode().strip()
    gp_dur = float(gp_dur_str)
    print(f"Gameplay 60fps video rendered: {gp_dur:.3f}s")

    # End card duration: 4.5 seconds (270 frames at 60 fps)
    end_card_frames = 270
    end_card_sec = end_card_frames / float(target_fps)
    total_sec = gp_dur + end_card_sec

    # 2. Master Audio
    click_path = os.path.expanduser("/home/emre/Belgeler/GitHub/PixelGame/Assets/Resources/Audio/ui_button_click.mp3")
    master_audio_wav = os.path.join(rec_dir, "master_audio_synced.wav")
    print(f"Building master synced audio with ambient breeze and end-card clicks -> {master_audio_wav}...")
    build_master_audio(audio_file, sample_rate, channels, gp_dur, total_sec, click_path, master_audio_wav)

    output_dir = os.path.expanduser("/home/emre/Belgeler/GitHub/PixelGame/scratch")
    final_dest_dir = os.path.expanduser("/home/emre/Videolar/PixelGame_Reklam")
    os.makedirs(final_dest_dir, exist_ok=True)

    configs = [
        {"lang": "tr", "show_headline": True, "label": "TR"},
        {"lang": "en", "show_headline": False, "label": "EN (No 'Can You Do Better' Headline)"},
    ]

    for cfg in configs:
        lang = cfg["lang"]
        show_headline = cfg["show_headline"]
        label = cfg["label"]

        tmp_ec_dir = tempfile.mkdtemp(prefix=f"endcard_60fps_{lang}_")
        print(f"\n--- Generating 60fps end card frames for [{label}] in {tmp_ec_dir} ---")

        for k in range(end_card_frames):
            ec_img = build_end_card_frame(last_frame_img, k, layers, lang=lang, show_headline=show_headline)
            ec_img.save(os.path.join(tmp_ec_dir, f"ec_{k:05d}.jpg"), quality=95)

        endcard_mp4 = os.path.join(tmp_ec_dir, "endcard_60fps.mp4")
        cmd_ec = [
            "ffmpeg", "-y", "-loglevel", "warning",
            "-framerate", "60",
            "-i", os.path.join(tmp_ec_dir, "ec_%05d.jpg"),
            "-c:v", "libx264", "-crf", "16", "-preset", "fast", "-pix_fmt", "yuv420p",
            endcard_mp4
        ]
        subprocess.run(cmd_ec, check=True)

        out_mp4 = os.path.join(output_dir, f"PixelGame_Creative_ASMR_{lang.upper()}.mp4")

        # Concat gameplay + endcard and mux master audio
        cmd_final = [
            "ffmpeg", "-y", "-loglevel", "warning",
            "-i", gameplay_mp4,
            "-i", endcard_mp4,
            "-i", master_audio_wav,
            "-filter_complex", "[0:v][1:v]concat=n=2:v=1:a=0[vout]",
            "-map", "[vout]", "-map", "2:a",
            "-c:v", "libx264", "-profile:v", "high", "-pix_fmt", "yuv420p", "-crf", "17", "-preset", "medium",
            "-r", "60",
            "-movflags", "+faststart",
            "-c:a", "aac", "-b:a", "192k",
            out_mp4
        ]

        print(f"Concatenating and producing final 60fps video: {out_mp4}...")
        subprocess.run(cmd_final, check=True)
        shutil.rmtree(tmp_ec_dir)
        print(f"Successfully generated: {out_mp4} ({os.path.getsize(out_mp4) / 1024 / 1024:.2f} MB)")

        # Copy to user's video folder
        dest_mp4 = os.path.join(final_dest_dir, f"PixelGame_Creative_ASMR_{lang.upper()}.mp4")
        shutil.copy2(out_mp4, dest_mp4)
        print(f"Copied to: {dest_mp4}")

    print("\n🎉 ALL ASMR CREATIVE AD VIDEOS SUCCESSFULLY PRODUCED AT 60 FPS!")

if __name__ == "__main__":
    main()
