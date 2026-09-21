using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Plays the animated corner gear GIF frames smoothly at the conveyor belt corner.
/// Automatically synchronizes playback speed with TrackFlow so the gear turns
/// naturally as the conveyor belt rolls.
/// Works with both World Space SpriteRenderer and UI Image.
/// </summary>
[ExecuteAlways]
public class CornerGearAnimator : MonoBehaviour
{
    [Header("GIF Frame Sequence")]
    [Tooltip("The 36 animation frames extracted from the gear GIF.")]
    public Sprite[] frames;

    [Header("Playback Settings")]
    [Tooltip("Base playback frame rate (20 fps matches the original 50ms GIF timing).")]
    public float baseFps = 20f;

    [Tooltip("If true, animation speed is scaled by TrackFlow belt speed.")]
    public bool syncWithTrackFlow = true;

    [Tooltip("Reverse the rotation direction if needed.")]
    public bool reverse = false;

    [Tooltip("Reference to the scene's TrackFlow conveyor manager.")]
    public TrackFlow trackFlow;

    private SpriteRenderer _spriteRenderer;
    private Image _uiImage;
    private float _timer;
    private int _lastIndex = -1;

    void Awake()
    {
        CacheRenderers();
    }

    void OnEnable()
    {
        CacheRenderers();
        if (trackFlow == null)
        {
            trackFlow = Object.FindFirstObjectByType<TrackFlow>();
        }

        if (frames == null || frames.Length == 0)
        {
            var resFrames = Resources.LoadAll<Sprite>("GearFrames");
            if (resFrames != null && resFrames.Length > 0)
            {
                System.Array.Sort(resFrames, (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
                frames = resFrames;
            }
            #if UNITY_EDITOR
            else
            {
                LoadFramesInEditor();
            }
            #endif
        }
    }

    void CacheRenderers()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _uiImage = GetComponent<Image>();
    }

    void Update()
    {
        if (frames == null || frames.Length == 0) return;

        float effectiveFps = baseFps;

        if (syncWithTrackFlow && Application.isPlaying)
        {
            if (trackFlow == null)
            {
                trackFlow = Object.FindFirstObjectByType<TrackFlow>();
            }

            if (trackFlow != null)
            {
                if (!trackFlow.enabled)
                {
                    // Conveyor is paused, gear pauses
                    return;
                }
                // Scale speed relative to standard flow speed (0.65)
                float speedRatio = Mathf.Abs(trackFlow.speed) / 0.65f;
                effectiveFps = baseFps * Mathf.Max(0.2f, speedRatio);
            }
        }

        float dt = Application.isPlaying ? Time.deltaTime : 0.033f;
        float dir = reverse ? -1f : 1f;

        // If TrackFlow has reverse enabled, match direction
        if (trackFlow != null && trackFlow.reverse)
        {
            dir = -dir;
        }

        _timer += dt * effectiveFps * dir;

        int count = frames.Length;
        int frameIndex = Mathf.FloorToInt(_timer) % count;
        if (frameIndex < 0) frameIndex += count;

        if (frameIndex != _lastIndex)
        {
            _lastIndex = frameIndex;
            ApplySprite(frames[frameIndex]);
        }
    }

    void ApplySprite(Sprite s)
    {
        if (s == null) return;

        if (_spriteRenderer != null)
        {
            _spriteRenderer.sprite = s;
        }
        else if (_uiImage != null)
        {
            _uiImage.sprite = s;
        }
    }

    #if UNITY_EDITOR
    [ContextMenu("Load Frames From UI/GearFrames")]
    public void LoadFramesInEditor()
    {
        string folder = "Assets/UI/GearFrames";
        var list = new System.Collections.Generic.List<Sprite>();

        for (int i = 0; i < 36; i++)
        {
            string path = $"{folder}/gear_{i:02d}.png";
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) list.Add(s);
        }

        if (list.Count > 0)
        {
            frames = list.ToArray();
            EditorUtility.SetDirty(this);
            Debug.Log($"<color=#00FFAA>[CornerGearAnimator]</color> {frames.Length} adet dişli karesi başarıyla yüklendi!");
            if (_lastIndex < 0 || _lastIndex >= frames.Length) _lastIndex = 0;
            ApplySprite(frames[0]);
        }
        else
        {
            Debug.LogWarning("[CornerGearAnimator] Assets/UI/GearFrames klasöründe kareler bulunamadı!");
        }
    }
    #endif
}
