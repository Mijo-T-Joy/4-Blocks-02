using UnityEngine;

/// <summary>
/// Adjusts a RectTransform (Panel) to stay inside the device's safe area,
/// avoiding notches, rounded corners, and home indicators on modern phones.
///
/// Usage:
///   1. Create a Canvas (Screen Space - Overlay)
///   2. Add a Panel as a child, stretch it to fill the Canvas
///   3. Attach this script to the Panel
///   4. Put ALL your UI elements INSIDE this Panel
///   5. The Panel will automatically shrink to avoid unsafe areas
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaHandler : MonoBehaviour
{
    private RectTransform panelRect;
    private Rect lastSafeArea = Rect.zero;
    private Vector2Int lastScreenSize = Vector2Int.zero;

    void Awake()
    {
        panelRect = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    void Update()
    {
        // Re-check if screen size or safe area changed (rotation, window resize)
        if (Screen.width != lastScreenSize.x || Screen.height != lastScreenSize.y)
        {
            ApplySafeArea();
        }
    }

    void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;

        if (safeArea == lastSafeArea &&
            Screen.width == lastScreenSize.x &&
            Screen.height == lastScreenSize.y)
        {
            return;
        }

        lastSafeArea = safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        // Convert safe area from pixel coordinates to anchor coordinates (0-1)
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        panelRect.anchorMin = anchorMin;
        panelRect.anchorMax = anchorMax;
    }
}
