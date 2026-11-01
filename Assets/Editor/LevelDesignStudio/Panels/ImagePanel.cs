using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PixelGame.Editor.Studio
{
    /// <summary>
    /// Level resmi: renk indirgeme, gürültü temizleme, orijinale dönüş ve tuval. Hesaplama eski araçla aynı
    /// (PixelPaletteOptimizer), ama sonuç önce yan yana gösterilir; Uygula'ya basılana kadar diske ve level'a yazılmaz.
    /// </summary>
    public sealed class ImagePanel : VisualElement
    {
        private const string TargetKey = "PixelGame_Studio_TargetColors";

        private readonly SequenceEditService m_Edit;
        public event Action CanvasRequested;
        private readonly Image m_Current = new Image { scaleMode = ScaleMode.ScaleToFit };
        private readonly Image m_Next = new Image { scaleMode = ScaleMode.ScaleToFit };
        private readonly Label m_CurrentCaption = new Label(), m_NextCaption = new Label();
        private readonly VisualElement m_NextBox, m_Strip, m_Confirm;
        private readonly Label m_Status, m_ConfirmText;
        private readonly SliderInt m_Target;
        private readonly Button m_Revert;

        private Texture2D m_Proposed;
        private string m_ProposedSuffix;
        private bool m_ProposedRevert;

        public ImagePanel(SequenceEditService edit)
        {
            m_Edit = edit;
            AddToClassList("image-panel");

            var views = new VisualElement();
            views.AddToClassList("image-panel__views");
            views.Add(View("Şimdiki resim", m_Current, m_CurrentCaption));
            m_NextBox = View("Önizleme", m_Next, m_NextCaption);
            views.Add(m_NextBox);
            Add(views);

            m_Confirm = new VisualElement();
            m_Confirm.AddToClassList("preview");
            m_ConfirmText = new Label();
            m_ConfirmText.AddToClassList("preview__detail");
            m_ConfirmText.style.flexGrow = 1;
            m_ConfirmText.style.flexShrink = 1;
            m_Confirm.Add(m_ConfirmText);
            var apply = new Button(Apply) { text = "Uygula" };
            apply.AddToClassList("preview__apply");
            m_Confirm.Add(apply);
            m_Confirm.Add(new Button(CancelProposal) { text = "Vazgeç" });
            Add(m_Confirm);

            var tools = new VisualElement();
            tools.AddToClassList("image-panel__tools");
            Add(tools);

            var colors = Card("Renkler");
            m_Status = new Label();
            colors.Add(m_Status);
            m_Strip = new VisualElement();
            m_Strip.AddToClassList("image-panel__strip");
            colors.Add(m_Strip);
            tools.Add(colors);

            var reduce = Card("Renk indirgeme");
            m_Target = new SliderInt("Hedef renk", 2, 16) { showInputField = true };
            m_Target.SetValueWithoutNotify(EditorPrefs.GetInt(TargetKey, 6));
            m_Target.RegisterValueChangedCallback(e => EditorPrefs.SetInt(TargetKey, e.newValue));
            reduce.Add(m_Target);
            var quick = new VisualElement();
            quick.AddToClassList("automation__row");
            foreach (int n in new[] { 3, 4, 5, 6, 7, 8 })
            {
                int target = n;
                quick.Add(new Button(() => { m_Target.value = target; ProposeReduce(target); }) { text = n.ToString() });
            }
            reduce.Add(quick);
            reduce.Add(new Button(() => ProposeReduce(m_Target.value)) { text = "İndirgemeyi önizle", tooltip = "Her zaman orijinal resimden indirger" });
            tools.Add(reduce);

            var clean = Card("Temizlik");
            clean.Add(new Button(ProposeDenoise) { text = "Gürültü temizle", tooltip = "Tek başına kalan pikselleri komşu renge çevirir" });
            m_Revert = new Button(ProposeRevert) { text = "Orijinale dön", tooltip = "Resmi indirgemeden önceki hâline döndürür" };
            clean.Add(m_Revert);
            clean.Add(new Button(() => CanvasRequested?.Invoke()) { text = "Tuvalde düzenle", tooltip = "Tuval sekmesini açar" });
            tools.Add(clean);

            RegisterCallback<DetachFromPanelEvent>(_ => DiscardProposal());
        }

        private static VisualElement View(string title, Image image, Label caption)
        {
            var box = new VisualElement();
            box.AddToClassList("image-panel__view");
            var label = new Label(title);
            label.AddToClassList("automation__title");
            box.Add(label);
            image.AddToClassList("image-panel__image");
            box.Add(image);
            caption.AddToClassList("level-preview__caption");
            box.Add(caption);
            return box;
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

        private void DiscardProposal()
        {
            if (m_Proposed != null) UnityEngine.Object.DestroyImmediate(m_Proposed);
            m_Proposed = null;
            m_ProposedRevert = false;
        }

        public void CancelProposal()
        {
            DiscardProposal();
            Refresh();
        }

        private void Propose(Texture2D texture, string suffix)
        {
            DiscardProposal();
            if (texture == null) return;
            texture.filterMode = FilterMode.Point;
            texture.hideFlags = HideFlags.HideAndDontSave;
            m_Proposed = texture;
            m_ProposedSuffix = suffix;
            Refresh();
        }

        public void ProposeReduce(int colors)
        {
            var level = m_Edit.Level;
            var source = level != null ? level.GetOriginalTexture() : null;
            if (source != null) Propose(PixelPaletteOptimizer.Quantize(source, colors), $"_{colors}c");
        }

        public void ProposeDenoise()
        {
            var level = m_Edit.Level;
            var source = level != null ? level.GetOriginalTexture() : null;
            if (source == null) return;
            PixelPaletteOptimizer.EnsureReadable(source);
            Propose(PixelPaletteOptimizer.CleanIsolatedPixels(source), "_denoise");
        }

        private void ProposeRevert()
        {
            DiscardProposal();
            m_ProposedRevert = true;
            Refresh();
        }

        private void Apply()
        {
            var level = m_Edit.Level;
            if (level == null) return;
            if (m_ProposedRevert) m_Edit.RevertImage();
            else if (m_Proposed != null)
            {
                var source = level.GetOriginalTexture();
                var saved = PixelPaletteOptimizer.SaveAsOptimizedAsset(source, m_Proposed, m_ProposedSuffix);
                if (saved != null) m_Edit.ReplaceImage(saved);
            }
            DiscardProposal();
            Refresh();
        }

        public void Refresh()
        {
            var level = m_Edit.Level;
            SetEnabled(level != null);
            if (level == null) return;
            var active = level.GetActiveTexture();
            m_Current.image = active;
            int count = active != null ? PixelPaletteOptimizer.CountUniqueColors(active) : 0;
            m_CurrentCaption.text = active != null ? $"{active.width}×{active.height} · {count} renk" : "Resim yok";
            m_Status.text = $"{level.ColorPalette.Count} renk · {level.GetTotalCubeCountInPalette()} küp";

            m_Strip.Clear();
            for (int p = 0; p < level.ColorPalette.Count; p++)
            {
                var e = level.ColorPalette[p];
                if (e == null || e.pixelCount <= 0) continue;
                var chip = new VisualElement();
                chip.AddToClassList("image-panel__chip");
                var swatch = new VisualElement();
                swatch.AddToClassList("swatch");
                swatch.style.backgroundColor = e.targetColor;
                chip.Add(swatch);
                chip.Add(new Label(e.pixelCount.ToString()));
                m_Strip.Add(chip);
            }

            var original = level.OriginalSourceTexture;
            m_Revert.SetEnabled(original != null && original != active);

            bool pending = m_Proposed != null || m_ProposedRevert;
            m_Confirm.style.display = pending ? DisplayStyle.Flex : DisplayStyle.None;
            m_NextBox.style.display = pending ? DisplayStyle.Flex : DisplayStyle.None;
            if (!pending) return;
            Texture2D next = m_ProposedRevert ? original : m_Proposed;
            m_Next.image = next;
            m_NextCaption.text = next != null ? $"{next.width}×{next.height} · {PixelPaletteOptimizer.CountUniqueColors(next)} renk" : "";
            int ships = m_Edit.Sequence?.Count ?? 0;
            m_ConfirmText.text = $"Uygularsan palet yeniden taranır ve {ships} gemilik sıra silinip palete göre yeniden üretilir" +
                                 (m_ProposedRevert ? "." : "; yeni resim Assets/PixelArt/Optimized klasörüne yazılır.") + " Güçlü Konteyner, halat ve gizli ayarları gider. Sıra Ctrl+Z ile geri alınır.";
        }
    }
}
