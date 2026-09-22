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
        private const string BgImagePath = "Assets/Kenney/ChatGPT Image 22 Eyl 2026 19_23_43.png";
        private const string ShadowShaderName = "Custom/URP_ShadowCatcher";
        private const string ShadowMatPath = "Assets/Materials/Gemi_ShadowCatcher_Mat.mat";
        private const string IndicatorMatPath = "Assets/Materials/Indicator_Slot_Mat.mat";
        private const string MainCubePrefabPath = "Assets/Prefabs/MainCube.prefab";
        private const string IndicatorModelPath = "Assets/Kenney/kenney_prototype-kit/Models/FBX format/indicator-square-b.fbx";
        private const string IndicatorTexturePath = "Assets/Kenney/kenney_prototype-kit/Models/Textures/variation-a.png";
        private const string ShipCargoModelPath = "Assets/Kenney/kenney_watercraft-pack/Models/FBX format/ship-cargo-a.fbx";
        private const string ShipTexturePath = "Assets/Kenney/kenney_watercraft-pack/Models/FBX format/Textures/colormap.png";
        private const string ShipMatPath = "Assets/Materials/Ship_Watercraft_Mat.mat";
        private const string ScreenshotPath = "scratch/gemi_gameplay_view.png";
        private const string AutoRunKey = "GemiSceneSetup_AutoRun_v25";

        // Kum alanı taş çerçevesinin tam ortası (World Units):
        // 9:16 ekranda orthoSize=8 iken Y=3.25f taş çerçevenin tam geometrik merkezidir.
        private static readonly Vector3 SandFrameCenterWorld = new Vector3(0f, 3.25f, 0f);
        private const float SandFrameCanvasY = 390.0f;
        private const float SandFrameCanvasSize = 510f;

        static GemiSceneSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!SessionState.GetBool(AutoRunKey, false))
                {
                    SessionState.SetBool(AutoRunKey, true);
                    Setup(includePixelArt: true, includeWaterSlots: true);
                    CaptureScreenshot();
                }
            };
        }

        [MenuItem("Tools/PixelGame/🏝️ Gemi Sahnesini Kur & Tüm Ögeleri Getir", priority = 10)]
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

        [MenuItem("Tools/PixelGame/🎨 Kum Alanındaki Piksel Resmi Yenile (Regenerate)", priority = 11)]
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

        [MenuItem("Tools/PixelGame/➡️ Sonraki Seviyeyi Yükle (Next Level)", priority = 12)]
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

        [MenuItem("Tools/PixelGame/⬅️ Önceki Seviyeyi Yükle (Prev Level)", priority = 13)]
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

        [MenuItem("Tools/PixelGame/📸 Gemi Sahnesi Ekran Görüntüsü Al", priority = 14)]
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
            if (activeRawImg.texture == null && bgTex != null)
            {
                activeRawImg.texture = bgTex;
            }
            activeRawImg.raycastTarget = false;

            RectTransform rawRect = activeRawImg.rectTransform;
            rawRect.anchorMin = Vector2.zero;
            rawRect.anchorMax = Vector2.one;
            rawRect.offsetMin = Vector2.zero;
            rawRect.offsetMax = Vector2.zero;
            rawRect.localScale = Vector3.one;

            // MainPlane UI Kılavuzu (Taş çerçevenin tam ortasını belirten görünmez UI hedefi)
            RectTransform targetFrameRect = null;
            Transform mainPlaneTr = canvasObj.transform.Find("MainPlane");
            if (mainPlaneTr == null)
            {
                GameObject planeObj = new GameObject("MainPlane", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                planeObj.transform.SetParent(canvasObj.transform, false);
                mainPlaneTr = planeObj.transform;
            }
            targetFrameRect = mainPlaneTr.GetComponent<RectTransform>();
            targetFrameRect.anchorMin = new Vector2(0.5f, 0.5f);
            targetFrameRect.anchorMax = new Vector2(0.5f, 0.5f);
            targetFrameRect.pivot = new Vector2(0.5f, 0.5f);
            targetFrameRect.anchoredPosition = new Vector2(0f, SandFrameCanvasY);
            targetFrameRect.sizeDelta = new Vector2(SandFrameCanvasSize, SandFrameCanvasSize);
            targetFrameRect.localScale = Vector3.one;

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
            Transform zoneBridge = EnsureZone(gameplayRoot.transform, "[Zone_Wooden_Bridge]", new Vector3(0f, -0.32f, 0f));
            Transform zoneWater = EnsureZone(gameplayRoot.transform, "[Zone_Water_LowerArea]", new Vector3(0f, -4.80f, 0f));

            // Eski Demo_Showcase nesnesini temizle
            Transform oldDemo = gameplayRoot.transform.Find("Demo_Showcase");
            if (oldDemo != null) Undo.DestroyObjectImmediate(oldDemo.gameObject);
            Transform strayDemo = GameObject.Find("Demo_Showcase")?.transform;
            if (strayDemo != null) Undo.DestroyObjectImmediate(strayDemo.gameObject);

            // 7. Piksel Görsellerini (PixelArtGenerator & LevelManager) Kum Çerçevesinin Ortasına Yerleştir
            if (includePixelArt)
            {
                SetupPixelArtSystem(zoneSand, targetFrameRect, cam);
            }

            // 8. Su Bölgesine 5 Adet Slot Yerleştir (indicator-square-b)
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
            if (genObj == null)
            {
                Transform inZone = sandZone.Find("[PixelArtGenerator]");
                if (inZone != null) genObj = inZone.gameObject;
                else
                {
                    genObj = new GameObject("[PixelArtGenerator]");
                    Undo.RegisterCreatedObjectUndo(genObj, "Create PixelArtGenerator");
                }
            }

            genObj.transform.SetParent(sandZone, false);
            genObj.transform.localPosition = Vector3.zero;
            genObj.transform.localRotation = Quaternion.identity;
            genObj.transform.localScale = Vector3.one;

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
            so.FindProperty("m_PreserveSceneEdits").boolValue = false;
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

            // 4. Piksel küplerini doğrudan çerçeve içerisine üret
            generator.GeneratePixelArt();

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

            slotsGroup.localPosition = new Vector3(0f, 0.40f, 0f);
            slotsGroup.localRotation = Quaternion.identity;
            slotsGroup.localScale = Vector3.one;

            // Indicator ve Gemi Modellerini yükle
            GameObject indicatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(IndicatorModelPath);
            GameObject shipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShipCargoModelPath);

            Material indicatorMat = GetOrCreateIndicatorMaterial();
            Material shipMat = GetOrCreateShipMaterial();

            // 5 adet slot yerleştir
            const int slotCount = 5;
            const float slotSpacing = 1.48f; // Ekran genişliğine (9.0 birim) tam oturan dengeli aralık
            float startX = -(slotCount - 1) * slotSpacing * 0.5f; // -2.96f

            Color[] initialShipColors = new Color[]
            {
                new Color(0.18f, 0.52f, 0.95f, 1f), // Canlı Mavi (Rakun Gövdesi)
                new Color(0.95f, 0.28f, 0.25f, 1f), // Canlı Kırmızı
                new Color(0.22f, 0.82f, 0.42f, 1f), // Canlı Yeşil
                new Color(0.98f, 0.78f, 0.15f, 1f), // Canlı Sarı (Zemin)
                new Color(0.25f, 0.28f, 0.38f, 1f)  // Koyu Gri/Mavi (Rakun Gözleri/Kuyruk)
            };

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
                slotTr.localPosition = new Vector3(posX, 0f, 0f);
                // 3D su perspektif açısıyla uyumlu eğim (Ebeveyn ölçeği tamamen UNIFORM 1.35):
                slotTr.localRotation = Quaternion.Euler(-68f, 0f, 0f);
                slotTr.localScale = Vector3.one * 1.35f;

                ShipSlot shipSlot = slotTr.GetComponent<ShipSlot>();
                if (shipSlot == null) shipSlot = slotTr.gameObject.AddComponent<ShipSlot>();
                shipSlot.SlotIndex = i;
                shipSlot.ReleaseShip();

                // Eski model çocuklarını temizle
                while (slotTr.childCount > 0)
                {
                    Undo.DestroyObjectImmediate(slotTr.GetChild(0).gameObject);
                }

                // indicator-square-b taban modelini ekle (Mesh'i dikeyde uzatarak ferah kıldık, ebeveyn ölçeği bozulmadı!)
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

            queueObj.localPosition = new Vector3(0f, -1.35f, 0f);
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
