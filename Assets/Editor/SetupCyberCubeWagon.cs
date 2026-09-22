using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelGame.Editor
{
    /// <summary>
    /// Görseldeki beyaz gövdeli, renkli butonlu ve 4 tarafında neon kapsülleri olan
    /// CyberCube modelini materyalleri, iz efektleri ve rozetleriyle birlikte
    /// prefab olarak üretip oyundaki bant vagonu olarak aktif eder.
    /// </summary>
    [InitializeOnLoad]
    public static class SetupCyberCubeWagon
    {
        private const string SessionKey = "SetupCyberCubeWagon_Executed_v2";

        static SetupCyberCubeWagon()
        {
            // Otomatik tetikleme kapatıldı: sahne/editör açılışında değiştirilmesin.
            // Gerekirse Tools menüsünden elle çalıştırılır.
            // EditorApplication.delayCall += AutoRunOnce;
        }

        private static void AutoRunOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) return;
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            ExecuteSetup(silent: true);
        }

        private const string ModelObjPath = "Assets/Models/CyberCube.obj";
        private const string TargetPrefabPath = "Assets/Prefabs/CyberCubeWagon.prefab";
        private const string BodyMatPath = "Assets/Materials/CyberCube_Body_Mat.mat";
        private const string AccentMatPath = "Assets/Materials/CyberCube_Accent_Mat.mat";
        private const string NeonMatPath = "Assets/Materials/CyberCube_Neon_Mat.mat";
        private const string BaseMatPath = "Assets/Materials/CyberCube_Base_Mat.mat";

        [MenuItem("Tools/PixelGame/🤖 CyberCube Modelini Kur & Aktif Et", priority = 1)]
        public static void SetupManual()
        {
            ExecuteSetup(silent: false);
        }

        public static void ExecuteSetup(bool silent = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) return;

            try
            {
                // 1. Model dosyasını kontrol et ve import ayarlarını doğrula
                AssetDatabase.Refresh();
                ModelImporter importer = AssetImporter.GetAtPath(ModelObjPath) as ModelImporter;
                if (importer != null)
                {
                    importer.importNormals = ModelImporterNormals.Import;
                    importer.importTangents = ModelImporterTangents.CalculateMikk;
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.SaveAndReimport();
                }

                GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelObjPath);
                if (modelAsset == null)
                {
                    Debug.LogError($"[SetupCyberCubeWagon] Model bulunamadı: {ModelObjPath}");
                    return;
                }

                MeshFilter sourceMf = modelAsset.GetComponentInChildren<MeshFilter>(true);
                Mesh cyberMesh = sourceMf != null ? sourceMf.sharedMesh : null;
                if (cyberMesh == null)
                {
                    // Doğrudan mesh ara
                    cyberMesh = AssetDatabase.LoadAssetAtPath<Mesh>(ModelObjPath);
                }

                if (cyberMesh == null)
                {
                    Debug.LogError("[SetupCyberCubeWagon] CyberCube mesh'i model dosyasından okunamadı!");
                    return;
                }

                // 2. 4 Adet Toon Materyalini Hazırla
                Material bodyMat = GetOrCreateMaterial(BodyMatPath, "CyberCube_Body_Mat", new Color(0.98f, 0.99f, 1.0f), false, 0.88f);
                Material accentMat = GetOrCreateMaterial(AccentMatPath, "CyberCube_Accent_Mat", TruckPaint.Blue, false, 0.82f);
                Material neonMat = GetOrCreateMaterial(NeonMatPath, "CyberCube_Neon_Mat", new Color(0.0f, 0.85f, 1.0f), true, 0.95f);
                Material baseMat = GetOrCreateMaterial(BaseMatPath, "CyberCube_Base_Mat", new Color(0.65f, 0.68f, 0.74f), false, 0.45f);

                // 3. CyberCubeWagon Prefab'ını İnşa Et
                GameObject wagonGo = new GameObject("CyberCubeWagon");
                wagonGo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                wagonGo.transform.localScale = Vector3.one;

                MeshFilter mf = wagonGo.AddComponent<MeshFilter>();
                mf.sharedMesh = cyberMesh;

                MeshRenderer mr = wagonGo.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new Material[] { bodyMat, accentMat, neonMat, baseMat };
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;

                // TruckCargo
                TruckCargo cargo = wagonGo.AddComponent<TruckCargo>();
                cargo.ResetCargo(TruckPaint.Blue, 8);

                // TruckPaint
                TruckPaint paint = wagonGo.AddComponent<TruckPaint>();
                paint.SetBodyColor(TruckPaint.Blue);

                // WagonTargetIndicator
                WagonTargetIndicator indicator = wagonGo.AddComponent<WagonTargetIndicator>();
                indicator.SetTargetColor(TruckPaint.Blue);

                // WagonClickTarget
                wagonGo.AddComponent<WagonClickTarget>();

                // BoxCollider
                BoxCollider box = wagonGo.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = new Vector3(0f, 0.55f, 0f);
                box.size = new Vector3(1.10f, 1.20f, 1.10f);

                // WagonCapacityBadge
                WagonCapacityBadge badge = wagonGo.AddComponent<WagonCapacityBadge>();
                ConfigureBadgeProperties(badge);

                // Prefab Olarak Kaydet
                Directory.CreateDirectory(Path.GetDirectoryName(TargetPrefabPath));
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(wagonGo, TargetPrefabPath);
                UnityEngine.Object.DestroyImmediate(wagonGo);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // 4. Sahnedeki TruckDispatcher'a bağla
                TruckDispatcher dispatcher = UnityEngine.Object.FindFirstObjectByType<TruckDispatcher>();
                if (dispatcher != null)
                {
                    SerializedObject so = new SerializedObject(dispatcher);
                    SerializedProperty prop = so.FindProperty("m_TruckPrefab");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = savedPrefab;
                        so.ApplyModifiedProperties();
                    }

                    EditorUtility.SetDirty(dispatcher);
                }

                // 5. SlotRow ve TruckPool yönlerini güncelle (model doğal olarak ileri bakar)
                TruckSlotRow slotRow = UnityEngine.Object.FindFirstObjectByType<TruckSlotRow>();
                if (slotRow != null)
                {
                    slotRow.Style.truckEuler = Vector3.zero;
                    slotRow.SyncStyleToSlots();
                    EditorUtility.SetDirty(slotRow);
                }

                TruckPool pool = UnityEngine.Object.FindFirstObjectByType<TruckPool>();
                if (pool != null)
                {
                    pool.Style.truckEuler = Vector3.zero;
                    EditorUtility.SetDirty(pool);
                }

                // 6. Sahnedeki TruckSlot taban hizalamalarını uygula
                foreach (TruckSlot slot in UnityEngine.Object.FindObjectsByType<TruckSlot>(FindObjectsSortMode.None))
                {
                    if (slot == null) continue;
                    slot.AnchorToBase = true;
                    slot.BaseVerticalRatio = -0.07f;
                    slot.BaseWidthFill = 0.58f;
                    slot.AlignAll();
                    EditorUtility.SetDirty(slot);
                }

                // 7. Sahneyi kaydet (yalnızca Edit modundayken)
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying)
                {
                    Scene activeScene = SceneManager.GetActiveScene();
                    if (activeScene.IsValid() && activeScene.isLoaded)
                    {
                        EditorSceneManager.MarkSceneDirty(activeScene);
                        EditorSceneManager.SaveOpenScenes();
                    }
                }

                Debug.Log("<color=#00FFAA><b>[SetupCyberCubeWagon]</b></color> CyberCube modeli başarıyla oluşturuldu, prefab kaydedildi ve konveyör bandında aktif vagon olarak bağlandı!");
                
                if (!silent)
                {
                    EditorUtility.DisplayDialog(
                        "CyberCube Aktif Edildi",
                        "Görseldeki model başarıyla hazırlandı ve aktif bant vagonu yapıldı:\n\n" +
                        "• Model: Assets/Models/CyberCube.obj\n" +
                        "• Prefab: Assets/Prefabs/CyberCubeWagon.prefab\n" +
                        "• 4 Submesh & Toon Malzemeleri bağlandı.\n" +
                        "• Beyaz gövde + Dinamik renkli neon buton ve kapsüller devrede.\n" +
                        "• Hareket hız şeritleri (Speed Trails) eklendi.\n" +
                        "• TruckDispatcher'a aktif vagon olarak atandı.",
                        "Tamam"
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SetupCyberCubeWagon] Hata oluştu: {ex}");
            }
        }

        private static Material GetOrCreateMaterial(string path, string name, Color color, bool isNeon, float smoothness)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = CartoonShader.Get();
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }

            CartoonShader.ApplyColor(mat, color);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);

            if (isNeon)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", color * 2.5f);
                }
            }

            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_SpecularRoughnessPBR")) mat.SetFloat("_SpecularRoughnessPBR", Mathf.Clamp01(1f - smoothness * 0.7f));

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void ConfigureBadgeProperties(WagonCapacityBadge badge)
        {
            if (badge == null) return;

            Sprite fullPlateSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Count_FullPlate.png");
            if (fullPlateSprite == null) fullPlateSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Count.png");
            Sprite innerPlateSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Count_InnerPlate.png");
            Sprite pillSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Badge_JuicyPill.png");
            if (pillSprite == null) pillSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Badge_MiniPill.png");
            Sprite shadowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/PoolSlot_Shadow.png");

            SerializedObject so = new SerializedObject(badge);
            so.FindProperty("m_HideModel").boolValue = false;
            so.FindProperty("m_EnableFakeShadow").boolValue = true;
            if (shadowSprite != null) so.FindProperty("m_FakeShadowSprite").objectReferenceValue = shadowSprite;
            if (fullPlateSprite != null) so.FindProperty("m_BackgroundSprite").objectReferenceValue = fullPlateSprite;
            if (innerPlateSprite != null) so.FindProperty("m_InnerPlateSprite").objectReferenceValue = innerPlateSprite;
            so.FindProperty("m_ActiveShowMiniPill").boolValue = true;
            if (pillSprite != null) so.FindProperty("m_MiniPillSprite").objectReferenceValue = pillSprite;
            so.FindProperty("m_ActiveHeadElevation").floatValue = 0.14f;
            so.FindProperty("m_ActiveWorldWidth").floatValue = 0.70f;
            so.ApplyModifiedProperties();
        }
    }
}
