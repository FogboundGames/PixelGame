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

        /// <summary>
        /// Geminin iki şeridini kurar: küpler panonun SOLUNDAN ve SAĞINDAN eşit sayıda
        /// gelir, önce panonun altındaki ORTA toplanma noktasında birleşir, oradan
        /// kıyıya iner. Bütün parçalar Catmull-Rom eğrisi — hiçbir yerde düz çizgi yok.
        /// </summary>
        private ShipLaneState GetOrBuildLaneState(ShipController ship, Vector3 sampleCubePos)
        {
            if (ship != null && m_LaneStates.TryGetValue(ship, out var cached) && cached != null)
                return cached;

            const float pierSurfaceZ = -0.65f;
            const float pierY = -0.32f;

            float boardBottomY = sampleCubePos.y;
            float boardMinX = sampleCubePos.x, boardMaxX = sampleCubePos.x;
            var cubes = Object.FindObjectsByType<PixelCube>(FindObjectsSortMode.None);
            if (cubes.Length > 0)
            {
                float minY = float.MaxValue, mnX = float.MaxValue, mxX = float.MinValue;
                foreach (var c in cubes)
                {
                    if (c == null || c.IsPopped) continue;
                    var p = c.transform.position;
                    minY = Mathf.Min(minY, p.y);
                    mnX = Mathf.Min(mnX, p.x);
                    mxX = Mathf.Max(mxX, p.x);
                }
                if (minY < float.MaxValue) { boardBottomY = minY; boardMinX = mnX; boardMaxX = mxX; }
            }

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
        /// Bir küp için sıradaki tarafı seçer (sol/sağ dönüşümlü) ve o taraftaki şeride
        /// giriş anını ayırır. Kapasite 10 ise 5 soldan 5 sağdan gelir.
        /// </summary>
        private void ReserveSideAndEntry(ShipController ship, Vector3 sampleCubePos, float cubeWorldSize,
                                         out bool useLeft, out ShoreLanePath lane, out float entryTime)
        {
            var st = GetOrBuildLaneState(ship, sampleCubePos);
            useLeft = (st.Toggle++ % 2) == 0;
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

        private void Awake()
        {
            s_Instance = this;
            EnsureReferences();
        }

        private void OnEnable()
        {
            s_Instance = this;
            EnsureReferences();
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
            return (dr * dr + dg * dg + db * db) < 0.09f;
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

        private IEnumerator ExtractMatchingCubesToShipRoutine(ShipController ship)
        {
            if (ship == null || ship.IsDeparting) yield break;
            if (m_ActiveExtractingShips.Contains(ship)) yield break;

            m_ActiveExtractingShips.Add(ship);

            try
            {
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

                    // Dıştaki küpleri gemiye en yakın olandan uzağa doğru sırala (doğal çekim sırası)
                    Vector3 shipPos = ship.transform.position;
                    // Sıradaki taraf (sol/sağ dönüşümlü) ve o tarafın şeridi.
                    // Kapasite 10 ise 5 küp soldan, 5 küp sağdan gelir.
                    var laneStateProbe = GetOrBuildLaneState(ship, exposedCubes[0].transform.position);
                    bool wantLeft = (laneStateProbe.Toggle % 2) == 0;
                    float midX = laneStateProbe.MidPoint.x;

                    // Önce istenen taraftaki küplere bak; o tarafta kalmadıysa diğer tarafı kullan.
                    exposedCubes.Sort((a, b) =>
                    {
                        bool aSide = (a.transform.position.x <= midX) == wantLeft;
                        bool bSide = (b.transform.position.x <= midX) == wantLeft;
                        if (aSide != bSide) return aSide ? -1 : 1;   // doğru taraf önce
                        // Aynı taraftaysa çıkışa (kendi kenarına) en yakın olan önce
                        float ea = Mathf.Abs(a.transform.position.x - midX);
                        float eb = Mathf.Abs(b.transform.position.x - midX);
                        return eb.CompareTo(ea);                      // kenara yakın = midX'ten uzak
                    });

                    PixelCube targetCube = exposedCubes[0];

                    // Küpü panodan koparmadan ÖNCE gemide yer ayır. Yer yoksa hiç koparma —
                    // eskiden kapasite yalnızca varışta kontrol edildiği için fazladan küp
                    // panodan siliniyor ama gemiye yazılamıyordu (boşa gidiyorlardı).
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

                    // Taraf ve şerit giriş sırasını ayır, sonra uçuşu başlat.
                    ReserveSideAndEntry(ship, cubeStartPos, cubeScale.x, out bool useLeft, out ShoreLanePath lane, out _);

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
            // 1. AŞAMA: Pano -> ORTAK ŞERİT -> Kıyı
            // ==========================================
            // Referans oyundaki gibi bütün küpler TEK bir eğriyi takip eder ve tek sıra
            // halinde, bir küp boyu aralıkla akar. Eskiden her küp kendi panodaki
            // yerinden kıyıya kendi düz çizgisiyle gidiyordu (yol 2.35-7.37 birim
            // arasında değişiyordu), o yüzden yelpaze gibi açılıyor, şerit oluşmuyordu.
            if (lane == null)
            {
                ReserveSideAndEntry(ship, startPos, cubeWorldSize, out useLeft, out lane, out _);
            }
            Vector3 pierLandingPos = lane.End;

            // --- 1A. ÇIKIŞ YAYI: küp yuvasından şeridin başına KAVİSLE gider ---
            // Düz çizgi istenmiyor: kontrol noktası panonun dışına doğru itilerek küp
            // resmin üzerinden kestirme gitmek yerine dışarı doğru bir yay çizer.
            Vector3 laneEntry = lane.Start;
            float outward = useLeft ? -1f : 1f;
            Vector3 exitCtrl = new Vector3(
                laneEntry.x + outward * 0.45f,
                Mathf.Lerp(startPos.y, laneEntry.y, 0.35f),
                laneEntry.z);

            var exitArc = ShoreLanePath.BuildThrough(new List<Vector3> { startPos, exitCtrl, laneEntry }, 20);

            float travelledArc = 0f;
            Vector3 prevP = startPos;
            while (travelledArc < exitArc.Length && flyerObj != null)
            {
                travelledArc += WalkSpeedUnitsPerSecond * Time.deltaTime;
                Vector3 p = exitArc.PointAtDistance(travelledArc);
                Vector3 d = p - prevP;
                flyerObj.transform.position = p;
                walker.Walk(Time.deltaTime, WalkSpeedUnitsPerSecond, d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.down);
                prevP = p;
                yield return null;
            }

            // --- 1B. Kuyruk: sırası gelene kadar şeridin gerisinde bekler ---
            Vector3 laneDir0 = (lane.PointAtDistance(Mathf.Min(0.05f, lane.Length)) - laneEntry).normalized;
            if (laneDir0.sqrMagnitude < 1e-6f) laneDir0 = Vector3.down;

            float laneGap = Mathf.Max(0.02f, cubeWorldSize / WalkSpeedUnitsPerSecond);
            float laneEntryTime = ReserveLaneEntry(ship, useLeft, cubeWorldSize);

            while (flyerObj != null)
            {
                float remaining = laneEntryTime - Time.time;
                float cubesAhead = Mathf.Max(0f, remaining / laneGap);
                Vector3 queueSlot = laneEntry - laneDir0 * (cubesAhead * cubeWorldSize);

                Vector3 cur = flyerObj.transform.position;
                Vector3 step = Vector3.MoveTowards(cur, queueSlot, WalkSpeedUnitsPerSecond * Time.deltaTime);
                Vector3 moveDir = step - cur;
                flyerObj.transform.position = step;
                walker.Walk(Time.deltaTime, WalkSpeedUnitsPerSecond,
                            moveDir.sqrMagnitude > 1e-8f ? moveDir.normalized : laneDir0);

                if (remaining <= 0f)
                {
                    if (Vector3.Distance(flyerObj.transform.position, laneEntry) <= cubeWorldSize * 0.5f)
                    {
                        flyerObj.transform.position = laneEntry;
                        break;
                    }
                    laneEntryTime = ReserveLaneEntry(ship, useLeft, cubeWorldSize);
                }

                yield return null;
            }

            // --- 1C. Ortak şerit üzerinde sabit hızla ilerleme ---
            float laneLength = lane.Length;
            float travelled = 0f;
            Vector3 lastPos = flyerObj != null ? flyerObj.transform.position : laneEntry;

            while (travelled < laneLength && flyerObj != null)
            {
                travelled += WalkSpeedUnitsPerSecond * Time.deltaTime;
                Vector3 p = lane.PointAtDistance(travelled);
                Vector3 dir = (p - lastPos);
                flyerObj.transform.position = p;
                walker.Walk(Time.deltaTime, WalkSpeedUnitsPerSecond, dir.sqrMagnitude > 1e-8f ? dir.normalized : laneDir0);
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

            ReserveSideAndEntry(targetShip, worldStart, cubeScale.x, out bool useLeft, out ShoreLanePath lane, out _);
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
        /// Seviye panosu o an hazır değilse veya boşsa, aktif bölümün (Level) paletindeki gerçek renklerden birini seçer.
        /// </summary>
        public Color GetRemainingLevelColor()
        {
            // 1. Önce DIŞTA (hemen toplanabilir) olan renklere öncelik ver!
            var exposed = GetExposedLevelColors();
            if (exposed != null && exposed.Count > 0)
            {
                return exposed[Random.Range(0, exposed.Count)];
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
                        return keys[Random.Range(0, keys.Count)];
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
                return entry.targetColor != Color.clear ? entry.targetColor : entry.originalColor;
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
