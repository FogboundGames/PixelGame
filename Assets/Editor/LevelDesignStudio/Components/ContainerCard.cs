using UnityEngine;
using UnityEngine.UIElements;

namespace PixelGame.Editor.Studio
{
    /// <summary>
    /// Kompakt gemi kartı: renk, sıra numarası, kapasite ve küçük durum işaretleri. Düzenlenebilir kontrol içermez;
    /// ayarlar seçilince sağdaki Inspector'da açılır. Index &gt;= gemi sayısı ise sıranın sonundaki boş yerdir.
    /// </summary>
    public sealed class ContainerCard : VisualElement
    {
        public int Index { get; }
        public bool IsEmptySlot { get; }

        public ContainerCard(int index, WagonSequenceEntry wagon)
        {
            Index = index;
            IsEmptySlot = wagon == null;
            AddToClassList("card");
            if (IsEmptySlot)
            {
                AddToClassList("card--empty");
                Add(new Label("+") { pickingMode = PickingMode.Ignore });
                tooltip = "Sıranın sonuna gemi ekle (fırça seçiliyse o renkte) ya da buraya gemi sürükle";
                return;
            }

            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList("card__fill");
            var first = Half(wagon.wagonColor, wagon.capacity);
            fill.Add(first);
            if (wagon.isDualColor)
            {
                AddToClassList("card--dual");
                fill.Add(Half(wagon.secondaryColor, wagon.secondaryCapacity > 0 ? wagon.secondaryCapacity : wagon.capacity));
            }
            Add(fill);

            Color text = StudioColors.TextOn(wagon.wagonColor);
            var number = new Label($"#{index + 1}") { pickingMode = PickingMode.Ignore };
            number.AddToClassList("card__number");
            number.style.color = text;
            Add(number);

            int total = wagon.capacity + (wagon.isDualColor ? (wagon.secondaryCapacity > 0 ? wagon.secondaryCapacity : wagon.capacity) : 0);
            var capacity = new Label(total.ToString()) { pickingMode = PickingMode.Ignore };
            capacity.AddToClassList("card__capacity");
            capacity.style.color = text;
            Add(capacity);

            var marks = new VisualElement { pickingMode = PickingMode.Ignore };
            marks.AddToClassList("card__marks");
            if (wagon.isHidden) marks.Add(Mark("?", "Gizli gemi"));
            if (wagon.linkId > 0) marks.Add(Mark("H", $"Halat {wagon.linkId}"));
            if (wagon.isDualColor) marks.Add(Mark("G", "Güçlü Konteyner"));
            Add(marks);
        }

        private static VisualElement Half(Color color, int weight)
        {
            var half = new VisualElement { pickingMode = PickingMode.Ignore };
            half.style.flexGrow = Mathf.Max(1, weight);
            half.style.backgroundColor = color;
            return half;
        }

        private static Label Mark(string text, string tip)
        {
            var mark = new Label(text) { pickingMode = PickingMode.Ignore, tooltip = tip };
            mark.AddToClassList("card__mark");
            return mark;
        }

        public void SetSelected(bool selected) => EnableInClassList("card--selected", selected);
        public void SetLinkTarget(bool on) => EnableInClassList("card--link-target", on);
        public void SetDropTarget(bool on) => EnableInClassList("card--drop", on);
        public void SetSeverity(RuleSeverity? severity)
        {
            EnableInClassList("card--critical", severity == RuleSeverity.Critical);
            EnableInClassList("card--warning", severity == RuleSeverity.Warning);
        }
    }
}
