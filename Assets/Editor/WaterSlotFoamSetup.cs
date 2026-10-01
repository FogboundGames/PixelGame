using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelGame.Editor
{
        public static class WaterSlotFoamSetup
    {
        private const string FLAG_FILE = "Temp/waterslot_foam_setup_done.tmp";

// Manuel Menüden Çağrılabilir

        [MenuItem("PixelGame/🌊 WaterSlot 1 Foam Kur ve Ekran Görüntüsü Al")]
        public static void SetupWaterSlot1AndCapture()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying || Application.isPlaying) return;
            try
            {
                // 1. Sahneyi kontrol et
                Scene activeScene = EditorSceneManager.GetActiveScene();
                if (!activeScene.path.Contains("Gemi.unity"))
                {
                    EditorSceneManager.OpenScene("Assets/Scenes/Gemi.unity");
                }

                // 2. WaterSlotsRow ve WaterSlot_1'i bul
                GameObject slotsRow = GameObject.Find("[WaterSlotsRow]");
                if (slotsRow == null)
                {
                    Debug.LogError("[WaterSlotFoamSetup] [WaterSlotsRow] sahnede bulunamadı!");
                    return;
                }

                Transform slot1Tr = slotsRow.transform.Find("WaterSlot_1");
                if (slot1Tr == null)
                {
                    Debug.LogError("[WaterSlotFoamSetup] WaterSlot_1 bulunamadı!");
                    return;
                }

                // 3. Eski IndicatorMesh ve [Slot_Lifebuoy] görselini deaktive et
                for (int i = 0; i < slot1Tr.childCount; i++)
                {
                    Transform child = slot1Tr.GetChild(i);
                    if (child.name.Contains("Indicator") || child.name.Contains("Lifebuoy") || child.name.Contains("can_simidi"))
                    {
                        child.gameObject.SetActive(false);
                        Debug.Log($"[WaterSlotFoamSetup] Eski görsel deaktive edildi: {child.name}");
                    }
                }

                // 4. FoamSlot GameObject'ini oluştur veya güncelle
                Transform foamTr = slot1Tr.Find("FoamSlot");
                GameObject foamGo;
                if (foamTr == null)
                {
                    foamGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    foamGo.name = "FoamSlot";
                    // Collider gereksiz, kaldır
                    Collider col = foamGo.GetComponent<Collider>();
                    if (col != null) Object.DestroyImmediate(col);

                    foamGo.transform.SetParent(slot1Tr, false);
                    foamTr = foamGo.transform;
                }
                else
                {
                    foamGo = foamTr.gameObject;
                    foamGo.SetActive(true);
                }

                foamTr.localPosition = new Vector3(0f, 0.02f, 0f);
                foamTr.localRotation = Quaternion.Euler(90f, 0f, 0f);
                foamTr.localScale = Vector3.one;

                // 5. Materyali ata
                Material foamMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_FoamSlot.mat");
                if (foamMat != null)
                {
                    MeshRenderer mr = foamGo.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        mr.sharedMaterial = foamMat;
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        mr.receiveShadows = false;
                    }
                }
                else
                {
                    Debug.LogWarning("[WaterSlotFoamSetup] M_FoamSlot.mat yüklenemedi!");
                }

                // 6. ShipSlot komponentini FoamSlot'u gösterecek şekilde güncelle
                ShipSlot shipSlot = slot1Tr.GetComponent<ShipSlot>();
                if (shipSlot != null)
                {
                    SerializedObject so = new SerializedObject(shipSlot);
                    SerializedProperty prop = so.FindProperty("m_IndicatorTransform");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = foamTr;
                        so.ApplyModifiedProperties();
                    }
                }

                // 7. MarinaSlotLayout'u yeniden hizala
                MarinaSlotLayout layout = slotsRow.GetComponent<MarinaSlotLayout>();
                if (layout != null)
                {
                    layout.ApplyLayout();
                }

                // 8. Sahneyi kaydet
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                Debug.Log("[WaterSlotFoamSetup] WaterSlot_1 FoamSlot kurulumu tamamlandı ve sahne kaydedildi.");

                // 9. Ekran Görüntüsü Al
                CaptureScreenshot();

                // Flag oluştur
                Directory.CreateDirectory("Temp");
                File.WriteAllText(FLAG_FILE, "done");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[WaterSlotFoamSetup] Hata: {ex}");
            }
        }

        [MenuItem("PixelGame/📸 Sahne Ekran Görüntüsü Al")]
        public static void CaptureScreenshot()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                cam = Object.FindFirstObjectByType<Camera>();
            }

            if (cam == null)
            {
                Debug.LogError("[WaterSlotFoamSetup] Kamera bulunamadı!");
                return;
            }

            int width = 1080;
            int height = 1920;
            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture prevRt = cam.targetTexture;

            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            cam.targetTexture = prevRt;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);

            byte[] bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            Directory.CreateDirectory("Assets/Screenshots");
            string savePath = "Assets/Screenshots/WaterSlot_Foam_Preview.png";
            File.WriteAllBytes(savePath, bytes);
            AssetDatabase.Refresh();

            // Artifacts dizinine kopyala
            string artifactDir = "/home/emre/.gemini/antigravity-ide/brain/1b534183-e146-4d34-9a9e-3af508ddd5cd";
            if (Directory.Exists(artifactDir))
            {
                File.WriteAllBytes(Path.Combine(artifactDir, "WaterSlot_Foam_Preview.png"), bytes);
            }

            Debug.Log($"[WaterSlotFoamSetup] Ekran görüntüsü kaydedildi: {savePath}");
        }

        [MenuItem("PixelGame/🌊 FoamSlot'u 5 Slota Yay (Spread To All 5 Slots)")]
        public static void SpreadFoamToAll5Slots()
        {
            GameObject slotsRow = GameObject.Find("[WaterSlotsRow]");
            if (slotsRow == null) return;

            Material foamMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_FoamSlot.mat");

            for (int s = 1; s <= 5; s++)
            {
                Transform slotTr = slotsRow.transform.Find($"WaterSlot_{s}");
                if (slotTr == null) continue;

                // Eski görselleri kapat
                for (int i = 0; i < slotTr.childCount; i++)
                {
                    Transform child = slotTr.GetChild(i);
                    if (child.name.Contains("Indicator") || child.name.Contains("Lifebuoy") || child.name.Contains("can_simidi"))
                    {
                        child.gameObject.SetActive(false);
                    }
                }

                // FoamSlot oluştur / güncelle
                Transform foamTr = slotTr.Find("FoamSlot");
                GameObject foamGo;
                if (foamTr == null)
                {
                    foamGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    foamGo.name = "FoamSlot";
                    Collider col = foamGo.GetComponent<Collider>();
                    if (col != null) Object.DestroyImmediate(col);

                    foamGo.transform.SetParent(slotTr, false);
                    foamTr = foamGo.transform;
                }
                else
                {
                    foamGo = foamTr.gameObject;
                    foamGo.SetActive(true);
                }

                foamTr.localPosition = new Vector3(0f, 0.02f, 0f);
                foamTr.localRotation = Quaternion.Euler(90f, 0f, 0f);
                foamTr.localScale = Vector3.one;

                if (foamMat != null)
                {
                    MeshRenderer mr = foamGo.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        mr.sharedMaterial = foamMat;
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        mr.receiveShadows = false;
                    }
                }

                ShipSlot shipSlot = slotTr.GetComponent<ShipSlot>();
                if (shipSlot != null)
                {
                    SerializedObject so = new SerializedObject(shipSlot);
                    SerializedProperty prop = so.FindProperty("m_IndicatorTransform");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = foamTr;
                        so.ApplyModifiedProperties();
                    }
                }
            }

            MarinaSlotLayout layout = slotsRow.GetComponent<MarinaSlotLayout>();
            if (layout != null)
            {
                layout.ApplyLayout();
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            CaptureScreenshot();
            Debug.Log("[WaterSlotFoamSetup] 5 slota başarıyla yayıldı!");
        }
    }
}
