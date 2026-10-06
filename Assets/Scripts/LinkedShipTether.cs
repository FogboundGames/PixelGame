using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// İki bağlı gemi (Linked Ships) arasında su üzerinde dinamik olarak gerilen,
    /// hafif sarkan ve gemiler hareket ettikçe onları takip eden halat/zincir bileşeni.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(LineRenderer))]
    public class LinkedShipTether : MonoBehaviour
    {
        [Header("🚢 Bağlı Gemiler")]
        [SerializeField] private ShipController m_ShipA;
        [SerializeField] private ShipController m_ShipB;
        [SerializeField] private int m_LinkId = 0;

        [Header("🪢 Halat / Zincir Görsel Ayarları")]
        [SerializeField] private int m_SegmentCount = 16;
        [SerializeField] private float m_SagAmount = 0.09f; // Suya doğru doğal ve estetik sarkma miktarı
        [SerializeField] private float m_LineWidth = 0.26f; // Gemi ölçeğine orantılı (%18 büyütülmüş) estetik halat kalınlığı
        [SerializeField] private Color m_RopeColor = new Color(0.85f, 0.65f, 0.35f, 1f);

        [Header("🌑 Halat Gölgesi")]
        [Tooltip("Gemilerin silüet gölgesiyle aynı ışık yönü, renk ve yumuşak kenarla suya düşen halat gölgesi.")]
        [SerializeField] private bool m_EnableShadow = true;

        private LineRenderer m_LineRenderer;
        private static Material s_TetherMaterial;

        private const string ShadowObjectName = "[Tether_Shadow]";
        private const float ShadowGroundY = -0.015f; // gemi gölgesiyle aynı su seviyesi (gemi yerel uzayı)
        private Vector3[] m_RopePoints;
        private GameObject m_ShadowObj;
        private Mesh m_ShadowMesh;
        private MeshRenderer m_ShadowRenderer;
        private MaterialPropertyBlock m_ShadowProps;
        private Vector3[] m_ShadowVerts;
        private Vector3[] m_ShadowNormals;
        private static readonly int ShadowColorId = Shader.PropertyToID("_Color");
        private static readonly int ShadowDirId = Shader.PropertyToID("_ShadowDir");
        private static readonly int ShadowGroundId = Shader.PropertyToID("_GroundY");
        private static readonly int ShadowSoftnessId = Shader.PropertyToID("_Softness");
        private float m_RattleIntensity = 0f;
        private Color m_LastColA;
        private Color m_LastColB;

        public ShipController ShipA => m_ShipA;
        public ShipController ShipB => m_ShipB;
        public int LinkId => m_LinkId;

        public static LinkedShipTether CreateTether(ShipController a, ShipController b, int linkId)
        {
            if (a == null || b == null) return null;

            GameObject tetherObj = new GameObject($"Tether_Link_{linkId}_{a.name}_{b.name}");
            
            Transform parentTr = null;
            GameObject container = GameObject.Find("[SHIP_TETHERS]");
            if (container == null)
            {
                GameObject models = GameObject.Find("[GAMEPLAY_MODELS]");
                if (models != null)
                {
                    container = new GameObject("[SHIP_TETHERS]");
                    container.transform.SetParent(models.transform, false);
                }
            }
            if (container != null)
            {
                parentTr = container.transform;
            }

            tetherObj.transform.SetParent(parentTr, true);

            LinkedShipTether tether = tetherObj.AddComponent<LinkedShipTether>();
            tether.Setup(a, b, linkId);
            return tether;
        }

        public void Setup(ShipController a, ShipController b, int linkId)
        {
            m_ShipA = a;
            m_ShipB = b;
            m_LinkId = linkId;

            EnsureLineRenderer();
            ApplyColorsFromShips();
            UpdateTetherPositions();
        }

        private void Awake()
        {
            EnsureLineRenderer();
        }

        private void OnEnable()
        {
            EnsureLineRenderer();
            ApplyColorsFromShips();
            UpdateTetherPositions();
        }

        public void EnsureLineRenderer()
        {
            if (m_LineRenderer == null) m_LineRenderer = GetComponent<LineRenderer>();
            if (m_LineRenderer == null) m_LineRenderer = gameObject.AddComponent<LineRenderer>();

            m_LineRenderer.useWorldSpace = true;
            m_LineRenderer.positionCount = m_SegmentCount;
            m_LineRenderer.startWidth = m_LineWidth;
            m_LineRenderer.endWidth = m_LineWidth;
            m_LineRenderer.numCapVertices = 8;
            m_LineRenderer.numCornerVertices = 8;

            if (s_TetherMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                s_TetherMaterial = new Material(shader);
                s_TetherMaterial.name = "Ship_Tether_SharedMat";
                s_TetherMaterial.color = Color.white;
                if (s_TetherMaterial.HasProperty("_BaseColor")) s_TetherMaterial.SetColor("_BaseColor", Color.white);
                if (s_TetherMaterial.HasProperty("_Color")) s_TetherMaterial.SetColor("_Color", Color.white);
            }

            m_LineRenderer.material = s_TetherMaterial;
            m_LineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_LineRenderer.receiveShadows = false;
            m_LineRenderer.sortingOrder = 50;
        }

        /// <summary>
        /// Halatı yarı yarıya bağlı iki geminin renklerine boyar (örn: biri beyaz biri siyahsa yarısı beyaz, yarısı siyah;
        /// ikisi aynı renkse tamamen o renkte). Ortada tatlı ve pürüzsüz bir renk geçişi oluşturur.
        /// </summary>
        public void ApplyColorsFromShips()
        {
            if (m_LineRenderer == null || m_ShipA == null || m_ShipB == null) return;

            Color colA = m_ShipA.ShipColor;
            Color colB = m_ShipB.ShipColor;

            if (colA.a < 0.1f) colA = m_RopeColor;
            if (colB.a < 0.1f) colB = m_RopeColor;

            m_LastColA = colA;
            m_LastColB = colB;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(colA, 0.0f),
                    new GradientColorKey(colA, 0.44f),
                    new GradientColorKey(colB, 0.56f),
                    new GradientColorKey(colB, 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 1.0f)
                }
            );

            m_LineRenderer.colorGradient = gradient;
        }

        private void LateUpdate()
        {
            if (m_ShipA == null || m_ShipB == null)
            {
                if (Application.isPlaying) Destroy(gameObject);
                return;
            }

            if (!m_ShipA.gameObject.activeInHierarchy || !m_ShipB.gameObject.activeInHierarchy)
            {
                if (Application.isPlaying) Destroy(gameObject);
                return;
            }

            // Eğer gemilerden biri veya her ikisi ayrılıyorsa halatı yok et
            if (m_ShipA.IsDeparting || m_ShipB.IsDeparting)
            {
                if (Application.isPlaying) Destroy(gameObject);
                return;
            }

            UpdateTetherPositions();
        }

        private void OnDisable()
        {
            if (m_ShadowObj != null) m_ShadowObj.SetActive(false);
        }

        private void OnDestroy()
        {
            if (m_ShadowMesh != null)
            {
                if (Application.isPlaying) Destroy(m_ShadowMesh);
                else DestroyImmediate(m_ShadowMesh);
            }

            if (m_ShipA != null && m_ShipA.Tether == this)
            {
                m_ShipA.SetTether(null);
            }
            if (m_ShipB != null && m_ShipB.Tether == this)
            {
                m_ShipB.SetTether(null);
            }
        }

        /// <summary>
        /// Gemi gövdesinin dış çevresindeki ideal halat bağlantı noktasını yerel koordinatta hesaplar.
        /// Z=0 noktası geminin tam boy ortası, Y=0.95f tam dikey bel ortasıdır.
        /// Böylece yan yana gemilerde halat gemilerin tam orta yerlerinden bağlanır.
        /// </summary>
        public static Vector3 CalculateHullPerimeterPoint(Vector3 localDir)
        {
            // Kenney boat-house-a gövde boyutları (yerel uzayda X yarıçapı ~0.85f, Z yarıçapı ~1.75f, gövde bel ortası ~0.95f)
            const float Rx = 0.85f;
            const float Rz = 1.75f;
            const float WaistY = 0.95f;

            float dx = localDir.x;
            float dz = localDir.z;
            float lenSq = dx * dx + dz * dz;

            if (lenSq < 0.0001f)
            {
                return new Vector3(0f, WaistY, 0f);
            }

            float invLen = 1f / Mathf.Sqrt(lenSq);
            dx *= invLen;
            dz *= invLen;

            // Elips yüzey kesişim formülü: (dx/Rx)^2 + (dz/Rz)^2 = 1 / factor^2
            float denom = (dx * dx) / (Rx * Rx) + (dz * dz) / (Rz * Rz);
            float factor = Mathf.Sqrt(1f / denom);

            return new Vector3(dx * factor, WaistY, dz * factor);
        }

        public void UpdateTetherPositions()
        {
            if (m_LineRenderer == null || m_ShipA == null || m_ShipB == null) return;

            if (m_ShipA.ShipColor != m_LastColA || m_ShipB.ShipColor != m_LastColB)
            {
                ApplyColorsFromShips();
            }

            // Gemilerin tam orta yükseklik ve merkez noktaları (yerel Z=0 geminin tam boy ortası, Y=0.95f bel ortasıdır)
            Vector3 centerA = m_ShipA.transform.TransformPoint(new Vector3(0f, 0.95f, 0f));
            Vector3 centerB = m_ShipB.transform.TransformPoint(new Vector3(0f, 0.95f, 0f));

            Vector3 worldDelta = centerB - centerA;
            float worldDist = worldDelta.magnitude;
            if (worldDist < 0.001f) return;

            Vector3 worldDir = worldDelta / worldDist;

            // A gemisinin gövde kenarındaki orta bağlantı noktası
            Vector3 localDirA = m_ShipA.transform.InverseTransformDirection(worldDir);
            Vector3 localAttachA = CalculateHullPerimeterPoint(localDirA);
            Vector3 posA = m_ShipA.transform.TransformPoint(localAttachA);

            // B gemisinin gövde kenarındaki orta bağlantı noktası
            Vector3 localDirB = m_ShipB.transform.InverseTransformDirection(-worldDir);
            Vector3 localAttachB = CalculateHullPerimeterPoint(localDirB);
            Vector3 posB = m_ShipB.transform.TransformPoint(localAttachB);

            float attachDist = Vector3.Distance(posA, posB);
            // Doğal sarkma miktarı: iki gemi arasındaki mesafeye orantılı tatlı bir sarkma
            float currentSag = Mathf.Clamp(m_SagAmount * Mathf.Clamp(attachDist / 1.1f, 0.35f, 1.2f), 0.04f, 0.16f);

            if (m_LineRenderer.positionCount != m_SegmentCount)
            {
                m_LineRenderer.positionCount = m_SegmentCount;
            }

            for (int i = 0; i < m_SegmentCount; i++)
            {
                float t = (float)i / (m_SegmentCount - 1);
                Vector3 p = Vector3.Lerp(posA, posB, t);

                // Parabolik sarkma (U eğrisi: t*(1-t)*4 tepe noktası)
                float sagFactor = 4f * t * (1f - t);
                p.y -= currentSag * sagFactor;

                if (m_RattleIntensity > 0.001f)
                {
                    p.x += Mathf.Sin(Time.time * 45f + i * 2f) * m_RattleIntensity * sagFactor;
                    p.z += Mathf.Cos(Time.time * 40f + i * 2f) * m_RattleIntensity * sagFactor;
                }

                m_LineRenderer.SetPosition(i, p);
                if (m_RopePoints == null || m_RopePoints.Length != m_SegmentCount) m_RopePoints = new Vector3[m_SegmentCount];
                m_RopePoints[i] = p;
            }

            UpdateShadow();

            if (m_RattleIntensity > 0f)
            {
                m_RattleIntensity = Mathf.Max(0f, m_RattleIntensity - Time.deltaTime * 3.5f);
            }
        }

        /// <summary>
        /// Halatın suya düşen gölgesi: her halat noktası, gemi silüet gölgesindeki gibi gemi yerel uzayında
        /// yüksekliği kadar ışık yönünde kaydırılıp su seviyesine indirilir (iki geminin çerçevesi t ile
        /// harmanlanır). Şerit, gemi gölgesinin materyalleriyle çizilir: aynı renk, aynı yumuşak kenar ve
        /// stencil sayesinde gemi gölgesiyle çakışan yerde tek kat gölge.
        /// </summary>
        private void UpdateShadow()
        {
            bool want = m_EnableShadow && m_ShipA != null && m_ShipB != null && m_ShipA.SilhouetteShadowEnabled
                        && m_RopePoints != null && m_RopePoints.Length >= 2;
            Material[] mats = want ? m_ShipA.GetSilhouetteShadowMaterials() : null;
            if (!want || mats == null)
            {
                if (m_ShadowObj != null && m_ShadowObj.activeSelf) m_ShadowObj.SetActive(false);
                return;
            }

            EnsureShadowObject();
            if (!m_ShadowObj.activeSelf) m_ShadowObj.SetActive(true);

            // Gölge objesi A gemisinin kök uzayına oturur (gemi gölgesinin nesne uzayıyla aynı)
            Transform a = m_ShipA.transform;
            Transform b = m_ShipB.transform;
            Transform parent = m_ShadowObj.transform.parent;
            m_ShadowObj.transform.SetPositionAndRotation(a.position, a.rotation);
            Vector3 aScale = a.lossyScale;
            Vector3 pScale = parent != null ? parent.lossyScale : Vector3.one;
            m_ShadowObj.transform.localScale = new Vector3(
                aScale.x / Mathf.Max(1e-5f, pScale.x), aScale.y / Mathf.Max(1e-5f, pScale.y), aScale.z / Mathf.Max(1e-5f, pScale.z));

            Vector2 dirA = m_ShipA.SilhouetteShadowDirection;
            Vector2 dirB = m_ShipB.SilhouetteShadowDirection;
            int n = m_RopePoints.Length;
            if (m_ShadowVerts == null || m_ShadowVerts.Length != n * 2)
            {
                m_ShadowVerts = new Vector3[n * 2];
                m_ShadowNormals = new Vector3[n * 2];
                m_ShadowMesh.Clear();
            }

            // Yansıtılmış merkez çizgisi (A yerel uzayında)
            var center = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float t = n > 1 ? i / (float)(n - 1) : 0f;
                Vector3 pa = ProjectToWater(a, m_RopePoints[i], dirA);
                Vector3 pb = ProjectToWater(b, m_RopePoints[i], dirB);
                Vector3 w = Vector3.Lerp(pa, pb, t);
                Vector3 local = a.InverseTransformPoint(w);
                local.y = ShadowGroundY;
                center[i] = local;
            }

            // Halat genişliği gemi yerel birimine çevrilir
            float halfWidth = 0.5f * m_LineWidth / Mathf.Max(1e-5f, aScale.x);
            for (int i = 0; i < n; i++)
            {
                Vector3 prev = center[Mathf.Max(0, i - 1)];
                Vector3 next = center[Mathf.Min(n - 1, i + 1)];
                Vector2 tan = new Vector2(next.x - prev.x, next.z - prev.z);
                if (tan.sqrMagnitude < 1e-10f) tan = Vector2.right;
                tan.Normalize();
                Vector3 side = new Vector3(-tan.y, 0f, tan.x);
                m_ShadowVerts[i * 2] = center[i] + side * halfWidth;
                m_ShadowVerts[i * 2 + 1] = center[i] - side * halfWidth;
                // Yumuşak kenar halkaları köşeleri yatay normal yönünde dışarı iter
                m_ShadowNormals[i * 2] = side;
                m_ShadowNormals[i * 2 + 1] = -side;
            }

            m_ShadowMesh.vertices = m_ShadowVerts;
            m_ShadowMesh.normals = m_ShadowNormals;
            if (m_ShadowMesh.subMeshCount == 0 || m_ShadowMesh.GetIndexCount(0) != (uint)((n - 1) * 6))
            {
                var tris = new int[(n - 1) * 6];
                for (int i = 0; i < n - 1; i++)
                {
                    int v = i * 2;
                    tris[i * 6 + 0] = v; tris[i * 6 + 1] = v + 2; tris[i * 6 + 2] = v + 1;
                    tris[i * 6 + 3] = v + 1; tris[i * 6 + 4] = v + 2; tris[i * 6 + 5] = v + 3;
                }
                m_ShadowMesh.triangles = tris;
            }
            m_ShadowMesh.RecalculateBounds();

            if (m_ShadowRenderer.sharedMaterials.Length != mats.Length || m_ShadowRenderer.sharedMaterial != mats[0])
                m_ShadowRenderer.sharedMaterials = mats;

            // Köşeler zaten yansıtıldı: shader'da ek kaydırma olmasın, sadece su seviyesine otursun
            m_ShadowProps.Clear();
            m_ShadowProps.SetColor(ShadowColorId, m_ShipA.SilhouetteShadowColor);
            m_ShadowProps.SetVector(ShadowDirId, Vector4.zero);
            m_ShadowProps.SetFloat(ShadowGroundId, ShadowGroundY);
            m_ShadowProps.SetFloat(ShadowSoftnessId, m_ShipA.SilhouetteShadowSoftness);
            m_ShadowRenderer.SetPropertyBlock(m_ShadowProps);
        }

        /// <summary>Dünya noktasını gemi yerel uzayında ışık yönünde su seviyesine indirir (gemi silüet gölgesiyle aynı formül).</summary>
        private static Vector3 ProjectToWater(Transform ship, Vector3 world, Vector2 dir)
        {
            Vector3 lp = ship.InverseTransformPoint(world);
            float height = Mathf.Max(0f, lp.y - ShadowGroundY);
            lp.x += dir.x * height;
            lp.z += dir.y * height;
            lp.y = ShadowGroundY;
            return ship.TransformPoint(lp);
        }

        private void EnsureShadowObject()
        {
            if (m_ShadowObj == null)
            {
                Transform found = transform.Find(ShadowObjectName);
                m_ShadowObj = found != null ? found.gameObject : new GameObject(ShadowObjectName);
                m_ShadowObj.hideFlags = HideFlags.DontSave; // her karede yeniden kurulur; sahneye kaydedilmesin
                m_ShadowObj.transform.SetParent(transform, true);
            }
            if (m_ShadowMesh == null)
            {
                m_ShadowMesh = new Mesh { name = "TetherShadowMesh", hideFlags = HideFlags.DontSave };
                m_ShadowMesh.MarkDynamic();
                m_ShadowVerts = null;
            }
            MeshFilter mf = m_ShadowObj.GetComponent<MeshFilter>();
            if (mf == null) mf = m_ShadowObj.AddComponent<MeshFilter>();
            if (mf.sharedMesh != m_ShadowMesh) mf.sharedMesh = m_ShadowMesh;
            if (m_ShadowRenderer == null)
            {
                m_ShadowRenderer = m_ShadowObj.GetComponent<MeshRenderer>();
                if (m_ShadowRenderer == null) m_ShadowRenderer = m_ShadowObj.AddComponent<MeshRenderer>();
                m_ShadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                m_ShadowRenderer.receiveShadows = false;
            }
            if (m_ShadowProps == null) m_ShadowProps = new MaterialPropertyBlock();
        }

        /// <summary>
        /// Kilitli bir gemiye tıklandığında halatın gerilip titremesini sağlar.
        /// </summary>
        public void Rattle()
        {
            m_RattleIntensity = 0.09f;
            transform.DOKill();
            transform.DOShakePosition(0.35f, new Vector3(0.08f, 0f, 0.08f), 15, 90f);
        }
    }
}
