using System.Collections.Generic;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Ekranın altındaki kamyon slotu şeridini yönetir.
    /// Slotların kendisi UI görselidir; üstlerindeki kamyonlar 3D nesnedir.
    /// Ekran boyutu değiştiğinde kamyonları slotlara yeniden hizalar.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Truck Slot Row")]
    public class TruckSlotRow : MonoBehaviour
    {
        [Header("🅿️ Slotlar")]
        [SerializeField] private List<TruckSlot> m_Slots = new List<TruckSlot>();

        [Header("🎨 Görünüm")]
        [Tooltip("Park yerlerinin boyutu, aralığı ve görünümü. " +
                 "Kaç tane olacağı bölüm verisinden gelir.")]
        [SerializeField] private TruckPlaceStyle m_Style = new TruckPlaceStyle();

        public List<TruckSlot> Slots => m_Slots;
        public TruckPlaceStyle Style => m_Style;
        public int SlotCount => m_Slots != null ? m_Slots.Count : 0;

        /// <summary>
        /// Şeridi verilen sütun/sıra sayısına göre yeniden kurar.
        /// Bölüm verisi değiştiğinde çağrılır.
        /// </summary>
        public void RebuildPlaces(int columns, int rows)
        {
            RectTransform rect = transform as RectTransform;
            if (rect == null) return;

            m_Slots = TruckPlaceBuilder.Build(rect, m_Style, columns, rows, "Slot");
        }

        private Vector2 m_LastScreenSize;

        private void OnEnable()
        {
            CollectSlotsIfEmpty();
            AlignAll();
        }

        private void Update()
        {
            // Ekran boyutu / en-boy oranı değişirse slotların dünya karşılığı da kayar
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            if (screenSize != m_LastScreenSize)
            {
                m_LastScreenSize = screenSize;
                AlignAll();
            }
        }

        private void CollectSlotsIfEmpty()
        {
            if (m_Slots != null && m_Slots.Count > 0) return;

            m_Slots = new List<TruckSlot>(GetComponentsInChildren<TruckSlot>(true));
        }

        /// <summary>Tüm kamyonları slotlarına yeniden hizalar.</summary>
        [ContextMenu("🚚 Tüm Kamyonları Hizala")]
        public void AlignAll()
        {
            if (m_Slots == null) return;

            foreach (TruckSlot slot in m_Slots)
            {
                if (slot == null) continue;
                slot.AlignTruck();
            }
        }

        /// <summary>Tüm kamyonların renklerini yeniden uygular.</summary>
        [ContextMenu("🎨 Tüm Kamyon Renklerini Uygula")]
        public void ApplyAllColors()
        {
            if (m_Slots == null) return;

            foreach (TruckSlot slot in m_Slots)
            {
                if (slot == null) continue;
                slot.ApplyTruckColor();
            }
        }

        public TruckSlot GetSlot(int index)
        {
            if (m_Slots == null || index < 0 || index >= m_Slots.Count) return null;
            return m_Slots[index];
        }

        /// <summary>Verilen renge en yakın kamyonu taşıyan slotu döndürür (renk eşleştirme için).</summary>
        public TruckSlot FindSlotByColor(Color color, float threshold = 0.15f)
        {
            if (m_Slots == null) return null;

            TruckSlot best = null;
            float bestDistance = float.MaxValue;

            foreach (TruckSlot slot in m_Slots)
            {
                if (slot == null) continue;

                Color c = slot.TruckColor;
                float distance = Mathf.Abs(c.r - color.r) + Mathf.Abs(c.g - color.g) + Mathf.Abs(c.b - color.b);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = slot;
                }
            }

            return bestDistance <= threshold * 3f ? best : null;
        }
    }
}
