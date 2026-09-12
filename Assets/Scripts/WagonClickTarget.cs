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
            if (PoolPlace == null || PoolPlace.IsEmpty) return;

            TruckDispatcher dispatcher = TruckDispatcher.Instance;
            if (dispatcher != null)
            {
                dispatcher.SendToSlot(PoolPlace);
            }
        }
    }
}
