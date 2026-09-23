using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// Su üzerinde bekleyen gemi kuyruğunu (sırasını) yönetir.
    /// 2 sıra x 4 sütun halinde ferah ve düzenli bir filo yerleşimi sağlar.
    /// Ön sıradaki gemi ayrıldığında, aynı kulvardaki (sütundaki) arka gemi öne kayar ve arkaya denizden yeni gemi gelir.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Ship Queue Pool")]
    public class ShipQueuePool : MonoBehaviour
    {
        [Header("🚢 Gemi Prefab & Ayarlar")]
        [SerializeField] private GameObject m_ShipPrefab;
        [SerializeField] private int m_Columns = 4;
        [SerializeField] private int m_Rows = 2;
        [SerializeField] private Vector2 m_Spacing = new Vector2(1.28f, 1.35f);
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
        /// Kuyruk için bekleme noktalarını (spot) bulur veya gerekirse oluşturur.
        /// Sahnede önceden ayarlanmış spot konumlarını KESİNLİKLE korur.
        /// </summary>
        public void EnsureSpots()
        {
            // 1. Önce sahnede halihazırda var olan çocuk spotları topla
            m_QueueSpots.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform cTr = transform.GetChild(i);
                if (cTr.name.StartsWith("Spot_"))
                {
                    m_QueueSpots.Add(cTr);
                }
            }

            // Sahnede zaten yeterli sayıda spot varsa mevcut konumlarını koru ve çık
            if (m_QueueSpots.Count >= Capacity)
            {
                return;
            }

            // Eğer eksik spot varsa veya hiç oluşturulmamışsa tamamla
            float startX = -(m_Columns - 1) * m_Spacing.x * 0.5f;

            for (int r = 0; r < m_Rows; r++)
            {
                for (int c = 0; c < m_Columns; c++)
                {
                    string spotName = $"Spot_R{r}_C{c}";
                    Transform existingSpot = transform.Find(spotName);
                    if (existingSpot == null)
                    {
                        GameObject spotObj = new GameObject(spotName);
                        existingSpot = spotObj.transform;
                        existingSpot.SetParent(transform, false);

                        float posX = startX + c * m_Spacing.x;
                        float posZ = -r * m_Spacing.y;

                        existingSpot.localPosition = new Vector3(posX, 0f, posZ);
                        existingSpot.localRotation = Quaternion.identity;
                        existingSpot.localScale = Vector3.one;
                    }

                    if (!m_QueueSpots.Contains(existingSpot))
                    {
                        m_QueueSpots.Add(existingSpot);
                    }
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
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(m_WaitingShips[i].gameObject);
                    else
                        Destroy(m_WaitingShips[i].gameObject);
#else
                    Destroy(m_WaitingShips[i].gameObject);
#endif
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
            shipObj.transform.localRotation = Quaternion.identity;
            shipObj.transform.localScale = Vector3.one * m_ShipScale;

            ShipController ship = shipObj.GetComponent<ShipController>();
            if (ship == null) ship = shipObj.AddComponent<ShipController>();

            Color shipColor = GetNextNeededColor();
            int capacity = GetRecommendedCapacity(shipColor);
            ship.Configure(shipColor, capacity);

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
        /// Ön sıradan bir gemi slota gönderildiğinde çağrılır.
        /// Aynı sütundaki arka gemi öne kayar ve arkaya denizden yeni gemi gelir.
        /// </summary>
        public void OnFrontShipDispatched(ShipController frontShip)
        {
            int frontIndex = m_WaitingShips.IndexOf(frontShip);
            if (frontIndex < 0) return;

            int col = frontIndex % m_Columns;
            int backIndex = m_Columns + col;

            m_WaitingShips[frontIndex] = null;

            // 1. Arkadaki gemiyi aynı sütunda öne kaydır
            if (backIndex < m_WaitingShips.Count && m_WaitingShips[backIndex] != null)
            {
                ShipController backShip = m_WaitingShips[backIndex];
                m_WaitingShips[frontIndex] = backShip;
                m_WaitingShips[backIndex] = null;

                Transform frontSpot = m_QueueSpots[frontIndex];
                backShip.transform.SetParent(frontSpot, true);

                // Su üzerinde öne doğru süzülme animasyonu
                backShip.transform.DOKill(true);
                backShip.transform.DOLocalMove(Vector3.zero, 0.48f).SetEase(Ease.OutQuad)
                    .OnUpdate(() =>
                    {
                        if (UnityEngine.Random.value < 0.20f && backShip != null)
                        {
                            ShipController.SpawnWaterRipple(backShip.transform.position + new Vector3(0f, -0.08f, 0.05f), 0.16f, 0.65f, 0.4f);
                        }
                    })
                    .OnComplete(() =>
                    {
                        if (backShip != null)
                        {
                            backShip.transform.localPosition = Vector3.zero;
                            backShip.transform.localRotation = Quaternion.identity;
                            backShip.transform.localScale = Vector3.one * m_ShipScale;
                        }
                    });
            }

            // 2. Boşalan arka yere açık denizden yeni gemi yüzerek gelsin
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(SpawnAndSailInNewShip(backIndex, 0.22f));
            }
        }

        private IEnumerator SpawnAndSailInNewShip(int spotIndex, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (spotIndex < 0 || spotIndex >= m_QueueSpots.Count) yield break;
            if (m_ShipPrefab == null) yield break;

            Transform spot = m_QueueSpots[spotIndex];

            // Gemiyi arkadan (açık denizden, local Z = -2.6f) başlat
            Vector3 startLocal = new Vector3(0f, 0f, -2.6f);

            GameObject shipObj = Instantiate(m_ShipPrefab, spot.TransformPoint(startLocal), spot.rotation, spot);
            shipObj.name = $"Waiting_Ship_{spotIndex}";
            shipObj.transform.localPosition = startLocal;
            shipObj.transform.localRotation = Quaternion.identity;
            shipObj.transform.localScale = Vector3.one * m_ShipScale;

            ShipController ship = shipObj.GetComponent<ShipController>();
            if (ship == null) ship = shipObj.AddComponent<ShipController>();

            Color shipColor = GetNextNeededColor();
            int capacity = GetRecommendedCapacity(shipColor);
            ship.Configure(shipColor, capacity);

            while (m_WaitingShips.Count <= spotIndex)
            {
                m_WaitingShips.Add(null);
            }
            m_WaitingShips[spotIndex] = ship;

            // Arkadan öne doğru süzülerek yerine yerleşsin
            shipObj.transform.DOLocalMove(Vector3.zero, 0.58f).SetEase(Ease.OutQuad)
                .OnUpdate(() =>
                {
                    if (UnityEngine.Random.value < 0.20f && ship != null)
                    {
                        ShipController.SpawnWaterRipple(ship.transform.position + new Vector3(0f, -0.08f, 0.05f), 0.16f, 0.65f, 0.4f);
                    }
                })
                .OnComplete(() =>
                {
                    if (ship != null)
                    {
                        ship.transform.localPosition = Vector3.zero;
                        ship.transform.localRotation = Quaternion.identity;
                        ship.transform.localScale = Vector3.one * m_ShipScale;
                    }
                });
        }

        private Color GetNextNeededColor()
        {
            if (ShipDispatcher.Instance != null)
            {
                Color color = ShipDispatcher.Instance.GetRemainingLevelColor();
                if (color != Color.clear) return color;
            }

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

        private int GetRecommendedCapacity(Color shipColor)
        {
            if (ShipDispatcher.Instance != null)
            {
                int remaining = ShipDispatcher.Instance.GetRemainingCountForColor(shipColor);
                if (remaining > 0)
                {
                    if (remaining <= 20) return remaining;
                    return UnityEngine.Random.Range(10, Mathf.Min(21, remaining + 1));
                }
            }
            return UnityEngine.Random.Range(10, 21);
        }
    }
}
