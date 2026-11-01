using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using UnityEngine.Networking;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PixelGame.Editor
{
    /// <summary>
    /// Proje kökündeki dashboard/ klasöründeki analiz panelini (Meta + GameAnalytics) Unity içinden yönetir:
    /// API anahtarlarını dashboard/.env dosyasına yazar, Node sunucusunu başlatır/durdurur ve paneli tarayıcıda açar.
    /// Anahtarlar yalnızca .env'de durur (git'e girmez).
    /// </summary>
    public sealed class LandFlowDashboardWindow : EditorWindow
    {
        private const string PidKey = "LandFlowDashboard.Pid";
        private const int DefaultPort = 5173;

        private static readonly string[] s_Keys = { "META_ACCESS_TOKEN", "META_AD_ACCOUNT_ID", "META_CURRENCY", "GA_API_KEY", "GA_GAME_ID" };

        private readonly Dictionary<string, string> m_Values = new Dictionary<string, string>();
        private string m_Status = "";
        private MessageType m_StatusType = MessageType.None;
        private Vector2 m_Scroll;
        private bool m_Running;
        private double m_NextCheck;

        private static string DashboardDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "dashboard"));
        private static string EnvPath => Path.Combine(DashboardDir, ".env");
        private static string ExamplePath => Path.Combine(DashboardDir, ".env.example");

        [MenuItem("PixelGame/📈 LandFlow Analiz Paneli")]
        public static void Open()
        {
            var w = GetWindow<LandFlowDashboardWindow>("LandFlow Analiz");
            w.minSize = new Vector2(460, 420);
            w.Show();
        }

        [MenuItem("PixelGame/📈 LandFlow Analiz Panelini Tarayıcıda Aç")]
        public static void QuickOpen()
        {
            string error = StartServerAndOpen();
            if (error != null) EditorUtility.DisplayDialog("LandFlow Analiz", error, "Tamam");
        }

        private void OnEnable() => LoadEnv();

        // Sunucu durumunu her karede değil, saniyede bir kontrol et
        private void Update()
        {
            if (EditorApplication.timeSinceStartup < m_NextCheck) return;
            m_NextCheck = EditorApplication.timeSinceStartup + 1.0;
            bool up = Directory.Exists(DashboardDir) && IsServerUp(Port);
            if (up != m_Running) { m_Running = up; Repaint(); }
        }

        private void OnGUI()
        {
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

            EditorGUILayout.LabelField("LandFlow Analiz Paneli", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Meta reklam verileri ve GameAnalytics oyuncu verileri (retention, DAU, oynama süresi, level tıkanmaları) tek sayfada.\n" +
                "Anahtar girilmezse panel ÖRNEK VERİ ile açılır.", MessageType.None);

            if (!Directory.Exists(DashboardDir))
            {
                EditorGUILayout.HelpBox($"dashboard klasörü bulunamadı:\n{DashboardDir}\nGit'ten son değişiklikleri çekin.", MessageType.Error);
                EditorGUILayout.EndScrollView();
                return;
            }

            bool running = m_Running;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Durum", running ? $"Çalışıyor → http://localhost:{Port}" : "Kapalı");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(running ? "Paneli Aç" : "Başlat ve Aç", GUILayout.Height(30)))
                {
                    string error = StartServerAndOpen();
                    SetStatus(error ?? "Panel tarayıcıda açıldı.", error == null ? MessageType.Info : MessageType.Error);
                    m_NextCheck = 0;
                }
                using (new EditorGUI.DisabledScope(!running))
                {
                    if (GUILayout.Button("Durdur", GUILayout.Height(30), GUILayout.Width(90)))
                    {
                        StopServer();
                        SetStatus("Sunucu durduruldu.", MessageType.Info);
                        m_NextCheck = 0;
                    }
                }
            }

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Meta (reklam verileri)", EditorStyles.boldLabel);
            PasswordRow("Erişim token'ı (ads_read)", "META_ACCESS_TOKEN");
            TextRow("Reklam hesabı (act_…)", "META_AD_ACCOUNT_ID");
            TextRow("Para birimi", "META_CURRENCY", "USD");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("GameAnalytics (oyuncu verileri)", EditorStyles.boldLabel);
            PasswordRow("Metrics API anahtarı", "GA_API_KEY");
            TextRow("Oyun ID (sayı)", "GA_GAME_ID");
            EditorGUILayout.HelpBox(
                "Oyundaki GA game/secret key veri okumak için çalışmaz. Metrics API anahtarı: GA → Organization settings → Metrics API (Pro plan).\n" +
                "Oyun ID: GA panelinde oyunu açınca adres çubuğundaki /game/<sayı>/.", MessageType.None);

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Anahtarları Kaydet"))
                {
                    SaveEnv();
                    bool wasRunning = IsServerUp(Port);
                    if (wasRunning && OwnedProcess() != null)
                    {
                        // Sunucu .env'i başlarken okur; yeni anahtarlar için yeniden başlat
                        StopServer();
                        string error = StartServer();
                        SetStatus(error ?? "Kaydedildi, sunucu yeni anahtarlarla yeniden başlatıldı.", error == null ? MessageType.Info : MessageType.Error);
                    }
                    else
                    {
                        SetStatus(wasRunning ? "Kaydedildi. Sunucu dışarıdan başlatılmış; yeni anahtarlar için onu yeniden başlatın." : "Kaydedildi.", MessageType.Info);
                    }
                }
                if (GUILayout.Button("Klasörü Göster", GUILayout.Width(120))) EditorUtility.RevealInFinder(Path.Combine(DashboardDir, "README.md"));
                if (GUILayout.Button("Nasıl alınır?", GUILayout.Width(110))) Application.OpenURL("file:///" + Path.Combine(DashboardDir, "README.md").Replace('\\', '/'));
            }

            if (!string.IsNullOrEmpty(m_Status))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(m_Status, m_StatusType);
            }

            EditorGUILayout.EndScrollView();
        }

        private void TextRow(string label, string key, string placeholder = "")
        {
            m_Values.TryGetValue(key, out string v);
            v = EditorGUILayout.TextField(label, string.IsNullOrEmpty(v) ? placeholder : v);
            m_Values[key] = v;
        }

        private void PasswordRow(string label, string key)
        {
            m_Values.TryGetValue(key, out string v);
            m_Values[key] = EditorGUILayout.PasswordField(label, v ?? "");
        }

        private void SetStatus(string text, MessageType type)
        {
            m_Status = text;
            m_StatusType = type;
            Repaint();
        }

        // ---------- .env ----------

        private static Dictionary<string, string> ReadEnv()
        {
            var dict = new Dictionary<string, string>();
            if (!File.Exists(EnvPath)) return dict;
            foreach (string raw in File.ReadAllLines(EnvPath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq > 0) dict[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim().Trim('"');
            }
            return dict;
        }

        private void LoadEnv()
        {
            m_Values.Clear();
            var env = ReadEnv();
            foreach (string k in s_Keys) m_Values[k] = env.TryGetValue(k, out string v) ? v : "";
        }

        private void SaveEnv()
        {
            // Mevcut dosyayı (yoksa örneği) satır satır koru, yalnızca bu penceredeki anahtarları güncelle
            var lines = new List<string>(File.Exists(EnvPath) ? File.ReadAllLines(EnvPath)
                : File.Exists(ExamplePath) ? File.ReadAllLines(ExamplePath) : Array.Empty<string>());
            var written = new HashSet<string>();
            for (int i = 0; i < lines.Count; i++)
            {
                string t = lines[i].Trim();
                int eq = t.IndexOf('=');
                if (t.StartsWith("#") || eq <= 0) continue;
                string key = t.Substring(0, eq).Trim();
                if (m_Values.TryGetValue(key, out string v))
                {
                    lines[i] = $"{key}={(v ?? "").Trim()}";
                    written.Add(key);
                }
            }
            foreach (var kv in m_Values)
            {
                if (!written.Contains(kv.Key)) lines.Add($"{kv.Key}={(kv.Value ?? "").Trim()}");
            }
            File.WriteAllLines(EnvPath, lines);
        }

        private static int Port
        {
            get
            {
                var env = ReadEnv();
                return env.TryGetValue("PORT", out string p) && int.TryParse(p, out int port) ? port : DefaultPort;
            }
        }

        // ---------- sunucu ----------

        private static string StartServerAndOpen()
        {
            if (!Directory.Exists(DashboardDir)) return $"dashboard klasörü bulunamadı: {DashboardDir}";
            int port = Port;
            if (!IsServerUp(port))
            {
                string error = StartServer();
                if (error != null) return error;
            }
            Application.OpenURL($"http://localhost:{port}");
            return null;
        }

        private static string StartServer()
        {
            int port = Port;
            string node = ResolveNode();
            if (node == null)
            {
                if (!EditorUtility.DisplayDialog("LandFlow Analiz",
                        "Panel için Node.js gerekiyor ve bu bilgisayarda bulunamadı.\n\n" +
                        "Otomatik indirip kurayım mı? (~30 MB, nodejs.org)\n" +
                        "Yalnızca bu projenin Library klasörüne kurulur; yönetici izni gerekmez, sisteme dokunmaz.",
                        "İndir ve Kur", "Vazgeç"))
                {
                    return "Node.js kurulmadı.";
                }
                string installError = InstallNode();
                if (installError != null) return installError;
                node = ResolveNode();
                if (node == null) return "Node.js kuruldu ama bulunamadı; Console'a bakın.";
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = node,
                    Arguments = "server.mjs",
                    WorkingDirectory = DashboardDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                var p = Process.Start(psi);
                if (p == null) return "Node başlatılamadı.";
                p.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.Log($"[LandFlow Analiz] {e.Data}"); };
                p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.LogWarning($"[LandFlow Analiz] {e.Data}"); };
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                SessionState.SetInt(PidKey, p.Id);

                // Sunucunun portu açmasını bekle (en fazla ~5 sn)
                for (int i = 0; i < 25 && !IsServerUp(port); i++)
                {
                    if (p.HasExited) return $"Sunucu kapandı (çıkış kodu {p.ExitCode}). Ayrıntı için Console'a bakın.";
                    System.Threading.Thread.Sleep(200);
                }
                return IsServerUp(port) ? null : "Sunucu başlamadı; Console'daki [LandFlow Analiz] satırlarına bakın.";
            }
            catch (Win32Exception e)
            {
                return $"Node.js çalıştırılamadı ({node}): {e.Message}";
            }
            catch (Exception e)
            {
                return $"Sunucu başlatılamadı: {e.Message}";
            }
        }

        // ---------- Node.js bulma / otomatik kurma ----------

        private const string NodeVersion = "v22.11.0";

        // nodejs.org/dist/v22.11.0/SHASUMS256.txt
        private static readonly Dictionary<string, string> s_NodeSha256 = new Dictionary<string, string>
        {
            { "win-x64.zip", "905373a059aecaf7f48c1ce10ffbd5334457ca00f678747f19db5ea7d256c236" },
            { "darwin-arm64.tar.gz", "2e89afe6f4e3aa6c7e21c560d8a0453d84807e97850bbb819b998531a22bdfde" },
            { "darwin-x64.tar.gz", "668d30b9512137b5f5baeef6c1bb4c46efff9a761ba990a034fb6b28b9da2465" },
        };

        private static string LocalNodeRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "LandFlowNode"));

        private static string PlatformSuffix()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor: return "win-x64.zip";
                case RuntimePlatform.OSXEditor:
                    return System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64
                        ? "darwin-arm64.tar.gz" : "darwin-x64.tar.gz";
                default: return null;
            }
        }

        /// <summary>Önce projeye kurulan, sonra sistemdeki Node.js'i bulur. Yoksa null.</summary>
        private static string ResolveNode()
        {
            bool win = Application.platform == RuntimePlatform.WindowsEditor;
            string suffix = PlatformSuffix();
            var candidates = new List<string>();
            if (suffix != null)
            {
                string dir = Path.Combine(LocalNodeRoot, $"node-{NodeVersion}-{suffix.Replace(".zip", "").Replace(".tar.gz", "")}");
                candidates.Add(win ? Path.Combine(dir, "node.exe") : Path.Combine(dir, "bin", "node"));
            }
            if (win)
            {
                candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe"));
                candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "nodejs", "node.exe"));
            }
            else
            {
                candidates.Add("/opt/homebrew/bin/node");
                candidates.Add("/usr/local/bin/node");
                candidates.Add("/usr/bin/node");
            }
            foreach (string c in candidates)
            {
                if (File.Exists(c)) return c;
            }
            return CanRun("node") ? "node" : null;
        }

        private static bool CanRun(string exe)
        {
            try
            {
                using (var p = Process.Start(new ProcessStartInfo(exe, "--version") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true }))
                {
                    if (p == null) return false;
                    p.WaitForExit(3000);
                    return p.HasExited && p.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Node.js'i nodejs.org'dan Library/LandFlowNode altına indirir, SHA-256 ile doğrular ve açar.</summary>
        private static string InstallNode()
        {
            string suffix = PlatformSuffix();
            if (suffix == null) return "Bu işletim sistemi için otomatik kurulum yok; https://nodejs.org adresinden kurun.";

            string file = $"node-{NodeVersion}-{suffix}";
            string url = $"https://nodejs.org/dist/{NodeVersion}/{file}";
            Directory.CreateDirectory(LocalNodeRoot);
            string archive = Path.Combine(LocalNodeRoot, file);

            try
            {
                using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET))
                {
                    req.downloadHandler = new DownloadHandlerFile(archive) { removeFileOnAbort = true };
                    var op = req.SendWebRequest();
                    while (!op.isDone)
                    {
                        if (EditorUtility.DisplayCancelableProgressBar("Node.js indiriliyor", $"{url}  ({req.downloadProgress:P0})", req.downloadProgress))
                        {
                            req.Abort();
                            return "İndirme iptal edildi.";
                        }
                        System.Threading.Thread.Sleep(50);
                    }
                    if (req.result != UnityWebRequest.Result.Success) return $"Node.js indirilemedi: {req.error}";
                }

                EditorUtility.DisplayProgressBar("Node.js kuruluyor", "Dosya doğrulanıyor…", 0.9f);
                string hash;
                using (var sha = SHA256.Create())
                using (var fs = File.OpenRead(archive))
                {
                    hash = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
                }
                if (hash != s_NodeSha256[suffix])
                {
                    File.Delete(archive);
                    return "İndirilen Node.js dosyası doğrulanamadı (SHA-256 uyuşmuyor); güvenlik için silindi.";
                }

                EditorUtility.DisplayProgressBar("Node.js kuruluyor", "Arşiv açılıyor…", 0.95f);
                // Windows 10+ ve macOS'ta tar hem zip hem tar.gz açar
                string tar = Application.platform == RuntimePlatform.WindowsEditor
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "tar.exe")
                    : "/usr/bin/tar";
                using (var p = Process.Start(new ProcessStartInfo(tar, $"-xf \"{archive}\" -C \"{LocalNodeRoot}\"")
                       { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true }))
                {
                    if (p == null) return "Arşiv açılamadı (tar çalışmadı).";
                    string err = p.StandardError.ReadToEnd();
                    p.WaitForExit(120000);
                    if (!p.HasExited || p.ExitCode != 0) return $"Arşiv açılamadı: {err}";
                }
                File.Delete(archive);
                Debug.Log($"[LandFlow Analiz] Node.js {NodeVersion} kuruldu: {LocalNodeRoot}");
                return null;
            }
            catch (Exception e)
            {
                return $"Node.js kurulamadı: {e.Message}";
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static Process OwnedProcess()
        {
            int pid = SessionState.GetInt(PidKey, 0);
            if (pid == 0) return null;
            try
            {
                var p = Process.GetProcessById(pid);
                // PID başka bir sürece yeniden verilmiş olabilir; yalnızca node ise bizimkidir
                return p.HasExited || p.ProcessName.IndexOf("node", StringComparison.OrdinalIgnoreCase) < 0 ? null : p;
            }
            catch
            {
                return null;
            }
        }

        private static void StopServer()
        {
            var p = OwnedProcess();
            if (p != null)
            {
                try { p.Kill(); p.WaitForExit(2000); } catch { /* zaten kapanmış */ }
            }
            SessionState.EraseInt(PidKey);
        }

        private static bool IsServerUp(int port)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect("127.0.0.1", port, null, null);
                    bool ok = result.AsyncWaitHandle.WaitOne(150);
                    if (ok) client.EndConnect(result);
                    return ok && client.Connected;
                }
            }
            catch
            {
                return false;
            }
        }

        [InitializeOnLoadMethod]
        private static void HookQuit()
        {
            // Unity kapanınca kendi başlattığımız sunucuyu da kapat
            EditorApplication.quitting -= StopServer;
            EditorApplication.quitting += StopServer;
        }
    }
}
