using System.Collections.Generic;
using UnityEngine;

namespace PixelGame.Editor.Studio
{
    public enum RuleSeverity { Critical = 0, Warning = 1, Info = 2 }

    /// <summary>Tek bir kural bulgusu: ne yanlış, neden önemli, nasıl düzelir ve hangi gemileri ilgilendiriyor.</summary>
    public sealed class RuleFinding
    {
        public RuleSeverity severity;
        public string code;
        public string what, why, fix;
        public int[] slots = new int[0];
    }

    /// <summary>
    /// Level kuralları. Kritik: level bitmez ya da oyun takılabilir. Uyarı: oynanır ama tasarım kuralına aykırı.
    /// Bilgi: öneri. Hiçbir bulgu kaydı engellemez ve hiçbir şey veriyi değiştirmez.
    /// Oyun kodu tek doğruluk kaynağıdır; tasarım kuralları docs/level-tasarim/REHBER.md ile aynıdır.
    /// Aynı senaryolar docs/level-tasarim/tools/rule_tests altında tutulur.
    /// </summary>
    public static class LevelRules
    {
        private const int OuterMinExposed = 6;

        public static List<RuleFinding> Check(LevelSnapshot s)
        {
            var list = new List<RuleFinding>();
            CheckSequence(s, list);
            CheckCapacity(s, list);
            CheckRopes(s, list);
            CheckOrder(s, list);
            CheckHidden(s, list);
            CheckOpening(s, list);
            CheckMisc(s, list);
            list.Sort((a, b) => a.severity.CompareTo(b.severity));
            return list;
        }

        /// <summary>Kaydetmeden bağımsız durum: kritik bulgu yoksa level yayına hazırdır.</summary>
        public static bool IsReadyToPublish(List<RuleFinding> findings)
        {
            foreach (var f in findings) if (f.severity == RuleSeverity.Critical) return false;
            return true;
        }

        private static void Add(List<RuleFinding> list, RuleSeverity sev, string code, string what, string why, string fix, params int[] slots) =>
            list.Add(new RuleFinding { severity = sev, code = code, what = what, why = why, fix = fix, slots = slots });

        private static void CheckSequence(LevelSnapshot s, List<RuleFinding> list)
        {
            if (s.ships.Count == 0)
                Add(list, RuleSeverity.Critical, "SEQUENCE_EMPTY", "Gemi sırası boş.", "Oyunda hiç gemi gelmez.", "Gemi ekle.");

            for (int i = 0; i < s.ships.Count; i++)
            {
                var w = s.ships[i];
                if (w.empty)
                {
                    Add(list, RuleSeverity.Critical, "SLOT_EMPTY", $"#{i + 1} boş ya da kapasitesi 0.",
                        "Oyun bu girdiyi atlar; sonraki gemiler ekranda görünenden başka sütuna düşer.", "Girdiyi sil ya da kapasite ver.", i);
                    continue;
                }
                if (w.color < 0)
                    Add(list, RuleSeverity.Critical, "COLOR_NOT_IN_PALETTE", $"#{i + 1} geminin rengi palette yok.",
                        "Bu gemi hiçbir küp toplayamaz.", "Geminin rengini değiştir.", i);
                if (w.dual && w.color2 < 0)
                    Add(list, RuleSeverity.Critical, "DUAL_SECOND_MISSING", $"#{i + 1} Güçlü Konteyner'in 2. rengi palette yok.",
                        "O yarı hiç dolmaz.", "2. rengi değiştir.", i);
                if (w.dual && w.color2 >= 0 && w.color2 == w.color)
                    Add(list, RuleSeverity.Warning, "DUAL_SAME_COLOR", $"#{i + 1} Güçlü Konteyner'in iki yarısı aynı renk.",
                        "Oyunda toplam kapasiteli tek renkli konteyner olur.", "2. rengi değiştir ya da normal gemiye çevir.", i);
            }
        }

