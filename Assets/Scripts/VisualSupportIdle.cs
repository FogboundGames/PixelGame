using UnityEngine;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// Sahnedeki yardımcı görsel destek karakterlerine / robotlarına / turret'lara
    /// canlı ve tatlı bir idle nefes alma / hafif salınım (bobbing) animasyonu kazandırır.
    /// Gameplay mekaniklerine hiçbir müdahalede bulunmaz.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Visual Support Idle")]
    public class VisualSupportIdle : MonoBehaviour
    {
        [Header("🕊️ Salınım (Float / Bobbing)")]
        [SerializeField] private float m_FloatDistance = 12f;
        [SerializeField] private float m_FloatDuration = 2.4f;

        [Header("💨 Nefes (Breathing Scale)")]
        [SerializeField] private float m_BreatheAmount = 0.04f;
        [SerializeField] private float m_BreatheDuration = 2.0f;

        [Header("🔄 Hafif Dönüş (Gentle Yaw Tilt)")]
        [SerializeField] private float m_TiltAngle = 3.5f;

        private Vector3 m_StartPos;
        private Vector3 m_StartScale;
        private Quaternion m_StartRot;

        private Tween m_MoveTween;
        private Tween m_ScaleTween;
        private Tween m_RotTween;

        private void Start()
        {
            m_StartPos = transform.localPosition;
            m_StartScale = transform.localScale;
            m_StartRot = transform.localRotation;

            // Rastgele faz gecikmesi
            float delay = Random.Range(0f, 1.2f);

            // Dikey süzülme
            m_MoveTween = transform.DOLocalMoveY(m_StartPos.y + m_FloatDistance, m_FloatDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(delay);

            // Nefes alma / esneme
            Vector3 targetScale = new Vector3(
                m_StartScale.x * (1f + m_BreatheAmount),
                m_StartScale.y * (1f - m_BreatheAmount * 0.5f),
                m_StartScale.z * (1f + m_BreatheAmount)
            );
            m_ScaleTween = transform.DOScale(targetScale, m_BreatheDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(delay * 0.8f);

            // Hafif sağa-sola tatlı yatma
            if (Mathf.Abs(m_TiltAngle) > 0.1f)
            {
                m_RotTween = transform.DOLocalRotate(new Vector3(0f, 0f, m_TiltAngle), m_FloatDuration * 1.3f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetDelay(delay * 0.5f);
            }
        }

        private void OnDestroy()
        {
            if (m_MoveTween != null && m_MoveTween.IsActive()) m_MoveTween.Kill();
            if (m_ScaleTween != null && m_ScaleTween.IsActive()) m_ScaleTween.Kill();
            if (m_RotTween != null && m_RotTween.IsActive()) m_RotTween.Kill();
        }
    }
}
