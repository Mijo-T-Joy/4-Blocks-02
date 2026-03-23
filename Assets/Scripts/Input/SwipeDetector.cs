using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Detects touch swipe gestures on mobile (Android/iOS) and converts them
/// into directional input that GameManager can use — same as WASD on desktop.
///
/// Usage:
///   1. Attach to a persistent GameObject (e.g., GameManager or InputManager)
///   2. Subscribe to the OnSwipe event:
///      swipeDetector.OnSwipe += HandleSwipe;
///   3. The event provides a Vector2Int direction:
///      (0,1) = Up, (0,-1) = Down, (-1,0) = Left, (1,0) = Right
/// </summary>
public class SwipeDetector : MonoBehaviour
{
    [Header("Swipe Settings")]
    [Tooltip("Minimum distance in pixels for a touch to count as a swipe")]
    public float minSwipeDistance = 50f;

    [Tooltip("Maximum time in seconds for a swipe gesture")]
    public float maxSwipeTime = 0.5f;

    /// <summary>
    /// Fired when a valid swipe is detected.
    /// Parameter: direction as Vector2Int (e.g., Vector2Int.up for swipe up)
    /// </summary>
    public event Action<Vector2Int> OnSwipe;

    private Vector2 touchStartPosition;
    private float touchStartTime;
    private bool isSwiping;

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        Touch.onFingerDown += OnFingerDown;
        Touch.onFingerUp += OnFingerUp;
    }

    void OnDisable()
    {
        Touch.onFingerDown -= OnFingerDown;
        Touch.onFingerUp -= OnFingerUp;
        EnhancedTouchSupport.Disable();
    }

    private void OnFingerDown(Finger finger)
    {
        // Only track primary finger (index 0)
        if (finger.index != 0) return;

        touchStartPosition = finger.screenPosition;
        touchStartTime = Time.time;
        isSwiping = true;
    }

    private void OnFingerUp(Finger finger)
    {
        if (finger.index != 0 || !isSwiping) return;
        isSwiping = false;

        float swipeDuration = Time.time - touchStartTime;
        if (swipeDuration > maxSwipeTime) return; // Too slow, not a swipe

        Vector2 swipeDelta = finger.screenPosition - touchStartPosition;
        float swipeDistance = swipeDelta.magnitude;

        if (swipeDistance < minSwipeDistance) return; // Too short, not a swipe

        // Determine dominant axis
        Vector2Int direction;
        if (Mathf.Abs(swipeDelta.x) > Mathf.Abs(swipeDelta.y))
        {
            // Horizontal swipe
            direction = swipeDelta.x > 0 ? Vector2Int.right : Vector2Int.left;
        }
        else
        {
            // Vertical swipe
            direction = swipeDelta.y > 0 ? Vector2Int.up : Vector2Int.down;
        }

        OnSwipe?.Invoke(direction);
    }
}
