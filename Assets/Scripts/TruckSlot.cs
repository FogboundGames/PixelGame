using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Ekranın altındaki tek bir kamyon slotunu temsil eder.
    /// Slotun kendisi bir UI görselidir (Slot.png), üstünde duran kamyon ise gerçek bir 3D nesnedir.
    ///
    /// Kamyon slotun çocuğudur: slotun eğimini, konumunu ve ölçeğini miras alır,
    /// böylece park yerinin düzlemine kendiliğinden oturur.
    /// Bu bileşen yalnızca kamyonun slot içindeki yönünü, boyutunu ve oturma yerini ayarlar.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Truck Slot")]
    public class TruckSlot : MonoBehaviour
    {
        [Header("🎯 Hedefler")]
        [Tooltip("Slotun UI dikdörtgeni. Boş bırakılırsa bu nesnenin kendi RectTransform'u kullanılır.")]
        [SerializeField] private RectTransform m_SlotRect;

        [Tooltip("Slotun üstünde duracak 3D kamyon (bu slotun çocuğu olmalı)")]
        [SerializeField] private Transform m_Truck;

        [Tooltip("Kamyonun slot içindeki taban duruşu. Slotun eğimi zaten miras alınır; " +
                 "bu yalnızca kamyonun park yerindeki yönüdür. Yaw bunun üzerine uygulanır.")]
        [SerializeField] private Quaternion m_BaseRotation = Quaternion.identity;

        [Header("📐 Yerleşim")]
        [Tooltip("Kamyonun slot düzlemi içindeki dönüşü. 0 = modelin doğal yönü.")]
        [Range(-180f, 180f)]
        [SerializeField] private float m_TruckYaw = 0f;

        [Tooltip("Kamyon slot genişliğinin ne kadarını kaplasın (1 = slotun tamamı)")]
        [Range(0.2f, 1.5f)]
        [SerializeField] private float m_WidthFill = 0.78f;

        [Tooltip("Kamyonun slot içindeki dikey yeri. 0.5 = tam ortası.")]
        [Range(0f, 1f)]
        [SerializeField] private float m_VerticalOffset = 0.5f;

        [Tooltip("Kamyonun slot yüzeyinden ne kadar önde duracağı. Z kavgasını (z-fighting) önler.")]
        [SerializeField] private float m_LiftOffset = 2f;

        [Header("🎨 Kamyon Rengi")]
        [SerializeField] private Color m_TruckColor = TruckPaint.Red;

        public RectTransform SlotRect => m_SlotRect != null ? m_SlotRect : transform as RectTransform;
        public Transform Truck { get => m_Truck; set { m_Truck = value; AlignTruck(); } }
        public Color TruckColor { get => m_TruckColor; set { m_TruckColor = value; ApplyTruckColor(); } }

        /// <summary>
        /// Park yerini kurar. Şerit oluşturulurken çağrılır.
        /// </summary>
        public void Configure(RectTransform rect, Quaternion truckRotation)
        {
            m_SlotRect = rect;
            m_BaseRotation = truckRotation;
        }

        /// <summary>Slotta kamyon var mı?</summary>
        public bool IsEmpty => m_Truck == null;

        /// <summary>Slottaki kamyonun yük bilgisi (boşsa null).</summary>
        public TruckCargo Cargo => m_Truck != null ? m_Truck.GetComponent<TruckCargo>() : null;

        /// <summary>
        /// Bir kamyonu bu slota yerleştirir: çocuğu yapar, park yerine oturtur ve rengini uygular.
        /// </summary>
        public void AssignTruck(Transform truck, Color color)
        {
            m_Truck = truck;
            m_TruckColor = color;

            if (m_Truck != null)
            {
                m_Truck.SetParent(SlotRect, false);
            }

            AlignTruck();
            ApplyTruckColor();
        }

        /// <summary>
        /// Kamyonu slottan ayırır ve döndürür. Slot boşalır.
        /// </summary>
        public Transform ReleaseTruck()
        {
            Transform truck = m_Truck;
            m_Truck = null;
            return truck;
        }

        private void OnEnable()
        {
            AlignTruck();
        }

        private void OnValidate()
        {
            #if UNITY_EDITOR
            // Transform ve materyal değişikliklerini OnValidate içinde doğrudan yapmak güvenli değil
            UnityEditor.EditorApplication.delayCall -= DeferredRefresh;
            UnityEditor.EditorApplication.delayCall += DeferredRefresh;
            #endif
        }

        #if UNITY_EDITOR
        private void DeferredRefresh()
        {
            if (this == null) return;
            AlignTruck();
            ApplyTruckColor();
        }
        #endif

        /// <summary>
        /// Kamyonu slotun içine oturtur: yönünü, boyutunu ve yerini ayarlar.
        /// Eğim slottan miras geldiği için burada eğimle uğraşılmaz.
        /// </summary>
        [ContextMenu("🚚 Kamyonu Slota Hizala")]
        public void AlignTruck()
        {
            if (m_Truck == null) return;

            RectTransform rect = SlotRect;
            if (rect == null) return;

            // 1. Yön: modelin taban rotasyonu korunur, yaw onun kendi ekseninde uygulanır
            m_Truck.localRotation = m_BaseRotation * Quaternion.AngleAxis(m_TruckYaw, Vector3.up);

            // 2. Boyut: slot genişliğinin belli bir oranı
            m_Truck.localPosition = Vector3.zero;
            if (TryGetTruckLocalBounds(out Bounds bounds) && bounds.size.x > 0.0001f)
            {
                float target = rect.rect.width * m_WidthFill;
                m_Truck.localScale *= target / bounds.size.x;
            }

            // 3. Yer: slotun içinde ortala, yüzeyinin biraz önüne al
            if (!TryGetTruckLocalBounds(out bounds)) return;

            m_Truck.localPosition = new Vector3(
                -bounds.center.x,
                -bounds.center.y + rect.rect.height * (m_VerticalOffset - 0.5f),
                -bounds.max.z - m_LiftOffset);
        }

        /// <summary>
        /// Kamyonun slot uzayındaki sınırlarını hesaplar.
        /// Mesh sınırları kullanılır, çünkü Renderer.bounds dünya uzayında eksen hizalıdır
        /// ve slot eğik olduğunda yanlış ölçü verir.
        /// </summary>
        private bool TryGetTruckLocalBounds(out Bounds bounds)
        {
            bounds = default;
            if (m_Truck == null) return false;

            Transform parent = m_Truck.parent;
            if (parent == null) return false;

            bool found = false;

            foreach (MeshFilter filter in m_Truck.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;

                Matrix4x4 toSlot = parent.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Bounds local = mesh.bounds;

                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3(
                        (i & 1) == 0 ? local.min.x : local.max.x,
                        (i & 2) == 0 ? local.min.y : local.max.y,
                        (i & 4) == 0 ? local.min.z : local.max.z);

                    Vector3 point = toSlot.MultiplyPoint3x4(corner);

                    if (!found)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        found = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// Slotun rengini kamyonun gövdesine uygular.
        /// </summary>
        public void ApplyTruckColor()
        {
            if (m_Truck == null) return;

            TruckPaint paint = m_Truck.GetComponent<TruckPaint>();
            if (paint == null) return;

            paint.SetBodyColor(m_TruckColor);
            paint.Apply();
        }
    }
}
