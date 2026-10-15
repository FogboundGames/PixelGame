import os
import sys
import io
import time
import uuid
import base64
import urllib.request
import urllib.parse
from PIL import Image, ImageDraw, ImageFont
import numpy as np

sys.stdout.reconfigure(encoding='utf-8')

ITEMS = [
    # 🌊 Deniz & Marina
    {"id": "Ahtapot", "name": "Ahtapot", "category": "Deniz & Marina", "badge": "marina", "emoji": "🐙", "colors": 5, "desc": "Sevimli mor ahtapot, büyük gözler"},
    {"id": "Yengec", "name": "Yengeç", "category": "Deniz & Marina", "badge": "marina", "emoji": "🦀", "colors": 5, "desc": "Kıskaçlı canlı kırmızı sahil yengeci"},
    {"id": "BalonBaligi", "name": "Balon Balığı", "category": "Deniz & Marina", "badge": "marina", "emoji": "🐡", "colors": 5, "desc": "Pofuduk sarı-turuncu kirpi balığı"},
    {"id": "Denizyildizi", "name": "Denizyıldızı", "category": "Deniz & Marina", "badge": "marina", "emoji": "⭐", "colors": 4, "desc": "Parlak altın sarısı 5 kollu denizyıldızı"},
    {"id": "Kopekbaligi", "name": "Köpekbalığı", "category": "Deniz & Marina", "badge": "marina", "emoji": "🦈", "colors": 4, "desc": "Mavi-gri sevimli yavru köpekbalığı"},

    # 🍩 Lezzetler & Yiyecekler
    {"id": "Donut", "name": "Donut", "category": "Lezzetler & Tatlılar", "badge": "food", "emoji": "🍩", "colors": 5, "desc": "Çilek soslu ve renkli şekerlemeli donut"},
    {"id": "Dondurma", "name": "Dondurma", "category": "Lezzetler & Tatlılar", "badge": "food", "emoji": "🍦", "colors": 4, "desc": "Külah üzerinde kıvrımlı leziz dondurma"},
    {"id": "Muz", "name": "Muz", "category": "Lezzetler & Tatlılar", "badge": "food", "emoji": "🍌", "colors": 4, "desc": "Olgun tropikal sarı muz demeti"},
    {"id": "Kahve", "name": "Kahve", "category": "Lezzetler & Tatlılar", "badge": "food", "emoji": "☕", "colors": 5, "desc": "Sıcak buharı tüten seramik kahve kupası"},
    {"id": "Susi", "name": "Suşi", "category": "Lezzetler & Tatlılar", "badge": "food", "emoji": "🍣", "colors": 4, "desc": "Somonlu taze nigiri suşi ve nori yosunu"},
    {"id": "Lolipop", "name": "Lolipop", "category": "Lezzetler & Tatlılar", "badge": "food", "emoji": "🍭", "colors": 5, "desc": "Spiralli renkli lolipop şekeri"},

    # 🐾 Sevimli Hayvanlar
    {"id": "Panda", "name": "Panda", "category": "Sevimli Hayvanlar", "badge": "animal", "emoji": "🐼", "colors": 4, "desc": "Siyah-beyaz sevimli panda yüzü"},
    {"id": "Kurbaga", "name": "Kurbağa", "category": "Sevimli Hayvanlar", "badge": "animal", "emoji": "🐸", "colors": 4, "desc": "Koca gözlü neşeli yeşil kurbağa"},
    {"id": "Penguen", "name": "Penguen", "category": "Sevimli Hayvanlar", "badge": "animal", "emoji": "🐧", "colors": 4, "desc": "Tombul minik kutup pengueni"},
    {"id": "Civciv", "name": "Civciv", "category": "Sevimli Hayvanlar", "badge": "animal", "emoji": "🐥", "colors": 4, "desc": "Sarı pofuduk sevimli yavru civciv"},
    {"id": "Koala", "name": "Koala", "category": "Sevimli Hayvanlar", "badge": "animal", "emoji": "🐨", "colors": 4, "desc": "Gri tüylü, sevimli uykucu koala"},

    # ⚔️ Macera & Retro Oyun
    {"id": "OyunKolu", "name": "Oyun Kolu", "category": "Macera & Retro", "badge": "adventure", "emoji": "🎮", "colors": 5, "desc": "Retro arcade oyun kolu ve renkli butonlar"},
    {"id": "SihirIksiri", "name": "Sihir İksiri", "category": "Macera & Retro", "badge": "adventure", "emoji": "🧪", "colors": 5, "desc": "Mor-turkuaz parıldayan simya iksiri"},
    {"id": "Tac", "name": "Kral Tacı", "category": "Macera & Retro", "badge": "adventure", "emoji": "👑", "colors": 5, "desc": "Mücevherli altın kraliyet tacı"},
    {"id": "Gitar", "name": "Elektro Gitar", "category": "Macera & Retro", "badge": "adventure", "emoji": "🎸", "colors": 5, "desc": "Ateş kırmızısı rock elektro gitar"},
    {"id": "Gokkusagi", "name": "Gökkuşağı", "category": "Macera & Retro", "badge": "adventure", "emoji": "🌈", "colors": 5, "desc": "Bulutlu renkli gökkuşağı yayları"},
    {"id": "SuperYildiz", "name": "Süper Yıldız", "category": "Macera & Retro", "badge": "adventure", "emoji": "🌟", "colors": 4, "desc": "Parıltılı retro sarı güç yıldızı"},
]

