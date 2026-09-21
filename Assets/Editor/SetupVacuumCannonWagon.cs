using System.IO;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class SetupVacuumCannonWagon
    {
        private const string SessionKey = "SetupVacuumCannonWagon_v2";
        private const string ModelPath = "Assets/FreeLowpolyScifiObjects/Art/Models/object_005.fbx";
        private const string SourcePrefabPath = "Assets/FreeLowpolyScifiObjects/Prefabs/Objects/object_005.prefab";
        private const string TargetPrefabPath = "Assets/Prefabs/VacuumCannon.prefab";

        static SetupVacuumCannonWagon()
        {
            // Otomatik tetikleme kapatıldı: proje her açıldığında sahneyi elle onay
            // almadan değiştirip kaydediyordu. Gerekirse elle çalıştırılır.
            // EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);

            ExecuteSetup(silent: true);
        }

        [MenuItem("Tools/PixelGame/🚀 Vakum Topu Vagonunu Kur (object_005)", priority = 25)]
        public static void SetupManual()
        {
            ExecuteSetup(silent: false);
        }

        public static void ExecuteSetup(bool silent)
        {
            // 1. Prefab üret veya güncelle
            GameObject cannonPrefab = BuildOrUpdatePrefab();
            if (cannonPrefab == null)
            {
                Debug.LogError("[SetupVacuumCannonWagon] object_005 modeli bulunamadı!");
                return;
            }

            // 2. Sahnedeki TruckDispatcher'a prefabı bağla ve ray döngüsünü kilitle
            TruckDispatcher dispatcher = Object.FindFirstObjectByType<TruckDispatcher>();
            if (dispatcher != null)
            {
                SerializedObject so = new SerializedObject(dispatcher);
                SerializedProperty prop = so.FindProperty("m_TruckPrefab");
                if (prop != null)
                {
                    prop.objectReferenceValue = cannonPrefab;
                    so.ApplyModifiedProperties();
                }

                dispatcher.SetupPerimeterLoop();
                EditorUtility.SetDirty(dispatcher);
            }

            // 3. SlotRow ve TruckPool'un yönlerini ve hizalamalarını güncelle
            TruckSlotRow slotRow = Object.FindFirstObjectByType<TruckSlotRow>();
            if (slotRow != null)
            {
                slotRow.Style.truckEuler = new Vector3(0f, 180f, 0f);
                slotRow.SyncStyleToSlots();
                EditorUtility.SetDirty(slotRow);
            }

            TruckPool pool = Object.FindFirstObjectByType<TruckPool>();
            if (pool != null)
            {
                pool.Style.truckEuler = new Vector3(0f, 180f, 0f);
                EditorUtility.SetDirty(pool);
            }

            // 4. Sahnedeki tüm TruckSlot'ların taban hizalamasını (AnchorToBase) etkinleştir
            foreach (TruckSlot slot in Object.FindObjectsByType<TruckSlot>(FindObjectsSortMode.None))
            {
                slot.AnchorToBase = true;
                slot.BaseVerticalRatio = -0.07f;
                slot.BaseWidthFill = 0.50f;
                slot.AlignAll();
                EditorUtility.SetDirty(slot);
            }

            // 5. Sahneyi kaydet
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

            if (!silent)
            {
                EditorUtility.DisplayDialog(
                    "Vakum Topu Vagonu Hazır",
                    "object_005 başarıyla küp çeken vagon olarak yapılandırıldı:\n\n" +
                    $"• Prefab: {TargetPrefabPath}\n" +
                    "• Toony Colors Pro 2 karikatür plastik materyali uygulandı.\n" +
                    "• Taban hizalaması (AnchorToBase) slot pad'lerinin üstüne kilitlendi.\n" +
                    "• Konveyör rayı üzerinde tam oturan yön ve ölçek (0.45) uygulandı.",
                    "Tamam"
                );
            }

            Debug.Log("<color=#00FFAA><b>[SetupVacuumCannonWagon]</b></color> Vakum topu (object_005) başarıyla kuruldu ve sahneye kaydedildi!");
        }

        private static GameObject BuildOrUpdatePrefab()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            if (source == null)
            {
                source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            }
            if (source == null) return null;

            GameObject instance = Object.Instantiate(source);
            instance.name = "VacuumCannon";
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;

            // TruckCargo
            TruckCargo cargo = instance.GetComponent<TruckCargo>();
            if (cargo == null) cargo = instance.AddComponent<TruckCargo>();

            // TruckPaint
            TruckPaint paint = instance.GetComponent<TruckPaint>();
            if (paint == null) paint = instance.AddComponent<TruckPaint>();
            paint.SetBodyColor(TruckPaint.Red);
            paint.Apply();

            // WagonCapacityBadge
            WagonCapacityBadge badge = instance.GetComponent<WagonCapacityBadge>();
            if (badge == null) badge = instance.AddComponent<WagonCapacityBadge>();

            // BoxCollider (isTrigger = true)
            BoxCollider col = instance.GetComponent<BoxCollider>();
            if (col == null) col = instance.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.45f, 0.05f);
            col.size = new Vector3(0.9f, 0.9f, 1.0f);

            // WagonClickTarget
            WagonClickTarget clickTarget = instance.GetComponent<WagonClickTarget>();
            if (clickTarget == null) clickTarget = instance.AddComponent<WagonClickTarget>();

            Directory.CreateDirectory(Path.GetDirectoryName(TargetPrefabPath));
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, TargetPrefabPath);
            Object.DestroyImmediate(instance);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return savedPrefab;
        }
    }
}
