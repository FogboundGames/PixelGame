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

                    MarinaSlotLayout marina = Object.FindFirstObjectByType<MarinaSlotLayout>();
                    if (marina != null)
                    {
                        marina.ApplyLayout();
                    }

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

