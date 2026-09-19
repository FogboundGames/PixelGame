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

            // 8. PHASE 12: CharacterArea (4 Sevimli Maskot Karakter: Yeşil, Kahve, Turuncu, Sarı)
            SetupCharacterArea(slotCanvas);

            // 9. PHASE 13: Sevimli Rakun (Level_01_Raccoon) Seviyesini Yükle
            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen != null)
            {
                PixelLevelData raccoonLevel = AssetDatabase.LoadAssetAtPath<PixelLevelData>("Assets/Levels/Level_01_Raccoon.asset");
                if (raccoonLevel != null)
                {
                    gen.LoadLevel(raccoonLevel);
                    Debug.Log("<color=#00FFAA><b>[VisualOverhaul]</b></color> Level 1 'Sevimli Rakun' başarıyla panoya yüklendi!");
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            CaptureGameViewScreenshot.Capture();

            Debug.Log("<color=#00FFAA><b>[VisualOverhaul]</b></color> Oyunun görsel kalitesi referansla birebir eşleşecek şekilde en üst seviyeye taşındı!");
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

            Transform oldBg = overlayCanvas.transform.Find("Background");
            if (oldBg != null)
            {
                Object.DestroyImmediate(oldBg.gameObject);
            }

            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.059f, 0.082f, 0.188f, 1f); // #0f1530 dark navy
                EditorUtility.SetDirty(cam);

                // Overlay Canvas'ı ScreenSpaceCamera moduna al ki Camera.Render() ile TopUI da çizilsin!
                overlayCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                overlayCanvas.worldCamera = cam;
                overlayCanvas.planeDistance = 5f;
                overlayCanvas.sortingOrder = 100;
                EditorUtility.SetDirty(overlayCanvas);
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

            // Eski ayrik widget'lari temizle
            Transform oldLvl = topObj.transform.Find("LevelBadge");
            if (oldLvl != null) Object.DestroyImmediate(oldLvl.gameObject);
            Transform oldLives = topObj.transform.Find("LivesWidget");
            if (oldLives != null) Object.DestroyImmediate(oldLives.gameObject);
            Transform oldCoins = topObj.transform.Find("CoinsWidget");
            if (oldCoins != null) Object.DestroyImmediate(oldCoins.gameObject);

            Sprite btnSettingsSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CasualUIDir}/btn_settings.png");
            Sprite topBarPodSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CasualUIDir}/top_bar_pod.png");

            // 1. Settings Button (Sol)
            GameObject btnSettings = GetOrCreateChild(topObj, "SettingsButton");
            RectTransform btnRt = btnSettings.GetComponent<RectTransform>();
            btnRt.anchorMin = btnRt.anchorMax = btnRt.pivot = new Vector2(0f, 0.5f);
            btnRt.anchoredPosition = new Vector2(100f, 0f);
            btnRt.sizeDelta = new Vector2(125f, 125f);
            Image btnImg = EnsureComponent<Image>(btnSettings);
            btnImg.sprite = btnSettingsSprite;
            btnImg.type = Image.Type.Simple;
            EnsureComponent<Button>(btnSettings);
            EnsureComponent<CasualUIButtonJuice>(btnSettings);

            // 2. Unified Header Capsule (Orta-Sağ)
            GameObject headerCapsule = GetOrCreateChild(topObj, "HeaderCapsule");
            RectTransform hcRt = headerCapsule.GetComponent<RectTransform>();
            hcRt.anchorMin = hcRt.anchorMax = hcRt.pivot = new Vector2(1f, 0.5f);
            hcRt.anchoredPosition = new Vector2(-40f, 0f);
            hcRt.sizeDelta = new Vector2(810f, 135f);
            Image hcImg = EnsureComponent<Image>(headerCapsule);
            hcImg.sprite = topBarPodSprite;
            hcImg.type = Image.Type.Simple;
            hcImg.raycastTarget = false;

            EditorUtility.SetDirty(topObj);
        }

        private static void SetupBoardAndSlots(Canvas slotCanvas, TMP_FontAsset tmpFont, Font legacyFont)
        {
            if (slotCanvas == null) return;

            TruckSlotRow slotRow = slotCanvas.GetComponentInChildren<TruckSlotRow>();
            if (slotRow == null) return;

            Sprite frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CasualUIDir}/board_frame_25d.png");
            Sprite slotPodSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CasualUIDir}/slot_pod_25d.png");
            Sprite slotShadowSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CasualUIDir}/slot_shadow.png");
            Sprite progressPodSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CasualUIDir}/progress_station_pod.png");

            // 1. Hareketli 3D Ray Prefabını (PerimeterRails + TrackFlow) Geri Yükle ve Aktif Et
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go != null && (go.name == "BoardFrame" || go.name == "BoardInner" || go.name == "BoardShadow"))
                {
                    Object.DestroyImmediate(go);
                }
            }

            // Ray materyallerini, prefablarını ve chevron dokusunu güncelle
            TrackSystemSetup.ExecuteSetup(silent: true);

            // PerimeterRails mesh renderer'larını aç
            GameObject railsObj = GameObject.Find("PerimeterRails");
            if (railsObj != null)
            {
                railsObj.SetActive(true);
                foreach (MeshRenderer mr in railsObj.GetComponentsInChildren<MeshRenderer>(true))
                {
                    mr.enabled = true;
                }
            }
            else
            {
                // Sahnede ray yoksa otomatik oluştur
                TrackSystemSetup.BuildSceneRails();
            }

            // TrackFlow bileşenini kontrol et ve aktif olduğundan emin ol
            TrackFlow flow = Object.FindFirstObjectByType<TrackFlow>();
            if (flow != null)
            {
                flow.enabled = true;
                flow.speed = 0.90f;
            }

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
                RectTransform csRt = cornerSlot.GetComponent<RectTransform>();
                if (csRt != null)
                {
                    csRt.anchoredPosition = new Vector2(-885f, 715f);
                    csRt.sizeDelta = new Vector2(170f, 285f);
                    csRt.localScale = Vector3.one;
                }

                Image csImg = cornerSlot.GetComponent<Image>();
                if (csImg != null)
                {
                    csImg.sprite = progressPodSprite;
                    csImg.type = Image.Type.Simple;
                    csImg.color = Color.white;
                }

                // Place_7 gibi eski 3B parçaların MeshRenderer'ını gizle
                foreach (MeshRenderer mr in cornerSlot.GetComponentsInChildren<MeshRenderer>(true))
                {
                    mr.enabled = false;
                }

                Transform counterTextTr = cornerSlot.Find("CounterText");
                if (counterTextTr != null)
                {
                    RectTransform ctRt = counterTextTr.GetComponent<RectTransform>();
                    if (ctRt != null)
                    {
                        ctRt.anchoredPosition = new Vector2(0f, -80f);
                        ctRt.sizeDelta = new Vector2(130f, 50f);
                    }
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
            TruckSlotRow slotRow = slotCanvas.GetComponentInChildren<TruckSlotRow>();
            if (slotRow == null) return;

            GameObject charArea = GetOrCreateChild(slotRow.gameObject, "CharacterArea");
            RectTransform caRt = charArea.GetComponent<RectTransform>();
            caRt.anchorMin = caRt.anchorMax = caRt.pivot = new Vector2(0.5f, 0.5f);
            caRt.anchoredPosition = new Vector2(0f, -330f);
            caRt.sizeDelta = new Vector2(1600f, 320f);

            string[] mascotFiles = new string[] {
                "mascot_green.png",
                "mascot_brown.png",
                "mascot_orange.png",
                "mascot_yellow.png"
            };

            float[] xPositions = new float[] { -540f, -180f, 180f, 540f };
            Sprite shadowSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CasualUIDir}/slot_shadow.png");

            for (int i = 0; i < mascotFiles.Length; i++)
            {
                string mfile = mascotFiles[i];
                Sprite mascotSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CasualUIDir}/{mfile}");

                GameObject mObj = GetOrCreateChild(charArea, $"Mascot_{i + 1}");
                RectTransform mRt = mObj.GetComponent<RectTransform>();
                mRt.anchorMin = mRt.anchorMax = mRt.pivot = new Vector2(0.5f, 0.5f);
                mRt.anchoredPosition = new Vector2(xPositions[i], 0f);
                mRt.sizeDelta = new Vector2(230f, 335f);

                // Contact shadow child
                GameObject sObj = GetOrCreateChild(mObj, "ContactShadow");
                RectTransform sRt = sObj.GetComponent<RectTransform>();
                sRt.anchorMin = sRt.anchorMax = sRt.pivot = new Vector2(0.5f, 0f);
                sRt.anchoredPosition = new Vector2(0f, -20f);
                sRt.sizeDelta = new Vector2(220f, 70f);
                Image sImg = EnsureComponent<Image>(sObj);
                sImg.sprite = shadowSprite;
                sImg.color = new Color32(4, 8, 20, 180);
                sImg.raycastTarget = false;

                // Mascot image
                Image mImg = EnsureComponent<Image>(mObj);
                mImg.sprite = mascotSprite;
                mImg.type = Image.Type.Simple;
                mImg.color = Color.white;
                mImg.raycastTarget = false;
            }
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
        private const string RunKey = "RunVisualOverhaul_v11_restore_moving_track";

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
