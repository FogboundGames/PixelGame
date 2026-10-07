using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelGame
{
    public enum MysteryRevealCondition
    {
        WhenExposed = 0,         // Dış havaya açıldığında (Erişilebilir olunca)
        WhenNeighborCleared = 1  // Bitişik herhangi bir komşu toplandığında
    }

    /// <summary>
    /// Bölümün zorluk kademesi. Varsayılan olarak LevelDifficultyReport analizinden
    /// atanır (Level Designer → "Kademeleri Analizden Ata").
    /// </summary>
    public enum LevelDifficultyTier
    {
        Kolay = 0,
        Orta = 1,
        Zor = 2
    }

    [Serializable]
    public class PaletteColorOverride
    {
        public Color originalColor = Color.white;
        public Color targetColor = Color.white;
        public int pixelCount = 0;
        public string label = "";

        public PaletteColorOverride() { }

        public PaletteColorOverride(Color original, Color target, int count = 0, string lbl = "")
        {
            originalColor = original;
            targetColor = target;
            pixelCount = count;
            label = lbl;
        }

        public bool IsOverridden => !ColorsMatch(originalColor, targetColor);

        public static bool ColorsMatch(Color a, Color b, float threshold = 0.03f)
        {
            return Mathf.Abs(a.r - b.r) < threshold &&
                   Mathf.Abs(a.g - b.g) < threshold &&
                   Mathf.Abs(a.b - b.b) < threshold &&
                   Mathf.Abs(a.a - b.a) < threshold;
        }
    }

    [Serializable]
    public class WagonSequenceEntry
    {
        public Color wagonColor = Color.white;
        [Min(1)]
        public int capacity = 16;
        public int paletteIndex = 0;
        public string label = "Vagon";

        [Tooltip("Eğer bu gemi başka bir gemiye bağlıysa her ikisi aynı pozitif ID'yi taşır (örn: 1, 2, 3...). 0 = Bağımsız gemi.")]
        public int linkId = 0;

        [Tooltip("Gizli gemi: Kuyrukta en ön sıraya gelene kadar rengi ve kapasite yazısı '?' desenli örtüyle saklanır.")]
        public bool isHidden = false;

        public WagonSequenceEntry() { }

        public WagonSequenceEntry(Color color, int cap = 16, int palIdx = 0, string lbl = "Vagon", int link = 0)
        {
            wagonColor = color;
            capacity = cap;
            paletteIndex = palIdx;
            label = lbl;
            linkId = link;
        }
    }

    public enum TruckPart
    {
        Tires,
        Rims,
        Glass,
        Headlights,
        Taillights,
        Chassis,
        Cabin,
        Cargo,
        Stone,
        Wood,
        Dark,
        StoneDark,
        MechaBody,
        Helmet,
        HelmetDark,
        Lamp
    }

    [Serializable]
    public class TruckPartColorSetting
    {
        public TruckPart part;
        [Tooltip("İşaretliyse bu parça sabit renk yerine o anki vagonun/hedef bloğun rengini alır.")]
        public bool matchBlockColor = false;
        public Color customColor = Color.white;

        public TruckPartColorSetting() { }

        public TruckPartColorSetting(TruckPart part, bool matchBlockColor, Color customColor)
        {
            this.part = part;
            this.matchBlockColor = matchBlockColor;
            this.customColor = customColor;
        }
    }

    [Serializable]
    public class LevelColorTheme
    {
        [SerializeField] private List<TruckPartColorSetting> m_Parts = new List<TruckPartColorSetting>();

        public List<TruckPartColorSetting> Parts => m_Parts;

        public LevelColorTheme()
        {
            ResetToDefault();
        }

        public void ResetToDefault()
        {
            m_Parts = new List<TruckPartColorSetting>();
            AddPart(TruckPart.Cabin, true, new Color32(230, 40, 40, 255));
            AddPart(TruckPart.Cargo, true, new Color32(230, 40, 40, 255));
            AddPart(TruckPart.Tires, false, new Color32(50, 50, 55, 255));
            AddPart(TruckPart.Rims, false, new Color32(195, 197, 202, 255));
            AddPart(TruckPart.Glass, false, new Color32(90, 110, 130, 255));
            AddPart(TruckPart.Headlights, false, new Color32(252, 250, 238, 255));
            AddPart(TruckPart.Taillights, false, new Color32(135, 24, 30, 255));
            AddPart(TruckPart.Chassis, false, new Color32(60, 60, 66, 255));
            AddPart(TruckPart.Stone, false, new Color32(128, 132, 140, 255));
            AddPart(TruckPart.StoneDark, false, new Color32(96, 99, 108, 255));
            AddPart(TruckPart.Wood, false, new Color32(120, 78, 48, 255));
            AddPart(TruckPart.Dark, false, new Color32(18, 16, 22, 255));
            AddPart(TruckPart.MechaBody, false, new Color32(235, 100, 20, 255));
            AddPart(TruckPart.Helmet, false, new Color32(255, 200, 0, 255));
            AddPart(TruckPart.HelmetDark, false, new Color32(40, 44, 52, 255));
            AddPart(TruckPart.Lamp, false, new Color32(255, 248, 200, 255));
        }

        private void AddPart(TruckPart part, bool matchBlock, Color color)
        {
            m_Parts.Add(new TruckPartColorSetting(part, matchBlock, color));
        }

        public TruckPartColorSetting GetSetting(TruckPart part)
        {
            EnsureAllPartsPresent();
            for (int i = 0; i < m_Parts.Count; i++)
            {
                if (m_Parts[i].part == part) return m_Parts[i];
            }
            var s = new TruckPartColorSetting(part, false, GetDefaultPartColor(part));
            m_Parts.Add(s);
            return s;
        }

        public Color ResolveColor(TruckPart part, Color blockColor)
        {
            var setting = GetSetting(part);
            return setting.matchBlockColor ? blockColor : setting.customColor;
        }

        public void EnsureAllPartsPresent()
        {
            if (m_Parts == null) m_Parts = new List<TruckPartColorSetting>();
            Array allValues = Enum.GetValues(typeof(TruckPart));
            foreach (TruckPart p in allValues)
            {
                bool exists = false;
                for (int i = 0; i < m_Parts.Count; i++)
                {
                    if (m_Parts[i].part == p) { exists = true; break; }
                }
                if (!exists)
                {
                    bool match = (p == TruckPart.Cabin || p == TruckPart.Cargo);
                    m_Parts.Add(new TruckPartColorSetting(p, match, GetDefaultPartColor(p)));
                }
            }
        }

        public static Color GetDefaultPartColor(TruckPart part)
        {
            switch (part)
            {
                case TruckPart.Tires: return new Color32(50, 50, 55, 255);
                case TruckPart.Rims: return new Color32(195, 197, 202, 255);
                case TruckPart.Glass: return new Color32(90, 110, 130, 255);
                case TruckPart.Headlights: return new Color32(252, 250, 238, 255);
                case TruckPart.Taillights: return new Color32(135, 24, 30, 255);
                case TruckPart.Chassis: return new Color32(60, 60, 66, 255);
                case TruckPart.Cabin: return new Color32(230, 40, 40, 255);
                case TruckPart.Cargo: return new Color32(230, 40, 40, 255);
                case TruckPart.Stone: return new Color32(128, 132, 140, 255);
                case TruckPart.StoneDark: return new Color32(96, 99, 108, 255);
                case TruckPart.Wood: return new Color32(120, 78, 48, 255);
                case TruckPart.Dark: return new Color32(18, 16, 22, 255);
                case TruckPart.MechaBody: return new Color32(235, 100, 20, 255);
                case TruckPart.Helmet: return new Color32(255, 200, 0, 255);
                case TruckPart.HelmetDark: return new Color32(40, 44, 52, 255);
                case TruckPart.Lamp: return new Color32(255, 248, 200, 255);
                default: return Color.white;
            }
        }
    }

    /// <summary>
    /// Tek bir piksel sanatı bölümünü (Level) temsil eden ScriptableObject veri varlığı.
    /// Renk paletini çıkarma, renk değiştirme (recolor), ton kaydırma (hue shift) ve filtreleme yeteneklerine sahiptir.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPixelLevel", menuName = "PixelGame/Level Data", order = 100)]
    public class PixelLevelData : ScriptableObject
    {
        [Header("📋 Bölüm Bilgileri")]
        [Tooltip("Bölümün adı (örn: Sevimli Rakun, Renkli Kalp)")]
        [SerializeField] private string m_LevelName = "Yeni Bölüm";

        [Tooltip("Bölüm sırası / numarası")]
        [SerializeField] private int m_LevelIndex = 1;

        [Tooltip("Bölümün zorluk kademesi. Genelde Level Designer'daki 'Kademeleri Analizden Ata' ile LevelDifficultyReport'tan atanır. " +
                 "Zor kademesindeyse üst barda tavşanın yanında 'HARD' rozeti görünür.")]
        [SerializeField] private LevelDifficultyTier m_DifficultyTier = LevelDifficultyTier.Kolay;

        /// <summary>Eski m_IsHardLevel bool'unun kademe alanına taşınması için; taşındıktan sonra false kalır.</summary>
        [HideInInspector, SerializeField, UnityEngine.Serialization.FormerlySerializedAs("m_IsHardLevel")]
        private bool m_LegacyIsHardLevel = false;

        [Header("🖼️ Kaynak Görsel")]
        [Tooltip("Bölümde küplerle çizilecek piksel resmi")]
        [SerializeField] private Texture2D m_LevelTexture;

        [Tooltip("Optimizasyondan önceki orijinal görsel (renk sayısını tekrar 5 veya 6 yapabilmek için korunur)")]
        [SerializeField] private Texture2D m_OriginalSourceTexture;

        [Tooltip("Alternatif olarak Sprite seçilebilir")]
        [SerializeField] private Sprite m_LevelSprite;

        [Header("📐 Çözünürlük & Uyum")]
        [Tooltip("Görselin kendi piksel boyutunu birebir (1:1) kullan. Görselin bozulmasını kesinlikle engeller!")]
        [SerializeField] private bool m_UseNativeResolution = true;

        [Tooltip("Eğer 'Use Native Resolution' kapalıysa kullanılacak özel ızgara çözünürlüğü")]
        [SerializeField] private Vector2Int m_CustomResolution = new Vector2Int(24, 24);

        [Header("🎨 Renk Paleti & Değiştirme (Recolor)")]
        [Tooltip("Görselden çıkarılan ve tek tek özelleştirilebilen renk paleti")]
        [SerializeField] private List<PaletteColorOverride> m_ColorPalette = new List<PaletteColorOverride>();

        [Tooltip("Genel renk filtresi (Tüm görsele uygulanacak ton)")]
        [SerializeField] private Color m_TintColor = Color.white;

        [Tooltip("Tüm görselin renk tonunu kaydır (Hue Shift, -180 ile +180 derece)")]
        [Range(-180f, 180f)]
        [SerializeField] private float m_HueShift = 0f;

        [Header("☀️ Canlılık & Işıma")]
        [Tooltip("Renk parlaklığı")]
        [Range(0.5f, 2.5f)]
        [SerializeField] private float m_ColorBrightness = 1.0f;

        [Tooltip("Renk doygunluğu")]
        [Range(0f, 2.5f)]
        [SerializeField] private float m_ColorSaturation = 1.0f;

        [Tooltip("Renk kontrastı")]
        [Range(0.5f, 2f)]
        [SerializeField] private float m_ColorContrast = 1.0f;

        [Tooltip("Işıma yoğunluğu (Arkadan aydınlatma canlılığı)")]
        [Range(0f, 2f)]
        [SerializeField] private float m_EmissionIntensity = 0.0f;

        [Header("🔲 Izgara & Küp Ayarları")]
        [Tooltip("Küpler arasındaki DİKEY (satırlar/önler, Y ekseni) boşluk oranı (0.04 = %4 boşluk, negatif = üst üste biner)")]
        [Range(-0.3f, 1.0f)]
        [SerializeField] private float m_CubeSpacing = 0.06f;

        [Tooltip("Küpler arasındaki YATAY (aynı satırdaki yanlar, X ekseni) boşluk oranı — Dikey'den bağımsız")]
        [Range(-0.3f, 1.0f)]
        [SerializeField] private float m_CubeSpacingX = 0.06f;

        [Tooltip("Küplerin 3D kabartma derinliği")]
        [Range(0.05f, 2f)]
        [SerializeField] private float m_CubeDepth = 0.4f;

        [Tooltip("Panonun X eksenindeki 3D eğim açısı (derece). Küplerin alt/ön et kalınlığının kameraya görünmesini sağlar.")]
        [Range(-45f, 45f)]
        [SerializeField] private float m_BoardTiltAngle = 18f;

        [Tooltip("Kameraya tam karşıdan bakıldığında küpün sadece üstü görünür; bu açı küpü eğerek hem üst hem ön yüzünü görünür kılar. " +
                 "Negatif değer küpü ters yöne eğer; tam tur (-180..180) serbesttir.")]
        [Range(-180f, 180f)]
        [SerializeField] private float m_CubeFrontTiltAngle = 25f;

        [Tooltip("Küplerin 3D dünyadaki Z düzlemi mesafesi")]
        [SerializeField] private float m_TargetZ = 0f;

        [Tooltip("Her satır (GridY arttıkça) küpün konumuna eklenen serbest X/Y/Z kademesi")]
        [SerializeField] private Vector3 m_CubeRowStepOffset = Vector3.zero;

        [Tooltip("Mavi çerçevenin iç kenar payı")]
        [Range(0f, 0.3f)]
        [SerializeField] private float m_InnerPadding = 0.08f;

        [Tooltip("Şeffaf (alpha < 0.1) pikseller için küp oluşturulmasın mı?")]
        [SerializeField] private bool m_SkipTransparent = true;

        [Header("❓ Gizli / Soru İşaretli Küpler (Mystery Cubes)")]
        [Tooltip("Bölüm başladığında rengi '?' ile gizli olan ve sonradan açılan küplerin koordinatları")]
        [SerializeField] private List<Vector2Int> m_MysteryCubeCoordinates = new List<Vector2Int>();

        [Tooltip("Gizli küplerin ortaya çıkma (açılma) koşulu")]
        [SerializeField] private MysteryRevealCondition m_MysteryRevealCondition = MysteryRevealCondition.WhenExposed;

        [Tooltip("Gizli küpün koyu arka plan rengi")]
        [SerializeField] private Color m_MysteryCubeColor = new Color(0.125f, 0.118f, 0.306f, 1f); // #201E4E

        [Header("🚚 Kamyon Düzeni")]
        [Tooltip("Öndeki park yerlerinin görseli. Boş bırakılırsa sahnedeki kurulumdan gelen " +
                 "görsel kullanılır; buraya bir sprite sürüklersen bu bölüme özel olur.")]
        [SerializeField] private Sprite m_SlotSprite;

        [Tooltip("Ray üzerinde aynı anda kaç vagon doldurulabilir. Aynı anda kaç renge " +
                 "çalışılabileceğini belirler; bölümün zorluğunu en çok bu ayar etkiler. " +
                 "1 verilirse tek vagonlu, çok daha kısıtlayıcı bir bölüm olur.")]
        [Range(1, 8)]
        [SerializeField] private int m_SlotCount = 5;

        [Tooltip("Havuzda yan yana kaç kamyon beklesin")]
        [Range(1, 8)]
        [SerializeField] private int m_PoolColumns = 5;

        [Tooltip("Havuzda kaç sıra kamyon beklesin. Sıra arttıkça oyuncu daha ilerisini görür.")]
        [Range(1, 5)]
        [SerializeField] private int m_PoolRows = 2;

        [Tooltip("Bir kamyonun kasasına kaç küp sığar. Küçük değer daha çok kamyon demektir; " +
                 "bölümdeki toplam küp sayısına göre ayarla.")]
        [Min(1)]
        [SerializeField] private int m_TruckCapacity = 16;

        [Tooltip("Gemi sırası üretilirken / karıştırılırken en küçük gemi kapasitesi.")]
        [Min(1)]
        [SerializeField] private int m_MinTruckCapacity = 4;

        [Tooltip("Gemi kapasitelerinin en sık geleceği değer. Sayılar bunun etrafında yoğunlaşır; " +
                 "en az ve en çok (Truck Capacity) değerleri seyrek gelir.")]
        [Min(1)]
        [SerializeField] private int m_PeakTruckCapacity = 10;

        [Tooltip("Açıkken gemi sayıları ağırlıklı olarak 10'un katları olur (10, 20...), aralarda nadiren ara sayılar " +
                 "(12, 13, 17...) gelir. Kapalıyken sayılar 'en sık' değeri etrafında karışık dağılır.")]
        [SerializeField] private bool m_UseRoundCapacities = true;
        [Tooltip("10'un katı olan gemilerin oranı (0–1). 0.8 → 10 gemiden ~8'i 10/20.")]
        [Range(0f, 1f)]
        [SerializeField] private float m_RoundCapacityRatio = 0.8f;

        [Header("🚚 Manuel Vagon & Maden Arabası Sıra Tasarımı (Opsiyonel Override)")]
        [Tooltip("Açıksa vagonlar otomatik rastgele karıştırılmaz; aşağıda dizilen birebir sırada ve kapasitelerle oyuna gelir.")]
        [SerializeField] private bool m_UseCustomWagonSequence = false;
        [SerializeField] private List<WagonSequenceEntry> m_WagonSequence = new List<WagonSequenceEntry>();

        [Header("🌑 Gölge Özelleştirme (Opsiyonel)")]
        [Tooltip("Bu bölüme özel kontur gölgesi dokusu (Boş bırakılırsa görselden otomatik üretilir)")]
        [SerializeField] private Texture2D m_FigureShadowTexture;

        [Header("🎨 Vagon, Madenci & Çevre Renk Teması (Opsiyonel Override)")]
        [Tooltip("Bu bölüme özel tema tanımlamak isterseniz açın. Kapalıysa oyunun Genel Tema Ayarları (GameThemeSettings) kullanılır.")]
        [SerializeField] private bool m_UseCustomColorTheme = false;
        [SerializeField] private LevelColorTheme m_ColorTheme = new LevelColorTheme();

        // Public Properties
        public string LevelName { get => m_LevelName; set => m_LevelName = value; }
        public int LevelIndex { get => m_LevelIndex; set => m_LevelIndex = value; }

        /// <summary>Zorluk kademesi (Kolay / Orta / Zor). Analizden atanır; HUD rozeti bunu kullanır.</summary>
        public LevelDifficultyTier DifficultyTier { get => m_DifficultyTier; set => m_DifficultyTier = value; }

        /// <summary>Eski alanla uyumluluk: yalnızca Zor kademesinde true döner.</summary>
        public bool IsHardLevel => m_DifficultyTier == LevelDifficultyTier.Zor;

        private void OnValidate()
        {
            // Göç: eski m_IsHardLevel bool'unu yeni kademeye taşı (true → Zor).
            if (m_LegacyIsHardLevel)
            {
                if (m_DifficultyTier == LevelDifficultyTier.Kolay)
                    m_DifficultyTier = LevelDifficultyTier.Zor;
                m_LegacyIsHardLevel = false;
            }
        }

        public Texture2D LevelTexture { get => m_LevelTexture; set => m_LevelTexture = value; }
        public Texture2D OriginalSourceTexture { get => m_OriginalSourceTexture; set => m_OriginalSourceTexture = value; }
        public Sprite LevelSprite { get => m_LevelSprite; set => m_LevelSprite = value; }
        public Texture2D FigureShadowTexture { get => m_FigureShadowTexture; set => m_FigureShadowTexture = value; }
        public bool UseCustomColorTheme { get => m_UseCustomColorTheme; set => m_UseCustomColorTheme = value; }
        public LevelColorTheme ColorTheme
        {
            get
            {
                if (m_UseCustomColorTheme)
                {
                    if (m_ColorTheme == null) m_ColorTheme = new LevelColorTheme();
                    m_ColorTheme.EnsureAllPartsPresent();
                    return m_ColorTheme;
                }
                return GameThemeSettings.CurrentTheme;
            }
            set => m_ColorTheme = value;
        }
        public LevelColorTheme CustomColorTheme
        {
            get
            {
                if (m_ColorTheme == null) m_ColorTheme = new LevelColorTheme();
                m_ColorTheme.EnsureAllPartsPresent();
                return m_ColorTheme;
            }
        }
        public bool UseNativeResolution { get => m_UseNativeResolution; set => m_UseNativeResolution = value; }
        public Vector2Int CustomResolution { get => m_CustomResolution; set => m_CustomResolution = value; }
        public List<PaletteColorOverride> ColorPalette => m_ColorPalette;
        public Color TintColor { get => m_TintColor; set => m_TintColor = value; }
        public float HueShift { get => m_HueShift; set => m_HueShift = value; }
        public float ColorBrightness { get => m_ColorBrightness; set => m_ColorBrightness = value; }
        public float ColorSaturation { get => m_ColorSaturation; set => m_ColorSaturation = value; }
        public float ColorContrast { get => m_ColorContrast; set => m_ColorContrast = value; }
        public float EmissionIntensity { get => m_EmissionIntensity; set => m_EmissionIntensity = value; }
        public float CubeSpacing { get => m_CubeSpacing; set => m_CubeSpacing = value; }
        public float CubeSpacingX { get => m_CubeSpacingX; set => m_CubeSpacingX = value; }
        public float CubeDepth { get => m_CubeDepth; set => m_CubeDepth = value; }
        public float BoardTiltAngle { get => m_BoardTiltAngle; set => m_BoardTiltAngle = value; }
        public float CubeFrontTiltAngle { get => m_CubeFrontTiltAngle; set => m_CubeFrontTiltAngle = value; }
        public float TargetZ { get => m_TargetZ; set => m_TargetZ = value; }
        public Vector3 CubeRowStepOffset { get => m_CubeRowStepOffset; set => m_CubeRowStepOffset = value; }
        public float InnerPadding { get => m_InnerPadding; set => m_InnerPadding = value; }
        public bool SkipTransparent { get => m_SkipTransparent; set => m_SkipTransparent = value; }

        public List<Vector2Int> MysteryCubeCoordinates
        {
            get
            {
                if (m_MysteryCubeCoordinates == null) m_MysteryCubeCoordinates = new List<Vector2Int>();
                return m_MysteryCubeCoordinates;
            }
            set => m_MysteryCubeCoordinates = value;
        }

        public MysteryRevealCondition MysteryRevealCondition
        {
            get => m_MysteryRevealCondition;
            set => m_MysteryRevealCondition = value;
        }

        public Color MysteryCubeColor
        {
            get => m_MysteryCubeColor;
            set => m_MysteryCubeColor = value;
        }

        public bool IsMysteryCube(int x, int y)
        {
            if (m_MysteryCubeCoordinates == null) return false;
            return m_MysteryCubeCoordinates.Contains(new Vector2Int(x, y));
        }

        public bool SetMysteryCube(int x, int y, bool isMystery)
        {
            if (m_MysteryCubeCoordinates == null) m_MysteryCubeCoordinates = new List<Vector2Int>();
            Vector2Int pos = new Vector2Int(x, y);
            bool contains = m_MysteryCubeCoordinates.Contains(pos);
            if (isMystery && !contains)
            {
                m_MysteryCubeCoordinates.Add(pos);
                return true;
            }
            else if (!isMystery && contains)
            {
                m_MysteryCubeCoordinates.Remove(pos);
                return true;
            }
            return false;
        }

        public bool ToggleMysteryCube(int x, int y)
        {
            if (m_MysteryCubeCoordinates == null) m_MysteryCubeCoordinates = new List<Vector2Int>();
            Vector2Int pos = new Vector2Int(x, y);
            if (m_MysteryCubeCoordinates.Contains(pos))
            {
                m_MysteryCubeCoordinates.Remove(pos);
                return false;
            }
            else
            {
                m_MysteryCubeCoordinates.Add(pos);
                return true;
            }
        }

        public void ClearMysteryCubes()
        {
            if (m_MysteryCubeCoordinates != null)
                m_MysteryCubeCoordinates.Clear();
        }

        public int GetMysteryCubeCount()
        {
            return m_MysteryCubeCoordinates != null ? m_MysteryCubeCoordinates.Count : 0;
        }

        public void SetMysteryRegion(int minX, int maxX, int minY, int maxY, bool isMystery)
        {
            if (m_MysteryCubeCoordinates == null) m_MysteryCubeCoordinates = new List<Vector2Int>();
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    SetMysteryCube(x, y, isMystery);
                }
            }
        }

        public void SetMysteryByColor(Color targetCol, bool isMystery, float threshold = 0.05f)
        {
            Texture2D tex = GetActiveTexture();
            if (tex == null) return;
            Vector2Int res = GetGridResolution();
            int cols = res.x;
            int rows = res.y;

            for (int x = 0; x < cols; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    int px = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) / cols * tex.width), 0, tex.width - 1);
                    int py = Mathf.Clamp(Mathf.FloorToInt((y + 0.5f) / rows * tex.height), 0, tex.height - 1);
                    Color c = tex.GetPixel(px, py);
                    if (m_SkipTransparent && c.a < 0.1f) continue;
                    c = ApplyColorPipeline(c);
                    if (PaletteColorOverride.ColorsMatch(c, targetCol, threshold))
                    {
                        SetMysteryCube(x, y, isMystery);
                    }
                }
            }
        }

        public Sprite SlotSprite { get => m_SlotSprite; set => m_SlotSprite = value; }
        public int SlotCount { get => m_SlotCount; set => m_SlotCount = Mathf.Max(1, value); }
        public int PoolColumns { get => m_PoolColumns; set => m_PoolColumns = Mathf.Max(1, value); }
        public int PoolRows { get => m_PoolRows; set => m_PoolRows = Mathf.Max(1, value); }
        public int TruckCapacity { get => m_TruckCapacity; set => m_TruckCapacity = Mathf.Max(1, value); }
        /// <summary>Karışık kapasitenin alt sınırı; hiçbir zaman TruckCapacity'yi geçmez.</summary>
        public int MinTruckCapacity { get => Mathf.Clamp(m_MinTruckCapacity, 1, Mathf.Max(1, m_TruckCapacity)); set => m_MinTruckCapacity = Mathf.Max(1, value); }
        /// <summary>Kapasitelerin yoğunlaştığı değer; her zaman [MinTruckCapacity, TruckCapacity] içinde.</summary>
        public bool UseRoundCapacities { get => m_UseRoundCapacities; set => m_UseRoundCapacities = value; }
        public float RoundCapacityRatio { get => Mathf.Clamp01(m_RoundCapacityRatio); set => m_RoundCapacityRatio = Mathf.Clamp01(value); }
        public int PeakTruckCapacity { get => Mathf.Clamp(m_PeakTruckCapacity, MinTruckCapacity, Mathf.Max(1, m_TruckCapacity)); set => m_PeakTruckCapacity = Mathf.Max(1, value); }
        public bool UseCustomWagonSequence { get => m_UseCustomWagonSequence; set => m_UseCustomWagonSequence = value; }
        public List<WagonSequenceEntry> WagonSequence 
        { 
            get 
            { 
                if (m_WagonSequence == null) m_WagonSequence = new List<WagonSequenceEntry>(); 
                return m_WagonSequence; 
            } 
            set => m_WagonSequence = value; 
        }

        /// <summary>Havuzda aynı anda görünen kamyon sayısı.</summary>
        public int PoolPlaceCount => m_PoolColumns * m_PoolRows;

        /// <summary>
        /// Paletteki küp sayılarına göre otomatik varsayılan bir vagon sırası oluşturur.
        /// Level Designer üzerinden bu sırayı özelleştirmek için başlangıç noktası sağlar.
        /// </summary>
        public void GenerateDefaultWagonSequenceFromPalette()
        {
            if (m_WagonSequence == null) m_WagonSequence = new List<WagonSequenceEntry>();
            m_WagonSequence.Clear();

            if (m_ColorPalette == null || m_ColorPalette.Count == 0) return;

            int cap = Mathf.Max(1, m_TruckCapacity);
            var rng = new System.Random();
            bool hasAdjustment = (m_ColorBrightness != 1f || m_ColorSaturation != 1f || m_ColorContrast != 1f);

            for (int i = 0; i < m_ColorPalette.Count; i++)
            {
                PaletteColorOverride entry = m_ColorPalette[i];
                if (entry == null || entry.pixelCount <= 0) continue;

                Color wagonColor = entry.targetColor;
                if (hasAdjustment)
                {
                    wagonColor = PixelCube.AdjustColor(wagonColor, m_ColorBrightness, m_ColorSaturation, m_ColorContrast);
                }

                string nameLabel = string.IsNullOrEmpty(entry.label) ? $"Renk #{i + 1}" : entry.label;
                List<int> loads = SplitForLevel(entry.pixelCount, cap, rng);
                for (int w = 0; w < loads.Count; w++)
                {
                    m_WagonSequence.Add(new WagonSequenceEntry(wagonColor, loads[w], i, $"{nameLabel} ({w + 1})"));
                }
            }
        }

        /// <summary>
        /// Renk paletindeki küpleri renklere göre sıralı değil, dengeli karışık (Round-Robin Interleaved)
        /// vagon dizisi halinde oluşturur. (Örn: Red 1 -> Green 1 -> Blue 1 -> Red 2 -> Green 2...)
        /// </summary>
        public void GenerateInterleavedWagonSequenceFromPalette()
        {
            if (m_WagonSequence == null) m_WagonSequence = new List<WagonSequenceEntry>();
            m_WagonSequence.Clear();

            if (m_ColorPalette == null || m_ColorPalette.Count == 0) return;

            int cap = Mathf.Max(1, m_TruckCapacity);
            var rng = new System.Random();
            bool hasAdjustment = (m_ColorBrightness != 1f || m_ColorSaturation != 1f || m_ColorContrast != 1f);

            List<List<WagonSequenceEntry>> colorWagonLists = new List<List<WagonSequenceEntry>>();

            for (int i = 0; i < m_ColorPalette.Count; i++)
            {
                PaletteColorOverride entry = m_ColorPalette[i];
                if (entry == null || entry.pixelCount <= 0) continue;

                Color wagonColor = entry.targetColor;
                if (hasAdjustment)
                {
                    wagonColor = PixelCube.AdjustColor(wagonColor, m_ColorBrightness, m_ColorSaturation, m_ColorContrast);
                }

                List<WagonSequenceEntry> list = new List<WagonSequenceEntry>();
                string nameLabel = string.IsNullOrEmpty(entry.label) ? $"Renk #{i + 1}" : entry.label;
                List<int> loads = SplitForLevel(entry.pixelCount, cap, rng);
                for (int w = 0; w < loads.Count; w++)
                {
                    list.Add(new WagonSequenceEntry(wagonColor, loads[w], i, $"{nameLabel} ({w + 1})"));
                }
                colorWagonLists.Add(list);
            }

            bool addedAny = true;
            int stepIndex = 0;
            while (addedAny)
            {
                addedAny = false;
                for (int c = 0; c < colorWagonLists.Count; c++)
                {
                    if (stepIndex < colorWagonLists[c].Count)
                    {
                        m_WagonSequence.Add(colorWagonLists[c][stepIndex]);
                        addedAny = true;
                    }
                }
                stepIndex++;
            }
        }

        /// <summary>
        /// Bir rengin küplerini [minCap, maxCap] aralığında karışık kapasiteli gemilere böler; toplam aynen korunur.
        /// Kapasiteler peakCap etrafında yoğunlaşan üçgen dağılımdan çekilir (uç değerler seyrek gelir).
        /// </summary>
        /// <summary>Bu bölümün ayarlarına göre (yuvarlak mod ya da en-sık dağılımı) bir rengi gemilere böler.</summary>
        private List<int> SplitForLevel(int total, int maxCap, System.Random rng)
        {
            return m_UseRoundCapacities
                ? SplitCapacitiesRound(total, MinTruckCapacity, maxCap, RoundCapacityRatio, rng)
                : SplitCapacities(total, MinTruckCapacity, PeakTruckCapacity, maxCap, rng);
        }

        /// <summary>
        /// Rengi ağırlıklı olarak 10'un katı kapasiteli gemilere böler (10, 20...); roundRatio dışındaki gemiler
        /// [minCap, maxCap] içindeki ara sayılardır. Toplam aynen korunur: rengin küp sayısı 10'a tam bölünmüyorsa
        /// artan kısım ara sayılı bir gemide toplanır; renk minCap'ten azsa tek küçük gemi olur.
        /// </summary>
        public static List<int> SplitCapacitiesRound(int total, int minCap, int maxCap, float roundRatio, System.Random rng)
        {
            var loads = new List<int>();
            if (total <= 0) return loads;
            maxCap = Mathf.Max(1, maxCap);
            minCap = Mathf.Clamp(minCap, 1, maxCap);

            var rounds = new List<int>();
            for (int r = 10; r <= maxCap; r += 10) if (r >= minCap) rounds.Add(r);
            if (rounds.Count == 0) return SplitCapacities(total, minCap, (minCap + maxCap) / 2, maxCap, rng);

            int remaining = total;
            int guard = 0;
            while (remaining > 0 && guard++ < 10000)
            {
                // Kalan tek gemiye sığıyorsa bitir (bu bir yuvarlak sayıysa zaten yuvarlak kalır)
                if (remaining <= maxCap)
                {
                    if (remaining >= minCap || loads.Count == 0)
                    {
                        loads.Add(remaining);
                    }
                    else
                    {
                        // minCap'ten küçük artık: sığan bir gemiye eklenir, yoksa kendi küçük gemisi olur
                        int target = -1;
                        for (int i = 0; i < loads.Count; i++)
                            if (loads[i] + remaining <= maxCap && (target < 0 || loads[i] % 10 != 0)) target = i;
                        if (target >= 0) loads[target] += remaining; else loads.Add(remaining);
                    }
                    break;
                }

                int pick;
                if (rng.NextDouble() < roundRatio)
                {
                    pick = rounds[rng.Next(rounds.Count)];
                }
                else
                {
                    // Ara sayı (10'un katı olmayan)
                    pick = minCap + rng.Next(maxCap - minCap + 1);
                    if (pick % 10 == 0) pick = Mathf.Clamp(pick + (rng.Next(2) == 0 ? -1 : 1) * (1 + rng.Next(3)), minCap, maxCap);
                }

                // Geriye minCap'ten küçük bir artık kalmasın (son gemiyi gereksiz küçültmesin)
                int left = remaining - pick;
                if (left > 0 && left < minCap) pick = Mathf.Max(minCap, pick - (minCap - left));
                loads.Add(pick);
                remaining -= pick;
            }

            for (int i = loads.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = loads[i]; loads[i] = loads[j]; loads[j] = t;
            }
            return loads;
        }

        public static List<int> SplitCapacities(int total, int minCap, int peakCap, int maxCap, System.Random rng, int forcedCount = 0)
        {
            var loads = new List<int>();
            if (total <= 0) return loads;
            maxCap = Mathf.Max(1, maxCap);
            minCap = Mathf.Clamp(minCap, 1, maxCap);
            peakCap = Mathf.Clamp(peakCap, minCap, maxCap);

            int fewest = Mathf.CeilToInt(total / (float)maxCap);
            int most = Mathf.Max(fewest, total / minCap);
            float mean = (minCap + peakCap + maxCap) / 3f;
            int count = forcedCount > 0
                ? Mathf.Max(fewest, forcedCount)
                : Mathf.Clamp(Mathf.RoundToInt(total / mean), fewest, most);

            // Üçgen dağılım: tepe noktası peakCap
            float range = maxCap - minCap;
            float f = range > 0f ? (peakCap - minCap) / range : 0.5f;
            int sum = 0;
            for (int i = 0; i < count; i++)
            {
                double u = rng.NextDouble();
                float v = range <= 0f ? minCap
                    : u < f ? minCap + Mathf.Sqrt((float)u * range * (peakCap - minCap))
                            : maxCap - Mathf.Sqrt((float)(1.0 - u) * range * (maxCap - peakCap));
                int load = Mathf.Clamp(Mathf.RoundToInt(v), minCap, maxCap);
                loads.Add(load);
                sum += load;
            }

            // Toplamı tutturmak için rastgele gemilerde birer birer düzelt (dağılımın şekli korunur)
            int floor = minCap, ceil = maxCap, guard = 0;
            while (sum != total)
            {
                int i = rng.Next(count);
                if (sum > total && loads[i] > floor) { loads[i]--; sum--; guard = 0; }
                else if (sum < total && loads[i] < ceil) { loads[i]++; sum++; guard = 0; }
                else if (++guard > count * 16)
                {
                    // Aralık yetmiyor (çok az / çok fazla küp): sınırlar gevşetilir
                    if (sum > total) { if (floor <= 1) break; floor--; }
                    else ceil++;
                    guard = 0;
                }
            }
            return loads;
        }

        /// <summary>
        /// Mevcut gemi sırasındaki kapasiteleri [MinTruckCapacity, TruckCapacity] aralığında yeniden karıştırır.
        /// Renk başına toplam korunur; gerekirse aynı renkten yeni gemi araya eklenir ya da sondaki boşa çıkan gemi silinir.
        /// Sıradaki renk düzeni, bağlı ve gizli gemiler korunur.
        /// </summary>
        public void VaryWagonCapacities(System.Random rng = null)
        {
            if (m_WagonSequence == null || m_WagonSequence.Count == 0) return;
            if (rng == null) rng = new System.Random();
            int maxCap = Mathf.Max(1, m_TruckCapacity);

            var colorKeys = new List<int>();
            foreach (var w in m_WagonSequence)
                if (w != null && !colorKeys.Contains(w.paletteIndex)) colorKeys.Add(w.paletteIndex);

            foreach (int key in colorKeys)
            {
                var ships = new List<WagonSequenceEntry>();
                int total = 0;
                foreach (var w in m_WagonSequence)
                {
                    if (w == null || w.paletteIndex != key) continue;
                    ships.Add(w);
                    total += Mathf.Max(0, w.capacity);
                }
                if (ships.Count == 0 || total <= 0) continue;

                List<int> loads = SplitForLevel(total, maxCap, rng);

                // Fazla gemi: sondan, bağlı/gizli olmayanlar silinir
                for (int i = ships.Count - 1; i >= 0 && ships.Count > loads.Count; i--)
                {
                    if (ships[i].linkId != 0 || ships[i].isHidden) continue;
                    m_WagonSequence.Remove(ships[i]);
                    ships.RemoveAt(i);
                }
                if (ships.Count > loads.Count)
                    loads = SplitCapacities(total, MinTruckCapacity, PeakTruckCapacity, maxCap, rng, ships.Count);

                // Eksik gemi: aynı renkli rastgele bir geminin hemen arkasına eklenir
                while (ships.Count < loads.Count)
                {
                    WagonSequenceEntry src = ships[rng.Next(ships.Count)];
                    var copy = new WagonSequenceEntry(src.wagonColor, 1, src.paletteIndex, src.label);
                    m_WagonSequence.Insert(m_WagonSequence.IndexOf(src) + 1, copy);
                    ships.Add(copy);
                }

                // Kapasiteler karışık dağıtılır (sıra içinde büyük/küçük gemiler serpiştirilsin)
                for (int i = loads.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    int t = loads[i]; loads[i] = loads[j]; loads[j] = t;
                }
                for (int i = 0; i < ships.Count; i++) ships[i].capacity = loads[i];
            }
        }

        /// <summary>Bölüm görselinde kırılması gereken toplam küp sayısı.</summary>
        public int GetTotalCubeCountInPalette()
        {
            if (m_ColorPalette == null) return 0;
            int sum = 0;
            foreach (var entry in m_ColorPalette)
            {
                if (entry != null && entry.pixelCount > 0)
                {
                    sum += entry.pixelCount;
                }
            }
            return sum;
        }

        /// <summary>Sıradaki vagonların toplam taşıma kapasitesi.</summary>
        public int GetTotalWagonCapacity()
        {
            if (m_WagonSequence == null) return 0;
            int sum = 0;
            foreach (var wagon in m_WagonSequence)
            {
                if (wagon != null) sum += wagon.capacity;
            }
            return sum;
        }

        /// <summary>İki renk arasındaki basit RGB uzaklığı (0 = aynı).</summary>
        public static float ColorDistance(Color a, Color b)
        {
            return (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b)) / 3f;
        }

        /// <summary>Belirtilen renge atanmış vagonların toplam taşıma kapasitesi.</summary>
        public int GetTotalAssignedCapacityForColor(Color targetColor, float threshold = 0.05f)
        {
            if (m_WagonSequence == null) return 0;
            int sum = 0;
            foreach (var wagon in m_WagonSequence)
            {
                if (wagon == null) continue;
                if (ColorDistance(wagon.wagonColor, targetColor) <= threshold)
                {
                    sum += wagon.capacity;
                }
            }
            return sum;
        }


        #region 🔗 Bağlı Gemi (Linked Ships) Yönetimi

        /// <summary>
        /// Kullanılmayan en küçük pozitif Link ID'sini bulur (1, 2, 3...).
        /// </summary>
        public int GetNextAvailableLinkId()
        {
            if (m_WagonSequence == null) return 1;
            HashSet<int> used = new HashSet<int>();
            foreach (var w in m_WagonSequence)
            {
                if (w != null && w.linkId > 0) used.Add(w.linkId);
            }
            int id = 1;
            while (used.Contains(id)) id++;
            return id;
        }

        /// <summary>
        /// Sıradaki iki gemiyi birbirine bağlar (ikisine de aynı linkId'yi atar).
        /// </summary>
        /// <summary>
        /// Gemi sırasındaki halatları ve gizli gemileri oranlara göre yeniden üretir (önceki bağ/gizlilik silinir).
        /// Halatlar bütün sıraya yayılır: yan yana çiftler her dalgada, üst üste çiftler oyun başındaki havuzda.
        /// Gizli gemiler en ön sıra dışından ve halatsız gemilerden seçilir (ön sıradaki gizli gemi hemen açılır).
        /// </summary>
        /// <param name="linkRatio">Halatlı gemi oranı (0–1). 0.2 → gemilerin ~%20'si halatlı (çift sayısı bunun yarısı).</param>
        /// <param name="hiddenRatio">Gizli gemi oranı (0–1).</param>
        public void GenerateLinksAndHidden(float linkRatio, float hiddenRatio, System.Random rng, out int linkPairs, out int hiddenCount, out int targetPairs)
        {
            linkPairs = 0; hiddenCount = 0; targetPairs = 0;
            if (m_WagonSequence == null || m_WagonSequence.Count == 0) return;
            if (rng == null) rng = new System.Random();

            foreach (var w in m_WagonSequence)
            {
                if (w == null) continue;
                w.linkId = 0;
                w.isHidden = false;
            }

            int n = m_WagonSequence.Count;
            // Oyundaki havuzla aynı sınırlar (ShipQueuePool.RebuildSpots)
            int cols = Mathf.Clamp(m_PoolColumns, 1, 8);
            int rows = Mathf.Clamp(m_PoolRows, 1, 6);
            int poolSize = Mathf.Min(n, cols * rows);

            // Aday çiftler (Level Designer ızgarasında komşu olanlar), bütün sıraya yayılır:
            //  - yan yana (aynı dalga, komşu sütun): her yerde. Ardışık iki gemi kuyruğa arka arkaya geldiği için
            //    oyunda ikisi birlikte bulunur (sütunlar farklı ilerleyince halat çapraz kalabilir, sorun değil).
            //  - üst üste (aynı sütun): yalnızca oyun başındaki havuzda. Sonradan gelenlerde ortak birkaç gemi
            //    sonra kuyruğa gireceği için ilki ortaksız (halatsız) gönderilebilirdi.
            var pairs = new List<Vector2Int>();
            for (int i = 0; i < n; i++)
            {
                int col = i % cols;
                if (col + 1 < cols && i + 1 < n) pairs.Add(new Vector2Int(i, i + 1));            // yan yana
                if (i + cols < poolSize) pairs.Add(new Vector2Int(i, i + cols));                  // üst üste (havuz)
            }
            for (int i = pairs.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var t = pairs[i]; pairs[i] = pairs[j]; pairs[j] = t;
            }

            targetPairs = Mathf.RoundToInt(n * Mathf.Clamp01(linkRatio) * 0.5f);
            var used = new bool[n];
            foreach (var p in pairs)
            {
                if (linkPairs >= targetPairs) continue;
                if (used[p.x] || used[p.y]) continue;
                var a = m_WagonSequence[p.x];
                var b = m_WagonSequence[p.y];
                if (a == null || b == null) continue;
                used[p.x] = used[p.y] = true;
                linkPairs++;
                a.linkId = linkPairs;
                b.linkId = linkPairs;
            }

            // Gizli gemiler: ön sıra dışı, halatsız
            var hidCands = new List<int>();
            for (int i = Mathf.Min(cols, n); i < n; i++)
                if (!used[i] && m_WagonSequence[i] != null) hidCands.Add(i);
            int targetHidden = Mathf.Min(hidCands.Count, Mathf.RoundToInt(n * Mathf.Clamp01(hiddenRatio)));
            for (int k = 0; k < targetHidden; k++)
            {
                int pick = k + rng.Next(hidCands.Count - k);
                int t = hidCands[k]; hidCands[k] = hidCands[pick]; hidCands[pick] = t;
                m_WagonSequence[hidCands[k]].isHidden = true;
                hiddenCount++;
            }

            m_UseCustomWagonSequence = true;
        }

        public void LinkWagons(int indexA, int indexB)
        {
            if (m_WagonSequence == null) return;
            if (indexA < 0 || indexA >= m_WagonSequence.Count) return;
            if (indexB < 0 || indexB >= m_WagonSequence.Count) return;
            if (indexA == indexB) return;

            var wagonA = m_WagonSequence[indexA];
            var wagonB = m_WagonSequence[indexB];
            if (wagonA == null || wagonB == null) return;

            // Varsa eski bağları temizle
            UnlinkWagon(indexA);
            UnlinkWagon(indexB);

            int newLinkId = GetNextAvailableLinkId();
            wagonA.linkId = newLinkId;
            wagonB.linkId = newLinkId;
            m_UseCustomWagonSequence = true;
        }

        /// <summary>
        /// Verilen indeksteki geminin bağını ve partnerinin bağını koparır (linkId = 0).
        /// </summary>
        public void UnlinkWagon(int index)
        {
            if (m_WagonSequence == null || index < 0 || index >= m_WagonSequence.Count) return;
            var target = m_WagonSequence[index];
            if (target == null || target.linkId <= 0) return;

            int oldLinkId = target.linkId;
            foreach (var w in m_WagonSequence)
            {
                if (w != null && w.linkId == oldLinkId)
                {
                    w.linkId = 0;
                }
            }
            m_UseCustomWagonSequence = true;
        }

        /// <summary>
        /// Verilen indeksteki gemiyle aynı linkId'yi paylaşan diğer geminin indeksini döner (-1 yoksa).
        /// </summary>
        public int GetLinkedPartnerIndex(int index)
        {
            if (m_WagonSequence == null || index < 0 || index >= m_WagonSequence.Count) return -1;
            var target = m_WagonSequence[index];
            if (target == null || target.linkId <= 0) return -1;

            for (int i = 0; i < m_WagonSequence.Count; i++)
            {
                if (i != index && m_WagonSequence[i] != null && m_WagonSequence[i].linkId == target.linkId)
                {
                    return i;
                }
            }
            return -1;
        }

        #endregion

        /// <summary>
        /// Bu bölümü bitirmek için gereken toplam kamyon sayısı.
        /// </summary>
        public int GetRequiredTruckCount()
        {
            if (m_UseCustomWagonSequence && m_WagonSequence != null && m_WagonSequence.Count > 0)
            {
                return m_WagonSequence.Count;
            }

            if (m_ColorPalette == null) return 0;

            int total = 0;

            foreach (PaletteColorOverride entry in m_ColorPalette)
            {
                if (entry == null || entry.pixelCount <= 0) continue;
                total += Mathf.CeilToInt((float)entry.pixelCount / Mathf.Max(1, m_TruckCapacity));
            }

            return total;
        }

        public Texture2D GetActiveTexture()
        {
            if (m_LevelTexture != null) return m_LevelTexture;
            if (m_LevelSprite != null && m_LevelSprite.texture != null) return m_LevelSprite.texture;
            return null;
        }

        /// <summary>
        /// Orijinal (indirgenmemiş) kaynak dokuyu verir.
        /// </summary>
        public Texture2D GetOriginalTexture()
        {
            if (m_OriginalSourceTexture != null) return m_OriginalSourceTexture;
            return GetActiveTexture();
        }

        /// <summary>
        /// İndirgenmiş görsel yerine orijinal görsele geri döner.
        /// </summary>
        public bool RevertToOriginalTexture()
        {
            if (m_OriginalSourceTexture != null && m_OriginalSourceTexture != m_LevelTexture)
            {
                m_LevelTexture = m_OriginalSourceTexture;
                ExtractPaletteFromTexture();
                GenerateInterleavedWagonSequenceFromPalette();
                return true;
            }
            return false;
        }

        public Vector2Int GetGridResolution()
        {
            if (m_UseNativeResolution)
            {
                Texture2D tex = GetActiveTexture();
                if (tex != null) return new Vector2Int(tex.width, tex.height);
            }
            return m_CustomResolution;
        }

        /// <summary>
        /// Görseldeki tüm benzersiz renkleri otomatik tarar ve renk paleti listesine ekler.
        /// Daha önceden yapılmış renk değiştirmeleri korunur.
        /// </summary>
        public void ExtractPaletteFromTexture()
        {
            Texture2D tex = GetActiveTexture();
            if (tex == null) return;

            // Eski hedefleri hatırla (kullanıcı değiştirdiyse kaybolmasın)
            Dictionary<Color, Color> previousOverrides = new Dictionary<Color, Color>();
            foreach (var item in m_ColorPalette)
            {
                if (item.IsOverridden)
                {
                    previousOverrides[item.originalColor] = item.targetColor;
                }
            }

            m_ColorPalette.Clear();

            // Pikselleri tara ve renkleri frekansına göre grupla
            Color[] pixels;
            try
            {
                pixels = tex.GetPixels();
            }
            catch
            {
                Debug.LogWarning($"[PixelLevelData] '{tex.name}' dokusu okunamadı. Read/Write iznini kontrol edin.");
                return;
            }

            List<PaletteColorOverride> foundColors = new List<PaletteColorOverride>();

            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                if (m_SkipTransparent && c.a < 0.1f) continue;

                // Mevcut grupta var mı?
                bool found = false;
                for (int j = 0; j < foundColors.Count; j++)
                {
                    if (PaletteColorOverride.ColorsMatch(foundColors[j].originalColor, c, 0.04f))
                    {
                        foundColors[j].pixelCount++;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    Color targetCol = c;
                    // Eski override eşleşmesi var mı?
                    foreach (var kvp in previousOverrides)
                    {
                        if (PaletteColorOverride.ColorsMatch(kvp.Key, c, 0.04f))
                        {
                            targetCol = kvp.Value;
                            break;
                        }
                    }

                    foundColors.Add(new PaletteColorOverride(c, targetCol, 1));
                }
            }

            // En çok kullanılan renkten en aza doğru sırala
            foundColors.Sort((a, b) => b.pixelCount.CompareTo(a.pixelCount));

            m_ColorPalette.AddRange(foundColors);
        }

        /// <summary>
        /// Orijinal piksel rengine tanımlı palet değişikliğini (recolor), ton kaydırmayı (hue shift) ve filtreyi uygular.
        /// </summary>
        public Color ApplyColorPipeline(Color rawColor)
        {
            Color c = rawColor;

            // 1. Palet Override (Kullanıcı bu rengi değiştirdi mi?)
            for (int i = 0; i < m_ColorPalette.Count; i++)
            {
                if (PaletteColorOverride.ColorsMatch(m_ColorPalette[i].originalColor, rawColor, 0.04f))
                {
                    c = m_ColorPalette[i].targetColor;
                    break;
                }
            }

            // 2. Tint Color (Renk Filtresi)
            if (m_TintColor != Color.white)
            {
                c.r *= m_TintColor.r;
                c.g *= m_TintColor.g;
                c.b *= m_TintColor.b;
            }

            // 3. Hue Shift (Renk Tonu Kaydırma)
            if (!Mathf.Approximately(m_HueShift, 0f))
            {
                Color.RGBToHSV(c, out float h, out float s, out float v);
                h = Mathf.Repeat(h + m_HueShift / 360f, 1f);
                c = Color.HSVToRGB(h, s, v);
                c.a = rawColor.a;
            }

            return c;
        }

        /// <summary>
        /// Palet değişikliklerini ve renk filtrelerini sıfırlar.
        /// </summary>
        public void ResetPalette()
        {
            foreach (var item in m_ColorPalette)
            {
                item.targetColor = item.originalColor;
            }
            m_TintColor = Color.white;
            m_HueShift = 0f;
            m_ColorBrightness = 1.0f;
            m_ColorSaturation = 1.0f;
            m_ColorContrast = 1.0f;
            m_EmissionIntensity = 0.0f;
        }
    }
}
