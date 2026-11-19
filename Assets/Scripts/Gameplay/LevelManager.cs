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

    [Header("Board & Obstacle Visibility (Toggle in Inspector)")]
    [Tooltip("Show or hide the frosted glass plate slab")]
    public bool showFrostedGlassBoard = false;

    [Tooltip("Show or hide the soft drop shadow beneath the board slab")]
    public bool showBoardShadow = false;

    [Tooltip("Show or hide the grid cell center dots")]
    public bool showGridCellFrames = true;

    [Tooltip("Show or hide the rounded frame around target star destination cells")]
    public bool showTargetCellFrame = true;

    [Tooltip("Color and brightness/opacity of the grid cell dots and wall cross marks")]
    public Color gridCellColor = new Color(1f, 1f, 1f, 0.85f);

    [Tooltip("Show or hide interior obstacle wall visuals and shadows (mechanics still block movement regardless)")]
    public bool showWallBlocks = false;

    [Tooltip("Show a smooth white cross mark (no frame) on independent/floating walls not connected to the outer grid border")]
    public bool showIndependentWallCross = true;

    [Tooltip("Show a continuous border outlining the entire playable area and wrapping around inner walls")]
    public bool showPlayableAreaBorder = true;

    [Tooltip("Color and opacity of the playable area / inner wall border")]
    public Color playableBorderColor = new Color(1f, 1f, 1f, 0.95f);

    [Tooltip("Thickness of the playable area border in Unity grid units")]
    [Range(0.02f, 0.14f)]
    public float playableBorderThickness = 0.065f;

    [Tooltip("Corner radius of the playable area / inner wall border in Unity grid units (0 = sharp, 0.14 = smooth small radius)")]
    [Range(0f, 0.35f)]
    public float playableBorderCornerRadius = 0.14f;

    [Header("Board & 3D Glass Styling")]
    [Tooltip("Color of the raised glass puzzle slab")]
    public Color boardGlassColor = new Color(0.42f, 0.91f, 0.83f, 0.75f);

    [Tooltip("Color of the board slab drop shadow cast onto the background")]
    public Color boardShadowColor = new Color(0f, 0.18f, 0.20f, 0.30f);

    [Header("Block & Wall Shadows")]
    [Tooltip("Color of the visible 3D drop shadow beneath blocks (matches reference image)")]
    public Color blockShadowColor = new Color(0f, 0.05f, 0.10f, 0.25f);

    [Tooltip("Offset of the shadow to the bottom-right (X, Y)")]
    public Vector2 blockShadowOffset = new Vector2(0.08f, -0.09f);

    [Header("Shadow Sprites (Auto-loaded from Assets/Images)")]
    [Tooltip("Soft Gaussian-blurred drop shadow for blocks")]
    public Sprite blockShadowSprite;

    [Tooltip("Soft ambient drop shadow for the board slab")]
    public Sprite boardShadowSprite;

    [Tooltip("9-sliced frosted glass board slab sprite")]
    public Sprite frostedGlassBoardSprite;

    [Tooltip("Thin etched cell frame sprite for un-targeted grid lines (Frame + Dot)")]
    public Sprite gridCellFrameSprite;

    [Tooltip("Thin etched cell frame sprite for target destination cells (Frame only)")]
    public Sprite targetCellFrameSprite;

    [Tooltip("Center dot sprite for grid cells")]
    public Sprite gridCellDotSprite;

    [Tooltip("Golden star or frosted recessed dish sprite for target destinations")]
    public Sprite targetDishSprite;

    [Tooltip("Smooth white cross mark sprite for independent/floating walls (not touching outer grid border)")]
    public Sprite independentWallCrossSprite;

    [Header("Acrylic Gem Sprites (Frosted / Monument Valley Style)")]
    public Sprite greenGemSprite;
    public Sprite redGemSprite;
    public Sprite blueGemSprite;
    public Sprite yellowGemSprite;
    public Sprite wallGemSprite;

    // ──────────────────────────────────────────────
    //  RUNTIME STATE
    // ──────────────────────────────────────────────

    public LevelData CurrentLevelData { get; private set; }

    // Keep track of spawned objects so we can destroy them on reload
    private List<GameObject> spawnedBoardElements = new List<GameObject>();
    private List<GameObject> spawnedWalls = new List<GameObject>();
    private List<GameObject> spawnedTargets = new List<GameObject>();
    private Block[] spawnedBlocks = new Block[4];
    private Sprite runtimePlayableBorderSprite;
    private Texture2D runtimePlayableBorderTexture;

    // ──────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ──────────────────────────────────────────────

    private void Awake()
    {
        // Clean up any leftover editor preview objects or duplicate objects
        CleanAllOrphanedObjects();
    }

    private void OnDestroy()
    {
        ReleaseRuntimeBorderResources();
    }

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
        SpawnBoardBackground(levelData, gridSystem);
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
        Vector2Int[] gridTargets = new Vector2Int[4];
        for (int i = 0; i < 4; i++)
        {
            gridTargets[i] = new Vector2Int(
                CurrentLevelData.blockTargetPositions[i].x,
                CurrentLevelData.blockTargetPositions[i].y
            );
        }
        return gridTargets;
    }

    /// <summary>
    /// Returns all wall positions that are "independent" (floating) — i.e., neither touching
    /// the outer border of the grid nor orthogonally connected (up/down/left/right) to any
    /// wall group that touches the outer border.
    /// </summary>
    public static HashSet<Vector2Int> GetIndependentWallPositions(LevelData level)
    {
        HashSet<Vector2Int> independentWalls = new HashSet<Vector2Int>();
        if (level == null || level.wallPositions == null || level.wallPositions.Length == 0)
            return independentWalls;

        int w = level.gridWidth;
        int h = level.gridHeight;
        if (w <= 0 || h <= 0) return independentWalls;

        HashSet<Vector2Int> allWalls = new HashSet<Vector2Int>();
        foreach (Vector2Int p in level.wallPositions)
        {
            if (p.x >= 0 && p.x < w && p.y >= 0 && p.y < h)
            {
                allWalls.Add(p);
            }
        }

        // 4-directional BFS flood-fill starting from every wall touching the grid's outer edge
        HashSet<Vector2Int> outlineWalls = new HashSet<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        foreach (Vector2Int wall in allWalls)
        {
            if (wall.x == 0 || wall.x == w - 1 || wall.y == 0 || wall.y == h - 1)
            {
                if (outlineWalls.Add(wall))
                {
                    queue.Enqueue(wall);
                }
            }
        }

        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            for (int i = 0; i < dirs.Length; i++)
            {
                Vector2Int neighbor = current + dirs[i];
                if (allWalls.Contains(neighbor) && outlineWalls.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        // Any wall not reached from the outer boundary is an independent/floating wall
        foreach (Vector2Int wall in allWalls)
        {
            if (!outlineWalls.Contains(wall))
            {
                independentWalls.Add(wall);
            }
        }

        return independentWalls;
    }

    // ──────────────────────────────────────────────
    //  PRIVATE SPAWNING LOGIC
    // ──────────────────────────────────────────────

    /// <summary>
    /// Destroys all spawned board elements, walls, and blocks to prep for a new level.
    /// Also clears any leftover preview or orphaned scene objects.
    /// </summary>
    public void ClearLevel()
    {
        ReleaseRuntimeBorderResources();
        CleanAllOrphanedObjects();
    }

    private void ReleaseRuntimeBorderResources()
    {
        if (runtimePlayableBorderSprite != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(runtimePlayableBorderSprite);
            else Destroy(runtimePlayableBorderSprite);
#else
            Destroy(runtimePlayableBorderSprite);
#endif
            runtimePlayableBorderSprite = null;
        }

        if (runtimePlayableBorderTexture != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(runtimePlayableBorderTexture);
            else Destroy(runtimePlayableBorderTexture);
#else
            Destroy(runtimePlayableBorderTexture);
#endif
            runtimePlayableBorderTexture = null;
        }
    }

    private void CleanAllOrphanedObjects()
    {
        // 1. Destroy tracked objects
        for (int i = spawnedBoardElements.Count - 1; i >= 0; i--)
        {
            if (spawnedBoardElements[i] != null) SafeDestroy(spawnedBoardElements[i]);
        }
        spawnedBoardElements.Clear();

        for (int i = spawnedWalls.Count - 1; i >= 0; i--)
        {
            if (spawnedWalls[i] != null) SafeDestroy(spawnedWalls[i]);
        }
        spawnedWalls.Clear();

        for (int i = spawnedTargets.Count - 1; i >= 0; i--)
        {
            if (spawnedTargets[i] != null) SafeDestroy(spawnedTargets[i]);
        }
        spawnedTargets.Clear();

        for (int i = 0; i < 4; i++)
        {
            if (spawnedBlocks[i] != null)
            {
                SafeDestroy(spawnedBlocks[i].gameObject);
                spawnedBlocks[i] = null;
            }
        }

        // 2. Destroy any children parented under LevelManager
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null)
            {
                SafeDestroy(child.gameObject);
            }
        }

        // 3. Search scene for any stray/leftover board or target elements
        if (gameObject.scene.isLoaded)
        {
            GameObject[] rootObjects = gameObject.scene.GetRootGameObjects();
            foreach (GameObject go in rootObjects)
            {
                if (go == null || go == gameObject) continue;
                string n = go.name;
                if (n == "PuzzleBoard" || n.StartsWith("Target_") || n.StartsWith("TargetCellFrame_") || n.StartsWith("Block_") || n.StartsWith("Wall_") || n.StartsWith("WallCross_") || n.StartsWith("GridCell_"))
                {
                    SafeDestroy(go);
                }
            }
        }
    }

    private static void SafeDestroy(GameObject obj)
    {
        if (obj == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(obj);
        else
            Destroy(obj);
#else
        Destroy(obj);
#endif
    }

    /// <summary>
    /// Spawns the raised frosted glass puzzle slab, glass bevel rim, ambient drop shadow,
    /// and etched cell grid frames matching the reference glass design.
    /// </summary>
    private void SpawnBoardBackground(LevelData level, GridSystem grid)
    {
        // Container for puzzle area visuals
        GameObject boardObj = new GameObject("PuzzleBoard");
        boardObj.transform.SetParent(transform, false);
        boardObj.transform.position = Vector3.zero;

        // 1. Board Ambient Drop Shadow (large soft shadow cast by the entire glass slab)
        GameObject boardShadow = new GameObject("BoardShadow");
        boardShadow.transform.SetParent(boardObj.transform, false);
        boardShadow.transform.localPosition = new Vector3(0.15f, -0.18f, 0f);
        boardShadow.transform.localScale = new Vector3(level.gridWidth + 1.15f, level.gridHeight + 1.15f, 1f);

        SpriteRenderer boardShadowSr = boardShadow.AddComponent<SpriteRenderer>();
        boardShadowSr.sprite = GetBoardShadowSprite();
        boardShadowSr.color = new Color(0f, 0.15f, 0.20f, 0.26f);
        boardShadowSr.sortingOrder = -9;
        boardShadow.SetActive(showBoardShadow);

        // 2. Raised Frosted Glass Slab (smooth rounded slab with bevel & rim highlights)
        GameObject plateObj = new GameObject("GlassPlate");
        plateObj.transform.SetParent(boardObj.transform, false);
        plateObj.transform.localPosition = Vector3.zero;

        SpriteRenderer plateSr = plateObj.AddComponent<SpriteRenderer>();
        plateSr.sprite = GetFrostedGlassBoardSprite();
        plateSr.drawMode = SpriteDrawMode.Simple;

        // Size board to encompass grid with 0.35 unit margin on each side
        float boardPadding = 0.70f;
        float boardSizeX = level.gridWidth + boardPadding;
        float boardSizeY = level.gridHeight + boardPadding;
        float spriteNativeUnits = plateSr.sprite != null ? plateSr.sprite.rect.width / plateSr.sprite.pixelsPerUnit : 5.12f;
        plateObj.transform.localScale = new Vector3(boardSizeX / spriteNativeUnits, boardSizeY / spriteNativeUnits, 1f);
        plateSr.color = Color.white;
        plateSr.sortingOrder = -8;
        plateObj.SetActive(showFrostedGlassBoard);

        // 3. Grid Cell Dots & Target Star Cell Frames:
        //    - Regular playable cells: center dot only (no frame)
        //    - Target star cells: rounded target frame (no dot; destination star sits inside)
        Sprite cellDotSprite = GetGridCellDotSprite();
        Sprite targetFrameSprite = GetTargetCellFrameSprite();

        if (cellDotSprite != null || targetFrameSprite != null)
        {
            for (int y = 0; y < level.gridHeight; y++)
            {
                for (int x = 0; x < level.gridWidth; x++)
                {
                    Vector2Int pos = new Vector2Int(x, y);

                    // Skip wall positions
                    if ((grid != null && grid.IsWall(pos)) || 
                        (level.wallPositions != null && System.Array.Exists(level.wallPositions, p => p == pos)))
                    {
                        continue;
                    }

                    bool isTarget = level.blockTargetPositions != null && System.Array.Exists(level.blockTargetPositions, p => p == pos);
                    Vector3 cellPos = grid.GridToWorld(pos);

                    if (isTarget)
                    {
                        if (targetFrameSprite != null)
                        {
                            GameObject frameObj = new GameObject($"TargetCellFrame_{x}_{y}");
                            frameObj.transform.SetParent(boardObj.transform, false);
                            frameObj.transform.position = cellPos;
                            frameObj.transform.localScale = Vector3.one;

                            SpriteRenderer frameSr = frameObj.AddComponent<SpriteRenderer>();
                            frameSr.sprite = targetFrameSprite;
                            frameSr.color = gridCellColor;
                            frameSr.sortingOrder = -7;
                            frameObj.SetActive(showTargetCellFrame);
                        }
                    }
                    else if (cellDotSprite != null)
                    {
                        GameObject cellObj = new GameObject($"GridCell_{x}_{y}");
                        cellObj.transform.SetParent(boardObj.transform, false);
                        cellObj.transform.position = cellPos;
                        cellObj.transform.localScale = Vector3.one;

                        SpriteRenderer cellSr = cellObj.AddComponent<SpriteRenderer>();
                        cellSr.sprite = cellDotSprite;
                        cellSr.color = gridCellColor;
                        cellSr.sortingOrder = -7;
                        cellObj.SetActive(showGridCellFrames);
                    }
                }
            }
        }

        // 4. Independent / Floating Walls: walls (or wall groups) not connected to the outer grid border get a smooth white cross mark (no frame)
        HashSet<Vector2Int> independentWalls = GetIndependentWallPositions(level);
        if (independentWalls.Count > 0)
        {
            Sprite crossSprite = GetIndependentWallCrossSprite();
            if (crossSprite != null)
            {
                foreach (Vector2Int wallPos in independentWalls)
                {
                    Vector3 worldPos = grid.GridToWorld(wallPos);
                    GameObject crossObj = new GameObject($"WallCross_{wallPos.x}_{wallPos.y}");
                    crossObj.transform.SetParent(boardObj.transform, false);
                    crossObj.transform.position = worldPos;
                    crossObj.transform.localScale = Vector3.one;

                    SpriteRenderer crossSr = crossObj.AddComponent<SpriteRenderer>();
                    crossSr.sprite = crossSprite;
                    crossSr.color = gridCellColor;
                    crossSr.sortingOrder = -7;
                    crossObj.SetActive(showIndependentWallCross);
                }
            }
        }

        // 5. Continuous Playable Area Border (outlines outer playable boundary + wraps around inner/floating walls)
        Sprite borderSprite = CreatePlayableAreaBorderSprite(level, grid);
        if (borderSprite != null)
        {
            GameObject borderObj = new GameObject("PlayableAreaBorder");
            borderObj.transform.SetParent(boardObj.transform, false);
            borderObj.transform.localPosition = Vector3.zero;
            borderObj.transform.localScale = Vector3.one;

            SpriteRenderer borderSr = borderObj.AddComponent<SpriteRenderer>();
            borderSr.sprite = borderSprite;
            borderSr.color = playableBorderColor;
            borderSr.sortingOrder = -6; // Above grid frames (-7), below targets (-5) and blocks (0)
            borderObj.SetActive(showPlayableAreaBorder);
        }

        spawnedBoardElements.Add(boardObj);
    }

    /// <summary>
    /// Generates a crisp, anti-aliased continuous border tracing every edge between a
    /// playable cell and a non-playable boundary or wall (both outer cutouts and inner/floating walls).
    /// </summary>
    private Sprite CreatePlayableAreaBorderSprite(LevelData level, GridSystem grid)
    {
        if (level == null || level.gridWidth <= 0 || level.gridHeight <= 0) return null;

        int w = level.gridWidth;
        int h = level.gridHeight;

        bool[,] playable = new bool[w, h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                bool isWall = (grid != null && grid.IsWall(pos)) ||
                              (level.wallPositions != null && System.Array.Exists(level.wallPositions, p => p == pos));
                playable[x, y] = !isWall;
            }
        }

        // Build horizontal and vertical boundary edge maps
        bool[,] hEdge = new bool[w, h + 1];
        for (int y = 0; y <= h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool above = y < h && playable[x, y];
                bool below = y > 0 && playable[x, y - 1];
                hEdge[x, y] = above != below;
            }
        }

        bool[,] vEdge = new bool[w + 1, h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x <= w; x++)
            {
                bool right = x < w && playable[x, y];
                bool left = x > 0 && playable[x - 1, y];
                vEdge[x, y] = right != left;
            }
        }

        // Detect 90-degree L-corners at every grid vertex (vx, vy)
        // cornerSx / cornerSy = +1 or -1 pointing toward the arc center from (vx, vy); 0 = not a 90-degree corner
        float cornerR = Mathf.Clamp(playableBorderCornerRadius, 0f, 0.35f);
        sbyte[,] cornerSx = new sbyte[w + 1, h + 1];
        sbyte[,] cornerSy = new sbyte[w + 1, h + 1];

        if (cornerR > 0.001f)
        {
            for (int vy = 0; vy <= h; vy++)
            {
                for (int vx = 0; vx <= w; vx++)
                {
                    bool eRight = vx < w && hEdge[vx, vy];
                    bool eLeft = vx > 0 && hEdge[vx - 1, vy];
                    bool eUp = vy < h && vEdge[vx, vy];
                    bool eDown = vy > 0 && vEdge[vx, vy - 1];

                    // Pure 90-degree corner (exactly one horizontal edge and one vertical edge)
                    if ((eRight ^ eLeft) && (eUp ^ eDown))
                    {
                        cornerSx[vx, vy] = (sbyte)(eRight ? 1 : -1);
                        cornerSy[vx, vy] = (sbyte)(eUp ? 1 : -1);
                    }
                }
            }
        }

        const int ppu = 96;
        const int pad = 12;
        int texW = w * ppu + pad * 2;
        int texH = h * ppu + pad * 2;

        Texture2D tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[texW * texH];
        // Pre-fill RGB with white (alpha 0) so bilinear filtering never creates dark grey edge halos
        Color32 clearWhite = new Color32(255, 255, 255, 0);
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clearWhite;

        float halfThickness = Mathf.Clamp(playableBorderThickness, 0.02f, 0.14f) * 0.5f;
        float aa = 1.15f / ppu;
        float reach = halfThickness + aa;
        float reachSq = reach * reach;
        float arcReach = cornerR + reach;

        for (int py = 0; py < texH; py++)
        {
            float gy = (py + 0.5f - pad) / ppu;
            int cy = Mathf.FloorToInt(gy);
            float fy = gy - cy;
            bool nearYLine = fy <= reach || fy >= 1f - reach;
            bool nearYCorner = fy <= arcReach || fy >= 1f - arcReach;

            int rowOffset = py * texW;

            for (int px = 0; px < texW; px++)
            {
                float gx = (px + 0.5f - pad) / ppu;
                int cx = Mathf.FloorToInt(gx);
                float fx = gx - cx;

                // Fast skip for pixels deep inside any cell away from both edges and rounded corners
                bool nearXLine = fx <= reach || fx >= 1f - reach;
                bool nearXCorner = fx <= arcReach || fx >= 1f - arcReach;
                if (!nearYLine && !nearXLine && !(nearXCorner && nearYCorner))
                {
                    continue;
                }

                float minDistSq = reachSq + 1f;

                // Check nearby horizontal edges (trimmed by cornerR at 90-degree corners)
                int exMin = Mathf.Max(0, cx - 1);
                int exMax = Mathf.Min(w - 1, cx + 1);
                int eyMin = Mathf.Max(0, cy);
                int eyMax = Mathf.Min(h, cy + 1);

                for (int ey = eyMin; ey <= eyMax; ey++)
                {
                    float dy = gy - ey;
                    float dySq = dy * dy;
                    if (dySq >= minDistSq) continue;

                    for (int ex = exMin; ex <= exMax; ex++)
                    {
                        if (!hEdge[ex, ey]) continue;
                        float x0 = ex + (cornerSx[ex, ey] != 0 ? cornerR : 0f);
                        float x1 = (ex + 1) - (cornerSx[ex + 1, ey] != 0 ? cornerR : 0f);
                        float dx = gx < x0 ? (x0 - gx) : (gx > x1 ? (gx - x1) : 0f);
                        float dSq = dx * dx + dySq;
                        if (dSq < minDistSq) minDistSq = dSq;
                    }
                }

                // Check nearby vertical edges (trimmed by cornerR at 90-degree corners)
                int vxMin = Mathf.Max(0, cx);
                int vxMax = Mathf.Min(w, cx + 1);
                int vyMin = Mathf.Max(0, cy - 1);
                int vyMax = Mathf.Min(h - 1, cy + 1);

                for (int vx = vxMin; vx <= vxMax; vx++)
                {
                    float dx = gx - vx;
                    float dxSq = dx * dx;
                    if (dxSq >= minDistSq) continue;

                    for (int vy = vyMin; vy <= vyMax; vy++)
                    {
                        if (!vEdge[vx, vy]) continue;
                        float y0 = vy + (cornerSy[vx, vy] != 0 ? cornerR : 0f);
                        float y1 = (vy + 1) - (cornerSy[vx, vy + 1] != 0 ? cornerR : 0f);
                        float dy = gy < y0 ? (y0 - gy) : (gy > y1 ? (gy - y1) : 0f);
                        float dSq = dxSq + dy * dy;
                        if (dSq < minDistSq) minDistSq = dSq;
                    }
                }

                // Check nearby 90-degree rounded corner quarter-circle arcs
                if (cornerR > 0.001f && nearXCorner && nearYCorner)
                {
                    int cvxMin = Mathf.Max(0, cx);
                    int cvxMax = Mathf.Min(w, cx + 1);
                    int cvyMin = Mathf.Max(0, cy);
                    int cvyMax = Mathf.Min(h, cy + 1);

                    for (int cvy = cvyMin; cvy <= cvyMax; cvy++)
                    {
                        for (int cvx = cvxMin; cvx <= cvxMax; cvx++)
                        {
                            sbyte sx = cornerSx[cvx, cvy];
                            if (sx == 0) continue;
                            sbyte sy = cornerSy[cvx, cvy];

                            float arcCx = cvx + sx * cornerR;
                            float arcCy = cvy + sy * cornerR;

                            // Transform into the corner's positive quarter-circle quadrant
                            float ux = (gx - arcCx) * (-sx);
                            float uy = (gy - arcCy) * (-sy);

                            if (ux >= 0f && uy >= 0f)
                            {
                                float radialDist = Mathf.Sqrt(ux * ux + uy * uy) - cornerR;
                                float dSq = radialDist * radialDist;
                                if (dSq < minDistSq) minDistSq = dSq;
                            }
                        }
                    }
                }

                if (minDistSq < reachSq)
                {
                    float dist = Mathf.Sqrt(minDistSq);
                    float alpha = 1f - Mathf.SmoothStep(halfThickness - aa, halfThickness + aa, dist);
                    byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255);
                    pixels[rowOffset + px] = new Color32(255, 255, 255, a);
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        ReleaseRuntimeBorderResources();
        runtimePlayableBorderTexture = tex;
        runtimePlayableBorderSprite = Sprite.Create(
            tex,
            new Rect(0, 0, texW, texH),
            new Vector2(0.5f, 0.5f),
            ppu
        );

        return runtimePlayableBorderSprite;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            foreach (GameObject elem in spawnedBoardElements)
            {
                if (elem != null)
                {
                    Transform plate = elem.transform.Find("GlassPlate");
                    if (plate != null) plate.gameObject.SetActive(showFrostedGlassBoard);

                    Transform shadow = elem.transform.Find("BoardShadow");
                    if (shadow != null) shadow.gameObject.SetActive(showBoardShadow);

                    Transform playableBorder = elem.transform.Find("PlayableAreaBorder");
                    if (playableBorder != null)
                    {
                        playableBorder.gameObject.SetActive(showPlayableAreaBorder);
                        SpriteRenderer bSr = playableBorder.GetComponent<SpriteRenderer>();
                        if (bSr != null) bSr.color = playableBorderColor;
                    }

                    for (int i = 0; i < elem.transform.childCount; i++)
                    {
                        Transform child = elem.transform.GetChild(i);
                        if (child.name.StartsWith("GridCell_"))
                        {
                            child.gameObject.SetActive(showGridCellFrames);
                            SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
                            if (sr != null) sr.color = gridCellColor;
                        }
                        else if (child.name.StartsWith("TargetCellFrame_"))
                        {
                            child.gameObject.SetActive(showTargetCellFrame);
                            SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
                            if (sr != null) sr.color = gridCellColor;
                        }
                        else if (child.name.StartsWith("WallCross_"))
                        {
                            child.gameObject.SetActive(showIndependentWallCross);
                            SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
                            if (sr != null) sr.color = gridCellColor;
                        }
                    }
                }
            }
            foreach (GameObject wall in spawnedWalls)
            {
                if (wall != null) wall.SetActive(showWallBlocks);
            }
        }
    }