        private static void CheckCapacity(LevelSnapshot s, List<RuleFinding> list)
        {
            if (s.unmappedCubes > 0)
                Add(list, RuleSeverity.Critical, "CUBES_NOT_IN_PALETTE", $"{s.unmappedCubes} küpün rengi palette yok.",
                    "Bu küpler için gemi gelmez, level bitmez.", "Paleti yeniden tara.");
            if (s.cells.Length == 0) return;

            var cubes = new int[s.colorCount];
            foreach (int c in s.cells) if (c >= 0 && c < s.colorCount) cubes[c]++;
            var caps = new int[s.colorCount];
            foreach (var w in s.ships)
            {
                if (w.empty) continue;
                if (w.color >= 0 && w.color < s.colorCount) caps[w.color] += w.capacity;
                if (w.dual && w.color2 >= 0 && w.color2 < s.colorCount) caps[w.color2] += w.capacity2;
            }
            for (int i = 0; i < s.colorCount; i++)
            {
                if (cubes[i] == caps[i]) continue;
                int d = cubes[i] - caps[i];
                Add(list, RuleSeverity.Critical, "CAPACITY_MISMATCH",
                    d > 0 ? $"{s.ColorName(i)}: {d} küp eksik taşınıyor ({caps[i]}/{cubes[i]})." : $"{s.ColorName(i)}: {-d} fazla kapasite ({caps[i]}/{cubes[i]}).",
                    d > 0 ? "Kalan küpler toplanamaz, level bitmez." : "Son gemi dolamadan kalkar.",
                    "Bu rengin gemi kapasitelerini küp sayısına eşitle.");
            }

            int limit = s.tier == 2 ? 50 : s.tier == 1 ? 40 : TotalCubes(cubes) >= 900 ? 40 : 30;
            for (int i = 0; i < s.ships.Count; i++)
            {
                var w = s.ships[i];
                if (!w.empty && Mathf.Max(w.capacity, w.capacity2) > limit)
                    Add(list, RuleSeverity.Warning, "CAPACITY_LIMIT", $"#{i + 1} kapasitesi {Mathf.Max(w.capacity, w.capacity2)}, bu zorlukta sınır {limit}.",
                        "Tek gemi fazla küp alır, level kolaylaşır.", "Gemiyi ikiye böl.", i);
            }
        }

        private static int TotalCubes(int[] cubes) { int t = 0; foreach (int c in cubes) t += c; return t; }

        private static void CheckRopes(LevelSnapshot s, List<RuleFinding> list)
        {
            var groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < s.ships.Count; i++)
            {
                var w = s.ships[i];
                if (w.empty || w.link <= 0) continue;
                if (!groups.TryGetValue(w.link, out var g)) groups[w.link] = g = new List<int>();
                g.Add(i);
                if (w.dual)
                    Add(list, RuleSeverity.Warning, "ROPE_ON_DUAL", $"#{i + 1} Güçlü Konteyner halatlı.",
                        "Bu kombinasyon tasarım kurallarında yok ve test edilmedi.", "Halatı kaldır.", i);
            }
            foreach (var kv in groups)
            {
                var m = kv.Value;
                if (m.Count != 2)
                {
                    Add(list, RuleSeverity.Critical, "ROPE_SIZE", $"Halat {kv.Key}: {m.Count} gemi bağlı, 2 olmalı.",
                        "Grup tamamlanmadan gemi gönderilemeyebilir.", "Halatı kaldırıp yeniden bağla.", m.ToArray());
                    continue;
                }
                int i = m[0], j = m[1];
                bool side = j == i + 1 && i % s.columns != s.columns - 1;
                bool stack = j == i + s.columns && j < s.columns * s.rows;
                if (!side && !stack)
                    Add(list, RuleSeverity.Warning, "ROPE_POSITION", $"Halat {kv.Key}: #{i + 1} ve #{j + 1} komşu değil.",
                        "Oyun çalışır ama geç gelen ortak beklenirken ötekiler sütunda takılı kalır.",
                        "Yan yana komşu sütuna ya da başlangıç havuzunda alt alta taşı.", i, j);
                if (s.ships[i].color >= 0 && s.ships[i].color == s.ships[j].color)
                    Add(list, RuleSeverity.Warning, "ROPE_SAME_COLOR", $"Halat {kv.Key}: iki gemi de {s.ColorName(s.ships[i].color)}.",
                        "Halat zorluk katmaz.", "Farklı renkli iki gemiyi bağla.", i, j);
            }
        }

