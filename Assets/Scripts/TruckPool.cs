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
        [SerializeField] private int m_Columns = 1;
        [SerializeField] private int m_Rows = 1;

        [Header("🎨 Görünüm")]
        [Tooltip("Bekleme yerlerinin boyutu, aralığı ve görünümü. " +
                 "Kaç tane ve kaç sıra olacağı bölüm verisinden gelir.")]
        [SerializeField] private TruckPlaceStyle m_Style = new TruckPlaceStyle();

        public List<TruckSlot> Places => m_Places;
        public TruckPlaceStyle Style => m_Style;
        public int Columns => m_Columns;
        public int Rows => m_Rows;

        /// <summary>
        /// Havuzu verilen sütun/sıra sayısına göre yeniden kurar.
        /// Bölüm verisi değiştiğinde çağrılır.
        /// </summary>
        public void RebuildPlaces(int columns, int rows)
        {
            m_Columns = Mathf.Max(1, columns);
            m_Rows = Mathf.Max(1, rows);

            RectTransform rect = transform as RectTransform;
            if (rect == null) return;

            // Havuzun rolü sabittir: park yeri çizilmez (yalnızca kamyonlar görünür)
            // ve kamyon seçilebilmesi için tıklanabilir olmalıdır
            m_Style.showSprite = false;
            m_Style.interactive = true;

            m_Places = TruckPlaceBuilder.Build(rect, m_Style, m_Columns, m_Rows, "Place");
        }
        public int PlaceCount => m_Places != null ? m_Places.Count : 0;

        private int GetCurrentPoolColumns()
        {
            if (m_Columns > 0) return m_Columns;

            if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevel != null)
            {
                return Mathf.Max(1, LevelManager.Instance.CurrentLevel.PoolColumns);
            }

            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen != null && gen.ActiveLevelData != null)
            {
                return Mathf.Max(1, gen.ActiveLevelData.PoolColumns);
            }

            return 4;
        }

        /// <summary>
        /// Belirtilen park yerinin en ön sırada (Row 0) olup olmadığını kontrol eder.
        /// En ön sıra haricindeki arka sıra vagonları slota yerleştirilemez.
        /// </summary>
        public bool IsFrontRowPlace(TruckSlot place)
        {
            if (m_Places == null || place == null) return false;
            int index = m_Places.IndexOf(place);
            if (index < 0) return false;

            int cols = GetCurrentPoolColumns();
            int rowIndex = index / cols;
            return rowIndex == 0;
        }

        /// <summary>
        /// Arka sıradaki vagonları hafifçe gölgelendirip karartarak en ön sıradaki vagonları belirginleştirir.
        /// </summary>
        public void UpdateRowVisuals()
        {
            if (m_Places == null) return;
            int cols = GetCurrentPoolColumns();

            for (int i = 0; i < m_Places.Count; i++)
            {
                TruckSlot place = m_Places[i];
                if (place == null || place.IsEmpty || place.Truck == null) continue;

                int rowIndex = i / cols;
                bool isFront = (rowIndex == 0);

                Renderer[] renderers = place.Truck.GetComponentsInChildren<Renderer>(true);
                Color tint = isFront ? Color.white : new Color(0.6f, 0.6f, 0.72f, 1f);

                foreach (var r in renderers)
                {
                    if (r == null || r.sharedMaterial == null) continue;
                    MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor("_Color", tint);
                    mpb.SetColor("_BaseColor", tint);
                    r.SetPropertyBlock(mpb);
                }
            }
        }

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
