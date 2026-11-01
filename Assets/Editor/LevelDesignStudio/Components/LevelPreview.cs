using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PixelGame.Editor.Studio
{
    /// <summary>
    /// Level resmi, oyundaki renklerle. Seçili geminin renkleri parlak kalır, öteki küpler soluklaşır;
    /// böylece gemi sırası tasarlarken o geminin hangi küpleri alacağı görünür.
    /// </summary>
    public sealed class LevelPreview : VisualElement
    {
        private const float DimAmount = 0.82f;
        private static readonly Color DimTarget = new Color(0.15f, 0.16f, 0.18f);

        private readonly Image m_Image = new Image { scaleMode = ScaleMode.ScaleToFit };
        private readonly Label m_Caption = new Label();
        private Texture2D m_Texture;

        public LevelPreview()
        {
            AddToClassList("level-preview");
            m_Image.AddToClassList("level-preview__image");
            Add(m_Image);
            m_Caption.AddToClassList("level-preview__caption");
            Add(m_Caption);
            RegisterCallback<DetachFromPanelEvent>(_ => ReleaseTexture());
        }

        private void ReleaseTexture()
        {
            if (m_Texture != null) Object.DestroyImmediate(m_Texture);
            m_Texture = null;
        }

        /// <param name="highlight">Vurgulanacak palet renkleri; boşsa bütün resim normal görünür.</param>
        public void Show(LevelSnapshot s, IList<PaletteColorOverride> palette, HashSet<int> highlight)
        {
            if (s == null || s.width == 0 || s.cells.Length == 0)
            {
                ReleaseTexture();
                m_Image.image = null;
                m_Caption.text = "Resim okunamadı";
                return;
            }

            if (m_Texture == null || m_Texture.width != s.width || m_Texture.height != s.height)
            {
                ReleaseTexture();
                m_Texture = new Texture2D(s.width, s.height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
                };
            }

            bool focused = highlight != null && highlight.Count > 0;
            int cubes = 0;
            var pixels = new Color32[s.cells.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                int c = s.cells[i];
                if (c < 0 || c >= palette.Count || palette[c] == null) { pixels[i] = new Color32(0, 0, 0, 0); continue; }
                Color color = palette[c].targetColor;
                bool lit = !focused || highlight.Contains(c);
                if (lit) cubes++;
                else color = Color.Lerp(color, DimTarget, DimAmount);
                color.a = 1f;
                pixels[i] = color;
            }
            m_Texture.SetPixels32(pixels);
            m_Texture.Apply(false);
            m_Image.image = m_Texture;
            m_Image.MarkDirtyRepaint();
            m_Caption.text = focused ? $"Vurgulu renklerde {cubes} küp" : $"{s.width}×{s.height} · {cubes} küp";
        }
    }
}
