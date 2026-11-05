using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGame
{
    /// <summary>
    /// Güncelleme pop-up'ı: izin ekranıyla aynı kart, HUD'un LilitaOne yazı tipi ve mavi düğmesi. Ekran koddan kurulur.
    /// Zorunlu güncellemede "Later" yoktur ve arkadaki dokunmalar engellenir.
    /// </summary>
    public static class LandFlowUpdatePopup
    {
        private static readonly Color s_OverlayColor = new Color(0.03f, 0.06f, 0.14f, 0.80f);
        private static readonly Color s_BodyColor = new Color(0.86f, 0.92f, 1f, 1f);
        private static readonly Color s_TitleColor = new Color(1f, 0.84f, 0.30f, 1f);

        private static GameObject s_Dialog;
        private static bool s_Required;

        public static bool IsShowing => s_Dialog != null;

        public static void Show(string title, string message, string version, bool required, Action onUpdate, Action onLater)
        {
            if (s_Dialog != null)
            {
                if (s_Required == required) return;
                Hide();
            }
            s_Required = required;

            CasualHudController hud = CasualHudController.Instance;
            TMP_FontAsset font = hud != null && hud.LevelText != null ? hud.LevelText.font : null;
            Sprite buttonSprite = hud != null && hud.LevelText != null ? hud.LevelText.transform.parent.GetComponent<Image>()?.sprite : null;

            s_Dialog = new GameObject("LandFlow_UpdateDialog");
            UnityEngine.Object.DontDestroyOnLoad(s_Dialog);

            Canvas canvas = s_Dialog.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10001; // Yükleme ekranı (9999) ve izin ekranının (10000) üstünde
            CanvasScaler scaler = s_Dialog.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0f;
            s_Dialog.AddComponent<GraphicRaycaster>();

            // Arkadaki dokunmaları engeller
            RectTransform overlayRect = LandFlowPrivacy.CreateRect("Overlay", s_Dialog.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image overlay = overlayRect.gameObject.AddComponent<Image>();
            overlay.color = s_OverlayColor;
            CanvasGroup overlayGroup = overlayRect.gameObject.AddComponent<CanvasGroup>();

            float height = required ? 680f : 780f;
            float top = height * 0.5f;
            Vector2 center = new Vector2(0.5f, 0.5f);
            RectTransform card = LandFlowPrivacy.CreateRect("Card", overlay.transform, center, center, Vector2.zero, new Vector2(900f, height));
            Image cardImage = card.gameObject.AddComponent<Image>();
            cardImage.sprite = LandFlowPrivacy.GetCardSprite();
            cardImage.type = Image.Type.Sliced;
            cardImage.color = Color.white;

            TextMeshProUGUI titleText = LandFlowPrivacy.CreateText("Title", card, new Vector2(0f, top - 110f), new Vector2(800f, 100f), title, 72f, font);
            titleText.color = s_TitleColor;
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 44f;
            titleText.fontSizeMax = 72f;

            TextMeshProUGUI versionText = LandFlowPrivacy.CreateText("Version", card, new Vector2(0f, top - 190f), new Vector2(800f, 50f), "Version " + version, 34f, font);
            versionText.color = s_BodyColor;

            TextMeshProUGUI messageText = LandFlowPrivacy.CreateText("Message", card, new Vector2(0f, top - 320f), new Vector2(780f, 200f), message, 38f, font);
            messageText.color = s_BodyColor;
            messageText.enableAutoSizing = true;
            messageText.fontSizeMin = 26f;
            messageText.fontSizeMax = 38f;

            CreateButton(card, buttonSprite, font, new Vector2(0f, -top + (required ? 120f : 190f)), () =>
            {
                if (!s_Required) Hide();
                onUpdate?.Invoke();
            });

            if (!required)
            {
                TextMeshProUGUI later = LandFlowPrivacy.CreateText("Btn_Later", card, new Vector2(0f, -top + 75f), new Vector2(400f, 70f), "<u>Later</u>", 36f, font);
                later.color = s_BodyColor;
                later.raycastTarget = true;
                later.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                {
                    Hide();
                    onLater?.Invoke();
                });
            }

            overlayGroup.alpha = 0f;
            overlayGroup.DOFade(1f, 0.2f).SetUpdate(true).SetLink(s_Dialog);
            card.localScale = Vector3.one * 0.88f;
            card.DOScale(1f, 0.28f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(s_Dialog);
        }

        public static void Hide()
        {
            if (s_Dialog == null) return;
            UnityEngine.Object.Destroy(s_Dialog);
            s_Dialog = null;
        }

        private static void CreateButton(RectTransform parent, Sprite sprite, TMP_FontAsset font, Vector2 position, Action onClick)
        {
            RectTransform rect = LandFlowPrivacy.CreateRect("Btn_Update", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(560f, 147f));
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            rect.gameObject.AddComponent<Button>().onClick.AddListener(() => onClick());
            LandFlowPrivacy.CreateText("Label", rect, new Vector2(0f, 7f), new Vector2(520f, 110f), "Update", 56f, font);
        }
    }
}
