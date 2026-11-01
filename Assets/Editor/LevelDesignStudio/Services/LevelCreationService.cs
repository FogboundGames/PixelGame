using UnityEngine;

namespace PixelGame.Editor.Studio
{
    /// <summary>
    /// Yeni level oluşturma. Görsel ayarların ve LevelSequence kaydının eskisiyle birebir aynı olması için eski pencerenin
    /// kendi kodunu kullanır: level sona eklenir, sıra ve sahnedeki LevelManager eşitlenir.
    /// </summary>
    public static class LevelCreationService
    {
        public static void CreateFromTexture(Texture2D texture) => WithDesigner(d => d.CreateLevelFromTexture(texture));

        public static void CreateEmpty() => WithDesigner(d => d.CreateNewLevel());

        private static void WithDesigner(System.Action<PixelLevelDesignerWindow> action)
        {
            var designer = ScriptableObject.CreateInstance<PixelLevelDesignerWindow>();
            try { action(designer); }
            finally { Object.DestroyImmediate(designer); }
        }
    }
}
