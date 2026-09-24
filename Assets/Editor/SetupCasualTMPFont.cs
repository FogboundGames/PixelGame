using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

namespace PixelGame.Editor
{
    public static class SetupCasualTMPFont
    {
        public const string SourceFontPath = "Assets/Fonts/LilitaOne-Regular.ttf";
        public const string SDFAssetPath = "Assets/Fonts/LilitaOne-Regular SDF.asset";

        // [MenuItem("Tools/PixelGame/🔤 LilitaOne TextMeshPro Font Asset Oluştur")]
        public static TMP_FontAsset GetOrCreateFontAsset()
        {
            try
            {
                TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SDFAssetPath);
                if (existing != null) return existing;

                Font font = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
                if (font == null)
                {
                    Debug.LogWarning($"[SetupCasualTMPFont] Kaynak font bulunamadı: {SourceFontPath}");
                    return null;
                }

                // TMP_Settings kontrolü: Eğer TMP_Settings yüklü değilse CreateFontAsset NullReferenceException atar.
                // Bu durumda güvenli şekilde null dönülür, sistem LilitaOne-Regular TTF fontunu kullanır.
                if (TMP_Settings.instance == null)
                {
                    return null;
                }

                // Create Dynamic SDF Font Asset
                TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                    font, 
                    90, 
                    9, 
                    GlyphRenderMode.SDFAA, 
                    1024, 
                    1024, 
                    AtlasPopulationMode.Dynamic, 
                    true
                );

                if (fontAsset == null)
                {
                    Debug.LogWarning("[SetupCasualTMPFont] TMP_FontAsset.CreateFontAsset başarısız oldu.");
                    return null;
                }

                fontAsset.name = "LilitaOne-Regular SDF";

                AssetDatabase.CreateAsset(fontAsset, SDFAssetPath);
                if (fontAsset.material != null)
                {
                    fontAsset.material.name = "LilitaOne-Regular SDF Material";
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                }
                if (fontAsset.atlasTexture != null)
                {
                    fontAsset.atlasTexture.name = "LilitaOne-Regular Atlas";
                    AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
                }

                EditorUtility.SetDirty(fontAsset);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"<color=#00FFAA><b>[SetupCasualTMPFont]</b></color> TextMeshPro SDF Font Asset oluşturuldu: {SDFAssetPath}");
                return fontAsset;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SetupCasualTMPFont] TMP Font Asset oluşturulurken hata oluştu (standart fonta dönülüyor): {ex.Message}");
                return null;
            }
        }
    }
}
