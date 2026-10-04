using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Köpük slotların (FoamSlot) su yüzeyinde doğal bir şekilde yüzmesini,
    /// hafif dalga salınımı (bobbing) yapmasını ve su dalgalarıyla senkronize nefes almasını sağlar.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Foam Slot Bobbing")]
    public class FoamSlotBobbing : MonoBehaviour
    {
        [Header("🌊 Yüzme & Salınım Ayarları")]
        [Tooltip("Su yüzeyinde dikey salınım hızı.")]
        [Range(0.5f, 5.0f)]
        [SerializeField] private float m_BobSpeed = 1.35f;

        [Tooltip("Dikey salınım miktarı (Y ekseni).")]
        [Range(0f, 0.05f)]
        [SerializeField] private float m_BobHeight = 0.008f;

        [Tooltip("Hafif su dalgası yana yatma açısı (Z ekseni tilt).")]
        [Range(0.0f, 4.0f)]
        [SerializeField] private float m_TiltAngle = 0.8f;

        [Header("🫁 Köpük Nefes Alma (Scale Breath)")]
        [Tooltip("Köpüğün hafifçe daralıp genişleme hızı.")]
        [Range(0.5f, 4.0f)]
        [SerializeField] private float m_BreathSpeed = 1.1f;

        [Tooltip("Köpüğün nefes alma ölçek genliği.")]
        [Range(0.0f, 0.08f)]
        [SerializeField] private float m_ScaleAmount = 0.015f;

        [Header("📐 Faz Kayması")]
        [Tooltip("Slotlar arasında dalganın soldan sağa akması için faz farkı.")]
        [SerializeField] private float m_PhaseOffset = 0f;

        private Vector3 m_BasePosition = new Vector3(0f, 0.025f, 0f);
        private Vector3 m_BaseScale = Vector3.one;

        [Header("💦 Suya Batma Animasyonu (Water Dip Impact)")]
        [Tooltip("Gemi yanaşınca görselin suya batıp yaylanması. İskele gibi sabit/ortak kollu görsellerde kapalı olmalı; " +
                 "yoksa tek bir cep şişip komşu iskelelerle ortak kolları kayar.")]
        [SerializeField] private bool m_EnableDipImpact = true;
        private float m_DipOffsetY = 0f;
        private Vector3 m_DipScaleOffset = Vector3.zero;
        private Coroutine m_DipCoroutine;

        public float PhaseOffset
        {
            get => m_PhaseOffset;
            set => m_PhaseOffset = value;
        }

        public void SetBasePosition(Vector3 basePos)
        {
            m_BasePosition = basePos;
        }

        public void SetBaseScale(Vector3 baseScale)
        {
            m_BaseScale = baseScale;
        }

        private void OnEnable()
        {
            m_BasePosition = new Vector3(0f, 0.025f, 0f);
            m_DipOffsetY = 0f;
            m_DipScaleOffset = Vector3.zero;
        }

        private void OnDisable()
        {
            if (m_DipCoroutine != null)
            {
                StopCoroutine(m_DipCoroutine);
                m_DipCoroutine = null;
            }
            m_DipOffsetY = 0f;
            m_DipScaleOffset = Vector3.zero;
        }

        /// <summary>
        /// Gemi yanaştığında can simidinin ve suyun hafifçe batıp yaylanmasını sağlar.
        /// </summary>
        public void TriggerWaterDipImpact(float depth = 0.16f, float duration = 0.52f)
        {
            if (!isActiveAndEnabled || !m_EnableDipImpact) return;
            if (m_DipCoroutine != null)
            {
                StopCoroutine(m_DipCoroutine);
            }
            m_DipCoroutine = StartCoroutine(WaterDipRoutine(depth, duration));
        }

        private System.Collections.IEnumerator WaterDipRoutine(float depth, float duration)
        {
            // 1. Faz: Suya ani dalış / batma (Plunge) - Hızlı ve tok darbe
            float plungeTime = duration * 0.28f;
            float elapsed = 0f;
            while (elapsed < plungeTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / plungeTime);
                float easeOut = Mathf.Sin(t * Mathf.PI * 0.5f);
                m_DipOffsetY = Mathf.Lerp(0f, -depth, easeOut);
                // Basınçla hafif yatay genişleme (hydrodynamic squash & stretch)
                m_DipScaleOffset = new Vector3(0.12f * easeOut, -0.08f * easeOut, 0.12f * easeOut);
                yield return null;
            }

            // 2. Faz: Suyun kaldırma kuvvetiyle yukarı geri fırlama / yaylanma (Rebound)
            float reboundTime = duration * 0.36f;
            elapsed = 0f;
            float reboundHeight = depth * 0.35f;
            while (elapsed < reboundTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / reboundTime);
                float ease = Mathf.Sin(t * Mathf.PI * 0.5f);
                m_DipOffsetY = Mathf.Lerp(-depth, reboundHeight, ease);
                m_DipScaleOffset = Vector3.Lerp(new Vector3(0.12f, -0.08f, 0.12f), new Vector3(-0.04f, 0.05f, -0.04f), ease);
                yield return null;
            }

            // 3. Faz: Durgunlaşma ve normal su seviyesine sönümlü oturma (Settle)
            float settleTime = duration * 0.36f;
            elapsed = 0f;
            while (elapsed < settleTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / settleTime);
                float smooth = Mathf.SmoothStep(0f, 1f, t);
                m_DipOffsetY = Mathf.Lerp(reboundHeight, 0f, smooth);
                m_DipScaleOffset = Vector3.Lerp(new Vector3(-0.04f, 0.05f, -0.04f), Vector3.zero, smooth);
                yield return null;
            }

            m_DipOffsetY = 0f;
            m_DipScaleOffset = Vector3.zero;
            m_DipCoroutine = null;
        }

        private void Update()
        {
            // Editörde sallanma yok: her yenilemede transform değişip sahne sürekli "değişti" (*)
            // olarak işaretleniyordu. Dinlenme duruşunu MarinaSlotLayout zaten kuruyor.
            if (!Application.isPlaying) return;

            float t = Time.time;

            // 1. Dikey su salınımı (Bobbing) + Suya batma etkisi (Dip)
            float bobY = Mathf.Sin((t * m_BobSpeed) + m_PhaseOffset) * m_BobHeight;
            transform.localPosition = new Vector3(m_BasePosition.x, m_BasePosition.y + bobY + m_DipOffsetY, m_BasePosition.z);

            // 2. Hafif su dalgası eğim salınımı (Tilt)
            float tiltZ = Mathf.Cos((t * m_BobSpeed * 0.85f) + m_PhaseOffset) * m_TiltAngle;
            transform.localRotation = Quaternion.Euler(0f, 0f, tiltZ);

            // 3. Hafif nefes alma / genleşme (Scale Breathing) + Suya batma basınç tepkisi
            float breath = 1.0f + Mathf.Sin((t * m_BreathSpeed) + m_PhaseOffset) * m_ScaleAmount;
            transform.localScale = new Vector3(
                (m_BaseScale.x * breath) + m_DipScaleOffset.x,
                (m_BaseScale.y * breath) + m_DipScaleOffset.y,
                (m_BaseScale.z * breath) + m_DipScaleOffset.z
            );
        }
    }
}
