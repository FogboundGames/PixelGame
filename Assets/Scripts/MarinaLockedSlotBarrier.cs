using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// Sahildeki kapalı / kilitli su slotlarını (dock bays) temsil eden görsel bariyer bileşeni.
    /// Kırmızı-beyaz dubalar, metal zincir ve altın asma kilit (🔒) görseli içerir.
    /// Su yüzeyinde hafifçe dalgalanır (bobbing) ve tıklandığında kilit sarsılma tepkisi verir.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Marina Locked Slot Barrier")]
    public class MarinaLockedSlotBarrier : MonoBehaviour, IPointerClickHandler
    {
        [Header("🔒 Bölme Bilgisi")]
        [SerializeField] private int m_BayIndex = 0;
        [SerializeField] private Transform m_VisualTransform;
        [SerializeField] private Transform m_BerthTransform;
        [SerializeField] private MarinaBuoyBobbing m_Bobbing;

        [Header("📏 Boyut & Konum")]
        [SerializeField] private float m_Width = 0.85f;
        [SerializeField] private float m_Height = 0.85f;
        [SerializeField] private float m_BerthWidth = 0.98f;
        [SerializeField] private float m_BerthLength = 1.20f;

        private Tween m_ShakeTween;

        public int BayIndex
        {
            get => m_BayIndex;
            set => m_BayIndex = value;
        }

        public Transform VisualTransform => m_VisualTransform;
        public Transform BerthTransform => m_BerthTransform;

        private void Awake()
        {
            EnsureComponents();
        }

        private void OnEnable()
        {
            EnsureComponents();
        }

        public void Setup(int bayIndex, Material barrierMat, Mesh barrierMesh, float width = 0.85f, float height = 0.85f)
        {
            Setup(bayIndex, barrierMat, barrierMesh, null, null, width, height, m_BerthWidth, m_BerthLength);
        }

        public void Setup(
            int bayIndex,
            Material barrierMat,
            Mesh barrierMesh,
            Material berthMat,
            Mesh berthMesh,
            float width = 0.85f,
            float height = 0.85f,
            float berthWidth = 0.98f,
            float berthLength = 1.20f)
        {
            m_BayIndex = bayIndex;
            m_Width = width;
            m_Height = height;
            m_BerthWidth = berthWidth;
            m_BerthLength = berthLength;

            EnsureBerthVisual(berthMat, berthMesh, berthWidth, berthLength);
            EnsureLockVisual(barrierMat, barrierMesh, width, height);
            EnsureCollider(Mathf.Max(width, berthWidth), berthLength);
        }

        public void EnsureComponents()
        {
            if (m_VisualTransform == null)
            {
                Transform child = transform.Find("[Barrier_Visual]");
                if (child != null) m_VisualTransform = child;
            }

            if (m_BerthTransform == null)
            {
                Transform child = transform.Find("[Berth_Visual]");
                if (child != null) m_BerthTransform = child;
            }

            if (m_Bobbing == null && m_VisualTransform != null)
            {
                m_Bobbing = m_VisualTransform.GetComponent<MarinaBuoyBobbing>();
            }
        }

        public void EnsureBerthVisual(Material berthMat, Mesh berthMesh, float width, float length)
        {
            if (m_BerthTransform == null)
            {
                Transform child = transform.Find("[Berth_Visual]");
                if (child != null)
                {
                    m_BerthTransform = child;
                }
                else
                {
                    GameObject go = new GameObject("[Berth_Visual]");
                    go.transform.SetParent(transform, false);
                    m_BerthTransform = go.transform;
                }
            }

            // Su yüzeyinde, iskele yanaşma bölmesine (berth bay) tam oturan yüzer slot
            m_BerthTransform.localPosition = new Vector3(0f, 0.015f, 0.15f);
            m_BerthTransform.localRotation = Quaternion.identity;
            m_BerthTransform.localScale = new Vector3(width, 1f, length);

            MeshFilter mf = m_BerthTransform.GetComponent<MeshFilter>();
            if (mf == null) mf = m_BerthTransform.gameObject.AddComponent<MeshFilter>();
            if (berthMesh != null && mf.sharedMesh != berthMesh)
            {
                mf.sharedMesh = berthMesh;
            }

            MeshRenderer mr = m_BerthTransform.GetComponent<MeshRenderer>();
            if (mr == null) mr = m_BerthTransform.gameObject.AddComponent<MeshRenderer>();
            if (berthMat != null && mr.sharedMaterial != berthMat)
            {
                mr.sharedMaterial = berthMat;
            }
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            mr.sortingOrder = 3;

            var bob = m_BerthTransform.GetComponent<MarinaBuoyBobbing>();
            if (bob == null) m_BerthTransform.gameObject.AddComponent<MarinaBuoyBobbing>();

            if (!m_BerthTransform.gameObject.activeSelf)
            {
                m_BerthTransform.gameObject.SetActive(true);
            }
        }

        public void EnsureLockVisual(Material barrierMat, Mesh barrierMesh, float width, float height)
        {
            if (m_VisualTransform == null)
            {
                Transform child = transform.Find("[Barrier_Visual]");
                if (child != null)
                {
                    m_VisualTransform = child;
                }
                else
                {
                    GameObject go = new GameObject("[Barrier_Visual]");
                    go.transform.SetParent(transform, false);
                    m_VisualTransform = go.transform;
                }
            }

            // Asma kilit rozeti slotun üstünde yüzer ve belirgin görünür
            m_VisualTransform.localPosition = new Vector3(0f, 0.08f, 0.15f);
            m_VisualTransform.localRotation = Quaternion.identity;
            m_VisualTransform.localScale = new Vector3(width, height, 1f);

            MeshFilter mf = m_VisualTransform.GetComponent<MeshFilter>();
            if (mf == null) mf = m_VisualTransform.gameObject.AddComponent<MeshFilter>();
            if (barrierMesh != null && mf.sharedMesh != barrierMesh)
            {
                mf.sharedMesh = barrierMesh;
            }

            MeshRenderer mr = m_VisualTransform.GetComponent<MeshRenderer>();
            if (mr == null) mr = m_VisualTransform.gameObject.AddComponent<MeshRenderer>();
            if (barrierMat != null && mr.sharedMaterial != barrierMat)
            {
                mr.sharedMaterial = barrierMat;
            }
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            mr.sortingOrder = 5;

            // Dalga salınımı (bobbing)
            if (m_Bobbing == null)
            {
                m_Bobbing = m_VisualTransform.GetComponent<MarinaBuoyBobbing>();
                if (m_Bobbing == null) m_Bobbing = m_VisualTransform.gameObject.AddComponent<MarinaBuoyBobbing>();
            }

            if (!m_VisualTransform.gameObject.activeSelf)
            {
                m_VisualTransform.gameObject.SetActive(true);
            }
        }

        private void EnsureCollider(float width, float length)
        {
            BoxCollider col = GetComponent<BoxCollider>();
            if (col == null) col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.05f, 0.15f);
            col.size = new Vector3(width, 0.5f, length);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            TriggerLockedJiggle();
        }

        private void OnMouseDown()
        {
            TriggerLockedJiggle();
        }

        /// <summary>
        /// Oyuncu kilitli slota dokunduğunda kilidin hafifçe sarsılıp "tık" sesiyle kilitli olduğunu hissettirmesi.
        /// </summary>
        public void TriggerLockedJiggle()
        {
            if (!Application.isPlaying) return;
            if (m_ShakeTween != null && m_ShakeTween.IsActive()) return;

            if (m_VisualTransform != null && m_VisualTransform.gameObject.activeInHierarchy)
            {
                m_VisualTransform.DOKill(true);
                m_ShakeTween = m_VisualTransform.DOShakeRotation(0.32f, new Vector3(0f, 0f, 18f), 14, 90f)
                    .OnComplete(() => m_ShakeTween = null);
            }
            else if (m_BerthTransform != null)
            {
                // Kilit rozeti gizliyse kapalı slotun kendisi "hayır" der gibi titrer
                // (konum/rotasyonu MarinaBuoyBobbing her kare yazdığı için ölçek kullanılır)
                m_BerthTransform.DOKill(true);
                Vector3 s = m_BerthTransform.localScale;
                Vector3 punch = s.y > 0.01f ? new Vector3(s.x * 0.08f, s.y * 0.08f, 0f) : new Vector3(s.x * 0.12f, 0f, s.z * 0.06f);
                m_ShakeTween = m_BerthTransform.DOPunchScale(punch, 0.32f, 10, 0.8f)
                    .OnComplete(() => m_ShakeTween = null);
            }

            HypercasualFeedbackManager feedback = FindFirstObjectByType<HypercasualFeedbackManager>();
            if (feedback != null)
            {
                feedback.PlayDenialFeedback();
            }
        }
    }
}
