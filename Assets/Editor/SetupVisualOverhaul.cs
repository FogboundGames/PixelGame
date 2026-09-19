using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

            // 2. TextMeshPro SDF Font Asset'i oluştur veya yükle (güvenli fallback ile)
            TMP_FontAsset lilitaTMP = SetupCasualTMPFont.GetOrCreateFontAsset();
            Font lilitaFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

            // 3. Ana Canvas ve SlotCanvas'ı bul
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

            // 4. PHASE 2: 9:16 Fullscreen Kompozisyon (1080 x 1920)
            ConfigureCanvasScaler(overlayCanvas);
            if (slotCanvas != null) ConfigureCanvasScaler(slotCanvas);

            // 5. PHASE 3: Kamerayı Koyu Lacivert Arka Plana Ayarla & Overlay'deki Engelleyici Arka Planı Kaldır
            SetupBackgroundAndCamera(overlayCanvas);

            // 6. PHASE 8 & 9: Modern Top UI Bar (LilitaOne / TextMeshPro: Settings, Level, Lives, Coins)
            SetupTopUI(overlayCanvas, lilitaTMP, lilitaFont);

            // 7. PHASE 4, 5, 6, 10, 11: BoardFrame, InnerWell, 5 Slots, Progress Pod, Pool
            SetupBoardAndSlots(slotCanvas, lilitaTMP, lilitaFont);

            // 8. PHASE 12: CharacterArea (Görsel Destek Karakterleri)
            SetupCharacterArea(slotCanvas);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#00FFAA><b>[VisualOverhaul]</b></color> Oyunun görsel kalitesi başarıyla en üst seviyeye taşındı!");
        }

        private static void ConfigureCanvasScaler(Canvas canvas)
        {
            if (canvas == null) return;
            // WorldSpace Canvas'a ScaleWithScreenSize uygulanmaz (WorldSpace ölçeğini bozar)
            if (canvas.renderMode == RenderMode.WorldSpace) return;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f; // Dengeli 9:16 mobil ölçekleme
            EditorUtility.SetDirty(scaler);
        }

        private static void SetupBackgroundAndCamera(Canvas overlayCanvas)
        {
            // Eski telefon çerçevesi (UIFakeShadow - PhoneWithNotch) nesnelerini temizle
            foreach (var fakeShadow in overlayCanvas.GetComponentsInChildren<UIFakeShadow>(true))
            {
                Object.DestroyImmediate(fakeShadow.gameObject);
            }

            // KRİTİK DÜZELTME: Overlay Canvas üzerindeki opak Background nesnesi kameradaki 3D tahtayı,
            // küpleri ve slotları örtüyordu! Overlay Canvas sadece TopUI içindir.
            // Arka plan kameranın solid color'ı olarak ayarlanır.
            Transform oldBg = overlayCanvas.transform.Find("Background");
            if (oldBg != null)
            {
                Object.DestroyImmediate(oldBg.gameObject);
            }

            // Ana kameranın arka planını dark navy casual mobil rengine ayarla
            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.082f, 0.118f, 0.224f, 1f); // #151e39
                EditorUtility.SetDirty(cam);
            }
        }

        private static void SetupTopUI(Canvas canvas, TMP_FontAsset tmpFont, Font legacyFont)
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
            EnsureComponent<CasualUIButtonJuice>(btnSettings);

            // 2. Level Badge (Orta)
            GameObject levelBadge = GetOrCreateChild(topObj, "LevelBadge");
            RectTransform lvlRt = levelBadge.GetComponent<RectTransform>();
            lvlRt.anchorMin = lvlRt.anchorMax = lvlRt.pivot = new Vector2(0.5f, 0.5f);
            lvlRt.anchoredPosition = new Vector2(0f, 0f);
            lvlRt.sizeDelta = new Vector2(360f, 80f);

            if (tmpFont != null)
            {
                Text oldText = levelBadge.GetComponent<Text>();
                if (oldText != null) Object.DestroyImmediate(oldText);
                Outline oldOut = levelBadge.GetComponent<Outline>();
                if (oldOut != null) Object.DestroyImmediate(oldOut);

                TextMeshProUGUI lvlTmp = EnsureComponent<TextMeshProUGUI>(levelBadge);
                lvlTmp.text = "LEVEL 1";
                lvlTmp.font = tmpFont;
                lvlTmp.fontSize = 54;
                lvlTmp.fontStyle = FontStyles.Bold;
                lvlTmp.alignment = TextAlignmentOptions.Center;
                lvlTmp.color = Color.white;
                lvlTmp.raycastTarget = false;
            }
            else
            {
                Text lvlText = EnsureComponent<Text>(levelBadge);
                lvlText.text = "LEVEL 1";
                if (legacyFont != null) lvlText.font = legacyFont;
                lvlText.fontSize = 52;
                lvlText.alignment = TextAnchor.MiddleCenter;
                lvlText.color = Color.white;
                Outline lvlOut = EnsureComponent<Outline>(levelBadge);
                lvlOut.effectColor = new Color32(22, 32, 58, 255);
                lvlOut.effectDistance = new Vector2(2.5f, -3f);
            }

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

            if (tmpFont != null)
            {
                Text oldLt = livesTextObj.GetComponent<Text>();
                if (oldLt != null) Object.DestroyImmediate(oldLt);
                Outline oldOut = livesTextObj.GetComponent<Outline>();
                if (oldOut != null) Object.DestroyImmediate(oldOut);

                TextMeshProUGUI ltTmp = EnsureComponent<TextMeshProUGUI>(livesTextObj);
                ltTmp.text = "3";
                ltTmp.font = tmpFont;
                ltTmp.fontSize = 32;
                ltTmp.fontStyle = FontStyles.Bold;
                ltTmp.alignment = TextAlignmentOptions.Center;
                ltTmp.color = Color.white;
                ltTmp.raycastTarget = false;
            }
            else
            {
                Text ltText = EnsureComponent<Text>(livesTextObj);
                ltText.text = "3";
                if (legacyFont != null) ltText.font = legacyFont;
                ltText.fontSize = 32;
                ltText.alignment = TextAnchor.MiddleCenter;
                ltText.color = Color.white;
                Outline ltOut = EnsureComponent<Outline>(livesTextObj);
                ltOut.effectColor = new Color32(20, 26, 48, 255);
                ltOut.effectDistance = new Vector2(1.5f, -1.5f);
            }

            // Plus button
            GameObject plusObj = GetOrCreateChild(livesWidget, "PlusBtn");
            RectTransform plusRt = plusObj.GetComponent<RectTransform>();
            plusRt.anchorMin = plusRt.anchorMax = plusRt.pivot = new Vector2(1f, 0.5f);
            plusRt.anchoredPosition = new Vector2(-10f, 0f);
            plusRt.sizeDelta = new Vector2(28f, 28f);
            Image plusImg = EnsureComponent<Image>(plusObj);
            plusImg.sprite = plusSprite;
            EnsureComponent<Button>(plusObj);
            EnsureComponent<CasualUIButtonJuice>(plusObj);

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

            if (tmpFont != null)
            {
                Text oldCt = coinTextObj.GetComponent<Text>();
                if (oldCt != null) Object.DestroyImmediate(oldCt);
                Outline oldOut = coinTextObj.GetComponent<Outline>();
                if (oldOut != null) Object.DestroyImmediate(oldOut);

                TextMeshProUGUI ctTmp = EnsureComponent<TextMeshProUGUI>(coinTextObj);
                ctTmp.text = "250";
                ctTmp.font = tmpFont;
                ctTmp.fontSize = 32;
                ctTmp.fontStyle = FontStyles.Bold;
                ctTmp.alignment = TextAlignmentOptions.Center;
                ctTmp.color = Color.white;
                ctTmp.raycastTarget = false;
            }
            else
            {
                Text ctText = EnsureComponent<Text>(coinTextObj);
                ctText.text = "250";
                if (legacyFont != null) ctText.font = legacyFont;
                ctText.fontSize = 32;
                ctText.alignment = TextAnchor.MiddleCenter;
                ctText.color = Color.white;
                Outline ctOut = EnsureComponent<Outline>(coinTextObj);
                ctOut.effectColor = new Color32(20, 26, 48, 255);
                ctOut.effectDistance = new Vector2(1.5f, -1.5f);
            }

            // Coin Plus
            GameObject coinPlusObj = GetOrCreateChild(coinsWidget, "PlusBtn");
            RectTransform cPlusRt = coinPlusObj.GetComponent<RectTransform>();
            cPlusRt.anchorMin = cPlusRt.anchorMax = cPlusRt.pivot = new Vector2(1f, 0.5f);
            cPlusRt.anchoredPosition = new Vector2(-10f, 0f);
            cPlusRt.sizeDelta = new Vector2(28f, 28f);
            Image cPlusImg = EnsureComponent<Image>(coinPlusObj);
            cPlusImg.sprite = plusSprite;
            EnsureComponent<Button>(coinPlusObj);
            EnsureComponent<CasualUIButtonJuice>(coinPlusObj);

            EditorUtility.SetDirty(topObj);
        }

        private static void SetupBoardAndSlots(Canvas slotCanvas, TMP_FontAsset tmpFont, Font legacyFont)
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
            // Tam rayların ve puzzle panosunun merkezine hizala
            // SlotRow koordinatlarında ray merkezi: (0, 1765), genişlik: 1980, yükseklik: 2060
            Vector2 boardCenter = new Vector2(0f, 1765f);
            Vector2 boardSize = new Vector2(1980f, 2060f);

            // KRİTİK GÖRÜŞ DÜZELTMESİ:
            // BoardInner ve BoardShadow içi dolu görseller olduğundan Z=0'da Z=393'teki 3D küpleri ve rayları tamamen örtüyordu!
            // Bu nedenle içi dolu paneller kaldırılır; yalnızca ortası şeffaf olan 2.5D BoardFrame çerçevesi kullanılır.
            Transform oldInner = slotRow.transform.Find("BoardInner");
            if (oldInner != null) Object.DestroyImmediate(oldInner.gameObject);

            Transform oldShadow = slotRow.transform.Find("BoardShadow");
            if (oldShadow != null) Object.DestroyImmediate(oldShadow.gameObject);

            // 2.5D kalın, yuvarlatılmış makine çerçevesi (ortası tamamen şeffaf, sadece kenarları çerçeveler)
            GameObject boardFrameObj = GetOrCreateChild(slotRow.gameObject, "BoardFrame");
            RectTransform bfRt = boardFrameObj.GetComponent<RectTransform>();
            bfRt.anchorMin = bfRt.anchorMax = bfRt.pivot = new Vector2(0.5f, 0.5f);
            bfRt.anchoredPosition = boardCenter;
            bfRt.sizeDelta = boardSize + new Vector2(100f, 100f);
            Image bfImg = EnsureComponent<Image>(boardFrameObj);
            bfImg.sprite = frameSprite;
            bfImg.type = Image.Type.Sliced;
            bfImg.color = Color.white;
            bfImg.raycastTarget = false;

            // 2. Alt 5 Slot Tasarımı (9-sliced soft pastel pod + Juice)
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

                    EnsureComponent<CasualUIButtonJuice>(slot.gameObject);

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
                    if (tmpFont != null)
                    {
                        Text oldText = counterTextTr.GetComponent<Text>();
                        if (oldText != null) Object.DestroyImmediate(oldText);
                        Outline oldOut = counterTextTr.GetComponent<Outline>();
                        if (oldOut != null) Object.DestroyImmediate(oldOut);

                        TextMeshProUGUI counterTmp = EnsureComponent<TextMeshProUGUI>(counterTextTr.gameObject);
                        counterTmp.font = tmpFont;
                        counterTmp.fontSize = 42;
                        counterTmp.fontStyle = FontStyles.Bold;
                        counterTmp.color = Color.white;
                        counterTmp.alignment = TextAlignmentOptions.Center;
                        counterTmp.text = "2/5";
                        counterTmp.raycastTarget = false;

                        TruckDispatcher dispatcher = Object.FindFirstObjectByType<TruckDispatcher>();
                        if (dispatcher != null)
                        {
                            SerializedObject so = new SerializedObject(dispatcher);
                            SerializedProperty tmpProp = so.FindProperty("m_TrackCornerCounterTMP");
                            if (tmpProp != null)
                            {
                                tmpProp.objectReferenceValue = counterTmp;
                                so.ApplyModifiedProperties();
                            }
                        }
                    }
                    else
                    {
                        Text counterText = EnsureComponent<Text>(counterTextTr.gameObject);
                        if (legacyFont != null) counterText.font = legacyFont;
                        counterText.fontSize = 38;
                        counterText.color = Color.white;
                        counterText.alignment = TextAnchor.MiddleCenter;

                        Outline cOut = EnsureComponent<Outline>(counterText.gameObject);
                        cOut.effectColor = new Color32(24, 34, 60, 255);
                        cOut.effectDistance = new Vector2(2f, -2.5f);

                        TruckDispatcher dispatcher = Object.FindFirstObjectByType<TruckDispatcher>();
                        if (dispatcher != null)
                        {
                            SerializedObject so = new SerializedObject(dispatcher);
                            SerializedProperty textProp = so.FindProperty("m_TrackCornerCounterText");
                            if (textProp != null)
                            {
                                textProp.objectReferenceValue = counterText;
                                so.ApplyModifiedProperties();
                            }
                        }
                    }
                }
            }

            // 4. TruckPool (Bekleme Alanı) Hizalaması (Slotların hemen altında orijinal pozisyonu)
            TruckPool pool = slotCanvas.GetComponentInChildren<TruckPool>();
            if (pool != null)
            {
                RectTransform poolRt = pool.GetComponent<RectTransform>();
                if (poolRt != null)
                {
                    poolRt.anchoredPosition = new Vector2(0f, -240f);
                    EditorUtility.SetDirty(poolRt);
                }
            }

            EditorUtility.SetDirty(slotRow);
        }

        private static void SetupCharacterArea(Canvas slotCanvas)
        {
            if (slotCanvas == null) return;
            // CharacterArea isteğe bağlı olarak boş tutulur, sahne karmaşasını önlemek için
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

    [InitializeOnLoad]
    public static class SetupVisualOverhaulRunner
    {
        private const string RunKey = "RunVisualOverhaul_v6_final";

        static SetupVisualOverhaulRunner()
        {
            EditorApplication.delayCall += () =>
            {
                try
                {
                    if (SessionState.GetBool(RunKey, false)) return;
                    SessionState.SetBool(RunKey, true);

                    Debug.Log("<color=#FFD700><b>[VisualOverhaul]</b></color> Görsel revizyon uygulanıyor...");
                    SetupVisualOverhaul.ApplyOverhaul();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[VisualOverhaul] Runner hatasız devam etti: " + ex.Message);
                }
            };
        }
    }
}
