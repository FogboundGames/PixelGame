"""Fail reklamı (gerçek hız): yanlış (kızarma + sarsıntı) → doğru (yıldız) → yanlış → yanlış → donma + "Nasıl ya?!" →
oyunun Level Failed ekranı → logolu kapanış. El göstergesi her dokunuşa gider.

Kullanım: python3 build_fail.py <ayar.json> <en|tr> <çıktı.mp4>
ayar.json: {"src": klasör, "wrong": [[kare, slot_x], ...], "right": [kare, slot_x], "stuck": kare,
            "fail": [ilk, son], "slot_y": 1287, "end_hold": 150}
"""
import json, math, os, subprocess, sys, tempfile
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ad_fx
from make_overlays import outlined_lines

FPS = 30
SHOCK_HOLD, FAIL_HOLD = 30, 18
FLASH_LEN, SPARK_LEN = 20, 28
TEXTS = {
    "en": dict(hook=["EASIEST LEVEL", "EVER?"], shock=["WAIT...", "WHAT?!"], end=["CAN YOU", "DO BETTER?"], cta="PLAY NOW"),
    "tr": dict(hook=["DÜNYANIN EN", "KOLAY BÖLÜMÜ?"], shock=["NASIL YA?!"], end=["SEN DAHA", "İYİ YAPAR MISIN?"], cta="HEMEN OYNA"),
}


def flash_alpha(k):
    x = k / FLASH_LEN
    return max(0.0, math.sin(x * math.pi * 2)) if x < 1 else 0.0


def main():
    cfg = json.load(open(sys.argv[1]))
    lang, out = sys.argv[2], sys.argv[3]
    src = os.path.expanduser(cfg["src"]).rstrip("/") + "/"
    wrong, right = cfg["wrong"], cfg["right"]
    stuck, (fail_a, fail_b) = cfg["stuck"], cfg["fail"]
    slot_y, end_hold = cfg.get("slot_y", 1287), cfg.get("end_hold", 150)
    t = TEXTS[lang]
    hook = outlined_lines(t["hook"], 104, 40, max_w=1000, band=True)
    shock = outlined_lines(t["shock"], 150, 660, fill=(255, 236, 90), stroke=(120, 20, 20), sw=16)
    hand, tip = ad_fx.make_hand()
    flash = ad_fx.red_flash_layer()
    base, btn, bcenter = ad_fx.end_card_parts(t["end"], t["cta"])
    taps = [(int(p[1]), int(p[2]), int(p[3])) for p in (ln.split() for ln in open(src + "taps.txt")) if p and p[0] == "TAP"]
    track = ad_fx.HandTrack(taps, hand, tip)

    plan = [(i, "play") for i in range(stuck + 1)]
    plan += [(stuck, "shock")] * SHOCK_HOLD
    plan += [(i, "fail") for i in range(fail_a, fail_b + 1)] + [(fail_b, "fail")] * FAIL_HOLD
    plan += [(stuck, "end")] * end_hold
    shock_start = stuck + 1
    end_start = len(plan) - end_hold
    hook_end = taps[0][0] + 4

    tmp = tempfile.mkdtemp(prefix="failbuild_", dir=os.path.dirname(src.rstrip("/")))
    for i, (s, kind) in enumerate(plan):
        fr = Image.open(f"{src}f_{s:05d}.jpg").convert("RGBA")
        if kind == "play":
            dx = dy = 0
            for (f0, sx) in wrong:
                k = i - f0
                if 0 <= k < FLASH_LEN:
                    fr = Image.alpha_composite(fr, ad_fx.fade(flash, flash_alpha(k)))
                ox, oy = ad_fx.shake_offset(k, 10, 14)
                dx += ox; dy += oy
            fr = ad_fx.apply_shake(fr, dx, dy)
            k = i - right[0]
            if 0 <= k < SPARK_LEN:
                fr = ad_fx.sparkles(fr, k, right[1], slot_y)
            if i < hook_end:
                fr = Image.alpha_composite(fr, ad_fx.fade(hook, min(1.0, (hook_end - i) / 6)))
            fr = ad_fx.tap_ripple(fr, i, taps)
            fr = track.draw(fr, i)
        elif kind == "shock":
            k = i - shock_start
            fr = ad_fx.apply_shake(fr, *ad_fx.shake_offset(k, 12, 18))
            sc = 1.0 + 0.12 * max(0, 1 - k / 5)
            sh = shock.resize((int(1080 * sc), int(1920 * sc)))
            layer = Image.new("RGBA", (1080, 1920))
            layer.alpha_composite(sh, ((1080 - sh.width) // 2, (1920 - sh.height) // 2))
            fr = Image.alpha_composite(fr, ad_fx.fade(layer, min(1.0, (k + 1) / 3)))
        elif kind == "end":
            fr = ad_fx.draw_end(fr, i - end_start, base, btn, bcenter, (hand, tip))
        fr.convert("RGB").save(f"{tmp}/o_{i:05d}.jpg", quality=92)

    play_s = (stuck + 1) / FPS
    af = (f"[1:a]atrim=0:{play_s},asetpts=PTS-STARTPTS,afade=t=out:st={play_s - 0.08}:d=0.08[a0];"
          f"anullsrc=r=48000:cl=stereo,atrim=0:{SHOCK_HOLD / FPS}[s1];"
          f"[1:a]atrim={fail_a / FPS}:{(fail_b + 1) / FPS},asetpts=PTS-STARTPTS[a2];"
          f"anullsrc=r=48000:cl=stereo,atrim=0:{(FAIL_HOLD + end_hold) / FPS}[s3];"
          f"[a0][s1][a2][s3]concat=n=4:v=0:a=1,alimiter=limit=0.89,loudnorm=I=-16:TP=-1.5:LRA=11,aresample=48000[aout]")
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-framerate", str(FPS), "-i", f"{tmp}/o_%05d.jpg",
                    "-f", "f32le", "-ar", "48000", "-ac", "2", "-i", os.path.join(src, "audio_f32le_48k_2ch.raw"),
                    "-filter_complex", af, "-map", "0:v", "-map", "[aout]", "-shortest",
                    "-c:v", "libx264", "-profile:v", "high", "-pix_fmt", "yuv420p", "-crf", "18", "-preset", "slow",
                    "-movflags", "+faststart", "-c:a", "aac", "-b:a", "160k", out], check=True)
    for f in os.listdir(tmp):
        os.remove(os.path.join(tmp, f))
    os.rmdir(tmp)
    print(out, f"{len(plan) / FPS:.1f}s")


if __name__ == "__main__":
    main()
