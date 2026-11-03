using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// Level 1 icin ozel etkilesimli egitim (tutorial) kontrolcusu.
    /// Konteyner alanini acik (belirgin) birakip ekranin diger tum kisimlarini
    /// yari saydam golgeler (cutout / spotlight). Ilk bastaki konteynerin texti uzerine
    /// sakin ve yavas bir el (dokunma) animasyonu gosterir. Oyuncu konteynere dokunup
    /// slota gonderdiginde yumusak bir fade-out ile kaybolur.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Level Tutorial Controller")]
    public class LevelTutorialController : MonoBehaviour
    {
        public static LevelTutorialController Instance { get; private set; }

        [Header("Gorsel Ayarlar")]
        [Tooltip("Konteyner disindaki alanlari kaplayan golgenin rengi ve opakligi")]
        [SerializeField] private Color m_ShadowColor = new Color(0.02f, 0.05f, 0.11f, 0.72f);

        [Header("Ipuclari ve Efektler")]
        [Tooltip("Metin balonu (Kullanici istegiyle kapatildi)")]
        [SerializeField] private bool m_ShowInstructionBadge = false;
        [SerializeField] private string m_TutorialPromptText = "Gemiyi gondermek icin dokun!";
        [SerializeField] private bool m_EnableHandPointer = true;

        [Tooltip("El ikonunun sayi textine gore ince konum ofseti")]
        [SerializeField] private Vector2 m_HandOffsetFromText = new Vector2(0f, 10f);

        [Header("Davranis Ayarlari")]
        [Tooltip("Sadece Level 1'de calisir.")]
        [SerializeField] private int m_TargetLevelNumber = 1;

        // UI Elemanlari
        private const float HandSize = 85f;
        private static readonly Vector2 HandOffsetFromNumber = new Vector2(58f, -42f);

        private CanvasGroup m_CanvasGroup;
        private RectTransform m_RootRect;
        private RectTransform m_ShadowTop;
        private RectTransform m_ShadowBottom;
        private RectTransform m_ShadowLeft;
        private RectTransform m_ShadowRight;

        private RectTransform m_BadgeRect;
        private TextMeshProUGUI m_BadgeText;
        private RectTransform m_HandPointerRect;
        private Image m_HandPointerImage;

        // Durum
        private bool m_IsActive = false;
        private bool m_IsDismissing = false;
        private bool m_UiInitialized = false;
        private Coroutine m_PositionTrackingRoutine;
        private Sequence m_HandSequence;
        private Vector2 m_CurrentHandTargetPos = Vector2.zero;
        private bool m_HandAnimationStarted = false;
        private float m_ShownAt;

        public bool IsActive => m_IsActive;

        private void Awake()
        {
            Instance = this;
            EnsureCanvasGroup();
        }

        private void OnEnable()
        {
            PixelArtGenerator.LevelLoaded += HandleLevelLoaded;
            ShipDispatcher.ShipDispatchedFromQueue += HandleShipDispatched;
        }

        private void OnDisable()
        {
            PixelArtGenerator.LevelLoaded -= HandleLevelLoaded;
            ShipDispatcher.ShipDispatchedFromQueue -= HandleShipDispatched;
            KillAnimations();
        }

        private void Start()
        {
            EnsureUIHierarchy();
            CheckCurrentLevelAndShow();
        }

        /// <summary>
        /// Level yuklendiginde tetiklenir.
        /// </summary>
        private void HandleLevelLoaded(PixelLevelData levelData)
        {
            int levelNum = 1;
            if (LevelManager.Instance != null)
            {
                levelNum = LevelManager.Instance.CurrentLevelIndex + 1;
            }
            else if (levelData != null)
            {
                levelNum = levelData.LevelIndex;
            }

            OnLevelChanged(levelNum);
        }

        /// <summary>
        /// Disaridan (or. CasualHudController) seviye degistiginde cagrilir.
        /// </summary>
        public void OnLevelChanged(int levelNumber)
        {
            if (levelNumber == m_TargetLevelNumber)
            {
                // Henuz gemi gonderilmediyse tutorial'i ac
                int sentCount = ShipDispatcher.Instance != null ? ShipDispatcher.Instance.PlayerSentShipCount : 0;
                if (sentCount == 0)
                {
                    ShowTutorial();
                }
                else
                {
                    HideTutorial(false);
                }
            }
            else
            {
                HideTutorial(false);
            }
        }

        /// <summary>
        /// Oyuncu bir gemi gonderdiginde tutorial kapanir.
        /// </summary>
        private void HandleShipDispatched(ShipController ship)
        {
            if (m_IsActive)
            {
                DismissTutorial();
            }
        }

        private void CheckCurrentLevelAndShow()
        {
            int currentLevel = 1;
            if (LevelManager.Instance != null)
            {
                currentLevel = LevelManager.Instance.CurrentLevelIndex + 1;
            }
            OnLevelChanged(currentLevel);
        }

        private void EnsureCanvasGroup()
        {
            m_CanvasGroup = GetComponent<CanvasGroup>();
            if (m_CanvasGroup == null)
            {
                m_CanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        /// <summary>
        /// 4 parcali golge ve el isaretcisi UI yapisini dinamik olarak insa eder.
        /// </summary>
        public void EnsureUIHierarchy()
        {
            EnsureCanvasGroup();
            m_RootRect = transform as RectTransform;
            if (m_RootRect == null) m_RootRect = gameObject.AddComponent<RectTransform>();

            // Tam ekran yayil
            m_RootRect.anchorMin = Vector2.zero;
            m_RootRect.anchorMax = Vector2.one;
            m_RootRect.offsetMin = Vector2.zero;
            m_RootRect.offsetMax = Vector2.zero;
            m_RootRect.localScale = Vector3.one;

            // 1. Golge Panelleri (Top, Bottom, Left, Right)
            m_ShadowTop = CreateShadowPanel("Shadow_Top");
            m_ShadowBottom = CreateShadowPanel("Shadow_Bottom");
            m_ShadowLeft = CreateShadowPanel("Shadow_Left");
            m_ShadowRight = CreateShadowPanel("Shadow_Right");

            // 2. Eski cerceve varsa temizle (Konteyner uzerinde hicbir isaret olmayacak)
            Transform existingFrame = transform.Find("Spotlight_Frame");
            if (existingFrame != null)
            {
                SafeDestroy(existingFrame.gameObject);
            }

            // 3. Bilgi Balonu (Kullanici istegi: texti kaldir)
            Transform existingBadge = transform.Find("Instruction_Badge");
            if (!m_ShowInstructionBadge)
            {
                if (existingBadge != null)
                {
                    SafeDestroy(existingBadge.gameObject);
                }
                m_BadgeRect = null;
                m_BadgeText = null;
            }
            else
            {
                CreateInstructionBadge();
            }

            // 4. El Isaretcisi (Hand Pointer)
            CreateHandPointer();

            m_UiInitialized = true;
        }

        private RectTransform CreateShadowPanel(string name)
        {
            Transform existing = transform.Find(name);
            GameObject panelGo = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(transform, false);

            Image img = panelGo.GetComponent<Image>();
            img.color = m_ShadowColor;
            img.raycastTarget = true; // Golgeye dokunulursa alttaki ogeleri engeller

            // Golgeye tiklandiginda oyuncuyu el isaretcisine yonlendir
            Button btn = panelGo.GetComponent<Button>();
            if (btn == null) btn = panelGo.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnShadowTapped);

            RectTransform rt = panelGo.transform as RectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        private void CreateInstructionBadge()
        {
            Transform existing = transform.Find("Instruction_Badge");
            GameObject badgeGo = existing != null ? existing.gameObject : new GameObject("Instruction_Badge", typeof(RectTransform), typeof(Image));
            badgeGo.transform.SetParent(transform, false);

            m_BadgeRect = badgeGo.transform as RectTransform;
            m_BadgeRect.anchorMin = new Vector2(0.5f, 0.5f);
            m_BadgeRect.anchorMax = new Vector2(0.5f, 0.5f);
            m_BadgeRect.pivot = new Vector2(0.5f, 0.5f);
            m_BadgeRect.sizeDelta = new Vector2(580f, 84f);

            Image bg = badgeGo.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.14f, 0.24f, 0.92f);
            bg.raycastTarget = false;

            Sprite pillSprite = Resources.Load<Sprite>("Badge_Pill");
            if (pillSprite == null) pillSprite = FindSpriteByName("Badge_MiniPill");
            if (pillSprite == null) pillSprite = FindSpriteByName("panellevel_clean");
            if (pillSprite != null)
            {
                bg.sprite = pillSprite;
                bg.type = Image.Type.Sliced;
            }

            Transform textTr = m_BadgeRect.Find("Text");
            GameObject textGo = textTr != null ? textTr.gameObject : new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(m_BadgeRect, false);

            RectTransform textRt = textGo.transform as RectTransform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(20f, 0f);
            textRt.offsetMax = new Vector2(-20f, 0f);

            m_BadgeText = textGo.GetComponent<TextMeshProUGUI>();
            m_BadgeText.text = m_TutorialPromptText;
            m_BadgeText.alignment = TextAlignmentOptions.Center;
            m_BadgeText.fontSize = 32f;
            m_BadgeText.fontStyle = FontStyles.Bold;
            m_BadgeText.color = Color.white;
            m_BadgeText.raycastTarget = false;

            if (CasualHudController.Instance != null && CasualHudController.Instance.LevelText != null)
            {
                m_BadgeText.font = CasualHudController.Instance.LevelText.font;
            }
        }

        private void CreateHandPointer()
        {
            Transform existing = transform.Find("Hand_Pointer");
            GameObject pointerGo = existing != null ? existing.gameObject : new GameObject("Hand_Pointer", typeof(RectTransform), typeof(Image));
            pointerGo.transform.SetParent(transform, false);

            // Eski Dot, Ripple gibi cocuk nesneleri tamamen temizle ("hic su isaret olmasin")
            for (int i = pointerGo.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = pointerGo.transform.GetChild(i);
                SafeDestroy(child.gameObject);
            }

            m_HandPointerRect = pointerGo.transform as RectTransform;
            m_HandPointerRect.anchorMin = new Vector2(0.5f, 0.5f);
            m_HandPointerRect.anchorMax = new Vector2(0.5f, 0.5f);
            // Parmak ucu sayiyi kapatmadan yanina gelecek sekilde konum HandOffsetFromNumber ile kaydirilir
            m_HandPointerRect.pivot = new Vector2(0.46f, 0.62f);
            m_HandPointerRect.sizeDelta = new Vector2(HandSize, HandSize);

            m_HandPointerImage = pointerGo.GetComponent<Image>();
            if (m_HandPointerImage == null) m_HandPointerImage = pointerGo.AddComponent<Image>();
            m_HandPointerImage.sprite = GetHandSprite();
            m_HandPointerImage.color = Color.white;
            m_HandPointerImage.raycastTarget = false;
            m_HandPointerImage.preserveAspect = true;
        }

        /// <summary>
        /// El ikonu konteyner sayisinin (kendi Canvas'i: kok + 20) ustunde cizilsin. overrideSorting yalnizca
        /// obje aktifken uygulandigi icin egitim gosterilirken cagrilir.
        /// </summary>
        private void ApplyHandSorting()
        {
            if (m_HandPointerRect == null) return;

            Canvas parentCanvas = m_HandPointerRect.parent != null ? m_HandPointerRect.parent.GetComponentInParent<Canvas>() : null;
            Canvas rootCanvas = parentCanvas != null ? parentCanvas.rootCanvas : null;

            Canvas handCanvas = m_HandPointerRect.GetComponent<Canvas>();
            if (handCanvas == null) handCanvas = m_HandPointerRect.gameObject.AddComponent<Canvas>();
            handCanvas.overrideSorting = true;
            if (rootCanvas != null)
            {
                handCanvas.sortingLayerID = rootCanvas.sortingLayerID;
                handCanvas.sortingOrder = rootCanvas.sortingOrder + 100;
            }
            else
            {
                handCanvas.sortingOrder = 150;
            }
        }

        private Sprite GetHandSprite()
        {
            Sprite sp = Resources.Load<Sprite>("Tutorial_Touch");
            if (sp != null) return sp;

            sp = FindSpriteByName("Touch");
            if (sp != null) return sp;

#if UNITY_EDITOR
            sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/Colored Icons/Touch.png");
            if (sp != null) return sp;
            sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/White Icons/White Click.png");
            if (sp != null) return sp;
#endif

            return null;
        }

        /// <summary>
        /// Egitim katmanini acar ve gemilerin konumuna gore spotlight'i ayarlar.
        /// </summary>
        public void ShowTutorial()
        {
            EnsureUIHierarchy();
            gameObject.SetActive(true);
            if (!m_IsActive) m_ShownAt = Time.unscaledTime;
            m_IsActive = true;
            m_IsDismissing = false;
            m_HandAnimationStarted = false;
            LandFlowAnalytics.DesignOnce("FTUE:Tutorial:Shown");

            // En uste getir (Canvas'taki diger HUD ogelerinin ustunde golge olabilsin)
            transform.SetAsLastSibling();
            ApplyHandSorting();

            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.DOKill();
                m_CanvasGroup.alpha = 0f;
                m_CanvasGroup.DOFade(1f, 0.35f).SetEase(Ease.OutQuad);
            }

            if (m_PositionTrackingRoutine != null) StopCoroutine(m_PositionTrackingRoutine);
            m_PositionTrackingRoutine = StartCoroutine(TrackAndFitCutoutRoutine());
        }

        /// <summary>
        /// Egitim katmanini kapatir.
        /// </summary>
        public void DismissTutorial()
        {
            if (!m_IsActive || m_IsDismissing) return;
            m_IsDismissing = true;
            // Değer: el işaretinin görünmesinden ilk gemi gönderimine kadar geçen saniye
            LandFlowAnalytics.DesignOnce("FTUE:Tutorial:Done", Time.unscaledTime - m_ShownAt);

            KillAnimations();

            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.DOKill();
                m_CanvasGroup.DOFade(0f, 0.30f).SetEase(Ease.OutQuad).OnComplete(() =>
                {
                    m_IsActive = false;
                    m_IsDismissing = false;
                    gameObject.SetActive(false);
                });
            }
            else
            {
                m_IsActive = false;
                m_IsDismissing = false;
                gameObject.SetActive(false);
            }
        }

        public void HideTutorial(bool animated = true)
        {
            if (animated)
            {
                DismissTutorial();
            }
            else
            {
                m_IsActive = false;
                m_IsDismissing = false;
                KillAnimations();
                gameObject.SetActive(false);
            }
        }

        private IEnumerator TrackAndFitCutoutRoutine()
        {
            // Gemiler ilk frame'de henuz konumlanmamis olabilir; birkac frame takip et
            for (int i = 0; i < 60; i++)
            {
                if (m_IsDismissing || !m_IsActive) yield break;
                UpdateCutoutGeometry();
                yield return new WaitForSeconds(0.04f);
            }

            // Oyun esnasinda gemiler dururken periyodik olarak hafifce guncel tut
            while (m_IsActive && !m_IsDismissing)
            {
                UpdateCutoutGeometry();
                yield return new WaitForSeconds(0.2f);
            }
        }

        /// <summary>
        /// Sahnedeki kuyruk gemilerinin ekran koordinatlarini hesaplayarak 4 golge panelini
        /// tam konteyner alanini cevreleyecek sekilde konumlandirir ve ilk konteynerin texti uzerine el animasyonu baslatir.
        /// </summary>
        private void UpdateCutoutGeometry()
        {
            if (m_RootRect == null) return;

            float canvasW = m_RootRect.rect.width;
            float canvasH = m_RootRect.rect.height;
            if (canvasW <= 10f || canvasH <= 10f)
            {
                canvasW = 1080f;
                canvasH = 1920f;
            }

            Rect cutoutRect;
            bool calculated = TryCalculateContainerBounds(out cutoutRect);
            if (!calculated)
            {
                // Standart guvenli alt alan fallback'i (1080x1920 icin)
                cutoutRect = new Rect(-500f, -870f, 1000f, 470f);
            }

            float cutMinX = cutoutRect.xMin;
            float cutMaxX = cutoutRect.xMax;
            float cutMinY = cutoutRect.yMin;
            float cutMaxY = cutoutRect.yMax;

            // 1. Ust Golge Paneli (cutMaxY'den ekranin en tepesine kadar)
            float topH = Mathf.Max(0f, (canvasH * 0.5f) - cutMaxY);
            float topY = (cutMaxY + (canvasH * 0.5f)) * 0.5f;
            m_ShadowTop.sizeDelta = new Vector2(canvasW, topH);
            m_ShadowTop.anchoredPosition = new Vector2(0f, topY);

            // 2. Alt Golge Paneli (ekranin en dibinden cutMinY'ye kadar)
            float botH = Mathf.Max(0f, cutMinY - (-canvasH * 0.5f));
            float botY = (cutMinY + (-canvasH * 0.5f)) * 0.5f;
            m_ShadowBottom.sizeDelta = new Vector2(canvasW, botH);
            m_ShadowBottom.anchoredPosition = new Vector2(0f, botY);

            // 3. Sol Golge Paneli (ekranin sol kenarindan cutMinX'e kadar, dikeyde delik boyunca)
            float leftW = Mathf.Max(0f, cutMinX - (-canvasW * 0.5f));
            float leftH = Mathf.Max(0f, cutMaxY - cutMinY);
            float leftX = (cutMinX + (-canvasW * 0.5f)) * 0.5f;
            float midY = (cutMinY + cutMaxY) * 0.5f;
            m_ShadowLeft.sizeDelta = new Vector2(leftW, leftH);
            m_ShadowLeft.anchoredPosition = new Vector2(leftX, midY);

            // 4. Sag Golge Paneli (cutMaxX'ten ekranin sag kenarina kadar, dikeyde delik boyunca)
            float rightW = Mathf.Max(0f, (canvasW * 0.5f) - cutMaxX);
            float rightH = leftH;
            float rightX = (cutMaxX + (canvasW * 0.5f)) * 0.5f;
            m_ShadowRight.sizeDelta = new Vector2(rightW, rightH);
            m_ShadowRight.anchoredPosition = new Vector2(rightX, midY);

            // 5. El isaretcisini ilk konteynerin texti uzerine yerlestirip yavas tiklatma animasyonunu guncelle
            UpdateHandPosition();
        }

        private void UpdateHandPosition()
        {
            if (m_HandPointerRect == null || !m_EnableHandPointer) return;

            Vector2 targetPos;
            if (TryGetFirstContainerPosition(out targetPos))
            {
                if (!m_HandAnimationStarted || Vector2.Distance(m_CurrentHandTargetPos, targetPos) > 4f)
                {
                    m_CurrentHandTargetPos = targetPos;
                    StartHandTapAnimation(targetPos);
                }
            }
        }

        /// <summary>
        /// Text uzerinde sakin, yavas ve dogal bir dokunma animasyonu yurutur.
        /// </summary>
        private void StartHandTapAnimation(Vector2 targetPos)
        {
            if (m_HandPointerRect == null || !m_EnableHandPointer) return;

            if (m_HandSequence != null)
            {
                m_HandSequence.Kill();
                m_HandSequence = null;
            }

            // Sayiyi kapatmasin: el sayinin sag altinda durur, dinlenirken hafifce yukari kalkar (+18px dikey)
            targetPos += HandOffsetFromNumber;
            Vector2 restPos = targetPos + new Vector2(0f, 18f);

            m_HandPointerRect.DOKill();
            m_HandPointerRect.anchoredPosition = restPos;
            m_HandPointerRect.localScale = Vector3.one;

            m_HandSequence = DOTween.Sequence();
            // 1. Eli sakin ve yavas sekilde text uzerine yaklastir (0.52s)
            m_HandSequence.Append(m_HandPointerRect.DOAnchorPos(targetPos, 0.52f).SetEase(Ease.InOutSine));
            // 2. Text uzerine dokunma/tiklatma basisi - Squash (0.16s)
            m_HandSequence.Append(m_HandPointerRect.DOScale(new Vector3(0.88f, 0.88f, 1f), 0.16f).SetEase(Ease.OutQuad));
            // 3. Basisin yaylanarak geri acilmasi (0.16s)
            m_HandSequence.Append(m_HandPointerRect.DOScale(Vector3.one, 0.16f).SetEase(Ease.OutQuad));
            // 4. Eli yavasca yukariya dinlenme konumuna geri al (0.54s)
            m_HandSequence.Append(m_HandPointerRect.DOAnchorPos(restPos, 0.54f).SetEase(Ease.InOutSine));
            // 5. Bir sonraki tiklama oncesi rahat bekleme (0.45s)
            m_HandSequence.AppendInterval(0.45f);

            m_HandSequence.SetLoops(-1, LoopType.Restart);
            m_HandAnimationStarted = true;
        }

        /// <summary>
        /// Sahnedeki ilk konteynerin kapasite sayisi text'inin (TextMeshProUGUI) Canvas uzerindeki yerel noktasini bulur.
        /// </summary>
        private bool TryGetFirstContainerPosition(out Vector2 localPt)
        {
            localPt = Vector2.zero;
            Camera cam = Camera.main;
            if (cam == null) cam = FindFirstObjectByType<Camera>();

            ShipQueuePool pool = ShipQueuePool.Instance != null ? ShipQueuePool.Instance : FindFirstObjectByType<ShipQueuePool>();
            ShipController firstShip = null;

            if (pool != null)
            {
                var waitingShips = pool.GetActiveWaitingShips();
                if (waitingShips != null && waitingShips.Count > 0)
                {
                    float minX = float.MaxValue;
                    for (int i = 0; i < waitingShips.Count; i++)
                    {
                        var s = waitingShips[i];
                        if (s == null || !s.gameObject.activeInHierarchy || s.IsDocked || s.IsDeparting) continue;
                        if (pool.IsFrontRow(s))
                        {
                            if (s.transform.position.x < minX)
                            {
                                minX = s.transform.position.x;
                                firstShip = s;
                            }
                        }
                    }

                    if (firstShip == null && waitingShips.Count > 0)
                    {
                        firstShip = waitingShips[0];
                    }
                }

                // Gemi henuz bulunamadiysa kuyruk spot 0 konumuna bak
                if (firstShip == null && pool.QueueSpots != null && pool.QueueSpots.Count > 0 && pool.QueueSpots[0] != null)
                {
                    Transform spot0 = pool.QueueSpots[0];
                    if (spot0 is RectTransform spotRt)
                    {
                        Vector3 screenPt = RectTransformUtility.WorldToScreenPoint(cam, spotRt.position);
                        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(m_RootRect, screenPt, cam, out localPt))
                        {
                            localPt += m_HandOffsetFromText;
                            return true;
                        }
                    }
                    else if (cam != null)
                    {
                        Vector3 screenPt = cam.WorldToScreenPoint(spot0.position);
                        if (screenPt.z >= 0)
                        {
                            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(m_RootRect, screenPt, cam, out localPt))
                            {
                                localPt += m_HandOffsetFromText;
                                return true;
                            }
                        }
                    }
                }
            }

            if (firstShip != null)
            {
                // Ilk once dogrudan sayi text'ini (TextMeshProUGUI / Badge_Text) bul ve tam text uzerine konumlan
                TextMeshProUGUI badgeText = firstShip.GetComponentInChildren<TextMeshProUGUI>(true);
                Transform targetTransform = badgeText != null ? badgeText.transform : firstShip.transform;

                if (targetTransform is RectTransform targetRt)
                {
                    Vector3 screenPt = RectTransformUtility.WorldToScreenPoint(cam, targetRt.position);
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(m_RootRect, screenPt, cam, out localPt))
                    {
                        localPt += m_HandOffsetFromText;
                        return true;
                    }
                }
                else if (cam != null)
                {
                    Vector3 screenPt = cam.WorldToScreenPoint(targetTransform.position);
                    if (screenPt.z >= 0)
                    {
                        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(m_RootRect, screenPt, cam, out localPt))
                        {
                            localPt += m_HandOffsetFromText;
                            return true;
                        }
                    }
                }
            }

            // Fallback: Cutout dikdortgeninin ilk ucte birlik kismi
            Rect cutoutRect;
            if (TryCalculateContainerBounds(out cutoutRect))
            {
                localPt = new Vector2(cutoutRect.xMin + (cutoutRect.width * 0.22f), cutoutRect.center.y) + m_HandOffsetFromText;
                return true;
            }

            return false;
        }

        private bool TryCalculateContainerBounds(out Rect canvasRect)
        {
            canvasRect = default;
            Camera cam = Camera.main;
            if (cam == null) cam = FindFirstObjectByType<Camera>();
            if (cam == null) return false;

            ShipQueuePool pool = ShipQueuePool.Instance != null ? ShipQueuePool.Instance : FindFirstObjectByType<ShipQueuePool>();
            if (pool == null) return false;

            var waitingShips = pool.GetActiveWaitingShips();
            if (waitingShips == null || waitingShips.Count == 0) return false;

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            int count = 0;
            for (int i = 0; i < waitingShips.Count; i++)
            {
                ShipController ship = waitingShips[i];
                if (ship == null || !ship.gameObject.activeInHierarchy || ship.IsDocked || ship.IsDeparting) continue;

                Vector3 worldPos = ship.transform.position;
                Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

                if (screenPos.z < 0) continue;

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(m_RootRect, screenPos, cam, out Vector2 localPt))
                {
                    minX = Mathf.Min(minX, localPt.x);
                    maxX = Mathf.Max(maxX, localPt.x);
                    minY = Mathf.Min(minY, localPt.y);
                    maxY = Mathf.Max(maxY, localPt.y);
                    count++;
                }
            }

            if (count == 0) return false;

            // Gemi boyutuna gore nefes payi (padding)
            float padX = 145f;
            float padY = 125f;

            minX -= padX;
            maxX += padX;
            minY -= padY;
            maxY += padY;

            canvasRect = new Rect(minX, minY, maxX - minX, maxY - minY);
            return true;
        }

        private void KillAnimations()
        {
            if (m_HandSequence != null)
            {
                m_HandSequence.Kill();
                m_HandSequence = null;
            }

            if (m_HandPointerRect != null)
            {
                m_HandPointerRect.DOKill();
            }

            if (m_BadgeRect != null) m_BadgeRect.DOKill();
        }

        /// <summary>
        /// Oyuncu karartilmis alana dokunursa el isaretcisini hafifce sallar (dikkat ceker).
        /// </summary>
        private void OnShadowTapped()
        {
            if (m_HandPointerRect != null)
            {
                m_HandPointerRect.DOKill(true);
                m_HandPointerRect.DOPunchRotation(new Vector3(0f, 0f, 15f), 0.32f, 8, 1f);
            }
        }

        private static void SafeDestroy(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(go);
            }
            else
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (go != null)
                    {
                        DestroyImmediate(go);
                    }
                };
#else
                Destroy(go);
#endif
            }
        }

        private static Sprite FindSpriteByName(string nameSnippet)
        {
            var allImgs = UnityEngine.Object.FindObjectsByType<Image>(FindObjectsSortMode.None);
            foreach (var img in allImgs)
            {
                if (img != null && img.sprite != null && img.sprite.name.IndexOf(nameSnippet, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return img.sprite;
                }
            }
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets($"{nameSnippet} t:Sprite");
            if (guids != null && guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                Sprite sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp != null) return sp;
            }
#endif
            return null;
        }
    }
}
