using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PixelGame.Editor.Studio
{
    /// <summary>
    /// Yakınlaştırılıp kaydırılabilen piksel yüzeyi. Çizim kararı vermez; fare olaylarını hücreye çevirip bildirir.
    /// Hücre koordinatı doku koordinatıdır: y aşağıdan yukarı.
    /// Tekerlek yakınlaştırır, orta tuş ya da Boşluk+sol tuş kaydırır.
    /// </summary>
    public sealed class CanvasSurface : VisualElement
    {
        private const float MinZoom = 3f, MaxZoom = 64f;

        public event Action<Vector2Int, int> CellDown;  // hücre, fare tuşu
        public event Action<Vector2Int> CellDrag;
        public event Action<Vector2Int> CellUp;
        public event Action<Vector2Int?> CellHover;

        private readonly VisualElement m_Content = new VisualElement { pickingMode = PickingMode.Ignore };
        private readonly Image m_Image = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.StretchToFill };
        private readonly Image m_Reference = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.StretchToFill };
        private readonly VisualElement m_Overlay = new VisualElement { pickingMode = PickingMode.Ignore };

        private int m_Width = 32, m_Height = 32;
        private float m_Zoom = 16f;
        private Vector2 m_Pan;
        private bool m_Fitted, m_Panning, m_Drawing, m_SpaceHeld;
        private Vector2 m_PanStart;
        private Vector2Int? m_LastCell;
        private readonly List<Vector2Int> m_Preview = new List<Vector2Int>();
        private Color m_PreviewColor = Color.white;

        public bool ShowGrid { get; set; } = true;
        /// <summary>Son basışta Alt tuşu basılı mıydı (kalemle renk alma için).</summary>
        public bool AltHeld { get; private set; }

        public CanvasSurface()
        {
            AddToClassList("canvas-surface");
            focusable = true;
            m_Content.AddToClassList("canvas-surface__content");
            m_Image.style.position = Position.Absolute;
            m_Reference.style.position = Position.Absolute;
            m_Overlay.style.position = Position.Absolute;
            foreach (var layer in new VisualElement[] { m_Image, m_Reference, m_Overlay })
            {
                layer.style.left = 0;
                layer.style.top = 0;
                layer.style.right = 0;
                layer.style.bottom = 0;
                m_Content.Add(layer);
            }
            m_Reference.style.display = DisplayStyle.None;
            Add(m_Content);
            m_Overlay.generateVisualContent += DrawOverlay;

            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<PointerDownEvent>(OnDown);
            RegisterCallback<PointerMoveEvent>(OnMove);
            RegisterCallback<PointerUpEvent>(OnUp);
            RegisterCallback<PointerLeaveEvent>(_ => { if (!m_Drawing) { m_LastCell = null; CellHover?.Invoke(null); } });
            RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Space) { m_SpaceHeld = true; e.StopPropagation(); } });
            RegisterCallback<KeyUpEvent>(e => { if (e.keyCode == KeyCode.Space) m_SpaceHeld = false; });
            RegisterCallback<GeometryChangedEvent>(_ => { if (!m_Fitted && contentRect.width > 1f) Fit(); });
        }

        public void SetTexture(Texture2D texture)
        {
            m_Image.image = texture;
            if (texture != null && (texture.width != m_Width || texture.height != m_Height))
            {
                m_Width = texture.width;
                m_Height = texture.height;
                m_Fitted = false;
                Fit();
            }
            m_Image.MarkDirtyRepaint();
            m_Overlay.MarkDirtyRepaint();
        }

        public void SetReference(Texture texture, float opacity)
        {
            m_Reference.image = texture;
            m_Reference.style.opacity = opacity;
            m_Reference.style.display = texture != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetPreview(IEnumerable<Vector2Int> cells, Color color)
        {
            m_Preview.Clear();
            if (cells != null) m_Preview.AddRange(cells);
            m_PreviewColor = color;
            m_Overlay.MarkDirtyRepaint();
        }

        public void RepaintOverlay() => m_Overlay.MarkDirtyRepaint();

        /// <summary>Resmi pencereye sığdırır ve ortalar.</summary>
        public void Fit()
        {
            var r = contentRect;
            if (r.width < 1f || r.height < 1f) return;
            m_Fitted = true;
            m_Zoom = Mathf.Clamp(Mathf.Floor(Mathf.Min((r.width - 16f) / m_Width, (r.height - 16f) / m_Height)), MinZoom, MaxZoom);
            m_Pan = new Vector2((r.width - m_Width * m_Zoom) * 0.5f, (r.height - m_Height * m_Zoom) * 0.5f);
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            m_Content.style.left = Mathf.Round(m_Pan.x);
            m_Content.style.top = Mathf.Round(m_Pan.y);
            m_Content.style.width = m_Width * m_Zoom;
            m_Content.style.height = m_Height * m_Zoom;
            m_Overlay.MarkDirtyRepaint();
        }

        private void OnWheel(WheelEvent e)
        {
            Vector2 mouse = e.localMousePosition;
            Vector2 cell = (mouse - m_Pan) / m_Zoom;
            float next = Mathf.Clamp(e.delta.y < 0f ? m_Zoom * 1.25f : m_Zoom / 1.25f, MinZoom, MaxZoom);
            if (Mathf.Abs(next - m_Zoom) < 0.01f) return;
            m_Zoom = Mathf.Round(next);
            m_Pan = mouse - cell * m_Zoom;
            ApplyTransform();
            e.StopPropagation();
        }

        private Vector2Int CellAt(Vector2 local)
        {
            Vector2 c = (local - m_Pan) / m_Zoom;
            return new Vector2Int(Mathf.FloorToInt(c.x), m_Height - 1 - Mathf.FloorToInt(c.y));
        }

        private bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < m_Width && c.y < m_Height;

        private void OnDown(PointerDownEvent e)
        {
            Focus();
            this.CapturePointer(e.pointerId);
            if (e.button == 2 || (e.button == 0 && m_SpaceHeld))
            {
                m_Panning = true;
                m_PanStart = (Vector2)e.localPosition - m_Pan;
            }
            else
            {
                var cell = CellAt(e.localPosition);
                if (!InBounds(cell)) { this.ReleasePointer(e.pointerId); return; }
                m_Drawing = true;
                m_LastCell = cell;
                AltHeld = e.altKey;
                CellDown?.Invoke(cell, e.button);
            }
            e.StopPropagation();
        }

        private void OnMove(PointerMoveEvent e)
        {
            if (m_Panning)
            {
                m_Pan = (Vector2)e.localPosition - m_PanStart;
                ApplyTransform();
                return;
            }
            var cell = CellAt(e.localPosition);
            if (m_Drawing)
            {
                if (cell != m_LastCell)
                {
                    m_LastCell = cell;
                    CellDrag?.Invoke(cell); // Sınır dışı da bildirilir: şekil araçları kenara yapışır
                }
                return;
            }
            Vector2Int? hover = InBounds(cell) ? cell : (Vector2Int?)null;
            if (hover != m_LastCell)
            {
                m_LastCell = hover;
                CellHover?.Invoke(hover);
            }
        }

        private void OnUp(PointerUpEvent e)
        {
            if (this.HasPointerCapture(e.pointerId)) this.ReleasePointer(e.pointerId);
            if (m_Panning) { m_Panning = false; return; }
            if (!m_Drawing) return;
            m_Drawing = false;
            CellUp?.Invoke(CellAt(e.localPosition));
        }

        private void DrawOverlay(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            float w = m_Width * m_Zoom, h = m_Height * m_Zoom;
            if (ShowGrid && m_Zoom >= 6f)
            {
                p.lineWidth = 1f;
                p.strokeColor = new Color(1f, 1f, 1f, 0.08f);
                p.BeginPath();
                for (int x = 0; x <= m_Width; x++) { p.MoveTo(new Vector2(x * m_Zoom, 0f)); p.LineTo(new Vector2(x * m_Zoom, h)); }
                for (int y = 0; y <= m_Height; y++) { p.MoveTo(new Vector2(0f, y * m_Zoom)); p.LineTo(new Vector2(w, y * m_Zoom)); }
                p.Stroke();
            }
            p.lineWidth = 1f;
            p.strokeColor = new Color(1f, 1f, 1f, 0.45f);
            p.BeginPath();
            p.MoveTo(Vector2.zero); p.LineTo(new Vector2(w, 0f)); p.LineTo(new Vector2(w, h)); p.LineTo(new Vector2(0f, h)); p.ClosePath();
            p.Stroke();

            if (m_Preview.Count == 0) return;
            Color fill = m_PreviewColor;
            fill.a = 0.85f;
            p.fillColor = fill;
            foreach (var c in m_Preview)
            {
                float x = c.x * m_Zoom, y = (m_Height - 1 - c.y) * m_Zoom;
                p.BeginPath();
                p.MoveTo(new Vector2(x, y));
                p.LineTo(new Vector2(x + m_Zoom, y));
                p.LineTo(new Vector2(x + m_Zoom, y + m_Zoom));
                p.LineTo(new Vector2(x, y + m_Zoom));
                p.ClosePath();
                p.Fill();
            }
        }
    }
}
