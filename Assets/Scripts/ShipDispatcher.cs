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
        [SerializeField] private ShipQueuePool m_QueuePool;

        [Header("🎨 Piksel Sanatı Bağlantısı")]
        [SerializeField] private PixelArtGenerator m_Generator;

        [Header("🚀 Kargo Uçuş Ayarları")]
        [SerializeField] private float m_FlyDuration = 0.55f;
        [SerializeField] private float m_ArcHeight = 1.2f;

        private bool m_LevelEndPending = false;
        private int m_ShipsAwaitingDeparture = 0;
        private int m_ActiveCargoFlightCount = 0;

        /// <summary>
        /// Koparılan parçanın kıyıya yürüme hızı (dünya birimi / sn). Süre buna göre
        /// mesafeden hesaplanır — sabit süre kullanılsaydı uzaktaki küpler yakındakilerden
        /// çok daha hızlı "kayar" görünürdü.
        /// </summary>
        // 0.229 birimlik küp 0.07 sn aralıkla bırakılıyorsa, aralarında tam bir küp
        // boyu kalması için hız = 0.229 / 0.07 ≈ 3.3 birim/sn olmalı. Daha yavaşta
        // şerit tıkanıp küpler birbirine biniyordu.
        private const float WalkSpeedUnitsPerSecond = 3.3f;

        /// <summary>Panodan küp koparma aralığı (sn). Referans oyunda ~65-80 ms.</summary>
        private const float CubeReleaseInterval = 0.07f;

        [Tooltip("Ortak şeridin yanal kavis miktarı (dünya birimi). 0 = düz çizgi.")]
        [SerializeField] private float m_ShoreLaneBend = 0.55f;

        /// <summary>Gemi başına iki taraflı şerit durumu.</summary>
        private class ShipLaneState
        {
            public ShoreLanePath Left;
            public ShoreLanePath Right;
            public float NextEntryLeft;
            public float NextEntryRight;
            public int Toggle;              // sırayla sol/sağ seçmek için
            public Vector3 MidPoint;
        }

        private readonly Dictionary<ShipController, ShipLaneState> m_LaneStates = new Dictionary<ShipController, ShipLaneState>();

        // Fermuar (zipper) sıralı toplama için son koparılan küplerin koordinatları ve renk takibi
        private Vector2Int? m_LastPoppedLeft = null;
        private Vector2Int? m_LastPoppedRight = null;
        private Color m_LastExtractedColor = Color.clear;

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
            m_BoardBoundsInitialized = false;
            m_LaneStates.Clear();
            m_LastPoppedLeft = null;
            m_LastPoppedRight = null;
            m_LastExtractedColor = Color.clear;
            EnsureBoardBounds(forceRefresh: true);
        }

        /// <summary>
        /// Geminin iki şeridini kurar: küpler panonun SOLUNDAN ve SAĞINDAN eşit sayıda
        /// gelir, önce panonun altındaki ORTA toplanma noktasında birleşir, oradan
        /// kıyıya iner. Bütün parçalar Catmull-Rom eğrisi — hiçbir yerde düz çizgi yok.
        /// </summary>
        private ShipLaneState GetOrBuildLaneState(ShipController ship, Vector3 sampleCubePos)
        {
            if (ship != null && m_LaneStates.TryGetValue(ship, out var cached) && cached != null)
                return cached;

            EnsureBoardBounds();

            const float pierSurfaceZ = -0.65f;
            const float pierY = -0.32f;

            float boardBottomY = m_BoardBottomY;
            float boardMinX = m_BoardMinX;
            float boardMaxX = m_BoardMaxX;

            float centerX = (boardMinX + boardMaxX) * 0.5f;
            float laneY = boardBottomY - 0.40f;
            float shipX = ship != null ? ship.transform.position.x : centerX;

            Vector3 mid   = new Vector3(centerX, laneY - 0.30f, pierSurfaceZ);
            Vector3 shore = new Vector3(Mathf.Lerp(centerX, shipX, 0.55f), pierY, pierSurfaceZ);

            // Sol kol: panonun solundan başlar, içeri kıvrılarak ortaya gelir.
            var leftPts = new List<Vector3>
            {
                new Vector3(boardMinX - 0.55f, laneY + 0.35f, pierSurfaceZ),
                new Vector3(boardMinX - 0.30f, laneY - 0.05f, pierSurfaceZ),
                new Vector3(centerX - 0.55f,   laneY - 0.28f, pierSurfaceZ),
                mid,
                new Vector3(Mathf.Lerp(centerX, shore.x, 0.5f), Mathf.Lerp(mid.y, pierY, 0.55f), pierSurfaceZ),
                shore
            };

            // Sağ kol: aynısının aynası.
            var rightPts = new List<Vector3>
            {
                new Vector3(boardMaxX + 0.55f, laneY + 0.35f, pierSurfaceZ),
                new Vector3(boardMaxX + 0.30f, laneY - 0.05f, pierSurfaceZ),
                new Vector3(centerX + 0.55f,   laneY - 0.28f, pierSurfaceZ),
                mid,
                new Vector3(Mathf.Lerp(centerX, shore.x, 0.5f), Mathf.Lerp(mid.y, pierY, 0.55f), pierSurfaceZ),
                shore
            };

            var st = new ShipLaneState
            {
                Left = ShoreLanePath.BuildThrough(leftPts),
                Right = ShoreLanePath.BuildThrough(rightPts),
                MidPoint = mid
            };
            if (ship != null) m_LaneStates[ship] = st;
            return st;
        }

        /// <summary>
        /// Bir küp için sıradaki tarafı seçer (sol/sağ dönüşümlü veya küpün konumuna göre) ve o taraftaki şeride
        /// giriş anını ayırır. Kapasite 10 ise 5 soldan 5 sağdan gelir.
        /// </summary>
        private void ReserveSideAndEntry(ShipController ship, Vector3 sampleCubePos, float cubeWorldSize,
                                         out bool useLeft, out ShoreLanePath lane, out float entryTime,
                                         bool? explicitUseLeft = null)
        {
            var st = GetOrBuildLaneState(ship, sampleCubePos);
            if (explicitUseLeft.HasValue)
            {
                useLeft = explicitUseLeft.Value;
                st.Toggle++;
            }
            else
            {
                useLeft = (st.Toggle++ % 2) == 0;
            }
            lane = useLeft ? st.Left : st.Right;

            float gap = Mathf.Max(0.02f, cubeWorldSize / WalkSpeedUnitsPerSecond);
            float now = Time.time;
            float prev = useLeft ? st.NextEntryLeft : st.NextEntryRight;
            entryTime = Mathf.Max(now, prev + gap);
            if (useLeft) st.NextEntryLeft = entryTime; else st.NextEntryRight = entryTime;
        }

        /// <summary>Şeride varışta sırayı tazeler (küp geç kaldıysa bir sonraki yeri alır).</summary>
        private float ReserveLaneEntry(ShipController ship, bool useLeft, float cubeWorldSize)
        {
            if (ship == null || !m_LaneStates.TryGetValue(ship, out var st)) return Time.time;
            float gap = Mathf.Max(0.02f, cubeWorldSize / WalkSpeedUnitsPerSecond);
            float now = Time.time;
            float prev = useLeft ? st.NextEntryLeft : st.NextEntryRight;
            float next = Mathf.Max(now, prev + gap);
            if (useLeft) st.NextEntryLeft = next; else st.NextEntryRight = next;
            return next;
        }

        /// <summary>
        /// Küpün panodaki anlık konumundan (startPos) başlayarak hedef geminin ahşap iskele
        /// hizasına kadar ana görselin içinden geçmeden, etrafından dolaşarak dosdoğru yürüyüş hattı oluşturur.
        /// </summary>
        private ShoreLanePath BuildCubeWalkPath(Vector3 startPos, ShipController ship, bool useLeft)
        {
            EnsureBoardBounds();

            const float pierSurfaceZ = -0.65f;
            const float pierY = -0.32f;

            float shipX = (ship != null) ? ship.transform.position.x : startPos.x;

            return ShoreLanePath.BuildAroundObstacle(
                startPos,
                shipX,
                m_BoardMinX,
                m_BoardMaxX,
                m_BoardBottomY,
                pierY,
                pierSurfaceZ
            );
        }

        private void Awake()
        {
            s_Instance = this;
            m_LastPoppedLeft = null;
            m_LastPoppedRight = null;
            m_LastExtractedColor = Color.clear;
            EnsureReferences();
            EnsureBoardBounds(forceRefresh: true);
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
            if (m_Slots == null || m_Slots.Count == 0)
            {
                m_Slots = new List<ShipSlot>(GetComponentsInChildren<ShipSlot>(true));
                if (m_Slots.Count == 0)
                {
                    m_Slots = new List<ShipSlot>(Object.FindObjectsByType<ShipSlot>(FindObjectsSortMode.None));
                }
            }

            if (m_QueuePool == null)
            {
                m_QueuePool = Object.FindFirstObjectByType<ShipQueuePool>();
            }

            if (m_Generator == null)
            {
                m_Generator = Object.FindFirstObjectByType<PixelArtGenerator>();
            }
        }

        /// <summary>
        /// Renk karşılaştırması için toleranslı renk eşleşmesi (RGB farkı kare toplamı < 0.09f, ~0.30f tolerans).
        /// </summary>
        public static bool ColorsMatch(Color a, Color b)
        {
            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;
            if ((dr * dr + dg * dg + db * db) < 0.12f) return true;

            // Sarı / Amber tonları için özel tolerans (küp ve gemi her koşulda %100 eşleşir):
            bool aIsYellow = a.r > 0.75f && a.g > 0.50f && a.b < 0.35f;
            bool bIsYellow = b.r > 0.75f && b.g > 0.50f && b.b < 0.35f;
            if (aIsYellow && bIsYellow) return true;

            return false;
        }

        /// <summary>
        /// Belirtilen renkteki küpün patlamasına izin var mı?
        /// Slotta o renkte henüz dolmamış bir gemi varsa true döner.
        /// </summary>
        public bool CanPop(Color cubeColor)
        {
            if (m_Slots == null || m_Slots.Count == 0) return true;

            foreach (var slot in m_Slots)
            {
                if (slot != null && !slot.IsEmpty && slot.DockedShip != null)
                {
                    ShipController ship = slot.DockedShip;
                    // IsFull DEĞİL CanAcceptMore: yolda olan (henüz varmamış) kargo da sayılır,
                    // yoksa uçuş süresi boyunca kapasitenin çok üstünde küp koparılıyor.
                    if (ship.CanAcceptMore && ColorsMatch(ship.ShipColor, cubeColor))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Küpün patlatılmasına izin var mı?
        /// 1) Küp DIŞTA olmalıdır (ortada/içte kalmış küpler patlatılamaz!).
        /// 2) Slotta bu renge uyan ve henüz dolmamış bir gemi olmalıdır.
        /// </summary>
        public bool CanPop(PixelCube cube)
        {
            if (cube == null || cube.IsPopped || !cube.gameObject.activeSelf) return false;

            // Ortadaki küpler dıştan açılmadan patlatılamaz
            if (!IsCubeExposed(cube))
            {
                cube.PlayBlockedWobble();
                return false;
            }

            return CanPop(cube.CurrentColor) || CanPop(cube.OriginalColor);
        }

        private static readonly HashSet<PixelCube> s_ReservedCubes = new HashSet<PixelCube>();
        private readonly HashSet<ShipController> m_ActiveExtractingShips = new HashSet<ShipController>();

        /// <summary>
        /// Bir küpün dış havaya temas edip etmediğini kontrol eder.
        /// Ortada/içte kalmış küpler (4 komşusu da dolu küplerle çevrili olanlar) dış küp değildir.
        /// </summary>
        public bool IsCubeExposed(PixelCube targetCube)
        {
            if (targetCube == null || targetCube.IsPopped || !targetCube.gameObject.activeSelf)
                return false;

            if (m_Generator == null) m_Generator = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (m_Generator == null || m_Generator.CubesContainer == null) return true;

            var allCubes = m_Generator.CubesContainer.GetComponentsInChildren<PixelCube>(false);
            if (allCubes == null || allCubes.Length == 0) return true;

            Dictionary<(int, int), PixelCube> gridMap = new Dictionary<(int, int), PixelCube>(allCubes.Length);
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            for (int i = 0; i < allCubes.Length; i++)
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

            if (!gridMap.ContainsKey((targetCube.GridX, targetCube.GridY)))
                return false;

            HashSet<(int, int)> outsideAir = CalculateOutsideAir(gridMap, minX, maxX, minY, maxY);

            int tx = targetCube.GridX;
            int ty = targetCube.GridY;
            return outsideAir.Contains((tx - 1, ty)) ||
                   outsideAir.Contains((tx + 1, ty)) ||
                   outsideAir.Contains((tx, ty - 1)) ||
                   outsideAir.Contains((tx, ty + 1));
        }

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

            var allCubes = m_Generator.CubesContainer.GetComponentsInChildren<PixelCube>(false);
            if (allCubes == null || allCubes.Length == 0) return result;

            Dictionary<(int, int), PixelCube> gridMap = new Dictionary<(int, int), PixelCube>(allCubes.Length);
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            for (int i = 0; i < allCubes.Length; i++)
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
                    if (ship != null && ship.CanAcceptMore && !m_ActiveExtractingShips.Contains(ship))
                    {
                        if (HasExposedMatchingCube(ship.ShipColor))
                        {
                            StartCoroutine(ExtractMatchingCubesToShipRoutine(ship));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Küpleri panodan delikli/aralıklı değil, pürüzsüz ve sıralı bir fermuar (zipper) gibi
        /// sütun sütun ve komşu komşu toplamak için sıradaki küpü seçer.
        /// </summary>
        private PixelCube PickNextSequentialCube(List<PixelCube> exposedCubes, ref bool wantLeft, float midX, Color shipColor)
        {
            if (exposedCubes == null || exposedCubes.Count == 0) return null;

            // Renk değiştiyse önceki zincir pozisyonlarını sıfırla
            if (!ColorsMatch(shipColor, m_LastExtractedColor))
            {
                m_LastPoppedLeft = null;
                m_LastPoppedRight = null;
                m_LastExtractedColor = shipColor;
            }

            bool currentWantLeft = wantLeft;

            // 1. İstenen taraftaki adayları filtrele
            List<PixelCube> candidates = exposedCubes.FindAll(c => (c.transform.position.x <= midX) == currentWantLeft);
            if (candidates.Count == 0)
            {
                // O tarafta hiç küp kalmadıysa diğer tarafa geç
                currentWantLeft = !currentWantLeft;
                candidates = exposedCubes.FindAll(c => (c.transform.position.x <= midX) == currentWantLeft);
                if (candidates.Count == 0)
                {
                    candidates = exposedCubes;
                }
            }

            Vector2Int? lastPos = currentWantLeft ? m_LastPoppedLeft : m_LastPoppedRight;
            PixelCube chosen = null;

            if (lastPos.HasValue)
            {
                Vector2Int prev = lastPos.Value;

                // A. Sütun Zinciri (Zipper):
                // 1. Aynı sütunda yukarı doğru hemen bir sonraki komşu küp: (prev.x, prev.y + 1)
                chosen = candidates.Find(c => c.GridX == prev.x && c.GridY == prev.y + 1);

                // 2. Bir üst komşu yoksa (arada küp manuel patlatıldıysa veya boşluk varsa),
                // aynı sütunda yukarıda kalan EN YAKIN küpü ara
                if (chosen == null)
                {
                    var aboveInSameCol = candidates.FindAll(c => c.GridX == prev.x && c.GridY > prev.y);
                    if (aboveInSameCol.Count > 0)
                    {
                        aboveInSameCol.Sort((a, b) => a.GridY.CompareTo(b.GridY));
                        chosen = aboveInSameCol[0];
                    }
                }

                // 3. Aynı sütunda yukarıda küp kalmadıysa ama aynı sütunda geride/aşağıda küp kaldıysa:
                if (chosen == null)
                {
                    var inSameCol = candidates.FindAll(c => c.GridX == prev.x);
                    if (inSameCol.Count > 0)
                    {
                        inSameCol.Sort((a, b) => a.GridY.CompareTo(b.GridY));
                        chosen = inSameCol[0];
                    }
                }
            }

            // 4. Zincir yoksa, ilk küpse veya o sütun tamamen bittiyse: en dış sütundan ve en alttan başla
            if (chosen == null)
            {
                candidates.Sort((a, b) =>
                {
                    if (currentWantLeft)
                    {
                        // Sol taraf: en küçük GridX (en dış sol sütun)
                        if (a.GridX != b.GridX) return a.GridX.CompareTo(b.GridX);
                    }
                    else
                    {
                        // Sağ taraf: en büyük GridX (en dış sağ sütun)
                        if (a.GridX != b.GridX) return b.GridX.CompareTo(a.GridX);
                    }
                    // Aynı sütunda: aşağıdan yukarıya (en küçük GridY önce)
                    return a.GridY.CompareTo(b.GridY);
                });

                chosen = candidates[0];
            }

            // Son konumu güncelle
            if (chosen != null)
            {
                if (currentWantLeft)
                    m_LastPoppedLeft = new Vector2Int(chosen.GridX, chosen.GridY);
                else
                    m_LastPoppedRight = new Vector2Int(chosen.GridX, chosen.GridY);
            }

            wantLeft = currentWantLeft;
            return chosen;
        }

        private IEnumerator ExtractMatchingCubesToShipRoutine(ShipController ship)
        {
            if (ship == null || ship.IsDeparting) yield break;
            if (m_ActiveExtractingShips.Contains(ship)) yield break;

            m_ActiveExtractingShips.Add(ship);

            try
            {
                int extractionStep = 0;
                while (ship != null && ship.CanAcceptMore)
                {
                    List<PixelCube> exposedCubes = GetExposedMatchingCubes(ship.ShipColor);

                    if (exposedCubes == null || exposedCubes.Count == 0)
                    {
                        // Dışta bu renkten küp yok!
                        // Seviyede bu renkten hala içeride (ortada) kilitli küp var mı kontrol et
                        int totalRemaining = GetRemainingCountForColor(ship.ShipColor);
                        if (totalRemaining == 0)
                        {
                            // Seviyedeki bu renge ait TÜM küpler zaten toplanmış, gemi daha fazla küp alamaz -> Kalkış yap
                            yield return new WaitForSeconds(0.35f);
                            // Havada hâlâ küp varsa kalkma: gemi hareket edince uçuştaki
                            // küpler onu harita dışına kadar kovalıyor ve AddCargo
                            // (IsDeparting yüzünden) onları saymadan düşürüyordu.
                            while (ship != null && ship.HasPendingCargo) yield return null;
                            if (ship != null && !ship.IsDeparting)
                            {
                                ship.DepartAndFreeSlot();
                            }
                        }
                        else
                        {
                            // "yoksa bekleyecek açılmasını"
                            // İçeride hala bu renkten küpler var ama şu an dışları kapalı!
                            // Gemi slotta bekleyecek, kalkış yapmayacak.
                        }
                        break;
                    }

                    // Sıradaki taraf (sol/sağ dönüşümlü) ve orta nokta
                    var laneStateProbe = GetOrBuildLaneState(ship, exposedCubes[0].transform.position);
                    float midX = laneStateProbe.MidPoint.x;
                    bool wantLeft = (extractionStep % 2) == 0;

                    // Küpleri aralıksız, sırayla (fermuar gibi) seç
                    PixelCube targetCube = PickNextSequentialCube(exposedCubes, ref wantLeft, midX, ship.ShipColor);
                    if (targetCube == null) break;

                    extractionStep++;

                    // Küpü panodan koparmadan ÖNCE gemide yer ayır. Yer yoksa hiç koparma
                    if (!ship.TryReserveCargo()) break;

                    s_ReservedCubes.Add(targetCube);

                    Vector3 cubeStartPos = targetCube.transform.position;
                    Color cubeColor = targetCube.CurrentColor;
                    Vector3 cubeScale = targetCube.transform.lossyScale;

                    targetCube.SetPoppedVisualState(true);

                    PixelCubeInteraction interaction = PixelCubeInteraction.Instance != null
                        ? PixelCubeInteraction.Instance
                        : Object.FindFirstObjectByType<PixelCubeInteraction>();
                    if (interaction != null) interaction.RegisterPoppedCube(targetCube);

                    // Taraf ve şerit giriş sırasını ayır (küpün kendi bulunduğu tarafın şeridini kullan)
                    bool cubeOnLeft = cubeStartPos.x <= midX;
                    ReserveSideAndEntry(ship, cubeStartPos, cubeScale.x, out bool useLeft, out ShoreLanePath lane, out _, cubeOnLeft);

                    StartCoroutine(FlyCubeThroughPierToShip(cubeStartPos, cubeColor, cubeScale.x * 0.45f, ship, targetCube, lane, cubeScale.x, useLeft));

                    // Referans oyunda akış ~12-16 küp/sn (küp başına 65-80 ms).
                    yield return new WaitForSeconds(CubeReleaseInterval);

                    // Bir küp patlatıldığında içerideki küpler dışarı açılmış olabilir!
                    // Bekleyen diğer gemileri tetikle
                    TriggerWaitingShipsCheck();
                }

                // Gemi dolduysa kalkış yap — ama önce havadaki son küpler insin.
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

        private IEnumerator FlyCubeThroughPierToShip(Vector3 startPos, Color color, float size, ShipController ship, PixelCube sourceCube, ShoreLanePath lane = null, float cubeWorldSize = 0.22f, bool useLeft = true)
        {
            // Bu küp henüz gemiye TAM ulaşmadı (uçuş animasyonu sürüyor) — seviye bitiş kontrolü
            // bu uçuşun bitmesini beklesin ki son küp koparılır kopartılmaz gemiler sahneyi
            // animasyon yarıda kesilerek terk etmesin.
            m_ActiveCargoFlightCount++;

            // 1. 3D Yürüyen Küp Nesnesi
            // Kök yalnızca konumu taşır (ölçeği 1 kalır ki TrailRenderer genişliği bozulmasın);
            // görsel gövde + bacaklar alt nesnede durur ve yürüyüşü o oynatır.
            GameObject flyerObj = new GameObject("WalkingCargoCube");
            flyerObj.transform.position = startPos;
            float baseScale = Mathf.Clamp(size, 0.20f, 0.36f);

            WalkingCargoVisual walker = WalkingCargoVisual.Attach(flyerObj, baseScale);

            // Panodaki küpün orijinal materyal ve rengini birebir uygula (fazladan parlama/glow olmasın).
            walker.ApplyColor(color);

            // 2. Hafif Kuyruk Efekti (Sadece kıyıdan gemiye zıplarken devreye girer)
            TrailRenderer tr = flyerObj.AddComponent<TrailRenderer>();
            tr.time = 0.20f;
            tr.minVertexDistance = 0.02f;
            tr.autodestruct = false;

            // Genişlik Eğrisi: Başlangıçta küp kalınlığında, geriye doğru incelerek sönen kuyruk
            AnimationCurve widthCurve = new AnimationCurve();
            widthCurve.AddKey(0f, baseScale * 0.75f);
            widthCurve.AddKey(0.4f, baseScale * 0.45f);
            widthCurve.AddKey(1f, 0f);
            tr.widthCurve = widthCurve;

            Shader trailShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            Material trailMat = new Material(trailShader);
            tr.material = trailMat;

            // Renk Gradyanı: Küpün kendi renginden yumuşakça şeffaflaşır (beyaz/altın ekstra parlama yok)
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(color, 0.0f),
                    new GradientColorKey(color, 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.6f, 0.0f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            tr.colorGradient = grad;

            // Rastgele fırıl fırıl dönme torku
            Vector3 randomTorque = new Vector3(
                Random.Range(-380f, 380f),
                Random.Range(-380f, 380f),
                Random.Range(-380f, 380f)
            );

            // Not: Doğuş "pop" ölçeği bilerek YOK. Küp panodaki yuvasından ayrılırken
            // ölçeği/rotasyonu/duruşu hiç değişmemeli; sadece konumu değişir.

            // Yürüyüş boyunca kuyruklu yıldız izi kapalı — yürüyen bir küpün arkasında
            // iz bırakması yanlış okunuyor. Kuyruk sadece kıyıdan gemiye zıplarken açılır.
            tr.emitting = false;

            // ==========================================
            // 1. AŞAMA: Pano -> Kendi Konumundan Yürüyüş -> Kıyı
            // ==========================================
            // Kullanıcı isteği: "küpler yürüme hareketine geçtiğinde oldukları yerden yürümeye başlasınlar
            // geriye gidip değil oldukları konum baz alınarak öyle hareket etsinler"
            // Küp panodaki kendi özgün konumundan (startPos) doğrudan yürümeye başlar.
            // Asla geriye gitmez; yumuşak Catmull-Rom eğrisiyle doğrudan iskeleye ve kıyıya doğru akar.
            ShoreLanePath walkPath = BuildCubeWalkPath(startPos, ship, useLeft);
            Vector3 pierLandingPos = walkPath.End;

            float pathLength = walkPath.Length;
            float travelled = 0f;
            Vector3 lastPos = startPos;

            while (travelled < pathLength && flyerObj != null)
            {
                travelled += WalkSpeedUnitsPerSecond * Time.deltaTime;
                Vector3 p = walkPath.PointAtDistance(travelled);
                Vector3 dir = (p - lastPos);
                flyerObj.transform.position = p;
                walker.Walk(Time.deltaTime, WalkSpeedUnitsPerSecond, dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector3.down);
                lastPos = p;
                yield return null;
            }

            // ------------------------------------------------------------------
            // 1C. Kıyıda çömelme — zıplamaya hazırlık
            // ------------------------------------------------------------------
            if (flyerObj != null)
            {
                flyerObj.transform.position = pierLandingPos;

                const float crouchDuration = 0.10f;
                float crouchElapsed = 0f;
                while (crouchElapsed < crouchDuration && flyerObj != null)
                {
                    crouchElapsed += Time.deltaTime;
                    // 0 -> 1 -> 0: çök, sonra yaylan
                    float ct = Mathf.Clamp01(crouchElapsed / crouchDuration);
                    walker.Crouch(Mathf.Sin(ct * Mathf.PI));
                    yield return null;
                }
                if (flyerObj != null) walker.Crouch(0f);
            }

            // ==========================================
            // 2. AŞAMA: Ahşap İskele -> Gemi Güvertesi (Ship Hop)
            // ==========================================
            Vector3 shipTargetPos = (ship != null) ? ship.transform.position + new Vector3(0f, 0.22f, 0.02f) : pierLandingPos;
            float stage2Duration = 0.30f;
            float elapsed = 0f;
            Vector3 stage2Start = (flyerObj != null) ? flyerObj.transform.position : pierLandingPos;

            // Zıplama başlıyor: kuyruklu yıldız izi burada açılır, bacaklar havada toplanır
            // ve fırıl fırıl dönüş de sadece bu aşamada uygulanır.
            if (tr != null)
            {
                tr.Clear();
                tr.emitting = true;
            }

            while (elapsed < stage2Duration && flyerObj != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / stage2Duration);

                if (ship != null)
                {
                    shipTargetPos = ship.transform.position + new Vector3(0f, 0.22f, 0.02f);
                }

                Vector3 current = Vector3.Lerp(stage2Start, shipTargetPos, t);
                float arc = Mathf.Sin(t * Mathf.PI) * 0.85f;
                current.y += arc;

                flyerObj.transform.position = current;
                flyerObj.transform.Rotate(randomTorque * 1.8f * Time.deltaTime, Space.Self);
                walker.SetAirborne(Time.deltaTime);
                yield return null;
            }

            if (flyerObj != null)
            {
                Destroy(flyerObj);
            }

            if (sourceCube != null)
            {
                s_ReservedCubes.Remove(sourceCube);
            }

            // 3. Gemiye Ulaşma & Şık İniş Efekti (Landing Splash)
            if (ship == null)
            {
                // Gemi arada yok olduysa ayrılan yer kimseye lazım değil; yine de
                // sayaç sızmasın diye bu durum AddCargo ile tüketilemez — gemi zaten yok.
            }
            else
            {
                // AddCargo rezervasyonu tüketir (gemi kalkıyor olsa bile).
                ship.AddCargo(1);

                // Not: Geminin gövdesine ayrıca bir "iniş yaylanması" (DOPunchScale) UYGULANMIYOR.
                // Kargolar art arda hızlı geldiğinde (DOKill koruması ve bitişte sıfırlama olmadan)
                // bu zıplamalar üst üste binip birbirini tam bitirmeden yenisi başlıyordu — ölçek
                // kademeli olarak sürüklenip gemi doldukça "büyüyormuş" gibi görünüyordu. Varilin
                // kendi OutBack "pop" animasyonu zaten yeterli görsel geri bildirim veriyor.

                // Canlı su dalgacığı ve parlama efekti
                ShipController.SpawnWaterRipple(ship.transform.position + new Vector3(0f, -0.05f, 0.05f), 0.24f, 0.95f, 0.45f);
                HypercasualWaterController.TriggerWaterRipple(ship.transform.position, 0.65f, 0.22f);
            }

            m_ActiveCargoFlightCount = Mathf.Max(0, m_ActiveCargoFlightCount - 1);

            CheckWinCondition();
            TriggerWaitingShipsCheck();
        }

        /// <summary>
        /// Piksel küp tıklandığında manuel olarak slottaki eşleşen gemiye doğru uçurur.
        /// </summary>
        public void NotifyCubePopped(Color cubeColor, Vector3 worldStart, Color shardColor, Vector3 cubeScale, Quaternion cubeRot)
        {
            ShipController targetShip = FindMatchingDockedShip(cubeColor);
            if (targetShip == null) return;

            // Otomatik çıkarmayla aynı kural: uçuş başlamadan gemide yer ayrılır.
            if (!targetShip.TryReserveCargo()) return;

            var st = GetOrBuildLaneState(targetShip, worldStart);
            bool cubeOnLeft = worldStart.x <= st.MidPoint.x;
            ReserveSideAndEntry(targetShip, worldStart, cubeScale.x, out bool useLeft, out ShoreLanePath lane, out _, cubeOnLeft);
            StartCoroutine(FlyCubeThroughPierToShip(worldStart, shardColor, cubeScale.x * 0.45f, targetShip, null, lane, cubeScale.x, useLeft));
        }

        /// <summary>
        /// Slotta bekleyen eşleşen gemiyi bulur.
        /// </summary>
        private ShipController FindMatchingDockedShip(Color color)
        {
            foreach (var slot in m_Slots)
            {
                if (slot != null && !slot.IsEmpty && slot.DockedShip != null)
                {
                    ShipController ship = slot.DockedShip;
                    if (ship.CanAcceptMore && ColorsMatch(ship.ShipColor, color))
                    {
                        return ship;
                    }
                }
            }
            return null;
        }

        private int m_LastAssignedSlotIndex = -1;

        /// <summary>
        /// Boş bir slot döndürür. Her zaman en soldakini seçmek yerine, bir önceki
        /// atamadan sonraki slottan başlayarak sırayla (round-robin) tarar — böylece
        /// slotlar dengeli kullanılır, sol taraf sürekli tekrar dolup sağ taraf
        /// uzun süre boş kalmaz.
        /// </summary>
        public ShipSlot FindEmptySlot()
        {
            if (m_Slots == null || m_Slots.Count == 0) return null;

            int count = m_Slots.Count;
            for (int offset = 1; offset <= count; offset++)
            {
                int index = (m_LastAssignedSlotIndex + offset) % count;
                var slot = m_Slots[index];
                if (slot != null && slot.IsEmpty)
                {
                    m_LastAssignedSlotIndex = index;
                    return slot;
                }
            }
            return null;
        }

        /// <summary>
        /// Kuyruktan tıklanan gemiyi boş bir slota göndermeyi dener.
        /// </summary>
        public bool TrySendShipFromQueue(ShipController ship)
        {
            if (ship == null || ship.IsDocked || ship.IsMoving || ship.IsDeparting) return false;

            // 1. En ön sıra kontrolü
            if (m_QueuePool != null && !m_QueuePool.IsFrontRow(ship))
            {
                ship.PlayWobble();
                return false;
            }

            // 2. Boş slot kontrolü
            ShipSlot emptySlot = FindEmptySlot();
            if (emptySlot == null)
            {
                ship.PlayWobble();
                return false;
            }

            // 3. Kuyruktan çıkar, arkadaki gemiyi öne kaydır ve açık denizden yenisini getir
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
        /// Dış havaya açık (hemen toplanabilir) küplerin renk listesini döner.
        /// </summary>
        public List<Color> GetExposedLevelColors()
        {
            List<Color> exposedColors = new List<Color>();
            if (m_Generator == null) m_Generator = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (m_Generator == null || m_Generator.CubesContainer == null) return exposedColors;

            var allCubes = m_Generator.CubesContainer.GetComponentsInChildren<PixelCube>(false);
            if (allCubes == null || allCubes.Length == 0) return exposedColors;

            Dictionary<(int, int), PixelCube> gridMap = new Dictionary<(int, int), PixelCube>(allCubes.Length);
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            for (int i = 0; i < allCubes.Length; i++)
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
            if (c.r > 0.75f && c.g > 0.50f && c.b < 0.35f)
            {
                return new Color(1.0f, 0.88f, 0.05f, 1f); // Net, saf parlak sarı
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
            if (m_Generator == null) m_Generator = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (m_Generator != null && m_Generator.CubesContainer != null)
            {
                var cubes = m_Generator.CubesContainer.GetComponentsInChildren<PixelCube>(false);
                if (cubes != null && cubes.Length > 0)
                {
                    int count = 0;
                    foreach (var cube in cubes)
                    {
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

        /// <summary>
        /// Tablodaki tüm küpler patlatıldıysa seviye tamamlanma sürecini başlatır. Seviye geçişi
        /// hemen olmaz: son gemi de sahneyi gerçekten terk edene kadar beklenir (bkz. BeginLevelEndSequence).
        /// </summary>
        public void CheckWinCondition()
        {
            if (m_LevelEndPending) return;

            if (m_Generator == null) m_Generator = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (m_Generator == null || m_Generator.CubesContainer == null) return;

            var cubes = m_Generator.CubesContainer.GetComponentsInChildren<PixelCube>(false);
            int unpoppedCount = 0;

            foreach (var cube in cubes)
            {
                if (cube != null && !cube.IsPopped && cube.gameObject.activeSelf)
                {
                    unpoppedCount++;
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
        }

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

            ShipController[] allShips = Object.FindObjectsByType<ShipController>(FindObjectsSortMode.None);
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
            foreach (var ship in allShips)
            {
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
            m_LevelEndPending = false;
            m_LastPoppedLeft = null;
            m_LastPoppedRight = null;
            m_LastExtractedColor = Color.clear;
            Debug.Log("<color=#00FFAA><b>[ShipDispatcher]</b></color> 🚢 Son gemi de sahneyi terk etti — seviye sıfırlanıyor.");

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
