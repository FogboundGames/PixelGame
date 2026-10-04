using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// Bölüm Stüdyosu tuvalinin Unity Undo sistemine kaydedilen kopyası.
    /// LevelCanvas düz bir sınıf olduğu için Undo onu doğrudan izleyemez; her değişiklikten önce
    /// bu obje Undo.RecordObject ile kaydedilir, değişiklikten sonra tuvalden yeniden doldurulur.
    /// Ctrl+Z / Ctrl+Y sonrası tuval bu objeden geri yüklenir.
    /// </summary>
    public class LevelCanvasUndoState : ScriptableObject
    {
        [SerializeField] private int m_Width;
        [SerializeField] private int m_Height;
        [SerializeField] private byte[] m_Cells;
        [SerializeField] private Color[] m_Palette;

        public void CaptureFrom(LevelCanvas canvas)
        {
            if (canvas == null) return;
            m_Width = canvas.Width;
            m_Height = canvas.Height;
            m_Cells = canvas.CopyCells();
            m_Palette = canvas.CustomPalette != null ? (Color[])canvas.CustomPalette.Clone() : null;
        }

        public void ApplyTo(LevelCanvas canvas)
        {
            if (canvas == null || m_Cells == null || m_Width <= 0 || m_Height <= 0) return;
            canvas.LoadState(m_Width, m_Height, m_Cells, m_Palette);
        }
    }
}
