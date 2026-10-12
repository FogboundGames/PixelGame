using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PixelGame.Editor
{
    public static class SetupStraightMarinaPier
    {
        private const string ScenePath = "Assets/Scenes/Gemi.unity";

        [MenuItem("PixelGame/⚓ Setup Straight Marina Pier (Düz İskele Slotları)")]
        public static void ApplyStraightPierSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath);
            }

            Debug.Log("<color=#00FFAA><b>[StraightPier]</b></color> Referans görsele tıpatıp uyarlama başlatıldı...");

            // 1. [Marina_Curved_Pier] nesnesini gizle
            GameObject curvedPier = GameObject.Find("[Marina_Curved_Pier]");
            if (curvedPier != null && curvedPier.activeSelf)
            {
                Undo.RecordObject(curvedPier, "Hide Curved Pier");
                curvedPier.SetActive(false);
                EditorUtility.SetDirty(curvedPier);
                Debug.Log("<color=#00FFAA><b>[StraightPier]</b></color> [Marina_Curved_Pier] gizlendi!");
            }

            // 2. MarinaSlotLayout'u düz iskeleye ve 5 slota ayarla
            MarinaSlotLayout layout = Object.FindFirstObjectByType<MarinaSlotLayout>();
            if (layout != null)
            {
                Undo.RecordObject(layout, "Setup Straight Pier");
                layout.EnableCurvedPier = false;
                layout.UsePerCountSettings = true;
                layout.SlotCount = 5;
                layout.MatchBackgroundBays = true;
                layout.BayCenterPx = 540f;
                layout.BayCenterYPx = 1353f;
                layout.BaySpacingPx = 174f;
                layout.BayFillRatio = 0.82f;
                layout.HideSlotIndicators = true;
                layout.ResetToCalibratedDefaults(0);

                // Slotlardaki gemileri tamamen temizle (referans görselde 5 slot da boş)
                var slots = layout.GetComponentsInChildren<ShipSlot>(true);
                foreach (var s in slots)
                {
                    s.ReleaseShip();
                    for (int c = s.transform.childCount - 1; c >= 0; c--)
                    {
                        var child = s.transform.GetChild(c);
                        if (child.GetComponent<ShipController>() != null)
                        {
                            Object.DestroyImmediate(child.gameObject);
                        }
                    }
                }

                // Sahnedeki fazlalık tüm gemileri temizle
                var allOldShips = Object.FindObjectsByType<ShipController>(FindObjectsSortMode.None);
                foreach (var ship in allOldShips)
                {
                    if (ship != null) Object.DestroyImmediate(ship.gameObject);
                }

                layout.ApplyLayout();
                EditorUtility.SetDirty(layout);
                EditorUtility.SetDirty(layout.gameObject);
                Debug.Log("<color=#00FFAA><b>[StraightPier]</b></color> MarinaSlotLayout düz iskeleye ayarlandı!");
            }

            // 3. Orijinal Yelkenliyi oluştur (7 kahverengi, 30 beyaz, 11 kırmızı, mavi gövde)
            PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
            PixelLevelData levelData = AssetDatabase.LoadAssetAtPath<PixelLevelData>("Assets/Levels/Level_30_Yelkenli.asset");
            if (gen != null && levelData != null)
            {
                Undo.RecordObject(gen, "Setup Original Sailboat");
                gen.ActiveLevelData = levelData;
                gen.transform.position = new Vector3(0f, 4.33f, 0.26f);
                gen.transform.localScale = new Vector3(1.18f, 1.18f, 1f);
                gen.GeneratePixelArt();
                EditorUtility.SetDirty(gen);
                EditorUtility.SetDirty(gen.gameObject);
                Debug.Log("<color=#00FFAA><b>[StraightPier]</b></color> Orijinal 73 küplük yelkenli gemi oluşturuldu!");
            }

            // 4. Kuyruktaki 3 Gemiyi referans görseldeki gibi kur: Beyaz(30), Kahve(7), Kırmızı(11)
            ShipQueuePool queuePool = Object.FindFirstObjectByType<ShipQueuePool>();
            if (queuePool != null)
            {
                Undo.RecordObject(queuePool, "Setup 3 Reference Ships");
                queuePool.transform.position = new Vector3(0f, -6.95f, 0f);
                queuePool.transform.rotation = Quaternion.Euler(-28f, 0f, 0f);
                queuePool.transform.localScale = Vector3.one * 1.35f;

                var qSo = new SerializedObject(queuePool);
                qSo.FindProperty("m_Columns").intValue = 3;
                qSo.FindProperty("m_Rows").intValue = 1;
                qSo.FindProperty("m_SpacingX").floatValue = 1.62f;
                qSo.FindProperty("m_SpacingY").floatValue = 1.25f;
                qSo.FindProperty("m_ShipScale").floatValue = 0.23f;
                qSo.ApplyModifiedPropertiesWithoutUndo();

                queuePool.RebuildSpots(3, 1);
                queuePool.ClearQueue();

                Color whiteColor = new Color(0.96f, 0.94f, 0.89f, 1f);
                Color brownColor = new Color(0.47f, 0.29f, 0.16f, 1f);
                Color redColor = new Color(0.87f, 0.23f, 0.20f, 1f);

                queuePool.SpawnShipAtSpot(0, whiteColor, 30);
                queuePool.SpawnShipAtSpot(1, brownColor, 7);
                queuePool.SpawnShipAtSpot(2, redColor, 11);

                EditorUtility.SetDirty(queuePool);
                EditorUtility.SetDirty(queuePool.gameObject);
                Debug.Log("<color=#00FFAA><b>[StraightPier]</b></color> 3 bekleme gemisi (Beyaz 30, Kahve 7, Kırmızı 11) oluşturuldu!");
            }

            // 5. Su kontrolcüsü alanını güncelle
            HypercasualWaterController water = Object.FindFirstObjectByType<HypercasualWaterController>();
            if (water != null)
            {
                Undo.RecordObject(water, "Update Water Limits");
                Material mat = water.EnsureMaterial();
                if (mat != null)
                {
                    mat.SetFloat("_WaterMinV", 0.00f);
                    mat.SetFloat("_WaterMaxV", 0.31f);
                    EditorUtility.SetDirty(mat);
                }
                EditorUtility.SetDirty(water);
            }

            // 6. Sahneyi kaydet
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            // 7. Ekran görüntüsü al
            CaptureGameViewScreenshot.CaptureGemiScene();
            Debug.Log("<color=#00FFAA><b>[StraightPier]</b></color> Ekran görüntüsü alındı!");
        }
    }
}
