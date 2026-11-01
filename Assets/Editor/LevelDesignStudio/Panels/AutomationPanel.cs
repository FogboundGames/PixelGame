using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PixelGame.Editor.Studio
{
    /// <summary>
    /// Eski araçtaki otomatik gemi araçları: havuz boyutu, gemi sayıları, halat/gizli oranı ve sıra üretimi.
    /// Sırayı değiştiren her işlem önce önizleme olarak gelir; ayarlar anında uygulanır ve Ctrl+Z ile geri alınır.
    /// </summary>
    public sealed class AutomationPanel : VisualElement
    {
        private const string LinkPrefKey = "PixelGame_GenLinkPercent";
        private const string HiddenPrefKey = "PixelGame_GenHiddenPercent";
        private const string OpenKey = "PixelGame_Studio_AutomationOpen";

        private readonly SequenceEditService m_Edit;
        public event Action<SequenceEditService.PendingChange> ProposalReady;

        private readonly Foldout m_Foldout;
        private readonly IntegerField m_Rows, m_Cols, m_Min, m_Max, m_Peak;
        private readonly DropdownField m_Mode;
        private readonly SliderInt m_Round, m_Link, m_Hidden;
        private readonly Label m_LinkState;
        private readonly Button m_Complete;

        public AutomationPanel(SequenceEditService edit)
        {
            m_Edit = edit;
            AddToClassList("automation");
            m_Foldout = new Foldout { text = "Otomatik araçlar", value = SessionState.GetBool(OpenKey, true) };
            m_Foldout.RegisterValueChangedCallback(e => SessionState.SetBool(OpenKey, e.newValue));
            Add(m_Foldout);
            var cards = new VisualElement();
            cards.AddToClassList("automation__cards");
            m_Foldout.Add(cards);

            var pool = Card("Havuz");
            m_Rows = Number("Sıra", "Oyun başında kaç sıra gemi bekler. Son level'larda 3.");
            m_Rows.RegisterValueChangedCallback(e => SetPool(e.newValue, m_Edit.Level.PoolColumns));
            pool.Add(m_Rows);
            m_Cols = Number("Sütun", "Yan yana kaç sütun gemi olur. Kolay'da 3, diğerlerinde çoğunlukla 4.");
            m_Cols.RegisterValueChangedCallback(e => SetPool(m_Edit.Level.PoolRows, e.newValue));
            pool.Add(m_Cols);
            cards.Add(pool);

            var numbers = Card("Gemi sayıları");
            m_Mode = new DropdownField("Mod", new List<string> { "10'un katları", "Karışık" }, 0)
            {
                tooltip = "10'un katları: sayılar çoğunlukla 10, 20...; Karışık: sayılar 'En sık' değerinin etrafında"
            };
            m_Mode.RegisterValueChangedCallback(_ => m_Edit.EditSettings("Gemi Sayı Modu", l => l.UseRoundCapacities = m_Mode.index == 0));
            numbers.Add(m_Mode);
            m_Min = Number("En az", "Bir geminin en küçük sayısı");
            m_Min.RegisterValueChangedCallback(e => SetClamped(m_Min, e, 1, m_Edit.Level.TruckCapacity, "Gemi Alt Sınırı", (l, v) => l.MinTruckCapacity = v));
            numbers.Add(m_Min);
            m_Max = Number("En çok", "Bir geminin en büyük sayısı. Sınır: Kolay 30, Orta 40, Zor 50.");
            m_Max.RegisterValueChangedCallback(e => SetClamped(m_Max, e, 1, 64, "Gemi Üst Sınırı", (l, v) =>
            {
                l.TruckCapacity = v;
                if (l.MinTruckCapacity > v) l.MinTruckCapacity = v;
                if (l.PeakTruckCapacity > v) l.PeakTruckCapacity = v;
            }));
            numbers.Add(m_Max);
            m_Round = new SliderInt("Yuvarlak %", 0, 100) { showInputField = true, tooltip = "10'un katı olan gemilerin oranı" };
            m_Round.RegisterValueChangedCallback(e => m_Edit.EditSettings("Yuvarlak Sayı Oranı", l => l.RoundCapacityRatio = e.newValue / 100f));
            numbers.Add(m_Round);
            m_Peak = Number("En sık", "Sayıların yoğunlaştığı değer");
            m_Peak.RegisterValueChangedCallback(e => SetClamped(m_Peak, e, m_Edit.Level.MinTruckCapacity, m_Edit.Level.TruckCapacity, "Gemi Yoğunluğu", (l, v) => l.PeakTruckCapacity = v));
            numbers.Add(m_Peak);
            numbers.Add(Action("Sayıları karıştır", "Renk sırasını bozmadan gemi sayılarını bu ayarlara göre yeniden dağıtır. Renk başına toplam korunur.",
                () => Propose(m_Edit.ProposeVaryCapacities)));
            cards.Add(numbers);

            var ropes = Card("Halat ve gizli");
            m_Link = new SliderInt("Halat %", 0, 60) { showInputField = true, tooltip = "Gemilerin yaklaşık bu kadarı halatla bağlanır" };
            m_Link.SetValueWithoutNotify(EditorPrefs.GetInt(LinkPrefKey, 20));
            m_Link.RegisterValueChangedCallback(e => EditorPrefs.SetInt(LinkPrefKey, e.newValue));
            ropes.Add(m_Link);
            m_Hidden = new SliderInt("Gizli %", 0, 60) { showInputField = true, tooltip = "Gemilerin yaklaşık bu kadarı gizli olur. Ön sıra ve halatlılar gizlenmez." };
            m_Hidden.SetValueWithoutNotify(EditorPrefs.GetInt(HiddenPrefKey, 15));
            m_Hidden.RegisterValueChangedCallback(e => EditorPrefs.SetInt(HiddenPrefKey, e.newValue));
            ropes.Add(m_Hidden);
            m_LinkState = new Label();
            m_LinkState.AddToClassList("automation__state");
            ropes.Add(m_LinkState);
            ropes.Add(Action("Halat ve gizli üret", "Mevcut halatları ve gizli gemileri silip oranlara göre yeniden dağıtır. Güçlü Konteyner'ler halatlanmaz ve gizlenmez.",
                () => Propose(() => m_Edit.ProposeLinksAndHidden(m_Link.value / 100f, m_Hidden.value / 100f))));
            cards.Add(ropes);

            var order = Card("Sıra üretimi");
            var smart = new VisualElement();
            smart.AddToClassList("automation__row");
            smart.Add(Action("Kolay", "Açık dış renkler öne, gömülü renkler sona. Mevcut sıranın yerine geçer.", () => Propose(() => m_Edit.ProposeSmartSort(SmartSortMode.Easy))));
            smart.Add(Action("Dengeli", "Açık renkler dönüşümlü dağıtılır. Mevcut sıranın yerine geçer.", () => Propose(() => m_Edit.ProposeSmartSort(SmartSortMode.Balanced))));
            smart.Add(Action("Zor", "Gömülü renkler erken girer. Mevcut sıranın yerine geçer.", () => Propose(() => m_Edit.ProposeSmartSort(SmartSortMode.Hard))));
            var smartTitle = new Label("Akıllı sırala");
            smartTitle.AddToClassList("automation__state");
            order.Add(smartTitle);
            order.Add(smart);
            order.Add(Action("Renkleri karışık dağıt", "Palete göre sıfırdan, renkleri dönüşümlü sıra üretir. Mevcut sıranın yerine geçer.", () => Propose(m_Edit.ProposeInterleaved)));
            m_Complete = Action("Eksikleri tamamla", "Kapasitesi eksik renkler için sıranın sonuna gemi ekler", () => m_Edit.AddMissingShips());
            order.Add(m_Complete);
            var clear = Action("Sırayı temizle", "Bütün gemileri siler", () => Propose(m_Edit.ProposeClear));
            clear.AddToClassList("inspector__danger");
            order.Add(clear);
            cards.Add(order);
        }

        private void Propose(Func<SequenceEditService.PendingChange> make)
        {
            var change = make();
            if (change != null) ProposalReady?.Invoke(change);
        }

        private void SetPool(int rows, int cols)
        {
            rows = Mathf.Clamp(rows, 1, 12);
            cols = Mathf.Clamp(cols, 1, 8);
            m_Edit.EditSettings("Havuz Boyutu", l =>
            {
                l.PoolRows = rows;
                l.PoolColumns = cols;
            });
        }

        private void SetClamped(IntegerField field, ChangeEvent<int> e, int min, int max, string undo, Action<PixelLevelData, int> set)
        {
            int v = Mathf.Clamp(e.newValue, min, Mathf.Max(min, max));
            if (v != e.newValue) field.SetValueWithoutNotify(v);
            m_Edit.EditSettings(undo, l => set(l, v));
        }

        private static VisualElement Card(string title)
        {
            var card = new VisualElement();
            card.AddToClassList("automation__card");
            var label = new Label(title);
            label.AddToClassList("automation__title");
            card.Add(label);
            return card;
        }

        private static IntegerField Number(string label, string tip) => new IntegerField(label) { isDelayed = true, tooltip = tip };

        private static Button Action(string text, string tip, Action action) => new Button(action) { text = text, tooltip = tip };

        /// <summary>Alanları seçili level'ın değerlerine eşitler.</summary>
        public void Refresh()
        {
            var level = m_Edit.Level;
            SetEnabled(level != null);
            if (level == null) return;
            m_Rows.SetValueWithoutNotify(level.PoolRows);
            m_Cols.SetValueWithoutNotify(level.PoolColumns);
            m_Mode.SetValueWithoutNotify(level.UseRoundCapacities ? m_Mode.choices[0] : m_Mode.choices[1]);
            m_Min.SetValueWithoutNotify(level.MinTruckCapacity);
            m_Max.SetValueWithoutNotify(level.TruckCapacity);
            m_Peak.SetValueWithoutNotify(level.PeakTruckCapacity);
            m_Round.SetValueWithoutNotify(Mathf.RoundToInt(level.RoundCapacityRatio * 100f));
            m_Round.style.display = level.UseRoundCapacities ? DisplayStyle.Flex : DisplayStyle.None;
            m_Peak.style.display = level.UseRoundCapacities ? DisplayStyle.None : DisplayStyle.Flex;

            var ropes = new HashSet<int>();
            int hidden = 0;
            if (m_Edit.Sequence != null)
                foreach (var w in m_Edit.Sequence)
                {
                    if (w == null) continue;
                    if (w.linkId > 0) ropes.Add(w.linkId);
                    if (w.isHidden) hidden++;
                }
            m_LinkState.text = $"Şu an: {ropes.Count} halat · {hidden} gizli gemi";

            int missing = m_Edit.MissingCubes();
            m_Complete.text = missing > 0 ? $"Eksikleri tamamla (+{missing})" : "Eksikleri tamamla";
            m_Complete.SetEnabled(missing > 0);
        }
    }
}
