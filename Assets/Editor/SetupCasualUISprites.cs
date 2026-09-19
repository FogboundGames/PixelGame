using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    public static class SetupCasualUISprites
    {
        public static void ConfigureAll()
        {
            SetSprite("Assets/UI/CasualUI/bg_dark_navy.png", Vector4.zero);
            SetSprite("Assets/UI/CasualUI/board_frame_25d.png", new Vector4(135, 135, 135, 135));
            SetSprite("Assets/UI/CasualUI/board_inner_well.png", new Vector4(36, 36, 36, 36));
            SetSprite("Assets/UI/CasualUI/board_shadow.png", new Vector4(52, 52, 52, 52));
            SetSprite("Assets/UI/CasualUI/slot_pod_25d.png", new Vector4(45, 45, 45, 45));
            SetSprite("Assets/UI/CasualUI/slot_shadow.png", new Vector4(36, 36, 36, 36));
            SetSprite("Assets/UI/CasualUI/btn_settings.png", new Vector4(36, 36, 36, 36));
            SetSprite("Assets/UI/CasualUI/ui_pill.png", new Vector4(44, 44, 44, 44));
            SetSprite("Assets/UI/CasualUI/progress_station_pod.png", Vector4.zero);
            SetSprite("Assets/UI/CasualUI/top_bar_pod.png", Vector4.zero);
            SetSprite("Assets/UI/CasualUI/mascot_green.png", Vector4.zero);
            SetSprite("Assets/UI/CasualUI/mascot_brown.png", Vector4.zero);
            SetSprite("Assets/UI/CasualUI/mascot_orange.png", Vector4.zero);
            SetSprite("Assets/UI/CasualUI/mascot_yellow.png", Vector4.zero);
            SetSprite("Assets/UI/CasualUI/icon_gear.png", Vector4.zero);
            SetSprite("Assets/UI/CasualUI/icon_heart.png", Vector4.zero);
            SetSprite("Assets/UI/CasualUI/icon_coin.png", Vector4.zero);
            SetSprite("Assets/UI/CasualUI/btn_plus.png", Vector4.zero);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00FFAA><b>[CasualUI]</b></color> Tüm UI Sprite'ları ve 9-slice sınırları başarıyla ayarlandı!");
        }

        private static void SetSprite(string path, Vector4 border)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
    }
}
