#pragma warning disable 0414
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// Gemi Sahnesi'nin (Gemi.unity) merkezi oyun yöneticisi (Gameplay Coordinator).
    /// Su slotlarını (ShipSlot), bekleme kuyruğunu (ShipQueuePool) ve küp patlama / kargo akışını koordine eder.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Ship Dispatcher")]
    public class ShipDispatcher : MonoBehaviour
    {
        private static ShipDispatcher s_Instance;
        public static ShipDispatcher Instance => s_Instance;

        [Header("⚓ Slotlar & Kuyruk")]
        [SerializeField] private List<ShipSlot> m_Slots = new List<ShipSlot>();

        /// <summary>Yanaşma slotları (salt okunur). Gemi rengi dağıtımında kullanılır.</summary>
        public IReadOnlyList<ShipSlot> Slots => m_Slots;
        [SerializeField] private ShipQueuePool m_QueuePool;

        [Header("🎨 Piksel Sanatı Bağlantısı")]
        [SerializeField] private PixelArtGenerator m_Generator;

        [Header("🚀 Kargo Treni & Sahilden Gemiye Zıplama Ayarları")]
        [Tooltip("Küp treninin kayma hızı (dünya birimi / sn). Referans videoda ~13 küp/sn akıyor (iki kol toplamı).")]
        [SerializeField] private float m_RopeSpeed = 2.4f;
        [Tooltip("Sahil kenarından gemiye zıplama süresi (sn).")]
        [SerializeField] private float m_HopDuration = 0.38f;
        [Tooltip("Sahil sonundan gemiye doğru zıplama yayının yüksekliği (parabolik zıplama tepe noktası).")]
        [SerializeField] private float m_HopArcHeight = 0.48f;
        [Tooltip("Kıyı noktasının dünya Y'si (ship == null durumunda yedek).")]
        [SerializeField] private float m_ShoreY = -2.88f;
        [Tooltip("Kıyı noktasının dünya Z'si (ship == null durumunda yedek).")]
        [SerializeField] private float m_ShoreZ = 0.07f;
        [Tooltip("Kumun suya değdiği kıyı çizgisi (dünya X,Y). Küpler bu çizgide, geminin en yakın noktasına kadar yürür ve oradan gemiye zıplar. Boşsa geminin hemen önü kullanılır.")]
        [SerializeField] private List<Vector2> m_Shoreline = new List<Vector2>();
        [Tooltip("Kıyı noktasının kum tarafına (geminin tersine) ne kadar içeride olacağı (dünya birimi).")]
        [SerializeField] private float m_ShorelineInset = 0.15f;
        [Tooltip("Boşsa panodaki küpün kendisi yürür. Bir prefab atanırsa (ör. Mixamo koşucusu MainCube_Running) küp yerinde gizlenir, yerine bu prefab küpün renginde yürür.")]
        [SerializeField] private GameObject m_CargoStandInPrefab;

        [Header("🪙 Altın Ödülleri")]
        [Tooltip("Bir gemi tam dolunca kazanılan altın.")]
        [SerializeField] private int m_CoinsPerFullShip = 1;

        /// <summary>Gemi tam dolduğunda çağrılır (ödül).</summary>
        public void OnShipFilled(ShipController ship)
        {
            if (CasualHudController.Instance != null) CasualHudController.Instance.AddCoins(m_CoinsPerFullShip);
        }
        [Tooltip("Resim tamamlanınca kazanılan altın (zor seviyede iki katı).")]
        [SerializeField] private int m_LevelCompleteCoins = 20;

        private bool m_LevelEndPending = false;
        private int m_ShipsAwaitingDeparture = 0;
        private int m_ActiveCargoFlightCount = 0;

        [Header("⚡ Otomatik Yerleştirme & 2X Turbo")]
        [SerializeField] private bool m_EnableAutoPlaceAndTurbo = true;
        private bool m_IsAutoPlacing = false;
        private bool m_IsTurboActive = false;
        private float m_AutoPlaceCheckTimer = 0f;

        [Header("❌ Seviye Başarısızlık (Deadlock)")]
        [SerializeField] private bool m_EnableFailOnDeadlock = true;
        [SerializeField] private float m_DeadlockGraceDuration = 1.0f;
        private bool m_IsLevelFailed = false;
        private float m_DeadlockTimer = 0f;
        private float m_DeadlockCheckIntervalTimer = 0f;
        private bool m_CachedDeadlockCondition = false;

        public bool IsAutoPlacing => m_IsAutoPlacing;
        public bool IsTurboActive => m_IsTurboActive;
        public bool IsLevelFailed => m_IsLevelFailed;

        // Sol ve sağ kolun kıyıdaki giriş noktaları arası yarım mesafe; küp boyundan (~0.23) biraz dar
        // tutularak iki kolun yan yana muntazam koridor oluşturması sağlanır.
        private const float ShoreSideOffset = 0.11f;

        /// <summary>Gemi başına kıyıya son varış zamanları — yeni tren eskisinin kuyruğuna binmesin.</summary>
        private class ShipRopeGate
        {
            public float LastArrivalLeft;
            public float LastArrivalRight;
        }

        private readonly Dictionary<ShipController, ShipRopeGate> m_RopeGates = new Dictionary<ShipController, ShipRopeGate>();

        // Izgara → dünya dönüşümü (seviye başına bir kez kurulur)
        private CubeGridFrame m_GridFrame;
        private bool m_GridFrameValid = false;

        // Pano / ana görsel sınırları: oyun boyunca küpler patlasa dahi asla içe küçülmez;
        // küplerin her zaman ana görselin dışındaki güvenli flank koridorundan inmesini garanti eder.
        private bool m_BoardBoundsInitialized = false;
        private float m_BoardMinX = -2.35f;
        private float m_BoardMaxX = 2.45f;
        private float m_BoardBottomY = 1.80f;
        private float m_BoardTopY = 5.85f;

        public void EnsureBoardBounds(bool forceRefresh = false)
        {
            if (m_BoardBoundsInitialized && !forceRefresh) return;

            if (m_Generator == null) m_Generator = Object.FindFirstObjectByType<PixelArtGenerator>();
            PixelCube[] allCubes = null;
            if (m_Generator != null && m_Generator.CubesContainer != null)
            {
                allCubes = m_Generator.CubesContainer.GetComponentsInChildren<PixelCube>(true);
            }
            if (allCubes == null || allCubes.Length == 0)
            {
                allCubes = Object.FindObjectsByType<PixelCube>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            }

            if (allCubes != null && allCubes.Length > 0)
            {
                float mnX = float.MaxValue, mxX = float.MinValue;
                float mnY = float.MaxValue, mxY = float.MinValue;
                foreach (var c in allCubes)
                {
                    if (c == null) continue;
                    var p = c.transform.position;
                    mnX = Mathf.Min(mnX, p.x);
                    mxX = Mathf.Max(mxX, p.x);
                    mnY = Mathf.Min(mnY, p.y);
                    mxY = Mathf.Max(mxY, p.y);
                }
                if (mnX < float.MaxValue)
                {
                    m_BoardMinX = mnX;
                    m_BoardMaxX = mxX;
                    m_BoardBottomY = mnY;
                    m_BoardTopY = mxY;
                    m_BoardBoundsInitialized = true;
                }
            }
        }

        private void OnLevelLoaded(PixelLevelData data)
        {
            m_IsLevelFailed = false;
            m_DeadlockTimer = 0f;
            m_DeadlockCheckIntervalTimer = 0f;
            m_CachedDeadlockCondition = false;
            SetTurboSpeed(false);
            m_IsAutoPlacing = false;
            m_BoardBoundsInitialized = false;
            m_RopeGates.Clear();
            m_GridFrameValid = false;
            EnsureBoardBounds(forceRefresh: true);
            EnsureReferences();
            if (m_QueuePool != null)
            {
                m_QueuePool.InitializeQueue();
            }
        }

        private void Awake()
        {
            s_Instance = this;
            SanitizeSettings();
            EnsureReferences();
            EnsureBoardBounds(forceRefresh: true);
        }

        private void OnValidate()
        {
            SanitizeSettings();
        }

        private void SanitizeSettings()
        {
            if (m_HopArcHeight < 0.20f) m_HopArcHeight = 0.48f;
            if (m_HopDuration < 0.20f) m_HopDuration = 0.38f;
            if (m_RopeSpeed < 1.8f) m_RopeSpeed = 2.4f;
        }

        private void OnEnable()
        {
            s_Instance = this;
            EnsureReferences();
            EnsureBoardBounds();
            PixelArtGenerator.LevelLoaded -= OnLevelLoaded;
            PixelArtGenerator.LevelLoaded += OnLevelLoaded;
        }

        private void OnDisable()
        {
            PixelArtGenerator.LevelLoaded -= OnLevelLoaded;
            SetTurboSpeed(false);
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            // 1. Deadlock & Seviye Başarısızlık Kontrolü (tüm slotlar dolup hamle kalmadığında)
            UpdateDeadlockCheck();

            // 2. Otomatik Yerleştirme & 2X Turbo Kontrolü
            if (!m_IsAutoPlacing && !m_LevelEndPending && !m_IsLevelFailed && m_EnableAutoPlaceAndTurbo)
            {
                m_AutoPlaceCheckTimer += Time.unscaledDeltaTime;
                if (m_AutoPlaceCheckTimer >= 0.25f)
                {
                    m_AutoPlaceCheckTimer = 0f;
                    CheckAutoPlaceRemainingShips();
                }
            }
        }

        private void Start()
        {
            EnsureReferences();
            if (m_QueuePool != null)
            {
                m_QueuePool.InitializeQueue();
            }
        }

        public void EnsureReferences()
        {
            if (m_Generator == null)
            {
                m_Generator = UnityEngine.Object.FindFirstObjectByType<PixelArtGenerator>();
            }

            PixelLevelData level = m_Generator != null ? m_Generator.ActiveLevelData : null;
            int targetSlots = (level != null && level.SlotCount > 0) ? level.SlotCount : 5;

            var marina = UnityEngine.Object.FindFirstObjectByType<MarinaSlotLayout>();
            if (marina != null)
            {
                marina.SetSlotCount(targetSlots);
            }

            // Sadece aktif olan slotları sıralı olarak al
            m_Slots.Clear();
            var allSlots = ShipSlot.ActiveSlots;
            for (int i = 0; i < allSlots.Count; i++)
            {
                var s = allSlots[i];
                if (s != null && s.gameObject.activeInHierarchy)
                {
                    m_Slots.Add(s);
                }
            }
            m_Slots.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));

            if (m_QueuePool == null)
            {
                m_QueuePool = UnityEngine.Object.FindFirstObjectByType<ShipQueuePool>();
            }
        }

        // Aktif bölümün paleti: her renk varyantı (orijinal, hedef, ayarlı hedef, gemi rengi) → palet indeksi.
        // Renk eşleşmesi bu indeks üzerinden yapılır; sabit tolerans koyu yeşili laciverde, açık maviyi
        // koyu maviye bağlıyordu (ve eşleşme geçişsiz olduğu için gemiler yanlış küpleri topluyordu).
        private static readonly List<Color> s_PaletteColors = new List<Color>();
        private static readonly List<int> s_PaletteIndices = new List<int>();
        private const float PaletteSnapMaxSqrDistance = 0.12f;
        private static int s_YellowPaletteEntryCount;

        private static bool IsYellowTone(Color c) => c.r > 0.75f && c.g > 0.50f && c.b < 0.35f;

        public static PixelLevelData ActivePaletteLevel { get; private set; }

        /// <summary>
        /// ColorsMatch'in kullanacağı paleti ayarlar. null verilirse eski toleranslı karşılaştırmaya döner.
        /// </summary>
        public static void SetActivePalette(PixelLevelData level)
        {
            ActivePaletteLevel = level;
            s_PaletteColors.Clear();
            s_PaletteIndices.Clear();
            s_YellowPaletteEntryCount = 0;
            if (level == null || level.ColorPalette == null) return;

            foreach (var entry in level.ColorPalette)
            {
                if (entry == null) continue;
                Color adjusted = PixelCube.AdjustColor(entry.targetColor, level.ColorBrightness, level.ColorSaturation, level.ColorContrast);
                if (IsYellowTone(entry.targetColor) || IsYellowTone(adjusted)) s_YellowPaletteEntryCount++;
            }

            for (int i = 0; i < level.ColorPalette.Count; i++)
            {
                var entry = level.ColorPalette[i];
                if (entry == null) continue;
                Color adjusted = PixelCube.AdjustColor(entry.targetColor, level.ColorBrightness, level.ColorSaturation, level.ColorContrast);
                AddPaletteVariant(entry.originalColor, i);
                AddPaletteVariant(entry.targetColor, i);
                AddPaletteVariant(adjusted, i);
                AddPaletteVariant(NormalizeShipColor(entry.targetColor), i);
                AddPaletteVariant(NormalizeShipColor(adjusted), i);
            }
        }

        private static void AddPaletteVariant(Color c, int index)
        {
            s_PaletteColors.Add(c);
            s_PaletteIndices.Add(index);
        }

        /// <summary>Rengi en yakın palet girdisine oturtur; palet yoksa veya renk hiçbirine yakın değilse -1.</summary>
        public static int GetPaletteIndex(Color c)
        {
            int bestIndex = -1;
            float bestDist = PaletteSnapMaxSqrDistance;
            for (int i = 0; i < s_PaletteColors.Count; i++)
            {
                Color p = s_PaletteColors[i];
                float dr = p.r - c.r, dg = p.g - c.g, db = p.b - c.b;
                float d = dr * dr + dg * dg + db * db;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestIndex = s_PaletteIndices[i];
                }
            }
            return bestIndex;
        }

        /// <summary>
        /// İki rengin aynı oyun rengi olup olmadığını söyler. Aktif palet varsa iki renk de en yakın palet
        /// girdisine oturtulup indeksleri karşılaştırılır; yoksa toleranslı RGB karşılaştırmasına düşer.
        /// </summary>
        public static bool ColorsMatch(Color a, Color b)
        {
            if (s_PaletteColors.Count > 0)
            {
                int ia = GetPaletteIndex(a);
                int ib = GetPaletteIndex(b);
                if (ia >= 0 && ib >= 0) return ia == ib;
            }

            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;
            if ((dr * dr + dg * dg + db * db) < 0.12f) return true;

            // Sarı / Amber tonları için özel tolerans (küp ve gemi her koşulda %100 eşleşir):
            if (IsYellowTone(a) && IsYellowTone(b)) return true;

            return false;
        }

        private static readonly HashSet<PixelCube> s_ReservedCubes = new HashSet<PixelCube>();
        private readonly HashSet<ShipController> m_ActiveExtractingShips = new HashSet<ShipController>();

        /// <summary>
        /// Izgara dışındaki tüm boş alanlardan (dış hava) BFS başlatarak dış havayı bulur.
        /// </summary>
        public static HashSet<(int, int)> CalculateOutsideAir(Dictionary<(int, int), PixelCube> gridMap, int minX, int maxX, int minY, int maxY)
        {
            HashSet<(int, int)> outsideAir = new HashSet<(int, int)>();
            if (gridMap == null || gridMap.Count == 0) return outsideAir;

            int boundMinX = minX - 1;
            int boundMaxX = maxX + 1;
            int boundMinY = minY - 1;
            int boundMaxY = maxY + 1;

            Queue<(int, int)> airQueue = new Queue<(int, int)>();

            for (int x = boundMinX; x <= boundMaxX; x++)
            {
                airQueue.Enqueue((x, boundMinY));
                outsideAir.Add((x, boundMinY));
                airQueue.Enqueue((x, boundMaxY));
                outsideAir.Add((x, boundMaxY));
            }
            for (int y = boundMinY + 1; y < boundMaxY; y++)
            {
                airQueue.Enqueue((boundMinX, y));
                outsideAir.Add((boundMinX, y));
                airQueue.Enqueue((boundMaxX, y));
                outsideAir.Add((boundMaxX, y));
            }

            int[] dx = { -1, 1, 0, 0 };
            int[] dy = { 0, 0, -1, 1 };

            while (airQueue.Count > 0)
            {
                var (cx, cy) = airQueue.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    int nx = cx + dx[i];
                    int ny = cy + dy[i];

                    if (nx >= boundMinX && nx <= boundMaxX && ny >= boundMinY && ny <= boundMaxY)
                    {
                        if (!outsideAir.Contains((nx, ny)))
                        {
                            if (!gridMap.ContainsKey((nx, ny)))
                            {
                                outsideAir.Add((nx, ny));
                                airQueue.Enqueue((nx, ny));
                            }
                        }
                    }
                }
            }

            return outsideAir;
        }

        /// <summary>
        /// Verilen renkle eşleşen, henüz patlatılmamış/rezerve edilmemiş ve DIŞTA olan (dış havaya temas eden) küpleri döner.
        /// </summary>
        public List<PixelCube> GetExposedMatchingCubes(Color shipColor)
        {
            List<PixelCube> result = new List<PixelCube>();

            if (m_Generator == null) m_Generator = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (m_Generator == null || m_Generator.CubesContainer == null) return result;

            var allCubes = PixelCube.ActiveCubes;
            if (allCubes == null || allCubes.Count == 0) return result;

            Dictionary<(int, int), PixelCube> gridMap = new Dictionary<(int, int), PixelCube>(allCubes.Count);
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            for (int i = 0; i < allCubes.Count; i++)
            {
                PixelCube c = allCubes[i];
                if (c != null && !c.IsPopped && c.gameObject.activeSelf)
                {
                    gridMap[(c.GridX, c.GridY)] = c;
                    if (c.GridX < minX) minX = c.GridX;
                    if (c.GridX > maxX) maxX = c.GridX;
                    if (c.GridY < minY) minY = c.GridY;
                    if (c.GridY > maxY) maxY = c.GridY;
                }
            }

            if (gridMap.Count == 0) return result;

            HashSet<(int, int)> outsideAir = CalculateOutsideAir(gridMap, minX, maxX, minY, maxY);

            foreach (var kvp in gridMap)
            {
                PixelCube cube = kvp.Value;
                if (cube == null || s_ReservedCubes.Contains(cube)) continue;

                if (ColorsMatch(cube.CurrentColor, shipColor) || ColorsMatch(cube.OriginalColor, shipColor))
                {
                    int x = cube.GridX;
                    int y = cube.GridY;
                    bool touchesAir = outsideAir.Contains((x - 1, y)) ||
                                      outsideAir.Contains((x + 1, y)) ||
                                      outsideAir.Contains((x, y - 1)) ||
                                      outsideAir.Contains((x, y + 1));

                    if (touchesAir)
                    {
                        result.Add(cube);
                    }
                }
            }

            return result;
        }

        public bool HasExposedMatchingCube(Color shipColor)
        {
            var list = GetExposedMatchingCubes(shipColor);
            return list != null && list.Count > 0;
        }

        /// <summary>
        /// Gemi bir slota yanaştığında çağrılır.
        /// Sadece dıştaki küpleri toplar; dışta küp yoksa açılana kadar slotta bekler.
        /// </summary>
        public void OnShipDocked(ShipController ship)
        {
            if (ship == null || ship.IsDeparting) return;
            StartCoroutine(ExtractMatchingCubesToShipRoutine(ship));
            CheckAutoPlaceRemainingShips();
        }

        /// <summary>
        /// Bir küp patladığında veya gemi yanaştığında, slotlarda bekleyen dolmamış diğer gemilerin
        /// önüne yeni açılan dış küp gelip gelmediğini kontrol eder ve toplamayı başlatır.
        /// </summary>
        public void TriggerWaitingShipsCheck()
        {
            if (m_Slots == null || m_Slots.Count == 0) return;

            foreach (var slot in m_Slots)
            {
                if (slot != null && !slot.IsEmpty && slot.DockedShip != null)
                {
                    ShipController ship = slot.DockedShip;
                    // Slot gemiye yola çıkarken ayrılır; tren ancak gemi slota oturunca başlasın
                    if (ship != null && ship.IsDocked && !ship.IsMoving && ship.CanAcceptMore && !m_ActiveExtractingShips.Contains(ship))
                    {
                        if (HasExposedMatchingCube(ship.ShipColor))
                        {
                            StartCoroutine(ExtractMatchingCubesToShipRoutine(ship));
                        }
                    }
                }
            }
        }

        private IEnumerator ExtractMatchingCubesToShipRoutine(ShipController ship)
        {
            if (ship == null || ship.IsDeparting) yield break;
            if (!ship.IsDocked || ship.IsMoving) yield break;
            if (m_ActiveExtractingShips.Contains(ship)) yield break;

            m_ActiveExtractingShips.Add(ship);

            try
            {
                while (ship != null && ship.CanAcceptMore)
                {
                    if (!TryLaunchRope(ship, out float boardClearTime))
                    {
                        // Dışta bu renkten küp yok!
                        // Seviyede bu renkten hala içeride (ortada) kilitli küp var mı kontrol et
                        int totalRemaining = GetRemainingCountForColor(ship.ShipColor);
                        if (totalRemaining == 0)
                        {
                            // Seviyedeki bu renge ait TÜM küpler zaten toplanmış, gemi daha fazla küp alamaz -> Kalkış yap
                            yield return new WaitForSeconds(0.35f);
                            // Yolda hâlâ küp varsa kalkma: gemi hareket edince yoldaki
                            // küpler onu harita dışına kadar kovalıyor ve AddCargo
                            // (IsDeparting yüzünden) onları saymadan düşürüyordu.
                            while (ship != null && ship.HasPendingCargo) yield return null;
                            if (ship != null && !ship.IsDeparting)
                            {
                                ship.DepartAndFreeSlot();
                            }
                        }
                        // else: içeride hâlâ bu renkten küp var ama şu an dışları kapalı —
                        // gemi slotta bekler, açılınca TriggerWaitingShipsCheck yeniden başlatır.
                        break;
                    }

                    // Bir sonraki tren ancak bu trenin kuyruğu panodan çıkınca kurulur;
                    // yoksa yeni trenin yolu, hâlâ panoda kayan küplerin içinden geçebilirdi.
                    while (ship != null && Time.time < boardClearTime) yield return null;

                    // Tren panodan ayrılınca içerideki küpler dışarı açılmış olabilir!
                    TriggerWaitingShipsCheck();
                }

                // Gemi dolduysa kalkış yap — ama önce yoldaki son küpler insin.
                if (ship != null && !ship.IsDeparting)
                {
                    while (ship != null && ship.HasPendingCargo) yield return null;

                    if (ship != null && ship.IsFull && !ship.IsDeparting)
                    {
                        yield return new WaitForSeconds(0.2f);
                        if (ship != null && !ship.IsDeparting)
                        {
                            ship.DepartAndFreeSlot();
                        }
                    }
                }
            }
            finally
            {
                if (ship != null)
                {
                    m_ActiveExtractingShips.Remove(ship);
                }
            }

            // Çekim bittikten sonra da genel kontrol yap
            TriggerWaitingShipsCheck();
        }

        /// <summary>
        /// Gemi için dış kenardaki eşleşen küplerden iki kollu bir tren kurar ve yola çıkarır.
        /// Dışta uygun küp yoksa false döner. <paramref name="boardClearTime"/>, trenin kuyruğunun
        /// panodan çıkacağı andır.
        /// </summary>
        private bool TryLaunchRope(ShipController ship, out float boardClearTime)
        {
            boardClearTime = Time.time;
            if (ship == null || !ship.CanAcceptMore) return false;
            if (!TryBuildLiveGrid(out var gridMap, out var outsideAir, out int minY)) return false;
            if (!EnsureGridFrame()) return false;

            // 1. Bu geminin rengiyle eşleşen ve henüz rezerve edilmemiş TÜM canlı küpleri topla
            var matchingCubes = new List<PixelCube>();
            bool hasExposedCube = false;
            foreach (var kvp in gridMap)
            {
                PixelCube cube = kvp.Value;
                if (s_ReservedCubes.Contains(cube)) continue;
                if (!ColorsMatch(cube.CurrentColor, ship.ShipColor) && !ColorsMatch(cube.OriginalColor, ship.ShipColor)) continue;

                matchingCubes.Add(cube);

                if (!hasExposedCube)
                {
                    int x = cube.GridX, y = cube.GridY;
                    if (outsideAir.Contains((x - 1, y)) || outsideAir.Contains((x + 1, y)) ||
                        outsideAir.Contains((x, y - 1)) || outsideAir.Contains((x, y + 1)))
                    {
                        hasExposedCube = true;
                    }
                }
            }

            // Eğer bu renkten küp yoksa veya henüz dışarıya açılmamışsa bekle
            if (matchingCubes.Count == 0 || !hasExposedCube) return false;

            // 2. Kullanıcı isteği: "gemiler slotlara yerleştiği anda kaç adet yazıyorsa üstünde o kadar sayı kadar piksel art küpleri harekete geçsin sonradan falan geçmesin aynı anda olsun"
            // Geminin üstünde yazan kalan kapasite kadar (veya tahtada o renkten kalan tüm küpler) küpü tek seferde rezerve et
            int needed = ship.RemainingCapacity;
            int targetCount = Mathf.Min(needed, matchingCubes.Count);
            if (targetCount <= 0) return false;

            int reserved = 0;
            while (reserved < targetCount && ship.TryReserveCargo()) reserved++;
            if (reserved == 0) return false;

            var leftArm = new List<PixelCube>();
            var rightArm = new List<PixelCube>();
            CargoRopeBuilder.BuildArms(matchingCubes, outsideAir, m_GridFrame, GetShorePoint(ship), reserved, leftArm, rightArm);

            int used = leftArm.Count + rightArm.Count;
            for (int i = used; i < reserved; i++) ship.ReleaseCargoReservation();
            if (used == 0) return false;

            // Dış havadan panonun altına iniş yolları. İki kolun ortak hücrelerinde yollar yarım
            // hücre sola/sağa kaydırılır ki iki tren aynı koridorda iç içe değil yan yana aksın.
            int bottomRowY = minY - 1;
            var leftRoute = CargoRopeBuilder.FindExitRoute((leftArm[0].GridX, leftArm[0].GridY), outsideAir, bottomRowY);
            var rightRoute = rightArm.Count > 0
                ? CargoRopeBuilder.FindExitRoute((rightArm[0].GridX, rightArm[0].GridY), outsideAir, bottomRowY)
                : new List<(int, int)>();
            var shared = new HashSet<(int, int)>(leftRoute);
            shared.IntersectWith(rightRoute);

            var leftExit = CargoRopeBuilder.SimplifyRoute((leftArm[0].GridX, leftArm[0].GridY), leftRoute, outsideAir);
            var rightExit = rightArm.Count > 0
                ? CargoRopeBuilder.SimplifyRoute((rightArm[0].GridX, rightArm[0].GridY), rightRoute, outsideAir)
                : null;

            // Panodan kıyıya iniş: iki kol gidiş yönüne DİK olarak ayrılır. Yatay ayrım yetmiyordu —
            // kollar çapraz inerken aradaki dik mesafe küp boyunun altına düşüp üst üste biniyorlardı.
            Vector3 shoreCenter = GetShorePoint(ship);
            Vector3 leftOut = ExitWorldPoint(leftArm[0], leftExit);
            Vector3 exitCenter = rightExit != null ? (leftOut + ExitWorldPoint(rightArm[0], rightExit)) * 0.5f : leftOut;
            Vector2 dir = new Vector2(shoreCenter.x - exitCenter.x, shoreCenter.y - exitCenter.y);
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.down;
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
            if (perp.x < 0f) perp = -perp; // her zaman sağı göstersin: sol kol eksi tarafta kalır

            float halfPitch = m_GridFrame.Pitch * 0.5f;
            var ropes = new List<CargoRope>(2)
            {
                CargoRopeBuilder.Build(leftArm, leftExit, shared, -halfPitch, m_GridFrame,
                    BuildApproach(leftOut, shoreCenter, perp * -ShoreSideOffset), true)
            };
            if (rightExit != null)
            {
                ropes.Add(CargoRopeBuilder.Build(rightArm, rightExit, shared, halfPitch, m_GridFrame,
                    BuildApproach(ExitWorldPoint(rightArm[0], rightExit), shoreCenter, perp * ShoreSideOffset), false));
            }

            // İki kol aynı anda kalkar; ama kıyıda bir önceki trenin kuyruğuna binmesin diye
            // gerekirse kalkış biraz ertelenir.
            if (!m_RopeGates.TryGetValue(ship, out ShipRopeGate gate))
            {
                gate = new ShipRopeGate();
                m_RopeGates[ship] = gate;
            }

            float speed = Mathf.Max(0.05f, m_RopeSpeed);
            float arrivalGap = m_GridFrame.Pitch / speed;
            float startTime = Time.time;
            foreach (var rope in ropes)
            {
                float lastArrival = rope.EntersLeft ? gate.LastArrivalLeft : gate.LastArrivalRight;
                float headTravel = (rope.Path.Length - rope.HeadDistance) / speed;
                startTime = Mathf.Max(startTime, lastArrival + arrivalGap - headTravel);
            }

            boardClearTime = startTime;
            foreach (var rope in ropes)
            {
                rope.ShoreAtLaunch = shoreCenter;
                float tailArrival = startTime + (rope.Path.Length - rope.TailDistance) / speed;
                if (rope.EntersLeft) gate.LastArrivalLeft = tailArrival; else gate.LastArrivalRight = tailArrival;

                float tailClear = startTime + Mathf.Max(0f, rope.BoardExitDistance - rope.TailDistance) / speed;
                boardClearTime = Mathf.Max(boardClearTime, tailClear);

                foreach (var cube in rope.Cubes) s_ReservedCubes.Add(cube);
            }

            // Kalkış anına kadar küpler panoda durur ama başka trene alınamaz (rezerve).
            // Seviye bitiş kontrolü yoldaki her küp gemiye inene kadar beklesin.
            m_ActiveCargoFlightCount += used;
            StartCoroutine(RunRopes(ropes, ship, startTime));
            return true;
        }

        /// <summary>
        /// Trenleri kalkış anında panodan koparır ve hepsini aynı hızda kıyıya kaydırır.
        /// Yolun sonuna varan küp sırayla gemiye zıplar.
        /// </summary>
        private IEnumerator RunRopes(List<CargoRope> ropes, ShipController ship, float startTime)
        {
            while (Time.time < startTime) yield return null;

            if (ship == null || ship.IsDeparting)
            {
                // Gemi kalkış beklerken gitti: ayrılan yerleri geri ver, küpler panoda kalsın.
                foreach (var rope in ropes)
                {
                    foreach (var cube in rope.Cubes)
                    {
                        if (ship != null) ship.ReleaseCargoReservation();
                        s_ReservedCubes.Remove(cube);
                        m_ActiveCargoFlightCount = Mathf.Max(0, m_ActiveCargoFlightCount - 1);
                    }
                }
                CheckWinCondition();
                TriggerWaitingShipsCheck();
                yield break;
            }

            // Tüm küpler aynı anda panodan ayrılır; kontur gölgesi bir kez yenilenir.
            // Bacaklı küp kendisi yürür; bacaksız bir küp prefab'ı kullanılıyorsa yerinde
            // gizlenir ve yerine kargo görseli yürür.
            var riders = new List<(CargoRope rope, int index, GameObject cargo)>();
            foreach (var rope in ropes)
            {
                for (int k = 0; k < rope.Cubes.Count; k++)
                {
                    PixelCube cube = rope.Cubes[k];
                    GameObject cargo;
                    ICargoRunner selfRunner = cube.GetComponent<ICargoRunner>();
                    if (m_CargoStandInPrefab != null)
                    {
                        cargo = CreateStandInCargo(cube, k);
                        cube.SetPoppedVisualState(true, regenerateContourShadow: false);
                    }
                    else if (selfRunner != null)
                    {
                        cube.BeginLeaving(regenerateContourShadow: false);
                        selfRunner.BeginWalk(k);
                        cargo = cube.gameObject;
                    }
                    else
                    {
                        cargo = CreateRopeCargo(cube);
                        cube.SetPoppedVisualState(true, regenerateContourShadow: false);
                    }
                    riders.Add((rope, k, cargo));
                }
            }
            if (m_Generator != null) m_Generator.RegenerateContourShadowFromLiveCubeState();

            var arrived = new bool[riders.Count];
            var runners = new ICargoRunner[riders.Count];
            for (int i = 0; i < riders.Count; i++)
            {
                runners[i] = riders[i].cargo != null ? riders[i].cargo.GetComponent<ICargoRunner>() : null;
            }
            int onPath = riders.Count;
            float travelled = 0f;

            while (onPath > 0)
            {
                travelled += Mathf.Max(0.05f, m_RopeSpeed) * Time.deltaTime;
                // Gemi yolda kayarsa (slotlar sola toplanınca) yolun kıyı ucu da onunla gelsin
                Vector3 shoreNow = ship != null ? GetShorePoint(ship) : ropes[0].ShoreAtLaunch;

                for (int i = 0; i < riders.Count; i++)
                {
                    if (arrived[i]) continue;
                    var (rope, index, cargo) = riders[i];
                    if (cargo == null) { arrived[i] = true; onPath--; continue; }

                    float d = rope.StartDistances[index] + travelled;
                    Vector3 shoreShift = shoreNow - rope.ShoreAtLaunch;
                    if (d >= rope.Path.Length)
                    {
                        arrived[i] = true;
                        onPath--;
                        cargo.transform.position = rope.PointAt(rope.Path.Length, shoreShift);
                        StartCoroutine(HopCargoToShip(cargo, ship, rope.EntersLeft, rope.Cubes[index]));
                    }
                    else
                    {
                        Vector3 p = rope.PointAt(d, shoreShift);
                        cargo.transform.position = p;
                        // Gittiği yöne kafasını çevirir gibi döner
                        if (runners[i] != null) runners[i].TurnToward(rope.PointAt(d + 0.05f, shoreShift) - p, Time.deltaTime);
                    }
                }

                yield return null;
            }
        }

        private static MaterialPropertyBlock s_CargoColorBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>
        /// Küpün yerine yürüyen "dublör" prefab: küpün yeri, açısı, boyutu ve rengiyle doğar.
        /// Animator'ı varsa yan yana küpler ters fazda koşar.
        /// </summary>
        private GameObject CreateStandInCargo(PixelCube cube, int indexInRope)
        {
            GameObject cargo = Instantiate(m_CargoStandInPrefab, cube.transform.position, cube.transform.rotation);
            cargo.name = "RopeCargoCube";
            cargo.transform.localScale = cube.transform.lossyScale;

            if (s_CargoColorBlock == null) s_CargoColorBlock = new MaterialPropertyBlock();
            s_CargoColorBlock.Clear();
            s_CargoColorBlock.SetColor(BaseColorId, cube.CurrentColor);
            s_CargoColorBlock.SetColor(ColorId, cube.CurrentColor);
            s_CargoColorBlock.SetColor(EmissionColorId, Color.black);
            foreach (Renderer r in cargo.GetComponentsInChildren<Renderer>(true))
            {
                r.SetPropertyBlock(s_CargoColorBlock);
            }

            Animator animator = cargo.GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.Play(0, 0, (indexInRope % 2) * 0.5f);
            }

            ICargoRunner runner = cargo.GetComponent<ICargoRunner>();
            if (runner != null) runner.BeginWalk(indexInRope);
            return cargo;
        }

        /// <summary>
        /// Bacaksız bir küp prefab'ı kullanılıyorsa küpün yerine yürüyen yedek görsel
        /// (küp yerinde gizlenir, bu kopya kayarak gider).
        /// </summary>
        private static GameObject CreateRopeCargo(PixelCube cube)
        {
            float size = cube.transform.lossyScale.x > 0.001f ? cube.transform.lossyScale.x : 0.2605f;
            GameObject walker = new GameObject("RopeCargoCube");
            walker.transform.position = cube.transform.position;
            WalkingCargoVisual visual = WalkingCargoVisual.Attach(walker, size);
            visual.ApplyColor(cube.CurrentColor);
            return walker;
        }

        /// <summary>
        /// Sahil kenarına gelen küp, sahil sonunda gemiye doğru zıplar (Jump arc to ship) ve gemiye kargo olarak biner.
        /// </summary>
        private IEnumerator HopCargoToShip(GameObject cargo, ShipController ship, bool fromLeft, PixelCube sourceCube)
        {
            if (cargo == null) yield break;

            Vector3 start = cargo.transform.position;
            ICargoRunner runner = cargo.GetComponent<ICargoRunner>();
            WaddleRunner waddle = cargo.GetComponent<WaddleRunner>();
            WalkingCargoVisual visual = cargo.GetComponent<WalkingCargoVisual>();

            if (waddle != null) waddle.IsAirborne = true;

            // Gemi güverte iniş hedefi:
            Vector3 deckOffset = new Vector3(fromLeft ? -0.06f : 0.06f, 0.16f, 0.02f);
            float duration = Mathf.Max(0.15f, m_HopDuration);
            float elapsed = 0f;

            Camera mainCam = ShipController.MainCamera;
            Vector3 arcUp = mainCam != null ? mainCam.transform.up : Vector3.up;

            Quaternion startRot = cargo.transform.rotation;
            Vector3 baseScale = cargo.transform.localScale;

            while (elapsed < duration && cargo != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Hedef pozisyon (gemi hareket ediyorsa dinamik takip):
                Vector3 target = ship != null ? ship.transform.position + deckOffset : start;

                // 1. Yatay/Dikey İlerleme (Pürüzsüz Parabolik Rota):
                float hT = Mathf.SmoothStep(0f, 1f, t);
                Vector3 p = Vector3.Lerp(start, target, hT);

                // Gerçek fiziksel parabolik yay: t=0'da 0, t=0.5'te tam m_HopArcHeight zirvesi, t=1'de 0
                float arc = 4f * t * (1f - t);
                p += arcUp * (arc * m_HopArcHeight);

                cargo.transform.position = p;

                // 2. Havada Dönüş & Yönelme:
                Vector3 toTarget = target - start;
                if (runner != null)
                {
                    runner.TurnToward(toTarget, Time.deltaTime);
                }

                // Havada öne doğru tatlı bir zıplama eğimi (tilt / pitch):
                float pitchAngle = Mathf.Sin(t * Mathf.PI) * 16f;
                cargo.transform.rotation = startRot * Quaternion.Euler(pitchAngle, 0f, 0f);

                // 3. Zıplama Bacak/Görsel Duruşu:
                if (visual != null)
                {
                    visual.SetAirborne(Time.deltaTime);
                }

                // 4. Squash & Stretch (Juice):
                if (t < 0.25f)
                {
                    // Kalkış: Y'de uzama (stretch)
                    float stretch = Mathf.Sin(t / 0.25f * Mathf.PI * 0.5f) * 0.18f;
                    cargo.transform.localScale = new Vector3(baseScale.x * (1f - stretch * 0.5f), baseScale.y * (1f + stretch), baseScale.z * (1f - stretch * 0.5f));
                }
                else if (t > 0.80f)
                {
                    // İniş yaklaşımı: Hafif basılma (squash)
                    float landingT = (t - 0.80f) / 0.20f;
                    float squash = Mathf.Sin(landingT * Mathf.PI * 0.5f) * 0.22f;
                    cargo.transform.localScale = new Vector3(baseScale.x * (1f + squash), baseScale.y * (1f - squash), baseScale.z * (1f + squash));
                }
                else
                {
                    cargo.transform.localScale = baseScale;
                }

                yield return null;
            }

            if (cargo != null)
            {
                // Kendisi yürüyen küp silinmez, gizlenir (resim sıfırlanınca yerine döner)
                if (sourceCube != null && cargo == sourceCube.gameObject)
                {
                    if (waddle != null) waddle.IsAirborne = false;
                    sourceCube.FinishLeaving();
                }
                else
                {
                    Destroy(cargo);
                }
            }
            if (sourceCube != null) s_ReservedCubes.Remove(sourceCube);

            if (ship != null)
            {
                // Gemiye kargo ekle ve görsel/ses/su geri bildirimini tetikle
                ship.AddCargo(1);
                ship.TriggerWaterDipImpact(0.12f, 0.35f);
                ShipController.SpawnWaterRipple(ship.transform.position + new Vector3(0f, -0.05f, 0.05f), 0.28f, 0.95f, 0.45f);
                HypercasualWaterController.TriggerWaterRipple(ship.transform.position, 0.70f, 0.25f);
            }

            m_ActiveCargoFlightCount = Mathf.Max(0, m_ActiveCargoFlightCount - 1);

            CheckWinCondition();
            TriggerWaitingShipsCheck();
        }

        /// <summary>Kolun panodan çıktığı nokta (çıkış yolunun son hücresi, yoksa baş küp).</summary>
        private Vector3 ExitWorldPoint(PixelCube head, List<(int, int)> exitCells)
        {
            if (exitCells == null || exitCells.Count == 0) return head.transform.position;
            var last = exitCells[exitCells.Count - 1];
            return m_GridFrame.ToWorld(last.Item1, last.Item2);
        }

        /// <summary>
        /// Panodan gemi slotuna zemin üzerinde pürüzsüz yürüyüş rotası (Catmull-Rom kontrol noktaları).
        /// Referans videodaki gibi küpler pano altından masa/kumsal zeminine adım atar,
        /// zemin koridorundan akarak hedef geminin girişine kadar kesintisiz yürür.
        /// </summary>
        private static List<Vector3> BuildApproach(Vector3 exitPoint, Vector3 shoreCenter, Vector3 laneOffset)
        {
            Vector3 targetEntrance = shoreCenter + laneOffset;
            float totalDy = exitPoint.y - targetEntrance.y;
            if (totalDy <= 0.35f)
            {
                return new List<Vector3> { targetEntrance };
            }

            // 1. Pano Alt Sınırından Zemine İniş:
            // Küp panonun alt kenarından dışarı çıkar çıkmaz masa/kumsal zemin düzlemine basar
            float dropY = exitPoint.y - 0.22f;
            float dropZ = Mathf.Lerp(exitPoint.z, targetEntrance.z, 0.10f);
            Vector3 pDrop = new Vector3(exitPoint.x + laneOffset.x * 0.35f, dropY, dropZ);

            // 2. Zemin Koridoru (Orta Zemin):
            // Referans videodaki gibi iki kol zemin üzerinde tatlı bir kavisle gemi X hizasına doğru akar
            float midY = Mathf.Lerp(dropY, targetEntrance.y + 0.30f, 0.46f);
            float midX = Mathf.Lerp(exitPoint.x, targetEntrance.x, 0.55f) + laneOffset.x * 0.85f;
            float midZ = Mathf.Lerp(exitPoint.z, targetEntrance.z, 0.50f);
            Vector3 pMid = new Vector3(midX, midY, midZ);

            // 3. Giriş Öncesi Hizalanma:
            // Geminin pruva çizgisi hizasında düzelerek doğrudan slot/güverte ağzına yönelir
            float appY = targetEntrance.y + 0.22f;
            float appX = Mathf.Lerp(exitPoint.x, targetEntrance.x, 0.90f) + laneOffset.x * 0.55f;
            float appZ = Mathf.Lerp(exitPoint.z, targetEntrance.z, 0.85f);
            Vector3 pApproach = new Vector3(appX, appY, appZ);

            // 4. Son Varış (Gemi Ön Eşiği):
            Vector3 pFinal = targetEntrance;

            return new List<Vector3> { pDrop, pMid, pApproach, pFinal };
        }

        /// <summary>Geminin kıyıdaki/slotundaki sahil kenarı noktası (küplerin sahildeki yürüyüşünü tamamlayıp gemiye zıpladığı yer).</summary>
        public Vector3 GetShorePoint(ShipController ship)
        {
            if (ship != null)
            {
                Vector3 shipPos = ship.transform.position;
                if (m_Shoreline != null && m_Shoreline.Count >= 2)
                {
                    // Kıyı çizgisinde gemiye en yakın nokta; biraz kum tarafına çekilir ki küpler suya basmasın
                    Vector2 ship2 = new Vector2(shipPos.x, shipPos.y);
                    Vector2 best = m_Shoreline[0];
                    float bestSq = float.MaxValue;
                    for (int i = 0; i < m_Shoreline.Count - 1; i++)
                    {
                        Vector2 a = m_Shoreline[i], b = m_Shoreline[i + 1];
                        Vector2 ab = b - a;
                        float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(ship2 - a, ab) / ab.sqrMagnitude) : 0f;
                        Vector2 q = a + ab * t;
                        float sq = (q - ship2).sqrMagnitude;
                        if (sq < bestSq) { bestSq = sq; best = q; }
                    }
                    Vector2 inward = best - ship2;
                    if (inward.sqrMagnitude > 1e-6f) best += inward.normalized * m_ShorelineInset;
                    return new Vector3(best.x, best.y, 0.22f);
                }
                // Sahil kenarı: Kum alanının bittiği, suyun başladığı kıyı eşiği (Y ≈ shipPos.y + 0.95f, Z = 0.22f)
                return new Vector3(shipPos.x, shipPos.y + 0.95f, 0.22f);
            }

            EnsureBoardBounds();
            float centerX = (m_BoardMinX + m_BoardMaxX) * 0.5f;
            return new Vector3(centerX, m_ShoreY + 0.95f, 0.22f);
        }

        /// <summary>Panodaki canlı (patlamamış) küplerin ızgarası ve dış hava hücreleri.</summary>
        private bool TryBuildLiveGrid(out Dictionary<(int, int), PixelCube> gridMap, out HashSet<(int, int)> outsideAir, out int minY)
        {
            gridMap = new Dictionary<(int, int), PixelCube>();
            outsideAir = null;
            minY = 0;

            if (m_Generator == null) m_Generator = UnityEngine.Object.FindFirstObjectByType<PixelArtGenerator>();
            if (m_Generator == null || m_Generator.CubesContainer == null) return false;

            var allCubes = PixelCube.ActiveCubes;
            int minX = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            minY = int.MaxValue;
            foreach (var c in allCubes)
            {
                if (c == null || c.IsPopped || !c.gameObject.activeSelf) continue;
                gridMap[(c.GridX, c.GridY)] = c;
                if (c.GridX < minX) minX = c.GridX;
                if (c.GridX > maxX) maxX = c.GridX;
                if (c.GridY < minY) minY = c.GridY;
                if (c.GridY > maxY) maxY = c.GridY;
            }
            if (gridMap.Count == 0) return false;

            outsideAir = CalculateOutsideAir(gridMap, minX, maxX, minY, maxY);
            return true;
        }

        private bool EnsureGridFrame()
        {
            if (m_GridFrameValid) return true;
            if (m_Generator == null) m_Generator = UnityEngine.Object.FindFirstObjectByType<PixelArtGenerator>();
            if (m_Generator == null || m_Generator.CubesContainer == null) return false;

            // Patlamış küpler yerinde durur, ama yürüyerek ayrılanlar yerini terk etmiştir:
            // ızgarayı yerinde duranlardan çıkar.
            var inPlace = new List<PixelCube>();
            foreach (var c in m_Generator.CubesContainer.GetComponentsInChildren<PixelCube>(true))
            {
                if (c != null && !c.IsLeaving) inPlace.Add(c);
            }
            m_GridFrameValid = CubeGridFrame.TryBuild(inPlace.ToArray(), out m_GridFrame);
            if (!m_GridFrameValid)
            {
                Debug.LogWarning("[ShipDispatcher] Küp ızgarası çıkarılamadı; kargo treni kurulamıyor.");
            }
            return m_GridFrameValid;
        }

        /// <summary>
        /// Boş olan en küçük indeksli (en soldaki: 1-2-3-4-5) slotu döner.
        /// Böylece her zaman 1 boşsa 1'e, 2 boşsa 2'ye dolar; arada boşluk kalmaz.
        /// </summary>
        public ShipSlot FindEmptySlot()
        {
            if (m_Slots == null || m_Slots.Count == 0) return null;

            for (int i = 0; i < m_Slots.Count; i++)
            {
                var slot = m_Slots[i];
                if (slot != null && slot.IsEmpty)
                {
                    return slot;
                }
            }
            return null;
        }

        /// <summary>
        /// Slotlardaki gemileri 1-2-3-4-5 sırasına göre sol hizalı (boşluksuz) kaydırır.
        /// Örneğin 2 boşsa 3'teki gemi 2'ye, 4'teki 3'e su üzerinden pürüzsüzce kayar;
        /// slotlar arasında asla boşluk kalmaz.
        /// </summary>
        public void CompactSlots(float delay = 0.12f)
        {
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(CompactSlotsRoutine(delay));
            }
        }

        private IEnumerator CompactSlotsRoutine(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (m_Slots == null || m_Slots.Count == 0) yield break;

            bool movedAny = false;

            // 1'den 5'e kadar (0'dan 4'e kadar indeksler) tara
            for (int targetIdx = 0; targetIdx < m_Slots.Count; targetIdx++)
            {
                var targetSlot = m_Slots[targetIdx];
                if (targetSlot == null) continue;

                // Hedef slot boşsa, kendisinden sonraki ilk uygun gemiyi bulup bu slota kaydır
                if (targetSlot.IsEmpty)
                {
                    for (int fromIdx = targetIdx + 1; fromIdx < m_Slots.Count; fromIdx++)
                    {
                        var fromSlot = m_Slots[fromIdx];
                        if (fromSlot != null && !fromSlot.IsEmpty && fromSlot.DockedShip != null)
                        {
                            ShipController ship = fromSlot.DockedShip;
                            if (!ship.IsDeparting && !ship.IsMoving)
                            {
                                fromSlot.ReleaseShip();
                                m_RopeGates.Remove(ship);
                                // Su üzerinden tatlı bir hızla hedef slota kaydır (0.28s)
                                ship.SailToSlot(targetSlot, 0.28f);
                                movedAny = true;
                                break;
                            }
                        }
                    }
                }
            }

            if (movedAny)
            {
                yield return new WaitForSeconds(0.30f);
                TriggerWaitingShipsCheck();
            }

            CheckAutoPlaceRemainingShips();
        }

        /// <summary>
        /// Kuyruktan tıklanan gemiyi boş bir slota göndermeyi dener.
        /// Eğer gemi başka bir gemiye bağlıysa, her ikisi birden en ön sırada olmalı ve
        /// sahilde en az 2 boş slot bulunmalıdır; ikisi birlikte hareket eder.
        /// </summary>
        public bool TrySendShipFromQueue(ShipController ship)
        {
            if (m_IsAutoPlacing || m_IsLevelFailed) return false;
            if (ship == null || ship.IsDocked || ship.IsMoving || ship.IsDeparting) return false;

            // 1. 🔗 Bağlı Gemi Kontrolü
            if (ship.IsLinked)
            {
                ShipController partner = ship.LinkedPartner;

                // Partner de en ön sırada mı?
                if (!ship.CanDispatchLinked() || partner == null)
                {
                    ship.PlayWobble();
                    if (partner != null) partner.PlayWobble();
                    if (ship.Tether != null) ship.Tether.Rattle();
                    return false;
                }

                // 2 slotluk boş yer var mı? (Kural: 2 slot kaplayacaklar, 2 slotluk yer yoksa tıklanmaz)
                List<ShipSlot> emptySlots = GetEmptySlots();
                if (emptySlots == null || emptySlots.Count < 2)
                {
                    ship.PlayWobble();
                    if (partner != null) partner.PlayWobble();
                    if (ship.Tether != null) ship.Tether.Rattle();
                    return false;
                }

                // İki slotu tahsis et ve iki gemiyi birlikte gönder
                ShipSlot slotA = emptySlots[0];
                ShipSlot slotB = emptySlots[1];

                // Her iki gemiyi de kuyruk sisteminden çıkar (alt alta olsalar dahi çift sıra kaydırmayı kusursuz işletir)
                if (m_QueuePool != null)
                {
                    m_QueuePool.OnLinkedShipsDispatched(ship, partner);
                }

                ship.transform.SetParent(null, true);
                partner.transform.SetParent(null, true);

                ship.SailToSlot(slotA);
                partner.SailToSlot(slotB);
                return true;
            }

            // 2. Normal Tekil Gemi Kontrolü
            // En ön sıra kontrolü
            if (m_QueuePool != null && !m_QueuePool.IsFrontRow(ship))
            {
                ship.PlayWobble();
                return false;
            }

            // Boş slot kontrolü
            ShipSlot emptySlot = FindEmptySlot();
            if (emptySlot == null)
            {
                ship.PlayWobble();
                return false;
            }

            // Kuyruktan çıkar, arkadaki gemiyi öne kaydır ve açık denizden yenisini getir
            if (m_QueuePool != null)
            {
                m_QueuePool.OnFrontShipDispatched(ship);
            }

            // Gemiyi kuyruk ebeveyninden hemen ayır ki arkadaki gemi geldiğinde çakışmasın
            ship.transform.SetParent(null, true);
            ship.SailToSlot(emptySlot);
            return true;
        }

        /// <summary>
        /// Sahnedeki tüm aktif ve boş yanaşma slotlarını döner.
        /// </summary>
        public List<ShipSlot> GetEmptySlots()
        {
            List<ShipSlot> list = new List<ShipSlot>();
            if (m_Slots != null)
            {
                for (int i = 0; i < m_Slots.Count; i++)
                {
                    var s = m_Slots[i];
                    if (s != null && s.IsEmpty && s.gameObject.activeInHierarchy)
                    {
                        list.Add(s);
                    }
                }
            }
            return list;
        }

        private bool IsAnyShipMoving()
        {
            var allShips = ShipController.ActiveShips;
            for (int i = 0; i < allShips.Count; i++)
            {
                var s = allShips[i];
                if (s != null && (s.IsMoving || s.IsDragging)) return true;
            }
            return false;
        }

        /// <summary>
        /// Kalan gemilerin boş slotlara sığıp sığmadığını kontrol eder.
        /// Kullanıcının isteği: "mesela slotların hepsi boş 5 tane slotta boş kaldı 3 gemi
        /// onları oto yerleştirip oyunu 2x oynatmak istiyorum otomatik bir şekilde yerleştirip 2x oynanacak yani"
        /// </summary>
        public void CheckAutoPlaceRemainingShips()
        {
            if (!m_EnableAutoPlaceAndTurbo || m_IsAutoPlacing || m_LevelEndPending || m_IsLevelFailed || !Application.isPlaying) return;
            if (m_QueuePool == null || m_Slots == null || m_Slots.Count == 0) return;

            // Eğer şu anda hareket eden veya sürüklenen herhangi bir gemi varsa bekle
            if (IsAnyShipMoving()) return;

            // Kuyrukta açık denizde veya sırada henüz gelmemiş başka gemi var mı?
            if (!m_QueuePool.HasNoMoreFutureShips()) return;

            // Şu an kuyrukta hazır bekleyen gemiler
            List<ShipController> waitingShips = m_QueuePool.GetActiveWaitingShips();
            int waitingCount = waitingShips.Count;
            if (waitingCount == 0) return;

            // Boş slotları al
            List<ShipSlot> emptySlots = GetEmptySlots();
            int emptyCount = emptySlots.Count;

            // Kural: Kalan bekleyen gemi sayısı boş slot sayısına eşit veya daha azsa (Örn: 5 slot boş, 3 gemi kaldı)
            // Tüm kalan gemiler tek seferde boş slotlara sığabilir; oyuncunun beklemesine gerek yok!
            if (waitingCount <= emptyCount)
            {
                StartCoroutine(AutoPlaceRemainingShipsRoutine(waitingShips, emptySlots));
            }
        }

        private IEnumerator AutoPlaceRemainingShipsRoutine(List<ShipController> shipsToPlace, List<ShipSlot> targetSlots)
        {
            m_IsAutoPlacing = true;
            Debug.Log($"<color=#00FFAA><b>[ShipDispatcher]</b></color> ⚡ Otomatik Yerleştirme Devrede! Kalan {shipsToPlace.Count} gemi {targetSlots.Count} boş slota yerleştiriliyor ve oyun 2X hıza alınıyor.");

            // 2X Hıza Geçiş ve Turbo Bildirimi
            SetTurboSpeed(true);

            // Gemileri sırayla, tatlı bir aralıkla (0.10s) boş slotlara yolla
            for (int i = 0; i < shipsToPlace.Count; i++)
            {
                if (i >= targetSlots.Count) break;

                ShipController ship = shipsToPlace[i];
                ShipSlot slot = targetSlots[i];

                if (ship != null && slot != null && slot.IsEmpty)
                {
                    if (m_QueuePool != null)
                    {
                        m_QueuePool.RemoveShipFromQueue(ship);
                    }

                    ship.transform.SetParent(null, true);
                    ship.SailToSlot(slot, 0.35f);
                }

                yield return new WaitForSeconds(0.10f);
            }

            m_IsAutoPlacing = false;
        }

        /// <summary>
        /// ⚡ 2X Turbo oyun hızını ve UI bildirimini yönetir.
        /// </summary>
        public void SetTurboSpeed(bool active)
        {
            m_IsTurboActive = active;
            Time.timeScale = active ? 2.0f : 1.0f;

            if (CasualHudController.Instance != null)
            {
                CasualHudController.Instance.SetTurboIndicator(active);
            }
        }

        /// <summary>
        /// Dış havaya açık (hemen toplanabilir) küplerin renk listesini döner.
        /// </summary>
        public List<Color> GetExposedLevelColors()
        {
            List<Color> exposedColors = new List<Color>();
            if (m_Generator == null) m_Generator = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (m_Generator == null || m_Generator.CubesContainer == null) return exposedColors;

            var allCubes = PixelCube.ActiveCubes;
            if (allCubes == null || allCubes.Count == 0) return exposedColors;

            Dictionary<(int, int), PixelCube> gridMap = new Dictionary<(int, int), PixelCube>(allCubes.Count);
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            for (int i = 0; i < allCubes.Count; i++)
            {
                PixelCube c = allCubes[i];
                if (c != null && !c.IsPopped && c.gameObject.activeSelf)
                {
                    gridMap[(c.GridX, c.GridY)] = c;
                    if (c.GridX < minX) minX = c.GridX;
                    if (c.GridX > maxX) maxX = c.GridX;
                    if (c.GridY < minY) minY = c.GridY;
                    if (c.GridY > maxY) maxY = c.GridY;
                }
            }

            if (gridMap.Count == 0) return exposedColors;

            HashSet<(int, int)> outsideAir = CalculateOutsideAir(gridMap, minX, maxX, minY, maxY);

            Dictionary<Color, int> countMap = new Dictionary<Color, int>();
            foreach (var kvp in gridMap)
            {
                PixelCube cube = kvp.Value;
                if (cube == null || s_ReservedCubes.Contains(cube)) continue;

                int x = cube.GridX;
                int y = cube.GridY;
                bool touchesAir = outsideAir.Contains((x - 1, y)) ||
                                  outsideAir.Contains((x + 1, y)) ||
                                  outsideAir.Contains((x, y - 1)) ||
                                  outsideAir.Contains((x, y + 1));

                if (touchesAir)
                {
                    Color c = cube.CurrentColor != Color.clear ? cube.CurrentColor : cube.OriginalColor;
                    bool matched = false;
                    foreach (var key in countMap.Keys)
                    {
                        if (ColorsMatch(key, c))
                        {
                            countMap[key]++;
                            matched = true;
                            break;
                        }
                    }
                    if (!matched)
                    {
                        countMap[c] = 1;
                    }
                }
            }

            return new List<Color>(countMap.Keys);
        }

        /// <summary>
        /// Seviyedeki aktif henüz patlatılmamış küplerin renklerinden birini döndürür.
        /// preferExposed true ise öncelikle dışta (hemen toplanabilir) olan renklere öncelik verir.
        /// preferExposed false ise seviyede toplanacak TÜM renklerden (ör. yeşil, siyah, kahverengi vb.) seçim yapar.
        /// </summary>
        public static Color NormalizeShipColor(Color c)
        {
            // Palette birden fazla sarı/amber ton varsa hepsi aynı pastel sarıya dönüşüp ayırt edilemez
            // hale gelirdi; o durumda gemi kendi gerçek rengini korur.
            if (s_YellowPaletteEntryCount <= 1 && IsYellowTone(c))
            {
                return new Color(0.957f, 0.831f, 0.384f, 1f); // 1. fotodaki gibi mat, yumuşak pastel sarı (#F4D462)
            }
            return c;
        }

        public Color GetRemainingLevelColor(bool preferExposed = true)
        {
            // 1. İsteniyorsa önce DIŞTA (hemen toplanabilir) olan renklere öncelik ver!
            if (preferExposed)
            {
                var exposed = GetExposedLevelColors();
                if (exposed != null && exposed.Count > 0)
                {
                    return NormalizeShipColor(exposed[Random.Range(0, exposed.Count)]);
                }
            }

            if (m_Generator == null) m_Generator = Object.FindFirstObjectByType<PixelArtGenerator>();

            if (m_Generator != null && m_Generator.CubesContainer != null)
            {
                var cubes = m_Generator.CubesContainer.GetComponentsInChildren<PixelCube>(false);
                if (cubes != null && cubes.Length > 0)
                {
                    Dictionary<Color, int> colorCounts = new Dictionary<Color, int>();

                    foreach (var cube in cubes)
                    {
                        if (cube != null && !cube.IsPopped && cube.gameObject.activeSelf && !s_ReservedCubes.Contains(cube))
                        {
                            Color c = cube.CurrentColor != Color.clear ? cube.CurrentColor : cube.OriginalColor;
                            bool matched = false;
                            foreach (var key in colorCounts.Keys)
                            {
                                if (ColorsMatch(key, c))
                                {
                                    colorCounts[key]++;
                                    matched = true;
                                    break;
                                }
                            }
                            if (!matched)
                            {
                                colorCounts[c] = 1;
                            }
                        }
                    }

                    if (colorCounts.Count > 0)
                    {
                        List<Color> keys = new List<Color>(colorCounts.Keys);
                        return NormalizeShipColor(keys[Random.Range(0, keys.Count)]);
                    }
                }
            }

            // Fallback: Aktif bölümün paletinden renk çek (Asla alakasız rastgele renk üretmez!)
            PixelLevelData level = (m_Generator != null) ? m_Generator.ActiveLevelData : null;
            if (level == null && LevelManager.Instance != null) level = LevelManager.Instance.CurrentLevel;
            if (level == null)
            {
                LevelManager lm = Object.FindFirstObjectByType<LevelManager>();
                if (lm != null) level = lm.CurrentLevel;
            }

            if (level != null && level.ColorPalette != null && level.ColorPalette.Count > 0)
            {
                var entry = level.ColorPalette[Random.Range(0, level.ColorPalette.Count)];
                return NormalizeShipColor(entry.targetColor != Color.clear ? entry.targetColor : entry.originalColor);
            }

            return Color.clear;
        }

        public int GetRemainingCountForColor(Color targetColor)
        {
            var activeCubes = PixelCube.ActiveCubes;
            if (activeCubes != null && activeCubes.Count > 0)
            {
                int count = 0;
                for (int i = 0; i < activeCubes.Count; i++)
                {
                    var cube = activeCubes[i];
                    if (cube != null && !cube.IsPopped && cube.gameObject.activeSelf && !s_ReservedCubes.Contains(cube))
                    {
                        if (ColorsMatch(cube.CurrentColor, targetColor) || ColorsMatch(cube.OriginalColor, targetColor))
                        {
                            count++;
                        }
                    }
                }
                if (count > 0) return count;
            }

            // Fallback: Seviye paletinden piksel sayısını bul
            PixelLevelData level = (m_Generator != null) ? m_Generator.ActiveLevelData : null;
            if (level == null && LevelManager.Instance != null) level = LevelManager.Instance.CurrentLevel;
            if (level != null && level.ColorPalette != null)
            {
                foreach (var p in level.ColorPalette)
                {
                    if (ColorsMatch(p.targetColor, targetColor) || ColorsMatch(p.originalColor, targetColor))
                    {
                        return p.pixelCount;
                    }
                }
            }

            return 0;
        }

        public int GetTotalRemainingCubes()
        {
            var activeCubes = PixelCube.ActiveCubes;
            if (activeCubes != null && activeCubes.Count > 0)
            {
                int count = 0;
                for (int i = 0; i < activeCubes.Count; i++)
                {
                    var cube = activeCubes[i];
                    if (cube != null && !cube.IsPopped && cube.gameObject.activeSelf && !s_ReservedCubes.Contains(cube))
                    {
                        count++;
                    }
                }
                return count;
            }
            return 0;
        }

        /// <summary>
        /// Tablodaki tüm küpler patlatıldıysa seviye tamamlanma sürecini başlatır. Seviye geçişi
        /// hemen olmaz: son gemi de sahneyi gerçekten terk edene kadar beklenir (bkz. BeginLevelEndSequence).
        /// </summary>
        public void CheckWinCondition()
        {
            if (m_LevelEndPending) return;

            var activeCubes = PixelCube.ActiveCubes;
            int unpoppedCount = 0;

            if (activeCubes != null)
            {
                for (int i = 0; i < activeCubes.Count; i++)
                {
                    var cube = activeCubes[i];
                    if (cube != null && !cube.IsPopped && cube.gameObject.activeSelf)
                    {
                        unpoppedCount++;
                    }
                }
            }

            // Hâlâ havada (panodan gemiye uçuş animasyonu sürmekte olan) küp varsa bekle —
            // yoksa son küp koparılır kopartılmaz, o küp henüz gemiye TAM ulaşmadan gemiler
            // sahneyi animasyon yarıda kesilerek terk ediyordu.
            if (unpoppedCount == 0 && m_ActiveCargoFlightCount <= 0)
            {
                Debug.Log("<color=#00FFAA><b>[ShipDispatcher]</b></color> 🎉 Tüm piksel resmi tamamlandı! Son gemilerin sahneyi terk etmesi bekleniyor...");
                BeginLevelEndSequence();
            }
            else
            {
                CheckAutoPlaceRemainingShips();
            }
        }

        #region ❌ Deadlock & Seviye Başarısızlık Kontrolü

        private void UpdateDeadlockCheck()
        {
            if (!m_EnableFailOnDeadlock || m_LevelEndPending || m_IsLevelFailed)
            {
                m_DeadlockTimer = 0f;
                m_DeadlockCheckIntervalTimer = 0f;
                m_CachedDeadlockCondition = false;
                return;
            }

            // Eğer herhangi bir slot boşsa deadlock kesinlikle imkansızdır; maliyetli kontrolleri yapma.
            if (HasAnyEmptySlot())
            {
                m_DeadlockTimer = 0f;
                m_DeadlockCheckIntervalTimer = 0f;
                m_CachedDeadlockCondition = false;
                return;
            }

            m_DeadlockCheckIntervalTimer += Time.unscaledDeltaTime;
            if (m_DeadlockCheckIntervalTimer >= 0.15f)
            {
                m_DeadlockCheckIntervalTimer = 0f;
                m_CachedDeadlockCondition = CheckDeadlockCondition();
            }

            if (m_CachedDeadlockCondition)
            {
                m_DeadlockTimer += Time.unscaledDeltaTime;
                if (m_DeadlockTimer >= m_DeadlockGraceDuration)
                {
                    TriggerLevelFail();
                }
            }
            else
            {
                m_DeadlockTimer = 0f;
            }
        }

        private bool HasAnyEmptySlot()
        {
            if (m_Slots == null || m_Slots.Count == 0) return true;
            for (int i = 0; i < m_Slots.Count; i++)
            {
                var slot = m_Slots[i];
                if (slot == null || !slot.gameObject.activeInHierarchy) continue;
                if (slot.IsEmpty || slot.DockedShip == null) return true;
            }
            return false;
        }

        private bool HasAnyDockedShipExposedCube(List<ShipSlot> slots)
        {
            if (m_Generator == null) m_Generator = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (m_Generator == null || m_Generator.CubesContainer == null) return false;

            var activeCubes = PixelCube.ActiveCubes;
            if (activeCubes == null || activeCubes.Count == 0) return false;

            // Docked gemilerin kabul edebileceği renkleri topla
            List<Color> candidateColors = new List<Color>(slots != null ? slots.Count : 4);
            if (slots != null)
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    var s = slots[i];
                    if (s != null && s.DockedShip != null && !s.DockedShip.IsDeparting && s.DockedShip.CanAcceptMore)
                    {
                        Color col = s.DockedShip.ShipColor;
                        bool alreadyAdded = false;
                        for (int c = 0; c < candidateColors.Count; c++)
                        {
                            if (ColorsMatch(candidateColors[c], col))
                            {
                                alreadyAdded = true;
                                break;
                            }
                        }
                        if (!alreadyAdded)
                        {
                            candidateColors.Add(col);
                        }
                    }
                }
            }

            if (candidateColors.Count == 0) return false;

            Dictionary<(int, int), PixelCube> gridMap = new Dictionary<(int, int), PixelCube>(activeCubes.Count);
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            for (int i = 0; i < activeCubes.Count; i++)
            {
                PixelCube c = activeCubes[i];
                if (c != null && !c.IsPopped && c.gameObject.activeSelf)
                {
                    gridMap[(c.GridX, c.GridY)] = c;
                    if (c.GridX < minX) minX = c.GridX;
                    if (c.GridX > maxX) maxX = c.GridX;
                    if (c.GridY < minY) minY = c.GridY;
                    if (c.GridY > maxY) maxY = c.GridY;
                }
            }

            if (gridMap.Count == 0) return false;

            HashSet<(int, int)> outsideAir = CalculateOutsideAir(gridMap, minX, maxX, minY, maxY);

            foreach (var kvp in gridMap)
            {
                PixelCube cube = kvp.Value;
                if (cube == null || s_ReservedCubes.Contains(cube)) continue;

                for (int c = 0; c < candidateColors.Count; c++)
                {
                    if (ColorsMatch(cube.CurrentColor, candidateColors[c]) || ColorsMatch(cube.OriginalColor, candidateColors[c]))
                    {
                        int x = cube.GridX;
                        int y = cube.GridY;
                        bool touchesAir = outsideAir.Contains((x - 1, y)) ||
                                          outsideAir.Contains((x + 1, y)) ||
                                          outsideAir.Contains((x, y - 1)) ||
                                          outsideAir.Contains((x, y + 1));
                        if (touchesAir)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Tüm slotlar dolduğunda ve hamle yapılamadığında true döner.
        /// Kullanıcı isteği: "slotların hepsi dolduğunda ve hamle yapılamadığında da level fail olacak"
        /// </summary>
        public bool CheckDeadlockCondition()
        {
            if (m_LevelEndPending || m_IsLevelFailed) return false;

            // 1. Slot referansları kontrolü: Herhangi bir slot boşsa oyuncu kuyruktan gemi gönderebilir -> fail DEĞİL
            if (m_Slots == null || m_Slots.Count == 0) return false;

            int activeSlotCount = 0;
            for (int i = 0; i < m_Slots.Count; i++)
            {
                var slot = m_Slots[i];
                if (slot == null || !slot.gameObject.activeInHierarchy) continue;

                activeSlotCount++;
                if (slot.IsEmpty || slot.DockedShip == null)
                {
                    return false;
                }
            }

            if (activeSlotCount == 0) return false;

            // 2. Tabloda küp kalmadıysa kazanılmıştır, fail olamaz
            int totalRemaining = GetTotalRemainingCubes();
            if (totalRemaining <= 0) return false;

            // 3. Havada uçuşan kargo var mı veya küp çekme coroutine'i çalışıyor mu?
            if (m_ActiveCargoFlightCount > 0) return false;
            if (m_ActiveExtractingShips.Count > 0) return false;

            // 4. Sahnedeki gemilerden herhangi biri hareket halinde mi, sürükleniyor mu veya ayrılıyor mu?
            var allShips = ShipController.ActiveShips;
            for (int i = 0; i < allShips.Count; i++)
            {
                var ship = allShips[i];
                if (ship == null) continue;

                if (ship.IsMoving || ship.IsDragging || ship.IsDeparting || ship.HasPendingCargo)
                {
                    return false;
                }
            }

            // 5. Slotta yanaşık duran gemileri analiz et:
            // Herhangi bir gemi:
            // a) Tam kapasiteye ulaşmışsa -> kalkış yapacak, slot boşalacak -> fail DEĞİL
            // b) Tabloda o renkten hiç küp kalmamışsa -> zorunlu kalkış yapacak, slot boşalacak -> fail DEĞİL
            for (int i = 0; i < m_Slots.Count; i++)
            {
                var slot = m_Slots[i];
                if (slot == null || !slot.gameObject.activeInHierarchy) continue;

                ShipController ship = slot.DockedShip;
                if (ship == null) return false;

                if (ship.IsFull || !ship.CanAcceptMore)
                {
                    return false;
                }

                if (GetRemainingCountForColor(ship.ShipColor) == 0)
                {
                    return false;
                }
            }

            // c) Dış hatta (exposed) eşleşen küpü olan herhangi bir gemi var mı?
            // Tek bir birleşik dış hat taraması ile kontrol edilir.
            if (HasAnyDockedShipExposedCube(m_Slots))
            {
                return false;
            }

            // Tüm slotlar dolu VE hiçbir gemi hamle yapamıyor, küp çekemiyor, hareket edemiyor!
            return true;
        }

        public void TriggerLevelFail()
        {
            if (m_IsLevelFailed || m_LevelEndPending) return;

            m_IsLevelFailed = true;
            m_DeadlockTimer = 0f;

            Debug.Log("<color=#FF3333><b>[ShipDispatcher]</b></color> ❌ SEVİYE BAŞARISIZ! Tüm slotlar dolu ve hamle yapılamıyor.");

            SetTurboSpeed(false);

            if (CasualHudController.Instance != null)
            {
                CasualHudController.Instance.ShowLevelFailPopup();
            }
            else
            {
                LandFlowLoadingScreen screen = LandFlowLoadingScreen.Instance ?? LandFlowLoadingScreen.EnsureInstance();
                if (screen != null)
                {
                    screen.ShowFailLevelAndRestart();
                }
            }
        }

        #endregion

        /// <summary>
        /// Küpler bitince çağrılır: artık toplanacak kargo kalmadığı için kuyrukta bekleyen (henüz
        /// yanaşmamış) gemileri hemen kaldırır. Sahnede hâlâ aktif olan gemilerden doğal olarak zaten
        /// kalkışa geçmiş olan (son kargoyla dolan gemi) kendi başına gider; geri kalanlar HEPSİ AYNI
        /// ANDA değil, kısa aralıklarla (kademeli) kalkışa zorlanır. Her geminin gerçekten sahneyi
        /// terk etmesi (OnDeparted) beklenir; son gemi de ayrıldığında bir sonraki seviyeye geçilir ve
        /// gemi kuyruğu o seviye için yeniden doldurulur.
        /// </summary>
        private void BeginLevelEndSequence()
        {
            m_LevelEndPending = true;

            var allShips = ShipController.ActiveShips;
            HashSet<ShipController> queueShips = m_QueuePool != null
                ? new HashSet<ShipController>(m_QueuePool.WaitingShips)
                : new HashSet<ShipController>();

            // Henüz yanaşmamış, kuyrukta bekleyen gemilerin artık toplayacağı kargo yok — bunları bekletmeden kaldır.
            if (m_QueuePool != null)
            {
                m_QueuePool.ClearQueue();
            }

            List<ShipController> shipsToForceDepart = new List<ShipController>();

            m_ShipsAwaitingDeparture = 0;
            for (int i = 0; i < allShips.Count; i++)
            {
                var ship = allShips[i];
                if (ship == null || queueShips.Contains(ship)) continue;

                m_ShipsAwaitingDeparture++;
                ship.OnDeparted += HandleShipDepartedDuringLevelEnd;

                // Zaten kalkışta olan (ör. son kargoyla az önce dolup kendi kendine kalkan) gemiye
                // dokunma — sadece hâlâ yanaşık duran gemileri kademeli kalkışa zorla.
                if (!ship.IsDeparting)
                {
                    shipsToForceDepart.Add(ship);
                }
            }

            if (shipsToForceDepart.Count > 0)
            {
                StartCoroutine(StaggeredForceDeparture(shipsToForceDepart));
            }

            if (m_ShipsAwaitingDeparture == 0)
            {
                CompleteLevelTransition();
            }
        }

        /// <summary>
        /// Kalan gemileri hepsi aynı anda değil, aralarında küçük bir gecikmeyle tek tek kalkışa
        /// zorlar — böylece "hepsi bir anda sahneyi terk etti" hissi yerine doğal, art arda bir
        /// kalkış sırası oluşur.
        /// </summary>
        private IEnumerator StaggeredForceDeparture(List<ShipController> ships)
        {
            const float staggerDelay = 0.25f;
            foreach (var ship in ships)
            {
                if (ship != null && !ship.IsDeparting)
                {
                    ship.DepartAndFreeSlot();
                }
                yield return new WaitForSeconds(staggerDelay);
            }
        }

        private void HandleShipDepartedDuringLevelEnd(ShipController ship)
        {
            ship.OnDeparted -= HandleShipDepartedDuringLevelEnd;
            m_ShipsAwaitingDeparture = Mathf.Max(0, m_ShipsAwaitingDeparture - 1);

            if (m_ShipsAwaitingDeparture == 0)
            {
                CompleteLevelTransition();
            }
        }

        private void CompleteLevelTransition()
        {
            SetTurboSpeed(false);
            m_IsAutoPlacing = false;
            Debug.Log("<color=#00FFAA><b>[ShipDispatcher]</b></color> 🚢 Son gemi de sahneyi terk etti — seviye tamamlandı.");

            PixelLevelData level = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : null;
            int reward = m_LevelCompleteCoins * (level != null && level.IsHardLevel ? 2 : 1);

            if (CasualHudController.Instance != null)
            {
                CasualHudController.Instance.AddCoins(reward);
                CasualHudController.Instance.HideLevelCompletePopup();
            }

            AdvanceToNextLevel(reward);
        }

        private void AdvanceToNextLevel(int reward = 0)
        {
            LandFlowLoadingScreen screen = LandFlowLoadingScreen.Instance ?? LandFlowLoadingScreen.EnsureInstance();
            if (screen != null)
            {
                string rewardText = reward > 0 ? $"+{reward} COINS" : null;
                screen.ShowLevelCompleteAndLoad(() =>
                {
                    m_LevelEndPending = false;
                    if (LevelManager.Instance != null)
                    {
                        LevelManager.Instance.NextLevel();
                    }

                    if (m_QueuePool != null)
                    {
                        m_QueuePool.InitializeQueue();
                    }
                }, rewardText);
            }
            else
            {
                m_LevelEndPending = false;
                if (LevelManager.Instance != null)
                {
                    LevelManager.Instance.NextLevel();
                }

                if (m_QueuePool != null)
                {
                    m_QueuePool.InitializeQueue();
                }
            }
        }
    }
}
