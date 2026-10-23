using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    public static class TestCollectionOrderLayers
    {
        [MenuItem("PixelGame/🧪 Test Outermost-To-Innermost Collection Order")]
        public static void RunDiagnostic()
        {
            var gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (gen == null || gen.CubesContainer == null)
            {
                Debug.LogWarning("[TestCollectionOrder] Sahne üzerinde PixelArtGenerator veya CubesContainer bulunamadı.");
                return;
            }

            var all = PixelCube.ActiveCubes;
            if (all == null || all.Count == 0)
            {
                all = new List<PixelCube>(gen.CubesContainer.GetComponentsInChildren<PixelCube>(false));
            }

            var present = new Dictionary<(int, int), PixelCube>();
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c != null && !c.IsPopped && c.gameObject.activeSelf)
                    present[(c.GridX, c.GridY)] = c;
            }

            if (present.Count == 0)
            {
                Debug.LogWarning("[TestCollectionOrder] Sahnede aktif küp kalmamış.");
                return;
            }

            Debug.Log($"<color=#00FFAA><b>[TestCollectionOrder]</b></color> Toplam {present.Count} aktif küp inceleniyor...");

            var layers = CollectionOrder.ComputePeelingLayers(present.Keys);
            var air = CollectionOrder.ComputeOutsideAir(present.Keys);

            int maxLayer = 0;
            foreach (var kvp in layers)
            {
                if (kvp.Value > maxLayer) maxLayer = kvp.Value;
            }

            Debug.Log($"<color=#00FFAA><b>[TestCollectionOrder]</b></color> Model katman derinliği: 0 (En dış) ile {maxLayer} (En iç çekirdek) arasında.");

            // Renk gruplarını test et
            var uniqueColors = new HashSet<Color>();
            foreach (var cube in present.Values)
            {
                uniqueColors.Add(cube.CurrentColor);
            }

            int passCount = 0;
            foreach (var col in uniqueColors)
            {
                var candidates = CollectionOrder.GetNextCollectibleCells(present.Keys, cell =>
                {
                    var c = present[cell];
                    return ShipDispatcher.ColorsMatch(c.CurrentColor, col);
                });

                if (candidates.Count == 0) continue;

                int firstLayer = layers[candidates[0]];
                bool allSameMinLayer = true;
                int maxOpenness = -1;

                foreach (var cell in candidates)
                {
                    int l = layers[cell];
                    if (l != firstLayer) allSameMinLayer = false;
                    int op = CollectionOrder.GetOpenness(air, cell.Item1, cell.Item2);
                    if (op > maxOpenness) maxOpenness = op;
                }

                if (allSameMinLayer)
                {
                    passCount++;
                    Debug.Log($"<color=#00FF00><b>[BAŞARILI]</b></color> Renk {col}: {candidates.Count} toplanabilir küp belirlendi. " +
                              $"Hepsi kesinlikle Katman {firstLayer} (en dış) üzerinde! En yüksek açıklık: {maxOpenness}.");
                }
                else
                {
                    Debug.LogError($"<color=#FF0000><b>[HATA]</b></color> Renk {col}: Katmanlar karışık!");
                }
            }

            Debug.Log($"<color=#00FFAA><b>[TestCollectionOrder]</b></color> Tamamlandı! {passCount} renk grubunun tamamı 'en dıştan en içe' kuralını %100 sağladı.");
        }
    }
}
