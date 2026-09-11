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
        Cargo
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
            }
            m_Dirty = true;
        }

        /// <summary>Kabin ve kasayı aynı renge boyar.</summary>
        public void SetBodyColor(Color color)
        {
            m_Cabin = color;
            m_Cargo = color;
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

        /// <summary>Renkleri hemen uygular (normalde kare sonunda otomatik uygulanır).</summary>
        public void Apply()
        {
            m_Dirty = false;
            if (m_Template == null)
            {
                CollectRenderers();
            }
            if (m_Template == null)
            {
                return;
            }

            Material material = GetOrCreateMaterial();
            foreach (Renderer target in m_Renderers)
            {
                target.sharedMaterial = material;
            }
        }

        private void CollectRenderers()
        {
            m_Renderers.Clear();
            foreach (Renderer target in GetComponentsInChildren<Renderer>(true))
            {
                Material material = target.sharedMaterial;
                if (material != null && material.name.StartsWith(k_MaterialPrefix, StringComparison.Ordinal))
                {
                    m_Renderers.Add(target);
                    if (m_Template == null)
                    {
                        m_Template = material;
                    }
                }
            }

            if (m_Renderers.Count == 0)
            {
                Debug.LogWarning($"[TruckPaint] '{name}' altında '{k_MaterialPrefix}' materyalini kullanan renderer bulunamadı.", this);
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
            if (material.HasProperty(s_BaseMapId)) material.SetTexture(s_BaseMapId, palette);
            if (material.HasProperty(s_MainTexId)) material.SetTexture(s_MainTexId, palette);
            if (material.HasProperty(s_BaseColorId)) material.SetColor(s_BaseColorId, Color.white);
            if (material.HasProperty(s_ColorId)) material.SetColor(s_ColorId, Color.white);

            s_MaterialsByScheme[key] = material;
            return material;
        }
    }
}
