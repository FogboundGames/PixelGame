using TMPro;
using UnityEngine;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// Üst HUD şeridini (seviye, can, altın, 2X hız göstergesi) günceller.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Casual Hud Controller")]
    public class CasualHudController : MonoBehaviour
    {
        public static CasualHudController Instance { get; private set; }

        [Header("🔗 Referanslar")]
        [SerializeField] private TextMeshProUGUI m_LevelText;
        [SerializeField] private TextMeshProUGUI m_LivesText;
        [SerializeField] private TextMeshProUGUI m_CoinsText;
        [SerializeField] private TextMeshProUGUI m_SubHeaderLevelText;
        [SerializeField] private GameObject m_HardBadge;

        [Header("🐰 Tavşan Maskotu & Zorluk")]
        [SerializeField] private GameObject m_RabbitMascot;
        [SerializeField] private Sprite m_RabbitSprite;
        [SerializeField] private Sprite m_HardPillSprite;

        [Header("⚡ 2X Hız Göstergesi")]
        [SerializeField] private GameObject m_TurboBadge;

        [Header("❌ Seviye Başarısız (Fail) Modal")]
        [SerializeField] private GameObject m_LevelFailPopup;

        [Header("🏆 Seviye Tamamlandı Modal")]
        [SerializeField] private GameObject m_LevelCompletePopup;
        [Tooltip("Pano görseli (başlık ve açıklama yazısı görselin içinde).")]
        [SerializeField] private Sprite m_CompletePanelSprite;
        [Tooltip("Altın ödülü pill'i (coin ve '+20' görselin içinde).")]
        [SerializeField] private Sprite m_CompleteRewardSprite;
        [Tooltip("DEVAM butonu görseli (yazı görselin içinde).")]
        [SerializeField] private Sprite m_CompleteContinueSprite;

        [Header("🎛️ Buton Görselleri & Durumlar")]
        [SerializeField] private UnityEngine.UI.Image m_SoundButtonImage;
        [SerializeField] private Sprite m_SoundOnSprite;
        [SerializeField] private Sprite m_SoundOffSprite;
        [SerializeField] private UnityEngine.UI.Image m_HapticsButtonImage;
        [SerializeField] private Sprite m_HapticsOnSprite;
        [SerializeField] private Sprite m_HapticsOffSprite;

        [Header("🧪 Başlangıç Değerleri")]
        [SerializeField] private int m_StartingLives = 3;
        [Tooltip("Oyuncunun hiç kaydı yokken (ilk açılış) başlayacağı altın. Sonrasında altın cihazda kayıtlıdır.")]
        [SerializeField] private int m_StartingCoins = 250;
        [SerializeField] private bool m_IsHardLevel = false;

        private int m_CurrentLives;
        private int m_CurrentCoins;
        private const string CoinsPrefKey = "PixelGame_Coins";
        private System.Action m_OnLevelCompleteContinue;
        private int m_PendingReward;
        private TextMeshProUGUI m_RewardText;
        private bool m_SoundEnabled = true;
        private bool m_MusicEnabled = true;
        private bool m_HapticsEnabled = true;

        public TextMeshProUGUI LevelText { get => m_LevelText; set => m_LevelText = value; }
        public TextMeshProUGUI LivesText { get => m_LivesText; set => m_LivesText = value; }
        public TextMeshProUGUI CoinsText { get => m_CoinsText; set => m_CoinsText = value; }
        public TextMeshProUGUI SubHeaderLevelText { get => m_SubHeaderLevelText; set => m_SubHeaderLevelText = value; }
        public GameObject HardBadge { get => m_HardBadge; set => m_HardBadge = value; }
        public GameObject RabbitMascot { get => m_RabbitMascot; set => m_RabbitMascot = value; }
        public Sprite RabbitSprite { get => m_RabbitSprite; set => m_RabbitSprite = value; }
        public Sprite HardPillSprite { get => m_HardPillSprite; set => m_HardPillSprite = value; }

        public UnityEngine.UI.Image SoundButtonImage { get => m_SoundButtonImage; set => m_SoundButtonImage = value; }
        public Sprite SoundOnSprite { get => m_SoundOnSprite; set => m_SoundOnSprite = value; }
        public Sprite SoundOffSprite { get => m_SoundOffSprite; set => m_SoundOffSprite = value; }
        public UnityEngine.UI.Image HapticsButtonImage { get => m_HapticsButtonImage; set => m_HapticsButtonImage = value; }
        public Sprite HapticsOnSprite { get => m_HapticsOnSprite; set => m_HapticsOnSprite = value; }
        public Sprite HapticsOffSprite { get => m_HapticsOffSprite; set => m_HapticsOffSprite = value; }

        public int CurrentLives => m_CurrentLives;
        public int CurrentCoins => m_CurrentCoins;
        public bool SoundEnabled => m_SoundEnabled;
        public bool MusicEnabled => m_MusicEnabled;
        public bool HapticsEnabled => m_HapticsEnabled;

        private void Awake()
        {
            Instance = this;
            Time.timeScale = 1.0f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1.0f;
        }

        private void OnEnable()
        {
            PixelArtGenerator.LevelLoaded += HandleLevelLoaded;
            EnsureRabbitMascot();
        }

        private void OnDisable()
        {
            PixelArtGenerator.LevelLoaded -= HandleLevelLoaded;
        }

        private void Start()
        {
            EnsureRabbitMascot();
            SetLives(m_StartingLives);
            SetCoins(PlayerPrefs.HasKey(CoinsPrefKey) ? PlayerPrefs.GetInt(CoinsPrefKey) : m_StartingCoins);
            RefreshLevelFromScene();
            UpdateSoundVisual();
            UpdateHapticsVisual();
        }

        private void RefreshLevelFromScene()
        {
            LevelManager levelManager = FindFirstObjectByType<LevelManager>();
            PixelLevelData currentLevel = null;
            int number = 1;
            if (levelManager != null)
            {
                number = levelManager.CurrentLevelIndex + 1;
                currentLevel = levelManager.CurrentLevel;
            }
            else
            {
                PixelArtGenerator gen = FindFirstObjectByType<PixelArtGenerator>();
                if (gen != null && gen.ActiveLevelData != null)
                {
                    currentLevel = gen.ActiveLevelData;
                    number = currentLevel.LevelIndex;
                }
            }

            bool isHard = (currentLevel != null) ? currentLevel.IsHardLevel : false;
            SetLevel(number, isHard);
        }

        private void HandleLevelLoaded(PixelLevelData level)
        {
            HideLevelFailPopup();
            SetTurboIndicator(false);
            if (level != null)
            {
                LevelManager levelManager = FindFirstObjectByType<LevelManager>();
                int number = levelManager != null ? levelManager.CurrentLevelIndex + 1 : level.LevelIndex;
                SetLevel(number, level.IsHardLevel);
            }
            else
            {
                RefreshLevelFromScene();
            }
        }

        public void SetLevel(int number)
        {
            LevelManager levelManager = FindFirstObjectByType<LevelManager>();
            PixelLevelData currentLevel = levelManager != null ? levelManager.CurrentLevel : null;
            bool isHard = (currentLevel != null) ? currentLevel.IsHardLevel : m_IsHardLevel;
            SetLevel(number, isHard);
        }

        public void SetLevel(int number, bool isHard)
        {
            int safeNum = Mathf.Max(1, number);
            if (m_LevelText != null) m_LevelText.text = $"LEVEL {safeNum}";
            if (m_SubHeaderLevelText != null) m_SubHeaderLevelText.text = $"Level {safeNum}";
            SetHardBadge(isHard);
        }

        public void SetHardBadge(bool active)
        {
            m_IsHardLevel = active;
            EnsureRabbitMascot();
            if (m_HardBadge != null) m_HardBadge.SetActive(active);
            if (m_RabbitMascot != null) m_RabbitMascot.SetActive(true);
        }

        /// <summary>
        /// Tavşan maskotunun her zaman sahnede kalmasını ve HARD pembe hapının sadece zor bölümlerde gelmesini sağlar.
        /// Kullanıcı isteği: "hard kısmı her levelde gelsin istemiyorum zor olarak belirlenenlere gelsin istiyorum sadece onun harici tavşan kalabilir hep"
        /// </summary>
        public void EnsureRabbitMascot()
        {
            // 1. HardBadge nesnesini bul
            if (m_HardBadge == null)
            {
                Transform foundHard = transform.Find("TopUI/SubHeader/HardBadge");
                if (foundHard == null) foundHard = transform.Find("SubHeader/HardBadge");
                if (foundHard == null)
                {
                    var allImgs = GetComponentsInChildren<UnityEngine.UI.Image>(true);
                    foreach (var img in allImgs)
                    {
                        if (img != null && img.gameObject.name == "HardBadge")
                        {
                            m_HardBadge = img.gameObject;
                            break;
                        }
                    }
                }
            }

            if (m_HardBadge == null) return;

            // 2. HardBadge görselini pembe HARD hapı (badge_hard_pill) olarak güncelle
            if (m_HardPillSprite == null)
            {
                m_HardPillSprite = FindSpriteByName("badge_hard_pill");
            }

            UnityEngine.UI.Image hardImg = m_HardBadge.GetComponent<UnityEngine.UI.Image>();
            if (hardImg != null && m_HardPillSprite != null && hardImg.sprite != m_HardPillSprite)
            {
                hardImg.sprite = m_HardPillSprite;
                hardImg.preserveAspect = true;
            }

            // 3. Tavşan Maskotu (RabbitMascot) nesnesini oluştur veya bul
            if (m_RabbitMascot == null)
            {
                Transform parentTr = m_HardBadge.transform.parent;
                Transform foundRabbit = parentTr != null ? parentTr.Find("RabbitMascot") : null;
                if (foundRabbit != null)
                {
                    m_RabbitMascot = foundRabbit.gameObject;
                }
                else if (parentTr != null)
                {
                    GameObject rGo = new GameObject("RabbitMascot", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                    rGo.transform.SetParent(parentTr, false);

                    RectTransform hardRt = m_HardBadge.GetComponent<RectTransform>();
                    RectTransform rRt = rGo.GetComponent<RectTransform>();
                    if (hardRt != null)
                    {
                        rRt.anchorMin = hardRt.anchorMin;
                        rRt.anchorMax = hardRt.anchorMax;
                        rRt.pivot = hardRt.pivot;
                        rRt.anchoredPosition = hardRt.anchoredPosition;
                        rRt.sizeDelta = hardRt.sizeDelta;
                    }

                    UnityEngine.UI.Image rImg = rGo.GetComponent<UnityEngine.UI.Image>();
                    if (m_RabbitSprite == null)
                    {
                        m_RabbitSprite = FindSpriteByName("mascot_bunny");
                    }
                    if (m_RabbitSprite != null)
                    {
                        rImg.sprite = m_RabbitSprite;
                    }
                    rImg.preserveAspect = true;
                    rImg.raycastTarget = false;

                    // Tavşan, pembe hapın hemen üzerinde görünsün diye sibling index'ini bir sonrasına al
                    rGo.transform.SetSiblingIndex(m_HardBadge.transform.GetSiblingIndex() + 1);
                    m_RabbitMascot = rGo;
                }
            }

            // 4. Tavşan maskotu DAİMA aktiftir (kullanıcı isteği: "onun harici tavşan kalabilir hep")
            if (m_RabbitMascot != null)
            {
                m_RabbitMascot.SetActive(true);
            }

            // 5. HARD pembe hapı sadece seviye zor ise görünür
            if (m_HardBadge != null)
            {
                m_HardBadge.SetActive(m_IsHardLevel);
            }
        }

        /// <summary>
        /// ⚡ 2X Turbo hız göstergesi.
        /// Kullanıcı isteği: "2x olduğunda da 2x ibaresi olmasın sahnemde bunu istemiyorum yani"
        /// </summary>
        public void SetTurboIndicator(bool active)
        {
            // Kullanıcı sahnede 2X ibaresi istemediği için rozet daima kapalı tutulur.
            if (m_TurboBadge != null && m_TurboBadge.activeSelf)
            {
                m_TurboBadge.SetActive(false);
            }
        }

        private void EnsureTurboBadge()
        {
            // Kullanıcı sahnede 2X ibaresi istemediği için rozet oluşturulmaz.
            if (m_TurboBadge != null)
            {
                m_TurboBadge.SetActive(false);
            }
        }

        public void ShowLevelFailPopup()
        {
            EnsureFailPopup();
            if (m_LevelFailPopup != null)
            {
                m_LevelFailPopup.SetActive(true);
                Transform card = m_LevelFailPopup.transform.Find("Fail_Card");
                if (card != null)
                {
                    card.DOKill(true);
                    card.localScale = Vector3.zero;
                    card.DOScale(Vector3.one, 0.38f).SetEase(Ease.OutBack).SetUpdate(true);
                }

                Transform btnObj = card != null ? card.Find("Btn_Retry_Modal") : null;
                if (btnObj != null)
                {
                    btnObj.DOKill(true);
                    btnObj.localScale = Vector3.one;
                    btnObj.DOScale(Vector3.one * 1.05f, 0.5f)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetEase(Ease.InOutSine)
                        .SetUpdate(true);
                }
            }
        }

        public void HideLevelFailPopup()
        {
            if (m_LevelFailPopup != null && m_LevelFailPopup.activeSelf)
            {
                Transform card = m_LevelFailPopup.transform.Find("Fail_Card");
                if (card != null)
                {
                    card.DOKill(true);
                    card.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).SetUpdate(true)
                        .OnComplete(() => m_LevelFailPopup.SetActive(false));
                }
                else
                {
                    m_LevelFailPopup.SetActive(false);
                }
            }
        }

        private Sprite FindSpriteByName(string nameSnippet)
        {
            var allImgs = GetComponentsInChildren<UnityEngine.UI.Image>(true);
            foreach (var img in allImgs)
            {
                if (img != null && img.sprite != null && img.sprite.name.IndexOf(nameSnippet, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return img.sprite;
                }
            }
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets(nameSnippet + " t:Sprite");
            if (guids != null && guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                Sprite sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp != null) return sp;
            }
#endif
            return null;
        }

        private void EnsureFailPopup()
        {
            if (m_LevelFailPopup != null) return;

            Sprite pillSprite = FindSpriteByName("ui_pill");
            Sprite restartSprite = FindSpriteByName("btn_restart");
            TMP_FontAsset font = (m_LevelText != null) ? m_LevelText.font : null;

            // 1. Root Overlay
            GameObject popupRoot = new GameObject("LevelFail_Popup");
            popupRoot.transform.SetParent(transform, false);

            RectTransform rootRect = popupRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;
            rootRect.anchoredPosition = Vector2.zero;

            UnityEngine.UI.Image overlay = popupRoot.AddComponent<UnityEngine.UI.Image>();
            overlay.color = new Color(0.03f, 0.06f, 0.12f, 0.85f);
            overlay.raycastTarget = true; // Arkadaki tüm dokunmaları ve sürüklemeleri engeller

            // 2. Ana Kart (Fail_Card)
            GameObject cardObj = new GameObject("Fail_Card");
            cardObj.transform.SetParent(popupRoot.transform, false);

            RectTransform cardRect = cardObj.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(560f, 480f);
            cardRect.anchoredPosition = new Vector2(0f, 30f);

            UnityEngine.UI.Image cardImg = cardObj.AddComponent<UnityEngine.UI.Image>();
            if (pillSprite != null)
            {
                cardImg.sprite = pillSprite;
                cardImg.type = UnityEngine.UI.Image.Type.Sliced;
            }
            cardImg.color = new Color(0.10f, 0.14f, 0.24f, 0.98f);

            UnityEngine.UI.Outline cardOutline = cardObj.AddComponent<UnityEngine.UI.Outline>();
            cardOutline.effectColor = new Color(0.95f, 0.35f, 0.30f, 0.65f);
            cardOutline.effectDistance = new Vector2(3f, -3f);

            // 3. Üst Başlık Rozeti (Header_Badge)
            GameObject headerObj = new GameObject("Header_Badge");
            headerObj.transform.SetParent(cardObj.transform, false);

            RectTransform headerRect = headerObj.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.5f, 1f);
            headerRect.anchorMax = new Vector2(0.5f, 1f);
            headerRect.pivot = new Vector2(0.5f, 0.5f);
            headerRect.anchoredPosition = new Vector2(0f, 0f);
            headerRect.sizeDelta = new Vector2(400f, 70f);

            UnityEngine.UI.Image headerBg = headerObj.AddComponent<UnityEngine.UI.Image>();
            if (pillSprite != null)
            {
                headerBg.sprite = pillSprite;
                headerBg.type = UnityEngine.UI.Image.Type.Sliced;
            }
            headerBg.color = new Color(0.92f, 0.22f, 0.25f, 1f);

            UnityEngine.UI.Outline headerOutline = headerObj.AddComponent<UnityEngine.UI.Outline>();
            headerOutline.effectColor = new Color(0.40f, 0.08f, 0.10f, 0.95f);
            headerOutline.effectDistance = new Vector2(2f, -3f);

            GameObject headerTextObj = new GameObject("HeaderText");
            headerTextObj.transform.SetParent(headerObj.transform, false);
            RectTransform htRect = headerTextObj.AddComponent<RectTransform>();
            htRect.anchorMin = Vector2.zero;
            htRect.anchorMax = Vector2.one;
            htRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI headerTmp = headerTextObj.AddComponent<TextMeshProUGUI>();
            headerTmp.text = "SEVİYE BAŞARISIZ";
            headerTmp.fontSize = 32;
            headerTmp.fontStyle = FontStyles.Bold;
            headerTmp.alignment = TextAlignmentOptions.Center;
            headerTmp.color = Color.white;
            if (font != null) headerTmp.font = font;

            // 4. Görsel İkon (Retry / Broken Icon)
            GameObject iconObj = new GameObject("Fail_Icon");
            iconObj.transform.SetParent(cardObj.transform, false);
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, -120f);
            iconRect.sizeDelta = new Vector2(84f, 84f);

            UnityEngine.UI.Image iconImg = iconObj.AddComponent<UnityEngine.UI.Image>();
            if (restartSprite != null)
            {
                iconImg.sprite = restartSprite;
                iconImg.preserveAspect = true;
                iconImg.color = new Color(1f, 0.45f, 0.45f, 1f);
            }

            // 5. Açıklama Metni (Fail_Desc)
            GameObject descObj = new GameObject("Fail_Desc");
            descObj.transform.SetParent(cardObj.transform, false);
            RectTransform descRect = descObj.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.5f, 0.5f);
            descRect.anchorMax = new Vector2(0.5f, 0.5f);
            descRect.pivot = new Vector2(0.5f, 0.5f);
            descRect.anchoredPosition = new Vector2(0f, -25f);
            descRect.sizeDelta = new Vector2(480f, 90f);

            TextMeshProUGUI descTmp = descObj.AddComponent<TextMeshProUGUI>();
            descTmp.text = "Tüm slotlar doldu ve\nuygun hamle kalmadı!";
            descTmp.fontSize = 24;
            descTmp.lineSpacing = 15f;
            descTmp.alignment = TextAlignmentOptions.Center;
            descTmp.color = new Color(0.88f, 0.92f, 0.98f, 0.95f);
            if (font != null) descTmp.font = font;

            // 6. Tekrar Dene Butonu (Btn_Retry_Modal)
            GameObject btnObj = new GameObject("Btn_Retry_Modal");
            btnObj.transform.SetParent(cardObj.transform, false);
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0f);
            btnRect.anchorMax = new Vector2(0.5f, 0f);
            btnRect.pivot = new Vector2(0.5f, 0f);
            btnRect.anchoredPosition = new Vector2(0f, 40f);
            btnRect.sizeDelta = new Vector2(380f, 84f);

            UnityEngine.UI.Image btnImg = btnObj.AddComponent<UnityEngine.UI.Image>();
            if (pillSprite != null)
            {
                btnImg.sprite = pillSprite;
                btnImg.type = UnityEngine.UI.Image.Type.Sliced;
            }
            btnImg.color = new Color(0.18f, 0.80f, 0.44f, 1f);

            UnityEngine.UI.Outline btnOutline = btnObj.AddComponent<UnityEngine.UI.Outline>();
            btnOutline.effectColor = new Color(0.08f, 0.45f, 0.22f, 0.95f);
            btnOutline.effectDistance = new Vector2(2f, -3f);

            UnityEngine.UI.Button btn = btnObj.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(() =>
            {
                btnObj.transform.DOPunchScale(Vector3.one * -0.08f, 0.15f).SetUpdate(true).OnComplete(() =>
                {
                    RestartLevel();
                });
            });

            // Buton içindeki İkon
            if (restartSprite != null)
            {
                GameObject btnIconObj = new GameObject("Icon");
                btnIconObj.transform.SetParent(btnObj.transform, false);
                RectTransform btnIconRect = btnIconObj.AddComponent<RectTransform>();
                btnIconRect.anchorMin = new Vector2(0f, 0.5f);
                btnIconRect.anchorMax = new Vector2(0f, 0.5f);
                btnIconRect.pivot = new Vector2(0.5f, 0.5f);
                btnIconRect.anchoredPosition = new Vector2(56f, 0f);
                btnIconRect.sizeDelta = new Vector2(44f, 44f);

                UnityEngine.UI.Image bIcon = btnIconObj.AddComponent<UnityEngine.UI.Image>();
                bIcon.sprite = restartSprite;
                bIcon.preserveAspect = true;
                bIcon.color = Color.white;
            }

            // Buton içindeki Metin
            GameObject btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = new Vector2(80f, 0f);
            btnTextRect.offsetMax = new Vector2(-20f, 0f);

            TextMeshProUGUI btnTmp = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnTmp.text = "TEKRAR DENE";
            btnTmp.fontSize = 28;
            btnTmp.fontStyle = FontStyles.Bold;
            btnTmp.alignment = TextAlignmentOptions.Center;
            btnTmp.color = Color.white;
            if (font != null) btnTmp.font = font;

            m_LevelFailPopup = popupRoot;
            m_LevelFailPopup.SetActive(false);
        }

        public void RestartLevel()
        {
            HideLevelFailPopup();
            Time.timeScale = 1.0f;
            Debug.Log("<color=#FF4444><b>[CasualHUD]</b></color> Seviye yeniden başlatılıyor...");
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(activeScene.buildIndex);
        }

        public void ToggleSound()
        {
            m_SoundEnabled = !m_SoundEnabled;
            AudioListener.pause = !m_SoundEnabled;
            UpdateSoundVisual();
            Debug.Log($"<color=#44FF44><b>[CasualHUD]</b></color> Ses: {(m_SoundEnabled ? "Açık" : "Kapalı")}");
        }

        private void UpdateSoundVisual()
        {
            if (m_SoundButtonImage != null)
            {
                if (m_SoundEnabled && m_SoundOnSprite != null) m_SoundButtonImage.sprite = m_SoundOnSprite;
                else if (!m_SoundEnabled && m_SoundOffSprite != null) m_SoundButtonImage.sprite = m_SoundOffSprite;
            }
        }

        public void ToggleMusic()
        {
            m_MusicEnabled = !m_MusicEnabled;
            Debug.Log($"<color=#44FF44><b>[CasualHUD]</b></color> Müzik: {(m_MusicEnabled ? "Açık" : "Kapalı")}");
        }

        public void ToggleHaptics()
        {
            m_HapticsEnabled = !m_HapticsEnabled;
            UpdateHapticsVisual();
            Debug.Log($"<color=#44FF44><b>[CasualHUD]</b></color> Titreşim/Haptic: {(m_HapticsEnabled ? "Açık" : "Engelli")}");
        }

        private void UpdateHapticsVisual()
        {
            if (m_HapticsButtonImage != null)
            {
                if (m_HapticsEnabled && m_HapticsOnSprite != null) m_HapticsButtonImage.sprite = m_HapticsOnSprite;
                else if (!m_HapticsEnabled && m_HapticsOffSprite != null) m_HapticsButtonImage.sprite = m_HapticsOffSprite;
            }
        }

        public void SetLives(int count)
        {
            m_CurrentLives = Mathf.Max(0, count);
            if (m_LivesText != null) m_LivesText.text = m_CurrentLives.ToString();
        }

        public void SetCoins(int count)
        {
            m_CurrentCoins = Mathf.Max(0, count);
            if (m_CoinsText != null) m_CoinsText.text = FormatCoins(m_CurrentCoins);
            PlayerPrefs.SetInt(CoinsPrefKey, m_CurrentCoins);
        }

        /// <summary>
        /// Altın ekler, cihaza kaydeder; sayaç hafifçe zıplar ve üstünde "+N" yazısı yükselip söner.
        /// </summary>
        public void AddCoins(int amount)
        {
            if (amount == 0) return;
            SetCoins(m_CurrentCoins + amount);
            PlayerPrefs.Save();

            if (m_CoinsText == null) return;
            Transform pill = m_CoinsText.transform.parent != null ? m_CoinsText.transform.parent : m_CoinsText.transform;
            pill.DOKill(true);
            pill.DOPunchScale(Vector3.one * 0.18f, 0.35f, 6, 0.6f).SetUpdate(true);

            GameObject floatObj = new GameObject("CoinGain");
            floatObj.transform.SetParent(m_CoinsText.transform, false);
            RectTransform fr = floatObj.AddComponent<RectTransform>();
            fr.anchorMin = fr.anchorMax = new Vector2(0.5f, 0f);
            fr.sizeDelta = new Vector2(200f, 60f);
            fr.anchoredPosition = new Vector2(0f, -10f);
            TextMeshProUGUI tmp = floatObj.AddComponent<TextMeshProUGUI>();
            tmp.text = (amount > 0 ? "+" : "") + amount;
            tmp.fontSize = 40;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.85f, 0.25f, 1f);
            tmp.raycastTarget = false;
            if (m_CoinsText.font != null) tmp.font = m_CoinsText.font;
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = new Color32(90, 50, 0, 255);

            fr.DOAnchorPosY(-90f, 0.8f).SetEase(Ease.OutCubic).SetUpdate(true);
            tmp.DOFade(0f, 0.8f).SetDelay(0.25f).SetUpdate(true).OnComplete(() => Destroy(floatObj));
        }

        /// <summary>
        /// Resim tamamlanınca gösterilir: kazanılan altını gösterir; "DEVAM"a basılınca altın eklenir,
        /// pencere kapanır ve <paramref name="onContinue"/> (bir sonraki seviyeye geçiş) çağrılır.
        /// </summary>
        public void ShowLevelCompletePopup(int reward, System.Action onContinue)
        {
            EnsureLevelCompletePopup();
            m_PendingReward = reward;
            m_OnLevelCompleteContinue = onContinue;
            if (m_RewardText != null) m_RewardText.text = "+" + reward + " ALTIN";

            m_LevelCompletePopup.SetActive(true);
            Transform card = m_LevelCompletePopup.transform.Find("Complete_Card");
            if (card != null)
            {
                card.DOKill(true);
                card.localScale = Vector3.zero;
                card.DOScale(Vector3.one, 0.42f).SetEase(Ease.OutBack).SetUpdate(true);

                Transform reward_ = card.Find("Reward");
                if (reward_ != null)
                {
                    reward_.DOKill(true);
                    reward_.localScale = Vector3.one;
                    reward_.DOScale(Vector3.one * 1.08f, 0.55f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
                }
            }
        }

        private void OnLevelCompleteContinueClicked()
        {
            if (m_LevelCompletePopup == null || !m_LevelCompletePopup.activeSelf) return;

            int reward = m_PendingReward;
            m_PendingReward = 0;
            System.Action next = m_OnLevelCompleteContinue;
            m_OnLevelCompleteContinue = null;

            Transform card = m_LevelCompletePopup.transform.Find("Complete_Card");
            if (card != null)
            {
                card.DOKill(true);
                foreach (Transform c in card) c.DOKill(true);
                card.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).SetUpdate(true)
                    .OnComplete(() => m_LevelCompletePopup.SetActive(false));
            }
            else
            {
                m_LevelCompletePopup.SetActive(false);
            }

            AddCoins(reward);
            next?.Invoke();
        }

        private void EnsureLevelCompletePopup()
        {
            if (m_LevelCompletePopup != null) return;

            if (m_CompletePanelSprite != null && m_CompleteRewardSprite != null && m_CompleteContinueSprite != null)
            {
                BuildSpriteLevelCompletePopup();
                return;
            }

            Sprite pillSprite = FindSpriteByName("ui_pill");
            TMP_FontAsset font = (m_LevelText != null) ? m_LevelText.font : null;

            GameObject popupRoot = new GameObject("LevelComplete_Popup");
            popupRoot.transform.SetParent(transform, false);
            RectTransform rootRect = popupRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;
            UnityEngine.UI.Image overlay = popupRoot.AddComponent<UnityEngine.UI.Image>();
            overlay.color = new Color(0.03f, 0.08f, 0.12f, 0.75f);
            overlay.raycastTarget = true;

            GameObject cardObj = new GameObject("Complete_Card");
            cardObj.transform.SetParent(popupRoot.transform, false);
            RectTransform cardRect = cardObj.AddComponent<RectTransform>();
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(560f, 460f);
            cardRect.anchoredPosition = new Vector2(0f, 30f);
            UnityEngine.UI.Image cardImg = cardObj.AddComponent<UnityEngine.UI.Image>();
            if (pillSprite != null) { cardImg.sprite = pillSprite; cardImg.type = UnityEngine.UI.Image.Type.Sliced; }
            cardImg.color = new Color(0.10f, 0.20f, 0.30f, 0.98f);
            UnityEngine.UI.Outline cardOutline = cardObj.AddComponent<UnityEngine.UI.Outline>();
            cardOutline.effectColor = new Color(0.35f, 0.85f, 0.55f, 0.65f);
            cardOutline.effectDistance = new Vector2(3f, -3f);

            // Başlık
            GameObject headerObj = new GameObject("Header_Badge");
            headerObj.transform.SetParent(cardObj.transform, false);
            RectTransform headerRect = headerObj.AddComponent<RectTransform>();
            headerRect.anchorMin = headerRect.anchorMax = new Vector2(0.5f, 1f);
            headerRect.sizeDelta = new Vector2(400f, 70f);
            UnityEngine.UI.Image headerBg = headerObj.AddComponent<UnityEngine.UI.Image>();
            if (pillSprite != null) { headerBg.sprite = pillSprite; headerBg.type = UnityEngine.UI.Image.Type.Sliced; }
            headerBg.color = new Color(0.18f, 0.72f, 0.42f, 1f);
            UnityEngine.UI.Outline headerOutline = headerObj.AddComponent<UnityEngine.UI.Outline>();
            headerOutline.effectColor = new Color(0.06f, 0.35f, 0.18f, 0.95f);
            headerOutline.effectDistance = new Vector2(2f, -3f);
            CreatePopupText(headerObj.transform, "HeaderText", "TAMAMLANDI!", 34, Color.white, font, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Açıklama
            CreatePopupText(cardObj.transform, "Complete_Desc", "Resmin tamamı\ngemilere yüklendi!", 26,
                new Color(0.88f, 0.94f, 0.98f, 0.95f), font,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -135f), new Vector2(480f, 90f));

            // Ödül
            GameObject rewardObj = new GameObject("Reward");
            rewardObj.transform.SetParent(cardObj.transform, false);
            RectTransform rewardRect = rewardObj.AddComponent<RectTransform>();
            rewardRect.anchorMin = rewardRect.anchorMax = new Vector2(0.5f, 0.5f);
            rewardRect.anchoredPosition = new Vector2(0f, -25f);
            rewardRect.sizeDelta = new Vector2(360f, 80f);
            UnityEngine.UI.Image rewardBg = rewardObj.AddComponent<UnityEngine.UI.Image>();
            if (pillSprite != null) { rewardBg.sprite = pillSprite; rewardBg.type = UnityEngine.UI.Image.Type.Sliced; }
            rewardBg.color = new Color(0.05f, 0.10f, 0.16f, 0.9f);
            m_RewardText = CreatePopupText(rewardObj.transform, "RewardText", "+0 ALTIN", 38,
                new Color(1f, 0.85f, 0.25f, 1f), font, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Devam butonu
            GameObject btnObj = new GameObject("Btn_Continue");
            btnObj.transform.SetParent(cardObj.transform, false);
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = btnRect.anchorMax = new Vector2(0.5f, 0f);
            btnRect.pivot = new Vector2(0.5f, 0f);
            btnRect.anchoredPosition = new Vector2(0f, 40f);
            btnRect.sizeDelta = new Vector2(380f, 84f);
            UnityEngine.UI.Image btnImg = btnObj.AddComponent<UnityEngine.UI.Image>();
            if (pillSprite != null) { btnImg.sprite = pillSprite; btnImg.type = UnityEngine.UI.Image.Type.Sliced; }
            btnImg.color = new Color(0.18f, 0.80f, 0.44f, 1f);
            UnityEngine.UI.Outline btnOutline = btnObj.AddComponent<UnityEngine.UI.Outline>();
            btnOutline.effectColor = new Color(0.08f, 0.45f, 0.22f, 0.95f);
            btnOutline.effectDistance = new Vector2(2f, -3f);
            UnityEngine.UI.Button btn = btnObj.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(() =>
            {
                btnObj.transform.DOPunchScale(Vector3.one * -0.08f, 0.15f).SetUpdate(true)
                    .OnComplete(OnLevelCompleteContinueClicked);
            });
            CreatePopupText(btnObj.transform, "Text", "DEVAM", 30, Color.white, font, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            m_LevelCompletePopup = popupRoot;
            m_LevelCompletePopup.SetActive(false);
        }

        /// <summary>
        /// Piksel art görsellerle kurulan pencere: pano (başlık + açıklama görselde), altın pill'i ve
        /// DEVAM butonu. Yerleşim referans tasarıma göre pano genişliğine orantılıdır.
        /// </summary>
        private void BuildSpriteLevelCompletePopup()
        {
            const float panelWidth = 780f;

            GameObject popupRoot = new GameObject("LevelComplete_Popup");
            popupRoot.transform.SetParent(transform, false);
            RectTransform rootRect = popupRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;
            UnityEngine.UI.Image overlay = popupRoot.AddComponent<UnityEngine.UI.Image>();
            overlay.color = new Color(0.03f, 0.08f, 0.12f, 0.6f);
            overlay.raycastTarget = true;

            Vector2 panelSize = SpriteSize(m_CompletePanelSprite, panelWidth);
            GameObject card = CreateSpriteImage(popupRoot.transform, "Complete_Card", m_CompletePanelSprite, panelSize, new Vector2(0f, 40f));
            card.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;

            // Referansta: ödül pill'i panonun ~%58'i genişliğinde, merkezin biraz altında;
            // buton ~%71 genişliğinde, alt kenara yakın.
            CreateSpriteImage(card.transform, "Reward", m_CompleteRewardSprite,
                SpriteSize(m_CompleteRewardSprite, panelWidth * 0.58f), new Vector2(0f, -panelSize.y * 0.03f));

            GameObject btnObj = CreateSpriteImage(card.transform, "Btn_Continue", m_CompleteContinueSprite,
                SpriteSize(m_CompleteContinueSprite, panelWidth * 0.71f), new Vector2(0f, -panelSize.y * 0.29f));
            UnityEngine.UI.Image btnImg = btnObj.GetComponent<UnityEngine.UI.Image>();
            btnImg.raycastTarget = true;
            UnityEngine.UI.Button btn = btnObj.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = btnImg;
            btn.transition = UnityEngine.UI.Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                btnObj.transform.DOKill(true);
                btnObj.transform.DOPunchScale(Vector3.one * -0.08f, 0.15f).SetUpdate(true)
                    .OnComplete(OnLevelCompleteContinueClicked);
            });

            m_RewardText = null; // ödül miktarı görselin içinde
            m_LevelCompletePopup = popupRoot;
            m_LevelCompletePopup.SetActive(false);
        }

        private static Vector2 SpriteSize(Sprite sprite, float width)
        {
            return new Vector2(width, width * sprite.rect.height / Mathf.Max(1f, sprite.rect.width));
        }

        private static GameObject CreateSpriteImage(Transform parent, string name, Sprite sprite, Vector2 size, Vector2 pos)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            UnityEngine.UI.Image img = obj.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return obj;
        }

        private static TextMeshProUGUI CreatePopupText(Transform parent, string name, string text, float size, Color color,
                                                       TMP_FontAsset font, Vector2 anchorMin, Vector2 anchorMax,
                                                       Vector2 anchoredPos, Vector2 sizeDelta)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.raycastTarget = false;
            if (font != null) tmp.font = font;
            return tmp;
        }

        /// <summary>1000 üstü değerleri "22K" gibi kısaltır; casual HUD'larda yaygın gösterimdir.</summary>
        private static string FormatCoins(int count)
        {
            if (count >= 1000) return (count / 1000f).ToString("0.#") + "K";
            return count.ToString();
        }
    }
}
