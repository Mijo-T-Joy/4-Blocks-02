using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// The BRAIN of the game — centralized controller that coordinates everything.
///
/// WHAT IT DOES:
///   1. Reads input (WASD/arrows from keyboard, swipes from mobile)
///   2. Sends slide commands to ALL 4 blocks simultaneously
///   3. Tracks move count
///   4. Checks win condition after all blocks stop
///   5. Manages undo (snapshots of all block positions)
///   6. Controls game state (Playing / Won / Paused)
///
/// WHY A SINGLETON:
///   There should only ever be ONE GameManager. The Singleton pattern
///   ensures that, and lets other scripts access it via GameManager.Instance.
///
/// SETUP IN UNITY:
///   1. Create an empty GameObject called "GameManager"
///   2. Attach this script to it
///   3. Also attach GridSystem to the same object (or assign in Inspector)
///   4. Assign references: levelManager, hudController, winScreenController
///      (these can be on separate GameObjects)
///   5. The SwipeDetector is found automatically at runtime
/// </summary>
public class GameManager : MonoBehaviour
{
    // ──────────────────────────────────────────────
    //  SINGLETON
    // ──────────────────────────────────────────────

    /// <summary>
    /// Global access point. Any script can call GameManager.Instance
    /// to get the one and only GameManager.
    /// </summary>
    public static GameManager Instance { get; private set; }

    // ──────────────────────────────────────────────
    //  GAME STATE
    // ──────────────────────────────────────────────

    /// <summary>
    /// The three possible states the game can be in.
    /// Playing = accepting input, Won = level complete, Paused = frozen.
    /// </summary>
    public enum GameState { Playing, Won, Paused }

    /// <summary>Current state — other scripts can read this to know what's happening.</summary>
    public GameState CurrentState { get; private set; } = GameState.Playing;

    // ──────────────────────────────────────────────
    //  INSPECTOR REFERENCES
    // ──────────────────────────────────────────────

    [Header("References")]
    [Tooltip("Drag the LevelManager GameObject here")]
    public LevelManager levelManager;

    [Tooltip("Drag the GridSystem component here (can be on this same object)")]
    public GridSystem gridSystem;

    [Tooltip("Drag the HUDController component here")]
    public HUDController hudController;

    [Tooltip("Drag the WinScreenController component here")]
    public WinScreenController winScreenController;

    [Header("Level")]
    [Tooltip("Drag the LevelRegistry asset here")]
    public LevelRegistry levelRegistry;
    
    private int currentLevelIndex = -1;

    // ──────────────────────────────────────────────
    //  RUNTIME STATE
    // ──────────────────────────────────────────────

    /// <summary>The 4 block instances in the current level.</summary>
    private Block[] blocks;

    /// <summary>How many moves the player has made this level.</summary>
    private int moveCount;

    /// <summary>
    /// Undo stack — each entry is a snapshot of all 4 block grid positions.
    /// When the player presses Undo, we pop the last snapshot and teleport
    /// all blocks back to those positions.
    /// </summary>
    private Stack<Vector2Int[]> undoStack = new Stack<Vector2Int[]>();

    /// <summary>How many blocks are currently mid-slide.</summary>
    private int slidingBlockCount;

    // Input
    private PlayerInputActions inputActions;
    private SwipeDetector swipeDetector;

    // ──────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ──────────────────────────────────────────────

    void Awake()
    {
        // Singleton setup — destroy duplicates
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Setup input
        inputActions = new PlayerInputActions();
    }

    void OnEnable()
    {
        inputActions.Player.Enable();

        // Subscribe to input actions
        inputActions.Player.Move.performed += OnMoveInput;
        inputActions.Player.Undo.performed += OnUndoInput;
        inputActions.Player.Restart.performed += OnRestartInput;

        // Find and subscribe to swipe detector (for mobile)
        swipeDetector = FindFirstObjectByType<SwipeDetector>();
        if (swipeDetector != null)
        {
            swipeDetector.OnSwipe += OnSwipeInput;
        }
    }

    void OnDisable()
    {
        inputActions.Player.Move.performed -= OnMoveInput;
        inputActions.Player.Undo.performed -= OnUndoInput;
        inputActions.Player.Restart.performed -= OnRestartInput;

        inputActions.Player.Disable();

        if (swipeDetector != null)
        {
            swipeDetector.OnSwipe -= OnSwipeInput;
        }
    }

