using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PixelGame.Editor.Studio
{
    /// <summary>Sütunlar ve dalgalar hâlinde gemi kartları; seçim, sürükleyerek takas ve halat çizgileri.</summary>
    public sealed class LaneBoardPanel : VisualElement
    {
        private const float DragThreshold = 6f;

        public event Action<int> CardClicked;          // Index: gemi ya da sondaki boş yer
        public event Action<int, int> SwapRequested;   // kaynak, hedef
        public event Action<int> MoveToEndRequested;

        private readonly ScrollView m_Scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
        private readonly VisualElement m_Grid = new VisualElement();
        private readonly VisualElement m_RopeLayer = new VisualElement { pickingMode = PickingMode.Ignore };
        private readonly List<ContainerCard> m_Cards = new List<ContainerCard>();
        private List<WagonSequenceEntry> m_Sequence;

        private ContainerCard m_Pressed, m_DropTarget;
        private Vector2 m_PressPos;
        private bool m_Dragging;
        private int m_PointerId = -1;
        private VisualElement m_Ghost;

        public LaneBoardPanel()
        {
            AddToClassList("board");
            m_Grid.AddToClassList("board__grid");
            m_Scroll.AddToClassList("board__scroll");
            m_Scroll.Add(m_Grid);
            Add(m_Scroll);

            m_RopeLayer.AddToClassList("board__ropes");
            m_RopeLayer.generateVisualContent += DrawRopes;

            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(_ => EndDrag());
        }

        public void Rebuild(List<WagonSequenceEntry> sequence, int columns, int poolRows, int selected, int linkFrom, List<RuleFinding> findings)
        {
            EndDrag();
            m_Sequence = sequence;
            m_Grid.Clear();
            m_Cards.Clear();
            if (sequence == null) return;

            var severity = new Dictionary<int, RuleSeverity>();
            foreach (var f in findings)
                foreach (int s in f.slots)
                    if (!severity.TryGetValue(s, out var cur) || f.severity < cur) severity[s] = f.severity;

            var header = Row();
            header.Add(RowLabel(""));
            for (int c = 0; c < columns; c++)
            {
                var title = new Label($"Sütun {c + 1}");
                title.AddToClassList("board__column-title");
                header.Add(title);
            }
            m_Grid.Add(header);

            // Sıranın sonunda bir satır boş yer: gemi eklemek ve sona taşımak için
            int count = sequence.Count;
            int total = (count / columns + 1) * columns;
            for (int r = 0; r * columns < total; r++)
            {
                if (r == poolRows)
                {
                    var divider = new Label("oyun başında görünmeyen gemiler");
                    divider.AddToClassList("board__divider");
                    m_Grid.Add(divider);
                }
                var row = Row();
                row.Add(RowLabel($"D{r + 1}"));
                for (int c = 0; c < columns; c++)
                {
                    int i = r * columns + c;
                    var wagon = i < count ? sequence[i] : null;
                    var card = new ContainerCard(i, wagon);
                    card.SetSelected(i == selected);
                    card.SetLinkTarget(linkFrom >= 0 && i == linkFrom);
                    if (i < count && severity.TryGetValue(i, out var sev) && sev != RuleSeverity.Info) card.SetSeverity(sev);
                    card.RegisterCallback<PointerDownEvent>(e => OnCardDown(card, e));
                    m_Cards.Add(card);
                    row.Add(card);
                }
                m_Grid.Add(row);
            }

            m_Grid.Add(m_RopeLayer);
            m_Grid.UnregisterCallback<GeometryChangedEvent>(OnGeometry);
            m_Grid.RegisterCallback<GeometryChangedEvent>(OnGeometry);
            m_RopeLayer.MarkDirtyRepaint();
        }

        private void OnGeometry(GeometryChangedEvent _) => m_RopeLayer.MarkDirtyRepaint();

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.AddToClassList("board__row");
            return row;
        }

        private static Label RowLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("board__row-label");
            return label;
        }

        private void DrawRopes(MeshGenerationContext ctx)
        {
            if (m_Sequence == null) return;
            var groups = new Dictionary<int, ContainerCard>();
            var p = ctx.painter2D;
            p.lineWidth = 3f;
            p.strokeColor = new Color(1f, 0.82f, 0.3f);
            p.lineCap = LineCap.Round;
            foreach (var card in m_Cards)
            {
                if (card.IsEmptySlot || card.Index >= m_Sequence.Count) continue;
                var w = m_Sequence[card.Index];
                if (w == null || w.linkId <= 0) continue;
                if (groups.TryGetValue(w.linkId, out var other))
                {
                    // Çizgi kartların kenarından kenarına: sayıların üstünden geçmesin
                    Vector2 from = Center(other), to = Center(card);
                    p.BeginPath();
                    p.MoveTo(EdgePoint(other, from, to));
                    p.LineTo(EdgePoint(card, to, from));
                    p.Stroke();
                }
                else groups[w.linkId] = card;
            }
        }

        private Vector2 Center(VisualElement card) => card.ChangeCoordinatesTo(m_RopeLayer, card.contentRect.center);

        /// <summary>Kartın merkezinden hedefe giden doğrunun kart kenarını kestiği nokta.</summary>
        private static Vector2 EdgePoint(VisualElement card, Vector2 center, Vector2 target)
        {
            Vector2 d = target - center;
            if (d == Vector2.zero) return center;
            float hw = card.layout.width * 0.5f, hh = card.layout.height * 0.5f;
            float scale = Mathf.Min(d.x != 0f ? hw / Mathf.Abs(d.x) : float.MaxValue, d.y != 0f ? hh / Mathf.Abs(d.y) : float.MaxValue);
            return center + d * scale;
        }

        private void OnCardDown(ContainerCard card, PointerDownEvent e)
        {
            if (e.button != 0) return;
            m_Pressed = card;
            m_PressPos = e.position;
            m_Dragging = false;
            m_PointerId = e.pointerId;
            this.CapturePointer(m_PointerId);
            e.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent e)
        {
            if (m_Pressed == null || !this.HasPointerCapture(e.pointerId)) return;
            if (!m_Dragging)
            {
                if (m_Pressed.IsEmptySlot || ((Vector2)e.position - m_PressPos).magnitude < DragThreshold) return;
                m_Dragging = true;
                m_Ghost = new ContainerCard(m_Pressed.Index, m_Sequence[m_Pressed.Index]) { pickingMode = PickingMode.Ignore };
                m_Ghost.AddToClassList("card--ghost");
                panel.visualTree.Add(m_Ghost);
            }
            m_Ghost.style.left = e.position.x - 36;
            m_Ghost.style.top = e.position.y - 26;

            var target = CardAt(e.position);
            if (target == m_Pressed) target = null;
            if (target != m_DropTarget)
            {
                m_DropTarget?.SetDropTarget(false);
                m_DropTarget = target;
                m_DropTarget?.SetDropTarget(true);
            }
        }

        private ContainerCard CardAt(Vector2 worldPos)
        {
            foreach (var card in m_Cards)
                if (card.worldBound.Contains(worldPos)) return card;
            return null;
        }

        private void OnPointerUp(PointerUpEvent e)
        {
            if (m_Pressed == null || !this.HasPointerCapture(e.pointerId)) return;
            var pressed = m_Pressed;
            var target = m_DropTarget;
            bool dragging = m_Dragging;
            this.ReleasePointer(e.pointerId);
            EndDrag();
            if (!dragging) CardClicked?.Invoke(pressed.Index);
            else if (target != null)
            {
                if (target.IsEmptySlot) MoveToEndRequested?.Invoke(pressed.Index);
                else SwapRequested?.Invoke(pressed.Index, target.Index);
            }
        }

        private void EndDrag()
        {
            m_DropTarget?.SetDropTarget(false);
            m_DropTarget = null;
            m_Pressed = null;
            m_Dragging = false;
            m_Ghost?.RemoveFromHierarchy();
            m_Ghost = null;
        }
    }
}
