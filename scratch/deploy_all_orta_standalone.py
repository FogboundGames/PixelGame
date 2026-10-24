import math, os, sys, uuid
from PIL import Image

def create_maki_sushi():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    px = im.load()
    cx, cy = 15.5, 15.5
    c_nori = (28, 43, 29, 255)
    c_rice = (245, 247, 242, 255)
    c_avocado = (75, 168, 56, 255)
    c_salmon = (255, 107, 83, 255)
    c_core = (255, 180, 50, 255)
    for y in range(32):
        for x in range(32):
            dx, dy = x - cx, y - cy
            dist = math.sqrt(dx*dx + dy*dy) + 0.15 * (abs(dx)**3 + abs(dy)**3)/(12**2)
            if dist <= 14.2:
                if dist > 11.2: px[x, y] = c_nori
                elif dist > 7.2: px[x, y] = c_rice
                elif dist > 4.5: px[x, y] = c_avocado
                elif dist > 2.0: px[x, y] = c_salmon
                else: px[x, y] = c_core
    return im

def create_avocado():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    px = im.load()
    c_skin = (35, 67, 22, 255)
    c_flesh1 = (111, 165, 42, 255)
    c_flesh2 = (218, 230, 115, 255)
    c_seed = (122, 59, 20, 255)
    c_shine = (186, 110, 43, 255)
    for y in range(32):
        for x in range(32):
            ny = (y - 16) / 14.0
            nx = (x - 15.5) / 12.0
            width_factor = (0.8 + 0.2 * (ny + 1.0)) if ny < 0 else (1.05 - 0.05 * (ny**2))
            adj_x = nx / max(0.2, width_factor)
            dist = math.sqrt(adj_x*adj_x + ny*ny)
            if dist <= 0.98:
                if dist > 0.78: px[x, y] = c_skin
                elif dist > 0.52: px[x, y] = c_flesh1
                else:
                    sdx = (x - 15.5) / 5.2
                    sdy = (y - 18.0) / 6.0
                    sdist = math.sqrt(sdx*sdx + sdy*sdy)
                    if sdist <= 0.55:
                        px[x, y] = c_shine if (sdx < -0.1 and sdy < -0.1 and sdist < 0.35) else c_seed
                    elif sdist <= 1.0: px[x, y] = c_seed
                    else: px[x, y] = c_flesh2
    return im

def create_round_watermelon():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    px = im.load()
    cx, cy = 15.5, 15.5
    c_rind_dark = (30, 99, 39, 255)
    c_rind_light = (214, 245, 208, 255)
    c_flesh = (232, 51, 72, 255)
    c_deep = (181, 21, 41, 255)
    c_seed = (30, 30, 36, 255)
    for y in range(32):
        for x in range(32):
            dx, dy = x - cx, y - cy
            dist = math.sqrt(dx*dx + dy*dy)
            if dist <= 14.2:
                if dist > 11.5: px[x, y] = c_rind_dark
                elif dist > 9.2: px[x, y] = c_rind_light
                elif dist > 4.5: px[x, y] = c_flesh
                else: px[x, y] = c_deep
    for angle_deg in range(0, 360, 45):
        rad = math.radians(angle_deg)
        sx = int(round(cx + 6.8 * math.cos(rad)))
        sy = int(round(cy + 6.8 * math.sin(rad)))
        px[sx, sy] = c_seed
    return im

def create_kiwi():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    px = im.load()
    cx, cy = 15.5, 15.5
    c_peel = (110, 75, 41, 255)
    c_flesh = (117, 189, 36, 255)
    c_flesh_lt = (175, 222, 90, 255)
    c_seeds = (27, 28, 29, 255)
    c_core = (237, 244, 194, 255)
    for y in range(32):
        for x in range(32):
            dx, dy = x - cx, y - cy
            dist = math.sqrt(dx*dx + dy*dy)
            if dist <= 14.0:
                if dist > 11.8: px[x, y] = c_peel
                elif dist > 7.5: px[x, y] = c_flesh
                elif dist > 5.2:
                    angle = math.atan2(dy, dx)
                    px[x, y] = c_seeds if math.sin(angle * 8) > 0.15 else c_flesh_lt
                elif dist > 3.0: px[x, y] = c_flesh_lt
                else: px[x, y] = c_core
    return im

def create_oyster_pearl():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    px = im.load()
    c_shell = (57, 76, 99, 255)
    c_nacre = (122, 102, 153, 255)
    c_mantle = (212, 138, 165, 255)
    c_pearl = (255, 255, 255, 255)
    c_gold = (255, 215, 64, 255)
    cx, cy = 15.5, 16.5
    for y in range(32):
        for x in range(32):
            dx = (x - cx) / 14.0
            dy = (y - cy) / 11.5
            dist = math.sqrt(dx*dx + dy*dy)
            if dist <= 1.0:
                if dist > 0.78: px[x, y] = c_shell
                elif dist > 0.52: px[x, y] = c_nacre
                elif dist > 0.32: px[x, y] = c_mantle
                else: px[x, y] = c_gold if (dx > 0.05 and dy > 0.05) else c_pearl
    return im

