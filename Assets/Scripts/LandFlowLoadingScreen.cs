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
    /// Land Flow oyun yükleme ve seviye geçiş ekranı.
    /// Solid renkli arka plan üzerinde Land Flow logosu, ortalanmış animasyonlu "LOADING..." yazısı
    /// ve pürüzsüz altın ilerleme çubuğu ile oyun açılışında, level geçişlerinde ve level fail sonrası
    /// asenkron, takılmasız (stutter-free) ve son derece akıcı bir şekilde çalışır.
    /// </summary>
    [DisallowMultipleComponent]
    public class LandFlowLoadingScreen : MonoBehaviour
    {
        private static LandFlowLoadingScreen s_Instance;
        public static LandFlowLoadingScreen Instance => s_Instance;

        [Header("🎨 Renk ve Görsel Ayarları")]
        [Tooltip("Solid arka plan rengi (Land Flow su temasına uygun derin okyanus mavisi)")]
        [SerializeField] private Color m_SolidBackgroundColor = new Color(0.09f, 0.45f, 0.82f, 1f); // #1773D1
        [SerializeField] private Sprite m_LogoSprite;
        [SerializeField] private TMP_FontAsset m_Font;

        [Header("⏱️ Zamanlama ve Yumuşaklık (Smooth Timings)")]
        [Tooltip("Giriş kararma süresi (sn)")]
        [SerializeField] private float m_FadeInDuration = 0.35f;
        [Tooltip("Çıkış kararma süresi (sn)")]
        [SerializeField] private float m_FadeOutDuration = 0.45f;
        [Tooltip("Yükleme ekranının ekranda kalacağı ideal minimum süre (sn)")]
        [SerializeField] private float m_MinDisplayDuration = 1.6f;
        [Tooltip("%100 dolduktan sonra ekranda kalma nefes payı (sn)")]
        [SerializeField] private float m_HoldBeforeFadeOut = 0.18f;

        [Header("📝 Metin Ayarları (Dynamic Text)")]
        [Tooltip("Varsayılan yükleme ekranı metni")]
        [SerializeField] private string m_DefaultLoadingText = "LOADING";
        [Tooltip("Bölüm kaybedildiğinde gösterilecek metin")]
        [SerializeField] private string m_FailLevelText = "FAIL LEVEL";
        [Tooltip("Bölüm başarıyla tamamlandığında gösterilecek metin")]
        [SerializeField] private string m_LevelCompleteText = "LEVEL COMPLETED";

        private CanvasGroup m_CanvasGroup;
        private Image m_BackgroundImage;
        private Image m_LogoImage;
        private TextMeshProUGUI m_LoadingText;
        private TextMeshProUGUI m_SubText;
        private RectTransform m_ProgressBarFill;
        private RectTransform m_ProgressBarBg;
        private Coroutine m_TextDotRoutine;
        private Coroutine m_ActiveLoadingRoutine;
        private Tween m_LogoPulseTween;
        private Tween m_ProgressBarTween;
        private Tween m_SubTextPulseTween;
        private float m_VisualProgress = 0f;
        private bool m_IsVisible = false;
        private string m_CurrentBaseMessage = "LOADING";
        private string m_CurrentSubMessage = null;

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

            BuildUI();

            // Oyun ilk açıldığında doğrudan ekranda olsun (ekran flaş yapmasın)
            if (Time.realtimeSinceStartup < 1.5f)
            {
                m_CanvasGroup.alpha = 1f;
                m_CanvasGroup.blocksRaycasts = true;
                m_CanvasGroup.interactable = true;
                m_IsVisible = true;
                StartAnimations();
            }
            else
            {
                m_CanvasGroup.alpha = 0f;
                m_CanvasGroup.blocksRaycasts = false;
                m_CanvasGroup.interactable = false;
                m_IsVisible = false;
            }
        }

        private void Start()
        {
            // Oyun açılışında ilk seviye hazır olana kadar zarifçe ekranda kalıp yumuşakça gizle
            if (m_IsVisible && m_ActiveLoadingRoutine == null)
            {
                StartCoroutine(InitialStartRoutine());
            }
        }

        private IEnumerator InitialStartRoutine()
        {
            UpdateProgressBarImmediate(0f);
            AnimateProgressBarTo(1.0f, m_MinDisplayDuration, Ease.InOutQuad);

            float elapsed = 0f;
            while (elapsed < m_MinDisplayDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Sahnenin oturması için kısa nefes payı
            yield return new WaitForSecondsRealtime(m_HoldBeforeFadeOut);
            Hide();
        }

        /// <summary>
        /// Yükleme ekranı UI hiyerarşisini kodla eksiksiz ve bağımsız olarak inşa eder.
        /// </summary>
        private void BuildUI()
        {
            // Canvas ayarları
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // En üstte görünsün

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            m_CanvasGroup = GetComponent<CanvasGroup>();
            if (m_CanvasGroup == null) m_CanvasGroup = gameObject.AddComponent<CanvasGroup>();

            // 1. Solid Renkli Tam Ekran Arka Plan
            GameObject bgObj = new GameObject("SolidBackground");
            bgObj.transform.SetParent(transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            m_BackgroundImage = bgObj.AddComponent<Image>();
            m_BackgroundImage.color = m_SolidBackgroundColor;
            m_BackgroundImage.raycastTarget = true;

            // 2. İçerik Kapsayıcısı (Ekranın Tam Ortasında)
            GameObject centerContainer = new GameObject("CenterContent");
            centerContainer.transform.SetParent(transform, false);
            RectTransform centerRect = centerContainer.AddComponent<RectTransform>();
            centerRect.anchorMin = new Vector2(0.5f, 0.5f);
            centerRect.anchorMax = new Vector2(0.5f, 0.5f);
            centerRect.pivot = new Vector2(0.5f, 0.5f);
            centerRect.anchoredPosition = new Vector2(0f, 40f);
            centerRect.sizeDelta = new Vector2(900f, 800f);

            // 3. Land Flow Logosu
            if (m_LogoSprite == null)
            {
                #if UNITY_EDITOR
                m_LogoSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/LandFlow_Logo.png");
                #endif
                if (m_LogoSprite == null)
                {
                    m_LogoSprite = Resources.Load<Sprite>("LandFlow_Logo");
                }
            }

            GameObject logoObj = new GameObject("LandFlow_Logo");
            logoObj.transform.SetParent(centerContainer.transform, false);
            RectTransform logoRect = logoObj.AddComponent<RectTransform>();
            logoRect.anchorMin = new Vector2(0.5f, 0.6f);
            logoRect.anchorMax = new Vector2(0.5f, 0.6f);
            logoRect.pivot = new Vector2(0.5f, 0.5f);
            logoRect.sizeDelta = new Vector2(620f, 310f); // 2:1 oranında net ve canlı
            logoRect.anchoredPosition = new Vector2(0f, 50f);

            m_LogoImage = logoObj.AddComponent<Image>();
            m_LogoImage.sprite = m_LogoSprite;
            m_LogoImage.preserveAspect = true;
            m_LogoImage.raycastTarget = false;

            // 4. "LOADING..." Yazısı (Logonun Altında)
            if (m_Font == null)
            {
                #if UNITY_EDITOR
                m_Font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/LilitaOne-Regular SDF.asset");
                #endif
            }

            GameObject textObj = new GameObject("LoadingText");
            textObj.transform.SetParent(centerContainer.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.32f);
            textRect.anchorMax = new Vector2(0.5f, 0.32f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(750f, 90f);
            textRect.anchoredPosition = new Vector2(0f, -30f);

            m_LoadingText = textObj.AddComponent<TextMeshProUGUI>();
            if (m_Font != null) m_LoadingText.font = m_Font;
            m_LoadingText.text = "LOADING...";
            m_LoadingText.fontSize = 52;
            m_LoadingText.enableAutoSizing = true;
            m_LoadingText.fontSizeMin = 36;
            m_LoadingText.fontSizeMax = 52;
            m_LoadingText.fontStyle = FontStyles.Bold;
            m_LoadingText.alignment = TextAlignmentOptions.Center;
            m_LoadingText.color = Color.white;
            m_LoadingText.enableVertexGradient = true;
            m_LoadingText.colorGradient = new VertexGradient(
                Color.white,
                Color.white,
                new Color(0.85f, 0.95f, 1f, 1f),
                new Color(0.85f, 0.95f, 1f, 1f)
            );
            m_LoadingText.raycastTarget = false;

            // Gölgeli metin efekti
            var outline = textObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.02f, 0.15f, 0.35f, 0.85f);
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            // 5. Alt Başlık / Ödül Metni (SubText - Örn: "+20 COINS" veya "PICTURE COMPLETED!")
            GameObject subTextObj = new GameObject("SubText");
            subTextObj.transform.SetParent(centerContainer.transform, false);
            RectTransform subRect = subTextObj.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 0.28f);
            subRect.anchorMax = new Vector2(0.5f, 0.28f);
            subRect.pivot = new Vector2(0.5f, 0.5f);
            subRect.sizeDelta = new Vector2(650f, 50f);
            subRect.anchoredPosition = new Vector2(0f, -80f);

            m_SubText = subTextObj.AddComponent<TextMeshProUGUI>();
            if (m_Font != null) m_SubText.font = m_Font;
            m_SubText.fontSize = 32;
            m_SubText.fontStyle = FontStyles.Bold;
            m_SubText.alignment = TextAlignmentOptions.Center;
            m_SubText.color = new Color(1f, 0.88f, 0.25f, 1f); // Canlı altın sarısı
            m_SubText.enableVertexGradient = true;
            m_SubText.colorGradient = new VertexGradient(
                new Color(1f, 0.95f, 0.55f, 1f),
                new Color(1f, 0.95f, 0.55f, 1f),
                new Color(1f, 0.72f, 0.12f, 1f),
                new Color(1f, 0.72f, 0.12f, 1f)
            );
            m_SubText.raycastTarget = false;

            var subOutline = subTextObj.AddComponent<Outline>();
            subOutline.effectColor = new Color(0.02f, 0.12f, 0.28f, 0.90f);
            subOutline.effectDistance = new Vector2(2f, -2f);
            subTextObj.SetActive(false); // Varsayılanda gizli, sadece ödül/alt metin olduğunda gösterilir

            // 6. Şık İlerleme Çubuğu (Progress Bar)
            GameObject barBg = new GameObject("ProgressBar_BG");
            barBg.transform.SetParent(centerContainer.transform, false);
            m_ProgressBarBg = barBg.AddComponent<RectTransform>();
            m_ProgressBarBg.anchorMin = new Vector2(0.5f, 0.22f);
            m_ProgressBarBg.anchorMax = new Vector2(0.5f, 0.22f);
            m_ProgressBarBg.pivot = new Vector2(0.5f, 0.5f);
            m_ProgressBarBg.sizeDelta = new Vector2(520f, 26f);
            m_ProgressBarBg.anchoredPosition = new Vector2(0f, -135f);

            Image bgBarImg = barBg.AddComponent<Image>();
            bgBarImg.color = new Color(0.04f, 0.22f, 0.45f, 0.85f);
            bgBarImg.raycastTarget = false;

            // Çerçeve konturu
            var barBorder = barBg.AddComponent<Outline>();
            barBorder.effectColor = new Color(0.25f, 0.65f, 0.95f, 0.55f);
            barBorder.effectDistance = new Vector2(2f, -2f);

            // Köşe yuvarlama için maske
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
            fillImg.color = new Color(1f, 0.85f, 0.20f, 1f); // Canlı altın sarısı (Land Flow'un 'LAND' rengiyle uyumlu)
            fillImg.raycastTarget = false;
        }

        private void StartAnimations()
        {
            // Logo için tatlı ve yumuşak nefes alma (breathing) animasyonu
            if (m_LogoImage != null)
            {
                m_LogoPulseTween?.Kill();
                m_LogoImage.transform.localScale = Vector3.one;
                m_LogoPulseTween = m_LogoImage.transform
                    .DOScale(Vector3.one * 1.045f, 0.85f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetUpdate(true);
            }

            // "LOADING..." noktacık animasyonu
            if (m_TextDotRoutine != null) StopCoroutine(m_TextDotRoutine);
            m_TextDotRoutine = StartCoroutine(AnimateLoadingDots());
        }

        private void StopAnimations()
        {
            m_ProgressBarTween?.Kill();
            m_LogoPulseTween?.Kill();
            m_SubTextPulseTween?.Kill();
            if (m_LogoImage != null)
            {
                m_LogoImage.transform.localScale = Vector3.one;
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
        /// </summary>
        public void SetMessage(string message, string subMessage = null)
        {
            if (string.IsNullOrEmpty(message)) message = m_DefaultLoadingText;
            m_CurrentBaseMessage = message.TrimEnd('.');
            m_CurrentSubMessage = subMessage;

            if (m_LoadingText != null)
            {
                m_LoadingText.text = m_CurrentBaseMessage + "...";
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
        }

        private IEnumerator AnimateLoadingDots()
        {
            int dots = 0;
            while (true)
            {
                if (m_LoadingText != null)
                {
                    string suffix = new string('.', dots);
                    m_LoadingText.text = m_CurrentBaseMessage + suffix;
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
        /// İlerleme çubuğunu verilen süre boyunca ipeksi bir yumuşaklıkla hedefe doğru animasyonla doldurur.
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
            StartAnimations();

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
                StopAnimations();
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
            // 1. Ekranı mevcut mesajla ("LOADING", "FAIL LEVEL" veya "LEVEL COMPLETED") aç ve tam opak olmasını garantiye al
            Show(m_CurrentBaseMessage, null, m_CurrentSubMessage);
            if (m_CanvasGroup.alpha < 0.999f)
            {
                yield return m_CanvasGroup.DOFade(1f, m_FadeInDuration).SetUpdate(true).WaitForCompletion();
            }
            m_CanvasGroup.alpha = 1f;

            // 2. Barı akıcı bir şekilde %20'ye doğru başlat
            UpdateProgressBarImmediate(0f);
            AnimateProgressBarTo(0.20f, 0.35f, Ease.OutQuad);

            // 3. Arka planda asenkron sahne yüklemeyi başlat
            AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneBuildIndex);
            asyncOp.allowSceneActivation = false;

            float elapsed = 0f;
            float targetLoadWait = Mathf.Max(0.7f, minDuration * 0.65f);

            // Sahne arka planda yüklenirken ve minimum süre dolana kadar bar organik biçimde ilerlesin
            while (asyncOp.progress < 0.9f || elapsed < targetLoadWait)
            {
                elapsed += Time.unscaledDeltaTime;
                float progressRatio = Mathf.Clamp01(elapsed / targetLoadWait);
                float targetP = Mathf.Lerp(0.20f, 0.88f, Mathf.SmoothStep(0f, 1f, progressRatio));
                AnimateProgressBarTo(targetP, 0.1f, Ease.Linear);
                yield return null;
            }

            // 4. Sahne verisi hazır! Sahneyi aktifleştir
            AnimateProgressBarTo(0.92f, 0.2f, Ease.OutQuad);
            asyncOp.allowSceneActivation = true;

            while (!asyncOp.isDone)
            {
                yield return null;
            }

            // 5. Yeni sahne kurulduktan sonra Awake/Start ve Generator ilk karelerini tamamlasın diye 2 frame bekle
            yield return null;
            yield return new WaitForEndOfFrame();

            // 6. Barı tatmin edici şekilde %100'e tamamla
            AnimateProgressBarTo(1.0f, 0.35f, Ease.OutCubic);
            yield return new WaitForSecondsRealtime(0.38f);

            // 7. %100'de hafif bekleme ve yumuşak fade out ile oyuna geçiş
            yield return new WaitForSecondsRealtime(m_HoldBeforeFadeOut);

            Hide();
            m_ActiveLoadingRoutine = null;
        }

        /// <summary>
        /// Seviyeyi Land Flow yükleme ekranı arkasında temiz ve şık bir şekilde yükler.
        /// Seviye geçişlerinde (NextLevel) veya özel aksiyonlarda çağrılır.
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
            // 1. Ekranı aç ve %100 opak olana kadar bekle
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

            // 2. Yükleme aksiyonunu (ör. LevelManager.NextLevel) çalıştır
            try
            {
                loadAction?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LandFlowLoadingScreen] Seviye yükleme hatası: {ex}");
            }

            // 3. Generator'ın küpleri vs. oluşturması ve ilk render için 2 frame bekle
            yield return null;
            yield return new WaitForEndOfFrame();

            // 4. Kalan sürede barı %100'e pürüzsüz taşı
            float remainingTime = Mathf.Max(0.45f, minDuration - elapsed);
            AnimateProgressBarTo(1.0f, remainingTime, Ease.OutCubic);

            yield return new WaitForSecondsRealtime(remainingTime);
            yield return new WaitForSecondsRealtime(m_HoldBeforeFadeOut);

            Hide();
            m_ActiveLoadingRoutine = null;
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
