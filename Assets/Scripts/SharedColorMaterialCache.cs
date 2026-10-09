using System.Collections.Generic;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Aynı renkteki renderer'lar için tek bir paylaşılan materyal örneği verir.
    /// MaterialPropertyBlock kullanan renderer'lar SRP Batcher dışında kalıp her biri ayrı draw call
    /// oluyordu (yüzlerce küp × gövde + bacak). Renk başına materyal ile hepsi SRP Batcher'a girer.
    /// Sadece oyun sırasında kullanılır; editörde sahneye kaydedilmeyen materyal referansı bırakmamak için MPB yolu korunur.
    /// </summary>
    public static class SharedColorMaterialCache
    {
        private struct Key : System.IEquatable<Key>
        {
            public int SourceId;
            public Color32 Color;
            public Color32 Emission;

            public bool Equals(Key other)
            {
                return SourceId == other.SourceId
                    && Color.r == other.Color.r && Color.g == other.Color.g && Color.b == other.Color.b && Color.a == other.Color.a
                    && Emission.r == other.Emission.r && Emission.g == other.Emission.g && Emission.b == other.Emission.b;
            }

            public override bool Equals(object obj) => obj is Key k && Equals(k);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = SourceId;
                    h = h * 31 + (Color.r | (Color.g << 8) | (Color.b << 16) | (Color.a << 24));
                    h = h * 31 + (Emission.r | (Emission.g << 8) | (Emission.b << 16));
                    return h;
                }
            }
        }

        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProp = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");

        private static readonly Dictionary<Key, Material> s_Cache = new Dictionary<Key, Material>(64);
        // Önbellekten verilen materyal → üretildiği kaynak materyal. Renderer'da zaten önbellek materyali
        // varken yeni renk istenirse yine orijinal kaynaktan türetilir (zincirleme kopya oluşmaz).
        private static readonly Dictionary<Material, Material> s_SourceOf = new Dictionary<Material, Material>(64);

        public static Material ResolveSource(Material mat)
        {
            if (mat == null) return null;
            return s_SourceOf.TryGetValue(mat, out Material src) && src != null ? src : mat;
        }

        public static Material Get(Material source, Color color, Color emission)
        {
            source = ResolveSource(source);
            if (source == null) return null;

            var key = new Key { SourceId = source.GetInstanceID(), Color = color, Emission = emission };
            if (s_Cache.TryGetValue(key, out Material mat) && mat != null)
                return mat;

            mat = new Material(source) { name = source.name + "_Shared" };
            if (mat.HasProperty(BaseColorProp)) mat.SetColor(BaseColorProp, color);
            if (mat.HasProperty(ColorProp)) mat.SetColor(ColorProp, color);
            if (mat.HasProperty(EmissionColorProp)) mat.SetColor(EmissionColorProp, emission);

            s_Cache[key] = mat;
            s_SourceOf[mat] = source;
            return mat;
        }

        /// <summary>Renderer'a renk materyalini atar ve eski MPB'yi temizler (SRP Batcher uyumu için şart).</summary>
        public static void Apply(Renderer r, Color color, Color emission)
        {
            if (r == null) return;
            Material mat = Get(r.sharedMaterial, color, emission);
            if (mat == null) return;
            if (r.sharedMaterial != mat) r.sharedMaterial = mat;
            if (r.HasPropertyBlock()) r.SetPropertyBlock(null);
        }
    }
}
