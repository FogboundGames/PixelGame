using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Su alanındaki 5 adet yanaşma slotundan (dock) birini temsil eder.
    /// indicator-square-b taban çerçevesini ve üzerine yanaşan ShipController nesnesini yönetir.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Ship Slot")]
    public class ShipSlot : MonoBehaviour
    {
        [Header("⚓ Slot Bilgisi")]
        [SerializeField] private int m_SlotIndex = 0;
        [SerializeField] private ShipController m_DockedShip;
        [SerializeField] private Transform m_IndicatorTransform;

        [Header("⚓ Slot Görsel Elemanları")]
        [SerializeField] private MeshRenderer m_BerthRenderer;
        [SerializeField] private MeshRenderer m_DockedShadowRenderer;
        [SerializeField] private bool m_EnableTargetGlow = true;

        private MaterialPropertyBlock m_BerthPropBlock;
        private MaterialPropertyBlock m_ShadowPropBlock;
        private float m_DockProgress = 0f; // 0 = empty (dashes + glow), 1 = docked (hull shadow + wake)

        private static readonly System.Collections.Generic.List<ShipSlot> s_ActiveSlots = new System.Collections.Generic.List<ShipSlot>(8);
        public static System.Collections.Generic.IReadOnlyList<ShipSlot> ActiveSlots => s_ActiveSlots;

        public int SlotIndex { get => m_SlotIndex; set => m_SlotIndex = value; }
        public ShipController DockedShip => m_DockedShip;
        public bool IsEmpty => m_DockedShip == null;
        public Transform IndicatorTransform => m_IndicatorTransform;
        public MeshRenderer BerthRenderer => m_BerthRenderer;
        public MeshRenderer DockedShadowRenderer => m_DockedShadowRenderer;

        private void OnEnable()
        {
            if (!s_ActiveSlots.Contains(this)) s_ActiveSlots.Add(this);
            EnsureIndicatorReference();
            UpdateVisualState(true);
        }

        private void OnDisable()
        {
            s_ActiveSlots.Remove(this);
        }

        private void Awake()
        {
            EnsureIndicatorReference();
            UpdateVisualState(true);
        }

        private void Update()
        {
            UpdateVisualState(false);
        }

        public void EnsureIndicatorReference()
        {
            if (m_IndicatorTransform == null || !m_IndicatorTransform.gameObject.activeSelf)
            {
                m_IndicatorTransform = transform.Find("[Slot_RoundedSquare]");
                if (m_IndicatorTransform == null)
                {
                    m_IndicatorTransform = transform.Find("[Slot_Berth]");
                }
                if (m_IndicatorTransform == null)
                {
                    m_IndicatorTransform = transform.Find("[Slot_Lifebuoy]");
                }
                if (m_IndicatorTransform == null)
                {
                    m_IndicatorTransform = transform.Find("FoamSlot");
                }
                if (m_IndicatorTransform == null)
                {
                    m_IndicatorTransform = transform.Find("IndicatorMesh");
                }
            }

            if (m_BerthRenderer == null && m_IndicatorTransform != null)
            {
                m_BerthRenderer = m_IndicatorTransform.GetComponent<MeshRenderer>();
            }

            Transform dsTr = transform.Find("[Slot_DockedShadow]");
            if (dsTr != null)
            {
                dsTr.gameObject.SetActive(false);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorApplication.delayCall += () =>
                    {
                        if (dsTr != null && dsTr.gameObject != null)
                        {
                            DestroyImmediate(dsTr.gameObject);
                        }
                    };
                }
                else
#endif
                {
                    Destroy(dsTr.gameObject);
                }
                m_DockedShadowRenderer = null;
            }
            else
            {
                m_DockedShadowRenderer = null;
            }
        }

        private void UpdateVisualState(bool immediate)
        {
            EnsureIndicatorReference();

            // Gemi slota tamamen oturana kadar (IsDocked) boş slot görseli kaybolmaz;
            // Öncesinde slota gidiş esnasında kararma/siyahlık oluşmaz.
            bool isShipLanded = m_DockedShip != null && m_DockedShip.IsDocked;
            float targetProgress = isShipLanded ? 1f : 0f;
            if (immediate || !Application.isPlaying)
            {
                m_DockProgress = targetProgress;
            }
            else
            {
                m_DockProgress = Mathf.MoveTowards(m_DockProgress, targetProgress, Time.deltaTime * 5.5f);
            }

            var layout = GetComponentInParent<MarinaSlotLayout>();
            if (layout != null && layout.HideSlotIndicators)
            {
                if (m_BerthRenderer != null && m_BerthRenderer.enabled) m_BerthRenderer.enabled = false;
                if (m_DockedShadowRenderer != null && m_DockedShadowRenderer.enabled) m_DockedShadowRenderer.enabled = false;
                return;
            }

            // 1. Boş Slot Kesikli Çizgi ve Hedef Parıltısı (Empty Berth + Target Glow)
            if (m_BerthRenderer != null)
            {
                float emptyAlpha = 1f - m_DockProgress;
                if (emptyAlpha > 0.005f)
                {
                    if (!m_BerthRenderer.enabled) m_BerthRenderer.enabled = true;

                    // Boşken hedef parıltısı hafifçe nefes alır (subtle pulsing target glow)
                    float pulse = 1f;
                    if (m_EnableTargetGlow && Application.isPlaying && emptyAlpha > 0.8f)
                    {
                        pulse = 0.86f + 0.14f * Mathf.Sin(Time.time * 2.8f + m_SlotIndex * 0.75f);
                    }

                    if (m_BerthPropBlock == null) m_BerthPropBlock = new MaterialPropertyBlock();
                    m_BerthRenderer.GetPropertyBlock(m_BerthPropBlock);
                    m_BerthPropBlock.SetColor("_Color", new Color(1f, 1f, 1f, emptyAlpha * pulse));
                    m_BerthRenderer.SetPropertyBlock(m_BerthPropBlock);
                }
                else
                {
                    if (m_BerthRenderer.enabled) m_BerthRenderer.enabled = false;
                }
            }

            // 2. Slottaki siyah temas gölgesi kaldırıldı (Gemi kendi silüet gölgesini kullanır)
            if (m_DockedShadowRenderer != null)
            {
                m_DockedShadowRenderer.enabled = false;
            }
        }

        /// <summary>
        /// Gemiyi bu slota bağlar.
        /// </summary>
        public void DockShip(ShipController ship)
        {
            m_DockedShip = ship;
            if (ship != null)
            {
                // Not: ship.IsDocked burada hemen true yapılmaz; gemi seyir animasyonunu
                // tamamlayıp slota tam oturduğunda ShipController içinde IsDocked = true yapılır.
                ship.EnsureSilhouetteShadow();
            }
            UpdateVisualState(false);
        }

        /// <summary>
        /// Slottaki gemiyi serbest bırakır.
        /// </summary>
        public void ReleaseShip()
        {
            if (m_DockedShip != null)
            {
                m_DockedShip.IsDocked = false;
                m_DockedShip.EnsureSilhouetteShadow();
            }
            m_DockedShip = null;
            UpdateVisualState(false);

            // Gemi ayrıldığında can simidinin hafifçe yukarı yaylanması (kurtulma kaldırma kuvveti)
            TriggerWaterDipImpact(-0.05f, 0.35f);
        }

        /// <summary>
        /// Gemi slota yanaştığında can simidinin ve suyun hafifçe suya batıp yaylanmasını sağlar.
        /// </summary>
        public void TriggerWaterDipImpact(float depth = 0.16f, float duration = 0.52f)
        {
            EnsureIndicatorReference();

            if (m_IndicatorTransform != null)
            {
                FoamSlotBobbing bobbing = m_IndicatorTransform.GetComponent<FoamSlotBobbing>();
                if (bobbing != null)
                {
                    bobbing.TriggerWaterDipImpact(depth, duration);
                }
            }
        }
    }
}