    void Start()
    {
        if (levelRegistry == null || levelRegistry.levels == null || levelRegistry.levels.Length == 0)
        {
            Debug.LogWarning("GameManager: No LevelRegistry assigned or it's empty!");
            return;
        }

        // Read the level chosen from the Level Select screen
        currentLevelIndex = Mathf.Clamp(
            LevelSelectController.SelectedLevelIndex,
            0,
            levelRegistry.levels.Length - 1
        );

        LoadLevel(levelRegistry.levels[currentLevelIndex]);
    }

    // ──────────────────────────────────────────────
    //  LEVEL MANAGEMENT
    // ──────────────────────────────────────────────

    /// <summary>
    /// Loads a level: sets up the grid, spawns walls + blocks, resets state.
    /// </summary>
    public void LoadLevel(LevelData levelData)
    {
        // Reset state
        CurrentState = GameState.Playing;
        moveCount = 0;
        undoStack.Clear();
        slidingBlockCount = 0;

        // Setup grid
        gridSystem.SetupGrid(levelData);

        // Spawn walls and blocks via LevelManager
        levelManager.LoadLevel(levelData, gridSystem);

        // Get references to the spawned blocks
        blocks = levelManager.GetBlocks();

        // Subscribe to each block's OnStopped event
        foreach (Block block in blocks)
        {
            block.OnStopped += OnBlockStopped;
        }

        // Update UI
        if (hudController != null)
        {
            hudController.UpdateMoveCount(moveCount);
            hudController.UpdateLevelName(levelData.levelName);
        }

        if (winScreenController != null)
        {
            winScreenController.Hide();
        }
    }

    // ──────────────────────────────────────────────
    //  INPUT HANDLING
    // ──────────────────────────────────────────────

    /// <summary>
    /// Called when WASD/arrows are pressed (New Input System callback).
    /// Reads the Vector2 direction and attempts to slide all blocks.
    /// </summary>
    private void OnMoveInput(InputAction.CallbackContext context)
    {
        if (CurrentState != GameState.Playing) return;

        Vector2 raw = context.ReadValue<Vector2>();

        // Convert to cardinal direction (no diagonals)
        Vector2Int direction = Vector2Int.zero;
        if (Mathf.Abs(raw.x) > Mathf.Abs(raw.y))
            direction = raw.x > 0 ? Vector2Int.right : Vector2Int.left;
        else if (Mathf.Abs(raw.y) > 0)
            direction = raw.y > 0 ? Vector2Int.up : Vector2Int.down;

        if (direction != Vector2Int.zero)
            TrySlideAllBlocks(direction);
    }

    /// <summary>
    /// Called when a swipe is detected on mobile.
    /// SwipeDetector already provides a clean Vector2Int direction.
    /// </summary>
    private void OnSwipeInput(Vector2Int direction)
    {
        if (CurrentState != GameState.Playing) return;
        TrySlideAllBlocks(direction);
    }

    /// <summary>Called when Undo key (Z) is pressed.</summary>
    private void OnUndoInput(InputAction.CallbackContext context)
    {
        if (CurrentState != GameState.Playing) return;
        UndoLastMove();
    }

    /// <summary>Called when Restart key (R) is pressed.</summary>
    private void OnRestartInput(InputAction.CallbackContext context)
    {
        if (CurrentState == GameState.Playing || CurrentState == GameState.Won)
        {
            RestartLevel();
        }
    }

    // ──────────────────────────────────────────────
    //  CORE GAME LOGIC
    // ──────────────────────────────────────────────

