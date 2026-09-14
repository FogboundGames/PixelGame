using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Vagonun kasası içinde oturan madencileri yönetir ve vagon raya girdiğinde
    /// madencilerin sırayla zıplayarak çıkıp hedeflerine koşmasını sağlar.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Miner Crew")]
    public class MinerCrew : MonoBehaviour
    {
        [Header("⚙️ Müfreze Ayarları")]
        [Tooltip("Madencilerin vagondan sırayla atlama aralığı (saniye)")]
        [SerializeField] private float m_SpawnDelay = 0.45f;

        [Tooltip("Madenci ölçek çarpanı")]
        [SerializeField] private float m_ScaleFactor = 1f;

        [Tooltip("Madenci koşu hızı (yavaşlatılmış ve dengeli)")]
        [SerializeField] private float m_RunSpeed = 0.85f;

        private TruckCargo m_Cargo;
        private GameObject m_MinerPrefab;
        private float m_ColorThreshold = 0.04f;
        private readonly List<Miner> m_SeatedMiners = new List<Miner>();
        private Coroutine m_SpawnCoroutine;
        private int m_DeployedMinersCount = 0;

        public int RemainingMinersToDeploy => m_Cargo != null ? Mathf.Max(0, m_Cargo.Capacity - m_DeployedMinersCount) : 0;

        /// <summary>
        /// Vagon kasanın üst açık hacmine uygun olarak görsel madencileri yerleştirir.
        /// </summary>
        public void PopulateSeatedMiners(
            TruckCargo cargo,
            GameObject minerPrefab,
            float scaleFactor = 1f,
            float runSpeed = 0.85f)
        {
            ClearSeatedMiners();
            m_DeployedMinersCount = 0;

            m_Cargo = cargo;
            m_MinerPrefab = minerPrefab;
            m_ScaleFactor = scaleFactor;
            m_RunSpeed = runSpeed;

            if (m_Cargo == null || m_Cargo.Capacity <= 0) return;

            Transform body = GetWagonBody(transform);
            TryGetLocalBounds(body, out Bounds bounds);

            int remaining = m_Cargo.RemainingCapacity;
            int visualCount = Mathf.Clamp(remaining, 0, 6);
            if (visualCount <= 0) return;

            int cols = 3;
            int maxRows = Mathf.CeilToInt((float)visualCount / cols);

            for (int i = 0; i < visualCount; i++)
            {
                int row = i / cols;
                int col = i % cols;

                float colOffset = cols > 1 ? ((float)col / (cols - 1) - 0.5f) : 0f;
                float rowOffset = maxRows > 1 ? ((float)row / (maxRows - 1) - 0.5f) : 0f;

                Vector3 localPos;
                float minerScale;

                if (bounds.size.x > 0.001f)
                {
                    // Vagon kasanın üst iç hacminde ferah ve düzgün konumlandır
                    float x = bounds.center.x + colOffset * (bounds.size.x * 0.45f);
                    float y = bounds.min.y + bounds.size.y * 0.70f;
                    float z = bounds.center.z + rowOffset * (bounds.size.z * 0.45f);
                    localPos = new Vector3(x, y, z);
                    minerScale = bounds.size.y * 0.42f * scaleFactor;
                }
                else
                {
                    localPos = new Vector3(colOffset * 0.25f, 0.45f, rowOffset * 0.25f);
                    minerScale = 0.30f * scaleFactor;
                }

                Miner seated = Miner.CreateSeatedMiner(
                    body,
                    localPos,
                    minerScale,
                    m_Cargo.CargoColor,
                    m_MinerPrefab,
                    m_RunSpeed
                );

                if (seated != null)
                {
                    m_SeatedMiners.Add(seated);
                }
            }
        }

        /// <summary>
        /// Vagon raya girdiğinde oturan madencileri sırayla dışarı zıplatır.
        /// Tüm madenciler inince <paramref name="onComplete"/> tetiklenir.
        /// </summary>
        public void StartJumpingOutSequence(
            float colorThreshold,
            float spawnDelay = 0.45f,
            float runSpeed = 0.85f,
            System.Action onComplete = null)
        {
            m_ColorThreshold = colorThreshold;
            m_SpawnDelay = spawnDelay;
            m_RunSpeed = runSpeed;

            if (m_Cargo == null)
            {
                m_Cargo = GetComponent<TruckCargo>();
            }

            if (m_SpawnCoroutine != null)
            {
                StopCoroutine(m_SpawnCoroutine);
            }
            m_SpawnCoroutine = StartCoroutine(JumpOutSequenceRoutine(onComplete));
        }

        private IEnumerator JumpOutSequenceRoutine(System.Action onComplete)
        {
            if (m_Cargo != null && !m_Cargo.IsFull && RemainingMinersToDeploy > 0)
            {
                Transform body = GetWagonBody(transform);
                TryGetLocalBounds(body, out Bounds bounds);
                float extraMinerScale = (bounds.size.x > 0.001f) ? (bounds.size.y * 0.42f * m_ScaleFactor) : (0.35f * m_ScaleFactor);

                float waitTimeWithoutSpawn = 0f;
                const float maxWaitTimeForNewCubes = 15.0f;

                while (RemainingMinersToDeploy > 0 && m_Cargo != null && !m_Cargo.IsFull)
                {
                    // 1. Dışarı atlayabilecek açık/erişilebilir uygun küp var mı?
                    if (Miner.HasAccessibleMatchingCube(m_Cargo.CargoColor, m_ColorThreshold))
                    {
                        waitTimeWithoutSpawn = 0f;

                        Miner miner = null;
                        bool fromSeatedList = false;

                        // m_SeatedMiners içinden vagonda oturan ilk geçerli madenciyi al
                        while (m_SeatedMiners.Count > 0)
                        {
                            Miner candidate = m_SeatedMiners[0];
                            m_SeatedMiners.RemoveAt(0);

                            if (candidate != null && candidate.gameObject != null && candidate.CurrentState == Miner.State.SeatedInWagon)
                            {
                                miner = candidate;
                                fromSeatedList = true;
                                break;
                            }
                        }

                        // Oturan görsel madenci kalmadıysa ek madenci oluşturup zıplat
                        if (miner == null)
                        {
                            miner = Miner.CreateSeatedMiner(
                                body,
                                Vector3.up * 0.45f,
                                extraMinerScale,
                                m_Cargo.CargoColor,
                                m_MinerPrefab,
                                m_RunSpeed
                            );
                        }

                        if (miner != null)
                        {
                            bool jumped = miner.JumpOutFromWagon(m_Cargo.CargoColor, m_ColorThreshold, m_RunSpeed);
                            if (jumped)
                            {
                                m_DeployedMinersCount++;
                                yield return new WaitForSeconds(m_SpawnDelay);
                                continue;
                            }
                            else
                            {
                                if (fromSeatedList)
                                {
                                    m_SeatedMiners.Insert(0, miner);
                                }
                                else
                                {
                                    miner.Release();
                                }
                            }
                        }
                    }

                    // 2. Şu an dışarıda açık küp yok, ancak panoda bu renkten kırılmamış iç küpler var mı
                    // VE sahada küplere doğru koşan madenciler var mı?
                    if (Miner.HasMatchingUnpoppedCube(m_Cargo.CargoColor, m_ColorThreshold) &&
                        Miner.HasActiveMinersTargetingColor(m_Cargo.CargoColor, m_ColorThreshold))
                    {
                        waitTimeWithoutSpawn += 0.2f;
                        if (waitTimeWithoutSpawn < maxWaitTimeForNewCubes)
                        {
                            // Koşan madencilerin küpü vurup yeni iç küpleri açığa çıkarmasını bekle
                            yield return new WaitForSeconds(0.2f);
                            continue;
                        }
                    }

                    // Seviyede bu renkten hiç blok kalmadı veya bekleyecek madenci yok
                    break;
                }
            }

            // İndirme tamamlandıktan sonra kasada kalan görsel oturucu varsa temizle (vagon temiz gitsin)
            ClearSeatedMiners();

            m_SpawnCoroutine = null;
            onComplete?.Invoke();
        }

        private static Transform GetWagonBody(Transform root)
        {
            if (root == null) return null;
            Transform body = root.Find("MineCart_Body");
            if (body == null) body = root.Find("Truck_Cargo");
            return body != null ? body : root;
        }

        private static bool TryGetLocalBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            if (root == null) return false;

            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>();
            if (filters == null || filters.Length == 0) return false;

            bool found = false;
            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;

                Matrix4x4 toRoot = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Bounds local = mesh.bounds;

                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3(
                        (i & 1) == 0 ? local.min.x : local.max.x,
                        (i & 2) == 0 ? local.min.y : local.max.y,
                        (i & 4) == 0 ? local.min.z : local.max.z);

                    Vector3 point = toRoot.MultiplyPoint3x4(corner);

                    if (!found)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        found = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }

            return found;
        }

        private void ClearSeatedMiners()
        {
            for (int i = 0; i < m_SeatedMiners.Count; i++)
            {
                if (m_SeatedMiners[i] != null && m_SeatedMiners[i].CurrentState == Miner.State.SeatedInWagon)
                {
                    m_SeatedMiners[i].Release();
                }
            }
            m_SeatedMiners.Clear();
        }

        private void OnDisable()
        {
            if (m_SpawnCoroutine != null)
            {
                StopCoroutine(m_SpawnCoroutine);
                m_SpawnCoroutine = null;
            }
            ClearSeatedMiners();
        }

        private void OnDestroy()
        {
            if (m_SpawnCoroutine != null)
            {
                StopCoroutine(m_SpawnCoroutine);
                m_SpawnCoroutine = null;
            }
            ClearSeatedMiners();
        }
    }
}
