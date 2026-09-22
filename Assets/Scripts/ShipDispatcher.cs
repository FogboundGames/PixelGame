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
        /// Piksel küp patlatıldığında çağrılır. Küp parçacıklarını slottaki eşleşen gemiye doğru uçurur.
        /// </summary>
        public void NotifyCubePopped(Color cubeColor, Vector3 worldStart, Color shardColor, Vector3 cubeScale, Quaternion cubeRot)
        {
            ShipController targetShip = FindMatchingDockedShip(cubeColor);
            if (targetShip == null) return;

            // Kargo parçacığını kavisle gemiye fırlat
            StartCoroutine(FlyShardToShipRoutine(worldStart, targetShip, shardColor, cubeScale.x * 0.45f));
        }

        private IEnumerator FlyShardToShipRoutine(Vector3 startPos, ShipController ship, Color color, float size)
        {
            // 3D Voksel uçuş nesnesi oluştur
            GameObject flyerObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flyerObj.name = "ShipCargo_Flyer";
            flyerObj.transform.position = startPos;
            flyerObj.transform.localScale = Vector3.one * Mathf.Clamp(size, 0.15f, 0.35f);

            Collider col = flyerObj.GetComponent<Collider>();
            if (col != null) Destroy(col);

            MeshRenderer mr = flyerObj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = color;
                mr.sharedMaterial = mat;
            }

            Vector3 randomTorque = new Vector3(
                Random.Range(-360f, 360f),
                Random.Range(-360f, 360f),
                Random.Range(-360f, 360f)
            );

            float elapsed = 0f;
            Vector3 endPos = (ship != null) ? ship.transform.position + new Vector3(0f, 0.2f, 0f) : startPos;

            while (elapsed < m_FlyDuration && flyerObj != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / m_FlyDuration);

                if (ship != null)
                {
                    endPos = ship.transform.position + new Vector3(0f, 0.2f, 0f);
                }

                Vector3 current = Vector3.Lerp(startPos, endPos, t);
                // Kavisli yay (Parabolic Arc)
                float arc = Mathf.Sin(t * Mathf.PI) * m_ArcHeight;
                current.y += arc;

                flyerObj.transform.position = current;
                flyerObj.transform.Rotate(randomTorque * Time.deltaTime, Space.Self);

                yield return null;
            }

            if (flyerObj != null)
            {
                Destroy(flyerObj);
            }

            if (ship != null)
            {
                ship.AddCargo(1);
            }

            CheckWinCondition();
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

            // 3. Kuyruktan çıkar ve slota gönder
            if (m_QueuePool != null)
            {
                m_QueuePool.RemoveShipFromQueue(ship);
            }

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
            List<Color> remainingColors = new List<Color>();

            foreach (var cube in cubes)
            {
                if (cube != null && !cube.IsPopped && cube.gameObject.activeSelf)
                {
                    Color c = cube.CurrentColor;
                    bool exists = false;
                    foreach (var rc in remainingColors)
                    {
                        if (ColorsMatch(rc, c)) { exists = true; break; }
                    }
                    if (!exists) remainingColors.Add(c);
                }
            }

            if (remainingColors.Count > 0)
            {
                return remainingColors[Random.Range(0, remainingColors.Count)];
            }
            return Color.clear;
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
