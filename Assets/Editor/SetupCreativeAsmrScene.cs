using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using PixelGame.Creative;

namespace PixelGame.Editor
{
    public static class SetupCreativeAsmrScene
    {
        private const string ScenePath = "Assets/Scenes/Creative_ASMR.unity";
        private const string TabletopCubePrefabPath = "Assets/Prefabs/MainCube_Tabletop.prefab";
        private const string FallbackCubePrefabPath = "Assets/Prefabs/MainCube.prefab";
        private const string ShadowMatPath = "Assets/Resources/CubeContactShadow_Mat.mat";
        private const string SailorPrefabPath = "Assets/Resources/Sailor_Runner.prefab";
        private const string HarborBgPath = "Assets/Renkli Liman İskelesi ve Deniz.png";

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

        // 3 Takım Renkleri
        public static readonly Color PinkColor = new Color(1.00f, 0.22f, 0.44f, 1f);   // #FF3870 (Canlı Mercan Pembe)
        public static readonly Color YellowColor = new Color(1.00f, 0.82f, 0.00f, 1f); // #FFD100 (Altın Sarısı)
        public static readonly Color BlueColor = new Color(0.00f, 0.71f, 0.95f, 1f);   // #00B5F2 (Elektrik Gökyüzü Mavisi)

        // Orijinal oyun sahnesinden (Gemi.unity) birebir referans alınan orantılar:
        private const float CellSize = 0.28f;
        private const float SailorToCubeRatio = 2.4215f; // 1.45 * 1.67

        private static bool s_HasBuilt = false;

        [InitializeOnLoadMethod]
        private static void AutoRebuildOnce()
        {
            if (s_HasBuilt || EditorApplication.isPlayingOrWillChangePlaymode) return;
            s_HasBuilt = true;
            EditorApplication.delayCall += () =>
            {
                BuildAndOpenCreativeScene();
            };
        }

