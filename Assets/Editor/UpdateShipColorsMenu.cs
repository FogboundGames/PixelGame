using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PixelGame.Editor
{
    public static class UpdateShipColorsMenu
    {
        [MenuItem("Tools/PixelGame/🚢 Gemi Güvertelerini Kahverengi Yap ve Yenile")]
        public static void RefreshShipDeckColors()
        {
            ShipController.ClearMaterialCache();

            var ships = Object.FindObjectsByType<ShipController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int count = 0;
            foreach (var ship in ships)
            {
                if (ship != null)
                {
                    ship.ApplyColorToShip(ship.ShipColor);
                    EditorUtility.SetDirty(ship);
                    count++;
                }
            }

            // Prefabları da güncelle
            string[] guids = AssetDatabase.FindAssets("t:Prefab Ship", new[] { "Assets/Prefabs" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    var shipCtrl = prefab.GetComponent<ShipController>();
                    if (shipCtrl != null)
                    {
                        shipCtrl.ApplyColorToShip(shipCtrl.ShipColor);
                        EditorUtility.SetDirty(prefab);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"<color=#00FF88><b>[ShipDeckUpdate]</b></color> {count} geminin güvertesi başarıyla sıcak ahşap kahverengi (teak wood brown) tonuna güncellendi!");
            EditorUtility.DisplayDialog("Gemi Güverteleri Güncellendi", $"{count} gemi güvertesi başarıyla kahverengi yapıldı.", "Tamam");
        }
    }
}
