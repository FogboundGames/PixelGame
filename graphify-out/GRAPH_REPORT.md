# Graph Report - PixelGame  (2026-10-08)

## Corpus Check
- 1176 files · ~9,320,617 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 4704 nodes · 10047 edges · 231 communities (198 shown, 17 thin omitted)
- Extraction: 94% EXTRACTED · 6% INFERRED · 0% AMBIGUOUS · INFERRED: 636 edges (avg confidence: 0.81)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- TCP2 Shader Templates
- ShipController
- Community 2
- Community 3
- Community 4
- Community 5
- AutoPlaytest
- Community 7
- Level Load Events
- ShipDispatcher
- DOTween Modules
- Casual HUD Coins
- PixelArtGenerator Board
- Community 13
- ShipQueuePool Editor
- Level Sequence Marina
- Background UI Setup
- Community 17
- Community 18
- Community 19
- Community 20
- Community 21
- Community 22
- Community 23
- Community 24
- Community 25
- Community 26
- Community 27
- Community 28
- Community 29
- Community 30
- Community 31
- Community 32
- Community 33
- Community 34
- Community 35
- Community 36
- Community 37
- Community 38
- Community 39
- Community 40
- Community 41
- Community 42
- Community 43
- Community 44
- Community 45
- Community 46
- Community 47
- Community 48
- Community 49
- Community 50
- Community 51
- Community 52
- Community 53
- Community 54
- Community 55
- Community 56
- Community 57
- Community 58
- Community 59
- Community 60
- Community 61
- Community 62
- Community 63
- Community 64
- Community 65
- Community 66
- Community 67
- Community 68
- Community 69
- Community 70
- Community 71
- Community 72
- Community 73
- Community 74
- Community 75
- Community 76
- Community 77
- Community 78
- Community 79
- Community 80
- Community 81
- Community 82
- Community 83
- Community 84
- Community 85
- Community 86
- Community 87
- Community 88
- Community 89
- Community 90
- Community 91
- Community 92
- Community 93
- Community 94
- Community 95
- Community 96
- Community 97
- Community 98
- Community 99
- Community 100
- Community 101
- Community 102
- Community 103
- Community 104
- Community 105
- Community 106
- Community 107
- Community 108
- Community 109
- Community 110
- Community 111
- Community 112
- Community 113
- Community 115
- Community 116
- Community 117
- Community 118
- Community 119
- Community 120
- Community 121
- Community 122
- Community 123
- Community 124
- Community 125
- Community 126
- Community 127
- Community 128
- Community 129
- Community 130
- Community 131
- Community 132
- Community 133
- Community 134
- Community 135
- Community 136
- Community 137
- Community 138
- Community 139
- Community 140
- Community 141
- Community 142
- Community 143
- Community 144
- Community 145
- Community 146
- Community 147
- Community 148
- Community 149
- Community 150
- Community 151
- Community 152
- Community 153
- Community 154
- Community 155
- Community 156
- Community 157
- Community 158
- Community 159
- Community 160
- Community 161
- Community 162
- Community 163
- Community 164
- Community 165
- Community 166
- Community 167
- Community 168
- Community 169
- Community 170
- Community 171
- Community 172
- Community 173
- Community 174
- Community 175
- Community 176
- Community 177
- Community 178
- Community 179
- Community 180
- Community 181
- Community 183
- Community 184
- Community 185
- Community 186
- Community 187
- Community 188
- Community 189
- Community 190
- Community 191
- Community 192
- Community 193
- Community 194
- Community 195
- Community 196
- Community 197
- Community 198
- Community 199
- Community 200
- Community 201
- Community 202
- Community 203
- Community 204
- Community 205
- Community 206
- Community 207
- Community 208
- Community 209
- Community 210
- Community 211
- Community 212
- Community 213
- Community 214
- Community 215
- Community 225

## God Nodes (most connected - your core abstractions)
1. `ShipController` - 213 edges
2. `ShaderProperty` - 150 edges
3. `PixelArtGenerator` - 139 edges
4. `Config` - 125 edges
5. `PixelCube` - 115 edges
6. `PixelLevelData` - 103 edges
7. `PixelLevelDesignerWindow` - 99 edges
8. `ShipDispatcher` - 99 edges
9. `CasualHudController` - 79 edges
10. `LevelBuilderWindow` - 72 edges

## Surprising Connections (you probably didn't know these)
- `Gear Frame Sprite Set (frames 23-35)` --part_of--> `Gear Frame Sequence`  [INFERRED]
  Assets/UI/GearFrames/gear_23.png → Assets/Scripts/CornerGearAnimator.cs
- `Corner_Gear GameObject` --uses--> `Gear Frame Sequence`  [INFERRED]
  Assets/Editor/SetupCornerGear.cs → Assets/Scripts/CornerGearAnimator.cs
- `LevelBuilderWindow` --references--> `LevelCanvas`  [EXTRACTED]
  Assets/Editor/LevelBuilderWindow.cs → Assets/Editor/LevelCanvas.cs
- `LevelBuilderWindow` --references--> `LevelCanvasUndoState`  [EXTRACTED]
  Assets/Editor/LevelBuilderWindow.cs → Assets/Editor/LevelCanvasUndoState.cs
- `PixelLevelDesignerWindow` --references--> `LevelDifficultyTableView`  [EXTRACTED]
  Assets/Editor/PixelLevelDesignerWindow.cs → Assets/Editor/LevelDifficultyOverviewWindow.cs

## Import Cycles
- None detected.

## Communities (231 total, 17 thin omitted)

### Community 0 - "TCP2 Shader Templates"
Cohesion: 0.04
Nodes (33): ProgramType, VariableType, Color, Color, Func, GenericMenu, GUIContent, KeyValuePair (+25 more)

### Community 1 - "ShipController"
Cohesion: 0.03
Nodes (63): Camera, Coroutine, Dictionary, Image, List, Mesh, PointerEventData, Transform (+55 more)

