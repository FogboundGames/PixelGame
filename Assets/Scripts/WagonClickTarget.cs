using UnityEngine;
using UnityEngine.EventSystems;

namespace PixelGame
{
    /// <summary>
    /// Havuzdaki bir vagona tıklandığında (UI veya 3B tıklama üzerinden)
    /// vagonu doğrudan ray akışına sokar.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Wagon Click Target")]
    public class WagonClickTarget : MonoBehaviour, IPointerClickHandler
    {
        public TruckSlot PoolPlace;

        public void OnPointerClick(PointerEventData eventData)
        {
            TriggerDispatch();
        }

        private void OnMouseDown()
        {
            TriggerDispatch();
        }

        public void TriggerDispatch()
        {
            // Vagon yer değiştirdiyse ebeveyn slottan güncel referansı al
            TruckSlot currentSlot = PoolPlace;
            if (transform.parent != null)
            {
                TruckSlot parentSlot = transform.parent.GetComponent<TruckSlot>();
                if (parentSlot != null)
                {
                    currentSlot = parentSlot;
                    PoolPlace = parentSlot;
                }
            }

            if (currentSlot == null || currentSlot.IsEmpty) return;

            TruckDispatcher dispatcher = TruckDispatcher.Instance;
            if (dispatcher != null)
            {
                dispatcher.SendToSlot(currentSlot);
            }
        }
    }
}
