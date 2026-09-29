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

        // 2. Prefab GameObject oluştur (Aşama 2 Decoupled Mimari)
        GameObject boatObj = new GameObject("Ship_Boat");
        boatObj.transform.position = Vector3.zero;
        boatObj.transform.rotation = Quaternion.identity;
        boatObj.transform.localScale = Vector3.one * ShipController.DefaultShipScale;

        // VisualRoot child (Görsel mesh bağımsız child transform altında)
        GameObject visObj = new GameObject("[VisualRoot]");
        visObj.transform.SetParent(boatObj.transform, false);
        visObj.transform.localPosition = Vector3.zero;
        visObj.transform.localRotation = Quaternion.identity;
        visObj.transform.localScale = Vector3.one;

        MeshFilter mf = visObj.AddComponent<MeshFilter>();
        mf.sharedMesh = boatMesh;

        MeshRenderer mr = visObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = shipMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        mr.receiveShadows = true;

        // Collider Gameplay Root üzerinde kalır (InteractionRoot) — VisualRoot'tan bağımsızdır
        BoxCollider col = boatObj.AddComponent<BoxCollider>();
        col.size = new Vector3(2.68f, 2.2f, 4.64f);
        col.center = new Vector3(0f, 1.0f, 0f);
        col.isTrigger = true;

        ShipController shipCtrl = boatObj.AddComponent<ShipController>();
        shipCtrl.EnsureDecoupledHierarchy();

        // Prefab olarak kaydet
        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(boatObj, prefabPath);
        Object.DestroyImmediate(boatObj);

        Debug.Log($"[SetupBoatHousePrefab] Successfully saved prefab to {prefabPath}!");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
