using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom inspector for LevelData assets with an interactive mini-map editor.
/// Allows painting/erasing walls, placing block start positions (1-4), and
/// placing target destination positions (1-4) directly inside the Inspector.
/// </summary>
[CustomEditor(typeof(LevelData))]
public class LevelDataEditor : Editor
{
    // ──────────────────────────────────────────────
    //  BRUSH MODES & VISUAL CONSTANTS
    // ──────────────────────────────────────────────

    private enum PaintTool
    {
        Wall,
        EraseWall,
        Block1,
        Block2,
        Block3,
        Block4,
        Target1,
        Target2,
        Target3,
        Target4
    }

    private static readonly Color[] BlockColors = new Color[]
    {
        new Color(0.10f, 0.85f, 0.32f, 1f), // 1: Green
        new Color(0.95f, 0.24f, 0.22f, 1f), // 2: Red
        new Color(0.16f, 0.54f, 0.98f, 1f), // 3: Blue
        new Color(0.98f, 0.86f, 0.14f, 1f)  // 4: Yellow
    };

    private static readonly string[] BlockNames = { "Green", "Red", "Blue", "Yellow" };

    private static readonly Color EmptyCellColor = new Color(0.18f, 0.22f, 0.26f, 1f);
    private static readonly Color WallCellColor = new Color(0.42f, 0.44f, 0.48f, 1f);
    private static readonly Color TargetGoldColor = new Color(0.95f, 0.78f, 0.14f, 1f);
    private static readonly Color GridBgColor = new Color(0.09f, 0.12f, 0.15f, 1f);
    private static readonly Color HoverHighlightColor = new Color(1f, 1f, 1f, 0.22f);

    // ──────────────────────────────────────────────
    //  EDITOR STATE
    // ──────────────────────────────────────────────

    private PaintTool activeTool = PaintTool.Wall;
    private bool isDraggingWall;
    private bool dragAddsWall = true;
    private Vector2Int lastDraggedCell = new Vector2Int(-1, -1);
    private Vector2Int hoveredCell = new Vector2Int(-1, -1);
    private string solveStatusMessage;
    private MessageType solveStatusType = MessageType.None;

    // ──────────────────────────────────────────────
    //  INSPECTOR GUI
    // ──────────────────────────────────────────────

    public override void OnInspectorGUI()
    {
        LevelData data = (LevelData)target;
        if (data == null) return;

        // Interactive Mini-Map at the top or right after default fields
        DrawDefaultInspector();

        EditorGUILayout.Space(12);
        DrawInteractiveMiniMap(data);
    }

    private void DrawInteractiveMiniMap(LevelData data)
    {
        if (data.gridWidth <= 0 || data.gridHeight <= 0)
        {
            EditorGUILayout.HelpBox("Set Grid Width and Grid Height above 0 to edit the mini-map.", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("Interactive Level Mini-Map", EditorStyles.boldLabel);

        DrawToolPalette(data);

        EditorGUILayout.Space(6);

        int w = data.gridWidth;
        int h = data.gridHeight;
        float axisMargin = 22f;
        float maxGridWidth = Mathf.Max(160f, EditorGUIUtility.currentViewWidth - 48f - axisMargin);
        float cellSize = Mathf.Clamp(Mathf.Floor(maxGridWidth / w), 22f, 38f);

        float totalW = axisMargin + (w * cellSize);
        float totalH = (h * cellSize) + axisMargin;

        Rect containerRect = GUILayoutUtility.GetRect(totalW, totalH, GUILayout.ExpandWidth(false));
        Rect gridRect = new Rect(containerRect.x + axisMargin, containerRect.y, w * cellSize, h * cellSize);

        // Background frame behind grid
        EditorGUI.DrawRect(gridRect, GridBgColor);

        // Coordinate axis labels (Y on left, X on bottom)
        GUIStyle axisStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.65f, 0.72f, 0.78f, 1f) }
        };

        for (int y = h - 1; y >= 0; y--)
        {
            float ry = gridRect.y + ((h - 1 - y) * cellSize);
            Rect yLabelRect = new Rect(containerRect.x, ry, axisMargin - 2f, cellSize);
            GUI.Label(yLabelRect, y.ToString(), axisStyle);
        }

