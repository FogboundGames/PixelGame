using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor.SceneManagement;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class SetupBackgroundUI
    {
        private const string SessionKey = "SetupBackgroundUI_AutoRunDone_v2";

        static SetupBackgroundUI()
        {
            // Auto-run devre dışı bırakıldı (Mavi çerçeve ve küplerin üzerine gölge binmesini engellemek için)
            // EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            if (SessionState.GetBool(SessionKey, false))
                return;

            SessionState.SetBool(SessionKey, true);
            ExecuteSetup(isAuto: true);
        }

        [MenuItem("Tools/PixelGame/Arka Planı ve Fake Shadow'u Otomatik Kur")]
        public static void SetupManual()
        {
            ExecuteSetup(isAuto: false);
        }

        [MenuItem("Tools/PixelGame/Sadece Fake Shadow Ekle veya Seç")]
        public static void SetupShadowOnly()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                ExecuteSetup(isAuto: false);
                return;
            }

            Transform bgTransform = canvas.transform.Find("Background");
            if (bgTransform == null)
            {
                ExecuteSetup(isAuto: false);
                return;
            }

            GameObject shadowObj = EnsureFakeShadow(bgTransform.gameObject);
            Selection.activeGameObject = shadowObj;
            EditorUtility.DisplayDialog("Fake Shadow Hazır", "FakeShadow nesnesi seçildi! Inspector panelinden gölge rengini, kalınlığını ve yumuşaklığını canlı olarak ayarlayabilirsiniz.", "Tamam");
        }

        private static void ExecuteSetup(bool isAuto)
        {
            // 1. Hatalı eski 3D Background nesnesini temizle (UI Image olmayan 3D Quad/Plane)
            GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var go in allObjects)
            {
                if (go.name == "Background" && go.GetComponent<Image>() == null)
                {
                    Undo.DestroyObjectImmediate(go);
                    Debug.Log("<color=yellow>[PixelGame]</color> Hatalı 3D Background nesnesi temizlendi.");
                }
            }

            // 2. Boş veya yetim kalmış eski Canvas'ları temizle
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c.transform.childCount == 0)
                {
                    Undo.DestroyObjectImmediate(c.gameObject);
                }
            }

            // 3. UI Canvas oluştur veya var olanı al
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            GameObject canvasObj;
            if (canvas == null)
            {
                canvasObj = new GameObject("Canvas");
                Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
            }
            else
            {
                canvasObj = canvas.gameObject;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            // 4. EventSystem yoksa ekle
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(esObj, "Create EventSystem");
                esObj.AddComponent<EventSystem>();

                var inputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (inputModuleType != null)
                {
                    esObj.AddComponent(inputModuleType);
                }
                else
                {
                    esObj.AddComponent<StandaloneInputModule>();
                }
            }

            // 5. Assets/UI içerisindeki Sprite görselini bul
            string imagePath = "Assets/UI/Gemini_Generated_Image_1pnet1pnet1pnet1.jpg";
            Sprite bgSprite = null;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(imagePath);
            foreach (var asset in assets)
            {
                if (asset is Sprite s)
                {
                    bgSprite = s;
                    break;
                }
            }

            if (bgSprite == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/UI" });
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                    foreach (var sub in subAssets)
                    {
                        if (sub is Sprite s)
                        {
                            bgSprite = s;
                            break;
                        }
                    }
                    if (bgSprite != null) break;
                }
            }

            // 6. Canvas altına Background (UI Image) oluştur
            Transform bgTransform = canvasObj.transform.Find("Background");
            GameObject bgObj;
            if (bgTransform == null)
            {
                bgObj = new GameObject("Background");
                Undo.RegisterCreatedObjectUndo(bgObj, "Create Background Image");
                bgObj.transform.SetParent(canvasObj.transform, false);
            }
            else
            {
                bgObj = bgTransform.gameObject;
            }

            Image img = bgObj.GetComponent<Image>();
            if (img == null)
            {
                img = bgObj.AddComponent<Image>();
            }

            img.sprite = bgSprite;
            img.color = Color.white;
            img.type = Image.Type.Simple;
            img.raycastTarget = false;

            RectTransform rt = bgObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            // 7. FakeShadow (İç sahte gölge) nesnesini kur
            GameObject shadowObj = EnsureFakeShadow(bgObj);

            // 8. Sahne görünümünü 2D'ye al ve FakeShadow'u seç
            Selection.activeGameObject = shadowObj;

            SceneView sv = SceneView.lastActiveSceneView;
            if (sv != null)
            {
                sv.in2DMode = true;
                sv.FrameSelected();
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("<color=#00FF00><b>[PixelGame]</b></color> UI Arka Planı ve Fake Shadow başarıyla hazırlandı!");

            if (!isAuto)
            {
                EditorUtility.DisplayDialog("Hazır!", "Arka plan ve Fake Shadow başarıyla oluşturuldu!\n\nSağ taraftaki Inspector panelinden FakeShadow ayarlarını (kalınlık, yumuşaklık, renk) anında canlı olarak değiştirebilirsin.", "Tamam");
            }
        }

        private static GameObject EnsureFakeShadow(GameObject bgObj)
        {
            Transform shadowTransform = bgObj.transform.Find("FakeShadow");
            GameObject shadowObj;
            if (shadowTransform == null)
            {
                shadowObj = new GameObject("FakeShadow");
                Undo.RegisterCreatedObjectUndo(shadowObj, "Create FakeShadow");
                shadowObj.transform.SetParent(bgObj.transform, false);
            }
            else
            {
                shadowObj = shadowTransform.gameObject;
            }

            UIFakeShadow shadow = shadowObj.GetComponent<UIFakeShadow>();
            if (shadow == null)
            {
                shadow = shadowObj.AddComponent<UIFakeShadow>();
            }

            shadow.shapeMode = ShadowShape.PhoneWithNotch;
            shadow.enableNotch = true;
            shadow.notchWidth = 340f;
            shadow.notchHeight = 65f;
            shadow.notchRadius = 22f;
            shadow.cornerRadius = 50f;
            shadow.globalSize = 55f;
            shadow.falloff = 1.5f;

            // Simülatördeki aktif cihazın Safe Area'sına göre çentiği ve köşeleri otomatik uyarla
            shadow.AutoDetectNotchFromSafeArea();

            RectTransform srt = shadowObj.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            srt.pivot = new Vector2(0.5f, 0.5f);

            return shadowObj;
        }
    }
}
