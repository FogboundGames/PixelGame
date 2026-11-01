using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor.Studio
{
    /// <summary>
    /// Gemi sırasındaki bütün değişiklikler buradan geçer: her biri Unity Undo'ya kaydedilir, level'ı değişmiş işaretler,
    /// etiketlere dokunmaz ve boş slot bırakmaz. Arayüz asset'e doğrudan yazmaz.
    /// </summary>
    public sealed class SequenceEditService
    {
        public PixelLevelData Level { get; private set; }
        public event Action Changed;

        public void SetLevel(PixelLevelData level) => Level = level;

        public List<WagonSequenceEntry> Sequence => Level != null ? Level.WagonSequence : null;

        private void Edit(string undoName, Action action)
        {
            if (Level == null) return;
            Undo.RecordObject(Level, undoName);
            action();
            Level.UseCustomWagonSequence = true;
            EditorUtility.SetDirty(Level);
            Changed?.Invoke();
        }

        private bool Valid(int i) => Sequence != null && i >= 0 && i < Sequence.Count && Sequence[i] != null;

        /// <summary>İki gemi yer değiştirir. Halat, gizlilik ve kapasite gemiyle birlikte gider.</summary>
        public void Swap(int a, int b)
        {
            if (a == b || !Valid(a) || !Valid(b)) return;
            Edit("Gemi Takası", () => (Sequence[a], Sequence[b]) = (Sequence[b], Sequence[a]));
        }

        /// <summary>Gemiyi çıkarır; sonrakiler bir sıra kayar, halat ortağı tek başına kalmaz.</summary>
        public void Remove(int i)
        {
            if (!Valid(i)) return;
            Edit("Gemi Sil", () =>
            {
                Level.UnlinkWagon(i);
                Sequence.RemoveAt(i);
            });
        }

        /// <summary>Sırayı kaydıran bir değişikliğin uygulanmamış hâli: önizleme için, asset'e dokunmaz.</summary>
        public sealed class PendingChange
        {
            public string title;
            public List<WagonSequenceEntry> order;
            public int droppedLink;     // bu değişiklikle kalkacak halat (0: yok)
            public int focusIndex = -1; // uygulandıktan sonra seçilecek gemi
            public Action apply;
            public bool regenerated;    // sıra yeniden üretildi: gemiler mevcut girdilerle aynı değil
            public Func<PendingChange> reroll; // rastgele işlemlerde "yeniden üret"
        }

        /// <summary>Gemiyi başka sıraya koyar; aradakiler kayar.</summary>
        public PendingChange ProposeInsert(int from, int to)
        {
            if (!Valid(from) || !Valid(to) || from == to) return null;
            var order = new List<WagonSequenceEntry>(Sequence);
            var moved = order[from];
            order.RemoveAt(from);
            order.Insert(to, moved);
            return new PendingChange
            {
                title = $"#{from + 1} gemiyi #{to + 1} sırasına ekle", order = order, focusIndex = to,
                apply = () => ApplyOrder("Gemiyi Araya Ekle", order)
            };
        }

        public PendingChange ProposeMoveToEnd(int from)
        {
            if (!Valid(from) || from == Sequence.Count - 1) return null;
            var order = new List<WagonSequenceEntry>(Sequence);
            var moved = order[from];
            order.RemoveAt(from);
            order.Add(moved);
            return new PendingChange
            {
                title = $"#{from + 1} gemiyi sıranın sonuna taşı", order = order, focusIndex = order.Count - 1,
                apply = () => ApplyOrder("Gemiyi Sona Taşı", order)
            };
        }

        public PendingChange ProposeRemove(int index)
        {
            if (!Valid(index)) return null;
            var order = new List<WagonSequenceEntry>(Sequence);
            int link = order[index].linkId;
            order.RemoveAt(index);
            return new PendingChange
            {
                title = $"#{index + 1} gemiyi sil", order = order, droppedLink = link,
                apply = () => Remove(index)
            };
        }

        /// <summary>
        /// Eski üreticileri level'ın geçici kopyasında çalıştırır; sonuç önizlemedir, asset'e uygulanana kadar dokunulmaz.
        /// </summary>
        private PendingChange ProposeFromCopy(string title, string undoName, Action<PixelLevelData> generate, bool random)
        {
            if (Level == null) return null;
            var copy = UnityEngine.Object.Instantiate(Level);
            copy.hideFlags = HideFlags.HideAndDontSave;
            List<WagonSequenceEntry> order;
            try
            {
                generate(copy);
                order = new List<WagonSequenceEntry>(copy.WagonSequence);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
            return new PendingChange
            {
                title = title, order = order, regenerated = true,
                apply = () => ApplyOrder(undoName, order),
                reroll = random ? () => ProposeFromCopy(title, undoName, generate, true) : (Func<PendingChange>)null
            };
        }

        public PendingChange ProposeVaryCapacities()
        {
            bool empty = Sequence == null || Sequence.Count == 0;
            return ProposeFromCopy(empty ? "Gemileri palete göre üret" : "Gemi sayılarını karıştır", "Gemi Sayılarını Karıştır", l =>
            {
                if (empty) l.GenerateInterleavedWagonSequenceFromPalette();
                else l.VaryWagonCapacities();
            }, true);
        }

        public PendingChange ProposeLinksAndHidden(float linkRatio, float hiddenRatio) =>
            ProposeFromCopy("Halat ve gizli gemileri yeniden üret", "Halat ve Gizli Üret", l =>
            {
                l.GenerateLinksAndHidden(linkRatio, hiddenRatio, new System.Random(), out _, out _, out _);
                // Güçlü Konteyner halatlı ya da gizli olmaz; üretici bunu bilmiyor
                for (int i = 0; i < l.WagonSequence.Count; i++)
                {
                    var w = l.WagonSequence[i];
                    if (w == null || !w.isDualColor) continue;
                    l.UnlinkWagon(i);
                    w.isHidden = false;
                }
            }, true);

        public PendingChange ProposeSmartSort(SmartSortMode mode)
        {
            string[] names = { "Kolay", "Dengeli", "Zor" };
            return ProposeFromCopy($"Akıllı sırala: {names[(int)mode]}", "Akıllı Sıralama", l => l.WagonSequence = SmartSequenceBuilder.Build(l, mode), false);
        }

        public PendingChange ProposeInterleaved() =>
            ProposeFromCopy("Renkleri karışık dağıt", "Renkleri Karışık Dağıt", l => l.GenerateInterleavedWagonSequenceFromPalette(), true);

        public PendingChange ProposeClear() =>
            Sequence == null || Sequence.Count == 0 ? null
                : new PendingChange { title = "Sırayı temizle", order = new List<WagonSequenceEntry>(), regenerated = true, apply = () => ApplyOrder("Sırayı Temizle", new List<WagonSequenceEntry>()) };

        /// <summary>
        /// Resmi değiştirir ve paleti yeniden tarar. Gemi sırası palete göre yeniden üretilir ya da korunur; korunursa
        /// gemilerin palet numaraları yeni palete göre güncellenir, renkler ve kapasiteler değişmez.
        /// </summary>
        public void ReplaceImage(Texture2D texture, bool regenerateShips = true)
        {
            if (Level == null || texture == null) return;
            Edit("Level Resmini Değiştir", () =>
            {
                if (Level.OriginalSourceTexture == null) Level.OriginalSourceTexture = Level.LevelTexture != null ? Level.LevelTexture : texture;
                Level.LevelTexture = texture;
                Level.ExtractPaletteFromTexture();
                if (regenerateShips || Sequence == null || Sequence.Count == 0)
                {
                    Level.GenerateInterleavedWagonSequenceFromPalette();
                    return;
                }
                foreach (var w in Sequence)
                {
                    if (w == null) continue;
                    int index = LevelSnapshot.FindColor(Level.ColorPalette, w.wagonColor);
                    if (index >= 0) w.paletteIndex = index;
                }
            });
        }

        public void RevertImage()
        {
            if (Level == null) return;
            Edit("Orijinal Resme Dön", () => Level.RevertToOriginalTexture());
        }

        /// <summary>Gemi sırası dışındaki level ayarları: havuz boyutu, kapasite sınırları.</summary>
        public void EditSettings(string undoName, Action<PixelLevelData> change)
        {
            if (Level == null) return;
            Undo.RecordObject(Level, undoName);
            change(Level);
            EditorUtility.SetDirty(Level);
            Changed?.Invoke();
        }

        /// <summary>Kapasitesi eksik küp sayısı (bütün renkler).</summary>
        public int MissingCubes()
        {
            if (Level == null || Level.ColorPalette == null) return 0;
            int missing = 0;
            foreach (var e in Level.ColorPalette)
                if (e != null && e.pixelCount > 0)
                    missing += Mathf.Max(0, e.pixelCount - Level.GetTotalAssignedCapacityForColor(e.targetColor));
            return missing;
        }

        /// <summary>Eksik renkler için sıranın sonuna gemi ekler; mevcut gemiler kaymaz.</summary>
        public void AddMissingShips()
        {
            if (MissingCubes() == 0) return;
            int cap = Mathf.Max(1, Level.TruckCapacity);
            Edit("Eksikleri Tamamla", () =>
            {
                for (int p = 0; p < Level.ColorPalette.Count; p++)
                {
                    var e = Level.ColorPalette[p];
                    if (e == null || e.pixelCount <= 0) continue;
                    int diff = e.pixelCount - Level.GetTotalAssignedCapacityForColor(e.targetColor);
                    for (; diff > 0; diff -= cap)
                        Sequence.Add(new WagonSequenceEntry(e.targetColor, Mathf.Min(cap, diff), p));
                }
            });
        }

        private void ApplyOrder(string undoName, List<WagonSequenceEntry> order)
        {
            Edit(undoName, () =>
            {
                Sequence.Clear();
                Sequence.AddRange(order);
            });
        }

        public void AddAtEnd(int paletteIndex)
        {
            if (Level == null || !PaletteEntry(paletteIndex, out var e)) return;
            int cap = Mathf.Max(1, Level.TruckCapacity);
            Edit("Gemi Ekle", () => Sequence.Add(new WagonSequenceEntry(e.targetColor, cap, paletteIndex)));
        }

        public void SetCapacity(int i, int capacity)
        {
            if (!Valid(i) || capacity < 1 || Sequence[i].capacity == capacity) return;
            Edit("Gemi Kapasitesi", () => Sequence[i].capacity = capacity);
        }

        public void SetSecondaryCapacity(int i, int capacity)
        {
            if (!Valid(i) || capacity < 1 || Sequence[i].secondaryCapacity == capacity) return;
            Edit("Gemi 2. Kapasite", () => Sequence[i].secondaryCapacity = capacity);
        }

        public void SetColor(int i, int paletteIndex)
        {
            if (!Valid(i) || !PaletteEntry(paletteIndex, out var e)) return;
            Edit("Gemi Rengi", () =>
            {
                Sequence[i].wagonColor = e.targetColor;
                Sequence[i].paletteIndex = paletteIndex;
            });
        }

        public void SetSecondaryColor(int i, int paletteIndex)
        {
            if (!Valid(i) || !PaletteEntry(paletteIndex, out var e)) return;
            Edit("Gemi 2. Rengi", () => Sequence[i].secondaryColor = e.targetColor);
        }

        public void SetHidden(int i, bool hidden)
        {
            if (!Valid(i) || Sequence[i].isHidden == hidden) return;
            Edit("Gizli Gemi", () => Sequence[i].isHidden = hidden);
        }

        /// <summary>Güçlü Konteyner yapar; 2. renk bölümde küpü en çok olan farklı renktir. İki renk yoksa false.</summary>
        public bool MakeDual(int i)
        {
            if (!Valid(i)) return false;
            var w = Sequence[i];
            int own = LevelSnapshot.FindColor(Level.ColorPalette, w.wagonColor);
            int best = -1;
            for (int p = 0; p < Level.ColorPalette.Count; p++)
            {
                var e = Level.ColorPalette[p];
                if (e == null || e.pixelCount <= 0 || p == own) continue;
                if (best < 0 || e.pixelCount > Level.ColorPalette[best].pixelCount) best = p;
            }
            if (best < 0) return false;
            Edit("Güçlü Konteyner Yap", () =>
            {
                Level.UnlinkWagon(i);
                w.isHidden = false;
                w.isDualColor = true;
                w.secondaryColor = Level.ColorPalette[best].targetColor;
                w.secondaryCapacity = w.capacity;
            });
            return true;
        }

        public void ClearDual(int i)
        {
            if (!Valid(i) || !Sequence[i].isDualColor) return;
            Edit("Güçlü Konteyneri Kaldır", () =>
            {
                Sequence[i].isDualColor = false;
                Sequence[i].secondaryCapacity = 0;
            });
        }

        public bool Link(int a, int b)
        {
            if (!Valid(a) || !Valid(b) || a == b) return false;
            bool ok = false;
            Edit("Halat Bağla", () => ok = Level.LinkWagons(a, b));
            return ok;
        }

        public void Unlink(int i)
        {
            if (!Valid(i) || Sequence[i].linkId <= 0) return;
            Edit("Halat Kaldır", () => Level.UnlinkWagon(i));
        }

        private bool PaletteEntry(int index, out PaletteColorOverride entry)
        {
            entry = null;
            var p = Level != null ? Level.ColorPalette : null;
            if (p == null || index < 0 || index >= p.Count || p[index] == null) return false;
            entry = p[index];
            return true;
        }

        public void Save()
        {
            if (Level != null) AssetDatabase.SaveAssetIfDirty(Level);
        }
    }
}
