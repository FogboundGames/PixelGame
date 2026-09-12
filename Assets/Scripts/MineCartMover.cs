using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Maden vagonunu ray boyunca ilerletir ve tekerlekleri aldığı yola göre döndürür.
    /// Tekerlek dönüşü kat edilen mesafeden hesaplandığı için hız ne olursa olsun
    /// tekerlekler kaymadan döner (FBX'teki MineCart_Roll klibine ihtiyaç duymaz).
    ///
    /// Animator ile FBX'teki MineCart_Roll klibini oynatacaksanız "Tekerlekleri Döndür"
    /// seçeneğini kapatın, yoksa ikisi birbirini ezer.
    /// </summary>
    [DisallowMultipleComponent]
    public class MineCartMover : MonoBehaviour
    {
        [Header("Hareket")]
        [Tooltip("m/sn cinsinden hız. Vagon kendi ileri yönünde (+Z) ilerler.")]
        [SerializeField] private float m_Speed = 1.4f;

        [Tooltip("Bu mesafe kadar gidip geri döner. 0 = sınırsız ilerle.")]
        [SerializeField, Min(0f)] private float m_TravelDistance = 6f;

        [Tooltip("Sınıra gelince geri dönsün mü, yoksa dursun mu?")]
        [SerializeField] private bool m_PingPong = true;

        [SerializeField] private bool m_MoveOnStart = true;

        [Header("Tekerlekler")]
        [SerializeField] private bool m_SpinWheels = true;
        [SerializeField, Min(0.01f)] private float m_WheelRadius = 0.22f;

        [Tooltip("Boş bırakılırsa MineCart_Wheel_* isimli çocuk objeler otomatik bulunur.")]
        [SerializeField] private Transform[] m_Wheels;

        private Vector3 m_StartPosition;
        private float m_Travelled;
        private int m_Direction = 1;
        private bool m_Moving;

        public bool IsMoving => m_Moving;
        public float Speed { get => m_Speed; set => m_Speed = value; }

        private void Awake()
        {
            m_StartPosition = transform.position;
            if (m_Wheels == null || m_Wheels.Length == 0)
            {
                CollectWheels();
            }
            m_Moving = m_MoveOnStart;
        }

        public void StartMoving() => m_Moving = true;

        /// <summary>
        /// Kat edilen mesafeye göre tekerlekleri döndürür.
        /// Hareketi kendisi yönetmeyen çağırıcılar (örn. kalkış animasyonu) için.
        /// </summary>
        public void SpinWheelsByDistance(float worldDistance)
        {
            if (!m_SpinWheels || Mathf.Approximately(worldDistance, 0f)) return;

            if (m_Wheels == null || m_Wheels.Length == 0)
            {
                CollectWheels();
            }
            if (m_Wheels == null) return;

            float degrees = worldDistance / (2f * Mathf.PI * m_WheelRadius) * 360f;

            foreach (Transform wheel in m_Wheels)
            {
                if (wheel != null)
                {
                    wheel.RotateAround(wheel.position, transform.right, degrees);
                }
            }
        }

        public void StopMoving() => m_Moving = false;

        /// <summary>Vagonu başlangıç noktasına döndürür.</summary>
        public void ResetToStart()
        {
            transform.position = m_StartPosition;
            m_Travelled = 0f;
            m_Direction = 1;
        }

        private void Update()
        {
            if (!m_Moving || Mathf.Approximately(m_Speed, 0f))
            {
                return;
            }

            float step = m_Speed * m_Direction * Time.deltaTime;

            if (m_TravelDistance > 0f)
            {
                float next = m_Travelled + step;
                if (next > m_TravelDistance || next < 0f)
                {
                    // sınıra tam otur, sonra yön değiştir ya da dur
                    float clamped = Mathf.Clamp(next, 0f, m_TravelDistance);
                    step = clamped - m_Travelled;
                    m_Travelled = clamped;
                    Move(step);
                    if (m_PingPong)
                    {
                        m_Direction = -m_Direction;
                    }
                    else
                    {
                        m_Moving = false;
                    }
                    return;
                }
                m_Travelled = next;
            }

            Move(step);
        }

        private void Move(float step)
        {
            transform.position += transform.forward * step;

            if (!m_SpinWheels || m_Wheels == null)
            {
                return;
            }

            float degrees = step / (2f * Mathf.PI * m_WheelRadius) * 360f;
            foreach (Transform wheel in m_Wheels)
            {
                if (wheel != null)
                {
                    // dünya eksenine göre döndürülür, böylece FBX eksen dönüşümünden etkilenmez
                    wheel.RotateAround(wheel.position, transform.right, degrees);
                }
            }
        }

        private void CollectWheels()
        {
            var found = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name.StartsWith("MineCart_Wheel") || child.name.StartsWith("Truck_Wheel"))
                {
                    found.Add(child);
                }
            }
            m_Wheels = found.ToArray();

            if (m_Wheels.Length == 0)
            {
                Debug.LogWarning($"[MineCartMover] '{name}' altında tekerlek objesi bulunamadı.", this);
            }
        }
    }
}
