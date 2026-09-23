import os
import re

def build_gemi_scene():
    scene_path = "Assets/Scenes/Gemi.unity"
    with open(scene_path, "r", encoding="utf-8", errors="ignore") as f:
        text = f.read()

    docs = text.split("--- !u!")
    header = docs[0]
    blocks = {}
    
    for d in docs[1:]:
        first_line = d.split("\n", 1)[0]
        type_and_id = first_line.strip()
        parts = type_and_id.split("&")
        doc_type = parts[0].strip()
        doc_id = parts[1].strip()
        body = d[len(first_line)+1:]
        blocks[doc_id] = (doc_type, body)

    print(f"Loaded {len(blocks)} YAML blocks from Gemi.unity")

    # 1. Update Main Camera Transform (ID: 2076594661)
    cam_trans_id = "2076594661"
    if cam_trans_id in blocks:
        c_type, c_body = blocks[cam_trans_id]
        c_body = re.sub(r"m_LocalPosition: \{[^\}]+\}", "m_LocalPosition: {x: 0, y: 0, z: -12}", c_body)
        c_body = re.sub(r"m_LocalRotation: \{[^\}]+\}", "m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}", c_body)
        c_body = re.sub(r"m_LocalEulerAnglesHint: \{[^\}]+\}", "m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}", c_body)
        c_body = re.sub(r"m_Children:[\s\S]*?(?=  m_Father:)", "m_Children: []\n", c_body)
        blocks[cam_trans_id] = (c_type, c_body)
        print("Updated Camera Transform: pos=(0, 0, -12), euler=(0, 0, 0)")

    # 2. Update Main Camera Component (ID: 2076594660)
    cam_comp_id = "2076594660"
    if cam_comp_id in blocks:
        c_type, c_body = blocks[cam_comp_id]
        c_body = re.sub(r"m_ClearFlags: \d+", "m_ClearFlags: 2", c_body)
        c_body = re.sub(r"m_BackGroundColor: \{[^\}]+\}", "m_BackGroundColor: {r: 0.04, g: 0.08, b: 0.16, a: 1}", c_body)
        c_body = re.sub(r"orthographic: \d+", "orthographic: 1", c_body)
        c_body = re.sub(r"orthographic size: [^\n]+", "orthographic size: 8", c_body)
        c_body = re.sub(r"near clip plane: [^\n]+", "near clip plane: 0.3", c_body)
        c_body = re.sub(r"far clip plane: [^\n]+", "far clip plane: 100", c_body)
        blocks[cam_comp_id] = (c_type, c_body)
        print("Updated Camera Component (Orthographic size 8, SolidColor)")

    # 3. Update Directional Light Transform (ID: 863993429)
    light_trans_id = "863993429"
    if light_trans_id in blocks:
        l_type, l_body = blocks[light_trans_id]
        l_body = re.sub(r"m_Father: \{fileID: [^\}]+\}", "m_Father: {fileID: 0}", l_body)
        l_body = re.sub(r"m_LocalPosition: \{[^\}]+\}", "m_LocalPosition: {x: -3, y: 8, z: -6}", l_body)
        l_body = re.sub(r"m_LocalRotation: \{[^\}]+\}", "m_LocalRotation: {x: 0.3951656, y: -0.254887, z: 0.1147774, w: 0.8752255}", l_body)
        l_body = re.sub(r"m_LocalEulerAnglesHint: \{[^\}]+\}", "m_LocalEulerAnglesHint: {x: 48, y: -32, z: 0}", l_body)
        blocks[light_trans_id] = (l_type, l_body)
        print("Updated Directional Light Transform (unparented from camera, angle 48, -32, 0)")

    # 4. Update Directional Light Component (ID: 863993428)
    light_comp_id = "863993428"
    if light_comp_id in blocks:
        l_type, l_body = blocks[light_comp_id]
        l_body = re.sub(r"m_Color: \{[^\}]+\}", "m_Color: {r: 1, g: 0.95, b: 0.88, a: 1}", l_body)
        l_body = re.sub(r"m_Intensity: [^\n]+", "m_Intensity: 1.25", l_body)
        l_body = re.sub(r"m_Type: \d+", "m_Type: 2", l_body)
        l_body = re.sub(r"m_Strength: [^\n]+", "m_Strength: 0.72", l_body)
        blocks[light_comp_id] = (l_type, l_body)
        print("Updated Directional Light Component (warm light, soft shadows)")

    # 5. Remove unused inactive RawImage (1140978643, 1140978644, 1140978645, 1140978646)
    for unused_id in ["1140978643", "1140978644", "1140978645", "1140978646"]:
        if unused_id in blocks:
            del blocks[unused_id]
            print(f"Removed unused RawImage entity {unused_id}")

    # 6. Update Background_Canvas (ID: 1275116340)
    if "1275116340" in blocks:
        c_type, c_body = blocks["1275116340"]
        c_body = re.sub(r"m_Name: .*", "m_Name: Background_Canvas", c_body)
        blocks["1275116340"] = (c_type, c_body)

    if "1275116343" in blocks:
        c_type, c_body = blocks["1275116343"]
        c_body = re.sub(r"m_RenderMode: \d+", "m_RenderMode: 1", c_body)
        c_body = re.sub(r"m_Camera: \{[^\}]+\}", "m_Camera: {fileID: 2076594660}", c_body)
        c_body = re.sub(r"m_PlaneDistance: [^\n]+", "m_PlaneDistance: 35", c_body)
        c_body = re.sub(r"m_SortingOrder: [^\n]+", "m_SortingOrder: -100", c_body)
        blocks["1275116343"] = (c_type, c_body)
        print("Updated Canvas to ScreenSpaceCamera (planeDistance 35, sorting -100)")

    if "1275116342" in blocks:
        cs_type, cs_body = blocks["1275116342"]
        cs_body = re.sub(r"m_UiScaleMode: \d+", "m_UiScaleMode: 1", cs_body)
        cs_body = re.sub(r"m_ReferenceResolution: \{[^\}]+\}", "m_ReferenceResolution: {x: 1080, y: 1920}", cs_body)
        cs_body = re.sub(r"m_MatchWidthOrHeight: [^\n]+", "m_MatchWidthOrHeight: 0", cs_body)
        blocks["1275116342"] = (cs_type, cs_body)
        print("Updated CanvasScaler: 1080x1920 MatchWidth")

    if "1275116344" in blocks:
        rt_type, rt_body = blocks["1275116344"]
        rt_body = re.sub(r"m_Children:[\s\S]*?(?=  m_Father:)", "m_Children:\n  - {fileID: 2101272285}\n", rt_body)
        blocks["1275116344"] = (rt_type, rt_body)

    # 7. Update BackgroundImage (ID: 2101272284)
    if "2101272284" in blocks:
        r_type, r_body = blocks["2101272284"]
        r_body = re.sub(r"m_Name: .*", "m_Name: BackgroundImage", r_body)
        blocks["2101272284"] = (r_type, r_body)

    if "2101272286" in blocks:
        ri_type, ri_body = blocks["2101272286"]
        ri_body = re.sub(r"m_RaycastTarget: \d+", "m_RaycastTarget: 0", ri_body)
        blocks["2101272286"] = (ri_type, ri_body)

    if "2101272285" in blocks:
        rt_type, rt_body = blocks["2101272285"]
        rt_body = re.sub(r"m_AnchorMin: \{[^\}]+\}", "m_AnchorMin: {x: 0, y: 0}", rt_body)
        rt_body = re.sub(r"m_AnchorMax: \{[^\}]+\}", "m_AnchorMax: {x: 1, y: 1}", rt_body)
        rt_body = re.sub(r"m_AnchoredPosition: \{[^\}]+\}", "m_AnchoredPosition: {x: 0, y: 0}", rt_body)
        rt_body = re.sub(r"m_SizeDelta: \{[^\}]+\}", "m_SizeDelta: {x: 0, y: 0}", rt_body)
        blocks["2101272285"] = (rt_type, rt_body)

    # 8. Re-parent legacy prototype objects under [Legacy_Prototype_Backup]
    legacy_go_yaml = """GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 930000002}
  m_Layer: 0
  m_Name: '[Legacy_Prototype_Backup]'
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 0
"""
    legacy_trans_yaml = """Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 930000001}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {fileID: 733942742}
  - {fileID: 1626078065}
  - {fileID: 1806172392}
  - {fileID: 574146192}
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
"""
    blocks["930000001"] = ("1", legacy_go_yaml)
    blocks["930000002"] = ("4", legacy_trans_yaml)

    for tid in ["733942742", "1626078065", "1806172392", "574146192"]:
        if tid in blocks:
            b_type, b_body = blocks[tid]
            b_body = re.sub(r"m_Father: \{fileID: [^\}]+\}", "m_Father: {fileID: 930000002}", b_body)
            blocks[tid] = (b_type, b_body)

    # 9. Add Ground_ShadowCatcher (GO: 910000001, Trans: 910000002, MeshFilter: 910000003, MeshRenderer: 910000004)
    shadow_go_yaml = """GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 910000002}
  - component: {fileID: 910000003}
  - component: {fileID: 910000004}
  m_Layer: 0
  m_Name: Ground_ShadowCatcher
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""
    shadow_trans_yaml = """Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 910000001}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0.05}
  m_LocalScale: {x: 14, y: 25, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
"""
    shadow_mf_yaml = """MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 910000001}
  m_Mesh: {fileID: 10210, guid: 0000000000000000e000000000000000, type: 0}
