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
        [SerializeField] private float m_SlotWidth = 1.27f;

        [Tooltip("Slotların boyu / uzunluğu (Length / Height - Z ekseni).")]
        [Range(0.3f, 3.5f)]
        [SerializeField] private float m_SlotLength = 1.82f;

        // Geriye dönük uyumluluk için
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
        [Tooltip("Kullanıcının referans görselindeki kavisli ahşap iskele modelini (4 ve 5 slot seçenekli) aktif eder.")]
        [SerializeField] private bool m_EnableCurvedPier = true;
        [SerializeField] private Material m_PierMaterial4Slots;
        [SerializeField] private Material m_PierMaterial5Slots;
        [SerializeField] private Material m_PierMaterial3Slots;
        [SerializeField] private float m_PierWidth4Slots = 8.1f;
        [SerializeField] private float m_PierWidth5Slots = 8.55f;
        [SerializeField] private float m_PierWidth3Slots = 7.2f;
        [SerializeField] private float m_PierOffsetY = 2.06f;
        [SerializeField] private float m_PierOffsetZ = 0.04f;
        [SerializeField] private float m_PierScaleMultiplier = 1.0f;
        [SerializeField] private float m_PierRotationX = -28f;
        [SerializeField] private float m_BaySlotOffsetY = 0f;

        public bool EnableCurvedPier
        {
            get => m_EnableCurvedPier;
            set { m_EnableCurvedPier = value; ApplyLayout(); }
        }

        public float PierWidth4Slots
        {
            get => m_PierWidth4Slots;
            set { m_PierWidth4Slots = value; ApplyLayout(); }
        }

        public float PierWidth5Slots
        {
            get => m_PierWidth5Slots;
            set { m_PierWidth5Slots = value; ApplyLayout(); }
        }

        public float PierWidth3Slots
        {
            get => m_PierWidth3Slots;
            set { m_PierWidth3Slots = value; ApplyLayout(); }
        }

        public float PierOffsetY
        {
            get => m_PierOffsetY;
            set { m_PierOffsetY = value; ApplyLayout(); }
        }

        public float PierOffsetZ
        {
            get => m_PierOffsetZ;
            set { m_PierOffsetZ = value; ApplyLayout(); }
        }

        public float PierScaleMultiplier
        {
            get => m_PierScaleMultiplier;
            set { m_PierScaleMultiplier = value; ApplyLayout(); }
        }

        public float PierRotationX
        {
            get => m_PierRotationX;
            set { m_PierRotationX = value; ApplyLayout(); }
        }

        public float BaySlotOffsetY
        {
            get => m_BaySlotOffsetY;
            set { m_BaySlotOffsetY = value; ApplyLayout(); }
        }

        public void SetCurvedPierMaterials(Material mat4, Material mat5, Material mat3)
        {
            m_PierMaterial4Slots = mat4;
            m_PierMaterial5Slots = mat5;
            m_PierMaterial3Slots = mat3;
        }

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
            m_SlotWidth = 1.27f;
            m_SlotLength = 1.82f;
            m_SlotScale = 1.27f;
            m_SlotSpacing = 1.68f;
            m_SlotAngle = 0f;
            m_WaterTiltX = -28f;
            m_OffsetY = -2.20f;
            m_OffsetZ = 0.05f;
            m_ArcCurveY = 0f;
            m_ArcAsymmetry = 0f;
            m_ArcAngleFan = 0f;
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

            if (m_EnableCurvedPier)
            {
                ApplyCurvedPierLayout(activeSlots, count);
            }
            else
            {
                ApplyLegacyDockLayout(activeSlots, count);
            }
        }

        private void ApplyCurvedPierLayout(System.Collections.Generic.List<ShipSlot> activeSlots, int count)
        {
            // 1. Slot sayısına göre uygun materyal, genişlik ve doku oranını seç
            Material targetMat = null;
            float pierWidth = m_PierWidth5Slots;
            float texWidth = 1422f;
            float texHeight = 634f;
            float baySpacingPx = 174f;

            if (count == 4)
            {
                targetMat = m_PierMaterial4Slots;
                pierWidth = m_PierWidth4Slots;
                texWidth = 1246f;
            }
            else if (count == 5)
            {
                targetMat = m_PierMaterial5Slots;
                pierWidth = m_PierWidth5Slots;
                texWidth = 1422f;
            }
            else if (count <= 3)
            {
                targetMat = m_PierMaterial3Slots;
                pierWidth = m_PierWidth3Slots;
                texWidth = 1070f;
            }
            else
            {
                targetMat = m_PierMaterial5Slots;
                pierWidth = m_PierWidth5Slots;
                texWidth = 1422f;
            }

#if UNITY_EDITOR
            if (targetMat == null)
            {
                string matName = count == 4 ? "Pier_Curved_4Slots_Mat" : (count <= 3 ? "Pier_Curved_3Slots_Mat" : "Pier_Curved_5Slots_Mat");
                targetMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/Marina/{matName}.mat");
            }
#endif

            float aspect = texWidth / texHeight;
            float finalWidth = pierWidth * m_PierScaleMultiplier;
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

            pierTr.localPosition = new Vector3(0f, m_PierOffsetY, m_PierOffsetZ);
            pierTr.localRotation = Quaternion.Euler(m_PierRotationX, 0f, 0f);
            pierTr.localScale = new Vector3(finalWidth, finalHeight, 1f);

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

            // 3. Slotları kavisli iskelenin koylarına (bays) matematiksel olarak tam oturt
            float worldBaySpacing = (baySpacingPx / texWidth) * finalWidth;
            float startX = -(count - 1) * worldBaySpacing * 0.5f;

            // Doku üzerinde koy su alanı: Ahşap yürüyüş yolunun tamamen dışında (Y > 494)
            // Kullanıcı isteği: "gemiler iskelelerin çok içine girerek yerleşiyor slota bunu istemiyorum biraz geride dursunlar istiyorum"
            // Gemiler ahşap yürüyüş yolunun içine girmesin, geride parmak iskelelerin arasında açık suda dursun.
            float baseBayLocalY = -0.52f * finalHeight + m_BaySlotOffsetY;

            for (int i = 0; i < count; i++)
            {
                var slot = activeSlots[i];
                Transform tr = slot.transform;
                float localX = startX + i * worldBaySpacing;
                float t = count > 1 ? (i - (count - 1) * 0.5f) : 0f;
                float localCurveY = m_ArcCurveY * (t * t) + m_ArcAsymmetry * t;
                float angle = m_SlotAngle + m_ArcAngleFan * t;

                // İskelenin kendi yerel düzleminde (Z=0) hesaplayıp pierTr rotasyonuyla (-40°) marina koordinatlarına dönüştür
                Vector3 pierLocalSlotPos = new Vector3(localX, baseBayLocalY + localCurveY, 0f);
                tr.localPosition = pierTr.localPosition + pierTr.localRotation * pierLocalSlotPos;

                // Kullanıcı isteği: Tekneler slota yerleşirken rotasyonları değişmesin, kuyrukla aynı kalsın
                ShipQueuePool queuePool = UnityEngine.Object.FindFirstObjectByType<ShipQueuePool>();
                Quaternion baseSlotRot = queuePool != null ? queuePool.transform.rotation : Quaternion.Euler(m_WaterTiltX, 0f, 0f);
                tr.rotation = baseSlotRot * Quaternion.Euler(0f, angle, 0f);
                tr.localScale = new Vector3(m_SlotWidth, 1f, m_SlotLength);

                // Eski düz parça görsellerini gizle (çünkü kavisli iskele tek parça ve koylar temiz su)
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

        private void ApplyLegacyDockLayout(System.Collections.Generic.List<ShipSlot> activeSlots, int count)
        {
            Transform pierTr = transform.Find("[Marina_Curved_Pier]");
            if (pierTr != null && pierTr.gameObject.activeSelf)
            {
                pierTr.gameObject.SetActive(false);
            }

            // Sıra ekrana sığmıyorsa aralık ve slot boyutu birlikte küçülür (oran korunur)
            float fit = 1f;
            Camera fitCam = Camera.main;
            if (m_FitToScreenWidth && fitCam != null && fitCam.orthographic)
            {
                float available = 2f * fitCam.orthographicSize * fitCam.aspect - 2f * m_ScreenEdgeMargin;
                float needed = (count - 1) * m_SlotSpacing + m_SlotWidth * m_BuoyVisualWidthRatio;
                if (needed > available && needed > 0.001f) fit = Mathf.Max(0.5f, available / needed);
            }
            float spacing = m_SlotSpacing * fit;

            float startX = -(count - 1) * spacing * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var slot = activeSlots[i];
                Transform tr = slot.transform;
                float posX = startX + i * spacing;
                float t = count > 1 ? (i - (count - 1) * 0.5f) : 0f;
                float posY = m_ArcCurveY * (t * t) + m_ArcAsymmetry * t;
                float angle = m_SlotAngle + m_ArcAngleFan * t;

                tr.localPosition = new Vector3(posX, posY, 0f);
                tr.localRotation = Quaternion.Euler(m_WaterTiltX, 0f, 0f) * Quaternion.Euler(0f, angle, 0f);
                tr.localScale = new Vector3(m_SlotWidth * fit, 1f, m_SlotLength * fit);

                // Can simidi slot görselini ([Slot_Lifebuoy]) öncelikli kullan
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
                            // Sağdaki slot soldakinin üstüne çizilsin: birleşme yerindeki kol/kazık hep üstte kalır
                            visualRenderer.sortingOrder = i;
                        }
                    }
                    visualTr.localRotation = Quaternion.identity;

                    // Can simidinin ekranda dolgun, dairesel ve izometrik derinlikli durması için perspektif ve scale kompanzasyonu
                    Vector3 circleComp = Vector3.one;
                    if (visualTr == lifebuoy)
                    {
                        Camera cam = Camera.main;
                        float camPitch = cam != null ? Mathf.DeltaAngle(0f, cam.transform.eulerAngles.x) : 0f;
                        float tiltFactor = Mathf.Sin(Mathf.Abs(m_WaterTiltX - camPitch) * Mathf.Deg2Rad);
                        if (tiltFactor < 0.05f) tiltFactor = 0.47f;
                        float zComp = (m_SlotVisualScreenAspect / tiltFactor) * (m_SlotWidth / Mathf.Max(0.001f, m_SlotLength));
                        circleComp = new Vector3(m_SlotVisualScale, 1f, m_SlotVisualScale * zComp);
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
                    bobbing.SetBasePosition(visualBasePos);
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
