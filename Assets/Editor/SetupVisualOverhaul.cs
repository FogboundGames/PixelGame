using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGame.Editor
{
    public static class SetupVisualOverhaul
    {
        private const string CasualUIDir = "Assets/UI/CasualUI";
        private const string FontPath = "Assets/Fonts/LilitaOne-Regular.ttf";

        [MenuItem("Tools/PixelGame/✨ Komple Görsel Dönüşümü Uygula (Complete Visual Overhaul)", priority = 1)]
        public static void ApplyOverhaul()
        {
            // 1. Sprite ayarlarını ve 9-slice sınırlarını yapılandır
            SetupCasualUISprites.ConfigureAll();

            Font lilitaFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (lilitaFont == null)
            {
                Debug.LogWarning("[VisualOverhaul] LilitaOne fontu bulunamadı, varsayılan sistem fontu kullanılacak.");
            }

            // 2. Ana Canvas ve SlotCanvas'ı bul
            Canvas overlayCanvas = null;
            Canvas slotCanvas = null;
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay) overlayCanvas = c;
                else if (c.gameObject.name.Contains("SlotCanvas") || c.renderMode == RenderMode.ScreenSpaceCamera) slotCanvas = c;
            }

            if (overlayCanvas == null)
            {
                GameObject ocObj = new GameObject("Canvas");
                overlayCanvas = ocObj.AddComponent<Canvas>();
                overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                ocObj.AddComponent<GraphicRaycaster>();
            }

            // 3. PHASE 2: 9:16 Fullscreen Kompozisyon (1080 x 1920)
            ConfigureCanvasScaler(overlayCanvas);
            if (slotCanvas != null) ConfigureCanvasScaler(slotCanvas);

            // 4. PHASE 3: Telefon Çerçevesini Kaldır & Koyu Lacivert Arka Plan Kur
            SetupBackgroundAndRemovePhoneBezel(overlayCanvas);

            // 5. PHASE 9: Modern Top UI Bar (Settings, Level, Lives, Coins)
            SetupTopUI(overlayCanvas, lilitaFont);

            // 6. PHASE 4, 5, 10, 11, 12: BoardFrame, InnerWell, 5 Slots, Progress Pod, Pool
            SetupBoardAndSlots(slotCanvas, lilitaFont);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#00FFAA><b>[VisualOverhaul]</b></color> Oyunun görsel kalitesi başarıyla en üst seviyeye taşındı!");
        }

        private static void ConfigureCanvasScaler(Canvas canvas)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f; // Dengeli 9:16 mobil ölçekleme
            EditorUtility.SetDirty(scaler);
        }

        private static void SetupBackgroundAndRemovePhoneBezel(Canvas canvas)
        {
            // Eski telefon çerçevesi (UIFakeShadow - PhoneWithNotch) nesnelerini temizle
            foreach (var fakeShadow in canvas.GetComponentsInChildren<UIFakeShadow>(true))
            {
                Object.DestroyImmediate(fakeShadow.gameObject);
            }

            // Background nesnesini bul veya oluştur
            Transform bgTr = canvas.transform.Find("Background");
            GameObject bgObj;
            if (bgTr == null)
            {
                bgObj = new GameObject("Background");
                bgObj.transform.SetParent(canvas.transform, false);
                bgObj.transform.SetAsFirstSibling();
            }
            else
            {
                bgObj = bgTr.gameObject;
                bgObj.transform.SetAsFirstSibling();
            }

            RectTransform rt = bgObj.GetComponent<RectTransform>();
            if (rt == null) rt = bgObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            Image img = bgObj.GetComponent<Image>();
            if (img == null) img = bgObj.AddComponent<Image>();

            Sprite bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CasualUIDir}/bg_dark_navy.png");
            if (bgSprite != null)
            {
                img.sprite = bgSprite;
                img.type = Image.Type.Simple;
                img.color = Color.white;
                img.raycastTarget = false;
            }

            EditorUtility.SetDirty(bgObj);
        }

        private static void SetupTopUI(Canvas canvas, Font font)
        {
            Transform existingTop = canvas.transform.Find("TopUI");
            GameObject topObj;
            if (existingTop != null)
            {
                topObj = existingTop.gameObject;
            }
            else
            {
                topObj = new GameObject("TopUI", typeof(RectTransform));
                topObj.transform.SetParent(canvas.transform, false);
            }

            RectTransform topRt = topObj.GetComponent<RectTransform>();
            topRt.anchorMin = new Vector2(0f, 1f);
            topRt.anchorMax = new Vector2(1f, 1f);
            topRt.pivot = new Vector2(0.5f, 1f);
            topRt.anchoredPosition = new Vector2(0f, -40f);
            topRt.sizeDelta = new Vector2(0f, 160f);

            Sprite btnSettingsSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CasualUIDir}/btn_settings.png");
            Sprite pillSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CasualUIDir}/ui_pill.png");
            Sprite heartSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CasualUIDir}/icon_heart.png");
            Sprite coinSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CasualUIDir}/icon_coin.png");
            Sprite plusSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CasualUIDir}/btn_plus.png");

            // 1. Settings Button (Sol)
            GameObject btnSettings = GetOrCreateChild(topObj, "SettingsButton");
            RectTransform btnRt = btnSettings.GetComponent<RectTransform>();
            btnRt.anchorMin = btnRt.anchorMax = btnRt.pivot = new Vector2(0f, 0.5f);
            btnRt.anchoredPosition = new Vector2(80f, 0f);
            btnRt.sizeDelta = new Vector2(96f, 96f);
            Image btnImg = EnsureComponent<Image>(btnSettings);
            btnImg.sprite = btnSettingsSprite;
            btnImg.type = Image.Type.Simple;
            Button btn = EnsureComponent<Button>(btnSettings);

            // 2. Level Badge (Orta)
            GameObject levelBadge = GetOrCreateChild(topObj, "LevelBadge");
            RectTransform lvlRt = levelBadge.GetComponent<RectTransform>();
            lvlRt.anchorMin = lvlRt.anchorMax = lvlRt.pivot = new Vector2(0.5f, 0.5f);
            lvlRt.anchoredPosition = new Vector2(0f, 0f);
            lvlRt.sizeDelta = new Vector2(340f, 80f);
            Text lvlText = EnsureComponent<Text>(levelBadge);
            lvlText.text = "LEVEL 1";
            if (font != null) lvlText.font = font;
            lvlText.fontSize = 52;
            lvlText.alignment = TextAnchor.MiddleCenter;
            lvlText.color = Color.white;
            Outline lvlOut = EnsureComponent<Outline>(levelBadge);
            lvlOut.effectColor = new Color32(22, 32, 58, 255);
            lvlOut.effectDistance = new Vector2(2.5f, -3f);
            Shadow lvlShadow = EnsureComponent<Shadow>(levelBadge);
            lvlShadow.effectColor = new Color32(8, 12, 24, 200);
            lvlShadow.effectDistance = new Vector2(0f, -4f);

            // 3. Lives Widget (Sağ - 1)
            GameObject livesWidget = GetOrCreateChild(topObj, "LivesWidget");
            RectTransform livesRt = livesWidget.GetComponent<RectTransform>();
            livesRt.anchorMin = livesRt.anchorMax = livesRt.pivot = new Vector2(1f, 0.5f);
            livesRt.anchoredPosition = new Vector2(-235f, 0f);
            livesRt.sizeDelta = new Vector2(135f, 66f);
            Image livesBg = EnsureComponent<Image>(livesWidget);
            livesBg.sprite = pillSprite;
            livesBg.type = Image.Type.Sliced;

            // Heart Icon
            GameObject heartObj = GetOrCreateChild(livesWidget, "Icon");
            RectTransform heartRt = heartObj.GetComponent<RectTransform>();
            heartRt.anchorMin = heartRt.anchorMax = heartRt.pivot = new Vector2(0f, 0.5f);
            heartRt.anchoredPosition = new Vector2(24f, 0f);
            heartRt.sizeDelta = new Vector2(40f, 40f);
            Image heartImg = EnsureComponent<Image>(heartObj);
            heartImg.sprite = heartSprite;
            heartImg.raycastTarget = false;

            // Lives Text
            GameObject livesTextObj = GetOrCreateChild(livesWidget, "Text");
            RectTransform ltRt = livesTextObj.GetComponent<RectTransform>();
            ltRt.anchorMin = ltRt.anchorMax = ltRt.pivot = new Vector2(0.5f, 0.5f);
            ltRt.anchoredPosition = new Vector2(8f, 0f);
            ltRt.sizeDelta = new Vector2(60f, 50f);
            Text ltText = EnsureComponent<Text>(livesTextObj);
            ltText.text = "3";
            if (font != null) ltText.font = font;
            ltText.fontSize = 32;
            ltText.alignment = TextAnchor.MiddleCenter;
            ltText.color = Color.white;
            Outline ltOut = EnsureComponent<Outline>(livesTextObj);
            ltOut.effectColor = new Color32(20, 26, 48, 255);
            ltOut.effectDistance = new Vector2(1.5f, -1.5f);

            // Plus button
            GameObject plusObj = GetOrCreateChild(livesWidget, "PlusBtn");
            RectTransform plusRt = plusObj.GetComponent<RectTransform>();
            plusRt.anchorMin = plusRt.anchorMax = plusRt.pivot = new Vector2(1f, 0.5f);
            plusRt.anchoredPosition = new Vector2(-10f, 0f);
            plusRt.sizeDelta = new Vector2(28f, 28f);
            Image plusImg = EnsureComponent<Image>(plusObj);
            plusImg.sprite = plusSprite;

            // 4. Coins Widget (Sağ - 2)
            GameObject coinsWidget = GetOrCreateChild(topObj, "CoinsWidget");
            RectTransform coinsRt = coinsWidget.GetComponent<RectTransform>();
            coinsRt.anchorMin = coinsRt.anchorMax = coinsRt.pivot = new Vector2(1f, 0.5f);
            coinsRt.anchoredPosition = new Vector2(-75f, 0f);
            coinsRt.sizeDelta = new Vector2(165f, 66f);
            Image coinsBg = EnsureComponent<Image>(coinsWidget);
            coinsBg.sprite = pillSprite;
            coinsBg.type = Image.Type.Sliced;

            // Coin Icon
            GameObject coinObj = GetOrCreateChild(coinsWidget, "Icon");
            RectTransform coinRt = coinObj.GetComponent<RectTransform>();
            coinRt.anchorMin = coinRt.anchorMax = coinRt.pivot = new Vector2(0f, 0.5f);
            coinRt.anchoredPosition = new Vector2(24f, 0f);
            coinRt.sizeDelta = new Vector2(40f, 40f);
            Image coinImg = EnsureComponent<Image>(coinObj);
            coinImg.sprite = coinSprite;
            coinImg.raycastTarget = false;

            // Coin Text
            GameObject coinTextObj = GetOrCreateChild(coinsWidget, "Text");
            RectTransform ctRt = coinTextObj.GetComponent<RectTransform>();
            ctRt.anchorMin = ctRt.anchorMax = ctRt.pivot = new Vector2(0.5f, 0.5f);
            ctRt.anchoredPosition = new Vector2(14f, 0f);
            ctRt.sizeDelta = new Vector2(80f, 50f);
            Text ctText = EnsureComponent<Text>(coinTextObj);
            ctText.text = "250";
            if (font != null) ctText.font = font;
            ctText.fontSize = 32;
            ctText.alignment = TextAnchor.MiddleCenter;
            ctText.color = Color.white;
            Outline ctOut = EnsureComponent<Outline>(coinTextObj);
            ctOut.effectColor = new Color32(20, 26, 48, 255);
            ctOut.effectDistance = new Vector2(1.5f, -1.5f);

            // Coin Plus
            GameObject coinPlusObj = GetOrCreateChild(coinsWidget, "PlusBtn");
            RectTransform cPlusRt = coinPlusObj.GetComponent<RectTransform>();
            cPlusRt.anchorMin = cPlusRt.anchorMax = cPlusRt.pivot = new Vector2(1f, 0.5f);
            cPlusRt.anchoredPosition = new Vector2(-10f, 0f);
            cPlusRt.sizeDelta = new Vector2(28f, 28f);
            Image cPlusImg = EnsureComponent<Image>(coinPlusObj);
            cPlusImg.sprite = plusSprite;

            EditorUtility.SetDirty(topObj);
        }

        private static void SetupBoardAndSlots(Canvas slotCanvas, Font font)
        {
            if (slotCanvas == null) return;

            TruckSlotRow slotRow = slotCanvas.GetComponentInChildren<TruckSlotRow>();
            if (slotRow == null) return;

            Sprite frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CasualUIDir}/board_frame_25d.png");
            Sprite innerSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CasualUIDir}/board_inner_well.png");
            Sprite shadowSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CasualUIDir}/board_shadow.png");
            Sprite slotPodSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CasualUIDir}/slot_pod_25d.png");
            Sprite slotShadowSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CasualUIDir}/slot_shadow.png");
            Sprite progressPodSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CasualUIDir}/progress_station_pod.png");

            // 1. Board Shadows & 2.5D Frame
            // Rayların ve puzzle panosunun merkezini ve boyutunu hesapla
            // Ray sınırları: LeftX: -2.71, RightX: 2.63, TopY: 2.65, BottomY: -2.73
            // SlotRow koordinatlarında ray merkezi ~ (0, 1600), genişlik ~ 1960, yükseklik ~ 1980
            Transform rails = slotRow.transform.Find("PerimeterRails");
            Vector2 boardCenter = new Vector2(0f, 1620f);
            Vector2 boardSize = new Vector2(1980f, 2020f);

            // A) BoardShadow (En arkadaki yumuşak ortam gölgesi)
            GameObject boardShadowObj = GetOrCreateChild(slotRow.gameObject, "BoardShadow");
            boardShadowObj.transform.SetAsFirstSibling();
            RectTransform bsRt = boardShadowObj.GetComponent<RectTransform>();
            bsRt.anchorMin = bsRt.anchorMax = bsRt.pivot = new Vector2(0.5f, 0.5f);
            bsRt.anchoredPosition = boardCenter + new Vector2(0f, -40f);
            bsRt.sizeDelta = boardSize + new Vector2(160f, 160f);
            Image bsImg = EnsureComponent<Image>(boardShadowObj);
            bsImg.sprite = shadowSprite;
            bsImg.type = Image.Type.Sliced;
            bsImg.color = new Color(1f, 1f, 1f, 0.75f);
            bsImg.raycastTarget = false;

            // B) BoardFrame (2.5D kalın, yuvarlatılmış makine çerçevesi)
            GameObject boardFrameObj = GetOrCreateChild(slotRow.gameObject, "BoardFrame");
            boardFrameObj.transform.SetSiblingIndex(1);
            RectTransform bfRt = boardFrameObj.GetComponent<RectTransform>();
            bfRt.anchorMin = bfRt.anchorMax = bfRt.pivot = new Vector2(0.5f, 0.5f);
            bfRt.anchoredPosition = boardCenter;
            bfRt.sizeDelta = boardSize + new Vector2(100f, 100f);
            Image bfImg = EnsureComponent<Image>(boardFrameObj);
            bfImg.sprite = frameSprite;
            bfImg.type = Image.Type.Sliced;
            bfImg.color = Color.white;
            bfImg.raycastTarget = false;

            // C) BoardInner (Piksel küplerinin altındaki temiz iç zemin)
            GameObject boardInnerObj = GetOrCreateChild(slotRow.gameObject, "BoardInner");
            boardInnerObj.transform.SetSiblingIndex(2);
            RectTransform biRt = boardInnerObj.GetComponent<RectTransform>();
            biRt.anchorMin = biRt.anchorMax = biRt.pivot = new Vector2(0.5f, 0.5f);
            biRt.anchoredPosition = boardCenter;
            biRt.sizeDelta = boardSize - new Vector2(140f, 140f);
            Image biImg = EnsureComponent<Image>(boardInnerObj);
            biImg.sprite = innerSprite;
            biImg.type = Image.Type.Sliced;
            biImg.color = Color.white;
            biImg.raycastTarget = false;

            // 2. Alt 5 Slot Tasarımı (9-sliced soft pastel pod)
            if (slotRow.Slots != null && slotRow.Slots.Count > 0)
            {
                slotRow.Style.sprite = slotPodSprite;
                slotRow.Style.showSprite = true;

                for (int i = 0; i < slotRow.Slots.Count; i++)
                {
                    TruckSlot slot = slotRow.Slots[i];
                    if (slot == null) continue;

                    Image slotImg = slot.GetComponent<Image>();
                    if (slotImg != null)
                    {
                        slotImg.sprite = slotPodSprite;
                        slotImg.type = Image.Type.Sliced;
                        slotImg.color = Color.white;
                    }

                    // Slot gölgesini güncelle
                    Transform shadowsRoot = slotRow.transform.Find("Shadows");
                    if (shadowsRoot != null)
                    {
                        Transform sTr = shadowsRoot.Find($"SlotShadow_{i + 1}");
                        if (sTr != null)
                        {
                            Image sImg = sTr.GetComponent<Image>();
                            if (sImg != null)
                            {
                                sImg.sprite = slotShadowSprite;
                                sImg.type = Image.Type.Sliced;
                                sImg.color = new Color32(8, 12, 28, 115);
                            }
                        }
                    }
                }
            }

            // 3. Progress Pod & "2/5" Sayacı
            Transform cornerSlot = slotRow.transform.Find("TrackCornerSlot");
            if (cornerSlot != null)
            {
                Image csImg = cornerSlot.GetComponent<Image>();
                if (csImg != null)
                {
                    csImg.sprite = progressPodSprite;
                    csImg.type = Image.Type.Sliced;
                    csImg.color = Color.white;
                }

                Transform counterTextTr = cornerSlot.Find("CounterText");
                if (counterTextTr != null)
                {
                    Text counterText = counterTextTr.GetComponent<Text>();
                    if (counterText != null)
                    {
                        if (font != null) counterText.font = font;
                        counterText.fontSize = 38;
                        counterText.color = Color.white;
                        counterText.alignment = TextAnchor.MiddleCenter;

                        Outline cOut = EnsureComponent<Outline>(counterText.gameObject);
                        cOut.effectColor = new Color32(24, 34, 60, 255);
                        cOut.effectDistance = new Vector2(2f, -2.5f);
                    }
                }
            }

            // 4. TruckPool (Bekleme Karakter/Vagon Alanı) Hizalaması
            TruckPool pool = slotCanvas.GetComponentInChildren<TruckPool>();
            if (pool != null)
            {
                RectTransform poolRt = pool.GetComponent<RectTransform>();
                if (poolRt != null)
                {
                    // Slotların hemen altına, ekranda tam görünecek şekilde yaklaştır
                    poolRt.anchoredPosition = new Vector2(0f, -240f);
                    EditorUtility.SetDirty(poolRt);
                }
            }

            EditorUtility.SetDirty(slotRow);
        }

        private static GameObject GetOrCreateChild(GameObject parent, string name)
        {
            Transform found = parent.transform.Find(name);
            if (found != null) return found.gameObject;

            GameObject created = new GameObject(name, typeof(RectTransform));
            created.transform.SetParent(parent.transform, false);
            return created;
        }

        private static T EnsureComponent<T>(GameObject obj) where T : Component
        {
            T comp = obj.GetComponent<T>();
            if (comp == null) comp = obj.AddComponent<T>();
            return comp;
        }
    }
}