    /// <summary>
    /// Attempts to slide all 4 blocks in the given direction.
    ///
    /// HOW IT WORKS:
    ///   1. Check no blocks are currently sliding (prevent input spam)
    ///   2. Save current positions to undo stack
    ///   3. Calculate where EACH block will end up (using GridSystem)
    ///   4. Tell each block to slide to its destination
    ///
    /// BLOCK PROCESSING ORDER:
    ///   Blocks closer to the wall in the slide direction are processed first.
    ///   This is critical! If sliding RIGHT, the rightmost block must be
    ///   calculated first because it stops at the wall, then the next block
    ///   stops against IT, and so on.
    /// </summary>
    private void TrySlideAllBlocks(Vector2Int direction)
    {
        // Don't accept input while blocks are still moving
        if (slidingBlockCount > 0) return;
        if (blocks == null || blocks.Length == 0) return;

        // Save current positions for undo BEFORE moving
        SaveUndoSnapshot();

        // Sort blocks by processing order based on slide direction
        // (nearest to the target wall = processed first)
        Block[] sortedBlocks = new Block[blocks.Length];
        System.Array.Copy(blocks, sortedBlocks, blocks.Length);
        SortBlocksByDirection(sortedBlocks, direction);

        // Calculate destinations for ALL blocks
        // We track already-assigned destinations so blocks can stop against each other
        Vector2Int[] assignedDestinations = new Vector2Int[0];
        bool anyBlockMoved = false;

        for (int i = 0; i < sortedBlocks.Length; i++)
        {
            Vector2Int start = sortedBlocks[i].GridPosition;
            Vector2Int dest = gridSystem.GetSlideDestination(start, direction, assignedDestinations);

            // Set this block's destination
            sortedBlocks[i].SetSlideTarget(dest, gridSystem);

            if (dest != start) anyBlockMoved = true;

            // Add this destination to the list for subsequent blocks to check against
            Vector2Int[] newAssigned = new Vector2Int[assignedDestinations.Length + 1];
            assignedDestinations.CopyTo(newAssigned, 0);
            newAssigned[assignedDestinations.Length] = dest;
            assignedDestinations = newAssigned;
        }

        if (!anyBlockMoved)
        {
            // No block would move — remove the undo snapshot we just saved
            undoStack.Pop();
            return;
        }

        // Start ALL blocks sliding
        slidingBlockCount = 0;
        foreach (Block block in blocks)
        {
            if (block.HasTarget)
            {
                slidingBlockCount++;
                block.StartSliding();
            }
        }

        // If somehow no blocks are actually sliding, check win immediately
        if (slidingBlockCount == 0)
        {
            moveCount++;
            if (hudController != null) hudController.UpdateMoveCount(moveCount);
            CheckWinCondition();
        }
    }

    /// <summary>
    /// Sorts blocks so the ones closest to the wall in the slide direction
    /// are processed first.
    ///
    /// Example: Sliding RIGHT with blocks at x=1, x=3, x=2, x=4
    ///   Sorted order: x=4, x=3, x=2, x=1 (rightmost first)
    ///   Block at x=4 hits the wall → stops at x=5
    ///   Block at x=3 hits block at x=5 → stops at x=4
    ///   ...and so on
    /// </summary>
    private void SortBlocksByDirection(Block[] blockArray, Vector2Int direction)
    {
        System.Array.Sort(blockArray, (a, b) =>
        {
            if (direction.x != 0)
            {
                // Horizontal slide: sort by x
                // RIGHT (+x): process highest x first (descending)
                // LEFT  (-x): process lowest x first (ascending)
                return direction.x > 0
                    ? b.GridPosition.x.CompareTo(a.GridPosition.x)
                    : a.GridPosition.x.CompareTo(b.GridPosition.x);
            }
            else
            {
                // Vertical slide: sort by y
                // UP   (+y): process highest y first (descending)
                // DOWN (-y): process lowest y first (ascending)
                return direction.y > 0
                    ? b.GridPosition.y.CompareTo(a.GridPosition.y)
                    : a.GridPosition.y.CompareTo(b.GridPosition.y);
            }
        });
    }

    /// <summary>
    /// Called by each Block when it finishes sliding.
    /// When ALL 4 blocks have stopped, we increment the move counter
    /// and check the win condition.
    /// </summary>
    private void OnBlockStopped()
    {
        slidingBlockCount--;

        if (slidingBlockCount <= 0)
        {
            slidingBlockCount = 0;

            // All blocks have stopped — count the move
            moveCount++;
            if (hudController != null) hudController.UpdateMoveCount(moveCount);

            // Check if all blocks are on their targets
            CheckWinCondition();
        }
    }

