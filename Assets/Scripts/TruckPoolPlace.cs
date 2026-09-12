using UnityEngine;
using UnityEngine.EventSystems;

namespace PixelGame
{
    /// <summary>
    /// Havuzdaki bir bekleme yerine tıklamayı yakalar ve oradaki kamyonu boş bir slota gönderir.
    /// Slotların hepsi doluysa veya yer boşsa hiçbir şey olmaz.
    /// </summary>
    [RequireComponent(typeof(TruckSlot))]
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Truck Pool Place")]
    public class TruckPoolPlace : MonoBehaviour, IPointerClickHandler
    {
        private TruckSlot m_Place;

        private void Awake()
        {
            m_Place = GetComponent<TruckSlot>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (m_Place == null) m_Place = GetComponent<TruckSlot>();
            if (m_Place == null || m_Place.IsEmpty) return;

            TruckDispatcher dispatcher = TruckDispatcher.Instance;
            if (dispatcher == null) return;

            dispatcher.SendToSlot(m_Place);
        }
    }
}
