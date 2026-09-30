using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class SetupGemiReferenceLayout
    {
        private const string ScenePath = "Assets/Scenes/Gemi.unity";
        private const string BgImagePath = "Assets/Kenney/BeachBackground_Clean.png";
        private const string ScreenshotPath = "scratch/gemi_gameplay_view.png";

        static SetupGemiReferenceLayout()
        {
            EditorApplication.delayCall += AutoRunOnce;
        }

        private static void AutoRunOnce()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string key = "ReferenceLayout_Applied_20261001_v13";
            if (SessionState.GetBool(key, false)) return;
            SessionState.SetBool(key, true);

            ApplyReferenceLayout();
        }

        [MenuItem("PixelGame/🏝️ Apply Reference Layout (Pixel Art, 5 Buoy Slots, Deck Ships)")]
        public static void ApplyReferenceLayout()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath);
            }

            Undo.SetCurrentGroupName("Apply Reference Layout to Gemi Scene");
            int undoGroup = Undo.GetCurrentGroup();

            Debug.Log("<color=#00FFAA><b>[ReferenceLayout]</b></color> Referans görsel düzeni uygulanıyor...");

            // 1. Kamera Kontrolü (Ortografik, 8.0 size, 9:16 portrait)
            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 0f, -12f);
                cam.transform.rotation = Quaternion.identity;
                cam.orthographic = true;
                cam.orthographicSize = 8.0f;
            }

            // 2. Arka Plan Canvas & Görsel
            Texture2D bgTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BgImagePath);
            GameObject bgCanvas = GameObject.Find("Background_Canvas");
            if (bgCanvas != null)
            {
                RawImage rawImg = bgCanvas.GetComponentInChildren<RawImage>(true);
                if (rawImg != null && bgTex != null)
                {
                    rawImg.texture = bgTex;
                    rawImg.color = Color.white;
                }

                // Eski çerçeveyi (OtCerceve / BoardFrame) gizle (yeni görselin kendi sahil kavisleri var)
                Transform frameTr = bgCanvas.transform.Find("OtCerceve");
                if (frameTr == null) frameTr = bgCanvas.transform.Find("BoardFrame");
                if (frameTr != null)
                {
                    frameTr.gameObject.SetActive(false);
                }

                // HypercasualWaterController ayarlarını yeni çift sahilli lagüne göre güncelle
                if (rawImg != null)
                {
                    HypercasualWaterController waterCtrl = rawImg.GetComponent<HypercasualWaterController>();
                    if (waterCtrl != null)
                    {
                        Material waterMat = waterCtrl.EnsureMaterial();
                        if (waterMat != null)
                        {
                            waterMat.SetFloat("_WaterMinV", 0.30f);
                            waterMat.SetFloat("_WaterMaxV", 0.65f);
                            waterMat.SetFloat("_WaterDarkness", 0.15f);
                            waterMat.SetFloat("_WaterBrightness", 1.0f);
                            waterMat.SetFloat("_WaterBlueDominance", 0.08f);
                        }
                    }
                }
            }

            // 3. Gameplay Kökü
            GameObject gameplayRoot = GameObject.Find("[GAMEPLAY_MODELS]");
            if (gameplayRoot == null)
            {
                gameplayRoot = new GameObject("[GAMEPLAY_MODELS]");
                Undo.RegisterCreatedObjectUndo(gameplayRoot, "Create Gameplay Models Root");
            }
            gameplayRoot.transform.position = Vector3.zero;

            // 4. Eski ahşap iskeleyi (3D_Pier_Dock / [Zone_Wooden_Bridge]) gizle (orta su kanalı temiz kalsın)
            Transform bridgeZone = gameplayRoot.transform.Find("[Zone_Wooden_Bridge]");
            if (bridgeZone != null)
            {
                bridgeZone.gameObject.SetActive(false);
            }

            // 5. Piksel Sanatı Bölgesi (Üst Kum Yarımadası / Adası)
            // Referans görselde üst kumsalın tam dairesel merkezinde (World Y ~ 5.15f)
            Transform sandZone = gameplayRoot.transform.Find("[Zone_Sand_PlayArea]");
            if (sandZone == null)
            {
                GameObject szGo = new GameObject("[Zone_Sand_PlayArea]");
                Undo.RegisterCreatedObjectUndo(szGo, "Create Sand Play Area Zone");
                sandZone = szGo.transform;
                sandZone.SetParent(gameplayRoot.transform, false);
            }
            sandZone.position = new Vector3(0f, 5.35f, 0f);
            sandZone.rotation = Quaternion.identity;
            sandZone.localScale = Vector3.one;

            // Piksel Generator
            PixelArtGenerator generator = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (generator != null)
            {
                generator.transform.SetParent(sandZone, true);
                generator.transform.localPosition = new Vector3(0f, 0f, 0f);
                generator.transform.localRotation = Quaternion.identity;
                generator.transform.localScale = new Vector3(1.18f, 1.18f, 1f);

                // Küpleri sahneye oturt
                if (generator.CubesContainer == null || generator.CubesContainer.childCount == 0)
                {
                    generator.GeneratePixelArt();
                }
            }

            // 6. Su Alanı ve 5 Adet Can Simidi Slotu (Orta Turkuaz Deniz)
            // Referans görselde suyun tam ortasında yatay düz sırada 5 can simidi (World Y ~ 0.20f)
            Transform waterZone = gameplayRoot.transform.Find("[Zone_Water_LowerArea]");
            if (waterZone == null)
            {
                GameObject wzGo = new GameObject("[Zone_Water_LowerArea]");
                Undo.RegisterCreatedObjectUndo(wzGo, "Create Water Lower Area Zone");
                waterZone = wzGo.transform;
                waterZone.SetParent(gameplayRoot.transform, false);
            }
            waterZone.position = new Vector3(0f, 0.20f, 0f);
            waterZone.rotation = Quaternion.identity;
            waterZone.localScale = Vector3.one;

            Transform slotsGroup = waterZone.Find("[WaterSlotsRow]");
            if (slotsGroup == null)
            {
                GameObject sGo = new GameObject("[WaterSlotsRow]");
                Undo.RegisterCreatedObjectUndo(sGo, "Create WaterSlotsRow");
                slotsGroup = sGo.transform;
                slotsGroup.SetParent(waterZone, false);
            }
            slotsGroup.localPosition = Vector3.zero;
            slotsGroup.localRotation = Quaternion.identity;
            slotsGroup.localScale = Vector3.one;

            MarinaSlotLayout slotLayout = slotsGroup.GetComponent<MarinaSlotLayout>();
            if (slotLayout == null) slotLayout = slotsGroup.gameObject.AddComponent<MarinaSlotLayout>();

            slotLayout.SlotCount = 5;
            slotLayout.SlotSpacing = 1.54f;
            slotLayout.SlotWidth = 1.38f;
            slotLayout.SlotLength = 1.593f; // Kamera eğiminde ekranda tam dairesel görünüm (1.38 / sin(60°))
            slotLayout.SlotAngle = 0f;
            slotLayout.WaterTiltX = -60f;
            slotLayout.OffsetY = 0f;
            slotLayout.OffsetZ = 0.05f;
            slotLayout.ArcCurveY = 0f;
            slotLayout.ArcAsymmetry = 0f;
            slotLayout.ArcAngleFan = 0f;

            // SetupMarinaDockSlots ile can simidi görsellerini kur
            SetupMarinaDockSlots.SetupMarinaSlots();
            slotLayout.ApplyLayout();

            // 6.1 Ahşap Levha Üzerindeki Yazı (Arka plan görselinde orijinal pikselleriyle gömülü)
            GameObject deckLabelObj = GameObject.Find("Deck_Banner_Label");
            if (deckLabelObj != null)
            {
                Undo.DestroyObjectImmediate(deckLabelObj);
            }

            // 7. Bekleyen Gemi Kuyruğu (Alt Kumsal / Ahşap Deck Tepsisi)
            // Referans görselde alt kumsaldaki ahşap tepsi alanı (World Y ~ -4.95f)
            Transform queueObj = waterZone.Find("[ShipQueuePool]");
            if (queueObj == null)
            {
                queueObj = gameplayRoot.transform.Find("[ShipQueuePool]");
            }
            if (queueObj == null)
            {
                GameObject qGo = new GameObject("[ShipQueuePool]");
                Undo.RegisterCreatedObjectUndo(qGo, "Create ShipQueuePool");
                queueObj = qGo.transform;
            }
            queueObj.SetParent(gameplayRoot.transform, false);
            queueObj.position = new Vector3(0f, -4.95f, 0f);
            queueObj.rotation = Quaternion.Euler(-60f, 0f, 0f);
            queueObj.localScale = Vector3.one * 1.35f;

            ShipQueuePool queuePool = queueObj.GetComponent<ShipQueuePool>();
            if (queuePool == null) queuePool = queueObj.gameObject.AddComponent<ShipQueuePool>();

            var qSo = new SerializedObject(queuePool);
            qSo.FindProperty("m_Columns").intValue = 4;
            qSo.FindProperty("m_Rows").intValue = 2;
            qSo.FindProperty("m_Spacing").vector2Value = new Vector2(1.64f, 0.90f);
            qSo.FindProperty("m_ShipScale").floatValue = 0.26f;
            qSo.ApplyModifiedPropertiesWithoutUndo();

            queuePool.RebuildSpots(4, 2);

            GameObject shipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kenney/kenney_watercraft-pack/Models/FBX format/ship-cargo-a.fbx");
            if (shipPrefab != null && !Application.isPlaying)
            {
                queuePool.ClearQueue();
                for (int s = 0; s < queuePool.Capacity; s++)
                {
                    queuePool.SpawnShipAtSpot(s);
                }
            }

            // 8. ShipDispatcher Referanslarını Güncelle
            ShipDispatcher dispatcher = gameplayRoot.GetComponent<ShipDispatcher>();
            if (dispatcher != null)
            {
                dispatcher.EnsureReferences();
            }

            // 9. Üst HUD Şeridini Kur (Referans tarzı tek sıra)
            SetupGemiTopHUD.BuildTopHUD();

            // 9. Sahneyi Kaydet ve Ekran Görüntüsü Al
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);

            GemiSceneSetup.CaptureScreenshot();

            Debug.Log("<color=#00FFAA><b>[ReferenceLayout]</b></color> Referans düzeni tamamlandı! Ekran görüntüsü alındı: " + ScreenshotPath);
        }
    }
}