#endif

    private void CreateShadeStrip(Transform parent, Sprite sprite, Vector3 localPos, Vector3 scale, Color color, int sortingOrder)
    {
        GameObject strip = new GameObject("GlassHighlight");
        strip.transform.SetParent(parent, false);
        strip.transform.localPosition = localPos;
        strip.transform.localScale = scale;

        SpriteRenderer sr = strip.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;
    }

    /// <summary>
    /// Helper to get a suitable sprite for the board and cells.
    /// Uses block or wall sprite if available.
    /// </summary>
    private Sprite GetDefaultSprite()
    {
        if (wallPrefab != null)
        {
            SpriteRenderer sr = wallPrefab.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null) return sr.sprite;
        }

        if (blockPrefab != null)
        {
            SpriteRenderer sr = blockPrefab.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null) return sr.sprite;
        }

        return null;
    }

    private Sprite GetBlockShadowSprite()
    {
        if (blockShadowSprite != null) return blockShadowSprite;
#if UNITY_EDITOR
        blockShadowSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/BlockShadowSoft.png");
        if (blockShadowSprite != null) return blockShadowSprite;
#endif
        return GetSoftShadowSprite();
    }

    private Sprite GetBoardShadowSprite()
    {
        if (boardShadowSprite != null) return boardShadowSprite;
#if UNITY_EDITOR
        boardShadowSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/BoardShadowSoft.png");
        if (boardShadowSprite != null) return boardShadowSprite;
#endif
        return GetSoftShadowSprite();
    }

    private Sprite GetFrostedGlassBoardSprite()
    {
        if (frostedGlassBoardSprite != null) return frostedGlassBoardSprite;
#if UNITY_EDITOR
        frostedGlassBoardSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/FrostedGlassBoard.png");
        if (frostedGlassBoardSprite != null) return frostedGlassBoardSprite;
#endif
        return GetDefaultSprite();
    }

    private Sprite GetGridCellFrameSprite()
    {
        if (gridCellFrameSprite != null) return gridCellFrameSprite;
#if UNITY_EDITOR
        gridCellFrameSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/GridCellFrame.png");
        if (gridCellFrameSprite != null) return gridCellFrameSprite;
#endif
        return null;
    }

    private Sprite GetTargetCellFrameSprite()
    {
        if (targetCellFrameSprite != null) return targetCellFrameSprite;
#if UNITY_EDITOR
        targetCellFrameSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/GridCellFrame_Target.png");
        if (targetCellFrameSprite != null) return targetCellFrameSprite;
#endif
        return null;
    }

    private static Sprite cachedFallbackDotSprite;

    private Sprite GetGridCellDotSprite()
    {
        if (gridCellDotSprite != null) return gridCellDotSprite;
#if UNITY_EDITOR
        gridCellDotSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/GridCellDot.png");
        if (gridCellDotSprite != null) return gridCellDotSprite;
#endif
        return GetFallbackDotSprite();
    }

    private Sprite GetFallbackDotSprite()
    {
        if (cachedFallbackDotSprite != null) return cachedFallbackDotSprite;

        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[size * size];
        float radius = 0.085f;
        float feather = 0.035f;

        for (int y = 0; y < size; y++)
        {
            float ny = (y + 0.5f) / size - 0.5f;
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size - 0.5f;
                float dist = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = 1f - Mathf.SmoothStep(radius - feather, radius + feather, dist);
                byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        cachedFallbackDotSprite = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );
        return cachedFallbackDotSprite;
    }

    private Sprite GetTargetDishSprite()
    {
        if (targetDishSprite != null) return targetDishSprite;
#if UNITY_EDITOR
        targetDishSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/TargetStar.png");
        if (targetDishSprite != null) return targetDishSprite;
        targetDishSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/TargetFlag.png");
        if (targetDishSprite != null) return targetDishSprite;
        targetDishSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/TargetDishFrosted.png");
        if (targetDishSprite != null) return targetDishSprite;
#endif
        return null;
    }

    private static Sprite cachedFallbackCrossSprite;

    private Sprite GetIndependentWallCrossSprite()
    {
        if (independentWallCrossSprite != null) return independentWallCrossSprite;
#if UNITY_EDITOR
        independentWallCrossSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/WallCrossMark.png");
        if (independentWallCrossSprite != null) return independentWallCrossSprite;
#endif
        return GetFallbackCrossSprite();
    }

    /// <summary>
    /// Procedurally generates a smooth anti-aliased white cross sprite (rounded caps, no frame)
    /// as a runtime fallback if independentWallCrossSprite is not assigned in the Inspector.
    /// </summary>
    private Sprite GetFallbackCrossSprite()
    {
        if (cachedFallbackCrossSprite != null) return cachedFallbackCrossSprite;

        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        float halfArm = 0.23f;       // Cross spans ~46% of cell width/height
        float halfThickness = 0.034f; // ~4.3px radius (~8.6px stroke width)
        float aa = 0.015f;            // Smooth anti-aliasing feather

        Vector2 a1 = new Vector2(-halfArm, -halfArm);
        Vector2 b1 = new Vector2(halfArm, halfArm);
        Vector2 a2 = new Vector2(-halfArm, halfArm);
        Vector2 b2 = new Vector2(halfArm, -halfArm);

        for (int y = 0; y < size; y++)
        {
            float ny = (y + 0.5f) / size - 0.5f;
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size - 0.5f;
                Vector2 p = new Vector2(nx, ny);

                float d1 = DistanceToSegment(p, a1, b1);
                float d2 = DistanceToSegment(p, a2, b2);
                float dist = Mathf.Min(d1, d2);

                float alpha = 1f - Mathf.SmoothStep(halfThickness - aa, halfThickness + aa, dist);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedFallbackCrossSprite = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );

        return cachedFallbackCrossSprite;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq <= 0.000001f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
        Vector2 closest = a + ab * t;
        return Vector2.Distance(p, closest);
    }

    private Sprite GetGemSprite(int blockIndex)
    {
        switch (blockIndex)
        {
            case 0:
                if (greenGemSprite == null) greenGemSprite = LoadSprite("Assets/Images/Block_Green.png");
                return greenGemSprite;
            case 1:
                if (redGemSprite == null) redGemSprite = LoadSprite("Assets/Images/Block_Red.png");
                return redGemSprite;
            case 2:
                if (blueGemSprite == null) blueGemSprite = LoadSprite("Assets/Images/Block_Blue.png");
                return blueGemSprite;
            case 3:
                if (yellowGemSprite == null) yellowGemSprite = LoadSprite("Assets/Images/Block_Yellow.png");
                return yellowGemSprite;
        }
        return null;
    }

    private Sprite GetWallBlockSprite()
    {
        if (wallGemSprite == null) wallGemSprite = LoadSprite("Assets/Images/Block_Wall.png");
        return wallGemSprite;
    }

    private Sprite LoadSprite(string path)
    {
#if UNITY_EDITOR
        Sprite sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sp != null) return sp;

        // Fallback: search all sub-assets if LoadAssetAtPath didn't find the main Sprite
        Object[] all = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
        if (all != null)
        {
            foreach (Object obj in all)
            {
                if (obj is Sprite s) return s;
            }
        }