        for (int x = 0; x < w; x++)
        {
            float rx = gridRect.x + (x * cellSize);
            Rect xLabelRect = new Rect(rx, gridRect.yMax + 2f, cellSize, axisMargin - 2f);
            GUI.Label(xLabelRect, x.ToString(), axisStyle);
        }

        // Handle mouse input on the grid
        HandleGridInput(data, gridRect, w, h, cellSize);

        // Draw every grid cell
        DrawGridCells(data, gridRect, w, h, cellSize);

        // Status & quick actions
        EditorGUILayout.Space(4);
        DrawStatusAndActions(data);
        DrawValidationWarnings(data);
    }

    // ──────────────────────────────────────────────
    //  TOOL PALETTE
    // ──────────────────────────────────────────────

    private void DrawToolPalette(LevelData data)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Row 1: Wall & Erase
        EditorGUILayout.BeginHorizontal();
        DrawToolButton(PaintTool.Wall, "■ Wall (Toggle)", WallCellColor, Color.white);
        DrawToolButton(PaintTool.EraseWall, "✕ Erase Wall", new Color(0.45f, 0.22f, 0.22f, 1f), Color.white);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        // Row 2: Block Start Positions (1-4)
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < 4; i++)
        {
            PaintTool tool = (PaintTool)((int)PaintTool.Block1 + i);
            Color textCol = (i == 3) ? Color.black : Color.white;
            DrawToolButton(tool, $"Start {i + 1}", BlockColors[i], textCol);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        // Row 3: Target Destinations (1-4)
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < 4; i++)
        {
            PaintTool tool = (PaintTool)((int)PaintTool.Target1 + i);
            Color bg = data.requireExactTargetMatch
                ? Color.Lerp(BlockColors[i], TargetGoldColor, 0.35f)
                : TargetGoldColor;
            Color textCol = (data.requireExactTargetMatch && i != 3) ? Color.white : new Color(0.15f, 0.10f, 0f, 1f);
            DrawToolButton(tool, $"★ Target {i + 1}", bg, textCol);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawToolButton(PaintTool tool, string label, Color tintColor, Color textColor)
    {
        bool isSelected = activeTool == tool;
        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = isSelected ? tintColor : Color.Lerp(tintColor, new Color(0.35f, 0.35f, 0.35f, 1f), 0.65f);

        GUIStyle style = new GUIStyle(GUI.skin.button)
        {
            fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal,
            fontSize = 11
        };

        if (isSelected)
        {
            style.normal.textColor = textColor;
            style.hover.textColor = textColor;
            style.active.textColor = textColor;
            style.focused.textColor = textColor;
        }

        string displayLabel = isSelected ? $"[{label}]" : label;
        if (GUILayout.Button(displayLabel, style, GUILayout.Height(24f)))
        {
            activeTool = tool;
        }

        GUI.backgroundColor = prevBg;
    }

    // ──────────────────────────────────────────────
    //  INPUT HANDLING (LEFT-CLICK / DRAG / RIGHT-CLICK)
    // ──────────────────────────────────────────────

    private void HandleGridInput(LevelData data, Rect gridRect, int w, int h, float cellSize)
    {
        Event e = Event.current;
        int controlID = GUIUtility.GetControlID(FocusType.Passive);

        bool insideGrid = gridRect.Contains(e.mousePosition);
        if (insideGrid && EditorWindow.mouseOverWindow != null)
        {
            EditorWindow.mouseOverWindow.wantsMouseMove = true;
        }

        Vector2Int cellUnderMouse = insideGrid ? MouseToGridCell(e.mousePosition, gridRect, w, h, cellSize) : new Vector2Int(-1, -1);

        if (e.type == EventType.MouseMove || e.type == EventType.Repaint)
        {
            if (hoveredCell != cellUnderMouse)
            {
                hoveredCell = cellUnderMouse;
                Repaint();
            }
        }

        switch (e.type)
        {
            case EventType.MouseDown:
                if (!insideGrid) break;

                if (e.button == 0)
                {
                    GUIUtility.hotControl = controlID;
                    lastDraggedCell = cellUnderMouse;

                    if (activeTool == PaintTool.Wall)
                    {
                        isDraggingWall = true;
                        dragAddsWall = !HasWall(data, cellUnderMouse);
                        ApplyWallPaint(data, cellUnderMouse, dragAddsWall);
                    }
                    else
                    {
                        isDraggingWall = false;
                        ApplyToolToCell(data, cellUnderMouse, activeTool);
                    }

                    e.Use();
                }
                else if (e.button == 1)
                {
                    ShowCellContextMenu(data, cellUnderMouse);
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl == controlID && e.button == 0 && insideGrid)
                {
                    if (cellUnderMouse != lastDraggedCell)
                    {
                        lastDraggedCell = cellUnderMouse;

                        if (activeTool == PaintTool.Wall && isDraggingWall)
                        {
                            ApplyWallPaint(data, cellUnderMouse, dragAddsWall);
                        }
                        else
                        {
                            ApplyToolToCell(data, cellUnderMouse, activeTool);
                        }
                    }

                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == controlID)
                {
                    GUIUtility.hotControl = 0;
                    isDraggingWall = false;
                    lastDraggedCell = new Vector2Int(-1, -1);
                    e.Use();
                }
                break;
        }
    }

    private Vector2Int MouseToGridCell(Vector2 mousePos, Rect gridRect, int w, int h, float cellSize)
    {
        int x = Mathf.Clamp(Mathf.FloorToInt((mousePos.x - gridRect.x) / cellSize), 0, w - 1);
        int rowFromTop = Mathf.Clamp(Mathf.FloorToInt((mousePos.y - gridRect.y) / cellSize), 0, h - 1);
        int y = h - 1 - rowFromTop;
        return new Vector2Int(x, y);
    }

    private void ShowCellContextMenu(LevelData data, Vector2Int pos)
    {
        GenericMenu menu = new GenericMenu();
        bool isWall = HasWall(data, pos);

        menu.AddItem(new GUIContent(isWall ? "Remove Wall" : "Place Wall"), false, () =>
        {
            ApplyWallPaint(data, pos, !isWall);
            Repaint();
        });

        menu.AddSeparator("");

        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            menu.AddItem(new GUIContent($"Set Block {idx + 1} Start ({BlockNames[idx]})"), false, () =>
            {
                SetBlockStart(data, idx, pos);
                Repaint();
            });
        }

        menu.AddSeparator("");

        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            menu.AddItem(new GUIContent($"Set Target {idx + 1} Destination"), false, () =>
            {
                SetTargetPosition(data, idx, pos);
                Repaint();
            });
        }

        menu.ShowAsContext();
    }

    // ──────────────────────────────────────────────
    //  DATA MUTATION (WITH UNDO & CLEANUP)
    // ──────────────────────────────────────────────

    private void ApplyToolToCell(LevelData data, Vector2Int pos, PaintTool tool)
    {
        switch (tool)
        {
            case PaintTool.Wall:
                ApplyWallPaint(data, pos, !HasWall(data, pos));
                break;
            case PaintTool.EraseWall:
                ApplyWallPaint(data, pos, false);
                break;
            case PaintTool.Block1:
            case PaintTool.Block2:
            case PaintTool.Block3:
            case PaintTool.Block4:
                SetBlockStart(data, (int)tool - (int)PaintTool.Block1, pos);
                break;
            case PaintTool.Target1:
            case PaintTool.Target2:
            case PaintTool.Target3:
            case PaintTool.Target4:
                SetTargetPosition(data, (int)tool - (int)PaintTool.Target1, pos);
                break;
        }
    }

    private void ApplyWallPaint(LevelData data, Vector2Int pos, bool shouldHaveWall)
    {
        // Prevent placing a wall directly on top of a block start or target
        if (shouldHaveWall && (FindBlockStartIndex(data, pos) >= 0 || FindTargetIndex(data, pos) >= 0))
        {
            return;
        }

        bool currentlyWall = HasWall(data, pos);
        if (currentlyWall == shouldHaveWall) return;

        Undo.RecordObject(data, shouldHaveWall ? "Add Level Wall" : "Remove Level Wall");

        List<Vector2Int> walls = data.wallPositions != null
            ? new List<Vector2Int>(data.wallPositions)
            : new List<Vector2Int>();

        if (shouldHaveWall)
        {
            if (!walls.Contains(pos))
                walls.Add(pos);
        }
        else
        {
            walls.RemoveAll(p => p == pos);
        }

        data.wallPositions = walls.ToArray();
        MarkDataDirty(data);
    }

    private void SetBlockStart(LevelData data, int blockIndex, Vector2Int pos)
    {
        bool needsResize = data.blockStartPositions == null || data.blockStartPositions.Length != 4;
        if (!needsResize && data.blockStartPositions[blockIndex] == pos && !HasWall(data, pos))
            return;

        Undo.RecordObject(data, $"Move Block {blockIndex + 1} Start");
        EnsureFixedArraySize(ref data.blockStartPositions, 4);

        Vector2Int oldPos = data.blockStartPositions[blockIndex];

        // If another block is already at pos, swap their positions so blocks never stack
        int existingIndex = System.Array.FindIndex(data.blockStartPositions, p => p == pos);
        if (existingIndex >= 0 && existingIndex != blockIndex)
        {
            data.blockStartPositions[existingIndex] = oldPos;
        }

        data.blockStartPositions[blockIndex] = pos;

        // Remove any wall on the destination cell
        RemoveWallSilent(data, pos);

        MarkDataDirty(data);
    }

    private void SetTargetPosition(LevelData data, int targetIndex, Vector2Int pos)
    {
        bool needsResize = data.blockTargetPositions == null || data.blockTargetPositions.Length != 4;
        if (!needsResize && data.blockTargetPositions[targetIndex] == pos && !HasWall(data, pos))
            return;

        Undo.RecordObject(data, $"Move Target {targetIndex + 1}");
        EnsureFixedArraySize(ref data.blockTargetPositions, 4);

        Vector2Int oldPos = data.blockTargetPositions[targetIndex];

        // If another target is already at pos, swap their positions so targets never stack
        int existingIndex = System.Array.FindIndex(data.blockTargetPositions, p => p == pos);
        if (existingIndex >= 0 && existingIndex != targetIndex)
        {
            data.blockTargetPositions[existingIndex] = oldPos;
        }

        data.blockTargetPositions[targetIndex] = pos;

        // Remove any wall on the destination cell
        RemoveWallSilent(data, pos);

        MarkDataDirty(data);
    }

    private void RemoveWallSilent(LevelData data, Vector2Int pos)
    {
        if (data.wallPositions == null) return;
        List<Vector2Int> walls = new List<Vector2Int>(data.wallPositions);
        if (walls.RemoveAll(p => p == pos) > 0)
        {
            data.wallPositions = walls.ToArray();
        }
    }

    private void EnsureFixedArraySize(ref Vector2Int[] arr, int size)
    {
        if (arr == null || arr.Length != size)
        {
            Vector2Int[] resized = new Vector2Int[size];
            if (arr != null)
            {
                int copyLen = Mathf.Min(arr.Length, size);
                System.Array.Copy(arr, resized, copyLen);
            }
            arr = resized;
        }
    }

    private void MarkDataDirty(LevelData data)
    {
        solveStatusMessage = null;
        EditorUtility.SetDirty(data);
        serializedObject.Update();
        Repaint();
    }

    // ──────────────────────────────────────────────
    //  CELL RENDERING
    // ──────────────────────────────────────────────

    private void DrawGridCells(LevelData data, Rect gridRect, int w, int h, float cellSize)
    {
        HashSet<Vector2Int> independentWalls = LevelManager.GetIndependentWallPositions(data);

        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Clamp(Mathf.RoundToInt(cellSize * 0.42f), 10, 14)
        };

        for (int y = h - 1; y >= 0; y--)
        {
            for (int x = 0; x < w; x++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                float rx = gridRect.x + (x * cellSize);
                float ry = gridRect.y + ((h - 1 - y) * cellSize);
                Rect cellRect = new Rect(rx + 1f, ry + 1f, cellSize - 2f, cellSize - 2f);

                bool isWall = HasWall(data, pos);
                bool isIndependentWall = isWall && independentWalls.Contains(pos);
                int targetIdx = FindTargetIndex(data, pos);
                int startIdx = FindBlockStartIndex(data, pos);

                Color fillColor = EmptyCellColor;
                string text = "·";
                Color textColor = new Color(1f, 1f, 1f, 0.25f);

                if (isIndependentWall)
                {
                    fillColor = new Color(0.14f, 0.17f, 0.21f, 1f);
                    text = "✕";
                    textColor = Color.white;
                }
                else if (isWall)
                {
                    fillColor = WallCellColor;
                    text = "■";
                    textColor = new Color(0.12f, 0.13f, 0.15f, 1f);
                }

                if (targetIdx >= 0 && startIdx >= 0)
                {
                    // Both block start and target occupy this cell: draw gold border + block fill
                    EditorGUI.DrawRect(cellRect, TargetGoldColor);
                    Rect innerRect = new Rect(cellRect.x + 2.5f, cellRect.y + 2.5f, cellRect.width - 5f, cellRect.height - 5f);
                    EditorGUI.DrawRect(innerRect, BlockColors[startIdx % BlockColors.Length]);

                    text = $"{startIdx + 1}★";
                    textColor = (startIdx == 3) ? Color.black : Color.white;
                }
                else if (startIdx >= 0)
                {
                    fillColor = BlockColors[startIdx % BlockColors.Length];
                    EditorGUI.DrawRect(cellRect, fillColor);
                    text = (startIdx + 1).ToString();
                    textColor = (startIdx == 3) ? Color.black : Color.white;
                }
                else if (targetIdx >= 0)
                {
                    fillColor = data.requireExactTargetMatch
                        ? Color.Lerp(BlockColors[targetIdx % BlockColors.Length], TargetGoldColor, 0.35f)
                        : TargetGoldColor;
                    EditorGUI.DrawRect(cellRect, fillColor);
                    text = $"★{targetIdx + 1}";
                    textColor = (data.requireExactTargetMatch && targetIdx != 3)
                        ? Color.white
                        : new Color(0.20f, 0.14f, 0f, 1f);
                }
                else
                {
                    EditorGUI.DrawRect(cellRect, fillColor);
                }

                if (hoveredCell == pos)
                {
                    EditorGUI.DrawRect(cellRect, HoverHighlightColor);
                }

                labelStyle.normal.textColor = textColor;
                GUI.Label(cellRect, text, labelStyle);
            }
        }
    }

    // ──────────────────────────────────────────────
    //  STATUS, QUICK ACTIONS & VALIDATION
    // ──────────────────────────────────────────────

    private void DrawStatusAndActions(LevelData data)
    {
        string hoverInfo = hoveredCell.x >= 0
            ? $"Cell ({hoveredCell.x}, {hoveredCell.y})"
            : "Left-click/drag to paint  |  Right-click any cell for quick menu";

        EditorGUILayout.LabelField(hoverInfo, EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Clear All Walls", GUILayout.Height(22f)))
        {
            if (data.wallPositions != null && data.wallPositions.Length > 0)
            {
                Undo.RecordObject(data, "Clear All Walls");
                data.wallPositions = new Vector2Int[0];
                MarkDataDirty(data);
            }
        }

        if (GUILayout.Button("Clean Duplicate / Out-of-Bounds Walls", GUILayout.Height(22f)))
        {
            CleanWalls(data);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Check Solvability (BFS Solver)", GUILayout.Height(24f)))
        {
            LevelPuzzleSolver.SolveResult res = LevelPuzzleSolver.Solve(data);
            if (res.IsSolvable)
            {
                solveStatusMessage = $"Solvable in {res.Moves.Count} moves:\n{string.Join(" → ", res.Moves)}";
                solveStatusType = MessageType.Info;
            }
            else
            {
                solveStatusMessage = $"Not solvable (explored {res.ExploredStates} reachable states).";
                solveStatusType = MessageType.Error;
            }
        }

        if (GUILayout.Button("Open Level Generator", GUILayout.Height(24f)))
        {
            LevelGeneratorWindow.OpenWindow();
        }

        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(solveStatusMessage))
        {
            EditorGUILayout.HelpBox(solveStatusMessage, solveStatusType);
        }

        EditorGUILayout.HelpBox(
            "Controls: Select a tool above, then Left-Click (or Drag) on the grid.\n" +
            "Right-Click any cell to directly assign Wall, Block Start (1-4), or Target (1-4).\n" +
            "Legend:  1-4 = Block Starts  |  ★ = Target  |  1★ = Block on Target  |  ■ = Outline Wall  |  ✕ = Independent Wall",
            MessageType.None
        );
    }

    private void CleanWalls(LevelData data)
    {
        if (data.wallPositions == null) return;

        Undo.RecordObject(data, "Clean Level Walls");
        HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
        List<Vector2Int> cleaned = new List<Vector2Int>();

        foreach (Vector2Int wall in data.wallPositions)
        {
            bool inBounds = wall.x >= 0 && wall.x < data.gridWidth && wall.y >= 0 && wall.y < data.gridHeight;
            bool overlapsActor = FindBlockStartIndex(data, wall) >= 0 || FindTargetIndex(data, wall) >= 0;

            if (inBounds && !overlapsActor && seen.Add(wall))
            {
                cleaned.Add(wall);
            }
        }

        data.wallPositions = cleaned.ToArray();
        MarkDataDirty(data);
    }

    private void DrawValidationWarnings(LevelData data)
    {
        List<string> warnings = new List<string>();

        if (data.blockStartPositions == null || data.blockStartPositions.Length != 4)
        {
            warnings.Add("Block Start Positions should have exactly 4 entries.");
        }
        else
        {
            for (int i = 0; i < 4; i++)
            {
                Vector2Int p = data.blockStartPositions[i];
                if (!IsInBounds(data, p))
                    warnings.Add($"Block {i + 1} Start ({p.x}, {p.y}) is outside the {data.gridWidth}x{data.gridHeight} grid.");
                if (HasWall(data, p))
                    warnings.Add($"Block {i + 1} Start ({p.x}, {p.y}) overlaps a wall.");
            }
        }

        if (data.blockTargetPositions == null || data.blockTargetPositions.Length != 4)
        {
            warnings.Add("Block Target Positions should have exactly 4 entries.");
        }
        else
        {
            for (int i = 0; i < 4; i++)
            {
                Vector2Int p = data.blockTargetPositions[i];
                if (!IsInBounds(data, p))
                    warnings.Add($"Target {i + 1} ({p.x}, {p.y}) is outside the {data.gridWidth}x{data.gridHeight} grid.");
                if (HasWall(data, p))
                    warnings.Add($"Target {i + 1} ({p.x}, {p.y}) overlaps a wall.");
            }
        }

        if (data.wallPositions != null)
        {
            HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
            int duplicates = 0;
            int outOfBounds = 0;

            foreach (Vector2Int w in data.wallPositions)
            {
                if (!IsInBounds(data, w)) outOfBounds++;
                if (!seen.Add(w)) duplicates++;
            }

            if (duplicates > 0)
                warnings.Add($"{duplicates} duplicate wall coordinate(s) found. Click 'Clean Duplicate / Out-of-Bounds Walls'.");
            if (outOfBounds > 0)
                warnings.Add($"{outOfBounds} wall(s) are outside the {data.gridWidth}x{data.gridHeight} grid.");
        }

        if (warnings.Count > 0)
        {
            EditorGUILayout.HelpBox(string.Join("\n", warnings), MessageType.Warning);
        }
    }

    // ──────────────────────────────────────────────
    //  LOOKUP HELPERS
    // ──────────────────────────────────────────────

    private static bool IsInBounds(LevelData data, Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < data.gridWidth && pos.y >= 0 && pos.y < data.gridHeight;
    }

    private static bool HasWall(LevelData data, Vector2Int pos)
    {
        return data.wallPositions != null && System.Array.Exists(data.wallPositions, p => p == pos);
    }

    private static int FindBlockStartIndex(LevelData data, Vector2Int pos)
    {
        if (data.blockStartPositions == null) return -1;
        return System.Array.FindIndex(data.blockStartPositions, p => p == pos);
    }

    private static int FindTargetIndex(LevelData data, Vector2Int pos)
    {
        if (data.blockTargetPositions == null) return -1;
        return System.Array.FindIndex(data.blockTargetPositions, p => p == pos);
    }
}

/// <summary>
/// Utility menu to clean any stray/leftover preview objects from the scene.
/// </summary>
public static class LevelSceneCleaner
{
    [MenuItem("Tools/4Blocks/Clean Background Preview Objects")]
    public static void CleanPreviewObjects()
    {
        int count = 0;
        GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject go in all)
        {
            if (go == null) continue;
            string n = go.name;
            if (n == "PuzzleBoard" || n.StartsWith("Target_") || n.StartsWith("Block_") || n.StartsWith("Wall_") || n.StartsWith("WallCross_") || n.StartsWith("GridCell_"))
            {
                Undo.DestroyObjectImmediate(go);
                count++;
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
        );
        Debug.Log($"Cleaned {count} preview objects from scene.");
    }
}