### Community 2 - "Community 2"
Cohesion: 0.03
Nodes (20): Implementation, OptionFeatures, Imp_LocalNormal, MenuLabel, VariableCompatibility, Imp_LocalPosition, MenuLabel, VariableCompatibility (+12 more)

### Community 3 - "Community 3"
Cohesion: 0.05
Nodes (40): Collider, Color, GameObject, IEnumerator, IReadOnlyList, List, Material, MaterialPropertyBlock (+32 more)

### Community 4 - "Community 4"
Cohesion: 0.05
Nodes (36): CustomDeserializeCallback, Dictionary, FloatPrecision, GenericMenu, GUIStyle, InjectionPoint, List, OnDeserializeCallback (+28 more)

### Community 5 - "Community 5"
Cohesion: 0.06
Nodes (19): GUIContent, List, MessageType, Rect, Stack, UIFeature, UIFeature_DropDownEnd, UIFeature_DropDownStart (+11 more)

### Community 6 - "AutoPlaytest"
Cohesion: 0.06
Nodes (34): Config, Dictionary, HashSet, LevelManager, List, MenuItem, PlayModeStateChange, Random (+26 more)

### Community 7 - "Community 7"
Cohesion: 0.05
Nodes (24): CustomMaterialProperty, Dictionary, GUIContent, HashSet, IEnumerable, KeyValuePair, List, ShaderImporter (+16 more)

### Community 8 - "Level Load Events"
Cohesion: 0.04
Nodes (50): List, Random, Sprite, Vector3, PixelLevelData, BoardTiltAngle, ColorBrightness, ColorContrast (+42 more)

### Community 9 - "ShipDispatcher"
Cohesion: 0.08
Nodes (16): Color, Dictionary, HashSet, IEnumerator, IReadOnlyList, List, ShipDispatcher, ActivePaletteLevel (+8 more)

### Community 10 - "DOTween Modules"
Cohesion: 0.09
Nodes (27): CanvasGroup, Color, ColorOptions, FloatOptions, Gradient, Image, LayoutElement, Outline (+19 more)

### Community 11 - "Casual HUD Coins"
Cohesion: 0.05
Nodes (30): Action, CasualHudController, CoinsText, CompleteContinueSprite, CompletePanelSprite, CompleteRewardSprite, CurrentCoins, CurrentLives (+22 more)

### Community 12 - "PixelArtGenerator Board"
Cohesion: 0.03
Nodes (52): Dictionary, IEnumerator, Material, Transform, Vector2Int, PixelArtGenerator, ActiveLevelData, BoardShadowColor (+44 more)

### Community 13 - "Community 13"
Cohesion: 0.06
Nodes (22): Color, Config, CustomMaterialProperty, List, MenuItem, ProgramType, SerializedProperty, Shader (+14 more)

### Community 14 - "ShipQueuePool Editor"
Cohesion: 0.08
Nodes (19): ShipQueuePoolEditor, Color, GameObject, IEnumerator, List, Transform, Vector2, ColumnLayoutPreset (+11 more)

### Community 15 - "Level Sequence Marina"
Cohesion: 0.09
Nodes (16): Action, GUIStyle, LevelManager, LevelSequence, MarinaSlotLayout, Material, PixelLevelData, Report (+8 more)

### Community 16 - "Background UI Setup"
Cohesion: 0.05
Nodes (37): Canvas, CanvasScaler, EventSystem, GameObject, GraphicRaycaster, Image, RectTransform, StandaloneInputModule (+29 more)

### Community 17 - "Community 17"
Cohesion: 0.09
Nodes (16): Shader, StringBuilder, Template, NotEmptyBlock, Dictionary, GUIContent, InjectionPoint, List (+8 more)

### Community 18 - "Community 18"
Cohesion: 0.07
Nodes (27): Color, GenericMenu, List, MenuItem, Shader, ShaderImporter, UIFeature, Vector2 (+19 more)

### Community 19 - "Community 19"
Cohesion: 0.07
Nodes (12): Imp_MaterialProperty_Texture, CanUseSeparateSampler, HasErrors, InvalidSampler, LinkedCustomMaterialProperty, LinkedShaderProperty, MenuLabel, UseOldSampler2DSyntax (+4 more)

### Community 20 - "Community 20"
Cohesion: 0.09
Nodes (21): Color, Color32, Gradient, List, Texture2D, GradientManager, LAST_SAVE_PATH, AssetImporter (+13 more)

### Community 21 - "Community 21"
Cohesion: 0.09
Nodes (29): Camera, Canvas, CanvasScaler, Collider, GameObject, GraphicRaycaster, HypercasualWaterController, Image (+21 more)

### Community 22 - "Community 22"
Cohesion: 0.06
Nodes (28): EventSystem, Rect, RectTransform, StandaloneInputModule, Transform, Vector2, Vector3, TCP2_Demo_Camera (+20 more)

### Community 23 - "Community 23"
Cohesion: 0.07
Nodes (24): Font, List, Data, GlobalOptions, data, ProjectOptions, data, Font (+16 more)

### Community 24 - "Community 24"
Cohesion: 0.08
Nodes (15): Action, Collider, IEnumerator, IReadOnlyList, Quaternion, Vector3, IReadOnlyList, List (+7 more)

### Community 25 - "Community 25"
Cohesion: 0.08
Nodes (26): Action, Color, GUIStyle, LevelManager, List, MenuItem, PixelLevelData, Result (+18 more)

### Community 26 - "Community 26"
Cohesion: 0.08
Nodes (13): Func, List, GenericImplementation, Imp_GenericFromTemplate, HasErrors, VariableCompatibility, Option, AvailableValue (+5 more)

### Community 27 - "Community 27"
Cohesion: 0.07
Nodes (32): LevelColorTheme, TruckPart, Color, LevelColorTheme, Parts, LevelDifficultyTier, Kolay, Orta (+24 more)