#endif
        return null;
    }

    private static Sprite cachedSoftShadowSprite;

    /// <summary>
    /// Generates a soft-feathered shadow sprite with defined core and smooth edge blur
    /// matching the 3D block shadows in the reference design.
    /// </summary>
    private Sprite GetSoftShadowSprite()
    {
        if (cachedSoftShadowSprite != null) return cachedSoftShadowSprite;

        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        float radius = 0.20f;   // Small core
        float feather = 0.70f;  // Wide, super soft edge blur

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Coordinates normalized to [-1, 1]
                float nx = (x / (float)(size - 1)) * 2f - 1f;
                float ny = (y / (float)(size - 1)) * 2f - 1f;

                // Distance from rounded box core
                float dx = Mathf.Max(0f, Mathf.Abs(nx) - radius);
                float dy = Mathf.Max(0f, Mathf.Abs(ny) - radius);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Smooth falloff to 0 at the edge
                float alpha = Mathf.Clamp01(1f - (dist / feather));
                alpha = Mathf.SmoothStep(0f, 1f, alpha);

                pixels[y * size + x] = new Color(0f, 0f, 0f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedSoftShadowSprite = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );

        return cachedSoftShadowSprite;
    }

    /// <summary>
    /// Spawns interior obstacle walls defined in the level data.
    /// Boundary walls are not spawned as game objects so the play area feels borderless and clean;
    /// GridSystem already prevents blocks from moving outside the grid boundaries.
    /// </summary>
    private void SpawnWalls(LevelData level, GridSystem grid)
    {
        // Spawn only interior walls defined in LevelData
        if (level.wallPositions != null)
        {
            foreach (Vector2Int playAreaPos in level.wallPositions)
            {
                // Filter out any coordinates outside the playable grid dimensions
                if (playAreaPos.x < 0 || playAreaPos.x >= level.gridWidth ||
                    playAreaPos.y < 0 || playAreaPos.y >= level.gridHeight)
                {
                    continue;
                }

                // Convert relative play-area pos to absolute grid pos
                Vector2Int gridPos = new Vector2Int(playAreaPos.x, playAreaPos.y);
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

        // Set wall sprite to rounded matte dark charcoal block matching reference image
        SpriteRenderer wallSr = wall.GetComponentInChildren<SpriteRenderer>();
        if (wallSr != null)
        {
            Sprite wallSprite = GetWallBlockSprite();
            if (wallSprite != null)
            {
                wallSr.sprite = wallSprite;
                wallSr.color = Color.white;
            }
            else
            {
                wallSr.color = wallColor;
            }
        }

        // Add 3D drop shadow beneath interior wall obstacle
        GameObject wallShadowObj = new GameObject("Shadow");
        wallShadowObj.transform.SetParent(wall.transform, false);
        wallShadowObj.transform.localPosition = new Vector3(blockShadowOffset.x, blockShadowOffset.y, 0f);
        wallShadowObj.transform.localScale = Vector3.one;

        SpriteRenderer wallShadowSr = wallShadowObj.AddComponent<SpriteRenderer>();
        wallShadowSr.sprite = GetBlockShadowSprite();
        wallShadowSr.color = blockShadowColor;
        wallShadowSr.sortingOrder = -2;

        wall.SetActive(showWallBlocks);
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
            // Level data stores positions relative to play area
            // Grid system uses absolute coordinates directly now
            Vector2Int startPos = level.blockStartPositions[i];
            Vector2Int gridPos = new Vector2Int(startPos.x, startPos.y);

            Vector3 worldPos = grid.GridToWorld(gridPos);
            GameObject blockObj = Instantiate(blockPrefab, worldPos, Quaternion.identity, transform);
            blockObj.name = $"Block_0{i + 1}";

            // Setup the Block component
            Block block = blockObj.GetComponent<Block>();
            block.BlockIndex = i;
            
            // Initial snap to ensure it starts exactly on the grid
            block.TeleportToGrid(gridPos, grid);

            // Setup the acrylic gem sprite (glossy rounded acrylic cube)
            SpriteRenderer sr = blockObj.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) 
            {
                Sprite gem = GetGemSprite(i);
                if (gem != null)
                {
                    sr.sprite = gem;
                    sr.color = Color.white;
                }
                else
                {
                    Color c = colors[i];
                    c.a = 1f;
                    sr.color = c;
                }
            }

            // Tinted translucent colored shadow matching reference design - soft, visible & diffused
            Color[] shadowColors = {
                new Color(0.02f, 0.28f, 0.12f, 0.25f), // Green tint
                new Color(0.35f, 0.02f, 0.10f, 0.25f), // Red tint
                new Color(0.02f, 0.15f, 0.40f, 0.25f), // Blue tint
                new Color(0.35f, 0.22f, 0.02f, 0.25f)  // Yellow tint
            };

            // Setup 3D drop shadow for the moving block (moves with block automatically)
            GameObject shadowObj = new GameObject("Shadow");
            shadowObj.transform.SetParent(blockObj.transform, false);
            shadowObj.transform.localPosition = new Vector3(blockShadowOffset.x, blockShadowOffset.y, 0f);
            shadowObj.transform.localScale = Vector3.one;

            SpriteRenderer shadowSr = shadowObj.AddComponent<SpriteRenderer>();
            shadowSr.sprite = GetBlockShadowSprite();
            shadowSr.color = shadowColors[i];
            shadowSr.sortingOrder = -2; // Behind block (0), above targets (-5) and puzzle plate (-9)

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
            Vector2Int gridPos = new Vector2Int(targetPos.x, targetPos.y);
            Vector3 worldPos = grid.GridToWorld(gridPos);

            GameObject targetObj = Instantiate(targetPrefab, worldPos, Quaternion.identity, transform);
            targetObj.name = $"Target_0{i + 1}";

            SpriteRenderer sr = targetObj.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                Sprite dishSprite = GetTargetDishSprite();
                if (dishSprite != null)
                {
                    sr.sprite = dishSprite;
                }

                // Place target visuals slightly behind blocks (-2 is shadow, -5 is target, -7 is grid, -8 is plate)
                sr.sortingOrder = -5;

                if (level.requireExactTargetMatch)
                {
                    // Exact match: matching color, but slightly transparent
                    Color c = exactColors[i];
                    c.a = 0.80f;
                    sr.color = c;
                }
                else
                {
                    // Any block: pure white frosted dish with baked inner depression shading
                    sr.color = Color.white;
                }
            }

            spawnedTargets.Add(targetObj);
        }
    }
}