def fetch_emoji_image(emoji_char, retries=4):
    encoded = urllib.parse.quote(emoji_char)
    urls = [
        f"https://emojicdn.elk.sh/{encoded}?style=twitter",
        f"https://raw.githubusercontent.com/twitter/twemoji/master/assets/72x72/{ord(emoji_char[0]):x}.png"
    ]
    for url in urls:
        for attempt in range(retries):
            try:
                req = urllib.request.Request(url, headers={'User-Agent': 'PixelGame/1.0'})
                with urllib.request.urlopen(req, timeout=12) as resp:
                    data = resp.read()
                return Image.open(io.BytesIO(data)).convert('RGBA')
            except Exception:
                time.sleep(1)
    return None

def quantize_kmeans(arr, k_colors):
    opaque_mask = arr[:, :, 3] >= 64
    valid_coords = np.argwhere(opaque_mask)
    if len(valid_coords) == 0:
        return arr, []
    
    pixels = arr[opaque_mask][:, :3].astype(np.float32)
    k = min(k_colors, len(np.unique(pixels, axis=0)))
    if k <= 1:
        return arr, [pixels[0].astype(np.uint8)]
    
    # KMeans++
    np.random.seed(42)
    centroids = [pixels[np.random.randint(len(pixels))]]
    for _ in range(1, k):
        dists = np.min([np.sum((pixels - c)**2, axis=1) for c in centroids], axis=0)
        probs = dists / (np.sum(dists) + 1e-6)
        next_c = pixels[np.random.choice(len(pixels), p=probs)]
        centroids.append(next_c)
    centroids = np.array(centroids)
    
    # Iterations
    for _ in range(15):
        dists = np.array([np.sum((pixels - c)**2, axis=1) for c in centroids])
        labels = np.argmin(dists, axis=0)
        new_centroids = []
        for i in range(k):
            pts = pixels[labels == i]
            if len(pts) > 0:
                new_centroids.append(pts.mean(axis=0))
            else:
                new_centroids.append(centroids[i])
        new_centroids = np.array(new_centroids)
        if np.allclose(centroids, new_centroids, atol=0.5):
            break
        centroids = new_centroids
        
    centroids = np.round(centroids).astype(np.uint8)
    
    # Assign colors
    out_arr = np.zeros_like(arr)
    dists = np.array([np.sum((pixels - c)**2, axis=1) for c in centroids])
    labels = np.argmin(dists, axis=0)
    for idx, (y, x) in enumerate(valid_coords):
        c = centroids[labels[idx]]
        out_arr[y, x] = [c[0], c[1], c[2], 255]
        
    return out_arr, centroids