### Community 28 - "Community 28"
Cohesion: 0.09
Nodes (19): Dictionary, HashSet, ProgramType, TextAsset, VariableType, Imp_CustomCode, HasErrors, MenuLabel (+11 more)

### Community 29 - "Community 29"
Cohesion: 0.11
Nodes (14): Color32, GUIStyle, MessageType, PixelArtGenerator, PixelLevelData, Texture2D, Vector2, LevelBuilderWindow (+6 more)

### Community 30 - "Community 30"
Cohesion: 0.11
Nodes (8): Dictionary, TextAsset, TCP2_Config, GUIContent, Rect, Stack, UIFeature, UIFeature_Keyword

### Community 31 - "Community 31"
Cohesion: 0.10
Nodes (26): Report, List, MessageType, PaletteColorOverride, PixelLevelData, LevelDesignWarnings, Warning, Color (+18 more)

### Community 32 - "Community 32"
Cohesion: 0.06
Nodes (33): Color, Dictionary, GUIStyle, TCP2_GUI, BigHeaderLabel, CogIcon, CogIcon2, ContextMenuButton (+25 more)

### Community 33 - "Community 33"
Cohesion: 0.11
Nodes (23): Gradient, Material, Sequence, Tween, TweenerCore, Vector2, VectorOptions, DOTweenCYInstruction (+15 more)

### Community 34 - "Community 34"
Cohesion: 0.12
Nodes (11): AndroidJavaObject, AudioClip, AudioSource, Color, Material, ParticleSystem, ParticleSystemRenderer, Texture2D (+3 more)

### Community 35 - "Community 35"
Cohesion: 0.10
Nodes (9): Color, Color32, Texture2D, LevelCanvas, ActivePalette, FilledCount, Height, IsEmpty (+1 more)

### Community 36 - "Community 36"
Cohesion: 0.07
Nodes (11): OptionFeatures, VariableType, CustomMaterialProperty, Channels, HasErrors, IsDotsInstanced, IsGpuInstanced, Label (+3 more)

### Community 37 - "Community 37"
Cohesion: 0.08
Nodes (18): Material, RenderTexture, Shader, Bloom, antiFlicker, highQuality, intensity, radius (+10 more)

### Community 38 - "Community 38"
Cohesion: 0.10
Nodes (18): GUIContent, List, MessageType, GC_Else, GC_EndIf, GC_EndIfDisable, GC_Header, label (+10 more)

### Community 39 - "Community 39"
Cohesion: 0.06
Nodes (12): DumpCornerDetailStub, Font, TMP_FontAsset, SetupCasualTMPFont, MenuItem, SetupGemiCubeGrid, MenuItem, ShipController (+4 more)

### Community 40 - "Community 40"
Cohesion: 0.12
Nodes (8): Dictionary, List, Shader, ShaderImporter, Texture2D, TCP2_ShaderGeneratorUtils, OutputPath, ShaderGeneratorTemplate

### Community 41 - "Community 41"
Cohesion: 0.09
Nodes (14): GUIContent, List, SmoothedNormalsUVType, CompressedXY, CompressedZW, FullXYZ, TextureChannel, Alpha (+6 more)

### Community 42 - "Community 42"
Cohesion: 0.14
Nodes (11): Action, Color, Coroutine, IEnumerator, Sprite, TMP_FontAsset, Tween, LandFlowLoadingScreen (+3 more)

### Community 43 - "Community 43"
Cohesion: 0.07
Nodes (27): Mesh, MarinaSlotLayout, ArcAngleFan, ArcAsymmetry, ArcCurveY, BaySlotOffsetY, Config3Slots, Config4Slots (+19 more)

### Community 44 - "Community 44"
Cohesion: 0.12
Nodes (8): BoxCollider, ContextMenu, GameObject, Material, MaterialPropertyBlock, MeshFilter, MeshRenderer, Shader

### Community 45 - "Community 45"
Cohesion: 0.07
Nodes (16): GUIContent, OnDeserializeCallback, BlendType, Add, Custom, LinearInterpolation, Multiply, MultiplyDouble (+8 more)

### Community 46 - "Community 46"
Cohesion: 0.10
Nodes (12): Enum, GUIStyle, List, Vector2, Vector3, Vector4, Constants, SGUILayout (+4 more)

### Community 47 - "Community 47"
Cohesion: 0.12
Nodes (15): Color, GameObject, Material, MaterialPropertyBlock, Mesh, MeshFilter, MeshRenderer, Transform (+7 more)

### Community 48 - "Community 48"
Cohesion: 0.13
Nodes (14): Dictionary, HashSet, List, Texture2D, EmojiEntry, EmojiList, PixelImageBrowserWindow, ConcurrentQueue (+6 more)

### Community 49 - "Community 49"
Cohesion: 0.09
Nodes (20): CustomDeserializeCallback, Dictionary, Func, CustomDeserializeCallbackAttribute, ForceSerializationAttribute, OnDeserializeCallbackAttribute, Serialization, SerializeAsAttribute (+12 more)

### Community 50 - "Community 50"
Cohesion: 0.07
Nodes (28): Styles, GrayBoldLabel, GrayInlineLabel, GrayLabel, GrayMiniBoldLabel, GrayMiniFoldout, GrayMiniFoldoutHighlighted, GrayMiniLabel (+20 more)

### Community 51 - "Community 51"
Cohesion: 0.07
Nodes (11): Imp_MaterialProperty, Color, Imp_MaterialProperty_Color, MenuLabel, VariableCompatibility, Imp_MaterialProperty_Float, MenuLabel, VariableCompatibility (+3 more)

### Community 52 - "Community 52"
Cohesion: 0.14
Nodes (12): Dictionary, List, MenuItem, Mesh, MeshFilter, Object, Vector2, SelectedMesh (+4 more)

### Community 53 - "Community 53"
Cohesion: 0.14
Nodes (11): AudioClip, AudioSource, Color, Material, Mesh, ParticleSystem, ParticleSystemRenderer, Random (+3 more)

