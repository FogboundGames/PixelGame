using UnityEngine;
using UnityEditor;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class SetupShipFakeShadow
    {
        private const string PrefabPath = "Assets/Prefabs/Ship_Boat.prefab";
        private const string ShadowMatPath = "Assets/Materials/ShipFakeShadow_Mat.mat";
        private const string RunKey = "Ship_FakeShadow_Setup_v1";

        static SetupShipFakeShadow()
        {
            // Otomatik tetikleme kapatıldı: Unity veya PC yeniden başladığında sahneyi habersiz değiştirmemesi için.
            // Gerekirse Tools menüsünden elle çalıştırılır.
            /*
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(RunKey, false)) return;
                SessionState.SetBool(RunKey, true);
                ApplyToPrefab();
            };
            */
        }

        [MenuItem("Tools/PixelGame/🌑 Gemi Prefabına Fake Shadow Ekle")]
        public static void ApplyToPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null)
            {
                Debug.LogWarning($"[PixelGame] {PrefabPath} yüklenemedi!");
                return;
            }

            try
            {
                ShipController ship = root.GetComponent<ShipController>();
                if (ship != null)
                {
                    ship.EnsureFakeShadow();
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("<color=#00FFAA><b>[PixelGame]</b></color> Ship_Boat prefab'ına ve sahneye sahte gölge (Ship Fake Shadow) başarıyla kaydedildi!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
