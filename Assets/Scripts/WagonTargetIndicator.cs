using UnityEngine;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// Vagonun gövde rengini değiştirmeden HANGİ RENGE HİTAP ETTİĞİNİ (hedef küp rengi)
    /// namlu ağzında parlayan yüklü mermi küresi ve ağız halkasıyla gösteren bileşen.
    /// Kullanıcının "+ olarak vagonların renkleri olduğu gibi kalsın ama hangi renge hitap edeceklerini bir şekilde belli etmeni istiyorum" talebine yanıt verir.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class WagonTargetIndicator : MonoBehaviour
    {
        [Header("🎯 Hedef Renk")]
        [SerializeField] private Color m_TargetColor = Color.red;

        [Header("📐 Namlu & Yüklü Mermi")]
        [Tooltip("Namlu ağzında duran mermi küresinin konumu (object_005 için local Y:1.35, Z:0.75)")]
        [SerializeField] private Vector3 m_MuzzleLocalPos = new Vector3(0f, 1.35f, 0.75f);

        [Tooltip("Yüklü mermi küresinin referans büyüklüğü")]
        [SerializeField] private float m_SphereScale = 0.26f;

        [Tooltip("Namlu ağzında hafif nabız (nefes alma) animasyonu")]
        [SerializeField] private bool m_EnableBreathing = true;

        private Transform m_MuzzleSphere;
        private MeshRenderer m_SphereRenderer;
        private Transform m_BaseRing;
        private MeshRenderer m_BaseRingRenderer;
        private Tween m_BreathTween;
        private TruckCargo m_Cargo;
        private bool? m_IsScifiTurret;

        /// <summary>
        /// Namlu küresi ve taban halkası yalnızca sci-fi taret modellerinde (VacuumCannon/
        /// object_005 gibi) anlamlıdır. Başka bir model (örn. BlueBot) bu bileşeni taşıyorsa
        /// namlu konumunda alakasız bir küre belirir ve gövde sınırlarını (bounds) şişirip
        /// rozet/ölçek hesaplarını bozar; bu yüzden model adı/mesh adı sci-fi değilse hiç
        /// oluşturulmazlar.
        /// </summary>
        private bool IsScifiTurret
        {
            get
            {
                if (m_IsScifiTurret.HasValue) return m_IsScifiTurret.Value;

                bool found = false;
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    if (r.name.IndexOf("object_", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        r.name.IndexOf("Cannon", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        r.name.IndexOf("Turret", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        found = true;
                        break;
                    }
                }

                m_IsScifiTurret = found;
                return found;
            }
        }

        public Color TargetColor => m_TargetColor;
        public Vector3 MuzzleWorldPosition => m_MuzzleSphere != null ? m_MuzzleSphere.position : transform.TransformPoint(m_MuzzleLocalPos);

        private void Awake()
        {
            m_Cargo = GetComponent<TruckCargo>();
            EnsureVisualIndicators();
            SyncWithCargo();
        }

        private void OnEnable()
        {
            EnsureVisualIndicators();
            SyncWithCargo();
            StartBreathing();
        }

        private void OnDisable()
        {
            StopBreathing();
        }

        private void Update()
        {
            // Cargo rengi değiştiğinde (örneğin runtime'da atandığında) otomatik senkronize et
            if (m_Cargo != null && m_Cargo.CargoColor != m_TargetColor)
            {
                SetTargetColor(m_Cargo.CargoColor);
            }
        }

        /// <summary>
        /// Hedef rengi ayarlar ve namludaki küre ile gövde halkasının rengini günceller.
        /// Vagonun kendi gövde boyası korunur.
        /// </summary>
        public void SetTargetColor(Color color)
        {
            m_TargetColor = color;

            EnsureVisualIndicators();
            if (m_SphereRenderer != null)
            {
                Material mat = CartoonShader.CreateMaterial(color, $"Mat_MuzzleAmmo_{ColorUtility.ToHtmlStringRGB(color)}");
                m_SphereRenderer.sharedMaterial = mat;
            }
            if (m_BaseRingRenderer != null)
            {
                Material mat = CartoonShader.CreateMaterial(color, $"Mat_BaseAccent_{ColorUtility.ToHtmlStringRGB(color)}");
                m_BaseRingRenderer.sharedMaterial = mat;
            }
        }

        /// <summary>
        /// Kamyonun kargosuyla hedef rengi senkronize eder.
        /// </summary>
        public void SyncWithCargo()
        {
            if (m_Cargo == null) m_Cargo = GetComponent<TruckCargo>();
            if (m_Cargo != null)
            {
                SetTargetColor(m_Cargo.CargoColor);
            }
        }

        /// <summary>
        /// Ateş edildiğinde namludaki mermiyi fırlatılmış gibi gösterir
        /// ve ardından namluda yeni bir mermi küresini canlandırır (re-arm/reload pop).
        /// </summary>
        public void PlayShootAndReloadAnimation()
        {
            if (m_MuzzleSphere == null) return;

            m_MuzzleSphere.DOKill();
            // Namludan fırlama hissi: anında küçül
            m_MuzzleSphere.localScale = Vector3.zero;

            // 0.18 saniye sonra yeni mermi namluda belirir (squash & bounce ile)
            m_MuzzleSphere.DOScale(Vector3.one * m_SphereScale, 0.22f)
                .SetDelay(0.12f)
                .SetEase(Ease.OutBack)
                .OnComplete(() =>
                {
                    StartBreathing();
                });
        }

        private void EnsureVisualIndicators()
        {
            if (!IsScifiTurret) return;

            EnsureMuzzleSphere();
            EnsureBaseRing();
        }

        private void EnsureBaseRing()
        {
            if (m_BaseRing != null) return;

            Transform existing = transform.Find("TurretBaseAccentRing");
            if (existing != null)
            {
                m_BaseRing = existing;
                m_BaseRingRenderer = m_BaseRing.GetComponent<MeshRenderer>();
                return;
            }

            GameObject ringObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringObj.name = "TurretBaseAccentRing";
            ringObj.transform.SetParent(transform, false);
            ringObj.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            ringObj.transform.localScale = new Vector3(0.78f, 0.025f, 0.78f);
            ringObj.transform.localRotation = Quaternion.identity;

            Collider col = ringObj.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            m_BaseRing = ringObj.transform;
            m_BaseRingRenderer = ringObj.GetComponent<MeshRenderer>();
            if (m_BaseRingRenderer != null)
            {
                Material mat = CartoonShader.CreateMaterial(m_TargetColor, $"Mat_BaseAccent_{ColorUtility.ToHtmlStringRGB(m_TargetColor)}");
                m_BaseRingRenderer.sharedMaterial = mat;
            }
        }

        private void EnsureMuzzleSphere()
        {
            if (m_MuzzleSphere != null) return;

            Transform existing = transform.Find("MuzzleAmmoSphere");
            if (existing != null)
            {
                m_MuzzleSphere = existing;
                m_SphereRenderer = m_MuzzleSphere.GetComponent<MeshRenderer>();
                return;
            }

            // Yoksa dinamik 3D küre üret
            GameObject sphereObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereObj.name = "MuzzleAmmoSphere";
            sphereObj.transform.SetParent(transform, false);
            sphereObj.transform.localPosition = m_MuzzleLocalPos;
            sphereObj.transform.localScale = Vector3.one * m_SphereScale;
            sphereObj.transform.localRotation = Quaternion.identity;

            // Fiziksel çarpışma gerekmez; yalnızca görsel gösterge
            Collider col = sphereObj.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            m_MuzzleSphere = sphereObj.transform;
            m_SphereRenderer = sphereObj.GetComponent<MeshRenderer>();

            if (m_SphereRenderer != null)
            {
                Material mat = CartoonShader.CreateMaterial(m_TargetColor, $"Mat_MuzzleAmmo_{ColorUtility.ToHtmlStringRGB(m_TargetColor)}");
                m_SphereRenderer.sharedMaterial = mat;
            }
        }

        private void StartBreathing()
        {
            if (!m_EnableBreathing || m_MuzzleSphere == null) return;

            m_MuzzleSphere.DOKill();
            m_MuzzleSphere.localScale = Vector3.one * m_SphereScale;

            m_BreathTween = m_MuzzleSphere.DOScale(Vector3.one * (m_SphereScale * 1.15f), 0.65f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private void StopBreathing()
        {
            m_BreathTween?.Kill();
            if (m_MuzzleSphere != null)
            {
                m_MuzzleSphere.DOKill();
            }
        }

        private void OnDestroy()
        {
            StopBreathing();
        }
    }
}
