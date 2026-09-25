using System.Collections.Generic;
using UnityEngine;

namespace PixelGame
{
    /// <summary>
    /// Oyundaki tüm bölümlerin oynanış sırasını tutan tek doğruluk kaynağı (single source of truth).
    /// Level Designer penceresi bu asset'i günceller; LevelManager sahnede bunu okur. Böylece
    /// sıralama sahneye değil, versiyonlanabilir bir asset'e bağlı olur ve LevelManager'ın kendi
    /// Inspector'ında elle yapılan bir sıralama, bir sonraki eşitlemede bu asset tarafından ezilir.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelSequence", menuName = "PixelGame/Level Sequence")]
    public class LevelSequence : ScriptableObject
    {
        [SerializeField] private List<PixelLevelData> m_Levels = new List<PixelLevelData>();

        public List<PixelLevelData> Levels => m_Levels;

        public void SetLevels(IEnumerable<PixelLevelData> levels)
        {
            m_Levels.Clear();
            m_Levels.AddRange(levels);
        }
    }
}
