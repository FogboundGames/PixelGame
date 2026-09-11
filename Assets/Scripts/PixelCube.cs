using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Piksel resmini oluşturan her bir 3D küpü temsil eder.
    /// Koordinat ve renk bilgilerini saklar, MaterialPropertyBlock ile GPU dostu renklendirme yapar.
    /// Canlı parlaklık, doygunluk (saturation) ve ışıma (emission) ayarlarını destekler.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class PixelCube : MonoBehaviour
    {
        [Header("Izgara Konumu")]
        [SerializeField] private int m_GridX;
        [SerializeField] private int m_GridY;

        [Header("Renk Bilgileri")]
        [SerializeField] private Color m_OriginalColor = Color.white;
        [SerializeField] private Color m_CurrentColor = Color.white;

        private MeshRenderer m_Renderer;
        private static MaterialPropertyBlock s_PropertyBlock;
        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProp = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");

        public int GridX => m_GridX;
        public int GridY => m_GridY;
        public Color OriginalColor => m_OriginalColor;
        public Color CurrentColor => m_CurrentColor;

        public void Initialize(int x, int y, Color originalColor, float emission = 0f)
        {
            m_GridX = x;
            m_GridY = y;
            m_OriginalColor = originalColor;
            m_CurrentColor = originalColor;
            ApplyColor(originalColor, emission);
        }

        public void SetColor(Color newColor, float emission = 0f)
        {
            m_OriginalColor = newColor;
            m_CurrentColor = newColor;
            ApplyColor(newColor, emission);
        }

        public void ApplyColor(Color color, float emission = 0f)
        {
            m_CurrentColor = color;

            if (m_Renderer == null)
                m_Renderer = GetComponent<MeshRenderer>();

            if (m_Renderer == null) return;

            if (s_PropertyBlock == null)
                s_PropertyBlock = new MaterialPropertyBlock();

            m_Renderer.GetPropertyBlock(s_PropertyBlock);
            s_PropertyBlock.SetColor(BaseColorProp, color);
            s_PropertyBlock.SetColor(ColorProp, color);

            if (emission > 0f)
            {
                s_PropertyBlock.SetColor(EmissionColorProp, color * emission);
            }
            else
            {
                s_PropertyBlock.SetColor(EmissionColorProp, Color.black);
            }

            m_Renderer.SetPropertyBlock(s_PropertyBlock);
        }

        public void UpdateColorAdjustments(float brightness, float saturation, float contrast, float emission)
        {
            Color adjusted = AdjustColor(m_OriginalColor, brightness, saturation, contrast);
            ApplyColor(adjusted, emission);
        }

        public static Color AdjustColor(Color col, float brightness, float saturation, float contrast)
        {
            // Renk tonunu koruyarak HSV formatında doygunluk ve parlaklık ayarla
            Color.RGBToHSV(col, out float h, out float s, out float v);

            s = Mathf.Clamp01(s * saturation);
            v = Mathf.Clamp01(v * brightness);

            Color result = Color.HSVToRGB(h, s, v);

            // Kontrast (0.5 orta noktası etrafında)
            if (!Mathf.Approximately(contrast, 1f))
            {
                result.r = Mathf.Clamp01((result.r - 0.5f) * contrast + 0.5f);
                result.g = Mathf.Clamp01((result.g - 0.5f) * contrast + 0.5f);
                result.b = Mathf.Clamp01((result.b - 0.5f) * contrast + 0.5f);
            }

            result.a = col.a;
            return result;
        }
    }
}
