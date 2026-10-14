using System.IO;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class CaptureGameViewScreenshot
    {
        private const string OutputPath = "scratch/gameplay_view_9_16.png";

        private static bool s_Captured = false;

        private const string TriggerKey = "PixelGame_Capture_Restore_Check_v3";

        static CaptureGameViewScreenshot()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!SessionState.GetBool(TriggerKey, false))
                {
                    SessionState.SetBool(TriggerKey, true);
                    CaptureGemiScene();
                }
            };
        }

        private static bool s_IsCapturing = false;

        [MenuItem("PixelGame/📸 9:16 Gemi Ekran Görüntüsü")]
        public static void CaptureGemiScene()
        {
            if (s_IsCapturing) return;
            try
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/Gemi.unity")
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Gemi.unity");
                    }
                }

                AssetDatabase.ImportAsset("Assets/PixelArt/Optimized/Yelkenli_canvas.png", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/Levels/Level_30_Yelkenli.asset", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/Resources/mystery_cube_question.png", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/Textures/mystery_cube_question.png", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/Kenney/BeachBackground_3Slots.png", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/Kenney/BeachBackground_4Slots.png", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/Kenney/BeachBackground_Clean.png", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/UI/CasualUI/btn_marine_lifebuoy.png", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/UI/CasualUI/btn_marine_haptic_on.png", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/UI/CasualUI/btn_marine_speed_1x.png", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/UI/CasualUI/btn_marine_speed_2x.png", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/UI/CasualUI/bg_level_marine.png", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/Textures/Marina/slot_berth_inner.png", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/Textures/Marina/slot_berth_docked.png", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/Textures/Marina/slot_berth_closed.png", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/Materials/Marina/Slot_Berth_Docked_Mat.mat", ImportAssetOptions.ForceUpdate);
                PixelCube.ClearMysteryMaterialCache();

                PixelArtGenerator gen = Object.FindFirstObjectByType<PixelArtGenerator>();
                if (gen != null)
                {
                    // Boşluk oranını eskisi gibi (level ayarı neyse) koru — asla değiştirme
                    if (gen.ActiveLevelData != null)
                    {
                        gen.CubeSpacingX = gen.ActiveLevelData.CubeSpacingX;
                        gen.CubeSpacing = gen.ActiveLevelData.CubeSpacing;
                        gen.CubeDepth = gen.ActiveLevelData.CubeDepth;
                    }
                    gen.GeneratePixelArt();
                    gen.UpdateExistingCubesLive();
                    gen.UpdateExistingCubesTransforms();
                }

                    ShipController.ClearMaterialCache();

                    ShipQueuePool shipPool = Object.FindFirstObjectByType<ShipQueuePool>();
                    if (shipPool != null)
                    {
                        shipPool.transform.position = new Vector3(0f, -6.00f, 0f);
                        shipPool.transform.rotation = Quaternion.Euler(-42f, 0f, 0f);
                        shipPool.transform.localScale = Vector3.one * 1.35f;

                        var qSo = new SerializedObject(shipPool);
                        qSo.FindProperty("m_Columns").intValue = 3;
                        qSo.FindProperty("m_Rows").intValue = 1;
                        qSo.FindProperty("m_SpacingX").floatValue = 1.48f;
                        qSo.FindProperty("m_SpacingY").floatValue = 2.30f;
                        qSo.FindProperty("m_ShipScale").floatValue = 0.38f;
                        qSo.FindProperty("m_OffsetY").floatValue = -6.00f;

                        var presetsProp = qSo.FindProperty("m_ColumnPresets");
                        if (presetsProp != null)
                        {
                            for (int p = 0; p < presetsProp.arraySize; p++)
                            {
                                var elem = presetsProp.GetArrayElementAtIndex(p);
                                if (elem.FindPropertyRelative("columns").intValue == 3)
                                {
                                    elem.FindPropertyRelative("offsetY").floatValue = -6.00f;
                                    elem.FindPropertyRelative("shipScale").floatValue = 0.38f;
                                    elem.FindPropertyRelative("spacingX").floatValue = 1.48f;
                                    elem.FindPropertyRelative("spacingY").floatValue = 2.30f;
                                }
                            }
                        }
                        qSo.ApplyModifiedPropertiesWithoutUndo();

                        shipPool.RebuildSpots(3, 1);
                        shipPool.transform.position = new Vector3(0f, -6.00f, 0f);
                        shipPool.ClearQueue();

                        Color whiteColor = new Color(0.96f, 0.94f, 0.89f, 1f);
                        Color brownColor = new Color(0.47f, 0.29f, 0.16f, 1f);
                        Color redColor = new Color(0.87f, 0.23f, 0.20f, 1f);

                        shipPool.SpawnShipAtSpot(0, whiteColor, 30);
                        shipPool.SpawnShipAtSpot(1, brownColor, 7);
                        shipPool.SpawnShipAtSpot(2, redColor, 11);
                    }

                    MarinaSlotLayout marina = Object.FindFirstObjectByType<MarinaSlotLayout>();
                    if (marina != null)
                    {
                        marina.SyncWithLevel();
                    }

                    ShipController[] ships = Object.FindObjectsByType<ShipController>(FindObjectsSortMode.None);
                    foreach (var ship in ships)
                    {
                        if (ship != null)
                        {
                            ship.ApplyColorToShip(ship.ShipColor);
                            ship.EnsureFakeShadow();
                            ship.EnsureSilhouetteShadow();
                        }
                    }

                    LinkedShipTether[] tethers = Object.FindObjectsByType<LinkedShipTether>(FindObjectsSortMode.None);
                    foreach (var tether in tethers)
                    {
                        if (tether != null)
                        {
                            tether.EnsureLineRenderer();
                            tether.ApplyColorsFromShips();
                            tether.UpdateTetherPositions();
                        }
                    }

                    GameObject coinPill = GameObject.Find("CoinPill");
                    if (coinPill != null) coinPill.SetActive(false);

                    CasualHudController hud = Object.FindFirstObjectByType<CasualHudController>();
                    if (hud != null) hud.HideCoinPill();

                    SetupGemiTopHUD.BuildTopHUD();

                    var allActiveSlots = marina != null ? marina.GetComponentsInChildren<ShipSlot>() : null;
                    if (allActiveSlots != null)
                    {
                        for (int sIdx = 0; sIdx < allActiveSlots.Length; sIdx++)
                        {
                            allActiveSlots[sIdx].ReleaseShip();
                        }
                    }

                Capture();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[CaptureGemiScene] " + ex.Message);
            }
        }

        [MenuItem("PixelGame/📸 9:16 Ekran Görüntüsü Al (Capture Screenshot)")]
        public static void Capture()
        {
            if (s_IsCapturing) return;
            s_IsCapturing = true;

            try
            {
                Camera cam = Camera.main;
                if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
                if (cam == null)
                {
                    Debug.LogError("[CaptureGameViewScreenshot] Sahne kamerası bulunamadı!");
                    return;
                }

                int width = 1080;
                int height = 1920;

                RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                RenderTexture prevRT = cam.targetTexture;
                RenderTexture prevActive = RenderTexture.active;

                try
                {
                    cam.targetTexture = rt;
                    cam.Render();

                    RenderTexture.active = rt;
                    Texture2D screenTex = new Texture2D(width, height, TextureFormat.RGB24, false);
                    screenTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    screenTex.Apply();

                    byte[] bytes = screenTex.EncodeToPNG();
                    string dir = Path.GetDirectoryName(OutputPath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllBytes(OutputPath, bytes);
                    Object.DestroyImmediate(screenTex);

                    Debug.Log($"<color=#00FFAA><b>[Screenshot]</b></color> 9:16 Ekran görüntüsü başarıyla kaydedildi: {OutputPath}");
                }
                finally
                {
                    cam.targetTexture = prevRT;
                    RenderTexture.active = prevActive;
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }
            }
            finally
            {
                s_IsCapturing = false;
            }
        }
    }
}

