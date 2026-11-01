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

        /// <summary>Gemi sıranın sonuna gider, aradakiler bir sıra kayar.</summary>
        public void MoveToEnd(int i)
        {
            if (!Valid(i) || i == Sequence.Count - 1) return;
            Edit("Gemiyi Sona Taşı", () =>
            {
                var w = Sequence[i];
                Sequence.RemoveAt(i);
                Sequence.Add(w);
            });
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
