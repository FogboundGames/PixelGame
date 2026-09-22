using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// Su üzerinde bekleyen gemi kuyruğunu (sırasını) yönetir.
    /// Renkleri seviyedeki piksel sanatına göre belirler.
    /// Ön sıradaki gemilere tıklandığında boş slotlara yanaştırır ve arkadaki gemileri öne kaydırır.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Ship Queue Pool")]
    public class ShipQueuePool : MonoBehaviour
    {
        [Header("🚢 Gemi Prefab & Ayarlar")]
        [SerializeField] private GameObject m_ShipPrefab;
        [SerializeField] private int m_Columns = 4;
        [SerializeField] private int m_Rows = 2;
        [SerializeField] private Vector2 m_Spacing = new Vector2(1.5f, 1.4f);
        [SerializeField] private float m_ShipScale = 0.126f;

        [Header("📍 Kuyruk Yerleri")]
        [SerializeField] private List<Transform> m_QueueSpots = new List<Transform>();
        [SerializeField] private List<ShipController> m_WaitingShips = new List<ShipController>();

        public int Capacity => m_Columns * m_Rows;
        public List<ShipController> WaitingShips => m_WaitingShips;

        private void Awake()
        {
            EnsureSpots();
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                InitializeQueue();
            }
        }

        /// <summary>
        /// Kuyruk için bekleme noktalarını (spot) oluşturur.
        /// </summary>
        public void EnsureSpots()
        {
            m_QueueSpots.Clear();
            int total = m_Columns * m_Rows;

            float startX = -(m_Columns - 1) * m_Spacing.x * 0.5f;

            for (int r = 0; r < m_Rows; r++)
            {
                for (int c = 0; c < m_Columns; c++)
                {
                    int index = r * m_Columns + c;
                    string spotName = $"Spot_R{r}_C{c}";
                    Transform spotTr = transform.Find(spotName);
                    if (spotTr == null)
                    {
                        GameObject spotObj = new GameObject(spotName);
                        spotTr = spotObj.transform;
                        spotTr.SetParent(transform, false);
                    }

                    float posX = startX + c * m_Spacing.x;
                    float posY = -r * m_Spacing.y;

                    spotTr.localPosition = new Vector3(posX, 0f, posY * 0.4f);
                    spotTr.localRotation = Quaternion.identity;
                    spotTr.localScale = Vector3.one;

                    m_QueueSpots.Add(spotTr);
                }
            }
        }

        /// <summary>
        /// Seviye başlangıcında kuyruğu renkli gemilerle doldurur.
        /// </summary>
        public void InitializeQueue()
        {
            ClearQueue();
            EnsureSpots();

            for (int i = 0; i < m_QueueSpots.Count; i++)
            {
                SpawnShipAtSpot(i);
            }
        }

        public void ClearQueue()
        {
            for (int i = m_WaitingShips.Count - 1; i >= 0; i--)
            {
                if (m_WaitingShips[i] != null)
                {
                    Destroy(m_WaitingShips[i].gameObject);
                }
            }
            m_WaitingShips.Clear();
        }

        /// <summary>
        /// Belirtilen spot indeksinde yeni bir gemi üretir.
        /// </summary>
        public ShipController SpawnShipAtSpot(int spotIndex)
        {
            if (spotIndex < 0 || spotIndex >= m_QueueSpots.Count) return null;
            if (m_ShipPrefab == null) return null;

            Transform spot = m_QueueSpots[spotIndex];

            GameObject shipObj = Instantiate(m_ShipPrefab, spot.position, spot.rotation, spot);
            shipObj.name = $"Waiting_Ship_{spotIndex}";
            shipObj.transform.localPosition = Vector3.zero;
            shipObj.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            shipObj.transform.localScale = Vector3.one * m_ShipScale;

            ShipController ship = shipObj.GetComponent<ShipController>();
            if (ship == null) ship = shipObj.AddComponent<ShipController>();

            // Renk ve kapasite belirle
            Color shipColor = GetNextNeededColor();
            int capacity = GetRecommendedCapacity();
            ship.Configure(shipColor, capacity);

            // Listeyi genişlet
            while (m_WaitingShips.Count <= spotIndex)
            {
                m_WaitingShips.Add(null);
            }
            m_WaitingShips[spotIndex] = ship;

            return ship;
        }

        /// <summary>
        /// Kuyruktaki bir geminin en ön sırada (Row 0) olup olmadığını kontrol eder.
        /// </summary>
        public bool IsFrontRow(ShipController ship)
        {
            int index = m_WaitingShips.IndexOf(ship);
            if (index < 0) return false;
            int rowIndex = index / m_Columns;
            return rowIndex == 0;
        }

        /// <summary>
        /// Gemiyi kuyruktan çıkarır ve arkadaki gemileri öne kaydırır.
        /// </summary>
        public void RemoveShipFromQueue(ShipController ship)
        {
            int index = m_WaitingShips.IndexOf(ship);
            if (index >= 0)
            {
                m_WaitingShips[index] = null;
                CompactAndRefill();
            }
        }

        /// <summary>
        /// Boşalan yerleri arkadan öne doğru kaydırarak doldurur ve en arkaya yeni gemiler ekler.
        /// </summary>
        public void CompactAndRefill()
        {
            // 1. Kaydırma (Compact)
            for (int i = 0; i < m_QueueSpots.Count - 1; i++)
            {
                if (m_WaitingShips[i] == null)
                {
                    // Arkadaki ilk dolu gemiyi bul
                    for (int j = i + 1; j < m_QueueSpots.Count; j++)
                    {
                        if (m_WaitingShips[j] != null)
                        {
                            ShipController movingShip = m_WaitingShips[j];
                            m_WaitingShips[i] = movingShip;
                            m_WaitingShips[j] = null;

                            // Yeni spotuna yumuşakça kaydır
                            Transform newSpot = m_QueueSpots[i];
                            movingShip.transform.SetParent(newSpot, true);
                            movingShip.transform.DOLocalMove(Vector3.zero, 0.35f).SetEase(Ease.OutQuad);
                            break;
                        }
                    }
                }
            }

            // 2. Arkadaki boş yerleri yeni gemilerle doldur (Refill)
            for (int i = 0; i < m_QueueSpots.Count; i++)
            {
                if (i >= m_WaitingShips.Count || m_WaitingShips[i] == null)
                {
                    SpawnShipAtSpot(i);
                }
            }
        }

        private Color GetNextNeededColor()
        {
            if (ShipDispatcher.Instance != null)
            {
                Color color = ShipDispatcher.Instance.GetRemainingLevelColor();
                if (color != Color.clear) return color;
            }

            // Varsayılan canlı casual renkler
            Color[] defaults = new Color[]
            {
                new Color(0.18f, 0.52f, 0.95f, 1f), // Canlı Mavi
                new Color(0.95f, 0.28f, 0.25f, 1f), // Canlı Kırmızı
                new Color(0.22f, 0.82f, 0.42f, 1f), // Canlı Yeşil
                new Color(0.98f, 0.78f, 0.15f, 1f), // Canlı Sarı
                new Color(0.95f, 0.52f, 0.12f, 1f), // Canlı Turuncu
                new Color(0.68f, 0.32f, 0.92f, 1f)  // Canlı Mor
            };
            return defaults[UnityEngine.Random.Range(0, defaults.Length)];
        }

        private int GetRecommendedCapacity()
        {
            return UnityEngine.Random.Range(10, 21);
        }
    }
}
