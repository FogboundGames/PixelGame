using System.Collections.Generic;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Sahnede levellerin yönetimini ve geçişlerini sağlayan runtime bileşen.
    /// PixelArtGenerator ile tam entegre çalışır.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Level Manager")]
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        private const string ProgressPrefKey = "PixelGame_CurrentLevelIndex";

        [Header("📋 Bölüm Sırası (Tek Doğruluk Kaynağı)")]
        [Tooltip("Atanmışsa bölüm sırası için tek doğruluk kaynağıdır: Level Designer bunu günceller, " +
                 "buradaki liste her yenilemede (Awake/RefreshFromSequence) bu asset'ten yeniden doldurulur.")]
        [SerializeField] private LevelSequence m_LevelSequence;

        [Header("📋 Bölüm Listesi (Levels)")]
        [Tooltip("Oyundaki tüm bölümler. 'Bölüm Sırası' atanmışsa bu liste sadece bir önbellektir; " +
                 "gerçek kaynak yukarıdaki LevelSequence asset'idir.")]
        [SerializeField] private List<PixelLevelData> m_Levels = new List<PixelLevelData>();

        [Tooltip("Şu an aktif olan bölüm indeksi (0 tabanlı)")]
        [SerializeField] private int m_CurrentLevelIndex = 0;

        [Header("⚙️ Jeneratör Referansı")]
        [SerializeField] private PixelArtGenerator m_Generator;

        public List<PixelLevelData> Levels => m_Levels;
        public LevelSequence Sequence { get => m_LevelSequence; set => m_LevelSequence = value; }
        public int CurrentLevelIndex => m_CurrentLevelIndex;
        public PixelLevelData CurrentLevel => (m_Levels != null && m_CurrentLevelIndex >= 0 && m_CurrentLevelIndex < m_Levels.Count) ? m_Levels[m_CurrentLevelIndex] : null;

        /// <summary>
        /// LevelSequence asset'i atanmışsa m_Levels'i onunla eşitler. LevelSequence tek doğruluk
        /// kaynağı olduğu için, m_Levels'te elle (Inspector'da) yapılmış farklı bir sıralama varsa
        /// burada ezilir.
        /// </summary>
        public void RefreshFromSequence()
        {
            if (m_LevelSequence == null) return;

            PixelLevelData currentLevel = CurrentLevel;
            m_Levels = new List<PixelLevelData>(m_LevelSequence.Levels);

            if (currentLevel != null)
            {
                int index = m_Levels.IndexOf(currentLevel);
                if (index >= 0) m_CurrentLevelIndex = index;
            }
        }

        private void Awake()
        {
            Instance = this;
            RefreshFromSequence();
            EnsureGenerator();
        }

        private void Start()
        {
            if (Application.isPlaying && m_Levels.Count > 0)
            {
                EnsureGenerator();

                // Kullanıcı isteği: "level save durumu olsun hangi levelde kaldıysak oradan devam etsin"
                int targetIndex = PlayerPrefs.HasKey(ProgressPrefKey)
                    ? PlayerPrefs.GetInt(ProgressPrefKey)
                    : m_CurrentLevelIndex;

                targetIndex = Mathf.Clamp(targetIndex, 0, m_Levels.Count - 1);

                // Eğer sahne düzenleme koruması (PreserveSceneEdits) AÇIKSA ve sahnede zaten küpler varsa
                // ve bu küpler kayıtlı seviyeyle uyuşuyorsa sahneyi koru.
                if (m_Generator != null && m_Generator.PreserveSceneEdits)
                {
                    Transform container = m_Generator.CubesContainer;
                    int childCount = container != null ? container.childCount : 0;
                    if (childCount > 0 && m_Generator.ActiveLevelData == m_Levels[targetIndex])
                    {
                        m_CurrentLevelIndex = targetIndex;
                        m_Generator.BindExistingLevel(m_Levels[targetIndex]);
                        Debug.Log($"<color=#00FFAA><b>[LevelManager]</b></color> Sahne düzeni korundu. Aktif Level {m_CurrentLevelIndex + 1}: '{m_Levels[targetIndex].LevelName}'");
                        return;
                    }
                }

                // Kaldığı seviyeden devam et:
                LoadLevel(targetIndex);
            }
        }

        public void LoadLevel(int index)
        {
            EnsureGenerator();
            if (m_Generator == null)
            {
                Debug.LogError("[LevelManager] PixelArtGenerator bulunamadı!");
                return;
            }

            if (m_Levels == null || m_Levels.Count == 0)
            {
                Debug.LogWarning("[LevelManager] Level listesi boş!");
                return;
            }

            m_CurrentLevelIndex = Mathf.Clamp(index, 0, m_Levels.Count - 1);
            Time.timeScale = 1.0f;
            PixelLevelData level = m_Levels[m_CurrentLevelIndex];
            if (level != null)
            {
                m_Generator.LoadLevel(level);
                Debug.Log($"<color=#00FFAA><b>[LevelManager]</b></color> Level {m_CurrentLevelIndex + 1}: '{level.LevelName}' yüklendi!");

                if (Application.isPlaying)
                {
                    PlayerPrefs.SetInt(ProgressPrefKey, m_CurrentLevelIndex);
                    PlayerPrefs.Save();
                }
            }
        }

        /// <summary>
        /// Kayıtlı oyuncu ilerlemesini (kaldığı bölüm) siler; bir sonraki başlangıçta 0. bölümden
        /// başlar. "Yeni Oyun" / ilerlemeyi sıfırlama gibi menü aksiyonlarından çağırmak için.
        /// </summary>
        [ContextMenu("İlerlemeyi Sıfırla (PlayerPrefs)")]
        public void ResetProgress()
        {
            PlayerPrefs.DeleteKey(ProgressPrefKey);
            PlayerPrefs.Save();
            m_CurrentLevelIndex = 0;
            Debug.Log("<color=#FFAA00><b>[LevelManager]</b></color> 🔄 Seviye ilerlemesi sıfırlandı (Bölüm 1'e dönüldü).");
        }

        public void NextLevel()
        {
            if (m_Levels.Count == 0) return;
            int next = (m_CurrentLevelIndex + 1) % m_Levels.Count;
            LoadLevel(next);
        }

        public void PreviousLevel()
        {
            if (m_Levels.Count == 0) return;
            int prev = m_CurrentLevelIndex - 1;
            if (prev < 0) prev = m_Levels.Count - 1;
            LoadLevel(prev);
        }

        public void ReloadCurrentLevel()
        {
            LoadLevel(m_CurrentLevelIndex);
        }

        private void EnsureGenerator()
        {
            if (m_Generator == null)
            {
                m_Generator = GetComponent<PixelArtGenerator>();
                if (m_Generator == null)
                    m_Generator = Object.FindFirstObjectByType<PixelArtGenerator>();
            }
        }
    }
}
