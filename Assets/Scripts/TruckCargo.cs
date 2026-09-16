using System;
using UnityEngine;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// Bir kamyonun taşıdığı yükü temsil eder: rengi, kapasitesi ve o an kaç küp aldığı.
    /// Kasa dolduğunda arka kapağını kapatıp kalkmaya hazır olduğunu bildirir.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Truck Cargo")]
    public class TruckCargo : MonoBehaviour
    {
        [Header("📦 Yük")]
        [Tooltip("Bu kamyonun topladığı küp rengi")]
        [SerializeField] private Color m_CargoColor = Color.red;

        [Tooltip("Kasaya kaç küp sığar")]
        [Min(1)]
        [SerializeField] private int m_Capacity = 8;

        [SerializeField] private int m_Load;

        [Tooltip("Bir küpün kaça bölündüğünün ortalaması (12 parçalı kırılma modeli için 12). Kasadaki parça boyutu bu sayıya göre hesaplanır.")]
        [Min(1)]
        [SerializeField] private int m_PiecesPerCube = 12;

        public Color CargoColor
        {
            get => m_CargoColor;
            set
            {
                m_CargoColor = value;
                ApplyPaint();
            }
        }

        public int Capacity { get => m_Capacity; set { m_Capacity = Mathf.Max(1, value); UpdateBadge(false); } }
        public int Load => m_Load;
        public int RemainingCapacity => Mathf.Max(0, m_Capacity - m_Load);
        public bool IsFull => m_Load >= m_Capacity;
        public float FillRatio => m_Capacity > 0 ? (float)m_Load / m_Capacity : 0f;

        /// <summary>Kasa dolduğunda tetiklenir.</summary>
        public event Action<TruckCargo> Filled;

        private TruckTailgate m_Tailgate;
        private CargoStack m_Stack;
        private WagonCapacityBadge m_Badge;

        /// <summary>Yolda olan (henüz kasaya varmamış) küp sayısı.</summary>
        private int m_InFlight;

        /// <summary>Kasa dolu ve yolda parça kalmadı mı? Kalkış bunu bekler.</summary>
        public bool IsSettled => IsFull && m_InFlight == 0;

        private void Awake()
        {
            m_Tailgate = GetComponent<TruckTailgate>();
            EnsureStack();
            EnsureBadge();
        }

        private void EnsureStack()
        {
            if (m_Stack != null) return;

            m_Stack = GetComponent<CargoStack>();
            if (m_Stack == null) m_Stack = gameObject.AddComponent<CargoStack>();
        }

        public void EnsureBadge()
        {
            if (m_Badge != null) return;
            m_Badge = GetComponent<WagonCapacityBadge>();
            if (m_Badge == null) m_Badge = gameObject.AddComponent<WagonCapacityBadge>();
        }

        public void UpdateBadge(bool punch = true)
        {
            EnsureBadge();
            if (m_Badge != null)
            {
                m_Badge.SetCount(RemainingCapacity, punch);
            }
        }

        /// <summary>Kamyonu boş bir yük için hazırlar ve kapağını açar.</summary>
        public void ResetCargo(Color color, int capacity)
        {
            m_CargoColor = color;
            m_Capacity = Mathf.Max(1, capacity);
            m_Load = 0;
            m_InFlight = 0;

            ApplyPaint();
            OpenTailgate();

            EnsureStack();
            UpdateBadge(false);

            // Kasaya toplam kaç parça düşecek: her küp birkaç parçaya bölünüyor.
            // Parça boyutu buna göre hesaplanır ki kasa dolsun ama taşmasın.
            if (m_Stack != null) m_Stack.Setup(m_Capacity * m_PiecesPerCube, color);
        }

        /// <summary>
        /// Bir piksel küpünün tüm parçalarını tek bir birim olarak kasaya kabul eder.
        /// Kalan kapasite 1 azalır ve rozet güncellenir.
        /// </summary>
        public bool LoadOneCube()
        {
            if (IsFull) return false;

            m_Load++;
            UpdateBadge(true);

            if (IsFull)
            {
                CloseTailgate();
                Filled?.Invoke(this);
                PlayCompletionFeedback();
            }

            return true;
        }

        private void PlayCompletionFeedback()
        {
            if (m_Badge != null)
            {
                m_Badge.PlayCompletionAnimation();
            }

            // Vagon gövdesine minik tatlı bir yaylanma animasyonu (hareket eden kök pozisyonu sarsmaz)
            Transform body = transform.Find("MineCart_Body");
            if (body == null) body = transform.Find("Truck_Cargo");
            Transform targetAnim = body != null ? body : transform;

            targetAnim.DOKill(true);
            targetAnim.DOPunchScale(new Vector3(0.08f, 0.14f, 0.08f), 0.35f, 5, 0.4f);
        }

        /// <summary>
        /// Kasaya bir küp ekler. Kasa dolduysa veya renk tutmuyorsa kabul etmez.
        /// </summary>
        public bool TryLoad(Color cubeColor, float threshold, int pieces = 1)
        {
            if (IsFull) return false;
            if (!Matches(cubeColor, threshold)) return false;

            // Sayaç hemen artar: kapasitenin aşılmasını engeller.
            // Görsel dolum ise parçalar kasaya vardıkça tamamlanır.
            m_Load++;
            m_InFlight += Mathf.Max(1, pieces);

            return true;
        }

        public int PiecesPerCube => Mathf.Max(1, m_PiecesPerCube);

        /// <summary>Kasadaki yığın (parça boyutunu okumak için).</summary>
        public CargoStack Stack
        {
            get { EnsureStack(); return m_Stack; }
        }

        /// <summary>
        /// Yoldaki bir parça kasaya vardığında çağrılır: yığına yerleşir ve
        /// kasa dolup yolda parça kalmadıysa kalkış bildirilir.
        /// </summary>
        public void OnPieceArrived(float sizeFactor)
        {
            EnsureStack();
            if (m_Stack != null) m_Stack.AddPiece(sizeFactor);

            m_InFlight = Mathf.Max(0, m_InFlight - 1);

            // Kalkış, yoldaki son parça da varana kadar beklemeli;
            // yoksa vagon kalkar ve kalan parçalar boşluğa uçar
            if (IsSettled)
            {
                CloseTailgate();
                Filled?.Invoke(this);
            }
        }

        /// <summary>Küpün rengi bu kamyonun yüküne uyuyor mu?</summary>
        public bool Matches(Color cubeColor, float threshold)
        {
            return ColorDistance(cubeColor, m_CargoColor) <= threshold;
        }

        /// <summary>İki renk arasındaki basit RGB uzaklığı (0 = aynı).</summary>
        public static float ColorDistance(Color a, Color b)
        {
            return (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b)) / 3f;
        }

        private void ApplyPaint()
        {
            TruckPaint paint = GetComponent<TruckPaint>();
            if (paint == null) return;

            paint.SetBodyColor(m_CargoColor);
            paint.Apply();
        }

        private void OpenTailgate()
        {
            if (m_Tailgate == null) m_Tailgate = GetComponent<TruckTailgate>();
            if (m_Tailgate == null) return;

            if (Application.isPlaying) m_Tailgate.Open();
            else m_Tailgate.SetOpenImmediate(true);
        }

        private void CloseTailgate()
        {
            if (m_Tailgate == null) m_Tailgate = GetComponent<TruckTailgate>();
            if (m_Tailgate == null) return;

            if (Application.isPlaying) m_Tailgate.Close();
            else m_Tailgate.SetOpenImmediate(false);
        }
    }
}
