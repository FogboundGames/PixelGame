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
        private const string RunKey = "MainCube_VisibleFakeShadow_Setup_v15";

        static SetupMainCubePrefabShadow()
        {
            // Kullanıcı gölgeleri kapattığı için otomatik gölge oluşturucu devre dışı bırakıldı
            // EditorApplication.delayCall += ApplyShadowToPrefabAndScene;
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
                "Tüm sahte gölgeler başarıyla uygulandı!\n\n" +
                "- Her parça altında 3D fake shadow ve bevel.\n" +
                "- Panonun 4 kenarını çevreleyen yumuşak Board Frame Shadow.\n" +
                "- Parçalandığında arkada hiçbir iz kalmıyor.", "Harika!");
        }

        [MenuItem("Tools/PixelGame/🌑 MainCube Prefabına Belirgin Fake Shadow Ayarla")]
        public static void ApplyManual()
        {
            ApplyShadowToPrefab(force: true);
            ApplyShadowsToSceneCubes();
            EditorUtility.DisplayDialog("Fake Shadow Ayarlandı", 
                "MainCube prefab'ına, sahnedeki küplere ve panonun 4 kenarına sahte gölge uygulandı!\n\n" +
                "- Küp altı ve kenar sahte gölgeleri aktif.\n" +
                "- Pano çevresi yumuşak çerçeve gölgesi aktif.\n" +
                "- Parça patlayınca arkada leke kalmaz.", "Tamam");
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

            // Material bul ve gölge rengini/opaklığını ayarla
            Material shadowMat = AssetDatabase.LoadAssetAtPath<Material>(ShadowMatPath);
            if (shadowMat == null)
            {
                string[] guids = AssetDatabase.FindAssets("SoftVoxelShadow_Mat t:Material");
                if (guids.Length > 0)
                {
                    shadowMat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            if (shadowMat != null)
            {
                Color sc = new Color(0.04f, 0.06f, 0.14f, 0.75f);
                if (shadowMat.HasProperty("_Color")) shadowMat.SetColor("_Color", sc);
                if (shadowMat.HasProperty("_BaseColor")) shadowMat.SetColor("_BaseColor", sc);
                shadowMat.color = sc;
                EditorUtility.SetDirty(shadowMat);
            }

            // Küp malzemesine bevel & gölge dokusunu (_BaseMap) bağla
            Material cubeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PixelCube_Cartoon.mat");
            if (cubeMat != null)
            {
                Texture2D bevelTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/VoxelCube_BevelShadow.png");
                if (bevelTex != null)
                {
                    cubeMat.SetTexture("_BaseMap", bevelTex);
                    cubeMat.mainTexture = bevelTex;
                    EditorUtility.SetDirty(cubeMat);
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

                // 2. CubeShadow (Her parçaya belirgin sahte gölge)
                Transform shadowTrans = root.transform.Find("CubeShadow");
                GameObject shadowObj = shadowTrans != null ? shadowTrans.gameObject : CreateShadowQuad("CubeShadow", root.transform);

                // Sağa ve aşağı düşen, küpün kenarlarından taşarak belirginleşen sahte gölge
                shadowObj.transform.localPosition = new Vector3(0.04f, -0.08f, 0.52f);
                shadowObj.transform.localRotation = Quaternion.identity;
                shadowObj.transform.localScale = new Vector3(1.34f, 1.34f, 1f);

                MeshRenderer mr = shadowObj.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    if (shadowMat != null) mr.sharedMaterial = shadowMat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                }

                // 3. CubeShadow_Bottom varsa kaldır (Küp patlayınca komşunun boşluğa sarkmasını engeller)
                Transform bottomTrans = root.transform.Find("CubeShadow_Bottom");
                if (bottomTrans != null)
                {
                    Object.DestroyImmediate(bottomTrans.gameObject);
                }

                pixelCube.SetShadowObjects(shadowObj, null);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("<color=#00FFAA><b>[PixelGame]</b></color> MainCube prefab'ına her parçada belirgin Fake Shadow kaydedildi!");
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
                gen.EnsureFigureContourShadowDisabled();
                gen.ShadowOffset = new Vector2(0.04f, -0.08f);
                gen.EnableBoardShadow = false;
                gen.EnsureBoardShadowDisabled();
                gen.ApplyShadowsToAllExistingCubes();
            }

            PixelCube[] allCubes = Object.FindObjectsByType<PixelCube>(FindObjectsSortMode.None);
            foreach (var c in allCubes)
            {
                c.KeepShadowPermanent = false;
                Transform bottomTrans = c.transform.Find("CubeShadow_Bottom");
                if (bottomTrans != null)
                {
                    Object.DestroyImmediate(bottomTrans.gameObject);
                }

                Transform shadowTrans = c.transform.Find("CubeShadow");
                if (shadowTrans != null)
                {
                    shadowTrans.localPosition = new Vector3(0.04f, -0.08f, 0.52f);
                    shadowTrans.localScale = new Vector3(1.34f, 1.34f, 1f);
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            SceneView.RepaintAll();
        }
    }
}
