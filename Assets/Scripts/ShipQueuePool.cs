using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// Belirli bir sütun sayısına (2, 3, 4, 5, 6 sütun) özel gemi boyutu ve aralık profili.
    /// Kullanıcı Inspector'dan istediği sütun sayısına göre ayarları özelleştirebilir.
    /// </summary>
    [System.Serializable]
    public class ColumnLayoutPreset
    {
        [Tooltip("Bu kuralın geçerli olduğu sütun sayısı (Örn: 2, 3, 4, 5, 6)")]
        public int columns = 4;

        [Range(0.10f, 0.40f)]
        [Tooltip("Bu sütun sayısındaki gemi boyutu")]
        public float shipScale = 0.21f;

        [Range(0.5f, 2.5f)]
        [Tooltip("Bu sütun sayısındaki yatay aralık (X)")]
        public float spacingX = 1.20f;

        [Range(0.5f, 2.5f)]
        [Tooltip("Bu sütun sayısındaki dikey aralık (Y)")]
        public float spacingY = 1.25f;

        [Range(-8.0f, 0.0f)]
        [Tooltip("Bu sütun sayısındaki havuz dikey konumu (Offset Y)")]
        public float offsetY = -4.95f;

        public ColumnLayoutPreset() { }

        public ColumnLayoutPreset(int cols, float scale, float spX, float spY, float offY)
        {
            columns = cols;
            shipScale = scale;
            spacingX = spX;
            spacingY = spY;
            offsetY = offY;
        }
    }

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

        [Header("📏 Gemi Boyutu & Aralıkları (Canlı Ayarlanabilir)")]
        [Tooltip("Gemilerin boyutu/ölçeği (Varsayılan: 0.295 - %18 büyütülmüş dolgun gemiler).")]
        [Range(0.12f, 0.50f)]
        [SerializeField] private float m_ShipScale = 0.295f;

        [Tooltip("Gemiler arasındaki yatay aralık (X ekseni). Kumsala taşmaması için slider ile anında ayarlayabilirsiniz.")]
        [Range(0.4f, 2.5f)]
        [SerializeField] private float m_SpacingX = 1.48f;

        [Tooltip("Ön ve arka sıralar arasındaki dikey aralık (Y ekseni). Gemilerin iç içe girmemesi için slider ile ayarlayabilirsiniz.")]
        [Range(0.6f, 3.0f)]
        [SerializeField] private float m_SpacingY = 2.30f;

        [Tooltip("Tüm gemi havuzunun dikey konumu (Yüksekliği).")]
        [Range(-10f, 0f)]
        [SerializeField] private float m_OffsetY = -7.40f;

        [Header("🎛️ Sütun Sayısına Göre Özel Profiller (Presets)")]
        [Tooltip("Açık olduğunda sütun sayısına (2, 3, 4, 5, 6) göre aşağıdaki profil ayarları otomatik uygulanır.")]
        [SerializeField] private bool m_UseColumnPresets = true;

        [SerializeField] private List<ColumnLayoutPreset> m_ColumnPresets = new List<ColumnLayoutPreset>()
        {
            new ColumnLayoutPreset(1, 0.50f, 1.55f, 2.60f, -6.88f),
            new ColumnLayoutPreset(2, 0.472f, 1.55f, 2.60f, -6.88f),
            new ColumnLayoutPreset(3, 0.354f, 1.48f, 2.30f, -7.40f),
            new ColumnLayoutPreset(4, 0.330f, 1.08f, 2.20f, -6.88f),
            new ColumnLayoutPreset(5, 0.295f, 0.82f, 2.48f, -8.15f),
            new ColumnLayoutPreset(6, 0.278f, 0.68f, 2.20f, -6.88f)
        };

        // Geriye dönük uyumluluk
        [SerializeField, HideInInspector] private Vector2 m_Spacing = new Vector2(1.20f, 1.25f);

        [Header("📍 Kuyruk Yerleri")]
        [SerializeField] private List<Transform> m_QueueSpots = new List<Transform>();
        [SerializeField] private List<ShipController> m_WaitingShips = new List<ShipController>();

        // Özel gemi sırası: sütun başına bir şerit. Tasarım listesindeki i. gemi
        // (i % sütunSayısı). şeride düşer; her sütun yalnız kendi şeridinden beslenir.
        // Böylece tasarımda hangi sütundaysa gemi oyunda da o sütuna gelir.
        private List<Queue<WagonSequenceEntry>> m_LevelSequenceLanes = new List<Queue<WagonSequenceEntry>>();
        private bool m_UsingLevelSequence = false;

        public int Capacity => m_Columns * m_Rows;
        public List<ShipController> WaitingShips => m_WaitingShips;
        public bool UsingLevelSequence => m_UsingLevelSequence;
        public int RemainingSequenceShipsCount => RemainingSequenceShipsTotal();

        private int RemainingSequenceShipsTotal()
        {
            int n = 0;
            if (m_LevelSequenceLanes != null)
                foreach (var q in m_LevelSequenceLanes)
                    if (q != null) n += q.Count;
            return n;
        }

        private bool LevelSequenceLanesExhausted()
        {
            if (m_LevelSequenceLanes == null) return true;
            foreach (var q in m_LevelSequenceLanes)
                if (q != null && q.Count > 0) return false;
            return true;
        }

        public bool UseColumnPresets { get => m_UseColumnPresets; set => m_UseColumnPresets = value; }
        public List<ColumnLayoutPreset> ColumnPresets => m_ColumnPresets;
        public float ShipScale => m_ShipScale;
        public float SpacingX => m_SpacingX;
        public float SpacingY => m_SpacingY;
        public float OffsetY => m_OffsetY;

        public ColumnLayoutPreset GetPresetForColumns(int cols)
        {
            if (m_ColumnPresets == null || m_ColumnPresets.Count == 0) return null;
            for (int i = 0; i < m_ColumnPresets.Count; i++)
            {
                if (m_ColumnPresets[i] != null && m_ColumnPresets[i].columns == cols)
                    return m_ColumnPresets[i];
            }
            return null;
        }

        public bool ApplyPresetForColumns(int cols)
        {
            if (!m_UseColumnPresets) return false;
            var preset = GetPresetForColumns(cols);
            if (preset != null)
            {
                m_ShipScale = preset.shipScale;
                m_SpacingX = preset.spacingX;
                m_SpacingY = preset.spacingY;
                m_OffsetY = preset.offsetY;

                Vector3 lp = transform.localPosition;
                lp.y = m_OffsetY;
                transform.localPosition = lp;
                return true;
            }
            return false;
        }

        public PixelLevelData GetActiveLevel()
        {
            LevelManager lm = LevelManager.Instance != null ? LevelManager.Instance : Object.FindFirstObjectByType<LevelManager>();
            if (lm != null && lm.CurrentLevel != null) return lm.CurrentLevel;

            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen != null && gen.ActiveLevelData != null) return gen.ActiveLevelData;

            return null;
        }

        public int GetTotalCapacityOfActiveShips()
        {
            // Sahnedeki TÜM canlı gemiler sayılır: kuyrukta bekleyen, slota doğru yolda olan ve slottaki.
            // Eskiden sadece kuyruk listesi + slottakiler sayılıyordu; slota giden gemi ikisinde de olmadığı
            // için kapasite eksik görünüyor, sıra bitince gereksiz ekstra gemiler (yanlış sayılarla) doğuyordu.
            // Rezerve edilmiş (yoldaki) küpler GetTotalRemainingCubes'ta sayılmadığı için burada da düşülür.
            int total = 0;
            var ships = ShipController.ActiveShips;
            for (int i = 0; i < ships.Count; i++)
            {
                ShipController s = ships[i];
                if (s == null || s.IsDeparting || !s.gameObject.activeInHierarchy) continue;
                total += Mathf.Max(0, s.Capacity - s.CurrentCargo - s.PendingCargo);
            }
            return total;
        }

        /// <summary>
        /// Kuyrukta şu anda hazır bekleyen (henüz slota gitmemiş, ayrılmamış) aktif gemileri döner.
        /// </summary>
        public List<ShipController> GetActiveWaitingShips()
        {
            List<ShipController> list = new List<ShipController>();
            if (m_WaitingShips != null)
            {
                for (int i = 0; i < m_WaitingShips.Count; i++)
                {
                    ShipController s = m_WaitingShips[i];
                    if (s != null && !s.IsDocked && !s.IsDeparting && s.gameObject.activeInHierarchy)
                    {
                        list.Add(s);
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Seviyede açık denizde veya sırada bekleyen başka yeni gemi gelip gelmeyeceğini döner.
        /// Eğer sırada başka gemi kalmadıysa ve tüm küpler mevcut gemilere yetiyorsa true döner.
        /// </summary>
        public bool HasNoMoreFutureShips()
        {
            if (m_UsingLevelSequence)
            {
                return LevelSequenceLanesExhausted();
            }
            else
            {
                int remainingCubes = ShipDispatcher.Instance != null ? ShipDispatcher.Instance.GetTotalRemainingCubes() : 0;
                int currentShipCapacity = GetTotalCapacityOfActiveShips();
                return remainingCubes <= currentShipCapacity;
            }
        }

        /// <summary>
        /// Gemiyi kuyruktan tamamen çıkarır ve referansını temizler.
        /// </summary>
        public void RemoveShipFromQueue(ShipController ship)
        {
            if (ship == null || m_WaitingShips == null) return;
            int idx = m_WaitingShips.IndexOf(ship);
            if (idx >= 0)
            {
                m_WaitingShips[idx] = null;
            }
        }

        // Son çekilen sıra girdisinin "gizli gemi" bayrağı (TryGetNextSequenceShip her çağrıda günceller)
        private bool m_LastSequenceShipHidden = false;

        // Spotun sütununa ait şeritten sıradaki gemiyi çeker.
        private bool TryGetNextSequenceShip(int spotIndex, out Color shipColor, out int capacity, out int linkId)
        {
            m_LastSequenceShipHidden = false;
            if (m_UsingLevelSequence && m_LevelSequenceLanes != null && m_LevelSequenceLanes.Count > 0)
            {
                int lane = spotIndex % m_LevelSequenceLanes.Count;
                if (lane < 0) lane += m_LevelSequenceLanes.Count;
                Queue<WagonSequenceEntry> q = m_LevelSequenceLanes[lane];
                if (q != null && q.Count > 0)
                {
                    WagonSequenceEntry entry = q.Dequeue();
                    if (entry != null && entry.capacity > 0)
                    {
                        shipColor = entry.wagonColor;
                        capacity = entry.capacity;
                        linkId = entry.linkId;
                        m_LastSequenceShipHidden = entry.isHidden;
                        return true;
                    }
                }
            }
            shipColor = Color.clear;
            capacity = 0;
            linkId = 0;
            return false;
        }

        public static ShipQueuePool Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            EnsureSpots();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                InitializeQueue();
            }
        }

        private float m_NextQueueHealTime;
        private readonly Dictionary<ShipController, float> m_MisplacedSince = new Dictionary<ShipController, float>();

        // Güvenlik ağı: kuyruk listesindeki yeriyle fiziksel spotu uyuşmayan (yarıda kesilmiş/bayat
        // kaydırma yüzünden ortada kalmış) gemiyi yerine süzdürür.
        private void Update()
        {
            if (!Application.isPlaying || Time.unscaledTime < m_NextQueueHealTime) return;
            m_NextQueueHealTime = Time.unscaledTime + 0.5f;
            if (!gameObject.activeInHierarchy) return;

            int n = Mathf.Min(m_WaitingShips.Count, m_QueueSpots.Count);
            for (int i = 0; i < n; i++)
            {
                ShipController s = m_WaitingShips[i];
                Transform spot = m_QueueSpots[i];
                if (s == null || spot == null || !s.gameObject.activeInHierarchy) continue;
                bool busy = s.IsMoving || s.IsDocked || s.IsDeparting || s.IsDragging || s.IsQueueAnimating || DOTween.IsTweening(s.transform);
                if (busy || s.transform.parent == spot) { m_MisplacedSince.Remove(s); continue; }

                // Normal kaydırma kısa gecikmeyle başlar; sadece uzun süre yanlış yerde kalan gemiye müdahale et
                if (!m_MisplacedSince.TryGetValue(s, out float since)) { m_MisplacedSince[s] = Time.unscaledTime; continue; }
                if (Time.unscaledTime - since < 0.6f) continue;
                m_MisplacedSince.Remove(s);
                StartCoroutine(MoveBackShipToFrontSpot(s, spot, 0f));
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Edit modunda veya Play modunda Inspector slider'ı oynatıldığında anında güncelle
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && gameObject != null)
                {
                    ApplyLiveSettings();
                }
            };
        }

        /// <summary>
        /// Inspector'daki slider ayarlarını (Gemi Ölçeği, X/Y Aralıkları, Havuz Yüksekliği)
        /// hem Edit hem Play modunda sahnedeki tüm gemilere anında uygular.
        /// </summary>
        public void ApplyLiveSettings()
        {
            int fingerprintBefore = Application.isPlaying ? 0 : ComputeHierarchyFingerprint();

            // 1. Havuzun genel dikey yüksekliğini (Y) güncelle
            Vector3 lp = transform.localPosition;
            lp.y = m_OffsetY;
            transform.localPosition = lp;

            // 2. Kullanıcının X ve Y aralıklarıyla spotları yeniden konumlandır
            RebuildSpots(m_Columns, m_Rows);

            // 3. Sahnede var olan tüm gemilerin (kuyruktaki + çocuklardaki) ölçeğini kullanıcının slider ayarıyla güncelle
            ShipController[] allShips = GetComponentsInChildren<ShipController>(true);
            foreach (var s in allShips)
            {
                if (s != null)
                {
                    s.SetBaseScale(Vector3.one * m_ShipScale);
                    s.transform.localScale = s.GetLocalScaleForBaseWorldScale();
                }
            }

            // 4. Eğer oyundaysak ve slota yanaşmış gemiler varsa onların da temel boyutunu senkronize et
            if (ShipDispatcher.Instance != null && ShipDispatcher.Instance.Slots != null)
            {
                foreach (var slot in ShipDispatcher.Instance.Slots)
                {
                    if (slot != null && slot.DockedShip != null)
                    {
                        slot.DockedShip.SetBaseScale(Vector3.one * m_ShipScale);
                    }
                }
            }

            // OnValidate sahne her yüklendiğinde (Play'den çıkış, derleme) de tetiklenir; hiçbir şey
            // değişmediyse sahneyi kirletme, yoksa sahne sürekli "değişti" (*) görünüyordu.
            if (!Application.isPlaying && ComputeHierarchyFingerprint() != fingerprintBefore)
            {
                UnityEditor.EditorUtility.SetDirty(gameObject);
                UnityEditor.SceneView.RepaintAll();
            }
        }

        /// <summary>
        /// Havuz ve altındaki tüm objelerin (spotlar, gemiler) isim/transform özetini döndürür;
        /// ApplyLiveSettings'in gerçekten bir değişiklik yapıp yapmadığını anlamak için.
        /// </summary>
        private int ComputeHierarchyFingerprint()
        {
            unchecked
            {
                int hash = 17;
                foreach (Transform t in GetComponentsInChildren<Transform>(true))
                {
                    hash = hash * 31 + t.GetInstanceID();
                    hash = hash * 31 + t.localPosition.GetHashCode();
                    hash = hash * 31 + t.localRotation.GetHashCode();
                    hash = hash * 31 + t.localScale.GetHashCode();
                }
                return hash;
            }
        }
#endif

        /// <summary>
        /// Kuyruk için bekleme noktalarını (spot) seviyenin kolon ve sıra ayarlarına göre dinamik olarak oluşturur ve konumlandırır.
        /// </summary>
        public void RebuildSpots(int targetCols, int targetRows)
        {
            m_Columns = Mathf.Clamp(targetCols, 1, 8);
            m_Rows = Mathf.Clamp(targetRows, 1, 10);
            int totalNeeded = m_Columns * m_Rows;

            // Eğer sütun profilleri aktifse, bu sütun sayısına özel boyutu ve aralıkları otomatik uygula
            if (m_UseColumnPresets)
            {
                ApplyPresetForColumns(m_Columns);
            }

            // 1. Mevcut spotları topla
            m_QueueSpots.Clear();
            List<Transform> existing = new List<Transform>();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform cTr = transform.GetChild(i);
                if (cTr.name.StartsWith("Spot_"))
                {
                    existing.Add(cTr);
                }
            }

            // Fazla spotları kaldır (spot altındaki gemilerle birlikte)
            while (existing.Count > totalNeeded)
            {
                int lastIdx = existing.Count - 1;
                Transform doomed = existing[lastIdx];
                existing.RemoveAt(lastIdx);
                if (doomed != null)
                {
                    for (int c = doomed.childCount - 1; c >= 0; c--)
                    {
                        Transform ship = doomed.GetChild(c);
                        ship.SetParent(null);
                        ship.gameObject.SetActive(false);
#if UNITY_EDITOR
                        if (!Application.isPlaying) DestroyImmediate(ship.gameObject);
                        else Destroy(ship.gameObject);
#else
                        Destroy(ship.gameObject);
#endif
                    }
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(doomed.gameObject);
                    else Destroy(doomed.gameObject);
#else
                    Destroy(doomed.gameObject);
#endif
                }
            }

            // Eksik spotları oluştur
            while (existing.Count < totalNeeded)
            {
                GameObject spotObj = new GameObject($"Spot_{existing.Count}");
                spotObj.transform.SetParent(transform, false);
                existing.Add(spotObj.transform);
            }

            // Kolon ve sıra aralıklarını doğrudan Inspector'daki slider ayarlarından al (kullanıcı tam kontrol sahibi)
            float actualSpacingX = m_SpacingX > 0.001f ? m_SpacingX : 1.20f;
            float actualSpacingY = m_SpacingY > 0.001f ? m_SpacingY : 1.25f;
            m_Spacing = new Vector2(actualSpacingX, actualSpacingY);

            float startX = -(m_Columns - 1) * actualSpacingX * 0.5f;

            for (int r = 0; r < m_Rows; r++)
            {
                for (int c = 0; c < m_Columns; c++)
                {
                    int spotIdx = r * m_Columns + c;
                    Transform spot = existing[spotIdx];
                    spot.name = $"Spot_R{r}_C{c}";

                    float posX = startX + c * actualSpacingX;
                    float posZ = -r * actualSpacingY;

                    spot.localPosition = new Vector3(posX, 0f, posZ);
                    spot.localRotation = Quaternion.identity;
                    spot.localScale = Vector3.one;

                    m_QueueSpots.Add(spot);
                }
            }
        }

        public void EnsureSpots()
        {
            PixelLevelData level = GetActiveLevel();
            int targetCols = (level != null && level.PoolColumns > 0) ? level.PoolColumns : m_Columns;
            int targetRows = (level != null && level.PoolRows > 0) ? level.PoolRows : m_Rows;
            RebuildSpots(targetCols, targetRows);
        }

        private int m_LastInitFrame = -1;
        private PixelLevelData m_LastInitLevel = null;

        /// <summary>
        /// Seviye başlangıcında kuyruğu renkli gemilerle doldurur.
        /// Seviyede özel gemi sırası (WagonSequence) varsa tam o sıra ve kapasiteler kullanılır;
        /// yoksa dinamik kilitlenmesiz renk ve kapasite algoritması devreye girer.
        /// </summary>
        public void InitializeQueue()
        {
            PixelLevelData level = GetActiveLevel();
            int targetCols = (level != null && level.PoolColumns > 0) ? level.PoolColumns : m_Columns;
            int targetRows = (level != null && level.PoolRows > 0) ? level.PoolRows : m_Rows;

            // Aynı frame içinde mükerrer çağrıları engelle (Start + LevelLoaded çakışması)
            if (Application.isPlaying && m_LastInitFrame == Time.frameCount && m_LastInitLevel == level)
            {
                return;
            }
            m_LastInitFrame = Application.isPlaying ? Time.frameCount : -1;
            m_LastInitLevel = level;

            // Eski çalışan kuyruk coroutine'lerini durdur ve tüm eski gemileri/halatları sıfırla
            StopAllCoroutines();
            ClearQueue();

            RebuildSpots(targetCols, targetRows);

            m_WaitingShips.Clear();
            for (int i = 0; i < m_QueueSpots.Count; i++)
            {
                m_WaitingShips.Add(null);
            }

            // 1. Seviyede tanımlı özel gemi sırası var mı kontrol et
            m_LevelSequenceLanes.Clear();
            if (level != null && level.UseCustomWagonSequence && level.WagonSequence != null && level.WagonSequence.Count > 0)
            {
                m_UsingLevelSequence = true;
                int laneCount = Mathf.Max(1, targetCols);
                for (int l = 0; l < laneCount; l++) m_LevelSequenceLanes.Add(new Queue<WagonSequenceEntry>());
                int li = 0;
                foreach (var entry in level.WagonSequence)
                {
                    if (entry != null && entry.capacity > 0)
                    {
                        m_LevelSequenceLanes[li % laneCount].Enqueue(entry);
                        li++;
                    }
                }
                Debug.Log($"<color=#00FFAA><b>[ShipQueuePool]</b></color> 🚢 Seviye özel gemi sırası devrede: {li} adet gemi {laneCount} şeride dağıtıldı.");
            }
            else
            {
                m_UsingLevelSequence = false;
            }

            for (int i = 0; i < m_QueueSpots.Count; i++)
            {
                Transform spot = m_QueueSpots[i];
                if (spot == null) continue;

                // Eğer özel sıra kullanılıyorsa ve tüm sıra baştan az sayıda gemiden ibaretse fazla spotları doldurma
                if (m_UsingLevelSequence && LevelSequenceLanesExhausted() && i >= level.WagonSequence.Count)
                {
                    continue;
                }

                Color shipColor;
                int capacity;
                int linkId;
                if (!TryGetNextSequenceShip(i, out shipColor, out capacity, out linkId))
                {
                    // Özel sırada bu sütunun şeridi bitti: spot boş kalsın (yedek gemi üretme)
                    if (m_UsingLevelSequence)
                    {
                        continue;
                    }

                    const bool preferExposed = true;
                    shipColor = GetNextNeededColor(preferExposed);
                    capacity = GetRecommendedCapacity(shipColor);
                    linkId = 0;
                }

                SpawnShipAtSpot(i, shipColor, capacity, linkId, m_LastSequenceShipHidden);
            }

            // Tüm gemiler oluştuktan sonra bağlı olanları eşleştir ve aralarına halat/zincir çek
            RefreshLinkedShipTethers();
        }

        public void ClearQueue()
        {
            for (int i = m_WaitingShips.Count - 1; i >= 0; i--)
            {
                if (m_WaitingShips[i] != null)
                {
                    var shipGo = m_WaitingShips[i].gameObject;
                    m_WaitingShips[i].transform.SetParent(null);
                    shipGo.SetActive(false);
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(shipGo);
                    else
                        Destroy(shipGo);
#else
                    Destroy(shipGo);
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
                    child.SetParent(null);
                    child.gameObject.SetActive(false);
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

            ClearAllTethers();
        }

        /// <summary>
        /// Belirtilen spot indeksinde yeni bir gemi üretir.
        /// </summary>
        public ShipController SpawnShipAtSpot(int spotIndex, Color shipColor, int capacity, int linkId = 0, bool hidden = false)
        {
            if (spotIndex < 0 || spotIndex >= m_QueueSpots.Count) return null;
            if (m_ShipPrefab == null) return null;

            Transform spot = m_QueueSpots[spotIndex];

            // Varsa eski ölü / pasif objeleri temizle
            for (int c = spot.childCount - 1; c >= 0; c--)
            {
                Transform oldChild = spot.GetChild(c);
                if (oldChild.GetComponent<ShipController>() != null)
                {
                    oldChild.SetParent(null);
                    oldChild.gameObject.SetActive(false);
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(oldChild.gameObject);
                    else Destroy(oldChild.gameObject);
#else
                    Destroy(oldChild.gameObject);
#endif
                }
            }

            GameObject shipObj = Instantiate(m_ShipPrefab, spot.position, spot.rotation, spot);
            shipObj.name = $"Waiting_Ship_{spotIndex}";
            shipObj.transform.localPosition = Vector3.zero;
            shipObj.transform.localRotation = Quaternion.identity;
            shipObj.transform.localScale = Vector3.one * m_ShipScale;

            ShipController ship = shipObj.GetComponent<ShipController>();
            if (ship == null) ship = shipObj.AddComponent<ShipController>();
            ship.SetBaseScale(Vector3.one * m_ShipScale);
            ship.SetLinkedPartner(null, linkId);

            ship.Configure(shipColor, capacity);
            ship.SetMysteryHidden(hidden);

            while (m_WaitingShips.Count <= spotIndex)
            {
                m_WaitingShips.Add(null);
            }
            m_WaitingShips[spotIndex] = ship;

            return ship;
        }

        public ShipController SpawnShipAtSpot(int spotIndex, bool preferExposed = true)
        {
            Color shipColor;
            int capacity;
            int linkId;
            if (!TryGetNextSequenceShip(spotIndex, out shipColor, out capacity, out linkId))
            {
                shipColor = GetNextNeededColor(preferExposed);
                capacity = GetRecommendedCapacity(shipColor);
                linkId = 0;
            }

            return SpawnShipAtSpot(spotIndex, shipColor, capacity, linkId, m_LastSequenceShipHidden);
        }

        /// <summary>
        /// Sahnedeki veya kuyruktaki tüm bağlı gemileri eşleştirir ve aralarına dinamik halat/zincir (tether) çeker.
        /// Slotlara yanaşmış gemiler dahil tüm bağlı gemilerin halatları korunur; asla gereksiz yere silinmez.
        /// </summary>
        public void RefreshLinkedShipTethers()
        {
            // 1. Sahnedeki mevcut tether objelerini kontrol et: Sadece geçersiz/kopmuş olanları temizle, geçerlileri koru!
            LinkedShipTether[] oldTethers = UnityEngine.Object.FindObjectsByType<LinkedShipTether>(FindObjectsSortMode.None);
            HashSet<int> activeTetherLinkIds = new HashSet<int>();

            for (int i = oldTethers.Length - 1; i >= 0; i--)
            {
                LinkedShipTether t = oldTethers[i];
                if (t == null) continue;

                bool isValid = t.ShipA != null && t.ShipB != null &&
                               t.ShipA.gameObject.activeInHierarchy && t.ShipB.gameObject.activeInHierarchy &&
                               !t.ShipA.IsDeparting && !t.ShipB.IsDeparting &&
                               t.ShipA.LinkId == t.LinkId && t.ShipB.LinkId == t.LinkId &&
                               t.LinkId > 0;

                if (!isValid)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(t.gameObject);
                    else Destroy(t.gameObject);
#else
                    Destroy(t.gameObject);
#endif
                }
                else
                {
                    activeTetherLinkIds.Add(t.LinkId);
                    t.ShipA.SetLinkedPartner(t.ShipB, t.LinkId, t);
                    t.ShipB.SetLinkedPartner(t.ShipA, t.LinkId, t);
                }
            }

            // 2. Sahnedeki TÜM aktif gemileri topla (kuyruk + slotlar + su üzerinde yüzenler)
            List<ShipController> activeShips = new List<ShipController>();

            // Kuyruktaki gemiler
            if (m_WaitingShips != null)
            {
                foreach (var s in m_WaitingShips)
                {
                    if (s != null && s.LinkId > 0 && s.gameObject.activeInHierarchy && !s.IsDeparting && !activeShips.Contains(s))
                    {
                        activeShips.Add(s);
                    }
                }
            }

            // Slotlardaki yanaşmış gemiler
            if (ShipDispatcher.Instance != null && ShipDispatcher.Instance.Slots != null)
            {
                foreach (var slot in ShipDispatcher.Instance.Slots)
                {
                    if (slot != null && slot.DockedShip != null)
                    {
                        ShipController s = slot.DockedShip;
                        if (s.LinkId > 0 && s.gameObject.activeInHierarchy && !s.IsDeparting && !activeShips.Contains(s))
                        {
                            activeShips.Add(s);
                        }
                    }
                }
            }

            // Sahnedeki diğer aktif gemiler (hareket halindekiler dahil)
            var sceneShips = ShipController.ActiveShips;
            for (int i = 0; i < sceneShips.Count; i++)
            {
                var s = sceneShips[i];
                if (s != null && s.LinkId > 0 && s.gameObject.activeInHierarchy && !s.IsDeparting && !activeShips.Contains(s))
                {
                    activeShips.Add(s);
                }
            }

            // 3. linkId'lerine göre grupla
            Dictionary<int, List<ShipController>> linkGroups = new Dictionary<int, List<ShipController>>();
            foreach (var s in activeShips)
            {
                if (!linkGroups.ContainsKey(s.LinkId))
                {
                    linkGroups[s.LinkId] = new List<ShipController>();
                }
                linkGroups[s.LinkId].Add(s);
            }

            // 4. Halatı henüz olmayan bağlı gruplar için yeni tether oluştur
            foreach (var kvp in linkGroups)
            {
                int linkId = kvp.Key;
                List<ShipController> ships = kvp.Value;

                if (ships.Count >= 2)
                {
                    ShipController a = ships[0];
                    ShipController b = ships[1];

                    if (!activeTetherLinkIds.Contains(linkId))
                    {
                        LinkedShipTether tether = LinkedShipTether.CreateTether(a, b, linkId);
                        a.SetLinkedPartner(b, linkId, tether);
                        b.SetLinkedPartner(a, linkId, tether);
                        activeTetherLinkIds.Add(linkId);
                    }
                    else
                    {
                        LinkedShipTether existing = a.Tether != null ? a.Tether : b.Tether;
                        if (a.LinkedPartner != b || a.Tether == null) a.SetLinkedPartner(b, linkId, existing);
                        if (b.LinkedPartner != a || b.Tether == null) b.SetLinkedPartner(a, linkId, existing);
                    }
                }
                else if (ships.Count == 1 && ships[0].LinkedPartner == null)
                {
                    // 5. Partner henüz doğmadı (sütun sırasında bekliyor): halat, partnerin denizden
                    //    geleceği noktaya uzansın; yoksa partner kameraya girene kadar bağ hiç görünmüyordu.
                    CreatePendingTetherIfPartnerQueued(ships[0], linkId);
                }
            }
        }

        private void CreatePendingTetherIfPartnerQueued(ShipController ship, int linkId)
        {
            if (!m_UsingLevelSequence || m_LevelSequenceLanes == null) return;

            for (int lane = 0; lane < m_LevelSequenceLanes.Count; lane++)
            {
                Queue<WagonSequenceEntry> q = m_LevelSequenceLanes[lane];
                if (q == null) continue;
                foreach (WagonSequenceEntry entry in q)
                {
                    if (entry == null || entry.linkId != linkId) continue;

                    // Bu şeridin yeni gemisi, sütunun en arka spotuna denizden (yerel Z = -2.6) gelir
                    int spawnIdx = (m_Rows - 1) * m_Columns + (lane % Mathf.Max(1, m_Columns));
                    if (spawnIdx < 0 || spawnIdx >= m_QueueSpots.Count || m_QueueSpots[spawnIdx] == null) return;

                    Vector3 spawnPoint = new Vector3(0f, 0.95f * m_ShipScale, -2.6f);
                    // Gizli gemi doğduğunda da renk görünmez; halat yarısı nötr halat renginde kalır
                    Color partnerColor = entry.isHidden ? new Color(0.85f, 0.65f, 0.35f, 1f) : entry.wagonColor;
                    LinkedShipTether.CreatePendingTether(ship, linkId, m_QueueSpots[spawnIdx], spawnPoint, partnerColor);
                    return;
                }
            }
        }

        public void ClearAllTethers()
        {
            LinkedShipTether[] oldTethers = UnityEngine.Object.FindObjectsByType<LinkedShipTether>(FindObjectsSortMode.None);
            for (int i = oldTethers.Length - 1; i >= 0; i--)
            {
                if (oldTethers[i] != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(oldTethers[i].gameObject);
                    else Destroy(oldTethers[i].gameObject);
#else
                    Destroy(oldTethers[i].gameObject);
#endif
                }
            }
        }

        /// <summary>
        /// Kuyruktaki bir geminin en ön sırada olup olmadığını kontrol eder.
        /// Eğer geminin önündeki tüm sıralar boş/ayrılmış ise bu gemi sütunun en önündeki serbest gemidir.
        /// </summary>
        public bool IsFrontRow(ShipController ship)
        {
            if (ship == null) return false;
            int index = m_WaitingShips != null ? m_WaitingShips.IndexOf(ship) : -1;
            if (index < 0 && ship.transform.parent != null)
            {
                int spotIdx = m_QueueSpots.IndexOf(ship.transform.parent);
                if (spotIdx >= 0)
                {
                    index = spotIdx;
                    while (m_WaitingShips.Count <= spotIdx) m_WaitingShips.Add(null);
                    m_WaitingShips[spotIdx] = ship;
                }
            }
            if (index < 0) return false;

            int col = index % m_Columns;
            int row = index / m_Columns;
            if (row == 0) return true;

            for (int r = 0; r < row; r++)
            {
                int checkIdx = r * m_Columns + col;
                if (checkIdx < m_WaitingShips.Count)
                {
                    ShipController blocker = m_WaitingShips[checkIdx];
                    if (blocker != null && blocker.gameObject.activeInHierarchy && !blocker.IsDeparting && !blocker.IsDocked)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Bir geminin çıkış yolunun açık olup olmadığını kontrol eder.
        /// - Tekil gemi ise: Önündeki tüm sıralar boş olmalıdır.
        /// - Bağlı gemi ise: Geminin önünde kendi bağlı partneri DIŞINDA başka bir engel gemi olmamalıdır.
        ///   (Böylece aynı sütunda alt alta bağlı olan gemiler birbirini engellemez!)
        /// </summary>
        public bool IsShipUnblockedForDispatch(ShipController ship)
        {
            if (ship == null) return false;
            int index = m_WaitingShips != null ? m_WaitingShips.IndexOf(ship) : -1;
            if (index < 0 && ship.transform.parent != null)
            {
                int spotIdx = m_QueueSpots.IndexOf(ship.transform.parent);
                if (spotIdx >= 0)
                {
                    index = spotIdx;
                    while (m_WaitingShips.Count <= spotIdx) m_WaitingShips.Add(null);
                    m_WaitingShips[spotIdx] = ship;
                }
            }
            if (index < 0) return false;

            int col = index % m_Columns;
            int row = index / m_Columns;

            // Eğer zaten en ön sıradaysa önü tamamen açıktır
            if (row == 0) return true;

            // Önündeki tüm satırları kontrol et
            for (int r = 0; r < row; r++)
            {
                int checkIdx = r * m_Columns + col;
                if (checkIdx < m_WaitingShips.Count)
                {
                    ShipController blocker = m_WaitingShips[checkIdx];
                    if (blocker != null && blocker.gameObject.activeInHierarchy && !blocker.IsDeparting && !blocker.IsDocked)
                    {
                        // Eğer öndeki gemi bu geminin kendi bağlı partneri ise engel sayılmaz!
                        if (ship.IsLinked && ship.LinkedPartner == blocker)
                        {
                            continue;
                        }
                        // Başka yabancı bir gemi varsa önü kapalıdır
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// İki bağlı gemi birlikte slota gönderildiğinde kuyruğu günceller.
        /// Eğer aynı sütundaysalar (alt alta / tandem), o sütunun iki sırasını birden çıkarıp arkadakileri öne kaydırır.
        /// Farklı sütundaysalar her sütunu ayrı ayrı günceller.
        /// </summary>
        public void OnLinkedShipsDispatched(ShipController shipA, ShipController shipB)
        {
            if (shipA == null && shipB == null) return;
            if (shipA == null) { OnFrontShipDispatched(shipB); return; }
            if (shipB == null) { OnFrontShipDispatched(shipA); return; }

            int colA = -1;
            int colB = -1;

            int idxA = m_WaitingShips != null ? m_WaitingShips.IndexOf(shipA) : -1;
            if (idxA >= 0) colA = idxA % m_Columns;
            else if (shipA.transform.parent != null)
            {
                int spotIdx = m_QueueSpots.IndexOf(shipA.transform.parent);
                if (spotIdx >= 0) colA = spotIdx % m_Columns;
            }

            int idxB = m_WaitingShips != null ? m_WaitingShips.IndexOf(shipB) : -1;
            if (idxB >= 0) colB = idxB % m_Columns;
            else if (shipB.transform.parent != null)
            {
                int spotIdx = m_QueueSpots.IndexOf(shipB.transform.parent);
                if (spotIdx >= 0) colB = spotIdx % m_Columns;
            }

            if (colA >= 0 && colB >= 0 && colA == colB)
            {
                // Aynı sütundalar (alt alta): iki gemiyi birden sütundan temizleyip arkadaki tüm sıraları öne kaydır
                ShiftColumnForward(colA, shipA, shipB);
            }
            else
            {
                // Farklı sütunlardalar: Her birini kendi sütununda dispatch et
                if (colA >= 0) ShiftColumnForward(colA, shipA);
                if (colB >= 0) ShiftColumnForward(colB, shipB);
            }
        }

        /// <summary>
        /// Ön sıradan bir gemi slota gönderildiğinde çağrılır.
        /// Aynı sütundaki arka gemiler öne kayar ve arkaya denizden yeni gemi gelir.
        /// </summary>
        public void OnFrontShipDispatched(ShipController frontShip)
        {
            if (frontShip == null) return;
            int col = -1;
            int frontIndex = m_WaitingShips != null ? m_WaitingShips.IndexOf(frontShip) : -1;
            if (frontIndex >= 0)
            {
                col = frontIndex % m_Columns;
            }
            else if (frontShip.transform.parent != null)
            {
                int spotIdx = m_QueueSpots.IndexOf(frontShip.transform.parent);
                if (spotIdx >= 0) col = spotIdx % m_Columns;
            }

            if (col >= 0)
            {
                ShiftColumnForward(col, frontShip);
            }
        }

        /// <summary>
        /// Belirtilen sütundaki gemileri kaldırılan gemiler hariç Row 0'dan itibaren boşluksuz olarak
        /// öne toplar ve arkada boşalan sıralara açık denizden yeni gemileri sırayla getirir.
        /// </summary>
        private void ShiftColumnForward(int col, params ShipController[] removedShips)
        {
            if (col < 0 || col >= m_Columns) return;

            HashSet<ShipController> removedSet = new HashSet<ShipController>();
            if (removedShips != null)
            {
                for (int i = 0; i < removedShips.Length; i++)
                {
                    if (removedShips[i] != null)
                    {
                        removedSet.Add(removedShips[i]);
                        RemoveShipFromQueue(removedShips[i]);
                    }
                }
            }

            // 1. Bu sütundaki tüm mevcut geçerli gemileri Row 0'dan Row m_Rows-1'e kadar sırayla topla
            List<ShipController> remaining = new List<ShipController>();
            for (int r = 0; r < m_Rows; r++)
            {
                int idx = r * m_Columns + col;
                ShipController s = idx < m_WaitingShips.Count ? m_WaitingShips[idx] : null;
                if (s == null && idx < m_QueueSpots.Count && m_QueueSpots[idx] != null)
                {
                    for (int c = 0; c < m_QueueSpots[idx].childCount; c++)
                    {
                        var childShip = m_QueueSpots[idx].GetChild(c).GetComponent<ShipController>();
                        if (childShip != null && !removedSet.Contains(childShip) && !childShip.IsDocked && !childShip.IsDeparting && childShip.gameObject.activeInHierarchy)
                        {
                            s = childShip;
                            break;
                        }
                    }
                }

                if (s != null)
                {
                    if (removedSet.Contains(s) || s.IsDocked || s.IsDeparting)
                    {
                        if (idx < m_WaitingShips.Count) m_WaitingShips[idx] = null;
                    }
                    else if (s.gameObject.activeInHierarchy)
                    {
                        remaining.Add(s);
                        if (idx < m_WaitingShips.Count) m_WaitingShips[idx] = null;
                    }
                    else
                    {
                        if (idx < m_WaitingShips.Count) m_WaitingShips[idx] = null;
                    }
                }
            }

            // 2. Kalan gemileri en ön sıralara (Row 0, 1, 2...) sırasıyla yerleştir ve kaydır
            float baseDelay = 0.08f;
            for (int r = 0; r < remaining.Count; r++)
            {
                int targetIdx = r * m_Columns + col;
                ShipController ship = remaining[r];
                m_WaitingShips[targetIdx] = ship;
                Transform targetSpot = m_QueueSpots[targetIdx];

                if (ship.transform.parent != targetSpot)
                {
                    if (gameObject.activeInHierarchy)
                    {
                        StartCoroutine(MoveBackShipToFrontSpot(ship, targetSpot, baseDelay * (r + 1)));
                    }
                    else
                    {
                        ship.transform.SetParent(targetSpot, false);
                        ship.transform.localPosition = Vector3.zero;
                        ship.transform.localRotation = Quaternion.identity;
                    }
                }
            }

            // 3. Kalan boş sıraları m_WaitingShips içinde null olarak temizle
            for (int r = remaining.Count; r < m_Rows; r++)
            {
                int targetIdx = r * m_Columns + col;
                if (targetIdx < m_WaitingShips.Count)
                {
                    m_WaitingShips[targetIdx] = null;
                }
            }

            // 4. Boşalan arka sıralara açık denizden yeni gemileri sırayla getir
            if (gameObject.activeInHierarchy)
            {
                float spawnDelay = 0.14f;
                for (int r = remaining.Count; r < m_Rows; r++)
                {
                    int targetIdx = r * m_Columns + col;
                    SpawnAndSailInNewShip(targetIdx, spawnDelay);
                    spawnDelay += 0.12f;
                }
            }
        }

        /// <summary>
        /// Arka sıradaki gemiyi, öndeki gemi slota doğru yola çıkıp ön spottan uzaklaşması için
        /// kısa bir gecikmenin ardından ön spota kaydırır.
        /// </summary>
        private IEnumerator MoveBackShipToFrontSpot(ShipController backShip, Transform frontSpot, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (backShip == null || frontSpot == null) yield break;
            if (backShip.IsDocked || backShip.IsDeparting) yield break;

            // Beklerken kuyruk yeniden kaydırıldıysa (sütundan art arda gemi gönderildi) bu emir bayattır:
            // gemiyi eski hedefine taşırsa liste "önde" derken gemi ortada kalıyordu.
            int targetIdx = m_QueueSpots.IndexOf(frontSpot);
            if (targetIdx < 0 || targetIdx >= m_WaitingShips.Count || m_WaitingShips[targetIdx] != backShip) yield break;

            // Spot altında başka aktif gemi kalmışsa (eski ebeveyn kalıntısı vs.) ayır
            for (int i = frontSpot.childCount - 1; i >= 0; i--)
            {
                Transform child = frontSpot.GetChild(i);
                if (child != backShip.transform)
                {
                    var otherShip = child.GetComponent<ShipController>();
                    if (otherShip != null)
                    {
                        if (otherShip.IsDeparting || otherShip.IsDocked)
                        {
                            otherShip.transform.SetParent(null, true);
                        }
                    }
                }
            }

            backShip.transform.DOKill(false);
            backShip.transform.SetParent(frontSpot, true);

            // Su sallanması (bobbing) animasyon süresince susturulur
            backShip.SetQueueAnimating(true);

            // Su üzerinde öne doğru süzülme animasyonu
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
                    if (backShip != null && !backShip.IsDeparting && !backShip.IsDocked && backShip.transform.parent == frontSpot)
                    {
                        backShip.transform.localPosition = Vector3.zero;
                        backShip.transform.localRotation = Quaternion.identity;
                        backShip.transform.localScale = Vector3.one * m_ShipScale;
                        backShip.SetBaseScale(Vector3.one * m_ShipScale);
                        backShip.SetQueueAnimating(false);
                        RefreshLinkedShipTethers();
                    }
                })
                .OnKill(() =>
                {
                    if (backShip != null)
                    {
                        backShip.SetQueueAnimating(false);
                    }
                });
        }

        /// <summary>
        /// En arka spota denizden yeni gemi getirir. Gemi kuyruk listesine HEMEN yazılır, sadece süzülme
        /// animasyonu gecikmeli başlar. Eskiden liste kaydı da gecikmeliydi: bu sürede aynı sütundan bir gemi
        /// daha gönderilince ikinci yeni gemi aynı spota doğup birincinin kaydının üstüne yazıyordu; birinci
        /// gemi listeden düşüp ekran dışında sonsuza kadar kalıyor, küpleri toplanamadığı için bölüm bitmiyordu.
        /// </summary>
        private void SpawnAndSailInNewShip(int spotIndex, float delay)
        {
            if (spotIndex < 0 || spotIndex >= m_QueueSpots.Count) return;
            if (m_ShipPrefab == null) return;

            // Eğer özel sıra kullanılıyorsa ve sırada başka gemi kalmadıysa:
            if (m_UsingLevelSequence && LevelSequenceLanesExhausted())
            {
                int remainingCubes = ShipDispatcher.Instance != null ? ShipDispatcher.Instance.GetTotalRemainingCubes() : 0;
                int currentShipCapacity = GetTotalCapacityOfActiveShips();
                if (remainingCubes <= currentShipCapacity)
                {
                    // Seviyedeki tüm küpler mevcut gemilerce karşılanıyor, yeni gemiye gerek yok
                    return;
                }
            }

            Color shipColor;
            int capacity;
            int linkId;
            if (!TryGetNextSequenceShip(spotIndex, out shipColor, out capacity, out linkId))
            {
                // Bu sütunun şeridi bitti ama diğer şeritlerde hâlâ gemi var: sütun boş kalsın.
                // Eskiden burada "ihtiyaca göre" yedek gemi doğuyordu; şeritler eşit bölünmediği için
                // sıradaki gemiler zaten gelecekken fazladan gemi geliyordu (ör. 11 yerine 13-18 gemi).
                // Yedek gemi yalnızca tüm sıra bitip kapasite gerçekten yetmediğinde (yukarıdaki kontrol) doğar.
                if (m_UsingLevelSequence && !LevelSequenceLanesExhausted()) return;

                shipColor = GetNextNeededColor(false);
                capacity = GetRecommendedCapacity(shipColor);
                linkId = 0;
            }

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
            ship.SetBaseScale(Vector3.one * m_ShipScale);
            ship.SetLinkedPartner(null, linkId);

            ship.Configure(shipColor, capacity);
            ship.SetMysteryHidden(m_LastSequenceShipHidden);

            while (m_WaitingShips.Count <= spotIndex)
            {
                m_WaitingShips.Add(null);
            }
            m_WaitingShips[spotIndex] = ship;

            if (gameObject.activeInHierarchy) StartCoroutine(SailInNewShip(ship, delay));
            else ship.transform.localPosition = Vector3.zero;
        }

        private IEnumerator SailInNewShip(ShipController ship, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (ship == null) yield break;
            // Beklerken öne kaydırıldıysa (kendi tween'i var) ya da çoktan slota gönderildiyse dokunma
            if (ship.transform.parent == null || ship.IsMoving || ship.IsDocked || ship.IsDeparting) yield break;
            if (DOTween.IsTweening(ship.transform)) yield break;
            GameObject shipObj = ship.gameObject;

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
                        // m_ShipScale dünya ölçeği: spot ölçeğine bölünerek doğru yerel ölçek kurulur (yoksa gemi ~%35 büyük kalıyordu)
                        ship.SetBaseScale(Vector3.one * m_ShipScale);
                        ship.SetQueueAnimating(false);
                        RefreshLinkedShipTethers();
                    }
                })
                .OnKill(() =>
                {
                    if (ship != null)
                    {
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
