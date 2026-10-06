#pragma warning disable 0414
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PixelGame
{
    /// <summary>
    /// Su üzerindeki tek bir kargo gemisini (ship-cargo-a) yönetir.
    /// Renk, World Space Canvas kapasite rozeti, su salınımı (bobbing),
    /// slota gerçekçi Bezier su rotasıyla yanaşma ve açık denize yelken açma animasyonlarını içerir.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Ship Controller")]
    public class ShipController : MonoBehaviour,
        IPointerClickHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        /// <summary>
        /// A/B test anahtarı: true ise kargo gelince eski DOTween Punch Scale efekti çalışır,
        /// false ise yeni su-salınımı tabanlı "sallanma" efekti. Level Designer'daki butonla
        /// Play Mode'da bile anında değiştirilebilir (tüm gemiler için ortak).
        /// Düz bir statik alan OLARAK TUTULMUYOR: Play Mode'a her girişte Unity script'leri
        /// "domain reload" ile sıfırlar ve düz statikler kod içindeki başlangıç değerine
        /// (false) geri dönerdi — EditorPrefs bu geçişlerden etkilenmeyen kalıcı bir depo.
        /// </summary>
        private const string LegacyCargoPunchPrefKey = "PixelGame_UseLegacyCargoPunch";
        private static bool s_UseLegacyCargoPunch;
        private static bool s_LegacyCargoPunchLoaded;

        public static bool UseLegacyCargoPunch
        {
            get
            {
#if UNITY_EDITOR
                if (!s_LegacyCargoPunchLoaded)
                {
                    s_UseLegacyCargoPunch = EditorPrefs.GetBool(LegacyCargoPunchPrefKey, false);
                    s_LegacyCargoPunchLoaded = true;
                }
#endif
                return s_UseLegacyCargoPunch;
            }
            set
            {
                s_UseLegacyCargoPunch = value;
#if UNITY_EDITOR
                EditorPrefs.SetBool(LegacyCargoPunchPrefKey, value);
                s_LegacyCargoPunchLoaded = true;
#endif
            }
        }

        private static readonly List<ShipController> s_ActiveShips = new List<ShipController>(16);
        public static IReadOnlyList<ShipController> ActiveShips => s_ActiveShips;

        private static Camera s_CachedMainCamera;
        public static Camera MainCamera
        {
            get
            {
                if (s_CachedMainCamera == null)
                {
                    s_CachedMainCamera = Camera.main;
                }
                return s_CachedMainCamera;
            }
        }

        private bool m_BadgeConfigured = false;

        [Header("🎨 Renk & Kimlik")]
        [SerializeField] private Color m_ShipColor = Color.red;
        [SerializeField] private string m_ColorName = "Red";

        [Header("📦 Kargo & Kapasite")]
        [SerializeField] private int m_Capacity = 16;
        [SerializeField] private int m_CurrentCargo = 0;

        [Header("⚓ Durum")]
        [SerializeField] private bool m_IsDocked = false;
        [SerializeField] private bool m_IsDeparting = false;
        [SerializeField] private bool m_IsMoving = false;
        [SerializeField] private ShipSlot m_CurrentSlot;

        [Header("⚓ Slota Yanaşma Ayarları (Dock Alignment)")]
        [Tooltip("Gemi slota oturduğunda slot merkezine göre yerel ileri (Z) ofseti. Geminin iskelenin yuvasına daha hoş, dolgun ve estetik oturmasını sağlar.")]
        [SerializeField] private float m_DockForwardOffset = 0.28f;
        [Tooltip("Gemi slota oturduğunda slot merkezine göre yerel dikey su seviyesi (Y) ofseti.")]
        [SerializeField] private float m_DockHeightOffset = 0.08f;

        public const float DefaultDockForwardOffset = 0.28f;
        public const float DefaultDockHeightOffset = 0.08f;

        public float DockForwardOffset { get => m_DockForwardOffset; set => m_DockForwardOffset = value; }
        public float DockHeightOffset { get => m_DockHeightOffset; set => m_DockHeightOffset = value; }

        [Header("🌊 Su Salınımı (Idle Water Bobbing)")]
        [SerializeField] private bool m_EnableWaterBobbing = true;
        [SerializeField] private float m_BobFrequency = 2.4f;
        [SerializeField] private float m_BobHeight = 0.035f;
        [SerializeField] private float m_RollAngle = 2.0f;
        [SerializeField] private float m_PitchAngle = 1.2f;

        [Header("💦 Suya Batma Animasyonu (Water Dip Impact)")]
        private float m_CurrentDipOffset = 0f;
        private Coroutine m_ShipDipCoroutine;

        [Header("🚢 Görsel Katman (Visual Decoupling)")]
        [Tooltip("Görsel mesh ve güverte modellerini barındıran ayrık child transform. " +
                 "Bobbing, banking roll ve görsel feedback buraya uygulanır; gameplay root transformu ve collider stabil kalır.")]
        [SerializeField] private Transform m_VisualRoot;
        public Transform VisualRoot => m_VisualRoot;

        [Header("🖐️ Gerçek Zamanlı Drag & Smooth Follow (Aşama 3 & 4)")]
        [Tooltip("Drag etkileşimini açar veya kapatır.")]
        [SerializeField] private bool m_EnableDrag = true;
        [Tooltip("Pointer hareketinin drag başlatması için aşması gereken piksel eşiği (2-8 px, varsayılan 4 px).")]
        [SerializeField] private float m_DragThreshold = 4f;
        [Tooltip("VisualRoot'un DragTarget'a yaklaşırken kullandığı yumuşatma süresi (SmoothTime). Düşük değerler daha atik/tepkiseldir (0.04 - 0.08 s).")]
        [SerializeField] private float m_DragSmoothTime = 0.06f;
        [Tooltip("Drag düzlemi yükseklik/derinlik ofseti.")]
        [SerializeField] private float m_DragPlaneHeight = 0f;
        [Tooltip("SmoothDamp için maksimum hareket hızı.")]
        [SerializeField] private float m_DragMaxSpeed = 100f;

        [Header("🚢 Pickup Hissi & Dinamik Rotasyon (Aşama 4)")]
        [Tooltip("Drag başladığında gemi görselinin su yüzeyinden ne kadar yükseleceği (0.10 - 0.20 world unit, varsayılan 0.12).")]
        [SerializeField] private float m_PickupLift = 0.12f;
        [Tooltip("Drag başladığında gemi görselinin ne kadar büyüyeceği (1.03 - 1.06, varsayılan 1.04).")]
        [SerializeField] private float m_PickupScaleMultiplier = 1.04f;
        [Tooltip("Pickup yükselme ve büyüme animasyon süresi (saniye, varsayılan 0.08).")]
        [SerializeField] private float m_PickupDuration = 0.08f;
        [Tooltip("Yana hareket ederken maksimum yatma (banking/roll) açısı (8° - 12°, varsayılan 10°).")]
        [SerializeField] private float m_MaxBankingAngle = 10f;
        [Tooltip("Yanal hızın banking açısına dönüşüm katsayısı.")]
        [SerializeField] private float m_BankingStrength = 2.5f;
        [Tooltip("İleri/geri hareket ederken maksimum pitch açısı (3° - 5°, varsayılan 4°).")]
        [SerializeField] private float m_MaxPitchAngle = 4f;
        [Tooltip("İleri/geri hızın pitch açısına dönüşüm katsayısı.")]
        [SerializeField] private float m_PitchStrength = 1.0f;
        [Tooltip("Hareket yönüne doğru maksimum yaw dönüş açısı (20° - 35°, varsayılan 25°).")]
        [SerializeField] private float m_MaxYawAngle = 25f;
        [Tooltip("Dinamik rotasyonun yumuşatma süresi (saniye, varsayılan 0.08s).")]
        [SerializeField] private float m_RotationSmoothTime = 0.08f;

        [Header("🧲 Manyetik Slot Algılama & Snap (Aşama 5)")]
        [Tooltip("Geminin gameplay pozisyonuna göre bir slotu aday kabul edeceği algılama yarıçapı (0.45 - 0.65, varsayılan 0.55).")]
        [SerializeField] private float m_DetectionRadius = 0.55f;
        [Tooltip("Slot etki alanına girildiğinde uygulanacak manyetik çekim kuvveti (0.20 - 0.60, varsayılan 0.40).")]
        [SerializeField] private float m_MagneticStrength = 0.40f;
        [Tooltip("Slota snap olma Bezier geçiş süresi (saniye, 0.12 - 0.20s, varsayılan 0.16s).")]
        [SerializeField] private float m_SnapDuration = 0.16f;
        [Tooltip("Şu anda algılanan en yakın aday slot (sadece bilgi amaçlı).")]
        [SerializeField] private ShipSlot m_CandidateSlot;

        // Drag & Pickup çalışma zamanı değişkenleri
        private bool m_IsPointerDown = false;
        private bool m_IsDragging = false;
        private bool m_IsPickedUp = false;
        private bool m_DragThresholdPassed = false;
        private bool m_WasDragged = false;
        private Vector2 m_PointerDownScreenPos;
        private Vector3 m_GrabOffset = Vector3.zero;
        private Vector3 m_DragTargetWorldPosition;
        private Vector3 m_SmoothedWorldPosition;
        private Vector3 m_VisualWorldPosition;
        private Vector3 m_DragSmoothVelocity = Vector3.zero;
        private Plane m_DragPlane;
        private Camera m_DragCamera;

        // Velocity & Dynamic Rotation çalışma zamanı
        private Vector3 m_PreviousWorldPosition;
        private Vector3 m_SmoothedVelocity = Vector3.zero;
        private Vector3 m_VelocitySmoothDeriv = Vector3.zero;
        private float m_CurrentPickupLift = 0f;
        private float m_PickupLiftVelocity = 0f;
        private float m_CurrentPickupScale = 1f;
        private float m_PickupScaleVelocity = 0f;
        private float m_CurrentBankingRoll = 0f;
        private float m_RollSmoothVelocity = 0f;
        private float m_CurrentDragPitch = 0f;
        private float m_PitchSmoothVelocity = 0f;
        private float m_CurrentDragYaw = 0f;
        private float m_YawSmoothVelocity = 0f;

        public bool IsDragging => m_IsDragging;
        public bool IsPickedUp => m_IsPickedUp;
        public float DragSmoothTime { get => m_DragSmoothTime; set => m_DragSmoothTime = value; }
        public float DragThreshold { get => m_DragThreshold; set => m_DragThreshold = value; }
        public bool EnableDrag { get => m_EnableDrag; set => m_EnableDrag = value; }
        public float DragPlaneHeight { get => m_DragPlaneHeight; set => m_DragPlaneHeight = value; }
        public float PickupLift { get => m_PickupLift; set => m_PickupLift = value; }
        public float PickupScaleMultiplier { get => m_PickupScaleMultiplier; set => m_PickupScaleMultiplier = value; }
        public float PickupDuration { get => m_PickupDuration; set => m_PickupDuration = value; }
        public float MaxBankingAngle { get => m_MaxBankingAngle; set => m_MaxBankingAngle = value; }
        public float MaxPitchAngle { get => m_MaxPitchAngle; set => m_MaxPitchAngle = value; }
        public float MaxYawAngle { get => m_MaxYawAngle; set => m_MaxYawAngle = value; }
        public float RotationSmoothTime { get => m_RotationSmoothTime; set => m_RotationSmoothTime = value; }
        public float DetectionRadius { get => m_DetectionRadius; set => m_DetectionRadius = value; }
        public float MagneticStrength { get => m_MagneticStrength; set => m_MagneticStrength = value; }
        public float SnapDuration { get => m_SnapDuration; set => m_SnapDuration = value; }
        public ShipSlot CandidateSlot => m_CandidateSlot;
        public Vector3 DragTargetWorldPosition => m_DragTargetWorldPosition;
        public Vector3 SmoothedWorldPosition => m_SmoothedWorldPosition;
        public Vector3 VisualWorldPosition => m_SmoothedWorldPosition;
        public Vector3 SmoothedVelocity => m_SmoothedVelocity;

        [Header("📦 Kargo Alınca Heyecanlı Sallanma (Cargo Wobble)")]
        [Tooltip("Kargo her geldiğinde 1'e sıçrar, sonra zamanla yumuşakça 0'a söner. Var olan su " +
                 "salınımının genliğini geçici olarak büyütür — DOTween tween'i olmadığı için (sadece " +
                 "sabit bir taban değere göre her karede yeniden hesaplanır) asla birikip 'kayma' yapmaz.")]
        [SerializeField] private float m_CargoWobbleDecaySpeed = 0.35f;
        [SerializeField] private float m_CargoWobbleRollMultiplier = 18.0f;
        [SerializeField] private float m_CargoWobblePitchMultiplier = 12.0f;
        private float m_CargoWobbleBoost = 0f;

        [Header("🏷️ Kapasite Rozeti (World Space UI)")]
        [SerializeField] private GameObject m_BadgeCanvasObj;
        [SerializeField] private TextMeshProUGUI m_BadgeText;
        [SerializeField] private Text m_BadgeUIText;
        [SerializeField] private Image m_BadgeImage;

        // Kapasite yazısının punto büyüklüğü (9be8b92 commit'indeki orijinal değer)
        private const float BadgeFontSize = 58f;

        /// <summary>
        /// 9be8b92 commit'indeki orijinal rozet stili: NET BEYAZ, bold, pürüzsüz yuvarlak siyah SDF kontur.
        /// Sonradan eklenen gölge/face dilate/karakter aralığı ayarları önceki denemelerden kalmış olabilir,
        /// o yüzden burada açıkça sıfırlanır.
        /// </summary>
        private static void ApplyBadgeTextStyle(TextMeshProUGUI tmp)
        {
            if (tmp == null) return;

            Color32 outlineCol = new Color32(18, 18, 22, 255);

            tmp.fontSize = BadgeFontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.characterSpacing = 0f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;

            // Prefab kaydedilirken / import worker'da OnValidate çağrıldığında font materyali henüz bağlı
            // olmayabilir; outlineWidth materyal kopyası oluşturmaya çalışıp UnassignedReferenceException atıyordu.
            if (tmp.font == null || tmp.fontSharedMaterial == null) return;

            tmp.outlineWidth = 0.28f;
            tmp.outlineColor = outlineCol;

            Material mat = tmp.fontMaterial;
            if (mat == null) return;

            mat.EnableKeyword("OUTLINE_ON");
            mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.28f);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, outlineCol);
            mat.DisableKeyword(ShaderUtilities.Keyword_Underlay);

            mat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            tmp.UpdateMeshPadding();
        }

        // Sabit temel ölçek (Her zaman uniform 0.307f - %18 büyütülmüş dolgun ve büyük gemiler)
        public const float DefaultShipScale = 0.307f;

        [Header("🌑 Gemi Sahte Gölgesi (Fake Shadow)")]
        [Tooltip("Gemi altına su yüzeyinde yumuşak 2.5D fake shadow ekler.")]
        [SerializeField] private bool m_EnableFakeShadow = true;
        [Tooltip("Gölgenin gemi gövdesine göre konumu (X: sağ/sol, Y: kıç/baş). Işık yönüne göre hafif sağa ve aşağı.")]
        [SerializeField] private Vector2 m_FakeShadowOffset = new Vector2(0.20f, -0.30f);
        [Tooltip("Gölge quad'ının boyutları (Genişlik X, Uzunluk Y).")]
        [SerializeField] private Vector2 m_FakeShadowSize = new Vector2(2.65f, 4.90f);
        [Tooltip("Gölgenin derin deniz rengi ve opaklığı.")]
        [SerializeField] private Color m_FakeShadowColor = new Color(0.012f, 0.045f, 0.16f, 0.60f);

        [SerializeField] private GameObject m_FakeShadowObj;
        private MeshRenderer m_FakeShadowRenderer;

        [Header("🌑 Gemi Silüet Gölgesi")]
        [Tooltip("Geminin gövde şeklini takip eden, sağa düşen net gölge. Açıkken eski yumuşak (quad) gölge ve gövdenin gerçek ışık gölgesi kapanır.")]
        [SerializeField] private bool m_EnableSilhouetteShadow = true;
        [Tooltip("Işık yönü: gövdenin her birim yüksekliği gölgeyi ne kadar kaydırır (X: sağ, Y: aşağı/kıç yönü negatif). Büyüdükçe gölge uzar.")]
        [SerializeField] private Vector2 m_SilhouetteShadowDirection = new Vector2(0.70f, -0.15f);
        [Tooltip("Gölge rengi ve opaklığı (alfa).")]
        [SerializeField] private Color m_SilhouetteShadowColor = new Color(0.02f, 0.08f, 0.22f, 0.40f);
        [Tooltip("Gölge kenarının yumuşaklığı (0 = keskin). Kenar bu genişlikte dışa doğru saydamlaşır.")]
        [Range(0f, 0.8f)]
        [SerializeField] private float m_SilhouetteShadowSoftness = 0.16f;
        [SerializeField] private Material m_SilhouetteShadowMaterial;
        private const string SilhouetteShadowName = "[Ship_SilhouetteShadow]";
        private static Material s_SilhouetteShadowFallbackMaterial;
        private static readonly int SilhouetteColorId = Shader.PropertyToID("_Color");
        private static readonly int SilhouetteDirectionId = Shader.PropertyToID("_ShadowDir");
        private static readonly int SilhouetteSoftnessId = Shader.PropertyToID("_Softness");

        // Yumuşak kenar halkaları: her biri silüeti biraz daha genişletip daha saydam çizer (ilk eleman çekirdek)
        private static readonly float[] SilhouetteRingAlphas = { 1f, 0.70f, 0.45f, 0.25f, 0.10f };
        private static Material s_SilhouetteRingsSource;
        private static Material[] s_SilhouetteRingMaterials;
        private static Material s_ShipFakeShadowMaterial;
        private static Mesh s_QuadMesh;

        // Dahili referanslar
        private MeshRenderer[] m_Renderers;
        private MaterialPropertyBlock m_PropBlock;
        private float m_BobRandomOffset;
        private Vector3 m_BaseLocalPosition;
        private Quaternion m_BaseLocalRotation;
        private Vector3 m_BaseScale = Vector3.one * DefaultShipScale;
        private static Material s_AlwaysOnTopMaterial;

        // Her gemi örneğine özel çalışma zamanı kimliği. Aynı renkteki iki gemi bile farklı ID taşır;
        // küp sahipliği renge değil bu ID'ye göre tutulur. İlk erişimde verilir (Instantiate kopyalamaz).
        private static int s_NextShipRuntimeId = 1;
        private int m_ShipRuntimeId;
        public int ShipRuntimeId
        {
            get
            {
                if (m_ShipRuntimeId == 0) m_ShipRuntimeId = s_NextShipRuntimeId++;
                return m_ShipRuntimeId;
            }
        }

        public Color ShipColor => m_ShipColor;
        public int Capacity => m_Capacity;
        public int CurrentCargo => m_CurrentCargo;
        public int RemainingCapacity => Mathf.Max(0, m_Capacity - m_CurrentCargo);
        public bool IsDocked => m_IsDocked;
        public bool IsFull => m_CurrentCargo >= m_Capacity;
        public bool IsDeparting => m_IsDeparting;

        // ---------------- 🔗 Bağlı Gemi (Linked Ships) ----------------
        private int m_LinkId = 0;
        private ShipController m_LinkedPartner = null;
        private LinkedShipTether m_Tether = null;

        public int LinkId => m_LinkId;
        public ShipController LinkedPartner => m_LinkedPartner;
        public bool IsLinked => m_LinkId > 0 && m_LinkedPartner != null;
        public LinkedShipTether Tether => m_Tether;

        public void SetLinkedPartner(ShipController partner, int linkId, LinkedShipTether tether = null)
        {
            m_LinkedPartner = partner;
            m_LinkId = linkId;
            m_Tether = tether;
        }

        public void SetTether(LinkedShipTether tether)
        {
            m_Tether = tether;
        }

        // ---------------- ❓ Gizli Gemi (Mystery Ship) ----------------
        // Level tool'da "gizli" işaretlenen gemiler kuyrukta en ön sıraya gelene kadar
        // '?' desenli lacivert bir örtüyle tamamen kaplanır: renk ve kapasite yazısı görünmez.
        private bool m_IsMysteryHidden = false;
        // Gemi kuyrukta arka sıralarda görüldü mü? Örtü yalnızca arkadan ön sıraya İLERLEYİNCE kalkar;
        // oyuna zaten ön sırada başlayan gizli gemiler slota yanaşana kadar gizli kalır.
        private bool m_MysterySeenInBackRow = false;
        private static Material s_MysteryCoverMaterial;
        private static ShipQueuePool s_CachedQueuePool;
        private static Texture2D s_MysteryPatternTexture;

        public bool IsMysteryHidden => m_IsMysteryHidden;

        public void SetMysteryHidden(bool hidden)
        {
            if (m_IsMysteryHidden == hidden) return;
            m_IsMysteryHidden = hidden;
            m_MysterySeenInBackRow = false;

            if (hidden)
            {
                ApplyMysteryCover();
                UpdateBadgeText(); // Sayı yerine "?" göster
            }
            else
            {
                // Orijinal takım rengini ve rozeti geri getir
                ApplyColorToShip(m_ShipColor);
            }
        }

        /// <summary>
        /// Gizli gemi en ön sıraya geldiğinde (veya kuyruktan ayrıldığında) örtüyü kaldırır.
        /// </summary>
        private void UpdateMysteryReveal()
        {
            if (!m_IsMysteryHidden || !Application.isPlaying) return;

            // Slota yanaştığında / kalkışta her durumda açılır
            bool reveal = m_IsDocked || m_IsDeparting;
            if (!reveal)
            {
                // Her karede sahne taraması yapmamak için kuyruk referansı önbelleğe alınır
                if (s_CachedQueuePool == null) s_CachedQueuePool = UnityEngine.Object.FindFirstObjectByType<ShipQueuePool>();
                ShipQueuePool pool = s_CachedQueuePool;
                if (pool == null || pool.WaitingShips == null || !pool.WaitingShips.Contains(this)) return;

                // Bağlı gemiler partnerinin hemen arkasından da çıkabildiği için "önü açık" ön sıra sayılır
                bool atFront = IsLinked ? pool.IsShipUnblockedForDispatch(this) : pool.IsFrontRow(this);
                if (!atFront)
                {
                    m_MysterySeenInBackRow = true;
                    return;
                }
                reveal = m_MysterySeenInBackRow;
            }

            if (reveal) RevealMystery();
        }

        public void RevealMystery()
        {
            if (!m_IsMysteryHidden) return;
            SetMysteryHidden(false);

            if (Application.isPlaying)
            {
                SpawnWaterRipple(transform.position + new Vector3(0f, -0.08f, 0f), 0.22f, 0.9f, 0.5f);
                // Örtü kalkarken küçük "pop" tepkisi (kuyruk kayma tween'lerine dokunmadan sadece görsel kök)
                if (m_VisualRoot != null)
                {
                    m_VisualRoot.DOPunchScale(new Vector3(0.14f, 0.14f, 0.14f), 0.35f, 4, 0.5f)
                        .OnComplete(() => { if (m_VisualRoot != null) m_VisualRoot.localScale = Vector3.one; });
                }
            }
        }

        private void ApplyMysteryCover()
        {
            EnsureVisualComponents();
            if (m_Renderers == null || m_Renderers.Length == 0)
            {
                m_Renderers = GetComponentsInChildren<MeshRenderer>(true);
            }

            Material coverMat = GetOrCreateMysteryCoverMaterial();
            if (coverMat == null) return;

            foreach (var mr in m_Renderers)
            {
                if (mr == null) continue;
                if (mr.name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (m_FakeShadowObj != null && (mr.gameObject == m_FakeShadowObj || mr.transform.IsChildOf(m_FakeShadowObj.transform))) continue;
                if (m_CargoDeckRoot != null && mr.transform.IsChildOf(m_CargoDeckRoot)) continue;

                mr.sharedMaterial = coverMat;
            }
        }

        public static Material GetOrCreateMysteryCoverMaterial()
        {
            if (s_MysteryCoverMaterial != null) return s_MysteryCoverMaterial;

            // Shader Resources klasöründe: build'e de dahil olur
            Shader shader = Resources.Load<Shader>("ShipMysteryCover");
            if (shader == null) shader = Shader.Find("PixelGame/ShipMysteryCover");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return null;

            s_MysteryCoverMaterial = new Material(shader) { name = "Ship_MysteryCover_Mat" };
            Texture2D pattern = GetOrCreateMysteryPatternTexture();
            if (s_MysteryCoverMaterial.HasProperty("_PatternTex")) s_MysteryCoverMaterial.SetTexture("_PatternTex", pattern);
            if (s_MysteryCoverMaterial.HasProperty("_BaseMap")) s_MysteryCoverMaterial.SetTexture("_BaseMap", pattern);
            return s_MysteryCoverMaterial;
        }

        /// <summary>
        /// Lacivert zemin üzerine rastgele açılı/boyutlu beyaz '?' işaretleri çizilmiş, kenarları dikişsiz döşenebilir doku.
        /// </summary>
        public static Texture2D GetOrCreateMysteryPatternTexture()
        {
            if (s_MysteryPatternTexture != null) return s_MysteryPatternTexture;

            const int size = 256;
            Color bg = new Color32(32, 30, 78, 255);
            Color fg = Color.white;

            float[] alpha = new float[size * size];
            UnityEngine.Random.State oldState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(7351);

            // Üst üste binmeyen yerleşim için basit jitter'lı grid (3x3 hücre)
            const int cells = 3;
            float cellSize = size / (float)cells;
            for (int cy = 0; cy < cells; cy++)
            {
                for (int cx = 0; cx < cells; cx++)
                {
                    float glyphPx = UnityEngine.Random.Range(50f, 66f);
                    float jitter = (cellSize - glyphPx * 0.6f) * 0.35f;
                    Vector2 center = new Vector2(
                        (cx + 0.5f) * cellSize + UnityEngine.Random.Range(-jitter, jitter) + (cy % 2) * cellSize * 0.5f,
                        (cy + 0.5f) * cellSize + UnityEngine.Random.Range(-jitter, jitter));
                    float angle = UnityEngine.Random.Range(-40f, 40f) * Mathf.Deg2Rad;
                    DrawQuestionMarkGlyph(alpha, size, center, glyphPx, angle);
                }
            }
            UnityEngine.Random.state = oldState;

            Color32[] pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.Lerp(bg, fg, alpha[i]);
            }

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.name = "Ship_MysteryPattern";
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4;
            tex.SetPixels32(pixels);
            tex.Apply(true, false);
            s_MysteryPatternTexture = tex;
            return tex;
        }

        // '?' glifini mesafe alanı (SDF) ile anti-aliased çizer; doku kenarlarında sarmalanır (dikişsiz).
        private static void DrawQuestionMarkGlyph(float[] alpha, int size, Vector2 center, float glyphPx, float angle)
        {
            float cos = Mathf.Cos(-angle);
            float sin = Mathf.Sin(-angle);
            int half = Mathf.CeilToInt(glyphPx * 0.6f);
            float pixelUnit = 1f / glyphPx;

            // Glif geometrisi (yükseklik ~1 birim, merkez orijinde, Y yukarı)
            Vector2 arcCenter = new Vector2(0f, 0.2f);
            const float arcRadius = 0.2f;
            const float stroke = 0.085f;
            const float arcStartDeg = -55f;   // sağ-alt (sapın başladığı yer)
            const float arcSpanDeg = 255f;    // üstten dolaşıp sol tarafa kadar
            float endRad = arcStartDeg * Mathf.Deg2Rad;
            Vector2 arcEnd = arcCenter + new Vector2(Mathf.Cos(endRad), Mathf.Sin(endRad)) * arcRadius;
            float tipRad = (arcStartDeg + arcSpanDeg) * Mathf.Deg2Rad;
            Vector2 arcTip = arcCenter + new Vector2(Mathf.Cos(tipRad), Mathf.Sin(tipRad)) * arcRadius;
            Vector2 stemMid = new Vector2(0f, -0.08f);
            Vector2 stemEnd = new Vector2(0f, -0.15f);
            Vector2 dotCenter = new Vector2(0f, -0.36f);
            const float dotRadius = 0.1f;

            for (int oy = -half; oy <= half; oy++)
            {
                for (int ox = -half; ox <= half; ox++)
                {
                    float px = Mathf.Floor(center.x) + ox + 0.5f - center.x;
                    float py = Mathf.Floor(center.y) + oy + 0.5f - center.y;
                    Vector2 p = new Vector2(px * cos - py * sin, px * sin + py * cos) * pixelUnit;

                    // Kanca yayı
                    Vector2 rel = p - arcCenter;
                    float a = Mathf.Atan2(rel.y, rel.x) * Mathf.Rad2Deg - arcStartDeg;
                    a = Mathf.Repeat(a, 360f);
                    float dArc = a <= arcSpanDeg
                        ? Mathf.Abs(rel.magnitude - arcRadius)
                        : Mathf.Min((p - arcEnd).magnitude, (p - arcTip).magnitude);

                    float dStem = Mathf.Min(DistanceToSegment(p, arcEnd, stemMid), DistanceToSegment(p, stemMid, stemEnd));
                    float d = Mathf.Min(Mathf.Min(dArc, dStem) - stroke, (p - dotCenter).magnitude - dotRadius);

                    float aa = Mathf.Clamp01(0.5f - d / pixelUnit);
                    if (aa <= 0f) continue;

                    int tx = ((int)Mathf.Floor(center.x) + ox) % size; if (tx < 0) tx += size;
                    int ty = ((int)Mathf.Floor(center.y) + oy) % size; if (ty < 0) ty += size;
                    int idx = ty * size + tx;
                    if (aa > alpha[idx]) alpha[idx] = aa;
                }
            }
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>
        /// Bağlı gemilerin ikisinin birden en ön sırada ve serbest olup olmadığını kontrol eder.
        /// </summary>
        public bool CanDispatchLinked()
        {
            if (!IsLinked) return true;
            if (m_LinkedPartner == null) return true;

            if (m_IsDocked || m_IsMoving || m_IsDeparting) return false;
            if (m_LinkedPartner.IsDocked || m_LinkedPartner.IsMoving || m_LinkedPartner.IsDeparting) return false;

            ShipQueuePool pool = UnityEngine.Object.FindFirstObjectByType<ShipQueuePool>();
            if (pool != null)
            {
                // Her iki geminin de çıkış yolu açık olmalı
                // (Alt alta bağlı gemiler birbirini engellemez; yalnızca yabancı gemiler engel sayılır)
                if (!pool.IsShipUnblockedForDispatch(this)) return false;
                if (!pool.IsShipUnblockedForDispatch(m_LinkedPartner)) return false;
            }

            return true;
        }

        // ---------------- Rezerve (yolda olan) kargo ----------------
        // Küp panodan koparıldığı anda gemiye yazılmıyor; uçuş ~1.5 sn sürüyor ve
        // AddCargo ancak varışta çağrılıyor. Bu sayaç olmadan kapasite kontrolü
        // sadece VARMIŞ kargoyu görüyordu: küpler 0.12 sn arayla fırlatıldığı için
        // 10 kapasiteli bir gemi için panodan ~22 küp çıkıyordu.
        private int m_PendingCargo = 0;

        /// <summary>Yola çıkmış ama henüz gemiye varmamış kargo sayısı.</summary>
        public int PendingCargo => m_PendingCargo;
        public bool HasPendingCargo => m_PendingCargo > 0;

        /// <summary>Yoldakiler DAHİL gemi hâlâ kargo alabilir mi?</summary>
        public bool CanAcceptMore => !m_IsDeparting && (m_CurrentCargo + m_PendingCargo) < m_Capacity;

        /// <summary>
        /// Uçuş BAŞLAMADAN önce çağrılır: yer varsa bir kargo yeri ayırıp true döner.
        /// false dönerse küp panodan koparılmamalıdır.
        /// </summary>
        public bool TryReserveCargo()
        {
            if (!CanAcceptMore) return false;
            m_PendingCargo++;
            return true;
        }

        /// <summary>Uçuş yarıda kesilirse ayrılan yeri geri verir.</summary>
        public void ReleaseCargoReservation()
        {
            m_PendingCargo = Mathf.Max(0, m_PendingCargo - 1);
        }
        public bool IsMoving => m_IsMoving;
        public ShipSlot CurrentSlot => m_CurrentSlot;

        public event Action<ShipController> OnCargoFilled;
        public event Action<ShipController> OnDeparted;

        private const string FontPath = "Assets/Fonts/LilitaOne-Regular SDF.asset";
        private const string TTFFontPath = "Assets/Fonts/LilitaOne-Regular.ttf";
        private const string BadgeSpritePath = "Assets/UI/Badge_MiniPill_Tintable.png";

        public static Material GetAlwaysOnTopMaterial()
        {
            if (s_AlwaysOnTopMaterial == null)
            {
                Shader shader = Shader.Find("UI/Default");
                Material baseMat = shader != null ? new Material(shader) : new Material(Canvas.GetDefaultCanvasMaterial());
                baseMat.name = "Ship_UI_AlwaysOnTop_Mat";
                baseMat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
                s_AlwaysOnTopMaterial = baseMat;
            }
            return s_AlwaysOnTopMaterial;
        }

        public static Material GetShipFakeShadowMaterial()
        {
            if (s_ShipFakeShadowMaterial != null) return s_ShipFakeShadowMaterial;
#if UNITY_EDITOR
            s_ShipFakeShadowMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ShipFakeShadow_Mat.mat");
            if (s_ShipFakeShadowMaterial == null)
            {
                s_ShipFakeShadowMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SoftVoxelShadow_Mat.mat");
            }
#endif
            if (s_ShipFakeShadowMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null)
                {
                    s_ShipFakeShadowMaterial = new Material(shader);
                    s_ShipFakeShadowMaterial.name = "ShipFakeShadow_Runtime_Mat";
                    s_ShipFakeShadowMaterial.renderQueue = 3000;
                    s_ShipFakeShadowMaterial.color = new Color(0.012f, 0.045f, 0.16f, 0.60f);
                }
            }
            return s_ShipFakeShadowMaterial;
        }

        private static Mesh GetQuadMesh()
        {
            if (s_QuadMesh == null)
            {
                s_QuadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            }
            return s_QuadMesh;
        }

        /// <summary>
        /// Gemi altına su yüzeyinde yumuşak, kaliteli 2.5D fake shadow ekler.
        /// Küplerin gölgesiyle uyumlu ışık yönünde (hafif sağa ve aşağı) düşer.
        /// </summary>
        public void EnsureFakeShadow()
        {
            if (!m_EnableFakeShadow || m_EnableSilhouetteShadow)
            {
                if (m_FakeShadowObj != null) m_FakeShadowObj.SetActive(false);
                return;
            }

            if (m_FakeShadowObj == null)
            {
                Transform found = transform.Find("[Ship_FakeShadow]");
                if (found == null) found = transform.Find("Ship_FakeShadow");
                if (found != null)
                {
                    m_FakeShadowObj = found.gameObject;
                }
            }

            if (m_FakeShadowObj == null)
            {
                m_FakeShadowObj = new GameObject("[Ship_FakeShadow]");
                m_FakeShadowObj.transform.SetParent(transform, false);
            }

            m_FakeShadowObj.SetActive(true);
            m_FakeShadowObj.transform.localPosition = new Vector3(m_FakeShadowOffset.x, -0.015f, m_FakeShadowOffset.y);
            m_FakeShadowObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            m_FakeShadowObj.transform.localScale = new Vector3(m_FakeShadowSize.x, m_FakeShadowSize.y, 1f);

            MeshFilter mf = m_FakeShadowObj.GetComponent<MeshFilter>();
            if (mf == null) mf = m_FakeShadowObj.AddComponent<MeshFilter>();
            if (mf.sharedMesh == null) mf.sharedMesh = GetQuadMesh();

            m_FakeShadowRenderer = m_FakeShadowObj.GetComponent<MeshRenderer>();
            if (m_FakeShadowRenderer == null) m_FakeShadowRenderer = m_FakeShadowObj.AddComponent<MeshRenderer>();
            m_FakeShadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_FakeShadowRenderer.receiveShadows = false;

            Material mat = GetShipFakeShadowMaterial();
            if (mat != null)
            {
                if (Application.isPlaying) m_FakeShadowRenderer.material = mat;
                else m_FakeShadowRenderer.sharedMaterial = mat;
            }
        }

        [ContextMenu("🌑 Ensure Fake Shadow (Sahte Gölgeyi Güncelle)")]
        public void UpdateFakeShadowManual()
        {
            EnsureFakeShadow();
            EnsureSilhouetteShadow();
        }

        // Halat gölgesi (LinkedShipTether) gemi gölgesiyle aynı görünsün diye ayarları okur
        public bool SilhouetteShadowEnabled => m_EnableSilhouetteShadow;
        public Vector2 SilhouetteShadowDirection => m_SilhouetteShadowDirection;
        public Color SilhouetteShadowColor => m_SilhouetteShadowColor;
        public float SilhouetteShadowSoftness => m_SilhouetteShadowSoftness;
        /// <summary>Gemi gölgesinin kullandığı (çekirdek + yumuşak kenar halkaları) paylaşılan materyaller.</summary>
        public Material[] GetSilhouetteShadowMaterials()
        {
            Material mat = GetSilhouetteShadowMaterial();
            if (mat == null) return null;
            return m_SilhouetteShadowSoftness > 0.001f ? GetSilhouetteRingMaterials(mat) : new[] { mat };
        }

        private Material GetSilhouetteShadowMaterial()
        {
            if (m_SilhouetteShadowMaterial != null) return m_SilhouetteShadowMaterial;
            if (s_SilhouetteShadowFallbackMaterial == null)
            {
                Shader shader = Shader.Find("PixelGame/ShipSilhouetteShadow");
                if (shader == null) return null;
                s_SilhouetteShadowFallbackMaterial = new Material(shader) { name = "ShipSilhouetteShadow_Runtime_Mat" };
            }
            return s_SilhouetteShadowFallbackMaterial;
        }

        /// <summary>
        /// Çekirdek gölge + yumuşak kenar halkaları. Halkalar kaynak materyalden kopyalanır ve sırayla
        /// (önce çekirdek, sonra içten dışa) çizilsin diye render kuyrukları birer artar; böylece tüm
        /// gemilerin çekirdekleri halkalardan önce stencil'i doldurur.
        /// </summary>
        private static Material[] GetSilhouetteRingMaterials(Material source)
        {
            if (s_SilhouetteRingMaterials != null && s_SilhouetteRingsSource == source) return s_SilhouetteRingMaterials;

            if (s_SilhouetteRingMaterials != null)
            {
                for (int i = 1; i < s_SilhouetteRingMaterials.Length; i++)
                {
                    if (s_SilhouetteRingMaterials[i] != null) DestroyImmediate(s_SilhouetteRingMaterials[i]);
                }
            }

            int ringCount = SilhouetteRingAlphas.Length;
            var mats = new Material[ringCount];
            mats[0] = source;
            for (int i = 1; i < ringCount; i++)
            {
                var ring = new Material(source)
                {
                    name = $"{source.name}_Ring{i}",
                    hideFlags = HideFlags.DontSave,
                    renderQueue = source.renderQueue + i
                };
                ring.SetFloat("_RingT", i / (float)(ringCount - 1));
                ring.SetFloat("_RingAlpha", SilhouetteRingAlphas[i]);
                mats[i] = ring;
            }

            s_SilhouetteRingsSource = source;
            s_SilhouetteRingMaterials = mats;
            return mats;
        }

        /// <summary>
        /// Gövde mesh'ini ışık yönünde su yüzeyine yansıtıp gölge olarak çizer (yansıtma ShipSilhouetteShadow
        /// shader'ında); böylece gölge geminin silüetini ve kabin gibi yüksek kısımlarını takip eder. Gölge VisualRoot'a değil köke bağlıdır:
        /// gemi tutulup kaldırıldığında ya da dalgada sallandığında gölge suda kalır.
        /// </summary>
        public void EnsureSilhouetteShadow()
        {
            Transform existing = transform.Find(SilhouetteShadowName);
            if (!m_EnableSilhouetteShadow)
            {
                if (existing != null) existing.gameObject.SetActive(false);
                return;
            }

            MeshFilter hullFilter = m_VisualRoot != null ? m_VisualRoot.GetComponent<MeshFilter>() : null;
            Material mat = GetSilhouetteShadowMaterial();
            if (hullFilter == null || hullFilter.sharedMesh == null || mat == null) return;

            GameObject shadowObj;
            if (existing != null)
            {
                shadowObj = existing.gameObject;
            }
            else
            {
                shadowObj = new GameObject(SilhouetteShadowName);
                shadowObj.transform.SetParent(transform, false);
            }

            shadowObj.SetActive(true);
            // Yansıtma shader'da yapılır (köşeler _ShadowDir yönünde su seviyesine iner); obje gövdeyle hizalı kalır
            shadowObj.transform.localPosition = new Vector3(0f, -0.015f, 0f);
            shadowObj.transform.localRotation = Quaternion.identity;
            shadowObj.transform.localScale = Vector3.one;

            MeshFilter mf = shadowObj.GetComponent<MeshFilter>();
            if (mf == null) mf = shadowObj.AddComponent<MeshFilter>();
            mf.sharedMesh = hullFilter.sharedMesh;

            MeshRenderer mr = shadowObj.GetComponent<MeshRenderer>();
            if (mr == null) mr = shadowObj.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sharedMaterials = m_SilhouetteShadowSoftness > 0.001f ? GetSilhouetteRingMaterials(mat) : new[] { mat };

            if (m_PropBlock == null) m_PropBlock = new MaterialPropertyBlock();
            m_PropBlock.Clear();
            m_PropBlock.SetColor(SilhouetteColorId, m_SilhouetteShadowColor);
            m_PropBlock.SetVector(SilhouetteDirectionId, new Vector4(m_SilhouetteShadowDirection.x, m_SilhouetteShadowDirection.y, 0f, 0f));
            m_PropBlock.SetFloat(SilhouetteSoftnessId, m_SilhouetteShadowSoftness);
            mr.SetPropertyBlock(m_PropBlock);

            // Eski yumuşak quad gölge ve gövdenin gerçek ışık gölgesi silüetle çakışmasın
            if (m_FakeShadowObj == null)
            {
                Transform oldShadow = transform.Find("[Ship_FakeShadow]");
                if (oldShadow != null) m_FakeShadowObj = oldShadow.gameObject;
            }
            if (m_FakeShadowObj != null) m_FakeShadowObj.SetActive(false);

            MeshRenderer hullRenderer = hullFilter.GetComponent<MeshRenderer>();
            if (hullRenderer != null) hullRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void Awake()
        {
            m_PropBlock = new MaterialPropertyBlock();
            m_BobRandomOffset = UnityEngine.Random.Range(0f, 100f);
            if (transform.lossyScale != Vector3.zero) m_BaseScale = transform.lossyScale;

            if (Application.isPlaying)
            {
                EnsureDecoupledHierarchy();
                CreateOrFindBadge();
                EnsureFakeShadow();
                EnsureSilhouetteShadow();
            }
        }

        private void OnEnable()
        {
            if (!s_ActiveShips.Contains(this))
            {
                s_ActiveShips.Add(this);
            }

            if (Application.isPlaying)
            {
                EnsureDecoupledHierarchy();
                CreateOrFindBadge();
                EnsureFakeShadow();
                EnsureSilhouetteShadow();
                UpdateBadgeText();
            }
            else
            {
                ApplyColorToShip(m_ShipColor);
                QueueEditorSilhouetteShadow();
            }
        }

        private void OnDisable()
        {
            s_ActiveShips.Remove(this);

            m_IsPointerDown = false;
            m_IsDragging = false;
            m_IsPickedUp = false;
            m_DragThresholdPassed = false;
            m_WasDragged = false;
            m_CandidateSlot = null;
            m_DragSmoothVelocity = Vector3.zero;
            m_SmoothedVelocity = Vector3.zero;
            m_CurrentPickupLift = 0f;
            m_CurrentPickupScale = 1f;
            m_CurrentBankingRoll = 0f;
            m_CurrentDragPitch = 0f;
            m_CurrentDragYaw = 0f;
        }

        private void OnDestroy()
        {
            s_ActiveShips.Remove(this);
        }

        private void OnValidate()
        {
            if (m_BadgeUIText != null)
            {
                UpdateBadgeText();
            }
            ApplyColorToShip(m_ShipColor);
            EnsureFakeShadow();
            QueueEditorSilhouetteShadow();
        }

        /// <summary>
        /// Silüet gölgeyi editörde (Play'e basmadan) da gösterir. OnEnable/OnValidate içinde obje
        /// oluşturmak Unity uyarısı verdiği için kurulum bir sonraki editör döngüsüne bırakılır.
        /// Prefab asset'inin kendisine dokunulmaz; sahnedeki gemiler güncellenir.
        /// </summary>
        private void QueueEditorSilhouetteShadow()
        {
#if UNITY_EDITOR
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || Application.isPlaying) return;
                if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
                if (!gameObject.scene.IsValid()) return;
                EnsureSilhouetteShadow();
            };
