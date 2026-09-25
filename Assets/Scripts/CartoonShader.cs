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
        /// Materyalin rengini ve Toony Colors Pro 2 hypercasual cel-shading / plastik
        /// cila ayarlarını uygular (PixelCube_Cartoon.mat ile %100 uyumlu).
        /// </summary>
        public static void ApplyColor(Material material, Color color)
        {
            if (material == null) return;

            material.color = color;

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);

            // 1. Toony Colors Pro 2 Cel-Shading (Açık, parlak ve canlı tonlama)
            if (material.HasProperty("_HColor")) material.SetColor("_HColor", Color.white);
            // Yumuşak, açık pastel gölge tonu (asla çamurlu/koyu gri olmaz)
            if (material.HasProperty("_SColor")) material.SetColor("_SColor", new Color(0.84f, 0.82f, 0.88f, 1f));
            if (material.HasProperty("_RampThreshold")) material.SetFloat("_RampThreshold", 0.383f);
            if (material.HasProperty("_RampSmoothing")) material.SetFloat("_RampSmoothing", 0.908f);

            // 2. Parlak 3B Plastik Oyuncak Cilası (Stylized Plastic & Specular Gloss)
            if (material.HasProperty("_StylizedPlasticOn"))
            {
                material.SetFloat("_StylizedPlasticOn", 1f);
                if (material.HasProperty("_PlasticHighlightIntensity")) material.SetFloat("_PlasticHighlightIntensity", 2.85f);
                if (material.HasProperty("_PlasticHighlightSize")) material.SetFloat("_PlasticHighlightSize", 0.26f);
                if (material.HasProperty("_PlasticHighlightColor")) material.SetColor("_PlasticHighlightColor", Color.white);
                if (material.HasProperty("_PlasticTopLight")) material.SetFloat("_PlasticTopLight", 0.25f);
                if (material.HasProperty("_PlasticBevelAO")) material.SetFloat("_PlasticBevelAO", 0.45f);
                // 0.05 sol/üst kenarda gerçek ışıktan bağımsız, istenmeyen ikinci bir "sahte" highlight
                // çiziyordu. 0 bunu tamamen kaldırıyor; bedeli satırlar arası ayraç çizgisinin de gitmesi
                // (üst-alt komşu küpler daha "yapışık" görünür) — kullanıcı bu ödünü bilerek tercih etti.
                if (material.HasProperty("_ProceduralBevelWidth")) material.SetFloat("_ProceduralBevelWidth", 0f);
                if (material.HasProperty("_ProceduralBevelIntensity")) material.SetFloat("_ProceduralBevelIntensity", 0.80f);
                if (material.HasProperty("_PillowRoundness")) material.SetFloat("_PillowRoundness", 0.50f);
                if (material.HasProperty("_PlasticAngleX")) material.SetFloat("_PlasticAngleX", -0.45f);
            }

            // 3. PBR Speküler Parlama ve Pürüzsüzlük
            if (material.HasProperty("_SpecularColor")) material.SetColor("_SpecularColor", Color.white);
            if (material.HasProperty("_SpecularRoughnessPBR")) material.SetFloat("_SpecularRoughnessPBR", 0.18f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.85f);

            // 4. Kenar Işığı (Rim Lighting)
            if (material.HasProperty("_RimColor")) material.SetColor("_RimColor", new Color(0.18f, 0.18f, 0.18f, 0.5f));
            if (material.HasProperty("_RimMin")) material.SetFloat("_RimMin", 0.55f);
            if (material.HasProperty("_RimMax")) material.SetFloat("_RimMax", 0.78f);
        }
    }
}
