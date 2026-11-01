using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PixelGame.Editor.Studio
{
    /// <summary>Seçili geminin ayarları; içerik geminin türüne göre değişir. Değişiklikler SequenceEditService'ten geçer.</summary>
    public sealed class InspectorPanel : VisualElement
    {
        private readonly SequenceEditService m_Edit;
        public event Action<int> LinkModeRequested;
        public event Action<string> Notify;
        public event Action<int> RemoveRequested;

        public InspectorPanel(SequenceEditService edit)
        {
            m_Edit = edit;
            AddToClassList("inspector");
        }

        public void Show(int index, int columns)
        {
            Clear();
            var seq = m_Edit.Sequence;
            if (seq == null || index < 0 || index >= seq.Count || seq[index] == null)
            {
                var hint = new Label("Düzenlemek için bir gemi seç.\nKartları sürükleyerek takas edebilirsin.");
                hint.AddToClassList("inspector__hint");
                Add(hint);
                return;
            }

            var w = seq[index];
            Add(Title(w.isDualColor ? $"#{index + 1} Güçlü Konteyner" : $"#{index + 1} Gemi"));
            var where = new Label($"Sütun {index % columns + 1} · Dalga {index / columns + 1}");
            where.AddToClassList("inspector__sub");
            Add(where);

            Add(ColorRow(w.isDualColor ? "1. Renk" : "Renk", w.wagonColor, w.isDualColor ? w.secondaryColor : (Color?)null, p => m_Edit.SetColor(index, p)));
            Add(CapacityRow(w.isDualColor ? "1. Kapasite" : "Kapasite", w.capacity, v => m_Edit.SetCapacity(index, v)));

            if (w.isDualColor)
            {
                int cap2 = w.secondaryCapacity > 0 ? w.secondaryCapacity : w.capacity;
                Add(ColorRow("2. Renk", w.secondaryColor, w.wagonColor, p => m_Edit.SetSecondaryColor(index, p)));
                Add(CapacityRow("2. Kapasite", cap2, v => m_Edit.SetSecondaryCapacity(index, v)));
                var total = new Label($"Toplam {w.capacity + cap2}");
                total.AddToClassList("inspector__sub");
                Add(total);
                Add(Action("Normal gemiye çevir", () => m_Edit.ClearDual(index)));
            }
            else
            {
                var hidden = new Toggle("Gizli") { value = w.isHidden, tooltip = "Ön sıraya gelene kadar rengi ve sayısı örtülü kalır" };
                hidden.RegisterValueChangedCallback(e => m_Edit.SetHidden(index, e.newValue));
                Add(hidden);

                if (w.linkId > 0)
                {
                    int partner = m_Edit.Level.GetLinkedPartnerIndex(index);
                    Add(new Label(partner >= 0 ? $"Halat: #{partner + 1} ile bağlı" : "Halat: ortağı yok"));
                    Add(Action("Halatı kaldır", () => m_Edit.Unlink(index)));
                }
                else if (!w.isHidden)
                {
                    Add(Action("Halat ekle", () => LinkModeRequested?.Invoke(index)));
                }

                Add(Action("Güçlü Konteyner yap", () =>
                {
                    if (!m_Edit.MakeDual(index)) Notify?.Invoke("Güçlü Konteyner için bölümde en az 2 farklı renk olmalı");
                }));
            }

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            Add(spacer);
            var remove = Action("Gemiyi sil", () => RemoveRequested?.Invoke(index));
            remove.AddToClassList("inspector__danger");
            Add(remove);
        }

        private static Label Title(string text)
        {
            var label = new Label(text);
            label.AddToClassList("inspector__title");
            return label;
        }

        private static Button Action(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.AddToClassList("inspector__action");
            return button;
        }

        private VisualElement CapacityRow(string label, int value, Action<int> set)
        {
            var field = new IntegerField(label) { value = value, isDelayed = true };
            field.RegisterValueChangedCallback(e =>
            {
                if (e.newValue < 1) { field.SetValueWithoutNotify(e.previousValue); return; }
                set(e.newValue);
            });
            return field;
        }

        private VisualElement ColorRow(string label, Color current, Color? other, Action<int> set)
        {
            var row = new VisualElement();
            row.AddToClassList("inspector__row");
            var title = new Label(label);
            title.AddToClassList("inspector__row-label");
            row.Add(title);
            var swatch = new VisualElement();
            swatch.AddToClassList("swatch");
            swatch.style.backgroundColor = current;
            row.Add(swatch);
            int own = LevelSnapshot.FindColor(m_Edit.Level.ColorPalette, current);
            var button = new Button(() =>
            {
                var menu = new GenericMenu();
                var palette = m_Edit.Level.ColorPalette;
                for (int p = 0; p < palette.Count; p++)
                {
                    var e = palette[p];
                    if (e == null || e.pixelCount <= 0) continue;
                    if (other.HasValue && PaletteColorOverride.ColorsMatch(e.targetColor, other.Value, 0.04f)) continue;
                    int index = p;
                    menu.AddItem(new GUIContent($"{StudioColors.Name(e.targetColor, p)} ({e.pixelCount} küp)"), p == own, () => set(index));
                }
                menu.ShowAsContext();
            }) { text = own >= 0 ? StudioColors.Name(current, own) : "Palette yok" };
            button.style.flexGrow = 1;
            row.Add(button);
            return row;
        }
    }
}
