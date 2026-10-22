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
        IPointerUpHandler
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

        [Header("📦 2D UI Konteyner Modu")]
        [SerializeField] private bool m_IsUiContainer = false;
        [SerializeField] private Image m_ContainerImage;
        public bool IsUiContainer { get => m_IsUiContainer || GetComponent<RectTransform>() != null; set => m_IsUiContainer = value; }
        public Image ContainerImage { get => m_ContainerImage; set => m_ContainerImage = value; }

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

        [Tooltip("Gemi, kıçı slot görselinin arka kenarına oturacak şekilde yanaşır (gemi boyundan bağımsız).")]
        [SerializeField] private bool m_AlignSternToSlotBack = true;
        [Tooltip("Kıçın slot arka kenarından içeri payı (slot boyunun oranı). Kesikli çerçeve kenardan ~%3 içeride.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float m_SternInsetRatio = 0.05f;
        [Tooltip("Yanaşınca slot içinde yana kayma (slot yerel X; negatif = sola).")]
        [SerializeField] private float m_DockSideOffset = 0f;

        public const float DefaultDockForwardOffset = 0.28f;
        public const float DefaultDockHeightOffset = 0.08f;

        public float DockForwardOffset { get => m_DockForwardOffset; set => m_DockForwardOffset = value; }
        public float DockHeightOffset { get => m_DockHeightOffset; set => m_DockHeightOffset = value; }

        [Header("🌊 Su Salınımı (Idle Water Bobbing)")]
        [SerializeField] private bool m_EnableWaterBobbing = false;
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
        [SerializeField] private bool m_EnableDrag = false;
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
        private bool m_SailSoundActive = false;
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
        public bool EnableDrag { get => false; set => m_EnableDrag = false; }
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

        // Kapasite yazısının punto büyüklüğü
        private const float BadgeFontSize = 42f;

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

            tmp.outlineWidth = 0.12f;
            tmp.outlineColor = outlineCol;

            Material mat = tmp.fontMaterial;
            if (mat == null) return;

            mat.EnableKeyword("OUTLINE_ON");
            mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.12f);
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

        [Header("🔢 Kapasite Yazısı")]
        [Tooltip("Açıkken sayı kabin çatısına boyanmış gibi çatı yüzeyine yatar ve gemiyle birlikte döner. Kapalıyken kameraya dik tabela gibi durur.")]
        [SerializeField] private bool m_BadgePaintedOnRoof = true;
        [Tooltip("Çatıya yazılı sayının gemi yerel uzayında ince ayarı (Y: çatıdan yükseklik, Z: ileri/geri).")]
        [SerializeField] private Vector3 m_BadgeRoofNudge = Vector3.zero;
        [Tooltip("Çatıya yazılı sayının boyut çarpanı.")]
        [Range(0.5f, 1.5f)]
        [SerializeField] private float m_BadgeRoofScale = 1f;
        [Tooltip("Kamera çatıya yandan baktığı için çatıdaki yazı ekranda basık görünür (pruva yönü ~%47). " +
                 "Yazı çatı üzerinde pruva yönünde bu kadar uzatılır: 1 = gerçek boya (basık), ~2.1 = tamamen düz görünür.")]
        [Range(1f, 2.5f)]
        [SerializeField] private float m_BadgeRoofStretch = 1.0f;
        // Konteyner tavanının üst yüzeyi (Y=0.885f tavan merkezi, Z=0f tam ortalı):
        private static readonly Vector3 RoofSurfaceCenter = new Vector3(0f, 0.885f, 0f);

        [Header("🌑 Gemi Silüet Gölgesi")]
        [Tooltip("Geminin gövde şeklini takip eden, sağa düşen net gölge. Açıkken eski yumuşak (quad) gölge ve gövdenin gerçek ışık gölgesi kapanır.")]
        [SerializeField] private bool m_EnableSilhouetteShadow = true;
        [Tooltip("Işık yönü: gövdenin her birim yüksekliği gölgeyi ne kadar kaydırır (X: sağ, Y: aşağı/kıç yönü negatif). Büyüdükçe gölge uzar.")]
        [SerializeField] private Vector2 m_SilhouetteShadowDirection = new Vector2(0.20f, -0.50f);
        [Tooltip("Gölge rengi ve opaklığı (alfa).")]
        [SerializeField] private Color m_SilhouetteShadowColor = new Color(0.02f, 0.08f, 0.22f, 0.38f);
        [Tooltip("Gölge kenarının yumuşaklığı (0 = keskin). Kenar bu genişlikte dışa doğru saydamlaşır.")]
        [Range(0f, 0.8f)]
        [SerializeField] private float m_SilhouetteShadowSoftness = 0.35f;
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

        public enum ChimneyCorner
        {
            RearRight,
            RearLeft,
            FrontRight,
            FrontLeft,
            Custom,
            FrontCenter // Custom'dan sonra: serileştirilmiş enum indeksleri kaymasın
        }

        public const ChimneyCorner DefaultChimneyCorner = ChimneyCorner.FrontCenter;

        [Header("💨 Baca & Duman Efekti (Chimney & Smoke)")]
        [Tooltip("Geminin tavanına sevimli bir baca ve hafifçe tüten hypercasual duman efekti ekler.")]
        [SerializeField] private bool m_EnableChimneySmoke = false;
        [Tooltip("Bacanın kabin çatısı üzerindeki konumu (Varsayılan: FrontCenter / Ön Orta, pencere boşluğunun içi).")]
        [SerializeField] private ChimneyCorner m_ChimneyCorner = DefaultChimneyCorner;
        [Tooltip("Bacanın kabin çatısı üzerindeki yerel konumu.")]
        [SerializeField] private Vector3 m_ChimneyLocalPos = new Vector3(0f, 0.732f, -2.193f);
        [SerializeField] private Transform m_ChimneyTransform;
        [SerializeField] private ParticleSystem m_ChimneySmokePS;
        [SerializeField] private Material m_ChimneyBodyMaterial;
        [SerializeField] private Material m_ChimneyRimMaterial;
        [SerializeField] private Material m_ChimneySmokeMaterial;

        [Header("🌊 Gemi Dibi Dalga Halkaları")]
        [Tooltip("Bekleyen ve yanaşmış geminin dibinde suya yayılan ince, eş merkezli halkalar (referans görseldeki gibi).")]
        [SerializeField] private bool m_EnableHullRings = true;
        [SerializeField, Range(1, 4)] private int m_HullRingCount = 2;
        [Tooltip("Halkanın gövde izdüşümüne göre başlangıç büyüklüğü (1 = gövdenin tam kenarı).")]
        [SerializeField, Range(0.8f, 1.6f)] private float m_HullRingStartScale = 0.95f;
        [Tooltip("Halkanın bir döngüde ne kadar genişlediği (başlangıç büyüklüğüne oran).")]
        [SerializeField, Range(0f, 1f)] private float m_HullRingGrow = 0.25f;
        [Tooltip("Bir halkanın genişleyip kaybolma süresi (sn).")]
        [SerializeField, Range(0.5f, 6f)] private float m_HullRingCycle = 2.6f;
        [SerializeField] private Color m_HullRingColor = new Color(0.85f, 0.96f, 1f, 0.32f);

        private Transform[] m_HullRings;
        private Vector2 m_HullRingHalfSize;
        private float m_HullRingPhase;
        private static Material s_HullRingMaterial;
        private static MaterialPropertyBlock s_HullRingBlock;
        private static readonly int HullRingColorProp = Shader.PropertyToID("_BaseColor");

        // Dahili referanslar
        private MeshRenderer[] m_Renderers;
        private MaterialPropertyBlock m_PropBlock;
        private float m_BobRandomOffset;
        private Vector3 m_BaseLocalPosition;
        private Quaternion m_BaseLocalRotation;
        private Vector3 m_BaseScale = Vector3.one * DefaultShipScale;
        private Vector3 m_UiDockedScale = Vector3.zero;
        private static Material s_AlwaysOnTopMaterial;

        // Slot dolu / geçersiz tıklama ret animasyonu değişkenleri
        private float m_DenialWobbleAngle = 0f;
        private float m_DenialWobbleOffsetX = 0f;
        private Tween m_DenialWobbleTween;
        private Tween m_DenialWobblePosTween;
        private Tween m_TextWarningColorTween;
        private Tween m_TextWarningScaleTween;

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

        public void SetCapacity(int capacity)
        {
            m_Capacity = Mathf.Max(1, capacity);
            m_WorkersExitedCount = 0;
            m_CurrentCargo = 0;
            UpdateBadgeText();
        }

        // ---------------- 👷 Dışarıdaki İşçiler (Workers On Mission) ----------------
        private int m_ActiveWorkersOut = 0;
        private int m_WorkersExitedCount = 0;
        public int ActiveWorkersOut => m_ActiveWorkersOut;

        /// <summary>
        /// Kullanıcı isteği: sayı işçi gemiden çıkınca değil, küpü alıp gemiye geri binince azalır.
        /// (Alan adı tarihsel: artık "geri binen işçi sayısı"nı tutar.)
        /// </summary>
        public int DisplayRemainingCapacity => Mathf.Max(0, m_Capacity - m_WorkersExitedCount);

        /// <summary>
        /// Gemiden bir işçi sahile zıplayıp göreve çıktığında çağrılır. Sayı burada düşmez (OnWorkerBoarded'da düşer).
        /// </summary>
        public void OnWorkerExited()
        {
            m_ActiveWorkersOut++;
        }

        /// <summary>
        /// Görevini tamamlayan işçi güverteye inip gemiye bindiğinde (ve yok olduğunda) çağrılır:
        /// Tüm işçiler geri binmişse ve gemi dolduysa gemi kalkışa geçer.
        /// </summary>
        /// <param name="boardedCargo">İşçinin getirdiği küp sayısı (yığın taşıyan denizcide sayı bir kerede bu kadar düşer).</param>
        public void OnWorkerBoarded(int boardedCargo = 1)
        {
            m_ActiveWorkersOut = Mathf.Max(0, m_ActiveWorkersOut - 1);
            m_WorkersExitedCount = Mathf.Min(m_Capacity, m_WorkersExitedCount + Mathf.Max(1, boardedCargo));
            UpdateBadgeText();
            PlayBadgePunchJuice();

            // Kullanıcı isteği: "tüm işçiler geri binince de ayrılır"
            if (IsFull && m_ActiveWorkersOut <= 0 && !m_IsDeparting)
            {
                OnCargoFilled?.Invoke(this);
                if (ShipDispatcher.Instance != null) ShipDispatcher.Instance.OnShipFilled(this);
                DepartAndFreeSlot();
            }
        }

        /// <summary>
        /// İşçi yolda bir sebeple iptal edilirse (hedef küp yok olduysa vb.) sayacı düzeltir.
        /// </summary>
        public void OnWorkerMissionAborted()
        {
            m_ActiveWorkersOut = Mathf.Max(0, m_ActiveWorkersOut - 1);
        }

        public void PlayBadgePunchJuice()
        {
            if (m_BadgeCanvasObj != null && m_BadgeCanvasObj.activeSelf)
            {
                m_BadgeCanvasObj.transform.DOKill(true);
                m_BadgeCanvasObj.transform.DOPunchScale(new Vector3(0.18f, 0.18f, 0.18f), 0.12f, 2, 0.4f)
                    .OnComplete(UpdateBadgePlacement);
            }
        }

        public bool IsDocked
        {
            get => m_IsDocked;
            set
            {
                m_IsDocked = value;
                EnsureSilhouetteShadow();
            }
        }
        public bool IsFull => m_CurrentCargo >= m_Capacity;
        public bool IsDeparting => m_IsDeparting;
        public bool IsMoving => m_IsMoving;
        private bool m_IsQueueAnimating = false;
        public bool IsQueueAnimating => m_IsQueueAnimating;

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
            if (IsUiContainer)
            {
                UpdateLinkedUiVisuals();
                if (partner != null && partner.IsUiContainer)
                {
                    partner.UpdateLinkedUiVisuals();
                }
            }
        }

        public void SetTether(LinkedShipTether tether)
        {
            m_Tether = tether;
        }

        /// <summary>
        /// Geminin bağlı bir partneri var mı ve bu partner hâlâ slota yanaşık ve aktif mi?
        /// </summary>
        public bool HasActiveLinkedPartner =>
            m_LinkId > 0 &&
            m_LinkedPartner != null &&
            m_LinkedPartner.gameObject.activeInHierarchy &&
            !m_LinkedPartner.IsDeparting;

        /// <summary>
        /// Bağlı partnerin de kalkışa hazır (tamamen dolu veya toplanacak küpü kalmamış) olup olmadığını doğrular.
        /// </summary>
        public bool IsLinkedPartnerReadyToDepart
        {
            get
            {
                if (!HasActiveLinkedPartner) return true;
                if (m_LinkedPartner.HasPendingCargo || m_LinkedPartner.ActiveWorkersOut > 0) return false;
                if (m_LinkedPartner.IsFull) return true;

                if (ShipDispatcher.Instance != null &&
                    ShipDispatcher.Instance.GetRemainingCountForColor(m_LinkedPartner.ShipColor) == 0)
                {
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Geminin şu anda kalkış yapmaya uygun olup olmadığını doğrular.
        /// Bağlı gemi değilse dolduğunda hemen kalkabilir; bağlı gemi ise partneri de hazır olmalıdır.
        /// </summary>
        public bool CanDepartNow
        {
            get
            {
                if (!m_IsDocked || m_IsDeparting) return false;
                if (HasPendingCargo || m_ActiveWorkersOut > 0) return false;

                bool isSelfReady = IsFull || (ShipDispatcher.Instance != null && ShipDispatcher.Instance.GetRemainingCountForColor(ShipColor) == 0);
                if (!isSelfReady) return false;

                if (HasActiveLinkedPartner)
                {
                    return IsLinkedPartnerReadyToDepart;
                }

                return true;
            }
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

            if (Application.isPlaying && IsUiContainer)
            {
                // Örtü kalkarken küçük "pop" (UI: su halkası yok)
                transform.DOPunchScale(Vector3.one * 0.12f, 0.35f, 4, 0.5f)
                    .OnComplete(() => transform.localScale = GetLocalScaleForBaseWorldScale());
            }
            else if (Application.isPlaying)
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
            if (IsUiContainer)
            {
                // UI konteyner: eski gemilerdeki gibi tüm gövde lacivert '?' desenli dokuyla kaplanır
                if (m_ContainerImage == null) m_ContainerImage = GetComponent<Image>() ?? GetComponentInChildren<Image>(true);
                Sprite cover = GetMysteryContainerSprite();
                if (m_ContainerImage != null && cover != null)
                {
                    m_ContainerImage.sprite = cover;
                    m_ContainerImage.color = Color.white;
                }
                return;
            }

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
                if (mr.name.IndexOf("HullRing", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (m_FakeShadowObj != null && (mr.gameObject == m_FakeShadowObj || mr.transform.IsChildOf(m_FakeShadowObj.transform))) continue;
                if (m_CargoDeckRoot != null && mr.transform.IsChildOf(m_CargoDeckRoot)) continue;
                if (m_ChimneyTransform != null && (mr.gameObject == m_ChimneyTransform.gameObject || mr.transform.IsChildOf(m_ChimneyTransform))) continue;
                if (mr.name.IndexOf("Chimney", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (mr.name.IndexOf("Smoke", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

                mr.sharedMaterial = coverMat;
            }

            UpdateChimneyRimColor();
        }

        private static Sprite s_MysteryContainerSprite;

        /// <summary>
        /// Taban konteynerin silüetini gizli gemi desenine (lacivert zemin + beyaz '?') boyar. Gölgelendirme
        /// taban parlaklığından alınır; alttaki yumuşak zemin gölgesi olduğu gibi kalır.
        /// </summary>
        public static Sprite GetMysteryContainerSprite()
        {
            if (s_MysteryContainerSprite != null) return s_MysteryContainerSprite;
            GetContainerSprite(Color.white); // taban pikselleri yüklensin
            if (s_ContainerBasePixels == null || s_ContainerBaseTex == null) return null;

            Texture2D pattern = GetOrCreateMysteryPatternTexture();
            Color32[] pat = pattern.GetPixels32();
            int pw = pattern.width, ph = pattern.height;
            int w = s_ContainerBaseTex.width, h = s_ContainerBaseTex.height;
            const float tilePx = 230f; // sprite pikselinde bir desen karosu (~3 '?' yan yana)

            var src = s_ContainerBasePixels;
            var dst = new Color32[src.Length];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    Color32 c = src[i];
                    // Zemin gölgesi (kahverengimsi koyu, yarı saydam) ve boş pikseller korunur
                    if (c.a == 0 || (c.r < 70 && c.g < 50 && c.b < 30)) { dst[i] = c; continue; }

                    Color.RGBToHSV(c, out _, out _, out float v);
                    float shade = Mathf.Clamp(v / ContainerBaseValue, 0.45f, 1.3f);
                    int px = (int)(x / tilePx * pw) % pw;
                    int py = (int)(y / tilePx * ph) % ph;
                    Color pc = pat[py * pw + px];
                    Color res = new Color(pc.r * shade, pc.g * shade, pc.b * shade, c.a / 255f);
                    dst[i] = res;
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "Container_Mystery",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            tex.SetPixels32(dst);
            tex.Apply(false, true);
            s_MysteryContainerSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            s_MysteryContainerSprite.name = tex.name;
            s_MysteryContainerSprite.hideFlags = HideFlags.DontSave;
            return s_MysteryContainerSprite;
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

            if (m_IsDocked || m_IsMoving || m_IsDeparting || m_IsQueueAnimating) return false;
            if (m_LinkedPartner.IsDocked || m_LinkedPartner.IsMoving || m_LinkedPartner.IsDeparting || m_LinkedPartner.IsQueueAnimating) return false;

            ShipQueuePool pool = ShipQueuePool.Instance != null ? ShipQueuePool.Instance : UnityEngine.Object.FindFirstObjectByType<ShipQueuePool>();
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
            if (IsUiContainer)
            {
                if (m_FakeShadowObj != null) m_FakeShadowObj.SetActive(false);
                return;
            }

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
            // Yanaşırken, slottayken ve kalkarken de gölge kalsın (gemi slota yola çıktığı anda "yanaşmış" sayılıyordu,
            // gölge tüm bu süre boyunca kayboluyordu)
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

        /// <summary>
        /// UI konteynerde sayının arkasında rozet (Badge_Pill vb.) olmaz: sadece yazı görünür. Sahne/prefab
        /// rozetli kurulsa bile yazının üstündeki tüm Image'lar (konteyner görseli hariç) kapatılır.
        /// </summary>
        private void HideUiBadgeBackground()
        {
            if (m_BadgeText == null) m_BadgeText = GetComponentInChildren<TextMeshProUGUI>(true);
            if (m_ContainerImage == null) m_ContainerImage = GetComponent<Image>();
            foreach (var img in GetComponentsInChildren<Image>(true))
            {
                if (img == null || img == m_ContainerImage) continue;
                bool isBadgeBg = img.name.IndexOf("Badge", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 (m_BadgeText != null && m_BadgeText.transform.IsChildOf(img.transform));
                if (isBadgeBg && img.enabled) img.enabled = false;
            }
        }

        private Image m_UiOutlineImage;
        private static Sprite s_ContainerOutlineSprite;
        // Kontur sprite'ı gövde sprite'ından her yandan 64/512 geniş (gölge taşmasın): anchor'lar o kadar dışarıda
        private const float UiOutlinePad = 64f / 512f;

        /// <summary>
        /// Konteynerin beyaz konturu ve yumuşak gölgesi (Container_Outline). Kendi Canvas'ında, kök Canvas'tan bir sıra
        /// geride çizilir: tüm konturlar tüm konteyner gövdelerinin ve halat köprüsünün arkasında kalır. Halatın
        /// konteynere girdiği yerde köprü konturun üstünden geçtiği için iki konteyner ve halat tek parça görünür.
        /// </summary>
        private void EnsureUiOutline()
        {
            if (!IsUiContainer) return;
            if (s_ContainerOutlineSprite == null)
            {
#if UNITY_EDITOR
                s_ContainerOutlineSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Containers/Container_Outline.png");
#endif
                if (s_ContainerOutlineSprite == null) s_ContainerOutlineSprite = Resources.Load<Sprite>("Containers/Container_Outline");
                if (s_ContainerOutlineSprite == null) return;
            }

            if (m_UiOutlineImage == null)
            {
                Transform existing = transform.Find("Outline");
                GameObject go = existing != null
                    ? existing.gameObject
                    : new GameObject("Outline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                if (existing == null)
                {
                    go.transform.SetParent(transform, false);
                    go.transform.SetAsFirstSibling();
                }
                var ort = (RectTransform)go.transform;
                ort.anchorMin = Vector2.one * -UiOutlinePad;
                ort.anchorMax = Vector2.one * (1f + UiOutlinePad);
                ort.offsetMin = Vector2.zero;
                ort.offsetMax = Vector2.zero;
                ort.pivot = new Vector2(0.5f, 0.5f);
                ort.localRotation = Quaternion.identity;
                ort.localScale = Vector3.one;
                m_UiOutlineImage = go.GetComponent<Image>();
                m_UiOutlineImage.raycastTarget = false;
            }
            m_UiOutlineImage.sprite = s_ContainerOutlineSprite;
            m_UiOutlineImage.color = Color.white;

            Canvas parentCanvas = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;
            Canvas root = parentCanvas != null ? parentCanvas.rootCanvas : null;
            Canvas oc = m_UiOutlineImage.GetComponent<Canvas>();
            if (oc == null) oc = m_UiOutlineImage.gameObject.AddComponent<Canvas>();
            oc.overrideSorting = true;
            if (root != null)
            {
                oc.sortingLayerID = root.sortingLayerID;
                oc.sortingOrder = root.sortingOrder - 1;
            }
        }

        /// <summary>
        /// Yeni 3D stil konteynerin üst kapak tepsisine metnin tam oturmasını ve boyutların düzgün olmasını sağlar.
        /// </summary>
        public void EnsureUiContainerLayout()
        {
            if (!IsUiContainer) return;
            EnsureUiOutline();
            RectTransform rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                if (Mathf.Abs(rt.sizeDelta.x - 160f) > 1f || Mathf.Abs(rt.sizeDelta.y - 163f) > 1f)
                {
                    rt.sizeDelta = new Vector2(160f, 163f);
                }
            }

            // Soket çukuru (bağlı konteynerler için kapağın ortasındaki 3D çukur)
            Transform socketTr = transform.Find("Socket_Recess");
            if (socketTr == null)
            {
                GameObject sObj = new GameObject("Socket_Recess", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                sObj.transform.SetParent(transform, false);
                sObj.transform.SetSiblingIndex(1);
                socketTr = sObj.transform;
            }
            if (socketTr != null)
            {
                RectTransform srt = socketTr.GetComponent<RectTransform>();
                if (srt != null)
                {
                    srt.anchorMin = new Vector2(0.5f, 0.5f);
                    srt.anchorMax = new Vector2(0.5f, 0.5f);
                    srt.pivot = new Vector2(0.5f, 0.5f);
                    srt.anchoredPosition = new Vector2(0f, 14f);
                    srt.sizeDelta = new Vector2(64f, 64f);
                }
                UnityEngine.UI.Image sImg = socketTr.GetComponent<UnityEngine.UI.Image>();
                if (sImg != null)
                {
                    if (sImg.sprite == null)
                    {
#if UNITY_EDITOR
                        sImg.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Containers/Container_Socket_Recess.png");
#endif
                        if (sImg.sprite == null) sImg.sprite = Resources.Load<Sprite>("Containers/Container_Socket_Recess");
                    }
                    sImg.color = Color.white;
                    sImg.raycastTarget = false;
                }
            }

            Transform pillTr = transform.Find("Badge_Pill");
            if (pillTr != null)
            {
                EnsureBadgeCanvasSorting();

                RectTransform prt = pillTr.GetComponent<RectTransform>();
                if (prt != null)
                {
                    prt.anchorMin = new Vector2(0.5f, 0.5f);
                    prt.anchorMax = new Vector2(0.5f, 0.5f);
                    prt.pivot = new Vector2(0.5f, 0.5f);
                    prt.anchoredPosition = new Vector2(0f, 14f);
                    prt.sizeDelta = new Vector2(140f, 110f);
                }
                UnityEngine.UI.Image pImg = pillTr.GetComponent<UnityEngine.UI.Image>();
                if (pImg != null) pImg.color = Color.clear;
            }
            if (m_BadgeText == null) m_BadgeText = GetComponentInChildren<TextMeshProUGUI>(true);
            if (m_BadgeText != null)
            {
                m_BadgeText.alignment = TextAlignmentOptions.Center;
                m_BadgeText.enableAutoSizing = true;
                m_BadgeText.fontSizeMin = 36f;
                m_BadgeText.fontSizeMax = 64f;
                m_BadgeText.fontSize = 58f;
                m_BadgeText.fontStyle = FontStyles.Bold;
                m_BadgeText.color = Color.white;
                m_BadgeText.outlineWidth = 0.28f;
                m_BadgeText.outlineColor = Color.black;
            }

            UpdateLinkedUiVisuals();
        }

        /// <summary>
        /// UI Konteynerlerde rozet sayısının (Badge_Pill) halat köprülerinin (+5) de üstünde çizilmesini garantiye alır.
        /// </summary>
        public void EnsureBadgeCanvasSorting()
        {
            if (!IsUiContainer) return;
            Transform pillTr = transform.Find("Badge_Pill");
            if (pillTr == null) return;

            Canvas rootCanvas = GetComponentInParent<Canvas>();
            Canvas pillCanvas = pillTr.GetComponent<Canvas>();
            if (pillCanvas == null)
            {
                pillCanvas = pillTr.gameObject.AddComponent<Canvas>();
                if (pillTr.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                    pillTr.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            pillCanvas.overrideSorting = true;
            if (rootCanvas != null)
            {
                pillCanvas.renderMode = rootCanvas.renderMode;
                pillCanvas.worldCamera = rootCanvas.worldCamera;
                pillCanvas.planeDistance = rootCanvas.planeDistance;
                pillCanvas.sortingLayerID = rootCanvas.sortingLayerID;
                pillCanvas.sortingOrder = rootCanvas.sortingOrder + 20; // Halat köprüsünün (+5) daima üstünde
                pillCanvas.additionalShaderChannels = rootCanvas.additionalShaderChannels;
            }
            else
            {
                pillCanvas.sortingOrder = 75;
            }

            Vector3 lp = pillTr.localPosition;
            if (Mathf.Abs(lp.z) > 0.001f)
            {
                pillTr.localPosition = new Vector3(lp.x, lp.y, 0f);
            }
        }

        /// <summary>
        /// UI Konteynerlerde rozet sayısının ve soket çukurunun görsel durumunu günceller.
        /// Sayılar hiçbir zaman kaymaz, daima kutunun merkezinde (0, 14) durur ve halat köprüsünün de üstünde çizilir.
        /// Bağlılık aktifse kapağın ortasındaki 3D soket çukurunu (Socket_Recess) açar; bağlı değilse kapatır.
        /// </summary>
        public void UpdateLinkedUiVisuals()
        {
            if (!IsUiContainer) return;

            Transform socketTr = transform.Find("Socket_Recess");
            RectTransform socketRt = socketTr != null ? socketTr.GetComponent<RectTransform>() : null;
            Transform pillTr = transform.Find("Badge_Pill");
            RectTransform prt = pillTr != null ? pillTr.GetComponent<RectTransform>() : null;

            bool isLinkedActive = m_LinkId > 0 && m_LinkedPartner != null &&
                                  m_LinkedPartner.gameObject.activeInHierarchy &&
                                  !m_LinkedPartner.IsDeparting && !m_IsDeparting;

            if (socketTr != null)
            {
                socketTr.gameObject.SetActive(isLinkedActive);
                if (socketRt != null)
                {
                    socketRt.anchoredPosition = new Vector2(0f, 14f);
                    socketRt.sizeDelta = new Vector2(64f, 64f);
                }
            }

            if (prt != null)
            {
                // Kullanıcı isteği: Sayılar HİÇ kaymasın, her zaman merkezde dursun ve halatların üstünde görünsün
                prt.anchoredPosition = new Vector2(0f, 14f);
                prt.sizeDelta = new Vector2(140f, 110f);
            }

            // Sayının halat köprüsünün de üstünde çizilmesini garantiye al
            EnsureBadgeCanvasSorting();
        }

        private void Awake()
        {
            m_PropBlock = new MaterialPropertyBlock();
            m_BobRandomOffset = UnityEngine.Random.Range(0f, 100f);
            if (transform.lossyScale != Vector3.zero) m_BaseScale = transform.lossyScale;

            if (IsUiContainer)
            {
                EnsureUiContainerLayout();
            }

            if (Application.isPlaying)
            {
                EnsureDecoupledHierarchy();
                CreateOrFindBadge();
                EnsureFakeShadow();
                EnsureSilhouetteShadow();
                EnsureChimneyAndSmoke();
            }
        }

        private void OnEnable()
        {
            if (!s_ActiveShips.Contains(this))
            {
                s_ActiveShips.Add(this);
            }

            if (IsUiContainer)
            {
                HideUiBadgeBackground();
                EnsureUiContainerLayout();
            }

            if (Application.isPlaying)
            {
                EnsureDecoupledHierarchy();
                CreateOrFindBadge();
                EnsureFakeShadow();
                EnsureSilhouetteShadow();
                EnsureChimneyAndSmoke();
                UpdateBadgeText();
            }
            else
            {
                ApplyColorToShip(m_ShipColor);
                QueueEditorSilhouetteShadow();
                QueueEditorChimneySetup();
            }
        }

        private void OnDisable()
        {
            s_ActiveShips.Remove(this);

            // Slota giderken yolculuğu kesilen (silinen / kapatılan) gemi seyir sesini de bıraksın
            if (m_SailSoundActive)
            {
                m_SailSoundActive = false;
                if (HypercasualFeedbackManager.ExistingInstance != null) HypercasualFeedbackManager.ExistingInstance.StopShipSailSound();
            }

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
            // Kapasite yazısı ayarı Inspector'da değişince anında görünsün
            // UI konteynerde yazı sahnede elle konumlanır; 3B gemi pozu (0,0 / ölçek 1) uygulanırsa ayar siliniyordu
            if (m_BadgeText != null && !IsUiContainer) ApplyBadgeTextPose(m_BadgeText.rectTransform);
#if UNITY_EDITOR
            // Prefab'daki yazı ayarı değişince sahnedeki gemilere de hemen geçsin (sahnedeki kuyruk gemileri
            // prefab örneği değil; aksi halde değişiklik ancak Play'de görünüyordu)
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this))
            {
                ShipController source = this;
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    SyncBadgeSettingsToScene(source);
                    SyncChimneySettingsToScene(source);
                };
            }
#endif
            ApplyColorToShip(m_ShipColor);
            EnsureFakeShadow();
            QueueEditorSilhouetteShadow();
            QueueEditorChimneySetup();
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

        /// <summary>
        /// Baca ve duman efektini editörde (Play'e basmadan) sahnedeki gemilere otomatik kurar ve günceller.
        /// </summary>
        private void QueueEditorChimneySetup()
        {
#if UNITY_EDITOR
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || Application.isPlaying) return;
                if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
                if (!gameObject.scene.IsValid()) return;
                EnsureChimneyAndSmoke();
            };
#endif
        }

        private void Start()
        {
            m_EnableDrag = false;
            m_IsDragging = false;
            m_IsPickedUp = false;

            m_BaseLocalPosition = transform.localPosition;
            m_BaseLocalRotation = transform.localRotation;
            if (transform.lossyScale != Vector3.zero) m_BaseScale = transform.lossyScale;

            m_SmoothedWorldPosition = transform.position;
            m_VisualWorldPosition = transform.position;
            m_DragTargetWorldPosition = transform.position;
            m_PreviousWorldPosition = transform.position;

            EnsureDecoupledHierarchy();
            EnsureFakeShadow();
            m_WorkersExitedCount = m_CurrentCargo;
            if (IsUiContainer)
            {
                EnsureUiContainerLayout();
            }
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
            // UI konteyner boyutunu RectTransform ve m_UiDockedScale belirler
            if (IsUiContainer) return (m_UiDockedScale != Vector3.zero) ? m_UiDockedScale : Vector3.one;

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
            m_BaseScale = IsUiContainer ? ((m_UiDockedScale != Vector3.zero) ? m_UiDockedScale : Vector3.one) : scale;
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
            // Gizli konteyner de en öne gelince açılmalı: UI erken dönüşünden ÖNCE kontrol edilir
            UpdateMysteryReveal();
            if (IsUiContainer) return;
            UpdateBadgePlacement();
            UpdateHullRings();
        }

        /// <summary>
        /// Geminin dibindeki dalga halkaları: gövdenin su hattındaki izdüşümünden biraz büyük elipsler,
        /// sırayla yavaşça genişleyip kaybolur. Gemi hareket ederken / kalkarken gizlenir.
        /// </summary>
        private void UpdateHullRings()
        {
            if (!Application.isPlaying) return;
            bool show = m_EnableHullRings && !m_IsMoving && !m_IsDeparting && !m_IsDragging && !m_IsPickedUp;
            if (!show)
            {
                if (m_HullRings != null)
                    foreach (var r in m_HullRings) if (r != null && r.gameObject.activeSelf) r.gameObject.SetActive(false);
                return;
            }
            if (!EnsureHullRings()) return;

            if (s_HullRingBlock == null) s_HullRingBlock = new MaterialPropertyBlock();
            float cycle = Mathf.Max(0.1f, m_HullRingCycle);
            for (int i = 0; i < m_HullRings.Length; i++)
            {
                Transform ring = m_HullRings[i];
                if (ring == null) continue;
                if (!ring.gameObject.activeSelf) ring.gameObject.SetActive(true);
                float t = Mathf.Repeat(Time.time / cycle + m_HullRingPhase + (float)i / m_HullRings.Length, 1f);
                float k = m_HullRingStartScale * (1f + m_HullRingGrow * t);
                // Halka maskesi quad'ın yarı boyunun 0.92'sinde: gövde kenarına oturması için ölçek buna göre
                ring.localScale = new Vector3(m_HullRingHalfSize.x * 2f * k / 0.92f, m_HullRingHalfSize.y * 2f * k / 0.92f, 1f);
                Color c = m_HullRingColor;
                c.a *= Mathf.Sin(t * Mathf.PI) * (1f - t * 0.5f);
                s_HullRingBlock.SetColor(HullRingColorProp, c);
                ring.GetComponent<MeshRenderer>().SetPropertyBlock(s_HullRingBlock);
            }
        }

        private bool EnsureHullRings()
        {
            if (m_HullRings != null && m_HullRings.Length == m_HullRingCount && m_HullRings[0] != null) return true;
            if (m_HullRings != null)
                foreach (var r in m_HullRings) if (r != null) Destroy(r.gameObject);

            if (s_HullRingMaterial == null)
            {
                Shader sh = Shader.Find("PixelGame/WaterRippleRing");
                if (sh == null) return false;
                s_HullRingMaterial = new Material(sh) { name = "HullRing_Mat" };
                s_HullRingMaterial.SetFloat("_InnerRadius", 0.875f);
                s_HullRingMaterial.SetFloat("_OuterRadius", 0.92f);
                s_HullRingMaterial.SetFloat("_EdgeSmoothness", 0.025f);
            }

            // Gövdenin gemi kök uzayındaki su hattı izdüşümü (x: en, z: boy)
            float hx = 0f, hz = 0f;
            Transform src = m_VisualRoot != null ? m_VisualRoot : transform;
            foreach (MeshRenderer r in src.GetComponentsInChildren<MeshRenderer>(false))
            {
                string n = r.gameObject.name;
                if (n.Contains("Shadow") || n.Contains("Smoke") || n.Contains("Canvas") || n.Contains("HullRing")) continue;
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Bounds b = mf.sharedMesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents,
                        new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f));
                    Vector3 lp = transform.InverseTransformPoint(r.transform.TransformPoint(corner));
                    hx = Mathf.Max(hx, Mathf.Abs(lp.x));
                    hz = Mathf.Max(hz, Mathf.Abs(lp.z));
                }
            }
            if (hx <= 0f || hz <= 0f) return false;
            m_HullRingHalfSize = new Vector2(hx, hz);
            m_HullRingPhase = UnityEngine.Random.value;

            int count = Mathf.Max(1, m_HullRingCount);
            m_HullRings = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "[HullRing]";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = s_HullRingMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                m_HullRings[i] = go.transform;
            }
            return true;
        }

        /// <summary>
        /// Kapasite yazısı ayarlarını başka bir gemiden (prefab) kopyalar. Sahneye kaydedilmiş kuyruk gemileri
        /// prefab örneği olmadığı için prefab'daki ayar değişikliği onlara kendiliğinden geçmez.
        /// </summary>
        public void CopyBadgeSettingsFrom(ShipController source)
        {
            if (source == null || source == this) return;
            m_BadgePaintedOnRoof = source.m_BadgePaintedOnRoof;
            m_BadgeRoofNudge = source.m_BadgeRoofNudge;
            m_BadgeRoofScale = source.m_BadgeRoofScale;
            m_BadgeRoofStretch = source.m_BadgeRoofStretch;
            UpdateBadgePlacement();
        }

#if UNITY_EDITOR
        private static void SyncBadgeSettingsToScene(ShipController source)
        {
            if (source == null || Application.isPlaying) return;
            var ships = UnityEngine.Object.FindObjectsByType<ShipController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var ship in ships)
            {
                if (ship == null || ship == source || UnityEditor.PrefabUtility.IsPartOfPrefabAsset(ship)) continue;
                if (ship.m_BadgePaintedOnRoof == source.m_BadgePaintedOnRoof && ship.m_BadgeRoofNudge == source.m_BadgeRoofNudge &&
                    Mathf.Approximately(ship.m_BadgeRoofScale, source.m_BadgeRoofScale) && Mathf.Approximately(ship.m_BadgeRoofStretch, source.m_BadgeRoofStretch))
                    continue;
                UnityEditor.Undo.RecordObject(ship, "Kapasite yazısı ayarı (prefab)");
                ship.CopyBadgeSettingsFrom(source);
                UnityEditor.EditorUtility.SetDirty(ship);
            }
        }

        private static void SyncChimneySettingsToScene(ShipController source)
        {
            if (source == null || Application.isPlaying) return;
            var ships = UnityEngine.Object.FindObjectsByType<ShipController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var ship in ships)
            {
                if (ship == null || ship == source || UnityEditor.PrefabUtility.IsPartOfPrefabAsset(ship)) continue;
                // Ayarlar zaten aynıysa dokunma (OnValidate her derlemede çalışıyor; yoksa sahne sürekli "değişti" (*) oluyordu)
                if (ship.m_EnableChimneySmoke == source.m_EnableChimneySmoke && ship.m_ChimneyCorner == source.m_ChimneyCorner &&
                    ship.m_ChimneyLocalPos == source.m_ChimneyLocalPos && ship.m_ChimneyBodyMaterial == source.m_ChimneyBodyMaterial &&
                    ship.m_ChimneyRimMaterial == source.m_ChimneyRimMaterial && ship.m_ChimneySmokeMaterial == source.m_ChimneySmokeMaterial)
                    continue;
                UnityEditor.Undo.RecordObject(ship, "Baca ayarı (prefab)");
                ship.m_EnableChimneySmoke = source.m_EnableChimneySmoke;
                ship.m_ChimneyCorner = source.m_ChimneyCorner;
                ship.m_ChimneyLocalPos = source.m_ChimneyLocalPos;
                ship.m_ChimneyBodyMaterial = source.m_ChimneyBodyMaterial;
                ship.m_ChimneyRimMaterial = source.m_ChimneyRimMaterial;
                ship.m_ChimneySmokeMaterial = source.m_ChimneySmokeMaterial;
                ship.EnsureChimneyAndSmoke();
                UnityEditor.EditorUtility.SetDirty(ship);
            }
        }
#endif

        /// <summary>Sayı yazısı rozetin tam ortasında, düz durur (çatıya yatırma rozet tuvalinde yapılır).</summary>
        private static void ApplyBadgeTextPose(RectTransform rt)
        {
            if (rt == null) return;
            if (rt.localPosition != Vector3.zero) rt.localPosition = Vector3.zero;
            if (rt.localRotation != Quaternion.identity) rt.localRotation = Quaternion.identity;
            if (rt.localScale != Vector3.one) rt.localScale = Vector3.one;
        }

        public void UpdateBadgePlacement()
        {
            if (IsUiContainer) return;

            if (m_BadgeCanvasObj == null)
            {
                Transform foundTr = transform.Find("Ship_Capacity_Canvas");
                if (foundTr != null) m_BadgeCanvasObj = foundTr.gameObject;
            }
            if (m_BadgeCanvasObj == null) return;

            // Kullanıcı isteği: "textteki sayı dolduğunda text yok olsun"
            // Kapasite dolduğunda veya gemi kalkışta iken rozet ve metin KESİNLİKLE gizlenir.
            // Gizli gemide sayı yerine aynı stilde süzülen bir "?" gösterilir (UpdateBadgeText)
            if (m_IsDeparting || IsFull || DisplayRemainingCapacity <= 0)
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
            Vector3 roofLocalPos = m_BadgePaintedOnRoof ? RoofSurfaceCenter + m_BadgeRoofNudge : new Vector3(0f, 2.22f, -0.42f);
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

            // 2. Çatıya boyalı: tuval çatı düzlemine yatar (ön yüzü yukarı, yazının üstü geminin pruvasına),
            //    gemiyle birlikte döner. Değilse kameraya dik tabela gibi bakar.
            Camera cam = MainCamera;
            Quaternion targetWorldRot = m_BadgePaintedOnRoof
                ? sourceTr.rotation * Quaternion.Euler(90f, 0f, 0f)
                : (cam != null ? cam.transform.rotation : Quaternion.identity);
            if (canvasTr.rotation != targetWorldRot)
            {
                canvasTr.rotation = targetWorldRot;
            }

            // 3. Tavanın ortasına tam oturan, net, dolgun HERO TEXT
            Vector3 boatLossy = transform.lossyScale;
            float avgLossy = (Mathf.Abs(boatLossy.x) + Mathf.Abs(boatLossy.y) + Mathf.Abs(boatLossy.z)) / 3f;
            if (avgLossy < 0.0001f) avgLossy = 0.35f;

            float targetLocalScaleFactor;
            if (m_BadgePaintedOnRoof)
            {
                targetLocalScaleFactor = 0.0055f * m_BadgeRoofScale;
            }
            else
            {
                targetLocalScaleFactor = 0.0085f / avgLossy;
            }

            if (m_IsMysteryHidden && Application.isPlaying)
            {
                // "?" rozeti hafifçe nefes alır gibi büyüyüp küçülür
                targetLocalScaleFactor *= 1f + Mathf.Sin(Time.time * 2.4f + m_BobRandomOffset * 1.7f) * 0.07f;
            }
            Vector3 targetLocalScale = Vector3.one * targetLocalScaleFactor;
            if (m_BadgePaintedOnRoof) targetLocalScale.y *= m_BadgeRoofStretch; // tuvalin Y'si çatıda pruva yönü
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
                    if (rt.sizeDelta != new Vector2(110f, 62f)) rt.sizeDelta = new Vector2(110f, 62f);
                    ApplyBadgeTextPose(rt);
                }

                ApplyBadgeTextStyle(m_BadgeText);
                m_BadgeConfigured = true;
            }

            if (!m_BadgeCanvasObj.activeSelf && !m_IsDeparting && !IsFull && DisplayRemainingCapacity > 0)
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
                Vector3 denialOffset = new Vector3(m_DenialWobbleOffsetX, 0f, 0f);

                m_VisualRoot.localPosition = pickupOffset + bobbingOffset + dipOffset + denialOffset;

                // Aşama 4 Rotation Composition:
                // FinalRotation = BobbingRotation * DynamicDragRotation * DenialWobbleRotation
                Quaternion bobbingRot = Quaternion.Euler(dPitch, 0f, dRoll);
                Quaternion dragRot = Quaternion.Euler(m_CurrentDragPitch, m_CurrentDragYaw, m_CurrentBankingRoll);
                Quaternion denialRot = Quaternion.Euler(0f, 0f, m_DenialWobbleAngle);
                m_VisualRoot.localRotation = bobbingRot * dragRot * denialRot;
            }
            else
            {
                transform.localPosition = m_BaseLocalPosition + new Vector3(m_DenialWobbleOffsetX, dy, 0f);
                transform.localRotation = m_BaseLocalRotation * Quaternion.Euler(dPitch, 0f, dRoll + m_DenialWobbleAngle);
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
            m_ActiveWorkersOut = 0;
            m_WorkersExitedCount = 0;
            m_ColorName = string.IsNullOrEmpty(colorName) ? ColorUtility.ToHtmlStringRGB(color) : colorName;

            m_IsMoving = false;
            m_IsQueueAnimating = false;
            m_IsDocked = false;
            m_IsDeparting = false;
            m_IsPickedUp = false;
            m_IsDragging = false;
            m_CandidateSlot = null;

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

        // Editörde sahneye kayıtlı gemi materyali yeniden kullanılır; aksi halde her derleme/sahne açılışında
        // yeni materyal üretilip sahne sürekli "değişti" (*) olarak işaretleniyordu. ClearMaterialCache
        // (ör. "Gemi Güvertelerini ... Yenile" menüsü) bilinçli yeniden üretim istediği için bunu kapatır.
        private static bool s_ReuseSceneBoatMaterials = true;

        public static void ClearMaterialCache()
        {
            s_CachedBoatTextures.Clear();
            s_CachedBoatMaterials.Clear();
            s_CachedChimneyRimMaterials.Clear();
            s_ReuseSceneBoatMaterials = false;
            ClearContainerSpriteCache();
        }

        public static void ClearContainerSpriteCache()
        {
            s_ContainerSprites.Clear();
            s_ContainerBasePixels = null;
            s_ContainerBaseTex = null;
        }

        /// <summary>
        /// Editörde, renderer'lardan birinde bu renk için zaten kayıtlı gemi materyali varsa onu önbelleğe alıp döndürür.
        /// </summary>
        private Material FindExistingBoatMaterial(Color color)
        {
            if (Application.isPlaying || !s_ReuseSceneBoatMaterials || m_Renderers == null) return null;

            Color32 key = (Color32)color;
            key.a = 255;
            if (s_CachedBoatMaterials.TryGetValue(key, out Material cached) && cached != null) return cached;

            string expectedName = $"Ship_BoatMat_{ColorUtility.ToHtmlStringRGB(color)}";
            foreach (var mr in m_Renderers)
            {
                if (mr == null) continue;
                Material existing = mr.sharedMaterial;
                if (existing != null && existing.name == expectedName)
                {
                    s_CachedBoatMaterials[key] = existing;
                    return existing;
                }
            }
            return null;
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

            Material mat = CartoonShader.CreateMaterial(color, $"Ship_BoatMat_{ColorUtility.ToHtmlStringRGB(color)}");
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            if (mat.HasProperty("_PlasticHighlightIntensity")) mat.SetFloat("_PlasticHighlightIntensity", 0.15f);
            if (mat.HasProperty("_SpecularColor")) mat.SetColor("_SpecularColor", new Color(1f, 1f, 1f, 0.35f));
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);

            s_CachedBoatMaterials[key] = mat;
            return mat;
        }

        // Kırmızı taban konteynerin (Resources/Containers/Container_Base, Read/Write açık) ölçülmüş ortanca parlaklık/doygunluğu
        private const float ContainerBaseValue = 0.92f;
        private const float ContainerBaseSaturation = 0.85f;
        private static readonly Dictionary<Color32, Sprite> s_ContainerSprites = new Dictionary<Color32, Sprite>();
        private static Color32[] s_ContainerBasePixels;
        private static Texture2D s_ContainerBaseTex;

        /// <summary>
        /// Bölüm tasarımındaki rengin AYNISINA boyanmış konteyner sprite'ı döndürür (8 hazır renge yuvarlanmaz).
        /// Taban kırmızı konteynerin tonu hedef renge çevrilir; ışık/gölge korunur, açık mavi kilit tokaları korunur.
        /// </summary>
        public static Sprite GetContainerSprite(Color color)
        {
            Color32 key = color;
            key.a = 255;
            if (s_ContainerSprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            if (s_ContainerBasePixels == null)
            {
#if UNITY_EDITOR
                s_ContainerBaseTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Containers/Container_Base.png");
#endif
                if (s_ContainerBaseTex == null) s_ContainerBaseTex = Resources.Load<Texture2D>("Containers/Container_Base");
                if (s_ContainerBaseTex == null || !s_ContainerBaseTex.isReadable)
                {
                    return Resources.Load<Sprite>("Containers/Container_Red");
                }
                s_ContainerBasePixels = s_ContainerBaseTex.GetPixels32();
            }

            Color.RGBToHSV(color, out float th, out float ts, out float tv);
            float satScale = ts / ContainerBaseSaturation;
            float valScale = tv / ContainerBaseValue;

            var src = s_ContainerBasePixels;
            var dst = new Color32[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                Color32 c = src[i];
                if (c.a == 0) { dst[i] = c; continue; }
                Color.RGBToHSV(c, out float h, out float s, out float v);
                // Açık mavi kilit tokaları ve mandalları (hue 0.52 - 0.68, s > 0.22f) boyanmaz, orijinal açık mavi kalır
                if (h >= 0.52f && h <= 0.68f && s > 0.22f) { dst[i] = c; continue; }
                float w = Mathf.Clamp01((s - 0.2f) / 0.15f); // gri/beyaz kilit çubukları ve kenar parlamaları boyanmaz
                // ...ama tonları hedef renge kaydırılır: kırmızı tabandan kalan pembe parlama izleri olmasın
                Color origTinted = Color.HSVToRGB(th, s, v);
                origTinted.a = c.a / 255f;
                if (w <= 0f) { dst[i] = origTinted; continue; }
                // Koyu renkte gölge çarpımla, açık renkte (beyaz vb.) farkla korunur; yoksa beyaz kutu düzleşiyordu
                float addW = tv * tv;
                float nv = (v * valScale) * (1f - addW) + (tv + (v - ContainerBaseValue) * 0.85f) * addW;
                Color recol = Color.HSVToRGB(th, Mathf.Clamp01(s * satScale), Mathf.Clamp01(nv));
                Color res = Color.Lerp(origTinted, recol, w);
                res.a = c.a / 255f;
                dst[i] = res;
            }

            var tex = new Texture2D(s_ContainerBaseTex.width, s_ContainerBaseTex.height, TextureFormat.RGBA32, false)
            {
                name = $"Container_{ColorUtility.ToHtmlStringRGB(color)}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            tex.SetPixels32(dst);
            tex.Apply(false, true);
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sp.name = tex.name;
            sp.hideFlags = HideFlags.DontSave;
            s_ContainerSprites[key] = sp;
            return sp;
        }

        /// <summary>
        /// Gemiyi küplerle %100 aynı tonda mat cartoon renk ile boyar.
        /// </summary>
        public void ApplyColorToShip(Color color)
        {
            m_ShipColor = color;
            if (IsUiContainer)
            {
                if (m_ContainerImage == null) m_ContainerImage = GetComponent<Image>() ?? GetComponentInChildren<Image>(true);
                if (m_ContainerImage != null)
                {
                    Sprite s = null;
                    if (m_IsMysteryHidden)
                    {
                        s = GetMysteryContainerSprite();
                    }
                    else if (!string.IsNullOrEmpty(m_ColorName))
                    {
#if UNITY_EDITOR
                        s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Textures/Containers/Container_{m_ColorName}.png");
#endif
                        if (s == null) s = Resources.Load<Sprite>($"Containers/Container_{m_ColorName}");
                    }
                    if (s == null) s = GetContainerSprite(color);
                    if (s != null) m_ContainerImage.sprite = s;
                    m_ContainerImage.color = Color.white;
                }
                EnsureUiOutline();
                if (m_BadgeText == null) m_BadgeText = GetComponentInChildren<TextMeshProUGUI>(true);
                if (Application.isPlaying && m_BadgeText != null && m_BadgeText.outlineWidth < 0.2f)
                {
                    // Rozetsiz sayı beyaz/açık konteynerde de okunsun
                    m_BadgeText.outlineColor = new Color32(30, 30, 40, 255);
                    m_BadgeText.outlineWidth = 0.25f;
                }
                UpdateBadgeText();
                return;
            }

            EnsureVisualComponents();
            CreateOrFindBadge();
            UpdateBadgePlacement();
            UpdateBadgeText();

            if (m_Renderers == null || m_Renderers.Length == 0)
            {
                m_Renderers = GetComponentsInChildren<MeshRenderer>(true);
            }

            Material boatMat = FindExistingBoatMaterial(color) ?? GetOrCreateBoatMaterial(color);

            foreach (var mr in m_Renderers)
            {
                if (mr == null) continue;
                // Gölge quad'ını asla gövde materyaliyle boyama
                if (mr.name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (mr.name.IndexOf("HullRing", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (mr.name.IndexOf("Hardware", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (m_FakeShadowObj != null && (mr.gameObject == m_FakeShadowObj || mr.transform.IsChildOf(m_FakeShadowObj.transform))) continue;
                // Dinamik varil renderers'ını ana gövde boyamasından ayrı tut
                if (m_CargoDeckRoot != null && mr.transform.IsChildOf(m_CargoDeckRoot)) continue;
                // Baca ve duman renderers'ını kendi özel materyalinde tut
                if (m_ChimneyTransform != null && (mr.gameObject == m_ChimneyTransform.gameObject || mr.transform.IsChildOf(m_ChimneyTransform))) continue;
                if (mr.name.IndexOf("Chimney", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (mr.name.IndexOf("Smoke", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

                var mats = mr.sharedMaterials;
                if (mats != null && mats.Length > 1)
                {
                    // Çoklu materyal: 0. eleman konteyner gövdesi, 1. eleman beyaz kilit çubukları ve kollar
                    mats[0] = boatMat;
                    if (Application.isPlaying) mr.materials = mats;
                    else mr.sharedMaterials = mats;
                }
                else
                {
                    if (Application.isPlaying)
                    {
                        mr.material = boatMat;
                    }
                    else
                    {
                        mr.sharedMaterial = boatMat;
                    }
                }
            }

            // Mevcut oluşturulmuş variller varsa renklerini de güncelle
            UpdateAllBarrelsColor();
            UpdateChimneyRimColor();

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

            // Kullanıcı isteği: "tüm işçiler geri binince de ayrılır"
            // Dışarıda hâlâ koşan işçi varsa kalkış yapılmaz, OnWorkerBoarded beklenir!
            if (IsFull && m_ActiveWorkersOut <= 0 && !m_IsDeparting)
            {
                OnCargoFilled?.Invoke(this);
                if (ShipDispatcher.Instance != null) ShipDispatcher.Instance.OnShipFilled(this);
                DepartAndFreeSlot();
            }
        }

        public void PlayCargoReceiveJuice()
        {
            transform.DOKill(true);
            if (IsUiContainer)
            {
                Vector3 currentScale = (m_UiDockedScale != Vector3.zero) ? m_UiDockedScale : transform.localScale;
                transform.DOPunchScale(new Vector3(0.08f, -0.08f, 0f) * currentScale.x, 0.14f, 3, 0.45f)
                    .OnComplete(() => transform.localScale = currentScale)
                    .OnKill(() => transform.localScale = currentScale);
            }
            else
            {
                transform.DOPunchScale(new Vector3(0.065f, -0.065f, 0.065f) * m_BaseScale.x, 0.12f, 2, 0.45f)
                    .OnComplete(() => transform.localScale = GetLocalScaleForBaseWorldScale());
            }
        }

        /// <summary>
        /// Kargo dolduğunda gemi slottan çıkar, sol tarafa doğru kavisli deniz rotasıyla hızlanarak yol alır ve yok olur.
        /// 2'li bağlı gemilerde kural: Biri dolunca tek başına gitmez; ikisi de dolana kadar bekler ve ikisi birden kalkar!
        /// </summary>
        public void DepartAndFreeSlot(bool force = false)
        {
            if (m_IsDeparting) return;

            // Kullanıcı isteği: "tüm işçiler geri binince de ayrılır"
            // Dışarıda işçi varken (force hariç) asla kalkış yapılmaz!
            if (!force && m_ActiveWorkersOut > 0)
            {
                Debug.Log($"<color=#FFAA00><b>[ShipController]</b></color> ⚓ '{name}' ({ShipColor}) henüz {m_ActiveWorkersOut} işçi dışarıda olduğu için ayrılamaz; tüm işçilerin geri binmesini bekliyor!");
                return;
            }

            // 2'li bağlı gemi kuralı:
            // Eğer gemi bağlıysa ve force == false ise, partneri de hazır (dolu) olmadan tek başına ASLA ayrılamaz!
            if (!force && HasActiveLinkedPartner)
            {
                if (!IsLinkedPartnerReadyToDepart)
                {
                    Debug.Log($"<color=#FFAA00><b>[ShipController]</b></color> ⚓ '{name}' ({ShipColor}) doldu ama bağlı partneri '{m_LinkedPartner.name}' ({m_LinkedPartner.ShipColor}) henüz dolmadı; partnerini bekliyor!");
                    return;
                }

                // İkisi de hazır! Partneri de birlikte kalkışa geçir:
                ShipController partnerToDepart = m_LinkedPartner;

                // Halatı kaldır
                if (m_Tether != null)
                {
                    Destroy(m_Tether.gameObject);
                    m_Tether = null;
                }
                if (partnerToDepart != null && partnerToDepart.Tether != null)
                {
                    Destroy(partnerToDepart.Tether.gameObject);
                    partnerToDepart.SetTether(null);
                }

                m_LinkedPartner = null;
                m_LinkId = 0;

                if (partnerToDepart != null && !partnerToDepart.IsDeparting)
                {
                    partnerToDepart.SetLinkedPartner(null, 0, null);
                    partnerToDepart.DepartAndFreeSlot(force: true);
                }
            }

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
            DepartAndFreeSlot(force: true);
        }

        private IEnumerator DepartToLeftRoutine()
        {
            if (IsUiContainer)
            {
                // 1. Text ve rozet hemen kaybolur (kullanıcı isteği: "sayı dolduğunda text yok olsun")
                if (m_BadgeText != null) m_BadgeText.gameObject.SetActive(false);
                if (m_BadgeCanvasObj != null) m_BadgeCanvasObj.SetActive(false);
                if (m_BadgeUIText != null) m_BadgeUIText.gameObject.SetActive(false);
                if (m_BadgeImage != null) m_BadgeImage.gameObject.SetActive(false);

                Transform pill = transform.Find("Badge_Pill");
                if (pill != null) pill.gameObject.SetActive(false);
                Transform socket = transform.Find("Socket_Recess");
                if (socket != null) socket.gameObject.SetActive(false);

                // DOTween ile süren önceki punch/ölçek tween'lerini durdur
                transform.DOKill(false);

                // Slot referansını sakla
                ShipSlot slotToFreeUi = m_CurrentSlot;
                m_CurrentSlot = null;

                // 2. Balon patlama animasyonu ("balon patlar gibi yok olsunlar pıt diye")
                // Aşama 1: Gerilme / Şişme (Anticipatory balloon swell - ~0.085s)
                Vector3 baseScale = transform.localScale;
                if (baseScale.x <= 0.001f)
                {
                    baseScale = (m_UiDockedScale != Vector3.zero) ? m_UiDockedScale : Vector3.one;
                }
                Vector3 swellScale = new Vector3(baseScale.x * 1.20f, baseScale.y * 1.26f, baseScale.z * 1.20f);

                float swellDur = 0.085f;
                float elapsed = 0f;
                while (elapsed < swellDur)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / swellDur);
                    float st = Mathf.Sin(t * Mathf.PI * 0.5f);
                    transform.localScale = Vector3.LerpUnclamped(baseScale, swellScale, st);
                    yield return null;
                }
                transform.localScale = swellScale;

                // Aşama 2: "PIT!" Patlama anı (Haptic, ASMR Ses & Görsel UI Patlama Parçacıkları)
                if (HypercasualFeedbackManager.Instance != null)
                {
                    HypercasualFeedbackManager.Instance.PlayBalloonPopFeedback(transform.position, m_ShipColor);
                }

                // UI Canvas üzerinde sevimli balon parçacıkları / pıt efekti
                SpawnUiBalloonPopBurst(transform.parent, transform.localPosition, m_ShipColor);

                // Slottan ayrılış: tam patlama anında slot serbest kalır, sıradaki konteyner hemen gelebilir
                if (slotToFreeUi != null) slotToFreeUi.ReleaseShip();

                // Aşama 3: Ani içe patlama ve sıfıra çekilerek yok oluş (~0.075s)
                float popDur = 0.075f;
                elapsed = 0f;
                while (elapsed < popDur)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / popDur);
                    float pt = t * t * t; // Kübik hızlanarak kayboluş
                    transform.localScale = Vector3.LerpUnclamped(swellScale, Vector3.zero, pt);
                    yield return null;
                }
                transform.localScale = Vector3.zero;

                OnDeparted?.Invoke(this);
                Destroy(gameObject);
                yield break;
            }

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

            // 🔊 Neşeli Liman Düdüğü & 📳 Kalkış Titreşimi
            HypercasualFeedbackManager.Instance.PlayShipDepartFeedback(transform.position);

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

            // Slottan tamamen geriye çıkınca slotu serbest bırak
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
            Quaternion leftTargetRot = WaterPlaneRotation * Quaternion.Euler(0f, -90f, 0f);

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

            SetSmokeEmissionMultiplier(2.2f);

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
        /// Yük konteyneri dolup balon gibi patladığında UI Canvas üzerinde renkli elmas parçacıklarının
        /// dışa doğru fırlayıp kaybolduğu sevimli "pıt" patlama görsel efekti üretir.
        /// </summary>
        private void SpawnUiBalloonPopBurst(Transform parent, Vector3 localPos, Color containerColor)
        {
            if (parent == null) return;

            GameObject burstObj = new GameObject("[BalloonPop_Burst_FX]", typeof(RectTransform));
            burstObj.transform.SetParent(parent, false);
            RectTransform burstRt = burstObj.GetComponent<RectTransform>();
            burstRt.anchorMin = new Vector2(0.5f, 0.5f);
            burstRt.anchorMax = new Vector2(0.5f, 0.5f);
            burstRt.pivot = new Vector2(0.5f, 0.5f);
            burstRt.localPosition = localPos;
            burstRt.localScale = Vector3.one;

            // 8 adet radyal balon parçacığı (küçük renkli elmas konfetiler)
            int particleCount = 8;
            for (int i = 0; i < particleCount; i++)
            {
                float angle = i * (360f / particleCount) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float flyDist = UnityEngine.Random.Range(55f, 90f);
                Vector2 targetPos = dir * flyDist;

                GameObject pObj = new GameObject($"PopBit_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                pObj.transform.SetParent(burstObj.transform, false);

                RectTransform prt = pObj.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(0.5f, 0.5f);
                prt.anchorMax = new Vector2(0.5f, 0.5f);
                prt.pivot = new Vector2(0.5f, 0.5f);
                prt.anchoredPosition = dir * 10f;
                float size = UnityEngine.Random.Range(16f, 24f);
                prt.sizeDelta = new Vector2(size, size);
                prt.localRotation = Quaternion.Euler(0f, 0f, 45f + i * 22.5f); // Tatlı elmas duruşu

                Image pImg = pObj.GetComponent<Image>();
                Color pColor = Color.Lerp(containerColor, Color.white, (i % 2 == 0) ? 0.30f : 0.65f);
                pImg.color = pColor;
                pImg.raycastTarget = false;

                // Dışa patlama, küçülme ve solma
                prt.DOAnchorPos(targetPos, 0.16f).SetEase(Ease.OutQuad);
                prt.DOScale(Vector3.zero, 0.16f).SetEase(Ease.InQuad);
                pImg.DOFade(0f, 0.16f).SetEase(Ease.InQuad);
            }

            // Burst kök nesnesini animasyon bitince temizle
            if (Application.isPlaying) Destroy(burstObj, 0.22f);
            else DestroyImmediate(burstObj);
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
            return slot.transform.TransformPoint(ComputeDockLocalPosition(slot));
        }

        /// <summary>
        /// Yanaşma noktası (slot yerel uzayında). Geminin kıçı slot görselinin ([Slot_Berth]) arka (açık su) kenarına
        /// oturacak şekilde hesaplanır; böylece gemi ileri taşıp slotun alt kısmını boş bırakmaz.
        /// Ölçülemezse sabit ofsete düşer.
        /// </summary>
        private Vector3 ComputeDockLocalPosition(ShipSlot slot)
        {
            Vector3 fallback = new Vector3(m_DockSideOffset, m_DockHeightOffset, m_DockForwardOffset);
            if (!m_AlignSternToSlotBack || slot == null) return fallback;

            Transform berth = slot.transform.Find("[Slot_Berth]");
            if (berth == null || !TryGetSternLocalZ(out float sternLocalZ)) return fallback;

            Transform slotTr = slot.transform;
            float baseScaleZ = m_BaseScale != Vector3.zero ? m_BaseScale.z : transform.lossyScale.z;
            float rootToSternWorld = Mathf.Max(0f, -sternLocalZ) * Mathf.Abs(baseScaleZ);

            // Berth quad'ı yerel -0.5..+0.5 (z) aralığında; -z ucu açık suya bakan arka kenar
            float berthBackLocalZ = berth.localPosition.z - berth.localScale.z * 0.5f;
            float berthWorldLength = berth.localScale.z * Mathf.Abs(slotTr.lossyScale.z);
            Vector3 backWorld = slotTr.TransformPoint(new Vector3(0f, m_DockHeightOffset, berthBackLocalZ));
            Vector3 slotForward = slotTr.TransformDirection(Vector3.forward).normalized;

            Vector3 rootWorld = backWorld + slotForward * (rootToSternWorld + berthWorldLength * m_SternInsetRatio);
            Vector3 local = slotTr.InverseTransformPoint(rootWorld);
            return new Vector3(m_DockSideOffset, m_DockHeightOffset, local.z);
        }

        /// <summary>Gövde görsellerinin gemi kök uzayındaki en arka (kıç) z değeri.</summary>
        private bool TryGetSternLocalZ(out float sternZ)
        {
            sternZ = float.MaxValue;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(false))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (r.GetComponent<TMPro.TMP_Text>() != null) continue;
                string n = r.gameObject.name;
                if (n.Contains("Shadow") || n.Contains("Smoke") || n.Contains("Canvas") || n.Contains("HullRing")) continue;

                Bounds b;
                if (r is SkinnedMeshRenderer smr) b = smr.localBounds;
                else
                {
                    MeshFilter mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    b = mf.sharedMesh.bounds;
                }

                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents,
                        new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f));
                    float z = transform.InverseTransformPoint(r.transform.TransformPoint(corner)).z;
                    if (z < sternZ) sternZ = z;
                }
            }
            return sternZ < float.MaxValue;
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

        /// <summary>
        /// 2D Canvas UI konteynerler için neşeli, zıplayarak slota yerleşme (hop & bounce) animasyonu.
        /// Konteyner boyutu (sizeDelta) ve ölçeği (Vector3.one) kesinlikle korunur, asla küçülmez.
        /// </summary>
        private IEnumerator SailToSlotRoutineUi(ShipSlot targetSlot, float customDuration = -1f, Action onComplete = null)
        {
            m_IsMoving = true;
            m_IsDragging = false;
            m_IsPickedUp = false;
            m_CandidateSlot = null;
            transform.DOKill(true);

            // Eski slottan ayrıl
            if (m_CurrentSlot != null && m_CurrentSlot != targetSlot)
            {
                m_CurrentSlot.ReleaseShip();
            }

            // Slota bağla
            targetSlot.DockShip(this);
            m_CurrentSlot = targetSlot;

            RectTransform rt = transform as RectTransform;
            // Konteynerın orijinal boyutunu koru: boyutu ASLA küçülmesin!
            Vector2 targetSize = (rt != null && rt.sizeDelta.x > 80f) ? rt.sizeDelta : new Vector2(160f, 163f);

            // Uçuş sırasında diğer UI elemanlarının en önünde görünmesi için Canvas köküne al
            Transform uiRoot = targetSlot.transform.parent != null ? targetSlot.transform.parent.parent : null;
            if (uiRoot == null)
            {
                Canvas c = targetSlot.GetComponentInParent<Canvas>();
                if (c != null) uiRoot = c.transform;
            }
            if (uiRoot != null)
            {
                transform.SetParent(uiRoot, true);
                transform.SetAsLastSibling();
            }

            // Slota yerleştiğinde ve uçuş sırasında orijinal görsel boyutunu kesinlikle koru (asla küçülmesin)
            Vector3 flightBaseScale = transform.localScale;
            if (flightBaseScale.x <= 0.001f) flightBaseScale = Vector3.one;

            Vector3 targetLocalScale = flightBaseScale;
            if (targetSlot != null && targetSlot.transform.lossyScale.x > 0.0001f)
            {
                Vector3 myLossy = transform.lossyScale;
                Vector3 slotLossy = targetSlot.transform.lossyScale;
                targetLocalScale = new Vector3(
                    myLossy.x / slotLossy.x,
                    myLossy.y / slotLossy.y,
                    myLossy.z / slotLossy.z
                );
            }
            if (targetLocalScale.x <= 0.001f) targetLocalScale = flightBaseScale;
            m_UiDockedScale = targetLocalScale;

            Vector3 startPos = transform.position;
            Vector3 targetPos = targetSlot.transform.position;

            float duration = (customDuration > 0f) ? customDuration : 0.38f;
            float elapsed = 0f;

            // Zıplama yayı yüksekliği (dünya/Canvas uzayında tatlı bir kavis):
            float jumpArcHeight = Mathf.Abs(targetPos.y - startPos.y) * 0.28f + 0.12f;
            float xDelta = targetPos.x - startPos.x;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Pürüzsüz hızlanma ve yavaşlama:
                float easeT = Mathf.SmoothStep(0f, 1f, t);

                // Yatay ve dikey hareket + havada kavisli zıplama (hop arc):
                Vector3 currentPos = Vector3.Lerp(startPos, targetPos, easeT);
                currentPos.y += Mathf.Sin(t * Mathf.PI) * jumpArcHeight;
                transform.position = currentPos;

                // Havada hareket yönüne göre tatlı ve neşeli hafif bir eğilme (tilt):
                float tiltAngle = Mathf.Clamp(-xDelta * 2.5f, -6f, 6f) * Mathf.Sin(t * Mathf.PI);
                transform.localRotation = Quaternion.Euler(0f, 0f, tiltAngle);

                // Havada hafif esneme (stretch):
                float stretch = Mathf.Sin(t * Mathf.PI) * 0.08f;
                transform.localScale = new Vector3(flightBaseScale.x * (1f - stretch * 0.5f), flightBaseScale.y * (1f + stretch), flightBaseScale.z);

                if (rt != null) rt.sizeDelta = targetSize;

                yield return null;
            }

            // 6. Slota Yerleşme ve Tatlı Zıplama / Yaylanma Animasyonu:
            transform.SetParent(targetSlot.transform, false); // worldPositionStays = false (asla bozulmaz!)
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = targetLocalScale;

            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = targetSize; // Boyut kesinlikle korunur
            }

            // "konteynarları slota yerleştirirken böyle hafif zıplar gibi yerleşsinler istiyorum"
            // Juicy Settling Bounce (Zıplayarak Slota Oturma):
            // 1. Dokunma anında hafif ezilme (squash):
            // 2. Yukarı neşeli zıplama (hop rebound):
            // 3. Slota geri iniş (impact fall):
            // 4. Kesin ve elastik olarak orijinal boyutunda yerine oturma:
            Sequence settleBounce = DOTween.Sequence();
            // Dokunma ezilmesi (squash):
            settleBounce.Append(transform.DOScale(new Vector3(targetLocalScale.x * 1.10f, targetLocalScale.y * 0.88f, targetLocalScale.z), 0.07f).SetEase(Ease.OutQuad));
            // Yukarı neşeli tatlı zıplama (hop rebound):
            settleBounce.Append(rt.DOAnchorPosY(16f, 0.11f).SetEase(Ease.OutQuad));
            settleBounce.Join(transform.DOScale(new Vector3(targetLocalScale.x * 0.95f, targetLocalScale.y * 1.07f, targetLocalScale.z), 0.11f));
            // Slota geri iniş:
            settleBounce.Append(rt.DOAnchorPosY(0f, 0.10f).SetEase(Ease.InQuad));
            settleBounce.Join(transform.DOScale(new Vector3(targetLocalScale.x * 1.05f, targetLocalScale.y * 0.94f, targetLocalScale.z), 0.10f));
            // Elastik tam oturma:
            settleBounce.Append(transform.DOScale(targetLocalScale, 0.10f).SetEase(Ease.OutBack));

            yield return settleBounce.WaitForCompletion();

            // Kesin boyut ve konum teyidi (asla küçülmesin)
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = targetLocalScale;
            if (rt != null)
            {
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = targetSize;
            }

            // Ses ve dokunsal geri bildirim:
            if (HypercasualFeedbackManager.Instance != null)
            {
                HypercasualFeedbackManager.Instance.PlayShipDockFeedback(transform.position);
            }

            m_IsMoving = false;
            m_IsDocked = true;

            if (ShipDispatcher.Instance != null)
            {
                ShipDispatcher.Instance.OnShipDocked(this);
            }

            onComplete?.Invoke();
        }

        private IEnumerator SailToSlotRoutine(ShipSlot targetSlot, float customDuration = -1f, Action onComplete = null)
        {
            if (IsUiContainer)
            {
                yield return StartCoroutine(SailToSlotRoutineUi(targetSlot, customDuration, onComplete));
                yield break;
            }

            m_IsMoving = true;
            m_IsDragging = false;
            m_IsPickedUp = false;
            m_CandidateSlot = null;
            m_EnableWaterBobbing = false;
            ResetVisualOffset();
            transform.DOKill(true);

            SetSmokeEmissionMultiplier(1.8f);

            // 🚢 Gemi slotlara gidene kadar çalacak seyir/motor sesi aktif olur:
            if (HypercasualFeedbackManager.Instance != null)
            {
                HypercasualFeedbackManager.Instance.StartShipSailSound(transform.position);
                m_SailSoundActive = true;
            }

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

            Vector3 targetLocalPos = ComputeDockLocalPosition(targetSlot);
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

                if (!IsUiContainer && tangent.sqrMagnitude > 1e-5f)
                {
                    Quaternion pathRot = Quaternion.LookRotation(tangent.normalized, slotUp);
                    // Rota sonuna yaklaştıkça (%70+) slotun kesin açısıyla tam hizalanır
                    float alignWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((easeT - 0.70f) / 0.30f));
                    transform.rotation = Quaternion.Slerp(pathRot, targetSlotWorldRot, alignWeight);
                }
                else if (IsUiContainer)
                {
                    transform.localRotation = Quaternion.identity;
                }

                // Viraja yatma (Bank Roll) m_VisualRoot üzerinde uygulanır
                float bankRoll = Mathf.Sin(easeT * Mathf.PI) * (-Mathf.Sign(lateralDelta) * Mathf.Clamp(Mathf.Abs(lateralDelta) * 4.5f, 1.5f, 6.0f));
                float finalRollWeight = 1f - Mathf.Clamp01((easeT - 0.65f) / 0.35f);
                if (m_VisualRoot != null && !IsUiContainer)
                {
                    m_VisualRoot.localRotation = Quaternion.Euler(0f, 0f, bankRoll * finalRollWeight);
                    // Ölçek varışa doğru normale döner
                    m_VisualRoot.localScale = Vector3.Lerp(Vector3.one * 1.04f, Vector3.one, easeT);
                    m_VisualRoot.localPosition = new Vector3(0f, Mathf.Lerp(0.04f, 0f, easeT), 0f);
                }

                // Duman ve su dalgası efekti
                if (!IsUiContainer && Time.time - lastSmokeTime > 0.045f)
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
            transform.localPosition = IsUiContainer ? Vector3.zero : targetLocalPos;
            transform.localRotation = Quaternion.identity;
            m_BaseLocalRotation = Quaternion.identity;
            if (!IsUiContainer)
            {
                transform.localScale = GetLocalScaleForBaseWorldScale();
            }
            else
            {
                transform.localScale = (m_UiDockedScale != Vector3.zero) ? m_UiDockedScale : Vector3.one;
            }

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

            // 🚢 Gemi slota ulaşıp yerleşti — seyir sesi biter!
            if (HypercasualFeedbackManager.Instance != null)
            {
                HypercasualFeedbackManager.Instance.StopShipSailSound();
                m_SailSoundActive = false;
            }

            // 💦 Su Sıçraması, 🔊 İskele Kilit Darbesi Sesi & 📳 Orta Mobil Titreşim
            HypercasualFeedbackManager.Instance.PlayShipDockFeedback(transform.position);

            m_IsMoving = false;
            m_IsDocked = true;
            m_EnableWaterBobbing = true;
            SetSmokeEmissionMultiplier(1.0f);

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
        /// <summary>
        /// Su yüzeyinin dünya rotasyonu: gemiler bu düzlemde durur. Kuyruk havuzunun rotasyonundan okunur
        /// (kamera açısı değişince gemi tarafı da birlikte döndürülür); havuz yoksa eski sabit açı.
        /// </summary>
        public static Quaternion WaterPlaneRotation =>
            ShipQueuePool.Instance != null ? ShipQueuePool.Instance.transform.rotation : Quaternion.Euler(-68f, 0f, 0f);

        public static void SpawnWaterRipple(Vector3 worldPos, float startScale = 0.2f, float maxScale = 0.75f, float duration = 0.45f)
        {
            // Suyu kaldırdığımız için su dalgaları oluşturulmaz
            return;
        }

        private static Material s_SmokePuffSharedMaterial;

        /// <summary>
        /// Referans videodaki gibi geminin arkasında beliren yumuşak beyaz puf duman bulutu (Cartoon Smoke Puff) oluşturur.
        /// </summary>
        public static void SpawnSmokePuff(Vector3 worldPos, float startScale = 0.10f, float maxScale = 0.32f, float duration = 0.38f)
        {
            // Konteynerlerde duman oluşturulmaz
            return;
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
            m_IsQueueAnimating = animating;
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
        /// Slotlar dolu olduğunda veya gemi gönderilemediğinde çalışan ret ve uyarı animasyonu:
        /// 1. Gemi iki yana belirgin şekilde sallanır (wobble / denial shake).
        /// 2. Kapasite metni (sayı) anında dikkat çekici kırmızıya döner (#FF3838) ve hafif büyüyüp yumuşakça eski beyaz haline döner.
        /// </summary>
        public void PlayDenialFeedback()
        {
            if (m_IsMoving || m_IsDeparting) return;

            TriggerDenialWobble();
            FlashBadgeTextWarningRed();
            HypercasualFeedbackManager.Instance.PlayDenialFeedback();
        }

        /// <summary>
        /// Geriye uyumluluk için PlayWobble çağrısı doğrudan PlayDenialFeedback'i tetikler.
        /// </summary>
        public void PlayWobble()
        {
            PlayDenialFeedback();
        }

        /// <summary>
        /// Geminin iki yana belirgin ve canlı şekilde "hayır" dercesine sallanmasını sağlar.
        /// </summary>
        public void TriggerDenialWobble()
        {
            m_DenialWobbleTween?.Kill();
            m_DenialWobblePosTween?.Kill();

            m_DenialWobbleAngle = 0f;
            m_DenialWobbleOffsetX = 0f;

            // Z ekseninde canlı ve belirgin hayır sallanması (±18 derece yaylanarak sönüm)
            m_DenialWobbleTween = DOTween.Punch(() => new Vector3(m_DenialWobbleAngle, 0f, 0f), v => m_DenialWobbleAngle = v.x, new Vector3(18f, 0f, 0f), 0.42f, 10, 0.55f)
                .SetUpdate(true)
                .OnComplete(() => m_DenialWobbleAngle = 0f);

            // X ekseninde hafif yatay sarsıntı (iki yana kafa sallama hissi)
            m_DenialWobblePosTween = DOTween.Punch(() => new Vector3(m_DenialWobbleOffsetX, 0f, 0f), v => m_DenialWobbleOffsetX = v.x, new Vector3(0.14f, 0f, 0f), 0.38f, 10, 0.55f)
                .SetUpdate(true)
                .OnComplete(() => m_DenialWobbleOffsetX = 0f);
        }

        /// <summary>
        /// Kapasite textini (veya '?' işaretini) anında parlak uyarı kırmızısına boyar,
        /// hafifçe büyütüp yaylandırır ve ardından yumuşakça orijinal beyaz renge geri döndürür.
        /// </summary>
        public void FlashBadgeTextWarningRed()
        {
            Color warningRed = new Color(1.0f, 0.22f, 0.22f, 1.0f); // Parlak ve belirgin uyarı kırmızısı (#FF3838)

            if (m_BadgeText != null)
            {
                m_TextWarningColorTween?.Kill();
                m_TextWarningScaleTween?.Kill();

                m_BadgeText.color = warningRed;
                m_BadgeText.transform.localScale = Vector3.one * 1.30f;

                // Hafifçe yaylanarak eski boyutuna döner
                m_TextWarningScaleTween = m_BadgeText.transform
                    .DOScale(Vector3.one, 0.38f)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true);

                // Kırmızıdan yumuşakça beyaz renge solar
                m_TextWarningColorTween = m_BadgeText
                    .DOColor(Color.white, 0.45f)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
            }

            if (m_BadgeUIText != null)
            {
                m_BadgeUIText.DOKill();
                m_BadgeUIText.color = warningRed;
                m_BadgeUIText.transform.localScale = Vector3.one * 1.30f;
                m_BadgeUIText.transform.DOScale(Vector3.one, 0.38f).SetEase(Ease.OutBack).SetUpdate(true);
                m_BadgeUIText.DOColor(Color.white, 0.45f).SetEase(Ease.OutQuad).SetUpdate(true);
            }

            if (m_BadgeImage != null)
            {
                m_BadgeImage.DOKill();
                m_BadgeImage.color = new Color(1.0f, 0.55f, 0.55f, 1.0f);
                m_BadgeImage.DOColor(Color.white, 0.38f).SetEase(Ease.OutQuad).SetUpdate(true);
            }
        }

        #region 🖐️ Gerçek Zamanlı Drag & Smooth Follow (Aşama 3)

        public bool CanInitiateDrag() => false;

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
            if (CasualHudController.IsHardBannerActive) return;
            m_IsPointerDown = true;
            m_DragThresholdPassed = false;
            m_WasDragged = false;
            m_IsDragging = false;
            m_IsPickedUp = false;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
        }

        public void OnDrag(PointerEventData eventData)
        {
        }

        public void OnEndDrag(PointerEventData eventData)
        {
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            m_IsPointerDown = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (CasualHudController.IsHardBannerActive) return;
            if (m_IsMoving || m_IsDeparting || m_IsQueueAnimating) return;
            if (IsLinked && m_LinkedPartner != null && m_LinkedPartner.IsQueueAnimating) return;

            // Zaten slotta yanaşmış bir gemiye tıklandıysa canlı dokunma yaylanması yap
            if (m_IsDocked)
            {
                PlayCargoReceiveJuice();
                return;
            }

            if (ShipDispatcher.Instance != null && (ShipDispatcher.Instance.IsAutoPlacing || ShipDispatcher.Instance.IsLevelFailed)) return;

            // Bağlı gemi henüz serbest değilse uyar ve gönderme
            if (IsLinked && !CanDispatchLinked())
            {
                PlayDenialFeedback();
                if (m_LinkedPartner != null) m_LinkedPartner.PlayDenialFeedback();
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

                ShipQueuePool queuePool = ShipQueuePool.Instance != null ? ShipQueuePool.Instance : UnityEngine.Object.FindFirstObjectByType<ShipQueuePool>();
                if (IsLinked && m_LinkedPartner != null && partnerSlot != null)
                {
                    transform.SetParent(null, true);
                    m_LinkedPartner.transform.SetParent(null, true);

                    if (queuePool != null)
                    {
                        queuePool.OnLinkedShipsDispatched(this, m_LinkedPartner);
                    }

                    ShipDispatcher.Instance?.NotifyShipSent();
                    SailToSlot(candidate, m_SnapDuration);
                    m_LinkedPartner.SailToSlot(partnerSlot, 0.35f);
                }
                else
                {
                    // Kuyruk hiyerarşisinden dünya uzayına çıkar
                    transform.SetParent(null, true);

                    // Kuyruk kontrolü: Ön sıradan slota gönderiliyorsa kuyruk yöneticisini bilgilendir
                    if (queuePool != null && queuePool.WaitingShips != null && queuePool.WaitingShips.Contains(this))
                    {
                        if (queuePool.IsFrontRow(this))
                        {
                            queuePool.OnFrontShipDispatched(this);
                        }
                    }

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
            if (IsUiContainer) return;

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

            EnsureChimneyAndSmoke();

            m_Renderers = GetComponentsInChildren<MeshRenderer>(true);
        }

        private void EnsureVisualComponents()
        {
            if (IsUiContainer) return;
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
        public void CreateOrFindBadge()
        {
            if (IsUiContainer)
            {
                if (m_BadgeText == null) m_BadgeText = GetComponentInChildren<TextMeshProUGUI>(true);
                UpdateBadgeText();
                return;
            }

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
                canvasRect.sizeDelta = new Vector2(110f, 62f);
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
            scaler.dynamicPixelsPerUnit = 25;
            scaler.referencePixelsPerUnit = 100;

            m_BadgeCanvasObj = canvasObj;
            m_BadgeConfigured = false;

            // 1. Görseldeki gibi koyu yuvarlak rozet (Dark Rounded Pill Badge)
            Transform pillTr = canvasObj.transform.Find("Badge_Pill");
            GameObject pillObj;
            if (pillTr != null)
            {
                pillObj = pillTr.gameObject;
            }
            else
            {
                pillObj = new GameObject("Badge_Pill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                pillObj.transform.SetParent(canvasObj.transform, false);
            }
            pillObj.transform.SetSiblingIndex(0);
            Image pillImg = pillObj.GetComponent<Image>();
            Sprite pillSprite = null;
#if UNITY_EDITOR
            pillSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Badge_Pill.png");
#endif
            if (pillSprite == null) pillSprite = Resources.Load<Sprite>("Badge_Pill") ?? Resources.Load<Sprite>("UI/Badge_Pill");
            if (pillSprite != null)
            {
                pillImg.sprite = pillSprite;
                pillImg.type = Image.Type.Simple;
                pillImg.color = Color.white;
            }
            else
            {
                pillImg.color = new Color32(30, 37, 56, 255);
            }
            RectTransform pillRect = pillObj.GetComponent<RectTransform>();
            pillRect.sizeDelta = new Vector2(110f, 62f);
            pillRect.pivot = new Vector2(0.5f, 0.5f);
            pillRect.anchoredPosition = Vector2.zero;
            m_BadgeImage = pillImg;

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
            textRect.sizeDelta = new Vector2(110f, 62f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            ApplyBadgeTextPose(textRect);

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

        public void UpdateBadgeText()
        {
            int remaining = DisplayRemainingCapacity;

            if (IsUiContainer)
            {
                if (m_BadgeText == null) m_BadgeText = GetComponentInChildren<TextMeshProUGUI>(true);
                if (m_BadgeText != null)
                {
                    if (Application.isPlaying && (remaining <= 0 || IsFull || m_IsDeparting))
                    {
                        m_BadgeText.gameObject.SetActive(false);
                    }
                    else
                    {
                        m_BadgeText.gameObject.SetActive(true);
                        int countToShow = remaining > 0 ? remaining : m_Capacity;
                        if (countToShow > 0)
                        {
                            m_BadgeText.text = m_IsMysteryHidden ? "?" : countToShow.ToString();
                        }
                        bool isWhiteContainer = m_ShipColor.r > 0.85f && m_ShipColor.g > 0.85f && m_ShipColor.b > 0.85f;
                        m_BadgeText.outlineColor = isWhiteContainer ? new Color(0.62f, 0.62f, 0.65f, 1f) : Color.black;
                    }
                }
                return;
            }

            // Kullanıcı isteği: "textteki sayı dolduğunda text yok olsun"
            // Kapasite dolduğunda (kalan <= 0 veya IsFull) text ve rozet HEMEN yok olur!
            if (remaining <= 0 || IsFull || m_IsDeparting)
            {
                if (m_BadgeCanvasObj != null && m_BadgeCanvasObj.activeSelf)
                    m_BadgeCanvasObj.SetActive(false);
                if (m_BadgeImage != null && m_BadgeImage.gameObject.activeSelf)
                    m_BadgeImage.gameObject.SetActive(false);
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

        #region 💨 Baca & Duman Efekti (Chimney & Gentle Smoke System)

        // İskele (3010, sortingOrder 5) ve slot (3005) görselleri saydam/ZWrite kapalı çizilir; URP saydamları önce
        // sortingOrder'a, sonra kuyruğa göre sıralar. Baca ikisinde de onların önünde olmazsa gemi iskeleye
        // yanaşınca eğik iskele düzlemi bacanın üstüne boyanır.
        private const int ChimneyRenderQueue = 3020;
        private const int ChimneySortingOrder = 20;

        private static Material WithChimneyRenderQueue(Material mat)
        {
            if (mat != null && mat.renderQueue != ChimneyRenderQueue) mat.renderQueue = ChimneyRenderQueue;
            return mat;
        }

        private Material GetChimneyBodyMaterial()
        {
            if (m_ChimneyBodyMaterial != null) return m_ChimneyBodyMaterial;
#if UNITY_EDITOR
            m_ChimneyBodyMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ship_Chimney_Mat.mat");
            if (m_ChimneyBodyMaterial != null) return m_ChimneyBodyMaterial;
#endif
            Shader sh = Shader.Find("Toony Colors Pro 2/PixelGame/Cartoon") ?? Shader.Find("Universal Render Pipeline/Lit");
            m_ChimneyBodyMaterial = new Material(sh);
            m_ChimneyBodyMaterial.name = "Ship_Chimney_Mat_Runtime";
            m_ChimneyBodyMaterial.SetColor("_BaseColor", new Color(0.10f, 0.11f, 0.13f, 1f));
            return m_ChimneyBodyMaterial;
        }

        private Material GetChimneyRimMaterial()
        {
            if (m_ChimneyRimMaterial != null) return m_ChimneyRimMaterial;
#if UNITY_EDITOR
            m_ChimneyRimMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ship_Chimney_Rim_Mat.mat");
            if (m_ChimneyRimMaterial != null) return m_ChimneyRimMaterial;
#endif
            Shader sh = Shader.Find("Toony Colors Pro 2/PixelGame/Cartoon") ?? Shader.Find("Universal Render Pipeline/Lit");
            m_ChimneyRimMaterial = new Material(sh);
            m_ChimneyRimMaterial.name = "Ship_Chimney_Rim_Mat_Runtime";
            m_ChimneyRimMaterial.SetColor("_BaseColor", new Color(0.95f, 0.16f, 0.16f, 1f));
            return m_ChimneyRimMaterial;
        }

        private Material GetChimneySmokeMaterial()
        {
            if (m_ChimneySmokeMaterial != null) return WithSmokeDrawnOverShip(m_ChimneySmokeMaterial);
#if UNITY_EDITOR
            m_ChimneySmokeMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ship_Smoke_Mat.mat");
            if (m_ChimneySmokeMaterial != null) return WithSmokeDrawnOverShip(m_ChimneySmokeMaterial);
#endif
            Shader sh = Shader.Find("PixelGame/CartoonSmokePuff") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            m_ChimneySmokeMaterial = new Material(sh);
            m_ChimneySmokeMaterial.name = "Ship_Smoke_Mat_Runtime";
            if (m_ChimneySmokeMaterial.HasProperty("_BaseColor"))
                m_ChimneySmokeMaterial.SetColor("_BaseColor", new Color(0.96f, 0.98f, 1.0f, 0.75f));
            if (m_ChimneySmokeMaterial.HasProperty("_Softness"))
                m_ChimneySmokeMaterial.SetFloat("_Softness", 0.28f);
            return WithSmokeDrawnOverShip(m_ChimneySmokeMaterial);
        }

        private static readonly Dictionary<Color32, Material> s_CachedChimneyRimMaterials = new Dictionary<Color32, Material>();

        /// <summary>
        /// Baca yakası geminin renginde boyanır (kırmızı yaka yeşil gemide yabancı duruyordu).
        /// Siyah gemide yaka siyah gövdeyle kaybolmasın diye güvertedeki gibi beyaz olur.
        /// </summary>
        private Material GetChimneyRimMaterialForShipColor(Color shipColor)
        {
            Color32 key = (Color32)shipColor;
            key.a = 255;
            if (s_CachedChimneyRimMaterials.TryGetValue(key, out Material cached) && cached != null) return cached;

            // Editörde sahneye kayıtlı yaka materyalini yeniden kullan; her derlemede yenisi üretilip sahne
            // sürekli "değişti" (*) oluyordu (gövde materyalindeki FindExistingBoatMaterial ile aynı sorun)
            string expectedName = $"Ship_Chimney_Rim_Mat_{ColorUtility.ToHtmlStringRGB(shipColor)}";
            if (!Application.isPlaying && s_ReuseSceneBoatMaterials && m_ChimneyTransform != null)
            {
                Transform rimTr = m_ChimneyTransform.Find("Chimney_Rim");
                MeshRenderer rimMr = rimTr != null ? rimTr.GetComponent<MeshRenderer>() : null;
                if (rimMr != null && rimMr.sharedMaterial != null && rimMr.sharedMaterial.name == expectedName)
                {
                    s_CachedChimneyRimMaterials[key] = rimMr.sharedMaterial;
                    return rimMr.sharedMaterial;
                }
            }

            Material baseMat = GetChimneyRimMaterial();
            if (baseMat == null) return null;

            float maxChannel = Mathf.Max(shipColor.r, Mathf.Max(shipColor.g, shipColor.b));
            float luminance = 0.299f * shipColor.r + 0.587f * shipColor.g + 0.114f * shipColor.b;
            bool isBlackShip = maxChannel < 0.28f || luminance < 0.22f;
            Color rimColor = isBlackShip ? new Color(0.96f, 0.97f, 0.99f, 1f) : shipColor;
            rimColor.a = 1f;

            Material mat = new Material(baseMat)
            {
                name = expectedName
            };
            CartoonShader.ApplyColor(mat, rimColor);
            // Gövdeyle aynı mat görünüm: ApplyColor'ın açtığı plastik parlama/specular/kenar ışığı kapatılır
            if (mat.HasProperty("_PlasticHighlightIntensity")) mat.SetFloat("_PlasticHighlightIntensity", 0f);
            if (mat.HasProperty("_PlasticHighlightColor")) mat.SetColor("_PlasticHighlightColor", Color.black);
            if (mat.HasProperty("_SpecularColor")) mat.SetColor("_SpecularColor", Color.black);
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", Color.clear);
            mat.renderQueue = ChimneyRenderQueue;

            s_CachedChimneyRimMaterials[key] = mat;
            return mat;
        }

        // Gizli (mystery) gemide renkli yaka rengi ele vermesin: örtü süresince yaka baca gövdesiyle aynı koyu renktedir
        private Material GetCurrentChimneyRimMaterial()
        {
            return m_IsMysteryHidden
                ? WithChimneyRenderQueue(GetChimneyBodyMaterial())
                : GetChimneyRimMaterialForShipColor(m_ShipColor);
        }

        private void UpdateChimneyRimColor()
        {
            if (m_ChimneyTransform == null) return;
            Transform rimTr = m_ChimneyTransform.Find("Chimney_Rim");
            if (rimTr == null) return;
            MeshRenderer rimMr = rimTr.GetComponent<MeshRenderer>();
            if (rimMr != null) rimMr.sharedMaterial = GetCurrentChimneyRimMaterial();
        }

        // Baca kabin pencere boşluğunun içinde; duman derinlik testine girerse kabin duvarları pufları
        // yarım yamalak keser ve duman geminin içinden geçiyormuş gibi görünür. Duman her zaman gemi üstünde çizilir.
        private static Material WithSmokeDrawnOverShip(Material mat)
        {
            if (mat != null && mat.HasProperty("_ZTest"))
                mat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            return mat;
        }

        public static Vector3 GetChimneyPositionForCorner(ChimneyCorner corner)
        {
            switch (corner)
            {
                case ChimneyCorner.RearLeft:
                    return new Vector3(-0.46f, 2.05f, 1.38f);
                case ChimneyCorner.FrontRight:
                    return new Vector3(0.46f, 2.00f, -1.60f);
                case ChimneyCorner.FrontLeft:
                    return new Vector3(-0.46f, 2.00f, -1.60f);
                case ChimneyCorner.FrontCenter:
                    return new Vector3(0f, 0.732f, -2.193f);
                case ChimneyCorner.RearRight:
                default:
                    return new Vector3(0.46f, 2.05f, 1.38f);
            }
        }

        /// <summary>
        /// Geminin kabin çatısına şık, belirgin bir hypercasual voxel baca ve hafifçe tüten duman partikül sistemi kurar.
        /// Varsayılan olarak kabinin önünde ortada (FrontCenter) konumlandırılır.
        /// </summary>
        public void EnsureChimneyAndSmoke()
        {
#if UNITY_EDITOR
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
#endif
            if (!m_EnableChimneySmoke)
            {
                if (m_ChimneyTransform != null) m_ChimneyTransform.gameObject.SetActive(false);
                return;
            }

            if (m_ChimneyCorner != ChimneyCorner.Custom)
            {
                m_ChimneyLocalPos = GetChimneyPositionForCorner(m_ChimneyCorner);
            }

            Transform parentTarget = m_VisualRoot != null ? m_VisualRoot : transform;

            Transform chimney = parentTarget.Find("[Chimney]");
            if (chimney == null && parentTarget != transform) chimney = transform.Find("[Chimney]");

            if (chimney == null)
            {
                GameObject chGo = new GameObject("[Chimney]");
                chGo.transform.SetParent(parentTarget, false);
                chGo.transform.localPosition = m_ChimneyLocalPos;
                chGo.transform.localRotation = Quaternion.identity;
                chGo.transform.localScale = Vector3.one;
                chimney = chGo.transform;
            }
            else
            {
                if (chimney.parent != parentTarget) chimney.SetParent(parentTarget, false);
                chimney.localPosition = m_ChimneyLocalPos;
                chimney.localRotation = Quaternion.identity;
                chimney.localScale = Vector3.one;
            }

            m_ChimneyTransform = chimney;
            chimney.gameObject.SetActive(true);

            // 1. Baca Gövdesi (Chimney_Body) - Daha yüksek (0.60) ve dolgun (0.36) belirgin voxel gövde
            Transform bodyTr = chimney.Find("Chimney_Body");
            GameObject bodyObj;
            if (bodyTr == null)
            {
                bodyObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bodyObj.name = "Chimney_Body";
                bodyObj.transform.SetParent(chimney, false);
                Collider c = bodyObj.GetComponent<Collider>();
                if (c != null)
                {
                    if (Application.isPlaying) Destroy(c);
                    else DestroyImmediate(c);
                }
            }
            else
            {
                bodyObj = bodyTr.gameObject;
            }
            bodyObj.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            bodyObj.transform.localRotation = Quaternion.identity;
            bodyObj.transform.localScale = new Vector3(0.22f, 0.36f, 0.22f);

            MeshRenderer bodyMr = bodyObj.GetComponent<MeshRenderer>();
            if (bodyMr != null)
            {
                bodyMr.sharedMaterial = WithChimneyRenderQueue(GetChimneyBodyMaterial());
                bodyMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                bodyMr.receiveShadows = false;
                bodyMr.sortingOrder = ChimneySortingOrder;
            }

            // 2. Baca Yakası / Çerçevesi (Chimney_Rim) - Üst ağızda belirgin kırmızı kontrastlı yaka
            Transform rimTr = chimney.Find("Chimney_Rim");
            GameObject rimObj;
            if (rimTr == null)
            {
                rimObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rimObj.name = "Chimney_Rim";
                rimObj.transform.SetParent(chimney, false);
                Collider c = rimObj.GetComponent<Collider>();
                if (c != null)
                {
                    if (Application.isPlaying) Destroy(c);
                    else DestroyImmediate(c);
                }
            }
            else
            {
                rimObj = rimTr.gameObject;
            }
            rimObj.transform.localPosition = new Vector3(0f, 0.38f, 0f);
            rimObj.transform.localRotation = Quaternion.identity;
            rimObj.transform.localScale = new Vector3(0.28f, 0.10f, 0.28f);

            MeshRenderer rimMr = rimObj.GetComponent<MeshRenderer>();
            if (rimMr != null)
            {
                rimMr.sharedMaterial = GetCurrentChimneyRimMaterial();
                rimMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rimMr.receiveShadows = false;
                rimMr.sortingOrder = ChimneySortingOrder;
            }

            // 3. Duman Partikül Sistemi ([Smoke_FX])
            Transform smokeTr = chimney.Find("[Smoke_FX]");
            GameObject smokeObj;
            if (smokeTr == null)
            {
                smokeObj = new GameObject("[Smoke_FX]");
                smokeObj.transform.SetParent(chimney, false);
                smokeTr = smokeObj.transform;
            }
            else
            {
                smokeObj = smokeTr.gameObject;
            }
            smokeTr.localPosition = new Vector3(0f, 0.44f, 0f);
            smokeTr.localRotation = Quaternion.Euler(-90f, 0f, 0f); // Yukarı doğru
            smokeTr.localScale = Vector3.one;

            ParticleSystem ps = smokeObj.GetComponent<ParticleSystem>();
            if (ps == null) ps = smokeObj.AddComponent<ParticleSystem>();
            m_ChimneySmokePS = ps;

            SetupChimneySmokeSystem(ps);
        }

        private void SetupChimneySmokeSystem(ParticleSystem ps)
        {
            if (ps == null) return;

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1.45f;
            main.startSpeed = 0.65f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.24f); // Başlangıçta daha belirgin ve net
            main.gravityModifier = -0.07f; // Sıcak hava pufu yukarı yükselir
            main.maxParticles = 65;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 2.8f; // Akıcı ve tatlı rölanti dumanı

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 6.5f;
            shape.radius = 0.045f;

            // Yükseldikçe sevimli pamuk bulut gibi genişleme (Size Over Lifetime)
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(new Keyframe(0.0f, 1.0f, 0.0f, 2.0f));
            sizeCurve.AddKey(new Keyframe(1.0f, 3.2f, 1.2f, 0.0f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            // Yumuşakça belirip gökyüzünde kaybolma (Color & Alpha Over Lifetime)
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.96f, 0.98f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.93f, 0.95f, 0.98f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.78f, 0.12f),
                    new GradientAlphaKey(0.60f, 0.65f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorOverLifetime.color = grad;

            // Hafif rüzgar / salınım esintisi (Velocity Over Lifetime)
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0.04f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.material = GetChimneySmokeMaterial();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.sortingOrder = ChimneySortingOrder + 1;
            }

            if (!ps.isPlaying && gameObject.activeInHierarchy)
            {
                ps.Play();
            }
        }

        /// <summary>
        /// Gemi seyir halindeyken duman atımını hafifçe artırır (1.8x), durduğunda rölantiye döner (1.0x).
        /// </summary>
        public void SetSmokeEmissionMultiplier(float multiplier)
        {
            if (m_ChimneySmokePS == null) return;
            var emission = m_ChimneySmokePS.emission;
            emission.rateOverTime = 2.8f * Mathf.Max(0.1f, multiplier);
        }

#if UNITY_EDITOR
        [ContextMenu("💨 Baca ve Duman Efektini Yenile")]
        public void RebuildChimneyAndSmoke()
        {
            EnsureChimneyAndSmoke();
        }
#endif

        #endregion
    }
}

