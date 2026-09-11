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

        [Tooltip("Mavi çerçevenin iç kenar payı")]
        [Range(0f, 0.3f)]
        [SerializeField] private float m_InnerPadding = 0.08f;

        [Tooltip("Şeffaf (alpha < 0.1) pikseller için küp oluşturulmasın mı?")]
        [SerializeField] private bool m_SkipTransparent = true;

        [Header("🌑 Gölge Özelleştirme (Opsiyonel)")]
        [Tooltip("Bu bölüme özel kontur gölgesi dokusu (Boş bırakılırsa görselden otomatik üretilir)")]
        [SerializeField] private Texture2D m_FigureShadowTexture;

        // Public Properties
        public string LevelName { get => m_LevelName; set => m_LevelName = value; }
        public int LevelIndex { get => m_LevelIndex; set => m_LevelIndex = value; }
        public Texture2D LevelTexture { get => m_LevelTexture; set => m_LevelTexture = value; }
        public Sprite LevelSprite { get => m_LevelSprite; set => m_LevelSprite = value; }
        public Texture2D FigureShadowTexture { get => m_FigureShadowTexture; set => m_FigureShadowTexture = value; }
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
        public float InnerPadding { get => m_InnerPadding; set => m_InnerPadding = value; }
        public bool SkipTransparent { get => m_SkipTransparent; set => m_SkipTransparent = value; }

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
