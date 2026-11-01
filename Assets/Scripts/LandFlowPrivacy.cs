using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGame
{
    /// <summary>
    /// Veri izni: "Allow" seçilmeden GameAnalytics ve Meta SDK başlatılmaz. İlk açılışta tek düğmeli ekran gösterilir;
    /// veri gönderimi HUD'daki "i" düğmesiyle (LandFlowPrivacyButton) her zaman kapatılabilir. İzin ekranı koddan kurulur.
    /// </summary>
    public static class LandFlowPrivacy
    {
        private const string PolicyUrl = "https://sites.google.com/view/pixelflowgame/ana-sayfa";
        private const string ConsentPrefKey = "LandFlow_DataConsent";

        private const int CardSpriteSize = 128;
        private const float CardCornerRadius = 44f;
        private const float CardBorderWidth = 7f;

        private static readonly Color s_CardColor = new Color(0.10f, 0.20f, 0.52f, 1f);
        private static readonly Color s_CardBorderColor = new Color(1f, 1f, 1f, 1f);
        private static readonly Color s_OverlayColor = new Color(0.03f, 0.06f, 0.14f, 0.80f);
        private static readonly Color s_BodyColor = new Color(0.86f, 0.92f, 1f, 1f);
        private static readonly Color s_TitleColor = new Color(1f, 0.84f, 0.30f, 1f);

        private static GameObject s_Dialog;
        private static Sprite s_CardSprite;

        public static event Action<bool> ConsentChanged;

        public static bool HasAnswered => PlayerPrefs.HasKey(ConsentPrefKey);
        public static bool Allowed => PlayerPrefs.GetInt(ConsentPrefKey, 0) == 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!HasAnswered) ShowDialog();
        }

        public static void SetConsent(bool allowed)
        {
            PlayerPrefs.SetInt(ConsentPrefKey, allowed ? 1 : 0);
            PlayerPrefs.Save();
            ConsentChanged?.Invoke(allowed);
        }

        public static void ShowDialog()
        {
            if (s_Dialog != null) return;

            CasualHudController hud = CasualHudController.Instance;
            TMP_FontAsset font = hud != null && hud.LevelText != null ? hud.LevelText.font : null;
            Sprite buttonSprite = hud != null && hud.LevelText != null ? hud.LevelText.transform.parent.GetComponent<Image>()?.sprite : null;

            s_Dialog = new GameObject("LandFlow_PrivacyDialog");
            UnityEngine.Object.DontDestroyOnLoad(s_Dialog);

            Canvas canvas = s_Dialog.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000; // Yükleme ekranının (9999) da üstünde
            CanvasScaler scaler = s_Dialog.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0f;
            s_Dialog.AddComponent<GraphicRaycaster>();

            // Arkadaki dokunmaları engeller
            RectTransform overlayRect = CreateRect("Overlay", s_Dialog.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image overlay = overlayRect.gameObject.AddComponent<Image>();
            overlay.color = s_OverlayColor;
            CanvasGroup overlayGroup = overlayRect.gameObject.AddComponent<CanvasGroup>();

            Vector2 center = new Vector2(0.5f, 0.5f);
            RectTransform card = CreateRect("Card", overlay.transform, center, center, Vector2.zero, new Vector2(900f, 780f));
            Image cardImage = card.gameObject.AddComponent<Image>();
            cardImage.sprite = GetCardSprite();
            cardImage.type = Image.Type.Sliced;
            cardImage.color = Color.white;

            // İlk açılışta tek düğme var; kapatma seçeneği "i" düğmesiyle açılan ekranda
            bool firstRun = !HasAnswered;
            string body = "We use GameAnalytics and Meta to improve the game and measure our ads. They may receive device info and identifiers.\n\n"
                + (firstRun ? "You can turn this off anytime with the \"i\" button." : "The game works the same either way. Change your choice anytime here.");

            TextMeshProUGUI title = CreateText("Title", card, new Vector2(0f, 300f), new Vector2(800f, 100f), "Your Privacy", 72f, font);
            title.color = s_TitleColor;
            TextMeshProUGUI bodyText = CreateText("Body", card, new Vector2(0f, 75f), new Vector2(780f, 320f), body, 38f, font);
            bodyText.color = s_BodyColor;

            string footer = HasAnswered ? (Allowed ? "Currently: Allowed" : "Currently: Not allowed") + "  |  " : "";
            TextMeshProUGUI link = CreateText("PolicyLink", card, new Vector2(0f, -135f), new Vector2(800f, 60f), footer + "<u>Privacy Policy</u>", 32f, font);
            link.color = s_BodyColor;
            link.raycastTarget = true;
            link.gameObject.AddComponent<Button>().onClick.AddListener(() => Application.OpenURL(PolicyUrl));

            if (!firstRun) CreateChoiceButton("Btn_Decline", card, -205f, "Don't Allow", false, buttonSprite, font);
            CreateChoiceButton("Btn_Allow", card, firstRun ? 0f : 205f, "Allow", true, buttonSprite, font);

            overlayGroup.alpha = 0f;
            overlayGroup.DOFade(1f, 0.2f).SetUpdate(true).SetLink(s_Dialog);
            card.localScale = Vector3.one * 0.88f;
            card.DOScale(1f, 0.28f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(s_Dialog);
        }

        private static void CreateChoiceButton(string name, RectTransform parent, float x, string label, bool allow, Sprite sprite, TMP_FontAsset font)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -265f), new Vector2(380f, 147f));
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            rect.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                UnityEngine.Object.Destroy(s_Dialog);
                s_Dialog = null;
                SetConsent(allow);
            });
            CreateText("Label", rect, new Vector2(0f, 7f), new Vector2(340f, 110f), label, 46f, font);
        }

        // Paketteki panel görselleri 9-slice'a uygun değil; yuvarlak köşeli kart koddan üretilir
        private static Sprite GetCardSprite()
        {
            if (s_CardSprite != null) return s_CardSprite;

            var texture = new Texture2D(CardSpriteSize, CardSpriteSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float half = CardSpriteSize * 0.5f;
            for (int y = 0; y < CardSpriteSize; y++)
            {
                for (int x = 0; x < CardSpriteSize; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - (half - CardCornerRadius), 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - (half - CardCornerRadius), 0f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy) - CardCornerRadius;

                    Color color = Color.Lerp(s_CardColor, s_CardBorderColor, Mathf.Clamp01(distance + CardBorderWidth + 0.5f));
                    color.a = Mathf.Clamp01(0.5f - distance);
                    texture.SetPixel(x, y, color);
                }
            }
            texture.Apply();

            float slice = CardCornerRadius + 4f;
            s_CardSprite = Sprite.Create(texture, new Rect(0f, 0f, CardSpriteSize, CardSpriteSize), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(slice, slice, slice, slice));
            return s_CardSprite;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static TextMeshProUGUI CreateText(string name, RectTransform parent, Vector2 position, Vector2 size, string text, float fontSize, TMP_FontAsset font)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