        private static void CheckOrder(LevelSnapshot s, List<RuleFinding> list)
        {
            for (int i = 0; i + s.columns < s.ships.Count; i++)
            {
                var a = s.ships[i];
                var b = s.ships[i + s.columns];
                if (a.empty || b.empty) continue;
                bool same = a.color == b.color || (b.dual && a.color == b.color2) ||
                            (a.dual && (a.color2 == b.color || (b.dual && a.color2 == b.color2)));
                if (same)
                    Add(list, RuleSeverity.Warning, "SAME_COLOR_IN_LANE", $"#{i + 1} ve #{i + s.columns + 1}: aynı sütunda aynı renk arka arkaya.",
                        "Oyuncu aynı rengi art arda alıp seçim yapmadan ilerler.", "Birini başka sütundaki gemiyle takas et.", i, i + s.columns);
            }
        }

        private static void CheckHidden(LevelSnapshot s, List<RuleFinding> list)
        {
            for (int i = 0; i < s.ships.Count; i++)
            {
                var w = s.ships[i];
                if (w.empty || !w.hidden) continue;
                if (i < s.columns)
                    Add(list, RuleSeverity.Warning, "HIDDEN_IN_FRONT", $"#{i + 1} gizli gemi ön sırada başlıyor.",
                        "Slota yanaşana kadar rengi açılmaz, oyuncuya haksız gelir.", "Gizliliği kaldır ya da arkaya taşı.", i);
                if (w.link > 0)
                    Add(list, RuleSeverity.Warning, "HIDDEN_WITH_ROPE", $"#{i + 1} hem gizli hem halatlı.",
                        "Oyuncu ne bağladığını göremez.", "Birini kaldır.", i);
                if (w.dual)
                    Add(list, RuleSeverity.Warning, "HIDDEN_DUAL", $"#{i + 1} Güçlü Konteyner gizli.",
                        "Bu kombinasyon tasarım kurallarında yok ve test edilmedi.", "Gizliliği kaldır.", i);
            }
        }

        private static void CheckOpening(LevelSnapshot s, List<RuleFinding> list)
        {
            int cols = s.columns;
            var first = new Dictionary<int, int>();
            int frontRopes = 0;
            for (int i = 0; i < s.ships.Count; i++)
            {
                var w = s.ships[i];
                if (w.empty || w.link <= 0) continue;
                if (!first.ContainsKey(w.link)) first[w.link] = i;
                if (i < cols) frontRopes++;
            }
            int poolRopes = 0;
            foreach (var kv in first) if (kv.Value < cols * 3) poolRopes++;
            if (poolRopes > 2)
                Add(list, RuleSeverity.Warning, "OPENING_ROPES", $"Açılışta (ilk 3 sıra) {poolRopes} halat var, en fazla 2 önerilir.",
                    "Açılış okunmaz olur, slotlar hemen kilitlenir.", "Halatları arka sıralara taşı.");
            if (frontRopes > cols / 2)
                Add(list, RuleSeverity.Warning, "FRONT_ROPES", $"Ön sıranın {frontRopes}/{cols} gemisi halatlı, en fazla yarısı önerilir.",
                    "Oyuncunun ilk hamlede seçeneği kalmaz.", "Ön sıradaki halatları azalt.");

            if (s.cells.Length == 0) return;
            var outer = OuterColors(s);
            int f1 = 0, f2 = 0;
            for (int i = 0; i < s.ships.Count && i < 2 * cols; i++)
            {
                var w = s.ships[i];
                if (w.empty) continue;
                bool isOuter = outer.Contains(w.color) || (w.dual && outer.Contains(w.color2));
                if (!isOuter) continue;
                f2++;
                if (i < cols) f1++;
            }
            if (f1 > 1)
                Add(list, RuleSeverity.Warning, "FRONT_OUTER", $"Ön sıranın {f1}/{cols} gemisi dış katman rengi, en fazla 1 önerilir.",
                    "Oyuncu ekrana bakmadan öndekileri yollar, level kendini oynar.", "Bunları arkadaki gemilerle takas et.");
            if (f2 > 3)
                Add(list, RuleSeverity.Warning, "OPEN_OUTER", $"İlk iki sırada {f2} dış katman gemisi var, en fazla 3 önerilir.",
                    "Açılış fazla kolaylaşır.", "Bunları arkadaki gemilerle takas et.");
        }

