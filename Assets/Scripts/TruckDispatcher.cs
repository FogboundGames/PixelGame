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

        [Header("🚂 Hareketli Ray Vagonları (Continuous Train Conveyor)")]
        [Tooltip("Vagonların ray üzerinde sürekli hareket etmesini sağlar (sağdan çıkıp soldan girer).")]
        [SerializeField] private bool m_ContinuousTrain = true;

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
        [SerializeField] private float m_FallToShelfDuration = 0.28f;

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

        private readonly List<MovingWagon> m_MovingWagons = new List<MovingWagon>();
        private readonly List<ShelfCubeGroup> m_ShelfCubes = new List<ShelfCubeGroup>();
        private int m_NextCubeId = 0;
        private Transform m_WagonsRoot;
        private float m_NextCascadeTime = 0f;

        [Header("🚂 Hat ve Havuz Ayarları")]
        [Tooltip("Ray üzerinde aynı anda dolaşabilecek maksimum vagon sayısı")]
        [SerializeField] private int m_MaxTrackWagons = 4;

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
            ClearMovingTrain();

            if (m_Pool != null) m_Pool.gameObject.SetActive(true);

            SpawnMovingTrainRoot();
            ClearPool();
            RebuildStrips();
            BuildQueue();
            RefillPool();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            if (m_ContinuousTrain)
            {
                UpdateMovingTrain();
                UpdateShelfCascadeFlow();
            }
        }

        #region 🚂 Hareketli Vagon Döngüsü (Continuous Train)

        private void ClearMovingTrain()
        {
            for (int i = 0; i < m_MovingWagons.Count; i++)
            {
                if (m_MovingWagons[i] != null && m_MovingWagons[i].GameObject != null)
                {
                    Destroy(m_MovingWagons[i].GameObject);
                }
            }
            m_MovingWagons.Clear();

            if (m_WagonsRoot != null)
            {
                Destroy(m_WagonsRoot.gameObject);
                m_WagonsRoot = null;
            }
        }

        private float m_WagonY = 0f;
        private float m_WagonZ = -22f;
        private Quaternion m_WagonRotation = Quaternion.identity;
        private Vector3 m_WagonScale = Vector3.one * 155f;

        private void CalibratePortalsAndAlignment()
        {
            if (m_Slots == null) return;

            // Portalların X koordinatlarını otomatik kalibre et
            Transform leftPortal = m_Slots.transform.Find("Portal_Left");
            Transform rightPortal = m_Slots.transform.Find("Portal_Right");
            if (leftPortal != null && rightPortal != null)
            {
                m_PortalLeftX = leftPortal.localPosition.x - 100f;
                m_PortalRightX = rightPortal.localPosition.x + 100f;
            }

            // Ray üstündeki vagon rotasyon, ölçek, Y ve Z değerlerini Slot_1'den mükemmel şekilde örnekle
            Vector3 defaultEuler = m_Slots.Style != null ? m_Slots.Style.truckEuler : new Vector3(0f, -90f, -270f);
            m_WagonRotation = Quaternion.Euler(defaultEuler);
            m_WagonScale = Vector3.one * 155f;
            m_WagonY = 0f;
            m_WagonZ = -22f;

            if (m_Slots.Slots != null && m_Slots.Slots.Count > 0 && m_Slots.Slots[0] != null && m_TruckPrefab != null)
            {
                TruckSlot refSlot = m_Slots.Slots[0];
                GameObject sampleObj = Instantiate(m_TruckPrefab, refSlot.SlotRect);
                sampleObj.name = "SampleWagon";
                refSlot.AssignTruck(sampleObj.transform, Color.white);

                m_WagonRotation = sampleObj.transform.localRotation;
                m_WagonScale = sampleObj.transform.localScale;
                m_WagonY = sampleObj.transform.localPosition.y;
                m_WagonZ = sampleObj.transform.localPosition.z;

                refSlot.ReleaseTruck();
                if (Application.isPlaying) Destroy(sampleObj);
                else DestroyImmediate(sampleObj);
            }
        }

        private void SpawnMovingTrainRoot()
        {
            if (m_Slots == null)
            {
                m_Slots = Object.FindFirstObjectByType<TruckSlotRow>();
            }
            if (m_Slots == null) return;

            CalibratePortalsAndAlignment();

            if (m_WagonsRoot != null) return;

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

        /// <summary>
        /// Oyuncunun havuzdan tıkladığı vagonu ray akışına sokar.
        /// Vagon havuzdan havalanıp ray üzerine konar ve sağa doğru ilerlemeye başlar.
        /// </summary>
        public bool SendWagonToMovingFlow(TruckSlot place)
        {
            if (place == null || place.IsEmpty) return false;

            // Ray hattı doluysa vagonu sallayarak oyuncuya geri bildirim ver
            if (m_MovingWagons.Count >= m_MaxTrackWagons)
            {
                if (place.Truck != null)
                {
                    place.Truck.DOKill();
                    place.Truck.DOPunchPosition(Vector3.up * 10f, 0.22f, 8, 0.5f);
                }
                return false;
            }

            Color color = place.TruckColor;
            Transform truck = place.ReleaseTruck();
            if (truck == null) return false;

            // Havuzdaki yerleri öne kaydır ve kuyruktan yenisini getir
            CompactPool();
            RefillPool();

            if (m_WagonsRoot == null)
            {
                SpawnMovingTrainRoot();
            }

            truck.SetParent(m_WagonsRoot, true);

            // Ray hattındaki başlangıç pozisyonu
            float startX = m_PortalLeftX + 220f;
            if (m_MovingWagons.Count > 0)
            {
                float minX = float.MaxValue;
                for (int i = 0; i < m_MovingWagons.Count; i++)
                {
                    if (m_MovingWagons[i].PositionX < minX) minX = m_MovingWagons[i].PositionX;
                }
                startX = Mathf.Max(m_PortalLeftX + 40f, minX - m_MinWagonSpacing);
            }

            Vector3 targetLocalPos = new Vector3(startX, m_WagonY, m_WagonZ);

            // Havuzdan raya tatlı bir zıplama animasyonu
            truck.DOKill();
            truck.DOLocalRotateQuaternion(m_WagonRotation, 0.32f);
            truck.DOScale(m_WagonScale, 0.32f);
            truck.DOLocalJump(targetLocalPos, 120f, 1, 0.36f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                if (truck != null) truck.localPosition = targetLocalPos;
            });

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

            m_MovingWagons.Add(new MovingWagon
            {
                GameObject = truck.gameObject,
                Transform = truck,
                Cargo = cargo,
                Paint = paint,
                Mover = mover,
                Animator = anim,
                PositionX = startX
            });

            return true;
        }

        private void UpdateMovingTrain()
        {
            if (m_MovingWagons.Count == 0) return;

            float step = m_TrainSpeed * Time.deltaTime;
            float worldScale = m_WagonsRoot != null ? m_WagonsRoot.lossyScale.x : 1f;

            for (int i = 0; i < m_MovingWagons.Count; i++)
            {
                MovingWagon wagon = m_MovingWagons[i];
                if (wagon == null || wagon.Transform == null) continue;

                // Öndeki vagon ile güvenli takip mesafesi
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

                wagon.PositionX += moveStep;

                // Sağ portaldan çıkan vagonun durumu
                if (wagon.PositionX > m_PortalRightX)
                {
                    // Dolmuş vagon teslim edildi sayılır ve tünelde tamamlanır (yok edilir)
                    if (wagon.Cargo != null && wagon.Cargo.IsFull)
                    {
                        Destroy(wagon.GameObject);
                        m_MovingWagons.RemoveAt(i);
                        i--;
                        continue;
                    }
                    else
                    {
                        // Henüz dolmamış vagon döngüye devam eder: soldan tekrar hatta girer
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

        /// <summary>
        /// Kırılan küp parçalarını doğrudan çerçevenin altındaki kırmızı alana fırlatır.
        /// 1 küpün tüm parçaları tek bir grup altında toplanır.
        /// </summary>
        /// <summary>
        /// Küp patladığında çağrılır:
        /// 1. Küpü 2-4 adet 3D parçaya böler.
        /// 2. Parçalar çerçevenin hemen altındaki kırmızı işaretli rafa dökülüp birikir (OutBounce).
        /// 3. Parçalar vagona akmaya hazır halde raf kuyruğuna kaydedilir.
        /// Parçalar her zaman patlayan küpün kendi görsel renginde (<paramref name="visualColor"/>) fırlar.
        /// </summary>
        public void NotifyCubePopped(Color cubeColor, Vector3 worldPosition, Color? visualColor = null)
        {
            Color paletteColor = ClassifyToPalette(cubeColor);
            Color pieceColor = visualColor ?? cubeColor;

            int pieces = Random.Range(m_MinPieces, m_MaxPieces + 1);
            CargoStack stack = null;
            if (m_MovingWagons.Count > 0 && m_MovingWagons[0].Cargo != null)
                stack = m_MovingWagons[0].Cargo.Stack;

            float baseSize = stack != null ? stack.BasePieceWorldSize : 0.1f;

            GetShelfArea(out Vector3 shelfCenter, out float shelfMinX, out float shelfMaxX);

            ShelfCubeGroup cubeGroup = new ShelfCubeGroup
            {
                CubeId = ++m_NextCubeId,
                Color = paletteColor,
                TotalFragments = pieces,
                IsFlowing = false
            };
            m_ShelfCubes.Add(cubeGroup);

            for (int i = 0; i < pieces; i++)
            {
                float sizeFactor = Random.Range(m_PieceSizeRange.x, m_PieceSizeRange.y);
                Vector3 start = worldPosition + Random.insideUnitSphere * m_PieceSpread;

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

                cubeGroup.SizeFactors.Add(sizeFactor);

                CargoFlyer.LaunchToShelf(
                    start,
                    dropPos,
                    shelfTarget,
                    pieceColor,
                    baseSize * sizeFactor,
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

                    // Bu küpün TÜM parçalarını (2-4 adet mini voksel) hareket halindeki vagona akıt!
                    for (int f = 0; f < matchingCube.Flyers.Count; f++)
                    {
                        CargoFlyer flyer = matchingCube.Flyers[f];
                        float sizeFactor = (f < matchingCube.SizeFactors.Count) ? matchingCube.SizeFactors[f] : 1f;

                        if (flyer != null)
                        {
                            flyer.FlowToMovingTarget(
                                wagonTarget,
                                m_FlowToCartDuration,
                                m_FlowArcHeight,
                                () =>
                                {
                                    if (targetCargo != null && targetCargo.Stack != null)
                                    {
                                        targetCargo.Stack.AddPiece(sizeFactor);
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
            if (GetShelfArea(out Vector3 shelfCenter, out float shelfMinX, out float shelfMaxX))
            {
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.85f); // Kırmızı raf gizmosu (Toplanma alanı)
                float width = m_ShelfGatherRadius * 2f;
                Gizmos.DrawWireCube(shelfCenter, new Vector3(width, 0.25f + m_ShelfStackHeight, 0.25f));
            }
        }

        #endregion

        #region 🎯 Kurallar & Klasik Mod Uyumluluğu

        public bool CanPop(Color cubeColor)
        {
            if (m_ContinuousTrain) return true; // Parçalar önce rafa birikeceği için küpler her zaman kırılabilir
            if (!m_RequireMatchingTruck) return true;

            return FindSlotFor(ClassifyToPalette(cubeColor)) != null;
        }

        private Color ClassifyToPalette(Color cubeColor)
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
            List<PaletteColorOverride> palette = GetPalette();
            if (palette == null || palette.Count == 0) return;

            var trucks = new List<TruckOrder>();
            int capacity = GetTruckCapacity();
            PixelLevelData level = GetLevel();
            bool hasAdjustment = level != null && (level.ColorBrightness != 1f || level.ColorSaturation != 1f || level.ColorContrast != 1f);

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
                paint.SetBodyColor(order.Color);
                paint.Apply();
            }

            cargo.EnsureBadge();
            cargo.UpdateBadge(false);

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

            if (m_ContinuousTrain)
            {
                return SendWagonToMovingFlow(place);
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
            return true;
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

            m_Moving[truck] = StartCoroutine(MoveRoutine(truck, startLocal, endLocal));
        }

        private IEnumerator MoveRoutine(Transform truck, Vector3 start, Vector3 end)
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
