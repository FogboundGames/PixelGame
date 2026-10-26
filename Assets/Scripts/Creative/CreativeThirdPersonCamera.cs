using UnityEngine;
using UnityEngine.InputSystem;

namespace PixelGame.Creative
{
    /// <summary>
    /// ASMR Kreatif videosu için takip kamerası.
    /// Hem oyunun orijinal açısını (İzometrik -20° tilt) hem de sinematik 3rd person perspektif takibini destekler.
    /// Karakteri ve 30 katlı küp kulesini mobil ekranda (9:16) DAİMA tam merkezde tutar.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [ExecuteAlways]
    public class CreativeThirdPersonCamera : MonoBehaviour
    {
        public enum CameraMode
        {
            [InspectorName("🎬 Oyunun Orijinal Açısı (Ortalanmış İzometrik - En Doğal & ASMR İçin İdeal)")]
            GameIsometricFollow = 0,

            [InspectorName("🎥 Sinematik 3rd Person Perspektif (Gemicinin Arkasından Takip)")]
            PerspectiveThirdPerson = 1,

            [InspectorName("🔍 Yakın Çekim Kule Takibi (Küp Alış ve Yığına Odaklı)")]
            CloseUpFocus = 2
        }

        [Header("🎯 Takip Hedefi")]
        [SerializeField] private Transform m_Target;

        [Header("📐 Kamera Modu")]
        [SerializeField] private CameraMode m_Mode = CameraMode.GameIsometricFollow;

        [Header("🎬 Mod 0: Oyunun Kendi Açısı Ayarları (GameIsometricFollow)")]
        [Tooltip("Oyun sahnesiyle birebir aynı açı (-20° eğim)")]
        [SerializeField] private float m_IsoTiltAngle = -20f;
        [SerializeField] private float m_IsoOrthoSize = 3.8f;
        [Tooltip("Gemicinin ekrandaki dikey konumu (Kulenin yukarı uzaması için gemiciyi hafif alt-ortaya merkezler)")]
        [SerializeField] private float m_IsoTargetOffsetY = 0.5f;
        [SerializeField] private float m_IsoCameraDistanceZ = -19.425f;

        [Header("🎥 Mod 1: 3rd Person Perspektif Ayarları")]
        [SerializeField] private float m_PerspFov = 48f;
        [SerializeField] private Vector3 m_PerspOffset = new Vector3(0f, -2.5f, -5.2f);
        [SerializeField] private Vector3 m_PerspEuler = new Vector3(336f, 0f, 0f);

        [Header("🔍 Mod 2: Yakın Çekim Ayarları")]
        [SerializeField] private float m_CloseUpFov = 42f;
        [SerializeField] private Vector3 m_CloseUpOffset = new Vector3(0f, -2.6f, -4.8f);
        [SerializeField] private Vector3 m_CloseUpEuler = new Vector3(338f, 0f, 0f);

        [Header("✨ Pürüzsüzleştirme (Damping)")]
        [Range(0.01f, 0.5f)] [SerializeField] private float m_PositionSmoothTime = 0.12f;
        [Range(0.01f, 0.5f)] [SerializeField] private float m_RotationSmoothTime = 0.15f;

        private Camera m_Camera;
        private Vector3 m_PosVelocity;
        private bool m_HasSnapped = false;

        public Transform Target
        {
            get => m_Target;
            set
            {
                m_Target = value;
                m_HasSnapped = false;
            }
        }

        public CameraMode Mode
        {
            get => m_Mode;
            set
            {
                m_Mode = value;
                ApplyCameraSettings();
            }
        }

        private void Awake()
        {
            m_Camera = GetComponent<Camera>();
            ApplyCameraSettings();
        }

        private void OnEnable()
        {
            m_Camera = GetComponent<Camera>();
            ApplyCameraSettings();
            m_HasSnapped = false;
        }

        public void ApplyCameraSettings()
        {
            if (m_Camera == null) m_Camera = GetComponent<Camera>();
            if (m_Camera == null) return;

            switch (m_Mode)
            {
                case CameraMode.GameIsometricFollow:
                    m_Camera.orthographic = true;
                    m_Camera.orthographicSize = m_IsoOrthoSize;
                    m_Camera.nearClipPlane = 0.3f;
                    m_Camera.farClipPlane = 120f;
                    break;

                case CameraMode.PerspectiveThirdPerson:
                    m_Camera.orthographic = false;
                    m_Camera.fieldOfView = m_PerspFov;
                    m_Camera.nearClipPlane = 0.3f;
                    m_Camera.farClipPlane = 100f;
                    break;

                case CameraMode.CloseUpFocus:
                    m_Camera.orthographic = false;
                    m_Camera.fieldOfView = m_CloseUpFov;
                    m_Camera.nearClipPlane = 0.3f;
                    m_Camera.farClipPlane = 100f;
                    break;
            }
        }

        private void LateUpdate()
        {
            // Yeni Input Sistemi ile güvenli mod değiştirme (Hata fırlatmaz)
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) Mode = CameraMode.GameIsometricFollow;
                if (kb.digit2Key.wasPressedThisFrame) Mode = CameraMode.PerspectiveThirdPerson;
                if (kb.digit3Key.wasPressedThisFrame) Mode = CameraMode.CloseUpFocus;
            }

            if (m_Target == null) return;

            Vector3 targetPos = m_Target.position;
            Vector3 desiredPos;
            Quaternion desiredRot;

            switch (m_Mode)
            {
                case CameraMode.GameIsometricFollow:
                default:
                    // Oyunun orijinal -20° eğimi:
                    // Z eksenindeki mesafeye göre Y projeksiyon düzeltmesi yapılarak gemici ekranda mükemmel ortalanır
                    float groundZ = 0.42f;
                    float deltaZ = groundZ - m_IsoCameraDistanceZ; // ~19.845
                    float tanTilt = Mathf.Tan(Mathf.Abs(m_IsoTiltAngle) * Mathf.Deg2Rad); // tan(20°) ≈ 0.364
                    float camY = (targetPos.y + m_IsoTargetOffsetY) - deltaZ * tanTilt;

                    desiredPos = new Vector3(targetPos.x, camY, m_IsoCameraDistanceZ);
                    desiredRot = Quaternion.Euler(360f + m_IsoTiltAngle, 0f, 0f); // 340° = -20°
                    break;

                case CameraMode.PerspectiveThirdPerson:
                    desiredPos = targetPos + m_PerspOffset;
                    desiredRot = Quaternion.Euler(m_PerspEuler);
                    break;

                case CameraMode.CloseUpFocus:
                    desiredPos = targetPos + m_CloseUpOffset;
                    desiredRot = Quaternion.Euler(m_CloseUpEuler);
                    break;
            }

            if (!m_HasSnapped)
            {
                transform.position = desiredPos;
                transform.rotation = desiredRot;
                m_PosVelocity = Vector3.zero;
                m_HasSnapped = true;
                return;
            }

            // Pürüzsüz takip
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref m_PosVelocity, m_PositionSmoothTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, Time.deltaTime / Mathf.Max(0.01f, m_RotationSmoothTime));
        }

        public void SnapToTarget()
        {
            m_HasSnapped = false;
        }
    }
}