        /// <summary>Başta açıkta en az 6 küpü olan renkler (dış hava: çerçeveden 4 yönlü flood fill).</summary>
        public static HashSet<int> OuterColors(LevelSnapshot s)
        {
            int w = s.width, h = s.height, aw = w + 2, ah = h + 2;
            var air = new bool[aw * ah];
            var stack = new Stack<int>();
            air[0] = true;
            stack.Push(0);
            while (stack.Count > 0)
            {
                int cur = stack.Pop(), cx = cur % aw, cy = cur / aw;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= aw || ny >= ah) continue;
                    int n = ny * aw + nx;
                    if (air[n]) continue;
                    if (nx >= 1 && ny >= 1 && nx <= w && ny <= h && s.cells[(ny - 1) * w + nx - 1] != -1) continue;
                    air[n] = true;
                    stack.Push(n);
                }
            }
            var exposed = new Dictionary<int, int>();
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int c = s.cells[y * w + x];
                if (c == -1) continue;
                int ax = x + 1, ay = y + 1;
                if (air[ay * aw + ax - 1] || air[ay * aw + ax + 1] || air[(ay - 1) * aw + ax] || air[(ay + 1) * aw + ax])
                {
                    exposed.TryGetValue(c, out int n);
                    exposed[c] = n + 1;
                }
            }
            var result = new HashSet<int>();
            foreach (var kv in exposed) if (kv.Value >= OuterMinExposed) result.Add(kv.Key);
            return result;
        }

        private static void CheckMisc(LevelSnapshot s, List<RuleFinding> list)
        {
            var occupied = new HashSet<int>();
            for (int i = 0; i < s.cells.Length; i++) if (s.cells[i] != -1) occupied.Add(i);
            if (s.cells.Length > 0)
                foreach (var m in s.mysteryCells)
                    if (m.x < 0 || m.y < 0 || m.x >= s.width || m.y >= s.height || !occupied.Contains(m.y * s.width + m.x))
                        Add(list, RuleSeverity.Warning, "MYSTERY_ON_EMPTY", $"Gizli küp boş hücrede: ({m.x}, {m.y}).",
                            "Bu koordinat oyunda işe yaramaz.", "Gizli küp ızgarasından kaldır.");

            if (!s.tierManual)
                Add(list, RuleSeverity.Info, "TIER_NOT_MANUAL", "Zorluk elle sabitlenmemiş.",
                    "Unity zorluğu kendi analiziyle değiştirebilir.", "Eski araçtan zorluğu elle seç.");

            var odd = new Dictionary<int, int>();
            foreach (var w in s.ships)
            {
                if (w.empty || w.capacity % 10 == 0 || w.color < 0) continue;
                odd.TryGetValue(w.color, out int n);
                odd[w.color] = n + 1;
            }
            foreach (var kv in odd)
                if (kv.Value > 1)
                    Add(list, RuleSeverity.Info, "ODD_CAPACITY", $"{s.ColorName(kv.Key)}: {kv.Value} küsuratlı gemi var, en fazla 1 önerilir.",
                        "Sayılar 10'un katı olunca daha rahat okunur.", "Kapasiteleri yeniden böl.");
        }
    }
}
