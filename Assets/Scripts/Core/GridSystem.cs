using UnityEngine;

/// <summary>
/// Manages the grid coordinate system for the puzzle.
///
/// WHAT THIS DOES:
///   The game world uses a grid where each cell is 1×1 Unity unit.
///   Grid origin (0,0) = bottom-left corner of the play area.
///   This script converts between grid coordinates (Vector2Int like 3,2)
///   and world positions (Vector3 like 3.5, 2.5, 0) — the +0.5 offset
///   centers objects in their cell.
///
/// KEY METHODS:
///   WorldToGrid()           — "what grid cell is this world position in?"
///   GridToWorld()           — "what world position is the center of this cell?"
///   IsWall()                — "is there a wall at this grid cell?"
///   GetSlideDestination()   — "if a block starts here and slides in this
///                              direction, where does it stop?"
///
/// HOW IT'S USED:
///   GameManager calls GetSlideDestination() to figure out where each block
///   should end up BEFORE telling the block to move. The block then just
///   lerps to that world position.
///
/// SETUP:
///   Attach to the same GameObject as GameManager (or any persistent object).
///   GameManager calls SetupGrid() when a level loads.
/// </summary>
public class GridSystem : MonoBehaviour
{
    // Grid dimensions (set when a level loads)
    private int gridWidth;
    private int gridHeight;

    // Tracks which cells are walls — true = wall, false = empty
    // Indexed as [x, y] where (0,0) is bottom-left
    private bool[,] wallGrid;

    /// <summary>
    /// Initializes the grid from a LevelData asset.
    /// Called by LevelManager when loading a level.
    ///
    /// Creates the wall grid including:
    ///   - Boundary walls (the outer ring around the play area)
    ///   - Interior walls (from LevelData.wallPositions)
    /// </summary>
    public void SetupGrid(LevelData levelData)
    {
        gridWidth = levelData.gridWidth;
        gridHeight = levelData.gridHeight;

        // +2 in each dimension to include boundary walls on all sides
        // So a 5×5 play area actually creates a 7×7 grid internally
        // The playable cells are at positions (1,1) through (5,5)
        wallGrid = new bool[gridWidth + 2, gridHeight + 2];

        // Mark boundary walls (top, bottom, left, right edges)
        for (int x = 0; x < gridWidth + 2; x++)
        {
            wallGrid[x, 0] = true;                  // Bottom row
            wallGrid[x, gridHeight + 1] = true;     // Top row
        }
        for (int y = 0; y < gridHeight + 2; y++)
        {
            wallGrid[0, y] = true;                  // Left column
            wallGrid[gridWidth + 1, y] = true;      // Right column
        }

        // Mark interior walls from the level data
        // Interior wall positions in LevelData are in PLAY AREA coords (0-based)
        // so we offset by +1 to account for the boundary wall ring
        if (levelData.wallPositions != null)
        {
            foreach (Vector2Int wallPos in levelData.wallPositions)
            {
                int gx = wallPos.x + 1;
                int gy = wallPos.y + 1;
                if (gx >= 0 && gx < gridWidth + 2 && gy >= 0 && gy < gridHeight + 2)
                {
                    wallGrid[gx, gy] = true;
                }
            }
        }
    }

    /// <summary>
    /// Converts a world position to grid coordinates.
    /// Example: world (1.7, 2.3, 0) → grid (1, 2)
    /// 
    /// Note: Grid coords include the boundary wall ring,
    /// so playable cells start at (1,1).
    /// </summary>
    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        float offsetX = (gridWidth + 2) / 2f;
        float offsetY = (gridHeight + 2) / 2f;

        // Floor to get the cell the position is in
        return new Vector2Int(
            Mathf.FloorToInt(worldPosition.x + offsetX),
            Mathf.FloorToInt(worldPosition.y + offsetY)
        );
    }

    /// <summary>
    /// Converts grid coordinates to the CENTER of that cell in world space.
    /// Example: grid (3, 2) → world (3.5, 2.5, 0)
    /// 
    /// The offset ensures objects are centered in their cell and the grid
    /// is centered around the origin (0,0).
    /// </summary>
    public Vector3 GridToWorld(Vector2Int gridPosition)
    {
        float offsetX = (gridWidth + 2) / 2f;
        float offsetY = (gridHeight + 2) / 2f;

        return new Vector3(
            gridPosition.x + 0.5f - offsetX,
            gridPosition.y + 0.5f - offsetY,
            0f
        );
    }

    /// <summary>
    /// Checks if a grid cell contains a wall.
    /// Returns true for boundary walls AND interior walls.
    /// Returns true for out-of-bounds positions (safety check).
    /// </summary>
    public bool IsWall(Vector2Int gridPosition)
    {
        int x = gridPosition.x;
        int y = gridPosition.y;

        // Out of bounds = treat as wall (safety)
        if (x < 0 || x >= gridWidth + 2 || y < 0 || y >= gridHeight + 2)
            return true;

        return wallGrid[x, y];
    }

    /// <summary>
    /// Calculates where a block would stop if it slides in a direction.
    /// 
    /// HOW IT WORKS:
    ///   Starting from 'start', step one cell at a time in 'direction'.
    ///   Stop when the NEXT cell is a wall or is occupied by another block.
    ///   Return the last valid cell.
    ///
    /// WHY CHECK OTHER BLOCKS:
    ///   When all 4 blocks slide simultaneously, blocks closer to the wall
    ///   in the slide direction stop first. So we process blocks in order
    ///   and each subsequent block can stop against an already-stopped block.
    ///
    /// PARAMETERS:
    ///   start    — current grid position of the block
    ///   direction — slide direction as Vector2Int (e.g., Vector2Int.right)
    ///   occupiedCells — grid positions of blocks that have ALREADY been
    ///                   processed and assigned their final positions
    /// </summary>
    public Vector2Int GetSlideDestination(Vector2Int start, Vector2Int direction,
                                          Vector2Int[] occupiedCells)
    {
        Vector2Int current = start;

        while (true)
        {
            Vector2Int next = current + direction;

            // Stop if next cell is a wall
            if (IsWall(next))
                break;

            // Stop if next cell is occupied by another block
            bool blocked = false;
            if (occupiedCells != null)
            {
                for (int i = 0; i < occupiedCells.Length; i++)
                {
                    if (occupiedCells[i] == next)
                    {
                        blocked = true;
                        break;
                    }
                }
            }
            if (blocked)
                break;

            // Cell is free — move there and keep going
            current = next;
        }

        return current;
    }

    /// <summary>
    /// Returns the grid position that corresponds to a play-area coordinate.
    /// Play area (0,0) = grid (1,1) because of the boundary wall ring.
    /// 
    /// Use this when converting LevelData positions (which are play-area
    /// relative) to actual grid positions.
    /// </summary>
    public Vector2Int PlayAreaToGrid(Vector2Int playAreaPos)
    {
        return new Vector2Int(playAreaPos.x + 1, playAreaPos.y + 1);
    }
}
