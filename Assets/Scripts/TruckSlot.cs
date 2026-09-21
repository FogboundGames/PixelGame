using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Ray üzerindeki tek bir vagon yerini temsil eder.
    ///
    /// Yerin kendisi görünmez bir UI dikdörtgenidir; altındaki ray parçası ve üstündeki
    /// vagon gerçek 3B nesnelerdir ve ikisi de bu dikdörtgenin çocuğudur.
    /// Böylece yerin eğimini, konumunu ve ölçeğini miras alırlar.
    ///
    /// Ray sabittir: vagon dolup gitse de yerinde kalır.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Truck Slot")]
    public class TruckSlot : MonoBehaviour
    {
        [Header("🎯 Hedefler")]
        [Tooltip("Slotun UI dikdörtgeni. Boş bırakılırsa bu nesnenin kendi RectTransform'u kullanılır.")]
        [SerializeField] private RectTransform m_SlotRect;

        [Tooltip("Slotun üstünde duracak 3D vagon (bu slotun çocuğu olmalı)")]
        [SerializeField] private Transform m_Truck;

        [Tooltip("Bu park yerinin altındaki ray parçası. Yan yana duran parçalar " +
                 "kesintisiz bir hat oluşturur.")]
        [SerializeField] private Transform m_Ground;

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

        [Tooltip("Vagonun ray yüzeyinden ne kadar önde duracağı. Z kavgasını (z-fighting) önler.")]
        [SerializeField] private float m_LiftOffset = 0.6f;

        [Header("🧱 Taban Hizalama (Pedestal Anchor)")]
        [Tooltip("Vagonun tabanını (pedestal/alt dairesini) slot zeminine mi oturtsun? " +
                 "Açıkken vagonun tabanı slot yüzeyine tam oturur; modelin yüksekliği yüzünden taban aşağı sarkmaz.")]
        [SerializeField] private bool m_AnchorToBase = true;

        [Tooltip("Tabanın slot yüzeyine göre dikey ofseti (slot yüksekliğinin oranı olarak). -0.07 slotun 3B eğimli yüzeyinin merkezidir.")]
        [Range(-0.5f, 0.5f)]
        [SerializeField] private float m_BaseVerticalRatio = -0.07f;

        [Tooltip("Tabanlı modeller için slot genişliğini doldurma oranı. " +
                 "Tabanın slot kenarlarından taşmasını önler (0.50 = slot genişliğinin yarısı).")]
        [Range(0.2f, 1.2f)]
        [SerializeField] private float m_BaseWidthFill = 0.85f;

        [Header("✋ Elle Sabit Duruş (Manual Transform Override)")]
        [Tooltip("Açıkken vagonun yerini/duruşunu/boyutunu otomatik hesaplama (FitToRect) yerine " +
                 "aşağıdaki sabit değerler belirler. Inspector'da bulunan bir duruşu birebir kilitlemek için kullanılır.")]
        [SerializeField] private bool m_UseManualTruckTransform = false;

        [SerializeField] private Vector3 m_ManualTruckPosition = Vector3.zero;
        [SerializeField] private Vector3 m_ManualTruckRotation = Vector3.zero;
        [SerializeField] private float m_ManualTruckScale = 1f;

        public bool UseManualTruckTransform { get => m_UseManualTruckTransform; set { m_UseManualTruckTransform = value; AlignTruck(); } }

        [Header("🛤️ Ray")]
        [Tooltip("Ray parçasının park yeri içindeki duruşu. Vagonun duruşundan bağımsızdır; " +
                 "elle bulunup doğrulanmış değerdir.")]
        [SerializeField] private Quaternion m_GroundBaseRotation = Quaternion.identity;

        [Tooltip("Rayın hat boyunca ek dönüşü. Ray yanlış yöne bakıyorsa 90 / -90 / 180 dene.")]
        [Range(-180f, 180f)]
        [SerializeField] private float m_GroundYaw = 0f;

        [Tooltip("Ray parçası slot genişliğinin ne kadarını kaplasın. " +
                 "1'den büyük değer, yan yana duran parçalar arasındaki boşluğu kapatır.")]
        [Range(0.5f, 2f)]
        [SerializeField] private float m_GroundWidthFill = 1.08f;

        [Tooltip("Ray parçasının slot içindeki dikey yeri. 0.5 = tam ortası.")]
        [Range(0f, 1f)]
        [SerializeField] private float m_GroundVerticalOffset = 0.5f;

        [Header("🎨 Kamyon Rengi")]
        [SerializeField] private Color m_TruckColor = TruckPaint.Red;

        public RectTransform SlotRect => m_SlotRect != null ? m_SlotRect : transform as RectTransform;
        public Transform Truck { get => m_Truck; set { m_Truck = value; AlignTruck(); } }
        public Transform Ground => m_Ground;
        public Color TruckColor { get => m_TruckColor; set { m_TruckColor = value; ApplyTruckColor(); } }
        public bool AnchorToBase { get => m_AnchorToBase; set { m_AnchorToBase = value; AlignTruck(); } }
        public float BaseVerticalRatio { get => m_BaseVerticalRatio; set { m_BaseVerticalRatio = value; AlignTruck(); } }
        public float BaseWidthFill { get => m_BaseWidthFill; set { m_BaseWidthFill = value; AlignTruck(); } }

        /// <summary>
        /// Park yerini kurar. Şerit oluşturulurken çağrılır.
        /// </summary>
        public void Configure(RectTransform rect, Quaternion truckRotation)
        {
            m_SlotRect = rect;
            m_BaseRotation = truckRotation;
        }

        /// <summary>
        /// Bu park yerinin ray parçasını bağlar ve yerine oturtur.
        /// Ray sabittir; vagon gelip gitse de yerinde kalır.
        /// </summary>
        public void SetGround(Transform ground, Quaternion rotation)
        {
            m_Ground = ground;
            m_GroundBaseRotation = rotation;
            AlignGround();
        }

        /// <summary>Ray parçasını slotun içine oturtur.</summary>
        [ContextMenu("🛤️ Rayı Hizala")]
        public void AlignGround()
        {
            if (m_Ground == null) return;

            RectTransform rect = SlotRect;
            if (rect == null) return;

            // Ray kendi doğal yönünde durur; slotun eğimi zaten miras gelir
            m_Ground.localRotation = m_GroundBaseRotation *
                                     Quaternion.AngleAxis(m_GroundYaw, Vector3.up);

            FitToRect(m_Ground, rect, m_GroundWidthFill, m_GroundVerticalOffset, 0f, isTruck: false);
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

                WagonClickTarget clickTarget = m_Truck.GetComponent<WagonClickTarget>();
                if (clickTarget != null)
                {
                    clickTarget.PoolPlace = this;
                }

                // Havuzdaysa (TruckPool) -> Tile modu (3B Model gizli, parlak tombul tile)
                // Normal Park Slotundaysa (SlotRow / TruckSlotRow) -> 3B Model modu (Robot modeli aktif!)
                WagonCapacityBadge badge = m_Truck.GetComponent<WagonCapacityBadge>();
                if (badge != null)
                {
                    bool isPool = GetComponentInParent<TruckPool>() != null;
                    badge.SetPoolMode(isPool);
                }
            }

            AlignTruck();
            ApplyTruckColor();

            if (TruckDispatcher.Instance != null)
            {
                TruckDispatcher.Instance.UpdateTrackCornerCounter();
            }
        }

        /// <summary>
        /// Kamyonu slottan ayırır ve döndürür. Slot boşalır.
        /// </summary>
        public Transform ReleaseTruck()
        {
            Transform truck = m_Truck;
            m_Truck = null;

            if (TruckDispatcher.Instance != null)
            {
                TruckDispatcher.Instance.UpdateTrackCornerCounter();
            }

            return truck;
        }

        private void OnEnable()
        {
            AlignAll();
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
            AlignAll();
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

            // Elle kilitlenmiş bir duruş varsa otomatik hizalamayı atla; Inspector'da
            // bulunup onaylanmış değerleri birebir uygula.
            if (m_UseManualTruckTransform)
            {
                m_Truck.localPosition = m_ManualTruckPosition;
                m_Truck.localRotation = Quaternion.Euler(m_ManualTruckRotation);
                m_Truck.localScale = Vector3.one * m_ManualTruckScale;
                return;
            }

            // 1. Yön: modelin taban rotasyonu korunur, yaw onun kendi ekseninde uygulanır
            m_Truck.localRotation = m_BaseRotation * Quaternion.AngleAxis(m_TruckYaw, Vector3.up);

            // 2. Boyut ve yer: slot genişliğine göre ölçekle, tabanı dikkate alarak slotun/rayın önüne oturt
            FitToRect(m_Truck, rect, m_WidthFill, m_VerticalOffset, m_LiftOffset, isTruck: true);
        }

        /// <summary>
        /// Rayı ve (varsa) vagonu birlikte hizalar.
        /// Ray vagondan bağımsızdır: slot boşken de yerinde durmalı.
        /// </summary>
        public void AlignAll()
        {
            AlignGround();
            AlignTruck();
        }

        /// <summary>
        /// Bir 3B modeli slotun içine sığdırır: genişliğine göre ölçekler,
        /// yatayda ortalar ve slot yüzeyinin önüne alır.
        /// Hem vagon hem ray parçası için kullanılır.
        /// </summary>
        private void FitToRect(Transform model, RectTransform rect,
                               float widthFill, float verticalOffset, float lift, bool isTruck = false)
        {
            model.localPosition = Vector3.zero;
            model.localScale = Vector3.one;

            if (TryGetLocalBounds(model, out Bounds bounds) && bounds.size.x > 0.0001f)
            {
                // Tabanı olan vagon modelleri için m_BaseWidthFill kullanılır (taban slottan taşmasın)
                float effectiveWidthFill = (isTruck && m_AnchorToBase && m_BaseWidthFill > 0.01f)
                    ? m_BaseWidthFill
                    : widthFill;

                float scale = (rect.rect.width * effectiveWidthFill) / bounds.size.x;

                if (bounds.size.y > 0.0001f && (!isTruck || !m_AnchorToBase))
                {
                    float heightLimited = rect.rect.height / bounds.size.y;
                    scale = Mathf.Min(scale, heightLimited);
                }

                model.localScale = Vector3.one * scale;
            }

            if (!TryGetLocalBounds(model, out bounds)) return;

            float posY;
            if (isTruck && m_AnchorToBase)
            {
                // Taban kısmını dikkate al: Modelin en alt taban noktası (-bounds.min.y)
                // slotun zemin yüzeyine (+ targetBaseY) tam oturur, aşağı sarkmaz.
                float targetBaseY = rect.rect.height * (verticalOffset - 0.5f) + rect.rect.height * m_BaseVerticalRatio;
                posY = -bounds.min.y + targetBaseY;
            }
            else
            {
                posY = -bounds.center.y + rect.rect.height * (verticalOffset - 0.5f);
            }

            model.localPosition = new Vector3(
                -bounds.center.x,
                posY,
                -bounds.max.z - lift);
        }

        /// <summary>
        /// Kamyonun slot uzayındaki sınırlarını hesaplar.
        /// Mesh sınırları kullanılır, çünkü Renderer.bounds dünya uzayında eksen hizalıdır
        /// ve slot eğik olduğunda yanlış ölçü verir.
        /// </summary>
        private static bool TryGetLocalBounds(Transform model, out Bounds bounds)
        {
            bounds = default;
            if (model == null) return false;

            Transform parent = model.parent;
            if (parent == null) return false;

            bool found = false;

            // 1. MeshFilter'ları (inaktif nesneler dahil!) ara
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
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

            // 2. Mesh bulunamazsa BoxCollider sınırlarını kullan
            if (!found)
            {
                BoxCollider col = model.GetComponent<BoxCollider>();
                if (col != null)
                {
                    Matrix4x4 toSlot = parent.worldToLocalMatrix * model.localToWorldMatrix;
                    Vector3 c = col.center;
                    Vector3 s = col.size * 0.5f;

                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 corner = new Vector3(
                            c.x + ((i & 1) == 0 ? -s.x : s.x),
                            c.y + ((i & 2) == 0 ? -s.y : s.y),
                            c.z + ((i & 4) == 0 ? -s.z : s.z));

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
