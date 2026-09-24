using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class SetupPoolSlotShadows
    {
        private const string ShadowSpritePath = "Assets/UI/PoolSlot_Shadow.png";

        static SetupPoolSlotShadows()
        {
            // Otomatik tetikleme kapatıldı: sahne editör açılışında değiştirilmesin.
            // Gerekirse Tools menüsünden elle çalıştırılır.
            // EditorApplication.delayCall += ApplyPoolShadows;
        }

        // [MenuItem("Tools/PixelGame/🌑 Havuz Slot Gölgelerini Uygula (Setup Pool Fake Shadows)", priority = 4)]
        public static void ApplyPoolShadows()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;

            // 1. Sprite Ayarlarını Yapılandır
            ConfigureSprite(ShadowSpritePath);
            AssetDatabase.SaveAssets();

            Sprite shadowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShadowSpritePath);

            // 2. Sahnedeki TruckPool nesnesini bul ve güncelle
            TruckPool pool = Object.FindFirstObjectByType<TruckPool>();
            if (pool != null)
            {
                pool.UpdateShadows();
                pool.RefreshEditorPreview();
                EditorUtility.SetDirty(pool.gameObject);

                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                Debug.Log("<color=#00FFAA><b>[PoolSlotShadows]</b></color> Havuz vagon/rozet nesnelerine dinamik alt nesne (child) 3B temas gölgeleri (Fake Shadow) başarıyla uygulandı!");
            }
        }

        private static void ConfigureSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (importer.alphaIsTransparency != true) { importer.alphaIsTransparency = true; dirty = true; }
            if (importer.mipmapEnabled != false) { importer.mipmapEnabled = false; dirty = true; }
            if (importer.filterMode != FilterMode.Bilinear) { importer.filterMode = FilterMode.Bilinear; dirty = true; }

            if (dirty)
            {
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
        }
    }
}
