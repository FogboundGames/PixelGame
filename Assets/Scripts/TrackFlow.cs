using UnityEngine;

/// <summary>
/// Scrolls the chevron texture on the track material so the path reads as flowing.
/// Put this on ONE manager object in the scene and assign the dark-channel material.
/// Every tile sharing that material flows in sync, at zero extra cost.
///
/// Material setup (URP/Lit or Unlit):
///   Base Map  = Track_Chevron.png
///   Tiling    = (4, 1)      <- 4 chevrons per straight tile, 3 through a corner
///   Offset    = (0, 0)      <- driven by this script
/// </summary>
public class TrackFlow : MonoBehaviour
{
    [Tooltip("The material used by the dark channel of the track tiles.")]
    public Material trackMaterial;

    [Tooltip("UV units per second. Tiling is 4, so 0.25 means one chevron per second. 0.90 gives a lively, snappy flow.")]
    public float speed = 0.90f;

    [Tooltip("Flow against the tiles' local +X instead of along it.")]
    public bool reverse = true;

    static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    static readonly int MainTex = Shader.PropertyToID("_MainTex");

    int   _prop;
    float _offset;

    void OnEnable()
    {
        if (trackMaterial == null) { enabled = false; return; }
        _prop = trackMaterial.HasProperty(BaseMap) ? BaseMap : MainTex;
        _offset = 0f;
    }

    void Update()
    {
        _offset = Mathf.Repeat(_offset + (reverse ? -speed : speed) * Time.deltaTime, 1f);
        Vector2 o = trackMaterial.GetTextureOffset(_prop);
        o.x = _offset;
        trackMaterial.SetTextureOffset(_prop, o);
    }

    // Leave the material asset clean when play mode ends.
    void OnDisable()
    {
        if (trackMaterial == null) return;
        Vector2 o = trackMaterial.GetTextureOffset(_prop);
        o.x = 0f;
        trackMaterial.SetTextureOffset(_prop, o);
    }

    /// <summary>Stop or start the flow at runtime (e.g. while a puzzle is paused).</summary>
    public void SetFlowing(bool on) { enabled = on; }
}
