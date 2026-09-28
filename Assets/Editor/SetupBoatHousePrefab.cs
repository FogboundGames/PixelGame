using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using PixelGame;

public static class SetupBoatHousePrefab
{
    [MenuItem("PixelGame/Setup Boat House Prefab")]
    public static void ExecuteSetup()
    {
        string fbxPath = "Assets/Kenney/kenney_watercraft-pack/Models/FBX format/boat-house-a.fbx";
        string matPath = "Assets/Materials/Ship_Watercraft_Mat.mat";
        string prefabPath = "Assets/Prefabs/Ship_Boat.prefab";

        // 1. Boat-house-a mesh'ini bul
        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        Mesh boatMesh = null;
        foreach (var sa in subAssets)
        {
            if (sa is Mesh m && m.name.Contains("boat-house-a"))
            {
                boatMesh = m;
                break;
            }
        }

        if (boatMesh == null)
        {
            Debug.LogError($"[SetupBoatHousePrefab] Could not find boat-house-a mesh in {fbxPath}!");
            return;
        }

        Material shipMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (shipMat == null)
        {
            Debug.LogError($"[SetupBoatHousePrefab] Could not find {matPath}!");
            return;
        }

        // 2. Prefab GameObject oluştur
        GameObject boatObj = new GameObject("Ship_Boat");
        boatObj.transform.position = Vector3.zero;
        boatObj.transform.rotation = Quaternion.identity;
        boatObj.transform.localScale = Vector3.one * ShipController.DefaultShipScale;

        MeshFilter mf = boatObj.AddComponent<MeshFilter>();
        mf.sharedMesh = boatMesh;

        MeshRenderer mr = boatObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = shipMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        mr.receiveShadows = true;

        BoxCollider col = boatObj.AddComponent<BoxCollider>();
        col.size = new Vector3(2.68f, 2.2f, 4.64f);
        col.center = new Vector3(0f, 1.0f, 0f);
        col.isTrigger = true;

        ShipController shipCtrl = boatObj.AddComponent<ShipController>();

        // Prefab olarak kaydet
        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(boatObj, prefabPath);
        Object.DestroyImmediate(boatObj);

        Debug.Log($"[SetupBoatHousePrefab] Successfully saved prefab to {prefabPath}!");

        // 3. Sahnedeki ShipQueuePool ve bekleyen gemileri güncelle
        ShipQueuePool pool = Object.FindFirstObjectByType<ShipQueuePool>();
        if (pool != null)
        {
            SerializedObject poolSO = new SerializedObject(pool);
            SerializedProperty prefabProp = poolSO.FindProperty("m_ShipPrefab");
            if (prefabProp != null)
            {
                prefabProp.objectReferenceValue = prefabAsset;
            }
            SerializedProperty scaleProp = poolSO.FindProperty("m_ShipScale");
            if (scaleProp != null)
            {
                scaleProp.floatValue = 0.26f;
            }
            poolSO.ApplyModifiedProperties();

            // Seviye renklerine göre dağıt (Sarı, Açık Yeşil, Yeşil, Siyah, Kahve)
            Color[] avocadoColors = new Color[]
            {
                new Color(0.976f, 0.659f, 0.145f, 1f), // 0: Sarı
                new Color(0.776f, 1.000f, 0.000f, 1f), // 1: Açık Yeşil
                new Color(0.596f, 0.769f, 0.000f, 1f), // 2: Yeşil
                new Color(0.12f, 0.12f, 0.14f, 1f),   // 3: Siyah
                new Color(0.35f, 0.22f, 0.18f, 1f),   // 4: Kahve
                new Color(0.976f, 0.659f, 0.145f, 1f), // 5: Sarı
                new Color(0.776f, 1.000f, 0.000f, 1f), // 6: Açık Yeşil
                new Color(0.12f, 0.12f, 0.14f, 1f)    // 7: Siyah
            };
            int[] capacities = new int[] { 16, 14, 18, 15, 17, 15, 21, 16 };

            // Bekleyen tüm gemileri güncelle
            for (int i = 0; i < pool.transform.childCount; i++)
            {
                Transform spot = pool.transform.GetChild(i);
                ShipController ship = spot.GetComponentInChildren<ShipController>(true);
                if (ship != null)
                {
                    // Mesh güncelle
                    MeshFilter shipMf = ship.GetComponent<MeshFilter>();
                    if (shipMf != null) shipMf.sharedMesh = boatMesh;

                    ship.transform.localScale = Vector3.one * 0.26f;

                    Color targetColor = (i < avocadoColors.Length) ? avocadoColors[i] : ship.ShipColor;
                    int targetCap = (i < capacities.Length) ? capacities[i] : ship.Capacity;

                    // Eski dairesel çerçeveyi temizle
                    Transform oldCanvas = ship.transform.Find("Ship_Capacity_Canvas");
                    if (oldCanvas != null)
                    {
                        Transform oldRing = oldCanvas.Find("Badge_CircleRing");
                        if (oldRing != null) Object.DestroyImmediate(oldRing.gameObject);
                    }

                    // Görseli yenile ve rengi uygula
                    ship.Configure(targetColor, targetCap);

                    // Doğrudan materyal ata (Sahne ve editörde %100 canlı görünmesi için)
                    MeshRenderer shipMr = ship.GetComponent<MeshRenderer>();
                    if (shipMr != null)
                    {
                        Material boatMat = ShipController.GetOrCreateBoatMaterial(targetColor);
                        shipMr.sharedMaterial = boatMat;
                    }

                    EditorUtility.SetDirty(ship);
                    EditorUtility.SetDirty(ship.gameObject);
                }
            }

            Debug.Log("[SetupBoatHousePrefab] Updated ShipQueuePool and configured all waiting ships with level colors at scale 0.26!");
        }

        // Sahneyi kaydet
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.isLoaded)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log($"[SetupBoatHousePrefab] Scene '{activeScene.name}' saved successfully!");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
