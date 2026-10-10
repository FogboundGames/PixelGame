using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class InspectCargoMesh
    {
        private static bool s_AutoNormalized = false;
        static InspectCargoMesh()
        {
            EditorApplication.delayCall += () =>
            {
                // Play mode'da sahne düzenlenemez (MarkSceneDirty hata atar) ve canlı küplere dokunmamalı.
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!s_AutoNormalized)
                {
                    s_AutoNormalized = true;
                    NormalizeAndAlignCubes();
                }
            };
        }

        [MenuItem("PixelGame/✨ Konteynerleri Hizala ve Güncelle (Normalize All)")]
        public static void NormalizeAndAlignCubes()
        {
            var gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen == null)
            {
                Debug.LogWarning("[Normalize] Sahnede PixelArtGenerator bulunamadı!");
                return;
            }

            gen.UpdateExistingCubesTransforms();
            gen.ApplyShadowsToAllExistingCubes();
            EditorUtility.SetDirty(gen.gameObject);
            if (gen.CubesContainer != null)
            {
                EditorUtility.SetDirty(gen.CubesContainer.gameObject);
                foreach (Transform child in gen.CubesContainer)
                {
                    EditorUtility.SetDirty(child.gameObject);
                }
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gen.gameObject.scene);
            Debug.Log("<color=#00FFAA>[Normalize]</color> Tüm konteyner yükleri başarıyla normalize edildi ve sahne güncellendi!");
        }

        [MenuItem("PixelGame/🔍 Check Pink Materials")]
        public static void CheckPinkMaterials()
        {
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null)
                    {
                        Debug.LogError($"[PINK] Renderer {r.gameObject.name} (parent: {r.transform.parent?.name}) has NULL material!");
                    }
                    else if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader" || !mat.shader.isSupported)
                    {
                        Debug.LogError($"[PINK] Renderer {r.gameObject.name} has ERROR/UNSUPPORTED shader: {mat.shader?.name} on material {mat.name}!");
                    }
                }
            }

            foreach (var pPath in new[] { "Assets/Prefabs/MainCube_Running_Tabletop.prefab", "Assets/Prefabs/MainCube_Running.prefab", "Assets/Prefabs/MainCube_Tabletop.prefab", "Assets/Prefabs/MainCube.prefab" })
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(pPath);
                if (p == null) continue;
                foreach (var r in p.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var mat in r.sharedMaterials)
                    {
                        if (mat == null)
                            Debug.LogError($"[PINK PREFAB] {pPath} -> {r.gameObject.name} has NULL material!");
                        else if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader" || !mat.shader.isSupported)
                            Debug.LogError($"[PINK PREFAB] {pPath} -> {r.gameObject.name} has ERROR/UNSUPPORTED shader: {mat.shader?.name} on {mat.name}!");
                        else
                            Debug.Log($"[OK PREFAB] {pPath} -> {r.gameObject.name} has shader: {mat.shader.name}");
                    }
                }
            }
        }

        [MenuItem("PixelGame/🔍 Inspect Cargo Mesh")]
        public static void Inspect()
        {
            CheckPinkMaterials();
            foreach (var name in new[] { "cargo-container-a.fbx", "cargo-container-b.fbx", "cargo-container-c.fbx" })
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Kenney/kenney_watercraft-pack/Models/FBX format/{name}");
                if (fbx != null)
                {
                    var mf = fbx.GetComponentInChildren<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var b = mf.sharedMesh.bounds;
                        Debug.Log($"<color=#00FFAA>[FBX {name}]</color> Center: {b.center}, Size: {b.size}, Min: {b.min}, Max: {b.max}");
                    }
                }
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MainCube_Tabletop.prefab");
            if (prefab != null)
            {
                Debug.Log($"<color=#00FFAA>[Prefab Root]</color> Name: {prefab.name}, Pos: {prefab.transform.localPosition}, Rot: {prefab.transform.localEulerAngles}, Scale: {prefab.transform.localScale}");
                foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    var b = mf.sharedMesh != null ? mf.sharedMesh.bounds : default;
                    Debug.Log($"  MF: {mf.gameObject.name}, Mesh: {(mf.sharedMesh != null ? mf.sharedMesh.name : "null")}, LocalPos: {mf.transform.localPosition}, LocalRot: {mf.transform.localEulerAngles}, LocalScale: {mf.transform.localScale}, BoundsSize: {b.size}, BoundsCenter: {b.center}");
                }
            }

            var gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen != null)
            {
                Debug.Log($"<color=#00FFAA>[Generator]</color> Spacing: {gen.CubeSpacing}, SpacingX: {gen.CubeSpacingX}, Depth: {gen.CubeDepth}, FrontTilt: {gen.CubeFrontTiltAngle}");
            }

            var container = gen != null ? gen.CubesContainer : null;
            if (container != null)
            {
                Debug.Log($"<color=#00FFAA>[Container]</color> ChildCount: {container.childCount}, Scale: {container.localScale}");
                if (container.childCount > 0)
                {
                    var firstChild = container.GetChild(0);
                    Debug.Log($"  FirstChild: {firstChild.name}, Pos: {firstChild.localPosition}, Rot: {firstChild.localEulerAngles}, Scale: {firstChild.localScale}");
                    var mf = firstChild.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var b = mf.sharedMesh.bounds;
                        Debug.Log($"  FirstChild Mesh: {mf.sharedMesh.name}, BoundsSize: {b.size}, BoundsCenter: {b.center}");
                    }
                }
            }
        }
    }
}
