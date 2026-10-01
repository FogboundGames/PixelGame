using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace PixelGame
{
    /// <summary>
    /// İki bağlı gemi (Linked Ships) arasında su üzerinde dinamik olarak gerilen,
    /// hafif sarkan ve gemiler hareket ettikçe onları takip eden halat/zincir bileşeni.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class LinkedShipTether : MonoBehaviour
    {
        [Header("🚢 Bağlı Gemiler")]
        [SerializeField] private ShipController m_ShipA;
        [SerializeField] private ShipController m_ShipB;
        [SerializeField] private int m_LinkId = 0;

        [Header("🪢 Halat / Zincir Görsel Ayarları")]
        [SerializeField] private int m_SegmentCount = 14;
        [SerializeField] private float m_SagAmount = 0.22f; // Suya doğru sarkma miktarı
        [SerializeField] private float m_LineWidth = 0.16f; // Daha belirgin ve kalın halat
        [SerializeField] private Color m_RopeColor = new Color(0.85f, 0.65f, 0.35f, 1f);

        private LineRenderer m_LineRenderer;
        private static Material s_TetherMaterial;
        private float m_RattleIntensity = 0f;
        private Color m_LastColA;
        private Color m_LastColB;

        public ShipController ShipA => m_ShipA;
        public ShipController ShipB => m_ShipB;
        public int LinkId => m_LinkId;

        public static LinkedShipTether CreateTether(ShipController a, ShipController b, int linkId)
        {
            if (a == null || b == null) return null;

            GameObject tetherObj = new GameObject($"Tether_Link_{linkId}_{a.name}_{b.name}");
            tetherObj.transform.SetParent(a.transform.parent, false);

            LinkedShipTether tether = tetherObj.AddComponent<LinkedShipTether>();
            tether.Setup(a, b, linkId);
            return tether;
        }

        public void Setup(ShipController a, ShipController b, int linkId)
        {
            m_ShipA = a;
            m_ShipB = b;
            m_LinkId = linkId;

            EnsureLineRenderer();
            ApplyColorsFromShips();
            UpdateTetherPositions();
        }

        private void Awake()
        {
            EnsureLineRenderer();
        }

        private void EnsureLineRenderer()
        {
            if (m_LineRenderer == null) m_LineRenderer = GetComponent<LineRenderer>();
            if (m_LineRenderer == null) m_LineRenderer = gameObject.AddComponent<LineRenderer>();

            m_LineRenderer.useWorldSpace = true;
            m_LineRenderer.positionCount = m_SegmentCount;
            m_LineRenderer.startWidth = m_LineWidth;
            m_LineRenderer.endWidth = m_LineWidth;
            m_LineRenderer.numCapVertices = 6;
            m_LineRenderer.numCornerVertices = 6;

            if (s_TetherMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Standard");
                s_TetherMaterial = new Material(shader);
                s_TetherMaterial.color = Color.white;
            }

            m_LineRenderer.material = s_TetherMaterial;
            m_LineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_LineRenderer.receiveShadows = false;
        }

        /// <summary>
        /// Halatı yarı yarıya bağlı iki geminin rengine boyar (Soldaki gemi mor ise solu mor, sağdaki pembe ise sağı pembe).
        /// </summary>
        public void ApplyColorsFromShips()
        {
            if (m_LineRenderer == null || m_ShipA == null || m_ShipB == null) return;

            Color colA = m_ShipA.ShipColor;
            Color colB = m_ShipB.ShipColor;

            if (colA.a < 0.1f) colA = m_RopeColor;
            if (colB.a < 0.1f) colB = m_RopeColor;

            m_LastColA = colA;
            m_LastColB = colB;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(colA, 0.0f),
                    new GradientColorKey(colA, 0.46f),
                    new GradientColorKey(colB, 0.54f),
                    new GradientColorKey(colB, 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 1.0f)
                }
            );

            m_LineRenderer.colorGradient = gradient;
        }

        private void LateUpdate()
        {
            if (m_ShipA == null || m_ShipB == null || !m_ShipA.gameObject.activeInHierarchy || !m_ShipB.gameObject.activeInHierarchy)
            {
                Destroy(gameObject);
                return;
            }

            // Eğer her iki gemi de ayrılıyorsa halatı yok et
            if (m_ShipA.IsDeparting && m_ShipB.IsDeparting)
            {
                Destroy(gameObject);
                return;
            }

            UpdateTetherPositions();
        }

        public void UpdateTetherPositions()
        {
            if (m_LineRenderer == null || m_ShipA == null || m_ShipB == null) return;

            if (m_ShipA.ShipColor != m_LastColA || m_ShipB.ShipColor != m_LastColB)
            {
                ApplyColorsFromShips();
            }

            Vector3 posA = m_ShipA.transform.position + new Vector3(0f, 0.05f, 0f);
            Vector3 posB = m_ShipB.transform.position + new Vector3(0f, 0.05f, 0f);

            float dist = Vector3.Distance(posA, posB);
            // Mesafe uzadıkça sarkma azalır (gerilir), yaklaştıkça sarkar
            float currentSag = Mathf.Clamp(m_SagAmount * (1.8f - Mathf.Clamp01(dist / 3.5f)), 0.02f, 0.35f);

            if (m_LineRenderer.positionCount != m_SegmentCount)
            {
                m_LineRenderer.positionCount = m_SegmentCount;
            }

            for (int i = 0; i < m_SegmentCount; i++)
            {
                float t = (float)i / (m_SegmentCount - 1);
                // Doğrusal enterpolasyon
                Vector3 p = Vector3.Lerp(posA, posB, t);

                // Parabolik sarkma (U eğrisi: t*(1-t)*4 tepe noktası)
                float sagFactor = 4f * t * (1f - t);
                p.y -= currentSag * sagFactor;

                // Titreme / gerilme sarsıntısı varsa ekle
                if (m_RattleIntensity > 0.001f)
                {
                    p.x += Mathf.Sin(Time.time * 45f + i * 2f) * m_RattleIntensity * sagFactor;
                    p.z += Mathf.Cos(Time.time * 40f + i * 2f) * m_RattleIntensity * sagFactor;
                }

                m_LineRenderer.SetPosition(i, p);
            }

            if (m_RattleIntensity > 0f)
            {
                m_RattleIntensity = Mathf.Max(0f, m_RattleIntensity - Time.deltaTime * 3.5f);
            }
        }

        /// <summary>
        /// Kilitli bir gemiye tıklandığında halatın gerilip titremesini sağlar.
        /// </summary>
        public void Rattle()
        {
            m_RattleIntensity = 0.09f;
            transform.DOKill();
            transform.DOShakePosition(0.35f, new Vector3(0.08f, 0f, 0.08f), 15, 90f);
        }
    }
}
