using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGame
{
    public enum ShadowShape
    {
        [InspectorName("📱 Çentikli Telefon Ekranı (Phone with Notch)")]
        PhoneWithNotch,

        [InspectorName("🔲 Düz Dikdörtgen / Panel (Rectangle)")]
        Rectangle
    }

    /// <summary>
    /// Ekranın / Panonun iç kenarlarına yumuşak ve ayarlanabilir sahte gölge (Inner Fake Shadow / Ambient Occlusion) ekler.
    /// Telefonun Çentiği (Notch) ve Yuvarlatılmış Köşeleriyle (Rounded Corners) birebir uyumlu çalışır.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    [ExecuteAlways]
    [AddComponentMenu("UI/PixelGame/Fake Shadow (İç Gölge)")]
    public class UIFakeShadow : MaskableGraphic
    {
        [Header("Gölge Şekli")]
        [SerializeField] private ShadowShape m_ShapeMode = ShadowShape.PhoneWithNotch;

        [Header("Çentik (Notch) Ayarları")]
        [SerializeField] private bool m_EnableNotch = true;
        [Min(0f)]
        [SerializeField] private float m_NotchWidth = 340f;
        [Min(0f)]
        [SerializeField] private float m_NotchHeight = 65f;
        [Min(0f)]
        [SerializeField] private float m_NotchRadius = 22f;

        [Header("Ekran Köşe Yuvarlama")]
        [Min(0f)]
        [SerializeField] private float m_CornerRadius = 50f;

        [Header("Gölge Rengi ve Yoğunluğu")]
        [SerializeField] private Color m_ShadowColor = new Color(0.04f, 0.03f, 0.02f, 0.85f);
        [Range(0f, 1f)]
        [SerializeField] private float m_Intensity = 1f;

        [Header("Gölge Boyutu (Kalınlık)")]
        [SerializeField] private bool m_UniformSize = true;
        [Min(0f)]
        [SerializeField] private float m_GlobalSize = 55f;

        [Min(0f)]
        [SerializeField] private float m_TopSize = 65f;
        [Min(0f)]
        [SerializeField] private float m_BottomSize = 55f;
        [Min(0f)]
        [SerializeField] private float m_LeftSize = 45f;
        [Min(0f)]
        [SerializeField] private float m_RightSize = 45f;

        [Header("Kenar Kontrolleri")]
        [SerializeField] private bool m_EnableTop = true;
        [SerializeField] private bool m_EnableBottom = true;
        [SerializeField] private bool m_EnableLeft = true;
        [SerializeField] private bool m_EnableRight = true;

        [Header("Yumuşaklık ve Kalite")]
        [Range(0.3f, 4f)]
        [SerializeField] private float m_Falloff = 1.5f;

        [Range(2, 16)]
        [SerializeField] private int m_QualitySteps = 6;

        [Range(2, 16)]
        [SerializeField] private int m_CornerSegments = 6;

        [Header("Çerçeve Boşluğu (Offset / Margin)")]
        [SerializeField] private float m_OffsetTop = 0f;
        [SerializeField] private float m_OffsetBottom = 0f;
        [SerializeField] private float m_OffsetLeft = 0f;
        [SerializeField] private float m_OffsetRight = 0f;

        public override Texture mainTexture => s_WhiteTexture;

        public ShadowShape shapeMode { get => m_ShapeMode; set { m_ShapeMode = value; SetVerticesDirty(); } }
        public bool enableNotch { get => m_EnableNotch; set { m_EnableNotch = value; SetVerticesDirty(); } }
        public float notchWidth { get => m_NotchWidth; set { m_NotchWidth = Mathf.Max(0f, value); SetVerticesDirty(); } }
        public float notchHeight { get => m_NotchHeight; set { m_NotchHeight = Mathf.Max(0f, value); SetVerticesDirty(); } }
        public float notchRadius { get => m_NotchRadius; set { m_NotchRadius = Mathf.Max(0f, value); SetVerticesDirty(); } }
        public float cornerRadius { get => m_CornerRadius; set { m_CornerRadius = Mathf.Max(0f, value); SetVerticesDirty(); } }
        public Color shadowColor { get => m_ShadowColor; set { m_ShadowColor = value; SetVerticesDirty(); } }
        public float intensity { get => m_Intensity; set { m_Intensity = Mathf.Clamp01(value); SetVerticesDirty(); } }
        public bool uniformSize { get => m_UniformSize; set { m_UniformSize = value; SetVerticesDirty(); } }
        public float globalSize { get => m_GlobalSize; set { m_GlobalSize = Mathf.Max(0f, value); SetVerticesDirty(); } }
        public float topSize { get => m_TopSize; set { m_TopSize = Mathf.Max(0f, value); SetVerticesDirty(); } }
        public float bottomSize { get => m_BottomSize; set { m_BottomSize = Mathf.Max(0f, value); SetVerticesDirty(); } }
        public float leftSize { get => m_LeftSize; set { m_LeftSize = Mathf.Max(0f, value); SetVerticesDirty(); } }
        public float rightSize { get => m_RightSize; set { m_RightSize = Mathf.Max(0f, value); SetVerticesDirty(); } }
        public float falloff { get => m_Falloff; set { m_Falloff = Mathf.Max(0.1f, value); SetVerticesDirty(); } }

        protected override void Reset()
        {
            base.Reset();
            raycastTarget = false;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
            SetMaterialDirty();
        }

        /// <summary>
        /// Unity Simulator veya cihaz ekranının güvenli alanından (Safe Area) çentik ve köşe ölçülerini otomatik hesaplar.
        /// </summary>
        public void AutoDetectNotchFromSafeArea()
        {
            Rect safe = Screen.safeArea;
            float sW = Screen.width;
            float sH = Screen.height;

            Rect r = rectTransform.rect;
            if (sW > 0 && sH > 0 && r.width > 0 && r.height > 0)
            {
                float scaleX = r.width / sW;
                float scaleY = r.height / sH;

                float topInset = (sH - safe.yMax) * scaleY;
                if (topInset > 8f)
                {
                    m_EnableNotch = true;
                    m_NotchHeight = topInset;
                    m_NotchWidth = Mathf.Clamp(r.width * 0.38f, 220f, 480f);
                    m_NotchRadius = Mathf.Clamp(topInset * 0.35f, 10f, 30f);
                }
                else
                {
                    m_EnableNotch = false;
                }

                float bottomInset = safe.yMin * scaleY;
                if (bottomInset > 10f)
                {
                    m_CornerRadius = Mathf.Clamp(bottomInset * 1.2f, 30f, 75f);
                }

                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect r = rectTransform.rect;
            float xMin = r.xMin + m_OffsetLeft;
            float xMax = r.xMax - m_OffsetRight;
            float yMin = r.yMin + m_OffsetBottom;
            float yMax = r.yMax - m_OffsetTop;

            if (xMax <= xMin || yMax <= yMin)
                return;

            if (m_ShapeMode == ShadowShape.PhoneWithNotch)
            {
                PopulateNotchedPhoneMesh(vh, xMin, xMax, yMin, yMax);
            }
            else
            {
                PopulateRectangleMesh(vh, xMin, xMax, yMin, yMax);
            }
        }

        private void PopulateNotchedPhoneMesh(VertexHelper vh, float xMin, float xMax, float yMin, float yMax)
        {
            float width = xMax - xMin;
            float height = yMax - yMin;
            float cx = (xMin + xMax) * 0.5f;

            float cr = Mathf.Min(m_CornerRadius, Mathf.Min(width, height) * 0.4f);
            bool hasNotch = m_EnableNotch && m_NotchWidth > 0f && m_NotchHeight > 0f;
            float nw = hasNotch ? Mathf.Min(m_NotchWidth * 0.5f, width * 0.45f) : 0f;
            float nh = hasNotch ? Mathf.Min(m_NotchHeight, height * 0.35f) : 0f;
            float nr = hasNotch ? Mathf.Min(m_NotchRadius, Mathf.Min(nw * 0.45f, nh * 0.45f)) : 0f;

            int cornerSegs = Mathf.Max(2, m_CornerSegments);

            List<Vector2> pts = new List<Vector2>(64);
            List<Vector2> nrms = new List<Vector2>(64);

            // 1. Sol Üst Köşe (Arc from PI to PI/2)
            if (cr > 0f)
            {
                Vector2 c = new Vector2(xMin + cr, yMax - cr);
                for (int i = 0; i <= cornerSegs; i++)
                {
                    float a = Mathf.Lerp(Mathf.PI, Mathf.PI * 0.5f, (float)i / cornerSegs);
                    pts.Add(new Vector2(c.x + cr * Mathf.Cos(a), c.y + cr * Mathf.Sin(a)));
                    nrms.Add(new Vector2(-Mathf.Cos(a), -Mathf.Sin(a)));
                }
            }
            else
            {
                pts.Add(new Vector2(xMin, yMax));
                nrms.Add(new Vector2(0.707f, -0.707f));
            }

            // 2. Sol Kulak (Left Ear) & Çentik (Notch)
            if (hasNotch)
            {
                // Sol kulak düzlüğü
                pts.Add(new Vector2(cx - nw - nr, yMax));
                nrms.Add(new Vector2(0f, -1f));

                // Çentik sol iniş yuvarlaması
                if (nr > 0f)
                {
                    Vector2 c = new Vector2(cx - nw - nr, yMax - nr);
                    for (int i = 1; i <= cornerSegs; i++)
                    {
                        float a = Mathf.Lerp(Mathf.PI * 0.5f, 0f, (float)i / cornerSegs);
                        pts.Add(new Vector2(c.x + nr * Mathf.Cos(a), c.y + nr * Mathf.Sin(a)));
                        nrms.Add(new Vector2(-Mathf.Cos(a), -Mathf.Sin(a)));
                    }
                }

                // Çentik sol dikey duvarı
                pts.Add(new Vector2(cx - nw, yMax - nh + nr));
                nrms.Add(new Vector2(-1f, 0f));

                // Çentik sol alt köşe yuvarlaması
                if (nr > 0f)
                {
                    Vector2 c = new Vector2(cx - nw + nr, yMax - nh + nr);
                    for (int i = 1; i <= cornerSegs; i++)
                    {
                        float a = Mathf.Lerp(Mathf.PI, Mathf.PI * 1.5f, (float)i / cornerSegs);
                        pts.Add(new Vector2(c.x + nr * Mathf.Cos(a), c.y + nr * Mathf.Sin(a)));
                        nrms.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
                    }
                }

                // Çentik alt yatay tabanı
                pts.Add(new Vector2(cx + nw - nr, yMax - nh));
                nrms.Add(new Vector2(0f, -1f));

                // Çentik sağ alt köşe yuvarlaması
                if (nr > 0f)
                {
                    Vector2 c = new Vector2(cx + nw - nr, yMax - nh + nr);
                    for (int i = 1; i <= cornerSegs; i++)
                    {
                        float a = Mathf.Lerp(Mathf.PI * 1.5f, Mathf.PI * 2f, (float)i / cornerSegs);
                        pts.Add(new Vector2(c.x + nr * Mathf.Cos(a), c.y + nr * Mathf.Sin(a)));
                        nrms.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
                    }
                }

                // Çentik sağ dikey duvarı
                pts.Add(new Vector2(cx + nw, yMax - nr));
                nrms.Add(new Vector2(1f, 0f));

                // Çentik sağ çıkış yuvarlaması
                if (nr > 0f)
                {
                    Vector2 c = new Vector2(cx + nw + nr, yMax - nr);
                    for (int i = 1; i <= cornerSegs; i++)
                    {
                        float a = Mathf.Lerp(Mathf.PI, Mathf.PI * 0.5f, (float)i / cornerSegs);
                        pts.Add(new Vector2(c.x + nr * Mathf.Cos(a), c.y + nr * Mathf.Sin(a)));
                        nrms.Add(new Vector2(-Mathf.Cos(a), -Mathf.Sin(a)));
                    }
                }

                // Sağ kulak düzlüğü
                pts.Add(new Vector2(xMax - cr, yMax));
                nrms.Add(new Vector2(0f, -1f));
            }
            else
            {
                pts.Add(new Vector2(xMax - cr, yMax));
                nrms.Add(new Vector2(0f, -1f));
            }

            // 3. Sağ Üst Köşe (Arc from PI/2 to 0)
            if (cr > 0f)
            {
                Vector2 c = new Vector2(xMax - cr, yMax - cr);
                for (int i = 1; i <= cornerSegs; i++)
                {
                    float a = Mathf.Lerp(Mathf.PI * 0.5f, 0f, (float)i / cornerSegs);
                    pts.Add(new Vector2(c.x + cr * Mathf.Cos(a), c.y + cr * Mathf.Sin(a)));
                    nrms.Add(new Vector2(-Mathf.Cos(a), -Mathf.Sin(a)));
                }
            }

            // 4. Sağ Kenar Düzlüğü
            pts.Add(new Vector2(xMax, yMin + cr));
            nrms.Add(new Vector2(-1f, 0f));

            // 5. Sağ Alt Köşe (Arc from 0 to -PI/2)
            if (cr > 0f)
            {
                Vector2 c = new Vector2(xMax - cr, yMin + cr);
                for (int i = 1; i <= cornerSegs; i++)
                {
                    float a = Mathf.Lerp(0f, -Mathf.PI * 0.5f, (float)i / cornerSegs);
                    pts.Add(new Vector2(c.x + cr * Mathf.Cos(a), c.y + cr * Mathf.Sin(a)));
                    nrms.Add(new Vector2(-Mathf.Cos(a), -Mathf.Sin(a)));
                }
            }

            // 6. Alt Kenar Düzlüğü
            pts.Add(new Vector2(xMin + cr, yMin));
            nrms.Add(new Vector2(0f, 1f));

            // 7. Sol Alt Köşe (Arc from 3PI/2 to PI)
            if (cr > 0f)
            {
                Vector2 c = new Vector2(xMin + cr, yMin + cr);
                for (int i = 1; i <= cornerSegs; i++)
                {
                    float a = Mathf.Lerp(Mathf.PI * 1.5f, Mathf.PI, (float)i / cornerSegs);
                    pts.Add(new Vector2(c.x + cr * Mathf.Cos(a), c.y + cr * Mathf.Sin(a)));
                    nrms.Add(new Vector2(-Mathf.Cos(a), -Mathf.Sin(a)));
                }
            }

            // 8. Sol Kenar Düzlüğü (Kapanış)
            pts.Add(new Vector2(xMin, yMax - cr));
            nrms.Add(new Vector2(1f, 0f));

            // Kontur boyunca gölge halkalarını oluştur
            int count = pts.Count;
            int steps = Mathf.Max(1, m_QualitySteps);

            float[] sizes = new float[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 n = nrms[i].normalized;
                nrms[i] = n;

                float s = m_GlobalSize;
                if (!m_UniformSize)
                {
                    float szX = n.x > 0 ? (m_EnableLeft ? m_LeftSize : 0f) : (m_EnableRight ? m_RightSize : 0f);
                    float szY = n.y > 0 ? (m_EnableBottom ? m_BottomSize : 0f) : (m_EnableTop ? m_TopSize : 0f);
                    s = Mathf.Abs(n.x) * szX + Mathf.Abs(n.y) * szY;
                }
                else
                {
                    // Kenar toggle kontrolü
                    if ((n.y < -0.4f && !m_EnableTop) ||
                        (n.y > 0.4f && !m_EnableBottom) ||
                        (n.x > 0.4f && !m_EnableLeft) ||
                        (n.x < -0.4f && !m_EnableRight))
                    {
                        s = 0f;
                    }
                }

                sizes[i] = Mathf.Clamp(s, 0f, Mathf.Min(width, height) * 0.45f);
            }

            for (int k = 0; k < steps; k++)
            {
                float t0 = (float)k / steps;
                float t1 = (float)(k + 1) / steps;
                Color c0 = CalculateColor(t0);
                Color c1 = CalculateColor(t1);

                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count;

                    Vector2 p0 = pts[i] + nrms[i] * (sizes[i] * t0);
                    Vector2 p1 = pts[next] + nrms[next] * (sizes[next] * t0);
                    Vector2 p2 = pts[next] + nrms[next] * (sizes[next] * t1);
                    Vector2 p3 = pts[i] + nrms[i] * (sizes[i] * t1);

                    AddQuad(vh, p0, p1, p2, p3, c0, c0, c1, c1);
                }
            }
        }

        private void PopulateRectangleMesh(VertexHelper vh, float xMin, float xMax, float yMin, float yMax)
        {
            float top = m_EnableTop ? (m_UniformSize ? m_GlobalSize : m_TopSize) : 0f;
            float bottom = m_EnableBottom ? (m_UniformSize ? m_GlobalSize : m_BottomSize) : 0f;
            float left = m_EnableLeft ? (m_UniformSize ? m_GlobalSize : m_LeftSize) : 0f;
            float right = m_EnableRight ? (m_UniformSize ? m_GlobalSize : m_RightSize) : 0f;

            float maxH = (yMax - yMin) * 0.5f;
            float maxW = (xMax - xMin) * 0.5f;

            top = Mathf.Clamp(top, 0f, maxH);
            bottom = Mathf.Clamp(bottom, 0f, maxH);
            left = Mathf.Clamp(left, 0f, maxW);
            right = Mathf.Clamp(right, 0f, maxW);

            float ixMin = xMin + left;
            float ixMax = xMax - right;
            float iyMin = yMin + bottom;
            float iyMax = yMax - top;

            int steps = Mathf.Max(1, m_QualitySteps);
            int cornerSegs = Mathf.Max(1, m_CornerSegments);

            // 1. Üst Kenar
            if (top > 0f)
            {
                for (int k = 0; k < steps; k++)
                {
                    float t0 = (float)k / steps;
                    float t1 = (float)(k + 1) / steps;
                    float y0 = Mathf.Lerp(yMax, iyMax, t0);
                    float y1 = Mathf.Lerp(yMax, iyMax, t1);
                    Color c0 = CalculateColor(t0);
                    Color c1 = CalculateColor(t1);

                    AddQuad(vh,
                        new Vector2(ixMin, y0),
                        new Vector2(ixMax, y0),
                        new Vector2(ixMax, y1),
                        new Vector2(ixMin, y1),
                        c0, c0, c1, c1);
                }
            }

            // 2. Alt Kenar
            if (bottom > 0f)
            {
                for (int k = 0; k < steps; k++)
                {
                    float t0 = (float)k / steps;
                    float t1 = (float)(k + 1) / steps;
                    float y0 = Mathf.Lerp(yMin, iyMin, t0);
                    float y1 = Mathf.Lerp(yMin, iyMin, t1);
                    Color c0 = CalculateColor(t0);
                    Color c1 = CalculateColor(t1);

                    AddQuad(vh,
                        new Vector2(ixMin, y0),
                        new Vector2(ixMax, y0),
                        new Vector2(ixMax, y1),
                        new Vector2(ixMin, y1),
                        c0, c0, c1, c1);
                }
            }

            // 3. Sol Kenar
            if (left > 0f)
            {
                for (int k = 0; k < steps; k++)
                {
                    float t0 = (float)k / steps;
                    float t1 = (float)(k + 1) / steps;
                    float x0 = Mathf.Lerp(xMin, ixMin, t0);
                    float x1 = Mathf.Lerp(xMin, ixMin, t1);
                    Color c0 = CalculateColor(t0);
                    Color c1 = CalculateColor(t1);

                    AddQuad(vh,
                        new Vector2(x0, iyMax),
                        new Vector2(x1, iyMax),
                        new Vector2(x1, iyMin),
                        new Vector2(x0, iyMin),
                        c0, c1, c1, c0);
                }
            }

            // 4. Sağ Kenar
            if (right > 0f)
            {
                for (int k = 0; k < steps; k++)
                {
                    float t0 = (float)k / steps;
                    float t1 = (float)(k + 1) / steps;
                    float x0 = Mathf.Lerp(xMax, ixMax, t0);
                    float x1 = Mathf.Lerp(xMax, ixMax, t1);
                    Color c0 = CalculateColor(t0);
                    Color c1 = CalculateColor(t1);

                    AddQuad(vh,
                        new Vector2(x0, iyMax),
                        new Vector2(x0, iyMin),
                        new Vector2(x1, iyMin),
                        new Vector2(x1, iyMax),
                        c0, c0, c1, c1);
                }
            }

            // 5. Köşeler
            if (left > 0f && top > 0f)
                DrawCorner(vh, ixMin, iyMax, -left, top, Mathf.PI * 0.5f, Mathf.PI, steps, cornerSegs);

            if (right > 0f && top > 0f)
                DrawCorner(vh, ixMax, iyMax, right, top, 0f, Mathf.PI * 0.5f, steps, cornerSegs);

            if (right > 0f && bottom > 0f)
                DrawCorner(vh, ixMax, iyMin, right, -bottom, -Mathf.PI * 0.5f, 0f, steps, cornerSegs);

            if (left > 0f && bottom > 0f)
                DrawCorner(vh, ixMin, iyMin, -left, -bottom, Mathf.PI, Mathf.PI * 1.5f, steps, cornerSegs);
        }

        private void DrawCorner(VertexHelper vh, float cx, float cy, float rx, float ry, float angleStart, float angleEnd, int steps, int segments)
        {
            rx = Mathf.Abs(rx);
            ry = Mathf.Abs(ry);

            for (int s = 0; s < segments; s++)
            {
                float a0 = Mathf.Lerp(angleStart, angleEnd, (float)s / segments);
                float a1 = Mathf.Lerp(angleStart, angleEnd, (float)(s + 1) / segments);

                float cos0 = Mathf.Cos(a0);
                float sin0 = Mathf.Sin(a0);
                float cos1 = Mathf.Cos(a1);
                float sin1 = Mathf.Sin(a1);

                for (int k = 0; k < steps; k++)
                {
                    float t0 = (float)k / steps;
                    float t1 = (float)(k + 1) / steps;

                    float f0 = 1f - t0;
                    float f1 = 1f - t1;

                    Vector2 p0 = new Vector2(cx + f0 * rx * cos0, cy + f0 * ry * sin0);
                    Vector2 p1 = new Vector2(cx + f0 * rx * cos1, cy + f0 * ry * sin1);
                    Vector2 p2 = new Vector2(cx + f1 * rx * cos1, cy + f1 * ry * sin1);
                    Vector2 p3 = new Vector2(cx + f1 * rx * cos0, cy + f1 * ry * sin0);

                    Color c0 = CalculateColor(t0);
                    Color c1 = CalculateColor(t1);

                    AddQuad(vh, p0, p1, p2, p3, c0, c0, c1, c1);
                }
            }
        }

        private Color CalculateColor(float t)
        {
            float factor = Mathf.Pow(Mathf.Clamp01(1f - t), m_Falloff);
            Color c = m_ShadowColor;
            c.a *= m_Intensity * factor;
            return c;
        }

        private void AddQuad(VertexHelper vh, Vector2 v0, Vector2 v1, Vector2 v2, Vector2 v3, Color c0, Color c1, Color c2, Color c3)
        {
            int startIndex = vh.currentVertCount;

            UIVertex vert = UIVertex.simpleVert;

            vert.position = v0;
            vert.color = c0;
            vh.AddVert(vert);

            vert.position = v1;
            vert.color = c1;
            vh.AddVert(vert);

            vert.position = v2;
            vert.color = c2;
            vh.AddVert(vert);

            vert.position = v3;
            vert.color = c3;
            vh.AddVert(vert);

            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vh.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
        }
    }
}
