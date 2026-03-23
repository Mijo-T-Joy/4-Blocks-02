using System;
using UnityEngine;

/// <summary>
/// Controls a single puzzle block's sliding movement.
///
/// IMPORTANT DESIGN CHANGE FROM V1:
///   v1: Each block read input independently (WASD/swipe events)
///   v2: Block is a PASSIVE MOVER — it does NOT read input.
///       GameManager decides when and where to slide, then calls
///       SetSlideTarget() and StartSliding() on this block.
///
/// HOW SLIDING WORKS:
///   1. GameManager calls SetSlideTarget(destination, gridSystem)
///      → Block stores the target world position
///   2. GameManager calls StartSliding()
///      → Block starts lerping toward the target in FixedUpdate()
///   3. When close enough, block snaps to exact grid position
///   4. Block fires OnStopped event → GameManager counts it
///   5. When all 4 blocks fire OnStopped, GameManager checks win
///
/// WHY LERP INSTEAD OF VELOCITY:
///   v1 used rb.linearVelocity which caused blocks to overshoot or jitter.
///   Lerping with rb.MovePosition() gives smooth, predictable movement
///   that always lands on exact grid positions.
///
/// PHYSICS SETUP (do this on the Prefab in Unity):
///   Rigidbody2D:
///     - Body Type: Dynamic
///     - Gravity Scale: 0
///     - Collision Detection: Continuous
///     - Constraints: Freeze Rotation Z
///   BoxCollider2D:
///     - Size: 0.95 × 0.95 (slight gap prevents blocks from "sticking")
///   Physics Material 2D:
///     - Friction: 0, Bounciness: 0 (assign to the collider)
/// </summary>
public class Block : MonoBehaviour
{
    // ──────────────────────────────────────────────
    //  INSPECTOR SETTINGS
    // ──────────────────────────────────────────────

    [Header("Movement")]
    [Tooltip("How fast the block slides toward its target (units per second)")]
    public float slideSpeed = 15f;

    [Tooltip("When block is this close to target, snap to exact position")]
    public float snapThreshold = 0.05f;

    // ──────────────────────────────────────────────
    //  PUBLIC PROPERTIES
    // ──────────────────────────────────────────────

    /// <summary>
    /// Index of this block (0-3). Set by LevelManager when spawning.
    /// Used to match block to its target position in LevelData.
    /// </summary>
    public int BlockIndex { get; set; }

    /// <summary>
    /// Returns true while the block is actively sliding toward a target.
    /// GameManager checks this to prevent new input during movement.
    /// </summary>
    public bool IsSliding { get; private set; }

    /// <summary>
    /// Returns true if SetSlideTarget() was called and the block has
    /// a destination that differs from its current position.
    /// </summary>
    public bool HasTarget { get; private set; }

    /// <summary>
    /// Current grid position (snapped). 
    /// This is in GRID coordinates (includes boundary wall offset).
    /// </summary>
    public Vector2Int GridPosition { get; private set; }
    
    // The target grid cell we are sliding towards
    private Vector2Int targetGridPosition;

    // ──────────────────────────────────────────────
    //  EVENTS
    // ──────────────────────────────────────────────

    /// <summary>
    /// Fired when this block finishes sliding and snaps to its grid cell.
    /// GameManager subscribes to this — when all 4 blocks fire OnStopped,
    /// it increments the move counter and checks win condition.
    /// </summary>
    public event Action OnStopped;

    // ──────────────────────────────────────────────
    //  PRIVATE STATE
    // ──────────────────────────────────────────────

    private Rigidbody2D rb;
    private Vector3 targetWorldPosition;

    // ──────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ──────────────────────────────────────────────

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// FixedUpdate is used because we're moving via Rigidbody2D.MovePosition().
    /// Physics operations should always happen in FixedUpdate for consistency.
    /// </summary>
    void FixedUpdate()
    {
        if (!IsSliding) return;

        // Move toward target
        Vector3 currentPos = rb.position;
        Vector3 newPos = Vector3.MoveTowards(currentPos, targetWorldPosition,
                                              slideSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);

        // Check if we've arrived
        float distance = Vector3.Distance(newPos, targetWorldPosition);
        if (distance <= snapThreshold)
        {
            // Snap to exact grid position
            rb.MovePosition(targetWorldPosition);
            GridPosition = targetGridPosition;
            IsSliding = false;
            HasTarget = false;

            // Notify GameManager that this block has stopped
            OnStopped?.Invoke();
        }
    }

    // ──────────────────────────────────────────────
    //  PUBLIC METHODS (called by GameManager)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Sets where this block should slide to.
    /// Called by GameManager BEFORE StartSliding().
    ///
    /// If destination == current position, HasTarget stays false
    /// and the block won't move (it's already at a wall).
    /// </summary>
    public void SetSlideTarget(Vector2Int gridDestination, GridSystem gridSystem)
    {
        Vector3 worldDest = gridSystem.GridToWorld(gridDestination);

        if (Vector3.Distance(transform.position, worldDest) < snapThreshold)
        {
            // Already at destination — no need to slide
            HasTarget = false;
            return;
        }

        targetGridPosition = gridDestination;
        targetWorldPosition = worldDest;
        HasTarget = true;
    }

    /// <summary>
    /// Begins the sliding animation toward the target set by SetSlideTarget().
    /// The actual movement happens in FixedUpdate().
    /// </summary>
    public void StartSliding()
    {
        if (!HasTarget) return;
        IsSliding = true;
    }

    /// <summary>
    /// Instantly moves the block to a grid position without animation.
    /// Used for:
    ///   - Initial spawning (LevelManager places blocks)
    ///   - Undo (GameManager restores previous positions)
    ///   - Restart (reload the level)
    /// </summary>
    public void TeleportToGrid(Vector2Int gridPosition, GridSystem gridSystem)
    {
        GridPosition = gridPosition;
        targetGridPosition = gridPosition;
        Vector3 worldPos = gridSystem.GridToWorld(gridPosition);
        transform.position = worldPos;
        rb.position = worldPos;
        IsSliding = false;
        HasTarget = false;
    }

    /// <summary>
    /// Snaps the block to the nearest grid cell center.
    /// Safety method — call if the block somehow ends up between cells.
    /// </summary>
    public void SnapToGrid()
    {
        // No longer supports snapping without GridSystem since grid formula involves dynamic offsets.
        // Call TeleportToGrid(GridPosition, gridSystem) instead to snap reliably if needed.
    }
}
