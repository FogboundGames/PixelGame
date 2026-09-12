using System;
using UnityEngine;

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

        public Color CargoColor
        {
            get => m_CargoColor;
            set
            {
                m_CargoColor = value;
                ApplyPaint();
            }
        }

        public int Capacity { get => m_Capacity; set => m_Capacity = Mathf.Max(1, value); }
        public int Load => m_Load;
        public bool IsFull => m_Load >= m_Capacity;
        public float FillRatio => m_Capacity > 0 ? (float)m_Load / m_Capacity : 0f;

        /// <summary>Kasa dolduğunda tetiklenir.</summary>
        public event Action<TruckCargo> Filled;

        private TruckTailgate m_Tailgate;

        private void Awake()
        {
            m_Tailgate = GetComponent<TruckTailgate>();
        }

        /// <summary>Kamyonu boş bir yük için hazırlar ve kapağını açar.</summary>
        public void ResetCargo(Color color, int capacity)
        {
            m_CargoColor = color;
            m_Capacity = Mathf.Max(1, capacity);
            m_Load = 0;

            ApplyPaint();
            OpenTailgate();
        }

        /// <summary>
        /// Kasaya bir küp ekler. Kasa dolduysa veya renk tutmuyorsa kabul etmez.
        /// </summary>
        public bool TryLoad(Color cubeColor, float threshold)
        {
            if (IsFull) return false;
            if (!Matches(cubeColor, threshold)) return false;

            m_Load++;

            if (IsFull)
            {
                CloseTailgate();
                Filled?.Invoke(this);
            }

            return true;
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
