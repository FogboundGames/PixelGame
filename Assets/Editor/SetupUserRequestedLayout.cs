using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class SetupUserRequestedLayout
    {
        private const string ScenePath = "Assets/Scenes/Gemi.unity";

        static SetupUserRequestedLayout()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
                string key = "SetupUserRequestedLayout_Applied_v1";
                if (!SessionState.GetBool(key, false))
                {
                    SessionState.SetBool(key, true);
                    ApplyLayoutAndFixSlots();
                }
            };
        }

        [MenuItem("PixelGame/⚓ Fix Pier Dock Slots & Align Frame (User Request)")]
        public static void ApplyLayoutAndFixSlots()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath);
            }

            Debug.Log("<color=#00FFAA><b>[UserRequestedLayout]</b></color> İskele bay slotları, OtCerceve ve gemi kuyruğu ayarlanıyor...");

            // 1. Gameplay Kökü & Alt Zone'ları sıfırla (öteleme hatası giderilir)
            GameObject gameplayRoot = GameObject.Find("[GAMEPLAY_MODELS]");
            if (gameplayRoot != null)
            {
                gameplayRoot.transform.localPosition = Vector3.zero;
                gameplayRoot.transform.localRotation = Quaternion.identity;
                gameplayRoot.transform.localScale = Vector3.one;

                Transform waterZone = gameplayRoot.transform.Find("[Zone_Water_LowerArea]");
                if (waterZone != null)
                {
                    waterZone.localPosition = Vector3.zero;
                    waterZone.localRotation = Quaternion.identity;
                    waterZone.localScale = Vector3.one;
                }

                Transform sandZone = gameplayRoot.transform.Find("[Zone_Sand_PlayArea]");
                if (sandZone != null)
                {
                    sandZone.localPosition = Vector3.zero;
                    sandZone.localRotation = Quaternion.identity;
                    sandZone.localScale = Vector3.one;
                }
            }

            // 2. OtCerceve & OtCerceve_Shadow (Kumsal çerçevesi)
            GameObject bgCanvas = GameObject.Find("Background_Canvas");
            if (bgCanvas != null)
            {
                Transform frameTr = bgCanvas.transform.Find("OtCerceve");
                if (frameTr == null) frameTr = bgCanvas.transform.Find("BoardFrame");
                if (frameTr != null)
                {
                    frameTr.gameObject.SetActive(true);
                    RectTransform rt = frameTr.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchoredPosition = new Vector2(0f, 360f);
                        rt.sizeDelta = new Vector2(800f, 860f);
                        rt.localScale = Vector3.one;
                    }
                }

                Transform shadowTr = bgCanvas.transform.Find("OtCerceve_Shadow");
                if (shadowTr != null)
                {
                    shadowTr.gameObject.SetActive(true);
                    RectTransform srt = shadowTr.GetComponent<RectTransform>();
                    if (srt != null)
                    {
                        srt.anchoredPosition = new Vector2(0f, 345f);
                        srt.sizeDelta = new Vector2(810f, 870f);
                        srt.localScale = Vector3.one;
                    }
                }
            }

            // 3. Piksel Sanatı Jeneratörü (Yelkenli çerçeve içine tam otursun)
            PixelArtGenerator generator = Object.FindFirstObjectByType<PixelArtGenerator>();
            if (generator != null)
            {
                generator.UseFixedSandArea = true;
                generator.FixedSandCenter = new Vector3(0f, 3.30f, 0.26f);
                generator.FixedSandWidth = 4.60f;
                generator.FixedSandHeight = 4.80f;
                generator.InnerPadding = 0.08f;
                generator.TargetFrameRect = null;
                generator.GeneratePixelArt();
                EditorUtility.SetDirty(generator);
            }

            // 4. MarinaSlotLayout: 3 slot doğrudan ahşap iskele koylarına (Y = -5.20f; su bölgesi 0 olduğu için dünyada son committeki -5.40 + 0.20 ile aynı) yanaşır
            MarinaSlotLayout marina = Object.FindFirstObjectByType<MarinaSlotLayout>();
            if (marina != null)
            {
                marina.UsePerCountSettings = true;
                marina.ResetToCalibratedDefaults(3);

                var cfg3 = marina.Config3Slots;
                if (cfg3 != null)
                {
                    cfg3.RowOffsetY = -5.20f;
                    cfg3.LegacySpacing = 1.454f;
                    cfg3.SlotWidth = 1.05f;
                    cfg3.SlotLength = 1.55f;
                    cfg3.BaySpacingPx = 174f;
                }
                marina.OffsetY = -5.20f;
                marina.SlotSpacing = 1.454f;
                marina.SetSlotCount(3);
                marina.ApplyLayout();
                EditorUtility.SetDirty(marina);
            }

            // 5. Gemi Bekleme Kuyruğu (ShipQueuePool): Tek yatay sıra en altta (Y = -6.80f)
            ShipQueuePool queuePool = Object.FindFirstObjectByType<ShipQueuePool>();
            if (queuePool != null)
            {
                var qSo = new SerializedObject(queuePool);
                var colsProp = qSo.FindProperty("m_Columns");
                var rowsProp = qSo.FindProperty("m_Rows");
                var offYProp = qSo.FindProperty("m_OffsetY");
                var spXProp = qSo.FindProperty("m_SpacingX");
                var scaleProp = qSo.FindProperty("m_ShipScale");

                if (colsProp != null) colsProp.intValue = 3;
                if (rowsProp != null) rowsProp.intValue = 1;
                if (offYProp != null) offYProp.floatValue = -6.80f;
                if (spXProp != null) spXProp.floatValue = 1.48f;
                if (scaleProp != null) scaleProp.floatValue = 0.38f;

                qSo.ApplyModifiedProperties();

                queuePool.RebuildSpots(3, 1);
                queuePool.InitializeQueue(true);
                EditorUtility.SetDirty(queuePool);
            }

            // 6. ShipDispatcher: Kıyı çizgisi iskele tahtası seviyesine (Y = -3.25f) güncellensin
            ShipDispatcher dispatcher = Object.FindFirstObjectByType<ShipDispatcher>();
            if (dispatcher != null)
            {
                var dSo = new SerializedObject(dispatcher);
                var shoreYProp = dSo.FindProperty("m_ShoreY");
                if (shoreYProp != null) shoreYProp.floatValue = -3.25f;

                var shorelineProp = dSo.FindProperty("m_Shoreline");
                if (shorelineProp != null)
                {
                    shorelineProp.ClearArray();
                    shorelineProp.InsertArrayElementAtIndex(0);
                    shorelineProp.GetArrayElementAtIndex(0).vector2Value = new Vector2(-6.0f, -3.25f);
                    shorelineProp.InsertArrayElementAtIndex(1);
                    shorelineProp.GetArrayElementAtIndex(1).vector2Value = new Vector2( 6.0f, -3.25f);
                }
                dSo.ApplyModifiedProperties();
                dispatcher.EnsureReferences();
                EditorUtility.SetDirty(dispatcher);
            }

            // 7. Sahneyi Kaydet
            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("<color=#00FFAA><b>[UserRequestedLayout]</b></color> Sahne başarıyla kaydedildi!");

            // 8. Ekran Görüntüsü Al
            CaptureGameViewScreenshot.CaptureGemiScene();
        }
    }
}
