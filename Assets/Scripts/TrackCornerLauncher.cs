using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// Konveyör bandının sol alt köşesindeki interaktif ve animasyonlu fırlatma istasyonu (Corner Launch Station).
    /// - Parlak 3B köşe gövdesi ve neon halkası (Corner_BasePlate, Corner_NeonRing),
    /// - Yaylı fiziksel 3B buton basma tepkisi (Corner_LaunchButton + DOPunchScale / DOScale),
    /// - Vagon fırlatıldığında genişleyen şok dalgası halkası (Corner_Shockwave),
    /// - Dinamik LilitaOne sayaç rozeti (2/5),
    /// - Canlı "gif" benzeri nefes alma (idle breathe pulse) animasyonu.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Track Corner Launcher")]
    public class TrackCornerLauncher : MonoBehaviour
    {
        private static TrackCornerLauncher s_Instance;
        public static TrackCornerLauncher Instance => s_Instance;

        [Header("🎨 UI Bileşenleri")]
        [SerializeField] private Image m_BasePlate;
        [SerializeField] private Image m_NeonRing;
        [SerializeField] private RectTransform m_ButtonTransform;
        [SerializeField] private Image m_ButtonImage;
        [SerializeField] private Image m_ShockwaveImage;
        [SerializeField] private RectTransform m_CounterBadgeRect;
        [SerializeField] private TextMeshProUGUI m_CounterTMP;

        [Header("⚙️ Durum")]
        [SerializeField] private int m_CurrentCount = 0;
        [SerializeField] private int m_MaxCount = 5;

        private Tween m_IdleGlowTween;
        private Tween m_IdleBreatheTween;
        private Vector3 m_InitialButtonLocalPos;

        private void Awake()
        {
            s_Instance = this;
            if (m_ButtonTransform != null)
            {
                m_InitialButtonLocalPos = m_ButtonTransform.localPosition;
            }
        }

        private void OnEnable()
        {
            s_Instance = this;
            if (Application.isPlaying)
            {
                StartIdleAnimations();
            }
        }

        private void OnDisable()
        {
            StopIdleAnimations();
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                StartIdleAnimations();
            }
        }

        public void StartIdleAnimations()
        {
            StopIdleAnimations();

            // 1. Neon Halka Nefes Alma Animasyonu (Canlı arcade hissi)
            if (m_NeonRing != null)
            {
                Color c = m_NeonRing.color;
                c.a = 0.45f;
                m_NeonRing.color = c;
                m_IdleGlowTween = m_NeonRing.DOFade(0.95f, 1.4f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo);
            }

            // 2. Butonun hafif canlı mikro-nabız animasyonu
            if (m_ButtonTransform != null)
            {
                m_ButtonTransform.localScale = Vector3.one;
                m_IdleBreatheTween = m_ButtonTransform.DOScale(1.04f, 1.8f)
                    .SetEase(Ease.InOutQuad)
                    .SetLoops(-1, LoopType.Yoyo);
            }
        }

        public void StopIdleAnimations()
        {
            if (m_IdleGlowTween != null && m_IdleGlowTween.IsActive()) m_IdleGlowTween.Kill();
            if (m_IdleBreatheTween != null && m_IdleBreatheTween.IsActive()) m_IdleBreatheTween.Kill();
        }

        /// <summary>
        /// Vagon raya fırlatıldığında veya slota yerleştiğinde çağrılır:
        /// Buton fiziksel olarak aşağı basılır, yaylanır, şok dalgası yayılır ve sayaç güncellenir.
        /// </summary>
        public void TriggerLaunch(int currentCount, int maxCount)
        {
            m_CurrentCount = currentCount;
            m_MaxCount = maxCount;
            UpdateCounterText();

            if (!Application.isPlaying) return;

            // 1. Buton Basma & Yaylanma Animasyonu (Tactile Juicy Button Press)
            if (m_ButtonTransform != null)
            {
                m_ButtonTransform.DOKill();
                m_ButtonTransform.localPosition = m_InitialButtonLocalPos;
                m_ButtonTransform.localScale = Vector3.one;

                // Butonu hızla ez (squash down)
                Sequence seq = DOTween.Sequence();
                seq.Append(m_ButtonTransform.DOScale(new Vector3(1.22f, 0.74f, 1f), 0.07f).SetEase(Ease.OutQuad));
                seq.Join(m_ButtonTransform.DOLocalMoveY(m_InitialButtonLocalPos.y - 8f, 0.07f).SetEase(Ease.OutQuad));

                // Yaylanarak geri sıçrat (spring bounce back)
                seq.Append(m_ButtonTransform.DOScale(Vector3.one, 0.26f).SetEase(Ease.OutBack));
                seq.Join(m_ButtonTransform.DOLocalMoveY(m_InitialButtonLocalPos.y, 0.26f).SetEase(Ease.OutBack));

                seq.OnComplete(() =>
                {
                    StartIdleAnimations();
                });
            }

            // 2. Neon Halka Parlama Patlaması (Flash Glow)
            if (m_NeonRing != null)
            {
                m_NeonRing.DOKill();
                Color c = m_NeonRing.color;
                c.a = 1f;
                m_NeonRing.color = c;
                m_NeonRing.DOFade(0.45f, 0.4f).SetEase(Ease.OutQuad).OnComplete(() =>
                {
                    if (m_IdleGlowTween == null || !m_IdleGlowTween.IsActive())
                    {
                        m_IdleGlowTween = m_NeonRing.DOFade(0.95f, 1.4f)
                            .SetEase(Ease.InOutSine)
                            .SetLoops(-1, LoopType.Yoyo);
                    }
                });
            }

            // 3. Genişleyen Şok Dalgası Halkası (Shockwave Ripple)
            if (m_ShockwaveImage != null)
            {
                Transform swTr = m_ShockwaveImage.transform;
                swTr.DOKill();
                m_ShockwaveImage.DOKill();

                swTr.localScale = Vector3.one * 0.75f;
                Color sc = m_ShockwaveImage.color;
                sc.a = 0.95f;
                m_ShockwaveImage.color = sc;
                m_ShockwaveImage.gameObject.SetActive(true);

                swTr.DOScale(Vector3.one * 1.65f, 0.38f).SetEase(Ease.OutQuad);
                m_ShockwaveImage.DOFade(0f, 0.38f).SetEase(Ease.OutQuad).OnComplete(() =>
                {
                    m_ShockwaveImage.gameObject.SetActive(false);
                });
            }

            // 4. Sayaç Rozeti Punch Efekti
            if (m_CounterBadgeRect != null)
            {
                m_CounterBadgeRect.DOKill();
                m_CounterBadgeRect.localScale = Vector3.one;
                m_CounterBadgeRect.DOPunchScale(Vector3.one * 0.24f, 0.28f, 6, 0.5f);
            }
        }

        public void SetCount(int currentCount, int maxCount)
        {
            m_CurrentCount = currentCount;
            m_MaxCount = maxCount;
            UpdateCounterText();
        }

        private void UpdateCounterText()
        {
            if (m_CounterTMP != null)
            {
                m_CounterTMP.text = $"{m_CurrentCount}/{m_MaxCount}";
            }
        }

        public TextMeshProUGUI GetCounterTMP() => m_CounterTMP;
    }
}
