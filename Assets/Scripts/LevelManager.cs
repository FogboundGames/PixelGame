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

        [Header("📋 Bölüm Listesi (Levels)")]
        [Tooltip("Oyundaki tüm bölümler")]
        [SerializeField] private List<PixelLevelData> m_Levels = new List<PixelLevelData>();

        [Tooltip("Şu an aktif olan bölüm indeksi (0 tabanlı)")]
        [SerializeField] private int m_CurrentLevelIndex = 0;

        [Header("⚙️ Jeneratör Referansı")]
        [SerializeField] private PixelArtGenerator m_Generator;

        public List<PixelLevelData> Levels => m_Levels;
        public int CurrentLevelIndex => m_CurrentLevelIndex;
        public PixelLevelData CurrentLevel => (m_Levels != null && m_CurrentLevelIndex >= 0 && m_CurrentLevelIndex < m_Levels.Count) ? m_Levels[m_CurrentLevelIndex] : null;

        private void Awake()
        {
            Instance = this;
            EnsureGenerator();
        }

        private void Start()
        {
            if (Application.isPlaying && m_Levels.Count > 0)
            {
                EnsureGenerator();
                // Sahnedeki mevcut küpler ve sahne koruması açıksa bunları silip yeniden üretme!
                if (m_Generator != null)
                {
                    Transform container = m_Generator.CubesContainer;
                    int childCount = container != null ? container.childCount : 0;

                    if (childCount > 0 || m_Generator.PreserveSceneEdits)
                    {
                        // Sahnedeki küpler hangi bölüme aitse ONU bağla.
                        PixelLevelData level = m_Generator.ActiveLevelData;

                        if (level != null)
                        {
                            int index = m_Levels.IndexOf(level);
                            if (index >= 0) m_CurrentLevelIndex = index;
                        }
                        else
                        {
                            m_CurrentLevelIndex = Mathf.Clamp(m_CurrentLevelIndex, 0, m_Levels.Count - 1);
                            level = m_Levels[m_CurrentLevelIndex];
                        }

                        if (level != null)
                        {
                            m_Generator.BindExistingLevel(level);
                            Debug.Log($"<color=#00FFAA><b>[LevelManager]</b></color> Sahnedeki mevcut {childCount} küp ve sahne tasarımı korundu. Aktif Level: '{level.LevelName}'");
                        }
                        return;
                    }
                }

                LoadLevel(m_CurrentLevelIndex);
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
            PixelLevelData level = m_Levels[m_CurrentLevelIndex];
            if (level != null)
            {
                m_Generator.LoadLevel(level);
                Debug.Log($"<color=#00FFAA><b>[LevelManager]</b></color> Level {m_CurrentLevelIndex + 1}: '{level.LevelName}' yüklendi!");
            }
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
