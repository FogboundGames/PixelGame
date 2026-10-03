using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// İnternetteki emoji arşivinde arama yapıp görselleri gözle seçmeye yarayan tarayıcı.
    /// Seçilenler seçili piksel boyutuna (varsayılan 32x32) indirgenip Assets/{boyut}x{boyut}'e kaydedilir; oradan Toplu Seviye
    /// Üreticisi ile bölüme dönüştürülür.
    ///
    /// Kaynaklar API anahtarı istemez: arama verisi emojibase (İngilizce ad + etiketler),
    /// görseller Twemoji (düz, küçük boyutlara en temiz iner) veya Google Noto (daha detaylı).
    /// </summary>
    public class PixelImageBrowserWindow : EditorWindow
    {
        private enum ImageSource
        {
            Twemoji,
            GoogleNoto
        }

        [Serializable]
        private class EmojiEntry
        {
            public string label;
            public string hexcode;
            public string[] tags;
            public string emoji;
        }

        [Serializable]
        private class EmojiList
        {
            public EmojiEntry[] items;
        }

        private const string DataUrl = "https://cdn.jsdelivr.net/npm/emojibase-data@17/en/data.json";
        private const string CacheDir = "Library/PixelGameImageBrowser";
        private const int PageSize = 60;
        private const int CellSize = 72;

        private static readonly HttpClient s_Http = CreateClient();
        // Aynı anda çok indirme başlatınca CDN isteklerin bir kısmı zaman aşımına uğruyordu
        private static readonly System.Threading.SemaphoreSlim s_Gate = new System.Threading.SemaphoreSlim(6);

        private EmojiEntry[] m_All;
        private readonly List<EmojiEntry> m_Results = new List<EmojiEntry>();
        private int m_Shown;
        private string m_Query = "";
        private string m_LastQuery = null;
        private ImageSource m_Source = ImageSource.Twemoji;
        private ImageSource m_LastSource;
        private bool m_ShowPixelPreview = true;
        private int m_PreviewSize;
        private readonly HashSet<string> m_Selected = new HashSet<string>();
        private Vector2 m_Scroll;
        private string m_Status = "";

        // İndirilen küçük resimler (anahtar: kaynak + hexcode). Ağ işi arka planda, Texture2D ana iş parçacığında kurulur.
        private readonly Dictionary<string, Texture2D> m_Thumbs = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, Texture2D> m_Pixel25 = new Dictionary<string, Texture2D>();
        private readonly HashSet<string> m_Pending = new HashSet<string>();
        private readonly HashSet<string> m_Failed = new HashSet<string>();
        private readonly ConcurrentQueue<(string key, byte[] data)> m_Downloaded = new ConcurrentQueue<(string, byte[])>();

        [MenuItem("Tools/PixelGame/🔎 Görsel Tarayıcı (Emoji → Piksel)", priority = 3)]
        public static void OpenWindow()
        {
            var window = GetWindow<PixelImageBrowserWindow>("Görsel Tarayıcı");
            window.minSize = new Vector2(520, 560);
            window.Show();
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.Add("User-Agent", "PixelGame-Editor/1.0");
            return client;
        }

        private void OnEnable()
        {
            EditorApplication.update += ProcessDownloads;
            LoadData();
        }

        private void OnDisable()
        {
            EditorApplication.update -= ProcessDownloads;
            foreach (var t in m_Thumbs.Values) if (t != null) DestroyImmediate(t);
            foreach (var t in m_Pixel25.Values) if (t != null) DestroyImmediate(t);
            m_Thumbs.Clear();
            m_Pixel25.Clear();
        }

        // ---------------------------------------------------------------- Veri

        private void LoadData()
        {
            string cachePath = Path.Combine(CacheDir, "emoji-data.json");
            try
            {
                string json;
                if (File.Exists(cachePath))
                {
                    json = File.ReadAllText(cachePath);
                }
                else
                {
                    m_Status = "Emoji listesi indiriliyor...";
                    json = s_Http.GetStringAsync(DataUrl).GetAwaiter().GetResult();
                    Directory.CreateDirectory(CacheDir);
                    File.WriteAllText(cachePath, json);
                }

                var list = JsonUtility.FromJson<EmojiList>("{\"items\":" + json + "}");
                var filtered = new List<EmojiEntry>();
                foreach (var e in list.items)
                {
                    if (e == null || string.IsNullOrEmpty(e.hexcode) || string.IsNullOrEmpty(e.label)) continue;
                    if (e.label.Contains("skin tone") || e.label.StartsWith("regional indicator")) continue;
                    filtered.Add(e);
                }
                m_All = filtered.ToArray();
                m_Status = $"{m_All.Length} görsel hazır. Aramak için İngilizce bir kelime yaz (ör. cat, pizza, car).";
            }
            catch (Exception ex)
            {
                m_All = new EmojiEntry[0];
                m_Status = "Emoji listesi indirilemedi: " + ex.Message;
            }
        }

        private void RunSearch()
        {
            m_Results.Clear();
            m_Shown = PageSize;
            m_Failed.Clear(); // yeni aramada indirilemeyenlere yeniden şans ver
            m_Scroll = Vector2.zero;
            if (m_All == null) return;

            string q = m_Query.Trim().ToLowerInvariant();
            foreach (var e in m_All)
            {
                if (q.Length == 0 || Matches(e, q)) m_Results.Add(e);
            }
        }

        private static bool Matches(EmojiEntry e, string q)
        {
            if (e.label.ToLowerInvariant().Contains(q)) return true;
            if (e.tags != null)
            {
                foreach (var t in e.tags) if (t != null && t.ToLowerInvariant().Contains(q)) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- Görsel URL'leri

        private static List<string> UrlsFor(ImageSource source, string hexcode)
        {
            string hex = hexcode.ToLowerInvariant();
            string noFe0f = hex.Replace("-fe0f", "");
            var urls = new List<string>();
            if (source == ImageSource.Twemoji)
            {
                urls.Add($"https://cdn.jsdelivr.net/gh/jdecked/twemoji@latest/assets/72x72/{hex}.png");
                if (noFe0f != hex) urls.Add($"https://cdn.jsdelivr.net/gh/jdecked/twemoji@latest/assets/72x72/{noFe0f}.png");
            }
            else
            {
                urls.Add($"https://fonts.gstatic.com/s/e/notoemoji/latest/{noFe0f.Replace('-', '_')}/512.png");
                if (noFe0f != hex) urls.Add($"https://fonts.gstatic.com/s/e/notoemoji/latest/{hex.Replace('-', '_')}/512.png");
            }
            return urls;
        }

        private static byte[] DownloadFirst(List<string> urls)
        {
            // Geçici ağ hatasına karşı bir kez daha dene
            for (int attempt = 0; attempt < 2; attempt++)
            {
                foreach (var url in urls)
                {
                    try
                    {
                        var resp = s_Http.GetAsync(url).GetAwaiter().GetResult();
                        if (resp.IsSuccessStatusCode) return resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    }
                    catch (Exception) { }
                }
            }
            return null;
        }

        private string Key(EmojiEntry e) => m_Source + ":" + e.hexcode;

        private void RequestThumb(EmojiEntry e)
        {
            string key = Key(e);
            if (m_Thumbs.ContainsKey(key) || m_Pending.Contains(key) || m_Failed.Contains(key)) return;
            m_Pending.Add(key);
            var urls = UrlsFor(m_Source, e.hexcode);
            System.Threading.Tasks.Task.Run(() =>
            {
                s_Gate.Wait();
                try { m_Downloaded.Enqueue((key, DownloadFirst(urls))); }
                finally { s_Gate.Release(); }
            });
        }

        private void ProcessDownloads()
        {
            bool any = false;
            while (m_Downloaded.TryDequeue(out var item))
            {
                any = true;
                m_Pending.Remove(item.key);
                if (item.data == null) { m_Failed.Add(item.key); continue; }

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                if (!tex.LoadImage(item.data)) { DestroyImmediate(tex); m_Failed.Add(item.key); continue; }
                m_Thumbs[item.key] = tex;

                MakePixelPreview(item.key, tex);
            }
            if (any) Repaint();
        }

        private void MakePixelPreview(string key, Texture2D source)
        {
            Texture2D px = BatchLevelGeneratorWindow.DownscaleTo(source, BatchLevelGeneratorWindow.PixelSize);
            if (px == null) return;
            px.filterMode = FilterMode.Point;
            px.hideFlags = HideFlags.HideAndDontSave;
            if (m_Pixel25.TryGetValue(key, out var old) && old != null) DestroyImmediate(old);
            m_Pixel25[key] = px;
        }

        /// <summary>Piksel boyutu değişince önizlemeleri eldeki görsellerden yeniden üretir.</summary>
        private void RebuildPixelPreviews()
        {
            m_PreviewSize = BatchLevelGeneratorWindow.PixelSize;
            foreach (var kv in m_Thumbs) MakePixelPreview(kv.Key, kv.Value);
        }

        // ---------------------------------------------------------------- Arayüz

        private void OnGUI()
        {
            if (m_All == null) LoadData();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUI.SetNextControlName("search");
            m_Query = EditorGUILayout.TextField(m_Query, EditorStyles.toolbarSearchField);
            m_Source = (ImageSource)EditorGUILayout.EnumPopup(m_Source, GUILayout.Width(110));
            EditorGUILayout.EndHorizontal();

            BatchLevelGeneratorWindow.DrawPixelSizeField();
            if (m_PreviewSize != BatchLevelGeneratorWindow.PixelSize) RebuildPixelPreviews();
            int size = BatchLevelGeneratorWindow.PixelSize;
            string outputFolder = BatchLevelGeneratorWindow.PixelFolder;

            EditorGUILayout.BeginHorizontal();
            m_ShowPixelPreview = EditorGUILayout.ToggleLeft($"{size}x{size} önizleme göster", m_ShowPixelPreview, GUILayout.Width(170));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(m_Status, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            if (m_Query != m_LastQuery || m_Source != m_LastSource)
            {
                m_LastQuery = m_Query;
                m_LastSource = m_Source;
                RunSearch();
            }

            EditorGUILayout.LabelField($"{m_Results.Count} sonuç  •  {m_Selected.Count} seçili  (seçmek için tıkla)", EditorStyles.boldLabel);

            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            DrawGrid();
            if (m_Shown < m_Results.Count && GUILayout.Button($"Daha fazla göster ({m_Results.Count - m_Shown} kaldı)", GUILayout.Height(24)))
            {
                m_Shown += PageSize;
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = m_Selected.Count > 0;
            if (GUILayout.Button("Seçimi temizle", GUILayout.Height(28), GUILayout.Width(110))) m_Selected.Clear();
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.5f);
            if (GUILayout.Button($"⬇ Seçilenleri {size}x{size} olarak '{outputFolder}'e kaydet ({m_Selected.Count})", GUILayout.Height(28)))
            {
                SaveSelected();
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button($"📁 Toplu Seviye Üreticisi'ni aç ({outputFolder})", GUILayout.Height(24)))
            {
                var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(outputFolder);
                if (folder != null) BatchLevelGeneratorWindow.OpenWithFolder(folder);
                else BatchLevelGeneratorWindow.OpenWindow();
            }
            EditorGUILayout.Space(4);
        }

        private void DrawGrid()
        {
            int count = Mathf.Min(m_Shown, m_Results.Count);
            float width = position.width - 24f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt(width / (CellSize + 6)));

            for (int i = 0; i < count; i += perRow)
            {
                EditorGUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + perRow, count); j++)
                {
                    DrawCell(m_Results[j]);
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawCell(EmojiEntry e)
        {
            Rect r = GUILayoutUtility.GetRect(CellSize, CellSize + 14, GUILayout.Width(CellSize), GUILayout.Height(CellSize + 14));
            Rect img = new Rect(r.x, r.y, CellSize, CellSize);
            bool selected = m_Selected.Contains(e.hexcode);

            EditorGUI.DrawRect(img, selected ? new Color(0.20f, 0.55f, 0.30f) : new Color(0.16f, 0.17f, 0.20f));

            string key = Key(e);
            RequestThumb(e);
            Texture2D tex = null;
            if (m_ShowPixelPreview) m_Pixel25.TryGetValue(key, out tex);
            if (tex == null) m_Thumbs.TryGetValue(key, out tex);

            if (tex != null) GUI.DrawTexture(new Rect(img.x + 6, img.y + 6, CellSize - 12, CellSize - 12), tex, ScaleMode.ScaleToFit);
            else EditorGUI.LabelField(img, m_Failed.Contains(key) ? "yok" : "...", new GUIStyle(EditorStyles.centeredGreyMiniLabel));

            GUI.Label(new Rect(r.x, r.y + CellSize, CellSize, 14), e.label, EditorStyles.centeredGreyMiniLabel);

            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                if (selected) m_Selected.Remove(e.hexcode); else m_Selected.Add(e.hexcode);
                Event.current.Use();
                Repaint();
            }
        }

        // ---------------------------------------------------------------- Kaydetme

        private void SaveSelected()
        {
            int size = BatchLevelGeneratorWindow.PixelSize;
            string outputFolder = BatchLevelGeneratorWindow.PixelFolder;
            if (!AssetDatabase.IsValidFolder(outputFolder)) AssetDatabase.CreateFolder("Assets", $"{size}x{size}");

            var byHex = new Dictionary<string, EmojiEntry>();
            foreach (var e in m_All) byHex[e.hexcode] = e;

            int saved = 0;
            var failed = new List<string>();
            var hexes = new List<string>(m_Selected);
            try
            {
                for (int i = 0; i < hexes.Count; i++)
                {
                    if (!byHex.TryGetValue(hexes[i], out EmojiEntry e)) continue;
                    EditorUtility.DisplayProgressBar("Görseller kaydediliyor", e.label, (float)i / hexes.Count);

                    byte[] data = DownloadFirst(UrlsFor(m_Source, e.hexcode));
                    if (data == null) { failed.Add(e.label); continue; }

                    var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!raw.LoadImage(data)) { DestroyImmediate(raw); failed.Add(e.label); continue; }
                    Texture2D px = BatchLevelGeneratorWindow.DownscaleTo(raw, size);
                    DestroyImmediate(raw);
                    if (px == null) { failed.Add(e.label); continue; }

                    string path = UniquePath(outputFolder + "/" + FileName(e.label) + ".png");
                    File.WriteAllBytes(path, px.EncodeToPNG());
                    DestroyImmediate(px);

                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.filterMode = FilterMode.Point;
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.isReadable = true;
                        importer.alphaIsTransparency = true;
                        importer.SaveAndReimport();
                    }
                    saved++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
            m_Selected.Clear();
            string msg = $"{saved} görsel {size}x{size} olarak '{outputFolder}' klasörüne kaydedildi.";
            if (failed.Count > 0) msg += "\n\nİndirilemeyenler: " + string.Join(", ", failed);
            msg += "\n\nBölüm yapmak için 'Toplu Seviye Üreticisi'ni aç' butonunu kullan.";
            EditorUtility.DisplayDialog("Görseller kaydedildi", msg, "Tamam");
        }

        private static string FileName(string label)
        {
            var sb = new StringBuilder();
            bool upper = true;
            foreach (char c in label)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(upper ? char.ToUpperInvariant(c) : c);
                    upper = false;
                }
                else
                {
                    upper = true;
                }
            }
            return sb.Length > 0 ? sb.ToString() : "Gorsel";
        }

        private static string UniquePath(string path)
        {
            if (!File.Exists(path)) return path;
            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            for (int i = 2; ; i++)
            {
                string p = $"{dir}/{name}_{i}.png";
                if (!File.Exists(p)) return p;
            }
        }
    }
}
