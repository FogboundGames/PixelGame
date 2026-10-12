using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGame.Editor
{
    /// <summary>
    /// Gemi sahnesine (Assets/Scenes/Gemi.unity) referans görseldeki (Image 1)
    /// üst HUD şeridini (Ayarlar butonu, LEVEL 1, Can ve Altın hapları) kurar.
    /// </summary>
    [InitializeOnLoad]
    public static class SetupGemiTopHUD
    {
        private const string ScenePath = "Assets/Scenes/Gemi.unity";
        private const string UIRoot = "Assets/UI/CasualUI/";
        private const string FontPath = "Assets/Fonts/LilitaOne-Regular SDF.asset";
        private const string AutoRunKey = "GemiTopHUD_Installed_v5";
        private const string RemoveCoinsKey = "PixelGame_CoinRemoved_v1";
        private const string RetryButtonKey = "PixelGame_RetryButtonInstalled_v2";

        private const string MarineHUDKey = "PixelGame_MarineHUD_Applied_v5";

        private static bool s_Applied = false;

        static SetupGemiTopHUD()
        {
            EditorApplication.delayCall += AutoApplyMarineHUDIfNeeded;
        }

        private static void AutoApplyMarineHUDIfNeeded()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (s_Applied) return;
            s_Applied = true;
            BuildTopHUD();
        }

        private static void AutoRemoveCoinsIfNeeded()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SessionState.GetBool(RemoveCoinsKey, false)) return;
            SessionState.SetBool(RemoveCoinsKey, true);
            RemoveCoinsFromScene();
        }

        private static void AutoSetupRetryButtonIfNeeded()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SessionState.GetBool(RetryButtonKey, false)) return;
            SessionState.SetBool(RetryButtonKey, true);
            EnsureRetryButtonInScene();
        }

        [MenuItem("PixelGame/🔄 Sahneye Retry Butonu Ekle (Ensure Retry Button)")]
        public static void EnsureRetryButtonInScene()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != ScenePath && System.IO.File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
            }

            GameObject canvasGo = GameObject.Find("HUD_Canvas");
            if (canvasGo == null) return;

            Transform topUIGo = canvasGo.transform.Find("TopUI");
            if (topUIGo == null) return;

            CasualHudController ctrl = canvasGo.GetComponent<CasualHudController>();

            Transform existingRetry = topUIGo.Find("RetryButton");
            GameObject retryGo;
            if (existingRetry != null)
            {
                retryGo = existingRetry.gameObject;
            }
            else
            {
                retryGo = new GameObject("RetryButton", typeof(RectTransform), typeof(Image), typeof(Button));
                Undo.RegisterCreatedObjectUndo(retryGo, "Create RetryButton");
                retryGo.transform.SetParent(topUIGo, false);
            }

            RectTransform retryRt = retryGo.GetComponent<RectTransform>();
            retryRt.anchorMin = new Vector2(0f, 1f);
            retryRt.anchorMax = new Vector2(0f, 1f);
            retryRt.pivot = new Vector2(0f, 1f);
            retryRt.anchoredPosition = new Vector2(30f, -80f);
            retryRt.sizeDelta = new Vector2(96f, 96f);
            retryRt.localScale = Vector3.one * 1.1f;

            Image retryImg = retryGo.GetComponent<Image>();
            Sprite retrySprite = LoadSprite("btn_marine_lifebuoy");
            if (retrySprite == null) retrySprite = LoadSprite("btn_retry_ref");
            if (retrySprite == null) retrySprite = LoadSprite("btn_restart");
            if (retrySprite != null) retryImg.sprite = retrySprite;
            retryImg.preserveAspect = true;

            CasualUIButtonJuice juice = retryGo.GetComponent<CasualUIButtonJuice>();
            if (juice == null) juice = retryGo.AddComponent<CasualUIButtonJuice>();

            Button retryBtn = retryGo.GetComponent<Button>();
            if (retryBtn != null && ctrl != null)
            {
                UnityEditor.Events.UnityEventTools.RemovePersistentListener(retryBtn.onClick, ctrl.RestartLevel);
                UnityEditor.Events.UnityEventTools.AddPersistentListener(retryBtn.onClick, ctrl.RestartLevel);
                ctrl.RetryButton = retryBtn;
                EditorUtility.SetDirty(ctrl);
            }

            // Hız (Speed 1X / 2X) Butonu
            Transform existingSpeed = topUIGo.Find("SpeedButton");
            GameObject speedGo;
            if (existingSpeed != null)
            {
                speedGo = existingSpeed.gameObject;
            }
            else
            {
                speedGo = new GameObject("SpeedButton", typeof(RectTransform), typeof(Image), typeof(Button));
                Undo.RegisterCreatedObjectUndo(speedGo, "Create SpeedButton");
                speedGo.transform.SetParent(topUIGo, false);
            }

            RectTransform speedRt = speedGo.GetComponent<RectTransform>();
            speedRt.anchorMin = new Vector2(0f, 1f);
            speedRt.anchorMax = new Vector2(0f, 1f);
            speedRt.pivot = new Vector2(0f, 1f);
            speedRt.anchoredPosition = new Vector2(145f, -80f);
            speedRt.sizeDelta = new Vector2(96f, 96f);
            speedRt.localScale = Vector3.one * 1.1f;

            Image speedImg = speedGo.GetComponent<Image>();
            Sprite speed1xSprite = LoadSprite("btn_marine_speed_1x");
            Sprite speed2xSprite = LoadSprite("btn_marine_speed_2x");
            if (speed1xSprite != null) speedImg.sprite = speed1xSprite;
            speedImg.preserveAspect = true;

            CasualUIButtonJuice speedJuice = speedGo.GetComponent<CasualUIButtonJuice>();
            if (speedJuice == null) speedJuice = speedGo.AddComponent<CasualUIButtonJuice>();

            Button speedBtn = speedGo.GetComponent<Button>();
            if (speedBtn != null && ctrl != null)
            {
                UnityEditor.Events.UnityEventTools.RemovePersistentListener(speedBtn.onClick, ctrl.ToggleSpeed2X);
                UnityEditor.Events.UnityEventTools.AddPersistentListener(speedBtn.onClick, ctrl.ToggleSpeed2X);
                ctrl.SpeedButton = speedBtn;
                ctrl.SpeedButtonImage = speedImg;
                ctrl.Speed1xSprite = speed1xSprite;
                ctrl.Speed2xSprite = speed2xSprite;
                EditorUtility.SetDirty(ctrl);
            }
            EditorUtility.SetDirty(speedGo);
            EditorUtility.SetDirty(retryGo);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("<color=#00FFAA><b>[SetupGemiTopHUD]</b></color> 🔄 Sol köşeye RetryButton başarıyla eklendi ve bağlandı!");
        }

        [MenuItem("PixelGame/🪙 Sahnedeki Coin Sistemini Kaldır (Remove Coin System)")]
        public static void RemoveCoinsFromScene()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != ScenePath && System.IO.File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
            }

            GameObject coinPill = GameObject.Find("CoinPill");
            if (coinPill != null)
            {
                Undo.DestroyObjectImmediate(coinPill);
                Debug.Log("<color=#00FFAA><b>[SetupGemiTopHUD]</b></color> 🪙 CoinPill sahneden tamamen silindi.");
            }

            GameObject canvasGo = GameObject.Find("HUD_Canvas");
            if (canvasGo != null)
            {
                CasualHudController ctrl = canvasGo.GetComponent<CasualHudController>();
                if (ctrl != null)
                {
                    ctrl.CoinsText = null;
                    EditorUtility.SetDirty(ctrl);
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        [MenuItem("PixelGame/🎯 Setup Gemi Top HUD (Image 1 Style)")]
        public static void BuildTopHUD()
        {
            if (!EditorSceneManager.GetActiveScene().path.Equals(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            // 1. HUD_Canvas (Screen Space - Camera)
            GameObject canvasGo = GameObject.Find("HUD_Canvas");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("HUD_Canvas");
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create HUD_Canvas");
            }

            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            if (canvas == null) canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0f;

            GraphicRaycaster raycaster = canvasGo.GetComponent<GraphicRaycaster>();
            if (raycaster == null) raycaster = canvasGo.AddComponent<GraphicRaycaster>();

            // 2. Varsa eski TopUI'yı temizle (idempotent)
            Transform oldTop = canvasGo.transform.Find("TopUI");
            if (oldTop != null)
            {
                Undo.DestroyObjectImmediate(oldTop.gameObject);
            }

            // 3. TopUI container
            GameObject topUIGo = new GameObject("TopUI", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(topUIGo, "Create TopUI");
            topUIGo.transform.SetParent(canvasGo.transform, false);

            RectTransform topRt = topUIGo.GetComponent<RectTransform>();
            topRt.anchorMin = new Vector2(0f, 1f);
            topRt.anchorMax = new Vector2(1f, 1f);
            topRt.pivot = new Vector2(0.5f, 1f);
            topRt.anchoredPosition = Vector2.zero;
            topRt.sizeDelta = new Vector2(0f, 150f);

            // 4. Arka plan çubuğu kaldırıldı (kum dokusunun üzerinde doğal şeffaf dursun)
            GameObject bannerGo = new GameObject("TopBanner", typeof(RectTransform));
            bannerGo.transform.SetParent(topUIGo.transform, false);

            // 5. SOL: Yeniden Başlat (Retry) Butonu (Kullanıcı isteği: sol işaretlenen köşede retry butonu)
            Transform oldCoinPill = topUIGo.transform.Find("CoinPill");
            if (oldCoinPill != null)
            {
                Undo.DestroyObjectImmediate(oldCoinPill.gameObject);
            }

            Transform oldRetry = topUIGo.transform.Find("RetryButton");
            if (oldRetry != null)
            {
                Undo.DestroyObjectImmediate(oldRetry.gameObject);
            }

            GameObject retryGo = new GameObject("RetryButton", typeof(RectTransform), typeof(Image), typeof(Button));
            retryGo.transform.SetParent(topUIGo.transform, false);

            RectTransform retryRt = retryGo.GetComponent<RectTransform>();
            retryRt.anchorMin = new Vector2(0f, 1f);
            retryRt.anchorMax = new Vector2(0f, 1f);
            retryRt.pivot = new Vector2(0f, 1f);
            retryRt.anchoredPosition = new Vector2(30f, -80f);
            retryRt.sizeDelta = new Vector2(96f, 96f);
            retryRt.localScale = Vector3.one * 1.1f;

            Image retryImg = retryGo.GetComponent<Image>();
            Sprite retrySprite = LoadSprite("btn_marine_lifebuoy");
            if (retrySprite == null) retrySprite = LoadSprite("btn_retry_ref");
            if (retrySprite == null) retrySprite = LoadSprite("btn_restart");
            if (retrySprite != null) retryImg.sprite = retrySprite;
            retryImg.preserveAspect = true;
            retryGo.AddComponent<CasualUIButtonJuice>();

            Button retryBtn = retryGo.GetComponent<Button>();

            // 5b. SOL: Hız (Speed 1X / 2X) Butonu (Retry'ın hemen yanında, simetrik)
            Transform oldSpeed = topUIGo.transform.Find("SpeedButton");
            if (oldSpeed != null)
            {
                Undo.DestroyObjectImmediate(oldSpeed.gameObject);
            }

            GameObject speedGo = new GameObject("SpeedButton", typeof(RectTransform), typeof(Image), typeof(Button));
            speedGo.transform.SetParent(topUIGo.transform, false);

            RectTransform speedRt = speedGo.GetComponent<RectTransform>();
            speedRt.anchorMin = new Vector2(0f, 1f);
            speedRt.anchorMax = new Vector2(0f, 1f);
            speedRt.pivot = new Vector2(0f, 1f);
            speedRt.anchoredPosition = new Vector2(145f, -80f);
            speedRt.sizeDelta = new Vector2(96f, 96f);
            speedRt.localScale = Vector3.one * 1.1f;

            Image speedImg = speedGo.GetComponent<Image>();
            Sprite speed1xSprite = LoadSprite("btn_marine_speed_1x");
            Sprite speed2xSprite = LoadSprite("btn_marine_speed_2x");
            if (speed1xSprite != null) speedImg.sprite = speed1xSprite;
            speedImg.preserveAspect = true;
            speedGo.AddComponent<CasualUIButtonJuice>();

            Button speedBtn = speedGo.GetComponent<Button>();

            // 6. ORTA: Seviye Kapsülü (🐚 LEVEL 1 🐚 - Marin Ahşap & Halat)
            GameObject levelCapsuleGo = new GameObject("LevelCapsule", typeof(RectTransform), typeof(Image));
            levelCapsuleGo.transform.SetParent(topUIGo.transform, false);

            RectTransform levelCapRt = levelCapsuleGo.GetComponent<RectTransform>();
            levelCapRt.anchorMin = new Vector2(0.5f, 1f);
            levelCapRt.anchorMax = new Vector2(0.5f, 1f);
            levelCapRt.pivot = new Vector2(0.5f, 1f);
            levelCapRt.anchoredPosition = new Vector2(0f, -80f);
            levelCapRt.sizeDelta = new Vector2(300f, 105.6f); // butonların görünen yüksekliği (96 x 1.1)
            levelCapRt.localScale = Vector3.one;

            // Butonlarla aynı lacivert zemin (metinsiz, 9-slice) üstüne düz metin
            Image levelCapImg = levelCapsuleGo.GetComponent<Image>();
            Sprite capSprite = LoadSprite("btn_marine_blank");
            if (capSprite != null)
            {
                levelCapImg.sprite = capSprite;
                levelCapImg.type = Image.Type.Sliced;
                levelCapImg.pixelsPerUnitMultiplier = 4.8f; // köşe yarıçapı butonlarınkiyle aynı
                levelCapImg.preserveAspect = false;
            }
            else
            {
                capSprite = LoadSprite("bg_level_marine");
                if (capSprite != null) levelCapImg.sprite = capSprite;
                levelCapImg.preserveAspect = true;
            }
            levelCapImg.color = Color.white;
            levelCapImg.raycastTarget = false;

            GameObject levelTextGo = new GameObject("LevelText", typeof(RectTransform));
            levelTextGo.transform.SetParent(levelCapsuleGo.transform, false);

            RectTransform ltRt = levelTextGo.GetComponent<RectTransform>();
            ltRt.anchorMin = new Vector2(0.12f, 0.05f);
            ltRt.anchorMax = new Vector2(0.88f, 0.95f);
            ltRt.offsetMin = Vector2.zero;
            ltRt.offsetMax = Vector2.zero;

            TextMeshProUGUI levelTmp = levelTextGo.AddComponent<TextMeshProUGUI>();
            levelTmp.text = "LEVEL 1"; // Dinamik ve canlı seviye metni (ortadaki kabukların arasına oturur)
            if (font != null) levelTmp.font = font;
            levelTmp.fontSize = 42f;
            levelTmp.fontStyle = FontStyles.Bold;
            levelTmp.alignment = TextAlignmentOptions.Center;
            levelTmp.color = Color.white;
            levelTmp.outlineWidth = 0.28f;
            levelTmp.outlineColor = new Color32(14, 55, 115, 255); // Derin marin okyanus gölgesi

            // 7. SAĞ: Ses ve Titreşim Butonları (Mavi 3D Marin Butonlar)
            // 7a. Ses Butonu
            GameObject soundGo = new GameObject("SoundButton", typeof(RectTransform), typeof(Image), typeof(Button));
            soundGo.transform.SetParent(topUIGo.transform, false);

            RectTransform soundRt = soundGo.GetComponent<RectTransform>();
            soundRt.anchorMin = new Vector2(1f, 1f);
            soundRt.anchorMax = new Vector2(1f, 1f);
            soundRt.pivot = new Vector2(1f, 1f);
            soundRt.anchoredPosition = new Vector2(-150f, -80f);
            soundRt.sizeDelta = new Vector2(96f, 96f);
            soundRt.localScale = Vector3.one * 1.1f;

            Image soundImg = soundGo.GetComponent<Image>();
            Sprite soundOnSprite = LoadSprite("btn_marine_sound_on");
            if (soundOnSprite == null) soundOnSprite = LoadSprite("btn_sound_ref");
            if (soundOnSprite == null) soundOnSprite = LoadSprite("btn_sound");
            Sprite soundOffSprite = LoadSprite("btn_marine_sound_off");
            if (soundOffSprite == null) soundOffSprite = LoadSprite("btn_sound_off");
            if (soundOnSprite != null) soundImg.sprite = soundOnSprite;
            soundImg.preserveAspect = true;
            soundGo.AddComponent<CasualUIButtonJuice>();

            // 7b. Titreşim (Haptics) Butonu (3D Mavi Telefon Titreşim İkonu)
            GameObject hapticsGo = new GameObject("HapticsButton", typeof(RectTransform), typeof(Image), typeof(Button));
            hapticsGo.transform.SetParent(topUIGo.transform, false);

            RectTransform hapRt = hapticsGo.GetComponent<RectTransform>();
            hapRt.anchorMin = new Vector2(1f, 1f);
            hapRt.anchorMax = new Vector2(1f, 1f);
            hapRt.pivot = new Vector2(1f, 1f);
            hapRt.anchoredPosition = new Vector2(-30f, -80f);
            hapRt.sizeDelta = new Vector2(96f, 96f);
            hapRt.localScale = Vector3.one * 1.1f;

            Image hapImg = hapticsGo.GetComponent<Image>();
            Sprite hapticOnSprite = LoadSprite("btn_marine_haptic_on");
            if (hapticOnSprite == null) hapticOnSprite = LoadSprite("btn_haptic_ref");
            if (hapticOnSprite == null) hapticOnSprite = LoadSprite("btn_haptic");
            Sprite hapticOffSprite = LoadSprite("btn_marine_haptic_off");
            if (hapticOffSprite == null) hapticOffSprite = LoadSprite("btn_haptic_off_ref");
            if (hapticOffSprite == null) hapticOffSprite = LoadSprite("btn_haptic_off");
            if (hapticOnSprite != null) hapImg.sprite = hapticOnSprite;
            hapImg.preserveAspect = true;
            hapticsGo.AddComponent<CasualUIButtonJuice>();

            // Controller bağlantıları
            CasualHudController ctrl = canvasGo.GetComponent<CasualHudController>();
            if (ctrl == null) ctrl = canvasGo.AddComponent<CasualHudController>();
            ctrl.LevelText = levelTmp;
            ctrl.CoinsText = null;
            ctrl.SoundButtonImage = soundImg;
            ctrl.SoundOnSprite = soundOnSprite;
            ctrl.SoundOffSprite = soundOffSprite;
            ctrl.HapticsButtonImage = hapImg;
            ctrl.HapticsOnSprite = hapticOnSprite;
            ctrl.HapticsOffSprite = hapticOffSprite;

            if (retryBtn != null)
            {
                UnityEditor.Events.UnityEventTools.AddPersistentListener(retryBtn.onClick, ctrl.RestartLevel);
                ctrl.RetryButton = retryBtn;
            }

            ctrl.SpeedButton = speedBtn;
            ctrl.SpeedButtonImage = speedImg;
            ctrl.Speed1xSprite = speed1xSprite;
            ctrl.Speed2xSprite = speed2xSprite;

            if (speedBtn != null && ctrl != null)
            {
                UnityEditor.Events.UnityEventTools.RemovePersistentListener(speedBtn.onClick, ctrl.ToggleSpeed2X);
                UnityEditor.Events.UnityEventTools.AddPersistentListener(speedBtn.onClick, ctrl.ToggleSpeed2X);
            }

            Button soundBtn = soundGo.GetComponent<Button>();
            if (soundBtn != null)
            {
                UnityEditor.Events.UnityEventTools.AddPersistentListener(soundBtn.onClick, ctrl.ToggleSound);
            }

            Button hapBtn = hapticsGo.GetComponent<Button>();
            if (hapBtn != null)
            {
                UnityEditor.Events.UnityEventTools.AddPersistentListener(hapBtn.onClick, ctrl.ToggleHaptics);
            }

            // 8. İkinci çubuğu (SubHeader) temizle (tek sıra şık referans düzeni)
            Transform oldSub = canvasGo.transform.Find("SubHeader");
            if (oldSub != null)
            {
                Undo.DestroyObjectImmediate(oldSub.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("<color=#00FFAA><b>[SetupGemiTopHUD]</b></color> 📱 Gemi sahnesi üst HUD başarıyla kuruldu!");
        }

        private static GameObject BuildPill(Transform parent, string name, string iconName, string countVal, TMP_FontAsset font, float width)
        {
            const float pillHeight = 78f;

            GameObject pill = new GameObject(name, typeof(RectTransform), typeof(Image));
            pill.transform.SetParent(parent, false);

            RectTransform pillRt = pill.GetComponent<RectTransform>();
            pillRt.sizeDelta = new Vector2(width, pillHeight);

            Image pillImg = pill.GetComponent<Image>();
            Sprite pillSprite = LoadSprite("ui_pill");
            if (pillSprite != null) pillImg.sprite = pillSprite;
            pillImg.type = Image.Type.Simple;
            pillImg.raycastTarget = false;

            LayoutElement le = pill.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = pillHeight;

            // İkon (sol kenara ortalanmış, pilden hafif taşan tatlı casual efekt)
            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(pill.transform, false);

            RectTransform iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = new Vector2(14f, 0f);
            iconRt.sizeDelta = new Vector2(80f, 80f);

            Image iconImg = iconGo.GetComponent<Image>();
            Sprite iconSprite = LoadSprite(iconName);
            if (iconSprite != null) iconImg.sprite = iconSprite;
            iconImg.raycastTarget = false;

            // Sayı Metni (ikon ve artı düğmesi arasında)
            GameObject countGo = new GameObject("Count", typeof(RectTransform));
            countGo.transform.SetParent(pill.transform, false);

            RectTransform countRt = countGo.GetComponent<RectTransform>();
            countRt.anchorMin = new Vector2(0f, 0f);
            countRt.anchorMax = new Vector2(1f, 1f);
            countRt.offsetMin = new Vector2(62f, 0f);
            countRt.offsetMax = new Vector2(-42f, 0f);

            TextMeshProUGUI countTmp = countGo.AddComponent<TextMeshProUGUI>();
            countTmp.text = countVal;
            if (font != null) countTmp.font = font;
            countTmp.fontSize = 44f;
            countTmp.fontStyle = FontStyles.Bold;
            countTmp.alignment = TextAlignmentOptions.Center;
            countTmp.color = Color.white;
            countTmp.textWrappingMode = TextWrappingModes.NoWrap;

            // Artı Düğmesi (+)
            GameObject plusGo = new GameObject("PlusButton", typeof(RectTransform), typeof(Image), typeof(Button));
            plusGo.transform.SetParent(pill.transform, false);

            RectTransform plusRt = plusGo.GetComponent<RectTransform>();
            plusRt.anchorMin = new Vector2(1f, 0.5f);
            plusRt.anchorMax = new Vector2(1f, 0.5f);
            plusRt.pivot = new Vector2(0.5f, 0.5f);
            plusRt.anchoredPosition = new Vector2(-10f, 0f);
            plusRt.sizeDelta = new Vector2(46f, 46f);

            Image plusImg = plusGo.GetComponent<Image>();
            Sprite plusSprite = LoadSprite("btn_plus");
            if (plusSprite != null) plusImg.sprite = plusSprite;
            plusGo.AddComponent<CasualUIButtonJuice>();

            return pill;
        }

        private static Sprite LoadSprite(string name)
        {
            string path = $"{UIRoot}{name}.png";
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
