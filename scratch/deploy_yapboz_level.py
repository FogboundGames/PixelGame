import uuid
import shutil

# 1. Save Texture
shutil.copy('scratch/puzzle_32.png', 'Assets/32x32/Yapboz.png')
tex_guid = uuid.uuid4().hex

meta_template = """fileFormatVersion: 2
guid: {GUID}
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
    wrapU: 0
    wrapV: 0
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
  platformSettings: []
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

with open('Assets/32x32/Yapboz.png.meta', 'w', encoding='utf-8') as f:
    f.write(meta_template.format(GUID=tex_guid))
print('Saved Assets/32x32/Yapboz.png and meta with GUID:', tex_guid)

# 2. Colors & Wagons
# 10 colors
# 0: Orange (152) -> {r: 0.972549, g: 0.45490196, b: 0.09411765, a: 1}
# 1: Pink (140)   -> {r: 0.95686275, g: 0.40784314, b: 0.7058824, a: 1}
# 2: Blue (128)   -> {r: 0.08627451, g: 0.42352942, b: 0.8784314, a: 1}
# 3: White (128)  -> {r: 0.9882353, g: 0.9882353, b: 0.9882353, a: 1}
# 4: Cyan (116)   -> {r: 0.08627451, g: 0.8156863, b: 0.9254902, a: 1}
# 5: Purple (106) -> {r: 0.49411765, g: 0.14901961, b: 0.63529414, a: 1}
# 6: Red (74)     -> {r: 0.8784314, g: 0.18039216, b: 0.1254902, a: 1}
# 7: Brown (64)   -> {r: 0.6509804, g: 0.44705883, b: 0.227451, a: 1}
# 8: Yellow (64)  -> {r: 0.99607843, g: 0.7607843, b: 0.047058824, a: 1}
# 9: Green (52)   -> {r: 0.10980392, g: 0.65882355, b: 0.29803923, a: 1}

color_defs = [
    ("{r: 0.972549, g: 0.45490196, b: 0.09411765, a: 1}", 152, "Turuncu Par\\u00e7a"),
    ("{r: 0.95686275, g: 0.40784314, b: 0.7058824, a: 1}", 140, "Pembe Par\\u00e7a"),
    ("{r: 0.08627451, g: 0.42352942, b: 0.8784314, a: 1}", 128, "Mavi Par\\u00e7a"),
    ("{r: 0.9882353, g: 0.9882353, b: 0.9882353, a: 1}", 128, "Beyaz Par\\u00e7a"),
    ("{r: 0.08627451, g: 0.8156863, b: 0.9254902, a: 1}", 116, "A\\u00e7\\u0131k Mavi Par\\u00e7a"),
    ("{r: 0.49411765, g: 0.14901961, b: 0.63529414, a: 1}", 106, "Mor Par\\u00e7a"),
    ("{r: 0.8784314, g: 0.18039216, b: 0.1254902, a: 1}", 74, "K\\u0131rm\\u0131z\\u0131 Par\\u00e7a"),
    ("{r: 0.6509804, g: 0.44705883, b: 0.227451, a: 1}", 64, "Kahverengi Par\\u00e7a"),
    ("{r: 0.99607843, g: 0.7607843, b: 0.047058824, a: 1}", 64, "Sar\\u0131 Par\\u00e7a"),
    ("{r: 0.10980392, g: 0.65882355, b: 0.29803923, a: 1}", 52, "Ye\\u015fil Par\\u00e7a"),
]

palette_yaml = ""
for col_str, cnt, lbl in color_defs:
    palette_yaml += f"""  - originalColor: {col_str}
    targetColor: {col_str}
    pixelCount: {cnt}
    label: "{lbl}"\n"""

# Tested 100% Solvable Wagon Sequence:
wagons = [
    (0, 30), (3, 30), (6, 30), (5, 30),
    (9, 30), (1, 30), (4, 30), (2, 30),
    (0, 30), (3, 30), (1, 30), (4, 30),
    (2, 30), (6, 24), (5, 30), (9, 22),
    (7, 30), (8, 30), (0, 30), (3, 30),
    (1, 30), (4, 30), (2, 30), (5, 26),
    (7, 20), (8, 20), (0, 30), (1, 30),
    (3, 20), (2, 20), (4, 26), (6, 20),
    (7, 14), (8, 14), (0, 32), (1, 20),
    (2, 18), (3, 18), (5, 20)
]

wagon_yaml = ""
for c_idx, cap in wagons:
    col_str, _, lbl = color_defs[c_idx]
    wagon_yaml += f"""  - wagonColor: {col_str}
    capacity: {cap}
    paletteIndex: {c_idx}
    label: "{lbl}"
    linkId: 0
    isHidden: 0\n"""

level_guid = uuid.uuid4().hex

level_asset_content = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: 56621c704aae40878464c98bd3ff671a, type: 3}}
  m_Name: Level_42_Yapboz
  m_EditorClassIdentifier: Assembly-CSharp::PixelGame.PixelLevelData
  m_LevelName: "Piksel Yapboz"
  m_LevelIndex: 42
  m_DifficultyTier: 1
  m_DifficultyManual: 1
  m_LegacyIsHardLevel: 0
  m_LevelTexture: {{fileID: 2800000, guid: {tex_guid}, type: 3}}
  m_OriginalSourceTexture: {{fileID: 2800000, guid: {tex_guid}, type: 3}}
  m_LevelSprite: {{fileID: 0}}
  m_UseNativeResolution: 1
  m_CustomResolution: {{x: 32, y: 32}}
  m_ColorPalette:
{palette_yaml}  m_TintColor: {{r: 1, g: 1, b: 1, a: 1}}
  m_HueShift: 0
  m_ColorBrightness: 1
  m_ColorSaturation: 1
  m_ColorContrast: 1
  m_EmissionIntensity: 0
  m_CubeSpacing: 0
  m_CubeSpacingX: 0
  m_CubeDepth: 1
  m_BoardTiltAngle: 18
  m_CubeFrontTiltAngle: 25
  m_TargetZ: 0
  m_CubeRowStepOffset: {{x: 0, y: 0, z: 0}}
  m_InnerPadding: 0.08
  m_SkipTransparent: 1
  m_MysteryCubeCoordinates: []
  m_MysteryRevealCondition: 0
  m_MysteryCubeColor: {{r: 0.125, g: 0.118, b: 0.306, a: 1}}
  m_SlotSprite: {{fileID: 0}}
  m_SlotCount: 4
  m_PoolColumns: 3
  m_PoolRows: 3
  m_DisableAutoPlaceTurbo: 0
  m_TruckCapacity: 30
  m_MinTruckCapacity: 4
  m_PeakTruckCapacity: 10
  m_UseRoundCapacities: 1
  m_RoundCapacityRatio: 0.8
  m_UseCustomWagonSequence: 1
  m_WagonSequence:
{wagon_yaml}  m_FigureShadowTexture: {{fileID: 0}}
  m_UseCustomColorTheme: 0
"""

with open('Assets/Levels/Level_42_Yapboz.asset', 'w', encoding='utf-8') as f:
    f.write(level_asset_content)

meta_content = f"""fileFormatVersion: 2
guid: {level_guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

with open('Assets/Levels/Level_42_Yapboz.asset.meta', 'w', encoding='utf-8') as f:
    f.write(meta_content)

print(f"Created Assets/Levels/Level_42_Yapboz.asset with GUID: {level_guid}")

# Append to LevelSequence.asset
seq_file = 'Assets/Levels/LevelSequence.asset'
with open(seq_file, 'r', encoding='utf-8') as f:
    seq_text = f.read()

if level_guid not in seq_text:
    new_entry = f"  - {{fileID: 11400000, guid: {level_guid}, type: 2}}\n"
    seq_text = seq_text.rstrip() + "\n" + new_entry
    with open(seq_file, 'w', encoding='utf-8') as f:
        f.write(seq_text)
    print("Appended Level_42_Yapboz to LevelSequence.asset!")