### Community 54 - "Community 54"
Cohesion: 0.14
Nodes (10): Dictionary, List, Rect, Texture2D, BatchLevelGeneratorWindow, PixelFolder, PixelSize, ColorReductionMode (+2 more)

### Community 55 - "Community 55"
Cohesion: 0.15
Nodes (13): Action, AssetImporter, Dictionary, MaterialEditor, MaterialProperty, Stack, MaterialInspector_Hybrid, DisableNextProperty (+5 more)

### Community 56 - "Community 56"
Cohesion: 0.17
Nodes (11): Dictionary, HashSet, List, Vector3, CargoRope, HeadDistance, TailDistance, CargoRopeBuilder (+3 more)

### Community 57 - "Community 57"
Cohesion: 0.12
Nodes (8): Animator, Collider, GameObject, MaterialPropertyBlock, MeshRenderer, Renderer, Vector2, Vector3

### Community 58 - "Community 58"
Cohesion: 0.19
Nodes (7): PixelArtGenerator, ExecuteBoardShadowUpdate, GameObject, Mesh, MeshFilter, MeshRenderer, Vector3

### Community 59 - "Community 59"
Cohesion: 0.17
Nodes (8): Camera, Canvas, ContextMenu, Image, RectTransform, Sprite, SpriteRenderer, Vector2

### Community 60 - "Community 60"
Cohesion: 0.16
Nodes (17): Button, Camera, Canvas, CanvasScaler, CasualHudController, CasualUIButtonJuice, ContentSizeFitter, GameObject (+9 more)

### Community 61 - "Community 61"
Cohesion: 0.10
Nodes (8): Dictionary, Implementation, Imp_Hook, MenuLabel, VariableCompatibility, Imp_VertexTexcoord, MenuLabel, VariableCompatibility

### Community 62 - "Community 62"
Cohesion: 0.09
Nodes (14): IEnumerable, Imp_CustomMaterialProperty, HasErrors, LinkedCustomMaterialProperty, MenuLabel, VariableCompatibility, willBeRemoved, UvSourceType (+6 more)

### Community 63 - "Community 63"
Cohesion: 0.18
Nodes (5): MaterialEditor, MaterialProperty, TCP2HeaderToggleDrawer, TCP2ToggleNoKeywordDrawer, TCP2UVScrolling

### Community 64 - "Community 64"
Cohesion: 0.15
Nodes (17): Button, Camera, Canvas, CanvasScaler, CasualHudController, CasualUIButtonJuice, GameObject, GraphicRaycaster (+9 more)

### Community 65 - "Community 65"
Cohesion: 0.08
Nodes (13): FloatPrecision, Vector2, Vector3, Vector4, Imp_ConstantFloat, MenuLabel, VariableCompatibility, Imp_ConstantValue (+5 more)

### Community 66 - "Community 66"
Cohesion: 0.19
Nodes (15): Color, Path, PathMode, PathOptions, PathType, Quaternion, Rigidbody, Sequence (+7 more)

### Community 67 - "Community 67"
Cohesion: 0.12
Nodes (13): Canvas, CanvasScaler, Color, Color32, Outline, RectTransform, Sprite, Text (+5 more)

### Community 68 - "Community 68"
Cohesion: 0.15
Nodes (12): ActiveRipple, Camera, Color, List, Material, RawImage, RuntimeInitializeOnLoadMethod, Vector2 (+4 more)

### Community 69 - "Community 69"
Cohesion: 0.09
Nodes (4): Implementation, HasErrors, IsDefaultImplementation, Operator

### Community 70 - "Community 70"
Cohesion: 0.08
Nodes (24): OptionFeatures, HSV_Colorize, HSV_Full, HSV_Grayscale, Local_Normal_Fragment, Local_Pos_Fragment, NoTile_Sampling, NoTile_Sampling_Vertex (+16 more)

### Community 71 - "Community 71"
Cohesion: 0.10
Nodes (16): Component, MonoBehaviour, Path, PathMode, PathOptions, PlayModeStateChange, Quaternion, Rigidbody (+8 more)

### Community 72 - "Community 72"
Cohesion: 0.12
Nodes (14): Collider, GameObject, Material, MeshRenderer, Quaternion, Transform, Vector2, Vector3 (+6 more)

### Community 73 - "Community 73"
Cohesion: 0.17
Nodes (6): Color, Rect, Vector2Int, Color, LevelCanvasUndoState, Event

### Community 74 - "Community 74"
Cohesion: 0.16
Nodes (9): ContextMenu, List, LevelManager, CurrentLevel, CurrentLevelIndex, Instance, Levels, Sequence (+1 more)

### Community 75 - "Community 75"
Cohesion: 0.09
Nodes (22): Vector2, InputAbstraction, Key_LeftShift, Key_RightShift, KeyDown_1, KeyDown_2, KeyDown_3, KeyDown_4 (+14 more)

### Community 76 - "Community 76"
Cohesion: 0.14
Nodes (10): Button, Camera, ContextMenu, List, Vector2, PixelCubeInteraction, Instance, PoppedCount (+2 more)

### Community 77 - "Community 77"
Cohesion: 0.13
Nodes (13): Collider, Color, GameObject, Material, MaterialPropertyBlock, MeshFilter, MeshRenderer, Quaternion (+5 more)

### Community 78 - "Community 78"
Cohesion: 0.21
Nodes (4): Color, List, ShipDispatcher, ShipQueuePool

### Community 79 - "Community 79"
Cohesion: 0.15
Nodes (10): Color, GameThemeSettings, LevelColorTheme, Material, MenuItem, TruckPart, Vector2, GameThemeSettingsWindow (+2 more)

### Community 80 - "Community 80"
Cohesion: 0.20
Nodes (10): Color, Material, Shader, Material, ShaderPropertiesGUI(), SmoothnessMapChannel, AlbedoAlpha, SpecularMetallicAlpha (+2 more)

### Community 81 - "Community 81"
Cohesion: 0.18
Nodes (5): AudioSource, FloatOptions, TweenerCore, DOTweenModuleAudio, AudioMixer

