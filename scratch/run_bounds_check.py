import os

script = """
using UnityEngine;
using UnityEditor;

public static class CheckSailorBounds
{
    [MenuItem("Tools/PixelGame/Check Sailor Bounds")]
    public static void Run()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Sailor_Runner.prefab");
        var instance = Object.Instantiate(prefab);
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        var renderers = instance.GetComponentsInChildren<Renderer>();
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        bool first = true;
        foreach (var r in renderers)
        {
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
            Debug.Log($"Renderer {r.name}: center={r.bounds.center}, extents={r.bounds.extents}, min={r.bounds.min}, max={r.bounds.max}");
        }
        Debug.Log($"TOTAL BOUNDS: center={b.center}, size={b.size}, min={b.min}, max={b.max}");
        Object.DestroyImmediate(instance);
    }
}
"""

with open("Assets/Editor/CheckSailorBounds.cs", "w", encoding="utf-8") as f:
    f.write(script)
print("Written CheckSailorBounds.cs")
