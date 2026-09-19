using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// UI butonlarına ve tıklanabilir slot/kartlara profesyonel dokunmatik basış (Juice / Punch) tepkisi verir.
    /// Basıldığında hafif küçülür (0.92x), bırakıldığında tatlı bir esneme ile orijinal boyutuna döner.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Casual UI Button Juice")]
    public class CasualUIButtonJuice : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Header("🧃 Juice Ayarları")]
        [Tooltip("Basıldığında küçülme oranı")]
        [SerializeField] private float m_PressedScaleMultiplier = 0.92f;

        [Tooltip("Basılma animasyon süresi (saniye)")]
        [SerializeField] private float m_PressDuration = 0.07f;

        [Tooltip("Bırakılma animasyon süresi (saniye)")]
        [SerializeField] private float m_ReleaseDuration = 0.14f;

        private Vector3 m_OriginalScale;
        private Tween m_CurrentTween;
        private bool m_IsPressed = false;

        private void Awake()
        {
            m_OriginalScale = transform.localScale;
            if (m_OriginalScale == Vector3.zero) m_OriginalScale = Vector3.one;
        }

        private void OnDisable()
        {
            KillTween();
            transform.localScale = m_OriginalScale;
            m_IsPressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            m_IsPressed = true;
            KillTween();
            Vector3 target = m_OriginalScale * m_PressedScaleMultiplier;
            m_CurrentTween = transform.DOScale(target, m_PressDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!m_IsPressed) return;
            m_IsPressed = false;
            BounceBack();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!m_IsPressed) return;
            m_IsPressed = false;
            BounceBack();
        }

        private void BounceBack()
        {
            KillTween();
            m_CurrentTween = transform.DOScale(m_OriginalScale, m_ReleaseDuration)
                .SetEase(Ease.OutBack, 2.5f)
                .SetUpdate(true);
        }

        private void KillTween()
        {
            if (m_CurrentTween != null && m_CurrentTween.IsActive())
            {
                m_CurrentTween.Kill();
                m_CurrentTween = null;
            }
        }
    }
}
