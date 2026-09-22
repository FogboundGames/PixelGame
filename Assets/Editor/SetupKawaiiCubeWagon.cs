using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelGame.Editor
{
    /// <summary>
    /// Görseldeki parlak jelibon/plastik gövdeli, iki siyah gözlü ve gülen ağızlı
    /// sevimli KawaiiCube modelini materyalleri, rozetleri ve bant hizalamalarıyla
    /// birlikte prefab olarak üretip oyundaki bant vagonu olarak aktif eder.
    /// Bant üzerinde tüm rotasyonları sabit, dik ve kameraya dönük kalacak şekilde ayarlar.
    /// </summary>
    [InitializeOnLoad]
    public static class SetupKawaiiCubeWagon
    {
        private const string SessionKey = "SetupKawaiiCubeWagon_Executed_v3";

        static SetupKawaiiCubeWagon()
        {
            EditorApplication.delayCall += AutoRunOnce;
        }

        private static void AutoRunOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) return;
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            ExecuteSetup(silent: true);
        }

        private const string ModelObjPath = "Assets/Models/KawaiiCube.obj";
        private const string TargetPrefabPath = "Assets/Prefabs/KawaiiCubeWagon.prefab";
        private const string FallbackCyberPrefabPath = "Assets/Prefabs/CyberCubeWagon.prefab";
        private const string BodyMatPath = "Assets/Materials/KawaiiCube_Body_Mat.mat";
        private const string FaceMatPath = "Assets/Materials/KawaiiCube_Face_Mat.mat";

        [MenuItem("Tools/PixelGame/🐱 KawaiiCube Modelini Kur & Aktif Et", priority = 1)]
        public static void SetupManual()
        {
            ExecuteSetup(silent: false);
        }

        public static void ExecuteSetup(bool silent = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
            {
                if (!silent)
                {
                    EditorUtility.DisplayDialog("Oyun Modu Aktif", "Kurulum oyun çalışırken (Play Mode) yapılamaz. Lütfen önce Play modunu durdurup tekrar deneyin.", "Tamam");
                }
                return;
            }

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
                    Debug.LogError($"[SetupKawaiiCubeWagon] Model bulunamadı: {ModelObjPath}");
                    return;
                }

                Mesh kawaiiMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/RoundedCube.asset");
                if (kawaiiMesh == null && modelAsset != null)
                {
                    MeshFilter sourceMf = modelAsset.GetComponentInChildren<MeshFilter>(true);
                    kawaiiMesh = sourceMf != null ? sourceMf.sharedMesh : null;
                }
                if (kawaiiMesh == null)
                {
                    kawaiiMesh = AssetDatabase.LoadAssetAtPath<Mesh>(ModelObjPath);
                }

                if (kawaiiMesh == null)
                {
                    Debug.LogError("[SetupKawaiiCubeWagon] KawaiiCube mesh'i yüklenemedi!");
                    return;
                }

                // 2. 2 Adet Toon Materyalini Hazırla
                Material bodyMat = GetOrCreateMaterial(BodyMatPath, "KawaiiCube_Body_Mat", new Color(0.0f, 0.65f, 1.0f), 0.94f);
                Material faceMat = GetOrCreateMaterial(FaceMatPath, "KawaiiCube_Face_Mat", new Color(0.06f, 0.07f, 0.10f), 0.90f);

                // 3. KawaiiCubeWagon Prefab'ını İnşa Et
                GameObject wagonGo = new GameObject("KawaiiCubeWagon");
                wagonGo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                wagonGo.transform.localScale = Vector3.one;

                MeshFilter mf = wagonGo.AddComponent<MeshFilter>();
                mf.sharedMesh = kawaiiMesh;

                MeshRenderer mr = wagonGo.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new Material[] { bodyMat, faceMat };
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

                // BoxCollider (Küp sınırlarına tam oturan trigger)
                BoxCollider box = wagonGo.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = Vector3.zero;
                box.size = Vector3.one;

                // WagonCapacityBadge
                WagonCapacityBadge badge = wagonGo.AddComponent<WagonCapacityBadge>();
                ConfigureBadgeProperties(badge);

                // 4. Sevimli Yüz Çocuk Nesnesi (Face Child) & KawaiiFaceController
                GameObject faceGo = new GameObject("Face");
                faceGo.transform.SetParent(wagonGo.transform, false);
                faceGo.transform.localPosition = new Vector3(0f, -0.08f, -0.52f);
                faceGo.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
                faceGo.transform.localScale = new Vector3(0.80f, 0.80f, 1f);

                SpriteRenderer faceSr = faceGo.AddComponent<SpriteRenderer>();
                Sprite defaultFaceSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/KawaiiFace_Smile.png");
                if (defaultFaceSprite == null) defaultFaceSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/KawaiiCube_Face.png");
                faceSr.sprite = defaultFaceSprite;
                faceSr.sortingOrder = 20;
                faceSr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                faceSr.receiveShadows = false;

                KawaiiFaceController faceCtrl = wagonGo.AddComponent<KawaiiFaceController>();
                faceCtrl.EnsureRendererReference();
                faceCtrl.ApplyExpression(force: true);

                // Prefab Olarak Kaydet (KawaiiCubeWagon)
                Directory.CreateDirectory(Path.GetDirectoryName(TargetPrefabPath));
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(wagonGo, TargetPrefabPath);

                // CyberCubeWagon prefab'ını da KawaiiCube mesh'i ile senkronize et
                GameObject cyberSavedPrefab = null;
                try
                {
                    wagonGo.name = "CyberCubeWagon";
                    cyberSavedPrefab = PrefabUtility.SaveAsPrefabAsset(wagonGo, FallbackCyberPrefabPath);
                }
                catch { }

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
                        prop.objectReferenceValue = savedPrefab != null ? savedPrefab : cyberSavedPrefab;
                        so.ApplyModifiedProperties();
                    }

                    EditorUtility.SetDirty(dispatcher);
                }

                // 5. SlotRow ve TruckPool yönlerini güncelle: 38° X açısı (İkinci görseldeki gibi üst yüzey parlar, yüz öne bakar)
                Vector3 cuteEuler = new Vector3(38f, 0f, 0f);
                TruckSlotRow slotRow = UnityEngine.Object.FindFirstObjectByType<TruckSlotRow>();
                if (slotRow != null)
                {
                    slotRow.Style.truckEuler = cuteEuler;
                    slotRow.SyncStyleToSlots();
                    EditorUtility.SetDirty(slotRow);
                }

                TruckPool pool = UnityEngine.Object.FindFirstObjectByType<TruckPool>();
                if (pool != null)
                {
                    pool.Style.truckEuler = cuteEuler;
                    EditorUtility.SetDirty(pool);
                }

                // 6. Sahnedeki TruckSlot taban hizalamalarını uygula
                foreach (TruckSlot slot in UnityEngine.Object.FindObjectsByType<TruckSlot>(FindObjectsSortMode.None))
                {
                    if (slot == null) continue;
                    slot.AnchorToBase = true;
                    slot.BaseVerticalRatio = -0.05f;
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

                Debug.Log("<color=#00FFAA><b>[SetupKawaiiCubeWagon]</b></color> KawaiiCube modeli başarıyla oluşturuldu, prefab kaydedildi ve konveyör bandında aktif vagon olarak bağlandı!");

                if (!silent)
                {
                    EditorUtility.DisplayDialog(
                        "KawaiiCube Aktif Edildi",
                        "Görseldeki sevimli küp modeli başarıyla hazırlandı ve aktif bant vagonu yapıldı:\n\n" +
                        "• Model: Assets/Models/KawaiiCube.obj\n" +
                        "• Prefab: Assets/Prefabs/KawaiiCubeWagon.prefab\n" +
                        "• Jelibon parlaklığında gövde + sevimli gülen yüz (• ‿ •)\n" +
                        "• Bant üzerinde tüm rotasyonları dik ve kameraya dönük sabitlendi.\n" +
                        "• TruckDispatcher'a aktif vagon olarak atandı.",
                        "Tamam"
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SetupKawaiiCubeWagon] Hata oluştu: {ex}");
            }
        }

        private static Material GetOrCreateMaterial(string path, string name, Color baseColor, float smoothness)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader toonShader = Shader.Find("PixelGame/Cartoon");
                if (toonShader == null) toonShader = Shader.Find("Universal Render Pipeline/Lit");
                if (toonShader == null) toonShader = Shader.Find("Standard");

                mat = new Material(toonShader);
                mat.name = name;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.color = baseColor;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 1f);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void ConfigureBadgeProperties(WagonCapacityBadge badge)
        {
            if (badge == null) return;
            SerializedObject so = new SerializedObject(badge);

            SerializedProperty followTarget = so.FindProperty("m_FollowTarget");
            if (followTarget != null) followTarget.boolValue = true;

            SerializedProperty badgeAnchor = so.FindProperty("m_BadgeAnchor");
            if (badgeAnchor != null) badgeAnchor.enumValueIndex = 0; // Top

            SerializedProperty anchorOffset = so.FindProperty("m_AnchorOffset");
            if (anchorOffset != null) anchorOffset.vector3Value = new Vector3(0f, 0.40f, 0f);

            SerializedProperty sizeMode = so.FindProperty("m_SizeMode");
            if (sizeMode != null) sizeMode.enumValueIndex = 2; // AutoFitModelWidth

            SerializedProperty dynamicColor = so.FindProperty("m_DynamicThemeColor");
            if (dynamicColor != null) dynamicColor.boolValue = true;

            SerializedProperty showMiniPill = so.FindProperty("m_ActiveShowMiniPill");
            if (showMiniPill != null) showMiniPill.boolValue = false;

            SerializedProperty billboard = so.FindProperty("m_Billboard");
            if (billboard != null) billboard.boolValue = true;

            so.ApplyModifiedProperties();
        }
    }
}