### Community 82 - "Community 82"
Cohesion: 0.21
Nodes (13): Color, FloatOptions, Path, PathMode, PathOptions, PathType, Rigidbody2D, Sequence (+5 more)

### Community 83 - "Community 83"
Cohesion: 0.17
Nodes (12): Action, Quaternion, Transform, Vector3, CubeMovementController, BankAngle, BaseScale, IsFinished (+4 more)

### Community 84 - "Community 84"
Cohesion: 0.23
Nodes (5): GUIContent, GenericMenu, MenuFunction2, Imp_CustomMaterialProperty, Imp_ShaderPropertyReference

### Community 85 - "Community 85"
Cohesion: 0.28
Nodes (11): Button, Color, GameObject, Image, Outline, RectTransform, Sprite, TextMeshProUGUI (+3 more)

### Community 86 - "Community 86"
Cohesion: 0.15
Nodes (11): AssetImportContext, GUIContent, ShaderOption, ShaderOptionCategory, TCP2, Unity, TCP2_ShaderImporter, ComparisonOperator (+3 more)

### Community 87 - "Community 87"
Cohesion: 0.24
Nodes (6): Color, List, Texture2D, Vector3, OptimizationResult, PixelPaletteOptimizer

### Community 88 - "Community 88"
Cohesion: 0.21
Nodes (8): Color, List, Material, MaterialEditor, MaterialProperty, Shader, TCP2_MaterialInspector, targetMaterial

### Community 89 - "Community 89"
Cohesion: 0.14
Nodes (3): List, Shader, ShaderImporter

### Community 90 - "Community 90"
Cohesion: 0.12
Nodes (18): BlendMode, Cutout, Fade, Opaque, Transparent, GUIContent, MaterialEditor, MaterialProperty (+10 more)

### Community 91 - "Community 91"
Cohesion: 0.17
Nodes (9): Dictionary, ShaderImporter, Float4Packer, Pack, Variable, VertexToFragmentVariablesManager, Float4Packer, Pack (+1 more)

### Community 92 - "Community 92"
Cohesion: 0.13
Nodes (6): AvailableValue, Imp_ShaderPropertyReference, HasErrors, LinkedShaderProperty, MenuLabel, VariableCompatibility

### Community 93 - "Community 93"
Cohesion: 0.12
Nodes (10): HsvType, Colorize, FullOffset, SaturationOffset, Imp_HSV, HasErrors, MenuLabel, noColorizeChannels (+2 more)

### Community 94 - "Community 94"
Cohesion: 0.19
Nodes (6): IList, Vector3, ShoreLanePath, End, Length, Start

### Community 95 - "Community 95"
Cohesion: 0.11
Nodes (8): MarinaSlotLayoutEditor, PixelLevelDataEditor, TCP2_Demo_Interactive_Environment_Editor, TCP2_GetPosOnWater_Editor, TCP2_ShaderUpdateUnityTime_Editor, GUIContent, TCP2_PlanarReflectionEditor, Editor

### Community 96 - "Community 96"
Cohesion: 0.22
Nodes (5): ContextMenu, List, Material, MeshFilter, MarinaSlotCountConfig

### Community 97 - "Community 97"
Cohesion: 0.18
Nodes (13): CubeMovementSettings, MenuItem, ShipDispatcher, SetupCubeMovementSettings, Camera, Collider, MarinaSlotLayout, Material (+5 more)

### Community 98 - "Community 98"
Cohesion: 0.29
Nodes (10): Button, CasualHudController, GameObject, Image, MenuItem, RectTransform, Sprite, Transform (+2 more)

### Community 99 - "Community 99"
Cohesion: 0.20
Nodes (11): GameObject, Material, MenuItem, Mesh, MeshFilter, MeshRenderer, PixelArtGenerator, PixelCube (+3 more)

### Community 100 - "Community 100"
Cohesion: 0.20
Nodes (5): Rect, Imp_MaterialProperty, Imp_ConstantFloat, Imp_MaterialProperty_Float, Imp_MaterialProperty_Range

### Community 101 - "Community 101"
Cohesion: 0.20
Nodes (11): Dictionary, Expression, ExpressionAnd, ExpressionLeaf, ExpressionNot, ExpressionOr, ExpressionParser, Enumerator (+3 more)

### Community 102 - "Community 102"
Cohesion: 0.20
Nodes (10): Color, Material, Renderer, RenderTexture, Shader, TCP2_PlanarReflection, CommandBuffer, LayerMask (+2 more)

### Community 103 - "Community 103"
Cohesion: 0.24
Nodes (10): Color, ColorOptions, FloatOptions, TweenerCore, Vector4, VectorOptions, DOTweenModuleEPOOutline, OutlineProperties (+2 more)

### Community 104 - "Community 104"
Cohesion: 0.26
Nodes (9): FloatOptions, ShakeRandomnessMode, Tweener, TweenerCore, Vector2, Vector3, VectorOptions, DOTweenModuleUIToolkit (+1 more)

### Community 105 - "Community 105"
Cohesion: 0.16
Nodes (12): Collider, Color, GameObject, Material, MeshRenderer, Vector2, Vector3, UpperBeachGround (+4 more)

### Community 106 - "Community 106"
Cohesion: 0.19
Nodes (4): PixelArtGenerator, PixelLevelData, Texture2D, PresetType

### Community 107 - "Community 107"
Cohesion: 0.16
Nodes (11): List, Material, MaterialEditor, MaterialProperty, Shader, Stack, MaterialInspector_SG2, targetMaterial (+3 more)

### Community 108 - "Community 108"
Cohesion: 0.17
Nodes (8): TCP2ColorNoAlphaDrawer, TCP2HeaderHelpDecorator, TCP2KeywordFilterDrawer, TCP2SeparatorDecorator, TCP2TextureSingleLine, TCP2Vector3FloatsDrawer, TCP2Vector4FloatsDrawer, MaterialPropertyDrawer

