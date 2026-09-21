using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGame.Editor
{
    /// <summary>
    /// Ekranın arkaplanını ve üst HUD şeridini (ayarlar, seviye, can, altın) kurar —
    /// Simulator önizlemesindeki gibi. CasualUI paketindeki gerçek sprite'ları kullanır.
    ///
    /// Not: "top_bar_pod.png" tek başına kullanıma hazır bir arkaplan değildir; içine
    /// "Level 25 / ❤3 / 🪙22K" örnek metni gömülmüş, sabit bir tasarım referansı
    /// (mockup) görselidir. Gerçek HUD, her biri kendi ikon+metnini taşıyan
    /// "ui_pill.png" örnekleriyle kurulur; top_bar_pod'a hiç dokunulmaz.
    ///
    /// Idempotent'tir: birden fazla çalıştırılırsa önce kendi ürettiği nesneleri
    /// temizleyip yeniden kurar, kopya üretmez.
    /// </summary>
    public static class SetupCasualHud
    {
        private const string k_UIRoot = "Assets/UI/CasualUI/";
        private const string k_FontPath = "Assets/Fonts/LilitaOne-Regular SDF.asset";

        [MenuItem("Tools/PixelGame/🖼️ Casual HUD & Arkaplan Kur", priority = 40)]
        public static void Apply()
        {
            Canvas hudCanvas = FindOrCleanupHudCanvas();
            if (hudCanvas == null)
            {
                EditorUtility.DisplayDialog("HUD Canvas Bulunamadı",
                    "Sahnede \"TopUI\" alt nesnesi olan bir Canvas bulunamadı. " +
                    "Önce mevcut HUD Canvas'ı (SettingsButton'ı barındıran) kontrol et.", "Tamam");
                return;
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_FontPath);

            // Önceki (hatalı) sürümde arkaplan HUD Canvas'ının içine kuruluyordu; kalıntı varsa temizle.
            Transform staleBg = hudCanvas.transform.Find("Background");
            if (staleBg != null) Object.DestroyImmediate(staleBg.gameObject);

            BuildBackground(hudCanvas.worldCamera != null ? hudCanvas.worldCamera : Camera.main);
            Transform topUI = hudCanvas.transform.Find("TopUI");
            BuildStatsRow(topUI, font);
            WireController(hudCanvas, topUI);

            EditorUtility.SetDirty(hudCanvas.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log("<color=#00FFB4><b>[PixelGame]</b></color> 🖼️ Casual HUD ve arkaplan kuruldu: " +
                      "koyu lacivert arkaplan + LEVEL/can/altın şeridi.");
        }

        /// <summary>
        /// Önceki oturumlarda kazara 4 kez üretilmiş "Canvas" kopyalarını temizler,
        /// gerçek HUD'u taşıyan tek Canvas'ı döndürür.
        /// </summary>
        private static Canvas FindOrCleanupHudCanvas()
        {
            List<Canvas> candidates = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(c => c.name == "Canvas" && c.transform.Find("TopUI") != null)
                .OrderBy(c => c.transform.GetSiblingIndex())
                .ToList();

            if (candidates.Count == 0) return null;

            Canvas keeper = candidates[0];
            for (int i = 1; i < candidates.Count; i++)
            {
                Debug.LogWarning("<color=#FFB400><b>[PixelGame]</b></color> Kopya HUD Canvas siliniyor: " +
                                  candidates[i].gameObject.GetInstanceID());
                Object.DestroyImmediate(candidates[i].gameObject);
            }

            return keeper;
        }

        /// <summary>
        /// Tam ekran, kaydırılmış (vignette) koyu lacivert arkaplanı kurar.
        ///
        /// KENDİ Canvas'ında kurulur, HUD Canvas'ının (TopUI) içinde DEĞİL. Sebebi:
        /// Screen Space - Camera modunda bir Canvas'ın tüm içeriği tek bir düzlemde
        /// (planeDistance) durur ve bu düzlem, kameranın gerçek derinlik arabelleğine
        /// (depth buffer) göre 3B sahneyle yarışır. HUD Canvas'ı planeDistance=5'te;
        /// tahta (~12.8 birim) ve SlotCanvas (~11.4 birim) ondan çok daha uzakta.
        /// Arkaplan aynı Canvas'a eklenseydi HUD ile aynı düzlemde durur ve önündeki
        /// hiçbir 3B nesne görünmezdi (tam olarak yaşanan buydu — arkaplan her şeyi
        /// kapatıyordu). Ayrı bir Canvas'ta çok daha büyük bir planeDistance vermek,
        /// arkaplanı tahtanın ve rayların GERİSİNE, olması gereken yere koyar.
        /// </summary>
        private static void BuildBackground(Camera hudCamera)
        {
            const string k_Name = "BackgroundCanvas";
            const float k_PlaneDistance = 30f; // tahta (~12.8) ve SlotCanvas'tan (~11.4) kasıtlı olarak çok uzakta

            GameObject existing = GameObject.Find(k_Name);
            if (existing != null) Object.DestroyImmediate(existing);

            GameObject canvasGO = new GameObject(k_Name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = hudCamera;
            canvas.planeDistance = k_PlaneDistance;

            // Canvas sıralaması: birden fazla Canvas aynı (Transparent) render kuyruğunu
            // paylaştığında Unity çizim sırasını GERÇEK 3B mesafeye göre değil,
            // Canvas.sortingOrder'a göre belirler. planeDistance=30 ile arkaplanı
            // kameradan gerçekten en uzağa koymak YETMEDİ: SlotCanvas'ın sortingOrder'ı
            // -10 olduğu için (0 > -10) arkaplan yine SlotCanvas'ın (dolayısıyla
            // slotların ve köşe rozetinin) ÜSTÜNE çiziliyor, onları tamamen gizliyordu.
            // Sahnedeki en düşük sortingOrder'ın altına inerek bunu kalıcı olarak önlüyoruz.
            int lowestExistingOrder = 0;
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c == canvas) continue;
                if (c.sortingOrder < lowestExistingOrder) lowestExistingOrder = c.sortingOrder;
            }
            canvas.sortingOrder = lowestExistingOrder - 10;

            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvasGO.transform, false);

            RectTransform rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = bg.GetComponent<Image>();
            img.sprite = LoadSprite("bg_dark_navy");
            img.type = Image.Type.Simple;
            img.raycastTarget = false; // arkaplan tıklama almasın
            img.color = Color.white;
        }

        /// <summary>
        /// TopUI'nin eski (top_bar_pod ile yanlış gerilmiş) "HeaderCapsule"ını söker,
        /// yerine sağa yaslı LEVEL metni + can pili + altın pilinden oluşan satırı kurar.
        /// </summary>
        private static void BuildStatsRow(Transform topUI, TMP_FontAsset font)
        {
            Transform oldCapsule = topUI.Find("HeaderCapsule");
            if (oldCapsule != null) Object.DestroyImmediate(oldCapsule.gameObject);

            Transform existingRow = topUI.Find("StatsRow");
            if (existingRow != null) Object.DestroyImmediate(existingRow.gameObject);

            GameObject row = new GameObject("StatsRow", typeof(RectTransform));
            row.transform.SetParent(topUI, false);

            RectTransform rowRT = row.GetComponent<RectTransform>();
            rowRT.anchorMin = new Vector2(1f, 0.5f);
            rowRT.anchorMax = new Vector2(1f, 0.5f);
            rowRT.pivot = new Vector2(1f, 0.5f);
            rowRT.anchoredPosition = new Vector2(-40f, 0f);
            rowRT.sizeDelta = new Vector2(200f, 140f); // ContentSizeFitter genişliği içerikten alacak

            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.spacing = 56f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = row.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            GameObject levelLabel = BuildLevelLabel(row.transform, font);
            GameObject heartPill = BuildStatPill(row.transform, "HeartPill", "icon_heart", "3", font);
            GameObject coinPill = BuildStatPill(row.transform, "CoinPill", "icon_coin", "250", font);

            // HorizontalLayoutGroup soldan sağa dizer; StatsRow sağa yaslı olduğu için
            // sıralama LEVEL -> Can -> Altın, tıpkı referanstaki gibi soldan sağa okunur.
        }

        private static GameObject BuildLevelLabel(Transform parent, TMP_FontAsset font)
        {
            GameObject go = new GameObject("LevelLabel", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(220f, 90f);

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = "LEVEL 1";
            if (font != null) tmp.font = font;
            tmp.fontSize = 52f;
            tmp.alignment = TextAlignmentOptions.MidlineRight;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.fontStyle = FontStyles.Normal;

            // Koyu arkaplan üstünde okunaklılık için ince dış çizgi (outline)
            tmp.outlineWidth = 0.15f;
            tmp.outlineColor = new Color32(20, 26, 56, 255);

            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 220f;
            le.preferredHeight = 90f;

            return go;
        }

        /// <summary>
        /// "ui_pill" arkaplanı üstünde, sol kenardan taşan ikon + ortalanmış sayı +
        /// sağ kenardan taşan (+) düğmesinden oluşan tek bir istatistik pili kurar.
        /// </summary>
        private static GameObject BuildStatPill(Transform parent, string name, string iconSprite, string initialText, TMP_FontAsset font)
        {
            const float pillWidth = 190f;
            const float pillHeight = 78f;

            GameObject pill = new GameObject(name, typeof(RectTransform), typeof(Image));
            pill.transform.SetParent(parent, false);

            RectTransform pillRT = pill.GetComponent<RectTransform>();
            pillRT.sizeDelta = new Vector2(pillWidth, pillHeight);

            Image pillImg = pill.GetComponent<Image>();
            pillImg.sprite = LoadSprite("ui_pill");
            pillImg.type = Image.Type.Simple;
            pillImg.raycastTarget = false;

            LayoutElement pillLE = pill.AddComponent<LayoutElement>();
            pillLE.preferredWidth = pillWidth;
            pillLE.preferredHeight = pillHeight;

            // İkon: pilin SOL kenarına ortalanmış, pilden biraz büyük (taşma efekti)
            const float iconSize = 84f;
            GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(pill.transform, false);
            RectTransform iconRT = icon.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0f, 0.5f);
            iconRT.anchorMax = new Vector2(0f, 0.5f);
            iconRT.pivot = new Vector2(0.5f, 0.5f);
            // Pilin sol kenarından biraz TAŞAR (0 tam kenarda, negatif tamamen içeri
            // alır); tamamen yarı-yarıya taşırsak bitişik pilin ikonuyla çakışıyordu.
            iconRT.anchoredPosition = new Vector2(14f, 0f);
            iconRT.sizeDelta = new Vector2(iconSize, iconSize);
            Image iconImg = icon.GetComponent<Image>();
            iconImg.sprite = LoadSprite(iconSprite);
            iconImg.raycastTarget = false;

            // Sayı metni: ikonla (+) düğmesi arasında, ortalanmış
            GameObject text = new GameObject("Count", typeof(RectTransform));
            text.transform.SetParent(pill.transform, false);
            RectTransform textRT = text.GetComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0f, 0f);
            textRT.anchorMax = new Vector2(1f, 1f);
            // İkon [-28, 56] aralığında görünür (14 ofset ile), (+) düğmesi [158, 190] aralığında;
            // metin ikisinin arasında kalan [64, 150] bandına ortalanır.
            textRT.offsetMin = new Vector2(64f, 0f);
            textRT.offsetMax = new Vector2(-40f, 0f);
            TextMeshProUGUI textTMP = text.AddComponent<TextMeshProUGUI>();
            textTMP.text = initialText;
            if (font != null) textTMP.font = font;
            textTMP.fontSize = 42f;
            textTMP.alignment = TextAlignmentOptions.Center;
            textTMP.color = Color.white;
            textTMP.textWrappingMode = TextWrappingModes.NoWrap;
            textTMP.overflowMode = TextOverflowModes.Overflow;

            // (+) düğmesi: pilin SAĞ kenarına ortalanmış
            const float plusSize = 44f;
            GameObject plus = new GameObject("PlusButton", typeof(RectTransform), typeof(Image));
            plus.transform.SetParent(pill.transform, false);
            RectTransform plusRT = plus.GetComponent<RectTransform>();
            plusRT.anchorMin = new Vector2(1f, 0.5f);
            plusRT.anchorMax = new Vector2(1f, 0.5f);
            plusRT.pivot = new Vector2(0.5f, 0.5f);
            plusRT.anchoredPosition = new Vector2(-10f, 0f);
            plusRT.sizeDelta = new Vector2(plusSize, plusSize);
            Image plusImg = plus.GetComponent<Image>();
            plusImg.sprite = LoadSprite("btn_plus");
            plus.AddComponent<Button>();
            plus.AddComponent<CasualUIButtonJuice>();

            return pill;
        }

        /// <summary>HUD Canvas'ına <see cref="CasualHudController"/>'ı ekler ve TMP alanlarını bağlar.</summary>
        private static void WireController(Canvas hudCanvas, Transform topUI)
        {
            CasualHudController controller = hudCanvas.GetComponent<CasualHudController>();
            if (controller == null) controller = hudCanvas.gameObject.AddComponent<CasualHudController>();

            Transform statsRow = topUI.Find("StatsRow");
            controller.LevelText = statsRow.Find("LevelLabel").GetComponent<TextMeshProUGUI>();
            controller.LivesText = statsRow.Find("HeartPill/Count").GetComponent<TextMeshProUGUI>();
            controller.CoinsText = statsRow.Find("CoinPill/Count").GetComponent<TextMeshProUGUI>();
        }

        private static Sprite LoadSprite(string fileNameWithoutExtension)
        {
            string path = k_UIRoot + fileNameWithoutExtension + ".png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError($"<color=#FF4444><b>[PixelGame]</b></color> Sprite bulunamadı: {path}");
            }
            return sprite;
        }
    }
}
