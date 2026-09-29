using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Marina şamandıralarına ve zincirlerine su yüzeyinde hafif, gerçekçi bir dalga salınımı (bobbing) verir.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Marina Buoy Bobbing")]
    public class MarinaBuoyBobbing : MonoBehaviour
    {
        [Header("🌊 Salınım Ayarları")]
        [SerializeField] private float m_Frequency = 2.2f;
        [SerializeField] private float m_Height = 0.012f;
        [SerializeField] private float m_TiltAngle = 1.5f;
        [SerializeField] private float m_RandomOffset = 0f;

        private Vector3 m_BaseLocalPos;
        private Quaternion m_BaseLocalRot;
        private bool m_Initialized = false;

        private void Start()
        {
            InitializeBase();
        }

        private void InitializeBase()
        {
            if (m_Initialized) return;
            m_BaseLocalPos = transform.localPosition;
            m_BaseLocalRot = transform.localRotation;
            if (m_RandomOffset == 0f)
            {
                m_RandomOffset = transform.position.x * 2.1f + transform.position.z * 1.7f;
            }
            m_Initialized = true;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (!m_Initialized) InitializeBase();

            float time = Time.time * m_Frequency + m_RandomOffset;
            float dy = Mathf.Sin(time) * m_Height;
            float roll = Mathf.Cos(time * 0.85f) * m_TiltAngle;
            float pitch = Mathf.Sin(time * 0.70f) * (m_TiltAngle * 0.6f);

            transform.localPosition = m_BaseLocalPos + new Vector3(0f, dy, 0f);
            transform.localRotation = m_BaseLocalRot * Quaternion.Euler(pitch, 0f, roll);
        }
    }
}
