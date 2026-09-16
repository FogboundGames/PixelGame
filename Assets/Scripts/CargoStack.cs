using System.Collections.Generic;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Vagonun kasasında biriken kırık taş parçalarını gösterir.
    ///
    /// Parçalar ızgaraya dizilmez: kasa içinde rastgele yerlere, rastgele açılarla,
    /// dolulukla birlikte yükselen bir yığın halinde düşerler. Böylece kasa
    /// "düzgün paketlenmiş" değil, moloz dolmuş gibi görünür.
    ///
    /// Yığın fizik kullanmaz; yükseklik doluluk oranından gelir. Onlarca parça için
    /// fizik hem pahalı hem de vagon hareket ederken güvenilmez olurdu.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Cargo Stack")]
    public class CargoStack : MonoBehaviour
    {
        [Header("📦 Kasa")]
        [Tooltip("Parçaların dolacağı kasa nesnesi. Boş bırakılırsa " +
                 "MineCart_Body / Truck_Cargo isimli çocuk aranır.")]
        [SerializeField] private Transform m_Body;

        [Tooltip("Kasa kenarlarından bırakılan pay (0.1 = %10). " +
                 "Parçaların duvarlara gömülmesini engeller.")]
        [Range(0f, 0.4f)]
        [SerializeField] private float m_Padding = 0.16f;

        [Tooltip("Yığının kasa yüksekliğinin ne kadarına çıkacağı. " +
                 "1'in altında kalması kasanın taşmamasını sağlar.")]
        [Range(0.2f, 1.2f)]
        [SerializeField] private float m_FillHeight = 0.75f;

        [Header("🪨 Parçalar")]
        [Tooltip("Yığının ne kadar dağınık duracağı. Yüksek değer parçaları " +
                 "birbirinin üstüne daha düzensiz yığar.")]
        [Range(0f, 1f)]
        [SerializeField] private float m_Scatter = 0.6f;

        [Tooltip("Parçaların kasayı ne kadar dolduracağı. Yükseltmek parçaları " +
                 "büyütür; düşürmek daha çok ama küçük parça demektir.")]
        [Range(0.2f, 1.5f)]
        [SerializeField] private float m_PackDensity = 0.85f;

        [Tooltip("Yerleşirken yukarıdan düşme süresi")]
        [Min(0f)]
        [SerializeField] private float m_DropDuration = 0.14f;

        private readonly List<Transform> m_Pieces = new List<Transform>();

        private Material m_Material;
        private Bounds m_Inner;
        private int m_TotalPieces = 1;
        private float m_BasePieceSize;
        private bool m_Ready;

        /// <summary>Kasada görünen parça sayısı.</summary>
        public int VisibleCount => m_Pieces.Count;

        /// <summary>
        /// Bir parçanın kasa uzayındaki temel kenar uzunluğu.
        /// Uçan parçalar da bu boyutta gelir ki varışta boyut atlaması olmasın.
        /// </summary>
        public float BasePieceSize => m_BasePieceSize;

        /// <summary>Bir parçanın dünya uzayındaki temel kenar uzunluğu.</summary>
        public float BasePieceWorldSize
        {
            get
            {
                Transform body = BodyTransform();
                float scale = body != null ? body.lossyScale.x : 1f;
                return m_BasePieceSize * scale;
            }
        }

        /// <summary>
        /// Yığını hazırlar. <paramref name="totalPieces"/> bu kasaya toplam kaç parça
        /// düşeceğidir; parça boyutu buna göre hesaplanır ki kasa tam dolsun ama taşmasın.
        /// </summary>
        public void Setup(int totalPieces, Color color)
        {
            m_TotalPieces = Mathf.Max(1, totalPieces);

            Clear();
            Measure();
            EnsureMaterial(color);
        }

        /// <summary>Kasadaki tüm parçaları kaldırır.</summary>
        public void Clear()
        {
            foreach (Transform piece in m_Pieces)
            {
                if (piece == null) continue;

                if (Application.isPlaying) Destroy(piece.gameObject);
                else DestroyImmediate(piece.gameObject);
            }

            m_Pieces.Clear();
        }

        /// <summary>
        /// Kasaya bir parça ekler. <paramref name="sizeFactor"/> parçanın temel boyuta
        /// göre büyüklüğüdür (1 = ortalama), böylece yığın tek tip görünmez.
        /// <paramref name="shardMesh"/> verilirse gerçek kırık taş mesh'i kullanılır.
        /// </summary>
        public void AddPiece(float sizeFactor, Mesh shardMesh = null)
        {
            if (!m_Ready) Measure();
            if (!m_Ready) return;

            int index = m_Pieces.Count;

            GameObject piece;
            if (shardMesh != null)
            {
                piece = new GameObject($"CargoPiece_{index + 1}");
                MeshFilter mf = piece.AddComponent<MeshFilter>();
                mf.sharedMesh = shardMesh;
                MeshRenderer mr = piece.AddComponent<MeshRenderer>();
                if (m_Material != null)
                {
                    int subMeshCount = shardMesh.subMeshCount;
                    if (subMeshCount > 1)
                    {
                        Material[] mats = new Material[subMeshCount];
                        for (int m = 0; m < subMeshCount; m++) mats[m] = m_Material;
                        mr.sharedMaterials = mats;
                    }
                    else
                    {
                        mr.sharedMaterial = m_Material;
                    }
                }
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            else
            {
                piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = $"CargoPiece_{index + 1}";

                // Yığındaki parçalar tıklamayı yutmasın; oyuncu tabloya basıyor
                Collider collider = piece.GetComponent<Collider>();
                if (collider != null) Destroy(collider);

                MeshRenderer renderer = piece.GetComponent<MeshRenderer>();
                if (renderer != null && m_Material != null)
                {
                    renderer.sharedMaterial = m_Material;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }

            Transform t = piece.transform;
            t.SetParent(BodyTransform(), false);

            // Her parça biraz farklı boyut ve açıda: taş kırığı gibi görünsün
            float size = m_BasePieceSize * Mathf.Max(0.1f, sizeFactor);
            t.localScale = new Vector3(
                size * Random.Range(0.85f, 1.15f),
                size * Random.Range(0.85f, 1.15f),
                size * Random.Range(0.85f, 1.15f));

            t.localRotation = Random.rotation;

            Vector3 target = GetPilePosition(index, size);
            t.localPosition = target;

            m_Pieces.Add(t);

            if (m_DropDuration > 0f && Application.isPlaying)
            {
                StartCoroutine(DropRoutine(t, target));
            }
        }

        /// <summary>
        /// Parçanın yığındaki yeri: yatayda rastgele, dikeyde doluluk oranıyla yükselir.
        /// Böylece parçalar birbirinin üstüne düzensizce biner.
        /// </summary>
        private Vector3 GetPilePosition(int index, float size)
        {
            float progress = m_TotalPieces > 1 ? (float)index / (m_TotalPieces - 1) : 0f;

            // Yatay dağılım: kasa içinde kalacak şekilde rastgele
            float halfX = Mathf.Max(0f, m_Inner.extents.x - size * 0.5f);
            float halfZ = Mathf.Max(0f, m_Inner.extents.z - size * 0.5f);

            float x = Random.Range(-halfX, halfX) * m_Scatter + m_Inner.center.x;
            float z = Random.Range(-halfZ, halfZ) * m_Scatter + m_Inner.center.z;

            // Yığın tabandan başlar, dolulukla yükselir; üstüne küçük bir dağınıklık
            float floor = m_Inner.min.y + size * 0.5f;
            float rise = m_Inner.size.y * m_FillHeight * progress;
            float jitter = size * Random.Range(-0.25f, 0.35f) * m_Scatter;

            return new Vector3(x, floor + rise + jitter, z);
        }

        private System.Collections.IEnumerator DropRoutine(Transform piece, Vector3 target)
        {
            Vector3 start = target + Vector3.up * m_Inner.size.y * 0.6f;
            float elapsed = 0f;

            while (elapsed < m_DropDuration && piece != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / m_DropDuration);

                // Hızlanarak düşsün
                piece.localPosition = Vector3.Lerp(start, target, t * t);
                yield return null;
            }

            if (piece != null) piece.localPosition = target;
        }

        /// <summary>
        /// Kasanın iç hacmini ölçer ve toplam parça sayısına göre parça boyutunu belirler.
        /// </summary>
        private void Measure()
        {
            m_Ready = false;

            Transform body = BodyTransform();
            if (body == null) return;

            if (!TryGetLocalBounds(body, out Bounds bounds)) return;

            Vector3 size = bounds.size;
            if (size.x <= 0.0001f || size.z <= 0.0001f) return;

            // Kenar payı bırakılmış iç hacim
            Vector3 inner = new Vector3(
                size.x * (1f - m_Padding * 2f),
                size.y * (1f - m_Padding),
                size.z * (1f - m_Padding * 2f));

            m_Inner = new Bounds(
                new Vector3(bounds.center.x, bounds.min.y + inner.y * 0.5f, bounds.center.z),
                inner);

            // Parça boyutu: dolacak hacmi parça sayısına bölüp küp kökünü al.
            // Böylece kapasite ne olursa olsun kasa dolar ama taşmaz.
            float volume = inner.x * inner.z * (inner.y * m_FillHeight);
            float perPiece = volume / m_TotalPieces;

            m_BasePieceSize = Mathf.Pow(Mathf.Max(perPiece, 1e-9f), 1f / 3f) * m_PackDensity;

            // Parça kasadan geniş olmasın
            float maxSize = Mathf.Min(inner.x, inner.z) * 0.5f;
            m_BasePieceSize = Mathf.Min(m_BasePieceSize, maxSize);

            m_Ready = m_BasePieceSize > 0f;
        }

        private Transform BodyTransform()
        {
            if (m_Body != null) return m_Body;

            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name.StartsWith("MineCart_Body") || child.name.StartsWith("Truck_Cargo"))
                {
                    m_Body = child;
                    return m_Body;
                }
            }

            m_Body = transform;
            return m_Body;
        }

        private void EnsureMaterial(Color color)
        {
            if (m_Material == null)
            {
                m_Material = CartoonShader.CreateMaterial(color, "CargoPiece_Mat");
                return;
            }

            CartoonShader.ApplyColor(m_Material, color);
        }

        private void OnDestroy()
        {
            if (m_Material != null) Destroy(m_Material);
        }

        /// <summary>Verilen nesnenin kendi uzayındaki sınırlarını mesh'lerden hesaplar.</summary>
        private static bool TryGetLocalBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool found = false;

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
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

                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }

            return found;
        }
    }
}
