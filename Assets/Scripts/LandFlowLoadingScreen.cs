using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// Land Flow Oyun Yükleme ve Şirket Açılış Ekranı (Studio Intro & Loading Screen).
    /// 
    /// 1. Faz (Oyun Açılışında):
    ///    - Şık ve karanlık Fogbound stüdyo logosu (siyah arka plan, ortalanmış beyaz Fogbound logosu,
    ///      hafif nefes alma animasyonu ve yumuşak geçiş).
    /// 
    /// 2. Faz (Land Flow Yükleme Ekranı):
    ///    - 9:16 dikey ekranı tamamen kaplayan (edge-to-edge), sevimli gemiler ve neşeli küplerle dolu
    ///      canlı tropikal Land Flow arka planı.
    ///    - "LAND FLOW" logosunun font ve 3D kabartma stiline birebir uygun "LOADING..." yazısı / 3D rozeti.
    ///    - Altın sarısı akıcı ilerleme çubuğu (Progress Bar).
    ///    - Oyun açılışında, level geçişlerinde ve level fail sonrası tamamen asenkron, takılmasız ve pürüzsüz çalışır.
    /// </summary>
    [DisallowMultipleComponent]
    public class LandFlowLoadingScreen : MonoBehaviour
    {
        private static LandFlowLoadingScreen s_Instance;
        public static LandFlowLoadingScreen Instance
        {
            get
            {
                if (s_Instance == null) s_Instance = FindFirstObjectByType<LandFlowLoadingScreen>();
                return s_Instance;
            }
        }

        [Header("🏢 Fogbound Stüdyo Giriş Ayarları")]
        [Tooltip("Fogbound şirket logosu sprite'ı")]
        [SerializeField] private Sprite m_FogboundLogoSprite;
        [Tooltip("Fogbound açılış logosunun ekranda kalma süresi (sn)")]
        [SerializeField] private float m_FogboundDisplayDuration = 1.2f;
        [Tooltip("Fogbound logosunun solma (fade out) süresi (sn)")]
        [SerializeField] private float m_FogboundFadeDuration = 0.45f;
        [Tooltip("Açık: oyun içi Fogbound intro'su gösterilir. Kapalı: logo yalnız Unity splash'te görünür.")]
        [SerializeField] private bool m_ShowFogboundIntro = false;

        [Header("🌊 Land Flow Yükleme Ekranı Görselleri")]
        [Tooltip("Tam ekran Land Flow yükleme ekranı arka plan görseli")]
        [SerializeField] private Sprite m_FullscreenSplashSprite;
        [Tooltip("3D kabartmalı Land Flow stili 'LOADING...' rozeti")]
        [SerializeField] private Sprite m_LoadingBadgeSprite;
        [Tooltip("Dinamik metinler için font (Lilita One / Titan One SDF)")]
        [SerializeField] private TMP_FontAsset m_Font;

        [Header("🕰️ Klasik Stil (Level Geçişleri - Ezgi Öncesi Tarif)")]
        [Tooltip("Solid arka plan rengi (derin okyanus mavisi #1773D1)")]
        [SerializeField] private Color m_ClassicBackgroundColor = new Color(0.09f, 0.45f, 0.82f, 1f);
        [Tooltip("Ortalanmış Land Flow logosu")]
        [SerializeField] private Sprite m_ClassicLogoSprite;

        [Header("⏱️ Zamanlama ve Yumuşaklık (Smooth Timings)")]
        [Tooltip("Giriş kararma süresi (sn)")]
        [SerializeField] private float m_FadeInDuration = 0.35f;
        [Tooltip("Çıkış kararma süresi (sn)")]
        [SerializeField] private float m_FadeOutDuration = 0.45f;
        [Tooltip("Yükleme ekranının ekranda kalacağı ideal minimum süre (sn)")]
        [SerializeField] private float m_MinDisplayDuration = 1.6f;
        [Tooltip("%100 dolduktan sonra ekranda kalma nefes payı (sn)")]
        [SerializeField] private float m_HoldBeforeFadeOut = 0.20f;

        [Header("📝 Metin Ayarları (Dynamic Text)")]
        [Tooltip("Varsayılan yükleme ekranı metni")]
        [SerializeField] private string m_DefaultLoadingText = "LOADING";
        [Tooltip("Bölüm kaybedildiğinde gösterilecek metin")]
        [SerializeField] private string m_FailLevelText = "FAIL LEVEL";
        [Tooltip("Bölüm başarıyla tamamlandığında gösterilecek metin")]
        [SerializeField] private string m_LevelCompleteText = "LEVEL COMPLETED";

        // UI Bileşenleri
        private CanvasGroup m_CanvasGroup;
        private GameObject m_LandFlowContent; // Resimli ağaç (yalnız açılışta görünür)
        private GameObject m_ClassicRoot; // Klasik ağaç (yalnız level geçişlerinde görünür)
        private Image m_ClassicLogoImage;
        private TextMeshProUGUI m_ClassicText;
        private Image m_ClassicBadgeImage; // "LOADING" için açılış ekranındaki 3D rozetin aynısı
        private TextMeshProUGUI m_ClassicSubText;
        private RectTransform m_ClassicBarFill;
        private RectTransform m_ClassicBarBg;
        private Tween m_ClassicLogoPulseTween;
        private Image m_FullscreenSplashImage;
        private Image m_LoadingBadgeImage;
        private TextMeshProUGUI m_LoadingText;
        private TextMeshProUGUI m_SubText;
        private RectTransform m_ProgressBarFill;
        private RectTransform m_ProgressBarBg;

        // Fogbound Intro Bileşenleri
        private GameObject m_FogboundOverlayObj;
        private CanvasGroup m_FogboundOverlayGroup;
        private Image m_FogboundLogoImage;
        private Tween m_FogboundPulseTween;

        // Animasyon ve Rutin Değişkenleri
        private Coroutine m_TextDotRoutine;
        private Coroutine m_ActiveLoadingRoutine;
        private Tween m_BadgePulseTween;
        private Tween m_ProgressBarTween;
        private Tween m_SubTextPulseTween;
        private float m_VisualProgress = 0f;
        private bool m_IsVisible = false;
        private string m_CurrentBaseMessage = "LOADING";
        private string m_CurrentSubMessage = null;
        // İlk sahne açılışında intro göster, sonrakilerde gizli başla.
        // (Eskiden saate bakılıyordu; Unity splash'i süreyi yiyince cihazda tutmuyordu.)
        private static bool s_BootShown = false;

        public bool IsVisible => m_IsVisible;

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadSpritesIfNeeded();
            BuildUI();

            // Oyun ilk açıldığında doğrudan Fogbound intro ile başla (ekran flaş yapmasın)
            if (!s_BootShown)
            {
                s_BootShown = true;
                // Açılışta resimli splash stili
                SetClassicMode(false);
                m_CanvasGroup.alpha = 1f;
                m_CanvasGroup.blocksRaycasts = true;
                m_CanvasGroup.interactable = true;
                m_IsVisible = true;

                // Fogbound ekranı aktif ve en üstte (bayrak açıksa)
                if (m_ShowFogboundIntro && m_FogboundOverlayObj != null)
                {
                    m_FogboundOverlayObj.SetActive(true);
                    m_FogboundOverlayGroup.alpha = 1f;
                }
                else if (m_FogboundOverlayObj != null)
                {
                    m_FogboundOverlayObj.SetActive(false);
                }
            }
            else
            {
                // Oyun ortasında sonradan oluştuysa gizli başla
                m_CanvasGroup.alpha = 0f;
                m_CanvasGroup.blocksRaycasts = false;
                m_CanvasGroup.interactable = false;
                m_IsVisible = false;
                if (m_FogboundOverlayObj != null)
                {
                    m_FogboundOverlayObj.SetActive(false);
                }
            }
        }

        private void Start()
        {
            // Oyun açılışında Fogbound intro -> Land Flow loading ekranı akışı
            if (m_IsVisible && m_ActiveLoadingRoutine == null)
            {
                StartCoroutine(InitialBootSequenceRoutine());
            }
        }

        /// <summary>
        /// Sprite referanslarını Resources veya AssetDatabase'den otomatik yükler.
        /// </summary>
        private void LoadSpritesIfNeeded()
        {
            if (m_FogboundLogoSprite == null)
            {
                m_FogboundLogoSprite = Resources.Load<Sprite>("Fogbound_Logo_Transparent");
                if (m_FogboundLogoSprite == null)
                {
                    m_FogboundLogoSprite = Resources.Load<Sprite>("Fogbound_Logo");
                }
#if UNITY_EDITOR
                if (m_FogboundLogoSprite == null)
                {
                    m_FogboundLogoSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Fogbound_Logo_Transparent.png");
                }
#endif
            }

            if (m_FullscreenSplashSprite == null)
            {
                m_FullscreenSplashSprite = Resources.Load<Sprite>("LandFlow_Splash_Clean");
                if (m_FullscreenSplashSprite == null)
                {
                    m_FullscreenSplashSprite = Resources.Load<Sprite>("LandFlow_Splash_Full");
                }
#if UNITY_EDITOR
                if (m_FullscreenSplashSprite == null)
                {
                    m_FullscreenSplashSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/LandFlow_Splash_Clean.png");
                }
#endif
            }

            if (m_LoadingBadgeSprite == null)
            {
                m_LoadingBadgeSprite = Resources.Load<Sprite>("Loading_Badge_3D");
#if UNITY_EDITOR
                if (m_LoadingBadgeSprite == null)
                {
                    m_LoadingBadgeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Loading_Badge_3D.png");
                }
#endif
            }

            if (m_Font == null)
            {
#if UNITY_EDITOR
                m_Font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/LilitaOne-Regular SDF.asset");
#endif
                if (m_Font == null)
                {
                    m_Font = GameThemeSettings.MainFont;
                }
            }
        }

        /// <summary>
        /// Oyun ilk açıldığında çalışan sinematik 2 fazlı başlangıç sekansı:
        /// 1. Faz: Fogbound Şirket Logosu (Karanlık zemin, zarif nefes alma, yumuşak solma)
        /// 2. Faz: Land Flow Tam Ekran Yükleme Ekranı (Canlı tropikal sahil, 3D 'LOADING...' yazısı, ilerleme çubuğu)
        /// </summary>
        private IEnumerator InitialBootSequenceRoutine()
        {
            UpdateProgressBarImmediate(0f);

            // --- 1. FAZ: FOGBOUND STÜDYO LOGOSU ---
            // Kapalı: logo zaten Unity splash'te gösteriliyor, ikinci kez tekrar etmesin.
            if (m_ShowFogboundIntro && m_FogboundOverlayObj != null && m_FogboundOverlayGroup != null)
            {
                m_FogboundOverlayObj.SetActive(true);
                m_FogboundOverlayGroup.alpha = 1f;

                // Logoya zarif bir nefes alma animasyonu
                if (m_FogboundLogoImage != null)
                {
                    m_FogboundPulseTween?.Kill();
                    m_FogboundLogoImage.transform.localScale = Vector3.one * 0.96f;
                    m_FogboundPulseTween = m_FogboundLogoImage.transform
                        .DOScale(Vector3.one * 1.035f, m_FogboundDisplayDuration * 0.9f)
                        .SetEase(Ease.OutSine)
                        .SetUpdate(true);
                }

                // Ekranda kalış süresi
                yield return new WaitForSecondsRealtime(m_FogboundDisplayDuration);

                // Yumuşakça solarak arkasındaki Land Flow ekranına geçiş yap
                yield return m_FogboundOverlayGroup
                    .DOFade(0f, m_FogboundFadeDuration)
                    .SetEase(Ease.InOutSine)
                    .SetUpdate(true)
                    .WaitForCompletion();

                m_FogboundOverlayObj.SetActive(false);
            }

            // --- 2. FAZ: LAND FLOW YÜKLEME EKRANI ---
            StartBadgeAndTextAnimations();

            // 🎵 Oyun Açılış Sesi (Brand Splash & Launch Jingle)
            if (HypercasualFeedbackManager.Instance != null)
            {
                HypercasualFeedbackManager.Instance.PlayGameLaunchFeedback();
            }

            // İlerleme çubuğunu organik olarak %100'e doldur
            AnimateProgressBarTo(1.0f, m_MinDisplayDuration, Ease.InOutQuad);

            float elapsed = 0f;
            while (elapsed < m_MinDisplayDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Sahnenin oturması için hafif nefes payı
            yield return new WaitForSecondsRealtime(m_HoldBeforeFadeOut);

            // Oyuna yumuşak geçiş
            Hide();
        }

        /// <summary>
        /// Yükleme ekranı UI hiyerarşisini kodla eksiksiz, bağımsız ve görsel olarak kusursuz inşa eder.
        /// </summary>
        private void BuildUI()
        {
            // Canvas ayarları
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // Her şeyin en üstünde görünsün

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            m_CanvasGroup = GetComponent<CanvasGroup>();
            if (m_CanvasGroup == null) m_CanvasGroup = gameObject.AddComponent<CanvasGroup>();

            // =========================================================================
            // A. LAND FLOW ANA YÜKLEME EKRANI İÇERİĞİ
            // =========================================================================
            GameObject landflowContent = new GameObject("LandFlow_Content");
            m_LandFlowContent = landflowContent;
            landflowContent.transform.SetParent(transform, false);
            RectTransform landflowRect = landflowContent.AddComponent<RectTransform>();
            landflowRect.anchorMin = Vector2.zero;
            landflowRect.anchorMax = Vector2.one;
            landflowRect.sizeDelta = Vector2.zero;

            // 1. Tam Ekran Arka Plan Görseli (Land Flow Splash - Edge to Edge)
            GameObject splashObj = new GameObject("FullscreenSplash");
            splashObj.transform.SetParent(landflowContent.transform, false);
            RectTransform splashRect = splashObj.AddComponent<RectTransform>();
            splashRect.anchorMin = Vector2.zero;
            splashRect.anchorMax = Vector2.one;
            splashRect.sizeDelta = Vector2.zero;

            m_FullscreenSplashImage = splashObj.AddComponent<Image>();
            m_FullscreenSplashImage.sprite = m_FullscreenSplashSprite;
            m_FullscreenSplashImage.color = Color.white;
            m_FullscreenSplashImage.type = Image.Type.Simple;
            m_FullscreenSplashImage.raycastTarget = true;

            // 2. Alt UI Bölgesi Kapsayıcısı (Su bölümünün alt kısmı için)
            // 1080x1920 ekranda alt kenardan ~160px yukarıda
            GameObject bottomUI = new GameObject("BottomUIContainer");
            bottomUI.transform.SetParent(landflowContent.transform, false);
            RectTransform bottomRect = bottomUI.AddComponent<RectTransform>();
            bottomRect.anchorMin = new Vector2(0.5f, 0f);
            bottomRect.anchorMax = new Vector2(0.5f, 0f);
            bottomRect.pivot = new Vector2(0.5f, 0f);
            bottomRect.anchoredPosition = new Vector2(0f, 130f);
            bottomRect.sizeDelta = new Vector2(900f, 320f);

            // 3. 3D "LOADING..." Rozeti (Land Flow Başlık Stiliyle Birebir Eşleşen 3D Bubble Font)
            GameObject badgeObj = new GameObject("LoadingBadge_3D");
            badgeObj.transform.SetParent(bottomUI.transform, false);
            RectTransform badgeRect = badgeObj.AddComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.5f, 0.5f);
            badgeRect.anchorMax = new Vector2(0.5f, 0.5f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.sizeDelta = new Vector2(460f, 96f);
            badgeRect.anchoredPosition = new Vector2(0f, 65f);

            m_LoadingBadgeImage = badgeObj.AddComponent<Image>();
            m_LoadingBadgeImage.sprite = m_LoadingBadgeSprite;
            m_LoadingBadgeImage.preserveAspect = true;
            m_LoadingBadgeImage.raycastTarget = false;

            // 4. Dinamik Alternatif Metin (Örn: "FAIL LEVEL", "LEVEL COMPLETED")
            // Varsayılanda gizlidir, özel bir mesaj verildiğinde 3D rozet yerine bu yazı gösterilir
            GameObject textObj = new GameObject("LoadingText_Dynamic");
            textObj.transform.SetParent(bottomUI.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(800f, 90f);
            textRect.anchoredPosition = new Vector2(0f, 65f);

            m_LoadingText = textObj.AddComponent<TextMeshProUGUI>();
            if (m_Font != null) m_LoadingText.font = m_Font;
            m_LoadingText.text = "LOADING...";
            m_LoadingText.fontSize = 50;
            m_LoadingText.enableAutoSizing = true;
            m_LoadingText.fontSizeMin = 34;
            m_LoadingText.fontSizeMax = 52;
            m_LoadingText.fontStyle = FontStyles.Bold;
            m_LoadingText.alignment = TextAlignmentOptions.Center;
            m_LoadingText.color = Color.white;
            m_LoadingText.enableVertexGradient = true;
            m_LoadingText.colorGradient = new VertexGradient(
                new Color(1f, 0.95f, 0.15f, 1f), // Parlak sarı tepe
                new Color(1f, 0.95f, 0.15f, 1f),
                new Color(1f, 0.52f, 0.00f, 1f), // Sıcak turuncu alt
                new Color(1f, 0.52f, 0.00f, 1f)
            );
            m_LoadingText.raycastTarget = false;

            // Koyu lacivert kontur (Land Flow stili)
            var textOutline = textObj.AddComponent<Outline>();
            textOutline.effectColor = new Color(0.03f, 0.16f, 0.42f, 0.95f);
            textOutline.effectDistance = new Vector2(3f, -3f);

            textObj.SetActive(false); // Varsayılanda 3D rozet gösterilir

            // 5. Alt Başlık / Ödül Metni (SubText - Örn: "+20 COINS" veya "PICTURE COMPLETED!")
            GameObject subTextObj = new GameObject("SubText");
            subTextObj.transform.SetParent(bottomUI.transform, false);
            RectTransform subRect = subTextObj.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 0.5f);
            subRect.anchorMax = new Vector2(0.5f, 0.5f);
            subRect.pivot = new Vector2(0.5f, 0.5f);
            subRect.sizeDelta = new Vector2(650f, 45f);
            subRect.anchoredPosition = new Vector2(0f, 15f);

            m_SubText = subTextObj.AddComponent<TextMeshProUGUI>();
            if (m_Font != null) m_SubText.font = m_Font;
            m_SubText.fontSize = 30;
            m_SubText.fontStyle = FontStyles.Bold;
            m_SubText.alignment = TextAlignmentOptions.Center;
            m_SubText.color = new Color(1f, 0.92f, 0.35f, 1f);
            m_SubText.enableVertexGradient = true;
            m_SubText.colorGradient = new VertexGradient(
                new Color(1f, 1.0f, 0.65f, 1f),
                new Color(1f, 1.0f, 0.65f, 1f),
                new Color(1f, 0.72f, 0.12f, 1f),
                new Color(1f, 0.72f, 0.12f, 1f)
            );
            m_SubText.raycastTarget = false;

            var subOutline = subTextObj.AddComponent<Outline>();
            subOutline.effectColor = new Color(0.02f, 0.12f, 0.30f, 0.90f);
            subOutline.effectDistance = new Vector2(2f, -2f);
            subTextObj.SetActive(false);

            // 6. Şık İlerleme Çubuğu (Progress Bar)
            GameObject barBg = new GameObject("ProgressBar_BG");
            barBg.transform.SetParent(bottomUI.transform, false);
            m_ProgressBarBg = barBg.AddComponent<RectTransform>();
            m_ProgressBarBg.anchorMin = new Vector2(0.5f, 0.5f);
            m_ProgressBarBg.anchorMax = new Vector2(0.5f, 0.5f);
            m_ProgressBarBg.pivot = new Vector2(0.5f, 0.5f);
            m_ProgressBarBg.sizeDelta = new Vector2(460f, 22f);
            m_ProgressBarBg.anchoredPosition = new Vector2(0f, -30f);

            Image bgBarImg = barBg.AddComponent<Image>();
            bgBarImg.color = new Color(0.03f, 0.16f, 0.42f, 0.92f); // Koyu okyanus laciverti
            bgBarImg.raycastTarget = false;

            var barBorder = barBg.AddComponent<Outline>();
            barBorder.effectColor = new Color(0.45f, 0.85f, 1.0f, 0.85f); // Açık camgöbeği ışıltı konturu
            barBorder.effectDistance = new Vector2(2f, -2f);

            Mask barMask = barBg.AddComponent<Mask>();
            barMask.showMaskGraphic = true;

            GameObject barFill = new GameObject("ProgressBar_Fill");
            barFill.transform.SetParent(barBg.transform, false);
            m_ProgressBarFill = barFill.AddComponent<RectTransform>();
            m_ProgressBarFill.anchorMin = new Vector2(0f, 0f);
            m_ProgressBarFill.anchorMax = new Vector2(0f, 1f);
            m_ProgressBarFill.pivot = new Vector2(0f, 0.5f);
            m_ProgressBarFill.sizeDelta = new Vector2(0f, 0f);
            m_ProgressBarFill.anchoredPosition = Vector2.zero;

            Image fillImg = barFill.AddComponent<Image>();
            fillImg.color = new Color(1f, 0.82f, 0.15f, 1f); // Canlı altın sarısı ('LAND' rengiyle uyumlu)
            fillImg.raycastTarget = false;

            // =========================================================================
            // B. FOGBOUND STÜDYO AÇILIŞ KATMANI (Studio Intro Overlay)
            // =========================================================================
            m_FogboundOverlayObj = new GameObject("Fogbound_IntroOverlay");
            m_FogboundOverlayObj.transform.SetParent(transform, false);
            RectTransform fogRect = m_FogboundOverlayObj.AddComponent<RectTransform>();
            fogRect.anchorMin = Vector2.zero;
            fogRect.anchorMax = Vector2.one;
            fogRect.sizeDelta = Vector2.zero;

            m_FogboundOverlayGroup = m_FogboundOverlayObj.AddComponent<CanvasGroup>();

            // Tam ekran saf siyah zemin (#000000)
            Image fogBgImg = m_FogboundOverlayObj.AddComponent<Image>();
            fogBgImg.color = Color.black;
            fogBgImg.raycastTarget = true;

            // Ortalanmış net Fogbound şirket logosu
            GameObject fogLogoObj = new GameObject("Fogbound_Logo");
            fogLogoObj.transform.SetParent(m_FogboundOverlayObj.transform, false);
            RectTransform fogLogoRect = fogLogoObj.AddComponent<RectTransform>();
            fogLogoRect.anchorMin = new Vector2(0.5f, 0.5f);
            fogLogoRect.anchorMax = new Vector2(0.5f, 0.5f);
            fogLogoRect.pivot = new Vector2(0.5f, 0.5f);
            fogLogoRect.sizeDelta = new Vector2(740f, 185f); // 4:1 oranında net ve görkemli
            fogLogoRect.anchoredPosition = Vector2.zero;

            m_FogboundLogoImage = fogLogoObj.AddComponent<Image>();
            m_FogboundLogoImage.sprite = m_FogboundLogoSprite;
            m_FogboundLogoImage.color = Color.white;
            m_FogboundLogoImage.preserveAspect = true;
            m_FogboundLogoImage.raycastTarget = false;

            m_FogboundOverlayObj.SetActive(false); // Sadece açılışta veya özel çağrıda aktifleştirilir

            // Klasik stil ağacı (Ezgi öncesi tarif, level geçişleri için)
            BuildClassicUI();
        }

        // =========================================================================
        // C. KLASİK STİL AĞACI (Ezgi öncesi tarif, birebir: solid zemin + ortalanmış logo)
        //    Yalnız level geçiş/fail ekranlarında görünür.
        // =========================================================================
        private void BuildClassicUI()
        {
            m_ClassicRoot = new GameObject("Classic_Content");
            m_ClassicRoot.transform.SetParent(transform, false);
            RectTransform rootRect = m_ClassicRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;

            // 1. Solid Renkli Tam Ekran Arka Plan
            GameObject bgObj = new GameObject("SolidBackground");
            bgObj.transform.SetParent(m_ClassicRoot.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = m_ClassicBackgroundColor;
            bgImg.raycastTarget = true;

            // 2. İçerik Kapsayıcısı (Ekranın Tam Ortasında)
            GameObject centerContainer = new GameObject("CenterContent");
            centerContainer.transform.SetParent(m_ClassicRoot.transform, false);
            RectTransform centerRect = centerContainer.AddComponent<RectTransform>();
            centerRect.anchorMin = new Vector2(0.5f, 0.5f);
            centerRect.anchorMax = new Vector2(0.5f, 0.5f);
            centerRect.pivot = new Vector2(0.5f, 0.5f);
            centerRect.anchoredPosition = new Vector2(0f, 40f);
            centerRect.sizeDelta = new Vector2(900f, 800f);

            // 3. Land Flow Logosu
            if (m_ClassicLogoSprite == null)
            {
#if UNITY_EDITOR
                m_ClassicLogoSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/LandFlow_Logo.png");
#endif
                if (m_ClassicLogoSprite == null)
                {
                    m_ClassicLogoSprite = Resources.Load<Sprite>("LandFlow_Logo");
                }
            }

            GameObject logoObj = new GameObject("LandFlow_Logo");
            logoObj.transform.SetParent(centerContainer.transform, false);
            RectTransform logoRect = logoObj.AddComponent<RectTransform>();
            logoRect.anchorMin = new Vector2(0.5f, 0.6f);
            logoRect.anchorMax = new Vector2(0.5f, 0.6f);
            logoRect.pivot = new Vector2(0.5f, 0.5f);
            logoRect.sizeDelta = new Vector2(620f, 310f);
            logoRect.anchoredPosition = new Vector2(0f, 50f);

            m_ClassicLogoImage = logoObj.AddComponent<Image>();
            m_ClassicLogoImage.sprite = m_ClassicLogoSprite;
            m_ClassicLogoImage.preserveAspect = true;
            m_ClassicLogoImage.raycastTarget = false;

            // 4. "LOADING..." Yazısı (Logonun Altında)
            if (m_Font == null)
            {
#if UNITY_EDITOR
                m_Font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/LilitaOne-Regular SDF.asset");
#endif
                if (m_Font == null) m_Font = GameThemeSettings.MainFont;
            }

            GameObject textObj = new GameObject("LoadingText");
            textObj.transform.SetParent(centerContainer.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.32f);
            textRect.anchorMax = new Vector2(0.5f, 0.32f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(820f, 110f);
            textRect.anchoredPosition = new Vector2(0f, -30f);

            m_ClassicText = textObj.AddComponent<TextMeshProUGUI>();
            if (m_Font != null) m_ClassicText.font = m_Font;
            m_ClassicText.text = "LOADING...";
            // Açılış ekranındaki "LOADING" rozetinin stili: sarı → turuncu, kalın lacivert kontur ve 3D gölge
            m_ClassicText.fontSize = 62;
            m_ClassicText.enableAutoSizing = true;
            m_ClassicText.fontSizeMin = 40;
            m_ClassicText.fontSizeMax = 62;
            m_ClassicText.fontStyle = FontStyles.Bold;
            m_ClassicText.alignment = TextAlignmentOptions.Center;
            m_ClassicText.color = Color.white;
            m_ClassicText.enableVertexGradient = true;
            m_ClassicText.colorGradient = new VertexGradient(
                new Color(1f, 0.95f, 0.15f, 1f), // Parlak sarı tepe
                new Color(1f, 0.95f, 0.15f, 1f),
                new Color(1f, 0.52f, 0.00f, 1f), // Sıcak turuncu alt
                new Color(1f, 0.52f, 0.00f, 1f)
            );
            m_ClassicText.raycastTarget = false;

            // Rozetteki gibi kalın lacivert kontur + alta düşen 3D gölge
            var outline = textObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.03f, 0.16f, 0.42f, 1f);
            outline.effectDistance = new Vector2(4f, -4f);
            var depth = textObj.AddComponent<Shadow>();
            depth.effectColor = new Color(0.02f, 0.10f, 0.30f, 1f);
            depth.effectDistance = new Vector2(0f, -7f);

            // "LOADING" mesajında açılış ekranındaki 3D rozet gösterilir (yazıyla aynı yerde)
            GameObject badgeObj = new GameObject("LoadingBadge_3D");
            badgeObj.transform.SetParent(centerContainer.transform, false);
            RectTransform badgeRect = badgeObj.AddComponent<RectTransform>();
            badgeRect.anchorMin = textRect.anchorMin;
            badgeRect.anchorMax = textRect.anchorMax;
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.sizeDelta = new Vector2(460f, 96f);
            badgeRect.anchoredPosition = textRect.anchoredPosition;
            m_ClassicBadgeImage = badgeObj.AddComponent<Image>();
            m_ClassicBadgeImage.sprite = m_LoadingBadgeSprite;
            m_ClassicBadgeImage.preserveAspect = true;
            m_ClassicBadgeImage.raycastTarget = false;
            badgeObj.SetActive(false);

            // 5. Alt Başlık / Ödül Metni
            GameObject subTextObj = new GameObject("SubText");
            subTextObj.transform.SetParent(centerContainer.transform, false);
            RectTransform subRect = subTextObj.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 0.28f);
            subRect.anchorMax = new Vector2(0.5f, 0.28f);
            subRect.pivot = new Vector2(0.5f, 0.5f);
            subRect.sizeDelta = new Vector2(650f, 50f);
            subRect.anchoredPosition = new Vector2(0f, -80f);

            m_ClassicSubText = subTextObj.AddComponent<TextMeshProUGUI>();
            if (m_Font != null) m_ClassicSubText.font = m_Font;
            m_ClassicSubText.fontSize = 32;
            m_ClassicSubText.fontStyle = FontStyles.Bold;
            m_ClassicSubText.alignment = TextAlignmentOptions.Center;
            m_ClassicSubText.color = new Color(1f, 0.88f, 0.25f, 1f);
            m_ClassicSubText.enableVertexGradient = true;
            m_ClassicSubText.colorGradient = new VertexGradient(
                new Color(1f, 0.95f, 0.55f, 1f),
                new Color(1f, 0.95f, 0.55f, 1f),
                new Color(1f, 0.72f, 0.12f, 1f),
                new Color(1f, 0.72f, 0.12f, 1f)
            );
            m_ClassicSubText.raycastTarget = false;

            var subOutline = subTextObj.AddComponent<Outline>();
            subOutline.effectColor = new Color(0.02f, 0.12f, 0.28f, 0.90f);
            subOutline.effectDistance = new Vector2(2f, -2f);
            subTextObj.SetActive(false);

            // 6. Şık İlerleme Çubuğu (Progress Bar)
            GameObject barBg = new GameObject("ProgressBar_BG");
            barBg.transform.SetParent(centerContainer.transform, false);
            m_ClassicBarBg = barBg.AddComponent<RectTransform>();
            m_ClassicBarBg.anchorMin = new Vector2(0.5f, 0.22f);
            m_ClassicBarBg.anchorMax = new Vector2(0.5f, 0.22f);
            m_ClassicBarBg.pivot = new Vector2(0.5f, 0.5f);
            // Açılış ekranındaki barla aynı ölçü ve renkler
            m_ClassicBarBg.sizeDelta = new Vector2(460f, 22f);
            m_ClassicBarBg.anchoredPosition = new Vector2(0f, -135f);

            Image bgBarImg = barBg.AddComponent<Image>();
            bgBarImg.color = new Color(0.03f, 0.16f, 0.42f, 0.92f); // Koyu okyanus laciverti
            bgBarImg.raycastTarget = false;

            var barBorder = barBg.AddComponent<Outline>();
            barBorder.effectColor = new Color(0.45f, 0.85f, 1.0f, 0.85f); // Açık camgöbeği ışıltı konturu
            barBorder.effectDistance = new Vector2(2f, -2f);

            Mask barMask = barBg.AddComponent<Mask>();
            barMask.showMaskGraphic = true;

            GameObject barFill = new GameObject("ProgressBar_Fill");
            barFill.transform.SetParent(barBg.transform, false);
            m_ClassicBarFill = barFill.AddComponent<RectTransform>();
            m_ClassicBarFill.anchorMin = new Vector2(0f, 0f);
            m_ClassicBarFill.anchorMax = new Vector2(0f, 1f);
            m_ClassicBarFill.pivot = new Vector2(0f, 0.5f);
            m_ClassicBarFill.sizeDelta = new Vector2(0f, 0f);
            m_ClassicBarFill.anchoredPosition = Vector2.zero;

            Image fillImg = barFill.AddComponent<Image>();
            fillImg.color = new Color(1f, 0.82f, 0.15f, 1f); // Canlı altın sarısı (açılış barıyla aynı)
            fillImg.raycastTarget = false;

            m_ClassicRoot.SetActive(false);
        }

        private bool IsClassicActive()
        {
            return m_ClassicRoot != null && m_ClassicRoot.activeSelf;
        }

        private void SetClassicMode(bool classic)
        {
            if (m_ClassicRoot != null) m_ClassicRoot.SetActive(classic);
            if (m_LandFlowContent != null) m_LandFlowContent.SetActive(!classic);
        }

        private void StartBadgeAndTextAnimations()
        {
            // Klasik modda: ortalanmış logo nefes alır, klasik yazı noktacıklanır
            if (IsClassicActive())
            {
                if (m_ClassicLogoImage != null)
                {
                    m_ClassicLogoPulseTween?.Kill();
                    m_ClassicLogoImage.transform.localScale = Vector3.one;
                    m_ClassicLogoPulseTween = m_ClassicLogoImage.transform
                        .DOScale(Vector3.one * 1.045f, 0.85f)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetEase(Ease.InOutSine)
                        .SetUpdate(true);
                }
                if (m_ClassicBadgeImage != null && m_ClassicBadgeImage.gameObject.activeSelf)
                {
                    m_BadgePulseTween?.Kill();
                    m_ClassicBadgeImage.transform.localScale = Vector3.one;
                    m_BadgePulseTween = m_ClassicBadgeImage.transform
                        .DOScale(Vector3.one * 1.05f, 0.85f)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetEase(Ease.InOutSine)
                        .SetUpdate(true);
                }
                if (m_ClassicText != null)
                {
                    if (m_TextDotRoutine != null) StopCoroutine(m_TextDotRoutine);
                    m_TextDotRoutine = StartCoroutine(AnimateLoadingDots());
                }
                return;
            }
            // 3D "LOADING..." rozeti için tatlı, canlı nefes alma animasyonu
            if (m_LoadingBadgeImage != null && m_LoadingBadgeImage.gameObject.activeSelf)
            {
                m_BadgePulseTween?.Kill();
                m_LoadingBadgeImage.transform.localScale = Vector3.one;
                m_BadgePulseTween = m_LoadingBadgeImage.transform
                    .DOScale(Vector3.one * 1.05f, 0.85f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetUpdate(true);
            }

            // Metin noktacık animasyonu (dinamik metin aktifse)
            if (m_LoadingText != null && m_LoadingText.gameObject.activeSelf)
            {
                if (m_TextDotRoutine != null) StopCoroutine(m_TextDotRoutine);
                m_TextDotRoutine = StartCoroutine(AnimateLoadingDots());
            }
        }

        private void StopBadgeAndTextAnimations()
        {
            m_ProgressBarTween?.Kill();
            m_BadgePulseTween?.Kill();
            m_SubTextPulseTween?.Kill();
            m_ClassicLogoPulseTween?.Kill();

            if (m_ClassicLogoImage != null)
            {
                m_ClassicLogoImage.transform.localScale = Vector3.one;
            }
            if (m_ClassicBadgeImage != null)
            {
                m_ClassicBadgeImage.transform.localScale = Vector3.one;
            }

            if (m_LoadingBadgeImage != null)
            {
                m_LoadingBadgeImage.transform.localScale = Vector3.one;
            }
            if (m_SubText != null)
            {
                m_SubText.transform.localScale = Vector3.one;
                m_SubText.gameObject.SetActive(false);
            }
            if (m_TextDotRoutine != null)
            {
                StopCoroutine(m_TextDotRoutine);
                m_TextDotRoutine = null;
            }
        }

        /// <summary>
        /// Yükleme ekranında gösterilecek ana metni ve isteğe bağlı alt metni (ödül/açıklama) ayarlar.
        /// Standart "LOADING" ise şık 3D rozeti gösterir; "FAIL LEVEL" veya "LEVEL COMPLETED" gibi
        /// özel durumlarda ise stillendirilmiş TextMeshPro metnini açar.
        /// </summary>
        public void SetMessage(string message, string subMessage = null)
        {
            if (string.IsNullOrEmpty(message)) message = m_DefaultLoadingText;
            m_CurrentBaseMessage = message.TrimEnd('.');
            m_CurrentSubMessage = subMessage;

            bool isDefaultLoading = string.Equals(m_CurrentBaseMessage, "LOADING", StringComparison.OrdinalIgnoreCase);

            if (m_LoadingBadgeImage != null)
            {
                m_LoadingBadgeImage.gameObject.SetActive(isDefaultLoading);
            }

            if (m_LoadingText != null)
            {
                m_LoadingText.gameObject.SetActive(!isDefaultLoading);
                if (!isDefaultLoading)
                {
                    m_LoadingText.text = m_CurrentBaseMessage;
                }
            }

            // Klasik ağaç: açılış ekranı gibi "LOADING" için 3D rozet, diğer mesajlarda yazı
            if (m_ClassicBadgeImage != null)
            {
                m_ClassicBadgeImage.gameObject.SetActive(isDefaultLoading);
            }
            if (m_ClassicText != null)
            {
                m_ClassicText.gameObject.SetActive(!isDefaultLoading);
                m_ClassicText.text = m_CurrentBaseMessage + "...";
            }

            if (m_SubText != null)
            {
                m_SubTextPulseTween?.Kill();
                if (!string.IsNullOrEmpty(subMessage))
                {
                    m_SubText.text = subMessage;
                    m_SubText.gameObject.SetActive(true);
                    m_SubText.transform.localScale = Vector3.one;
                    m_SubTextPulseTween = m_SubText.transform
                        .DOScale(Vector3.one * 1.06f, 0.45f)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetEase(Ease.InOutSine)
                        .SetUpdate(true);
                }
                else
                {
                    m_SubText.gameObject.SetActive(false);
                }
            }

            // Klasik stil: alt yazı (coin/ödül metni) gösterilmez
            if (m_ClassicSubText != null)
            {
                m_ClassicSubText.gameObject.SetActive(false);
            }
        }

        private IEnumerator AnimateLoadingDots()
        {
            int dots = 0;
            while (true)
            {
                if (m_LoadingText != null && m_LoadingText.gameObject.activeSelf)
                {
                    string suffix = new string('.', dots);
                    m_LoadingText.text = m_CurrentBaseMessage + suffix;
                }
                if (m_ClassicText != null && IsClassicActive())
                {
                    string suffix = new string('.', dots);
                    m_ClassicText.text = m_CurrentBaseMessage + suffix;
                }
                dots = (dots + 1) % 4;
                yield return new WaitForSecondsRealtime(0.28f);
            }
        }

        /// <summary>
        /// İlerleme çubuğunu anında belirli bir değere ayarlar.
        /// </summary>
        public void UpdateProgressBarImmediate(float normalizedProgress)
        {
            m_ProgressBarTween?.Kill();
            m_VisualProgress = Mathf.Clamp01(normalizedProgress);
            ApplyProgressBarWidth(m_VisualProgress);
        }

        /// <summary>
        /// İlerleme çubuğunu verilen süre boyunca ipeksi bir yumuşaklıkla hedefe doğru doldurur.
        /// </summary>
        public void AnimateProgressBarTo(float targetProgress, float duration, Ease ease = Ease.OutQuad)
        {
            m_ProgressBarTween?.Kill();
            float target = Mathf.Clamp01(targetProgress);
            m_ProgressBarTween = DOTween.To(() => m_VisualProgress, x =>
            {
                m_VisualProgress = x;
                ApplyProgressBarWidth(x);
            }, target, duration)
            .SetEase(ease)
            .SetUpdate(true);
        }

        private void ApplyProgressBarWidth(float progress)
        {
            if (m_ProgressBarFill != null && m_ProgressBarBg != null)
            {
                float targetWidth = m_ProgressBarBg.sizeDelta.x * Mathf.Clamp01(progress);
                m_ProgressBarFill.sizeDelta = new Vector2(targetWidth, 0f);
            }
            if (m_ClassicBarFill != null && m_ClassicBarBg != null)
            {
                float targetWidth = m_ClassicBarBg.sizeDelta.x * Mathf.Clamp01(progress);
                m_ClassicBarFill.sizeDelta = new Vector2(targetWidth, 0f);
            }
        }

        /// <summary>
        /// Geriye uyumluluk için hızlı ilerleme çubuğu güncellemesi.
        /// </summary>
        public void UpdateProgressBar(float normalizedProgress)
        {
            UpdateProgressBarImmediate(normalizedProgress);
        }

        /// <summary>
        /// Yükleme ekranını yumuşakça gösterir.
        /// </summary>
        public void Show(string message = null, Action onShown = null, string subMessage = null)
        {
            if (m_FogboundOverlayObj != null)
            {
                m_FogboundOverlayObj.SetActive(false); // Normal geçişlerde Fogbound intro oynatılmaz
            }

            // Level geçişleri klasik stilde (Ezgi öncesi tarif)
            SetClassicMode(true);

            if (!string.IsNullOrEmpty(message))
            {
                SetMessage(message, subMessage);
            }
            else if (!string.IsNullOrEmpty(subMessage))
            {
                SetMessage(m_CurrentBaseMessage, subMessage);
            }
            else if (string.IsNullOrEmpty(m_CurrentBaseMessage))
            {
                SetMessage(m_DefaultLoadingText, null);
            }

            m_IsVisible = true;
            m_CanvasGroup.blocksRaycasts = true;
            m_CanvasGroup.interactable = true;
            UpdateProgressBarImmediate(0f);
            StartBadgeAndTextAnimations();

            m_CanvasGroup.DOKill();
            m_CanvasGroup.DOFade(1f, m_FadeInDuration).SetUpdate(true).SetEase(Ease.OutSine).OnComplete(() =>
            {
                onShown?.Invoke();
            });
        }

        /// <summary>
        /// Seviye başarısız olduğunda (Level Fail) Land Flow yükleme ekranını "FAIL LEVEL"
        /// başlığı ve yumuşak animasyonla açıp aktif seviyeyi pürüzsüzce yeniden başlatır.
        /// </summary>
        public void ShowFailLevelAndRestart(float minDuration = -1f)
        {
            SetMessage(m_FailLevelText, null);
            var activeScene = SceneManager.GetActiveScene();
            LoadSceneAsync(activeScene.buildIndex, minDuration);
        }

        /// <summary>
        /// Seviye tamamlandığında (Level Complete) Land Flow yükleme ekranını "LEVEL COMPLETED"
        /// başlığı, kazanılan ödül bilgisi ve pürüzsüz ilerleme çubuğuyla açarak sonraki seviyeye geçer.
        /// </summary>
        public void ShowLevelCompleteAndLoad(Action loadAction, string subMessage = null, float minDuration = -1f, string message = null)
        {
            string mainMsg = !string.IsNullOrEmpty(message) ? message : m_LevelCompleteText;
            SetMessage(mainMsg, subMessage);
            float holdTime = minDuration > 0f ? minDuration : m_MinDisplayDuration;
            if (m_ActiveLoadingRoutine != null) StopCoroutine(m_ActiveLoadingRoutine);
            m_ActiveLoadingRoutine = StartCoroutine(LoadWithScreenRoutine(loadAction, holdTime));
        }

        /// <summary>
        /// Yükleme ekranını yumuşakça gizler.
        /// </summary>
        public void Hide(float fadeDuration = -1f, Action onHidden = null)
        {
            float duration = fadeDuration >= 0f ? fadeDuration : m_FadeOutDuration;
            m_CanvasGroup.DOKill();
            m_CanvasGroup.DOFade(0f, duration).SetUpdate(true).SetEase(Ease.InOutSine).OnComplete(() =>
            {
                m_IsVisible = false;
                m_CanvasGroup.blocksRaycasts = false;
                m_CanvasGroup.interactable = false;
                StopBadgeAndTextAnimations();
                m_CurrentBaseMessage = m_DefaultLoadingText;
                m_CurrentSubMessage = null;
                onHidden?.Invoke();
            });
        }

        /// <summary>
        /// Aktif sahneyi pürüzsüz ve asenkron (arkada takılma olmadan) şekilde yeniden başlatır.
        /// </summary>
        public void RestartCurrentScene(float minDuration = -1f, string message = null)
        {
            if (!string.IsNullOrEmpty(message)) SetMessage(message);
            var activeScene = SceneManager.GetActiveScene();
            LoadSceneAsync(activeScene.buildIndex, minDuration);
        }

        /// <summary>
        /// Belirtilen sahneyi Land Flow yükleme ekranı arkasında tamamen asenkron, takılmasız ve pürüzsüz yükler.
        /// </summary>
        public void LoadSceneAsync(int sceneBuildIndex, float minDuration = -1f)
        {
            float holdTime = minDuration > 0f ? minDuration : m_MinDisplayDuration;
            if (m_ActiveLoadingRoutine != null) StopCoroutine(m_ActiveLoadingRoutine);
            m_ActiveLoadingRoutine = StartCoroutine(LoadSceneAsyncRoutine(sceneBuildIndex, holdTime));
        }

        private IEnumerator LoadSceneAsyncRoutine(int sceneBuildIndex, float minDuration)
        {
            Show(m_CurrentBaseMessage, null, m_CurrentSubMessage);
            if (m_CanvasGroup.alpha < 0.999f)
            {
                yield return m_CanvasGroup.DOFade(1f, m_FadeInDuration).SetUpdate(true).WaitForCompletion();
            }
            m_CanvasGroup.alpha = 1f;

            UpdateProgressBarImmediate(0f);
            AnimateProgressBarTo(0.20f, 0.35f, Ease.OutQuad);

            AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneBuildIndex);
            asyncOp.allowSceneActivation = false;

            float elapsed = 0f;
            float targetLoadWait = Mathf.Max(0.7f, minDuration * 0.65f);

            while (asyncOp.progress < 0.9f || elapsed < targetLoadWait)
            {
                elapsed += Time.unscaledDeltaTime;
                float progressRatio = Mathf.Clamp01(elapsed / targetLoadWait);
                float targetP = Mathf.Lerp(0.20f, 0.88f, Mathf.SmoothStep(0f, 1f, progressRatio));
                AnimateProgressBarTo(targetP, 0.1f, Ease.Linear);
                yield return null;
            }

            AnimateProgressBarTo(0.92f, 0.2f, Ease.OutQuad);
            asyncOp.allowSceneActivation = true;

            while (!asyncOp.isDone)
            {
                yield return null;
            }

            yield return null;
            yield return new WaitForEndOfFrame();

            AnimateProgressBarTo(1.0f, 0.35f, Ease.OutCubic);
            yield return new WaitForSecondsRealtime(0.38f);

            yield return new WaitForSecondsRealtime(m_HoldBeforeFadeOut);

            Hide();
            m_ActiveLoadingRoutine = null;
        }

        /// <summary>
        /// Seviyeyi Land Flow yükleme ekranı arkasında temiz ve şık bir şekilde yükler.
        /// </summary>
        public void ShowAndLoad(Action loadAction, float minDuration = -1f, string message = null, string subMessage = null)
        {
            if (!string.IsNullOrEmpty(message) || !string.IsNullOrEmpty(subMessage))
            {
                SetMessage(message, subMessage);
            }
            float holdTime = minDuration > 0f ? minDuration : m_MinDisplayDuration;
            if (m_ActiveLoadingRoutine != null) StopCoroutine(m_ActiveLoadingRoutine);
            m_ActiveLoadingRoutine = StartCoroutine(LoadWithScreenRoutine(loadAction, holdTime));
        }

        private IEnumerator LoadWithScreenRoutine(Action loadAction, float minDuration)
        {
            Show(m_CurrentBaseMessage, null, m_CurrentSubMessage);
            if (m_CanvasGroup.alpha < 0.999f)
            {
                yield return m_CanvasGroup.DOFade(1f, m_FadeInDuration).SetUpdate(true).WaitForCompletion();
            }
            m_CanvasGroup.alpha = 1f;

            UpdateProgressBarImmediate(0f);
            float firstStageDuration = Mathf.Max(0.45f, minDuration * 0.45f);
            AnimateProgressBarTo(0.50f, firstStageDuration, Ease.OutQuad);

            float elapsed = 0f;
            while (elapsed < firstStageDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            try
            {
                loadAction?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LandFlowLoadingScreen] Seviye yükleme hatası: {ex}");
            }

            yield return null;
            yield return new WaitForEndOfFrame();

            float remainingTime = Mathf.Max(0.45f, minDuration - elapsed);
            AnimateProgressBarTo(1.0f, remainingTime, Ease.OutCubic);

            yield return new WaitForSecondsRealtime(remainingTime);
            yield return new WaitForSecondsRealtime(m_HoldBeforeFadeOut);

            Hide();
            m_ActiveLoadingRoutine = null;
        }

        /// <summary>
        /// İstenildiği zaman Fogbound stüdyo açılışını ve ardından Land Flow yükleme ekranını test etmek için çağrılabilir.
        /// </summary>
        public void PlayFullIntroSequence()
        {
            if (m_ActiveLoadingRoutine != null) StopCoroutine(m_ActiveLoadingRoutine);
            m_CanvasGroup.alpha = 1f;
            m_CanvasGroup.blocksRaycasts = true;
            m_CanvasGroup.interactable = true;
            m_IsVisible = true;
            m_ActiveLoadingRoutine = StartCoroutine(InitialBootSequenceRoutine());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInit()
        {
            EnsureInstance();
        }

        /// <summary>
        /// Sahneye otomatik olarak LandFlowLoadingScreen ekler (yoksa oluşturur).
        /// </summary>
        public static LandFlowLoadingScreen EnsureInstance()
        {
            if (s_Instance != null) return s_Instance;

            s_Instance = FindFirstObjectByType<LandFlowLoadingScreen>();
            if (s_Instance != null)
            {
                DontDestroyOnLoad(s_Instance.gameObject);
                return s_Instance;
            }

            GameObject go = new GameObject("LandFlow_LoadingScreen");
            s_Instance = go.AddComponent<LandFlowLoadingScreen>();
            DontDestroyOnLoad(go);
            return s_Instance;
        }
    }
}
