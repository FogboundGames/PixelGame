using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    [InitializeOnLoad]
    public static class PurgeStrayStudioPreview
    {
        static PurgeStrayStudioPreview()
        {
            EditorApplication.delayCall += ExecutePurge;
        }

        [MenuItem("Tools/PixelGame/🧹 Sahnede Kalan Gizli Önizleme Robotlarını Temizle", priority = 1)]
        public static void ExecutePurge()
        {
            int purged = 0;
            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

            for (int i = 0; i < allObjects.Length; i++)
            {
                GameObject obj = allObjects[i];
                if (obj == null) continue;

                // Prefab varlıklarına dokunma; sadece sahnede canlı duran nesneleri kontrol et
                if (EditorUtility.IsPersistent(obj)) continue;

                if (obj.name == "StudioPreviewModel" ||
                    (obj.name.Contains("BlueBot") && obj.scene.name != null && obj.scene.isLoaded && (obj.hideFlags & HideFlags.DontSave) != 0))
                {
                    Object.DestroyImmediate(obj);
                    purged++;
                }
            }

            // Ayrıca slotların içine takılmış stray nesneler varsa temizle
            TruckSlot[] slots = Object.FindObjectsByType<TruckSlot>(FindObjectsSortMode.None);
            foreach (TruckSlot slot in slots)
            {
                if (slot == null) continue;
                if (slot.GetComponentInParent<TruckPool>() == null) // Sadece ray slotları
                {
                    if (slot.SlotRect != null)
                    {
                        for (int c = slot.SlotRect.childCount - 1; c >= 0; c--)
                        {
                            Transform child = slot.SlotRect.GetChild(c);
                            if (child != null && child != slot.Ground)
                            {
                                Object.DestroyImmediate(child.gameObject);
                                purged++;
                            }
                        }
                    }
                    slot.Truck = null;
                }
            }

            WagonCapacityBadge.PurgeOrphanBadges();

            if (purged > 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                Debug.Log($"<color=#00FFAA><b>[PurgeStrayPreview]</b></color> 🧹 Sahnede takılı kalan {purged} adet sahipsiz robot/nesne başarıyla temizlendi!");
            }
        }
    }
}
