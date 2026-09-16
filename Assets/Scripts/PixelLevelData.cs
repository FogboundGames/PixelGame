using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelGame
{
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
            AddPart(TruckPart.MechaBody, true, new Color32(88, 96, 108, 255));
            AddPart(TruckPart.Helmet, true, new Color32(252, 190, 28, 255));
            AddPart(TruckPart.HelmetDark, false, new Color32(44, 44, 52, 255));
            AddPart(TruckPart.Lamp, false, new Color32(255, 246, 200, 255));
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
                    bool match = (p == TruckPart.Cabin || p == TruckPart.Cargo || p == TruckPart.MechaBody || p == TruckPart.Helmet);
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
                case TruckPart.MechaBody: return new Color32(88, 96, 108, 255);
                case TruckPart.Helmet: return new Color32(252, 190, 28, 255);
                case TruckPart.HelmetDark: return new Color32(44, 44, 52, 255);
                case TruckPart.Lamp: return new Color32(255, 246, 200, 255);
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

        [Header("🖼️ Kaynak Görsel")]
        [Tooltip("Bölümde küplerle çizilecek piksel resmi")]
        [SerializeField] private Texture2D m_LevelTexture;

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
        [SerializeField] private float m_ColorBrightness = 1.25f;

        [Tooltip("Renk doygunluğu")]
        [Range(0f, 2.5f)]
        [SerializeField] private float m_ColorSaturation = 1.25f;

        [Tooltip("Renk kontrastı")]
        [Range(0.5f, 2f)]
        [SerializeField] private float m_ColorContrast = 1.05f;

        [Tooltip("Işıma yoğunluğu (Arkadan aydınlatma canlılığı)")]
        [Range(0f, 2f)]
        [SerializeField] private float m_EmissionIntensity = 0.35f;

        [Header("🔲 Izgara & Küp Ayarları")]
        [Tooltip("Küpler arası ızgara boşluğu (0.04 = %4 boşluk)")]
        [Range(0f, 0.3f)]
        [SerializeField] private float m_CubeSpacing = 0.04f;

        [Tooltip("Küplerin 3D kabartma derinliği")]
        [Range(0.05f, 2f)]
        [SerializeField] private float m_CubeDepth = 0.4f;

        [Tooltip("Panonun X eksenindeki 3D eğim açısı (derece). Küplerin alt/ön et kalınlığının kameraya görünmesini sağlar.")]
        [Range(-45f, 45f)]
        [SerializeField] private float m_BoardTiltAngle = 18f;

        [Tooltip("Mavi çerçevenin iç kenar payı")]
        [Range(0f, 0.3f)]
        [SerializeField] private float m_InnerPadding = 0.08f;

        [Tooltip("Şeffaf (alpha < 0.1) pikseller için küp oluşturulmasın mı?")]
        [SerializeField] private bool m_SkipTransparent = true;

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
        public Texture2D LevelTexture { get => m_LevelTexture; set => m_LevelTexture = value; }
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
        public float CubeDepth { get => m_CubeDepth; set => m_CubeDepth = value; }
        public float BoardTiltAngle { get => m_BoardTiltAngle; set => m_BoardTiltAngle = value; }
        public float InnerPadding { get => m_InnerPadding; set => m_InnerPadding = value; }
        public bool SkipTransparent { get => m_SkipTransparent; set => m_SkipTransparent = value; }

        public Sprite SlotSprite { get => m_SlotSprite; set => m_SlotSprite = value; }
        public int SlotCount { get => m_SlotCount; set => m_SlotCount = Mathf.Max(1, value); }
        public int PoolColumns { get => m_PoolColumns; set => m_PoolColumns = Mathf.Max(1, value); }
        public int PoolRows { get => m_PoolRows; set => m_PoolRows = Mathf.Max(1, value); }
        public int TruckCapacity { get => m_TruckCapacity; set => m_TruckCapacity = Mathf.Max(1, value); }

        /// <summary>Havuzda aynı anda görünen kamyon sayısı.</summary>
        public int PoolPlaceCount => m_PoolColumns * m_PoolRows;

        /// <summary>
        /// Bu bölümü bitirmek için gereken toplam kamyon sayısı.
        /// Her renk için o renkteki küpleri taşıyacak kadar kamyon çıkar.
        /// </summary>
        public int GetRequiredTruckCount()
        {
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
            m_ColorBrightness = 1.25f;
            m_ColorSaturation = 1.25f;
            m_ColorContrast = 1.05f;
            m_EmissionIntensity = 0.35f;
        }
    }
}
