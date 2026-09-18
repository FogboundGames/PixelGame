using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// Kamyon ve vagon döngüsünün merkezi.
    ///
    /// İki çalışma modunu destekler:
    /// 1) Hareketli Tren Döngüsü (Continuous Train Conveyor - Aktif):
    ///    - Vagonlar ray hattı boyunca soldan sağa sürekli ilerler (sağ tünelden çıkıp sol tünelden girer).
    ///    - Tıklanan küpler kırılıp doğrudan çerçevenin altındaki kırmızı rafa (Shelf) düşer ve orada birikir.
    ///    - Raydan geçen eşleşen renkteki vagonlar tablonun altından geçerken raftaki parçalar kavisle vagona akar.
    ///    - Kasa dolduğunda vagon sağ tünelden geçerek teslimatı tamamlar ve soldan yeni renkle girer.
    ///
    /// 2) Klasik Slot & Havuz Modu:
    ///    - Eski park yeri ve bekleme havuzu mekaniği.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Truck Dispatcher")]
    public class TruckDispatcher : MonoBehaviour
    {
        private static TruckDispatcher s_Instance;
        public static TruckDispatcher Instance => s_Instance;

        [Header("🔗 Bağlantılar")]
        [SerializeField] private TruckSlotRow m_Slots;
        [SerializeField] private TruckPool m_Pool;
        [SerializeField] private GameObject m_TruckPrefab;
        [SerializeField] private PixelArtGenerator m_Generator;

        [Header("🔄 Mavi Çerçeve Etrafında Dönen Ray Döngüsü (Perimeter Track Loop)")]
        [Tooltip("Vagonların mavi çerçevenin 4 kenarı boyunca kesintisiz dolaşmasını sağlar.")]
        [SerializeField] private bool m_PerimeterTrain = true;
        public bool PerimeterTrain { get => m_PerimeterTrain; set => m_PerimeterTrain = value; }

        [Tooltip("Vagonların döngü üzerindeki seyir hızı (dünya birimi / saniye).")]
        [Range(0.5f, 6.0f)]
        [SerializeField] private float m_PerimeterSpeed = 2.1f;

        [Tooltip("Ray döngüsünün çerçevenin sol ve sağ dışına olan yatay mesafesi (dünya birimi).")]
        [SerializeField] private float m_LoopMarginX = 0.32f;

        [Tooltip("Ray döngüsünün çerçevenin alt ve üst dışına olan dikey mesafesi (dünya birimi).")]
        [SerializeField] private float m_LoopMarginY = 0.36f;

        [Tooltip("Ray döngüsünün 4 köşesindeki yuvarlama yarıçapı.")]
        [SerializeField] private float m_LoopCornerRadius = 0.45f;

        [Tooltip("Vagonların yerdeki parçaları vakumla çekebileceği maksimum mesafe (dünya birimi).")]
        [SerializeField] private float m_VacuumRadius = 2.4f;

        [Tooltip("Vakum çekiminin uçuş süresi (saniye).")]
        [SerializeField] private float m_VacuumDuration = 0.28f;

        [Tooltip("Vakum çekiminin kavis yüksekliği.")]
        [SerializeField] private float m_VacuumArcHeight = 0.35f;

        [Tooltip("Parçaların kırıldığında oldukları yere dökülüş süresi.")]
        [SerializeField] private float m_LocalDropFallDuration = 0.24f;

        [Tooltip("Kırılan parçaların küp merkezinden dışa saçılma yarıçap katsayısı (küp sınırlarına göre).")]
        [SerializeField] private float m_LocalDropScatterRadius = 1.35f;

        [Tooltip("Parçaların kırıntı gibi farklı boyutlarda olması için rastgele ölçek varyasyonu.")]
        [SerializeField] private float m_LocalDropScaleVariation = 0.35f;

        [Tooltip("Parçaların ilk saçılma anında kameraya doğru kabarma/yay yüksekliği.")]
        [SerializeField] private float m_LocalDropArcHeight = 0.15f;

        [Header("🧹 Raydan Geçerken Süpürme (Proximity Sweeping)")]
        [Tooltip("Vagon rayda ilerlerken ne kadar mesafedeki küpleri süpürebilir (dünya birimi).")]
        [Range(1.0f, 4.0f)]
        [SerializeField] private float m_SweepRadius = 2.2f;
        public float SweepRadius { get => m_SweepRadius; set => m_SweepRadius = value; }

        [Tooltip("Vagonun ardışık küpleri süpürme sıklığı/aralığı (saniye).")]
        [Range(0.05f, 0.50f)]
        [SerializeField] private float m_SweepInterval = 0.16f;
        public float SweepInterval { get => m_SweepInterval; set => m_SweepInterval = value; }

        [Tooltip("Çerçeve etrafına 3D ray prefab'ı döşensin mi?")]
        [SerializeField] private bool m_ShowPerimeterRails = true;

        [Tooltip("Ray parçası prefab'ı (boşsa Assets/Prefabs/Track.prefab otomatik yüklenir).")]
        [SerializeField] private GameObject m_TrackPrefab;

        [Header("🚂 Hareketli Ray Vagonları (Continuous Train Conveyor - Eski Hat)")]
        [Tooltip("Vagonların alt ray üzerinde sürekli hareket etmesini sağlar (sağdan çıkıp soldan girer).")]
        [SerializeField] private bool m_ContinuousTrain = false;

        [Tooltip("Vagonların ray üzerindeki seyir hızı (şerit birimi / saniye)")]
        [Range(40f, 600f)]
        [SerializeField] private float m_TrainSpeed = 380f;

        [Tooltip("Vagonların sol tünel giriş koordinatı (X)")]
        [SerializeField] private float m_PortalLeftX = -1080f;

        [Tooltip("Vagonların sağ tünel çıkış koordinatı (X)")]
        [SerializeField] private float m_PortalRightX = 1080f;

        [Tooltip("Raftan vagona parça çekiminin aktif olduğu X aralığı (tablonun altı)")]
        [SerializeField] private Vector2 m_PickupZoneX = new Vector2(-750f, 750f);

        [Header("🎯 Kurallar")]
        [Tooltip("Açıkken küp ancak rengine uyan bir kamyon slotta varsa patlar. " +
                 "Kapalıyken her küp patlar (eski serbest davranış).")]
        [SerializeField] private bool m_RequireMatchingTruck = false;

        [Tooltip("Kamyon rengi ile palet rengi arasındaki tolerans.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float m_ColorThreshold = 0.04f;

        [Tooltip("Bölüm verisinde kamyon ayarı yoksa kullanılacak yedek kapasite.")]
        [Min(1)]
        [SerializeField] private int m_FallbackTruckCapacity = 16;

        [Header("📦 Küpün Parçalanıp Vagona Dolması")]
        [Tooltip("Bir küpün en az kaç parçaya bölüneceği")]
        [Min(1)]
        [SerializeField] private int m_MinPieces = 2;

        [Tooltip("Bir küpün en fazla kaç parçaya bölüneceği")]
        [Min(1)]
        [SerializeField] private int m_MaxPieces = 4;

        [Tooltip("Parça boyutlarının değişim aralığı (temel boyuta göre çarpan).")]
        [SerializeField] private Vector2 m_PieceSizeRange = new Vector2(0.55f, 1.5f);

        [Tooltip("Parçaların küpün çevresinden ne kadar dağınık kopacağı (dünya birimi)")]
        [Min(0f)]
        [SerializeField] private float m_PieceSpread = 0.06f;

        [Header("📦 Kırmızı Raf: Altta Birikme & Vagona Akma (DOTween)")]
        [Tooltip("Parçaların küpten alt rafa (kırmızı işaretli alan) düşüş süresi")]
        [Min(0.1f)]
        [SerializeField] private float m_FallToShelfDuration = 0.38f;

        [Tooltip("Parçaların raftan vagona akış / uçuş süresi")]
        [Min(0.1f)]
        [SerializeField] private float m_FlowToCartDuration = 0.20f;

        [Tooltip("Raftan vagona uçuş kavis yüksekliği")]
        [SerializeField] private float m_FlowArcHeight = 0.65f;

        [Tooltip("Parçaların raftan vagona akarken aralarındaki akış gecikmesi (şelale efekti)")]
        [Range(0.01f, 0.2f)]
        [SerializeField] private float m_FlowStaggerDelay = 0.05f;

        [Tooltip("Raf dikey ince ayarı (Y ofseti)")]
        [SerializeField] private float m_ShelfYOffset = 0f;

        [Tooltip("Rafın yatay yayılma genişlik çarpanı")]
        [Range(0.2f, 1.2f)]
        [SerializeField] private float m_ShelfWidthFactor = 0.75f;

        [Tooltip("Rafta biriken parçaların yatay saçılma yarıçapı")]
        [SerializeField] private float m_ShelfScatterX = 0.45f;
        public float ShelfScatterX => m_ShelfScatterX;

        [Tooltip("Parçaların çerçevenin alt orta kısmında (kırmızı elips alanı) toplanma yarıçapı")]
        [SerializeField] private float m_ShelfGatherRadius = 0.32f;

        [Tooltip("Parçaların düştükleri yerden orta toplanma alanına doğru hafifçe çekilme süresi")]
        [Min(0.05f)]
        [SerializeField] private float m_PullToCenterDuration = 0.22f;

        [Tooltip("Rafta biriken parçaların dikey rastgele yığılma yüksekliği")]
        [SerializeField] private float m_ShelfStackHeight = 0.08f;

        [Tooltip("İsteğe bağlı özel raf hedef transformu (boşsa çerçevenin altından otomatik hesaplanır)")]
        [SerializeField] private Transform m_CustomShelfAnchor;

        [Header("🚚 Klasik Mod Geçiş & Kalkış")]
        [SerializeField] private float m_MoveDuration = 0.4f;
        [SerializeField] private float m_MoveArc = -120f;
        [SerializeField] private float m_DepartSpeed = 900f;
        [SerializeField] private float m_DepartAccelTime = 0.9f;
        [SerializeField] private float m_DepartExtraDistance = 600f;
        [SerializeField] private float m_DepartDelay = 0.35f;

        [Header("⛏️ Madenci (Miner) Sistemi")]
        [Tooltip("Vagonlardan çıkacak madenci karakter prefab'ı (boşsa geçici kapsül veya MechaMiner kullanılır).")]
        [SerializeField] private GameObject m_MinerPrefab;

        [Tooltip("Madencilerin vagon ölçeğine göre genel boyut çarpanı.")]
        [SerializeField] private float m_MinerScaleFactor = 1.15f;

        [Tooltip("Madencilerin vagondan sırayla atlama gecikmesi (saniye).")]
        [SerializeField] private float m_MinerSpawnDelay = 0.45f;

        [Tooltip("Madencilerin küplere koşu hızı (yavaşlatılmış ve dengelenmiş).")]
        [SerializeField] private float m_MinerRunSpeed = 0.85f;

        [Header("👷 Madenci (Miner)")]
        [Tooltip("Madencilerin vagonlara binmesi, rayda inip küplere koşması ve kırması. Kapalıyken küpler vagon slota oturduğunda cam gibi kırılarak doğrudan vagona akar.")]
        [SerializeField] private bool m_EnableMiners = false;
        public bool EnableMiners { get => m_EnableMiners; set => m_EnableMiners = value; }

        [Header("💎 Sıralı Cam Kırılma Efekti (Slot Modu)")]
        [Tooltip("Vagon slota oturduğunda küplerin sırayla kırılma aralığı (saniye - daha yavaş ve belirgin)")]
        [Range(0.08f, 0.50f)]
        [SerializeField] private float m_ShatterInterval = 0.22f;

        [Tooltip("Parçaların rafta kısa bekleme/birikme süresi")]
        [Range(0.05f, 0.4f)]
        [SerializeField] private float m_PauseOnShelfDuration = 0.14f;

        [Tooltip("Parçaların raftan vagona kayarak akış süresi")]
        [Range(0.2f, 0.8f)]
        [SerializeField] private float m_SlideToWagonDuration = 0.38f;

        [Tooltip("Parçaların belirginlik ölçek çarpanı")]
        [Range(1.0f, 2.0f)]
        [SerializeField] private float m_ShardScaleMultiplier = 1.35f;

        private static readonly HashSet<PixelCube> s_ReservedCubes = new HashSet<PixelCube>();
        public static void ClearReservedCubes() => s_ReservedCubes.Clear();

        public GameObject MinerPrefab
        {
            get
            {
                if (m_MinerPrefab != null) return m_MinerPrefab;

                #if UNITY_EDITOR
                m_MinerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MechaMiner.prefab");
                if (m_MinerPrefab != null) return m_MinerPrefab;
                #endif

                return m_MinerPrefab;
            }
        }

        #region 🚂 Hareketli Vagon Sınıfı & Veri Yapıları

        [System.Serializable]
        public class MovingWagon
        {
            public GameObject GameObject;
            public Transform Transform;
            public TruckCargo Cargo;
            public TruckPaint Paint;
            public MineCartMover Mover;
            public Animator Animator;
            public float PositionX;
            public float DistanceOnLoop;
            public bool IsDeployingMiners = false;
            public bool HasStartedDeploying = false;
            public bool HasCompletedInitialDeployment = false;
            public bool IsJumpingToTrack = false;
            public bool IsDeparting = false;
            public float NextSweepTime = 0f;
            public int PendingSweeps = 0;
            public int SweepComboCount = 0;
        }

        public class BoardShardGroup
        {
            public int CubeId;
            public Color Color;
            public Vector3 Position;
            public List<CargoFlyer> Flyers = new List<CargoFlyer>();
            public bool IsVacuuming = false;
            public bool IsSettled = false;
        }

        public class ShelfCubeGroup
        {
            public int CubeId;
            public Color Color;
            public int TotalFragments;
            public List<CargoFlyer> Flyers = new List<CargoFlyer>();
            public List<float> SizeFactors = new List<float>();
            public bool IsFlowing;
            public bool IsReady => Flyers.Count >= TotalFragments;
        }

        /// <summary>
        /// Mavi çerçevenin etrafındaki 4 kenarlı yuvarlatılmış kapalı ray döngüsü matematiği.
        /// </summary>
        public class PerimeterTrackLoop
        {
            public Vector3 Center;
            public float OuterWidth;
            public float OuterHeight;
            public float CornerRadius;
            public float Z;

            public float LeftX;
            public float RightX;
            public float BottomY;
            public float TopY;

            public float StraightBottom;
            public float CornerBottomRight;
            public float StraightRight;
            public float CornerTopRight;
            public float StraightTop;
            public float CornerTopLeft;
            public float StraightLeft;
            public float CornerBottomLeft;

            public float TotalPerimeter;

            public void Setup(Vector3 center, float width, float height, float cornerRadius, float z)
            {
                Center = center;
                OuterWidth = Mathf.Max(0.5f, width);
                OuterHeight = Mathf.Max(0.5f, height);
                Z = z;

                LeftX = center.x - OuterWidth * 0.5f;
                RightX = center.x + OuterWidth * 0.5f;
                BottomY = center.y - OuterHeight * 0.5f;
                TopY = center.y + OuterHeight * 0.5f;

                CornerRadius = Mathf.Clamp(cornerRadius, 0.05f, Mathf.Min(OuterWidth, OuterHeight) * 0.45f);

                float r = CornerRadius;
                StraightBottom = Mathf.Max(0.1f, (RightX - r) - (LeftX + r));
                StraightRight = Mathf.Max(0.1f, (TopY - r) - (BottomY + r));
                StraightTop = StraightBottom;
                StraightLeft = StraightRight;

                float cornerArc = 0.5f * Mathf.PI * r;
                CornerBottomRight = cornerArc;
                CornerTopRight = cornerArc;
                CornerTopLeft = cornerArc;
                CornerBottomLeft = cornerArc;

                TotalPerimeter = (StraightBottom + StraightRight + StraightTop + StraightLeft) + (4f * cornerArc);
            }

            public void Evaluate(float distance, out Vector3 position, out Vector3 tangent, out Quaternion rotation, Quaternion baseRotation)
            {
                if (TotalPerimeter <= 0.001f)
                {
                    position = Center;
                    tangent = Vector3.right;
                    rotation = baseRotation;
                    return;
                }

                float s = Mathf.Repeat(distance, TotalPerimeter);
                float r = CornerRadius;

                // Saat yönünün tersi (Counter-Clockwise döngü):
                // 1. Alt kenar (Soldan sağa): +X
                if (s < StraightBottom)
                {
                    float t = s / StraightBottom;
                    position = new Vector3(Mathf.Lerp(LeftX + r, RightX - r, t), BottomY, Z);
                    tangent = Vector3.right;
                }
                // 2. Sağ-Alt köşe yayı: -90° -> 0°
                else if (s < StraightBottom + CornerBottomRight)
                {
                    float ds = s - StraightBottom;
                    float angle = Mathf.Lerp(-90f, 0f, ds / CornerBottomRight) * Mathf.Deg2Rad;
                    Vector3 cornerCenter = new Vector3(RightX - r, BottomY + r, Z);
                    position = cornerCenter + new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);
                    tangent = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0f).normalized;
                }
                // 3. Sağ kenar (Aşağıdan yukarıya): +Y
                else if (s < StraightBottom + CornerBottomRight + StraightRight)
                {
                    float ds = s - (StraightBottom + CornerBottomRight);
                    float t = ds / StraightRight;
                    position = new Vector3(RightX, Mathf.Lerp(BottomY + r, TopY - r, t), Z);
                    tangent = Vector3.up;
                }
                // 4. Sağ-Üst köşe yayı: 0° -> 90°
                else if (s < StraightBottom + CornerBottomRight + StraightRight + CornerTopRight)
                {
                    float ds = s - (StraightBottom + CornerBottomRight + StraightRight);
                    float angle = Mathf.Lerp(0f, 90f, ds / CornerTopRight) * Mathf.Deg2Rad;
                    Vector3 cornerCenter = new Vector3(RightX - r, TopY - r, Z);
                    position = cornerCenter + new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);
                    tangent = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0f).normalized;
                }
                // 5. Üst kenar (Sağdan sola): -X
                else if (s < StraightBottom + CornerBottomRight + StraightRight + CornerTopRight + StraightTop)
                {
                    float ds = s - (StraightBottom + CornerBottomRight + StraightRight + CornerTopRight);
                    float t = ds / StraightTop;
                    position = new Vector3(Mathf.Lerp(RightX - r, LeftX + r, t), TopY, Z);
                    tangent = Vector3.left;
                }
                // 6. Sol-Üst köşe yayı: 90° -> 180°
                else if (s < StraightBottom + CornerBottomRight + StraightRight + CornerTopRight + StraightTop + CornerTopLeft)
                {
                    float ds = s - (StraightBottom + CornerBottomRight + StraightRight + CornerTopRight + StraightTop);
                    float angle = Mathf.Lerp(90f, 180f, ds / CornerTopLeft) * Mathf.Deg2Rad;
                    Vector3 cornerCenter = new Vector3(LeftX + r, TopY - r, Z);
                    position = cornerCenter + new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);
                    tangent = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0f).normalized;
                }
                // 7. Sol kenar (Yukarıdan aşağıya): -Y
                else if (s < StraightBottom + CornerBottomRight + StraightRight + CornerTopRight + StraightTop + CornerTopLeft + StraightLeft)
                {
                    float ds = s - (StraightBottom + CornerBottomRight + StraightRight + CornerTopRight + StraightTop + CornerTopLeft);
                    float t = ds / StraightLeft;
                    position = new Vector3(LeftX, Mathf.Lerp(TopY - r, BottomY + r, t), Z);
                    tangent = Vector3.down;
                }
                // 8. Sol-Alt köşe yayı: 180° -> 270°
                else
                {
                    float ds = s - (StraightBottom + CornerBottomRight + StraightRight + CornerTopRight + StraightTop + CornerTopLeft + StraightLeft);
                    float angle = Mathf.Lerp(180f, 270f, ds / CornerBottomLeft) * Mathf.Deg2Rad;
                    Vector3 cornerCenter = new Vector3(LeftX + r, BottomY + r, Z);
                    position = cornerCenter + new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);
                    tangent = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0f).normalized;
                }

                float yaw = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
                rotation = Quaternion.AngleAxis(yaw, Vector3.forward) * baseRotation;
            }
        }

        private readonly List<MovingWagon> m_MovingWagons = new List<MovingWagon>();
        private readonly List<ShelfCubeGroup> m_ShelfCubes = new List<ShelfCubeGroup>();
        private readonly List<BoardShardGroup> m_BoardShards = new List<BoardShardGroup>();
        private readonly PerimeterTrackLoop m_Loop = new PerimeterTrackLoop();
        private int m_NextCubeId = 0;
        private Transform m_WagonsRoot;
        private float m_NextCascadeTime = 0f;

        [Header("🚂 Hat ve Havuz Ayarları")]
        [Tooltip("Ray üzerinde aynı anda dolaşabilecek maksimum vagon sayısı")]
        [SerializeField] private int m_MaxTrackWagons = 5;

        [Tooltip("Raydaki vagonlar arasındaki minimum takip mesafesi")]
        [SerializeField] private float m_MinWagonSpacing = 360f;

        private struct TruckOrder
        {
            public Color Color;
            public int Capacity;
        }

        private readonly Queue<TruckOrder> m_Queue = new Queue<TruckOrder>();
        private readonly Dictionary<Transform, Coroutine> m_Moving = new Dictionary<Transform, Coroutine>();

        public bool RequireMatchingTruck { get => m_RequireMatchingTruck; set => m_RequireMatchingTruck = value; }
        public int QueuedTruckCount => m_Queue.Count;
        public IReadOnlyList<MovingWagon> MovingWagons => m_MovingWagons;
        public PerimeterTrackLoop TrackLoop => m_Loop;
        public Transform WagonsRoot => m_WagonsRoot;

        #endregion

        private void Awake()
        {
            s_Instance = this;
            DOTween.SetTweensCapacity(2000, 500);
        }

        private void OnEnable()
        {
            PixelArtGenerator.LevelLoaded -= OnLevelLoaded;
            PixelArtGenerator.LevelLoaded += OnLevelLoaded;
        }

        private void OnDisable()
        {
            PixelArtGenerator.LevelLoaded -= OnLevelLoaded;
            ClearMovingTrain();
        }

        private void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
            ClearMovingTrain();
        }

        private void Start()
        {
            if (!Application.isPlaying) return;
            StartCoroutine(RebuildNextFrame());
        }

        private IEnumerator RebuildNextFrame()
        {
            yield return null;
            Rebuild();
        }

        private void OnLevelLoaded(PixelLevelData level)
        {
            if (level != null && level.SlotCount > 0)
            {
                m_MaxTrackWagons = level.SlotCount;
            }
            if (!Application.isPlaying) return;
            Rebuild();
        }

        /// <summary>
        /// Kamyon ve vagon sistemini aktif bölüm verisine göre baştan kurar.
        /// Oyuncu havuzdaki vagonları seçerek hatta sürer.
        /// </summary>
        public void Rebuild()
        {
            StopAllCoroutines();
            m_Moving.Clear();
            m_ShelfCubes.Clear();
            m_BoardShards.Clear();
            ClearReservedCubes();
            ClearSlots();
            ClearMovingTrain();

            if (m_Pool != null) m_Pool.gameObject.SetActive(true);

            SetupPerimeterLoop();
            SpawnMovingTrainRoot();
            ClearPool();
            RebuildStrips();
            BuildQueue();
            RefillPool();
            if (m_Pool != null) m_Pool.UpdateRowVisuals();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            if (m_PerimeterTrain)
            {
                UpdateMovingTrain();
                UpdateWagonProximitySweeping();
                UpdateVacuumSuction();
            }
            else if (m_ContinuousTrain)
            {
                UpdateMovingTrain();
                UpdateShelfCascadeFlow();
            }
        }

        #region 🚂 Hareketli Vagon Döngüsü (Continuous Train)

        private void ClearMovingTrain()
        {
            Miner.ClearAllActiveMiners();

            for (int i = 0; i < m_MovingWagons.Count; i++)
            {
                if (m_MovingWagons[i] != null && m_MovingWagons[i].GameObject != null)
                {
                    Destroy(m_MovingWagons[i].GameObject);
                }
            }
            m_MovingWagons.Clear();
            // m_WagonsRoot asla silinmez; sahnedeki raylar ve hiyerarşi korunur!
        }

        private float m_WagonY = 0f;
        private float m_WagonZ = -22f;
        private Quaternion m_WagonRotation = Quaternion.identity;
        private Vector3 m_WagonScale = Vector3.one * 0.38f;

        public bool CalculatePerimeterLoopBounds(out Vector3 loopCenter, out float loopWidth, out float loopHeight)
        {
            loopCenter = new Vector3(0f, 2.5f, 0f);
            loopWidth = 4.2f;
            loopHeight = 4.6f;

            if (m_Generator == null)
            {
                m_Generator = Object.FindFirstObjectByType<PixelArtGenerator>();
            }

            Camera cam = (m_Generator != null && m_Generator.WorldCamera != null) ? m_Generator.WorldCamera : Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();

            if (m_Generator != null)
            {
                // 1. Sahnedeki mevcut küplerin dünya sınırlarını hesapla (En güvenilir ve hatasız yöntem)
                if (m_Generator.CubesContainer != null && m_Generator.CubesContainer.childCount > 0)
                {
                    Bounds bounds = default;
                    bool hasCube = false;
                    foreach (Transform child in m_Generator.CubesContainer)
                    {
                        if (child == null || !child.gameObject.activeSelf) continue;
                        Vector3 pos = child.position;
                        if (!hasCube)
                        {
                            bounds = new Bounds(pos, Vector3.one * 0.16f);
                            hasCube = true;
                        }
                        else
                        {
                            bounds.Encapsulate(pos);
                        }
                    }

                    if (hasCube && bounds.size.x > 0.5f && bounds.size.y > 0.5f)
                    {
                        loopCenter = bounds.center;
                        loopCenter.z = m_Generator.TargetZ;
                        loopWidth = bounds.size.x + (m_LoopMarginX * 2f) + 0.45f;
                        loopHeight = bounds.size.y + (m_LoopMarginY * 2f) + 0.45f;
                        return true;
                    }
                }

                RectTransform frameRect = m_Generator.TargetFrameRect;
                if (frameRect == null)
                {
                    GameObject mp = GameObject.Find("MainPlane");
                    if (mp != null) frameRect = mp.GetComponent<RectTransform>();
                }

                if (frameRect != null && cam != null)
                {
                    Canvas.ForceUpdateCanvases();

                    Canvas canvas = frameRect.GetComponentInParent<Canvas>();
                    Vector3[] corners = new Vector3[4];
                    frameRect.GetWorldCorners(corners);

                    float targetZ = m_Generator.TargetZ;
                    float camDist = Mathf.Abs(cam.transform.position.z - targetZ);
                    if (camDist < 0.1f) camDist = 10f;

                    if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            corners[i] = cam.ScreenToWorldPoint(new Vector3(corners[i].x, corners[i].y, camDist));
                        }
                    }
                    else
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            Vector3 screenPoint = cam.WorldToScreenPoint(corners[i]);
                            corners[i] = cam.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, camDist));
                        }
                    }

                    Vector3 bottomLeft = corners[0];
                    Vector3 topRight = corners[2];

                    loopCenter = (bottomLeft + topRight) * 0.5f;
                    loopCenter.z = targetZ;

                    float rawWidth = Mathf.Abs(topRight.x - bottomLeft.x);
                    float rawHeight = Mathf.Abs(topRight.y - bottomLeft.y);

                    if (rawWidth > 0.5f && rawHeight > 0.5f)
                    {
                        loopWidth = rawWidth + (m_LoopMarginX * 2f);
                        loopHeight = rawHeight + (m_LoopMarginY * 2f);
                        return true;
                    }
                }

                // Fallback: pixel art generator'ın küp sınırları
                if (cam != null && m_Generator.CalculateTargetWorldBounds(cam, out Vector3 wCenter, out float wWidth, out float wHeight))
                {
                    loopCenter = wCenter;
                    loopCenter.z = m_Generator.TargetZ;
                    loopWidth = wWidth + (m_LoopMarginX * 2f) + 0.35f;
                    loopHeight = wHeight + (m_LoopMarginY * 2f) + 0.35f;
                    return true;
                }
            }

            return false;
        }

        [ContextMenu("🛤️ Çevresel Döngüyü Güncelle")]
        public void SetupPerimeterLoop()
        {
            CalculatePerimeterLoopBounds(out Vector3 center, out float width, out float height);
            float targetZ = (m_Generator != null) ? m_Generator.TargetZ : 0f;
            m_Loop.Setup(center, width, height, m_LoopCornerRadius, targetZ - 0.12f);
        }

        [ContextMenu("🛤️ Çevresel Rayları Oluştur")]
        public void GeneratePerimeterRails()
        {
            if (!m_ShowPerimeterRails) return;

            if (m_TrackPrefab == null)
            {
                #if UNITY_EDITOR
                m_TrackPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Track.prefab");
                #endif
            }
            if (m_TrackPrefab == null) return;

            SetupPerimeterLoop();

            if (m_WagonsRoot == null)
            {
                GameObject rootObj = GameObject.Find("[PerimeterWagonsRoot]");
                if (rootObj == null)
                {
                    rootObj = new GameObject("[PerimeterWagonsRoot]");
                    #if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        UnityEditor.Undo.RegisterCreatedObjectUndo(rootObj, "Perimeter Wagons Root");
                    }
                    #endif
                }
                m_WagonsRoot = rootObj.transform;
            }

            Transform existing = m_WagonsRoot.Find("PerimeterRails");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }

            GameObject railsGroup = new GameObject("PerimeterRails");
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.RegisterCreatedObjectUndo(railsGroup, "Perimeter Rails");
            }
            #endif
            railsGroup.transform.SetParent(m_WagonsRoot, false);

            float trackStep = 0.35f;
            int count = Mathf.Max(12, Mathf.RoundToInt(m_Loop.TotalPerimeter / trackStep));
            float actualStep = m_Loop.TotalPerimeter / count;

            Vector3 railScale = (m_WagonScale.sqrMagnitude > 0.01f && m_WagonScale.x < 5f)
                ? m_WagonScale * 0.95f
                : Vector3.one * 0.40f;

            for (int i = 0; i < count; i++)
            {
                float s = i * actualStep;
                m_Loop.Evaluate(s, out Vector3 pos, out Vector3 tangent, out Quaternion rot, m_WagonRotation);

                GameObject rail = Instantiate(m_TrackPrefab, railsGroup.transform);
                rail.name = $"Track_{i}";
                rail.transform.position = pos;
                rail.transform.rotation = rot;
                rail.transform.localScale = railScale;
            }

            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(gameObject);
                if (m_WagonsRoot != null) UnityEditor.EditorUtility.SetDirty(m_WagonsRoot.gameObject);
            }
            #endif
        }

        private void CalibratePortalsAndAlignment()
        {
            Vector3 defaultEuler = (m_Slots != null && m_Slots.Style != null) ? m_Slots.Style.truckEuler : new Vector3(0f, -90f, -270f);
            m_WagonRotation = Quaternion.Euler(defaultEuler);

            if (m_Pool != null && m_Pool.Places != null && m_Pool.Places.Count > 0 && m_Pool.Places[0] != null && m_Pool.Places[0].Truck != null)
            {
                m_WagonScale = m_Pool.Places[0].Truck.lossyScale;
            }
            else if (m_Slots != null && m_Slots.Slots != null && m_Slots.Slots.Count > 0 && m_Slots.Slots[0] != null && m_TruckPrefab != null)
            {
                TruckSlot refSlot = m_Slots.Slots[0];
                GameObject sampleObj = Instantiate(m_TruckPrefab, refSlot.SlotRect);
                sampleObj.name = "SampleWagon";
                refSlot.AssignTruck(sampleObj.transform, Color.white);

                m_WagonScale = sampleObj.transform.lossyScale;
                m_WagonY = sampleObj.transform.localPosition.y;
                m_WagonZ = sampleObj.transform.localPosition.z;

                refSlot.ReleaseTruck();
                if (Application.isPlaying) Destroy(sampleObj);
                else DestroyImmediate(sampleObj);
            }
            else
            {
                m_WagonScale = Vector3.one * 0.38f;
            }

            if (m_WagonScale.sqrMagnitude < 0.01f || m_WagonScale.x > 5f)
            {
                m_WagonScale = Vector3.one * 0.38f;
            }

            if (m_Slots != null)
            {
                Transform leftPortal = m_Slots.transform.Find("Portal_Left");
                Transform rightPortal = m_Slots.transform.Find("Portal_Right");
                if (leftPortal != null && rightPortal != null)
                {
                    m_PortalLeftX = leftPortal.localPosition.x - 100f;
                    m_PortalRightX = rightPortal.localPosition.x + 100f;
                }
            }
        }

        private void SpawnMovingTrainRoot()
        {
            if (m_Slots == null)
            {
                m_Slots = Object.FindFirstObjectByType<TruckSlotRow>();
            }

            SetupPerimeterLoop();

            if (m_PerimeterTrain)
            {
                if (m_Slots != null)
                {
                    m_Slots.gameObject.SetActive(false);
                }
            }

            CalibratePortalsAndAlignment();

            if (m_PerimeterTrain)
            {
                // Sahnedeki mevcut kökü koru! Asla silip sıfırlama!
                Transform existing = m_WagonsRoot;
                if (existing == null)
                {
                    GameObject existingObj = GameObject.Find("[PerimeterWagonsRoot]");
                    if (existingObj != null) existing = existingObj.transform;
                }

                if (existing != null)
                {
                    m_WagonsRoot = existing;
                }
                else
                {
                    GameObject rootObj = new GameObject("[PerimeterWagonsRoot]");
                    if (m_Generator != null)
                    {
                        rootObj.transform.SetParent(m_Generator.transform, false);
                    }
                    else
                    {
                        rootObj.transform.position = Vector3.zero;
                    }
                    m_WagonsRoot = rootObj.transform;
                }

                // Ray kontrolü: Kullanıcı sahnede rayları sildiyse Play'e basınca hortlatma!
                // Sadece sahnede zaten raylar varsa (PerimeterRails) korunur.
            }
            else
            {
                if (m_Slots == null) return;
                Transform existing = m_Slots.transform.Find("MovingWagons");
                if (existing != null)
                {
                    if (Application.isPlaying) Destroy(existing.gameObject);
                    else DestroyImmediate(existing.gameObject);
                }

                GameObject rootObj = new GameObject("MovingWagons", typeof(RectTransform));
                rootObj.transform.SetParent(m_Slots.transform, false);
                rootObj.transform.localPosition = Vector3.zero;

                float tilt = m_Slots.Style != null ? m_Slots.Style.tilt : 45f;
                rootObj.transform.localRotation = Quaternion.Euler(tilt, 0f, 0f);
                rootObj.transform.localScale = Vector3.one;
                m_WagonsRoot = rootObj.transform;
            }
        }

        /// <summary>
        /// Oyuncunun havuzdan tıkladığı vagonu ray akışına sokar.
        /// Vagon havuzdan havalanıp ray üzerine konar ve kesintisiz dolaşıma başlar.
        /// </summary>
        public bool SendWagonToMovingFlow(TruckSlot place)
        {
            if (place == null || place.IsEmpty) return false;

            // Ray hattı doluysa (maksimum 5 vagon)
            if (m_MovingWagons.Count >= m_MaxTrackWagons)
            {
                if (place.Truck != null)
                {
                    place.Truck.DOKill();
                    place.Truck.DOPunchPosition(Vector3.up * 0.08f, 0.22f, 8, 0.5f);
                }
                return false;
            }

            Color color = place.TruckColor;
            Transform truck = place.ReleaseTruck();
            if (truck == null) return false;

            CompactPool();
            RefillPool();

            if (m_WagonsRoot == null)
            {
                SpawnMovingTrainRoot();
            }

            truck.SetParent(m_WagonsRoot, true);

            TruckCargo cargo = truck.GetComponent<TruckCargo>();
            if (cargo == null) cargo = truck.gameObject.AddComponent<TruckCargo>();
            cargo.EnsureBadge();
            cargo.UpdateBadge(false);

            WagonClickTarget clickTarget = truck.GetComponent<WagonClickTarget>();
            if (clickTarget != null) clickTarget.PoolPlace = null;
            BoxCollider triggerBox = truck.GetComponent<BoxCollider>();
            if (triggerBox != null) Destroy(triggerBox);

            TruckPaint paint = truck.GetComponent<TruckPaint>();
            if (paint != null)
            {
                paint.SetBodyColor(color);
                paint.Apply();
            }

            MineCartMover mover = truck.GetComponent<MineCartMover>();
            if (mover != null) mover.StopMoving();

            Animator anim = truck.GetComponent<Animator>();
            if (anim != null) anim.speed = 1f;

            if (m_PerimeterTrain)
            {
                float entryDistance = m_Loop.StraightBottom * 0.5f;
                if (m_MovingWagons.Count > 0)
                {
                    float maxGap = 0f;
                    float bestGapMid = entryDistance;

                    List<float> sorted = new List<float>();
                    for (int i = 0; i < m_MovingWagons.Count; i++)
                    {
                        sorted.Add(m_MovingWagons[i].DistanceOnLoop);
                    }
                    sorted.Sort();

                    for (int i = 0; i < sorted.Count; i++)
                    {
                        float next = (i + 1 < sorted.Count) ? sorted[i + 1] : (sorted[0] + m_Loop.TotalPerimeter);
                        float gap = next - sorted[i];
                        if (gap > maxGap)
                        {
                            maxGap = gap;
                            bestGapMid = Mathf.Repeat(sorted[i] + gap * 0.5f, m_Loop.TotalPerimeter);
                        }
                    }
                    entryDistance = bestGapMid;
                }

                m_Loop.Evaluate(entryDistance, out Vector3 targetPos, out Vector3 tangent, out Quaternion targetRot, m_WagonRotation);

                MovingWagon movingWagon = new MovingWagon
                {
                    GameObject = truck.gameObject,
                    Transform = truck,
                    Cargo = cargo,
                    Paint = paint,
                    Mover = mover,
                    Animator = anim,
                    DistanceOnLoop = entryDistance,
                    IsDeployingMiners = false,
                    HasStartedDeploying = false,
                    IsJumpingToTrack = true,
                    IsDeparting = false
                };
                m_MovingWagons.Add(movingWagon);

                truck.DOKill();
                truck.DORotateQuaternion(targetRot, 0.35f).SetEase(Ease.OutQuad);
                truck.DOScale(m_WagonScale, 0.35f);
                truck.DOMove(targetPos, 0.35f).SetEase(Ease.OutQuad).OnComplete(() =>
                {
                    if (truck != null)
                    {
                        truck.position = targetPos;
                        truck.rotation = targetRot;
                        movingWagon.IsJumpingToTrack = false;
                    }
                });

                // Kullanıcı talebi: Vagon raya konulduğunda küpler hemen patlamaz.
                // Vagon rayda ilerlerken küplerin yakınından geçerken onları dinamik süpürür.
                return true;
            }

            // Eski yatay hat mantığı (Fallback)
            float startX = 0f;
            if (m_MovingWagons.Count > 0)
            {
                float minX = float.MaxValue;
                bool centerBlocked = false;

                for (int i = 0; i < m_MovingWagons.Count; i++)
                {
                    float wx = m_MovingWagons[i].PositionX;
                    if (wx < minX) minX = wx;
                    if (Mathf.Abs(wx - 0f) < m_MinWagonSpacing * 0.85f)
                    {
                        centerBlocked = true;
                    }
                }

                if (centerBlocked || minX < 0f)
                {
                    startX = Mathf.Max(m_PortalLeftX + 40f, minX - m_MinWagonSpacing);
                }
                else
                {
                    startX = 0f;
                }
            }

            Vector3 targetLocalPos = new Vector3(startX, m_WagonY, m_WagonZ);

            MovingWagon legacyWagon = new MovingWagon
            {
                GameObject = truck.gameObject,
                Transform = truck,
                Cargo = cargo,
                Paint = paint,
                Mover = mover,
                Animator = anim,
                PositionX = startX,
                IsDeployingMiners = false,
                HasStartedDeploying = false,
                IsJumpingToTrack = true
            };
            m_MovingWagons.Add(legacyWagon);

            truck.DOKill();
            truck.DOLocalRotateQuaternion(m_WagonRotation, 0.28f);
            truck.DOScale(m_WagonScale, 0.28f);
            truck.DOLocalMove(targetLocalPos, 0.28f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                if (truck != null)
                {
                    truck.localPosition = targetLocalPos;
                    legacyWagon.IsJumpingToTrack = false;
                }
            });

            return true;
        }

        private void UpdateMovingTrain()
        {
            if (m_MovingWagons.Count == 0) return;

            if (m_PerimeterTrain)
            {
                UpdatePerimeterTrainCirculation();
                return;
            }

            UpdateLegacyHorizontalTrain();
        }

        private void UpdatePerimeterTrainCirculation()
        {
            float baseStep = m_PerimeterSpeed * Time.deltaTime;
            float minSpacing = m_Loop.TotalPerimeter / (m_MaxTrackWagons + 1);

            for (int i = 0; i < m_MovingWagons.Count; i++)
            {
                MovingWagon wagon = m_MovingWagons[i];
                if (wagon == null || wagon.Transform == null || wagon.IsJumpingToTrack || wagon.IsDeparting) continue;

                float moveStep = baseStep;
                for (int j = 0; j < m_MovingWagons.Count; j++)
                {
                    if (i == j) continue;
                    MovingWagon other = m_MovingWagons[j];
                    if (other == null || other.IsDeparting) continue;

                    float aheadDist = Mathf.Repeat(other.DistanceOnLoop - wagon.DistanceOnLoop, m_Loop.TotalPerimeter);
                    if (aheadDist > 0.001f && aheadDist < minSpacing)
                    {
                        float slowFactor = Mathf.Clamp01(aheadDist / minSpacing);
                        moveStep = Mathf.Min(moveStep, baseStep * slowFactor);
                    }
                }

                wagon.DistanceOnLoop = Mathf.Repeat(wagon.DistanceOnLoop + moveStep, m_Loop.TotalPerimeter);
                m_Loop.Evaluate(wagon.DistanceOnLoop, out Vector3 pos, out Vector3 tangent, out Quaternion rot, m_WagonRotation);

                wagon.Transform.position = pos;
                wagon.Transform.rotation = rot;

                if (wagon.Mover != null)
                {
                    wagon.Mover.SpinWheelsByDistance(moveStep);
                }
                if (wagon.Animator != null)
                {
                    wagon.Animator.speed = (moveStep > 0.001f) ? 1f : 0f;
                }
            }
        }

        /// <summary>
        /// Vagon ray üzerinde dolaşırken, menzili içindeki eşleşen renkteki sağlam küpleri
        /// dış katmandan içe doğru sırayla dinamik olarak kırıp kasasına çeker (süpürme mekaniği).
        /// </summary>
        private void UpdateWagonProximitySweeping()
        {
            if (m_MovingWagons.Count == 0) return;

            PixelCube[] allCubes = Miner.GetCubesCached();
            if (allCubes == null || allCubes.Length == 0)
            {
                var gen = m_Generator != null ? m_Generator : Object.FindFirstObjectByType<PixelArtGenerator>();
                if (gen != null && gen.CubesContainer != null)
                {
                    allCubes = gen.CubesContainer.GetComponentsInChildren<PixelCube>();
                }
            }

            if (allCubes == null || allCubes.Length == 0) return;

            Dictionary<PixelCube, int> depthMap = null;

            for (int w = 0; w < m_MovingWagons.Count; w++)
            {
                MovingWagon wagon = m_MovingWagons[w];
                if (wagon == null || wagon.Transform == null || wagon.Cargo == null) continue;
                if (wagon.IsDeparting || wagon.IsJumpingToTrack) continue;

                // Kalan kapasite kontrolü (uçuş halindeki süpürmeler de düşülür)
                int availableCap = wagon.Cargo.RemainingCapacity - wagon.PendingSweeps;
                if (availableCap <= 0 || wagon.Cargo.IsFull) continue;

                // Süpürme aralığı / ritim cooldown kontrolü
                if (Time.time < wagon.NextSweepTime) continue;

                Color wagonColor = wagon.Cargo.CargoColor;
                Vector3 wagonPos = wagon.Transform.position;

                PixelCube bestTarget = null;
                float bestDist = float.MaxValue;
                int minDepth = int.MaxValue;
                int matchingRemainingOnBoard = 0;

                for (int i = 0; i < allCubes.Length; i++)
                {
                    PixelCube cube = allCubes[i];
                    if (cube == null || cube.IsPopped || !cube.gameObject.activeInHierarchy) continue;

                    bool colorMatches = (TruckCargo.ColorDistance(cube.CurrentColor, wagonColor) <= m_ColorThreshold) ||
                                       (TruckCargo.ColorDistance(ClassifyToPalette(cube.CurrentColor), wagonColor) <= m_ColorThreshold);
                    if (!colorMatches) continue;

                    matchingRemainingOnBoard++;

                    if (s_ReservedCubes.Contains(cube)) continue;

                    float dist = Vector3.Distance(wagonPos, cube.transform.position);
                    if (dist > m_SweepRadius) continue;

                    if (depthMap == null)
                    {
                        depthMap = Miner.CalculateLayerDepths(allCubes);
                    }

                    int depth = (depthMap != null && depthMap.TryGetValue(cube, out int d)) ? d : 0;

                    // Öncelik: 1) Dış katman (daha düşük derinlik), 2) Vagona en yakın olan
                    if (depth < minDepth || (depth == minDepth && dist < bestDist))
                    {
                        minDepth = depth;
                        bestDist = dist;
                        bestTarget = cube;
                    }
                }

                if (bestTarget != null)
                {
                    wagon.NextSweepTime = Time.time + m_SweepInterval;
                    wagon.PendingSweeps++;
                    wagon.SweepComboCount++;
                    s_ReservedCubes.Add(bestTarget);

                    SweepCubeIntoWagon(bestTarget, wagon);
                }
                else if (matchingRemainingOnBoard == 0 && wagon.PendingSweeps == 0)
                {
                    // Tabloda bu renkten hiç küp kalmadıysa ve yerdeki parçalar da bittiyse
                    bool hasBoardShards = false;
                    for (int s = 0; s < m_BoardShards.Count; s++)
                    {
                        if (m_BoardShards[s] != null && TruckCargo.ColorDistance(m_BoardShards[s].Color, wagonColor) <= m_ColorThreshold)
                        {
                            hasBoardShards = true;
                            break;
                        }
                    }

                    if (!hasBoardShards && wagon.Cargo.Load > 0)
                    {
                        StartCoroutine(WagonDepartFromLoopRoutine(wagon));
                    }
                }
            }
        }

        /// <summary>
        /// Belirlenen küpü kırar ve 12 Voronoi parçasını hareket halindeki vagona dinamik vakumla akıtır.
        /// </summary>
        private void SweepCubeIntoWagon(PixelCube cube, MovingWagon wagon)
        {
            if (cube == null || wagon == null || wagon.Cargo == null) return;

            Vector3 cubePos = cube.transform.position;
            Quaternion cubeRot = cube.transform.rotation;
            Vector3 cubeScale = cube.transform.lossyScale;
            Color cubeColor = cube.CurrentColor;

            // 1. Kristal cam kırılma sesi (Combo arttıkça pitch yükselir)
            if (VoxelParticleManager.Instance != null)
            {
                float comboPitch = Mathf.Clamp(0.96f + (wagon.SweepComboCount * 0.032f), 0.95f, 1.85f);
                VoxelParticleManager.Instance.PlayGlassShatterSound(comboPitch);
                VoxelParticleManager.Instance.SpawnVoxelBurst(cubePos, cubeScale, cubeColor);
            }

            // 2. Küpün kendisini ve gölgesini gizle
            cube.SetPoppedVisualState(true);
            s_ReservedCubes.Remove(cube);
            if (PixelCubeInteraction.Instance != null)
            {
                PixelCubeInteraction.Instance.RegisterPoppedCube(cube);
            }

            // 3. 12 adet 3D Voronoi kırık parçasını oluştur
            FracturedCubeData fracData = FracturedCubeData.Instance;
            int shardCount = (fracData != null && fracData.ShardCount > 0) ? fracData.ShardCount : 12;

            int arrivedCount = 0;
            bool cubeCountRegistered = false;

            Transform targetWagon = wagon.Transform;
            TruckCargo targetCargo = wagon.Cargo;
            Vector3 sScale = cubeScale * m_ShardScaleMultiplier;

            for (int s = 0; s < shardCount; s++)
            {
                FracturedCubeData.ShardData shard = (fracData != null) ? fracData.GetShard(s) : default;
                Vector3 localOffset = shard.localOffset;
                Vector3 outwardDir = cubeRot * (shard.outwardDir.sqrMagnitude > 0.001f ? shard.outwardDir : (localOffset.sqrMagnitude > 0.001f ? localOffset.normalized : Vector3.up));

                Vector3 shardStart = cubePos + cubeRot * Vector3.Scale(localOffset * 0.5f, cubeScale);
                Mesh shardMesh = shard.mesh;

                float sizeVar = UnityEngine.Random.Range(0.85f, 1.15f);
                Vector3 thisShardScale = sScale * sizeVar;

                Vector3 targetOffset = Vector3.up * 0.20f + (targetWagon != null ? (targetWagon.right * UnityEngine.Random.Range(-0.16f, 0.16f)) + (targetWagon.forward * UnityEngine.Random.Range(-0.12f, 0.12f)) : Vector3.zero);

                float stagger = s * 0.006f;

                DOVirtual.DelayedCall(stagger, () =>
                {
                    if (targetWagon == null) return;
                    CargoFlyer.LaunchShardVacuumToMovingWagon(
                        shardStart,
                        cubeRot,
                        cubeColor,
                        shardMesh,
                        thisShardScale,
                        targetWagon,
                        targetOffset,
                        m_VacuumDuration,
                        m_VacuumArcHeight,
                        () =>
                        {
                            arrivedCount++;
                            if (targetCargo != null && targetCargo.Stack != null)
                            {
                                targetCargo.Stack.AddPiece(1f, shardMesh);
                            }

                            if (!cubeCountRegistered && arrivedCount >= Mathf.Min(3, shardCount))
                            {
                                cubeCountRegistered = true;
                                if (wagon != null)
                                {
                                    wagon.PendingSweeps = Mathf.Max(0, wagon.PendingSweeps - 1);
                                }
                                if (targetCargo != null)
                                {
                                    targetCargo.LoadOneCube();
                                    if (targetWagon != null)
                                    {
                                        targetWagon.DOKill(true);
                                        targetWagon.DOPunchScale(new Vector3(0.04f, 0.08f, 0.04f), 0.16f, 3, 0.4f);
                                    }

                                    if (targetCargo.IsFull)
                                    {
                                        StartCoroutine(WagonDepartFromLoopRoutine(wagon));
                                    }
                                }
                            }
                        }
                    );
                });
            }
        }

        private void UpdateVacuumSuction()
        {
            if (m_BoardShards.Count == 0 || m_MovingWagons.Count == 0) return;

            for (int w = 0; w < m_MovingWagons.Count; w++)
            {
                MovingWagon wagon = m_MovingWagons[w];
                if (wagon == null || wagon.Cargo == null || wagon.IsDeparting || wagon.IsJumpingToTrack) continue;
                if (wagon.Cargo.IsFull || wagon.Cargo.RemainingCapacity <= 0) continue;

                Color wagonColor = wagon.Cargo.CargoColor;
                Vector3 wagonPos = wagon.Transform.position;

                BoardShardGroup bestTarget = null;
                float bestDist = float.MaxValue;

                for (int s = 0; s < m_BoardShards.Count; s++)
                {
                    BoardShardGroup group = m_BoardShards[s];
                    if (group == null || group.IsVacuuming || !group.IsSettled) continue;

                    float colorDist = TruckCargo.ColorDistance(group.Color, wagonColor);
                    if (colorDist > m_ColorThreshold) continue;

                    float dist = Vector3.Distance(wagonPos, group.Position);
                    if (dist <= m_VacuumRadius && dist < bestDist)
                    {
                        bestDist = dist;
                        bestTarget = group;
                    }
                }

                if (bestTarget != null)
                {
                    bestTarget.IsVacuuming = true;
                    StartCoroutine(VacuumShardGroupRoutine(bestTarget, wagon));
                }
            }
        }

        private IEnumerator VacuumShardGroupRoutine(BoardShardGroup group, MovingWagon wagon)
        {
            if (group == null || wagon == null || wagon.Cargo == null) yield break;

            m_BoardShards.Remove(group);
            wagon.Cargo.LoadOneCube();

            Transform targetWagon = wagon.Transform;
            TruckCargo targetCargo = wagon.Cargo;

            int arrived = 0;
            int total = group.Flyers.Count;

            for (int f = 0; f < group.Flyers.Count; f++)
            {
                CargoFlyer flyer = group.Flyers[f];
                if (flyer != null)
                {
                    Mesh shardMesh = flyer.CurrentMesh;
                    Vector3 offset = Vector3.up * 0.20f + Random.insideUnitSphere * 0.04f;

                    flyer.VacuumPullToMovingTarget(
                        targetWagon,
                        offset,
                        m_VacuumDuration,
                        m_VacuumArcHeight,
                        () =>
                        {
                            arrived++;
                            if (targetCargo != null && targetCargo.Stack != null)
                            {
                                targetCargo.Stack.AddPiece(1f, shardMesh);
                            }

                            if (arrived >= total)
                            {
                                if (targetWagon != null)
                                {
                                    targetWagon.DOKill(true);
                                    targetWagon.DOPunchScale(new Vector3(0.04f, 0.08f, 0.04f), 0.16f, 3, 0.4f);
                                }

                                if (targetCargo != null && targetCargo.IsFull)
                                {
                                    StartCoroutine(WagonDepartFromLoopRoutine(wagon));
                                }
                            }
                        }
                    );
                }

                yield return new WaitForSeconds(0.012f);
            }
        }

        private IEnumerator WagonDepartFromLoopRoutine(MovingWagon wagon)
        {
            if (wagon == null || wagon.IsDeparting) yield break;
            wagon.IsDeparting = true;

            yield return new WaitForSeconds(0.25f);

            if (wagon.Transform == null) yield break;

            wagon.Transform.DOKill();
            wagon.Transform.DOPunchScale(new Vector3(0.12f, 0.12f, 0.12f), 0.25f, 4, 0.5f);
            yield return new WaitForSeconds(0.22f);

            if (wagon.Transform != null)
            {
                wagon.Transform.DOScale(Vector3.zero, 0.32f).SetEase(Ease.InBack);
                wagon.Transform.DOMove(wagon.Transform.position + wagon.Transform.forward * 1.8f + Vector3.back * 0.5f, 0.32f).SetEase(Ease.InQuad);
            }

            yield return new WaitForSeconds(0.35f);

            if (wagon.GameObject != null)
            {
                Destroy(wagon.GameObject);
            }
            m_MovingWagons.Remove(wagon);

            CompactPool();
            RefillPool();
            if (m_Pool != null) m_Pool.UpdateRowVisuals();
        }

        private IEnumerator SequentialShatterForLoopRoutine(Color targetColor, TruckCargo cargo)
        {
            yield return new WaitForSeconds(0.35f);

            PixelCube[] allCubes = Object.FindObjectsByType<PixelCube>(FindObjectsSortMode.None);
            if (allCubes == null || allCubes.Length == 0) yield break;

            List<PixelCube> matching = new List<PixelCube>();
            for (int i = 0; i < allCubes.Length; i++)
            {
                PixelCube c = allCubes[i];
                if (c == null || c.IsPopped || !c.gameObject.activeInHierarchy) continue;
                if (s_ReservedCubes.Contains(c)) continue;

                if (TruckCargo.ColorDistance(c.CurrentColor, targetColor) <= m_ColorThreshold ||
                    TruckCargo.ColorDistance(ClassifyToPalette(c.CurrentColor), targetColor) <= m_ColorThreshold)
                {
                    matching.Add(c);
                }
            }

            if (matching.Count == 0) yield break;

            var depthMap = Miner.CalculateLayerDepths(allCubes);
            matching.Sort((a, b) =>
            {
                int depthA = depthMap.TryGetValue(a, out int dA) ? dA : 0;
                int depthB = depthMap.TryGetValue(b, out int dB) ? dB : 0;
                if (depthA != depthB) return depthA.CompareTo(depthB);
                return a.transform.position.y.CompareTo(b.transform.position.y);
            });

            int countToShatter = Mathf.Min(cargo != null ? cargo.RemainingCapacity : 16, matching.Count);
            List<PixelCube> targets = new List<PixelCube>(countToShatter);
            for (int i = 0; i < countToShatter; i++)
            {
                targets.Add(matching[i]);
                s_ReservedCubes.Add(matching[i]);
            }

            for (int i = 0; i < targets.Count; i++)
            {
                if (cargo != null && cargo.IsFull)
                {
                    for (int rem = i; rem < targets.Count; rem++)
                    {
                        s_ReservedCubes.Remove(targets[rem]);
                    }
                    yield break;
                }

                PixelCube cube = targets[i];
                s_ReservedCubes.Remove(cube);

                if (cube != null && !cube.IsPopped && cube.gameObject.activeInHierarchy)
                {
                    if (VoxelParticleManager.Instance != null)
                    {
                        float comboPitch = Mathf.Clamp(1.0f + (i * 0.032f), 0.95f, 1.85f);
                        VoxelParticleManager.Instance.PlayGlassShatterSound(comboPitch);
                        VoxelParticleManager.Instance.SpawnVoxelBurst(cube.transform.position, cube.transform.lossyScale, cube.CurrentColor);
                    }

                    cube.SetPoppedVisualState(true);
                    if (PixelCubeInteraction.Instance != null)
                    {
                        PixelCubeInteraction.Instance.RegisterPoppedCube(cube);
                    }

                    NotifyCubePopped(cube.CurrentColor, cube.transform.position, cube.CurrentColor, cube.transform.lossyScale, cube.transform.rotation);
                }

                yield return new WaitForSeconds(m_ShatterInterval);
            }
        }

        private void UpdateLegacyHorizontalTrain()
        {
            float step = m_TrainSpeed * Time.deltaTime;
            float worldScale = m_WagonsRoot != null ? m_WagonsRoot.lossyScale.x : 1f;

            for (int i = 0; i < m_MovingWagons.Count; i++)
            {
                MovingWagon wagon = m_MovingWagons[i];
                if (wagon == null || wagon.Transform == null || wagon.IsJumpingToTrack) continue;

                if (wagon.IsDeployingMiners)
                {
                    wagon.Transform.localPosition = new Vector3(wagon.PositionX, m_WagonY, m_WagonZ);
                    if (wagon.Animator != null) wagon.Animator.speed = 0f;
                    continue;
                }

                float moveStep = step;
                for (int j = 0; j < m_MovingWagons.Count; j++)
                {
                    if (i == j) continue;
                    MovingWagon ahead = m_MovingWagons[j];
                    if (ahead == null) continue;

                    float dx = ahead.PositionX - wagon.PositionX;
                    if (dx > 0f && dx < m_MinWagonSpacing)
                    {
                        moveStep = Mathf.Min(moveStep, Mathf.Max(0f, dx - (m_MinWagonSpacing * 0.85f)));
                    }
                }

                MinerCrew crew = wagon.Transform.GetComponent<MinerCrew>();
                bool hasMinersToDeploy = crew == null || crew.RemainingMinersToDeploy > 0;

                if (m_EnableMiners && !wagon.HasCompletedInitialDeployment && !wagon.HasStartedDeploying && wagon.Cargo != null && !wagon.Cargo.IsFull && hasMinersToDeploy)
                {
                    if (wagon.PositionX < 0f && (wagon.PositionX + moveStep) >= 0f)
                    {
                        moveStep = -wagon.PositionX;
                    }

                    if (Mathf.Abs(wagon.PositionX + moveStep) <= 0.05f || (wagon.PositionX >= -0.05f && wagon.PositionX <= 0.05f))
                    {
                        if (Miner.HasAccessibleMatchingCube(wagon.Cargo.CargoColor, m_ColorThreshold) ||
                            Miner.HasMatchingUnpoppedCube(wagon.Cargo.CargoColor, m_ColorThreshold))
                        {
                            wagon.PositionX = 0f;
                            wagon.Transform.localPosition = new Vector3(0f, m_WagonY, m_WagonZ);
                            wagon.HasStartedDeploying = true;
                            wagon.IsDeployingMiners = true;
                            if (wagon.Animator != null) wagon.Animator.speed = 0f;

                            if (crew == null) crew = wagon.Transform.gameObject.AddComponent<MinerCrew>();
                            crew.StartJumpingOutSequence(m_ColorThreshold, m_MinerSpawnDelay, m_MinerRunSpeed, () =>
                            {
                                wagon.IsDeployingMiners = false;
                                wagon.HasCompletedInitialDeployment = true;
                            });

                            continue;
                        }
                        else
                        {
                            wagon.HasStartedDeploying = true;
                            wagon.HasCompletedInitialDeployment = true;
                        }
                    }
                }

                wagon.PositionX += moveStep;

                if (wagon.PositionX > 50f && wagon.HasStartedDeploying)
                {
                    wagon.HasCompletedInitialDeployment = true;
                }

                if (wagon.PositionX > m_PortalRightX)
                {
                    if (wagon.Cargo != null && wagon.Cargo.IsFull)
                    {
                        Destroy(wagon.GameObject);
                        m_MovingWagons.RemoveAt(i);
                        i--;
                        continue;
                    }
                    else
                    {
                        wagon.PositionX = m_PortalLeftX + (wagon.PositionX - m_PortalRightX);
                    }
                }

                wagon.Transform.localPosition = new Vector3(wagon.PositionX, m_WagonY, m_WagonZ);

                if (wagon.Mover != null)
                {
                    wagon.Mover.SpinWheelsByDistance(moveStep * worldScale);
                }
                if (wagon.Animator != null)
                {
                    wagon.Animator.speed = (moveStep > 0.001f) ? 1f : 0f;
                }
            }
        }

        #endregion

        #region 📦 Kırmızı Raf & Vagona Akış (Shelf Flow)

        private void NotifyCubePoppedPerimeterDrop(
            Color paletteColor,
            Color pieceColor,
            Vector3 worldPosition,
            Vector3? cubeScale,
            Quaternion? cubeRotation,
            FracturedCubeData fracData,
            int shardCount)
        {
            Vector3 sScale = (cubeScale ?? Vector3.one) * m_ShardScaleMultiplier;
            Quaternion sRot = cubeRotation ?? Quaternion.identity;

            BoardShardGroup group = new BoardShardGroup
            {
                CubeId = ++m_NextCubeId,
                Color = paletteColor,
                Position = worldPosition, // Tam olarak küpün olduğu nokta
                IsVacuuming = false,
                IsSettled = false
            };
            m_BoardShards.Add(group);

            int landedCount = 0;

            Vector3 cScale = cubeScale ?? (Vector3.one * 0.35f);
            float halfW = Mathf.Max(0.12f, cScale.x * 0.5f);
            float halfH = Mathf.Max(0.12f, cScale.y * 0.5f);

            for (int s = 0; s < shardCount; s++)
            {
                FracturedCubeData.ShardData shard = (fracData != null) ? fracData.GetShard(s) : default;
                Vector3 localOffset = shard.localOffset;
                Vector3 outwardDir = sRot * (shard.outwardDir.sqrMagnitude > 0.001f ? shard.outwardDir : (localOffset.sqrMagnitude > 0.001f ? localOffset.normalized : Vector3.up));

                Vector3 shardStart = worldPosition + sRot * Vector3.Scale(localOffset * 0.5f, cScale);
                Mesh shardMesh = shard.mesh;

                // 360 Derece Doğal Kir/Kırıntı Saçılması (Altın Açı + Radyal Jitter):
                // Parçalar tam olarak küpün bulunduğu alana homojen ve doğal bir toprak/moloz öbeği gibi dağılır
                float angle = (s * 2.39996323f) + Random.Range(-0.22f, 0.22f); // Altın açı spiral dağılımı
                float rNorm = Mathf.Sqrt((s + 0.5f) / (float)shardCount); // Daire içi homojen alan dağılımı
                
                // 3 kademeli kir dağılımı: Çekirdek kırıntılar, gövde parçaları ve dışa sıçrayan taneler
                float rDist;
                int tier = s % 3;
                if (tier == 0)
                {
                    rDist = rNorm * Random.Range(0.25f, 0.65f); // İç çekirdek kırıntıları
                }
                else if (tier == 1)
                {
                    rDist = rNorm * Random.Range(0.65f, 1.10f); // Küp gövdesi üzerindeki parçalar
                }
                else
                {
                    rDist = rNorm * Random.Range(1.05f, 1.50f); // Dışa hafif saçılan taneler
                }
                rDist *= m_LocalDropScatterRadius;

                float dropX = worldPosition.x + Mathf.Cos(angle) * halfW * rDist;
                float dropY = worldPosition.y + Mathf.Sin(angle) * halfH * rDist;
                
                // Z derinliği: Panonun hemen önünde, Z-fighting (titreme) olmadan şıkça katmanlanır
                float dropZ = worldPosition.z - 0.025f - (s * 0.0025f) - Random.Range(0f, 0.008f);
                Vector3 dropPos = new Vector3(dropX, dropY, dropZ);

                // Kir/kırıntı boyut çeşitliliği: Bazı taneler minik, bazıları dolgun taş/parça
                float sizeVar;
                if (tier == 0)
                {
                    sizeVar = Random.Range(0.68f, 0.88f); // Minik kırıntı taneleri
                }
                else if (tier == 1)
                {
                    sizeVar = Random.Range(0.92f, 1.15f); // Ana gövde kırıkları
                }
                else
                {
                    sizeVar = Random.Range(0.75f, 1.05f); // Dışa fırlayan parçalar
                }

                // Kullanıcı ayarı varyasyon çarpanı ile harmanla
                float finalScaleFactor = Mathf.Lerp(1f, sizeVar, Mathf.Clamp01(m_LocalDropScaleVariation * 2.5f));
                Vector3 thisShardScale = sScale * finalScaleFactor;

                // Mikro zamanlama gecikmesi (organik patlama hissi)
                float stagger = s * 0.005f + Random.Range(0f, 0.008f);

                CargoFlyer flyer = CargoFlyer.LaunchShardLocalDrop(
                    shardStart,
                    sRot,
                    outwardDir,
                    dropPos,
                    pieceColor,
                    shardMesh,
                    thisShardScale,
                    stagger,
                    m_LocalDropFallDuration * Random.Range(0.95f, 1.08f),
                    () =>
                    {
                        landedCount++;
                        if (landedCount >= shardCount)
                        {
                            group.IsSettled = true;
                        }
                    },
                    m_LocalDropArcHeight
                );

                group.Flyers.Add(flyer);
            }
        }

        /// <summary>
        /// Küp patladığında çağrılır:
        /// 1. Küpü Blender'daki 12 doğal kırık parçasına (Ore_Frag_00..11) böler.
        /// 2. Parçalar ilk anda küpün kendi içindeki 3D çatlak konumlarında başlar ve mikro patlamayla ayrılır.
        /// 3. Parçalar çerçevenin hemen altındaki kırmızı işaretli rafa dökülüp birikir (OutBounce).
        /// 4. Parçalar vagona akmaya hazır halde raf kuyruğuna kaydedilir.
        /// </summary>
        public void NotifyCubePopped(
            Color cubeColor,
            Vector3 worldPosition,
            Color? visualColor = null,
            Vector3? cubeScale = null,
            Quaternion? cubeRotation = null)
        {
            Color paletteColor = ClassifyToPalette(cubeColor);
            Color pieceColor = visualColor ?? cubeColor;

            FracturedCubeData fracData = FracturedCubeData.Instance;
            int shardCount = (fracData != null && fracData.ShardCount > 0) ? fracData.ShardCount : 12;

            if (m_PerimeterTrain)
            {
                NotifyCubePoppedPerimeterDrop(paletteColor, pieceColor, worldPosition, cubeScale, cubeRotation, fracData, shardCount);
                return;
            }

            // Slot modunda parçalar önce rafa düşer, ardından slottaki vagona kayar
            if (!m_ContinuousTrain)
            {
                TruckSlot slot = FindSlotFor(paletteColor);
                if (slot != null && slot.Truck != null && slot.Cargo != null)
                {
                    Transform truck = slot.Truck;
                    TruckCargo cargo = slot.Cargo;
                    Vector3 sScale = cubeScale ?? Vector3.one;
                    Quaternion sRot = cubeRotation ?? Quaternion.identity;

                    GetShelfArea(out Vector3 sCenter, out float sMinX, out float sMaxX);
                    float dX = Mathf.Clamp(worldPosition.x + Random.Range(-0.15f, 0.15f), sMinX, sMaxX);
                    Vector3 dPos = new Vector3(dX, sCenter.y, sCenter.z);
                    Vector3 sTgt = new Vector3(Mathf.Clamp(sCenter.x + Random.Range(-m_ShelfGatherRadius, m_ShelfGatherRadius), sMinX, sMaxX), sCenter.y + Random.Range(0f, m_ShelfStackHeight * 0.8f), sCenter.z);

                    Vector3 finalScale = sScale * m_ShardScaleMultiplier;
                    int arrived = 0;
                    bool registered = false;

                    for (int s = 0; s < shardCount; s++)
                    {
                        FracturedCubeData.ShardData shard = (fracData != null) ? fracData.GetShard(s) : default;
                        Vector3 localOffset = shard.localOffset;
                        Vector3 outwardDir = sRot * (shard.outwardDir.sqrMagnitude > 0.001f ? shard.outwardDir : (localOffset.sqrMagnitude > 0.001f ? localOffset.normalized : Vector3.up));

                        Vector3 shardStart = worldPosition + sRot * Vector3.Scale(localOffset * 0.5f, sScale);
                        Mesh shardMesh = shard.mesh;

                        Vector3 targetOffset = Vector3.up * 0.28f + (truck.right * Random.Range(-0.20f, 0.20f)) + (truck.forward * Random.Range(-0.14f, 0.14f));
                        float stagger = s * 0.004f;

                        CargoFlyer.LaunchGlassShardViaShelfToWagon(
                            shardStart,
                            sRot,
                            outwardDir,
                            dPos,
                            sTgt,
                            truck,
                            targetOffset,
                            pieceColor,
                            shardMesh,
                            finalScale,
                            stagger,
                            m_FallToShelfDuration * Random.Range(0.95f, 1.08f),
                            m_PauseOnShelfDuration,
                            m_SlideToWagonDuration * Random.Range(0.95f, 1.08f),
                            () =>
                            {
                                arrived++;
                                if (cargo != null && cargo.Stack != null)
                                {
                                    cargo.Stack.AddPiece(1f, shardMesh);
                                }

                                if (!registered && arrived >= Mathf.Min(3, shardCount))
                                {
                                    registered = true;
                                    if (cargo != null)
                                    {
                                        cargo.LoadOneCube();
                                        if (truck != null)
                                        {
                                            truck.DOKill(true);
                                            truck.DOPunchScale(new Vector3(0.04f, 0.08f, 0.04f), 0.16f, 3, 0.4f);
                                        }
                                    }
                                }
                            }
                        );
                    }
                    return;
                }
            }

            CargoStack stack = null;
            if (m_MovingWagons.Count > 0 && m_MovingWagons[0].Cargo != null)
                stack = m_MovingWagons[0].Cargo.Stack;

            float baseSize = stack != null ? stack.BasePieceWorldSize : 0.1f;

            GetShelfArea(out Vector3 shelfCenter, out float shelfMinX, out float shelfMaxX);

            ShelfCubeGroup cubeGroup = new ShelfCubeGroup
            {
                CubeId = ++m_NextCubeId,
                Color = paletteColor,
                TotalFragments = shardCount,
                IsFlowing = false
            };
            m_ShelfCubes.Add(cubeGroup);

            Vector3 scale = cubeScale ?? Vector3.one;
            Quaternion rot = cubeRotation ?? Quaternion.identity;

            for (int i = 0; i < shardCount; i++)
            {
                FracturedCubeData.ShardData shard = (fracData != null) ? fracData.GetShard(i) : default;
                Vector3 localOffset = shard.localOffset;
                Vector3 outwardDir = rot * (shard.outwardDir.sqrMagnitude > 0.001f ? shard.outwardDir : (localOffset.sqrMagnitude > 0.001f ? localOffset.normalized : Vector3.up));

                // Küpün kendi içindeki 3D koordinatı (Blender [-1, 1] yerel uzayını dünya boyutuna dönüştürür)
                Vector3 start = worldPosition + rot * Vector3.Scale(localOffset * 0.5f, scale);

                // 1. İlk düşüş noktası: Küpün alt hizasındaki rafa (hafif doğal yayılmayla)
                float dropX = Mathf.Clamp(
                    worldPosition.x + Random.Range(-0.15f, 0.15f),
                    shelfMinX,
                    shelfMaxX
                );
                Vector3 dropPos = new Vector3(dropX, shelfCenter.y + Random.Range(0f, m_ShelfStackHeight * 0.5f), shelfCenter.z);

                // 2. Nihai toplanma noktası: Belirlediğimiz orta dar alan (kırmızı halka)
                float targetX = Mathf.Clamp(
                    shelfCenter.x + Random.Range(-m_ShelfGatherRadius, m_ShelfGatherRadius),
                    shelfMinX,
                    shelfMaxX
                );
                float targetY = shelfCenter.y + Random.Range(0f, m_ShelfStackHeight);
                float targetZ = shelfCenter.z + Random.Range(-0.02f, 0.02f);
                Vector3 shelfTarget = new Vector3(targetX, targetY, targetZ);

                float sizeFactor = 1f;
                cubeGroup.SizeFactors.Add(sizeFactor);

                Vector3 shardScale = scale;

                CargoFlyer.LaunchShardToShelf(
                    start,
                    rot,
                    outwardDir,
                    dropPos,
                    shelfTarget,
                    pieceColor,
                    shard.mesh,
                    shardScale,
                    m_FallToShelfDuration * Random.Range(0.9f, 1.15f),
                    m_PullToCenterDuration,
                    (landedFlyer) =>
                    {
                        cubeGroup.Flyers.Add(landedFlyer);
                    }
                );
            }
        }

        /// <summary>
        /// Raydan geçen vagonları denetler. Vagon eşleşen renkteki küpün TÜM parçalarını
        /// (1 küp olarak sayarak) içine çeker ve rozetindeki kalan küp sayısı 1 azalır.
        /// </summary>
        private void UpdateShelfCascadeFlow()
        {
            if (m_ShelfCubes.Count == 0) return;
            if (Time.time < m_NextCascadeTime) return;

            for (int w = 0; w < m_MovingWagons.Count; w++)
            {
                MovingWagon wagon = m_MovingWagons[w];
                if (wagon == null || wagon.Cargo == null) continue;

                // Vagon yükleme bölgesinde mi ve kalan kapasitesi var mı?
                if (wagon.PositionX < m_PickupZoneX.x || wagon.PositionX > m_PickupZoneX.y) continue;
                if (wagon.Cargo.IsFull || wagon.Cargo.RemainingCapacity <= 0) continue;

                // Rafta bu vagonun rengiyle eşleşen ve henüz akmaya başlamamış küp grubu ara
                ShelfCubeGroup matchingCube = null;
                for (int c = 0; c < m_ShelfCubes.Count; c++)
                {
                    ShelfCubeGroup cube = m_ShelfCubes[c];
                    if (cube == null || cube.IsFlowing || !cube.IsReady) continue;

                    float dist = TruckCargo.ColorDistance(cube.Color, wagon.Cargo.CargoColor);
                    if (dist <= m_ColorThreshold)
                    {
                        matchingCube = cube;
                        break;
                    }
                }

                if (matchingCube != null)
                {
                    matchingCube.IsFlowing = true;
                    m_ShelfCubes.Remove(matchingCube);

                    // 1 küp yüklendi -> Vagonun kapasitesi 1 azalır ve rozet (Badge) güncellenir
                    wagon.Cargo.LoadOneCube();

                    Transform wagonTarget = wagon.Transform;
                    TruckCargo targetCargo = wagon.Cargo;

                    // Bu küpün TÜM 12 kırık parçasını hareket halindeki vagona akıt!
                    for (int f = 0; f < matchingCube.Flyers.Count; f++)
                    {
                        CargoFlyer flyer = matchingCube.Flyers[f];
                        float sizeFactor = (f < matchingCube.SizeFactors.Count) ? matchingCube.SizeFactors[f] : 1f;

                        if (flyer != null)
                        {
                            Mesh shardMesh = flyer.CurrentMesh;
                            flyer.FlowToMovingTarget(
                                wagonTarget,
                                m_FlowToCartDuration,
                                m_FlowArcHeight,
                                () =>
                                {
                                    if (targetCargo != null && targetCargo.Stack != null)
                                    {
                                        targetCargo.Stack.AddPiece(sizeFactor, shardMesh);
                                    }
                                }
                            );
                        }
                    }

                    m_NextCascadeTime = Time.time + m_FlowStaggerDelay;
                    break;
                }
            }
        }

        /// <summary>
        /// Görseldeki kırmızı daireyle işaretlenen alt raf/hazne alanının dünya koordinatlarını hesaplar.
        /// </summary>
        public bool GetShelfArea(out Vector3 shelfCenter, out float shelfMinX, out float shelfMaxX)
        {
            shelfCenter = Vector3.zero;
            shelfMinX = -1f;
            shelfMaxX = 1f;

            if (m_CustomShelfAnchor != null)
            {
                shelfCenter = m_CustomShelfAnchor.position + Vector3.up * m_ShelfYOffset;
                float halfW = 1.2f * m_ShelfWidthFactor;
                shelfMinX = shelfCenter.x - halfW;
                shelfMaxX = shelfCenter.x + halfW;
                return true;
            }

            if (m_Generator == null)
            {
                m_Generator = Object.FindFirstObjectByType<PixelArtGenerator>();
            }

            Camera cam = Camera.main;
            if (m_Generator != null && m_Generator.CalculateTargetWorldBounds(cam, out Vector3 worldCenter, out float worldWidth, out float worldHeight))
            {
                Texture2D tex = m_Generator.GetActiveTexture();
                m_Generator.GetEffectiveGridSize(tex, out int cols, out int rows);
                if (cols <= 0) cols = 24;
                if (rows <= 0) rows = 24;

                float cellSize = Mathf.Min(worldWidth / cols, worldHeight / rows);
                float totalHeight = rows * cellSize;

                float gridBottomY = worldCenter.y - totalHeight * 0.5f;
                float frameBottomY = worldCenter.y - worldHeight * 0.5f;

                // Kullanıcının kırmızıyla işaretlediği yer: Mavi çerçevenin alt kenarı / iç hazne pervazı
                // Parçalar çerçevenin dışına taşmaz, doğrudan mavi çerçevenin alt iç pervazına düşer ve toplanır
                float shelfLedgeY = frameBottomY + worldHeight * 0.05f + m_ShelfYOffset;
                float shelfZ = worldCenter.z - 0.10f;

                // Mavi çerçevenin iç sol ve sağ sınırları (sınırlar o mavi çerçeve olsun)
                float frameInnerMarginX = worldWidth * 0.07f;
                float shelfHalfW = (worldWidth * 0.5f - frameInnerMarginX) * Mathf.Clamp01(m_ShelfWidthFactor);

                shelfCenter = new Vector3(worldCenter.x, shelfLedgeY, shelfZ);
                shelfMinX = worldCenter.x - shelfHalfW;
                shelfMaxX = worldCenter.x + shelfHalfW;
                return true;
            }

            if (m_Slots != null)
            {
                Vector3 slotPos = m_Slots.transform.position;
                shelfCenter = new Vector3(slotPos.x, slotPos.y + 1.2f + m_ShelfYOffset, -0.12f);
                shelfMinX = shelfCenter.x - 1.5f * m_ShelfWidthFactor;
                shelfMaxX = shelfCenter.x + 1.5f * m_ShelfWidthFactor;
                return true;
            }

            return false;
        }

        private void OnDrawGizmos()
        {
            if (m_PerimeterTrain)
            {
                CalculatePerimeterLoopBounds(out Vector3 center, out float width, out float height);
                PerimeterTrackLoop tempLoop = new PerimeterTrackLoop();
                tempLoop.Setup(center, width, height, m_LoopCornerRadius, center.z - 0.12f);

                Gizmos.color = Color.cyan;
                int segments = 64;
                Vector3 prev = Vector3.zero;
                for (int i = 0; i <= segments; i++)
                {
                    float dist = (i / (float)segments) * tempLoop.TotalPerimeter;
                    tempLoop.Evaluate(dist, out Vector3 p, out Vector3 t, out Quaternion r, Quaternion.identity);
                    if (i > 0)
                    {
                        Gizmos.DrawLine(prev, p);
                    }
                    prev = p;
                }
            }

            if (GetShelfArea(out Vector3 shelfCenter, out float shelfMinX, out float shelfMaxX))
            {
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.85f);
                float width = m_ShelfGatherRadius * 2f;
                Gizmos.DrawWireCube(shelfCenter, new Vector3(width, 0.25f + m_ShelfStackHeight, 0.25f));
            }
        }

        #endregion

        #region 🎯 Kurallar & Klasik Mod Uyumluluğu

        public bool CanPop(Color cubeColor)
        {
            if (m_PerimeterTrain || m_ContinuousTrain)
            {
                if (!m_RequireMatchingTruck) return true;

                for (int i = 0; i < m_MovingWagons.Count; i++)
                {
                    var w = m_MovingWagons[i];
                    if (w != null && w.Cargo != null && !w.Cargo.IsFull)
                    {
                        if (TruckCargo.ColorDistance(cubeColor, w.Cargo.CargoColor) <= m_ColorThreshold ||
                            TruckCargo.ColorDistance(ClassifyToPalette(cubeColor), w.Cargo.CargoColor) <= m_ColorThreshold)
                        {
                            return true;
                        }
                    }
                }

                // Çevresel modda havuzda bekleyen vagon varsa da küp patlayıp yere dökülebilir;
                // vagon hatta sürüldüğünde yanından geçerken vakumlayacaktır.
                if (m_PerimeterTrain && m_Pool != null && m_Pool.Places != null)
                {
                    Color paletteColor = ClassifyToPalette(cubeColor);
                    for (int p = 0; p < m_Pool.Places.Count; p++)
                    {
                        var place = m_Pool.Places[p];
                        if (place != null && place.Truck != null && place.Cargo != null && !place.Cargo.IsFull)
                        {
                            if (TruckCargo.ColorDistance(cubeColor, place.Cargo.CargoColor) <= m_ColorThreshold ||
                                TruckCargo.ColorDistance(paletteColor, place.Cargo.CargoColor) <= m_ColorThreshold)
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            if (!m_RequireMatchingTruck) return true;

            return FindSlotFor(ClassifyToPalette(cubeColor)) != null;
        }

        public Color ClassifyToPalette(Color cubeColor)
        {
            List<PaletteColorOverride> palette = GetPalette();
            if (palette == null || palette.Count == 0) return cubeColor;

            PixelLevelData level = GetLevel();
            bool hasAdjustment = level != null && (level.ColorBrightness != 1f || level.ColorSaturation != 1f || level.ColorContrast != 1f);

            Color best = cubeColor;
            float bestDistance = float.MaxValue;

            foreach (PaletteColorOverride entry in palette)
            {
                if (entry == null || entry.pixelCount <= 0) continue;

                Color candidate = entry.targetColor;
                if (hasAdjustment)
                {
                    candidate = PixelCube.AdjustColor(candidate, level.ColorBrightness, level.ColorSaturation, level.ColorContrast);
                }

                float dAdj = TruckCargo.ColorDistance(cubeColor, candidate);
                float dRaw = TruckCargo.ColorDistance(cubeColor, entry.targetColor);
                float dOrig = TruckCargo.ColorDistance(cubeColor, entry.originalColor);
                float dMin = Mathf.Min(dAdj, Mathf.Min(dRaw, dOrig));

                if (dMin < bestDistance)
                {
                    bestDistance = dMin;
                    best = candidate;
                }
            }

            return best;
        }

        private void RebuildStrips()
        {
            PixelLevelData level = GetLevel();
            if (level == null) return;

            if (level.SlotSprite != null)
            {
                if (m_Slots != null) m_Slots.Style.sprite = level.SlotSprite;
                if (m_Pool != null) m_Pool.Style.sprite = level.SlotSprite;
            }

            if (m_Slots != null)
            {
                // Ray şeridi (m_Slots) sahnedeki tüneller arası sabit ray hattıdır.
                // Farklı levellarda rayların ve tünellerin boyutu ASLA değişmemeli, hep sabit kalmalı!
                m_Slots.UpdateShadows();
            }

            if (m_Pool != null) m_Pool.RebuildPlaces(level.PoolColumns, level.PoolRows);
        }

        private PixelLevelData GetLevel()
        {
            if (m_Generator == null) m_Generator = Object.FindFirstObjectByType<PixelArtGenerator>();
            return m_Generator != null ? m_Generator.ActiveLevelData : null;
        }

        private List<PaletteColorOverride> GetPalette()
        {
            PixelLevelData level = GetLevel();
            return level != null ? level.ColorPalette : null;
        }

        private int GetTruckCapacity()
        {
            PixelLevelData level = GetLevel();
            return level != null ? level.TruckCapacity : m_FallbackTruckCapacity;
        }

        private void ClearSlots()
        {
            if (m_Slots == null) return;
            foreach (TruckSlot slot in m_Slots.Slots)
            {
                if (slot == null || slot.IsEmpty) continue;
                Transform truck = slot.ReleaseTruck();
                if (truck != null) Destroy(truck.gameObject);
            }
        }

        private void ClearPool()
        {
            if (m_Pool == null) return;
            foreach (TruckSlot place in m_Pool.Places)
            {
                if (place == null || place.IsEmpty) continue;
                Transform truck = place.ReleaseTruck();
                if (truck != null) Destroy(truck.gameObject);
            }
        }

        public void BuildQueue()
        {
            m_Queue.Clear();
            PixelLevelData level = GetLevel();

            // 1. Manuel Vagon Sırası: Eğer seviyede özel sıra tanımlanmışsa BİREBİR o sırayı kullan!
            if (level != null && level.UseCustomWagonSequence && level.WagonSequence != null && level.WagonSequence.Count > 0)
            {
                foreach (WagonSequenceEntry seq in level.WagonSequence)
                {
                    if (seq == null) continue;
                    m_Queue.Enqueue(new TruckOrder { Color = seq.wagonColor, Capacity = seq.capacity });
                }
                return;
            }

            // 2. Sahnedeki gerçek küp sayılarını dinamik say (kullanıcı sahnede küp sildiyse doğru yansısın)
            List<PaletteColorOverride> palette = GetPalette();
            var trucks = new List<TruckOrder>();
            int capacity = GetTruckCapacity();
            bool hasAdjustment = level != null && (level.ColorBrightness != 1f || level.ColorSaturation != 1f || level.ColorContrast != 1f);

            bool countedFromScene = false;
            if (m_Generator == null) m_Generator = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (m_Generator != null && m_Generator.CubesContainer != null && m_Generator.CubesContainer.childCount > 0)
            {
                PixelCube[] sceneCubes = m_Generator.CubesContainer.GetComponentsInChildren<PixelCube>();
                if (sceneCubes != null && sceneCubes.Length > 0)
                {
                    Dictionary<Color, int> sceneColorCounts = new Dictionary<Color, int>();
                    foreach (var cube in sceneCubes)
                    {
                        if (cube == null || !cube.gameObject.activeSelf || cube.IsPopped) continue;
                        Color c = cube.CurrentColor;
                        if (c.a < 0.1f) continue;

                        Color matchedColor = c;
                        if (palette != null)
                        {
                            foreach (var entry in palette)
                            {
                                Color testColor = hasAdjustment
                                    ? PixelCube.AdjustColor(entry.targetColor, level.ColorBrightness, level.ColorSaturation, level.ColorContrast)
                                    : entry.targetColor;
                                if (PaletteColorOverride.ColorsMatch(testColor, c, 0.08f))
                                {
                                    matchedColor = testColor;
                                    break;
                                }
                            }
                        }

                        if (sceneColorCounts.ContainsKey(matchedColor))
                            sceneColorCounts[matchedColor]++;
                        else
                            sceneColorCounts[matchedColor] = 1;
                    }

                    if (sceneColorCounts.Count > 0)
                    {
                        countedFromScene = true;
                        foreach (var kvp in sceneColorCounts)
                        {
                            Color truckColor = kvp.Key;
                            int remaining = kvp.Value;
                            while (remaining > 0)
                            {
                                int load = Mathf.Min(capacity, remaining);
                                trucks.Add(new TruckOrder { Color = truckColor, Capacity = load });
                                remaining -= load;
                            }
                        }
                    }
                }
            }

            if (!countedFromScene)
            {
                if (palette == null || palette.Count == 0) return;

                for (int i = 0; i < palette.Count; i++)
                {
                    PaletteColorOverride entry = palette[i];
                    if (entry == null || entry.pixelCount <= 0) continue;

                    Color truckColor = entry.targetColor;
                    if (hasAdjustment)
                    {
                        truckColor = PixelCube.AdjustColor(truckColor, level.ColorBrightness, level.ColorSaturation, level.ColorContrast);
                    }

                    int remaining = entry.pixelCount;
                    while (remaining > 0)
                    {
                        int load = Mathf.Min(capacity, remaining);
                        trucks.Add(new TruckOrder { Color = truckColor, Capacity = load });
                        remaining -= load;
                    }
                }
            }

            for (int i = trucks.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (trucks[i], trucks[j]) = (trucks[j], trucks[i]);
            }

            foreach (TruckOrder order in trucks)
            {
                m_Queue.Enqueue(order);
            }
        }

        public void RefillPool()
        {
            if (m_Pool == null) return;
            foreach (TruckSlot place in m_Pool.Places)
            {
                if (place == null || !place.IsEmpty) continue;
                if (m_Queue.Count == 0) break;

                TruckOrder order = m_Queue.Dequeue();
                SpawnTruckInto(place, order);
            }
        }

        private void SpawnTruckInto(TruckSlot place, TruckOrder order)
        {
            if (m_TruckPrefab == null || place == null) return;

            GameObject truck = Instantiate(m_TruckPrefab);
            truck.name = $"Truck_{ColorUtility.ToHtmlStringRGB(order.Color)}_{order.Capacity}";

            place.AssignTruck(truck.transform, order.Color);

            TruckCargo cargo = truck.GetComponent<TruckCargo>();
            if (cargo == null) cargo = truck.AddComponent<TruckCargo>();
            cargo.ResetCargo(order.Color, order.Capacity);

            TruckPaint paint = truck.GetComponent<TruckPaint>();
            if (paint != null)
            {
                PixelLevelData lvl = GetLevel();
                LevelColorTheme theme = (lvl != null && lvl.ColorTheme != null) ? lvl.ColorTheme : GameThemeSettings.CurrentTheme;
                paint.ApplyTheme(theme, order.Color);
            }

            cargo.EnsureBadge();
            cargo.UpdateBadge(false);

            // Vagon henüz havuzdayken içine oturacak görsel madencileri sadece madenci modu açıksa yerleştir
            if (m_EnableMiners)
            {
                MinerCrew crew = truck.GetComponent<MinerCrew>();
                if (crew == null) crew = truck.AddComponent<MinerCrew>();
                crew.PopulateSeatedMiners(cargo, MinerPrefab, m_MinerScaleFactor, m_MinerRunSpeed);
            }

            MineCartMover mover = truck.GetComponent<MineCartMover>();
            if (mover != null) mover.StopMoving();

            Animator animator = truck.GetComponent<Animator>();
            if (animator != null) animator.speed = 0f;

            // Havuzdaki vagona doğrudan tıklanabilmesi için tıklama hedefi ve tetikleyici collider ekle
            WagonClickTarget clickTarget = truck.GetComponent<WagonClickTarget>();
            if (clickTarget == null) clickTarget = truck.AddComponent<WagonClickTarget>();
            clickTarget.PoolPlace = place;

            BoxCollider box = truck.GetComponent<BoxCollider>();
            if (box == null) box = truck.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.4f, 1.4f, 1.4f);
            box.center = new Vector3(0f, 0.5f, 0f);
        }

        public bool SendToSlot(TruckSlot place)
        {
            if (place == null || place.IsEmpty) return false;

            // 🔒 En ön sıra kuralı: Sadece en ön sıradaki (Row 0) vagonlar slota/raya gönderilebilir!
            if (m_Pool != null && !m_Pool.IsFrontRowPlace(place))
            {
                if (place.Truck != null)
                {
                    StartCoroutine(AnimateLockedWobble(place.Truck));
                }
                return false;
            }

            if (m_PerimeterTrain || m_ContinuousTrain)
            {
                bool sent = SendWagonToMovingFlow(place);
                if (sent && m_Pool != null) m_Pool.UpdateRowVisuals();
                return sent;
            }

            if (m_Slots == null) return false;
            TruckSlot target = FindEmptySlot();
            if (target == null) return false;

            Color color = place.TruckColor;
            Transform truck = place.ReleaseTruck();

            MoveTruckInto(target, truck, color);

            TruckCargo cargo = truck != null ? truck.GetComponent<TruckCargo>() : null;
            if (cargo != null)
            {
                cargo.Filled -= OnCargoFilled;
                cargo.Filled += OnCargoFilled;
            }

            CompactPool();
            RefillPool();
            if (m_Pool != null) m_Pool.UpdateRowVisuals();
            return true;
        }

        private System.Collections.IEnumerator AnimateLockedWobble(Transform target)
        {
            if (target == null) yield break;
            Vector3 origLocal = target.localPosition;
            float elapsed = 0f;
            float duration = 0.22f;

            while (elapsed < duration)
            {
                if (target == null) yield break;
                elapsed += Time.deltaTime;
                float percent = elapsed / duration;
                float offset = Mathf.Sin(percent * Mathf.PI * 8f) * 0.12f * (1f - percent);
                target.localPosition = origLocal + new Vector3(offset, 0f, 0f);
                yield return null;
            }
            if (target != null) target.localPosition = origLocal;
        }

        private void MoveTruckInto(TruckSlot target, Transform truck, Color color)
        {
            if (target == null) return;

            if (truck == null)
            {
                target.AssignTruck(null, color);
                return;
            }

            Vector3 startWorld = truck.position;
            target.AssignTruck(truck, color);

            Vector3 endLocal = truck.localPosition;
            Vector3 startLocal = truck.parent != null
                ? truck.parent.InverseTransformPoint(startWorld)
                : endLocal;

            if (m_Moving.TryGetValue(truck, out Coroutine running) && running != null)
            {
                StopCoroutine(running);
            }

            m_Moving[truck] = StartCoroutine(MoveRoutine(truck, startLocal, endLocal, target));
        }

        private IEnumerator MoveRoutine(Transform truck, Vector3 start, Vector3 end, TruckSlot targetSlot)
        {
            float elapsed = 0f;
            while (elapsed < m_MoveDuration && truck != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / m_MoveDuration);
                float ease = Mathf.SmoothStep(0f, 1f, t);

                Vector3 pos = Vector3.Lerp(start, end, ease);
                pos.z += Mathf.Sin(t * Mathf.PI) * m_MoveArc;
                truck.localPosition = pos;
                yield return null;
            }

            if (truck != null)
            {
                truck.localPosition = end;
                m_Moving.Remove(truck);

                // Slota oturuşta tatmin edici yaylanma (landing bounce)
                bool isParkingSlot = (m_Slots != null && m_Slots.Slots != null && m_Slots.Slots.Contains(targetSlot));
                if (isParkingSlot)
                {
                    truck.DOKill(true);
                    truck.DOPunchScale(new Vector3(0.06f, 0.12f, 0.06f), 0.22f, 4, 0.4f);

                    TruckCargo cargo = truck.GetComponent<TruckCargo>();
                    // Kullanıcı talebi: Vagon slota oturduğunda küpler hemen patlamaz.
                }
            }
        }

        /// <summary>
        /// Vagon slota oturduğunda, tablodaki o renge uyan küpleri dış katmandan içe doğru sırayla cam gibi kırıp vagona akıtır.
        /// </summary>
        private IEnumerator SequentialGlassShatterRoutine(TruckSlot targetSlot, Transform truck, TruckCargo cargo)
        {
            if (cargo == null || cargo.IsFull || cargo.RemainingCapacity <= 0) yield break;

            // Vagonun slota inişinin hemen ardından iniş yaylanmasını hissettirecek kısa bir an
            yield return new WaitForSeconds(0.06f);

            if (truck == null || targetSlot == null || targetSlot.Truck != truck) yield break;

            // 1. Tablodaki tüm aktif ve kırılmamış küpleri topla
            PixelCube[] allCubes = Miner.GetCubesCached();
            if (allCubes == null || allCubes.Length == 0)
            {
                var gen = m_Generator != null ? m_Generator : Object.FindFirstObjectByType<PixelArtGenerator>();
                if (gen != null && gen.CubesContainer != null)
                {
                    allCubes = gen.CubesContainer.GetComponentsInChildren<PixelCube>();
                }
            }

            if (allCubes == null || allCubes.Length == 0) yield break;

            Color targetColor = cargo.CargoColor;

            // 2. Bu renge uyan ve henüz patlamamış / rezerve edilmemiş küpleri filtrele
            List<PixelCube> matching = new List<PixelCube>();
            for (int i = 0; i < allCubes.Length; i++)
            {
                PixelCube c = allCubes[i];
                if (c == null || c.IsPopped || !c.gameObject.activeInHierarchy) continue;
                if (s_ReservedCubes.Contains(c)) continue;

                if (TruckCargo.ColorDistance(c.CurrentColor, targetColor) <= m_ColorThreshold ||
                    TruckCargo.ColorDistance(ClassifyToPalette(c.CurrentColor), targetColor) <= m_ColorThreshold)
                {
                    matching.Add(c);
                }
            }

            if (matching.Count == 0) yield break;

            // 3. Dıştan içe (outside-in) katman derinliği hesapla
            var depthMap = Miner.CalculateLayerDepths(allCubes);

            // Dış katmandan (derinlik 0) başlayarak, aynı katman içinde de vagona yakınlığa göre sırala
            Vector3 truckPos = truck.position;
            matching.Sort((a, b) =>
            {
                int depthA = depthMap.TryGetValue(a, out int dA) ? dA : 0;
                int depthB = depthMap.TryGetValue(b, out int dB) ? dB : 0;
                if (depthA != depthB) return depthA.CompareTo(depthB);

                // Aynı katmanda ise vagona yakın olan önce kırılsın (tatmin edici kavis dalgası)
                float distA = (a.transform.position - truckPos).sqrMagnitude;
                float distB = (b.transform.position - truckPos).sqrMagnitude;
                return distA.CompareTo(distB);
            });

            // Vagonun alabileceği kadar küp rezerve et
            int countToShatter = Mathf.Min(cargo.RemainingCapacity, matching.Count);
            List<PixelCube> targets = new List<PixelCube>(countToShatter);
            for (int i = 0; i < countToShatter; i++)
            {
                targets.Add(matching[i]);
                s_ReservedCubes.Add(matching[i]);
            }

            // 4. Sırayla cam gibi kırıp vagona akıt!
            for (int i = 0; i < targets.Count; i++)
            {
                if (truck == null || cargo == null || cargo.IsFull)
                {
                    for (int rem = i; rem < targets.Count; rem++)
                    {
                        s_ReservedCubes.Remove(targets[rem]);
                    }
                    yield break;
                }

                PixelCube cube = targets[i];
                s_ReservedCubes.Remove(cube);

                if (cube != null && !cube.IsPopped && cube.gameObject.activeInHierarchy)
                {
                    ShatterCubeLikeGlass(cube, truck, cargo, i, targets.Count);
                }

                yield return new WaitForSeconds(m_ShatterInterval);
            }
        }

        private void ShatterCubeLikeGlass(PixelCube cube, Transform truck, TruckCargo cargo, int comboIndex, int totalCombo)
        {
            if (cube == null || cube.IsPopped) return;

            Vector3 cubePos = cube.transform.position;
            Quaternion cubeRot = cube.transform.rotation;
            Vector3 cubeScale = cube.transform.lossyScale;
            Color cubeColor = cube.CurrentColor;

            // 1. Kristal cam kırılma sesi (Combo yükseldikçe müzikal pitch artar!)
            if (VoxelParticleManager.Instance != null)
            {
                float comboPitch = Mathf.Clamp(1.0f + (comboIndex * 0.032f), 0.95f, 1.85f);
                VoxelParticleManager.Instance.PlayGlassShatterSound(comboPitch);
                VoxelParticleManager.Instance.SpawnVoxelBurst(cubePos, cubeScale, cubeColor);
            }

            // 2. Küpün kendisini ve gölgesini anında gizle, etkileşim yöneticisine bildir
            cube.SetPoppedVisualState(true);
            if (PixelCubeInteraction.Instance != null)
            {
                PixelCubeInteraction.Instance.RegisterPoppedCube(cube);
            }

            // 3. Raf koordinatlarını hesapla (belirlenen alt iç pervaz alanı)
            GetShelfArea(out Vector3 shelfCenter, out float shelfMinX, out float shelfMaxX);

            float dropX = Mathf.Clamp(cubePos.x + Random.Range(-0.15f, 0.15f), shelfMinX, shelfMaxX);
            Vector3 dropPos = new Vector3(dropX, shelfCenter.y, shelfCenter.z);

            float targetX = Mathf.Clamp(shelfCenter.x + Random.Range(-m_ShelfGatherRadius, m_ShelfGatherRadius), shelfMinX, shelfMaxX);
            float targetY = shelfCenter.y + Random.Range(0f, m_ShelfStackHeight * 0.8f);
            float targetZ = shelfCenter.z + Random.Range(-0.02f, 0.02f);
            Vector3 shelfTarget = new Vector3(targetX, targetY, targetZ);

            // 4. Blender'dan gelen 12 adet 3D Voronoi kırık parçasını oluştur (belirgin ve iri ölçekli)
            FracturedCubeData fracData = FracturedCubeData.Instance;
            int shardCount = (fracData != null && fracData.ShardCount > 0) ? fracData.ShardCount : 12;

            int arrivedCount = 0;
            bool cubeCountRegistered = false;

            Vector3 shardScale = cubeScale * m_ShardScaleMultiplier;

            for (int s = 0; s < shardCount; s++)
            {
                FracturedCubeData.ShardData shard = (fracData != null) ? fracData.GetShard(s) : default;
                Vector3 localOffset = shard.localOffset;
                Vector3 outwardDir = cubeRot * (shard.outwardDir.sqrMagnitude > 0.001f ? shard.outwardDir : (localOffset.sqrMagnitude > 0.001f ? localOffset.normalized : Vector3.up));

                Vector3 shardStart = cubePos + cubeRot * Vector3.Scale(localOffset * 0.5f, cubeScale);
                Mesh shardMesh = shard.mesh;

                // Vagon kasası içinde hafif rastgele dağılım
                Vector3 targetOffset = Vector3.up * 0.28f + (truck.right * Random.Range(-0.20f, 0.20f)) + (truck.forward * Random.Range(-0.14f, 0.14f));

                float stagger = s * 0.004f;

                CargoFlyer.LaunchGlassShardViaShelfToWagon(
                    shardStart,
                    cubeRot,
                    outwardDir,
                    dropPos,
                    shelfTarget,
                    truck,
                    targetOffset,
                    cubeColor,
                    shardMesh,
                    shardScale,
                    stagger,
                    m_FallToShelfDuration * Random.Range(0.95f, 1.08f),
                    m_PauseOnShelfDuration,
                    m_SlideToWagonDuration * Random.Range(0.95f, 1.08f),
                    () =>
                    {
                        arrivedCount++;
                        if (cargo != null && cargo.Stack != null)
                        {
                            cargo.Stack.AddPiece(1f, shardMesh);
                        }

                        // Parçalar vagona aktığında vagon yükü ve rozeti güncellensin
                        if (!cubeCountRegistered && arrivedCount >= Mathf.Min(3, shardCount))
                        {
                            cubeCountRegistered = true;
                            if (cargo != null)
                            {
                                cargo.LoadOneCube();
                                if (truck != null)
                                {
                                    truck.DOKill(true);
                                    truck.DOPunchScale(new Vector3(0.04f, 0.08f, 0.04f), 0.16f, 3, 0.4f);
                                }
                            }
                        }
                    }
                );
            }
        }

        private void CompactPool()
        {
            if (m_Pool == null) return;
            var places = m_Pool.Places;
            int write = 0;

            for (int read = 0; read < places.Count; read++)
            {
                TruckSlot source = places[read];
                if (source == null || source.IsEmpty) continue;

                if (read != write)
                {
                    TruckSlot target = places[write];
                    if (target != null)
                    {
                        Color color = source.TruckColor;
                        MoveTruckInto(target, source.ReleaseTruck(), color);
                    }
                }
                write++;
            }
        }

        private TruckSlot FindEmptySlot()
        {
            if (m_Slots == null) return null;
            var slots = m_Slots.Slots;
            int count = slots.Count;
            if (count == 0) return null;

            int center = count / 2;
            for (int offset = 0; offset <= count; offset++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    int index = center + offset * side;
                    if (index < 0 || index >= count) continue;

                    TruckSlot slot = slots[index];
                    if (slot != null && slot.IsEmpty) return slot;

                    if (offset == 0) break;
                }
            }
            return null;
        }

        private TruckSlot FindSlotFor(Color cubeColor)
        {
            if (m_Slots == null) return null;
            TruckSlot best = null;
            float bestDistance = float.MaxValue;

            foreach (TruckSlot slot in m_Slots.Slots)
            {
                if (slot == null || slot.IsEmpty) continue;
                TruckCargo cargo = slot.Cargo;
                if (cargo == null || cargo.IsFull) continue;

                float distance = TruckCargo.ColorDistance(cubeColor, cargo.CargoColor);
                if (distance > m_ColorThreshold) continue;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = slot;
                }
            }
            return best;
        }

        private void OnCargoFilled(TruckCargo cargo)
        {
            cargo.Filled -= OnCargoFilled;
            TruckSlot slot = FindSlotOf(cargo.transform);
            if (slot == null) return;
            StartCoroutine(DepartRoutine(slot, cargo.transform));
        }

        private TruckSlot FindSlotOf(Transform truck)
        {
            if (m_Slots == null) return null;
            foreach (TruckSlot slot in m_Slots.Slots)
            {
                if (slot != null && slot.Truck == truck) return slot;
            }
            return null;
        }

        private IEnumerator DepartRoutine(TruckSlot slot, Transform truck)
        {
            yield return new WaitForSeconds(m_DepartDelay);
            slot.ReleaseTruck();
            if (truck == null) yield break;

            MineCartMover mover = truck.GetComponent<MineCartMover>();
            Animator animator = truck.GetComponent<Animator>();

            RectTransform slotRect = slot.SlotRect;
            RectTransform rowRect = slotRect != null ? slotRect.parent as RectTransform : null;

            float railHalf = rowRect != null ? rowRect.rect.width * 0.5f : 1000f;
            float slotX = slotRect != null ? slotRect.anchoredPosition.x : 0f;

            float distance = (railHalf - slotX) + m_DepartExtraDistance;
            float travelled = 0f;
            float elapsed = 0f;
            float worldScale = truck.parent != null ? truck.parent.lossyScale.x : 1f;

            while (travelled < distance && truck != null)
            {
                elapsed += Time.deltaTime;
                float accel = m_DepartAccelTime > 0f
                    ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / m_DepartAccelTime))
                    : 1f;

                float step = m_DepartSpeed * accel * Time.deltaTime;
                travelled += step;
                truck.localPosition += Vector3.right * step;

                if (animator != null) animator.speed = accel;
                else if (mover != null) mover.SpinWheelsByDistance(step * worldScale);

                yield return null;
            }

            if (truck != null) Destroy(truck.gameObject);
        }

        #endregion
    }
}
