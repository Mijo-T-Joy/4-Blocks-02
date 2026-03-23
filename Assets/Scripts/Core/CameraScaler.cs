using UnityEngine;

/// <summary>
/// Adapts the orthographic camera so the puzzle grid is always fully visible
/// on any device aspect ratio (phones, tablets, desktops).
/// 
/// Strategy for PORTRAIT game:
///   - Lock the horizontal width (what the player always sees left-to-right)
///   - Taller screens (modern phones 19.5:9) see more above/below
///   - Slightly wider screens (iPad 3:4) zoom out a bit to keep full width
///   - VERY wide screens (Windows landscape) use PILLARBOXING (black bars on sides)
///     instead of zooming out too far
/// 
/// Attach to the Main Camera. Set Projection to Orthographic.
/// </summary>
public class CameraScaler : MonoBehaviour
{
    [Header("Reference Resolution (Portrait)")]
    [Tooltip("Reference width in pixels (portrait mode)")]
    public float referenceWidth = 1080f;

    [Tooltip("Reference height in pixels (portrait mode)")]
    public float referenceHeight = 1920f;

    [Tooltip("Orthographic size at reference resolution = refHeight / (2 * PPU)")]
    public float referenceOrthographicSize = 9.6f;

    [Header("Pillarbox Settings")]
    [Tooltip("Maximum aspect ratio before pillarboxing kicks in. " +
             "0.75 = iPad (3:4). Screens wider than this get black bars on the sides.")]
    public float maxAspectBeforePillarbox = 0.85f;

    private Camera cam;
    private float lastAspect;

    void Awake()
    {
        cam = GetComponent<Camera>();
        AdjustCamera();
    }

    void Update()
    {
        // Recalculate if aspect ratio changes (e.g., window resize on PC)
        float currentAspect = (float)Screen.width / Screen.height;
        if (!Mathf.Approximately(currentAspect, lastAspect))
        {
            AdjustCamera();
        }
    }

    void AdjustCamera()
    {
        float referenceAspect = referenceWidth / referenceHeight; // 0.5625 for 9:16
        float currentAspect = (float)Screen.width / Screen.height;
        lastAspect = currentAspect;

        if (currentAspect <= referenceAspect)
        {
            // Narrower or same as reference (tall phones like 9:19.5)
            // Keep ortho size, player sees more vertically
            cam.orthographicSize = referenceOrthographicSize;
            cam.rect = new Rect(0, 0, 1, 1); // Full screen, no bars
        }
        else if (currentAspect <= maxAspectBeforePillarbox)
        {
            // Slightly wider (e.g., iPad 3:4 = 0.75)
            // Zoom out a bit to keep the full width visible
            cam.orthographicSize = referenceOrthographicSize *
                                   (currentAspect / referenceAspect);
            cam.rect = new Rect(0, 0, 1, 1); // Full screen, no bars
        }
        else
        {
            // MUCH wider than reference (e.g., Windows landscape 16:9 = 1.78)
            // Use pillarboxing: cap the zoom, add black bars on sides
            cam.orthographicSize = referenceOrthographicSize *
                                   (maxAspectBeforePillarbox / referenceAspect);

            // Calculate how wide the game viewport should be
            float targetAspect = maxAspectBeforePillarbox;
            float viewportWidth = targetAspect / currentAspect;
            float offsetX = (1f - viewportWidth) / 2f;

            cam.rect = new Rect(offsetX, 0, viewportWidth, 1);
        }
    }
}
