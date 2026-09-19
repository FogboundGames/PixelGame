using TMPro;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Üst HUD şeridini (seviye, can, altın) günceller.
    ///
    /// Henüz bir can/ekonomi sistemi olmadığı için can ve altın burada sabit
    /// başlangıç değerleriyle gösterilir; gerçek sistem geldiğinde <see cref="SetLives"/>
    /// ve <see cref="SetCoins"/> oradan çağrılabilir. Seviye numarası ise
    /// <see cref="PixelArtGenerator.LevelLoaded"/> olayına ve <see cref="LevelManager"/>'a
    /// bağlanarak gerçek zamanlı takip edilir.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PixelGame/Casual Hud Controller")]
    public class CasualHudController : MonoBehaviour
    {
        [Header("🔗 Referanslar")]
        [SerializeField] private TextMeshProUGUI m_LevelText;
        [SerializeField] private TextMeshProUGUI m_LivesText;
        [SerializeField] private TextMeshProUGUI m_CoinsText;

        [Header("🧪 Başlangıç Değerleri (ekonomi sistemi gelene kadar)")]
        [SerializeField] private int m_StartingLives = 3;
        [SerializeField] private int m_StartingCoins = 250;

        private int m_CurrentLives;
        private int m_CurrentCoins;

        public TextMeshProUGUI LevelText { get => m_LevelText; set => m_LevelText = value; }
        public TextMeshProUGUI LivesText { get => m_LivesText; set => m_LivesText = value; }
        public TextMeshProUGUI CoinsText { get => m_CoinsText; set => m_CoinsText = value; }

        public int CurrentLives => m_CurrentLives;
        public int CurrentCoins => m_CurrentCoins;

        private void OnEnable()
        {
            PixelArtGenerator.LevelLoaded += HandleLevelLoaded;
        }

        private void OnDisable()
        {
            PixelArtGenerator.LevelLoaded -= HandleLevelLoaded;
        }

        private void Start()
        {
            SetLives(m_StartingLives);
            SetCoins(m_StartingCoins);
            RefreshLevelFromScene();
        }

        private void RefreshLevelFromScene()
        {
            LevelManager levelManager = FindFirstObjectByType<LevelManager>();
            int number = levelManager != null ? levelManager.CurrentLevelIndex + 1 : 1;
            SetLevel(number);
        }

        private void HandleLevelLoaded(PixelLevelData level)
        {
            RefreshLevelFromScene();
        }

        public void SetLevel(int number)
        {
            if (m_LevelText != null) m_LevelText.text = $"LEVEL {Mathf.Max(1, number)}";
        }

        public void SetLives(int count)
        {
            m_CurrentLives = Mathf.Max(0, count);
            if (m_LivesText != null) m_LivesText.text = m_CurrentLives.ToString();
        }

        public void SetCoins(int count)
        {
            m_CurrentCoins = Mathf.Max(0, count);
            if (m_CoinsText != null) m_CoinsText.text = FormatCoins(m_CurrentCoins);
        }

        /// <summary>1000 üstü değerleri "22K" gibi kısaltır; casual HUD'larda yaygın gösterimdir.</summary>
        private static string FormatCoins(int count)
        {
            if (count >= 1000) return (count / 1000f).ToString("0.#") + "K";
            return count.ToString();
        }
    }
}
