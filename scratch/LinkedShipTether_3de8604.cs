using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// Ä°ki baÄŸlÄ± gemi (Linked Ships) arasÄ±nda su Ã¼zerinde dinamik olarak gerilen,
    /// hafif sarkan ve gemiler hareket ettikÃ§e onlarÄ± takip eden halat/zincir bileÅŸeni.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(LineRenderer))]
    public class LinkedShipTether : MonoBehaviour
    {
        [Header("ğŸš¢ BaÄŸlÄ± Gemiler")]
        [SerializeField] private ShipController m_ShipA;
        [SerializeField] private ShipController m_ShipB;
        [SerializeField] private int m_LinkId = 0;

        [Header("ğŸª¢ Halat / Zincir GÃ¶rsel AyarlarÄ±")]
        [SerializeField] private int m_SegmentCount = 16;
        [SerializeField] private float m_SagAmount = 0.09f; // Suya doÄŸru doÄŸal ve estetik sarkma miktarÄ±
        [SerializeField] private float m_LineWidth = 0.26f; // Gemi Ã¶lÃ§eÄŸine orantÄ±lÄ± (%18 bÃ¼yÃ¼tÃ¼lmÃ¼ÅŸ) estetik halat kalÄ±nlÄ±ÄŸÄ±
        [SerializeField] private Color m_RopeColor = new Color(0.85f, 0.65f, 0.35f, 1f);

        [Header("ğŸŒ‘ Halat GÃ¶lgesi")]
        [Tooltip("Gemilerin silÃ¼et gÃ¶lgesiyle aynÄ± Ä±ÅŸÄ±k yÃ¶nÃ¼, renk ve yumuÅŸak kenarla suya dÃ¼ÅŸen halat gÃ¶lgesi.")]
        [SerializeField] private bool m_EnableShadow = true;

        private LineRenderer m_LineRenderer;
        private static Material s_TetherMaterial;

        private const string ShadowObjectName = "[Tether_Shadow]";
        private const float ShadowGroundY = -0.015f; // gemi gÃ¶lgesiyle aynÄ± su seviyesi (gemi yerel uzayÄ±)
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

        // Bekleyen halat: partner gemi henÃ¼z kuyruÄŸa doÄŸmadÄ±ysa (sÃ¼tun sÄ±rasÄ±nda bekliyorsa) halat,
        // partnerin denizden geleceÄŸi noktaya (kamera dÄ±ÅŸÄ±) uzanÄ±r; partner doÄŸup sÃ¼zÃ¼lÃ¼rken onu takip eder.
        // Sadece gÃ¶rseldir: gemiler arasÄ±nda oynanÄ±ÅŸ baÄŸlantÄ±sÄ± (SetLinkedPartner) kurmaz.
        private Transform m_PendingAnchor;
        private Vector3 m_PendingLocalPoint;
        private Color m_PendingColor;

        public ShipController ShipA => m_ShipA;
        public ShipController ShipB => m_ShipB;
        public int LinkId => m_LinkId;
        public bool IsPending => m_ShipB == null && m_PendingAnchor != null;

        public static LinkedShipTether CreatePendingTether(ShipController a, int linkId, Transform anchor, Vector3 anchorLocalPoint, Color partnerColor)
        {
            if (a == null || anchor == null) return null;

            // Sadece oyunda: editÃ¶rde kurulursa sahneye kaydedilirdi
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

        /// <summary>Bekleyen halatta partner doÄŸmuÅŸsa (sÃ¼zÃ¼lerek geliyorsa) onu dÃ¶ndÃ¼rÃ¼r.</summary>
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
        /// HalatÄ± yarÄ± yarÄ±ya baÄŸlÄ± iki geminin renklerine boyar (Ã¶rn: biri beyaz biri siyahsa yarÄ±sÄ± beyaz, yarÄ±sÄ± siyah;
        /// ikisi aynÄ± renkse tamamen o renkte). Ortada tatlÄ± ve pÃ¼rÃ¼zsÃ¼z bir renk geÃ§iÅŸi oluÅŸturur.
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
            if (IsPending)
            {
                // GerÃ§ek halat kurulduysa (partner baÄŸlandÄ±) ya da gemi ayrÄ±ldÄ±ysa bekleyen halat biter
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

            // EÄŸer gemilerden biri veya her ikisi ayrÄ±lÄ±yorsa halatÄ± yok et
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
            ClearUiFrontShip();
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
        /// Gemi gÃ¶vdesinin dÄ±ÅŸ Ã§evresindeki ideal halat baÄŸlantÄ± noktasÄ±nÄ± yerel koordinatta hesaplar.
        /// Z=0 noktasÄ± geminin tam boy ortasÄ±, Y=0.95f tam dikey bel ortasÄ±dÄ±r.
        /// BÃ¶ylece yan yana gemilerde halat gemilerin tam orta yerlerinden baÄŸlanÄ±r.
        /// </summary>
        public static Vector3 CalculateHullPerimeterPoint(Vector3 localDir)
        {
            // Kenney boat-house-a gÃ¶vde boyutlarÄ± (yerel uzayda X yarÄ±Ã§apÄ± ~0.85f, Z yarÄ±Ã§apÄ± ~1.75f, gÃ¶vde bel ortasÄ± ~0.95f)
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

            // Elips yÃ¼zey kesiÅŸim formÃ¼lÃ¼: (dx/Rx)^2 + (dz/Rz)^2 = 1 / factor^2
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

            // Halat, gemilerin sallanan gÃ¶vdesine (VisualRoot: su sallanmasÄ±, kaldÄ±rma, dalma) baÄŸlanÄ±r;
            // kÃ¶k objeye baÄŸlÄ±yken gemiler sallanÄ±rken halat sabit kalÄ±yordu.
            Transform hullA = m_ShipA.VisualRoot != null ? m_ShipA.VisualRoot : m_ShipA.transform;
            Transform hullB = partner != null ? (partner.VisualRoot != null ? partner.VisualRoot : partner.transform) : null;

            // Gemilerin tam orta yÃ¼kseklik ve merkez noktalarÄ± (yerel Z=0 geminin tam boy ortasÄ±, Y=0.95f bel ortasÄ±dÄ±r)
            Vector3 centerA = hullA.TransformPoint(new Vector3(0f, 0.95f, 0f));
            Vector3 centerB = hullB != null
                ? hullB.TransformPoint(new Vector3(0f, 0.95f, 0f))
                : m_PendingAnchor.TransformPoint(m_PendingLocalPoint);

            Vector3 worldDelta = centerB - centerA;
            float worldDist = worldDelta.magnitude;
            if (worldDist < 0.001f) return;

            Vector3 worldDir = worldDelta / worldDist;

            // A gemisinin gÃ¶vde kenarÄ±ndaki orta baÄŸlantÄ± noktasÄ±
            Vector3 localDirA = hullA.InverseTransformDirection(worldDir);
            Vector3 localAttachA = CalculateHullPerimeterPoint(localDirA);
            Vector3 posA = hullA.TransformPoint(localAttachA);

            // B gemisinin gÃ¶vde kenarÄ±ndaki orta baÄŸlantÄ± noktasÄ± (bekleyen halatta: partnerin geleceÄŸi nokta)
            Vector3 posB = centerB;
            if (hullB != null)
            {
                Vector3 localDirB = hullB.InverseTransformDirection(-worldDir);
                Vector3 localAttachB = CalculateHullPerimeterPoint(localDirB);
                posB = hullB.TransformPoint(localAttachB);
            }

            float attachDist = Vector3.Distance(posA, posB);
            // DoÄŸal sarkma miktarÄ±: iki gemi arasÄ±ndaki mesafeye orantÄ±lÄ± tatlÄ± bir sarkma
            float currentSag = Mathf.Clamp(m_SagAmount * Mathf.Clamp(attachDist / 1.1f, 0.35f, 1.2f), 0.04f, 0.16f);

            if (m_LineRenderer.positionCount != m_SegmentCount)
            {
                m_LineRenderer.positionCount = m_SegmentCount;
            }

            for (int i = 0; i < m_SegmentCount; i++)
            {
                float t = (float)i / (m_SegmentCount - 1);
                Vector3 p = Vector3.Lerp(posA, posB, t);

                // Parabolik sarkma (U eÄŸrisi: t*(1-t)*4 tepe noktasÄ±)
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
        /// UI konteynerler (HUD Canvas): gemi gÃ¶vdesi elipsi burada anlamsÄ±z (Canvas Ã¶lÃ§eÄŸi Ã§ok kÃ¼Ã§Ã¼k, baÄŸlantÄ±
        /// noktalarÄ± konteynerin ortasÄ±na dÃ¼ÅŸÃ¼p kalÄ±n ÅŸerit iki konteynerin Ã¶n yÃ¼zÃ¼nÃ¼ kesiyordu). Halat iki
        /// konteynerin birbirine bakan kenarlarÄ± arasÄ±nda hafif sarkÄ±k Ã§izilir ve Canvas'Ä±n Ã¼stÃ¼nde sÄ±ralanÄ±r.
        /// </summary>
        private void UpdateUiContainerTether(ShipController partner)
        {
            var rtA = (RectTransform)m_ShipA.transform;
            Vector3 posA = rtA.TransformPoint(rtA.rect.center);
            float boxHeight = rtA.rect.height * rtA.lossyScale.y;
            Vector3 posB;
            if (partner != null)
            {
                var rtB = (RectTransform)partner.transform;
                posB = rtB.TransformPoint(rtB.rect.center);
            }
            else
            {
                // Partner henÃ¼z kuyrukta deÄŸil: eski dÃ¼zendeki "denizden geleceÄŸi nokta" (yerel Z -2.6) Canvas'ta
                // ÅŸeridin en arka spotunun tam ortasÄ±na dÃ¼ÅŸÃ¼yor, halat oradaki ilgisiz kutuya baÄŸlÄ± gÃ¶rÃ¼nÃ¼yordu.
                // Halat o spotun altÄ±na, partnerin aÅŸaÄŸÄ±dan geleceÄŸi yÃ¶ne uzanÄ±r.
                posB = m_PendingAnchor.position - rtA.up * (boxHeight * UiPendingDropOfBox);
            }

            // Halat her zaman konteynerlerin (ve iskele kutularÄ±nÄ±n) Ã¼stÃ¼nde Ã§izilir: Canvas'Ä±n altÄ±ndayken iskelede
            // beyaz kutularÄ±n, kuyrukta da aradaki konteynerlerin arkasÄ±nda kayboluyor, hareket anÄ±nda Ã¼ste
            // sÄ±Ã§rÄ±yordu. Konteyner yÃ¼zlerini olabildiÄŸince az kesmek iÃ§in kenardan kenara baÄŸlanÄ±r.
            Vector3 centerA = posA, centerB = posB;
            Vector3 up = rtA.up;
            Vector3 right = rtA.right;
            Vector3 bulge = -up; // yatay halat aÅŸaÄŸÄ± sarkar
            Vector3 delta = centerB - centerA;
            bool vertical = partner != null
                && Mathf.Abs(Vector3.Dot(delta, up)) > Mathf.Abs(Vector3.Dot(delta, right)) * UiVerticalLinkRatio;
            ShipController lowerShip = null;
            if (vertical)
            {
                // Ãœst Ã¼ste baÄŸlÄ± konteynerler: halat Ã¼sttekinin ortasÄ±ndan dÃ¼z iner ve alttakinin arkasÄ±na girer
                // (alttaki konteyner halatÄ±n Ã¼stÃ¼nde Ã§izilir); ortadan giden halat alttakinin sayÄ±sÄ±nÄ± kapatÄ±yordu.
                lowerShip = Vector3.Dot(delta, up) < 0f ? partner : m_ShipA;
                posA = centerA;
                posB = centerB;
                bulge = Vector3.zero;
            }
            else
            {
                posA = UiBoxEdgePoint(rtA, centerB);
                if (partner != null) posB = UiBoxEdgePoint((RectTransform)partner.transform, centerA);
            }

            float width = boxHeight * UiRopeWidthOfBox;
            m_LineRenderer.startWidth = width;
            m_LineRenderer.endWidth = width;
            Canvas canvas = rtA.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                Canvas root = canvas.rootCanvas;
                m_LineRenderer.sortingLayerID = root.sortingLayerID;
                m_LineRenderer.sortingOrder = root.sortingOrder + 1;
                SetUiFrontShip(lowerShip, root);
            }
            if (m_ShadowObj != null && m_ShadowObj.activeSelf) m_ShadowObj.SetActive(false);

            float sag = boxHeight * UiRopeSagOfBox;
            if (m_LineRenderer.positionCount != m_SegmentCount) m_LineRenderer.positionCount = m_SegmentCount;
            for (int i = 0; i < m_SegmentCount; i++)
            {
                float t = (float)i / (m_SegmentCount - 1);
                float sagFactor = 4f * t * (1f - t);
                Vector3 p = Vector3.Lerp(posA, posB, t) + bulge * (sag * sagFactor);
                if (m_RattleIntensity > 0.001f)
                    p += (vertical ? up : right) * (Mathf.Sin(Time.time * 45f + i * 2f) * m_RattleIntensity * boxHeight * sagFactor);
                m_LineRenderer.SetPosition(i, p);
            }

            if (m_RattleIntensity > 0f)
                m_RattleIntensity = Mathf.Max(0f, m_RattleIntensity - Time.deltaTime * 3.5f);
        }

        /// <summary>Konteynerin gÃ¶rÃ¼nen gÃ¶vdesinin, hedef noktaya bakan kenarÄ±ndaki nokta (dÃ¼nya uzayÄ±nda).</summary>
        private static Vector3 UiBoxEdgePoint(RectTransform box, Vector3 towardWorld)
        {
            Rect r = box.rect;
            Vector2 c = r.center;
            Vector2 d = (Vector2)box.InverseTransformPoint(towardWorld) - c;
            // GÃ¶vde kenarÄ±na tam baÄŸlanÄ±nca aradaki dar boÅŸlukta kÄ±sa bir leke kalÄ±yordu: uÃ§ gÃ¶vdenin biraz iÃ§inde
            float hw = r.width * 0.5f * UiBoxBodyWidthOfRect * UiRopeAnchorDepth;
            float hh = r.height * 0.5f * UiBoxBodyHeightOfRect * UiRopeAnchorDepth;
            float k = Mathf.Max(Mathf.Abs(d.x) / Mathf.Max(1e-4f, hw), Mathf.Abs(d.y) / Mathf.Max(1e-4f, hh));
            return box.TransformPoint(k > 1e-4f ? c + d / k : c);
        }

        // HalatÄ±n Ã¶nÃ¼nde Ã§izilen (dikey baÄŸlantÄ±da alttaki) konteyner; iÃ§ Canvas ile sÄ±ralamasÄ± Ã¶ne alÄ±nÄ±r
        private ShipController m_UiFrontShip;

        /// <summary>
        /// Dikey baÄŸlantÄ±da alttaki konteyner halatÄ±n Ã¶nÃ¼nde Ã§izilmeli; halat Canvas'Ä±n bir Ã¼stÃ¼nde olduÄŸundan
        /// konteynere kendi sÄ±ralamasÄ±nÄ± veren iÃ§ bir Canvas eklenir (tÄ±klama iÃ§in GraphicRaycaster ile).
        /// ship = null verilirse Ã¶nceki konteynerin sÄ±ralamasÄ± geri alÄ±nÄ±r.
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

        // Dikey baÄŸlantÄ±: dÃ¼ÅŸey fark yatayÄ±n bu katÄ±ndan bÃ¼yÃ¼kse halat Ã¼sttekinden dÃ¼z iner
        private const float UiVerticalLinkRatio = 1.5f;

        // Konteyner sprite'Ä±nda gÃ¶vde, rect'in bu kadarÄ±nÄ± kaplar (kalanÄ± ÅŸeffaf kenar/gÃ¶lge)
        private const float UiBoxBodyWidthOfRect = 0.78f;
        private const float UiBoxBodyHeightOfRect = 0.7f;
        // Halat ucu: gÃ¶vde merkezinden kenarÄ±na giden yolun bu oranÄ±nda (1 = tam kenar)
        private const float UiRopeAnchorDepth = 0.6f;

        // UI konteyner halatÄ±: kalÄ±nlÄ±k ve sarkma, konteyner yÃ¼ksekliÄŸine oranla
        private const float UiRopeWidthOfBox = 0.1f;
        private const float UiRopeSagOfBox = 0.14f;
        // Bekleyen halat: ÅŸeridin en arka spotunun kaÃ§ kutu boyu altÄ±na uzanÄ±r
        private const float UiPendingDropOfBox = 1.3f;

        /// <summary>
        /// HalatÄ±n suya dÃ¼ÅŸen gÃ¶lgesi: her halat noktasÄ±, gemi silÃ¼et gÃ¶lgesindeki gibi gemi yerel uzayÄ±nda
        /// yÃ¼ksekliÄŸi kadar Ä±ÅŸÄ±k yÃ¶nÃ¼nde kaydÄ±rÄ±lÄ±p su seviyesine indirilir (iki geminin Ã§erÃ§evesi t ile
        /// harmanlanÄ±r). Åerit, gemi gÃ¶lgesinin materyalleriyle Ã§izilir: aynÄ± renk, aynÄ± yumuÅŸak kenar ve
        /// stencil sayesinde gemi gÃ¶lgesiyle Ã§akÄ±ÅŸan yerde tek kat gÃ¶lge.
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

            // GÃ¶lge objesi A gemisinin kÃ¶k uzayÄ±na oturur (gemi gÃ¶lgesinin nesne uzayÄ±yla aynÄ±)
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

            // YansÄ±tÄ±lmÄ±ÅŸ merkez Ã§izgisi (A yerel uzayÄ±nda)
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

            // Halat geniÅŸliÄŸi gemi yerel birimine Ã§evrilir
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
                // YumuÅŸak kenar halkalarÄ± kÃ¶ÅŸeleri yatay normal yÃ¶nÃ¼nde dÄ±ÅŸarÄ± iter
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

            // KÃ¶ÅŸeler zaten yansÄ±tÄ±ldÄ±: shader'da ek kaydÄ±rma olmasÄ±n, sadece su seviyesine otursun
            m_ShadowProps.Clear();
            m_ShadowProps.SetColor(ShadowColorId, m_ShipA.SilhouetteShadowColor);
            m_ShadowProps.SetVector(ShadowDirId, Vector4.zero);
            m_ShadowProps.SetFloat(ShadowGroundId, ShadowGroundY);
            m_ShadowProps.SetFloat(ShadowSoftnessId, m_ShipA.SilhouetteShadowSoftness);
            m_ShadowRenderer.SetPropertyBlock(m_ShadowProps);
        }

        /// <summary>DÃ¼nya noktasÄ±nÄ± gemi yerel uzayÄ±nda Ä±ÅŸÄ±k yÃ¶nÃ¼nde su seviyesine indirir (gemi silÃ¼et gÃ¶lgesiyle aynÄ± formÃ¼l).</summary>
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
        /// Kilitli bir gemiye tÄ±klandÄ±ÄŸÄ±nda halatÄ±n gerilip titremesini saÄŸlar.
        /// </summary>
        public void Rattle()
        {
            m_RattleIntensity = 0.09f;
            transform.DOKill();
            transform.DOShakePosition(0.35f, new Vector3(0.08f, 0f, 0.08f), 15, 90f);
        }
    }
}
