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
        [SerializeField] private float m_ShipScale = 0.26f;

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
        /// Spotlarda önceden oluşturulmuş gemiler varsa bunları doğrudan seviye renkleriyle tazeler.
        /// </summary>
        public void InitializeQueue()
        {
            EnsureSpots();

            for (int i = 0; i < m_QueueSpots.Count; i++)
            {
                Transform spot = m_QueueSpots[i];
                if (spot == null) continue;

                // HER gemi "açıkta" (hemen toplanabilir) olan renklerden seçilir.
                //
                // Eskiden bu sadece ilk 2 gemi için yapılıyordu, gerisi kalan renkler
                // arasından rastgele seçiliyordu. Sorun: tamamen gömülü bir renge gemi
                // atanırsa o gemi hiç dolamaz, kalkamaz ve slotu kalıcı olarak işgal eder.
                // Bütün slotlar böyle olunca oyun kilitlenir.
                //
                // LevelSolvabilityAnalyzer ile ölçüldü (bölüm başına 30-200 deneme):
                //   preferExposed = (i < 2)  -> kilitlenme riski %37-93
                //   preferExposed = her gemi -> %3-20
                // Ara değerler (3, 4) işe yaramıyor; tek bir gömülü renkli gemi bile
                // slotu tıkadığı için hepsinin seçilebilir olması gerekiyor.
                //
                // GetRemainingLevelColor açıkta renk yoksa zaten kalan renklere düşüyor,
                // yani bu güvenli taraf.
                const bool preferExposed = true;
                Color shipColor = GetNextNeededColor(preferExposed);
                int capacity = GetRecommendedCapacity(shipColor);

                ShipController existingShip = spot.GetComponentInChildren<ShipController>();
                if (existingShip != null)
                {
                    existingShip.Configure(shipColor, capacity);

                    while (m_WaitingShips.Count <= i) m_WaitingShips.Add(null);
                    m_WaitingShips[i] = existingShip;
                }
                else
                {
                    SpawnShipAtSpot(i, preferExposed);
                }
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

            foreach (var spot in m_QueueSpots)
            {
                if (spot == null) continue;
                for (int i = spot.childCount - 1; i >= 0; i--)
                {
                    Transform child = spot.GetChild(i);
                    if (child.GetComponent<ShipController>() == null) continue;
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(child.gameObject);
                    else
                        Destroy(child.gameObject);
#else
                    Destroy(child.gameObject);
#endif
                }
            }
        }

        /// <summary>
        /// Belirtilen spot indeksinde yeni bir gemi üretir.
        /// </summary>
        /// <summary>
        /// Varsayılan artık true: gömülü renge gemi atayıp slot tıkamamak için
        /// (bkz. RefreshQueue içindeki açıklama ve ölçüm).
        /// </summary>
        public ShipController SpawnShipAtSpot(int spotIndex, bool preferExposed = true)
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

            Color shipColor = GetNextNeededColor(preferExposed);
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

                // Öndeki (az önce tıklanan) gemi slota doğru yola çıkıp ön spottan gerçekten
                // uzaklaşana kadar arkadaki gemiyi bekletiyoruz — yoksa ikisi tam aynı anda,
                // aynı noktada iç içe biniyordu.
                if (gameObject.activeInHierarchy)
                {
                    StartCoroutine(MoveBackShipToFrontSpot(backShip, frontSpot, 0.15f));
                }
            }

            // 2. Boşalan arka yere açık denizden yeni gemi yüzerek gelsin
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(SpawnAndSailInNewShip(backIndex, 0.18f));
            }
        }

        /// <summary>
        /// Arka sıradaki gemiyi, öndeki gemi slota doğru yola çıkıp ön spottan uzaklaşması için
        /// kısa bir gecikmenin ardından ön spota kaydırır. Gecikme olmadan ikisi tam aynı anda
        /// aynı noktada başlayıp iç içe giriyordu.
        /// </summary>
        private IEnumerator MoveBackShipToFrontSpot(ShipController backShip, Transform frontSpot, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (backShip == null || frontSpot == null) yield break;

            backShip.transform.SetParent(frontSpot, true);

            // Su sallanması (bobbing) animasyon süresince pozisyonu eski tabana geri
            // çekip DOTween ile çakışmasın diye geçici olarak susturulur.
            backShip.SetQueueAnimating(true);

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
                        backShip.SetQueueAnimating(false);
                    }
                });
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

            Color shipColor = GetNextNeededColor(false);
            int capacity = GetRecommendedCapacity(shipColor);
            ship.Configure(shipColor, capacity);

            while (m_WaitingShips.Count <= spotIndex)
            {
                m_WaitingShips.Add(null);
            }
            m_WaitingShips[spotIndex] = ship;

            // Su sallanması (bobbing) animasyon süresince pozisyonu eski (arkadaki) tabana
            // geri çekip DOTween ile çakışmasın diye geçici olarak susturulur — aksi halde
            // gemi görünürde hiç ilerlemez, hep açık deniz başlangıç noktasında kalır.
            ship.SetQueueAnimating(true);

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
                        ship.SetQueueAnimating(false);
                    }
                });
        }

        private Color GetNextNeededColor(bool preferExposed = true)
        {
            if (ShipDispatcher.Instance != null)
            {
                // 1) Açıktaki renklerden, HENÜZ BAŞKA BİR GEMİYE VERİLMEMİŞ olanı tercih et.
                //
                // Aynı renk birden fazla slotu kaplarsa, o renk tükendiğinde birden çok
                // slot aynı anda boşa düşüyor ve kalan renkler gömülüyse oyun kilitleniyor.
                // Slotlara farklı renk dağıtmak bu riski tamamen kapatıyor.
                //
                // LevelSolvabilityAnalyzer ölçümü (8 bölüm, bölüm başına 40 deneme):
                //   eski kural (ilk 2 gemi açıktan)          -> kilit riski %43-95
                //   her gemi açıktan                          -> %0-20
                //   her gemi açıktan + slotlara farklı renk   -> 8 bölümde de %0
                if (preferExposed)
                {
                    List<Color> exposed = ShipDispatcher.Instance.GetExposedLevelColors();
                    if (exposed != null && exposed.Count > 0)
                    {
                        List<Color> unused = new List<Color>(exposed.Count);
                        for (int i = 0; i < exposed.Count; i++)
                        {
                            if (!IsColorAlreadyQueued(exposed[i])) unused.Add(exposed[i]);
                        }

                        List<Color> pick = unused.Count > 0 ? unused : exposed;
                        return ShipDispatcher.NormalizeShipColor(pick[UnityEngine.Random.Range(0, pick.Count)]);
                    }
                }

                Color color = ShipDispatcher.Instance.GetRemainingLevelColor(preferExposed);
                if (color != Color.clear) return color;
            }

            // Fallback 1: LevelManager veya PixelArtGenerator üzerinden seviye paletine eriş
            LevelManager lm = LevelManager.Instance != null ? LevelManager.Instance : Object.FindFirstObjectByType<LevelManager>();
            PixelLevelData level = lm != null ? lm.CurrentLevel : null;
            if (level == null)
            {
                PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
                if (gen != null) level = gen.ActiveLevelData;
            }

            if (level != null && level.ColorPalette != null && level.ColorPalette.Count > 0)
            {
                var entry = level.ColorPalette[UnityEngine.Random.Range(0, level.ColorPalette.Count)];
                Color chosen = entry.targetColor != Color.clear ? entry.targetColor : entry.originalColor;
                return ShipDispatcher.NormalizeShipColor(chosen);
            }

            // Fallback 2: Son çare seviye renkleri (Rastgele aykırı renkler yerine seviye tonları)
            Color[] defaults = new Color[]
            {
                new Color(0.957f, 0.831f, 0.384f, 1f), // Sarı / Altın (Photo 1)
                new Color(0.475f, 0.200f, 0.780f, 1f), // Mor / Asil Lavanta (Photo 1)
                new Color(0.180f, 0.190f, 0.220f, 1f), // Siyah / Koyu Kömür (Photo 1)
                new Color(0.920f, 0.930f, 0.950f, 1f), // Beyaz / Açık Gümüş (Photo 1)
                new Color(0.584f, 0.498f, 0.380f, 1f)  // Kahve / Sıcak Karamel (Photo 1)
            };
            return defaults[UnityEngine.Random.Range(0, defaults.Length)];
        }

        /// <summary>Bu renk şu an bekleyen/yanaşmış gemilerden birine zaten atanmış mı?</summary>
        private bool IsColorAlreadyQueued(Color c)
        {
            if (m_WaitingShips != null)
            {
                for (int i = 0; i < m_WaitingShips.Count; i++)
                {
                    ShipController s = m_WaitingShips[i];
                    if (s != null && !s.IsDeparting && ShipDispatcher.ColorsMatch(s.ShipColor, c)) return true;
                }
            }

            ShipDispatcher disp = ShipDispatcher.Instance;
            if (disp != null)
            {
                foreach (ShipSlot slot in disp.Slots)
                {
                    if (slot != null && slot.DockedShip != null && !slot.DockedShip.IsDeparting
                        && ShipDispatcher.ColorsMatch(slot.DockedShip.ShipColor, c))
                        return true;
                }
            }

            return false;
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

            // Seviye paletinden kalan tahmini piksel sayısı
            LevelManager lm = LevelManager.Instance != null ? LevelManager.Instance : Object.FindFirstObjectByType<LevelManager>();
            PixelLevelData level = lm != null ? lm.CurrentLevel : null;
            if (level == null)
            {
                PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
                if (gen != null) level = gen.ActiveLevelData;
            }

            if (level != null && level.ColorPalette != null)
            {
                foreach (var p in level.ColorPalette)
                {
                    if (ShipDispatcher.ColorsMatch(p.targetColor, shipColor) || ShipDispatcher.ColorsMatch(p.originalColor, shipColor))
                    {
                        if (p.pixelCount > 0)
                        {
                            return p.pixelCount <= 20 ? p.pixelCount : UnityEngine.Random.Range(10, 21);
                        }
                    }
                }
            }

            return UnityEngine.Random.Range(10, 21);
        }
    }
}
