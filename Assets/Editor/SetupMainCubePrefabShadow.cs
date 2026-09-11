using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace PixelGame.Editor
{
    /// <summary>
    /// MainCube prefab'ına kalıcı olarak 360 derece çevreleyen sahte gölgeleri (Back & Bottom Quads) ekler.
    /// Oyunu başlatmadan da (Edit Mode) sahnede tam önizleme yapılmasını sağlar.
    /// </summary>
    [InitializeOnLoad]
    public static class SetupMainCubePrefabShadow
    {
        private const string PrefabPath = "Assets/Prefabs/MainCube.prefab";
        private const string ShadowMatPath = "Assets/Materials/SoftVoxelShadow_Mat.mat";
        private const string RunKey = "MainCube_AllAround_Shadow_Setup_v9";

        static SetupMainCubePrefabShadow()
        {
            EditorApplication.delayCall += ApplyShadowToPrefabAndScene;
        }

        private static GameObject CreateShadowQuad(string name, Transform parent)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            MeshFilter mf = obj.AddComponent<MeshFilter>();
            Mesh builtinQuad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            if (builtinQuad != null) mf.sharedMesh = builtinQuad;
            MeshRenderer mr = obj.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return obj;
        }

        [MenuItem("Tools/PixelGame/👁️ Sahnede Piksel Resmi ve Gölgeleri Canlı Önizle")]
        public static void PreviewInSceneManual()
        {
            ApplyShadowToPrefab(force: true);
            ApplyShadowsToSceneCubes();
            EditorUtility.DisplayDialog("Canlı Önizleme Hazır!", 
                "Piksel resmi, tüm şekil kontur gölgesi ve küp gölgeleri sahneye başarıyla yerleştirildi!\n\n" +
                "- Şeklin dış hatları ikinci görseldeki gibi belirgin ve yumuşak kontur gölgesine sahiptir.\n" +
                "- Oyunu başlatmadan da Scene view üzerinde tam görsel önizleme yapabilirsiniz.\n" +
                "- Küp patladığında parçalar dökülür, gölgeler panoda sabit kalır.", "Harika!");
        }

        [MenuItem("Tools/PixelGame/🌑 MainCube Prefabına Fake Shadow Ekle (Kalıcı ve Her Yönde)")]
        public static void ApplyManual()
        {
            ApplyShadowToPrefab(force: true);
            ApplyShadowsToSceneCubes();
            EditorUtility.DisplayDialog("Fake Shadow Prefab'a Eklendi", 
                "MainCube prefab'ına ve sahnedeki tüm küplere 360 derece Fake Shadow başarıyla eklendi!\n\n" +
                "- Hem arka panelde hem de 3D alt zeminde gölge hazırlandı.\n" +
                "- Küp patlatıldığında parçaları aşağı dökülür, gölge ise panoda hep sabit kalır.", "Tamam");
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
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogWarning($"[PixelGame] {PrefabPath} bulunamadı!");
                return;
            }

            // Material bul
            Material shadowMat = AssetDatabase.LoadAssetAtPath<Material>(ShadowMatPath);
            if (shadowMat == null)
            {
                string[] guids = AssetDatabase.FindAssets("SoftVoxelShadow_Mat t:Material");
                if (guids.Length > 0)
                {
                    shadowMat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) return;

            try
            {
                // 1. PixelCube bileşeni kontrolü
                PixelCube pixelCube = root.GetComponent<PixelCube>();
                if (pixelCube == null)
                {
                    pixelCube = root.AddComponent<PixelCube>();
                }
                pixelCube.KeepShadowPermanent = false;

                // 2. CubeShadow (Arka Pano 360 derece çevreleyen gölge)
                Transform shadowTrans = root.transform.Find("CubeShadow");
                GameObject shadowObj = shadowTrans != null ? shadowTrans.gameObject : CreateShadowQuad("CubeShadow", root.transform);

                // Gerçekçi, soft ve doğal sönümlü gölge
                shadowObj.transform.localPosition = new Vector3(0f, -0.04f, 0.52f);
                shadowObj.transform.localRotation = Quaternion.identity;
                shadowObj.transform.localScale = new Vector3(1.22f, 1.22f, 1f);

                MeshRenderer mr = shadowObj.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    if (shadowMat != null) mr.sharedMaterial = shadowMat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                }

                // 3. CubeShadow_Bottom (3D Perspektif Zemin Gölgesi)
                Transform bottomTrans = root.transform.Find("CubeShadow_Bottom");
                GameObject bottomObj = bottomTrans != null ? bottomTrans.gameObject : CreateShadowQuad("CubeShadow_Bottom", root.transform);

                bottomObj.transform.localPosition = new Vector3(0f, -0.505f, 0f);
                bottomObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                bottomObj.transform.localScale = new Vector3(1.22f, 1.22f, 1f);

                MeshRenderer bMr = bottomObj.GetComponent<MeshRenderer>();
                if (bMr != null)
                {
                    if (shadowMat != null) bMr.sharedMaterial = shadowMat;
                    bMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    bMr.receiveShadows = false;
                }

                pixelCube.SetShadowObjects(shadowObj, bottomObj);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("<color=#00FFAA><b>[PixelGame]</b></color> MainCube prefab'ına her yönde (360 derece + zemin) Fake Shadow kaydedildi!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void ApplyShadowsToSceneCubes()
        {
            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen != null)
            {
                gen.GeneratePixelArt();
                gen.ApplyShadowsToAllExistingCubes();
            }

            PixelCube[] allCubes = Object.FindObjectsByType<PixelCube>(FindObjectsSortMode.None);
            foreach (var c in allCubes)
            {
                c.KeepShadowPermanent = false;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            SceneView.RepaintAll();
        }
    }
}