def create_treasure_chest():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    px = im.load()
    c_iron = (49, 55, 66, 255)
    c_wood = (115, 67, 34, 255)
    c_velvet = (122, 29, 58, 255)
    c_gold = (255, 196, 0, 255)
    c_gem = (0, 229, 255, 255)
    for y in range(32):
        for x in range(32):
            if 5 <= y <= 27 and 4 <= x <= 27:
                if y in (5, 6, 26, 27) or x in (4, 5, 26, 27) or x in (11, 12, 19, 20):
                    px[x, y] = c_iron
                elif y in (7, 8, 24, 25) or x in (6, 7, 24, 25):
                    px[x, y] = c_wood
                elif y in (9, 10, 22, 23) or x in (8, 9, 22, 23):
                    px[x, y] = c_velvet
                elif y in (11, 12, 13, 19, 20, 21) or x in (10, 13, 14, 17, 18, 21):
                    px[x, y] = c_gold
                else:
                    px[x, y] = c_gem if 14 <= x <= 17 and 14 <= y <= 18 else c_gold
    return im

def create_planet_layers():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    px = im.load()
    cx, cy = 15.5, 15.5
    c_crust = (37, 94, 148, 255)
    c_stone = (138, 86, 54, 255)
    c_magma = (230, 74, 25, 255)
    c_liquid = (255, 160, 0, 255)
    c_solid = (255, 245, 157, 255)
    for y in range(32):
        for x in range(32):
            dx, dy = x - cx, y - cy
            dist = math.sqrt(dx*dx + dy*dy)
            if dist <= 14.5:
                if dist > 11.5: px[x, y] = c_crust
                elif dist > 8.5: px[x, y] = c_stone
                elif dist > 5.5: px[x, y] = c_magma
                elif dist > 2.8: px[x, y] = c_liquid
                else: px[x, y] = c_solid
    return im

def create_submarine_porthole():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    px = im.load()
    cx, cy = 15.5, 15.5
    c_steel = (60, 68, 77, 255)
    c_brass = (201, 138, 34, 255)
    c_glass_deep = (18, 52, 86, 255)
    c_glass_aqua = (40, 140, 170, 255)
    c_starfish = (255, 87, 34, 255)
    for y in range(32):
        for x in range(32):
            dx, dy = x - cx, y - cy
            dist = math.sqrt(dx*dx + dy*dy)
            if dist <= 14.5:
                if dist > 11.8: px[x, y] = c_steel
                elif dist > 8.8: px[x, y] = c_brass
                elif dist > 5.5: px[x, y] = c_glass_deep
                elif dist > 3.0: px[x, y] = c_glass_aqua
                else: px[x, y] = c_starfish
    return im

def create_crystal_geode():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    px = im.load()
    cx, cy = 15.5, 15.5
    c_rock = (55, 62, 71, 255)
    c_quartz = (125, 144, 163, 255)
    c_dark_violet = (98, 37, 143, 255)
    c_amethyst = (157, 78, 221, 255)
    c_diamond = (224, 170, 255, 255)
    for y in range(32):
        for x in range(32):
            dx, dy = x - cx, y - cy
            angle = math.atan2(dy, dx)
            r_mod = 1.0 + 0.08 * math.cos(angle * 5) + 0.06 * math.sin(angle * 3)
            dist = math.sqrt(dx*dx + dy*dy) / r_mod
            if dist <= 14.5:
                if dist > 11.2: px[x, y] = c_rock
                elif dist > 8.2: px[x, y] = c_quartz
                elif dist > 5.5: px[x, y] = c_dark_violet
                elif dist > 2.8: px[x, y] = c_amethyst
                else: px[x, y] = c_diamond
    return im

def create_gift_box():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    px = im.load()
    c_ribbon = (211, 47, 47, 255)
    c_box = (255, 160, 0, 255)
    c_silk = (26, 35, 126, 255)
    c_emerald = (0, 230, 118, 255)
    c_shine = (255, 255, 255, 255)
    for y in range(32):
        for x in range(32):
            if 5 <= y <= 27 and 5 <= x <= 27:
                if y in (5, 6, 26, 27) or x in (5, 6, 26, 27) or x in (15, 16) or y in (15, 16):
                    px[x, y] = c_ribbon
                elif y in (7, 8, 24, 25) or x in (7, 8, 24, 25):
                    px[x, y] = c_box
                elif y in (9, 10, 22, 23) or x in (9, 10, 22, 23):
                    px[x, y] = c_silk
                else:
                    if 13 <= x <= 18 and 13 <= y <= 18:
                        px[x, y] = c_shine if (x in (15, 16) and y in (15, 16)) else c_emerald
                    else:
                        px[x, y] = c_silk
    return im

