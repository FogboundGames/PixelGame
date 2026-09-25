using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class GemiSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Gemi.unity";
        private const string BgImagePath = "Assets/Kenney/BeachBackground_Clean.png";
        private const string FrameImagePath = "Assets/Kenney/ChatGPT Image 22 Eyl 2026 18_04_01.png";
        private const string ShadowShaderName = "Custom/URP_ShadowCatcher";
        private const string ShadowMatPath = "Assets/Materials/Gemi_ShadowCatcher_Mat.mat";
        private const string IndicatorMatPath = "Assets/Materials/Indicator_Slot_Mat.mat";
        private const string MainCubePrefabPath = "Assets/Prefabs/MainCube.prefab";
        private const string IndicatorModelPath = "Assets/Kenney/kenney_prototype-kit/Models/FBX format/indicator-square-b.fbx";
        private const string IndicatorTexturePath = "Assets/Kenney/kenney_prototype-kit/Models/Textures/variation-a.png";
        private const string ShipCargoModelPath = "Assets/Kenney/kenney_watercraft-pack/Models/FBX format/ship-cargo-a.fbx";
        private const string ShipTexturePath = "Assets/Kenney/kenney_watercraft-pack/Models/FBX format/Textures/colormap.png";
        private const string ShipMatPath = "Assets/Materials/Ship_Watercraft_Mat.mat";
        private const string PierModelPath = "Assets/Kenney/kenney_watercraft-pack/Models/FBX format/ramp-wide.fbx";
        private const string PierPrefabPath = "Assets/Prefabs/Pier_Dock.prefab";
        private const string ScreenshotPath = "scratch/gemi_gameplay_view.png";
        private const string AutoRunKey = "GemiSceneSetup_AutoRun_v29";

        // Kullanıcının sahnede elle ayarladığı referans değerler (Kalıcı / Sabit Referans):
        public static readonly Vector2 UserOtCercevePosition = new Vector2(3.33f, 558.9f);
        public static readonly Vector2 UserOtCerceveSize = new Vector2(796.57f, 927.8f);
        public static readonly Vector3 UserGeneratorPosition = new Vector3(-0.041753f, 0.17511f, 0f);
        public static readonly Vector3 UserGeneratorScale = new Vector3(1.3072706f, 1.4263f, 1f);
        private static readonly Vector3 SandFrameCenterWorld = new Vector3(0f, 4.17f, 0f);

        private const float SandFrameCanvasSize = 530.0f;

        static GemiSceneSetup()
        {
            // Kullanıcının sahnede elle yaptığı düzenlemelerin %100 sabit kalması için otomatik yeniden kurulum kapalıdır.
        }

        // [MenuItem("Tools/PixelGame/🏝️ Gemi Sahnesini Kur & Tüm Ögeleri Getir", priority = 10)]
        public static void SetupGemiSceneMenu()
        {
            Setup(includePixelArt: true, includeWaterSlots: true);
            CaptureScreenshot();
            EditorUtility.DisplayDialog("Gemi Sahnesi Hazır! 🏝️⚓",
                "Sahne tüm ögeleriyle başarıyla yapılandırıldı!\n\n" +
                "• Kum alanında taş çerçevenin ortasına 3D Piksel Görseli yerleştirildi.\n" +
                "• Su alanına 5 adet 'indicator-square-b' slotu yan yana dizildi.\n" +
                "• LevelManager ve PixelCubeInteraction tam aktif.\n" +
                "• URP yumuşak gölgeler zemin üzerinde canlı olarak çalışıyor.", "Harika!");
        }

        // [MenuItem("Tools/PixelGame/🎨 Kum Alanındaki Piksel Resmi Yenile (Regenerate)", priority = 11)]
        public static void RegeneratePixelArtMenu()
        {
            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen != null)
            {
                gen.GeneratePixelArt();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                CaptureScreenshot();
                Debug.Log("<color=#00FFAA><b>[GemiSceneSetup]</b></color> Piksel sanatı başarıyla yenilendi!");
            }
            else
            {
                Setup(includePixelArt: true, includeWaterSlots: true);
            }
        }

        // [MenuItem("Tools/PixelGame/➡️ Sonraki Seviyeyi Yükle (Next Level)", priority = 12)]
        public static void NextLevelMenu()
        {
            LevelManager lm = Object.FindFirstObjectByType<LevelManager>();
            if (lm != null)
            {
                lm.NextLevel();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                CaptureScreenshot();
            }
        }

        // [MenuItem("Tools/PixelGame/⬅️ Önceki Seviyeyi Yükle (Prev Level)", priority = 13)]
        public static void PrevLevelMenu()
        {
            LevelManager lm = Object.FindFirstObjectByType<LevelManager>();
            if (lm != null)
            {
                lm.PreviousLevel();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                CaptureScreenshot();
            }
        }

        // [MenuItem("Tools/PixelGame/📸 Gemi Sahnesi Ekran Görüntüsü Al", priority = 14)]
        public static void CaptureScreenshotMenu()
        {
            CaptureScreenshot();
            EditorUtility.DisplayDialog("Ekran Görüntüsü Alındı",
                $"Ekran görüntüsü kaydedildi:\n{ScreenshotPath}", "Tamam");
        }

        public static void Setup(bool includePixelArt = true, bool includeWaterSlots = true)
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath);
            }

            Undo.SetCurrentGroupName("Setup Gemi Scene with Pixel Art and Water Slots");
            int undoGroup = Undo.GetCurrentGroup();

            // 1. Kamera Düzeni
            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                cam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
                camObj.AddComponent<AudioListener>();
            }

            cam.transform.position = new Vector3(0f, 0f, -12f);
            cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.08f, 0.16f, 1f);
            cam.orthographic = true;
            cam.orthographicSize = 8.0f; // 9:16 portrait ekranda dikey 16, yatay 9 birim
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;

            // URP ek bileşenleri
            var camData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (camData == null)
            {
                camData = cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            }
            if (camData != null)
            {
                camData.renderShadows = true;
            }

            // 2. Işıklandırma (Directional Light) - Doğal sıcak güneş açısı
            Light dirLight = Object.FindFirstObjectByType<Light>();
            if (dirLight == null || dirLight.type != LightType.Directional)
            {
                GameObject lightObj = new GameObject("Directional Light");
                dirLight = lightObj.AddComponent<Light>();
                dirLight.type = LightType.Directional;
            }

            dirLight.transform.SetParent(null, true);
            dirLight.transform.position = new Vector3(-3f, 8f, -6f);
            dirLight.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            dirLight.color = new Color(1.0f, 0.95f, 0.88f, 1f);
            dirLight.intensity = 1.25f;
            dirLight.shadows = LightShadows.Soft;
            dirLight.shadowStrength = 0.72f;

            // 3. Arka Plan Canvas (Screen Space - Camera)
            Texture2D bgTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BgImagePath);
            if (bgTex == null)
            {
                Debug.LogError($"[GemiSceneSetup] Arka plan görseli bulunamadı: {BgImagePath}");
            }

            GameObject canvasObj = GameObject.Find("Background_Canvas");
            if (canvasObj == null)
            {
                GameObject oldCanvas = GameObject.Find("Canvas");
                if (oldCanvas != null) canvasObj = oldCanvas;
                else canvasObj = new GameObject("Background_Canvas");
            }
            canvasObj.name = "Background_Canvas";

            Canvas canvas = canvasObj.GetComponent<Canvas>();
            if (canvas == null) canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 35f;
            canvas.sortingOrder = -100;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;

            if (canvasObj.GetComponent<GraphicRaycaster>() == null)
            {
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // BackgroundImage
            RawImage activeRawImg = canvasObj.GetComponentInChildren<RawImage>(true);
            if (activeRawImg == null)
            {
                GameObject rawObj = new GameObject("BackgroundImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                rawObj.transform.SetParent(canvasObj.transform, false);
                activeRawImg = rawObj.GetComponent<RawImage>();
            }
            activeRawImg.name = "BackgroundImage";
            if (bgTex != null)
            {
                activeRawImg.texture = bgTex;
            }
            activeRawImg.raycastTarget = false;

            // Hypercasual canlı su materyali ve kontrolcüsü ekle
            HypercasualWaterController waterCtrl = activeRawImg.GetComponent<HypercasualWaterController>();
            if (waterCtrl == null) waterCtrl = activeRawImg.gameObject.AddComponent<HypercasualWaterController>();
            waterCtrl.EnsureSetup();

            RectTransform rawRect = activeRawImg.rectTransform;
            rawRect.anchorMin = Vector2.zero;
            rawRect.anchorMax = Vector2.one;
            rawRect.offsetMin = Vector2.zero;
            rawRect.offsetMax = Vector2.zero;
            rawRect.localScale = Vector3.one;

            // Sahnede Serbestçe Genişletilebilir & Taşınabilir Çerçeve (OtCerceve / BoardFrame)
            Sprite frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FrameImagePath);
            Transform otTr = canvasObj.transform.Find("OtCerceve");
            Transform boardFrameTr = otTr != null ? otTr : canvasObj.transform.Find("BoardFrame");
            bool isNewFrame = false;
            if (boardFrameTr == null)
            {
                GameObject frameGo = new GameObject("OtCerceve", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                frameGo.transform.SetParent(canvasObj.transform, false);
                boardFrameTr = frameGo.transform;
                isNewFrame = true;
            }
            RectTransform boardFrameRect = boardFrameTr.GetComponent<RectTransform>();
            boardFrameRect.anchorMin = new Vector2(0.5f, 0.5f);
            boardFrameRect.anchorMax = new Vector2(0.5f, 0.5f);
            boardFrameRect.pivot = new Vector2(0.5f, 0.5f);
            if (isNewFrame)
            {
                boardFrameRect.anchoredPosition = UserOtCercevePosition;
                boardFrameRect.sizeDelta = UserOtCerceveSize;
                boardFrameRect.localScale = Vector3.one;
            }

            Image frameImg = boardFrameTr.GetComponent<Image>();
            if (frameImg != null)
            {
                if (frameImg.sprite == null) frameImg.sprite = frameSprite;
                frameImg.type = Image.Type.Simple;
                frameImg.raycastTarget = false;
                frameImg.color = Color.white;
            }

            // Çerçeve arkasına yumuşak kum sahte gölgesini (Fake Shadow) ekle
            PixelArtGenerator.EnsureOtCerceveShadow(boardFrameTr.gameObject);

            // MainPlane UI Kılavuzu (BoardFrame içine tam oturan piksel üretim hedefi)
            RectTransform targetFrameRect = boardFrameRect;
            Transform mainPlaneTr = boardFrameTr.Find("MainPlane");
            if (mainPlaneTr == null)
            {
                mainPlaneTr = canvasObj.transform.Find("MainPlane");
            }
            bool isNewMainPlane = false;
            if (mainPlaneTr == null)
            {
                GameObject planeGo = new GameObject("MainPlane", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                planeGo.transform.SetParent(boardFrameTr, false);
                mainPlaneTr = planeGo.transform;
                isNewMainPlane = true;
            }
            else if (mainPlaneTr.parent != boardFrameTr)
            {
                mainPlaneTr.SetParent(boardFrameTr, false);
            }

            targetFrameRect = mainPlaneTr.GetComponent<RectTransform>();
            targetFrameRect.anchorMin = new Vector2(0.5f, 0.5f);
            targetFrameRect.anchorMax = new Vector2(0.5f, 0.5f);
            targetFrameRect.pivot = new Vector2(0.5f, 0.5f);
            if (isNewMainPlane)
            {
                targetFrameRect.anchoredPosition = Vector2.zero; // BoardFrame'in tam ortası
                targetFrameRect.sizeDelta = new Vector2(SandFrameCanvasSize, SandFrameCanvasSize);
                targetFrameRect.localScale = Vector3.one;
            }

            Image planeImg = targetFrameRect.GetComponent<Image>();
            if (planeImg != null)
            {
                planeImg.enabled = false;
                planeImg.raycastTarget = false;
            }

            // 4. URP Şeffaf Gölge Yakalayıcı Zemin (Shadow Catcher)
            Material shadowMat = GetOrCreateShadowMaterial();
            GameObject shadowPlane = GameObject.Find("Ground_ShadowCatcher");
            if (shadowPlane == null)
            {
                shadowPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
                shadowPlane.name = "Ground_ShadowCatcher";
                Undo.RegisterCreatedObjectUndo(shadowPlane, "Create Ground Shadow Catcher");
            }

            Collider col = shadowPlane.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            shadowPlane.transform.position = new Vector3(0f, 0f, 0.05f);
            shadowPlane.transform.rotation = Quaternion.identity;
            shadowPlane.transform.localScale = new Vector3(14f, 24f, 1f);

            MeshRenderer shadowMr = shadowPlane.GetComponent<MeshRenderer>();
            if (shadowMr != null)
            {
                shadowMr.sharedMaterial = shadowMat;
                shadowMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                shadowMr.receiveShadows = true;
            }

            // 5. Eski Geçici Nesneleri Yedekle
            BackupLegacyObjects();

            // 6. Gameplay Bölgeleri ve Kılavuz Root
            GameObject gameplayRoot = GameObject.Find("[GAMEPLAY_MODELS]");
            if (gameplayRoot == null)
            {
                gameplayRoot = new GameObject("[GAMEPLAY_MODELS]");
                Undo.RegisterCreatedObjectUndo(gameplayRoot, "Create Gameplay Models Root");
            }
            gameplayRoot.transform.position = Vector3.zero;
            gameplayRoot.transform.rotation = Quaternion.identity;
            gameplayRoot.transform.localScale = Vector3.one;

            Transform zoneSand = EnsureZone(gameplayRoot.transform, "[Zone_Sand_PlayArea]", SandFrameCenterWorld);
            Transform zoneBridge = EnsureZone(gameplayRoot.transform, "[Zone_Wooden_Bridge]", new Vector3(0f, -0.58f, 0f));
            Transform zoneWater = EnsureZone(gameplayRoot.transform, "[Zone_Water_LowerArea]", new Vector3(0f, -4.80f, 0f));

            // 7. 3D Ahşap İskele (Kenney Prefab)
            SetupWoodenPier(zoneBridge);

            // Eski Demo_Showcase nesnesini temizle
            Transform oldDemo = gameplayRoot.transform.Find("Demo_Showcase");
            if (oldDemo != null) Undo.DestroyObjectImmediate(oldDemo.gameObject);
            Transform strayDemo = GameObject.Find("Demo_Showcase")?.transform;
            if (strayDemo != null) Undo.DestroyObjectImmediate(strayDemo.gameObject);

            // 8. Piksel Görsellerini (PixelArtGenerator & LevelManager) Kum Çerçevesinin Ortasına Yerleştir
            if (includePixelArt)
            {
                SetupPixelArtSystem(zoneSand, targetFrameRect, cam);
            }

            // 9. Su Bölgesine 5 Adet Çapraz Slot Yerleştir (indicator-square-b)
            if (includeWaterSlots)
            {
                SetupWaterSlots(zoneWater);
            }

            // Sahneyi kaydet
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("<color=#00FFAA><b>[GemiSceneSetup]</b></color> Gemi sahnesi 5 su slotu ve piksel görselleriyle başarıyla yapılandırıldı!");
        }

        private static void SetupPixelArtSystem(Transform sandZone, RectTransform targetFrameRect, Camera cam)
        {
            GameObject genObj = GameObject.Find("[PixelArtGenerator]");
            bool isNewGen = false;
            if (genObj == null)
            {
                Transform inZone = sandZone.Find("[PixelArtGenerator]");
                if (inZone != null) genObj = inZone.gameObject;
                else
                {
                    genObj = new GameObject("[PixelArtGenerator]");
                    Undo.RegisterCreatedObjectUndo(genObj, "Create PixelArtGenerator");
                    isNewGen = true;
                }
            }

            if (genObj.transform.parent != sandZone)
            {
                genObj.transform.SetParent(sandZone, true);
            }
            if (isNewGen)
            {
                genObj.transform.localPosition = UserGeneratorPosition;
                genObj.transform.localRotation = Quaternion.identity;
                genObj.transform.localScale = UserGeneratorScale;
            }

            // 1. PixelArtGenerator bileşeni
            PixelArtGenerator generator = genObj.GetComponent<PixelArtGenerator>();
            if (generator == null) generator = genObj.AddComponent<PixelArtGenerator>();

            GameObject cubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainCubePrefabPath);
            if (cubePrefab == null)
            {
                Debug.LogError($"[GemiSceneSetup] MainCube prefab'ı bulunamadı: {MainCubePrefabPath}");
            }

            PixelLevelData defaultLevel = AssetDatabase.LoadAssetAtPath<PixelLevelData>("Assets/Levels/Level_01_Raccoon.asset");
            Texture2D defaultTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/PixelArt_Raccoon.png");

            var so = new SerializedObject(generator);
            so.FindProperty("m_CubePrefab").objectReferenceValue = cubePrefab;
            so.FindProperty("m_SourceTexture").objectReferenceValue = defaultTex;
            so.FindProperty("m_ActiveLevelData").objectReferenceValue = defaultLevel;
            so.FindProperty("m_TargetFrameRect").objectReferenceValue = targetFrameRect;
            so.FindProperty("m_WorldCamera").objectReferenceValue = cam;
            so.FindProperty("m_UseNativeResolution").boolValue = true;
            so.FindProperty("m_InnerPadding").floatValue = 0.05f;
            so.FindProperty("m_CubeSpacing").floatValue = 0.02f;
            so.FindProperty("m_CubeDepth").floatValue = 0.75f;
            so.FindProperty("m_ColorBrightness").floatValue = 1.15f;
            so.FindProperty("m_ColorSaturation").floatValue = 1.25f;
            so.FindProperty("m_ColorContrast").floatValue = 1.05f;
            so.FindProperty("m_EmissionIntensity").floatValue = 0.25f;
            so.FindProperty("m_PreserveSceneEdits").boolValue = true;
            so.FindProperty("m_EnableCubeShadows").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 2. LevelManager bileşeni
            LevelManager levelManager = genObj.GetComponent<LevelManager>();
            if (levelManager == null) levelManager = genObj.AddComponent<LevelManager>();

            var lmSo = new SerializedObject(levelManager);
            var levelsProp = lmSo.FindProperty("m_Levels");
            levelsProp.ClearArray();

            string[] levelGuids = AssetDatabase.FindAssets("t:PixelLevelData", new[] { "Assets/Levels" });
            for (int i = 0; i < levelGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(levelGuids[i]);
                PixelLevelData levelData = AssetDatabase.LoadAssetAtPath<PixelLevelData>(path);
                if (levelData != null)
                {
                    levelsProp.InsertArrayElementAtIndex(levelsProp.arraySize);
                    levelsProp.GetArrayElementAtIndex(levelsProp.arraySize - 1).objectReferenceValue = levelData;
                }
            }

            lmSo.FindProperty("m_CurrentLevelIndex").intValue = 0;
            lmSo.FindProperty("m_Generator").objectReferenceValue = generator;
            lmSo.ApplyModifiedPropertiesWithoutUndo();

            // 3. PixelCubeInteraction bileşeni
            PixelCubeInteraction interaction = genObj.GetComponent<PixelCubeInteraction>();
            if (interaction == null) interaction = genObj.AddComponent<PixelCubeInteraction>();

            var interSo = new SerializedObject(interaction);
            interSo.FindProperty("m_WorldCamera").objectReferenceValue = cam;
            interSo.FindProperty("m_AllowDragPopping").boolValue = true;
            interSo.FindProperty("m_BlockOverUIButtonsOnly").boolValue = true;
            interSo.FindProperty("m_ShowResetButtonOnScreen").boolValue = false;
            interSo.ApplyModifiedPropertiesWithoutUndo();

            // 4. Piksel küpleri sahnede mevcut değilse üret; sahnede varsa kullanıcının elle ayarladığı küpleri %100 koru
            if (generator.CubesContainer == null || generator.CubesContainer.childCount == 0)
            {
                generator.GeneratePixelArt();
            }

            if (generator.CubesContainer != null)
            {
                var meshRenderers = generator.CubesContainer.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var mr in meshRenderers)
                {
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    mr.receiveShadows = true;
                }
            }
        }

        private const string PierWoodMatPath = "Assets/Materials/Pier_Wood_Mat.mat";
        private const string PierBoltMatPath = "Assets/Materials/Pier_Bolt_Mat.mat";

        private static void SetupWoodenPier(Transform bridgeZone)
        {
            // Eski iskele nesnesini temizle
            while (bridgeZone.childCount > 0)
            {
                Undo.DestroyObjectImmediate(bridgeZone.GetChild(0).gameObject);
            }

            GameObject pierModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PierModelPath);
            if (pierModelPrefab == null)
            {
                Debug.LogWarning($"[GemiSceneSetup] İskele modeli bulunamadı: {PierModelPath}");
                return;
            }

            GameObject pierInstance = (GameObject)PrefabUtility.InstantiatePrefab(pierModelPrefab, bridgeZone);
            pierInstance.name = "3D_Pier_Dock";
            // Kıyı çizgisine tam oturan 3D ahşap iskele
            // Kenney ramp-wide modeli: X ekseninde genişlik (5.14), Y ekseninde yükseklik (1.14), Z ekseninde derinlik (2.81)
            // Üst tahtaların tam net ve kalın görünmesi için -80 derece X rotasyonu (hafif 10° 3D perspektif)
            pierInstance.transform.localPosition = new Vector3(0f, 0f, 0.05f);
            pierInstance.transform.localRotation = Quaternion.Euler(-80f, 0f, 0f);
            pierInstance.transform.localScale = new Vector3(0.68f, 0.75f, 0.90f);

            Material woodMat = GetOrCreatePierWoodMaterial();
            var meshRenderers = pierInstance.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in meshRenderers)
            {
                if (woodMat != null)
                {
                    mr.sharedMaterial = woodMat;
                }
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;
            }

            // Referans görselindeki 4 adet tahtanın sol ve sağ kenarlarındaki gümüş cıvatalar (Rivets/Bolts)
            Transform boltsParent = pierInstance.transform.Find("Rivets_Bolts");
            if (boltsParent != null) Undo.DestroyObjectImmediate(boltsParent.gameObject);
            GameObject boltsGo = new GameObject("Rivets_Bolts");
            boltsGo.transform.SetParent(pierInstance.transform, false);
            Material boltMat = GetOrCreatePierBoltMaterial();

            float[] boltZ = new float[] { -0.75f, -0.22f, 0.30f, 0.85f };
            float boltY = 0.82f; // Tahta üst yüzeyi
            float boltX = 1.95f; // Tahta sol/sağ kenarları

            for (int b = 0; b < boltZ.Length; b++)
            {
                CreatePierBolt(boltsGo.transform, new Vector3(-boltX, boltY, boltZ[b]), boltMat);
                CreatePierBolt(boltsGo.transform, new Vector3(boltX, boltY, boltZ[b]), boltMat);
            }

            string prefabDir = Path.GetDirectoryName(PierPrefabPath);
            if (!Directory.Exists(prefabDir)) Directory.CreateDirectory(prefabDir);
            PrefabUtility.SaveAsPrefabAsset(pierInstance, PierPrefabPath);
        }

        private static void CreatePierBolt(Transform parent, Vector3 localPos, Material mat)
        {
            GameObject bolt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bolt.name = "Bolt";
            Collider col = bolt.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            bolt.transform.SetParent(parent, false);
            bolt.transform.localPosition = localPos;
            bolt.transform.localRotation = Quaternion.identity;
            bolt.transform.localScale = new Vector3(0.20f, 0.06f, 0.20f);

            MeshRenderer mr = bolt.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        private static void SetupWaterSlots(Transform waterZone)
        {
            // Eski tüm geçici dekorasyonları ve prefab instance'larını temizle
            for (int i = waterZone.childCount - 1; i >= 0; i--)
            {
                Transform child = waterZone.GetChild(i);
                if (child.name != "[WaterSlotsRow]" && child.name != "[ShipQueuePool]")
                {
                    GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(child.gameObject);
                    if (root != null)
                        Undo.DestroyObjectImmediate(root);
                    else
                        Undo.DestroyObjectImmediate(child.gameObject);
                }
            }

            // 1. ShipDispatcher Yöneticisi
            GameObject gameplayRoot = waterZone.parent != null ? waterZone.parent.gameObject : waterZone.gameObject;
            ShipDispatcher dispatcher = gameplayRoot.GetComponent<ShipDispatcher>();
            if (dispatcher == null) dispatcher = gameplayRoot.AddComponent<ShipDispatcher>();

            // 2. Su alanı altındaki Slotlar grubu
            Transform slotsGroup = waterZone.Find("[WaterSlotsRow]");
            if (slotsGroup == null)
            {
                GameObject slotsObj = new GameObject("[WaterSlotsRow]");
                Undo.RegisterCreatedObjectUndo(slotsObj, "Create WaterSlotsRow");
                slotsGroup = slotsObj.transform;
                slotsGroup.SetParent(waterZone, false);
            }

            slotsGroup.localPosition = new Vector3(0f, 0.45f, 0f);
            slotsGroup.localRotation = Quaternion.identity;
            slotsGroup.localScale = Vector3.one;

            // Indicator ve Gemi Modellerini yükle
            GameObject indicatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(IndicatorModelPath);
            GameObject shipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShipCargoModelPath);

            Material indicatorMat = GetOrCreateIndicatorMaterial();
            Material shipMat = GetOrCreateShipMaterial();

            // 5 adet slotu ÇAPRAZ marina düzeninde yerleştir
            const int slotCount = 5;
            // Kamera görüş alanı yarı genişliği (~3.70 birim) ile slot indicator'ının kendi
            // yarı-genişliğini (~0.85 birim) hesaba katarak en dıştaki slotlar ekran dışına
            // taşmasın diye 1.62'den 1.40'a düşürüldü (bkz. slotAngle=-28° ile birlikte ölçüldü).
            const float slotSpacing = 1.40f;
            float startX = -(slotCount - 1) * slotSpacing * 0.5f;
            const float slotAngle = -28f; // Çapraz marina yanaşma açısı

            for (int i = 0; i < slotCount; i++)
            {
                string slotName = $"WaterSlot_{i + 1}";
                Transform slotTr = slotsGroup.Find(slotName);
                if (slotTr == null)
                {
                    GameObject slotGo = new GameObject(slotName);
                    Undo.RegisterCreatedObjectUndo(slotGo, $"Create {slotName}");
                    slotTr = slotGo.transform;
                    slotTr.SetParent(slotsGroup, false);
                }

                float posX = startX + i * slotSpacing;
                // Kullanıcı isteği: Slotlar aynı yatay hizada (yan yana) dizilirken çapraz açısını (-28°) korur
                slotTr.localPosition = new Vector3(posX, 0f, 0f);
                // 3D su perspektif açısı (-68° X) üzerine su düzleminde lokal Y ekseninde slotAngle (-28°) açısı verilerek gemiler ve slotlar çapraz marina düzeninde yerleşir:
                slotTr.localRotation = Quaternion.Euler(-68f, 0f, 0f) * Quaternion.Euler(0f, slotAngle, 0f);
                slotTr.localScale = Vector3.one * 1.15f;

                ShipSlot shipSlot = slotTr.GetComponent<ShipSlot>();
                if (shipSlot == null) shipSlot = slotTr.gameObject.AddComponent<ShipSlot>();
                shipSlot.SlotIndex = i;
                shipSlot.ReleaseShip();

                // Eski model çocuklarını temizle
                while (slotTr.childCount > 0)
                {
                    Undo.DestroyObjectImmediate(slotTr.GetChild(0).gameObject);
                }

                // indicator-square-b taban modelini ekle (Çapraz slot çerçevesi)
                if (indicatorPrefab != null)
                {
                    GameObject indicatorInstance = (GameObject)PrefabUtility.InstantiatePrefab(indicatorPrefab, slotTr);
                    indicatorInstance.name = "IndicatorMesh";
                    indicatorInstance.transform.localPosition = Vector3.zero;
                    indicatorInstance.transform.localRotation = Quaternion.identity;
                    indicatorInstance.transform.localScale = new Vector3(1.0f, 1.0f, 1.25f);

                    var indRenderers = indicatorInstance.GetComponentsInChildren<MeshRenderer>(true);
                    foreach (var mr in indRenderers)
                    {
                        if (indicatorMat != null)
                        {
                            mr.sharedMaterial = indicatorMat;
                        }
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                        mr.receiveShadows = true;
                    }
                }

            }

            // 3. Su Alanı Bekleme Kuyruğu (Ship Queue Pool - Ferah su kanalı ve aralıklar)
            Transform queueObj = waterZone.Find("[ShipQueuePool]");
            if (queueObj == null)
            {
                GameObject qGo = new GameObject("[ShipQueuePool]");
                Undo.RegisterCreatedObjectUndo(qGo, "Create ShipQueuePool");
                queueObj = qGo.transform;
                queueObj.SetParent(waterZone, false);
            }

            queueObj.localPosition = new Vector3(0f, -1.45f, 0f);
            queueObj.localRotation = Quaternion.Euler(-68f, 0f, 0f);
            queueObj.localScale = Vector3.one * 1.35f;

            ShipQueuePool queuePool = queueObj.GetComponent<ShipQueuePool>();
            if (queuePool == null) queuePool = queueObj.gameObject.AddComponent<ShipQueuePool>();

            var qSo = new SerializedObject(queuePool);
            qSo.FindProperty("m_ShipPrefab").objectReferenceValue = shipPrefab;
            qSo.FindProperty("m_Columns").intValue = 4;
            qSo.FindProperty("m_Rows").intValue = 2;
            qSo.FindProperty("m_Spacing").vector2Value = new Vector2(1.28f, 1.22f);
            qSo.FindProperty("m_ShipScale").floatValue = 0.126f;
            qSo.ApplyModifiedPropertiesWithoutUndo();

            queuePool.EnsureSpots();

            // Edit Mode önizleme gemilerini oluştur
            if (shipPrefab != null && !Application.isPlaying)
            {
                queuePool.ClearQueue();
                for (int s = 0; s < queuePool.Capacity; s++)
                {
                    queuePool.SpawnShipAtSpot(s);
                }
            }

            dispatcher.EnsureReferences();
        }

        private static Material GetOrCreateShipMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(ShipMatPath);
            Shader toonShader = Shader.Find("Toony Colors Pro 2/PixelGame/Cartoon");
            if (toonShader == null) toonShader = Shader.Find("Universal Render Pipeline/Lit");

            if (mat == null)
            {
                mat = new Material(toonShader);
                mat.name = "Ship_Watercraft_Mat";

                string dir = Path.GetDirectoryName(ShipMatPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                AssetDatabase.CreateAsset(mat, ShipMatPath);
            }
            else if (mat.shader != toonShader && toonShader != null)
            {
                mat.shader = toonShader;
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ShipTexturePath);
            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
            }
            mat.SetColor("_BaseColor", Color.white);

            // Toony Colors Pro Şık Cel-Shading & Toy Plastic Ayarları
            if (mat.HasProperty("_HColor")) mat.SetColor("_HColor", new Color(1.0f, 0.98f, 0.92f, 1f));
            if (mat.HasProperty("_SColor")) mat.SetColor("_SColor", new Color(0.42f, 0.48f, 0.65f, 1f));
            if (mat.HasProperty("_RampThreshold")) mat.SetFloat("_RampThreshold", 0.50f);
            if (mat.HasProperty("_RampSmoothing")) mat.SetFloat("_RampSmoothing", 0.18f);
            if (mat.HasProperty("_SpecularColor")) mat.SetColor("_SpecularColor", new Color(0.9f, 0.9f, 0.9f, 1f));
            if (mat.HasProperty("_SpecularRoughnessPBR")) mat.SetFloat("_SpecularRoughnessPBR", 0.35f);
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", new Color(0.35f, 0.80f, 1.0f, 0.65f));
            if (mat.HasProperty("_RimMin")) mat.SetFloat("_RimMin", 0.45f);
            if (mat.HasProperty("_RimMax")) mat.SetFloat("_RimMax", 0.95f);
            if (mat.HasProperty("_StylizedPlasticOn")) mat.SetFloat("_StylizedPlasticOn", 1f);
            if (mat.HasProperty("_PlasticHighlightIntensity")) mat.SetFloat("_PlasticHighlightIntensity", 1.2f);
            if (mat.HasProperty("_PlasticHighlightColor")) mat.SetColor("_PlasticHighlightColor", Color.white);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static Transform EnsureZone(Transform parent, string name, Vector3 pos)
        {
            Transform found = parent.Find(name);
            if (found == null)
            {
                GameObject obj = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(obj, $"Create {name}");
                found = obj.transform;
                found.SetParent(parent, false);
            }
            found.position = pos;
            found.localRotation = Quaternion.identity;
            found.localScale = Vector3.one;
            return found;
        }

        private static void BackupLegacyObjects()
        {
            string[] legacyNames = new[] { "Waters", "Land", "Slots", "Rampa" };
            GameObject backupParent = GameObject.Find("[Legacy_Prototype_Backup]");

            foreach (string name in legacyNames)
            {
                GameObject go = GameObject.Find(name);
                if (go != null)
                {
                    if (backupParent == null)
                    {
                        backupParent = new GameObject("[Legacy_Prototype_Backup]");
                        backupParent.SetActive(false);
                        Undo.RegisterCreatedObjectUndo(backupParent, "Create Legacy Backup");
                    }
                    go.transform.SetParent(backupParent.transform, true);
                    go.SetActive(false);
                }
            }

            if (backupParent != null)
            {
                backupParent.SetActive(false);
            }
        }

        private static Material GetOrCreateIndicatorMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(IndicatorMatPath);
            Shader toonShader = Shader.Find("Toony Colors Pro 2/PixelGame/Cartoon");
            if (toonShader == null) toonShader = Shader.Find("Universal Render Pipeline/Lit");

            if (mat == null)
            {
                mat = new Material(toonShader);
                mat.name = "Indicator_Slot_Mat";

                string dir = Path.GetDirectoryName(IndicatorMatPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                AssetDatabase.CreateAsset(mat, IndicatorMatPath);
            }
            else if (mat.shader != toonShader && toonShader != null)
            {
                mat.shader = toonShader;
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(IndicatorTexturePath);
            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
            }
            mat.SetColor("_BaseColor", new Color(1.0f, 0.95f, 0.82f, 1.0f));

            if (mat.HasProperty("_HColor")) mat.SetColor("_HColor", new Color(1f, 1f, 0.95f, 1f));
            if (mat.HasProperty("_SColor")) mat.SetColor("_SColor", new Color(0.85f, 0.70f, 0.45f, 1f));
            if (mat.HasProperty("_RampThreshold")) mat.SetFloat("_RampThreshold", 0.50f);
            if (mat.HasProperty("_RampSmoothing")) mat.SetFloat("_RampSmoothing", 0.20f);
            if (mat.HasProperty("_SpecularColor")) mat.SetColor("_SpecularColor", new Color(0.9f, 0.9f, 0.9f, 1f));
            if (mat.HasProperty("_SpecularRoughnessPBR")) mat.SetFloat("_SpecularRoughnessPBR", 0.30f);
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", new Color(1.0f, 0.88f, 0.40f, 0.85f));
            if (mat.HasProperty("_RimMin")) mat.SetFloat("_RimMin", 0.40f);
            if (mat.HasProperty("_RimMax")) mat.SetFloat("_RimMax", 0.90f);
            if (mat.HasProperty("_StylizedPlasticOn")) mat.SetFloat("_StylizedPlasticOn", 1f);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static Material GetOrCreatePierWoodMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(PierWoodMatPath);
            Shader toonShader = Shader.Find("Toony Colors Pro 2/PixelGame/Cartoon");
            if (toonShader == null) toonShader = Shader.Find("Universal Render Pipeline/Lit");

            if (mat == null)
            {
                mat = new Material(toonShader);
                mat.name = "Pier_Wood_Mat";

                string dir = Path.GetDirectoryName(PierWoodMatPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                AssetDatabase.CreateAsset(mat, PierWoodMatPath);
            }
            else if (mat.shader != toonShader && toonShader != null)
            {
                mat.shader = toonShader;
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ShipTexturePath);
            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
            }
            // Sıcak tik/meşe ağacı ahşap tonu
            mat.SetColor("_BaseColor", new Color(0.78f, 0.48f, 0.22f, 1f));

            if (mat.HasProperty("_HColor")) mat.SetColor("_HColor", new Color(0.96f, 0.82f, 0.62f, 1f));
            if (mat.HasProperty("_SColor")) mat.SetColor("_SColor", new Color(0.46f, 0.26f, 0.12f, 1f));
            if (mat.HasProperty("_RampThreshold")) mat.SetFloat("_RampThreshold", 0.50f);
            if (mat.HasProperty("_RampSmoothing")) mat.SetFloat("_RampSmoothing", 0.22f);
            if (mat.HasProperty("_SpecularColor")) mat.SetColor("_SpecularColor", new Color(0.85f, 0.70f, 0.55f, 1f));
            if (mat.HasProperty("_SpecularRoughnessPBR")) mat.SetFloat("_SpecularRoughnessPBR", 0.35f);
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", new Color(0.92f, 0.75f, 0.45f, 0.70f));
            if (mat.HasProperty("_RimMin")) mat.SetFloat("_RimMin", 0.45f);
            if (mat.HasProperty("_RimMax")) mat.SetFloat("_RimMax", 0.92f);
            if (mat.HasProperty("_StylizedPlasticOn")) mat.SetFloat("_StylizedPlasticOn", 0f);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static Material GetOrCreatePierBoltMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(PierBoltMatPath);
            Shader toonShader = Shader.Find("Toony Colors Pro 2/PixelGame/Cartoon");
            if (toonShader == null) toonShader = Shader.Find("Universal Render Pipeline/Lit");

            if (mat == null)
            {
                mat = new Material(toonShader);
                mat.name = "Pier_Bolt_Mat";

                string dir = Path.GetDirectoryName(PierBoltMatPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                AssetDatabase.CreateAsset(mat, PierBoltMatPath);
            }
            else if (mat.shader != toonShader && toonShader != null)
            {
                mat.shader = toonShader;
            }

            // Gümüş / metalik parlak perçin tonu
            mat.SetColor("_BaseColor", new Color(0.85f, 0.88f, 0.92f, 1f));

            if (mat.HasProperty("_HColor")) mat.SetColor("_HColor", Color.white);
            if (mat.HasProperty("_SColor")) mat.SetColor("_SColor", new Color(0.40f, 0.44f, 0.50f, 1f));
            if (mat.HasProperty("_RampThreshold")) mat.SetFloat("_RampThreshold", 0.50f);
            if (mat.HasProperty("_RampSmoothing")) mat.SetFloat("_RampSmoothing", 0.15f);
            if (mat.HasProperty("_SpecularColor")) mat.SetColor("_SpecularColor", Color.white);
            if (mat.HasProperty("_SpecularRoughnessPBR")) mat.SetFloat("_SpecularRoughnessPBR", 0.15f);
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", new Color(1f, 1f, 1f, 0.85f));
            if (mat.HasProperty("_RimMin")) mat.SetFloat("_RimMin", 0.35f);
            if (mat.HasProperty("_RimMax")) mat.SetFloat("_RimMax", 0.95f);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static Material GetOrCreateShadowMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(ShadowMatPath);
            if (mat != null) return mat;

            Shader shader = Shader.Find(ShadowShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[GemiSceneSetup] {ShadowShaderName} bulunamadı, fallback shader kullanılıyor.");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            mat = new Material(shader);
            mat.name = "Gemi_ShadowCatcher_Mat";
            if (mat.HasProperty("_ShadowColor"))
            {
                mat.SetColor("_ShadowColor", new Color(0.04f, 0.08f, 0.16f, 0.42f));
            }

            string dir = Path.GetDirectoryName(ShadowMatPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            AssetDatabase.CreateAsset(mat, ShadowMatPath);
            AssetDatabase.SaveAssets();
            return mat;
        }

        public static void CaptureScreenshot()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            if (cam == null) return;

            int width = 1080;
            int height = 1920;

            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture prevRT = cam.targetTexture;
            RenderTexture prevActive = RenderTexture.active;

            try
            {
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                byte[] bytes = tex.EncodeToPNG();
                string dir = Path.GetDirectoryName(ScreenshotPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(ScreenshotPath, bytes);

                Object.DestroyImmediate(tex);
                Debug.Log($"<color=#00FFAA><b>[Screenshot]</b></color> 9:16 Ekran görüntüsü kaydedildi: {ScreenshotPath}");
            }
            finally
            {
                cam.targetTexture = prevRT;
                RenderTexture.active = prevActive;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }
    }
}
// Build trigger: 2026-09-23 20:00