#endif
        }

        private void Start()
        {
            m_BaseLocalPosition = transform.localPosition;
            m_BaseLocalRotation = transform.localRotation;
            if (transform.lossyScale != Vector3.zero) m_BaseScale = transform.lossyScale;

            m_SmoothedWorldPosition = transform.position;
            m_VisualWorldPosition = transform.position;
            m_DragTargetWorldPosition = transform.position;
            m_PreviousWorldPosition = transform.position;

            EnsureDecoupledHierarchy();
            EnsureFakeShadow();
            UpdateBadgeText();
            ApplyColorToShip(m_ShipColor);
        }

        private void Update()
        {
            UpdateDragFollow();

            if (m_EnableWaterBobbing && !m_IsMoving && !m_IsDeparting && Application.isPlaying)
            {
                ApplyWaterBobbing();
            }
            else if (m_VisualRoot != null && !m_IsMoving && !m_IsDeparting && Application.isPlaying)
            {
                Vector3 pickupOffset = new Vector3(0f, m_CurrentPickupLift, 0f);
                m_VisualRoot.localPosition = pickupOffset;

                Quaternion dragRot = Quaternion.Euler(m_CurrentDragPitch, m_CurrentDragYaw, m_CurrentBankingRoll);
                m_VisualRoot.localRotation = dragRot;
            }
        }

        /// <summary>
        /// Geminin mevcut GÖRSEL (dünya uzayı) boyutunu koruyacak local scale'i
        /// hesaplar. m_BaseScale ilk dünya ölçeği olarak tutulur; gemi başka bir ebeveyne
        /// (ör. slot) geçtiğinde ebeveynin lossyScale'ine bölünerek aynı dünya boyutunu korur.
        /// Asla 0.26f gibi sabit bir dünya boyutuyla ezilmez.
        /// </summary>
        public Vector3 GetLocalScaleForBaseWorldScale()
        {
            Vector3 baseScale = (m_BaseScale != Vector3.zero) ? m_BaseScale : transform.lossyScale;
            if (transform.parent == null) return baseScale;

            Vector3 parentLossy = transform.parent.lossyScale;
            return new Vector3(
                baseScale.x / Mathf.Max(0.0001f, parentLossy.x),
                baseScale.y / Mathf.Max(0.0001f, parentLossy.y),
                baseScale.z / Mathf.Max(0.0001f, parentLossy.z)
            );
        }

        public void SetBaseScale(Vector3 scale)
        {
            m_BaseScale = scale;
            transform.localScale = GetLocalScaleForBaseWorldScale();
        }

        public void ApplyBaseScale()
        {
            // Orijinal ölçeği koru — asla 0.26f gibi sabit bir dünya boyutu ile ezme!
            if (m_BaseScale == Vector3.zero && transform.lossyScale != Vector3.zero)
            {
                m_BaseScale = transform.lossyScale;
            }
            if (m_VisualRoot != null && m_VisualRoot.localScale != Vector3.one)
            {
                m_VisualRoot.localScale = Vector3.one;
            }
        }

        private void LateUpdate()
        {
            UpdateMysteryReveal();
            UpdateBadgePlacement();
        }

        public void UpdateBadgePlacement()
        {
            if (m_BadgeCanvasObj == null)
            {
                Transform foundTr = transform.Find("Ship_Capacity_Canvas");
                if (foundTr != null) m_BadgeCanvasObj = foundTr.gameObject;
            }
            if (m_BadgeCanvasObj == null) return;

            // Kullanıcı isteği: "textteki sayı dolduğunda text yok olsun"
            // Kapasite dolduğunda veya gemi kalkışta iken rozet ve metin KESİNLİKLE gizlenir.
            // Gizli gemide sayı yerine aynı stilde süzülen bir "?" gösterilir (UpdateBadgeText)
            if (m_IsDeparting || IsFull || RemainingCapacity <= 0)
            {
                if (m_BadgeCanvasObj.activeSelf) m_BadgeCanvasObj.SetActive(false);
                return;
            }

            Transform canvasTr = m_BadgeCanvasObj.transform;
            if (canvasTr.parent != transform)
            {
                canvasTr.SetParent(transform, true);
            }

            // Kullanıcı isteği: "gemi textlerimi eski commitlerdeki hale getir" (9be8b92 sürümü)
            // 1. Gemi kabin çatısının tam geometrik merkezi (Y=2.22f tavan düzlemi, Z=-0.42f tavan merkezi):
            Vector3 roofLocalPos = new Vector3(0f, 2.22f, -0.42f);
            // Gizli gemideki "?" çatı üstünde hafifçe süzülür
            if (m_IsMysteryHidden && Application.isPlaying)
            {
                roofLocalPos.y += (Mathf.Sin(Time.time * 3.2f + m_BobRandomOffset) * 0.5f + 0.5f) * 0.12f;
            }
            Transform sourceTr = m_VisualRoot != null ? m_VisualRoot : transform;
            Vector3 targetWorldPos = sourceTr.TransformPoint(roofLocalPos);
            if ((canvasTr.position - targetWorldPos).sqrMagnitude > 0.00001f)
            {
                canvasTr.position = targetWorldPos;
            }

            // 2. Yazı HER ZAMAN kameraya dik, düzgün ve net bakar; asla yana yatmaz, bozulmaz.
            Camera cam = MainCamera;
            Quaternion targetWorldRot = cam != null ? cam.transform.rotation : Quaternion.identity;
            if (canvasTr.rotation != targetWorldRot)
            {
                canvasTr.rotation = targetWorldRot;
            }

            // 3. Tavanın ortasına tam oturan, net, dolgun HERO TEXT
            Vector3 boatLossy = transform.lossyScale;
            float avgLossy = (Mathf.Abs(boatLossy.x) + Mathf.Abs(boatLossy.y) + Mathf.Abs(boatLossy.z)) / 3f;
            if (avgLossy < 0.0001f) avgLossy = 0.35f;

            float targetLocalScaleFactor = 0.0085f / avgLossy;
            if (m_IsMysteryHidden && Application.isPlaying)
            {
                // "?" rozeti hafifçe nefes alır gibi büyüyüp küçülür
                targetLocalScaleFactor *= 1f + Mathf.Sin(Time.time * 2.4f + m_BobRandomOffset * 1.7f) * 0.07f;
            }
            Vector3 targetLocalScale = Vector3.one * targetLocalScaleFactor;
            if ((canvasTr.localScale - targetLocalScale).sqrMagnitude > 0.000001f)
            {
                canvasTr.localScale = targetLocalScale;
            }

            // Kullanıcı isteği: "textleri büyütelim ve gemiye ortalayalım"
            // Pivot merkezde: yazının ortası tam kabin çatısının merkezine oturur (önceden alt kenarı oturuyordu → yazı pruvaya kayıyordu)
            RectTransform canvasRect = canvasTr.GetComponent<RectTransform>();
            if (canvasRect != null && canvasRect.pivot != new Vector2(0.5f, 0.5f))
            {
                canvasRect.pivot = new Vector2(0.5f, 0.5f);
            }

            if (m_BadgeText == null)
            {
                m_BadgeText = m_BadgeCanvasObj.GetComponentInChildren<TextMeshProUGUI>(true);
            }
            if (m_BadgeText != null && !m_BadgeConfigured)
            {
                RectTransform rt = m_BadgeText.rectTransform;
                if (rt != null)
                {
                    if (rt.pivot != new Vector2(0.5f, 0.5f)) rt.pivot = new Vector2(0.5f, 0.5f);
                    if (rt.anchoredPosition != Vector2.zero) rt.anchoredPosition = Vector2.zero;
                    if (rt.localPosition != Vector3.zero) rt.localPosition = Vector3.zero;
                    if (rt.sizeDelta != new Vector2(210f, 140f)) rt.sizeDelta = new Vector2(210f, 140f);
                    if (rt.localScale != Vector3.one) rt.localScale = Vector3.one;
                }

                ApplyBadgeTextStyle(m_BadgeText);
                m_BadgeConfigured = true;
            }

            if (!m_BadgeCanvasObj.activeSelf && !m_IsDeparting && !IsFull && RemainingCapacity > 0)
            {
                m_BadgeCanvasObj.SetActive(true);
            }
        }

        private void ApplyWaterBobbing()
        {
            // Kargo geldikçe tazelenen "heyecan" boost'u zamanla 0'a söner (her karede sabit bir
            // taban değerden yeniden hesaplandığı için DOTween tween'leri gibi asla birikip kayma yapmaz).
            if (m_CargoWobbleBoost > 0f)
            {
                m_CargoWobbleBoost = Mathf.Max(0f, m_CargoWobbleBoost - Time.deltaTime * m_CargoWobbleDecaySpeed);
            }

            float time = Time.time * m_BobFrequency + m_BobRandomOffset;
            float dy = Mathf.Sin(time) * m_BobHeight;
            float rollAmp = m_RollAngle * (1f + m_CargoWobbleBoost * m_CargoWobbleRollMultiplier);
            float pitchAmp = m_PitchAngle * (1f + m_CargoWobbleBoost * m_CargoWobblePitchMultiplier);
            float dRoll = Mathf.Sin(time * 0.85f) * rollAmp;
            float dPitch = Mathf.Cos(time * 0.75f) * pitchAmp;

            if (m_VisualRoot != null)
            {
                // Aşama 4.6 Senkronize Mimari:
                // transform.position zaten smooth gameplay pozisyonudur (Ship_Boat + BoxCollider).
                // VisualRoot yalnızca yerel dikey ofsetleri taşır (Pickup Lift + Water Bobbing).
                Vector3 pickupOffset = new Vector3(0f, m_CurrentPickupLift, 0f);
                Vector3 bobbingOffset = new Vector3(0f, dy, 0f);
                Vector3 dipOffset = new Vector3(0f, m_CurrentDipOffset, 0f);

                m_VisualRoot.localPosition = pickupOffset + bobbingOffset + dipOffset;

                // Aşama 4 Rotation Composition:
                // FinalRotation = BobbingRotation * DynamicDragRotation (Pitch + Yaw + Banking Roll)
                Quaternion bobbingRot = Quaternion.Euler(dPitch, 0f, dRoll);
                Quaternion dragRot = Quaternion.Euler(m_CurrentDragPitch, m_CurrentDragYaw, m_CurrentBankingRoll);
                m_VisualRoot.localRotation = bobbingRot * dragRot;
            }
            else
            {
                transform.localPosition = m_BaseLocalPosition + new Vector3(0f, dy, 0f);
                transform.localRotation = m_BaseLocalRotation * Quaternion.Euler(dPitch, 0f, dRoll);
            }

            // Gemi drag ile kaldırıldığında (pickup lift) sahte gölgenin su üzerinde hafif büyüyüp yayılması
            if (m_FakeShadowObj != null && m_EnableFakeShadow)
            {
                if (m_CurrentPickupLift > 0.001f)
                {
                    float liftRatio = Mathf.Clamp01(m_CurrentPickupLift / Mathf.Max(0.001f, m_PickupLift));
                    float scaleMul = 1f + 0.12f * liftRatio;
                    m_FakeShadowObj.transform.localScale = new Vector3(m_FakeShadowSize.x * scaleMul, m_FakeShadowSize.y * scaleMul, 1f);
                    m_FakeShadowObj.transform.localPosition = new Vector3(
                        m_FakeShadowOffset.x * (1f + 0.25f * liftRatio),
                        -0.015f,
                        m_FakeShadowOffset.y * (1f + 0.25f * liftRatio)
                    );
                }
                else
                {
                    m_FakeShadowObj.transform.localScale = new Vector3(m_FakeShadowSize.x, m_FakeShadowSize.y, 1f);
                    m_FakeShadowObj.transform.localPosition = new Vector3(m_FakeShadowOffset.x, -0.015f, m_FakeShadowOffset.y);
                }
            }
        }

        /// <summary>
        /// Gemi slota yanaştığında suyun içine hafifçe dalıp yaylanmasını sağlar (Water Dip Impact).
        /// </summary>
        public void TriggerWaterDipImpact(float depth = 0.18f, float duration = 0.52f)
        {
            if (!gameObject.activeInHierarchy) return;
            if (m_ShipDipCoroutine != null)
            {
                StopCoroutine(m_ShipDipCoroutine);
            }
            m_ShipDipCoroutine = StartCoroutine(ShipWaterDipRoutine(depth, duration));
        }

        private IEnumerator ShipWaterDipRoutine(float depth, float duration)
        {
            // 1. Faz: Suya ani dalış / batma (Plunge) - Tok ve tatlı darbe
            float plungeTime = duration * 0.28f;
            float elapsed = 0f;
            while (elapsed < plungeTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / plungeTime);
                float easeOut = Mathf.Sin(t * Mathf.PI * 0.5f);
                m_CurrentDipOffset = Mathf.Lerp(0f, -depth, easeOut);
                yield return null;
            }

            // 2. Faz: Suyun kaldırma kuvvetiyle yukarı geri fırlama / yaylanma (Rebound)
            float reboundTime = duration * 0.36f;
            elapsed = 0f;
            float reboundHeight = depth * 0.35f;
            while (elapsed < reboundTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / reboundTime);
                float ease = Mathf.Sin(t * Mathf.PI * 0.5f);
                m_CurrentDipOffset = Mathf.Lerp(-depth, reboundHeight, ease);
                yield return null;
            }

            // 3. Faz: Durgunlaşma ve normal su seviyesine sönümlü oturma (Settle)
            float settleTime = duration * 0.36f;
            elapsed = 0f;
            while (elapsed < settleTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / settleTime);
                float smooth = Mathf.SmoothStep(0f, 1f, t);
                m_CurrentDipOffset = Mathf.Lerp(reboundHeight, 0f, smooth);
                yield return null;
            }

            m_CurrentDipOffset = 0f;
            m_ShipDipCoroutine = null;
        }

        /// <summary>
        /// Görsel katmanın (VisualRoot) yerel offset ve rotasyonunu sıfırlar (0,0,0).
        /// Hareket, yanaşma veya animasyon geçişlerinde çağrılır.
        /// </summary>
        public void ResetVisualOffset()
        {
            m_SmoothedWorldPosition = transform.position;
            m_VisualWorldPosition = transform.position;
            m_PreviousWorldPosition = transform.position;
            m_DragSmoothVelocity = Vector3.zero;
            m_SmoothedVelocity = Vector3.zero;
            m_VelocitySmoothDeriv = Vector3.zero;
            m_CurrentPickupLift = 0f;
            m_PickupLiftVelocity = 0f;
            m_CurrentPickupScale = 1f;
            m_PickupScaleVelocity = 0f;
            m_CurrentBankingRoll = 0f;
            m_RollSmoothVelocity = 0f;
            m_CurrentDragPitch = 0f;
            m_PitchSmoothVelocity = 0f;
            m_CurrentDragYaw = 0f;
            m_YawSmoothVelocity = 0f;

            if (m_VisualRoot != null)
            {
                m_VisualRoot.localPosition = Vector3.zero;
                m_VisualRoot.localRotation = Quaternion.identity;
                m_VisualRoot.localScale = Vector3.one;
            }
        }

        /// <summary>
        /// Kargo alındığında çağrılır: geminin idle su salınımını kısa süreliğine daha canlı/enerjik
        /// (daha büyük genlikte) yapar, sonra doğal olarak sönümlenip normal sakin salınıma döner.
        /// </summary>
        private void TriggerCargoWobble()
        {
            m_CargoWobbleBoost = 1f;
        }

        [Header("📦 Dinamik Varil Güvertesi (Cargo Deck)")]
        [SerializeField] private Transform m_CargoDeckRoot;
        private readonly List<GameObject> m_SpawnedBarrels = new List<GameObject>();
        private static Mesh s_BarrelMesh;
        private Material m_BarrelSharedMaterial;

        /// <summary>
        /// Gemiyi belirli bir renk ve kapasite ile yapılandırır.
        /// Başlangıçta güverte tamamen boştur, küpler geldikçe aynı renkte variller oluşur.
        /// </summary>
        public void Configure(Color color, int capacity, string colorName = "")
        {
            m_ShipColor = color;
            m_IsMysteryHidden = false; // Gizlilik durumunu kuyruk (SetMysteryHidden) belirler
            m_Capacity = Mathf.Max(1, capacity);
            m_CurrentCargo = 0;
            m_PendingCargo = 0;
            m_ColorName = string.IsNullOrEmpty(colorName) ? ColorUtility.ToHtmlStringRGB(color) : colorName;

            ClearCargoBarrels();
            EnsureVisualComponents();
            CreateOrFindBadge();
            EnsureFakeShadow();
            ApplyColorToShip(color);
            UpdateBadgeText();
        }

        private static Texture2D s_WhiteTex;
        public static Texture2D GetWhiteTexture()
        {
            if (s_WhiteTex != null) return s_WhiteTex;
            s_WhiteTex = new Texture2D(1, 1);
            s_WhiteTex.SetPixel(0, 0, Color.white);
            s_WhiteTex.Apply();
            return s_WhiteTex;
        }

        private static readonly Dictionary<Color32, Texture2D> s_CachedBoatTextures = new Dictionary<Color32, Texture2D>();
        private static readonly Dictionary<Color32, Material> s_CachedBoatMaterials = new Dictionary<Color32, Material>();

        public static void ClearMaterialCache()
        {
            s_CachedBoatTextures.Clear();
            s_CachedBoatMaterials.Clear();
        }

        public static Texture2D GetOrCreateTintedBoatTexture(Color shipColor)
        {
            Color32 key = (Color32)shipColor;
            key.a = 255;
            if (s_CachedBoatTextures.TryGetValue(key, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            int size = 64;
            int tileSize = size / 16;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = $"BoatAtlas_{ColorUtility.ToHtmlStringRGB(shipColor)}";
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            // Kenney boat-house-a palet eşlemesi:
            // Tile 1: Cam pencereler (açık gök mavisi)
            // Tile 7: ANA GÖVDE VE KABİN DUVARLARI (624 yüzey - Takım Rengi!)
            // Tile 9: KABİN TAVANI (84 yüzey - Kullanıcı isteği: ortadaki küplerin renginde!)
            // Tile 11: SU HATTI / ALT OMURGA (40 yüzey - Takım renginin derin gölgesi)
            // Tile 13: GÜVERTE & KOKPİT GİRİNTİSİ (28 yüzey - Sıcak ahşap kahverengi)
            Color32 glassColor = new Color32(185, 228, 255, 255);
            Color32 hullMain = (Color32)shipColor;
            hullMain.a = 255;
            // Kullanıcı isteği: "ortadaki küplerin renginde olsun textlerin altındaki kısımda"
            // Çatı (Tile 9, metnin altındaki zemin): Ortadaki küplerle birebir aynı canlı renk!
            Color32 roofColor = hullMain;
            Color32 waterlineColor = shipColor * 0.70f;
            waterlineColor.a = 255;
            // Kullanıcı isteği: "gemilerin o kısmını kahverengi yapmıştık ama tekrar siyaha çevirelim siyah olan gemi için sadece beyaz olsun o kısmı ayırt edilebilir olması açısından canım"
            float maxChannel = Mathf.Max(shipColor.r, Mathf.Max(shipColor.g, shipColor.b));
            float luminance = 0.299f * shipColor.r + 0.587f * shipColor.g + 0.114f * shipColor.b;
            bool isBlackShip = maxChannel < 0.28f || luminance < 0.22f;

            Color32 deckColor = isBlackShip
                ? new Color32(245, 248, 252, 255)  // Siyah gemide ayırt edilebilir olması için BEYAZ
                : new Color32(32, 35, 42, 255);     // Diğer tüm gemilerde tekrar SİYAH

            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int tileX = x / tileSize;
                    Color32 pixelCol;
                    if (tileX == 1)
                        pixelCol = glassColor;
                    else if (tileX == 7)
                        pixelCol = hullMain;   // 624 yüzey -> Ana gövde ve kabin!
                    else if (tileX == 9)
                        pixelCol = roofColor;  // 84 yüzey -> Tavan (yazının altındaki zemin!)
                    else if (tileX == 11)
                        pixelCol = waterlineColor; // 40 yüzey -> Su altı
                    else if (tileX == 13)
                        pixelCol = deckColor;  // 28 yüzey -> Güverte/tampon
                    else
                        pixelCol = hullMain;

                    pixels[y * size + x] = pixelCol;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            s_CachedBoatTextures[key] = tex;
            return tex;
        }

        public static Material GetOrCreateBoatMaterial(Color color)
        {
            Color32 key = (Color32)color;
            key.a = 255;
            if (s_CachedBoatMaterials.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            Material baseMat = null;
#if UNITY_EDITOR
            baseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PixelCube_Cartoon.mat");
            if (baseMat == null)
            {
                baseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ship_Watercraft_Mat.mat");
            }
#endif
            Material mat = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Toony Colors Pro 2/PixelGame/Cartoon") ?? Shader.Find("Universal Render Pipeline/Lit"));
            mat.name = $"Ship_BoatMat_{ColorUtility.ToHtmlStringRGB(color)}";

            // Kenney atlas tabanlı akıllı palet dokusu (Pencereler cam mavisi, tamponlar grafit kauçuk, tavan açık vurgulu, gövde takım rengi):
            Texture2D boatAtlas = GetOrCreateTintedBoatTexture(color);
            mat.SetTexture("_BaseMap", boatAtlas);
            mat.SetTexture("_MainTex", boatAtlas);
            // Beyaz BaseColor çarpanı ile dokudaki çoklu materyal tonları (cam, kauçuk, tavan, gövde) korunur:
            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_Color", Color.white);

            // PixelCube_Cartoon.mat ile %100 birebir aynı aydınlatma, gölge (HColor/SColor),
            // ramp threshold/smoothing, top-light ve yüzey tonlamasını uygula:
            CartoonShader.ApplyColor(mat, Color.white);
            if (mat.HasProperty("_MatCapColor")) mat.SetColor("_MatCapColor", new Color(0.12f, 0.12f, 0.12f, 1f));

            // Küplerle aynı tonlama kalsın ama gemi parlamasın: ApplyColor küplerin plastik vurgu şeridini,
            // specular'ı ve kenar parlamasını da açıyor; gövdenin sağ kenarında beyaz bir parlama yapıyordu.
            if (mat.HasProperty("_PlasticHighlightIntensity")) mat.SetFloat("_PlasticHighlightIntensity", 0f);
            if (mat.HasProperty("_PlasticHighlightColor")) mat.SetColor("_PlasticHighlightColor", Color.black);
            if (mat.HasProperty("_SpecularColor")) mat.SetColor("_SpecularColor", Color.black);
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", Color.clear);

            s_CachedBoatMaterials[key] = mat;
            return mat;
        }

        /// <summary>
        /// Gemiyi küplerle %100 aynı tonda mat cartoon renk ile boyar.
        /// </summary>
        public void ApplyColorToShip(Color color)
        {
            m_ShipColor = color;
            EnsureVisualComponents();
            CreateOrFindBadge();
            UpdateBadgePlacement();
            UpdateBadgeText();

            if (m_Renderers == null || m_Renderers.Length == 0)
            {
                m_Renderers = GetComponentsInChildren<MeshRenderer>(true);
            }

            Material boatMat = GetOrCreateBoatMaterial(color);

            foreach (var mr in m_Renderers)
            {
                if (mr == null) continue;
                // Gölge quad'ını asla gövde materyaliyle boyama
                if (mr.name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (m_FakeShadowObj != null && (mr.gameObject == m_FakeShadowObj || mr.transform.IsChildOf(m_FakeShadowObj.transform))) continue;
                // Dinamik varil renderers'ını ana gövde boyamasından ayrı tut
                if (m_CargoDeckRoot != null && mr.transform.IsChildOf(m_CargoDeckRoot)) continue;

                if (Application.isPlaying)
                {
                    mr.material = boatMat;
                }
                else
                {
                    mr.sharedMaterial = boatMat;
                }
            }

            // Mevcut oluşturulmuş variller varsa renklerini de güncelle
            UpdateAllBarrelsColor();

            // Gizli gemi hâlâ örtülüyse renk güncellemesi örtüyü ezmesin
            if (m_IsMysteryHidden) ApplyMysteryCover();
        }

        /// <summary>
        /// Gemiye kargo (küp parçacığı) ekler ve güvertede geminin renginde bir 3D varil oluşturur.
        /// </summary>
        public void AddCargo(int amount = 1)
        {
            // Rezervasyon her hâlükârda tüketilir — gemi kalkıyor olsa bile sayaç sızmasın,
            // yoksa gemi bir daha hiç "dolabilir" duruma gelemez.
            m_PendingCargo = Mathf.Max(0, m_PendingCargo - amount);

            if (m_IsDeparting) return;

            m_CurrentCargo = Mathf.Min(m_Capacity, m_CurrentCargo + amount);
            UpdateBadgeText();

            // Referans videodaki gibi küp geldikçe canlı, enerjik yaylanma / squash tepkisi
            PlayCargoReceiveJuice();

            if (IsFull && !m_IsDeparting)
            {
                OnCargoFilled?.Invoke(this);
                if (ShipDispatcher.Instance != null) ShipDispatcher.Instance.OnShipFilled(this);
                DepartAndFreeSlot();
            }
        }

        public void PlayCargoReceiveJuice()
        {
            transform.DOKill(true);
            transform.DOPunchScale(new Vector3(0.065f, -0.065f, 0.065f) * m_BaseScale.x, 0.12f, 2, 0.45f)
                .OnComplete(() => transform.localScale = GetLocalScaleForBaseWorldScale());
        }

        /// <summary>
        /// Kargo dolduğunda gemi slottan çıkar, sol tarafa doğru kavisli deniz rotasıyla hızlanarak yol alır ve yok olur.
        /// Kullanıcının isteği: Text ve dairesel rozet gemi dolduğu an tamamen yok olur; gemi sol tarafa giderek kaybolur.
        /// </summary>
        public void DepartAndFreeSlot()
        {
            if (m_IsDeparting) return;
            m_IsDeparting = true;
            m_EnableWaterBobbing = false;

            // Halat/zincir varsa bağını kopar ve partneri serbest bırak
            if (m_Tether != null)
            {
                Destroy(m_Tether.gameObject);
                m_Tether = null;
            }
            if (m_LinkedPartner != null)
            {
                m_LinkedPartner.SetLinkedPartner(null, 0, null);
                m_LinkedPartner = null;
            }
            m_LinkId = 0;

            // Rozeti / texti HEMEN yok et (Kullanıcı: "textteki sayı dolduğunda text yok olsun")
            if (m_BadgeCanvasObj != null)
            {
                m_BadgeCanvasObj.SetActive(false);
            }
            if (m_BadgeUIText != null)
            {
                m_BadgeUIText.gameObject.SetActive(false);
            }
            if (m_BadgeImage != null)
            {
                m_BadgeImage.gameObject.SetActive(false);
            }

            StartCoroutine(DepartToLeftRoutine());
        }

        [ContextMenu("🚢 Test Depart To Left (Test Kalkış)")]
        public void TestDepartToLeft()
        {
            DepartAndFreeSlot();
        }

        private IEnumerator DepartToLeftRoutine()
        {
            // 1. Text ve rozet hemen kaybolur
            if (m_BadgeCanvasObj != null) m_BadgeCanvasObj.SetActive(false);
            if (m_BadgeUIText != null) m_BadgeUIText.gameObject.SetActive(false);
            if (m_BadgeImage != null) m_BadgeImage.gameObject.SetActive(false);

            // Slottan dünya koordinatlarına çık
            transform.SetParent(null, true);
            transform.DOKill(true);
            ResetVisualOffset();

            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;

            // Slottan ayrılış: Geminin slotu serbest bırakmasını geri çıkış tamamlanana kadar bekletiriz
            ShipSlot slotToFree = m_CurrentSlot;
            m_CurrentSlot = null;

            // =========================================================================
            // FAZ 1: GERİ ÇIKIŞ (Slottan geriye doğru süzülerek çıkma)
            // Kullanıcı isteği: "gemiler dolduğunda hafif geri gidip öyle yollarına devam etsinler
            // yani slottan çıksınlar sonra sola gitsinler"
            // =========================================================================
            float reverseDist = 1.35f;
            Vector3 reverseDir = -transform.forward;
            Vector3 reversePos = startPos + reverseDir * reverseDist;

            float reverseDuration = 0.38f;
            float reverseElapsed = 0f;
            float lastRippleTime = 0f;

            // Kalkış anında kıç tarafında su dalgası ve hafif motor egzoz dumanı
            SpawnWaterRipple(startPos - transform.forward * 0.30f, 0.22f, 0.75f, 0.35f);
            SpawnSmokePuff(startPos - transform.forward * 0.30f, 0.12f, 0.35f, 0.35f);

            while (reverseElapsed < reverseDuration)
            {
                reverseElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(reverseElapsed / reverseDuration);
                // Slottan geriye doğru çıkarken tatlı bir frenleme eğrisi (SmoothStep)
                float ease = Mathf.SmoothStep(0f, 1f, t);

                transform.position = Vector3.Lerp(startPos, reversePos, ease);
                transform.rotation = startRot;

                if (Time.time - lastRippleTime > 0.075f)
                {
                    lastRippleTime = Time.time;
                    Vector3 sternPos = transform.position - transform.forward * 0.30f;
                    SpawnWaterRipple(sternPos, 0.20f, 0.60f, 0.30f);
                }

                yield return null;
            }

            transform.position = reversePos;

            // Slottan tamamen geriye çıkınca slotu serbest bırak ve diğer gemileri kaydır
            if (slotToFree != null)
            {
                slotToFree.ReleaseShip();
                slotToFree = null;
            }

            if (ShipDispatcher.Instance != null)
            {
                ShipDispatcher.Instance.CompactSlots(0.05f);
            }

            // =========================================================================
            // FAZ 2: SOLA DÖNÜŞ VE HIZLANARAK AYRILIŞ (Açık suda sola dümen kırıp hızlanma)
            // =========================================================================
            // Su yüzeyi düzleminde sola bakan hedef rotasyon (Lokal Y ekseninde -90° dönüş):
            Quaternion leftTargetRot = Quaternion.Euler(-68f, 0f, 0f) * Quaternion.Euler(0f, -90f, 0f);

            // Ekranın sol kenarından tamamen çıkacak hedef koordinat (X = -8.8f):
            Vector3 departTarget = new Vector3(-8.8f, reversePos.y, reversePos.z);

            // 4 Noktalı Pürüzsüz Bezier Su Rotası (reversePos'tan sola doğru hızlanan rota)
            Vector3 p0 = reversePos;
            Vector3 p1 = reversePos + new Vector3(-1.1f, 0f, 0f);
            Vector3 p2 = new Vector3(Mathf.Lerp(p1.x, departTarget.x, 0.38f), reversePos.y, reversePos.z);
            Vector3 p3 = departTarget;

            float cruiseDuration = 0.95f;
            float cruiseElapsed = 0f;
            float lastSmokeTime = 0f;

            while (cruiseElapsed < cruiseDuration)
            {
                cruiseElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(cruiseElapsed / cruiseDuration);

                // Hızlanarak sol kenardan çıkış (Ease-in akışı)
                float moveT = (t < 0.20f) ? (2.5f * t * t) : (t - 0.10f) / 0.90f;
                moveT = Mathf.Clamp01(moveT);

                // Konum güncellemesi
                Vector3 currentPos = EvaluateCubicBezier(p0, p1, p2, p3, moveT);
                transform.position = currentPos;

                // Slottan çıktıktan sonra burnunu sola çevirir (t: 0.0 -> 0.38)
                float turnT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.38f));
                float bankRoll = Mathf.Sin(turnT * Mathf.PI) * 7.5f;

                transform.rotation = Quaternion.Slerp(startRot, leftTargetRot, turnT);
                if (m_VisualRoot != null)
                {
                    m_VisualRoot.localRotation = Quaternion.Euler(0f, 0f, bankRoll);
                }
                else
                {
                    transform.rotation = transform.rotation * Quaternion.Euler(0f, 0f, bankRoll);
                }

                // Referans videodaki gibi arkasında beyaz puf duman bulutları ve köpük izi
                if (Time.time - lastSmokeTime > 0.040f)
                {
                    lastSmokeTime = Time.time;
                    Vector3 exhaustPos = currentPos - transform.forward * 0.35f + new Vector3(0f, -0.05f, 0.02f);
                    SpawnSmokePuff(exhaustPos, 0.12f, 0.36f, 0.38f);
                    SpawnWaterRipple(exhaustPos, 0.18f, 0.65f, 0.38f);
                }

                yield return null;
            }

            OnDeparted?.Invoke(this);
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        /// <summary>
        /// Slotun gemi yanaşma merkezinin dünya koordinatını döner.
        /// </summary>
        public static Vector3 GetSlotDockPosition(ShipSlot slot)
        {
            if (slot == null) return Vector3.zero;
            return slot.transform.TransformPoint(new Vector3(0f, DefaultDockHeightOffset, DefaultDockForwardOffset));
        }

        public Vector3 GetDockPosition(ShipSlot slot)
        {
            if (slot == null) return Vector3.zero;
            return slot.transform.TransformPoint(new Vector3(0f, m_DockHeightOffset, m_DockForwardOffset));
        }

        /// <summary>
        /// Sahnede yer alan mevcut slotları döner. Yeni GameObject veya trigger oluşturulmaz.
        /// </summary>
        public static IReadOnlyList<ShipSlot> GetAllSlots()
        {
            return ShipSlot.ActiveSlots;
        }

        /// <summary>
        /// Geminin belirtilen slota yanaşmaya uygun olup olmadığını doğrular.
        /// </summary>
        public bool CanDockInSlot(ShipSlot slot)
        {
            if (slot == null || !slot.IsEmpty) return false;
            if (m_IsDocked || m_IsMoving || m_IsDeparting) return false;

            // Kuyruktaki gemiler için sıra kontrolü (yalnızca ön sıradaki veya serbest bağlı gemiler slota yanaşabilir)
            ShipQueuePool queuePool = UnityEngine.Object.FindFirstObjectByType<ShipQueuePool>();
            if (queuePool != null && queuePool.WaitingShips != null && queuePool.WaitingShips.Contains(this))
            {
                if (IsLinked)
                {
                    if (!CanDispatchLinked()) return false;
                }
                else
                {
                    if (!queuePool.IsFrontRow(this)) return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Verilen detection dünya pozisyonuna en yakın geçerli ve boş slotu bulur.
        /// Slot detection için Ship_Boat root'unun anlık gameplay pozisyonu referans alınır.
        /// VisualRoot'un pickup lift, bobbing, pitch, roll, banking offsetleri dahil edilmez.
        /// </summary>
        public ShipSlot FindCandidateSlot(Vector3 detectionPos, out float closestDist)
        {
            closestDist = float.MaxValue;
            ShipSlot closestSlot = null;

            IReadOnlyList<ShipSlot> allSlots = GetAllSlots();
            if (allSlots == null || allSlots.Count == 0) return null;

            for (int i = 0; i < allSlots.Count; i++)
            {
                ShipSlot slot = allSlots[i];
                if (slot == null) continue;

                // Yalnızca boş ve bu geminin yanaşabileceği slotlar aday olabilir
                if (!slot.IsEmpty || !CanDockInSlot(slot)) continue;

                Vector3 dockPos = GetSlotDockPosition(slot);
                float dist = Vector3.Distance(detectionPos, dockPos);

                if (dist <= m_DetectionRadius && dist < closestDist)
                {
                    closestDist = dist;
                    closestSlot = slot;
                }
            }

            return closestSlot;
        }

        /// <summary>
        /// Gemiyi bekleme sırasından hedef slota doğru gerçek bir gemi gibi kavisli Bezier su rotasıyla yüzdürür.
        /// </summary>
        public void SailToSlot(ShipSlot targetSlot, Action onComplete = null)
        {
            SailToSlot(targetSlot, -1f, onComplete);
        }

        /// <summary>
        /// Gemiyi hedef slota belirtilen geçiş süresiyle kavisli Bezier su rotasıyla yüzdürür (Aşama 5 Snap).
        /// </summary>
        public void SailToSlot(ShipSlot targetSlot, float customDuration, Action onComplete = null)
        {
            if (targetSlot == null) return;
            StartCoroutine(SailToSlotRoutine(targetSlot, customDuration, onComplete));
        }

        private IEnumerator SailToSlotRoutine(ShipSlot targetSlot, float customDuration = -1f, Action onComplete = null)
        {
            m_IsMoving = true;
            m_IsDragging = false;
            m_IsPickedUp = false;
            m_CandidateSlot = null;
            m_EnableWaterBobbing = false;
            ResetVisualOffset();
            transform.DOKill(true);

            // Eski slottan ayrıl
            if (m_CurrentSlot != null && m_CurrentSlot != targetSlot)
            {
                m_CurrentSlot.ReleaseShip();
            }

            // Slota bağla
            targetSlot.DockShip(this);
            m_CurrentSlot = targetSlot;

            Vector3 startWorldPos = transform.position;
            Vector3 startWorldScale = transform.lossyScale;
            Quaternion startRot = transform.rotation;

            Vector3 targetLocalPos = new Vector3(0f, m_DockHeightOffset, m_DockForwardOffset);
            Quaternion targetSlotWorldRot = targetSlot.transform.rotation;
            Vector3 targetWorld = targetSlot.transform.TransformPoint(targetLocalPos);
            Vector3 slotUp = targetSlotWorldRot * Vector3.up;

            // 1. PICKUP / START: VisualRoot üzerinde hafif kalkış ve minik scale (1.04x)
            if (m_VisualRoot != null)
            {
                m_VisualRoot.localPosition = new Vector3(0f, 0.04f, 0f);
                m_VisualRoot.localScale = Vector3.one * 1.04f;
            }

            // 2 & 3. TEK, PÜRÜZSÜZ VE DOĞAL BEZIER ROTASI
            // P0: Geminin kalkış noktası
            // P3: Slotun tam varış noktası
            // P1 & P2: Keyfi dünya eksenleri yerine doğrudan start->target yönü ve slot giriş ekseninden türetilir.
            Vector3 p0 = startWorldPos;
            Vector3 p3 = targetWorld;
            Vector3 delta = p3 - p0;
            float dist = delta.magnitude;

            // Kalkış doğrultusu (geminin mevcut ileri yönü ile hedefe doğru yönün pürüzsüz karışımı)
            Vector3 vStart = Vector3.Lerp(transform.forward, delta.normalized, 0.45f).normalized;
            Vector3 p1 = p0 + vStart * (dist * 0.38f);

            // Varış doğrultusu: Slotun kendi ileri ekseni boyunca yaklaşır.
            // Bu sayede teğet (P3 - P2) doğrudan slotun içine bakar; ASLA yana kayma (sideways drift) yapmaz!
            Vector3 vSlot = (targetSlotWorldRot * Vector3.forward).normalized;
            Vector3 p2 = p3 - vSlot * (dist * 0.32f);

            // Snap durumunda m_SnapDuration (0.16s), normal click durumunda 0.46s
            float duration = (customDuration > 0f) ? customDuration : 0.46f;
            float elapsed = 0f;
            float lastSmokeTime = 0f;
            float lateralDelta = targetWorld.x - startWorldPos.x;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 5. HIZ PROFİLİ: %0 yavaş -> %20 hızlan -> %70 seyir -> %90 yavaşla -> %100 oturma
                float easeT = Mathf.SmoothStep(0f, 1f, t);

                // Pozisyon:
                Vector3 currentWorldPos = EvaluateCubicBezier(p0, p1, p2, p3, easeT);
                transform.position = currentWorldPos;

                // 4. ROTASYON: Rotasyon hareketin anlık teğetine bakar (Forward = Path Tangent)
                Vector3 tangent = 3f * (1f - easeT) * (1f - easeT) * (p1 - p0) +
                                  6f * (1f - easeT) * easeT * (p2 - p1) +
                                  3f * easeT * easeT * (p3 - p2);

                if (tangent.sqrMagnitude > 1e-5f)
                {
                    Quaternion pathRot = Quaternion.LookRotation(tangent.normalized, slotUp);
                    // Rota sonuna yaklaştıkça (%70+) slotun kesin açısıyla tam hizalanır
                    float alignWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((easeT - 0.70f) / 0.30f));
                    transform.rotation = Quaternion.Slerp(pathRot, targetSlotWorldRot, alignWeight);
                }

                // Viraja yatma (Bank Roll) m_VisualRoot üzerinde uygulanır
                float bankRoll = Mathf.Sin(easeT * Mathf.PI) * (-Mathf.Sign(lateralDelta) * Mathf.Clamp(Mathf.Abs(lateralDelta) * 4.5f, 1.5f, 6.0f));
                float finalRollWeight = 1f - Mathf.Clamp01((easeT - 0.65f) / 0.35f);
                if (m_VisualRoot != null)
                {
                    m_VisualRoot.localRotation = Quaternion.Euler(0f, 0f, bankRoll * finalRollWeight);
                    // Ölçek varışa doğru normale döner
                    m_VisualRoot.localScale = Vector3.Lerp(Vector3.one * 1.04f, Vector3.one, easeT);
                    m_VisualRoot.localPosition = new Vector3(0f, Mathf.Lerp(0.04f, 0f, easeT), 0f);
                }

                // Duman ve su dalgası efekti
                if (Time.time - lastSmokeTime > 0.045f)
                {
                    lastSmokeTime = Time.time;
                    Vector3 exhaustPos = currentWorldPos - transform.forward * 0.32f + new Vector3(0f, -0.05f, 0.02f);
                    SpawnSmokePuff(exhaustPos, 0.10f, 0.28f, 0.32f);
                    SpawnWaterRipple(exhaustPos, 0.15f, 0.52f, 0.35f);
                }

                yield return null;
            }

            // 6. FINAL SNAP & DOCKING
            transform.SetParent(targetSlot.transform, true);
            transform.localPosition = targetLocalPos;
            transform.localRotation = Quaternion.identity;
            m_BaseLocalRotation = Quaternion.identity;
            transform.localScale = GetLocalScaleForBaseWorldScale();

            m_BaseLocalPosition = targetLocalPos;
            ResetVisualOffset();

            // 6.b) SETTLING MOVEMENT: 0.10 saniyelik çok tatlı, doğal sönümlü yaylanma
            float settleDur = 0.10f;
            float settleElapsed = 0f;
            while (settleElapsed < settleDur)
            {
                settleElapsed += Time.deltaTime;
                float sT = Mathf.Clamp01(settleElapsed / settleDur);
                float settleDip = Mathf.Sin(sT * Mathf.PI) * 0.035f * (1f - sT);
                if (m_VisualRoot != null)
                {
                    m_VisualRoot.localPosition = new Vector3(0f, -settleDip, 0f);
                    m_VisualRoot.localRotation = Quaternion.identity;
                    m_VisualRoot.localScale = Vector3.one;
                }
                yield return null;
            }

            if (m_VisualRoot != null)
            {
                m_VisualRoot.localPosition = Vector3.zero;
                m_VisualRoot.localRotation = Quaternion.identity;
                m_VisualRoot.localScale = Vector3.one;
            }

            // Su etkisi ve dalga
            if (targetSlot != null)
            {
                targetSlot.TriggerWaterDipImpact(0.18f, 0.52f);
            }
            TriggerWaterDipImpact(0.18f, 0.52f);
            SpawnWaterRipple(transform.position, 0.35f, 1.25f, 0.55f);

            m_IsMoving = false;
            m_IsDocked = true;
            m_EnableWaterBobbing = true;

            // ÖNEMLİ BUG DÜZELTMESİ: Gemi slota vardığında hemen CompactSlots ÇAĞRILMAZ.
            // CompactSlots yalnızca bir gemi ayrıldığında (boşluk açıldığında) çalışmalıdır;
            // aksi halde slota yeni giren gemiyi anında yana doğru kaydırıyordu.
            if (ShipDispatcher.Instance != null)
            {
                ShipDispatcher.Instance.OnShipDocked(this);
            }

            onComplete?.Invoke();
        }

        [Header("🧭 Slota Gidişte Yönelme")]
        [Tooltip("Slota giderken geminin burnunu rota yönüne çevirebileceği en büyük açı (derece). 0 = eski davranış (düz kayma).")]
        [Range(0f, 60f)]
        [SerializeField] private float m_SailHeadingMaxDegrees = 35f;
        [Tooltip("Bu mesafeden kısa gidişlerde (ör. sürükle-bırak snap) yönelme orantılı olarak azalır; kısa hamlede titreme olmasın.")]
        [SerializeField] private float m_SailHeadingFullDistance = 1.2f;

        /// <summary>
        /// Slota giderken geminin burnunu rotanın teğetine çeviren sapma açısı (slotUp ekseni etrafında).
        /// Küplerin yürürken gittikleri yöne dönmesi gibi: başta rotaya yönelir, yol boyunca kavisi takip eder,
        /// son kısımda slotun düz duruşuna geri döner. Açı sınırlıdır, yoksa geri/yan giden gemi tersine dönerdi.
        /// </summary>
        private float ComputeSailHeadingYaw(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float easeT, float t,
            Quaternion baseRot, Vector3 up, float travelDistance)
        {
            if (m_SailHeadingMaxDegrees <= 0f) return 0f;

            Vector3 tangent = EvaluateCubicBezierTangent(p0, p1, p2, p3, easeT);
            tangent = Vector3.ProjectOnPlane(tangent, up);
            Vector3 forward = Vector3.ProjectOnPlane(baseRot * Vector3.forward, up);
            if (tangent.sqrMagnitude < 1e-8f || forward.sqrMagnitude < 1e-8f) return 0f;

            float yaw = Mathf.Clamp(Vector3.SignedAngle(forward, tangent, up), -m_SailHeadingMaxDegrees, m_SailHeadingMaxDegrees);

            // Yumuşak giriş (ilk %20) ve slota yaklaşırken düz duruşa dönüş (son %35)
            float turnIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.2f));
            float turnOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.65f) / 0.35f));
            float distanceWeight = m_SailHeadingFullDistance > 0.001f ? Mathf.Clamp01(travelDistance / m_SailHeadingFullDistance) : 1f;

            return yaw * turnIn * turnOut * distanceWeight;
        }

        private static Vector3 EvaluateCubicBezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            return 3f * u * u * (p1 - p0) + 6f * u * t * (p2 - p1) + 3f * t * t * (p3 - p2);
        }

        private static Vector3 EvaluateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector3 p = uuu * p0;
            p += 3f * uu * t * p1;
            p += 3f * u * tt * p2;
            p += ttt * p3;
            return p;
        }

        /// <summary>
        /// Su üzerinde genişleyip sönen beyaz köpük dalgası (Water Ripple Ring) oluşturur.
        /// </summary>
        public static void SpawnWaterRipple(Vector3 worldPos, float startScale = 0.2f, float maxScale = 0.75f, float duration = 0.45f)
        {
            // Arka plandaki su dokusunun canlı dalgalanmasını da tetikle
            HypercasualWaterController.TriggerWaterRipple(worldPos, 0.55f, Mathf.Clamp(maxScale * 0.22f, 0.08f, 0.28f));

            GameObject ripple = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ripple.name = "WaterRipple_FX";
            ripple.transform.position = new Vector3(worldPos.x, worldPos.y, worldPos.z + 0.02f);
            ripple.transform.rotation = Quaternion.Euler(-68f, 0f, 0f);
            ripple.transform.localScale = Vector3.one * startScale;

            Collider col = ripple.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            MeshRenderer mr = ripple.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Shader rippleShader = Shader.Find("PixelGame/WaterRippleRing");
                if (rippleShader == null) rippleShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (rippleShader == null) rippleShader = Shader.Find("Unlit/Transparent");
                // Build'de shader bulunamazsa efekti atla: istisna, çağıran gemi hareketini yarıda kesiyordu
                if (rippleShader == null)
                {
                    Destroy(ripple);
                    return;
                }

                Material mat = new Material(rippleShader);
                Color foamColor = new Color(0.85f, 0.96f, 1f, 0.85f);
                mat.SetColor("_BaseColor", foamColor);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", foamColor);
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                ripple.transform.DOScale(Vector3.one * maxScale, duration).SetEase(Ease.OutQuad);
                mat.DOFade(0f, "_BaseColor", duration).SetEase(Ease.InQuad)
                    .OnComplete(() =>
                    {
                        if (Application.isPlaying)
                        {
                            Destroy(mat);
                            Destroy(ripple);
                        }
                        else
                        {
                            DestroyImmediate(mat);
                            DestroyImmediate(ripple);
                        }
                    });
            }
            else
            {
                if (Application.isPlaying) Destroy(ripple, duration);
                else DestroyImmediate(ripple);
            }
        }

        private static Material s_SmokePuffSharedMaterial;

        /// <summary>
        /// Referans videodaki gibi geminin arkasında beliren yumuşak beyaz puf duman bulutu (Cartoon Smoke Puff) oluşturur.
        /// </summary>
        public static void SpawnSmokePuff(Vector3 worldPos, float startScale = 0.10f, float maxScale = 0.32f, float duration = 0.38f)
        {
            GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Quad);
            puff.name = "SmokePuff_FX";
            puff.transform.position = worldPos;

            Camera cam = Camera.main;
            if (cam != null) puff.transform.rotation = cam.transform.rotation;
            else puff.transform.rotation = Quaternion.Euler(-68f, 0f, 0f);

            puff.transform.localScale = Vector3.one * startScale;

            Collider col = puff.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            MeshRenderer mr = puff.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                if (s_SmokePuffSharedMaterial == null)
                {
                    Shader smokeShader = Shader.Find("PixelGame/CartoonSmokePuff");
                    if (smokeShader == null) smokeShader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (smokeShader == null) smokeShader = Shader.Find("Unlit/Transparent");
                    // Build'de shader bulunamazsa efekti atla: istisna, çağıran gemi hareketini yarıda kesiyordu
                    if (smokeShader == null)
                    {
                        Destroy(puff);
                        return;
                    }
                    s_SmokePuffSharedMaterial = new Material(smokeShader);
                    s_SmokePuffSharedMaterial.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.90f));
                }

                Material instMat = new Material(s_SmokePuffSharedMaterial);
                mr.sharedMaterial = instMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                Vector3 drift = Vector3.up * 0.12f + new Vector3(UnityEngine.Random.Range(-0.04f, 0.04f), UnityEngine.Random.Range(0f, 0.04f), UnityEngine.Random.Range(-0.04f, 0.04f));
                puff.transform.DOMove(worldPos + drift, duration).SetEase(Ease.OutQuad);
                puff.transform.DOScale(Vector3.one * maxScale, duration).SetEase(Ease.OutQuad);
                instMat.DOFade(0f, "_BaseColor", duration).SetEase(Ease.InQuad)
                    .OnComplete(() =>
                    {
                        if (Application.isPlaying)
                        {
                            Destroy(instMat);
                            Destroy(puff);
                        }
                        else
                        {
                            DestroyImmediate(instMat);
                            DestroyImmediate(puff);
                        }
                    });
            }
            else
            {
                if (Application.isPlaying) Destroy(puff, duration);
                else DestroyImmediate(puff);
            }
        }

        /// <summary>
        /// Gemi, ShipController dışından (ör. ShipQueuePool'un arkadan gelme / öne kayma
        /// kuyruk animasyonları) DOTween ile taşınırken çağrılmalıdır. Su sallanması (bobbing)
        /// her karede pozisyonu eski "dinlenme" tabanına geri çektiği için, bu susturulmazsa
        /// dışarıdan yapılan pozisyon animasyonu her karede ezilip gemi hiç ilerlemiyormuş gibi
        /// görünür. Animasyon bitince `false` ile çağırıp geminin O ANKİ konumunu/rotasyonunu
        /// yeni dinlenme tabanı olarak kaydet — aksi halde bir sonraki bobbing karesi gemiyi
        /// eski (animasyon öncesi) konuma geri çeker.
        /// </summary>
        public void SetQueueAnimating(bool animating)
        {
            m_IsMoving = animating;
            if (animating)
            {
                ResetVisualOffset();
            }
            else
            {
                m_BaseLocalPosition = transform.localPosition;
                m_BaseLocalRotation = transform.localRotation;
            }
        }

        /// <summary>
        /// Slotlar doluysa veya geçersiz tıklamada gemi iki yana sallanır (Wobble).
        /// Görsel sarsıntı VisualRoot'a uygulanır, root collider stabil kalır.
        /// </summary>
        public void PlayWobble()
        {
            if (m_IsMoving || m_IsDeparting) return;
            Transform targetTr = m_VisualRoot != null ? m_VisualRoot : transform;
            targetTr.DOKill(true);
            targetTr.DOShakeRotation(0.35f, new Vector3(0f, 0f, 15f), 12, 90f, true)
                .OnComplete(() => targetTr.localRotation = Quaternion.identity);
        }

        #region 🖐️ Gerçek Zamanlı Drag & Smooth Follow (Aşama 3)

        public bool CanInitiateDrag()
        {
            if (!m_EnableDrag) return false;
            if (m_IsDocked || m_IsMoving || m_IsDeparting) return false;
            if (ShipDispatcher.Instance != null && (ShipDispatcher.Instance.IsAutoPlacing || ShipDispatcher.Instance.IsLevelFailed)) return false;
            if (IsLinked && !CanDispatchLinked())
            {
                PlayWobble();
                if (m_LinkedPartner != null) m_LinkedPartner.PlayWobble();
                if (m_Tether != null) m_Tether.Rattle();
                return false;
            }
            return true;
        }

        private Plane GetDragPlane(Camera cam)
        {
            Vector3 planePoint = transform.position;
            if (m_DragPlaneHeight != 0f)
            {
                planePoint += cam.transform.forward * m_DragPlaneHeight;
            }
            return new Plane(-cam.transform.forward, planePoint);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!CanInitiateDrag()) return;

            // EventSystem'in dahili piksel drag eşiğini Inspector'dan tanımlanan DragThreshold ile senkronize et
            if (EventSystem.current != null && m_DragThreshold > 0f)
            {
                EventSystem.current.pixelDragThreshold = Mathf.RoundToInt(m_DragThreshold);
            }

            m_IsPointerDown = true;
            m_DragThresholdPassed = false;
            m_WasDragged = false;
            m_IsDragging = false;
            m_IsPickedUp = false;
            m_PointerDownScreenPos = eventData.position;

            Camera cam = eventData.pressEventCamera ?? Camera.main;
            m_DragCamera = cam;
            if (cam == null) return;

            m_DragPlane = GetDragPlane(cam);
            Ray ray = cam.ScreenPointToRay(eventData.position);

            // 1. Raycast ile drag plane kesişimini bul
            if (m_DragPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                // 2. Hit point ile gemi gameplay position arasındaki Grab Offset'i hesapla
                m_GrabOffset = transform.position - hitPoint;
            }
            else
            {
                m_GrabOffset = Vector3.zero;
            }

            m_DragTargetWorldPosition = transform.position;
            m_SmoothedWorldPosition = transform.position;
            m_VisualWorldPosition = transform.position;
            m_PreviousWorldPosition = transform.position;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!CanInitiateDrag()) return;
            if (!m_DragThresholdPassed)
            {
                m_DragThresholdPassed = true;
                m_IsDragging = true;
                m_IsPickedUp = true;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!m_IsPointerDown) return;
            if (!CanInitiateDrag()) return;

            // Threshold kontrolü: 2-8 piksel arasındaki eşiği geçmeden drag başlamaz
            if (!m_DragThresholdPassed)
            {
                float distSq = (eventData.position - m_PointerDownScreenPos).sqrMagnitude;
                if (distSq >= m_DragThreshold * m_DragThreshold)
                {
                    m_DragThresholdPassed = true;
                    m_IsDragging = true;
                    m_IsPickedUp = true;
                }
            }

            if (m_IsDragging)
            {
                Camera cam = m_DragCamera != null ? m_DragCamera : (eventData.pressEventCamera ?? Camera.main);
                if (cam != null)
                {
                    Ray ray = cam.ScreenPointToRay(eventData.position);
                    if (m_DragPlane.Raycast(ray, out float enter))
                    {
                        Vector3 pointerWorld = ray.GetPoint(enter);
                        m_DragTargetWorldPosition = pointerWorld + m_GrabOffset;
                    }
                }
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (m_IsDragging || m_DragThresholdPassed)
            {
                HandleDropValidation();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            m_IsPointerDown = false;
            if (m_IsDragging || m_DragThresholdPassed)
            {
                HandleDropValidation();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Eğer drag eşiği aşıldıysa normal click tetiklenmez (tıklama ile drag birbirinden ayrılır)
            if (m_DragThresholdPassed || m_IsDragging || m_WasDragged || eventData.dragging)
            {
                m_WasDragged = false;
                m_DragThresholdPassed = false;
                return;
            }

            if (m_IsDocked || m_IsMoving || m_IsDeparting) return;
            if (ShipDispatcher.Instance != null && (ShipDispatcher.Instance.IsAutoPlacing || ShipDispatcher.Instance.IsLevelFailed)) return;

            // Bağlı gemi henüz serbest değilse uyar ve gönderme
            if (IsLinked && !CanDispatchLinked())
            {
                PlayWobble();
                if (m_LinkedPartner != null) m_LinkedPartner.PlayWobble();
                if (m_Tether != null) m_Tether.Rattle();
                return;
            }

            // Mevcut click-to-send mantığı %100 aynen çalışır
            if (ShipDispatcher.Instance != null)
            {
                ShipDispatcher.Instance.TrySendShipFromQueue(this);
            }
        }

        /// <summary>
        /// Sürükleme bırakıldığında (PointerUp / EndDrag) drop validation ve magnetic snap mantığını işletir.
        /// </summary>
        private void HandleDropValidation()
        {
            if (!m_IsDragging && !m_DragThresholdPassed) return;

            m_WasDragged = true;
            m_IsDragging = false;
            m_IsPickedUp = false;
            m_DragSmoothVelocity = Vector3.zero;

            // Slot detection için Ship_Boat gameplay root'unun anlık gameplay pozisyonu referans alınır.
            // VisualRoot'un pickup lift, bobbing, pitch, roll, banking offsetleri dahil edilmez.
            Vector3 detectionPos = transform.position;
            ShipSlot candidate = FindCandidateSlot(detectionPos, out float closestDist);

            bool isValidDrop = (candidate != null && candidate.IsEmpty && CanDockInSlot(candidate));

            ShipSlot partnerSlot = null;
            if (isValidDrop && IsLinked && m_LinkedPartner != null)
            {
                // Bağlı gemi için 2. boş slot ara (candidate dışındaki en yakın boş slot)
                float minPartnerDist = float.MaxValue;
                if (ShipDispatcher.Instance != null && ShipDispatcher.Instance.Slots != null)
                {
                    foreach (var s in ShipDispatcher.Instance.Slots)
                    {
                        if (s != null && s != candidate && s.IsEmpty)
                        {
                            float d = Vector3.Distance(candidate.transform.position, s.transform.position);
                            if (d < minPartnerDist)
                            {
                                minPartnerDist = d;
                                partnerSlot = s;
                            }
                        }
                    }
                }

                if (partnerSlot == null)
                {
                    // 2. boş slot yoksa bağlı gemiler yanaşamaz!
                    isValidDrop = false;
                    PlayWobble();
                    if (m_LinkedPartner != null) m_LinkedPartner.PlayWobble();
                    if (m_Tether != null) m_Tether.Rattle();
                }
            }

            if (isValidDrop)
            {
                m_CandidateSlot = null;

                ShipQueuePool queuePool = UnityEngine.Object.FindFirstObjectByType<ShipQueuePool>();
                if (IsLinked && m_LinkedPartner != null && partnerSlot != null)
                {
                    if (queuePool != null)
                    {
                        queuePool.OnLinkedShipsDispatched(this, m_LinkedPartner);
                    }

                    transform.SetParent(null, true);
                    m_LinkedPartner.transform.SetParent(null, true);

                    ShipDispatcher.Instance?.NotifyShipSent();
                    SailToSlot(candidate, m_SnapDuration);
                    m_LinkedPartner.SailToSlot(partnerSlot, 0.35f);
                }
                else
                {
                    // Kuyruk kontrolü: Ön sıradan slota gönderiliyorsa kuyruk yöneticisini bilgilendir
                    if (queuePool != null && queuePool.WaitingShips != null && queuePool.WaitingShips.Contains(this))
                    {
                        if (queuePool.IsFrontRow(this))
                        {
                            queuePool.OnFrontShipDispatched(this);
                        }
                    }

                    // Kuyruk hiyerarşisinden dünya uzayına çıkar
                    transform.SetParent(null, true);

                    // Mevcut Bezier SailToSlotRoutine ile hızlı, tatmin edici snap (m_SnapDuration: 0.16s)
                    ShipDispatcher.Instance?.NotifyShipSent();
                    SailToSlot(candidate, m_SnapDuration);
                }
            }
            else
            {
                // GEÇERSİZ DROP:
                // Gemi bırakıldığı pozisyonda kalır.
                // - Başlangıç pozisyonuna dönmez / teleport olmaz
                // - Mevcut bir slota gönderilmez
                // - ShipDispatcher çağrılmaz
                // - Click davranışı tetiklenmez
                m_CandidateSlot = null;
                m_BaseLocalPosition = transform.localPosition;
                m_BaseLocalRotation = transform.localRotation;
                m_SmoothedWorldPosition = transform.position;
                m_VisualWorldPosition = transform.position;
                m_PreviousWorldPosition = transform.position;
            }
        }

        private void UpdateDragFollow()
        {
            if (!Application.isPlaying) return;

            // 1. Smooth Follow & Gameplay Root Position (Aşama 4.6 Senkronize Hareket + Aşama 5 Magnetic Attraction)
            if (m_IsDragging)
            {
                // Slot detection için Ship_Boat gameplay root'unun anlık gameplay pozisyonunu referans al
                // VisualRoot'un pickup lift, bobbing, pitch, roll, banking offsetleri dahil edilmez
                Vector3 detectionPos = transform.position;
                m_CandidateSlot = FindCandidateSlot(detectionPos, out float candidateDist);

                Vector3 targetFollowPos = m_DragTargetWorldPosition;

                // Eğer geçerli aday slot menzildeyse hafif manyetik çekim uygula (ani teleport yok)
                if (m_CandidateSlot != null && candidateDist <= m_DetectionRadius)
                {
                    Vector3 slotDockPos = GetSlotDockPosition(m_CandidateSlot);
                    float pullFactor = 1f - Mathf.Clamp01(candidateDist / m_DetectionRadius);
                    float smoothPull = Mathf.SmoothStep(0f, 1f, pullFactor) * m_MagneticStrength;
                    targetFollowPos = Vector3.Lerp(m_DragTargetWorldPosition, slotDockPos, smoothPull);
                }

                m_SmoothedWorldPosition = Vector3.SmoothDamp(
                    m_SmoothedWorldPosition,
                    targetFollowPos,
                    ref m_DragSmoothVelocity,
                    m_DragSmoothTime,
                    m_DragMaxSpeed,
                    Time.deltaTime
                );

                // Gameplay root ve BoxCollider artık Smooth Follow pozisyonunu doğrudan takip eder
                transform.position = m_SmoothedWorldPosition;
                m_VisualWorldPosition = m_SmoothedWorldPosition;

#if UNITY_EDITOR
                // Hedef ile smooth takip edilen gerçek gameplay pozisyonu arasındaki çizgi
                Debug.DrawLine(transform.position, targetFollowPos, Color.green);
                if (m_CandidateSlot != null)
                {
                    Debug.DrawLine(transform.position, GetSlotDockPosition(m_CandidateSlot), Color.cyan);
                }
#endif
            }
            else
            {
                m_CandidateSlot = null;
                m_DragSmoothVelocity = Vector3.zero;
                m_SmoothedWorldPosition = transform.position;
                m_VisualWorldPosition = transform.position;
            }

            // 2. Velocity Hesaplama & Yumuşatma (Gerçek smooth gameplay hareketinden türetilir)
            Vector3 currentWorldPos = transform.position;
            if (m_IsDragging && Time.deltaTime > 0.0001f)
            {
                Vector3 rawVelocity = (currentWorldPos - m_PreviousWorldPosition) / Time.deltaTime;
                m_SmoothedVelocity = Vector3.SmoothDamp(m_SmoothedVelocity, rawVelocity, ref m_VelocitySmoothDeriv, 0.04f);
            }
            else
            {
                m_SmoothedVelocity = Vector3.SmoothDamp(m_SmoothedVelocity, Vector3.zero, ref m_VelocitySmoothDeriv, m_RotationSmoothTime);
            }
            m_PreviousWorldPosition = currentWorldPos;

            // 3. Pickup Lift & Scale Yumuşatma (0.08 s responsive & smooth geçiş)
            float targetLift = m_IsPickedUp ? m_PickupLift : 0f;
            m_CurrentPickupLift = Mathf.SmoothDamp(m_CurrentPickupLift, targetLift, ref m_PickupLiftVelocity, m_PickupDuration);

            float targetScale = m_IsPickedUp ? m_PickupScaleMultiplier : 1f;
            m_CurrentPickupScale = Mathf.SmoothDamp(m_CurrentPickupScale, targetScale, ref m_PickupScaleVelocity, m_PickupDuration);

            if (m_VisualRoot != null)
            {
                // Sadece VisualRoot'un ölçeği hafif büyütülür (1.04), Ship_Boat root ölçeği (0.26 / 0.351) asla değişmez
                m_VisualRoot.localScale = Vector3.one * m_CurrentPickupScale;
            }

            // 4. Dinamik Rotasyon Hesaplama (Gemi yerel hareket yönü tabanlı)
            Vector3 localVelocity = transform.InverseTransformDirection(m_SmoothedVelocity);
            float speed = m_SmoothedVelocity.magnitude;

            float targetRoll = 0f;
            float targetPitch = 0f;
            float targetYaw = 0f;

            if (m_IsDragging && speed > 0.04f)
            {
                // Banking: Sağa doğru çekildiğinde hafifçe yana yatış (maksimum 10°)
                targetRoll = Mathf.Clamp(-localVelocity.x * m_BankingStrength, -m_MaxBankingAngle, m_MaxBankingAngle);
                // Pitch: İleri/geri hareket hafif eğimi (maksimum 4°)
                targetPitch = Mathf.Clamp(localVelocity.z * m_PitchStrength, -m_MaxPitchAngle, m_MaxPitchAngle);
                // Yaw: Hareket yönüne doğru sınırlı pruva yönelimi (maksimum 25°, asla 180° dönmez)
                targetYaw = Mathf.Clamp(Mathf.Atan2(localVelocity.x, Mathf.Max(0.5f, localVelocity.z)) * Mathf.Rad2Deg, -m_MaxYawAngle, m_MaxYawAngle);
            }

            m_CurrentBankingRoll = Mathf.SmoothDamp(m_CurrentBankingRoll, targetRoll, ref m_RollSmoothVelocity, m_RotationSmoothTime);
            m_CurrentDragPitch = Mathf.SmoothDamp(m_CurrentDragPitch, targetPitch, ref m_PitchSmoothVelocity, m_RotationSmoothTime);
            m_CurrentDragYaw = Mathf.SmoothDampAngle(m_CurrentDragYaw, targetYaw, ref m_YawSmoothVelocity, m_RotationSmoothTime);
        }

        #endregion

        /// <summary>
        /// Aşama 2 Transform Decoupling:
        /// Root (Ship_Boat) -> Gameplay Root + BoxCollider (Interaction) + Badge
        /// [VisualRoot]     -> MeshFilter + MeshRenderer + [CargoDeck] (Görsel öğeler)
        /// </summary>
        public void EnsureDecoupledHierarchy()
        {
#if UNITY_EDITOR
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
#endif
            // 1. VisualRoot transformunu bul veya oluştur
            if (m_VisualRoot == null)
            {
                Transform found = transform.Find("[VisualRoot]");
                if (found == null) found = transform.Find("VisualRoot");
                if (found != null)
                {
                    m_VisualRoot = found;
                }
                else
                {
                    GameObject visGo = new GameObject("[VisualRoot]");
                    visGo.transform.SetParent(transform, false);
                    visGo.transform.localPosition = Vector3.zero;
                    visGo.transform.localRotation = Quaternion.identity;
                    visGo.transform.localScale = Vector3.one;
                    m_VisualRoot = visGo.transform;
                }
            }

            if (m_VisualRoot != null && !m_IsMoving)
            {
                if (m_VisualRoot.localScale != Vector3.one) m_VisualRoot.localScale = Vector3.one;
            }

            // 2. Eğer root GameObject'te MeshFilter veya MeshRenderer varsa VisualRoot'a taşı
            MeshFilter rootMf = GetComponent<MeshFilter>();
            MeshRenderer rootMr = GetComponent<MeshRenderer>();

            if (rootMf != null || rootMr != null)
            {
                MeshFilter visMf = m_VisualRoot.GetComponent<MeshFilter>();
                if (visMf == null) visMf = m_VisualRoot.gameObject.AddComponent<MeshFilter>();
                if (rootMf != null && rootMf.sharedMesh != null)
                {
                    visMf.sharedMesh = rootMf.sharedMesh;
                }

                MeshRenderer visMr = m_VisualRoot.GetComponent<MeshRenderer>();
                if (visMr == null) visMr = m_VisualRoot.gameObject.AddComponent<MeshRenderer>();
                if (rootMr != null)
                {
                    visMr.sharedMaterials = rootMr.sharedMaterials;
                    visMr.shadowCastingMode = rootMr.shadowCastingMode;
                    visMr.receiveShadows = rootMr.receiveShadows;
                }

                if (Application.isPlaying)
                {
                    if (rootMf != null) Destroy(rootMf);
                    if (rootMr != null) Destroy(rootMr);
                }
                else
                {
#if UNITY_EDITOR
                    if (rootMf != null) Undo.DestroyObjectImmediate(rootMf);
                    if (rootMr != null) Undo.DestroyObjectImmediate(rootMr);
#endif
                }
            }

            // 3. CargoDeck'i VisualRoot altına yerleştir
            EnsureCargoDeck();

            // 4. BoxCollider root üzerinde kalır (InteractionRoot) — VisualRoot'tan tamamen bağımsızdır
            BoxCollider col = GetComponent<BoxCollider>();
            if (col == null)
            {
                col = gameObject.AddComponent<BoxCollider>();
            }
            col.size = new Vector3(2.2f, 2.5f, 4.2f);
            col.center = new Vector3(0f, 1.0f, 0f);
            col.isTrigger = true;

            // 5. FBX içindeki gereksiz parçaları gizle
            foreach (Transform child in transform)
            {
                if (child == m_VisualRoot || child == m_FakeShadowObj?.transform || child.name.Contains("Shadow")) continue;
                string cName = child.name.ToLowerInvariant();
                if (cName.Contains("cargo-b") || cName.Contains("cargo-c") || cName.Contains("cargo_b") || cName.Contains("cargo_c"))
                {
                    child.gameObject.SetActive(false);
                }
            }
            if (m_VisualRoot != null)
            {
                foreach (Transform child in m_VisualRoot)
                {
                    string cName = child.name.ToLowerInvariant();
                    if (cName.Contains("cargo-b") || cName.Contains("cargo-c") || cName.Contains("cargo_b") || cName.Contains("cargo_c"))
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }

            m_Renderers = GetComponentsInChildren<MeshRenderer>(true);
        }

        private void EnsureVisualComponents()
        {
            EnsureDecoupledHierarchy();
        }

        private void EnsureCargoDeck()
        {
#if UNITY_EDITOR
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
#endif
            Transform parentTarget = m_VisualRoot != null ? m_VisualRoot : transform;

            if (m_CargoDeckRoot != null)
            {
                if (m_CargoDeckRoot.parent != parentTarget)
                {
                    m_CargoDeckRoot.SetParent(parentTarget, false);
                    m_CargoDeckRoot.localPosition = Vector3.zero;
                    m_CargoDeckRoot.localRotation = Quaternion.identity;
                    m_CargoDeckRoot.localScale = Vector3.one;
                }
                return;
            }

            Transform deck = parentTarget.Find("[CargoDeck]");
            if (deck == null && parentTarget != transform) deck = transform.Find("[CargoDeck]");

            if (deck != null)
            {
                m_CargoDeckRoot = deck;
                if (m_CargoDeckRoot.parent != parentTarget)
                {
                    m_CargoDeckRoot.SetParent(parentTarget, false);
                    m_CargoDeckRoot.localPosition = Vector3.zero;
                    m_CargoDeckRoot.localRotation = Quaternion.identity;
                    m_CargoDeckRoot.localScale = Vector3.one;
                }
            }
            else
            {
                GameObject deckObj = new GameObject("[CargoDeck]");
                deckObj.transform.SetParent(parentTarget, false);
                deckObj.transform.localPosition = Vector3.zero;
                deckObj.transform.localRotation = Quaternion.identity;
                deckObj.transform.localScale = Vector3.one;
                m_CargoDeckRoot = deckObj.transform;
            }
        }

        /// <summary>
        /// Güvertedeki tüm varilleri temizler.
        /// </summary>
        public void ClearCargoBarrels()
        {
            EnsureCargoDeck();
            if (m_CargoDeckRoot != null)
            {
                for (int i = m_CargoDeckRoot.childCount - 1; i >= 0; i--)
                {
                    Transform child = m_CargoDeckRoot.GetChild(i);
                    if (child != null)
                    {
                        if (Application.isPlaying) Destroy(child.gameObject);
                        else DestroyImmediate(child.gameObject);
                    }
                }
            }
            m_SpawnedBarrels.Clear();
        }

        /// <summary>
        /// Güvertedeki belirli bir indeksli varilin yerel (local) koordinatını hesaplar.
        /// 2 sütun x 4 satır taban düzenindedir; 8'den fazla ise 2. kat olarak üstlerine istiflenir.
        /// </summary>
        public Vector3 GetDeckLocalPositionForBarrel(int index)
        {
            int layer = (index >= 8 && m_Capacity > 8) ? 1 : 0;
            int indexInLayer = (layer == 1) ? (index - 8) : index;

            int col = indexInLayer % 2;
            int row = indexInLayer / 2;

            float posX = (col == 0) ? -0.36f : 0.36f;
            float posY = (layer == 0) ? 0.52f : 1.02f;
            float posZ = -0.95f - (row * 0.72f);

            return new Vector3(posX, posY, posZ);
        }

        private Material GetOrCreateBarrelMaterial()
        {
            if (m_BarrelSharedMaterial == null)
            {
                Shader shader = CartoonShader.Get();
                m_BarrelSharedMaterial = new Material(shader);
                m_BarrelSharedMaterial.name = "Ship_CargoBarrel_Mat";
            }

            CartoonShader.ApplyColor(m_BarrelSharedMaterial, m_ShipColor);
            return m_BarrelSharedMaterial;
        }

        private void UpdateAllBarrelsColor()
        {
            if (m_BarrelSharedMaterial != null)
            {
                GetOrCreateBarrelMaterial();
            }
            foreach (var b in m_SpawnedBarrels)
            {
                if (b != null)
                {
                    var mr = b.GetComponent<MeshRenderer>();
                    if (mr != null) mr.sharedMaterial = GetOrCreateBarrelMaterial();
                }
            }
        }

        /// <summary>
        /// Güverteye geminin renginde yeni bir 3D kargo varili ekler ve zıplatarak gösterir.
        /// </summary>
        private void SpawnCargoBarrel(int index)
        {
            EnsureCargoDeck();
            if (m_CargoDeckRoot == null) return;

            Vector3 localPos = GetDeckLocalPositionForBarrel(index);
            Vector3 targetScale = new Vector3(0.50f, 0.38f, 0.50f);

#if UNITY_EDITOR
            if (s_BarrelMesh == null)
            {
                GameObject barrelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FreeLowpolyScifiObjects/Prefabs/Barrels/barrel.prefab");
                if (barrelPrefab != null)
                {
                    MeshFilter mf = barrelPrefab.GetComponent<MeshFilter>();
                    if (mf != null) s_BarrelMesh = mf.sharedMesh;
                }
            }
#endif

            GameObject barrelObj;
            if (s_BarrelMesh != null)
            {
                barrelObj = new GameObject($"Barrel_{index}");
                MeshFilter mf = barrelObj.AddComponent<MeshFilter>();
                mf.sharedMesh = s_BarrelMesh;
                MeshRenderer mr = barrelObj.AddComponent<MeshRenderer>();
                mr.sharedMaterial = GetOrCreateBarrelMaterial();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;
                targetScale = Vector3.one * 0.40f;
            }
            else
            {
                barrelObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                barrelObj.name = $"Barrel_{index}";
                Collider col = barrelObj.GetComponent<Collider>();
                if (col != null) Destroy(col);
                MeshRenderer mr = barrelObj.GetComponent<MeshRenderer>();
                mr.sharedMaterial = GetOrCreateBarrelMaterial();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;
            }

            barrelObj.transform.SetParent(m_CargoDeckRoot, false);
            barrelObj.transform.localPosition = localPos;
            barrelObj.transform.localRotation = Quaternion.identity;
            barrelObj.transform.localScale = Vector3.zero;

            m_SpawnedBarrels.Add(barrelObj);

            // Tatlı yaylanarak beliren Juice Pop animasyonu
            barrelObj.transform.DOScale(targetScale, 0.22f).SetEase(Ease.OutBack);
        }

        private static Sprite s_CircleRingSprite;

        /// <summary>
        /// İçi %100 şeffaf, etrafı SAF BEYAZ yuvarlak dairesel çerçeveden (White Ring Frame) oluşan 2D Sprite üretir.
        /// </summary>
        public static Sprite GetCircleRingSprite()
        {
            if (s_CircleRingSprite != null) return s_CircleRingSprite;

            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float center = size * 0.5f;
            float radius = size * 0.46f;
            float borderWidth = size * 0.09f; // Dış beyaz çember halkası genişliği

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist <= radius + 1f && dist >= radius - borderWidth - 1f)
                    {
                        float outerFade = Mathf.Clamp01(radius + 1f - dist);
                        float innerFade = Mathf.Clamp01(dist - (radius - borderWidth - 1f));
                        float alpha = Mathf.Min(outerFade, innerFade);
                        // SAF BEYAZ yuvarlak çerçeve halkası
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                    else
                    {
                        // İÇİ %100 ŞEFFAF (Dahili dolgu YOK, arkaplansız!)
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            s_CircleRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return s_CircleRingSprite;
        }

        public static void InvalidateRingSprite()
        {
            s_CircleRingSprite = null;
        }

        /// <summary>
        /// Geminin kabin çatısı üzerine (2. fotodaki gibi) çerçevesiz, kalın siyah konturlu NET BEYAZ rakam metni oluşturur.
        /// </summary>
        private void CreateOrFindBadge()
        {
#if UNITY_EDITOR
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
#endif
            Transform existingCanvas = transform.Find("Ship_Capacity_Canvas");
            GameObject canvasObj;

            if (existingCanvas != null && existingCanvas.GetComponent<RectTransform>() == null)
            {
                if (Application.isPlaying) Destroy(existingCanvas.gameObject);
                else DestroyImmediate(existingCanvas.gameObject);
                existingCanvas = null;
            }

            if (existingCanvas != null)
            {
                canvasObj = existingCanvas.gameObject;
            }
            else
            {
                canvasObj = new GameObject("Ship_Capacity_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                canvasObj.transform.SetParent(transform, false);
            }

            // Standart Canvas transform değerleri (Gemi gövde çatısı tam merkezi: X=0, Y=2.25, Z=-0.42)
            canvasObj.transform.localPosition = new Vector3(0f, 2.25f, -0.42f);
            canvasObj.transform.localRotation = Quaternion.identity;
            canvasObj.transform.localScale = Vector3.one * 0.0295f;

            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasRect.sizeDelta = new Vector2(210f, 140f);
                canvasRect.pivot = new Vector2(0.5f, 0.5f);
                canvasRect.anchoredPosition = Vector2.zero;
            }

            Canvas canvas = canvasObj.GetComponent<Canvas>();
            if (canvas == null) canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 3000;

            Camera cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (cam != null) canvas.worldCamera = cam;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10;
            scaler.referencePixelsPerUnit = 100;

            m_BadgeCanvasObj = canvasObj;
            m_BadgeConfigured = false;

            // Dairesel çerçeve halkasını kaldır (2. fotodaki gibi çerçevesiz düz yazı)
            Transform bgTr = canvasObj.transform.Find("Badge_CircleRing");
            if (bgTr != null)
            {
                if (Application.isPlaying) Destroy(bgTr.gameObject);
                else DestroyImmediate(bgTr.gameObject, true);
            }
            m_BadgeImage = null;

            // Metin Nesnesi (Badge_Text)
            Transform textTr = canvasObj.transform.Find("Badge_Text");
            GameObject textObj;
            if (textTr != null)
            {
                textObj = textTr.gameObject;
            }
            else
            {
                textObj = new GameObject("Badge_Text", typeof(RectTransform), typeof(CanvasRenderer));
                textObj.transform.SetParent(canvasObj.transform, false);
            }

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.localPosition = Vector3.zero;
            textRect.localRotation = Quaternion.identity;
            textRect.localScale = Vector3.one;
            textRect.sizeDelta = new Vector2(210f, 140f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.pivot = new Vector2(0.5f, 0.5f);

            // Eski pikselli UI.Outline ve Shadow bileşenlerini temizle
            var oldOutlines = textObj.GetComponents<Outline>();
            for (int i = 0; i < oldOutlines.Length; i++)
            {
                if (Application.isPlaying) Destroy(oldOutlines[i]);
                else DestroyImmediate(oldOutlines[i]);
            }
            var oldShadows = textObj.GetComponents<Shadow>();
            for (int i = 0; i < oldShadows.Length; i++)
            {
                if (Application.isPlaying) Destroy(oldShadows[i]);
                else DestroyImmediate(oldShadows[i]);
            }

            // 3. Kullanıcı isteği: "2.görseldeki gibi görünsün textler daha kaliteli hale getir"
            // TextMeshPro SDF ile vektör kalitesinde, pürüzsüz yuvarlak siyah konturlu NET BEYAZ rakam:
            TMP_FontAsset tmpFont = null;
#if UNITY_EDITOR
            tmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
#endif
            // Build'de AssetDatabase yok: font Resources'taki tema ayarlarından gelir
            if (tmpFont == null) tmpFont = GameThemeSettings.MainFont;
            if (tmpFont == null) tmpFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LilitaOne-Regular SDF");
            if (tmpFont == null) tmpFont = Resources.Load<TMP_FontAsset>("Fonts/LilitaOne-Regular SDF");
            if (tmpFont == null) tmpFont = Resources.Load<TMP_FontAsset>("LilitaOne-Regular SDF");

            TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = textObj.AddComponent<TextMeshProUGUI>();

            if (tmpFont != null) tmp.font = tmpFont;
            ApplyBadgeTextStyle(tmp);

            m_BadgeText = tmp;

            // Eski standart Text varsa devre dışı bırak
            Text oldUiText = textObj.GetComponent<Text>();
            if (oldUiText != null)
            {
                oldUiText.enabled = false;
            }
            m_BadgeUIText = null;

            UpdateBadgePlacement();
            UpdateBadgeText();
        }

        private void UpdateBadgeText()
        {
            int remaining = RemainingCapacity;

            // Kullanıcı isteği: "textteki sayı dolduğunda text yok olsun"
            // Kapasite dolduğunda (kalan <= 0 veya IsFull) text HEMEN yok olur!
            if (remaining <= 0 || IsFull || m_IsDeparting)
            {
                if (m_BadgeCanvasObj != null && m_BadgeCanvasObj.activeSelf)
                    m_BadgeCanvasObj.SetActive(false);
                if (m_BadgeText != null && m_BadgeText.gameObject.activeSelf)
                    m_BadgeText.gameObject.SetActive(false);
                if (m_BadgeUIText != null && m_BadgeUIText.gameObject.activeSelf)
                    m_BadgeUIText.gameObject.SetActive(false);
                return;
            }

            // Gizli gemi: kapasite saklanır, yerine "?" yazılır
            string countStr = m_IsMysteryHidden ? "?" : remaining.ToString();

            if (m_BadgeCanvasObj != null && !m_BadgeCanvasObj.activeSelf)
                m_BadgeCanvasObj.SetActive(true);

            if (m_BadgeText != null)
            {
                if (!m_BadgeText.gameObject.activeSelf) m_BadgeText.gameObject.SetActive(true);
                m_BadgeText.text = countStr;
            }
            if (m_BadgeUIText != null && m_BadgeUIText.enabled)
            {
                if (!m_BadgeUIText.gameObject.activeSelf) m_BadgeUIText.gameObject.SetActive(true);
                m_BadgeUIText.text = countStr;
            }
        }
    }
}

