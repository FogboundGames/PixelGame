using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DG.Tweening;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// Seçilen bölümleri Play modunda otomatik oyunculara (dikkatsiz + akıllı) birkaç kez oynatır,
    /// her oyunun sonucunu ve oynanış verilerini toplar; özet tabloyu pencerede gösterir ve CSV'ye yazar.
    /// </summary>
    public class AutoPlaytestWindow : EditorWindow
    {
        [MenuItem("Tools/PixelGame/🤖 Otomatik Oyun Testi", priority = 30)]
        public static void Open() => GetWindow<AutoPlaytestWindow>("🤖 Oyun Testi").minSize = new Vector2(760, 420);

        private Vector2 m_LevelScroll, m_ResultScroll;
        private AutoPlaytestRunner.Config m_Config;
        private bool[] m_LevelSelected;

        private void OnEnable()
        {
            m_Config = AutoPlaytestRunner.LoadConfig();
            EditorApplication.update += RepaintWhileRunning;
        }

        private void OnDisable() => EditorApplication.update -= RepaintWhileRunning;

        private double m_LastRepaint;
        private void RepaintWhileRunning()
        {
            if (AutoPlaytestRunner.IsRunning && EditorApplication.timeSinceStartup - m_LastRepaint > 0.5)
            {
                m_LastRepaint = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void OnGUI()
        {
            var lm = FindFirstObjectByType<LevelManager>();
            var levels = lm != null ? lm.Levels : null;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("🤖 Otomatik Oyun Testi", new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 });
            EditorGUILayout.HelpBox("Seçilen bölümleri Play modunda iki tip oyuncuyla oynatır: 🎲 Dikkatsiz (rastgele ön sıra gemisi) ve 🧠 Akıllı (dışarıda en çok küpü olan renk). " +
                                    "Test sırasında Unity penceresi önde kalsın; arka planda Unity yavaşlar. Kayıtlı 'mevcut level' test sonunda geri yüklenir.", MessageType.Info);

            if (levels == null || levels.Count == 0)
            {
                EditorGUILayout.HelpBox("Sahnede LevelManager ya da level listesi bulunamadı (Gemi sahnesini aç).", MessageType.Warning);
                DrawResults();
                return;
            }

            if (m_LevelSelected == null || m_LevelSelected.Length != levels.Count)
            {
                m_LevelSelected = new bool[levels.Count];
                for (int i = 0; i < levels.Count; i++) m_LevelSelected[i] = m_Config.levels.Count == 0 || m_Config.levels.Contains(i);
            }

            using (new EditorGUI.DisabledScope(AutoPlaytestRunner.IsRunning || EditorApplication.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();

                // Sol: bölüm seçimi
                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(300));
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Bölümler ({m_LevelSelected.Count(b => b)}/{levels.Count})", EditorStyles.boldLabel);
                if (GUILayout.Button("Hepsi", EditorStyles.miniButtonLeft, GUILayout.Width(50))) for (int i = 0; i < m_LevelSelected.Length; i++) m_LevelSelected[i] = true;
                if (GUILayout.Button("Hiçbiri", EditorStyles.miniButtonRight, GUILayout.Width(50))) for (int i = 0; i < m_LevelSelected.Length; i++) m_LevelSelected[i] = false;
                EditorGUILayout.EndHorizontal();
                m_LevelScroll = EditorGUILayout.BeginScrollView(m_LevelScroll, GUILayout.Height(150));
                for (int i = 0; i < levels.Count; i++)
                    m_LevelSelected[i] = EditorGUILayout.ToggleLeft($"{i + 1}. {(levels[i] ? levels[i].name : "(boş)")}", m_LevelSelected[i]);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();

                // Sağ: ayarlar
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Ayarlar", EditorStyles.boldLabel);
                m_Config.randomPlayer = EditorGUILayout.ToggleLeft("🎲 Dikkatsiz oyuncu (rastgele)", m_Config.randomPlayer);
                m_Config.smartPlayer = EditorGUILayout.ToggleLeft("🧠 Akıllı oyuncu (açık rengi seçer)", m_Config.smartPlayer);
                m_Config.runsPerPlayer = EditorGUILayout.IntSlider(new GUIContent("Oyuncu başına oyun", "Her bölüm, her oyuncu tipiyle bu kadar oynanır."), m_Config.runsPerPlayer, 1, 10);
                m_Config.timeScale = EditorGUILayout.Slider(new GUIContent("Oyun hızı", "1 = gerçek hız. Yüksek hız testi kısaltır."), m_Config.timeScale, 1f, 5f);
                m_Config.tapInterval = EditorGUILayout.Slider(new GUIContent("Dokunuş aralığı (sn)", "Oyuncunun iki dokunuşu arasındaki gerçek süre. Küçük = hızlı oyuncu."), m_Config.tapInterval, 0.05f, 1.5f);
                m_Config.stuckSeconds = EditorGUILayout.Slider(new GUIContent("Takılma süresi (oyun sn)", "Bu kadar oyun süresi hiç ilerleme olmazsa 'takıldı' sayılır."), m_Config.stuckSeconds, 10f, 90f);
                m_Config.maxSecondsPerRun = EditorGUILayout.Slider(new GUIContent("En uzun oyun (oyun sn)", "Bir oyun bundan uzun sürerse 'zaman aşımı'."), m_Config.maxSecondsPerRun, 60f, 900f);
                int selected = m_LevelSelected.Count(b => b);
                int players = (m_Config.randomPlayer ? 1 : 0) + (m_Config.smartPlayer ? 1 : 0);
                EditorGUILayout.LabelField($"Toplam {selected * players * m_Config.runsPerPlayer} oyun", EditorStyles.miniBoldLabel);
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            if (!AutoPlaytestRunner.IsRunning)
            {
                GUI.backgroundColor = new Color(0.45f, 0.9f, 0.5f);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || !(m_Config.randomPlayer || m_Config.smartPlayer) || !m_LevelSelected.Any(b => b)))
                {
                    if (GUILayout.Button("▶ Testi Başlat", GUILayout.Height(30)))
                    {
                        m_Config.levels = Enumerable.Range(0, m_LevelSelected.Length).Where(i => m_LevelSelected[i]).ToList();
                        AutoPlaytestRunner.Start(m_Config);
                    }
                }
            }
            else
            {
                GUI.backgroundColor = new Color(1f, 0.5f, 0.45f);
                if (GUILayout.Button("■ Durdur", GUILayout.Height(30))) AutoPlaytestRunner.Stop("Kullanıcı durdurdu");
            }
            GUI.backgroundColor = Color.white;
            string csv = AutoPlaytestRunner.LastCsvPath;
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(csv) || !File.Exists(csv)))
            {
                if (GUILayout.Button("📄 CSV'yi Göster", GUILayout.Height(30), GUILayout.Width(130))) EditorUtility.RevealInFinder(csv);
            }
            EditorGUILayout.EndHorizontal();

            if (AutoPlaytestRunner.IsRunning || !string.IsNullOrEmpty(AutoPlaytestRunner.Status))
                EditorGUILayout.HelpBox(AutoPlaytestRunner.Status, AutoPlaytestRunner.IsRunning ? MessageType.None : MessageType.Info);

            DrawResults();
        }

        private void DrawResults()
        {
            var jobs = AutoPlaytestRunner.LoadResults();
            if (jobs.Count == 0) return;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Sonuçlar (bölüm × oyuncu)", EditorStyles.boldLabel);

            string[] heads = { "Bölüm", "Oyuncu", "Oyun", "Kazan", "Kayıp", "Takıldı", "Ort. süre", "Gönderim", "Slot dolu", "Boşta bekleme", "Ekstra gemi", "Kayıp gemi", "Hata" };
            float[] widths = { 190, 70, 40, 50, 50, 55, 65, 65, 65, 90, 75, 70, 45 };
            m_ResultScroll = EditorGUILayout.BeginScrollView(m_ResultScroll);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            for (int i = 0; i < heads.Length; i++) GUILayout.Label(heads[i], EditorStyles.miniBoldLabel, GUILayout.Width(widths[i]));
            EditorGUILayout.EndHorizontal();

            foreach (var grp in jobs.GroupBy(j => (j.levelIndex, j.player)).OrderBy(g => g.Key.levelIndex).ThenBy(g => g.Key.player))
            {
                var list = grp.ToList();
                int n = list.Count;
                int win = list.Count(j => j.outcome == "KAZANDI"), fail = list.Count(j => j.outcome == "KAYBETTI"), stuck = n - win - fail;
                var row = new[]
                {
                    $"{list[0].levelIndex + 1}. {list[0].levelName}",
                    list[0].player == "smart" ? "🧠 Akıllı" : "🎲 Dikkatsiz",
                    n.ToString(),
                    Pct(win, n), Pct(fail, n), Pct(stuck, n),
                    $"{list.Average(j => j.gameSeconds):0} sn",
                    $"{list.Average(j => j.sends):0.#}",
                    $"%{list.Average(j => j.gameSeconds > 0 ? 100f * j.slotsFullSeconds / j.gameSeconds : 0f):0}",
                    $"{list.Average(j => j.idleDockedSeconds):0.#} sn",
                    list.Sum(j => j.extraShips).ToString(),
                    list.Sum(j => j.orphanShips).ToString(),
                    list.Sum(j => j.errors).ToString(),
                };
                EditorGUILayout.BeginHorizontal();
                for (int i = 0; i < row.Length; i++)
                {
                    var st = EditorStyles.label;
                    if ((i == 5 && stuck > 0) || (i >= 10 && row[i] != "0")) st = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(1f, 0.45f, 0.4f) } };
                    else if (i == 4 && fail > 0) st = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(1f, 0.75f, 0.3f) } };
                    GUILayout.Label(row[i], st, GUILayout.Width(widths[i]));
                }
                EditorGUILayout.EndHorizontal();
            }

            var problems = jobs.Where(j => j.outcome != "KAZANDI" && j.outcome != "KAYBETTI" || j.errors > 0 || j.extraShips > 0 || j.orphanShips > 0).ToList();
            if (problems.Count > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Sorunlu oyunlar", EditorStyles.boldLabel);
                foreach (var j in problems.Take(30))
                    EditorGUILayout.HelpBox($"{j.levelIndex + 1}. {j.levelName} · {(j.player == "smart" ? "Akıllı" : "Dikkatsiz")} #{j.run + 1}: {j.outcome} · kalan küp {j.remainingCubes}" +
                                            (string.IsNullOrEmpty(j.note) ? "" : $"\n{j.note}"), MessageType.Warning);
            }
            EditorGUILayout.EndScrollView();
        }

        private static string Pct(int a, int n) => n == 0 ? "-" : $"%{100 * a / n}";
    }

    /// <summary>Oyunları Play modunda yürüten, domain reload'lar arası durumu SessionState'te tutan sürücü.</summary>
    [InitializeOnLoad]
    public static class AutoPlaytestRunner
    {
        [Serializable]
        public class Config
        {
            public List<int> levels = new List<int>();
            public bool randomPlayer = true;
            public bool smartPlayer = true;
            public int runsPerPlayer = 3;
            public float timeScale = 3f;
            public float tapInterval = 0.25f;
            public float stuckSeconds = 30f;
            public float maxSecondsPerRun = 420f;
        }

        [Serializable]
        public class JobResult
        {
            public int levelIndex;
            public string levelName;
            public string player;
            public int run;
            public string outcome;
            public float gameSeconds;
            public int sends;
            public int failedTaps;
            public float slotsFullSeconds;
            public float idleDockedSeconds;
            public int maxDocked;
            public int linkedSends;
            public int extraShips;
            public int orphanShips;
            public int errors;
            public int remainingCubes;
            public int totalCubes;
            public string note;
        }

        [Serializable] private class ResultList { public List<JobResult> items = new List<JobResult>(); }

        private const string KeyRunning = "AutoPlaytest_Running";
        private const string KeyConfig = "AutoPlaytest_Config";
        private const string KeyResults = "AutoPlaytest_Results";
        private const string KeyStatus = "AutoPlaytest_Status";
        private const string KeySavedPref = "AutoPlaytest_SavedLevelPref";
        private const string KeyCsv = "AutoPlaytest_CsvPath";
        private const string PrefsConfig = "PixelGame_AutoPlaytestConfig";
        private const string ProgressPrefKey = "PixelGame_CurrentLevelIndex";

        public static bool IsRunning => SessionState.GetBool(KeyRunning, false);
        public static string Status => SessionState.GetString(KeyStatus, "");
        public static string LastCsvPath => SessionState.GetString(KeyCsv, "");

        static AutoPlaytestRunner()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static Config LoadConfig()
        {
            string json = EditorPrefs.GetString(PrefsConfig, "");
            var c = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Config>(json);
            return c ?? new Config();
        }

        public static List<JobResult> LoadResults()
        {
            string json = SessionState.GetString(KeyResults, "");
            if (string.IsNullOrEmpty(json)) return new List<JobResult>();
            return JsonUtility.FromJson<ResultList>(json)?.items ?? new List<JobResult>();
        }

        private static void SaveResults(List<JobResult> list) => SessionState.SetString(KeyResults, JsonUtility.ToJson(new ResultList { items = list }));
        private static void SetStatus(string s) => SessionState.SetString(KeyStatus, s);

        public static void Start(Config cfg)
        {
            if (EditorApplication.isPlaying) return;
            EditorPrefs.SetString(PrefsConfig, JsonUtility.ToJson(cfg));
            SessionState.SetString(KeyConfig, JsonUtility.ToJson(cfg));
            SessionState.SetString(KeyResults, "");
            SessionState.SetString(KeyCsv, "");
            SessionState.SetInt(KeySavedPref, PlayerPrefs.GetInt(ProgressPrefKey, -999));
            SessionState.SetBool(KeyRunning, true);
            SetStatus("Play modu başlatılıyor...");
            EditorApplication.isPlaying = true;
        }

        public static void Stop(string reason)
        {
            if (!IsRunning) return;
            SessionState.SetBool(KeyRunning, false);
            s_Driver = null;
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            var results = LoadResults();
            string csv = WriteCsv(results);
            SessionState.SetString(KeyCsv, csv);
            SetStatus($"Bitti: {reason}. {results.Count} oyun oynandı." + (string.IsNullOrEmpty(csv) ? "" : $" CSV: {csv}"));
            Time.timeScale = 1f;
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        private static void OnPlayModeChanged(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.EnteredPlayMode && IsRunning)
            {
                var cfg = JsonUtility.FromJson<Config>(SessionState.GetString(KeyConfig, "{}"));
                s_Driver = new Driver(cfg);
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
                Application.logMessageReceived -= OnLog;
                Application.logMessageReceived += OnLog;
            }
            else if (s == PlayModeStateChange.ExitingPlayMode && IsRunning)
            {
                Stop("Play modundan çıkıldı");
            }
            else if (s == PlayModeStateChange.EnteredEditMode)
            {
                // Kayıtlı 'mevcut level' tercihini teste başlamadan önceki haline getir
                int saved = SessionState.GetInt(KeySavedPref, int.MinValue);
                if (saved != int.MinValue)
                {
                    if (saved == -999) PlayerPrefs.DeleteKey(ProgressPrefKey); else PlayerPrefs.SetInt(ProgressPrefKey, saved);
                    PlayerPrefs.Save();
                    SessionState.EraseInt(KeySavedPref);
                }
            }
        }

        private static Driver s_Driver;
        private static void Tick() { if (s_Driver != null) s_Driver.Tick(); }
        private static void OnLog(string msg, string stack, LogType type)
        {
            if (s_Driver != null && (type == LogType.Exception || type == LogType.Error)) s_Driver.OnError(msg);
        }

        // -----------------------------------------------------------------
        private class Driver
        {
            private readonly Config m_Cfg;
            private readonly List<(int level, string player, int run)> m_Jobs = new List<(int, string, int)>();
            private int m_JobIndex = -1;
            private readonly System.Random m_Rng = new System.Random(12345);

            private LevelManager m_Lm;
            private ShipDispatcher m_Disp;
            private ShipQueuePool m_Pool;

            private JobResult m_Cur;
            private enum Phase { Loading, Playing, WaitingWinTransition, Recording }
            private Phase m_Phase;
            private float m_PhaseStartReal, m_LastTapReal, m_LastProgressGame, m_StartGame;
            private int m_LastProgressSig;
            private int m_SeqCount;
            private readonly HashSet<int> m_SeenShips = new HashSet<int>();
            private readonly Dictionary<int, float> m_OrphanSince = new Dictionary<int, float>();
            private readonly HashSet<int> m_OrphanCounted = new HashSet<int>();
            private string m_FirstError;

            public Driver(Config cfg)
            {
                m_Cfg = cfg;
                foreach (int lvl in cfg.levels)
                {
                    if (cfg.randomPlayer) for (int r = 0; r < cfg.runsPerPlayer; r++) m_Jobs.Add((lvl, "random", r));
                    if (cfg.smartPlayer) for (int r = 0; r < cfg.runsPerPlayer; r++) m_Jobs.Add((lvl, "smart", r));
                }
            }

            public void OnError(string msg)
            {
                if (m_Cur == null) return;
                m_Cur.errors++;
                if (m_FirstError == null) m_FirstError = msg.Length > 200 ? msg.Substring(0, 200) : msg;
            }

            public void Tick()
            {
                if (!EditorApplication.isPlaying) return;
                if (m_Lm == null) m_Lm = LevelManager.Instance != null ? LevelManager.Instance : UnityEngine.Object.FindFirstObjectByType<LevelManager>();
                if (m_Disp == null) m_Disp = ShipDispatcher.Instance;
                if (m_Pool == null) m_Pool = UnityEngine.Object.FindFirstObjectByType<ShipQueuePool>();
                if (m_Lm == null || m_Disp == null || m_Pool == null) return;

                float real = Time.realtimeSinceStartup;
                if (m_JobIndex < 0) { NextJob(); return; }

                switch (m_Phase)
                {
                    case Phase.Loading:
                        if (real - m_PhaseStartReal < 1.5f) return;
                        BeginPlaying();
                        return;
                    case Phase.WaitingWinTransition:
                        // Oyun kazanınca kendisi sonraki bölüme geçer; o geçiş bitmeden yeni bölüm yüklenmez
                        if (m_Lm.CurrentLevelIndex != m_Jobs[m_JobIndex].level || real - m_PhaseStartReal > 20f) Finish();
                        return;
                    case Phase.Playing:
                        PlayTick(real);
                        return;
                }
            }

            private void NextJob()
            {
                m_JobIndex++;
                if (m_JobIndex >= m_Jobs.Count) { Stop("Tüm oyunlar tamamlandı"); return; }
                var job = m_Jobs[m_JobIndex];
                var level = m_Lm.Levels[job.level];
                SetStatus($"Oynanıyor {m_JobIndex + 1}/{m_Jobs.Count}: {job.level + 1}. {(level ? level.name : "?")} · {(job.player == "smart" ? "Akıllı" : "Dikkatsiz")} #{job.run + 1}");

                var hud = CasualHudController.Instance;
                if (hud != null) hud.HideLevelFailPopup();

                m_Lm.LoadLevel(job.level);
                m_Phase = Phase.Loading;
                m_PhaseStartReal = Time.realtimeSinceStartup;
                m_Cur = new JobResult
                {
                    levelIndex = job.level,
                    levelName = level ? level.name : "?",
                    player = job.player,
                    run = job.run,
                };
                m_SeqCount = level != null && level.UseCustomWagonSequence && level.WagonSequence != null ? level.WagonSequence.Count : -1;
                m_SeenShips.Clear(); m_OrphanSince.Clear(); m_OrphanCounted.Clear(); m_FirstError = null;
            }

            private void BeginPlaying()
            {
                m_Phase = Phase.Playing;
                m_StartGame = Time.time;
                m_LastProgressGame = Time.time;
                m_LastProgressSig = -1;
                m_LastTapReal = 0f;
                m_Cur.totalCubes = m_Disp.GetTotalRemainingCubes();
            }

            private void PlayTick(float real)
            {
                if (Time.timeScale > 0f && Mathf.Abs(Time.timeScale - m_Cfg.timeScale) > 0.01f && !m_Disp.IsAutoPlacing) Time.timeScale = m_Cfg.timeScale;
                float dt = Time.deltaTime;
                float gameT = Time.time - m_StartGame;
                var job = m_Jobs[m_JobIndex];

                // Sonuç kontrolü
                if (m_Lm.CurrentLevelIndex != job.level) { m_Cur.outcome = "KAZANDI"; Finish(); return; }
                if (m_Disp.GetTotalRemainingCubes() == 0 && IsLevelEndPending())
                {
                    m_Cur.outcome = "KAZANDI";
                    m_Phase = Phase.WaitingWinTransition;
                    m_PhaseStartReal = real;
                    m_Cur.gameSeconds = gameT;
                    return;
                }
                if (m_Disp.IsLevelFailed) { m_Cur.outcome = "KAYBETTI"; Finish(); return; }

                // Metrikler
                var slots = m_Disp.Slots;
                int active = 0, docked = 0;
                if (slots != null)
                {
                    foreach (var s in slots)
                    {
                        if (s == null || !s.gameObject.activeInHierarchy) continue;
                        active++;
                        var ship = s.DockedShip;
                        if (ship == null) continue;
                        docked++;
                        if (ship.IsDocked && !ship.IsDeparting && ship.CanAcceptMore && !ship.HasPendingCargo && !m_Disp.HasExposedMatchingCube(ship.ShipColor))
                            m_Cur.idleDockedSeconds += dt;
                    }
                }
                if (active > 0 && docked >= active) m_Cur.slotsFullSeconds += dt;
                m_Cur.maxDocked = Mathf.Max(m_Cur.maxDocked, docked);

                foreach (var ship in ShipController.ActiveShips)
                {
                    if (ship == null || EditorUtility.IsPersistent(ship) || !ship.gameObject.activeInHierarchy || ship.Capacity <= 0) continue;
                    int id = ship.GetInstanceID();
                    m_SeenShips.Add(id);
                    bool orphan = !ship.IsDocked && !ship.IsMoving && !ship.IsDeparting && !m_Pool.WaitingShips.Contains(ship) && !DOTween.IsTweening(ship.transform);
                    if (orphan)
                    {
                        if (!m_OrphanSince.ContainsKey(id)) m_OrphanSince[id] = Time.time;
                        else if (Time.time - m_OrphanSince[id] > 2f && m_OrphanCounted.Add(id)) m_Cur.orphanShips++;
                    }
                    else m_OrphanSince.Remove(id);
                }

                // İlerleme / takılma
                int sig = m_Disp.GetTotalRemainingCubes() * 1000 + m_Cur.sends;
                if (sig != m_LastProgressSig) { m_LastProgressSig = sig; m_LastProgressGame = Time.time; }
                if (Time.time - m_LastProgressGame > m_Cfg.stuckSeconds)
                {
                    m_Cur.outcome = "TAKILDI";
                    m_Cur.note = Diagnose();
                    Finish();
                    return;
                }
                if (gameT > m_Cfg.maxSecondsPerRun) { m_Cur.outcome = "ZAMAN AŞIMI"; m_Cur.note = Diagnose(); Finish(); return; }

                // Oyuncu
                if (real - m_LastTapReal >= m_Cfg.tapInterval && !IsLevelEndPending())
                {
                    m_LastTapReal = real;
                    var pick = job.player == "smart" ? PickSmart() : PickRandom();
                    if (pick != null)
                    {
                        bool linked = pick.IsLinked;
                        if (m_Disp.TrySendShipFromQueue(pick)) { m_Cur.sends++; if (linked) m_Cur.linkedSends++; }
                        else m_Cur.failedTaps++;
                    }
                }
            }

            private List<ShipController> Candidates()
            {
                var list = new List<ShipController>();
                foreach (var sh in m_Pool.WaitingShips)
                    if (sh != null && sh.gameObject.activeInHierarchy && !sh.IsMoving && !sh.IsDocked && !sh.IsDeparting && (sh.IsLinked ? sh.CanDispatchLinked() : m_Pool.IsFrontRow(sh)))
                        list.Add(sh);
                return list;
            }

            private ShipController PickRandom()
            {
                var c = Candidates();
                return c.Count > 0 ? c[m_Rng.Next(c.Count)] : null;
            }

            private ShipController PickSmart()
            {
                var c = Candidates();
                if (c.Count == 0) return null;
                if (m_Disp.FindEmptySlot() == null) return null;   // slot yoksa bekle

                ShipController best = null;
                int bestScore = 0;
                foreach (var sh in c)
                {
                    if (sh.IsMysteryHidden) continue;
                    int exposed = m_Disp.GetExposedMatchingCubes(sh.ShipColor).Count;
                    if (sh.IsLinked && sh.LinkedPartner != null) exposed = Mathf.Min(exposed, m_Disp.GetExposedMatchingCubes(sh.LinkedPartner.ShipColor).Count);
                    int score = Mathf.Min(exposed, sh.Capacity) * 10 + exposed;
                    if (score > bestScore) { bestScore = score; best = sh; }
                }
                if (best != null) return best;

                // Açık renkli gemi yoksa: slotlarda toplayan gemi varsa bekle, yoksa (kilit olmasın) rastgele gönder
                foreach (var s in m_Disp.Slots)
                    if (s != null && s.DockedShip != null && s.DockedShip.HasPendingCargo) return null;
                return c[m_Rng.Next(c.Count)];
            }

            private bool IsLevelEndPending()
            {
                var f = typeof(ShipDispatcher).GetField("m_LevelEndPending", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                return f != null && (bool)f.GetValue(m_Disp);
            }

            private string Diagnose()
            {
                var sb = new StringBuilder();
                sb.Append($"kuyruk={m_Pool.GetActiveWaitingShips().Count} ");
                foreach (var s in m_Disp.Slots)
                {
                    if (s == null || !s.gameObject.activeInHierarchy) continue;
                    var sh = s.DockedShip;
                    sb.Append(sh == null ? "[boş] " : $"[{ColorUtility.ToHtmlStringRGB(sh.ShipColor)} {sh.CurrentCargo}/{sh.Capacity} açık={m_Disp.HasExposedMatchingCube(sh.ShipColor)}] ");
                }
                return sb.ToString();
            }

            private void Finish()
            {
                if (m_Cur != null)
                {
                    if (m_Cur.gameSeconds <= 0f) m_Cur.gameSeconds = Time.time - m_StartGame;
                    if (string.IsNullOrEmpty(m_Cur.outcome)) m_Cur.outcome = "BİLİNMİYOR";
                    m_Cur.remainingCubes = m_Cur.outcome == "KAZANDI" ? 0 : m_Disp.GetTotalRemainingCubes();
                    if (m_SeqCount > 0) m_Cur.extraShips = Mathf.Max(0, m_SeenShips.Count - m_SeqCount);
                    if (m_FirstError != null) m_Cur.note = (m_Cur.note ?? "") + (string.IsNullOrEmpty(m_Cur.note) ? "" : " | ") + "ilk hata: " + m_FirstError;
                    var results = LoadResults();
                    results.Add(m_Cur);
                    SaveResults(results);
                    m_Cur = null;
                }
                NextJob();
            }
        }

        private static string WriteCsv(List<JobResult> results)
        {
            if (results == null || results.Count == 0) return "";
            try
            {
                string dir = Path.Combine(Directory.GetCurrentDirectory(), "PlaytestReports");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, $"oyun_testi_{DateTime.Now:yyyyMMdd_HHmm}.csv");
                var sb = new StringBuilder();
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                sb.AppendLine("bolum_no;bolum;oyuncu;oyun;sonuc;oyun_sn;gonderim;basarisiz_dokunus;slot_dolu_sn;bosta_bekleme_sn;en_cok_yanasik;halatli_gonderim;ekstra_gemi;kayip_gemi;hata;kalan_kup;toplam_kup;not");
                foreach (var j in results)
                {
                    sb.AppendLine(string.Join(";", new[]
                    {
                        (j.levelIndex + 1).ToString(), j.levelName, j.player, (j.run + 1).ToString(), j.outcome,
                        j.gameSeconds.ToString("0.0", inv), j.sends.ToString(), j.failedTaps.ToString(),
                        j.slotsFullSeconds.ToString("0.0", inv), j.idleDockedSeconds.ToString("0.0", inv), j.maxDocked.ToString(),
                        j.linkedSends.ToString(), j.extraShips.ToString(), j.orphanShips.ToString(), j.errors.ToString(),
                        j.remainingCubes.ToString(), j.totalCubes.ToString(), (j.note ?? "").Replace(";", ",").Replace("\n", " ")
                    }));
                }
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                return path;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Oyun Testi] CSV yazılamadı: " + e.Message);
                return "";
            }
        }
    }
}