def create_bullseye_target():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    px = im.load()
    cx, cy = 15.5, 15.5
    c_ring1 = (29, 45, 80, 255)
    c_ring2 = (0, 180, 216, 255)
    c_ring3 = (255, 209, 102, 255)
    c_ring4 = (239, 71, 111, 255)
    c_center = (255, 241, 118, 255)
    for y in range(32):
        for x in range(32):
            dx, dy = x - cx, y - cy
            dist = math.sqrt(dx*dx + dy*dy)
            if dist <= 14.5:
                if dist > 11.5: px[x, y] = c_ring1
                elif dist > 8.5: px[x, y] = c_ring2
                elif dist > 5.5: px[x, y] = c_ring3
                elif dist > 2.8: px[x, y] = c_ring4
                else: px[x, y] = c_center
    return im

def create_coconut():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    px = im.load()
    cx, cy = 15.5, 15.5
    c_husk = (78, 52, 46, 255)
    c_shell = (109, 76, 65, 255)
    c_meat = (245, 245, 245, 255)
    c_water = (128, 222, 234, 255)
    c_deep = (0, 172, 193, 255)
    for y in range(32):
        for x in range(32):
            dx, dy = x - cx, y - cy
            dist = math.sqrt(dx*dx + dy*dy)
            if dist <= 14.2:
                if dist > 11.5: px[x, y] = c_husk
                elif dist > 9.0: px[x, y] = c_shell
                elif dist > 6.0: px[x, y] = c_meat
                elif dist > 3.0: px[x, y] = c_water
                else: px[x, y] = c_deep
    return im

def generate_authentic_kilim():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    px = im.load()
    c_bordeaux = (152, 28, 46, 255)
    c_navy     = (28, 34, 62, 255)
    c_orange   = (236, 114, 22, 255)
    c_cream    = (244, 238, 224, 255)
    for y in range(32):
        for x in range(32):
            if (x in (0, 31) and y in (0, 31)):
                continue
            if x in (0, 1, 30, 31) or y in (0, 1, 30, 31):
                px[x, y] = c_bordeaux
            elif x in (2, 3, 28, 29) or y in (2, 3, 28, 29):
                is_floret = False
                for pos in [7, 15, 23]:
                    if y in (2, 3, 28, 29) and abs(x - pos) <= 1: is_floret = True
                    if x in (2, 3, 28, 29) and abs(y - pos) <= 1: is_floret = True
                px[x, y] = c_cream if is_floret else c_navy
            elif x in (4, 27) or y in (4, 27):
                px[x, y] = c_bordeaux
            elif x in (5, 26) or y in (5, 26):
                px[x, y] = c_navy if (x + y) % 2 == 0 else c_bordeaux
            else:
                px[x, y] = c_orange
    cx, cy = 15.5, 15.5
    for y in range(6, 26):
        for x in range(6, 26):
            dx = abs(x - cx)
            dy = abs(y - cy)
            d = dx + dy
            if 6.0 <= d <= 7.2: px[x, y] = c_navy
            elif 4.2 <= d < 6.0: px[x, y] = c_orange
            elif 2.5 <= d < 4.2: px[x, y] = c_navy
            elif 1.2 <= d < 2.5: px[x, y] = c_orange
            elif d < 1.2: px[x, y] = c_cream
            if dy in (7, 8) and dx in (1, 2): px[x, y] = c_navy
            if dy == 8 and dx in (3, 4): px[x, y] = c_navy
            if dy == 7 and dx == 4: px[x, y] = c_navy
            if dx in (7, 8) and dy in (1, 2): px[x, y] = c_navy
            if dx == 8 and dy in (3, 4): px[x, y] = c_navy
            if dx == 7 and dy == 4: px[x, y] = c_navy
            if dx in (7, 8) and dy in (7, 8):
                if dx == 8 or dy == 8: px[x, y] = c_navy
                elif dx == 7 and dy == 7: px[x, y] = c_cream
    return im

ALL_ITEMS = [
    ("KilimMotifi", generate_authentic_kilim),
    ("MakiSushi", create_maki_sushi),
    ("Avokado", create_avocado),
    ("KarpuzKesiti", create_round_watermelon),
    ("KiviDilimi", create_kiwi),
    ("IstiridyeInci", create_oyster_pearl),
    ("HazineSandigi", create_treasure_chest),
    ("GezegenKatmanlari", create_planet_layers),
    ("LumbozDenizalti", create_submarine_porthole),
    ("KristalJeot", create_crystal_geode),
    ("HediyeliKutu", create_gift_box),
    ("HedefTahtasi", create_bullseye_target),
    ("HindistanCevizi", create_coconut)
]

def main():
    out_dir = os.path.join("Assets", "ORTA")
    os.makedirs(out_dir, exist_ok=True)
    
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
    print("Created ORTA.meta")

    for name, func in ALL_ITEMS:
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
        print(f"Saved: {png_path}")

    print(f"Successfully generated all {len(ALL_ITEMS)} assets in Assets/ORTA!")

if __name__ == '__main__':
    main()
