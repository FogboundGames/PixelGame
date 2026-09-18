using System.IO;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class TrackSystemSetup
    {
        private const string TexturePath = "Assets/Textures/Track_Chevron.png";
        private const string MatRimPath = "Assets/Materials/Track_Rim.mat";
        private const string MatChannelPath = "Assets/Materials/Track_Channel.mat";

        private const string FbxStraightPath = "Assets/Models/Track/Track_Straight.fbx";
        private const string FbxCornerPath = "Assets/Models/Track/Track_Corner.fbx";

        private const string PrefabStraightPath = "Assets/Prefabs/Track_Straight.prefab";
        private const string PrefabCornerPath = "Assets/Prefabs/Track_Corner.prefab";

        static TrackSystemSetup()
        {
            EditorApplication.delayCall += RunSetupIfMissing;
        }

        private static void RunSetupIfMissing()
        {
            if (!File.Exists(MatRimPath) || !File.Exists(MatChannelPath) ||
                !File.Exists(PrefabStraightPath) || !File.Exists(PrefabCornerPath))
            {
                ExecuteSetup(silent: true);
            }
        }

        [MenuItem("Tools/PixelGame/🛤️ Yeni Ray Sistemini Kur (Materyaller & Prefablar)", priority = 36)]
        public static void SetupManual()
        {
            ExecuteSetup(silent: false);
        }

        public static void ExecuteSetup(bool silent)
        {
            // 1. Doku (Texture) Wrap Mode ayarını kontrol et ve gerekirse Repeat yap
            EnsureTextureWrapMode(TexturePath);

            // 2. Materyalleri oluştur veya güncelle
            Material rimMat = EnsureRimMaterial();
            Material channelMat = EnsureChannelMaterial();

            // 2b. FBX modellerinin materyal remap ayarlarını yap (FBX doğrudan sahneye atılsa bile doğru materyalleri alsın)
            RemapFbxMaterials(FbxStraightPath, rimMat, channelMat);
            RemapFbxMaterials(FbxCornerPath, rimMat, channelMat);

            // 3. Prefabları oluştur
            bool straightCreated = CreateOrUpdateTrackPrefab(FbxStraightPath, PrefabStraightPath, rimMat, channelMat);
            bool cornerCreated = CreateOrUpdateTrackPrefab(FbxCornerPath, PrefabCornerPath, rimMat, channelMat);

            // 4. Sahneye TrackFlow yöneticisi ekle / güncelle
            EnsureSceneTrackFlow(channelMat);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!silent)
            {
                EditorUtility.DisplayDialog(
                    "Yeni Ray Sistemi Hazır",
                    "Aşağıdaki bileşenler başarıyla kuruldu:\n\n" +
                    $"• Rim Materyali: {MatRimPath} (#F0F8FE)\n" +
                    $"• Channel Materyali: {MatChannelPath} (Chevron dokusu, Tiling 4x1)\n" +
                    $"• Straight Prefab: {PrefabStraightPath}\n" +
                    $"• Corner Prefab: {PrefabCornerPath}\n" +
                    $"• TrackFlow Yöneticisi sahneye bağlandı.",
                    "Tamam"
                );
            }

            Debug.Log("<color=#00FFAA><b>[TrackSystemSetup]</b></color> Ray materyalleri, prefabları ve TrackFlow başarıyla yapılandırıldı!");
        }

        private static void EnsureTextureWrapMode(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                bool dirty = false;
                if (importer.wrapMode != TextureWrapMode.Repeat)
                {
                    importer.wrapMode = TextureWrapMode.Repeat;
                    dirty = true;
                }
                if (dirty)
                {
                    importer.SaveAndReimport();
                }
            }
        }

        private static Material EnsureRimMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatRimPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(shader);
                mat.name = "Track_Rim";
                AssetDatabase.CreateAsset(mat, MatRimPath);
            }
            else if (mat.shader != shader && shader != null)
            {
                mat.shader = shader;
            }

            // Renk: #F0F8FE (240, 248, 254)
            Color rimColor = new Color(240f / 255f, 248f / 255f, 254f / 255f, 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", rimColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", rimColor);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.5f);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material EnsureChannelMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatChannelPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(shader);
                mat.name = "Track_Channel";
                AssetDatabase.CreateAsset(mat, MatChannelPath);
            }
            else if (mat.shader != shader && shader != null)
            {
                mat.shader = shader;
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }

            // Tiling = (4, 1), Offset = (0, 0)
            Vector2 tiling = new Vector2(4f, 1f);
            Vector2 offset = Vector2.zero;
            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTextureScale("_BaseMap", tiling);
                mat.SetTextureOffset("_BaseMap", offset);
            }
            if (mat.HasProperty("_MainTex"))
            {
                mat.SetTextureScale("_MainTex", tiling);
                mat.SetTextureOffset("_MainTex", offset);
            }

            // Base Color = Beyaz
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void RemapFbxMaterials(string fbxPath, Material rimMat, Material channelMat)
        {
            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return;

            var idRim = new AssetImporter.SourceAssetIdentifier(typeof(Material), "Track_SideLight");
            var idChannel = new AssetImporter.SourceAssetIdentifier(typeof(Material), "Track_TopDark");

            importer.AddRemap(idRim, rimMat);
            importer.AddRemap(idChannel, channelMat);
            importer.SaveAndReimport();
        }

        private static bool CreateOrUpdateTrackPrefab(string fbxPath, string prefabPath, Material rimMat, Material channelMat)
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null)
            {
                Debug.LogWarning($"[TrackSystemSetup] FBX bulunamadı: {fbxPath}");
                return false;
            }

            // Geçici nesne oluştur
            GameObject tempInstance = Object.Instantiate(fbx);
            tempInstance.name = Path.GetFileNameWithoutExtension(prefabPath);

            // MeshRenderer'ları bul ve materyal slotlarını ayarla
            // Slot 0: Rim, Slot 1: Channel
            MeshRenderer[] renderers = tempInstance.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer r in renderers)
            {
                Material[] mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0)
                {
                    mats = new Material[2];
                }
                else if (mats.Length == 1)
                {
                    Material m0 = mats[0];
                    mats = new Material[2];
                    mats[0] = m0;
                }

                mats[0] = rimMat;
                if (mats.Length > 1)
                {
                    mats[1] = channelMat;
                }

                r.sharedMaterials = mats;
            }

            // Prefab olarak kaydet
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempInstance, prefabPath);
            Object.DestroyImmediate(tempInstance);

            return savedPrefab != null;
        }

        private static void EnsureSceneTrackFlow(Material channelMat)
        {
            TrackFlow flow = Object.FindFirstObjectByType<TrackFlow>();
            if (flow == null)
            {
                GameObject flowObj = GameObject.Find("[TrackFlowManager]");
                if (flowObj == null)
                {
                    flowObj = new GameObject("[TrackFlowManager]");
                    #if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        Undo.RegisterCreatedObjectUndo(flowObj, "Create TrackFlowManager");
                    }
                    #endif
                }
                flow = flowObj.GetComponent<TrackFlow>() ?? flowObj.AddComponent<TrackFlow>();
            }

            if (flow != null)
            {
                flow.trackMaterial = channelMat;
                flow.speed = 0.90f;
                flow.reverse = true;
                EditorUtility.SetDirty(flow);
            }
        }

        [MenuItem("Tools/PixelGame/🛤️ Sahneye Rayları Otomatik Döşe", priority = 37)]
        public static void BuildSceneRails()
        {
            ExecuteSetup(silent: true);

            TruckDispatcher td = Object.FindFirstObjectByType<TruckDispatcher>();
            if (td == null)
            {
                Debug.LogError("[TrackSystemSetup] Sahnede TruckDispatcher bulunamadı!");
                return;
            }

            td.SetupPerimeterLoop();
            var loop = td.TrackLoop;

            GameObject straightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabStraightPath);
            GameObject cornerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabCornerPath);
            if (straightPrefab == null || cornerPrefab == null)
            {
                Debug.LogError("[TrackSystemSetup] Ray prefabları bulunamadı!");
                return;
            }

            Transform wagonsRoot = td.WagonsRoot;
            if (wagonsRoot == null)
            {
                GameObject rootObj = GameObject.Find("[PerimeterWagonsRoot]");
                if (rootObj == null) rootObj = new GameObject("[PerimeterWagonsRoot]");
                wagonsRoot = rootObj.transform;
            }

            Transform existing = wagonsRoot.Find("PerimeterRails");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            GameObject railsGroup = new GameObject("PerimeterRails");
            Undo.RegisterCreatedObjectUndo(railsGroup, "Create Perimeter Rails");
            railsGroup.transform.SetParent(wagonsRoot, false);

            float s = 0.60f;
            float r = 0.5f * s;
            Quaternion baseRot = Quaternion.Euler(-90f, 0f, 0f);

            float leftX = loop.LeftX;
            float rightX = loop.RightX;
            float bottomY = loop.BottomY;
            float topY = loop.TopY;
            float z = loop.Z;

            // 4 Köşe (Track_Corner)
            SpawnCorner(railsGroup.transform, cornerPrefab, new Vector3(rightX, bottomY, z), Quaternion.AngleAxis(180f, Vector3.forward) * baseRot, s, "Corner_BR");
            SpawnCorner(railsGroup.transform, cornerPrefab, new Vector3(rightX, topY, z), Quaternion.AngleAxis(270f, Vector3.forward) * baseRot, s, "Corner_TR");
            SpawnCorner(railsGroup.transform, cornerPrefab, new Vector3(leftX, topY, z), Quaternion.AngleAxis(0f, Vector3.forward) * baseRot, s, "Corner_TL");
            SpawnCorner(railsGroup.transform, cornerPrefab, new Vector3(leftX, bottomY, z), Quaternion.AngleAxis(90f, Vector3.forward) * baseRot, s, "Corner_BL");

            // 4 Düz Kenar (Track_Straight: model ok yönü -X olduğu için CCW döngüde)
            SpawnEdge(railsGroup.transform, straightPrefab, new Vector3(leftX + r, bottomY, z), new Vector3(rightX - r, bottomY, z), Vector3.right, Quaternion.AngleAxis(180f, Vector3.forward) * baseRot, s, "Straight_Bottom");
            SpawnEdge(railsGroup.transform, straightPrefab, new Vector3(rightX, bottomY + r, z), new Vector3(rightX, topY - r, z), Vector3.up, Quaternion.AngleAxis(270f, Vector3.forward) * baseRot, s, "Straight_Right");
            SpawnEdge(railsGroup.transform, straightPrefab, new Vector3(rightX - r, topY, z), new Vector3(leftX + r, topY, z), Vector3.left, Quaternion.AngleAxis(0f, Vector3.forward) * baseRot, s, "Straight_Top");
            SpawnEdge(railsGroup.transform, straightPrefab, new Vector3(leftX, topY - r, z), new Vector3(leftX, bottomY + r, z), Vector3.down, Quaternion.AngleAxis(90f, Vector3.forward) * baseRot, s, "Straight_Left");

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

            Debug.Log($"<color=#00FFAA><b>[TrackSystemSetup]</b></color> Sahneye {railsGroup.transform.childCount} adet modüler ray başarıyla döşendi!");
        }

        private static void SpawnCorner(Transform parent, GameObject prefab, Vector3 pos, Quaternion rot, float scale, string name)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = Vector3.one * scale;
        }

        private static void SpawnEdge(Transform parent, GameObject prefab, Vector3 start, Vector3 end, Vector3 dir, Quaternion rot, float scale, string prefix)
        {
            float totalLen = Vector3.Distance(start, end);
            if (totalLen <= 0.01f) return;

            int count = Mathf.Max(1, Mathf.RoundToInt(totalLen / scale));
            float pieceLen = totalLen / count;
            Vector3 pieceScale = new Vector3(pieceLen, scale, scale);

            for (int i = 0; i < count; i++)
            {
                Vector3 center = start + dir * ((i + 0.5f) * pieceLen);
                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.name = $"{prefix}_{i}";
                go.transform.position = center;
                go.transform.rotation = rot;
                go.transform.localScale = pieceScale;
            }
        }
    }
}
