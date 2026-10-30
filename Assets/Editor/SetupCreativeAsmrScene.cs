using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using TMPro;
using PixelGame.Creative;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class SetupCreativeAsmrScene
    {
        private const string ScenePath = "Assets/Scenes/Creative_ASMR.unity";
        private const string TabletopCubePrefabPath = "Assets/Prefabs/MainCube_Tabletop.prefab";
        private const string FallbackCubePrefabPath = "Assets/Prefabs/MainCube.prefab";
        private const string ShadowMatPath = "Assets/Resources/CubeContactShadow_Mat.mat";
        private const string SoftShadowMatPath = "Assets/Materials/SoftVoxelShadow_Mat.mat";
        private const string SailorPrefabPath = "Assets/Resources/Sailor_Runner.prefab";
        private const string HarborBgPath = "Assets/Renkli Liman İskelesi ve Deniz.png";
        private const string FontPath = "Assets/Fonts/LilitaOne-Regular SDF.asset";

        // Konteyner Görselleri
        private const string ContainerBluePath = "Assets/Textures/Containers/Container_Blue.png";
        private const string ContainerPureShadowPath = "Assets/Textures/Containers/Container_PureShadow.png";
        private const string BadgePillPath = "Assets/UI/Badge_Pill.png";
        private const string ContainerShadowPath = "Assets/UI/CargoContainer_Shadow.png";
        private const string ContainerSoftShadowMatPath = "Assets/Materials/SoftContainerShadow_Mat.mat";

        // Küp Materyalleri (Toony Colors Pro - Parlak Plastik Efektli)
        private const string CubeMatPinkPath = "Assets/Materials/Creative_Mat_Pink.mat";
        private const string CubeMatYellowPath = "Assets/Materials/Creative_Mat_Yellow.mat";
        private const string CubeMatBluePath = "Assets/Materials/Creative_Mat_Blue.mat";

        // Gemici Materyalleri
        private const string SailorTeamPinkPath = "Assets/Materials/Creative_Sailor_Team_Pink.mat";
        private const string SailorStripesPinkPath = "Assets/Materials/Creative_Sailor_Stripes_Pink.mat";
        private const string SailorTeamYellowPath = "Assets/Materials/Creative_Sailor_Team_Yellow.mat";
        private const string SailorStripesYellowPath = "Assets/Materials/Creative_Sailor_Stripes_Yellow.mat";
        private const string SailorTeamBluePath = "Assets/Materials/Creative_Sailor_Team_Blue.mat";
        private const string SailorStripesBluePath = "Assets/Materials/Creative_Sailor_Stripes_Blue.mat";

        // Takım Renkleri
        public static readonly Color PinkColor = new Color(1.00f, 0.22f, 0.44f, 1f);   // #FF3870 (Canlı Mercan Pembe)
        public static readonly Color YellowColor = new Color(1.00f, 0.82f, 0.00f, 1f); // #FFD100 (Altın Sarısı)
        public static readonly Color BlueColor = new Color(0.00f, 0.71f, 0.95f, 1f);   // #00B5F2 (Elektrik Gökyüzü Mavisi)

        // Orijinal oyun sahnesinden (Gemi.unity) türetilen, daha kompakt ve sevimli ASMR orantıları:
        private const float CellSize = 0.22f; // Daha küçük küpler ve gemiciler
        private const float SailorToCubeRatio = 2.4215f; // 1.45 * 1.67

        private static bool s_HasBuilt = false;

        static SetupCreativeAsmrScene()
        {
            EditorApplication.update -= CheckFlag;
            EditorApplication.update += CheckFlag;
        }

        private static void CheckFlag()
        {
            if (System.IO.File.Exists("scratch/build_creative.flag"))
            {
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.isPlaying = false;
                    return;
                }
                try { System.IO.File.Delete("scratch/build_creative.flag"); } catch {}
                BuildAndOpenCreativeScene();
                CaptureGameViewScreenshot.Capture();
            }

            if (System.IO.File.Exists("scratch/record_creative.flag"))
            {
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.isPlaying = false;
                    return;
                }
                try { System.IO.File.Delete("scratch/record_creative.flag"); } catch {}
                BuildAndOpenCreativeScene();

                // Kameraya recorder bileşeni ekle
                Camera cam = Camera.main;
                if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
                if (cam != null)
                {
                    if (cam.GetComponent<CreativeSceneVideoRecorder>() == null)
                    {
                        cam.gameObject.AddComponent<CreativeSceneVideoRecorder>();
                    }
                }

                // is_recording.flag oluştur
                System.IO.File.WriteAllText("scratch/is_recording.flag", "record");

                EditorSceneManager.SaveOpenScenes();
                EditorApplication.isPlaying = true;
            }
        }

        [InitializeOnLoadMethod]
        private static void AutoRebuildOnce()
        {
            EditorApplication.update -= CheckFlag;
            EditorApplication.update += CheckFlag;

            if (s_HasBuilt || EditorApplication.isPlayingOrWillChangePlaymode) return;
            s_HasBuilt = true;
            EditorApplication.delayCall += () =>
            {
                BuildAndOpenCreativeScene();
            };
        }

        // [MenuItem("Tools/PixelGame/🎬 ASMR Kreatif Sahnesini Aç", priority = 0)]
        public static void OpenCreativeScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        // [MenuItem("Tools/PixelGame/🎬 ASMR Kreatif Sahnesini Sıfırdan Yeniden Kur (Su Damlası + Konteyner)", priority = 1)]
        public static void BuildAndOpenCreativeScene()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset("Assets/Textures/SoftContainerShadow.png", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(ContainerSoftShadowMatPath, ImportAssetOptions.ForceUpdate);

            // 1. Yeni ve tertemiz sahne aç
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. IŞIKLANDIRMA - Gemi.unity ile Birebir Aynı (Saf Beyaz, 1.25 Yoğunluk, 36°x12° Açı)
            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.72f;
            light.shadowBias = 0.05f;
            light.shadowNormalBias = 0.4f;
            light.shadowNearPlane = 0.2f;
            lightGo.transform.position = new Vector3(-3.0f, 8.0f, -6.0f);
            lightGo.transform.rotation = Quaternion.Euler(36.0f, 12.0f, 0.0f);

            var urpLight = lightGo.AddComponent<UniversalAdditionalLightData>();
            if (urpLight != null)
            {
                urpLight.usePipelineSettings = true;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.212f, 0.227f, 0.259f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.114f, 0.125f, 0.133f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.047f, 0.043f, 0.035f, 1f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.subtractiveShadowColor = new Color(0.42f, 0.478f, 0.627f, 1f);
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.fog = false;

            // 3. Su Damlası Grid ve Başlangıç Koordinatları
            float groundZ = 0.42f;
            int totalCols = 5;
            int totalRows = 6;
            float stepX = CellSize;
            float stepY = CellSize;
            float gridStartX = -((totalCols - 1) * stepX) * 0.5f; // -0.56f
            float gridStartY = -((totalRows - 1) * stepY) * 0.5f; // -0.70f

            // Gemicinin alt başlangıç noktası (DİK koşu için alttan başlar):
            float sailorStartY = gridStartY - 0.45f; // ~ -1.15f

            // Kamera Hedef Merkezi (CameraCenterAnchor)
            GameObject centerAnchorGo = new GameObject("CameraCenterAnchor");
            centerAnchorGo.transform.position = new Vector3(0f, sailorStartY, groundZ);

            // 4. 3rd Person Takip Kamerası (Default: PerspectiveThirdPerson - Gemicinin Arkasından Takip)
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.82f, 0.85f, 0.90f, 1f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 120f;
            cam.fieldOfView = 48f;
            cam.orthographic = false;

            camGo.AddComponent<AudioListener>();
            var urpCam = camGo.AddComponent<UniversalAdditionalCameraData>();
            if (urpCam != null) urpCam.renderShadows = true;

            camGo.transform.position = new Vector3(0f, sailorStartY - 2.10f, groundZ - 3.20f);
            camGo.transform.rotation = Quaternion.Euler(322f, 0f, 0f);

            var tpCam = camGo.AddComponent<CreativeThirdPersonCamera>();
            tpCam.Mode = CreativeThirdPersonCamera.CameraMode.PerspectiveThirdPerson;
            tpCam.Target = centerAnchorGo.transform;
            tpCam.ApplyCameraSettings();

            // 5. Arkaplan Canvas (9:16)
            GameObject canvasGo = new GameObject("Background_Canvas");
            canvasGo.layer = 5;
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 35f;
            canvas.sortingOrder = -100;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;

            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject bgImageGo = new GameObject("BackgroundImage");
            bgImageGo.layer = 5;
            bgImageGo.transform.SetParent(canvasGo.transform, false);

            RectTransform bgRect = bgImageGo.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.anchoredPosition = Vector2.zero;
            bgRect.sizeDelta = Vector2.zero;
            bgRect.pivot = new Vector2(0.5f, 0.5f);

            RawImage rawImage = bgImageGo.AddComponent<RawImage>();
            Texture2D harborTex = AssetDatabase.LoadAssetAtPath<Texture2D>(HarborBgPath);
            if (harborTex != null)
            {
                rawImage.texture = harborTex;
            }
            // Sadece alt temiz gri iskele zeminini zoomla (üstteki deniz, vinç, gökyüzü tamamen kırpılır)
            rawImage.uvRect = new Rect(0.12f, 0.0f, 0.76f, 0.42f);
            rawImage.color = Color.white;
            rawImage.raycastTarget = false;

            // 6. UpperBeachGround (Yumuşak temas gölgeleri için görünmez zemin)
            GameObject ubGo = new GameObject("[Upper_Beach_Ground]");
            UpperBeachGround ubg = ubGo.AddComponent<UpperBeachGround>();
            ubg.Center = new Vector3(0f, 0.5f, 0.423f);
            ubg.Size = new Vector2(16f, 20f);
            ubg.EnsureGround();

            // 7. ASMR Geri Bildirim Yöneticisi
            GameObject feedbackGo = new GameObject("[HypercasualFeedbackManager]");
            HypercasualFeedbackManager feedback = feedbackGo.AddComponent<HypercasualFeedbackManager>();
            feedback.AsmrCreativeOnlyPickupSound = true;
            feedback.AsmrUsePureProceduralSound = false; // Orijinal Kenney impactSoft küp sesleri hakim
            feedback.AsmrPickupVolume = 0.50f;
            feedback.PickupSoundVolume = 0.50f;
            feedback.LiftoffSoundDebounceTime = 0.032f;

            // Orijinal oyundaki (Gemi.unity) 5 adet impactSoft küp toplama ses klibini bağla:
            feedback.PickupSoundClips = new AudioClip[]
            {
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Kenney/kenney_impact-sounds/Audio/impactSoft_medium_000.ogg"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Kenney/kenney_impact-sounds/Audio/impactSoft_medium_001.ogg"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Kenney/kenney_impact-sounds/Audio/impactSoft_medium_002.ogg"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Kenney/kenney_impact-sounds/Audio/impactSoft_medium_003.ogg"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Kenney/kenney_impact-sounds/Audio/impactSoft_medium_004.ogg")
            };

            // 8. 💧 SU DAMLASI (WATER DROP) 16 KÜP DESENİ
            // 5 sütun x 6 satır içerisinde damla formu:
            // Sütun 0: 1 küp (R2)
            // Sütun 1: 4 küp (R0, R1, R2, R3)
            // Sütun 2: 6 küp (R5, R4, R3, R2, R1, R0 - sivri tepeden tabana)
            // Sütun 3: 4 küp (R0, R1, R2, R3)
            // Sütun 4: 1 küp (R2)
            // Toplam = 1 + 4 + 6 + 4 + 1 = 16 Küp!
            GameObject gridRoot = new GameObject("[Pixel_Cubes_WaterDrop_16]");
            GameObject cubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TabletopCubePrefabPath);
            if (cubePrefab == null)
            {
                cubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FallbackCubePrefabPath);
            }

            Material matBlue = AssetDatabase.LoadAssetAtPath<Material>(CubeMatBluePath);
            Material shadowMat = AssetDatabase.LoadAssetAtPath<Material>(ShadowMatPath);
            Material softShadowMat = AssetDatabase.LoadAssetAtPath<Material>(SoftShadowMatPath);
            if (softShadowMat == null) softShadowMat = shadowMat;

            var waterDropPattern = new (int col, int row)[]
            {
                // Satır 0 (Dip kavis, soldan sağa):
                (1, 0), (2, 0), (3, 0),
                // Satır 1 (Sağdan sola akıcı geçiş):
                (3, 1), (2, 1), (1, 1),
                // Satır 2 (En geniş gövde, soldan sağa):
                (0, 2), (1, 2), (2, 2), (3, 2), (4, 2),
                // Satır 3 (Sağdan sola):
                (3, 3), (2, 3), (1, 3),
                // Satır 4 (Damlanın boynu):
                (2, 4),
                // Satır 5 (Damlanın sivri tepe ucu):
                (2, 5)
            };

            Dictionary<(int col, int row), PixelCube> cubeLookup = new Dictionary<(int col, int row), PixelCube>();

            for (int i = 0; i < waterDropPattern.Length; i++)
            {
                int c = waterDropPattern[i].col;
                int r = waterDropPattern[i].row;

                Vector3 pos = new Vector3(gridStartX + c * stepX, gridStartY + r * stepY, groundZ);

                GameObject cubeGo;
                if (cubePrefab != null)
                {
                    cubeGo = (GameObject)PrefabUtility.InstantiatePrefab(cubePrefab, gridRoot.transform);
                    cubeGo.transform.position = pos;
                    cubeGo.transform.rotation = Quaternion.identity;
                    cubeGo.transform.localScale = Vector3.one * CellSize;
                }
                else
                {
                    cubeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cubeGo.transform.SetParent(gridRoot.transform, false);
                    cubeGo.transform.position = pos;
                    cubeGo.transform.rotation = Quaternion.identity;
                    cubeGo.transform.localScale = Vector3.one * CellSize;
                }

                cubeGo.name = $"PixelCube_WaterDrop_{i}_C{c}_R{r}";

                for (int chIdx = 0; chIdx < cubeGo.transform.childCount; chIdx++)
                {
                    Transform ch = cubeGo.transform.GetChild(chIdx);
                    if (ch != null && ch.name.StartsWith("LegsMount")) ch.gameObject.SetActive(false);
                }

                MeshRenderer mr = cubeGo.GetComponent<MeshRenderer>();
                if (mr != null && matBlue != null)
                {
                    mr.sharedMaterial = matBlue;
                }

                PixelCube pixelCube = cubeGo.GetComponent<PixelCube>();
                if (pixelCube == null) pixelCube = cubeGo.AddComponent<PixelCube>();

                pixelCube.Initialize(c, r, BlueColor, 0f);
                pixelCube.ApplyColor(BlueColor, 0f);
                pixelCube.SetBodyCastsRealtimeShadow(false);

                // 🌑 SAHNEYE ÖZEL ZEMİN TEMAS GÖLGESİ (Cube Ground Contact Shadow)
                // Küpün arkasında (Z = 0.52f, sortingOrder = -1) durur; asla küpün içine veya önüne çizilmez!
                // Yalnızca şeklin tabanından ve kenarlarından zemine doğru taşar.
                GameObject shadowQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                shadowQuad.name = "CubeShadow";
                shadowQuad.transform.SetParent(cubeGo.transform, false);
                shadowQuad.transform.localPosition = new Vector3(0f, -0.58f, 0.52f);
                shadowQuad.transform.localRotation = Quaternion.identity;
                shadowQuad.transform.localScale = new Vector3(1.45f, 1.05f, 1f);

                MeshRenderer shadowMr = shadowQuad.GetComponent<MeshRenderer>();
                if (shadowMr != null)
                {
                    if (shadowMat != null) shadowMr.sharedMaterial = shadowMat;
                    else if (softShadowMat != null) shadowMr.sharedMaterial = softShadowMat;
                    shadowMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    shadowMr.receiveShadows = false;
                    shadowMr.sortingOrder = -1; // Küp gövdesinin (-0) ve diğer küplerin arkasında, Canvas (-100) önünde!
                }

                Collider shadowCol = shadowQuad.GetComponent<Collider>();
                if (shadowCol != null) Object.DestroyImmediate(shadowCol);

                pixelCube.SetShadowObject(shadowQuad);
                pixelCube.KeepShadowPermanent = true;

                PrefabUtility.UnpackPrefabInstance(cubeGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                EditorUtility.SetDirty(cubeGo);
                EditorUtility.SetDirty(pixelCube);

                cubeLookup[(c, r)] = pixelCube;
            }

            // 3 Gemici için Rotalar:
            // 1. ORTA HERO GEMİCİ (c == 2 sütunu boyunca dümdüz yukarı, 6 küp):
            List<PixelCube> middleCubes = new List<PixelCube>();
            var middleCoords = new (int c, int r)[] { (2, 0), (2, 1), (2, 2), (2, 3), (2, 4), (2, 5) };
            foreach (var coord in middleCoords)
            {
                if (cubeLookup.TryGetValue(coord, out var cube)) middleCubes.Add(cube);
            }

            // 2. SOL GEMİCİ (Sol taraf kavisindeki küpler, 5 küp):
            List<PixelCube> leftCubes = new List<PixelCube>();
            var leftCoords = new (int c, int r)[] { (1, 0), (1, 1), (0, 2), (1, 2), (1, 3) };
            foreach (var coord in leftCoords)
            {
                if (cubeLookup.TryGetValue(coord, out var cube)) leftCubes.Add(cube);
            }

            // 3. SAĞ GEMİCİ (Sağ taraf kavisindeki küpler, 5 küp):
            List<PixelCube> rightCubes = new List<PixelCube>();
            var rightCoords = new (int c, int r)[] { (3, 0), (3, 1), (4, 2), (3, 2), (3, 3) };
            foreach (var coord in rightCoords)
            {
                if (cubeLookup.TryGetValue(coord, out var cube)) rightCubes.Add(cube);
            }

            // 9. 📦 DÜNYA İÇİ KONTEYNER İSTASYONU (İSKELE TEPESİNDE - VARIŞ NOKTASI)
            // Bloklardan ferahça uzakta, iskelenin ucunda (Y = 2.45f) durur; gemiciler blokları taşıyarak bir süre yürür
            float containerY = 2.45f;
            float containerX = 0f; // Tam merkezde
            GameObject containerRoot = new GameObject("Container_Dropoff_Station");
            containerRoot.transform.position = new Vector3(containerX, containerY, groundZ);
            containerRoot.transform.rotation = Quaternion.Euler(-20f, 0f, 0f); // Kameraya dik ve net bakan açı

            // Konteyner Zemin Temas Gölgesi (Konteyner kutu formuna özel, her yönden yumuşak sönen kapsül gölge)
            GameObject contShadowGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            contShadowGo.name = "Container_FakeShadow";
            contShadowGo.transform.position = new Vector3(containerX, 1.38f, groundZ + 0.02f);
            contShadowGo.transform.rotation = Quaternion.identity;
            contShadowGo.transform.localScale = new Vector3(1.28f, 0.78f, 1f);

            MeshRenderer contShadowMr = contShadowGo.GetComponent<MeshRenderer>();
            if (contShadowMr != null)
            {
                Material contShadowMat = AssetDatabase.LoadAssetAtPath<Material>(ContainerSoftShadowMatPath);
                if (contShadowMat == null)
                {
                    Texture2D contTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/SoftContainerShadow.png");
                    if (contTex != null)
                    {
                        contShadowMat = new Material(Shader.Find("Sprites/Default"));
                        contShadowMat.mainTexture = contTex;
                        contShadowMat.color = new Color(0.02f, 0.045f, 0.12f, 0.65f);
                    }
                }
                if (contShadowMat != null) contShadowMr.sharedMaterial = contShadowMat;
                else if (softShadowMat != null) contShadowMr.sharedMaterial = softShadowMat;
                contShadowMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                contShadowMr.receiveShadows = false;
                contShadowMr.sortingOrder = 2; // Zemin (-100) önünde, Konteyner (5) arkasında
            }

            Collider contCol = contShadowGo.GetComponent<Collider>();
            if (contCol != null) Object.DestroyImmediate(contCol);

            var tweaker = contShadowGo.AddComponent<PixelGame.Creative.ContainerShadowTweaker>();
            tweaker.Width = 1.28f;
            tweaker.Height = 0.78f;
            tweaker.PositionX = containerX;
            tweaker.PositionY = 1.38f;
            tweaker.PositionZ = groundZ + 0.02f;

            // Dünya alanı Canvas (Konteyner görseli kesinlikle gölgenin önünde olsun diye sortingOrder = 5)
            Canvas containerCanvas = containerRoot.AddComponent<Canvas>();
            containerCanvas.renderMode = RenderMode.WorldSpace;
            containerCanvas.sortingOrder = 5;
            RectTransform containerRt = containerRoot.GetComponent<RectTransform>();
            containerRt.sizeDelta = new Vector2(160f, 163f);
            containerRt.localScale = Vector3.one * 0.0078f; // ~1.25m dünya boyutu

            // 1. KATMAN: 🌑 Saf Konteyner Gölgesi (Container_PureShadow.png) - Beyaz kontursuz, saf yumuşak gölge
            GameObject shadowChildGo = new GameObject("Shadow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shadowChildGo.transform.SetParent(containerRoot.transform, false);

            float uiOutlinePad = 64f / 512f;
            RectTransform srt = (RectTransform)shadowChildGo.transform;
            srt.anchorMin = Vector2.one * -uiOutlinePad;
            srt.anchorMax = Vector2.one * (1f + uiOutlinePad);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.localRotation = Quaternion.identity;
            srt.localScale = Vector3.one;

            Image shadowImg = shadowChildGo.GetComponent<Image>();
            Sprite pureShadowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ContainerPureShadowPath);
            if (pureShadowSprite != null) shadowImg.sprite = pureShadowSprite;
            shadowImg.color = Color.white;
            shadowImg.raycastTarget = false;

            // Mavi Body (sortingOrder = 5) arkasında çizilsin:
            Canvas sc = shadowChildGo.AddComponent<Canvas>();
            sc.overrideSorting = true;
            sc.sortingOrder = 4;
            srt.localPosition = Vector3.zero;

            // 2. KATMAN: 🟦 Mavi Konteyner Gövdesi (Container_Blue.png) - Gölgenin önünde çizilir
            GameObject bodyGo = new GameObject("Body", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bodyGo.transform.SetParent(containerRoot.transform, false);

            RectTransform brt = (RectTransform)bodyGo.transform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.localRotation = Quaternion.identity;
            brt.localScale = Vector3.one;

            Image containerImg = bodyGo.GetComponent<Image>();
            Sprite containerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ContainerBluePath);
            if (containerSprite != null) containerImg.sprite = containerSprite;
            containerImg.color = Color.white;
            containerImg.raycastTarget = false;

            // Quad sahte gölgesi kapatılır
            contShadowGo.SetActive(false);

            // 3. KATMAN: 🏷️ Konteyner Sayı Rozeti (Badge_Pill & TextMeshProUGUI)
            // Kullanıcı isteği: "ana oyundaki gibi konteynerlar üstünde sayı olsun. her küp girince azalsın falan."
            GameObject badgeGo = new GameObject("Badge_Pill", typeof(RectTransform));
            badgeGo.transform.SetParent(containerRoot.transform, false);

            RectTransform badgeRt = (RectTransform)badgeGo.transform;
            badgeRt.anchorMin = new Vector2(0.5f, 0.5f);
            badgeRt.anchorMax = new Vector2(0.5f, 0.5f);
            badgeRt.pivot = new Vector2(0.5f, 0.5f);
            badgeRt.anchoredPosition = new Vector2(0f, 21f);
            badgeRt.sizeDelta = new Vector2(140f, 110f);

            Canvas badgeCanvas = badgeGo.AddComponent<Canvas>();
            badgeCanvas.overrideSorting = true;
            badgeCanvas.sortingOrder = 10; // Body (5) üzerinde çizilsin

            TextMeshProUGUI badgeTmp = badgeGo.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset lilitaFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (lilitaFont != null) badgeTmp.font = lilitaFont;
            badgeTmp.alignment = TextAlignmentOptions.Center;
            badgeTmp.enableAutoSizing = true;
            badgeTmp.fontSizeMin = 36f;
            badgeTmp.fontSizeMax = 64f;
            badgeTmp.fontSize = 58f;
            badgeTmp.fontStyle = FontStyles.Bold;
            badgeTmp.color = Color.white;
            badgeTmp.outlineWidth = 0.28f;
            badgeTmp.outlineColor = Color.black;
            badgeTmp.text = "16";
            badgeTmp.raycastTarget = false;

            // 4. KATMAN: 🌟 Bitiş Damgası (Perfect Stamp / TextMeshProUGUI)
            GameObject stampGo = new GameObject("Perfect_Stamp", typeof(RectTransform));
            stampGo.transform.SetParent(containerRoot.transform, false);

            RectTransform stampRt = (RectTransform)stampGo.transform;
            stampRt.anchorMin = new Vector2(0.5f, 0.5f);
            stampRt.anchorMax = new Vector2(0.5f, 0.5f);
            stampRt.pivot = new Vector2(0.5f, 0.5f);
            stampRt.anchoredPosition = new Vector2(0f, 142f); // Konteynerin ve rozetin üzerinde ferah duruş
            stampRt.sizeDelta = new Vector2(460f, 130f);

            Canvas stampCanvas = stampGo.AddComponent<Canvas>();
            stampCanvas.overrideSorting = true;
            stampCanvas.sortingOrder = 25; // En üstte net okunsun

            TextMeshProUGUI stampTmp = stampGo.AddComponent<TextMeshProUGUI>();
            if (lilitaFont != null) stampTmp.font = lilitaFont;
            stampTmp.alignment = TextAlignmentOptions.Center;
            stampTmp.enableAutoSizing = true;
            stampTmp.fontSizeMin = 28f;
            stampTmp.fontSizeMax = 52f;
            stampTmp.fontSize = 48f;
            stampTmp.fontStyle = FontStyles.Bold;
            stampTmp.enableWordWrapping = false;
            stampTmp.color = new Color(1.0f, 0.88f, 0.10f, 1f); // Altın sarısı / Canlı sarı
            stampTmp.outlineWidth = 0.32f;
            stampTmp.outlineColor = new Color(0.08f, 0.10f, 0.22f, 1f); // Tok koyu lacivert kontur
            stampTmp.text = "PERFECT!";
            stampTmp.raycastTarget = false;
            stampGo.SetActive(false); // Başlangıçta gizli

            // 10. 🏃 3 GEMİCİ TAKIMI (SOL, ORTA HERO, SAĞ)
            // Kullanıcı isteği:
            // - Tek denizci olmasın, 3 tane olsun.
            // - Ortadakini kamera takip etsin.
            // - Ortadaki dümdüz alırken diğerleri solu ve sağı alır.
            // - Sonra biri soldan, biri önden, diğeri sağdan konteynere bırakır blokları!

            GameObject sailorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SailorPrefabPath);
            Material sailorTeamMat = AssetDatabase.LoadAssetAtPath<Material>(SailorTeamBluePath);
            Material sailorStripeMat = AssetDatabase.LoadAssetAtPath<Material>(SailorStripesBluePath);

            float sailorScale = CellSize * SailorToCubeRatio;
            float laneOffset = 0.36f; // Sol ve sağ koridor aralığı (başlangıçta şapkaların birbirine çarpmaması için)

            Vector3 leftStartPos = new Vector3(-laneOffset, sailorStartY, groundZ);
            Vector3 middleStartPos = new Vector3(0f, sailorStartY, groundZ);
            Vector3 rightStartPos = new Vector3(+laneOffset, sailorStartY, groundZ);

            // 1. SOL GEMİCİ: Sol taraf kavisini toplar, konteynere SOLDAN yanaşır (ferah mesafeli durur)
            GameObject leftSailorGo = InstantiateSailorGo(sailorPrefab, "Sailor_Left_Blue", leftStartPos, sailorScale, sailorTeamMat, sailorStripeMat, BlueColor, groundZ);
            var leftSailorData = new CreativeSailorData
            {
                Name = "Sailor_Left",
                SailorObject = leftSailorGo,
                TeamColor = BlueColor,
                AssignedCubes = leftCubes,
                Visual = leftSailorGo.GetComponent<SailorRunnerVisual>(),
                InitialPosition = leftStartPos,
                DropOffset = new Vector3(-0.75f, -0.38f, 0f), // Konteynerin SOLUNDA ve mesafeli durur
                FinalFacingDirection = new Vector3(0.85f, 0.52f, 0f).normalized // Konteynere sağ-yukarı bakar
            };

            // 2. ORTA HERO GEMİCİ: Dümdüz yukarı c=2 sütununu toplar, ÖNDEN yanaşır (dipten değil mesafeli)
            GameObject middleSailorGo = InstantiateSailorGo(sailorPrefab, "Sailor_Hero_Middle_Blue", middleStartPos, sailorScale, sailorTeamMat, sailorStripeMat, BlueColor, groundZ);
            var middleSailorData = new CreativeSailorData
            {
                Name = "Sailor_Hero_Middle",
                SailorObject = middleSailorGo,
                TeamColor = BlueColor,
                AssignedCubes = middleCubes,
                Visual = middleSailorGo.GetComponent<SailorRunnerVisual>(),
                InitialPosition = middleStartPos,
                DropOffset = new Vector3(0f, -0.80f, 0f), // Konteynerin ÖNÜNDE ve mesafeli durur (dipten değil)
                FinalFacingDirection = Vector3.up // Konteynere dik bakar
            };

            // 3. SAĞ GEMİCİ: Sağ taraf kavisini toplar, konteynere SAĞDAN yanaşır (ferah mesafeli durur)
            GameObject rightSailorGo = InstantiateSailorGo(sailorPrefab, "Sailor_Right_Blue", rightStartPos, sailorScale, sailorTeamMat, sailorStripeMat, BlueColor, groundZ);
            var rightSailorData = new CreativeSailorData
            {
                Name = "Sailor_Right",
                SailorObject = rightSailorGo,
                TeamColor = BlueColor,
                AssignedCubes = rightCubes,
                Visual = rightSailorGo.GetComponent<SailorRunnerVisual>(),
                InitialPosition = rightStartPos,
                DropOffset = new Vector3(+0.75f, -0.38f, 0f), // Konteynerin SAĞINDA ve mesafeli durur
                FinalFacingDirection = new Vector3(-0.85f, 0.52f, 0f).normalized // Konteynere sol-yukarı bakar
            };

            // Kamera orta gemicinin tam peşine takılır
            centerAnchorGo.transform.position = middleStartPos;
            tpCam.Target = centerAnchorGo.transform;
            if (tpCam.GetComponent<CreativeSceneVideoRecorder>() == null)
            {
                tpCam.gameObject.AddComponent<CreativeSceneVideoRecorder>();
            }

            // 11. 🎬 SAHNE YÖNETİCİSİ (CreativeAsmrSceneController)
            GameObject managerGo = new GameObject("[CREATIVE_ASMR_MANAGER]");
            CreativeAsmrSceneController controller = managerGo.AddComponent<CreativeAsmrSceneController>();
            controller.Camera = tpCam;
            controller.CameraCenterAnchor = centerAnchorGo.transform;
            controller.CameraFollowSailorIndex = 1; // 1 = Orta Hero Gemici!

            controller.Sailors.Add(leftSailorData);   // Index 0: Sol
            controller.Sailors.Add(middleSailorData); // Index 1: Orta (Hero - Kamera Takip)
            controller.Sailors.Add(rightSailorData);  // Index 2: Sağ

            controller.ContainerTransform = containerRoot.transform;
            controller.ContainerBadgeText = badgeTmp;
            controller.BadgeAnchoredPosition = new Vector2(0f, 21f);
            controller.ContainerImage = containerImg;
            controller.ContainerCapacity = 16;
            controller.PerfectStamp = stampGo;

            // 🎊 Konfeti Efekti Sistemi ([Creative_Confetti_FX])
            GameObject confettiGo = new GameObject("[Creative_Confetti_FX]");
            confettiGo.transform.SetParent(managerGo.transform, false);
            confettiGo.transform.position = containerRoot.transform.position;
            ParticleSystem confettiPs = confettiGo.AddComponent<ParticleSystem>();
            controller.SetupConfettiParticleSystem(confettiPs);
            controller.ConfettiPS = confettiPs;

            controller.CacheInitialCubeData();

            EditorUtility.SetDirty(managerGo);

            // 12. Sahneyi kaydet
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log("<color=#00FFAA><b>[Creative ASMR Scene]</b></color> 🌊 Su Damlası + Konteyner Teslimatı + Third Person Kamera Başarıyla Kuruldu: <b>Assets/Scenes/Creative_ASMR.unity</b>!");
        }

        // [MenuItem("Tools/PixelGame/🎮 Normal Oyun Sahnesini Aç (Gemi)", priority = 2)]
        public static void OpenGameScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene("Assets/Scenes/Gemi.unity", OpenSceneMode.Single);
            }
        }

        private static GameObject InstantiateSailorGo(GameObject prefab, string name, Vector3 pos, float scale, Material teamMat, Material stripeMat, Color teamColor, float groundZ)
        {
            GameObject go;
            if (prefab != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.position = pos;
            }
            else
            {
                go = new GameObject(name);
                go.transform.position = pos;
            }
            go.name = name;
            go.transform.localScale = Vector3.one * scale;
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            var vis = go.GetComponent<SailorRunnerVisual>();
            if (vis != null)
            {
                vis.TiltTowardsCamera = 0f;
                vis.SnapFacing(Vector3.up);
                vis.SetupTeamRing(teamColor, groundZ);
                vis.SetRingVisible(true);
                vis.SetupFakeShadow(groundZ);
            }

            ApplySailorPermanentMaterials(go, teamMat, stripeMat, teamColor);
            EditorUtility.SetDirty(go);
            return go;
        }

        private static void ApplySailorPermanentMaterials(GameObject sailor, Material teamMat, Material stripeMat, Color teamColor)
        {
            if (sailor == null) return;

            foreach (var r in sailor.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is SpriteRenderer) continue;
                if (r.name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

                if (r.name.IndexOf("Cap", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.name.IndexOf("Pompom", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.name.StartsWith("TeamColor", System.StringComparison.Ordinal))
                {
                    if (teamMat != null) r.sharedMaterial = teamMat;
                }
                else
                {
                    Material[] mats = r.sharedMaterials;
                    bool changed = false;
                    for (int m = 0; m < mats.Length; m++)
                    {
                        if (mats[m] == null) continue;
                        string sName = mats[m].name;

                        if (sName.StartsWith("M_TeamColor", System.StringComparison.Ordinal))
                        {
                            if (teamMat != null) mats[m] = teamMat;
                            changed = true;
                        }
                        else if (sName.StartsWith("M_Navy", System.StringComparison.Ordinal))
                        {
                            if (r.name == "Torso" || r.name == "ArmL" || r.name == "ArmR")
                            {
                                if (stripeMat != null) mats[m] = stripeMat;
                                changed = true;
                            }
                        }
                    }
                    if (changed) r.sharedMaterials = mats;
                }
                EditorUtility.SetDirty(r);
            }
        }
    }
}
