using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Oyunun cartoon (toon) görünümünü veren shader'a tek erişim noktası.
    ///
    /// Shader, Toony Colors Pro 2'nin Shader Generator 2 aracıyla URP template'inden
    /// üretilmiştir. Adı burada tek yerde tutulur; shader yeniden üretilip adı değişirse
    /// yalnızca bu sabiti güncellemek yeterlidir.
    ///
    /// Uyumlu property'ler: _BaseColor (küp renkleri), _BaseMap (kamyon palet dokusu),
    /// _HColor / _SColor (toon aydınlık ve gölge tonları).
    /// </summary>
    public static class CartoonShader
    {
        /// <summary>Üretilen cartoon shader'ın tam adı.</summary>
        public const string ShaderName = "Toony Colors Pro 2/PixelGame/Cartoon";

        private static Shader s_Cached;
        private static bool s_Warned;

        /// <summary>
        /// Cartoon shader'ı döndürür. Bulunamazsa URP Lit'e düşer, böylece
        /// shader silinse veya yeniden adlandırılsa da oyun çalışmaya devam eder.
        /// </summary>
        public static Shader Get()
        {
            if (s_Cached != null) return s_Cached;

            s_Cached = Shader.Find(ShaderName);
            if (s_Cached != null) return s_Cached;

            if (!s_Warned)
            {
                s_Warned = true;
                Debug.LogWarning(
                    $"[CartoonShader] '{ShaderName}' bulunamadı; URP Lit kullanılacak. " +
                    "Shader'ı Tools > Toony Colors Pro > Shader Generator 2 ile yeniden üretirsen " +
                    "adının bu sabitle aynı olduğundan emin ol.");
            }

            s_Cached = Shader.Find("Universal Render Pipeline/Lit");
            if (s_Cached == null) s_Cached = Shader.Find("Standard");

            return s_Cached;
        }

        /// <summary>Verilen renkte yeni bir cartoon materyali üretir.</summary>
        public static Material CreateMaterial(Color color, string name)
        {
            Material material = new Material(Get()) { name = name };
            ApplyColor(material, color);
            return material;
        }

        /// <summary>
        /// Materyalin rengini ayarlar. Toon gölgeleme aydınlık ve gölge tonlarını
        /// ayrı okuduğu için ana rengin yanında onları da renge göre türetiyoruz;
        /// aksi halde bütün nesneler aynı gri gölgeyle çıkıyor.
        /// </summary>
        public static void ApplyColor(Material material, Color color)
        {
            if (material == null) return;

            material.color = color;

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);

            // Aydınlık ton: rengin biraz açığı, gölge tonu: biraz koyusu ve soğuğu
            if (material.HasProperty("_HColor"))
            {
                material.SetColor("_HColor", Color.Lerp(color, Color.white, 0.18f));
            }

            if (material.HasProperty("_SColor"))
            {
                Color shade = Color.Lerp(color, new Color(0.15f, 0.18f, 0.32f), 0.42f);
                material.SetColor("_SColor", shade);
            }
        }
    }
}
