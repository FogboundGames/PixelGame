using System.IO;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class CaptureGameViewScreenshot
    {
        private const string OutputPath = "scratch/gameplay_view_9_16.png";

        static CaptureGameViewScreenshot()
        {
            EditorApplication.playModeStateChanged += (s) =>
            {
                if (s == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += CaptureGemiScene;
            };
            EditorApplication.delayCall += CaptureGemiScene;
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

                MarinaSlotLayout marina = Object.FindFirstObjectByType<MarinaSlotLayout>();
                if (marina != null)
                {
                    marina.ApplyLayout();
                }

                AssetDatabase.ImportAsset("Assets/Resources/mystery_cube_question.png", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/Textures/mystery_cube_question.png", ImportAssetOptions.ForceUpdate);
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
                        gen.UpdateExistingCubesLive();
                        gen.UpdateExistingCubesTransforms();
                    }

                    ShipController.ClearMaterialCache();

                    ShipQueuePool shipPool = Object.FindFirstObjectByType<ShipQueuePool>();
                    if (shipPool != null)
                    {
                        PixelLevelData curLevel = gen != null ? gen.ActiveLevelData : null;
                        if (curLevel != null)
                        {
                            shipPool.RebuildSpots(curLevel.PoolColumns, curLevel.PoolRows);
                        }
                        shipPool.InitializeQueue();
                    }

                    ShipController[] ships = Object.FindObjectsByType<ShipController>(FindObjectsSortMode.None);
                    foreach (var ship in ships)
                    {
                        if (ship != null)
                        {
                            ship.ApplyColorToShip(ship.ShipColor);
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

                    SetupGemiTopHUD.EnsureRetryButtonInScene();

                    var allActiveSlots = marina != null ? marina.GetComponentsInChildren<ShipSlot>() : null;
                    if (allActiveSlots != null && allActiveSlots.Length >= 2 && ships != null && ships.Length >= 2)
                    {
                        Vector3 targetLocalPos = new Vector3(0f, 0.08f, 0.02f);
                        ships[0].transform.position = allActiveSlots[0].transform.TransformPoint(targetLocalPos);
                        ships[0].transform.rotation = allActiveSlots[0].transform.rotation;
                        ships[1].transform.position = allActiveSlots[1].transform.TransformPoint(targetLocalPos);
                        ships[1].transform.rotation = allActiveSlots[1].transform.rotation;
                    }

                Capture();

                // Çekimden sonra sırayı tekrar temizle
                ShipQueuePool pool = Object.FindFirstObjectByType<ShipQueuePool>();
                if (pool != null) pool.InitializeQueue();
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

