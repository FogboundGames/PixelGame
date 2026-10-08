using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using PixelGame;

namespace PixelGame.EditorTools
{
    [InitializeOnLoad]
    public static class SetupShipChimneySmoke
    {
        static SetupShipChimneySmoke()
        {
            EditorApplication.delayCall += ExecuteAutoSetup;
        }

        private static void ExecuteAutoSetup()
        {
            // Play modunda (oyun sırasında script yeniden derlenince) prefab kaydetmek ve sahne gemilerini
            // yeniden kurmak çalışan oyunu bozar; MarkSceneDirty de play modunda exception fırlatır.
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SetupPrefabAndSceneShips(silent: false);
        }

        [MenuItem("PixelGame/Ships/Setup Chimney & Smoke on Ships")]
        public static void MenuSetup()
        {
            SetupPrefabAndSceneShips(silent: false);
        }

        public static void SetupPrefabAndSceneShips(bool silent = false)
        {
            string prefabPath = "Assets/Prefabs/Ship_Boat.prefab";
            int updatedPrefabs = 0;
            int updatedSceneShips = 0;

            // 1. Prefab Güncellemesi
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset != null)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                if (root != null)
                {
                    ShipController ship = root.GetComponent<ShipController>();
                    if (ship != null)
                    {
                        SerializedObject so = new SerializedObject(ship);
                        var propCorner = so.FindProperty("m_ChimneyCorner");
                        var propPos = so.FindProperty("m_ChimneyLocalPos");
                        if (propCorner != null && propCorner.enumValueIndex != (int)ShipController.ChimneyCorner.Custom)
                        {
                            propCorner.enumValueIndex = (int)ShipController.DefaultChimneyCorner;
                            if (propPos != null) propPos.vector3Value = ShipController.GetChimneyPositionForCorner(ShipController.DefaultChimneyCorner);
                        }
                        so.ApplyModifiedProperties();

                        ship.EnsureChimneyAndSmoke();
                        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                        updatedPrefabs++;
                    }
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            // 2. Sahnedeki Mevcut Gemilerin Güncellenmesi
            ShipController[] sceneShips = Object.FindObjectsByType<ShipController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (sceneShips != null && sceneShips.Length > 0)
            {
                foreach (var ship in sceneShips)
                {
                    if (ship != null)
                    {
                        SerializedObject so = new SerializedObject(ship);
                        var propCorner = so.FindProperty("m_ChimneyCorner");
                        var propPos = so.FindProperty("m_ChimneyLocalPos");
                        if (propCorner != null && propCorner.enumValueIndex != (int)ShipController.ChimneyCorner.Custom)
                        {
                            propCorner.enumValueIndex = (int)ShipController.DefaultChimneyCorner;
                            if (propPos != null) propPos.vector3Value = ShipController.GetChimneyPositionForCorner(ShipController.DefaultChimneyCorner);
                        }
                        so.ApplyModifiedProperties();

                        ship.EnsureChimneyAndSmoke();
                        EditorUtility.SetDirty(ship);
                        updatedSceneShips++;
                    }
                }

                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.IsValid() && activeScene.isLoaded)
                {
                    EditorSceneManager.MarkSceneDirty(activeScene);
                }
            }

            if (!silent)
            {
                Debug.Log($"<color=#00FFAA><b>[ShipChimney]</b></color> 💨 Baca & Duman efekti başarıyla uygulandı! Prefab: {updatedPrefabs}, Sahne Gemileri: {updatedSceneShips}");
            }
        }
    }
}
