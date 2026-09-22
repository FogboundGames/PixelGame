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

        public int SlotIndex { get => m_SlotIndex; set => m_SlotIndex = value; }
        public ShipController DockedShip => m_DockedShip;
        public bool IsEmpty => m_DockedShip == null;
        public Transform IndicatorTransform => m_IndicatorTransform;

        private void Awake()
        {
            if (m_IndicatorTransform == null)
            {
                m_IndicatorTransform = transform.Find("IndicatorMesh");
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
        }
    }
}