### Community 109 - "Community 109"
Cohesion: 0.12
Nodes (13): Color, TMP_FontAsset, GameThemeSettings, CurrentTheme, Instance, MainFont, PreviewBlockColor, Theme (+5 more)

### Community 110 - "Community 110"
Cohesion: 0.26
Nodes (6): Argument, Dictionary, List, TextAsset, Argument, Module

### Community 112 - "Community 112"
Cohesion: 0.16
Nodes (10): Collider, MarinaSlotLayout, Material, MenuItem, Mesh, MeshFilter, MeshRenderer, ShipSlot (+2 more)

### Community 113 - "Community 113"
Cohesion: 0.19
Nodes (11): GameObject, Mesh, MeshFilter, Vector3, FracturedCubeData, Instance, IntactMesh, ShardCount (+3 more)

### Community 115 - "Community 115"
Cohesion: 0.13
Nodes (9): Vector3, TCP2_Demo_AutoRotate, Transform, TCP2_Demo_Interactive_Content, Material, Shader, TCP2_Demo_PipelineMaterial, TCP2_ShaderUpdateUnityTime (+1 more)

### Community 116 - "Community 116"
Cohesion: 0.15
Nodes (10): Material, Shader, GC_IfDisableKeyword, GC_IfDisableProperty, GC_IfKeyword, expression, materials, GC_IfProperty (+2 more)

### Community 117 - "Community 117"
Cohesion: 0.13
Nodes (15): TerrainLayerVariable, diffuseRemapMax, diffuseRemapMin, diffuseTexture, error, maskMapRemapMax, maskMapRemapMin, maskMapTexture (+7 more)

### Community 118 - "Community 118"
Cohesion: 0.20
Nodes (7): PointerEventData, Tween, Vector3, CasualUIButtonJuice, IPointerDownHandler, IPointerExitHandler, IPointerUpHandler

### Community 119 - "Community 119"
Cohesion: 0.21
Nodes (6): Coroutine, IEnumerator, Vector3, FoamSlotBobbing, PhaseOffset, MeshRenderer

### Community 120 - "Community 120"
Cohesion: 0.19
Nodes (11): Canvas, CanvasGroup, CanvasScaler, GameObject, GraphicRaycaster, Image, Outline, RectTransform (+3 more)

### Community 122 - "Community 122"
Cohesion: 0.16
Nodes (9): AnimatedProperty, AnimationCurve, Material, AnimatedProperty, MaterialPropertyType, Color, Float, TCP2_Demo_AnimateMaterial (+1 more)

### Community 123 - "Community 123"
Cohesion: 0.16
Nodes (10): Camera, CasualHudController, LinkedShipTether, MarinaSlotLayout, MenuItem, PixelArtGenerator, ShipController, ShipQueuePool (+2 more)

### Community 124 - "Community 124"
Cohesion: 0.21
Nodes (6): GameObject, PixelArtGenerator, PixelCubeInteraction, RectTransform, Texture2D, PixelArtAutoSetup

### Community 125 - "Community 125"
Cohesion: 0.19
Nodes (8): MarinaSlotLayout, Material, MenuItem, Mesh, ShipSlot, Texture2D, UpperBeachGround, SetupCurvedMarinaPier

### Community 126 - "Community 126"
Cohesion: 0.15
Nodes (11): Camera, GameObject, HypercasualWaterController, MarinaSlotLayout, MenuItem, PixelArtGenerator, RawImage, ShipDispatcher (+3 more)

### Community 127 - "Community 127"
Cohesion: 0.24
Nodes (6): GUIContent, MaterialEditor, MaterialProperty, Styles, TCP2_MaterialInspector_PBS, ColorPickerHDRConfig

### Community 128 - "Community 128"
Cohesion: 0.22
Nodes (6): Material, MaterialProperty, Shader, TCP2_OutlineInspector, targetMaterial, MaterialEditor

### Community 129 - "Community 129"
Cohesion: 0.23
Nodes (5): Imp_LocalNormal, Imp_LocalPosition, Imp_ObjectWorldPosition, Imp_WorldNormal, Imp_WorldPosition

### Community 131 - "Community 131"
Cohesion: 0.24
Nodes (5): Dictionary, PixelCube, Vector2Int, SceneCubeCountResult, SceneCubeCountResult

### Community 132 - "Community 132"
Cohesion: 0.24
Nodes (8): Color, ColorOptions, Gradient, Sequence, SpriteRenderer, Tweener, TweenerCore, DOTweenModuleSprite

### Community 133 - "Community 133"
Cohesion: 0.23
Nodes (3): Quaternion, Vector3, CargoRunnerHeading

### Community 134 - "Community 134"
Cohesion: 0.26
Nodes (7): List, Quaternion, Transform, Vector3, Leg, QuadLegWalker, Leg

### Community 136 - "Community 136"
Cohesion: 0.24
Nodes (3): MenuItem, TextMeshProUGUI, TestShipDeparture

### Community 137 - "Community 137"
Cohesion: 0.23
Nodes (4): List, Token, Token, TokenType

### Community 138 - "Community 138"
Cohesion: 0.24
Nodes (8): Color, ShaderPropertyType, Texture2D, Vector2, Vector4, TerrainLayerProperty, TerrainLayer, TerrainLayerVariable

### Community 139 - "Community 139"
Cohesion: 0.20
Nodes (7): GUIContent, GUIStyle, Rect, Vector2, Tooltip, style, EditorWindow

### Community 140 - "Community 140"
Cohesion: 0.23
Nodes (5): AssetImporter, MessageType, Texture2D, TCP2GradientDrawer, TCP2HelpBoxDecorator

### Community 141 - "Community 141"
Cohesion: 0.24
Nodes (3): Rect, TCP2HeaderDecorator, TCP2OutlineNormalsGUIDrawer

### Community 142 - "Community 142"
Cohesion: 0.20
Nodes (5): DefaultAsset, MenuItem, Vector2, DefaultAsset, EmojiList

