using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Oyuncak kamyonun arka kapağını menteşesi etrafında açıp kapatır (Animator gerekmez).
    /// Menteşe ekseni arka tekerleklerden, açılma yönü kamyon gövdesinden otomatik hesaplanır;
    /// bu yüzden FBX import ayarlarındaki eksen dönüşümünden etkilenmez.
    /// Hareketin sonunda oyuncak hissi veren küçük bir sekme vardır.
    /// </summary>
    [DisallowMultipleComponent]
    public class TruckTailgate : MonoBehaviour
    {
        [Header("Parçalar (boş bırakılırsa isimden bulunur)")]
        [SerializeField] private Transform m_Tailgate;
        [SerializeField] private Transform m_LeftRearWheel;
        [SerializeField] private Transform m_RightRearWheel;

        [Header("Hareket")]
        [SerializeField] private float m_OpenAngle = 90f;
        [SerializeField, Min(0.01f)] private float m_OpenDuration = 0.7f;
        [SerializeField, Min(0.01f)] private float m_CloseDuration = 0.5f;
        [SerializeField] private bool m_StartOpen;

        [Tooltip("0 = kapalı, 1 = tam açık. 1'i aşan değerler sekmeyi oluşturur.")]
        [SerializeField] private AnimationCurve m_OpenCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.55f, 98f / 90f),
            new Keyframe(0.75f, 86f / 90f),
            new Keyframe(0.9f, 91f / 90f),
            new Keyframe(1f, 1f));

        [Tooltip("1 = tam açık, 0 = kapalı. 0'ın altına inen değerler sekmeyi oluşturur.")]
        [SerializeField] private AnimationCurve m_CloseCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.67f, -4f / 90f),
            new Keyframe(0.87f, 2f / 90f),
            new Keyframe(1f, 0f));

        private Quaternion m_RestRotation;
        private Vector3 m_AxisInParent;
        private float m_Sign = 1f;
        private float m_Current;
        private float m_From;
        private float m_Timer;
        private float m_Duration;
        private AnimationCurve m_ActiveCurve;
        private bool m_IsOpen;
        private bool m_Initialized;

        public bool IsOpen => m_IsOpen;
        public bool IsMoving => m_ActiveCurve != null;

        private void Awake()
        {
            if (Initialize())
            {
                SetOpenImmediate(m_StartOpen);
            }
        }

        private void Update()
        {
            if (m_ActiveCurve == null)
            {
                return;
            }

            m_Timer += Time.deltaTime;
            float t = Mathf.Clamp01(m_Timer / m_Duration);
            float curve = m_ActiveCurve.Evaluate(t);
            // Hareket yarıda kesilirse kapak bulunduğu açıdan devam eder.
            float value = m_IsOpen ? m_From + curve * (1f - m_From) : curve * m_From;
            ApplyAngle(value);

            if (t >= 1f)
            {
                m_ActiveCurve = null;
                ApplyAngle(m_IsOpen ? 1f : 0f);
            }
        }

        public void Open() => Play(true);

        public void Close() => Play(false);

        public void Toggle() => Play(!m_IsOpen);

        public void SetOpenImmediate(bool open)
        {
            if (!Initialize())
            {
                return;
            }
            m_ActiveCurve = null;
            m_IsOpen = open;
            ApplyAngle(open ? 1f : 0f);
        }

        private void Play(bool open)
        {
            if (!Initialize() || (open == m_IsOpen && m_ActiveCurve == null))
            {
                return;
            }

            m_IsOpen = open;
            m_From = m_Current;
            m_ActiveCurve = open ? m_OpenCurve : m_CloseCurve;
            m_Duration = Mathf.Max(0.01f, open ? m_OpenDuration : m_CloseDuration);
            m_Timer = 0f;
        }

        private void ApplyAngle(float normalized)
        {
            m_Current = normalized;
            m_Tailgate.localRotation = Quaternion.AngleAxis(m_Sign * m_OpenAngle * normalized, m_AxisInParent) * m_RestRotation;
        }

        private bool Initialize()
        {
            if (m_Initialized)
            {
                return true;
            }

            if (m_Tailgate == null) m_Tailgate = FindChild(transform, "Truck_Tailgate");
            if (m_LeftRearWheel == null) m_LeftRearWheel = FindChild(transform, "Truck_Wheel_RL");
            if (m_RightRearWheel == null) m_RightRearWheel = FindChild(transform, "Truck_Wheel_RR");

            if (m_Tailgate == null || m_Tailgate.parent == null || m_LeftRearWheel == null || m_RightRearWheel == null)
            {
                Debug.LogWarning($"[TruckTailgate] '{name}' altında Truck_Tailgate / Truck_Wheel_RL / Truck_Wheel_RR bulunamadı.", this);
                return false;
            }

            m_RestRotation = m_Tailgate.localRotation;

            // Menteşe ekseni: sol arka tekerlekten sağ arka tekerleğe.
            Vector3 axisWorld = (m_RightRearWheel.position - m_LeftRearWheel.position).normalized;
            m_AxisInParent = m_Tailgate.parent.InverseTransformDirection(axisWorld).normalized;

            // Açılma yönü: kapağı gövdeden uzaklaştıran yön.
            Vector3 pivot = m_Tailgate.position;
            Renderer gateRenderer = m_Tailgate.GetComponent<Renderer>();
            Renderer bodyRenderer = m_Tailgate.parent.GetComponent<Renderer>();
            Vector3 gateCenter = gateRenderer != null ? gateRenderer.bounds.center : pivot + m_Tailgate.parent.up;
            Vector3 bodyCenter = bodyRenderer != null ? bodyRenderer.bounds.center : transform.position;
            Vector3 offset = gateCenter - pivot;
            Vector3 positive = pivot + Quaternion.AngleAxis(90f, axisWorld) * offset;
            Vector3 negative = pivot + Quaternion.AngleAxis(-90f, axisWorld) * offset;
            m_Sign = (positive - bodyCenter).sqrMagnitude >= (negative - bodyCenter).sqrMagnitude ? 1f : -1f;

            m_Initialized = true;
            return true;
        }

        private static Transform FindChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                {
                    return child;
                }
                Transform found = FindChild(child, childName);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }
    }
}
