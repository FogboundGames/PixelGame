using System.IO;
using UnityEditor;
using UnityEngine;

namespace PixelGame.Editor
{
    public static class DebugInspectShips
    {
        // [MenuItem("PixelGame/🔍 Debug Inspect Ships")]
        public static void Inspect()
        {
            var ships = Object.FindObjectsByType<ShipController>(FindObjectsSortMode.None);
            using (var sw = new StreamWriter("scratch/ships_info.txt"))
            {
                sw.WriteLine($"Total ships found: {ships.Length}");
                foreach (var ship in ships)
                {
                    sw.WriteLine($"Ship: name='{ship.gameObject.name}', active={ship.gameObject.activeInHierarchy}, parent='{(ship.transform.parent != null ? ship.transform.parent.name : "null")}', worldPos={ship.transform.position}, localPos={ship.transform.localPosition}, color={ship.ShipColor}, capacity={ship.Capacity}, isDocked={ship.IsDocked}");
                }

                var queuePool = Object.FindFirstObjectByType<ShipQueuePool>();
                if (queuePool != null)
                {
                    sw.WriteLine($"QueuePool: pos={queuePool.transform.position}, rot={queuePool.transform.rotation.eulerAngles}, scale={queuePool.transform.localScale}");
                    for (int i = 0; i < queuePool.transform.childCount; i++)
                    {
                        var c = queuePool.transform.GetChild(i);
                        sw.WriteLine($"  Child {i}: name='{c.name}', localPos={c.localPosition}, worldPos={c.position}, childCount={c.childCount}");
                    }
                }

                var marina = Object.FindFirstObjectByType<MarinaSlotLayout>();
                if (marina != null)
                {
                    sw.WriteLine($"Marina: pos={marina.transform.position}");
                    var slots = marina.GetComponentsInChildren<ShipSlot>();
                    for (int i = 0; i < slots.Length; i++)
                    {
                        sw.WriteLine($"  Slot {i}: name='{slots[i].name}', pos={slots[i].transform.position}, dockedShip={(slots[i].DockedShip != null ? slots[i].DockedShip.name : "null")}");
                    }
                }
            }
            Debug.Log("[DebugInspectShips] Saved info to scratch/ships_info.txt");
        }
    }
}
