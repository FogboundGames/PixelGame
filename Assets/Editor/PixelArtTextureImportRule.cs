using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    /// <summary>
    /// Piksel art kaynak klasörlerine giren resimleri her zaman sıkıştırmasız, nokta filtreli ve mipmap'siz içe aktarır.
    /// Varsayılan ayarlarda Unity resmi aktif platformun formatıyla (Android: ASTC 6x6) sıkıştırıyordu; 6x6 bloklu
    /// sıkıştırma 32x32 piksel art'ın renklerini bozuyor, Bölüm Stüdyosu da bozuk renkleri okuyordu.
    /// </summary>
    public class PixelArtTextureImportRule : AssetPostprocessor
    {
        private static readonly string[] PixelArtFolders =
        {
            "Assets/25x25/",
            "Assets/32x32/",
            "Assets/PixelArt/",
        };

        public static bool IsPixelArtPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            foreach (var folder in PixelArtFolders)
            {
                if (path.StartsWith(folder)) return true;
            }
            return false;
        }

        private void OnPreprocessTexture()
        {
            if (!IsPixelArtPath(assetPath)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
        }
    }
}
