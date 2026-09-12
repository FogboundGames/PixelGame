using System.Collections.Generic;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Sıradaki kamyonların beklediği havuz.
    /// Oyuncu buradan bir kamyon seçip boş bir slota gönderir; boşalan yere kuyruktan yenisi gelir.
    /// Havuzdaki yerler de slotlar gibi UI görselidir, kamyonlar onların çocuğudur.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Truck Pool")]
    public class TruckPool : MonoBehaviour
    {
        [Header("🅿️ Havuz Yerleri")]
        [SerializeField] private List<TruckSlot> m_Places = new List<TruckSlot>();

        [Header("🎨 Görünüm")]
        [Tooltip("Bekleme yerlerinin boyutu, aralığı ve görünümü. " +
                 "Kaç tane ve kaç sıra olacağı bölüm verisinden gelir.")]
        [SerializeField] private TruckPlaceStyle m_Style = new TruckPlaceStyle();

        public List<TruckSlot> Places => m_Places;
        public TruckPlaceStyle Style => m_Style;

        /// <summary>
        /// Havuzu verilen sütun/sıra sayısına göre yeniden kurar.
        /// Bölüm verisi değiştiğinde çağrılır.
        /// </summary>
        public void RebuildPlaces(int columns, int rows)
        {
            RectTransform rect = transform as RectTransform;
            if (rect == null) return;

            m_Places = TruckPlaceBuilder.Build(rect, m_Style, columns, rows, "Place");
        }
        public int PlaceCount => m_Places != null ? m_Places.Count : 0;

        private void OnEnable()
        {
            if (m_Places == null || m_Places.Count == 0)
            {
                m_Places = new List<TruckSlot>(GetComponentsInChildren<TruckSlot>(true));
            }
        }

        public TruckSlot GetPlace(int index)
        {
            if (m_Places == null || index < 0 || index >= m_Places.Count) return null;
            return m_Places[index];
        }

        /// <summary>Havuzda kamyon bekleyen ilk boş olmayan yeri bulur.</summary>
        public TruckSlot FindFirstOccupied()
        {
            if (m_Places == null) return null;

            foreach (TruckSlot place in m_Places)
            {
                if (place != null && !place.IsEmpty) return place;
            }

            return null;
        }

        /// <summary>Havuzdaki ilk boş yeri bulur.</summary>
        public TruckSlot FindFirstEmpty()
        {
            if (m_Places == null) return null;

            foreach (TruckSlot place in m_Places)
            {
                if (place != null && place.IsEmpty) return place;
            }

            return null;
        }

        /// <summary>Verilen kamyonun havuzdaki yerini bulur.</summary>
        public TruckSlot FindPlaceOf(Transform truck)
        {
            if (m_Places == null || truck == null) return null;

            foreach (TruckSlot place in m_Places)
            {
                if (place != null && place.Truck == truck) return place;
            }

            return null;
        }

        /// <summary>Havuzdaki kamyonları öne kaydırarak boşlukları kapatır.</summary>
        public void Compact()
        {
            if (m_Places == null) return;

            int write = 0;

            for (int read = 0; read < m_Places.Count; read++)
            {
                TruckSlot source = m_Places[read];
                if (source == null || source.IsEmpty) continue;

                if (read != write)
                {
                    TruckSlot target = m_Places[write];
                    if (target != null)
                    {
                        Color color = source.TruckColor;
                        target.AssignTruck(source.ReleaseTruck(), color);
                    }
                }

                write++;
            }
        }
    }
}
