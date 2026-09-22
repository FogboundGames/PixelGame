import os

prefab_paths = [
    r"Assets/Prefabs/KawaiiCubeWagon.prefab",
    r"Assets/Prefabs/CyberCubeWagon.prefab"
]

face_blocks = """--- !u!1 &1000000000000002
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 4000000000000002}
  - component: {fileID: 2120000000000001}
  m_Layer: 0
  m_Name: Face
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &4000000000000002
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1000000000000002}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0.42, z: -0.526}
  m_LocalScale: {x: 0.85, y: 0.85, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 4000000000000001}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!212 &2120000000000001
SpriteRenderer:
  serializedVersion: 2
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1000000000000002}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 0
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 0
  m_RayTraceProcedural: 0
  m_RayTracingAccelStructBuildFlagsOverride: 0
  m_RayTracingAccelStructBuildFlags: 1
  m_SmallMeshCulling: 1
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {fileID: 10754, guid: 0000000000000000f000000000000000, type: 0}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {fileID: 0}
  m_ProbeAnchor: {fileID: 0}
  m_LightProbeVolumeOverride: {fileID: 0}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 0
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {fileID: 0}
  m_GlobalIlluminationMeshLod: 0
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 15
  m_MaskInteraction: 0
  m_Sprite: {fileID: 21300000, guid: d607eb94d218444c907d41f11d28b1b1, type: 3}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_FlipX: 0
  m_FlipY: 0
  m_DrawMode: 0
  m_Size: {x: 1, y: 1}
  m_AdaptiveModeThreshold: 0.5
  m_SpriteTileMode: 0
  m_WasSpriteAssigned: 1
  m_SpriteSortPoint: 0
--- !u!114 &1140000000000009
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1000000000000001}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: e9a31749bf414ba692d04cae27f71120, type: 3}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::PixelGame.KawaiiFaceController
  m_Expression: -1
  m_FaceRenderer: {fileID: 2120000000000001}
  m_SmileSprite: {fileID: 21300000, guid: d607eb94d218444c907d41f11d28b1b1, type: 3}
  m_WinkSprite: {fileID: 21300000, guid: f13d17546a0f4e5caf759f6fa7eb06fa, type: 3}
  m_HappySprite: {fileID: 21300000, guid: 2f313efcfecd4fcd8f62743037498510, type: 3}
  m_ExcitedSprite: {fileID: 21300000, guid: 8a845a6ead55417dbdd5e8d00456d771, type: 3}
  m_CatSprite: {fileID: 21300000, guid: 4ea2b7d495284c99a5999cc03ebfc2d3, type: 3}
"""

for p in prefab_paths:
    if not os.path.exists(p): continue
    with open(p, "r", encoding="utf-8") as f:
        content = f.read()

    # Skip if already injected
    if "&1000000000000002" in content:
        print(f"Already injected: {p}")
        continue

    # Add component to root GameObject
    target_comp = "  - component: {fileID: 6500000000000001}\n"
    repl_comp = "  - component: {fileID: 6500000000000001}\n  - component: {fileID: 1140000000000009}\n"
    if target_comp in content:
        content = content.replace(target_comp, repl_comp, 1)

    # Add child to root Transform
    target_child = "  m_Children:\n  - {fileID: 4318671170707779090}\n"
    repl_child = "  m_Children:\n  - {fileID: 4318671170707779090}\n  - {fileID: 4000000000000002}\n"
    if target_child in content:
        content = content.replace(target_child, repl_child, 1)
    else:
        # Fallback if other child exists
        target_child2 = "  m_Children:\n"
        repl_child2 = "  m_Children:\n  - {fileID: 4000000000000002}\n"
        content = content.replace(target_child2, repl_child2, 1)

    # Append Face blocks
    content = content.rstrip() + "\n" + face_blocks

    with open(p, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"Successfully injected face child into: {p}")
