using System;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Gemi sahnesindeki su slotlarının (WaterSlot_1..5) boyutunu, aralarındaki mesafeyi,
    /// sahil kavisini (arc curve) ve marina yanaşma açısını canlı (live) olarak ayarlamayı sağlar.
    /// 2. görseldeki geniş, orantılı ve sahil koyuna uyumlu kavisli can simidi düzenini tam yansıtır.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Marina Slot Layout")]
    public class MarinaSlotLayout : MonoBehaviour
    {
        [Header("⚓ Slot Boyutları (Width & Length / Height)")]
        [Tooltip("Slotların yatay genişliği (Width / En - X ekseni).")]
        [Range(0.3f, 3.5f)]
        [SerializeField] private float m_SlotWidth = 1.08f;

        [Tooltip("Slotların boyu / uzunluğu (Length / Height - Z ekseni).")]
        [Range(0.3f, 3.5f)]
        [SerializeField] private float m_SlotLength = 1.55f;

        // Geriye dönük uyumluluk için
        [SerializeField, HideInInspector] private float m_SlotScale = 1.08f;

        [Header("📏 Slotlar Arası Mesafe (Aralık)")]
        [Tooltip("Slotların birbirine olan yatay mesafesi.")]
        [Range(0.6f, 2.5f)]
        [SerializeField] private float m_SlotSpacing = 1.48f;

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
        [SerializeField] private float m_ArcCurveY = 0f;

        [Tooltip("Kavisin sol/sağ asimetrisi.")]
        [Range(-0.2f, 0.2f)]
        [SerializeField] private float m_ArcAsymmetry = 0f;

        [Tooltip("Slotların kavis yönüne göre yelpaze açısı.")]
        [Range(-10f, 10f)]
        [SerializeField] private float m_ArcAngleFan = 0f;

        public float SlotWidth
        {
            get => m_SlotWidth;
            set { m_SlotWidth = value; ApplyLayout(); }
        }

        public float SlotLength
        {
            get => m_SlotLength;
            set { m_SlotLength = value; ApplyLayout(); }
        }

        public float SlotScale
        {
            get => (m_SlotWidth + m_SlotLength) * 0.5f;
            set { m_SlotWidth = value; m_SlotLength = value; m_SlotScale = value; ApplyLayout(); }
        }

        public float SlotSpacing
        {
            get => m_SlotSpacing;
            set { m_SlotSpacing = value; ApplyLayout(); }
        }

        public float SlotAngle
        {
            get => m_SlotAngle;
            set { m_SlotAngle = value; ApplyLayout(); }
        }

        public float WaterTiltX
        {
            get => m_WaterTiltX;
            set { m_WaterTiltX = value; ApplyLayout(); }
        }

        public float OffsetY
        {
            get => m_OffsetY;
            set { m_OffsetY = value; ApplyLayout(); }
        }

        public float OffsetZ
        {
            get => m_OffsetZ;
            set { m_OffsetZ = value; ApplyLayout(); }
        }

        public float ArcCurveY
        {
            get => m_ArcCurveY;
            set { m_ArcCurveY = value; ApplyLayout(); }
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

        [Header("⚓ Aktif Slot Sayısı")]
        [Tooltip("Sahnedeki aktif yanaşma slotu sayısı (1 - 8). Seviye verisindeki SlotCount ile otomatik senkronize olur.")]
        [Range(1, 8)]
        [SerializeField] private int m_SlotCount = 5;

        public int SlotCount
        {
            get => m_SlotCount;
            set => SetSlotCount(value);
        }

        private void Awake()
        {
            SyncWithLevel();
        }

        private void OnEnable()
        {
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

            var allSlots = new System.Collections.Generic.List<ShipSlot>(GetComponentsInChildren<ShipSlot>(true));
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
            if (m_SlotWidth <= 0.001f) m_SlotWidth = 1.08f;
            if (m_SlotLength <= 0.001f) m_SlotLength = 1.55f;
            ApplyLayout();
        }

        private void Reset()
        {
            m_SlotCount = 5;
            m_SlotWidth = 1.08f;
            m_SlotLength = 1.55f;
            m_SlotScale = 1.08f;
            m_SlotSpacing = 1.48f;
            m_SlotAngle = 0f;
            m_WaterTiltX = -28f;
            m_OffsetY = -2.20f;
            m_OffsetZ = 0.05f;
            m_ArcCurveY = 0f;
            m_ArcAsymmetry = 0f;
            m_ArcAngleFan = 0f;
            ApplyLayout();
        }

        /// <summary>
        /// Tüm aktif çocuk slot nesnelerini 2. görsel oran ve kavis değerlerine göre anında yeniden hizalar ve ölçekler.
        /// </summary>
        [ContextMenu("Slotları Yeniden Hizala (Apply Layout)")]
        public void ApplyLayout()
        {
            transform.localPosition = new Vector3(transform.localPosition.x, m_OffsetY, m_OffsetZ);

            var allSlots = GetComponentsInChildren<ShipSlot>(true);
            if (allSlots == null || allSlots.Length == 0) return;

            // Sadece aktif slotları filtrele ve sırala
            var activeSlots = new System.Collections.Generic.List<ShipSlot>();
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

            float startX = -(count - 1) * m_SlotSpacing * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var slot = activeSlots[i];
                Transform tr = slot.transform;
                float posX = startX + i * m_SlotSpacing;
                float t = count > 1 ? (i - (count - 1) * 0.5f) : 0f;
                float posY = m_ArcCurveY * (t * t) + m_ArcAsymmetry * t;
                float angle = m_SlotAngle + m_ArcAngleFan * t;

                tr.localPosition = new Vector3(posX, posY, 0f);
                tr.localRotation = Quaternion.Euler(m_WaterTiltX, 0f, 0f) * Quaternion.Euler(0f, angle, 0f);
                tr.localScale = new Vector3(m_SlotWidth, 1f, m_SlotLength);

                // Can simidi slot görselini ([Slot_Lifebuoy]) öncelikli kullan
                Transform foamSlot = tr.Find("FoamSlot");
                Transform lifebuoy = tr.Find("[Slot_Lifebuoy]");
                Transform visualTr = lifebuoy != null ? lifebuoy : foamSlot;

                if (visualTr != null)
                {
                    visualTr.localPosition = new Vector3(0f, 0.025f, 0f);
                    visualTr.localRotation = Quaternion.identity;

                    // Can simidinin ekranda dolgun, dairesel ve izometrik derinlikli durması için perspektif ve scale kompanzasyonu
                    Vector3 circleComp = Vector3.one;
                    if (visualTr == lifebuoy)
                    {
                        float tiltFactor = Mathf.Sin(Mathf.Abs(m_WaterTiltX) * Mathf.Deg2Rad);
                        if (tiltFactor < 0.05f) tiltFactor = 0.47f;
                        // Kullanıcı referans görselindeki dolgunluk ve 0.90 dairesel en/boy oranı
                        float targetScreenAspect = 0.90f;
                        float zComp = (targetScreenAspect / tiltFactor) * (m_SlotWidth / Mathf.Max(0.001f, m_SlotLength));
                        // 1.35f çarpanı ile simitlerin su alanını doldurması ve gemileri rahatça kucaklaması sağlanır
                        circleComp = new Vector3(1.35f, 1f, 1.35f * zComp);
                    }
                    visualTr.localScale = circleComp;

                    if (!visualTr.gameObject.activeSelf)
                    {
                        visualTr.gameObject.SetActive(true);
                    }

                    // Köpük slot su salınım animasyonunu ekle / senkronize et
                    FoamSlotBobbing bobbing = visualTr.GetComponent<FoamSlotBobbing>();
                    if (bobbing == null)
                    {
                        bobbing = visualTr.gameObject.AddComponent<FoamSlotBobbing>();
                    }
                    bobbing.PhaseOffset = i * 0.75f;
                    bobbing.SetBasePosition(new Vector3(0f, 0.025f, 0f));
                    bobbing.SetBaseScale(circleComp);
                }

                // Can simidi aktifken eski düz FoamSlot kapatılsın
                if (lifebuoy != null && foamSlot != null)
                {
                    foamSlot.gameObject.SetActive(false);
                }
            }
        }
    }
}
