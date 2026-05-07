using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles the physical spawning of a level in the Unity scene.
///
/// WHAT THIS DOES:
///   1. Destroys any old walls/blocks from a previous level
///   2. Reads a LevelData ScriptableObject
///   3. Spawns boundary walls around the edge of the grid
///   4. Spawns interior walls from LevelData.wallPositions
///   5. Spawns 4 blocks at LevelData.blockStartPositions
///   6. Assigns colors to blocks (Block 0 = Green, etc.)
///
/// SETUP IN UNITY:
///   1. Create an empty GameObject called "LevelManager"
///   2. Attach this script
///   3. Assign the pre-made Wall prefab to `wallPrefab`
///   4. Assign the pre-made Block prefab to `blockPrefab`
/// </summary>
public class LevelManager : MonoBehaviour
{
    // ──────────────────────────────────────────────
    //  INSPECTOR SETTINGS
    // ──────────────────────────────────────────────

    [Header("Prefabs")]
    [Tooltip("Prefab for a single 1x1 wall section")]
    public GameObject wallPrefab;

    [Tooltip("Prefab for a sliding block")]
    public GameObject blockPrefab;

    [Tooltip("Prefab for a target indicator")]
    public GameObject targetPrefab;

    [Header("Block Colors (Matches v1 Architecture)")]
    public Color block1Color = new Color(0f, 1f, 0.25f, 1f);     // Green
    public Color block2Color = new Color(1f, 0f, 0.21f, 1f);     // Red
    public Color block3Color = new Color(0f, 0.39f, 1f, 1f);     // Blue
    public Color block4Color = new Color(0.99f, 1f, 0f, 1f);     // Yellow
    public Color wallColor = new Color(0.37f, 0.37f, 0.37f, 1f); // Dark Gray

    // ──────────────────────────────────────────────
    //  RUNTIME STATE
    // ──────────────────────────────────────────────

    public LevelData CurrentLevelData { get; private set; }

    // Keep track of spawned objects so we can destroy them on reload
    private List<GameObject> spawnedWalls = new List<GameObject>();
    private List<GameObject> spawnedTargets = new List<GameObject>();
    private Block[] spawnedBlocks = new Block[4];

    // ──────────────────────────────────────────────
    //  PUBLIC METHODS (Called by GameManager)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Loads a level: clears old objects, spawns new walls and blocks.
    /// Called by GameManager when a level starts or restarts.
    /// </summary>
    public void LoadLevel(LevelData levelData, GridSystem gridSystem)
    {
        CurrentLevelData = levelData;

        ClearLevel();
        SpawnWalls(levelData, gridSystem);
        SpawnBlocks(levelData, gridSystem);
        SpawnTargets(levelData, gridSystem);
    }

    /// <summary>
    /// Returns the 4 spawned block components.
    /// GameManager needs these to tell them when to slide.
    /// </summary>
    public Block[] GetBlocks()
    {
        return spawnedBlocks;
    }

    /// <summary>
    /// Returns the 4 target grid positions from the current LevelData.
    /// GameManager needs this to check if the player has won.
    /// </summary>
    public Vector2Int[] GetTargetGridPositions()
    {
        if (CurrentLevelData == null) return null;

        // Convert play-area coordinates from LevelData into actual Grid coordinates
        // (remember: play area is offset by +1 to make room for boundary walls)
        Vector2Int[] gridTargets = new Vector2Int[4];
        for (int i = 0; i < 4; i++)
        {
            gridTargets[i] = new Vector2Int(
                CurrentLevelData.blockTargetPositions[i].x + 1,
                CurrentLevelData.blockTargetPositions[i].y + 1
            );
        }
        return gridTargets;
    }

    // ──────────────────────────────────────────────
    //  PRIVATE SPAWNING LOGIC
    // ──────────────────────────────────────────────

    /// <summary>
    /// Destroys all spawned walls and blocks to prep for a new level.
    /// </summary>
    private void ClearLevel()
    {
        foreach (GameObject wall in spawnedWalls)
        {
            if (wall != null) Destroy(wall);
        }
        spawnedWalls.Clear();

        foreach (GameObject target in spawnedTargets)
        {
            if (target != null) Destroy(target);
        }
        spawnedTargets.Clear();

        for (int i = 0; i < 4; i++)
        {
            if (spawnedBlocks[i] != null)
            {
                Destroy(spawnedBlocks[i].gameObject);
            }
        }
    }

