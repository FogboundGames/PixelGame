using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace PixelGame.Editor
{
    /// <summary>
    /// MainCube prefab'ına ve türevlerine (MainCube_Tabletop vb.) referans fotoğraftaki gibi
    /// alt kısımda duran yumuşak pill/capsule sahte gölgeyi (Fake Shadow) kalıcı olarak ekler ve ayarlar.
    /// </summary>
    [InitializeOnLoad]
    public static class SetupMainCubePrefabShadow
    {
        private const string PrefabPath = "Assets/Prefabs/MainCube.prefab";
        private const string TabletopPrefabPath = "Assets/Prefabs/MainCube_Tabletop.prefab";
        private const string RunningPrefabPath = "Assets/Prefabs/MainCube_Running.prefab";
        private const string ShadowMatPath = "Assets/Materials/CubeFakeShadow_Mat.mat";
        private const string RunKey = "MainCube_PillFakeShadow_Setup_v3_perfect";

        public static readonly Vector3 ShadowLocalPos = new Vector3(0f, -0.58f, 0.52f);
        public static readonly Vector3 ShadowLocalScale = new Vector3(1.45f, 0.85f, 1f);

        static SetupMainCubePrefabShadow()
        {
            // Otomatik tetikleme kapatıldı: Unity her açıldığında prefab'ları ve sahnedeki küpleri
            // habersiz değiştirip sahneyi kirli (*) yapıyordu. Gerekirse Tools menüsünden elle çalıştırılır.
            // EditorApplication.delayCall += ApplyShadowToPrefabAndScene;
        }

        private static GameObject CreateShadowQuad(string name, Transform parent)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.layer = 2; // Ignore Raycast
            MeshFilter mf = obj.AddComponent<MeshFilter>();
            Mesh builtinQuad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            if (builtinQuad != null) mf.sharedMesh = builtinQuad;
            MeshRenderer mr = obj.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            mr.sortingOrder = -1;
            return obj;
        }

        [MenuItem("Tools/PixelGame/🌑 Küp Prefablarına Referans Fake Shadow Uygula", priority = 10)]
        public static void ApplyManual()
        {
            ApplyShadowToPrefab(force: true);
            ApplyShadowsToSceneCubes();
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Fake Shadow Hazır!", 
                    "Referans fotoğraftaki gibi küplerin alt kısmına yumuşak Fake Shadow uygulandı!\n\n" +
                    "- MainCube, MainCube_Tabletop ve MainCube_Running prefablarına CubeShadow eklendi.\n" +
                    "- Sahnedeki tüm küplere uygulandı.\n" +
                    "- Küpler yürürken de altındaki gölge korunur.", "Harika!");
            }
        }

        public static void ApplyShadowToPrefabAndScene()
        {
            if (SessionState.GetBool(RunKey, false)) return;
            SessionState.SetBool(RunKey, true);

            ApplyShadowToPrefab(force: false);
            ApplyShadowsToSceneCubes();
        }

        public static void ApplyShadowToPrefab(bool force)
        {
            Material shadowMat = AssetDatabase.LoadAssetAtPath<Material>(ShadowMatPath);
            if (shadowMat == null)
            {
                string[] guids = AssetDatabase.FindAssets("CubeFakeShadow_Mat t:Material");
                if (guids.Length > 0)
                {
                    shadowMat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            ConfigurePrefabShadow(PrefabPath, shadowMat);
            ConfigurePrefabShadow(TabletopPrefabPath, shadowMat);
            ConfigurePrefabShadow(RunningPrefabPath, shadowMat);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ConfigurePrefabShadow(string prefabPath, Material shadowMat)
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null) return;

            try
            {
                PixelCube pixelCube = root.GetComponent<PixelCube>();
                if (pixelCube == null)
                {
                    pixelCube = root.AddComponent<PixelCube>();
                }
                pixelCube.KeepShadowPermanent = false;

                Transform shadowTrans = root.transform.Find("CubeShadow");
                GameObject shadowObj = shadowTrans != null ? shadowTrans.gameObject : CreateShadowQuad("CubeShadow", root.transform);
                shadowObj.layer = 2; // Ignore Raycast
                shadowObj.SetActive(true);

                shadowObj.transform.localPosition = ShadowLocalPos;
                shadowObj.transform.localRotation = Quaternion.identity;
                shadowObj.transform.localScale = ShadowLocalScale;

                MeshRenderer mr = shadowObj.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    if (shadowMat != null) mr.sharedMaterial = shadowMat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                }

                Transform bottomTrans = root.transform.Find("CubeShadow_Bottom");
                if (bottomTrans != null)
                {
                    Object.DestroyImmediate(bottomTrans.gameObject);
                }

                pixelCube.SetShadowObjects(shadowObj, null);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"<color=#00FFAA><b>[PixelGame]</b></color> {prefabPath} prefabına Fake Shadow kaydedildi!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void ApplyShadowsToSceneCubes()
        {
            Material shadowMat = AssetDatabase.LoadAssetAtPath<Material>(ShadowMatPath);
            if (shadowMat == null)
            {
                string[] guids = AssetDatabase.FindAssets("CubeFakeShadow_Mat t:Material");
                if (guids.Length > 0)
                {
                    shadowMat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen != null)
            {
                gen.EnableCubeShadows = true;
                gen.CubeShadowMaterial = shadowMat;
                gen.ShadowOffset = new Vector2(ShadowLocalPos.x, ShadowLocalPos.y);
                gen.ShadowScale = 1.0f;
                gen.EnsureFigureContourShadowDisabled();
                gen.EnsureBoardShadowDisabled();
                gen.ApplyShadowsToAllExistingCubes();
            }

            PixelCube[] allCubes = Object.FindObjectsByType<PixelCube>(FindObjectsSortMode.None);
            foreach (var c in allCubes)
            {
                if (c == null) continue;
                c.KeepShadowPermanent = false;

                Transform bottomTrans = c.transform.Find("CubeShadow_Bottom");
                if (bottomTrans != null)
                {
                    Object.DestroyImmediate(bottomTrans.gameObject);
                }

                Transform shadowTrans = c.transform.Find("CubeShadow");
                GameObject shadowObj = shadowTrans != null ? shadowTrans.gameObject : CreateShadowQuad("CubeShadow", c.transform);
                shadowObj.layer = 2; // Ignore Raycast
                shadowObj.SetActive(true);

                shadowObj.transform.localPosition = ShadowLocalPos;
                shadowObj.transform.localRotation = Quaternion.identity;
                shadowObj.transform.localScale = ShadowLocalScale;

                MeshRenderer mr = shadowObj.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    if (shadowMat != null) mr.sharedMaterial = shadowMat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    mr.sortingOrder = -1;
                }

                c.SetShadowObjects(shadowObj, null);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            SceneView.RepaintAll();
            Debug.Log($"<color=#00FFAA><b>[PixelGame]</b></color> Sahnedeki {allCubes.Length} adet küpe referans Fake Shadow uygulandı ve sahne kaydedildi!");
        }
    }
}
