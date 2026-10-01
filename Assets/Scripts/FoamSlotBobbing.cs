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
        [Range(0.001f, 0.05f)]
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

        public float PhaseOffset
        {
            get => m_PhaseOffset;
            set => m_PhaseOffset = value;
        }

        public void SetBasePosition(Vector3 basePos)
        {
            m_BasePosition = basePos;
        }

        private void OnEnable()
        {
            m_BasePosition = new Vector3(0f, 0.025f, 0f);
        }

        private void Update()
        {
            float t = Application.isPlaying ? Time.time : (float)Time.realtimeSinceStartup;

            // 1. Dikey su salınımı (Bobbing)
            float bobY = Mathf.Sin((t * m_BobSpeed) + m_PhaseOffset) * m_BobHeight;
            transform.localPosition = new Vector3(m_BasePosition.x, m_BasePosition.y + bobY, m_BasePosition.z);

            // 2. Hafif su dalgası eğim salınımı (Tilt)
            float tiltZ = Mathf.Cos((t * m_BobSpeed * 0.85f) + m_PhaseOffset) * m_TiltAngle;
            transform.localRotation = Quaternion.Euler(0f, 0f, tiltZ);

            // 3. Hafif nefes alma / genleşme (Scale Breathing)
            float breath = 1.0f + Mathf.Sin((t * m_BreathSpeed) + m_PhaseOffset) * m_ScaleAmount;
            transform.localScale = new Vector3(breath, breath, breath);
        }
    }
}
