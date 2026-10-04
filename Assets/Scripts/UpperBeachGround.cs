using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Üst sahil (kumsal) bölgesinde yer alan görünmez zemin düzlemi.
    /// Kullanıcı isteği: "üst sahil kısmına bir zemin koyalım ama görünmesin bu zemini koymamızın
    /// amacı pikselart küplerinin yürüme animasyonunun daha güzel görünmesini sağlamak olacak"
    /// 
    /// Bu zemin tamamen şeffaftır (görünmezdir) fakat Universal Render Pipeline (URP) gölgelerini
    /// yakalar. Yürüyen küpler ve bacakları kumsal üzerinde adım atarken yumuşak gerçek zamanlı gölgeler
    /// düşürür; böylece küpler havada uçuyormuş gibi değil, kumsal zeminine basarak yürüyormuş gibi görünür.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Upper Beach Ground")]
    public class UpperBeachGround : MonoBehaviour
    {
        [Header("🏖️ Zemin Konumu ve Boyutları")]
        [Tooltip("Görünmez kumsal zemin düzleminin merkez konumu")]
        [SerializeField] private Vector3 m_Center = new Vector3(0f, 3.40f, 0.35f);

        [Tooltip("Görünmez kumsal zemin düzleminin genişlik ve yüksekliği")]
        [SerializeField] private Vector2 m_Size = new Vector2(11.5f, 5.4f);

        [Tooltip("Zemin eğim açısı (X ekseni)")]
        [SerializeField] private float m_TiltX = 0f;

        [Header("🌑 Gölge Yakalayıcı")]
        [SerializeField] private Material m_ShadowCatcherMaterial;
        [SerializeField] private Color m_ShadowColor = new Color(0.08f, 0.12f, 0.22f, 0.42f);

        private GameObject m_GroundQuad;
        private MeshRenderer m_Renderer;

        public Vector3 Center
        {
            get => m_Center;
            set { m_Center = value; UpdateGroundTransform(); }
        }

        public Vector2 Size
        {
            get => m_Size;
            set { m_Size = value; UpdateGroundTransform(); }
        }

        public float TiltX
        {
            get => m_TiltX;
            set { m_TiltX = value; UpdateGroundTransform(); }
        }

        private void OnEnable()
        {
            EnsureGround();
        }

        private void Start()
        {
            EnsureGround();
        }

        private void Update()
        {
            if (!Application.isPlaying && m_GroundQuad != null)
            {
                UpdateGroundTransform();
            }
        }

        public void EnsureGround()
        {
            Transform child = transform.Find("[Beach_Invisible_Ground_Quad]");
            if (child != null)
            {
                m_GroundQuad = child.gameObject;
            }
            else
            {
                m_GroundQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                m_GroundQuad.name = "[Beach_Invisible_Ground_Quad]";
                m_GroundQuad.transform.SetParent(transform, false);
            }

            // Tıklamaları asla engellemesin (collider tamamen kaldırılır)
            Collider col = m_GroundQuad.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
            m_GroundQuad.layer = 2; // Ignore Raycast

            // Gölge materyalini bağla
            if (m_ShadowCatcherMaterial == null)
            {
#if UNITY_EDITOR
                m_ShadowCatcherMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Gemi_ShadowCatcher_Mat.mat");
#endif
            }

            m_Renderer = m_GroundQuad.GetComponent<MeshRenderer>();
            if (m_Renderer != null)
            {
                if (m_ShadowCatcherMaterial != null)
                {
                    m_Renderer.sharedMaterial = m_ShadowCatcherMaterial;
                }
                m_Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                m_Renderer.receiveShadows = true;
                m_Renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                m_Renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                m_Renderer.sortingOrder = 1;
            }

            UpdateGroundTransform();
        }

        private void UpdateGroundTransform()
        {
            if (m_GroundQuad == null) return;
            m_GroundQuad.transform.localPosition = m_Center;
            m_GroundQuad.transform.localRotation = Quaternion.Euler(m_TiltX, 0f, 0f);
            m_GroundQuad.transform.localScale = new Vector3(m_Size.x, m_Size.y, 1f);
        }
    }
}
