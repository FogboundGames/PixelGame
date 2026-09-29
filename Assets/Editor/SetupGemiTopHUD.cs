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
        private const string AutoRunKey = "GemiTopHUD_Installed_v2";

        static SetupGemiTopHUD()
        {
            EditorApplication.delayCall += AutoRunIfNeeded;
        }

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

            // 1. HUD_Canvas (Screen Space - Overlay)
            GameObject canvasGo = GameObject.Find("HUD_Canvas");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("HUD_Canvas");
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create HUD_Canvas");
            }

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            if (canvas == null) canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

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
            topRt.sizeDelta = new Vector2(0f, 180f);

            // 4. Arka plan çentiği / banner (hud_top_banner.png)
            GameObject bannerGo = new GameObject("TopBanner", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(bannerGo, "Create TopBanner");
            bannerGo.transform.SetParent(topUIGo.transform, false);

            RectTransform bannerRt = bannerGo.GetComponent<RectTransform>();
            bannerRt.anchorMin = new Vector2(0f, 1f);
            bannerRt.anchorMax = new Vector2(1f, 1f);
            bannerRt.pivot = new Vector2(0.5f, 1f);
            bannerRt.anchoredPosition = Vector2.zero;
            bannerRt.sizeDelta = new Vector2(0f, 180f);

            Image bannerImg = bannerGo.GetComponent<Image>();
            Sprite bannerSprite = LoadSprite("hud_top_banner");
            if (bannerSprite != null) bannerImg.sprite = bannerSprite;
            bannerImg.color = Color.white;
            bannerImg.raycastTarget = false;

            // 5. Sol: Ayarlar Düğmesi (SettingsButton - Mor kare, beyaz dişli)
            GameObject settingsGo = new GameObject("SettingsButton", typeof(RectTransform), typeof(Image), typeof(Button));
            Undo.RegisterCreatedObjectUndo(settingsGo, "Create SettingsButton");
            settingsGo.transform.SetParent(topUIGo.transform, false);

            RectTransform setRt = settingsGo.GetComponent<RectTransform>();
            setRt.anchorMin = new Vector2(0f, 0.5f);
            setRt.anchorMax = new Vector2(0f, 0.5f);
            setRt.pivot = new Vector2(0.5f, 0.5f);
            setRt.anchoredPosition = new Vector2(90f, -8f);
            setRt.sizeDelta = new Vector2(104f, 104f);

            Image setImg = settingsGo.GetComponent<Image>();
            Sprite setSprite = LoadSprite("btn_settings");
            if (setSprite != null) setImg.sprite = setSprite;
            setImg.preserveAspect = true;
            settingsGo.AddComponent<CasualUIButtonJuice>();

            // 6. Sağ: İstatistikler Şeridi (LEVEL + Can + Altın)
            GameObject rowGo = new GameObject("StatsRow", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(rowGo, "Create StatsRow");
            rowGo.transform.SetParent(topUIGo.transform, false);

            RectTransform rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(1f, 0.5f);
            rowRt.anchorMax = new Vector2(1f, 0.5f);
            rowRt.pivot = new Vector2(1f, 0.5f);
            rowRt.anchoredPosition = new Vector2(-40f, -8f);

            HorizontalLayoutGroup layout = rowGo.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.spacing = 30f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = rowGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            // 6a. LEVEL 1 Etiketi
            GameObject levelGo = new GameObject("LevelLabel", typeof(RectTransform));
            levelGo.transform.SetParent(rowGo.transform, false);

            RectTransform levelRt = levelGo.GetComponent<RectTransform>();
            levelRt.sizeDelta = new Vector2(210f, 80f);

            TextMeshProUGUI levelTmp = levelGo.AddComponent<TextMeshProUGUI>();
            levelTmp.text = "LEVEL 1";
            if (font != null) levelTmp.font = font;
            levelTmp.fontSize = 50f;
            levelTmp.fontStyle = FontStyles.Bold;
            levelTmp.alignment = TextAlignmentOptions.MidlineRight;
            levelTmp.color = Color.white;
            levelTmp.outlineWidth = 0.20f;
            levelTmp.outlineColor = new Color32(16, 22, 40, 255);
            levelTmp.textWrappingMode = TextWrappingModes.NoWrap;

            LayoutElement levelLe = levelGo.AddComponent<LayoutElement>();
            levelLe.preferredWidth = 210f;
            levelLe.preferredHeight = 80f;

            // 6b. Can Hapı (HeartPill - Kırmızı kalp, 3, yeşil artı)
            GameObject heartPill = BuildPill(rowGo.transform, "HeartPill", "icon_heart", "3", font, 195f);

            // 6c. Altın Hapı (CoinPill - Sarı yıldızlı para, 250, yeşil artı)
            GameObject coinPill = BuildPill(rowGo.transform, "CoinPill", "icon_coin", "250", font, 205f);

            // 7. CasualHudController bağla
            CasualHudController ctrl = canvasGo.GetComponent<CasualHudController>();
            if (ctrl == null) ctrl = canvasGo.AddComponent<CasualHudController>();
            ctrl.LevelText = levelTmp;
            ctrl.LivesText = heartPill.transform.Find("Count")?.GetComponent<TextMeshProUGUI>();
            ctrl.CoinsText = coinPill.transform.Find("Count")?.GetComponent<TextMeshProUGUI>();

            // 8. Varsa eski SubHeader'ı temizle (idempotent)
            Transform oldSub = canvasGo.transform.Find("SubHeader");
            if (oldSub != null)
            {
                Undo.DestroyObjectImmediate(oldSub.gameObject);
            }

            // 9. SubHeader container (Seviye Aksiyon Barı: Restart + HARD + Level 35 + Sound + Haptic)
            GameObject subHeaderGo = new GameObject("SubHeader", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(subHeaderGo, "Create SubHeader");
            subHeaderGo.transform.SetParent(canvasGo.transform, false);

            RectTransform subRt = subHeaderGo.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0f, 1f);
            subRt.anchorMax = new Vector2(1f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.anchoredPosition = new Vector2(0f, -188f);
            subRt.sizeDelta = new Vector2(0f, 120f);

            // 9a. Restart Button (Kırmızı kare, altın çerçeveli yenileme düğmesi)
            GameObject restartGo = new GameObject("RestartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            Undo.RegisterCreatedObjectUndo(restartGo, "Create RestartButton");
            restartGo.transform.SetParent(subHeaderGo.transform, false);

            RectTransform restartRt = restartGo.GetComponent<RectTransform>();
            restartRt.anchorMin = new Vector2(0f, 0.5f);
            restartRt.anchorMax = new Vector2(0f, 0.5f);
            restartRt.pivot = new Vector2(0.5f, 0.5f);
            restartRt.anchoredPosition = new Vector2(75f, 0f);
            restartRt.sizeDelta = new Vector2(96f, 96f);

            Image restartImg = restartGo.GetComponent<Image>();
            Sprite restartSprite = LoadSprite("btn_restart");
            if (restartSprite != null) restartImg.sprite = restartSprite;
            restartImg.preserveAspect = true;
            restartGo.AddComponent<CasualUIButtonJuice>();
            Button restartBtn = restartGo.GetComponent<Button>();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(restartBtn.onClick, ctrl.RestartLevel);

            // 9b. HARD Rozeti (Alevli kafatası + 3D HARD yazısı)
            GameObject hardGo = new GameObject("HardBadge", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(hardGo, "Create HardBadge");
            hardGo.transform.SetParent(subHeaderGo.transform, false);

            RectTransform hardRt = hardGo.GetComponent<RectTransform>();
            hardRt.anchorMin = new Vector2(0f, 0.5f);
            hardRt.anchorMax = new Vector2(0f, 0.5f);
            hardRt.pivot = new Vector2(0f, 0.5f);
            hardRt.anchoredPosition = new Vector2(140f, 0f);
            hardRt.sizeDelta = new Vector2(190f, 76f);

            Image hardImg = hardGo.GetComponent<Image>();
            Sprite hardSprite = LoadSprite("badge_hard");
            if (hardSprite != null) hardImg.sprite = hardSprite;
            hardImg.preserveAspect = true;
            hardImg.raycastTarget = false;

            // 9c. Seviye Başlığı (Ortalanmış "Level 35")
            GameObject subLevelGo = new GameObject("LevelTitle", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(subLevelGo, "Create LevelTitle");
            subLevelGo.transform.SetParent(subHeaderGo.transform, false);

            RectTransform subLevelRt = subLevelGo.GetComponent<RectTransform>();
            subLevelRt.anchorMin = new Vector2(0.5f, 0.5f);
            subLevelRt.anchorMax = new Vector2(0.5f, 0.5f);
            subLevelRt.pivot = new Vector2(0.5f, 0.5f);
            subLevelRt.anchoredPosition = new Vector2(0f, 0f);
            subLevelRt.sizeDelta = new Vector2(340f, 80f);

            TextMeshProUGUI subLevelTmp = subLevelGo.AddComponent<TextMeshProUGUI>();
            subLevelTmp.text = "Level 35";
            if (font != null) subLevelTmp.font = font;
            subLevelTmp.fontSize = 58f;
            subLevelTmp.fontStyle = FontStyles.Bold;
            subLevelTmp.alignment = TextAlignmentOptions.Center;
            subLevelTmp.color = Color.white;
            subLevelTmp.outlineWidth = 0.22f;
            subLevelTmp.outlineColor = new Color32(20, 15, 30, 255);
            subLevelTmp.textWrappingMode = TextWrappingModes.NoWrap;

            // 9d. Sound Button (Yeşil kare, altın çerçeveli hoparlör düğmesi)
            GameObject soundGo = new GameObject("SoundButton", typeof(RectTransform), typeof(Image), typeof(Button));
            Undo.RegisterCreatedObjectUndo(soundGo, "Create SoundButton");
            soundGo.transform.SetParent(subHeaderGo.transform, false);

            RectTransform soundRt = soundGo.GetComponent<RectTransform>();
            soundRt.anchorMin = new Vector2(1f, 0.5f);
            soundRt.anchorMax = new Vector2(1f, 0.5f);
            soundRt.pivot = new Vector2(0.5f, 0.5f);
            soundRt.anchoredPosition = new Vector2(-185f, 0f);
            soundRt.sizeDelta = new Vector2(96f, 96f);

            Image soundImg = soundGo.GetComponent<Image>();
            Sprite soundSprite = LoadSprite("btn_sound");
            if (soundSprite != null) soundImg.sprite = soundSprite;
            soundImg.preserveAspect = true;
            soundGo.AddComponent<CasualUIButtonJuice>();
            Button soundBtn = soundGo.GetComponent<Button>();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(soundBtn.onClick, ctrl.ToggleSound);

            // 9e. Haptic Button (Yeşil kare, altın çerçeveli titreşim düğmesi)
            GameObject hapticGo = new GameObject("HapticButton", typeof(RectTransform), typeof(Image), typeof(Button));
            Undo.RegisterCreatedObjectUndo(hapticGo, "Create HapticButton");
            hapticGo.transform.SetParent(subHeaderGo.transform, false);

            RectTransform hapticRt = hapticGo.GetComponent<RectTransform>();
            hapticRt.anchorMin = new Vector2(1f, 0.5f);
            hapticRt.anchorMax = new Vector2(1f, 0.5f);
            hapticRt.pivot = new Vector2(0.5f, 0.5f);
            hapticRt.anchoredPosition = new Vector2(-75f, 0f);
            hapticRt.sizeDelta = new Vector2(96f, 96f);

            Image hapticImg = hapticGo.GetComponent<Image>();
            Sprite hapticSprite = LoadSprite("btn_haptic");
            if (hapticSprite != null) hapticImg.sprite = hapticSprite;
            hapticImg.preserveAspect = true;
            hapticGo.AddComponent<CasualUIButtonJuice>();
            Button hapticBtn = hapticGo.GetComponent<Button>();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(hapticBtn.onClick, ctrl.ToggleHaptics);

            // Controller bağla
            ctrl.SubHeaderLevelText = subLevelTmp;
            ctrl.HardBadge = hardGo;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("<color=#00FFAA><b>[SetupGemiTopHUD]</b></color> 📱 Gemi sahnesi üst HUD ve Seviye Aksiyon Barı (Restart + HARD + Level 35 + Sound + Haptic) başarıyla kuruldu!");
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
