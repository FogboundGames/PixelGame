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

        private static readonly System.Collections.Generic.List<ShipSlot> s_ActiveSlots = new System.Collections.Generic.List<ShipSlot>(8);
        public static System.Collections.Generic.IReadOnlyList<ShipSlot> ActiveSlots => s_ActiveSlots;

        public int SlotIndex { get => m_SlotIndex; set => m_SlotIndex = value; }
        public ShipController DockedShip => m_DockedShip;
        public bool IsEmpty => m_DockedShip == null;
        public Transform IndicatorTransform => m_IndicatorTransform;

        private void OnEnable()
        {
            if (!s_ActiveSlots.Contains(this)) s_ActiveSlots.Add(this);
        }

        private void OnDisable()
        {
            s_ActiveSlots.Remove(this);
        }

        private void Awake()
        {
            EnsureIndicatorReference();
        }

        private void EnsureIndicatorReference()
        {
            if (m_IndicatorTransform == null || !m_IndicatorTransform.gameObject.activeSelf)
            {
                m_IndicatorTransform = transform.Find("[Slot_Lifebuoy]");
                if (m_IndicatorTransform == null)
                {
                    m_IndicatorTransform = transform.Find("FoamSlot");
                }
                if (m_IndicatorTransform == null)
                {
                    m_IndicatorTransform = transform.Find("IndicatorMesh");
                }
            }
        }

        /// <summary>
        /// Gemiyi bu slota bağlar.
        /// </summary>
        public void DockShip(ShipController ship)
        {
            m_DockedShip = ship;
        }

        /// <summary>
        /// Slottaki gemiyi serbest bırakır.
        /// </summary>
        public void ReleaseShip()
        {
            m_DockedShip = null;

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