        [MenuItem("Tools/PixelGame/🎬 ASMR Kreatif Sahnesini Aç", priority = 0)]
        public static void OpenCreativeScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        [MenuItem("Tools/PixelGame/🎮 Normal Oyun Sahnesini Aç (Gemi)", priority = 1)]
        public static void OpenGameScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene("Assets/Scenes/Gemi.unity", OpenSceneMode.Single);
            }
        }

        [MenuItem("Tools/PixelGame/🎬 ASMR Kreatif Sahnesini Sıfırdan Yeniden Kur", priority = 2)]
        public static void BuildAndOpenCreativeScene()
        {
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

            // 3. Grid ve Başlangıç Koordinatları
            float groundZ = 0.42f;
            int totalCols = 5;
            int totalRows = 6;
            int laneRows = 2; // Her gemici 2 satır x 5 sütun = 10 küp toplar (Toplam 30 küp)
            float stepX = CellSize;
            float stepY = CellSize;
            float gridStartX = -((totalCols - 1) * stepX) * 0.5f; // -0.56f
            float gridStartY = -((totalRows - 1) * stepY) * 0.5f; // -0.70f

            // Kullanıcının çizdiği sol başlangıç noktası:
            float sailorStartX = gridStartX - 0.44f; // -1.00f

            // Kamera Hedef Merkezi (CameraCenterAnchor) - Gemicilerin sol başlangıç konumu hizasında
            GameObject centerAnchorGo = new GameObject("CameraCenterAnchor");
            centerAnchorGo.transform.position = new Vector3(sailorStartX, 0f, groundZ);

            // 4. 3rd Person Takip Kamerası (-20° Eğim & İzometrik Mod, 3.4f OrthoSize)
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.82f, 0.85f, 0.90f, 1f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 120f;
            cam.orthographic = true;
            cam.orthographicSize = 3.4f;

            camGo.AddComponent<AudioListener>();
            var urpCam = camGo.AddComponent<UniversalAdditionalCameraData>();
            if (urpCam != null) urpCam.renderShadows = true;

            float camZ = -19.425f;
            float deltaZ = groundZ - camZ;
            float tanTilt = Mathf.Tan(20f * Mathf.Deg2Rad);
            float initialCamY = 0f - deltaZ * tanTilt;

            camGo.transform.position = new Vector3(sailorStartX, initialCamY, camZ);
            camGo.transform.rotation = Quaternion.Euler(340f, 0f, 0f);

            var tpCam = camGo.AddComponent<CreativeThirdPersonCamera>();
            tpCam.Mode = CreativeThirdPersonCamera.CameraMode.GameIsometricFollow;
            tpCam.Target = centerAnchorGo.transform;
            tpCam.ApplyCameraSettings();

            // 5. Arkaplan Canvas
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
            feedback.AsmrUsePureProceduralSound = false;
            feedback.AsmrPickupVolume = 0.35f;
            feedback.LiftoffSoundDebounceTime = 0.032f;

            // 8. KÜP IZGARASI (YATAY KULVARLAR: Üstte Pembe, Ortada Sarı, Altta Mavi)
            GameObject gridRoot = new GameObject("[Pixel_Cubes_Grid_30]");
            GameObject cubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TabletopCubePrefabPath);
            if (cubePrefab == null)
            {
                cubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FallbackCubePrefabPath);
            }

            Material matPink = AssetDatabase.LoadAssetAtPath<Material>(CubeMatPinkPath);
            Material matYellow = AssetDatabase.LoadAssetAtPath<Material>(CubeMatYellowPath);
            Material matBlue = AssetDatabase.LoadAssetAtPath<Material>(CubeMatBluePath);
            Material shadowMat = AssetDatabase.LoadAssetAtPath<Material>(ShadowMatPath);

            var laneConfigs = new[]
            {
                new { Name = "Pink",   Color = PinkColor,   Mat = matPink,   StartRow = 4, CenterY =  0.56f },
                new { Name = "Yellow", Color = YellowColor, Mat = matYellow, StartRow = 2, CenterY =  0.00f },
                new { Name = "Blue",   Color = BlueColor,   Mat = matBlue,   StartRow = 0, CenterY = -0.56f }
            };

            var sailorsData = new List<CreativeSailorData>();

            for (int l = 0; l < laneConfigs.Length; l++)
            {
                var cfg = laneConfigs[l];
                PixelCube[,] laneMatrix = new PixelCube[laneRows, totalCols];

                for (int r = 0; r < laneRows; r++)
                {
                    int globalRow = cfg.StartRow + r;
                    for (int c = 0; c < totalCols; c++)
                    {
                        Vector3 pos = new Vector3(gridStartX + c * stepX, gridStartY + globalRow * stepY, groundZ);

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
                            cubeGo.transform.localScale = Vector3.one * CellSize;
                        }

                        cubeGo.name = $"PixelCube_{cfg.Name}_R{globalRow}_C{c}";

                        for (int i = 0; i < cubeGo.transform.childCount; i++)
                        {
                            Transform ch = cubeGo.transform.GetChild(i);
                            if (ch != null && ch.name.StartsWith("LegsMount")) ch.gameObject.SetActive(false);
                        }

                        MeshRenderer mr = cubeGo.GetComponent<MeshRenderer>();
                        if (mr != null && cfg.Mat != null)
                        {
                            mr.sharedMaterial = cfg.Mat;
                        }

                        PixelCube pixelCube = cubeGo.GetComponent<PixelCube>();
                        if (pixelCube == null) pixelCube = cubeGo.AddComponent<PixelCube>();

                        pixelCube.Initialize(c, globalRow, cfg.Color, 0f);
                        pixelCube.ApplyColor(cfg.Color, 0f);

                        if (shadowMat != null)
                        {
                            pixelCube.EnsureShadow(shadowMat, new Vector2(0f, -0.45f), 1f, new Color(0.04f, 0.06f, 0.14f, 0.6f), 1.4f);
                        }
                        pixelCube.SetBodyCastsRealtimeShadow(false);

                        PrefabUtility.UnpackPrefabInstance(cubeGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                        EditorUtility.SetDirty(cubeGo);
                        EditorUtility.SetDirty(pixelCube);

                        laneMatrix[r, c] = pixelCube;
                    }
                }

                // 3. FOTOĞRAFTAKİ İŞARETLİ YERDEN SOLDAN SAĞA DOĞRU TOPLAMA SIRASI (Col 0'dan Col 4'e):
                List<PixelCube> orderedLaneCubes = new List<PixelCube>();
                for (int c = 0; c < totalCols; c++) // 0, 1, 2, 3, 4 (Soldan sağa)
                {
                    if (c % 2 == 0)
                    {
                        for (int r = laneRows - 1; r >= 0; r--) orderedLaneCubes.Add(laneMatrix[r, c]);
                    }
                    else
                    {
                        for (int r = 0; r < laneRows; r++) orderedLaneCubes.Add(laneMatrix[r, c]);
                    }
                }

                // 9. GEMİCİ KARAKTERİ (SOLDAKİ İŞARETLİ ALANDAN BAŞLAR, SAĞA DOĞRU KOŞAR)
                GameObject sailorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SailorPrefabPath);
                GameObject sailorGo;
                Vector3 sailorStartPos = new Vector3(sailorStartX, cfg.CenterY, groundZ);

                if (sailorPrefab != null)
                {
                    sailorGo = (GameObject)PrefabUtility.InstantiatePrefab(sailorPrefab);
                    sailorGo.transform.position = sailorStartPos;
                }
                else
                {
                    sailorGo = new GameObject($"Sailor_{cfg.Name}");
                    sailorGo.transform.position = sailorStartPos;
                }
                sailorGo.name = $"Sailor_{cfg.Name}";
                sailorGo.transform.localScale = Vector3.one * (CellSize * SailorToCubeRatio);

                PrefabUtility.UnpackPrefabInstance(sailorGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                var sailorVis = sailorGo.GetComponent<SailorRunnerVisual>();
                if (sailorVis != null)
                {
                    // Sağa doğru (küplere) bakış
                    sailorVis.SnapFacing(Vector3.right);
                    sailorVis.SetupTeamRing(cfg.Color, groundZ);
                    sailorVis.SetRingVisible(true);
                    sailorVis.SetupFakeShadow(groundZ);
                }

                string teamMatPath = l == 0 ? SailorTeamPinkPath : (l == 1 ? SailorTeamYellowPath : SailorTeamBluePath);
                string stripeMatPath = l == 0 ? SailorStripesPinkPath : (l == 1 ? SailorStripesYellowPath : SailorStripesBluePath);
                Material sailorTeamMat = AssetDatabase.LoadAssetAtPath<Material>(teamMatPath);
                Material sailorStripeMat = AssetDatabase.LoadAssetAtPath<Material>(stripeMatPath);

                ApplySailorPermanentMaterials(sailorGo, sailorTeamMat, sailorStripeMat, cfg.Color);
                EditorUtility.SetDirty(sailorGo);

                var sData = new CreativeSailorData
                {
                    Name = cfg.Name,
                    SailorObject = sailorGo,
                    TeamColor = cfg.Color,
                    AssignedCubes = orderedLaneCubes,
                    Visual = sailorVis,
                    InitialPosition = sailorStartPos
                };
                sailorsData.Add(sData);
            }

            // 10. Sahne Yöneticisi (CreativeAsmrSceneController)
            GameObject managerGo = new GameObject("[CREATIVE_ASMR_MANAGER]");
            CreativeAsmrSceneController controller = managerGo.AddComponent<CreativeAsmrSceneController>();
            controller.Camera = tpCam;
            controller.CameraCenterAnchor = centerAnchorGo.transform;
            controller.Sailors.AddRange(sailorsData);
            controller.CacheInitialCubeData();

            EditorUtility.SetDirty(managerGo);

            // 11. Sahneyi kaydet
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log("<color=#00FFAA><b>[Creative ASMR Scene]</b></color> 3 Gemici soldan sağa doğru toplayacak şekilde sahne başarıyla kuruldu: <b>Assets/Scenes/Creative_ASMR.unity</b>!");
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