### Community 143 - "Community 143"
Cohesion: 0.29
Nodes (6): Vector4, Camera, Vector3, Vector4, Matrix4x4, ScriptableRenderContext

### Community 144 - "Community 144"
Cohesion: 0.22
Nodes (10): GUIContent, MaterialEditor, MaterialProperty, FindProperties(), OnGUI(), Styles, WorkflowMode, Dielectric (+2 more)

### Community 145 - "Community 145"
Cohesion: 0.29
Nodes (3): TextAsset, ShaderGeneratorTemplate, textAsset

### Community 147 - "Community 147"
Cohesion: 0.18
Nodes (11): BlendFactor, DstAlpha, DstColor, One, OneMinusDstAlpha, OneMinusDstColor, OneMinusSrcAlpha, OneMinusSrcColor (+3 more)

### Community 148 - "Community 148"
Cohesion: 0.24
Nodes (6): AnimationCurve, CubeMovementSettings, Default, GlidePickupLift, GlidePickupSquash, ICargoRunner

### Community 149 - "Community 149"
Cohesion: 0.27
Nodes (5): Quaternion, Transform, Vector2, Vector3, RunnerLegUpright

### Community 150 - "Community 150"
Cohesion: 0.24
Nodes (9): Corner_Gear GameObject, Gear Frame Sprite Set (frames 23-35), Conveyor Speed Sync, Corner_BL Anchor, Corner Gear, Gear Frame Sequence, Gear Rotation Animation, Spinning Gear (+1 more)

### Community 151 - "Community 151"
Cohesion: 0.22
Nodes (7): GameObject, MenuItem, MeshRenderer, PixelArtGenerator, PixelCube, ShipDispatcher, CleanupSceneShadows

### Community 152 - "Community 152"
Cohesion: 0.20
Nodes (9): BlendOperation, Add, Max, Min, RevSub, Sub, DepthWrite, Off (+1 more)

### Community 154 - "Community 154"
Cohesion: 0.28
Nodes (3): AssetPostprocessor, PixelArtTextureImportRule, TCP2_ShaderPostProcessor

### Community 155 - "Community 155"
Cohesion: 0.31
Nodes (4): FracturedCubeData, Mesh, MeshFilter, FracturedCubeSetup

### Community 156 - "Community 156"
Cohesion: 0.31
Nodes (5): GameObject, Material, MeshRenderer, Shader, SetupCartoonShader

### Community 159 - "Community 159"
Cohesion: 0.25
Nodes (7): Material, MaterialEditor, MaterialProperty, Stack, TCP2_MaterialInspector_SG, targetMaterial, ShaderGUI

### Community 160 - "Community 160"
Cohesion: 0.28
Nodes (3): Imp_HSV, Imp_MaterialProperty_Color, Imp_VertexColor

### Community 161 - "Community 161"
Cohesion: 0.22
Nodes (9): CompareFunction, Always, Equal, GEqual, Greater, LEqual, Less, Never (+1 more)

### Community 162 - "Community 162"
Cohesion: 0.22
Nodes (9): StencilOperation, DecrSat, DecrWrap, IncrSat, IncrWrap, Invert, Keep, Replace (+1 more)

### Community 163 - "Community 163"
Cohesion: 0.22
Nodes (9): VariableType, color, color_rgba, fixed_function_enum, fixed_function_float, @float, float2, float3 (+1 more)

### Community 164 - "Community 164"
Cohesion: 0.42
Nodes (4): List, TCP2ShaderImporter_Editor, Importer, TCP2_ShaderImporter

### Community 165 - "Community 165"
Cohesion: 0.44
Nodes (4): Transform, Vector3, Vector4, TCP2_GetVertexWavesPosition

### Community 166 - "Community 166"
Cohesion: 0.39
Nodes (4): Color, Material, Shader, CartoonShader

### Community 167 - "Community 167"
Cohesion: 0.29
Nodes (5): Text, TextMeshProUGUI, Transform, InspectSceneTexts, TextMesh

### Community 168 - "Community 168"
Cohesion: 0.25
Nodes (8): DetailTab, ColorStudio, DifficultyTable, LevelSetup, MysteryCubes, SceneTools, SimpleMode, TruckLayout

### Community 169 - "Community 169"
Cohesion: 0.25
Nodes (6): BoxCollider, Material, MenuItem, MeshFilter, MeshRenderer, SetupBoatHousePrefab

### Community 170 - "Community 170"
Cohesion: 0.36
Nodes (4): LandFlowLoadingScreen, MenuItem, Sprite, SetupLoadingScreen

### Community 171 - "Community 171"
Cohesion: 0.36
Nodes (3): Action, RenderingMode, RenderQueue

### Community 172 - "Community 172"
Cohesion: 0.39
Nodes (3): Material, Vector3, TCP2_GetPosOnWater

### Community 173 - "Community 173"
Cohesion: 0.25
Nodes (8): MovementState, Anticipation, Arriving, Boarding, Completed, Idle, Moving, Settling

### Community 174 - "Community 174"
Cohesion: 0.25
Nodes (6): CubeAssignmentState, ArrivedAtShip, Assigned, Locked, MovingToShip, Unassigned

### Community 176 - "Community 176"
Cohesion: 0.38
Nodes (4): Component, StringBuilder, Transform, DumpCornerAndCanvasHierarchy

### Community 177 - "Community 177"
Cohesion: 0.29
Nodes (4): GameObject, MenuItem, ShipController, UpdateShipColorsMenu

### Community 179 - "Community 179"
Cohesion: 0.29
Nodes (7): TokenType, BINARY_OP, CLOSE_PAREN, EXPR_END, LITERAL, OPEN_PAREN, UNARY_OP

### Community 180 - "Community 180"
Cohesion: 0.38
Nodes (4): Mesh, Vector3, SmoothedNormalsChannel, SmoothedNormalsUVType

