using UnityEngine;

/// <summary>
/// Editor‑only component that draws the wall cells of a <c>GridSystem</c> as gizmos.
/// Attach it to the same GameObject that holds the GridSystem at runtime.
/// The script lives in an <c>Editor</c> folder so it is excluded from builds.
/// </summary>
[ExecuteInEditMode]
public class DebugGridGizmo : MonoBehaviour
{
    // Customize gizmo colour/size via inspector if desired.
    public Color WallColor = Color.red;
    public float CellSize = 1f;

    private void OnDrawGizmos()
    {
        // Find the GridSystem component on this GameObject (or its children).
        var grid = GetComponentInChildren<GridSystem>();
        if (grid == null) return;

        // Assume GridSystem exposes a 2‑D bool array called "walls" (true = wall).
        // Use reflection only if the exact field name is unknown – safe fallback.
        var gridType = grid.GetType();
        var wallsField = gridType.GetField("wallGrid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) 
                         ?? gridType.GetField("walls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (wallsField == null) return;
        var walls = wallsField.GetValue(grid) as bool[,];
        if (walls == null) return;

        int rows = walls.GetLength(0);
        int cols = walls.GetLength(1);
        Vector3 origin = grid.transform.position;

        Gizmos.color = WallColor;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (walls[r, c])
                {
                    // Draw a cube at the cell centre.
                    Vector3 cellPos = origin + new Vector3(c * CellSize, 0f, r * CellSize);
                    Gizmos.DrawCube(cellPos, Vector3.one * CellSize * 0.9f);
                }
            }
        }
    }
}

