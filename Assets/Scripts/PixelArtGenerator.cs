using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PixelGame
{
    public enum SamplingMode
    {
        [InspectorName("🎯 Point (Keskin / Pixel Art - Önerilen)")]
        Point,

        [InspectorName("🌊 Bilinear (Yumuşak / Geçişli)")]
        Bilinear
    }

    /// <summary>
    /// Verilen bir piksel görselini okur ve MainCube prefab'larından
    /// sahnedeki mavi çerçeve (MainPlane) içine tam sığacak bir 3D piksel resmi oluşturur.
    /// 1:1 piksel uyumu, renk paleti özelleştirmesi ve canlı güncellemeleri destekler.
    /// </summary>
    [ExecuteAlways]
    [SelectionBase]
    [AddComponentMenu("PixelGame/Pixel Art Generator")]
    public class PixelArtGenerator : MonoBehaviour
    {
        [Header("📦 Prefab & Hedefler")]
        [Tooltip("Oluşturulacak küp prefabı (MainCube.prefab)")]
        [SerializeField] private GameObject m_CubePrefab;

        [Tooltip("Piksel resmi yapılacak kaynak doku (örn. PixelArt_Raccoon)")]
        [SerializeField] private Texture2D m_SourceTexture;

        [Tooltip("Alternatif olarak Sprite seçilebilir")]
        [SerializeField] private Sprite m_SourceSprite;

        [Tooltip("Küplerin yerleştirileceği UI çerçevesi (Canvas altındaki MainPlane)")]
        [SerializeField] private RectTransform m_TargetFrameRect;

        [Tooltip("Hizalamada kullanılacak kamera (boş bırakılırsa Camera.main kullanılır)")]
        [SerializeField] private Camera m_WorldCamera;

        [Header("📋 Aktif Bölüm Verisi")]
        [Tooltip("Şu anda sahnede oluşturulan bölüm verisi (opsiyonel)")]
        [SerializeField] private PixelLevelData m_ActiveLevelData;

        [Header("📐 1:1 Doğal Piksel Uyumu")]
        [Tooltip("Görselin kendi piksel çözünürlüğünü 1:1 kullan (1 Piksel = 1 Küp). Görselin bozulmasını kesinlikle önler!")]
        [SerializeField] private bool m_UseNativeResolution = true;

        [Tooltip("Eğer 'Use Native Resolution' kapalıysa kullanılacak manuel ızgara boyutu (X: Sütun, Y: Satır)")]
        [SerializeField] private Vector2Int m_GridResolution = new Vector2Int(24, 24);

        [Header("🎨 Renk & Kalite Ayarları")]
        [Tooltip("Piksel okuma modu. Pixel Art için Point modu renklerin bulanıklaşmasını engeller.")]
        [SerializeField] private SamplingMode m_SamplingMode = SamplingMode.Point;

        [Tooltip("Renk parlaklığı çarpanı")]
        [Range(0.5f, 2.5f)]
        [SerializeField] private float m_ColorBrightness = 1.0f;

        [Tooltip("Renk doygunluğu çarpanı")]
        [Range(0f, 2.5f)]
        [SerializeField] private float m_ColorSaturation = 1.0f;

        [Tooltip("Renk kontrastı")]
        [Range(0.5f, 2f)]
        [SerializeField] private float m_ColorContrast = 1.0f;

        [Tooltip("Işıma yoğunluğu")]
        [Range(0f, 2f)]
        [SerializeField] private float m_EmissionIntensity = 0.05f;

        [Header("🔲 Izgara & Küp Yerleşimi")]
        [Tooltip("Küpler arasındaki DİKEY (satırlar/önler, Y ekseni) fiziksel boşluk oranı (0 = bitişik, 0.1 = %10 boşluk, negatif = üst üste biner)")]
        [Range(-0.3f, 1.0f)]
        [SerializeField] private float m_CubeSpacing = 0.04f;

        [Tooltip("Küpler arasındaki YATAY (aynı satırdaki yanlar, X ekseni) fiziksel boşluk oranı (0 = bitişik, 0.1 = %10 boşluk, negatif = üst üste biner). Küplerin şeklini/kareliğini bozmaz, aralarındaki mesafeyi açar.")]
        [Range(-0.3f, 1.0f)]
        [SerializeField] private float m_CubeSpacingX = 0.04f;

        [Tooltip("Küplerin Z eksenindeki kalınlığı / derinliği (3D kabartma hissi)")]
        [Range(0.05f, 2f)]
        [SerializeField] private float m_CubeDepth = 0.4f;

        [Tooltip("Mavi çerçevenin iç payı (çerçevenin eğimli kenarlarına taşmaması için)")]
        [Range(0f, 0.3f)]
        [SerializeField] private float m_InnerPadding = 0.08f;

        [Tooltip("Küplerin 3D dünyadaki Z düzlemi mesafesi")]
        [SerializeField] private float m_TargetZ = 0f;

        [Tooltip("Kameraya tam karşıdan (0°) bakıldığında küpün sadece üstü görünür; bu açı küpü öne doğru eğerek hem üst hem ön yüzünü görünür kılar")]
        [Range(0f, 45f)]
        [SerializeField] private float m_CubeFrontTiltAngle = 25f;

        [Tooltip("Her satır (GridY arttıkça) küpün konumuna eklenen serbest X/Y/Z kademesi. Örn. Z=0.12 verirsen her üst satır bir öncekinden 0.12 birim daha ileri/geri kayar; X veya Y'ye değer verirsen satırlar yana/yukarı da kayabilir. Sahnede canlı görmek için 'Preserve Scene Edits' kapalı olmalı.")]
        [SerializeField] private Vector3 m_CubeRowStepOffset = new Vector3(0f, 0f, 0.12f);

        [Tooltip("Şeffaf (alpha < 0.1) pikseller için küp oluşturulmasın mı?")]
        [SerializeField] private bool m_SkipTransparent = true;

        [Tooltip("Oyun başladığında otomatik oluştursun mu?")]
        [SerializeField] private bool m_GenerateOnStart = false;

        [Header("🔒 Sahne Koruması (Preserve Scene Edits)")]
        [Tooltip("Açık olduğunda sahnede yaptığınız tüm değişiklikler (silinen küpler, taşınan parçalar, silinen çerçeve/raylar) Play'e basıldığında KESİNLİKLE korunur; hiçbir şey yeniden üretilip sahneyi bozmaz.")]
        [SerializeField] private bool m_PreserveSceneEdits = true;

        [Header("🌑 Küp Altı Sahte Gölge (Fake Shadow - Her Yönde)")]
        [Tooltip("Her bir piksel küpünün altına 360 derece çevreleyen yumuşak sahte gölge yerleştir")]
        [SerializeField] private bool m_EnableCubeShadows = false;
        [SerializeField] private Material m_CubeShadowMaterial;
        [SerializeField] private Vector2 m_ShadowOffset = new Vector2(0.04f, -0.08f); // Sağa ve aşağı düşen belirgin, doğal sahte gölge
        [SerializeField] private float m_ShadowScale = 1.34f;                     // Küpün altından ve kenarlarından taşarak 3D derinlik katan gölge boyutu
        [SerializeField] private Color m_ShadowColor = new Color(0.04f, 0.06f, 0.14f, 0.75f); // Belirgin, tok ve estetik sahte gölge tonu

        [Header("🌑 Şekil Çevresi Kontur Gölgesi (Figure Contour Shadow)")]
        [Tooltip("Tüm piksel figürünün arkasını kaplayan genel siluet gölgesi (Küp patlayınca arkada iz kalmaması için varsayılan kapalı)")]
        [SerializeField] private bool m_EnableFigureContourShadow = false;
        [SerializeField] [Range(0f, 1f)] private float m_FigureShadowOpacity = 0.95f;
        [SerializeField] private Vector2 m_FigureShadowOffset = new Vector2(0f, 0f);
        [SerializeField] [Range(0.9f, 1.4f)] private float m_FigureShadowScale = 1.05f;
        [SerializeField] private Texture2D m_CustomFigureShadowTexture;

        [Header("🖼️ Pano Çevresi Sahte Gölge (Board Frame Shadow)")]
        [Tooltip("Piksel panosunun 4 kenarını çevreleyen yumuşak sahte gölge (Görselde kırmızı çizilen çerçeve gölgesi)")]
        [SerializeField] private bool m_EnableBoardShadow = false;
        [SerializeField] private Material m_BoardShadowMaterial;
        [SerializeField] private Vector2 m_BoardShadowOffset = new Vector2(0.04f, -0.06f);
        [SerializeField] [Range(1.0f, 1.35f)] private float m_BoardShadowScale = 1.12f;
        [SerializeField] [Range(0f, 1f)] private float m_BoardShadowOpacity = 0.85f;
        [SerializeField] private Color m_BoardShadowColor = new Color(0.04f, 0.06f, 0.14f, 0.85f);

        [Header("📂 Kapsayıcı (Container)")]
        [SerializeField] private Transform m_CubesContainer;

        [Header("🖼️ 3D Sahne Çerçeve & Pano (Scene Frame & Board)")]
        [Tooltip("Scene View'da tasarım yaparken çerçevenin 3D dünyada (küplerin hemen arkasında) net görünmesini sağlar")]
        [SerializeField] private bool m_ShowSceneFramePreview = true;
        [Tooltip("Çerçevenin içindeki beyaz tuval/pano zeminini Scene View'da gösterir")]
        [SerializeField] private bool m_ShowSceneBoardBackground = true;
        [SerializeField] private Transform m_FramePreviewTransform;

        // Properties
        public bool EnableCubeShadows { get => m_EnableCubeShadows; set { m_EnableCubeShadows = value; ApplyShadowsToAllExistingCubes(); } }
        public Material CubeShadowMaterial { get => m_CubeShadowMaterial; set { m_CubeShadowMaterial = value; ApplyShadowsToAllExistingCubes(); } }
        public Vector2 ShadowOffset { get => m_ShadowOffset; set { m_ShadowOffset = value; ApplyShadowsToAllExistingCubes(); } }
        public float ShadowScale { get => m_ShadowScale; set { m_ShadowScale = value; ApplyShadowsToAllExistingCubes(); } }

        public bool EnableFigureContourShadow { get => m_EnableFigureContourShadow; set { m_EnableFigureContourShadow = value; UpdateContourShadowLive(); } }
        public float FigureShadowOpacity { get => m_FigureShadowOpacity; set { m_FigureShadowOpacity = value; UpdateContourShadowLive(); } }
        public Vector2 FigureShadowOffset { get => m_FigureShadowOffset; set { m_FigureShadowOffset = value; UpdateContourShadowLive(); } }
        public float FigureShadowScale { get => m_FigureShadowScale; set { m_FigureShadowScale = value; UpdateContourShadowLive(); } }
        public Texture2D CustomFigureShadowTexture { get => m_CustomFigureShadowTexture; set { m_CustomFigureShadowTexture = value; UpdateContourShadowLive(); } }

        public bool EnableBoardShadow { get => m_EnableBoardShadow; set { m_EnableBoardShadow = value; UpdateBoardShadowLive(); } }
        public Material BoardShadowMaterial { get => m_BoardShadowMaterial; set { m_BoardShadowMaterial = value; UpdateBoardShadowLive(); } }
        public Vector2 BoardShadowOffset { get => m_BoardShadowOffset; set { m_BoardShadowOffset = value; UpdateBoardShadowLive(); } }
        public float BoardShadowScale { get => m_BoardShadowScale; set { m_BoardShadowScale = value; UpdateBoardShadowLive(); } }
        public float BoardShadowOpacity { get => m_BoardShadowOpacity; set { m_BoardShadowOpacity = value; UpdateBoardShadowLive(); } }
        public Color BoardShadowColor { get => m_BoardShadowColor; set { m_BoardShadowColor = value; UpdateBoardShadowLive(); } }

        public GameObject CubePrefab { get => m_CubePrefab; set => m_CubePrefab = value; }
        public Texture2D SourceTexture { get => m_SourceTexture; set => m_SourceTexture = value; }
        public Sprite SourceSprite { get => m_SourceSprite; set => m_SourceSprite = value; }
        public RectTransform TargetFrameRect { get => m_TargetFrameRect; set => m_TargetFrameRect = value; }
        public PixelLevelData ActiveLevelData { get => m_ActiveLevelData; set => m_ActiveLevelData = value; }
        public bool UseNativeResolution { get => m_UseNativeResolution; set => m_UseNativeResolution = value; }
        public Vector2Int GridResolution { get => m_GridResolution; set => m_GridResolution = value; }
        public float CubeSpacing { get => m_CubeSpacing; set => m_CubeSpacing = value; }
        public float CubeSpacingX { get => m_CubeSpacingX; set => m_CubeSpacingX = value; }
        public float CubeDepth { get => m_CubeDepth; set => m_CubeDepth = value; }
        public float InnerPadding { get => m_InnerPadding; set => m_InnerPadding = value; }
        public float ColorBrightness { get => m_ColorBrightness; set { m_ColorBrightness = value; UpdateExistingCubesLive(); } }
        public float ColorSaturation { get => m_ColorSaturation; set { m_ColorSaturation = value; UpdateExistingCubesLive(); } }
        public float ColorContrast { get => m_ColorContrast; set { m_ColorContrast = value; UpdateExistingCubesLive(); } }
        public float EmissionIntensity { get => m_EmissionIntensity; set { m_EmissionIntensity = value; UpdateExistingCubesLive(); } }
        public SamplingMode Sampling { get => m_SamplingMode; set => m_SamplingMode = value; }
        public bool SkipTransparent { get => m_SkipTransparent; set => m_SkipTransparent = value; }
        public bool PreserveSceneEdits { get => m_PreserveSceneEdits; set => m_PreserveSceneEdits = value; }
        public Transform CubesContainer
        {
            get
            {
                if (m_CubesContainer == null)
                {
                    m_CubesContainer = transform.Find("PixelArtContainer");
                    if (m_CubesContainer == null)
                    {
                        PixelCube anyCube = GetComponentInChildren<PixelCube>(true);
                        if (anyCube != null && anyCube.transform.parent != null)
                        {
                            m_CubesContainer = anyCube.transform.parent;
                        }
                    }
                }
                return m_CubesContainer;
            }
        }
        public float TargetZ => m_TargetZ;
        public Camera WorldCamera { get => GetActiveCamera(); set => m_WorldCamera = value; }
        public bool ShowSceneFramePreview { get => m_ShowSceneFramePreview; set { m_ShowSceneFramePreview = value; EnsureWorldFramePreview(true); } }
        public bool ShowSceneBoardBackground { get => m_ShowSceneBoardBackground; set { m_ShowSceneBoardBackground = value; EnsureWorldFramePreview(true); } }

        private void Awake()
        {
            EnsureInteractionComponents();
            if (!m_EnableBoardShadow) EnsureBoardShadowDisabled();
            if (!m_EnableFigureContourShadow) EnsureFigureContourShadowDisabled();
        }

        private void OnEnable()
        {
            EnsureInteractionComponents();

            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.delayCall += EnsureScenePreviewInEditor;
            }
            #endif
        }

        #if UNITY_EDITOR
        /// <summary>
        /// Oyunu başlatmadan da (Edit Mode'da) sahne görünümünde tüm resmi ve gölgeleri canlı önizletir!
        /// </summary>
        public void EnsureScenePreviewInEditor()
        {
            if (this == null || Application.isPlaying) return;

            // Sahne koruması aktifse kullanıcının düzenlediği sahneye KESİNLİKLE dokunma!
            if (m_PreserveSceneEdits) return;

            // Sahnede zaten küpler mevcutsa editör açılışında veya domain reload sırasında
            // bunları boş yere silip sıfırdan oluşturma!
            if (CubesContainer != null && CubesContainer.childCount > 0)
            {
                return;
            }

            // Sahnede hiç küp yoksa ve otomatik üretim açıksa oluştur
            if (m_GenerateOnStart)
            {
                GeneratePixelArt();
            }
        }
        #endif

        private void Start()
        {
            EnsureInteractionComponents();

            if (Application.isPlaying)
            {
                // Sahne koruması: Kullanıcının sildiği, taşıdığı veya değiştirdiği hiçbir nesneyi sıfırlama!
                if (m_PreserveSceneEdits)
                {
                    // Yine de mevcut küpleri SİLMEDEN/yeniden RENKLENDİRMEDEN, o anki gerçek
                    // ekran/Game View çözünürlüğüne göre pozisyon ve boyutlarını tazele.
                    // Edit Mode önizlemesi farklı bir pencere boyutunda/oranında yapılmış olabilir;
                    // bu yapılmazsa resim Play Mode'a girince küçük veya kaymış görünür.
                    if (CubesContainer != null && CubesContainer.childCount > 0)
                    {
                        StartCoroutine(RefreshLayoutNextFrame());
                    }
                    return;
                }

                if (!m_EnableBoardShadow) EnsureBoardShadowDisabled();
                if (!m_EnableFigureContourShadow) EnsureFigureContourShadowDisabled();
                if (!m_EnableCubeShadows) ApplyShadowsToAllExistingCubes();

                if (m_GenerateOnStart && (CubesContainer == null || CubesContainer.childCount == 0))
                {
                    GeneratePixelArt();
                }
                else
                {
                    if (m_EnableCubeShadows)
                    {
                        ApplyShadowsToAllExistingCubes();
                    }
                    if (m_EnableFigureContourShadow)
                    {
                        UpdateContourShadowLive();
                    }
                    if (m_EnableBoardShadow)
                    {
                        UpdateBoardShadowLive();
                    }
                }
            }
        }

        /// <summary>
        /// Play Mode'un ilk karesinde Canvas/CanvasScaler henüz kendi boyutunu netleştirmemiş
        /// olabilir (script çalışma sırası garanti değildir). Bir kare bekleyip UI düzeni
        /// oturduktan SONRA gerçek çözünürlüğe göre küp pozisyon/boyutlarını tazeler.
        /// </summary>
        private IEnumerator RefreshLayoutNextFrame()
        {
            yield return null;
            if (this == null || !Application.isPlaying) yield break;
            if (CubesContainer != null && CubesContainer.childCount > 0)
            {
                UpdateExistingCubesTransforms();
            }
        }

        public void EnsureInteractionComponents()
        {
            if (GetComponent<PixelCubeInteraction>() == null)
            {
                gameObject.AddComponent<PixelCubeInteraction>();
            }
        }

        private void OnValidate()
        {
            if (m_CubesContainer != null && m_CubesContainer.childCount > 0)
            {
                UpdateExistingCubesTransforms();
                UpdateExistingCubesLive();
                #if UNITY_EDITOR
                if (m_EnableCubeShadows)
                {
                    EditorApplication.delayCall -= DeferredApplyShadows;
                    EditorApplication.delayCall += DeferredApplyShadows;
                }
                if (m_EnableFigureContourShadow)
                {
                    EditorApplication.delayCall -= DeferredApplyContourShadow;
                    EditorApplication.delayCall += DeferredApplyContourShadow;
                }
                if (m_EnableBoardShadow)
                {
                    EditorApplication.delayCall -= DeferredApplyBoardShadow;
                    EditorApplication.delayCall += DeferredApplyBoardShadow;
                }
                #endif
            }

            #if UNITY_EDITOR
            EnsureWorldFramePreview(forceCreate: false);
            #endif
        }

        private void Update()
        {
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (m_ShowSceneFramePreview && (transform.hasChanged || (m_CubesContainer != null && m_CubesContainer.hasChanged)))
                {
                    transform.hasChanged = false;
                    if (m_CubesContainer != null) m_CubesContainer.hasChanged = false;
                    EnsureWorldFramePreview(forceCreate: false);
                }
            }
            #endif
        }

        #if UNITY_EDITOR
        private void DeferredApplyShadows()
        {
            if (this == null) return;
            ApplyShadowsToAllExistingCubes();
        }

        private void DeferredApplyContourShadow()
        {
            if (this == null) return;
            UpdateContourShadowLive();
        }

        private void DeferredApplyBoardShadow()
        {
            if (this == null) return;
            UpdateBoardShadowLive();
        }
        #endif

        /// <summary>
        /// Bir LevelData varlığını yükler, renk paletini hazırlar ve küpleri otomatik oluşturur.
        /// </summary>
        public void LoadLevel(PixelLevelData levelData)
        {
            if (levelData == null) return;

            m_ActiveLevelData = levelData;
            m_SourceTexture = levelData.LevelTexture;
            m_SourceSprite = levelData.LevelSprite;
            m_UseNativeResolution = levelData.UseNativeResolution;
            m_GridResolution = levelData.CustomResolution;
            m_CubeSpacing = levelData.CubeSpacing;
            m_CubeSpacingX = levelData.CubeSpacingX;
            m_CubeDepth = levelData.CubeDepth;
            m_CubeFrontTiltAngle = levelData.CubeFrontTiltAngle;
            m_TargetZ = levelData.TargetZ;
            m_CubeRowStepOffset = levelData.CubeRowStepOffset;
            m_InnerPadding = levelData.InnerPadding;
            m_ColorBrightness = levelData.ColorBrightness;
            m_ColorSaturation = levelData.ColorSaturation;
            m_ColorContrast = levelData.ColorContrast;
            m_EmissionIntensity = levelData.EmissionIntensity;
            m_SkipTransparent = levelData.SkipTransparent;

            // Palet boşsa otomatik çıkar
            if (levelData.ColorPalette.Count == 0 && levelData.GetActiveTexture() != null)
            {
                levelData.ExtractPaletteFromTexture();
            }

            GeneratePixelArt();

            // Bölüme bağlı sistemler (kamyon kuyruğu gibi) kendilerini yenilesin
            LevelLoaded?.Invoke(levelData);
        }

        /// <summary>
        /// Generator'daki güncel düzen ayarlarını (Inspector'da canlı ayarlanmış olanlar dahil)
        /// o an bağlı olan PixelLevelData asset'ine geri yazar ve diske kaydeder.
        /// Bu çağrılmadan Inspector'daki değişiklikler sadece sahnedeki canlı objede kalır;
        /// başka bir seviyeye geçip geri dönüldüğünde LoadLevel() eski (kaydedilmemiş) değerleri
        /// yeniden yükler ve yapılan ayar kaybolur.
        /// </summary>
        public void SaveToActiveLevel()
        {
            if (m_ActiveLevelData == null) return;

            m_ActiveLevelData.UseNativeResolution = m_UseNativeResolution;
            m_ActiveLevelData.CustomResolution = m_GridResolution;
            m_ActiveLevelData.CubeSpacing = m_CubeSpacing;
            m_ActiveLevelData.CubeSpacingX = m_CubeSpacingX;
            m_ActiveLevelData.CubeDepth = m_CubeDepth;
            m_ActiveLevelData.CubeFrontTiltAngle = m_CubeFrontTiltAngle;
            m_ActiveLevelData.TargetZ = m_TargetZ;
            m_ActiveLevelData.CubeRowStepOffset = m_CubeRowStepOffset;
            m_ActiveLevelData.InnerPadding = m_InnerPadding;
            m_ActiveLevelData.ColorBrightness = m_ColorBrightness;
            m_ActiveLevelData.ColorSaturation = m_ColorSaturation;
            m_ActiveLevelData.ColorContrast = m_ColorContrast;
            m_ActiveLevelData.EmissionIntensity = m_EmissionIntensity;
            m_ActiveLevelData.SkipTransparent = m_SkipTransparent;

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(m_ActiveLevelData);
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log($"<color=#00FFAA><b>[PixelArtGenerator]</b></color> Ayarlar '{m_ActiveLevelData.LevelName}' seviyesine kaydedildi.");
#endif
        }

        /// <summary>
        /// Sahnede zaten var olan küpleri silmeden seviye verisini bağlar ve dinleyicileri tetikler.
        /// Böylece sahneye önceden yerleştirilen küpler ve sahte gölgeler sıfırlanmaz.
        /// </summary>
        public void BindExistingLevel(PixelLevelData levelData)
        {
            if (levelData == null) return;

            m_ActiveLevelData = levelData;
            m_SourceTexture = levelData.LevelTexture;
            m_SourceSprite = levelData.LevelSprite;

            if (levelData.ColorPalette.Count == 0 && levelData.GetActiveTexture() != null)
            {
                levelData.ExtractPaletteFromTexture();
            }

            if (m_EnableCubeShadows)
            {
                ApplyShadowsToAllExistingCubes();
            }
            if (m_EnableFigureContourShadow)
            {
                UpdateContourShadowLive();
            }
            if (m_EnableBoardShadow)
            {
                UpdateBoardShadowLive();
            }

            LevelLoaded?.Invoke(levelData);
        }

        /// <summary>
        /// Seviye yüklendiğinde dışarıdan dinleyicileri bilgilendirmek için statik tetikleyici.
        /// </summary>
        public static void TriggerLevelLoaded(PixelLevelData levelData)
        {
            LevelLoaded?.Invoke(levelData);
        }

        /// <summary>
        /// Bir bölüm yüklenip küpleri oluşturulduğunda tetiklenir.
        /// Bölümün paletine göre kurulan sistemler bunu dinleyerek kendilerini yeniler.
        /// </summary>
        public static event System.Action<PixelLevelData> LevelLoaded;

        /// <summary>
        /// Sahnede var olan küplerin renklerini anında günceller.
        /// </summary>
        public void UpdateExistingCubesLive()
        {
            if (m_CubesContainer == null) return;

            Texture2D activeTex = GetActiveTexture();
            GetEffectiveGridSize(activeTex, out int cols, out int rows);

            PixelCube[] cubes = m_CubesContainer.GetComponentsInChildren<PixelCube>();
            foreach (var cube in cubes)
            {
                if (activeTex != null)
                {
                    Color rawColor = SampleRawColor(activeTex, cube.GridX, cube.GridY, cols, rows);
                    cube.SetColor(rawColor);
                }

                cube.UpdateColorAdjustments(m_ColorBrightness, m_ColorSaturation, m_ColorContrast, m_EmissionIntensity);
            }
        }

        /// <summary>
        /// Sahnede var olan küplerin boyutunu, aralığını (CubeSpacing), derinliğini ve merkezini canlı olarak günceller.
        /// Kapsayıcının (PixelArtContainer) olası kayma ve orantısız scale bozukluklarını da otomatik düzeltir.
        /// </summary>
        [ContextMenu("📐 Küp Boyut ve Boşluklarını Canlı Güncelle (Update Spacing)")]
        public void UpdateExistingCubesTransforms()
        {
            if (m_CubesContainer == null) EnsureContainer();
            if (m_CubesContainer == null) return;

            PixelCube[] cubes = m_CubesContainer.GetComponentsInChildren<PixelCube>(true);
            if (cubes == null || cubes.Length == 0) return;

            Camera cam = GetActiveCamera();
            if (cam == null) return;

            // Kapsayıcının transformundaki istenmeyen ofset veya orantısız scale'ı ray köküne eşitle
            Transform wagonsRoot = GameObject.Find("[PerimeterWagonsRoot]")?.transform;
            if (wagonsRoot != null && m_CubesContainer.parent == wagonsRoot.parent)
            {
                m_CubesContainer.localPosition = wagonsRoot.localPosition;
                m_CubesContainer.localRotation = wagonsRoot.localRotation;
                m_CubesContainer.localScale = wagonsRoot.localScale;
            }

            if (!CalculateTargetWorldBounds(cam, out Vector3 worldCenter, out float worldWidth, out float worldHeight))
                return;

            Texture2D activeTex = GetActiveTexture();
            GetEffectiveGridSize(activeTex, out int cols, out int rows);
            if (cols <= 0 || rows <= 0) { cols = 24; rows = 24; }

            // Görseldeki şeffaf olmayan (dolu) piksellerin sınırlarını ve merkezini tespit et
            int minX = cols, maxX = -1, minY = rows, maxY = -1;
            if (m_SkipTransparent)
            {
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < cols; x++)
                    {
                        Color c = SampleRawColor(activeTex, x, y, cols, rows);
                        if (c.a >= 0.1f)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                }
            }

            float cellSize;
            Vector3 startPos;
            float stepX;
            float stepY;

            if (m_SkipTransparent && maxX >= minX && maxY >= minY)
            {
                int visW = maxX - minX + 1;
                int visH = maxY - minY + 1;
                float visCenterX = (minX + maxX) * 0.5f;
                float visCenterY = (minY + maxY) * 0.5f;

                float visCellSizeX = (worldWidth * 0.88f) / visW;
                float visCellSizeY = (worldHeight * 0.88f) / visH;
                cellSize = Mathf.Min(visCellSizeX, visCellSizeY);

                stepX = cellSize * (1f + m_CubeSpacingX);
                stepY = cellSize * (1f + m_CubeSpacing);

                startPos = new Vector3(
                    worldCenter.x - visCenterX * stepX,
                    worldCenter.y - visCenterY * stepY,
                    m_TargetZ
                );
            }
            else
            {
                float cellSizeX = worldWidth / cols;
                float cellSizeY = worldHeight / rows;
                cellSize = Mathf.Min(cellSizeX, cellSizeY);

                stepX = cellSize * (1f + m_CubeSpacingX);
                stepY = cellSize * (1f + m_CubeSpacing);
                float totalWidth = (cols - 1) * stepX;
                float totalHeight = (rows - 1) * stepY;

                startPos = new Vector3(
                    worldCenter.x - totalWidth * 0.5f,
                    worldCenter.y - totalHeight * 0.5f,
                    m_TargetZ
                );
            }

            Vector3 cubeScale = new Vector3(
                cellSize,
                cellSize,
                cellSize * m_CubeDepth
            );

            Quaternion tiltedRot = Quaternion.Euler(m_CubeFrontTiltAngle, 0f, 0f);

            for (int i = 0; i < cubes.Length; i++)
            {
                PixelCube cube = cubes[i];
                if (cube == null) continue;

                Vector3 pos = startPos + new Vector3(cube.GridX * stepX, cube.GridY * stepY, 0f) + cube.GridY * m_CubeRowStepOffset;
                cube.transform.position = pos;
                cube.transform.rotation = tiltedRot;
                cube.transform.localScale = cubeScale;
            }
        }

        /// <summary>
        /// Projenin mevcut URP / Işık gölge durumunu kontrol eder.
        /// Eğer Directional Light üzerinde gerçek zamanlı yumuşak gölgeler aktifse (PC/High),
        /// her bir küpün altındaki sahte gölgeleri (Fake Shadow Quad'ları) otomatik gizleyerek/temizleyerek
        /// gereksiz 1200+ Overdraw ve Draw Call oluşmasını engeller.
        /// Gerçek gölgeler pasifse (Mobile/Low) Sahte Gölgelerin çizilmesine izin verir.
        /// </summary>
        public bool EvaluateShadowMode()
        {
            if (!m_EnableCubeShadows) return false;

            Light mainLight = RenderSettings.sun;
            if (mainLight == null)
            {
                Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (var l in lights)
                {
                    if (l.type == LightType.Directional && l.enabled)
                    {
                        mainLight = l;
                        break;
                    }
                }
            }

            if (mainLight != null && mainLight.shadows != LightShadows.None && mainLight.shadowStrength > 0.1f)
            {
                return false; // Real-time URP shadow active -> disable fake shadow quads to avoid overdraw!
            }

            return true; // Real-time shadow off -> enable fake shadow quads
        }

        /// <summary>
        /// Küpleri mavi çerçeveye göre hesaplayarak 1:1 piksel uyumuyla oluşturur.
        /// </summary>
        [ContextMenu("Piksel Resmi Oluştur")]
        public void GeneratePixelArt()
        {
            Texture2D activeTex = GetActiveTexture();
            if (activeTex == null)
            {
                Debug.LogWarning("[PixelArtGenerator] Kaynak doku seçilmedi!");
                return;
            }

            if (m_CubePrefab == null)
            {
                #if UNITY_EDITOR
                string[] guids = AssetDatabase.FindAssets("MainCube t:Prefab");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    m_CubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
                #endif

                if (m_CubePrefab == null)
                {
                    Debug.LogError("[PixelArtGenerator] MainCube prefab'ı bulunamadı!");
                    return;
                }
            }

            EnsureTargetFrameRect();

            Camera cam = GetActiveCamera();
            if (cam == null)
            {
                Debug.LogError("[PixelArtGenerator] Sahne kamerası (Camera.main) bulunamadı!");
                return;
            }

            // 1. Hedef dünya alanı sınırlarını hesapla
            if (!CalculateTargetWorldBounds(cam, out Vector3 worldCenter, out float worldWidth, out float worldHeight))
            {
                // Oyun içinde bu genellikle Canvas düzeninin ilk karede henüz hazır olmamasıdır;
                // bir kare sonra tekrar denemek yeterli olur.
                if (Application.isPlaying)
                {
                    ScheduleGenerateRetry();
                    return;
                }
                #if UNITY_EDITOR
                else
                {
                    // Editör açılışında GameView veya Canvas henüz hazır değilse biraz sonra tekrar denesin
                    EditorApplication.delayCall -= EnsureScenePreviewInEditor;
                    EditorApplication.delayCall += EnsureScenePreviewInEditor;
                    return;
                }
                #else
                Debug.LogError("[PixelArtGenerator] Hedef çerçevenin dünya koordinatları hesaplanamadı!");
                return;
                #endif
            }

            // 2. Izgara boyutlarını belirle (1:1 piksel koruması)
            GetEffectiveGridSize(activeTex, out int cols, out int rows);

            if (cols <= 0 || rows <= 0)
            {
                cols = 24;
                rows = 24;
            }

            // Görseldeki şeffaf olmayan (dolu) piksellerin sınırlarını ve merkezini tespit et
            int minX = cols, maxX = -1, minY = rows, maxY = -1;
            if (m_SkipTransparent)
            {
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < cols; x++)
                    {
                        Color c = SampleRawColor(activeTex, x, y, cols, rows);
                        if (c.a >= 0.1f)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                }
            }

            float cellSize;
            Vector3 startPos;
            float totalWidth;
            float totalHeight;

            float stepX;
            float stepY;

            // Eğer şeffaf arkaplanlı izole bir figür varsa (örn. Kalp), sadece figürün dolu sınırlarını çerçeveye yay ve tam merkeze oturt
            if (m_SkipTransparent && maxX >= minX && maxY >= minY)
            {
                int visW = maxX - minX + 1;
                int visH = maxY - minY + 1;
                float visCenterX = (minX + maxX) * 0.5f;
                float visCenterY = (minY + maxY) * 0.5f;

                // Dolu figürün çerçevenin içini ferahça dolduracağı hücre boyutu (%88 çerçeve oranı)
                float visCellSizeX = (worldWidth * 0.88f) / visW;
                float visCellSizeY = (worldHeight * 0.88f) / visH;
                cellSize = Mathf.Min(visCellSizeX, visCellSizeY);

                stepX = cellSize * (1f + m_CubeSpacingX);
                stepY = cellSize * (1f + m_CubeSpacing);
                totalWidth = visW * stepX;
                totalHeight = visH * stepY;

                // Figürün geometrik merkezini hedef çerçevenin tam ortasına (worldCenter) oturt
                startPos = new Vector3(
                    worldCenter.x - visCenterX * stepX,
                    worldCenter.y - visCenterY * stepY,
                    m_TargetZ
                );
            }
            else
            {
                // Tam dolu kare veya opak görseller için standart matris yerleşimi
                float cellSizeX = worldWidth / cols;
                float cellSizeY = worldHeight / rows;
                cellSize = Mathf.Min(cellSizeX, cellSizeY);

                stepX = cellSize * (1f + m_CubeSpacingX);
                stepY = cellSize * (1f + m_CubeSpacing);
                totalWidth = cols * stepX;
                totalHeight = rows * stepY;

                startPos = new Vector3(
                    worldCenter.x - totalWidth * 0.5f + stepX * 0.5f,
                    worldCenter.y - totalHeight * 0.5f + stepY * 0.5f,
                    m_TargetZ
                );
            }

            Vector3 cubeScale = new Vector3(
                cellSize,
                cellSize,
                cellSize * m_CubeDepth
            );

            // 4. Eski küpleri temizle
            ClearCubes();

            // 5. Kapsayıcıyı hazırla
            EnsureContainer();

            bool useFakeShadows = EvaluateShadowMode();
            Material shadowMat = useFakeShadows ? GetOrCreateShadowMaterial() : null;

            // 6. Pikselleri oku ve küpleri oluştur
            int createdCount = 0;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    Color rawColor = SampleRawColor(activeTex, x, y, cols, rows);

                    // Şeffaf piksel kontrolü
                    if (m_SkipTransparent && rawColor.a < 0.1f)
                        continue;

                    Color adjustedColor = PixelCube.AdjustColor(rawColor, m_ColorBrightness, m_ColorSaturation, m_ColorContrast);

                    Vector3 pos = startPos + new Vector3(x * stepX, y * stepY, 0f) + y * m_CubeRowStepOffset;
                    Quaternion rot = Quaternion.Euler(m_CubeFrontTiltAngle, 0f, 0f);

                    GameObject cubeObj;
                    #if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        cubeObj = (GameObject)PrefabUtility.InstantiatePrefab(m_CubePrefab, m_CubesContainer);
                        cubeObj.transform.position = pos;
                        cubeObj.transform.rotation = rot;
                        cubeObj.transform.localScale = cubeScale;
                        cubeObj.name = $"Pixel_{x}_{y}";
                        Undo.RegisterCreatedObjectUndo(cubeObj, "Generate Pixel Cube");
                    }
                    else
                    #endif
                    {
                        cubeObj = Instantiate(m_CubePrefab, pos, rot, m_CubesContainer);
                        cubeObj.transform.localScale = cubeScale;
                        cubeObj.name = $"Pixel_{x}_{y}";
                    }

                    // PixelCube bileşeni ekle ve renklendir
                    PixelCube pixelCube = cubeObj.GetComponent<PixelCube>();
                    if (pixelCube == null)
                    {
                        pixelCube = cubeObj.AddComponent<PixelCube>();
                    }

                    pixelCube.Initialize(x, y, rawColor, m_EmissionIntensity);
                    pixelCube.ApplyColor(adjustedColor, m_EmissionIntensity);

                    // Küp altına sahte gölge (Fake Shadow) ekle
                    if (useFakeShadows && shadowMat != null)
                    {
                        pixelCube.EnsureShadow(shadowMat, m_ShadowOffset, m_ShadowScale, m_ShadowColor);
                    }

                    createdCount++;
                }
            }

            // 7. Kontur gölgesi ayarı açıksa uygula, kapalıysa temizle
            if (m_EnableFigureContourShadow)
            {
                EnsureFigureContourShadow(worldCenter, totalWidth, totalHeight);
            }
            else
            {
                EnsureFigureContourShadowDisabled();
            }

            // 8. Pano çevresi sahte gölge (Board Frame Shadow - Kullanıcının kırmızıyla çizdiği 4 kenar gölgesi)
            if (m_EnableBoardShadow)
            {
                EnsureBoardShadow(worldCenter, totalWidth, totalHeight);
            }
            else
            {
                EnsureBoardShadowDisabled();
            }

            Debug.Log($"<color=#00FFAA><b>[PixelArtGenerator]</b></color> Başarıyla {createdCount} adet küp oluşturuldu! ({cols}x{rows} ızgara)");
        }

        private bool m_GenerateRetryScheduled;

        /// <summary>
        /// Canvas düzeni hazır olmadığı için başarısız olan üretimi bir sonraki karede tekrar dener.
        /// Aynı anda tek bir tekrar planlanır.
        /// </summary>
        private void ScheduleGenerateRetry()
        {
            if (m_GenerateRetryScheduled) return;

            m_GenerateRetryScheduled = true;
            StartCoroutine(GenerateAfterCanvasReady());
        }

        private System.Collections.IEnumerator GenerateAfterCanvasReady()
        {
            // CanvasScaler ölçeği Update sırasında belirlediği için bir kare beklemek gerekir
            yield return null;
            Canvas.ForceUpdateCanvases();

            m_GenerateRetryScheduled = false;
            GeneratePixelArt();
        }

        public Material GetOrCreateShadowMaterial()
        {
            if (m_CubeShadowMaterial != null) return m_CubeShadowMaterial;

            #if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("SoftVoxelShadow_Mat t:Material");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                m_CubeShadowMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m_CubeShadowMaterial != null) return m_CubeShadowMaterial;
            }
            #endif

            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            m_CubeShadowMaterial = new Material(s);
            m_CubeShadowMaterial.name = "Runtime_CubeShadow_Mat";
            return m_CubeShadowMaterial;
        }

        /// <summary>
        /// Sahnedeki mevcut tüm küplere yeniden oluşturmaya gerek kalmadan sahte gölge ekler / günceller.
        /// </summary>
        [ContextMenu("🌑 Tüm Küplere Fake Shadow Uygula / Güncelle")]
        public void ApplyShadowsToAllExistingCubes()
        {
            if (m_CubesContainer == null) return;

            bool useFakeShadows = EvaluateShadowMode();
            Material shadowMat = useFakeShadows ? GetOrCreateShadowMaterial() : null;
            PixelCube[] cubes = m_CubesContainer.GetComponentsInChildren<PixelCube>(true);

            foreach (var cube in cubes)
            {
                if (cube == null) continue;

                if (useFakeShadows && shadowMat != null)
                {
                    cube.EnsureShadow(shadowMat, m_ShadowOffset, m_ShadowScale, m_ShadowColor);
                }
                else
                {
                    if (cube.ShadowObject != null)
                    {
                        #if UNITY_EDITOR
                        if (!Application.isPlaying)
                            DestroyImmediate(cube.ShadowObject);
                        else
                        #endif
                            Destroy(cube.ShadowObject);
                    }
                    Transform childShadow = cube.transform.Find("CubeShadow");
                    if (childShadow != null)
                    {
                        #if UNITY_EDITOR
                        if (!Application.isPlaying)
                            DestroyImmediate(childShadow.gameObject);
                        else
                        #endif
                            Destroy(childShadow.gameObject);
                    }
                }
            }

            if (m_EnableFigureContourShadow)
            {
                Camera cam = GetActiveCamera();
                if (cam != null && CalculateTargetWorldBounds(cam, out Vector3 worldCenter, out float worldWidth, out float worldHeight))
                {
                    GetEffectiveGridSize(GetActiveTexture(), out int cols, out int rows);
                    float cellSize = Mathf.Min(worldWidth / Mathf.Max(1, cols), worldHeight / Mathf.Max(1, rows));
                    EnsureFigureContourShadow(worldCenter, cols * cellSize, rows * cellSize);
                }
            }
            else
            {
                EnsureFigureContourShadowDisabled();
            }

            if (m_EnableBoardShadow)
            {
                Camera cam = GetActiveCamera();
                if (cam != null && CalculateTargetWorldBounds(cam, out Vector3 worldCenter, out float worldWidth, out float worldHeight))
                {
                    GetEffectiveGridSize(GetActiveTexture(), out int cols, out int rows);
                    float cellSize = Mathf.Min(worldWidth / Mathf.Max(1, cols), worldHeight / Mathf.Max(1, rows));
                    EnsureBoardShadow(worldCenter, cols * cellSize, rows * cellSize);
                }
            }
            else
            {
                EnsureBoardShadowDisabled();
            }

            Debug.Log($"<color=#FFAA00>[PixelGame]</color> {cubes.Length} adet küpün sahte gölgesi (Fake Shadow) güncellendi!");
        }

        #region 🌑 Şekil Kontur Gölgesi (Figure Contour Shadow)

        private static Mesh s_SharedQuadMesh;
        private static Mesh GetOrCreateQuadMesh()
        {
            if (s_SharedQuadMesh != null) return s_SharedQuadMesh;

            Mesh mesh = new Mesh();
            mesh.name = "FigureShadowQuadMesh";
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            s_SharedQuadMesh = mesh;
            return s_SharedQuadMesh;
        }

        private Material m_FigureShadowMaterial;
        private Material GetOrCreateFigureShadowMaterial(Texture2D shadowTex)
        {
            if (m_FigureShadowMaterial == null)
            {
                #if UNITY_EDITOR
                string[] guids = AssetDatabase.FindAssets("FigureContourShadow_Mat t:Material");
                if (guids.Length > 0)
                {
                    string p = AssetDatabase.GUIDToAssetPath(guids[0]);
                    Material matAsset = AssetDatabase.LoadAssetAtPath<Material>(p);
                    if (matAsset != null)
                    {
                        m_FigureShadowMaterial = new Material(matAsset);
                    }
                }
                #endif

                if (m_FigureShadowMaterial == null)
                {
                    Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                    m_FigureShadowMaterial = new Material(shader);
                    m_FigureShadowMaterial.name = "Runtime_FigureContourShadow_Mat";
                    m_FigureShadowMaterial.renderQueue = 2990;
                }
            }

            if (shadowTex != null)
            {
                if (m_FigureShadowMaterial.HasProperty("_MainTex"))
                    m_FigureShadowMaterial.SetTexture("_MainTex", shadowTex);
                if (m_FigureShadowMaterial.HasProperty("_BaseMap"))
                    m_FigureShadowMaterial.SetTexture("_BaseMap", shadowTex);
                m_FigureShadowMaterial.mainTexture = shadowTex;
            }

            Color c = new Color(1f, 1f, 1f, m_FigureShadowOpacity);
            if (m_FigureShadowMaterial.HasProperty("_Color"))
                m_FigureShadowMaterial.SetColor("_Color", c);
            if (m_FigureShadowMaterial.HasProperty("_BaseColor"))
                m_FigureShadowMaterial.SetColor("_BaseColor", c);
            m_FigureShadowMaterial.color = c;

            return m_FigureShadowMaterial;
        }

        private static readonly System.Collections.Generic.Dictionary<string, Texture2D> s_DynamicShadowCache = new System.Collections.Generic.Dictionary<string, Texture2D>();

        private Texture2D GetAppropriateFigureShadowTexture()
        {
            if (m_CustomFigureShadowTexture != null) return m_CustomFigureShadowTexture;
            if (m_ActiveLevelData != null && m_ActiveLevelData.FigureShadowTexture != null) return m_ActiveLevelData.FigureShadowTexture;

            Texture2D activeTex = GetActiveTexture();
            if (activeTex == null) return null;

            string texName = activeTex.name;

            #if UNITY_EDITOR
            // 1. İsim eşleşmeli hazır gölge dokusu ara (örn: FigureShadow_Star, FigureShadow_Raccoon, vb.)
            string cleanName = texName.Replace("PixelArt_", "").Replace("Level_", "").Replace("Texture_", "");
            string baseName = cleanName.Contains("_") ? cleanName.Split('_')[0] : cleanName;

            string[] guids = AssetDatabase.FindAssets($"FigureShadow_{cleanName} t:Texture2D");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                Texture2D found = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (found != null) return found;
            }

            if (baseName != cleanName)
            {
                string[] baseGuids = AssetDatabase.FindAssets($"FigureShadow_{baseName} t:Texture2D");
                if (baseGuids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(baseGuids[0]);
                    Texture2D found = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (found != null) return found;
                }
            }

            // Genel ad araması
            string[] anyGuids = AssetDatabase.FindAssets("FigureShadow t:Texture2D");
            foreach (var g in anyGuids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (p.IndexOf(cleanName, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    p.IndexOf(baseName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Texture2D found = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                    if (found != null) return found;
                }
            }
            #endif

            // 2. Eğer hazır doku bulunamadıysa: HERHANGİ BİR YENİ LEVEL İÇİN DİNAMİK OLARAK OTOMATİK OLUŞTUR!
            return GenerateDynamicContourShadowTexture(activeTex);
        }

        /// <summary>
        /// Herhangi bir kaynak piksel dokusu için otomatik olarak yumuşak silüet kontur gölgesi üretir.
        /// Bu sayede oyuna eklenecek TÜM yeni leveller otomatik olarak gölgeli hale gelir!
        /// </summary>
        public Texture2D GenerateDynamicContourShadowTexture(Texture2D srcTex, int targetSize = 256)
        {
            if (srcTex == null) return null;

            string cacheKey = $"{srcTex.name}_{srcTex.width}_{srcTex.height}_{targetSize}";
            if (s_DynamicShadowCache.TryGetValue(cacheKey, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            #if UNITY_EDITOR
            if (!srcTex.isReadable)
            {
                EnsureTextureReadableEditor(srcTex);
            }
            #endif

            int srcW = srcTex.width;
            int srcH = srcTex.height;

            // 1. Kaynak görselin şeffaflık maskesini oku ve şeffaflık kontrolü yap
            Color[] srcPixels = srcTex.GetPixels();
            bool hasTransparency = false;
            for (int i = 0; i < srcPixels.Length; i++)
            {
                if (srcPixels[i].a < 0.9f)
                {
                    hasTransparency = true;
                    break;
                }
            }

            // Eğer görselin şeffaf arka planı yoksa (düz renk arka planlı, örn: pembe fonlu Rakun):
            // 4 köşenin rengini kontrol et; tek renk arka plandan figür silüetini çıkar
            Color cornerColor = Color.white;
            bool isSolidBackground = false;
            if (!hasTransparency && srcW > 1 && srcH > 1)
            {
                Color cTL = srcPixels[(srcH - 1) * srcW];
                Color cTR = srcPixels[(srcH - 1) * srcW + (srcW - 1)];
                Color cBL = srcPixels[0];
                Color cBR = srcPixels[srcW - 1];

                float diff1 = Mathf.Abs(cTL.r - cTR.r) + Mathf.Abs(cTL.g - cTR.g) + Mathf.Abs(cTL.b - cTR.b);
                float diff2 = Mathf.Abs(cTL.r - cBL.r) + Mathf.Abs(cTL.g - cBL.g) + Mathf.Abs(cTL.b - cBL.b);
                float diff3 = Mathf.Abs(cTL.r - cBR.r) + Mathf.Abs(cTL.g - cBR.g) + Mathf.Abs(cTL.b - cBR.b);

                if (diff1 < 0.15f && diff2 < 0.15f && diff3 < 0.15f)
                {
                    cornerColor = (cTL + cTR + cBL + cBR) * 0.25f;
                    isSolidBackground = true;
                }
            }

            if (!hasTransparency && !isSolidBackground)
            {
                return null;
            }

            float[] mask = new float[targetSize * targetSize];

            for (int y = 0; y < targetSize; y++)
            {
                int srcY = Mathf.Clamp(Mathf.FloorToInt(((float)y / targetSize) * srcH), 0, srcH - 1);
                for (int x = 0; x < targetSize; x++)
                {
                    int srcX = Mathf.Clamp(Mathf.FloorToInt(((float)x / targetSize) * srcW), 0, srcW - 1);
                    Color pixel = srcPixels[srcY * srcW + srcX];
                    if (hasTransparency)
                    {
                        mask[y * targetSize + x] = pixel.a > 0.1f ? 1f : 0f;
                    }
                    else
                    {
                        float colorDiff = Mathf.Abs(pixel.r - cornerColor.r) + Mathf.Abs(pixel.g - cornerColor.g) + Mathf.Abs(pixel.b - cornerColor.b);
                        mask[y * targetSize + x] = colorDiff > 0.08f ? 1f : 0f;
                    }
                }
            }

            // 2. Ayrılabilir (Separable) hızlı bulanıklaştırma fonksiyonu
            float[] Blur(float[] input, int r)
            {
                float[] hBlur = new float[targetSize * targetSize];
                for (int y = 0; y < targetSize; y++)
                {
                    int rowOffset = y * targetSize;
                    float sum = 0f;
                    int count = 0;
                    for (int k = -r; k <= r; k++)
                    {
                        int cx = Mathf.Clamp(k, 0, targetSize - 1);
                        sum += input[rowOffset + cx];
                        count++;
                    }
                    hBlur[rowOffset] = sum / count;

                    for (int x = 1; x < targetSize; x++)
                    {
                        int removeIdx = Mathf.Clamp(x - r - 1, 0, targetSize - 1);
                        int addIdx = Mathf.Clamp(x + r, 0, targetSize - 1);
                        sum += input[rowOffset + addIdx] - input[rowOffset + removeIdx];
                        hBlur[rowOffset + x] = Mathf.Max(0f, sum / count);
                    }
                }

                float[] vBlur = new float[targetSize * targetSize];
                for (int x = 0; x < targetSize; x++)
                {
                    float sum = 0f;
                    int count = 0;
                    for (int k = -r; k <= r; k++)
                    {
                        int cy = Mathf.Clamp(k, 0, targetSize - 1);
                        sum += hBlur[cy * targetSize + x];
                        count++;
                    }
                    vBlur[x] = sum / count;

                    for (int y = 1; y < targetSize; y++)
                    {
                        int removeIdx = Mathf.Clamp(y - r - 1, 0, targetSize - 1);
                        int addIdx = Mathf.Clamp(y + r, 0, targetSize - 1);
                        sum += hBlur[addIdx * targetSize + x] - hBlur[removeIdx * targetSize + x];
                        vBlur[y * targetSize + x] = Mathf.Max(0f, sum / count);
                    }
                }
                return vBlur;
            }

            // 3. Ambient (her yöne eşit taşan) + Drop (hafif aşağı düşen) gölge katmanı
            float[] ambient = Blur(mask, 8);
            ambient = Blur(ambient, 8);

            int dropOffset = 6;
            float[] dropInput = new float[targetSize * targetSize];
            for (int y = dropOffset; y < targetSize; y++)
            {
                System.Array.Copy(mask, y * targetSize, dropInput, (y - dropOffset) * targetSize, targetSize);
            }
            float[] drop = Blur(dropInput, 12);
            drop = Blur(drop, 12);

            // 4. Sonuç dokusunu oluştur (İç alan şeffaf kalır, arkada siyah leke oluşmaz!)
            Texture2D shadowTex = new Texture2D(targetSize, targetSize, TextureFormat.RGBA32, false);
            shadowTex.name = $"GeneratedShadow_{srcTex.name}";
            shadowTex.filterMode = FilterMode.Bilinear;
            shadowTex.wrapMode = TextureWrapMode.Clamp;

            Color[] finalPixels = new Color[targetSize * targetSize];
            Color shadowColor = new Color(15f / 255f, 22f / 255f, 42f / 255f, 1f);

            for (int i = 0; i < targetSize * targetSize; i++)
            {
                float a = Mathf.Clamp01(ambient[i] * 1.25f + drop[i] * 0.75f);
                // Küplerin iç kısmını çıkararak boşluk bırak (hollow):
                float outerOnly = Mathf.Clamp01(a - mask[i] * 0.95f);
                float curvedAlpha = Mathf.Pow(outerOnly, 0.9f);
                finalPixels[i] = new Color(shadowColor.r, shadowColor.g, shadowColor.b, curvedAlpha);
            }

            shadowTex.SetPixels(finalPixels);
            shadowTex.Apply();

            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                try
                {
                    string cleanName = srcTex.name.Replace("PixelArt_", "").Replace("Level_", "");
                    string savePath = $"Assets/Textures/FigureShadow_{cleanName}.png";
                    if (!System.IO.File.Exists(savePath))
                    {
                        byte[] pngBytes = shadowTex.EncodeToPNG();
                        System.IO.File.WriteAllBytes(savePath, pngBytes);
                        AssetDatabase.ImportAsset(savePath, ImportAssetOptions.ForceUpdate);

                        TextureImporter importer = AssetImporter.GetAtPath(savePath) as TextureImporter;
                        if (importer != null)
                        {
                            importer.alphaIsTransparency = true;
                            importer.wrapMode = TextureWrapMode.Clamp;
                            importer.SaveAndReimport();
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[PixelArtGenerator] Gölge kaydedilirken uyarı: {ex.Message}");
                }
            }
            #endif

            s_DynamicShadowCache[cacheKey] = shadowTex;
            return shadowTex;
        }

        public void EnsureFigureContourShadowDisabled()
        {
            if (m_CubesContainer != null)
            {
                Transform shadowTrans = m_CubesContainer.Find("FigureContourShadow");
                if (shadowTrans != null)
                {
                    #if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(shadowTrans.gameObject);
                    else
                    #endif
                        Destroy(shadowTrans.gameObject);
                }
            }

            // Sahne genelinde yetim kalmış FigureContourShadow nesnelerini de temizle
            GameObject[] allObjs = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var go in allObjs)
            {
                if (go != null && (go.name == "FigureContourShadow" || go.name.StartsWith("FigureContourShadow")))
                {
                    #if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(go);
                    else
                    #endif
                        Destroy(go);
                }
            }
        }

        public void UpdateContourShadowLive()
        {
            if (m_CubesContainer == null) return;

            if (!m_EnableFigureContourShadow)
            {
                EnsureFigureContourShadowDisabled();
                return;
            }

            Transform shadowTrans = m_CubesContainer.Find("FigureContourShadow");
            if (shadowTrans != null)
            {
                shadowTrans.gameObject.SetActive(true);
                MeshRenderer mr = shadowTrans.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null)
                {
                    Color col = new Color(1f, 1f, 1f, m_FigureShadowOpacity);
                    if (mr.sharedMaterial.HasProperty("_Color"))
                        mr.sharedMaterial.SetColor("_Color", col);
                    if (mr.sharedMaterial.HasProperty("_BaseColor"))
                        mr.sharedMaterial.SetColor("_BaseColor", col);
                    mr.sharedMaterial.color = col;
                }

                Camera cam = GetActiveCamera();
                if (cam != null && CalculateTargetWorldBounds(cam, out Vector3 worldCenter, out float worldWidth, out float worldHeight))
                {
                    GetEffectiveGridSize(GetActiveTexture(), out int cols, out int rows);
                    float cellSize = Mathf.Min(worldWidth / Mathf.Max(1, cols), worldHeight / Mathf.Max(1, rows));
                    float totalWidth = cols * cellSize;
                    float totalHeight = rows * cellSize;

                    shadowTrans.position = new Vector3(
                        worldCenter.x + m_FigureShadowOffset.x,
                        worldCenter.y + m_FigureShadowOffset.y,
                        m_TargetZ + 0.06f
                    );
                    shadowTrans.localScale = new Vector3(
                        totalWidth * m_FigureShadowScale,
                        totalHeight * m_FigureShadowScale,
                        1f
                    );
                }
            }
            else if (m_EnableFigureContourShadow)
            {
                Camera cam = GetActiveCamera();
                if (cam != null && CalculateTargetWorldBounds(cam, out Vector3 worldCenter, out float worldWidth, out float worldHeight))
                {
                    GetEffectiveGridSize(GetActiveTexture(), out int cols, out int rows);
                    float cellSize = Mathf.Min(worldWidth / Mathf.Max(1, cols), worldHeight / Mathf.Max(1, rows));
                    EnsureFigureContourShadow(worldCenter, cols * cellSize, rows * cellSize);
                }
            }
        }

        public void EnsureFigureContourShadow(Vector3 worldCenter, float totalWidth, float totalHeight)
        {
            if (m_CubesContainer == null) return;

            if (!m_EnableFigureContourShadow)
            {
                EnsureFigureContourShadowDisabled();
                return;
            }

            Transform shadowTrans = m_CubesContainer.Find("FigureContourShadow");
            GameObject shadowObj;

            if (shadowTrans == null)
            {
                shadowObj = new GameObject("FigureContourShadow");
                #if UNITY_EDITOR
                Undo.RegisterCreatedObjectUndo(shadowObj, "Create Figure Contour Shadow");
                #endif
                shadowObj.transform.SetParent(m_CubesContainer, false);
                shadowObj.transform.SetAsFirstSibling();

                MeshFilter mf = shadowObj.AddComponent<MeshFilter>();
                #if UNITY_EDITOR
                Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                mf.sharedMesh = quad != null ? quad : GetOrCreateQuadMesh();
                #else
                mf.sharedMesh = GetOrCreateQuadMesh();
                #endif

                shadowObj.AddComponent<MeshRenderer>();
            }
            else
            {
                shadowObj = shadowTrans.gameObject;
                shadowObj.transform.SetAsFirstSibling();
            }

            Texture2D shadowTex = GetAppropriateFigureShadowTexture();
            MeshRenderer mr = shadowObj.GetComponent<MeshRenderer>();

            if (shadowTex != null)
            {
                Material mat = GetOrCreateFigureShadowMaterial(shadowTex);
                if (mr != null)
                {
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                }
                shadowObj.SetActive(true);
            }
            else if (mr != null && mr.sharedMaterial != null && mr.sharedMaterial.mainTexture != null)
            {
                // Sahnede önceden ayarlanmış materyal ve doku var, olduğu gibi koru!
                shadowObj.SetActive(true);
            }
            else
            {
                shadowObj.SetActive(false);
                return;
            }

            // Küplerin hemen arkasında (z = m_TargetZ + 0.06f)
            shadowObj.transform.position = new Vector3(
                worldCenter.x + m_FigureShadowOffset.x,
                worldCenter.y + m_FigureShadowOffset.y,
                m_TargetZ + 0.06f
            );
            shadowObj.transform.rotation = Quaternion.identity;
            shadowObj.transform.localScale = new Vector3(
                totalWidth * m_FigureShadowScale,
                totalHeight * m_FigureShadowScale,
                1f
            );
        }

        #endregion

        #region 🖼️ Pano Çevresi Sahte Gölge (Board Frame Shadow - 4 Kenarı Saran Sahte Gölge)

        public Material GetOrCreateBoardShadowMaterial()
        {
            if (m_BoardShadowMaterial != null) return m_BoardShadowMaterial;

            #if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("BoardFrameShadow_Mat t:Material");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                m_BoardShadowMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m_BoardShadowMaterial != null) return m_BoardShadowMaterial;
            }
            #endif

            Shader s = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            m_BoardShadowMaterial = new Material(s);
            m_BoardShadowMaterial.name = "Runtime_BoardFrameShadow_Mat";
            m_BoardShadowMaterial.renderQueue = 2990;
            return m_BoardShadowMaterial;
        }

        public void EnsureBoardShadow(Vector3 worldCenter, float totalWidth, float totalHeight)
        {
            if (m_CubesContainer == null) return;

            if (!m_EnableBoardShadow)
            {
                EnsureBoardShadowDisabled();
                return;
            }

            Transform shadowTrans = m_CubesContainer.Find("BoardGridShadow");
            GameObject shadowObj;

            if (shadowTrans == null)
            {
                shadowObj = new GameObject("BoardGridShadow");
                #if UNITY_EDITOR
                Undo.RegisterCreatedObjectUndo(shadowObj, "Create Board Grid Shadow");
                #endif
                shadowObj.transform.SetParent(m_CubesContainer, false);
                shadowObj.transform.SetAsFirstSibling();

                MeshFilter mf = shadowObj.AddComponent<MeshFilter>();
                #if UNITY_EDITOR
                Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                mf.sharedMesh = quad != null ? quad : GetOrCreateQuadMesh();
                #else
                mf.sharedMesh = GetOrCreateQuadMesh();
                #endif

                shadowObj.AddComponent<MeshRenderer>();
            }
            else
            {
                shadowObj = shadowTrans.gameObject;
                shadowObj.transform.SetAsFirstSibling();
            }

            shadowObj.SetActive(true);

            MeshRenderer mr = shadowObj.GetComponent<MeshRenderer>();
            Material mat = GetOrCreateBoardShadowMaterial();
            if (mr != null && mat != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                MaterialPropertyBlock block = new MaterialPropertyBlock();
                mr.GetPropertyBlock(block);
                Color col = m_BoardShadowColor;
                col.a = m_BoardShadowOpacity;
                block.SetColor("_Color", col);
                block.SetColor("_BaseColor", col);
                mr.SetPropertyBlock(block);
            }

            // Pozisyon: Küplerin hemen arkasında (z = m_TargetZ + 0.08f), pano merkezinde
            shadowObj.transform.position = new Vector3(
                worldCenter.x + m_BoardShadowOffset.x,
                worldCenter.y + m_BoardShadowOffset.y,
                m_TargetZ + 0.08f
            );
            shadowObj.transform.rotation = Quaternion.identity;
            shadowObj.transform.localScale = new Vector3(
                totalWidth * m_BoardShadowScale,
                totalHeight * m_BoardShadowScale,
                1f
            );
        }

        public void EnsureBoardShadowDisabled()
        {
            if (m_CubesContainer != null)
            {
                Transform shadowTrans = m_CubesContainer.Find("BoardGridShadow");
                if (shadowTrans != null)
                {
                    #if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(shadowTrans.gameObject);
                    else
                    #endif
                        Destroy(shadowTrans.gameObject);
                }
            }

            // Sahne genelinde yetim kalmış BoardGridShadow nesnelerini de temizle
            GameObject[] allObjs = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var go in allObjs)
            {
                if (go != null && (go.name == "BoardGridShadow" || go.name.StartsWith("BoardGridShadow")))
                {
                    #if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(go);
                    else
                    #endif
                        Destroy(go);
                }
            }
        }

        public void UpdateBoardShadowLive()
        {
            if (m_CubesContainer == null) return;

            if (!m_EnableBoardShadow)
            {
                EnsureBoardShadowDisabled();
                return;
            }

            Transform shadowTrans = m_CubesContainer.Find("BoardGridShadow");
            if (shadowTrans != null)
            {
                MeshRenderer mr = shadowTrans.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    MaterialPropertyBlock block = new MaterialPropertyBlock();
                    mr.GetPropertyBlock(block);
                    Color col = m_BoardShadowColor;
                    col.a = m_BoardShadowOpacity;
                    block.SetColor("_Color", col);
                    block.SetColor("_BaseColor", col);
                    mr.SetPropertyBlock(block);
                }
            }

            Camera cam = GetActiveCamera();
            if (cam != null && CalculateTargetWorldBounds(cam, out Vector3 worldCenter, out float worldWidth, out float worldHeight))
            {
                GetEffectiveGridSize(GetActiveTexture(), out int cols, out int rows);
                float cellSize = Mathf.Min(worldWidth / Mathf.Max(1, cols), worldHeight / Mathf.Max(1, rows));
                EnsureBoardShadow(worldCenter, cols * cellSize, rows * cellSize);
            }
        }

        #endregion

        public void GetEffectiveGridSize(Texture2D tex, out int cols, out int rows)
        {
            if (m_UseNativeResolution && tex != null)
            {
                cols = tex.width;
                rows = tex.height;
                return;
            }

            cols = m_GridResolution.x > 0 ? m_GridResolution.x : (tex != null ? tex.width : 24);
            rows = m_GridResolution.y > 0 ? m_GridResolution.y : (tex != null ? tex.height : 24);
        }

        /// <summary>
        /// Mevcut tüm küpleri temizler.
        /// </summary>
        [ContextMenu("Küpleri Temizle")]
        public void ClearCubes()
        {
            Miner.InvalidateLayoutCache();

            if (m_CubesContainer == null) return;

            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                while (m_CubesContainer.childCount > 0)
                {
                    Transform child = m_CubesContainer.GetChild(0);
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
                return;
            }
            #endif

            for (int i = m_CubesContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(m_CubesContainer.GetChild(i).gameObject);
            }
        }

        public bool CalculateTargetWorldBounds(Camera cam, out Vector3 worldCenter, out float worldWidth, out float worldHeight)
        {
            worldCenter = Vector3.zero;
            worldWidth = 0f;
            worldHeight = 0f;

            // 1. Kullanıcının yerleştirdiği OtCerceve veya BoardFrame varsa doğrudan onun dünya sınırlarına tam oturt
            EnsureTargetFrameRect();
            if (cam == null) cam = GetActiveCamera();

            if (m_TargetFrameRect != null && cam != null)
            {
                Canvas.ForceUpdateCanvases();

                Rect frame = m_TargetFrameRect.rect;
                Vector3 frameScale = m_TargetFrameRect.lossyScale;

                if (frame.width * Mathf.Abs(frameScale.x) >= 1f &&
                    frame.height * Mathf.Abs(frameScale.y) >= 1f)
                {
                    Canvas canvas = m_TargetFrameRect.GetComponentInParent<Canvas>();
                    Vector3[] corners = new Vector3[4];
                    m_TargetFrameRect.GetWorldCorners(corners);

                    float camDist = Mathf.Abs(cam.transform.position.z - m_TargetZ);
                    if (camDist < 0.1f) camDist = 10f;

                    if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        float frameScreenWidth = Mathf.Abs(corners[2].x - corners[0].x);
                        float frameScreenHeight = Mathf.Abs(corners[2].y - corners[0].y);
                        float frameScreenCenterY = (corners[0].y + corners[2].y) * 0.5f;

                        if (frameScreenWidth >= cam.pixelWidth * 0.15f &&
                            frameScreenHeight >= cam.pixelHeight * 0.1f)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                corners[i] = cam.ScreenToWorldPoint(new Vector3(corners[i].x, corners[i].y, camDist));
                            }

                            Vector3 bottomLeft = corners[0];
                            Vector3 topRight = corners[2];

                            float rawW = Mathf.Abs(topRight.x - bottomLeft.x);
                            float rawH = Mathf.Abs(topRight.y - bottomLeft.y);

                            if (rawW >= 0.5f && rawH >= 0.5f)
                            {
                                worldCenter = (bottomLeft + topRight) * 0.5f;
                                worldCenter.z = m_TargetZ;
                                float padX = rawW * m_InnerPadding;
                                float padY = rawH * m_InnerPadding;
                                worldWidth = Mathf.Max(0.1f, rawW - padX * 2f);
                                worldHeight = Mathf.Max(0.1f, rawH - padY * 2f);
                                return true;
                            }
                        }
                    }
                    else
                    {
                        Vector3 bottomLeft = corners[0];
                        Vector3 topRight = corners[2];

                        float rawW = Mathf.Abs(topRight.x - bottomLeft.x);
                        float rawH = Mathf.Abs(topRight.y - bottomLeft.y);

                        if (rawW >= 0.5f && rawH >= 0.5f)
                        {
                            worldCenter = new Vector3((bottomLeft.x + topRight.x) * 0.5f, (bottomLeft.y + topRight.y) * 0.5f, m_TargetZ);
                            float padX = rawW * m_InnerPadding;
                            float padY = rawH * m_InnerPadding;
                            worldWidth = Mathf.Max(0.1f, rawW - padX * 2f);
                            worldHeight = Mathf.Max(0.1f, rawH - padY * 2f);
                            return true;
                        }
                    }
                }
            }

            // 2. Mavi ray çerçevesi varsa onun tam ortasına ve içine hizala
            TruckDispatcher dispatcher = Object.FindFirstObjectByType<TruckDispatcher>();
            if (dispatcher != null && dispatcher.TryGetExistingRailBounds(out Vector3 railCenter, out float railW, out float railH, out _, out _))
            {
                worldCenter = railCenter;
                worldCenter.z = m_TargetZ;
                float trackPadding = dispatcher.ModularTrackScale * 0.90f;
                float innerW = Mathf.Max(0.5f, railW - trackPadding * 2f);
                float innerH = Mathf.Max(0.5f, railH - trackPadding * 2f);
                float padX = innerW * m_InnerPadding;
                float padY = innerH * m_InnerPadding;
                worldWidth = Mathf.Max(0.1f, innerW - padX * 2f);
                worldHeight = Mathf.Max(0.1f, innerH - padY * 2f);
                return true;
            }

            // 3. Sahnede mevcut küpler varsa doğrudan 3D dünya sınırlarını hesapla
            // (TruckDispatcher, Miner ve Scene Frame ile %100 senkron ve hatasız yöntem)
            if (m_CubesContainer == null) EnsureContainer();
            if (m_CubesContainer != null && m_CubesContainer.childCount > 0)
            {
                Bounds bounds = default;
                bool hasCube = false;
                for (int i = 0; i < m_CubesContainer.childCount; i++)
                {
                    Transform child = m_CubesContainer.GetChild(i);
                    if (child == null || !child.gameObject.activeSelf) continue;
                    string cName = child.name;
                    if (cName.StartsWith("BoardGrid") || cName.StartsWith("FigureContour") || cName.Contains("Shadow") || cName.Contains("[SceneFrame")) continue;

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

                if (hasCube && bounds.size.x > 0.3f && bounds.size.y > 0.3f)
                {
                    worldCenter = bounds.center;
                    worldCenter.z = m_TargetZ;
                    worldWidth = bounds.size.x;
                    worldHeight = bounds.size.y;
                    return true;
                }
            }

            // 3. Nihai güvenli fallback: Jeneratörün kendi transform pozisyonu (Sahne Kontrolü)
            worldCenter = transform.position;
            worldCenter.z = m_TargetZ;
            worldWidth = 3.8f;
            worldHeight = 3.8f;
            return true;
        }

        private Color SampleRawColor(Texture2D tex, int gridX, int gridY, int totalCols, int totalRows)
        {
            #if UNITY_EDITOR
            if (!tex.isReadable)
            {
                EnsureTextureReadableEditor(tex);
            }
            #endif

            Color basePixel;

            // 1:1 Doğal Piksel Çözünürlüğü: Doğrudan piksel koordinatını oku (Sıfır bozulma!)
            if (totalCols == tex.width && totalRows == tex.height)
            {
                basePixel = tex.GetPixel(gridX, gridY);
            }
            else
            {
                float u = (gridX + 0.5f) / totalCols;
                float v = (gridY + 0.5f) / totalRows;

                if (m_SamplingMode == SamplingMode.Point)
                {
                    int px = Mathf.Clamp(Mathf.FloorToInt(u * tex.width), 0, tex.width - 1);
                    int py = Mathf.Clamp(Mathf.FloorToInt(v * tex.height), 0, tex.height - 1);
                    basePixel = tex.GetPixel(px, py);
                }
                else
                {
                    basePixel = tex.GetPixelBilinear(u, v);
                }
            }

            // Aktif level verisi varsa palet değişikliğini (recolor), ton kaydırmayı ve tint filtresini uygula
            if (m_ActiveLevelData != null)
            {
                basePixel = m_ActiveLevelData.ApplyColorPipeline(basePixel);
            }

            return basePixel;
        }

        #if UNITY_EDITOR
        private void EnsureTextureReadableEditor(Texture2D tex)
        {
            try
            {
                string path = AssetDatabase.GetAssetPath(tex);
                if (!string.IsNullOrEmpty(path))
                {
                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null && !importer.isReadable)
                    {
                        importer.isReadable = true;
                        importer.SaveAndReimport();
                    }
                }
            }
            catch
            {
                // sessizce devam et
            }
        }
        #endif

        public Texture2D GetActiveTexture()
        {
            if (m_SourceTexture != null)
                return m_SourceTexture;

            if (m_SourceSprite != null && m_SourceSprite.texture != null)
                return m_SourceSprite.texture;

            return null;
        }

        public Camera GetActiveCamera()
        {
            if (m_WorldCamera != null)
                return m_WorldCamera;

            return Camera.main;
        }

        private void EnsureTargetFrameRect()
        {
            // 1. Kullanıcının yerleştirdiği OtCerceve çerçevesini doğrudan ve en yüksek öncelikle ara
            GameObject otGo = GameObject.Find("OtCerceve");
            if (otGo != null)
            {
                RectTransform rt = otGo.GetComponent<RectTransform>();
                if (rt != null)
                {
                    m_TargetFrameRect = rt;
                    EnsureOtCerceveShadow(otGo);
                    return;
                }
            }

            if (m_TargetFrameRect != null && m_TargetFrameRect.gameObject != null)
            {
                if (m_TargetFrameRect.name == "OtCerceve")
                {
                    EnsureOtCerceveShadow(m_TargetFrameRect.gameObject);
                }
                return;
            }

            // 2. Diğer alternatif çerçeve isimlerini dene
            string[] preferredNames = new string[] { "BoardFrame", "MainPlane", "SandFrame", "Frame" };
            foreach (var name in preferredNames)
            {
                GameObject go = GameObject.Find(name);
                if (go != null)
                {
                    RectTransform rt = go.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        m_TargetFrameRect = rt;
                        break;
                    }
                }
            }

            // 2D Canvas'taki kılavuz nesnenin ekranda hayalet beyaz kutu çizmesini engelle
            if (m_TargetFrameRect != null && m_TargetFrameRect.name == "MainPlane")
            {
                UnityEngine.UI.Image img = m_TargetFrameRect.GetComponent<UnityEngine.UI.Image>();
                if (img != null && img.enabled)
                {
                    img.enabled = false;
                }
            }
        }

        /// <summary>
        /// OtCerceve çerçevesinin arkasındaki gölgeyi (OtCerceve_Shadow) kontrol eder.
        /// Nesne sahnede zaten varsa kullanıcının Scene View veya Inspector'da yaptığı TÜM ayarlamaları (konum, boyut, renk, açı) %100 korur.
        /// </summary>
        public static void EnsureOtCerceveShadow(GameObject otGo)
        {
            if (otGo == null) return;

            bool isNewlyCreated = false;
            Transform shadowTr = otGo.transform.Find("OtCerceve_Shadow");
            if (shadowTr == null && otGo.transform.parent != null)
            {
                shadowTr = otGo.transform.parent.Find("OtCerceve_Shadow");
            }

            if (shadowTr == null)
            {
                GameObject shadowGo = new GameObject("OtCerceve_Shadow", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                shadowGo.transform.SetParent(otGo.transform, false);
                shadowGo.transform.SetAsFirstSibling(); // Çerçevenin arkasında kalması için en arkaya al
                shadowTr = shadowGo.transform;
                isNewlyCreated = true;
            }

            // Eğer nesne yeni oluşturulduysa başlangıç değerlerini ata; önceden varsa kullanıcının ayarlarını KESİNLİKLE elleme!
            RectTransform rt = shadowTr.GetComponent<RectTransform>();
            if (rt != null && isNewlyCreated)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(4f, -10f); // Kum üzerinde aşağı ve sağa düşen doğal gölge
                rt.sizeDelta = new Vector2(36f, 36f);       // Çerçevenin etrafına yayılan yumuşak pay
                rt.localScale = Vector3.one;
            }

            UnityEngine.UI.Image img = shadowTr.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                img.raycastTarget = false;
                if (isNewlyCreated)
                {
                    img.color = new Color(0.14f, 0.09f, 0.03f, 0.60f); // Sıcak kehribar / kum gölgesi tonu
                }

#if UNITY_EDITOR
                if (img.sprite == null)
                {
                    Sprite shadowSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/OtCerceve_Shadow.png");
                    if (shadowSprite == null) shadowSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Kenney/OtCerceve_Shadow.png");
                    if (shadowSprite != null) img.sprite = shadowSprite;
                }
#endif
            }
        }

        /// <summary>
        /// Sahnedeki OtCerceve çerçevesini dünya uzayında referans alır ve mevcut tüm küpleri
        /// hiçbirini silmeden / yapılarını bozmadan doğrudan çerçevenin tam ortasına oturtur.
        /// </summary>
        [ContextMenu("🎯 Küpleri OtCerceve Çerçevesine Tam Oturt / Hizala")]
        public void AlignCubesToTargetFrame()
        {
            EnsureTargetFrameRect();
            Camera cam = GetActiveCamera();
            if (cam == null) cam = Camera.main;

            if (m_CubesContainer == null) EnsureContainer();
            if (m_CubesContainer == null || m_CubesContainer.childCount == 0)
            {
                Debug.LogWarning("[PixelArtGenerator] Hizalanacak küp bulunamadı!");
                return;
            }

            if (!CalculateTargetWorldBounds(cam, out Vector3 targetCenter, out float targetW, out float targetH))
            {
                Debug.LogError("[PixelArtGenerator] Hedef çerçevenin dünya sınırları hesaplanamadı!");
                return;
            }

            // Mevcut küplerin geometrik merkezini hesapla
            Bounds bounds = default;
            bool hasCube = false;
            for (int i = 0; i < m_CubesContainer.childCount; i++)
            {
                Transform child = m_CubesContainer.GetChild(i);
                if (child == null || !child.gameObject.activeSelf) continue;
                string cName = child.name;
                if (cName.StartsWith("BoardGrid") || cName.StartsWith("FigureContour") || cName.Contains("Shadow") || cName.Contains("[SceneFrame")) continue;

                Vector3 pos = child.position;
                if (!hasCube) { bounds = new Bounds(pos, Vector3.one * 0.16f); hasCube = true; }
                else bounds.Encapsulate(pos);
            }

            if (!hasCube) return;

            Vector3 delta = targetCenter - bounds.center;
            delta.z = 0f;

            for (int i = 0; i < m_CubesContainer.childCount; i++)
            {
                Transform child = m_CubesContainer.GetChild(i);
                if (child == null) continue;
                child.position += delta;
            }

            EnsureWorldFramePreview();

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(gameObject);
            if (m_CubesContainer != null) UnityEditor.EditorUtility.SetDirty(m_CubesContainer.gameObject);
#endif
            string frameName = m_TargetFrameRect != null ? m_TargetFrameRect.name : "Çerçeve";
            Debug.Log($"<color=#00FFAA><b>[PixelArtGenerator]</b></color> Tüm küpler '{frameName}' çerçevesinin merkezine ({targetCenter}) başarıyla oturtuldu!");
        }

        private void EnsureContainer()
        {
            if (m_CubesContainer != null) return;

            Transform existing = transform.Find("PixelArtContainer");
            if (existing != null)
            {
                m_CubesContainer = existing;
                return;
            }

            GameObject containerObj = new GameObject("PixelArtContainer");
            containerObj.transform.SetParent(transform, false);
            m_CubesContainer = containerObj.transform;

            #if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(containerObj, "Create PixelArtContainer");
            #endif
        }

        /// <summary>
        /// Scene View'da tasarım yaparken çerçevenin ve pano zemininin 3D dünyada
        /// tam olarak küplerin arkasında net olarak görünmesini sağlar.
        /// </summary>
        public void EnsureWorldFramePreview(bool forceCreate = false)
        {
            if (!m_ShowSceneFramePreview)
            {
                if (m_FramePreviewTransform != null) m_FramePreviewTransform.gameObject.SetActive(false);
                Transform found = transform.Find("[SceneFramePreview]");
                if (found != null) found.gameObject.SetActive(false);
                return;
            }

            Camera cam = GetActiveCamera();
            EnsureTargetFrameRect();

            if (!CalculateTargetWorldBounds(cam, out Vector3 worldCenter, out float worldWidth, out float worldHeight))
                return;

            float fullW = worldWidth / Mathf.Max(0.01f, 1f - m_InnerPadding * 2f);
            float fullH = worldHeight / Mathf.Max(0.01f, 1f - m_InnerPadding * 2f);

            if (m_FramePreviewTransform == null)
            {
                Transform existing = transform.Find("[SceneFramePreview]");
                if (existing != null) m_FramePreviewTransform = existing;
            }

            if (m_FramePreviewTransform == null)
            {
                // Sahnede kullanıcı sildiyse ve zorla üretim istenmediyse (örn. Update döngüsü), yeniden üretme!
                if (!forceCreate) return;

                GameObject previewObj = new GameObject("[SceneFramePreview]");
                previewObj.transform.SetParent(transform, false);
                m_FramePreviewTransform = previewObj.transform;
            }

            m_FramePreviewTransform.gameObject.SetActive(true);
            m_FramePreviewTransform.position = new Vector3(worldCenter.x, worldCenter.y, m_TargetZ + 0.08f);
            m_FramePreviewTransform.rotation = Quaternion.identity;

            // 1. Mavi Çerçeve Pervazı (Frame Sprite)
            SpriteRenderer sr = m_FramePreviewTransform.GetComponent<SpriteRenderer>();
            if (sr == null) sr = m_FramePreviewTransform.gameObject.AddComponent<SpriteRenderer>();

            Sprite frameSprite = null;
            #if UNITY_EDITOR
            frameSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Frame.png") 
                       ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Frame.png");
            #endif
            if (frameSprite == null && m_TargetFrameRect != null)
            {
                UnityEngine.UI.Image img = m_TargetFrameRect.GetComponent<UnityEngine.UI.Image>();
                if (img != null && img.sprite != null) frameSprite = img.sprite;
            }

            if (frameSprite != null)
            {
                sr.sprite = frameSprite;
                sr.drawMode = SpriteDrawMode.Simple;
                float ppx = frameSprite.rect.width / frameSprite.pixelsPerUnit;
                float ppy = frameSprite.rect.height / frameSprite.pixelsPerUnit;
                m_FramePreviewTransform.localScale = new Vector3(fullW / Mathf.Max(0.01f, ppx), fullH / Mathf.Max(0.01f, ppy), 1f);
                sr.color = Color.white;
                sr.sortingOrder = -10; // Küplerin arkasında kalır
            }

            // 2. Çerçevenin İçindeki Beyaz Tuval / Pano Zemini (Scene Board Canvas)
            Transform boardChild = m_FramePreviewTransform.Find("[BoardBackground]");
            if (m_ShowSceneBoardBackground)
            {
                if (boardChild == null)
                {
                    if (!forceCreate) return; // Kullanıcı pano arka planını sildiyse hortlatma!

                    GameObject bgObj = new GameObject("[BoardBackground]");
                    bgObj.transform.SetParent(m_FramePreviewTransform, false);
                    boardChild = bgObj.transform;
                }

                boardChild.gameObject.SetActive(true);
                boardChild.localPosition = new Vector3(0f, 0f, 0.02f);
                boardChild.localRotation = Quaternion.identity;

                MeshFilter mf = boardChild.GetComponent<MeshFilter>();
                if (mf == null) mf = boardChild.gameObject.AddComponent<MeshFilter>();
                mf.sharedMesh = GetOrCreateQuadMesh();

                MeshRenderer mr = boardChild.GetComponent<MeshRenderer>();
                if (mr == null) mr = boardChild.gameObject.AddComponent<MeshRenderer>();

                Shader boardShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                if (mr.sharedMaterial == null || mr.sharedMaterial.shader != boardShader)
                {
                    mr.sharedMaterial = new Material(boardShader);
                    mr.sharedMaterial.name = "Scene_BoardBackground_Mat";
                }
                mr.sharedMaterial.color = new Color(0.96f, 0.97f, 0.98f, 0.98f); // Temiz tuval rengi
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                // Parent'ın ölçeğini nötralize ederek iç oynanabilir alana tam oturmasını sağla
                float ppxInv = 1f / Mathf.Max(0.001f, m_FramePreviewTransform.localScale.x);
                float ppyInv = 1f / Mathf.Max(0.001f, m_FramePreviewTransform.localScale.y);
                boardChild.localScale = new Vector3(worldWidth * 1.02f * ppxInv, worldHeight * 1.02f * ppyInv, 1f);
            }
            else
            {
                if (boardChild != null) boardChild.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Sahnede daha önce kazara veya test amaçlı patlatılmış/gizlenmiş tüm küpleri geri görünür yapar.
        /// </summary>
        public void RestoreAllPoppedCubes()
        {
            if (m_CubesContainer == null) EnsureContainer();
            if (m_CubesContainer == null) return;

            PixelCube[] cubes = m_CubesContainer.GetComponentsInChildren<PixelCube>(true);
            for (int i = 0; i < cubes.Length; i++)
            {
                if (cubes[i] != null)
                {
                    cubes[i].gameObject.SetActive(true);
                    cubes[i].SetPoppedVisualState(false);
                }
            }

            PixelCubeInteraction interaction = Object.FindFirstObjectByType<PixelCubeInteraction>();
            if (interaction != null) interaction.ResetAllCubes();

            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(gameObject);
            #endif
            Debug.Log($"<color=cyan>[PixelGame]</color> {cubes.Length} adet küpün tamamı sahnede görünür hale getirildi!");
        }

        /// <summary>
        /// Sahnedeki mevcut tüm küpleri ve çerçeveyi jeneratörün pozisyonuna hizalar.
        /// Tasarım ve sahne kontrolünü kolaylaştırır.
        /// </summary>
        [ContextMenu("🎯 Küpleri Merkeze Hizala (Snap to Center)")]
        public void CenterPixelArtToOrigin()
        {
            if (m_CubesContainer == null) EnsureContainer();
            if (m_CubesContainer == null || m_CubesContainer.childCount == 0) return;

            Bounds bounds = default;
            bool hasCube = false;
            for (int i = 0; i < m_CubesContainer.childCount; i++)
            {
                Transform child = m_CubesContainer.GetChild(i);
                if (child == null || !child.gameObject.activeSelf) continue;
                string cName = child.name;
                if (cName.StartsWith("BoardGrid") || cName.StartsWith("FigureContour") || cName.Contains("Shadow") || cName.Contains("[SceneFrame")) continue;

                Vector3 pos = child.position;
                if (!hasCube) { bounds = new Bounds(pos, Vector3.one * 0.16f); hasCube = true; }
                else bounds.Encapsulate(pos);
            }

            if (!hasCube) return;

            Vector3 delta = transform.position - bounds.center;
            delta.z = 0f;

            for (int i = 0; i < m_CubesContainer.childCount; i++)
            {
                Transform child = m_CubesContainer.GetChild(i);
                if (child == null) continue;
                child.position += delta;
            }

            EnsureWorldFramePreview();
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(gameObject);
            UnityEditor.EditorUtility.SetDirty(m_CubesContainer.gameObject);
            #endif
            Debug.Log($"<color=cyan>[PixelGame]</color> Tüm küpler jeneratör merkezine hizalandı: {transform.position}");
        }

        /// <summary>
        /// Sahnede tüm çizim, çerçeve, ray ve vagon nesnelerini [PixelArtGenerator] altında toplar.
        /// Böylece kullanıcı jeneratörü Scene View'da taşıdığında tüm sahne kusursuz bir şekilde birlikte hareket eder.
        /// </summary>
        [ContextMenu("🔗 Tüm Sahneyi Jeneratör Altında Birleştir")]
        public void OrganizeSceneHierarchy()
        {
            EnsureContainer();
            EnsureWorldFramePreview();

            GameObject wagonsRoot = GameObject.Find("[PerimeterWagonsRoot]");
            if (wagonsRoot != null && wagonsRoot.transform.parent != transform)
            {
                #if UNITY_EDITOR
                UnityEditor.Undo.SetTransformParent(wagonsRoot.transform, transform, "Organize Scene Hierarchy");
                #else
                wagonsRoot.transform.SetParent(transform, true);
                #endif
            }

            TruckDispatcher dispatcher = Object.FindFirstObjectByType<TruckDispatcher>();
            if (dispatcher != null)
            {
                dispatcher.SetupPerimeterLoop();
                Transform wagonsRootTrans = dispatcher.WagonsRoot;
                if (wagonsRootTrans == null)
                {
                    GameObject wr = GameObject.Find("[PerimeterWagonsRoot]");
                    if (wr != null) wagonsRootTrans = wr.transform;
                }
                if (wagonsRootTrans != null && wagonsRootTrans.Find("PerimeterRails") != null)
                {
                    dispatcher.GeneratePerimeterRails();
                }
            }

            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(gameObject);
            #endif
            Debug.Log("<color=cyan>[PixelGame]</color> Tüm sahne [PixelArtGenerator] altında toplandı. Artık jeneratörü taşıdığınızda her şey birlikte hareket eder!");
        }

        /// <summary>
        /// Tüm sahne tasarımını (küpler, çerçeve, raylar) verilen delta miktarı kadar kaydırır.
        /// </summary>
        public void ShiftEntireScene(Vector3 delta)
        {
            OrganizeSceneHierarchy();
            transform.position += delta;
            EnsureWorldFramePreview(false);

            TruckDispatcher dispatcher = Object.FindFirstObjectByType<TruckDispatcher>();
            if (dispatcher != null)
            {
                dispatcher.SetupPerimeterLoop();
                Transform wagonsRootTrans = dispatcher.WagonsRoot;
                if (wagonsRootTrans == null)
                {
                    GameObject wr = GameObject.Find("[PerimeterWagonsRoot]");
                    if (wr != null) wagonsRootTrans = wr.transform;
                }
                if (wagonsRootTrans != null && wagonsRootTrans.Find("PerimeterRails") != null)
                {
                    dispatcher.GeneratePerimeterRails();
                }
            }

            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(gameObject);
            #endif
        }

        /// <summary>
        /// Tüm sahne tasarımını belirtilen hedef dünya merkezine konumlandırır (Örn: Y = 1.6m).
        /// </summary>
        public void SetSceneCenter(Vector3 targetCenter)
        {
            OrganizeSceneHierarchy();
            transform.position = targetCenter;
            EnsureWorldFramePreview(false);

            TruckDispatcher dispatcher = Object.FindFirstObjectByType<TruckDispatcher>();
            if (dispatcher != null)
            {
                dispatcher.SetupPerimeterLoop();
                Transform wagonsRootTrans = dispatcher.WagonsRoot;
                if (wagonsRootTrans == null)
                {
                    GameObject wr = GameObject.Find("[PerimeterWagonsRoot]");
                    if (wr != null) wagonsRootTrans = wr.transform;
                }
                if (wagonsRootTrans != null && wagonsRootTrans.Find("PerimeterRails") != null)
                {
                    dispatcher.GeneratePerimeterRails();
                }
            }

            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(gameObject);
            #endif
        }

        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Camera cam = GetActiveCamera();
            EnsureTargetFrameRect();

            if (CalculateTargetWorldBounds(cam, out Vector3 center, out float width, out float height))
            {
                float fullW = width / Mathf.Max(0.01f, 1f - m_InnerPadding * 2f);
                float fullH = height / Mathf.Max(0.01f, 1f - m_InnerPadding * 2f);

                // 1. Dış Çerçeve Sınırı (Parlak Mavi/Turkuaz Tel Kafes)
                Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
                Gizmos.DrawWireCube(new Vector3(center.x, center.y, m_TargetZ), new Vector3(fullW, fullH, 0.05f));

                // 2. İç Oynanabilir Alan (Piksel Izgarası Sınırı - Altın Sarısı)
                Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.75f);
                Gizmos.DrawWireCube(new Vector3(center.x, center.y, m_TargetZ), new Vector3(width, height, 0.05f));

                // 3. Çerçeve Köşe Tutamaçları (Yeşil)
                float handleSize = Mathf.Min(width, height) * 0.035f;
                Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.85f);
                Vector3 half = new Vector3(fullW * 0.5f, fullH * 0.5f, 0f);
                Gizmos.DrawWireCube(new Vector3(center.x - half.x, center.y - half.y, m_TargetZ), Vector3.one * handleSize);
                Gizmos.DrawWireCube(new Vector3(center.x + half.x, center.y - half.y, m_TargetZ), Vector3.one * handleSize);
                Gizmos.DrawWireCube(new Vector3(center.x - half.x, center.y + half.y, m_TargetZ), Vector3.one * handleSize);
                Gizmos.DrawWireCube(new Vector3(center.x + half.x, center.y + half.y, m_TargetZ), Vector3.one * handleSize);
            }
        }
        #endif
    }
}
