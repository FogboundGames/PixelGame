using System.Collections.Generic;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Sıradaki kamyonların beklediği havuz.
    /// Oyuncu buradan bir kamyon seçip boş bir slota gönderir; boşalan yere kuyruktan yenisi gelir.
    /// Havuzdaki yerler de slotlar gibi UI görselidir, kamyonlar onların çocuğudur.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Truck Pool")]
    public class TruckPool : MonoBehaviour
    {
        [Header("🅿️ Havuz Yerleri")]
        [SerializeField] private List<TruckSlot> m_Places = new List<TruckSlot>();
        [SerializeField] private int m_Columns = 2;
        [SerializeField] private int m_Rows = 2;

        [Header("🎨 Görünüm")]
        [Tooltip("Bekleme yerlerinin boyutu, aralığı ve görünümü. " +
                 "Kaç tane ve kaç sıra olacağı bölüm verisinden gelir.")]
        [SerializeField] private TruckPlaceStyle m_Style = new TruckPlaceStyle();

        [Header("🌑 Havuz Slot Gölgeleri (Pool Fake Shadows)")]
        [Tooltip("Havuzdaki her bekleme karosunun/slotunun altına yumuşak 3B temas gölgesi ekler.")]
        [SerializeField] private bool m_EnableShadows = true;

        [Tooltip("Havuz slot gölgesi görseli (Assets/UI/PoolSlot_Shadow.png).")]
        [SerializeField] private Sprite m_ShadowSprite;

        [Tooltip("Gölge rengi ve opaklığı.")]
        [SerializeField] private Color m_ShadowColor = new Color(0.015f, 0.025f, 0.06f, 0.55f);

        [Tooltip("Gölgenin karoya göre X ve Y ofseti.")]
        [SerializeField] private Vector2 m_ShadowOffset = new Vector2(0f, -20f);

        [Tooltip("Gölgenin boyut çarpanı.")]
        [SerializeField] private Vector2 m_ShadowScale = new Vector2(0.82f, 0.58f);

        [Tooltip("Gölgenin derinliği (Z).")]
        [SerializeField] private float m_ShadowZ = 2f;

        [Header("👁️ Editör Önizlemesi (Edit Mode Preview)")]
        [Tooltip("Oyun başlatılmadan da havuz karolarını (2. görseldeki tombul 3B sarı rozetler) Edit Mode'da sahnede gösterir ve anlık düzenlemenizi sağlar.")]
        [SerializeField] private bool m_PreviewInEditor = true;

        [Tooltip("Önizleme kapasite sayısı (Varsayılan: 16)")]
        [SerializeField] private int m_PreviewCapacity = 16;

        [Tooltip("Önizleme karo rengi (Varsayılan: Parlak Altın Sarısı)")]
        [SerializeField] private Color m_PreviewColor = new Color(1f, 0.85f, 0.24f, 1f);

        public List<TruckSlot> Places => m_Places;
        public TruckPlaceStyle Style => m_Style;
        public int Columns => m_Columns;
        public int Rows => m_Rows;
        public bool EnableShadows { get => m_EnableShadows; set { m_EnableShadows = value; UpdateShadows(); } }
        public Color ShadowColor { get => m_ShadowColor; set { m_ShadowColor = value; UpdateShadows(); } }
        public Vector2 ShadowOffset { get => m_ShadowOffset; set { m_ShadowOffset = value; UpdateShadows(); } }
        public Vector2 ShadowScale { get => m_ShadowScale; set { m_ShadowScale = value; UpdateShadows(); } }
        public float ShadowZ { get => m_ShadowZ; set { m_ShadowZ = value; UpdateShadows(); } }
        public bool PreviewInEditor { get => m_PreviewInEditor; set { m_PreviewInEditor = value; RefreshEditorPreview(); } }
        public int PreviewCapacity { get => m_PreviewCapacity; set { m_PreviewCapacity = value; RefreshEditorPreview(); } }
        public Color PreviewColor { get => m_PreviewColor; set { m_PreviewColor = value; RefreshEditorPreview(); } }

        /// <summary>
        /// Havuzu verilen sütun/sıra sayısına göre yeniden kurar.
        /// Bölüm verisi değiştiğinde çağrılır.
        /// </summary>
        public void RebuildPlaces(int columns, int rows)
        {
            m_Columns = Mathf.Max(1, columns);
            m_Rows = Mathf.Max(1, rows);

            RectTransform rect = transform as RectTransform;
            if (rect == null) return;

            // Havuzun rolü sabittir: park yeri çizilmez (yalnızca kamyonlar görünür)
            // ve kamyon seçilebilmesi için tıklanabilir olmalıdır
            m_Style.showSprite = false;
            m_Style.interactive = true;

            m_Places = TruckPlaceBuilder.Build(rect, m_Style, m_Columns, m_Rows, "Place");
            UpdateShadows();

            if (!Application.isPlaying && m_PreviewInEditor)
            {
                RefreshEditorPreview();
            }
        }
        public int PlaceCount => m_Places != null ? m_Places.Count : 0;

        private int GetCurrentPoolColumns()
        {
            if (m_Columns > 0) return m_Columns;

            if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevel != null)
            {
                return Mathf.Max(1, LevelManager.Instance.CurrentLevel.PoolColumns);
            }

            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen != null && gen.ActiveLevelData != null)
            {
                return Mathf.Max(1, gen.ActiveLevelData.PoolColumns);
            }

            return 4;
        }

        /// <summary>
        /// Belirtilen park yerinin en ön sırada (Row 0) olup olmadığını kontrol eder.
        /// En ön sıra haricindeki arka sıra vagonları slota yerleştirilemez.
        /// </summary>
        public bool IsFrontRowPlace(TruckSlot place)
        {
            if (m_Places == null || place == null) return false;
            int index = m_Places.IndexOf(place);
            if (index < 0) return false;

            int cols = GetCurrentPoolColumns();
            int rowIndex = index / cols;
            return rowIndex == 0;
        }

        /// <summary>
        /// Arka sıradaki vagonları hafifçe gölgelendirip karartarak en ön sıradaki vagonları belirginleştirir.
        /// </summary>
        public void UpdateRowVisuals()
        {
            if (m_Places == null) return;
            int cols = GetCurrentPoolColumns();

            for (int i = 0; i < m_Places.Count; i++)
            {
                TruckSlot place = m_Places[i];
                if (place == null || place.IsEmpty || place.Truck == null) continue;

                int rowIndex = i / cols;
                bool isFront = (rowIndex == 0);

                Renderer[] renderers = place.Truck.GetComponentsInChildren<Renderer>(true);
                Color tint = isFront ? Color.white : new Color(0.90f, 0.90f, 0.95f, 1f);

                foreach (var r in renderers)
                {
                    if (r == null || r.sharedMaterial == null) continue;

                    // MaterialPropertyBlock'a yazılan _BaseColor/_Color, materyalin kendi
                    // rengiyle ÇARPILMAZ — o shader girdisini o renderer için TAMAMEN
                    // DEĞİŞTİRİR. "tint" burada doğrudan yazılınca (Color.white dahil)
                    // vagonun gerçek kargo rengi (materyalde duran _BaseColor) tamamen
                    // siliniyor ve her vagon düz beyaz/gri görünüyordu — hangi renk
                    // verilirse verilsin fark etmiyordu, çünkü bu kod onu her seferinde
                    // eziyordu. Doğrusu: materyalin GERÇEK rengini oku, tonlamayı ONUN
                    // üstüne kendi hesapla, sonucu yaz.
                    Color baseColor = r.sharedMaterial.HasProperty("_BaseColor")
                        ? r.sharedMaterial.GetColor("_BaseColor")
                        : (r.sharedMaterial.HasProperty("_Color") ? r.sharedMaterial.GetColor("_Color") : Color.white);

                    Color shaded = new Color(baseColor.r * tint.r, baseColor.g * tint.g, baseColor.b * tint.b, baseColor.a);

                    MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor("_Color", shaded);
                    mpb.SetColor("_BaseColor", shaded);
                    r.SetPropertyBlock(mpb);
                }
            }

            UpdateShadows();
        }

        private void Awake()
        {
            if (Application.isPlaying)
            {
                ClearEditorPreview();
            }
        }

        private void Start()
        {
            UpdateShadows();
        }

        private void OnEnable()
        {
            if (m_Places == null || m_Places.Count == 0)
            {
                m_Places = new List<TruckSlot>(GetComponentsInChildren<TruckSlot>(true));
            }

            UpdateShadows();

            if (!Application.isPlaying && m_PreviewInEditor)
            {
                RefreshEditorPreview();
            }
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                ClearEditorPreview();
            }
        }

        private void OnValidate()
        {
            UpdateShadows();

            if (!Application.isPlaying && m_PreviewInEditor)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.delayCall -= DeferredEditorPreview;
                UnityEditor.EditorApplication.delayCall += DeferredEditorPreview;
#endif
            }
        }

