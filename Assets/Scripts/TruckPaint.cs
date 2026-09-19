using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Oyuncak kamyonun renklendirilebilir parça grupları.
    /// </summary>
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

    /// <summary>
    /// Oyuncak kamyonun her parçasını ayrı ayrı renklendirir.
    /// Model tek bir materyal ("Mat_Truck") kullanır ve renklerini 4x4'lük bir palet dokusundan okur;
    /// her parça grubu paletteki kendi hücresine bakar. Bir rengi değiştirmek o hücreyi değiştirmek demektir.
    /// Aynı renk şemasına sahip kamyonlar aynı materyali paylaşır, böylece SRP Batcher bozulmaz.
    ///
    /// Kullanım:
    ///   truck.SetBodyColor(TruckPaint.Blue);                // kabin + kasa
    ///   truck.SetColor(TruckPart.Rims, TruckPaint.Yellow);  // tek parça
    /// </summary>
    [DisallowMultipleComponent]
    public class TruckPaint : MonoBehaviour
    {
        public static readonly Color Red = new Color32(230, 40, 40, 255);
        public static readonly Color Blue = new Color32(40, 110, 235, 255);
        public static readonly Color Yellow = new Color32(255, 196, 30, 255);
        public static readonly Color Green = new Color32(60, 190, 80, 255);
        public static readonly Color Purple = new Color32(150, 70, 220, 255);
        public static readonly Color Orange = new Color32(255, 128, 30, 255);
        public static readonly Color White = new Color32(245, 245, 245, 255);
        public static readonly Color Black = new Color32(30, 30, 34, 255);

        [Tooltip("Vagon ve ray materyali cartoon shader ile üretilsin mi? " +
                 "Palet dokusu _BaseMap üzerinden okunduğu için shader değişimi renk şemasını bozmaz.")]
        [SerializeField] private bool m_UseCartoonShader = true;

        private const string k_MaterialPrefix = "Mat_Truck";
        private const int k_PaletteSize = 4;

        // Her parçanın 4x4 paletteki hücresi (sütun, satır). Blender çıktısıyla birebir aynı olmalı.
        private static readonly Vector2Int[] s_Cells =
        {
            new Vector2Int(0, 0), // Tires
            new Vector2Int(1, 0), // Rims
            new Vector2Int(2, 0), // Glass
            new Vector2Int(3, 0), // Headlights
            new Vector2Int(0, 1), // Taillights
            new Vector2Int(1, 1), // Chassis
            new Vector2Int(2, 1), // Cabin (kabin + kaput)
            new Vector2Int(3, 1), // Cargo (kasa + arka kapak)
            new Vector2Int(0, 2), // Stone (maden girişi taşları)
            new Vector2Int(1, 2), // Wood (ahşap tahkimat + ray traversleri)
            new Vector2Int(2, 2), // Dark (maden girişinin karanlık içi)
            new Vector2Int(3, 2), // StoneDark (taşların koyu tonu)
            new Vector2Int(0, 3), // MechaBody (madenci karakterin gövdesi)
            new Vector2Int(1, 3), // Helmet (baret kubbesi + siperlik)
            new Vector2Int(2, 3), // HelmetDark (baret farının gövdesi)
            new Vector2Int(3, 3), // Lamp (baret farının merceği)
        };

        private static readonly Dictionary<string, Material> s_MaterialsByScheme = new Dictionary<string, Material>();
        private static readonly int s_BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int s_MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int s_BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int s_ColorId = Shader.PropertyToID("_Color");

        [Header("Gövde")]
        [SerializeField] private Color m_Cabin = new Color32(230, 40, 40, 255);
        [SerializeField] private Color m_Cargo = new Color32(230, 40, 40, 255);

        [Header("Tekerlekler")]
        [SerializeField] private Color m_Tires = new Color32(50, 50, 55, 255);
        [SerializeField] private Color m_Rims = new Color32(195, 197, 202, 255);

        [Header("Detaylar")]
        [SerializeField] private Color m_Glass = new Color32(90, 110, 130, 255);
        [SerializeField] private Color m_Headlights = new Color32(252, 250, 238, 255);
        [SerializeField] private Color m_Taillights = new Color32(135, 24, 30, 255);
        [SerializeField] private Color m_Chassis = new Color32(60, 60, 66, 255);

        [Header("Maden Girişi / Ray")]
        [SerializeField] private Color m_Stone = new Color32(128, 132, 140, 255);
        [SerializeField] private Color m_StoneDark = new Color32(96, 99, 108, 255);
        [SerializeField] private Color m_Wood = new Color32(120, 78, 48, 255);
        [SerializeField] private Color m_Dark = new Color32(18, 16, 22, 255);

        [Header("Madenci Karakter")]
        [SerializeField] private Color m_MechaBody = new Color32(88, 96, 108, 255);
        [SerializeField] private Color m_Helmet = new Color32(252, 190, 28, 255);
        [SerializeField] private Color m_HelmetDark = new Color32(44, 44, 52, 255);
        [SerializeField] private Color m_Lamp = new Color32(255, 246, 200, 255);

        private readonly List<Renderer> m_Renderers = new List<Renderer>();
        private Material m_Template;
        private bool m_Dirty;

        private void Awake()
        {
            CollectRenderers();
            Apply();
        }

        private void LateUpdate()
        {
            // Aynı karede yapılan birden fazla değişiklik tek seferde uygulanır.
            if (m_Dirty)
            {
                Apply();
            }
        }

        public Color GetColor(TruckPart part)
        {
            switch (part)
            {
                case TruckPart.Tires: return m_Tires;
                case TruckPart.Rims: return m_Rims;
                case TruckPart.Glass: return m_Glass;
                case TruckPart.Headlights: return m_Headlights;
                case TruckPart.Taillights: return m_Taillights;
                case TruckPart.Chassis: return m_Chassis;
                case TruckPart.Cabin: return m_Cabin;
                case TruckPart.Stone: return m_Stone;
                case TruckPart.Wood: return m_Wood;
                case TruckPart.Dark: return m_Dark;
                case TruckPart.StoneDark: return m_StoneDark;
                case TruckPart.MechaBody: return m_MechaBody;
                case TruckPart.Helmet: return m_Helmet;
                case TruckPart.HelmetDark: return m_HelmetDark;
                case TruckPart.Lamp: return m_Lamp;
                default: return m_Cargo;
            }
        }

        public void SetColor(TruckPart part, Color color)
        {
            switch (part)
            {
                case TruckPart.Tires: m_Tires = color; break;
                case TruckPart.Rims: m_Rims = color; break;
                case TruckPart.Glass: m_Glass = color; break;
                case TruckPart.Headlights: m_Headlights = color; break;
                case TruckPart.Taillights: m_Taillights = color; break;
                case TruckPart.Chassis: m_Chassis = color; break;
                case TruckPart.Cabin: m_Cabin = color; break;
                case TruckPart.Cargo: m_Cargo = color; break;
                case TruckPart.Stone: m_Stone = color; break;
                case TruckPart.Wood: m_Wood = color; break;
                case TruckPart.Dark: m_Dark = color; break;
                case TruckPart.StoneDark: m_StoneDark = color; break;
                case TruckPart.MechaBody: m_MechaBody = color; break;
                case TruckPart.Helmet: m_Helmet = color; break;
                case TruckPart.HelmetDark: m_HelmetDark = color; break;
                case TruckPart.Lamp: m_Lamp = color; break;
            }
            m_Dirty = true;
        }

        /// <summary>Kabin ve kasayı (ve varsa madenci gövdesini) vagon rengine boyar; baret ve kazma sabit renk kalır.</summary>
        public void SetBodyColor(Color color)
        {
            m_Cabin = color;
            m_Cargo = color;
            m_MechaBody = color;
            m_Helmet = new Color32(255, 200, 0, 255);
            m_HelmetDark = new Color32(40, 44, 52, 255);
            m_Lamp = new Color32(255, 248, 200, 255);
            m_Rims = new Color32(195, 197, 202, 255);
            m_Wood = new Color32(120, 78, 48, 255);
            m_Dirty = true;
        }

        /// <summary>Madenci karakterin gövdesini ve baretini boyar.</summary>
        public void SetMinerColors(Color bodyColor, Color helmetColor)
        {
            m_MechaBody = bodyColor;
            m_Helmet = helmetColor;
            m_Dirty = true;
        }

        public void SetScheme(Color cabin, Color cargo, Color tires, Color rims,
                              Color glass, Color headlights, Color taillights, Color chassis)
        {
            m_Cabin = cabin;
            m_Cargo = cargo;
            m_Tires = tires;
            m_Rims = rims;
            m_Glass = glass;
            m_Headlights = headlights;
            m_Taillights = taillights;
            m_Chassis = chassis;
            m_Dirty = true;
        }

        /// <summary>
        /// Seviyenin renk temasını o anki vagon/blok rengiyle harmanlayarak uygular.
        /// Dinamik parçalar (matchBlockColor == true) blockColor'ı alır, diğer parçalar özel rengini korur.
        /// </summary>
        public void ApplyTheme(LevelColorTheme theme, Color blockColor)
        {
            if (theme == null)
            {
                SetBodyColor(blockColor);
                Apply();
                return;
            }

            Array allParts = Enum.GetValues(typeof(TruckPart));
            foreach (TruckPart part in allParts)
            {
                Color resolved = theme.ResolveColor(part, blockColor);
                SetColor(part, resolved);
            }

            // Baret, baret lambası, kazma başı (çelik) ve kazma sapı (ahşap) sabit ve belirgin madenci renklerindedir
            m_Helmet = new Color32(255, 200, 0, 255);       // Parlak Güvenlik Sarısı
            m_HelmetDark = new Color32(40, 44, 52, 255);    // Koyu Gri Siperlik
            m_Lamp = new Color32(255, 248, 200, 255);       // Açık Krem Headlamp
            m_Rims = new Color32(195, 197, 202, 255);       // Kazma Metal Ucu (Çelik)
            m_Wood = new Color32(120, 78, 48, 255);        // Kazma Sapı (Ahşap)

            Apply();
        }

        /// <summary>Renkleri hemen uygular (normalde kare sonunda otomatik uygulanır).</summary>
        public void Apply()
        {
            m_Dirty = false;
            if (m_Template == null)
            {
                CollectRenderers();
            }
            if (m_Template == null && m_Renderers.Count == 0)
            {
                return;
            }

            // Yalnızca klasik MineCart modeli UV 4x4 renk palet dokusunu kullanır;
            // Diğer tüm modeller (vakum topu object_005, şişe vb.) Toony Colors Pro 2
            // karikatür plastik materyalleri (direct cartoon color) ile boyanır.
            bool isMineCart = false;
            string rootName = transform.name;
            if (rootName.IndexOf("MineCart", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                isMineCart = true;
            }
            else
            {
                foreach (Renderer target in m_Renderers)
                {
                    if (target != null && target.name.IndexOf("MineCart", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isMineCart = true;
                        break;
                    }
                }
            }

            bool isDirectColorModel = !isMineCart;

            if (isDirectColorModel)
            {
                ApplyDirectColors();
                return;
            }

            Material material = GetOrCreateMaterial();
            foreach (Renderer target in m_Renderers)
            {
                if (target != null)
                {
                    target.sharedMaterial = material;
                }
            }
        }

        /// <summary>
        /// UV paleti kullanmayan modeller (şişe gibi) için materyal rengini doğrudan uygular.
        ///
        /// İki şeye dikkat:
        /// 1) Küplerle aynı toon shader kullanılır. Eskiden burada URP/Lit sabitti ve
        ///    şişeler, üzerinde uğraşılan karikatür görünümün (ramp, gölge tonu, specular,
        ///    matcap) tamamen dışında, düz PBR olarak kalıyordu.
        /// 2) Materyaller renge göre önbelleğe alınır. Eskiden her çağrıda yeni Material
        ///    üretiliyordu; Apply() vagon başına en az üç kez çağrıldığı için (AssignTruck,
        ///    ResetCargo, ApplyTheme) her doğumda renderer başına 6 materyal sızıyor ve
        ///    batch'lenme imkânsız hâle geliyordu.
        /// </summary>
        private void ApplyDirectColors()
        {
            Color bodyColor = m_Cabin;
            Color innerColor = new Color(0.95f, 0.90f, 0.75f, 1f);

            Material body = GetOrCreateDirectMaterial(bodyColor, 0.45f);
            Material inner = GetOrCreateDirectMaterial(innerColor, 0.30f);

            foreach (Renderer target in m_Renderers)
            {
                if (target == null) continue;

                Material[] mats = target.sharedMaterials;
                if (mats == null || mats.Length == 0) mats = new Material[1];

                mats[0] = body;
                if (mats.Length > 1) mats[1] = inner;

                target.sharedMaterials = mats;
            }
        }

        /// <summary>Renge göre önbelleğe alınmış düz renk materyali döndürür.</summary>
        private Material GetOrCreateDirectMaterial(Color color, float smoothness)
        {
            string key = $"direct_{ColorUtility.ToHtmlStringRGBA(color)}_{smoothness:F2}_{(m_UseCartoonShader ? "toon" : "lit")}";

            if (s_MaterialsByScheme.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            Shader shader = null;
            if (m_UseCartoonShader) shader = CartoonShader.Get();
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material material = new Material(shader) { name = $"Mat_Direct_{ColorUtility.ToHtmlStringRGB(color)}" };
            if (m_UseCartoonShader)
            {
                CartoonShader.ApplyColor(material, color);
            }
            else
            {
                if (material.HasProperty(s_BaseColorId)) material.SetColor(s_BaseColorId, color);
                if (material.HasProperty(s_ColorId)) material.SetColor(s_ColorId, color);
            }
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);

            s_MaterialsByScheme[key] = material;
            return material;
        }

        private void CollectRenderers()
        {
            m_Renderers.Clear();
            foreach (Renderer target in GetComponentsInChildren<Renderer>(true))
            {
                if (target is MeshRenderer || target is SkinnedMeshRenderer)
                {
                    m_Renderers.Add(target);
                    if (m_Template == null && target.sharedMaterial != null)
                    {
                        m_Template = target.sharedMaterial;
                    }
                }
            }

            if (m_Renderers.Count == 0)
            {
                Debug.LogWarning($"[TruckPaint] '{name}' altında renderer bulunamadı.", this);
            }
        }

        private Material GetOrCreateMaterial()
        {
            int partCount = s_Cells.Length;
            var keyBuilder = new StringBuilder(partCount * 6);
            for (int i = 0; i < partCount; i++)
            {
                keyBuilder.Append(ColorUtility.ToHtmlStringRGB(GetColor((TruckPart)i)));
            }
            string key = keyBuilder.ToString();

            if (s_MaterialsByScheme.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            var palette = new Texture2D(k_PaletteSize, k_PaletteSize, TextureFormat.RGBA32, false, false)
            {
                name = "Truck_Palette_" + key,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[k_PaletteSize * k_PaletteSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }
            for (int i = 0; i < partCount; i++)
            {
                Vector2Int cell = s_Cells[i];
                pixels[cell.y * k_PaletteSize + cell.x] = GetColor((TruckPart)i);
            }
            palette.SetPixels32(pixels);
            palette.Apply(false, true);

            var material = new Material(m_Template) { name = k_MaterialPrefix + "_" + key };

            // Cartoon görünüm: palet dokusu ve renkler aynı property adlarını kullandığı
            // için shader'ı değiştirmek renk şemasını bozmaz
            if (m_UseCartoonShader)
            {
                Shader cartoon = CartoonShader.Get();
                if (cartoon != null)
                {
                    material.shader = cartoon;
                    CartoonShader.ApplyColor(material, Color.white);
                }
            }

            if (material.HasProperty(s_BaseMapId)) material.SetTexture(s_BaseMapId, palette);
            if (material.HasProperty(s_MainTexId)) material.SetTexture(s_MainTexId, palette);
            if (material.HasProperty(s_BaseColorId)) material.SetColor(s_BaseColorId, Color.white);
            if (material.HasProperty(s_ColorId)) material.SetColor(s_ColorId, Color.white);

            s_MaterialsByScheme[key] = material;
            return material;
        }
    }
}
