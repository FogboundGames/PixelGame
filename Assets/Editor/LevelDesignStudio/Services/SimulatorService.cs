using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor.Studio
{
    [Serializable]
    public sealed class SimResult
    {
        public string guid;
        public string hash;         // ölçülen sıranın parmak izi: değişirse sonuç eskimiş demektir
        public float random, smart; // kazanma oranları
        public int solvable;        // 1 çözülebilir, 0 çözümsüz, -1 arama sınırına takıldı
        public float tapRandom, tapSmart; // hızlı oynama (denizciler çalışırken basma) testi
        public long ticks;
    }

    /// <summary>
    /// docs/level-tasarim/tools altındaki C++ simülatörünü arka planda çalıştırır; editörü kilitlemez. Kuralları kendisi
    /// hesaplamaz, tek doğruluk kaynağı simülatördür. Sonuçlar kullanıcıya özel Library klasöründe saklanır, asset'e yazılmaz.
    /// Gizli küp ve gizli gemi etkisini simülatör ölçmez.
    /// </summary>
    public sealed class SimulatorService
    {
        // REHBER.md §7 ile aynı parametreler
        private const int Runs = 300, SeedMain = 9, SeedTap = 10, TimeoutMs = 180000;
        private const string ToolsDir = "docs/level-tasarim/tools";
        private const string CachePath = "Library/LevelStudio/sim_cache.json";
        private const string TempDir = "Temp/LevelStudio";

        [Serializable] private class CacheFile { public List<SimResult> items = new List<SimResult>(); }

        private sealed class Job { public string guid, hash, text; }

        private readonly Dictionary<string, SimResult> m_Cache = new Dictionary<string, SimResult>();
        private readonly Queue<Job> m_Waiting = new Queue<Job>();
        private readonly ConcurrentQueue<(Job job, SimResult result, string error)> m_Done = new ConcurrentQueue<(Job, SimResult, string)>();
        private readonly HashSet<string> m_Running = new HashSet<string>();
        private CancellationTokenSource m_Cancel;
        private bool m_Hooked, m_CacheChanged;

        public int TotalQueued { get; private set; }
        public int Finished { get; private set; }
        public string LastError { get; private set; }
        public bool IsBusy => m_Waiting.Count > 0 || m_Running.Count > 0;
        public event Action ResultsChanged;

        public static string BinaryPath => Path.GetFullPath(Path.Combine(ToolsDir,
            Application.platform == RuntimePlatform.WindowsEditor ? "sim.exe" : "sim"));
        public static bool IsAvailable => File.Exists(BinaryPath);

        public SimulatorService() => LoadCache();

        public static string GuidOf(PixelLevelData level) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(level));

        /// <summary>Simülatörün okuduğu biçim; level_asset.py export ile birebir aynıdır. Boş girdiler oyunda olduğu gibi atlanır.</summary>
        public static string Export(LevelSnapshot s)
        {
            var sb = new StringBuilder(s.width * s.height * 3 + 512);
            sb.Append(s.width).Append(' ').Append(s.height).Append('\n');
            for (int y = s.height - 1; y >= 0; y--)
            {
                for (int x = 0; x < s.width; x++)
                {
                    if (x > 0) sb.Append(' ');
                    sb.Append(s.cells[y * s.width + x]);
                }
                sb.Append('\n');
            }
            var ships = new List<LevelSnapshot.Ship>();
            foreach (var w in s.ships) if (!w.empty) ships.Add(w);
            sb.Append(s.columns).Append(' ').Append(Mathf.Min(s.rows, 3)).Append(" 5\n").Append(ships.Count).Append('\n');
            foreach (var w in ships)
            {
                sb.Append(w.color).Append(' ').Append(w.capacity).Append(' ').Append(w.link).Append(' ').Append(w.hidden ? 1 : 0);
                if (w.dual) sb.Append(' ').Append(w.color2).Append(' ').Append(w.capacity2);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        private static string Hash(string text)
        {
            using (var md5 = MD5.Create())
                return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        /// <summary>Son sonuç; <paramref name="stale"/>: o ölçümden sonra gemi sırası ya da resim değişmiş.</summary>
        public SimResult Get(PixelLevelData level, LevelSnapshot snapshot, out bool stale)
        {
            stale = false;
            if (!m_Cache.TryGetValue(GuidOf(level), out var r)) return null;
            stale = r.hash != Hash(Export(snapshot));
            return r;
        }

        public bool IsRunning(PixelLevelData level)
        {
            string guid = GuidOf(level);
            if (m_Running.Contains(guid)) return true;
            foreach (var j in m_Waiting) if (j.guid == guid) return true;
            return false;
        }

        public void Enqueue(IEnumerable<PixelLevelData> levels, bool skipFresh)
        {
            if (!IsAvailable) return;
            if (!IsBusy) { TotalQueued = 0; Finished = 0; LastError = null; }
            foreach (var level in levels)
            {
                if (level == null || IsRunning(level)) continue;
                string guid = GuidOf(level);
                string text = Export(LevelSnapshot.From(level));
                string hash = Hash(text);
                if (skipFresh && m_Cache.TryGetValue(guid, out var r) && r.hash == hash) continue;
                m_Waiting.Enqueue(new Job { guid = guid, hash = hash, text = text });
                TotalQueued++;
            }
            if (m_Cancel == null) m_Cancel = new CancellationTokenSource();
            Hook(true);
            Pump();
            ResultsChanged?.Invoke();
        }

        public void CancelAll()
        {
            m_Waiting.Clear();
            m_Cancel?.Cancel();
            ResultsChanged?.Invoke();
        }

        /// <summary>Pencere kapanırken ya da kod yeniden yüklenirken çalışan işleri durdurur.</summary>
        public void Shutdown()
        {
            m_Waiting.Clear();
            m_Cancel?.Cancel();
            Hook(false);
            SaveCache();
        }

        private void Hook(bool on)
        {
            if (on == m_Hooked) return;
            m_Hooked = on;
            if (on) EditorApplication.update += Update;
            else EditorApplication.update -= Update;
        }

        private void Pump()
        {
            int limit = Mathf.Max(1, Environment.ProcessorCount - 2);
            string binary = BinaryPath;
            var token = m_Cancel.Token;
            while (m_Running.Count < limit && m_Waiting.Count > 0)
            {
                var job = m_Waiting.Dequeue();
                m_Running.Add(job.guid);
                Directory.CreateDirectory(TempDir);
                string path = Path.GetFullPath(Path.Combine(TempDir, job.guid + ".txt"));
                Task.Run(() => RunJob(job, binary, path, token));
            }
        }

        private void RunJob(Job job, string binary, string path, CancellationToken token)
        {
            try
            {
                File.WriteAllText(path, job.text);
                string[] main = RunProcess(binary, $"\"{path}\" {Runs} {SeedMain} 1", token);
                string[] tap = RunProcess(binary, $"\"{path}\" {Runs} {SeedTap} 0 m", token);
                var c = CultureInfo.InvariantCulture;
                m_Done.Enqueue((job, new SimResult
                {
                    guid = job.guid, hash = job.hash,
                    random = float.Parse(main[0], c), smart = float.Parse(main[1], c), solvable = int.Parse(main[2], c),
                    tapRandom = float.Parse(tap[0], c), tapSmart = float.Parse(tap[1], c), ticks = DateTime.Now.Ticks
                }, null));
            }
            catch (Exception e)
            {
                m_Done.Enqueue((job, null, e.Message));
            }
            finally
            {
                try { File.Delete(path); } catch (IOException) { }
            }
        }

        private static string[] RunProcess(string binary, string args, CancellationToken token)
        {
            var info = new ProcessStartInfo(binary, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            using (var p = Process.Start(info))
            {
                var output = p.StandardOutput.ReadToEndAsync();
                var watch = Stopwatch.StartNew();
                while (!p.WaitForExit(100))
                {
                    if (token.IsCancellationRequested || watch.ElapsedMilliseconds > TimeoutMs)
                    {
                        try { p.Kill(); } catch (InvalidOperationException) { }
                        throw new OperationCanceledException(token.IsCancellationRequested ? "durduruldu" : "zaman aşımı");
                    }
                }
                if (p.ExitCode != 0) throw new InvalidOperationException($"simülatör çıkış kodu {p.ExitCode}");
                var parts = output.Result.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) throw new InvalidOperationException("simülatör çıktısı okunamadı");
                return parts;
            }
        }

        private void Update()
        {
            bool changed = false;
            while (m_Done.TryDequeue(out var item))
            {
                m_Running.Remove(item.job.guid);
                Finished++;
                changed = true;
                if (item.result != null)
                {
                    m_Cache[item.job.guid] = item.result;
                    m_CacheChanged = true;
                }
                else if (!(m_Cancel?.IsCancellationRequested ?? false)) LastError = item.error;
            }
            if (m_Cancel != null && m_Cancel.IsCancellationRequested && m_Running.Count == 0)
            {
                m_Cancel.Dispose();
                m_Cancel = null;
            }
            if (m_Cancel != null && m_Waiting.Count > 0) Pump();
            if (!IsBusy)
            {
                Hook(false);
                SaveCache();
                m_Cancel?.Dispose();
                m_Cancel = null;
            }
            if (changed || !IsBusy) ResultsChanged?.Invoke();
        }

        private void LoadCache()
        {
            try
            {
                if (!File.Exists(CachePath)) return;
                var file = JsonUtility.FromJson<CacheFile>(File.ReadAllText(CachePath));
                foreach (var r in file.items) if (!string.IsNullOrEmpty(r.guid)) m_Cache[r.guid] = r;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException)
            {
                UnityEngine.Debug.LogWarning("[Level Stüdyosu] Simülasyon önbelleği okunamadı: " + e.Message);
            }
        }

        private void SaveCache()
        {
            if (!m_CacheChanged) return;
            m_CacheChanged = false;
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath));
            File.WriteAllText(CachePath, JsonUtility.ToJson(new CacheFile { items = new List<SimResult>(m_Cache.Values) }));
        }
    }
}