### Community 181 - "Community 181"
Cohesion: 0.29
Nodes (7): SmoothedNormalsChannel, Tangents, UV1, UV2, UV3, UV4, VertexColors

### Community 183 - "Community 183"
Cohesion: 0.43
Nodes (3): Quaternion, Vector3, MarinaBuoyBobbing

### Community 184 - "Community 184"
Cohesion: 0.33
Nodes (4): Quaternion, Tween, Vector3, VisualSupportIdle

### Community 185 - "Community 185"
Cohesion: 0.33
Nodes (4): Culling, Back, Front, Off

### Community 187 - "Community 187"
Cohesion: 0.33
Nodes (6): ComparisonOperator, Equal, Greater, GreaterOrEqual, Less, LessOrEqual

### Community 189 - "Community 189"
Cohesion: 0.33
Nodes (3): Material, Renderer, TCP2_GetVertexWavesPosition_Editor

### Community 190 - "Community 190"
Cohesion: 0.33
Nodes (4): List, Material, Shader, TCP2_RuntimeUtils

### Community 191 - "Community 191"
Cohesion: 0.40
Nodes (5): SlotOption, AutoByDifficulty, Fixed3, Fixed4, Fixed5

### Community 192 - "Community 192"
Cohesion: 0.40
Nodes (5): PresetType, Forest, Neon, Pastel, Sunset

### Community 194 - "Community 194"
Cohesion: 0.40
Nodes (4): Material, TCP2_Demo_InvertedMaskImage, materialForRendering, Image

### Community 195 - "Community 195"
Cohesion: 0.40
Nodes (5): BlendMode, Cutout, Fade, Opaque, Transparent

### Community 196 - "Community 196"
Cohesion: 0.40
Nodes (5): BlendMode, Cutout, Fade, Opaque, Transparent

### Community 197 - "Community 197"
Cohesion: 0.40
Nodes (3): IndentedLine, ButtonClick, IDisposable

### Community 198 - "Community 198"
Cohesion: 0.40
Nodes (5): ProgramType, FixedFunction, Fragment, Undefined, Vertex

### Community 200 - "Community 200"
Cohesion: 0.40
Nodes (5): TagType, Elif, Else, End, If

### Community 201 - "Community 201"
Cohesion: 0.50
Nodes (4): ColorReductionMode, Fixed, Original, Progression

### Community 202 - "Community 202"
Cohesion: 0.50
Nodes (4): CanvasTool, Damlalik, Kalem, RenkDegistir

### Community 203 - "Community 203"
Cohesion: 0.50
Nodes (4): Difficulty, Kolay, Orta, Zor

### Community 205 - "Community 205"
Cohesion: 0.50
Nodes (4): MysteryBrushMode, Erase, Paint, Toggle

### Community 206 - "Community 206"
Cohesion: 0.50
Nodes (4): WagonDifficultyMode, Easy, Hard, Medium

### Community 207 - "Community 207"
Cohesion: 0.50
Nodes (4): RenderingMode, Fade, Opaque, Transparent

### Community 208 - "Community 208"
Cohesion: 0.50
Nodes (4): WorkflowMode, Dielectric, Metallic, Specular

### Community 209 - "Community 209"
Cohesion: 0.50
Nodes (4): ParseBlock, Features, Flags, None

### Community 210 - "Community 210"
Cohesion: 0.50
Nodes (4): FloatPrecision, @fixed, @float, half

### Community 211 - "Community 211"
Cohesion: 0.67
Nodes (3): SourceType, EmojiApi, FolderOrDrop

### Community 212 - "Community 212"
Cohesion: 0.67
Nodes (3): SourceMode, Ciz, Gorsel

### Community 214 - "Community 214"
Cohesion: 0.67
Nodes (3): ImageSource, GoogleNoto, Twemoji

### Community 215 - "Community 215"
Cohesion: 0.67
Nodes (3): SmoothnessMapChannel, AlbedoAlpha, SpecularMetallicAlpha

## Knowledge Gaps
- **808 isolated node(s):** `Conveyor Speed Sync`, `Corner_BL Anchor`, `TrackFlow Conveyor`, `UI Sprite Frame Animation`, `IsRunning` (+803 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1650 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **17 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `ShaderProperty` connect `TCP2 Shader Templates` to `Community 2`, `Community 4`, `Community 7`, `Community 13`, `Community 17`, `Community 146`, `Community 19`, `Community 152`, `Community 26`, `Community 28`, `Community 163`, `Community 36`, `Community 45`, `Community 49`, `Community 51`, `Community 61`, `Community 62`, `Community 65`, `Community 69`, `Community 70`, `Community 198`, `Community 210`, `Community 84`, `Community 92`, `Community 93`?**
  _High betweenness centrality (0.158) - this node is a cross-community bridge._
- **Why does `ShaderGenerator2` connect `Community 13` to `Community 129`, `Community 5`, `Community 7`, `Community 139`, `Community 17`, `Community 178`, `Community 23`, `Community 91`, `Community 28`?**
  _High betweenness centrality (0.120) - this node is a cross-community bridge._
- **Why does `PixelLevelDesignerWindow` connect `Level Sequence Marina` to `Community 192`, `Community 131`, `Community 39`, `Community 168`, `Community 106`, `Community 139`, `Community 205`, `Community 78`, `Community 175`, `Community 111`, `Community 79`, `Community 206`, `Community 21`, `Community 25`, `Community 27`, `Community 29`, `Community 31`?**
  _High betweenness centrality (0.119) - this node is a cross-community bridge._
- **What connects `Conveyor Speed Sync`, `Corner_BL Anchor`, `TrackFlow Conveyor` to the rest of the system?**
  _808 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `TCP2 Shader Templates` be split into smaller, more focused modules?**
  _Cohesion score 0.04323936932632585 - nodes in this community are weakly interconnected._
- **Should `ShipController` be split into smaller, more focused modules?**
  _Cohesion score 0.02721518987341772 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.034299034299034296 - nodes in this community are weakly interconnected._