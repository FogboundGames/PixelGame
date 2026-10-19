using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Belirli bir slot sayısına (ör. 3, 4, 5) özel kıyı, iskele ve slot yerleşim ayarları.
    /// Farklı slot sayılarında sahil koyunun kavisinden veya doku oranından kaynaklanan kaymaları önler.
    /// </summary>
    [System.Serializable]
    public class MarinaSlotCountConfig
    {
        [Tooltip("Slot sayısı (3, 4, 5 vb.).")]
        public int SlotCount = 5;

        [Header("🏖️ Kıyı / Sahil Konumu (Shore Placement)")]
        [Tooltip("Tüm slot şeridinin yatay X eksenindeki ofseti (rıhtım babaları arasına tam ortalama).")]
        public float RowOffsetX = 0.042f;

        [Tooltip("Tüm slot şeridinin Y eksenindeki kıyı yüksekliği (kumsal çizgisine oturma).")]
        public float RowOffsetY = -2.20f;

        [Tooltip("Tüm slot şeridinin Z eksenindeki derinliği.")]
        public float RowOffsetZ = 0.05f;

        [Header("🪵 İskele Görseli (Pier Visual)")]
        [Tooltip("Bu slot sayısına ait iskele materyali.")]
        public Material PierMaterial;

        [Tooltip("İskele genişliği (dünya birimi).")]
        public float PierWidth = 8.55f;

        [Tooltip("İskele dikey Y ofseti.")]
        public float PierOffsetY = 2.06f;

        [Tooltip("İskele derinlik Z ofseti.")]
        public float PierOffsetZ = 0.04f;

        [Tooltip("İskele genel ölçek çarpanı.")]
        public float PierScaleMultiplier = 1.07f;

        [Tooltip("İskeleyi yalnızca yatayda gerer (yükseklik değişmez): >1 yayı genişletip yayvanlaştırır, kollar sahil " +
                 "kenarına doğru açılır. Slotlar da iskeleyle birlikte açılır.")]
        [Range(0.7f, 1.6f)]
        public float PierStretchX = 1f;

        [Tooltip("Doku üzerindeki koy aralığı (piksel).")]
        public float BaySpacingPx = 174f;

        [Header("⚓ Gemi Yanaşma & Kavis (Bay & Arc)")]
        [Tooltip("Gemilerin iskele koylarına yanaşma dikey ofseti (Y).")]
        public float BaySlotOffsetY = 0f;

        [Tooltip("Slotların sahil koyu kıyısına uyumlu yay/kavis gücü (Y).")]
        public float ArcCurveY = 0.042f;

        [Tooltip("Slotların yanaşma açısı.")]
        public float SlotAngle = 0f;

        [Header("📐 Slot Boyutları & Klasik Aralık")]
        [Tooltip("Slotların genişliği (X ekseni).")]
        public float SlotWidth = 1.05f;

        [Tooltip("Slotların boyu / uzunluğu (Z ekseni).")]
        public float SlotLength = 1.55f;

        [Tooltip("Klasik modda slotlar arası mesafe.")]
        public float LegacySpacing = 1.68f;

        public MarinaSlotCountConfig() { }

        public MarinaSlotCountConfig(int count)
        {
            SlotCount = count;
        }

        public MarinaSlotCountConfig Clone()
        {
            return (MarinaSlotCountConfig)MemberwiseClone();
        }
    }

    /// <summary>
    /// Gemi sahnesindeki su slotlarının (WaterSlot_1..5) boyutunu, aralarındaki mesafeyi,
    /// sahil kavisini (arc curve) ve marina yanaşma açısını canlı (live) olarak ayarlamayı sağlar.
    /// Her slot sayısına (3, 4, 5) göre kıyıya tam oturan bağımsız yapılandırma sunar.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Marina Slot Layout")]
    public class MarinaSlotLayout : MonoBehaviour
    {
        [Header("⚙️ Slot Sayısına Özel Kıyı & İskele Ayarları")]
        [Tooltip("Aktif edildiğinde 3, 4, 5 ve diğer slot sayıları için kıyıya özel bağımsız ayarlar kullanılır.")]
        [SerializeField] private bool m_UsePerCountSettings = true;

        [SerializeField] private MarinaSlotCountConfig m_Config3Slots;
        [SerializeField] private MarinaSlotCountConfig m_Config4Slots;
        [SerializeField] private MarinaSlotCountConfig m_Config5Slots;
        [SerializeField] private List<MarinaSlotCountConfig> m_CustomConfigs = new List<MarinaSlotCountConfig>();

        [Header("⚓ Slot Boyutları (Width & Length / Height)")]
        [Tooltip("Slotların yatay genişliği (Width / En - X ekseni).")]
        [Range(0.3f, 3.5f)]
        [SerializeField] private float m_SlotWidth = 1.05f;

        [Tooltip("Slotların boyu / uzunluğu (Length / Height - Z ekseni).")]
        [Range(0.3f, 3.5f)]
        [SerializeField] private float m_SlotLength = 1.55f;

        [SerializeField, HideInInspector] private float m_SlotScale = 1.27f;

        [Header("📏 Slotlar Arası Mesafe (Aralık)")]
        [Tooltip("Slotların birbirine olan yatay mesafesi.")]
        [Range(0.6f, 2.5f)]
        [SerializeField] private float m_SlotSpacing = 1.68f;

        [Header("📱 Ekrana Sığdırma")]
        [Tooltip("Slot sırası ekrandan taşarsa (ör. 5 slot) aralık ve simit boyutu birlikte, ekrana sığacak kadar küçülür. Sığıyorsa hiçbir şey değişmez.")]
        [SerializeField] private bool m_FitToScreenWidth = true;
        [Tooltip("Sıranın ekran kenarlarından bırakacağı boşluk (dünya birimi).")]
        [SerializeField] private float m_ScreenEdgeMargin = 0.15f;
        [Tooltip("Simidin köpüğüyle birlikte görünen genişliği / slot genişliği (ölçülen: ~1.51).")]
        [SerializeField] private float m_BuoyVisualWidthRatio = 1.51f;

        [Header("🪵 Arka Plan Babalarına Hizalama")]
        [Tooltip("Slot aralığını arka plan dokusundaki babalar arası mesafeden (BaySpacingPx) hesaplar; " +
                 "arka plan ekrana gerildiği için her en-boy oranında slotlar babaların tam ortasına oturur.")]
        [SerializeField] private bool m_MatchBackgroundBays = true;
        [Tooltip("Yanaşma görselinin genişliği / babalar arası mesafe. ~0.72 iki babanın arasını değmeden doldurur.")]
        [Range(0.4f, 1f)]
        [SerializeField] private float m_BayFillRatio = 0.82f;
        [Tooltip("Arka plan dokusunda sıranın ortasının X pikseli (3 slotta orta boşluk, 4 slotta orta baba).")]
        [SerializeField] private float m_BayCenterPx = 541f;
        [Tooltip("Arka plan dokusunda yanaşma görselinin merkezinin üstten Y pikseli (iskele tahtasının hemen altı, babaların arası). " +
                 "Büyütünce slotlar aşağı iner.")]
        [SerializeField] private float m_BayCenterYPx = 1353f;
        [Tooltip("Arka plan görselinin orijinal piksel boyutu (yukarıdaki piksel değerleri buna göre). Import sonrası doku boyutu farklı olabilir.")]
        [SerializeField] private Vector2 m_BackgroundSourceSizePx = new Vector2(1080f, 1920f);

        private Vector2 m_LastBackgroundSize;

        [Header("🫧 Slot Görseli (Köpük Halkası)")]
        [Tooltip("Slot görselini (can simidi / köpük) gizler; görsel arka plandaki ahşap iskele ve babalar (posts) kullanılır.")]
        [SerializeField] private bool m_HideSlotIndicators = false;
        public bool HideSlotIndicators { get => m_HideSlotIndicators; set => m_HideSlotIndicators = value; }
        [Tooltip("Slot görselinin slot genişliğine göre ölçeği.")]
        [SerializeField] private float m_SlotVisualScale = 1.30f;
        [Tooltip("Slot görselinin ekranda görünen yükseklik / genişlik oranı. 1'den büyükse dikey elips olur ve gemiyi boyuna sarar.")]
        [SerializeField] private float m_SlotVisualScreenAspect = 1.17f;
        [Tooltip("Slot görselini slot içinde ileri (ekranda yukarı) kaydırır; gemi gövdesi yükseldiği için ekranda halkanın üstüne oturuyordu, bununla ortalanır.")]
        [SerializeField] private float m_SlotVisualOffsetZ = 0.38f;
        [Tooltip("Sıradaki son slotun görseli (ör. iki kolu da olan tam iskele). Boşsa slot görsellerinin materyaline dokunulmaz.")]
        [SerializeField] private Material m_SlotVisualMaterial;
        [Tooltip("Son slot dışındaki slotların görseli (ör. sağ kolu kesilmiş iskele): sağ komşunun sol kolu birleşme yerini kapatır, " +
                 "böylece yan yana slotlar tek parça iskele gibi görünür. Boşsa hepsi m_SlotVisualMaterial kullanır.")]
        [SerializeField] private Material m_SlotVisualMaterialJoined;

        [Header("🟦 Açık Slot Görseli (İç Kesikli Alan)")]
        [Tooltip("Açık slotlarda slot_berth görselinin yalnızca iç kısmı (kesikli çerçeve + yumuşak dolgu) kullanılır; " +
                 "babalar ve tamponlar olmadan. Açıkken Indicator gösterilmez.")]
        [SerializeField] private bool m_UseInnerBerthVisual = true;
        [SerializeField] private Material m_SlotBerthInnerMaterial;
        [Header("🚢 Gemi Yanaşma Su Teması & Gölge Görseli (Docked Hull Shadow)")]
        [Tooltip("Gemi slota yanaştığında gövdenin altına serilen temas gölgesi ve su temas halkası materyali.")]
        [SerializeField] private Material m_SlotBerthDockedMaterial;
        [Tooltip("Açık slot görselinin boy çarpanı (Slot Berth Length × bu). Kilitli slotları etkilemez.")]
        [Range(0.5f, 3f)]
        [SerializeField] private float m_OpenBerthLengthScale = 1.65f;

        [Header("⛔ Kapalı Slot Görseli")]
        [Tooltip("Engel olan kapalı bölmeler taralı çizgili görselle gösterilir.")]
        [SerializeField] private bool m_UseClosedSlotVisual = true;
        [SerializeField] private Material m_SlotBerthClosedMaterial;

        [Header("🟨 Açık Slot Görseli (Indicator)")]
        [Tooltip("Açık slotlarda kesikli mavi kutu yerine slotun altındaki IndicatorMesh (Kenney indicator-square-b) gösterilir.")]
        [SerializeField] private bool m_UseIndicatorMeshVisual = true;
        [Tooltip("Indicator rengi; düşük alfa eski slotlar gibi yumuşak, yarı saydam görünüm verir.")]
        [SerializeField] private Color m_IndicatorColor = new Color(1f, 1f, 1f, 0.5f);
        [Tooltip("Indicator'ın kalınlık (yükseklik) çarpanı. Küçük değer çerçeveyi suya yapışık, ince gösterir.")]
        [Range(0.02f, 1f)]
        [SerializeField] private float m_IndicatorHeightScale = 0.1f;

        [NonSerialized] private Material m_IndicatorRuntimeMaterial;

        [Header("🛥️ Su Üstü Yanaşma Slotu (Floating Berth Slip)")]
        [Tooltip("İskele parmakları arasına yerleştirilen 3B yüzer yanaşma slotu materyali.")]
        [SerializeField] private Material m_SlotBerthMaterial;

        [Tooltip("Yüzer yanaşma slotu XZ düzlem 3B mesh'i.")]
        [SerializeField] private Mesh m_SlotBerthMesh;

        [Tooltip("Yüzer yanaşma slotunun genişliği (tahta babalar arasına tam oturan genişlik).")]
        [SerializeField] private float m_SlotBerthWidth = 0.98f;

        [Tooltip("Yüzer yanaşma slotunun uzunluğu (Z ekseni).")]
        [SerializeField] private float m_SlotBerthLength = 1.20f;

        [Tooltip("Yüzer yanaşma slotunun su yüzeyinden dikey yüksekliği.")]
        [SerializeField] private float m_SlotBerthOffsetY = 0.015f;

        [Tooltip("Yüzer yanaşma slotunun Z eksenindeki konumu (iskele kıyısına oturma).")]
        [SerializeField] private float m_SlotBerthOffsetZ = 0.15f;

        [Header("🔒 Kapalı / Kilitli Slot Görseli (Locked Bay Marker)")]
        [Tooltip("Bölümde kilitli olan bölmelere konulan kilitli slot çerçevesi ve asma kilit (🔒) materyali.")]
        [SerializeField] private Material m_LockedBarrierMaterial;

        [Tooltip("Kapalı slot çerçevesinin genişliği.")]
        [SerializeField] private float m_LockedBarrierWidth = 0.98f;

        [Tooltip("Kapalı slot çerçevesinin yüksekliği / boyu.")]
        [SerializeField] private float m_LockedBarrierHeight = 0.98f;

        [Tooltip("Kapalı slot çerçevesinin dikey Y ofseti.")]
        [SerializeField] private float m_LockedBarrierOffsetY = 0.0f;

        [Tooltip("Kapalı slot çerçevesinin derinlik Z ofseti.")]
        [SerializeField] private float m_LockedBarrierOffsetZ = 0.02f;

        [Header("🪵 Kavisli İskele (Curved Marina Pier Visual)")]
        [Tooltip("Kavisli ahşap iskele modelini (eski dairesel koy için) aktif eder. Yeni düz iskeleli arka planda false olmalıdır.")]
        [SerializeField] private bool m_EnableCurvedPier = false;
        [SerializeField] private Texture m_Background3Slots;
        [SerializeField] private Texture m_Background4Slots;
        [Tooltip("Beş bölmenin de açık (taralı olmayan) çizildiği arka plan. Kapalı bölmeleri slot sayısına göre kod taralı görselle kapatır.")]
        [SerializeField] private Texture m_BackgroundAllBaysOpen;
        [SerializeField] private Material m_PierMaterial4Slots;
        [SerializeField] private Material m_PierMaterial5Slots;
        [SerializeField] private Material m_PierMaterial3Slots;
        [SerializeField] private float m_PierWidth4Slots = 8.1f;
        [SerializeField] private float m_PierWidth5Slots = 8.55f;
        [SerializeField] private float m_PierWidth3Slots = 7.2f;
        [SerializeField] private float m_PierOffsetY = 2.06f;
        [SerializeField] private float m_PierOffsetZ = 0.04f;
        [SerializeField] private float m_PierScaleMultiplier = 1.07f;
        [SerializeField] private float m_PierRotationX = -40f;
        [SerializeField] private float m_BaySlotOffsetY = 0f;

        [Header("📐 Yanaşma Açısı")]
        [Tooltip("Slotların ve gemilerin yanaşma açısı (Referans: 0 derece, düz yatay).")]
        [Range(-60f, 60f)]
        [SerializeField] private float m_SlotAngle = 0f;

        [Header("🌊 Su Düzlemi Eğim Açısı")]
        [Tooltip("Kamera perspektifine göre su yüzeyi eğim açısı (varsayılan: -28 derece, pikselart küpleriyle uyumlu 3B izometrik derinlik).")]
        [Range(-90f, 0f)]
        [SerializeField] private float m_WaterTiltX = -28f;

        [Header("📍 Dikey Yükseklik & Derinlik")]
        [Tooltip("Slot şeridinin Y eksenindeki yüksekliği (Kumsal kıyısına yakınlık: -5.40f).")]
        [Range(-8f, 5f)]
        [SerializeField] private float m_OffsetY = -5.40f;

        [Tooltip("Slot şeridinin Z eksenindeki derinliği.")]
        [Range(-3f, 3f)]
        [SerializeField] private float m_OffsetZ = 0.05f;

        [Header("🌊 Sahil Kavis / Yay Eğrisi (Shoreline Arc)")]
        [Tooltip("Slotların sahil koyu kıyısına uyumlu yay/kavis yapması için Y ekseni eğrilik gücü (0 = Düz sıra).")]
        [Range(-0.3f, 0.3f)]
        [SerializeField] private float m_ArcCurveY = 0f;

        [Tooltip("Kavisin sol/sağ asimetrisi.")]
        [Range(-0.2f, 0.2f)]
        [SerializeField] private float m_ArcAsymmetry = 0f;

        [Tooltip("Slotların kavis yönüne göre yelpaze açısı.")]
        [Range(-10f, 10f)]
        [SerializeField] private float m_ArcAngleFan = 0f;

        [Header("⚓ Aktif Slot Sayısı")]
        [Tooltip("Sahnedeki aktif yanaşma slotu sayısı (1 - 8). Seviye verisindeki SlotCount ile otomatik senkronize olur.")]
        [Range(1, 8)]
        [SerializeField] private int m_SlotCount = 5;

        public bool UsePerCountSettings
        {
            get => m_UsePerCountSettings;
            set { m_UsePerCountSettings = value; ApplyLayout(); }
        }

        public MarinaSlotCountConfig Config3Slots => m_Config3Slots;
        public MarinaSlotCountConfig Config4Slots => m_Config4Slots;
        public MarinaSlotCountConfig Config5Slots => m_Config5Slots;

        public bool EnableCurvedPier
        {
            get => m_EnableCurvedPier;
            set { m_EnableCurvedPier = value; ApplyLayout(); }
        }

        public float PierWidth4Slots
        {
            get => m_Config4Slots != null && m_UsePerCountSettings ? m_Config4Slots.PierWidth : m_PierWidth4Slots;
            set
            {
                m_PierWidth4Slots = value;
                if (m_Config4Slots != null) m_Config4Slots.PierWidth = value;
                ApplyLayout();
            }
        }

        public float PierWidth5Slots
        {
            get => m_Config5Slots != null && m_UsePerCountSettings ? m_Config5Slots.PierWidth : m_PierWidth5Slots;
            set
            {
                m_PierWidth5Slots = value;
                if (m_Config5Slots != null) m_Config5Slots.PierWidth = value;
                ApplyLayout();
            }
        }

        public float PierWidth3Slots
        {
            get => m_Config3Slots != null && m_UsePerCountSettings ? m_Config3Slots.PierWidth : m_PierWidth3Slots;
            set
            {
                m_PierWidth3Slots = value;
                if (m_Config3Slots != null) m_Config3Slots.PierWidth = value;
                ApplyLayout();
            }
        }

        public float PierOffsetY
        {
            get => m_UsePerCountSettings ? GetConfig(BayCountFor(m_SlotCount)).PierOffsetY : m_PierOffsetY;
            set
            {
                m_PierOffsetY = value;
                if (m_UsePerCountSettings) GetConfig(BayCountFor(m_SlotCount)).PierOffsetY = value;
                ApplyLayout();
            }
        }

        public float PierOffsetZ
        {
            get => m_UsePerCountSettings ? GetConfig(BayCountFor(m_SlotCount)).PierOffsetZ : m_PierOffsetZ;
            set
            {
                m_PierOffsetZ = value;
                if (m_UsePerCountSettings) GetConfig(BayCountFor(m_SlotCount)).PierOffsetZ = value;
                ApplyLayout();
            }
        }

        public float PierScaleMultiplier
        {
            get => m_UsePerCountSettings ? GetConfig(BayCountFor(m_SlotCount)).PierScaleMultiplier : m_PierScaleMultiplier;
            set
            {
                m_PierScaleMultiplier = value;
                if (m_UsePerCountSettings) GetConfig(BayCountFor(m_SlotCount)).PierScaleMultiplier = value;
                ApplyLayout();
            }
        }

        public float PierRotationX
        {
            get => m_PierRotationX;
            set { m_PierRotationX = value; ApplyLayout(); }
        }

        public float BaySlotOffsetY
        {
            get => m_UsePerCountSettings ? GetConfig(BayCountFor(m_SlotCount)).BaySlotOffsetY : m_BaySlotOffsetY;
            set
            {
                m_BaySlotOffsetY = value;
                if (m_UsePerCountSettings) GetConfig(BayCountFor(m_SlotCount)).BaySlotOffsetY = value;
                ApplyLayout();
            }
        }

        public float SlotWidth
        {
            get => m_UsePerCountSettings ? GetConfig(BayCountFor(m_SlotCount)).SlotWidth : m_SlotWidth;
            set
            {
                m_SlotWidth = value;
                if (m_UsePerCountSettings) GetConfig(BayCountFor(m_SlotCount)).SlotWidth = value;
                ApplyLayout();
            }
        }

        public float SlotLength
        {
            get => m_UsePerCountSettings ? GetConfig(BayCountFor(m_SlotCount)).SlotLength : m_SlotLength;
            set
            {
                m_SlotLength = value;
                if (m_UsePerCountSettings) GetConfig(BayCountFor(m_SlotCount)).SlotLength = value;
                ApplyLayout();
            }
        }

        public float SlotScale
        {
            get => (SlotWidth + SlotLength) * 0.5f;
            set { SlotWidth = value; SlotLength = value; m_SlotScale = value; ApplyLayout(); }
        }

        public float SlotSpacing
        {
            get => m_UsePerCountSettings ? GetConfig(BayCountFor(m_SlotCount)).LegacySpacing : m_SlotSpacing;
            set
            {
                m_SlotSpacing = value;
                if (m_UsePerCountSettings) GetConfig(BayCountFor(m_SlotCount)).LegacySpacing = value;
                ApplyLayout();
            }
        }

        public float SlotAngle
        {
            get => m_UsePerCountSettings ? GetConfig(BayCountFor(m_SlotCount)).SlotAngle : m_SlotAngle;
            set
            {
                m_SlotAngle = value;
                if (m_UsePerCountSettings) GetConfig(BayCountFor(m_SlotCount)).SlotAngle = value;
                ApplyLayout();
            }
        }

        public float WaterTiltX
        {
            get => m_WaterTiltX;
            set { m_WaterTiltX = value; ApplyLayout(); }
        }

        public float OffsetY
        {
            get => m_UsePerCountSettings ? GetConfig(BayCountFor(m_SlotCount)).RowOffsetY : m_OffsetY;
            set
            {
                m_OffsetY = value;
                if (m_UsePerCountSettings) GetConfig(BayCountFor(m_SlotCount)).RowOffsetY = value;
                ApplyLayout();
            }
        }

        public float OffsetZ
        {
            get => m_UsePerCountSettings ? GetConfig(BayCountFor(m_SlotCount)).RowOffsetZ : m_OffsetZ;
            set
            {
                m_OffsetZ = value;
                if (m_UsePerCountSettings) GetConfig(BayCountFor(m_SlotCount)).RowOffsetZ = value;
                ApplyLayout();
            }
        }

        public Material SlotBerthMaterial
        {
            get => m_SlotBerthMaterial;
            set { m_SlotBerthMaterial = value; ApplyLayout(); }
        }

        public Mesh SlotBerthMesh
        {
            get => m_SlotBerthMesh;
            set { m_SlotBerthMesh = value; ApplyLayout(); }
        }

        public float SlotBerthWidth
        {
            get => m_SlotBerthWidth;
            set { m_SlotBerthWidth = value; ApplyLayout(); }
        }

        public float SlotBerthLength
        {
            get => m_SlotBerthLength;
            set { m_SlotBerthLength = value; ApplyLayout(); }
        }

        public float SlotBerthOffsetY
        {
            get => m_SlotBerthOffsetY;
            set { m_SlotBerthOffsetY = value; ApplyLayout(); }
        }

        public float SlotBerthOffsetZ
        {
            get => m_SlotBerthOffsetZ;
            set { m_SlotBerthOffsetZ = value; ApplyLayout(); }
        }

        public float ArcCurveY
        {
            get => m_UsePerCountSettings ? GetConfig(BayCountFor(m_SlotCount)).ArcCurveY : m_ArcCurveY;
            set
            {
                m_ArcCurveY = value;
                if (m_UsePerCountSettings) GetConfig(BayCountFor(m_SlotCount)).ArcCurveY = value;
                ApplyLayout();
            }
        }

        public float ArcAsymmetry
        {
            get => m_ArcAsymmetry;
            set { m_ArcAsymmetry = value; ApplyLayout(); }
        }

        public float ArcAngleFan
        {
            get => m_ArcAngleFan;
            set { m_ArcAngleFan = value; ApplyLayout(); }
        }

        public int SlotCount
        {
            get => m_SlotCount;
            set => SetSlotCount(value);
        }

        public int OpenSlotCount => m_SlotCount;
        public int TotalBaysCount => m_SlotCount;
        public int ClosedSlotCount => 0;

        /// <summary>İskelede hep 5 sabit bölme vardır; 5'ten fazla slot istenirse bölme sayısı slot sayısına çıkar.</summary>
        public static int BayCountFor(int slotCount)
        {
            return Mathf.Max(5, slotCount);
        }

        /// <summary>
        /// Açık slotların 5 sabit bölmeden hangilerine oturacağı (bölmelerin konumu slot sayısına göre değişmez):
        /// 1: { 2 } (orta)
        /// 2: { 1, 3 }
        /// 3: { 1, 2, 3 }
        /// 4: { 0, 1, 3, 4 } (orta hariç hepsi)
        /// 5: { 0, 1, 2, 3, 4 }
        /// Kullanılmayan bölmeler boş kalır (kapalı slot görseli yok).
        /// </summary>
        public static int[] OpenBaysFor(int slotCount)
        {
            switch (slotCount)
            {
                case 1: return new[] { 2 };
                case 2: return new[] { 1, 3 };
                case 3: return new[] { 1, 2, 3 };
                case 4: return new[] { 0, 1, 3, 4 };
            }
            int n = Mathf.Max(0, slotCount);
            var all = new int[n];
            for (int i = 0; i < n; i++) all[i] = i;
            return all;
        }
        public bool MatchBackgroundBays
        {
            get => m_MatchBackgroundBays;
            set { m_MatchBackgroundBays = value; ApplyLayout(); }
        }

        public float BayFillRatio
        {
            get => m_BayFillRatio;
            set { m_BayFillRatio = value; ApplyLayout(); }
        }

        public float BayCenterPx
        {
            get => m_BayCenterPx;
            set { m_BayCenterPx = value; ApplyLayout(); }
        }

        public float BayCenterYPx
        {
            get => m_BayCenterYPx;
            set { m_BayCenterYPx = value; ApplyLayout(); }
        }

        public float BaySpacingPx
        {
            get => m_UsePerCountSettings ? GetConfig(BayCountFor(m_SlotCount)).BaySpacingPx : 174f;
            set
            {
                if (m_UsePerCountSettings) GetConfig(BayCountFor(m_SlotCount)).BaySpacingPx = value;
                ApplyLayout();
            }
        }

        public Material SlotBerthDockedMaterial
        {
            get => m_SlotBerthDockedMaterial;
            set { m_SlotBerthDockedMaterial = value; ApplyLayout(); }
        }

        public Material LockedBarrierMaterial
        {
            get => m_LockedBarrierMaterial;
            set { m_LockedBarrierMaterial = value; ApplyLayout(); }
        }

        public float LockedBarrierWidth
        {
            get => m_LockedBarrierWidth;
            set { m_LockedBarrierWidth = value; ApplyLayout(); }
        }

        public float LockedBarrierHeight
        {
            get => m_LockedBarrierHeight;
            set { m_LockedBarrierHeight = value; ApplyLayout(); }
        }

        public float LockedBarrierOffsetY
        {
            get => m_LockedBarrierOffsetY;
            set { m_LockedBarrierOffsetY = value; ApplyLayout(); }
        }

        public float LockedBarrierOffsetZ
        {
            get => m_LockedBarrierOffsetZ;
            set { m_LockedBarrierOffsetZ = value; ApplyLayout(); }
        }

        public void SetCurvedPierMaterials(Material mat4, Material mat5, Material mat3)
        {
            m_PierMaterial4Slots = mat4;
            m_PierMaterial5Slots = mat5;
            m_PierMaterial3Slots = mat3;
            EnsureConfigsInitialized();
            if (m_Config4Slots != null) m_Config4Slots.PierMaterial = mat4;
            if (m_Config5Slots != null) m_Config5Slots.PierMaterial = mat5;
            if (m_Config3Slots != null) m_Config3Slots.PierMaterial = mat3;
        }

        public MarinaSlotCountConfig GetConfig(int count)
        {
            EnsureConfigsInitialized();
            if (count == 3) return m_Config3Slots;
            if (count == 4) return m_Config4Slots;
            if (count == 5) return m_Config5Slots;

            if (m_CustomConfigs != null)
            {
                for (int i = 0; i < m_CustomConfigs.Count; i++)
                {
                    if (m_CustomConfigs[i] != null && m_CustomConfigs[i].SlotCount == count)
                    {
                        return m_CustomConfigs[i];
                    }
                }
            }

            if (count <= 3) return m_Config3Slots;
            return m_Config5Slots;
        }

        public void EnsureConfigsInitialized()
        {
            // 3 Slot Ayarları
            if (m_Config3Slots == null || m_Config3Slots.SlotCount != 3)
            {
                m_Config3Slots = new MarinaSlotCountConfig(3)
                {
                    RowOffsetX = 0.042f, // Rıhtım babaları arasına tam ortalayan X ofseti
                    RowOffsetY = -5.40f, // Arka plandaki ahşap iskele ve babalar seviyesi (rıhtımın tam kıyısı)
                    RowOffsetZ = 0.05f,
                    PierWidth = m_PierWidth3Slots > 0.01f ? m_PierWidth3Slots : 7.2f,
                    PierOffsetY = 2.06f,
                    PierOffsetZ = 0.04f,
                    PierScaleMultiplier = m_PierScaleMultiplier > 0.01f ? m_PierScaleMultiplier : 1.07f,
                    BaySpacingPx = 174f,
                    BaySlotOffsetY = 0f,
                    ArcCurveY = 0f,
                    SlotAngle = 0f,
                    SlotWidth = m_SlotWidth > 0.01f ? m_SlotWidth : 1.00f,
                    SlotLength = m_SlotLength > 0.01f ? m_SlotLength : 1.55f,
                    LegacySpacing = 1.454f,
                    PierMaterial = m_PierMaterial3Slots
                };
            }

            // 4 Slot Ayarları
            if (m_Config4Slots == null || m_Config4Slots.SlotCount != 4)
            {
                m_Config4Slots = new MarinaSlotCountConfig(4)
                {
                    RowOffsetX = 0.042f,
                    RowOffsetY = -5.40f, // Düz iskele hizası (rıhtımın tam kıyısı)
                    RowOffsetZ = 0.05f,
                    PierWidth = m_PierWidth4Slots > 0.01f ? m_PierWidth4Slots : 8.1f,
                    PierOffsetY = 2.06f,
                    PierOffsetZ = 0.04f,
                    PierScaleMultiplier = m_PierScaleMultiplier > 0.01f ? m_PierScaleMultiplier : 1.07f,
                    BaySpacingPx = 174f,
                    BaySlotOffsetY = 0f,
                    ArcCurveY = 0f,
                    SlotAngle = 0f,
                    SlotWidth = m_SlotWidth > 0.01f ? m_SlotWidth : 1.00f,
                    SlotLength = m_SlotLength > 0.01f ? m_SlotLength : 1.55f,
                    LegacySpacing = 1.454f,
                    PierMaterial = m_PierMaterial4Slots
                };
            }

            // 5 Slot Ayarları
            if (m_Config5Slots == null || m_Config5Slots.SlotCount != 5)
            {
                m_Config5Slots = new MarinaSlotCountConfig(5)
                {
                    RowOffsetX = 0.042f,
                    RowOffsetY = -5.40f, // 5 adet ahşap babaya tam oturan Y seviyesi
                    RowOffsetZ = 0.05f,
                    PierWidth = m_PierWidth5Slots > 0.01f ? m_PierWidth5Slots : 8.55f,
                    PierOffsetY = 2.06f,
                    PierOffsetZ = 0.04f,
                    PierScaleMultiplier = m_PierScaleMultiplier > 0.01f ? m_PierScaleMultiplier : 1.07f,
                    BaySpacingPx = 174f,
                    BaySlotOffsetY = 0f,
                    ArcCurveY = 0f,
                    SlotAngle = 0f,
                    SlotWidth = m_SlotWidth > 0.01f ? m_SlotWidth : 1.00f,
                    SlotLength = m_SlotLength > 0.01f ? m_SlotLength : 1.55f,
                    LegacySpacing = 1.454f,
                    PierMaterial = m_PierMaterial5Slots
                };
            }

#if UNITY_EDITOR
            if (m_SlotBerthMaterial == null)
                m_SlotBerthMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Marina/Slot_Berth_Mat.mat");
            if (m_SlotBerthClosedMaterial == null)
                m_SlotBerthClosedMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Marina/Slot_Berth_Closed_Mat.mat");
            if (m_SlotBerthInnerMaterial == null)
                m_SlotBerthInnerMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Marina/Slot_Berth_Inner_Mat.mat");
            if (m_SlotBerthMesh == null)
                m_SlotBerthMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/Marina/Slot_Berth_Quad.asset");
            if (m_Background3Slots == null)
                m_Background3Slots = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>("Assets/Kenney/BeachBackground_3Slots.png");
            if (m_Background4Slots == null)
                m_Background4Slots = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>("Assets/Kenney/BeachBackground_4Slots.png");
            if (m_Config3Slots.PierMaterial == null)
                m_Config3Slots.PierMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Marina/Pier_Curved_3Slots_Mat.mat");
            if (m_Config4Slots.PierMaterial == null)
                m_Config4Slots.PierMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Marina/Pier_Curved_4Slots_Mat.mat");
            if (m_Config5Slots.PierMaterial == null)
                m_Config5Slots.PierMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Marina/Pier_Curved_5Slots_Mat.mat");
            if (m_LockedBarrierMaterial == null)
                m_LockedBarrierMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Marina/Slot_Locked_Barrier_Mat.mat");
#endif
        }

        /// <summary>
        /// İstenen slot sayısı için (3, 4, 5 veya hepsi) kıyıya kalibre edilmiş varsayılan değerleri yükler.
        /// </summary>
        public void ResetToCalibratedDefaults(int count = 0)
        {
            if (count == 0 || count == 3)
            {
                m_Config3Slots = new MarinaSlotCountConfig(3)
                {
                    RowOffsetX = 0.042f,
                    RowOffsetY = -5.40f,
                    RowOffsetZ = 0.05f,
                    PierWidth = 7.2f,
                    PierOffsetY = 2.06f,
                    PierOffsetZ = 0.04f,
                    PierScaleMultiplier = 1.07f,
                    BaySpacingPx = 174f,
                    BaySlotOffsetY = 0f,
                    ArcCurveY = 0f,
                    SlotAngle = 0f,
                    SlotWidth = 1.00f,
                    SlotLength = 1.55f,
                    LegacySpacing = 1.454f,
                    PierMaterial = m_PierMaterial3Slots
                };
            }

            if (count == 0 || count == 4)
            {
                m_Config4Slots = new MarinaSlotCountConfig(4)
                {
                    RowOffsetX = 0.042f,
                    RowOffsetY = -5.40f,
                    RowOffsetZ = 0.05f,
                    PierWidth = 8.1f,
                    PierOffsetY = 2.06f,
                    PierOffsetZ = 0.04f,
                    PierScaleMultiplier = 1.07f,
                    BaySpacingPx = 174f,
                    BaySlotOffsetY = 0f,
                    ArcCurveY = 0f,
                    SlotAngle = 0f,
                    SlotWidth = 1.00f,
                    SlotLength = 1.55f,
                    LegacySpacing = 1.454f,
                    PierMaterial = m_PierMaterial4Slots
                };
            }

            if (count == 0 || count == 5)
            {
                m_Config5Slots = new MarinaSlotCountConfig(5)
                {
                    RowOffsetX = 0.042f,
                    RowOffsetY = -5.40f,
                    RowOffsetZ = 0.05f,
                    PierWidth = 8.55f,
                    PierOffsetY = 2.06f,
                    PierOffsetZ = 0.04f,
                    PierScaleMultiplier = 1.07f,
                    BaySpacingPx = 174f,
                    BaySlotOffsetY = 0f,
                    ArcCurveY = 0f,
                    SlotAngle = 0f,
                    SlotWidth = 1.00f,
                    SlotLength = 1.55f,
                    LegacySpacing = 1.454f,
                    PierMaterial = m_PierMaterial5Slots
                };
            }

            EnsureConfigsInitialized();
            ApplyLayout();
        }

        private void Awake()
        {
            EnsureConfigsInitialized();
            DeactivateAllBarriers();
            SyncWithLevel();
        }

        private void OnEnable()
        {
            EnsureConfigsInitialized();
            DeactivateAllBarriers();
            PixelArtGenerator.LevelLoaded -= OnLevelLoaded;
            PixelArtGenerator.LevelLoaded += OnLevelLoaded;
#if UNITY_EDITOR
            // Edit modunda Update yalnızca sahne değişince çalışır; Game view yeniden boyutlanınca da hizala
            UnityEditor.EditorApplication.update -= Update;
            if (!Application.isPlaying) UnityEditor.EditorApplication.update += Update;
#endif
        }

        private void OnDisable()
        {
            PixelArtGenerator.LevelLoaded -= OnLevelLoaded;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= Update;
#endif
        }

        private void Start()
        {
            SyncWithLevel();
        }

        private void OnLevelLoaded(PixelLevelData level)
        {
            if (level != null && level.SlotCount > 0)
            {
                SetSlotCount(level.SlotCount);
            }
            else
            {
                ApplyLayout();
            }
        }

        public void SyncWithLevel()
        {
            PixelArtGenerator gen = UnityEngine.Object.FindFirstObjectByType<PixelArtGenerator>();
            PixelLevelData level = gen != null ? gen.ActiveLevelData : null;
            if (level != null && level.SlotCount > 0)
            {
                SetSlotCount(level.SlotCount);
            }
            else
            {
                ApplyLayout();
            }
        }

        public void SetSlotCount(int targetCount)
        {
            m_SlotCount = Mathf.Clamp(targetCount, 1, 8);
            EnsureConfigsInitialized();

            var allSlots = new List<ShipSlot>(GetComponentsInChildren<ShipSlot>(true));
            allSlots.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));

            if (allSlots.Count == 0) return;

            // Eksik slot varsa ilkinden klonlayarak üret
            while (allSlots.Count < m_SlotCount)
            {
                GameObject newSlotObj = Instantiate(allSlots[0].gameObject, transform);
                newSlotObj.name = $"WaterSlot_{allSlots.Count + 1}";
                ShipSlot newSlot = newSlotObj.GetComponent<ShipSlot>();
                if (newSlot != null)
                {
                    newSlot.SlotIndex = allSlots.Count;
                    newSlot.ReleaseShip();
                }
                allSlots.Add(newSlot);
            }

            // İstenen sayı kadarını aktif yap, fazlasını deaktive et
            for (int i = 0; i < allSlots.Count; i++)
            {
                if (allSlots[i] != null)
                {
                    allSlots[i].SlotIndex = i;
                    bool shouldBeActive = (i < m_SlotCount);
                    if (allSlots[i].gameObject.activeSelf != shouldBeActive)
                    {
                        allSlots[i].gameObject.SetActive(shouldBeActive);
                    }
                }
            }

            ApplyLayout();
        }

        private void OnValidate()
        {
            if (m_SlotWidth <= 0.001f) m_SlotWidth = 1.05f;
            if (m_SlotLength <= 0.001f) m_SlotLength = 1.55f;
            EnsureConfigsInitialized();
            ApplyLayout();
        }

        private void Reset()
        {
            m_SlotCount = 5;
            m_SlotWidth = 1.05f;
            m_SlotLength = 1.55f;
            m_SlotScale = 1.27f;
            m_SlotSpacing = 1.68f;
            m_SlotAngle = 0f;
            m_WaterTiltX = -28f;
            m_OffsetY = -2.20f;
            m_OffsetZ = 0.05f;
            m_ArcCurveY = 0.042f;
            m_ArcAsymmetry = 0f;
            m_ArcAngleFan = 0f;
            ResetToCalibratedDefaults(0);
            ApplyLayout();
        }

        private static Mesh s_BerthQuadMesh;

        private static Mesh GetBerthQuadMesh()
        {
            if (s_BerthQuadMesh != null && s_BerthQuadMesh.vertexCount == 4) return s_BerthQuadMesh;

#if UNITY_EDITOR
            s_BerthQuadMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/Marina/Slot_Berth_Quad.asset");
            if (s_BerthQuadMesh != null && s_BerthQuadMesh.vertexCount == 4) return s_BerthQuadMesh;
#endif

            s_BerthQuadMesh = new Mesh { name = "Slot_Berth_Quad" };
            s_BerthQuadMesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, 0f, -0.5f), // 0: sol-alt (açık su / giriş)
                new Vector3( 0.5f, 0f, -0.5f), // 1: sağ-alt (açık su / giriş)
                new Vector3(-0.5f, 0f,  0.5f), // 2: sol-üst (kumsal / rıhtım)
                new Vector3( 0.5f, 0f,  0.5f)  // 3: sağ-üst (kumsal / rıhtım)
            };
            s_BerthQuadMesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            s_BerthQuadMesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            s_BerthQuadMesh.normals = new Vector3[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            s_BerthQuadMesh.RecalculateBounds();

#if UNITY_EDITOR
            if (!System.IO.File.Exists("Assets/Meshes/Marina/Slot_Berth_Quad.asset"))
            {
                if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Meshes/Marina"))
                {
                    if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Meshes")) UnityEditor.AssetDatabase.CreateFolder("Assets", "Meshes");
                    UnityEditor.AssetDatabase.CreateFolder("Assets/Meshes", "Marina");
                }
                UnityEditor.AssetDatabase.CreateAsset(s_BerthQuadMesh, "Assets/Meshes/Marina/Slot_Berth_Quad.asset");
            }
#endif
            return s_BerthQuadMesh;
        }

        private static Mesh s_PierQuadMesh;

        private static Mesh GetPierQuadMesh()
        {
            if (s_PierQuadMesh != null && s_PierQuadMesh.bounds.extents.y > 0.1f) return s_PierQuadMesh;

#if UNITY_EDITOR
            s_PierQuadMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/Marina/Pier_Curved_Quad.asset");
            if (s_PierQuadMesh != null && s_PierQuadMesh.bounds.extents.y > 0.1f) return s_PierQuadMesh;
#endif

            s_PierQuadMesh = new Mesh { name = "Pier_Curved_Quad" };
            s_PierQuadMesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0f), // 0: sol-alt (parmak iskeleler / açık su)
                new Vector3( 0.5f, -0.5f, 0f), // 1: sağ-alt (parmak iskeleler / açık su)
                new Vector3(-0.5f,  0.5f, 0f), // 2: sol-üst (kumsal / kanatlar)
                new Vector3( 0.5f,  0.5f, 0f)  // 3: sağ-üst (kumsal / kanatlar)
            };
            s_PierQuadMesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            s_PierQuadMesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            s_PierQuadMesh.normals = new Vector3[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            s_PierQuadMesh.RecalculateBounds();

#if UNITY_EDITOR
            if (!System.IO.File.Exists("Assets/Meshes/Marina/Pier_Curved_Quad.asset"))
            {
                if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Meshes/Marina"))
                {
                    if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Meshes")) UnityEditor.AssetDatabase.CreateFolder("Assets", "Meshes");
                    UnityEditor.AssetDatabase.CreateFolder("Assets/Meshes", "Marina");
                }
                UnityEditor.AssetDatabase.CreateAsset(s_PierQuadMesh, "Assets/Meshes/Marina/Pier_Curved_Quad.asset");
            }
#endif
            return s_PierQuadMesh;
        }

        /// <summary>
        /// Tüm aktif çocuk slot nesnelerini kavisli iskele (veya klasik can simidi) düzenine göre anında yeniden hizalar ve ölçekler.
        /// </summary>
        [ContextMenu("Slotları Yeniden Hizala (Apply Layout)")]
        public void ApplyLayout()
        {
            EnsureConfigsInitialized();

            var allSlots = GetComponentsInChildren<ShipSlot>(true);
            if (allSlots == null || allSlots.Length == 0) return;

            // Sadece aktif slotları filtrele ve sırala
            var activeSlots = new List<ShipSlot>();
            for (int i = 0; i < allSlots.Length; i++)
            {
                if (allSlots[i] != null && allSlots[i].gameObject.activeSelf)
                {
                    activeSlots.Add(allSlots[i]);
                }
            }

            activeSlots.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));

            int count = activeSlots.Count;
            if (count == 0) return;

            // 5 bölme sabit: 1-5 slotta yerleşim (aralık, kıyı ofseti, arka plan) hep 5 slot ayarlarından gelir
            int layoutCount = BayCountFor(count);
            MarinaSlotCountConfig cfg = m_UsePerCountSettings ? GetConfig(layoutCount) : null;
            float rowOffsetX = cfg != null ? cfg.RowOffsetX : 0.042f;
            float rowOffsetY = cfg != null ? cfg.RowOffsetY : m_OffsetY;
            float rowOffsetZ = cfg != null ? cfg.RowOffsetZ : m_OffsetZ;

            transform.localPosition = new Vector3(rowOffsetX, rowOffsetY, rowOffsetZ);

            if (m_EnableCurvedPier)
            {
                ApplyCurvedPierLayout(activeSlots, count, cfg);
            }
            else
            {
                ApplyLegacyDockLayout(activeSlots, count, cfg);
                UpdateBackgroundTexture(layoutCount);
            }
        }

        private void ApplyCurvedPierLayout(List<ShipSlot> activeSlots, int count, MarinaSlotCountConfig cfg)
        {
            // 1. Slot sayısına göre uygun materyal, genişlik ve doku oranını seç
            Material targetMat = cfg != null ? cfg.PierMaterial : (count == 4 ? m_PierMaterial4Slots : (count <= 3 ? m_PierMaterial3Slots : m_PierMaterial5Slots));
            float pierWidth = cfg != null ? cfg.PierWidth : (count == 4 ? m_PierWidth4Slots : (count <= 3 ? m_PierWidth3Slots : m_PierWidth5Slots));
            float scaleMultiplier = cfg != null ? cfg.PierScaleMultiplier : m_PierScaleMultiplier;
            float pierOffsetY = cfg != null ? cfg.PierOffsetY : m_PierOffsetY;
            float pierOffsetZ = cfg != null ? cfg.PierOffsetZ : m_PierOffsetZ;
            float baySlotOffsetY = cfg != null ? cfg.BaySlotOffsetY : m_BaySlotOffsetY;
            float arcCurveY = cfg != null ? cfg.ArcCurveY : m_ArcCurveY;
            float slotAngle = cfg != null ? cfg.SlotAngle : m_SlotAngle;
            float slotWidth = cfg != null ? cfg.SlotWidth : m_SlotWidth;
            float slotLength = cfg != null ? cfg.SlotLength : m_SlotLength;
            float baySpacingPx = cfg != null ? cfg.BaySpacingPx : (count == 4 ? 173f : (count <= 3 ? 171f : 174f));

            float texWidth = count == 4 ? 1246f : (count <= 3 ? 1070f : 1422f);
            float texHeight = 634f;

#if UNITY_EDITOR
            if (targetMat == null)
            {
                string matName = count == 4 ? "Pier_Curved_4Slots_Mat" : (count <= 3 ? "Pier_Curved_3Slots_Mat" : "Pier_Curved_5Slots_Mat");
                targetMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/Marina/{matName}.mat");
            }
#endif

            float aspect = texWidth / texHeight;
            float finalWidth = pierWidth * scaleMultiplier;
            float finalHeight = finalWidth / aspect;

            // 2. Kavisli İskele nesnesini bul veya oluştur
            Transform pierTr = transform.Find("[Marina_Curved_Pier]");
            if (pierTr == null)
            {
                GameObject pGo = new GameObject("[Marina_Curved_Pier]");
                pierTr = pGo.transform;
                pierTr.SetParent(transform, false);
            }

            if (!pierTr.gameObject.activeSelf)
            {
                pierTr.gameObject.SetActive(true);
            }

            pierTr.localPosition = new Vector3(0f, pierOffsetY, pierOffsetZ);
            pierTr.localRotation = Quaternion.Euler(m_PierRotationX, 0f, 0f);
            float stretchX = cfg != null && cfg.PierStretchX > 0.01f ? cfg.PierStretchX : 1f;
            float stretchedWidth = finalWidth * stretchX;
            pierTr.localScale = new Vector3(stretchedWidth, finalHeight, 1f);

            MeshFilter mf = pierTr.GetComponent<MeshFilter>();
            if (mf == null) mf = pierTr.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = GetPierQuadMesh();

            MeshRenderer mr = pierTr.GetComponent<MeshRenderer>();
            if (mr == null) mr = pierTr.gameObject.AddComponent<MeshRenderer>();
            if (targetMat != null && mr.sharedMaterial != targetMat)
            {
                mr.sharedMaterial = targetMat;
            }
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            mr.sortingOrder = 5;

            // 3. Slotları kavisli iskelenin koylarına matematiksel olarak tam oturt
            float worldBaySpacing = (baySpacingPx / texWidth) * stretchedWidth;
            float startX = -(count - 1) * worldBaySpacing * 0.5f;

            // Gemilerin iskele kolları arasında açık suya oturma derinliği
            float baseBayLocalY = -0.52f * finalHeight + baySlotOffsetY;

            for (int i = 0; i < count; i++)
            {
                var slot = activeSlots[i];
                Transform tr = slot.transform;
                float localX = startX + i * worldBaySpacing;
                float t = count > 1 ? (i - (count - 1) * 0.5f) : 0f;
                float localCurveY = arcCurveY * (t * t) + m_ArcAsymmetry * t;
                float angle = slotAngle + m_ArcAngleFan * t;

                // İskelenin kendi yerel düzleminde (Z=0) hesaplayıp pierTr rotasyonuyla marina koordinatlarına dönüştür
                Vector3 pierLocalSlotPos = new Vector3(localX, baseBayLocalY + localCurveY, 0f);
                tr.localPosition = pierTr.localPosition + pierTr.localRotation * pierLocalSlotPos;

                ShipQueuePool queuePool = UnityEngine.Object.FindFirstObjectByType<ShipQueuePool>();
                Quaternion baseSlotRot = queuePool != null ? queuePool.transform.rotation : Quaternion.Euler(m_WaterTiltX, 0f, 0f);
                tr.rotation = baseSlotRot * Quaternion.Euler(0f, angle, 0f);
                tr.localScale = new Vector3(slotWidth, 1f, slotLength);

                // Kavisli iskele tek parça olduğu için eski slot görsellerini gizle
                Transform lifebuoy = tr.Find("[Slot_Lifebuoy]");
                if (lifebuoy != null && lifebuoy.gameObject.activeSelf)
                {
                    lifebuoy.gameObject.SetActive(false);
                }
                Transform foamSlot = tr.Find("FoamSlot");
                if (foamSlot != null && foamSlot.gameObject.activeSelf)
                {
                    foamSlot.gameObject.SetActive(false);
                }
                Transform berth = tr.Find("[Slot_Berth]");
                if (berth != null && berth.gameObject.activeSelf)
                {
                    berth.gameObject.SetActive(false);
                }
            }

            var existingBarriers = GetComponentsInChildren<MarinaLockedSlotBarrier>(true);
            for (int b = 0; b < existingBarriers.Length; b++)
            {
                if (existingBarriers[b] != null && existingBarriers[b].gameObject.activeSelf)
                    existingBarriers[b].gameObject.SetActive(false);
            }
        }

        private void ApplyLegacyDockLayout(List<ShipSlot> activeSlots, int count, MarinaSlotCountConfig cfg)
        {
            Transform pierTr = transform.Find("[Marina_Curved_Pier]");
            if (pierTr != null && pierTr.gameObject.activeSelf)
            {
                pierTr.gameObject.SetActive(false);
            }

            float rawSpacing = cfg != null ? cfg.LegacySpacing : m_SlotSpacing;
            float slotWidth = cfg != null ? cfg.SlotWidth : m_SlotWidth;
            float slotLength = cfg != null ? cfg.SlotLength : m_SlotLength;
            float arcCurveY = cfg != null ? cfg.ArcCurveY : m_ArcCurveY;
            float slotAngle = cfg != null ? cfg.SlotAngle : m_SlotAngle;

            float fit = 1f;
            float spacing;
            float baySpacingPx = cfg != null && cfg.BaySpacingPx > 1f ? cfg.BaySpacingPx : 174f;
            BackgroundFrame bgFrame = default;
            bool matchBays = m_MatchBackgroundBays && TryGetBackgroundFrame(out bgFrame);
            if (matchBays)
            {
                // Aralık babalara kilitlenir; slot görseli de iki baba arasındaki boşluğu dolduracak kadar ölçeklenir
                float scaleX = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
                spacing = bgFrame.PxToWorldX(baySpacingPx) / scaleX;
                float berthWidth = m_SlotBerthWidth > 0.001f ? m_SlotBerthWidth : 0.98f;
                fit = m_BayFillRatio * spacing / berthWidth;
            }
            else
            {
                // Sıra ekrana sığmıyorsa aralık ve slot boyutu birlikte küçülür
                Camera fitCam = Camera.main;
                if (m_FitToScreenWidth && fitCam != null && fitCam.orthographic)
                {
                    float available = 2f * fitCam.orthographicSize * fitCam.aspect - 2f * m_ScreenEdgeMargin;
                    float needed = (count - 1) * rawSpacing + slotWidth;
                    if (needed > available && needed > 0.001f) fit = Mathf.Max(0.5f, available / needed);
                }
                spacing = rawSpacing * fit;
            }
            // Slotlar arka plandaki bölmelere oturur (ortadaki bölme yerel x=0); sıra OpenBaysFor'da
            int[] openBays = OpenBaysFor(count);
            int bayCount = BayCountFor(count);
            float bayCenter = (bayCount - 1) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var slot = activeSlots[i];
                Transform tr = slot.transform;
                float t = openBays[i] - bayCenter;
                float posX = t * spacing;
                float posY = arcCurveY * (t * t) + m_ArcAsymmetry * t;
                float angle = slotAngle + m_ArcAngleFan * t;

                tr.localPosition = new Vector3(posX, posY, 0f);
                ShipQueuePool queuePool = UnityEngine.Object.FindFirstObjectByType<ShipQueuePool>();
                Quaternion baseSlotRot = queuePool != null ? queuePool.transform.rotation : Quaternion.Euler(m_WaterTiltX, 0f, 0f);
                tr.rotation = baseSlotRot * Quaternion.Euler(0f, angle, 0f);
                tr.localScale = new Vector3(slotWidth * fit, 1f, slotLength * fit);

                // Eski slot görselleri (can simidi, köpük vb.) gizlenir
                Transform foamSlot = tr.Find("FoamSlot");
                if (foamSlot != null && foamSlot.gameObject.activeSelf) foamSlot.gameObject.SetActive(false);
                Transform lifebuoy = tr.Find("[Slot_Lifebuoy]");
                if (lifebuoy != null && lifebuoy.gameObject.activeSelf) lifebuoy.gameObject.SetActive(false);
                Transform indicator = tr.Find("IndicatorMesh");
                bool useIndicator = m_UseIndicatorMeshVisual && !m_UseInnerBerthVisual && indicator != null;
                if (indicator != null && indicator.gameObject.activeSelf != useIndicator) indicator.gameObject.SetActive(useIndicator);

                // 2. Görsel Yuvarlak Kare Slot varsa eski [Slot_Berth] nesnesini devre dışı bırak
                Transform roundedSquareTr = tr.Find("[Slot_RoundedSquare]");
                if (roundedSquareTr != null)
                {
                    Transform oldBerth = tr.Find("[Slot_Berth]");
                    if (oldBerth != null && oldBerth.gameObject.activeSelf) oldBerth.gameObject.SetActive(false);
                    continue;
                }

                // 🛥️ Su Üstü Yanaşma Slotu (Floating Berth Slip)
                Transform berthTr = tr.Find("[Slot_Berth]");
                if (berthTr == null)
                {
                    GameObject bGo = new GameObject("[Slot_Berth]");
                    bGo.transform.SetParent(tr, false);
                    berthTr = bGo.transform;
                }

                if (m_HideSlotIndicators)
                {
                    if (berthTr.gameObject.activeSelf) berthTr.gameObject.SetActive(false);
                }
                else
                {
                    if (!berthTr.gameObject.activeSelf) berthTr.gameObject.SetActive(true);
                }

                berthTr.localPosition = new Vector3(0f, m_SlotBerthOffsetY, m_SlotBerthOffsetZ);
                berthTr.localRotation = Quaternion.identity;
                float fitSlotWidth = slotWidth > 0.001f ? slotWidth : 1f;
                float fitSlotLength = slotLength > 0.001f ? slotLength : 1.55f;
                float openBerthLength = m_SlotBerthLength * (m_UseInnerBerthVisual ? m_OpenBerthLengthScale : 1f);
                berthTr.localScale = new Vector3(m_SlotBerthWidth / fitSlotWidth, 1f, openBerthLength / fitSlotLength);

                MeshFilter bMf = berthTr.GetComponent<MeshFilter>();
                if (bMf == null) bMf = berthTr.gameObject.AddComponent<MeshFilter>();
                Mesh bMesh = m_SlotBerthMesh != null ? m_SlotBerthMesh : GetBerthQuadMesh();
                if (bMf.sharedMesh != bMesh) bMf.sharedMesh = bMesh;

                MeshRenderer bMr = berthTr.GetComponent<MeshRenderer>();
                if (bMr == null) bMr = berthTr.gameObject.AddComponent<MeshRenderer>();
                Material berthMat = m_UseInnerBerthVisual && m_SlotBerthInnerMaterial != null ? m_SlotBerthInnerMaterial : m_SlotBerthMaterial;
                if (berthMat != null && bMr.sharedMaterial != berthMat)
                {
                    bMr.sharedMaterial = berthMat;
                }
                bMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                bMr.receiveShadows = false;
                bMr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                bMr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                bMr.sortingOrder = 3;

                var bBobbing = berthTr.GetComponent<MarinaBuoyBobbing>();
                if (bBobbing == null) berthTr.gameObject.AddComponent<MarinaBuoyBobbing>();

                // 🚢 Gemi yanaşırken slotta oluşan siyahlık kaldırıldı (Docked Shadow iptal edildi)
                Transform dockedShadowTr = tr.Find("[Slot_DockedShadow]");
                if (dockedShadowTr != null)
                {
                    dockedShadowTr.gameObject.SetActive(false);
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        UnityEditor.EditorApplication.delayCall += () =>
                        {
                            if (dockedShadowTr != null && dockedShadowTr.gameObject != null)
                            {
                                UnityEngine.Object.DestroyImmediate(dockedShadowTr.gameObject);
                            }
                        };
                    }
                    else
#endif
                    {
                        UnityEngine.Object.Destroy(dockedShadowTr.gameObject);
                    }
                }

                // Slot bileşeninin referanslarını güncelle
                ShipSlot shipSlot = tr.GetComponent<ShipSlot>();
                if (shipSlot != null)
                {
                    shipSlot.EnsureIndicatorReference();
                }

                // Açık slot görseli olarak indicator asset'i: kesikli kutunun yerini ve boyutunu alır
                if (useIndicator)
                {
                    FitIndicatorToBerth(tr, indicator, berthTr.localPosition,
                        m_SlotBerthWidth / fitSlotWidth, m_SlotBerthLength / fitSlotLength);
                    if (berthTr.gameObject.activeSelf) berthTr.gameObject.SetActive(false);
                }
            }

            // Kapalı slotlar kaldırıldı (kullanıcı kararı): yalnızca açık slotlar gösterilir. Kapalı bölme görseli
            // hiçbir yerden oluşturulmaz; önceki sürümlerden sahnede kalanlar gizlenir. Geri getirmeyin.
            RemoveClosedBayVisuals();

            // Sıranın ortasını (yerel x=0) arka plandaki orta bölmeye, yanaşma görselini de babaların arasına oturt
            if (matchBays)
            {
                Transform firstSlot = activeSlots[0].transform;
                Transform firstBerth = firstSlot.Find("[Slot_Berth]");
                Vector3 berthOffset = firstBerth != null ? firstBerth.position - firstSlot.position : Vector3.zero;
                Vector3 rowVisualCenter = transform.position + berthOffset;
                Vector3 target = bgFrame.PxToWorld(m_BayCenterPx, m_BayCenterYPx);
                Vector3 delta = target - rowVisualCenter;
                transform.position += bgFrame.Right * Vector3.Dot(delta, bgFrame.Right)
                                    + bgFrame.Up * Vector3.Dot(delta, bgFrame.Up);
            }
        }

        /// <summary>
        /// Indicator modelini (FBX) slot yerel uzayında ölçüp, taban izi verilen genişlik/uzunluğa ve merkezi
        /// yanaşma noktasına denk gelecek şekilde ölçekler ve konumlar.
        /// </summary>
        private void FitIndicatorToBerth(Transform slotTr, Transform indicator, Vector3 center, float width, float length)
        {
            indicator.localRotation = Quaternion.identity;
            indicator.localPosition = center;
            indicator.localScale = Vector3.one;

            bool any = false;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (MeshFilter mf in indicator.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Bounds b = mf.sharedMesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents,
                        new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f));
                    Vector3 p = slotTr.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                    any = true;
                }
            }
            if (!any) return;

            Vector3 size = max - min;
            if (size.x < 0.0001f || size.z < 0.0001f) return;
            float sx = width / size.x;
            float sz = length / size.z;
            float sy = (sx + sz) * 0.5f * m_IndicatorHeightScale;
            indicator.localScale = new Vector3(sx, sy, sz);

            // Ölçek pivot etrafında uygulandı; modelin taban merkezini yanaşma noktasına kaydır
            Vector3 meshCenter = (min + max) * 0.5f;
            Vector3 scaledCenter = center + Vector3.Scale(meshCenter - center, new Vector3(sx, sy, sz));
            Vector3 scaledBottom = center + Vector3.Scale(new Vector3(meshCenter.x, min.y, meshCenter.z) - center, new Vector3(sx, sy, sz));
            indicator.localPosition = center + new Vector3(center.x - scaledCenter.x, center.y - scaledBottom.y, center.z - scaledCenter.z);

            // Yumuşak, yarı saydam görünüm (eski kesikli slot gibi): ışıksız, şeffaf materyal
            if (m_IndicatorRuntimeMaterial == null)
            {
                Shader s = Shader.Find("Sprites/Default");
                if (s != null) m_IndicatorRuntimeMaterial = new Material(s) { name = "Slot_Indicator_Soft (Runtime)", hideFlags = HideFlags.DontSave };
            }
            if (m_IndicatorRuntimeMaterial != null) m_IndicatorRuntimeMaterial.color = m_IndicatorColor;

            foreach (MeshRenderer r in indicator.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (m_IndicatorRuntimeMaterial != null && r.sharedMaterial != m_IndicatorRuntimeMaterial)
                    r.sharedMaterial = m_IndicatorRuntimeMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.sortingOrder = 3;
            }
        }

        /// <summary>Ekrana gerilmiş arka plan dokusunun piksel koordinatlarını dünya uzayına eşler.</summary>
        private struct BackgroundFrame
        {
            public Vector3 BottomLeft;
            public Vector3 AxisU;   // doku genişliği boyunca (soldan sağa)
            public Vector3 AxisV;   // doku yüksekliği boyunca (alttan üste)
            public Rect UV;
            public float TexWidth;
            public float TexHeight;
            public Vector3 Right;
            public Vector3 Up;

            /// <summary>Dokuda soldan px, üstten py pikseldeki noktanın dünya konumu.</summary>
            public Vector3 PxToWorld(float px, float pyFromTop)
            {
                float u = (px / TexWidth - UV.x) / UV.width;
                float v = ((1f - pyFromTop / TexHeight) - UV.y) / UV.height;
                return BottomLeft + AxisU * u + AxisV * v;
            }

            public float PxToWorldX(float px) => AxisU.magnitude * px / (TexWidth * UV.width);
        }

        private bool TryGetBackgroundFrame(out BackgroundFrame frame)
        {
            frame = default;
            GameObject bgObj = GameObject.Find("BackgroundImage");
            UnityEngine.UI.RawImage rawImg = bgObj != null ? bgObj.GetComponent<UnityEngine.UI.RawImage>() : null;
            if (rawImg == null || rawImg.texture == null) return false;
            if (rawImg.uvRect.width < 0.0001f || rawImg.uvRect.height < 0.0001f) return false;

            Camera cam = rawImg.canvas != null && rawImg.canvas.worldCamera != null ? rawImg.canvas.worldCamera : Camera.main;
            if (cam == null || !cam.orthographic) return false;

            var corners = new Vector3[4];
            rawImg.rectTransform.GetWorldCorners(corners); // 0: sol-alt, 1: sol-üst, 2: sağ-üst, 3: sağ-alt
            Vector3 axisU = corners[3] - corners[0];
            if (axisU.sqrMagnitude < 0.0001f) return false;

            frame = new BackgroundFrame
            {
                BottomLeft = corners[0],
                AxisU = axisU,
                AxisV = corners[1] - corners[0],
                UV = rawImg.uvRect,
                // Import ayarı (NPOT -> ToNearest) dokuyu 1024x2048'e gerdiği için texture.width/height
                // piksel ölçümleriyle uyuşmaz; ölçümler orijinal görsel boyutuna göre yapıldı.
                TexWidth = m_BackgroundSourceSizePx.x > 1f ? m_BackgroundSourceSizePx.x : rawImg.texture.width,
                TexHeight = m_BackgroundSourceSizePx.y > 1f ? m_BackgroundSourceSizePx.y : rawImg.texture.height,
                Right = cam.transform.right,
                Up = cam.transform.up
            };
            return true;
        }

        private void Update()
        {
            // Arka plan boyutu değişince (Game view / cihaz döndürme) babalara yeniden hizala
            if (!m_MatchBackgroundBays || m_EnableCurvedPier) return;
            if (!TryGetBackgroundFrame(out BackgroundFrame frame)) return;
            Vector2 size = new Vector2(frame.AxisU.magnitude, frame.AxisV.magnitude);
            if ((size - m_LastBackgroundSize).sqrMagnitude < 0.000001f) return;
            m_LastBackgroundSize = size;
            ApplyLayout();
        }

        public void UpdateBackgroundTexture(int count)
        {
            EnsureBackgroundTexturesInitialized();
            GameObject bgObj = GameObject.Find("BackgroundImage");
            if (bgObj != null)
            {
                UnityEngine.UI.RawImage rawImg = bgObj.GetComponent<UnityEngine.UI.RawImage>();
                if (rawImg != null)
                {
                    // Her slot sayısında kumlu temiz sahil arka planı kullanılır (su ve iskeleler kaldırıldı)
                    Texture targetTex = m_BackgroundAllBaysOpen != null ? m_BackgroundAllBaysOpen : (m_Background4Slots != null ? m_Background4Slots : m_Background3Slots);

                    if (targetTex != null && rawImg.texture != targetTex)
                    {
#if UNITY_EDITOR
                        UnityEditor.Undo.RecordObject(rawImg, "Switch Marina Background Texture");
#endif
                        rawImg.texture = targetTex;
#if UNITY_EDITOR
                        UnityEditor.EditorUtility.SetDirty(rawImg);
#endif
                    }
                }
            }
        }

        /// <summary>
        /// Engel olan kilitli/kapalı slot bariyerlerini tamamen devre dışı bırakır.
        /// </summary>
        public void DeactivateAllBarriers()
        {
            RemoveClosedBayVisuals();
        }

        /// <summary>Önceki sürümlerden kalmış kapalı slot (kilitli bölme) görsellerini gizler; yenisi oluşturulmaz.</summary>
        public void RemoveClosedBayVisuals()
        {
            var existingBarriers = GetComponentsInChildren<MarinaLockedSlotBarrier>(true);
            for (int b = 0; b < existingBarriers.Length; b++)
            {
                if (existingBarriers[b] != null && existingBarriers[b].gameObject.activeSelf)
                    existingBarriers[b].gameObject.SetActive(false);
            }

            for (int c = 0; c < transform.childCount; c++)
            {
                Transform child = transform.GetChild(c);
                if (child.name.StartsWith(ClosedBayPrefix) && child.gameObject.activeSelf)
                    child.gameObject.SetActive(false);
            }
        }

        private const string ClosedBayPrefix = "[Closed_Bay_";

        private void EnsureBackgroundTexturesInitialized()
        {
#if UNITY_EDITOR
            if (m_Background3Slots == null)
            {
                m_Background3Slots = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>("Assets/Renkli İskele ve Deniz Manzarası.png");
            }
            if (m_Background4Slots == null)
            {
                m_Background4Slots = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>("Assets/Renkli İskele ve Deniz Manzarası.png");
            }
            if (m_BackgroundAllBaysOpen == null)
            {
                m_BackgroundAllBaysOpen = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>("Assets/Renkli İskele ve Deniz Manzarası.png");
            }
#endif
        }
    }
}