"""
    shadow_mr_yaml = """MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 910000001}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 2
  m_RayTraceProcedural: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {fileID: 2100000, guid: 931e512dbd514463a0f9220ee132cd4f, type: 2}
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
  m_AdditionalVertexStreams: {fileID: 0}
"""
    blocks["910000001"] = ("1", shadow_go_yaml)
    blocks["910000002"] = ("4", shadow_trans_yaml)
    blocks["910000003"] = ("33", shadow_mf_yaml)
    blocks["910000004"] = ("23", shadow_mr_yaml)

    # 10. Add [GAMEPLAY_MODELS] with Zone Anchors
    gameplay_go = """GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 920000002}
  m_Layer: 0
  m_Name: '[GAMEPLAY_MODELS]'
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""
    gameplay_trans = """Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 920000001}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {fileID: 920000012}
  - {fileID: 920000022}
  - {fileID: 920000032}
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
"""
    zone_sand_go = """GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 920000012}
  m_Layer: 0
  m_Name: '[Zone_Sand_PlayArea]'
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""
    zone_sand_trans = """Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 920000011}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 4.17, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 920000002}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
"""
    zone_bridge_go = """GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 920000022}
  m_Layer: 0
  m_Name: '[Zone_Wooden_Bridge]'
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""
    zone_bridge_trans = """Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 920000021}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: -0.32, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 920000002}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
"""
    zone_water_go = """GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 920000032}
  m_Layer: 0
  m_Name: '[Zone_Water_LowerArea]'
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""
    zone_water_trans = """Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 920000031}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: -4.80, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 920000002}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
"""
    blocks["920000001"] = ("1", gameplay_go)
    blocks["920000002"] = ("4", gameplay_trans)
    blocks["920000011"] = ("1", zone_sand_go)
    blocks["920000012"] = ("4", zone_sand_trans)
    blocks["920000021"] = ("1", zone_bridge_go)
    blocks["920000022"] = ("4", zone_bridge_trans)
    blocks["920000031"] = ("1", zone_water_go)
    blocks["920000032"] = ("4", zone_water_trans)

    out_docs = [header]
    for bid, (b_type, b_body) in blocks.items():
        out_docs.append(f"{b_type} &{bid}\n{b_body.strip()}\n")

    new_content = "--- !u!".join(out_docs)
    with open(scene_path, "w", encoding="utf-8") as f:
        f.write(new_content)
    print(f"Successfully rebuilt and saved {scene_path}!")

if __name__ == "__main__":
    build_gemi_scene()