    /// <summary>
    /// Checks if every block is sitting on its appropriate target position.
    /// Depending on LevelData.requireExactTargetMatch, this either requires
    /// Block 0 on Target 0 or just any block on any target.
    /// </summary>
    private void CheckWinCondition()
    {
        if (levelManager == null) return;

        Vector2Int[] targets = levelManager.GetTargetGridPositions();
        if (targets == null || targets.Length == 0) return;

        bool requireExact = false;
        if (levelManager.CurrentLevelData != null)
        {
            requireExact = levelManager.CurrentLevelData.requireExactTargetMatch;
        }

        if (requireExact)
        {
            // Each block must be on its exact corresponding target
            for (int i = 0; i < blocks.Length; i++)
            {
                if (blocks[i].GridPosition != targets[i])
                {
                    return; // At least one block is not on its specific target
                }
            }
        }
        else
        {
            // Any block can be on any target
            for (int i = 0; i < blocks.Length; i++)
            {
                bool isOnAnyTarget = false;
                for (int j = 0; j < targets.Length; j++)
                {
                    if (blocks[i].GridPosition == targets[j])
                    {
                        isOnAnyTarget = true;
                        break;
                    }
                }

                if (!isOnAnyTarget)
                {
                    return; // At least one block is not on any target
                }
            }
        }

        // All blocks on targets — PLAYER WINS!
        CurrentState = GameState.Won;
        Debug.Log("🎉 Level Complete in " + moveCount + " moves!");

        // Save progress
        LevelProgressManager.CompleteLevel(currentLevelIndex);

        if (winScreenController != null)
        {
            winScreenController.Show(moveCount);
        }
    }

    // ──────────────────────────────────────────────
    //  UNDO SYSTEM
    // ──────────────────────────────────────────────

    /// <summary>
    /// Saves a snapshot of all block positions to the undo stack.
    /// Called BEFORE each move so we can restore if the player presses Undo.
    /// </summary>
    private void SaveUndoSnapshot()
    {
        Vector2Int[] snapshot = new Vector2Int[blocks.Length];
        for (int i = 0; i < blocks.Length; i++)
        {
            snapshot[i] = blocks[i].GridPosition;
        }
        undoStack.Push(snapshot);
    }

    /// <summary>
    /// Restores all blocks to their previous positions (pops from undo stack).
    /// </summary>
    private void UndoLastMove()
    {
        if (undoStack.Count == 0) return;
        if (slidingBlockCount > 0) return; // Can't undo while blocks are moving

        Vector2Int[] snapshot = undoStack.Pop();
        for (int i = 0; i < blocks.Length; i++)
        {
            blocks[i].TeleportToGrid(snapshot[i], gridSystem);
        }

        moveCount = Mathf.Max(0, moveCount - 1);
        if (hudController != null) hudController.UpdateMoveCount(moveCount);
    }

    // ──────────────────────────────────────────────
    //  RESTART
    // ──────────────────────────────────────────────

    /// <summary>
    /// Reloads the current level from scratch.
    /// </summary>
    public void RestartLevel()
    {
        if (levelManager.CurrentLevelData != null)
        {
            // Unsubscribe from old blocks before reloading
            if (blocks != null)
            {
                foreach (Block block in blocks)
                {
                    block.OnStopped -= OnBlockStopped;
                }
            }

            LoadLevel(levelManager.CurrentLevelData);
        }
    }

    /// <summary>
    /// Loads the next level. Call this from the WinScreen "Next Level" button.
    /// </summary>
    public void LoadNextLevel()
    {
        if (levelRegistry != null && levelRegistry.levels.Length > 0
            && currentLevelIndex < levelRegistry.levels.Length - 1)
        {
            currentLevelIndex++;

            // Unsubscribe from old blocks
            if (blocks != null)
            {
                foreach (Block block in blocks)
                {
                    block.OnStopped -= OnBlockStopped;
                }
            }

            LoadLevel(levelRegistry.levels[currentLevelIndex]);
        }
        else
        {
            Debug.Log("You beat all levels! Returning to level select.");
            GoToLevelSelect();
        }
    }

    /// <summary>
    /// Returns to the Level Select scene.
    /// </summary>
    public void GoToLevelSelect()
    {
        SceneManager.LoadScene("LevelSelect");
    }
}