#if UNITY_EDITOR
        private void DeferredEditorPreview()
        {
            if (this == null) return;
            UpdateShadows();
            RefreshEditorPreview();
        }
#endif

        private Sprite ResolveShadowSprite()
        {
            if (m_ShadowSprite != null) return m_ShadowSprite;
#if UNITY_EDITOR
            m_ShadowSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/PoolSlot_Shadow.png");
            if (m_ShadowSprite == null)
                m_ShadowSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/SlotShadow.png");
#endif
            return m_ShadowSprite;
        }

        /// <summary>
        /// Havuzdaki tüm bekleme slotlarının/karolarının gölgelerini günceller.
        /// Artık gölgeler vagon/rozet nesnesinin kendi alt nesnesi (child) olduğu için obje hareket ettiğinde onunla birlikte taşınır.
        /// </summary>
        [ContextMenu("🌑 Havuz Gölgelerini Güncelle (Update Shadows)")]
        public void UpdateShadows()
        {
            // Eski statik havuz gölgelerini temizle (artık gölgeler vagonun doğrudan child objesidir)
            Transform shadowsTrans = transform.Find("Shadows");
            if (shadowsTrans != null)
            {
                if (Application.isPlaying) Destroy(shadowsTrans.gameObject);
                else DestroyImmediate(shadowsTrans.gameObject);
            }

            if (m_Places == null) return;
            for (int i = 0; i < m_Places.Count; i++)
            {
                TruckSlot place = m_Places[i];
                if (place == null || place.Truck == null) continue;

                WagonCapacityBadge badge = place.Truck.GetComponent<WagonCapacityBadge>();
                if (badge != null)
                {
                    badge.ApplyStyle();
                    badge.UpdatePlacement();
                }
            }
        }

        /// <summary>
        /// Oyun başlatılmadan önce sahnede havuz karolarını (2. görseldeki 3B altın sarısı rozetler)
        /// birebir aynı görünümde oluşturur ve anlık düzenleme imkanı sunar.
        /// </summary>
        [ContextMenu("👁️ Editör Önizlemesini Tazele")]
        public void RefreshEditorPreview()
        {
            if (Application.isPlaying) return;

            if (m_Places == null || m_Places.Count == 0)
            {
                m_Places = new List<TruckSlot>(GetComponentsInChildren<TruckSlot>(true));
            }

            if (m_Places.Count == 0)
            {
                RebuildPlaces(m_Columns, m_Rows);
            }

            if (!m_PreviewInEditor)
            {
                ClearEditorPreview();
                return;
            }

            // Aktif bölüm verisi varsa sütun/satır ve kapasite bilgilerini al
            int cap = m_PreviewCapacity;
            Color col = m_PreviewColor;

            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            PixelLevelData lvl = gen != null ? gen.ActiveLevelData : null;
            if (lvl == null)
            {
                LevelManager lm = Object.FindFirstObjectByType<LevelManager>();
                if (lm != null) lvl = lm.CurrentLevel;
            }

            if (lvl != null)
            {
                if (lvl.TruckCapacity > 0) cap = lvl.TruckCapacity;
                if (lvl.ColorPalette != null && lvl.ColorPalette.Count > 0)
                {
                    col = lvl.ColorPalette[0].targetColor;
                }
            }

            for (int i = 0; i < m_Places.Count; i++)
            {
                TruckSlot place = m_Places[i];
                if (place == null) continue;

                Color placeColor = col;
                if (lvl != null && lvl.UseCustomWagonSequence && lvl.WagonSequence != null && i < lvl.WagonSequence.Count)
                {
                    placeColor = lvl.WagonSequence[i].wagonColor;
                    if (lvl.WagonSequence[i].capacity > 0) cap = lvl.WagonSequence[i].capacity;
                }

                Transform currentTruck = place.Truck;
                GameObject tileObj;

                if (currentTruck == null)
                {
                    tileObj = new GameObject($"[PreviewTile_{i}]");
                    tileObj.hideFlags = HideFlags.DontSave;
                    place.AssignTruck(tileObj.transform, placeColor);
                }
                else
                {
                    tileObj = currentTruck.gameObject;
                }

                WagonCapacityBadge badge = tileObj.GetComponent<WagonCapacityBadge>();
                if (badge == null) badge = tileObj.AddComponent<WagonCapacityBadge>();

                badge.SetPoolMode(true);
                badge.SetOverrideColor(placeColor);
                badge.SetCount(cap, false);
                badge.ApplyStyle();
                badge.UpdatePlacement();

                place.AlignTruck();
            }

            UpdateRowVisuals();
        }

        /// <summary>
        /// Editör modunda oluşturulan geçici önizleme karolarını temizler.
        /// </summary>
        [ContextMenu("🧹 Editör Önizlemesini Temizle")]
        public void ClearEditorPreview()
        {
            if (m_Places == null) return;

            for (int i = 0; i < m_Places.Count; i++)
            {
                TruckSlot place = m_Places[i];
                if (place == null) continue;

                Transform truck = place.ReleaseTruck();
                if (truck != null && truck.name.StartsWith("[PreviewTile"))
                {
                    if (Application.isPlaying) Destroy(truck.gameObject);
                    else DestroyImmediate(truck.gameObject);
                }

                if (place.SlotRect != null)
                {
                    for (int c = place.SlotRect.childCount - 1; c >= 0; c--)
                    {
                        Transform child = place.SlotRect.GetChild(c);
                        if (child != null && child.name.StartsWith("[PreviewTile"))
                        {
                            if (Application.isPlaying) Destroy(child.gameObject);
                            else DestroyImmediate(child.gameObject);
                        }
                    }
                }
            }
        }

        /// <summary>Havuzda kamyon bekleyen ilk boş olmayan yeri bulur.</summary>
        public TruckSlot FindFirstOccupied()
        {
            if (m_Places == null) return null;

            foreach (TruckSlot place in m_Places)
            {
                if (place != null && !place.IsEmpty) return place;
            }

            return null;
        }

        /// <summary>Havuzdaki ilk boş yeri bulur.</summary>
        public TruckSlot FindFirstEmpty()
        {
            if (m_Places == null) return null;

            foreach (TruckSlot place in m_Places)
            {
                if (place != null && place.IsEmpty) return place;
            }

            return null;
        }

        /// <summary>Verilen kamyonun havuzdaki yerini bulur.</summary>
        public TruckSlot FindPlaceOf(Transform truck)
        {
            if (m_Places == null || truck == null) return null;

            foreach (TruckSlot place in m_Places)
            {
                if (place != null && place.Truck == truck) return place;
            }

            return null;
        }

        /// <summary>Havuzdaki kamyonları öne kaydırarak boşlukları kapatır.</summary>
        public void Compact()
        {
            if (m_Places == null) return;

            int write = 0;

            for (int read = 0; read < m_Places.Count; read++)
            {
                TruckSlot source = m_Places[read];
                if (source == null || source.IsEmpty) continue;

                if (read != write)
                {
                    TruckSlot target = m_Places[write];
                    if (target != null)
                    {
                        Color color = source.TruckColor;
                        target.AssignTruck(source.ReleaseTruck(), color);
                    }
                }

                write++;
            }
        }
    }
}
