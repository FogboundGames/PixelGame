import math, os, sys, uuid
from PIL import Image

sys.path.append(os.path.abspath('scratch'))
from test_detailed_items import (
    create_pirate_galleon,
    create_lighthouse_night,
    create_samurai_kabuto,
    create_magic_potion,
    create_retro_console,
    create_fire_dragon,
    create_zen_bonsai
)
from test_more_detailed import (
    create_steam_locomotive,
    create_ship_compass
)

NEW_DETAILED_ITEMS = [
    ("KorsanGemisi", create_pirate_galleon, "Korsan Kalyonu (Ahşap Gövde, Şişkin Yelkenler, Jolly Roger Korsan Bayrağı & Dalgalar)"),
    ("DenizFeneriGece", create_lighthouse_night, "Gece Deniz Feneri (Kırmızı-Beyaz Kule, Gece Göğü, Yıldızlar, Projektör Işığı & Dalgalar)"),
    ("SamurayMigferi", create_samurai_kabuto, "Samuray Kabuto Miğferi (Altın Hilal Arması, Katmanlı Zırh Plakaları, Korkutucu Yüz Maskesi)"),
    ("BuyuluIksir", create_magic_potion, "Büyülü Simya İksiri (Altın Kafesli Şişe, Mor Mana Girdabı, Işıldayan Baloncuklar & Kristal Tıpa)"),
    ("RetroKonsol", create_retro_console, "Retro El Konsolu (Piksel Ekran, D-Pad Yön Tuşları, Kırmızı Aksiyon Tuşları, Hoparlör Kanalları)"),
    ("EjderhaBasi", create_fire_dragon, "Kızıl Ejderha Başı (Obsidyen Boynuzlar, Yakut Pullar, Parlayan Sarı Sürüngen Gözü & Alev Dili)"),
    ("BonsaiAgaci", create_zen_bonsai, "Zen Bonsai Ağacı (Kobalt Mavi Seramik Saksı, Yaşlı Kıvrımlı Gövde, Zümrüt İğne Bulutları & Sakura)"),
    ("BuharliLokomotif", create_steam_locomotive, "Buharlı Lokomotif (Siyah Kazan, Pirinç Perçinler, Bacadan Çıkan Pofuduk Buhar & Yanan Fener)"),
    ("GemiPusulasi", create_ship_compass, "Nostaljik Gemi Pusulası (Pirinç Kadran, 8 Kollu Pusula Yıldızı, Kırmızı-Beyaz İğne & Dereceler)")
]

def deploy_detailed():
    out_dir = os.path.join("Assets", "ORTA")
    os.makedirs(out_dir, exist_ok=True)
    
    for name, func, desc in NEW_DETAILED_ITEMS:
        img = func()
        png_path = os.path.join(out_dir, f"{name}.png")
        meta_path = png_path + ".meta"
        
        img.save(png_path, "PNG")
        
        tex_guid = uuid.uuid4().hex
        with open(meta_path, "w", encoding="utf-8") as mf:
            mf.write(f"""fileFormatVersion: 2
guid: {tex_guid}
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
    spriteID: {tex_guid[:16]}0800000000000000
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
""")
        # Count non-transparent pixels
        colors = set()
        cubes = 0
        for p in img.getdata():
            if p[3] > 20:
                cubes += 1
                colors.add(p[:3])
        print(f"[DEPLOYED DETAILED] {name:18} | Kupler: {cubes:4} | Renkler: {len(colors):2}")

    print("All detailed images deployed successfully to Assets/ORTA!")

if __name__ == '__main__':
    deploy_detailed()
