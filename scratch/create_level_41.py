import uuid

level_guid = "f67c673339b64587b4e5bd02019ba66b"
tex_guid = "c2ba5f00d72c4f47829fd886bebefb58"

# 100% Solvable Peeling Wagon Sequence:
# Total 528 cubes:
# Color 0 (Süzülen Bal): 30*10 + 20 = 320
# Color 1 (Altın Bal): 20*3 + 13 = 73
# Color 2 (Petek Çerçevesi): 20*2 + 22 = 62
# Color 3 (Koyu Çikolata / Arı): 20*2 = 40
# Color 4 (Bal Parıltısı & Kanat): 20 + 13 = 33

asset_content = f"""%YAML 1.1
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
  m_Name: Level_41_BalPetegi
  m_EditorClassIdentifier: Assembly-CSharp::PixelGame.PixelLevelData
  m_LevelName: "Bal Pete\\u011fi"
  m_LevelIndex: 41
  m_DifficultyTier: 1
  m_DifficultyManual: 1
  m_LegacyIsHardLevel: 0
  m_LevelTexture: {{fileID: 2800000, guid: {tex_guid}, type: 3}}
  m_OriginalSourceTexture: {{fileID: 2800000, guid: {tex_guid}, type: 3}}
  m_LevelSprite: {{fileID: 0}}
  m_UseNativeResolution: 1
  m_CustomResolution: {{x: 32, y: 32}}
  m_ColorPalette:
  - originalColor: {{r: 0.83137256, g: 0.49411765, b: 0.0627451, a: 1}}
    targetColor: {{r: 0.83137256, g: 0.49411765, b: 0.0627451, a: 1}}
    pixelCount: 320
    label: "S\\xFCz\\xFClen Bal"
  - originalColor: {{r: 0.9882353, g: 0.7372549, b: 0.10980392, a: 1}}
    targetColor: {{r: 0.9882353, g: 0.7372549, b: 0.10980392, a: 1}}
    pixelCount: 73
    label: "Alt\\u0131n Bal"
  - originalColor: {{r: 0.5803922, g: 0.3137255, b: 0.078431375, a: 1}}
    targetColor: {{r: 0.5803922, g: 0.3137255, b: 0.078431375, a: 1}}
    pixelCount: 62
    label: "Petek \\xC7er\\xE7evesi"
  - originalColor: {{r: 0.26666668, g: 0.14117648, b: 0.047058824, a: 1}}
    targetColor: {{r: 0.26666668, g: 0.14117648, b: 0.047058824, a: 1}}
    pixelCount: 40
    label: "Koyu \\xC7ikolata / Ar\\u0131"
  - originalColor: {{r: 1, g: 1, b: 1, a: 1}}
    targetColor: {{r: 1, g: 1, b: 1, a: 1}}
    pixelCount: 33
    label: "Bal Par\\u0131lt\\u0131s\\u0131 & Kanat"
  m_TintColor: {{r: 1, g: 1, b: 1, a: 1}}
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
  - wagonColor: {{r: 0.83137256, g: 0.49411765, b: 0.0627451, a: 1}}
    capacity: 30
    paletteIndex: 0
    label: "S\\xFCz\\xFClen Bal"
    linkId: 0
    isHidden: 0
  - wagonColor: {{r: 0.83137256, g: 0.49411765, b: 0.0627451, a: 1}}
    capacity: 30
    paletteIndex: 0
    label: "S\\xFCz\\xFClen Bal"
    linkId: 0
    isHidden: 0
  - wagonColor: {{r: 0.26666668, g: 0.14117648, b: 0.047058824, a: 1}}
    capacity: 20
    paletteIndex: 3
    label: "Koyu \\xC7ikolata / Ar\\u0131"
    linkId: 0
    isHidden: 0
  - wagonColor: {{r: 0.83137256, g: 0.49411765, b: 0.0627451, a: 1}}
    capacity: 30
    paletteIndex: 0
    label: "S\\xFCz\\xFClen Bal"
    linkId: 0
    isHidden: 0
  - wagonColor: {{r: 0.5803922, g: 0.3137255, b: 0.078431375, a: 1}}
    capacity: 20
    paletteIndex: 2
    label: "Petek \\xC7er\\xE7evesi"
    linkId: 0
    isHidden: 0
  - wagonColor: {{r: 0.83137256, g: 0.49411765, b: 0.0627451, a: 1}}
    capacity: 30
    paletteIndex: 0
    label: "S\\xFCz\\xFClen Bal"
    linkId: 0
    isHidden: 0
  - wagonColor: {{r: 0.9882353, g: 0.7372549, b: 0.10980392, a: 1}}
    capacity: 20
    paletteIndex: 1
    label: "Alt\\u0131n Bal"
    linkId: 0
    isHidden: 0
  - wagonColor: {{r: 0.26666668, g: 0.14117648, b: 0.047058824, a: 1}}
    capacity: 20
    paletteIndex: 3
    label: "Koyu \\xC7ikolata / Ar\\u0131"
    linkId: 0
    isHidden: 0
  - wagonColor: {{r: 0.83137256, g: 0.49411765, b: 0.0627451, a: 1}}
    capacity: 30
    paletteIndex: 0
    label: "S\\xFCz\\xFClen Bal"
    linkId: 0
    isHidden: 0
  - wagonColor: {{r: 0.5803922, g: 0.3137255, b: 0.078431375, a: 1}}
    capacity: 20
    paletteIndex: 2
    label: "Petek \\xC7er\\xE7evesi"
    linkId: 0
    isHidden: 0
  - wagonColor: {{r: 1, g: 1, b: 1, a: 1}}
    capacity: 20
    paletteIndex: 4
    label: "Bal Par\\u0131lt\\u0131s\\u0131 & Kanat"
    linkId: 0
    isHidden: 0
  - wagonColor: {{r: 0.83137256, g: 0.49411765, b: 0.0627451, a: 1}}
    capacity: 30
    paletteIndex: 0
    label: "S\\xFCz\\xFClen Bal"
    linkId: 0
    isHidden: 0
  - wagonColor: {{r: 0.9882353, g: 0.7372549, b: 0.10980392, a: 1}}
    capacity: 20
    paletteIndex: 1
    label: "Alt\\u0131n Bal"
    linkId: 0
    isHidden: 0
  - wagonColor: {{r: 0.83137256, g: 0.49411765, b: 0.0627451, a: 1}}
    capacity: 30
    paletteIndex: 0
    label: "S\\xFCz\\xFClen Bal"
    linkId: 0
    isHidden: 0
  - wagonColor: {{r: 0.5803922, g: 0.3137255, b: 0.078431375, a: 1}}
    capacity: 22
    paletteIndex: 2
    label: "Petek \\xC7er\\xE7evesi"
    linkId: 0
    isHidden: 0
  - wagonColor: {{r: 0.9882353, g: 0.7372549, b: 0.10980392, a: 1}}
    capacity: 20
    paletteIndex: 1
    label: "Alt\\u0131n Bal"
    linkId: 0
    isHidden: 0
  - wagonColor: {{r: 1, g: 1, b: 1, a: 1}}
    capacity: 13
    paletteIndex: 4
    label: "Bal Par\\u0131lt\\u0131s\\u0131 & Kanat"
    linkId: 0
    isHidden: 0
  - wagonColor: {{r: 0.83137256, g: 0.49411765, b: 0.0627451, a: 1}}
    capacity: 30
    paletteIndex: 0
    label: "S\\xFCz\\xFClen Bal"
    linkId: 0
    isHidden: 0
  - wagonColor: {{r: 0.9882353, g: 0.7372549, b: 0.10980392, a: 1}}
    capacity: 13
    paletteIndex: 1
    label: "Alt\\u0131n Bal"
    linkId: 0
    isHidden: 0
  - wagonColor: {{r: 0.83137256, g: 0.49411765, b: 0.0627451, a: 1}}
    capacity: 30
    paletteIndex: 0
    label: "S\\xFCz\\xFClen Bal"
    linkId: 0
    isHidden: 0
  - wagonColor: {{r: 0.83137256, g: 0.49411765, b: 0.0627451, a: 1}}
    capacity: 30
    paletteIndex: 0
    label: "S\\xFCz\\xFClen Bal"
    linkId: 0
    isHidden: 0
  - wagonColor: {{r: 0.83137256, g: 0.49411765, b: 0.0627451, a: 1}}
    capacity: 30
    paletteIndex: 0
    label: "S\\xFCz\\xFClen Bal"
    linkId: 0
    isHidden: 0
  - wagonColor: {{r: 0.83137256, g: 0.49411765, b: 0.0627451, a: 1}}
    capacity: 20
    paletteIndex: 0
    label: "S\\xFCz\\xFClen Bal"
    linkId: 0
    isHidden: 0
  m_FigureShadowTexture: {{fileID: 0}}
  m_UseCustomColorTheme: 0
"""

with open('Assets/Levels/Level_41_BalPetegi.asset', 'w', encoding='utf-8') as f:
    f.write(asset_content)

print("Updated Assets/Levels/Level_41_BalPetegi.asset with 100% solvable sequence!")
