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

        // Bekleyen halat: partner gemi henüz kuyruğa doğmadıysa (sütun sırasında bekliyorsa) halat,
        // partnerin denizden geleceği noktaya (kamera dışı) uzanır; partner doğup süzülürken onu takip eder.
        // Sadece görseldir: gemiler arasında oynanış bağlantısı (SetLinkedPartner) kurmaz.
        private Transform m_PendingAnchor;
        private Vector3 m_PendingLocalPoint;
        private Color m_PendingColor;

        // UI Konteyner Köprüsü (2D Canvas HUD modu için özel 3B silindirik köprü barı)
        private GameObject m_UiBridgeObj;
        private RectTransform m_UiBridgeRt;
        private RectTransform m_UiBridgeBorderTop;
        private RectTransform m_UiBridgeBorderBottom;
        private UnityEngine.UI.Image m_UiBridgeHalfA;
        private UnityEngine.UI.Image m_UiBridgeHalfB;
        private UnityEngine.UI.Image m_UiBridgeShine;
        private UiFilletGraphic m_UiBridgeFillets;

        public ShipController ShipA => m_ShipA;
        public ShipController ShipB => m_ShipB;
        public int LinkId => m_LinkId;
        public bool IsPending => m_ShipB == null && m_PendingAnchor != null;

        public static LinkedShipTether CreatePendingTether(ShipController a, int linkId, Transform anchor, Vector3 anchorLocalPoint, Color partnerColor)
        {
            if (a == null || anchor == null) return null;

            // Sadece oyunda: editörde kurulursa sahneye kaydedilirdi
            if (!Application.isPlaying) return null;

            GameObject tetherObj = new GameObject($"Tether_Pending_{linkId}_{a.name}");
            GameObject container = GameObject.Find("[SHIP_TETHERS]");
            if (container != null) tetherObj.transform.SetParent(container.transform, true);

            LinkedShipTether tether = tetherObj.AddComponent<LinkedShipTether>();
            tether.m_ShipA = a;
            tether.m_ShipB = null;
            tether.m_LinkId = linkId;
            tether.m_PendingAnchor = anchor;
            tether.m_PendingLocalPoint = anchorLocalPoint;
            tether.m_PendingColor = partnerColor;
            tether.EnsureLineRenderer();
            tether.ApplyColorsFromShips();
            tether.UpdateTetherPositions();
            return tether;
        }

        /// <summary>Bekleyen halatta partner doğmuşsa (süzülerek geliyorsa) onu döndürür.</summary>
        private ShipController FindArrivingPartner()
        {
            var ships = ShipController.ActiveShips;
            for (int i = 0; i < ships.Count; i++)
            {
                ShipController s = ships[i];
                if (s != null && s != m_ShipA && s.LinkId == m_LinkId && !s.IsDeparting && s.gameObject.activeInHierarchy)
                    return s;
            }
            return null;
        }

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
            if (m_LineRenderer == null || m_ShipA == null) return;
            ShipController partner = m_ShipB != null ? m_ShipB : (IsPending ? FindArrivingPartner() : null);
            if (partner == null && !IsPending) return;

            Color colA = m_ShipA.ShipColor;
            Color colB = partner != null ? partner.ShipColor : m_PendingColor;

            if (colA.a < 0.1f) colA = m_RopeColor;
            if (colB.a < 0.1f) colB = m_RopeColor;

            m_LastColA = colA;
            m_LastColB = colB;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(colA, 0.0f),
                    new GradientColorKey(colA, 0.499f),
                    new GradientColorKey(colB, 0.501f),
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
            if (IsPending)
            {
                // Gerçek halat kurulduysa (partner bağlandı) ya da gemi ayrıldıysa bekleyen halat biter
                if (m_ShipA == null || !m_ShipA.gameObject.activeInHierarchy || m_ShipA.IsDeparting ||
                    m_ShipA.LinkId != m_LinkId || m_ShipA.LinkedPartner != null)
                {
                    if (Application.isPlaying) Destroy(gameObject);
                    else DestroyImmediate(gameObject);
                    return;
                }
                UpdateTetherPositions();
                return;
            }

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
            if (m_UiBridgeObj != null) m_UiBridgeObj.SetActive(false);
        }

        private void OnDestroy()
        {
            ClearUiFrontShip();
            if (m_UiBridgeObj != null)
            {
                if (Application.isPlaying) Destroy(m_UiBridgeObj);
                else DestroyImmediate(m_UiBridgeObj);
            }
            if (m_ShipA != null && m_ShipA.IsUiContainer) m_ShipA.UpdateLinkedUiVisuals();
            if (m_ShipB != null && m_ShipB.IsUiContainer) m_ShipB.UpdateLinkedUiVisuals();

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
            if (m_LineRenderer == null || m_ShipA == null) return;

            ShipController partner = m_ShipB != null ? m_ShipB : (IsPending ? FindArrivingPartner() : null);
            if (partner == null && !IsPending) return;

            Color partnerColor = partner != null ? partner.ShipColor : m_PendingColor;
            if (m_ShipA.ShipColor != m_LastColA || partnerColor != m_LastColB)
            {
                ApplyColorsFromShips();
            }

            if (m_ShipA.IsUiContainer)
            {
                UpdateUiContainerTether(partner);
                return;
            }

            // Halat, gemilerin sallanan gövdesine (VisualRoot: su sallanması, kaldırma, dalma) bağlanır;
            // kök objeye bağlıyken gemiler sallanırken halat sabit kalıyordu.
            Transform hullA = m_ShipA.VisualRoot != null ? m_ShipA.VisualRoot : m_ShipA.transform;
            Transform hullB = partner != null ? (partner.VisualRoot != null ? partner.VisualRoot : partner.transform) : null;

            // Gemilerin tam orta yükseklik ve merkez noktaları (yerel Z=0 geminin tam boy ortası, Y=0.95f bel ortasıdır)
            Vector3 centerA = hullA.TransformPoint(new Vector3(0f, 0.95f, 0f));
            Vector3 centerB = hullB != null
                ? hullB.TransformPoint(new Vector3(0f, 0.95f, 0f))
                : m_PendingAnchor.TransformPoint(m_PendingLocalPoint);

            Vector3 worldDelta = centerB - centerA;
            float worldDist = worldDelta.magnitude;
            if (worldDist < 0.001f) return;

            Vector3 worldDir = worldDelta / worldDist;

            // A gemisinin gövde kenarındaki orta bağlantı noktası
            Vector3 localDirA = hullA.InverseTransformDirection(worldDir);
            Vector3 localAttachA = CalculateHullPerimeterPoint(localDirA);
            Vector3 posA = hullA.TransformPoint(localAttachA);

            // B gemisinin gövde kenarındaki orta bağlantı noktası (bekleyen halatta: partnerin geleceği nokta)
            Vector3 posB = centerB;
            if (hullB != null)
            {
                Vector3 localDirB = hullB.InverseTransformDirection(-worldDir);
                Vector3 localAttachB = CalculateHullPerimeterPoint(localDirB);
                posB = hullB.TransformPoint(localAttachB);
            }

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

        private void EnsureUiBridgeBar()
        {
            if (m_UiBridgeObj != null) return;

            Transform uiParent = null;
            var rtA = (RectTransform)m_ShipA.transform;
            Canvas canvas = rtA.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                Transform uiRoot = canvas.transform.Find("[Containers_UI_Root]");
                if (uiRoot != null)
                {
                    Transform tethersRoot = uiRoot.Find("[UI_Tethers_Root]");
                    if (tethersRoot == null)
                    {
                        GameObject trObj = new GameObject("[UI_Tethers_Root]", typeof(RectTransform));
                        trObj.transform.SetParent(uiRoot, false);
                        RectTransform trRt = trObj.GetComponent<RectTransform>();
                        trRt.anchorMin = Vector2.zero;
                        trRt.anchorMax = Vector2.one;
                        trRt.offsetMin = Vector2.zero;
                        trRt.offsetMax = Vector2.zero;
                        trObj.transform.SetSiblingIndex(uiRoot.childCount - 1);
                        tethersRoot = trObj.transform;
                    }
                    uiParent = tethersRoot;
                }
                else
                {
                    uiParent = canvas.transform;
                }
            }
            if (uiParent == null) uiParent = rtA.parent;

            m_UiBridgeObj = new GameObject($"[UI_Bridge_Bar_{m_LinkId}_{m_ShipA.name}]", typeof(RectTransform));
            m_UiBridgeObj.transform.SetParent(uiParent, false);
            m_UiBridgeRt = m_UiBridgeObj.GetComponent<RectTransform>();
            m_UiBridgeRt.pivot = new Vector2(0.5f, 0.5f);

            Canvas bridgeCanvas = m_UiBridgeObj.AddComponent<Canvas>();
            bridgeCanvas.overrideSorting = true;
            if (canvas != null)
            {
                bridgeCanvas.renderMode = canvas.renderMode;
                bridgeCanvas.worldCamera = canvas.worldCamera;
                bridgeCanvas.planeDistance = canvas.planeDistance;
                bridgeCanvas.sortingLayerID = canvas.sortingLayerID;
                bridgeCanvas.sortingOrder = canvas.sortingOrder + 5;
                bridgeCanvas.additionalShaderChannels = canvas.additionalShaderChannels;
            }
            else
            {
                bridgeCanvas.sortingOrder = 55;
            }

            // 1. Half_A (Sol yarı: Ship A rengi, sol uç kapsül yuvarlak)
            GameObject objA = new GameObject("Half_A", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            objA.transform.SetParent(m_UiBridgeRt, false);
            RectTransform rtHalfA = objA.GetComponent<RectTransform>();
            rtHalfA.anchorMin = new Vector2(0f, 0f);
            rtHalfA.anchorMax = new Vector2(0.5f, 1f);
            rtHalfA.offsetMin = Vector2.zero;
            rtHalfA.offsetMax = Vector2.zero;
            m_UiBridgeHalfA = objA.GetComponent<UnityEngine.UI.Image>();
#if UNITY_EDITOR
            m_UiBridgeHalfA.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Containers/Bridge_Cap_Left.png");
#endif
            if (m_UiBridgeHalfA.sprite == null) m_UiBridgeHalfA.sprite = Resources.Load<Sprite>("Containers/Bridge_Cap_Left");
            m_UiBridgeHalfA.type = UnityEngine.UI.Image.Type.Sliced;
            m_UiBridgeHalfA.raycastTarget = false;

            // 2. Half_B (Sağ yarı: Ship B rengi, sağ uç kapsül yuvarlak)
            GameObject objB = new GameObject("Half_B", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            objB.transform.SetParent(m_UiBridgeRt, false);
            RectTransform rtHalfB = objB.GetComponent<RectTransform>();
            rtHalfB.anchorMin = new Vector2(0.5f, 0f);
            rtHalfB.anchorMax = new Vector2(1f, 1f);
            rtHalfB.offsetMin = Vector2.zero;
            rtHalfB.offsetMax = Vector2.zero;
            m_UiBridgeHalfB = objB.GetComponent<UnityEngine.UI.Image>();
#if UNITY_EDITOR
            m_UiBridgeHalfB.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Containers/Bridge_Cap_Right.png");
#endif
            if (m_UiBridgeHalfB.sprite == null) m_UiBridgeHalfB.sprite = Resources.Load<Sprite>("Containers/Bridge_Cap_Right");
            m_UiBridgeHalfB.type = UnityEngine.UI.Image.Type.Sliced;
            m_UiBridgeHalfB.raycastTarget = false;

            // 3. Cylinder_Shine (Tüm bar boyunca üstte beyaz parlama, altta gölge 3D silindir efekti)
            GameObject objShine = new GameObject("Cylinder_Shine", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            objShine.transform.SetParent(m_UiBridgeRt, false);
            RectTransform rtShine = objShine.GetComponent<RectTransform>();
            rtShine.anchorMin = Vector2.zero;
            rtShine.anchorMax = Vector2.one;
            rtShine.offsetMin = Vector2.zero;
            rtShine.offsetMax = Vector2.zero;
            m_UiBridgeShine = objShine.GetComponent<UnityEngine.UI.Image>();
#if UNITY_EDITOR
            m_UiBridgeShine.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Containers/Bridge_Cylinder_Shine.png");
#endif
            if (m_UiBridgeShine.sprite == null) m_UiBridgeShine.sprite = Resources.Load<Sprite>("Containers/Bridge_Cylinder_Shine");
            m_UiBridgeShine.type = UnityEngine.UI.Image.Type.Sliced;
            m_UiBridgeShine.color = Color.white;
            m_UiBridgeShine.raycastTarget = false;

            // 4. Beyaz kenarlıklar: köprü boyunca üst ve alt beyaz çizgi. Konteyner konturlarıyla aynı arka katmanda
            // (kök Canvas'tan bir sıra geride) çizilir; konteyner gövdeleri üstlerini örttüğü için çizgiler tam
            // konteynerin kenarında biter ve halatın konteynere girdiği yerde kontur kalmaz.
            GameObject bordersObj = new GameObject("Borders", typeof(RectTransform));
            bordersObj.transform.SetParent(m_UiBridgeRt, false);
            RectTransform bordersRt = bordersObj.GetComponent<RectTransform>();
            bordersRt.anchorMin = Vector2.zero;
            bordersRt.anchorMax = Vector2.one;
            bordersRt.offsetMin = Vector2.zero;
            bordersRt.offsetMax = Vector2.zero;
            Canvas bordersCanvas = bordersObj.AddComponent<Canvas>();
            bordersCanvas.overrideSorting = true;
            if (canvas != null)
            {
                Canvas rootCanvas = canvas.rootCanvas;
                bordersCanvas.sortingLayerID = rootCanvas.sortingLayerID;
                bordersCanvas.sortingOrder = rootCanvas.sortingOrder - 1;
            }

            GameObject borderTopObj = new GameObject("Border_Top", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            borderTopObj.transform.SetParent(bordersRt, false);
            m_UiBridgeBorderTop = borderTopObj.GetComponent<RectTransform>();
            m_UiBridgeBorderTop.pivot = new Vector2(0.5f, 0.5f);
            var imgTop = borderTopObj.GetComponent<UnityEngine.UI.Image>();
            imgTop.color = Color.white;
            imgTop.raycastTarget = false;

            GameObject borderBottomObj = new GameObject("Border_Bottom", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            borderBottomObj.transform.SetParent(bordersRt, false);
            m_UiBridgeBorderBottom = borderBottomObj.GetComponent<RectTransform>();
            m_UiBridgeBorderBottom.pivot = new Vector2(0.5f, 0.5f);
            var imgBottom = borderBottomObj.GetComponent<UnityEngine.UI.Image>();
            imgBottom.color = Color.white;
            imgBottom.raycastTarget = false;

            // 5. Kenar çizgisinin konteyner konturuna bağlandığı köşeleri yumuşatan yuvarlak dolgular (aynı arka katman)
            GameObject filletsObj = new GameObject("Fillets", typeof(RectTransform), typeof(CanvasRenderer));
            filletsObj.transform.SetParent(bordersRt, false);
            RectTransform filletsRt = filletsObj.GetComponent<RectTransform>();
            filletsRt.anchorMin = Vector2.zero;
            filletsRt.anchorMax = Vector2.one;
            filletsRt.offsetMin = Vector2.zero;
            filletsRt.offsetMax = Vector2.zero;
            m_UiBridgeFillets = filletsObj.AddComponent<UiFilletGraphic>();
            m_UiBridgeFillets.color = Color.white;
            m_UiBridgeFillets.raycastTarget = false;
            // Kontur kalınlığı mertebesinde: köşe sivri kalmaz ama köprünün uçlarında beyaz "boğaz" oluşmaz
            m_UiBridgeFillets.Radius = UiBridgeFilletRadius;
        }

        /// <summary>
        /// UI Konteyner Köprüsü: İki konteynerin kapağındaki soket yuvaları arasında gerilen, tam ortadan
        /// ikiye bölünmüş çift renkli (iki konteynerin kendi renkleri), 3D silindirik ve kenarları beyaz çizgili köprü.
        /// </summary>
        private void UpdateUiContainerTether(ShipController partner)
        {
            if (m_LineRenderer != null && m_LineRenderer.enabled)
            {
                m_LineRenderer.enabled = false;
            }
            if (m_ShadowObj != null && m_ShadowObj.activeSelf)
            {
                m_ShadowObj.SetActive(false);
            }

            EnsureUiBridgeBar();
            if (m_UiBridgeObj == null) return;

            var rtA = (RectTransform)m_ShipA.transform;
            Transform socketTrA = rtA.Find("Socket_Recess");
            Vector3 socketA = socketTrA != null ? socketTrA.position : rtA.TransformPoint(new Vector3(0f, 14f, 0f));
            Vector3 socketB;
            Color colB;

            if (partner != null)
            {
                var rtB = (RectTransform)partner.transform;
                Transform socketTrB = rtB.Find("Socket_Recess");
                socketB = socketTrB != null ? socketTrB.position : rtB.TransformPoint(new Vector3(0f, 14f, 0f));
                colB = partner.ShipColor;
            }
            else if (m_PendingAnchor != null)
            {
                // Partner henüz kuyrukta değil: köprü, partnerin geleceği sütunun en arka spotunun altına uzanır.
                // Hep düz aşağı çizilince başka sütundan gelecek eş yerine alttaki konteynere bağlıymış gibi duruyordu.
                float boxHeight = rtA.rect.height * rtA.lossyScale.y;
                socketB = m_PendingAnchor.position - rtA.up * (boxHeight * 1.2f);
                colB = m_PendingColor;
            }
            else
            {
                m_UiBridgeObj.SetActive(false);
                return;
            }

            if (!m_UiBridgeObj.activeSelf) m_UiBridgeObj.SetActive(true);

            Color colA = m_ShipA.ShipColor;
            if (colA.a < 0.1f) colA = m_RopeColor;
            if (colB.a < 0.1f) colB = m_RopeColor;

            if (m_UiBridgeHalfA != null) m_UiBridgeHalfA.color = colA;
            if (m_UiBridgeHalfB != null) m_UiBridgeHalfB.color = colB;

            // İki konteynerin rozet yazılarını ve soket çukurlarını görsel olarak güncelle
            m_ShipA.UpdateLinkedUiVisuals();
            if (partner != null) partner.UpdateLinkedUiVisuals();

            // Köprünün UI Canvas piksel uzayındaki konumu, mesafesi ve açısı
            Transform parentTr = m_UiBridgeRt.parent;
            Vector2 localA = parentTr != null ? (Vector2)parentTr.InverseTransformPoint(socketA) : (Vector2)socketA;
            Vector2 localB = parentTr != null ? (Vector2)parentTr.InverseTransformPoint(socketB) : (Vector2)socketB;
            Vector2 localDiff = localB - localA;
            float pixelDist = localDiff.magnitude;
            if (pixelDist < 0.001f) return;

            float angle = Mathf.Atan2(localDiff.y, localDiff.x) * Mathf.Rad2Deg;
            Vector2 localCenter = (localA + localB) * 0.5f;

            m_UiBridgeRt.anchoredPosition = localCenter;
            m_UiBridgeRt.localRotation = Quaternion.Euler(0f, 0f, angle);

            const float barHeight = 40f;
            m_UiBridgeRt.sizeDelta = new Vector2(pixelDist, barHeight);

            // Beyaz dış kenarlıklar köprünün tam boyunca: konteynerlerin içinde kalan kısımlar gövdelerin altında
            // kaldığından görünen kısım tam iki konteynerin konturu arasındadır. Kalınlık konteyner konturuyla aynı.
            // Eşi henüz kuyrukta olmayan (bekleyen) köprüde serbest uç yuvarlak kapaktır: çizgiler oraya kadar uzanmaz.
            float borderLength = partner != null ? pixelDist : Mathf.Max(0f, pixelDist - barHeight * 0.5f);
            float borderShift = partner != null ? 0f : -barHeight * 0.25f;
            m_UiBridgeBorderTop.gameObject.SetActive(true);
            m_UiBridgeBorderBottom.gameObject.SetActive(true);
            m_UiBridgeBorderTop.anchoredPosition = new Vector2(borderShift, barHeight * 0.5f + UiOutlineThickness * 0.5f);
            m_UiBridgeBorderTop.sizeDelta = new Vector2(borderLength, UiOutlineThickness);
            m_UiBridgeBorderBottom.anchoredPosition = new Vector2(borderShift, -barHeight * 0.5f - UiOutlineThickness * 0.5f);
            m_UiBridgeBorderBottom.sizeDelta = new Vector2(borderLength, UiOutlineThickness);

            UpdateUiBridgeFillets(rtA, partner != null ? (RectTransform)partner.transform : null, pixelDist, barHeight);
        }

        /// <summary>
        /// Köprünün beyaz kenar çizgilerinin konteyner konturlarına bağlandığı dört köşeye yuvarlak dolgu koyar.
        /// Köprü yerel uzayında A soketi (-L/2, 0), B soketi (+L/2, 0); çizgilerin dış kenarı ±(bar/2 + kontur).
        /// </summary>
        private void UpdateUiBridgeFillets(RectTransform boxA, RectTransform boxB, float length, float barHeight)
        {
            if (m_UiBridgeFillets == null) return;
            var space = (RectTransform)m_UiBridgeFillets.transform;
            float off = barHeight * 0.5f + UiOutlineThickness;
            m_UiBridgeFillets.BeginCorners();
            AddUiBoxFillets(space, boxA, new Vector2(-length * 0.5f, 0f), Vector2.right, off);
            if (boxB != null) AddUiBoxFillets(space, boxB, new Vector2(length * 0.5f, 0f), Vector2.left, off);
            m_UiBridgeFillets.EndCorners();
        }

        private void AddUiBoxFillets(RectTransform space, RectTransform box, Vector2 socket, Vector2 dir, float off)
        {
            if (box == null) return;
            Rect r = box.rect;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 nSide = new Vector2(0f, side);
                // Çizginin dış kenarı soketin hizasında konteynerin içinden başlar; konturdan çıktığı nokta köşedir
                Vector2 oL = box.InverseTransformPoint(space.TransformPoint(socket + nSide * off));
                Vector2 dL = box.InverseTransformVector(space.TransformVector(dir));
                Vector2 nL = box.InverseTransformVector(space.TransformVector(nSide));
                if (!RayExitUiOutline(r, oL, dL, out Vector2 hitL, out Vector2 tanL)) continue;
                if (Vector2.Dot(tanL, nL) < 0f) tanL = -tanL;   // kontur kenarı köprüden uzaklaşan yöne
                Vector2 p = space.InverseTransformPoint(box.TransformPoint(hitL));
                Vector2 e = ((Vector2)space.InverseTransformVector(box.TransformVector(tanL))).normalized;
                m_UiBridgeFillets.AddCorner(p, dir, e);
            }
        }

        /// <summary>Konteyner konturunun dış sınırından (s_UiOutlinePoly) içeriden çıkan ışının çıkış noktası ve oradaki kenar yönü.</summary>
        private static bool RayExitUiOutline(Rect r, Vector2 o, Vector2 d, out Vector2 hit, out Vector2 tangent)
        {
            hit = default;
            tangent = default;
            float best = float.MaxValue;
            int n = s_UiOutlinePoly.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = r.center + Vector2.Scale(s_UiOutlinePoly[i], r.size);
                Vector2 b = r.center + Vector2.Scale(s_UiOutlinePoly[(i + 1) % n], r.size);
                Vector2 sgm = b - a;
                float den = d.x * sgm.y - d.y * sgm.x;
                if (Mathf.Abs(den) < 1e-6f) continue;
                Vector2 ao = a - o;
                float t = (ao.x * sgm.y - ao.y * sgm.x) / den;
                float u = (ao.x * d.y - ao.y * d.x) / den;
                if (t > 0f && u >= 0f && u <= 1f && t < best)
                {
                    best = t;
                    hit = o + d * t;
                    tangent = sgm.normalized;
                }
            }
            return best < float.MaxValue;
        }

        // Container_Outline beyaz konturunun dış sınırı: konteyner rect'ine oranla (merkez 0, ±0.5), saat yönünün tersine.
        // Sprite'tan ölçüldü; konteyner görselleri değişirse yeniden üretilmeli.
        private static readonly Vector2[] s_UiOutlinePoly =
        {
            new Vector2(0.4619f, 0.0186f), new Vector2(0.4619f, 0.0488f), new Vector2(0.4618f, 0.0794f), new Vector2(0.4617f, 0.1104f), new Vector2(0.4617f, 0.1423f), new Vector2(0.4607f, 0.1749f), new Vector2(0.4583f, 0.2084f), new Vector2(0.4565f, 0.2437f), new Vector2(0.4554f, 0.2816f), new Vector2(0.4514f, 0.3201f), new Vector2(0.4397f, 0.3555f), new Vector2(0.4197f, 0.3858f), new Vector2(0.3943f, 0.4118f), new Vector2(0.3651f, 0.4335f), new Vector2(0.3316f, 0.4488f), new Vector2(0.2942f, 0.4562f), new Vector2(0.2553f, 0.4579f), new Vector2(0.2182f, 0.4579f), new Vector2(0.1834f, 0.4579f), new Vector2(0.1504f, 0.4578f), new Vector2(0.1189f, 0.4577f), new Vector2(0.0885f, 0.4577f), new Vector2(0.0589f, 0.4576f), new Vector2(0.0298f, 0.4577f), new Vector2(0.0010f, 0.4578f), new Vector2(-0.0279f, 0.4577f), new Vector2(-0.0570f, 0.4576f), new Vector2(-0.0866f, 0.4577f), new Vector2(-0.1170f, 0.4577f), new Vector2(-0.1485f, 0.4578f), new Vector2(-0.1815f, 0.4579f), new Vector2(-0.2159f, 0.4573f), new Vector2(-0.2518f, 0.4553f), new Vector2(-0.2889f, 0.4514f), new Vector2(-0.3262f, 0.4441f), new Vector2(-0.3606f, 0.4305f), new Vector2(-0.3905f, 0.4099f), new Vector2(-0.4154f, 0.3838f), new Vector2(-0.4344f, 0.3530f), new Vector2(-0.4461f, 0.3178f), new Vector2(-0.4509f, 0.2801f), new Vector2(-0.4519f, 0.2425f), new Vector2(-0.4520f, 0.2066f), new Vector2(-0.4525f, 0.1728f), new Vector2(-0.4539f, 0.1406f), new Vector2(-0.4554f, 0.1095f), new Vector2(-0.4565f, 0.0789f), new Vector2(-0.4575f, 0.0486f), new Vector2(-0.4580f, 0.0186f), new Vector2(-0.4585f, -0.0117f), new Vector2(-0.4594f, -0.0422f), new Vector2(-0.4603f, -0.0734f), new Vector2(-0.4617f, -0.1058f), new Vector2(-0.4632f, -0.1394f), new Vector2(-0.4617f, -0.1734f), new Vector2(-0.4549f, -0.2065f), new Vector2(-0.4432f, -0.2380f), new Vector2(-0.4290f, -0.2690f), new Vector2(-0.4138f, -0.3000f), new Vector2(-0.3958f, -0.3296f), new Vector2(-0.3721f, -0.3544f), new Vector2(-0.3412f, -0.3710f), new Vector2(-0.3047f, -0.3787f), new Vector2(-0.2669f, -0.3812f), new Vector2(-0.2307f, -0.3816f), new Vector2(-0.1973f, -0.3826f), new Vector2(-0.1680f, -0.3895f), new Vector2(-0.1421f, -0.4036f), new Vector2(-0.1154f, -0.4157f), new Vector2(-0.0864f, -0.4201f), new Vector2(-0.0567f, -0.4171f), new Vector2(-0.0274f, -0.4074f), new Vector2(0.0011f, -0.4036f), new Vector2(0.0297f, -0.4123f), new Vector2(0.0588f, -0.4195f), new Vector2(0.0883f, -0.4196f), new Vector2(0.1161f, -0.4119f), new Vector2(0.1415f, -0.3964f), new Vector2(0.1682f, -0.3848f), new Vector2(0.1989f, -0.3817f), new Vector2(0.2327f, -0.3816f), new Vector2(0.2688f, -0.3812f), new Vector2(0.3065f, -0.3786f), new Vector2(0.3423f, -0.3701f), new Vector2(0.3724f, -0.3527f), new Vector2(0.3962f, -0.3281f), new Vector2(0.4154f, -0.2996f), new Vector2(0.4322f, -0.2697f), new Vector2(0.4471f, -0.2391f), new Vector2(0.4584f, -0.2072f), new Vector2(0.4642f, -0.1737f), new Vector2(0.4651f, -0.1394f), new Vector2(0.4637f, -0.1058f), new Vector2(0.4622f, -0.0734f), new Vector2(0.4618f, -0.0422f), new Vector2(0.4619f, -0.0117f)
        };

        /// <summary>Konteynerin görünen gövdesinin, hedef noktaya bakan kenarındaki nokta (dünya uzayında).</summary>
        private static Vector3 UiBoxEdgePoint(RectTransform box, Vector3 towardWorld)
        {
            Rect r = box.rect;
            Vector2 c = r.center;
            Vector2 d = (Vector2)box.InverseTransformPoint(towardWorld) - c;
            // Gövde kenarına tam bağlanınca aradaki dar boşlukta kısa bir leke kalıyordu: uç gövdenin biraz içinde
            float hw = r.width * 0.5f * UiBoxBodyWidthOfRect * UiRopeAnchorDepth;
            float hh = r.height * 0.5f * UiBoxBodyHeightOfRect * UiRopeAnchorDepth;
            float k = Mathf.Max(Mathf.Abs(d.x) / Mathf.Max(1e-4f, hw), Mathf.Abs(d.y) / Mathf.Max(1e-4f, hh));
            return box.TransformPoint(k > 1e-4f ? c + d / k : c);
        }

        // Halatın önünde çizilen (dikey bağlantıda alttaki) konteyner; iç Canvas ile sıralaması öne alınır
        private ShipController m_UiFrontShip;

        /// <summary>
        /// Dikey bağlantıda alttaki konteyner halatın önünde çizilmeli; halat Canvas'ın bir üstünde olduğundan
        /// konteynere kendi sıralamasını veren iç bir Canvas eklenir (tıklama için GraphicRaycaster ile).
        /// ship = null verilirse önceki konteynerin sıralaması geri alınır.
        /// </summary>
        private void SetUiFrontShip(ShipController ship, Canvas root)
        {
            if (m_UiFrontShip == ship)
            {
                if (ship == null) return;
            }
            else
            {
                ClearUiFrontShip();
                m_UiFrontShip = ship;
                if (ship == null) return;
            }

            Canvas c = ship.GetComponent<Canvas>();
            if (c == null)
            {
                c = ship.gameObject.AddComponent<Canvas>();
                if (ship.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                    ship.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            c.overrideSorting = true;
            c.sortingLayerID = root.sortingLayerID;
            c.sortingOrder = root.sortingOrder + 2;
        }

        private void ClearUiFrontShip()
        {
            if (m_UiFrontShip != null)
            {
                Canvas c = m_UiFrontShip.GetComponent<Canvas>();
                if (c != null) c.overrideSorting = false;
            }
            m_UiFrontShip = null;
        }

        // Beyaz kontur kalınlığı (canvas birimi): Container_Outline'daki 14 px / (512 px → 160 birim)
        private const float UiOutlineThickness = 4.4f;
        // Köprü çizgisi ile konteyner konturunun birleştiği köşedeki yuvarlatma yarıçapı (canvas birimi)
        private const float UiBridgeFilletRadius = 3.5f;

        // Dikey bağlantı: düşey fark yatayın bu katından büyükse halat üsttekinden düz iner
        private const float UiVerticalLinkRatio = 1.5f;

        // Konteyner sprite'ında gövde, rect'in bu kadarını kaplar (kalanı şeffaf kenar/gölge)
        private const float UiBoxBodyWidthOfRect = 0.78f;
        private const float UiBoxBodyHeightOfRect = 0.7f;
        // Halat ucu: gövde merkezinden kenarına giden yolun bu oranında (1 = tam kenar)
        private const float UiRopeAnchorDepth = 0.6f;

        // UI konteyner halatı: kalınlık ve sarkma, konteyner yüksekliğine oranla
        private const float UiRopeWidthOfBox = 0.1f;
        private const float UiRopeSagOfBox = 0.14f;
        // Bekleyen halat: şeridin en arka spotunun kaç kutu boyu altına uzanır
        private const float UiPendingDropOfBox = 1.3f;

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
