using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class SetupTileBadgeOverhaul
    {
        static SetupTileBadgeOverhaul()
        {
            EditorApplication.delayCall += ApplyOverhaul;
        }

        private const string CountPath = "Assets/UI/Count.png";
        private const string CountTintablePath = "Assets/UI/Count_Tintable.png";
        private const string CountInnerPlatePath = "Assets/UI/Count_InnerPlate.png";
        private const string CountFullPlatePath = "Assets/UI/Count_FullPlate.png";
        private const string JuicyPillPath = "Assets/UI/Badge_JuicyPill.png";
        private const string MiniPillPath = "Assets/UI/Badge_MiniPill.png";
        private const string MiniPillTintablePath = "Assets/UI/Badge_MiniPill_Tintable.png";
        private const string BlueBotWithNeckPrefabPath = "Assets/Prefabs/BlueBotWithNeckWagon.prefab";
        private const string BlueBotPrefabPath = "Assets/Prefabs/BlueBotWagon.prefab";
        private const string VacuumCannonPrefabPath = "Assets/Prefabs/VacuumCannon.prefab";

        [MenuItem("Tools/PixelGame/✨ 3B Tombul Rozet & Model Gizleme Ayarını Uygula (Tile Badge Overhaul)", priority = 2)]
        public static void ApplyOverhaul()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;

            // 1. Sprite Ayarlarını Yapılandır
            ConfigureSprite(CountPath);
            ConfigureSprite(CountTintablePath);
            ConfigureSprite(CountInnerPlatePath);
            ConfigureSprite(CountFullPlatePath);
            ConfigureSprite(JuicyPillPath);
            ConfigureSprite(MiniPillPath);
            ConfigureSprite(MiniPillTintablePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Sprite fullPlateSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CountFullPlatePath);
            if (fullPlateSprite == null) fullPlateSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CountPath);
            Sprite innerPlateSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CountInnerPlatePath);
            Sprite pillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(JuicyPillPath);
            if (pillSprite == null) pillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MiniPillPath);

            // 2. Prefabları Yapılandır
            ConfigurePrefab(BlueBotWithNeckPrefabPath, fullPlateSprite, innerPlateSprite, pillSprite);
            ConfigurePrefab(BlueBotPrefabPath, fullPlateSprite, innerPlateSprite, pillSprite);
            ConfigurePrefab(VacuumCannonPrefabPath, fullPlateSprite, innerPlateSprite, pillSprite);

            // 3. Sahnedeki Vagonları Güncelle
            foreach (WagonCapacityBadge badge in Object.FindObjectsByType<WagonCapacityBadge>(FindObjectsSortMode.None))
            {
                if (badge == null) continue;
                bool inPool = badge.transform.parent != null && badge.GetComponentInParent<TruckPool>() != null;
                SerializedObject so = new SerializedObject(badge);
                so.FindProperty("m_HideModel").boolValue = inPool;
                so.FindProperty("m_ShowBackgroundBox").boolValue = true;
                so.FindProperty("m_ActiveShowMiniPill").boolValue = true;
                so.FindProperty("m_ActiveHeadElevation").floatValue = 0.12f;
                so.FindProperty("m_ActiveWorldWidth").floatValue = 0.76f;
                so.FindProperty("m_ManualOffset").vector3Value = Vector3.zero;
                so.FindProperty("m_PoolTargetWorldScale").floatValue = 0.0070f;
                so.FindProperty("m_ManualTiltDegrees").floatValue = 0f;

                SerializedProperty bgProp = so.FindProperty("m_BackgroundSprite");
                if (bgProp != null && fullPlateSprite != null) bgProp.objectReferenceValue = fullPlateSprite;

                SerializedProperty innerProp = so.FindProperty("m_InnerPlateSprite");
                if (innerProp != null && innerPlateSprite != null) innerProp.objectReferenceValue = innerPlateSprite;

                SerializedProperty miniProp = so.FindProperty("m_MiniPillSprite");
                if (miniProp != null && pillSprite != null) miniProp.objectReferenceValue = pillSprite;

                so.ApplyModifiedProperties();

                badge.SetPoolMode(inPool);
                badge.EnsureBadgeUI();
                badge.ApplyStyle();
                badge.UpdatePlacement();
                EditorUtility.SetDirty(badge);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();

            Debug.Log("<color=#00FFAA><b>[TileBadgeOverhaul]</b></color> 3B tombul rozetler, büyük parlak kapsül rozetler ve model görünürlükleri başarıyla güncellendi!");
        }

        private static void ConfigureSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        private static void ConfigurePrefab(string prefabPath, Sprite tintableSprite, Sprite innerPlateSprite, Sprite miniPillSprite)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null) return;

            try
            {
                Transform modelChild = prefabRoot.transform.Find("Model");
                if (modelChild != null)
                {
                    modelChild.gameObject.SetActive(true);
                }
                foreach (Renderer r in prefabRoot.GetComponentsInChildren<Renderer>(true))
                {
                    if (r != null) r.enabled = true;
                }

                WagonCapacityBadge badge = prefabRoot.GetComponent<WagonCapacityBadge>();
                if (badge == null) badge = prefabRoot.AddComponent<WagonCapacityBadge>();

                SerializedObject so = new SerializedObject(badge);
                so.FindProperty("m_HideModel").boolValue = false;
                so.FindProperty("m_ShowBackgroundBox").boolValue = true;
                so.FindProperty("m_ActiveShowMiniPill").boolValue = true;
                so.FindProperty("m_ActiveHeadElevation").floatValue = 0.12f;
                so.FindProperty("m_ActiveWorldWidth").floatValue = 0.76f;
                so.FindProperty("m_ManualOffset").vector3Value = Vector3.zero;
                so.FindProperty("m_PoolTargetWorldScale").floatValue = 0.0070f;
                so.FindProperty("m_ManualTiltDegrees").floatValue = 0f;

                if (tintableSprite != null)
                {
                    SerializedProperty bgProp = so.FindProperty("m_BackgroundSprite");
                    if (bgProp != null) bgProp.objectReferenceValue = tintableSprite;
                }

                if (innerPlateSprite != null)
                {
                    SerializedProperty innerProp = so.FindProperty("m_InnerPlateSprite");
                    if (innerProp != null) innerProp.objectReferenceValue = innerPlateSprite;
                }

                if (miniPillSprite != null)
                {
                    SerializedProperty miniProp = so.FindProperty("m_MiniPillSprite");
                    if (miniProp != null) miniProp.objectReferenceValue = miniPillSprite;
                }

                so.ApplyModifiedProperties();

                BoxCollider col = prefabRoot.GetComponent<BoxCollider>();
                if (col != null)
                {
                    col.center = Vector3.zero;
                    col.size = new Vector3(1.2f, 1.2f, 0.4f);
                    col.isTrigger = true;
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }

    [InitializeOnLoad]
    public static class SetupTileBadgeOverhaulRunner
    {
        private const string RunKey = "TileBadgeOverhaul_Executed_v6";

        static SetupTileBadgeOverhaulRunner()
        {
            EditorApplication.delayCall += () =>
            {
                try
                {
                    if (SessionState.GetBool(RunKey, false)) return;
                    SessionState.SetBool(RunKey, true);

                    SetupTileBadgeOverhaul.ApplyOverhaul();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[TileBadgeOverhaul] Runner hatasız devam etti: " + ex.Message);
                }
            };
        }
    }
}