def denoise_isolated(arr):
    h, w = arr.shape[:2]
    out = arr.copy()
    dx = [-1, 0, 1, -1, 1, -1, 0, 1]
    dy = [-1, -1, -1, 0, 0, 1, 1, 1]
    
    for y in range(h):
        for x in range(w):
            if arr[y, x, 3] < 128:
                continue
            cur_rgb = arr[y, x, :3]
            
            same_count = 0
            neighbor_colors = []
            for i in range(8):
                nx, ny = x + dx[i], y + dy[i]
                if 0 <= nx < w and 0 <= ny < h:
                    if arr[ny, nx, 3] >= 128:
                        n_rgb = arr[ny, nx, :3]
                        if np.array_equal(cur_rgb, n_rgb):
                            same_count += 1
                        else:
                            neighbor_colors.append(tuple(n_rgb))
            
            if same_count <= 1 and len(neighbor_colors) >= 3:
                from collections import Counter
                most_common = Counter(neighbor_colors).most_common(1)[0][0]
                out[y, x, :3] = most_common
    return out

def process_image(raw_img, size, k_colors):
    inner_size = size - 2
    raw_w, raw_h = raw_img.size
    aspect = raw_w / raw_h
    if aspect >= 1.0:
        new_w = inner_size
        new_h = max(1, int(inner_size / aspect))
    else:
        new_h = inner_size
        new_w = max(1, int(inner_size * aspect))
        
    scaled = raw_img.resize((new_w, new_h), Image.Resampling.LANCZOS)
    
    canvas = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    offset_x = (size - new_w) // 2
    offset_y = (size - new_h) // 2
    canvas.paste(scaled, (offset_x, offset_y))
    
    arr = np.array(canvas)
    mask = arr[:, :, 3] >= 70
    arr[~mask] = [0, 0, 0, 0]
    arr[mask, 3] = 255
    
    q_arr, palette = quantize_kmeans(arr, k_colors)
    d_arr = denoise_isolated(q_arr)
    
    return Image.fromarray(d_arr), palette

def generate_unity_meta(guid):
    return f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 1
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 0
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData: 
    physicsShape: []
    bones: []
    spriteID: {guid[:16]}0800000000000000
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

def save_with_meta(img, file_path):
    os.makedirs(os.path.dirname(file_path), exist_ok=True)
    img.save(file_path)
    meta_path = file_path + ".meta"
    if not os.path.exists(meta_path):
        guid = uuid.uuid4().hex
        with open(meta_path, "w", encoding="utf-8") as f:
            f.write(generate_unity_meta(guid))

def to_base64_data_uri(img):
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    b64 = base64.b64encode(buf.getvalue()).decode("utf-8")
    return f"data:image/png;base64,{b64}"

def rgb_to_hex(rgb):
    return f"#{int(rgb[0]):02x}{int(rgb[1]):02x}{int(rgb[2]):02x}"

def main():
    print(f"=== Starting Pixel Art Generation ({len(ITEMS)} items) ===")
    
    results = []
    
    for idx, item in enumerate(ITEMS, 1):
        item_id = item["id"]
        emoji = item["emoji"]
        target_k = item["colors"]
        print(f"[{idx}/{len(ITEMS)}] Processing '{item['name']}' ({item_id}) ...")
        
        raw_img = fetch_emoji_image(emoji)
        if raw_img is None:
            print(f"  FAILED to fetch {item_id}, skipping.")
            continue
            
        # 1. 25x25
        img25, pal25 = process_image(raw_img, 25, target_k)
        path25 = f"Assets/25x25/{item_id}.png"
        save_with_meta(img25, path25)
        
        # 2. 32x32
        img32, pal32 = process_image(raw_img, 32, target_k)
        path32 = f"Assets/32x32/{item_id}.png"
        save_with_meta(img32, path32)
        
        # 3. Optimized (_4c / _5c)
        path_opt = f"Assets/PixelArt/Optimized/{item_id}_{target_k}c.png"
        save_with_meta(img25, path_opt)
        
        b64_data = to_base64_data_uri(img25)
        hex_colors = [rgb_to_hex(c) for c in pal25]
        
        results.append({
            "spec": item,
            "img25": img25,
            "img32": img32,
            "pal25": pal25,
            "hex_colors": hex_colors,
            "b64": b64_data,
            "path25": path25,
            "path32": path32,
            "path_opt": path_opt
        })
        print(f"  Saved 25x25, 32x32, and Optimized ({len(pal25)} colors).")
        
    print(f"\nSuccessfully generated {len(results)}/{len(ITEMS)} pixel art visuals!")
    
    create_showcase_sheet(results)
    create_html_gallery(results)

