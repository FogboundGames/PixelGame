using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// Bölüm Stüdyosu'nun piksel tuvali: kullanıcının pencere içinde doğrudan
    /// çizebilmesi için basit bir renk indeksi ızgarası.
    ///
    /// Asset olarak hiçbir şey yazmaz; istendiğinde <see cref="ToTexture"/> ile
    /// bellekte bir Texture2D üretir. Bölüm oluşturulurken bu doku PNG olarak
    /// diske kaydedilip bölüme bağlanır.
    ///
    /// 0 = boş (şeffaf). 1..N = palet rengi.
    /// </summary>
    public class LevelCanvas
    {
        public int Width { get; private set; }
        public int Height { get; private set; }

        private byte[] m_Cells;

        /// <summary>Tuvalde kullanılabilecek renkler. 0. sıra "silgi" olduğu için burada yok.</summary>
        public static readonly Color[] Palette =
        {
            new Color(0.93f, 0.26f, 0.35f), // kırmızı
            new Color(0.98f, 0.55f, 0.18f), // turuncu
            new Color(0.99f, 0.82f, 0.25f), // sarı
            new Color(0.40f, 0.78f, 0.35f), // yeşil
            new Color(0.25f, 0.62f, 0.93f), // mavi
            new Color(0.60f, 0.40f, 0.85f), // mor
            new Color(0.96f, 0.52f, 0.72f), // pembe
            new Color(0.45f, 0.32f, 0.24f), // kahve
            new Color(0.16f, 0.17f, 0.20f), // siyah
            new Color(0.95f, 0.95f, 0.95f), // beyaz
        };

        public LevelCanvas(int size = 16) : this(size, size) { }

        public LevelCanvas(int w, int h)
        {
            Resize(w, h);
        }

        public void Resize(int w, int h)
        {
            w = Mathf.Clamp(w, 4, 64);
            h = Mathf.Clamp(h, 4, 64);

            var old = m_Cells;
            int ow = Width, oh = Height;

            Width = w; Height = h;
            m_Cells = new byte[w * h];

            // Boyut değişince mevcut çizimi sol-alta hizalayarak koru
            if (old != null)
            {
                for (int y = 0; y < Mathf.Min(oh, h); y++)
                    for (int x = 0; x < Mathf.Min(ow, w); x++)
                        m_Cells[y * w + x] = old[y * ow + x];
            }
        }

        public byte Get(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return 0;
            return m_Cells[y * Width + x];
        }

        public void Set(int x, int y, byte v)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            m_Cells[y * Width + x] = v;
        }

        public void Clear()
        {
            System.Array.Clear(m_Cells, 0, m_Cells.Length);
        }

        public bool IsEmpty
        {
            get
            {
                for (int i = 0; i < m_Cells.Length; i++) if (m_Cells[i] != 0) return false;
                return true;
            }
        }

        public int FilledCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < m_Cells.Length; i++) if (m_Cells[i] != 0) n++;
                return n;
            }
        }

        public Color[] CustomPalette;

        public Color[] ActivePalette => CustomPalette != null && CustomPalette.Length > 0 ? CustomPalette : Palette;

        public Color ColorOf(byte v)
        {
            if (v == 0) return Color.clear;
            Color[] pal = ActivePalette;
            int i = Mathf.Clamp(v - 1, 0, pal.Length - 1);
            return pal[i];
        }

        /// <summary>
        /// Tuval paletine yeni bir renk ekler ve yeni rengin fırça indeksini (1-tabanlı) döner.
        /// </summary>
        public byte AddColor(Color c)
        {
            c.a = 1f;
            Color[] current = ActivePalette;
            var list = new System.Collections.Generic.List<Color>(current);

            // Zaten çok benzer bir renk var mı? Varsa o rengin indeksini seç
            for (int i = 0; i < list.Count; i++)
            {
                if (Mathf.Abs(list[i].r - c.r) < 0.01f &&
                    Mathf.Abs(list[i].g - c.g) < 0.01f &&
                    Mathf.Abs(list[i].b - c.b) < 0.01f)
                {
                    return (byte)(i + 1);
                }
            }

            list.Add(c);
            CustomPalette = list.ToArray();
            return (byte)CustomPalette.Length;
        }

        /// <summary>
        /// Var olan bir fırça rengini günceller (tuvaldeki o renkle boyalı tüm hücreler anında yeni renge geçer).
        /// </summary>
        public void UpdateColor(byte brushIndex, Color c)
        {
            if (brushIndex == 0) return;
            c.a = 1f;
            Color[] current = ActivePalette;
            var list = new System.Collections.Generic.List<Color>(current);
            int palIdx = brushIndex - 1;
            if (palIdx >= 0 && palIdx < list.Count)
            {
                list[palIdx] = c;
                CustomPalette = list.ToArray();
            }
        }

        /// <summary>
        /// Var olan bir Texture2D görselini tuvale aktarır ve görselin renklerini tuval paleti yapar.
        /// </summary>
        public static LevelCanvas FromTexture(Texture2D tex)
        {
            if (tex == null) return new LevelCanvas(16);

            // Doku okunabilir değilse geçici okunabilir kopyasını al
            RenderTexture prevRT = RenderTexture.active;
            Texture2D readableTex = tex;
            bool createdTemp = false;

            if (!tex.isReadable)
            {
                RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                readableTex = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                readableTex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                readableTex.Apply();
                RenderTexture.active = prevRT;
                RenderTexture.ReleaseTemporary(rt);
                createdTemp = true;
            }

            int w = Mathf.Clamp(readableTex.width, 4, 64);
            int h = Mathf.Clamp(readableTex.height, 4, 64);

            LevelCanvas canvas = new LevelCanvas(w, h);
            Color[] pixels = readableTex.GetPixels();

            if (createdTemp)
            {
                Object.DestroyImmediate(readableTex);
            }

            // 1. Benzersiz renkleri topla
            System.Collections.Generic.List<Color> uniqueColors = new System.Collections.Generic.List<Color>();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                if (c.a < 0.2f) continue;

                bool matched = false;
                for (int j = 0; j < uniqueColors.Count; j++)
                {
                    if (Mathf.Abs(uniqueColors[j].r - c.r) < 0.04f &&
                        Mathf.Abs(uniqueColors[j].g - c.g) < 0.04f &&
                        Mathf.Abs(uniqueColors[j].b - c.b) < 0.04f)
                    {
                        matched = true;
                        break;
                    }
                }
                if (!matched && uniqueColors.Count < 20)
                {
                    uniqueColors.Add(new Color(c.r, c.g, c.b, 1f));
                }
            }

            if (uniqueColors.Count == 0)
            {
                uniqueColors.AddRange(Palette);
            }

            canvas.CustomPalette = uniqueColors.ToArray();

            // 2. Hücreleri doldur
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c = pixels[y * w + x];
                    if (c.a < 0.2f)
                    {
                        canvas.Set(x, y, 0);
                        continue;
                    }

                    int bestIdx = 0;
                    float bestDistSq = float.MaxValue;
                    for (int j = 0; j < uniqueColors.Count; j++)
                    {
                        Vector3 diff = new Vector3(uniqueColors[j].r - c.r, uniqueColors[j].g - c.g, uniqueColors[j].b - c.b);
                        float dSq = diff.sqrMagnitude;
                        if (dSq < bestDistSq)
                        {
                            bestDistSq = dSq;
                            bestIdx = j;
                        }
                    }

                    canvas.Set(x, y, (byte)(bestIdx + 1));
                }
            }

            return canvas;
        }

        /// <summary>
        /// Tuvali Texture2D'ye çevirir. Doku satır sırası Unity'nin beklediği gibi
        /// alttan üste; tuvalin (0,0)'ı da sol-alt kabul edildiği için ek çevirme yok.
        /// </summary>
        public Texture2D ToTexture()
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var px = new Color32[Width * Height];
            for (int i = 0; i < m_Cells.Length; i++)
            {
                Color c = ColorOf(m_Cells[i]);
                px[i] = m_Cells[i] == 0 ? new Color32(0, 0, 0, 0) : (Color32)c;
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------------
        // Hazır şablonlar — kod içinde üretilir, asset dosyası gerekmez.
        // ------------------------------------------------------------------

        public static readonly string[] TemplateNames = { "Kalp", "Yıldız", "Gülen Yüz", "Çiçek", "Yelkenli" };

        public static LevelCanvas Template(int index)
        {
            switch (index)
            {
                case 0: return FromArt(HeartArt, 'r');
                case 1: return FromArt(StarArt, 'y');
                case 2: return FromArt(SmileArt, 'y');
                case 3: return FromArt(FlowerArt, 'p');
                default: return FromArt(BoatArt, 'b');
            }
        }

        // Harf -> palet indeksi (1 tabanlı)
        private static byte Code(char c)
        {
            switch (c)
            {
                case 'r': return 1;  // kırmızı
                case 'o': return 2;  // turuncu
                case 'y': return 3;  // sarı
                case 'g': return 4;  // yeşil
                case 'b': return 5;  // mavi
                case 'm': return 6;  // mor
                case 'p': return 7;  // pembe
                case 'k': return 8;  // kahve
                case 'x': return 9;  // siyah
                case 'w': return 10; // beyaz
                default: return 0;
            }
        }

        private static LevelCanvas FromArt(string[] rows, char fallback)
        {
            int h = rows.Length;
            int w = 0;
            foreach (var r in rows) w = Mathf.Max(w, r.Length);

            var c = new LevelCanvas(w, h);
            for (int y = 0; y < h; y++)
            {
                // Dizideki ilk satır görselin ÜST satırı; tuvalde (0,0) sol-alt.
                string row = rows[h - 1 - y];
                for (int x = 0; x < row.Length; x++)
                {
                    char ch = row[x];
                    if (ch == '.' || ch == ' ') continue;
                    byte code = Code(ch);
                    c.Set(x, y, code == 0 ? Code(fallback) : code);
                }
            }
            return c;
        }

        private static readonly string[] HeartArt =
        {
            "..rrr....rrr..",
            ".rrrrr..rrrrr.",
            "rrrrrrrrrrrrrr",
            "rrrrrrrrrrrrrr",
            "rrrrrrrrrrrrrr",
            ".rrrrrrrrrrrr.",
            "..rrrrrrrrrr..",
            "...rrrrrrrr...",
            "....rrrrrr....",
            ".....rrrr.....",
            "......rr......",
        };

        private static readonly string[] StarArt =
        {
            ".......yy.......",
            "......yyyy......",
            "......yyyy......",
            ".....yyyyyy.....",
            "yyyyyyyyyyyyyyyy",
            ".yyyyyyyyyyyyyy.",
            "..yyyyyyyyyyyy..",
            "...yyyyyyyyyy...",
            "....yyyyyyyy....",
            "...yyyyyyyyyy...",
            "..yyyy....yyyy..",
            ".yyy........yyy.",
        };

        private static readonly string[] SmileArt =
        {
            "....yyyyyy....",
            "..yyyyyyyyyy..",
            ".yyyyyyyyyyyy.",
            "yyyxxyyyyxxyyy",
            "yyyxxyyyyxxyyy",
            "yyyyyyyyyyyyyy",
            "yyyyyyyyyyyyyy",
            "yyxyyyyyyyyxyy",
            "yyyxxyyyyxxyyy",
            ".yyyxxxxxxyyy.",
            "..yyyyyyyyyy..",
            "....yyyyyy....",
        };

        private static readonly string[] FlowerArt =
        {
            "....pp..pp....",
            "...pppppppp...",
            "..pppp yy pppp",
            "..ppp yyyy ppp",
            "..pppp yy pppp",
            "...pppppppp...",
            "....pp..pp....",
            "......gg......",
            "....g.gg......",
            "....gggg.g....",
            "......gg.g....",
            "......gg......",
        };

        private static readonly string[] BoatArt =
        {
            ".......w......",
            ".......ww.....",
            ".......www....",
            ".......wwww...",
            ".......wwwww..",
            ".......w......",
            "...kkkkkkkkk..",
            "..kkkkkkkkkkk.",
            "...kkkkkkkkk..",
            "bbbbbbbbbbbbbb",
            "bbbbbbbbbbbbbb",
        };
    }
}
