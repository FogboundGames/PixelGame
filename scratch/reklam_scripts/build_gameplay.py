"""Pure gameplay reklamı: gerçek hız (hızlandırma yok), el göstergesi + dokunma dalgası, hook / ara yazı, logolu kapanış.

Kullanım: python3 build_gameplay.py <ham kare klasörü> <son kare> <en|tr> <konu: pumpkin|capybara> <çıktı.mp4>
taps.txt içindeki TAP ve SPEED satırları el göstergesinin gittiği noktalardır.
"""
import os, subprocess, sys, tempfile
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ad_fx
from make_overlays import outlined_lines

FPS = 30
HOLD = int(os.environ.get("HOLD", "90"))   # kapanış kartı (kare); 90 = 3 sn
FADE = 6
TEXTS = {
    ("pumpkin", "en"): dict(hook=["CAN YOU CLEAR", "THIS PUMPKIN?"], mid=["SEND THE", "RIGHT COLOR!"], end=["CAN YOU", "FINISH IT?"], cta="PLAY NOW"),
    ("pumpkin", "tr"): dict(hook=["BU BALKABAĞINI", "BİTİREBİLİR MİSİN?"], mid=["DOĞRU RENGİ", "GÖNDER!"], end=["SEN", "BİTİREBİLİR MİSİN?"], cta="HEMEN OYNA"),
    ("capybara", "en"): dict(hook=["CAN YOU CLEAR", "THIS CAPYBARA?"], mid=["SEND THE", "RIGHT COLOR!"], end=["CAN YOU", "FINISH IT?"], cta="PLAY NOW"),
    ("capybara", "tr"): dict(hook=["BU KAPİBARAYI", "BİTİREBİLİR MİSİN?"], mid=["DOĞRU RENGİ", "GÖNDER!"], end=["SEN", "BİTİREBİLİR MİSİN?"], cta="HEMEN OYNA"),
}


def main():
    src, src_end, lang, topic, out = sys.argv[1].rstrip("/") + "/", int(sys.argv[2]), sys.argv[3], sys.argv[4], sys.argv[5]
    t = TEXTS[(topic, lang)]
    hook = outlined_lines(t["hook"], 104, 40, max_w=1000, band=True)
    mid = outlined_lines(t["mid"], 100, 40, max_w=1000, band=True)
    hand, tip = ad_fx.make_hand()
    base, btn, bcenter = ad_fx.end_card_parts(t["end"], t["cta"])
    taps = []
    for ln in open(src + "taps.txt"):
        p = ln.split()
        if p and p[0] in ("TAP", "SPEED") and int(p[1]) <= src_end:
            taps.append((int(p[1]), int(p[2]), int(p[3])))
    track = ad_fx.HandTrack(taps, hand, tip)
    hook_end = 96
    mid_a, mid_b = 270, 345
    total = src_end + 1 + HOLD
    tmp = tempfile.mkdtemp(prefix="gpbuild_", dir=os.path.dirname(src.rstrip("/")))
    for i in range(total):
        s = min(i, src_end)
        fr = Image.open(f"{src}f_{s:05d}.jpg").convert("RGBA")
        if i <= src_end:
            if i < hook_end:
                fr = Image.alpha_composite(fr, ad_fx.fade(hook, min(1.0, (hook_end - i) / FADE)))
            if mid_a <= i < mid_b:
                fr = Image.alpha_composite(fr, ad_fx.fade(mid, min(1.0, (i - mid_a + 1) / FADE, (mid_b - i) / FADE)))
            fr = ad_fx.tap_ripple(fr, i, taps)
            fr = track.draw(fr, i)
        else:
            fr = ad_fx.draw_end(fr, i - src_end - 1, base, btn, bcenter, (hand, tip))
        fr.convert("RGB").save(f"{tmp}/o_{i:05d}.jpg", quality=92)
    end_s = (src_end + 1) / FPS
    hold_s = HOLD / FPS
    af = (f"[1:a]apad,atrim=0:{end_s + hold_s},asetpts=PTS-STARTPTS,afade=t=out:st={end_s + 0.4}:d={hold_s - 0.4},"
          f"alimiter=limit=0.89,loudnorm=I=-16:TP=-1.5:LRA=11,aresample=48000[aout]")
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-framerate", str(FPS), "-i", f"{tmp}/o_%05d.jpg",
                    "-f", "f32le", "-ar", "48000", "-ac", "2", "-i", os.path.join(src, "audio_f32le_48k_2ch.raw"),
                    "-filter_complex", af, "-map", "0:v", "-map", "[aout]", "-shortest",
                    "-c:v", "libx264", "-profile:v", "high", "-pix_fmt", "yuv420p", "-crf", "18", "-preset", "slow",
                    "-movflags", "+faststart", "-c:a", "aac", "-b:a", "160k", out], check=True)
    for f in os.listdir(tmp):
        os.remove(os.path.join(tmp, f))
    os.rmdir(tmp)
    print(out, f"{total / FPS:.1f}s", len(taps), "dokunuş")


if __name__ == "__main__":
    main()
