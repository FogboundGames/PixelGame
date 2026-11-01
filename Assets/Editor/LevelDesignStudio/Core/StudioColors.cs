using UnityEngine;

namespace PixelGame.Editor.Studio
{
    /// <summary>Stüdyoda renkleri okunur adla göstermek ve üzerindeki yazı rengini seçmek için.</summary>
    public static class StudioColors
    {
        public static string Name(Color c, int index)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            string name;
            if (v < 0.22f) name = "Siyah";
            else if (s < 0.12f && v > 0.85f) name = "Beyaz";
            else if (s < 0.15f) name = "Gri";
            else
            {
                float deg = h * 360f;
                if (deg < 15f || deg >= 345f) name = "Kırmızı";
                else if (deg < 42f) name = v < 0.65f ? "Kahverengi" : "Turuncu";
                else if (deg < 68f) name = "Sarı";
                else if (deg < 155f) name = "Yeşil";
                else if (deg < 195f) name = "Turkuaz";
                else if (deg < 255f) name = "Mavi";
                else if (deg < 285f) name = "Mor";
                else name = "Pembe";
            }
            return $"{name} {index + 1}";
        }

        public static Color TextOn(Color background) => background.grayscale > 0.55f ? new Color(0.09f, 0.09f, 0.11f) : Color.white;
    }
}
