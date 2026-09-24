using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class SetupCornerLauncherStation
    {
        private const string CasualUIDir = "Assets/UI/CasualUI";
        private const string FontPath = "Assets/Fonts/LilitaOne-Regular.ttf";

        private const string BasePlatePath = "Assets/UI/Corner_BasePlate.png";
        private const string LaunchButtonPath = "Assets/UI/Corner_LaunchButton.png";
        private const string NeonRingPath = "Assets/UI/Corner_NeonRing.png";
        private const string ShockwavePath = "Assets/UI/Corner_Shockwave.png";
        private const string JuicyPillPath = "Assets/UI/Badge_JuicyPill.png";

        static SetupCornerLauncherStation()
        {
            // Otomatik tetikleme kapatıldı: sahne editör açılışında değiştirilmesin.
            // Gerekirse Tools menüsünden elle çalıştırılır.
            // EditorApplication.delayCall += ApplyCornerLauncher;
        }

        // [MenuItem("Tools/PixelGame/🚀 Köşe Fırlatma İstasyonunu & Canlı Butonu Kur (Corner Launcher Setup)", priority = 3)]
        public static void ApplyCornerLauncher()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;

            // 1. Doku İçe Aktarım Ayarlarını Yapılandır
            ConfigureSprite(BasePlatePath);
            ConfigureSprite(LaunchButtonPath);
            ConfigureSprite(NeonRingPath);
            ConfigureSprite(ShockwavePath);
            ConfigureSprite(JuicyPillPath);
            AssetDatabase.SaveAssets();

            Sprite basePlateSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BasePlatePath);
            Sprite launchButtonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(LaunchButtonPath);
            Sprite neonRingSprite = AssetDatabase.LoadAssetAtPath<Sprite>(NeonRingPath);
            Sprite shockwaveSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShockwavePath);
            Sprite juicyPillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(JuicyPillPath);

            TMP_FontAsset lilitaTMP = SetupCasualTMPFont.GetOrCreateFontAsset();

            // 2. SlotRow altındaki TrackCornerSlot'u bul veya oluştur
            TruckSlotRow slotRow = Object.FindFirstObjectByType<TruckSlotRow>();
            if (slotRow == null)
            {
                Debug.LogWarning("[CornerLauncherSetup] Sahnede TruckSlotRow bulunamadı!");
                return;
            }

            Transform cornerSlotTr = slotRow.transform.Find("TrackCornerSlot");
            GameObject cornerSlotObj;
            if (cornerSlotTr == null)
            {
                cornerSlotObj = new GameObject("TrackCornerSlot", typeof(RectTransform));
                cornerSlotObj.transform.SetParent(slotRow.transform, false);
                cornerSlotTr = cornerSlotObj.transform;
            }
            else
            {
                cornerSlotObj = cornerSlotTr.gameObject;
            }

            // Ana Köşe Nesnesi Ayarları
            RectTransform csRt = cornerSlotObj.GetComponent<RectTransform>();
            if (csRt == null) csRt = cornerSlotObj.AddComponent<RectTransform>();
            csRt.anchorMin = new Vector2(0.5f, 0.5f);
            csRt.anchorMax = new Vector2(0.5f, 0.5f);
            csRt.pivot = new Vector2(0.5f, 0.5f);
            csRt.anchoredPosition = new Vector2(-885f, 650f);
            csRt.sizeDelta = new Vector2(190f, 190f);
            csRt.localScale = Vector3.one;

            // Eski Image veya MeshRenderer'ları temizle
            Image mainImg = cornerSlotObj.GetComponent<Image>();
            if (mainImg != null)
            {
                mainImg.color = Color.clear;
                mainImg.raycastTarget = false;
            }

            // Alt nesneleri temizle veya yapılandır
            for (int i = cornerSlotTr.childCount - 1; i >= 0; i--)
            {
                Transform ch = cornerSlotTr.GetChild(i);
                Object.DestroyImmediate(ch.gameObject);
            }

            // ========================================================
            // 1. Altlık / Gövde Plakası (BasePlate)
            // ========================================================
            GameObject basePlateObj = new GameObject("BasePlate", typeof(RectTransform), typeof(Image));
            basePlateObj.transform.SetParent(cornerSlotTr, false);
            RectTransform bpRt = basePlateObj.GetComponent<RectTransform>();
            bpRt.anchorMin = Vector2.zero;
            bpRt.anchorMax = Vector2.one;
            bpRt.sizeDelta = Vector2.zero;
            bpRt.anchoredPosition = Vector2.zero;

            Image bpImg = basePlateObj.GetComponent<Image>();
            bpImg.sprite = basePlateSprite;
            bpImg.type = Image.Type.Simple;
            bpImg.preserveAspect = true;
            bpImg.color = Color.white;
            bpImg.raycastTarget = false;

            // ========================================================
            // 2. Parlayan Neon Halka (NeonRing)
            // ========================================================
            GameObject neonRingObj = new GameObject("NeonGlowRing", typeof(RectTransform), typeof(Image));
            neonRingObj.transform.SetParent(cornerSlotTr, false);
            RectTransform nrRt = neonRingObj.GetComponent<RectTransform>();
            nrRt.anchorMin = new Vector2(0.12f, 0.16f);
            nrRt.anchorMax = new Vector2(0.88f, 0.92f);
            nrRt.sizeDelta = Vector2.zero;
            nrRt.anchoredPosition = Vector2.zero;

            Image nrImg = neonRingObj.GetComponent<Image>();
            nrImg.sprite = neonRingSprite;
            nrImg.type = Image.Type.Simple;
            nrImg.preserveAspect = true;
            nrImg.color = new Color(0f, 0.9f, 1f, 0.75f);
            nrImg.raycastTarget = false;

            // ========================================================
            // 3. Genişleyen Şok Dalgası (Shockwave)
            // ========================================================
            GameObject shockObj = new GameObject("ShockwaveRipple", typeof(RectTransform), typeof(Image));
            shockObj.transform.SetParent(cornerSlotTr, false);
            RectTransform swRt = shockObj.GetComponent<RectTransform>();
            swRt.anchorMin = new Vector2(0.10f, 0.14f);
            swRt.anchorMax = new Vector2(0.90f, 0.94f);
            swRt.sizeDelta = Vector2.zero;
            swRt.anchoredPosition = Vector2.zero;

            Image swImg = shockObj.GetComponent<Image>();
            swImg.sprite = shockwaveSprite;
            swImg.type = Image.Type.Simple;
            swImg.preserveAspect = true;
            swImg.color = new Color(1f, 1f, 1f, 0f);
            swImg.raycastTarget = false;
            shockObj.SetActive(false);

            // ========================================================
            // 4. 3B Parlak Yaylı Buton (LaunchButton)
            // ========================================================
            GameObject btnObj = new GameObject("LaunchButtonPad", typeof(RectTransform), typeof(Image));
            btnObj.transform.SetParent(cornerSlotTr, false);
            RectTransform btnRt = btnObj.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.16f, 0.20f);
            btnRt.anchorMax = new Vector2(0.84f, 0.88f);
            btnRt.sizeDelta = Vector2.zero;
            btnRt.anchoredPosition = Vector2.zero;

            Image btnImg = btnObj.GetComponent<Image>();
            btnImg.sprite = launchButtonSprite;
            btnImg.type = Image.Type.Simple;
            btnImg.preserveAspect = true;
            btnImg.color = Color.white;
            btnImg.raycastTarget = true;

            // ========================================================
            // 5. Şık 3B Sayaç Rozeti (CounterBadge - 0/4, 2/5)
            // ========================================================
            GameObject badgeObj = new GameObject("CounterBadge", typeof(RectTransform), typeof(Image));
            badgeObj.transform.SetParent(cornerSlotTr, false);
            RectTransform badgeRt = badgeObj.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(0.5f, 0f);
            badgeRt.anchorMax = new Vector2(0.5f, 0f);
            badgeRt.pivot = new Vector2(0.5f, 0.5f);
            badgeRt.anchoredPosition = new Vector2(0f, 8f);
            badgeRt.sizeDelta = new Vector2(125f, 52f);

            Image badgeImg = badgeObj.GetComponent<Image>();
            badgeImg.sprite = juicyPillSprite;
            badgeImg.type = Image.Type.Simple;
            badgeImg.preserveAspect = true;
            badgeImg.color = Color.white;
            badgeImg.raycastTarget = false;

            // 5b. Sayaç Metni (LilitaOne TMP)
            GameObject ctObj = new GameObject("CounterText", typeof(RectTransform), typeof(TextMeshProUGUI));
            ctObj.transform.SetParent(badgeObj.transform, false);
            RectTransform ctRt = ctObj.GetComponent<RectTransform>();
            ctRt.anchorMin = Vector2.zero;
            ctRt.anchorMax = Vector2.one;
            ctRt.pivot = new Vector2(0.5f, 0.5f);
            ctRt.anchoredPosition = new Vector2(0f, 1f);
            ctRt.sizeDelta = Vector2.zero;

            TextMeshProUGUI counterTmp = ctObj.GetComponent<TextMeshProUGUI>();
            if (lilitaTMP != null) counterTmp.font = lilitaTMP;
            counterTmp.fontSize = 32;
            counterTmp.fontStyle = FontStyles.Bold;
            counterTmp.color = Color.white;
            counterTmp.alignment = TextAlignmentOptions.Center;
            counterTmp.text = "0/4";
            counterTmp.raycastTarget = false;

            // ========================================================
            // 6. TrackCornerLauncher Bileşenini Yapılandır
            // ========================================================
            TrackCornerLauncher launcher = cornerSlotObj.GetComponent<TrackCornerLauncher>();
            if (launcher == null) launcher = cornerSlotObj.AddComponent<TrackCornerLauncher>();

            SerializedObject so = new SerializedObject(launcher);
            so.FindProperty("m_BasePlate").objectReferenceValue = bpImg;
            so.FindProperty("m_NeonRing").objectReferenceValue = nrImg;
            so.FindProperty("m_ButtonTransform").objectReferenceValue = btnRt;
            so.FindProperty("m_ButtonImage").objectReferenceValue = btnImg;
            so.FindProperty("m_ShockwaveImage").objectReferenceValue = swImg;
            so.FindProperty("m_CounterBadgeRect").objectReferenceValue = badgeRt;
            so.FindProperty("m_CounterTMP").objectReferenceValue = counterTmp;
            so.ApplyModifiedProperties();

            // 7. TruckDispatcher Bağlantısını Güncelle
            TruckDispatcher dispatcher = Object.FindFirstObjectByType<TruckDispatcher>();
            if (dispatcher != null)
            {
                SerializedObject soDisp = new SerializedObject(dispatcher);
                SerializedProperty tmpProp = soDisp.FindProperty("m_TrackCornerCounterTMP");
                if (tmpProp != null)
                {
                    tmpProp.objectReferenceValue = counterTmp;
                    soDisp.ApplyModifiedProperties();
                }
                dispatcher.UpdateTrackCornerCounter();
                EditorUtility.SetDirty(dispatcher);
            }

            EditorUtility.SetDirty(cornerSlotObj);
            AssetDatabase.SaveAssets();

            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
            }

            Debug.Log("<color=#00FFAA><b>[CornerLauncherSetup]</b></color> Canlı animasyonlu köşe istasyonu ve yaylı 3B buton başarıyla kuruldu!");
        }

        private static void ConfigureSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (importer.alphaIsTransparency != true) { importer.alphaIsTransparency = true; dirty = true; }
            if (importer.mipmapEnabled != false) { importer.mipmapEnabled = false; dirty = true; }

            if (dirty)
            {
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
        }
    }
}
