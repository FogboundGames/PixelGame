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

        static SetupGemiTopHUD()
        {
            EditorApplication.delayCall += AutoRunIfNeeded;
        }

        /// <summary>Artık otomatik çağrılmıyor; bkz. statik kurucu.</summary>
        private static void AutoRunIfNeeded()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SessionState.GetBool(AutoRunKey, false)) return;
            SessionState.SetBool(AutoRunKey, true);
            BuildTopHUD();
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

            // 5. SOL: Altın Hapı (CoinPill - 🪙 250 +)
            GameObject coinPill = new GameObject("CoinPill", typeof(RectTransform), typeof(Image));
            coinPill.transform.SetParent(topUIGo.transform, false);
            RectTransform coinRt = coinPill.GetComponent<RectTransform>();
            coinRt.anchorMin = new Vector2(0f, 1f);
            coinRt.anchorMax = new Vector2(0f, 1f);
            coinRt.pivot = new Vector2(0f, 1f);
            coinRt.anchoredPosition = new Vector2(30f, -22f);
            coinRt.sizeDelta = new Vector2(265f, 88f);

            Image coinImg = coinPill.GetComponent<Image>();
            Sprite coinSprite = LoadSprite("bg_coin_ref");
            if (coinSprite == null) coinSprite = LoadSprite("ui_pill");
            if (coinSprite != null) coinImg.sprite = coinSprite;
            coinImg.color = Color.white;
            coinImg.preserveAspect = true;

            // Dinamik Altın Sayısı Text (Opsiyonel sayaç)
            GameObject coinCountGo = new GameObject("Count", typeof(RectTransform));
            coinCountGo.transform.SetParent(coinPill.transform, false);
            RectTransform ccRt = coinCountGo.GetComponent<RectTransform>();
            ccRt.anchorMin = new Vector2(0.32f, 0f);
            ccRt.anchorMax = new Vector2(0.80f, 1f);
            ccRt.offsetMin = Vector2.zero;
            ccRt.offsetMax = Vector2.zero;
            TextMeshProUGUI coinTmp = coinCountGo.AddComponent<TextMeshProUGUI>();
            coinTmp.text = "250";
            if (font != null) coinTmp.font = font;
            coinTmp.fontSize = 42f;
            coinTmp.fontStyle = FontStyles.Bold;
            coinTmp.alignment = TextAlignmentOptions.Center;
            coinTmp.color = Color.white;
            coinTmp.outlineWidth = 0.28f;
            coinTmp.outlineColor = new Color32(40, 15, 60, 255);

            // 6. ORTA: Seviye Kapsülü (🐚 LEVEL 5 🐚)
            GameObject levelCapsuleGo = new GameObject("LevelCapsule", typeof(RectTransform), typeof(Image));
            levelCapsuleGo.transform.SetParent(topUIGo.transform, false);

            RectTransform levelCapRt = levelCapsuleGo.GetComponent<RectTransform>();
            levelCapRt.anchorMin = new Vector2(0.5f, 1f);
            levelCapRt.anchorMax = new Vector2(0.5f, 1f);
            levelCapRt.pivot = new Vector2(0.5f, 1f);
            levelCapRt.anchoredPosition = new Vector2(0f, -20f);
            levelCapRt.sizeDelta = new Vector2(365f, 98f);

            Image levelCapImg = levelCapsuleGo.GetComponent<Image>();
            Sprite capSprite = LoadSprite("bg_level_ref");
            if (capSprite == null) capSprite = LoadSprite("bg_level_capsule");
            if (capSprite != null) levelCapImg.sprite = capSprite;
            levelCapImg.color = Color.white;
            levelCapImg.preserveAspect = true;
            levelCapImg.raycastTarget = false;

            GameObject levelTextGo = new GameObject("LevelText", typeof(RectTransform));
            levelTextGo.transform.SetParent(levelCapsuleGo.transform, false);

            RectTransform ltRt = levelTextGo.GetComponent<RectTransform>();
            ltRt.anchorMin = Vector2.zero;
            ltRt.anchorMax = Vector2.one;
            ltRt.offsetMin = Vector2.zero;
            ltRt.offsetMax = Vector2.zero;

            TextMeshProUGUI levelTmp = levelTextGo.AddComponent<TextMeshProUGUI>();
            levelTmp.text = ""; // Görselin kendisinde pixel-perfect LEVEL 5 bulunuyor
            if (font != null) levelTmp.font = font;
            levelTmp.fontSize = 44f;
            levelTmp.fontStyle = FontStyles.Bold;
            levelTmp.alignment = TextAlignmentOptions.Center;
            levelTmp.color = Color.white;
            levelTmp.outlineWidth = 0.25f;
            levelTmp.outlineColor = new Color32(24, 60, 120, 255);

            // 7. SAĞ: Ses ve Ayarlar Butonları
            // 7a. Ses Butonu
            GameObject soundGo = new GameObject("SoundButton", typeof(RectTransform), typeof(Image), typeof(Button));
            soundGo.transform.SetParent(topUIGo.transform, false);

            RectTransform soundRt = soundGo.GetComponent<RectTransform>();
            soundRt.anchorMin = new Vector2(1f, 1f);
            soundRt.anchorMax = new Vector2(1f, 1f);
            soundRt.pivot = new Vector2(1f, 1f);
            soundRt.anchoredPosition = new Vector2(-138f, -22f);
            soundRt.sizeDelta = new Vector2(96f, 96f);

            Image soundImg = soundGo.GetComponent<Image>();
            Sprite soundOnSprite = LoadSprite("btn_sound_ref");
            if (soundOnSprite == null) soundOnSprite = LoadSprite("btn_sound");
            Sprite soundOffSprite = LoadSprite("btn_sound_off");
            if (soundOnSprite != null) soundImg.sprite = soundOnSprite;
            soundImg.preserveAspect = true;
            soundGo.AddComponent<CasualUIButtonJuice>();

            // 7b. Ayarlar Butonu
            GameObject settingsGo = new GameObject("SettingsButton", typeof(RectTransform), typeof(Image), typeof(Button));
            settingsGo.transform.SetParent(topUIGo.transform, false);

            RectTransform setRt = settingsGo.GetComponent<RectTransform>();
            setRt.anchorMin = new Vector2(1f, 1f);
            setRt.anchorMax = new Vector2(1f, 1f);
            setRt.pivot = new Vector2(1f, 1f);
            setRt.anchoredPosition = new Vector2(-30f, -22f);
            setRt.sizeDelta = new Vector2(96f, 96f);

            Image setImg = settingsGo.GetComponent<Image>();
            Sprite setSprite = LoadSprite("btn_settings_ref");
            if (setSprite == null) setSprite = LoadSprite("btn_settings");
            if (setSprite != null) setImg.sprite = setSprite;
            setImg.preserveAspect = true;
            settingsGo.AddComponent<CasualUIButtonJuice>();

            // Controller bağlantıları
            CasualHudController ctrl = canvasGo.GetComponent<CasualHudController>();
            if (ctrl == null) ctrl = canvasGo.AddComponent<CasualHudController>();
            ctrl.LevelText = levelTmp;
            ctrl.CoinsText = coinTmp;
            ctrl.SoundButtonImage = soundImg;
            ctrl.SoundOnSprite = soundOnSprite;
            ctrl.SoundOffSprite = soundOffSprite;

            Button soundBtn = soundGo.GetComponent<Button>();
            if (soundBtn != null)
            {
                UnityEditor.Events.UnityEventTools.AddPersistentListener(soundBtn.onClick, ctrl.ToggleSound);
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
