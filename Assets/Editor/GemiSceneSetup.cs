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
        private const string ScreenshotPath = "scratch/gemi_gameplay_view.png";
        private const string AutoRunKey = "GemiSceneSetup_AutoRun_v2";

        static GemiSceneSetup()
        {
            EditorApplication.delayCall += OnEditorDelayCall;
        }

        private static void OnEditorDelayCall()
        {
            if (SessionState.GetBool(AutoRunKey, false)) return;
            SessionState.SetBool(AutoRunKey, true);
            Setup(includeDemoModels: true);
            CaptureScreenshot();
        }

        [MenuItem("Tools/PixelGame/🏝️ Gemi Sahnesi Kurulumunu Yap (Setup Gemi Scene)", priority = 10)]
        public static void SetupGemiSceneMenu()
        {
            Setup(includeDemoModels: true);
            CaptureScreenshot();
            EditorUtility.DisplayDialog("Gemi Sahnesi Hazır!",
                "2D sabit arka plan, URP şeffaf gölge yakalayıcı zemin ve 3D gameplay katmanları başarıyla kuruldu!\n\n" +
                "Örnek 3D modeller [GAMEPLAY_MODELS] altında yerleştirildi. Kendi modellerinizi [Zone_Sand_PlayArea], [Zone_Wooden_Bridge] ve [Zone_Water_LowerArea] gruplarına serbestçe ekleyebilirsiniz.", "Harika");
        }

        [MenuItem("Tools/PixelGame/🏝️ Gemi Sahnesi Kur (Temiz - Demosuz)", priority = 11)]
        public static void SetupGemiSceneCleanMenu()
        {
            Setup(includeDemoModels: false);
            CaptureScreenshot();
            EditorUtility.DisplayDialog("Gemi Sahnesi Hazır (Temiz)",
                "2D sabit arka plan ve 3D katmanlar demosuz, tertemiz şekilde kuruldu!", "Tamam");
        }

        [MenuItem("Tools/PixelGame/📸 Gemi Sahnesi Ekran Görüntüsü Al", priority = 12)]
        public static void CaptureScreenshotMenu()
        {
            CaptureScreenshot();
            EditorUtility.DisplayDialog("Ekran Görüntüsü Alındı",
                $"Ekran görüntüsü kaydedildi:\n{ScreenshotPath}", "Tamam");
        }

        public static void Setup(bool includeDemoModels = true)
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath);
            }

            Undo.SetCurrentGroupName("Setup Gemi Scene");
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

            // 2. Işıklandırma (Directional Light) - Görseldeki doğal güneş açısıyla eşleşir
            Light dirLight = Object.FindFirstObjectByType<Light>();
            if (dirLight == null || dirLight.type != LightType.Directional)
            {
                GameObject lightObj = new GameObject("Directional Light");
                dirLight = lightObj.AddComponent<Light>();
                dirLight.type = LightType.Directional;
            }

            dirLight.transform.SetParent(null, true);
            dirLight.transform.position = new Vector3(-3f, 8f, -6f);
            // Sol-üstten hafif açıyla düşen sıcak gün ışığı:
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
                // Eski Canvas varsa adını güncelle
                GameObject oldCanvas = GameObject.Find("Canvas");
                if (oldCanvas != null) canvasObj = oldCanvas;
                else canvasObj = new GameObject("Background_Canvas");
            }
            canvasObj.name = "Background_Canvas";

            Canvas canvas = canvasObj.GetComponent<Canvas>();
            if (canvas == null) canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 35f; // 3D modellerin arkasında
            canvas.sortingOrder = -100; // En alt katman

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f; // Genişliğe göre eşle (mobil dikey tam dolum)

            if (canvasObj.GetComponent<GraphicRaycaster>() == null)
            {
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // RawImage temizliği ve ayarı
            RawImage[] existingRawImages = canvasObj.GetComponentsInChildren<RawImage>(true);
            RawImage activeRawImg = null;
            for (int i = 0; i < existingRawImages.Length; i++)
            {
                if (activeRawImg == null)
                {
                    activeRawImg = existingRawImages[i];
                    activeRawImg.gameObject.SetActive(true);
                }
                else
                {
                    Undo.DestroyObjectImmediate(existingRawImages[i].gameObject);
                }
            }

            if (activeRawImg == null)
            {
                GameObject rawObj = new GameObject("BackgroundImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                rawObj.transform.SetParent(canvasObj.transform, false);
                activeRawImg = rawObj.GetComponent<RawImage>();
            }
            activeRawImg.name = "BackgroundImage";
            activeRawImg.texture = bgTex;
            activeRawImg.raycastTarget = false; // Tıklamaları yutmaz, 3D modellere geçiş sağlar

            RectTransform rawRect = activeRawImg.rectTransform;
            rawRect.anchorMin = Vector2.zero;
            rawRect.anchorMax = Vector2.one;
            rawRect.offsetMin = Vector2.zero;
            rawRect.offsetMax = Vector2.zero;
            rawRect.localScale = Vector3.one;

            // 4. URP Şeffaf Gölge Yakalayıcı Zemin (Shadow Catcher)
            Material shadowMat = GetOrCreateShadowMaterial();
            GameObject shadowPlane = GameObject.Find("Ground_ShadowCatcher");
            if (shadowPlane == null)
            {
                shadowPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
                shadowPlane.name = "Ground_ShadowCatcher";
                Undo.RegisterCreatedObjectUndo(shadowPlane, "Create Ground Shadow Catcher");
            }

            // Collider gerekmez, tıklamaları engellemesin
            Collider col = shadowPlane.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            shadowPlane.transform.position = new Vector3(0f, 0f, 0.05f); // 3D modellerin hemen arkasında
            shadowPlane.transform.rotation = Quaternion.identity;
            shadowPlane.transform.localScale = new Vector3(14f, 24f, 1f); // Tüm ekranı kapsar

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

            // Bölgeleri oluştur
            Transform zoneSand = EnsureZone(gameplayRoot.transform, "[Zone_Sand_PlayArea]", new Vector3(0f, 3.85f, 0f));
            Transform zoneBridge = EnsureZone(gameplayRoot.transform, "[Zone_Wooden_Bridge]", new Vector3(0f, -0.32f, 0f));
            Transform zoneWater = EnsureZone(gameplayRoot.transform, "[Zone_Water_LowerArea]", new Vector3(0f, -4.80f, 0f));

            // 7. Demo Modelleri (Kullanıcı kendi modellerini ekleyebilsin diye örnek)
            Transform existingDemo = gameplayRoot.transform.Find("Demo_Showcase");
            if (existingDemo != null) Undo.DestroyObjectImmediate(existingDemo.gameObject);

            if (includeDemoModels)
            {
                SetupDemoShowcase(zoneSand, zoneWater);
            }

            // Sahneyi kaydet
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("<color=#00FFAA><b>[GemiSceneSetup]</b></color> Gemi sahnesi başarıyla yapılandırıldı ve kaydedildi!");
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
                // Yumuşak, sıcak-soğuk dengeli temas gölgesi rengi:
                mat.SetColor("_ShadowColor", new Color(0.04f, 0.08f, 0.16f, 0.42f));
            }

            string dir = Path.GetDirectoryName(ShadowMatPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            AssetDatabase.CreateAsset(mat, ShadowMatPath);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static void SetupDemoShowcase(Transform sandZone, Transform waterZone)
        {
            GameObject showcaseRoot = new GameObject("Demo_Showcase");
            Undo.RegisterCreatedObjectUndo(showcaseRoot, "Create Demo Showcase");
            showcaseRoot.transform.position = Vector3.zero;

            // 1. Kum alanına 3D küp/puzzle objesi
            string cubePrefabPath = "Assets/Prefabs/MainCube.prefab";
            GameObject cubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(cubePrefabPath);
            if (cubePrefab != null)
            {
                GameObject cubeInstance = (GameObject)PrefabUtility.InstantiatePrefab(cubePrefab, showcaseRoot.transform);
                cubeInstance.name = "Demo_SandCube";
                cubeInstance.transform.position = sandZone.position + new Vector3(0f, 0f, -0.2f);
                cubeInstance.transform.rotation = Quaternion.Euler(28f, -25f, 0f);
                cubeInstance.transform.localScale = Vector3.one * 1.6f;

                var mr = cubeInstance.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    mr.receiveShadows = true;
                }
            }

            // 2. Su alanına Kenney teknesi
            string boatPath = "Assets/Kenney/kenney_watercraft-pack/Models/FBX format/boat-speed-a.fbx";
            GameObject boatModel = AssetDatabase.LoadAssetAtPath<GameObject>(boatPath);
            if (boatModel != null)
            {
                GameObject boatInstance = (GameObject)PrefabUtility.InstantiatePrefab(boatModel, showcaseRoot.transform);
                boatInstance.name = "Demo_WaterBoat";
                boatInstance.transform.position = waterZone.position + new Vector3(0f, 0.4f, -0.3f);
                boatInstance.transform.rotation = Quaternion.Euler(32f, -145f, 0f);
                boatInstance.transform.localScale = Vector3.one * 1.8f;

                var renderers = boatInstance.GetComponentsInChildren<MeshRenderer>();
                foreach (var r in renderers)
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = true;
                }
            }
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
