using System;
using UnityEngine;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// Vagonun namlusundan fırlatılan enerjik 3D mermi küresi (Cannon Projectile).
    /// Hedef küpe doğru hızlı, kavisli ve tatmin edici bir balistik uçuş yapar.
    /// Renk, iz (trail) ve çarpma anında voksel patlamasını tetikler.
    /// </summary>
    [DisallowMultipleComponent]
    public class CannonProjectile : MonoBehaviour
    {
        [Header("Görsel & Materyal")]
        [SerializeField] private MeshRenderer m_Renderer;
        [SerializeField] private TrailRenderer m_Trail;
        [SerializeField] private float m_BaseScale = 0.22f;

        [Header("Uçuş Ayarları")]
        [SerializeField] private float m_DefaultDuration = 0.24f;
        [SerializeField] private float m_ArcHeight = 0.35f;

        private static Material s_CachedMaterialTemplate;
        private Color m_CurrentColor;
        private Tween m_FlightTween;

        private void Awake()
        {
            if (m_Renderer == null) m_Renderer = GetComponent<MeshRenderer>();
            if (m_Trail == null) m_Trail = GetComponent<TrailRenderer>();
        }

        /// <summary>
        /// Kürenin rengini Toony Colors Pro 2 cartoon plastik shader ile ayarlar.
        /// </summary>
        public void SetColor(Color color)
        {
            m_CurrentColor = color;

            if (m_Renderer != null)
            {
                Material mat = CartoonShader.CreateMaterial(color, $"Mat_Projectile_{ColorUtility.ToHtmlStringRGB(color)}");
                m_Renderer.sharedMaterial = mat;
            }

            if (m_Trail != null)
            {
                m_Trail.startColor = new Color(color.r, color.g, color.b, 0.85f);
                m_Trail.endColor = new Color(color.r, color.g, color.b, 0f);
            }
        }

        /// <summary>
        /// Küreyi verilen başlangıç noktasından hedef küpe doğru fırlatır.
        /// </summary>
        public void Launch(Vector3 startPos, Vector3 targetPos, Color color, Action onHit, float duration = -1f)
        {
            if (duration <= 0f) duration = m_DefaultDuration;

            transform.position = startPos;
            transform.localScale = Vector3.one * m_BaseScale;
            SetColor(color);

            if (m_Trail != null)
            {
                m_Trail.Clear();
                m_Trail.enabled = true;
            }

            // Başlangıçta namludan fırlama ezilme-uzama (squash & stretch) hissi
            Vector3 flightDir = (targetPos - startPos).normalized;
            if (flightDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(flightDir);
            }

            transform.DOKill();
            transform.DOScale(new Vector3(m_BaseScale * 0.7f, m_BaseScale * 0.7f, m_BaseScale * 1.4f), duration * 0.4f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    transform.DOScale(Vector3.one * m_BaseScale, duration * 0.6f).SetEase(Ease.InQuad);
                });

            // Parabolik uçuş eğrisi (DoTween ile pürüzsüz interpolasyon)
            Vector3 midPoint = (startPos + targetPos) * 0.5f;
            // Kameraya ve yukarıya doğru hafif kavis (-Z ve +Y)
            midPoint += Vector3.up * (m_ArcHeight * 0.6f) - Vector3.forward * (m_ArcHeight * 0.8f);

            Vector3[] path = new Vector3[] { startPos, midPoint, targetPos };

            m_FlightTween?.Kill();
            m_FlightTween = transform.DOPath(path, duration, PathType.CatmullRom)
                .SetEase(Ease.InQuad)
                .OnUpdate(() =>
                {
                    // Uçuş yönüne doğru bak
                    if (flightDir.sqrMagnitude > 0.001f)
                    {
                        transform.rotation = Quaternion.LookRotation(flightDir);
                    }
                })
                .OnComplete(() =>
                {
                    if (m_Trail != null) m_Trail.enabled = false;
                    onHit?.Invoke();
                    Destroy(gameObject);
                });
        }

        private void OnDestroy()
        {
            m_FlightTween?.Kill();
            transform.DOKill();
        }
    }
}
