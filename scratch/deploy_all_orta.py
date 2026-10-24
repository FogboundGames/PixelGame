import math, os, sys, uuid
from PIL import Image

sys.path.insert(0, os.path.abspath("scratch"))

from test_refined_layers import (
    create_maki_sushi,
    create_avocado,
    create_round_watermelon,
    create_kiwi,
    create_oyster_pearl,
    create_treasure_chest,
    create_planet_layers,
    create_submarine_porthole,
    create_crystal_geode,
    create_gift_box,
    create_bullseye_target,
    create_coconut
)
from create_kilim_motif import generate_authentic_kilim
from layered_eval import evaluate_image_details

ALL_GENERATORS = [
    ("KilimMotifi", generate_authentic_kilim, "Geleneksel Anadolu Kilimi Motifi (Referans Gorsel Uyarlamasi)"),
    ("MakiSushi", create_maki_sushi, "Esmerkezli Katmanli Sushi Rulosu (Nori > Pirinc > Avokado > Somon > Havyar)"),
    ("Avokado", create_avocado, "Katmanli Avokado Kesiti (Koyu Kabuk > Acik Yesil Et > Sari Et > Cekirdek)"),
    ("KarpuzKesiti", create_round_watermelon, "Tam Katmanli Karpuz Kesiti (Koyu Yesil Kabuk > Beyaz Rind > Kirmizi Et > Cekirdekler)"),
    ("KiviDilimi", create_kiwi, "Enine Kivi Kesiti (Fuzyon Kabuk > Zumrut Et > Cekirdek Cemberi > Beyaz Kalp)"),
    ("IstiridyeInci", create_oyster_pearl, "Istiridye & Gizli Inci (Kaya Kabuk > Sedef Katman > Pembe Yatak > Inci)"),
    ("HazineSandigi", create_treasure_chest, "Kilitli Hazine Sandigi (Demir Cerceve > Mese Govde > Kadife Astar > Altinlar > Elmas)"),
    ("GezegenKatmanlari", create_planet_layers, "Gezegen & Jeot Katmanlari (Okyanus Kabuk > Tas Manto > Magma > Cekirdek)"),
    ("LumbozDenizalti", create_submarine_porthole, "Gemi Lumbozu & Deniz Yildizi (Celik Govde > Pirinc Cember > Derin Su > Mercan Yildiz)"),
    ("KristalJeot", create_crystal_geode, "Kristal Ametist Jeotu (Dis Kaya > Kuvars Bandi > Mor Ametist > Elmas Kalp)"),
    ("HediyeliKutu", create_gift_box, "Acilan Surpriz Kutu (Kirmizi Kurdele > Altin Kutu > Ipek Yatak > Zumrut)"),
    ("HedefTahtasi", create_bullseye_target, "Esmerkezli Hedef Tahtasi (Lacivert > Turkuaz > Sari > Mercan > 12den Merkez)"),
    ("HindistanCevizi", create_coconut, "Acilmis Tropikal Hindistan Cevizi (Lifli Kabuk > Sert Ceper > Kar Beyazi Et > Berrak Su)")
]

def deploy_all():
    out_dir = os.path.join("Assets", "ORTA")
    os.makedirs(out_dir, exist_ok=True)
    
    # 1. Folder meta
    folder_meta_path = os.path.join("Assets", "ORTA.meta")
    folder_guid = uuid.uuid4().hex
    with open(folder_meta_path, "w", encoding="utf-8") as f:
        f.write(f"""fileFormatVersion: 2
guid: {folder_guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""")
    print(f"[META] Created folder meta: {folder_meta_path}")

    # 2. Generate and write all images & their metas
    deployed = []
    for name, func, desc in ALL_GENERATORS:
        img = func()
        ev = evaluate_image_details(img)
        
        file_name = f"{name}.png"
        file_path = os.path.join(out_dir, file_name)
        meta_path = file_path + ".meta"
        
        img.save(file_path, "PNG")
        
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
        deployed.append((file_name, ev['score'], ev['tier'], ev['colors'], ev['buried_colors'], ev['exposed_share'], ev['shells'], desc))
        print(f"[OK] {file_name:22} | Puan: {ev['score']:4.1f} ({ev['tier']}) | Gomulu: {ev['buried_colors']} | Acik: {ev['exposed_share']*100:4.1f}% | Kabuk: {ev['shells']}")

    print(f"\nSuccessfully deployed {len(deployed)} assets to Assets/ORTA.")

if __name__ == '__main__':
    deploy_all()
