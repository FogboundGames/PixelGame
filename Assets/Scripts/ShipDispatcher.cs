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
        /// Renk karşılaştırması için toleranslı renk eşleşmesi (RGB farkı < 0.18f).
        /// </summary>
        public static bool ColorsMatch(Color a, Color b)
        {
            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;
            return (dr * dr + dg * dg + db * db) < 0.035f;
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
                    if (!ship.IsDeparting && !ship.IsFull && ColorsMatch(ship.ShipColor, cubeColor))
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

            return CanPop(cube.CurrentColor);
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

                if (ColorsMatch(cube.CurrentColor, shipColor))
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
                    if (ship != null && !ship.IsDeparting && !ship.IsFull && !m_ActiveExtractingShips.Contains(ship))
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
                while (ship != null && !ship.IsDeparting && !ship.IsFull)
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
                    exposedCubes.Sort((a, b) =>
                    {
                        float distA = (a.transform.position - shipPos).sqrMagnitude;
                        float distB = (b.transform.position - shipPos).sqrMagnitude;
                        return distA.CompareTo(distB);
                    });

                    // En uygun dış küpü al
                    PixelCube targetCube = exposedCubes[0];
                    s_ReservedCubes.Add(targetCube);

                    Vector3 cubeStartPos = targetCube.transform.position;
                    Color cubeColor = targetCube.CurrentColor;
                    Vector3 cubeScale = targetCube.transform.lossyScale;

                    targetCube.SetPoppedVisualState(true);

                    PixelCubeInteraction interaction = PixelCubeInteraction.Instance != null
                        ? PixelCubeInteraction.Instance
                        : Object.FindFirstObjectByType<PixelCubeInteraction>();
                    if (interaction != null) interaction.RegisterPoppedCube(targetCube);

                    // 2 Aşamalı İskele Zıplama Uçuşu Başlat (Pano -> İskele -> Gemi)
                    StartCoroutine(FlyCubeThroughPierToShip(cubeStartPos, cubeColor, cubeScale.x * 0.45f, ship, targetCube));

                    yield return new WaitForSeconds(0.12f); // Seri ve tatlı zıplama ritmi

                    // Bir küp patlatıldığında içerideki küpler dışarı açılmış olabilir!
                    // Bekleyen diğer gemileri tetikle
                    TriggerWaitingShipsCheck();
                }

                // Gemi dolduysa kalkış yap
                if (ship != null && ship.IsFull && !ship.IsDeparting)
                {
                    yield return new WaitForSeconds(0.2f);
                    if (ship != null && !ship.IsDeparting)
                    {
                        ship.DepartAndFreeSlot();
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

        private IEnumerator FlyCubeThroughPierToShip(Vector3 startPos, Color color, float size, ShipController ship, PixelCube sourceCube)
        {
            // 1. Parlak, Canlı 3D Voksel Küp Nesnesi
            GameObject flyerObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flyerObj.name = "RadiantFlying_Voxel";
            flyerObj.transform.position = startPos;
            float baseScale = Mathf.Clamp(size, 0.20f, 0.36f);
            flyerObj.transform.localScale = Vector3.one * baseScale;

            Collider col = flyerObj.GetComponent<Collider>();
            if (col != null) Destroy(col);

            MeshRenderer mr = flyerObj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Shader shader = Shader.Find("Toony Colors Pro 2/PixelGame/Cartoon");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

                Material mat = new Material(shader);
                Color brightColor = Color.Lerp(color, Color.white, 0.28f);
                mat.color = brightColor;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", brightColor);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", brightColor);

                // Parlama (Emission / HDR Glow)
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * 2.8f);

                // TCP2 Toon Plastik Vurgusu
                if (mat.HasProperty("_HColor")) mat.SetColor("_HColor", Color.white);
                if (mat.HasProperty("_SColor")) mat.SetColor("_SColor", Color.Lerp(color, Color.black, 0.25f));
                if (mat.HasProperty("_SpecularColor")) mat.SetColor("_SpecularColor", Color.white);
                if (mat.HasProperty("_SpecularRoughnessPBR")) mat.SetFloat("_SpecularRoughnessPBR", 0.25f);
                if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", Color.Lerp(color, Color.white, 0.65f));

                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // 2. Işıltılı Kuyruk Efekti (Trail Renderer - Kuyruklu Yıldız Görünümü)
            TrailRenderer tr = flyerObj.AddComponent<TrailRenderer>();
            tr.time = 0.24f;
            tr.minVertexDistance = 0.02f;
            tr.autodestruct = false;

            // Genişlik Eğrisi: Başlangıçta küp kalınlığında, geriye doğru zarifçe incelerek sönen kuyruk
            AnimationCurve widthCurve = new AnimationCurve();
            widthCurve.AddKey(0f, baseScale * 0.85f);
            widthCurve.AddKey(0.4f, baseScale * 0.55f);
            widthCurve.AddKey(1f, 0f);
            tr.widthCurve = widthCurve;

            Shader trailShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            Material trailMat = new Material(trailShader);
            tr.material = trailMat;

            // Renk Gradyanı: Beyazımsı parlak çekirdek -> Canlı küp rengi -> Şeffaf altın ışıltı
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.Lerp(color, Color.white, 0.65f), 0.0f),
                    new GradientColorKey(color, 0.35f),
                    new GradientColorKey(Color.Lerp(color, new Color(1f, 0.9f, 0.3f), 0.45f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.95f, 0.0f),
                    new GradientAlphaKey(0.70f, 0.35f),
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

            // Başlangıçta enerjik minik fırlama boyutu (Pop Scale)
            flyerObj.transform.localScale = Vector3.one * (baseScale * 1.35f);
            flyerObj.transform.DOScale(Vector3.one * baseScale, 0.15f).SetEase(Ease.OutBack);

            // ==========================================
            // 1. AŞAMA: Pano -> Ahşap İskele (Pier Hop)
            // ==========================================
            float pierY = -0.32f; // Ahşap iskele bölgesi
            float targetShipX = (ship != null) ? ship.transform.position.x : startPos.x;
            Vector3 pierLandingPos = new Vector3(
                Mathf.Lerp(startPos.x, targetShipX, 0.55f) + Random.Range(-0.12f, 0.12f),
                pierY + Random.Range(-0.08f, 0.08f),
                0.02f
            );

            float stage1Duration = 0.28f;
            float elapsed = 0f;

            while (elapsed < stage1Duration && flyerObj != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / stage1Duration);

                // Dinamik parabolik yay ve hız eğrisi
                Vector3 current = Vector3.Lerp(startPos, pierLandingPos, t);
                float arc = Mathf.Sin(t * Mathf.PI) * 0.65f;
                current.y += arc;

                flyerObj.transform.position = current;
                flyerObj.transform.Rotate(randomTorque * Time.deltaTime, Space.Self);
                yield return null;
            }

            // İskelede minik zıplama / tahta teması esnemesi (Squash & Stretch)
            if (flyerObj != null)
            {
                flyerObj.transform.position = pierLandingPos;
                flyerObj.transform.DOPunchScale(new Vector3(0.35f, -0.25f, 0.35f) * baseScale, 0.09f, 3, 0.6f);
            }

            yield return new WaitForSeconds(0.04f);

            // ==========================================
            // 2. AŞAMA: Ahşap İskele -> Gemi Güvertesi (Ship Hop)
            // ==========================================
            Vector3 shipTargetPos = (ship != null) ? ship.transform.position + new Vector3(0f, 0.22f, 0.02f) : pierLandingPos;
            float stage2Duration = 0.30f;
            elapsed = 0f;
            Vector3 stage2Start = (flyerObj != null) ? flyerObj.transform.position : pierLandingPos;

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

            // 3. Gemiye Ulaşma & Şık İniş Efekti (Landing Splash & Ship Hull Punch)
            if (ship != null)
            {
                ship.AddCargo(1);

                // Geminin gövdesine enerjik iniş yaylanması
                ship.transform.DOPunchScale(new Vector3(0.06f, -0.05f, 0.06f), 0.18f, 3, 0.5f);

                // Canlı su dalgacığı ve parlama efekti
                ShipController.SpawnWaterRipple(ship.transform.position + new Vector3(0f, -0.05f, 0.05f), 0.24f, 0.95f, 0.45f);
            }

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

            StartCoroutine(FlyCubeThroughPierToShip(worldStart, shardColor, cubeScale.x * 0.45f, targetShip, null));
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
                    if (!ship.IsDeparting && !ship.IsFull && ColorsMatch(ship.ShipColor, color))
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
        /// Seviyedeki aktif henüz patlatılmamış küplerin renklerinden birini döndürür.
        /// </summary>
        public Color GetRemainingLevelColor()
        {
            if (m_Generator == null) m_Generator = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (m_Generator == null || m_Generator.CubesContainer == null) return Color.clear;

            var cubes = m_Generator.CubesContainer.GetComponentsInChildren<PixelCube>(false);
            Dictionary<Color, int> colorCounts = new Dictionary<Color, int>();

            foreach (var cube in cubes)
            {
                if (cube != null && !cube.IsPopped && cube.gameObject.activeSelf && !s_ReservedCubes.Contains(cube))
                {
                    Color c = cube.CurrentColor;
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
            return Color.clear;
        }

        public int GetRemainingCountForColor(Color targetColor)
        {
            if (m_Generator == null) m_Generator = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (m_Generator == null || m_Generator.CubesContainer == null) return 0;

            var cubes = m_Generator.CubesContainer.GetComponentsInChildren<PixelCube>(false);
            int count = 0;
            foreach (var cube in cubes)
            {
                if (cube != null && !cube.IsPopped && cube.gameObject.activeSelf && !s_ReservedCubes.Contains(cube))
                {
                    if (ColorsMatch(cube.CurrentColor, targetColor))
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Tablodaki tüm küpler patlatıldıysa seviye tamamlanır.
        /// </summary>
        public void CheckWinCondition()
        {
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

            if (unpoppedCount == 0)
            {
                Debug.Log("<color=#00FFAA><b>[ShipDispatcher]</b></color> 🎉 TEBRİKLER! Tüm piksel resmi tamamlandı!");
                if (LevelManager.Instance != null)
                {
                    LevelManager.Instance.NextLevel();
                }
            }
        }
    }
}