def create_showcase_sheet(results):
    if not results:
        return
    print("Generating visual showcase sheet...")
    
    try:
        font_title = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 28)
        font_sub = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 16)
        font_cat = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 13)
        font_name = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 17)
        font_label = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 12)
        font_desc = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 12)
    except Exception:
        font_title = font_sub = font_cat = font_name = font_label = font_desc = ImageFont.load_default()
    
    cols = 4
    rows = (len(results) + cols - 1) // cols
    card_w = 280
    card_h = 340
    margin = 24
    
    total_w = cols * card_w + (cols + 1) * margin
    total_h = rows * card_h + (rows + 1) * margin + 130
    
    sheet = Image.new("RGBA", (total_w, total_h), (16, 20, 28, 255))
    draw = ImageDraw.Draw(sheet)
    
    # Header
    draw.rectangle([0, 0, total_w, 110], fill=(24, 30, 46, 255))
    draw.line([(0, 110), (total_w, 110)], fill=(55, 75, 115, 255), width=2)
    
    draw.text((margin, 20), "PIXEL GAME — YENİ SEVİYE PİKSEL GÖRSELLERİ", font=font_title, fill=(255, 255, 255, 255))
    draw.text((margin, 62), f"Toplam {len(results)} Adet Yeni 25x25 & 32x32 Piksel Sanatı | K-Means Renk İndirgemeli, Gemi Kargo Mekaniğine Tam Uyumlu", font=font_sub, fill=(130, 175, 235, 255))
    
    for idx, res in enumerate(results):
        r = idx // cols
        c = idx % cols
        x = margin + c * (card_w + margin)
        y = 130 + r * (card_h + margin)
        
        draw.rounded_rectangle([x, y, x + card_w, y + card_h], radius=14, fill=(26, 33, 48, 255), outline=(45, 60, 88, 255), width=2)
        
        cat = res["spec"]["category"]
        cat_color = (60, 140, 220) if "Deniz" in cat else ((220, 90, 120) if "Lezzet" in cat else ((80, 190, 120) if "Hayvan" in cat else (220, 160, 40)))
        draw.rounded_rectangle([x + 14, y + 14, x + card_w - 14, y + 38], radius=6, fill=(cat_color[0]//4, cat_color[1]//4, cat_color[2]//4, 255), outline=cat_color, width=1)
        draw.text((x + 22, y + 17), cat, font=font_cat, fill=(225, 238, 255, 255))
        
        name = res["spec"]["name"]
        draw.text((x + 16, y + 46), name, font=font_name, fill=(255, 255, 255, 255))
        
        scale = 5
        sprite = res["img25"]
        upscaled = sprite.resize((25 * scale, 25 * scale), Image.Resampling.NEAREST)
        
        box_x = x + (card_w - 25 * scale) // 2
        box_y = y + 78
        draw.rectangle([box_x - 4, box_y - 4, box_x + 25 * scale + 4, box_y + 25 * scale + 4], fill=(18, 22, 32, 255), outline=(65, 82, 118, 255), width=1)
        
        chk_size = 10
        for cy in range(0, 25 * scale, chk_size):
            for cx in range(0, 25 * scale, chk_size):
                color = (32, 38, 54) if ((cx // chk_size) + (cy // chk_size)) % 2 == 0 else (24, 28, 40)
                draw.rectangle([box_x + cx, box_y + cy, box_x + cx + chk_size, box_y + cy + chk_size], fill=color)
                
        sheet.paste(upscaled, (box_x, box_y), upscaled)
        
        swatch_y = box_y + 25 * scale + 14
        draw.text((x + 16, swatch_y), f"Renk Paleti ({len(res['pal25'])} renk):", font=font_label, fill=(160, 180, 210, 255))
        
        pal = res["pal25"]
        sw_w = 30
        sw_h = 16
        sw_x = x + 16
        for c_idx, rgb in enumerate(pal[:7]):
            draw.rectangle([sw_x + c_idx * (sw_w + 5), swatch_y + 20, sw_x + c_idx * (sw_w + 5) + sw_w, swatch_y + 20 + sw_h], 
                           fill=(int(rgb[0]), int(rgb[1]), int(rgb[2]), 255), outline=(255, 255, 255, 140), width=1)
            
        desc = res["spec"]["desc"]
        draw.text((x + 16, swatch_y + 44), desc, font=font_desc, fill=(130, 148, 175, 255))
        
    os.makedirs("Assets/Screenshots", exist_ok=True)
    showcase_path = "Assets/Screenshots/PixelArtShowcase.png"
    sheet.save(showcase_path)
    print(f"Showcase sheet saved to: {showcase_path}")

def create_html_gallery(results):
    print("Generating standalone HTML gallery with embedded base64 data...")
    import json
    
    items_json = []
    for r in results:
        items_json.append({
            "id": r["spec"]["id"],
            "name": r["spec"]["name"],
            "cat": r["spec"]["category"],
            "badge": r["spec"]["badge"],
            "desc": r["spec"]["desc"],
            "b64": r["b64"],
            "colors": r["hex_colors"],
            "path25": r["path25"],
            "path32": r["path32"]
        })
        
    html = f"""<!DOCTYPE html>
<html lang="tr">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Pixel Game — Yeni Seviye Piksel Görselleri Galerisi</title>
  <style>
    :root {{
      --bg: #0d1117;
      --card-bg: #161b22;
      --card-border: #30363d;
      --text: #f0f6fc;
      --text-muted: #8b949e;
      --accent: #58a6ff;
    }}
    * {{ box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif; }}
    body {{ background-color: var(--bg); color: var(--text); padding: 32px 24px; }}
    header {{ max-width: 1240px; margin: 0 auto 32px; text-align: center; }}
    h1 {{ font-size: 2.2rem; font-weight: 800; margin-bottom: 8px; background: linear-gradient(90deg, #58a6ff, #3fb950, #f0883e); -webkit-background-clip: text; -webkit-text-fill-color: transparent; }}
    p.subtitle {{ color: var(--text-muted); font-size: 1.05rem; }}
    .filters {{ display: flex; justify-content: center; gap: 10px; margin-top: 20px; flex-wrap: wrap; }}
    .filter-btn {{ background: #21262d; border: 1px solid var(--card-border); color: var(--text); padding: 8px 16px; border-radius: 20px; cursor: pointer; font-size: 0.9rem; transition: all 0.2s; }}
    .filter-btn:hover, .filter-btn.active {{ background: #388bfd; border-color: #58a6ff; }}
    .gallery {{ max-width: 1240px; margin: 0 auto; display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 20px; }}
    .card {{ background: var(--card-bg); border: 1px solid var(--card-border); border-radius: 12px; padding: 18px; display: flex; flex-direction: column; align-items: center; transition: transform 0.2s, border-color 0.2s; position: relative; }}
    .card:hover {{ transform: translateY(-4px); border-color: var(--accent); }}
    .badge {{ align-self: flex-start; font-size: 0.75rem; font-weight: 700; padding: 4px 10px; border-radius: 12px; margin-bottom: 12px; text-transform: uppercase; letter-spacing: 0.5px; }}
    .badge.marina {{ background: rgba(56, 139, 253, 0.2); color: #58a6ff; border: 1px solid rgba(56, 139, 253, 0.4); }}
    .badge.food {{ background: rgba(240, 136, 62, 0.2); color: #f0883e; border: 1px solid rgba(240, 136, 62, 0.4); }}
    .badge.animal {{ background: rgba(63, 185, 80, 0.2); color: #3fb950; border: 1px solid rgba(63, 185, 80, 0.4); }}
    .badge.adventure {{ background: rgba(219, 97, 162, 0.2); color: #db61a2; border: 1px solid rgba(219, 97, 162, 0.4); }}
    .preview-box {{ width: 150px; height: 150px; border-radius: 8px; border: 1px solid #30363d; background: repeating-conic-gradient(#1c2128 0% 25%, #22272e 0% 50%) 50% / 20px 20px; display: flex; align-items: center; justify-content: center; margin-bottom: 14px; }}
    .preview-box img {{ width: 125px; height: 125px; image-rendering: pixelated; object-fit: contain; }}
    .card-title {{ font-size: 1.15rem; font-weight: 700; margin-bottom: 4px; text-align: center; }}
    .card-desc {{ font-size: 0.85rem; color: var(--text-muted); text-align: center; margin-bottom: 14px; min-height: 38px; }}
    .palette-container {{ width: 100%; border-top: 1px solid #21262d; padding-top: 12px; }}
    .palette-label {{ font-size: 0.75rem; color: var(--text-muted); margin-bottom: 6px; }}
    .palette-row {{ display: flex; gap: 6px; }}
    .color-swatch {{ flex: 1; height: 18px; border-radius: 4px; border: 1px solid rgba(255,255,255,0.15); }}
    .asset-paths {{ margin-top: 10px; font-size: 0.7rem; color: #6e7681; background: #0d1117; padding: 6px 8px; border-radius: 6px; width: 100%; word-break: break-all; }}
  </style>
</head>
<body>
  <header>
    <h1>Piksel Oyun — Yeni Seviye Görselleri</h1>
    <p class="subtitle">Mevcut bölüm görselleri (Burger, Tilki, Kalp, Balık vb.) tarzında {len(results)} adet yeni piksel sanatı paketi</p>
    <div class="filters">
      <button class="filter-btn active" onclick="filterCategory('all')">Tümü ({len(results)})</button>
      <button class="filter-btn" onclick="filterCategory('Deniz & Marina')">Deniz & Marina (5)</button>
      <button class="filter-btn" onclick="filterCategory('Lezzetler & Tatlılar')">Lezzetler & Tatlılar (6)</button>
      <button class="filter-btn" onclick="filterCategory('Sevimli Hayvanlar')">Sevimli Hayvanlar (5)</button>
      <button class="filter-btn" onclick="filterCategory('Macera & Retro')">Macera & Retro (6)</button>
    </div>
  </header>

  <main class="gallery" id="galleryContainer">
  </main>

  <script>
    const items = {json.dumps(items_json, ensure_ascii=False)};
    const container = document.getElementById('galleryContainer');

    function renderCards(filter = 'all') {{
      container.innerHTML = '';
      items.filter(it => filter === 'all' || it.cat === filter).forEach(it => {{
        const card = document.createElement('div');
        card.className = 'card';
        card.innerHTML = `
          <span class="badge ${{it.badge}}">${{it.cat}}</span>
          <div class="preview-box">
            <img src="${{it.b64}}" alt="${{it.name}}">
          </div>
          <div class="card-title">${{it.name}}</div>
          <div class="card-desc">${{it.desc}}</div>
          <div class="palette-container">
            <div class="palette-label">Kargo Renk Paleti (${{it.colors.length}} Renk)</div>
            <div class="palette-row">
              ${{it.colors.map(c => `<div class="color-swatch" style="background-color: ${{c}};" title="${{c}}"></div>`).join('')}}
            </div>
          </div>
          <div class="asset-paths">
            📁 ${{it.path25}}<br>
            📁 ${{it.path32}}
          </div>
        `;
        container.appendChild(card);
      }});
    }}

    function filterCategory(cat) {{
      document.querySelectorAll('.filter-btn').forEach(btn => {{
        btn.classList.toggle('active', btn.textContent.includes(cat) || (cat === 'all' && btn.textContent.includes('Tümü')));
      }});
      renderCards(cat);
    }}

    renderCards();
  </script>
</body>
</html>
"""
    html_path = "Assets/Screenshots/PixelGallery.html"
    with open(html_path, "w", encoding="utf-8") as f:
        f.write(html)
    print(f"HTML gallery updated: {html_path}")

if __name__ == "__main__":
    main()
