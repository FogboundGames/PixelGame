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

        private static readonly HashSet<PixelCube> s_ReservedCubes = new HashSet<PixelCube>();

        /// <summary>
        /// Gemi bir slota yanaştığında çağrılır.
        /// Çizilen görseldeki eşleşen renkteki küpleri iskeleyi kullanarak gemiye zıplatır.
        /// </summary>
        public void OnShipDocked(ShipController ship)
        {
            if (ship == null || ship.IsDeparting) return;
            StartCoroutine(ExtractMatchingCubesToShipRoutine(ship));
        }

        private IEnumerator ExtractMatchingCubesToShipRoutine(ShipController ship)
        {
            if (ship == null) yield break;

            // 1. Geminin rengiyle eşleşen, henüz patlatılmamış ve rezerve edilmemiş küpleri bul
            if (m_Generator == null) m_Generator = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (m_Generator == null || m_Generator.CubesContainer == null) yield break;

            var allCubes = m_Generator.CubesContainer.GetComponentsInChildren<PixelCube>(false);
            List<PixelCube> matchingCubes = new List<PixelCube>();

            foreach (var cube in allCubes)
            {
                if (cube != null && !cube.IsPopped && cube.gameObject.activeSelf && !s_ReservedCubes.Contains(cube))
                {
                    if (ColorsMatch(cube.CurrentColor, ship.ShipColor))
                    {
                        matchingCubes.Add(cube);
                    }
                }
            }

            // Aşağıdan yukarıya doğru (iskeleye en yakın olanlardan başlayarak) çekici bir sırayla aksınlar
            matchingCubes.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));

            int cubesToExtract = Mathf.Min(ship.RemainingCapacity, matchingCubes.Count);
            if (cubesToExtract <= 0)
            {
                // Slotta eşleşen küp kalmadıysa kısa bir süre sonra gemi kalkış yapsın
                if (matchingCubes.Count == 0 && !ship.IsDeparting)
                {
                    yield return new WaitForSeconds(0.35f);
                    if (ship != null && !ship.IsDeparting) ship.DepartAndFreeSlot();
                }
                yield break;
            }

            for (int i = 0; i < cubesToExtract; i++)
            {
                PixelCube cube = matchingCubes[i];
                if (cube == null || cube.IsPopped || s_ReservedCubes.Contains(cube)) continue;
                if (ship == null || ship.IsDeparting || ship.IsFull) break;

                s_ReservedCubes.Add(cube);

                // Küpü panodan patlatıp gizle
                Vector3 cubeStartPos = cube.transform.position;
                Color cubeColor = cube.CurrentColor;
                Vector3 cubeScale = cube.transform.lossyScale;

                cube.SetPoppedVisualState(true);

                PixelCubeInteraction interaction = PixelCubeInteraction.Instance != null
                    ? PixelCubeInteraction.Instance
                    : Object.FindFirstObjectByType<PixelCubeInteraction>();
                if (interaction != null) interaction.RegisterPoppedCube(cube);

                // 2 Aşamalı İskele Zıplama Uçuşu Başlat (Pano -> İskele -> Gemi)
                StartCoroutine(FlyCubeThroughPierToShip(cubeStartPos, cubeColor, cubeScale.x * 0.45f, ship, cube));

                yield return new WaitForSeconds(0.12f); // Seri ve tatlı zıplama ritmi
            }
        }

        private IEnumerator FlyCubeThroughPierToShip(Vector3 startPos, Color color, float size, ShipController ship, PixelCube sourceCube)
        {
            // 3D Voksel küp nesnesi
            GameObject flyerObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flyerObj.name = "PierJumping_Voxel";
            flyerObj.transform.position = startPos;
            flyerObj.transform.localScale = Vector3.one * Mathf.Clamp(size, 0.18f, 0.32f);

            Collider col = flyerObj.GetComponent<Collider>();
            if (col != null) Destroy(col);

            MeshRenderer mr = flyerObj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Shader shader = Shader.Find("Toony Colors Pro 2/PixelGame/Cartoon");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                Material mat = new Material(shader);
                mat.color = color;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                mr.sharedMaterial = mat;
            }

            Vector3 randomTorque = new Vector3(
                Random.Range(-280f, 280f),
                Random.Range(-280f, 280f),
                Random.Range(-280f, 280f)
            );

            // ==========================================
            // 1. AŞAMA: Pano -> Ahşap İskele (Pier Hop)
            // ==========================================
            float pierY = -0.32f; // Ahşap iskele bölgesi
            float targetShipX = (ship != null) ? ship.transform.position.x : startPos.x;
            Vector3 pierLandingPos = new Vector3(
                Mathf.Lerp(startPos.x, targetShipX, 0.55f) + Random.Range(-0.12f, 0.12f),
                pierY + Random.Range(-0.10f, 0.10f),
                0.02f
            );

            float stage1Duration = 0.28f;
            float elapsed = 0f;

            while (elapsed < stage1Duration && flyerObj != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / stage1Duration);

                Vector3 current = Vector3.Lerp(startPos, pierLandingPos, t);
                float arc = Mathf.Sin(t * Mathf.PI) * 0.55f;
                current.y += arc;

                flyerObj.transform.position = current;
                flyerObj.transform.Rotate(randomTorque * Time.deltaTime, Space.Self);
                yield return null;
            }

            // İskelede minik zıplama / tahta teması efekti
            if (flyerObj != null)
            {
                flyerObj.transform.position = pierLandingPos;
                flyerObj.transform.DOPunchScale(new Vector3(0.12f, -0.12f, 0.12f) * size, 0.08f, 2, 0.5f);
            }

            yield return new WaitForSeconds(0.04f);

            // ==========================================
            // 2. AŞAMA: Ahşap İskele -> Gemi Güvertesi (Ship Hop)
            // ==========================================
            Vector3 shipTargetPos = (ship != null) ? ship.transform.position + new Vector3(0f, 0.22f, 0.02f) : pierLandingPos;
            float stage2Duration = 0.32f;
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
                float arc = Mathf.Sin(t * Mathf.PI) * 0.75f;
                current.y += arc;

                flyerObj.transform.position = current;
                flyerObj.transform.Rotate(randomTorque * 1.5f * Time.deltaTime, Space.Self);
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

            if (ship != null)
            {
                ship.AddCargo(1);
                ShipController.SpawnWaterRipple(ship.transform.position + new Vector3(0f, -0.05f, 0.05f), 0.22f, 0.85f, 0.45f);
            }

            CheckWinCondition();
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

        /// <summary>
        /// İlk boş slotu döndürür.
        /// </summary>
        public ShipSlot FindEmptySlot()
        {
            foreach (var slot in m_Slots)
            {
                if (slot != null && slot.IsEmpty)
                {
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
