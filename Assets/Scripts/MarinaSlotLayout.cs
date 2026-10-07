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

        [Header("🫧 Slot Görseli (Köpük Halkası)")]
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

        [Header("🪵 Kavisli İskele (Curved Marina Pier Visual)")]
        [Tooltip("Kullanıcının referans görselindeki kavisli ahşap iskele modelini (3, 4 ve 5 slot seçenekli) aktif eder.")]
        [SerializeField] private bool m_EnableCurvedPier = true;
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
        [Tooltip("Slot şeridinin Y eksenindeki yüksekliği (Kumsal kıyısına yakınlık: -2.20f).")]
        [Range(-5f, 5f)]
        [SerializeField] private float m_OffsetY = -2.20f;

        [Tooltip("Slot şeridinin Z eksenindeki derinliği.")]
        [Range(-3f, 3f)]
        [SerializeField] private float m_OffsetZ = 0.05f;

        [Header("🌊 Sahil Kavis / Yay Eğrisi (Shoreline Arc)")]
        [Tooltip("Slotların sahil koyu kıyısına uyumlu yay/kavis yapması için Y ekseni eğrilik gücü (0 = Düz sıra).")]
        [Range(-0.3f, 0.3f)]
        [SerializeField] private float m_ArcCurveY = 0.042f;

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
            get => m_UsePerCountSettings ? GetConfig(m_SlotCount).PierOffsetY : m_PierOffsetY;
            set
            {
                m_PierOffsetY = value;
                if (m_UsePerCountSettings) GetConfig(m_SlotCount).PierOffsetY = value;
                ApplyLayout();
            }
        }

        public float PierOffsetZ
        {
            get => m_UsePerCountSettings ? GetConfig(m_SlotCount).PierOffsetZ : m_PierOffsetZ;
            set
            {
                m_PierOffsetZ = value;
                if (m_UsePerCountSettings) GetConfig(m_SlotCount).PierOffsetZ = value;
                ApplyLayout();
            }
        }

        public float PierScaleMultiplier
        {
            get => m_UsePerCountSettings ? GetConfig(m_SlotCount).PierScaleMultiplier : m_PierScaleMultiplier;
            set
            {
                m_PierScaleMultiplier = value;
                if (m_UsePerCountSettings) GetConfig(m_SlotCount).PierScaleMultiplier = value;
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
            get => m_UsePerCountSettings ? GetConfig(m_SlotCount).BaySlotOffsetY : m_BaySlotOffsetY;
            set
            {
                m_BaySlotOffsetY = value;
                if (m_UsePerCountSettings) GetConfig(m_SlotCount).BaySlotOffsetY = value;
                ApplyLayout();
            }
        }

        public float SlotWidth
        {
            get => m_UsePerCountSettings ? GetConfig(m_SlotCount).SlotWidth : m_SlotWidth;
            set
            {
                m_SlotWidth = value;
                if (m_UsePerCountSettings) GetConfig(m_SlotCount).SlotWidth = value;
                ApplyLayout();
            }
        }

        public float SlotLength
        {
            get => m_UsePerCountSettings ? GetConfig(m_SlotCount).SlotLength : m_SlotLength;
            set
            {
                m_SlotLength = value;
                if (m_UsePerCountSettings) GetConfig(m_SlotCount).SlotLength = value;
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
            get => m_UsePerCountSettings ? GetConfig(m_SlotCount).LegacySpacing : m_SlotSpacing;
            set
            {
                m_SlotSpacing = value;
                if (m_UsePerCountSettings) GetConfig(m_SlotCount).LegacySpacing = value;
                ApplyLayout();
            }
        }

        public float SlotAngle
        {
            get => m_UsePerCountSettings ? GetConfig(m_SlotCount).SlotAngle : m_SlotAngle;
            set
            {
                m_SlotAngle = value;
                if (m_UsePerCountSettings) GetConfig(m_SlotCount).SlotAngle = value;
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
            get => m_UsePerCountSettings ? GetConfig(m_SlotCount).RowOffsetY : m_OffsetY;
            set
            {
                m_OffsetY = value;
                if (m_UsePerCountSettings) GetConfig(m_SlotCount).RowOffsetY = value;
                ApplyLayout();
            }
        }

        public float OffsetZ
        {
            get => m_UsePerCountSettings ? GetConfig(m_SlotCount).RowOffsetZ : m_OffsetZ;
            set
            {
                m_OffsetZ = value;
                if (m_UsePerCountSettings) GetConfig(m_SlotCount).RowOffsetZ = value;
                ApplyLayout();
            }
        }

        public float ArcCurveY
        {
            get => m_UsePerCountSettings ? GetConfig(m_SlotCount).ArcCurveY : m_ArcCurveY;
            set
            {
                m_ArcCurveY = value;
                if (m_UsePerCountSettings) GetConfig(m_SlotCount).ArcCurveY = value;
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
                    RowOffsetY = -2.26f, // Kumsal koyuna tam oturan Y yüksekliği
                    RowOffsetZ = 0.05f,
                    PierWidth = m_PierWidth3Slots > 0.01f ? m_PierWidth3Slots : 7.2f,
                    PierOffsetY = 2.06f,
                    PierOffsetZ = 0.04f,
                    PierScaleMultiplier = m_PierScaleMultiplier > 0.01f ? m_PierScaleMultiplier : 1.07f,
                    BaySpacingPx = 171f,
                    BaySlotOffsetY = 0f,
                    ArcCurveY = 0.042f,
                    SlotAngle = 0f,
                    SlotWidth = m_SlotWidth > 0.01f ? m_SlotWidth : 1.05f,
                    SlotLength = m_SlotLength > 0.01f ? m_SlotLength : 1.55f,
                    LegacySpacing = 1.68f,
                    PierMaterial = m_PierMaterial3Slots
                };
            }

            // 4 Slot Ayarları
            if (m_Config4Slots == null || m_Config4Slots.SlotCount != 4)
            {
                m_Config4Slots = new MarinaSlotCountConfig(4)
                {
                    RowOffsetY = -2.23f, // 4 slot koy kavis oturumu
                    RowOffsetZ = 0.05f,
                    PierWidth = m_PierWidth4Slots > 0.01f ? m_PierWidth4Slots : 8.1f,
                    PierOffsetY = 2.06f,
                    PierOffsetZ = 0.04f,
                    PierScaleMultiplier = m_PierScaleMultiplier > 0.01f ? m_PierScaleMultiplier : 1.07f,
                    BaySpacingPx = 173f,
                    BaySlotOffsetY = 0f,
                    ArcCurveY = 0.042f,
                    SlotAngle = 0f,
                    SlotWidth = m_SlotWidth > 0.01f ? m_SlotWidth : 1.05f,
                    SlotLength = m_SlotLength > 0.01f ? m_SlotLength : 1.55f,
                    LegacySpacing = 1.68f,
                    PierMaterial = m_PierMaterial4Slots
                };
            }

            // 5 Slot Ayarları
            if (m_Config5Slots == null || m_Config5Slots.SlotCount != 5)
            {
                m_Config5Slots = new MarinaSlotCountConfig(5)
                {
                    RowOffsetY = -2.20f, // 5 slot standart sahil çizgisi
                    RowOffsetZ = 0.05f,
                    PierWidth = m_PierWidth5Slots > 0.01f ? m_PierWidth5Slots : 8.55f,
                    PierOffsetY = 2.06f,
                    PierOffsetZ = 0.04f,
                    PierScaleMultiplier = m_PierScaleMultiplier > 0.01f ? m_PierScaleMultiplier : 1.07f,
                    BaySpacingPx = 174f,
                    BaySlotOffsetY = 0f,
                    ArcCurveY = 0.042f,
                    SlotAngle = 0f,
                    SlotWidth = m_SlotWidth > 0.01f ? m_SlotWidth : 1.05f,
                    SlotLength = m_SlotLength > 0.01f ? m_SlotLength : 1.55f,
                    LegacySpacing = 1.528f,
                    PierMaterial = m_PierMaterial5Slots
                };
            }

#if UNITY_EDITOR
            if (m_Config3Slots.PierMaterial == null)
                m_Config3Slots.PierMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Marina/Pier_Curved_3Slots_Mat.mat");
            if (m_Config4Slots.PierMaterial == null)
                m_Config4Slots.PierMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Marina/Pier_Curved_4Slots_Mat.mat");
            if (m_Config5Slots.PierMaterial == null)
                m_Config5Slots.PierMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Marina/Pier_Curved_5Slots_Mat.mat");
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
                    RowOffsetY = -2.26f,
                    RowOffsetZ = 0.05f,
                    PierWidth = 7.2f,
                    PierOffsetY = 2.06f,
                    PierOffsetZ = 0.04f,
                    PierScaleMultiplier = 1.07f,
                    BaySpacingPx = 171f,
                    BaySlotOffsetY = 0f,
                    ArcCurveY = 0.042f,
                    SlotAngle = 0f,
                    SlotWidth = 1.05f,
                    SlotLength = 1.55f,
                    LegacySpacing = 1.68f,
                    PierMaterial = m_PierMaterial3Slots
                };
            }

            if (count == 0 || count == 4)
            {
                m_Config4Slots = new MarinaSlotCountConfig(4)
                {
                    RowOffsetY = -2.23f,
                    RowOffsetZ = 0.05f,
                    PierWidth = 8.1f,
                    PierOffsetY = 2.06f,
                    PierOffsetZ = 0.04f,
                    PierScaleMultiplier = 1.07f,
                    BaySpacingPx = 173f,
                    BaySlotOffsetY = 0f,
                    ArcCurveY = 0.042f,
                    SlotAngle = 0f,
                    SlotWidth = 1.05f,
                    SlotLength = 1.55f,
                    LegacySpacing = 1.68f,
                    PierMaterial = m_PierMaterial4Slots
                };
            }

            if (count == 0 || count == 5)
            {
                m_Config5Slots = new MarinaSlotCountConfig(5)
                {
                    RowOffsetY = -2.20f,
                    RowOffsetZ = 0.05f,
                    PierWidth = 8.55f,
                    PierOffsetY = 2.06f,
                    PierOffsetZ = 0.04f,
                    PierScaleMultiplier = 1.07f,
                    BaySpacingPx = 174f,
                    BaySlotOffsetY = 0f,
                    ArcCurveY = 0.042f,
                    SlotAngle = 0f,
                    SlotWidth = 1.05f,
                    SlotLength = 1.55f,
                    LegacySpacing = 1.528f,
                    PierMaterial = m_PierMaterial5Slots
                };
            }

            EnsureConfigsInitialized();
            ApplyLayout();
        }

        private void Awake()
        {
            EnsureConfigsInitialized();
            SyncWithLevel();
        }

        private void OnEnable()
        {
            EnsureConfigsInitialized();
            PixelArtGenerator.LevelLoaded -= OnLevelLoaded;
            PixelArtGenerator.LevelLoaded += OnLevelLoaded;
        }

        private void OnDisable()
        {
            PixelArtGenerator.LevelLoaded -= OnLevelLoaded;
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

            MarinaSlotCountConfig cfg = m_UsePerCountSettings ? GetConfig(count) : null;
            float rowOffsetY = cfg != null ? cfg.RowOffsetY : m_OffsetY;
            float rowOffsetZ = cfg != null ? cfg.RowOffsetZ : m_OffsetZ;

            transform.localPosition = new Vector3(transform.localPosition.x, rowOffsetY, rowOffsetZ);

            if (m_EnableCurvedPier)
            {
                ApplyCurvedPierLayout(activeSlots, count, cfg);
            }
            else
            {
                ApplyLegacyDockLayout(activeSlots, count, cfg);
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

            // Sıra ekrana sığmıyorsa aralık ve slot boyutu birlikte küçülür
            float fit = 1f;
            Camera fitCam = Camera.main;
            if (m_FitToScreenWidth && fitCam != null && fitCam.orthographic)
            {
                float available = 2f * fitCam.orthographicSize * fitCam.aspect - 2f * m_ScreenEdgeMargin;
                float needed = (count - 1) * rawSpacing + slotWidth * m_BuoyVisualWidthRatio;
                if (needed > available && needed > 0.001f) fit = Mathf.Max(0.5f, available / needed);
            }
            float spacing = rawSpacing * fit;
            float startX = -(count - 1) * spacing * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var slot = activeSlots[i];
                Transform tr = slot.transform;
                float posX = startX + i * spacing;
                float t = count > 1 ? (i - (count - 1) * 0.5f) : 0f;
                float posY = arcCurveY * (t * t) + m_ArcAsymmetry * t;
                float angle = slotAngle + m_ArcAngleFan * t;

                tr.localPosition = new Vector3(posX, posY, 0f);
                tr.localRotation = Quaternion.Euler(m_WaterTiltX, 0f, 0f) * Quaternion.Euler(0f, angle, 0f);
                tr.localScale = new Vector3(slotWidth * fit, 1f, slotLength * fit);

                Transform foamSlot = tr.Find("FoamSlot");
                Transform lifebuoy = tr.Find("[Slot_Lifebuoy]");
                Transform visualTr = lifebuoy != null ? lifebuoy : foamSlot;

                if (visualTr != null)
                {
                    Vector3 visualBasePos = new Vector3(0f, 0.025f, visualTr == lifebuoy ? m_SlotVisualOffsetZ : 0f);
                    visualTr.localPosition = visualBasePos;

                    if (visualTr == lifebuoy && m_SlotVisualMaterial != null)
                    {
                        MeshRenderer visualRenderer = visualTr.GetComponent<MeshRenderer>();
                        if (visualRenderer != null)
                        {
                            bool isLast = i == count - 1;
                            Material target = (!isLast && m_SlotVisualMaterialJoined != null) ? m_SlotVisualMaterialJoined : m_SlotVisualMaterial;
                            if (visualRenderer.sharedMaterial != target) visualRenderer.sharedMaterial = target;
                            visualRenderer.sortingOrder = i;
                        }
                    }
                    visualTr.localRotation = Quaternion.identity;

                    Vector3 circleComp = Vector3.one;
                    if (visualTr == lifebuoy)
                    {
                        Camera cam = Camera.main;
                        float camPitch = cam != null ? Mathf.DeltaAngle(0f, cam.transform.eulerAngles.x) : 0f;
                        float tiltFactor = Mathf.Sin(Mathf.Abs(m_WaterTiltX - camPitch) * Mathf.Deg2Rad);
                        if (tiltFactor < 0.05f) tiltFactor = 0.47f;
                        float zComp = (m_SlotVisualScreenAspect / tiltFactor) * (slotWidth / Mathf.Max(0.001f, slotLength));
                        circleComp = new Vector3(m_SlotVisualScale, 1f, m_SlotVisualScale * zComp);
                    }
                    visualTr.localScale = circleComp;

                    if (!visualTr.gameObject.activeSelf)
                    {
                        visualTr.gameObject.SetActive(true);
                    }

                    FoamSlotBobbing bobbing = visualTr.GetComponent<FoamSlotBobbing>();
                    if (bobbing == null)
                    {
                        bobbing = visualTr.gameObject.AddComponent<FoamSlotBobbing>();
                    }
                    bobbing.PhaseOffset = i * 0.75f;
                    bobbing.SetBasePosition(visualBasePos);
                    bobbing.SetBaseScale(circleComp);
                }

                if (lifebuoy != null && foamSlot != null)
                {
                    foamSlot.gameObject.SetActive(false);
                }
            }
        }
    }
}