    /// <summary>
    /// Spawns the boundary ring and interior walls.
    /// </summary>
    private void SpawnWalls(LevelData level, GridSystem grid)
    {
        // 1. Spawn boundary walls (the outer edge of the screen)
        // A 5x5 grid means play area is x=1..5, y=1..5
        // Boundary walls are at x=0, x=6 and y=0, y=6
        for (int x = 0; x < level.gridWidth + 2; x++)
        {
            SpawnSingleWall(new Vector2Int(x, 0), grid);                   // Bottom
            SpawnSingleWall(new Vector2Int(x, level.gridHeight + 1), grid); // Top
        }
        for (int y = 1; y <= level.gridHeight; y++)
        {
            SpawnSingleWall(new Vector2Int(0, y), grid);                   // Left
            SpawnSingleWall(new Vector2Int(level.gridWidth + 1, y), grid); // Right
        }

        // 2. Spawn interior walls defined in LevelData
        if (level.wallPositions != null)
        {
            foreach (Vector2Int playAreaPos in level.wallPositions)
            {
                // Convert relative play-area pos (0,0) to absolute grid pos (1,1)
                Vector2Int gridPos = new Vector2Int(playAreaPos.x + 1, playAreaPos.y + 1);
                SpawnSingleWall(gridPos, grid);
            }
        }
    }

    /// <summary>
    /// Instantiates a single wall prefab at the given grid coordinate.
    /// </summary>
    private void SpawnSingleWall(Vector2Int gridPos, GridSystem grid)
    {
        if (wallPrefab == null)
        {
            Debug.LogError("LevelManager: Wall Prefab is not assigned!");
            return;
        }

        Vector3 worldPos = grid.GridToWorld(gridPos);
        GameObject wall = Instantiate(wallPrefab, worldPos, Quaternion.identity, transform);
        wall.name = $"Wall_{gridPos.x}_{gridPos.y}";

        // We no longer override the wall color here, so the prefab's color takes effect.
        // SpriteRenderer sr = wall.GetComponentInChildren<SpriteRenderer>();
        // if (sr != null) sr.color = wallColor;

        spawnedWalls.Add(wall);
    }

    /// <summary>
    /// Spawns the 4 player blocks at their starting positions.
    /// </summary>
    private void SpawnBlocks(LevelData level, GridSystem grid)
    {
        if (blockPrefab == null)
        {
            Debug.LogError("LevelManager: Block Prefab is not assigned!");
            return;
        }

        Color[] colors = { block1Color, block2Color, block3Color, block4Color };

        for (int i = 0; i < 4; i++)
        {
            // Level data stores positions relative to play area (starts at 0,0)
            // Grid system needs absolute coordinates (starts at 1,1 due to boundary walls)
            Vector2Int startPos = level.blockStartPositions[i];
            Vector2Int gridPos = new Vector2Int(startPos.x + 1, startPos.y + 1);

            Vector3 worldPos = grid.GridToWorld(gridPos);
            GameObject blockObj = Instantiate(blockPrefab, worldPos, Quaternion.identity, transform);
            blockObj.name = $"Block_0{i + 1}";

            // Setup the Block component
            Block block = blockObj.GetComponent<Block>();
            block.BlockIndex = i;
            
            // Initial snap to ensure it starts exactly on the grid
            block.TeleportToGrid(gridPos, grid);

            // Color the block
            SpriteRenderer sr = blockObj.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) 
            {
                Color c = colors[i];
                c.a = 1f; // Force 100% opacity to prevent washed-out colors against white background
                sr.color = c;
            }

            spawnedBlocks[i] = block;
        }
    }

    /// <summary>
    /// Spawns un-collidable visual indicators for the target positions.
    /// Colored light gray if any block can go there, or specific lighter colors if exact match is required.
    /// </summary>
    private void SpawnTargets(LevelData level, GridSystem grid)
    {
        if (targetPrefab == null)
        {
            Debug.LogError("LevelManager: Target Prefab is not assigned!");
            return;
        }

        Color[] exactColors = { block1Color, block2Color, block3Color, block4Color };
        
        // Since background is white, use a darker gray for "any target" so it stands out
        Color anyColor = new Color(0.3f, 0.3f, 0.3f, 0.6f); 

        for (int i = 0; i < 4; i++)
        {
            Vector2Int targetPos = level.blockTargetPositions[i];
            Vector2Int gridPos = new Vector2Int(targetPos.x + 1, targetPos.y + 1);
            Vector3 worldPos = grid.GridToWorld(gridPos);

            GameObject targetObj = Instantiate(targetPrefab, worldPos, Quaternion.identity, transform);
            targetObj.name = $"Target_0{i + 1}";

            SpriteRenderer sr = targetObj.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                // Place target visuals slightly behind blocks so blocks render on top
                sr.sortingOrder = -5;

                if (level.requireExactTargetMatch)
                {
                    // Exact match: matching color, but slightly transparent
                    Color c = exactColors[i];
                    c.a = 0.75f; // Increased alpha from 0.35f to 0.75f to be visible on white bg
                    sr.color = c;
                }
                else
                {
                    // Any block: generic gray color
                    sr.color = anyColor;
                }
            }

            spawnedTargets.Add(targetObj);
        }
    }
}
