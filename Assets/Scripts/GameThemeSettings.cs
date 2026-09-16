using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PixelGame
{
    /// <summary>
    /// Tüm oyun genelindeki Vagon, Madenci, Ray/Çevre parça renklerini ve
    /// Toon görsel stilini merkezi olarak yöneten Global Tema Ayarları.
    /// Bölümlere özel değil, oyun genelinde tek bir noktadan yönetilir.
    /// </summary>
    [CreateAssetMenu(fileName = "GameThemeSettings", menuName = "PixelGame/Global Game Theme Settings", order = 10)]
    public class GameThemeSettings : ScriptableObject
    {
        public const string ResourcePath = "GameThemeSettings";
        public const string AssetPath = "Assets/Resources/GameThemeSettings.asset";

        private static GameThemeSettings s_Instance;

        [Header("🎨 Genel Vagon, Madenci & Çevre Renk Teması")]
        [SerializeField] private LevelColorTheme m_Theme = new LevelColorTheme();

        [Header("🧪 Editör Test Rengi")]
        [SerializeField] private Color m_PreviewBlockColor = new Color32(230, 40, 40, 255);

        public LevelColorTheme Theme
        {
            get
            {
                if (m_Theme == null) m_Theme = new LevelColorTheme();
                m_Theme.EnsureAllPartsPresent();
                return m_Theme;
            }
            set => m_Theme = value;
        }

        public Color PreviewBlockColor
        {
            get => m_PreviewBlockColor;
            set => m_PreviewBlockColor = value;
        }

        public static GameThemeSettings Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = Resources.Load<GameThemeSettings>(ResourcePath);

#if UNITY_EDITOR
                    if (s_Instance == null)
                    {
                        s_Instance = AssetDatabase.LoadAssetAtPath<GameThemeSettings>(AssetPath);
                    }

                    if (s_Instance == null)
                    {
                        s_Instance = CreateOrLoadSettings();
                    }
#endif
                    if (s_Instance == null)
                    {
                        s_Instance = CreateInstance<GameThemeSettings>();
                    }
                }
                return s_Instance;
            }
        }

#if UNITY_EDITOR
        public static GameThemeSettings CreateOrLoadSettings()
        {
            GameThemeSettings asset = Resources.Load<GameThemeSettings>(ResourcePath);
            if (asset != null) return asset;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            asset = CreateInstance<GameThemeSettings>();
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#00FFAA><b>[GameThemeSettings]</b></color> Yeni Global Tema ayar dosyası oluşturuldu: {AssetPath}");
            return asset;
        }
#endif

        /// <summary>
        /// Aktif geçerli temayı döndürür. Null dönmez.
        /// </summary>
        public static LevelColorTheme CurrentTheme
        {
            get
            {
                var inst = Instance;
                return inst != null ? inst.Theme : new LevelColorTheme();
            }
        }
    }
}
