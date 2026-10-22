using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGame
{
    /// <summary>
    /// İki kenarın birleştiği içbükey köşeleri dolduran yuvarlak "fillet" parçaları çizer (UI). Bağlı konteyner
    /// köprüsünün beyaz kenar çizgisi konteynerin beyaz konturuna sivri bir köşeyle bağlanıyordu; her köşe, iki
    /// kenara teğet bir yayla yumuşatılır. Köşeler yerel uzayda verilir: P köşe noktası, d ve e köşeden uzaklaşan
    /// iki kenar yönü (birim vektör).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class UiFilletGraphic : MaskableGraphic
    {
        private struct Corner
        {
            public Vector2 P, D, E;
        }

        [SerializeField] private float m_Radius = 3.5f;
        [SerializeField, Range(2, 24)] private int m_ArcSegments = 10;

        private readonly List<Corner> m_Corners = new List<Corner>();
        private readonly List<Corner> m_Pending = new List<Corner>();

        public float Radius { get => m_Radius; set { m_Radius = value; SetVerticesDirty(); } }

        public void BeginCorners() => m_Pending.Clear();

        public void AddCorner(Vector2 p, Vector2 d, Vector2 e)
        {
            m_Pending.Add(new Corner { P = p, D = d.normalized, E = e.normalized });
        }

        /// <summary>Yeni köşe listesi öncekinden farklıysa mesh'i yeniden kurar.</summary>
        public void EndCorners()
        {
            bool same = m_Pending.Count == m_Corners.Count;
            for (int i = 0; same && i < m_Pending.Count; i++)
            {
                Corner a = m_Pending[i], b = m_Corners[i];
                same = (a.P - b.P).sqrMagnitude < 1e-4f && (a.D - b.D).sqrMagnitude < 1e-6f && (a.E - b.E).sqrMagnitude < 1e-6f;
            }
            if (same) return;
            m_Corners.Clear();
            m_Corners.AddRange(m_Pending);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Color32 col = color;
            foreach (var c in m_Corners)
            {
                float cos = Mathf.Clamp(Vector2.Dot(c.D, c.E), -1f, 1f);
                float theta = Mathf.Acos(cos);
                // Neredeyse düz (kenarlar aynı doğrultuda) ya da çok dar köşede fillet anlamsız / sonsuz uzar
                if (theta < 20f * Mathf.Deg2Rad || theta > 170f * Mathf.Deg2Rad) continue;

                float half = theta * 0.5f;
                float k = Mathf.Min(m_Radius / Mathf.Tan(half), m_Radius * 3f);
                float r = k * Mathf.Tan(half);
                Vector2 bis = (c.D + c.E).normalized;
                Vector2 center = c.P + bis * (r / Mathf.Sin(half));
                Vector2 a = c.P + c.D * k;
                Vector2 b = c.P + c.E * k;

                float a0 = Mathf.Atan2(a.y - center.y, a.x - center.x);
                float a1 = Mathf.Atan2(b.y - center.y, b.x - center.x);
                float delta = Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, a1 * Mathf.Rad2Deg) * Mathf.Deg2Rad;

                int start = vh.currentVertCount;
                vh.AddVert(c.P, col, Vector4.zero);
                for (int i = 0; i <= m_ArcSegments; i++)
                {
                    float ang = a0 + delta * i / m_ArcSegments;
                    vh.AddVert(center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r, col, Vector4.zero);
                }
                for (int i = 0; i < m_ArcSegments; i++)
                    vh.AddTriangle(start, start + 1 + i, start + 2 + i);
            }
        }
    }
}
