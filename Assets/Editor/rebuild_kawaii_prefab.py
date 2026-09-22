import os

def rebuild_kawaii_prefab():
    target_path = r"Assets/Prefabs/KawaiiCubeWagon.prefab"
    cyber_path = r"Assets/Prefabs/CyberCubeWagon.prefab"

    prefab_content = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &1000000000000001
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 4000000000000001}
  - component: {fileID: 3300000000000001}
  - component: {fileID: 2300000000000001}
  - component: {fileID: 1140000000000001}
  - component: {fileID: 1140000000000003}
  - component: {fileID: 1140000000000002}
  - component: {fileID: 1140000000000004}
  - component: {fileID: 6500000000000001}
  - component: {fileID: 1140000000000009}
  m_Layer: 0
  m_Name: KawaiiCubeWagon
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &4000000000000001
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1000000000000001}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {fileID: 4000000000000002}
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!33 &3300000000000001
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1000000000000001}
  m_Mesh: {fileID: -1959270343172774926, guid: 6f7b2c01994e45d8b671a8bc54de0001, type: 3}
--- !u!23 &2300000000000001
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1000000000000001}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 2
  m_RayTraceProcedural: 0
  m_RayTracingAccelStructBuildFlagsOverride: 0
  m_RayTracingAccelStructBuildFlags: 1
  m_SmallMeshCulling: 1
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {fileID: 2100000, guid: 33a0390858d0564478617003876194dd, type: 2}
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
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {fileID: 0}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
  m_MaskInteraction: 0
--- !u!114 &1140000000000001
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1000000000000001}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 32f734b0a2c074f70b962b9aec38b056, type: 3}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::PixelGame.TruckCargo
  m_CargoColor: {r: 0.14117648, g: 0.53333336, b: 1, a: 1}
  m_Capacity: 8
  m_Load: 0
  m_PiecesPerCube: 12
--- !u!114 &1140000000000003
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1000000000000001}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: a682fadb4c59465d86aa267255ed0790, type: 3}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::PixelGame.WagonCapacityBadge
  m_HideModel: 0
  m_EnableFakeShadow: 1
  m_FakeShadowSprite: {fileID: 21300000, guid: 19791a8a9634a9347ba13d60348412cb, type: 3}
  m_FakeShadowColor: {r: 0.015, g: 0.025, b: 0.06, a: 0.55}
  m_FakeShadowOffset: {x: 0, y: -14}
  m_FakeShadowScale: {x: 1.5, y: 1.35}
  m_BackgroundSprite: {fileID: 0}
  m_InnerPlateSprite: {fileID: 21300000, guid: 6301e24ab1764f1d8620ae1e6a5b77e5, type: 3}
  m_ActiveShowMiniPill: 0
  m_MiniPillSprite: {fileID: 0}
  m_ActiveHeadElevation: 0.09
  m_ActiveWorldWidth: 0.65
  m_ShowBackgroundBox: 1
  m_CenterOffset: {x: 0, y: 0, z: 0.05}
  m_CustomFont: {fileID: 0}
  m_ManualOffset: {x: 0, y: 0, z: 0}
  m_PoolTargetWorldScale: 0.007
  m_ManualTiltDegrees: 0
  m_OverrideColor: {r: 0, g: 0, b: 0, a: 0}
--- !u!114 &1140000000000002
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1000000000000001}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 577416688ab6ed34b8b18dba311ceb4f, type: 3}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::PixelGame.TruckPaint
  m_UseCartoonShader: 1
  m_Cabin: {r: 0.14117648, g: 0.53333336, b: 1, a: 1}
  m_Cargo: {r: 0.14117648, g: 0.53333336, b: 1, a: 1}
--- !u!114 &1140000000000004
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1000000000000001}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 297d9e006fbd4ddbb485aa85673c56ac, type: 3}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::PixelGame.WagonClickTarget
  PoolPlace: {fileID: 0}
--- !u!65 &6500000000000001
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1000000000000001}
  m_Material: {fileID: 0}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 1
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 3
  m_Size: {x: 1.05, y: 0.92, z: 1.05}
  m_Center: {x: 0, y: 0.45, z: 0}
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
--- !u!1 &1000000000000002
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
  m_LocalRotation: {x: -0.15643448, y: 0, z: 0, w: 0.98768836}
  m_LocalPosition: {x: 0, y: 0.32, z: -0.525}
  m_LocalScale: {x: 0.85, y: 0.85, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 4000000000000001}
  m_LocalEulerAnglesHint: {x: -18, y: 0, z: 0}
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
  m_SortingOrder: 20
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
"""

    with open(target_path, "w", encoding="utf-8") as f:
        f.write(prefab_content)
    print(f"Rebuilt: {target_path}")

    cyber_content = prefab_content.replace("KawaiiCubeWagon", "CyberCubeWagon")
    with open(cyber_path, "w", encoding="utf-8") as f:
        f.write(cyber_content)
    print(f"Rebuilt: {cyber_path}")

if __name__ == "__main__":
    rebuild_kawaii_prefab()
